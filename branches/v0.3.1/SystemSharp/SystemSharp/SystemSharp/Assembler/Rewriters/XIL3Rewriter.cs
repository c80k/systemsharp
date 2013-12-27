/**
 * Copyright 2011-2013 Christian Köllner
 * 
 * This file is part of System#.
 *
 * System# is free software: you can redistribute it and/or modify it under 
 * the terms of the GNU Lesser General Public License (LGPL) as published 
 * by the Free Software Foundation, either version 3 of the License, or (at 
 * your option) any later version.
 *
 * System# is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more 
 * details.
 *
 * You should have received a copy of the GNU General Public License along 
 * with System#. If not, see http://www.gnu.org/licenses/lgpl.html.
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Meta;

namespace SystemSharp.Assembler.Rewriters
{
    /// <summary>
    /// This class provides a default implementation of a XIL-3 code transformation.
    /// It alleviates the user from many complicated bookkeeping tasks, such as remapping
    /// operand slots and retargeting branch labels.
    /// </summary>
    public class XIL3Rewriter : IXIL3Rewriter
    {
        /// <summary>
        /// Input instruction list
        /// </summary>
        public IList<XIL3Instr> InInstructions { get; private set; }

        /// <summary>
        /// Output instruction list
        /// </summary>
        public List<XIL3Instr> OutInstructions { get; private set; }

        private Dictionary<int, int> _remap;
        private List<Tuple<BranchLabel, BranchLabel>> _remapLabels;
        private Dictionary<int, int> _slotRemap;
        private int _curSlot;
        private Dictionary<string, Action<XIL3Instr>> _handlers;
        private IList<TypeDescriptor> _orgSlotTypes;
        private List<TypeDescriptor> _slotTypes;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        public XIL3Rewriter()
        {
            OutInstructions = new List<XIL3Instr>();
            _remap = new Dictionary<int, int>();
            _remapLabels = new List<Tuple<BranchLabel, BranchLabel>>();
            _slotRemap = new Dictionary<int, int>();
            _handlers = new Dictionary<string, Action<XIL3Instr>>();
            _slotTypes = new List<TypeDescriptor>();
            RegisterDefaultHandlers();
        }

        /// <summary>
        /// Returns all slot datatypes associated with the input instructions
        /// </summary>
        public IList<TypeDescriptor> InSlotTypes
        {
            get { return _orgSlotTypes; }
        }

        /// <summary>
        /// Returns all slot datatypes associated with the output instructions
        /// </summary>
        public IList<TypeDescriptor> OutSlotTypes
        {
            get { return _slotTypes; }
        }

        /// <summary>
        /// Returns the index which will be assigned to the next emitted instruction
        /// </summary>
        protected int NextOutputInstructionIndex
        {
            get { return OutInstructions.Count; }
        }

        /// <summary>
        /// Returns the index of the last emitted instruction
        /// </summary>
        protected int LastOutputInstructionIndex
        {
            get { return NextOutputInstructionIndex - 1; }
        }

        /// <summary>
        /// Returns current input instruction
        /// </summary>
        protected XIL3Instr CurInstr { get; private set; }

        /// <summary>
        /// Emits an instruction and assigns an index to it
        /// </summary>
        /// <param name="i">instruction to emit</param>
        protected void Emit(XIL3Instr i)
        {
            i.Index = NextOutputInstructionIndex;
            i.CILRef = CurInstr.CILRef;
            OutInstructions.Add(i);
        }

        /// <summary>
        /// Allocates a new instruction output slot
        /// </summary>
        /// <param name="type">datatype of slot</param>
        /// <returns>index of newly allocated output slot</returns>
        protected virtual int AllocSlot(TypeDescriptor type)
        {
            _slotTypes.Add(type);
            return _curSlot++;
        }

        /// <summary>
        /// Announces that a slot of the input instruction list and a slot of the output instruction list belong together.
        /// </summary>
        /// <param name="oldSlot">slot of the input instruction list</param>
        /// <param name="newSlot">slot of the output instruction list</param>
        protected void RemapSlot(int oldSlot, int newSlot)
        {
            _slotRemap[oldSlot] = newSlot;
        }

        /// <summary>
        /// For a given slot of the input instruction list, returns the corresponding slot of the output instruction list.
        /// </summary>
        /// <param name="oldSlot">slot of the input instruction list</param>
        /// <returns>corresponding slot of the output instruction list</returns>
        protected int GetMappedSlot(int oldSlot)
        {
            return _slotRemap[oldSlot];
        }

        /// <summary>
        /// Retargets a branch label used in the input instruction list to the new location inside the output instruction list.
        /// </summary>
        /// <param name="label">branch label of input instruction list</param>
        /// <returns>corresponding branch label of output instruction list</returns>
        protected BranchLabel Retarget(BranchLabel label)
        {
            BranchLabel newLabel = new BranchLabel();
            _remapLabels.Add(new Tuple<BranchLabel, BranchLabel>(label, newLabel));
            return newLabel;
        }

        /// <summary>
        /// Registers a handler function for a specific XIL opcode
        /// </summary>
        /// <param name="icode">XIL opcode</param>
        /// <param name="handler">handler function to call for each occurence of specified opcode</param>
        protected void SetHandler(string icode, Action<XIL3Instr> handler)
        {
            _handlers[icode] = handler;
        }

        /// <summary>
        /// Remaps the data dependencies from input instructions to output instructions.
        /// </summary>
        /// <param name="preds">dependencies of input instruction</param>
        /// <returns>corresponding dependencies for output instruction</returns>
        protected InstructionDependency[] RemapPreds(InstructionDependency[] preds)
        {
            var result = new List<InstructionDependency>();
            foreach (var pred in preds)
            {
                int prev, cur;
                if (!_remap.TryGetValue(pred.PredIndex, out cur))
                    continue;
                if (!_remap.TryGetValue(pred.PredIndex - 1, out prev))
                    prev = cur - 1;
                for (int i = prev + 1; i <= cur; i++)
                    result.Add(pred.Remap(i));
            }
            return result.ToArray();
        }

        /// <summary>
        /// Given the result slots of an input instruction, allocates new slots on the output side and
        /// remembers the mapping between input-side and output-side slots.
        /// </summary>
        /// <param name="slots">result slots of input instruction</param>
        /// <returns>newly allocated result slots for output instruction</returns>
        protected int[] RemapResultSlots(int[] slots)
        {
            int[] rslots = new int[slots.Length];
            for (int j = 0; j < rslots.Length; j++)
            {
                int rslot = AllocSlot(_orgSlotTypes[slots[j]]);
                RemapSlot(slots[j], rslot);
                rslots[j] = rslot;
            }
            return rslots;
        }

        /// <summary>
        /// Maps the operand slots of an input instruction to the corresponding operand slots for the output side.
        /// </summary>
        /// <param name="slots">operand slots of input instruction</param>
        /// <returns>corresponding operand slots at output side</returns>
        protected int[] RemapOperandSlots(int[] slots)
        {
            int[] oslots = new int[slots.Length];
            for (int j = 0; j < oslots.Length; j++)
            {
                oslots[j] = GetMappedSlot(slots[j]);
            }
            return oslots;
        }

        /// <summary>
        /// The default handler is applied to any non-branching instruction when there is no more specific handler registered.
        /// </summary>
        /// <param name="i">instruction to process</param>
        virtual protected void ProcessDefault(XIL3Instr i)
        {
            int[] rslots = RemapResultSlots(i.ResultSlots);
            int[] oslots = RemapOperandSlots(i.OperandSlots);
            var preds = RemapPreds(i.Preds);
            Emit(i.Command.Create3AC(preds, oslots, rslots));
        }

        /// <summary>
        /// The default branch handler is applied to any branching instruction when there is no more specific handler registered.
        /// </summary>
        /// <param name="i">instruction to process</param>
        virtual protected void ProcessBranch(XIL3Instr i)
        {
            BranchLabel label = (BranchLabel)i.StaticOperand;
            BranchLabel newLabel = Retarget(label);
            XILInstr xi = new XILInstr(i.Name, newLabel);
            int[] rslots = RemapResultSlots(i.ResultSlots);
            int[] oslots = RemapOperandSlots(i.OperandSlots);
            var preds = RemapPreds(i.Preds);
            Emit(xi.Create3AC(preds, oslots, rslots));
        }

        /// <summary>
        /// This method is called for any instruction. Its default behavior is to lookup handler inside the handler dictionary
        /// and redirect the processing to that handler.
        /// </summary>
        /// <param name="i">instruction to process</param>
        virtual protected void ProcessInstruction(XIL3Instr i)
        {
            Action<XIL3Instr> handler = _handlers[i.Name];
            handler(i);
        }

        private void RegisterDefaultHandlers()
        {
            foreach (string icode in InstructionCodes.AllCodes)
            {
                SetHandler(icode, ProcessDefault);
            }
            SetHandler(InstructionCodes.BranchIfFalse, ProcessBranch);
            SetHandler(InstructionCodes.BranchIfTrue, ProcessBranch);
            SetHandler(InstructionCodes.Goto, ProcessBranch);
        }

        /// <summary>
        /// This method is called after processing all inputs instructions. Its default behavior is to
        /// adjust all output branch labels with their actual targets.
        /// </summary>
        protected virtual void PostProcess()
        {
            foreach (Tuple<BranchLabel, BranchLabel> pair in _remapLabels)
            {
                pair.Item2.InstructionIndex = _remap[pair.Item1.InstructionIndex];
            }
        }

        /// <summary>
        /// This method implements the complete transformation.
        /// </summary>
        protected virtual void Rewrite()
        {
            _remap[-1] = -1;
            foreach (XIL3Instr i in InInstructions)
            {
                CurInstr = i;
                ProcessInstruction(i);
                _remap[i.Index] = LastOutputInstructionIndex;
            }
            PostProcess();
            CheckSanityOfResult();
        }

        public virtual XIL3Function Rewrite(XIL3Function func)
        {
            InInstructions = func.Instructions;
            _orgSlotTypes = func.SlotTypes;
            Rewrite();
            return new XIL3Function(func.Name, func.Arguments, func.Locals,
                OutInstructions.ToArray(),
                OutSlotTypes.ToArray());
        }

        private void CheckSanityOfResult()
        {
            foreach (XIL3Instr xil3i in OutInstructions)
            {
                Debug.Assert(xil3i != null);
                Debug.Assert(xil3i.ToString() != null);
                Debug.Assert(xil3i.OperandSlots.All(s => s >= 0 && s < _slotTypes.Count));
                Debug.Assert(xil3i.ResultSlots.All(s => s >= 0 && s < _slotTypes.Count));
            }
        }
    }
}

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
    public class XIL3Rewriter : IXIL3Rewriter
    {
        public IList<XIL3Instr> InInstructions { get; private set; }
        public List<XIL3Instr> OutInstructions { get; private set; }

        private Dictionary<int, int> _remap;
        private List<Tuple<BranchLabel, BranchLabel>> _remapLabels;
        private Dictionary<int, int> _slotRemap;
        private int _curSlot;
        private Dictionary<string, Action<XIL3Instr>> _handlers;
        private IList<TypeDescriptor> _orgSlotTypes;
        private List<TypeDescriptor> _slotTypes;

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

        public IList<TypeDescriptor> InSlotTypes
        {
            get { return _orgSlotTypes; }
        }

        public IList<TypeDescriptor> OutSlotTypes
        {
            get { return _slotTypes; }
        }

        protected int NextOutputInstructionIndex
        {
            get { return OutInstructions.Count; }
        }

        protected int LastOutputInstructionIndex
        {
            get { return NextOutputInstructionIndex - 1; }
        }

        protected XIL3Instr CurInstr { get; private set; }

        protected void Emit(XIL3Instr i)
        {
            i.Index = NextOutputInstructionIndex;
            i.CILRef = CurInstr.CILRef;
            OutInstructions.Add(i);
        }

        protected virtual int AllocSlot(TypeDescriptor type)
        {
            _slotTypes.Add(type);
            return _curSlot++;
        }

        protected void RemapSlot(int oldSlot, int newSlot)
        {
            _slotRemap[oldSlot] = newSlot;
        }

        protected int GetMappedSlot(int oldSlot)
        {
            return _slotRemap[oldSlot];
        }

        protected BranchLabel Retarget(BranchLabel label)
        {
            BranchLabel newLabel = new BranchLabel();
            _remapLabels.Add(new Tuple<BranchLabel, BranchLabel>(label, newLabel));
            return newLabel;
        }

        protected void SetHandler(string icode, Action<XIL3Instr> handler)
        {
            _handlers[icode] = handler;
        }

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

        protected int[] RemapOperandSlots(int[] slots)
        {
            int[] oslots = new int[slots.Length];
            for (int j = 0; j < oslots.Length; j++)
            {
                oslots[j] = GetMappedSlot(slots[j]);
            }
            return oslots;
        }

        virtual protected void ProcessDefault(XIL3Instr i)
        {
            int[] rslots = RemapResultSlots(i.ResultSlots);
            int[] oslots = RemapOperandSlots(i.OperandSlots);
            var preds = RemapPreds(i.Preds);
            Emit(i.Command.Create3AC(preds, oslots, rslots));
        }

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

        protected virtual void PostProcess()
        {
            foreach (Tuple<BranchLabel, BranchLabel> pair in _remapLabels)
            {
                pair.Item2.InstructionIndex = _remap[pair.Item1.InstructionIndex];
            }
        }

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

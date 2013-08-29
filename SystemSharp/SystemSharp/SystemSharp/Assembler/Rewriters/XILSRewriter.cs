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
using System.Linq;
using System.Text;
using SystemSharp.Meta;

namespace SystemSharp.Assembler.Rewriters
{
    public class XILSRewriter
    {
        public IList<XILSInstr> InInstructions { get; private set; }
        public List<XILSInstr> OutInstructions { get; private set; }

        private Dictionary<int, int> _remap;
        private List<Tuple<BranchLabel, BranchLabel>> _remapLabels;
        private Dictionary<string, Action<XILSInstr>> _handlers;
        private Stack<TypeDescriptor> _typeStack;
        private List<XILSInstr> _curBB;

        public XILSRewriter(IList<XILSInstr> instructions)
        {
            InInstructions = instructions;
            OutInstructions = new List<XILSInstr>();
            _remap = new Dictionary<int, int>();
            _remapLabels = new List<Tuple<BranchLabel, BranchLabel>>();
            _handlers = new Dictionary<string, Action<XILSInstr>>();
            _typeStack = new Stack<TypeDescriptor>();
            _curBB = new List<XILSInstr>();
            RegisterDefaultHandlers();
        }

        protected int NextOutputInstructionIndex
        {
            get { return OutInstructions.Count; }
        }

        protected int LastOutputInstructionIndex
        {
            get { return NextOutputInstructionIndex - 1; }
        }

        protected Stack<TypeDescriptor> TypeStack
        {
            get { return _typeStack; }
        }

        protected virtual void Emit(XILSInstr xilsi)
        {
            xilsi.Index = NextOutputInstructionIndex;
            if (CurInstr != null)
                xilsi.CILRef = CurInstr.CILRef;
            int numOperands = xilsi.OperandTypes.Length;
            for (int i = numOperands - 1; i >= 0; i--)
            {
                TypeDescriptor t = _typeStack.Pop();
                if (!t.Equals(xilsi.OperandTypes[i]))
                    throw new ArgumentException("Incompatible operand types");
            }
            foreach (TypeDescriptor result in xilsi.ResultTypes)
            {
                _typeStack.Push(result);
            }
            OutInstructions.Add(xilsi);
            if (xilsi.Name == InstructionCodes.BranchIfFalse ||
                xilsi.Name == InstructionCodes.BranchIfTrue ||
                xilsi.Name == InstructionCodes.Goto)
            {
                _curBB.Clear();
            }
            else
            {
                _curBB.Add(xilsi);
            }
        }

        protected BranchLabel Retarget(BranchLabel label)
        {
            BranchLabel newLabel = new BranchLabel();
            _remapLabels.Add(new Tuple<BranchLabel, BranchLabel>(label, newLabel));
            return newLabel;
        }

        protected void SetHandler(string icode, Action<XILSInstr> handler)
        {
            _handlers[icode] = handler;
        }

        protected InstructionDependency[] RemapPreds(InstructionDependency[] preds)
        {
            var result = new List<InstructionDependency>();
            foreach (var pred in preds)
            {
                int next, cur;
                if (!_remap.TryGetValue(pred.PredIndex, out cur))
                    continue;
                if (!_remap.TryGetValue(pred.PredIndex + 1, out next))
                    next = cur + 1;
                for (int i = cur; i < next; i++)
                    result.Add(pred.Remap(i));
            }
            return result.ToArray();
        }

        virtual protected void ProcessDefault(XILSInstr i)
        {
            var preds = RemapPreds(i.Preds);
            Emit(i.Command.CreateStk(preds, i.OperandTypes, i.ResultTypes));
        }

        virtual protected void ProcessBranch(XILSInstr i)
        {
            BranchLabel label = (BranchLabel)i.StaticOperand;
            BranchLabel newLabel = Retarget(label);
            XILInstr xi = new XILInstr(i.Name, newLabel);
            var preds = RemapPreds(i.Preds);
            Emit(xi.CreateStk(preds, i.OperandTypes, i.ResultTypes));
        }

        virtual protected void ProcessInstruction(XILSInstr i)
        {
            Action<XILSInstr> handler = _handlers[i.Name];
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

        protected virtual void PreProcess()
        {
        }

        protected virtual void PostProcess()
        {
            foreach (Tuple<BranchLabel, BranchLabel> pair in _remapLabels)
            {
                pair.Item2.InstructionIndex = _remap[pair.Item1.InstructionIndex];
            }
        }

        protected XILSInstr CurInstr { get; private set; }

        protected IEnumerable<XILSInstr> CurBB
        {
            get { return _curBB; }
        }

        public void Rewrite()
        {
            PreProcess();
            _remap[-1] = -1;
            foreach (XILSInstr i in InInstructions)
            {
                _remap[i.Index] = NextOutputInstructionIndex;
                CurInstr = i;
                ProcessInstruction(i);
            }
            PostProcess();
        }
    }
}

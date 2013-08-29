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
using SystemSharp.Meta;

namespace SystemSharp.Assembler
{
    class StkTo3ACImpl
    {
        private static readonly InstructionDependency[] empty = new InstructionDependency[0];

        private XILSFunction _func;
        private Stack<int> _stack = new Stack<int>();
        private Dictionary<int, int> _indexMap = new Dictionary<int, int>();
        private List<TypeDescriptor> _slotTypes = new List<TypeDescriptor>();
        private int _curSlot;

        private StkTo3ACImpl(XILSFunction func)
        {
            _func = func;
        }

        private void Remap(XILSInstr xilsi, out InstructionDependency[] preds, out int[] oslots, out int[] rslots)
        {
            oslots = new int[xilsi.OperandTypes.Length];
            rslots = new int[xilsi.ResultTypes.Length];
            for (int i = oslots.Length - 1; i >= 0; i--)
                oslots[i] = _stack.Pop();
            for (int i = 0; i < rslots.Length; i++)
            {
                rslots[i] = _curSlot;
                _stack.Push(_curSlot);
                Debug.Assert(xilsi.ResultTypes[i] != null);
                _slotTypes.Add(xilsi.ResultTypes[i]);
                ++_curSlot;
            }
            preds = xilsi.Preds.SelectMany(i =>
            {
                int j;
                if (_indexMap.TryGetValue(i.PredIndex, out j))
                    return new InstructionDependency[] { i.Remap(j) };
                else
                    return empty;
            }).ToArray();
        }

        private XIL3Function Run()
        {
            List<XIL3Instr> xil3is = new List<XIL3Instr>();
            List<BranchLabel> targets = new List<BranchLabel>();
            foreach (XILSInstr xilsi in _func.Instructions)
            {
                switch (xilsi.Name)
                {
                    case InstructionCodes.Pop:
                        _stack.Pop();
                        break;

                    case InstructionCodes.Dup:
                        _stack.Push(_stack.Peek());
                        break;

                    case InstructionCodes.Swap:
                        {
                            int a = _stack.Pop();
                            int b = _stack.Pop();
                            _stack.Push(a);
                            _stack.Push(b);
                        }
                        break;

                    case InstructionCodes.Dig:
                        {
                            int pos = (int)xilsi.StaticOperand;
                            Stack<int> tmpStack = new Stack<int>();
                            for (int i = 0; i < pos; i++)
                                tmpStack.Push(_stack.Pop());
                            int dig = _stack.Pop();
                            for (int i = 0; i < pos; i++)
                                _stack.Push(tmpStack.Pop());
                            _stack.Push(dig);
                        }
                        break;

                    default:
                        {
                            int[] oslots, rslots;
                            InstructionDependency[] preds;
                            Remap(xilsi, out preds, out oslots, out rslots);
                            XIL3Instr xil3i;
                            switch (xilsi.Name)
                            {
                                case InstructionCodes.Goto:
                                case InstructionCodes.BranchIfFalse:
                                case InstructionCodes.BranchIfTrue:
                                    {
                                        BranchLabel orgTarget = (BranchLabel)xilsi.StaticOperand;
                                        BranchLabel target = new BranchLabel()
                                        {
                                            InstructionIndex = orgTarget.InstructionIndex
                                        };
                                        targets.Add(target);
                                        xil3i = new XILInstr(xilsi.Name, target).Create3AC(preds, oslots, rslots);
                                    }
                                    break;

                                default:
                                    xil3i = xilsi.Command.Create3AC(preds, oslots, rslots);
                                    break;
                            }
                            xil3i.Index = xil3is.Count;
                            xil3i.CILRef = xilsi.CILRef;
                            xil3is.Add(xil3i);
                            _indexMap[xilsi.Index] = xil3i.Index;
                        }
                        break;
                }
            }
            foreach (BranchLabel target in targets)
            {
                target.InstructionIndex = _indexMap[target.InstructionIndex];
            }
            return new XIL3Function(_func.Name, _func.Arguments, _func.Locals, xil3is.ToArray(), _slotTypes.ToArray());
        }

        public static XIL3Function ToXIL3(XILSFunction xils)
        {
            StkTo3ACImpl conv = new StkTo3ACImpl(xils);
            return conv.Run();
        }
    }

    public static class StkTo3AC
    {
        public static XIL3Function ToXIL3(this XILSFunction xils)
        {
            return StkTo3ACImpl.ToXIL3(xils);
        }
    }
}

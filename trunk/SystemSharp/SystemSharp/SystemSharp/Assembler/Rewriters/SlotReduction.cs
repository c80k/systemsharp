/**
 * Copyright 2012-2013 Christian Köllner
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using GraphAlgorithms;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    public class SlotReducer : XIL3Rewriter
    {
        private XIL3Function _func;
        private XIL3InstructionInfo _xil3ii = new XIL3InstructionInfo();
        private int[] _preOrder;
        private int[] _lifeStart;
        private int[] _lifeEnd;
        private int[] _newSlotIndices;

        public SlotReducer()
        {
        }

        private void ComputePreOrder()
        {
            int icount = InInstructions.Count;
            _preOrder = new int[icount];
            for (int i = 0; i < icount; i++)
                _preOrder[i] = -1;
            Stack<int> s = new Stack<int>();
            int order = 0;
            for (int i = 0; i < icount; i++)
            {
                if (_preOrder[i] >= 0)
                    continue;
                s.Push(i);
                while (s.Any())
                {
                    int v = s.Pop();
                    if (_preOrder[v] < 0)
                    {
                        _preOrder[v] = order++;
                        IEnumerable<int> succs = 
                            _xil3ii.GetSuccessors(InInstructions[v]);
                        foreach (int succ in succs)
                        {
                            if (_preOrder[succ] < 0)
                                s.Push(succ);
                        }
                    }
                }
            }
        }

        private void ComputeLifetimes()
        {
            int icount = InInstructions.Count;
            int scount = InSlotTypes.Count;
            _lifeStart = new int[scount];
            _lifeEnd = new int[scount];
            for (int i = 0; i < scount; i++)
            {
                _lifeStart[i] = int.MaxValue;
                _lifeEnd[i] = int.MinValue;
            }
            for (int i = 0; i < icount; i++)
            {
                var xil3i = InInstructions[i];
                foreach (int oslot in xil3i.OperandSlots)
                {
                    _lifeEnd[oslot] = Math.Max(_lifeEnd[oslot], _preOrder[i]);
                }
                foreach (int rslot in xil3i.ResultSlots)
                {
                    Debug.Assert(_lifeStart[rslot] == int.MaxValue);
                    _lifeStart[rslot] = _preOrder[i];
                }
            }
        }

        private struct SlotEntry
        {
            public int Slot;
            public int LifeStart;

            public SlotEntry(int slot, int lifeStart)
            {
                Slot = slot;
                LifeStart = lifeStart;
            }
        }

        private class SlotComparer : IComparer<SlotEntry>
        {
            public int Compare(SlotEntry x, SlotEntry y)
            {
                return x.LifeStart - y.LifeStart;
            }
        }

        private void ComputeSlotSpilling()
        {
            // Just a stupid greedy algorithm...

            int scount = InSlotTypes.Count;
            var lifeList = new SortedSet<SlotEntry>(new SlotComparer());
            for (int i = 0; i < scount; i++)
            {
                lifeList.Add(new SlotEntry(i, _lifeStart[i]));
            }
            SlotEntry inf = new SlotEntry(-1, int.MaxValue);

            _newSlotIndices = new int[scount];
            while (lifeList.Any())
            {
                var node = lifeList.First();
                int cur = node.Slot;
                TypeDescriptor slotType = InSlotTypes[cur];
                int slotIndex = AllocSlot(slotType);
                _newSlotIndices[cur] = slotIndex;
                lifeList.Remove(node);

                int end = _lifeEnd[cur];
                var nextView = lifeList.GetViewBetween(new SlotEntry(-1, end + 1), inf);
                bool found;
                do
                {
                    found = false;
                    foreach (var nextSlot in nextView)
                    {
                        int next = nextSlot.Slot;
                        TypeDescriptor type = InSlotTypes[next];
                        if (type.Equals(slotType))
                        {
                            _newSlotIndices[next] = slotIndex;
                            lifeList.Remove(nextSlot);
                            end = _lifeEnd[next];
                            nextView = lifeList.GetViewBetween(new SlotEntry(-1, end + 1), inf);
                            found = true;
                            break;
                        }
                    }
                } while (found);
            }
        }

        private int[] RemapSlots(int[] slots)
        {
            Contract.Requires<ArgumentNullException>(slots != null);
            return slots.Select(s => _newSlotIndices[s]).ToArray();
        }

        protected override void ProcessInstruction(XIL3Instr i)
        {
            Emit(i.Command.Create3AC(
                i.Preds,
                RemapSlots(i.OperandSlots),
                RemapSlots(i.ResultSlots)));
        }

        protected override void Rewrite()
        {
            ComputePreOrder();
            ComputeLifetimes();
            ComputeSlotSpilling();
            base.Rewrite();
        }

        public override XIL3Function Rewrite(XIL3Function func)
        {
            _func = func;
            return base.Rewrite(func);
        }
    }

    public class ReduceSlots : IXIL3Rewriter
    {
        public override string ToString()
        {
            return "SlotReduction";
        }

        public XIL3Function Rewrite(XIL3Function func)
        {
            var rw = new SlotReducer();
            return rw.Rewrite(func);            
        }
    }
}

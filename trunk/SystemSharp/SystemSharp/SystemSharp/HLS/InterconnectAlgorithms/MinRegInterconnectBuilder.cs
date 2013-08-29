/**
 * Copyright 2011-2012 Christian Köllner
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
using SystemSharp.Components;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.DesignGen
{
    public class MinRegInterconnectBuilder: IInterconnectBuilder
    {
        private class FactoryImpl : IInterconnectBuilderFactory
        {
            public IInterconnectBuilder Create(Component host, IAutoBinder binder)
            {
                return new MinRegInterconnectBuilder(host, binder);
            }
        }

        private struct SlotEntry
        {
            public int Slot;
            public long LifeStart;

            public SlotEntry(int slot, long lifeStart)
            {
                Slot = slot;
                LifeStart = lifeStart;
            }
        }

        private class SlotComparer : IComparer<SlotEntry>
        {
            public int Compare(SlotEntry x, SlotEntry y)
            {
                long cmp = x.LifeStart - y.LifeStart;
                if (cmp == 0)
                    cmp = x.Slot - y.Slot;
                return (cmp == 0) ? 0 : ((cmp < 0) ? -1 : 1);
            }
        }

        private class SyncTemplate : AlgorithmTemplate
        {
            private MinRegInterconnectBuilder _icb;

            public SyncTemplate(MinRegInterconnectBuilder icb)
            {
                _icb = icb;
            }

            protected override void DeclareAlgorithm()
            {
                Signal<StdLogic> clkInst = _icb._binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                SignalRef clkRising = SignalRef.Create(clkInst.Descriptor, SignalRef.EReferencedProperty.RisingEdge);
                LiteralReference lrClkRising = new LiteralReference(clkRising);

                If(lrClkRising);
                {
                    for (int i = 0; i < _icb._regsCur.Length; i++)
                    {
                        if (_icb._regsCur[i] == null)
                            continue;

                        var slotCurInst = _icb._regsCur[i];
                        SignalRef slotCur = SignalRef.Create(slotCurInst, SignalRef.EReferencedProperty.Next);

                        var slotNextInst = _icb._regsNext[i];
                        SignalRef slotNext = SignalRef.Create(slotNextInst, SignalRef.EReferencedProperty.Cur);
                        LiteralReference lrSlotNext = new LiteralReference(slotNext);

                        Store(slotCur, lrSlotNext);
                    }
                }
                EndIf();
            }

            protected override string FunctionName
            {
                get { return "SyncFSM"; }
            }
        }

        private Component _host;
        private IAutoBinder _binder;
        private FlowMatrix _flowSpec;
        private TypeDescriptor[] _tempRegTypes;
        private int[] _regIndices;
        private FlowMatrix _realFlow;
        private SignalBase[] _regsCur;
        private SignalBase[] _regsNext;

        private SignalRef[] _tempRegs;
        private int[] _lifeStart;
        private int[] _lifeEnd;
        private int[] _slotAvail;

        private MinRegInterconnectBuilder(Component host, IAutoBinder binder)
        {
            _host = host;
            _binder = binder;
        }

        private void Init()
        {
            int scount = _flowSpec.FlowTargets.Select(t => t.GetTemporaryIndex()).Max() + 1;
            _tempRegs = new SignalRef[scount];
            _tempRegTypes = new TypeDescriptor[scount];
            foreach (var target in _flowSpec.FlowTargets)
            {
                if (!target.IsTemporary())
                    continue;
                int index = target.GetTemporaryIndex();
                _tempRegs[index] = target;
                _tempRegTypes[index] = target.Desc.ElementType;
            }
            _lifeStart = new int[scount];
            _lifeEnd = new int[scount];
            _slotAvail = new int[scount];
            for (int i = 0; i < scount; i++)
            {
                _lifeStart[i] = int.MaxValue;
                _lifeEnd[i] = int.MinValue;
                _slotAvail[i] = int.MaxValue;
            }
        }

        private void ComputeLifetimes()
        {
            for (int i = 0; i < _flowSpec.NumCSteps; i++)
            {
                var pflow = _flowSpec.GetFlow(i);
                foreach (var flow in pflow.Flows)
                {
                    int tindex = flow.Target.GetTemporaryIndex();
                    if (tindex >= 0)
                    {
                        _lifeStart[tindex] = Math.Min(_lifeStart[tindex], i);
                    }
                    var sflow = flow as SignalFlow;
                    if (sflow != null)
                    {
                        int sindex = sflow.Source.GetTemporaryIndex();
                        if (sindex >= 0)
                        {
                            _lifeEnd[sindex] = Math.Max(_lifeEnd[sindex], i + 1);
                        }
                    }
                }
            }
            for (int i = 0; i < _lifeStart.Length; i++)
            {
                Debug.Assert(!IsMayfly(i));
            }
        }

        private bool IsMayfly(int oslot)
        {
            return _lifeEnd[oslot] - _lifeStart[oslot] == 1;
        }

        private bool IsUnused(int oslot)
        {
            return _lifeEnd[oslot] < _lifeStart[oslot];
        }

        private void ComputeSpilling()
        {
            // Just a stupid greedy algorithm...

            int scount = _tempRegs.Length;

            _regIndices = new int[scount];
            var lifeList = new SortedSet<SlotEntry>(new SlotComparer());
            for (int i = 0; i < scount; i++)
            {
                _regIndices[i] = int.MinValue;
                lifeList.Add(new SlotEntry(i, _lifeStart[i]));
            }
            SlotEntry inf = new SlotEntry(-1, int.MaxValue);
            int nextReg = 0;

            while (lifeList.Any())
            {
                var node = lifeList.First();
                int cur = node.Slot;
                int slotIndex;
                if (IsUnused(cur))
                {
                    _regIndices[cur] = -1;
                    lifeList.Remove(node);
                    continue;
                }
                else
                {
                    slotIndex = nextReg++;
                    _regIndices[cur] = slotIndex;
                    lifeList.Remove(node);
                }                

                var type = _tempRegTypes[cur];
                long end = _lifeEnd[cur];
                var nextView = lifeList.GetViewBetween(new SlotEntry(-1, end), inf);
                bool found;
                do
                {
                    found = false;
                    foreach (var nextSlot in nextView)
                    {
                        int next = nextSlot.Slot;
                        var nextType = _tempRegTypes[next];
                        if (IsUnused(next) || type.Equals(nextType))
                        {
                            if (IsUnused(next))
                            {
                                _regIndices[next] = -1;
                                lifeList.Remove(nextSlot);
                            }
                            else if (type.Equals(nextType))
                            {
                                _regIndices[next] = slotIndex;
                                lifeList.Remove(nextSlot);
                                end = _lifeEnd[next];
                            }
                            nextView = lifeList.GetViewBetween(new SlotEntry(-1, end), inf);
                            found = true;
                            break;
                        }
                    }
                } while (found);
            }

            _regsCur = new SignalBase[nextReg];
            _regsNext = new SignalBase[nextReg];
        }

        private void InstantiateControlLogic()
        {
            var regTypes = new TypeDescriptor[_regsCur.Length];
            for (int i = 0; i < _regIndices.Length; i++)
            {
                int rindex = _regIndices[i];
                if (rindex < 0)
                    continue;

                regTypes[rindex] = _tempRegTypes[i];
            }

            for (int i = 0; i < _regsCur.Length; i++)
            {
                string name = "R" + i + "_cur";
                _regsCur[i] = _binder.GetSignal(EPortUsage.Default, name, null, regTypes[i].GetSampleInstance());
                name = "R" + i + "_next";
                _regsNext[i] = _binder.GetSignal(EPortUsage.Default, name, null, regTypes[i].GetSampleInstance());
            }

            var syncTempl = new SyncTemplate(this);
            var syncFunc = syncTempl.GetAlgorithm();
            Signal<StdLogic> clkInst = _binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
            _host.Descriptor.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, syncFunc, clkInst.Descriptor);
        }

        private void AssembleFlowMatrix()
        {
            _realFlow.AddNeutral(_flowSpec.NeutralFlow);
            for (int i = 0; i < _regsCur.Length; i++)
            {
                if (_regsCur[i] == null)
                    continue;

                _realFlow.AddNeutral(
                    new SignalFlow(
                        _regsCur[i].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Cur),
                        _regsNext[i].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Next)));
            }

            for (int i = 0; i < _flowSpec.NumCSteps; i++)
            {
                var pflow = _flowSpec.GetFlow(i);
                foreach (var flow in pflow.Flows)
                {
                    var sflow = flow as SignalFlow;
                    var vflow = flow as ValueFlow;
                    
                    var target = flow.Target;
                    int tindex = flow.Target.GetTemporaryIndex();
                    if (tindex >= 0)
                    {
                        int rindex = _regIndices[tindex];
                        if (rindex < 0)
                            continue;
                        target = _regsNext[rindex].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Next);
                    }
                    if (sflow != null)
                    {
                        var source = sflow.Source;
                        int sindex = sflow.Source.GetTemporaryIndex();
                        if (sindex >= 0)
                            source = _regsCur[_regIndices[sindex]].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Cur);
                        _realFlow.Add(i, new SignalFlow(source, target));

                    }
                    else
                    {
                        _realFlow.Add(i, new ValueFlow(vflow.Value, target));
                    }
                }
            }
        }

        public void CreateInterconnect(FlowMatrix flowSpec, FlowMatrix detailedFlow)
        {
            _flowSpec = flowSpec;
            _realFlow = detailedFlow;
            _flowSpec.Transitize();
            Init();
            ComputeLifetimes();
            ComputeSpilling();
            InstantiateControlLogic();
            AssembleFlowMatrix();
        }

        public static readonly IInterconnectBuilderFactory Factory = new FactoryImpl();
    }
}

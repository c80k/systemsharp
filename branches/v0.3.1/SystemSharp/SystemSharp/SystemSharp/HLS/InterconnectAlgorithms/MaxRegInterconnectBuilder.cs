/**
 * Copyright 2012 Christian Köllner
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
    /// <summary>
    /// This trivial interconnect builder assigns each data transfer to an individual register.
    /// It is intended as a "null hypothesis" performance comparisons between different interconnect builders
    /// and should not be used in practice.
    /// </summary>
    public class MaxRegInterconnectBuilder : IInterconnectBuilder
    {
        private class FactoryImpl : IInterconnectBuilderFactory
        {
            public IInterconnectBuilder Create(Component host, IAutoBinder binder)
            {
                return new MaxRegInterconnectBuilder(host, binder);
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
            private MaxRegInterconnectBuilder _icb;

            public SyncTemplate(MaxRegInterconnectBuilder icb)
            {
                _icb = icb;
            }

            protected override void DeclareAlgorithm()
            {
                Signal<StdLogic> clkInst = _icb._host.AutoBinder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
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
        private FlowMatrix _realFlow;
        private SignalBase[] _regsCur;
        private SignalBase[] _regsNext;
        private int[] _regIndices;

        private MaxRegInterconnectBuilder(Component host, IAutoBinder binder)
        {
            _host = host;
            _binder = binder;
        }

        private void InstantiateControlLogic()
        {
            int scount = _flowSpec.FlowTargets.Select(t => t.GetTemporaryIndex()).Max() + 1;
            _regIndices = new int[scount];
            for (int i = 0; i < scount; i++)
                _regIndices[i] = -1;

            int curReg = 0;
            for (int i = 0; i < _flowSpec.NumCSteps; i++)
            {
                var pflow = _flowSpec.GetFlow(i);
                foreach (var flow in pflow.Flows)
                {
                    var sflow = flow as SignalFlow;

                    var target = flow.Target;
                    int tindex = flow.Target.GetTemporaryIndex();
                    if (sflow != null)
                    {
                        var source = sflow.Source;
                        int sindex = sflow.Source.GetTemporaryIndex();
                        if (sindex >= 0 && _regIndices[sindex] < 0)
                        {
                            _regIndices[sindex] = curReg++;
                        }
                    }
                }
            }

            int numRegs = curReg;
            _regsCur = new SignalBase[numRegs];
            _regsNext = new SignalBase[numRegs];
            foreach (var target in _flowSpec.FlowTargets)
            {
                if (!target.IsTemporary())
                    continue;
                int index = target.GetTemporaryIndex();
                int rindex = _regIndices[index];
                if (rindex < 0)
                    continue;
                if (_regsCur[rindex] != null)
                    continue;

                string name = "R" + rindex + "_cur";
                _regsCur[rindex] = _binder.GetSignal(EPortUsage.Default, name, null, target.Desc.InitialValue);
                name = "R" + rindex + "_next";
                _regsNext[rindex] = _binder.GetSignal(EPortUsage.Default, name, null, target.Desc.InitialValue);
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
                        {
                            int rindex = _regIndices[sindex];
                            source = _regsCur[rindex].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Cur);
                        }
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
            InstantiateControlLogic();
            AssembleFlowMatrix();
        }

        /// <summary>
        /// Returns a factory for creating instances of this class.
        /// </summary>
        public static readonly IInterconnectBuilderFactory Factory = new FactoryImpl();
    }
}

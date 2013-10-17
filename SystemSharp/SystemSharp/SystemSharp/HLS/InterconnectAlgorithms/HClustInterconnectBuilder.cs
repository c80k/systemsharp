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
using System.Diagnostics.Contracts;
using System.Linq;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SetAlgorithms;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Assembler.DesignGen
{
    public class HClustInterconnectBuilder: IInterconnectBuilder
    {
        private class FactoryImpl : IInterconnectBuilderFactory
        {
            public IInterconnectBuilder Create(Component host, IAutoBinder binder)
            {
                return new HClustInterconnectBuilder(host, binder);
            }
        }

        private class IntSetAdapter : ISetAdapter<int>
        {
            private IPropMap<int, int> _idMap;

            public IntSetAdapter()
            {
                _idMap = new DelegatePropMap<int, int>(i => i);
            }

            public IPropMap<int, int> Index
            {
                get { return _idMap; }
            }
        }

        private class IndexedIntSetAdapter : ISetAdapter<int>
        {
            private int[] _bwdIndices;
            private IPropMap<int, int> _idxMap;

            public IndexedIntSetAdapter(int[] indices)
            {
                Contract.Requires<ArgumentException>(indices != null);

                int max = indices.Max();
                _bwdIndices = Arrays.Same(-1, max + 1);
                for (int i = 0; i < indices.Length; i++)
                    _bwdIndices[indices[i]] = i;
                _idxMap = new DelegatePropMap<int, int>(i => _bwdIndices[i]);
            }

            public IPropMap<int, int> Index
            {
                get { return _idxMap; }
            }
        }

        private class SyncTemplate : AlgorithmTemplate
        {
            private HClustInterconnectBuilder _icb;

            public SyncTemplate(HClustInterconnectBuilder icb)
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
        private FlowMatrix _detailedFlow;
        
        private int[] _lifeStart;
        private int[] _lifeEnd;
        private TypeDescriptor[] _tempRegTypes;

        // Register merging procedure
        private UnionFind<int> _eqRegs;
        private IntervalSet[] _lifeTimes;
        private Dictionary<SignalRef, HashSet<int>> _fuMuxIn;
        private HashSet<SignalRef>[] _regIn;
        private HashSet<SignalRef>[] _regOut;
        private ILookup<int, int> _regLookup;

        // RAM inference procedure
        private UnionFind<int> _eqMems;
        private SortedSet<int>[] _writeTimes;
        private SortedSet<int>[] _readTimes;
        private StdLogicVector[] _memAddrs;
        private ILookup<int, int> _memLookup;

        private SignalBase[] _regsCur;
        private SignalBase[] _regsNext;

        private SignalBase[] _memAddrRSignals;
        private SignalBase[] _memAddrWSignals;
        private SignalBase[] _memWrSignals;
        private SignalBase[] _memDInSignals;
        private SignalBase[] _memDOutSignals;
        private SimpleDPRAM[] _memInstances;

        private bool[] _isMemMap;
        private int[] _idxMap;

        public HClustInterconnectBuilder(Component host, IAutoBinder binder)
        {
            _host = host;
            _binder = binder;
        }

        private void Init()
        {
            var tempTargets = _flowSpec.FlowTargets.Select(t => t.GetTemporaryIndex());
            int maxRegs = tempTargets.Max() + 1;
            int scount = maxRegs;
            _tempRegTypes = new TypeDescriptor[scount];
            //_regMuxCost = Enumerable.Repeat(1, scount).ToArray();
            _fuMuxIn = new Dictionary<SignalRef, HashSet<int>>();
            _regOut = new HashSet<SignalRef>[scount];
            _regIn = new HashSet<SignalRef>[scount];
            for (int i = 0; i < scount; i++)
            {
                _regIn[i] = new HashSet<SignalRef>();
                _regOut[i] = new HashSet<SignalRef>();
            }
            foreach (var target in _flowSpec.FlowTargets)
            {
                if (target.IsTemporary())
                {
                    int index = target.GetTemporaryIndex();
                    _tempRegTypes[index] = target.Desc.ElementType;
                }
            }
            for (int cstep = 0; cstep < _flowSpec.NumCSteps; cstep++)
            {
                var pflow = _flowSpec.GetFlow(cstep);
                foreach (var flow in pflow.Flows)
                {
                    var sflow = flow as SignalFlow;
                    if (sflow != null && sflow.Source.IsTemporary())
                    {
                        int sindex = sflow.Source.GetTemporaryIndex();
                        _fuMuxIn.Add(sflow.Target, sindex);
                        _regOut[sindex].Add(sflow.Target);
                    }
                    if (sflow != null && flow.Target.IsTemporary())
                    {
                        int tindex = flow.Target.GetTemporaryIndex();
                        _regIn[tindex].Add(sflow.Source);
                    }
                }
            }
            _lifeStart = new int[scount];
            _lifeEnd = new int[scount];
            for (int i = 0; i < scount; i++)
            {
                _lifeStart[i] = int.MaxValue;
                _lifeEnd[i] = int.MinValue;
            }
            _eqRegs = new UnionFind<int>(new IntSetAdapter(), Enumerable.Range(0, scount).ToList());
            _lifeTimes = new IntervalSet[scount];
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
                            _lifeEnd[sindex] = Math.Max(_lifeEnd[sindex], i);
                        }
                    }
                }
            }
            for (int j = 0; j < _lifeStart.Length; j++)
            {
                if (_lifeEnd[j] != int.MinValue &&
                    _lifeEnd[j] < _lifeStart[j])
                    throw new XILSchedulingFailedException("Bad schedule: improper ordering");
            }
            int scount = _lifeStart.Length;
            for (int i = 0; i < scount; i++)
            {
                Debug.Assert(!IsMayfly(i));
                if (!IsUnused(i))
                {
                    _lifeTimes[i] = new IntervalSet();
                    _lifeTimes[i].Add(_lifeStart[i], _lifeEnd[i]);
                }
            }
        }

        private bool IsMayfly(int oslot)
        {
            return _lifeEnd[oslot] - _lifeStart[oslot] == 0;
        }

        private bool IsUnused(int oslot)
        {
            return _lifeEnd[oslot] < _lifeStart[oslot];
        }

        private bool AreSharable(int slot1, int slot2)
        {
            if (IsUnused(slot1) || IsUnused(slot2))
                return false;

            return (!_lifeTimes[_eqRegs.Find(slot1)].Intersects(_lifeTimes[_eqRegs.Find(slot2)])) &&
                _tempRegTypes[slot1].Equals(_tempRegTypes[slot2]);
        }

        private bool AreSharableInMemory(int slot1, int slot2)
        {
            int rep1 = _eqMems.Find(slot1);
            int rep2 = _eqMems.Find(slot2);
            return _tempRegTypes[slot1].Equals(_tempRegTypes[slot2]) &&
                !_writeTimes[rep1].Any(t => _writeTimes[rep2].Contains(t)) &&
                !_readTimes[rep1].Any(t => _readTimes[rep2].Contains(t));
        }

        private int ComputeFuMuxCost(IEnumerable<int> inSet)
        {
            Contract.Requires<ArgumentNullException>(inSet != null);

            return inSet.Select(i => _eqRegs.Find(i)).Distinct().Count();
        }

        private int ComputeFuMuxCost(SignalRef target)
        {
            return ComputeFuMuxCost(_fuMuxIn[target]);
        }

        private int ComputeMaxFuMuxCost()
        {
            return _fuMuxIn.Select(kvp => ComputeFuMuxCost(kvp.Value)).Max();
        }

        private void DoClustering()
        {
            int n = _tempRegTypes.Length;
            
            bool found;
            do
            {
                found = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int slot1 = 0; slot1 < n; slot1++)
                    {
                        for (int slot2 = slot1 + 1; slot2 < n; slot2++)
                        {
                            if (AreSharable(slot1, slot2))
                            {
                                int rep1 = _eqRegs.Find(slot1);
                                int rep2 = _eqRegs.Find(slot2);
                                if (rep1 == rep2)
                                    continue;

                                bool merge = false;
                                var mergedInputs = _regIn[slot1].Union(_regIn[slot2]);
                                int newRegMuxCost = mergedInputs.Count();
                                if (pass == 0)
                                {
                                    merge = newRegMuxCost ==
                                        Math.Max(_regIn[slot1].Count, _regIn[slot2].Count);
                                }
                                else
                                {
                                    var commonTargets = _regOut[slot1].Intersect(_regOut[slot2]);
                                    if (!commonTargets.Any())
                                        continue;

                                    /*int newFuMuxCost = commonTargets.Select(t => ComputeFuMuxCost(t) - 1).Max();
                                    if (newFuMuxCost - newRegMuxCost >= -1)
                                        merge = true;*/

                                    int outdeg = Math.Max(
                                        _regOut[slot1].Max(t => ComputeFuMuxCost(t)),
                                        _regOut[slot2].Max(t => ComputeFuMuxCost(t)));
                                    merge = (newRegMuxCost <= outdeg);
                                }
                                if (merge)
                                {
                                    _eqRegs.Union(slot1, slot2);
                                    _regIn[slot1].UnionWith(_regIn[slot2]);
                                    _regIn[slot2] = _regIn[slot1];
                                    int rep = _eqRegs.Find(slot1);
                                    _lifeTimes[rep].Add(_lifeTimes[rep1]);
                                    _lifeTimes[rep].Add(_lifeTimes[rep2]);
                                    found = true;
                                }
                            }
                        }
                    }
                }
            } while (found);

            _regLookup = _eqRegs.ToLookup();
        }

        private void DoMemoryClustering()
        {
            var lookup = _eqRegs.ToLookup();
            int[] indices = lookup.Select(grp => grp.Key).Where(i => !IsUnused(i)).ToArray();
            if (indices.Length == 0)
                return;
            _eqMems = new UnionFind<int>(new IndexedIntSetAdapter(indices), indices);

            _writeTimes = new SortedSet<int>[_tempRegTypes.Length];
            _readTimes = new SortedSet<int>[_tempRegTypes.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int j = indices[i];
                _writeTimes[j] = new SortedSet<int>();
                _readTimes[j] = new SortedSet<int>();
                _writeTimes[j].AddRange(_lifeTimes[j].LeftPoints);
                _readTimes[j].AddRange(_lifeTimes[j].RightPoints);
            }

            bool found;
            do
            {
                found = false;
                for (int i = 0; i < indices.Length; i++)
                {
                    int slot1 = indices[i];
                    for (int j = i + 1; j < indices.Length; j++)
                    {
                        int slot2 = indices[j];
                        if (AreSharableInMemory(slot1, slot2))
                        {
                            int rep1 = _eqMems.Find(slot1);
                            int rep2 = _eqMems.Find(slot2);
                            if (rep1 == rep2)
                                continue;

                            var mergedInputs = _regIn[slot1].Union(_regIn[slot2]);
                            int newRegMuxCost = mergedInputs.Count();
                            var commonTargets = _regOut[slot1].Intersect(_regOut[slot2]);
                            if (!commonTargets.Any())
                                continue;
                            int newFuMuxCost = commonTargets.Select(t => ComputeFuMuxCost(t) - 1).Max();
                            if (newFuMuxCost - newRegMuxCost >= -1)
                            {
                                _eqMems.Union(slot1, slot2);
                                _regIn[slot1].UnionWith(_regIn[slot2]);
                                _regIn[slot2] = _regIn[slot1];
                                int rep = _eqMems.Find(slot1);
                                if (rep != rep1)
                                {
                                    _writeTimes[rep].AddRange(_writeTimes[rep1]);
                                    _readTimes[rep].AddRange(_readTimes[rep1]);
                                }
                                if (rep != rep2)
                                {
                                    _writeTimes[rep].AddRange(_writeTimes[rep2]);
                                    _readTimes[rep].AddRange(_readTimes[rep2]);
                                }
                                found = true;
                            }
                        }
                    }
                }
            } while (found);

            _memLookup = _eqMems.ToLookup();
            _memAddrs = new StdLogicVector[_tempRegTypes.Length];
            foreach (var grp in _memLookup)
            {
                var active = grp.Where(i => !IsUnused(i));
                if (active.Count() <= 1)
                    continue;

                int addrWidth = MathExt.CeilLog2(active.Count());
                uint addr = 0;
                foreach (var stgSlot in active)
                {
                    if (IsUnused(stgSlot))
                        continue;

                    _memAddrs[stgSlot] = StdLogicVector.FromUInt(addr, addrWidth);
                    addr++;
                }
            }
        }

        private void InstantiateControlLogic()
        {
            if (_memLookup == null)
                // All registers unused, nothing to instantiate
                return;

            int rcount = 0;
            int mcount = 0;
            var regsCur = new List<SignalBase>();
            var regsNext = new List<SignalBase>();
            var memInstances = new List<SimpleDPRAM>();
            var memAddrRSignals = new List<SignalBase>();
            var memAddrWSignals = new List<SignalBase>();
            var memDInSignals = new List<SignalBase>();
            var memDOutSignals = new List<SignalBase>();
            var memWrEnSignals = new List<SignalBase>();

            int scount = _tempRegTypes.Length;
            _idxMap = Arrays.Same(-1, scount);
            _isMemMap = Arrays.Same(false, scount);

            foreach (var grp in _memLookup)
            {
                if (grp.Count() <= 800000)
                {
                    // Implement as register
                    foreach (int slot in grp)
                    {
                        if (IsUnused(slot))
                            continue;

                        _idxMap[slot] = rcount;

                        var regType = _tempRegTypes[slot];
                        string name = "R" + rcount + "_cur";
                        var regCur = _binder.GetSignal(EPortUsage.Default, name, null, regType.GetSampleInstance());
                        //regCur.Descriptor.TagTemporary(rcount);
                        name = "R" + rcount + "_next";
                        var regNext = _binder.GetSignal(EPortUsage.Default, name, null, regType.GetSampleInstance());
                        //regNext.Descriptor.TagTemporary(rcount);

                        regsCur.Add(regCur);
                        regsNext.Add(regNext);

                        ++rcount;
                    }
                }
                else
                {
                    // Implement as memory
                    foreach (int slot in grp)
                    {
                        _isMemMap[slot] = true;
                        _idxMap[slot] = mcount;
                    }

                    int rep = grp.Key;

                    string name = "M" + rcount + "_AddrR";
                    var sigAddrR = _binder.GetSignal<StdLogicVector>(EPortUsage.Default, name, null, _memAddrs[rep]);
                    name = "M" + rcount + "_AddrW";
                    var sigAddrW = _binder.GetSignal<StdLogicVector>(EPortUsage.Default, name, null, _memAddrs[rep]);
                    name = "M" + rcount + "_DIn";
                    var regType = _tempRegTypes[rep];
                    var sigDIn = _binder.GetSignal<StdLogicVector>(EPortUsage.Default, name, null, (StdLogicVector)regType.GetSampleInstance());
                    name = "M" + rcount + "_DOut";
                    var sigDOut = _binder.GetSignal<StdLogicVector>(EPortUsage.Default, name, null, (StdLogicVector)regType.GetSampleInstance());
                    name = "M" + rcount + "_WrEn";
                    var sigWrEn = _binder.GetSignal<StdLogic>(EPortUsage.Default, name, null, StdLogic._0);

                    var mem = new SimpleDPRAM(
                        (uint)grp.Count(),
                        (uint)TypeLowering.Instance.GetWireWidth(regType))
                    {
                        Clk = _binder.GetSignal<StdLogic>(EPortUsage.Clock, null, null, StdLogic._0),
                        RdAddr = sigAddrR,
                        WrAddr = sigAddrW,
                        DataIn = sigDIn,
                        DataOut = sigDOut,
                        WrEn = sigWrEn
                    };
                    _host.Descriptor.AddChild(mem.Descriptor, "Mem" + rcount);

                    memInstances.Add(mem);
                    memAddrRSignals.Add(sigAddrR);
                    memAddrWSignals.Add(sigAddrW);
                    memDInSignals.Add(sigDIn);
                    memDOutSignals.Add(sigDOut);
                    memWrEnSignals.Add(sigWrEn);

                    ++mcount;
                }
            }

            _regsCur = regsCur.ToArray();
            _regsNext = regsNext.ToArray();

            _memInstances = memInstances.ToArray();
            _memAddrRSignals = memAddrRSignals.ToArray();
            _memAddrWSignals = memAddrWSignals.ToArray();
            _memDInSignals = memDInSignals.ToArray();
            _memDOutSignals = memDOutSignals.ToArray();
            _memWrSignals = memWrEnSignals.ToArray();

            var syncTempl = new SyncTemplate(this);
            var syncFunc = syncTempl.GetAlgorithm();
            Signal<StdLogic> clkInst = _binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
            _binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, syncFunc, clkInst.Descriptor);
        }

        private void AssembleFlowMatrix()
        {
            _detailedFlow.AddNeutral(_flowSpec.NeutralFlow);
            if (_regsCur != null)
            {
                for (int i = 0; i < _regsCur.Length; i++)
                {
                    if (_regsCur[i] == null)
                        continue;

                    _detailedFlow.AddNeutral(
                        new SignalFlow(
                            _regsCur[i].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Cur),
                            _regsNext[i].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Next)));
                }
            }
            if (_memInstances != null)
            {
                for (int i = 0; i < _memInstances.Length; i++)
                {
                    _detailedFlow.AddNeutral(
                        new ValueFlow(
                            StdLogic._0, _memWrSignals[i].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                    _detailedFlow.AddNeutral(
                        new ValueFlow(
                            StdLogicVector.DCs(_memInstances[i].AddrWidth),
                            _memAddrRSignals[i].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                    _detailedFlow.AddNeutral(
                        new ValueFlow(
                            StdLogicVector.DCs(_memInstances[i].AddrWidth),
                            _memAddrWSignals[i].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                    _detailedFlow.AddNeutral(
                        new ValueFlow(
                            StdLogicVector.DCs(_memInstances[i].Width),
                            _memDInSignals[i].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                }
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
                        if (IsUnused(tindex))
                            continue;
                        int rep = _eqRegs.Find(tindex);
                        if (_isMemMap[rep])
                        {
                            int rindex = _idxMap[_eqMems.Find(rep)];
                            target = _memDInSignals[rindex].ToSignalRef(SignalRef.EReferencedProperty.Next);
                            _detailedFlow.Add(i, 
                                new ValueFlow(_memAddrs[rep], 
                                    _memAddrWSignals[rindex].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                            _detailedFlow.Add(i,
                                new ValueFlow(StdLogic._1,
                                    _memWrSignals[rindex].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                        }
                        else
                        {
                            int rindex = _idxMap[rep];
                            if (rindex < 0)
                                continue;
                            target = _regsNext[rindex].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Next);
                        }
                    }
                    if (sflow != null)
                    {
                        var source = sflow.Source;
                        int sindex = sflow.Source.GetTemporaryIndex();
                        if (sindex >= 0)
                        {
                            int rep = _eqRegs.Find(sindex);
                            if (_isMemMap[rep])
                            {
                                int rindex = _idxMap[_eqMems.Find(rep)];
                                source = _memDOutSignals[rindex].ToSignalRef(SignalRef.EReferencedProperty.Cur);
                                _detailedFlow.Add(i - 1,
                                    new ValueFlow(_memAddrs[rep],
                                        _memAddrRSignals[rindex].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                            }
                            else
                            {
                                int rindex = _idxMap[rep];
                                source = _regsCur[rindex].ToSignalRef(SysDOM.SignalRef.EReferencedProperty.Cur);
                            }
                        }
                        _detailedFlow.Add(i, new SignalFlow(source, target));

                    }
                    else
                    {
                        _detailedFlow.Add(i, new ValueFlow(vflow.Value, target));
                    }
                }
            }
        }

        private void VerifyResult()
        {
            //TODO: Need new verification concept when block memories come into play
#if false
            var orgFlows = _flowSpec.GetTimedFlows().OrderBy(f => f.Target.ToString()).OrderBy(f => f.Time).ToArray();
            var resFlows = _detailedFlow.GetTimedFlows().OrderBy(f => f.Target.ToString()).OrderBy(f => f.Time).ToArray();
            /*
            var ins = new List<ITimedFlow>();
            var del = new List<ITimedFlow>();
            int i = 0; int j = 0;
            while (i < orgFlows.Length && j < resFlows.Length)
            {
                if (!orgFlows[i].Equals(resFlows[j]))
                {
                    if (i < orgFlows.Length - 1 && orgFlows[i + 1].Equals(resFlows[j]))
                    {
                        ins.Add(orgFlows[i]);
                        ++i;
                    }
                    else if (j < resFlows.Length - 1 && orgFlows[i].Equals(resFlows[j + 1]))
                    {
                        del.Add(resFlows[j]);
                        ++j;
                    }
                }
                else
                {
                    ++i;
                    ++j;
                }
            }
            Debug.Assert(ins.Count == 0 && del.Count == 0);
             * */
            Debug.Assert(orgFlows.Length == resFlows.Length);
            for (int i = 0; i < orgFlows.Length; i++)
            {
                Debug.Assert(orgFlows[i].Equals(resFlows[i]));
            }
#endif
        }

        public void CreateInterconnect(FlowMatrix flowSpec, FlowMatrix detailedFlow)
        {
            _flowSpec = flowSpec;
            _detailedFlow = detailedFlow;
            _flowSpec.Transitize();
            Init();
            ComputeLifetimes();
            DoClustering();
            DoMemoryClustering();
            InstantiateControlLogic();
            AssembleFlowMatrix();
            VerifyResult();
        }

        public static readonly IInterconnectBuilderFactory Factory = new FactoryImpl();
    }
}

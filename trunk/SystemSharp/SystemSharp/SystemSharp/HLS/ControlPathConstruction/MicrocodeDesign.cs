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
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SetAlgorithms;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Assembler.DesignGen
{
    class EncodedFlow
    {
        public SignalRef[] Targets { get; protected set; }
        public int NumSymbols { get; protected set; }
        public int[] EncodedSymbols { get; protected set; }
        public int Order { get; private set; }

        protected Dictionary<ParFlow, int> _fwdEnc = new Dictionary<ParFlow, int>();
        protected List<ParFlow> _bwdEnc;
        private Array _selSymbols;
        private SignalDescriptor _symbol;
        private SignalDescriptor _symbold;

        public Dictionary<ParFlow, int> FwdEnc { get { return _fwdEnc; } }
        public List<ParFlow> BwdEnc { get { return _bwdEnc; } }

        public EncodedFlow(List<Flow> flows, int order)
        {
            Contract.Requires(flows != null && flows.Any());
            Contract.Requires(flows.All(f => f.Target.Equals(flows.First().Target)));

            Targets = new SignalRef[] { flows.First().Target };
            EncodedSymbols = new int[flows.Count];
            Order = order;
            _bwdEnc = new List<ParFlow>();
            int i = 0;
            foreach (var flow in flows)
            {
                int sym = 0;
                if (!FlowMatrix.IsDontCareFlow(flow))
                {
                    var vflow = flow as ValueFlow;
                    Flow cflow;
                    if (vflow != null)
                        cflow = FlowMatrix.AsDontCareFlow(vflow);
                    else
                        cflow = flow;
                    var cpflow = new ParFlow(new Flow[] { cflow });
                    if (!_fwdEnc.TryGetValue(cpflow, out sym))
                    {
                        sym = ++NumSymbols;
                        _fwdEnc[cpflow] = sym;
                        _bwdEnc.Add(new ParFlow(new Flow[] { cflow }));
                    }
                }
                EncodedSymbols[i] = sym;
                i++;
            }
            if (_bwdEnc.Count == 0)
            {
                var dummy = new ParFlow();
                _bwdEnc.Add(dummy);
                _fwdEnc[dummy] = 1;
                NumSymbols = 1;
            }
        }

        protected EncodedFlow()
        {
        }

        private void ImplementFlow(ValueFlowCoder vfc, Flow flow, IAlgorithmBuilder pbuilder, SLVSignal cwSignal, HashSet<ISignalOrPortDescriptor> sensitivity)
        {
            if (FlowMatrix.IsDontCareFlow(flow))
            {
                int valOffset = vfc.GetValueWordOffset(flow.Target);
                int valWidth = vfc.GetValueWordWidth(flow.Target);
                LiteralReference lrCWValSlice;
                if (flow.Target.Desc.ElementType.CILType.Equals(typeof(StdLogic)))
                {
                    lrCWValSlice = new LiteralReference(
                        ((ISignal)cwSignal[valOffset])
                            .ToSignalRef(SignalRef.EReferencedProperty.Cur));
                }
                else
                {
                    lrCWValSlice = new LiteralReference(
                        ((ISignal)cwSignal[valOffset + valWidth - 1, valOffset])
                            .ToSignalRef(SignalRef.EReferencedProperty.Cur));
                }
                pbuilder.Store(flow.Target, lrCWValSlice);
            }
            else if (flow is SignalFlow)
            {
                var sflow = flow as SignalFlow;
                pbuilder.Store(flow.Target, sflow.Source);
                sensitivity.Add(sflow.Source.Desc);
            }
            else
            {
                var vflow = flow as ValueFlow;
                pbuilder.Store(vflow.Target,
                    LiteralReference.CreateConstant(vflow.Value));
            }
        }

        private void CreateSelSymbol(IAutoBinder binder, bool registered)
        {
            int count = NumSymbols;
            if (count < 2)
                return;

            // This code will create a one-hot decoding
            var selSymbols = new StdLogicVector[NumSymbols];
            _selSymbols = selSymbols;
            for (int i = 0; i < NumSymbols; i++)
            {
                StdLogicVector sym = StdLogicVector._0s(NumSymbols);
                sym[i] = '1';
                selSymbols[i] = sym;
            }
            var signal = binder.GetSignal(EPortUsage.Default, "MUXSymbol" + Order, null, selSymbols[0]);
            _symbol = signal.Descriptor;
            if (registered)
            {
                var dsignal = binder.GetSignal(EPortUsage.Default, "MUXReg" + Order, null, selSymbols[0]);
                _symbold = dsignal.Descriptor;
            }
        }

        public virtual void AssembleStagedDecoderSync(int[] syms, int selWidth, 
            LiteralReference lrCWSelSlice, IAutoBinder binder, IAlgorithmBuilder pbuilder,
            bool registered)
        {
            if (NumSymbols < 2)
                return;

            CreateSelSymbol(binder, registered);

            pbuilder.Switch(lrCWSelSlice);

            for (int i = 0; i < syms.Length; i++)
            {
                var selValue = StdLogicVector.FromUInt((uint)i, selWidth);
                pbuilder.Case(LiteralReference.CreateConstant(selValue));

                int sym = syms[i];
                var symbol = _selSymbols.GetValue(sym - 1);
                pbuilder.Store(
                    _symbol.SignalInstance.ToSignalRef(SignalRef.EReferencedProperty.Next),
                    LiteralReference.CreateConstant(symbol));

                pbuilder.EndCase();
            }

            pbuilder.DefaultCase();
            {
                pbuilder.Store(
                    _symbol.SignalInstance.ToSignalRef(SignalRef.EReferencedProperty.Next),
                    LiteralReference.CreateConstant(StdLogicVector.Xs(NumSymbols)));
            }
            pbuilder.EndCase();

            if (registered)
            {
                pbuilder.Store(
                    _symbold.SignalInstance.ToSignalRef(SignalRef.EReferencedProperty.Next),
                    _symbol.SignalInstance.ToSignalRef(SignalRef.EReferencedProperty.Cur));
            }

            pbuilder.EndSwitch();
        }

        public virtual void AssembleStagedDecoderComb(
            ValueFlowCoder vfc,
            LiteralReference lrCWSelSlice,
            IAlgorithmBuilder pbuilder, SLVSignal cwSignal,
            HashSet<ISignalOrPortDescriptor> sensitivity,
            bool registered)
        {
            if (NumSymbols == 0)
            {
                foreach (var target in Targets)
                {
                    pbuilder.Store(target, LiteralReference.CreateConstant(target.Desc.InitialValue));
                }
            }
            else if (NumSymbols == 1)
            {
                var pflow = BwdEnc[0];
                foreach (var flow in pflow.Flows)
                {
                    ImplementFlow(vfc, flow, pbuilder, cwSignal, sensitivity);
                }
            }
            else
            {
                var symbol = registered ? _symbold : _symbol;
                pbuilder.Switch(symbol.SignalInstance.ToSignalRef(SignalRef.EReferencedProperty.Cur));

                for (int i = 0; i < NumSymbols; i++)
                {
                    var selValue = _selSymbols.GetValue(i);
                    var pflow = BwdEnc[i];

                    pbuilder.Case(LiteralReference.CreateConstant(selValue));
                    foreach (var flow in pflow.Flows)
                    {
                        ImplementFlow(vfc, flow, pbuilder, cwSignal, sensitivity);
                    }
                    pbuilder.EndCase();
                }

                var nulls = StdLogicVector._0s(NumSymbols);
                pbuilder.Case(LiteralReference.CreateConstant(nulls));
                {
                    foreach (var target in Targets)
                    {
                        int width = Marshal.SerializeForHW(target.Desc.InitialValue).Size;
                        pbuilder.Store(target, 
                            LiteralReference.CreateConstant(StdLogicVector._0s(width)));
                    }
                }
                pbuilder.EndCase();

                pbuilder.DefaultCase();
                {
                    foreach (var target in Targets)
                    {
                        int width = Marshal.SerializeForHW(target.Desc.InitialValue).Size;
                        pbuilder.Store(target,
                            LiteralReference.CreateConstant(StdLogicVector.Xs(width)));
                    }
                }
                pbuilder.EndCase();

                pbuilder.EndSwitch();
            }
        }
    }

    class MergedFlow : EncodedFlow
    {
        private EncodedFlow _encFlow0;
        private EncodedFlow _encFlow1;
        private int[] _encMap0, _encMap1;

        public double Score { get; private set; }

        public MergedFlow(EncodedFlow encFlow0, EncodedFlow encFlow1)
        {
            Debug.Assert(encFlow0.NumSymbols > 1);
            Debug.Assert(encFlow1.NumSymbols > 1);

            _encFlow0 = encFlow0;
            _encFlow1 = encFlow1;
            Targets = encFlow0.Targets.Concat(encFlow1.Targets).ToArray();

            int[] encMap0, encMap1;
            EncodedSymbols = MicrocodeAlgorithms.EncodeTogether(
                encFlow0.EncodedSymbols, encFlow0.NumSymbols,
                encFlow1.EncodedSymbols, encFlow1.NumSymbols,
                out encMap0, out encMap1);
            _encMap0 = encMap0;
            _encMap1 = encMap1;

            NumSymbols = _encMap0.Length;
            Score = MathExt.CeilLog2(encFlow0.NumSymbols) +
                MathExt.CeilLog2(encFlow1.NumSymbols) -
                Math.Log(NumSymbols, 2.0);
        }

        public void Realize()
        {
            _bwdEnc = new List<ParFlow>();
            int sym;
            for (sym = 0; sym < _encMap0.Length; sym++)
            {
                int sym0 = _encMap0[sym];
                ParFlow sym0Flow;
                if (sym0 == 0)
                    sym0 = 1;
                sym0Flow = _encFlow0.BwdEnc[sym0 - 1];

                int sym1 = _encMap1[sym];
                ParFlow sym1Flow;
                if (sym1 == 0)
                    sym1 = 1;
                sym1Flow = _encFlow1.BwdEnc[sym1 - 1];

                var pflow = new ParFlow();
                pflow.Integrate(sym0Flow);
                pflow.Integrate(sym1Flow);
                _bwdEnc.Add(pflow);
            }

            sym = 0;
            foreach (var pflow in _bwdEnc)
            {
                _fwdEnc[pflow] = ++sym;
            }

            // Verify result
            for (int i = 0; i < EncodedSymbols.Length; i++)
            {
                sym = EncodedSymbols[i];
                int sym0 = _encFlow0.EncodedSymbols[i];
                int sym1 = _encFlow1.EncodedSymbols[i];
                if (sym == 0)
                {
                    Debug.Assert(sym0 == 0 && sym1 == 0);
                    continue;
                }
                if (sym0 != 0)
                {
                    var pflow = _encFlow0.BwdEnc[sym0 - 1];
                    var mflow = BwdEnc[sym - 1];
                    foreach (var flow in pflow.Flows)
                    {
                        Debug.Assert(mflow.LookupTarget(flow.Target).Equals(flow));
                    }
                }
                if (sym1 != 0)
                {
                    var pflow = _encFlow1.BwdEnc[sym1 - 1];
                    var mflow = BwdEnc[sym - 1];
                    foreach (var flow in pflow.Flows)
                    {
                        Debug.Assert(mflow.LookupTarget(flow.Target).Equals(flow));
                    }
                }
            }
        }

        public override void AssembleStagedDecoderComb(ValueFlowCoder vfc, LiteralReference lrCWSelSlice, 
            IAlgorithmBuilder pbuilder, SLVSignal cwSignal, HashSet<ISignalOrPortDescriptor> sensitivity,
            bool registered)
        {
            _encFlow0.AssembleStagedDecoderComb(vfc, lrCWSelSlice, pbuilder, cwSignal, sensitivity, registered);
            _encFlow1.AssembleStagedDecoderComb(vfc, lrCWSelSlice, pbuilder, cwSignal, sensitivity, registered);
        }

        public override void AssembleStagedDecoderSync(int[] syms, int selWidth, LiteralReference lrCWSelSlice, 
            IAutoBinder binder, IAlgorithmBuilder pbuilder, bool registered)
        {
            int[] syms0 = syms.Select(i => _encMap0[i - 1]).ToArray();
            int[] syms1 = syms.Select(i => _encMap1[i - 1]).ToArray();
            _encFlow0.AssembleStagedDecoderSync(syms0, selWidth, lrCWSelSlice, binder, pbuilder, registered);
            _encFlow1.AssembleStagedDecoderSync(syms1, selWidth, lrCWSelSlice, binder, pbuilder, registered);
        }
    }

    class MicroString
    {
        private ValueFlowCoder _vfc;
        private SignalRef[] _targets;
        private EncodedFlow _encFlow;

        public SignalRef[] Targets
        {
            get { return _targets; }
        }

        public int SelWidth { get; private set; }
        public int SelOffset { get; internal set; }
        public int Order { get; internal set; }

        public MicroString(EncodedFlow encFlow, ValueFlowCoder vfc)
        {
            _encFlow = encFlow;
            _targets = encFlow.Targets;
            _vfc = vfc;
            if (_encFlow.NumSymbols == 0)
                SelWidth = 0;
            else
                SelWidth = MathExt.CeilLog2(_encFlow.NumSymbols);
        }

        public void Encode(int cstep, ParFlow pflow, ref StdLogicVector cw)
        {
            foreach (var flow in pflow.Flows)
            {
                if (FlowMatrix.IsDontCareFlow(flow))
                    continue;

                var vflow = flow as ValueFlow;
                if (vflow != null)
                {
                    int offs = _vfc.GetValueWordOffset(flow.Target);
                    var ser = Marshal.SerializeForHW(vflow.Value);
                    cw[offs + ser.Size - 1, offs] = ser;
                }
            }

            if (SelWidth <= 0)
                return;

            int symbol = _encFlow.EncodedSymbols[cstep];
            if (symbol == 0)
                symbol = 1;
            uint index = (uint)(symbol - 1);
            cw[SelOffset + SelWidth - 1, SelOffset] = StdLogicVector.FromUInt(index, SelWidth);
        }

        private void ImplementFlow(Flow flow, IAlgorithmBuilder pbuilder, SLVSignal cwSignal, HashSet<ISignalOrPortDescriptor> sensitivity)
        {
            if (FlowMatrix.IsDontCareFlow(flow))
            {
                int valOffset = _vfc.GetValueWordOffset(flow.Target);
                int valWidth = _vfc.GetValueWordWidth(flow.Target);
                LiteralReference lrCWValSlice;
                if (flow.Target.Desc.ElementType.CILType.Equals(typeof(StdLogic)))
                {
                    lrCWValSlice = new LiteralReference(
                        ((ISignal)cwSignal[valOffset])
                            .ToSignalRef(SignalRef.EReferencedProperty.Cur));
                }
                else
                {
                    lrCWValSlice = new LiteralReference(
                        ((ISignal)cwSignal[valOffset + valWidth - 1, valOffset])
                            .ToSignalRef(SignalRef.EReferencedProperty.Cur));
                }
                pbuilder.Store(flow.Target, lrCWValSlice);
            }
            else if (flow is SignalFlow)
            {
                var sflow = flow as SignalFlow;
                pbuilder.Store(flow.Target, sflow.Source);
                sensitivity.Add(sflow.Source.Desc);
            }
            else
            {
                var vflow = flow as ValueFlow;
                pbuilder.Store(vflow.Target, 
                    LiteralReference.CreateConstant(vflow.Value));
            }
        }

        internal void AssembleDecoder(IAlgorithmBuilder pbuilder, SLVSignal cwSignal, HashSet<ISignalOrPortDescriptor> sensitivity)
        {
            if (_encFlow.NumSymbols == 0)
            {
                foreach (var target in _targets)
                {
                    pbuilder.Store(target, LiteralReference.CreateConstant(target.Desc.InitialValue));
                }
            }
            else if (_encFlow.NumSymbols == 1)
            {
                var pflow = _encFlow.BwdEnc[0];
                foreach (var flow in pflow.Flows)
                {
                    ImplementFlow(flow, pbuilder, cwSignal, sensitivity);
                }
            }
            else
            {
                var lrCWSelSlice = new LiteralReference(
                    ((ISignal)cwSignal[SelOffset + SelWidth - 1, SelOffset])
                        .ToSignalRef(SignalRef.EReferencedProperty.Cur));
                pbuilder.Switch(lrCWSelSlice);

                for (int i = 0; i < _encFlow.NumSymbols; i++)
                {
                    var selValue = StdLogicVector.FromUInt((uint)i, SelWidth);
                    var pflow = _encFlow.BwdEnc[i];

                    if (i + 1 == _encFlow.NumSymbols)
                        pbuilder.DefaultCase();
                    else
                        pbuilder.Case(LiteralReference.CreateConstant(selValue));
                    foreach (var flow in pflow.Flows)
                    {
                        ImplementFlow(flow, pbuilder, cwSignal, sensitivity);
                    }
                    pbuilder.EndCase();                    

                }

                pbuilder.EndSwitch();
            }
        }

        internal void AssembleStagedDecoderSync(IAutoBinder binder, IAlgorithmBuilder pbuilder, SLVSignal cwSignal, bool registered)
        {
            LiteralReference lrCWSelSlice = null;
            if (SelWidth != 0)
            {
                lrCWSelSlice = new LiteralReference(
                    ((ISignal)cwSignal[SelOffset + SelWidth - 1, SelOffset])
                        .ToSignalRef(SignalRef.EReferencedProperty.Cur));
            }
            _encFlow.AssembleStagedDecoderSync(Enumerable.Range(1, _encFlow.NumSymbols).ToArray(),
                SelWidth, lrCWSelSlice, binder, pbuilder, registered);
        }

        internal void AssembleStagedDecoderComb(IAlgorithmBuilder pbuilder, SLVSignal cwSignal,
            HashSet<ISignalOrPortDescriptor> sensitivity, bool registered)
        {
            LiteralReference lrCWSelSlice = null;
            if (SelWidth != 0)
            {
                lrCWSelSlice = new LiteralReference(
                    ((ISignal)cwSignal[SelOffset + SelWidth - 1, SelOffset])
                        .ToSignalRef(SignalRef.EReferencedProperty.Cur));
            }
            _encFlow.AssembleStagedDecoderComb(_vfc, lrCWSelSlice, pbuilder, cwSignal, sensitivity, registered);
        }
    }

    class ValueFlowCoder
    {
        private List<ParFlow> _flowList = new List<ParFlow>();
        private Dictionary<SignalRef, int> _widthMap = new Dictionary<SignalRef, int>();
        private Dictionary<SignalRef, int> _offsetMap = new Dictionary<SignalRef, int>();

        public int ValueWordWidth { get; private set; }

        public void AddFlow(ParFlow pflow)
        {
            var vflows = pflow.Flows.Where(f => f is ValueFlow && !FlowMatrix.IsDontCareFlow(f));
            _flowList.Add(new ParFlow(vflows));
            foreach (var f in vflows)
            {
                var vflow = (ValueFlow)f;
                int width = Marshal.SerializeForHW(vflow.Value).Size;
                _widthMap[f.Target] = width;
            }
        }

        public int GetUncompressedValueWordWidth()
        {
            return _widthMap.Values.Sum();
        }

        public void Encode()
        {
            // This is a simple, inefficient greedy heuristic which essentially finds maximal cliques
            // within the graph of mutually exclusive signal targets.
            var mutexSet = new HashSet<Tuple<SignalRef, SignalRef>>();
            foreach (var pflow in _flowList)
            {
                var flows = pflow.Flows.ToArray();
                for (int i = 0; i < flows.Length; i++)
                {
                    for (int j = 0; j < flows.Length; j++)
                    {
                        var vf0 = (ValueFlow)flows[i];
                        var vf1 = (ValueFlow)flows[j];
                        if (!FlowMatrix.IsDontCareFlow(vf0) &&
                            !FlowMatrix.IsDontCareFlow(vf1) &&
                            !vf0.Value.Equals(vf1.Value))
                        {
                            mutexSet.Add(Tuple.Create(vf0.Target, vf1.Target));
                        }
                    }
                }
            }
            var order = _widthMap.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key);
            var list = new LinkedList<SignalRef>(order);
            int curOffset = 0;
            while (list.First != null)
            {
                var lln = list.First;
                var curGroup = new HashSet<SignalRef>();
                curGroup.Add(lln.Value);
                int width = _widthMap[lln.Value];
                _offsetMap[lln.Value] = curOffset;
                var next = lln.Next;
                list.Remove(lln);
                lln = next;
                while (lln != null)
                {
                    bool found = true;
                    foreach (var elem in curGroup)
                    {
                        if (mutexSet.Contains(Tuple.Create(elem, lln.Value)))
                        {
                            found = false;
                            break;
                        }
                    }
                    if (found)
                    {
                        curGroup.Add(lln.Value);
                        _offsetMap[lln.Value] = curOffset;
                        next = lln.Next;
                        list.Remove(lln);
                        lln = next;
                    }
                    else
                    {
                        lln = lln.Next;
                    }
                }
                curOffset += width;
            }
            ValueWordWidth = curOffset;
        }

        public int GetValueWordOffset(SignalRef target)
        {
            return _offsetMap[target];
        }

        public int GetValueWordWidth(SignalRef target)
        {
            return _widthMap[target];
        }
    }

    public class MicrocodeDesigner
    {
        private MicroString[] _strings;
        private ValueFlowCoder _vcf;

        public MicrocodeDesigner()
        {
            _vcf = new ValueFlowCoder();
        }

        public FlowMatrix FlowSpec { get; private set; }
        public int CWWidth { get; private set; }

        public string ComputeEncoding(FlowMatrix flowSpec, int maxSelWidth = 6)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Control word encoding report");
            sb.AppendFormat("  Number of c-steps: {0}", flowSpec.NumCSteps);
            sb.AppendLine();
            sb.AppendFormat("  Maximum LUT inputs: {0}", maxSelWidth);
            sb.AppendLine();

            FlowSpec = flowSpec;

            var flowMap = new Dictionary<SignalRef, List<Flow>>();

            var neutralFlow = flowSpec.NeutralFlow;
            _vcf.AddFlow(neutralFlow);
            for (int i = 0; i < flowSpec.NumCSteps; i++)
            {
                var pflow = flowSpec.GetFlow(i);
                var nflow = new ParFlow(neutralFlow);
                nflow.Integrate(pflow);
                _vcf.AddFlow(nflow);
                foreach (var flow in nflow.Flows)
                {
                    List<Flow> flows;
                    if (!flowMap.TryGetValue(flow.Target, out flows))
                    {
                        flows = new List<Flow>();
                        flowMap[flow.Target] = flows;
                    }
                    flows.Add(flow);
                }
            }
            _vcf.Encode();
            var startTime = DateTime.Now;

            var encFlows = flowMap.Values
                .Select((l, i) => new EncodedFlow(l, i)).ToArray();
            var uncompressedMuxBits = encFlows.Sum(ef => MathExt.CeilLog2(ef.NumSymbols));
            sb.AppendFormat("  Uncompressed CW: {0} MUX bits + {1} value bits",
                uncompressedMuxBits, _vcf.GetUncompressedValueWordWidth());
            sb.AppendLine();

            int numTargets = encFlows.Length;
            var mergeCandidates = new List<Tuple<int, int, MergedFlow>>();
            var indices = new SortedSet<int>(Enumerable.Range(0, numTargets));
            var curGen = (EncodedFlow[])encFlows.Clone();
            bool mergedAny;
            var nextCandidates = new List<Tuple<int, int, MergedFlow>>();

            do
            {
                foreach (int i in indices)
                {
                    if (curGen[i].NumSymbols <= 1)
                        continue;

                    var upview = indices.GetViewBetween(i + 1, numTargets);
                    foreach (int j in upview)
                    {
                        if (curGen[j].NumSymbols <= 1)
                            continue;

                        var mergedFlow = new MergedFlow(curGen[i], curGen[j]);
                        mergeCandidates.Add(Tuple.Create(i, j, mergedFlow));
                    }
                }

                var orderedMergeCandidates = mergeCandidates.OrderByDescending(t => t.Item3.Score);
                var nextGen = (EncodedFlow[])curGen.Clone();
                var mergedIndices = new HashSet<int>();
                var mergedLowIndices = new SortedSet<int>();
                var mergedHiIndices = new HashSet<int>();
                mergedAny = false;
                foreach (var tup in orderedMergeCandidates)
                {
                    Debug.Assert(tup.Item2 > tup.Item1);

                    var mergedFlow = tup.Item3;
                    if (mergedFlow.Score == 0.0)
                        break;

                    int selWidth = MathExt.CeilLog2(mergedFlow.NumSymbols);
                    if (selWidth > maxSelWidth)
                        continue;

                    if (mergedIndices.Contains(tup.Item1) ||
                        mergedIndices.Contains(tup.Item2))
                        continue;

                    mergedIndices.Add(tup.Item1);
                    mergedIndices.Add(tup.Item2);
                    mergedLowIndices.Add(tup.Item1);
                    mergedHiIndices.Add(tup.Item2);
                    indices.Remove(tup.Item2);

                    mergedFlow.Realize();
                    Debug.Assert(nextGen[tup.Item1].Targets.All(t => mergedFlow.Targets.Contains(t)));
                    Debug.Assert(nextGen[tup.Item2].Targets.All(t => mergedFlow.Targets.Contains(t)));
                    nextGen[tup.Item1] = mergedFlow;
                    mergedAny = true;
                }
                nextCandidates.Clear();
                curGen = nextGen;
                mergeCandidates.Clear();
                mergeCandidates.AddRange(nextCandidates);
            }
            while (mergedAny);

            _strings = indices.Select(i => new MicroString(curGen[i], _vcf)).ToArray();

            // Verification
            var coveredTargets = _strings.SelectMany(s => s.Targets);
            var allTargets = encFlows.SelectMany(f => f.Targets);
            var isect0 = coveredTargets.Except(allTargets);
            var isect1 = allTargets.Except(coveredTargets);
            Debug.Assert(!isect0.Any());
            Debug.Assert(!isect1.Any());
            //

            int offset = _vcf.ValueWordWidth;
            int order = 0;
            foreach (var ms in _strings)
            {
                ms.SelOffset = offset;
                ms.Order = order;
                offset += ms.SelWidth;
                order++;
            }
            CWWidth = offset;

            var stopTime = DateTime.Now;
            var runTime = stopTime - startTime;
            sb.AppendFormat("  Compressed CW: {0} MUX bits + {1} value bits",
                offset - _vcf.ValueWordWidth, _vcf.ValueWordWidth);
            sb.AppendLine();
            sb.AppendFormat("  Maximum LUT inputs: {0}", _strings.Max(s => s.SelWidth));
            sb.AppendFormat("  Running time: {0} ms", runTime.TotalMilliseconds);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Number of MUX inputs; Number of occurences");
            var histo = _strings.GroupBy(s => s.SelWidth)
                .OrderByDescending(grp => grp.Key);
            foreach (var grp in histo)
            {
                sb.AppendFormat("{0}; {1}", grp.Key, grp.Count());
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private void Encode(int cstep, ParFlow pflow, ref StdLogicVector cword)
        {
            foreach (var ms in _strings)
            {
                var projflow = new ParFlow();
                foreach (var target in ms.Targets)
                {
                    var flow = pflow.LookupTarget(target);
                    if (flow != null)
                        projflow.Add(flow);
                }
                ms.Encode(cstep, projflow, ref cword);
            }
        }

        public StdLogicVector Encode(int cstep, ParFlow cstepFlow)
        {
            var cword = StdLogicVector._0s(CWWidth);
            ParFlow allFlow = new ParFlow(FlowSpec.NeutralFlow);
            allFlow.Integrate(cstepFlow);
            Encode(cstep, allFlow, ref cword);
            return cword;
        }

        public void CreateDecoder(IAutoBinder binder, SLVSignal cwSignal)
        {
            var pbuilder = new DefaultAlgorithmBuilder();
            var sensitivity = new HashSet<ISignalOrPortDescriptor>();
            sensitivity.Add(cwSignal.Descriptor);
            foreach (var ms in _strings)
            {
                ms.AssembleDecoder(pbuilder, cwSignal, sensitivity);
            }
            var decFunc = pbuilder.Complete();
            decFunc.Name = "cwdecode";
            binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, decFunc, sensitivity.ToArray());
        }

        public void CreateStagedDecoder(IAutoBinder binder, SLVSignal cwSignal, SLSignal clkSignal, bool registered)
        {
            var valWordInit = StdLogicVector._0s(_vcf.ValueWordWidth);
            var rcwSignal = (SLVSignal)binder.GetSignal(EPortUsage.Default, "D1_CW", null, valWordInit);
            var rcwSignalDesc = rcwSignal.Descriptor;
            SLVSignal rrcwSignal = null;
            if (registered)
            {
                rrcwSignal = (SLVSignal)binder.GetSignal(EPortUsage.Default, "D2_CW", null, valWordInit);
            }

            var syncBuilder = new DefaultAlgorithmBuilder();
            syncBuilder.If(clkSignal.ToSignalRef(SignalRef.EReferencedProperty.RisingEdge));

            syncBuilder.Store(rcwSignal.ToSignalRef(SignalRef.EReferencedProperty.Next),
                ((ISignal)cwSignal[_vcf.ValueWordWidth - 1, 0]).ToSignalRef(SignalRef.EReferencedProperty.Cur));
            if (registered)
            {
                syncBuilder.Store(rrcwSignal.ToSignalRef(SignalRef.EReferencedProperty.Next),
                    rcwSignal.ToSignalRef(SignalRef.EReferencedProperty.Cur));
            }

            foreach (var ms in _strings)
            {
                ms.AssembleStagedDecoderSync(binder, syncBuilder, cwSignal, registered);
            }
            syncBuilder.EndIf();
            var syncFunc = syncBuilder.Complete();
            syncFunc.Name = "cwdecode_sync";
            binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, syncFunc, clkSignal.Descriptor);

            var combBuilder = new DefaultAlgorithmBuilder();
            var sensitivity = new HashSet<ISignalOrPortDescriptor>();
            sensitivity.Add(cwSignal.Descriptor);
            sensitivity.Add(rcwSignalDesc);
            if (registered)
                sensitivity.Add(rrcwSignal.Descriptor);
            foreach (var ms in _strings)
            {
                ms.AssembleStagedDecoderComb(combBuilder, registered ? rrcwSignal : rcwSignal, sensitivity, registered);
            }
            var combFunc = combBuilder.Complete();
            combFunc.Name = "cwdecode_comb";
            binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, combFunc, sensitivity.ToArray());
        }
    }
}

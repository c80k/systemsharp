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
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    public class FlowMatrix
    {
        private class FlowGraph
        {
            private Dictionary<SignalRef, Flow> _flows = new Dictionary<SignalRef, Flow>();
            private StringBuilder _comment = new StringBuilder();

            public void Add(Flow flow)
            {
                _flows[flow.Target] = flow;
            }

            public void Remove(SignalRef target)
            {
                _flows.Remove(target);
                Debug.Assert(_flows.Values.All(f => f is ValueFlow || !((SignalFlow)f).Source.Equals(target)));
            }

            public void AddComment(string comment)
            {
                _comment.AppendLine(comment);
            }

            public string Comment
            {
                get { return _comment.ToString(); }
            }

            public void Transitize()
            {
                Queue<Flow> q = new Queue<Flow>(_flows.Values);

                // DEBUG only
                // var copy = new List<KeyValuePair<SignalBase, Flow>>(_flows);

                while (q.Any())
                {
                    var flow = q.Dequeue();
                    SignalFlow sflow = flow as SignalFlow;
                    if (sflow != null)
                    {
                        Flow trflow;
                        if (_flows.TryGetValue(sflow.Source, out trflow))
                        {
                            SignalFlow strflow = trflow as SignalFlow;
                            if (strflow != null)
                            {
                                trflow = new SignalFlow(strflow.Source, sflow.Target);
                            }
                            ValueFlow vtrflow = trflow as ValueFlow;
                            if (vtrflow != null)
                            {
                                trflow = new ValueFlow(vtrflow.Value, sflow.Target);
                            }
                            _flows[sflow.Target] = trflow;
                            if (trflow.Equals(sflow))
                                throw new InvalidOperationException("Cyclic dataflow");

                            q.Enqueue(trflow);
                        }
                    }
                }
            }

            public ParFlow ToFlow()
            {
                return new ParFlow(_flows.Values);
            }

            public IEnumerable<SignalRef> FlowTargets
            {
                get { return _flows.Keys; }
            }

            public IEnumerable<SignalRef> FlowSources
            {
                get
                {
                    return _flows.Values
                        .Select(f => f as SignalFlow)
                        .Where(f => f != null)
                        .Select(f => f.Source)
                        .Distinct();
                }
            }

            public IEnumerable<Flow> GetFlowsTo(SignalRef target)
            {
                Flow flow;
                if (_flows.TryGetValue(target, out flow))
                    return Enumerable.Repeat(flow, 1);
                else
                    return Enumerable.Empty<Flow>();
            }
        }

        public static ValueFlow CreateDontCareFlow(SignalRef target)
        {
            return new ValueFlow(StdLogicVector.DCs(
                Marshal.SerializeForHW(target.Desc.InitialValue).Size), target);
        }

        public static bool IsDontCareFlow(Flow flow)
        {
            ValueFlow vflow = flow as ValueFlow;
            if (vflow == null)
                return false;
            StdLogicVector? slvdata = vflow.Value as StdLogicVector?;
            StdLogic? sldata = vflow.Value as StdLogic?;
            if (slvdata.HasValue)
                return (slvdata.Value.Equals(StdLogicVector.DCs(slvdata.Value.Size)));
            if (sldata.HasValue)
                return (sldata.Value.Equals(StdLogic.DC));
            return false;
        }

        public static ValueFlow AsDontCareFlow(ValueFlow vflow, StdLogic symbol)
        {
            StdLogicVector? slvdata = vflow.Value as StdLogicVector?;
            StdLogic? sldata = vflow.Value as StdLogic?;
            if (slvdata.HasValue)
                return new ValueFlow(StdLogicVector.AllSame(symbol, slvdata.Value.Size), vflow.Target);
            if (sldata.HasValue)
                return new ValueFlow(symbol, vflow.Target);

            return new ValueFlow(StdLogicVector.AllSame(symbol, Marshal.SerializeForHW(vflow.Value).Size), vflow.Target);
        }

        public static ValueFlow AsDontCareFlow(ValueFlow vflow)
        {
            return AsDontCareFlow(vflow, StdLogic.DC);
        }

        private List<FlowGraph> _graphs = new List<FlowGraph>();
        private FlowGraph _neutral = new FlowGraph();

        public int NumCSteps
        {
            get { return _graphs.Count; }
        }

        private void Reserve(int cstep)
        {
            while (cstep >= _graphs.Count)
                _graphs.Add(new FlowGraph());
        }

        public void Add(int cstep, Flow flow)
        {
            Debug.Assert(flow.Target.Desc.Owner != null ||
                ((DescriptorBase)flow.Target.Desc).HasAttribute<int>());

            if (flow.Target.Desc.ElementType == null)
                throw new ArgumentException();
            var sflow = flow as SignalFlow;
            if (sflow != null && sflow.Source.Desc.ElementType == null)
                throw new ArgumentException();

            Reserve(cstep);
            _graphs[cstep].Add(flow);
        }

        public void Add(int cstep, ParFlow pflow)
        {
            foreach (Flow flow in pflow.Flows)
                Add(cstep, flow);
        }

        public void Add(int cstep, IEnumerable<ParFlow> seq)
        {
            foreach (ParFlow pflow in seq)
                Add(cstep++, pflow);
        }

        public void Add(int cstep, IEnumerable<TAVerb> taseq)
        {
            Add(cstep, taseq.Select(v => v.ToCombFlow()));
        }

        public void Remove(int cstep, SignalRef target)
        {
            _graphs[cstep].Remove(target);
        }

        public ParFlow GetFlow(int cstep)
        {
            return _graphs[cstep].ToFlow();
        }

        public void AddNeutral(ParFlow pflow)
        {
            foreach (Flow flow in pflow.Flows)
                _neutral.Add(flow);
        }

        public void AddNeutral(Flow flow)
        {
            _neutral.Add(flow);
        }

        public void AddNeutral(TAVerb verb)
        {
            AddNeutral(verb.ToCombFlow());
        }

        public void AddNeutral(IEnumerable<TAVerb> taseq)
        {
            // Currently, any neutral transaction is expected to last exactly one clock step.
            AddNeutral(taseq.Single());
        }

        public ParFlow NeutralFlow
        {
            get { return _neutral.ToFlow(); }
        }

        public void AppendComment(int cstep, string comment)
        {
            Reserve(cstep);
            _graphs[cstep].AddComment(comment);
        }

        public string GetComment(int cstep)
        {
            return _graphs[cstep].Comment;
        }

        public void Transitize()
        {
            foreach (var g in _graphs)
                g.Transitize();
        }

        public IEnumerable<SignalRef> FlowTargets
        {
            get
            {
                return _graphs.SelectMany(g => g.FlowTargets)
                    .Concat(_neutral.FlowTargets)
                    .Distinct();
            }
        }

        public IEnumerable<SignalRef> FlowSources
        {
            get
            {
                return _graphs.SelectMany(g => g.FlowSources)
                    .Concat(_neutral.FlowSources)
                    .Distinct();
            }
        }

        public IEnumerable<Flow> GetFlowsTo(SignalRef target)
        {
            return _graphs.SelectMany(g => g.GetFlowsTo(target))
                .Union(_neutral.GetFlowsTo(target))
                .Distinct();
        }

        public IEnumerable<ITimedFlow> GetTimedFlows()
        {
            var result = new List<ITimedFlow>();
            var tempMap = new Dictionary<int, ITimedFlow>();
            for (int cstep = 0; cstep < _graphs.Count; cstep++)
            {
                var g = _graphs[cstep];
                var flows = g.ToFlow().Flows;
                foreach (var f in flows)
                {
                    var sf = f as SignalFlow;
                    var vf = f as ValueFlow;
                    if (sf != null)
                    {
                        ITimedFlow tf;
                        if (sf.Source.IsTemporary())
                        {
                            tf = tempMap[sf.Source.GetTemporaryIndex()];
                            var tsf = tf as TimedSignalFlow;
                            var tvf = tf as TimedValueFlow;
                            if (tsf != null)
                                tf = new TimedSignalFlow(tsf.Source, sf.Target, tf.Time, 0);
                            else
                                tf = new TimedValueFlow(tvf.Value, sf.Target, cstep);
                        }
                        else
                        {
                            tf = new TimedSignalFlow(sf.Source, sf.Target, cstep, 0);
                        }
                        if (f.Target.IsTemporary())
                        {
                            tempMap[f.Target.GetTemporaryIndex()] = tf;
                        }
                        else
                        {
                            var tsf = tf as TimedSignalFlow;
                            var tvf = tf as TimedValueFlow;
                            if (tsf != null)
                                tf = new TimedSignalFlow(tsf.Source, tf.Target, tf.Time, cstep - tf.Time);
                            else
                                tf = new TimedValueFlow(tvf.Value, tf.Target, cstep);
                            result.Add(tf);
                        }
                    }
                    else
                    {
                        var tf = new TimedValueFlow(vf.Value, f.Target, cstep);
                        if (f.Target.IsTemporary())
                        {
                            tempMap[f.Target.GetTemporaryIndex()] = tf;
                        }
                        else
                        {
                            result.Add(tf);
                        }
                    }
                }
            }
            return result;
        }

        public string GetMUXReport()
        {
            StringBuilder sb = new StringBuilder();
            var flows = FlowTargets
                .Select(t => Tuple.Create(t, GetFlowsTo(t)))
                .OrderBy(tup => tup.Item2.Count());
            foreach (var tup in flows)
            {
                sb.AppendFormat("MUX target {0}, fan-in: {1}\n",
                    tup.Item1.ToString(),
                    tup.Item2.Count());
                foreach (var flow in tup.Item2)
                {
                    sb.AppendFormat("  {0}\n", flow.ToString());
                }
            }
            return sb.ToString();
        }

        public string GetMUXHisto()
        {
            var histo = FlowTargets
                .Select(t => Tuple.Create(t, GetFlowsTo(t).Count()))
                .GroupBy(tup => tup.Item2)
                .Select(grp => Tuple.Create(grp.Key, grp.Count()))
                .OrderBy(tup => tup.Item1)
                .Select(tup => tup.Item1.ToString() + ";" + tup.Item2.ToString() + ";");
            return string.Join(Environment.NewLine, histo);
        }

        public string GetTimedFlowReport()
        {
            return string.Join(Environment.NewLine, GetTimedFlows());
        }

        public string GetFlowReport()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _graphs.Count; i++)
            {
                sb.AppendLine(i + ":");
                var pflow = _graphs[i].ToFlow();
                foreach (var flow in pflow.Flows)
                {
                    sb.AppendLine("  " + flow.ToString());
                }
            }
            return sb.ToString();
        }

        public void ReplaceDontCares()
        {
            var picks = new Dictionary<SignalRef, Flow>();
            foreach (var target in FlowTargets)
            {
                Flow pick = GetFlowsTo(target)
                    .Where(f => !IsDontCareFlow(f))
                    .FirstOrDefault();
                if (pick == null)
                {
                    ValueFlow vflow = (ValueFlow)GetFlowsTo(target).First();
                    StdLogicVector data = (StdLogicVector)vflow.Value;
                    pick = new ValueFlow(StdLogicVector._0s(data.Size), target);
                }
                picks[target] = pick;
            }
            var allGraphs = _graphs.Concat(Enumerable.Repeat(_neutral, 1));
            foreach (FlowGraph g in allGraphs)
            {
                var pflow = g.ToFlow();
                foreach (Flow flow in pflow.Flows)
                {
                    if (IsDontCareFlow(flow))
                    {
                        Flow pick = picks[flow.Target];
                        g.Add(pick);
                    }
                }
            }
        }

        public void ReplaceDontCaresByTriStates()
        {
            var allGraphs = _graphs.Concat(Enumerable.Repeat(_neutral, 1));
            foreach (FlowGraph g in allGraphs)
            {
                var pflow = g.ToFlow();
                foreach (Flow flow in pflow.Flows)
                {
                    if (IsDontCareFlow(flow))
                    {
                        var oflow = flow as ValueFlow;
                        var slv = (StdLogicVector)oflow.Value;
                        var zslv = StdLogicVector.Zs(slv.Size);
                        var nflow = new ValueFlow(zslv, flow.Target);
                        g.Add(nflow);
                    }
                }
            }
        }

        public void RemoveDontCares()
        {
            var allGraphs = _graphs.Concat(Enumerable.Repeat(_neutral, 1));
            foreach (var g in allGraphs)
            {
                foreach (var flow in g.ToFlow().Flows)
                {
                    if (IsDontCareFlow(flow))
                        g.Remove(flow.Target);
                }
            }
        }
    }

    public static class FlowExtensions
    {
        public static void TagTemporary(this SignalDescriptor desc, int tempIndex)
        {
            desc.AddAttribute(tempIndex);
        }

        public static void TagTemporary(this SignalRef sref, int tempIndex)
        {
            TagTemporary(((SignalDescriptor)sref.Desc), tempIndex);
        }

        public static void TagEndpoint(this SignalDescriptor desc)
        {
            desc.RemoveAttribute<int>();
        }

        public static void TagEndpoint(this SignalRef sref)
        {
            TagEndpoint((SignalDescriptor)sref.Desc);
        }

        public static bool IsTemporary(this SignalRef sref)
        {
            return ((DescriptorBase)sref.Desc).HasAttribute<int>();
        }

        public static int GetTemporaryIndex(this SignalRef sref)
        {
            if (IsTemporary(sref))
                return ((DescriptorBase)sref.Desc).QueryAttribute<int>();
            else
                return -1;
        }

        public static bool IsEndToEnd(this SignalFlow flow)
        {
            return !flow.Source.IsTemporary() && !flow.Target.IsTemporary();
        }
    }
}

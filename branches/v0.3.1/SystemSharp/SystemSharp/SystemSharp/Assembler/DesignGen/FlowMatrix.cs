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
using SystemSharp.Components;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    /// <summary>
    /// A flow matrix models timed dataflows between data sources and sinks. It is the fundamental abstraction used for interconnect and
    /// control path construction.
    /// </summary>
    /// <remarks>
    /// <para>You can image a flow matrix as a cubic adjacency matrix, describing a data transfer between a source and a sink at a given time
    /// (although it is not implemented that way). Another way to look at the flow matrix is to understand it as a list of "transfer orders",
    /// each order describing a data source, a data sink and a time (i.e. c-step) when the transfer is active. Data sources and sinks may be 
    /// concrete ports and signals, but they might also be virtual registers - meant to be replaced by concrete signals during interconnect construction.
    /// Moreover, the flow matrix introduces the concept of so-called neutral dataflows. These act as default dataflows, being active
    /// whenever there is no explicit dataflow specified at a given c-step.</para>
    /// <para>As the flow matrix is used in many contexts, and as the needs of algorithms using it vary, the flow matrix provides a bunch of methods
    /// to query information and to manipulate it.</para>
    /// </remarks>
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

        /// <summary>
        /// Creates a dataflow which transfers the "don't care" literal to a signal target of choice
        /// </summary>
        /// <param name="target">signal target</param>
        /// <returns>the resulting dataflow</returns>
        public static ValueFlow CreateDontCareFlow(SignalRef target)
        {
            return new ValueFlow(StdLogicVector.DCs(
                Marshal.SerializeForHW(target.Desc.InitialValue).Size), target);
        }

        /// <summary>
        /// Checks whether a given dataflow transfers the don't care literal to its destination.
        /// </summary>
        /// <param name="flow">a dataflow</param>
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

        /// <summary>
        /// Replaces the data symbol of a value-flow with the specified symbol. 
        /// If the old data symbol is a logic vector, the specified symbol is replicated to a vector.
        /// </summary>
        /// <param name="vflow">a value-flow</param>
        /// <param name="symbol">replacement symbol</param>
        /// <returns>new value-flow - same target, but different data symbol</returns>
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

        /// <summary>
        /// Replaces the data symbol of a value-flow with the don't care symbol
        /// </summary>
        /// <param name="vflow">a value-flow</param>
        /// <returns>new value-flow - same target, but with don't care symbol</returns>
        public static ValueFlow AsDontCareFlow(ValueFlow vflow)
        {
            return AsDontCareFlow(vflow, StdLogic.DC);
        }

        private List<FlowGraph> _graphs = new List<FlowGraph>();
        private FlowGraph _neutral = new FlowGraph();

        /// <summary>
        /// Number of c-steps described by flow matrix
        /// </summary>
        public int NumCSteps
        {
            get { return _graphs.Count; }
        }

        private void Reserve(int cstep)
        {
            while (cstep >= _graphs.Count)
                _graphs.Add(new FlowGraph());
        }

        /// <summary>
        /// Adds a dataflow to the flow matrix at specified c-step
        /// </summary>
        /// <param name="cstep">c-step when the dataflow is active</param>
        /// <param name="flow">the dataflow to add</param>
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

        /// <summary>
        /// Adds an aggregate dataflow to the flow matrix at specified c-step
        /// </summary>
        /// <param name="cstep">c-step when the dataflow is active</param>
        /// <param name="pflow">the aggregate dataflow to add</param>
        public void Add(int cstep, ParFlow pflow)
        {
            foreach (Flow flow in pflow.Flows)
                Add(cstep, flow);
        }

        /// <summary>
        /// Adds a sequence of aggregate dataflows to the flow matrix. Beginning from specified c-step,
        /// each following aggregate dataflow is scheduled one c-step later.
        /// </summary>
        /// <param name="cstep">c-step when the first aggregate dataflow is active</param>
        /// <param name="seq">sequence of aggregate dataflows</param>
        public void Add(int cstep, IEnumerable<ParFlow> seq)
        {
            foreach (ParFlow pflow in seq)
                Add(cstep++, pflow);
        }

        /// <summary>
        /// Adds a sequence of transaction verbs to the flow matrix. Beginning from specified c-step,
        /// each following transaction verb is scheduled one c-step later. The transaction verbs are converted
        /// to dataflows during this procedure.
        /// </summary>
        /// <param name="cstep">c-step when the first transaction verb is active</param>
        /// <param name="taseq">sequence of transaction verbs</param>
        public void Add(int cstep, IEnumerable<TAVerb> taseq)
        {
            Add(cstep, taseq.Select(v => v.ToCombFlow()));
        }

        /// <summary>
        /// Removes the dataflow targeting specified signal at specified c-step
        /// </summary>
        /// <param name="cstep">the c-step</param>
        /// <param name="target">target to which dataflow is to be removed</param>
        public void Remove(int cstep, SignalRef target)
        {
            _graphs[cstep].Remove(target);
        }

        /// <summary>
        /// Returns all active flows for a given c-step.
        /// </summary>
        /// <param name="cstep">c-step to query</param>
        /// <returns>an aggregate dataflow covering all active dataflows</returns>
        public ParFlow GetFlow(int cstep)
        {
            return _graphs[cstep].ToFlow();
        }

        /// <summary>
        /// Adds an aggregate dataflow describing the neutral (default) dataflow for all specified targets.
        /// </summary>
        /// <param name="pflow">the neutral dataflow as aggregate dataflow</param>
        public void AddNeutral(ParFlow pflow)
        {
            foreach (Flow flow in pflow.Flows)
                _neutral.Add(flow);
        }

        /// <summary>
        /// Adds a neutral (default) dataflow for a specific signal target.
        /// </summary>
        /// <param name="flow">the neutral dataflow</param>
        public void AddNeutral(Flow flow)
        {
            _neutral.Add(flow);
        }

        /// <summary>
        /// Adds a transaction verb to describe the neutral (default) dataflows for all specified signal targets.
        /// </summary>
        /// <param name="verb">a transaction verb intended as neutral flow</param>
        public void AddNeutral(TAVerb verb)
        {
            AddNeutral(verb.ToCombFlow());
        }

        /// <summary>
        /// Adds a neutral transaction to the flow matrix. The transaction is expected to contain exactly one element.
        /// </summary>
        /// <param name="taseq">neutral transaction</param>
        public void AddNeutral(IEnumerable<TAVerb> taseq)
        {
            Contract.Requires<ArgumentNullException>(taseq != null);
            Contract.Requires<ArgumentException>(taseq.Count() == 1, "Neutral transactions are required to consist of exactly one c-step");

            AddNeutral(taseq.Single());
        }

        /// <summary>
        /// Returns the neutral (default) dataflows to all known signal targets.
        /// </summary>
        public ParFlow NeutralFlow
        {
            get { return _neutral.ToFlow(); }
        }

        /// <summary>
        /// Appends a documentary comment to a certain c-step. This method may be called multiple times for the same c-step.
        /// All comments for the same c-step will be concatenated.
        /// </summary>
        /// <param name="cstep">c-step where comment should be attached</param>
        /// <param name="comment">the comment</param>
        public void AppendComment(int cstep, string comment)
        {
            Reserve(cstep);
            _graphs[cstep].AddComment(comment);
        }

        /// <summary>
        /// Returns the concatenated comments for specified c-step.
        /// </summary>
        /// <param name="cstep">c-step to query for</param>
        /// <returns>concatenated comments</returns>
        public string GetComment(int cstep)
        {
            return _graphs[cstep].Comment;
        }

        /// <summary>
        /// Eliminates all transitive dataflows inside the flow matrix. I.e. any two dataflows in the form (a -> b), (b -> c) will be replaced
        /// by a single dataflow (a -> c). This transformation keeps the semantics of the flow matrix.
        /// </summary>
        public void Transitize()
        {
            foreach (var g in _graphs)
                g.Transitize();
        }

        /// <summary>
        /// Returns all signals targeted by any dataflow inside the matrix
        /// </summary>
        public IEnumerable<SignalRef> FlowTargets
        {
            get
            {
                return _graphs.SelectMany(g => g.FlowTargets)
                    .Concat(_neutral.FlowTargets)
                    .Distinct();
            }
        }

        /// <summary>
        /// Returns all signals being the source of any dataflow inside the matrix
        /// </summary>
        public IEnumerable<SignalRef> FlowSources
        {
            get
            {
                return _graphs.SelectMany(g => g.FlowSources)
                    .Concat(_neutral.FlowSources)
                    .Distinct();
            }
        }

        /// <summary>
        /// Returns all dataflows targeting specified signal, regardless of their activation times.
        /// </summary>
        /// <param name="target">signal target to query for</param>
        /// <returns>all dataflows targeting specified signal</returns>
        public IEnumerable<Flow> GetFlowsTo(SignalRef target)
        {
            return _graphs.SelectMany(g => g.GetFlowsTo(target))
                .Union(_neutral.GetFlowsTo(target))
                .Distinct();
        }

        /// <summary>
        /// Converts the flow matrix to a sequence of timed dataflows
        /// </summary>
        /// <returns>sequence of timed dataflows</returns>
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

        /// <summary>
        /// Returns a documentary report describing the quantities and sizes of multiplexers which are induced by this
        /// flow matrix.
        /// </summary>
        /// <returns>textual report</returns>
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

        /// <summary>
        /// Returns a documentary string describing a histogram of multiplexer sizes which are induced by this flow matrix.
        /// </summary>
        /// <returns>textual histogram</returns>
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

        /// <summary>
        /// Converts the timed dataflow representation of this flow matrix to a textual description.
        /// </summary>
        /// <returns>textual description of timed dataflows</returns>
        public string GetTimedFlowReport()
        {
            return string.Join(Environment.NewLine, GetTimedFlows());
        }

        /// <summary>
        /// Returns a textual description of this flow matrix.
        /// </summary>
        /// <returns>textual description</returns>
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

        /// <summary>
        /// Replaces all value-flows transferring don't-care values by real values.
        /// </summary>
        /// <remarks>
        /// The semantics of the don't-care symbol admit any such symbol to be replaced with any other symbol, e.g. '0' or '1'
        /// without changing the behavior. However, if we blindly replace any don't care symbol with - let's say - logical zeroes,
        /// we won't perform optimally, since we might introduce unnecessary multiplexers. Therefore, the method first tries to
        /// find existing non-don't-care value-flows as suitable replacement candidates. Only if no such is found, it arbitrarily
        /// chooses to replace don't-cares with logical zeroes.
        /// </remarks>
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

        /// <summary>
        /// Replaces any don't-care symbol with the high-impedance symbol.
        /// </summary>
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

        /// <summary>
        /// Removes all value-flows containing don't-care values from this matrix.
        /// </summary>
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
        /// <summary>
        /// Tags a signal descriptor as virtual register, meant to be replaced with a different signal created at some later stage.
        /// </summary>
        /// <param name="desc">signal descriptor to tag</param>
        /// <param name="tempIndex">virtual register index</param>
        public static void TagTemporary(this SignalDescriptor desc, int tempIndex)
        {
            desc.AddAttribute(tempIndex);
        }

        /// <summary>
        /// Tags a signal reference as virtual register, meant to be replaced with a different signal created at some later stage.
        /// </summary>
        /// <param name="sref">signal reference</param>
        /// <param name="tempIndex">virtual register index</param>
        public static void TagTemporary(this SignalRef sref, int tempIndex)
        {
            TagTemporary(((SignalDescriptor)sref.Desc), tempIndex);
        }

        /// <summary>
        /// Tells whether a given signal reference is used as a virtual register.
        /// </summary>
        /// <param name="sref">signal reference to query</param>
        /// <returns>whether given signal reference is used as a virtual register</returns>
        public static bool IsTemporary(this SignalRef sref)
        {
            return ((DescriptorBase)sref.Desc).HasAttribute<int>();
        }

        /// <summary>
        /// Retrieves the virtual register index of a given signal reference
        /// </summary>
        /// <param name="sref">signal reference to query</param>
        /// <returns>its virtual register index, or -1 if the reference is not used as a virtual register</returns>
        public static int GetTemporaryIndex(this SignalRef sref)
        {
            if (IsTemporary(sref))
                return ((DescriptorBase)sref.Desc).QueryAttribute<int>();
            else
                return -1;
        }

        /// <summary>
        /// Tells whether a given signal-flow is between real signals, with no virtual register involved.
        /// </summary>
        /// <param name="flow">signal-flow to query</param>
        /// <returns>whether both endpoints are non-virtual</returns>
        public static bool IsEndToEnd(this SignalFlow flow)
        {
            return !flow.Source.IsTemporary() && !flow.Target.IsTemporary();
        }
    }
}

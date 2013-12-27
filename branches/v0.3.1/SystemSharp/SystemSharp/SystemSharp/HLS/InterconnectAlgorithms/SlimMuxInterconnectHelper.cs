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
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.SysDOM;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Assembler.DesignGen
{
    class SlimMuxInterconnectHelper : ISlimMuxAdapter<int, int, TimedSignalFlow>
    {
        private class PipeInfo
        {
            public int source;
            public int sink;
            public long delay;
            public int capacity;
            public bool useEn;
        }

        private struct ResInfo
        {
            public int emitter;
            public long depTime;
        }

        private CacheDictionary<SignalRef, int> _signal2Idx;
        private IPropMap<TimedSignalFlow, long> _depTime =
            new DelegatePropMap<TimedSignalFlow, long>(tf => tf.Time);
        private IPropMap<TimedSignalFlow, long> _arrTime =
            new DelegatePropMap<TimedSignalFlow, long>(tf => tf.Time + tf.Delay);
        private IPropMap<TimedSignalFlow, int> _dep;
        private IPropMap<TimedSignalFlow, int> _dst;
        private List<ISignal> _pipeInSignals = new List<ISignal>();
        private List<ISignal> _pipeOutSignals = new List<ISignal>();
        private ISignal[] _pipeEnSignals;
        private List<int> _icrCurSignals = new List<int>();
        private List<PipeInfo> _pipes = new List<PipeInfo>();
        private List<Dictionary<long, ResInfo>> _resTable = new List<Dictionary<long, ResInfo>>();
        private IPropMap<int, int> _pipeSource;
        private IPropMap<int, int> _pipeSink;
        private IPropMap<int, long> _pipeDelay;
        private List<List<int>> _succs = new List<List<int>>();
        private List<List<int>> _preds = new List<List<int>>();
        private IPropMap<int, IEnumerable<int>> _pmSuccs;
        private IPropMap<int, IEnumerable<int>> _pmPreds;
        private IPropMap<int, bool> _isFixed;
        private IAutoBinder _binder;

        public SlimMuxInterconnectHelper(IAutoBinder binder)
        {
            _binder = binder;
            _signal2Idx = new CacheDictionary<SignalRef, int>(CreateFUSignalIndex);
            _dep = new DelegatePropMap<TimedSignalFlow, int>(tf => _signal2Idx[tf.Source]);
            _dst = new DelegatePropMap<TimedSignalFlow, int>(tf => _signal2Idx[tf.Target]);
            _pipeSource = new DelegatePropMap<int, int>(GetPipeSource);
            _pipeSink = new DelegatePropMap<int, int>(GetPipeSink);
            _pipeDelay = new DelegatePropMap<int, long>(GetPipeDelay);
            _pmPreds = new DelegatePropMap<int, IEnumerable<int>>(GetPreds);
            _pmSuccs = new DelegatePropMap<int, IEnumerable<int>>(GetSuccs);
            _isFixed = new DelegatePropMap<int, bool>(GetIsFixed);
        }

        private int CreateFUSignalIndex(SignalRef fuSignal)
        {
            int result = _pipeInSignals.Count;
            _pipeInSignals.Add(fuSignal.ToSignal());
            _succs.Add(new List<int>());
            _preds.Add(new List<int>());
            return result;
        }

        private bool GetIsFixed(int signalIdx)
        {
            return !_icrCurSignals.Contains(signalIdx);
        }

        private int GetPipeSource(int ipipe)
        {
            Contract.Requires<ArgumentException>(ipipe >= 0);
            Contract.Requires<ArgumentException>(ipipe < _pipes.Count);

            return _pipes[ipipe].source;
        }

        private int GetPipeSink(int ipipe)
        {
            Contract.Requires<ArgumentException>(ipipe >= 0);
            Contract.Requires<ArgumentException>(ipipe < _pipes.Count);

            return _pipes[ipipe].sink;
        }

        private long GetPipeDelay(int ipipe)
        {
            Contract.Requires<ArgumentException>(ipipe >= 0);
            Contract.Requires<ArgumentException>(ipipe < _pipes.Count);

            return _pipes[ipipe].delay;
        }

        private IEnumerable<int> GetPreds(int inode)
        {
            Contract.Requires<ArgumentException>(inode >= 0);
            Contract.Requires<ArgumentException>(inode < _preds.Count);

            return _preds[inode];
        }

        private IEnumerable<int> GetSuccs(int inode)
        {
            Contract.Requires<ArgumentException>(inode >= 0);
            Contract.Requires<ArgumentException>(inode < _preds.Count);

            return _succs[inode];
        }

        public IPropMap<TimedSignalFlow, long> DepartureTime
        {
            get { return _depTime; }
        }

        public IPropMap<TimedSignalFlow, long> ArrivalTime
        {
            get { return _arrTime; }
        }

        public IPropMap<TimedSignalFlow, int> Departure
        {
            get { return _dep; }
        }

        public IPropMap<TimedSignalFlow, int> Destination
        {
            get { return _dst; }
        }

        public IPropMap<int, int> Source
        {
            get { return _pipeSource; }
        }

        public IPropMap<int, int> Sink
        {
            get { return _pipeSink; }
        }

        public IPropMap<int, long> Delay
        {
            get { return _pipeDelay; }
        }

        public IPropMap<int, bool> IsEndpoint
        {
            get { return _isFixed; }
        }

        public IPropMap<int, IEnumerable<int>> Succs
        {
            get { return _pmSuccs; }
        }

        public IPropMap<int, IEnumerable<int>> Preds
        {
            get { return _pmPreds; }
        }

        public int AddPipe(int source, int sink, long delay)
        {
            // Basic check against cycles. "Obvious" cycles (1/2 edges) only!
            bool isCycle = source == sink ||
                _preds[source].Select(p => _pipes[p].source).Contains(sink) ||
                _succs[sink].Select(p => _pipes[p].sink).Contains(source);
            
            //Debug.Assert(source != sink);
            //Debug.Assert(!_preds[source].Select(p => _pipes[p].source).Contains(sink));
            //Debug.Assert(!_succs[sink].Select(p => _pipes[p].sink).Contains(source));

            if (isCycle)
            {
                string before = GetInterconnectGraphForDotty();
                File.WriteAllText("SlimMuxBugReport_before.dotty", before);
            }

            // Disallow redundant pipes of delay 0
            Debug.Assert(delay > 0 || _pipes.All(p => p.source != source || p.sink != sink || p.delay != delay));

            var pi = new PipeInfo()
            {
                source = source,
                sink = sink,
                delay = delay
            };
            int idx = _pipes.Count;
            _pipes.Add(pi);
            _preds[sink].Add(idx);
            _succs[source].Add(idx);
            _resTable.Add(new Dictionary<long, ResInfo>());

            if (isCycle)
            {
                string after = GetInterconnectGraphForDotty();
                File.WriteAllText("SlimMuxBugReport_after.dotty", after);

                // roll back
                _pipes.RemoveAt(_pipes.Count - 1);
                _preds[sink].RemoveAt(_preds[sink].Count - 1);
                _succs[source].RemoveAt(_succs[source].Count - 1);
                _resTable.RemoveAt(_resTable.Count - 1);

                Debug.Fail("Cycle detected");
            }

            return idx;
        }

        public void SplitPipe(int pipe, long delay1, long delay2, out int mid, out int left, out int right)
        {
            var pi = _pipes[pipe];
            Debug.Assert(pi.delay == delay1 + delay2);
            int mididx = _pipeInSignals.Count;
            mid = mididx;
            object initval = _pipeInSignals[pi.source].InitialValueObject;
            int icridx = _icrCurSignals.Count;
            var curSignal = _binder.GetSignal(EPortUsage.Default, "fifo" + icridx + "_in", null, initval);
            _pipeInSignals.Add(curSignal);
            _icrCurSignals.Add(mid);

            _preds.Add(new List<int>());
            _succs.Add(new List<int>());
            var piLeft = new PipeInfo()
            {
                source = pi.source,
                sink = mididx,
                delay = delay1
            };
            var piRight = new PipeInfo()
            {
                source = mididx,
                sink = pi.sink,
                delay = delay2
            };
            _pipes[pipe] = piLeft;
            left = pipe;
            int rightidx = _pipes.Count;
            right = rightidx;
            _pipes.Add(piRight);

            _preds[pi.sink].Remove(pipe);
            _preds[mididx].Add(pipe);
            _succs[mididx].Add(rightidx);
            _preds[pi.sink].Add(rightidx);

            var rmap = new Dictionary<long, ResInfo>();
            _resTable.Add(rmap);
            foreach (var kvp in _resTable[pipe])
            {
                rmap[kvp.Key + delay1] = kvp.Value;
            }
        }

        public void BindPipe(int pipe, long time, int emitter, long emitTime)
        {
            Debug.Assert(!_resTable[pipe].ContainsKey(time));
            _resTable[pipe][time] = new ResInfo()
            {
                depTime = emitTime,
                emitter = emitter
            };
        }

        public bool IsPipeBound(int pipe, long time, out int emitter, out long emitTime)
        {
            ResInfo ri;
            if (_resTable[pipe].TryGetValue(time, out ri))
            {
                emitter = ri.emitter;
                emitTime = ri.depTime;
                return true;
            }
            else
            {
                emitter = -1;
                emitTime = -1;
                return false;
            }
        }

        public IEnumerable<ISignal> PipeInSignals
        {
            get { return _icrCurSignals.Select(i => _pipeInSignals[i]); }
        }

        public IEnumerable<ISignal> PipeOutSignals
        {
            get { return _pipeOutSignals; }
        }

        public IEnumerable<RegPipe> InstantiatePipes()
        {
            _pipeEnSignals = new ISignal[_pipes.Count];
            for (int i = 0; i < _pipes.Count; i++)
            {
                var pi = _pipes[i];
                var srcsig = _pipeInSignals[pi.source];
                object initval = srcsig.InitialValueObject;
                int width = TypeLowering.Instance.GetWireWidth(
                    TypeDescriptor.GetTypeOf(srcsig.InitialValueObject));
                int capa = _pipes[i].capacity;
                if (capa == 0)
                {
                    _pipeOutSignals.Add(_pipeInSignals[pi.source]);
                    continue;
                }
                var outSignal = _binder.GetSignal(
                    EPortUsage.Default, "fifo" + pi.source + "_out" + pi.sink + "_" + i,
                    null, initval);
                _pipeOutSignals.Add(outSignal);
                bool useEn = capa < pi.delay;
                _pipes[i].capacity = capa;
                _pipes[i].useEn = useEn;

                RegPipe rpipe = new RegPipe(capa, width, useEn)
                {
                    Din = (In<StdLogicVector>)_pipeInSignals[pi.source],
                    Dout = (Out<StdLogicVector>)outSignal
                };

                if (capa > 0)
                {
                    rpipe.Clk = (In<StdLogic>)_binder.GetSignal(EPortUsage.Clock, "Clk", null, null);
                }

                if (useEn)
                {
                    _pipeEnSignals[i] = _binder.GetSignal(
                        EPortUsage.Default,
                        "fifoen" + i, null, StdLogic._0);
                    rpipe.En = (In<StdLogic>)_pipeEnSignals[i];
                }

                yield return rpipe;
            }
        }

        public bool[,] ComputePipeEnMatrix(int numCsteps)
        {
            var result = new bool[numCsteps, _pipes.Count];
            var q = new LinkedList<long>();
            var ts = new SortedSet<long>();
            for (int pipe = 0; pipe < _pipes.Count; pipe++)
            {
                int capa = CalcPipeCapacity(pipe);
                long delay = _pipes[pipe].delay;

                if (delay == 0)
                    continue;

                var reservations = _resTable[pipe];
                var restimes = reservations.Keys.OrderBy(k => k);

                bool pass = false;

                do
                {
                    q.Clear();
                    ts.Clear();
                    foreach (long time in restimes)
                    {
                        while (q.Count > 0 && q.First.Value < time)
                        {
                            ts.Add(q.First.Value);
                            result[q.First.Value, pipe] = true;
                            q.RemoveFirst();
                        }
                        ts.Add(time);
                        ts.Add(time + delay);
                        Debug.Assert(time + delay < numCsteps);
                        result[time, pipe] = true;
                        if (q.Count > 0)
                            q.RemoveFirst();
                        long post = delay - capa + q.Count + 1;
                        for (long strobe = post; strobe < delay; strobe++)
                        {
                            long mark = time + strobe;
                            if (q.Count == 0 || q.Last.Value < mark)
                                q.AddLast(mark);
                        }
                    }
                    while (q.Count > 0)
                    {
                        result[q.First.Value, pipe] = true;
                        ts.Add(q.First.Value);
                        q.RemoveFirst();
                    }

                    // Verify
                    var afetchtimes = restimes.Select(t => t + delay).ToArray();
                    int nf = 0;
                    var fifo = new LinkedList<long>();
                    pass = true;
                    foreach (long time in ts)
                    {
                        Debug.Assert(nf < afetchtimes.Length);
                        if (afetchtimes[nf] == time)
                        {
                            Debug.Assert(fifo.Count == capa);
                            //Debug.Assert(fifo.First.Value == time - delay);
                            if (fifo.First.Value != time - delay)
                            {
                                pass = false;
                                ++capa;
                                break;
                            }
                            nf++;
                        }
                        Debug.Assert(time < numCsteps);
                        if (result[time, pipe])
                        {
                            fifo.AddLast(time);
                            if (fifo.Count > capa)
                                fifo.RemoveFirst();
                        }
                    }

                    Debug.Assert(capa <= delay);

                } while (!pass);

                _pipes[pipe].capacity = capa;
                _pipes[pipe].useEn = capa < delay;
            }
            return result;
        }

        public IEnumerable<ParFlow> ToFlow(int numCsteps, ParFlow neutralFlow, bool[,] pipeEnMatrix)
        {
            int muxCount = _pipeInSignals.Count;
            int[,] flowMatrix = new int[numCsteps, muxCount];
            
            // 1st pass: set some arbitrary MUX selection
            for (int i = 0; i < numCsteps; i++)
            {
                for (int j = 0; j < muxCount; j++)
                {
                    if (_preds[j].Any())
                        flowMatrix[i, j] = _preds[j].First();
                    else
                        flowMatrix[i, j] = -1;
                }
            }

            // 2nd pass: reset MUX selection whenever neutral flow requires
            //           some value transfer which is not "don't care"
            foreach (var flow in neutralFlow.Flows)
            {
                if (FlowMatrix.IsDontCareFlow(flow) || 
                    !_signal2Idx.IsCached(flow.Target))
                    continue;

                int idx = _signal2Idx[flow.Target];
                for (int i = 0; i < numCsteps; i++)
                {
                    flowMatrix[i, idx] = -1;
                }
            }

            // 3rd pass: transfer MUX reservations to matrix
            for (int i = 0; i < _resTable.Count; i++)
            {
                var rmap = _resTable[i];
                var pi = _pipes[i];
                foreach (var kvp in rmap)
                {
                    flowMatrix[kvp.Key + pi.delay, pi.sink] = i;
                }
            }

            var pipeen = pipeEnMatrix;

            // last pass: convert to flows
            for (int i = 0; i < numCsteps; i++)
            {
                var flows = new List<Flow>();
                for (int j = 0; j < muxCount; j++)
                {
                    int k = flowMatrix[i, j];
                    if (k >= 0)
                    {
                        flows.Add(
                            new SignalFlow(
                                _pipeOutSignals[k].ToSignalRef(SignalRef.EReferencedProperty.Cur),
                                _pipeInSignals[j].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                    }
                }
                for (int pipe = 0; pipe < _pipes.Count; pipe++)
                {
                    if (!_pipes[pipe].useEn)
                        continue;
                    flows.Add(
                        new ValueFlow(
                            pipeen[i, pipe] ? StdLogic._1 : StdLogic._0,
                            _pipeEnSignals[pipe].ToSignalRef(SignalRef.EReferencedProperty.Next)));
                }
                yield return new ParFlow(flows);
            }
        }

        private int CalcPipeCapacity(int pipe)
        {
            Contract.Requires<ArgumentException>(pipe >= 0);

            long delay = _pipes[pipe].delay;
            var reservations = _resTable[pipe];
            var restimes = reservations.Keys.OrderBy(k => k);
            var q = new LinkedList<long>();
            int capa = 0;
            foreach (long time in restimes)
            {
                q.AddLast(time);
                long kicktime = time - delay;
                if (q.First.Value <= kicktime)
                    q.RemoveFirst();
                if (q.Count > capa)
                    capa = q.Count;
            }
            Debug.Assert(capa <= delay);
            return capa;
        }

        public string GetMUXReport()
        {
            StringBuilder sb = new StringBuilder();
            var dset = _preds
                .Select((p, i) => Tuple.Create(i, p))
                .OrderBy(tup => tup.Item2.Count);
            foreach (var tup in dset)
            {
                sb.AppendFormat("MUX of {0}, fanin = {1}",
                    _pipeInSignals[tup.Item1].ToSignalRef(SignalRef.EReferencedProperty.Instance).Desc.Name,
                    tup.Item2.Count);
                sb.AppendLine();
                foreach (var src in tup.Item2)
                {
                    sb.AppendFormat("  <= {0}",
                        _pipeInSignals[_pipes[src].source].ToSignalRef(SignalRef.EReferencedProperty.Instance).Desc.Name);
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        public string GetMUXHisto()
        {
            return string.Join(Environment.NewLine,
                _preds.Select(p => p.Count)
                    .Where(c => c >= 2)
                    .GroupBy(c => c)
                    .OrderBy(g => g.Key)
                    .Select(g => g.Key + ";" + g.Count() + ";"));
        }

        public string GetInterconnectGraphForDotty()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("digraph g {");
            foreach (var pi in _pipes)
            {
                sb.AppendFormat("  {0} -> {1} [label={2}]",
                    ((IDescriptive)_pipeInSignals[pi.source]).Descriptor.Name,
                    ((IDescriptive)_pipeInSignals[pi.sink]).Descriptor.Name,
                    pi.delay);
                sb.AppendLine();
            }
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}

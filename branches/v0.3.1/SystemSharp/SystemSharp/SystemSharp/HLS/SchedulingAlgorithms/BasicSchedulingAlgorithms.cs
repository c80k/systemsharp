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
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Collections;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.SchedulingAlgorithms
{
    abstract class NodeSet<T>
    {
        public abstract IEnumerable<T> Nodes { get; }

        public static NodeSet<T> From(T x)
        {
            return new SingleNode<T>(x);
        }

        public static NodeSet<T> Merge(NodeSet<T> s1, NodeSet<T> s2)
        {
            MultiNode<T> ms1 = s1 as MultiNode<T>;
            MultiNode<T> ms2 = s2 as MultiNode<T>;
            if (ms1 != null)
            {
                foreach (T x in s2.Nodes)
                    ms1.Add(x);
                return ms1;
            }
            if (ms2 != null)
            {
                foreach (T x in s1.Nodes)
                    ms2.Add(x);
                return ms2;
            }
            return new MultiNode<T>(
                s1.Nodes.Union(s2.Nodes).Distinct());
        }
    }

    class SingleNode<T>: NodeSet<T>
    {
        private T _node;

        public SingleNode(T node)
        {
            _node = node;
        }

        private IEnumerable<T> EnumNode()
        {
            yield return _node;
        }

        public override IEnumerable<T> Nodes
        {
            get { return EnumNode(); }
        }
    }

    class MultiNode<T> : NodeSet<T>
    {
        private HashSet<T> _nodes;

        public MultiNode()
        {
            _nodes = new HashSet<T>();
        }

        public MultiNode(IEnumerable<T> stock)
        {
            _nodes = new HashSet<T>(stock);
        }

        public void Add(T node)
        {
            _nodes.Add(node);
        }

        public override IEnumerable<T> Nodes
        {
            get { return _nodes; }
        }
    }

    /// <summary>
    /// The scheduling algorithm failed because there is no feasible schedule under the given constraints.
    /// </summary>
    public class NotSchedulableException : Exception
    {
    }

    /// <summary>
    /// This trivial scheduling algorithm puts all operations in serial order, without considering any parallelism. It is 
    /// intended as a "null hypothesis" for performance evaluations and should not be used in practice.
    /// </summary>
    public class SequentialScheduler: 
        IBasicBlockSchedulingAlgorithm
    {
        private SequentialScheduler()
        {
        }

        public long Schedule<T>(ISchedulingAdapter<T> a, IList<T> nodes, long startTime, IList<T> result)
        {
            long curTime = startTime;
            foreach (T node in nodes)
            {
                a.CStep[node] = curTime;
                result.Add(node);
                curTime += a.Latency[node];
            }
            return curTime;
        }

        public List<long> Schedule<T>(ISchedulingAdapter<T> a, IEnumerable<IList<T>> blocks, IList<T> result)
        {
            long time = 0;
            var times = new List<long>();
            foreach (IList<T> block in blocks)
            {
                times.Add(time);
                time = Schedule(a, block, time, result);
            }
            times.Add(time);
            return times;
        }

        public void Schedule<T>(IEnumerable<T> tasks, ISchedulingAdapter<T> scha, SchedulingConstraints constraints)
        {
            long curTime = constraints.StartTime;
            foreach (T node in tasks)
            {
                scha.CStep[node] = curTime;
                curTime += scha.Latency[node];
            }
            constraints.EndTime = curTime;
        }

        /// <summary>
        /// Returns the one and only instance of the sequential scheduling algorithm.
        /// </summary>
        public static readonly SequentialScheduler Instance = new SequentialScheduler();
    }

    /// <summary>
    /// Classic as-soon-as-possible (ASAP) scheduling algorithm.
    /// </summary>
    public class ASAPScheduler: 
        IBasicBlockSchedulingAlgorithm
    {
        private ASAPScheduler(bool constrainedResources)
        {
            ConstrainedResources = constrainedResources;
        }

        /// <summary>
        /// Whether the scheduling is performed under resource constraints, i.e. assigning an instruction to
        /// a particular c-step might fail because of limited parallelism.
        /// </summary>
        public bool ConstrainedResources { get; private set; }

        internal static void CheckInput<T>(ISchedulingAdapter<T> a, IList<T> instructions)
        {
            Contract.Requires<ArgumentNullException>(a != null);
            Contract.Requires<ArgumentNullException>(instructions != null);

            if (instructions.Any())
            {
                T first = instructions.First();
                for (int i = 0; i < instructions.Count; i++)
                {
                    T instr = instructions[i];
                    Debug.Assert(a.Preds[instr].All(pred => instructions.Contains(pred.Task)));
                    Debug.Assert(a.Succs[instr].All(succ => instructions.Contains(succ.Task)));
                }
            }
        }

        public long Schedule<T>(ISchedulingAdapter<T> a, IList<T> nodes, IList<T> startNodes,
            SchedulingConstraints constraints)
        {
            CheckInput(a, nodes);

            long startTime = constraints.StartTime;

            foreach (T x in nodes)
            {
                a.CStep[x] = long.MinValue;
            }

            var pq = new PriorityQueue<NodeSet<T>>()
            {
                Resolve = NodeSet<T>.Merge
            };
            foreach (T x in startNodes)
                pq.Enqueue(startTime, NodeSet<T>.From(x));
            long endTime = startTime + 1;
            while (!pq.IsEmpty)
            {
                var cur = pq.Dequeue();
                long curTime = cur.Key;
                var curSet = cur.Value;
                foreach (T x in curSet.Nodes)
                {
                    bool ready = true;
                    long reqTime = curTime;
                    foreach (var dw in a.Preds[x])
                    {
                        T w = dw.Task;
                        if (a.CStep[w] == long.MinValue)
                        {
                            // at least one predecessor is not yet scheduled 
                            // -> we cannot tell whether it is ok to schedule current task.
                            ready = false;
                            reqTime = long.MinValue;
                            break;
                        }
                        else if (a.CStep[w] + dw.MinDelay > curTime)
                        {
                            // at least one predecessor did not yet complete.
                            ready = false;
                            if (reqTime > long.MinValue)
                                reqTime = Math.Max(reqTime, a.CStep[w] + dw.MinDelay);
                        }
                    }
                    if (ready)
                    {
                        // Check for deadline violations in second pass
                        foreach (var dw in a.Preds[x])
                        {
                            T w = dw.Task;
                            if (a.CStep[w] + dw.MaxDelay < curTime)
                            {
                                // deadline exceeded
                                throw new NotSchedulableException();
                            }
                        }

                        if (a.CStep[x] == long.MinValue)
                        {
                            long preHint, postHint;
                            if (!ConstrainedResources || a.TryPin(x, curTime, out preHint, out postHint))
                            {
                                a.CStep[x] = curTime;
                                long lat = a.Latency[x];
                                long nextTime = curTime + lat;
                                if (lat > 0)
                                    endTime = Math.Max(endTime, nextTime);
                                else
                                    endTime = Math.Max(endTime, nextTime + 1);

                                // enqueue successor tasks
                                foreach (var dy in a.Succs[x])
                                {
                                    T y = dy.Task;
                                    pq.Enqueue(curTime + dy.MinDelay, NodeSet<T>.From(y));
                                }
                            }
                            else
                            {
                                pq.Enqueue(postHint, NodeSet<T>.From(x));
                            }
                        }
                    }
                    else if (reqTime > long.MinValue)
                    {
                        pq.Enqueue(reqTime, NodeSet<T>.From(x));
                    }
                }
            }
            foreach (T x in nodes)
            {
                if (a.CStep[x] == long.MinValue)
                    throw new NotSchedulableException();
            }
            return endTime;
        }

        public List<long> Schedule<T>(ISchedulingAdapter<T> a, IEnumerable<IList<T>> blocks)
        {
            long time = 0;
            var times = new List<long>();
            var constraints = new SchedulingConstraints();
            foreach (IList<T> block in blocks)
            {
                times.Add(time);
                constraints.StartTime = time;
                time = Schedule(a, block, block, constraints);
            }
            times.Add(time);
            return times;
        }

        public void Schedule<T>(IEnumerable<T> tasks, ISchedulingAdapter<T> scha, SchedulingConstraints constraints)
        {
            var nodes = tasks.ToList();
            constraints.EndTime = Schedule(scha, nodes, nodes, constraints);
        }

        /// <summary>
        /// Returns the one and only instance for unconstrained case, i.e. the algorithm is always free to assign any
        /// instruction to any c-step, regardless of the arising parallelism.
        /// </summary>
        public static readonly ASAPScheduler Instance = new ASAPScheduler(true);

        /// <summary>
        /// Returns the one and only instance for resource-constrained case, i.e. assigning a particular instruction to
        /// a particular c-step might fail because of limited parallelism.
        /// </summary>
        public static readonly ASAPScheduler InstanceUnlimitedResources = new ASAPScheduler(false);
    }

    /// <summary>
    /// Classic as-late-as-possible (ASAP) scheduling algorithm.
    /// </summary>
    public class ALAPScheduler : 
        IBasicBlockSchedulingAlgorithm,
        ICFGSchedulingAlgorithm
    {
        private ALAPScheduler(bool constrainedResources)
        {
            ConstrainedResources = constrainedResources;
        }

        protected virtual IEnumerable<T> PriorizeNodes<T>(IEnumerable<T> nodes)
        {
            return nodes;
        }

        /// <summary>
        /// Whether the scheduling is performed under resource constraints, i.e. assigning an instruction to
        /// a particular c-step might fail because of limited parallelism.
        /// </summary>
        public bool ConstrainedResources { get; private set; }

        public long Schedule<T>(ISchedulingAdapter<T> a, IList<T> nodes, IList<T> end, SchedulingConstraints constraints)
        {
            ASAPScheduler.CheckInput(a, nodes);

            long endTime = constraints.EndTime;

            foreach (T x in nodes)
            {
                a.CStep[x] = long.MaxValue;
            }

            var pq = new PriorityQueue<NodeSet<T>>()
            {
                Resolve = NodeSet<T>.Merge
            };
            foreach (T x in end)
                pq.Enqueue(-endTime, NodeSet<T>.From(x));
            long startTime = endTime - 1;
            while (!pq.IsEmpty)
            {
                var cur = pq.Dequeue();
                long curTime = -cur.Key;
                long reqTime = long.MaxValue;
                var curSet = cur.Value;
                var order = PriorizeNodes(curSet.Nodes);
                foreach (T x in order)
                {
                    bool ready = true;
                    foreach (var dy in a.Succs[x])
                    {
                        T y = dy.Task;
                        if (a.CStep[y] == long.MaxValue)
                        {
                            // at least one successor is not yet scheduled
                            // -> task not ready
                            ready = false;
                            reqTime = long.MaxValue;
                            break;
                        }
                        else if (a.CStep[y] < curTime - a.Latency[x] + dy.MinDelay)
                        {
                            // at least one successor starts before current task could
                            // complete -> task not ready
                            ready = false;
                            if (reqTime < long.MaxValue)
                                reqTime = Math.Min(reqTime, a.CStep[y] + a.Latency[x] - dy.MinDelay);
                        }
                    }
                    if (ready)
                    {
                        // Check for deadline violations in second pass
                        foreach (var dy in a.Succs[x])
                        {
                            T y = dy.Task;
                            if (a.CStep[y] > curTime - a.Latency[x] + dy.MaxDelay)
                            {
                                // deadline exceeded
                                throw new NotSchedulableException();
                            }
                        }

                        long lat = a.Latency[x];
                        long execTime = curTime - lat;
                        // If some operation is combinatorial (latency == 0) and is scheduled as
                        // last instruction, it must be moved one step back to fit inside the schedule's
                        // time frame.
                        if (execTime == endTime)
                            --execTime;
                        long preHint, postHint;
                        if (!ConstrainedResources || a.TryPin(x, execTime, out preHint, out postHint))
                        {
                            a.CStep[x] = execTime;
                            long nextTime = execTime;
                            startTime = Math.Min(startTime, execTime);
                            // requeue predecessors
                            foreach (var dw in a.Preds[x])
                            {
                                T w = dw.Task;
                                pq.Enqueue(-(execTime - dw.MinDelay + a.Latency[w]), NodeSet<T>.From(w));
                            }
                        }
                        else
                        {
                            if (preHint < 0)
                                throw new NotSchedulableException();

                            pq.Enqueue(-(preHint+lat), NodeSet<T>.From(x));
                        }
                    }
                    else if (reqTime < long.MaxValue)
                    {
                        pq.Enqueue(-reqTime, NodeSet<T>.From(x));
                    }
                }
            }
            foreach (T x in nodes)
            {
                if (a.CStep[x] == long.MaxValue)
                    throw new NotSchedulableException();
            }
            return startTime;
        }

        public List<long> Schedule<T>(ISchedulingAdapter<T> a, IEnumerable<IList<T>> blocks, long endTime)
        {
            long time = endTime;
            var times = new List<long>();
            var constraints = new SchedulingConstraints();
            constraints.EndTime = endTime;
            foreach (IList<T> block in blocks.Reverse())
            {
                times.Add(time);
                constraints.EndTime = time;
                long preTime = Schedule(a, block, block, constraints);
                time = preTime;
            }
            times.Add(time);
            return times;
        }

        public void Schedule<T>(IEnumerable<T> tasks, ISchedulingAdapter<T> scha, SchedulingConstraints constraints)
        {
            var nodes = tasks.ToList();
            constraints.StartTime = Schedule(scha, nodes, nodes, constraints);
        }

        /// <summary>
        /// Returns the one and only instance for unconstrained case, i.e. the algorithm is always free to assign any
        /// instruction to any c-step, regardless of the arising parallelism.
        /// </summary>
        public static readonly ALAPScheduler Instance = new ALAPScheduler(true);

        /// <summary>
        /// Returns the one and only instance for resource-constrained case, i.e. assigning a particular instruction to
        /// a particular c-step might fail because of limited parallelism.
        /// </summary>
        public static readonly ALAPScheduler InstanceUnlimitedResources = new ALAPScheduler(false);

        public virtual void Schedule<T>(ControlFlowGraph<T> cfg, SchedulingConstraints constraints, ISchedulingAdapter<T> scha) where T : Analysis.IInstruction
        {
            var endTimes = new Queue<long>();
            long cur = long.MaxValue - 1;
            foreach (var bb in cfg.BasicBlocks)
            {
                if (bb.IsExitBlock)
                    break;

                constraints.EndTime = cur;
                Schedule(bb.Range, scha, constraints);
                endTimes.Enqueue(constraints.EndTime - constraints.StartTime);
            }
            scha.ClearSchedule();
            constraints.EndTime = 0;
            foreach (var bb in cfg.BasicBlocks)
            {
                if (bb.IsExitBlock)
                    break;

                constraints.EndTime += endTimes.Dequeue();
                Schedule(bb.Range, scha, constraints);
            }
        }
    }
}

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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.SchedulingAlgorithms
{
    public interface ISlimMuxAdapter<Tn, Tp, Tf>
    {
        IPropMap<Tf, long> DepartureTime { get; }
        IPropMap<Tf, long> ArrivalTime { get; }
        IPropMap<Tf, Tn> Departure { get; }
        IPropMap<Tf, Tn> Destination { get; }

        IPropMap<Tp, Tn> Source { get; }
        IPropMap<Tp, Tn> Sink { get; }
        IPropMap<Tp, long> Delay { get; }

        IPropMap<Tn, bool> IsEndpoint { get; }
        IPropMap<Tn, IEnumerable<Tp>> Succs { get; }
        IPropMap<Tn, IEnumerable<Tp>> Preds { get; }
        Tp AddPipe(Tn source, Tn sink, long delay);
        void SplitPipe(Tp pipe, long delay1, long delay2, out Tn mid, out Tp left, out Tp right);
        void BindPipe(Tp pipe, long time, Tn emitter, long emitTime);
        bool IsPipeBound(Tp pipe, long time, out Tn emitter, out long emitTime);
    }

    public class SlimMux<Tn, Tp, Tf>
    {
        private abstract class Hop
        {
            public abstract Tn Realize(ISlimMuxAdapter<Tn, Tp, Tf> a);
            public abstract Tn Node { get; }
        }

        private abstract class Pipe
        {
            public abstract Tp Realize(ISlimMuxAdapter<Tn, Tp, Tf> a);
            public abstract Tp Inst { get; }
            public abstract long GetDelay(ISlimMuxAdapter<Tn, Tp, Tf> a);
            public abstract Hop GetSource(ISlimMuxAdapter<Tn, Tp, Tf> a);
            public abstract Hop GetSink(ISlimMuxAdapter<Tn, Tp, Tf> a);

            public string ToString(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return GetSource(a) + "-{" + GetDelay(a) + "}-" + GetSink(a);
            }
        }

        private class RealHop: Hop
        {
            private Tn _node;
            public override Tn Node
            {
                get { return _node; }
            }

            public RealHop(Tn node)
            {
                _node = node;
            }

            public override Tn Realize(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return Node;
            }

            public static explicit operator RealHop(Tn node)
            {
                return new RealHop(node);
            }

            public override string ToString()
            {
                return _node.ToString();
            }
        }

        private class RealPipe : Pipe
        {
            private Tp _pipe;
            public override Tp Inst
            {
                get { return _pipe; }
            }

            public RealPipe(Tp pipe)
            {
                _pipe = pipe;
            }

            public override Tp Realize(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return Inst;
            }

            public override long GetDelay(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return a.Delay[_pipe];
            }

            public override Hop GetSource(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return (RealHop)a.Source[_pipe];
            }

            public override Hop GetSink(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return (RealHop)a.Sink[_pipe];
            }

            public static explicit operator RealPipe(Tp pipe)
            {
                return new RealPipe(pipe);
            }

            public override string ToString()
            {
                return _pipe.ToString();
            }
        }

        private class InsertionHop: Hop
        {
            private class LPipe : Pipe
            {
                public Hop Source { get; private set; }
                public Hop Sink { get; private set; }
                public long Delay { get; private set; }

                private Tp _inst;
                private bool _haveInst;

                public LPipe(Hop left, Hop right, long delay)
                {
                    Source = left;
                    Sink = right;
                    Delay = delay;
                }

                public override long GetDelay(ISlimMuxAdapter<Tn, Tp, Tf> a)
                {
                    return Delay;
                }

                public override Hop GetSink(ISlimMuxAdapter<Tn, Tp, Tf> a)
                {
                    return Sink;
                }

                public override Hop GetSource(ISlimMuxAdapter<Tn, Tp, Tf> a)
                {
                    return Source;
                }

                public override Tp Inst
                {
                    get 
                    {
                        Debug.Assert(_haveInst);
                        return _inst; 
                    }
                }

                public override Tp Realize(ISlimMuxAdapter<Tn, Tp, Tf> a)
                {
                    Sink.Realize(a);
                    return _inst;
                }

                public void SetInst(Tp inst)
                {
                    _inst = inst;
                    _haveInst = true;
                }
            }

            public Pipe OrgPipe { get; private set; }
            public Tn RealNode { get; private set; }
            public bool IsRealized { get; private set; }
            public long PredDelay { get; private set; }
            public long SuccDelay { get; private set; }

            private LPipe _leftPipe;
            public Pipe LeftPipe
            {
                get { return _leftPipe; }
            }

            private LPipe _rightPipe;
            public Pipe RightPipe 
            {
                get { return _rightPipe; }
            }

            public override Tn Node
            {
                get 
                {
                    Debug.Assert(IsRealized);
                    return RealNode; 
                }
            }

            public InsertionHop(ISlimMuxAdapter<Tn, Tp, Tf> a, Pipe orgPipe, long predDelay, long succDelay)
            {
                Debug.Assert(orgPipe.GetDelay(a) == predDelay + succDelay);

                OrgPipe = orgPipe;
                PredDelay = predDelay;
                SuccDelay = succDelay;
                _leftPipe = new LPipe(orgPipe.GetSource(a), this, predDelay);
                _rightPipe = new LPipe(this, orgPipe.GetSink(a), succDelay);
            }

            public override Tn Realize(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                if (!IsRealized)
                {
                    Tn mid;
                    Tp orgPipe = OrgPipe.Realize(a);
                    Tp left, right;
                    a.SplitPipe(orgPipe, PredDelay, SuccDelay, out mid, out left, out right);
                    RealNode = mid;
                    _leftPipe.SetInst(left);
                    _rightPipe.SetInst(right);
                    IsRealized = true;
                }
                return RealNode;
            }

            public override string ToString()
            {
                return IsRealized ? "!" + RealNode.ToString() : 
                    "Hop<" + _leftPipe.Source + "-" + _rightPipe.Sink + "@" + PredDelay + ">";
            }
        }

        private class InsertionPipe : Pipe
        {
            public Hop Source { get; private set; }
            public Hop Sink { get; private set; }
            public long Delay { get; private set; }
            public Tp RealPipe { get; private set; }
            public bool IsRealized { get; private set; }

            public InsertionPipe(Hop source, Hop sink, long delay)
            {
                Debug.Assert(delay >= 0);

                Source = source;
                Sink = sink;
                Delay = delay;
            }

            public override Tp Inst
            {
                get 
                {
                    Debug.Assert(IsRealized);
                    return RealPipe; 
                }
            }

            public override Tp Realize(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                if (!IsRealized)
                {
                    RealPipe = a.AddPipe(Source.Realize(a), Sink.Realize(a), Delay);
                    IsRealized = true;
                }
                return RealPipe;
            }

            public override long GetDelay(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return Delay;
            }

            public override Hop GetSource(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return Source;
            }

            public override Hop GetSink(ISlimMuxAdapter<Tn, Tp, Tf> a)
            {
                return Sink;
            }

            public override string ToString()
            {
                return IsRealized ? "!" + RealPipe :
                    "Pipe<" + Source + "-" + Sink + "@" + Delay + ">";
            }
        }

        private class Path
        {
            private ISlimMuxAdapter<Tn, Tp, Tf> _a;

            public Pipe[] Segments { get; private set; }

            public Path(ISlimMuxAdapter<Tn, Tp, Tf> a, Pipe[] segments)
            {
                Debug.Assert(segments.Skip(1).All(s => s.GetDelay(a) >= 1));

                _a = a;
                Segments = segments;
            }

            public long TotalDelay
            {
                get { return Segments.Select(s => s.GetDelay(_a)).Sum(); }
            }

            public void Realize()
            {
                foreach (Pipe pipe in Segments)
                {
                    pipe.Realize(_a);
                }
            }

            public void Bind(long depTime)
            {
                long curTime = depTime;
                Tn emitter = _a.Source[Segments[0].Inst];
                for (int i = 0; i < Segments.Length; i++)
                {
                    Tp pipe = Segments[i].Realize(_a);
                    Tn tmpEmitter;
                    long tmpDepTime;
                    if (_a.IsPipeBound(pipe, curTime, out tmpEmitter, out tmpDepTime))
                    {
                        Debug.Assert(object.Equals(emitter, tmpEmitter));
                        Debug.Assert(depTime == tmpDepTime);
                    }
                    else
                    {
                        _a.BindPipe(pipe, curTime, emitter, depTime);
                    }
                    curTime += _a.Delay[pipe];
                }
            }

            public void Check()
            {
                for (int i = 1; i < Segments.Length; i++)
                {
                    Debug.Assert(object.Equals(_a.Sink[Segments[i - 1].Inst], _a.Source[Segments[i].Inst]));
                }
            }

            public override string ToString()
            {
                return string.Join(";", Segments.Select(s => s.ToString(_a)));
            }
        }

        public int PreferredMuxFanIn { get; set; }

        private ISlimMuxAdapter<Tn, Tp, Tf> _a;
        private IEnumerable<Tf> _flows;

        private IEnumerable<Path> FindPaths(long depTime, Tn source, Tn target)
        {
            var s = new Stack<Tuple<Tp, long>>();
            var parent = new Dictionary<Tp, Tp>();

            foreach (Tp pipe in _a.Succs[source])
            {
                long delay = _a.Delay[pipe];
                if (delay == 0 && !object.Equals(_a.Sink[pipe], target))
                    continue;

                Tn emitter;
                long emitTime;
                if (_a.IsPipeBound(pipe, depTime, out emitter, out emitTime) &&
                    !(emitTime == depTime && object.Equals(emitter, source)))
                    continue;

                s.Push(Tuple.Create(pipe, depTime + delay));
            }
            while (s.Any())
            {
                var tup = s.Pop();
                Tp curPipe = tup.Item1;
                long curTime = tup.Item2;
                Tn cur = _a.Sink[curPipe];
                if (object.Equals(cur, target))
                {
                    var pipes = new Stack<Pipe>();
                    Tp curp = curPipe;
                    do
                    {
                        pipes.Push((RealPipe)curp);
                        if (object.Equals(_a.Source[curp], source))
                            break;
                        else
                            curp = parent[curp];
                    } while (true);
                    yield return new Path(_a, pipes.ToArray());
                }
                else if (!_a.IsEndpoint[cur])
                {
                    foreach (Tp pipe in _a.Succs[cur])
                    {
                        Tn succ = _a.Sink[pipe];
                        long succdly = _a.Delay[pipe];
                        if (succdly == 0 && !object.Equals(succ, target))
                            continue;

                        Tn emitter;
                        long emitTime;
                        if (_a.IsPipeBound(pipe, curTime, out emitter, out emitTime) &&
                            !(emitTime == depTime && object.Equals(emitter, source)))
                            continue;

                        parent[pipe] = curPipe;
                        s.Push(Tuple.Create(pipe, curTime + succdly));
                    }
                }
            }
        }

        private IEnumerable<Path> FindIncomingPaths(long depTime, Tn source, long arrTime, Tn target)
        {
            var s = new Stack<Tuple<Tp, long>>();
            var parent = new Dictionary<Tp, Tp>();

            foreach (Tp pipe in _a.Preds[target])
            {
                long delay = _a.Delay[pipe];
                if (delay == 0)
                    continue;
                long time = arrTime - delay;

                Tn emitter;
                long emitTime;
                if (_a.IsPipeBound(pipe, time, out emitter, out emitTime) &&
                    (depTime != emitTime || !source.Equals(emitter)))
                    continue;

                s.Push(Tuple.Create(pipe, time));
            }
            while (s.Any())
            {
                var tup = s.Pop();
                Tp curPipe = tup.Item1;
                long curTime = tup.Item2;
                Tn cur = _a.Source[curPipe];
                if (_a.Preds[cur].Any())
                {
                    foreach (Tp pipe in _a.Preds[cur])
                    {
                        Tn pred = _a.Source[pipe];
                        long preddly = _a.Delay[pipe];

                        Tn emitter;
                        long emitTime;
                        if (_a.IsPipeBound(pipe, curTime, out emitter, out emitTime) &&
                            (depTime != emitTime || !source.Equals(emitter))) 
                            continue;

                        parent[pipe] = curPipe;
                        s.Push(Tuple.Create(pipe, curTime - preddly));
                    }
                }
                else
                {
                    var pipes = new List<Pipe>();
                    Tp curp = curPipe;
                    do
                    {
                        pipes.Add((RealPipe)curp);
                        if (object.Equals(_a.Sink[curp], target))
                            break;
                        else
                            curp = parent[curp];
                    } while (true);
                    yield return new Path(_a, pipes.ToArray());
                }
            }
        }

        private IEnumerable<Path> FindOutgoingPaths(long depTime, Tn source)
        {
            var s = new Stack<Tuple<Tp, long>>();
            var parent = new Dictionary<Tp, Tp>();

            foreach (Tp pipe in _a.Succs[source])
            {
                long delay = _a.Delay[pipe];
                if (delay == 0)
                    continue;

                if (_a.IsEndpoint[_a.Sink[pipe]])
                    continue;

                long time = depTime - delay;

                Tn emitter;
                long emitTime;
                if (_a.IsPipeBound(pipe, time, out emitter, out emitTime) &&
                    (depTime != emitTime || !source.Equals(emitter)))
                    continue;

                s.Push(Tuple.Create(pipe, time));
            }
            while (s.Any())
            {
                var tup = s.Pop();
                Tp curPipe = tup.Item1;
                long curTime = tup.Item2;
                Tn cur = _a.Sink[curPipe];
                if (_a.Succs[cur].Any())
                {
                    foreach (Tp pipe in _a.Succs[cur])
                    {
                        Tn succ = _a.Sink[pipe];

                        if (_a.IsEndpoint[succ])
                            continue;

                        long succdly = _a.Delay[pipe];

                        Tn emitter;
                        long emitTime;
                        if (_a.IsPipeBound(pipe, curTime, out emitter, out emitTime) &&
                            (depTime != emitTime || !source.Equals(emitter)))
                            continue;

                        parent[pipe] = curPipe;
                        s.Push(Tuple.Create(pipe, curTime + succdly));
                    }
                }
                else
                {
                    var pipes = new LinkedList<Pipe>();
                    Tp curp = curPipe;
                    do
                    {
                        pipes.AddFirst((RealPipe)curp);
                        if (object.Equals(_a.Source[curp], source))
                            break;
                        else
                            curp = parent[curp];
                    } while (true);
                    yield return new Path(_a, pipes.ToArray());
                }
            }
        }

        private SlimMux(ISlimMuxAdapter<Tn, Tp, Tf> a, IEnumerable<Tf> flows)
        {
            _a = a;
            _flows = flows;
            PreferredMuxFanIn = 4;
        }

        private long ComputeJoinCost(IEnumerable<Tp> fanin)
        {
            Contract.Requires<ArgumentNullException>(fanin != null);

            int count = fanin.Count();
            if (count == 1)
                return 1;
            else if (count < PreferredMuxFanIn)
                return 0;
            else
                return count;
        }

        private long GetSplitJoinCost()
        {
            return 1;
        }

        private void CreateAndBindPath(Tn source, long depTime, Tn dest, long arrTime)
        {
            long delay = arrTime - depTime;
            Tp pipe = _a.AddPipe(source, dest, delay);
            _a.BindPipe(pipe, depTime, source, depTime);
        }

        private long CreatePath(Path path, long delay, out Path xpath)
        {
            xpath = new Path(_a, new Pipe[] 
            { 
                new InsertionPipe(
                    path.Segments.First().GetSource(_a), 
                    path.Segments.Last().GetSink(_a), 
                    delay) 
            });
            long cost = _a.Preds[path.Segments.Last().GetSink(_a).Node].Count() + 1;
            return cost;
        }

        private long ExtendPath(Path path, long delayInc, out Path xpath)
        {
            Debug.Assert(delayInc >= 1);

            if (path.TotalDelay <= 1)
            {
                Debug.Assert(path.Segments.Length == 1);
                Tp pipe = path.Segments[0].Inst;
                Pipe ipipe = new InsertionPipe((RealHop)_a.Source[pipe], (RealHop)_a.Sink[pipe], delayInc);
                xpath = new Path(_a, new Pipe[] { ipipe });
                long fanin = _a.Preds[_a.Sink[pipe]].Count();
                return fanin + 1;
            }
            Hop bestSpliceHop = null;
            Hop bestJoinHop = null;
            long bestCost = long.MaxValue;
            int spliceIdx = -1;
            int joinIdx = -1;
            var segments = new List<Pipe>();
            for (int i = 0; i < path.Segments.Length; i++)
            {
                Pipe seg = path.Segments[i];
                Tp pipe = seg.Inst;
                long delay = seg.GetDelay(_a);
                Debug.Assert(delay > 0);
                if (bestCost > 1)
                {
                    if (delay == 1)
                    {
                        long cost = ComputeJoinCost(_a.Preds[_a.Sink[pipe]]);
                        if (cost < bestCost)
                        {
                            bestSpliceHop = (RealHop)_a.Source[pipe];
                            bestJoinHop = (RealHop)_a.Sink[pipe];
                            bestCost = cost;
                            spliceIdx = i;
                            joinIdx = i + 1;
                        }
                        segments.Add(seg);
                    }
                    if (bestCost > GetSplitJoinCost())
                    {
                        bestSpliceHop = (RealHop)_a.Source[pipe];
                        var ihop = new InsertionHop(_a, seg, 1, delay - 1);
                        bestJoinHop = ihop;
                        bestCost = 1;
                        spliceIdx = i;
                        joinIdx = i + 1;
                        segments.Add(ihop.LeftPipe);
                        segments.Add(ihop.RightPipe);
                    }
                }
                else
                {
                    segments.Add(seg);
                }
            }
            Pipe nose = new InsertionPipe(bestSpliceHop, bestJoinHop, delayInc + 1);
            var newSegments = new List<Pipe>();
            newSegments.AddRange(segments.Take(spliceIdx));
            newSegments.Add(nose);
            newSegments.AddRange(segments.Skip(joinIdx));
            xpath = new Path(_a, newSegments.ToArray());
            return bestCost + 1;
        }

        private struct SegLink: IComparable<SegLink>
        {
            public long time;
            public LinkedListNode<Pipe> pos;

            public int CompareTo(SegLink other)
            {
                if (time < other.time)
                    return -1;
                else if (time > other.time)
                    return 1;
                else
                    return 0;
            }
        }

        private Hop FindSpliceHop(SortedSet<SegLink> segset, SegLink max, long joinTime, long delayDec)
        {
            long spliceTime = joinTime - delayDec - 1;
            var query = new SegLink() { pos = null, time = spliceTime + 1 };
            var spliceSet = segset.GetViewBetween(query, max);
            Debug.Assert(spliceSet.Any());
            var spliceLink = spliceSet.First();
            Pipe spliceSeg = spliceLink.pos.Value;
            long segEndTime = spliceLink.time;
            long segDelay = spliceSeg.GetDelay(_a);
            long segStartTime = spliceLink.time - segDelay;
            Debug.Assert(segStartTime <= spliceTime);
            Debug.Assert(segEndTime > spliceTime);
            Hop spliceHop;
            if (segStartTime == spliceTime)
            {
                spliceHop = spliceSeg.GetSource(_a);
            }
            else
            {
                spliceHop = new InsertionHop(_a, spliceSeg, spliceTime - segStartTime, segEndTime - spliceTime);
            }
            return spliceHop;
        }

        private long ShortcutPath(Path path, long delayDec, out Path xpath)
        {
            var segset = new SortedSet<SegLink>();
            var seglist = new LinkedList<Pipe>();
            long time = 0;
            for (int i = 0; i < path.Segments.Length; i++)            
            {
                Pipe seg = path.Segments[i];
                time += seg.GetDelay(_a);
                var pos = seglist.AddLast(seg);
                segset.Add(new SegLink() { pos = pos, time = time });
            }
            SegLink max = new SegLink() { pos = null, time = time + 1 };
            SegLink minJoin = new SegLink() { pos = null, time = delayDec + 1 };
            var joinset = segset.GetViewBetween(minJoin, max);
            Debug.Assert(joinset.Any());
            Hop bestSpliceHop = null;
            Hop bestJoinHop = null;
            long bestCost = long.MaxValue;
            long bestJoinTime = -1;
            foreach (var seglink in joinset)
            {
                long joinTime = seglink.time;
                Pipe seg = seglink.pos.Value;
                Hop joinHop = seg.GetSink(_a);
                long joinCost = _a.Preds[joinHop.Node].Count();
                if (joinCost < bestCost)
                {
                    bestCost = joinCost;
                    bestJoinHop = joinHop;
                    bestJoinTime = joinTime;
                    bestSpliceHop = FindSpliceHop(segset, max, joinTime, delayDec);
                }
                long segDelay = seg.GetDelay(_a);
                --joinTime;
                if (segDelay > 1 && joinTime > delayDec && bestCost > 1)
                {
                    bestCost = 1;
                    var insHop = new InsertionHop(_a, seg, segDelay - 1, 1);
                    bestJoinHop = insHop;
                    bestJoinTime = joinTime;

                    var left = seglist.AddBefore(seglink.pos, insHop.LeftPipe);
                    var right = seglist.AddAfter(seglink.pos, insHop.RightPipe);
                    seglist.Remove(seglink.pos);
                    segset.Remove(seglink);
                    var leftLink = new SegLink() { pos = left, time = seglink.time - 1 };
                    segset.Add(leftLink);
                    var rightLink = new SegLink() { pos = right, time = seglink.time };
                    segset.Add(rightLink);
                    bestSpliceHop = FindSpliceHop(segset, max, joinTime, delayDec);
                    break;
                }
            }
            long bestSpliceTime = bestJoinTime - delayDec - 1;
            var newSegments = new List<Pipe>();
            var wormhole = new InsertionPipe(bestSpliceHop, bestJoinHop, 1);
            time = 0;
            var curpos = seglist.First;
            while (curpos != null)
            {
                Pipe seg = curpos.Value;
                long nextTime = time + seg.GetDelay(_a);
                if (nextTime <= bestSpliceTime || time >= bestJoinTime)
                {
                    newSegments.Add(seg);
                }
                else if (time == bestSpliceTime)
                {
                    newSegments.Add(wormhole);
                }
                else if (time < bestSpliceTime && nextTime > bestSpliceTime)
                {
                    var joinHop = (InsertionHop)bestSpliceHop;
                    newSegments.Add(joinHop.LeftPipe);
                    newSegments.Add(wormhole);
                }
                time = nextTime;
                curpos = curpos.Next;
            }
            xpath = new Path(_a, newSegments.ToArray());
            return bestCost + 1;
        }

        private long FindCheapestPathAdjustment(Path path, long depTime, long arrTime, out Path xpath)
        {
            long pathDelay = path.TotalDelay;
            long reqDelay = arrTime - depTime;
            if (pathDelay == reqDelay)
            {
                xpath = path;
                return 0;
            }
            else
            {
                // Problem here: If we shortcut/extend an existing path, we risk to collide with a reserved part...
                xpath = null;
                return long.MaxValue;
            }
            /*
            if (reqDelay <= 1 || pathDelay <= 1)
            {
                return CreatePath(path, reqDelay, out xpath);
            }
            if (pathDelay < reqDelay)
            {
                return ExtendPath(path, reqDelay - pathDelay, out xpath);
            }
            if (pathDelay > reqDelay)
            {
                return ShortcutPath(path, pathDelay - reqDelay, out xpath);
            }
            throw new InvalidOperationException("not reached");
             * */
        }

        private bool FindCheapestPathConnection(Path srcPath, Path dstPath, long depTime, long arrTime, out Path xpath, out long cost)
        {
            long reqDelay = arrTime - depTime;
            if (reqDelay < 2)
            {
                xpath = null;
                cost = long.MaxValue;
                return false;
            }

            long bestCost = long.MaxValue;
            xpath = null;
            long srcDelay = srcPath.TotalDelay;
            for (int i = srcPath.Segments.Length - 1; i >= 0; i--)
            {
                Pipe srcSeg = srcPath.Segments[i];
                if (reqDelay > srcDelay)
                {
                    long dstDelay = dstPath.TotalDelay;
                    long pathDelay = srcDelay + dstDelay;
                    for (int j = 0; j < dstPath.Segments.Length; j++)
                    {
                        Pipe dstSeg = dstPath.Segments[j];

                        if (reqDelay > pathDelay)
                        {
                            var node = dstSeg.GetSource(_a).Node;
                            if (!_a.IsEndpoint[node])
                            {
                                long joinCost = ComputeJoinCost(_a.Preds[dstSeg.GetSource(_a).Node]);
                                if (joinCost < bestCost)
                                {
                                    var link = new InsertionPipe(srcSeg.GetSink(_a), dstSeg.GetSource(_a), reqDelay - pathDelay);
                                    xpath = new Path(_a, new Pipe[] { link });
                                    bestCost = joinCost;
                                }
                            }

                            long segDelay = dstSeg.GetDelay(_a);
                            if (segDelay >= 2 && bestCost > GetSplitJoinCost())
                            {
                                var join = new InsertionHop(_a, dstSeg, 1, segDelay - 1);
                                var link = new InsertionPipe(srcSeg.GetSink(_a), join, reqDelay - pathDelay + 1);
                                xpath = new Path(_a, new Pipe[] { link });
                                bestCost = GetSplitJoinCost();
                            }

                            if (bestCost == 0)
                                break;
                        }

                        pathDelay -= dstSeg.GetDelay(_a);
                    }
                    if (bestCost == 0)
                        break;
                }
                srcDelay -= srcSeg.GetDelay(_a);
            }

            // To be implemented
            cost = bestCost + 1;
            return bestCost != long.MaxValue;
        }

        private bool FindCheapestPathConnection(Tn source, Path path, long depTime, long arrTime, out Path xpath, out long cost)
        {
            long reqDelay = arrTime - depTime;
            if (path.TotalDelay < 2 || reqDelay < 2)
            {
                xpath = null;
                cost = long.MaxValue;
                return false;
            }

            long maxCommon = Math.Min(reqDelay - 1, path.TotalDelay - 1);
            long minJoinPoint = path.TotalDelay - maxCommon;
            long time = 0;
            long bestJoinPoint = -1;
            long bestJoinCost = long.MaxValue;
            Hop bestJoinHop = null;
            for (int i = 0; i < path.Segments.Length; i++)
            {
                Pipe seg = path.Segments[i];
                long segDelay = seg.GetDelay(_a);
                long nextTime = time + segDelay;
                if (nextTime > minJoinPoint)
                {
                    long joinPoint = Math.Max(time, minJoinPoint);
                    if (joinPoint == time)
                    {
                        long joinCost = ComputeJoinCost(_a.Preds[seg.GetSource(_a).Node]);
                        if (joinCost < bestJoinCost)
                        {
                            bestJoinCost = joinCost;
                            bestJoinPoint = joinPoint;
                            bestJoinHop = seg.GetSource(_a);
                        }
                        ++joinPoint;
                    }
                    if (joinPoint < nextTime && bestJoinCost > GetSplitJoinCost())
                    {
                        bestJoinPoint = joinPoint;
                        bestJoinCost = GetSplitJoinCost();
                        bestJoinHop = new InsertionHop(_a, seg, joinPoint - time, nextTime - joinPoint);
                    }
                    if (bestJoinCost == 0)
                        break;
                }
                time = nextTime;
            }
            var newSegments = new List<Pipe>();
            newSegments.Add(new InsertionPipe((RealHop)source, bestJoinHop, reqDelay - path.TotalDelay + bestJoinPoint));
            time = 0;
            for (int i = 0; i < path.Segments.Length; i++)
            {
                Pipe seg = path.Segments[i];
                long segDelay = seg.GetDelay(_a);
                long nextTime = time + segDelay;
                if (nextTime > bestJoinPoint)
                {
                    if (time >= bestJoinPoint)
                    {
                        newSegments.Add(seg);
                    }
                    else
                    {
                        newSegments.Add(((InsertionHop)bestJoinHop).RightPipe);
                    }
                }
                time = nextTime;
            }
            xpath = new Path(_a, newSegments.ToArray());
            Debug.Assert(xpath.TotalDelay == reqDelay);
            cost = bestJoinCost + 1;
            return true;
        }

        private void Run()
        {
            var order = _flows.OrderByDescending(f => _a.ArrivalTime[f] - _a.DepartureTime[f]);
            foreach (Tf flow in order)
            {
                long depTime = _a.DepartureTime[flow];
                long arrTime = _a.ArrivalTime[flow];
                Tn dep = _a.Departure[flow];
                Tn dst = _a.Destination[flow];
                var paths = FindPaths(depTime, dep, dst);
                long bestCost = long.MaxValue;
                Path bestPath = null;
                bool any = false;

                // Debug only
                Path inPath1, inPath2;
                string solutionType;

                foreach (Path path in paths)
                {
                    Path xpath;
                    long cost = FindCheapestPathAdjustment(path, depTime, arrTime, out xpath);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestPath = xpath;
                        any = true;

                        // Debug
                        inPath1 = path;
                        solutionType = "reusing existing path";
                    }
                }
                if (bestCost > 0)
                {
                    var outPaths = FindOutgoingPaths(depTime, dep);
                    var inPaths = FindIncomingPaths(depTime, dep, arrTime, dst);
                    if (outPaths.Any())
                    {
                        foreach (Path outPath in outPaths)
                        {
                            foreach (Path inPath in inPaths)
                            {
                                Path xpath;
                                long cost;
                                bool found = FindCheapestPathConnection(outPath, inPath, depTime, arrTime, out xpath, out cost);
                                if (found && cost < bestCost)
                                {
                                    bestPath = xpath;
                                    bestCost = cost;
                                    any = true;

                                    // Debug
                                    inPath1 = outPath;
                                    inPath2 = inPath;
                                    solutionType = "connecting two paths";

                                    if (cost == 0)
                                        break;
                                }
                            }
                            if (bestCost == 0)
                                break;
                        }
                    }
                    else
                    {
                        foreach (Path inPath in inPaths)
                        {
                            Debug.Assert(object.Equals(dst, _a.Sink[inPath.Segments.Last().Inst]));

                            Path xpath;
                            long cost;
                            bool found = FindCheapestPathConnection(dep, inPath, depTime, arrTime, out xpath, out cost);                            
                            if (found && cost < bestCost)
                            {
                                bestPath = xpath;
                                bestCost = cost;
                                any = true;
                                
                                // Debug
                                inPath1 = inPath;
                                solutionType = "joining incoming path";

                                if (cost == 0)
                                    break;
                            }
                        }
                    }
                }
                if (any)
                {
                    Debug.Assert(bestPath.TotalDelay == arrTime - depTime);
                    string preDesc = bestPath.ToString();
                    bestPath.Realize();
                    Debug.Assert(object.Equals(dst, _a.Sink[bestPath.Segments.Last().Inst]));
                    bestPath.Check();
                    bestPath.Bind(depTime);
                }
                else
                {
                    CreateAndBindPath(dep, depTime, dst, arrTime);
                }
            }
        }

        public static void ConstructInterconnect(ISlimMuxAdapter<Tn, Tp, Tf> a, IEnumerable<Tf> flows)
        {
            var slimmux = new SlimMux<Tn, Tp, Tf>(a, flows);
            slimmux.Run();
        }
    }
}

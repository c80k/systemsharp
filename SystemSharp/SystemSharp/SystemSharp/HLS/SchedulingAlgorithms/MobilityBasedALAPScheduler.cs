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
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.SchedulingAlgorithms
{
    // Currently deactivated, got no time to implement this correctly...
#if false
    public class MobilityBasedALAPScheduler:
        IFunctionSchedulingAlgorithm
    {
        private class MobilityALAP<T> : ALAPScheduler
        {
            private IPropMap<T, long> _asapMap;
            private IPropMap<T, long> _alapMap;

            public MobilityALAP(IPropMap<T, long> asapMap, IPropMap<T, long> alapMap)
                : base(true)
            {
                _asapMap = asapMap;
                _alapMap = alapMap;
            }

            private long GetMobility(T node)
            {
                return _alapMap[node] - _asapMap[node];
            }

            protected override IEnumerable<T2> PriorizeNodes<T2>(IEnumerable<T2> nodes)
            {
                return nodes.OrderBy(n => GetMobility((T)(object)n));
            }

            public override void Schedule<T>(ControlFlowGraph<T> cfg, ISchedulingConstraints<T> constraints, ISchedulingAdapter<T> scha)
            {
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

        private class ASLAPAdapter<T> : ISchedulingAdapter<T>
        {
            private ISchedulingAdapter<T> _scha;
            private IPropMap<T, long> _aslapIndex;

            public ASLAPAdapter(ISchedulingAdapter<T> scha, IPropMap<T, long> aslapIndex)
            {
                _scha = scha;
                _aslapIndex = aslapIndex;
            }

            public IPropMap<T, int> Index
            {
                get { return _scha.Index; }
            }

            public IPropMap<T, T[]> Preds
            {
                get { return _scha.Preds; }
            }

            public IPropMap<T, T[]> Succs
            {
                get { return _scha.Succs; }
            }

            public IPropMap<T, int[]> Operands
            {
                get { return _scha.Operands; }
            }

            public IPropMap<T, int[]> Results
            {
                get { return _scha.Results; }
            }

            public IPropMap<T, long> Latency
            {
                get { return _scha.Latency; }
            }

            public IPropMap<T, long> CStep
            {
                get { return _aslapIndex; }
            }

            public IPropMap<T, object> IClass
            {
                get { return _scha.IClass; }
            }

            public bool TryPin(T task, long cstep, out long preHint, out long postHint)
            {
                return _scha.TryPin(task, cstep, out preHint, out postHint);
            }

            public void ClearSchedule()
            {
                _scha.ClearSchedule();
            }
        }

        public void Schedule<T>(ControlFlowGraph<T> cfg, ISchedulingConstraints<T> constraints, ISchedulingAdapter<T> scha) where T : IInstruction
        {
            var asap = new long[cfg.Instructions.Count];
            var alap = new long[cfg.Instructions.Count];
            var asapMap = new ArrayBackedPropMap<T, long>(asap, i => i.Index);
            var alapMap = new ArrayBackedPropMap<T, long>(alap, i => i.Index);
            foreach (var bb in cfg.BasicBlocks)
            {
                if (bb.IsExitBlock)
                    break;

                var asapa = new ASLAPAdapter<T>(scha, asapMap);
                var alapa = new ASLAPAdapter<T>(scha, alapMap);
                ASAPScheduler.Instance.Schedule(bb.Range, asapa, constraints);
                asapa.ClearSchedule();
                long oldStartTime = constraints.StartTime;
                constraints.EndTime = constraints.StartTime + (long)Math.Ceiling((constraints.EndTime - constraints.StartTime) * constraints.SchedScale);
                ALAPScheduler.Instance.Schedule(bb.Range, alapa, constraints);
                alapa.ClearSchedule();
                constraints.StartTime = oldStartTime;
            }
            var sch = new MobilityALAP<T>(asapMap, alapMap);
            constraints.StartTime = 0;
            sch.Schedule(cfg, constraints, scha);
        }

        public static readonly MobilityBasedALAPScheduler Instance = new MobilityBasedALAPScheduler();
    }
#endif
}

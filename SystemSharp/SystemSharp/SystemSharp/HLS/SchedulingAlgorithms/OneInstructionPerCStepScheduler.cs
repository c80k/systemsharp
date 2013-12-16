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
    /// <summary>
    /// Trivial basic block scheduling algorithms which schedules all operations strictly sequentially.
    /// This algorithm is intended to be used as a "null hypothesis" for performance comparisons and should
    /// not be used in practice.
    /// </summary>
    public class OneInstructionPerCStepScheduler :
        IBasicBlockSchedulingAlgorithm
    {
        private OneInstructionPerCStepScheduler()
        {
        }

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

        public long Schedule<T>(ISchedulingAdapter<T> a, IList<T> nodes, IList<T> startNodes, long startTime)
        {
            CheckInput(a, nodes);

            foreach (T x in nodes)
            {
                a.CStep[x] = long.MinValue;
            }

            long curTime = startTime;
            foreach (T node in nodes)
            {
                var preds = a.Preds[node];
                long time = curTime;
                if (preds.Length > 0)
                    time = Math.Max(time, preds.Max(p => a.CStep[p.Task] + p.MinDelay));
                if (preds.Any(p => time - a.CStep[p.Task] > p.MaxDelay))
                    throw new NotSchedulableException();
                a.CStep[node] = time;
                curTime = time + 1;
            }

            return curTime;
        }

        public List<long> Schedule<T>(ISchedulingAdapter<T> a, IEnumerable<IList<T>> blocks)
        {
            long time = 0;
            var times = new List<long>();
            foreach (IList<T> block in blocks)
            {
                times.Add(time);
                time = Schedule(a, block, block, time);
            }
            times.Add(time);
            return times;
        }

        public void Schedule<T>(IEnumerable<T> tasks, ISchedulingAdapter<T> scha, SchedulingConstraints constraints)
        {
            var nodes = tasks.ToList();
            constraints.EndTime = Schedule(scha, nodes, nodes, constraints.StartTime);
        }

        /// <summary>
        /// The one and only instance of this algorithm
        /// </summary>
        public static readonly OneInstructionPerCStepScheduler Instance = new OneInstructionPerCStepScheduler();
    }
}

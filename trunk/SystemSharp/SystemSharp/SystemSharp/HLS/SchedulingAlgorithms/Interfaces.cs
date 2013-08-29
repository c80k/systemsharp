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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Assembler;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.SchedulingAlgorithms
{
    public class ScheduleDependency<T>
    {
        public T Task { get; private set; }
        public long MinDelay { get; private set; }
        public long MaxDelay { get; private set; }

        internal ScheduleDependency(T task, long minDelay, long maxDelay)
        {
            Contract.Requires<ArgumentException>(minDelay >= int.MinValue);
            Contract.Requires<ArgumentException>(minDelay <= int.MaxValue);
            Contract.Requires<ArgumentException>(maxDelay >= int.MinValue);
            Contract.Requires<ArgumentException>(maxDelay <= int.MaxValue);
            Contract.Requires<ArgumentException>(minDelay <= maxDelay);

            Task = task;
            MinDelay = minDelay;
            MaxDelay = maxDelay;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ScheduleDependency<T>;
            if (other == null)
                return false;

            return Task.Equals(other.Task) &&
                MinDelay == other.MinDelay &&
                MaxDelay == other.MaxDelay;
        }

        public override int GetHashCode()
        {
            return Task.GetHashCode() ^
                MinDelay.GetHashCode() ^
                (3 * MaxDelay.GetHashCode());
        }

        public override string ToString()
        {
            return "from " + Task.ToString() + ": " + MinDelay + "..." + MaxDelay;
        }
    }

    [ContractClass(typeof(SchedulingAdapterContractClass<>))]
    public interface ISchedulingAdapter<T>
    {
        IPropMap<T, int> Index { get; }
        IPropMap<T, ScheduleDependency<T>[]> Preds { get; }
        IPropMap<T, ScheduleDependency<T>[]> Succs { get; }
        IPropMap<T, int[]> Operands { get; }
        IPropMap<T, int[]> Results { get; }
        IPropMap<T, long> Latency { get; }
        IPropMap<T, long> CStep { get; }
        IPropMap<T, object> IClass { get; }
        bool TryPin(T task, long cstep, out long preHint, out long postHint);
        void ClearSchedule();
    }

    [ContractClassFor(typeof(ISchedulingAdapter<>))]
    abstract class SchedulingAdapterContractClass<T> : ISchedulingAdapter<T>
    {
        public IPropMap<T, int> Index
        {
            get 
            { 
                Contract.Ensures(Contract.Result<IPropMap<T, int>>() != null); 
                throw new NotImplementedException();
            }
        }

        public IPropMap<T, ScheduleDependency<T>[]> Preds
        {
            get 
            {
                Contract.Ensures(Contract.Result<IPropMap<T, ScheduleDependency<T>[]>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public IPropMap<T, ScheduleDependency<T>[]> Succs
        {
            get 
            {
                Contract.Ensures(Contract.Result<IPropMap<T, ScheduleDependency<T>[]>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public IPropMap<T, int[]> Operands
        {
            get 
            {
                Contract.Ensures(Contract.Result<IPropMap<T, int[]>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public IPropMap<T, int[]> Results
        {
            get 
            {
                Contract.Ensures(Contract.Result<IPropMap<T, int[]>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public IPropMap<T, long> Latency
        {
            get 
            {
                Contract.Ensures(Contract.Result<IPropMap<T, long>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public IPropMap<T, long> CStep
        {
            get 
            {
                Contract.Ensures(Contract.Result<IPropMap<T, long>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public IPropMap<T, object> IClass
        {
            get 
            {
                Contract.Ensures(Contract.Result<IPropMap<T, object>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public bool TryPin(T task, long cstep, out long preHint, out long postHint)
        {
            Contract.Requires<ArgumentNullException>(cstep >= 0);
            throw new NotImplementedException();
        }

        public void ClearSchedule()
        {
            throw new NotImplementedException();
        }
    }

    public class ConstrainedPath
    {
        public int First { get; private set; }
        public int Last { get; private set; }
        public long MinCSteps { get; private set; }
        public long MaxCSteps { get; private set; }

        public ConstrainedPath(int first, int last, int minCSteps, int maxCSteps)
        {
            First = first;
            Last = last;
            MinCSteps = minCSteps;
            MaxCSteps = maxCSteps;
        }
    }

    public class SchedulingConstraints
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public double SchedScale { get; set; }
        public List<ScheduleProfiler> Profilers { get; private set; }
        public bool MinimizeNumberOfFUs { get; set; }
        //public List<ConstrainedPath> ConstrainedPaths { get; private set; }

        public SchedulingConstraints()
        {
            Profilers = new List<ScheduleProfiler>();
            //ConstrainedPaths = new List<ConstrainedPath>();
            SchedScale = 1.1;
        }
    }

    public interface IBasicBlockSchedulingAlgorithm
    {
        void Schedule<T>(IEnumerable<T> tasks, ISchedulingAdapter<T> scha, SchedulingConstraints constraints);
    }

    public interface ICFGSchedulingAlgorithm
    {
        void Schedule<T>(ControlFlowGraph<T> cfg, SchedulingConstraints constraints, ISchedulingAdapter<T> scha)
            where T: IInstruction;
    }
}

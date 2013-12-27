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
    /// <summary>
    /// Models a scheduling dependency between any two operations.
    /// </summary>
    /// <typeparam name="T">Type of operation/task/instruction to consider</typeparam>
    public class ScheduleDependency<T>
    {
        /// <summary>
        /// Predecessing operation/task/instruction
        /// </summary>
        public T Task { get; private set; }

        /// <summary>
        /// Minimum number of c-steps to lapse after initiiation of <c>Task</c>
        /// </summary>
        public long MinDelay { get; private set; }

        /// <summary>
        /// Maximum number of c-steps to lapse after initiiation of <c>Task</c>
        /// </summary>
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

        /// <summary>
        /// Two dependencies are defined to be equal iff they refer to the same task and have the same minimum/maximum delays.
        /// </summary>
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

    /// <summary>
    /// A scheduling adapter is used to extract the scheduling-relevant properties
    /// from any type of operation/task/instruction.
    /// </summary>
    /// <typeparam name="T">type of operation/task/instruction</typeparam>
    [ContractClass(typeof(SchedulingAdapterContractClass<>))]
    public interface ISchedulingAdapter<T>
    {
        /// <summary>
        /// Returns a readable property map which maps each operation to a unique instruction index.
        /// </summary>
        IPropMap<T, int> Index { get; }

        /// <summary>
        /// Returns a readable property map which maps each operation to its predecessor dependencies.
        /// </summary>
        IPropMap<T, ScheduleDependency<T>[]> Preds { get; }

        /// <summary>
        /// Returns a readable property map which maps each operation to its successor dependencies.
        /// </summary>
        IPropMap<T, ScheduleDependency<T>[]> Succs { get; }

        /// <summary>
        /// Returns a readable property map which maps each operation to its operands. An operand is represented
        /// by a unique integer index (like a handle) that equals the result index of the producing instruction.
        /// </summary>
        IPropMap<T, int[]> Operands { get; }

        /// <summary>
        /// Returns a readable property map which maps each operation to its result. A result is represented
        /// by a unique integer index (like a handle) that equals the operand indices of its consuming instructions.
        /// Single assignment is mandatory, i.e. no two operations may produce the same result index.
        /// </summary>
        IPropMap<T, int[]> Results { get; }

        /// <summary>
        /// Returns a readable property map which maps each operation to its latency (measured in c-steps).
        /// </summary>
        IPropMap<T, long> Latency { get; }

        /// <summary>
        /// Returns a readable and writeable property map to receive the c-step which was selected by the
        /// scheduling algorithm.
        /// </summary>
        IPropMap<T, long> CStep { get; }

        /// <summary>
        /// Returns a readable property map which classifies each operation with respect to its type.
        /// The actual type of the returned object does not matter. Two operations are considered to be
        /// of equal type if their according classification objects are equal in terms of <c>object.Equals</c>.
        /// The classification is required for resource-constrained scheduling algorithms, such as
        /// force-directed scheduling: any two operations of the same type are assumed to compete for the same set
        /// of execution resources.
        /// </summary>
        IPropMap<T, object> IClass { get; }

        /// <summary>
        /// Tries to assign an operation to a particular c-step which is determined by the scheduling algorithm.
        /// </summary>
        /// <param name="task">operation to assign</param>
        /// <param name="cstep">selected c-step</param>
        /// <param name="preHint">out parameter to receive a viable earlier c-step if the assignment is inhibited</param>
        /// <param name="postHint">out parameter to receive a viale later c-step if the assignment is inhibited</param>
        /// <returns><c>true</c> if the assignment is successful, <c>false</c> if inhibited</returns>
        bool TryPin(T task, long cstep, out long preHint, out long postHint);

        /// <summary>
        /// Clears all assigned operation/c-step mappings.
        /// </summary>
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

    /// <summary>
    /// Provides requirements and hints to the scheduling algorithm.
    /// </summary>
    /// <remarks>
    /// The underlying concept is somewhat unclear and not sound, so this class should be subject
    /// to major refactorings.
    /// </remarks>
    public class SchedulingConstraints
    {
        /// <summary>
        /// Earliest c-step to assign.
        /// </summary>
        public long StartTime { get; set; }

        /// <summary>
        /// Latest c-step to assign.
        /// </summary>
        public long EndTime { get; set; }

        /// <summary>
        /// Scaling factor for time/resource tradeoff scheduling algorithms which gets multiplied with the
        /// ASAP schedule length to determine the admissible time frame. Therefore required to be >= 1.
        /// </summary>
        public double SchedScale { get; set; }

        /// <summary>
        /// Attached profilers.
        /// </summary>
        public List<ScheduleProfiler> Profilers { get; private set; }

        /// <summary>
        /// Whether the scheduling algorithm should try to minimize the number of functional units,
        /// possibly at the expense of the resulting design performance.
        /// </summary>
        public bool MinimizeNumberOfFUs { get; set; }

        /// <summary>
        /// Constructs an instance and assigns some default values.
        /// </summary>
        public SchedulingConstraints()
        {
            Profilers = new List<ScheduleProfiler>();
            SchedScale = 1.1;
        }
    }

    /// <summary>
    /// Interface of a scheduling algorithm at the basic block level.
    /// </summary>
    public interface IBasicBlockSchedulingAlgorithm
    {
        /// <summary>
        /// Performs scheduling for a given sequence of operations/instructions/tasks.
        /// </summary>
        /// <typeparam name="T">type of operation/instruction/task</typeparam>
        /// <param name="tasks">sequence of operations to schedule</param>
        /// <param name="scha">scheduling adapter, exposing all scheduling-relevant operation properties</param>
        /// <param name="constraints">scheduling constraints</param>
        void Schedule<T>(IEnumerable<T> tasks, ISchedulingAdapter<T> scha, SchedulingConstraints constraints);
    }

    /// <summary>
    /// Interface of a function-level scheduling algorithm.
    /// </summary>
    public interface ICFGSchedulingAlgorithm
    {
        /// <summary>
        /// Performs scheduling for a given control-flow graph.
        /// </summary>
        /// <typeparam name="T">type of instruction</typeparam>
        /// <param name="cfg">control-flow graph to schedule</param>
        /// <param name="constraints">scheduling constraints</param>
        /// <param name="scha">scheduling adapter, exposing all scheduling-relevant operation properties</param>
        void Schedule<T>(ControlFlowGraph<T> cfg, SchedulingConstraints constraints, ISchedulingAdapter<T> scha)
            where T: IInstruction;
    }
}

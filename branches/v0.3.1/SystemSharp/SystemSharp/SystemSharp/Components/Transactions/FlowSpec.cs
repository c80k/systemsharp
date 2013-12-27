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
using System.Linq;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Transformations;
using SystemSharp.Meta;

namespace SystemSharp.Components.Transactions
{
    /// <summary>
    /// Abstract model of a single dataflow
    /// </summary>
    public interface IFlow
    {
        /// <summary>
        /// Target of dataflow
        /// </summary>
        SignalRef Target { get; }

        /// <summary>
        /// Converts the dataflow to a behavioral description
        /// </summary>
        /// <returns>process implementing the dataflow</returns>
        IProcess ToProcess();
    }

    /// <summary>
    /// Abstract base class for a single dataflow
    /// </summary>
    public abstract class Flow:
        IFlow
    {
        /// <summary>
        /// Target of dataflow
        /// </summary>
        public SignalRef Target { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="target">target of dataflow</param>
        public Flow(SignalRef target)
        {
            Target = target;
        }

        /// <summary>
        /// Converts the dataflow to a behavioral description
        /// </summary>
        /// <returns>process implementing the dataflow</returns>
        public abstract IProcess ToProcess();

        /// <summary>
        /// Returns the right-hand-side expression being assigned to <c>Target</c>.
        /// </summary>
        public Expression GetRHS()
        {
            var builder = new DefaultAlgorithmBuilder();
            ToProcess().Implement(builder);
            var stmt = builder.Complete().Body.AsStatementList().First();
            var sstmt = stmt as StoreStatement;
            return sstmt.Value;
        }
    }

    /// <summary>
    /// A dataflow from one signal to another signal
    /// </summary>
    public class SignalFlow : Flow
    {
        /// <summary>
        /// Source of dataflow
        /// </summary>
        public SignalRef Source { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="source">source of dataflow</param>
        /// <param name="target">target of dataflow</param>
        public SignalFlow(SignalRef source, SignalRef target):
            base(target)
        {
            Source = source;
        }

        public override string ToString()
        {
            return Target.ToString() + " <= " + Source.ToString();
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>SignalFlow</c> with same source and same target.
        /// </summary>
        public override bool Equals(object obj)
        {
            SignalFlow other = obj as SignalFlow;
            if (other == null)
                return false;
            return Target.Equals(other.Target) &&
                Source.Equals(other.Source);
        }

        public override int GetHashCode()
        {
            return Target.GetHashCode() ^ (3 * Source.GetHashCode());
        }

        public override IProcess ToProcess()
        {
            return Target.ToSignal().DriveUT(Source.ToSignal().AsSignalSource());
        }
    }

    /// <summary>
    /// A dataflow from one signal to another, whereby the transport is delayed by a specified number of clocks.
    /// </summary>
    public class DelayedSignalFlow : 
        SignalFlow,
        IComparable<DelayedSignalFlow>
    {
        /// <summary>
        /// Transport delay
        /// </summary>
        public long Delay { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="source">source of dataflow</param>
        /// <param name="target">target of dataflow</param>
        /// <param name="delay">transport delay</param>
        public DelayedSignalFlow(SignalRef source, SignalRef target, long delay):
            base(source, target)
        {
            Delay = delay;
        }

        public override string ToString()
        {
            return Target.ToString() + " <= " + Source.ToSignal() + " after " + Delay;
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>DelayedSignalFlow</c> with same source and same target.
        /// </summary>
        public override bool Equals(object obj)
        {
            DelayedSignalFlow other = obj as DelayedSignalFlow;
            if (other == null)
                return false;
            return Target.Equals(other.Target) &&
                Source.Equals(other.Source) &&
                Delay == other.Delay;
        }

        public override int GetHashCode()
        {
            return Target.GetHashCode() ^ 
                (3 * Source.GetHashCode()) ^
                Delay.GetHashCode();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="NotSupportedException">always thrown</exception>
        public override IProcess ToProcess()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not yet defined.
        /// </summary>
        /// <exception cref="NotImplementedException">always thrown</exception>
        public int CompareTo(DelayedSignalFlow other)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A dataflow from one signal to another which happens at a specified timestamp.
    /// </summary>
    public class TimestampedSignalFlow : SignalFlow
    {
        /// <summary>
        /// Timestamp when this dataflow is active
        /// </summary>
        public long Time { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="source">source of dataflow</param>
        /// <param name="target">target of dataflow</param>
        /// <param name="time">timestamp when dataflow is active</param>
        public TimestampedSignalFlow(SignalRef source, SignalRef target, long time) :
            base(source, target)
        {
            Time = time;
        }

        public override string ToString()
        {
            return Time + ": " + Target.ToString() + " <= " + Source.ToString();
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>TimestampedSignalFlow</c> with same source and same target.
        /// </summary>
        public override bool Equals(object obj)
        {
            TimestampedSignalFlow other = obj as TimestampedSignalFlow;
            if (other == null)
                return false;
            return Target.Equals(other.Target) &&
                Source.Equals(other.Source) &&
                Time == other.Time;
        }

        public override int GetHashCode()
        {
            return Target.GetHashCode() ^
                (3 * Source.GetHashCode()) ^
                Time.GetHashCode();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="NotSupportedException">always thrown</exception>
        public override IProcess ToProcess()
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// A timed dataflow is a dataflow with a specific timestamp.
    /// </summary>
    public interface ITimedFlow : 
        IFlow,
        IComparable<ITimedFlow>
    {
        /// <summary>
        /// Timestamp when dataflow is active
        /// </summary>
        long Time { get; }
    }

    /// <summary>
    /// A dataflow from one signal to another which starts at a specified timestamp and has a specified transport delay.
    /// </summary>
    public class TimedSignalFlow : 
        SignalFlow,
        ITimedFlow,
        IComparable<TimedSignalFlow>
    {
        /// <summary>
        /// Timestamp when dataflow is active
        /// </summary>
        public long Time { get; private set; }

        /// <summary>
        /// Transport delay
        /// </summary>
        public long Delay { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="source">source of dataflow</param>
        /// <param name="target">target of dataflow</param>
        /// <param name="time">timestamp when dataflow is active</param>
        /// <param name="delay">transport delay</param>
        public TimedSignalFlow(SignalRef source, SignalRef target, long time, long delay) :
            base(source, target)
        {
            Time = time;
            Delay = delay;
        }

        public override string ToString()
        {
            return Time + ": " + Target.ToString() + " <= " + Source.ToString() + " after " + Delay;
        }

        public override bool Equals(object obj)
        {
            TimedSignalFlow other = obj as TimedSignalFlow;
            if (other == null)
                return false;
            return Target.Equals(other.Target) &&
                Source.Equals(other.Source) &&
                Time == other.Time &&
                Delay == other.Delay;
        }

        public override int GetHashCode()
        {
            return Target.GetHashCode() ^
                (3 * Source.GetHashCode()) ^
                (5 * Delay.GetHashCode()) ^
                Time.GetHashCode();
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <exception cref="NotSupportedException">always thrown</exception>
        public override IProcess ToProcess()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Compares this instance to another timed signal flow.
        /// A partial ordering is defined such that dataflows with earlier timestamp precede, 
        /// shorter transport delay precedes for equal timestamps.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(TimedSignalFlow other)
        {
            if (Time < other.Time)
                return -1;
            if (Time > other.Time)
                return 1;
            if (Delay < other.Delay)
                return -1;
            if (Delay > other.Delay)
                return 1;
            return 0;
        }

        public int CompareTo(ITimedFlow other)
        {
            if (Time < other.Time)
                return -1;
            if (Time > other.Time)
                return 1;
            return 0;
        }
    }

    /// <summary>
    /// A dataflow where a constant value is transferred to the target signal.
    /// </summary>
    public class ValueFlow : Flow
    {
        /// <summary>
        /// Constant value to transfer
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="value">constant value to transfer</param>
        /// <param name="target">target signal</param>
        public ValueFlow(object value, SignalRef target):
            base(target)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Target.ToString() + " <= \"" + Value.ToString() + "\"";
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>ValueFlow</c> with same constant and same target.
        /// </summary>
        public override bool Equals(object obj)
        {
            ValueFlow other = obj as ValueFlow;
            if (other == null)
                return false;
            return Target.Equals(other.Target) &&
                Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return Target.GetHashCode() ^ Value.GetHashCode();
        }

        public override IProcess ToProcess()
        {
            return Target.ToSignal().DriveUT(SignalSource.CreateUT(Value));
        }
    }

    /// <summary>
    /// A transfer of a constant value to a signal which is active at a specified timestamp.
    /// </summary>
    public class TimedValueFlow :
        ValueFlow,
        ITimedFlow,
        IComparable<TimedValueFlow>
    {
        /// <summary>
        /// Timestamp when this flow is active
        /// </summary>
        public long Time { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="value">constant value to transfer</param>
        /// <param name="target">target of dataflow</param>
        /// <param name="time">timestamp when dataflow is active</param>
        public TimedValueFlow(object value, SignalRef target, long time) :
            base(value, target)
        {
            Time = time;
        }

        public int CompareTo(ITimedFlow other)
        {
            if (Time < other.Time)
                return -1;
            else if (Time == other.Time)
                return 0;
            else
                return 1;
        }

        public int CompareTo(TimedValueFlow other)
        {
            return CompareTo((ITimedFlow)other);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>TimedValueFlow</c> with same value, same target and same timstamp.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            TimedValueFlow other = obj as TimedValueFlow;
            if (other == null)
                return false;
            return object.Equals(Value, other.Value) &&
                object.Equals(Target, other.Target) &&
                Time == other.Time;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^
                Target.GetHashCode() ^
                Time.GetHashCode();
        }

        public override string ToString()
        {
            return Time + ": " + base.ToString();
        }
    }

    /// <summary>
    /// Describes an aggregate dataflow, consisting of multiple single dataflows which are realized in parallel.
    /// </summary>
    public class ParFlow
    {
        private Dictionary<SignalRef, Flow> _flows;

        /// <summary>
        /// All parallel dataflows
        /// </summary>
        public IEnumerable<Flow> Flows
        {
            get { return _flows.Values; }
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public ParFlow()
        {
            _flows = new Dictionary<SignalRef, Flow>();
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="flows">dataflows to be realized in parallel</param>
        public ParFlow(IEnumerable<Flow> flows)
        {
            _flows = new Dictionary<SignalRef, Flow>();
            foreach (var flow in flows)
                _flows[flow.Target] = flow;
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="other">other aggregate flow from which to overtake the parallel flows</param>
        public ParFlow(ParFlow other)
        {
            _flows = new Dictionary<SignalRef, Flow>(other._flows);
        }

        public IProcess ToProcess()
        {
            return Processes.Fork(Flows.Select(f => f.ToProcess()).ToArray());
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>ParFlow</c> with equal set of dataflows.
        /// </summary>
        public override bool Equals(object obj)
        {
            var other = obj as ParFlow;
            if (other == null)
                return false;

            var vset = new HashSet<Flow>(_flows.Values);
            return vset.SetEquals(other._flows.Values);
        }

        public override int GetHashCode()
        {
            return _flows.GetSetHashCode();
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, _flows);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="target"/> is the destination of any contained dataflows.
        /// </summary>
        public bool ContainsTarget(SignalRef target)
        {
            return _flows.ContainsKey(target);
        }

        /// <summary>
        /// Looks up the dataflow associated with a specific destination.
        /// </summary>
        /// <param name="target">destination signal</param>
        /// <returns>associated dataflow, or <c>null</c> if not present</returns>
        public Flow LookupTarget(SignalRef target)
        {
            Flow result;
            _flows.TryGetValue(target, out result);
            return result;
        }

        /// <summary>
        /// Adds another dataflow to the set of parallel flows.
        /// </summary>
        /// <param name="flow">dataflow to add</param>
        public void Add(Flow flow)
        {
            _flows[flow.Target] = flow;
        }

        /// <summary>
        /// Adds all dataflows of another aggregate dataflow to the set of parallel flows.
        /// </summary>
        /// <param name="other">aggregate dataflow</param>
        public void Integrate(ParFlow other)
        {
            foreach (var flow in other.Flows)
                Add(flow);
        }
    }

    /// <summary>
    /// This static class contains extensions to convert processes to aggregate dataflows
    /// </summary>
    public static class ProcessToFlowExtensions
    {
        private class ToFlowBuilder : 
            IAlgorithmBuilder,
            ILiteralVisitor
        {
            bool _isTarget;
            SignalRef _target;
            private List<Flow> _flows = new List<Flow>();

            public ParFlow Result
            {
                get { return new ParFlow(_flows); }
            }

            public ToFlowBuilder()
            {
            }

            public void DeclareLocal(IStorableLiteral v)
            {
            }

            public void Store(IStorableLiteral var, Expression val)
            {
                if (var.StoreMode != EStoreMode.Transfer)
                    throw new NotSupportedException();

                _isTarget = true;
                var.Accept(this);

                var rhs = val as LiteralReference;
                if (rhs == null)
                    throw new NotSupportedException();

                _isTarget = false;
                rhs.ReferencedObject.Accept(this);
            }

            public IfStatement If(Expression cond)
            {
                throw new NotSupportedException();
            }

            public void ElseIf(Expression cond)
            {
                throw new NotSupportedException();
            }

            public void Else()
            {
                throw new NotSupportedException();
            }

            public void EndIf()
            {
                throw new NotSupportedException();
            }

            public LoopBlock Loop()
            {
                throw new NotSupportedException();
            }

            public void Break(LoopBlock loop)
            {
                throw new NotSupportedException();
            }

            public void Continue(LoopBlock loop)
            {
                throw new NotSupportedException();
            }

            public void EndLoop()
            {
                throw new NotSupportedException();
            }

            public void Solve(Algebraic.EquationSystem eqsys)
            {
                throw new NotSupportedException();
            }

            public void InlineCall(Function fn, Expression[] inArgs, Variable[] outArgs, bool shareLocals = false)
            {
                throw new NotSupportedException();
            }

            public CaseStatement Switch(Expression selector)
            {
                throw new NotSupportedException();
            }

            public void Case(Expression cond)
            {
                throw new NotSupportedException();
            }

            public void DefaultCase()
            {
                throw new NotSupportedException();
            }

            public void GotoCase(CaseStatement cstmt, int index)
            {
                throw new NotSupportedException();
            }

            public void Break(CaseStatement stmt)
            {
                throw new NotSupportedException();
            }

            public void EndCase()
            {
                throw new NotSupportedException();
            }

            public void EndSwitch()
            {
                throw new NotSupportedException();
            }

            public GotoStatement Goto()
            {
                throw new NotSupportedException();
            }

            public void Return()
            {
                throw new NotSupportedException();
            }

            public void Return(Expression returnValue)
            {
                throw new NotSupportedException();
            }

            public void Throw(Expression expr)
            {
                throw new NotSupportedException();
            }

            public void Call(ICallable callee, Expression[] arguments)
            {
                throw new NotSupportedException();
            }

            public void Nop()
            {
                throw new NotSupportedException();
            }

            public Statement LastStatement
            {
                get { throw new NotImplementedException(); }
            }

            public void RemoveLastStatement()
            {
                throw new NotImplementedException();
            }

            public bool HaveAnyStatement
            {
                get { throw new NotImplementedException(); }
            }

            public IAlgorithmBuilder BeginSubAlgorithm()
            {
                throw new NotImplementedException();
            }

            public void Comment(string comment)
            {
            }

            public void VisitConstant(Constant constant)
            {
                Debug.Assert(!_isTarget);
                if (constant.ConstantValue == null)
                    throw new ArgumentException();
                Flow flow = new ValueFlow(constant.ConstantValue, _target);
                _flows.Add(flow);
            }

            public void VisitVariable(Variable variable)
            {
                throw new NotSupportedException();
            }

            public void VisitFieldRef(FieldRef fieldRef)
            {
                throw new NotSupportedException();
            }

            public void VisitThisRef(ThisRef thisRef)
            {
                throw new NotSupportedException();
            }

            public void VisitSignalRef(SignalRef signalRef)
            {
                if (signalRef.Desc.ElementType == null)
                    throw new ArgumentException();
                if (_isTarget)
                {
                    _target = signalRef;
                }
                else
                {
                    Flow flow = new SignalFlow(signalRef, _target);
                    _flows.Add(flow);
                }
            }

            public void VisitArrayRef(ArrayRef arrayRef)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Converts the process to an aggregate dataflow. For the conversion to succeed, the process must be
        /// a plain list of register transfers. Control-flow statements and arithmetic/logic expressions are not supported.
        /// </summary>
        /// <param name="ps">process to convert</param>
        /// <returns>aggregate dataflow representation</returns>
        /// <exception cref="NotSupportedException">if the process contains anything unsupported (i.e. control flow, expressions)</exception>
        public static ParFlow ToFlow(this IProcess ps)
        {
            var tfb = new ToFlowBuilder();
            ps.Implement(tfb);
            return tfb.Result;
        }
    }
}

/**
 * Copyright 2011-2013 Christian Köllner, David Hlavac
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
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using SystemSharp.Analysis;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// This class is used internally.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ValueProxy<T>
    {
        public T Value { get; set; }
    }

    /// <summary>
    /// The DesignObject is the abstract base class of M0 modeling classes, such as Component and Channel.
    /// </summary>
    [ModelElement]
    public abstract class DesignObject
    {
        private List<DesignObject> _children = new List<DesignObject>();

        /// <summary>
        /// Constructs a SimObject instance.
        /// </summary>
        public DesignObject()
        {
            Context = DesignContext.Instance;
            Init();
        }

        /// <summary>
        /// Constructs a SimObject instance for a specific design context.
        /// </summary>
        protected DesignObject(DesignContext context)
        {
            Contract.Requires<ArgumentNullException>(context != null);

            Context = context;
            Init();
        }

        private void Init()
        {
            if (Context.CaptureDesignObjectOrigins)
                OriginInfo = Environment.StackTrace;
        }
        
        /// <summary>
        /// Provides access to the simulation context which was used throughout the construction of the SimObject.
        /// </summary>
        public DesignContext Context
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.SimulationContext)]
            get;
            private set;
        }

        /// <summary>
        /// Returns a collection of all dependent ("owned") objects.
        /// </summary>
        public ICollection<DesignObject> Children
        {
            get
            {
                Contract.Assume(_children != null);

                return new ReadOnlyCollection<DesignObject>(_children);
            }
        }

        /// <summary>
        /// Provides diagnostic information on object creator (i.e. stack trace during constructor call).
        /// Logging must be previously enabled by setting CaptureDesignObjectOrigins property of DesignContext to true.
        /// </summary>
        public string OriginInfo { get; private set; }
    }

    [MapToIntrinsicType(EIntrinsicTypes.IllegalRuntimeType)]
    public interface IAwaitable : INotifyCompletion
    {
        bool IsCompleted 
        { 
            [RewriteIsCompleted]
            get; 
        }
        [RewriteGetResult]
        void GetResult();
    }

    /// <summary>
    /// Helper class for process-local storage
    /// </summary>
    internal class PLSSlot : DesignObject
    {
        private int _slot;
        private object _fallbackValue;

        /// <summary>
        /// Constructs a process-local storage slot
        /// </summary>
        /// <param name="slot">the slot index</param>
        public PLSSlot(DesignContext context, int slot) :
            base(context)
        {
            _slot = slot;
        }

        /// <summary>
        /// Provides access to the process-local value of the underlying storage slot
        /// </summary>
        public object Value
        {
            get
            {
                if (Context.CurrentProcess == null)
                    return _fallbackValue;
                else
                    return Context.CurrentProcess.GetLocal(_slot);
            }
            set
            {
                if (Context.CurrentProcess == null)
                    _fallbackValue = value;
                else
                    Context.CurrentProcess.StoreLocal(_slot, value);
            }
        }

        /// <summary>
        /// Provides access to all process-local values.
        /// </summary>
        /// <param name="index">The process for which the value should be retrieved/stored</param>
        /// <returns></returns>
        public object this[Process index]
        {
            get
            {
                return index.GetLocal(_slot);
            }
            set
            {
                index.StoreLocal(_slot, value);
            }
        }
    }

    /// <summary>
    /// This enumeration defines kernel message categories.
    /// </summary>
    public enum EIssueClass
    {
        Info,
        Warning,
        Error
    };

    /// <summary>
    /// This enumeration provides some standard time units.
    /// </summary>
    public enum ETimeUnit
    {
        /// <summary>
        /// seconds
        /// </summary>
        sec,
        /// <summary>
        /// milli-seconds
        /// </summary>
        ms,
        /// <summary>
        /// micro-seconds
        /// </summary>
        us,
        /// <summary>
        /// nano-seconds
        /// </summary>
        ns,
        /// <summary>
        /// pico-seconds
        /// </summary>
        ps,
        /// <summary>
        /// femto-seconds
        /// </summary>
        fs
    }

    /// <summary>
    /// This class encodes a time or period of time which is specified as a pair of value and time unit.
    /// </summary>
    [MapToIntrinsicType(EIntrinsicTypes.Time)]
    public class Time : IComparable<Time>
    {
        private class RewriteAwaitTime : RewriteAwait
        {
            public override bool Rewrite(
                CodeDescriptor decompilee, 
                Expression waitObject, 
                Analysis.IDecompiler stack, 
                IFunctionBuilder builder)
            {
                var fspec = new FunctionSpec(typeof(void))
                {
                    IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitFor)
                };
                builder.Call(fspec, waitObject);
                return true;
            }
        }

        [RewriteAwaitTime]
        [MapToIntrinsicType(EIntrinsicTypes.IllegalRuntimeType)]
        private class Awaiter : IAwaitable
        {
            private Time _time;

            public Awaiter(Time time)
            {
                _time = time;
            }

            public bool IsCompleted
            {
                [RewriteIsCompleted]
                get { return false; }
            }

            public void OnCompleted(Action continuation)
            {
                DesignContext.Instance.CurrentProcess.SetContinuation(continuation);
                DesignContext.Instance.Schedule(DesignContext.Instance.CurrentProcess.ContinueProcess,
                    _time.GetTicks(DesignContext.Instance));
            }

            [RewriteGetResult]
            public void GetResult()
            {
            }
        }

        private Awaiter _awaiter;

        /// <summary>
        /// Infinitely in the future
        /// Added by Mário Ferreira
        /// </summary>
        public static readonly Time Infinite = new Time(double.PositiveInfinity, ETimeUnit.sec);

        /// <summary>
        /// The value.
        /// </summary>
        public double Value { get; private set; }

        /// <summary>
        /// The time unit.
        /// </summary>
        public ETimeUnit Unit { get; private set; }

        /// <summary>
        /// Constructs a time specification.
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="unit">The unit</param>
        [StaticEvaluation]
        public Time(double value, ETimeUnit unit)
        {
            if (value < 0.0)
                throw new ArgumentException("Negative times are not allowed");

            Value = value;
            Unit = unit;
            _awaiter = new Awaiter(this);
        }

        public IAwaitable GetAwaiter()
        {
            return _awaiter;
        }

        /// <summary>
        /// Expresses the encoded time in a different unit.
        /// </summary>
        /// <example>
        /// <code>new Time(42.0, ETimeUnit.sec).ScalteTo(ETimeUnit.ms)</code> returns 42000.0
        /// </example>
        /// <param name="destUnit">The destination time unit</param>
        /// <returns>The value in terms of the destination time unit.</returns>
        public double ScaleTo(ETimeUnit destUnit)
        {
            double result = Value;
            while (destUnit < Unit)
            {
                result *= 1e-3;
                ++destUnit;
            }
            while (destUnit > Unit)
            {
                result *= 1e3;
                --destUnit;
            }
            return result;
        }

        /// <summary>
        /// Converts the encoded time to raw ticks with respect to a simulation context.
        /// </summary>
        /// <remarks>
        /// The conversion depends on the time resolution which is specified for the simulation context.
        /// </remarks>
        /// <param name="context">The simulation context</param>
        /// <returns>The time expressed in raw ticks</returns>
        internal long GetTicks(DesignContext context)
        {
            return (long)(ScaleTo(context.Resolution.Unit) / context.Resolution.Value);
        }

        [TypeConversion(typeof(Time), typeof(string))]
        public override string ToString()
        {
            return Value.ToString() + Unit.ToString();
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ Unit.GetHashCode();
        }

        /// <summary>
        /// Tests this object for equality with another object.
        /// </summary>
        /// <remarks>
        /// Two time representations are defined to be equal iff both unit and value match.
        /// Therefore, 0.1sec and 100ms are NOT the same. To check whether two Time instances actually
        /// represent the same physical time, use CompareTo() instead.
        /// </remarks>
        /// <param name="obj">The object to which this object should be compared</param>
        /// <returns>true if both objects are equal, false if not</returns>
        public override bool Equals(object obj)
        {
            if (obj is Time)
            {
                Time time = (Time)obj;
                return time.ScaleTo(Unit) == Value;
            }
            else
                return false;
        }

        /// <summary>
        /// Returns if time lies infinitely in the future
        /// Added by Mário Ferreira
        /// </summary>
        public bool IsInfinite
        {
            get { return double.IsInfinity(Value); }
        }

        #region IComparable<Time> Members

        /// <summary>
        /// Compares this time to another time.
        /// </summary>
        /// <remarks>
        /// If the time units don't match, the values get scaled accordingly.
        /// </remarks>
        /// <param name="other">The time to which this time should be compared</param>
        /// <returns>0 if both times specify the same physical time, -1 if this time is shorter, 1 if it is longer</returns>
        public int CompareTo(Time other)
        {
            double diff = Value - other.ScaleTo(Unit);
            if (diff < 0.0)
                return -1;
            else if (diff > 0.0)
                return 1;
            else
                return 0;
        }

        #endregion

        public static Time operator +(Time a, Time b)
        {
            return new Time(a.Value + b.ScaleTo(a.Unit), a.Unit);
        }

        public static Time operator -(Time a, Time b)
        {
            return new Time(a.Value - b.ScaleTo(a.Unit), a.Unit);
        }

        /// <summary>
        /// Scales a time by a factor.
        /// </summary>
        /// <param name="scale">The scaling factor</param>
        /// <param name="time">The time to be scaled</param>
        /// <returns>The scaled time</returns>
        public static Time operator *(double scale, Time time)
        {
            return new Time(scale * time.Value, time.Unit);
        }

        /// <summary>
        /// Divides a time by a value.
        /// </summary>
        /// <param name="time">The time to be divided.</param>
        /// <param name="quot">The divider</param>
        /// <returns>The divided time</returns>
        public static Time operator /(Time time, double quot)
        {
            return new Time(time.Value / quot, time.Unit);
        }

        public static double operator /(Time time1, Time time2)
        {
            double normTime1 = time1.ScaleTo(time2.Unit);
            return normTime1 / time2.Value;
        }

        public static bool operator <(Time a, Time b)
        {
            return a.Value - b.ScaleTo(a.Unit) < 0.0;
        }

        public static bool operator <=(Time a, Time b)
        {
            return a.Value - b.ScaleTo(a.Unit) <= 0.0;
        }

        public static bool operator >(Time a, Time b)
        {
            return a.Value - b.ScaleTo(a.Unit) >= 0.0;
        }

        public static bool operator >=(Time a, Time b)
        {
            return a.Value - b.ScaleTo(a.Unit) >= 0.0;
        }

        public static bool operator ==(Time a, Time b)
        {
            return Math.Abs(a.Value - b.ScaleTo(a.Unit)) < (1e1 * double.Epsilon);
        }

        public static bool operator !=(Time a, Time b)
        {
            return Math.Abs(a.Value - b.ScaleTo(a.Unit)) >= (1e1 * double.Epsilon);
        }

        public static Time Create(double value, ETimeUnit unit)
        {
            return new Time(value, unit);
        }
    }
}

/**
 * Copyright 2011-2014 Christian Köllner, David Hlavac
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;
using LinqEx = System.Linq.Expressions.Expression;

namespace SystemSharp.Components
{
#if false
    /// <summary>
    /// Interface definition for signals
    /// </summary>
    public interface ISignal
    {
        /// <summary>
        /// The changed event is triggered whenever the signal changes its value.
        /// </summary>
        EventSource ChangedEvent { get; }

        /// <summary>
        /// Initial signal value, i.e. before the first write operation occurs.
        /// </summary>
        object InitialValueObject { [StaticEvaluation] get; }

        /// <summary>
        /// Signal value of previous delta cycle
        /// </summary>
        object PreObject { [SignalProperty(SignalRef.EReferencedProperty.Pre)] get; }

        /// <summary>
        /// Current signal value
        /// </summary>
        object CurObject { [SignalProperty(SignalRef.EReferencedProperty.Cur)] get; }

        /// <summary>
        /// Sets the signal value for next delta cycle
        /// </summary>
        object NextObject { [SignalProperty(SignalRef.EReferencedProperty.Next)] set; }

        /// <summary>
        /// Type of signal data
        /// </summary>
        TypeDescriptor ElementType { get; }

        /// <summary>
        /// Represents this signal as a SysDOM signal reference.
        /// </summary>
        /// <param name="prop">signal property to reference</param>
        SignalRef ToSignalRef(SignalRef.EReferencedProperty prop);
    }
#endif

    /// <summary>
    /// This is the abstract base class of a signal.
    /// </summary>
    [SignalArgument]
    [SignalField]
    public abstract class SignalBase : 
        DesignObject,
        IDescriptive<SignalDescriptor, SignalBase>
    {
        private SignalDescriptor _descriptor;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public SignalBase()
        {
            Context.RegisterSignal(this);
        }

        /// <summary>
        /// Creates a SysDOM signal descriptor for this signal instance.
        /// You must override this method in your concrete signal implementation.
        /// </summary>
        protected abstract SignalDescriptor CreateSignalDescriptor();

        /// <summary>
        /// Returns the event which is signaled whenever the signal changes its value.
        /// </summary>       
        public abstract EventSource ChangedEvent
        {
            [SignalProperty(SignalRef.EReferencedProperty.ChangedEvent)]
            get;
        }
        
        /// <summary>
        /// Returns the initial value of this signal (untyped).
        /// </summary>
        public abstract object InitialValue
        { 
            [StaticEvaluation]
            get; 
            [AssumeNotCalled]
            set; 
        }

        /// <summary>
        /// Returns the previous value of this signal (untyped).
        /// </summary>
        public abstract object Pre
        {
            [SignalProperty(SignalRef.EReferencedProperty.Pre)]
            get; 
        }

        /// <summary>
        /// Returns the current value of this signal (untyped).
        /// </summary>
        public abstract object Cur
        {
            [SignalProperty(SignalRef.EReferencedProperty.Cur)]
            get; 
        }

        /// <summary>
        /// Writes the signal value (untyped).
        /// </summary>
        public abstract object Next
        {
            [SignalProperty(SignalRef.EReferencedProperty.Next)]
            set; 
        }

        DescriptorBase IDescriptive.Descriptor
        {
            get { return Descriptor; }
        }

        public new SignalDescriptor Descriptor
        {
            get 
            {
                if (_descriptor == null)
                    _descriptor = CreateSignalDescriptor();
                return _descriptor;
            }
        }
    }

    /// <summary>
    /// Basic signal implementation.
    /// </summary>
    /// <remarks>
    /// A signal is a mquantity which changes its value at discrete time instants.
    /// </remarks>
    /// <typeparam name="T">The data type of the carried quantity.</typeparam>
    [MapToIntrinsicType(EIntrinsicTypes.Signal)]
    public class Signal<T> : 
        SignalBase,
        IIn<T>,
        IOut<T>
    {
        #region private types

        private class InProxy : In<T>
        {
            private Signal<T> _instance;

            public InProxy(Signal<T> instance)
            {
                _instance = instance;
            }

            public override T Cur
            {
                get { return _instance.Cur; }
            }

            public override T Pre
            {
                get { return _instance.Pre; }
            }

            public override EventSource ChangedEvent
            {
                get { return _instance.ChangedEvent; }
            }
        }

        private class OutProxy : Out<T>
        {
            private Signal<T> _instance;

            public OutProxy(Signal<T> instance)
            {
                _instance = instance;
            }

            public override T Next
            {
                set { _instance.Next = value; }
            }
        }

        private class InOutProxy : InOut<T>
        {
            private Signal<T> _instance;

            public InOutProxy(Signal<T> instance)
            {
                _instance = instance;
            }

            public override T Cur
            {
                get { return _instance.Cur; }
            }

            public override T Pre
            {
                get { return _instance.Pre; }
            }

            public override T Next
            {
                set { _instance.Next = value; }
            }
        }

        #endregion private types

        private T _initialValue;
        protected T _preValue;
        protected T _curValue;
        protected T _nextValue;
        private Event _changedEvent;

        /// <summary>
        /// Constructs a signal.
        /// </summary>
        public Signal()
        {
            Context.OnNextDeltaCycle += OnNextDeltaCycle;
            _changedEvent = new Event(this);
        }

        /// <summary>
        /// Represents the signal's initial value.
        /// </summary>
        /// <remarks>
        /// At the beginning of the simulation, the signal is pre-initialized with the specified value.
        /// </remarks>
        public new virtual T InitialValue
        {
            get
            {
                return _initialValue;
            }
            set
            {
                if (DesignContext.Instance.State != DesignContext.ESimState.Construction &&
                    DesignContext.Instance.State != DesignContext.ESimState.DesignAnalysis)
                    throw new InvalidOperationException("Access to initial value is only allowed during construction");

                _initialValue = value;
                _nextValue = value;
                _curValue = value;
            }
        }

        object SignalBase.InitialValue
        {
            [StaticEvaluation]
            get { return InitialValue; }
            [AssumeNotCalled]
            set { InitialValue = (T)value; }
        }

        /// <summary>
        /// Returns the current signal value.
        /// </summary>        
        public new virtual T Cur
        {
            [SignalProperty(SignalRef.EReferencedProperty.Cur)]
            get
            {
                if (DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis)
                    return _initialValue;

                if (DesignContext.Instance.State != DesignContext.ESimState.Simulation)
                    throw new InvalidOperationException("Access to value is only allowed during simulation");

                return _curValue;
            }
        }

        /// <summary>
        /// Returns the current signal value (untyped).
        /// </summary>
        object SignalBase.Cur
        {
            get { return Cur; }
        }

        /// <summary>
        /// Writes the signal value.
        /// </summary>        
        public new virtual T Next
        {
            [SignalProperty(SignalRef.EReferencedProperty.Next)]
            set
            {
                if (DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis)
                    return;

                if (DesignContext.Instance.State != DesignContext.ESimState.Simulation)
                    throw new InvalidOperationException("Access to value is only allowed during simulation");

                _nextValue = value;
            }
        }

        /// <summary>
        /// Write the signal value (untyped).
        /// </summary>
        object SignalBase.Next
        {
            set  { Next = (T)value; }
        }

        /// <summary>
        /// Returns the previous signal value.
        /// </summary>        
        public new virtual T Pre
        {
            [SignalProperty(SignalRef.EReferencedProperty.Pre)]
            get
            {
                if (DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis)
                    return _initialValue;

                if (DesignContext.Instance.State != DesignContext.ESimState.Simulation)
                    throw new InvalidOperationException("Access to value is only allowed during simulation");

                return _preValue;
            }
        }

        /// <summary>
        /// Returns the previous signal value (untyped).
        /// </summary>
        object SignalBase.Pre
        {
            get { return Pre; }
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        protected virtual void OnNextDeltaCycle()
        {
            if (!_nextValue.Equals(_curValue))
                _changedEvent.Fire();
            _preValue = _curValue;
            _curValue = _nextValue;
        }

        protected override SignalDescriptor CreateSignalDescriptor()
        {
            return new SignalDescriptor(this);
        }

        public static implicit operator In<T>(Signal<T> signal)
        {
            return new InProxy(signal);
        }

        public static implicit operator Out<T>(Signal<T> signal)
        {
            return new OutProxy(signal);
        }

        public static implicit operator InOut<T>(Signal<T> signal)
        {
            return new InOutProxy(signal);
        }

        /// <summary>
        /// Constructs a signal interface that provides the negated signal value.
        /// </summary>
        /// <param name="x">signal whose value to negate</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement negation.</exception>
        public static In<T> operator -(Signal<T> x)
        {
            return new UnaryOpIn<T>(x, e => -e, GenericMath<T>.Negate);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise complemented signal value.
        /// </summary>
        /// <param name="x">signal whose value to negate</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the complement operator.</exception>
        public static In<T> operator !(Signal<T> x)
        {
            return new UnaryOpIn<T>(x, e => !e, GenericMath<T>.Not);
        }

        /// <summary>
        /// Constructs a signal interface that provides the sum of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first summand</param>
        /// <param name="y">signal that provides the second summand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement addition.</exception>
        public static In<T> operator +(Signal<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex + ey, GenericMath<T>.Add);
        }

        /// <summary>
        /// Constructs a signal interface that provides the difference of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the subtrahend</param>
        /// <param name="y">signal that provides the subtractor</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement subtraction.</exception>
        public static In<T> operator -(Signal<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex - ey, GenericMath<T>.Subtract);
        }

        /// <summary>
        /// Constructs a signal interface that provides the product of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first multiplicand</param>
        /// <param name="y">signal that provides the second multiplicand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement multiplication.</exception>
        public static In<T> operator *(Signal<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex * ey, GenericMath<T>.Multiply);
        }

        /// <summary>
        /// Constructs a signal interface that provides the quotient of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the dividend</param>
        /// <param name="y">signal that provides the divisor</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement division.</exception>
        public static In<T> operator /(Signal<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex / ey, GenericMath<T>.Divide);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise conjunction of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "and" operator.</exception>
        public static In<T> operator &(Signal<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex & ey, GenericMath<T>.And);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise disjunction of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "or" operator.</exception>
        public static In<T> operator |(Signal<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex | ey, GenericMath<T>.Or);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise antivalence of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "exclusive or" operator.</exception>
        public static In<T> operator ^(Signal<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex ^ ey, GenericMath<T>.Xor);
        }
    }

    /// <summary>
    /// This class represents a signal which transports a resolved data type.
    /// </summary>
    /// <remarks>
    /// A resolved data type defines a resolution rule which is applied when multiple drives write to the same signal.
    /// An example is four-valued logic which allows for modeling of tri-state logic. If one process "drives" the signal
    /// to 'Z' (high impedance) and another process drives it to '1', the resulting signal value gets resolved to '1'.
    /// </remarks>
    /// <typeparam name="T">The data type of the carried quantity. It must implement the IResolvable interface.</T></typeparam>
    public class ResolvedSignal<T> : Signal<T> where T : IResolvable<T>
    {
        private PLV<T> _nextValues = new PLV<T>();

        /// <summary>
        /// Constructs a resolved signal.
        /// </summary>
        public ResolvedSignal()
        {
        }

        public override T Next
        {
            /*internal get
            {
                if (_drivers.Contains(Context.CurrentProcess))
                    return (T)_nextValues.Value;
                else
                    return Cur;
            }*/
            set
            {
                if (DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis)
                    return;

                _nextValues.Value = value;
            }
        }

        protected override void OnNextDeltaCycle()
        {
            bool first = true;
            foreach (T value in _nextValues.Values)
            {
                if (first)
                {
                    _nextValue = value;
                    first = false;
                }
                else
                {
                    _nextValue = _nextValue.Resolve(value);
                }
            }
            _nextValues.Reset();

            base.OnNextDeltaCycle();
        }
    }

    /// <summary>
    /// This static class provides convenience methods for working with signals.
    /// </summary>
    public static class Signals
    {
        /// <summary>
        /// Creates a signal implementation for a given initial value.
        /// The factory method automatically selects the most suitable signal, i.e. an <c>SLSignal</c> if <paramref name="initialValue"/>
        /// is of type <c>StdLogic</c>, <c>SLVSignal</c> if <paramref name="initialValue"/> is of type <c>StdLogicVector</c>, <c>Signal1D</c>
        /// if <paramref name="initialValue"/> is an array or just <c>Signal</c> otherwise.
        /// </summary>
        /// <param name="initialValue">desired initial value for created signal</param>
        public static SignalBase CreateInstance(object initialValue)
        {
            TypeDescriptor elemType = TypeDescriptor.GetTypeOf(initialValue);
            SignalBase sinst;
            if (elemType.CILType.Equals(typeof(StdLogic)))
            {
                sinst = new SLSignal();
                sinst.InitialValueObject = initialValue;
            }
            else if (elemType.CILType.Equals(typeof(StdLogicVector)))
            {
                sinst = new SLVSignal(((StdLogicVector)initialValue).Size);
                sinst.InitialValueObject = initialValue;
            }
            else if (elemType.CILType.IsArray && elemType.CILType.GetArrayRank() == 1)
            {
                Array array = (Array)initialValue;
                Type subElemType = elemType.CILType.GetElementType();
                int length = array.GetLength(0);
                Array signalArray = Array.CreateInstance(typeof(Signal<>).MakeGenericType(subElemType), length);
                for (int i = 0; i < length; i++)
                {
                    signalArray.SetValue(CreateInstance(array.GetValue(i)), i);
                }
                sinst = (SignalBase)Activator.CreateInstance(
                    typeof(VSignal<>).MakeGenericType(subElemType), signalArray);
            }
            else if (elemType.CILType.IsArray)
            {
                throw new NotSupportedException("Signals of multi-dimensional arrays not supported");
            }
            else
            {
                sinst = (SignalBase)Activator.CreateInstance(typeof(Signal<>).MakeGenericType(elemType.CILType));
                sinst.InitialValueObject = initialValue;
            }
            return sinst;
        }
    }
}

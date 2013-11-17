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
using System.Linq;
using System.Text;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;
using System.Diagnostics.Contracts;

namespace SystemSharp.Components
{
    /// <summary>
    /// This is the general marker interface for ports.
    /// </summary>
    public interface IPort
    {
    }

    /// <summary>
    /// This interface models a port with inbound dataflow.
    /// </summary>
    public interface IInPort : IPort
    {
        /// <summary>
        /// Returns the event which is signaled when there is new data or when the data changed its value.
        /// </summary>        
        AbstractEvent ChangedEvent { [SignalProperty(SignalRef.EReferencedProperty.ChangedEvent)] get; }
    }

    /// <summary>
    /// This interface models a port with outbound dataflow.
    /// </summary>
    public interface IOutPort : IPort
    {
    }

    /// <summary>
    /// This interface models a port with bi-directional dataflow.
    /// </summary>
    public interface IInOutPort: IInPort, IOutPort
    {
    }

    /// <summary>
    /// This interface models a signal input port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    [MapToPort(EPortDirection.In)]
    [SignalArgument]
    [SignalField]
    public interface In<T>: IInPort
    {
        /// <summary>
        /// Returns the current signal value.
        /// </summary>        
        T Cur { [SignalProperty(SignalRef.EReferencedProperty.Cur)] get; }

        /// <summary>
        /// Returns the previous signal value (which the signal had in the last delta cycle).
        /// </summary>        
        T Pre { [SignalProperty(SignalRef.EReferencedProperty.Pre)] get; }

        Out<T> Dual { get; }
    }

    /// <summary>
    /// This interface models a signal output port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    [MapToPort(EPortDirection.Out)]
    [SignalArgument]
    [SignalField]
    public interface Out<T> : IOutPort
    {
        /// <summary>
        /// Writes the signal value.
        /// </summary>        
        T Next { [SignalProperty(SignalRef.EReferencedProperty.Next)] set; }

        In<T> Dual { [StaticEvaluationDoNotAnalyze] get; }
    }

    /// <summary>
    /// This interface models a bi-directional signal port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    [MapToPort(EPortDirection.InOut)]
    [SignalArgument]
    [SignalField]
    public interface InOut<T> : IInOutPort, In<T>, Out<T>
    {
    }

    /// <summary>
    /// Models objects which implement one-dimensional indexer properties, giving access a single elements or
    /// a sub-range of elements ("slicing").
    /// </summary>
    /// <typeparam name="T0">type of a single element</typeparam>
    /// <typeparam name="TA">type of a sub-range of elements</typeparam>
    public interface IIndexed<T0, TA>
    {   
        /// <summary>
        /// Returns the element at position <paramref name="index"/>.
        /// </summary>
        T0 this[int index] 
        {
            [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Index)]
            get; 
        }

        /// <summary>
        /// Returns a sub-range of elements, specified by <paramref name="index"/>.
        /// </summary>
        TA this[Range index] 
        {
            [MapToSlice]
            get; 
        }
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) input port.
    /// </summary>
    /// <typeparam name="TE">type of single data element</typeparam>
    /// <typeparam name="TI">type of sub-range of data elements</typeparam>
    [MapToPort(EPortDirection.In)]
    [SignalArgument]
    [SignalField]
    public interface XIn<TE, TI> : 
        In<TE>, 
        IIndexed<TI, XIn<TE, TI>>
    {
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) output port.
    /// </summary>
    /// <typeparam name="TE">type of single data element</typeparam>
    /// <typeparam name="TI">type of sub-range of data elements</typeparam>
    [MapToPort(EPortDirection.Out)]
    [SignalArgument]
    [SignalField]
    public interface XOut<TE, TI> : 
        Out<TE>, 
        IIndexed<TI, XOut<TE, TI>>
    { 
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) input/output port.
    /// </summary>
    /// <typeparam name="TE">type of single data element</typeparam>
    /// <typeparam name="TI">type of sub-range of data elements</typeparam>
    [MapToPort(EPortDirection.InOut)]
    [SignalArgument]
    [SignalField]
    public interface XInOut<TE, TI> : 
        XIn<TE, TI>, 
        XOut<TE, TI>,
        IIndexed<TI, XInOut<TE, TI>>
    {
    }

    /// <summary>
    /// A resolution interface for resolvable data types.
    /// </summary>
    /// <typeparam name="T">The resolvable data type</typeparam>
    public interface IResolvable<T>
    {
        /// <summary>
        /// Resolves two data instances.
        /// </summary>
        /// <param name="x">The first value</param>
        /// <param name="y">The second value</param>
        /// <returns>The resolution of the first and the second value</returns>
        T Resolve(T x, T y);
    }

    /// <summary>
    /// This is the abstract base class for channels.
    /// </summary>
    /// <remarks>
    /// A channel encapsulates communication whereas a component models computation. The methods of a channel might
    /// be called concurrently from multiple processes, so the implementor must cater for thread-safety.
    /// </remarks>
    public abstract class Channel : 
        DesignObject, 
        IDescriptive<ChannelDescriptor>,
        IContainmentImplementor
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public Channel()
        {
        }

        /// <summary>
        /// Creates a SysDOM descriptor for this kind of channel. You must implement this method in you concrete channel class.
        /// </summary>
        protected abstract ChannelDescriptor CreateDescriptor();

        public virtual void SetOwner(DescriptorBase owner, System.Reflection.MemberInfo declSite, IndexSpec indexSpec)
        {
            owner.AddChild(Descriptor, (System.Reflection.FieldInfo)declSite, indexSpec);
        }

        /// <summary>
        /// Returns the SysDOM descriptor describing this channel instance.
        /// </summary>
        private ChannelDescriptor _descriptor;
        public ChannelDescriptor Descriptor 
        {
            get
            {
                if (_descriptor == null)
                    _descriptor = CreateDescriptor();
                return _descriptor;
            }
        }

        /// <summary>
        /// Returns the SysDOM descriptor describing this channel instance.
        /// </summary>
        DescriptorBase IDescriptive.Descriptor
        {
            get { return Descriptor; }
        }
    }

    /// <summary>
    /// A complex channel is (similiar to SystemC) a channel which contains a component. The latter models
    /// some behavior which is implemented by this channel.
    /// </summary>
    public abstract class ComplexChannel : Channel
    {
        /// <summary>
        /// Creates the component which represents the channel's internal behavior.
        /// You must override this method in your concrete complex channel implementation class.
        /// </summary>
        protected abstract Component CreateInternalBehavior();

        /// <summary>
        /// Returns the component which represents the internal behavior of this channel.
        /// </summary>
        private Component _internalBehavior;
        public Component InternalBehavior
        {
            get { return _internalBehavior; }
        }

        private void SetupInternalBehavior()
        {
            _internalBehavior = CreateInternalBehavior();
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public ComplexChannel()
        {
            Context.OnEndOfConstruction += SetupInternalBehavior;
        }

        public override void SetOwner(DescriptorBase owner, System.Reflection.MemberInfo declSite, IndexSpec indexSpec)
        {
            base.SetOwner(owner, declSite, indexSpec);
            InternalBehavior.SetOwner(this.Descriptor, null, null);
        }
    }

    /// <summary>
    /// Interface definition for signals
    /// </summary>
    public interface ISignal
    {
        /// <summary>
        /// The changed event is triggered whenever the signal changes its value.
        /// </summary>
        AbstractEvent ChangedEvent { get; }

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

    /// <summary>
    /// This is the abstract base class of a signal.
    /// </summary>
    [SignalArgument]
    [SignalField]
    public abstract class SignalBase : 
        Channel,
        ISignal,
        IDescriptive<SignalDescriptor>,
        IDescriptive<ISignalDescriptor>
    {
        public SignalBase()
        {
            Context.RegisterSignal(this);
        }

        protected abstract SignalDescriptor CreateSignalDescriptor();

        /// <summary>
        /// Returns the event which is signaled whenever the signal changes its value.
        /// </summary>       
        public AbstractEvent ChangedEvent
        {
            [SignalProperty(SignalRef.EReferencedProperty.ChangedEvent)]
            get;
            protected set;
        }
        
        /// <summary>
        /// Returns the initial value of this signal (untyped).
        /// </summary>
        public abstract object InitialValueObject 
        { 
            [StaticEvaluation]
            get; 
            [AssumeNotCalled]
            set; 
        }

        /// <summary>
        /// Returns the previous value of this signal (untyped).
        /// </summary>
        public abstract object PreObject 
        {
            [SignalProperty(SignalRef.EReferencedProperty.Pre)]
            get; 
        }

        /// <summary>
        /// Returns the current value of this signal (untyped).
        /// </summary>
        public abstract object CurObject 
        {
            [SignalProperty(SignalRef.EReferencedProperty.Cur)]
            get; 
        }

        /// <summary>
        /// Write the signal value (untyped).
        /// </summary>
        public abstract object NextObject 
        {
            [SignalProperty(SignalRef.EReferencedProperty.Next)]
            set; 
        }

        protected sealed override ChannelDescriptor CreateDescriptor()
        {
            return CreateSignalDescriptor();
        }

        DescriptorBase IDescriptive.Descriptor
        {
            get { return Descriptor; }
        }

        public new SignalDescriptor Descriptor
        {
            get { return (SignalDescriptor)base.Descriptor; }
        }

        public abstract TypeDescriptor ElementType { get; }

        public override void SetOwner(DescriptorBase owner, System.Reflection.MemberInfo declSite, IndexSpec indexSpec)
        {
            base.SetOwner(owner, declSite, indexSpec);
            IPackageOrComponentDescriptor pcd = owner as IPackageOrComponentDescriptor;
            if (pcd != null && ElementType.Package != null)
                pcd.AddDependency(ElementType.Package);
        }

        public SignalRef ToSignalRef(SignalRef.EReferencedProperty prop)
        {
            return new SignalRef(Descriptor, prop);
        }

        [AssumeNotCalled]
        public virtual ISignal ApplyIndex(IndexSpec idx)
        {
            if (idx.Indices.Length != 0)
                throw new ArgumentException();
            return this;
        }

        ISignalDescriptor IDescriptive<ISignalDescriptor>.Descriptor
        {
            get { return Descriptor; }
        }
    }

    /// <summary>
    /// This class models a signal.
    /// </summary>
    /// <remarks>
    /// A signal is a mathematical quantity which changes its value at discrete time instants.
    /// </remarks>
    /// <typeparam name="T">The data type of the carried quantity.</typeparam>
    [MapToIntrinsicType(EIntrinsicTypes.Signal)]
    public class Signal<T> : SignalBase, InOut<T>, In<T>, Out<T>
    {
        /// <summary>
        /// Constructs a signal.
        /// </summary>
        public Signal()
        {
            Context.OnNextDeltaCycle += OnNextDeltaCycle;
            ChangedEvent = new Event(this);
        }

        /// <summary>
        /// Represents the signal's initial value.
        /// </summary>
        /// <remarks>
        /// At the beginning of the simulation, the signal is pre-initialized with the specified value.
        /// </remarks>
        public virtual T InitialValue
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

        public override object InitialValueObject
        {
            [StaticEvaluation]
            get { return InitialValue; }
            [AssumeNotCalled]
            set { InitialValue = (T)value; }
        }

        private T _initialValue;
        protected T _preValue;
        protected T _curValue;
        protected T _nextValue;

        /// <summary>
        /// Returns the current signal value.
        /// </summary>        
        public virtual T Cur
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
        public override object CurObject
        {
            get { return Cur; }
        }

        /// <summary>
        /// Write the signal value.
        /// </summary>        
        public virtual T Next
        {
            internal get
            {
                return _nextValue;
            }
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
        public override object NextObject
        {
            set  { Next = (T)value; }
        }

        /// <summary>
        /// Returns the previous signal value.
        /// </summary>        
        public virtual T Pre
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
        public override object PreObject
        {
            get { return Pre; }
        }

        In<T> Out<T>.Dual
        {
            get { return this; }
        }

        Out<T> In<T>.Dual
        {
            get { return this; }
        }

        public new Event ChangedEvent
        {
            get { return (Event)base.ChangedEvent; }
            set { base.ChangedEvent = value; }
        }

        protected virtual void OnNextDeltaCycle()
        {
            if (!_nextValue.Equals(_curValue))
                ChangedEvent.Fire();
            _preValue = _curValue;
            _curValue = _nextValue;
        }

        protected override SignalDescriptor CreateSignalDescriptor()
        {
            return new SignalDescriptor(this, ElementType);
        }

        public override TypeDescriptor ElementType
        {
            get 
            {
                return TypeDescriptor.MakeType(InitialValue, typeof(T));
            }
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
        private PLSSlot _nextValues;
        private HashSet<Process> _drivers = new HashSet<Process>();

        /// <summary>
        /// Constructs a resolved signal.
        /// </summary>
        public ResolvedSignal()
        {
            _nextValues = Context.AllocPLS();
        }

        public override T Next
        {
            internal get
            {
                if (_drivers.Contains(Context.CurrentProcess))
                    return (T)_nextValues.Value;
                else
                    return Cur;
            }
            set
            {
                if (DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis)
                    return;

                _nextValues.Value = value;
                _drivers.Add(Context.CurrentProcess);
            }
        }

        protected override void OnNextDeltaCycle()
        {
            bool first = true;
            foreach (Process driver in _drivers)
            {
                if (first)
                {
                    _nextValue = (T)_nextValues[driver];
                    first = false;
                }
                else
                {
                    _nextValue = _nextValue.Resolve(_nextValue, (T)_nextValues[driver]);
                }
            }
            _drivers.Clear();

            base.OnNextDeltaCycle();
        }
    }

    public static class Signals
    {
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
                    typeof(Signal1D<>).MakeGenericType(subElemType), signalArray);
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

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
using System.Linq;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.Meta;
using SystemSharp.DataTypes;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// The proxy represents the application of a sub-range to an underlying <c>Signal1D</c>. I.e. the proxy does not
    /// manage the signal data by itself but instead acts as an adapter between the actual signal and the view on that signal
    /// which results from applying the sub-range.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class Signal1DProxy<T> :
        XInOut<T[], InOut<T>>,
        ISignal
    {
        private Signal1D<T> _ref;
        private Range _range;
        private MultiEvent _changedEvent;

        private IEnumerable<AbstractEvent> GetRefEvents()
        {
            foreach (int i in _range.Values)
                yield return _ref[i].ChangedEvent;
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="reference">underlying signal instance</param>
        /// <param name="range">sub-range to apply</param>
        public Signal1DProxy(Signal1D<T> reference, Range range)
        {
            _ref = reference;
            _range = range;
            _changedEvent = new MultiEvent(reference, GetRefEvents());
        }

        public T[] Cur
        {
            [SignalProperty(SignalRef.EReferencedProperty.Cur)]
            get { return _ref.Cur.Slice(_range); }
        }

        public T[] Pre
        {
            [SignalProperty(SignalRef.EReferencedProperty.Pre)]
            get { return _ref.Pre.Slice(_range); }
        }

        public Out<T[]> Dual
        {
            get { return this; }
        }
        
        public AbstractEvent ChangedEvent
        {
            [SignalProperty(SignalRef.EReferencedProperty.ChangedEvent)]
            get { return _changedEvent; }
        }

        public InOut<T> this[int index]
        {
            [SignalIndexer]
            get { return _ref[_range.Unproject(index)]; }
        }

        public XInOut<T[], InOut<T>> this[Range index]
        {
            [SignalIndexer]
            get { return new Signal1DProxy<T>(_ref, _range.Unproject(index)); }
        }

        public T[] Next
        {
            [SignalProperty(SignalRef.EReferencedProperty.Next)]
            set 
            { 
                foreach (int i in _range.Values)
                    _ref[i].Next = value[_range.Project(i)];
            }
        }

        In<T[]> Out<T[]>.Dual
        {
            get { return this; }
        }

        XIn<T[], InOut<T>> IIndexed<InOut<T>, XIn<T[], InOut<T>>>.this[Range index]
        {
            [SignalIndexer]
            get { return this[index]; }
        }

        XOut<T[], InOut<T>> IIndexed<InOut<T>, XOut<T[], InOut<T>>>.this[Range index]
        {
            [SignalIndexer]
            get { return this[index]; }
        }


        public object InitialValueObject
        {
            get { return _ref.InitialValue.Slice(_range); }
        }

        public object PreObject
        {
            get { return Pre; }
        }

        public object CurObject
        {
            get { return Cur; }
        }

        public object NextObject
        {
            set { Next = (T[])value; }
        }

        public TypeDescriptor ElementType
        {
            get { return TypeDescriptor.GetTypeOf(InitialValueObject); }
        }

        public SignalRef ToSignalRef(SignalRef.EReferencedProperty prop)
        {
            var index = new IndexSpec((DimSpec)_range);
            return new SignalRef(_ref.Descriptor, prop,
                index.AsExpressions(), index, true);
        }
    }

    /// <summary>
    /// Models a signal which carries one-dimensional signal data.
    /// It provides indexing properties to apply an index or sub-range.
    /// </summary>
    /// <typeparam name="T">type of a single element of the one-dimensional data</typeparam>
    public class Signal1D<T> :
        SignalBase,
        IContainmentImplementor,
        XInOut<T[], InOut<T>>
    {
        /// <summary>
        /// Creates a carrier signal for a single data element.
        /// </summary>
        /// <param name="index">index of element</param>
        /// <returns>the underlying carrier signal</returns>
        public delegate Signal<T> CreateFunc(int index);

        private Signal<T>[] _signals;

        /// <summary>
        /// Constructs an instance based on a sequence of underlying carrier signals, i.e. one signal for
        /// each data element.
        /// </summary>
        public Signal1D(IEnumerable<Signal<T>> signals)
        {
            _signals = signals.ToArray();
            Initialize();
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="dim">element count (length) of represented one-dimensional data</param>
        /// <param name="creator">signal creation function</param>
        public Signal1D(int dim, CreateFunc creator)
        {
            _signals = new Signal<T>[dim];
            for (int i = 0; i < dim; i++)
            {
                _signals[i] = creator(i);
            }
            Initialize();
        }

        private void Initialize()
        {
            if (!_signals.All(s => s.ElementType.Equals(_signals[0].ElementType)))
                throw new ArgumentException("Signals inside this container must all have the same types and dimensions");

            _elementType = TypeDescriptor.MakeType(
                _signals.Select(sig => sig.InitialValue).ToArray(),
                typeof(T[]));

            for (int i = 0; i < _signals.Length; i++)
            {
                _signals[i].SetOwner(Descriptor, null, new IndexSpec(i));
            }

            ChangedEvent = new MultiEvent(this, _signals.Select(s => s.ChangedEvent));
        }
        
        /// <summary>
        /// Constructs a new view on this signal which results from applying <paramref name="index"/> to it.
        /// </summary>
        public InOut<T> this[int index]
        {
            [SignalIndexer]
            get { return _signals[index]; }
        }

        /// <summary>
        /// Constructs a new view on this signal which results from applying <paramref name="index"/> to it.
        /// </summary>
        public InOut<T> this[Unsigned index]
        {
            [SignalIndexer]
            get { return _signals[index.IntValue]; }
        }

        internal object GetIndexerSample(int index)
        {
            return _signals.Length > 0 ? _signals[0] : null;
        }

        /// <summary>
        /// Constructs a new view on this signal which results from applying sub-range <paramref name="range"/> to it.
        /// </summary>
        public XInOut<T[], InOut<T>> this[Range index]
        {
            [SignalIndexer]
            get
            {
                if (index.Direction != EDimDirection.To)
                    throw new NotImplementedException("Only up-range projections are supported");

                if (Context.State == DesignContext.ESimState.Construction ||
                    Context.State == DesignContext.ESimState.Elaboration)
                {
                    return new Signal1D<T>(_signals.Skip((int)index.FirstBound).Take((int)index.Size));
                }
                else
                {
                    return new Signal1DProxy<T>(this, index);
                }
            }
        }

        internal object GetIndexerSample(Range index)
        {
            int length = index.SecondBound - index.FirstBound + 1;
            if (length >= 0 && length <= _signals.Length)
                return new Signal1DProxy<T>(this,
                    index.Direction == EDimDirection.Downto ?
                        new Range(length - 1, 0, EDimDirection.Downto) :
                        new Range(0, length - 1, EDimDirection.To));
            else
                return null;
        }

        [AssumeNotCalled]
        public override ISignal ApplyIndex(IndexSpec idx)
        {
            if (idx.Indices.Length == 0)
            {
                return this;
            }
            else if (idx.Indices.Length == 1)
            {
                DimSpec idx0 = idx.Indices[0];
                switch (idx0.Kind)
                {
                    case DimSpec.EKind.Index:
                        return (ISignal)this[(int)idx0];

                    case DimSpec.EKind.Range:
                        return (ISignal)this[(Range)idx0];

                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public T[] Cur
        {
            [SignalProperty(SignalRef.EReferencedProperty.Cur)]
            get { return _signals.Select(s => s.Cur).ToArray(); }
        }

        public T[] Pre
        {
            [SignalProperty(SignalRef.EReferencedProperty.Pre)]
            get { return _signals.Select(s => s.Pre).ToArray(); }
        }

        public Out<T[]> Dual
        {
            get { return this; }
        }

        private void CheckValue(T[] value)
        {
            if (value == null)
                throw new ArgumentException("value must not be null");

            if (value.LongLength != _signals.LongLength)
                throw new ArgumentException("value has invalid dimensions");
        }

        public T[] Next
        {
            [SignalProperty(SignalRef.EReferencedProperty.Next)]
            set
            {
                CheckValue(value);
                for (long i = 0; i < _signals.LongLength; i++)
                    _signals[i].Next = value[i];
            }
        }

        public T[] InitialValue
        {
            get
            {
                return _signals.Select(s => s.InitialValue).ToArray();
            }
            set
            {
                CheckValue(value);
                for (long i = 0; i < _signals.LongLength; i++)
                    _signals[i].InitialValue = value[i];
            }
        }

        public override object InitialValueObject
        {
            get { return InitialValue; }
            set { InitialValue = (T[])value; }
        }

        In<T[]> Out<T[]>.Dual
        {
            get { return this; }
        }

        TypeDescriptor _elementType;

        public override TypeDescriptor ElementType { get { return _elementType; } }

        public override object PreObject
        {
            get { return Pre; }
        }

        public override object CurObject
        {
            get { return Cur; }
        }

        public override object NextObject
        {
            set { Next = (T[])value; }
        }

        void IContainmentImplementor.SetOwner(
            DescriptorBase owner, 
            System.Reflection.MemberInfo declSite,
            IndexSpec indexSpec)
        {
            base.SetOwner(owner, declSite, IndexSpec.Empty);
        }

        protected override SignalDescriptor CreateSignalDescriptor()
        {
            return new SignalDescriptor(this, ElementType);
        }

        XIn<T[], InOut<T>> IIndexed<InOut<T>, XIn<T[], InOut<T>>>.this[Range index]
        {
            [SignalIndexer]
            get { return this[index]; }
        }


        XOut<T[], InOut<T>> IIndexed<InOut<T>, XOut<T[], InOut<T>>>.this[Range index]
        {
            [SignalIndexer]
            get { return this[index]; }
        }
    }

#if false
    public class Signal2D<T> :
        XInOut<T[,], XInOut<T[], InOut<T>>>
    {
        public delegate Signal<T> CreateFunc(long i0, long i1);

        private Signal<T>[,] _signals;
        private Signal1D<T>[] _rows;
        private MultiEvent _changedEvent;

        public Signal2D(long dim0, long dim1, CreateFunc creator)
        {
            _signals = new Signal<T>[dim0, dim1];
            _rows = new Signal1D<T>[dim0];
            for (long i = 0; i < dim0; i++)
            {
                for (long j = 0; j < dim1; j++)
                {
                    _signals[i,j] = creator(i,j);
                }
                _rows[i] = new Signal1D<T>(dim1, j => _signals[i, j]);
            }
            _changedEvent = new MultiEvent(_rows.Select(r => r.ChangedEvent));
        }

        public T[,] Cur
        {
            get 
            {
                long dim0 = _signals.GetLongLength(0);
                long dim1 = _signals.GetLongLength(1);
                T[,] result = new T[dim0, dim1];
                for (long i = 0; i < dim0; i++)
                {
                    for (long j = 0; j < dim1; j++)
                    {
                        result[i, j] = _signals[i, j].Cur;
                    }
                }
                return result;
            }
        }

        public T[,] Pre
        {
            get
            {
                long dim0 = _signals.GetLongLength(0);
                long dim1 = _signals.GetLongLength(1);
                T[,] result = new T[dim0, dim1];
                for (long i = 0; i < dim0; i++)
                {
                    for (long j = 0; j < dim1; j++)
                    {
                        result[i, j] = _signals[i, j].Pre;
                    }
                }
                return result;
            }
        }

        public Out<T[,]> Dual
        {
            get { return this; }
        }

        public AbstractEvent ChangedEvent
        {
            get { return _changedEvent; }
        }

        public XInOut<T[], InOut<T>> this[long i0]
        {
            get 
            {
                return _rows[i0];
            }
        }

        public T[,] Next
        {
            set 
            {
                long dim0 = _signals.GetLongLength(0);
                long dim1 = _signals.GetLongLength(1);
                for (long i = 0; i < dim0; i++)
                {
                    for (long j = 0; j < dim1; j++)
                    {
                        _signals[i, j].Next = value[i, j];
                    }
                }
            }
        }

        In<T[,]> Out<T[,]>.Dual
        {
            get { return this; }
        }
    }
#endif
}

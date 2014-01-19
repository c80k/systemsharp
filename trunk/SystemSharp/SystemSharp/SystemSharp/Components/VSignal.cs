/**
 * Copyright 2011-2014 Christian Köllner
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
using SystemSharp.Collections;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// Models a signal which carries one-dimensional signal data.
    /// It provides indexing properties to apply an index or sub-range.
    /// </summary>
    /// <typeparam name="T">type of a single element of the one-dimensional data</typeparam>
    public class VSignal<T> : 
        SignalBase,
        IVInOut<T>
    {
        #region private types

        private class InOutIndexer : InOut<T>
        {
            private VSignal<T> _vsignal;
            private int _index;

            public InOutIndexer(VSignal<T> vsignal, int index)
            {
                Contract.Requires<ArgumentNullException>(vsignal != null, "vsignal");
                Contract.Requires<ArgumentOutOfRangeException>(index >= 0, "index is less than 0.");
                Contract.Requires<ArgumentOutOfRangeException>(index < vsignal.Size, 
                    string.Format("index value of {0} exceeds signal size of {1}.", index, vsignal.Size));

                _vsignal = vsignal;
                _index = index;
            }

            public override T Cur
            {
                get { return _vsignal._signals[_index].Cur; }
            }

            public override T Pre
            {
                get { return _vsignal._signals[_index].Pre; }
            }

            public override T Next
            {
                set { _vsignal._signals[_index].Next = value; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class InOutRangeIndexer : VInOut<T>
        {
            private VSignal<T> _vsignal;
            private Range _projRange;

            public InOutRangeIndexer(VSignal<T> vsignal, Range projRange)
            {
                _vsignal = vsignal;
                _projRange = projRange;
            }

            public override Vector<T> Cur
            {
                get { return _projRange.Values.Select(_ => _vsignal._signals[_].Cur).ToArray().AsVector(); }
            }

            public override Vector<T> Pre
            {
                get { return _projRange.Values.Select(_ => _vsignal._signals[_].Pre).ToArray().AsVector(); }
            }

            public override Vector<T> Next
            {
                set 
                { 
                    Contract.Requires<ArgumentNullException>(value != null, "value");
                    Contract.Requires<ArgumentException>(value.Size == _projRange.Size, 
                        string.Format("Expected vector of length {0}, but supplied vector has length {1}.", _projRange.Size, value.Size));

                    int i = 0;
                    foreach (int j in _projRange.Values)
                        _vsignal._signals[j].Next = value[i++];
                }
            }

            public override InOut<T> this[int i]
            {
                get { return new InOutIndexer(_vsignal, _projRange.Unproject(i)); }
            }

            public override VInOut<T> this[Range r]
            {
                get { return new InOutRangeIndexer(_vsignal, _projRange.Unproject(r)); }
            }

            public override int Size
            {
                get { return (int)_projRange.Size; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class InOutProxy : VInOut<T>
        {
            private VSignal<T> _vsignal;

            public InOutProxy(VSignal<T> vsignal)
            {
                _vsignal = vsignal;
            }

            public override Vector<T> Cur
            {
                get { return _vsignal.Cur; }
            }

            public override Vector<T> Pre
            {
                get { return _vsignal.Pre; }
            }

            public override Vector<T> Next
            {
                set { _vsignal.Next = value; }
            }

            public override InOut<T> this[int i]
            {
                get { return new InOutIndexer(_vsignal, i); }
            }

            public override VInOut<T> this[Range r]
            {
                get { return new InOutRangeIndexer(_vsignal, r); }
            }

            public override int Size
            {
                get { return _vsignal.Size; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        #endregion private types

        private Signal<T>[] _signals;
        private EventSource _changedEvent;

        /// <summary>
        /// Constructs a vector-valued signal.
        /// </summary>
        /// <param name="size">desired vector size</param>
        /// <param name="initialValue">initial signal value per element</param>
        public VSignal(int size, T initialValue)
        {
            _signals = new Signal<T>[size];
            for (int i = 0; i < size; i++)
                _signals[i] = (Signal<T>)Signals.CreateInstance(initialValue);

            Initialize();
        }

        private void Initialize()
        {
            if (!_signals.All(s => TypeDescriptor.GetTypeOf(s.InitialValue).Equals(TypeDescriptor.GetTypeOf(_signals[0].InitialValue))))
                throw new ArgumentException("Signals inside this container must all have the same types and dimensions");

            _changedEvent = new MultiEvent(this, _signals.Select(s => s.ChangedEvent));
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

        protected override SignalDescriptor CreateSignalDescriptor()
        {
            var desc = new SignalDescriptor(this);
            for (int i = 0; i < _signals.Length; i++)
            {
                _signals[i].Descriptor.Nest(desc, new IndexSpec(i));
            }
            return desc;
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public Vector<T> InitialValue
        {
            get { return _signals.Select(_ => _.InitialValue).ToArray().AsVector(); }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "value");
                Contract.Requires<ArgumentException>(_signals.Length == value.Size, "Vector must have exactly same size like this vector signal.");

                for (int i = 0; i < _signals.Length; i++)
                    _signals[i].InitialValue = value[i];
            }
        }

        object SignalBase.InitialValue
        {
            get { return InitialValue; }
            set { InitialValue = (Vector<T>)value; }
        }

        public Vector<T> Pre
        {
            get { return _signals.Select(_ => _.Pre).ToArray().AsVector(); }
        }

        object SignalBase.Pre
        {
            get { return Pre; }
        }

        public Vector<T> Cur
        {
            get { return _signals.Select(_ => _.Cur).ToArray().AsVector(); }
        }

        object SignalBase.Cur
        {
            get { return Cur; }
        }

        public Vector<T> Next
        {
            set
            {
                Contract.Requires<ArgumentNullException>(value != null);
                Contract.Requires<ArgumentException>(value.Size == _signals.Length, "Size of assigned vector does not match signal size.");
                for (int i = 0; i < value.Size; i++)
                    _signals[i].Next = value[i];
            }
        }

        object SignalBase.Next
        {
            set { Next = (Vector<T>)value; }
        }

        InOut<T> IIndexable<InOut<T>>.this[int i]
        {
            get { return _signals[i]; }
        }

        public IIndexable<Signal<T>> this[Range r]
        {
            get { throw new NotImplementedException(); }
        }

        public int Size
        {
            get { return _signals.Length; }
        }
    }
}

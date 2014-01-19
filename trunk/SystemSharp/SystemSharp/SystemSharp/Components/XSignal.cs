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
    /// Models a signal which carries two-dimensional signal data.
    /// It provides indexing properties to apply an index or sub-area.
    /// </summary>
    /// <typeparam name="T">type of a single element of the two-dimensional data</typeparam>
    public class XSignal<T> :
        SignalBase,
        IXInOut<T>
    {
        #region private types

        private class InOutIndexer : XInOut<T>
        {
            private Box<Signal<T>> _target;

            public InOutIndexer(Box<Signal<T>> target)
            {
                Contract.Requires<ArgumentNullException>(target != null, "target");

                _target = target;
            }

            public override Box<T> Cur
            {
                get { return _target.Select(s => s.Cur); }
            }

            public override Box<T> Pre
            {
                get { return _target.Select(s => s.Pre); }
            }

            public override Box<T> Next
            {
                set 
                {
                    Contract.Requires<ArgumentNullException>(value != null, "value");
                    Contract.Requires<ArgumentException>(value.Sizes == Sizes, "value has wrong dimensionality");

                    foreach (var index in Box.EnumerateFromLoToHi(_target.Sizes.ToArray()))
                        _target.ElementAt(index).Next = value.ElementAt(index);
                }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        #endregion private types

        private Box<Signal<T>> _signals;
        private EventSource _changedEvent;

        /// <summary>
        /// Constructs a box-valued signal.
        /// </summary>
        /// <param name="initialValue">initial signal value per element</param>
        /// <param name="sizes">desired box lengths</param>
        public XSignal(T initialValue, params int[] sizes)
        {
            _signals = Box.Create(_ => new Signal<T>() { InitialValue = initialValue }, sizes);
            _changedEvent = new MultiEvent(this, _signals.Select(_ => _.ChangedEvent).Enumerate());
        }

        protected override SignalDescriptor CreateSignalDescriptor()
        {
            var desc = new SignalDescriptor(this);
            foreach (var index in Box.EnumerateFromLoToHi(_signals.Sizes.ToArray()))
            {
                _signals.ElementAt(index).Descriptor.Nest(desc, new IndexSpec(index.Select(i => (DimSpec)i)));
            }
            return desc;
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public Box<T> InitialValue
        {
            get
            {
                return _signals.Select(_ => _.InitialValue);
            }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "value");
                Contract.Requires<ArgumentException>(Sizes == value.Sizes, "Assigned value must have same dimensions as signal.");

                foreach (var index in Box.EnumerateFromLoToHi(_signals.Sizes.ToArray()))
                    _signals.ElementAt(index).InitialValue = value.ElementAt(index);
            }
        }

        object SignalBase.InitialValue
        {
            get { return InitialValue; }
            set { InitialValue = (Box<T>)value; }
        }

        public Box<T> Pre
        {
            get { return _signals.Select(_ => _.Pre); }
        }

        object SignalBase.Pre
        {
            get { return Pre; }
        }

        public Box<T> Cur
        {
            get { return _signals.Select(_ => _.Cur); }
        }

        object SignalBase.Cur
        {
            get { return Cur; }
        }

        public Box<T> Next
        {
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "value");
                Contract.Requires<ArgumentException>(Sizes == value.Sizes, "Assigned value must have same dimensions as signal.");

                foreach (var index in Box.EnumerateFromLoToHi(_signals.Sizes.ToArray()))
                    _signals.ElementAt(index).Next = value.ElementAt(index);
            }
        }

        object SignalBase.Next
        {
            set { Next = (Box<T>)value; }
        }

        public Vector<int> Sizes
        {
            get { return _signals.Sizes; }
        }

        public new XInOut<T> this[params DimSpec[] indices]
        {
            get { return new InOutIndexer(_signals[indices]); }
        }

        public InOut<T> GetElement()
        {
            return _signals.GetElement();
        }

        public Expression DescribingExpression
        {
            get { throw new NotImplementedException(); }
        }

        public static implicit operator XInOut<T>(XSignal<T> signal)
        {
            return new InOutIndexer(signal._signals);
        }

    }
}

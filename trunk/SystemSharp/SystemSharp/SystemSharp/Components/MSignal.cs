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
    public class MSignal<T> :
        SignalBase,
        IVInOut<T>
    {
        #region private types

        private class InitialValueMatrix : Matrix<T>
        {
            private MSignal<T> _signal;

            public InitialValueMatrix(MSignal<T> signal)
            {
                _signal = signal;
            }

            public override int Size0
            {
                get { return _signal.Size0; }
            }

            public override int Size1
            {
                get { return _signal.Size1; }
            }

            public override T this[int i, int j]
            {
                get { return _signal._signals[i, j].InitialValue; }
            }
        }

        private class InOutIndexer : InOut<T>
        {
            private MSignal<T> _MSignal;
            private int _index0;
            private int _index1;

            public InOutIndexer(MSignal<T> MSignal, int index0, int index1)
            {
                Contract.Requires<ArgumentNullException>(MSignal != null, "MSignal");
                Contract.Requires<ArgumentOutOfRangeException>(index0 >= 0, "index0 is less than 0.");
                Contract.Requires<ArgumentOutOfRangeException>(index0 < MSignal.Size0,
                    string.Format("index0 value of {0} exceeds signal height of {1}.", index0, MSignal.Size0));
                Contract.Requires<ArgumentOutOfRangeException>(index1 >= 0, "index1 is less than 0.");
                Contract.Requires<ArgumentOutOfRangeException>(index1 < MSignal.Size1,
                    string.Format("index value of {0} exceeds signal height of {1}.", index1, MSignal.Size1));

                _MSignal = MSignal;
                _index0 = index0;
                _index1 = index1;
            }

            public override T Cur
            {
                get { return _MSignal._signals[_index0, _index1].Cur; }
            }

            public override T Pre
            {
                get { return _MSignal._signals[_index0, _index1].Pre; }
            }

            public override T Next
            {
                set { _MSignal._signals[_index0, _index1].Next = value; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class InOutRowIndexer : VInOut<T>
        {
            private MSignal<T> _MSignal;
            private int _row;
            private Range _colRange;

            public InOutRowIndexer(MSignal<T> MSignal, int row, Range colRange)
            {
                _MSignal = MSignal;
                _row = row;
                _colRange = colRange;
            }

            public override Vector<T> Cur
            {
                get { return _colRange.Values.Select(_ => _MSignal._signals[_row, _].Cur).ToArray().AsVector(); }
            }

            public override Vector<T> Pre
            {
                get { return _colRange.Values.Select(_ => _MSignal._signals[_row, _].Pre).ToArray().AsVector(); }
            }

            public override Vector<T> Next
            {
                set
                {
                    Contract.Requires<ArgumentNullException>(value != null, "value");
                    Contract.Requires<ArgumentException>(value.Size == _colRange.Size,
                        string.Format("Expected vector of length {0}, but supplied vector has length {1}.", _colRange.Size, value.Size));

                    int i = 0;
                    foreach (int j in _colRange.Values)
                        _MSignal._signals[_row, j].Next = value[i++];
                }
            }

            public override InOut<T> this[int i]
            {
                get { return new InOutIndexer(_MSignal, _row, _colRange.Unproject(i)); }
            }

            public override VInOut<T> this[Range r]
            {
                get { return new InOutRowIndexer(_MSignal, _row, _colRange.Unproject(r)); }
            }

            public override int Size
            {
                get { return (int)_colRange.Size; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class InOutColumnIndexer : VInOut<T>
        {
            private MSignal<T> _MSignal;
            private Range _rowRange;
            private int _column;

            public InOutColumnIndexer(MSignal<T> MSignal, Range rowRange, int column)
            {
                _MSignal = MSignal;
                _rowRange = rowRange;
                _column = column;
            }

            public override Vector<T> Cur
            {
                get { return _rowRange.Values.Select(_ => _MSignal._signals[_, _column].Cur).ToArray().AsVector(); }
            }

            public override Vector<T> Pre
            {
                get { return _rowRange.Values.Select(_ => _MSignal._signals[_, _column].Pre).ToArray().AsVector(); }
            }

            public override Vector<T> Next
            {
                set
                {
                    Contract.Requires<ArgumentNullException>(value != null, "value");
                    Contract.Requires<ArgumentException>(value.Size == _rowRange.Size,
                        string.Format("Expected vector of length {0}, but supplied vector has length {1}.", _rowRange.Size, value.Size));

                    int i = 0;
                    foreach (int j in _rowRange.Values)
                        _MSignal._signals[j, _column].Next = value[i++];
                }
            }

            public override InOut<T> this[int i]
            {
                get { return new InOutIndexer(_MSignal, _rowRange.Unproject(i), _column); }
            }

            public override VInOut<T> this[Range r]
            {
                get { return new InOutColumnIndexer(_MSignal, _rowRange.Unproject(r), _column); }
            }

            public override int Size
            {
                get { return (int)_rowRange.Size; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class InOutAreaIndexer : MInOut<T>
        {
            private MSignal<T> _MSignal;
            private Range _rowRange;
            private Range _colRange;

            public InOutAreaIndexer(MSignal<T> MSignal, Range rowRange, Range colRange)
            {
                _MSignal = MSignal;
                _rowRange = rowRange;
                _colRange = colRange;
            }

            public override Matrix<T> Cur
            {
                get { return _MSignal._signals[_rowRange, _colRange].Select(_ => _.Cur); }
            }

            public override Matrix<T> Pre
            {
                get { return _MSignal._signals[_rowRange, _colRange].Select(_ => _.Pre); }
            }

            public override Matrix<T> Next
            {
                set
                {
                    Contract.Requires<ArgumentNullException>(value != null, "value");
                    Contract.Requires<ArgumentException>(value.Size0 == _rowRange.Size,
                        string.Format("Expected matrix of height {0}, but supplied matrix has height {1}.", _rowRange.Size, value.Size0));
                    Contract.Requires<ArgumentException>(value.Size1 == _colRange.Size,
                        string.Format("Expected matrix of height {0}, but supplied matrix has height {1}.", _rowRange.Size, value.Size0));

                    int i0 = 0;
                    foreach (int i1 in _rowRange.Values)
                    {
                        int j0 = 0;
                        foreach (int j1 in _colRange.Values)
                        {
                            _MSignal._signals[i1, j1].Next = value[i0, j0];
                            ++j0;
                        }
                        ++i0;
                    }
                }
            }

            public override InOut<T> this[int i, int j]
            {
                get { return new InOutIndexer(_MSignal, _rowRange.Unproject(i), _colRange.Unproject(j)); }
            }

            public override VInOut<T> this[int i, Range rj]
            {
                get { return new InOutRowIndexer(_MSignal, _rowRange.Unproject(i), _colRange.Unproject(rj)); }
            }

            public override VInOut<T> this[Range ri, int j]
            {
                get { return new InOutColumnIndexer(_MSignal, _rowRange.Unproject(ri), _colRange.Unproject(j)); }
            }

            public override MInOut<T> this[Range ri, Range rj]
            {
                get { return new InOutAreaIndexer(_MSignal, _rowRange.Unproject(ri), _colRange.Unproject(rj)); }
            }

            public override int Size
            {
                get { return (int)_rowRange.Size; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class InOutProxy : MInOut<T>
        {
            private MSignal<T> _MSignal;

            public InOutProxy(MSignal<T> MSignal)
            {
                _MSignal = MSignal;
            }

            public override Matrix<T> Cur
            {
                get { return _MSignal.Cur; }
            }

            public override Matrix<T> Pre
            {
                get { return _MSignal.Pre; }
            }

            public override Matrix<T> Next
            {
                set { _MSignal.Next = value; }
            }

            public override InOut<T> this[int i, int j]
            {
                get { return new InOutIndexer(_MSignal, i, j); }
            }

            public override VInOut<T> this[int i, Range rj]
            {
                get { return new InOutRowIndexer(_MSignal, i, rj); }
            }

            public override VInOut<T> this[Range ri, int j]
            {
                get { return new InOutColumnIndexer(_MSignal, ri, j); }
            }

            public override MInOut<T> this[Range ri, Range rj]
            {
                get { return new InOutAreaIndexer(_MSignal, ri, rj); }
            }

            public override int Size0
            {
                get { return _MSignal.Size0; }
            }

            public override int Size1
            {
                get { return _MSignal.Size1; }
            }

            public override Expression DescribingExpression
            {
                get { throw new NotImplementedException(); }
            }
        }

        #endregion private types

        private Matrix<Signal<T>> _signals;
        private EventSource _changedEvent;

        /// <summary>
        /// Constructs a matrix-valued signal.
        /// </summary>
        /// <param name="size1">desired matrix height</param>
        /// <param name="size0">desired matrix width</param>
        /// <param name="initialValue">initial signal value per element</param>
        public MSignal(int size0, int size1, T initialValue)
        {
            var signals = new Signal<T>[size0, size1];
            for (int i = 0; i < size0; i++)
                for (int j = 0; j < size1; i++)
                    signals[i, j] = (Signal<T>)Signals.CreateInstance(initialValue);
            _signals = Matrix.AsMatrix(signals);

            Initialize();
        }

        private void Initialize()
        {
            if (_signals.TotalSize > 0)
            {
                var initValueType = TypeDescriptor.GetTypeOf(_signals[0, 0].InitialValue);
                int size0 = Size0;
                int size1 = Size1;
                for (int i = 0; i < size0; i++)
                {
                    for (int j = 0; j < size1; j++)
                    {
                        if (!TypeDescriptor.GetTypeOf(_signals[i, j].InitialValue).Equals(initValueType))
                            throw new ArgumentException("Signals inside this container must all have the same types and dimensions");
                    }
                }
            }
            _changedEvent = new MultiEvent(this, _signals.Select(_ => _.ChangedEvent).Serialize());
        }

        /// <summary>
        /// Constructs a new view on this signal which results from applying <paramref name="index"/> to it.
        /// </summary>
        public InOut<T> this[int index0, int index1]
        {
            [SignalIndexer]
            get { return _signals[index0, index1]; }
        }

        /// <summary>
        /// Constructs a new view on this signal which results from applying <paramref name="index"/> to it.
        /// </summary>
        public InOut<T> this[Unsigned index0, Unsigned index1]
        {
            [SignalIndexer]
            get { return _signals[index0.IntValue, index1.IntValue]; }
        }

        protected override SignalDescriptor CreateSignalDescriptor()
        {
            var desc = new SignalDescriptor(this);
            for (int i = 0; i < Size0; i++)
            {
                for (int j = 0; j < Size1; j++)
                {
                    _signals[i, j].Descriptor.Nest(desc, new IndexSpec(i, j));
                }
            }
            return desc;
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public Matrix<T> InitialValue
        {
            get 
            {
                return _signals.Select(_ => _.InitialValue);
            }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "value");
                Contract.Requires<ArgumentException>(Size0 == value.Size0, "Assigned value must have same height as signal.");
                Contract.Requires<ArgumentException>(Size1 == value.Size1, "Assigned value must have same width as signal.");

                for (int i = 0; i < Size0; i++)
                    for (int j = 0; j < Size1; j++)
                        _signals[i, j].InitialValue = value[i, j];
            }
        }

        object SignalBase.InitialValue
        {
            get { return InitialValue; }
            set { InitialValue = (Matrix<T>)value; }
        }

        public Matrix<T> Pre
        {
            get { return _signals.Select(_ => _.Pre); }
        }

        object SignalBase.Pre
        {
            get { return Pre; }
        }

        public Matrix<T> Cur
        {
            get { return _signals.Select(_ => _.Cur); }
        }

        object SignalBase.Cur
        {
            get { return Cur; }
        }

        public Matrix<T> Next
        {
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "value");
                Contract.Requires<ArgumentException>(Size0 == value.Size0, "Assigned value must have same height as signal.");
                Contract.Requires<ArgumentException>(Size1 == value.Size1, "Assigned value must have same width as signal.");

                for (int i = 0; i < Size0; i++)
                    for (int j = 0; j < Size1; j++)
                        _signals[i, j].Next = value[i, j];
            }
        }

        object SignalBase.Next
        {
            set { Next = (Matrix<T>)value; }
        }

        InOut<T> IMatrixIndexable<InOut<T>>.this[int i, int j]
        {
            get { return _signals[i, j]; }
        }

        public InOut<T> this[Unsigned i, Unsigned j]
        {
            get { return this[i.IntValue, j.IntValue]; }
        }

        public VInOut<T> this[int i, Range rj]
        {
            get { return new InOutRowIndexer(this, i, rj); }
        }

        public VInOut<T> this[Range ri, int j]
        {
            get { return new InOutColumnIndexer(this, ri, j); }
        }

        public MInOut<T> this[Range ri, Range rj]
        {
            get { return new InOutAreaIndexer(this, ri, rj); }
        }

        public int Size0
        {
            get { return _signals.Size0; }
        }

        public int Size1
        {
            get { return _signals.Size1; }
        }

        public static implicit operator MInOut<T>(MSignal<T> signal)
        {
            return new InOutProxy(signal);
        }
    }
}

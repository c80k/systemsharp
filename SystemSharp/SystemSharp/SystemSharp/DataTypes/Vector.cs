/**
 * Copyright 2014 Christian Köllner
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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Common;
using SystemSharp.DataTypes.Meta;
using LinqEx = System.Linq.Expressions.Expression;

namespace SystemSharp.DataTypes
{
    public static class Vector
    {
        public static Vector<T> AsVector<T>(this IIndexable<T> indexable)
        {
            var vec = indexable as Vector<T>;
            if (vec == null)
                vec = new ProxyVector<T>(indexable);

            return vec;
        }

        public static Vector<T> AsVector<T>(this T[] array)
        {
            return new DenseVector<T>(array);
        }

        public static Vector<T> Create<T>(params T[] elements)
        {
            return elements.AsVector();
        }

        public static RowVector<T> AsRowVector<T>(this T[] array)
        {
            return new ProxyRowVector<T>(new DenseVector<T>(array));
        }

        public static ColVector<T> AsColVector<T>(this T[] array)
        {
            return new ProxyColVector<T>(new DenseVector<T>(array));
        }

        public static Vector<T> AsVector<T>(this IMultiIndexable<T> nddata)
        {
            return new BoxAsVector<T>(nddata);
        }

        public static Vector<T> Zero<T>(int size, ITypeInstanceTraits<T> traits)
        {
            Contract.Requires<ArgumentNullException>(traits != null, "traits");
            return new ZeroVector<T>(size, traits.GetZero());
        }

        public static Vector<T> Zero<T>(int size)
        {
            T zero = default(T);
            var traits = TypeTraits.GetGenericTraits<T>() as ITypeInstanceTraits<T>;
            if (traits != null)
                zero = traits.GetZero();
            return new ZeroVector<T>(size, zero);
        }

        public static Vector<T> Same<T>(int size, T element)
        {
            return new ZeroVector<T>(size, element);
        }
    }

    public abstract class Vector<T> :
        IIndexable<T>,
        IEnumerable<T>,
        IEquatable<Vector<T>>,
        IHasTypeInstanceTraits<Vector<T>>
    {
        #region private types
        private class VectorGenericTypeTraits : IGenericTypeTraits<Vector<T>>
        {
            private IGenericTypeTraits<T> _elemTraits;

            public VectorGenericTypeTraits()
            {
                _elemTraits = TypeTraits.GetGenericTraits<T>();
            }

            public bool IsZero(Vector<T> value)
            {
                if (_elemTraits == null)
                    return false;
                return value.All(v => _elemTraits.IsZero(v));
            }

            public bool IsOne(Vector<T> value)
            {
                return false;
            }

            public bool IsMinusOne(Vector<T> value)
            {
                return false;
            }

            public EAlgebraicTypeProperties Properties
            {
                get 
                {
                    if (_elemTraits == null)
                        return EAlgebraicTypeProperties.None;

                    var baseProps = _elemTraits.Properties;
                    baseProps &= ~EAlgebraicTypeProperties.MultiplicationIsCommutative;
                    return baseProps;
                }
            }
        }

        private class VectorTypeInstanceTraits : ITypeInstanceTraits<Vector<T>>
        {
            private Vector<T> _vector;

            public VectorTypeInstanceTraits(Vector<T> vector)
            {
                _vector = vector;
            }

            public bool IsEmpty
            {
                get { return _vector.Size == 0; }
            }

            public bool OneExists
            {
                get { return false; }
            }

            public Vector<T> GetZero()
            {
                if (IsEmpty)
                {
                    return Vector.Zero<T>(_vector.Size);
                }
                else
                {
                    var traits = TypeTraits.GetInstanceTraits(_vector[0]);
                    if (traits == null)
                        return Vector.Zero<T>(_vector.Size);
                    else
                        return Vector.Zero(_vector.Size, traits);
                }
            }

            public Vector<T> GetOne()
            {
                return null;
            }
        }
        #endregion private types

        static Vector()
        {
            GenericTypeTraits<Vector<T>>.Register(new VectorGenericTypeTraits());
        }

        private static LinqEx IndexAccess(LinqEx vec, LinqEx index)
        {
            System.Linq.Expressions.Expression<Func<Vector<T>, int, T>> ex = (Vector<T> v, int i) => v[i];
            var indexEx = ex.Body as System.Linq.Expressions.IndexExpression;
            return indexEx.Update(vec, new LinqEx[] { index });
        }

        private static Action<Vector<T>, T[]> CreateUnOp(Func<LinqEx, LinqEx> op)
        {
            var a = LinqEx.Parameter(typeof(Vector<T>));
            var c = LinqEx.Parameter(typeof(T[]));
            var i = LinqEx.Variable(typeof(int));
            var pastLoop = LinqEx.Label();
            return LinqEx.Lambda<Action<Vector<T>, T[]>>(
                LinqEx.Block(
                    LinqEx.Assign(i, LinqEx.Constant(0)),
                    LinqEx.Loop(LinqEx.Block(
                        LinqEx.Assign(LinqEx.ArrayAccess(c, i), op(IndexAccess(a, i))),
                        LinqEx.AddAssign(i, LinqEx.Constant(1)),
                        LinqEx.IfThen(LinqEx.Equal(i, LinqEx.ArrayLength(c)),
                            LinqEx.Break(pastLoop))),
                        pastLoop)),
                a, c)
                .Compile();
        }

        private static Action<Vector<T>, Vector<T>, T[]> CreateBinOp(Func<LinqEx, LinqEx, LinqEx> op)
        {
            var a = LinqEx.Parameter(typeof(T[]));
            var b = LinqEx.Parameter(typeof(T[]));
            var c = LinqEx.Parameter(typeof(T[]));
            var i = LinqEx.Variable(typeof(int));
            var pastLoop = LinqEx.Label();
            return LinqEx.Lambda<Action<Vector<T>, Vector<T>, T[]>>(
                LinqEx.Block(
                    LinqEx.Assign(i, LinqEx.Constant(0)),
                    LinqEx.Loop(LinqEx.Block(
                        LinqEx.Assign(LinqEx.ArrayAccess(c, i), op(IndexAccess(a, i), IndexAccess(b, i))),
                        LinqEx.AddAssign(i, LinqEx.Constant(1)),
                        LinqEx.IfThen(LinqEx.Equal(i, LinqEx.ArrayLength(c)),
                            LinqEx.Break(pastLoop))),
                        pastLoop)),
                a, b, c)
                .Compile();
        }

        private static Lazy<Action<Vector<T>, T[]>> _negArrays = new Lazy<Action<Vector<T>, T[]>>(() => CreateUnOp(LinqEx.Negate));
        private static Lazy<Action<Vector<T>, T[]>> _notArrays = new Lazy<Action<Vector<T>, T[]>>(() => CreateUnOp(LinqEx.Not));

        private static Lazy<Action<Vector<T>, Vector<T>, T[]>> _addArrays = new Lazy<Action<Vector<T>, Vector<T>, T[]>>(() => CreateBinOp(LinqEx.Add));
        private static Lazy<Action<Vector<T>, Vector<T>, T[]>> _subArrays = new Lazy<Action<Vector<T>, Vector<T>, T[]>>(() => CreateBinOp(LinqEx.Subtract));
        private static Lazy<Action<Vector<T>, Vector<T>, T[]>> _mulArrays = new Lazy<Action<Vector<T>, Vector<T>, T[]>>(() => CreateBinOp(LinqEx.Multiply));
        private static Lazy<Action<Vector<T>, Vector<T>, T[]>> _divArrays = new Lazy<Action<Vector<T>, Vector<T>, T[]>>(() => CreateBinOp(LinqEx.Divide));
        private static Lazy<Action<Vector<T>, Vector<T>, T[]>> _andArrays = new Lazy<Action<Vector<T>, Vector<T>, T[]>>(() => CreateBinOp(LinqEx.And));
        private static Lazy<Action<Vector<T>, Vector<T>, T[]>> _orArrays = new Lazy<Action<Vector<T>, Vector<T>, T[]>>(() => CreateBinOp(LinqEx.Or));
        private static Lazy<Action<Vector<T>, Vector<T>, T[]>> _xorArrays = new Lazy<Action<Vector<T>, Vector<T>, T[]>>(() => CreateBinOp(LinqEx.ExclusiveOr));

        public abstract T this[int i] { get; }
        public new abstract Vector<T> this[Range r] { get; }
        public abstract int Size { get; }
        public abstract IEnumerator<T> GetEnumerator();

        IIndexable<T> IIndexable<T>.this[Range r]
        {
            get { return this[r]; }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public ITypeInstanceTraits<Vector<T>> InstanceTraits
        {
            get { return new VectorTypeInstanceTraits(this); }
        }

        public bool Equals(Vector<T> other)
        {
            if (other == null)
                return false;
            if (Size != other.Size)
                return false;

            var elementComparer = EqualityComparer<T>.Default;
            for (int i = 0; i < Size; i++)
            {
                if (!elementComparer.Equals(this[i], other[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            var vec = obj as Vector<T>;
            if (vec == null)
                return false;

            return Equals(vec);
        }

        public override int GetHashCode()
        {
            return this.Aggregate(Size, (x, y) => x.RotateLeft(1) ^ y.GetHashCode());
        }

        public override string ToString()
        {
            return "vector(" + Size + ")";
        }

        public T Sum()
        {
            if (Size == 0)
            {
                return default(T);
            }
            else if (Size == 1)
            {
                return this[0];
            }
            else
            {
                int half = Size / 2;
                int rest = Size % 1;
                T[] tmp = new T[half + rest];
                for (int i = 0; i < half; i++)
                    tmp[i] = GenericMath<T>.Add(this[2 * i], this[2 * i + 1]);
                if (rest == 1)
                    tmp[half] = this[Size - 1];
                while (half > 0)
                {
                    rest = half % 2;
                    half /= 2;

                    for (int i = 0; i < half; i++)
                        tmp[i] = GenericMath<T>.Add(tmp[2 * i], tmp[2 * i + 1]);
                    if (rest == 1)
                        tmp[half] = tmp[2 * half];
                }
                return tmp[0];
            }
        }

        public static Vector<T> operator -(Vector<T> a)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");

            var c = new T[a.Size];
            _negArrays.Value(a, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator !(Vector<T> a)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");

            var c = new T[a.Size];
            _notArrays.Value(a, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator +(Vector<T> a, Vector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size, "a and b must be of same size.");

            var c = new T[a.Size];
            _addArrays.Value(a, b, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator -(Vector<T> a, Vector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size, "a and b must be of same size.");

            var c = new T[a.Size];
            _subArrays.Value(a, b, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator *(Vector<T> a, Vector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size, "a and b must be of same size.");

            var c = new T[a.Size];
            _mulArrays.Value(a, b, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator /(Vector<T> a, Vector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size, "a and b must be of same size.");

            var c = new T[a.Size];
            _divArrays.Value(a, b, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator &(Vector<T> a, Vector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size, "a and b must be of same size.");

            var c = new T[a.Size];
            _andArrays.Value(a, b, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator |(Vector<T> a, Vector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size, "a and b must be of same size.");

            var c = new T[a.Size];
            _orArrays.Value(a, b, c);
            return new DenseVector<T>(c);
        }

        public static Vector<T> operator ^(Vector<T> a, Vector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size, "a and b must be of same size.");

            var c = new T[a.Size];
            _xorArrays.Value(a, b, c);
            return new DenseVector<T>(c);
        }

        public static bool operator ==(Vector<T> a, Vector<T> b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(Vector<T> a, Vector<T> b)
        {
            if (a == null && b == null)
                return false;
            if (a == null || b == null)
                return true;

            return !a.Equals(b);
        }
    }

    class OuterProductMatrix<T> : Matrix<T>
    {
        private ColVector<T> _cv;
        private RowVector<T> _rv;

        public OuterProductMatrix(ColVector<T> cv, RowVector<T> rv)
        {
            _cv = cv;
            _rv = rv;
        }

        public override int Size0
        {
            get { return _cv.Size; }
        }

        public override int Size1
        {
            get { return _rv.Size; }
        }

        public override T this[int i, int j]
        {
            get { return GenericMath<T>.Multiply(_cv[i], _rv[j]); }
        }
    }

    public abstract class RowVector<T> : Vector<T>
    {
        public static T operator *(RowVector<T> rv, ColVector<T> cv)
        {
            return (((Vector<T>)rv) * ((Vector<T>)cv)).Sum();
        }

        public static Matrix<T> operator *(ColVector<T> cv, RowVector<T> rv)
        {
            return new OuterProductMatrix<T>(cv, rv);
        }
    }

    public abstract class ColVector<T> : Vector<T>
    {
    }

    class VectorEnumerator
    {
        private class VectorUpEnumerator<T> : IEnumerator<T>
        {
            private Vector<T> _vector;
            private int _curIndex;

            public VectorUpEnumerator(Vector<T> vector)
            {
                _vector = vector;
                _curIndex = -1;
            }

            public T Current
            {
                get
                {
                    if (_curIndex < 0 || _curIndex >= _vector.Size)
                        throw new InvalidOperationException("Enumeration is out of elements.");

                    return _vector[_curIndex];
                }
            }

            public void Dispose()
            {
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                return (++_curIndex < _vector.Size);
            }

            public void Reset()
            {
                _curIndex = -1;
            }
        }

        private class VectorDownEnumerator<T> : IEnumerator<T>
        {
            private Vector<T> _vector;
            private int _curIndex;

            public VectorDownEnumerator(Vector<T> vector)
            {
                _vector = vector;
                _curIndex = vector.Size;
            }

            public T Current
            {
                get
                {
                    if (_curIndex < 0 || _curIndex >= _vector.Size)
                        throw new InvalidOperationException("Enumeration is out of elements.");

                    return _vector[_curIndex];
                }
            }

            public void Dispose()
            {
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                return (--_curIndex >= 0);
            }

            public void Reset()
            {
                _curIndex = _vector.Size;
            }
        }

        public static IEnumerator<T> Create<T>(Vector<T> vector, EDimDirection dir)
        {
            switch (dir)
            {
                case EDimDirection.To:
                    return new VectorUpEnumerator<T>(vector);

                case EDimDirection.Downto:
                    return new VectorDownEnumerator<T>(vector);

                default:
                    throw new NotImplementedException();
            }
        }
    }

    class ProxyVector<T> : Vector<T>
    {
        private IIndexable<T> _data;
        private Range _projRange;

        public ProxyVector(IIndexable<T> data)
        {
            _data = data;
            _projRange = 0.To(_data.Size - 1);
        }

        public ProxyVector(IIndexable<T> data, Range projRange)
        {
            Contract.Requires<ArgumentOutOfRangeException>(0.To(data.Size - 1).Contains(projRange), "slice exceeds vector bounds");
            _data = data;
            _projRange = projRange;
        }

        public override T this[int i]
        {
            get { return _data[_projRange.Unproject(i)]; }
        }

        public override Vector<T> this[Range r]
        {
            get { return new ProxyVector<T>(_data, _projRange.Unproject(r)); }
        }

        public override int Size
        {
            get { return (int)_projRange.Size; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return VectorEnumerator.Create(this, _projRange.Direction);
        }
    }

    class DenseVector<T>: Vector<T>
    {
        private T[] _data;
        private Range _projRange;

        public DenseVector(T[] data)
        {
            _data = data;
            _projRange = 0.To(data.Length - 1);
        }

        private DenseVector(T[] data, Range projRange)
        {
            _data = data;
            _projRange = projRange;
        }

        public override T this[int i]
        {
            get { return _data[_projRange.Unproject(i)]; }
        }

        public override Vector<T> this[Range r]
        {
            get
            {
                Range newRange = r.Project(_projRange);
                return new DenseVector<T>(_data, newRange);
            }
        }

        public override int Size
        {
            get { return (int)_projRange.Size; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return VectorEnumerator.Create(this, _projRange.Direction);
        }
    }

    class ProxyRowVector<T> : RowVector<T>
    {
        private ProxyVector<T> _proxy;

        public ProxyRowVector(IIndexable<T> source)
        {
            _proxy = new ProxyVector<T>(source);
        }

        public override T this[int i]
        {
            get { return _proxy[i]; }
        }

        public override Vector<T> this[Range r]
        {
            get { return _proxy[r]; }
        }

        public override int Size
        {
            get { return _proxy.Size; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return _proxy.GetEnumerator();
        }
    }

    class ProxyColVector<T> : ColVector<T>
    {
        private ProxyVector<T> _proxy;

        public ProxyColVector(IIndexable<T> source)
        {
            _proxy = new ProxyVector<T>(source);
        }

        public override T this[int i]
        {
            get { return _proxy[i]; }
        }

        public override Vector<T> this[Range r]
        {
            get { return _proxy[r]; }
        }

        public override int Size
        {
            get { return _proxy.Size; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return _proxy.GetEnumerator();
        }
    }

    class BoxAsVector<T> : Vector<T>
    {
        private IMultiIndexable<T> _source;

        public BoxAsVector(IMultiIndexable<T> source)
        {
            Contract.Requires<ArgumentNullException>(source != null, "source");
            Contract.Requires<ArgumentException>(source.Sizes.Size == 1, "source is not rank 1");

            _source = source;
        }

        public override T this[int i]
        {
            get { return _source[i].GetElement(); }
        }

        public override Vector<T> this[Range r]
        {
            get { return _source[r].AsVector(); }
        }

        public override int Size
        {
            get { return _source.Sizes[0]; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return VectorEnumerator.Create(this, EDimDirection.To);
        }
    }

    class ZeroVector<T> : Vector<T>
    {
        private int _size;
        private T _zero;

        public ZeroVector(int size, T zero)
        {
            Contract.Requires<ArgumentOutOfRangeException>(size >= 0, "size is negative");
            _size = size;
            _zero = zero;
        }

        public override T this[int i]
        {
            get { return _zero; }
        }

        public override Vector<T> this[Range r]
        {
            get { return new ProxyVector<T>(this, r); }
        }

        public override int Size
        {
            get { return _size; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return VectorEnumerator.Create(this, EDimDirection.To);
        }
    }
}

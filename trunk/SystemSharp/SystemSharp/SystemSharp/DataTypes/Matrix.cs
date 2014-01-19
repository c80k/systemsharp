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
    public static class Matrix
    {
        public static Matrix<T> AsMatrix<T>(T[,] data)
        {
            return new DenseMatrix<T>(data);
        }

        public static Matrix<T> AsMatrix<T>(IMultiIndexable<T> nddata)
        {
            return new BoxAsMatrix<T>(nddata);
        }

        public static Matrix<TDest> Select<TSource, TDest>(this IMatrixIndexable<TSource> matrix, Func<TSource, TDest> selectFn)
        {
            return new SelectMatrix<TSource, TDest>(matrix, selectFn);
        }

        public static Matrix<T> Zero<T>(int size0, int size1, ITypeInstanceTraits<T> traits)
        {
            Contract.Requires<ArgumentNullException>(traits != null, "traits");
            return new ZeroMatrix<T>(size0, size1, traits.GetZero());
        }

        public static Matrix<T> Zero<T>(int size0, int size1)
        {
            T zero = default(T);
            var traits = TypeTraits.GetGenericTraits<T>() as ITypeInstanceTraits<T>;
            if (traits != null)
                zero = traits.GetZero();
            return new ZeroMatrix<T>(size0, size1, zero);
        }

        public static Matrix<T> Same<T>(int size0, int size1, T element)
        {
            return new ZeroMatrix<T>(size0, size1, element);
        }

        public static Matrix<T> Diag<T>(int size, T element)
        {
            T zero = default(T);
            var traits = TypeTraits.GetInstanceTraits<T>(element);
            if (traits != null)
                zero = traits.GetZero();
            return new DiagMatrix<T>(size, element, zero);
        }

        public static Matrix<T> One<T>(int size, ITypeInstanceTraits<T> traits)
        {
            Contract.Requires<ArgumentNullException>(traits != null, "traits");
            return new DiagMatrix<T>(size, traits.GetOne(), traits.GetZero());
        }

        public static Matrix<T> One<T>(int size)
        {
            var traits = TypeTraits.GetGenericTraits<T>() as ITypeInstanceTraits<T>;
            if (traits == null)
                throw new InvalidOperationException("Type instance traits required, please use method One<T>(int size, ITypeInstanceTraits<T> traits)");
            return new DiagMatrix<T>(size, traits.GetOne(), traits.GetZero());
        }
    }

    public abstract class Matrix<T>:
        IMatrixIndexable<T>,
        IEquatable<Matrix<T>>,
        IHasTypeInstanceTraits<Matrix<T>>
    {
        #region private types
        private class MatrixGenericTypeTraits : IGenericTypeTraits<Matrix<T>>
        {
            private IGenericTypeTraits<T> _elemTraits;

            public MatrixGenericTypeTraits()
            {
                _elemTraits = TypeTraits.GetGenericTraits<T>();
            }

            public bool IsZero(Matrix<T> value)
            {
                if (_elemTraits == null)
                    return false;

                return value.Serialize().All(_ => _elemTraits.IsZero(_));
            }

            private bool IsDiag(Matrix<T> value, Func<T, bool> diagPred)
            {
                if (value.Size0 != value.Size1)
                    return false;

                for (int i = 0; i < value.Size0; i++)
                {
                    for (int j = 0; j < value.Size1; j++)
                    {
                        if (i == j)
                        {
                            if (!_elemTraits.IsOne(value[i, j]))
                                return false;
                        }
                        else
                        {
                            if (!diagPred(value[i, j]))
                                return false;
                        }
                    }
                }
                return true;
            }

            public bool IsOne(Matrix<T> value)
            {
                if (_elemTraits == null)
                    return false;

                return IsDiag(value, _elemTraits.IsOne);
            }

            public bool IsMinusOne(Matrix<T> value)
            {
                if (_elemTraits == null)
                    return false;

                return IsDiag(value, _elemTraits.IsMinusOne);
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

        private class MatrixTypeInstanceTraits : ITypeInstanceTraits<Matrix<T>>
        {
            private Matrix<T> _matrix;

            public MatrixTypeInstanceTraits(Matrix<T> matrix)
            {
                _matrix = matrix;
            }

            public bool IsEmpty
            {
                get { return _matrix.Size0 == 0 || _matrix.Size1 == 0; }
            }

            private ITypeInstanceTraits<T> TryGetElementTraits()
            {
                if (IsEmpty)
                    return null;

                var element = _matrix[0, 0];
                return TypeTraits.GetInstanceTraits(element);
            }

            public bool OneExists
            {
                get
                {
                    var traits = TryGetElementTraits();
                    return _matrix.Size0 == _matrix.Size1 && traits != null && traits.OneExists;
                }
            }

            public Matrix<T> GetZero()
            {
                var traits = TryGetElementTraits();
                if (traits == null)
                    return Matrix.Zero(_matrix.Size0, _matrix.Size1);
                else
                    return Matrix.Zero(_matrix.Size0, _matrix.Size1, traits);
            }

            public Matrix<T> GetOne()
            {
                if (!OneExists)
                    return null;

                var traits = TryGetElementTraits();
                return Matrix.One(_matrix.Size0, traits);
            }
        }

        #endregion private types

        static Matrix()
        {
            GenericTypeTraits<Matrix<T>>.Register(new MatrixGenericTypeTraits());
        }

        private static LinqEx IndexAccess(LinqEx mat, LinqEx row, LinqEx col)
        {
            System.Linq.Expressions.Expression<Func<Matrix<T>, int, int, T>> ex = (Matrix<T> m, int i, int j) => m[i, j];
            var indexEx = ex.Body as System.Linq.Expressions.IndexExpression;
            return indexEx.Update(mat, new LinqEx[] { row, col });
        }

        private static LinqEx GetSize0(LinqEx mat)
        {
            System.Linq.Expressions.Expression<Func<Matrix<T>, int, int>> ex = (Matrix<T> m, int i) => m.Size0;
            var membEx = ex.Body as System.Linq.Expressions.MemberExpression;
            return membEx.Update(mat);
        }

        private static LinqEx GetSize1(LinqEx mat)
        {
            System.Linq.Expressions.Expression<Func<Matrix<T>, int, int>> ex = (Matrix<T> m, int i) => m.Size1;
            var membEx = ex.Body as System.Linq.Expressions.MemberExpression;
            return membEx.Update(mat);
        }

        private static Action<Matrix<T>, T[,]> CreateUnOp(Func<LinqEx, LinqEx> op)
        {
            var a = LinqEx.Parameter(typeof(Matrix<T>));
            var c = LinqEx.Parameter(typeof(T[,]));
            var i = LinqEx.Variable(typeof(int));
            var j = LinqEx.Variable(typeof(int));
            var pastOuterLoop = LinqEx.Label();
            var pastInnerLoop = LinqEx.Label();
            return LinqEx.Lambda<Action<Matrix<T>, T[,]>>(
                LinqEx.Block(
                    LinqEx.Assign(i, LinqEx.Constant(0)),
                    LinqEx.Loop(LinqEx.Block(
                        LinqEx.Assign(j, LinqEx.Constant(0)),
                        LinqEx.Loop(LinqEx.Block(
                            LinqEx.Assign(LinqEx.ArrayAccess(c, i, j), op(IndexAccess(a, i, j))),
                            LinqEx.AddAssign(j, LinqEx.Constant(1)),
                            LinqEx.IfThen(LinqEx.Equal(j, GetSize0(a)),
                                LinqEx.Break(pastInnerLoop))
                        ), pastInnerLoop),
                        LinqEx.AddAssign(i, LinqEx.Constant(1)),
                        LinqEx.IfThen(LinqEx.Equal(i, GetSize1(a)),
                            LinqEx.Break(pastOuterLoop))),
                        pastOuterLoop)),
                a, c)
                .Compile();
        }

        private static Action<Matrix<T>, Matrix<T>, T[,]> CreateBinOp(Func<LinqEx, LinqEx, LinqEx> op)
        {
            var a = LinqEx.Parameter(typeof(Matrix<T>));
            var b = LinqEx.Parameter(typeof(Matrix<T>));
            var c = LinqEx.Parameter(typeof(T[,]));
            var i = LinqEx.Variable(typeof(int));
            var j = LinqEx.Variable(typeof(int));
            var pastOuterLoop = LinqEx.Label();
            var pastInnerLoop = LinqEx.Label();
            return LinqEx.Lambda<Action<Matrix<T>, Matrix<T>, T[,]>>(
                LinqEx.Block(
                    LinqEx.Assign(i, LinqEx.Constant(0)),
                    LinqEx.Loop(LinqEx.Block(
                        LinqEx.Assign(j, LinqEx.Constant(0)),
                        LinqEx.Loop(LinqEx.Block(
                            LinqEx.Assign(LinqEx.ArrayAccess(c, i, j), op(IndexAccess(a, i, j), IndexAccess(b, i, j))),
                            LinqEx.AddAssign(j, LinqEx.Constant(1)),
                            LinqEx.IfThen(LinqEx.Equal(j, GetSize0(a)),
                                LinqEx.Break(pastInnerLoop))
                        ), pastInnerLoop),
                        LinqEx.AddAssign(i, LinqEx.Constant(1)),
                        LinqEx.IfThen(LinqEx.Equal(i, GetSize1(a)),
                            LinqEx.Break(pastOuterLoop))),
                        pastOuterLoop)),
                a, b, c)
                .Compile();
        }

        private static Lazy<Action<Matrix<T>, T[,]>> _negMatrices = new Lazy<Action<Matrix<T>, T[,]>>(() => CreateUnOp(LinqEx.Negate));
        private static Lazy<Action<Matrix<T>, T[,]>> _notMatrices = new Lazy<Action<Matrix<T>, T[,]>>(() => CreateUnOp(LinqEx.Not));

        private static Lazy<Action<Matrix<T>, Matrix<T>, T[,]>> _addMatrices = new Lazy<Action<Matrix<T>, Matrix<T>, T[,]>>(() => CreateBinOp(LinqEx.Add));
        private static Lazy<Action<Matrix<T>, Matrix<T>, T[,]>> _subMatrices = new Lazy<Action<Matrix<T>, Matrix<T>, T[,]>>(() => CreateBinOp(LinqEx.Subtract));
        private static Lazy<Action<Matrix<T>, Matrix<T>, T[,]>> _mulMatrices = new Lazy<Action<Matrix<T>, Matrix<T>, T[,]>>(() => CreateBinOp(LinqEx.Multiply));
        private static Lazy<Action<Matrix<T>, Matrix<T>, T[,]>> _divMatrices = new Lazy<Action<Matrix<T>, Matrix<T>, T[,]>>(() => CreateBinOp(LinqEx.Divide));
        private static Lazy<Action<Matrix<T>, Matrix<T>, T[,]>> _andMatrices = new Lazy<Action<Matrix<T>, Matrix<T>, T[,]>>(() => CreateBinOp(LinqEx.And));
        private static Lazy<Action<Matrix<T>, Matrix<T>, T[,]>> _orMatrices = new Lazy<Action<Matrix<T>, Matrix<T>, T[,]>>(() => CreateBinOp(LinqEx.Or));
        private static Lazy<Action<Matrix<T>, Matrix<T>, T[,]>> _xorMatrices = new Lazy<Action<Matrix<T>, Matrix<T>, T[,]>>(() => CreateBinOp(LinqEx.ExclusiveOr));

        public abstract int Size0 { get; }
        public abstract int Size1 { get; }

        public long TotalSize
        {
            get { return (long)Size0 * Size1; }
        }

        public abstract T this[int i, int j] { get; }

        public bool Equals(Matrix<T> other)
        {
            if (other == null)
                return false;
            if (other.Size0 != Size0)
                return false;
            if (other.Size1 != Size1)
                return false;

            var elementComparer = EqualityComparer<T>.Default;
            for (int i = 0; i < Size0; i++)
                for (int j = 0; j < Size1; j++)
                    if (!elementComparer.Equals(this[i, j], other[i, j]))
                        return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            var matrix = obj as Matrix<T>;
            if (matrix == null)
                return false;

            return Equals(matrix);
        }

        public override int GetHashCode()
        {
            return Size0 ^ Size1.RotateLeft(1) ^ Serialize().GetHashCode();
        }

        public override string ToString()
        {
            return "matrix(" + Size0 + " x " + Size1 + ")";
        }

        public new virtual RowVector<T> this[int i, Range rj]
        {
            get { return new MatrixRowProjection<T>(this, i, rj); }
        }

        public new virtual ColVector<T> this[Range ri, int rj]
        {
            get { return new MatrixColProjection<T>(this, ri, rj); }
        }

        public new virtual Matrix<T> this[Range ri, Range rj]
        {
            get { return new SubMatrixProjection<T>(this, ri, rj); }
        }

        IIndexable<T> IMatrixIndexable<T>.this[int i, Range rj]
        {
            get { return this[i, rj]; }
        }

        IIndexable<T> IMatrixIndexable<T>.this[Range ri, int rj]
        {
            get { return this[ri, rj]; }
        }

        IMatrixIndexable<T> IMatrixIndexable<T>.this[Range ri, Range rj]
        {
            get { return this[ri, rj]; }
        }

        public ITypeInstanceTraits<Matrix<T>> InstanceTraits
        {
            get { return new MatrixTypeInstanceTraits(this); }
        }

        public Vector<T> Serialize(IEnumerable<Tuple<int, int>> order)
        {
            return Vector.AsVector(order.Select(tup => this[tup.Item1, tup.Item2]).ToArray());
        }

        private IEnumerable<Tuple<int, int>> TopLeftToBottomRightOrder
        {
            get
            {
                return from i in Enumerable.Range(0, Size0)
                       from j in Enumerable.Range(0, Size1)
                       select Tuple.Create(i, j);
            }
        }

        public Vector<T> Serialize()
        {
            return Serialize(TopLeftToBottomRightOrder);
        }

        public static Matrix<T> operator -(Matrix<T> a)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");

            T[,]  c = new T[a.Size0, a.Size1];
            _negMatrices.Value(a, c);
            return new DenseMatrix<T>(c);
        }

        public static Matrix<T> operator !(Matrix<T> a)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");

            T[,]  c = new T[a.Size0, a.Size1];
            _notMatrices.Value(a, c);
            return new DenseMatrix<T>(c);
        }

        public static Matrix<T> operator +(Matrix<T> a, Matrix<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size0 == b.Size0, "Matrices do not have the same number of rows.");
            Contract.Requires<ArgumentException>(a.Size1 == b.Size1, "Matrices do not have the same number of columns.");

            T[,] c = new T[a.Size0, a.Size1];
            _addMatrices.Value(a, b, c);
            return new DenseMatrix<T>(c);
        }

        public static Matrix<T> operator -(Matrix<T> a, Matrix<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size0 == b.Size0, "Matrices do not have the same number of rows.");
            Contract.Requires<ArgumentException>(a.Size1 == b.Size1, "Matrices do not have the same number of columns.");

            T[,] c = new T[a.Size0, a.Size1];
            _subMatrices.Value(a, b, c);
            return new DenseMatrix<T>(c);
        }

        public static ColVector<T> operator *(Matrix<T> a, ColVector<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size1 == b.Size, "Matrix does not have same number of columns as multiplicand vector.");

            T[] c = new T[a.Size0];
            Range colRange = 0.To(a.Size1 - 1);
            for (int i = 0; i < c.Length; i++)
                c[i] = a[i, colRange] * b;
            return Vector.AsColVector(c);
        }

        public static RowVector<T> operator *(RowVector<T> a, Matrix<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size == b.Size0, "Matrix does not have same number of rows as multiplicand vector.");

            T[] c = new T[b.Size1];
            Range rowRange = 0.To(b.Size0 - 1);
            for (int i = 0; i < c.Length; i++)
                c[i] = a * b[rowRange, i];
            return Vector.AsRowVector(c);
        }

        public static Matrix<T> operator *(Matrix<T> a, Matrix<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size1 == b.Size0, "Left matrix does not have same number of rows as right matrix number of columns.");

            T[,] c = new T[a.Size0, b.Size1];
            Range colRange = 0.To(a.Size1 - 1);
            Range rowRange = 0.To(b.Size0 - 1);
            for (int i = 0; i < a.Size0; i++)
                for (int j = 0; j < b.Size1; j++)
                    c[i, j] = a[i, colRange] * b[rowRange, j];
            return Matrix.AsMatrix(c);
        }

        public static Matrix<T> operator &(Matrix<T> a, Matrix<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size0 == b.Size0, "Matrices do not have the same number of rows.");
            Contract.Requires<ArgumentException>(a.Size1 == b.Size1, "Matrices do not have the same number of columns.");

            T[,] c = new T[a.Size0, a.Size1];
            _andMatrices.Value(a, b, c);
            return new DenseMatrix<T>(c);
        }

        public static Matrix<T> operator |(Matrix<T> a, Matrix<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size0 == b.Size0, "Matrices do not have the same number of rows.");
            Contract.Requires<ArgumentException>(a.Size1 == b.Size1, "Matrices do not have the same number of columns.");

            T[,] c = new T[a.Size0, a.Size1];
            _orMatrices.Value(a, b, c);
            return new DenseMatrix<T>(c);
        }

        public static Matrix<T> operator ^(Matrix<T> a, Matrix<T> b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            Contract.Requires<ArgumentException>(a.Size0 == b.Size0, "Matrices do not have the same number of rows.");
            Contract.Requires<ArgumentException>(a.Size1 == b.Size1, "Matrices do not have the same number of columns.");

            T[,] c = new T[a.Size0, a.Size1];
            _xorMatrices.Value(a, b, c);
            return new DenseMatrix<T>(c);
        }

        public static bool operator ==(Matrix<T> a, Matrix<T> b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(Matrix<T> a, Matrix<T> b)
        {
            if (a == null && b == null)
                return false;
            if (a == null || b == null)
                return true;

            return !a.Equals(b);
        }
    }

    class MatrixRowProjection<T> : RowVector<T>
    {
        private IMatrixIndexable<T> _source;
        private int _row;
        private Range _columns;

        public MatrixRowProjection(IMatrixIndexable<T> source, int row, Range columns)
        {
            _source = source;
            _row = row;
            _columns = columns;
        }

        public override T this[int i]
        {
            get { return _source[_row, _columns.Unproject(i)]; }
        }

        public override RowVector<T> this[Range r]
        {
            get { return new MatrixRowProjection<T>(_source, _row, _columns.Unproject(r)); }
        }

        public override int Size
        {
            get { return (int)_columns.Size; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return VectorEnumerator.Create(this, _columns.Direction);
        }
    }

    class MatrixColProjection<T> : ColVector<T>
    {
        private IMatrixIndexable<T> _source;
        private Range _rows;
        private int _column;

        public MatrixColProjection(IMatrixIndexable<T> source, Range rows, int column)
        {
            _source = source;
            _rows = rows;
            _column = column;
        }

        public override T this[int i]
        {
            get { return _source[_rows.Unproject(i), _column]; }
        }

        public override ColVector<T> this[Range r]
        {
            get { return new MatrixColProjection<T>(_source, _rows.Unproject(r), _column); }
        }

        public override int Size
        {
            get { return (int)_rows.Size; }
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return VectorEnumerator.Create(this, _rows.Direction);
        }
    }

    class SubMatrixProjection<T> : Matrix<T>
    {
        private IMatrixIndexable<T> _source;
        private Range _rows;
        private Range _columns;

        public SubMatrixProjection(IMatrixIndexable<T> source, Range rows, Range columns)
        {
            _rows = rows;
            _columns = columns;
        }

        public override int Size0
        {
            get { return (int)_rows.Size; }
        }

        public override int Size1
        {
            get { return (int)_columns.Size; }
        }

        public override T this[int i, int j]
        {
            get { return _source[_rows.Unproject(i), _columns.Unproject(j)]; }
        }

        public override RowVector<T> this[int i, Range rj]
        {
            get { return new MatrixRowProjection<T>(_source, _rows.Unproject(i), _columns.Unproject(rj)); }
        }

        public override ColVector<T> this[Range ri, int j]
        {
            get { return new MatrixColProjection<T>(_source, _rows.Unproject(ri), _columns.Unproject(j)); }
        }

        public override Matrix<T> this[Range ri, Range rj]
        {
            get { return new SubMatrixProjection<T>(_source, _rows.Unproject(ri), _columns.Unproject(rj)); }
        }
    }

    class DenseMatrix<T> : Matrix<T>
    {
        private T[,] _data;

        public DenseMatrix(T[,] data)
        {
            _data = data;
        }

        public override int Size0
        {
            get { return _data.GetLength(0); }
        }

        public override int Size1
        {
            get { return _data.GetLength(1); }
        }

        public override T this[int i, int j]
        {
            get { return _data[i, j]; }
        }
    }

    class SelectMatrix<TSrc, TDest> : Matrix<TDest>
    {
        private IMatrixIndexable<TSrc> _source;
        private Func<TSrc, TDest> _selectFn;

        public SelectMatrix(IMatrixIndexable<TSrc> source, Func<TSrc, TDest> selectFn)
        {
            _source = source;
            _selectFn = selectFn;
        }

        public override int Size0
        {
            get { return _source.Size0; }
        }

        public override int Size1
        {
            get { return _source.Size1; }
        }

        public override TDest this[int i, int j]
        {
            get { return _selectFn(_source[i, j]); }
        }
    }

    class BoxAsMatrix<T> : Matrix<T>
    {
        private IMultiIndexable<T> _source;

        public BoxAsMatrix(IMultiIndexable<T> source)
        {
            Contract.Requires<ArgumentNullException>(source != null, "source");
            Contract.Requires<ArgumentException>(source.Sizes.Size == 2, "source is not rank 2");

            _source = source;
        }

        public override int Size0
        {
            get { return _source.Sizes[0]; }
        }

        public override int Size1
        {
            get { return _source.Sizes[1]; }
        }

        public override T this[int i, int j]
        {
            get { return _source[i, j].GetElement(); }
        }
    }

    class ZeroMatrix<T> : Matrix<T>
    {
        private int _size0;
        private int _size1;
        private T _zero;

        public ZeroMatrix(int size0, int size1, T zero)
        {
            Contract.Requires<ArgumentOutOfRangeException>(size0 >= 0, "size0 is negative");
            Contract.Requires<ArgumentOutOfRangeException>(size1 >= 0, "size1 is negative");

            _size0 = size0;
            _size1 = size1;
            _zero = zero;
        }

        public override int Size0
        {
            get { return _size0; }
        }

        public override int Size1
        {
            get { return _size1; }
        }

        public override T this[int i, int j]
        {
            get { return _zero; }
        }
    }

    class DiagMatrix<T> : Matrix<T>
    {
        private int _size;
        private T _zero;
        private T _one;

        public DiagMatrix(int size, T one, T zero)
        {
            Contract.Requires<ArgumentOutOfRangeException>(size >= 0, "size is negative");
            _size = size;
            _one = one;
            _zero = zero;
        }

        public override int Size0
        {
            get { return _size; }
        }

        public override int Size1
        {
            get { return _size; }
        }

        public override T this[int i, int j]
        {
            get { return (i == j) ? _one : _zero; }
        }
    }
}

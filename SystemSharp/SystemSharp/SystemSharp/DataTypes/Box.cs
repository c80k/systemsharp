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
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Common;
using SystemSharp.DataTypes.Meta;

namespace SystemSharp.DataTypes
{
    public static class Box
    {
        public static IEnumerable<int[]> EnumerateFromLoToHi(int[] sizes)
        {
            if (sizes.All(_ => _ >= 1))
            {
                int[] curIndex = new int[sizes.Length];
                int i;
                do
                {
                    yield return curIndex;

                    for (i = 0; i < sizes.Length; i++)
                    {
                        if (++curIndex[i] == sizes[i])
                            curIndex[i] = 0;
                        else
                            break;
                    }
                } while (i < sizes.Length);
            }
        }

        public static Box<T> AsBox<T>(this Array array)
        {
            Contract.Requires<ArgumentNullException>(array != null, "array");
            Contract.Requires<ArgumentException>(typeof(T).IsAssignableFrom(array.GetType().GetElementType()),
                "Generic type is not assignable from array element type.");

            return new DenseBox<T>(array);
        }

        public static Box<T> Create<T>(Func<int[], T> elementCreator, params int[] sizes)
        {
            Contract.Requires<ArgumentNullException>(elementCreator != null, "elementCreator");
            Contract.Requires<ArgumentNullException>(sizes != null, "sizes");
            Contract.Requires<ArgumentOutOfRangeException>(sizes.All(_ => _ >= 0), "sizes contains a negative element");

            if (sizes.Length == 0)
            {
                return new Box0<T>(elementCreator(new int[0]));
            }

            var data = Array.CreateInstance(typeof(T), sizes);
            foreach (var index in EnumerateFromLoToHi(sizes))
            {
                T elem = elementCreator(index);
                data.SetValue(elem, index);
            }
            return data.AsBox<T>();
        }

        public static Box<T> AsBox<T>(this IIndexable<T> vector)
        {
            return new Box1<T>(vector);
        }

        public static Box<T> AsBox<T>(this IMatrixIndexable<T> matrix)
        {
            return new Box2<T>(matrix);
        }

        public static Box<T> AsBox<T>(this IMultiIndexable<T> nddata)
        {
            var box = nddata as Box<T>;
            if (box == null)
                return new BoxWrapper<T>(nddata);
            else
                return box;
        }

        public static Box<T> FromScalar<T>(T element)
        {
            return new Box0<T>(element);
        }

        public static Box<T> Transpose<T>(this IMultiIndexable<T> nddata, params int[] newDimensions)
        {
            return new BoxTransposition<T>(nddata, newDimensions);
        }

        public static Box<TTarget> Select<TSource, TTarget>(this IMultiIndexable<TSource> nddata, Func<TSource, TTarget> selector)
        {
            return new SelectBox<TSource, TTarget>(nddata, selector);
        }

        public static IEnumerable<T> Serialize<T>(this IMultiIndexable<T> nddata, IEnumerable<int[]> indexOrder)
        {
            int rank = nddata.Sizes.Size;
            if (rank == 0)
            {
                yield return nddata.GetElement();
            }
            else
            {
                var dsindex = new DimSpec[rank];
                foreach (var index in indexOrder)
                {
                    if (index == null)
                        throw new ArgumentException("index enumeration returned null index");
                    if (index.Length != dsindex.Length)
                        throw new ArgumentException("index enumeration returned index of wrong rank");
                    for (int i = 0; i < index.Length; i++)
                        dsindex[i] = index[i];
                    yield return nddata[dsindex].GetElement();
                }
            }
        }

        public static IEnumerable<T> Enumerate<T>(this IMultiIndexable<T> nddata)
        {
            Contract.Requires<ArgumentNullException>(nddata != null, "nddata");

            return Serialize(nddata, EnumerateFromLoToHi(nddata.Sizes.ToArray()));
        }

        public static Box<T> Zero<T>(ITypeInstanceTraits<T> elemTraits, params int[] sizes)
        {
            Contract.Requires<ArgumentNullException>(elemTraits != null, "elemTraits");
            Contract.Requires<ArgumentNullException>(sizes != null, "sizes");

            return new ZeroBox<T>(sizes.AsVector(), elemTraits.GetZero());
        }

        public static Box<T> Zero<T>(params int[] sizes)
        {
            Contract.Requires<ArgumentNullException>(sizes != null, "sizes");

            T zero = default(T);
            var traits = TypeTraits.GetGenericTraits<T>() as ITypeInstanceTraits<T>;
            if (traits != null)
                zero = traits.GetZero();
            return new ZeroBox<T>(sizes.AsVector(), zero);
        }

        public static Box<T> Same<T>(T element, params int[] sizes)
        {
            Contract.Requires<ArgumentNullException>(sizes != null, "sizes");

            return new ZeroBox<T>(sizes.AsVector(), element);
        }

        public static Box<T> Diag<T>(int rank, int size, T element)
        {
            return new DiagBox<T>(rank, size, element);
        }

        public static Box<T> One<T>(int rank, int size, ITypeInstanceTraits<T> traits)
        {
            Contract.Requires<ArgumentNullException>(traits != null, "traits");
            return Diag(rank, size, traits.GetOne());
        }

        public static Box<T> One<T>(int rank, int size)
        {
            var traits = TypeTraits.GetGenericTraits<T>() as ITypeInstanceTraits<T>;
            if (traits == null)
            {
                throw new InvalidOperationException(
                    string.Format("Require type instance traits for type {0}, please use method One<T>(int rank, int size, ITypeInstanceTraits<T> traits)",
                    typeof(T).Name));
            }
            return new DiagBox<T>(rank, size, traits.GetOne());
        }
    }

    public abstract class Box<T>: 
        IMultiIndexable<T>,
        IEquatable<Box<T>>,
        IHasTypeInstanceTraits<Box<T>>
    {
        #region private types
        private class BoxGenericTypeTraits<T> : IGenericTypeTraits<Box<T>>
        {
            private IGenericTypeTraits<T> _elemTraits;

            public BoxGenericTypeTraits()
            {
                _elemTraits = TypeTraits.GetGenericTraits<T>();
            }

            public override bool IsZero(Box<T> value)
            {
                if (_elemTraits == null)
                    return false;

                return value.Enumerate().All(v => _elemTraits.IsZero(v));
            }

            private bool IsDiag(Box<T> value, Func<T, bool> diagPred)
            {
                if (value.Sizes.Skip(1).All(s => s == value.Sizes[0]))
                    return false;

                if (value.Rank == 0)
                {
                    return _elemTraits.IsOne(value.GetElement());
                }
                else
                {
                    foreach (var index in Box.EnumerateFromLoToHi(value.Sizes.ToArray()))
                    {
                        bool isDiag = true;
                        for (int i = 1; i < index.Length; i++)
                        {
                            if (index[i] != index[0])
                            {
                                isDiag = false;
                                break;
                            }
                        }
                        if (isDiag)
                        {
                            if (!diagPred(value.ElementAt(index)))
                                return false;
                        }
                        else
                        {
                            if (!_elemTraits.IsZero(value.ElementAt(index)))
                                return false;
                        }
                    }
                    return true;
                }
            }

            public override bool IsOne(Box<T> value)
            {
                if (_elemTraits == null)
                    return false;

                return IsDiag(value, _elemTraits.IsOne);
            }

            public override bool IsMinusOne(Box<T> value)
            {
                if (_elemTraits == null)
                    return false;

                return IsDiag(value, _elemTraits.IsMinusOne);
            }

            public override EAlgebraicTypeProperties Properties
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

        private class BoxTypeInstanceTraits<T> : ITypeInstanceTraits<Box<T>>
        {
            private Box<T> _box;

            public BoxTypeInstanceTraits(Box<T> box)
            {
                _box = box;
            }

            public bool IsEmpty
            {
                get { return _box.Rank > 0 && _box.Sizes.Any(s => s == 0); }
            }

            private ITypeInstanceTraits<T> TryGetInstanceTraits()
            {
                if (_box.Sizes.All(s => s > 0))
                {
                    int[] indices = new int[_box.Rank];
                    return TypeTraits.GetInstanceTraits(_box.ElementAt(indices));
                }
                else
                {
                    return null;
                }
            }


            public bool OneExists
            {
                get 
                {
                    var traits = TryGetInstanceTraits();
                    if (traits == null)
                    {
                        return false;
                    }
                    else if (_box.Rank == 0 ||
                        (_box.Rank == 1 && _box.Sizes[0] == 1) ||
                        _box.Sizes.Skip(1).All(s => s == _box.Sizes[0]))
                    {
                        return traits.OneExists;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            public Box<T> GetZero()
            {
                var traits = TryGetInstanceTraits();
                if (traits == null)
                    return Box.Zero(_box.Sizes.ToArray());
                else
                    return Box.Zero(traits, _box.Sizes.ToArray());
            }

            public Box<T> GetOne()
            {
                if (!OneExists)
                    return null;

                var traits = TryGetInstanceTraits();
                return Box.One(_box.Rank, _box.Rank > 0 ? _box.Sizes[0] : 1, traits);
            }
        }

        #endregion private types

        static Box()
        {
            GenericTypeTraits<Box<T>>.Register(new BoxGenericTypeTraits<T>());
        }

        public abstract Vector<int> Sizes { get; }
        public new abstract Box<T> this[params DimSpec[] indices] { get; }
        public abstract T GetElement();

        IMultiIndexable<T> IMultiIndexable<T>.this[params DimSpec[] indices]
        {
            get { return this[indices]; }
        }

        public virtual int Rank
        {
            get { return Sizes.Size; }
        }

        public abstract T ElementAt(params int[] indices);

        public bool Equals(Box<T> other)
        {
            if (other == null)
                return false;
            if (other.Sizes != Sizes)
                return false;

            var comparer = EqualityComparer<T>.Default;
            if (Rank == 0)
            {
                return comparer.Equals(GetElement(), other.GetElement());
            }
            else
            {
                foreach (var index in Box.EnumerateFromLoToHi(Sizes.ToArray()))
                {
                    if (!comparer.Equals(ElementAt(index), other.ElementAt(index)))
                        return false;
                }

                return true;
            }
        }

        public ITypeInstanceTraits<Box<T>> InstanceTraits
        {
            get { throw new NotImplementedException(); }
        }

        public override bool Equals(object obj)
        {
            var other = obj as Box<T>;
            if (other == null)
                return false;

            return Equals(other);
        }

        public override int GetHashCode()
        {
            return this.Enumerate().Aggregate(Sizes.GetHashCode(), (x, y) => x.RotateLeft(1) ^ y.GetHashCode());
        }

        public override string ToString()
        {
            return "box(" + string.Join(" x ", Sizes) + ")";
        }

        public static bool operator ==(Box<T> a, Box<T> b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(Box<T> a, Box<T> b)
        {
            if (a == null && b == null)
                return false;
            if (a == null || b == null)
                return true;

            return !a.Equals(b);
        }
    }

    class BoxProjection<T> : Box<T>
    {
        private Box<T> _source;
        private IndexSpec _index;

        public BoxProjection(Box<T> source, IndexSpec index)
        {
            Contract.Requires<ArgumentNullException>(source != null, "source");
            Contract.Requires<ArgumentNullException>(index != null, "indices");
            Contract.Requires<ArgumentException>(source.Rank >= index.MinSourceDimension, "Index length is greater than source rank.");

            _source = source;
            _index = index.Project(FullSourceIndex);
        }

        public override Vector<int> Sizes
        {
            get 
            {
                return (from ds in _index.Indices
                        where ds.Kind == DimSpec.EKind.Range
                        select (int)((Range)ds).Size).ToArray().AsVector();
            }
        }

        private IndexSpec FullSourceIndex
        {
            get
            {
                return new IndexSpec(_source.Sizes.Select(_ => (DimSpec)0.To(_ - 1)).ToArray());
            }
        }

        public override int Rank
        {
            get { return _index.MinTargetDimension; }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get 
            {
                Contract.Requires<ArgumentNullException>(indices != null, "indices");

                var indexToApply = new IndexSpec(indices);
                var newIndex = indexToApply.Project(_index);
                return new BoxProjection<T>(_source, newIndex);
            }
        }

        private static int GetIndex(DimSpec ds)
        {
            switch (ds.Kind)
            {
                case DimSpec.EKind.Index: return (int)ds;
                case DimSpec.EKind.Range: return ((Range)ds).FirstBound;
                default: throw new NotImplementedException();
            }
        }

        public override T GetElement()
        {
            if (_index.Indices.Any(i => i.Kind == DimSpec.EKind.Range && ((Range)i).Size != 1))
                throw new InvalidOperationException("Projection does not describe a single point.");

            int[] index = _index.Indices.Select(i => GetIndex(i)).ToArray();
            return _source.ElementAt(index);
        }
    }

    class DenseBox<T> : Box<T>
    {
        private Array _data;

        public DenseBox(Array data)
        {
            Contract.Requires<ArgumentNullException>(data != null, "data");

            _data = data;
        }

        public override Vector<int> Sizes
        {
            get 
            {
                int[] sizes = new int[_data.Rank];
                for (int i = 0; i < sizes.Length; i++)
                    sizes[i] = _data.GetLength(i);
                return Vector.AsVector(sizes);
            }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get { return new BoxProjection<T>(this, new IndexSpec(indices)); }
        }

        public override T GetElement()
        {
            if (Sizes.Any(_ => _ != 1))
                throw new InvalidOperationException("Data does not describe a single point.");

            return (T)_data.GetValue(new int[_data.Rank]);
        }

        public override T ElementAt(params int[] indices)
        {
            return (T)_data.GetValue(indices);
        }
    }

    class Box0<T> : Box<T>
    {
        private T _element;

        public Box0(T element)
        {
            _element = element;
        }

        public override Vector<int> Sizes
        {
            get { return Vector.Create<int>(); }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get 
            {
                if (indices.Length > 0)
                    throw new ArgumentException("Box describes a single data point, no index applicable.");

                return this;
            }
        }

        public override T GetElement()
        {
            return _element;
        }

        public override T ElementAt(params int[] indices)
        {
            if (indices.Length > 0)
                throw new ArgumentException("Box describes a single data point, no index applicable.");

            return _element;
        }
    }

    class Box1<T> : Box<T>
    {
        private IIndexable<T> _source;

        public Box1(IIndexable<T> source)
        {
            _source = source;
        }

        public override Vector<int> Sizes
        {
            get { return Vector.Create(_source.Size); }
        }

        public override int Rank
        {
            get { return 1; }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get { return new BoxProjection<T>(this, new IndexSpec(indices)); }
        }

        public override T GetElement()
        {
            if (_source.Size != 1)
                throw new InvalidOperationException("Data does not describe a single point.");

            return _source[0];
        }

        public override T ElementAt(params int[] indices)
        {
            Contract.Requires<ArgumentNullException>(indices != null, "indices");
            Contract.Requires<ArgumentException>(indices.Length == 1, "indices must contain exactly one element.");
            return _source[indices[0]];
        }
    }

    class Box2<T> : Box<T>
    {
        private IMatrixIndexable<T> _source;

        public Box2(IMatrixIndexable<T> source)
        {
            _source = source;
        }

        public override Vector<int> Sizes
        {
            get { return Vector.Create(_source.Size0, _source.Size1); }
        }

        public override int Rank
        {
            get { return 2; }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get { return new BoxProjection<T>(this, new IndexSpec(indices)); }
        }

        public override T GetElement()
        {
            if (_source.Size0 != 1 || _source.Size1 != 1)
                throw new InvalidOperationException("Data does not describe a single point.");

            return _source[0, 0];
        }

        public override T ElementAt(params int[] indices)
        {
            Contract.Requires<ArgumentNullException>(indices != null, "indices");
            Contract.Requires<ArgumentException>(indices.Length != 2, "indices must contain exactly two elements.");
            return _source[indices[0], indices[1]];
        }
    }

    class BoxWrapper<T> : Box<T>
    {
        private IMultiIndexable<T> _nddata;

        public BoxWrapper(IMultiIndexable<T> nddata)
        {
            _nddata = nddata;
        }

        public override Vector<int> Sizes
        {
            get { return _nddata.Sizes; }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get { return _nddata[indices].AsBox(); }
        }

        public override T GetElement()
        {
            return _nddata.GetElement();
        }

        public override T ElementAt(params int[] indices)
        {
            return _nddata[indices.Select(i => (DimSpec)i).ToArray()].GetElement();
        }
    }

    class BoxTransposition<T> : Box<T>
    {
        private IMultiIndexable<T> _source;
        private int[] _newDimensions;

        public BoxTransposition(IMultiIndexable<T> source, int[] newDimensions)
        {
            Contract.Requires<ArgumentNullException>(source != null, "source");
            Contract.Requires<ArgumentNullException>(newDimensions != null, "newDimensions");
            Contract.Requires<ArgumentException>(newDimensions.Length >= 1, "newDimensions must contain at least one element.");
            Contract.Requires<ArgumentOutOfRangeException>(
                newDimensions.OrderBy(x => x).Equals(Enumerable.Range(0, source.Sizes.Size)),
                "newDimensions is not a permutation of source rank elements.");

            _source = source;
            _newDimensions = (int[])newDimensions.Clone();
        }

        public override Vector<int> Sizes
        {
            get { return _newDimensions.Select(x => _source.Sizes[x]).ToArray().AsVector(); }
        }

        public override int Rank
        {
            get { return _newDimensions.Length; }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get { return new BoxProjection<T>(this, new IndexSpec(indices)); }
        }

        public override T GetElement()
        {
            if (Sizes.Any(s => s != 1))
                throw new InvalidOperationException("Data does not describe a single point.");

            return ElementAt(new int[Rank]);
        }

        public override T ElementAt(params int[] indices)
        {
            Contract.Requires<ArgumentNullException>(indices != null, "indices");
            Contract.Requires<ArgumentException>(indices.Length == Rank, "Length of indices must equal the rank.");

            DimSpec[] translatedIndices = new DimSpec[Rank];
            for (int i = 0; i < indices.Length; i++)
                translatedIndices[_newDimensions[i]] = indices[i];
            return _source[translatedIndices].GetElement();
        }
    }

    class SelectBox<TSource, TTarget> : Box<TTarget>
    {
        private IMultiIndexable<TSource> _source;
        private Func<TSource, TTarget> _selector;

        public SelectBox(IMultiIndexable<TSource> source, Func<TSource, TTarget> selector)
        {
            _source = source;
            _selector = selector;
        }

        public override Vector<int> Sizes
        {
            get { return _source.Sizes; }
        }

        public override Box<TTarget> this[params DimSpec[] indices]
        {
            get { return new SelectBox<TSource, TTarget>(_source[indices], _selector); }
        }

        public override TTarget GetElement()
        {
            return _selector(_source.GetElement());
        }

        public override TTarget ElementAt(params int[] indices)
        {
            var box = _source as Box<TSource>;
            if (box != null)
                return _selector(box.ElementAt(indices));
            else
                return _selector(_source[indices.Select(i => (DimSpec)i).ToArray()].GetElement());
        }
    }

    class ZeroBox<T> : Box<T>
    {
        private Vector<int> _sizes;
        private T _zeroElem;

        public ZeroBox(Vector<int> sizes, T zeroElem)
        {
            Contract.Requires<ArgumentNullException>(sizes != null, "sizes");

            _sizes = sizes;
            _zeroElem = zeroElem;
        }

        public override Vector<int> Sizes
        {
            get { return _sizes; }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get { return new BoxProjection<T>(this, new IndexSpec(indices)); }
        }

        public override T GetElement()
        {
            if (_sizes.Size > 0 && _sizes.Any(s => s != 1))
                throw new InvalidOperationException("Box does not describe a single data point.");

            return _zeroElem;
        }

        public override T ElementAt(params int[] indices)
        {
            return _zeroElem;
        }
    }

    class DiagBox<T> : Box<T>
    {
        private int _rank;
        private int _size;
        private T _diagElem;
        private T _zeroElem;

        public DiagBox(int rank, int size, T diagElem)
        {
            Contract.Requires<ArgumentOutOfRangeException>(rank >= 0, "rank is negative");
            Contract.Requires<ArgumentOutOfRangeException>(size >= 0, "size is negative");
            
            _rank = rank;
            _size = size;
            _diagElem = diagElem;
            _zeroElem = TypeTraits.GetInstanceTraits(diagElem).GetZero();
        }

        public override int Rank
        {
            get { return _rank; }
        }

        public override Vector<int> Sizes
        {
            get { return Enumerable.Repeat(_size, _rank).ToArray().AsVector(); }
        }

        public override Box<T> this[params DimSpec[] indices]
        {
            get { return new BoxProjection<T>(this, new IndexSpec(indices)); }
        }

        public override T GetElement()
        {
            if (_rank > 0 && _size != 1)
                throw new InvalidOperationException("Box does not describe a single point of data.");

            return _diagElem;
        }

        public override T ElementAt(params int[] indices)
        {
            if (indices.Skip(1).All(i => i == indices[0]))
                return _diagElem;
            else
                return _zeroElem;
        }
    }
}

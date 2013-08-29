using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.DataTypes;

namespace SystemSharp.Components.DSP
{
    public interface IRepresentative
    {
        object Representant { get; }
    }

    public interface ISeries: IRepresentative
    {
        object this[int i] { get; }
    }

    public interface IRepresentative<T>: IRepresentative
    {
        T Representant { get; }
    }

    public interface ISeries<T>: 
        IRepresentative<T>,
        ISeries
    {
        T this[int i] { get; }
    }

    public abstract class AbstractSeries<T> : ISeries<T>
    {
        public abstract T this[int i] { get; }
        public abstract T Representant { get; }

        object IRepresentative.Representant
        {
            get { return Representant; }
        }

        object ISeries.this[int i]
        {
            get { return this[i]; }
        }
    }

    public class SamplingSoure
    {
        public ISeries Source { get; private set; }
        public Fraction SamplingRatio { get; private set; }

        public SamplingSoure(ISeries soure, Fraction samplingRatio)
        {
            Source = soure;
            SamplingRatio = samplingRatio;
        }
    }

    public interface ISamplingTransformation
    {
        SamplingSoure[] SamplingSources { get; }
    }

    public interface IVector<T> : IRepresentative<T>
    {
        T this[int i] { get; }
        int Length { get; }
    }

    public abstract class AbstractVector<T> : IVector<T>
    {
        public abstract T this[int i] { get; }
        public abstract int Length { get; }
        public abstract T Representant { get; }

        object IRepresentative.Representant
        {
            get { return Representant; }
        }
    }

    public interface IMatrix<T> : IRepresentative<T>
    {
        T this[int i, int j] { get; }
        int Width { get; }
        int Height { get; }
    }

    public abstract class AbstractMatrix<T> : IMatrix<T>
    {
        public abstract T this[int i, int j] { get; }
        public abstract int Width { get; }
        public abstract int Height { get; }
        public abstract T Representant { get; }

        object IRepresentative.Representant
        {
            get { return Representant; }
        }
    }

    public static class Vector
    {
        private class ArrayBackedVector<T> : AbstractVector<T>
        {
            private T[] _array;

            public ArrayBackedVector(T[] array)
            {
                _array = array;
            }

            public override T this[int i]
            {
                get { return _array[i]; }
            }

            public override int Length
            {
                get { return _array.Length; }
            }

            public override T Representant
            {
                get { return _array[0]; }
            }
        }

        private class SubVector<T> : AbstractVector<T>
        {
            private IVector<T> _root;
            private int _first;
            private int _length;

            public SubVector(IVector<T> root, int first, int length)
            {
                _root = root;
                _first = first;
                _length = length;
            }

            public override T this[int i]
            {
                get { return _root[i + _first]; }
            }

            public override int Length
            {
                get { return _length; }
            }

            public override T Representant
            {
                get { return _root.Representant; }
            }
        }

        private class PairwiseProductVector<T> : AbstractVector<T>
        {
            private IVector<T> _root0;
            private IVector<T> _root1;

            public PairwiseProductVector(IVector<T> root0, IVector<T> root1)
            {
                _root0 = root0;
                _root1 = root1;
            }

            public override T this[int i]
            {
                get { return (dynamic)_root0[i] * _root1[i]; }
            }

            public override int Length
            {
                get { return _root0.Length; }
            }

            public override T Representant
            {
                get { return (dynamic)_root0.Representant + _root1.Representant; }
            }
        }

        public static IVector<T> AsVector<T>(this T[] array)
        {
            return new ArrayBackedVector<T>(array);
        }

        public static IVector<T> Crop<T>(this IVector<T> vector, int first, int length)
        {
            return new SubVector<T>(vector, first, length);
        }

        public static T Aggregate<T>(this IVector<T> vector, Func<T, T, T> fn)
        {
            if (vector.Length == 1)
            {
                return vector[0];
            }
            else
            {
                T cur = fn(vector[0], vector[1]);
                for (int i = 1; i < vector.Length; i++)
                    cur = fn(cur, vector[i]);
                return cur;
            }
        }

        public static T Sum<T>(this IVector<T> vector)
        {
            return vector.Aggregate((a, b) => (dynamic)a + b);
        }

        public static IVector<T> PairMul<T>(this IVector<T> a, IVector<T> b)
        {
            return new PairwiseProductVector<T>(a, b);
        }
    }

    public static class Matrix
    {
        private class ConstMatrix<T> : AbstractMatrix<T>
        {
            private T _value;
            private int _width;
            private int _height;

            public ConstMatrix(T value, int width, int height)
            {
                _value = value;
                _width = width;
                _height = height;
            }

            public override T this[int i, int j]
            {
                get { return _value; }
            }

            public override int Width
            {
                get { return _width; }
            }

            public override int Height
            {
                get { return _height; }
            }

            public override T Representant
            {
                get { return _value; }
            }
        }

        private class ArrayBackedMatrix<T> : AbstractMatrix<T>
        {
            private T[,] _array;

            public ArrayBackedMatrix(T[,] array)
            {
                _array = array;
            }

            public override T this[int i, int j]
            {
                get { return _array[i, j]; }
            }

            public override int Width
            {
                get { return _array.GetLength(1); }
            }

            public override int Height
            {
                get { return _array.GetLength(0); }
            }

            public override T Representant
            {
                get { return _array[0, 0]; }
            }
        }

        private class TransposedMatrix<T> : AbstractMatrix<T>
        {
            private IMatrix<T> _root;

            public TransposedMatrix(IMatrix<T> root)
            {
                _root = root;
            }

            public override T this[int i, int j]
            {
                get { return _root[j, i]; }
            }

            public override int Width
            {
                get { return _root.Height; }
            }

            public override int Height
            {
                get { return _root.Width; }
            }

            public override T Representant
            {
                get { return _root.Representant; }
            }
        }

        private class SubMatrix<T> : AbstractMatrix<T>
        {
            private IMatrix<T> _root;
            private int _row;
            private int _col;
            private int _width;
            private int _height;

            public SubMatrix(IMatrix<T> root, int row, int col, int width, int height)
            {
                _root = root;
                _row = row;
                _col = col;
                _width = width;
                _height = height;
            }

            public override T this[int i, int j]
            {
                get { return _root[i + _row, j + _col]; }
            }

            public override int Width
            {
                get { return _width; }
            }

            public override int Height
            {
                get { return _height; }
            }

            public override T Representant
            {
                get { return _root.Representant; }
            }
        }

        private class MatrixRow<T> : AbstractVector<T>
        {
            private IMatrix<T> _root;
            private int _row;

            public MatrixRow(IMatrix<T> root, int row)
            {
                _root = root;
                _row = row;
            }

            public override T this[int i]
            {
                get { return _root[_row, i]; }
            }

            public override int Length
            {
                get { return _root.Width; }
            }

            public override T Representant
            {
                get { return _root.Representant; }
            }
        }

        private class MatrixCol<T> : AbstractVector<T>
        {
            private IMatrix<T> _root;
            private int _col;

            public MatrixCol(IMatrix<T> root, int col)
            {
                _root = root;
                _col = col;
            }

            public override T this[int i]
            {
                get { return _root[i, _col]; }
            }

            public override int Length
            {
                get { return _root.Height; }
            }

            public override T Representant
            {
                get { return _root.Representant; }
            }
        }

        private class FnMatrix<T1, T2, TF> : AbstractMatrix<TF>
        {
            private IMatrix<T1> _m1;
            private IMatrix<T2> _m2;
            private Func<T1, T2, TF> _fn;

            public FnMatrix(IMatrix<T1> m1, IMatrix<T2> m2, Func<T1, T2, TF> fn)
            {
                _m1 = m1;
                _m2 = m2;
                _fn = fn;
            }

            public override TF this[int i, int j]
            {
                get { return _fn(_m1[i, j], _m2[i, j]); }
            }

            public override int Width
            {
                get { return _m1.Width; }
            }

            public override int Height
            {
                get { return _m2.Height; }
            }

            public override TF Representant
            {
                get { return _fn(_m1.Representant, _m2.Representant); }
            }
        }

        private class SumMatrix<T> : AbstractMatrix<T>
        {
            private IMatrix<T> _root0;
            private IMatrix<T> _root1;

            public SumMatrix(IMatrix<T> root0, IMatrix<T> root1)
            {
                _root0 = root0;
                _root1 = root1;
            }

            public override T this[int i, int j]
            {
                get { return (dynamic)_root0[i, j] + _root1[i, j]; }
            }

            public override int Width
            {
                get { return _root0.Width; }
            }

            public override int Height
            {
                get { return _root1.Width; }
            }

            public override T Representant
            {
                get { return (dynamic)_root0.Representant + _root1.Representant; }
            }
        }

        private class PairwiseProductMatrix<T> : AbstractMatrix<T>
        {
            private IMatrix<T> _root0;
            private IMatrix<T> _root1;

            public PairwiseProductMatrix(IMatrix<T> root0, IMatrix<T> root1)
            {
                _root0 = root0;
                _root1 = root1;
            }

            public override T this[int i, int j]
            {
                get { return (dynamic)_root0[i, j] * _root1[i, j]; }
            }

            public override int Width
            {
                get { return _root0.Width; }
            }

            public override int Height
            {
                get { return _root0.Height; }
            }

            public override T Representant
            {
                get { return _root0.Representant; }
            }
        }

        private class ProductMatrix<T> : AbstractMatrix<T>
        {
            private IMatrix<T> _left;
            private IMatrix<T> _right;

            public ProductMatrix(IMatrix<T> left, IMatrix<T> right)
            {
                _left = left;
                _right = right;
            }

            public override T this[int i, int j]
            {
                get { return _left.GetRow(i).PairMul(_right.GetCol(j)).Sum(); }
            }

            public override int Width
            {
                get { return _right.Width; }
            }

            public override int Height
            {
                get { return _left.Height; }
            }

            public override T Representant
            {
                get { return this[0, 0]; }
            }
        }

        private class SerializedMatrix<T> : AbstractVector<T>
        {
            private IMatrix<T> _root;

            public SerializedMatrix(IMatrix<T> root)
            {
                _root = root;
            }

            public override T this[int i]
            {
                get { return _root[i / _root.Width, i % _root.Width]; }
            }

            public override int Length
            {
                get { return _root.Width * _root.Height; }
            }

            public override T Representant
            {
                get { return _root.Representant; }
            }
        }

        private class TiledMatrix<T> : AbstractMatrix<IMatrix<T>>
        {
            private IMatrix<T> _root;
            private int _tileWidth;
            private int _tileHeight;
            private int _xStride;
            private int _yStride;

            public TiledMatrix(IMatrix<T> root, int tileWidth, int tileHeight, int xStride, int yStride)
            {
                _root = root;
                _tileWidth = tileWidth;
                _tileHeight = tileHeight;
                _xStride = xStride;
                _yStride = yStride;
            }

            public override IMatrix<T> this[int i, int j]
            {
                get { return _root.Crop(i * _yStride, j * _xStride, _tileWidth, _tileHeight); }
            }

            public override int Width
            {
                get { return _root.Width / _xStride; }
            }

            public override int Height
            {
                get { return _root.Height / _yStride; }
            }

            public override IMatrix<T> Representant
            {
                get { return Constant(_root.Representant, _tileWidth, _tileHeight); }
            }
        }

        public static IMatrix<T> Constant<T>(T value, int width, int height)
        {
            return new ConstMatrix<T>(value, width, height);
        }

        public static IMatrix<T> AsMatrix<T>(this T[,] array)
        {
            return new ArrayBackedMatrix<T>(array);
        }

        public static IMatrix<T> Transpose<T>(this IMatrix<T> matrix)
        {
            return new TransposedMatrix<T>(matrix);
        }

        public static IMatrix<T> Crop<T>(this IMatrix<T> matrix, int row, int col, int height, int width)
        {
            return new SubMatrix<T>(matrix, row, col, width, height);
        }

        public static IVector<T> GetRow<T>(this IMatrix<T> matrix, int row)
        {
            return new MatrixRow<T>(matrix, row);
        }

        public static IVector<T> GetCol<T>(this IMatrix<T> matrix, int col)
        {
            return new MatrixCol<T>(matrix, col);
        }

        public static IMatrix<TF> Combine<T1, T2, TF>(this IMatrix<T1> m1, IMatrix<T2> m2, Func<T1, T2, TF> fn)
        {
            return new FnMatrix<T1, T2, TF>(m1, m2, fn);
        }

        public static IMatrix<T> Add<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            return new SumMatrix<T>(a, b);
        }

        public static IMatrix<T> Sub<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            return a.Combine(b, (x, y) => (T)((dynamic)x - y));
        }

        public static IMatrix<T> PairMul<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            return new PairwiseProductMatrix<T>(a, b);
        }

        public static IVector<T> Serialize<T>(this IMatrix<T> matrix)
        {
            return new SerializedMatrix<T>(matrix);
        }

        public static T Aggregate<T>(this IMatrix<T> matrix, Func<T, T, T> fn)
        {
            return matrix.Serialize().Aggregate(fn);
        }

        public static T Sum<T>(this IMatrix<T> matrix)
        {
            return matrix.Aggregate((a, b) => (dynamic)a + b);
        }

        public static IMatrix<T> Mul<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            return new ProductMatrix<T>(a, b);
        }

        public static T Convolve<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            return a.PairMul(b).Sum();
        }

        public static IMatrix<T> Dx<T>(this IMatrix<T> matrix)
        {
            return matrix.Crop(0, 1, matrix.Width - 1, matrix.Height).Sub(
                matrix.Crop(0, 0, matrix.Width - 1, matrix.Height));
        }

        public static IMatrix<T> Dy<T>(this IMatrix<T> matrix)
        {
            return matrix.Crop(1, 0, matrix.Width, matrix.Height - 1).Sub(
                matrix.Crop(0, 0, matrix.Width, matrix.Height - 1));
        }
    }

    public static class Series
    {
        private class ArrayBackedSeries<T> : AbstractSeries<T>
        {
            private T[] _array;

            public ArrayBackedSeries(T[] array)
            {
                Contract.Requires<ArgumentNullException>(array != null);
                Contract.Requires<ArgumentException>(array.Length > 0, "Array must not be empty");

                _array = array;
            }

            public override T this[int i]
            {
                get { return _array[i]; }
            }

            public override T Representant
            {
                get { return _array[0]; }
            }
        }

        private class ImageSeries<T> : 
            AbstractSeries<IMatrix<T>>,
            ISamplingTransformation
        {
            private class SeriesImage<T> : AbstractMatrix<T>
            {
                private ISeries<T> _root;
                private int _width;
                private int _height;
                private int _offset;

                public SeriesImage(ISeries<T> root, int width, int height, int offset)
                {
                    _root = root;
                    _width = width;
                    _height = height;
                    _offset = offset;
                }

                public override T this[int i, int j]
                {
                    get { return _root[i * _width + j + _offset]; }
                }

                public override int Width
                {
                    get { return _width; }
                }

                public override int Height
                {
                    get { return _height; }
                }

                public override T Representant
                {
                    get { return _root.Representant; }
                }
            }

            private ISeries<T> _root;
            private int _width;
            private int _height;

            public ImageSeries(ISeries<T> root, int width, int height)
            {
                _root = root;
                _width = width;
                _height = height;
            }

            public override IMatrix<T> this[int i]
            {
                get
                {
                    return new SeriesImage<T>(_root, _width, _height, i * _width * _height);
                }
            }

            public override IMatrix<T> Representant
            {
                get { return Matrix.Constant(_root.Representant, _width, _height); }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { new SamplingSoure(_root, Fraction.Inverted(_width * _height)) }; }
            }
        }

        private class ConvertedSeries<T, TConv> : 
            AbstractSeries<TConv>,
            ISamplingTransformation
        {
            private ISeries<T> _root;
            private Func<T, TConv> _convfn;

            public ConvertedSeries(ISeries<T> root, Func<T, TConv> convfn)
            {
                _root = root;
                _convfn = convfn;
            }

            public override TConv this[int i]
            {
                get { return _convfn(_root[i]); }
            }

            public override TConv Representant
            {
                get { return _convfn(_root.Representant); }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { new SamplingSoure(_root, 1) }; }
            }
        }

        private class CombinedSeries<T1, T2, TF> : 
            AbstractSeries<TF>,
            ISamplingTransformation
        {
            private ISeries<T1> _a;
            private ISeries<T2> _b;
            private Func<T1, T2, TF> _fn;

            public CombinedSeries(ISeries<T1> a, ISeries<T2> b, Func<T1, T2, TF> fn)
            {
                _a = a;
                _b = b;
                _fn = fn;
            }

            public override TF this[int i]
            {
                get { return _fn(_a[i], _b[i]); }
            }

            public override TF Representant
            {
                get { return _fn(_a.Representant, _b.Representant); }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { new SamplingSoure(_a, 1), new SamplingSoure(_b, 1) }; }
            }
        }

        private class CombinedSeries<T1, T2, T3, TF> : 
            AbstractSeries<TF>,
            ISamplingTransformation
        {
            private ISeries<T1> _a;
            private ISeries<T2> _b;
            private ISeries<T3> _c;
            private Func<T1, T2, T3, TF> _fn;

            public CombinedSeries(ISeries<T1> a, ISeries<T2> b, ISeries<T3> c, Func<T1, T2, T3, TF> fn)
            {
                _a = a;
                _b = b;
                _c = c;
                _fn = fn;
            }

            public override TF this[int i]
            {
                get { return _fn(_a[i], _b[i], _c[i]); }
            }

            public override TF Representant
            {
                get { return _fn(_a.Representant, _b.Representant, _c.Representant); }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { new SamplingSoure(_a, 1), new SamplingSoure(_b, 1), new SamplingSoure(_c, 1) }; }
            }
        }

        private class CombinedSeries<T1, T2, T3, T4, TF> : 
            AbstractSeries<TF>,
            ISamplingTransformation
        {
            private ISeries<T1> _a;
            private ISeries<T2> _b;
            private ISeries<T3> _c;
            private ISeries<T4> _d;
            private Func<T1, T2, T3, T4, TF> _fn;

            public CombinedSeries(ISeries<T1> a, ISeries<T2> b, ISeries<T3> c, ISeries<T4> d, Func<T1, T2, T3, T4, TF> fn)
            {
                _a = a;
                _b = b;
                _c = c;
                _d = d;
                _fn = fn;
            }

            public override TF this[int i]
            {
                get { return _fn(_a[i], _b[i], _c[i], _d[i]); }
            }

            public override TF Representant
            {
                get { return _fn(_a.Representant, _b.Representant, _c.Representant, _d.Representant); }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { 
                    new SamplingSoure(_a, 1), new SamplingSoure(_b, 1), 
                    new SamplingSoure(_c, 1), new SamplingSoure(_c, 1) };
                }
            }
        }

        private class DelayedSeries<T> : 
            AbstractSeries<T>,
            ISamplingTransformation
        {
            private ISeries<T> _root;
            private int _delay;

            public DelayedSeries(ISeries<T> root, int delay)
            {
                _root = root;
                _delay = delay;
            }

            public override T this[int i]
            {
                get { return _root[i + _delay]; }
            }

            public override T Representant
            {
                get { return _root.Representant; }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { new SamplingSoure(_root, 1) }; }
            }
        }

        private class MultiplexedSeries<T> : 
            AbstractSeries<IVector<T>>,
            ISamplingTransformation
        {
            private ISeries<T>[] _root;

            public MultiplexedSeries(ISeries<T>[] root)
            {
                _root = root;
            }

            public override IVector<T> this[int i]
            {
                get { return _root.Select(r => r[i]).ToArray().AsVector(); }
            }

            public override IVector<T> Representant
            {
                get { return _root.Select(r => r.Representant).ToArray().AsVector(); }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return _root.Select(_ => new SamplingSoure(_, 1)).ToArray(); }
            }
        }

        public static ISeries<T> AsSeries<T>(this T[] array)
        {
            return new ArrayBackedSeries<T>(array);
        }

        public static ISeries<IMatrix<T>> ToMatrixSeries<T>(this ISeries<T> series, int width, int height)
        {
            return new ImageSeries<T>(series, width, height);
        }

        public static ISeries<TConv> Convert<T, TConv>(this ISeries<T> series, Func<T, TConv> convfn)
        {
            return new ConvertedSeries<T, TConv>(series, convfn);
        }

        public static ISeries<TF> Combine<T1, T2, TF>(this ISeries<T1> a, ISeries<T2> b, Func<T1, T2, TF> fn)
        {
            return new CombinedSeries<T1, T2, TF>(a, b, fn);
        }

        public static ISeries<TF> Combine<T1, T2, T3, TF>(ISeries<T1> a, ISeries<T2> b, ISeries<T3> c, Func<T1, T2, T3, TF> fn)
        {
            return new CombinedSeries<T1, T2, T3, TF>(a, b, c, fn);
        }

        public static ISeries<TF> Combine<T1, T2, T3, T4, TF>(ISeries<T1> a, ISeries<T2> b, ISeries<T3> c, ISeries<T4> d, Func<T1, T2, T3, T4, TF> fn)
        {
            return new CombinedSeries<T1, T2, T3, T4, TF>(a, b, c, d, fn);
        }

        public static ISeries<T> D<T>(this ISeries<T> series, int delay)
        {
            return new DelayedSeries<T>(series, delay);
        }

        public static ISeries<IVector<T>> Multiplex<T>(params ISeries<T>[] series)
        {
            return new MultiplexedSeries<T>(series);
        }
    }

    public static class MatrixSeries
    {
        private class SerializedVectorSeries<T> : 
            AbstractSeries<T>,
            ISamplingTransformation
        {
            private ISeries<IVector<T>> _root;
            private int _length;

            public SerializedVectorSeries(ISeries<IVector<T>> root)
            {
                _root = root;
                _length = root.Representant.Length;
            }

            public override T this[int i]
            {
                get { return _root[i / _length][i % _length]; }
            }

            public override T Representant
            {
                get { return _root.Representant.Representant; }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { new SamplingSoure(_root, _length) }; }
            }
        }

        private class SerializedMatrixSeries<T> : 
            AbstractSeries<T>,
            ISamplingTransformation
        {
            private ISeries<IMatrix<T>> _root;
            private int _width;
            private int _height;
            private int _area;

            public SerializedMatrixSeries(ISeries<IMatrix<T>> root)
            {
                _root = root;
                _width = root.Representant.Width;
                _height = root.Representant.Height;
                _area = _width * _height;
            }

            public override T this[int i]
            {
                get { return _root[i % _area][i / _width, i % _width]; }
            }

            public override T Representant
            {
                get { return _root.Representant.Representant; }
            }

            public SamplingSoure[] SamplingSources
            {
                get { return new SamplingSoure[] { new SamplingSoure(_root, _area) }; }
            }
        }

        public static ISeries<T> Serialize<T>(this ISeries<IVector<T>> series)
        {
            return new SerializedVectorSeries<T>(series);
        }

        public static ISeries<T> Serialize<T>(this ISeries<IMatrix<T>> series)
        {
            return new SerializedMatrixSeries<T>(series);
        }

        public static ISeries<IMatrix<T>> Add<T>(this ISeries<IMatrix<T>> a, ISeries<IMatrix<T>> b)
        {
            return a.Combine(b, (x, y) => x.Add(y));
        }

        public static ISeries<IMatrix<T>> Sub<T>(this ISeries<IMatrix<T>> a, ISeries<IMatrix<T>> b)
        {
            return a.Combine(b, (x, y) => x.Sub(y));
        }

        public static ISeries<IMatrix<T>> Dt<T>(this ISeries<IMatrix<T>> series)
        {
            return series.Sub(series.D(1));
        }

        public static ISeries<IVector<T>> LucasKanade<T>(this ISeries<IMatrix<T>> series)
        {
            var dt = series.Dt().Convert(_ => _.Serialize());
            var dx = series.Convert(_ => _.Dx().Serialize());
            var dy = series.Convert(_ => _.Dy().Serialize());
            var xx = dx.Combine(dx, (x0, x1) => x0.PairMul(x1).Sum());
            var xy = dx.Combine(dy, (x, y) => x.PairMul(y).Sum());
            var yy = dy.Combine(dy, (y0, y1) => y0.PairMul(y1).Sum());
            var xt = dx.Combine(dt, (x, t) => x.PairMul(t).Sum());
            var yt = dy.Combine(dt, (y, t) => y.PairMul(t).Sum());
            var det = Series.Combine(xx, xy, yy, (_0, _1, _2) => (T)((dynamic)_0 * _2 - (dynamic)_1 * _1));
            var vx = Series.Combine(xt, yt, xy, yy, (xt_, yt_, xy_, yy_) => (T)((dynamic)xy_ * yt_ - (dynamic)xt_ * yy_));
            var vy = Series.Combine(xx, xy, xt, yt, (xx_, xy_, xt_, yt_) => (T)((dynamic)xt_ * xy_ - (dynamic)xx_ * yt_));
            return Series.Multiplex(vx, vy, det);
        }
    }
}

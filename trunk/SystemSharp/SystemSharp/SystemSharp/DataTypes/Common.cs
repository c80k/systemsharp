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
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using SystemSharp.Collections;

namespace SystemSharp.DataTypes
{
    /// <summary>
    /// This enumeration is used to model the direction of a range.
    /// </summary>
    public enum EDimDirection
    {
        /// <summary>
        /// Increasing range
        /// </summary>
        To,
        /// <summary>
        /// Decreasing range
        /// </summary>
        Downto
    }

    /// <summary>
    /// A discrete range.
    /// </summary>
    public struct Range:
        IEquatable<Range>,
        IComparable<Range>
    {
        /// <summary>
        /// Represents the possible relation of two ranges.
        /// </summary>
        public enum EComparisonResult
        {
            /// <summary>
            /// Ranges have different directions and are therefore unrelated.
            /// </summary>
            Unrelated,

            /// <summary>
            /// All values of the first range are smaller than the values of the second range.
            /// </summary>
            Left,

            /// <summary>
            /// Both ranges overlap, and the smallest value of the first range is smaller than the
            /// smallest value of the second range.
            /// </summary>
            LeftOverlap,

            /// <summary>
            /// The first range is a true subset of the second range.
            /// </summary>
            FirstIncluded,

            /// <summary>
            /// The second range is a true subset of the first range.
            /// </summary>
            SecondIncluded,

            /// <summary>
            /// Both ranges are equal.
            /// </summary>
            Equal,

            /// <summary>
            /// Both ranges overlap, and the smallest value of the second range is smaller than the
            /// smallest value of the first range.
            /// </summary>
            RightOverlap,

            /// <summary>
            /// All values of the first range are greater than the values of the second range.
            /// </summary>
            Right
        }

        /// <summary>
        /// Direction of range (concept borrowed from VHDL)
        /// </summary>
        public EDimDirection Direction { get; private set; }

        /// <summary>
        /// First index of range (including)
        /// </summary>
        public int FirstBound { get; private set; }

        /// <summary>
        /// Last index of range (including)
        /// </summary>
        public int SecondBound { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="firstIndex">first range index (including)</param>
        /// <param name="secondIndex">last range index (including)</param>
        /// <param name="direction">direction of range</param>
        public Range(int firstIndex, int secondIndex, EDimDirection direction) :
            this()
        {
            Direction = direction;
            FirstBound = firstIndex;
            SecondBound = secondIndex;
        }

        /// <summary>
        /// Number of discrete indices which fall into this range.
        /// </summary>
        public long Size
        {
            get
            {
                switch (Direction)
                {
                    case EDimDirection.To: return SecondBound - FirstBound + 1;
                    case EDimDirection.Downto: return FirstBound - SecondBound + 1;
                    default: throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Returns the index of the original element if we apply this range as a projection and take element <paramref name="offset"/>
        /// from the projection result.
        /// </summary>
        public int Unproject(int offset)
        {
            if (offset < 0 || offset >= Size)
                throw new ArgumentException("Index not within range");

            switch (Direction)
            {
                case EDimDirection.To: return FirstBound + offset;
                case EDimDirection.Downto: return SecondBound + offset;
                default: throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the index of the resulting element if we apply this range as a projection to original element <paramref name="offset"/>.
        /// </summary>
        public int Project(int offset)
        {
            switch (Direction)
            {
                case EDimDirection.To:
                    if (offset < FirstBound || offset > SecondBound)
                        throw new ArgumentException("Index not within range");
                    return offset - FirstBound;

                case EDimDirection.Downto:
                    if (offset > FirstBound || offset < SecondBound)
                        throw new ArgumentException("Index not within range");
                    return offset - SecondBound;

                default: throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the original element range if we apply this range as a projection and take <paramref name="range"/>
        /// from the projection result.
        /// </summary>
        public Range Unproject(Range range)
        {
            return new Range(
                Unproject(range).FirstBound,
                Unproject(range).SecondBound,
                range.Direction);
        }

        /// <summary>
        /// Returns the resulting range if we apply this range as a projection to original <paramref name="range"/>.
        /// </summary>
        public Range Project(Range range)
        {
            return new Range(
                Project(range).FirstBound,
                Project(range).SecondBound,
                Direction);
        }

        /// <summary>
        /// Returns <c>true</c> iff this range contains <paramref name="index"/>.
        /// </summary>
        public bool Contains(int index)
        {
            switch (Direction)
            {
                case EDimDirection.Downto:
                    return index <= FirstBound && index >= SecondBound;

                case EDimDirection.To:
                    return index >= FirstBound && index <= SecondBound;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns <c>true</c> iff this range contains the specified range.
        /// </summary>
        public bool Contains(Range range)
        {
            var compared = Range.Compare(this, range);
            return compared == EComparisonResult.SecondIncluded ||
                compared == EComparisonResult.Equal;
        }

        /// <summary>
        /// Two ranges are defined to be equal iff they have the same direction and extactly the same first/last indices.
        /// </summary>
        public bool Equals(Range other)
        {
            return FirstBound == other.FirstBound &&
                SecondBound == other.SecondBound &&
                Direction == other.Direction;
        }

        public int CompareTo(Range other)
        {
            switch (Range.Compare(this, other))
            {
                case EComparisonResult.Left: return -1;
                case EComparisonResult.Right: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// Two ranges are defined to be equal iff they have the same direction and extactly the same first/last indices.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is Range)
                return Equals((Range)obj);
            else
                return false;
        }

        public override int GetHashCode()
        {
            return (int)(FirstBound ^ SecondBound) ^ Direction.GetHashCode();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(FirstBound);
            switch (Direction)
            {
                case EDimDirection.Downto: sb.Append(" downto "); break;
                case EDimDirection.To: sb.Append(" to "); break;
                default: throw new NotImplementedException();
            }
            sb.Append(SecondBound);
            return sb.ToString();
        }

#if false
        /// <summary>
        /// Constructs a descending range.
        /// </summary>
        /// <param name="hi">upper index</param>
        /// <param name="lo">lower index</param>
        public static Range Downto(int hi, int lo)
        {
            return new Range(hi, lo, EDimDirection.Downto);
        }

        /// <summary>
        /// Constructs an ascending range.
        /// </summary>
        /// <param name="lo">lower index</param>
        /// <param name="hi">upper index</param>
        public static Range Upto(int lo, int hi)
        {
            return new Range(lo, hi, EDimDirection.To);
        }
#endif

        /// <summary>
        /// Compares to ranges with respect to all possible overlap cases.
        /// </summary>
        /// <param name="ra">first range</param>
        /// <param name="rb">second range</param>
        public static EComparisonResult Compare(Range ra, Range rb)
        {
            if (ra.Direction != rb.Direction)
                return EComparisonResult.Unrelated;

            int ralo, rahi, rblo, rbhi;
            switch (ra.Direction)
            {
                case EDimDirection.Downto:
                    ralo = ra.SecondBound;
                    rahi = ra.FirstBound;
                    rblo = rb.SecondBound;
                    rbhi = rb.FirstBound;
                    break;

                case EDimDirection.To:
                    ralo = ra.FirstBound;
                    rahi = ra.SecondBound;
                    rblo = rb.FirstBound;
                    rbhi = rb.SecondBound;
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (rahi < rblo)
                return EComparisonResult.Left;

            if (rbhi < ralo)
                return EComparisonResult.Right;

            if (ralo < rblo && rahi < rbhi)
                return EComparisonResult.LeftOverlap;

            if (rblo < ralo && rbhi < rahi)
                return EComparisonResult.RightOverlap;

            if (ralo > rblo || rahi < rbhi)
                return EComparisonResult.FirstIncluded;

            if (rblo > ralo || rbhi < rahi)
                return EComparisonResult.SecondIncluded;

            return EComparisonResult.Equal;
        }

        /// <summary>
        /// Returns the narrower of two ranges. One of the ranges must completely include the other one.
        /// </summary>
        /// <exception cref="ArgumentException">if neither range includes the other one</exception>
        public static Range Min(Range ra, Range rb)
        {
            switch (Compare(ra, rb))
            {
                case EComparisonResult.Unrelated:
                    throw new ArgumentException("Ranges must have equal directions");

                case EComparisonResult.FirstIncluded:
                case EComparisonResult.Equal:
                    return ra;

                case EComparisonResult.SecondIncluded:
                    return rb;

                default:
                    throw new ArgumentException("Ranges overlap, but no range is included within the other one.");
            }
        }

        /// <summary>
        /// Returns the wider of two ranges. One of the ranges must completely include the other one.
        /// </summary>
        /// <exception cref="ArgumentException">if neither range includes the other one</exception>
        public static Range Max(Range ra, Range rb)
        {
            switch (Compare(ra, rb))
            {
                case EComparisonResult.Unrelated:
                    throw new ArgumentException("Ranges must have equal directions");

                case EComparisonResult.FirstIncluded:
                case EComparisonResult.Equal:
                    return rb;

                case EComparisonResult.SecondIncluded:
                    return ra;

                default:
                    throw new ArgumentException("Ranges overlap, but no range is included within the other one");
            }
        }

        /// <summary>
        /// Adds offset <paramref name="offs"/> to range <paramref name="r"/>.
        /// </summary>
        public static Range operator+ (Range r, int offs)
        {
            return new Range(r.FirstBound + offs, r.SecondBound + offs, r.Direction);
        }

        /// <summary>
        /// Subtracts offset <paramref name="offs"/> to range <paramref name="r"/>.
        /// </summary>
        public static Range operator -(Range r, int offs)
        {
            return new Range(r.FirstBound - offs, r.SecondBound - offs, r.Direction);
        }

        /// <summary>
        /// Enumerates all discrete indices falling into this range in ascending or descending order,
        /// depending on the range direction.
        /// </summary>
        public IEnumerable<int> Values
        {
            get
            {
                switch (Direction)
                {
                    case EDimDirection.Downto:
                        return Enumerable.Range(SecondBound, (int)Size).Reverse();

                    case EDimDirection.To:
                        return Enumerable.Range(FirstBound, (int)Size);

                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }

    /// <summary>
    /// Provides extension methods for constructing <c>Range</c> instances.
    /// </summary>
    public static class RangeConstructor
    {
        /// <summary>
        /// Constructs an ascending range.
        /// </summary>
        /// <param name="from">lower index</param>
        /// <param name="to">upper index</param>
        public static Range To(this int from, int to)
        {
            return new Range(from, to, EDimDirection.To);
        }

        /// <summary>
        /// Constructs a descending range.
        /// </summary>
        /// <param name="from">upper index</param>
        /// <param name="downto">lower index</param>
        public static Range Downto(this int from, int downto)
        {
            return new Range(from, downto, EDimDirection.Downto);
        }
    }

    /// <summary>
    /// A dimensional specifier is either a single index or a range.
    /// </summary>
    public class DimSpec: 
        IEquatable<DimSpec>,
        IComparable<DimSpec>
    {
        /// <summary>
        /// Kind of specifier, index or range
        /// </summary>
        public enum EKind
        {
            Index,
            Range
        }

        /// <summary>
        /// Kind of this specifier, index or range
        /// </summary>
        public EKind Kind { get; private set; }

        private Range Index { get; set; }

        private DimSpec(int index)
        {
            Kind = EKind.Index;
            Index = Range.Upto(index, index);
        }

        private DimSpec(Range range)
        {
            Kind = EKind.Range;
            Index = range;
        }

        private DimSpec(EKind kind, Range range)
        {
            Kind = kind;
            Index = range;
        }

        /// <summary>
        /// Two dimensional specifiers are defined to be equal iff the are of the same kind with the same
        /// index or range, respectively.
        /// </summary>
        public bool Equals(DimSpec other)
        {
            return Kind == other.Kind && Index.Equals(other.Index);
        }

        public int CompareTo(DimSpec other)
        {
            if (other.Kind != Kind)
                return 0;

            switch (Kind)
            {
                case EKind.Index:
                    return ((long)this).CompareTo((long)other);

                case EKind.Range:
                    return ((Range)this).CompareTo((Range)other);

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Enumerates all indices which are covered by this dimensional specifier.
        /// In case this specifier represents a single index, an enumeration with that index
        /// as one and only element is returned. In case it represents a range, all integer values
        /// inside that range are returned.
        /// </summary>
        public IEnumerable<int> IndexValues
        {
            get
            {
                switch (Kind)
                {
                    case EKind.Index: return new int[] { (int)this };
                    case EKind.Range: return ((Range)this).Values;
                    default: throw new NotImplementedException();
                }
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case EKind.Index: return ((long)this).ToString();
                case EKind.Range: return ((Range)this).ToString();
                default: throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Two dimensional specifiers are defined to be equal iff the are of the same kind with the same
        /// index or range, respectively.
        /// </summary>
        public override bool Equals(object obj)
        {
            var other = obj as DimSpec;
            if (other == null)
                return false;

            return Equals(obj);
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode() ^ Index.GetHashCode();
        }

        /// <summary>
        /// The lowest index represented by this specifier, i.e. the index itself if this specifier is of index-kind.
        /// </summary>
        public int BaseIndex
        {
            get { return Index.Direction == EDimDirection.Downto ? Index.SecondBound : Index.FirstBound; }
        }

        public DimSpec Project(Range range)
        {
            switch (Kind)
            {
                case EKind.Index: return range.Unproject((int)this);
                case EKind.Range: return range.Unproject((Range)this);
                default: throw new NotImplementedException();
            }
        }

        public DimSpec Unproject(Range range)
        {
            switch (Kind)
            {
                case EKind.Index: return range.Project((int)this);
                case EKind.Range: return range.Project((Range)this);
                default: throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Represents <paramref name="index"/> as dimensional specifier.
        /// </summary>
        public static implicit operator DimSpec(int index)
        {
            return new DimSpec(index);
        }

        /// <summary>
        /// Represents <paramref name="range"/> as dimensional specifier.
        /// </summary>
        public static implicit operator DimSpec(Range range)
        {
            return new DimSpec(range);
        }

        /// <summary>
        /// Adds offset <paramref name="offs"/> to dimensional specifier <paramref name="a"/>.
        /// </summary>
        public static DimSpec operator+ (DimSpec a, int offs)
        {
            return new DimSpec(a.Kind, a.Index + offs);
        }

        /// <summary>
        /// Subtracts offset <paramref name="offs"/> to dimensional specifier <paramref name="a"/>.
        /// </summary>
        public static DimSpec operator -(DimSpec a, int offs)
        {
            return new DimSpec(a.Kind, a.Index - offs);
        }

        /// <summary>
        /// Converts an index-kind dimensional specifier to its represented index.
        /// </summary>
        /// <exception cref="ArgumentException">if <param name="ds"/> is not index-kind.</exception>
        public static explicit operator long(DimSpec ds)
        {
            if (ds.Kind != EKind.Index)
                throw new ArgumentException("Cannot convert dimension specifier to an index as it specifies a range");

            return ds.Index.FirstBound;
        }

        /// <summary>
        /// Converts a range-kind dimensional specifier to its represented index.
        /// </summary>
        /// <exception cref="ArgumentException">if <param name="ds"/> is not range-kind.</exception>
        public static explicit operator Range(DimSpec ds)
        {
            if (ds.Kind != EKind.Range)
                throw new ArgumentException("Cannot convert dimension specifier to a range as it specifies an index");

            return ds.Index;
        }

        public static bool operator <(DimSpec a, DimSpec b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            return a.CompareTo(b) < 0;
        }

        public static bool operator <=(DimSpec a, DimSpec b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            return a.CompareTo(b) < 0 || a.Equals(b);
        }

        public static bool operator ==(DimSpec a, DimSpec b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(DimSpec a, DimSpec b)
        {
            if (a == null && b == null)
                return false;
            if (a == null || b == null)
                return true;

            return !a.Equals(b);
        }

        public static bool operator >=(DimSpec a, DimSpec b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            return a.CompareTo(b) > 0 || a.Equals(b);
        }

        public static bool operator >(DimSpec a, DimSpec b)
        {
            Contract.Requires<ArgumentNullException>(a != null, "a");
            Contract.Requires<ArgumentNullException>(b != null, "b");
            return a.CompareTo(b) > 0;
        }
    }

    /// <summary>
    /// An index specifier is an aggregation of dimensional specifiers.
    /// </summary>
    public class IndexSpec: IComparable<IndexSpec>
    {
        private class IndexComparerImpl : IComparer<IndexSpec>
        {
            public int Compare(IndexSpec x, IndexSpec y)
            {
                if (x == null || y == null)
                    return 0;

                int count = Math.Min(x.Indices.Length, y.Indices.Length);
                for (int i = 0; i < count; i++)
                {
                    if (x.Indices[i] < y.Indices[i])
                        return -1;

                    if (x.Indices[i] > y.Indices[i])
                        return 1;
                }
                return 0;
            }
        }

        /// <summary>
        /// Compares index specifiers from first to last index.
        /// </summary>
        public static readonly IComparer<IndexSpec> IndexComparer = new IndexComparerImpl();

        /// <summary>
        /// The empty specifier.
        /// </summary>
        public static readonly IndexSpec Empty = new IndexSpec();

        /// <summary>
        /// The dimensional specifiers
        /// </summary>
        public DimSpec[] Indices { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="indices">sequence of dimensional specifiers</param>
        public IndexSpec(IEnumerable<DimSpec> indices)
        {
            Contract.Requires<ArgumentNullException>(indices != null, "indices");

            Indices = indices.ToArray();
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="indices">sequence of dimensional specifiers</param>
        public IndexSpec(params DimSpec[] indices)
        {
            Contract.Requires(indices != null);

            Indices = indices;
        }

        /// <summary>
        /// Number of dimensional specifiers
        /// </summary>
        public int MinSourceDimension
        {
            get { return Indices.Length; }
        }

        /// <summary>
        /// Minimum dimension of result when applying this index specifier.
        /// </summary>
        public int MinTargetDimension
        {
            get
            {
                int dim = 0;
                foreach (DimSpec index in Indices)
                {
                    if (index.Kind == DimSpec.EKind.Range)
                        ++dim;
                }
                return dim;
            }
        }

        public int DimensionReduction
        {
            get
            {
                int dim = 0;
                foreach (DimSpec index in Indices)
                {
                    if (index.Kind == DimSpec.EKind.Index)
                        ++dim;
                }
                return dim;
            }
        }

        /// <summary>
        /// Constructs an index specifier which represents applying this specifier to specifier <paramref name="first"/>.
        /// </summary>
        public IndexSpec Project(IndexSpec first)
        {
            Contract.Requires<ArgumentNullException>(first != null, "first");

            var indexList = new List<DimSpec>();
            int j = 0;
            for (int i = 0; i < first.Indices.Length; i++)
            {
                if (first.Indices[i].Kind == DimSpec.EKind.Index ||
                    j == Indices.Length)
                {
                    indexList.Add(first.Indices[i]);
                }
                else
                {
                    indexList.Add(Indices[j].Project((Range)first.Indices[i]));
                    ++j;
                }
            }
            for (; j < Indices.Length; j++)
            {
                indexList.Add(Indices[j]);
            }
            return new IndexSpec(indexList.ToArray());
        }

        /// <summary>
        /// Computes an <c>IndexSpec second</c> such that <c>second.Project(this).Equals(result)</c>.
        /// </summary>
        /// <returns>second</returns>
        public IndexSpec Unproject(IndexSpec result)
        {
            Contract.Requires<ArgumentNullException>(result != null, "result");

            var indexList = new List<DimSpec>();
            int j = 0;
            for (int i = 0; i < Indices.Length; i++)
            {
                if (Indices[i].Kind == DimSpec.EKind.Index ||
                    j == result.Indices.Length)
                {
                    indexList.Add(Indices[i]);
                }
                else
                {
                    indexList.Add(result.Indices[j].Unproject((Range)Indices[i]));
                    ++j;
                }
            }
            for (; j < result.Indices.Length; j++)
            {
                indexList.Add(result.Indices[j]);
            }
            return new IndexSpec(indexList.ToArray());
        }

        /// <summary>
        /// Applies an offset to each dimensional specifier, such that each dimensional specifier of the resulting
        /// index specifier is based at 0.
        /// </summary>
        public IndexSpec BaseAtZero()
        {
            var zindices = Indices.Select(i => i - i.BaseIndex);
            return new IndexSpec(zindices);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (Indices.Length > 0)
            {
                sb.Append("(");
                bool first = true;
                foreach (DimSpec dim in Indices)
                {
                    if (first)
                        first = false;
                    else
                        sb.Append(",");
                    sb.Append(dim.ToString());
                }
                sb.Append(")");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Two index specifiers are defined to be equal iff their underlying dimensional specifiers are equal.
        /// </summary>
        public override bool Equals(object obj)
        {
            var other = obj as IndexSpec;
            if (other == null)
                return false;

            return Indices.SequenceEqual(other.Indices);
        }

        public override int GetHashCode()
        {
            return Indices.GetSequenceHashCode();
        }

        public int CompareTo(IndexSpec other)
        {
            return IndexComparer.Compare(this, other);
        }
    }
}

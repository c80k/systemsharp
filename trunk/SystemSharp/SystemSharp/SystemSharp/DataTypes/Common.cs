﻿/**
 * Copyright 2011 Christian Köllner
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
    /// This struct represents a range.
    /// </summary>
    public struct Range //: IEnumerable<int>
    {
        public enum EComparisonResult
        {
            Unrelated,
            Left,
            LeftOverlap,
            LeftInclusion,
            Equality,
            RightInclusion,
            RightOverlap,
            Right
        }

        public EDimDirection Direction { get; private set; }
        public int FirstBound { get; private set; }
        public int SecondBound { get; private set; }

        public Range(int firstIndex, int secondIndex, EDimDirection direction) :
            this()
        {
            Direction = direction;
            FirstBound = firstIndex;
            SecondBound = secondIndex;
        }

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

        public long Project(int offset)
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

        public Range Unproject(Range range)
        {
            return new Range(
                Unproject(range).FirstBound,
                Unproject(range).SecondBound,
                range.Direction);
        }

        public Range Project(Range range)
        {
            return new Range(
                Project(range).FirstBound,
                Project(range).SecondBound,
                Direction);
        }

        public override bool Equals(object obj)
        {
            if (obj is Range)
            {
                Range range = (Range)obj;
                return FirstBound == range.FirstBound &&
                    SecondBound == range.SecondBound &&
                    Direction == range.Direction;
            }
            else
            {
                return false;
            }
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

        public static Range Downto(int hi, int lo)
        {
            return new Range(hi, lo, EDimDirection.Downto);
        }

        public static Range Upto(int lo, int hi)
        {
            return new Range(lo, hi, EDimDirection.To);
        }

        public static EComparisonResult Compare(Range ra, Range rb)
        {
            if (ra.Direction != rb.Direction)
                return EComparisonResult.Unrelated;

            if (ra.SecondBound < rb.FirstBound)
                return EComparisonResult.Left;

            if (rb.SecondBound < ra.FirstBound)
                return EComparisonResult.Right;

            if (ra.FirstBound < rb.FirstBound)
                return EComparisonResult.LeftOverlap;

            if (rb.FirstBound < ra.FirstBound)
                return EComparisonResult.RightOverlap;

            if (ra.FirstBound > rb.FirstBound)
                return EComparisonResult.LeftInclusion;

            if (rb.FirstBound > ra.FirstBound)
                return EComparisonResult.RightInclusion;

            return EComparisonResult.Equality;
        }

        public static Range Min(Range ra, Range rb)
        {
            switch (Compare(ra, rb))
            {
                case EComparisonResult.Unrelated:
                    throw new ArgumentException("Ranges must have equal directions");

                case EComparisonResult.LeftInclusion:
                case EComparisonResult.Equality:
                    return ra;

                case EComparisonResult.RightInclusion:
                    return rb;

                default:
                    throw new ArgumentException("Ranges overlap, but no range is included within the other one");
            }
        }

        public static Range Max(Range ra, Range rb)
        {
            switch (Compare(ra, rb))
            {
                case EComparisonResult.Unrelated:
                    throw new ArgumentException("Ranges must have equal directions");

                case EComparisonResult.LeftInclusion:
                case EComparisonResult.Equality:
                    return rb;

                case EComparisonResult.RightInclusion:
                    return ra;

                default:
                    throw new ArgumentException("Ranges overlap, but no range is included within the other one");
            }
        }

        public static Range operator+ (Range r, int offs)
        {
            return new Range(r.FirstBound + offs, r.SecondBound + offs, r.Direction);
        }

        public static Range operator- (Range r, int offs)
        {
            return new Range(r.FirstBound - offs, r.SecondBound - offs, r.Direction);
        }

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

    public class DimSpec
    {
        public enum EKind
        {
            Index,
            Range
        }

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

        public override string ToString()
        {
            switch (Kind)
            {
                case EKind.Index: return ((long)this).ToString();
                case EKind.Range: return ((Range)this).ToString();
                default: throw new NotImplementedException();
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as DimSpec;
            if (other == null)
                return false;

            return Kind == other.Kind && Index.Equals(other.Index);
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode() ^ Index.GetHashCode();
        }

        public int BaseIndex
        {
            get { return Index.Direction == EDimDirection.Downto ? Index.SecondBound : Index.FirstBound; }
        }

        public static implicit operator DimSpec(int index)
        {
            return new DimSpec(index);
        }

        public static implicit operator DimSpec(Range range)
        {
            return new DimSpec(range);
        }

        public static DimSpec operator+ (DimSpec a, int offs)
        {
            return new DimSpec(a.Kind, a.Index + offs);
        }

        public static DimSpec operator- (DimSpec a, int offs)
        {
            return new DimSpec(a.Kind, a.Index - offs);
        }

        public static explicit operator long(DimSpec ds)
        {
            if (ds.Kind != EKind.Index)
                throw new ArgumentException("Cannot convert dimension specifier to an index as it specifies a range");

            return ds.Index.FirstBound;
        }

        public static explicit operator Range(DimSpec ds)
        {
            if (ds.Kind != EKind.Range)
                throw new ArgumentException("Cannot convert dimension specifier to a range as it specifies an index");

            return ds.Index;
        }
    }

    public class IndexSpec
    {
        public static readonly IndexSpec Empty = new IndexSpec();

        public DimSpec[] Indices { get; private set; }

        public IndexSpec(IEnumerable<DimSpec> indices)
        {
            Contract.Requires(indices != null);

            Indices = indices.ToArray();
        }

        public IndexSpec(params DimSpec[] indices)
        {
            Contract.Requires(indices != null);

            Indices = indices;
        }

        public int SourceDimension
        {
            get { return Indices.Length; }
        }

        public int TargetDimension
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

        public IndexSpec ApplyTo(IndexSpec first)
        {
            if (first == null)
                return this;

            // Special case: no index operator, i.e. array of indices is empty.
            if (first.TargetDimension == 0)
                return this;

            if (first.TargetDimension < SourceDimension)
                throw new ArgumentException("Dimensionalities do not match");

            int sdim1 = first.SourceDimension;
            DimSpec[] indices = new DimSpec[sdim1];
            int index2 = 0;
            int start = first.TargetDimension - SourceDimension;
            for (int index1 = 0; index1 < sdim1; index1++)
            {
                indices[index1] = first.Indices[index1];
                if (index1 < start)
                    continue;

                if (indices[index1].Kind == DimSpec.EKind.Range)
                {
                    Range itsRange = (Range)indices[index1];
                    if (Indices[index2].Kind == DimSpec.EKind.Index)
                    {
                        int myidx = (int)Indices[index2];
                        int totalIdx;
                        switch (itsRange.Direction)
                        {
                            case EDimDirection.Downto:
                                totalIdx = itsRange.SecondBound + myidx;
                                if (totalIdx < itsRange.SecondBound ||
                                    totalIdx > itsRange.FirstBound)
                                    throw new ArgumentException("Index exceeds range bounds");
                                break;

                            case EDimDirection.To:
                                totalIdx = itsRange.FirstBound + myidx;
                                if (totalIdx < itsRange.FirstBound ||
                                    totalIdx > itsRange.SecondBound)
                                    throw new ArgumentException("Index exceeds range bounds");
                                break;

                            default: throw new NotImplementedException();
                        }
                        indices[index1] = totalIdx;
                    }
                    else
                    {
                        var myRange = (Range)indices[index2];
                        if (itsRange.Direction != myRange.Direction)
                            throw new ArgumentException("Projection ranges must have same direction");

                        Range totalRange;
                        switch (itsRange.Direction)
                        {
                            case EDimDirection.Downto:
                                totalRange = new Range(
                                    myRange.FirstBound + itsRange.SecondBound,
                                    myRange.SecondBound + itsRange.SecondBound,
                                    EDimDirection.Downto);
                                if (totalRange.FirstBound > itsRange.FirstBound ||
                                    totalRange.SecondBound < itsRange.SecondBound)
                                    throw new ArgumentException("Index exceeds range bounds");
                                break;

                            case EDimDirection.To:
                                totalRange = new Range(
                                    myRange.FirstBound + itsRange.FirstBound,
                                    myRange.SecondBound + itsRange.FirstBound,
                                    EDimDirection.To);
                                if (totalRange.FirstBound < itsRange.FirstBound ||
                                    totalRange.SecondBound > itsRange.SecondBound)
                                    throw new ArgumentException("Index exceeds range bounds");
                                break;

                            default: throw new NotImplementedException();
                        }
                    }
                    indices[index1] = Indices[index2++];
                }
            }
            return new IndexSpec(indices);
        }

        /// <summary>
        /// Finds an IndexSpec second such that second.ApplyTo(this).Equals(result)
        /// </summary>
        /// <returns>second</returns>
        public IndexSpec Unproject(IndexSpec result)
        {
            if (SourceDimension == 0)
                return result;

            if (this.Equals(result))
                return new IndexSpec(); // empty index

            if (TargetDimension != result.SourceDimension)
                throw new ArgumentException("dimensions do not match");

            int rindex = 0;
            var rindices = new DimSpec[TargetDimension];
            for (int myindex = 0; myindex < SourceDimension; myindex++)
            {
                if (Indices[myindex].Kind == DimSpec.EKind.Index)
                    continue;

                var myrange = (Range)Indices[myindex];
                var rdim = result.Indices[rindex];
                switch (rdim.Kind)
                {
                    case DimSpec.EKind.Index:
                        {
                            int rdimidx = (int)rdim;
                            switch (myrange.Direction)
                            {
                                case EDimDirection.Downto:
                                    if (rdimidx < myrange.SecondBound ||
                                        rdimidx > myrange.FirstBound)
                                        throw new ArgumentException("Index exceeds range bounds");
                                    rindices[rindex] = rdimidx - myrange.SecondBound;
                                    break;

                                case EDimDirection.To:
                                    if (rdimidx > myrange.SecondBound ||
                                        rdimidx < myrange.FirstBound)
                                        throw new ArgumentException("Index exceeds range bounds");
                                    rindices[rindex] = rdimidx - myrange.FirstBound;
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }
                        }
                        break;

                    case DimSpec.EKind.Range:
                        {
                            var rrange = (Range)rdim;
                            if (rrange.Direction != myrange.Direction)
                                throw new ArgumentException("Range directions do not match");

                            switch (myrange.Direction)
                            {
                                case EDimDirection.Downto:
                                    if (rrange.FirstBound < myrange.SecondBound ||
                                        rrange.FirstBound > myrange.FirstBound ||
                                        rrange.SecondBound < myrange.SecondBound ||
                                        rrange.SecondBound > myrange.FirstBound)
                                        throw new ArgumentException("Index exceeds range bounds");
                                    rindices[rindex] = new Range(
                                        rrange.FirstBound - myrange.SecondBound,
                                        rrange.SecondBound - myrange.SecondBound,
                                        EDimDirection.Downto);
                                    break;

                                case EDimDirection.To:
                                    if (rrange.FirstBound > myrange.SecondBound ||
                                        rrange.FirstBound < myrange.FirstBound ||
                                        rrange.SecondBound > myrange.SecondBound ||
                                        rrange.SecondBound < myrange.FirstBound)
                                        throw new ArgumentException("Index exceeds range bounds");
                                    rindices[rindex] = new Range(
                                        rrange.FirstBound - myrange.FirstBound,
                                        rrange.SecondBound - myrange.FirstBound,
                                        EDimDirection.To);
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }
                        }
                        break;
                }
                rindex++;
            }

            return new IndexSpec(rindices);
        }

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
    }
}
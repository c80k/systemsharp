/**
 * Copyright 2012 Christian Köllner
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

namespace SystemSharp.Collections
{
    public class IntervalSet
    {
        private class Interval : IComparable<Interval>
        {
            public int Left;
            public int Right;

            public Interval(int left, int right)
            {
                Left = left;
                Right = right;
            }

            public override bool Equals(object obj)
            {
                var other = obj as Interval;
                return other.Left == Left &&
                    other.Right == Right;
            }

            public override int GetHashCode()
            {
                return Left.GetHashCode() ^ (3 * Right.GetHashCode());
            }

            public override string  ToString()
            {
 	             return "[" + Left.ToString() + "-" + Right.ToString() + "]";
            }

            public int CompareTo(Interval other)
            {
                return Left < other.Left ? -1 : ((Left > other.Left) ? 1 : 0);
            }

            public bool Test(int point)
            {
                return point >= Left && point <= Right;
            }

            public bool Intersects(int left, int right)
            {
                return (left >= Left && left <= Right) ||
                    (Left >= left && Left <= right);
            }

            public bool Intersects(Interval other)
            {
                return Intersects(other.Left, other.Right);
            }
        }

        private SortedSet<Interval> _intervals = new SortedSet<Interval>();

        public IEnumerable<int> LeftPoints
        {
            get { return _intervals.Select(iv => iv.Left); }
        }

        public IEnumerable<int> RightPoints
        {
            get { return _intervals.Select(iv => iv.Right); }
        }

        public bool Add(int left, int right)
        {
            
            Contract.Requires(Contains(left, right) || !Intersects(left, right));
            if (left > right)
            {
                // Interval is empty
                return true;
            }
            if (Contains(left, right))
            {
                return false;
            }
            else
            {
                _intervals.Add(new Interval(left, right));
                return true;
            }
        }

        public void Add(IntervalSet other)
        {
            if (this == other)
                return;

            foreach (var interval in other._intervals)
            {
                Add(interval.Left, interval.Right);
            }
        }

        [Pure]
        public bool Test(int point)
        {
            var min = new Interval(int.MinValue, int.MinValue);
            var max = new Interval(point, point);
            var view = _intervals.GetViewBetween(min, max);
            if (!view.Any())
                return false;
            var test = view.Max;
            return test.Test(point);
        }

        [Pure]
        public bool Intersects(int left, int right)
        {
            var min = new Interval(int.MinValue, int.MinValue);
            var max = new Interval(right, right);
            var view = _intervals.GetViewBetween(min, max);
            if (!view.Any())
                return false;
            var test = view.Max;
            return test.Intersects(left, right);
        }

        [Pure]
        public bool Contains(int left, int right)
        {
            var min = new Interval(int.MinValue, int.MinValue);
            var max = new Interval(right, right);
            var view = _intervals.GetViewBetween(min, max);
            if (!view.Any())
                return false;
            var test = view.Max;
            return test.Equals(new Interval(left, right));
        }

        [Pure]
        public bool Intersects(IntervalSet other)
        {
            var enum1 = _intervals.GetEnumerator();
            var enum2 = other._intervals.GetEnumerator();
            if (!enum1.MoveNext() || !enum2.MoveNext())
                return false;
            do
            {
                var i1 = enum1.Current;
                var i2 = enum2.Current;
                if (i1.Intersects(i2))
                    return true;
                if (i1.Right < i2.Right)
                {
                    if (!enum1.MoveNext())
                        return false;
                }
                else
                {
                    if (!enum2.MoveNext())
                        return false;
                }
            } while (true);
        }

        public override string ToString()
        {
            return string.Join("; ", _intervals);
        }
    }
}

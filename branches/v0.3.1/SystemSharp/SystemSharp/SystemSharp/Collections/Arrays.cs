/**
 * Copyright 2011-2013 Christian Köllner
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
using SystemSharp.DataTypes;

namespace SystemSharp.Collections
{
    /// <summary>
    /// This static class provides some convenience methods to operate on arrays
    /// </summary>
    public static class Arrays
    {
        private class ArrayEqualityComparer<T> : IEqualityComparer<T[]>
        {
            public bool Equals(T[] x, T[] y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(T[] obj)
            {
                return obj.GetSequenceHashCode();
            }
        }

        /// <summary>
        /// Creates an array and fills it up with the same element.
        /// </summary>
        /// <typeparam name="T">Element type</typeparam>
        /// <param name="element">The element to fill</param>
        /// <param name="count">The desired length of the array</param>
        /// <returns>An array of length "count" with all elements set to "element"</returns>
        public static T[] Same<T>(T element, long count)
        {
            T[] result = new T[count];
            for (long i = 0; i < count; i++)
                result[i] = element;
            return result;
        }

        /// <summary>
        /// Creates an equality comparer for arrays, whereby two arrays are defined to be equal iff they are element-wise equal.
        /// </summary>
        /// <typeparam name="T">array element type</typeparam>
        public static IEqualityComparer<T[]> CreateArrayEqualityComparer<T>()
        {
            return new ArrayEqualityComparer<T>();
        }

        /// <summary>
        /// Returns a sub-array from a given array.
        /// </summary>
        /// <typeparam name="T">element type of array</typeparam>
        /// <param name="arr">an array</param>
        /// <param name="range">range to slice out (only up-ranges supported)</param>
        /// <returns>the sliced sub-array</returns>
        public static T[] Slice<T>(this T[] arr, Range range)
        {
            Contract.Requires<NotSupportedException>(range.Direction != EDimDirection.To, "Only up-ranges supported");

            long size = range.Size;
            T[] result = new T[size];
            for (long i = 0; i < size; i++)
                result[i] = arr[i + range.FirstBound];
            return result;
        }
    }
}

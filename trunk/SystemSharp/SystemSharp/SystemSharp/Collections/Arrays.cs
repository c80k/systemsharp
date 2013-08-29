/**
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

namespace SystemSharp.Collections
{
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

        public static IEqualityComparer<T[]> CreateArrayEqualityComparer<T>()
        {
            return new ArrayEqualityComparer<T>();
        }
    }
}

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
using SystemSharp.DataTypes;

namespace SystemSharp.Collections
{
    public static class Lists
    {
        public static IList<T> Concat<T>(this IList<T> list, params T[] items)
        {
            List<T> result = new List<T>(list);
            result.InsertRange(result.Count, items);
            return result;
        }

        public static T[] Slice<T>(this T[] arr, Range range)
        {
            if (range.Direction != EDimDirection.To)
                throw new NotImplementedException("Only up-ranges supported");

            long size = range.Size;
            T[] result = new T[size];
            for (long i = 0; i < size; i++)
                result[i] = arr[i + range.FirstBound];
            return result;
        }
    }
}

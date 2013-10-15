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
using System.Linq;
using System.Text;

namespace SystemSharp.Collections
{
    public static class Sets
    {
        /// <summary>
        /// Adds a sequence of elements to the collection
        /// </summary>
        /// <typeparam name="T">type of element</typeparam>
        /// <param name="me">a collection</param>
        /// <param name="items">sequence of elements to be added</param>
        public static void AddRange<T>(this ICollection<T> me, IEnumerable<T> items)
        {
            foreach (T item in items)
                me.Add(item);
        }
    }
}

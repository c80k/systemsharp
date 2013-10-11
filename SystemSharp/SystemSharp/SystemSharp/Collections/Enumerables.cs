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
 * 
 * CHANGE LOG
 * ==========
 * 2011-08-15 CK fixed hash code computation
 * 2011-12-04 CK fixed computation to tolerate zero elements
 * 2012-02-19 CK added GetSetHashCode
 * 2012-09-20 CK added int GetSequenceHashCode<T>(this IEnumerable<T> seq, Func<T, int> elemHashCode)
 * 2013-10-11 CK fixed GetSetHashCode<T>(...) to tolerate duplicated elements
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace SystemSharp.Collections
{
    /// <summary>
    /// This static class provides extension methods to operate on IEnumerable&lt;T&gt;
    /// </summary>
    public static class Enumerables
    {
        /// <summary>
        /// Computes a hash code on the sequence, such that from x.SequenceEqual(y) it follows that 
        /// x.GetSequenceHashCode() == y.GetSequenceHashCode().
        /// </summary>
        /// <typeparam name="T">element type inside sequence</typeparam>
        /// <param name="seq">a sequence</param>
        /// <returns>computed hash code</returns>
        public static int GetSequenceHashCode<T>(this IEnumerable<T> seq)
        {
            Contract.Requires(seq != null);
            return seq.Aggregate(0, (h, e) => (int)(((uint)h << 1) | ((uint)h >> 31)) ^ (e == null ? 0 : e.GetHashCode()));
        }

        /// <summary>
        /// Computes a hash code on the sequence, using a caller-supplied hash function.
        /// </summary>
        /// <typeparam name="T">element type inside sequence</typeparam>
        /// <param name="seq">a sequence</param>
        /// <param name="elemHashCode">hash function</param>
        /// <returns>computed hash code</returns>
        public static int GetSequenceHashCode<T>(this IEnumerable<T> seq, Func<T, int> elemHashCode)
        {
            Contract.Requires(seq != null);
            return seq.Aggregate(0, (h, e) => (int)(((uint)h << 1) | ((uint)h >> 31)) ^ (elemHashCode(e)));
        }

        /// <summary>
        /// Computes a hash code on the sequence, such that its hash code equals any other sequence containing the same
        /// set of elements (regardless of their order and possible duplications).
        /// </summary>
        /// <typeparam name="T">element type inside sequence</typeparam>
        /// <param name="seq">a sequence</param>
        /// <returns>computed hash code</returns>
        public static int GetSetHashCode<T>(this IEnumerable<T> seq)
        {
            Contract.Requires(seq != null);
            return seq.Distinct().Aggregate(0, (h, e) => h ^ (e == null ? 0 : e.GetHashCode()));
        }
    }
}

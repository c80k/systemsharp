/**
 * Copyright 2011-2012 Christian Köllner
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
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace SystemSharp.Collections
{
    public static class Enumerables
    {
        public static int GetSequenceHashCode<T>(this IEnumerable<T> seq)
        {
            Contract.Requires(seq != null);
            return seq.Aggregate(0, (h, e) => (int)(((uint)h << 1) | ((uint)h >> 31)) ^ (e == null ? 0 : e.GetHashCode()));
        }

        public static int GetSequenceHashCode<T>(this IEnumerable<T> seq, Func<T, int> elemHashCode)
        {
            Contract.Requires(seq != null);
            return seq.Aggregate(0, (h, e) => (int)(((uint)h << 1) | ((uint)h >> 31)) ^ (elemHashCode(e)));
        }

        public static int GetSetHashCode<T>(this IEnumerable<T> seq)
        {
            Contract.Requires(seq != null);
            return seq.Aggregate(0, (h, e) => h ^ (e == null ? 0 : e.GetHashCode()));
        }
    }
}

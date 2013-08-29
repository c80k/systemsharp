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

namespace SystemSharp.Common
{
    public static class StringHelpers
    {
#if false
        [Obsolete("Use string.Join() instead")]
        public static string ToStringList<T>(this IEnumerable<T> objs, string sep = ", ")
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (T obj in objs)
            {
                if (first)
                    first = false;
                else
                    sb.Append(sep);
                sb.Append(obj);
            }
            return sb.ToString();
        }
#endif

        public static string Zeros(long n)
        {
            return ManyOf("0", n);
        }

        public static string Spaces(long n)
        {
            return ManyOf(" ", n);
        }

        public static string ManyOf(string lit, long n)
        {
            if (n == 0)
                return "";
            else if (n == 1)
                return lit;
            else if (n < 0)
                throw new ArgumentException();
            else
                return ManyOf(lit, n / 2) + ManyOf(lit, n - (n / 2));
        }
    }
}

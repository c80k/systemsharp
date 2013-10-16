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

namespace SystemSharp.Common
{
    public static class StringHelpers
    {
        /// <summary>
        /// Returns a string of <paramref name="n"/> zeros.
        /// </summary>
        public static string Zeros(long n)
        {
            Contract.Requires<ArgumentOutOfRangeException>(n >= 0);
            return ManyOf("0", n);
        }

        /// <summary>
        /// Returns a string of <paramref name="n"/> space characters.
        /// </summary>
        public static string Spaces(long n)
        {
            Contract.Requires<ArgumentOutOfRangeException>(n >= 0);
            return ManyOf(" ", n);
        }

        /// <summary>
        /// Returns a string which consists of literal <paramref name="lit"/> being <paramref name="n"/> times repeated.
        /// </summary>
        public static string ManyOf(string lit, long n)
        {
            Contract.Requires<ArgumentNullException>(lit != null, "lit");
            Contract.Requires<ArgumentOutOfRangeException>(n >= 0, "n");

            if (n == 0)
                return "";
            else if (n == 1)
                return lit;
            else
                return ManyOf(lit, n / 2) + ManyOf(lit, n - (n / 2));
        }
    }
}

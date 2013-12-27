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
using System.Reflection;
using System.Text;

namespace SystemSharp.Interop.Xilinx
{
    /// <summary>
    /// Describes Xilinx device packages.
    /// </summary>
    /// <remarks>
    /// This enum is far from being complete. It just enumerates an arbitrary choice of packages.
    /// An exhaustive enumeration is yet to be defined.
    /// </remarks>
    public enum EPackage
    {
        Undefined,
        cp132,
        cpg196,
        cs484,
        csg225,
        csg484,
        ff324,
        ff484,
        ff665,
        ff668,
        ff676,
        ff784,
        ff1136,
        ff1153,
        ff1156,
        ff1738,
        ff1759,
        fg320,
        fg456,
        fg676,
        fgg676,
        ft256,
        ftg256,
        pq208,
        pqg208,
        sfb363,
        tq144,
        tqg144,
        vq100,
        vqg100
    }

    public static class Packages
    {
        /// <summary>
        /// Enumerates all Xilinx device packages.
        /// </summary>
        public static IEnumerable<EPackage> GetPackages()
        {
            return typeof(EPackage)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(fi => (EPackage)fi.GetValue(null));
        }
    }
}

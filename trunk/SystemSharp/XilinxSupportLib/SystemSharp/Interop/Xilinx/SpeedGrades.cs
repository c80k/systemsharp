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
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SystemSharp.Interop.Xilinx
{
    public enum ESpeedGrade
    {
        [PropID(EPropAssoc.ISE, "undefined")]
        [PropID(EPropAssoc.CoreGen, "undefined")]
        [PropID(EPropAssoc.CoreGenProj, "undefined")]
        Undefined,

        [PropID(EPropAssoc.ISE, "-1")]
        [PropID(EPropAssoc.CoreGen, "-1")]
        [PropID(EPropAssoc.CoreGenProj, "-1")]
        _1,

        [PropID(EPropAssoc.ISE, "-2")]
        [PropID(EPropAssoc.CoreGen, "-2")]
        [PropID(EPropAssoc.CoreGenProj, "-2")]
        _2,

        [PropID(EPropAssoc.ISE, "-3")]
        [PropID(EPropAssoc.CoreGen, "-3")]
        [PropID(EPropAssoc.CoreGenProj, "-3")]
        _3,

        [PropID(EPropAssoc.ISE, "-4")]
        [PropID(EPropAssoc.CoreGen, "-4")]
        [PropID(EPropAssoc.CoreGenProj, "-4")]
        _4,

        [PropID(EPropAssoc.ISE, "-5")]
        [PropID(EPropAssoc.CoreGen, "-5")]
        [PropID(EPropAssoc.CoreGenProj, "-5")]
        _5
    }

    public static class SpeedGrades
    {
        public static IEnumerable<ESpeedGrade> GetSpeedGrades()
        {
            return typeof(EDevice)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(fi => (ESpeedGrade)fi.GetValue(null));
        }
    }
}

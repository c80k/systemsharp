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

namespace SystemSharp.Interop.Xilinx
{
    public enum EDeviceFamily
    {
        [PropID(EPropAssoc.ISE, "Automotive 9500XL")]
        [PropID(EPropAssoc.CoreGen, "automotive 9500xl")] // ?
        [PropID(EPropAssoc.CoreGenProj, "automotive 9500xl")] // ?
        Automotive_9500XL,

        [PropID(EPropAssoc.ISE, "Automotive CoolRunner2")]
        [PropID(EPropAssoc.CoreGen, "automotive coolrunner2")] // ?
        [PropID(EPropAssoc.CoreGenProj, "automotive coolrunner2")] // ?
        Automotive_CoolRunner2,

        [PropID(EPropAssoc.ISE, "Automotive Spartan-3A DSP")]
        [PropID(EPropAssoc.CoreGen, "automotive spartan-3a dsp")] // ?
        [PropID(EPropAssoc.CoreGenProj, "automotive spartan-3a dsp")] // ?
        Automotive_Spartan3A_DSP,

        [PropID(EPropAssoc.ISE, "Automotive Spartan3")]
        [PropID(EPropAssoc.CoreGen, "automotive spartan3")] // ?
        [PropID(EPropAssoc.CoreGenProj, "automotive spartan3")] // ?
        Automotive_Spartan3,

        [PropID(EPropAssoc.ISE, "Automotive Spartan3A")]
        [PropID(EPropAssoc.CoreGen, "automotive spartan3a")] // ?
        [PropID(EPropAssoc.CoreGenProj, "automotive spartan3a")] // ?
        Automotive_Spartan3A,

        [PropID(EPropAssoc.ISE, "Automotive Spartan3E")]
        [PropID(EPropAssoc.CoreGen, "automotive spartan3e")] // ?
        [PropID(EPropAssoc.CoreGenProj, "automotive spartan3e")] // ?
        Automotive_Spartan3E,

        [PropID(EPropAssoc.ISE, "Automotive Spartan6")]
        [PropID(EPropAssoc.CoreGen, "automotive spartan6")] // ?
        [PropID(EPropAssoc.CoreGenProj, "automotive spartan6")] // ?
        Automotive_Spartan6,

        [PropID(EPropAssoc.ISE, "CoolRunner XPLA3 CPLDs")]
        [PropID(EPropAssoc.CoreGen, "coolrunner xpla3 cplds")] // ?
        [PropID(EPropAssoc.CoreGenProj, "coolrunner xpla3 cplds")] // ?
        CoolRunner_XPLA3,

        [PropID(EPropAssoc.ISE, "CoolRunner2 CPLDs")]
        [PropID(EPropAssoc.CoreGen, "coolrunner2 cplds")] // ?
        [PropID(EPropAssoc.CoreGenProj, "coolrunner2 cplds")] // ?
        CoolRunner2,

        [PropID(EPropAssoc.ISE, "Spartan3")]
        [PropID(EPropAssoc.CoreGen, "spartan3")]
        [PropID(EPropAssoc.CoreGenProj, "spartan3")]
        Spartan3,

        [PropID(EPropAssoc.ISE, "Spartan-3A DSP")]
        [PropID(EPropAssoc.CoreGen, "spartan-3a dsp")] // ?
        [PropID(EPropAssoc.CoreGenProj, "spartan-3a dsp")] // ?
        Spartan3A_DSP,

        [PropID(EPropAssoc.ISE, "Spartan3A and Spartan3AN")]
        [PropID(EPropAssoc.CoreGen, "spartan3a and spartan3an")] // ?
        [PropID(EPropAssoc.CoreGenProj, "spartan3a and Spartan3an")] // ?
        Spartan3A_3AN,

        [PropID(EPropAssoc.ISE, "Spartan3E")]
        [PropID(EPropAssoc.CoreGen, "spartan3e")] // ?
        [PropID(EPropAssoc.CoreGenProj, "spartan3e")] // ?
        Spartan3E,

        [PropID(EPropAssoc.ISE, "Spartan6")]
        [PropID(EPropAssoc.CoreGen, "spartan6")] // ?
        [PropID(EPropAssoc.CoreGenProj, "spartan6")] // ?
        Spartan6,

        [PropID(EPropAssoc.ISE, "Spartan6 Lower Power")]
        [PropID(EPropAssoc.CoreGen, "spartan6 low power")] // ?
        [PropID(EPropAssoc.CoreGenProj, "spartan6 low power")] // ?
        Spartan6_LowPower,

        [PropID(EPropAssoc.ISE, "Virtex4")]
        [PropID(EPropAssoc.CoreGen, "virtex4")] // ?
        [PropID(EPropAssoc.CoreGenProj, "virtex4")] // ?
        Virtex4,

        [PropID(EPropAssoc.ISE, "Virtex5")]
        [PropID(EPropAssoc.CoreGen, "virtex5")] // ?
        [PropID(EPropAssoc.CoreGenProj, "virtex5")] // ?
        Virtex5,

        [PropID(EPropAssoc.ISE, "Virtex6")]
        [PropID(EPropAssoc.CoreGen, "virtex6")] // ?
        [PropID(EPropAssoc.CoreGenProj, "virtex6")] // ?
        Virtex6,

        [PropID(EPropAssoc.ISE, "Virtex6 Lower Power")]
        [PropID(EPropAssoc.CoreGen, "virtex6 lower power")] // ?
        [PropID(EPropAssoc.CoreGenProj, "virtex6 lower power")] // ?
        Virtex6_LowPower,

        [PropID(EPropAssoc.ISE, "XC9500 CPLDs")]
        [PropID(EPropAssoc.CoreGen, "xc9500 cplds")] // ?
        [PropID(EPropAssoc.CoreGenProj, "xc9500 cplds")] // ?
        XC9500,

        [PropID(EPropAssoc.ISE, "XC9500XL CPLDs")]
        [PropID(EPropAssoc.CoreGen, "xc9500XL cplds")] // ?
        [PropID(EPropAssoc.CoreGenProj, "xc9500XL cplds")] // ?
        XC9500XL
    }
}

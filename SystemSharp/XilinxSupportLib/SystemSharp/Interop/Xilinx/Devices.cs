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
    public enum EDevice
    {
        Undefined,

        [DeclareFamily(EDeviceFamily.Automotive_9500XL)]
        [PropID(EPropAssoc.ISE, "xa95*xl")]
        [PropID(EPropAssoc.CoreGen, "xa95*xl")]
        [PropID(EPropAssoc.CoreGenProj, "xa95*xl")]
        xa95_xl,

        [DeclareFamily(EDeviceFamily.Automotive_CoolRunner2)]
        [PropID(EPropAssoc.ISE, "xa2c*")]
        [PropID(EPropAssoc.CoreGen, "xa2c*")]
        [PropID(EPropAssoc.CoreGenProj, "xa2c*")]
        xa2c_,

        [DeclareFamily(EDeviceFamily.Automotive_Spartan3A_DSP)]
        [DeclarePackage(EPackage.csg484)]
        [DeclarePackage(EPackage.fgg676)]
        xa3sd1800a,

        [DeclareFamily(EDeviceFamily.Automotive_Spartan3)]
        [DeclarePackage(EPackage.vqg100)]
        [DeclarePackage(EPackage.pqg208)]
        xa3s50,

        [DeclareFamily(EDeviceFamily.Automotive_Spartan3A)]
        [DeclarePackage(EPackage.ftg256)]
        xa3s200a,

        [DeclareFamily(EDeviceFamily.Automotive_Spartan3E)]
        [DeclarePackage(EPackage.vqg100)]
        xa3s100e,

        [DeclareFamily(EDeviceFamily.Automotive_Spartan6)]
        [DeclarePackage(EPackage.csg225)]
        xa6slx4,

        [PropID(EPropAssoc.ISE, "xcr3*xl")]
        [PropID(EPropAssoc.CoreGen, "xcr3*xl")]
        [PropID(EPropAssoc.CoreGenProj, "xcr3*xl")]
        [DeclareFamily(EDeviceFamily.CoolRunner_XPLA3)]
        xcr3_xl,

        [PropID(EPropAssoc.ISE, "xc2c*")]
        [PropID(EPropAssoc.CoreGen, "xc2c*")]
        [PropID(EPropAssoc.CoreGenProj, "xc2c*")]
        [DeclareFamily(EDeviceFamily.CoolRunner2)]
        xc2c_,

        [DeclareFamily(EDeviceFamily.Spartan3)]
        [DeclarePackage(EPackage.pq208)]
        [DeclarePackage(EPackage.tq144)]
        [DeclarePackage(EPackage.vq100)]
        [DeclarePackage(EPackage.cp132)]
        xc3s50,

        [DeclareFamily(EDeviceFamily.Spartan3)]
        [DeclarePackage(EPackage.fg320)]
        [DeclarePackage(EPackage.fg456)]
        [DeclarePackage(EPackage.ft256)]
        [DeclarePackage(EPackage.pq208)]
        [DeclarePackage(EPackage.tq144)]
        xc3s400,

        [DeclareFamily(EDeviceFamily.Spartan3)]
        xc3s1500l,

        [DeclareFamily(EDeviceFamily.Spartan3A_DSP)]
        [DeclarePackage(EPackage.cs484)]
        [DeclarePackage(EPackage.fg676)]
        xc3sd1800a,

        [DeclareFamily(EDeviceFamily.Spartan3A_3AN)]
        [DeclarePackage(EPackage.tq144)]
        [DeclarePackage(EPackage.ft256)]
        [DeclarePackage(EPackage.vq100)]
        xc3s50a,

        [DeclareFamily(EDeviceFamily.Spartan3E)]
        [DeclarePackage(EPackage.vq100)]
        [DeclarePackage(EPackage.cp132)]
        [DeclarePackage(EPackage.tq144)]
        xc3s100e,

        [DeclareFamily(EDeviceFamily.Spartan6)]
        [DeclarePackage(EPackage.tqg144)]
        [DeclarePackage(EPackage.cpg196)]
        [DeclarePackage(EPackage.csg225)]
        xc6slx4,

        [DeclareFamily(EDeviceFamily.Spartan6_LowPower)]
        [DeclarePackage(EPackage.tqg144)]
        [DeclarePackage(EPackage.cpg196)]
        [DeclarePackage(EPackage.csg225)]
        xc6slx4l,

        [DeclareFamily(EDeviceFamily.Virtex4)]
        [DeclarePackage(EPackage.sfb363)]
        [DeclarePackage(EPackage.ff668)]
        [DeclarePackage(EPackage.ff676)]
        xc4vlx15,

        [DeclareFamily(EDeviceFamily.Virtex5)]
        [DeclarePackage(EPackage.ff665)]
        xc5vfx30t,

        [DeclareFamily(EDeviceFamily.Virtex5)]
        [DeclarePackage(EPackage.ff1136)]
        xc5vfx70t,

        [DeclareFamily(EDeviceFamily.Virtex5)]
        [DeclarePackage(EPackage.ff324)]
        [DeclarePackage(EPackage.ff676)]
        xc5vlx30,

        [DeclareFamily(EDeviceFamily.Virtex5)]
        [DeclarePackage(EPackage.ff324)]
        [DeclarePackage(EPackage.ff676)]
        [DeclarePackage(EPackage.ff1153)]
        xc5vlx50,

        [DeclareFamily(EDeviceFamily.Virtex5)]
        [DeclarePackage(EPackage.ff1136)]
        [DeclarePackage(EPackage.ff1738)]
        xc5vlx110t,

        [DeclareFamily(EDeviceFamily.Virtex5)]
        [DeclarePackage(EPackage.ff1738)]
        xc5vlx330t,

        [DeclareFamily(EDeviceFamily.Virtex6)]
        [DeclarePackage(EPackage.ff784)]
        [DeclarePackage(EPackage.ff1156)]
        [DeclarePackage(EPackage.ff1759)]
        xc6vcx240t,

        [DeclareFamily(EDeviceFamily.Virtex6)]
        [DeclarePackage(EPackage.ff784)]
        [DeclarePackage(EPackage.ff1156)]
        xc6vlx240t,

        [DeclareFamily(EDeviceFamily.Virtex6)]
        [DeclarePackage(EPackage.ff484)]
        [DeclarePackage(EPackage.ff784)]
        xc6vlx75t,

        [DeclareFamily(EDeviceFamily.Virtex6_LowPower)]
        [DeclarePackage(EPackage.ff484)]
        [DeclarePackage(EPackage.ff784)]
        xc6vlx75tl,

        [DeclareFamily(EDeviceFamily.XC9500)]
        [PropID(EPropAssoc.ISE, "xc95*")]
        [PropID(EPropAssoc.CoreGen, "xc95*")] // ?
        [PropID(EPropAssoc.CoreGenProj, "xc95*")] // ?
        xc95_,

        [DeclareFamily(EDeviceFamily.XC9500XL)]
        [PropID(EPropAssoc.ISE, "xc95*xl")]
        [PropID(EPropAssoc.CoreGen, "xc95*xl")] // ?
        [PropID(EPropAssoc.CoreGenProj, "xc95*xl")] // ?
        xc95_xl
    }

    public static class DeviceExtensions
    {
        public static EDeviceFamily GetFamily(this EDevice device)
        {
            FieldInfo field = typeof(EDevice)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(fi => (EDevice)fi.GetValue(null) == device)
                .Single();
            DeclareFamily famAttr = (DeclareFamily)field
                .GetCustomAttributes(typeof(DeclareFamily), false)
                .Single();
            return famAttr.Family;
        }

        private static bool IsFamily(DeclareFamily declFamily, EDeviceFamily family)
        {
            return declFamily != null && declFamily.Family == family;
        }

        public static IEnumerable<EDevice> GetDevices(this EDeviceFamily family)
        {
            return typeof(EDevice)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(fi => IsFamily((DeclareFamily)fi.GetCustomAttributes(typeof(DeclareFamily), false).SingleOrDefault(), family))
                .Select(fi => (EDevice)fi.GetValue(null));
        }

        public static IEnumerable<EPackage> GetPackages(this EDevice device)
        {
            FieldInfo field = typeof(EDevice)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(fi => (EDevice)fi.GetValue(null) == device)
                .Single();
            return field.GetCustomAttributes(typeof(DeclarePackage), false)
                .Cast<DeclarePackage>()
                .Select(dp => dp.Package);
        }
    }
}

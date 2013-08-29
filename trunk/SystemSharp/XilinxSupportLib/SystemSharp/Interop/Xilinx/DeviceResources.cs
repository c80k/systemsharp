/**
 * Copyright 2012 Christian Köllner
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
    public enum EDeviceResource
    {
        [PropID(EPropAssoc.PARReport, "Slice Registers")]
        SliceRegisters,

        [PropID(EPropAssoc.PARReport, "Slice LUTs")]
        SliceLUTs,

        [PropID(EPropAssoc.PARReport, "occupied Slices")]
        OccupiedSlices,

        [PropID(EPropAssoc.PARReport, "RAMB36E1/FIFO36E1s")]
        RAMB36s,

        [PropID(EPropAssoc.PARReport, "RAMB18E1/FIFO18E1s")]
        RAMB18s,

        [PropID(EPropAssoc.PARReport, "BUFG/BUFGCTRLs")]
        BUFGs,

        [PropID(EPropAssoc.PARReport, "ILOGICE1/ISERDESE1s")]
        ILOGICE1s,

        [PropID(EPropAssoc.PARReport, "OLOGICE1/OSERDESE1s")]
        OLOGICE1,

        BSCANS,
        BUFHCEs,
        BUFIODQSs,
        BUFRs,
        CAPTUREs,
        DSP48E1s,
        EFUSE_USRs,
        FRAME_ECCs,
        GTXE1s,
        IBUFDS_GTXE1s,
        ICAPs,
        IDELAYCTRLs,
        IODELAYE1s,
        MMCM_ADVs,
        PCIE_2_0s,
        STARTUPs,
        SYSMONs,
        TEMAC_SINGLEs
    }

    public static class DeviceResources
    {
        private static Dictionary<string, EDeviceResource> _rmap;

        private static void CreateRMap()
        {
            _rmap = new Dictionary<string, EDeviceResource>();
            foreach (var value in Enum.GetValues(typeof(EDeviceResource)))
            {
                string str = PropEnum.ToString(value, EPropAssoc.PARReport);
                _rmap[str] = (EDeviceResource)value;
            }
        }

        public static bool ResolveResourceType(string rname, out EDeviceResource res)
        {
            if (_rmap == null)
                CreateRMap();
            return _rmap.TryGetValue(rname, out res);
        }
    }

}

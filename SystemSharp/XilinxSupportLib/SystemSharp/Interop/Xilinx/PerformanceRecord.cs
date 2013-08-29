/**
 * Copyright 2012-2013 Christian Köllner
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
using SystemSharp.Components;

namespace SystemSharp.Interop.Xilinx
{
    public class ResourceRecord
    {
        /*
        public EDevice Device { get; internal set; }
        public EPackage Package { get; internal set; }
        public ESpeedGrade SpeedGrade { get; internal set; }
        public EISEVersion ISEVersion { get; internal set; }
         * */

        public int SliceRegisters { get; internal set; }
        public int SliceLUTs { get; internal set; }
        public int OccupiedSlices { get; internal set; }
        public int RAMB36s { get; internal set; }
        public int RAMB18s { get; internal set; }
        public int DSP48E1s { get; internal set; }

        public void AssignResource(EDeviceResource res, int amount)
        {
            switch (res)
            {
                case EDeviceResource.SliceRegisters:
                    SliceRegisters = amount;
                    break;

                case EDeviceResource.SliceLUTs:
                    SliceLUTs = amount;
                    break;

                case EDeviceResource.OccupiedSlices:
                    OccupiedSlices = amount;
                    break;

                case EDeviceResource.RAMB18s:
                    RAMB18s = amount;
                    break;

                case EDeviceResource.RAMB36s:
                    RAMB36s = amount;
                    break;

                case EDeviceResource.DSP48E1s:
                    DSP48E1s = amount;
                    break;
            }
        }
    }

    public class PerformanceRecord : ResourceRecord
    {
        public double MinPeriod { get; internal set; }
        public byte[] Netlist { get; set; }
    }
}

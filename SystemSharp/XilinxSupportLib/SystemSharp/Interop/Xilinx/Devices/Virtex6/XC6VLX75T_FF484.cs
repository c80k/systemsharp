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
using System.Text;

namespace SystemSharp.Interop.Xilinx.Devices.Virtex6
{
    /// <summary>
    /// Frame model of a XC6VLX75T FF484 device.
    /// </summary>
    public class XC6VLX75T_FF484: XilinxDevice
    {
        public override EDevice Device
        {
            get { return EDevice.xc6vlx75t; }
        }

        public override EPackage Package
        {
            get { return EPackage.ff484; }
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public XC6VLX75T_FF484()
        {
            AddPin(new XilinxPin("A6"));
            AddPin(new XilinxPin("A7"));
            AddPin(new XilinxPin("A8"));
            AddPin(new XilinxPin("A9"));
            AddPin(new XilinxPin("A11"));
            AddPin(new XilinxPin("A12"));
            AddPin(new XilinxPin("A13"));
            AddPin(new XilinxPin("A14"));
            AddPin(new XilinxPin("A16"));
            AddPin(new XilinxPin("A17"));
            AddPin(new XilinxPin("A18"));
            AddPin(new XilinxPin("A19"));
            AddPin(new XilinxPin("A21"));
            AddPin(new XilinxPin("B6"));
            AddPin(new XilinxPin("B8"));
            AddPin(new XilinxPin("B9"));
            AddPin(new XilinxPin("B10"));
            AddPin(new XilinxPin("B11"));
            AddPin(new XilinxPin("B13"));
            AddPin(new XilinxPin("B14"));
            AddPin(new XilinxPin("B15"));
            AddPin(new XilinxPin("B16"));
            AddPin(new XilinxPin("B18"));
            AddPin(new XilinxPin("B19"));
            AddPin(new XilinxPin("B20"));
            AddPin(new XilinxPin("B21"));
            AddPin(new XilinxPin("B22"));
            AddPin(new XilinxPin("C6"));
            AddPin(new XilinxPin("C7"));
            AddPin(new XilinxPin("C8"));
            AddPin(new XilinxPin("C10"));
            AddPin(new XilinxPin("C11"));
            AddPin(new XilinxPin("C12"));
            AddPin(new XilinxPin("C13"));
            AddPin(new XilinxPin("C15"));
            AddPin(new XilinxPin("C16"));
            AddPin(new XilinxPin("C17"));
            AddPin(new XilinxPin("C18"));
            AddPin(new XilinxPin("C20"));
            AddPin(new XilinxPin("C21"));
            AddPin(new XilinxPin("C22"));
            AddPin(new XilinxPin("D7"));
            AddPin(new XilinxPin("D8"));
            AddPin(new XilinxPin("D9"));
            AddPin(new XilinxPin("D10"));
            AddPin(new XilinxPin("D12"));
            AddPin(new XilinxPin("D13"));
            AddPin(new XilinxPin("D14"));
            AddPin(new XilinxPin("D15"));
            AddPin(new XilinxPin("D17"));
            AddPin(new XilinxPin("D18"));
            AddPin(new XilinxPin("D19"));
            AddPin(new XilinxPin("D20"));
            AddPin(new XilinxPin("D22"));
            AddPin(new XilinxPin("E6"));
            AddPin(new XilinxPin("E7"));
            AddPin(new XilinxPin("E9"));
            AddPin(new XilinxPin("E10"));
            AddPin(new XilinxPin("E11"));
            AddPin(new XilinxPin("E12"));
            AddPin(new XilinxPin("E14"));
            AddPin(new XilinxPin("E15"));
            AddPin(new XilinxPin("E16"));
            AddPin(new XilinxPin("E17"));
            AddPin(new XilinxPin("E19"));
            AddPin(new XilinxPin("E20"));
            AddPin(new XilinxPin("E21"));
            AddPin(new XilinxPin("E22"));
            AddPin(new XilinxPin("F6"));
            AddPin(new XilinxPin("F7"));
            AddPin(new XilinxPin("F8"));
            AddPin(new XilinxPin("F9"));
            AddPin(new XilinxPin("F11"));
            AddPin(new XilinxPin("F12"));
            AddPin(new XilinxPin("F13"));
            AddPin(new XilinxPin("F14"));
            AddPin(new XilinxPin("F16"));
            AddPin(new XilinxPin("F17"));
            AddPin(new XilinxPin("F18"));
            AddPin(new XilinxPin("F19"));
            AddPin(new XilinxPin("F21"));
            AddPin(new XilinxPin("F22"));
            AddPin(new XilinxPin("G6"));
            AddPin(new XilinxPin("G8"));
            AddPin(new XilinxPin("G9"));
            AddPin(new XilinxPin("G10"));
            AddPin(new XilinxPin("G11"));
            AddPin(new XilinxPin("G13"));
            AddPin(new XilinxPin("G14"));
            AddPin(new XilinxPin("G15"));
            AddPin(new XilinxPin("G16"));
            AddPin(new XilinxPin("G18"));
            AddPin(new XilinxPin("G19"));
            AddPin(new XilinxPin("G20"));
            AddPin(new XilinxPin("G21"));
            AddPin(new XilinxPin("H8"));
            AddPin(new XilinxPin("H10"));
            AddPin(new XilinxPin("H11"));
            AddPin(new XilinxPin("H12"));
            AddPin(new XilinxPin("H13"));
            AddPin(new XilinxPin("H15"));
            AddPin(new XilinxPin("H16"));
            AddPin(new XilinxPin("H17"));
            AddPin(new XilinxPin("H18"));
            AddPin(new XilinxPin("H20"));
            AddPin(new XilinxPin("H21"));
            AddPin(new XilinxPin("H22"));
            AddPin(new XilinxPin("J17"));
            AddPin(new XilinxPin("J18"));
            AddPin(new XilinxPin("J19"));
            AddPin(new XilinxPin("J20"));
            AddPin(new XilinxPin("J22"));
            AddPin(new XilinxPin("K17"));
            AddPin(new XilinxPin("K19"));
            AddPin(new XilinxPin("K20"));
            AddPin(new XilinxPin("K21"));
            AddPin(new XilinxPin("K22"));
            AddPin(new XilinxPin("L17"));
            AddPin(new XilinxPin("L18"));
            AddPin(new XilinxPin("L19"));
            AddPin(new XilinxPin("L21"));
            AddPin(new XilinxPin("L22"));
            AddPin(new XilinxPin("M18"));
            AddPin(new XilinxPin("M19"));
            AddPin(new XilinxPin("M20"));
            AddPin(new XilinxPin("M21"));
            AddPin(new XilinxPin("N17"));
            AddPin(new XilinxPin("N18"));
            AddPin(new XilinxPin("N20"));
            AddPin(new XilinxPin("N21"));
            AddPin(new XilinxPin("N22"));
            AddPin(new XilinxPin("P17"));
            AddPin(new XilinxPin("P18"));
            AddPin(new XilinxPin("P19"));
            AddPin(new XilinxPin("P20"));
            AddPin(new XilinxPin("P22"));
            AddPin(new XilinxPin("R9"));
            AddPin(new XilinxPin("R14"));
            AddPin(new XilinxPin("R15"));
            AddPin(new XilinxPin("R16"));
            AddPin(new XilinxPin("R17"));
            AddPin(new XilinxPin("R19"));
            AddPin(new XilinxPin("R20"));
            AddPin(new XilinxPin("R21"));
            AddPin(new XilinxPin("R22"));
            AddPin(new XilinxPin("T6"));
            AddPin(new XilinxPin("T7"));
            AddPin(new XilinxPin("T8"));
            AddPin(new XilinxPin("T9"));
            AddPin(new XilinxPin("T11"));
            AddPin(new XilinxPin("T12"));
            AddPin(new XilinxPin("T13"));
            AddPin(new XilinxPin("T14"));
            AddPin(new XilinxPin("T16"));
            AddPin(new XilinxPin("T17"));
            AddPin(new XilinxPin("T18"));
            AddPin(new XilinxPin("T19"));
            AddPin(new XilinxPin("T21"));
            AddPin(new XilinxPin("T22"));
            AddPin(new XilinxPin("U6"));
            AddPin(new XilinxPin("U8"));
            AddPin(new XilinxPin("U9"));
            AddPin(new XilinxPin("U10"));
            AddPin(new XilinxPin("U11"));
            AddPin(new XilinxPin("U13"));
            AddPin(new XilinxPin("U14"));
            AddPin(new XilinxPin("U15"));
            AddPin(new XilinxPin("U16"));
            AddPin(new XilinxPin("U18"));
            AddPin(new XilinxPin("U19"));
            AddPin(new XilinxPin("U20"));
            AddPin(new XilinxPin("U21"));
            AddPin(new XilinxPin("V6"));
            AddPin(new XilinxPin("V7"));
            AddPin(new XilinxPin("V8"));
            AddPin(new XilinxPin("V10"));
            AddPin(new XilinxPin("V11"));
            AddPin(new XilinxPin("V12"));
            AddPin(new XilinxPin("V13"));
            AddPin(new XilinxPin("V15"));
            AddPin(new XilinxPin("V16"));
            AddPin(new XilinxPin("V17"));
            AddPin(new XilinxPin("V18"));
            AddPin(new XilinxPin("V20"));
            AddPin(new XilinxPin("V21"));
            AddPin(new XilinxPin("V22"));
            AddPin(new XilinxPin("W7"));
            AddPin(new XilinxPin("W8"));
            AddPin(new XilinxPin("W9"));
            AddPin(new XilinxPin("W10"));
            AddPin(new XilinxPin("W12"));
            AddPin(new XilinxPin("W13"));
            AddPin(new XilinxPin("W14"));
            AddPin(new XilinxPin("W15"));
            AddPin(new XilinxPin("W17"));
            AddPin(new XilinxPin("W18"));
            AddPin(new XilinxPin("W19"));
            AddPin(new XilinxPin("W20"));
            AddPin(new XilinxPin("W22"));
            AddPin(new XilinxPin("Y6"));
            AddPin(new XilinxPin("Y7"));
            AddPin(new XilinxPin("Y9"));
            AddPin(new XilinxPin("Y10"));
            AddPin(new XilinxPin("Y11"));
            AddPin(new XilinxPin("Y12"));
            AddPin(new XilinxPin("Y14"));
            AddPin(new XilinxPin("Y15"));
            AddPin(new XilinxPin("Y16"));
            AddPin(new XilinxPin("Y17"));
            AddPin(new XilinxPin("Y19"));
            AddPin(new XilinxPin("Y20"));
            AddPin(new XilinxPin("Y21"));
            AddPin(new XilinxPin("Y22"));
            AddPin(new XilinxPin("AA6"));
            AddPin(new XilinxPin("AA7"));
            AddPin(new XilinxPin("AA8"));
            AddPin(new XilinxPin("AA9"));
            AddPin(new XilinxPin("AA11"));
            AddPin(new XilinxPin("AA12"));
            AddPin(new XilinxPin("AA13"));
            AddPin(new XilinxPin("AA14"));
            AddPin(new XilinxPin("AA16"));
            AddPin(new XilinxPin("AA17"));
            AddPin(new XilinxPin("AA18"));
            AddPin(new XilinxPin("AA19"));
            AddPin(new XilinxPin("AA21"));
            AddPin(new XilinxPin("AA22"));
            AddPin(new XilinxPin("AB6"));
            AddPin(new XilinxPin("AB8"));
            AddPin(new XilinxPin("AB9"));
            AddPin(new XilinxPin("AB10"));
            AddPin(new XilinxPin("AB11"));
            AddPin(new XilinxPin("AB13"));
            AddPin(new XilinxPin("AB14"));
            AddPin(new XilinxPin("AB15"));
            AddPin(new XilinxPin("AB16"));
            AddPin(new XilinxPin("AB18"));
            AddPin(new XilinxPin("AB19"));
            AddPin(new XilinxPin("AB20"));
            AddPin(new XilinxPin("AB21"));
        }
    }
}

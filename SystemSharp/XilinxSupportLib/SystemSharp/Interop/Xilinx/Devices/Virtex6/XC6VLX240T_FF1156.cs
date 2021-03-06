﻿/**
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
    /// Frame model of a XC6VLX240T FF1156 device.
    /// </summary>
    public class XC6VLX240T_FF1156 : XilinxDevice
    {
        public override EDevice Device
        {
            get { return EDevice.xc6vlx240t; }
        }

        public override EPackage Package
        {
            get { return EPackage.ff1156; }
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public XC6VLX240T_FF1156()
        {
            AddPin(new XilinxPin("AD25"));
            AddPin(new XilinxPin("AD26"));
            AddPin(new XilinxPin("AE27"));
            AddPin(new XilinxPin("AD27"));
            AddPin(new XilinxPin("AH33"));
            AddPin(new XilinxPin("AH32"));
            AddPin(new XilinxPin("AE28"));
            AddPin(new XilinxPin("AE29"));
            AddPin(new XilinxPin("AJ34"));
            AddPin(new XilinxPin("AH34"));
            AddPin(new XilinxPin("AF28"));
            AddPin(new XilinxPin("AF29"));
            AddPin(new XilinxPin("AL34"));
            AddPin(new XilinxPin("AK34"));
            AddPin(new XilinxPin("AH29"));
            AddPin(new XilinxPin("AH30"));
            AddPin(new XilinxPin("AN33"));
            AddPin(new XilinxPin("AN34"));
            AddPin(new XilinxPin("AG27"));
            AddPin(new XilinxPin("AG28"));
            AddPin(new XilinxPin("AF30"));
            AddPin(new XilinxPin("AG30"));
            AddPin(new XilinxPin("AF26"));
            AddPin(new XilinxPin("AE26"));
            AddPin(new XilinxPin("AJ31"));
            AddPin(new XilinxPin("AJ32"));
            AddPin(new XilinxPin("AJ29"));
            AddPin(new XilinxPin("AJ30"));
            AddPin(new XilinxPin("AK33"));
            AddPin(new XilinxPin("AK32"));
            AddPin(new XilinxPin("AL31"));
            AddPin(new XilinxPin("AK31"));
            AddPin(new XilinxPin("AM33"));
            AddPin(new XilinxPin("AL33"));
            AddPin(new XilinxPin("AN32"));
            AddPin(new XilinxPin("AM32"));
            AddPin(new XilinxPin("AP32"));
            AddPin(new XilinxPin("AP33"));
            AddPin(new XilinxPin("AL30"));
            AddPin(new XilinxPin("AM31"));
            AddPin(new XilinxPin("AA34"));
            AddPin(new XilinxPin("AA33"));
            AddPin(new XilinxPin("AA30"));
            AddPin(new XilinxPin("AA31"));
            AddPin(new XilinxPin("AD34"));
            AddPin(new XilinxPin("AC34"));
            AddPin(new XilinxPin("AB30"));
            AddPin(new XilinxPin("AB31"));
            AddPin(new XilinxPin("AC33"));
            AddPin(new XilinxPin("AB33"));
            AddPin(new XilinxPin("AE31"));
            AddPin(new XilinxPin("AD31"));
            AddPin(new XilinxPin("AA25"));
            AddPin(new XilinxPin("Y26"));
            AddPin(new XilinxPin("AA28"));
            AddPin(new XilinxPin("AA29"));
            AddPin(new XilinxPin("AE34"));
            AddPin(new XilinxPin("AF34"));
            AddPin(new XilinxPin("AD30"));
            AddPin(new XilinxPin("AC30"));
            AddPin(new XilinxPin("AE33"));
            AddPin(new XilinxPin("AF33"));
            AddPin(new XilinxPin("AD29"));
            AddPin(new XilinxPin("AC29"));
            AddPin(new XilinxPin("AB32"));
            AddPin(new XilinxPin("AC32"));
            AddPin(new XilinxPin("AB28"));
            AddPin(new XilinxPin("AC28"));
            AddPin(new XilinxPin("AD32"));
            AddPin(new XilinxPin("AE32"));
            AddPin(new XilinxPin("AB27"));
            AddPin(new XilinxPin("AC27"));
            AddPin(new XilinxPin("AG33"));
            AddPin(new XilinxPin("AG32"));
            AddPin(new XilinxPin("AA26"));
            AddPin(new XilinxPin("AB26"));
            AddPin(new XilinxPin("AG31"));
            AddPin(new XilinxPin("AF31"));
            AddPin(new XilinxPin("AB25"));
            AddPin(new XilinxPin("AC25"));
            AddPin(new XilinxPin("U25"));
            AddPin(new XilinxPin("T25"));
            AddPin(new XilinxPin("T28"));
            AddPin(new XilinxPin("T29"));
            AddPin(new XilinxPin("R33"));
            AddPin(new XilinxPin("R34"));
            AddPin(new XilinxPin("T30"));
            AddPin(new XilinxPin("T31"));
            AddPin(new XilinxPin("T33"));
            AddPin(new XilinxPin("T34"));
            AddPin(new XilinxPin("U26"));
            AddPin(new XilinxPin("U27"));
            AddPin(new XilinxPin("U33"));
            AddPin(new XilinxPin("U32"));
            AddPin(new XilinxPin("U28"));
            AddPin(new XilinxPin("V29"));
            AddPin(new XilinxPin("U31"));
            AddPin(new XilinxPin("U30"));
            AddPin(new XilinxPin("V30"));
            AddPin(new XilinxPin("W30"));
            AddPin(new XilinxPin("V34"));
            AddPin(new XilinxPin("W34"));
            AddPin(new XilinxPin("V28"));
            AddPin(new XilinxPin("V27"));
            AddPin(new XilinxPin("V32"));
            AddPin(new XilinxPin("V33"));
            AddPin(new XilinxPin("Y32"));
            AddPin(new XilinxPin("Y31"));
            AddPin(new XilinxPin("Y33"));
            AddPin(new XilinxPin("Y34"));
            AddPin(new XilinxPin("W29"));
            AddPin(new XilinxPin("Y29"));
            AddPin(new XilinxPin("W31"));
            AddPin(new XilinxPin("W32"));
            AddPin(new XilinxPin("Y28"));
            AddPin(new XilinxPin("Y27"));
            AddPin(new XilinxPin("W25"));
            AddPin(new XilinxPin("V25"));
            AddPin(new XilinxPin("W27"));
            AddPin(new XilinxPin("W26"));
            AddPin(new XilinxPin("M31"));
            AddPin(new XilinxPin("L31"));
            AddPin(new XilinxPin("N25"));
            AddPin(new XilinxPin("M25"));
            AddPin(new XilinxPin("K32"));
            AddPin(new XilinxPin("K31"));
            AddPin(new XilinxPin("M26"));
            AddPin(new XilinxPin("M27"));
            AddPin(new XilinxPin("P31"));
            AddPin(new XilinxPin("P30"));
            AddPin(new XilinxPin("N27"));
            AddPin(new XilinxPin("P27"));
            AddPin(new XilinxPin("L33"));
            AddPin(new XilinxPin("M32"));
            AddPin(new XilinxPin("L28"));
            AddPin(new XilinxPin("M28"));
            AddPin(new XilinxPin("N32"));
            AddPin(new XilinxPin("P32"));
            AddPin(new XilinxPin("N28"));
            AddPin(new XilinxPin("N29"));
            AddPin(new XilinxPin("N33"));
            AddPin(new XilinxPin("M33"));
            AddPin(new XilinxPin("L29"));
            AddPin(new XilinxPin("L30"));
            AddPin(new XilinxPin("P25"));
            AddPin(new XilinxPin("P26"));
            AddPin(new XilinxPin("R28"));
            AddPin(new XilinxPin("R27"));
            AddPin(new XilinxPin("R31"));
            AddPin(new XilinxPin("R32"));
            AddPin(new XilinxPin("R26"));
            AddPin(new XilinxPin("T26"));
            AddPin(new XilinxPin("K34"));
            AddPin(new XilinxPin("L34"));
            AddPin(new XilinxPin("M30"));
            AddPin(new XilinxPin("N30"));
            AddPin(new XilinxPin("N34"));
            AddPin(new XilinxPin("P34"));
            AddPin(new XilinxPin("P29"));
            AddPin(new XilinxPin("R29"));
            AddPin(new XilinxPin("C32"));
            AddPin(new XilinxPin("B32"));
            AddPin(new XilinxPin("J26"));
            AddPin(new XilinxPin("J27"));
            AddPin(new XilinxPin("E32"));
            AddPin(new XilinxPin("E33"));
            AddPin(new XilinxPin("F30"));
            AddPin(new XilinxPin("G30"));
            AddPin(new XilinxPin("A33"));
            AddPin(new XilinxPin("B33"));
            AddPin(new XilinxPin("G31"));
            AddPin(new XilinxPin("H30"));
            AddPin(new XilinxPin("C33"));
            AddPin(new XilinxPin("B34"));
            AddPin(new XilinxPin("K28"));
            AddPin(new XilinxPin("J29"));
            AddPin(new XilinxPin("D34"));
            AddPin(new XilinxPin("C34"));
            AddPin(new XilinxPin("K26"));
            AddPin(new XilinxPin("K27"));
            AddPin(new XilinxPin("F33"));
            AddPin(new XilinxPin("G33"));
            AddPin(new XilinxPin("F31"));
            AddPin(new XilinxPin("E31"));
            AddPin(new XilinxPin("E34"));
            AddPin(new XilinxPin("F34"));
            AddPin(new XilinxPin("J30"));
            AddPin(new XilinxPin("K29"));
            AddPin(new XilinxPin("H34"));
            AddPin(new XilinxPin("H33"));
            AddPin(new XilinxPin("D31"));
            AddPin(new XilinxPin("D32"));
            AddPin(new XilinxPin("K33"));
            AddPin(new XilinxPin("J34"));
            AddPin(new XilinxPin("G32"));
            AddPin(new XilinxPin("H32"));
            AddPin(new XilinxPin("L25"));
            AddPin(new XilinxPin("L26"));
            AddPin(new XilinxPin("J31"));
            AddPin(new XilinxPin("J32"));
            AddPin(new XilinxPin("AE21"));
            AddPin(new XilinxPin("AD21"));
            AddPin(new XilinxPin("AM18"));
            AddPin(new XilinxPin("AL18"));
            AddPin(new XilinxPin("AG22"));
            AddPin(new XilinxPin("AH22"));
            AddPin(new XilinxPin("AP19"));
            AddPin(new XilinxPin("AN18"));
            AddPin(new XilinxPin("AK22"));
            AddPin(new XilinxPin("AJ22"));
            AddPin(new XilinxPin("AN19"));
            AddPin(new XilinxPin("AN20"));
            AddPin(new XilinxPin("AC20"));
            AddPin(new XilinxPin("AD20"));
            AddPin(new XilinxPin("AM20"));
            AddPin(new XilinxPin("AL20"));
            AddPin(new XilinxPin("AF19"));
            AddPin(new XilinxPin("AE19"));
            AddPin(new XilinxPin("AP20"));
            AddPin(new XilinxPin("AP21"));
            AddPin(new XilinxPin("AK19"));
            AddPin(new XilinxPin("AL19"));
            AddPin(new XilinxPin("AF20"));
            AddPin(new XilinxPin("AF21"));
            AddPin(new XilinxPin("AJ20"));
            AddPin(new XilinxPin("AH20"));
            AddPin(new XilinxPin("AM21"));
            AddPin(new XilinxPin("AL21"));
            AddPin(new XilinxPin("AC19"));
            AddPin(new XilinxPin("AD19"));
            AddPin(new XilinxPin("AM23"));
            AddPin(new XilinxPin("AL23"));
            AddPin(new XilinxPin("AK21"));
            AddPin(new XilinxPin("AJ21"));
            AddPin(new XilinxPin("AM22"));
            AddPin(new XilinxPin("AN22"));
            AddPin(new XilinxPin("AG20"));
            AddPin(new XilinxPin("AG21"));
            AddPin(new XilinxPin("AP22"));
            AddPin(new XilinxPin("AN23"));
            AddPin(new XilinxPin("AH27"));
            AddPin(new XilinxPin("AH28"));
            AddPin(new XilinxPin("AN30"));
            AddPin(new XilinxPin("AM30"));
            AddPin(new XilinxPin("AG25"));
            AddPin(new XilinxPin("AG26"));
            AddPin(new XilinxPin("AP30"));
            AddPin(new XilinxPin("AP31"));
            AddPin(new XilinxPin("AL29"));
            AddPin(new XilinxPin("AK29"));
            AddPin(new XilinxPin("AN29"));
            AddPin(new XilinxPin("AP29"));
            AddPin(new XilinxPin("AL28"));
            AddPin(new XilinxPin("AK28"));
            AddPin(new XilinxPin("AN28"));
            AddPin(new XilinxPin("AM28"));
            AddPin(new XilinxPin("AH25"));
            AddPin(new XilinxPin("AJ25"));
            AddPin(new XilinxPin("AN27"));
            AddPin(new XilinxPin("AM27"));
            AddPin(new XilinxPin("AK27"));
            AddPin(new XilinxPin("AJ27"));
            AddPin(new XilinxPin("AH23"));
            AddPin(new XilinxPin("AH24"));
            AddPin(new XilinxPin("AK26"));
            AddPin(new XilinxPin("AJ26"));
            AddPin(new XilinxPin("AL26"));
            AddPin(new XilinxPin("AM26"));
            AddPin(new XilinxPin("AJ24"));
            AddPin(new XilinxPin("AK24"));
            AddPin(new XilinxPin("AP27"));
            AddPin(new XilinxPin("AP26"));
            AddPin(new XilinxPin("AM25"));
            AddPin(new XilinxPin("AL25"));
            AddPin(new XilinxPin("AN25"));
            AddPin(new XilinxPin("AN24"));
            AddPin(new XilinxPin("AK23"));
            AddPin(new XilinxPin("AL24"));
            AddPin(new XilinxPin("AP25"));
            AddPin(new XilinxPin("AP24"));
            AddPin(new XilinxPin("L23"));
            AddPin(new XilinxPin("M22"));
            AddPin(new XilinxPin("K24"));
            AddPin(new XilinxPin("K23"));
            AddPin(new XilinxPin("M23"));
            AddPin(new XilinxPin("L24"));
            AddPin(new XilinxPin("F24"));
            AddPin(new XilinxPin("F23"));
            AddPin(new XilinxPin("N23"));
            AddPin(new XilinxPin("N24"));
            AddPin(new XilinxPin("H23"));
            AddPin(new XilinxPin("G23"));
            AddPin(new XilinxPin("R24"));
            AddPin(new XilinxPin("P24"));
            AddPin(new XilinxPin("H25"));
            AddPin(new XilinxPin("H24"));
            AddPin(new XilinxPin("T24"));
            AddPin(new XilinxPin("T23"));
            AddPin(new XilinxPin("J25"));
            AddPin(new XilinxPin("J24"));
            AddPin(new XilinxPin("U23"));
            AddPin(new XilinxPin("V23"));
            AddPin(new XilinxPin("AD24"));
            AddPin(new XilinxPin("AE24"));
            AddPin(new XilinxPin("V24"));
            AddPin(new XilinxPin("W24"));
            AddPin(new XilinxPin("AF25"));
            AddPin(new XilinxPin("AF24"));
            AddPin(new XilinxPin("Y24"));
            AddPin(new XilinxPin("AA24"));
            AddPin(new XilinxPin("AF23"));
            AddPin(new XilinxPin("AG23"));
            AddPin(new XilinxPin("AA23"));
            AddPin(new XilinxPin("AB23"));
            AddPin(new XilinxPin("AE23"));
            AddPin(new XilinxPin("AE22"));
            AddPin(new XilinxPin("AC23"));
            AddPin(new XilinxPin("AC24"));
            AddPin(new XilinxPin("AC22"));
            AddPin(new XilinxPin("AD22"));
            AddPin(new XilinxPin("D25"));
            AddPin(new XilinxPin("D26"));
            AddPin(new XilinxPin("C24"));
            AddPin(new XilinxPin("C25"));
            AddPin(new XilinxPin("E26"));
            AddPin(new XilinxPin("F26"));
            AddPin(new XilinxPin("B25"));
            AddPin(new XilinxPin("A25"));
            AddPin(new XilinxPin("D27"));
            AddPin(new XilinxPin("E27"));
            AddPin(new XilinxPin("B26"));
            AddPin(new XilinxPin("A26"));
            AddPin(new XilinxPin("G26"));
            AddPin(new XilinxPin("G27"));
            AddPin(new XilinxPin("B27"));
            AddPin(new XilinxPin("C27"));
            AddPin(new XilinxPin("D24"));
            AddPin(new XilinxPin("E24"));
            AddPin(new XilinxPin("C28"));
            AddPin(new XilinxPin("B28"));
            AddPin(new XilinxPin("C29"));
            AddPin(new XilinxPin("D29"));
            AddPin(new XilinxPin("F25"));
            AddPin(new XilinxPin("G25"));
            AddPin(new XilinxPin("H27"));
            AddPin(new XilinxPin("G28"));
            AddPin(new XilinxPin("A28"));
            AddPin(new XilinxPin("A29"));
            AddPin(new XilinxPin("F28"));
            AddPin(new XilinxPin("E28"));
            AddPin(new XilinxPin("A30"));
            AddPin(new XilinxPin("B30"));
            AddPin(new XilinxPin("E29"));
            AddPin(new XilinxPin("F29"));
            AddPin(new XilinxPin("C30"));
            AddPin(new XilinxPin("D30"));
            AddPin(new XilinxPin("H28"));
            AddPin(new XilinxPin("H29"));
            AddPin(new XilinxPin("B31"));
            AddPin(new XilinxPin("A31"));
            AddPin(new XilinxPin("C20"));
            AddPin(new XilinxPin("D20"));
            AddPin(new XilinxPin("A23"));
            AddPin(new XilinxPin("A24"));
            AddPin(new XilinxPin("G21"));
            AddPin(new XilinxPin("G22"));
            AddPin(new XilinxPin("B23"));
            AddPin(new XilinxPin("C23"));
            AddPin(new XilinxPin("J20"));
            AddPin(new XilinxPin("J21"));
            AddPin(new XilinxPin("B21"));
            AddPin(new XilinxPin("B22"));
            AddPin(new XilinxPin("E22"));
            AddPin(new XilinxPin("E23"));
            AddPin(new XilinxPin("A20"));
            AddPin(new XilinxPin("A21"));
            AddPin(new XilinxPin("F19"));
            AddPin(new XilinxPin("F20"));
            AddPin(new XilinxPin("B20"));
            AddPin(new XilinxPin("C19"));
            AddPin(new XilinxPin("F21"));
            AddPin(new XilinxPin("G20"));
            AddPin(new XilinxPin("H19"));
            AddPin(new XilinxPin("H20"));
            AddPin(new XilinxPin("D21"));
            AddPin(new XilinxPin("E21"));
            AddPin(new XilinxPin("E19"));
            AddPin(new XilinxPin("D19"));
            AddPin(new XilinxPin("H22"));
            AddPin(new XilinxPin("J22"));
            AddPin(new XilinxPin("A18"));
            AddPin(new XilinxPin("A19"));
            AddPin(new XilinxPin("K21"));
            AddPin(new XilinxPin("K22"));
            AddPin(new XilinxPin("B18"));
            AddPin(new XilinxPin("C18"));
            AddPin(new XilinxPin("L20"));
            AddPin(new XilinxPin("L21"));
            AddPin(new XilinxPin("C22"));
            AddPin(new XilinxPin("D22"));
            AddPin(new XilinxPin("AG15"));
            AddPin(new XilinxPin("AF15"));
            AddPin(new XilinxPin("AK14"));
            AddPin(new XilinxPin("AJ14"));
            AddPin(new XilinxPin("AJ15"));
            AddPin(new XilinxPin("AH15"));
            AddPin(new XilinxPin("AL15"));
            AddPin(new XilinxPin("AL14"));
            AddPin(new XilinxPin("AG16"));
            AddPin(new XilinxPin("AF16"));
            AddPin(new XilinxPin("AN15"));
            AddPin(new XilinxPin("AM15"));
            AddPin(new XilinxPin("AJ17"));
            AddPin(new XilinxPin("AJ16"));
            AddPin(new XilinxPin("AP16"));
            AddPin(new XilinxPin("AP15"));
            AddPin(new XilinxPin("AH17"));
            AddPin(new XilinxPin("AG17"));
            AddPin(new XilinxPin("AC15"));
            AddPin(new XilinxPin("AD15"));
            AddPin(new XilinxPin("AE16"));
            AddPin(new XilinxPin("AD16"));
            AddPin(new XilinxPin("AC18"));
            AddPin(new XilinxPin("AC17"));
            AddPin(new XilinxPin("AH18"));
            AddPin(new XilinxPin("AG18"));
            AddPin(new XilinxPin("AN17"));
            AddPin(new XilinxPin("AP17"));
            AddPin(new XilinxPin("AJ19"));
            AddPin(new XilinxPin("AH19"));
            AddPin(new XilinxPin("AM17"));
            AddPin(new XilinxPin("AM16"));
            AddPin(new XilinxPin("AD17"));
            AddPin(new XilinxPin("AE17"));
            AddPin(new XilinxPin("AK18"));
            AddPin(new XilinxPin("AK17"));
            AddPin(new XilinxPin("AE18"));
            AddPin(new XilinxPin("AF18"));
            AddPin(new XilinxPin("AL16"));
            AddPin(new XilinxPin("AK16"));
            AddPin(new XilinxPin("AE13"));
            AddPin(new XilinxPin("AE12"));
            AddPin(new XilinxPin("AJ11"));
            AddPin(new XilinxPin("AK11"));
            AddPin(new XilinxPin("AD14"));
            AddPin(new XilinxPin("AC14"));
            AddPin(new XilinxPin("AK12"));
            AddPin(new XilinxPin("AJ12"));
            AddPin(new XilinxPin("AF11"));
            AddPin(new XilinxPin("AE11"));
            AddPin(new XilinxPin("AM10"));
            AddPin(new XilinxPin("AL10"));
            AddPin(new XilinxPin("AG11"));
            AddPin(new XilinxPin("AG10"));
            AddPin(new XilinxPin("AL11"));
            AddPin(new XilinxPin("AM11"));
            AddPin(new XilinxPin("AJ10"));
            AddPin(new XilinxPin("AH10"));
            AddPin(new XilinxPin("AC13"));
            AddPin(new XilinxPin("AC12"));
            AddPin(new XilinxPin("AD12"));
            AddPin(new XilinxPin("AD11"));
            AddPin(new XilinxPin("AP11"));
            AddPin(new XilinxPin("AP12"));
            AddPin(new XilinxPin("AF13"));
            AddPin(new XilinxPin("AG13"));
            AddPin(new XilinxPin("AM12"));
            AddPin(new XilinxPin("AN12"));
            AddPin(new XilinxPin("AE14"));
            AddPin(new XilinxPin("AF14"));
            AddPin(new XilinxPin("AN13"));
            AddPin(new XilinxPin("AM13"));
            AddPin(new XilinxPin("AG12"));
            AddPin(new XilinxPin("AH12"));
            AddPin(new XilinxPin("AK13"));
            AddPin(new XilinxPin("AL13"));
            AddPin(new XilinxPin("AH13"));
            AddPin(new XilinxPin("AH14"));
            AddPin(new XilinxPin("AP14"));
            AddPin(new XilinxPin("AN14"));
            AddPin(new XilinxPin("J9"));
            AddPin(new XilinxPin("H9"));
            AddPin(new XilinxPin("A10"));
            AddPin(new XilinxPin("B10"));
            AddPin(new XilinxPin("F9"));
            AddPin(new XilinxPin("F10"));
            AddPin(new XilinxPin("C10"));
            AddPin(new XilinxPin("D10"));
            AddPin(new XilinxPin("C9"));
            AddPin(new XilinxPin("D9"));
            AddPin(new XilinxPin("A9"));
            AddPin(new XilinxPin("A8"));
            AddPin(new XilinxPin("E8"));
            AddPin(new XilinxPin("E9"));
            AddPin(new XilinxPin("B8"));
            AddPin(new XilinxPin("C8"));
            AddPin(new XilinxPin("L9"));
            AddPin(new XilinxPin("K9"));
            AddPin(new XilinxPin("L10"));
            AddPin(new XilinxPin("M10"));
            AddPin(new XilinxPin("AC10"));
            AddPin(new XilinxPin("AB10"));
            AddPin(new XilinxPin("AH9"));
            AddPin(new XilinxPin("AJ9"));
            AddPin(new XilinxPin("AD10"));
            AddPin(new XilinxPin("AC9"));
            AddPin(new XilinxPin("AK8"));
            AddPin(new XilinxPin("AL8"));
            AddPin(new XilinxPin("AD9"));
            AddPin(new XilinxPin("AE9"));
            AddPin(new XilinxPin("AK9"));
            AddPin(new XilinxPin("AL9"));
            AddPin(new XilinxPin("AF9"));
            AddPin(new XilinxPin("AF10"));
            AddPin(new XilinxPin("AN9"));
            AddPin(new XilinxPin("AP9"));
            AddPin(new XilinxPin("AG8"));
            AddPin(new XilinxPin("AH8"));
            AddPin(new XilinxPin("AN10"));
            AddPin(new XilinxPin("AP10"));
            AddPin(new XilinxPin("G13"));
            AddPin(new XilinxPin("H14"));
            AddPin(new XilinxPin("D14"));
            AddPin(new XilinxPin("C14"));
            AddPin(new XilinxPin("G11"));
            AddPin(new XilinxPin("F11"));
            AddPin(new XilinxPin("A13"));
            AddPin(new XilinxPin("A14"));
            AddPin(new XilinxPin("G12"));
            AddPin(new XilinxPin("H13"));
            AddPin(new XilinxPin("F14"));
            AddPin(new XilinxPin("E14"));
            AddPin(new XilinxPin("H10"));
            AddPin(new XilinxPin("G10"));
            AddPin(new XilinxPin("B12"));
            AddPin(new XilinxPin("B13"));
            AddPin(new XilinxPin("K14"));
            AddPin(new XilinxPin("J14"));
            AddPin(new XilinxPin("L13"));
            AddPin(new XilinxPin("M13"));
            AddPin(new XilinxPin("M12"));
            AddPin(new XilinxPin("M11"));
            AddPin(new XilinxPin("C13"));
            AddPin(new XilinxPin("C12"));
            AddPin(new XilinxPin("H12"));
            AddPin(new XilinxPin("J12"));
            AddPin(new XilinxPin("A11"));
            AddPin(new XilinxPin("B11"));
            AddPin(new XilinxPin("J11"));
            AddPin(new XilinxPin("J10"));
            AddPin(new XilinxPin("E13"));
            AddPin(new XilinxPin("F13"));
            AddPin(new XilinxPin("K11"));
            AddPin(new XilinxPin("L11"));
            AddPin(new XilinxPin("D12"));
            AddPin(new XilinxPin("E12"));
            AddPin(new XilinxPin("K13"));
            AddPin(new XilinxPin("K12"));
            AddPin(new XilinxPin("D11"));
            AddPin(new XilinxPin("E11"));
            AddPin(new XilinxPin("F18"));
            AddPin(new XilinxPin("E17"));
            AddPin(new XilinxPin("E18"));
            AddPin(new XilinxPin("D17"));
            AddPin(new XilinxPin("K18"));
            AddPin(new XilinxPin("K17"));
            AddPin(new XilinxPin("H17"));
            AddPin(new XilinxPin("G17"));
            AddPin(new XilinxPin("L19"));
            AddPin(new XilinxPin("L18"));
            AddPin(new XilinxPin("C17"));
            AddPin(new XilinxPin("B17"));
            AddPin(new XilinxPin("K19"));
            AddPin(new XilinxPin("J19"));
            AddPin(new XilinxPin("M18"));
            AddPin(new XilinxPin("M17"));
            AddPin(new XilinxPin("G18"));
            AddPin(new XilinxPin("H18"));
            AddPin(new XilinxPin("K16"));
            AddPin(new XilinxPin("L16"));
            AddPin(new XilinxPin("L15"));
            AddPin(new XilinxPin("L14"));
            AddPin(new XilinxPin("A16"));
            AddPin(new XilinxPin("B16"));
            AddPin(new XilinxPin("F16"));
            AddPin(new XilinxPin("G16"));
            AddPin(new XilinxPin("E16"));
            AddPin(new XilinxPin("D16"));
            AddPin(new XilinxPin("J17"));
            AddPin(new XilinxPin("J16"));
            AddPin(new XilinxPin("A15"));
            AddPin(new XilinxPin("B15"));
            AddPin(new XilinxPin("G15"));
            AddPin(new XilinxPin("F15"));
            AddPin(new XilinxPin("M16"));
            AddPin(new XilinxPin("M15"));
            AddPin(new XilinxPin("H15"));
            AddPin(new XilinxPin("J15"));
            AddPin(new XilinxPin("D15"));
            AddPin(new XilinxPin("C15"));
        }
    }
}

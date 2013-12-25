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
 * 
 * */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Interop.Xilinx
{
    public abstract class XilinxDevice: AbstractXilinxDevice
    {
        private HashBasedPropMap<string, XilinxPin> _pins = new HashBasedPropMap<string, XilinxPin>();
        public override IPropMap<string, XilinxPin> Pins { get { return _pins; } }
        public override IEnumerable<XilinxPin> PinList { get { return _pins.Values; } }

        /// <summary>
        /// Adds a pin to the device.
        /// </summary>
        /// <param name="pin">pin to add</param>
        protected void AddPin(XilinxPin pin)
        {
            _pins[pin.Name] = pin;
        }
    }
}

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
 * 
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Interop.Xilinx
{
    /// <summary>
    /// Default implementation of <c>AbstractXilinxDevice</c>.
    /// </summary>
    public class DefaultXilinxDevice: AbstractXilinxDevice
    {
        private CacheBasedPropMap<string, XilinxPin> _pins;

        private XilinxPin CreatePin(string name)
        {
            return new XilinxPin(name);
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public DefaultXilinxDevice()
        {
            _pins = new CacheBasedPropMap<string, XilinxPin>(CreatePin);
        }

        private EDevice _device;
        public override EDevice Device 
        {
            get { return _device; }
        }

        /// <summary>
        /// Sets the device.
        /// </summary>
        public void SetDevice(EDevice device)
        {
            _device = device;
        }

        private EPackage _package;
        public override EPackage Package
        {
            get { return _package; }
        }

        /// <summary>
        /// Sets the package.
        /// </summary>
        public void SetPackage(EPackage package)
        {
            _package = package;
        }

        public override IPropMap<string, XilinxPin> Pins
        {
            get { return _pins; }
        }

        public override IEnumerable<XilinxPin> PinList
        {
            get { return _pins.Values; }
        }
    }
}

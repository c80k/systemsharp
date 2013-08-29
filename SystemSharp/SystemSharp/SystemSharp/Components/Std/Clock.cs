/**
 * Copyright 2011-2013 Christian Köllner, David Hlavac
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
using SystemSharp.Analysis;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;

namespace SystemSharp.Components.Std
{
    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    public class Clock : Component
    {
        public Out<StdLogic> Clk { private get; set; }

        private Time _hiPeriod;
        private Time _loPeriod;
        private ClockSpecAttribute _clockSpec;

        public Clock(Time period, double duty = 0.5)
        {
            _hiPeriod = duty * period;
            _loPeriod = (1.0 - duty) * period;
            _clockSpec = new ClockSpecAttribute(period, duty);
        }

        private async void Process()
        {
            Clk.Next = '0';
            await _loPeriod;
            Clk.Next = '1';
            await _hiPeriod;
        }

        protected override void Initialize()
        {
            AddThread(Process);
            ((SignalDescriptor)((IDescriptive)Clk).Descriptor).AddAttribute(_clockSpec);
        }
    }
}

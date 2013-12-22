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
using SystemSharp.Components;

namespace SystemSharp.Meta
{
    /// <summary>
    /// This attribute is usually attached to signal descriptors and indicates that the underlying
    /// signal is used as a clock line.
    /// </summary>
    public class ClockSpecAttribute
    {
        /// <summary>
        /// Clock period
        /// </summary>
        public Time Period { get; private set; }

        /// <summary>
        ///  Clock duty cycle
        /// </summary>
        public double Duty { get; private set; }

        /// <summary>
        /// Constructs an instance of this attribute.
        /// </summary>
        /// <param name="period">clock period</param>
        /// <param name="duty">duty cycle</param>
        public ClockSpecAttribute(Time period, double duty)
        {
            Period = period;
            Duty = duty;
        }
    }

    [AttributeUsage(AttributeTargets.Property|AttributeTargets.Field)]
    public class PerformanceRelevant: Attribute
    {
    }
}

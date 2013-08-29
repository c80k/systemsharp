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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using SystemSharp.Components;

namespace SystemSharp.Interop.Xilinx.TRCE
{
    public static class TWXParser
    {
        public static void ParseMinPeriod(string reportPath, PerformanceRecord rec)
        {
            var doc = new XmlDocument();
            doc.Load(reportPath);
            var report = doc.GetElementsByTagName("twReport").Item(0);
            if (report == null)
                return;
            var sum = doc.GetElementsByTagName("twSum").Item(0);
            if (sum == null)
                return;
            var stats = doc.GetElementsByTagName("twStats").Item(0);
            if (stats == null)
                return;
            var minPer = doc.GetElementsByTagName("twMinPer").Item(0);
            if (minPer == null)
                return;
            double value;
            if (!double.TryParse(
                minPer.InnerText, 
                NumberStyles.Number, 
                CultureInfo.InvariantCulture, out value))
                return;
            rec.MinPeriod = value;
        }
    }
}

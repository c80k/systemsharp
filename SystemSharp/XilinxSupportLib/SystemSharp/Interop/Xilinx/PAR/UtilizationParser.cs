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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SystemSharp.Interop.Xilinx.PAR
{
    public static class UtilizationParser
    {
        private static readonly Regex LineRegex = new Regex(@"^\s*Number of (?<rname>[^:]+):\s+(?<count>[\d,]+) out of\s+(?<total>[\d,]+)");

        private static bool MatchLine(string line, out EDeviceResource res, out int count, out int total)
        {
            var match = LineRegex.Match(line);
            if (match.Success)
            {
                string rname = match.Result("${rname}");
                if (DeviceResources.ResolveResourceType(rname, out res))
                {
                    count = int.Parse(match.Result("${count}"), NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                    total = int.Parse(match.Result("${total}"), NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            res = default(EDeviceResource);
            count = 0;
            total = 0;
            return false;
        }

        public static void ParseUtilization(string reportPath, ResourceRecord rec)
        {
            using (var rd = new StreamReader(reportPath))
            {
                while (!rd.EndOfStream)
                {
                    string line = rd.ReadLine();
                    EDeviceResource res;
                    int count, total;
                    if (MatchLine(line, out res, out count, out total))
                    {
                        rec.AssignResource(res, count);
                    }
                }
                rd.Close();
            }
        }

        public static void ParseTotals(string reportPath, ResourceRecord rec)
        {
            using (var rd = new StreamReader(reportPath))
            {
                while (!rd.EndOfStream)
                {
                    string line = rd.ReadLine();
                    EDeviceResource res;
                    int count, total;
                    if (MatchLine(line, out res, out count, out total))
                    {
                        rec.AssignResource(res, total);
                    }
                }
                rd.Close();
            }
        }
    }
}

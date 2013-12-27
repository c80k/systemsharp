/**
 * Copyright 2011 Christian Köllner
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

namespace SystemSharp.Common
{
    using System.Text;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    static class Subst
    {
        public static void MapDrive(char letter, string path)
        {
            if (!DefineDosDevice(0, devName(letter), path))
                throw new Win32Exception();
        }
        public static void UnmapDrive(char letter)
        {
            if (!DefineDosDevice(2, devName(letter), null))
                throw new Win32Exception();
        }
        public static string GetDriveMapping(char letter)
        {
            var sb = new StringBuilder(259);
            if (QueryDosDevice(devName(letter), sb, sb.Capacity) == 0)
            {
                // Return empty string if the drive is not mapped
                int err = Marshal.GetLastWin32Error();
                if (err == 2) return "";
                throw new Win32Exception();
            }
            return sb.ToString().Substring(4);
        }


        private static string devName(char letter)
        {
            return new string(char.ToUpper(letter), 1) + ":";
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DefineDosDevice(int flags, string devname, string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int QueryDosDevice(string devname, StringBuilder buffer, int bufSize);
    }
}

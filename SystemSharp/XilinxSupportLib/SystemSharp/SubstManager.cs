/**
 * Copyright 2011-2012 Christian Köllner
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Collections;

namespace SystemSharp.Interop.Xilinx
{
    class SubstManager
    {
        private static readonly char[] _allLetters = 
        { 
            'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' 
        };

        private static readonly object _syncRoot = new object();

        private static SubstManager _instance;
        public static SubstManager Instance
        {
            get
            {
                lock (_syncRoot)
                {
                    if (_instance == null)
                        _instance = new SubstManager();
                    return _instance;
                }
            }
        }

        private char[] _driveSet;
        private HashSet<char> _freeDriveSet = new HashSet<char>();
        private HashSet<char> _allocatedDriveSet = new HashSet<char>();
        private BlockingCollection<char> _freeDriveQ;

        private SubstManager()
        {
            FindAvailableDrives();
            _freeDriveQ = new BlockingCollection<char>(_driveSet.Length);
            foreach (char drive in _driveSet)
                _freeDriveQ.Add(drive);
        }

        public int MaxDrives
        {
            get { return _driveSet.Length; }
        }

        public char AllocateDrive()
        {
            return _freeDriveQ.Take();
        }

        public void ReleaseDrive(char drive)
        {
            _freeDriveQ.Add(drive);
        }

        private void FindAvailableDrives()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            var unavailLetters = drives.Select(d => d.Name.ToLower()[0]);
            _driveSet = _allLetters.Except(unavailLetters).ToArray();
        }
    }
}

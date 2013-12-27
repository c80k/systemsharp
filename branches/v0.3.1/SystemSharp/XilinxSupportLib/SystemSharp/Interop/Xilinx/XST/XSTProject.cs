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
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Synthesis;

namespace SystemSharp.Interop.Xilinx.XST
{
    /// <summary>
    /// Generates projects which are compatible with the Xilinx XST tool.
    /// </summary>
    public class XSTProject: IProject
    {
        private string _path;
        private List<string> _fileList = new List<string>();

        /// <summary>
        /// Constructs a new project.
        /// </summary>
        /// <param name="path">path to project file</param>
        public XSTProject(string path)
        {
            _path = path;
        }

        public string AddFile(string name)
        {
            _fileList.Add(name);
            return Path.Combine(_path, name);
        }

        public void AddFileAttribute(string name, object attr)
        {
        }

        public void Save()
        {
            var sw = new StreamWriter(_path);
            foreach (var file in _fileList)
                sw.WriteLine("vhdl work " + file);
            sw.Close();
        }
    }
}

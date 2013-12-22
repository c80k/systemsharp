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
 * */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SystemSharp.Synthesis.DocGen
{
    /// <summary>
    /// A documentation project gathers a collection of documentation files.
    /// </summary>
    public class DocumentationProject: IProject
    {
        public string RootPath { get; private set; }

        /// <summary>
        /// Constructs a documentation project.
        /// </summary>
        /// <param name="rootPath">path to project root directory</param>
        public DocumentationProject(string rootPath)
        {
            RootPath = rootPath;
        }

        public string AddFile(string name)
        {
            string path = Path.Combine(RootPath, name);
            string dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            return path;
        }

        public void AddFileAttribute(string name, object attr)
        {
        }

        public void Save()
        {
        }
    }
}

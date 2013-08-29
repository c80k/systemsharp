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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Experimental.IO;
using SystemSharp.Components;
using SystemSharp.Meta;

namespace SystemSharp.Synthesis.DocGen
{
    public class DocumentationGenerator:
        ICodeGenerator
    {
        public string GetComponentID(IComponentDescriptor cdesc)
        {
            return cdesc.Name;
        }

        public void Initialize(IProject project, DesignContext context)
        {
        }

        public void GenerateComponent(IProject project, IComponentDescriptor cdesc)
        {
            var docproj = project as DocumentationProject;
            if (docproj == null)
                throw new ArgumentException("Expected a documentation project");
            var docs = ((DescriptorBase)cdesc).GetDocumentation();
            string prefix = cdesc.GetFullName().Replace(".", "/");
            foreach (var doc in docs.Documents)
            {
                string legalName = doc.Name.Replace('<', '_').Replace('>', '_');
                string name = prefix + "/" + legalName;
                string path = docproj.AddFile(name);
                var fs = LongPathFile.Open(path, FileMode.Create, FileAccess.Write);
                var wr = new StreamWriter(fs);
                wr.WriteLine(doc.Content.ToString());
                wr.Close();
            }
        }

        public void GeneratePackage(IProject project, PackageDescriptor pdesc)
        {
        }
    }
}

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
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Interop.Mentor
{
    /// <summary>
    /// Represents a Modelsim or Questa (Mentor Graphics) project output.
    /// </summary>
    /// <remarks>
    /// This class will not create a Modelsim/Questa project file. Instead, it will create a .do TCL script which is supposed 
    /// to be executed inside the simulation environment. The script will create a new project and add all necessary sources.
    /// Modelsim, Questa and Mentor Graphics are registered trademarks.
    /// </remarks>
    public class ModelsimProject: IProject
    {
        private string _path;
        private string _name;
        private List<string> _fileList = new List<string>();
        private Dictionary<string, int> _dependencyOrder = new Dictionary<string, int>();
        private Dictionary<string, string> _libraryName = new Dictionary<string, string>();

        private static string MakeUNIXPath(string file)
        {
            return file.Replace('\\', '/');
        }

        /// <summary>
        /// Constructs a new Modelsim/Questa target project.
        /// </summary>
        /// <param name="path">The output path of the project.</param>
        /// <param name="name">The name of the project.</param>
        public ModelsimProject(string path, string name)
        {
            _path = path;
            _name = name;
#if false
            Version = "6";
            DefaultLib = "work";
            SortMethod = "unused";
#endif
        }

        /// <summary>
        /// Returns the output path of the project.
        /// </summary>
        public string ProjectPath
        {
            get { return _path; }
        }

        /// <summary>
        /// Converts a file name to a full output path.
        /// </summary>
        /// <param name="file">a file name</param>
        /// <returns>its output path</returns>
        public string MakeFullPath(string file)
        {
            return Path.GetFullPath(ProjectPath + "\\" + file);
        }

        /// <summary>
        /// Adds a file to the project
        /// </summary>
        /// <param name="name">The file name (including proper extension)</param>
        /// <returns>its output path</returns>
        public string AddFile(string name)
        {
            if (Path.GetExtension(name) == ".vhd" && !_fileList.Contains(name))
                _fileList.Add(name);
            return MakeFullPath(name);
        }

        /// <summary>
        /// Attaches an arbitrary attribute to a project file.
        /// </summary>
        /// <remarks>Currently, the method is interested in attributes of type IPackageOrComponentDescriptor (in order to determine the compilation order) and
        /// in attributes of type LibraryName (in order to determine the library namespace of project files). All other attributes are ignored.</remarks>
        /// <param name="name">The file name (including proper extension)</param>
        /// <param name="attr">An object which represents the attribute</param>
        public void AddFileAttribute(string name, object attr)
        {
            var pcd = attr as IPackageOrComponentDescriptor;
            if (pcd != null)
            {
                _dependencyOrder[name] = pcd.DependencyOrder;
            }
            var lib = attr as LibraryAttribute;
            if (lib != null)
            {
                _libraryName[name] = lib.Name;
            }
        }

#if false
        public string Version { get; set; }
        public string DefaultLib { get; set; }
        public string SortMethod { get; set; }

        public void Save()
        {
            StreamWriter sw = new StreamWriter(_path + "\\" + _name + ".mpf");
            sw.BaseStream.Write(
                MentorSupportLib.Properties.Resources.ModelsimProjectHeader,
                0,
                MentorSupportLib.Properties.Resources.ModelsimProjectHeader.Length);

            sw.WriteLine("Project_Version = {0}", Version);
            sw.WriteLine("Project_DefaultLib = {0}", DefaultLib);
            sw.WriteLine("Project_SortMethod = {0}", SortMethod);
            sw.WriteLine("Project_Files_Count = {0}", _fileList.Count);
            
            for (int i = 0; i < _fileList.Count; i++)
            {
                sw.WriteLine("Project_File_{0} = {1}", i, MakeUNIXPath(MakeFullPath(_fileList[i])));
                int order = 0;
                _dependencyOrder.TryGetValue(_fileList[i], out order);
                sw.WriteLine(
                    "Project_File_P_{0} = vhdl_novitalcheck 0 file_type vhdl group_id 0 cover_nofec 0 vhdl_nodebug 0 vhdl_1164 1 vhdl_noload 0 vhdl_synth 0 vhdl_enable0In 0 folder {{Top Level}} last_compile 0 vhdl_disableopt 0 vhdl_vital 0 cover_excludedefault 0 vhdl_warn1 1 vhdl_warn2 1 vhdl_explicit 1 vhdl_showsource 0 vhdl_warn3 1 cover_covercells 0 vhdl_0InOptions {{}} vhdl_warn4 1 voptflow 1 cover_optlevel 3 vhdl_options {{}} vhdl_warn5 1 toggle - ood 1 cover_noshort 0 compile_to work compile_order {1} cover_nosub 0 dont_compile 0 vhdl_use93 2002",
                    i, order);
            }
            sw.Flush();

            sw.BaseStream.Write(
                MentorSupportLib.Properties.Resources.ModelsimProjectTrailer,
                0,
                MentorSupportLib.Properties.Resources.ModelsimProjectTrailer.Length);
            sw.Close();
        }
#endif

        private int GetOrder(string file)
        {
            int order = 0;
            _dependencyOrder.TryGetValue(file, out order);
            return order;
        }

        private string GetLibrary(string file)
        {
            string lib;
            if (!_libraryName.TryGetValue(file, out lib))
                lib = "work";
            return lib;
        }

        /// <summary>
        /// Creates a .do TCL script which will setup a ModelSim/Questa project upon execution inside the simulator.
        /// </summary>
        public void Save()
        {
            var sw = new StreamWriter(_path + "\\" + _name + ".do");
            var groups = _fileList.GroupBy(f => GetLibrary(f));
            sw.WriteLine("catch { project close }");
            sw.WriteLine("project new . {0}", _name);
            foreach (var grp in groups)
            {
                //sw.WriteLine("project new . {0} {0}", grp.Key);
                var files = grp.OrderBy(f => GetOrder(f));
                foreach (string file in files)
                {
                    sw.WriteLine("project addfile {0}", file);
                    //sw.WriteLine("vcom -work {0} {1}", grp.Key, file);
                }
                //sw.WriteLine("vlib {0}", grp.Key);
                //sw.WriteLine("vmap {0} ../{0}", grp.Key);
                //sw.WriteLine("project close");
            }
            foreach (var grp in groups)
            {
                //sw.WriteLine("project new . {0} {0}", grp.Key);
                var files = grp.OrderBy(f => GetOrder(f));
                foreach (string file in files)
                {
                    //sw.WriteLine("project addfile {0}", file);
                    sw.WriteLine("vcom -work {0} {1}", grp.Key, file);
                }
                //sw.WriteLine("vlib {0}", grp.Key);
                //sw.WriteLine("vmap {0} ../{0}", grp.Key);
                //sw.WriteLine("project close");
            }
            sw.Close();
        }
    }
}

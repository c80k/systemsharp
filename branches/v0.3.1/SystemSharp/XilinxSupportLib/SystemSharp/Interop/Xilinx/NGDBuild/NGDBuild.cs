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
using System.Linq;
using System.Text;

namespace SystemSharp.Interop.Xilinx.NGDBuild
{
    public enum ENGOGeneration
    {
        Timestamp,
        On,
        Off
    }

    /// <summary>
    /// Provides access to the Xilinx "ngdbuild" tool.
    /// </summary>
    public class NGDBuildFlow
    {
        public string PartName { get; set; }
        public List<string> SearchDirs { get; private set; }
        public List<string> Libraries { get; private set; }
        public string RulesFile { get; set; }
        public string IntermediateDir { get; set; }
        public ENGOGeneration NGOGeneration { get; set; }
        public string UserConstraintsFile { get; set; }
        public bool IgnoreLocationConstraints { get; set; }
        public bool AllowUnmatchedLOCConstraints { get; set; }
        public bool AllowUnmatchedTimingGroupConstraints { get; set; }
        public bool InferPadComponents { get; set; }
        public bool IgnoreDefaultUCF { get; set; }
        public bool AllowUnexpandedBlocks { get; set; }
        public bool InsertKeepHierarchy { get; set; }
        public string BMMFile { get; set; }
        public string FilterFile { get; set; }
        public bool Quiet { get; set; }
        public bool Verbose { get; set; }
        public string DesignName { get; set; }
        public string NGDFile { get; set; }

        public NGDBuildFlow()
        {
            SearchDirs = new List<string>();
            Libraries = new List<string>();
        }

        public ProcessPool.Tool AddToBatch(XilinxProject proj, ProcessPool.ToolBatch batch)
        {
            var cmd = new StringBuilder();
            if (PartName != null)
                cmd.Append(" -p " + PartName);
            foreach (var dir in SearchDirs)
                cmd.Append(" -sd " + dir);
            foreach (var lib in Libraries)
                cmd.Append(" -l " + lib);
            if (RulesFile != null)
                cmd.Append(" -ur " + RulesFile);
            if (IntermediateDir != null)
                cmd.Append(" -dd " + IntermediateDir);
            cmd.Append(" -nt ");
            switch (NGOGeneration)
            { 
                case ENGOGeneration.Off:
                    cmd.Append(" off");
                    break;
                case ENGOGeneration.On:
                    cmd.Append(" on");
                    break;
                case ENGOGeneration.Timestamp:
                    cmd.Append(" timestamp");
                    break;
            }
            if (UserConstraintsFile != null)
                cmd.Append(" -uc " + UserConstraintsFile);
            if (IgnoreLocationConstraints)
                cmd.Append(" -r");
            if (AllowUnmatchedLOCConstraints)
                cmd.Append(" -aul");
            if (AllowUnmatchedTimingGroupConstraints)
                cmd.Append(" -aut");
            if (InferPadComponents)
                cmd.Append(" -a");
            if (IgnoreDefaultUCF)
                cmd.Append(" -i");
            if (AllowUnexpandedBlocks)
                cmd.Append(" -u");
            if (InsertKeepHierarchy)
                cmd.Append(" -insert_keep_hierarchy");
            if (BMMFile != null)
                cmd.Append(" -bm " + BMMFile);
            if (FilterFile != null)
                cmd.Append(" -filter " + FilterFile);
            cmd.Append(" -intstyle silent");
            if (Quiet)
                cmd.Append(" -quiet");
            if (Verbose)
                cmd.Append(" -verbose");
            cmd.Append(" " + DesignName);
            if (NGDFile != null)
                cmd.Append(" " + NGDFile);

            return proj.AddToolToBatch(batch, proj.ProjectPath, "ngdbuild", cmd.ToString());
        }
    }
}

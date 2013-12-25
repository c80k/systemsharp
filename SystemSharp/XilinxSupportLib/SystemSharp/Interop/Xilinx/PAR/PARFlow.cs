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

namespace SystemSharp.Interop.Xilinx.PAR
{
    public enum EOverallEffortLevel
    {
        [PropID(EPropAssoc.PAR, "high")]
        High,

        [PropID(EPropAssoc.PAR, "std")]
        Standard
    }

    public enum EExtraEffortLevel
    {
        None,

        [PropID(EPropAssoc.PAR, "n")]
        Normal,

        [PropID(EPropAssoc.PAR, "c")]
        ContinueOnImpossible
    }

    public enum EPowerReductionEffort
    {
        [PropID(EPropAssoc.PAR, "off")]
        Off,

        [PropID(EPropAssoc.PAR, "on")]
        On,

        [PropID(EPropAssoc.PAR, "xe")]
        ExtraEffort
    }

    /// <summary>
    /// Provides access to the Xilinx "par" tool.
    /// </summary>
    public class PARFlow
    {
        public EOverallEffortLevel OverallEffortLevel { get; set; }
        public EOverallEffortLevel PlacerEffortLevel { get; set; }
        public EOverallEffortLevel RouterEffortLevel { get; set; }
        public EExtraEffortLevel ExtraEffortLevel { get; set; }
        public int MultiThreading { get; set; }
        public int PlacerCostTableEntry { get; set; }
        public bool KeepCurrentPlacement { get; set; }
        public bool ReentrantRoute { get; set; }
        public bool DontRunRouter { get; set; }
        public bool Overwrite { get; set; }
        public string FilterFile { get; set; }
        public string SmartGuideFile { get; set; }
        public bool IgnoreUserTimingConstraintsAutoGen { get; set; }
        public bool IgnoreUserTimingConstraintsNoGen { get; set; }
        public bool NoPadReport { get; set; }
        public EPowerReductionEffort PowerReduction { get; set; }
        public string PowerActivityFile { get; set; }
        public string ISERepositoryFile { get; set; }
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public string PhysicalConstraintsFile { get; set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public PARFlow()
        {
            PlacerCostTableEntry = 1;
            ReentrantRoute = false;
            OverallEffortLevel = EOverallEffortLevel.High;
            PlacerEffortLevel = EOverallEffortLevel.High;
            RouterEffortLevel = EOverallEffortLevel.High;
            ExtraEffortLevel = EExtraEffortLevel.None;
            IgnoreUserTimingConstraintsAutoGen = false;
            IgnoreUserTimingConstraintsNoGen = false;
            DontRunRouter = false;
            Overwrite = true;
            NoPadReport = false;
            PowerReduction = EPowerReductionEffort.Off;
            MultiThreading = 1;
        }

        public ProcessPool.Tool AddToBatch(XilinxProject proj, ProcessPool.ToolBatch batch)
        {
            var cmd = new StringBuilder();
            cmd.Append("-ol \"" + PropEnum.ToString(OverallEffortLevel, EPropAssoc.PAR) + "\"");
#if false
            //FIXME: Supporting device families?
            cmd.Append(" -pl \"" + PropEnum.ToString(PlacerEffortLevel, EPropAssoc.PAR) + "\"");
            cmd.Append(" -rl \"" + PropEnum.ToString(RouterEffortLevel, EPropAssoc.PAR) + "\"");
#endif
            if (ExtraEffortLevel != EExtraEffortLevel.None)
                cmd.Append(" -xl \"" + PropEnum.ToString(ExtraEffortLevel, EPropAssoc.PAR) + "\"");
            if (MultiThreading > 1)
                cmd.Append(" -mt \"" + MultiThreading + "\"");
#if false
            //FIXME: Supporting device families?
            cmd.Append(" -t \"" + PlacerCostTableEntry + "\"");
#endif
            if (KeepCurrentPlacement)
                cmd.Append(" -p");
            if (ReentrantRoute)
                cmd.Append(" -k");
            if (DontRunRouter)
                cmd.Append(" -r");
            if (Overwrite)
                cmd.Append(" -w");
            if (SmartGuideFile != null)
                cmd.Append(" -smartguide \"" + SmartGuideFile + "\"");
            if (IgnoreUserTimingConstraintsAutoGen)
                cmd.Append(" -x");
            if (NoPadReport)
                cmd.Append(" -nopad");
            cmd.Append(" -power \"" + PropEnum.ToString(PowerReduction, EPropAssoc.PAR) + "\"");
            if (PowerActivityFile != null)
                cmd.Append(" -activityfile \"" + PowerActivityFile + "\"");
            if (FilterFile != null)
                cmd.Append(" -filter \"" + FilterFile + "\"");
            if (IgnoreUserTimingConstraintsNoGen)
                cmd.Append(" -ntd");
            cmd.Append(" -intstyle silent");
            if (ISERepositoryFile != null)
                cmd.Append(" -ise \"" + ISERepositoryFile + "\"");
            if (FilterFile != null)
                cmd.Append(" -filter \"" + FilterFile + "\"");
            cmd.Append(" \"" + InputFile + "\"");
            cmd.Append(" \"" + OutputFile + "\"");
            if (PhysicalConstraintsFile != null)
                cmd.Append(" \"" + PhysicalConstraintsFile + "\"");
            return batch.Add(proj.ISEBinPath, proj.ProjectPath, "par", cmd.ToString());
        }
    }
}

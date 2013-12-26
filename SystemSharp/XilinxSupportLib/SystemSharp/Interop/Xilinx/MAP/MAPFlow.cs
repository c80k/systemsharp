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

namespace SystemSharp.Interop.Xilinx.MAP
{
    public enum EOptimizationStrategyCoverMode
    {
        [PropID(EPropAssoc.MAP, "area")]
        Area,

        [PropID(EPropAssoc.MAP, "speed")]
        Speed,

        [PropID(EPropAssoc.MAP, "balanced")]
        Balanced,

        [PropID(EPropAssoc.MAP, "off")]
        Off
    }

    public enum EPackIORegistersIntoIOBs
    {
        [PropID(EPropAssoc.MAP, "b")]
        InputsAndOutputs,

        InputsOnly,
        OutputsOnly,

        [PropID(EPropAssoc.MAP, "off")]
        Off
    }

    /// <summary>
    /// Provides access to the Xilinx "map" tool.
    /// </summary>
    public class MAPFlow
    {
        public string PartName { get; set; }
        public EPlacerEffortLevelMap PlacerEffort { get; set; }
        public EPlacerExtraEffortMap PlacerExtraEffort { get; set; }
        public int StartingPlacerCostTable { get; set; }
        public bool CombinatorialLogicOptimization { get; set; }
        public ERegisterDuplicationMap RegisterDuplication { get; set; }
        public EGlobalOptimizationMapVirtex5 GlobalOptimization { get; set; }
        public bool EquivalentRegisterRemoval { get; set; }
        public bool IgnoreUserTimingConstraints { get; set; }
        public bool TrimUnconnectedSignals { get; set; }
        public bool IgnoreKeepHierarchy { get; set; }
        public EOptimizationStrategyCoverMode OptimizationStrategyCoverMode { get; set; }
        public bool GenerateDetailedMapReport { get; set; }
        public EUseRLOCConstraints UseRLOCConstraints { get; set; }
        public EPackIORegistersIntoIOBs PackIORegistersIntoIOBs { get; set; }
        public bool MaximumCompression { get; set; }
        public ELUTCombining LUTCombining { get; set; }
        public bool MapSliceLogicIntoUnusedBlockRAMs { get; set; }
        public bool PowerReduction { get; set; }
        public string PowerActivityFile { get; set; }
        public int MultiThreading { get; set; }
        public bool Overwrite { get; set; }
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public string PRFFile { get; set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public MAPFlow()
        {
            PlacerEffort = EPlacerEffortLevelMap.High;
            PlacerExtraEffort = EPlacerExtraEffortMap.None;
            StartingPlacerCostTable = 1;
            CombinatorialLogicOptimization = false;
            RegisterDuplication = ERegisterDuplicationMap.Off;
            GlobalOptimization = EGlobalOptimizationMapVirtex5.Off;
            EquivalentRegisterRemoval = true;
            IgnoreUserTimingConstraints = false;
            TrimUnconnectedSignals = true;
            IgnoreKeepHierarchy = false;
            OptimizationStrategyCoverMode = EOptimizationStrategyCoverMode.Off;
            GenerateDetailedMapReport = false;
            UseRLOCConstraints = EUseRLOCConstraints.Yes;
            PackIORegistersIntoIOBs = EPackIORegistersIntoIOBs.Off;
            MaximumCompression = false;
            LUTCombining = ELUTCombining.Off;
            MapSliceLogicIntoUnusedBlockRAMs = false;
            PowerReduction = false;
            MultiThreading = 1;
            Overwrite = true;
        }

        public ProcessPool.Tool AddToBatch(XilinxProject proj, ProcessPool.ToolBatch batch)
        {
            var cmd = new StringBuilder();
            if (PartName != null)
                cmd.Append("-p " + PartName);
            cmd.Append(" -ol \"" + PropEnum.ToString(PlacerEffort, EPropAssoc.MAP) + "\"");
            if (PlacerExtraEffort != EPlacerExtraEffortMap.None)
                throw new NotImplementedException();
            cmd.Append(" -t " + StartingPlacerCostTable);
            cmd.Append(" -logic_opt ");
            if (CombinatorialLogicOptimization)
                cmd.Append("\"on\"");
            else
                cmd.Append("\"off\"");
            cmd.Append(" -register_duplication ");
            cmd.Append("\"" + PropEnum.ToString(RegisterDuplication, EPropAssoc.MAP) + "\"");
            cmd.Append(" -global_opt \"" + PropEnum.ToString(GlobalOptimization, EPropAssoc.MAP) + "\"");
            cmd.Append(" -equivalent_register_removal ");
            if (EquivalentRegisterRemoval)
                cmd.Append("\"on\"");
            else
                cmd.Append("\"off\"");
            if (IgnoreUserTimingConstraints)
                cmd.Append(" -x");
            if (TrimUnconnectedSignals)
                cmd.Append(" -u");
            if (IgnoreKeepHierarchy)
                cmd.Append(" -ignore_keep_hierarchy");
#if false
            //FIXME: Which architectures allow for this property?
            cmd.Append(" -cm \"" + PropEnum.ToString(OptimizationStrategyCoverMode, EPropAssoc.MAP) + "\"");
#endif
            if (GenerateDetailedMapReport)
                cmd.Append(" -detail");
            cmd.Append(" -ir \"" + PropEnum.ToString(UseRLOCConstraints, EPropAssoc.MAP) + "\"");
            cmd.Append(" -pr \"" + PropEnum.ToString(PackIORegistersIntoIOBs, EPropAssoc.MAP) + "\"");
            if (MaximumCompression)
                cmd.Append(" -c");
            cmd.Append(" -lc \"" + PropEnum.ToString(LUTCombining, EPropAssoc.MAP) + "\"");
            if (MapSliceLogicIntoUnusedBlockRAMs)
                cmd.Append(" -bp");
            cmd.Append(" -power ");
            if (PowerReduction)
                cmd.Append("\"on\"");
            else
                cmd.Append("\"off\"");
            if (PowerActivityFile != null)
                cmd.Append(" -activityfile \"" + PowerActivityFile + "\"");
            cmd.Append(" -mt \"" + MultiThreading + "\"");
            if (Overwrite)
                cmd.Append(" -w");
            if (OutputFile != null)
                cmd.Append(" -o \"" + OutputFile + "\"");
            cmd.Append(" \"" + InputFile + "\"");
            if (PRFFile != null)
                cmd.Append(" " + PRFFile);

            return proj.AddToolToBatch(batch, proj.ProjectPath, "map", cmd.ToString());
        }
    }
}

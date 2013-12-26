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

namespace SystemSharp.Interop.Xilinx.XST
{
    public enum EInputFormat
    {
        [PropID(EPropAssoc.XST, "mixed")]
        Mixed
    }

    public enum EOutputFormat
    {
        NGC
    }

    public enum EOptimizationMode
    {
        Speed,
        Area
    }

    public enum EOptimizationEffort
    {
        [PropID(EPropAssoc.XST, "1")]
        Normal,

        [PropID(EPropAssoc.XST, "2")]
        High
    }

    public enum EYesNo
    {
        [PropID(EPropAssoc.XST, "YES")]
        Yes,

        [PropID(EPropAssoc.XST, "NO")]
        No
    }

    public enum EKeepHierarchy
    {
        Yes,
        No,
        Soft
    }

    public enum ENetlistHierarchy
    {
        [PropID(EPropAssoc.XST, "As_optimized")]
        AsOptimized,
        Rebuilt
    }

    public enum EGenerateRTLSchematic
    {
        Yes,
        No,
        Only
    }

    public enum EGlobalOptimizationGoal
    {
        AllClockNets
    }

    public enum ECase
    {
        Maintain,
        Lower,
        Upper
    }

    public enum ELUTCombining
    {
        No,
        Auto,
        Area
    }

    public enum ENoAuto
    {
        No,
        Auto
    }

    public enum EFSMEncoding
    {
        Auto
    }

    public enum EFSMStyle
    {
        LUT,
        Bram
    }

    public enum EYesNoLC
    {
        Yes,
        No
    }

    public enum ERAMStyle
    {
        Auto,
        Distributed,
        Block
    }

    public enum EMUXStyle
    {
        Auto,
        MUXF,
        MUXCY
    }

    public enum EYesNoForce
    {
        Yes,
        No,
        Force
    }

    public enum EYesNoAuto
    {
        Yes,
        No,
        Auto
    }

    public enum ERegisterBalancing
    {
        No,
        Yes,
        Forward,
        Backward
    }

    /// <summary>
    /// Provides access to the Xilinx XST tool.
    /// </summary>
    public class XSTFlow
    {
        [AttributeUsage(AttributeTargets.Property)]
        private class FlowProp : Attribute
        {
            public FlowProp(string id)
            {
                ID = id;
            }

            public string ID { get; private set; }
        }

        public string TempDir { get; set; }
        public string XstHdpDir { get; set; }

        [FlowProp("ifn")]
        public string XSTProjectPath { get; set; }

        [FlowProp("ifmt")]
        public EInputFormat InputFormat { get; set; }

        [FlowProp("ofn")]
        public string OutputFile { get; set; }

        [FlowProp("ofmt")]
        public EOutputFormat OutputFormat { get; set; }

        [FlowProp("p")]
        public string PartName { get; set; }

        [FlowProp("top")]
        public string TopLevelUnitName { get; set; }

        [FlowProp("opt_mode")]
        public EOptimizationMode OptimizationMode { get; set; }

        [FlowProp("opt_level")]
        public EOptimizationEffort OptimizationEffort { get; set; }

        [FlowProp("power")]
        public EYesNo PowerReduction { get; set; }

        [FlowProp("iuc")]
        public EYesNo UseSynthesisConstraintsFile { get; set; }

        [FlowProp("uc")]
        public string SynthesisConstraintsFile { get; set; }

        [FlowProp("lso")]
        public string LibrarySearchOrder { get; set; }

        [FlowProp("keep_hierarchy")]
        public EKeepHierarchy KeepHierarchy { get; set; }

        [FlowProp("netlist_hierarchy")]
        public ENetlistHierarchy NetlistHierarchy { get; set; }

        [FlowProp("rtlview")]
        public EGenerateRTLSchematic GenerateRTLSchematic { get; set; }

        [FlowProp("glob_opt")]
        public EGlobalOptimizationGoal GlobalOptimizationGoal { get; set; }

        [FlowProp("read_cores")]
        public EYesNo ReadCores { get; set; }

        [FlowProp("sd")]
        public string CoresSearchDirectories { get; set; }

        [FlowProp("write_timing_constraints")]
        public EYesNo WriteTimingConstraints { get; set; }

        [FlowProp("cross_clock_analysis")]
        public EYesNo CrossClockAnalysis { get; set; }

        [FlowProp("hierarchy_separator")]
        public char HierarchySeparator { get; set; }

        [FlowProp("bus_delimiter")]
        public string BusDelimiter { get; set; }

        [FlowProp("case")]
        public ECase Case { get; set; }

        [FlowProp("slice_utilization_ratio")]
        public int SliceUtilizationRatio { get; set; }

        [FlowProp("bram_utilization_ratio")]
        public int BRAMUtilizationRatio { get; set; }

        [FlowProp("dsp_utilization_ratio")]
        public int DSPUtilizationRatio { get; set; }

        [FlowProp("lc")]
        public ELUTCombining LUTCombining { get; set; }

        [FlowProp("reduce_control_sets")]
        public ENoAuto ReduceControlSets { get; set; }

        //[FlowProp("verilog2001")]
        //public EYesNo Verilog2001 { get; set; }

        [FlowProp("fsm_extract")]
        public EYesNo FSMExtract { get; set; }

        [FlowProp("fsm_encoding")]
        public EFSMEncoding FSMEncoding { get; set; }

        [FlowProp("safe_implementation")]
        public EYesNo SafeImplementation { get; set; }

        [FlowProp("fsm_style")]
        public EFSMStyle FSMStyle { get; set; }

        [FlowProp("ram_extract")]
        public EYesNoLC RAMExtract { get; set; }

        [FlowProp("ram_style")]
        public ERAMStyle RAMStyle { get; set; }

        [FlowProp("rom_extract")]
        public EYesNoLC ROMExtract { get; set; }

        //[FlowProp("mux_style")]
        //public EMUXStyle MUXStyle { get; set; }

        //[FlowProp("decoder_extract")]
        //public EYesNo DecoderExtraction { get; set; }

        //[FlowProp("priority_extract")]
        //public EYesNoForce PriorityEncoderExtraction { get; set; }

        [FlowProp("shreg_extract")]
        public EYesNo ShiftRegisterExtraction { get; set; }

        //[FlowProp("shift_extract")]
        //public EYesNo ShifterExtraction { get; set; }

        //[FlowProp("xor_collapse")]
        //public EYesNo XORCollapsing { get; set; }

        [FlowProp("rom_style")]
        public ERAMStyle ROMStyle { get; set; }

        [FlowProp("auto_bram_packing")]
        public EYesNo AutomaticBRAMPacking { get; set; }

        //[FlowProp("mux_extract")]
        //public EYesNoForce MUXExtraction { get; set; }

        [FlowProp("resource_sharing")]
        public EYesNo ResourceSharing { get; set; }

        [FlowProp("async_to_sync")]
        public EYesNo AsyncToSync { get; set; }

        [FlowProp("use_dsp48")]
        public EYesNoAuto UseDSPBlock { get; set; }

        [FlowProp("iobuf")]
        public EYesNo AddIOBuffers { get; set; }

        [FlowProp("max_fanout")]
        public int MaxFanout { get; set; }

        [FlowProp("bufg")]
        public int NumberOfClockBuffers { get; set; }

        [FlowProp("register_duplication")]
        public EYesNo RegisterDuplication { get; set; }

        [FlowProp("register_balancing")]
        public ERegisterBalancing RegisterBalancing { get; set; }

        //[FlowProp("slice_packing")]
        //public EYesNo SlicePacking { get; set; }

        [FlowProp("optimize_primitives")]
        public EYesNo OptimizePrimitives { get; set; }

        [FlowProp("use_clock_enable")]
        public EYesNoAuto UseClockEnable { get; set; }

        [FlowProp("use_sync_set")]
        public EYesNoAuto UseSyncSet { get; set; }

        [FlowProp("use_sync_reset")]
        public EYesNoAuto UseSyncReset { get; set; }

        [FlowProp("iob")]
        public EYesNoAuto PackIORegistersIntoIOBs { get; set; }

        [FlowProp("equivalent_register_removal")]
        public EYesNo EquivalentRegisterRemoval { get; set; }

        [FlowProp("slice_utilization_ratio_maxmargin")]
        public int SliceUtilizationRatioMaxMargin { get; set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public XSTFlow()
        {
            TempDir = "xst/projnav.tmp";
            XstHdpDir = "xst";

            InputFormat = EInputFormat.Mixed;
            OutputFormat = EOutputFormat.NGC;
            OptimizationMode = EOptimizationMode.Speed;
            OptimizationEffort = EOptimizationEffort.Normal;
            PowerReduction = EYesNo.No;
            UseSynthesisConstraintsFile = EYesNo.No;
            KeepHierarchy = EKeepHierarchy.No;
            NetlistHierarchy = ENetlistHierarchy.AsOptimized;
            GenerateRTLSchematic = EGenerateRTLSchematic.Yes;
            GlobalOptimizationGoal = EGlobalOptimizationGoal.AllClockNets;
            ReadCores = EYesNo.Yes;
            WriteTimingConstraints = EYesNo.No;
            CrossClockAnalysis = EYesNo.No;
            HierarchySeparator = '/';
            BusDelimiter = "<>";
            Case = ECase.Maintain;
            SliceUtilizationRatio = 100;
            BRAMUtilizationRatio = 100;
            DSPUtilizationRatio = 100;
            LUTCombining = ELUTCombining.Auto;
            ReduceControlSets = ENoAuto.Auto;
            //Verilog2001 = EYesNo.Yes;
            FSMExtract = EYesNo.Yes;
            FSMEncoding = EFSMEncoding.Auto;
            SafeImplementation = EYesNo.No;
            FSMStyle = EFSMStyle.LUT;
            RAMExtract = EYesNoLC.Yes;
            RAMStyle = ERAMStyle.Auto;
            ROMExtract = EYesNoLC.Yes;
            //MUXStyle = EMUXStyle.Auto;
            //DecoderExtraction = EYesNo.Yes;
            //PriorityEncoderExtraction = EYesNoForce.Yes;
            ShiftRegisterExtraction = EYesNo.Yes;
            //ShifterExtraction = EYesNo.Yes;
            //XORCollapsing = EYesNo.Yes;
            ROMStyle = ERAMStyle.Auto;
            AutomaticBRAMPacking = EYesNo.No;
            //MUXExtraction = EYesNoForce.Yes;
            ResourceSharing = EYesNo.Yes;
            AsyncToSync = EYesNo.No;
            UseDSPBlock = EYesNoAuto.Auto;
            AddIOBuffers = EYesNo.Yes;
            MaxFanout = 100000;
            NumberOfClockBuffers = 32;
            RegisterDuplication = EYesNo.Yes;
            RegisterBalancing = ERegisterBalancing.No;
            //SlicePacking = EYesNo.Yes;
            OptimizePrimitives = EYesNo.No;
            UseClockEnable = EYesNoAuto.Auto;
            UseSyncSet = EYesNoAuto.Auto;
            UseSyncReset = EYesNoAuto.Auto;
            PackIORegistersIntoIOBs = EYesNoAuto.Auto;
            EquivalentRegisterRemoval = EYesNo.Yes;
            SliceUtilizationRatioMaxMargin = 5;
        }

        public void SaveToXSTScript(string path)
        {
            var sw = new StreamWriter(path);
            if (TempDir != null)
                sw.WriteLine("set -tmpdir \"" + TempDir + "\"");
            if (XstHdpDir != null)
                sw.WriteLine("set -xsthdpdir \"" + XstHdpDir + "\"");
            sw.WriteLine("run");
            foreach (var prop in typeof(XSTFlow).GetProperties())
            {
                if (!Attribute.IsDefined(prop, typeof(FlowProp)))
                    continue;
                object value = prop.GetValue(this, new object[0]);
                if (value == null)
                    continue;
                var flowProp = (FlowProp)Attribute.GetCustomAttribute(prop, typeof(FlowProp));
                sw.Write("-" + flowProp.ID);
                sw.Write(" ");
                if (value.GetType().IsEnum)
                {
                    sw.WriteLine(PropEnum.ToString(value, EPropAssoc.XST));
                }
                else
                {
                    sw.WriteLine(value.ToString());
                }
            }
            sw.WriteLine();
            sw.Close();
        }

        public static ProcessPool.Tool AddToBatch(XilinxProject proj, ProcessPool.ToolBatch batch, string scriptPath, string logPath)
        {
            string args = "-intstyle \"silent\" -ifn \"" + scriptPath + "\" -ofn \"" + logPath + "\"";
            return proj.AddToolToBatch(batch, proj.ProjectPath, "xst", args);
        }

        public ProcessPool.Tool SaveToXSTScriptAndAddToBatch(XilinxProject proj, ProcessPool.ToolBatch batch, string scriptPath, string logPath)
        {
            SaveToXSTScript(scriptPath);
            return AddToBatch(proj, batch, scriptPath, logPath);
        }
    }
}

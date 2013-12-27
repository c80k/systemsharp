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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.Assembler;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx.CoreGen;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Interop.Xilinx.IPCores
{
    class MemReadRewriter : RewriteCall
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            var side = args[0].Sample as IXilinxBlockMemSide;
            if (side == null)
                return false;

            var sample = StdLogicVector._0s(side.DataReadWidth);
            var code = IntrinsicFunctions.XILOpCode(
                DefaultInstructionSet.Instance.RdMem(side),
                TypeDescriptor.GetTypeOf(sample),
                args[1].Expr);
            stack.Push(code, sample);
            return true;
        }
    }

    class MemWriteRewriter : RewriteCall
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            var side = args[0].Sample as IXilinxBlockMemSide;
            if (side == null)
                return false;

            var code = IntrinsicFunctions.XILOpCode(
                DefaultInstructionSet.Instance.WrMem(side),
                typeof(void));
            builder.Call(code.Callee, args[1].Expr, args[2].Expr);
            return true;
        }
    }

    /// <summary>
    /// Transaction site interface for Xilinx block memory.
    /// </summary>
    public interface IXilinxBlockMemSide: 
        ITransactionSite,
        IRAM
    {
        /// <summary>
        /// Reads from the memory.
        /// </summary>
        /// <param name="addr">address to read from</param>
        /// <param name="data">signal sink to receive read data</param>
        /// <returns>the read transaction</returns>
        [StaticEvaluation]
        [AssumeCalled]
        IEnumerable<TAVerb> Read(
            ISignalSource<StdLogicVector> addr,
            ISignalSink<StdLogicVector> data);

        /// <summary>
        /// Writes to the memory.
        /// </summary>
        /// <param name="addr">address to write to</param>
        /// <param name="data">data to write</param>
        /// <returns>the write transaction</returns>
        [StaticEvaluation]
        [AssumeCalled]
        IEnumerable<TAVerb> Write(
            ISignalSource<StdLogicVector> addr,
            ISignalSource<StdLogicVector> data);

        /// <summary>
        /// Reads from the memory.
        /// </summary>
        /// <param name="addr">address to read from</param>
        /// <returns>read task</returns>
        [MemReadRewriter]
        Task<StdLogicVector> Read(StdLogicVector addr);


        /// <summary>
        /// Writes to the memory.
        /// </summary>
        /// <param name="addr">address to write to</param>
        /// <param name="data">data to write</param>
        [MemWriteRewriter]
        void Write(StdLogicVector addr, StdLogicVector data);

        /// <summary>
        /// Returns a address width for read operations.
        /// </summary>
        int AddrReadWidth { get; }

        /// <summary>
        /// Returns the address width for write operations.
        /// </summary>
        int AddrWriteWidth { get; }

        /// <summary>
        /// Returns the data width for read operations.
        /// </summary>
        int DataReadWidth { get; }

        /// <summary>
        /// Returns the data width for write operations.
        /// </summary>
        int DataWriteWidth { get; }

        /// <summary>
        /// Returns the memory depth for read operations.
        /// </summary>
        int DataReadDepth { get; }

        /// <summary>
        /// Returns the memory depth for write operations.
        /// </summary>
        int DataWriteDepth { get; }
    }

    /// <summary>
    /// Models a Xilinx block memory.
    /// </summary>
    public class BlockMem: FunctionalUnit
    {
        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "Block_Memory_Generator family Xilinx,_Inc. 4.3")]
            Block_Memory_Generator_4_3,

            [PropID(EPropAssoc.CoreGen, "Block_Memory_Generator family Xilinx,_Inc. 3.2")]
            Block_Memory_Generator_3_2
        }

        public enum EIPAlgorithm
        {
            [PropID(EPropAssoc.CoreGen, "Minimum_Area")]
            MinimumArea,

            [PropID(EPropAssoc.CoreGen, "Low_Power")]
            LowPower,

            [PropID(EPropAssoc.CoreGen, "Fixed_Primitives")]
            FixedPrimitives
        }

        public enum ECollisionWarnings
        {
            [PropID(EPropAssoc.CoreGen, "ALL")]
            All,

            [PropID(EPropAssoc.CoreGen, "NONE")]
            None,

            [PropID(EPropAssoc.CoreGen, "WARNING_ONLY")]
            WarningOnly,

            [PropID(EPropAssoc.CoreGen, "GENERATE_X_ONLY")]
            GenerateXOnly
        }

        public enum EECCType
        {
            [PropID(EPropAssoc.CoreGen, "No_ECC")]
            NoECC
        }

        public enum EENAUsage
        {
            [PropID(EPropAssoc.CoreGen, "Always_Enabled")]            
            AlwaysEnabled,

            [PropID(EPropAssoc.CoreGen, "Use_ENA_Pin")]            
            UseENAPin
        }

        public enum EENBUsage
        {
            [PropID(EPropAssoc.CoreGen, "Always_Enabled")]
            AlwaysEnabled,

            [PropID(EPropAssoc.CoreGen, "Use_ENB_Pin")]
            UseENBPin
        }

        public enum EErrorInjectionType
        {
            [PropID(EPropAssoc.CoreGen, "Single_Bit_Error_Injection")]
            SingleBitErrorInjection
        }

        public enum EMemoryType
        {
            [PropID(EPropAssoc.CoreGen, "Single_Port_RAM")]
            SinglePortRAM,

            [PropID(EPropAssoc.CoreGen, "Simple_Dual_Port_RAM")]
            SimpleDualPortRAM,

            [PropID(EPropAssoc.CoreGen, "True_Dual_Port_RAM")]
            TrueDualPortRAM,

            [PropID(EPropAssoc.CoreGen, "Single_Port_ROM")]
            SinglePortROM,

            [PropID(EPropAssoc.CoreGen, "Dual_Port_ROM")]
            DualPortROM
        }

        public enum EOperatingMode
        {
            [PropID(EPropAssoc.CoreGen, "WRITE_FIRST")]
            WriteFirst,

            [PropID(EPropAssoc.CoreGen, "READ_FIRST")]
            ReadFirst,

            [PropID(EPropAssoc.CoreGen, "NO_CHANGE")]
            NoChange
        }

        public enum EPrimitive
        {
            [PropID(EPropAssoc.CoreGen, "8kx2")]
            _8kx2,

            [PropID(EPropAssoc.CoreGen, "16kx1")]
            _16kx1,

            [PropID(EPropAssoc.CoreGen, "4kx4")]
            _4kx4,

            [PropID(EPropAssoc.CoreGen, "2kx9")]
            _2kx9,

            [PropID(EPropAssoc.CoreGen, "1kx18")]
            _1kx18,

            [PropID(EPropAssoc.CoreGen, "512x36")]
            _512x36
        }

        public enum EResetPriority
        {
            [PropID(EPropAssoc.CoreGen, "CE")]
            CE
        }

        public enum EResetType
        {
            [PropID(EPropAssoc.CoreGen, "SYNC")]
            Sync
        }

        [Flags]
        public enum ETAGroup
        {
            SideA,
            SideB
        }

        public enum ETAPorts
        {
            Address,
            DataIn,
            DataOut,
            Enable,
            Write,
            Clock,
            UseEnable
        }

        [TAPort(ETARole.Exchange, ETAGroup.SideA, ETAPorts.Address)]
        public In<StdLogicVector> AddrA { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideA, ETAPorts.DataIn)]
        public In<StdLogicVector> DinA { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideA, ETAPorts.Enable)]
        public In<StdLogic> EnA { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideA, ETAPorts.Write)]
        public In<StdLogicVector> WeA { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideA, ETAPorts.DataOut)]
        public Out<StdLogicVector> DoutA { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Clock, ETAGroup.SideA, ETAPorts.Clock)]
        public In<StdLogic> ClkA { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideB, ETAPorts.Address)]
        public In<StdLogicVector> AddrB { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideB, ETAPorts.DataIn)]
        public In<StdLogicVector> DinB { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideB, ETAPorts.Enable)]
        public In<StdLogic> EnB { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideB, ETAPorts.Write)]
        public In<StdLogicVector> WeB { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Clock, ETAGroup.SideB, ETAPorts.Clock)]
        public In<StdLogic> ClkB { [StaticEvaluation] private get; set; }

        [TAPort(ETARole.Exchange, ETAGroup.SideB, ETAPorts.DataOut)]
        public Out<StdLogicVector> DoutB { [StaticEvaluation] private get; set; }

        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "additional_inputs_for_power_estimation")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        bool AdditionalInputsForPowerEstimation { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "algorithm")]
        [PerformanceRelevant]
        public EIPAlgorithm IPAlgorithm { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "assume_synchronous_clk")]
        [PerformanceRelevant]
        public bool AssumeSynchronousClk { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "byte_size")]
        [PerformanceRelevant]
        public int ByteSize { [StaticEvaluation] get; set; }

        public StdLogicVector[] InitialImage { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "coe_file")]
        string COEFile { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "collision_warnings")]
        public ECollisionWarnings CollisionWarnings { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        string ComponentName { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "disable_collision_warnings")]
        public bool DisableCollisionWarnings { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "disable_out_of_range_warnings")]
        public bool DisableOutOfRangeWarnings { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "ecc")]
        [PerformanceRelevant]
        public bool ECC { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "ecctype")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        [PerformanceRelevant]
        public EECCType ECCType { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "enable_a")]
        [PerformanceRelevant]
        public EENAUsage EnableA { [StaticEvaluation] get; set; }
        
        [TAPort(ETARole.Parameter, ETAGroup.SideA, ETAPorts.UseEnable)]
        public bool UseEnableA
        {
            [StaticEvaluation]
            get { return EnableA == EENAUsage.UseENAPin; }
            set { EnableA = value ? EENAUsage.UseENAPin : EENAUsage.AlwaysEnabled; }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "enable_b")]
        [PerformanceRelevant]
        public EENBUsage EnableB { [StaticEvaluation] get; set; }

        [TAPort(ETARole.Parameter, ETAGroup.SideB, ETAPorts.UseEnable)]
        public bool UseEnableB
        {
            [StaticEvaluation]
            get { return EnableB == EENBUsage.UseENBPin; }
            set { EnableB = value ? EENBUsage.UseENBPin : EENBUsage.AlwaysEnabled; }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "error_injection_type")]
        public EErrorInjectionType ErrorInjectionType { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "fill_remaining_memory_locations")]
        public bool FillRemainingMemoryLocations { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "load_init_file")]
        public bool LoadInitFile { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "memory_type")]
        [PerformanceRelevant]
        public EMemoryType MemoryType { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "operating_mode_a")]
        [PerformanceRelevant]
        public EOperatingMode OperatingModeA { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "operating_mode_b")]
        [PerformanceRelevant]
        public EOperatingMode OperatingModeB { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_reset_value_a")]
        public int OutputResetValueA { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_reset_value_b")]
        public int OutputResetValueB { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pipeline_stages")]
        [PerformanceRelevant]
        public int PipelineStages { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "port_a_clock")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        public int PortAClock { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "port_a_enable_rate")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        public int PortAEnableRate { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "port_a_write_rate")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        public int PortAWriteRate { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "port_b_clock")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        public int PortBClock { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "port_b_enable_rate")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        public int PortBEnableRate { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "port_b_write_rate")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        public int PortBWriteRate { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "primitive")]
        [PerformanceRelevant]
        public EPrimitive Primitive { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "read_width_a")]
        [PerformanceRelevant]
        public int ReadWidthA { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "read_width_b")]
        [PerformanceRelevant]
        public int ReadWidthB { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_porta_input_of_softecc")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        [PerformanceRelevant]
        public bool RegisterPortAInputOfSoftECC { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_porta_output_of_memory_core")]
        [PerformanceRelevant]
        public bool RegisterPortAOutputOfMemoryCore { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_porta_output_of_memory_primitives")]
        [PerformanceRelevant]
        public bool RegisterPortAOutputOfMemoryPrimitives { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_portb_output_of_memory_core")]
        [PerformanceRelevant]
        public bool RegisterPortBOutputOfMemoryCore { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_portb_output_of_memory_primitives")]
        [PerformanceRelevant]
        public bool RegisterPortBOutputOfMemoryPrimitives { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_portb_output_of_softecc")]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        [PerformanceRelevant]
        public bool RegisterPortBOutputOfSoftECC { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "remaining_memory_locations")]
        public int RemainingMemoryLocations { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "reset_memory_latch_a")]
        [PerformanceRelevant]
        public bool ResetMemoryLatchA { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "reset_memory_latch_b")]
        [PerformanceRelevant]
        public bool ResetMemoryLatchB { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "reset_priority_a")]
        [PerformanceRelevant]
        public EResetPriority ResetPriorityA { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "reset_priority_b")]
        [PerformanceRelevant]
        public EResetPriority ResetPriorityB { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "reset_type")]
        [PerformanceRelevant]
        public EResetType ResetType { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PresentOn(EGenerator.Block_Memory_Generator_4_3)]
        [PropID(EPropAssoc.CoreGen, "softecc")]
        [PerformanceRelevant]
        public bool SoftECC { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_byte_write_enable")]
        [PerformanceRelevant]
        public bool UseByteWriteEnable { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_error_injection_pins")]
        [PerformanceRelevant]
        public bool UseErrorInjectionPins { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_ramb16bwer_reset_behavior")]        
        [PresentOn(EGenerator.Block_Memory_Generator_3_2)]
        public bool UseRamb16bwerResetBehavior { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_regcea_pin")]
        [PerformanceRelevant]
        public bool UseRegCeAPin { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_regceb_pin")]
        [PerformanceRelevant]
        public bool UseRegCeBPin { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_rsta_pin")]
        [PerformanceRelevant]
        public bool UseRstAPin { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_rstb_pin")]
        [PerformanceRelevant]
        public bool UseRstBPin { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "write_depth_a")]
        [PerformanceRelevant]
        public int WriteDepthA { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "write_width_a")]
        [PerformanceRelevant]
        public int WriteWidthA { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "write_width_b")]
        [PerformanceRelevant]
        public int WriteWidthB { [StaticEvaluation] get; set; }

        public int AddrReadWidthA
        {
            [StaticEvaluation]
            get
            {
                long membits = Math.BigMul((int)WriteDepthA, (int)WriteWidthA);
                uint memwords = (uint)((membits + ReadWidthA - 1) / ReadWidthA);
                return (int)Math.Ceiling(Math.Log(memwords, 2.0));
            }
        }

        public int AddrWriteWidthA
        {
            [StaticEvaluation]
            get { return (int)Math.Ceiling(Math.Log(WriteDepthA, 2.0)); }
        }

        public int AddrWidthA
        {
            [StaticEvaluation]
            get { return Math.Max(AddrWriteWidthA, AddrReadWidthA); }
        }

        public int AddrReadWidthB
        {
            [StaticEvaluation]
            get
            {
                long membits = Math.BigMul((int)WriteDepthA, (int)WriteWidthA);
                uint memwords = (uint)((membits + ReadWidthB - 1) / ReadWidthB);
                return (int)Math.Ceiling(Math.Log(memwords, 2.0));
            }
        }

        public int AddrWriteWidthB
        {
            [StaticEvaluation]
            get
            {
                long membits = Math.BigMul((int)WriteDepthA, (int)WriteWidthA);
                uint memwords = (uint)((membits + WriteWidthB - 1) / WriteWidthB);
                return (int)Math.Ceiling(Math.Log(memwords, 2.0));
            }
        }

        public int AddrWidthB
        {
            [StaticEvaluation]
            get { return Math.Max(AddrReadWidthB, AddrWriteWidthB); }
        }

        private class MemoryTransactor : 
            DefaultTransactionSite,
            IXilinxBlockMemSide
        {
            private BlockMem _mem;
            private ETAGroup _group;
            private string _name;

            public MemoryTransactor(BlockMem mem, ETAGroup group):
                base(mem)
            {
                _mem = mem;
                _name = group.ToString();
                _group = group;
            }

            public override string Name
            {
                get {return _name; }
            }

            public bool UseEnable
            {
                [StaticEvaluationDoNotAnalyze]
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.UseEnableA;
                        case ETAGroup.SideB:
                            return _mem.UseEnableB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogic> En
            {
                [StaticEvaluationDoNotAnalyze]
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.EnA;
                        case ETAGroup.SideB:
                            return _mem.EnB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogicVector> We
            {
                [StaticEvaluationDoNotAnalyze]
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.WeA;
                        case ETAGroup.SideB:
                            return _mem.WeB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogicVector> AddrIn
            {
                [StaticEvaluationDoNotAnalyze]
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.AddrA;
                        case ETAGroup.SideB:
                            return _mem.AddrB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogicVector> DataIn
            {
                [StaticEvaluationDoNotAnalyze]
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.DinA;
                        case ETAGroup.SideB:
                            return _mem.DinB;
                        default:
                            throw new NotImplementedException();
                    }
                }
                [AssumeNotCalled]
                set
                {
                    switch (_group)
                    { 
                        case ETAGroup.SideA:
                            _mem.DinA = value;
                            break;
                        case ETAGroup.SideB:
                            _mem.DinB = value;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public Out<StdLogicVector> DataOut
            {
                [StaticEvaluationDoNotAnalyze]
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.DoutA;
                        case ETAGroup.SideB:
                            return _mem.DoutB;
                        default:
                            throw new NotImplementedException();
                    }
                }
                [AssumeNotCalled]
                set
                {
                    switch (_group)
                    { 
                        case ETAGroup.SideA:
                            _mem.DoutA = value;
                            break;
                        case ETAGroup.SideB:
                            _mem.DoutB = value;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            #region IMemoryInterface Member

            [StaticEvaluation]
            public IEnumerable<TAVerb> Read(ISignalSource<StdLogicVector> addr, ISignalSink<StdLogicVector> data)
            {
                Contract.Requires(addr != null);
                Contract.Requires(data != null);

                if (UseEnable)
                {
                    yield return Verb(ETVMode.Locked,
                        En.Dual.Stick<StdLogic>('1'),
                        We.Dual.Stick<StdLogicVector>("0"),
                        AddrIn.Dual.Drive(addr),
                        DataIn.Dual.Stick(StdLogicVector.DCs(DataWriteWidth)));
                }
                else
                {
                    yield return Verb(ETVMode.Locked,
                        We.Dual.Stick<StdLogicVector>("0"),
                        AddrIn.Dual.Drive(addr),
                        DataIn.Dual.Stick(StdLogicVector.DCs(DataWriteWidth)));
                }
                if (data.Sync != null)
                {
                    yield return new TAVerb(this, ETVMode.Shared, () => { });
                    yield return new TAVerb(this, ETVMode.Shared, () => { data.Sync.Write(DataOut.Dual.Cur); });
                }
                else
                {
                    yield return Verb(ETVMode.Shared,
                        data.Comb.Connect(DataOut.Dual.AsSignalSource()));
                }
            }

            [StaticEvaluation]
            public IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> addr, ISignalSource<StdLogicVector> data)
            {
                if (UseEnable)
                {
                    yield return Verb(ETVMode.Locked,
                        En.Dual.Stick<StdLogic>('1'),
                        We.Dual.Stick<StdLogicVector>("1"),
                        AddrIn.Dual.Drive(addr),
                        DataIn.Dual.Drive(data));
                }
                else
                {
                    yield return Verb(ETVMode.Locked,
                        We.Dual.Stick<StdLogicVector>("1"),
                        AddrIn.Dual.Drive(addr),
                        DataIn.Dual.Drive(data));
                }
            }

            [StaticEvaluation]
            public override IEnumerable<TAVerb> DoNothing()
            {
                if (UseEnable)
                {
                    yield return Verb(ETVMode.Shared,
                        En.Dual.Stick<StdLogic>('0'),
                        We.Dual.Stick<StdLogicVector>("0"),
                        AddrIn.Dual.Stick(StdLogicVector.DCs(AddrWriteWidth)),
                        DataIn.Dual.Stick(StdLogicVector.DCs(DataWriteWidth)));
                }
                else
                {
                    yield return Verb(ETVMode.Shared,
                        We.Dual.Stick<StdLogicVector>("0"),
                        AddrIn.Dual.Stick(StdLogicVector.DCs(AddrReadWidth)),
                        DataIn.Dual.Stick(StdLogicVector.DCs(DataWriteWidth)));
                }
            }

            #endregion


            public int AddrReadWidth
            {
                get 
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.AddrReadWidthA;
                        case ETAGroup.SideB:
                            return _mem.AddrReadWidthB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public int AddrWriteWidth
            {
                get 
                { 
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.AddrWriteWidthA;
                        case ETAGroup.SideB:
                            return _mem.AddrWriteWidthB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public int DataReadWidth
            {
                get 
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.ReadWidthA;
                        case ETAGroup.SideB:
                            return _mem.ReadWidthB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public int DataWriteWidth
            {
                get 
                { 
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.WriteWidthA;
                        case ETAGroup.SideB:
                            return _mem.WriteWidthB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public int DataReadDepth
            {
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.ReadDepthA;
                        case ETAGroup.SideB:
                            return _mem.ReadDepthB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public int DataWriteDepth
            {
                get
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.WriteDepthA;
                        case ETAGroup.SideB:
                            return _mem.WriteDepthB;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogicVector> WrEn
            {
                get { return We; }
                set 
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            _mem.WeA = value;
                            break;
                        case ETAGroup.SideB:
                            _mem.WeB = value;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogic> Clk
            {
                get 
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            return _mem.ClkA;
                        case ETAGroup.SideB:
                            return _mem.ClkB;
                        default:
                            throw new NotImplementedException();
                    }
                }
                set
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            _mem.ClkA = value;
                            break;
                        case ETAGroup.SideB:
                            _mem.ClkB = value;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogicVector> Addr
            {
                get { return AddrIn; }
                set
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            _mem.AddrA = value;
                            break;
                        case ETAGroup.SideB:
                            _mem.AddrB = value;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public In<StdLogic> RdEn
            {
                get { return En; }
                set
                {
                    switch (_group)
                    {
                        case ETAGroup.SideA:
                            _mem.EnA = value;
                            break;
                        case ETAGroup.SideB:
                            _mem.EnB = value;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public void PreWrite(StdLogicVector addr, StdLogicVector data)
            {
                _mem.InitialWrite(addr, data);
            }

            public override void Establish(IAutoBinder binder)
            {
                var bmem = _mem;
                if (bmem.ClkA == null)
                    bmem.ClkA = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                if (bmem.UseEnableA && bmem.EnA == null)
                    bmem.EnA = binder.GetSignal<StdLogic>(EPortUsage.Default, "EnA", null, '0');
                if (bmem.WeA == null)
                    bmem.WeA = binder.GetSignal<StdLogicVector>(EPortUsage.Default, "WeA", null, "0");
                if (bmem.DinA == null)
                    bmem.DinA = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "DInA", null,
                        StdLogicVector._0s(bmem.WriteWidthA));
                if (bmem.DoutA == null)
                    bmem.DoutA = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "DOutA", null,
                        StdLogicVector._0s(bmem.ReadWidthA));
                if (bmem.MemoryType == EMemoryType.DualPortROM ||
                    bmem.MemoryType == EMemoryType.SimpleDualPortRAM ||
                    bmem.MemoryType == EMemoryType.TrueDualPortRAM)
                {
                    if (bmem.AddrB == null)
                        bmem.AddrB = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "AddrB", null,
                            StdLogicVector._0s(bmem.AddrWidthB));
                    if (bmem.ClkB == null)
                        bmem.ClkB = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                    if (bmem.UseEnableB && bmem.EnB == null)
                        bmem.EnB = binder.GetSignal<StdLogic>(EPortUsage.Default, "EnB", null, '0');
                    if (bmem.WeB == null)
                        bmem.WeB = binder.GetSignal<StdLogicVector>(EPortUsage.Default, "WeB", null, "0");
                    if (bmem.DinB == null)
                        bmem.DinB = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "DInB", null,
                            StdLogicVector._0s(bmem.WriteWidthB));
                    if (bmem.DoutB != null)
                        bmem.DoutB = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "DOutB", null,
                            StdLogicVector._0s(bmem.ReadWidthB));
                    if (bmem.AddrB != null)
                        bmem.AddrB = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "AddrB", null,
                            StdLogicVector._0s(bmem.AddrWidthB));
                }
            }

            public async Task<StdLogicVector> Read(StdLogicVector addr)
            {
                AddrIn.Dual.Next = addr;
                await RisingEdge(Clk);
                return DataOut.Dual.Cur;
            }

            public async void Write(StdLogicVector addr, StdLogicVector data)
            {
                AddrIn.Dual.Next = addr;
                DataIn.Dual.Next = data;
                await RisingEdge(Clk);
            }

            public uint Depth
            {
                get { return (uint)DataReadDepth; }
            }

            public uint Width
            {
                get { return (uint)DataReadWidth; }
            }
        }

        private MemoryTransactor _sideA, _sideB;

        /// <summary>
        /// Returns the transaction site of side A
        /// </summary>
        public IXilinxBlockMemSide SideA
        {
            [StaticEvaluation] get { return _sideA; }
        }

        /// <summary>
        /// Returns the transaction site of side B
        /// </summary>
        public IXilinxBlockMemSide SideB
        {
            [StaticEvaluation] get { return _sideB; }
        }

        private int _memsize;
        private SLVSignal _data;
        private SLVSignal _dataA1, _dataA2;
        private SLVSignal _dataB1, _dataB2;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public BlockMem()
        {
            Generator = EGenerator.Block_Memory_Generator_4_3;
            AdditionalInputsForPowerEstimation = false;
            IPAlgorithm = EIPAlgorithm.MinimumArea;
            AssumeSynchronousClk = true;
            ByteSize = 9;
            COEFile = "no_coe_file_loaded";
            CollisionWarnings = ECollisionWarnings.All;
            DisableCollisionWarnings = false;
            DisableOutOfRangeWarnings = false;
            ECC = false;
            ECCType = EECCType.NoECC;
            EnableA = EENAUsage.UseENAPin;
            EnableB = EENBUsage.UseENBPin;
            ErrorInjectionType = EErrorInjectionType.SingleBitErrorInjection;
            FillRemainingMemoryLocations = false;
            LoadInitFile = false;
            MemoryType = EMemoryType.TrueDualPortRAM;
            OperatingModeA = EOperatingMode.WriteFirst;
            OperatingModeB = EOperatingMode.WriteFirst;
            OutputResetValueA = 0;
            OutputResetValueB = 0;
            PipelineStages = 0;
            PortAClock = 100;
            PortAEnableRate = 100;
            PortAWriteRate = 50;
            PortBClock = 100;
            PortBEnableRate = 100;
            PortBWriteRate = 50;
            Primitive = EPrimitive._8kx2;
            ReadWidthA = 32;
            ReadWidthB = 32;
            RegisterPortAInputOfSoftECC = false;
            RegisterPortAOutputOfMemoryCore = false;
            RegisterPortAOutputOfMemoryPrimitives = false;
            RegisterPortBOutputOfMemoryCore = false;
            RegisterPortBOutputOfMemoryPrimitives = false;
            RegisterPortBOutputOfSoftECC = false;
            RemainingMemoryLocations = 0;
            ResetMemoryLatchA = false;
            ResetMemoryLatchB = false;
            ResetPriorityA = EResetPriority.CE;
            ResetPriorityB = EResetPriority.CE;
            ResetType = EResetType.Sync;
            SoftECC = false;
            UseByteWriteEnable = false;
            UseErrorInjectionPins = false;
            UseRegCeAPin = false;
            UseRegCeBPin = false;
            UseRstAPin = false;
            UseRstBPin = false;
            WriteDepthA = 512;
            WriteWidthA = 32;
            WriteWidthB = 32;
            _sideA = new MemoryTransactor(this, ETAGroup.SideA);
            _sideB = new MemoryTransactor(this, ETAGroup.SideB);
        }

        protected override void PreInitialize()
        {
            _memsize = (int)Math.BigMul(WriteDepthA, WriteWidthA);
            _data = new SLVSignal((int)_memsize);

            if (InitialImage != null)
            {
                StdLogicVector init = StdLogicVector._0s(0);
                for (int i = 0; i < InitialImage.Length; i++)
                    init = InitialImage[i].Concat(init);
                _data.InitialValue = init;
            }
            else
            {
                _data.InitialValue = StdLogicVector.Us((int)_memsize);
            }

            if (RegisterPortAOutputOfMemoryPrimitives)
                _dataA1 = new SLVSignal(ReadWidthA);
            if (RegisterPortAOutputOfMemoryCore)
                _dataA2 = new SLVSignal(ReadWidthA);
            if (RegisterPortBOutputOfMemoryPrimitives)
                _dataB1 = new SLVSignal(ReadWidthB);
            if (RegisterPortBOutputOfMemoryCore)
                _dataB2 = new SLVSignal(ReadWidthB);
        }

        public bool IsROM
        {
            get
            {
                return MemoryType == EMemoryType.DualPortROM ||
                    MemoryType == EMemoryType.SinglePortROM;
            }
        }

        protected override void Initialize()
        {
            AddProcess(OnClockA, ClkA.ChangedEvent);
            if (MemoryType != EMemoryType.SinglePortRAM &&
                MemoryType != EMemoryType.SinglePortROM)
            {
                AddProcess(OnClockB, ClkB.ChangedEvent);
            }

            if (AddrA == null || DoutA == null)
                throw new InvalidOperationException("Unbound port");
            if (!IsROM && (DinA == null || WeA == null))
                throw new InvalidOperationException("Unbound port");
            if (UseEnableA && EnA == null)
                throw new InvalidOperationException("Unbound port");

            if ((!IsROM && WriteWidthA != DinA.Size()) ||
                ReadWidthA != DoutA.Size() ||
                AddrWidthA != AddrA.Size())
                throw new InvalidOperationException("Size mismatch");

            if (MemoryType == EMemoryType.DualPortROM ||
                MemoryType == EMemoryType.SimpleDualPortRAM ||
                MemoryType == EMemoryType.TrueDualPortRAM)
            {
                if ((!IsROM && WriteWidthB != DinB.Size()) ||
                    ReadWidthB != DoutB.Size() ||
                    AddrWidthB != AddrB.Size())
                    throw new InvalidOperationException("Size mismatch");

                if (AddrB == null || DoutB == null)
                    throw new InvalidOperationException("Unbound port");
                if (!IsROM && (DinB == null || WeB == null))
                    throw new InvalidOperationException("Unbound port");
                if (UseEnableB && EnB == null)
                    throw new InvalidOperationException("Unbound port");
            }
        }

        protected override void PostInitialize()
        {
        }

        private void ReportCollision(int addr)
        {
            Console.WriteLine("Block memory: collision detected (address = 0x" + addr.ToString("X") + ")");
        }

        [DoNotAnalyze]
        private void OnClockA()
        {
            if (ClkA.RisingEdge())
            {
                if (EnableA == EENAUsage.AlwaysEnabled || EnA.Cur == '1')
                {
                    StdLogicVector result = StdLogicVector.Xs(ReadWidthA);

                    int addr = ReadWidthA * (int)AddrA.Cur.ULongValue;
                    if (WeA.Cur.Equals((StdLogicVector)"1"))
                    {
                        if (AddrA.Cur.IsProper)
                        {
                            if (EnableB == EENBUsage.AlwaysEnabled || EnB.Cur == '1')
                            {
                                int addrB = ReadWidthB * (int)AddrB.Cur.ULongValue;
                                if (addr == addrB)
                                {
                                    ReportCollision(addr);
                                }
                            }

                            int hiaddr = addr + WriteWidthA - 1;
                            _data[hiaddr, addr].Next = DinA.Cur;
                        }
                        else
                        {
                            Context.Report(EIssueClass.Warning, "Improper address, will corrupt memory");
                            _data.Next = StdLogicVector.Xs((int)_memsize);
                        }
                    }
                    else
                    {
                        if (AddrA.Cur.IsProper)
                        {
                            //Console.WriteLine(Context.CurTime + ": read access to address " + AddrA.Cur.ToString());
                            int hiaddr = addr + ReadWidthA - 1;
                            result = _data[hiaddr, addr].Cur;
                        }
                        else
                        {
                            result = StdLogicVector.Xs(ReadWidthA);
                        }
                    }

                    if (RegisterPortAOutputOfMemoryPrimitives)
                        _dataA1.Next = result;
                    else if (RegisterPortAOutputOfMemoryCore)
                        _dataA2.Next = result;
                    else
                        DoutA.Next = result;
                }

                if (RegisterPortAOutputOfMemoryCore &&
                    RegisterPortAOutputOfMemoryPrimitives)
                {
                    _dataA2.Next = _dataA1.Cur;
                    DoutA.Next = _dataA2.Cur;
                }
                else if (RegisterPortAOutputOfMemoryPrimitives)
                {
                    DoutA.Next = _dataA1.Cur;
                }
                else if (RegisterPortBOutputOfMemoryCore)
                {
                    DoutA.Next = _dataA2.Cur;
                }
            }
        }

        [DoNotAnalyze]
        private void OnClockB()
        {
            if (ClkB.RisingEdge())
            {
                if (EnableB == EENBUsage.AlwaysEnabled || EnB.Cur == '1')
                {
                    StdLogicVector result = StdLogicVector.Xs(ReadWidthB);

                    int addr = ReadWidthB * (int)AddrB.Cur.ULongValue;
                    if (WeB.Cur.Equals((StdLogicVector)"1"))
                    {
                        if (AddrB.Cur.IsProper)
                        {
                            if (EnableA == EENAUsage.AlwaysEnabled || EnA.Cur == '1')
                            {
                                int addrA = ReadWidthA * (int)AddrA.Cur.ULongValue;
                                if (addr == addrA)
                                {
                                    ReportCollision(addr);
                                }
                            }

                            //Console.WriteLine(Context.CurTime + ": write access to address " + BddrB.Cur.ToString());
                            int hiaddr = addr + WriteWidthB - 1;
                            _data[hiaddr, addr].Next = DinB.Cur;
                        }
                        else
                        {
                            Context.Report(EIssueClass.Warning, "Improper address, will corrupt memory");
                            _data.Next = StdLogicVector.Xs((int)_memsize);
                        }
                    }
                    else
                    {
                        if (AddrB.Cur.IsProper)
                        {
                            //Console.WriteLine(Context.CurTime + ": read access to address " + BddrB.Cur.ToString());
                            int hiaddr = addr + ReadWidthB - 1;
                            result = _data[hiaddr, addr].Cur;
                        }
                        else
                        {
                            result = StdLogicVector.Xs(ReadWidthB);
                        }
                    }

                    if (RegisterPortBOutputOfMemoryPrimitives)
                        _dataB1.Next = result;
                    else if (RegisterPortBOutputOfMemoryCore)
                        _dataB2.Next = result;
                    else
                        DoutB.Next = result;
                }

                if (RegisterPortBOutputOfMemoryCore &&
                    RegisterPortBOutputOfMemoryPrimitives)
                {
                    _dataB2.Next = _dataB1.Cur;
                    DoutB.Next = _dataB2.Cur;
                }
                else if (RegisterPortBOutputOfMemoryPrimitives)
                {
                    DoutB.Next = _dataB1.Cur;
                }
                else if (RegisterPortBOutputOfMemoryCore)
                {
                    DoutB.Next = _dataB2.Cur;
                }
            }
        }

        protected override void OnSynthesis(ISynthesisContext ctx)
        {
            var xproj = ctx.Project as XilinxProject;
            if (xproj == null)
                return;

            switch (xproj.ISEVersion)
            {
                case EISEVersion._11_1: // ?
                case EISEVersion._11_2: // !
                case EISEVersion._11_3: // ?
                case EISEVersion._11_4: // ?
                case EISEVersion._11_5: // ?
                    Generator = EGenerator.Block_Memory_Generator_3_2;
                    break;

                case EISEVersion._13_2: // !
                default: // ?
                    Generator = EGenerator.Block_Memory_Generator_4_3;
                    break;
            }

            string name = ctx.CodeGen.GetComponentID(Descriptor);
            ComponentName = name;
            CoreGenDescription cgproj, xco;
            xproj.AddNewCoreGenDescription(name, out cgproj, out xco);
            if (InitialImage != null)
            {
                string coefile = name + ".coe";
                string coedir = Path.GetDirectoryName(cgproj.Path);
                if (!Directory.Exists(coedir))
                    Directory.CreateDirectory(coedir);
                string coepath = Path.Combine(coedir, coefile);
                //string coepath = xproj.AddFile(coefile);
                COEDescription coe = new COEDescription(COEDescription.ETarget.BlockMem);
                coe.Data = InitialImage;
                coe.Radix = 2;
                coe.Store(coepath);
                COEFile = coefile;
                LoadInitFile = true;
            }
            xco.FromComponent(this);
            xco.Store();
            xproj.ExecuteCoreGen(xco.Path, cgproj.Path);
        }

        public void InitialWrite(StdLogicVector addr, StdLogicVector data)
        {
            if (InitialImage == null)
            {
                InitialImage = new StdLogicVector[WriteDepthA];
                for (int i = 0; i < WriteDepthA; i++)
                    InitialImage[i] = StdLogicVector.FromLong(RemainingMemoryLocations, (int)WriteWidthA);
            }

            ulong laddr = addr.ULongValue;
            if (data.Size != WriteWidthA)
                throw new ArgumentException("Wrong data size");
            InitialImage[laddr] = data;
        }

        public int ReadDepthA
        {
            get { return (int)((_memsize + (uint)ReadWidthA - 1) / (uint)ReadWidthA); }
        }

        public int WriteDepthB
        {
            get { return (int)((_memsize + (uint)WriteWidthB - 1) / (uint)WriteWidthB); }
        }

        public int ReadDepthB
        {
            get { return (int)((_memsize + (uint)ReadWidthB - 1) / (uint)ReadWidthB); }
        }

        public ulong BaseAddress { get; set; }
    }

    /// <summary>
    /// Maps memory read/write instructions to Xilinx block memory.
    /// </summary>
    public class BlockMemXILMapper : IXILMapper
    { 
        private class BlockMemReadMapping: DefaultXILMapping
        {
            private IXilinxBlockMemSide _site;

            public BlockMemReadMapping(IXilinxBlockMemSide site):
                base(site, EMappingKind.ExclusiveResource)
            {
                _site = site;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _site.Read(operands[0], results[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                return _site.Read(
                    SignalSource.Create(StdLogicVector._0s(_site.AddrReadWidth)),
                    SignalSink.Nil<StdLogicVector>());
            }

            public override int InitiationInterval
            {
                get { return 1; }
            }

            public override int Latency
            {
                get { return 1; }
            }

            public override string Description
            {
                get { return _site.DataReadDepth + "x" + _site.DataReadWidth + " bit block memory element reader"; }
            }
        }

        private class BlockMemReadFixMapping : DefaultXILMapping
        {
            private IXilinxBlockMemSide _site;
            private Unsigned _addr;

            public BlockMemReadFixMapping(IXilinxBlockMemSide site, Unsigned addr) :
                base(site, EMappingKind.ExclusiveResource)
            {
                _site = site;
                _addr = addr;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _site.Read(SignalSource.Create(_addr.SLVValue), results[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                return _site.Read(
                    SignalSource.Create(StdLogicVector._0s(_site.AddrReadWidth)),
                    SignalSink.Nil<StdLogicVector>());
            }

            public override int InitiationInterval
            {
                get { return 1; }
            }

            public override int Latency
            {
                get { return 1; }
            }

            public override string Description
            {
                get { return _site.DataReadDepth + "x" + _site.DataReadWidth + " bit block memory fixed element reader"; }
            }
        }

        private class BlockMemWriteMapping : DefaultXILMapping
        {
            private IXilinxBlockMemSide _site;

            public BlockMemWriteMapping(IXilinxBlockMemSide site) :
                base(site, EMappingKind.ExclusiveResource)
            {
                _site = site;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _site.Write(operands[1], operands[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                return _site.Write(
                    SignalSource.Create(StdLogicVector._0s(_site.AddrWriteWidth)),
                    SignalSource.Create(StdLogicVector._0s(_site.DataWriteWidth)));
            }

            public override int InitiationInterval
            {
                get { return 1; }
            }

            public override int Latency
            {
                get { return 1; }
            }

            public override string Description
            {
                get { return _site.DataReadDepth + "x" + _site.DataReadWidth + " bit block memory element writer"; }
            }
        }

        private class BlockMemWriteFixMapping : DefaultXILMapping
        {
            private IXilinxBlockMemSide _site;
            private Unsigned _addr;

            public BlockMemWriteFixMapping(IXilinxBlockMemSide site, Unsigned addr) :
                base(site, EMappingKind.ExclusiveResource)
            {
                _site = site;
                _addr = addr;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _site.Write(SignalSource.Create(_addr.SLVValue), operands[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                return _site.Write(
                    SignalSource.Create(StdLogicVector._0s(_site.AddrWriteWidth)),
                    SignalSource.Create(StdLogicVector._0s(_site.DataWriteWidth)));
            }

            public override int InitiationInterval
            {
                get { return 1; }
            }

            public override int Latency
            {
                get { return 1; }
            }

            public override string Description
            {
                get { return _site.DataReadDepth + "x" + _site.DataReadWidth + " bit block memory fixed element writer"; }
            }
        }

        private MemoryRegion _region;

        /// <summary>
        /// Constructs a new mapper.
        /// </summary>
        /// <param name="region">memory region to associate this mapper with</param>
        public BlockMemXILMapper(MemoryRegion region)
        {
            _region = region;
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.RdMem(_region);
            yield return DefaultInstructionSet.Instance.RdMemFix(_region, 0);
            yield return DefaultInstructionSet.Instance.WrMem(_region);
            yield return DefaultInstructionSet.Instance.WrMemFix(_region, 0);
        }

        private int GetAddrSize(TypeDescriptor addrType)
        {
            if (addrType.CILType.Equals(typeof(Unsigned)) ||
                addrType.CILType.Equals(typeof(StdLogicVector)))
            {
                return (int)addrType.TypeParams[0];
            }
            else
            {
                return -1;
            }
        }

        public IXILMapping TryMapOne(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            var bmem = fu as BlockMem;
            if (bmem == null)
                return null;
            var side = taSite as IXilinxBlockMemSide;

            MemoryRegion region = null;
            Unsigned addr = Unsigned.FromUInt(0, 1);
            switch (instr.Name)
            {
                case InstructionCodes.RdMem:
                case InstructionCodes.WrMem:
                    region = instr.Operand as MemoryRegion;
                    break;

                case InstructionCodes.RdMemFix:
                case InstructionCodes.WrMemFix:
                    {
                        var tup = ((Tuple<MemoryRegion, Unsigned>)instr.Operand);
                        region = tup.Item1;
                        addr = tup.Item2;
                        if (addr.Size != bmem.AddrWidthA)
                            throw new InvalidOperationException("Wrong address width");
                    }
                    break;

                default:
                    return null;
            }

            TypeDescriptor dataType;

            switch (instr.Name)
            {
                case InstructionCodes.RdMem:
                    dataType = TypeDescriptor.GetTypeOf(StdLogicVector._0s(side.DataReadWidth));
                    if (bmem.SideA.AddrReadWidth != GetAddrSize(operandTypes[0]))
                        return null;
                    if (!dataType.Equals(resultTypes[0]))
                        return null;
                    return new BlockMemReadMapping(side);

                case InstructionCodes.RdMemFix:
                    dataType = TypeDescriptor.GetTypeOf(StdLogicVector._0s(side.DataReadWidth));
                    if (!dataType.Equals(resultTypes[0]))
                        return null;
                    return new BlockMemReadFixMapping(side, addr);

                case InstructionCodes.WrMem:
                    dataType = TypeDescriptor.GetTypeOf(StdLogicVector._0s(side.DataReadWidth));
                    if (!dataType.Equals(operandTypes[0]))
                        return null;
                    if (bmem.SideA.AddrReadWidth != GetAddrSize(operandTypes[1]))
                        return null;
                    return new BlockMemWriteMapping(side);

                case InstructionCodes.WrMemFix:
                    dataType = TypeDescriptor.GetTypeOf(StdLogicVector._0s(bmem.SideB.DataReadWidth));
                    if (!dataType.Equals(operandTypes[0]))
                        return null;
                    return new BlockMemWriteFixMapping(side, addr);

                default:
                    return null;
            }
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var mapping = TryMapOne(taSite, instr, operandTypes, resultTypes);
            if (mapping != null)
                yield return mapping;
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            MemoryRegion region = null;
            IXilinxBlockMemSide side = null;
            Unsigned fixAddr = Unsigned.FromUInt(0, 1);
            switch (instr.Name)
            {
                case InstructionCodes.RdMem:
                case InstructionCodes.WrMem:
                    region = instr.Operand as MemoryRegion;
                    side = instr.Operand as IXilinxBlockMemSide;
                    break;

                case InstructionCodes.RdMemFix:
                case InstructionCodes.WrMemFix:
                    {
                        var tup = ((Tuple<MemoryRegion, Unsigned>)instr.Operand);
                        region = tup.Item1;
                        fixAddr = tup.Item2;
                    }
                    break;

                default:
                    return null;
            }

            if (side == null &&
                region != _region)
                return null;

            if (side == null)
            {
                var bmem = new BlockMem()
                {
                    IPAlgorithm = BlockMem.EIPAlgorithm.MinimumArea,
                    AssumeSynchronousClk = true,
                    ECC = false,
                    ECCType = BlockMem.EECCType.NoECC,
                    EnableA = BlockMem.EENAUsage.AlwaysEnabled,
                    EnableB = BlockMem.EENBUsage.AlwaysEnabled,
                    ErrorInjectionType = BlockMem.EErrorInjectionType.SingleBitErrorInjection,
                    MemoryType = BlockMem.EMemoryType.SinglePortRAM,
                    OperatingModeA = BlockMem.EOperatingMode.WriteFirst,
                    OperatingModeB = BlockMem.EOperatingMode.WriteFirst,
                    PipelineStages = 1,
                    ReadWidthA = (int)region.MarshalInfo.WordSize,
                    ReadWidthB = (int)region.MarshalInfo.WordSize,
                    RegisterPortAInputOfSoftECC = false,
                    RegisterPortAOutputOfMemoryCore = false,
                    RegisterPortAOutputOfMemoryPrimitives = false,
                    RegisterPortBOutputOfMemoryCore = false,
                    RegisterPortBOutputOfMemoryPrimitives = false,
                    RegisterPortBOutputOfSoftECC = false,
                    ResetMemoryLatchA = false,
                    ResetMemoryLatchB = false,
                    ResetPriorityA = BlockMem.EResetPriority.CE,
                    ResetPriorityB = BlockMem.EResetPriority.CE,
                    ResetType = BlockMem.EResetType.Sync,
                    SoftECC = false,
                    UseByteWriteEnable = false,
                    UseErrorInjectionPins = false,
                    UseRegCeAPin = false,
                    UseRegCeBPin = false,
                    UseRstAPin = false,
                    UseRstBPin = false,
                    WriteDepthA = MathExt.CeilPow2((int)region.RequiredSize),
                    WriteWidthA = (int)region.MarshalInfo.WordSize,
                    WriteWidthB = (int)region.MarshalInfo.WordSize
                };

                foreach (MemoryMappedStorage mms in region.Items)
                {
                    if (mms.Data != null)
                    {
                        StdLogicVector[] data = mms.Layout.SerializeInstance(mms.Data);
                        StdLogicVector addr = mms.BaseAddress.SLVValue;
                        foreach (StdLogicVector dataWord in data)
                        {
                            bmem.InitialWrite(addr, dataWord);
                            addr += "1";
                        }
                    }
                }

                side = bmem.SideA;
            }

            switch (instr.Name)
            {
                case InstructionCodes.RdMem:
                    return new BlockMemReadMapping(side);

                case InstructionCodes.RdMemFix:
                    return new BlockMemReadFixMapping(side, fixAddr);

                case InstructionCodes.WrMem:
                    return new BlockMemWriteMapping(side);

                case InstructionCodes.WrMemFix:
                    return new BlockMemWriteFixMapping(side, fixAddr);

                default:
                    throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    /// An <c>IBlockMemFactory</c> implementation which creates Xilinx block memories.
    /// </summary>
    public class XilinxBlockMemFactory : IBlockMemFactory
    {
        public void CreateROM(int addrWidth, int dataWidth, int capacity, int readLatency, 
            out Component part, out IROM rom)
        {
            bool regPrim, regOut;
            switch (readLatency)
            {
                case 1:
                    regPrim = false;
                    regOut = false;
                    break;

                case 2:
                    regPrim = true;
                    regOut = false;
                    break;

                case 3:
                    regPrim = true;
                    regOut = true;
                    break;

                default:
                    throw new NotSupportedException();
            }

            BlockMem bmem = new BlockMem()
            {
                IPAlgorithm = BlockMem.EIPAlgorithm.MinimumArea,
                AssumeSynchronousClk = false,
                ECC = false,
                ECCType = BlockMem.EECCType.NoECC,
                EnableA = BlockMem.EENAUsage.UseENAPin,
                EnableB = BlockMem.EENBUsage.AlwaysEnabled,
                ErrorInjectionType = BlockMem.EErrorInjectionType.SingleBitErrorInjection,
                MemoryType = BlockMem.EMemoryType.SinglePortROM,
                OperatingModeA = BlockMem.EOperatingMode.WriteFirst,
                OperatingModeB = BlockMem.EOperatingMode.WriteFirst,
                PipelineStages = readLatency - 1,
                ReadWidthA = dataWidth,
                ReadWidthB = dataWidth,
                RegisterPortAInputOfSoftECC = false,
                RegisterPortAOutputOfMemoryCore = regOut,
                RegisterPortAOutputOfMemoryPrimitives = regPrim,
                RegisterPortBOutputOfMemoryCore = false,
                RegisterPortBOutputOfMemoryPrimitives = false,
                RegisterPortBOutputOfSoftECC = false,
                ResetMemoryLatchA = false,
                ResetMemoryLatchB = false,
                ResetPriorityA = BlockMem.EResetPriority.CE,
                ResetPriorityB = BlockMem.EResetPriority.CE,
                ResetType = BlockMem.EResetType.Sync,
                SoftECC = false,
                UseByteWriteEnable = false,
                UseErrorInjectionPins = false,
                UseRegCeAPin = false,
                UseRegCeBPin = false,
                UseRstAPin = false,
                UseRstBPin = false,
                WriteDepthA = MathExt.CeilPow2(capacity),
                WriteWidthA = dataWidth,
                WriteWidthB = dataWidth
            };
            part = bmem;
            rom = bmem.SideA;
        }

        public void CreateRAM(int addrWidth, int dataWidth, int capacity, int readLatency, int writeLatency,
            out Component part, out IRAM ram)
        {
            if (writeLatency != 1)
                throw new NotSupportedException();

            bool regPrim, regOut;
            switch (readLatency)
            {
                case 1:
                    regPrim = false;
                    regOut = false;
                    break;

                case 2:
                    regPrim = true;
                    regOut = false;
                    break;

                case 3:
                    regPrim = true;
                    regOut = true;
                    break;

                default:
                    throw new NotSupportedException();
            }

            BlockMem bmem = new BlockMem()
            {
                IPAlgorithm = BlockMem.EIPAlgorithm.MinimumArea,
                AssumeSynchronousClk = false,
                ECC = false,
                ECCType = BlockMem.EECCType.NoECC,
                EnableA = BlockMem.EENAUsage.UseENAPin,
                EnableB = BlockMem.EENBUsage.AlwaysEnabled,
                ErrorInjectionType = BlockMem.EErrorInjectionType.SingleBitErrorInjection,
                MemoryType = BlockMem.EMemoryType.SinglePortRAM,
                OperatingModeA = BlockMem.EOperatingMode.WriteFirst,
                OperatingModeB = BlockMem.EOperatingMode.WriteFirst,
                PipelineStages = 0,
                ReadWidthA = dataWidth,
                ReadWidthB = dataWidth,
                RegisterPortAInputOfSoftECC = false,
                RegisterPortAOutputOfMemoryCore = regOut,
                RegisterPortAOutputOfMemoryPrimitives = regPrim,
                RegisterPortBOutputOfMemoryCore = false,
                RegisterPortBOutputOfMemoryPrimitives = false,
                RegisterPortBOutputOfSoftECC = false,
                ResetMemoryLatchA = false,
                ResetMemoryLatchB = false,
                ResetPriorityA = BlockMem.EResetPriority.CE,
                ResetPriorityB = BlockMem.EResetPriority.CE,
                ResetType = BlockMem.EResetType.Sync,
                SoftECC = false,
                UseByteWriteEnable = false,
                UseErrorInjectionPins = false,
                UseRegCeAPin = false,
                UseRegCeBPin = false,
                UseRstAPin = false,
                UseRstBPin = false,
                WriteDepthA = MathExt.CeilPow2(capacity),
                WriteWidthA = dataWidth,
                WriteWidthB = dataWidth
            };
            part = bmem;
            ram = bmem.SideA;
        }
    }
}

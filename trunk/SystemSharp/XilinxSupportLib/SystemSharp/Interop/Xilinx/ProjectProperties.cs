/**
 * Copyright 2011 Christian Köllner
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

namespace SystemSharp.Interop.Xilinx
{
    public enum EBusFormat
    {
        [PropID(EPropAssoc.ISE, "<>")]
        [PropID(EPropAssoc.CoreGen, "BusFormatAngleBracketNotRipped")]
        [PropID(EPropAssoc.CoreGenProj, "BusFormatAngleBracketNotRipped")]
        BusFormatAngleBracketNotRipped
    }

    public enum EFlowVendor
    {
        [PropID(EPropAssoc.CoreGen, "Foundation_ISE")]
        [PropID(EPropAssoc.CoreGenProj, "Foundation_ISE")]
        Foundation_ISE
    }

    public enum EImplementationFileType
    {
        [PropID(EPropAssoc.CoreGenProj, "Ngc")]
        NGC
    }

    public enum ESimulationFiles
    {
        [PropID(EPropAssoc.CoreGenProj, "Behavioral")]
        Behavioral
    }

    public enum EAnalysisEffortLevel
    {
        [PropID(EPropAssoc.ISE, "Standard")]
        Standard
    }

    public enum ECase
    {
        [PropID(EPropAssoc.ISE, "Maintain")]
        Maintain
    }

    public enum ECaseImplementationStyle
    {
        [PropID(EPropAssoc.ISE, "None")]
        None
    }

    public enum EPinConfig
    {
        [PropID(EPropAssoc.ISE, "Pull Up")]
        PullUp,

        [PropID(EPropAssoc.ISE, "Pull Down")]
        PullDown
    }

    public enum EDCIUpdateMode
    {
        [PropID(EPropAssoc.ISE, "Quiet(Off)")]
        Quiet
    }

    public enum EDelayValue
    {
        [PropID(EPropAssoc.ISE, "Setup Time")]
        SetupTime
    }

    public enum EOutputEvents
    {
        [PropID(EPropAssoc.ISE, "Default (4)")]
        Default_4,

        [PropID(EPropAssoc.ISE, "Default (5)")]
        Default_5,

        [PropID(EPropAssoc.ISE, "Default (6)")]
        Default_6
    }

    public enum EEnableMultiThreading
    {
        [PropID(EPropAssoc.ISE, "Off")]
        Off
    }

    public enum EEncryptKeySelectVirtex6
    {
        [PropID(EPropAssoc.ISE, "BRAM")]
        BRAM
    }

    public enum EExtraEffort
    {
        [PropID(EPropAssoc.ISE, "None")]
        None
    }

    public enum EStartupClock
    {
        [PropID(EPropAssoc.ISE, "CCLK")]
        CCLK
    }

    public enum EFSMEncodingAlgorithm
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum EFSMStyle
    {
        [PropID(EPropAssoc.ISE, "LUT")]
        LUT
    }

    public enum EFallbackReconfiguration
    {
        [PropID(EPropAssoc.ISE, "Enable")]
        Enable
    }

    public enum EGenerateRTLSchematic
    {
        [PropID(EPropAssoc.ISE, "Yes")]
        Yes
    }

    public enum EGlobalOptimizationGoal
    {
        [PropID(EPropAssoc.ISE, "AllClockNets")]
        AllClockNets
    }

    public enum EGlobalOptimizationMapVirtex5
    {
        [PropID(EPropAssoc.ISE, "Off")]
        [PropID(EPropAssoc.MAP, "off")]
        Off,

        [PropID(EPropAssoc.ISE, "Area")]
        [PropID(EPropAssoc.MAP, "area")]
        Area,

        [PropID(EPropAssoc.ISE, "Speed")]
        [PropID(EPropAssoc.MAP, "speed")]
        Speed,

        [PropID(EPropAssoc.ISE, "Power")]
        [PropID(EPropAssoc.MAP, "power")]
        Power
    }

    public enum EJTAGToSystemMonitorConnection
    {
        [PropID(EPropAssoc.ISE, "Enable")]
        Enable
    }

    public enum EKeepHierarchy
    {
        [PropID(EPropAssoc.ISE, "No")]
        No
    }

    public enum ELUTCombining
    {
        [PropID(EPropAssoc.ISE, "Off")]
        [PropID(EPropAssoc.MAP, "off")]
        Off,

        [PropID(EPropAssoc.ISE, "Auto")]
        [PropID(EPropAssoc.MAP, "auto")]
        Auto,

        [PropID(EPropAssoc.ISE, "Area")]
        [PropID(EPropAssoc.MAP, "area")]
        Area
    }

    public enum ELanguage
    {
        [PropID(EPropAssoc.ISE, "All")]
        All
    }

    public enum EMUXExtraction
    {
        [PropID(EPropAssoc.ISE, "Yes")]
        Yes
    }

    public enum ENetlistHierarchy
    {
        [PropID(EPropAssoc.ISE, "As Optimized")]
        AsOptimized
    }

    public enum ENetlistTranslationType
    {
        [PropID(EPropAssoc.ISE, "Timestamp")]
        Timestamp
    }

    public enum EOptimizationEffort
    {
        [PropID(EPropAssoc.ISE, "Normal")]
        Normal
    }

    public enum EOptimizationGoal
    {
        [PropID(EPropAssoc.ISE, "Speed")]
        Speed
    }

    public enum EPackIORegistersIntoIOBs
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto,

        [PropID(EPropAssoc.ISE, "Off")]
        Off
    }

    public enum EPlaceAndRouteEffortLevel
    {
        [PropID(EPropAssoc.ISE, "High")]
        High
    }

    public enum EPlaceAndRouteMode
    {
        [PropID(EPropAssoc.ISE, "Normal Place and Route")]
        Normal,

        [PropID(EPropAssoc.ISE, "Place Only")]
        PlaceOnly,

        [PropID(EPropAssoc.ISE, "Route Only")]
        RouteOnly,

        [PropID(EPropAssoc.ISE, "Reentrant Route")]
        ReentrantRoute
    }

    public enum EPlacerEffortLevelMap
    {
        [PropID(EPropAssoc.MAP, "std")]
        Standard,

        [PropID(EPropAssoc.MAP, "med")]
        Medium,

        [PropID(EPropAssoc.ISE, "High")]
        [PropID(EPropAssoc.MAP, "high")]
        High
    }

    public enum EPlacerExtraEffortMap
    {
        [PropID(EPropAssoc.ISE, "None")]
        None
    }

    public enum EPowerReductionMapVirtex6
    {
        [PropID(EPropAssoc.ISE, "Off")]
        Off
    }

    public enum EPropertySpecification
    {
        [PropID(EPropAssoc.ISE, "Store all values")]
        StoreAllValues
    }

    public enum ERAMStyle
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum EROMStyle
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum EReduceControlSets
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum ERegenerateCore
    {
        [PropID(EPropAssoc.ISE, "Under Current Project Setting")]
        UnderCurrentProjectSetting
    }

    public enum ERegisterBalancing
    {
        [PropID(EPropAssoc.ISE, "No")]
        No
    }

    public enum ERegisterDuplicationMap
    {
        [PropID(EPropAssoc.ISE, "Off")]
        [PropID(EPropAssoc.MAP, "off")]
        Off,

        [PropID(EPropAssoc.ISE, "On")]
        [PropID(EPropAssoc.MAP, "on")]
        On
    }

    public enum EReportType
    {
        [PropID(EPropAssoc.ISE, "Verbose Report")]
        VerboseReport
    }

    public enum ESafeImplementation
    {
        [PropID(EPropAssoc.ISE, "No")]
        No
    }

    public enum ESecurity
    {
        [PropID(EPropAssoc.ISE, "Enable Readback and Reconfiguration")]
        EnableReadbackAndReconfiguration
    }

    public enum ETimingMode
    {
        [PropID(EPropAssoc.ISE, "Performance Evaluation")]
        PerformanceEvaluation
    }

    public enum ESourceType
    {
        [PropID(EPropAssoc.ISE, "HDL")]
        HDL
    }

    public enum EUseClockEnable
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum EUseDSPBlock
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum EUseRLOCConstraints
    {
        [PropID(EPropAssoc.ISE, "Yes")]
        [PropID(EPropAssoc.MAP, "all")]
        Yes,

        [PropID(EPropAssoc.ISE, "No")]
        [PropID(EPropAssoc.MAP, "off")]
        No,

        [PropID(EPropAssoc.MAP, "place")]
        ForPackingOnly
    }

    public enum EUseReset
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum EUserAccessRegisterValue
    {
        [PropID(EPropAssoc.ISE, "None")]
        None
    }

    public enum EVHDLStandard
    {
        [PropID(EPropAssoc.ISE, "VHDL-93")]
        VHDL93
    }

    public enum EWaitForDCIMatchVirtex5
    {
        [PropID(EPropAssoc.ISE, "Auto")]
        Auto
    }

    public enum EWaitForPLLLockVirtex6
    {
        [PropID(EPropAssoc.ISE, "NoWait")]
        NoWait
    }

    public enum EWatchdogTimerModeVirtex5
    {
        [PropID(EPropAssoc.ISE, "Off")]
        Off
    }

    public enum EXilinxProjectProperties
    {
        [PropID(EPropAssoc.CoreGen, "addpads")]
        [PropValueType(typeof(bool))]
        AddPads,

        [PropID(EPropAssoc.CoreGen, "asysymbol")]
        [PropValueType(typeof(bool), true)]
        ASYSymbol,

        [PropID(EPropAssoc.CoreGen, "createndf")]
        [PropValueType(typeof(bool))]
        CreateNDF,

        [PropID(EPropAssoc.CoreGenProj, "designflow")]
        [PropValueType(typeof(EDesignFlow))]
        DesignFlow,

        [PropID(EPropAssoc.CoreGen, "formalverification")]
        [PropValueType(typeof(bool))]
        FormalVerification,

        [PropID(EPropAssoc.CoreGen, "foundationsym")]
        [PropValueType(typeof(bool))]
        FoundationSym,

        [PropID(EPropAssoc.CoreGen, "implementationfiletype")]
        [PropValueType(typeof(EImplementationFileType))]
        ImplementationFileType,

        [PropID(EPropAssoc.ISE, "AES Initial Vector virtex6")]
        [PropValueType(typeof(string), "")]
        AESInitialVectorVirtex6,

        [PropID(EPropAssoc.ISE, "AES Key (Hex String) virtex6")]
        [PropValueType(typeof(string), "")]
        AESKeyVirtex6,

        [PropID(EPropAssoc.ISE, "Add I/O Buffers")]
        [PropValueType(typeof(bool), true)]
        AddIOBuffers,

        [PropID(EPropAssoc.ISE, "Allow Logic Optimization Across Hierarchy")]
        [PropValueType(typeof(bool), false)]
        AllowLogicOptimizationAcrossHierarchy,

        [PropID(EPropAssoc.ISE, "Allow SelectMAP Pins to Persist")]
        [PropValueType(typeof(bool), false)]
        AllowSelectMAPPinsToPersist,

        [PropID(EPropAssoc.ISE, "Allow Unexpanded Blocks")]
        [PropValueType(typeof(bool), false)]
        AllowUnexpandedBlocks,

        [PropID(EPropAssoc.ISE, "Allow Unmatched LOC Constraints")]
        [PropValueType(typeof(bool), false)]
        AllowUnmatchedLOCConstraints,

        [PropID(EPropAssoc.ISE, "Allow Unmatched Timing Group Constraints")]
        [PropValueType(typeof(bool), false)]
        AllowUnmatchedTimingGroupConstraints,

        [PropID(EPropAssoc.ISE, "Analysis Effort Level")]
        [PropValueType(typeof(EAnalysisEffortLevel))]
        AnalysisEffortLevel,

        [PropID(EPropAssoc.ISE, "Asynchronous To Synchronous")]
        [PropValueType(typeof(bool))]
        AsynchronousToSynchronous,

        [PropID(EPropAssoc.ISE, "Auto Implementation Compile Order")]
        [PropValueType(typeof(bool), true)]
        AutoImplementationCompileOrder,

        [PropID(EPropAssoc.ISE, "Auto Implementation Top")]
        [PropValueType(typeof(bool), true)]
        AutoImplementationTop,

        [PropID(EPropAssoc.ISE, "Automatic BRAM Packing")]
        [PropValueType(typeof(bool), false)]
        AutomaticBRAMPacking,

        [PropID(EPropAssoc.ISE, "Automatically Insert glbl Module in the Netlist")]
        [PropValueType(typeof(bool), true)]
        Auto_glbl,

        [PropID(EPropAssoc.ISE, "Automatically Run Generate Target PROM/ACE File")]
        [PropValueType(typeof(bool), false)]
        AutoGenerate_PROM_ACE,

        [PropID(EPropAssoc.ISE, "BPI Reads Per Page")]
        [PropValueType(typeof(int), 1)]
        BPIReadsPerPage,

        [PropID(EPropAssoc.ISE, "BRAM Utilization Ratio")]
        [PropValueType(typeof(int), 100)]
        BRAMUtilizationRatio,

        [PropID(EPropAssoc.ISE, "Bring Out Global Set/Reset Net as a Port")]
        [PropValueType(typeof(bool), false)]
        GlobalSetResetAsPort,

        [PropID(EPropAssoc.ISE, "Bring Out Global Tristate Net as a Port")]
        [PropValueType(typeof(bool), false)]
        GlobalTristateAsPort,

        [PropID(EPropAssoc.ISE, "Bus Delimiter")]
        [PropID(EPropAssoc.CoreGen, "BusFormat")]
        [PropID(EPropAssoc.CoreGenProj, "BusFormat")]
        [PropValueType(typeof(EBusFormat))]
        BusDelimiter,

        [PropID(EPropAssoc.ISE, "Case")]
        [PropValueType(typeof(ECase))]
        Case,

        [PropID(EPropAssoc.ISE, "Case Implementation Style")]
        [PropValueType(typeof(ECaseImplementationStyle))]
        CaseImplementationStyle,

        [PropID(EPropAssoc.ISE, "Change Device Speed To")]
        [PropValueType(typeof(ESpeedGrade))]
        ChangeDeviceSpeed,

        [PropID(EPropAssoc.ISE, "Change Device Speed To Post Trace")]
        [PropValueType(typeof(ESpeedGrade))]
        ChangeDeviceSpeedPostTrace,

        [PropID(EPropAssoc.ISE, "Combinatorial Logic Optimization")]
        [PropValueType(typeof(bool), false)]
        CombinatorialLogicOptimization,

        [PropID(EPropAssoc.ISE, "Compile EDK Simulation Library")]
        [PropValueType(typeof(bool), true)]
        CompileEDKSimulationLibrary,

        [PropID(EPropAssoc.ISE, "Compile SIMPRIM (Timing) Simulation Library")]
        [PropValueType(typeof(bool), true)]
        CompileSIMPRIMSimulationLibrary,

        [PropID(EPropAssoc.ISE, "Compile UNISIM (Functional) Simulation Library")]
        [PropValueType(typeof(bool), true)]
        CompileUNISIMSimulationLibrary,

        [PropID(EPropAssoc.ISE, "Compile XilinxCoreLib (CORE Generator) Simulation Library")]
        [PropValueType(typeof(bool), true)]
        CompileXilinxCoreLib,

        [PropID(EPropAssoc.ISE, "Compile for HDL Debugging")]
        [PropValueType(typeof(bool), true)]
        CompileForHDLDebugging,

        [PropID(EPropAssoc.ISE, "Configuration Clk (Configuration Pins)")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationClk,

        [PropID(EPropAssoc.ISE, "Configuration Pin Busy")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinBusy,

        [PropID(EPropAssoc.ISE, "Configuration Pin CS")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinCS,

        [PropID(EPropAssoc.ISE, "Configuration Pin DIn")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinDIn,

        [PropID(EPropAssoc.ISE, "Configuration Pin Done")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinDone,

        [PropID(EPropAssoc.ISE, "Configuration Pin HSWAPEN")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinHSWAPEN,

        [PropID(EPropAssoc.ISE, "Configuration Pin Init")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinInit,

        [PropID(EPropAssoc.ISE, "Configuration Pin M0")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinM0,

        [PropID(EPropAssoc.ISE, "Configuration Pin M1")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinM1,

        [PropID(EPropAssoc.ISE, "Configuration Pin M2")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinM2,

        [PropID(EPropAssoc.ISE, "Configuration Pin Program")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinProgram,

        [PropID(EPropAssoc.ISE, "Configuration Pin RdWr")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        ConfigurationPinRdWr,

        [PropID(EPropAssoc.ISE, "Configuration Rate virtex5")]
        [PropValueType(typeof(int), 2)]
        ConfigurationRateVirtex5,

        [PropID(EPropAssoc.ISE, "Correlate Output to Input Design")]
        [PropValueType(typeof(bool), false)]
        CorrelateOutputToInputDesign,

        [PropID(EPropAssoc.ISE, "Create ASCII Configuration File")]
        [PropValueType(typeof(bool), false)]
        CreateASCIIConfigurationFile,

        [PropID(EPropAssoc.ISE, "Create Binary Configuration File")]
        [PropValueType(typeof(bool), false)]
        CreateBinaryConfigurationFile,

        [PropID(EPropAssoc.ISE, "Create Bit File")]
        [PropValueType(typeof(bool), true)]
        CreateBitFile,

        [PropID(EPropAssoc.ISE, "Create I/O Pads from Ports")]
        [PropValueType(typeof(bool), false)]
        CreateIOPadsFromPorts,

        [PropID(EPropAssoc.ISE, "Create IEEE 1532 Configuration File")]
        [PropValueType(typeof(bool), false)]
        CreateIEEE1532ConfigurationFile,

        [PropID(EPropAssoc.ISE, "Create Logic Allocation File")]
        [PropValueType(typeof(bool), false)]
        CreateLogicAllocationFile,

        [PropID(EPropAssoc.ISE, "Create Mask File")]
        [PropValueType(typeof(bool), false)]
        CreateMaskFile,

        [PropID(EPropAssoc.ISE, "Create ReadBack Data Files")]
        [PropValueType(typeof(bool), false)]
        CreateReadBackDataFiles,

        [PropID(EPropAssoc.ISE, "Cross Clock Analysis")]
        [PropValueType(typeof(bool), false)]
        CrossClockAnalysis,

        [PropID(EPropAssoc.ISE, "Cycles for First BPI Page Read")]
        [PropValueType(typeof(int), 1)]
        CyclesForFirstBPIPageRead,

        [PropID(EPropAssoc.ISE, "DCI Update Mode")]
        [PropValueType(typeof(EDCIUpdateMode), EDCIUpdateMode.Quiet)]
        DCIUpdateMode,

        [PropID(EPropAssoc.ISE, "DSP Utilization Ratio")]
        [PropValueType(typeof(int), 100)]
        DSPUtilizationRatio,

        [PropID(EPropAssoc.ISE, "Delay Values To Be Read from SDF")]
        [PropValueType(typeof(EDelayValue), EDelayValue.SetupTime)]
        DelayValuesToBeReadFromSDF,

        [PropID(EPropAssoc.ISE, "Device")]
        [PropID(EPropAssoc.CoreGen, "device")]
        [PropID(EPropAssoc.CoreGenProj, "device")]
        [PropValueType(typeof(EDevice))]
        Device,

        [PropID(EPropAssoc.ISE, "Device Family")]
        [PropID(EPropAssoc.CoreGen, "devicefamily")]
        [PropID(EPropAssoc.CoreGenProj, "devicefamily")]
        [PropValueType(typeof(EDeviceFamily))]
        DeviceFamily,

        [PropID(EPropAssoc.ISE, "Device Speed Grade/Select ABS Minimum")]
        [PropValueType(typeof(ESpeedGrade))]
        DeviceSpeedGrade,

        [PropID(EPropAssoc.ISE, "Disable Detailed Package Model Insertion")]
        [PropValueType(typeof(bool), false)]
        DisableDetailedPackageModelInsertion,

        [PropID(EPropAssoc.ISE, "Disable JTAG Connection")]
        [PropValueType(typeof(bool), false)]
        DisableJTAGConnection,

        [PropID(EPropAssoc.ISE, "Do Not Escape Signal and Instance Names in Netlist")]
        [PropValueType(typeof(bool), false)]
        DontEscapeSignalAndInstanceNamesInNetlist,

        [PropID(EPropAssoc.ISE, "Done (Output Events)")]
        [PropValueType(typeof(EOutputEvents), EOutputEvents.Default_4)]
        DoneOutputEvents,

        [PropID(EPropAssoc.ISE, "Drive Done Pin High")]
        [PropValueType(typeof(bool), false)]
        DriveDonePinHigh,

        [PropID(EPropAssoc.ISE, "Enable BitStream Compression")]
        [PropValueType(typeof(bool), false)]
        EnableBitStreamCompression,

        [PropID(EPropAssoc.ISE, "Enable Cyclic Redundancy Checking (CRC)")]
        [PropValueType(typeof(bool), true)]
        EnableCRC,

        [PropID(EPropAssoc.ISE, "Enable Debugging of Serial Mode BitStream")]
        [PropValueType(typeof(bool), false)]
        EnableDebuggingOfSerialModeBitStream,

        [PropID(EPropAssoc.ISE, "Enable Internal Done Pipe")]
        [PropValueType(typeof(bool), false)]
        EnableInternalDonePipe,

        [PropID(EPropAssoc.ISE, "Enable Message Filtering")]
        [PropValueType(typeof(bool), false)]
        EnableMessageFiltering,

        [PropID(EPropAssoc.ISE, "Enable Multi-Threading")]
        [PropValueType(typeof(EEnableMultiThreading), EEnableMultiThreading.Off)]
        EnableMultiThreading,

        [PropID(EPropAssoc.ISE, "Enable Multi-Threading par virtex5")]
        [PropValueType(typeof(EEnableMultiThreading), EEnableMultiThreading.Off)]
        EnableMultiThreadingParVirtex5,

        [PropID(EPropAssoc.ISE, "Enable Outputs (Output Events)")]
        [PropValueType(typeof(EOutputEvents), EOutputEvents.Default_5)]
        EnableOutputs,

        [PropID(EPropAssoc.ISE, "Encrypt Bitstream")]
        [PropValueType(typeof(bool), false)]
        EncryptBitStream,

        [PropID(EPropAssoc.ISE, "Encrypt Bitstream virtex6")]
        [PropValueType(typeof(bool), false)]
        EncryptBitStreamVirtex6,

        [PropID(EPropAssoc.ISE, "Encrypt Key Select virtex6")]
        [PropValueType(typeof(EEncryptKeySelectVirtex6), EEncryptKeySelectVirtex6.BRAM)]
        EncryptKeySelectVirtex6,

        [PropID(EPropAssoc.ISE, "Equivalent Register Removal")]
        [PropValueType(typeof(bool), true)]
        EquivalentRegisterRemoval,

        [PropID(EPropAssoc.ISE, "Equivalent Register Removal XST")]
        [PropValueType(typeof(bool), true)]
        EquivalentRegisterRemovalXST,

        [PropID(EPropAssoc.ISE, "Evaluation Development Board")]
        [PropValueType(typeof(string), "None Specified")]
        EvaluationDevelopmentBoard,

        [PropID(EPropAssoc.ISE, "Exclude Compilation of Deprecated EDK Cores")]
        [PropValueType(typeof(bool), true)]
        ExcludeCompilationofDeprecatedEDKCores,

        [PropID(EPropAssoc.ISE, "Exclude Compilation of EDK Sub-Libraries")]
        [PropValueType(typeof(bool), false)]
        ExcludeCompilationOfEDKSubLibraries,

        [PropID(EPropAssoc.ISE, "Extra Cost Tables Map virtex6")]
        [PropValueType(typeof(int), 0)]
        ExtraCostTablesMapVirtex6,

        [PropID(EPropAssoc.ISE, "Extra Effort (Highest PAR level only)")]
        [PropValueType(typeof(EExtraEffort), EExtraEffort.None)]
        ExtraEffort,

        [PropID(EPropAssoc.ISE, "FPGA Start-Up Clock")]
        [PropValueType(typeof(EStartupClock), EStartupClock.CCLK)]
        FPGAStartUpClock,

        [PropID(EPropAssoc.ISE, "FSM Encoding Algorithm")]
        [PropValueType(typeof(EFSMEncodingAlgorithm), EFSMEncodingAlgorithm.Auto)]
        FSMEncodingAlgorithm,

        [PropID(EPropAssoc.ISE, "FSM Style")]
        [PropValueType(typeof(EFSMStyle), EFSMStyle.LUT)]
        FSMStyle,

        [PropID(EPropAssoc.ISE, "Fallback Reconfiguration")]
        [PropValueType(typeof(EFallbackReconfiguration), EFallbackReconfiguration.Enable)]
        FallbackReconfiguration,

        [PropID(EPropAssoc.ISE, "Filter Files From Compile Order")]
        [PropValueType(typeof(bool), true)]
        FilterFilesFromCompileOrder,

        [PropID(EPropAssoc.ISE, "Flatten Output Netlist")]
        [PropValueType(typeof(bool), false)]
        FlattenOutputNetlist,

        [PropID(EPropAssoc.ISE, "Functional Model Target Language ArchWiz")]
        [PropValueType(typeof(EHDL), EHDL.Verilog)]
        FunctionalModelTargetLanguageArchWiz,

        [PropID(EPropAssoc.ISE, "Functional Model Target Language Coregen")]
        [PropValueType(typeof(EHDL), EHDL.Verilog)]
        FunctionalModelTargetLanguageCoreGen,

        [PropID(EPropAssoc.ISE, "Functional Model Target Language Schematic")]
        [PropValueType(typeof(EHDL), EHDL.Verilog)]
        FunctionalModelTargetLanguageSchematic,

        [PropID(EPropAssoc.ISE, "Generate Architecture Only (No Entity Declaration)")]
        [PropValueType(typeof(bool), false)]
        GenerateArchitectureOnly,

        [PropID(EPropAssoc.ISE, "Generate Asynchronous Delay Report")]
        [PropValueType(typeof(bool), false)]
        GenerateAsynchronousDelayReport,

        [PropID(EPropAssoc.ISE, "Generate Clock Region Report")]
        [PropValueType(typeof(bool), false)]
        GenerateClockRegionReport,

        [PropID(EPropAssoc.ISE, "Generate Constraints Interaction Report")]
        [PropValueType(typeof(bool), false)]
        GenerateConstraintsInteractionReport,

        [PropID(EPropAssoc.ISE, "Generate Constraints Interaction Report Post Trace")]
        [PropValueType(typeof(bool), false)]
        GenerateConstraintsInteractionReportPostTrace,

        [PropID(EPropAssoc.ISE, "Generate Datasheet Section")]
        [PropValueType(typeof(bool), true)]
        GenerateDatasheetSection,

        [PropID(EPropAssoc.ISE, "Generate Datasheet Section Post Trace")]
        [PropValueType(typeof(bool), true)]
        GenerateDatasheetSectionPostTrace,

        [PropID(EPropAssoc.ISE, "Generate Detailed MAP Report")]
        [PropValueType(typeof(bool), false)]
        GenerateDetailedMAPReport,

        [PropID(EPropAssoc.ISE, "Generate Multiple Hierarchical Netlist Files")]
        [PropValueType(typeof(bool), false)]
        GenerateMultipleHierarchicalNetlistFiles,

        [PropID(EPropAssoc.ISE, "Generate Post-Place & Route Power Report")]
        [PropValueType(typeof(bool), false)]
        GeneratePostPlaceAndRoutePowerReport,

        [PropID(EPropAssoc.ISE, "Generate Post-Place & Route Simulation Model")]
        [PropValueType(typeof(bool), false)]
        GeneratePostPlaceAndRouteSimulationModel,

        [PropID(EPropAssoc.ISE, "Generate RTL Schematic")]
        [PropValueType(typeof(EGenerateRTLSchematic), EGenerateRTLSchematic.Yes)]
        GenerateRTLSchematic,

        [PropID(EPropAssoc.ISE, "Generate SAIF File for Power Optimization/Estimation Par")]
        [PropValueType(typeof(bool), false)]
        GenerateSAIFFile,

        [PropID(EPropAssoc.ISE, "Generate Testbench File")]
        [PropValueType(typeof(bool), false)]
        GenerateTestbenchFile,

        [PropID(EPropAssoc.ISE, "Generate Timegroups Section")]
        [PropValueType(typeof(bool), false)]
        GenerateTimegroupsSection,

        [PropID(EPropAssoc.ISE, "Generate Timegroups Section Post Trace")]
        [PropValueType(typeof(bool), false)]
        GenerateTimegroupsSectionPostTrace,

        [PropID(EPropAssoc.ISE, "Generics, Parameters")]
        [PropValueType(typeof(string), "")]
        GenericsParameters,

        [PropID(EPropAssoc.ISE, "Global Optimization Goal")]
        [PropValueType(typeof(EGlobalOptimizationGoal), EGlobalOptimizationGoal.AllClockNets)]
        GlobalOptimizationGoal,

        [PropID(EPropAssoc.ISE, "Global Optimization map virtex5")]
        [PropValueType(typeof(EGlobalOptimizationMapVirtex5), EGlobalOptimizationMapVirtex5.Off)]
        GlobalOptimizationMapVirtex5,

        [PropID(EPropAssoc.ISE, "Global Set/Reset Port Name")]
        [PropValueType(typeof(string), "GSR_PORT")]
        GlobalSetResetPortName,

        [PropID(EPropAssoc.ISE, "Global Tristate Port Name")]
        [PropValueType(typeof(string), "GTS_PORT")]
        GlobalTristatePortName,

        [PropID(EPropAssoc.ISE, "HMAC Key (Hex String)")]
        [PropValueType(typeof(string), "")]
        HMACKey,

        [PropID(EPropAssoc.ISE, "Hierarchy Separator")]
        [PropValueType(typeof(string), "/")]
        HierarchySeparator,

        [PropID(EPropAssoc.ISE, "ISim UUT Instance Name")]
        [PropValueType(typeof(string), "UUT")]
        ISimUUTInstanceName,

        [PropID(EPropAssoc.ISE, "Ignore User Timing Constraints Map")]
        [PropValueType(typeof(bool), false)]
        IgnoreUserTimingConstraintsMap,

        [PropID(EPropAssoc.ISE, "Ignore User Timing Constraints Par")]
        [PropValueType(typeof(bool), false)]
        IgnoreUserTimingConstraintsPar,

        [PropID(EPropAssoc.ISE, "Implementation Top")]
        [PropValueType(typeof(string), "")]
        ImplementationTop,

        [PropID(EPropAssoc.ISE, "Implementation Top Instance Path")]
        [PropValueType(typeof(string), "")]
        ImplementationTopInstancePath,

        [PropID(EPropAssoc.ISE, "Implementation Top File")]
        [PropValueType(typeof(string), "")]
        ImplementationTopFile,

        [PropID(EPropAssoc.ISE, "Include 'uselib Directive in Verilog File")]
        [PropValueType(typeof(bool), false)]
        IncludeUselibDirectiveInVerilogFile,

        [PropID(EPropAssoc.ISE, "Include SIMPRIM Models in Verilog File")]
        [PropValueType(typeof(bool), false)]
        IncludeSIMPRIMModelsInVerilogFile,

        [PropID(EPropAssoc.ISE, "Include UNISIM Models in Verilog File")]
        [PropValueType(typeof(bool), false)]
        IncludeUNISIMModelsInVerilogFile,

        [PropID(EPropAssoc.ISE, "Include sdf_annotate task in Verilog File")]
        [PropValueType(typeof(bool), true)]
        Include_sdf_annotate_TaskInVerilogFile,

        [PropID(EPropAssoc.ISE, "Incremental Compilation")]
        [PropValueType(typeof(bool), true)]
        IncrementalCompilation,

        [PropID(EPropAssoc.ISE, "Insert Buffers to Prevent Pulse Swallowing")]
        [PropValueType(typeof(bool), true)]
        InsertBuffersToPreventPulseSwallowing,

        [PropID(EPropAssoc.ISE, "Instantiation Template Target Language Xps")]
        [PropValueType(typeof(EHDL), EHDL.Verilog)]
        InstantiationTemplateTargetLanguageXps,

        [PropID(EPropAssoc.ISE, "JTAG Pin TCK")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        JTAGPinTCK,

        [PropID(EPropAssoc.ISE, "JTAG Pin TDI")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        JTAGPinTDI,

        [PropID(EPropAssoc.ISE, "JTAG Pin TDO")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        JTAGPinTDO,

        [PropID(EPropAssoc.ISE, "JTAG Pin TMS")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullUp)]
        JTAGPinTMS,

        [PropID(EPropAssoc.ISE, "JTAG to System Monitor Connection")]
        [PropValueType(typeof(EJTAGToSystemMonitorConnection), EJTAGToSystemMonitorConnection.Enable)]
        JTAGToSystemMonitorConnection,

        [PropID(EPropAssoc.ISE, "Keep Hierarchy")]
        [PropValueType(typeof(EKeepHierarchy), EKeepHierarchy.No)]
        KeepHierarchy,

        [PropID(EPropAssoc.ISE, "LUT Combining Map")]
        [PropValueType(typeof(ELUTCombining), ELUTCombining.Off)]
        LUTCombiningMap,

        [PropID(EPropAssoc.ISE, "LUT Combining Xst")]
        [PropValueType(typeof(ELUTCombining), ELUTCombining.Auto)]
        LUTCombiningPar,

        [PropID(EPropAssoc.ISE, "Language")]
        [PropValueType(typeof(ELanguage), ELanguage.All)]
        Language,

        [PropID(EPropAssoc.ISE, "Launch SDK after Export")]
        [PropValueType(typeof(bool), true)]
        LaunchSDKAfterExport,

        [PropID(EPropAssoc.ISE, "Library for Verilog Sources")]
        [PropValueType(typeof(string), "")]
        LibraryForVerilogSources,

        [PropID(EPropAssoc.ISE, "Load glbl")]
        [PropValueType(typeof(bool), true)]
        Load_glbl,

        [PropID(EPropAssoc.ISE, "Manual Implementation Compile Order")]
        [PropValueType(typeof(bool), false)]
        ManualImplementationCompileOrder,

        [PropID(EPropAssoc.ISE, "Map Slice Logic into Unused Block RAMs")]
        [PropValueType(typeof(bool), false)]
        MapSliceLogicIntoUnusedBlockRAMs,

        [PropID(EPropAssoc.ISE, "Max Fanout")]
        [PropValueType(typeof(int), 100000)]
        MaxFanout,

        [PropID(EPropAssoc.ISE, "Maximum Compression")]
        [PropValueType(typeof(bool), false)]
        MaximumCompression,

        [PropID(EPropAssoc.ISE, "Maximum Number of Lines in Report")]
        [PropValueType(typeof(int), 1000)]
        MaximumNumberOfLinesInReport,

        [PropID(EPropAssoc.ISE, "Maximum Signal Name Length")]
        [PropValueType(typeof(int), 20)]
        MaximumSignalNameLength,

        [PropID(EPropAssoc.ISE, "Move First Flip-Flop Stage")]
        [PropValueType(typeof(bool), true)]
        MoveFirstFlipFlopStage,

        [PropID(EPropAssoc.ISE, "Move Last Flip-Flop Stage")]
        [PropValueType(typeof(bool), true)]
        MoveLastFlipFlopStage,

        [PropID(EPropAssoc.ISE, "Mux Extraction")]
        [PropValueType(typeof(EMUXExtraction), EMUXExtraction.Yes)]
        MuxExtraction,

        [PropID(EPropAssoc.ISE, "Netlist Hierarchy")]
        [PropValueType(typeof(ENetlistHierarchy), ENetlistHierarchy.AsOptimized)]
        NetlistHierarchy,

        [PropID(EPropAssoc.ISE, "Netlist Translation Type")]
        [PropValueType(typeof(ENetlistTranslationType), ENetlistTranslationType.Timestamp)]
        NetlistTranslationType,

        [PropID(EPropAssoc.ISE, "Number of Clock Buffers")]
        [PropValueType(typeof(int), 32)]
        NumberOfClockBuffers,

        [PropID(EPropAssoc.ISE, "Number of Paths in Error/Verbose Report")]
        [PropValueType(typeof(int), 3)]
        NumberOfPathsInErrorVerboseReport,

        [PropID(EPropAssoc.ISE, "Number of Paths in Error/Verbose Report Post Trace")]
        [PropValueType(typeof(int), 3)]
        NumberOfPathsInErrorVerboseReportPostTrace,

        [PropID(EPropAssoc.ISE, "Optimization Effort")]
        [PropValueType(typeof(EOptimizationEffort), EOptimizationEffort.Normal)]
        OptimizationEffort,

        [PropID(EPropAssoc.ISE, "Optimization Effort virtex6")]
        [PropValueType(typeof(EOptimizationEffort), EOptimizationEffort.Normal)]
        OptimizationEffortVirtex6,

        [PropID(EPropAssoc.ISE, "Optimization Goal")]
        [PropValueType(typeof(EOptimizationGoal), EOptimizationGoal.Speed)]
        OptimizationGoal,

        [PropID(EPropAssoc.ISE, "Optimize Instantiated Primitives")]
        [PropValueType(typeof(bool), false)]
        OptimizeInstantiatedPrimitives,

        [PropID(EPropAssoc.ISE, "Other Bitgen Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherBitgenCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Other Compiler Options")]
        [PropValueType(typeof(string), "")]
        OtherCompilerOptions,

        [PropID(EPropAssoc.ISE, "Other Compiler Options Fit")]
        [PropValueType(typeof(string), "")]
        OtherCompilerOptionsFit,

        [PropID(EPropAssoc.ISE, "Other Compiler Options Map")]
        [PropValueType(typeof(string), "")]
        OtherCompilerOptionsMap,

        [PropID(EPropAssoc.ISE, "Other Compiler Options Par")]
        [PropValueType(typeof(string), "")]
        OtherCompilerOptionsPar,

        [PropID(EPropAssoc.ISE, "Other Compiler Options Translate")]
        [PropValueType(typeof(string), "")]
        OtherCompilerOptionsTranslate,

        [PropID(EPropAssoc.ISE, "Other Compxlib Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherCompxlibCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Other Map Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherMapCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Other NETGEN Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherNETGENCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Other Ngdbuild Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherNgdbuildCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Other Place & Route Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherPlaceAndRouteCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Other Simulator Commands Behavioral")]
        [PropValueType(typeof(string), "")]
        OtherSimulatorCommandsBehavioral,

        [PropID(EPropAssoc.ISE, "Other Simulator Commands Post-Map")]
        [PropValueType(typeof(string), "")]
        OtherSimulatorCommandsPostMap,

        [PropID(EPropAssoc.ISE, "Other Simulator Commands Post-Route")]
        [PropValueType(typeof(string), "")]
        OtherSimulatorCommandsPostRoute,

        [PropID(EPropAssoc.ISE, "Other Simulator Commands Post-Translate")]
        [PropValueType(typeof(string), "")]
        OtherSimulatorCommandsPostTranslate,

        [PropID(EPropAssoc.ISE, "Other XPWR Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherXPWRCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Other XST Command Line Options")]
        [PropValueType(typeof(string), "")]
        OtherXSTCommandLineOptions,

        [PropID(EPropAssoc.ISE, "Output Extended Identifiers")]
        [PropValueType(typeof(bool), false)]
        OutputExtendedIdentifiers,

        [PropID(EPropAssoc.ISE, "Output File Name")]
        [PropValueType(typeof(string), "")]
        OutputFileName,

        [PropID(EPropAssoc.ISE, "Overwrite Compiled Libraries")]
        [PropValueType(typeof(bool), false)]
        OverwriteCompiledLibraries,

        [PropID(EPropAssoc.ISE, "Pack I/O Registers into IOBs")]
        [PropValueType(typeof(EPackIORegistersIntoIOBs), EPackIORegistersIntoIOBs.Auto)]
        PackIORegistersIntoIOBs,

        [PropID(EPropAssoc.ISE, "Pack I/O Registers/Latches into IOBs")]
        [PropValueType(typeof(EPackIORegistersIntoIOBs), EPackIORegistersIntoIOBs.Off)]
        PackIORegisters_LatchesIntoIOBs,

        [PropID(EPropAssoc.ISE, "Package")]
        [PropID(EPropAssoc.CoreGen, "package")]
        [PropID(EPropAssoc.CoreGenProj, "package")]
        [PropValueType(typeof(EPackage))]
        Package,

        [PropID(EPropAssoc.ISE, "Perform Advanced Analysis")]
        [PropValueType(typeof(bool), false)]
        PerformAdvancedAnalysis,

        [PropID(EPropAssoc.ISE, "Perform Advanced Analysis Post Trace")]
        [PropValueType(typeof(bool), false)]
        PerformAdvancedAnalysisPostTrace,

        [PropID(EPropAssoc.ISE, "Place & Route Effort Level (Overall)")]
        [PropValueType(typeof(EPlaceAndRouteEffortLevel), EPlaceAndRouteEffortLevel.High)]
        PlaceAndRouteEffortLevel,

        [PropID(EPropAssoc.ISE, "Place And Route Mode")]
        [PropValueType(typeof(EPlaceAndRouteMode), EPlaceAndRouteMode.Normal)]
        PlaceAndRouteMode,

        [PropID(EPropAssoc.ISE, "Placer Effort Level Map")]
        [PropValueType(typeof(EPlacerEffortLevelMap), EPlacerEffortLevelMap.High)]
        PlacerEffortLevelMap,

        [PropID(EPropAssoc.ISE, "Placer Extra Effort Map")]
        [PropValueType(typeof(EPlacerExtraEffortMap), EPlacerExtraEffortMap.None)]
        PlacerExtraEffortMap,

        [PropID(EPropAssoc.ISE, "Port to be used")]
        [PropValueType(typeof(string), "Auto - default")]
        PortToBeUsed,

        [PropID(EPropAssoc.ISE, "Post Map Simulation Model Name")]
        [PropValueType(typeof(string), "_map.v")]
        PostMapSimulationModelName,

        [PropID(EPropAssoc.ISE, "Post Place & Route Simulation Model Name")]
        [PropValueType(typeof(string), "_timesim.v")]
        PostPlaceAndRouteSimulationModelName,

        [PropID(EPropAssoc.ISE, "Post Synthesis Simulation Model Name")]
        [PropValueType(typeof(string), "_synthesis.v")]
        PostSynthesisSimulationModelName,

        [PropID(EPropAssoc.ISE, "Post Translate Simulation Model Name")]
        [PropValueType(typeof(string), "_translate.v")]
        PostTranslateSimulationModelName,

        [PropID(EPropAssoc.ISE, "Power Down Device if Over Safe Temperature")]
        [PropValueType(typeof(bool), false)]
        PowerDownDeviceIfOverSafeTemperature,

        [PropID(EPropAssoc.ISE, "Power Reduction Map virtex6")]
        [PropValueType(typeof(EPowerReductionMapVirtex6), EPowerReductionMapVirtex6.Off)]
        PowerReductionMapVirtex6,

        [PropID(EPropAssoc.ISE, "Power Reduction Par")]
        [PropValueType(typeof(bool), false)]
        PowerReductionPar,

        [PropID(EPropAssoc.ISE, "Power Reduction Xst")]
        [PropValueType(typeof(bool), false)]
        PowerReductionXst,

        [PropID(EPropAssoc.ISE, "Preferred Language")]
        [PropID(EPropAssoc.CoreGen, "designentry")]
        [PropID(EPropAssoc.CoreGenProj, "designentry")]
        [PropValueType(typeof(EHDL))]
        PreferredLanguage,

        [PropID(EPropAssoc.ISE, "Produce Verbose Report")]
        [PropValueType(typeof(bool), false)]
        ProduceVerboseReport,

        [PropID(EPropAssoc.ISE, "Project Description")]
        [PropValueType(typeof(string), "")]
        ProjectDescription,

        [PropID(EPropAssoc.ISE, "Property Specification in Project File")]
        [PropValueType(typeof(EPropertySpecification), EPropertySpecification.StoreAllValues)]
        PropertySpecificationInProjectFile,

        [PropID(EPropAssoc.ISE, "RAM Extraction")]
        [PropValueType(typeof(bool), true)]
        RAMExtraction,

        [PropID(EPropAssoc.ISE, "RAM Style")]
        [PropValueType(typeof(ERAMStyle), ERAMStyle.Auto)]
        RAMStyle,

        [PropID(EPropAssoc.ISE, "ROM Extraction")]
        [PropValueType(typeof(bool), true)]
        ROMExtraction,

        [PropID(EPropAssoc.ISE, "ROM Style")]
        [PropValueType(typeof(EROMStyle), EROMStyle.Auto)]
        ROMStyle,

        [PropID(EPropAssoc.ISE, "Read Cores")]
        [PropValueType(typeof(bool), true)]
        ReadCores,

        [PropID(EPropAssoc.ISE, "Reduce Control Sets")]
        [PropValueType(typeof(EReduceControlSets), EReduceControlSets.Auto)]
        ReduceControlSets,

        [PropID(EPropAssoc.ISE, "Regenerate Core")]
        [PropValueType(typeof(ERegenerateCore), ERegenerateCore.UnderCurrentProjectSetting)]
        RegenerateCore,

        [PropID(EPropAssoc.ISE, "Register Balancing")]
        [PropValueType(typeof(ERegisterBalancing), ERegisterBalancing.No)]
        RegisterBalancing,

        [PropID(EPropAssoc.ISE, "Register Duplication Map")]
        [PropValueType(typeof(ERegisterDuplicationMap), ERegisterDuplicationMap.Off)]
        RegisterDuplicationMap,

        [PropID(EPropAssoc.ISE, "Register Duplication Xst")]
        [PropValueType(typeof(bool), true)]
        RegisterDuplicationXst,

        [PropID(EPropAssoc.ISE, "Register Ordering virtex6")]
        [PropValueType(typeof(int), 4)]
        RegisterOrderingVirtex6,

        [PropID(EPropAssoc.ISE, "Release Write Enable (Output Events)")]
        [PropValueType(typeof(EOutputEvents), EOutputEvents.Default_6)]
        ReleaseWriteEnable,

        [PropID(EPropAssoc.ISE, "Rename Design Instance in Testbench File to")]
        [PropValueType(typeof(string), "UUT")]
        RenameDesignInstanceInTestbenchFileTo,

        [PropID(EPropAssoc.ISE, "Rename Top Level Architecture To")]
        [PropValueType(typeof(string), "Structure")]
        RenameTopLevelArchitectureTo,

        [PropID(EPropAssoc.ISE, "Rename Top Level Entity to")]
        [PropValueType(typeof(string), "")]
        RenameTopLevelEntityTo,

        [PropID(EPropAssoc.ISE, "Rename Top Level Module To")]
        [PropValueType(typeof(string), "")]
        RenameTopLevelModuleTo,

        [PropID(EPropAssoc.ISE, "Report Fastest Path(s) in Each Constraint")]
        [PropValueType(typeof(bool), true)]
        ReportFastestPaths,

        [PropID(EPropAssoc.ISE, "Report Fastest Path(s) in Each Constraint Post Trace")]
        [PropValueType(typeof(bool), true)]
        ReportFastestPathsPostTrace,

        [PropID(EPropAssoc.ISE, "Report Paths by Endpoint")]
        [PropValueType(typeof(int), 3)]
        ReportPathsByEndpoint,

        [PropID(EPropAssoc.ISE, "Report Paths by Endpoint Post Trace")]
        [PropValueType(typeof(int), 3)]
        ReportPathsByEndpointPostTrace,

        [PropID(EPropAssoc.ISE, "Report Type")]
        [PropValueType(typeof(EReportType), EReportType.VerboseReport)]
        ReportType,

        [PropID(EPropAssoc.ISE, "Report Type Post Trace")]
        [PropValueType(typeof(EReportType), EReportType.VerboseReport)]
        ReportTypePostTrace,

        [PropID(EPropAssoc.ISE, "Report Unconstrained Paths")]
        [PropValueType(typeof(string), "")]
        ReportUnconstrainedPaths,

        [PropID(EPropAssoc.ISE, "Report Unconstrained Paths Post Trace")]
        [PropValueType(typeof(string), "")]
        ReportUnconstrainedPathsPostTrace,

        [PropID(EPropAssoc.ISE, "Reset On Configuration Pulse Width")]
        [PropValueType(typeof(int), 100)]
        ResetOnConfigurationPulseWidth,

        [PropID(EPropAssoc.ISE, "Resource Sharing")]
        [PropValueType(typeof(bool), true)]
        ResourceSharing,

        [PropID(EPropAssoc.ISE, "Retain Hierarchy")]
        [PropValueType(typeof(bool), true)]
        RetainHierarchy,

        [PropID(EPropAssoc.ISE, "Run Design Rules Checker (DRC)")]
        [PropValueType(typeof(bool), true)]
        RunDRC,

        [PropID(EPropAssoc.ISE, "Run for Specified Time")]
        [PropValueType(typeof(bool), true)]
        RunForSpecifiedTime,

        [PropID(EPropAssoc.ISE, "Run for Specified Time Map")]
        [PropValueType(typeof(bool), true)]
        RunForSpecifiedTimeMap,

        [PropID(EPropAssoc.ISE, "Run for Specified Time Par")]
        [PropValueType(typeof(bool), true)]
        RunForSpecifiedTimePar,

        [PropID(EPropAssoc.ISE, "Run for Specified Time Translate")]
        [PropValueType(typeof(bool), true)]
        RunForSpecifiedTimeTranslate,

        [PropID(EPropAssoc.ISE, "Safe Implementation")]
        [PropValueType(typeof(ESafeImplementation), ESafeImplementation.No)]
        SafeImplementation,

        [PropID(EPropAssoc.ISE, "Security")]
        [PropValueType(typeof(ESecurity), ESecurity.EnableReadbackAndReconfiguration)]
        Security,

        [PropID(EPropAssoc.ISE, "Selected Simulation Root Source Node Behavioral")]
        [PropValueType(typeof(string), "")]
        SelectedSimulationRootSourceNodeBehavioral,

        [PropID(EPropAssoc.ISE, "Selected Simulation Root Source Node Post-Map")]
        [PropValueType(typeof(string), "")]
        SelectedSimulationRootSourceNodePostMap,

        [PropID(EPropAssoc.ISE, "Selected Simulation Root Source Node Post-Route")]
        [PropValueType(typeof(string), "")]
        SelectedSimulationRootSourceNodePostRoute,

        [PropID(EPropAssoc.ISE, "Selected Simulation Root Source Node Post-Translate")]
        [PropValueType(typeof(string), "")]
        SelectedSimulationRootSourceNodePostTranslate,

        [PropID(EPropAssoc.ISE, "Selected Simulation Source Node")]
        [PropValueType(typeof(string), "UUT")]
        SelectedSimulationSourceNode,

        [PropID(EPropAssoc.ISE, "Shift Register Extraction")]
        [PropValueType(typeof(bool), true)]
        ShiftRegisterExtraction,

        [PropID(EPropAssoc.ISE, "Shift Register Minimum Size virtex6")]
        [PropValueType(typeof(int), 2)]
        ShiftRegisterMinimumSizeVirtex6,

        [PropID(EPropAssoc.ISE, "Show All Models")]
        [PropValueType(typeof(bool), false)]
        ShowAllModels,

        [PropID(EPropAssoc.ISE, "Simulation Model Target")]
        [PropValueType(typeof(EHDL), EHDL.Verilog)]
        SimulationModelTarget,

        [PropID(EPropAssoc.ISE, "Simulation Run Time ISim")]
        [PropValueType(typeof(string), "1000 ns")]
        SimulationRunTimeISim,

        [PropID(EPropAssoc.ISE, "Simulation Run Time Map")]
        [PropValueType(typeof(string), "1000 ns")]
        SimulationRunTimeMap,

        [PropID(EPropAssoc.ISE, "Simulation Run Time Par")]
        [PropValueType(typeof(string), "1000 ns")]
        SimulationRunTimePar,

        [PropID(EPropAssoc.ISE, "Simulation Run Time Translate")]
        [PropValueType(typeof(string), "1000 ns")]
        SimulationRunTimeTranslate,

        [PropID(EPropAssoc.ISE, "Simulator")]
        [PropValueType(typeof(string), "ISim (VHDL/Verilog)")]
        Simulator,

        [PropID(EPropAssoc.ISE, "Slice Utilization Ratio")]
        [PropValueType(typeof(int), 100)]
        SliceUtilizationRatio,

        [PropID(EPropAssoc.ISE, "Specify 'define Macro Name and Value")]
        [PropValueType(typeof(string), "")]
        Specify_defineMacroNameAndValue,

        [PropID(EPropAssoc.ISE, "Specify Top Level Instance Names Behavioral")]
        [PropValueType(typeof(string), "Default")]
        SpecifyTopLevelInstanceNamesBehavioral,

        [PropID(EPropAssoc.ISE, "Specify Top Level Instance Names Post-Map")]
        [PropValueType(typeof(string), "Default")]
        SpecifyTopLevelInstanceNamesPostMap,

        [PropID(EPropAssoc.ISE, "Specify Top Level Instance Names Post-Route")]
        [PropValueType(typeof(string), "Default")]
        SpecifyTopLevelInstanceNamesPostRoute,

        [PropID(EPropAssoc.ISE, "Specify Top Level Instance Names Post-Translate")]
        [PropValueType(typeof(string), "Default")]
        SpecifyTopLevelInstanceNamesPostTranslate,

        [PropID(EPropAssoc.ISE, "Speed Grade")]
        [PropID(EPropAssoc.CoreGen, "speedgrade")]
        [PropID(EPropAssoc.CoreGenProj, "speedgrade")]
        [PropValueType(typeof(ESpeedGrade))]
        SpeedGrade,

        [PropID(EPropAssoc.ISE, "Starting Address for Fallback Configuration virtex6")]
        [PropValueType(typeof(string), "None")]
        StartingAddressForFallbackConfigurationVirtex6,

        [PropID(EPropAssoc.ISE, "Starting Placer Cost Table (1-100)")]
        [PropValueType(typeof(int), 1)]
        StartingPlacerCostTable,

        [PropID(EPropAssoc.ISE, "Synthesis Tool")]
        [PropValueType(typeof(string), "XST (VHDL/Verilog)")]
        SynthesisTool,

        [PropID(EPropAssoc.ISE, "Target Simulator")]
        [PropValueType(typeof(string), "Modelsim-SE Mixed")]
        TargetSimulator,

        [PropID(EPropAssoc.ISE, "Timing Mode Map")]
        [PropValueType(typeof(ETimingMode), ETimingMode.PerformanceEvaluation)]
        TimingModeMap,

        [PropID(EPropAssoc.ISE, "Timing Mode Par")]
        [PropValueType(typeof(ETimingMode), ETimingMode.PerformanceEvaluation)]
        TimingModePar,

        [PropID(EPropAssoc.ISE, "Top-Level Module Name in Output Netlist")]
        [PropValueType(typeof(string), "")]
        TopLevelModuleNameInOutputNetlist,

        [PropID(EPropAssoc.ISE, "Top-Level Source Type")]
        [PropValueType(typeof(ESourceType), ESourceType.HDL)]
        TopLevelSourceType,

        [PropID(EPropAssoc.ISE, "Trim Unconnected Signals")]
        [PropValueType(typeof(bool), true)]
        TrimUnconnectedSignals,

        [PropID(EPropAssoc.ISE, "Tristate On Configuration Pulse Width")]
        [PropValueType(typeof(int), 0)]
        TristateOnConfigurationPulseWidth,

        [PropID(EPropAssoc.ISE, "Unused IOB Pins")]
        [PropValueType(typeof(EPinConfig), EPinConfig.PullDown)]
        UnusedIOBPins,

        [PropID(EPropAssoc.ISE, "Use 64-bit PlanAhead on 64-bit Systems")]
        [PropValueType(typeof(bool), true)]
        Use64bitPlanAheadOn64bitSystems,

        [PropID(EPropAssoc.ISE, "Use Clock Enable")]
        [PropValueType(typeof(EUseClockEnable), EUseClockEnable.Auto)]
        UseClockEnable,

        [PropID(EPropAssoc.ISE, "Use Custom Project File Behavioral")]
        [PropValueType(typeof(bool), false)]
        UseCustomProjectFileBehavioral,

        [PropID(EPropAssoc.ISE, "Use Custom Project File Post-Map")]
        [PropValueType(typeof(bool), false)]
        UseCustomProjectFilePostMap,

        [PropID(EPropAssoc.ISE, "Use Custom Project File Post-Route")]
        [PropValueType(typeof(bool), false)]
        UseCustomProjectFilePostRoute,

        [PropID(EPropAssoc.ISE, "Use Custom Project File Post-Translate")]
        [PropValueType(typeof(bool), false)]
        UseCustomProjectFilePostTranslate,

        [PropID(EPropAssoc.ISE, "Use Custom Simulation Command File Behavioral")]
        [PropValueType(typeof(bool), false)]
        UseCustomSimulationCommandFileBehavioral,

        [PropID(EPropAssoc.ISE, "Use Custom Simulation Command File Map")]
        [PropValueType(typeof(bool), false)]
        UseCustomSimulationCommandFileMap,

        [PropID(EPropAssoc.ISE, "Use Custom Simulation Command File Par")]
        [PropValueType(typeof(bool), false)]
        UseCustomSimulationCommandFilePar,

        [PropID(EPropAssoc.ISE, "Use Custom Simulation Command File Translate")]
        [PropValueType(typeof(bool), false)]
        UseCustomSimulationCommandFileTranslate,

        [PropID(EPropAssoc.ISE, "Use Custom Waveform Configuration File Behav")]
        [PropValueType(typeof(bool), false)]
        UseCustomWaveformConfigurationFileBehav,

        [PropID(EPropAssoc.ISE, "Use Custom Waveform Configuration File Map")]
        [PropValueType(typeof(bool), false)]
        UseCustomWaveformConfigurationFileMap,

        [PropID(EPropAssoc.ISE, "Use Custom Waveform Configuration File Par")]
        [PropValueType(typeof(bool), false)]
        UseCustomWaveformConfigurationFilePar,

        [PropID(EPropAssoc.ISE, "Use Custom Waveform Configuration File Translate")]
        [PropValueType(typeof(bool), false)]
        UseCustomWaveformConfigurationFileTranslate,

        [PropID(EPropAssoc.ISE, "Use DSP Block")]
        [PropValueType(typeof(EUseDSPBlock), EUseDSPBlock.Auto)]
        UseDSPBlock,

        [PropID(EPropAssoc.ISE, "Use LOC Constraints")]
        [PropValueType(typeof(bool), true)]
        UseLOCConstraints,

        [PropID(EPropAssoc.ISE, "Use RLOC Constraints")]
        [PropValueType(typeof(EUseRLOCConstraints), EUseRLOCConstraints.Yes)]
        UseRLOCConstraints,

        [PropID(EPropAssoc.ISE, "Use Smart Guide")]
        [PropValueType(typeof(bool), false)]
        UseSmartGuide,

        [PropID(EPropAssoc.ISE, "Use Synchronous Reset")]
        [PropValueType(typeof(EUseReset), EUseReset.Auto)]
        UseSynchronousReset,

        [PropID(EPropAssoc.ISE, "Use Synchronous Set")]
        [PropValueType(typeof(EUseReset), EUseReset.Auto)]
        UseSynchronousSet,

        [PropID(EPropAssoc.ISE, "Use Synthesis Constraints File")]
        [PropValueType(typeof(bool), true)]
        UseSynthesisConstraintsFile,

        [PropID(EPropAssoc.ISE, "User Access Register Value")]
        [PropValueType(typeof(EUserAccessRegisterValue), EUserAccessRegisterValue.None)]
        UserAccessRegisterValue,

        [PropID(EPropAssoc.ISE, "UserID Code (8 Digit Hexadecimal)")]
        [PropValueType(typeof(string), "0xFFFFFFFF")]
        UserIDCode,

        [PropID(EPropAssoc.ISE, "VHDL Source Analysis Standard")]
        [PropValueType(typeof(EVHDLStandard), EVHDLStandard.VHDL93)]
        VHDLSourceAnalysisStandard,

        [PropID(EPropAssoc.ISE, "Value Range Check")]
        [PropValueType(typeof(bool), false)]
        ValueRangeCheck,

        [PropID(EPropAssoc.ISE, "Verilog 2001 Xst")]
        [PropValueType(typeof(bool), true)]
        Verilog2001Xst,

        [PropID(EPropAssoc.ISE, "Verilog Macros")]
        [PropValueType(typeof(string), "")]
        VerilogMacros,

        [PropID(EPropAssoc.ISE, "Wait for DCI Match (Output Events) virtex5")]
        [PropValueType(typeof(EWaitForDCIMatchVirtex5), EWaitForDCIMatchVirtex5.Auto)]
        WaitForDCIMatchVirtex5,

        [PropID(EPropAssoc.ISE, "Wait for PLL Lock (Output Events) virtex6")]
        [PropValueType(typeof(EWaitForPLLLockVirtex6), EWaitForPLLLockVirtex6.NoWait)]
        WaitForPLLLockVirtex6,

        [PropID(EPropAssoc.ISE, "Watchdog Timer Mode virtex5")]
        [PropValueType(typeof(EWatchdogTimerModeVirtex5), EWatchdogTimerModeVirtex5.Off)]
        WatchdogTimerModeVirtex5,

        [PropID(EPropAssoc.ISE, "Watchdog Timer Value virtex5")]
        [PropValueType(typeof(string), "0x000000")]
        WatchdogTimerValueVirtex5,

        [PropID(EPropAssoc.ISE, "Working Directory")]
        [PropValueType(typeof(string), ".")]
        WorkingDirectory,

        [PropID(EPropAssoc.ISE, "Write Timing Constraints")]
        [PropValueType(typeof(bool), false)]
        WriteTimingConstraints,

        [PropID(EPropAssoc.ISE, "PROP_BehavioralSimTop")]
        [PropValueType(typeof(string), "")]
        PROP_BehavioralSimTop,

        [PropID(EPropAssoc.ISE, "PROP_DesignName")]
        [PropValueType(typeof(string), "")]
        PROP_DesignName,

        [PropID(EPropAssoc.ISE, "PROP_DevFamilyPMName")]
        [PropValueType(typeof(string), "")]
        PROP_DevFamilyPMName,

        [PropID(EPropAssoc.ISE, "PROP_FPGAConfiguration")]
        [PropValueType(typeof(string), "FPGAConfiguration")]
        PROP_FPGAConfiguration,

        [PropID(EPropAssoc.ISE, "PROP_PostFitSimTop")]
        [PropValueType(typeof(string), "")]
        PROP_PostFitSimTop,

        [PropID(EPropAssoc.ISE, "PROP_PostMapSimTop")]
        [PropValueType(typeof(string), "")]
        PROP_PostMapSimTop,

        [PropID(EPropAssoc.ISE, "PROP_PostParSimTop")]
        [PropValueType(typeof(string), "")]
        PROP_PostParSimTop,

        [PropID(EPropAssoc.ISE, "PROP_PostSynthSimTop")]
        [PropValueType(typeof(string), "")]
        PROP_PostSynthSimTop,

        [PropID(EPropAssoc.ISE, "PROP_PostXlateSimTop")]
        [PropValueType(typeof(string), "")]
        PROP_PostXlateSimTop,

        [PropID(EPropAssoc.ISE, "PROP_PreSynthesis")]
        [PropValueType(typeof(string), "PreSynthesis")]
        PROP_PreSynthesis,

        [PropID(EPropAssoc.ISE, "PROP_intProjectCreationTimestamp")]
        [PropValueType(typeof(string), "")]
        PROP_intProjectCreationTimestamp,

        [PropID(EPropAssoc.ISE, "PROP_intWbtProjectID")]
        [PropValueType(typeof(string), "")]
        PROP_intWbtProjectID,

        [PropID(EPropAssoc.ISE, "PROP_intWorkingDirLocWRTProjDir")]
        [PropValueType(typeof(string), "Same")]
        PROP_intWorkingDirLocWRTProjDir,

        [PropID(EPropAssoc.ISE, "PROP_intWorkingDirUsed")]
        [PropValueType(typeof(string), "No")]
        PROP_intWorkingDirUsed,

        [PropID(EPropAssoc.CoreGen, "removerpms")]
        [PropValueType(typeof(bool))]
        Removerpms,

        [PropID(EPropAssoc.CoreGen, "simulationfiles")]
        [PropValueType(typeof(ESimulationFiles))]
        SimulationFiles,

        [PropID(EPropAssoc.CoreGenProj, "simulationoutputproducts")]
        [PropValueType(typeof(EHDL))]
        SimulationOutputProducts,

        [PropID(EPropAssoc.CoreGen, "FlowVendor")]
        [PropID(EPropAssoc.CoreGenProj, "FlowVendor")]
        [PropValueType(typeof(EFlowVendor))]
        FlowVendor,

        [PropID(EPropAssoc.CoreGen, "VHDLSim")]
        [PropID(EPropAssoc.CoreGenProj, "VHDLSim")]
        [PropValueType(typeof(bool))]
        VHDLSim,

        [PropID(EPropAssoc.CoreGen, "VerilogSim")]
        [PropID(EPropAssoc.CoreGenProj, "VerilogSim")]
        [PropValueType(typeof(bool))]
        VerilogSim,

        [PropID(EPropAssoc.CoreGenProj, "workingdirectory")]
        [PropValueType(typeof(string), "./tmp/")]
        CoreGen_WorkingDirectory
    }
}

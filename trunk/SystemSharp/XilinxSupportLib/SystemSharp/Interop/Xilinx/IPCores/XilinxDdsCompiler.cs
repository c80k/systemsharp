using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.CoreGen;
using System.IO;

namespace SystemSharp.Interop.Xilinx.IPCores
{
    public class XilinxDdsCompiler : Component
    {
        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "DDS_Compiler family Xilinx,_Inc. 4.0")]
            DDS_Compiler_C_4_0,
        }

        public enum EAmplitudeMode
        {
            [PropID(EPropAssoc.CoreGen,"Full_Range")]
            FullRange,
            [PropID(EPropAssoc.CoreGen, "Unit_Circle")]
            UnitCircle,
        }

        public enum EDdsCompiler
        {
            [PropID(EPropAssoc.CoreGen, "DdsCompiler")]
            DdsCompiler, 
            
        }

        public enum EDSP48Use
        {
            [PropID(EPropAssoc.CoreGen, "Minimal")]
            Minimal,
            [PropID(EPropAssoc.CoreGen, "Maximal")]
            Maximal,
        }

        public enum ECoregen
        {
            [PropID(EPropAssoc.CoreGen, "Coregen")]
            Coregen,
        }

        public enum EOPtimizationGoal
        {
            [PropID(EPropAssoc.CoreGen, "Auto")]
            Auto,
            [PropID(EPropAssoc.CoreGen, "Area")]
            Area,
            [PropID(EPropAssoc.CoreGen, "Speed")]
            Speed,
        }
        public enum ELatencyOption
        {
            [PropID(EPropAssoc.CoreGen, "Auto")]
            Auto,
            [PropID(EPropAssoc.CoreGen, "Configurable")]
            Configurable,
        }
        public enum ENoiseShaping
        {
            [PropID(EPropAssoc.CoreGen, "Auto")]
            Auto,
            [PropID(EPropAssoc.CoreGen, "None")]
            None,
            [PropID(EPropAssoc.CoreGen, "Phase_Dithering")]
            PhaseDithering,
            [PropID(EPropAssoc.CoreGen, "Taylor_Series_Corrected")]
            TaylorSeriesCorrected,
            
        }

        public enum EOutputSelection
        {
            [PropID(EPropAssoc.CoreGen, "Sine_and_Cosine")]
            SineAndCosine,
            [PropID(EPropAssoc.CoreGen, "Sine")]
            Sine,
            [PropID(EPropAssoc.CoreGen, "Cosine")]
            Cosine,
        }

        public enum EParameterSelection   // Parameter Selection
        {
            [PropID(EPropAssoc.CoreGen, "System_Parameters")]
            SystemParameters,
            [PropID(EPropAssoc.CoreGen, "Hardware_Parameters")]
            HardwareParameters,
        }

        public enum EConfigurationOptions    // GUI_Name: Configuration OPtion
        {
            [PropID(EPropAssoc.CoreGen, "Phase_Generator_and_SIN_COS_LUT")] // XCO Values 
            PhaseGeneratorAndSinCosLut,
            [PropID(EPropAssoc.CoreGen, "Phase_Generator_Only")]  // XCO Values
            PhaseGeneratorOnly,
            [PropID(EPropAssoc.CoreGen, "Sin_and_cos_only")]        // XCO Values
            SinAndCosOnly,
        }

        public enum EMemoryType
        {
            [PropID(EPropAssoc.CoreGen, "Auto")]
            Auto,
            [PropID(EPropAssoc.CoreGen, "Distributed_ROM")]
            DistributedROM,
            [PropID(EPropAssoc.CoreGen, "Block_ROM")]
            BlockROM,

        }
        public enum EPhaseOffset
        {
            [PropID(EPropAssoc.CoreGen, "Fixed")]
            Fixed,     
            [PropID(EPropAssoc.CoreGen, "None")]
            None,
             [PropID(EPropAssoc.CoreGen, "Programmable")]
            programmable,
            [PropID(EPropAssoc.CoreGen, "Streaming")]
            Streaming,

        }

        public enum EPhaseIncrement
        {
            [PropID(EPropAssoc.CoreGen, "Fixed")]
            Fixed,
            [PropID(EPropAssoc.CoreGen, "Programmable")]
            programmable,
            [PropID(EPropAssoc.CoreGen, "Streaming")]
            Streaming,
        }

        
        public In<StdLogic> CLK { private get; set; }
       // public Out <double> x { private get; set; }
       // public In<StdLogicVector> k_in { private get; set; }
        public Out<StdLogicVector> SINE { private get; set; }
        public Out<StdLogicVector> COSINE { private get; set; }
        public Out<StdLogicVector> PHASE_OUT { private get; set; }
        public In<StdLogicVector> DATA { private get; set; }
        public In<StdLogic> WE { private get; set; }
        public In<StdLogic> REG_SELECT { private get; set; }
        public In<StdLogicVector> PINC_IN { private get; set; }
        public In<StdLogicVector> POFF_IN { private get; set; }
        public In<StdLogicVector> PHASE_IN { private get; set; }
        public In<StdLogicVector> SCLR { private get; set; }
        public In<StdLogic> CE { private get; set; }
        public Out<StdLogic> RDY { private get; set; }
        //public Out <StdLogicVector>  PHASE_OUT1 { private get; set; }


        //public Out<StdLogicVector> t { private get; set; } 



        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "amplitude_mode")]
        public EAmplitudeMode AmplitudeMode { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "channel_pin")]
        public bool ChannelPin { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "channels")]
        public int Channels { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clock_enable")]
        public bool ClockEnable { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        public String ComponentName { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dds_clock_rate")]
        public int DdsRlockRate { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dsp48_use")]
        public EDSP48Use Dsp48Use { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "explicit_period")]
        public bool ExplicitPeriod { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "frequency_resolution")]
        public Double FrequencyResolution { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "gui_behaviour")]
        public ECoregen GuiBehaviour { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "has_phase_out")]
        public bool HasPhaseOut { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "latency")]
        public int Latency { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "latency_configuration")]
        public ELatencyOption LatencyConfiguration { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "memory_type")]
        public EMemoryType MemoryType { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "negative_cosine")]
        public bool NegativeCosine { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "negative_sine")]
        public bool NegativeSine { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "noise_shaping")]
        public ENoiseShaping NoiseShaping { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "optimization_goal")]
        public EOPtimizationGoal OptimizationGoal { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency1")]
        public int OutputFrequency1 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency2")]
        public int OutputFrequency2 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency3")]
        public int OutputFrequency3 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency4")]
        public int OutputFrequency4 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency5")]
        public int OutputFrequency5 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency6")]
        public int OutputFrequency6 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency7")]
        public int OutputFrequency7 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency8")]
        public int OutputFrequency8 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency9")]
        public int OutputFrequency9 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency10")]
        public int OutputFrequency10 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency11")]
        public int OutputFrequency11 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_frequency12")]
        public int OutputFrequency12 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_selection")]
        public EOutputSelection OutputSelection { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_width")]
        public int OutputWidth { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "parameter_entry")]
        public EParameterSelection ParameterEntry { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "partspresent")]
        public EConfigurationOptions Partspresent { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "period")]
        public int Period { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_increment")]
        public EPhaseIncrement PhaseIncrement { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset")]
        public EPhaseOffset PhaseOffset { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles1")]
        public int PhaseOffsetAngles1 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles2")]
        public int PhaseOffsetAngles2 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles3")]
        public int PhaseOffsetAngles3 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles4")]
        public int PhaseOffsetAngles4 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles5")]
        public int PhaseOffsetAngles5 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles6")]
        public int PhaseOffsetAngles6 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles7")]
        public int PhaseOffsetAngles7 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles8")]
        public int PhaseOffsetAngles8 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles9")]
        public int PhaseOffsetAngles9 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles10")]
        public int PhaseOffsetAngles10 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles11")]
        public int PhaseOffsetAngles11 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles12")]
        public int PhaseOffsetAngles12 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles13")]
        public int PhaseOffsetAngles13 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles14")]
        public int PhaseOffsetAngles14 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles15")]
        public int PhaseOffsetAngles15 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_offset_angles16")]
        public int PhaseOffsetAngles16 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_width")]
        public int PhaseWidth { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc1")]
        public StdLogicVector pinc1 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc2")]
        public double pinc2 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc3")]
        public double pinc3 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc4")]
        public int pinc4 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc4")]
        public int pinc5 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc6")]
        public int pinc6 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc7")]
        public int pinc7 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc8")]
        public int pinc8 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc9")]
        public int pinc9 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc10")]
        public int pinc10 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc11")]
        public int pinc11 { [StaticEvaluation] get; set; }


        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc12")]
        public int pinc12 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc13")]
        public int pinc13 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc14")]
        public int pinc14 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc15")]
        public int pinc15 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pinc16")]
        public int pinc16 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff1")]
        public StdLogicVector poff1 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff2")]
        public int poff2 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff3")]
        public int poff3 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff4")]
        public int poff4 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff5")]
        public int poff5 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff6")]
        public int poff6 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff7")]
        public int poff7 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff8")]
        public int poff8 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff9")]
        public int poff9 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff10")]
        public int poff10 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff11")]
        public int poff11 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff12")]
        public int poff12 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff13")]
        public int poff13 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff14")]
        public int poff14 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff15")]
        public int poff15 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "poff16")]
        public int poff16 { [StaticEvaluation] get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "por_mode")]
        public bool PorMode { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "rdy")]
        public bool Rdy { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "rfd")]
        public bool Rfd { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sclr_pin")]
        public bool SclrPin { get; set; }

       
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "spurious_free_dynamic_range")]
        public int SpuriousFreeDynamicRange { [StaticEvaluation] get; set; }

       // public double k_in { get; set; }


        public XilinxDdsCompiler()
        {
            Generator = EGenerator.DDS_Compiler_C_4_0;
            AmplitudeMode = EAmplitudeMode.FullRange;
            ChannelPin = false;
            Channels = 1;
            ClockEnable = false;
            //ComponentName = EDdsCompiler.DdsCompiler;
            DdsRlockRate = 100;
            Dsp48Use = EDSP48Use.Minimal;
            ExplicitPeriod = false;
            FrequencyResolution = 0.4;
            GuiBehaviour = ECoregen.Coregen;
            HasPhaseOut = true;
            Latency = 2;
            LatencyConfiguration = ELatencyOption.Configurable;
            MemoryType = EMemoryType.Auto;
            NegativeCosine = false;
            NegativeSine = false;
            NoiseShaping = ENoiseShaping.Auto;
            OptimizationGoal = EOPtimizationGoal.Auto;
            OutputFrequency1 = 0;
            OutputFrequency2 = 0;
            OutputFrequency3 = 0;
            OutputFrequency4 = 0;
            OutputFrequency5 = 0;
            OutputFrequency6 = 0;
            OutputFrequency7 = 0;
            OutputFrequency8 = 0;
            OutputFrequency9 = 0;
            OutputFrequency10 = 0;
            OutputFrequency11 = 0;
            OutputFrequency12= 0;
            OutputSelection = EOutputSelection.SineAndCosine;
            OutputWidth = 6;
            ParameterEntry = EParameterSelection.SystemParameters;
            Partspresent = EConfigurationOptions.PhaseGeneratorAndSinCosLut;
            Period = 1;
            PhaseIncrement = EPhaseIncrement.Fixed;
            PhaseOffset = EPhaseOffset.Fixed;
            PhaseOffsetAngles1=0;
            PhaseOffsetAngles10=0;
            PhaseOffsetAngles11=0;
            PhaseOffsetAngles12=0;
            PhaseOffsetAngles13=0;
            PhaseOffsetAngles14=0;
            PhaseOffsetAngles15=0;
            PhaseOffsetAngles16=0;
            PhaseOffsetAngles2=0;
            PhaseOffsetAngles3=0;
            PhaseOffsetAngles4=0;
            PhaseOffsetAngles5=0;
            PhaseOffsetAngles6=0;
            PhaseOffsetAngles7=0;
            PhaseOffsetAngles8=0;
            PhaseOffsetAngles9=0;
            pinc1 = "0000000000";
            pinc2 = 0;
            pinc3 = 0;
            pinc4 = 0;
            pinc5 = 0;
            pinc6 = 0;
            pinc7 = 0;
            pinc8 = 0;
            pinc8 = 0;
            pinc9 = 0;
            pinc10 = 0;
            pinc11= 0;
            pinc12= 0;
            pinc13= 0;
            pinc14= 0;
            pinc15= 0;
            pinc16 = 0;
            poff1 = "0000000000";
            poff2 = 0;
            poff3 = 0;
            poff4 = 0;
            poff5 = 0;
            poff6 = 0;
            poff7 = 0;
            poff8 = 0;
            poff9 = 0;
            poff10 = 0;
            poff11= 0;
            poff12= 0;
            poff13= 0;
            poff14= 0;
            poff15= 0;
            poff16= 0;
            PorMode = false;
            Rdy = false;
            Rfd = false;
            SclrPin = false;
            SpuriousFreeDynamicRange = 36;
            PhaseWidth = 10;
            
           

        }

        private SLVSignal _t;
        private SLVSignal PhaseIn;
        private SLVSignal PINC;
        private SLVSignal Dat_in;
        private SLVSignal Dat_outOff;
        public SLVSignal Dat_outIncr;
        //private SLVSignal PINC;      
        private StdLogicVector Phse_in = "0000000000";
        private StdLogicVector Gleitkomma = "0000000000";
        private SLVSignal POFF;
        private SLVSignal PH;
        private StdLogicVector Phase_out1 = "0000000000";
        private StdLogicVector Sinc = "0000000000";
        private StdLogicVector Cos = "0000000000";
        private StdLogicVector phase = "0000000000";
        //private double phaseIN = 0;
       private StdLogicVector phase2 = "0000000000";
       private StdLogicVector pinc_in = "0000000000";
       private StdLogicVector poffin = "0000000000";
       private StdLogicVector phase4 = "0000000000";
       private StdLogicVector phase5 = "0000000000";
       private StdLogicVector phase6 = "0000000000";
       private SLVSignal phase3 ;
       private RegPipe _RegPipe;
       //private RegPipe _RegPipeIncr;
       //public int latenc { get; set; }
       public int width { get; set; }
        
                     
        protected override void  PreInitialize()
        {
            _t = new SLVSignal(PhaseWidth)
            {
                InitialValue = StdLogicVector._0s(PhaseWidth)
            };                          
            PINC = new SLVSignal(PhaseWidth)
            {
                InitialValue = StdLogicVector._0s(PhaseWidth)
            };
            POFF = new SLVSignal(PhaseWidth)
            {
                InitialValue = StdLogicVector._0s(PhaseWidth)
            };
             PH = new SLVSignal(PhaseWidth)
            {
                InitialValue = StdLogicVector._0s(PhaseWidth)
            };
            
            PhaseIn = new SLVSignal(PhaseWidth)
            {
                InitialValue = StdLogicVector._0s(PhaseWidth)
            };
            phase3 = new SLVSignal(PhaseWidth)
            {
                InitialValue = StdLogicVector._0s(PhaseWidth)
            };

            //Dat_inIncr = new SLVSignal(PhaseWidth)
            //{
            //    InitialValue = StdLogicVector._1s(PhaseWidth)
            //};
            Dat_in = new SLVSignal(PhaseWidth)
           {
               InitialValue = StdLogicVector._0s(PhaseWidth)
           };
            //Dat_outIncr = new SLVSignal(PhaseWidth)
            //{
            //    InitialValue = StdLogicVector._1s(PhaseWidth)
            //};
            Dat_outOff = new SLVSignal(PhaseWidth)
            {
                InitialValue = StdLogicVector._0s(PhaseWidth)
            };
            _RegPipe = new RegPipe(Latency, PhaseWidth)
            {
              Clk = CLK,
                Din = Dat_in,
                Dout = Dat_outOff,
            };
            //_RegPipeIncr = new RegPipe(depth, width)
            //{
            //    Clk = CLK,
            //    Din = PINC_IN,
            //    Dout = Dat_outIncr,
            //};

        } // hier wird das Egpipe instansiert tec......





        //********************************Hier wird das angegebene Binaere Vektor in Dezimal umgewandelt************************
        private double Getradian(StdLogicVector x)
        {
            StdLogicVector phse = x; // N bits
            long iphase = phse.SignedValue.LongValue;
            double dphase = (double)iphase * Math.Pow(2.0, -(PhaseWidth - 1)) * Math.PI; // hier wird die Phase skaliert und damit kann Sinus berechnet werden

            return dphase;
        }
        //********************************************************************************************************************
        //*******************Hier wird die angegebene Dezimalzahl im Binaere umgewandelt**************************************
        //int intw;
        private StdLogicVector Getbinaer(double x)
        {
            StdLogicVector v = SFix.FromDouble(x, 2, OutputWidth - 2).SignedValue.SLVValue;
            //double y = SFix.FromSigned(v.SignedValue, 10).DoubleValue;
            return v;
        }
        //********************************************************************************************************************

        private StdLogicVector Getpipe( StdLogicVector x)
        {
            Dat_in.Next = x;
            //Latency = y;
            return Dat_outOff.Cur;
        }
        //private StdLogicVector GetpipeIncr()
        //{           
        //    return Dat_outOff.Cur;                         
        //}             

        private StdLogicVector GetPhaseInProgrammable()
        {
            if (WE.Cur == '1')
            {
                if (PhaseOffset == EPhaseOffset.programmable)
                {
                    if (REG_SELECT.Cur == '0')
                    {
                        PINC.Next = DATA.Cur;
                        PH.Next = PINC.Cur;
                    }                    
               else
                    {
                        PH.Next = DATA.Cur;                       
                    }
                }
            }               
                return PH.Cur;
            
        }

        private StdLogicVector GetPhaseOffProgrammable()
        {
            if (WE.Cur == '1')
            {
                if (PhaseIncrement == EPhaseIncrement.programmable)
                {                    
                     if (REG_SELECT.Cur == '1')
                    {
                        POFF.Next = DATA.Cur;
                        PH.Next = POFF.Cur;
                    }
                    else
                    {
                        PH.Next = DATA.Cur;
                    }
                }
            }
            return PH.Cur;

        }
         //private StdLogicVector GetPoffProgrammable()
         //{                                     

        //                   if (REG_SELECT.Cur == '1'&& WE.Cur == '1')
         //                   {
         //                       POFF.Next = DATA.Cur;
         //                   }
                           
         //                   return POFF.Cur;                                              
         //   }

         private StdLogicVector GetPhaseIn()
         {             
            phase3.Next = PHASE_IN.Cur;
             
            return phase3.Cur;

         }

        private StdLogicVector GetPinc()
        {                               
                switch(PhaseIncrement)
                {
                case EPhaseIncrement.Fixed:                 
                     return pinc1;
                     
                case EPhaseIncrement.programmable:
                                            
                      return GetPhaseInProgrammable();                      
                                                    
                case EPhaseIncrement.Streaming:
                                                      
                     return PINC_IN.Cur;                                    
                default: 
                    throw new NotImplementedException();
                    
                }
        }

        private StdLogicVector GetOffset()
        {
            switch (PhaseOffset)
            {
                case EPhaseOffset.Fixed:
                    return poff1;

                case EPhaseOffset.None:
                    return StdLogicVector._0s(PhaseWidth);

                case EPhaseOffset.programmable:

                    return GetPhaseOffProgrammable();

                case EPhaseOffset.Streaming:
                              
                    return POFF_IN.Cur;
                  
                default:
                    throw new NotImplementedException();
            }
        }

       
        private StdLogicVector GetSinus(double x)
        {
            double sin = Math.Sin(x);
            
            return Getbinaer(sin);
        }

        private StdLogicVector GetCosinus(double x)
        {
            double cos = Math.Cos(x);
            return Getbinaer(cos); ;
        }

        private void Processing()
        {
            if (CLK.RisingEdge())
            {
                phase = phase + GetPinc();
                phase2 = phase2 + GetOffset();// hier ist das Problem, die Getpinc() incrementiert sich nicht nur die Getoffset()
                phase4 = phase + phase2;
                //phase5 = Getpipe(phase4);
                //phase6 = GetPhaseIn();
            }
                switch (Partspresent)
                {
                    case EConfigurationOptions.PhaseGeneratorAndSinCosLut:
                        {
                            PHASE_OUT.Next = phase4;
                            SINE.Next = GetSinus(Getradian(phase4));
                            COSINE.Next = GetCosinus(Getradian(phase4));
                        } break;

                    case EConfigurationOptions.PhaseGeneratorOnly:
                        {

                            PHASE_OUT.Next = phase2;

                        } break;
                    case EConfigurationOptions.SinAndCosOnly:
                        {
                            PHASE_OUT.Next = Getpipe(GetPhaseIn());
                            SINE.Next = GetSinus(Getradian(Getpipe(GetPhaseIn())));
                            COSINE.Next = GetCosinus(Getradian(Getpipe(GetPhaseIn())));
                            //PHASE_OUT1 = GetPhaseIn();
                        } break;
                }
            }


        protected override void OnSynthesis(ISynthesisContext ctx)
        {
            if (!(ctx.Project is XilinxProject))
            {
                throw new InvalidOperationException("This floating point block can only be synthesized within the context of a Xilinx ISE project.");
            }
            XilinxProject xproj = (XilinxProject)ctx.Project;
            string name = ctx.CodeGen.GetComponentID(Descriptor);
            ComponentName = name;
            CoreGenDescription cgproj, xco;
            xproj.AddNewCoreGenDescription(name, out cgproj, out xco);
            xco.FromComponent(this);
            xco.Store();

            xproj.ExecuteCoreGen(xco.Path, cgproj.Path);
        }

        protected override void Initialize()
        {
            AddProcess(Processing, CLK);

        }
    }
}

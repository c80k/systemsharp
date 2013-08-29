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
using SystemSharp.Components;
using SystemSharp.DataTypes;

namespace SystemSharp.Interop.Xilinx.IPCores
{
    public class XilinxClockingWizard: Component
    {
        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "Clocking_Wizard family Xilinx,_Inc. 1.8")]
            Clocking_Wizard_1_8
        }

        public enum ECalcDone
        {
            [PropID(EPropAssoc.CoreGen, "DONE")]
            Done
        }

        public enum EClkFeedbackInSignaling
        {
            [PropID(EPropAssoc.CoreGen, "SINGLE")]
            Single
        }

        public enum EClkBufferType
        {
            [PropID(EPropAssoc.CoreGen, "BUFG")]
            BUFG
        }

        public enum EClockManagerType
        {
            [PropID(EPropAssoc.CoreGen, "MANUAL")]
            Manual
        }

        public enum EDCMClkFeedback
        {
            [PropID(EPropAssoc.CoreGen, "1X")]
            _1X
        }

        public enum EClkOutPort
        {
            [PropID(EPropAssoc.CoreGen, "CLK0")]
            Clk0
        }

        public enum EClkGenClkOutPort
        {
            [PropID(EPropAssoc.CoreGen, "CLKFX")]
            ClkFx
        }

        public enum ESpreadSpectrum
        {
            [PropID(EPropAssoc.CoreGen, "NONE")]
            None
        }

        public enum EClkOutPhaseShift
        {
            [PropID(EPropAssoc.CoreGen, "NONE")]
            None
        }

        public enum EDeskewAdjust
        {
            [PropID(EPropAssoc.CoreGen, "SYSTEM_SYNCHRONOUS")]
            SystemSynchronous
        }

        public enum EDcmNotes
        {
            [PropID(EPropAssoc.CoreGen, "None")]
            None
        }

        public enum EFeedbackSource
        {
            [PropID(EPropAssoc.CoreGen, "FDBK_AUTO")]
            AutoFeedback
        }

        public enum EFreqUnits
        {
            [PropID(EPropAssoc.CoreGen, "Units_MHz")]
            MHz
        }

        public enum EJitterUnits
        {
            [PropID(EPropAssoc.CoreGen, "Units_UI")]
            UI
        }

        public enum EJitterOptions
        {
            [PropID(EPropAssoc.CoreGen, "UI")]
            UI
        }

        public enum EJitterSel
        {
            [PropID(EPropAssoc.CoreGen, "No_Jitter")]
            NoJitter
        }

        public enum EMMCMBandwidth
        {
            [PropID(EPropAssoc.CoreGen, "OPTIMIZED")]
            Optimized
        }

        public enum ECompensation
        {
            [PropID(EPropAssoc.CoreGen, "ZHOLD")]
            ZHold
        }

        public enum EMMCMNotes
        {
            [PropID(EPropAssoc.CoreGen, "None")]
            Note
        }

        public enum EPlatform
        {
            [PropID(EPropAssoc.CoreGen, "nt64")]
            NT64
        }

        public enum EPLLBandwidth
        {
            [PropID(EPropAssoc.CoreGen, "OPTIMIZED")]
            Optimized
        }

        public enum EPLLClkFeedback
        {
            [PropID(EPropAssoc.CoreGen, "CLKFBOUT")]
            CLKFBOUT
        }

        public enum EPLLCompensation
        {
            [PropID(EPropAssoc.CoreGen, "SYSTEM_SYNCHRONOUS")]
            SystemSynchronous
        }

        public enum EPLLNotes
        {
            [PropID(EPropAssoc.CoreGen, "None")]
            None
        }

        public enum EClkSource
        {
            [PropID(EPropAssoc.CoreGen, "Single_ended_clock_capable_pin")]
            SingleEndedClockCapablePin
        }

        public enum EPrimTypeSel
        {
            [PropID(EPropAssoc.CoreGen, "MMCM_ADV")]
            MMCM_ADV
        }

        public enum ERelativeInClk
        {
            [PropID(EPropAssoc.CoreGen, "REL_PRIMARY")]
            RelPrimary
        }

        public enum ESummaryStrings
        {
            [PropID(EPropAssoc.CoreGen, "empty")]
            Empty
        }

        public In<StdLogic> ClkIn1 { private get; set; }
        public Out<StdLogic> ClkOut1 { private get; set; }
        public In<StdLogic> Reset { private get; set; }
        public Out<StdLogic> Locked { private get; set; }

        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "calc_done")]
        ECalcDone CalcDone { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clk_out1_use_fine_ps_gui")]
        bool ClkOut1UseFinePsGui { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clk_out2_use_fine_ps_gui")]
        bool ClkOut2UseFinePsGui { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clk_out3_use_fine_ps_gui")]
        bool ClkOut3UseFinePsGui { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clk_out4_use_fine_ps_gui")]
        bool ClkOut4UseFinePsGui { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clk_out5_use_fine_ps_gui")]
        bool ClkOut5UseFinePsGui { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clk_out6_use_fine_ps_gui")]
        bool ClkOut6UseFinePsGui { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clk_out7_use_fine_ps_gui")]
        bool ClkOut7UseFinePsGui { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkfb_in_signaling")]
        EClkFeedbackInSignaling ClockFeedbackInSignaling { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkin1_jitter_ps")]
        double ClkIn1Jitter_ps { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkin1_ui_jitter")]
        double ClkIn1UiJitter { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkin2_jitter_ps")]
        double ClkIn2Jitter_ps { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkin2_ui_jitter")]
        double ClkIn2UiJitter { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout1_drives")]
        EClkBufferType ClkOut1_Buffer { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout1_requested_duty_cycle")]
        public double ClkOut1_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout1_requested_out_freq")]
        public double ClkOut1_Freq { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout1_requested_phase")]
        public double ClkOut1_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout2_drives")]
        EClkBufferType ClkOut2_Buffer { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout2_requested_duty_cycle")]
        public double ClkOut2_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout2_requested_out_freq")]
        public double ClkOut2_Freq { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout2_requested_phase")]
        public double ClkOut2_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout2_used")]
        public bool ClkOut2_Used { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout3_drives")]
        EClkBufferType ClkOut3_Buffer { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout3_requested_duty_cycle")]
        public double ClkOut3_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout3_requested_out_freq")]
        public double ClkOut3_Freq { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout3_requested_phase")]
        public double ClkOut3_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout3_used")]
        public bool ClkOut3_Used { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout4_drives")]
        EClkBufferType ClkOut4_Buffer { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout4_requested_duty_cycle")]
        public double ClkOut4_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout4_requested_out_freq")]
        public double ClkOut4_Freq { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout4_requested_phase")]
        public double ClkOut4_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout4_used")]
        public bool ClkOut4_Used { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout5_drives")]
        EClkBufferType ClkOut5_Buffer { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout5_requested_duty_cycle")]
        public double ClkOut5_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout5_requested_out_freq")]
        public double ClkOut5_Freq { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout5_requested_phase")]
        public double ClkOut5_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout5_used")]
        public bool ClkOut5_Used { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout6_drives")]
        EClkBufferType ClkOut6_Buffer { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout6_requested_duty_cycle")]
        public double ClkOut6_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout6_requested_out_freq")]
        public double ClkOut6_Freq { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout6_requested_phase")]
        public double ClkOut6_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout6_used")]
        public bool ClkOut6_Used { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout7_drives")]
        EClkBufferType ClkOut7_Buffer { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout7_requested_duty_cycle")]
        public double ClkOut7_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout7_requested_out_freq")]
        public double ClkOut7_Freq { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout7_requested_phase")]
        public double ClkOut7_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clkout7_used")]
        public bool ClkOut7_Used { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clock_mgr_type")]
        EClockManagerType ClockManagerType { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        public string ComponentName 
        {
            get
            {
                return Descriptor.Name;
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clk_feedback")]
        EDCMClkFeedback DCM_ClkFeedback { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clk_out1_port")]
        EClkOutPort DCM_ClkOutPort1 { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clk_out2_port")]
        EClkOutPort DCM_ClkOutPort2 { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clk_out3_port")]
        EClkOutPort DCM_ClkOutPort3 { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clk_out4_port")]
        EClkOutPort DCM_ClkOutPort4 { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clk_out5_port")]
        EClkOutPort DCM_ClkOutPort5 { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clk_out6_port")]
        EClkOutPort DCM_ClkOutPort6 { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkdv_divide")]
        uint DCM_ClkDv_Divide { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkfx_divide")]
        uint DCM_ClkFx_Divide { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkfx_multiply")]
        uint DCM_ClkFx_Multiply { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clk_out1_port")]
        EClkGenClkOutPort DCM_ClkGen_ClkOut1Port { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clk_out2_port")]
        EClkGenClkOutPort DCM_ClkGen_ClkOut2Port { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clk_out3_port")]
        EClkGenClkOutPort DCM_ClkGen_ClkOut3Port { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clkfx_divide")]
        uint DCM_ClkGen_ClkFx_Divide { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clkfx_md_max")]
        double DCM_ClkGen_ClkFx_Md_Max { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clkfx_multiply")]
        uint DCM_ClkGen_ClkFx_Multiply { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clkfxdv_divide")]
        uint DCM_ClkGen_ClkFxDv_Divide { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_clkin_period")]
        double DCM_ClkGen_ClkInPeriod { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_notes")]
        EDcmNotes DCM_ClkGen_Notes { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_spread_spectrum")]
        ESpreadSpectrum DCM_ClkGen_SpreadSpectrum { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkgen_startup_wait")]
        bool DCM_ClkGen_StartupWait { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkin_divide_by_2")]
        bool DCM_ClkIn_DivideBy2 { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkin_period")]
        double DCM_ClkIn_Period { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_clkout_phase_shift")]
        EClkOutPhaseShift DCM_ClkOut_PhaseShift { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_deskew_adjust")]
        EDeskewAdjust DCM_DeskewAdjust { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_notes")]
        EDcmNotes DCM_Notes { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_phase_shift")]
        uint DCM_PhaseShift { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dcm_startup_wait")]
        bool DCM_StartupWait { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "feedback_source")]
        public EFeedbackSource FeedbackSource { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "in_freq_units")]
        public EFreqUnits InFreqUnits { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "in_jitter_units")]
        EJitterUnits JitterUnits { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "jitter_options")]
        EJitterOptions JitterOptions { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "jitter_sel")]
        EJitterSel JitterSel { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_bandwidth")]
        EMMCMBandwidth MMCM_Bandwidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkfbout_mult_f")]
        double MMCM_ClkFbOut_Mult_f { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkfbout_phase")]
        double MMCM_ClkFbOut_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkfbout_use_fine_ps")]
        bool MMCM_ClkFbOut_UseFinePs { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkin1_period")]
        public double MMCM_ClkIn1_Period { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkin2_period")]
        public double MMCM_ClkIn2_Period { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkout0_divide_f")]
        double MMCM_ClkOut0_Divide_f { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkout0_duty_cycle")]
        double MMCM_ClkOut0_DutyCycle { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkout0_phase")]
        double MMCM_ClkOut0_Phase { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "mmcm_clkout0_use_fine_ps")]
        double MMCM_ClkOut0_UseFinePs { get; set; }
    }
}

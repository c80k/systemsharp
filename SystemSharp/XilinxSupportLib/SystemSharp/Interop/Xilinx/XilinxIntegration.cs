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
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Components.Std;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Meta;

namespace SystemSharp.Interop.Xilinx
{
    [Flags]
    public enum EXilinxIPCores
    {
        None = 0x0,
        FloatingPoint = 0x1,
        FixedPointAddSub = 0x2,
        Cordic = 0x4,
        FixedPointDiv = 0x8,
        FixedPointMul = 0x10,
        BlockMem = 0x20,
        All = FloatingPoint | FixedPointAddSub | Cordic | FixedPointDiv | FixedPointMul | BlockMem
    }

    /// <summary>
    /// This static class provides services for registering the Xilinx IP core models into the System# framework.
    /// </summary>
    public static class XilinxIntegration
    {
        private static Dictionary<string, EISEVersion> _versionMap = new Dictionary<string, EISEVersion>();

        static XilinxIntegration()
        {
            foreach (PropDesc prop in PropEnum.EnumProps(typeof(EISEVersion)))
                _versionMap[prop.IDs[EPropAssoc.ISE]] = (EISEVersion)prop.EnumValue;
        }

        /// <summary>
        /// Registers Xilinx IP core models for the design.
        /// </summary>
        /// <param name="dd">the design</param>
        /// <param name="flags">which IP cores should be registered</param>
        public static void RegisterIPCores(this DesignDescriptor dd, EXilinxIPCores flags = EXilinxIPCores.All)
        {
            var plan = dd.GetHLSPlan();
            if (flags.HasFlag(EXilinxIPCores.FloatingPoint))
                plan.AddXILMapper(typeof(FloatingPointCore));
            if (flags.HasFlag(EXilinxIPCores.FixedPointAddSub))
                plan.AddXILMapper(typeof(XilinxAdderSubtracter));
            if (flags.HasFlag(EXilinxIPCores.Cordic))
            {
                plan.AddXILMapper(typeof(XilinxCordic));
                var fpi = (from t in plan.XILSTransformations
                           where t is FixPointImplementor
                           select t as FixPointImplementor).SingleOrDefault();
                if (fpi != null)
                    fpi.HaveXilinxCordic = true;
            }
            if (flags.HasFlag(EXilinxIPCores.FixedPointDiv))
                plan.AddXILMapper(typeof(XilinxDivider));
            if (flags.HasFlag(EXilinxIPCores.FixedPointMul))
                plan.AddXILMapper(typeof(XilinxMultiplier));
            if (flags.HasFlag(EXilinxIPCores.BlockMem))
            {
                BlockMemXILMapper bmxm = new BlockMemXILMapper(plan.MemMapper.DefaultRegion);
                plan.AddXILMapper(bmxm);
            }
        }

        private class PipelineDepthCalculator
        {
            private float _ratio;

            public PipelineDepthCalculator(float ratio)
            {
                _ratio = ratio;
            }

            public int CalcALUPipelineDepth(ALU.EFunction op, ALU.EArithMode mode, int osize0, int osize1, int rsize)
            {
                switch (op)
                {
                    case ALU.EFunction.Add:
                    case ALU.EFunction.Compare:
                    case ALU.EFunction.Sub:
                        {
                            float max = (float)osize0 * 0.0625f;
                            int depth = (int)Math.Round(_ratio * max);
                            return depth;
                        }

                    case ALU.EFunction.And:
                    case ALU.EFunction.Not:
                    case ALU.EFunction.Or:
                        return 0;

                    case ALU.EFunction.Mul:
                        {
                            float min = 0.0f;
                            // These coefficients were found experimentally by
                            // regression of Xilinx datasheet
                            float qterm = 0.0049f * (float)osize0 * (float)osize1;
                            float linterm = 0.0857f * (float)(osize0 + osize1);
                            float max = 0.5f * qterm + 0.4f * linterm + 1.0f;
                            int depth = (int)Math.Round(min + _ratio * (max - min));
                            return depth;
                        }

                    case ALU.EFunction.Neg:
                        {
                            float max = osize0 / 16.0f;
                            int depth = (int)Math.Ceiling(_ratio * max);
                            return depth;
                        }

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Configures the latency computation heuristic for a given synthesis plan.
        /// </summary>
        /// <param name="plan">synthesis plan</param>
        /// <param name="ratio">scaling factor for operator latencies</param>
        public static void SetLatencyProfile(HLSPlan plan, float ratio)
        {
            var axms = plan.XILMappers
                .Where(m => m is ALUXILMapper)
                .Cast<ALUXILMapper>();
            foreach (var axm in axms)
            {
                var calc = new PipelineDepthCalculator(ratio);
                axm.CalcPipelineDepth = calc.CalcALUPipelineDepth;
            }

            var xaxms = plan.XILMappers
                .Where(m => m is XilinxAdderSubtracterXILMapper)
                .Cast<XilinxAdderSubtracterXILMapper>();
            foreach (var xaxm in xaxms)
            {
                xaxm.Config.PipeStageScaling = ratio;
            }

            var mxms = plan.XILMappers
                .Where(m => m is XilinxMultiplierXILMapper)
                .Cast <XilinxMultiplierXILMapper>();
            foreach (var mxm in mxms)
            {
                mxm.Config.PipeStageScaling = ratio; 
            }

            var dxms = plan.XILMappers
                .Where(m => m is XilinxDividerXILMapper)
                .Cast<XilinxDividerXILMapper>();
            foreach (var dxm in dxms)
            {
                dxm.Config.PipeStageScaling = ratio;
            }

            var fpxms = plan.XILMappers
                .Where(m => m is FloatingPointXILMapper)
                .Cast<FloatingPointXILMapper>();
            foreach (var fpxm in fpxms)
            {
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.AddSubtract].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.AddSubtract].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.AddSubtract].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Compare].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Compare].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Compare].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Divide].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Divide].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Divide].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FixedToFloat].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FixedToFloat].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FixedToFloat].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFixed].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFixed].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFixed].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFloat].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFloat].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFloat].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Multiply].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Multiply].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Multiply].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.SquareRoot].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.SquareRoot].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.SquareRoot].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.AddSubtract].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.AddSubtract].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.AddSubtract].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Compare].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Compare].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Compare].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Divide].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Divide].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Divide].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FixedToFloat].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FixedToFloat].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FixedToFloat].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFixed].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFixed].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFixed].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFloat].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFloat].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFloat].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Multiply].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Multiply].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Multiply].LatencyRatio = ratio;

                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.SquareRoot].UseMaximumLatency = false;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.SquareRoot].SpecifyLatencyRatio = true;
                fpxm.Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.SquareRoot].LatencyRatio = ratio;
            }

            var cordics = plan.XILMappers
                .Where(m => m is CordicXILMapper)
                .Cast<CordicXILMapper>();
            foreach (var cordic in cordics)
            {
                if (ratio < 0.1f)
                {
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].PipeliningMode = XilinxCordic.EPipeliningMode.No_Pipelining;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].PipeliningMode = XilinxCordic.EPipeliningMode.No_Pipelining;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].PipeliningMode = XilinxCordic.EPipeliningMode.No_Pipelining;                    
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].PipeliningMode = XilinxCordic.EPipeliningMode.No_Pipelining;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].PipeliningMode = XilinxCordic.EPipeliningMode.No_Pipelining;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].PipeliningMode = XilinxCordic.EPipeliningMode.No_Pipelining;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].PipeliningMode = XilinxCordic.EPipeliningMode.No_Pipelining;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].RegisterInputs = false;
                }
                else if (ratio < 0.5f)
                {
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].RegisterInputs = false;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].RegisterInputs = false;
                }
                else if (ratio < 1.0f)
                {
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].RegisterInputs = true;
                }
                else
                {
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].PipeliningMode = XilinxCordic.EPipeliningMode.Maximum;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].PipeliningMode = XilinxCordic.EPipeliningMode.Maximum;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].PipeliningMode = XilinxCordic.EPipeliningMode.Maximum;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].PipeliningMode = XilinxCordic.EPipeliningMode.Maximum;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].PipeliningMode = XilinxCordic.EPipeliningMode.Maximum;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].PipeliningMode = XilinxCordic.EPipeliningMode.Maximum;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].PipeliningMode = XilinxCordic.EPipeliningMode.Maximum;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctan].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Arctanh].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Rotate].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinAndCos].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Sqrt].RegisterInputs = true;
                    cordic.Config[XilinxCordic.EFunctionalSelection.Translate].RegisterInputs = true;
                }
            }
        }
    }
}

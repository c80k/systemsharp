using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.Devices.Virtex6;
using SystemSharp.Interop.Xilinx.IPCores;

namespace XilinxTest
{
    class FPUWrapper : Component
    {
        public In<StdLogic> Clk { get; set; }
        public In<StdLogicVector> A { get; set; }
        public In<StdLogicVector> B { get; set; }
        public Out<StdLogicVector> R { get; set; }

        private FloatingPointCore _fpu;

        public FPUWrapper(EISEVersion iseVer)
        {
            var a = new SLVSignal(32);
            var b = new SLVSignal(32);
            var r = new SLVSignal(32);
            var clk = new SLSignal();
            _fpu = new FloatingPointCore()
            {
                A = a,
                B = b,
                Clk = clk,
                Result = r,
                DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage,
                Function = FloatingPointCore.EFunction.AddSubtract,
                TargetDeviceFamily = SystemSharp.Interop.Xilinx.EDeviceFamily.Virtex6,
                UseMaximumLatency = true,
                Precision = FloatingPointCore.EPrecision.Single,
                ResultPrecision = FloatingPointCore.EPrecision.Single,
                AddSubSel = FloatingPointCore.EAddSub.Add,
                TargetISEVersion = iseVer
            };
            Clk = clk;
            A = a;
            B = b;
            R = r;
        }
    }

    class FloatingPointCoreTest
    {
        public static void GenerateFloatingPointCores()
        {
            var ise = ISEDetector.DetectMostRecentISEInstallation();
            DesignContext.Reset();
            var fpu = new FPUWrapper(ise.VersionTag);
            DesignContext.Instance.Elaborate();
            var fpga = new XC6VLX240T_FF1156();
            fpga.SpeedGrade = ESpeedGrade._2;
            fpga.TopLevelComponent = fpu;
            var proj = fpga.Synthesize("c:\\temp\\fputest", "fpu", null, 
                EFlowStep.HDLGen | EFlowStep.IPCores);
            var flow = proj.ConfigureFlow(fpu);
            flow.TRCE.ReportUnconstrainedPaths = true;
            flow.Start(
                EFlowStep.XST | 
                EFlowStep.NGDBuild | 
                EFlowStep.Map | 
                EFlowStep.PAR | 
                EFlowStep.TRCE);
            proj.AwaitRunningToolsToFinish();
            PerformanceRecord designRec;
            ResourceRecord deviceRec;
            flow.ParseResourceRecords(out designRec, out deviceRec);
        }
    }
}

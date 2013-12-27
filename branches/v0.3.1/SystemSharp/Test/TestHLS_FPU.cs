using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.DocGen;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    public class TestHLS_FPU : Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }
        public Out<StdLogicVector> Sum { private get; set; }
        public Out<StdLogicVector> Diff { private get; set; }
        public Out<StdLogicVector> Prod { private get; set; }
        public Out<StdLogicVector> Quot { private get; set; }
        public Out<StdLogicVector> Neg { private get; set; }
        public Out<StdLogicVector> Abs { private get; set; }

        [HLS]
        private async void Computation()
        {
            await Tick;
            while (true)
            {
                ProgramFlow.DoNotUnroll();
                Sum.Next = (A.Cur.ToDouble() + B.Cur.ToDouble()).ToSLV();
                Diff.Next = (A.Cur.ToDouble() - B.Cur.ToDouble()).ToSLV();
                Prod.Next = (A.Cur.ToDouble() * B.Cur.ToDouble()).ToSLV();
                Quot.Next = (A.Cur.ToDouble() / B.Cur.ToDouble()).ToSLV();
                Neg.Next = (-A.Cur.ToDouble()).ToSLV();
                Abs.Next = Math.Abs(A.Cur.ToDouble()).ToSLV();
                await 30.Ticks();
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Computation, Clk.RisingEdge, Clk);
        }
    }

    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    public class TestHLS_FPU_Testbench : Component
    {
        private TestHLS_FPU _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _a = new SLVSignal(64);
        private SLVSignal _b = new SLVSignal(64);
        private SLVSignal _sum = new SLVSignal(64);
        private SLVSignal _diff = new SLVSignal(64);
        private SLVSignal _prod = new SLVSignal(64);
        private SLVSignal _quot = new SLVSignal(64);
        private SLVSignal _neg = new SLVSignal(64);
        private SLVSignal _abs = new SLVSignal(64);

        public TestHLS_FPU_Testbench()
        {
            _dut = new TestHLS_FPU()
            {
                Clk = _clk,
                A = _a,
                B = _b,
                Sum = _sum,
                Diff = _diff,
                Prod = _prod,
                Quot = _quot,
                Neg = _neg,
                Abs = _abs
            };
            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            for (double i = -5.0; i < 5.0; i += 2.0)
            {
                for (double j = -0.5; j < 0.5; j += 0.2)
                {
                    _a.Next = i.ToSLV();
                    _b.Next = j.ToSLV();
                    await 64.Ticks();
                    Console.WriteLine("A = " + _a.Cur.ToDouble() + ", B = " + _b.Cur.ToDouble() + ", Sum = " + _sum.Cur.ToDouble());
                    Console.WriteLine("A = " + _a.Cur.ToDouble() + ", B = " + _b.Cur.ToDouble() + ", Diff = " + _diff.Cur.ToDouble());
                    Console.WriteLine("A = " + _a.Cur.ToDouble() + ", B = " + _b.Cur.ToDouble() + ", Prod = " + _prod.Cur.ToDouble());
                    Console.WriteLine("A = " + _a.Cur.ToDouble() + ", B = " + _b.Cur.ToDouble() + ", Quot = " + _quot.Cur.ToDouble());
                    Console.WriteLine("A = " + _a.Cur.ToDouble() + ", B = " + _b.Cur.ToDouble() + ", Neg = " + _neg.Cur.ToDouble());
                    Console.WriteLine("A = " + _a.Cur.ToDouble() + ", B = " + _b.Cur.ToDouble() + ", Abs = " + _abs.Cur.ToDouble());
                }
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(StimulateAndTest, _clk.RisingEdge, _clk);
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestHLS_FPU_Testbench tb = new TestHLS_FPU_Testbench();

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(3.0, ETimeUnit.us));
            DesignContext.Stop();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_TestHLS_FPU", "TestHLS_FPU");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.SetVHDLProfile();

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen);
            project.Save();
            var eng = SynthesisEngine.Create(
                DesignContext.Instance, new DocumentationProject(@".\hdl_out_TestHLS_FPU\doc"));
        }
    }
}

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
    public class TestHLS_ALU : Component
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
        public Out<StdLogic> Rdy { private get; set; }

        [HLS]
        private async void Computation()
        {
            await Tick;
            while (true)
            {
                ProgramFlow.IOBarrier();
                Rdy.Next = '0';
                Sum.Next = StdLogicVector.FromInt(A.Cur.IntValue + B.Cur.IntValue, 32);
                Diff.Next = StdLogicVector.FromInt(A.Cur.IntValue - B.Cur.IntValue, 32);
                Prod.Next = StdLogicVector.FromInt(A.Cur.IntValue * B.Cur.IntValue, 32);
                Quot.Next = StdLogicVector.FromInt(B.Cur.IntValue / A.Cur.IntValue, 32);
                Neg.Next = StdLogicVector.FromInt(-A.Cur.IntValue, 32);
                Abs.Next = StdLogicVector.FromInt(Math.Abs(A.Cur.IntValue), 32);
                //await 9.Ticks();
                await NTicks(9);
                ProgramFlow.IOBarrier();
                Rdy.Next = '1';
                ProgramFlow.IOBarrier();
                await Tick;
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Computation, Clk.RisingEdge, Clk);
        }
    }

    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    public class TestHLS_ALU_Testbench : Component
    {
        private TestHLS_ALU _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _a = new SLVSignal(32);
        private SLVSignal _b = new SLVSignal(32);
        private SLVSignal _sum = new SLVSignal(32);
        private SLVSignal _diff = new SLVSignal(32);
        private SLVSignal _prod = new SLVSignal(32);
        private SLVSignal _quot = new SLVSignal(32);
        private SLVSignal _neg = new SLVSignal(32);
        private SLVSignal _abs = new SLVSignal(32);
        private SLSignal _rdy = new SLSignal();

        public TestHLS_ALU_Testbench()
        {
            _dut = new TestHLS_ALU()
            {
                Clk = _clk,
                A = _a,
                B = _b,
                Sum = _sum,
                Diff = _diff,
                Prod = _prod,
                Quot = _quot,
                Neg = _neg,
                Abs = _abs,
                Rdy = _rdy
            };
            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            for (int i = -5; i < 5; i += 2)
            {
                for (int j = -5; j < 5; j += 2)
                {
                    _a.Next = StdLogicVector.FromInt(i, 32);
                    _b.Next = StdLogicVector.FromInt(j, 32);
                    while (_rdy.Cur != '0')
                        await Tick;
                    while (_rdy.Cur != '1')
                        await Tick;
                    Console.WriteLine("A = " + _a.Cur.IntValue + ", B = " + _b.Cur.IntValue + ", Sum = " + _sum.Cur.IntValue);
                    Console.WriteLine("A = " + _a.Cur.IntValue + ", B = " + _b.Cur.IntValue + ", Diff = " + _diff.Cur.IntValue);
                    Console.WriteLine("A = " + _a.Cur.IntValue + ", B = " + _b.Cur.IntValue + ", Prod = " + _prod.Cur.IntValue);
                    Console.WriteLine("A = " + _a.Cur.IntValue + ", B = " + _b.Cur.IntValue + ", Quot = " + _quot.Cur.IntValue);
                    Console.WriteLine("A = " + _a.Cur.IntValue + ", B = " + _b.Cur.IntValue + ", Neg = " + _neg.Cur.IntValue);
                    Console.WriteLine("A = " + _a.Cur.IntValue + ", B = " + _b.Cur.IntValue + ", Abs = " + _abs.Cur.IntValue);
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

            TestHLS_ALU_Testbench tb = new TestHLS_ALU_Testbench();

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(10.0, ETimeUnit.us));
            DesignContext.Stop();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_TestHLS_ALU", "TestHLS_ALU");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.SetVHDLProfile();

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen); ;
            var eng = SynthesisEngine.Create(
                DesignContext.Instance, new DocumentationProject(@".\hdl_out_TestHLS_ALU\doc"));
            eng.Synthesize(new DocumentationGenerator());
            project.Save();
        }
    }
}

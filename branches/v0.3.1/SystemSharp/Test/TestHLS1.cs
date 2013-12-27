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
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    public class TestHLSComponent1: Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }

        public Out<StdLogic> Rdy { private get; set; }
        public Out<StdLogicVector> Sum { private get; set; }
        public Out<StdLogicVector> Diff { private get; set; }
        public Out<StdLogicVector> Prod { private get; set; }
        public Out<StdLogicVector> Quot { private get; set; }

        [HLS]
        private async void Computation()
        {
            await Tick;
            Rdy.Next = '0';
            while (true)
            {
                ProgramFlow.DoNotUnroll();
                int a = A.Cur.IntValue;
                int b = B.Cur.IntValue;
                int sum = a + b;
                int diff = a - b;
                int prod = a * b;
                int quot = a / b;
                ProgramFlow.IOBarrier();
                Rdy.Next = '1';
                Sum.Next = StdLogicVector.FromInt(sum, 32);
                Diff.Next = StdLogicVector.FromInt(diff, 32);
                Prod.Next = StdLogicVector.FromInt(prod, 32);
                Quot.Next = StdLogicVector.FromInt(quot, 32);
                await Tick;
                ProgramFlow.IOBarrier();
                Rdy.Next = '0';
                await Tick;
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Computation, Clk.RisingEdge, Clk);
        }
    }

    public class TestHLSTestbench1 : Component
    {
        private TestHLSComponent1 _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _a = new SLVSignal(32);
        private SLVSignal _b = new SLVSignal(32);
        private SLVSignal _sum = new SLVSignal(32);
        private SLVSignal _diff = new SLVSignal(32);
        private SLVSignal _prod = new SLVSignal(32);
        private SLVSignal _quot = new SLVSignal(32);
        private SLSignal _rdy = new SLSignal();

        public TestHLSTestbench1()
        {
            _dut = new TestHLSComponent1()
            {
                Clk = _clk,
                A = _a,
                B = _b,
                Sum = _sum,
                Diff = _diff,
                Prod = _prod,
                Quot = _quot,
                Rdy = _rdy
            };
            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            for (int i = 1; i < 10; i++)
            {
                for (int j = 1; j < 10; j++)
                {
                    _a.Next = StdLogicVector.FromInt(i, 32);
                    _b.Next = StdLogicVector.FromInt(j, 32);
                    do
                    {
                        await RisingEdge(_clk);
                    } while (_rdy.Cur != '0');
                    do
                    {
                        await RisingEdge(_clk);
                    } while (_rdy.Cur != '1');
                    Console.WriteLine(i + " + " + j + " = " + _sum.Cur.IntValue);
                    Console.WriteLine(i + " - " + j + " = " + _diff.Cur.IntValue);
                    Console.WriteLine(i + " * " + j + " = " + _prod.Cur.IntValue);
                    Console.WriteLine(i + " / " + j + " = " + _quot.Cur.IntValue);
                }
            }
        }

        protected override void Initialize()
        {
            AddThread(StimulateAndTest);
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestHLSTestbench1 tb = new TestHLSTestbench1();

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(1.0, ETimeUnit.us));
            DesignContext.Stop();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_TestHLS1", "TestHLS1");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.SetVHDLProfile();
            project.SkipIPCoreSynthesis = true;

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen); ;
            project.Save();
        }
    }
}

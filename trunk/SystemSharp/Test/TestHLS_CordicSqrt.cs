using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Common;
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
    public class TestHLS_CordicSqrt : Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        public In<StdLogicVector> X1 { private get; set; }
        public In<StdLogicVector> X2 { private get; set; }
        public Out<StdLogicVector> Sqrt1 { private get; set; }
        public Out<StdLogicVector> Sqrt2 { private get; set; }
        public Out<StdLogic> Rdy { private get; set; }

        private int _fracWidth;

        public TestHLS_CordicSqrt(int fracWidth)
        {
            _fracWidth = fracWidth;
        }

        [HLS]
        private async void Computation()
        {
            await Tick;
            while (true)
            {
                ProgramFlow.DoNotUnroll();
                ProgramFlow.IOBarrier();
                Rdy.Next = '0';
                Sqrt1.Next = MathExt.Sqrt(UFix.FromUnsigned(X1.Cur.UnsignedValue, _fracWidth)).SLVValue;
                Sqrt2.Next = MathExt.Sqrt(UFix.FromUnsigned(X2.Cur.UnsignedValue, _fracWidth)).SLVValue;
                await NTicks(63);
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
    public class TestHLS_CordicSqrt_Testbench : Component
    {
        private TestHLS_CordicSqrt _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _x1 = new SLVSignal(16);
        private SLVSignal _x2 = new SLVSignal(17);
        private SLVSignal _sqrt1 = new SLVSignal(16);
        private SLVSignal _sqrt2 = new SLVSignal(17);
        private SLSignal _rdy = new SLSignal();

        public TestHLS_CordicSqrt_Testbench()
        {
            _dut = new TestHLS_CordicSqrt(8)
            {
                Clk = _clk,
                X1 = _x1,
                X2 = _x2,
                Sqrt1 = _sqrt1,
                Sqrt2 = _sqrt2,
                Rdy = _rdy
            };
            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            for (double i = 0.01; i < 100.0; i *= 2.0)
            {
                _x1.Next = UFix.FromDouble(i, 8, 8).SLVValue;
                _x2.Next = UFix.FromDouble(i, 9, 8).SLVValue;
                while (_rdy.Cur != '0')
                    await _rdy;
                while (_rdy.Cur != '1')
                    await _rdy;
                var x1 = UFix.FromUnsigned(_x1.Cur.UnsignedValue, 8);
                var sqrt1 = UFix.FromUnsigned(_sqrt1.Cur.UnsignedValue, 12);
                Console.WriteLine("X1 = " + x1.DoubleValue + ", Sqrt = " + sqrt1.DoubleValue + ", Sqrt^2 = " + (sqrt1 * sqrt1).DoubleValue);
                var x2 = UFix.FromUnsigned(_x2.Cur.UnsignedValue, 8);
                var sqrt2 = UFix.FromUnsigned(_sqrt2.Cur.UnsignedValue, 12);
                Console.WriteLine("X2 = " + x1.DoubleValue + ", Sqrt = " + sqrt2.DoubleValue + ", Sqrt^2 = " + (sqrt2 * sqrt2).DoubleValue);
            }
        }

        protected override void Initialize()
        {
            AddThread(StimulateAndTest);
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestHLS_CordicSqrt_Testbench tb = new TestHLS_CordicSqrt_Testbench();

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(100.0, ETimeUnit.us));
            DesignContext.Stop();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            var docproj = new DocumentationProject(@".\hdl_out_TestHLSSqrt_Cordic\doc");
            var project = new XilinxProject(@".\hdl_out_TestHLSSqrt_Cordic", "TestHLSSqrt_Cordic");
            project.ISEVersion = EISEVersion._13_2;
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.SetVHDLProfile();
            //project.SkipIPCoreSynthesis = true;

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen);
            SynthesisEngine.Create(DesignContext.Instance, docproj).Synthesize(new DocumentationGenerator());
            project.Save();
            docproj.Save();
        }
    }
}

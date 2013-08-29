using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    class TestHLS_SinCosLUT: Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        public In<StdLogicVector> X { private get; set; }
        public Out<StdLogicVector> Sin { private get; set; }
        public Out<StdLogicVector> Cos { private get; set; }
        public Out<StdLogic> Rdy { private get; set; }

        private int _xFracWidth;
        private int _yFracWidth;

        public TestHLS_SinCosLUT(int xFracWidth, int yFracWidth)
        {
            _xFracWidth = xFracWidth;
            _yFracWidth = yFracWidth;
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
                var sincos = MathExt.ScSinCos(SFix.FromSigned(X.Cur.SignedValue, _xFracWidth), _yFracWidth);
                Cos.Next = sincos.Item1.SLVValue;
                Sin.Next = sincos.Item2.SLVValue;
                await 12.Ticks();
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
    public class TestHLS_SinCosLUT_Testbench : Component
    {
        private int _xFracWidth;
        private int _yFracWidth;

        private TestHLS_SinCosLUT _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _x;
        private SLVSignal _sin;
        private SLVSignal _cos;
        private SLSignal _rdy = new SLSignal();

        public TestHLS_SinCosLUT_Testbench(int xFracWidth, int yFracWidth)
        {
            _xFracWidth = xFracWidth;
            _yFracWidth = yFracWidth;

            _x = new SLVSignal(2 + xFracWidth);
            _sin = new SLVSignal(2 + yFracWidth);
            _cos = new SLVSignal(2 + yFracWidth);

            _dut = new TestHLS_SinCosLUT(xFracWidth, yFracWidth)
            {
                Clk = _clk,
                X = _x,
                Sin = _sin,
                Cos = _cos,
                Rdy = _rdy
            };
            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            DesignContext.Instance.FixPoint.DefaultRadix = 10;

            for (double i = -1.0; i < 1.0; i += 0.0625)
            {
                var x = SFix.FromDouble(i, 2, _xFracWidth);
                _x.Next = x.SLVValue;
                while (_rdy.Cur != '0')
                    await Tick;
                while (_rdy.Cur != '1')
                    await Tick;
                Console.WriteLine("X = " + x + 
                    ", Sin = " + SFix.FromSigned(_sin.Cur.SignedValue, _yFracWidth) +
                    ", Cos = " + SFix.FromSigned(_cos.Cur.SignedValue, _yFracWidth));
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(StimulateAndTest, _clk.RisingEdge, _clk);
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            var tb = new TestHLS_SinCosLUT_Testbench(8, 9);

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(100.0, ETimeUnit.us));
            DesignContext.Stop();
            //XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_TestHLS_SinCosLUT_Testbench", "TestHLS_SinCosLUT_Testbench");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.ISEVersion = EISEVersion._13_2;
            project.SetVHDLProfile();

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen);
            project.Save();
        }
    }
}

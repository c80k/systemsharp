using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.Devices.Virtex6;
using SystemSharp.Synthesis;

namespace Test
{
    public class TestHLS_SFixDiv: Component
    {
        private readonly int iw, fw;

        private SLSignal _clk = new SLSignal();
        private volatile bool _rdy;
        private volatile bool _nxt;
        private SFix _dividend;
        private SFix _divisor;
        private SFix _quotient;

        private Clock _clkGen;

        public TestHLS_SFixDiv(int iw, int fw)
        {
            this.iw = iw;
            this.fw = fw;
            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        protected override void Initialize()
        {
            AddClockedThread(DivideProcess, _clk.RisingEdge, _clk);
            AddClockedThread(TestProcess, _clk.RisingEdge, _clk);
        }

        [HLS]
        private async void DivideProcess()
        {
            _rdy = true;
            while (true)
            {
                ProgramFlow.DoNotUnroll();

                while (!_nxt)
                {
                    ProgramFlow.DoNotUnroll();
                    await Tick;
                }

                _rdy = false;
                ProgramFlow.Barrier();
                _quotient = _dividend / _divisor;
                ProgramFlow.Barrier();
                await 10.Ticks();
                _rdy = true;
            }
        }

        private async void TestProcess()
        {
            _nxt = false;

            await Tick;

            double a = 100.0;
            double b = 1.0;

            while (true)
            {
                //ProgramFlow.DoNotUnroll();

                _dividend = SFix.FromDouble(a, iw, fw);
                _divisor = SFix.FromDouble(b, iw, fw);
                _nxt = true;
                while (_rdy)
                {
                    //ProgramFlow.DoNotUnroll();
                    await Tick;
                }
                _nxt = false;
                while (!_rdy)
                {
                    //ProgramFlow.DoNotUnroll();
                    await Tick;
                }

                Console.WriteLine(_dividend.DoubleValue + " / " + _divisor.DoubleValue + " = " + _quotient.DoubleValue);
                b = b + 1.0;
            }
        }

        public static void RunTest()
        {
            DesignContext.Reset();
            FixedPointSettings.GlobalArithSizingMode = EArithSizingMode.InSizeIsOutSize;
            var design = new TestHLS_SFixDiv(8, 32);
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(1.0, ETimeUnit.us));
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            XC6VLX240T_FF1156 fpga = new XC6VLX240T_FF1156()
            {
                SpeedGrade = ESpeedGrade._2,
                TopLevelComponent = design
            };
            fpga.Testbenches.Add(design);
            fpga.Synthesize(@".\hdl_out_TestHLS_SFixDiv", "TestHLS_SFixDiv");
        }
    }
}

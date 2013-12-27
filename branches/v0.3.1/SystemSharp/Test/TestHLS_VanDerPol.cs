using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Analysis;
using SystemSharp.Interop.Xilinx.Devices.Virtex6;

namespace Test
{
    public class TestHLS_VanDerPol : Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { internal get; set; }

        public In<StdLogicVector> Mu { internal get; set; }
        public In<StdLogicVector> Dt { internal get; set; }
        public Out<StdLogicVector> Y { internal get; set; }
        public Out<StdLogicVector> Ydot { internal get; set; }
        public Out<StdLogicVector> Ydotdot { internal get; set; }
        public Out<StdLogic> Rdy { internal get; set; }

        [AssumeConst]
        private double[] _y = new double[] { 0.0, 1.0, 0.0 };

        [HLS]
        private async void Computation()
        {
            await Tick;
            while (true)
            {
                ProgramFlow.DoNotUnroll();

                Rdy.Next = '0';
                ProgramFlow.IOBarrier();

                // y^('')-mu(1-y^2)y^'+y=0. 
                double dt = Dt.Cur.ToDouble();
                double mu = Mu.Cur.ToDouble();
                _y[2] = mu * (1 - _y[0] * _y[0]) * _y[1] - _y[0];
                _y[0] += _y[1] * dt;
                _y[1] += _y[2] * dt;
                await 39.Ticks();
                Y.Next = _y[0].ToSLV();
                Ydot.Next = _y[1].ToSLV();
                Ydotdot.Next = _y[2].ToSLV();
                ProgramFlow.IOBarrier();
                Rdy.Next = '1';
                await Tick;
                ProgramFlow.IOBarrier();
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Computation, Clk.RisingEdge, Clk);
        }
    }

    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    public class TestHLS_VanDerPol_Testbench : Component
    {
        private TestHLS_VanDerPol _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _mu = new SLVSignal(64);
        private SLVSignal _dt = new SLVSignal(64);
        private SLVSignal _y = new SLVSignal(64);
        private SLVSignal _ydot = new SLVSignal(64);
        private SLVSignal _ydotdot = new SLVSignal(64);
        private SLSignal _rdy = new SLSignal();

        public TestHLS_VanDerPol_Testbench()
        {
            _dut = new TestHLS_VanDerPol()
            {
                Clk = _clk,
                Mu = _mu,
                Dt = _dt,
                Y = _y,
                Ydot = _ydot,
                Ydotdot = _ydotdot,
                Rdy = _rdy
            };
            _clkGen = new Clock(new Time(4.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            _mu.Next = (5.0).ToSLV();
            _dt.Next = (0.1).ToSLV();
            while (true)
            {
                while (_rdy.Cur != '0')
                    await _rdy;
                while (_rdy.Cur != '1')
                    await _rdy;
                Console.WriteLine("Y = " + _y.Cur.ToDouble() + ", Y' = " + _ydot.Cur.ToDouble() + ", Y'' = " + _ydotdot.Cur.ToDouble());
            }
        }

        protected override void Initialize()
        {
            AddThread(StimulateAndTest);
        }

        public TestHLS_VanDerPol DUT { get { return _dut; } }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestHLS_VanDerPol_Testbench tb = new TestHLS_VanDerPol_Testbench();

            DesignContext.Instance.Elaborate();
            //DesignContext.Instance.Simulate(new Time(4.0, ETimeUnit.us));
            //DesignContext.Stop();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            XC6VLX240T_FF1156 fpga = new XC6VLX240T_FF1156()
            {
                SpeedGrade = ESpeedGrade._2,
                TopLevelComponent = tb.DUT
            };
            fpga.Testbenches.Add(tb);
            fpga.Pins["J9"].Map(tb.DUT.Clk);
            fpga.Synthesize(@".\hdl_out_TestHLS_VanDerPol", "TestHLS_VanDerPol");
        }
    }
}

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
using SystemSharp.Interop.Xilinx.Devices.Virtex6;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.DocGen;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    public class TestHLS_CFlow : Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { internal get; set; }

        public In<StdLogicVector> X { internal get; set; }
        public In<StdLogicVector> Y { internal get; set; }
        public Out<StdLogicVector> Z { internal get; set; }
        public Out<StdLogic> Rdy { internal get; set; }

        [HLS]
        private async void Computation()
        {
            await Tick;
            while (true)
            {
                ProgramFlow.DoNotUnroll();
                ProgramFlow.IOBarrier();
                Rdy.Next = '0';
                int x = X.Cur.IntValue;
                int y = Y.Cur.IntValue;
                int z;
                if (x == 0)
                {
                    z = y;
                }
                else
                {
                    while (y != 0)
                    {
                        ProgramFlow.DoNotUnroll();

                        if (x > y)
                            x -= y;
                        else
                            y -= x;
                    }
                    z = x;
                }
                Z.Next = StdLogicVector.FromInt(z, 32);
                await 63.Ticks();
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
    public class TestHLS_CFlow_Testbench : Component
    {
        private TestHLS_CFlow _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _x = new SLVSignal(32);
        private SLVSignal _y = new SLVSignal(32);
        private SLVSignal _z = new SLVSignal(32);
        private SLSignal _rdy = new SLSignal();

        public TestHLS_CFlow_Testbench()
        {
            _dut = new TestHLS_CFlow()
            {
                Clk = _clk,
                X = _x,
                Y = _y,
                Z = _z,
                Rdy = _rdy
            };
            _clkGen = new Clock(new Time(4.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    _x.Next = StdLogicVector.FromInt(i, 32);
                    _y.Next = StdLogicVector.FromInt(j, 32);
                    while (_rdy.Cur != '0')
                        await _rdy;
                    while (_rdy.Cur != '1')
                        await _rdy;
                    Console.WriteLine("The GCD of " + i + " and " + j + " is " + _z.Cur.IntValue);
                }
            }
        }

        protected override void Initialize()
        {
            AddThread(StimulateAndTest);
        }

        public TestHLS_CFlow DUT
        {
            get { return _dut; }
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestHLS_CFlow_Testbench tb = new TestHLS_CFlow_Testbench();

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(100.0, ETimeUnit.us));
            DesignContext.Stop();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            XC6VLX75T_FF484 fpga = new XC6VLX75T_FF484()
            {
                SpeedGrade = ESpeedGrade._2,
                TopLevelComponent = tb.DUT
            };
            fpga.Testbenches.Add(tb);
            fpga.Pins["T13"].Map(tb.DUT.Clk);
            fpga.Synthesize(@".\hdl_out_TestHLS_CFlow", "TestHLS_CFlow");
            var eng = SynthesisEngine.Create(
                DesignContext.Instance, new DocumentationProject(@".\hdl_out_TestHLS_CFlow\doc"));
            eng.Synthesize(new DocumentationGenerator());
        }
    }
}

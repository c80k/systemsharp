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
    public class TestHLS_Cordic : Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        public In<StdLogicVector> X { private get; set; }
        public Out<StdLogicVector> Sin { private get; set; }
        public Out<StdLogicVector> Cos { private get; set; }
        public Out<StdLogic> Rdy { private get; set; }

        [HLS]
        private async void Computation()
        {
            await Tick;
            while (true)
            {
                ProgramFlow.DoNotUnroll();
                ProgramFlow.IOBarrier();
                Rdy.Next = '0';
                Sin.Next = (Math.Sin(X.Cur.ToDouble())).ToSLV();
                Cos.Next = (Math.Cos(X.Cur.ToDouble())).ToSLV();
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
    public class TestHLS_Cordic_Testbench : Component
    {
        private TestHLS_Cordic _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _x = new SLVSignal(64);
        private SLVSignal _sin = new SLVSignal(64);
        private SLVSignal _cos = new SLVSignal(64);
        private SLSignal _rdy = new SLSignal();

        public TestHLS_Cordic_Testbench()
        {
            _dut = new TestHLS_Cordic()
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
            for (double i = -3.0; i < 3.0; i += 0.125)
            {
                double x = Math.PI * i;
                _x.Next = x.ToSLV();
                while (_rdy.Cur != '0')
                    await _rdy;
                while (_rdy.Cur != '1')
                    await _rdy;
                Console.WriteLine("X = " + _x.Cur.ToDouble() + ", Sin = " + _sin.Cur.ToDouble() + ", Cos = " + _cos.Cur.ToDouble());
            }
        }

        protected override void Initialize()
        {
            AddThread(StimulateAndTest);
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestHLS_Cordic_Testbench tb = new TestHLS_Cordic_Testbench();

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(100.0, ETimeUnit.us));
            DesignContext.Stop();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_TestHLS_Cordic", "TestHLS_Cordic");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.SetVHDLProfile();
            //project.SkipIPCoreSynthesis = true;

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen); ;
            project.Save();
        }
    }
}

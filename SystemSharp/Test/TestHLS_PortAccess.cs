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
    public class TestHLS_PortAccess : Component
    {
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        public In<StdLogicVector> DIn { private get; set; }
        public Out<StdLogicVector> DOut { private get; set; }

        [HLS]
        private async void Computation()
        {
            await Tick;
            while (true)
            {
                ProgramFlow.DoNotUnroll();
                DOut.Next = DIn.Cur;
                await Tick;
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Computation, Clk.RisingEdge, Clk);
        }
    }

    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    public class TestHLS_PortAccess_Testbench : Component
    {
        private TestHLS_PortAccess _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _in = new SLVSignal(32);
        private SLVSignal _out = new SLVSignal(32);

        public TestHLS_PortAccess_Testbench()
        {
            _dut = new TestHLS_PortAccess()
            {
                Clk = _clk,
                DIn = _in,
                DOut = _out               
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
                _in.Next = StdLogicVector.FromInt(i, 32);
                Console.WriteLine("in: " + _in.Cur.IntValue + ", out: " + _out.Cur.IntValue);
                await RisingEdge(_clk);
            }
        }

        protected override void Initialize()
        {
            AddThread(StimulateAndTest);
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestHLS_PortAccess_Testbench tb = new TestHLS_PortAccess_Testbench();

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(1.0, ETimeUnit.us));
            DesignContext.Stop();
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_TestHLS_PortAccess", "TestHLS_PortAccess");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.SetVHDLProfile();
            project.SkipIPCoreSynthesis = true;

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen);
            project.Save();
        }
    }
}

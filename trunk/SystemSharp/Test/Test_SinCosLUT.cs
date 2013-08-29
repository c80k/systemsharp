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
using SystemSharp.Interop.Mentor;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    public class Test_SinCosLUT_Testbench : Component
    {
        private int _xFracWidth;
        private int _yFracWidth;
        private int _pipeStages;

        private SinCosLUTCore _dut;
        private Clock _clkGen;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _x;
        private SLVSignal _sin;
        private SLVSignal _cos;
        private SLSignal _rdy = new SLSignal();

        public Test_SinCosLUT_Testbench(int lutWidth, int xFracWidth, int yFracWidth, int pipeStages)
        {
            _xFracWidth = xFracWidth;
            _yFracWidth = yFracWidth;
            _pipeStages = pipeStages;

            _x = new SLVSignal(2 + xFracWidth);
            _sin = new SLVSignal(2 + yFracWidth);
            _cos = new SLVSignal(2 + yFracWidth);

            _dut = new SinCosLUTCore(lutWidth, xFracWidth, yFracWidth, pipeStages)
            {
                Clk = _clk,
                X = _x,
                Sin = _sin,
                Cos = _cos
            };
            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _clk
            };
        }

        private async void StimulateAndTest()
        {
            DesignContext.Instance.FixPoint.DefaultRadix = 10;

            for (double i = -1.0; i <= 1.0; i += 0.0625)
            {
                var x = SFix.FromDouble(i, 2, _xFracWidth);
                _x.Next = x.SLVValue;
                //DesignContext.Wait(_pipeStages + 4);
                await NTicks(_pipeStages + 3);
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
            FixedPointSettings.GlobalDefaultRadix = 10;

            var tb = new Test_SinCosLUT_Testbench(7, 8, 9, 0);

            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(1.0, ETimeUnit.us));
            DesignContext.Stop();
            //XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_Test_SinCosLUT_Testbench", "Test_SinCosLUT_Testbench");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Virtex6);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc6vlx240t);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.ff1156);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._2);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            project.SetVHDLProfile();
            project.TwinProject = new ModelsimProject(@".\hdl_out_Test_SinCosLUT_Testbench", "Test_SinCosLUT_Testbench");

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(tb, codeGen);
            project.Save();
        }
    }
}

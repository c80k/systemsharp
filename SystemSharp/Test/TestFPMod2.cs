using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    public class Mod2TestDesign : Component
    {
        private SLSignal _clk;
        private SLVSignal _x, _r;

        private Clock _clkGen;
        private FixPMod2 _mod2;

        public Mod2TestDesign(int inIntWidth, int fracWidth, int outIntWidth)
        {
            _clk = new SLSignal();
            _x = new SLVSignal(inIntWidth + fracWidth) { InitialValue = StdLogicVector._0s(inIntWidth + fracWidth) };
            _r = new SLVSignal(outIntWidth + fracWidth) { InitialValue = StdLogicVector._0s(outIntWidth + fracWidth) };

            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns));
            Bind(() => _clkGen.Clk = _clk);

            _mod2 = new FixPMod2(inIntWidth, fracWidth, outIntWidth)
            {
                X = _x,
                R = _r
            };
        }

        private async void StimProcess()
        {
            DesignContext.Instance.FixPoint.DefaultRadix = 10;
            await Tick;

            double cur = 2.1;
            while (true)
            {
                var inval = SFix.FromDouble(cur, _mod2.InIntWidth, _mod2.FracWidth);
                _x.Next = inval.SLVValue;
                await Tick;
                var outval = SFix.FromSigned(_r.Cur.SignedValue, _mod2.FracWidth);
                var outd = outval.DoubleValue;
                var refd = Math.IEEERemainder(cur, 2.0);
                Debug.Assert(outd >= -1.0 && outd <= 1.0);
                Console.WriteLine(cur + " mod 2 == " + outd);
                cur -= 0.1;
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(StimProcess, _clk.RisingEdge, _clk);
        }

        public static void Run()
        {
            DesignContext.Reset();
            Mod2TestDesign td = new Mod2TestDesign(3, 10, 2);
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(1.0, ETimeUnit.us));

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_ALUTestDesign", "ALUTestDesign");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            project.SetVHDLProfile();

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(codeGen);
            project.Save();
        }
    }
}

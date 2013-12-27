using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    class TestConcatDesign: Component
    {
        public In<StdLogicVector> X { get; set; }
        public Out<StdLogicVector> Y1 { get; set; }
        public Out<StdLogicVector> Y2 { get; set; }
        public Out<StdLogicVector> Y3 { get; set; }

        private void Process()
        {
            Y1.Next = X.Cur[0].Concat(X.Cur[-1, 0]);
            Y2.Next = X.Cur[-1, 0].Concat(X.Cur[0]);
            Y3.Next = X.Cur[0].Concat(X.Cur[1]);
        }

        protected override void Initialize()
        {
            AddProcess(Process, X);
        }
    }

    class TestConcatTestbench : Component
    {
        private SLVSignal _x = new SLVSignal(32);
        private SLVSignal _y1 = new SLVSignal(1);
        private SLVSignal _y2 = new SLVSignal(1);
        private SLVSignal _y3 = new SLVSignal(2);
        private TestConcatDesign _design;

        public TestConcatTestbench()
        {
            _design = new TestConcatDesign()
            {
                X = _x,
                Y1 = _y1,
                Y2 = _y2,
                Y3 = _y3
            };
        }

        public static void Run()
        {
            DesignContext.Reset();
            var td = new TestConcatTestbench();
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(0.5, ETimeUnit.us));

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_TestConcatDesign", "TestConcatDesign");
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

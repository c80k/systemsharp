using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    public class FileWriterTestbench: Component
    {
        private void Process()
        {
            var wr = new StreamWriter("outputs.txt");
            wr.Write(1);
            wr.Write(";");
            wr.Write(2);
            wr.WriteLine(";");
            wr.Close();
            DesignContext.ExitProcess();
        }

        protected override void Initialize()
        {
            AddThread(Process);
        }

        public static void RunTest()
        {
            DesignContext.Reset();
            var tb = new FileWriterTestbench();
            DesignContext.Instance.Elaborate();

            DesignContext.Instance.Simulate(new Time(0.5, ETimeUnit.us));

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_WriteFile", "FileWriterDesign");
            project.ISEVersion = EISEVersion._13_2;
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

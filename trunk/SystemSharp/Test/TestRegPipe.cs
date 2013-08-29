using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Mentor;
using SystemSharp.Interop.Xilinx;

namespace Test
{
    public class TestRegPipe
    {
        public static void RunTest()
        {
            DesignContext.Reset();
            RegPipe dut = new RegPipe(100, 32, true)
            {
                Clk = new SLSignal(),
                Din = new SLVSignal(32),
                Dout = new SLVSignal(32),
                En = new SLSignal()
            };
            DesignContext.Instance.Elaborate();
            var fpga = new DefaultXilinxDevice()
            {
                SpeedGrade = ESpeedGrade._2,
                TopLevelComponent = dut
            };
            fpga.SetDevice(EDevice.xc5vlx110t);
            fpga.SetPackage(EPackage.ff1136);
            fpga.SpeedGrade = ESpeedGrade._2;
            fpga.Synthesize(@"c:\temp\RegPipeTest", "RegPipeTest",
                new ModelsimProject(@"c:\temp\RegPipeTest", "RegPipeTest"));
        }
    }
}

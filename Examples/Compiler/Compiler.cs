/** System# example: Compiler
 * 
 * This example demonstrates the compilation of C# code to some different
 * assembler code. The result can be used for further transformations, such as
 * scheduling the code for a specific target hardware.
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Assembler;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.SystemCGen;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;

namespace Example_Compiler
{
    /// <summary>
    /// An instance of this component contains the process to be compiled.
    /// This demo implementation computes the greatest common divisor (GCD) of
    /// two numbers.
    /// </summary>
    class GCD: Component
    {
        public In<int> A { private get; set; }
        public In<int> B { private get; set; }
        public Out<int> R { private get; set; }

        private void Euclid()
        {
            int a = A.Cur;
            int b = B.Cur;
            if (a == 0)
                R.Next = 0;
            else
            {
                while (b != 0)
                {
                    if (a > b)
                        a -= b;
                    else
                        b -= a;
                    R.Next = a;
                }
            }
        }

        protected override void Initialize()
        {
            AddProcess(Euclid, A, B);
        }
    }

    /// <summary>
    /// A simple testbench to verify the GCD implementation.
    /// </summary>
    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    class Testbench : Component
    {
        public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

        private Clock _clkGen;
        private GCD _gcd;

        private SLSignal _clk = new SLSignal();
        private Signal<int> _a = new Signal<int>();
        private Signal<int> _b = new Signal<int>();
        private Signal<int> _r = new Signal<int>();

        public Testbench()
        {
            _clkGen = new Clock(ClockPeriod)
            {
                Clk = _clk
            };
            _gcd = new GCD()
            {
                A = _a,
                B = _b,
                R = _r
            };
        }

        private async void Test()
        {
            await Tick;
            for (int a = 0; a < 10; a++)
            {
                for (int b = 0; b < 10; b++)
                {
                    _a.Next = a;
                    _b.Next = b;
                    await Tick;
                    Console.WriteLine("GCD of " + a + " and " + b + " is " + _r.Cur);
                }
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Test, _clk.RisingEdge, _clk);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Testbench tb = new Testbench();
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(101 * Testbench.ClockPeriod);

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project_SC = new XilinxProject(@".\SystemC_output", "SimpleCounter");
            project_SC.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            project_SC.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            project_SC.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            project_SC.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            project_SC.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);

            SystemCGenerator codeGen_SC = new SystemCGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project_SC).Synthesize(codeGen_SC);
            project_SC.Save();
            
            DesignContext.Instance.CompleteAnalysis();

            // The actual compilation steps start here.
            ProcessDescriptor euclid = tb.Descriptor.FindComponent("_gcd").FindProcess("Euclid");
            XIL3Function asm = euclid.Implementation.Compile(DefaultInstructionSet.Instance).ToXIL3();
            Console.WriteLine(asm);
        }
    }
}

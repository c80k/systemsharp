#define RUNANALYSIS

/** System# example: Counter
 * 
 * This example demonstrates a simple n-bit counter.
 * 
 * If you are new to System#, this example might serve as a good starting 
 * point. You will see how to...
 *  - declare a module: Derive a class from Component
 *  - define a component interface having input/output ports: see In<T>/Out<T>
 *  - work with hardware datatypes: See StdLogic/StdLogicVector. They behave
 *    pretty much the same way you known them from VHDL.
 *  - Declare a signal (see SLSignal, SLVSignal)
 *  - Define an instantiate a process (see AddProcess)
 *  - Simulate a design and translate it into VHDL (see Main)
 *    */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Synthesis.SystemCGen;

namespace SystemSharp.Examples.Counter
{
    class Bundle
    {
        /// <summary>
        /// This class models a binary, clock-synchronous counter.
        /// </summary>
        class Counter : Component
        {
            /// <summary>
            /// The clock input
            /// </summary>
            public In<StdLogic> Clk { private get; set; }

            /// <summary>
            /// The counter value output
            /// </summary>
            public Out<StdLogicVector> Ctr { private get; set; }

            /// <summary>
            /// An internal signal to store the current counter value.
            /// </summary>
            private SLVSignal _ctr;

            /// <summary>
            /// Creates an instance of a Counter component.
            /// </summary>
            /// <param name="width">the desired bit-width of the counter</param>
            public Counter(int width)
            {
                _ctr = new SLVSignal(StdLogicVector._1s(width));
            }

            /// <summary>
            /// This process performs the signal updates.
            /// </summary>
            [TransformIntoFSM]
            private async void Processing()
            {
                await Tick;
                Ctr.Next = _ctr.Cur;
                _ctr.Next = _ctr.Cur + "1";
            }

            /// <summary>
            /// All processes must be instantiated inside the overridden 
            /// Initialize method.
            /// </summary>
            protected override void Initialize()
            {
                // Instantiate method Processing as a process which is sensitive to the Clk port.
                AddClockedThread(Processing, Clk.RisingEdge, Clk);
            }
        }

        /// <summary>
        /// The testbench for our counter. It instantiates a clock generator, the
        /// counter and a logging component which is used to display the current
        /// counter value at the console.
        /// </summary>
        class SimpleCounterTestbench : Component
        {
            public static readonly int CounterSize = 10;
            public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

            /// <summary>
            /// The internal clock signal
            /// </summary>
            private SLSignal _clk = new SLSignal();

            /// <summary>
            /// The internal signal for storing the counter value
            /// </summary>
            private SLVSignal _count = new SLVSignal(CounterSize);

            private Clock _m_clk;
            private Counter _m_ctr;
            private ConsoleLogger<StdLogicVector> _m_logger;

            /// <summary>
            /// Constructs a testbench instance. All other instances are created 
            /// and bound here.
            /// </summary>
            public SimpleCounterTestbench()
            {
                // Create a clock generator and bind its output to the clock signal.
                _m_clk = new Clock(ClockPeriod)
                {
                    Clk = _clk
                };
                // Create a counter and bind it the the clock and the counter value signal.
                _m_ctr = new Counter(CounterSize)
                {
                    Clk = _clk,
                    Ctr = _count
                };
                // Create a logger which will display the counter value at the console output.
                _m_logger = new ConsoleLogger<StdLogicVector>()
                {
                    DataIn = _count
                };
            }

        }

        class Program
        {
            static void Main(string[] args)
            {
                ///default values
                int cycles = 100;

                ///elaborate
                SimpleCounterTestbench tb = new SimpleCounterTestbench();
                DesignContext.Instance.Elaborate();

                ///print out config
                Console.WriteLine("#cycles: " + cycles);

                ///simulate
                DesignContext.Instance.Simulate(cycles * SimpleCounterTestbench.ClockPeriod);

                ///notify completion
                Console.WriteLine("Done.  [ #cycles = " + cycles + " ]");


#if RUNANALYSIS
            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_output", "SimpleCounter");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(codeGen);
            project.Save();

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
#endif
            }
        }
    }
}
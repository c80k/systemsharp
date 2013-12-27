#undef RUNANALYSIS

/** System# example: ManyCounters
 * 
 * This example demonstrates many simple n-bit counters counting in parallel.
 * 
 */

using System;
using System.Threading;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;

namespace SystemSharp.Examples.ManyCounters
{
    class Bundle
    {
        static int Cycles = 100;
        static long Start;

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
            public static int NumCounters = 10;
            public static readonly int CounterSize = 10;
            public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

            /// <summary>
            /// The internal clock signal
            /// </summary>
            private SLSignal _clk = new SLSignal();

            private Clock _m_clk;

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

                for (int i = 0; i < NumCounters; i++)
                {
                    //The internal signal for storing the counter value
                    var _ctr = new SLVSignal(CounterSize);

                    // Create a counter and bind it the the clock and the counter value signal.
                    var _m_ctr = new Counter(CounterSize)
                    {
                        Clk = _clk,
                        Ctr = _ctr
                    };

                    // Create a logger which will display the counter value at the console output.
                    var _m_logger = new ConsoleLogger<StdLogicVector>()
                    {
                        DataIn = _ctr
                    };
                }
            }
        }

        static void Main(string[] args)
        {
            ///run simulation
            Program.Run();

            ///calculate duration
            long duration = System.DateTime.Now.Ticks - Start;

            ///notify completion
            DesignContext.WriteLine("Done.  [ #counters = " + SimpleCounterTestbench.NumCounters + ", #cycles = " + Cycles + " ]");

            ///wait for user input
            Console.ReadLine();

            ///return exit code
            ;
        }

        class Program
        {
            public static void Run()
            {
                SimpleCounterTestbench tb = new SimpleCounterTestbench();
                DesignContext.Instance.Elaborate();

                ///remember start time
                Start = System.DateTime.Now.Ticks;

                DesignContext.Instance.Simulate(Cycles * SimpleCounterTestbench.ClockPeriod);

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
#endif
            }
        }
    }
}
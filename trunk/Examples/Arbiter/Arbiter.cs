/** System# example: Bus arbiter
 * 
 * This example demonstrates a simple bus arbitration where multiple bus 
 * masters issue arbitration requests and hold the grant for a variable amount
 * of time. 
 * 
 * The given example is only intended for simulation, so do not 
 * expect synthesizable VHDL.
 * 
 * The main purpose of this example is to show how...
 *  - multiple instances of a component class can be turned into a collection
 *    (see: ComponentCollection)
 *  - vectorized signals are used to realize 1:n connection schemes between
 *    components (see: Signal1D)
 *    */

#define RUNANALYSIS

using System;
using System.Collections.Generic;
using System.Threading;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Synthesis.SystemCGen;

namespace Example_Arbiter
{
    class Bundle
    {
        static int Cycles = 100;
        static long Start;

        /// <summary>
        /// This is component models the bus arbiter. It incorporates priority-
        /// based arbitration and clock-synchronouos double-handshake.
        /// </summary>
        /// <remarks>
        /// The Arbiter implementation will even produce synthesizable HDL. 
        /// However, all other design components won't.
        /// </remarks>
        [ComponentPurpose(EComponentPurpose.SimulationAndSynthesis)]
        class Arbiter : Component
        {
            /// <summary>
            /// The clock input signal
            /// </summary>
            public In<StdLogic> Clk { private get; set; }

            /// <summary>
            /// A vectorized input port for the request signals from all masters.
            /// </summary>
            public XIn<StdLogic[], InOut<StdLogic>> Request { private get; set; }

            /// <summary>
            /// A vectorized output port for the grants to all masters.
            /// </summary>
            public XOut<StdLogic[], InOut<StdLogic>> Grant { private get; set; }

            private int _count;
            private Signal<int> _curGrant = new Signal<int>() { InitialValue = -1 };

            /// <summary>
            /// Creates an instance of the Arbiter component.
            /// </summary>
            /// <param name="count">the amount of masters to be arbitrated</param>
            public Arbiter(int count)
            {
                _count = count;
            }

            /// <summary>
            /// The actual arbitration process.
            /// </summary>
            private void Arbitrate()
            {
                if (Clk.RisingEdge())
                {
                    if (_curGrant.Cur == -1)
                    {
                        for (int i = 0; i < _count; i++)
                        {
                            if (Request[i].Cur == '1')
                            {
                                _curGrant.Next = i;
                                Grant[i].Next = '1';
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (Request[_curGrant.Cur].Cur == '0')
                        {
                            Grant[_curGrant.Cur].Next = '0';
                            _curGrant.Next = -1;
                        }
                    }
                }
            }

            /// <summary>
            /// All processes must be instantiated inside the Initialize method.
            /// </summary>
            protected override void Initialize()
            {
                AddProcess(Arbitrate, Clk);
            }
        }

        /// <summary>
        /// This class models a bus master. It issues an arbitration request, then
        /// waits for the respective grant, holds the bus for a pre-defined busy 
        /// time and afterwards restarts its operation after a pre-defined idle
        /// time.
        /// </summary>
        [ComponentPurpose(EComponentPurpose.SimulationOnly)]
        class BusMaster : Component
        {
            public In<StdLogic> Clk { private get; set; }
            public In<StdLogic> Grant { private get; set; }
            public Out<StdLogic> Request { private get; set; }

            private int _deviceNum;
            private int _busyTime;
            private int _idleTime;

            /// <summary>
            /// Constructs an instance of a BusMaster component.
            /// </summary>
            /// <param name="deviceNum">an associated device number (only used for diagnostic output)</param>
            /// <param name="busyTime">the busy time in multiples of a clock cycle</param>
            /// <param name="idleTime">the idle time in muktiples of a clock cycle</param>
            public BusMaster(int deviceNum, int busyTime, int idleTime)
            {
                _deviceNum = deviceNum;
                _busyTime = busyTime;
                _idleTime = idleTime;
            }

            [TransformIntoFSM]
            private async void BusMasterProcess()
            {
                Request.Next = '0';

                await Tick;
                do
                {
                    DesignContext.WriteLine(DesignContext.Instance.CurTime + ": " + _deviceNum + " request");
                    Request.Next = '1';

                    do
                    {
                        await Tick;
                    } while (Grant.Cur != '1');

                    DesignContext.WriteLine(DesignContext.Instance.CurTime + ": " + _deviceNum + " grant");

                    for (int i = 0; i < _busyTime; i++)
                    {
                        await Tick;
                    }

                    DesignContext.WriteLine(DesignContext.Instance.CurTime + ": " + _deviceNum + " release");
                    Request.Next = '0';

                    for (int i = 0; i < _idleTime; i++)
                    {
                        await Tick;
                    }

                } while (true);
            }

            protected override void Initialize()
            {
                AddClockedThread(BusMasterProcess, Clk.RisingEdge, Clk);
            }
        }

        /// <summary>
        /// This is the testbench for our design. It instantiates a clock 
        /// generator, an arbiter, several bus masters and interconnects
        /// them.
        /// </summary>
        [ComponentPurpose(EComponentPurpose.SimulationOnly)]
        class Testbench : Component
        {
            public static int NumMasters = 2;
            public static readonly Time ClockCycle = new Time(10.0, ETimeUnit.ns);

            private SLSignal _clk = new SLSignal();
            private VSignal<StdLogic> _request = new VSignal<StdLogic>(NumMasters, i => new SLSignal())
            {
                InitialValue = Arrays.Same<StdLogic>('0', NumMasters)
            };

            private VSignal<StdLogic> _grant = new VSignal<StdLogic>(NumMasters, i => new SLSignal())
            {
                InitialValue = Arrays.Same<StdLogic>('0', NumMasters)
            };

            private Clock _clkgen;
            private Arbiter _arbiter;
            private ComponentCollection _masters;

            private IEnumerable<Component> CreateBusMasters()
            {
                for (int i = 0; i < NumMasters; i++)
                {
                    BusMaster master = new BusMaster(i, i + 1, 8 * (NumMasters - i + 1));
                    object oi = (object)i;
                    Bind(() =>
                    {
                        master.Clk = _clk;
                        master.Request = _request[(int)oi];
                        master.Grant = _grant[(int)oi];
                    });
                    yield return master;
                }
            }

            public Testbench()
            {
                _clkgen = new Clock(ClockCycle)
                {
                    Clk = _clk
                };
                _arbiter = new Arbiter(NumMasters)
                {
                    Clk = _clk,
                    Request = _request,
                    Grant = _grant
                };
                _masters = new ComponentCollection(CreateBusMasters());
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("#masters:" + Testbench.NumMasters);

            ///run simulation
            Program.Run();

            ///notify completion
            Console.WriteLine("Done.  [ #masters = " + Testbench.NumMasters + ", #cycles = " + Cycles + " ]");
        }

        public class Program
        {
            public static void Run()
            {
                Testbench tb = new Testbench();
                DesignContext.Instance.Elaborate();
                
                ///remember start time
                Start = System.DateTime.Now.Ticks;

                DesignContext.Instance.Simulate(Cycles * Testbench.ClockCycle);

#if RUNANALYSIS
                // Now convert the design to VHDL and embed it into a Xilinx ISE project
                XilinxProject project = new XilinxProject(@".\hdl_output", "Arbiter");
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
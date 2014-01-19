#define RUNANALYSIS

/** System# example: Serialization/deserialization of data
 * 
 * This example demonstrates how the [TransformIntoFSM] attribute is used to
 * turn a clocked thread having Wait() statements into synthesizable VHDL.
 * 
 * The design objective is as follows: A BitDeeserializer is connected to a 
 * BitDeserializer. 
 *    */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Synthesis.SystemCGen;

namespace Example_SerParBits
{
    /// <summary>
    /// The BitSerializer models a simple parallel/serial converter.
    /// It takes a data word as input and turns it into a serial bit stream.
    /// </summary>
    class BitSerializer : Component
    {
        /// <summary>
        /// Clock input
        /// </summary>
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// Sync strobe: '1' initiates a conversion.
        /// </summary>
        public In<StdLogic> SyncIn { private get; set; }

        /// <summary>
        /// Parallel input word
        /// </summary>
        public In<StdLogicVector> ParIn { private get; set; }

        /// <summary>
        /// '1' acknowledges the beginning of the transfer.
        /// </summary>
        public Out<StdLogic> SyncOut { private get; set; }

        /// <summary>
        /// Serial output
        /// </summary>
        public Out<StdLogic> SerOut { private get; set; }

        private int _size;

        /// <summary>
        /// Constructs a BitSerializer instance
        /// </summary>
        /// <param name="size">The length of a serialized data word</param>
        public BitSerializer(int size)
        {
            _size = size;
        }

        protected override void Initialize()
        {
            AddClockedThread(Serialize, Clk.RisingEdge, Clk);
        }

        [TransformIntoFSM]
        private async void Serialize()
        {
            await Tick;
            do
            {
                StdLogicVector cap;

                SyncOut.Next = '0';

                cap = ParIn.Cur;
                while (!SyncIn.Cur)
                {
                    await Tick;
                    cap = ParIn.Cur;
                }

                SyncOut.Next = '1';
                int i = 0;
                do
                {
                    SerOut.Next = cap[i];
                    await Tick;
                    SyncOut.Next = '0';
                } while (++i < _size);

            } while (true);
        }
    }

    /// <summary>
    /// The BitDeserializer component models a simple serial/parallel converter.
    /// It takes a serial input stream as input and assembles a parallel data word from it.
    /// </summary>
    class BitDeserializer : Component
    {
        /// <summary>
        /// Clock input
        /// </summary>
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// Sync input strobe
        /// </summary>
        public In<StdLogic> SyncIn { private get; set; }

        /// <summary>
        /// Serial data input
        /// </summary>
        public In<StdLogic> SerIn { private get; set; }

        /// <summary>
        /// Acknowledges the beginning of a serial capture sequence.
        /// </summary>
        public Out<StdLogic> SyncOut { private get; set; }

        /// <summary>
        /// Parallel output
        /// </summary>
        public Out<StdLogicVector> ParOut { private get; set; }

        private int _size;
        private VSignal<StdLogic> _shiftReg;

        /// <summary>
        /// Constructs a BitDeserializer component
        /// </summary>
        /// <param name="size">The data word length</param>
        public BitDeserializer(int size)
        {
            _size = size;
            _shiftReg = new VSignal<StdLogic>(size, i => new Signal<StdLogic>());
        }

        [TransformIntoFSM]
        private async void Deserialize()
        {
            await Tick;
            do
            {
                SyncOut.Next = '0';

                while (!SyncIn.Cur)
                    await Tick;

                for (int i = _size - 1; i >= 0; i--)
                {
                    _shiftReg[i].Next = SerIn.Cur;
                    await Tick;
                }

                SyncOut.Next = '1';
                ParOut.Next = _shiftReg.Cur.Concat<StdLogicVector, StdLogic>();
                await Tick;
            } while (true);
        }

        protected override void Initialize()
        {
            AddClockedThread(Deserialize, Clk.RisingEdge, Clk);
        }
    }

    /// <summary>
    /// The demo design
    /// </summary>
    class Design : Component
    {
        public In<StdLogic> Clk { private get; set; }
        public In<StdLogic> SyncIn { private get; set; }
        public In<StdLogicVector> ParIn { private get; set; }
        public Out<StdLogic> SyncOut { private get; set; }
        public Out<StdLogicVector> ParOut { private get; set; }

        public static readonly int DataWidth = 27;

        private BitSerializer _ser;
        private BitDeserializer _deser;

        private SLSignal _syncMid = new SLSignal() { InitialValue = '0' };
        private SLSignal _serMid = new SLSignal() { InitialValue = '0' };

        public Design()
        {
        }

        protected override void PreInitialize()
        {
            _ser = new BitSerializer(DataWidth)
            {
                Clk = Clk,
                SyncIn = SyncIn,
                ParIn = ParIn,
                SyncOut = _syncMid,
                SerOut = _serMid
            };
            _deser = new BitDeserializer(DataWidth)
            {
                Clk = Clk,
                SyncIn = _syncMid,
                SerIn = _serMid,
                SyncOut = SyncOut,
                ParOut = ParOut
            };
        }

        protected override void Initialize()
        {
        }
    }

    /// <summary>
    /// The testbench of our demo design.
    /// </summary>
    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    class Testbench: Component
    {
        public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

        private Clock _clock;
        private Design _design;

        private SLSignal _clk = new SLSignal() { InitialValue = '0' };
        private SLSignal _syncIn = new SLSignal() { InitialValue = '0' };
        private SLSignal _syncOut = new SLSignal() { InitialValue = '0' };
        private SLVSignal _parIn = new SLVSignal(Design.DataWidth) { InitialValue = StdLogicVector._0s(Design.DataWidth) };
        private SLVSignal _parOut = new SLVSignal(Design.DataWidth) { InitialValue = StdLogicVector._0s(Design.DataWidth) };

        public Testbench()
        {
            _clock = new Clock(ClockPeriod)
            {
                Clk = _clk
            };
            _design = new Design()
            {
                Clk = _clk,
                SyncIn = _syncIn,
                ParIn = _parIn,
                SyncOut = _syncOut,
                ParOut = _parOut
            };
        }

        [TransformIntoFSM]
        private async void TestProcess()
        {
            await Tick;
            int curNumber = 0x12345678;
            do
            {
                _syncIn.Next = '1';
                StdLogicVector next = StdLogicVector.FromLong(curNumber, Design.DataWidth);
                _parIn.Next = next;
                Console.WriteLine(DesignContext.Instance.CurTime + ": input number " + next.IntValue);
                await Tick;
                _syncIn.Next = '0';
                while (!_syncOut.Cur)
                    await Tick;
                Console.WriteLine(DesignContext.Instance.CurTime + ": output number " + _parOut.Cur.IntValue);
                curNumber *= 2;
            } while (true);
        }

        protected override void Initialize()
        {
            AddClockedThread(TestProcess, _clk.RisingEdge, _clk);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Testbench tb = new Testbench();
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(10 * (Design.DataWidth + 3) * Testbench.ClockPeriod);
            //Console.Read();

#if RUNANALYSIS
            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_output", "BitSerializer");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(codeGen);
            project.Save();

            //// Now convert the design to VHDL and embed it into a Xilinx ISE project
            //XilinxProject project_SC = new XilinxProject(@".\SystemC_output", "SimpleCounter");
            //project_SC.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            //project_SC.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            //project_SC.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            //project_SC.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            //project_SC.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);

            //SystemCGenerator codeGen_SC = new SystemCGenerator();
            //SynthesisEngine.Create(DesignContext.Instance, project_SC).Synthesize(codeGen_SC);
            //project_SC.Save();

#endif
        }
    }
}

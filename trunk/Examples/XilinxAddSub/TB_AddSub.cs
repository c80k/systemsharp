using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
namespace Addsub
{
    class Testbench_XilinxAdderSubtracter : Component
    {
        // public static readonly int width = 10;
        public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

        private SLSignal _clk = new SLSignal();
        private SLVSignal _A = new SLVSignal(StdLogicVector._1s(20));
        private SLVSignal _B = new SLVSignal(StdLogicVector._1s(20));
        private SLVSignal _S = new SLVSignal(StdLogicVector._1s(20));
        private SLSignal _C_out = new SLSignal()
        {
            InitialValue = '0'
        };
        private SLSignal _CE = new SLSignal()
        {
            InitialValue = '1'
        };

        private Clock _m_clk;
        private XilinxAdderSubtracter _m_signal;
        private ConsoleLogger<StdLogicVector> _m_logger1;
        private ConsoleLogger<StdLogicVector> _m_logger2;
        private ConsoleLogger<StdLogic> _m_logger3;
        private ConsoleLogger<StdLogicVector> _m_logger4;
        private ConsoleLogger<StdLogic> _m_logger5;

        public async void Process()
        {
            _A.Next = "10001000100010001000";
            _B.Next = "01000100010001000100";
            _CE.Next = '1';
            await Tick;

            /*if (_S.Cur.Equals( _A.Cur + _B.Cur))
            {
                Console.WriteLine(" gut");
            }*/
        }

        protected override void Initialize()
        {
            AddClockedThread(Process, _clk.RisingEdge, _clk);
        }

        public Testbench_XilinxAdderSubtracter()
        {
            _m_clk = new Clock(ClockPeriod)
            {
                Clk = _clk
            };

            _m_signal = new XilinxAdderSubtracter()
            {
                CLK = _clk,
                A = _A,
                B = _B,
                S = _S,
               // CE = _CE,
                //C_out = _C_out,
            };

            _m_logger4 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _S
            };

            _m_logger1 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _A

            };
            _m_logger2 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _B

            };
            _m_logger3 = new ConsoleLogger<StdLogic>()
            {
                DataIn = _C_out

            };
            _m_logger5 = new ConsoleLogger<StdLogic>()
            {
                DataIn = _CE

            };
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            Testbench_XilinxAdderSubtracter tb = new Testbench_XilinxAdderSubtracter();
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(100 * Testbench_XilinxAdderSubtracter.ClockPeriod);
            //DesignContext.Instance.Simulate(10 * (Testbench_zaehler.DataWidth + 3) * Testbench_zaehler.ClockPeriod);

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_output", "XilinxAdderSubtracter");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(codeGen);
            project.Save();
        }
    }
    
}

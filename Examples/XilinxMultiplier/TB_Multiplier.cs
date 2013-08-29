using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Multiplier
{

    class Tesbernch_Multiplier : Component
    {
        public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);
        private SLSignal _clk = new SLSignal();
        private SLVSignal _A = new SLVSignal(StdLogicVector._1s(18));
        private SLVSignal _B = new SLVSignal(StdLogicVector._1s(18));
        private SLVSignal _P = new SLVSignal(StdLogicVector._1s(36))
        {
            InitialValue = "000000000000000000000000000000000000"
        };
        private Clock _m_clk;
        private XilinxMultiplier _m_signal;
        private ConsoleLogger<StdLogicVector> _m_logger1;
        private ConsoleLogger<StdLogicVector> _m_logger2;
        private ConsoleLogger<StdLogicVector> _m_logger3;

        public async void Process()
        {
            _A.Next = "011000000001100111";
            _B.Next = "000000000000100111";
            await Tick;
        }

        protected override void Initialize()
        {
            AddClockedThread(Process, _clk.RisingEdge, _clk);
        }

        public Tesbernch_Multiplier()
        {
            _m_clk = new Clock(ClockPeriod)
            {
                Clk = _clk
            };

            _m_signal = new XilinxMultiplier()
            {
                CLK = _clk,
                A = _A,
                B = _B,
                P = _P,

            };

            _m_logger1 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _A
            };

            _m_logger2 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _B

            };
            _m_logger3 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _P

            };
        }





        class Program
        {
            static void Main(string[] args)
            {
                Tesbernch_Multiplier tb = new Tesbernch_Multiplier();
                DesignContext.Instance.Elaborate();
                DesignContext.Instance.Simulate(100 * Tesbernch_Multiplier.ClockPeriod);
                //DesignContext.Instance.Simulate(10 * (testbench_XilinxMultiplier_.DataWidth + 3) * testbench_XilinxMultiplier_.ClockPeriod);

                // Now convert the design to VHDL and embed it into a Xilinx ISE project
                XilinxProject project = new XilinxProject(@".\hdl_output", "XilinxMultiplier");
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
}

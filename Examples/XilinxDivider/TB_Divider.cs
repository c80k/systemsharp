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

namespace Divider
{
    class testbench_XilinxDivider : Component
    {
        public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

        private SLSignal _clk = new SLSignal();
        private SLVSignal _DIVIDEND = new SLVSignal(StdLogicVector._1s(16));
        private SLVSignal _DIVISOR = new SLVSignal(StdLogicVector._1s(16));
        private SLVSignal _QUOTIENT = new SLVSignal(StdLogicVector._1s(16))
        {
            InitialValue = "0000000000000000"
        };
        private SLVSignal _FRACTION = new SLVSignal(StdLogicVector._1s(16))
        {
            InitialValue = "0000000000000000"
        };
        // private SLVSignal _Fraction = new SLVSignal(StdLogicVector._1s(32));


        private Clock _m_clk;
        private XilinxDivider _m_signal; 
        private ConsoleLogger<StdLogicVector> _m_logger1;
        private ConsoleLogger<StdLogicVector> _m_logger2;
        private ConsoleLogger<StdLogicVector> _m_logger3;

        public async void Process()
        {
            _DIVISOR.Next   = "0000000001100111";
            _DIVIDEND.Next  = "0000000000001001";
            await Tick;
        }

        protected override void Initialize()
        {
            AddClockedThread(Process, _clk.RisingEdge, _clk);
        }

        public testbench_XilinxDivider()
        {
            _m_clk = new Clock(ClockPeriod)
            {
                Clk = _clk
            };

            _m_signal = new XilinxDivider()
            {
                CLK = _clk,
                DIVIDEND = _DIVIDEND,
                DIVISOR = _DIVISOR,
                QUOTIENT = _QUOTIENT,
                FRACTIONAL = _FRACTION
            };

            _m_logger1 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _DIVIDEND
            };

            _m_logger2 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _DIVISOR

            };
            _m_logger3 = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _QUOTIENT

            };

        }



        class Program
        {
            static void Main(string[] args)
            {
                testbench_XilinxDivider tb = new testbench_XilinxDivider();
                DesignContext.Instance.Elaborate();
                DesignContext.Instance.Simulate(100 * testbench_XilinxDivider.ClockPeriod);
                //DesignContext.Instance.Simulate(10 * (testbench_XilinxDivider_.DataWidth + 3) * testbench_XilinxDivider_.ClockPeriod);

                // Now convert the design to VHDL and embed it into a Xilinx ISE project
                XilinxProject project = new XilinxProject(@".\hdl_output", "XilinxDivider");
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

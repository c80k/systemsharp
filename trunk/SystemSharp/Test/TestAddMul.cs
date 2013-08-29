using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.Devices.Virtex6;

namespace Test
{
    public class TestAddMul0: Component
    {
        public In<SFix> A { private get; set; }
        public In<SFix> B { private get; set; }
        public In<SFix> C { private get; set; }
        public In<SFix> D { private get; set; }
        public Out<SFix> R { private get; set; }

        private void Compute()
        {
            R.Next = A.Cur * B.Cur + C.Cur * D.Cur;
        }

        protected override void Initialize()
        {
            AddProcess(Compute, A, B, C, D);
        }

        public static void RunTest()
        {
            DesignContext.Reset();
            FixedPointSettings.GlobalArithSizingMode = EArithSizingMode.VHDLCompliant;

            var a = SFix.FromDouble(1.0, 8, 10);
            var b = SFix.FromDouble(2.0, 8, 10);
            var c = SFix.FromDouble(3.0, 8, 10);
            var d = SFix.FromDouble(4.0, 8, 10);

            TestAddMul0 dut = new TestAddMul0()
            {
                A = new Signal<SFix>() { InitialValue = a },
                B = new Signal<SFix>() { InitialValue = b },
                C = new Signal<SFix>() { InitialValue = c },
                D = new Signal<SFix>() { InitialValue = d },
                R = new Signal<SFix>() { InitialValue = a * b + c * d }
            };

            DesignContext.Instance.Elaborate();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            XC6VLX240T_FF1156 fpga = new XC6VLX240T_FF1156()
            {
                SpeedGrade = ESpeedGrade._2,
                TopLevelComponent = dut
            };
            fpga.Synthesize(@".\hdl_out_TestAddMul0", "TestAddMul0");
        }
    }

    public class TestAddMul1 : Component
    {
        public In<StdLogic> Clk { private get; set; }
        public In<SFix> A { private get; set; }
        public In<SFix> B { private get; set; }
        public In<SFix> C { private get; set; }
        public In<SFix> D { private get; set; }
        public Out<SFix> R { private get; set; }

        private Signal<SFix> _tmp1 = new Signal<SFix>();
        private Signal<SFix> _tmp2 = new Signal<SFix>();

        private void Compute()
        {
            if (Clk.RisingEdge())
            {
                _tmp1.Next = A.Cur * B.Cur;
                _tmp2.Next = C.Cur * D.Cur;
                R.Next = _tmp1.Cur + _tmp2.Cur;
            }
        }

        protected override void PreInitialize()
        {
            _tmp1.InitialValue = ((Signal<SFix>)A).InitialValue * ((Signal<SFix>)B).InitialValue;
            _tmp2.InitialValue = ((Signal<SFix>)C).InitialValue * ((Signal<SFix>)D).InitialValue;
        }

        protected override void Initialize()
        {
            AddProcess(Compute, Clk);
        }

        public static void RunTest()
        {
            DesignContext.Reset();
            FixedPointSettings.GlobalArithSizingMode = EArithSizingMode.VHDLCompliant;

            var a = SFix.FromDouble(1.0, 8, 10);
            var b = SFix.FromDouble(2.0, 8, 10);
            var c = SFix.FromDouble(3.0, 8, 10);
            var d = SFix.FromDouble(4.0, 8, 10);

            TestAddMul1 dut = new TestAddMul1()
            {
                Clk = new SLSignal(),
                A = new Signal<SFix>() { InitialValue = a },
                B = new Signal<SFix>() { InitialValue = b },
                C = new Signal<SFix>() { InitialValue = c },
                D = new Signal<SFix>() { InitialValue = d },
                R = new Signal<SFix>() { InitialValue = a * b + c * d }
            };

            DesignContext.Instance.Elaborate();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            XC6VLX240T_FF1156 fpga = new XC6VLX240T_FF1156()
            {
                SpeedGrade = ESpeedGrade._2,
                TopLevelComponent = dut
            };
            fpga.Synthesize(@".\hdl_out_TestAddMul1", "TestAddMul1");
        }
    }

    public class TestAddMul2 : Component
    {
        private enum State
        {
            Mul1,
            Mul2,
            Add
        }

        public In<StdLogic> Clk { private get; set; }
        public In<SFix> A { private get; set; }
        public In<SFix> B { private get; set; }
        public In<SFix> C { private get; set; }
        public In<SFix> D { private get; set; }
        public Out<SFix> R { private get; set; }

        private Signal<State> _state = new Signal<State>();
        private Signal<SFix> _tmp1 = new Signal<SFix>();
        private Signal<SFix> _tmp2 = new Signal<SFix>();

        private void Compute()
        {
            if (Clk.RisingEdge())
            {
                switch (_state.Cur)
                {
                    case State.Mul1:
                        _tmp1.Next = A.Cur * B.Cur;
                        _state.Next = State.Mul2;
                        break;

                    case State.Mul2:
                        _tmp2.Next = C.Cur * D.Cur;
                        _state.Next = State.Add;
                        break;

                    case State.Add:
                        R.Next = _tmp1.Cur + _tmp2.Cur;
                        _state.Next = State.Mul1;
                        break;
                }
            }
        }

        protected override void PreInitialize()
        {
            _tmp1.InitialValue = ((Signal<SFix>)A).InitialValue * ((Signal<SFix>)B).InitialValue;
            _tmp2.InitialValue = ((Signal<SFix>)C).InitialValue * ((Signal<SFix>)D).InitialValue;
        }

        protected override void Initialize()
        {
            AddProcess(Compute, Clk);
        }

        public static void RunTest()
        {
            DesignContext.Reset();
            FixedPointSettings.GlobalArithSizingMode = EArithSizingMode.VHDLCompliant;

            var a = SFix.FromDouble(1.0, 8, 10);
            var b = SFix.FromDouble(2.0, 8, 10);
            var c = SFix.FromDouble(3.0, 8, 10);
            var d = SFix.FromDouble(4.0, 8, 10);

            TestAddMul2 dut = new TestAddMul2()
            {
                Clk = new SLSignal(),
                A = new Signal<SFix>() { InitialValue = a },
                B = new Signal<SFix>() { InitialValue = b },
                C = new Signal<SFix>() { InitialValue = c },
                D = new Signal<SFix>() { InitialValue = d },
                R = new Signal<SFix>() { InitialValue = a * b + c * d }
            };

            DesignContext.Instance.Elaborate();
            XilinxIntegration.RegisterIPCores(DesignContext.Instance.Descriptor);
            DesignContext.Instance.CompleteAnalysis();

            XC6VLX240T_FF1156 fpga = new XC6VLX240T_FF1156()
            {
                SpeedGrade = ESpeedGrade._2,
                TopLevelComponent = dut
            };
            fpga.Synthesize(@".\hdl_out_TestAddMul2", "TestAddMul2");
        }
    }
}

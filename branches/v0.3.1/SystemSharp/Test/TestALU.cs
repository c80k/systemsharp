using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace Test
{
    public class ALUTestDesign: Component
    {
        private SLSignal _clk;
        private SLVSignal _a, _b, _ba;
        private SLVSignal _rAdd, _rSub, _rMul, _rNeg, _rNot, _rAnd, _rOr;

        private Clock _clkGen;
        private ALU _add;
        private ALU _sub;
        private ALU _mul;
        private ALU _neg;
        private ALU _not;
        private ALU _and;
        private ALU _or;

        public ALUTestDesign(int awidth, int bwidth, int pipelineDepth)
        {
            _clk = new SLSignal();
            _a = new SLVSignal(awidth) { InitialValue = StdLogicVector._0s(awidth) };
            _b = new SLVSignal(bwidth) { InitialValue = StdLogicVector._0s(bwidth) };
            _ba = new SLVSignal(awidth) { InitialValue = StdLogicVector._0s(awidth) };

            _clkGen = new Clock(new Time(10.0, ETimeUnit.ns));
            Bind(() => _clkGen.Clk = _clk);

            _add = new ALU(ALU.EFunction.Add, ALU.EArithMode.Signed, pipelineDepth,
                awidth, bwidth, Math.Max(awidth, bwidth) + 1);
            Bind(() =>
            {
                _add.Clk = _clk;
                _add.A = _a;
                _add.B = _b;
                _add.R = _rAdd;
            });
            _rAdd = new SLVSignal(_add.RWidth);

            _sub = new ALU(ALU.EFunction.Sub, ALU.EArithMode.Signed, pipelineDepth,
                awidth, bwidth, Math.Max(awidth, bwidth) + 1);
            Bind(() =>
            {
                _sub.Clk = _clk;
                _sub.A = _a;
                _sub.B = _b;
                _sub.R = _rSub;
            });
            _rSub = new SLVSignal(_sub.RWidth);

            _mul = new ALU(ALU.EFunction.Mul, ALU.EArithMode.Signed, pipelineDepth,
                awidth, bwidth, awidth + bwidth);
            Bind(() =>
            {
                _mul.Clk = _clk;
                _mul.A = _a;
                _mul.B = _b;
                _mul.R = _rMul;
            });
            _rMul = new SLVSignal(_mul.RWidth);

            _neg = new ALU(ALU.EFunction.Neg, ALU.EArithMode.Signed, pipelineDepth,
                awidth, 0, awidth + 1);
            Bind(() =>
            {
                _neg.Clk = _clk;
                _neg.A = _a;
                _neg.R = _rNeg;
            });
            _rNeg = new SLVSignal(_neg.RWidth);

            _not = new ALU(ALU.EFunction.Not, ALU.EArithMode.Signed, pipelineDepth,
                awidth, 0, awidth);
            Bind(() =>
            {
                _not.Clk = _clk;
                _not.A = _a;
                _not.R = _rNot;
            });
            _rNot = new SLVSignal(_not.RWidth);

            _and = new ALU(ALU.EFunction.And, ALU.EArithMode.Signed, pipelineDepth,
                awidth, awidth, awidth);
            Bind(() =>
            {
                _and.Clk = _clk;
                _and.A = _a;
                _and.B = _ba;
                _and.R = _rAnd;
            });
            _rAnd = new SLVSignal(_and.RWidth);

            _or = new ALU(ALU.EFunction.Or, ALU.EArithMode.Signed, pipelineDepth,
                awidth, awidth, awidth);
            Bind(() =>
            {
                _or.Clk = _clk;
                _or.A = _a;
                _or.B = _ba;
                _or.R = _rOr;
            });
            _rOr = new SLVSignal(_or.RWidth);
        }

        private void DriveBAProcess()
        {
            _ba.Next = _b.Cur[_a.Size - 1, 0];
        }

        private async void StimProcess()
        {
            await Tick;

            int i = -100;
            int j = 100;
            while (true)
            {
                Console.WriteLine("Next input: a = " + i + ", b = " + j);
                _a.Next = StdLogicVector.FromInt(i, _a.Size);
                _b.Next = StdLogicVector.FromInt(j, _b.Size);
                i += 2;
                --j;
                await Tick;
            }
        }

        private void LogAddProcess()
        {
            Console.WriteLine("Add result = " + _rAdd.Cur.IntValue.ToString());
        }

        private void LogSubProcess()
        {
            Console.WriteLine("Sub result = " + _rSub.Cur.IntValue.ToString());
        }

        private void LogMulProcess()
        {
            Console.WriteLine("Mul result = " + _rMul.Cur.IntValue.ToString());
        }

        private void LogNegProcess()
        {
            Console.WriteLine("Neg result = " + _rNeg.Cur.IntValue.ToString());
        }

        private void LogNotProcess()
        {
            Console.WriteLine("Not result = " + _rNot.Cur.ToString());
        }

        private void LogAndProcess()
        {
            Console.WriteLine("And result = " + _rAnd.Cur.ToString());
        }

        private void LogOrProcess()
        {
            Console.WriteLine("Or result = " + _rOr.Cur.ToString());
        }

        protected override void Initialize()
        {
            AddProcess(DriveBAProcess, _b);
            AddProcess(LogAddProcess, _rAdd);
            AddProcess(LogSubProcess, _rSub);
            AddProcess(LogMulProcess, _rMul);
            AddProcess(LogNegProcess, _rNeg);
            AddProcess(LogNotProcess, _rNot);
            AddProcess(LogAndProcess, _rAnd);
            AddProcess(LogOrProcess, _rOr);
            AddClockedThread(StimProcess, _clk.RisingEdge, _clk);
        }

        public static void Run()
        {
            DesignContext.Reset();
            ALUTestDesign td = new ALUTestDesign(8, 8, 2);
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(new Time(0.5, ETimeUnit.us));

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_out_ALUTestDesign", "ALUTestDesign");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            project.SetVHDLProfile();

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(codeGen);
            project.Save();
        }
    }
}

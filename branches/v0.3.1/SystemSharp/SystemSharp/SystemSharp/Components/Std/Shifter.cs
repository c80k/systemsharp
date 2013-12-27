/**
 * Copyright 2011-2013 Christian Köllner
 * 
 * This file is part of System#.
 *
 * System# is free software: you can redistribute it and/or modify it under 
 * the terms of the GNU Lesser General Public License (LGPL) as published 
 * by the Free Software Foundation, either version 3 of the License, or (at 
 * your option) any later version.
 *
 * System# is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more 
 * details.
 *
 * You should have received a copy of the GNU General Public License along 
 * with System#. If not, see http://www.gnu.org/licenses/lgpl.html.
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Common;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// Transaction site interface for logical shift operations. Arithmetic shift is currently not supported.
    /// </summary>
    public interface IShifterTransactor : 
        ITransactionSite
    {
        /// <summary>
        /// Returns a transaction for shifting left.
        /// </summary>
        /// <param name="x">source of operand to shift</param>
        /// <param name="s">source of bit-count to shift</param>
        /// <param name="y">sink for shifted result</param>
        IEnumerable<TAVerb> LShift(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> s,
            ISignalSink<StdLogicVector> y);

        /// <summary>
        /// Returns a transaction for shifting right.
        /// </summary>
        /// <param name="x">source of operand to shift</param>
        /// <param name="s"></param>
        /// <param name="y">sink for shifted result</param>
        IEnumerable<TAVerb> RShift(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> s,
            ISignalSink<StdLogicVector> y);
    }

    /// <summary>
    /// A logical shift unit (arithmetic shifts not currently not supported).
    /// </summary>
    [DeclareXILMapper(typeof(ShifterXILMapper))]
    public class Shifter: Component
    {
        private class ShifterTransactor : 
            DefaultTransactionSite,
            IShifterTransactor
        {
            private Shifter _host;

            public ShifterTransactor(Shifter host):
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(
                    ETVMode.Locked,
                    _host.X.Dual.Stick(StdLogicVector.DCs(_host.DataWidth)),
                    _host.Shift.Dual.Stick(StdLogicVector.DCs(_host.ShiftWidth)),
                    _host.Dir.Dual.Stick("-"));
            }

            public IEnumerable<TAVerb> LShift(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> s, ISignalSink<StdLogicVector> y)
            {
                if (_host.PipelineDepth == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        _host.X.Dual.Drive(x),
                        _host.Shift.Dual.Drive(s),
                        _host.Dir.Dual.Stick("0"),
                        y.Comb.Connect(_host.Y.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked,
                        _host.X.Dual.Drive(x),
                        _host.Shift.Dual.Drive(s),
                        _host.Dir.Dual.Stick("0"));
                    for (int i = 0; i < _host.PipelineDepth; i++)
                    {
                        yield return Verb(ETVMode.Shared);
                    }
                    yield return Verb(ETVMode.Shared,
                        y.Comb.Connect(_host.Y.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> RShift(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> s, ISignalSink<StdLogicVector> y)
            {
                if (_host.PipelineDepth == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        _host.X.Dual.Drive(x),
                        _host.Shift.Dual.Drive(s),
                        _host.Dir.Dual.Stick("1"),
                        y.Comb.Connect(_host.Y.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked,
                        _host.X.Dual.Drive(x),
                        _host.Shift.Dual.Drive(s),
                        _host.Dir.Dual.Stick("1"));
                    for (int i = 0; i < _host.PipelineDepth; i++)
                    {
                        yield return Verb(ETVMode.Shared);
                    }
                    yield return Verb(ETVMode.Shared,
                        y.Comb.Connect(_host.Y.Dual.AsSignalSource()));
                }
            }

            public override void Establish(IAutoBinder binder)
            {
                var shifter = _host;
                shifter.Clk = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                shifter.X = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "X", null, StdLogicVector._0s(shifter.DataWidth));
                shifter.Shift = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "S", null, StdLogicVector._0s(shifter.ShiftWidth));
                shifter.Dir = binder.GetSignal<StdLogicVector>(EPortUsage.Default, "Dir", null, StdLogicVector._0s(1));
                shifter.Y = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "Y", null, StdLogicVector._0s(shifter.DataWidth));
            }
        }

        /// <summary>
        /// Clock signal input
        /// </summary>
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// Operand input
        /// </summary>
        public In<StdLogicVector> X { private get; set; }

        /// <summary>
        /// Number of bits to shift
        /// </summary>
        public In<StdLogicVector> Shift { private get; set; }

        /// <summary>
        /// Shift direction: '0' is left, '1' is right
        /// </summary>
        public In<StdLogicVector> Dir { private get; set; }

        /// <summary>
        /// Shifted result
        /// </summary>
        public Out<StdLogicVector> Y { private get; set; }

        /// <summary>
        /// Operand and result bit-width
        /// </summary>
        [PerformanceRelevant]
        public int DataWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Bit-width of shift signal, i.e. maximum shift is (2^<c>ShiftWidth</c>-1) to left or right
        /// </summary>
        [PerformanceRelevant]
        public int ShiftWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Number of pipeline stages, i.e. computation latency
        /// </summary>
        [PerformanceRelevant]
        public int PipelineDepth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public IShifterTransactor TASite { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="dataWidth">bit-width of operand</param>
        /// <param name="pipelineDepth">number of pipeline stages, i.e. computation latency</param>
        public Shifter(int dataWidth, int pipelineDepth)
        {
            DataWidth = dataWidth;
            ShiftWidth = MathExt.CeilLog2(dataWidth);
            PipelineDepth = pipelineDepth;

            _y = new SLVSignal(DataWidth);
            _pad = StdLogicVector._0s(DataWidth);
            _pipe = new RegPipe(PipelineDepth, DataWidth);
            Bind(() => {
                _pipe.Clk = Clk;
                _pipe.Din = _y;
                _pipe.Dout = Y;
            });
            TASite = new ShifterTransactor(this);
        }

        private SLVSignal _y;
        private StdLogicVector _pad;
        private RegPipe _pipe;

        protected override void Initialize()
        {
            AddProcess(Processing, Clk);
        }

        private void Processing()
        {
            if (Clk.RisingEdge())
            {
                int shift = Shift.Cur.UnsignedValue.IntValue;
                if (Dir.Cur == "0") // shift left
                    _y.Next = X.Cur.Concat(_pad)[2*DataWidth - shift - 1, DataWidth - shift];
                else // shift right
                    _y.Next = _pad.Concat(X.Cur)[DataWidth + shift - 1, shift];
            }
        }
    }

    /// <summary>
    /// A service for mapping XIL instructions performing logical shifts to hardware. Arithmetic shifts are currently not supported.
    /// </summary>
    public class ShifterXILMapper : IXILMapper
    {
        private class LShiftXILMapping : DefaultXILMapping
        {
            private Shifter _host;

            /// <summary>
            /// Constructs a new instance.
            /// </summary>
            /// <param name="host">shifter unit</param>
            public LShiftXILMapping(Shifter host) :
                base(host.TASite, EMappingKind.ReplicatableResource)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _host.TASite.LShift(operands[0], operands[1], results[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                return _host.TASite.LShift(
                    SignalSource.Create(StdLogicVector._0s(_host.DataWidth)),
                    SignalSource.Create(StdLogicVector._0s(_host.ShiftWidth)), 
                    SignalSink.Nil<StdLogicVector>());
            }

            public override string Description
            {
                get { return _host.DataWidth + " bit, " + _host.PipelineDepth + " stage left shifter"; }
            }
        }

        private class RShiftXILMapping : DefaultXILMapping
        {
            private Shifter _host;

            public RShiftXILMapping(Shifter host) :
                base(host.TASite, EMappingKind.ReplicatableResource)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _host.TASite.RShift(operands[0], operands[1], results[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                return _host.TASite.RShift(
                    SignalSource.Create(StdLogicVector._0s(_host.DataWidth)),
                    SignalSource.Create(StdLogicVector._0s(_host.ShiftWidth)),
                    SignalSink.Nil<StdLogicVector>());
            }

            public override string Description
            {
                get { return _host.DataWidth + " bit, " + _host.PipelineDepth + " stage left shifter"; }
            }
        }

        /// <summary>
        /// Returns lshift, rshift
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.LShift();
            yield return DefaultInstructionSet.Instance.RShift();
        }

        private int GetFixSize(TypeDescriptor type)
        {
            if (!type.IsComplete)
                return -1;

            if (type.CILType.Equals(typeof(Signed)))
            {
                int size = (int)type.TypeParams[0];
                return size;
            }
            else if (type.CILType.Equals(typeof(Unsigned)))
            {
                int size = (int)type.TypeParams[0];
                return size;
            }
            else if (type.CILType.Equals(typeof(SFix)) ||
                type.CILType.Equals(typeof(UFix)))
            {
                return ((FixFormat)type.TypeParams[0]).TotalWidth;
            }
            else if (type.CILType.Equals(typeof(StdLogicVector)))
            {
                int size = (int)type.TypeParams[0];
                return size;
            }
            else
            {
                return -1;
            }
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            Shifter shifter = fu as Shifter;
            if (shifter == null)
                yield break;

            if (instr.Name != InstructionCodes.LShift &&
                instr.Name != InstructionCodes.RShift)
                yield break;

            int fmtData = GetFixSize(operandTypes[0]);
            int fmtShift = GetFixSize(operandTypes[1]);
            int fmtResult = GetFixSize(resultTypes[0]);
            if (fmtData < 0 || fmtShift < 0 || fmtResult < 0)
                yield break;

            if (fmtData != fmtResult)
                yield break;

            if (shifter.DataWidth != fmtData ||
                shifter.ShiftWidth != fmtShift)
                yield break;

            switch (instr.Name)
            {
                case InstructionCodes.LShift:
                    yield return new LShiftXILMapping(shifter);
                    break;

                case InstructionCodes.RShift:
                    yield return new RShiftXILMapping(shifter);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (instr.Name != InstructionCodes.LShift &&
                instr.Name != InstructionCodes.RShift)
                return null;

            int fmtData = GetFixSize(operandTypes[0]);
            int fmtShift = GetFixSize(operandTypes[1]);
            int fmtResult = GetFixSize(resultTypes[0]);
            if (fmtData < 0 || fmtShift < 0 || fmtResult < 0)
                return null;

            if (!fmtData.Equals(fmtResult))
                return null;

            if (fmtShift != MathExt.CeilLog2(fmtData))
                return null;

            Shifter shifter = new Shifter(fmtData, 1);

            switch (instr.Name)
            {
                case InstructionCodes.LShift:
                    return new LShiftXILMapping(shifter);

                case InstructionCodes.RShift:
                    return new RShiftXILMapping(shifter);

                default:
                    throw new NotImplementedException();
            }
        }
    }
}

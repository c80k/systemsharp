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
using System.Diagnostics.Contracts;
using System.Linq;
using SystemSharp.Analysis;
using SystemSharp.Assembler;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// Provides a synthesizable implementation of an arithmetic-logical unit (ALU). It is intended to be used during high-level synthesis
    /// for mapping basic arithmetic/logical instructions.
    /// </summary>
    [DeclareXILMapper(typeof(ALUXILMapper))]
    public class ALU: FunctionalUnit
    {
        /// <summary>
        /// Transaction site interface of ALU
        /// </summary>
        public interface IALUTransactor : ITransactionSite
        {
            /// <summary>
            /// Returns a transaction for the preselected binary operation on the ALU.
            /// </summary>
            /// <param name="a">source of first operand</param>
            /// <param name="b">source of second operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> Do(ISignalSource<StdLogicVector> a,
                ISignalSource<StdLogicVector> b,
                ISignalSink<StdLogicVector> r);

            /// <summary>
            /// Returns a transaction for a "less than" comparison on the ALU.
            /// </summary>
            /// <param name="a">source of first operand</param>
            /// <param name="b">source of second operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> IsLt(ISignalSource<StdLogicVector> a,
                ISignalSource<StdLogicVector> b,
                ISignalSink<StdLogicVector> r);

            /// <summary>
            /// Returns a transaction for a "less than or equal" comparison on the ALU.
            /// </summary>
            /// <param name="a">source of first operand</param>
            /// <param name="b">source of second operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> IsLte(ISignalSource<StdLogicVector> a,
                ISignalSource<StdLogicVector> b,
                ISignalSink<StdLogicVector> r);

            /// <summary>
            /// Returns a transaction for an "equality" comparison on the ALU.
            /// </summary>
            /// <param name="a">source of first operand</param>
            /// <param name="b">source of second operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> IsEq(ISignalSource<StdLogicVector> a,
                ISignalSource<StdLogicVector> b,
                ISignalSink<StdLogicVector> r);

            /// <summary>
            /// Returns a transaction for an "inequality" comparison on the ALU.
            /// </summary>
            /// <param name="a">source of first operand</param>
            /// <param name="b">source of second operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> IsNEq(ISignalSource<StdLogicVector> a,
                ISignalSource<StdLogicVector> b,
                ISignalSink<StdLogicVector> r);

            /// <summary>
            /// Returns a transaction for a "greater than or equal" comparison on the ALU.
            /// </summary>
            /// <param name="a">source of first operand</param>
            /// <param name="b">source of second operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> IsGte(ISignalSource<StdLogicVector> a,
                ISignalSource<StdLogicVector> b,
                ISignalSink<StdLogicVector> r);

            /// <summary>
            /// Returns a transaction for a "greater than" comparison on the ALU.
            /// </summary>
            /// <param name="a">source of first operand</param>
            /// <param name="b">source of second operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> IsGt(ISignalSource<StdLogicVector> a,
                ISignalSource<StdLogicVector> b,
                ISignalSink<StdLogicVector> r);

            /// <summary>
            /// Returns a transaction for the preselected unary operation on the ALU.
            /// </summary>
            /// <param name="a">source operand</param>
            /// <param name="r">sink for result</param>
            IEnumerable<TAVerb> Do(ISignalSource<StdLogicVector> a,
                ISignalSink<StdLogicVector> r);
        }

        private class ALUTransactor : DefaultTransactionSite,
            IALUTransactor
        {
            protected ALU _host;
            private bool _established;

            public ALUTransactor(ALU host):
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                if (_host.Arity > 1)
                {
                    yield return Verb(ETVMode.Locked, 
                        _host.A.Dual.Stick(StdLogicVector.DCs(_host.AWidth)),
                        _host.B.Dual.Stick(StdLogicVector.DCs(_host.BWidth)));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, 
                        _host.A.Dual.Stick(StdLogicVector.DCs(_host.AWidth)));
                }
            }

            private IEnumerable<TAVerb> Do(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, 
                In<StdLogicVector> rs, ISignalSink<StdLogicVector> r)
            {
                if (_host.PipelineDepth == 0)
                {
                    yield return Verb(ETVMode.Locked, 
                        _host.A.Dual.Drive(a)
                            .Par(_host.B.Dual.Drive(b))
                            .Par(r.Comb.Connect(rs.AsSignalSource())));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, 
                        _host.A.Dual.Drive(a)
                            .Par(_host.B.Dual.Drive(b)));
                    for (int i = 1; i < _host.PipelineDepth; i++)
                    {
                        yield return Verb(ETVMode.Shared);
                    }
                    yield return Verb(ETVMode.Shared,
                        r.Comb.Connect(rs.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Do(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                if (_host.FuncSel == EFunction.Compare)
                    throw new InvalidOperationException();

                return Do(a, b, _host.R.Dual, r);
            }

            public IEnumerable<TAVerb> IsLt(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                if (_host.FuncSel != EFunction.Compare)
                    throw new InvalidOperationException();

                return Do(a, b, _host.CmpLt.Dual, r);
            }

            public IEnumerable<TAVerb> IsLte(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                if (_host.FuncSel != EFunction.Compare)
                    throw new InvalidOperationException();

                return Do(a, b, _host.CmpLte.Dual, r);
            }

            public IEnumerable<TAVerb> IsEq(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                if (_host.FuncSel != EFunction.Compare)
                    throw new InvalidOperationException();

                return Do(a, b, _host.CmpEq.Dual, r);
            }

            public IEnumerable<TAVerb> IsNEq(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                if (_host.FuncSel != EFunction.Compare)
                    throw new InvalidOperationException();

                return Do(a, b, _host.CmpNeq.Dual, r);
            }

            public IEnumerable<TAVerb> IsGte(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                if (_host.FuncSel != EFunction.Compare)
                    throw new InvalidOperationException();

                return Do(a, b, _host.CmpGte.Dual, r);
            }

            public IEnumerable<TAVerb> IsGt(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                if (_host.FuncSel != EFunction.Compare)
                    throw new InvalidOperationException();

                return Do(a, b, _host.CmpGt.Dual, r);
            }

            public IEnumerable<TAVerb> Do(ISignalSource<StdLogicVector> a, ISignalSink<StdLogicVector> r)
            {
                if (_host.PipelineDepth == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        _host.A.Dual.Drive(a)
                            .Par(r.Comb.Connect(_host.R.Dual.AsSignalSource())));
                }
                else
                {
                    yield return Verb(ETVMode.Locked,
                        _host.A.Dual.Drive(a));
                    for (int i = 1; i < _host.PipelineDepth; i++)
                    {
                        yield return Verb(ETVMode.Shared);
                    }
                    yield return Verb(ETVMode.Shared,
                        r.Comb.Connect(_host.R.Dual.AsSignalSource()));
                }
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_established)
                    return;
                
                var alu = _host;
                alu.Clk = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                alu.A = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "A", null, StdLogicVector._0s(alu.AWidth));
                if (alu.Arity > 1)
                    alu.B = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "B", null, StdLogicVector._0s(alu.BWidth));
                if (alu.FuncSel == ALU.EFunction.Compare)
                {
                    alu.CmpLt = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "IsLt", null, StdLogicVector._0s(1));
                    alu.CmpLte = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "IsLte", null, StdLogicVector._0s(1));
                    alu.CmpEq = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "IsEq", null, StdLogicVector._0s(1));
                    alu.CmpNeq = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "IsNEq", null, StdLogicVector._0s(1));
                    alu.CmpGte = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "IsGte", null, StdLogicVector._0s(1));
                    alu.CmpGt = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "IsGt", null, StdLogicVector._0s(1));
                }
                else
                {
                    alu.R = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "R", null, StdLogicVector._0s(alu.RWidth));
                }

                _established = true;
            }
        }

        /// <summary>
        /// Preselected function
        /// </summary>
        public enum EFunction
        {
            /// <summary>
            /// Addition
            /// </summary>
            Add,

            /// <summary>
            /// Subtraction
            /// </summary>
            Sub,

            /// <summary>
            /// Multiplication
            /// </summary>
            Mul,

            /// <summary>
            /// Negation
            /// </summary>
            Neg,

            /// <summary>
            /// Bit complement
            /// </summary>
            Not,

            /// <summary>
            /// Bitwise conjunction
            /// </summary>
            And,

            /// <summary>
            /// Bitwise disjunction
            /// </summary>
            Or,

            /// <summary>
            /// Comparison
            /// </summary>
            Compare
        }

        /// <summary>
        /// Signedness of operand
        /// </summary>
        public enum EArithMode
        {
            Signed,
            Unsigned
        }

        /// <summary>
        /// Clock signal input
        /// </summary>
        [PortUsage(EPortUsage.Clock)]
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// Input of first (or sole) operand
        /// </summary>
        public In<StdLogicVector> A { private get; set; }

        /// <summary>
        /// Input of second operand
        /// </summary>
        public In<StdLogicVector> B { private get; set; }

        /// <summary>
        /// Result output
        /// </summary>
        public Out<StdLogicVector> R { private get; set; }

        /// <summary>
        /// Comparison outcome - less than (single bit)
        /// </summary>
        public Out<StdLogicVector> CmpLt { private get; set; }

        /// <summary>
        /// Comparison outcome - greater than (single bit)
        /// </summary>
        public Out<StdLogicVector> CmpGt { private get; set; }

        /// <summary>
        /// Comparison outcome - equal (single bit)
        /// </summary>
        public Out<StdLogicVector> CmpEq { private get; set; }

        /// <summary>
        /// Comparison outcome - not equal (single bit)
        /// </summary>
        public Out<StdLogicVector> CmpNeq { private get; set; }

        /// <summary>
        /// Comparison outcome - less than or equal (single bit)
        /// </summary>
        public Out<StdLogicVector> CmpLte { private get; set; }

        /// <summary>
        /// Comparison outcome - greater than or equal (single bit)
        /// </summary>
        public Out<StdLogicVector> CmpGte { private get; set; }

        /// <summary>
        /// Which operation this ALU is supposed to carry out.
        /// </summary>
        [PerformanceRelevant]
        public readonly EFunction FuncSel;

        /// <summary>
        /// Signedness of operands/result
        /// </summary>
        [PerformanceRelevant]
        public readonly EArithMode ArithMode;

        /// <summary>
        /// Desired latency
        /// </summary>
        [PerformanceRelevant]
        public readonly int PipelineDepth;

        /// <summary>
        /// Bit-width of first (or sole) operand
        /// </summary>
        [PerformanceRelevant]
        public readonly int AWidth;

        /// <summary>
        /// Bit-width of second operand
        /// </summary>
        [PerformanceRelevant]
        public readonly int BWidth;

        /// <summary>
        /// Result width
        /// </summary>
        [PerformanceRelevant]
        public readonly int RWidth;

        /// <summary>
        /// Returns true if <paramref name="obj"/> is an ALU with same parameters
        /// </summary>
        public override bool IsEquivalent(Component obj)
        {
            var other = obj as ALU;
            if (other == null)
                return false;
            return FuncSel == other.FuncSel &&
                ArithMode == other.ArithMode &&
                PipelineDepth == other.PipelineDepth &&
                AWidth == other.AWidth &&
                BWidth == other.BWidth &&
                RWidth == other.RWidth;
        }

        public override int GetBehaviorHashCode()
        {
            return FuncSel.GetHashCode() ^
                ArithMode.GetHashCode() ^
                PipelineDepth ^
                AWidth ^
                BWidth ^
                RWidth;
        }

        private ALUTransactor _transactor;
        /// <summary>
        /// The associated transaction site
        /// </summary>
        public IALUTransactor Transactor
        {
            get { return _transactor; }
        }

        private RegPipe _pipe;
        private SLVSignal _opResult;
        private SLVSignal _pipeOut;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="funcSel">which operation the ALU is supposed to carry out</param>
        /// <param name="arithMode">signedness of operands/result</param>
        /// <param name="pipelineDepth">desired latency</param>
        /// <param name="awidth">width of first (or sole) operand</param>
        /// <param name="bwidth">width of second operand</param>
        /// <param name="rwidth">width of result</param>
        /// <exception cref="ArgumentOutOfRangeException">if parameter is invalid or inconsistency between parameters was detected</exception>
        public ALU(EFunction funcSel, EArithMode arithMode,
            int pipelineDepth, int awidth, int bwidth, int rwidth)
        {
            Contract.Requires<ArgumentOutOfRangeException>(pipelineDepth >= 0, "Pipeline depth must be non-negative.");
            Contract.Requires<ArgumentOutOfRangeException>(awidth >= 1, "Operand width A must be positive.");
            Contract.Requires<ArgumentOutOfRangeException>(funcSel.IsUnary() || bwidth >= 1, "Operand width B must be positive.");
            Contract.Requires<ArgumentOutOfRangeException>(funcSel == EFunction.Compare || rwidth >= 1, "Result width must be positive");
            Contract.Requires<ArgumentOutOfRangeException>(funcSel == EFunction.Add || funcSel == EFunction.Mul || funcSel == EFunction.Neg ||
                funcSel == EFunction.Sub || awidth == bwidth, "Operand sizes must be equal for this kind of operation.");
            Contract.Requires<ArgumentOutOfRangeException>(funcSel == EFunction.Add || funcSel == EFunction.Compare ||
                funcSel == EFunction.Mul || funcSel == EFunction.Neg || funcSel == EFunction.Sub || rwidth == awidth,
                "Result and operand sizes must be equal for this kind of instruction.");

            FuncSel = funcSel;
            ArithMode = arithMode;
            PipelineDepth = pipelineDepth;
            if (funcSel == EFunction.Compare)
                rwidth = 6;
            AWidth = awidth;
            BWidth = bwidth;
            RWidth = rwidth;
            _opResult = new SLVSignal(rwidth)
            {
                InitialValue = StdLogicVector._0s(rwidth)
            };
            if (pipelineDepth >= 3)
            {
                // If pipeline depth > 0, the operand inputs will be registered.
                // Thus, the post pipeline must be reduced by one stage.
                _pipe = new RegPipe(Math.Max(0, pipelineDepth - 1), rwidth);
                if (FuncSel == EFunction.Compare)
                {
                    _pipeOut = new SLVSignal(rwidth)
                    {
                        InitialValue = StdLogicVector._0s(rwidth)
                    };

                    Bind(() =>
                    {
                        _pipe.Clk = Clk;
                        _pipe.Din = _opResult;
                        _pipe.Dout = _pipeOut;
                    });
                }
                else
                {
                    Bind(() =>
                    {
                        _pipe.Clk = Clk;
                        _pipe.Din = _opResult;
                        _pipe.Dout = R;
                    });
                }
            }
            _transactor = new ALUTransactor(this);
        }

        /// <summary>
        /// Returns 2 for binary operations, 1 for unary operations
        /// </summary>
        public int Arity
        {
            get { return FuncSel.IsBinary() ? 2 : 1; }
        }

        public override string DisplayName
        {
            get
            {
                switch (FuncSel)
                {
                    case EFunction.Add: return "Add";
                    case EFunction.And: return "And";
                    case EFunction.Compare: return "Compare";
                    case EFunction.Mul: return "Mul";
                    case EFunction.Neg: return "Neg";
                    case EFunction.Not: return "Not";
                    case EFunction.Or: return "Or";
                    case EFunction.Sub: return "Sub";
                    default: throw new NotImplementedException();
                }
            }
        }

        private void AddSignedProcess()
        {
            R.Next = (A.Cur.SignedValue.Resize(RWidth) + B.Cur.SignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
        }

        private void AddUnsignedProcess()
        {
            R.Next = (A.Cur.UnsignedValue.Resize(RWidth) + B.Cur.UnsignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
        }

        private void SubSignedProcess()
        {
            R.Next = (A.Cur.SignedValue.Resize(RWidth) - B.Cur.SignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
        }

        private void SubUnsignedProcess()
        {
            R.Next = (A.Cur.UnsignedValue.Resize(RWidth) - B.Cur.UnsignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
        }

        private void MulSignedProcess()
        {
            R.Next = (A.Cur.SignedValue * B.Cur.SignedValue).Resize(RWidth).SLVValue;
        }

        private void MulUnsignedProcess()
        {
            R.Next = (A.Cur.UnsignedValue * B.Cur.UnsignedValue).Resize(RWidth).SLVValue;
        }

        private void NegSignedProcess()
        {
            R.Next = (-A.Cur.SignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
        }

        private void NotProcess()
        {
            R.Next = ~A.Cur;
        }

        private void AndProcess()
        {
            R.Next = A.Cur & B.Cur;
        }

        private void OrProcess()
        {
            R.Next = A.Cur | B.Cur;
        }

        private void CmpSignedProcess()
        {
            bool isLt = A.Cur.SignedValue < B.Cur.SignedValue;
            bool isGt = A.Cur.SignedValue > B.Cur.SignedValue;
            bool isNeq = isLt || isGt;
            bool isEq = !isNeq;
            StdLogic ltBit = isLt ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic gtBit = isGt ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic neqBit = isNeq ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic eqBit = isEq ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic lteBit = isLt || isEq ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic gteBit = isGt || isEq ? (StdLogic)'1' : (StdLogic)'0';
            _opResult.Next = ltBit.Concat(gtBit).Concat(neqBit).Concat(eqBit).Concat(lteBit).Concat(gteBit);
        }

        private void CmpUnsignedProcess()
        {
            bool isLt = A.Cur.UnsignedValue < B.Cur.UnsignedValue;
            bool isGt = A.Cur.UnsignedValue > B.Cur.UnsignedValue;
            bool isNeq = isLt || isGt;
            bool isEq = !isNeq;
            StdLogic ltBit = isLt ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic gtBit = isGt ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic neqBit = isNeq ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic eqBit = isEq ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic lteBit = isLt || isEq ? (StdLogic)'1' : (StdLogic)'0';
            StdLogic gteBit = isGt || isEq ? (StdLogic)'1' : (StdLogic)'0';
            _opResult.Next = ltBit.Concat(gtBit).Concat(neqBit).Concat(eqBit).Concat(lteBit).Concat(gteBit);
        }

        private void AddSignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = (A.Cur.SignedValue.Resize(RWidth) + B.Cur.SignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
            }
        }

        private void AddUnsignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = (A.Cur.UnsignedValue.Resize(RWidth) + B.Cur.UnsignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
            }
        }

        private void SubSignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = (A.Cur.SignedValue.Resize(RWidth) - B.Cur.SignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
            }
        }

        private void SubUnsignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = (A.Cur.UnsignedValue.Resize(RWidth) - B.Cur.UnsignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
            }
        }

        private void MulSignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = (A.Cur.SignedValue * B.Cur.SignedValue).Resize(RWidth).SLVValue;
            }
        }

        private void MulUnsignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = (A.Cur.UnsignedValue * B.Cur.UnsignedValue).Resize(RWidth).SLVValue;
            }
        }

        private void NegSignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = (-A.Cur.SignedValue.Resize(RWidth)).Resize(RWidth).SLVValue;
            }
        }

        private void NotProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = ~A.Cur;
            }
        }

        private void AndProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = A.Cur & B.Cur;
            }
        }

        private void OrProcessSync()
        {
            if (Clk.RisingEdge())
            {
                _opResult.Next = A.Cur | B.Cur;
            }
        }

        private void CmpSignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                bool isLt = A.Cur.SignedValue < B.Cur.SignedValue;
                bool isGt = A.Cur.SignedValue > B.Cur.SignedValue;
                bool isNeq = isLt || isGt;
                bool isEq = !isNeq;
                StdLogic ltBit = isLt ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic gtBit = isGt ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic neqBit = isNeq ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic eqBit = isEq ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic lteBit = isLt || isEq ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic gteBit = isGt || isEq ? (StdLogic)'1' : (StdLogic)'0';
                _opResult.Next = ltBit.Concat(gtBit).Concat(neqBit).Concat(eqBit).Concat(lteBit).Concat(gteBit);
            }
        }

        private void CmpUnsignedProcessSync()
        {
            if (Clk.RisingEdge())
            {
                bool isLt = A.Cur.UnsignedValue < B.Cur.UnsignedValue;
                bool isGt = A.Cur.UnsignedValue > B.Cur.UnsignedValue;
                bool isNeq = isLt || isGt;
                bool isEq = !isNeq;
                StdLogic ltBit = isLt ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic gtBit = isGt ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic neqBit = isNeq ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic eqBit = isEq ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic lteBit = isLt || isEq ? (StdLogic)'1' : (StdLogic)'0';
                StdLogic gteBit = isGt || isEq ? (StdLogic)'1' : (StdLogic)'0';
                _opResult.Next = ltBit.Concat(gtBit).Concat(neqBit).Concat(eqBit).Concat(lteBit).Concat(gteBit);
            }
        }

        private void DriveCmpProcessPiped()
        {
            CmpLt.Next = _pipeOut.Cur[5, 5];
            CmpGt.Next = _pipeOut.Cur[4, 4];
            CmpNeq.Next = _pipeOut.Cur[3, 3];
            CmpEq.Next = _pipeOut.Cur[2, 2];
            CmpLte.Next = _pipeOut.Cur[1, 1];
            CmpGte.Next = _pipeOut.Cur[0, 0];
        }

        private void DriveRProcessUnpiped()
        {
            R.Next = _opResult.Cur;
        }

        private void DriveCmpProcessUnpiped()
        {
            CmpLt.Next = _opResult.Cur[5, 5];
            CmpGt.Next = _opResult.Cur[4, 4];
            CmpNeq.Next = _opResult.Cur[3, 3];
            CmpEq.Next = _opResult.Cur[2, 2];
            CmpLte.Next = _opResult.Cur[1, 1];
            CmpGte.Next = _opResult.Cur[0, 0];
        }

        private void DriveRProcessSync()
        {
            if (Clk.RisingEdge())
                R.Next = _opResult.Cur;
        }

        private void DriveCmpProcessSync()
        {
            if (Clk.RisingEdge())
            {
                CmpLt.Next = _opResult.Cur[5, 5];
                CmpGt.Next = _opResult.Cur[4, 4];
                CmpNeq.Next = _opResult.Cur[3, 3];
                CmpEq.Next = _opResult.Cur[2, 2];
                CmpLte.Next = _opResult.Cur[1, 1];
                CmpGte.Next = _opResult.Cur[0, 0];
            }
        }


        protected override void Initialize()
        {
            if (PipelineDepth > 0)
            {
                switch (FuncSel)
                {
                    case EFunction.Add:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(AddSignedProcessSync, Clk);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(AddUnsignedProcessSync, Clk);
                                break;
                        }
                        break;

                    case EFunction.Sub:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(SubSignedProcessSync, Clk);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(SubUnsignedProcessSync, Clk);
                                break;
                        }
                        break;

                    case EFunction.Mul:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(MulSignedProcessSync, Clk);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(MulUnsignedProcessSync, Clk);
                                break;
                        }
                        break;

                    case EFunction.Neg:
                        AddProcess(NegSignedProcessSync, Clk);
                        break;

                    case EFunction.Not:
                        AddProcess(NotProcessSync, Clk);
                        break;

                    case EFunction.Or:
                        AddProcess(OrProcessSync, Clk);
                        break;

                    case EFunction.And:
                        AddProcess(AndProcessSync, Clk);
                        break;

                    case EFunction.Compare:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(CmpSignedProcessSync, Clk);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(CmpUnsignedProcessSync, Clk);
                                break;
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }

                if (PipelineDepth == 1)
                {
                    if (FuncSel == EFunction.Compare)
                        AddProcess(DriveCmpProcessUnpiped, _opResult);
                    else
                        AddProcess(DriveRProcessUnpiped, _opResult);
                }
                else if (PipelineDepth == 2)
                {
                    if (FuncSel == EFunction.Compare)
                        AddProcess(DriveCmpProcessSync, Clk);
                    else
                        AddProcess(DriveRProcessSync, Clk);
                }
                else if (PipelineDepth >= 2)
                {
                    if (FuncSel == EFunction.Compare)
                        AddProcess(DriveCmpProcessPiped, _pipeOut);
                }
            }
            else
            {
                switch (FuncSel)
                {
                    case EFunction.Add:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(AddSignedProcess, A, B);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(AddUnsignedProcess, A, B);
                                break;
                        }
                        break;

                    case EFunction.Sub:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(SubSignedProcess, A, B);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(SubUnsignedProcess, A, B);
                                break;
                        }
                        break;

                    case EFunction.Mul:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(MulSignedProcess, A, B);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(MulUnsignedProcess, A, B);
                                break;
                        }
                        break;

                    case EFunction.Neg:
                        AddProcess(NegSignedProcess, A);
                        break;

                    case EFunction.Not:
                        AddProcess(NotProcess, A);
                        break;

                    case EFunction.Or:
                        AddProcess(OrProcess, A, B);
                        break;

                    case EFunction.And:
                        AddProcess(AndProcess, A, B);
                        break;

                    case EFunction.Compare:
                        switch (ArithMode)
                        {
                            case EArithMode.Signed:
                                AddProcess(CmpSignedProcess, A, B);
                                break;
                            case EArithMode.Unsigned:
                                AddProcess(CmpUnsignedProcess, A, B);
                                break;
                        }
                        AddProcess(DriveCmpProcessUnpiped, _opResult);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }

    /// <summary>
    /// Represents a mapping of a XIL instruction to an ALU
    /// </summary>
    class ALUXILMapping : IXILMapping
    {
        private ITransactionSite _taSite;
        private Func<ISignalSource<StdLogicVector>[], ISignalSink<StdLogicVector>[], IEnumerable<TAVerb>> _realize;
        private bool _swap;

        public ALUXILMapping(ITransactionSite transactor,
            Func<ISignalSource<StdLogicVector>[], ISignalSink<StdLogicVector>[], IEnumerable<TAVerb>> realize, bool swap)
        {
            _taSite = transactor;
            _realize = realize;
            _swap = swap;
        }

        public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
        {
            if (_swap)
                return _realize(new ISignalSource<StdLogicVector>[] { operands[1], operands[0] }, results);
            else
                return _realize(operands, results);
        }

        public ITransactionSite TASite
        {
            get { return _taSite; }
        }

        public EMappingKind ResourceKind
        {
            get { return EMappingKind.ReplicatableResource; }
        }

        public int InitiationInterval
        {
            get { return 1; }
        }

        public int Latency
        {
            get { return ((ALU)_taSite.Host).PipelineDepth; }
        }

        public string Description
        {
            get
            {
                var alu = (ALU)_taSite.Host;
                string arith = "";
                switch (alu.ArithMode)
                {
                    case ALU.EArithMode.Signed:
                        arith = "signed";
                        break;

                    case ALU.EArithMode.Unsigned:
                        arith = "unsigned";
                        break;

                    default:
                        throw new NotImplementedException();
                }

                string suffixb =  "(" + alu.AWidth + ", " + alu.BWidth + " => " + alu.RWidth + ")";
                string suffixu =  "(" + alu.AWidth + " => " + alu.RWidth + ")";
                switch (alu.FuncSel)
                {
                    case ALU.EFunction.Add:
                        return "integer adder " + suffixb;
                    case ALU.EFunction.And:
                        return "bitwise and " + suffixb;
                    case ALU.EFunction.Compare:
                        return arith + " " + alu.AWidth + " bit comparator";
                    case ALU.EFunction.Mul:
                        return arith + " multiplier" + suffixb;
                    case ALU.EFunction.Neg:
                        return "negation " + suffixu;
                    case ALU.EFunction.Not:
                        return alu.AWidth + " bit inverter";
                    case ALU.EFunction.Or:
                        return "bitwise or " + suffixb;
                    case ALU.EFunction.Sub:
                        return "integer subtractor " + suffixb;
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }

    /// <summary>
    /// Implements a service for mapping XIL instructions to arithmetic-logical units (i.e. instances of ALU)
    /// </summary>
    public class ALUXILMapper : IXILMapper
    {
        /// <summary>
        /// Computes a somehow optimal pipeline depth from a given parametrization
        /// </summary>
        /// <param name="op">preselected operation</param>
        /// <param name="mode">signedness of operands/result</param>
        /// <param name="osize0">size of first (or sole) operand</param>
        /// <param name="osize1">size of second operand (if applicable)</param>
        /// <param name="rsize">size of result (if applicable)</param>
        /// <returns>desired pipeline depth (i.e. mapping lateny)</returns>
        public delegate int PipelineDepthCalcFunc(ALU.EFunction op, ALU.EArithMode mode, int osize0, int osize1, int rsize);

        /// <summary>
        /// Custom pipeline depth calculation function, preinitialized with a simple default implementation.
        /// </summary>
        public PipelineDepthCalcFunc CalcPipelineDepth { get; set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public ALUXILMapper()
        {
            CalcPipelineDepth = DefaultCalcPipelineDepth;
        }

        private int DefaultCalcPipelineDepth(ALU.EFunction op, ALU.EArithMode mode, int osize0, int osize1, int rsize)
        {
            switch (op)
            {
                case ALU.EFunction.And:
                case ALU.EFunction.Not:
                case ALU.EFunction.Or:
                    return 0;

                case ALU.EFunction.Add:
                case ALU.EFunction.Compare:
                case ALU.EFunction.Neg:
                case ALU.EFunction.Sub:
                    return 1;

                case ALU.EFunction.Mul:
                    return 2;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Supports add, sub, mul, neg, not, and, or, islt, islte, iseq, isneq, isgte, isgt
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Add();
            yield return DefaultInstructionSet.Instance.Sub();
            yield return DefaultInstructionSet.Instance.Mul();
            yield return DefaultInstructionSet.Instance.Neg();
            yield return DefaultInstructionSet.Instance.Not();
            yield return DefaultInstructionSet.Instance.And();
            yield return DefaultInstructionSet.Instance.Or();
            yield return DefaultInstructionSet.Instance.IsLt();
            yield return DefaultInstructionSet.Instance.IsLte();
            yield return DefaultInstructionSet.Instance.IsEq();
            yield return DefaultInstructionSet.Instance.IsNEq();
            yield return DefaultInstructionSet.Instance.IsGte();
            yield return DefaultInstructionSet.Instance.IsGt();
        }

        private bool CheckFixComplianceInternal(XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            if (!operandTypes[0].CILType.Equals(typeof(SFix)) &&
                !operandTypes[0].CILType.Equals(typeof(UFix)))
            {
                return true;
            }

            dynamic smp0 = operandTypes[0].GetSampleInstance();
            dynamic smp1 = operandTypes.Length > 1 ? operandTypes[1].GetSampleInstance() : null;
            dynamic smpr = resultTypes[0].GetSampleInstance();
            object smpe;

            try
            {
                switch (instr.Name)
                {
                    case InstructionCodes.Add:
                    case InstructionCodes.Sub:
                        smpe = smp0 + smp1;
                        break;

                    case InstructionCodes.Mul:
                        smpe = smp0 * smp1;
                        break;

                    case InstructionCodes.Neg:
                        smpe = -smp0;
                        break;

                    default:
                        return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            var fmtr = resultTypes[0].GetFixFormat();
            var fmte = TypeDescriptor.GetTypeOf(smpe).GetFixFormat();
            return fmte.FracWidth == fmtr.FracWidth;
        }

        private bool CheckFixCompliance(XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var oldMode = DesignContext.Instance.FixPoint.ArithSizingMode;
            DesignContext.Instance.FixPoint.ArithSizingMode = EArithSizingMode.VHDLCompliant;
            bool result = CheckFixComplianceInternal(instr, operandTypes, resultTypes);
            DesignContext.Instance.FixPoint.ArithSizingMode = oldMode;
            return result;
        }

        private IXILMapping TryMapOne(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, bool swap)
        {
            var fu = taSite.Host;
            ALU alu = fu as ALU;
            if (alu == null)
                return null;
            if (resultTypes.Length != 1)
                return null;
            TypeDescriptor rtype = resultTypes[0];
            if (!rtype.IsComplete)
                return null;

            if (!CheckFixCompliance(instr, operandTypes, resultTypes))
                return null;

            int rsize = TypeLowering.Instance.GetWireWidth(rtype);
            int[] osizes = operandTypes.Select(t => TypeLowering.Instance.GetWireWidth(t)).ToArray();
            Func<ISignalSource<StdLogicVector>[], ISignalSink<StdLogicVector>[], IEnumerable<TAVerb>> realize;
            if (operandTypes.Length == 1)
            {
                realize = (os, rs) => alu.Transactor.Do(os[0], rs[0]);

                TypeDescriptor otype = operandTypes[0];
                long osize = osizes[0];
                switch (instr.Name)
                {
                    case InstructionCodes.Neg:
                        if (alu.FuncSel != ALU.EFunction.Neg)
                            return null;
                        if ((!otype.CILType.Equals(typeof(Signed)) ||
                            !rtype.CILType.Equals(typeof(Signed))) &&
                            (!otype.CILType.Equals(typeof(SFix)) ||
                            !rtype.CILType.Equals(typeof(SFix))))
                            return null;
                        if (alu.AWidth != osize ||
                            alu.RWidth != rsize)
                            return null;
                        break;

                    case InstructionCodes.Not:
                        if (alu.FuncSel != ALU.EFunction.Not)
                            return null;
                        if (!otype.CILType.Equals(typeof(StdLogicVector)) ||
                            !rtype.CILType.Equals(typeof(StdLogicVector)))
                            return null;
                        if (alu.AWidth != osize ||
                            alu.RWidth != osize)
                            return null;
                        break;

                    default:
                        return null;
                }
            }
            else
            {
                realize = (os, rs) => alu.Transactor.Do(os[0], os[1], rs[0]);

                TypeDescriptor otype0 = operandTypes[0];
                TypeDescriptor otype1 = operandTypes[1];
                long osize0 = osizes[0];
                long osize1 = osizes[1];
                if (alu.AWidth != osize0 ||
                    alu.BWidth != osize1 ||
                    (alu.FuncSel != ALU.EFunction.Compare && alu.RWidth != rsize))
                    return null;
                bool isArith = false;
                switch (instr.Name)
                {
                    case InstructionCodes.Add:
                    case InstructionCodes.Sub:
                    case InstructionCodes.Mul:
                        isArith = true;
                        goto case InstructionCodes.IsLt;

                    case InstructionCodes.IsLt:
                    case InstructionCodes.IsLte:
                    case InstructionCodes.IsEq:
                    case InstructionCodes.IsNEq:
                    case InstructionCodes.IsGte:
                    case InstructionCodes.IsGt:
                        switch (alu.ArithMode)
                        {
                            case ALU.EArithMode.Signed:
                                if ((!otype0.CILType.Equals(typeof(Signed)) ||
                                    !otype1.CILType.Equals(typeof(Signed)) ||
                                    (isArith && !rtype.CILType.Equals(typeof(Signed))) ||
                                    (!isArith && !rtype.CILType.Equals(typeof(bool)) &&
                                    !rtype.CILType.Equals(typeof(StdLogicVector)))) &&

                                    (!otype0.CILType.Equals(typeof(SFix)) ||
                                    !otype1.CILType.Equals(typeof(SFix)) ||
                                    (isArith && !rtype.CILType.Equals(typeof(SFix))) ||
                                    (!isArith && !rtype.CILType.Equals(typeof(bool)) &&
                                    !rtype.CILType.Equals(typeof(StdLogicVector)))))
                                    return null;
                                break;

                            case ALU.EArithMode.Unsigned:
                                if ((!(otype0.CILType.Equals(typeof(Unsigned)) || otype0.CILType.Equals(typeof(StdLogicVector))) ||
                                    !(otype1.CILType.Equals(typeof(Unsigned)) || otype1.CILType.Equals(typeof(StdLogicVector))) ||
                                    (isArith && !(rtype.CILType.Equals(typeof(Unsigned)) || rtype.CILType.Equals(typeof(StdLogicVector)))) ||
                                    (!isArith && !rtype.CILType.Equals(typeof(bool)) &&
                                    !rtype.CILType.Equals(typeof(StdLogicVector)))) &&

                                    (!(otype0.CILType.Equals(typeof(UFix)) || otype0.CILType.Equals(typeof(StdLogicVector)) || otype0.CILType.Equals(typeof(StdLogic))) ||
                                    !(otype1.CILType.Equals(typeof(UFix)) || otype1.CILType.Equals(typeof(StdLogicVector)) || otype1.CILType.Equals(typeof(StdLogic))) ||
                                    (isArith && !rtype.CILType.Equals(typeof(UFix))) ||
                                    (!isArith && !rtype.CILType.Equals(typeof(bool)) &&
                                    !rtype.CILType.Equals(typeof(StdLogicVector)))))
                                    return null;
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        switch (alu.FuncSel)
                        {
                            case ALU.EFunction.Add:
                                if (!instr.Name.Equals(InstructionCodes.Add))
                                    return null;
                                break;

                            case ALU.EFunction.Sub:
                                if (!instr.Name.Equals(InstructionCodes.Sub))
                                    return null;
                                break;

                            case ALU.EFunction.Mul:
                                if (!instr.Name.Equals(InstructionCodes.Mul))
                                    return null;
                                break;

                            case ALU.EFunction.Compare:
                                switch (instr.Name)
                                {
                                    case InstructionCodes.IsLt:
                                        realize = (os, rs) => alu.Transactor.IsLt(os[0], os[1], rs[0]);
                                        break;
                                    case InstructionCodes.IsLte:
                                        realize = (os, rs) => alu.Transactor.IsLte(os[0], os[1], rs[0]);
                                        break;
                                    case InstructionCodes.IsEq:
                                        realize = (os, rs) => alu.Transactor.IsEq(os[0], os[1], rs[0]);
                                        break;
                                    case InstructionCodes.IsNEq:
                                        realize = (os, rs) => alu.Transactor.IsNEq(os[0], os[1], rs[0]);
                                        break;
                                    case InstructionCodes.IsGte:
                                        realize = (os, rs) => alu.Transactor.IsGte(os[0], os[1], rs[0]);
                                        break;
                                    case InstructionCodes.IsGt:
                                        realize = (os, rs) => alu.Transactor.IsGt(os[0], os[1], rs[0]);
                                        break;
                                    default:
                                        return null;
                                }
                                break;
                        }
                        break;

                    case InstructionCodes.And:
                        if (alu.FuncSel != ALU.EFunction.And)
                            return null;
                        break;

                    case InstructionCodes.Or:
                        if (alu.FuncSel != ALU.EFunction.Or)
                            return null;
                        break;
                }
            }
            return new ALUXILMapping(alu.Transactor, realize, swap);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            IXILMapping alt0 = null, alt1 = null;
            alt0 = TryMapOne(taSite, instr, operandTypes, resultTypes, false);
            switch (instr.Name)
            {
                case InstructionCodes.Add:
                case InstructionCodes.Mul:
                case InstructionCodes.And:
                case InstructionCodes.Or:
                case InstructionCodes.IsEq:
                case InstructionCodes.IsNEq:
                    // These operations are commutative => "a op b", "b op a" are both feasible
                    alt1 = TryMapOne(taSite, instr, new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsGt:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsLt(), 
                        new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsGte:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsLte(),
                        new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsLt:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsGt(),
                        new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsLte:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsGte(),
                        new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;
            }

            if (alt0 != null)
                yield return alt0;

            if (alt1 != null)
                yield return alt1;
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (!CheckFixCompliance(instr, operandTypes, resultTypes))
                return null;

            bool isArith = false;
            int osize0, osize1 = 0;
            int rsize;
            ALU.EArithMode amode;
            ALU.EFunction op;
            switch (instr.Name)
            {
                case InstructionCodes.Add:
                case InstructionCodes.Sub:
                case InstructionCodes.Mul:
                    isArith = true;
                    goto case InstructionCodes.IsLt;

                case InstructionCodes.IsLt:
                case InstructionCodes.IsLte:
                case InstructionCodes.IsEq:
                case InstructionCodes.IsNEq:
                case InstructionCodes.IsGte:
                case InstructionCodes.IsGt:
                    {
                        if (operandTypes.All(t => t.CILType.Equals(typeof(Signed))) &&
                            (!isArith || resultTypes[0].CILType.Equals(typeof(Signed))) &&
                            (isArith || resultTypes[0].CILType.Equals(typeof(bool)) ||
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector))))
                            amode = ALU.EArithMode.Signed;
                        else if (operandTypes.All(t => t.CILType.Equals(typeof(SFix))) &&
                            (!isArith || resultTypes[0].CILType.Equals(typeof(SFix))) &&
                            (isArith || resultTypes[0].CILType.Equals(typeof(bool)) ||
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector))))
                            amode = ALU.EArithMode.Signed;
                        else if (operandTypes.All(t => t.CILType.Equals(typeof(Unsigned))) &&
                            (!isArith || resultTypes[0].CILType.Equals(typeof(Unsigned))) &&
                            (isArith || resultTypes[0].CILType.Equals(typeof(bool)) ||
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector))))
                            amode = ALU.EArithMode.Unsigned;
                        else if (operandTypes.All(t => t.CILType.Equals(typeof(UFix))) &&
                            (!isArith || resultTypes[0].CILType.Equals(typeof(UFix))) &&
                            (isArith || resultTypes[0].CILType.Equals(typeof(bool)) ||
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector))))
                            amode = ALU.EArithMode.Unsigned;
                        else if (operandTypes.All(t => t.CILType.Equals(typeof(StdLogicVector))) &&
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector)))
                            amode = ALU.EArithMode.Unsigned;
                        else if (operandTypes.All(t => t.CILType.Equals(typeof(StdLogic))) &&
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector)) &&
                            (instr.Name == InstructionCodes.IsEq ||
                            instr.Name == InstructionCodes.IsNEq))
                            amode = ALU.EArithMode.Unsigned;
                        else
                            return null;
                        osize0 = TypeLowering.Instance.GetWireWidth(operandTypes[0]);
                        osize1 = TypeLowering.Instance.GetWireWidth(operandTypes[1]);
                        rsize = TypeLowering.Instance.GetWireWidth(resultTypes[0]); ;

                        switch (instr.Name)
                        {
                            case InstructionCodes.Add:
                                op = ALU.EFunction.Add;
                                break;

                            case InstructionCodes.Sub:
                                op = ALU.EFunction.Sub;
                                break;

                            case InstructionCodes.Mul:
                                op = ALU.EFunction.Mul;
                                break;

                            case InstructionCodes.IsLt:
                            case InstructionCodes.IsLte:
                            case InstructionCodes.IsEq:
                            case InstructionCodes.IsNEq:
                            case InstructionCodes.IsGte:
                            case InstructionCodes.IsGt:
                                op = ALU.EFunction.Compare;
                                break;

                            default:
                                throw new InvalidOperationException();
                        }
                    }
                    break;

                case InstructionCodes.Neg:
                    {
                        if (operandTypes[0].CILType.Equals(typeof(Signed)) &&
                            resultTypes[0].CILType.Equals(typeof(Signed)))
                            amode = ALU.EArithMode.Signed;
                        else if (operandTypes[0].CILType.Equals(typeof(SFix)) &&
                            resultTypes[0].CILType.Equals(typeof(SFix)))
                            amode = ALU.EArithMode.Signed;
                        else if (operandTypes[0].CILType.Equals(typeof(Unsigned)) &&
                            resultTypes[0].CILType.Equals(typeof(Unsigned)))
                            amode = ALU.EArithMode.Unsigned;
                        else if (operandTypes[0].CILType.Equals(typeof(UFix)) &&
                            resultTypes[0].CILType.Equals(typeof(UFix)))
                            amode = ALU.EArithMode.Unsigned;
                        else
                            return null;
                        osize0 = TypeLowering.Instance.GetWireWidth(operandTypes[0]);
                        rsize = TypeLowering.Instance.GetWireWidth(resultTypes[0]);

                        //alu = new ALU(ALU.EFunction.Neg, amode, 1, osize, 0, rsize);
                        op = ALU.EFunction.Neg;                        
                    }
                    break;

                case InstructionCodes.And:
                case InstructionCodes.Or:
                    {
                        if (operandTypes.Length != 2 ||
                            resultTypes.Length != 1)
                            return null;

                        if (!((operandTypes[0].CILType.Equals(typeof(StdLogicVector)) &&
                            operandTypes[1].CILType.Equals(typeof(StdLogicVector)) &&
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector))) ||

                            (operandTypes[0].CILType.Equals(typeof(Unsigned)) &&
                            operandTypes[1].CILType.Equals(typeof(Unsigned)) &&
                            resultTypes[0].CILType.Equals(typeof(Unsigned)))))
                            return null;

                        osize0 = TypeLowering.Instance.GetWireWidth(operandTypes[0]);
                        osize1 = TypeLowering.Instance.GetWireWidth(operandTypes[1]);
                        rsize = TypeLowering.Instance.GetWireWidth(resultTypes[0]);
                        amode = ALU.EArithMode.Signed;

                        switch (instr.Name)
                        {
                            case InstructionCodes.And:
                                //alu = new ALU(ALU.EFunction.And, ALU.EArithMode.Signed, 0, osize0, osize1, rsize);
                                op = ALU.EFunction.And;
                                break;

                            case InstructionCodes.Or:
                                //alu = new ALU(ALU.EFunction.Or, ALU.EArithMode.Signed, 0, osize0, osize1, rsize);
                                op = ALU.EFunction.Or;
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                    }
                    break;

                case InstructionCodes.Not:
                    {
                        if (operandTypes.Length != 1 ||
                            resultTypes.Length != 1)
                            return null;

                        if (!((operandTypes[0].CILType.Equals(typeof(StdLogicVector)) &&
                            resultTypes[0].CILType.Equals(typeof(StdLogicVector)))))
                            return null;

                        osize0 = TypeLowering.Instance.GetWireWidth(operandTypes[0]);
                        rsize = TypeLowering.Instance.GetWireWidth(resultTypes[0]);

                        //alu = new ALU(ALU.EFunction.Not, ALU.EArithMode.Unsigned, 0, osize, 0, rsize);
                        amode = ALU.EArithMode.Unsigned;
                        op = ALU.EFunction.Not;
                    }
                    break;

                default:
                    return null;
            }

            int pdepth = CalcPipelineDepth(op, amode, osize0, osize1, rsize);
            ALU alu = new ALU(op, amode, pdepth, osize0, osize1, rsize);
            return TryMapOne(alu.Transactor, instr, operandTypes, resultTypes, false);
        }
    }

    public static class ALUFuncSelExtension
    {
        /// <summary>
        /// Returns true if function is unary (Neg/Not)
        /// </summary>
        public static bool IsUnary(this ALU.EFunction func)
        {
            return func == ALU.EFunction.Neg || func == ALU.EFunction.Not;
        }

        /// <summary>
        /// Returns true if function is binary (Add/Sub/Mul/Compare)
        /// </summary>
        public static bool IsBinary(this ALU.EFunction func)
        {
            return !IsUnary(func);
        }

        /// <summary>
        /// Returns true if function is logical (And/Or/Not)
        /// </summary>
        public static bool IsLogical(this ALU.EFunction func)
        {
            return func == ALU.EFunction.And || func == ALU.EFunction.Or || func == ALU.EFunction.Not;
        }

        /// <summary>
        /// Returns true if function is arithmetical (Add/Sub/Mul/Compare)
        /// </summary>
        public static bool IsArithmetical(this ALU.EFunction func)
        {
            return !IsLogical(func);
        }
    }
}

/**
 * Copyright 2012-2013 Christian Köllner
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
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// Transaction site interface for <c>FixedAbs</c> which implements the absolute value function for fixed-point numbers.
    /// </summary>
    public interface IFixedAbsTransactionSite : ITransactionSite
    {
        /// <summary>
        /// Returns a transaction for computing the absolute value function.
        /// </summary>
        /// <param name="operand">operand source</param>
        /// <param name="result">result sink</param>
        IEnumerable<TAVerb> Abs(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// The hosting <c>FixedAbs</c> component
        /// </summary>
        new FixedAbs Host { get; }
    }

    /// <summary>
    /// Implements a synthesizable absolute value function for fixed-point arithmetic. The component is 
    /// intended to be used during high-level synthesis for mapping basic arithmetic/logical instructions.
    /// </summary>
    [DeclareXILMapper(typeof(FixedAbsXILMapper))]
    public class FixedAbs: Component
    {
        private class TASiteImpl : 
            DefaultTransactionSite,
            IFixedAbsTransactionSite
        {
            private FixedAbs _host;
            private bool _established;

            public TASiteImpl(FixedAbs host) :
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(ETVMode.Locked,
                    _host.Operand.Dual.Stick(StdLogicVector.DCs(_host.InputWidth)));
            }

            public IEnumerable<TAVerb> Abs(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        _host.Operand.Dual.Drive(operand),
                        result.Comb.Connect(_host.Result.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked,
                        _host.Operand.Dual.Drive(operand));
                    for (int i = 1; i < _host.Latency; i++)
                        yield return Verb(ETVMode.Shared);
                    yield return Verb(ETVMode.Shared,
                        result.Comb.Connect(_host.Result.Dual.AsSignalSource()));
                }
            }

            public new FixedAbs Host
            {
                get { return _host; }
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_established)
                    return;

                if (_host.Latency > 0)
                    _host.Clk = (SLSignal)binder.GetSignal(EPortUsage.Clock, null, null, null);
                _host.Operand = (SLVSignal)binder.GetSignal(EPortUsage.Operand, "Operand", null, StdLogicVector._0s(_host.InputWidth));
                _host.Result = (SLVSignal)binder.GetSignal(EPortUsage.Result, "Result", null, StdLogicVector._0s(_host.OutputWidth));
                _established = true;
            }
        }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public IFixedAbsTransactionSite TASite { get; private set; }

        /// <summary>
        /// Latency of computation
        /// </summary>
        public int Latency { get; private set; }

        /// <summary>
        /// Clock signal input
        /// </summary>
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// Operand input
        /// </summary>
        public In<StdLogicVector> Operand { private get; set; }

        /// <summary>
        /// Result (absolute value) output
        /// </summary>
        public Out<StdLogicVector> Result { private get; set; }

        /// <summary>
        /// Bit-width of operand
        /// </summary>
        public int InputWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Bit-width of result
        /// </summary>
        public int OutputWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>FixedAbs</c> instance with
        /// same parametrization.
        /// </summary>
        public override bool IsEquivalent(Component obj)
        {
            var other = obj as FixedAbs;
            if (other == null)
                return false;
            return InputWidth == other.InputWidth &&
                OutputWidth == other.OutputWidth;
        }

        public override int GetBehaviorHashCode()
        {
            return InputWidth ^ OutputWidth;
        }

        private SLVSignal _pipeIn;
        private SLVSignal _pipeOut;
        private RegPipe _rpipe;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="inWidth">bit-width of operand</param>
        /// <param name="outWidth">bit-width of result</param>
        /// <param name="latency">desired latency</param>
        public FixedAbs(int inWidth, int outWidth, int latency)
        {
            InputWidth = inWidth;
            OutputWidth = outWidth;
            Latency = latency;
            if (latency > 1)
            {
                _pipeIn = new SLVSignal(outWidth);
                _pipeOut = new SLVSignal(outWidth);
                _rpipe = new RegPipe(latency, inWidth);
            }
            TASite = new TASiteImpl(this);
        }

        protected override void PreInitialize()
        {
            if (Latency > 1)
            {
                _rpipe.Clk = Clk;
                _rpipe.Din = _pipeIn;
                _rpipe.Dout = _pipeOut;
            }
        }

        protected override void Initialize()
        {
            if (Latency == 0)
                AddProcess(ComputeAbs0, Operand);
            else if (Latency == 1)
                AddProcess(ComputeAbs1, Clk);
            else
                AddProcess(ComputeAbsN, Operand, _pipeOut);
        }

        private void ComputeAbs0()
        {
            if (Operand.Cur[InputWidth - 1] == '1')
                Result.Next = (-Operand.Cur.SignedValue).Resize(OutputWidth).SLVValue;
            else
                Result.Next = (Operand.Cur.SignedValue).Resize(OutputWidth).SLVValue;
        }

        private void ComputeAbs1()
        {
            if (Clk.RisingEdge())
            {
                if (Operand.Cur[InputWidth - 1] == '1')
                    Result.Next = (-Operand.Cur.SignedValue).Resize(OutputWidth).SLVValue;
                else
                    Result.Next = (Operand.Cur.SignedValue).Resize(OutputWidth).SLVValue;
            }
        }

        private void ComputeAbsN()
        {
            if (Operand.Cur[InputWidth - 1] == '1')
                _pipeIn.Next = (-Operand.Cur.SignedValue).Resize(OutputWidth).SLVValue;
            else
                _pipeIn.Next = (Operand.Cur.SignedValue).Resize(OutputWidth).SLVValue;
            Result.Next = _pipeOut.Cur;
        }
    }

    /// <summary>
    /// A service for mapping the "abs" (absolute value) XIL instruction with fixed-point arithmetic to hardware.
    /// </summary>
    public class FixedAbsXILMapper : IXILMapper
    {
        private class AbsMapping : IXILMapping
        {
            private FixedAbs _fu;

            public AbsMapping(FixedAbs fu)
            {
                _fu = fu;
            }

            public ITransactionSite TASite
            {
                get { return _fu.TASite; }
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
                get { return _fu.Latency; }
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _fu.TASite.Abs(operands[0], results[0]);
            }

            public string Description
            {
                get { return _fu.InputWidth + " => " + _fu.OutputWidth + " bit integer absolute value"; }
            }
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public FixedAbsXILMapper()
        {
            ComputeLatency = (x, y) => 1;
        }

        /// <summary>
        /// Gets or sets a user-defined function for determining the optimal computation latency, based on
        /// operand and result bit-widths. First argument is operand bit-width, second argument is result bit-width.
        /// Result is desired latency. The property is pre-initialized with a default function which always returns 1.
        /// </summary>
        public Func<int, int, int> ComputeLatency { get; set; }

        /// <summary>
        /// Returns abs
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Abs();
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            IFixedAbsTransactionSite fats = taSite as IFixedAbsTransactionSite;
            if (fats == null)
                yield break;

            if (instr.Name != InstructionCodes.Abs)
                yield break;

            var operandFormat = operandTypes[0].GetFixFormat();
            if (operandFormat == null)
                yield break;

            var resultFormat = resultTypes[0].GetFixFormat();
            if (resultFormat == null)
                yield break;

            if (operandFormat.FracWidth != resultFormat.FracWidth)
                yield break;

            if (operandFormat.TotalWidth != fats.Host.InputWidth)
                yield break;

            if (resultFormat.TotalWidth != fats.Host.OutputWidth)
                yield break;

            yield return new AbsMapping(fats.Host);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            if (instr.Name != InstructionCodes.Abs)
                return null;

            var operandFormat = operandTypes[0].GetFixFormat();
            if (operandFormat == null)
                return null;

            var resultFormat = resultTypes[0].GetFixFormat();
            if (resultFormat == null)
                return null;

            if (operandFormat.FracWidth != resultFormat.FracWidth)
                return null;

            var fu = new FixedAbs(
                operandFormat.TotalWidth, 
                resultFormat.TotalWidth,
                ComputeLatency(operandFormat.TotalWidth, resultFormat.TotalWidth));
            return new AbsMapping(fu);
        }
    }
}

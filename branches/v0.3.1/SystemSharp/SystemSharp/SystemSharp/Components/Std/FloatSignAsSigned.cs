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
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// Transaction site interface for <c>FloatSignAsSigned</c>, a component for computing the sign of
    /// floating-point operands.
    /// </summary>
    public interface IFloatSignAsSignedTransactionSite :
        ITransactionSite
    {
        /// <summary>
        /// Returns a transaction which computes the sign of the operand.
        /// </summary>
        /// <param name="operand">operand source</param>
        /// <param name="result">result (i.e. sign) sink</param>
        IEnumerable<TAVerb> Sign(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result);
    }

    /// <summary>
    /// Provides a synthesizable implementation of the signum function for floating-point arithmetic. 
    /// The result is returned as a signed fixed-point number (-1, 0 or 1) with configurable bit-width.
    /// The component is intended to be used during high-level synthesis for mapping basic arithmetic/logical instructions.
    /// </summary>
    [DeclareXILMapper(typeof(FloatSignAsSignedXILMapper))]
    public class FloatSignAsSigned: Component
    {
        private class TransactionSite : 
            DefaultTransactionSite,
            IFloatSignAsSignedTransactionSite
        {
            private FloatSignAsSigned _host;
            private bool _established;

            public TransactionSite(FloatSignAsSigned host) :
                base(host)
            {
                _host = host;
            }

            public IEnumerable<TAVerb> Sign(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                yield return Verb(ETVMode.Locked,
                    _host.DIn.Dual.Drive(operand),
                    result.Comb.Connect(_host.DOut.Dual.AsSignalSource()));
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(ETVMode.Locked,
                    _host.DIn.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.FloatWidth))));
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_established)
                    return;

                _host.DIn = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "DIn", null, StdLogicVector._0s(_host.FloatWidth));
                _host.DOut = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "DOut", null, StdLogicVector._0s(_host.OutFormat.TotalWidth));

                _established = true;
            }
        }

        /// <summary>
        /// Operand input
        /// </summary>
        public In<StdLogicVector> DIn { private get; set; }

        /// <summary>
        /// Result (i.e. sign) output
        /// </summary>
        public Out<StdLogicVector> DOut { private get; set; }

        /// <summary>
        /// Bit-width of operand
        /// </summary>
        [PerformanceRelevant]
        public int FloatWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fixed-point format of result
        /// </summary>
        public FixFormat OutFormat { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>FloatSignAsSigned</c> instance
        /// with same parametrization.
        /// </summary>
        public override bool IsEquivalent(Component obj)
        {
            var other = obj as FloatSignAsSigned;
            if (other == null)
                return false;
            return FloatWidth == other.FloatWidth &&
                OutFormat.Equals(other.OutFormat);
        }

        public override int GetBehaviorHashCode()
        {
            return FloatWidth ^
                OutFormat.GetHashCode();
        }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public IFloatSignAsSignedTransactionSite TASite { get; private set; }

        private StdLogicVector _outM1;
        private StdLogicVector _out0;
        private StdLogicVector _out1;
        private StdLogicVector _zeros;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="floatWidth">total bit-width of input floating-point number
        /// (actual partitioning between exponent and mantissa bits does not matter)</param>
        /// <param name="outFormat">desired fixed-point output format</param>
        public FloatSignAsSigned(int floatWidth, FixFormat outFormat)
        {
            FloatWidth = floatWidth;
            OutFormat = outFormat;

            _outM1 = SFix.FromDouble(-1.0, outFormat.IntWidth, outFormat.FracWidth).SLVValue;
            _out0 = SFix.FromDouble(0.0, outFormat.IntWidth, outFormat.FracWidth).SLVValue;
            _out1 = SFix.FromDouble(1.0, outFormat.IntWidth, outFormat.FracWidth).SLVValue;
            _zeros = StdLogicVector._0s(floatWidth - 1);

            TASite = new TransactionSite(this);
        }

        private void ComputeSign()
        {
            if (DIn.Cur[FloatWidth - 1] == '0')
            {
                if (DIn.Cur[FloatWidth - 2, 0] == _zeros)
                {
                    DOut.Next = _out0;
                }
                else
                {
                    DOut.Next = _out1;
                }
            }
            else
            {
                DOut.Next = _outM1;
            }
        }

        protected override void Initialize()
        {
            AddProcess(ComputeSign, DIn);
        }
    }

    /// <summary>
    /// A service for mapping the "sign" (signum) XIL instruction 
    /// with floating-point arithmetic to hardware.
    /// </summary>
    public class FloatSignAsSignedXILMapper : IXILMapper
    {
        private class Mapping : IXILMapping
        {
            private FloatSignAsSigned _host;

            public Mapping(FloatSignAsSigned host)
            {
                _host = host;
            }

            public ITransactionSite TASite
            {
                get { return _host.TASite; }
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
                get { return 0; }
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _host.TASite.Sign(operands[0], results[0]);
            }

            public string Description
            {
                get { return _host.FloatWidth + " bit fixed/floating-point sign"; }
            }
        }

        /// <summary>
        /// Returns sign
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Sign();
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            var fsas = fu as FloatSignAsSigned;
            if (fsas == null)
                yield break;

            if (instr.Name != InstructionCodes.Sign)
                yield break;

            int floatWidth;
            if (operandTypes[0].CILType.Equals(typeof(float)))
                floatWidth = 32;
            else if (operandTypes[0].CILType.Equals(typeof(double)))
                floatWidth = 64;
            else if (operandTypes[0].CILType.Equals(typeof(Signed)) ||
                operandTypes[0].CILType.Equals(typeof(SFix)))
                floatWidth = operandTypes[0].GetFixFormat().TotalWidth;
            else
                yield break;
            if (floatWidth != fsas.FloatWidth)
                yield break;

            var fmt = resultTypes[0].GetFixFormat();
            if (fmt == null || !fmt.IsSigned || fmt.IntWidth < 2)
                yield break;

            if (!fmt.Equals(fsas.OutFormat))
                yield break;

            yield return new Mapping(fsas);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            if (instr.Name != InstructionCodes.Sign)
                return null;

            int floatWidth;
            if (operandTypes[0].CILType.Equals(typeof(float)))
                floatWidth = 32;
            else if (operandTypes[0].CILType.Equals(typeof(double)))
                floatWidth = 64;
            else if (operandTypes[0].CILType.Equals(typeof(Signed)) ||
                operandTypes[0].CILType.Equals(typeof(SFix)))
                floatWidth = operandTypes[0].GetFixFormat().TotalWidth;
            else
                return null;

            var fmt = resultTypes[0].GetFixFormat();
            if (fmt == null || !fmt.IsSigned || fmt.IntWidth < 2)
                return null;

            var fsas = new FloatSignAsSigned(floatWidth, fmt);
            return new Mapping(fsas);
        }
    }
}

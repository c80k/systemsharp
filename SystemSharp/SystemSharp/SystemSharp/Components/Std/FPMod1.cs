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
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Assembler;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    public interface IFPMod1TransactionSite : ITransactionSite
    {
        IEnumerable<TAVerb> Mod1(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result);
    }

    /// <summary>
    /// Given a fixed-point number x, this component computes the fixed-point modulus of x and 1.0: r = mod(x, 1.0)
    /// The modulus operation is generalized to fixed-point numbers in the following manner: r is a number such that
    /// r &gt;= -1 and r &lt;= 1 and x = k + r with k being an integer.
    /// The component is intended to be used during high-level synthesis for mapping basic arithmetic/logical instructions.
    /// </summary>
    [DeclareXILMapper(typeof(FPMod1XILMapper))]
    public class FixPMod1 : Component
    {
        private class FPMod1TransactionSite :
            DefaultTransactionSite,
            IFPMod1TransactionSite
        {
            private FixPMod1 _host;
            private bool _established;

            public FPMod1TransactionSite(FixPMod1 host) :
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(ETVMode.Locked, _host.X.Dual.Stick(StdLogicVector.DCs(_host.InIntWidth + _host.FracWidth)));
            }

            public IEnumerable<TAVerb> Mod1(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                yield return Verb(ETVMode.Locked,
                    _host.X.Dual.Drive(operand),
                    result.Comb.Connect(_host.R.Dual.AsSignalSource()));
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_established)
                    return;

                _host.X = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "X", null, StdLogicVector._0s(_host.InIntWidth + _host.FracWidth));
                _host.R = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "R", null, StdLogicVector._0s(_host.OutIntWidth + _host.FracWidth));

                _established = true;
            }
        }

        /// <summary>
        /// Operand input
        /// </summary>
        public In<StdLogicVector> X { private get; set; }

        /// <summary>
        /// Result output
        /// </summary>
        public Out<StdLogicVector> R { private get; set; }

        /// <summary>
        /// Number of operand integer bits
        /// </summary>
        [PerformanceRelevant]
        public int InIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Number of output integer bits
        /// </summary>
        [PerformanceRelevant]
        public int OutIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Number of operand and result fractional bits
        /// </summary>
        [PerformanceRelevant]
        public int FracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public IFPMod1TransactionSite TASite { get; private set; }

        private StdLogicVector _zeroes;
        private StdLogicVector _padZeroes;
        private StdLogicVector _padOnes;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="inIntWidth">operand integer bits</param>
        /// <param name="fracWidth">operand and result fractional bits
        /// (operand and result automatically have same number of fractional bits)</param>
        /// <param name="outIntWidth">result integer bits</param>
        public FixPMod1(int inIntWidth, int fracWidth, int outIntWidth)
        {
            Contract.Requires(inIntWidth >= 2 && outIntWidth >= 2);

            InIntWidth = inIntWidth;
            OutIntWidth = outIntWidth;
            FracWidth = fracWidth;

            _zeroes = StdLogicVector._0s(FracWidth);
            _padZeroes = StdLogicVector._0s(OutIntWidth - 1);
            _padOnes = StdLogicVector._1s(OutIntWidth - 1);
            TASite = new FPMod1TransactionSite(this);
        }

        private void Process()
        {
            StdLogic ib1 = X.Cur[FracWidth + 1];
            StdLogic ib0 = X.Cur[FracWidth];
            StdLogicVector rv = X.Cur[FracWidth - 1, 0];
            bool ibf1 = ib1 == '1';
            bool ibf0 = ib0 == '1';
            bool rvz = rv == _zeroes;
            bool flag = ibf0 && (ibf1 || !rvz);
            if (flag)
            {
                R.Next = _padOnes.Concat(ib0.Concat(rv));
            }
            else
            {
                R.Next = _padZeroes.Concat(ib0.Concat(rv));
            }
        }

        protected override void Initialize()
        {
            AddProcess(Process, X);
        }
    }

    /// <summary>
    /// A service for mapping the "mod2" and "rempow2" (remainder function for constant divisors which are a power of 2) 
    /// XIL instructions with fixed-point arithmetic to hardware. "rempow2" is not yet fully supported: the restriction is that
    /// the static operand must be 0, implying a divisor of 2^0 = 1.
    /// </summary>
    public class FPMod1XILMapper : IXILMapper
    {
        private class FPMod1XILMapping : IXILMapping
        {
            private FixPMod1 _host;

            public FPMod1XILMapping(FixPMod1 host)
            {
                _host = host;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _host.TASite.Mod1(operands[0], results[0]);
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
                get { return 1; }
            }

            public string Description
            {
                get
                {
                    return "sfix" + (_host.InIntWidth + _host.FracWidth) + "_" + _host.FracWidth + " => " +
                        (_host.OutIntWidth + _host.FracWidth) + "_" + _host.FracWidth + " modulus 2";
                }
            }
        }

        /// <summary>
        /// Returns mod2 and rempow2
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Mod2();
            yield return DefaultInstructionSet.Instance.Rempow2(0);
        }

        private FixFormat GetFixFormat(TypeDescriptor type)
        {
            if (!type.IsComplete)
                return null;

            if (type.CILType.Equals(typeof(Signed)))
            {
                int size = (int)type.TypeParams[0];
                return new FixFormat(true, size, 0);
            }
            else if (type.CILType.Equals(typeof(Unsigned)))
            {
                int size = (int)type.TypeParams[0];
                return new FixFormat(false, size, 0);
            }
            else if (type.CILType.Equals(typeof(SFix)) ||
                type.CILType.Equals(typeof(UFix)))
            {
                return (FixFormat)type.TypeParams[0];
            }
            else
            {
                return null;
            }
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            FixPMod1 fpmod1 = fu as FixPMod1;
            if (fpmod1 == null)
                yield break;
            if (instr.Name != InstructionCodes.Mod2 &&
                instr.Name != InstructionCodes.Rempow2)
                yield break;

            if (instr.Name == InstructionCodes.Rempow2)
            {
                int n = (int)instr.Operand;
                if (n != 0)
                    yield break;
            }

            FixFormat infmt = GetFixFormat(operandTypes[0]);
            FixFormat outfmt = GetFixFormat(resultTypes[0]);
            if (infmt == null || outfmt == null)
                yield break;

            if (infmt.IntWidth < 2 || outfmt.IntWidth < 2)
                yield break;
            if (infmt.FracWidth != outfmt.FracWidth)
                yield break;

            if (infmt.IntWidth != fpmod1.InIntWidth)
                yield break;
            if (infmt.FracWidth != fpmod1.FracWidth)
                yield break;
            if (outfmt.IntWidth != fpmod1.OutIntWidth)
                yield break;

            yield return new FPMod1XILMapping(fpmod1);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (instr.Name != InstructionCodes.Mod2 &&
                instr.Name != InstructionCodes.Rempow2)
                return null;

            if (instr.Name == InstructionCodes.Rempow2 )
            {
                int n = (int)instr.Operand;
                if (n != 0)
                    return null;
            }

            FixFormat infmt = GetFixFormat(operandTypes[0]);
            FixFormat outfmt = GetFixFormat(resultTypes[0]);
            if (infmt == null || outfmt == null)
                return null;

            if (infmt.IntWidth < 2 || outfmt.IntWidth < 2)
                return null;
            if (infmt.FracWidth != outfmt.FracWidth)
                return null;

            FixPMod1 slicer = new FixPMod1(infmt.IntWidth, infmt.FracWidth, outfmt.IntWidth);

            return new FPMod1XILMapping(slicer);
        }
    }
}

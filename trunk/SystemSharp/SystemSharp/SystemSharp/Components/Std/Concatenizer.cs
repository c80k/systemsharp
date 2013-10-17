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
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// Transaction site interface of <c>Concatenizer</c>
    /// </summary>
    public interface IConcatTransactor:
        ITransactionSite
    {
        /// <summary>
        /// Returns a transaction for bit-vector concatenation.
        /// </summary>
        /// <param name="ops">Signal sources providing vectors to concat.
        /// Concatenation is done in "downto" sense, i.e. first element constitutes the highest portion.</param>
        /// <param name="r">signal sink receiving concatenated signal</param>
        IEnumerable<TAVerb> Concat(ISignalSource<StdLogicVector>[] ops, ISignalSink<StdLogicVector> r);
    }

    /// <summary>
    /// A synthesizable implementation of a bit-vector concatenating unit. It is intended to be used during high-level synthesis
    /// for mapping concat instructions.
    /// </summary>
    /// <remarks>
    /// Seen from hardware perspective, bit-vector concatenation is nothing more than joining multiple wires to the same hardware.
    /// Indeed, the concatenizer does not perform any "real" work and therefore does not require any logic. It just performs a formal
    /// reorganization of its input signals.
    /// </remarks>
    [DeclareXILMapper(typeof(ConcatenizerXILMapper))]
    public class Concatenizer: Component
    {
        private class Transactor :
            DefaultTransactionSite,
            IConcatTransactor
        {
            private Concatenizer _host;
            private bool _established;

            public Transactor(Concatenizer host) :
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                IProcess[] actions = new IProcess[_host.NumWords];
                for (int i = 0; i < _host.NumWords; i++)
                    actions[i] = _host.Ops[i].Stick(StdLogicVector.DCs(_host.WordWidth));
                yield return Verb(ETVMode.Locked, actions);
            }

            public IEnumerable<TAVerb> Concat(ISignalSource<StdLogicVector>[] ops, ISignalSink<StdLogicVector> r)
            {
                IProcess[] actions = new IProcess[_host.NumWords + 1];
                for (int i = 0; i < _host.NumWords; i++)
                    actions[i] = _host.Ops[i].Drive(ops[i]);
                actions[_host.NumWords] = r.Comb.Connect(_host.R.Dual.AsSignalSource());
                yield return Verb(ETVMode.Locked, actions);
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_established)
                    return;

                var cc = _host;
                cc.Ops = (XIn<StdLogicVector[], InOut<StdLogicVector>>)binder.GetSignal(EPortUsage.Operand, "Ops", null,
                    Enumerable.Repeat(StdLogicVector._0s(cc.WordWidth), cc.NumWords).ToArray());
                cc.R = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "R", null, StdLogicVector._0s(cc.OutputWidth));

                _established = true;
            }
        }

        /// <summary>
        /// Input signals to be concatenated. Sense is "downto", i.e. first signal constitutes the highest portion.
        /// A current restriction is that all signals must be of same width.
        /// </summary>
        public XIn<StdLogicVector[], InOut<StdLogicVector>> Ops { private get; set; }

        /// <summary>
        /// Concatenated output signal
        /// </summary>
        public Out<StdLogicVector> R { private get; set; }

        /// <summary>
        /// Number of input signals
        /// </summary>
        [PerformanceRelevant]
        public int NumWords { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Width per input signal
        /// </summary>
        [PerformanceRelevant]
        public int WordWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Width of concatenated output signal
        /// </summary>
        public int OutputWidth
        {
            [StaticEvaluation] get { return NumWords * WordWidth; }
        }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public IConcatTransactor TASite { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="numWords">number of input signals</param>
        /// <param name="wordWidth">width per input signal</param>
        public Concatenizer(int numWords, int wordWidth)
        {
            NumWords = numWords;
            WordWidth = wordWidth;
            TASite = new Transactor(this);
        }

        private void Processing()
        {
            StdLogicVector result = StdLogicVector._0s(OutputWidth);
            for (int i = 0; i < NumWords; i++)
                result[WordWidth * (i+1) - 1, WordWidth*i] = Ops[i].Cur;
            R.Next = result;
        }

        protected override void Initialize()
        {
            AddProcess(Processing, Ops);
        }
    }

    /// <summary>
    /// Implements a service for mapping concatenation-kind XIL instructions to the <c>Concatenizer</c>.
    /// </summary>
    class ConcatenizerXILMapper : IXILMapper
    {
        private class ConcatXILMapping : DefaultXILMapping
        {
            private Concatenizer _host;

            public ConcatXILMapping(Concatenizer host) :
                base(host.TASite, EMappingKind.LightweightResource)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _host.TASite.Concat(operands, results[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                ISignalSource<StdLogicVector>[] ops = Enumerable.Repeat(
                    SignalSource.Create(StdLogicVector._0s(_host.WordWidth)),
                    _host.NumWords).ToArray();
                return _host.TASite.Concat(ops, SignalSink.Nil<StdLogicVector>());
            }

            public override string Description
            {
                get { return _host.NumWords + " x " + _host.WordWidth + " bit concat"; }
            }
        }

        /// <summary>
        /// Returns concat
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Concat();
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            Concatenizer cc = fu as Concatenizer;
            if (cc == null)
                yield break;

            if (instr.Name != InstructionCodes.Concat)
                yield break;

            if (!operandTypes[0].CILType.Equals(typeof(StdLogicVector)) ||
                !resultTypes[0].CILType.Equals(typeof(StdLogicVector)))
                yield break;

            if (!operandTypes.All(t => t.Equals(operandTypes[0])))
                yield break;

            int wordWidth = (int)operandTypes[0].TypeParams[0];
            if (cc.WordWidth != wordWidth ||
                cc.NumWords != operandTypes.Length)
                yield break;

            yield return new ConcatXILMapping(cc);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (instr.Name != InstructionCodes.Concat)
                return null;

            if (!operandTypes[0].CILType.Equals(typeof(StdLogicVector)) ||
                !resultTypes[0].CILType.Equals(typeof(StdLogicVector)))
                return null;

            if (!operandTypes.All(t => t.Equals(operandTypes[0])))
                return null;

            int wordWidth = (int)operandTypes[0].TypeParams[0];
            Concatenizer cc = new Concatenizer(operandTypes.Length, wordWidth);

            return new ConcatXILMapping(cc);
        }

        public void Realize()
        {
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public ConcatenizerXILMapper()
        {
        }
    }
}

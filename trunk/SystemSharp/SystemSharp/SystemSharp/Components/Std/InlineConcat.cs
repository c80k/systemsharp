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
using SystemSharp.Collections;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Std
{
    public class InlineConcatMapper: IXILMapper
    {
        private class InlineConcatMapperTransactionSite : DefaultTransactionSite
        {
            private class ConcatProcessBuilder : AlgorithmTemplate
            {
                private InlineConcatMapperTransactionSite _taSite;

                public ConcatProcessBuilder(InlineConcatMapperTransactionSite taSite)
                {
                    _taSite = taSite;
                }

                protected override void DeclareAlgorithm()
                {
                    var inSrs = _taSite._inSignals.Select(s => SignalRef.Create(s.Descriptor, SignalRef.EReferencedProperty.Cur));
                    var inLrs = inSrs.Select(s => (Expression)new LiteralReference(s));
                    var concat = inLrs.Aggregate((e1, e2) => Expression.Concat(e1, e2));
                    var outSr = SignalRef.Create(_taSite._outSignal.Descriptor, SignalRef.EReferencedProperty.Next);
                    Store(outSr, concat);
                }
            }

            private int[] _argWidths;
            private SLVSignal[] _inSignals;
            private SLVSignal _outSignal;
            private bool _realized;

            public InlineConcatMapperTransactionSite(InlineConcatMapper mapper, Component host, int[] argWidths) :
                base(host)
            {
                _argWidths = argWidths;
            }

            public int[] ArgWidths
            {
                get { return _argWidths; }
            }

            public IEnumerable<TAVerb> Concat(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector> result)
            {
                var inps = _inSignals.Select((s, i) => s.Drive(operands[i]));
                var outps = Enumerable.Repeat(result.Comb.Connect(_outSignal.AsSignalSource<StdLogicVector>()), 1);
                yield return Verb(ETVMode.Locked, inps.Concat(outps).ToArray());
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                var inps = _inSignals.Select(s => s.Stick(StdLogicVector.DCs(s.InitialValue.Size)));
                yield return Verb(ETVMode.Locked, inps.ToArray());
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_realized)
                    return;

                _inSignals = _argWidths.Select((w, i) => 
                    (SLVSignal)binder.GetSignal(EPortUsage.Default, "concat" + i, null,
                    StdLogicVector.Us(w))).ToArray();
                int total = _argWidths.Sum();
                _outSignal = (SLVSignal)binder.GetSignal(EPortUsage.Default, "concout", null, StdLogicVector.Us(total));
                var pb = new ConcatProcessBuilder(this);
                var alg = pb.GetAlgorithm();
                alg.Name = "concatenize";
                binder.CreateProcess(Process.EProcessKind.Triggered, alg, _inSignals.Select(s => s.Descriptor).ToArray());
                _realized = true;
            }
        }

        private class KeyClass
        {
            private int[] _widths;

            public KeyClass(int[] widths)
            {
                _widths = widths;
            }

            public override bool Equals(object obj)
            {
                var other = obj as KeyClass;
                if (other == null)
                    return false;
                return _widths.SequenceEqual(other._widths);
            }

            public override int GetHashCode()
            {
                return _widths.GetSequenceHashCode();
            }
        }

        private class ConcatMapping : IXILMapping
        {
            private InlineConcatMapperTransactionSite _taSite;

            public ConcatMapping(InlineConcatMapperTransactionSite taSite)
            {
                _taSite = taSite;
            }

            public ITransactionSite TASite
            {
                get { return _taSite; }
            }

            public EMappingKind ResourceKind
            {
                get { return EMappingKind.LightweightResource; }
            }

            public int InitiationInterval
            {
                get { return 1; }
            }

            public int Latency
            {
                get { return 1; }
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _taSite.Concat(operands, results[0]);
            }

            public string Description
            {
                get { return "(" + string.Join("/", _taSite.ArgWidths) + ") bit concat"; }
            }
        }

        public InlineConcatMapper()
        {
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Concat();
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            if (instr.Name != InstructionCodes.Concat)
                yield break;

            int[] inWidths = operandTypes.Select(t => Marshal.SerializeForHW(t.GetSampleInstance()).Size).ToArray();
            var key = new KeyClass(inWidths);
            var taCM = taSite as InlineConcatMapperTransactionSite;
            if (taCM == null)
                yield break;
            if (!taCM.ArgWidths.SequenceEqual(inWidths))
                yield break;
            yield return new ConcatMapping(taCM);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            if (instr.Name != InstructionCodes.Concat)
                return null;

            int[] inWidths = operandTypes.Select(t => Marshal.SerializeForHW(t.GetSampleInstance()).Size).ToArray();
            var key = new KeyClass(inWidths);
            InlineConcatMapperTransactionSite taSite = 
                new InlineConcatMapperTransactionSite(this, host, inWidths);

            return new ConcatMapping(taSite);
        }
    }
}

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
    public interface IMUX2TransactionSite: ITransactionSite
    {
        IEnumerable<TAVerb> Select(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b,
            ISignalSource<StdLogicVector> sel, ISignalSink<StdLogicVector> r);
    }

    [DeclareXILMapper(typeof(MUX2Mapper))]
    public class MUX2: FunctionalUnit
    {
        private class MUX2TransactionSite :
            DefaultTransactionSite,
            IMUX2TransactionSite
        {
            private MUX2 _host;

            public MUX2TransactionSite(MUX2 host) :
                base(host)
            {
                _host = host;
            }

            public override void Establish(IAutoBinder binder)
            {
                var mux2 = _host;
                mux2.A = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "A", null, StdLogicVector._0s(mux2.Width));
                mux2.B = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "B", null, StdLogicVector._0s(mux2.Width));
                mux2.Sel = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "Sel", null, StdLogicVector._0s(1));
                mux2.R = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "R", null, StdLogicVector._0s(mux2.Width));
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(ETVMode.Locked, 
                    _host.A.Dual.Stick(StdLogicVector.DCs(_host.Width)),
                    _host.B.Dual.Stick(StdLogicVector.DCs(_host.Width)),
                    _host.Sel.Dual.Stick(StdLogicVector.DCs(1)));
            }

            public IEnumerable<TAVerb> Select(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, 
                ISignalSource<StdLogicVector> sel, ISignalSink<StdLogicVector> r)
            {
                yield return Verb(ETVMode.Locked,
                    _host.A.Dual.Drive(a),
                    _host.B.Dual.Drive(b),
                    _host.Sel.Dual.Drive(sel),
                    r.Comb.Connect(_host.R.Dual.AsSignalSource()));
            }
        }

        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }
        public In<StdLogicVector> Sel { private get; set; }
        public Out<StdLogicVector> R { private get; set; }

        [PerformanceRelevant]
        public int Width { get; private set; }

        public override bool IsEquivalent(Component obj)
        {
            var other = obj as MUX2;
            if (other == null)
                return false;
            return Width == other.Width;
        }

        public override int GetBehaviorHashCode()
        {
            return Width;
        }

        public IMUX2TransactionSite TASite { get; private set; }

        public MUX2(int width)
        {
            Width = width;
            TASite = new MUX2TransactionSite(this);
        }

        private void Process()
        {
            if (Sel.Cur == "0")
                R.Next = A.Cur;
            else
                R.Next = B.Cur;
        }

        protected override void Initialize()
        {
            AddProcess(Process, A, B, Sel);
        }
    }

    public class MUX2Mapper : IXILMapper
    {
        private class MUX2XILMapping : IXILMapping
        {
            IMUX2TransactionSite _site;

            public MUX2XILMapping(IMUX2TransactionSite site)
            {
                _site = site;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _site.Select(operands[1], operands[0], operands[2], results[0]);
            }

            public ITransactionSite TASite
            {
                get { return _site; }
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
                get { return 0; }
            }

            public string Description
            {
                get
                {
                    var mux2 = (MUX2)_site.Host;
                    return mux2.Width + " bit 2-to-1 mux";
                }
            }
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Select();
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            MUX2 mux2 = fu as MUX2;
            if (mux2 == null)
                yield break;

            if (instr.Name != InstructionCodes.Select)
                yield break;

            int width = TypeLowering.Instance.GetWireWidth(operandTypes[1]);
            if (width != mux2.Width)
                yield break;

            yield return new MUX2XILMapping(mux2.TASite);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (instr.Name != InstructionCodes.Select)
                return null;

            int width = TypeLowering.Instance.GetWireWidth(operandTypes[1]);
            MUX2 mux2 = new MUX2(width);
            return new MUX2XILMapping(mux2.TASite);
        }
    }
}

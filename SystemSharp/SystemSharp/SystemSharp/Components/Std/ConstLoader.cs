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
    class ConstLoadingTransactionSite : DefaultTransactionSite
    {
        private StdLogicVector _constValue;
        private bool _createSignal;
        private SLVSignal _constSignal;

        public ConstLoadingTransactionSite(Component host, StdLogicVector constValue, bool createSignal) :
            base(host)
        {
            _constValue = constValue;
            _createSignal = createSignal;
        }

        public override void Establish(IAutoBinder binder)
        {
            if (_createSignal)
            {
                _constSignal = Host.Descriptor
                    .GetSignals()
                    .Where(s => s.HasAttribute<ConstLoadingTransactionSite>() &&
                        s.InitialValue.Equals(_constValue))
                    .Select(s => s.Instance)
                    .Cast<SLVSignal>()
                    .SingleOrDefault();
                if (_constSignal == null)
                {
                    _constSignal = (SLVSignal)binder.GetSignal(EPortUsage.Default, "const_" + _constValue.ToString(), null, _constValue);
                    _constSignal.Descriptor.AddAttribute(this);
                }
            }
        }

        public override IEnumerable<TAVerb> DoNothing()
        {
            yield return Verb(ETVMode.Locked);
        }

        public IEnumerable<TAVerb> LoadConstant(ISignalSink<StdLogicVector> target)
        {
            if (_constSignal != null)
                yield return Verb(ETVMode.Shared, target.Comb.Connect(_constSignal.AsSignalSource<StdLogicVector>()));
            else
                yield return Verb(ETVMode.Shared, target.Comb.Connect(SignalSource.Create(_constValue)));
        }

        public StdLogicVector ConstValue
        {
            get { return _constValue; }
        }
    }

    class ConstLoadingXILMapping :
        IXILMapping
    {
        private ConstLoadingTransactionSite _site;

        public ConstLoadingXILMapping(ConstLoadingTransactionSite site)
        {
            _site = site;
        }

        public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
        {
            return _site.LoadConstant(results[0]);
        }

        public ITransactionSite TASite
        {
            get { return _site; }
        }

        public EMappingKind ResourceKind
        {
            get { return EMappingKind.ExclusiveResource; }
        }

        public int InitiationInterval
        {
            get { return 0; }
        }

        public int Latency
        {
            get { return 0; }
        }

        public string Description
        {
            get { return "constant loader: " + _site.ConstValue.ToString(); }
        }
    }

    public class ConstLoadingXILMapper : IXILMapper
    {
        public bool CreateSignalsForConstants { get; set; }

        #region IXILMapper Member

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.LdConst(null);
            yield return DefaultInstructionSet.Instance.Ld0();
        }

        private bool TryGetConstSLV(XILInstr instr, TypeDescriptor rtype, out StdLogicVector constSLV)
        {
            switch (instr.Name)
            {
                case InstructionCodes.LdConst:
                    {
                        object constValue = instr.Operand;
                        constSLV = StdLogicVector.Serialize(constValue);
                    }
                    break;

                case InstructionCodes.Ld0:
                    {
                        object sample = rtype.GetSampleInstance();
                        StdLogicVector slv = StdLogicVector.Serialize(sample);
                        constSLV = StdLogicVector._0s(slv.Size);
                    }
                    break;

                default:
                    {
                        constSLV = "";
                        return false;
                    }
            }
            return true;
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            StdLogicVector constSLV;
            if (!TryGetConstSLV(instr, resultTypes[0], out constSLV))
                yield break;

            var fu = taSite.Host;
            ConstLoadingTransactionSite clts = new ConstLoadingTransactionSite(fu, constSLV, CreateSignalsForConstants);
            yield return new ConstLoadingXILMapping(clts);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            StdLogicVector constSLV;
            if (!TryGetConstSLV(instr, resultTypes[0], out constSLV))
                return null;

            var clts = new ConstLoadingTransactionSite(host, constSLV, CreateSignalsForConstants);
            return new ConstLoadingXILMapping(clts);
        }

        #endregion
    }

    public class SignalConstLoadingXILMapper : ConstLoadingXILMapper
    {
        public SignalConstLoadingXILMapper() :
            base()
        {
            CreateSignalsForConstants = true;
        }
    }
}

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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Common;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// A service for mapping XIL instructions which perform array accesses to hardware. The arrays are mapped to
    /// memory models. However, the service does not create any separate component instance. Instead, it inserts the 
    /// memory models and the necessary control logic directly into the hosting component.
    /// </summary>
    public class InlineMemoryMapper: IXILMapper
    {
        private class MemoryMapperTransactionSite: DefaultTransactionSite
        {
            private class MemIfBuilder : AlgorithmTemplate
            {
                private MemoryMapperTransactionSite _taSite;

                public MemIfBuilder(MemoryMapperTransactionSite taSite)
                {
                    _taSite = taSite;
                }

                protected override void DeclareAlgorithm()
                {
                    var srClk = SignalRef.Create(_taSite._clk, SignalRef.EReferencedProperty.RisingEdge);
                    var lrClk = new LiteralReference(srClk);
                    var srWrEn = _taSite.NeedWriteAccess ? SignalRef.Create(_taSite._wrEn, SignalRef.EReferencedProperty.Cur) : null;
                    var lrWrEn = _taSite.NeedWriteAccess ? new LiteralReference(srWrEn) : null;
                    var srAddr = SignalRef.Create(_taSite._addr, SignalRef.EReferencedProperty.Cur);
                    var lrAddr = new LiteralReference(srAddr);
                    var srDataIn = _taSite.NeedWriteAccess ? SignalRef.Create(_taSite._dataIn, SignalRef.EReferencedProperty.Cur) : null;
                    var lrDataIn = _taSite.NeedWriteAccess ? new LiteralReference(srDataIn) : null;
                    var srDataOut = SignalRef.Create(_taSite._dataOut, SignalRef.EReferencedProperty.Next);
                    var hi = LiteralReference.CreateConstant(StdLogic._1);
                    var addrUType = TypeDescriptor.GetTypeOf(((StdLogicVector)_taSite._addr.InitialValue).UnsignedValue);
                    var uAddr = IntrinsicFunctions.Cast(lrAddr, typeof(StdLogicVector), addrUType);
                    var iAddr = IntrinsicFunctions.Cast(uAddr, addrUType.CILType, typeof(int));
                    var array = _taSite._array;
                    var lrArray = new LiteralReference(array.ArrayLit);
                    var elemType = array.ElementType;
                    var aref = new ArrayRef(lrArray, elemType, iAddr);
                    var convDataIn = _taSite.NeedWriteAccess ? IntrinsicFunctions.Cast(lrDataIn, typeof(StdLogicVector), elemType) : null;
                    var convAref = IntrinsicFunctions.Cast(aref, elemType.CILType, _taSite._dataOut.ElementType);

                    If(lrClk);
                    {
                        Store(srDataOut, convAref);
                        if (_taSite.NeedWriteAccess)
                        {
                            If(Expression.Equal(lrWrEn, hi));
                            {
                                Store(aref, convDataIn);
                            }
                            EndIf();
                        }
                    }
                    EndIf();
                }
            }

            private InlineMemoryMapper _mapper;
            private FixedArrayRef _array;
            private bool _realized;

            int _addrBits;
            int _dataBits;
            
            private SignalDescriptor _clk;
            private SignalDescriptor _wrEn;
            private SignalDescriptor _addr;
            private SignalDescriptor _dataIn;
            private SignalDescriptor _dataOut;

            private SLSignal _clkI;
            private SLSignal _wrEnI;
            private SLVSignal _addrI;
            private SLVSignal _dataInI;
            private SLVSignal _dataOutI;

            public MemoryMapperTransactionSite(InlineMemoryMapper mapper, Component host, FixedArrayRef array):
                base(host)
            {
                _mapper = mapper;
                _array = array;

                // work-around, since concept is currently not working: It is not possible to decide "on-the-fly" whether
                // write access is required or not, since transaction site needs to be established immediately.
                IndicateWriteAccess(true);
            }

            public FixedArrayRef Array { get { return _array; } }

            private bool _needWriteAccess;
            public bool NeedWriteAccess 
            {
                get { return _needWriteAccess; }
            }

            public void IndicateWriteAccess(bool needWriteAccess)
            {
                if (needWriteAccess != _needWriteAccess && _realized)
                    throw new InvalidOperationException("Unit is already established, no change of write access indication allowed.");
                _needWriteAccess = needWriteAccess;
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_realized)
                    return;

                _addrBits = MathExt.CeilLog2(_array.ArrayObj.Length);
                _dataBits = Marshal.SerializeForHW(_array.ElementType.GetSampleInstance()).Size;

                _clkI = (SLSignal)binder.GetSignal(EPortUsage.Clock, "Clk", null, null);
                _dataOutI = (SLVSignal)binder.GetSignal(EPortUsage.Default, "memIf_dataOut", null, StdLogicVector._0s(_dataBits));
                _addrI = (SLVSignal)binder.GetSignal(EPortUsage.Default, "memIf_addr", null, StdLogicVector._0s(_addrBits));
                _clk = _clkI.Descriptor;
                _addr = _addrI.Descriptor;
                _dataOut = _dataOutI.Descriptor;

                if (NeedWriteAccess)
                {
                    _wrEnI = (SLSignal)binder.GetSignal(EPortUsage.Default, "memIf_wrEn", null, StdLogic._0);
                    _dataInI = (SLVSignal)binder.GetSignal(EPortUsage.Default, "memIf_dataIn", null, StdLogicVector._0s(_dataBits));
                    _wrEn = _wrEnI.Descriptor;
                    _dataIn = _dataInI.Descriptor;
                }

                var memIfBuilder = new MemIfBuilder(this);
                var memIfAlg = memIfBuilder.GetAlgorithm();
                memIfAlg.Name = "MemIf";
                binder.CreateProcess(Process.EProcessKind.Triggered, memIfAlg, _clk);

                _realized = true;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                if (_wrEnI != null)
                {
                    // Memory used as RAM

                    yield return Verb(ETVMode.Locked,
                        _wrEnI.Stick(StdLogic._0),
                        _addrI.Stick(StdLogicVector.DCs(_addrBits)),
                        _dataInI.Stick(StdLogicVector.DCs(_dataBits)));
                }
                else
                {
                    // Memory used as ROM

                    yield return Verb(ETVMode.Locked,
                        _addrI.Stick(StdLogicVector.DCs(_addrBits)));
                }
            }

            public IEnumerable<TAVerb> LdelemFixA(ISignalSource<StdLogicVector> addr, 
                ISignalSink<StdLogicVector> data)
            {
                if (_wrEnI != null)
                {
                    // Memory used as RAM

                    yield return Verb(ETVMode.Locked,
                        _wrEnI.Stick(StdLogic._0),
                        _addrI.Drive(addr),
                        _dataInI.Stick(StdLogicVector._0s(_dataBits)));
                }
                else
                {
                    // Memory used as ROM

                    yield return Verb(ETVMode.Locked, _addrI.Drive(addr));
                }
                yield return Verb(ETVMode.Shared,
                    data.Comb.Connect(_dataOutI.AsSignalSource<StdLogicVector>()));
            }

            public IEnumerable<TAVerb> LdelemFixAFixI(ISignalSink<StdLogicVector> data, long[] indices)
            {
                if (_wrEnI != null)
                {
                    // Memory used as RAM

                    yield return Verb(ETVMode.Locked,
                        _wrEnI.Stick(StdLogic._0),
                        _addrI.Stick(StdLogicVector.FromULong((ulong)indices[0], _addrBits)),
                        _dataInI.Stick(StdLogicVector._0s(_dataBits)));
                }
                else
                {
                    // Memory used as ROM

                    yield return Verb(ETVMode.Locked,
                        _addrI.Stick(StdLogicVector.FromULong((ulong)indices[0], _addrBits)));
                }
                yield return Verb(ETVMode.Shared,
                    data.Comb.Connect(_dataOutI.AsSignalSource<StdLogicVector>()));
            }

            public IEnumerable<TAVerb> StelemFixA(ISignalSource<StdLogicVector> addr,
                ISignalSource<StdLogicVector> data)
            {
                Contract.Requires(NeedWriteAccess);

                yield return Verb(ETVMode.Locked,
                    _wrEnI.Stick(StdLogic._1),
                    _addrI.Drive(addr),
                    _dataInI.Drive(data));
            }

            public IEnumerable<TAVerb> StelemFixAFixI(ISignalSource<StdLogicVector> data, long[] indices)
            {
                Contract.Requires(NeedWriteAccess);

                yield return Verb(ETVMode.Locked,
                    _wrEnI.Stick(StdLogic._1),
                    _addrI.Stick(StdLogicVector.FromULong((ulong)indices[0], _addrBits)),
                    _dataInI.Drive(data));
            }
        }

        private abstract class AbstractXILMapping : IXILMapping
        {
            protected MemoryMapperTransactionSite _taSite;

            public AbstractXILMapping(MemoryMapperTransactionSite taSite)
            {
                _taSite = taSite;
            }

            public ITransactionSite TASite
            {
                get { return _taSite; }
            }

            public EMappingKind ResourceKind
            {
                get { return EMappingKind.ExclusiveResource; }
            }

            public int InitiationInterval
            {
                get { return 1; }
            }

            public int Latency
            {
                get { return 1; }
            }

            public abstract IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results);
            public abstract string Description { get; }
        }

        private class LdelemFixAMapping : AbstractXILMapping
        {
            public LdelemFixAMapping(MemoryMapperTransactionSite taSite):
                base(taSite)
            {
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _taSite.LdelemFixA(operands[0], results[0]);
            }

            public override string Description
            {
                get { return _taSite.Array.ToString() + " array element loader"; }
            }
        }

        private class LdelemFixAFixIMapping : AbstractXILMapping
        {
            private long[] _indices;

            public LdelemFixAFixIMapping(MemoryMapperTransactionSite taSite, long[] indices) :
                base(taSite)
            {
                Contract.Requires(indices != null);
                Contract.Requires(indices.Length == 1);

                _indices = indices;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _taSite.LdelemFixAFixI(results[0], _indices);
            }

            public override string Description
            {
                get { return _taSite.Array.ToString() + " array element loader"; }
            }
        }

        private class StelemFixAMapping : AbstractXILMapping
        {
            public StelemFixAMapping(MemoryMapperTransactionSite taSite) :
                base(taSite)
            {
                taSite.IndicateWriteAccess(true);
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _taSite.StelemFixA(operands[0], operands[1]);
            }

            public override string Description
            {
                get { return _taSite.Array.ToString() + " array element writer"; }
            }
        }

        private class StelemFixAFixIMapping : AbstractXILMapping
        {
            private long[] _indices;

            public StelemFixAFixIMapping(MemoryMapperTransactionSite taSite, long[] indices) :
                base(taSite)
            {
                Contract.Requires(indices != null);
                Contract.Requires(indices.Length == 1);

                _indices = indices;
                taSite.IndicateWriteAccess(true);
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _taSite.StelemFixAFixI(operands[0], _indices);
            }

            public override string Description
            {
                get { return _taSite.Array.ToString() + " array element writer"; }
            }
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public InlineMemoryMapper()
        {
        }

        /// <summary>
        /// Returns ldelemfixa, ldelemfixafixi, stelemfixa, stelemfixafixi
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.LdelemFixA(null);
            yield return DefaultInstructionSet.Instance.LdelemFixAFixI(null);
            yield return DefaultInstructionSet.Instance.StelemFixA(null);
            yield return DefaultInstructionSet.Instance.StelemFixAFixI(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            var far = instr.Operand as FixedArrayRef;
            if (far == null)
                yield break;

            var taMM = taSite as MemoryMapperTransactionSite;
            if (taMM == null)
                yield break;
            if (taMM.Array.ArrayObj != far.ArrayObj)
                yield break;

            switch (instr.Name)
            {
                case InstructionCodes.LdelemFixA:
                    yield return new LdelemFixAMapping(taMM);
                    break;

                case InstructionCodes.LdelemFixAFixI:
                    yield return new LdelemFixAFixIMapping(taMM, far.Indices);
                    break;

                case InstructionCodes.StelemFixA:
                    yield return new StelemFixAMapping(taMM);
                    break;

                case InstructionCodes.StelemFixAFixI:
                    yield return new StelemFixAFixIMapping(taMM, far.Indices);
                    break;

                default:
                    yield break;
            }
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            var far = instr.Operand as FixedArrayRef;
            if (far == null)
                return null;

            var taSite = new MemoryMapperTransactionSite(this, host, far);

            switch (instr.Name)
            {
                case InstructionCodes.LdelemFixA:
                    return new LdelemFixAMapping(taSite);

                case InstructionCodes.LdelemFixAFixI:
                    return new LdelemFixAFixIMapping(taSite, far.Indices);

                case InstructionCodes.StelemFixA:
                    return new StelemFixAMapping(taSite);

                case InstructionCodes.StelemFixAFixI:
                    return new StelemFixAFixIMapping(taSite, far.Indices);

                default:
                    return null;
            }
        }
    }
}

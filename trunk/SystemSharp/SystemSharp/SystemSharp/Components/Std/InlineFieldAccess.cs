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
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Std
{
    public class InlineFieldMapper: IXILMapper
    {
        private class InlineFieldMapperTransactionSite : DefaultTransactionSite
        {
            private class CommonClockProcess
            {
                public SignalDescriptor Clk { get; private set; }
                public IAlgorithmBuilder BodyBuilder { get; private set; }
                public DefaultAlgorithmBuilder FrameBuilder { get; private set; }
                public ProcessDescriptor CCProc { get; set; }

                public CommonClockProcess(SignalDescriptor clk)
                {
                    Contract.Requires<ArgumentNullException>(clk != null);

                    Clk = clk;
                    var frame = new DefaultAlgorithmBuilder();

                    var srClk = SignalRef.Create(Clk, SignalRef.EReferencedProperty.RisingEdge);
                    var lrClk = new LiteralReference(srClk);
                    frame.If(lrClk);
                    BodyBuilder = frame.BeginSubAlgorithm();
                    frame.EndIf();
                    FrameBuilder = frame;
                }

                public void AddAccessor(InlineFieldMapperTransactionSite taSite, bool needRead, bool needWrite)
                {
                    var srWrEn = needWrite ? SignalRef.Create(taSite._wrEn, SignalRef.EReferencedProperty.Cur) : null;
                    var lrWrEn = needWrite ? new LiteralReference(srWrEn) : null;
                    var srDataIn = needWrite ? SignalRef.Create(taSite._dataIn, SignalRef.EReferencedProperty.Cur) : null;
                    var lrDataIn = needWrite ? new LiteralReference(srDataIn) : null;
                    var srDataOut = needRead ? SignalRef.Create(taSite._dataOut, SignalRef.EReferencedProperty.Next) : null;
                    var hi = LiteralReference.CreateConstant(StdLogic._1);
                    var elemType = taSite._literal.Type;
                    var lrVar = new LiteralReference((Literal)taSite._literal);
                    var convDataIn = needWrite ? IntrinsicFunctions.Cast(lrDataIn, typeof(StdLogicVector), elemType) : null;
                    var convVar = needRead ? IntrinsicFunctions.Cast(lrVar, elemType.CILType, taSite._dataOut.ElementType) : null;
                    bool isBool = taSite._literal.Type.CILType.Equals(typeof(bool));
                    var lr1 = LiteralReference.CreateConstant((StdLogicVector)"1");
                    var lr0 = LiteralReference.CreateConstant((StdLogicVector)"0");
                    if (needWrite)
                    {
                        BodyBuilder.If(Expression.Equal(lrWrEn, hi));
                        {
                            if (isBool)
                                BodyBuilder.Store(taSite._literal, Expression.Equal(lrDataIn, lr1));
                            else
                                BodyBuilder.Store(taSite._literal, convDataIn);

                            var diagOut = taSite.Host as ISupportsDiagnosticOutput;
                            if (diagOut != null && diagOut.EnableDiagnostics)
                            {
                                Expression vref = new LiteralReference(taSite.Literal);
                                var fref = taSite.Literal as FieldRef;
                                var field = fref != null ? fref.FieldDesc : null;
                                if (field != null && field.HasAttribute<ActualTypeAttribute>())
                                {
                                    var atype = field.QueryAttribute<ActualTypeAttribute>();
                                    vref = IntrinsicFunctions.Cast(vref, vref.ResultType.CILType, atype.ActualType, true);
                                }
                                BodyBuilder.ReportLine(taSite.Literal.Name + " changed to ", vref);
                            }
                        }
                        BodyBuilder.EndIf();
                    }
                    if (needRead)
                    {
                        if (isBool)
                        {
                            BodyBuilder.If(lrVar);
                            {
                                BodyBuilder.If(lrVar);
                                BodyBuilder.Store(srDataOut, lr1);
                            }
                            BodyBuilder.Else();
                            {
                                BodyBuilder.Store(srDataOut, lr0);
                            }
                            BodyBuilder.EndIf();
                        }
                        else
                        {
                            BodyBuilder.Store(srDataOut, convVar);
                        }
                    }
                }
            }

            private InlineFieldMapper _mapper;
            private IStorableLiteral _literal;
            private bool _realized;

            private int _dataBits;

            private SignalDescriptor _clk;
            private SignalDescriptor _wrEn;
            private SignalDescriptor _dataIn;
            private SignalDescriptor _dataOut;

            private SLSignal _clkI;
            private SLSignal _wrEnI;
            private SLVSignal _dataInI;
            private SLVSignal _dataOutI;

            public InlineFieldMapperTransactionSite(InlineFieldMapper mapper, Component host, IStorableLiteral literal) :
                base(host)
            {
                _mapper = mapper;
                _literal = literal;
            }

            public IStorableLiteral Literal
            {
                get { return _literal; }
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_realized)
                    return;

                object constValue;
                var literal = (Literal)_literal;
                if (literal.IsConst(out constValue))
                {
                    var slv = Marshal.SerializeForHW(constValue);
                    _dataOutI = (SLVSignal)binder.GetSignal(EPortUsage.Default, "constSignal_" + constValue.ToString(), null, slv);
                    _dataOut = _dataOutI.Descriptor;
                }
                else
                {
                    bool needRead = true, needWrite = true;
                    var fref = _literal as FieldRef;
                    if (fref != null)
                    {
                        needRead = fref.FieldDesc.IsReadInCurrentContext(Host.Context);
                        needWrite = fref.FieldDesc.IsWrittenInCurrentContext(Host.Context);
                    }

                    var stlit = (IStorableLiteral)_literal;
                    string name = stlit.Name;
                    var valueSample = _literal.Type.GetSampleInstance();
                    _dataBits = Marshal.SerializeForHW(valueSample).Size;

                    _clkI = (SLSignal)binder.GetSignal(EPortUsage.Clock, null, null, null);
                    _clk = _clkI.Descriptor;
                    if (needWrite)
                    {
                        _wrEnI = (SLSignal)binder.GetSignal(EPortUsage.Default, name + "_wrEn", null, StdLogic._0);
                        _wrEn = _wrEnI.Descriptor;
                        _dataInI = (SLVSignal)binder.GetSignal(EPortUsage.Default, name + "_dataIn", null, StdLogicVector._0s(_dataBits));
                        _dataIn = _dataInI.Descriptor;
                    }
                    if (needRead)
                    {
                        _dataOutI = (SLVSignal)binder.GetSignal(EPortUsage.Default, name + "_dataOut", null, StdLogicVector._0s(_dataBits));
                        _dataOut = _dataOutI.Descriptor;
                    }

                    //var apb = new AccessProcessBuilder(this, needRead, needWrite);
                    //var alg = apb.GetAlgorithm();
                    //alg.Name = name + "_process";
                    //binder.CreateProcess(Process.EProcessKind.Triggered, alg, _clk);
                    CommonClockProcess ccp = null;
                    string algName = name + "_process";
                    if (Host.Descriptor.HasAttribute<CommonClockProcess>())
                    {
                        ccp = Host.Descriptor.QueryAttribute<CommonClockProcess>();
                        Host.Descriptor.RemoveChild(ccp.CCProc);
                    }
                    else
                    {
                        ccp = new CommonClockProcess(_clk);
                        Host.Descriptor.AddAttribute(ccp);
                    }
                    ccp.AddAccessor(this, needRead, needWrite);
                    var alg = ccp.FrameBuilder.Complete();
                    alg.Name = algName;
                    ccp.CCProc = binder.CreateProcess(Process.EProcessKind.Triggered, alg, _clk);
                }

                _realized = true;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                if (_wrEnI == null)
                {
                    yield return Verb(ETVMode.Locked);
                }
                else
                {
                    yield return Verb(ETVMode.Locked,
                        _wrEnI.Stick(StdLogic._0),
                        _dataInI.Stick(StdLogicVector.DCs(_dataBits)));
                }
            }

            public IEnumerable<TAVerb> Read(ISignalSink<StdLogicVector> dest)
            {
                var fref = _literal as FieldRef;
                if (fref != null)
                {
                    if (!fref.FieldDesc.IsReadInCurrentContext(Host.Context))
                        throw new InvalidOperationException("Field is marked as not being read");
                }

                yield return Verb(ETVMode.Shared,
                    dest.Comb.Connect(_dataOutI.AsSignalSource<StdLogicVector>()));
            }

            public IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> data)
            {
                var fref = _literal as FieldRef;
                if (fref != null)
                {
                    if (!fref.FieldDesc.IsWrittenInCurrentContext(Host.Context))
                        throw new InvalidOperationException("Field is marked as not being written");
                }

                yield return Verb(ETVMode.Locked,
                    _wrEnI.Stick(StdLogic._1),
                    _dataInI.Drive(data));
            }
        }

        private class ReadXILMapping : IXILMapping
        {
            private InlineFieldMapperTransactionSite _taSite;

            public ReadXILMapping(InlineFieldMapperTransactionSite taSite)
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
                get { return 0; }
            }

            public int Latency
            {
                get { return 0; }
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _taSite.Read(results[0]);
            }

            public string Description
            {
                get { return _taSite.Literal.Name + " variable reader"; }
            }
        }

        private class WriteXILMapping : IXILMapping
        {
            private InlineFieldMapperTransactionSite _taSite;

            public WriteXILMapping(InlineFieldMapperTransactionSite taSite)
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

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _taSite.Write(operands[0]);
            }

            public string Description
            {
                get { return _taSite.Literal.Name + " variable writer"; }
            }
        }

        public InlineFieldMapper()
        {
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.LoadVar(null);
            yield return DefaultInstructionSet.Instance.StoreVar(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            var v = instr.Operand as FieldRef;
            if (v == null)
                yield break;

            var taLV = taSite as InlineFieldMapperTransactionSite;
            if (taLV == null)
                yield break;

            if (!taLV.Literal.Equals(v))
                yield break;

            switch (instr.Name)
            {
                case InstructionCodes.LoadVar:
                    yield return new ReadXILMapping(taLV);
                    break;

                case InstructionCodes.StoreVar:
                    yield return new WriteXILMapping(taLV);
                    break;

                default:
                    yield break;
            }
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            var v = instr.Operand as FieldRef;
            if (v == null)
                return null;

            var taSite = new InlineFieldMapperTransactionSite(this, host, v);

            switch (instr.Name)
            {
                case InstructionCodes.LoadVar:
                    return new ReadXILMapping(taSite);

                case InstructionCodes.StoreVar:
                    return new WriteXILMapping(taSite);

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// An instance of this class attached to a FieldDescriptor (as an attribute, using AddAttribute) indicates its
    /// actual type which may be different from its formal field type, e.g. if the field is represented as a
    /// StdLogicVector. In that case, the actual type gives a hint to the correct interpretation of the
    /// raw binary data, e.g. for meaningful diagnostic outputs.
    /// </summary>
    public class ActualTypeAttribute : Attribute
    {
        public TypeDescriptor ActualType { get; private set; }

        public ActualTypeAttribute(TypeDescriptor originalType)
        {
            ActualType = originalType;
        }
    }
}

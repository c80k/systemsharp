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
using SystemSharp.Collections;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Std
{
    class DirectPortReadTransactionSite : ITransactionSite
    {
        private class ConvProcessBuilder : AlgorithmTemplate
        {
            private DirectPortReadTransactionSite _taSite;

            public ConvProcessBuilder(DirectPortReadTransactionSite taSite)
            {
                _taSite = taSite;
            }

            protected override void DeclareAlgorithm()
            {
                var srCur = SignalRef.Create(_taSite._portSignal, SignalRef.EReferencedProperty.Cur);
                var lrCur = new LiteralReference(srCur);
                if (_taSite._portSignal.ElementType.CILType.Equals(typeof(StdLogic)))
                {
                    var index = new IndexSpec((DimSpec)0);
                    var srSLV = new SignalRef(
                        _taSite._slvSignal,
                        SignalRef.EReferencedProperty.Next,
                        index.AsExpressions(),
                        index, true);
                    Store(srSLV, lrCur);
                }
                else
                {
                    var convFn = IntrinsicFunctions.Cast(lrCur,
                        _taSite._portSignal.ElementType.CILType,
                        _taSite._slvSignal.ElementType);
                    var srSLV = SignalRef.Create(_taSite._slvSignal, SignalRef.EReferencedProperty.Next);
                    Store(srSLV, convFn);
                }
            }
        }

        private Component _host;
        private ISignalOrPortDescriptor _port;
        private SignalDescriptor _portSignal;
        private SignalDescriptor _slvSignal;
        private bool _established;

        public DirectPortReadTransactionSite(Component host, ISignalOrPortDescriptor port)
        {
            _host = host;
            _port = port;

            _portSignal = port as SignalDescriptor;
            if (_portSignal == null)
                _portSignal = (SignalDescriptor)((IPortDescriptor)port).BoundSignal;            
        }

        public Component Host
        {
            get { return _host; }
        }

        public string Name
        {
            get { return _port.Name + "_reader"; }
        }

        public IEnumerable<TAVerb> DoNothing()
        {
            yield return new TAVerb(this, ETVMode.Locked, () => { });
        }

        public ISignalOrPortDescriptor Port
        {
            get { return _port; }
        }

        public SLVSignal SLVSignal
        {
            get { return (SLVSignal)_slvSignal.Instance; }
        }

        public void Establish(IAutoBinder binder)
        {
            if (_established)
                return;

            if (!_portSignal.ElementType.CILType.Equals(typeof(StdLogicVector)))
            {
                var signalInst = binder.GetSignal(EPortUsage.Default, _port.Name + "_slv", null,
                    Marshal.SerializeForHW(_portSignal.InitialValue));
                _slvSignal = signalInst.Descriptor;
                var templ = new ConvProcessBuilder(this);
                var alg = templ.GetAlgorithm();
                binder.CreateProcess(Process.EProcessKind.Triggered, alg, _portSignal);
            }
            else
            {
                _slvSignal = _portSignal;
            }

            _established = true;
        }
    }

    class DirectPortReadXILMapping : IXILMapping
    {
        private Component _host;
        private DirectPortReadTransactionSite _taSite;

        public DirectPortReadXILMapping(Component host, DirectPortReadTransactionSite taSite)
        {
            _host = host;
            _taSite = taSite;
        }

        public EMappingKind ResourceKind
        {
            get { return EMappingKind.ExclusiveResource; }
        }

        public ITransactionSite TASite
        {
            get { return _taSite; }
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
            yield return new TAVerb(_taSite, 
                ETVMode.Shared, () => { }, 
                results[0].Comb.Connect(_taSite.SLVSignal.AsSignalSource<StdLogicVector>()));
        }

        public string Description
        {
            get { return _taSite.Port.Name + " port reader"; }
        }
    }

    public class PortReaderXILMapper: IXILMapper
    {
        public PortReaderXILMapper()
        {
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.ReadPort(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            if (!instr.Name.Equals(InstructionCodes.RdPort))
                yield break;

            var ts = taSite as DirectPortReadTransactionSite;
            if (ts == null)
                yield break;

            //if (ts.Host != _host)
            //    yield break;

            var tgPort = (ISignalOrPortDescriptor)instr.Operand;
            if (!ts.Port.Equals(tgPort))
                yield break;

            yield return new DirectPortReadXILMapping(ts.Host, ts);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (!instr.Name.Equals(InstructionCodes.RdPort))
                return null;

            var tgPort = (ISignalOrPortDescriptor)instr.Operand;
            if (!tgPort.Owner.Equals(host.Descriptor))
                return null;

            var ts = new DirectPortReadTransactionSite(host, tgPort);
            return TryMap(ts, instr, operandTypes, resultTypes).SingleOrDefault();
        }
    }

    public interface IPortWriterSite : ITransactionSite
    {
        IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> data);
    }

    interface IPortWriter
    {
        int DataWidth { get; }
        ISignalOrPortDescriptor Port { get; }
        ISignalOrPortDescriptor SignalDesc { get; }
        IPortWriterSite TASite { get; }
        void Bind(SignalBase clk, SignalBase enIn, SignalBase din, SignalBase dout);
    }

    public abstract class PortWriter<T> : 
        FunctionalUnit,
        IPortWriter
    {
        public In<StdLogic> Clk { get; set; }
        public In<StdLogic> EnIn { get; set; }
        public In<StdLogicVector> DIn { get; set; }
        public Out<T> DOut { get; set; }

        [PerformanceRelevant]
        public int DataWidth { get; private set; }

        private PortWriterSite<T> _TASite;
        public IPortWriterSite TASite
        {
            get { return _TASite; }
        }

        public ISignalOrPortDescriptor Port { get; internal set; }

        public ISignalOrPortDescriptor SignalDesc
        {
            get { return (ISignalOrPortDescriptor)((IDescriptive)DOut).Descriptor; }
        }

        public PortWriter(int dataWidth)
        {
            DataWidth = dataWidth;
            _TASite = new PortWriterSite<T>(this);
        }

        protected abstract void Process();

        protected override void Initialize()
        {
            AddProcess(Process, Clk, EnIn);
        }

        public void Bind(SignalBase clk, SignalBase enIn, SignalBase din, SignalBase dout)
        {
            Clk = (In<StdLogic>)clk;
            EnIn = (In<StdLogic>)enIn;
            DIn = (In<StdLogicVector>)din;
            DOut = (Out<T>)dout;
        }
    }

    public class IntPortWriter :
        PortWriter<int>
    {
        public IntPortWriter() :
            base(32)
        {
        }

        protected override void Process()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                DOut.Next = DIn.Cur.IntValue;
            }
        }
    }

    public class FloatPortWriter :
        PortWriter<float>
    {
        public FloatPortWriter() :
            base(32)
        {
        }

        protected override void Process()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                DOut.Next = DIn.Cur.ToFloat();
            }
        }
    }

    public class DoublePortWriter :
        PortWriter<double>
    {
        public DoublePortWriter() :
            base(64)
        {
        }

        protected override void Process()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                DOut.Next = DIn.Cur.ToDouble();
            }
        }
    }

    public class SLVPortWriter :
        PortWriter<StdLogicVector>
    {
        public SLVPortWriter(int dataWidth) :
            base(dataWidth)
        {
        }

        protected override void Process()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                DOut.Next = DIn.Cur;
            }
        }
    }

    public class SLPortWriter :
        PortWriter<StdLogic>
    {
        public SLPortWriter() :
            base(1)
        {
        }

        protected override void Process()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                DOut.Next = DIn.Cur[0];
            }
        }
    }

    class PortWriterSite<T> : 
        DefaultTransactionSite, 
        IPortWriterSite
    {
        private PortWriter<T> _host;
        private bool _established;

        public PortWriterSite(PortWriter<T> host) :
            base(host)
        {
            _host = host;
        }

        public override IEnumerable<TAVerb> DoNothing()
        {
            yield return Verb(ETVMode.Locked,
                _host.EnIn.Dual.Stick<StdLogic>('0'),
                _host.DIn.Dual.Stick(StdLogicVector.DCs(_host.DataWidth)));
        }

        public override void Establish(IAutoBinder binder)
        {
            if (_established)
                return;

            var boundSignal = _host.Port as SignalDescriptor;
            if (boundSignal == null)
                boundSignal = (SignalDescriptor)((IPortDescriptor)_host.Port).BoundSignal;
            SignalBase portSignal = boundSignal.Instance;

            _host.Bind(
                binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0'),
                binder.GetSignal<StdLogic>(EPortUsage.Default, _host.Port.Name + "_En", null, '0'),
                binder.GetSignal<StdLogicVector>(EPortUsage.Operand, _host.Port.Name + "_In", null, StdLogicVector._0s(_host.DataWidth)),
                portSignal);

            _established = true;
        }

        #region IPortWriterSite Member

        public IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> data)
        {
            yield return Verb(ETVMode.Locked,
                _host.EnIn.Dual.Stick<StdLogic>('1'),
                _host.DIn.Dual.Drive(data));
        }

        #endregion
    }

    class PortWriterXILMapping : DefaultXILMapping
    {
        private IPortWriterSite _site;
        private int _dataWidth;

        public PortWriterXILMapping(IPortWriterSite site):
            base(site, EMappingKind.ExclusiveResource)
        {
            _site = site;
            _dataWidth = ((IPortWriter)site.Host).DataWidth;
        }

        public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
        {
            return _site.Write(operands[0]);
        }

        protected override IEnumerable<TAVerb> RealizeDefault()
        {
            return _site.Write(SignalSource.Create(StdLogicVector._0s(_dataWidth)));
        }

        public override int Latency
        {
            get { return 0; }
        }

        public override string Description
        {
            get
            {
                var pwr = (IPortWriter)_site.Host;
                return pwr.Port.Name + " port writer";
            }
        }
    }

    class InlinePortWriteSite :
        DefaultTransactionSite,
        IPortWriterSite
    {
        private class ConvProcessBuilder : AlgorithmTemplate
        {
            private InlinePortWriteSite _taSite;

            public ConvProcessBuilder(InlinePortWriteSite taSite)
            {
                _taSite = taSite;
            }

            protected override void DeclareAlgorithm()
            {
                var srClk = SignalRef.Create(_taSite._clk, SignalRef.EReferencedProperty.RisingEdge);
                var lrClk = new LiteralReference(srClk);
                var srEn = SignalRef.Create(_taSite._en, SignalRef.EReferencedProperty.Cur);
                var lrEn = new LiteralReference(srEn);
                var lr1 = LiteralReference.CreateConstant(StdLogic._1);
                var cond = lrClk & (Expression.Equal(lrEn, lr1));
                var srSLV = SignalRef.Create(_taSite._slvSignal, SignalRef.EReferencedProperty.Cur);
                var lrSLV = new LiteralReference(srSLV);
                Expression conv;
                if (_taSite._port.InitialValue.GetType().Equals(typeof(StdLogicVector)))
                {
                    conv = lrSLV;
                }
                else
                {
                    conv = IntrinsicFunctions.Cast(
                        lrSLV,
                        typeof(StdLogicVector),
                        TypeDescriptor.GetTypeOf(_taSite._port.InitialValue));
                }
                var srNext = SignalRef.Create(_taSite._port, SignalRef.EReferencedProperty.Next);
                
                If(cond);
                {
                    Store(srNext, conv);
                }
                EndIf();
            }
        }

        private Component _host;
        private ISignalOrPortDescriptor _port;
        private SignalDescriptor _clk;
        private SignalDescriptor _en;
        private SignalDescriptor _slvSignal;
        private bool _established;

        private SLSignal _enI;
        private SLVSignal _slvSignalI;

        private int _dataWidth;

        public InlinePortWriteSite(Component host, ISignalOrPortDescriptor port):
            base(host)
        {
            _host = host;
            _port = port;
            _dataWidth = Marshal.SerializeForHW(port.InitialValue).Size;
        }

        public ISignalOrPortDescriptor TargetPort
        {
            get { return _port; }
        }

        public override IEnumerable<TAVerb> DoNothing()
        {
            yield return Verb(ETVMode.Locked,
                _enI.Stick<StdLogic>('0'),
                _slvSignalI.Stick<StdLogicVector>(StdLogicVector.DCs(_dataWidth)));
        }

        public IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> data)
        {
            yield return Verb(ETVMode.Locked,
                _enI.Stick<StdLogic>('1'),
                _slvSignalI.AsCombSink().Comb.Connect(data));
        }

        public override void Establish(IAutoBinder binder)
        {
            if (_established)
                return;

            _clk = binder.GetSignal(EPortUsage.Clock, null, null, null).Descriptor;
            _enI = (SLSignal)binder.GetSignal(EPortUsage.Default, _port.Name + "_en", null, StdLogic._1);
            _en = _enI.Descriptor;
            _slvSignalI = (SLVSignal)binder.GetSignal(EPortUsage.Default, _port.Name + "_in", null, StdLogicVector._0s(_dataWidth));
            _slvSignal = _slvSignalI.Descriptor;
            var templ = new ConvProcessBuilder(this);
            var alg = templ.GetAlgorithm();
            binder.CreateProcess(Process.EProcessKind.Triggered, alg, _clk, _en);

            _established = true;
        }
    }

    class InlinePortWriterXILMapping : IXILMapping
    {
        private InlinePortWriteSite _taSite;

        public InlinePortWriterXILMapping(InlinePortWriteSite taSite)
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
            get { return _taSite.TargetPort.Name + " port writer"; }
        }
    }

    public class PortWriterXILMapper : IXILMapper
    {
        public PortWriterXILMapper()
        {
        }

        #region IXILMapper Member

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.WritePort(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            if (!instr.Name.Equals(InstructionCodes.WrPort))
                yield break;

            var pwSite = taSite as InlinePortWriteSite;
            if (pwSite == null)
                yield break;

            var tgPort = (ISignalOrPortDescriptor)instr.Operand;
            if (!tgPort.Equals(pwSite.TargetPort))
                yield break;

            yield return new InlinePortWriterXILMapping(pwSite);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (!instr.Name.Equals(InstructionCodes.WrPort))
                return null;

            var tgPort = (ISignalOrPortDescriptor)instr.Operand;

            InlinePortWriteSite taSite;
            taSite = new InlinePortWriteSite(host, tgPort);
            return new InlinePortWriterXILMapping(taSite);
        }

        #endregion
    }
}

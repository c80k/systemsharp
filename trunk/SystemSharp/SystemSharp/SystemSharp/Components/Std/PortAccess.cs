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
    /// <summary>
    /// Transaction site for implementing port-read accesses.
    /// </summary>
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

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="host">hosting component</param>
        /// <param name="port">port descriptor</param>
        public DirectPortReadTransactionSite(Component host, ISignalOrPortDescriptor port)
        {
            _host = host;
            _port = port;

            _portSignal = port as SignalDescriptor;
            if (_portSignal == null)
                _portSignal = (SignalDescriptor)((IPortDescriptor)port).BoundSignal;            
        }

        /// <summary>
        /// Hosting component
        /// </summary>
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

        /// <summary>
        /// Port descriptor
        /// </summary>
        public ISignalOrPortDescriptor Port
        {
            get { return _port; }
        }

        /// <summary>
        /// Signal which is bind to the port
        /// </summary>
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

    /// <summary>
    /// A mapping of the "rdport" XIL instruction to a <c>DirectPortReadTransactionSite</c>
    /// </summary>
    class DirectPortReadXILMapping : IXILMapping
    {
        private Component _host;
        private DirectPortReadTransactionSite _taSite;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="host">hosting component</param>
        /// <param name="taSite">implementing transaction site</param>
        public DirectPortReadXILMapping(Component host, DirectPortReadTransactionSite taSite)
        {
            _host = host;
            _taSite = taSite;
        }

        /// <summary>
        /// Returns <c>EMappingKind.ExclusiveResource</c>, since a port is a unique resource.
        /// </summary>
        public EMappingKind ResourceKind
        {
            get { return EMappingKind.ExclusiveResource; }
        }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public ITransactionSite TASite
        {
            get { return _taSite; }
        }

        /// <summary>
        /// Always 0
        /// </summary>
        public int InitiationInterval
        {
            get { return 0; }
        }

        /// <summary>
        /// Always 0
        /// </summary>
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

    /// <summary>
    /// A service for mapping the "rdport" XIL instruction to hardware.
    /// </summary>
    public class PortReaderXILMapper: IXILMapper
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public PortReaderXILMapper()
        {
        }

        /// <summary>
        /// Returns rdport
        /// </summary>
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

    /// <summary>
    /// Transaction site interface for mapping port write accesses
    /// </summary>
    public interface IPortWriterSite : ITransactionSite
    {
        /// <summary>
        /// Returns a transaction which performs a write access on the port this instance was created for.
        /// </summary>
        /// <param name="data">source of data to write</param>
        IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> data);
    }

    /// <summary>
    /// Interface definition for a component which writes to a port
    /// </summary>
    interface IPortWriter
    {
        /// <summary>
        /// Bit-width of port
        /// </summary>
        int DataWidth { get; }

        /// <summary>
        /// The port being written
        /// </summary>
        ISignalOrPortDescriptor Port { get; }

        /// <summary>
        /// Descriptor of signal to which the port is bound
        /// </summary>
        ISignalOrPortDescriptor SignalDesc { get; }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        IPortWriterSite TASite { get; }

        /// <summary>
        /// Associates the component with the necessary control signals.
        /// </summary>
        /// <param name="clk">clock signal</param>
        /// <param name="enIn">write enable signal</param>
        /// <param name="din">data input signal</param>
        /// <param name="dout">data read-back signal</param>
        void Bind(SignalBase clk, SignalBase enIn, SignalBase din, SignalBase dout);
    }

    /// <summary>
    /// Abstract base implementation of a component which writes to a port.
    /// </summary>
    /// <typeparam name="T">type of data being communicated by the port</typeparam>
    public abstract class PortWriter<T> : 
        FunctionalUnit,
        IPortWriter
    {
        /// <summary>
        /// Clock input signal
        /// </summary>
        public In<StdLogic> Clk { get; set; }

        /// <summary>
        /// Write enable signal
        /// </summary>
        public In<StdLogic> EnIn { get; set; }

        /// <summary>
        /// Data input signal
        /// </summary>
        public In<StdLogicVector> DIn { get; set; }

        /// <summary>
        /// Data read-back signal
        /// </summary>
        public Out<T> DOut { get; set; }

        /// <summary>
        /// Bit-width of data
        /// </summary>
        [PerformanceRelevant]
        public int DataWidth { get; private set; }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        private PortWriterSite<T> _TASite;
        public IPortWriterSite TASite
        {
            get { return _TASite; }
        }

        /// <summary>
        /// Port being written
        /// </summary>
        public ISignalOrPortDescriptor Port { get; internal set; }

        /// <summary>
        /// Descriptor of signal which is bound to the port.
        /// </summary>
        public ISignalOrPortDescriptor SignalDesc
        {
            get { return (ISignalOrPortDescriptor)((IDescriptive)DOut).Descriptor; }
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="dataWidth">bit-width of data communicated by the port</param>
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

    /// <summary>
    /// A port writer implemented for <c>int</c>-typed ports.
    /// </summary>
    public class IntPortWriter :
        PortWriter<int>
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
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

    /// <summary>
    /// A port writer implementation for <c>float</c>-typed ports.
    /// </summary>
    public class FloatPortWriter :
        PortWriter<float>
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
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

    /// <summary>
    /// A port writer implementation for <c>double</c>-typed ports.
    /// </summary>
    public class DoublePortWriter :
        PortWriter<double>
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
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

    /// <summary>
    /// A port writer implementation for <c>StdLogicVector</c>-typed ports.
    /// </summary>
    public class SLVPortWriter :
        PortWriter<StdLogicVector>
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="dataWidth">bit-width of data</param>
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

    /// <summary>
    /// A port writer implementation for <c>StdLogic</c>-typed ports.
    /// </summary>
    public class SLPortWriter :
        PortWriter<StdLogic>
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
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

    /// <summary>
    /// Transaction site implementation for write accesses to ports.
    /// </summary>
    /// <typeparam name="T">type of data communicated by port</typeparam>
    class PortWriterSite<T> : 
        DefaultTransactionSite, 
        IPortWriterSite
    {
        private PortWriter<T> _host;
        private bool _established;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="host">implementing port writer instance</param>
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

    /// <summary>
    /// Describes a mapping of the "wrport" XIL instruction to a port writer component.
    /// </summary>
    class PortWriterXILMapping : DefaultXILMapping
    {
        private IPortWriterSite _site;
        private int _dataWidth;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="site">transaction site</param>
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

        /// <summary>
        /// Always 0
        /// </summary>
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

    /// <summary>
    /// A service for mapping the "wrport" XIL instruction to an inline hardware implementation.
    /// </summary>
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

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="host">hosting component</param>
        /// <param name="port">port being accessed</param>
        public InlinePortWriteSite(Component host, ISignalOrPortDescriptor port):
            base(host)
        {
            _host = host;
            _port = port;
            _dataWidth = Marshal.SerializeForHW(port.InitialValue).Size;
        }

        /// <summary>
        /// Associated port
        /// </summary>
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

    /// <summary>
    /// Describes a mapping of the "wrport" XIL instruction to an inline hardware implementation.
    /// </summary>
    class InlinePortWriterXILMapping : IXILMapping
    {
        private InlinePortWriteSite _taSite;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="taSite">implementing transaction site</param>
        public InlinePortWriterXILMapping(InlinePortWriteSite taSite)
        {
            _taSite = taSite;
        }

        public ITransactionSite TASite
        {
            get { return _taSite; }
        }

        /// <summary>
        /// Returns <c>EMappingKind.ExclusiveResource</c>, since a port is a unique resource.
        /// </summary>
        public EMappingKind ResourceKind
        {
            get { return EMappingKind.ExclusiveResource; }
        }

        /// <summary>
        /// Always 1
        /// </summary>
        public int InitiationInterval
        {
            get { return 1; }
        }

        /// <summary>
        /// Always 1
        /// </summary>
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

    /// <summary>
    /// A service for mapping the "wrport" XIL instruction to hardware.
    /// </summary>
    public class PortWriterXILMapper : IXILMapper
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public PortWriterXILMapper()
        {
        }

        #region IXILMapper Member

        /// <summary>
        /// Returns wrport
        /// </summary>
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

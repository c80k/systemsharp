/**
 * Copyright 2013 Christian Köllner
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler;
using SystemSharp.Common;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// A unit for 1D linear interpolation over equidistant data points. 
    /// A register transfer level interface to communicates with a separate memory block
    /// storing the data table is provided. Computation assumes fixed-point data.
    /// </summary>
    class LERPUnit: Component
    {
        /// <summary>
        /// Clock input signal
        /// </summary>
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// x-value input
        /// </summary>
        public In<UFix> X { private get; set; }

        /// <summary>
        /// Interpolated y-value output
        /// </summary>
        public Out<SFix> Y { private get; set; }

        /// <summary>
        /// Address output to data table
        /// </summary>
        public Out<Unsigned> Addr { private get; set; }

        /// <summary>
        /// Input from data table
        /// </summary>
        public In<SFix> Data { private get; set; }

        /// <summary>
        /// Integer bits of operand
        /// </summary>
        public int XIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional bits of operand
        /// </summary>
        public int XFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Integer bits of result
        /// </summary>
        public int YIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional bits of result
        /// </summary>
        public int YFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Address width of data table
        /// </summary>
        public int AddrWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Computation-only latency. Total latency is <c>PipeStages + 2</c>, since 2 additional
        /// clocks are required for data table lookup.
        /// </summary>
        public int PipeStages { [StaticEvaluation] get; private set; }

        private SLVSignal _yIn;
        private SLVSignal _yOut;
        private RegPipe _yPipe;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="xIntWidth">integer width of operand</param>
        /// <param name="xFracWidth">fractional width of operand</param>
        /// <param name="yIntWidth">integer width of result</param>
        /// <param name="yFracWidth">fractional width of result</param>
        /// <param name="pipeStages">desired computation-only latency</param>
        public LERPUnit(int xIntWidth, int xFracWidth, int yIntWidth, int yFracWidth, int pipeStages)
        {
            Contract.Requires<ArgumentOutOfRangeException>(xIntWidth > 0, "xIntWidth must be positive.");
            Contract.Requires<ArgumentOutOfRangeException>(xFracWidth >= 0, "xFracWidth must be non-negative.");
            Contract.Requires<ArgumentOutOfRangeException>(yIntWidth + yFracWidth > 0, "total bit-width of result must be positive");
            Contract.Requires<ArgumentOutOfRangeException>(pipeStages >= 0, "pipeStages must be non-negative.");
            Contract.Requires<ArgumentOutOfRangeException>(xFracWidth > 0 || pipeStages == 0, "xFracWidth == 0 is a degenerate case (lookup-only). No additional pipeline stages allowed.");

            PipeStages = pipeStages;
            XIntWidth = xIntWidth;
            XFracWidth = xFracWidth;
            YIntWidth = yIntWidth;
            YFracWidth = yFracWidth;
            AddrWidth = xIntWidth;

            _yIn = new SLVSignal(yIntWidth + yFracWidth);
            _yOut = new SLVSignal(yIntWidth + yFracWidth);
            _yPipe = new RegPipe(pipeStages, yIntWidth + yFracWidth);
            Bind(() =>
            {
                _yPipe.Clk = Clk;
                _yPipe.Din = _yIn;
                _yPipe.Dout = _yOut;
            });
        }

        protected override void Initialize()
        {
            if (XFracWidth > 0)
                AddClockedThread(LERPProcess, Clk.RisingEdge, Clk);
            else
                AddProcess(LookupProcess, Clk);
            AddProcess(DriveYProcess, _yOut);
        }

        [TransformIntoFSM]
        private async void LERPProcess()
        {
            await Tick;
            while (true)
            {
                Addr.Next = X.Cur.GetIntPart().Resize(AddrWidth);
                SFix alpha = UFix.FromUnsigned(X.Cur.GetFracPart(), XFracWidth).SFixValue;
                await Tick;
                SFix v0 = Data.Cur;
                Addr.Next = (X.Cur.GetIntPart() + Unsigned.One).Resize(AddrWidth);
                await Tick;
                SFix v1 = Data.Cur;
                _yIn.Next = (v0 + alpha * (v1 - v0)).Resize(YIntWidth, YFracWidth).SLVValue;
                await PipeStages.Ticks();
            }
        }

        private void LookupProcess()
        {
            if (Clk.RisingEdge())
            {
                Addr.Next = X.Cur.GetIntPart().Resize(AddrWidth);
                _yIn.Next = Data.Cur.SLVValue;
            }
        }

        private void DriveYProcess()
        {
            Y.Next = SFix.FromSLV(_yOut.Cur, YFracWidth);
        }
    }

    /// <summary>
    /// A unit for 1D linear interpolation over equidistant data points.
    /// A pre-initialized data table is stored internally. However, a register transfer level
    /// interface to internal memory is provided which allows to modify the data table during runtime.
    /// Computation assumes fixed-point data.
    /// </summary>
    public class LERP11Core: Component
    {
        /// <summary>
        /// Clock signal input
        /// </summary>
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// x-value input
        /// </summary>
        public In<StdLogicVector> X { private get; set; }

        /// <summary>
        /// interpolated y-value output
        /// </summary>
        public Out<StdLogicVector> Y { private get; set; }

        /// <summary>
        /// Read enable to internal data table
        /// </summary>
        public In<StdLogic> MemRdEn { private get; set; }

        /// <summary>
        /// Write enable to internal data table
        /// </summary>
        public In<StdLogic> MemWrEn { private get; set; }

        /// <summary>
        /// Address to internal data table
        /// </summary>
        public In<Unsigned> MemAddr { private get; set; }

        /// <summary>
        /// Data to write into internal data table
        /// </summary>
        public In<StdLogicVector> MemDataIn { private get; set; }

        /// <summary>
        /// Data read from internal data table
        /// </summary>
        public Out<StdLogicVector> MemDataOut { private get; set; }

        private Signal<UFix> _x;
        private Signal<SFix> _y;
        private Signal<Unsigned> _unitAddr;
        private Signal<SFix> _unitData;
        private VSignal<SFix> _memContent;

        private LERPUnit _lerpUnit;

        /// <summary>
        /// Integer width of operand
        /// </summary>
        public int XIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional width of operand
        /// </summary>
        public int XFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Integer width of result
        /// </summary>
        public int YIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional width of result
        /// </summary>
        public int YFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Integer width of data table word
        /// </summary>
        public int DIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional width of data table word
        /// </summary>
        public int DFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Address width of internal data table
        /// </summary>
        public int AddrWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Computation-only latency. Total latency is <c>PipeStages + 2</c>, since 2 additional clocks
        /// are required for data table lookup.
        /// </summary>
        public int PipeStages { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="xIntWidth">integer bits of operand</param>
        /// <param name="xFracWidth">fractional bits of operand</param>
        /// <param name="yIntWidth">integer bits of result</param>
        /// <param name="yFracWidth">fractional bits of result</param>
        /// <param name="pipeStages">desired computation-only latency</param>
        /// <param name="data">data table</param>
        public LERP11Core(int xIntWidth, int xFracWidth, int yIntWidth, int yFracWidth, int pipeStages,
            SFix[] data)
        {
            Contract.Requires<ArgumentOutOfRangeException>(xIntWidth > 0, "xIntWidth must be positive.");
            Contract.Requires<ArgumentOutOfRangeException>(xFracWidth >= 0, "xFracWidth must be non-negative.");
            Contract.Requires<ArgumentOutOfRangeException>(yIntWidth + yFracWidth > 0, "total bit-width of result must be positive");
            Contract.Requires<ArgumentOutOfRangeException>(pipeStages >= 0, "pipeStages must be non-negative.");
            Contract.Requires<ArgumentOutOfRangeException>(xFracWidth > 0 || pipeStages == 0, "xFracWidth == 0 is a degenerate case (lookup-only). No additional pipeline stages allowed.");
            Contract.Requires<ArgumentNullException>(data != null, "data");

            PipeStages = pipeStages;
            XIntWidth = xIntWidth;
            XFracWidth = xFracWidth;
            YIntWidth = yIntWidth;
            YFracWidth = yFracWidth;
            DIntWidth = data[0].Format.IntWidth;
            DFracWidth = data[0].Format.FracWidth;

            _x = new Signal<UFix>()
            {
                InitialValue = UFix.FromDouble(0.0, xIntWidth, xFracWidth)
            };
            _y = new Signal<SFix>()
            {
                InitialValue = SFix.FromDouble(0.0, yIntWidth, yFracWidth)
            };
            AddrWidth = MathExt.CeilPow2(data.Length);
            _unitAddr = new Signal<Unsigned>()
            {
                InitialValue = Unsigned.FromUInt(0, AddrWidth)
            };
            _memContent = new VSignal<SFix>(data.Length, _ => new Signal<SFix>() { InitialValue = data[_] });

            _lerpUnit = new LERPUnit(xIntWidth, xFracWidth, yIntWidth, yFracWidth, pipeStages);
            Bind(() =>
            {
                _lerpUnit.Clk = Clk;
                _lerpUnit.X = _x;
                _lerpUnit.Y = _y;
                _lerpUnit.Addr = _unitAddr;
                _lerpUnit.Data = _unitData;
            });
        }

        private void MemProcess()
        {
            Unsigned addr;
            if (MemRdEn.Cur == '1' || MemWrEn.Cur == '1')
            {
                addr = MemAddr.Cur;
                if (MemWrEn.Cur == '1')
                {
                    _memContent[addr].Next = SFix.FromSigned(MemDataIn.Cur.SignedValue, DFracWidth);
                }
            }
            else
            {
                addr = _unitAddr.Cur;
            }
            SFix data = _memContent[addr].Cur;
            _unitData.Next = data;
            MemDataOut.Next = data.SLVValue;
        }
    }

    /// <summary>
    /// Transaction site interface for sine, cosine and parallel sine/cosine functions
    /// </summary>
    public interface ISinCosTransactionSite :
        ITransactionSite
    {
        /// <summary>
        /// Returns a transaction which computes the sine function
        /// </summary>
        /// <param name="x">operand source</param>
        /// <param name="y">result sink</param>
        IEnumerable<TAVerb> Sin(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> y);

        /// <summary>
        /// Returns a transaction which computes the cosine function
        /// </summary>
        /// <param name="x">operand source</param>
        /// <param name="y">result sink</param>
        IEnumerable<TAVerb> Cos(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> y);

        /// <summary>
        /// Returns a transaction which computes the parallel sine/cosine function
        /// </summary>
        /// <param name="x">operand source</param>
        /// <param name="sin">result sink for sine</param>
        /// <param name="cos">result sink for cosine</param>
        IEnumerable<TAVerb> SinCos(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> sin, ISignalSink<StdLogicVector> cos);
    }

    /// <summary>
    /// Provides a synthesizable implementation of sine and cosine functions, using linear interpolation in fixed-point
    /// arithmetic. Operand is specified in scaled radians and must be in range between -1 and 1 (with -1 corresponding to -PI,
    /// 1 corresponding to PI).
    /// The component is intended to be used by high-level synthesis to map trigonometric functions.
    /// </summary>
    [DeclareXILMapper(typeof(SinCosXILMapper))]
    public class SinCosLUTCore : Component
    {
        private class TransactionSite :
            ISinCosTransactionSite
        {
            private SinCosLUTCore _host;
            private bool _established;

            public TransactionSite(SinCosLUTCore host)
            {
                _host = host;
            }

            public IEnumerable<TAVerb> Sin(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> y)
            {
                if (_host.Latency == 0)
                {
                    yield return new TAVerb(this, ETVMode.Locked, () => { },
                        _host.X.Dual.Drive(x).Par(y.Comb.Connect(_host.Sin.Dual.AsSignalSource())));
                }
                else
                {
                    for (int i = 0; i < _host.InitiationInterval; i++)
                    {
                        yield return new TAVerb(this, ETVMode.Locked, () => { }, _host.X.Dual.Drive(x));
                    }
                    for (int i = _host.InitiationInterval; i < _host.Latency; i++)
                    {
                        yield return new TAVerb(this, ETVMode.Shared, () => { });
                    }
                    yield return new TAVerb(this, ETVMode.Shared, () => { }, y.Comb.Connect(_host.Sin.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Cos(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> y)
            {
                if (_host.Latency == 0)
                {
                    yield return new TAVerb(this, ETVMode.Locked, () => { },
                        _host.X.Dual.Drive(x).Par(y.Comb.Connect(_host.Cos.Dual.AsSignalSource())));
                }
                else
                {
                    for (int i = 0; i < _host.InitiationInterval; i++)
                    {
                        yield return new TAVerb(this, ETVMode.Locked, () => { }, _host.X.Dual.Drive(x));
                    }
                    for (int i = _host.InitiationInterval; i < _host.Latency; i++)
                    {
                        yield return new TAVerb(this, ETVMode.Shared, () => { });
                    }
                    yield return new TAVerb(this, ETVMode.Shared, () => { }, y.Comb.Connect(_host.Cos.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> SinCos(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> sin, ISignalSink<StdLogicVector> cos)
            {
                if (_host.Latency == 0)
                {
                    yield return new TAVerb(this, ETVMode.Locked, () => { },
                        _host.X.Dual.Drive(x).Par(
                         sin.Comb.Connect(_host.Sin.Dual.AsSignalSource())).Par(
                         cos.Comb.Connect(_host.Cos.Dual.AsSignalSource())));
                }
                else
                {
                    for (int i = 0; i < _host.InitiationInterval; i++)
                    {
                        yield return new TAVerb(this, ETVMode.Locked, () => { }, _host.X.Dual.Drive(x));
                    }
                    for (int i = _host.InitiationInterval; i < _host.Latency; i++)
                    {
                        yield return new TAVerb(this, ETVMode.Shared, () => { });
                    }
                    yield return new TAVerb(this, ETVMode.Shared, () => { },
                        sin.Comb.Connect(_host.Sin.Dual.AsSignalSource()).Par(
                        cos.Comb.Connect(_host.Cos.Dual.AsSignalSource())));
                }
            }

            public Component Host
            {
                get { return _host; }
            }

            public string Name
            {
                get { return "SinCosUnitTransactionSite"; }
            }

            public IEnumerable<TAVerb> DoNothing()
            {
                yield return new TAVerb(this, ETVMode.Locked, () => { },
                    _host.X.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.XIntWidth + _host.XFracWidth))));
            }

            public void Establish(IAutoBinder binder)
            {
                if (_established)
                    return;

                var unit = _host;
                unit.Clk = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, StdLogic._0);
                unit.X = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "X", null, StdLogicVector._0s(unit.XIntWidth + unit.XFracWidth));
                unit.Sin = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "SinOut", null, StdLogicVector._0s(unit.YIntWidth + unit.YFracWidth));
                unit.Cos = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "CosOut", null, StdLogicVector._0s(unit.YIntWidth + unit.YFracWidth));

                _established = true;
            }
        }

        /// <summary>
        /// Clock signal input
        /// </summary>
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// Operand input
        /// </summary>
        public In<StdLogicVector> X { private get; set; }

        /// <summary>
        /// Sine output
        /// </summary>
        public Out<StdLogicVector> Sin { private get; set; }

        /// <summary>
        /// Cosine output
        /// </summary>
        public Out<StdLogicVector> Cos { private get; set; }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public ISinCosTransactionSite TASite { get; private set; }

        /// <summary>
        /// Initiation interval of computation is 2 if interpolation is performed, 1 for pure table lookup.
        /// </summary>
        public int InitiationInterval
        {
            get { return XFracWidth - LUTWidth == 1 ? 1 : 2; }
        }

        /// <summary>
        /// Total computation latency
        /// </summary>
        public int Latency
        {
            get { return InitiationInterval + 1 + PipeStages; }
        }

        private UFix _mirror, _mirror2;

        private Signal<UFix> _x;
        private Signal<UFix> _xq;
        private Signal<SFix> _sinRaw;
        private Signal<SFix> _cosRaw;
        private SLVSignal _sinIn;
        private SLVSignal _cosIn;
        private SLVSignal _sinOut;
        private SLVSignal _cosOut;
        private Signal<Unsigned> _sinAddr;
        private Signal<Unsigned> _cosAddr;
        private Signal<SFix> _sinData;
        private Signal<SFix> _cosData;
        private VSignal<SFix> _sinLUT;
        private SLVSignal _sinFlipSignIn;
        private SLVSignal _cosFlipSignIn;
        private SLVSignal _sinFlipSignOut;
        private SLVSignal _cosFlipSignOut;

        private LERPUnit _sinUnit;
        private LERPUnit _cosUnit;
        private RegPipe _sinPipe;
        private RegPipe _cosPipe;
        private RegPipe _sinFlipSignPipe;
        private RegPipe _cosFlipSignPipe;

        /// <summary>
        /// Integer width of operand
        /// </summary>
        public int XIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional width of operand
        /// </summary>
        public int XFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Integer width of result
        /// </summary>
        public int YIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional width of result
        /// </summary>
        public int YFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Integer width of data table entry
        /// </summary>
        public int DIntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional width of data table entry
        /// </summary>
        public int DFracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Resolution of data table (see remarks)
        /// </summary>
        /// <remarks>
        /// This property determines the quantization precision. The core exploits symmetry and quantizes only the
        /// [0..PI/2] section of the sine wave. And it will use 2^<c>LUTWidth</c> + 2 data points for that.
        /// </remarks>
        public int LUTWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Address width of data table
        /// </summary>
        public int AddrWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Additional pipeline stages for interpolation computation. For total latency it is better to query
        /// the <c>Latency</c> property.
        /// </summary>
        public int PipeStages { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="lutWidth">resolution of data table</param>
        /// <param name="xFracWidth">fractional width of operand</param>
        /// <param name="yFracWidth">fractional width of result</param>
        /// <param name="pipeStages">additional pipeline stages for interpolation computation</param>
        public SinCosLUTCore(int lutWidth, int xFracWidth, int yFracWidth, int pipeStages)
        {
            PipeStages = pipeStages;
            XIntWidth = 2;
            XFracWidth = xFracWidth;
            YIntWidth = 2;
            YFracWidth = yFracWidth;
            DIntWidth = 2;
            DFracWidth = yFracWidth;
            LUTWidth = lutWidth;

            _x = new Signal<UFix>()
            {
                InitialValue = UFix.FromDouble(0.0, LUTWidth + 1, XFracWidth - LUTWidth - 1)
            };
            _xq = new Signal<UFix>()
            {
                InitialValue = UFix.FromDouble(0.0, LUTWidth + 1, XFracWidth - LUTWidth - 1)
            };
            _sinRaw = new Signal<SFix>()
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth)
            };
            _cosRaw = new Signal<SFix>()
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth)
            };
            _sinIn = new SLVSignal(YIntWidth + YFracWidth)
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth).SLVValue
            };
            _cosIn = new SLVSignal(YIntWidth + YFracWidth)
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth).SLVValue
            };
            _sinOut = new SLVSignal(YIntWidth + YFracWidth)
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth).SLVValue
            };
            _cosOut = new SLVSignal(YIntWidth + YFracWidth)
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth).SLVValue
            };
            AddrWidth = lutWidth + 1;
            _sinAddr = new Signal<Unsigned>()
            {
                InitialValue = Unsigned.FromUInt(0, AddrWidth)
            };
            _cosAddr = new Signal<Unsigned>()
            {
                InitialValue = Unsigned.FromUInt(0, AddrWidth)
            };
            _sinData = new Signal<SFix>()
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth)
            };
            _cosData = new Signal<SFix>()
            {
                InitialValue = SFix.FromDouble(0.0, YIntWidth, YFracWidth)
            };
            _sinLUT = new VSignal<SFix>((1 << lutWidth) + 2, _ => new Signal<SFix>() 
            { 
                InitialValue = SFix.FromDouble(Math.Sin(Math.PI * 0.5 * _ / (double)(1 << lutWidth)), 2, yFracWidth) 
            });
            _sinFlipSignIn = new SLVSignal(1)
            {
                InitialValue = "0"
            };
            _cosFlipSignIn = new SLVSignal(1)
            {
                InitialValue = "0"
            };
            _sinFlipSignOut = new SLVSignal(1)
            {
                InitialValue = "0"
            };
            _cosFlipSignOut = new SLVSignal(1)
            {
                InitialValue = "0"
            };

            _mirror = UFix.FromUnsigned(Unsigned.One.Resize(XFracWidth + 2) << (xFracWidth + 1), xFracWidth - LUTWidth);
            _mirror2 = UFix.FromUnsigned(Unsigned.One.Resize(XFracWidth + 2) << xFracWidth, xFracWidth - LUTWidth);

            _sinPipe = new RegPipe(pipeStages, YIntWidth + YFracWidth);
            Bind(() => {
                _sinPipe.Clk = Clk;
                _sinPipe.Din = _sinIn;
                _sinPipe.Dout = _sinOut;
            });

            _cosPipe = new RegPipe(pipeStages, YIntWidth + YFracWidth);
            Bind(() => {
                _cosPipe.Clk = Clk;
                _cosPipe.Din = _cosIn;
                _cosPipe.Dout = _cosOut;
            });

            _sinFlipSignPipe = new RegPipe(2, 1);
            Bind(() => {
                _sinFlipSignPipe.Clk = Clk;
                _sinFlipSignPipe.Din = _sinFlipSignIn;
                _sinFlipSignPipe.Dout = _sinFlipSignOut;
            });

            _cosFlipSignPipe = new RegPipe(2, 1);
            Bind(() => {
                _cosFlipSignPipe.Clk = Clk;
                _cosFlipSignPipe.Din = _cosFlipSignIn;
                _cosFlipSignPipe.Dout = _cosFlipSignOut;
            });

            _sinUnit = new LERPUnit(lutWidth + 1, xFracWidth - 1 - lutWidth, YIntWidth, yFracWidth, 0);
            Bind(() =>
            {
                _sinUnit.Clk = Clk;
                _sinUnit.X = _x;
                _sinUnit.Y = _sinRaw;
                _sinUnit.Addr = _sinAddr;
                _sinUnit.Data = _sinData;
            });

            _cosUnit = new LERPUnit(lutWidth + 1, xFracWidth - 1 - lutWidth, YIntWidth, yFracWidth, 0);
            Bind(() =>
            {
                _cosUnit.Clk = Clk;
                _cosUnit.X = _xq;
                _cosUnit.Y = _cosRaw;
                _cosUnit.Addr = _cosAddr;
                _cosUnit.Data = _cosData;
            });

            TASite = new TransactionSite(this);
        }

        private void ArgsProcess()
        {
            UFix x, xd, xq;
            _sinFlipSignIn.Next = "0";
            _cosFlipSignIn.Next = "0";
            if (X.Cur[XFracWidth + 1] == '1')
            {
                // x is negative
                x = (-SFix.FromSigned(X.Cur.SignedValue, XFracWidth - LUTWidth - 1)).UFixValue.Resize(LUTWidth + 2, XFracWidth - LUTWidth - 1);
                _sinFlipSignIn.Next = "1";
            }
            else
            {
                // x is non-negative
                x = UFix.FromUnsigned(X.Cur[XFracWidth, 0].UnsignedValue, XFracWidth - LUTWidth - 1);
            }
            if (x.SLVValue[XFracWidth] == '1' || x.SLVValue[XFracWidth - 1] == '1')
            {
                // between Pi/2 and Pi
                xd = (_mirror - x).Resize(LUTWidth + 1, XFracWidth).Resize(LUTWidth + 1, XFracWidth - LUTWidth - 1);
                xq = (x - _mirror2).Resize(LUTWidth + 1, XFracWidth).Resize(LUTWidth + 1, XFracWidth - LUTWidth - 1);
                _cosFlipSignIn.Next = "1";
            }
            else
            {
                xd = x.Resize(LUTWidth + 1, XFracWidth - LUTWidth - 1);
                xq = (_mirror2 - x).Resize(LUTWidth + 1, XFracWidth).Resize(LUTWidth + 1, XFracWidth - LUTWidth - 1);
            }
            _x.Next = xd;
            _xq.Next = xq;
        }

        private void DriveResultPipes()
        {
            if (_sinFlipSignOut.Cur == "1")
                _sinIn.Next = (-_sinRaw.Cur).Resize(2, YFracWidth).SLVValue;
            else
                _sinIn.Next = _sinRaw.Cur.SLVValue;

            if (_cosFlipSignOut.Cur == "1")
                _cosIn.Next = (-_cosRaw.Cur).Resize(2, YFracWidth).SLVValue;
            else
                _cosIn.Next = _cosRaw.Cur.SLVValue;
        }

        private void DriveResultPorts()
        {
            Sin.Next = _sinOut.Cur;
            Cos.Next = _cosOut.Cur;
        }

        private void ROMProcess()
        {
            _sinData.Next = _sinLUT[_sinAddr.Cur].Cur;
            _cosData.Next = _sinLUT[_cosAddr.Cur].Cur;
        }

        protected override void Initialize()
        {
            AddProcess(ArgsProcess, X);
            AddProcess(DriveResultPipes, _sinFlipSignOut, _sinRaw, _cosFlipSignOut, _cosRaw);
            AddProcess(DriveResultPorts, _sinOut, _cosOut);
            AddProcess(ROMProcess, _sinAddr, _cosAddr);
        }
    }

    /// <summary>
    /// A service for mapping the scsincos (parallel sine/cosine over scaled radians) XIL instruction to hardware.
    /// </summary>
    public class SinCosXILMapper : IXILMapper
    {
        private class Mapping : IXILMapping
        {
            private SinCosLUTCore _host;

            public Mapping(SinCosLUTCore host)
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
                get { return _host.InitiationInterval; }
            }

            public int Latency
            {
                get { return _host.Latency; }
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _host.TASite.SinCos(operands[0], results[1], results[0]);
            }

            public string Description
            {
                get
                {
                    string text = "sfix" +
                        (_host.XIntWidth + _host.XFracWidth) + "_" + _host.XFracWidth +
                        " -> " + (_host.YIntWidth + _host.YFracWidth) + "_" + _host.YFracWidth + ", " +
                        (3 + _host.PipeStages) + " stage LUT-based scaled radian sin/cos";
                    return text;
                }
            }
        }

        /// <summary>
        /// Returns scsincos
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.ScSinCos();
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            var scc = fu as SinCosLUTCore;
            if (scc == null)
                yield break;

            if (operandTypes.Length != 1 || resultTypes.Length != 2)
                yield break;

            var xType = TypeDescriptor.GetTypeOf(SFix.FromDouble(0.0, scc.XIntWidth, scc.XFracWidth));
            var yType = TypeDescriptor.GetTypeOf(SFix.FromDouble(0.0, scc.YIntWidth, scc.YFracWidth));
            if (!operandTypes[0].Equals(xType) ||
                !resultTypes[0].Equals(yType) ||
                !resultTypes[1].Equals(yType))
                yield break;

            if (instr.Name != InstructionCodes.ScSinCos)
                yield break;

            yield return new Mapping(scc);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            if (operandTypes.Length != 1 || resultTypes.Length != 2)
                return null;

            if (!operandTypes[0].CILType.Equals(typeof(SFix)) ||
                !resultTypes[0].CILType.Equals(typeof(SFix)) || 
                !resultTypes[1].Equals(resultTypes[0]))
                return null;

            var xfmt = SFix.GetFormat(operandTypes[0]);
            var yfmt = SFix.GetFormat(resultTypes[0]);

            int lutWidth = Math.Max(1, xfmt.FracWidth - 1);
            int mulWidth = Math.Max(1, xfmt.FracWidth - lutWidth);
            int pipeStages = 0; // 2 * mulWidth * mulWidth / (18 * 18) + yfmt.FracWidth / 18 + 1;
            var scc = new SinCosLUTCore(lutWidth, xfmt.FracWidth, yfmt.FracWidth, pipeStages);

            var mappings = TryMap(scc.TASite, instr, operandTypes, resultTypes);
            Debug.Assert(mappings.Any());
            return mappings.First();
        }
    }
}

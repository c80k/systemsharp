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
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.CoreGen;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace SystemSharp.Interop.Xilinx.IPCores
{
    /// <summary>
    /// Transaction site interface for Xilinx dividers.
    /// </summary>
    public interface IDividerTransactionSite:
        ITransactionSite
    {
        /// <summary>
        /// Performs a division.
        /// </summary>
        /// <returns>division transaction</returns>
        IEnumerable<TAVerb> Divide(ISignalSource<StdLogicVector> dividend, ISignalSource<StdLogicVector> divisor,
            ISignalSink<StdLogicVector> quotient);

        /// <summary>
        /// Performs a division with fractional part computation.
        /// </summary>
        /// <returns>division transaction</returns>
        IEnumerable<TAVerb> Divide(ISignalSource<StdLogicVector> dividend, ISignalSource<StdLogicVector> divisor,
            ISignalSink<StdLogicVector> quotient, ISignalSink<StdLogicVector> fractional);
    }

    /// <summary>
    /// Models a Xilinx divider IP core.
    /// </summary>
    [DeclareXILMapper(typeof(XilinxDividerXILMapper))]
    public class XilinxDivider : Component
    {
        private class TransactionSite :
            DefaultTransactionSite,
            IDividerTransactionSite
        {
            private XilinxDivider _host;

            public TransactionSite(XilinxDivider host) :
                base(host)
            {
                _host = host;
            }

            public override string Name
            {
                get { return "XilinxDivider"; }
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                IProcess ctrl = 
                    _host.DIVIDEND.Dual.Stick(StdLogicVector.DCs(_host.DividendAndQuotientWidth)).Par(
                    _host.DIVISOR.Dual.Stick(StdLogicVector.DCs(_host.DivisorWidth)));
                if (_host.HasCE)
                    ctrl = ctrl.Par(_host.CE.Dual.Stick('1'));
                if (_host.HasSCLR)
                    ctrl = ctrl.Par(_host.SCLR.Dual.Stick('0'));
                if (_host.HasND)
                    ctrl = ctrl.Par(_host.ND.Dual.Stick('0'));

                yield return Verb(ETVMode.Locked, ctrl);
            }

            public IEnumerable<TAVerb> Divide(ISignalSource<StdLogicVector> dividend, ISignalSource<StdLogicVector> divisor, 
                ISignalSink<StdLogicVector> quotient)
            {
                IProcess ctrl =
                    _host.DIVIDEND.Dual.Drive(dividend).Par(
                    _host.DIVISOR.Dual.Drive(divisor));
                if (_host.HasCE)
                    ctrl = ctrl.Par(_host.CE.Dual.Stick('1'));
                if (_host.HasSCLR)
                    ctrl = ctrl.Par(_host.SCLR.Dual.Stick('0'));
                if (_host.HasND)
                    ctrl = ctrl.Par(_host.ND.Dual.Stick('1'));
                for (int i = 0; i < _host.ClocksPerDivision; i++)
                    yield return Verb(ETVMode.Locked, ctrl);
                for (int i = _host.ClocksPerDivision; i < _host.Latency; i++)
                    yield return Verb(ETVMode.Shared);
                yield return Verb(ETVMode.Shared, quotient.Comb.Connect(_host.QUOTIENT.Dual.AsSignalSource()));
            }

            public IEnumerable<TAVerb> Divide(ISignalSource<StdLogicVector> dividend, ISignalSource<StdLogicVector> divisor,
                ISignalSink<StdLogicVector> quotient, ISignalSink<StdLogicVector> fractional)
            {
                IProcess ctrl =
                    _host.DIVIDEND.Dual.Drive(dividend).Par(
                    _host.DIVISOR.Dual.Drive(divisor));
                if (_host.HasCE)
                    ctrl = ctrl.Par(_host.CE.Dual.Stick('1'));
                if (_host.HasSCLR)
                    ctrl = ctrl.Par(_host.SCLR.Dual.Stick('0'));
                if (_host.HasND)
                    ctrl = ctrl.Par(_host.ND.Dual.Stick('1'));
                for (int i = 0; i < _host.ClocksPerDivision; i++)
                    yield return Verb(ETVMode.Locked, ctrl);
                for (int i = _host.ClocksPerDivision; i < _host.Latency; i++)
                    yield return Verb(ETVMode.Shared);
                yield return Verb(ETVMode.Shared, 
                    quotient.Comb.Connect(_host.QUOTIENT.Dual.AsSignalSource()),
                    fractional.Comb.Connect(_host.FRACTIONAL.Dual.AsSignalSource()));
            }

            public override void Establish(IAutoBinder binder)
            {
                var divider = _host;
                divider.CLK = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                divider.DIVIDEND = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "dividend", null, StdLogicVector._0s(divider.DividendAndQuotientWidth));
                divider.DIVISOR = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "divisor", null, StdLogicVector._0s(divider.DivisorWidth));
                divider.QUOTIENT = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "quotient", null, StdLogicVector._0s(divider.DividendAndQuotientWidth));
                if (divider.FractionWidth != 0)
                    divider.FRACTIONAL = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "fractional", null, StdLogicVector._0s(divider.FractionWidth));
                divider.RDY = binder.GetSignal<StdLogic>(EPortUsage.Default, "rdy", null, '0');
                divider.RFD = binder.GetSignal<StdLogic>(EPortUsage.Default, "rfd", null, '0');
                divider.ND = binder.GetSignal<StdLogic>(EPortUsage.Default, "nd", null, '0');
            }
        }

        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "Divider_Generator family Xilinx,_Inc. 3.0")]
            Divider_3_0
        }

        public enum ESignedness
        {
            [PropID(EPropAssoc.CoreGen, "Signed")]
            Signed,
            [PropID(EPropAssoc.CoreGen, "Unsigned")]
            Unsigned
        }

        public enum ERemainder
        {
            [PropID(EPropAssoc.CoreGen, "Remainder")]
            Remainder,
            [PropID(EPropAssoc.CoreGen, "Fractional")]
            Fractional
        }

        public enum EFractional 
        {
            [PropID(EPropAssoc.CoreGen, "Fractional")]
            Fractional
        }

        public enum ERadix
        {
            [PropID(EPropAssoc.CoreGen, "Radix2")]
            Radix2,
            [PropID(EPropAssoc.CoreGen, "High_Radix")]
            HighRadix
        }

        public enum ELatencyConfiguration
        {
            [PropID(EPropAssoc.CoreGen, "Automatic")]
            Automatic,
            [PropID(EPropAssoc.CoreGen, "Manual")]
            Manual
        }

        public enum ESclrOverrrideCe
        {
            [PropID(EPropAssoc.CoreGen, " SCLR_overrides_CE")]
            SclrOverrrideCe
        }

        public enum ECeOverrrideSclr
        {
            [PropID(EPropAssoc.CoreGen, "CE_overrides_SCLR")]
            ECeOverrrideSclr
        }

        public In<StdLogic> CLK { private get; set; }
        public In<StdLogic> CE { private get; set; }
        public In<StdLogic> SCLR { private get; set; }
        public In<StdLogicVector> DIVIDEND { private get; set; }
        public In<StdLogicVector> DIVISOR { private get; set; }
        public In<StdLogic> ND { private get; set; }
        public Out<StdLogic> RFD { private get; set;}
        public Out<StdLogicVector> QUOTIENT { private get; set;}
        public Out<StdLogicVector> FRACTIONAL{ private get; set;}
        public Out<StdLogic> RDY { private get; set;}
        public Out<StdLogic> DIVIDE_BY_ZERO { private get; set;}

        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "algorithm_type")]
        [PerformanceRelevant]
        public ERadix AlgorithmType { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        public string ComponentName { get; set; }       

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "dividend_and_quotient_width")]
        [PerformanceRelevant]
        public int DividendAndQuotientWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "divisor_width")]
        [PerformanceRelevant]
        public int DivisorWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "remainder_type")]
        [PerformanceRelevant]
        public ERemainder RemainderType { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "operand_sign")]
        [PerformanceRelevant]
        public ESignedness OperandSign { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "fractional_width")]
        [PerformanceRelevant]
        public int FractionWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "latency_configuration")]
        [PerformanceRelevant]
        public ELatencyConfiguration LatencyConfiguration { get; set; }

        private int _latency;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "latency")]
        [PerformanceRelevant]
        public int Latency 
        {
            get
            {
                switch (LatencyConfiguration)
                {
                    case ELatencyConfiguration.Automatic:
                        return AutoLatency;

                    case ELatencyConfiguration.Manual:
                        if (_latency < MinLatency || _latency > MaxLatency)
                            _latency = AutoLatency;
                        return _latency;

                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                if (LatencyConfiguration == ELatencyConfiguration.Automatic &&
                    value != AutoLatency)
                    throw new ArgumentException("Set LatencyConfiguration to Manual first");
                if (value < MinLatency && value > MaxLatency)
                    throw new ArgumentException("Invalid latency");
                _latency = value;
            }
        }

        public static readonly int[] ValidClocksPerDivision = new int[] { 1, 2, 4, 8 };

        private int _clocksPerDivision;
    
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clocks_per_division")]
        [PerformanceRelevant]
        public int ClocksPerDivision 
        {
            get
            {
                if (AlgorithmType == ERadix.HighRadix &&
                    _clocksPerDivision != 1)
                    _clocksPerDivision = 1;
                else if (!ValidClocksPerDivision.Contains(_clocksPerDivision))
                    _clocksPerDivision = 1;
                return _clocksPerDivision;
            }
            set
            {
                if (AlgorithmType == ERadix.HighRadix &&
                    value != 1)
                    throw new ArgumentException("In HighRadix mode, only 1 is adminissble");
                if (!ValidClocksPerDivision.Contains(value))
                    throw new ArgumentException("Only the following values as admissible: " + string.Join(", ", ValidClocksPerDivision));
                _clocksPerDivision = value;
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "divide_by_zero_detect")]
        [PerformanceRelevant]
        public bool DivideByZeroDetect { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "ce")]
        [PerformanceRelevant]
        public bool HasCE { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sclr")]
        [PerformanceRelevant]
        public bool HasSCLR { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sclr_ce_priority")]
        [PerformanceRelevant]
        public ESclrOverrrideCe SclrCePriority { get; set; }

        public bool HasND
        {
            get { return AlgorithmType == ERadix.HighRadix; }
        }

        public int AutoLatency
        {
            get
            {
                switch (AlgorithmType)
                {
                    case ERadix.Radix2:
                        {
                            int baselat = DividendAndQuotientWidth + 2;
                            if (RemainderType == ERemainder.Fractional)
                                baselat += FractionWidth;
                            if (OperandSign == ESignedness.Signed)
                                baselat += 2;
                            if (ClocksPerDivision > 1)
                                baselat += 1;
                            return baselat;
                        }

                    case ERadix.HighRadix:
                        return MinLatency;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public int MinLatency
        {
            get
            {
                switch (AlgorithmType)
                {
                    case ERadix.Radix2:
                        return AutoLatency;

                    case ERadix.HighRadix:
                        {
                            int k = DividendAndQuotientWidth + FractionWidth;
                            if (k <= 12)
                                return 2;
                            else if (k <= 26)
                                return 3;
                            else if (k <= 40)
                                return 4;
                            else if (k <= 54)
                                return 5;
                            else if (k <= 68)
                                return 6;
                            else
                                return 7;
                        }

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public int MaxLatency
        {
            get
            {
                switch (AlgorithmType)
                {
                    case ERadix.Radix2:
                        return AutoLatency;

                    case ERadix.HighRadix:
                        {
                            int k = DividendAndQuotientWidth + FractionWidth;
                            int d = DivisorWidth;
                            if (k <= 12)
                            {
                                if (d <= 8)
                                    return 16;
                                else if (d <= 18)
                                    return 17;
                                else if (d <= 32)
                                    return 18;
                                else if (d <= 35)
                                    return 19;
                                else if (d <= 48)
                                    return 20;
                                else if (d <= 52)
                                    return 22;
                                else
                                    return 23;
                            }
                            else if (k <= 26)
                            {
                                if (d <= 8)
                                    return 20;
                                else if (d <= 18)
                                    return 21;
                                else if (d <= 32)
                                    return 22;
                                else if (d <= 35)
                                    return 23;
                                else if (d <= 48)
                                    return 24;
                                else if (d <= 52)
                                    return 26;
                                else
                                    return 27;
                            }
                            else if (k <= 40)
                            {
                                if (d <= 8)
                                    return 24;
                                else if (d <= 18)
                                    return 25;
                                else if (d <= 32)
                                    return 26;
                                else if (d <= 35)
                                    return 27;
                                else if (d <= 48)
                                    return 28;
                                else if (d <= 52)
                                    return 30;
                                else
                                    return 31;
                            }
                            else if (k <= 54)
                            {
                                if (d <= 8)
                                    return 29;
                                else if (d <= 18)
                                    return 30;
                                else if (d <= 32)
                                    return 31;
                                else if (d <= 35)
                                    return 32;
                                else if (d <= 48)
                                    return 33;
                                else if (d <= 52)
                                    return 35;
                                else
                                    return 36;
                            }
                            else if (k <= 68)
                            {
                                if (d <= 8)
                                    return 33;
                                else if (d <= 18)
                                    return 34;
                                else if (d <= 32)
                                    return 35;
                                else if (d <= 35)
                                    return 36;
                                else if (d <= 48)
                                    return 37;
                                else if (d <= 52)
                                    return 39;
                                else if (d <= 54)
                                    return 40;
                            }
                            else
                            {
                                if (d <= 8)
                                    return 37;
                                else if (d <= 18)
                                    return 38;
                                else if (d <= 32)
                                    return 39;
                                else if (d <= 35)
                                    return 40;
                                else if (d <= 48)
                                    return 41;
                                else if (d <= 52)
                                    return 43;
                                else
                                    return 44;
                            }

                            // not reachable
                            throw new InvalidOperationException();
                        }

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private TransactionSite _tasite;

        /// <summary>
        /// Returns the divider transaction site.
        /// </summary>
        public IDividerTransactionSite TASite
        {
            get { return _tasite; }
        }

        private SLVSignal _quotient, _quotientOut;
        private SLVSignal _remainder, _remainderOut;
        private RegPipe _qpipe;
        private RegPipe _rpipe;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public XilinxDivider()
        {
            Generator = EGenerator.Divider_3_0;
            AlgorithmType = ERadix.Radix2;
            HasCE = false;
            ClocksPerDivision = 1;
            DivideByZeroDetect = false;
            DividendAndQuotientWidth = 16;
            DivisorWidth = 16;
            FractionWidth = 16;
            Latency = 20;
            LatencyConfiguration = ELatencyConfiguration.Automatic;
            OperandSign = ESignedness.Signed;
            RemainderType = ERemainder.Remainder;
            HasSCLR = false;
            SclrCePriority = ESclrOverrrideCe.SclrOverrrideCe;
            _tasite = new TransactionSite(this);
        }

        protected override void PreInitialize()
        {
            _quotient = new SLVSignal(DividendAndQuotientWidth);
            _remainder = new SLVSignal(FractionWidth);
            _quotientOut = new SLVSignal(DividendAndQuotientWidth);
            _remainderOut = new SLVSignal(FractionWidth);

            _qpipe = new RegPipe(Latency, DividendAndQuotientWidth);
            Bind(() => 
            {
                _qpipe.Clk = CLK;
                _qpipe.Din = _quotient;
                _qpipe.Dout = _quotientOut;
            });
            _rpipe = new RegPipe(Latency, FractionWidth);
            Bind(() =>
            {
                _rpipe.Clk = CLK;
                _rpipe.Din = _remainder;
                _rpipe.Dout = _remainderOut;
            });
        }

        [DoNotAnalyze]
        private void TransferResult()
        {
            QUOTIENT.Next = _quotientOut.Cur;
            FRACTIONAL.Next = _remainderOut.Cur;
        }

        [DoNotAnalyze]
        private void Processing()
        {
            if (CLK.RisingEdge())
            {
                Signed tmp = DIVISOR.Cur.SignedValue.Resize(DividendAndQuotientWidth + FractionWidth) /
                    DIVIDEND.Cur.SignedValue;
                _quotient.Next = tmp.SLVValue[DividendAndQuotientWidth + FractionWidth - 1, FractionWidth];
                if (FractionWidth > 0)
                    _remainder.Next = tmp.SLVValue[FractionWidth - 1, 0];
            }
        }

        protected override void OnSynthesis(ISynthesisContext ctx)
        {
            var xproj = ctx.Project as XilinxProject;
            if (xproj == null)
                return;
            string name = ctx.CodeGen.GetComponentID(Descriptor);
            ComponentName = name;
            CoreGenDescription cgproj, xco;
            xproj.AddNewCoreGenDescription(name, out cgproj, out xco);
            xco.FromComponent(this);
            xco.Store();
            xproj.ExecuteCoreGen(xco.Path, cgproj.Path);
        }

        protected override void Initialize()
        {
            AddProcess(Processing, CLK);
            AddProcess(TransferResult, _quotientOut, _remainderOut);
        }
    }

    /// <summary>
    /// Maps integral and fixed-point divisions to the Xilinx divider IP core.
    /// </summary>
    public class XilinxDividerXILMapper : IXILMapper
    {
        /// <summary>
        /// Provides core-specific tuning options.
        /// </summary>
        public class CoreConfig
        {
            internal CoreConfig()
            {
                PipeStageScaling = 1.0;
            }

            /// <summary>
            /// Gets or sets the latency scaling factor.
            /// </summary>
            public double PipeStageScaling { get; set; }
        }

        /// <summary>
        /// Provides core-specific tuning options.
        /// </summary>
        public CoreConfig Config { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public XilinxDividerXILMapper()
        {
            Config = new CoreConfig();
        }

        private class DividerXILMapping : IXILMapping
        {
            private XilinxDivider _host;

            public DividerXILMapping(XilinxDivider host)
            {
                _host = host;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                if (results.Length == 1)
                    return _host.TASite.Divide(operands[0], operands[1], results[0]);
                else
                    return _host.TASite.Divide(operands[0], operands[1], results[0], results[1]);
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
                get 
                {
                    switch (_host.AlgorithmType)
                    {
                        case XilinxDivider.ERadix.Radix2:
                            return _host.ClocksPerDivision;

                        case XilinxDivider.ERadix.HighRadix:
                            // The core has a complicated throughput behavior and actually
                            // relies on handshake signals (nd/rfd). Doesn't fit into the
                            // concept... Workaround: Provide a viable upper bound.
                            // The following was found experimentally:
                            return (_host.Latency + 1) / 2;

                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public int Latency
            {
                get { return _host.Latency; }
            }

            public string Description
            {
                get
                {
                    string signed = _host.OperandSign == XilinxDivider.ESignedness.Signed ? "signed" : "unsigned";
                    return "Xilinx " +
                        _host.DividendAndQuotientWidth + "/" + _host.DivisorWidth + " => " +
                        _host.DividendAndQuotientWidth + "." + _host.FractionWidth + " bit, " +
                        _host.Latency + " stage " + signed + " integer divider";                    
                }
            }
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Div();
            yield return DefaultInstructionSet.Instance.DivQF();
        }

        private FixFormat GetFixFormat(TypeDescriptor type)
        {
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

        public IXILMapping TryMapOne(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            XilinxDivider divider = fu as XilinxDivider;
            if (divider == null)
                return null;

            if (instr.Name != InstructionCodes.Div &&
                instr.Name != InstructionCodes.DivQF)
                return null;

            FixFormat fmtDividend = GetFixFormat(operandTypes[0]);
            FixFormat fmtDivisor = GetFixFormat(operandTypes[1]);
            FixFormat fmtQuotient = GetFixFormat(resultTypes[0]);
            FixFormat fmtFractional = null;
            if (instr.Name == InstructionCodes.DivQF)
                fmtFractional = GetFixFormat(resultTypes[1]);

            if (fmtDividend.IsSigned != fmtDivisor.IsSigned ||
                fmtDividend.IsSigned != fmtQuotient.IsSigned)
                return null;

            if (fmtDividend.TotalWidth != fmtQuotient.TotalWidth)
                return null;

            int qFracWidth = fmtDividend.FracWidth - fmtDivisor.FracWidth;
            if (qFracWidth != fmtQuotient.FracWidth)
                return null;

            int fracWidth = 0;
            if (fmtFractional != null)
                fracWidth = fmtFractional.TotalWidth;

            if (divider.DividendAndQuotientWidth != fmtDividend.TotalWidth)
                return null;

            if (divider.DivisorWidth != fmtDivisor.TotalWidth)
                return null;

            if ((divider.OperandSign == XilinxDivider.ESignedness.Signed) !=
                fmtDividend.IsSigned)
                return null;

            if (divider.FractionWidth != fracWidth)
                return null;

            return new DividerXILMapping(divider);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var mapping = TryMapOne(taSite, instr, operandTypes, resultTypes);
            if (mapping != null)
                yield return mapping;
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (instr.Name != InstructionCodes.Div &&
                instr.Name != InstructionCodes.DivQF)
                return null;

            FixFormat fmtDividend = GetFixFormat(operandTypes[0]);
            FixFormat fmtDivisor = GetFixFormat(operandTypes[1]);
            FixFormat fmtQuotient = GetFixFormat(resultTypes[0]);

            if (fmtDividend == null ||
                fmtDivisor == null ||
                fmtQuotient == null)
                return null;

            FixFormat fmtFractional = null;
            if (instr.Name == InstructionCodes.DivQF)
                fmtFractional = GetFixFormat(resultTypes[1]);

            if (fmtDividend.IsSigned != fmtDivisor.IsSigned ||
                fmtDividend.IsSigned != fmtQuotient.IsSigned)
                return null;

            if (fmtDividend.TotalWidth != fmtQuotient.TotalWidth)
                return null;

            int qFracWidth = fmtDividend.FracWidth - fmtDivisor.FracWidth;
            if (qFracWidth != fmtQuotient.FracWidth)
                return null;

            int fracWidth = 0;
            if (fmtFractional != null)
                fracWidth = fmtFractional.TotalWidth;

            XilinxDivider divider = new XilinxDivider()
            {
                DividendAndQuotientWidth = fmtDividend.TotalWidth,
                DivisorWidth = fmtDivisor.TotalWidth,
                FractionWidth = fracWidth,
                OperandSign = fmtDividend.IsSigned ? XilinxDivider.ESignedness.Signed : XilinxDivider.ESignedness.Unsigned,
                AlgorithmType = XilinxDivider.ERadix.HighRadix,
                RemainderType = XilinxDivider.ERemainder.Fractional,
                HasCE = false,
                HasSCLR = false,
                DivideByZeroDetect = false,
                LatencyConfiguration = XilinxDivider.ELatencyConfiguration.Manual,
                ClocksPerDivision = 1
            };
            int min = divider.MinLatency;
            int max = divider.MaxLatency;
            int scaledStages = (int)Math.Round(min + Config.PipeStageScaling * (max - min));
            if (scaledStages < min)
                scaledStages = min;
            else if (scaledStages > max)
                scaledStages = max;
            divider.Latency = scaledStages;

            return new DividerXILMapping(divider);
        }
    }
}

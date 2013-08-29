/**
 * Copyright 2011-2013 Christian Köllner
 *                     Denis Tchokonthe
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
    public interface IAdderSubtracterTransactionSite : ITransactionSite
    {
        IEnumerable<TAVerb> Add(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b,
            ISignalSink<StdLogicVector> r);
        IEnumerable<TAVerb> Subtract(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b,
            ISignalSink<StdLogicVector> r);
    }

    [DeclareXILMapper(typeof(XilinxAdderSubtracterXILMapper))]
    public class XilinxAdderSubtracter : Component
    {
        private class TransactionSite :
            DefaultTransactionSite,
            IAdderSubtracterTransactionSite
        {
            private XilinxAdderSubtracter _host;

            public TransactionSite(XilinxAdderSubtracter host) :
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                IProcess action = _host.A.Dual.Stick(StdLogicVector.DCs(_host.Awidth));
                if (!_host.HasConstantInput)
                    action = action.Par(_host.B.Dual.Stick(StdLogicVector.DCs(_host.Bwidth)));
                if (_host.HasCE)
                    action = action.Par(_host.CE.Dual.Stick<StdLogic>('1'));
                if (_host.HasSCLR)
                    action = action.Par(_host.SCLR.Dual.Stick<StdLogic>('0'));
                if (_host.HasSSET)
                    action = action.Par(_host.SSET.Dual.Stick<StdLogic>('0'));
                if (_host.HasBypass)
                    action = action.Par(_host.BYPASS.Dual.Stick<StdLogic>('-'));
                if (_host.HasCarryIn)
                    action = action.Par(_host.C_in.Dual.Stick<StdLogic>('-'));
                yield return Verb(ETVMode.Locked, action);
            }

            public IEnumerable<TAVerb> Add(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                IProcess action = _host.A.Dual.Drive(a).Par(
                    _host.B.Dual.Drive(b));
                if (_host.HasCE)
                    action = action.Par(_host.CE.Dual.Stick<StdLogic>('1'));
                if (_host.HasSCLR)
                    action = action.Par(_host.SCLR.Dual.Stick<StdLogic>('0'));
                if (_host.HasBypass)
                    action = action.Par(_host.BYPASS.Dual.Stick<StdLogic>(_host.BypassSense == ESense.ActiveHigh ? StdLogic._0 : StdLogic._1));
                if (_host.HasCarryIn)
                    action = action.Par(_host.C_in.Dual.Stick<StdLogic>('0'));
                if (_host.AddMode == EAddMode.AddSubtract)
                    action = action.Par(_host.ADD.Dual.Stick<StdLogic>('1'));
                if (_host.Latency == 0)
                {
                    action = action.Par(r.Comb.Connect(_host.S.Dual.AsSignalSource()));
                    yield return Verb(ETVMode.Locked, action);
                }
                else
                {
                    yield return Verb(ETVMode.Locked, action);
                    for (int i = 1; i < _host.Latency; i++)
                        yield return Verb(ETVMode.Shared);
                    yield return Verb(ETVMode.Shared, r.Comb.Connect(_host.S.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Subtract(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                IProcess action = _host.A.Dual.Drive(a).Par(
                    _host.B.Dual.Drive(b));
                if (_host.HasCE)
                    action = action.Par(_host.CE.Dual.Stick<StdLogic>('1'));
                if (_host.HasSCLR)
                    action = action.Par(_host.SCLR.Dual.Stick<StdLogic>('0'));
                if (_host.HasBypass)
                    action = action.Par(_host.BYPASS.Dual.Stick<StdLogic>(_host.BypassSense == ESense.ActiveHigh ? StdLogic._0 : StdLogic._1));
                if (_host.HasCarryIn)
                    action = action.Par(_host.C_in.Dual.Stick<StdLogic>('0'));
                if (_host.AddMode == EAddMode.AddSubtract)
                    action = action.Par(_host.ADD.Dual.Stick<StdLogic>('0'));
                if (_host.Latency == 0)
                {
                    action = action.Par(r.Comb.Connect(_host.S.Dual.AsSignalSource()));
                    yield return Verb(ETVMode.Locked, action);
                }
                else
                {
                    yield return Verb(ETVMode.Locked, action);
                    for (int i = 1; i < _host.Latency; i++)
                        yield return Verb(ETVMode.Shared);
                    yield return Verb(ETVMode.Shared, r.Comb.Connect(_host.S.Dual.AsSignalSource()));
                }
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_host.Latency > 0)
                    _host.CLK = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                _host.A = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "A", null, StdLogicVector._0s(_host.Awidth));
                _host.B = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "B", null, StdLogicVector._0s(_host.Bwidth));
                _host.S = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "S", null, StdLogicVector._0s(_host.OutWidth));
                if (_host.HasBypass)
                    _host.BYPASS = binder.GetSignal<StdLogic>(EPortUsage.Default, "bypass", null, '0');
                if (_host.HasCarryIn)
                    _host.C_in = binder.GetSignal<StdLogic>(EPortUsage.Default, "c_in", null, '0');
                if (_host.HasCarryOut)
                    _host.C_out = binder.GetSignal<StdLogic>(EPortUsage.Default, "c_out", null, '0');
                if (_host.HasCE)
                    _host.CE = binder.GetSignal<StdLogic>(EPortUsage.Default, "ce", null, '0');
                if (_host.HasSCLR)
                    _host.SCLR = binder.GetSignal<StdLogic>(EPortUsage.Default, "sclr", null, '0');
                if (_host.HasSSET)
                    _host.SSET = binder.GetSignal<StdLogic>(EPortUsage.Default, "sset", null, '0');
                //FIXME: What about SINIT?
            }
        }

        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "Adder_Subtracter family Xilinx,_Inc. 11.0")]
            Adder_Subtracter_11_0
        }

        public enum ESignedness
        {
            [PropID(EPropAssoc.CoreGen, "Signed")]
            Signed,
            [PropID(EPropAssoc.CoreGen, "Unsigned")]
            Unsigned
        }

        public enum EAddMode
        {
            [PropID(EPropAssoc.CoreGen, "Add")]
            Add,

            [PropID(EPropAssoc.CoreGen, "Subtract")]
            Subtract,

            [PropID(EPropAssoc.CoreGen, "AddSubtract")]
            AddSubtract
        }

        public enum ESense
        {
            [PropID(EPropAssoc.CoreGen, "Active_Low")]
            ActiveLow,

            [PropID(EPropAssoc.CoreGen, "Active_High")]
            ActiveHigh
        }

        public enum EImplementation
        {
            [PropID(EPropAssoc.CoreGen, "Fabric")]
            Fabric,

            [PropID(EPropAssoc.CoreGen, "DSP48")]
            DSP48
        }

        public enum ECeOverridesBypass
        {
            [PropID(EPropAssoc.CoreGen, "CE_Overrides_Bypass")]
            CeOverridesBypass
        }

        public enum ELatencyConfiguration
        {
            [PropID(EPropAssoc.CoreGen, "Manual")]
            Manual,

            [PropID(EPropAssoc.CoreGen, "Automatic")]
            Automatic
        }

        public enum ESyncOverridesCe
        {
            [PropID(EPropAssoc.CoreGen, "Sync_Overrides_CE")]
            SyncOverridesCe
        }

        public enum ERsetOverridesSet
        {
            [PropID(EPropAssoc.CoreGen, "Reset_Overrides_Set")]
            RsetOverridesSet
        }

        public IAdderSubtracterTransactionSite TASite { get; private set; }

        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }
        public In<StdLogic> CLK { private get; set; }
        public In<StdLogic> ADD { private get; set; }
        public In<StdLogic> C_in { private get; set; }
        public Out<StdLogic> C_out { private get; set; }
        public Out<StdLogicVector> S { private get; set; }
        public In<StdLogic> CE { private get; set; }
        public In<StdLogic> BYPASS { private get; set; }
        public In<StdLogic> SCLR { private get; set; }
        public In<StdLogic> SSET { private get; set; }
        public In<StdLogic> SINIT { private get; set; }

        public EDeviceFamily TargetDeviceFamily { get; set; }

        private SLVSignal _result;
        private RegPipe _rpipe;

        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "a_type")]
        [PerformanceRelevant]
        public ESignedness Atype { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "b_type")]
        [PerformanceRelevant]
        public ESignedness Btype { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "a_width")]
        [PerformanceRelevant]
        public int Awidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "add_mode")]
        [PerformanceRelevant]
        public EAddMode AddMode { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "ainit_value")]
        public int AinitValue { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "b_constant")]
        [PerformanceRelevant]
        public bool Bconstant { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "b_width")]
        [PerformanceRelevant]
        public int Bwidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "b_value")]
        public StdLogicVector Bvalue { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "borrow_sense")]
        [PerformanceRelevant]
        public ESense BorrowSense { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "bypass")]
        [PerformanceRelevant]
        public bool HasBypass { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "bypass_ce_priority")]
        [PerformanceRelevant]
        public ECeOverridesBypass BypassCePriority { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "bypass_sense")]
        [PerformanceRelevant]
        public ESense BypassSense { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_in")]
        [PerformanceRelevant]
        public bool HasCarryIn { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_out")]
        [PerformanceRelevant]
        public bool HasCarryOut { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "ce")]
        [PerformanceRelevant]
        public bool HasCE { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        public string ComponentName { get; set; }
       
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "implementation")]
        [PerformanceRelevant]
        public EImplementation Implementation { get; set; }

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
                        switch (TargetDeviceFamily)
                        {
                            case EDeviceFamily.Virtex4:
                                return (OutWidth - 1) / 12 + 1;

                            case EDeviceFamily.Virtex5:
                            case EDeviceFamily.Virtex6:
                            case EDeviceFamily.Virtex6_LowPower:
                                return (OutWidth - 1) / 20 + 1;

                            case EDeviceFamily.Spartan3A_DSP:
                                return (OutWidth - 1) / 8 + 1;

                            default:
                                return (OutWidth - 1) / 10 + 1;
                        }

                    case ELatencyConfiguration.Manual:
                        return _latency;

                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                if (LatencyConfiguration != ELatencyConfiguration.Manual)
                    throw new InvalidOperationException("Please set LatencyConfiguration to ELatencyConfiguration.Manual first");

                _latency = value;
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "latency_configuration")]
        public ELatencyConfiguration LatencyConfiguration { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "out_width")]
        [PerformanceRelevant]
        public int OutWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sclr")]
        [PerformanceRelevant]
        public bool HasSCLR { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sinit")]
        [PerformanceRelevant]
        public bool HasConstantInput { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sinit_value")]
        public StdLogicVector ConstantInputValue { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sset")]
        [PerformanceRelevant]
        public bool HasSSET { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sync_ce_priority")]
        [PerformanceRelevant]
        public ESyncOverridesCe SyncCePriority { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sync_ctrl_priority")]
        [PerformanceRelevant]
        public ERsetOverridesSet SyncCtrlPriority { get; set; }

        public XilinxAdderSubtracter()
        {
            TASite = new TransactionSite(this);
            Generator = EGenerator.Adder_Subtracter_11_0;
            Atype = ESignedness.Signed;
            Btype = ESignedness.Signed;
            Awidth = 20;
            AddMode = EAddMode.Add;
            AinitValue = 0;
            Bconstant = false;
            Bvalue = "00000000000000000000";
            Bwidth = 20;
            BorrowSense = ESense.ActiveLow;
            HasBypass = false;
            BypassSense = ESense.ActiveHigh;
            BypassCePriority = ECeOverridesBypass.CeOverridesBypass;
            HasCarryIn = false;
            HasCE = false;
            Implementation = EImplementation.Fabric;
            Latency = 2;
            LatencyConfiguration = ELatencyConfiguration.Manual;
            OutWidth = 20;
            HasSCLR = false;
            HasConstantInput = false;
            ConstantInputValue = StdLogicVector._0s(Bwidth);
            HasSSET = false;
            SyncCePriority = ESyncOverridesCe.SyncOverridesCe;
            SyncCtrlPriority = ERsetOverridesSet.RsetOverridesSet;
        }

        public int MaxLatency
        {
            get
            {
                switch (Implementation)
                {
                    case EImplementation.DSP48:
                        return 2;

                    case EImplementation.Fabric:
                        return Math.Min(64, OutWidth);

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        [DoNotAnalyze]
        private void Processing()
        {
            if (Latency == 0 || (CLK.RisingEdge() && (!HasCE || CE.Cur == '1')))
            {
                int width = Math.Max(Awidth, Bwidth);
                if (AddMode == EAddMode.Add || 
                    (AddMode == EAddMode.AddSubtract && ADD.Cur == '1'))
                {
                    _result.Next =
                        (A.Cur.UnsignedValue.Resize(width) +
                        B.Cur.UnsignedValue.Resize(width)).Resize(OutWidth).SLVValue;
                }
                else
                {
                    _result.Next =
                        (A.Cur.UnsignedValue.Resize(width) -
                        B.Cur.UnsignedValue.Resize(width)).Resize(OutWidth).SLVValue;
                }
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
            Descriptor.AddAttribute(xco);
            xproj.ExecuteCoreGen(xco.Path, cgproj.Path);
        }

        protected override void PreInitialize()
        {
            _result = new SLVSignal(OutWidth);
            _rpipe = new RegPipe(Latency, OutWidth);
            Bind(() =>
            {
                _rpipe.Clk = CLK;
                _rpipe.Din = _result;
                _rpipe.Dout = S;
            });
        }

        protected override void Initialize()
        {
            if (Latency == 0)
                AddProcess(Processing, A, B);
            else
                AddProcess(Processing, CLK);
        }

        public override bool IsEquivalent(Component component)
        {
            var other = component as XilinxAdderSubtracter;
            if (other == null)
                return false;
            return AddMode == other.AddMode &&
                this.AinitValue == other.AinitValue &&
                this.Atype == other.Atype &&
                this.Awidth == other.Awidth &&
                this.Bconstant == other.Bconstant &&
                this.BorrowSense == other.BorrowSense &&
                this.Btype == other.Btype &&
                this.Bvalue == other.Bvalue &&
                this.Bwidth == other.Bwidth &&
                this.BypassCePriority == other.BypassCePriority &&
                this.BypassSense == other.BypassSense &&
                this.ConstantInputValue == other.ConstantInputValue &&
                this.Generator == other.Generator &&
                this.HasBypass == other.HasBypass &&
                this.HasCarryIn == other.HasCarryIn &&
                this.HasCarryOut == other.HasCarryOut &&
                this.HasCE == other.HasCE &&
                this.HasConstantInput == other.HasConstantInput &&
                this.HasSCLR == other.HasSCLR &&
                this.HasSSET == other.HasSSET &&
                this.Implementation == other.Implementation &&
                this.Latency == other.Latency &&
                this.LatencyConfiguration == other.LatencyConfiguration &&
                this.OutWidth == other.OutWidth;
        }

        public override int GetBehaviorHashCode()
        {
            return AddMode.GetHashCode() ^
                AinitValue.GetHashCode() ^
                Atype.GetHashCode() ^
                Awidth ^
                Bconstant.GetHashCode() ^
                BorrowSense.GetHashCode() ^
                Btype.GetHashCode() ^
                Bvalue.GetHashCode() ^
                Bwidth ^
                BypassCePriority.GetHashCode() ^
                BypassSense.GetHashCode() ^
                ConstantInputValue.GetHashCode() ^
                Generator.GetHashCode() ^
                HasBypass.GetHashCode() ^
                HasCarryIn.GetHashCode() ^
                HasCarryOut.GetHashCode() ^
                HasCE.GetHashCode() ^
                HasConstantInput.GetHashCode() ^
                HasSCLR.GetHashCode() ^
                HasSSET.GetHashCode() ^
                Implementation.GetHashCode() ^
                Latency.GetHashCode() ^
                LatencyConfiguration.GetHashCode() ^
                OutWidth;
        }
    }

    public class XilinxAdderSubtracterXILMapper : IXILMapper
    {
        public class CoreConfig
        {
            public CoreConfig()
            {
                PipeStageScaling = 1.0;
            }

            public double PipeStageScaling { get; set; }
        }

        public XilinxAdderSubtracterXILMapper()
        {
            Config = new CoreConfig();
        }

        public CoreConfig Config { get; private set; }

        private class XILMapping : IXILMapping
        {
            private XilinxAdderSubtracter _host;
            private bool _isAdd;
            private bool _swap;

            public XILMapping(XilinxAdderSubtracter host, bool isAdd, bool swap)
            {
                _host = host;
                _isAdd = isAdd;
                _swap = swap;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                int i0 = _swap ? 1 : 0;
                int i1 = _swap ? 0 : 1;
                if (_isAdd)
                    return _host.TASite.Add(operands[i0], operands[i1], results[0]);
                else
                    return _host.TASite.Subtract(operands[i0], operands[i1], results[0]);
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
                get { return 1; }
            }

            public int Latency
            {
                get { return _host.Latency; }
            }

            public string Description
            {
                get
                {
                    string text = "Xilinx " + _host.Awidth + "/" + _host.Bwidth + " bit, " + _host.Latency + " stage integer ";
                    switch (_host.AddMode)
                    {
                        case XilinxAdderSubtracter.EAddMode.Add:
                            return text + " adder";

                        case XilinxAdderSubtracter.EAddMode.AddSubtract:
                            return text + " adder/subtracter";

                        case XilinxAdderSubtracter.EAddMode.Subtract:
                            return text + " subtracter";

                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Add();
            yield return DefaultInstructionSet.Instance.Sub();
        }

        private FixFormat GetFixFormat(TypeDescriptor type)
        {
            if (!type.IsComplete)
                return null;

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

        public IXILMapping TryMapOne(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, bool swap)
        {
            var fu = taSite.Host;
            var asub = fu as XilinxAdderSubtracter;
            if (asub == null)
                return null;

            bool iisAdd = instr.Name == InstructionCodes.Add;
            bool iisSub = instr.Name == InstructionCodes.Sub;

            if ((asub.AddMode == XilinxAdderSubtracter.EAddMode.Add && !iisAdd) ||
                (asub.AddMode == XilinxAdderSubtracter.EAddMode.Subtract && !iisSub) ||
                (!iisAdd && !iisSub))
                return null;

            FixFormat fmta, fmtb, fmtr;
            fmta = GetFixFormat(operandTypes[0]);
            fmtb = GetFixFormat(operandTypes[1]);
            fmtr = GetFixFormat(resultTypes[0]);
            if (fmta == null || fmtb == null || fmtr == null)
                return null;

            bool expectSigned = fmta.IsSigned || fmtb.IsSigned;
            if (expectSigned != fmtr.IsSigned)
                return null;

            if (fmta.FracWidth != fmtb.FracWidth ||
                fmtr.FracWidth != fmta.FracWidth)
                return null;

            if (fmta.TotalWidth != asub.Awidth ||
                fmtb.TotalWidth != asub.Bwidth ||
                fmtr.TotalWidth != asub.OutWidth)
                return null;

            return new XILMapping(asub, iisAdd, swap);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            IXILMapping alt0, alt1 = null;
            alt0 = TryMapOne(taSite, instr, operandTypes, resultTypes, false);
            switch (instr.Name)
            {
                case InstructionCodes.Add:
                    alt1 = TryMapOne(taSite, instr, new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;
            }

            if (alt0 != null)
                yield return alt0;
            if (alt1 != null)
                yield return alt1;
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            bool iisAdd = instr.Name == InstructionCodes.Add;
            bool iisSub = instr.Name == InstructionCodes.Sub;

            if (!iisAdd && !iisSub)
                return null;

            FixFormat fmta, fmtb, fmtr;
            fmta = GetFixFormat(operandTypes[0]);
            fmtb = GetFixFormat(operandTypes[1]);
            fmtr = GetFixFormat(resultTypes[0]);
            if (fmta == null || fmtb == null || fmtr == null)
                return null;

            bool expectSigned = fmta.IsSigned || fmtb.IsSigned;
            if (expectSigned != fmtr.IsSigned)
                return null;

            if (fmta.FracWidth != fmtb.FracWidth ||
                fmtr.FracWidth != fmta.FracWidth)
                return null;

            int expectedWidth = Math.Max(fmta.TotalWidth, fmtb.TotalWidth);
            if (fmtr.TotalWidth != expectedWidth &&
                fmtr.TotalWidth != (expectedWidth + 1))
                return null;

            var xproj = proj as XilinxProject;
            if (xproj == null)
                return null;

            var asub = new XilinxAdderSubtracter()
            {
                AddMode = iisAdd ? XilinxAdderSubtracter.EAddMode.Add : XilinxAdderSubtracter.EAddMode.Subtract,
                Atype = fmta.IsSigned ? XilinxAdderSubtracter.ESignedness.Signed : XilinxAdderSubtracter.ESignedness.Unsigned,
                Awidth = fmta.TotalWidth,
                Bconstant = false,
                Btype = fmtb.IsSigned ? XilinxAdderSubtracter.ESignedness.Signed : XilinxAdderSubtracter.ESignedness.Unsigned,
                Bwidth = fmtb.TotalWidth,
                Implementation = XilinxAdderSubtracter.EImplementation.Fabric,
                TargetDeviceFamily = xproj.DeviceFamily,
                OutWidth = fmtr.TotalWidth,
                LatencyConfiguration = XilinxAdderSubtracter.ELatencyConfiguration.Automatic
            };

            int scaledStages = (int)Math.Round(Config.PipeStageScaling * asub.Latency);
            asub.LatencyConfiguration = XilinxAdderSubtracter.ELatencyConfiguration.Manual;
            asub.Latency = scaledStages;

            return new XILMapping(asub, iisAdd, false);
        }
    }
}

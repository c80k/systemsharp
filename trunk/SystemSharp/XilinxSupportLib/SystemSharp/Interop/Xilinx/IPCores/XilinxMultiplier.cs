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
using System.Diagnostics;
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
    public interface IMultiplierTransactionSite: ITransactionSite
    {
        IEnumerable<TAVerb> Multiply(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b,
            ISignalSink<StdLogicVector> r);
    }

    [DeclareXILMapper(typeof(XilinxMultiplierXILMapper))]
    public class XilinxMultiplier : Component
    {
        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "Multiplier family Xilinx,_Inc. 11.0")]
            Multiplier_11_0,

            [PropID(EPropAssoc.CoreGen, "Multiplier family Xilinx,_Inc. 11.2")]
            Multiplier_11_2
        }

        public enum ESignedness
        {
            [PropID(EPropAssoc.CoreGen, "Signed")]
            Signed,

            [PropID(EPropAssoc.CoreGen, "Unsigned")]
            Unsigned
        }

        public enum EMultiplierType
        {
            [PropID(EPropAssoc.CoreGen, "Parallel_Multiplier")]
            ParallelMultiplier,

            [PropID(EPropAssoc.CoreGen, "Constant_Coefficient_Multiplier")]
            ConstantCoefficientMultiplier
        }

        public enum EConstruction
        {
            [PropID(EPropAssoc.CoreGen, "Use_LUTs")]
            UseLuts,

            [PropID(EPropAssoc.CoreGen, "Use_Mults")]
            UseMults
        }

        public enum EOptimizationGoal
        {
            [PropID(EPropAssoc.CoreGen, "Speed")]
            Speed,

            [PropID(EPropAssoc.CoreGen, "Area")]
            Area
        }

        public enum EMemoryType
        {
            [PropID(EPropAssoc.CoreGen, "Distributed_Memory")]
            DistributedMemory,

            [PropID(EPropAssoc.CoreGen, "Block_Memory")]
            BlockMemory
        }

        public enum ESCLR_CE_Priority
        {
            [PropID(EPropAssoc.CoreGen, "SCLR_Overrides_CE")]
            SclrOverrrideCe,

            [PropID(EPropAssoc.CoreGen, "CE_Overrides_SCLR")]
            ECeOverrrideSclr
        }

        private class TransactionSite : 
            DefaultTransactionSite,
            IMultiplierTransactionSite
        {
            private XilinxMultiplier _host;

            public TransactionSite(XilinxMultiplier host) :
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                IProcess action = _host.A.Dual.Stick(StdLogicVector.DCs(_host.PortAWidth));
                if (_host.MultType == EMultiplierType.ParallelMultiplier)
                    action = action.Par(_host.B.Dual.Stick(StdLogicVector.DCs(_host.PortBWidth)));
                if (_host.HasCE)
                    action = action.Par(_host.CE.Dual.Stick<StdLogic>('1'));
                if (_host.HasSCLR)
                    action = action.Par(_host.SCLR.Dual.Stick<StdLogic>('0'));
                yield return Verb(ETVMode.Locked, action);
            }

            public IEnumerable<TAVerb> Multiply(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> r)
            {
                IProcess action = _host.A.Dual.Drive(a).Par(
                    _host.B.Dual.Drive(b));
                if (_host.HasCE)
                    action = action.Par(_host.CE.Dual.Stick<StdLogic>('1'));
                if (_host.HasSCLR)
                    action = action.Par(_host.SCLR.Dual.Stick<StdLogic>('0'));
                if (_host.PipeStages == 0)
                {
                    action = action.Par(r.Comb.Connect(_host.P.Dual.AsSignalSource()));
                    yield return Verb(ETVMode.Locked, action);
                }
                else
                {
                    yield return Verb(ETVMode.Locked, action);
                    for (int i = 1; i < _host.PipeStages; i++)
                        yield return Verb(ETVMode.Shared);
                    yield return Verb(ETVMode.Shared, r.Comb.Connect(_host.P.Dual.AsSignalSource()));
                }
            }

            public override void Establish(IAutoBinder binder)
            {
                var mul = _host;
                if (mul.PipeStages > 0)
                    mul.CLK = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                mul.A = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "A", null, StdLogicVector._0s(mul.PortAWidth));
                mul.B = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "B", null, StdLogicVector._0s(mul.PortBWidth));
                mul.P = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "P", null, StdLogicVector._0s(mul.OutputWidthHigh - mul.OutputWidthLow + 1));
            }
        }

        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }
        public Out<StdLogicVector> P { private get; set; }
        public In<StdLogic> CLK { private get; set; }
        public In<StdLogic> CE { private get; set; }
        public In<StdLogic> SCLR { private get; set; }

        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "ccmimp")]
        [PerformanceRelevant]
        public EMemoryType ConstantMemoryType { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "clockenable")]
        [PerformanceRelevant]
        public bool HasCE { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        public string ComponentName { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "constvalue")]
        [PerformanceRelevant]
        public int ConstValue { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "internaluser")]
        public int InternalUser { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "multiplier_construction")]
        [PerformanceRelevant]
        public EConstruction MultiplierConstruction { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "multtype")]
        [PerformanceRelevant]
        public EMultiplierType MultType { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "optgoal")]
        [PerformanceRelevant]
        public EOptimizationGoal OptGoal { get; set; }

        private int _outputWidthHigh;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "outputwidthhigh")]
        [PerformanceRelevant]
        public int OutputWidthHigh 
        {
            get 
            {
                return UseCustomOutputWidth ?
                    _outputWidthHigh :
                    DefaultOutputWidthHigh;
            }
            set
            {
                if (!UseCustomOutputWidth &&
                    value != DefaultOutputWidthHigh)
                    throw new ArgumentException("Can't set that output width - disable UseCustomOutputWidth first");
                _outputWidthHigh = value;
            }
        }

        private int _outputWidthLow;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "outputwidthlow")]
        [PerformanceRelevant]
        public int OutputWidthLow
        {
            get
            {
                return UseCustomOutputWidth ?
                    _outputWidthLow :
                    DefaultOutputWidthLow;
            }
            set
            {
                if (!UseCustomOutputWidth &&
                    value != DefaultOutputWidthLow)
                    throw new ArgumentException("Can't set that output width - disable UseCustomOutputWidth first");
                _outputWidthLow = value;
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pipestages")]
        [PerformanceRelevant]
        public int PipeStages { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "portatype")]
        [PerformanceRelevant]
        public ESignedness PortAType { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "portawidth")]
        [PerformanceRelevant]
        public int PortAWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "portbwidth")]
        [PerformanceRelevant]
        public int PortBWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "portbtype")]
        [PerformanceRelevant]
        public ESignedness PortBType { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "roundpoint")]
        [PerformanceRelevant]
        public int RoundPoint { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sclrcepriority")]
        [PerformanceRelevant]
        public ESCLR_CE_Priority SCLR_CE_Priority { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "syncclear")]
        [PerformanceRelevant]
        public bool HasSCLR { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "use_custom_output_width")]
        [PerformanceRelevant]
        public bool UseCustomOutputWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "userounding")]
        [PerformanceRelevant]
        public bool UseRounding { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "zerodetect")]
        [PerformanceRelevant]
        public bool ZeroDetect { get; set; }

        public override bool IsEquivalent(Component obj)
        {
            var other = obj as XilinxMultiplier;
            if (other == null)
                return false;
            return 
                this.ConstantMemoryType == other.ConstantMemoryType &&
                this.Generator == other.Generator &&
                this.ConstValue == other.ConstValue &&
                this.HasCE == other.HasCE &&
                this.HasSCLR == other.HasSCLR &&
                this.MultiplierConstruction == other.MultiplierConstruction &&
                this.MultType == other.MultType &&
                this.OptGoal == other.OptGoal &&
                this.OutputWidthHigh == other.OutputWidthHigh &&
                this.OutputWidthLow == other.OutputWidthLow &&
                this.PipeStages == other.PipeStages &&
                this.PortAType == other.PortAType &&
                this.PortAWidth == other.PortAWidth &&
                this.PortBType == other.PortBType &&
                this.PortBWidth == other.PortBWidth &&
                this.ResultType == other.ResultType &&
                this.RoundPoint == other.RoundPoint &&
                this.SCLR_CE_Priority == other.SCLR_CE_Priority &&
                this.UseCustomOutputWidth == other.UseCustomOutputWidth &&
                this.UseRounding == other.UseRounding &&
                this.ZeroDetect == other.ZeroDetect;
        }

        public override int GetBehaviorHashCode()
        {
            return
                ConstantMemoryType.GetHashCode() ^
                Generator.GetHashCode() ^
                ConstValue ^
                HasCE.GetHashCode() ^
                HasSCLR.GetHashCode() ^
                MultiplierConstruction.GetHashCode() ^
                MultType.GetHashCode() ^
                OptGoal.GetHashCode() ^
                OutputWidthHigh ^
                OutputWidthLow ^
                PipeStages ^
                PortAType.GetHashCode() ^
                PortAWidth ^
                PortBType.GetHashCode() ^
                PortBWidth ^
                ResultType.GetHashCode() ^
                RoundPoint ^
                SCLR_CE_Priority.GetHashCode() ^
                UseCustomOutputWidth.GetHashCode() ^
                UseRounding.GetHashCode() ^
                ZeroDetect.GetHashCode();
        }

        public int DefaultOutputWidthHigh
        {
            get { return PortAWidth + PortAWidth - 1; }
        }

        public int DefaultOutputWidthLow
        {
            get { return 0; }
        }

        public ESignedness ResultType
        {
            get
            {
                return PortAType == ESignedness.Signed || PortBType == ESignedness.Signed ?
                    ESignedness.Signed : ESignedness.Unsigned;
            }
        }

        public IMultiplierTransactionSite TASite { get; private set; }

        private RegPipe _outPipe;
        private SLVSignal _pipeIn;

        public XilinxMultiplier()
        {
            Generator = EGenerator.Multiplier_11_2;
            ConstantMemoryType = EMemoryType.DistributedMemory;
            HasCE = false;
            ConstValue = 129;
            InternalUser = 0;
            MultiplierConstruction = EConstruction.UseLuts;
            MultType = EMultiplierType.ParallelMultiplier;
            OptGoal = EOptimizationGoal.Speed;
            PipeStages = 1;
            PortAType = ESignedness.Signed;
            PortAWidth = 18;
            PortBWidth = 18;
            RoundPoint = 0;
            SCLR_CE_Priority = ESCLR_CE_Priority.SclrOverrrideCe;
            HasSCLR = false;
            UseCustomOutputWidth = false;
            ZeroDetect = false;
            TASite = new TransactionSite(this);
        }

        protected override void PreInitialize()
        {
            int width = OutputWidthHigh - OutputWidthLow + 1;
            _pipeIn = new SLVSignal(width);
            if (PipeStages > 0)
            {
                _outPipe = new RegPipe(PipeStages, width);
                Bind(() =>
                {
                    _outPipe.Clk = CLK;
                    _outPipe.Din = _pipeIn;
                    _outPipe.Dout = P;
                });
            }
            else
            {
                AddProcess(DrivePIm, _pipeIn);
            }
        }

        private void DrivePIm()
        {
            P.Next = _pipeIn.Cur;
        }

        [DoNotAnalyze]
        private void Processing()
        {
            switch (PortAType)
            {
                case ESignedness.Signed:
                    switch (PortBType)
                    {
                        case ESignedness.Signed:
                            {
                                Signed temp = A.Cur.SignedValue * B.Cur.SignedValue;
                                _pipeIn.Next = temp.SLVValue[OutputWidthHigh, OutputWidthLow];
                            }
                            break;

                        case ESignedness.Unsigned:
                            {
                                Signed temp = A.Cur.SignedValue * B.Cur.UnsignedValue.SignedValue;
                                _pipeIn.Next = temp.SLVValue[OutputWidthHigh, OutputWidthLow];
                            }
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                    break;

                case ESignedness.Unsigned:
                    switch (PortBType)
                    {
                        case ESignedness.Signed:
                            {
                                Signed temp = A.Cur.UnsignedValue.SignedValue * B.Cur.SignedValue;
                                _pipeIn.Next = temp.SLVValue[OutputWidthHigh, OutputWidthLow];
                            }
                            break;

                        case ESignedness.Unsigned:
                            {
                                Unsigned temp = A.Cur.UnsignedValue * B.Cur.UnsignedValue;
                                _pipeIn.Next = temp.SLVValue[OutputWidthHigh, OutputWidthLow];
                            }
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                    break;

                default:
                    throw new NotImplementedException();
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
            AddProcess(Processing, A, B);
        }
    }

   public class XilinxMultiplierXILMapper : IXILMapper
   {
       public class CoreConfig
       {
           public CoreConfig()
           {
               PipeStageScaling = 1.0;
           }

           public double PipeStageScaling { get; set; }
       }

       public XilinxMultiplierXILMapper()
       {
           Config = new CoreConfig();
       }

       public CoreConfig Config { get; private set; }

       private class XILMapping : IXILMapping
       {
           private XilinxMultiplier _host;
           private bool _swap;

           public XILMapping(XilinxMultiplier host, bool swap)
           {
               _host = host;
               _swap = swap;
           }

           public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
           {
               int i0 = _swap ? 1 : 0;
               int i1 = _swap ? 0 : 1;
               return _host.TASite.Multiply(operands[i0], operands[i1], results[0]);
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
               get { return _host.PipeStages; }
           }

           public string Description
           {
               get
               {
                   return "Xilinx " + _host.PortAWidth + "/" + _host.PortBWidth + " bit, " +
                        _host.PipeStages + " stage integer multiplier";
               }
           }
       }

       public IEnumerable<XILInstr> GetSupportedInstructions()
       {
           yield return DefaultInstructionSet.Instance.Mul();
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
           XilinxMultiplier mul = fu as XilinxMultiplier;
           if (mul == null)
               return null;

           if (instr.Name != InstructionCodes.Mul)
               return null;

           if (mul.MultType != XilinxMultiplier.EMultiplierType.ParallelMultiplier)
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

           int expectedHigh = fmtr.IntWidth + fmta.FracWidth + fmtb.FracWidth - 1;
           int expectedLow = fmta.FracWidth + fmtb.FracWidth - fmtr.FracWidth;

           if (fmta.TotalWidth != mul.PortAWidth ||
               fmtb.TotalWidth != mul.PortBWidth ||
               expectedHigh != mul.OutputWidthHigh ||
               expectedLow != mul.OutputWidthLow)
               return null;

           return new XILMapping(mul, swap);
       }

       public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
       {
           var alt0 = TryMapOne(taSite, instr, operandTypes, resultTypes, false);
           var alt1 = TryMapOne(taSite, instr, new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
           if (alt0 != null)
               yield return alt0;
           if (alt1 != null)
               yield return alt1;
       }

       private int ComputeOptimalPipeStages(XilinxMultiplier mul)
       {
           // Assumption: Virtex6 device

           if (mul.PortAWidth <= 9 && mul.PortBWidth <= 9 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseLuts &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Area &&
               mul.ResultType == XilinxMultiplier.ESignedness.Unsigned)
               return 3;

           if (mul.PortAWidth <= 9 && mul.PortBWidth <= 9 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseLuts &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Speed &&
               mul.ResultType == XilinxMultiplier.ESignedness.Unsigned)
               return 4;

           if (mul.PortAWidth <= 12 && mul.PortBWidth <= 12 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseLuts &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Area &&
               mul.ResultType == XilinxMultiplier.ESignedness.Unsigned)
               return 3;

           if (mul.PortAWidth <= 12 && mul.PortBWidth <= 12 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseLuts &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Speed &&
               mul.ResultType == XilinxMultiplier.ESignedness.Unsigned)
               return 4;

           if (mul.PortAWidth <= 18 && mul.PortBWidth <= 18 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Speed &&
               mul.ResultType == XilinxMultiplier.ESignedness.Signed)
               return 3;

           if (mul.PortAWidth <= 20 && mul.PortBWidth <= 20 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Speed &&
               mul.ResultType == XilinxMultiplier.ESignedness.Signed)
               return 4;

           if (mul.PortAWidth <= 20 && mul.PortBWidth <= 20 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Area &&
               mul.ResultType == XilinxMultiplier.ESignedness.Signed)
               return 3;

           if (mul.PortAWidth <= 24 && mul.PortBWidth <= 24 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Speed &&
               mul.ResultType == XilinxMultiplier.ESignedness.Unsigned)
               return 4;

           if (mul.PortAWidth <= 24 && mul.PortBWidth <= 24 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Area &&
               mul.ResultType == XilinxMultiplier.ESignedness.Unsigned)
               return 5;

           if (mul.PortAWidth <= 25 && mul.PortBWidth <= 18 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.ResultType == XilinxMultiplier.ESignedness.Signed)
               return 3;

           if (mul.PortAWidth <= 18 && mul.PortBWidth <= 25 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.ResultType == XilinxMultiplier.ESignedness.Signed)
               return 3;

           if (mul.PortAWidth <= 35 && mul.PortBWidth <= 35 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Speed &&
               mul.ResultType == XilinxMultiplier.ESignedness.Signed)
               return 6;

           if (mul.PortAWidth <= 53 && mul.PortBWidth <= 53 &&
               mul.MultiplierConstruction == XilinxMultiplier.EConstruction.UseMults &&
               mul.OptGoal == XilinxMultiplier.EOptimizationGoal.Speed &&
               mul.ResultType == XilinxMultiplier.ESignedness.Unsigned)
               return 12;

           // don't know...
           return 12;
       }

       public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
       {
           if (instr.Name != InstructionCodes.Mul)
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

           int high = fmtr.IntWidth + fmta.FracWidth + fmtb.FracWidth - 1;
           int low = fmta.FracWidth + fmtb.FracWidth - fmtr.FracWidth;

           var xproj = proj as XilinxProject;
           XilinxMultiplier.EGenerator gen = XilinxMultiplier.EGenerator.Multiplier_11_2;
           switch (xproj.ISEVersion)
           {
               case EISEVersion._11_1:
               case EISEVersion._11_2:
               case EISEVersion._11_3:
               case EISEVersion._11_4:
               case EISEVersion._11_5:
                   gen = XilinxMultiplier.EGenerator.Multiplier_11_0;
                   break;
           }

           XilinxMultiplier mul = new XilinxMultiplier()
           {
               Generator = gen,
               HasCE = false,
               MultiplierConstruction = XilinxMultiplier.EConstruction.UseMults,
               MultType = XilinxMultiplier.EMultiplierType.ParallelMultiplier,
               OptGoal = XilinxMultiplier.EOptimizationGoal.Speed,
               UseCustomOutputWidth = true,
               OutputWidthHigh = high,
               OutputWidthLow = low,
               PortAType = fmta.IsSigned ? XilinxMultiplier.ESignedness.Signed : XilinxMultiplier.ESignedness.Unsigned,
               PortAWidth = fmta.TotalWidth,
               PortBType = fmtb.IsSigned ? XilinxMultiplier.ESignedness.Signed : XilinxMultiplier.ESignedness.Unsigned,
               PortBWidth = fmtb.TotalWidth,
               HasSCLR = false,
               UseRounding = false,
               ZeroDetect = false
           };
           int optStages = ComputeOptimalPipeStages(mul);
           {
               // debug only
               DesignContext.Push();
               var mul2 = new XilinxMultiplier()
               {
                   Generator = gen,
                   HasCE = false,
                   MultiplierConstruction = XilinxMultiplier.EConstruction.UseMults,
                   MultType = XilinxMultiplier.EMultiplierType.ParallelMultiplier,
                   OptGoal = XilinxMultiplier.EOptimizationGoal.Speed,
                   UseCustomOutputWidth = true,
                   OutputWidthHigh = high,
                   OutputWidthLow = low,
                   PortAType = fmtb.IsSigned ? XilinxMultiplier.ESignedness.Signed : XilinxMultiplier.ESignedness.Unsigned,
                   PortAWidth = fmtb.TotalWidth,
                   PortBType = fmta.IsSigned ? XilinxMultiplier.ESignedness.Signed : XilinxMultiplier.ESignedness.Unsigned,
                   PortBWidth = fmta.TotalWidth,
                   HasSCLR = false,
                   UseRounding = false,
                   ZeroDetect = false
               };
               Debug.Assert(optStages == ComputeOptimalPipeStages(mul2));
               DesignContext.Pop();
           }
           int scaledStages = (int)Math.Round(Config.PipeStageScaling * optStages);
           mul.PipeStages = scaledStages;
           return new XILMapping(mul, false);
       }
   }
}

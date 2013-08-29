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
    public interface ICordicTransactionSite :
        ITransactionSite
    {
        IEnumerable<TAVerb> Rotate(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y,
            ISignalSource<StdLogicVector> ang, ISignalSink<StdLogicVector> xOut, ISignalSink<StdLogicVector> yOut);
        IEnumerable<TAVerb> Translate(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y,
            ISignalSink<StdLogicVector> mag, ISignalSink<StdLogicVector> ang);
        IEnumerable<TAVerb> SinCos(ISignalSource<StdLogicVector> ang,
            ISignalSink<StdLogicVector> cosOut, ISignalSink<StdLogicVector> sinOut);
        IEnumerable<TAVerb> SinhCosh(ISignalSource<StdLogicVector> ang,
            ISignalSink<StdLogicVector> coshOut, ISignalSink<StdLogicVector> sinhOut);
        IEnumerable<TAVerb> Atan(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y,
            ISignalSink<StdLogicVector> ang);
        IEnumerable<TAVerb> Atanh(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y,
            ISignalSink<StdLogicVector> ang);
        IEnumerable<TAVerb> Sqrt(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> sqrt);
    }

    [DeclareXILMapper(typeof(CordicXILMapper))]
    public class XilinxCordic : Component
    {
        private class CordicTransactionSite :
            DefaultTransactionSite,
            ICordicTransactionSite
        {
            private XilinxCordic _host;

            public CordicTransactionSite(XilinxCordic host) :
                base(host)
            {
                _host = host;
            }

            public override void Establish(IAutoBinder binder)
            {
                var cordic = _host;
                cordic.Clk = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, StdLogic._0);
                if (cordic.HasCE)
                    cordic.CE = binder.GetSignal<StdLogic>(EPortUsage.Default, "CE", null, StdLogic._0);
                if (cordic.HasND)
                    cordic.ND = binder.GetSignal<StdLogic>(EPortUsage.Default, "ND", null, StdLogic._0);
                if (cordic.HasPhaseIn)
                    cordic.Phase_In = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "PhaseIn", null, StdLogicVector._0s(cordic.InputWidth));
                if (cordic.HasPhaseOut)
                    cordic.Phase_Out = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "PhaseOut", null, StdLogicVector._0s(cordic.OutputWidth));
                if (cordic.HasRdy)
                    cordic.RDY = binder.GetSignal<StdLogic>(EPortUsage.Default, "Rdy", null, StdLogic._0);
                if (cordic.HasSCLR)
                    cordic.SCLR = binder.GetSignal<StdLogic>(EPortUsage.Default, "SCLR", null, StdLogic._0);
                if (cordic.HasXIn)
                    cordic.X_In = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "XIn", null, StdLogicVector._0s(cordic.InputWidth));
                if (cordic.HasXOut)
                    cordic.X_Out = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "XOut", null, StdLogicVector._0s(cordic.OutputWidth));
                if (cordic.HasYIn)
                    cordic.Y_In = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "YIn", null, StdLogicVector._0s(cordic.InputWidth));
                if (cordic.HasYOut)
                    cordic.Y_Out = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "YOut", null, StdLogicVector._0s(cordic.OutputWidth));

            }

            private IProcess AddCtrlPins(IProcess process, bool nd)
            {
                if (_host.HasCE)
                    process = process.Par(_host.CE.Dual.Drive(SignalSource.Create(StdLogic._1)));
                if (_host.HasND)
                    process = process.Par(_host.ND.Dual.Drive(SignalSource.Create(nd ? StdLogic._1 : StdLogic._0)));
                if (_host.HasSCLR)
                    process = process.Par(_host.SCLR.Dual.Drive(SignalSource.Create(StdLogic._0)));
                return process;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                switch (_host.ArchitecturalConfiguration)
                {
                    case EArchitecturalConfiguration.Parallel:
                        {
                            IProcess during;
                            ISignalSource<StdLogicVector> z = SignalSource.Create(StdLogicVector.DCs(_host.InputWidth));
                            switch (_host.FunctionalSelection)
                            {
                                case EFunctionalSelection.Arctan:
                                case EFunctionalSelection.Arctanh:
                                    during = _host.X_In.Dual.Drive(z).Par(
                                        _host.Y_In.Dual.Drive(z));
                                    break;

                                case EFunctionalSelection.Rotate:
                                    during = _host.X_In.Dual.Drive(z).Par(
                                        _host.Y_In.Dual.Drive(z)).Par(
                                        _host.Phase_In.Dual.Drive(z));
                                    break;

                                case EFunctionalSelection.SinAndCos:
                                case EFunctionalSelection.SinhAndCosh:
                                    during = _host.Phase_In.Dual.Drive(z);
                                    break;

                                case EFunctionalSelection.Sqrt:
                                    during = _host.X_In.Dual.Drive(z);
                                    break;

                                case EFunctionalSelection.Translate:
                                    during = _host.X_In.Dual.Drive(z).Par(
                                        _host.Phase_In.Dual.Drive(z));
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }
                            during = AddCtrlPins(during, false);

                            yield return Verb(ETVMode.Locked, during);
                        }
                        break;
                }
            }

            private IEnumerable<TAVerb> AwaitResult()
            {
                switch (_host.ArchitecturalConfiguration)
                {
                    case EArchitecturalConfiguration.Parallel:
                        for (int i = 1; i < _host.Latency; i++)
                            yield return Verb(
                                _host.ArchitecturalConfiguration == EArchitecturalConfiguration.Parallel ? 
                                ETVMode.Shared : ETVMode.Locked);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            #region ICordicTransactionSite Member

            public IEnumerable<TAVerb> Rotate(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y, ISignalSource<StdLogicVector> ang, ISignalSink<StdLogicVector> xOut, ISignalSink<StdLogicVector> yOut)
            {
                if (_host.FunctionalSelection != EFunctionalSelection.Rotate)
                    throw new NotSupportedException();

                IProcess during =
                    _host.X_In.Dual.Drive(x).Par(
                    _host.Y_In.Dual.Drive(y)).Par(
                    _host.Phase_In.Dual.Drive(ang));
                during = AddCtrlPins(during, true);
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        during,
                        xOut.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        yOut.Comb.Connect(_host.Y_Out.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, during);
                    foreach (TAVerb verb in AwaitResult())
                        yield return verb;
                    yield return Verb(ETVMode.Shared,
                        xOut.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        yOut.Comb.Connect(_host.Y_Out.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Translate(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y, ISignalSink<StdLogicVector> mag, ISignalSink<StdLogicVector> ang)
            {
                if (_host.FunctionalSelection != EFunctionalSelection.Translate)
                    throw new NotSupportedException();

                IProcess during =
                    _host.X_In.Dual.Drive(x).Par(
                    _host.Y_In.Dual.Drive(y));
                during = AddCtrlPins(during, true);
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        during,
                        mag.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        ang.Comb.Connect(_host.Phase_Out.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, during);
                    foreach (TAVerb verb in AwaitResult())
                        yield return verb;
                    yield return Verb(ETVMode.Shared,
                        mag.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        ang.Comb.Connect(_host.Phase_Out.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> SinCos(ISignalSource<StdLogicVector> ang, ISignalSink<StdLogicVector> cosOut, ISignalSink<StdLogicVector> sinOut)
            {
                if (_host.FunctionalSelection != EFunctionalSelection.SinAndCos)
                    throw new NotSupportedException();

                IProcess during = _host.Phase_In.Dual.Drive(ang);
                during = AddCtrlPins(during, true);
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        during,
                        cosOut.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        sinOut.Comb.Connect(_host.Y_Out.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, during);
                    foreach (TAVerb verb in AwaitResult())
                        yield return verb;
                    yield return Verb(ETVMode.Shared,
                        cosOut.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        sinOut.Comb.Connect(_host.Y_Out.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> SinhCosh(ISignalSource<StdLogicVector> ang, ISignalSink<StdLogicVector> coshOut, ISignalSink<StdLogicVector> sinhOut)
            {
                if (_host.FunctionalSelection != EFunctionalSelection.SinhAndCosh)
                    throw new NotSupportedException();

                IProcess during = _host.Phase_In.Dual.Drive(ang);
                during = AddCtrlPins(during, true);
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        during,
                        coshOut.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        sinhOut.Comb.Connect(_host.Y_Out.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, during);
                    foreach (TAVerb verb in AwaitResult())
                        yield return verb;
                    yield return Verb(ETVMode.Shared,
                        coshOut.Comb.Connect(_host.X_Out.Dual.AsSignalSource()),
                        sinhOut.Comb.Connect(_host.Y_Out.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Atan(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y, ISignalSink<StdLogicVector> ang)
            {
                if (_host.FunctionalSelection != EFunctionalSelection.Arctan)
                    throw new NotSupportedException();

                IProcess during = 
                    _host.X_In.Dual.Drive(x).Par(
                    _host.Y_In.Dual.Drive(y));
                during = AddCtrlPins(during, true);
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        during,
                        ang.Comb.Connect(_host.Phase_Out.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, during);
                    foreach (TAVerb verb in AwaitResult())
                        yield return verb;
                    yield return Verb(ETVMode.Shared,
                        ang.Comb.Connect(_host.Phase_Out.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Atanh(ISignalSource<StdLogicVector> x, ISignalSource<StdLogicVector> y, ISignalSink<StdLogicVector> ang)
            {
                if (_host.FunctionalSelection != EFunctionalSelection.Arctanh)
                    throw new NotSupportedException();

                IProcess during =
                    _host.X_In.Dual.Drive(x).Par(
                    _host.Y_In.Dual.Drive(y));
                during = AddCtrlPins(during, true);
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        during,
                        ang.Comb.Connect(_host.Phase_Out.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, during);
                    foreach (TAVerb verb in AwaitResult())
                        yield return verb;
                    yield return Verb(ETVMode.Shared,
                        ang.Comb.Connect(_host.Phase_Out.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Sqrt(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> sqrt)
            {
                if (_host.FunctionalSelection != EFunctionalSelection.Sqrt)
                    throw new NotSupportedException();

                IProcess during = _host.X_In.Dual.Drive(x);
                during = AddCtrlPins(during, true);
                if (_host.Latency == 0)
                {
                    yield return Verb(ETVMode.Locked,
                        during,
                        sqrt.Comb.Connect(_host.X_Out.Dual.AsSignalSource()));
                }
                else
                {
                    yield return Verb(ETVMode.Locked, during);
                    foreach (TAVerb verb in AwaitResult())
                        yield return verb;
                    yield return Verb(ETVMode.Shared,
                        sqrt.Comb.Connect(_host.X_Out.Dual.AsSignalSource()));
                }
            }

            #endregion
        }

        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "CORDIC family Xilinx,_Inc. 4.0")]
            CORDIC_4_0,
        }

        public enum EArchitecturalConfiguration
        {
            [PropID(EPropAssoc.CoreGen, "Parallel")]
            Parallel,

            [PropID(EPropAssoc.CoreGen, "Word_Serial")]
            WordSerial
        }

        public enum EScaleCompensation
        {
            [PropID(EPropAssoc.CoreGen, "No_Scale_Compensation")]
            NoScaleCompensation,

            [PropID(EPropAssoc.CoreGen, "LUT_based")]
            LUT_based,

            [PropID(EPropAssoc.CoreGen, "BRAM")]
            BRAM,

            [PropID(EPropAssoc.CoreGen, "Embedded_Multiplier")]
            EmbeddedMultiplier
        }

        public enum EFunctionalSelection
        {          
            [PropID(EPropAssoc.CoreGen, "Rotate")]
            Rotate,

            [PropID(EPropAssoc.CoreGen, "Translate")]
            Translate,

            [PropID(EPropAssoc.CoreGen, "Sin_and_Cos")]
            SinAndCos,

            [PropID(EPropAssoc.CoreGen, "Sinh_and_Cosh")]
            SinhAndCosh,

            [PropID(EPropAssoc.CoreGen, "Arctan")]
            Arctan,

            [PropID(EPropAssoc.CoreGen, "Arctanh")]
            Arctanh,

            [PropID(EPropAssoc.CoreGen, "Square_Root")]
            Sqrt
        }
            
        public enum EPhaseFormat
        {
            [PropID(EPropAssoc.CoreGen, "Radians")]
            Radians,

            [PropID(EPropAssoc.CoreGen, "Scaled_Radians")]
            ScaledRadians
        }

        public enum EDataFormat
        {
            [PropID(EPropAssoc.CoreGen, "SignedFraction")]
            SignedFraction,

            [PropID(EPropAssoc.CoreGen, "UnsignedFraction")]
            UnsignedFraction,

            [PropID(EPropAssoc.CoreGen, "UnsignedInteger")]
            UnsignedInteger
        }

        public enum EPipeliningMode
        {
            [PropID(EPropAssoc.CoreGen, "No_Pipelining")]
            No_Pipelining,

            [PropID(EPropAssoc.CoreGen, "Optimal")]
            Optimal,

            [PropID(EPropAssoc.CoreGen, "Maximum")]
            Maximum
        }

        public enum ERoundingMode
        {
            [PropID(EPropAssoc.CoreGen, "Truncate")]
            Truncate,

            [PropID(EPropAssoc.CoreGen, "Round_Pos_Inf")]
            RoundPosInf,

            [PropID(EPropAssoc.CoreGen, "Round_Pos_Neg_Inf")]
            RoundPosNegInf,

            [PropID(EPropAssoc.CoreGen, "Nearest_Even")]
            NearestEven
        }

        public In<StdLogicVector> X_In { private get; set; }
        public In<StdLogicVector> Y_In { private get; set; }
        public In<StdLogicVector> Phase_In { private get; set; }
        public In<StdLogic> Clk { private get; set; }
        public In<StdLogic> ND { private get; set; }
        public In<StdLogic> CE { private get; set; }
        public In<StdLogic> SCLR { private get; set; }
        public Out<StdLogicVector> X_Out { private get; set; }
        public Out<StdLogicVector> Y_Out { private get; set; }
        public Out<StdLogicVector> Phase_Out { private get; set; }
        public Out<StdLogic> RFD { private get; set; }
        public Out<StdLogic> RDY { private get; set; }

        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "architectural_configuration")]
        [PerformanceRelevant]
        public EArchitecturalConfiguration ArchitecturalConfiguration { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "ce")]
        [PerformanceRelevant]
        public bool HasCE { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "coarse_rotation")]
        [PerformanceRelevant]
        public bool CoarseRotation { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "compensation_scaling")]
        [PerformanceRelevant]
        public EScaleCompensation CompensationScaling { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "functional_selection")]
        [PerformanceRelevant]
        public EFunctionalSelection FunctionalSelection { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "data_format")]
        [PerformanceRelevant]
        public EDataFormat DataFormat { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "input_width")]
        [PerformanceRelevant]
        public int InputWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "iterations")]
        [PerformanceRelevant]
        public int Iterations { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "nd")]
        [PerformanceRelevant]
        public bool HasND { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "output_width")]
        [PerformanceRelevant]
        public int OutputWidth { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_format")]
        [PerformanceRelevant]
        public EPhaseFormat PhaseFormat { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "phase_output")]
        [PerformanceRelevant]
        public bool HasPhaseOut
        {
            get
            {
                switch (FunctionalSelection)
                {
                    case EFunctionalSelection.Translate:
                    case EFunctionalSelection.Arctan:
                    case EFunctionalSelection.Arctanh:
                        return true;

                    default:
                        return false;
                }
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "pipelining_mode")]
        [PerformanceRelevant]
        public EPipeliningMode PipeliningMode { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "precision")]
        [PerformanceRelevant]
        public int Precision { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "rdy")]
        [PerformanceRelevant]
        public bool HasRdy { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_inputs")]
        [PerformanceRelevant]
        public bool RegisterInputs { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "register_outputs")]
        [PerformanceRelevant]
        public bool RegisterOutputs { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        public string ComponentName { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "round_mode")]
        [PerformanceRelevant]
        public ERoundingMode RoundMode { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "sclr")]
        [PerformanceRelevant]
        public bool HasSCLR { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "x_out")]
        public bool HasXOut
        {
            get
            {
                switch (FunctionalSelection)
                {
                    case EFunctionalSelection.Rotate:
                    case EFunctionalSelection.SinAndCos:
                    case EFunctionalSelection.SinhAndCosh:
                    case EFunctionalSelection.Sqrt:
                    case EFunctionalSelection.Translate:
                        return true;

                    default:
                        return false;
                }
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "y_out")]
        public bool HasYOut
        {
            get
            {
                switch (FunctionalSelection)
                {
                    case EFunctionalSelection.Rotate:
                    case EFunctionalSelection.SinAndCos:
                    case EFunctionalSelection.SinhAndCosh:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public int Latency
        {
            get 
            {
                int baseLat;
                switch (PipeliningMode)
                {
                    case EPipeliningMode.No_Pipelining:
                        baseLat = 0;
                        break;

                    case EPipeliningMode.Maximum:
                    case EPipeliningMode.Optimal:
                        if (Iterations != 0)
                        {
                            baseLat = Iterations;
                            if (CoarseRotation)
                                baseLat += 2;
                        }
                        else
                        {
                            baseLat = OutputWidth;
                        }

                        switch (FunctionalSelection)
                        {
                            case EFunctionalSelection.Rotate:
                            case EFunctionalSelection.Translate:
                                switch (CompensationScaling)
                                {
                                    case EScaleCompensation.NoScaleCompensation:
                                        baseLat += 3;
                                        break;

                                    case EScaleCompensation.LUT_based:
                                        if (OutputWidth == 8)
                                            baseLat += 5;
                                        else if (OutputWidth <= 19)
                                            baseLat += 6;
                                        else
                                            baseLat += 7;
                                        break;

                                    case EScaleCompensation.BRAM:
                                        if (OutputWidth <= 14)
                                            baseLat += 7;
                                        else if (OutputWidth <= 31)
                                            baseLat += 8;
                                        else
                                            baseLat += 9;
                                        break;

                                    case EScaleCompensation.EmbeddedMultiplier:
                                        if (OutputWidth <= 17)
                                            baseLat += 6;
                                        else if (OutputWidth <= 23)
                                            baseLat += 7;
                                        else if (OutputWidth <= 32)
                                            baseLat += 9;
                                        else if (OutputWidth <= 36)
                                            baseLat += 10;
                                        else if (OutputWidth == 37)
                                            baseLat += 14;
                                        else if (OutputWidth <= 42)
                                            baseLat += 11;
                                        else
                                            baseLat += 14;
                                        break;

                                    default:
                                        throw new NotImplementedException();
                                }
                                break;

                            case EFunctionalSelection.SinAndCos:
                            case EFunctionalSelection.Arctan:
                                baseLat += 3;
                                break;

                            case EFunctionalSelection.SinhAndCosh:
                            case EFunctionalSelection.Arctanh:
                                if (OutputWidth <= 12)
                                    baseLat += 4;
                                else
                                    baseLat += 5;
                                break;

                            case EFunctionalSelection.Sqrt:
                                switch (PipeliningMode)
                                {
                                    case EPipeliningMode.Optimal:
                                        baseLat = (OutputWidth + 1) / 2;
                                        break;

                                    case EPipeliningMode.Maximum:
                                        baseLat--;
                                        break;

                                    default:
                                        throw new NotImplementedException();
                                }
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }
                if (FunctionalSelection != EFunctionalSelection.Sqrt &&
                    ArchitecturalConfiguration == EArchitecturalConfiguration.WordSerial)
                    baseLat += 2;

                if (RegisterInputs)
                    baseLat++;

                if (RegisterOutputs && 
                    PipeliningMode == EPipeliningMode.No_Pipelining)
                    baseLat++;

                return baseLat;
            }
        }

        public int InitiationInterval
        {
            get
            {
                switch (ArchitecturalConfiguration)
                {
                    case EArchitecturalConfiguration.Parallel:
                        return 1;

                    case EArchitecturalConfiguration.WordSerial:
                        return Latency;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public bool HasXIn
        {
            get
            {
                switch (FunctionalSelection)
                {
                    case EFunctionalSelection.Rotate:
                    case EFunctionalSelection.Translate:
                    case EFunctionalSelection.Arctan:
                    case EFunctionalSelection.Arctanh:
                    case EFunctionalSelection.Sqrt:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public bool HasYIn
        {
            get
            {
                switch (FunctionalSelection)
                {
                    case EFunctionalSelection.Rotate:
                    case EFunctionalSelection.Translate:
                    case EFunctionalSelection.Arctan:
                    case EFunctionalSelection.Arctanh:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public bool HasPhaseIn
        {
            get
            {
                switch (FunctionalSelection)
                {
                    case EFunctionalSelection.Rotate:
                    case EFunctionalSelection.SinAndCos:
                    case EFunctionalSelection.SinhAndCosh:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public ICordicTransactionSite TASite { get; private set; }

        private RegPipe _xpipe;
        private RegPipe _ypipe;
        private RegPipe _ppipe;
        private SLVSignal _xr, _yr, _pr;

        public XilinxCordic()
        {
            Generator = EGenerator.CORDIC_4_0;
            ArchitecturalConfiguration = EArchitecturalConfiguration.Parallel;
            HasCE = false;
            CoarseRotation = false;
            CompensationScaling = EScaleCompensation.NoScaleCompensation;
            DataFormat = EDataFormat.SignedFraction;
            FunctionalSelection = EFunctionalSelection.Rotate;
            InputWidth = 10;
            Iterations = 0;
            HasND = false;
            OutputWidth = 10;
            PhaseFormat = EPhaseFormat.Radians;
            PipeliningMode = EPipeliningMode.Maximum;
            Precision = 0;
            HasRdy = true; ;
            RegisterInputs = true;
            RegisterOutputs = true;
            RoundMode = ERoundingMode.Truncate;
            HasSCLR = false;
            TASite = new CordicTransactionSite(this);
        }

        protected override void PreInitialize()
        {
            _yr = new SLVSignal(OutputWidth)
            {
                InitialValue = StdLogicVector._0s(OutputWidth)
            };
            _pr = new SLVSignal(OutputWidth)
            {
                InitialValue = StdLogicVector._0s(OutputWidth)
            };
            if (HasXOut)
            {
                _xr = new SLVSignal(OutputWidth)
                {
                    InitialValue = StdLogicVector._0s(OutputWidth)
                };
                _xpipe = new RegPipe(Latency, OutputWidth);
                Bind(() =>
                {
                    _xpipe.Clk = Clk;
                    _xpipe.Din = _xr;
                    _xpipe.Dout = X_Out;
                });
            }
            if (HasYOut)
            {
                _yr = new SLVSignal(OutputWidth)
                {
                    InitialValue = StdLogicVector._0s(OutputWidth)
                };
                _ypipe = new RegPipe(Latency, OutputWidth);
                Bind(() =>
                {
                    _ypipe.Clk = Clk;
                    _ypipe.Din = _yr;
                    _ypipe.Dout = Y_Out;
                });
            }
            if (HasPhaseOut)
            {
                _pr = new SLVSignal(OutputWidth)
                {
                    InitialValue = StdLogicVector._0s(OutputWidth)
                };
                _ppipe = new RegPipe(Latency, OutputWidth);
                Bind(() =>
                {
                    _ppipe.Clk = Clk;
                    _ppipe.Din = _pr;
                    _ppipe.Dout = Phase_Out;
                });
            }
        }

        [DoNotAnalyze]
        private void Processing()
        {
            switch (FunctionalSelection)
            {
                case EFunctionalSelection.Rotate:
                    {
                        SFix x = SFix.FromSigned(X_In.Cur.SignedValue, InputWidth - 2);
                        double xd = x.DoubleValue;

                        SFix y = SFix.FromSigned(Y_In.Cur.SignedValue, InputWidth - 2);
                        double yd = y.DoubleValue;

                        SFix w = SFix.FromSigned(Phase_In.Cur.SignedValue, InputWidth - 3); 
                        double wd = w.DoubleValue;
                        if (PhaseFormat == EPhaseFormat.ScaledRadians)
                            wd *= Math.PI;

                        double x_out = (Math.Cos(wd)) * xd - (Math.Sin(wd)) * yd;
                        double y_out = (Math.Cos(wd)) * xd + (Math.Sin(wd)) * yd;

                        SFix xout = SFix.FromDouble(x_out, 2, OutputWidth - 2);                       
                        _xr.Next = xout.SignedValue.SLVValue;

                        SFix yout = SFix.FromDouble(y_out, 2, OutputWidth - 2);
                        _yr.Next = yout.SignedValue.SLVValue;
                    }
                    break;

                case EFunctionalSelection.Translate:
                    {
                        SFix x = SFix.FromSigned(X_In.Cur.SignedValue, InputWidth - 2);
                        double xd = x.DoubleValue;

                        SFix y = SFix.FromSigned(Y_In.Cur.SignedValue, InputWidth - 2);
                        double yd = y.DoubleValue;

                        double x_ou1 = xd*xd + yd*yd;
                        double x_ou2 = Math.Sqrt(x_ou1);

                        double w_ou = Math.Atan2(xd, yd);

                        SFix xout = SFix.FromDouble(x_ou2, 2, OutputWidth - 2);
                        _xr.Next = xout.SignedValue.SLVValue;

                        SFix wout = SFix.FromDouble(w_ou, 3, OutputWidth - 3);
                        _pr.Next = wout.SignedValue.SLVValue;
                    }
                    break;

                case EFunctionalSelection.SinAndCos:
                    {
                        SFix w = SFix.FromSigned(Phase_In.Cur.SignedValue, InputWidth - 3);
                        double wd = w.DoubleValue;

                        double x_ou = Math.Cos(wd);
                        double y_ou = Math.Sin(wd);

                        SFix xout = SFix.FromDouble(x_ou, 2, OutputWidth - 2);
                        _xr.Next = xout.SignedValue.SLVValue;

                        SFix yout = SFix.FromDouble(y_ou, 2, OutputWidth - 2);
                        _yr.Next = yout.SignedValue.SLVValue;
                    }
                    break;

                case EFunctionalSelection.SinhAndCosh:
                    {
                        SFix w = SFix.FromSigned(Phase_In.Cur.SignedValue, InputWidth - 3);                        
                        double wd = w.DoubleValue;

                        double x_ou = Math.Cosh(wd);
                        double y_ou = Math.Sinh(wd);

                        SFix xout = SFix.FromDouble(x_ou, 2, OutputWidth - 2);
                        _xr.Next = xout.SignedValue.SLVValue;

                        SFix yout = SFix.FromDouble(y_ou, 2, OutputWidth - 2);
                        _yr.Next = yout.SignedValue.SLVValue;
                    }
                    break;

                case EFunctionalSelection.Arctan:
                    {
                        SFix x = SFix.FromSigned(X_In.Cur.SignedValue, InputWidth - 2);
                        double xd = x.DoubleValue;

                        SFix y = SFix.FromSigned(Y_In.Cur.SignedValue, InputWidth - 2);
                        double yd = y.DoubleValue;
                       
                        double w_ou = Math.Atan2(xd, yd);

                        SFix wout = SFix.FromDouble(w_ou, 3, OutputWidth - 3);
                        _pr.Next = wout.SignedValue.SLVValue;
                    }
                    break;

                case EFunctionalSelection.Arctanh:
                    {
                        SFix x = SFix.FromSigned(X_In.Cur.SignedValue, InputWidth - 2);
                        double xd = x.DoubleValue;
                        double w_ou = 0.5 * Math.Log((xd + 1.0) / (xd - 1.0));

                        SFix wout = SFix.FromDouble(w_ou, 3, OutputWidth - 3);
                        _pr.Next = wout.SignedValue.SLVValue;
                    }
                    break;

                case EFunctionalSelection.Sqrt:
                    {
                        double xd;
                        UFix x;

                        switch (DataFormat)
                        {
                            case EDataFormat.UnsignedFraction:
                                x = UFix.FromUnsigned(X_In.Cur.UnsignedValue, InputWidth - 2);
                                xd = x.DoubleValue;
                                break;

                            case EDataFormat.UnsignedInteger:
                                x = UFix.FromUnsigned(X_In.Cur.UnsignedValue, 0);
                                xd = x.DoubleValue;
                                break;

                            default:
                                throw new NotSupportedException();
                        }

                        double w_ou = Math.Sqrt(xd);
                        UFix wout;

                        switch (DataFormat)
                        {
                            case EDataFormat.UnsignedFraction:
                                wout = UFix.FromDouble(w_ou, 2, OutputWidth - 2);
                                break;

                            case EDataFormat.UnsignedInteger:
                                wout = UFix.FromDouble(w_ou, OutputWidth, 0);
                                break;

                            default:
                                throw new NotSupportedException();
                        }
                        _pr.Next = wout.UnsignedValue.SLVValue;
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
            AddProcess(Processing, Clk);
        }

        public override int GetBehaviorHashCode()
        {
            return this.CoarseRotation.GetHashCode() ^
                this.CompensationScaling.GetHashCode() ^
                this.DataFormat.GetHashCode() ^
                this.FunctionalSelection.GetHashCode() ^
                this.Generator.GetHashCode() ^
                this.HasCE.GetHashCode() ^
                this.HasND.GetHashCode() ^
                this.HasPhaseIn.GetHashCode() ^
                this.HasPhaseOut.GetHashCode() ^
                this.HasRdy.GetHashCode() ^
                this.HasSCLR.GetHashCode() ^
                this.HasXIn.GetHashCode() ^
                this.HasXOut.GetHashCode() ^
                this.HasYIn.GetHashCode() ^
                this.HasYOut.GetHashCode() ^
                this.InitiationInterval.GetHashCode() ^
                this.InputWidth.GetHashCode() ^
                this.Iterations.GetHashCode() ^
                this.Latency.GetHashCode() ^
                this.OutputWidth.GetHashCode() ^
                this.PhaseFormat.GetHashCode() ^
                this.PipeliningMode.GetHashCode() ^
                this.Precision.GetHashCode() ^
                this.RegisterInputs.GetHashCode() ^
                this.RegisterOutputs.GetHashCode() ^
                this.RoundMode.GetHashCode();
        }

        public override bool IsEquivalent(Component component)
        {
            var cordic = component as XilinxCordic;
            if (cordic == null)
                return false;

            return this.CoarseRotation == cordic.CoarseRotation &&
                this.CompensationScaling == cordic.CompensationScaling &&
                this.DataFormat == cordic.DataFormat &&
                this.FunctionalSelection == cordic.FunctionalSelection &&
                this.Generator == cordic.Generator &&
                this.HasCE == cordic.HasCE &&
                this.HasND == cordic.HasND &&
                this.HasPhaseIn == cordic.HasPhaseIn &&
                this.HasPhaseOut == cordic.HasPhaseOut &&
                this.HasRdy == cordic.HasRdy &&
                this.HasSCLR == cordic.HasSCLR &&
                this.HasXIn == cordic.HasXIn &&
                this.HasXOut == cordic.HasXOut &&
                this.HasYIn == cordic.HasYIn &&
                this.HasYOut == cordic.HasYOut &&
                this.InitiationInterval == cordic.InitiationInterval &&
                this.InputWidth == cordic.InputWidth &&
                this.Iterations == cordic.Iterations &&
                this.Latency == cordic.Latency &&
                this.OutputWidth == cordic.OutputWidth &&
                this.PhaseFormat == cordic.PhaseFormat &&
                this.PipeliningMode == cordic.PipeliningMode &&
                this.Precision == cordic.Precision &&
                this.RegisterInputs == cordic.RegisterInputs &&
                this.RegisterOutputs == cordic.RegisterOutputs &&
                this.RoundMode == cordic.RoundMode;
        }
    }

    public class CordicConfiguration
    {
        public XilinxCordic.EArchitecturalConfiguration ArchitecturalConfiguration { get; set; }
        public XilinxCordic.EPipeliningMode PipeliningMode { get; set; }
        public XilinxCordic.ERoundingMode RoundingMode { get; set; }
        public XilinxCordic.EScaleCompensation ScaleCompensation { get; set; }
        public bool CoarseRotation { get; set; }
        public bool HasCE { get; set; }
        public bool HasND { get; set; }
        public bool HasSCLR { get; set; }
        public bool HasRdy { get; set; }
        public bool RegisterInputs { get; set; }
        public bool RegisterOutputs { get; set; }
    }

    public class CordicConfigurator
    {
        private CacheDictionary<XilinxCordic.EFunctionalSelection, CordicConfiguration> _map;

        public CordicConfigurator()
        {
            _map = new CacheDictionary<XilinxCordic.EFunctionalSelection, CordicConfiguration>(CreateConfiguration);
        }

        private CordicConfiguration CreateConfiguration(XilinxCordic.EFunctionalSelection key)
        {
            return new CordicConfiguration();
        }

        public CordicConfiguration this[XilinxCordic.EFunctionalSelection key]
        {
            get { return _map[key]; }
        }
    }

    public class CordicXILMapper : IXILMapper
    {
        private class CordicXILMapping : IXILMapping
        {
            public enum ETrigSelection
            {
                SinOnly,
                CosOnly,
                Both
            }

            private XilinxCordic _host;
            private ETrigSelection _tsel;

            public CordicXILMapping(XilinxCordic host, ETrigSelection tsel)
            {
                _host = host;
                _tsel = tsel;
            }

            #region IXILMapping Member

            public EMappingKind ResourceKind
            {
                get { return EMappingKind.ReplicatableResource; }
            }

            public ITransactionSite TASite
            {
                get { return _host.TASite; }
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
                switch (_host.FunctionalSelection)
                {
                    case XilinxCordic.EFunctionalSelection.Arctan:
                        return _host.TASite.Atan(operands[0], operands[1], results[0]);

                    case XilinxCordic.EFunctionalSelection.Arctanh:
                        return _host.TASite.Atanh(operands[0], operands[1], results[0]);

                    case XilinxCordic.EFunctionalSelection.Rotate:
                        return _host.TASite.Rotate(operands[0], operands[1], operands[2], results[0], results[1]);

                    case XilinxCordic.EFunctionalSelection.SinAndCos:
                        switch (_tsel)
                        {
                            case ETrigSelection.Both:
                                return _host.TASite.SinCos(operands[0], results[0], results[1]);

                            case ETrigSelection.CosOnly:
                                return _host.TASite.SinCos(operands[0], results[0], SignalSink.Nil<StdLogicVector>());

                            case ETrigSelection.SinOnly:
                                return _host.TASite.SinCos(operands[0], SignalSink.Nil<StdLogicVector>(), results[0]);

                            default:
                                throw new NotImplementedException();
                        }

                    case XilinxCordic.EFunctionalSelection.SinhAndCosh:
                        switch (_tsel)
                        {
                            case ETrigSelection.Both:
                                return _host.TASite.SinhCosh(operands[0], results[0], results[1]);

                            case ETrigSelection.CosOnly:
                                return _host.TASite.SinhCosh(operands[0], results[0], SignalSink.Nil<StdLogicVector>());

                            case ETrigSelection.SinOnly:
                                return _host.TASite.SinCos(operands[0], SignalSink.Nil<StdLogicVector>(), results[0]);

                            default:
                                throw new NotImplementedException();
                        }

                    case XilinxCordic.EFunctionalSelection.Sqrt:
                        return _host.TASite.Sqrt(operands[0], results[0]);

                    case XilinxCordic.EFunctionalSelection.Translate:
                        return _host.TASite.Translate(operands[0], operands[1], results[0], results[1]);

                    default:
                        throw new NotImplementedException();
                }
            }

            public string Description
            {
                get
                {
                    string prefix = "Xilinx " + _host.InputWidth + " => " + _host.OutputWidth + " bit CORDIC ";
                    switch (_host.FunctionalSelection)
                    {
                        case XilinxCordic.EFunctionalSelection.Arctan:
                            return prefix + " arctan";

                        case XilinxCordic.EFunctionalSelection.Arctanh:
                            return prefix + " arctanh";

                        case XilinxCordic.EFunctionalSelection.Rotate:
                            return prefix + " rotate";

                        case XilinxCordic.EFunctionalSelection.SinAndCos:
                            switch (_tsel)
                            {
                                case ETrigSelection.Both:
                                    return prefix + " sin/cos";

                                case ETrigSelection.CosOnly:
                                    return prefix + " cos";

                                case ETrigSelection.SinOnly:
                                    return prefix + " sin";

                                default:
                                    throw new NotImplementedException();
                            }

                        case XilinxCordic.EFunctionalSelection.SinhAndCosh:
                            switch (_tsel)
                            {
                                case ETrigSelection.Both:
                                    return prefix + " sinh/cosh";

                                case ETrigSelection.CosOnly:
                                    return prefix + " cosh";

                                case ETrigSelection.SinOnly:
                                    return prefix + " sinh";

                                default:
                                    throw new NotImplementedException();
                            }

                        case XilinxCordic.EFunctionalSelection.Sqrt:
                            return prefix + " square-root";

                        case XilinxCordic.EFunctionalSelection.Translate:
                            return prefix + " translate";

                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            #endregion
        }

        public CordicXILMapper()
        {
            Config = new CordicConfigurator();

            Config[XilinxCordic.EFunctionalSelection.Arctan].ArchitecturalConfiguration = XilinxCordic.EArchitecturalConfiguration.Parallel;
            Config[XilinxCordic.EFunctionalSelection.Arctan].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
            Config[XilinxCordic.EFunctionalSelection.Arctan].RoundingMode = XilinxCordic.ERoundingMode.Truncate;
            Config[XilinxCordic.EFunctionalSelection.Arctan].ScaleCompensation = XilinxCordic.EScaleCompensation.NoScaleCompensation;
            Config[XilinxCordic.EFunctionalSelection.Arctan].CoarseRotation = true;
            Config[XilinxCordic.EFunctionalSelection.Arctan].RegisterInputs = true;
            Config[XilinxCordic.EFunctionalSelection.Arctan].RegisterOutputs = true;
            Config[XilinxCordic.EFunctionalSelection.Arctan].HasCE = false;
            Config[XilinxCordic.EFunctionalSelection.Arctan].HasND = true;
            Config[XilinxCordic.EFunctionalSelection.Arctan].HasRdy = false;
            Config[XilinxCordic.EFunctionalSelection.Arctan].HasSCLR = false;

            Config[XilinxCordic.EFunctionalSelection.Arctanh].ArchitecturalConfiguration = XilinxCordic.EArchitecturalConfiguration.Parallel;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].RoundingMode = XilinxCordic.ERoundingMode.Truncate;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].ScaleCompensation = XilinxCordic.EScaleCompensation.NoScaleCompensation;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].CoarseRotation = false;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].RegisterInputs = true;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].RegisterOutputs = true;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].HasCE = false;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].HasND = true;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].HasRdy = false;
            Config[XilinxCordic.EFunctionalSelection.Arctanh].HasSCLR = false;

            Config[XilinxCordic.EFunctionalSelection.Rotate].ArchitecturalConfiguration = XilinxCordic.EArchitecturalConfiguration.Parallel;
            Config[XilinxCordic.EFunctionalSelection.Rotate].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
            Config[XilinxCordic.EFunctionalSelection.Rotate].RoundingMode = XilinxCordic.ERoundingMode.Truncate;
            Config[XilinxCordic.EFunctionalSelection.Rotate].ScaleCompensation = XilinxCordic.EScaleCompensation.NoScaleCompensation;
            Config[XilinxCordic.EFunctionalSelection.Rotate].CoarseRotation = true;
            Config[XilinxCordic.EFunctionalSelection.Rotate].RegisterInputs = true;
            Config[XilinxCordic.EFunctionalSelection.Rotate].RegisterOutputs = true;
            Config[XilinxCordic.EFunctionalSelection.Rotate].HasCE = false;
            Config[XilinxCordic.EFunctionalSelection.Rotate].HasND = true;
            Config[XilinxCordic.EFunctionalSelection.Rotate].HasRdy = false;
            Config[XilinxCordic.EFunctionalSelection.Rotate].HasSCLR = false;

            Config[XilinxCordic.EFunctionalSelection.Translate].ArchitecturalConfiguration = XilinxCordic.EArchitecturalConfiguration.Parallel;
            Config[XilinxCordic.EFunctionalSelection.Translate].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
            Config[XilinxCordic.EFunctionalSelection.Translate].RoundingMode = XilinxCordic.ERoundingMode.Truncate;
            Config[XilinxCordic.EFunctionalSelection.Translate].ScaleCompensation = XilinxCordic.EScaleCompensation.NoScaleCompensation;
            Config[XilinxCordic.EFunctionalSelection.Translate].CoarseRotation = true;
            Config[XilinxCordic.EFunctionalSelection.Translate].RegisterInputs = true;
            Config[XilinxCordic.EFunctionalSelection.Translate].RegisterOutputs = true;
            Config[XilinxCordic.EFunctionalSelection.Translate].HasCE = false;
            Config[XilinxCordic.EFunctionalSelection.Translate].HasND = true;
            Config[XilinxCordic.EFunctionalSelection.Translate].HasRdy = false;
            Config[XilinxCordic.EFunctionalSelection.Translate].HasSCLR = false;

            Config[XilinxCordic.EFunctionalSelection.SinAndCos].ArchitecturalConfiguration = XilinxCordic.EArchitecturalConfiguration.Parallel;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].RoundingMode = XilinxCordic.ERoundingMode.Truncate;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].ScaleCompensation = XilinxCordic.EScaleCompensation.NoScaleCompensation;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].CoarseRotation = true;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].RegisterInputs = true;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].RegisterOutputs = true;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].HasCE = false;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].HasND = true;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].HasRdy = true;
            Config[XilinxCordic.EFunctionalSelection.SinAndCos].HasSCLR = false;

            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].ArchitecturalConfiguration = XilinxCordic.EArchitecturalConfiguration.Parallel;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].RoundingMode = XilinxCordic.ERoundingMode.Truncate;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].ScaleCompensation = XilinxCordic.EScaleCompensation.NoScaleCompensation;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].CoarseRotation = false;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].RegisterInputs = true;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].RegisterOutputs = true;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].HasCE = false;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].HasND = true;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].HasRdy = false;
            Config[XilinxCordic.EFunctionalSelection.SinhAndCosh].HasSCLR = false;

            Config[XilinxCordic.EFunctionalSelection.Sqrt].ArchitecturalConfiguration = XilinxCordic.EArchitecturalConfiguration.Parallel;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].PipeliningMode = XilinxCordic.EPipeliningMode.Optimal;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].RoundingMode = XilinxCordic.ERoundingMode.Truncate;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].ScaleCompensation = XilinxCordic.EScaleCompensation.NoScaleCompensation;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].CoarseRotation = false;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].RegisterInputs = true;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].RegisterOutputs = true;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].HasCE = false;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].HasND = true;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].HasRdy = false;
            Config[XilinxCordic.EFunctionalSelection.Sqrt].HasSCLR = false;
        }

        public CordicConfigurator Config { get; private set; }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Sin();
            yield return DefaultInstructionSet.Instance.ScSin();
            yield return DefaultInstructionSet.Instance.Cos();
            yield return DefaultInstructionSet.Instance.ScCos();
            yield return DefaultInstructionSet.Instance.SinCos();
            yield return DefaultInstructionSet.Instance.ScSinCos();
            yield return DefaultInstructionSet.Instance.Sqrt();
        }

        public IXILMapping TryMapOne(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            XilinxCordic cordic = fu as XilinxCordic;
            if (cordic == null)
                return null;

            switch (instr.Name)
            {
                case InstructionCodes.Sin:
                case InstructionCodes.Cos:
                case InstructionCodes.SinCos:
                    {
                        if (cordic.FunctionalSelection != XilinxCordic.EFunctionalSelection.SinAndCos)
                            return null;

                        if (cordic.PhaseFormat != XilinxCordic.EPhaseFormat.Radians)
                            return null;

                        TypeDescriptor infmt = TypeDescriptor.GetTypeOf(
                            SFix.FromDouble(0.0, 3, cordic.InputWidth - 3));
                        if (!operandTypes[0].Equals(infmt))
                            return null;

                        TypeDescriptor outfmt = TypeDescriptor.GetTypeOf(
                            SFix.FromDouble(0.0, 2, cordic.OutputWidth - 2));
                        if (!resultTypes[0].Equals(outfmt))
                            return null;

                        CordicXILMapping.ETrigSelection tsel;
                        switch (instr.Name)
                        {
                            case InstructionCodes.SinCos:
                                tsel = CordicXILMapping.ETrigSelection.Both;
                                break;
                            case InstructionCodes.Sin:
                                tsel = CordicXILMapping.ETrigSelection.SinOnly;
                                break;
                            case InstructionCodes.Cos:
                                tsel = CordicXILMapping.ETrigSelection.CosOnly;
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        return new CordicXILMapping(cordic, tsel);
                    }

                case InstructionCodes.ScSin:
                case InstructionCodes.ScCos:
                case InstructionCodes.ScSinCos:
                    {
                        if (cordic.FunctionalSelection != XilinxCordic.EFunctionalSelection.SinAndCos)
                            return null;

                        if (cordic.PhaseFormat != XilinxCordic.EPhaseFormat.ScaledRadians)
                            return null;

                        TypeDescriptor infmt = TypeDescriptor.GetTypeOf(
                            SFix.FromDouble(0.0, 3, cordic.InputWidth - 3));
                        if (!operandTypes[0].Equals(infmt))
                            return null;

                        TypeDescriptor outfmt = TypeDescriptor.GetTypeOf(
                            SFix.FromDouble(0.0, 2, cordic.OutputWidth - 2));
                        if (!resultTypes[0].Equals(outfmt))
                            return null;

                        CordicXILMapping.ETrigSelection tsel;
                        switch (instr.Name)
                        {
                            case InstructionCodes.ScSinCos:
                                tsel = CordicXILMapping.ETrigSelection.Both;
                                break;
                            case InstructionCodes.ScSin:
                                tsel = CordicXILMapping.ETrigSelection.SinOnly;
                                break;
                            case InstructionCodes.ScCos:
                                tsel = CordicXILMapping.ETrigSelection.CosOnly;
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        return new CordicXILMapping(cordic, tsel);
                    }

                case InstructionCodes.Sqrt:
                    {
                        if (cordic.FunctionalSelection != XilinxCordic.EFunctionalSelection.Sqrt)
                            return null;

                        switch (cordic.DataFormat)
                        {
                            case XilinxCordic.EDataFormat.UnsignedFraction:
                                {
                                    var infmt = operandTypes[0].GetFixFormat();
                                    var outfmt = resultTypes[0].GetFixFormat();
                                    if (infmt.IntWidth % 2 == 0)
                                        return null;

                                    if (outfmt.IntWidth != (infmt.IntWidth + 1) / 2)
                                        return null;

                                    if (cordic.InputWidth != infmt.TotalWidth ||
                                        cordic.OutputWidth != outfmt.TotalWidth)
                                        return null;
                                }
                                break;

                            case XilinxCordic.EDataFormat.UnsignedInteger:
                                {
                                    TypeDescriptor infmt = TypeDescriptor.GetTypeOf(
                                        Unsigned.FromUInt(0, cordic.InputWidth));
                                    if (!operandTypes[0].Equals(infmt))
                                        return null;

                                    TypeDescriptor outfmt = TypeDescriptor.GetTypeOf(
                                        Unsigned.FromUInt(0, cordic.OutputWidth));
                                    if (!resultTypes[0].Equals(outfmt))
                                        return null;
                                }
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        return new CordicXILMapping(cordic, CordicXILMapping.ETrigSelection.Both);
                    }

                default:
                    return null;
            }
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var mapping = TryMapOne(taSite, instr, operandTypes, resultTypes);
            if (mapping != null)
                yield return mapping;
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            XilinxCordic cordic = null;
            CordicXILMapping.ETrigSelection tsel = CordicXILMapping.ETrigSelection.Both;

            switch (instr.Name)
            {
                case InstructionCodes.Sin:
                case InstructionCodes.Cos:
                case InstructionCodes.SinCos:
                    {
                        if (!operandTypes[0].CILType.Equals(typeof(SFix)))
                            return null;

                        if (!resultTypes[0].CILType.Equals(typeof(SFix)))
                            return null;

                        if (!operandTypes[0].IsComplete || !resultTypes[0].IsComplete)
                            return null;

                        FixFormat infmt = (FixFormat)operandTypes[0].TypeParams[0];
                        if (infmt.IntWidth != 3)
                            return null;

                        FixFormat outfmt = (FixFormat)resultTypes[0].TypeParams[0];
                        if (outfmt.IntWidth != 2)
                            return null;

                        switch (instr.Name)
                        {
                            case InstructionCodes.SinCos:
                                tsel = CordicXILMapping.ETrigSelection.Both;
                                break;
                            case InstructionCodes.Sin:
                                tsel = CordicXILMapping.ETrigSelection.SinOnly;
                                break;
                            case InstructionCodes.Cos:
                                tsel = CordicXILMapping.ETrigSelection.CosOnly;
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        cordic = new XilinxCordic()
                        {
                            FunctionalSelection = XilinxCordic.EFunctionalSelection.SinAndCos,
                            DataFormat = XilinxCordic.EDataFormat.SignedFraction,
                            PhaseFormat = XilinxCordic.EPhaseFormat.Radians,
                            InputWidth = infmt.TotalWidth,
                            OutputWidth = outfmt.TotalWidth
                        };
                    }
                    break;

                case InstructionCodes.ScSin:
                case InstructionCodes.ScCos:
                case InstructionCodes.ScSinCos:
                    {
                        if (!operandTypes[0].CILType.Equals(typeof(SFix)))
                            return null;

                        if (!resultTypes[0].CILType.Equals(typeof(SFix)))
                            return null;

                        if (!operandTypes[0].IsComplete || !resultTypes[0].IsComplete)
                            return null;

                        FixFormat infmt = (FixFormat)operandTypes[0].TypeParams[0];
                        if (infmt.IntWidth != 3)
                            return null;

                        FixFormat outfmt = (FixFormat)resultTypes[0].TypeParams[0];
                        if (outfmt.IntWidth != 2)
                            return null;

                        switch (instr.Name)
                        {
                            case InstructionCodes.ScSinCos:
                                tsel = CordicXILMapping.ETrigSelection.Both;
                                break;
                            case InstructionCodes.ScSin:
                                tsel = CordicXILMapping.ETrigSelection.SinOnly;
                                break;
                            case InstructionCodes.ScCos:
                                tsel = CordicXILMapping.ETrigSelection.CosOnly;
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        cordic = new XilinxCordic()
                        {
                            FunctionalSelection = XilinxCordic.EFunctionalSelection.SinAndCos,
                            DataFormat = XilinxCordic.EDataFormat.SignedFraction,
                            PhaseFormat = XilinxCordic.EPhaseFormat.ScaledRadians,
                            InputWidth = infmt.TotalWidth,
                            OutputWidth = outfmt.TotalWidth
                        };
                    }
                    break;

                case InstructionCodes.Sqrt:
                    {
                        if (operandTypes[0].CILType.Equals(typeof(Unsigned)))
                        {
                            if (!resultTypes[0].CILType.Equals(typeof(Unsigned)))
                                return null;

                            if (!operandTypes[0].IsComplete || !resultTypes[0].IsComplete)
                                return null;

                            int inWidth = (int)operandTypes[0].TypeParams[0];
                            int outWidth = (int)resultTypes[0].TypeParams[0];

                            cordic = new XilinxCordic()
                            {
                                FunctionalSelection = XilinxCordic.EFunctionalSelection.Sqrt,
                                DataFormat = XilinxCordic.EDataFormat.UnsignedInteger,
                                InputWidth = inWidth,
                                OutputWidth = outWidth
                            };
                        }
                        else if (operandTypes[0].CILType.Equals(typeof(UFix)))
                        {
                            if (!resultTypes[0].CILType.Equals(typeof(UFix)))
                                return null;

                            if (!operandTypes[0].IsComplete || !resultTypes[0].IsComplete)
                                return null;

                            var infmt = operandTypes[0].GetFixFormat();
                            var outfmt = resultTypes[0].GetFixFormat();
                            if (infmt.IntWidth % 2 == 0)
                                return null;

                            if (outfmt.IntWidth != (infmt.IntWidth + 1) / 2)
                                return null;

                            cordic = new XilinxCordic()
                            {
                                FunctionalSelection = XilinxCordic.EFunctionalSelection.Sqrt,
                                DataFormat = XilinxCordic.EDataFormat.UnsignedFraction,
                                InputWidth = infmt.TotalWidth,
                                OutputWidth = outfmt.TotalWidth
                            };
                        }
                        else
                        {
                            return null;
                        }
                        break;
                    }

                default:
                    return null;
            }

            CordicConfiguration config = Config[cordic.FunctionalSelection];
            cordic.ArchitecturalConfiguration = config.ArchitecturalConfiguration;
            cordic.CoarseRotation = config.CoarseRotation;
            cordic.HasCE = config.HasCE;
            cordic.HasND = config.HasND;
            cordic.HasRdy = config.HasRdy;
            cordic.HasSCLR = config.HasSCLR;
            cordic.PipeliningMode = config.PipeliningMode;
            cordic.RegisterInputs = config.RegisterInputs;
            cordic.RegisterOutputs = config.RegisterOutputs;
            cordic.RoundMode = config.RoundingMode;
            cordic.CompensationScaling = config.ScaleCompensation;

            return new CordicXILMapping(cordic, tsel);
        }
    }
}

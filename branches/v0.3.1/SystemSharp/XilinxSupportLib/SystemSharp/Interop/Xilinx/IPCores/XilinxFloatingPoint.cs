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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Interop.Xilinx.CoreGen;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Interop.Xilinx.IPCores
{
    /// <summary>
    /// Transaction site interface of Xilinx floating-point core 
    /// </summary>
    public interface IFloatingPointCoreTransactionSite:
        ITransactionSite
    {
        /// <summary>
        /// Performs the pre-configured unary operation.
        /// </summary>
        /// <returns>transaction</returns>
        IEnumerable<TAVerb> DoUnOp(ISignalSource<StdLogicVector> operand,
            ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs the pre-configured binary operation.
        /// </summary>
        /// <returns>transaction</returns>
        IEnumerable<TAVerb> DoBinOp(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Suspends core operation for one clock step.
        /// </summary>
        /// <returns>pause transaction</returns>
        IEnumerable<TAVerb> Pause();

        /// <summary>
        /// Resets the core.
        /// </summary>
        /// <returns>reset transaction</returns>
        IEnumerable<TAVerb> Reset();

        /// <summary>
        /// Performs an addition.
        /// </summary>
        /// <returns>addition transaction</returns>
        IEnumerable<TAVerb> Add(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a subtraction.
        /// </summary>
        /// <returns>subtraction transaction</returns>
        IEnumerable<TAVerb> Sub(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a multiplication.
        /// </summary>
        /// <returns>multiplication transaction</returns>
        IEnumerable<TAVerb> Mul(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a division.
        /// </summary>
        /// <returns>division transaction</returns>
        IEnumerable<TAVerb> Div(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a square-root computation.
        /// </summary>
        /// <returns>square-root transaction</returns>
        IEnumerable<TAVerb> Sqrt(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a "less than" comparison.
        /// </summary>
        /// <returns>comparison transaction</returns>
        IEnumerable<TAVerb> IsLt(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a "less than or equal" comparison.
        /// </summary>
        /// <returns>comparison transaction</returns>
        IEnumerable<TAVerb> IsLte(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs an equality comparison.
        /// </summary>
        /// <returns>comparison transaction</returns>
        IEnumerable<TAVerb> IsEq(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs an inquality comparison.
        /// </summary>
        /// <returns>comparison transaction</returns>
        IEnumerable<TAVerb> IsNEq(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a "greater than or equal" comparison.
        /// </summary>
        /// <returns>comparison transaction</returns>
        IEnumerable<TAVerb> IsGte(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Performs a "greater than" comparison.
        /// </summary>
        /// <returns>comparison transaction</returns>
        IEnumerable<TAVerb> IsGt(ISignalSource<StdLogicVector> a,
            ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Converts a fixed-point number to floating-point.
        /// </summary>
        /// <returns>conversion transaction</returns>
        IEnumerable<TAVerb> Fix2Float(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Converts a floating-point number to fixed-point.
        /// </summary>
        /// <returns>conversion transaction</returns>
        IEnumerable<TAVerb> Float2Fix(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result);

        /// <summary>
        /// Converts between floating-point formats.
        /// </summary>
        /// <returns>conversion transaction</returns>
        IEnumerable<TAVerb> Float2Float(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result);
    }

    /// <summary>
    /// Models a Xilinx floating-point core.
    /// </summary>
    [DeclareXILMapper(typeof(FloatingPointXILMapper))]
    public class FloatingPointCore : FunctionalUnit
    {
        public enum EGenerator
        {
            [PropID(EPropAssoc.CoreGen, "Floating-point family Xilinx,_Inc. 5.0")]
            Floating_Point_5_0
        }

        public enum EFunction
        {
            [PropID(EPropAssoc.CoreGen, "Add_Subtract")]
            AddSubtract,

            [PropID(EPropAssoc.CoreGen, "Multiply")]
            Multiply,

            [PropID(EPropAssoc.CoreGen, "Divide")]
            Divide,

            [PropID(EPropAssoc.CoreGen, "Square_root")]
            SquareRoot,

            [PropID(EPropAssoc.CoreGen, "Compare")]
            Compare,

            [PropID(EPropAssoc.CoreGen, "Fixed_to_float")]
            FixedToFloat,

            [PropID(EPropAssoc.CoreGen, "Float_to_fixed")]
            FloatToFixed,

            [PropID(EPropAssoc.CoreGen, "Float_to_float")]
            FloatToFloat
        }

        public enum EAddSub
        {
            [PropID(EPropAssoc.CoreGen, "Both")]
            Both,

            [PropID(EPropAssoc.CoreGen, "Add")]
            Add,

            [PropID(EPropAssoc.CoreGen, "Subtract")]
            Subtract
        }

        public enum ECompareOp
        {
            [PropID(EPropAssoc.CoreGen, "Programmable")]
            Programmable,

            [PropID(EPropAssoc.CoreGen, "Equal")]
            Equal,

            [PropID(EPropAssoc.CoreGen, "Not_Equal")]
            NotEqual,

            [PropID(EPropAssoc.CoreGen, "Less_Than")]
            LessThan,

            [PropID(EPropAssoc.CoreGen, "Less_Than_Or_Equal")]
            LessThanOrEqual,

            [PropID(EPropAssoc.CoreGen, "Greater_Than")]
            GreaterThan,

            [PropID(EPropAssoc.CoreGen, "Greater_Than_Or_Equal")]
            GreaterThanOrEqual,

            [PropID(EPropAssoc.CoreGen, "Unordered")]
            Unordered,

            [PropID(EPropAssoc.CoreGen, "Condition_Code")]
            ConditionCode
        };

        public enum EPrecision
        {
            [PropID(EPropAssoc.CoreGen, "Single")]
            Single,

            [PropID(EPropAssoc.CoreGen, "Double")]
            Double,

            [PropID(EPropAssoc.CoreGen, "Int32")]
            Int32,

            [PropID(EPropAssoc.CoreGen, "Custom")]
            Custom
        }

        public enum EOptimization
        {
            [PropID(EPropAssoc.CoreGen, "LowLatency")]
            LowLatency,

            [PropID(EPropAssoc.CoreGen, "Speed_Optimized")]
            HighSpeed
        }

        public enum EDSP48EUsage
        {
            [PropID(EPropAssoc.CoreGen, "No_Usage")]
            NoUsage,

            [PropID(EPropAssoc.CoreGen, "Medium_Usage")]
            MediumUsage,

            [PropID(EPropAssoc.CoreGen, "Full_Usage")]
            FullUsage,

            [PropID(EPropAssoc.CoreGen, "Max_Usage")]
            MaxUsage
        }

        public enum EMultiplierUsage
        {
            LogicOnly,
            MULT18x18,
            DSP48,
            DSP48A,
            DSP48E
        }

        private class TransactionSite: 
            DefaultTransactionSite,
            IFloatingPointCoreTransactionSite
        {
            private FloatingPointCore _host;

            public TransactionSite(FloatingPointCore host):
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                IProcess pr = _host.A.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.OperandWidth)));
                if (_host.Arity > 1)
                    pr = pr.Par(_host.B.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.OperandWidth))));
                if ((_host.Function == EFunction.AddSubtract && _host.AddSubSel == EAddSub.Both) ||
                    (_host.Function == EFunction.Compare && _host.CompareSel == ECompareOp.Programmable))
                    pr = pr.Par(_host.Operation.Dual.Drive(SignalSource.Create<StdLogicVector>("000000")));
                if (_host.HasOperationND)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasSCLR)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasCE)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                yield return Verb(ETVMode.Locked, pr);
            }

            public IEnumerable<TAVerb> DoUnOp(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                if (_host.Arity != 1)
                    throw new InvalidOperationException();

                IProcess pr = _host.A.Dual.Drive(operand);
                if (_host.HasOperationND)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                if (_host.HasSCLR)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasCE)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                var rpr = result.Comb.Connect(_host.Result.Dual.AsSignalSource());
                if (_host.Latency == 0)
                {
                    pr = pr.Par(rpr);
                    yield return Verb(ETVMode.Locked, pr);
                }
                else
                {
                    for (int i = 0; i < _host.CyclesPerOperation; i++)
                        yield return Verb(ETVMode.Locked, pr);
                    for (int i = _host.CyclesPerOperation; i < _host.Latency; i++)
                        yield return Verb(ETVMode.Shared);
                    yield return Verb(ETVMode.Shared, rpr);
                }
            }

            private IEnumerable<TAVerb> DoBinOp(StdLogicVector op, ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Arity != 2)
                    throw new InvalidOperationException();

                IProcess pr = _host.A.Dual.Drive(a)
                    .Par(_host.B.Dual.Drive(b));
                if ((_host.Function == EFunction.AddSubtract && _host.AddSubSel == EAddSub.Both) ||
                    (_host.Function == EFunction.Compare && _host.CompareSel == ECompareOp.Programmable))
                    pr = pr.Par(_host.Operation.Dual.Drive(SignalSource.Create(op)));
                if (_host.HasOperationND)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                if (_host.HasSCLR)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasCE)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                var rpr = result.Comb.Connect(_host.Result.Dual.AsSignalSource());
                if (_host.Latency == 0)
                {
                    pr = pr.Par(rpr);
                    yield return Verb(ETVMode.Locked, pr);
                }
                else
                {
                    for (int i = 0; i < _host.CyclesPerOperation; i++)
                        yield return Verb(ETVMode.Locked, pr);
                    for (int i = _host.CyclesPerOperation; i < _host.Latency; i++)
                        yield return Verb(ETVMode.Shared);
                    yield return Verb(ETVMode.Shared, rpr);
                }
            }

            public IEnumerable<TAVerb> DoBinOp(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                return DoBinOp("000000", a, b, result);
            }

            private IEnumerable<TAVerb> DoBinOp1(StdLogicVector op, ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Arity != 2)
                    throw new InvalidOperationException();

                IProcess pr = _host.A.Dual.Drive(a)
                    .Par(_host.B.Dual.Drive(b));
                if ((_host.Function == EFunction.AddSubtract && _host.AddSubSel == EAddSub.Both) ||
                    (_host.Function == EFunction.Compare && _host.CompareSel == ECompareOp.Programmable))
                    pr = pr.Par(_host.Operation.Dual.Drive(SignalSource.Create(op)));
                if (_host.HasOperationND)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                if (_host.HasSCLR)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasCE)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));

                var rpr = result.Comb.Connect(((SLVSignal)_host.Result)[0, 0].AsSignalSource());
                if (_host.Latency == 0)
                {
                    pr = pr.Par(rpr);
                    yield return Verb(ETVMode.Locked, pr);
                }
                else
                {
                    for (int i = 0; i < _host.CyclesPerOperation; i++)
                        yield return Verb(ETVMode.Locked, pr);
                    for (int i = _host.CyclesPerOperation; i < _host.Latency; i++)
                        yield return Verb(ETVMode.Shared);
                    yield return Verb(ETVMode.Shared, rpr);
                }

            }

            public IEnumerable<TAVerb> DoBinOp1(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                return DoBinOp1("000000", a, b, result);
            }

            public IEnumerable<TAVerb> Pause()
            {
                IProcess pr = _host.Operation.Dual.Drive(SignalSource.Create<StdLogicVector>("000000"));
                pr = pr.Par(_host.A.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.OperandWidth))));
                if (_host.Arity > 1)
                    pr = pr.Par(_host.B.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.OperandWidth))));
                if (_host.HasOperationND)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasSCLR)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasCE)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                yield return Verb(ETVMode.Locked, pr);
            }

            public IEnumerable<TAVerb> Reset()
            {
                IProcess pr = _host.Operation.Dual.Drive(SignalSource.Create<StdLogicVector>("000000"));
                pr = pr.Par(_host.A.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.OperandWidth))));
                if (_host.Arity > 1)
                    pr = pr.Par(_host.B.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.OperandWidth))));
                if (_host.HasOperationND)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._0)));
                if (_host.HasSCLR)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                if (_host.HasCE)
                    pr = pr.Par(_host.OperationND.Dual.Drive(SignalSource.Create(StdLogic._1)));
                yield return Verb(ETVMode.Locked, pr);
            }

            public IEnumerable<TAVerb> Add(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.AddSubtract)
                    throw new InvalidOperationException();

                switch (_host.AddSubSel)
                {
                    case EAddSub.Add:
                        return DoBinOp(a, b, result);

                    case EAddSub.Both:
                        return DoBinOp("000000", a, b, result);

                    case EAddSub.Subtract:
                        throw new NotSupportedException("Xilinx floating-point core is configured for subtraction. Addition not supported");

                    default:
                        throw new NotImplementedException();
                }
            }

            public IEnumerable<TAVerb> Sub(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.AddSubtract)
                    throw new InvalidOperationException();

                switch (_host.AddSubSel)
                {
                    case EAddSub.Subtract:
                        return DoBinOp(a, b, result);

                    case EAddSub.Both:
                        return DoBinOp("000001", a, b, result);

                    case EAddSub.Add:
                        throw new NotSupportedException("Xilinx floating-point core is configured for addition. Subtraction not supported");

                    default:
                        throw new NotImplementedException();
                }
            }

            public IEnumerable<TAVerb> Mul(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Multiply)
                    throw new InvalidOperationException();

                return DoBinOp(a, b, result);
            }

            public IEnumerable<TAVerb> Div(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Divide)
                    throw new InvalidOperationException();

                return DoBinOp(a, b, result);
            }

            public IEnumerable<TAVerb> Sqrt(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.SquareRoot)
                    throw new InvalidOperationException();

                return DoUnOp(x, result);
            }

            public IEnumerable<TAVerb> IsLt(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Compare)
                    throw new InvalidOperationException();

                if (_host.CompareSel == ECompareOp.Programmable)
                    return DoBinOp1("001100", a, b, result);
                else if (_host.CompareSel == ECompareOp.LessThan)
                    return DoBinOp1(a, b, result);
                else if (_host.CompareSel == ECompareOp.ConditionCode)
                    throw new NotImplementedException();
                else
                    throw new NotSupportedException("Comparison mode not supported");
            }

            public IEnumerable<TAVerb> IsLte(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Compare)
                    throw new InvalidOperationException();

                if (_host.CompareSel == ECompareOp.Programmable)
                    return DoBinOp1("011100", a, b, result);
                else if (_host.CompareSel == ECompareOp.LessThanOrEqual)
                    return DoBinOp1(a, b, result);
                else if (_host.CompareSel == ECompareOp.ConditionCode)
                    throw new NotImplementedException();
                else
                    throw new NotSupportedException("Comparison mode not supported");
            }

            public IEnumerable<TAVerb> IsEq(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Compare)
                    throw new InvalidOperationException();

                if (_host.CompareSel == ECompareOp.Programmable)
                    return DoBinOp("010100", a, b, result);
                else if (_host.CompareSel == ECompareOp.Equal)
                    return DoBinOp(a, b, result);
                else if (_host.CompareSel == ECompareOp.ConditionCode)
                    throw new NotImplementedException();
                else
                    throw new NotSupportedException("Comparison mode not supported");
            }

            public IEnumerable<TAVerb> IsNEq(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Compare)
                    throw new InvalidOperationException();

                if (_host.CompareSel == ECompareOp.Programmable)
                    return DoBinOp1("101100", a, b, result);
                else if (_host.CompareSel == ECompareOp.NotEqual)
                    return DoBinOp1(a, b, result);
                else if (_host.CompareSel == ECompareOp.ConditionCode)
                    throw new NotImplementedException();
                else
                    throw new NotSupportedException("Comparison mode not supported");
            }

            public IEnumerable<TAVerb> IsGte(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Compare)
                    throw new InvalidOperationException();

                if (_host.CompareSel == ECompareOp.Programmable)
                    return DoBinOp1("110100", a, b, result);
                else if (_host.CompareSel == ECompareOp.GreaterThanOrEqual)
                    return DoBinOp1(a, b, result);
                else if (_host.CompareSel == ECompareOp.ConditionCode)
                    throw new NotImplementedException();
                else
                    throw new NotSupportedException("Comparison mode not supported");
            }

            public IEnumerable<TAVerb> IsGt(ISignalSource<StdLogicVector> a, ISignalSource<StdLogicVector> b, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.Compare)
                    throw new InvalidOperationException();

                if (_host.CompareSel == ECompareOp.Programmable)
                    return DoBinOp1("100100", a, b, result);
                else if (_host.CompareSel == ECompareOp.GreaterThan)
                    return DoBinOp1(a, b, result);
                else if (_host.CompareSel == ECompareOp.ConditionCode)
                    throw new NotImplementedException();
                else
                    throw new NotSupportedException("Comparison mode not supported");
            }

            public IEnumerable<TAVerb> Fix2Float(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.FixedToFloat)
                    throw new InvalidOperationException();

                return DoUnOp(x, result);
            }

            public IEnumerable<TAVerb> Float2Fix(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.FloatToFixed)
                    throw new InvalidOperationException();

                return DoUnOp(x, result);
            }

            public IEnumerable<TAVerb> Float2Float(ISignalSource<StdLogicVector> x, ISignalSink<StdLogicVector> result)
            {
                if (_host.Function != EFunction.FloatToFloat)
                    throw new InvalidOperationException();

                return DoUnOp(x, result);
            }

            public override void Establish(IAutoBinder binder)
            {
                var fpu = _host;
                bool haveClk = fpu.Latency > 0;
                if (fpu.Function == EFunction.Multiply)
                    haveClk = true;
                if (fpu.Latency > 0)
                {
                    fpu.Clk = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                }
                if ((fpu.Function == FloatingPointCore.EFunction.AddSubtract && fpu.AddSubSel == FloatingPointCore.EAddSub.Both) ||
                    (fpu.Function == FloatingPointCore.EFunction.Compare && fpu.CompareSel == FloatingPointCore.ECompareOp.Programmable))
                    fpu.Operation = binder.GetSignal<StdLogicVector>(EPortUsage.Default, "Operation", null, StdLogicVector._0s(6));
                fpu.A = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "A", null, StdLogicVector._0s(fpu.OperandWidth));
                if (fpu.Arity > 1)
                    fpu.B = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "B", null, StdLogicVector._0s(fpu.OperandWidth));
                fpu.Result = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "R", null, StdLogicVector._0s(fpu.ResultWidth));
                if (fpu.HasCE)
                    fpu.CE = binder.GetSignal<StdLogic>(EPortUsage.Default, "CE", null, '0');
                if (fpu.HasDivideByZero)
                    fpu.DivideByZero = binder.GetSignal<StdLogic>(EPortUsage.Result, "DivideByZero", null, '0');
                if (fpu.HasInvalidOp)
                    fpu.InvalidOp = binder.GetSignal<StdLogic>(EPortUsage.Result, "InvalidOp", null, '0');
                if (fpu.HasOperationND)
                    fpu.OperationND = binder.GetSignal<StdLogic>(EPortUsage.Default, "OperationND", null, '0');
                if (fpu.HasOperationRFD)
                    fpu.OperationRFD = binder.GetSignal<StdLogic>(EPortUsage.Default, "OperationRFD", null, '0');
                if (fpu.HasOverflow)
                    fpu.Overflow = binder.GetSignal<StdLogic>(EPortUsage.Result, "Overflow", null, '0');
                if (fpu.HasRdy)
                    fpu.Rdy = binder.GetSignal<StdLogic>(EPortUsage.Default, "Rdy", null, '0');
                if (fpu.HasSCLR)
                    fpu.SCLR = binder.GetSignal<StdLogic>(EPortUsage.Reset, "SCLR", null, '0');
            }
        }

        public In<StdLogic> Clk { private get; set; }

        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }
        public In<StdLogicVector> Operation { private get; set; }
        public In<StdLogic> OperationND { private get; set; }
        public In<StdLogic> SCLR { private get; set; }
        public In<StdLogic> CE { private get; set; }

        public Out<StdLogic> OperationRFD { private get; set; }
        public Out<StdLogicVector> Result { private get; set; }
        public Out<StdLogic> Underflow { private get; set; }
        public Out<StdLogic> Overflow { private get; set; }
        public Out<StdLogic> InvalidOp { private get; set; }
        public Out<StdLogic> DivideByZero { private get; set; }
        public Out<StdLogic> Rdy { private get; set; }

        public EDeviceFamily TargetDeviceFamily { get; set; }
        public EISEVersion TargetISEVersion { get; set; }

        [CoreGenProp(ECoreGenUsage.Select)]
        public EGenerator Generator { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "operation_type")]
        [PerformanceRelevant]
        public EFunction Function { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "add_sub_value")]
        [PerformanceRelevant]
        public EAddSub AddSubSel { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_compare_operation")]
        [PerformanceRelevant]
        public ECompareOp CompareSel { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "a_precision_type")]
        [PerformanceRelevant]
        public EPrecision Precision { get; set; }

        private int _exponentWidth;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_a_exponent_width")]
        [PerformanceRelevant]
        public int ExponentWidth 
        {
            get
            {
                switch (Precision)
                {
                    case EPrecision.Single:
                        return FloatFormat.SingleFormat.ExponentWidth;
                    case EPrecision.Double:
                        return FloatFormat.DoubleFormat.ExponentWidth;
                    case EPrecision.Custom:
                        return _exponentWidth;
                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                if (Precision != EPrecision.Custom && value != ExponentWidth)
                    throw new InvalidOperationException("Set Precision to Custom");
                _exponentWidth = value;
            }
        }

        private int _fractionWidth;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_a_fraction_width")]
        [PerformanceRelevant]
        public int FractionWidth 
        {
            get
            {
                switch (Precision)
                {
                    case EPrecision.Single:
                        return FloatFormat.SingleFormat.FractionWidth + 1;
                    case EPrecision.Double:
                        return FloatFormat.DoubleFormat.FractionWidth + 1;
                    case EPrecision.Custom:
                        return _fractionWidth + 1;
                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                if (Precision != EPrecision.Custom && value != FractionWidth)
                    throw new InvalidOperationException("Set Precision to Custom");
                _fractionWidth = value - 1;
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_optimization")]
        [PerformanceRelevant]
        public EOptimization Optimization { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_mult_usage")]
        [PerformanceRelevant]
        public EDSP48EUsage DSP48EUsage { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "maximum_latency")]
        public bool UseMaximumLatency { get; set; }

        private int _latency;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_latency")]
        [PerformanceRelevant]
        public int Latency
        {
            get
            {
                if (UseMaximumLatency)
                    return MaximumLatency;
                else
                    return _latency;
            }
            set
            {
                if (UseMaximumLatency && value != MaximumLatency)
                    throw new InvalidOperationException("Set UseMaximumLatency to false");
                _latency = value;
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_rate")]
        [PerformanceRelevant]
        public int CyclesPerOperation { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_operation_nd")]
        [PerformanceRelevant]
        public bool HasOperationND { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_operation_rfd")]
        [PerformanceRelevant]
        public bool HasOperationRFD { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_rdy")]
        [PerformanceRelevant]
        public bool HasRdy { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_sclr")]
        [PerformanceRelevant]
        public bool HasSCLR { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_ce")]
        [PerformanceRelevant]
        public bool HasCE { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_underflow")]
        [PerformanceRelevant]
        public bool HasUnderflow { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_overflow")]
        [PerformanceRelevant]
        public bool HasOverflow { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_invalid_op")]
        [PerformanceRelevant]
        public bool HasInvalidOp { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_has_divide_by_zero")]
        [PerformanceRelevant]
        public bool HasDivideByZero { get; set; }

        private int _resultExponentWidth;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_result_exponent_width")]
        [PerformanceRelevant]
        public int ResultExponentWidth 
        {
            get
            {
                switch (ResultPrecision)
                {
                    case EPrecision.Single:
                        return FloatFormat.SingleFormat.ExponentWidth;
                    case EPrecision.Double:
                        return FloatFormat.DoubleFormat.ExponentWidth;
                    case EPrecision.Custom:
                        return _resultExponentWidth;
                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                if (ResultPrecision != EPrecision.Custom && value != ResultExponentWidth)
                    throw new InvalidOperationException("Set ResultPrecision to Custom");
                _resultExponentWidth = value;
            }
        }

        private int _resultFractionWidth;
        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "c_result_fraction_width")]
        [PerformanceRelevant]
        public int ResultFractionWidth 
        {
            get
            {
                switch (ResultPrecision)
                {
                    case EPrecision.Single:
                        return FloatFormat.SingleFormat.FractionWidth + 1;
                    case EPrecision.Double:
                        return FloatFormat.DoubleFormat.FractionWidth + 1;
                    case EPrecision.Custom:
                        return _resultFractionWidth + 1;
                    default:
                        throw new InvalidOperationException();
                }
            }
            set
            {
                if (ResultPrecision != EPrecision.Custom && value != ResultFractionWidth)
                    throw new InvalidOperationException("Set ResultPrecision to Custom");
                _resultFractionWidth = value - 1;
            }
        }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "result_precision_type")]
        [PerformanceRelevant]
        public EPrecision ResultPrecision { get; set; }

        [CoreGenProp(ECoreGenUsage.CSet)]
        [PropID(EPropAssoc.CoreGen, "component_name")]
        string ComponentName { get; set; }

        public int OperandWidth
        {
            get 
            {
                return ExponentWidth + FractionWidth;
            }
        }

        public int ResultWidth
        {
            get 
            {
                switch (Function)
                {
                    case EFunction.Compare:
                        switch (TargetISEVersion)
                        {
                            case EISEVersion._11_1:
                            case EISEVersion._11_2:
                            case EISEVersion._11_3:
                            case EISEVersion._11_4:
                            case EISEVersion._11_5: // ?
                                return 32;

                            case EISEVersion._13_2:
                            default: // ?
                                switch (CompareSel)
                                {
                                    case ECompareOp.ConditionCode:
                                        return 4;
                                    default:
                                        return 1;
                                }
                        }

                    default:
                        return ResultExponentWidth + ResultFractionWidth; 
                }                
            }
        }

        public int Arity
        {
            get
            {
                switch (Function)
                {
                    case EFunction.AddSubtract:
                    case EFunction.Compare:
                    case EFunction.Divide:
                    case EFunction.Multiply:
                        return 2;

                    case EFunction.FixedToFloat:
                    case EFunction.FloatToFixed:
                    case EFunction.FloatToFloat:
                    case EFunction.SquareRoot:
                        return 1;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public override string DisplayName
        {
            get
            {
                return "Float" + Function.ToString();
            }
        }

        /// <summary>
        /// Returns the transaction site.
        /// </summary>
        public IFloatingPointCoreTransactionSite TASite { get; private set; }

        private RegPipe _rpipe;
        private SLVSignal _rin;
        private SLVSignal _rout;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public FloatingPointCore()
        {
            Generator = EGenerator.Floating_Point_5_0;
            Function = EFunction.AddSubtract;
            AddSubSel = EAddSub.Both;
            CompareSel = ECompareOp.Programmable;
            Precision = EPrecision.Single;
            ExponentWidth = 8;
            FractionWidth = 24;
            Optimization = EOptimization.HighSpeed;
            DSP48EUsage = EDSP48EUsage.FullUsage;
            UseMaximumLatency = true;
            //Latency = 8;
            CyclesPerOperation = 1;
            HasCE = false;
            HasDivideByZero = false;
            HasInvalidOp = false;
            HasOperationND = false;
            HasOperationRFD = false;
            HasOverflow = false;
            HasRdy = false;
            HasSCLR = false;
            HasUnderflow = false;
            ResultPrecision = EPrecision.Single;
            ResultExponentWidth = 8;
            ResultFractionWidth = 24;
            TASite = new TransactionSite(this);
        }

        public FloatFormat GetInputFormat()
        {
            FloatFormat fmt;
            switch (Precision)
            {
                case EPrecision.Single:
                    fmt = FloatFormat.SingleFormat;
                    break;

                case EPrecision.Double:
                    fmt = FloatFormat.DoubleFormat;
                    break;

                case EPrecision.Custom:
                    fmt = new FloatFormat(ExponentWidth, FractionWidth);
                    break;

                default:
                    throw new NotImplementedException();
            }
            return fmt;
        }

        public FloatFormat GetResultFormat()
        {
            FloatFormat fmt;
            switch (ResultPrecision)
            {
                case EPrecision.Single:
                    fmt = FloatFormat.SingleFormat;
                    break;

                case EPrecision.Double:
                    fmt = FloatFormat.DoubleFormat;
                    break;

                case EPrecision.Custom:
                    fmt = new FloatFormat(ResultExponentWidth, ResultFractionWidth);
                    break;

                default:
                    throw new NotImplementedException();
            }
            return fmt;
        }

        private FloatFormat GetOutputFormat()
        {
            switch (Function)
            {
                case EFunction.FixedToFloat:
                case EFunction.FloatToFloat:
                    return GetResultFormat();

                case EFunction.FloatToFixed:
                    throw new NotImplementedException();

                default:
                    return GetInputFormat();
            }
        }

        public EMultiplierUsage MultiplierUsage
        {
            get
            {
                if (DSP48EUsage == EDSP48EUsage.NoUsage)
                    return EMultiplierUsage.LogicOnly;

                switch (Function)
                {
                    case EFunction.Multiply:
                        switch (TargetDeviceFamily)
                        {
                            case EDeviceFamily.Automotive_Spartan3:
                            case EDeviceFamily.Automotive_Spartan3A:
                            case EDeviceFamily.Automotive_Spartan3E:
                            case EDeviceFamily.Spartan3:
                            case EDeviceFamily.Spartan3A_3AN:
                            case EDeviceFamily.Spartan3E:
                                switch (DSP48EUsage)
                                {
                                    case EDSP48EUsage.FullUsage:
                                        return EMultiplierUsage.MULT18x18;

                                    default:
                                        throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage must be set to 'full usage' for this device family.");
                                }

                            case EDeviceFamily.Automotive_Spartan3A_DSP:
                            case EDeviceFamily.Automotive_Spartan6:
                            case EDeviceFamily.Spartan3A_DSP:
                            case EDeviceFamily.Spartan6:
                            case EDeviceFamily.Spartan6_LowPower:
                                return EMultiplierUsage.DSP48A;

                            case EDeviceFamily.Virtex4:
                                return EMultiplierUsage.DSP48;

                            case EDeviceFamily.Virtex5:
                            case EDeviceFamily.Virtex6:
                            case EDeviceFamily.Virtex6_LowPower:
                                return EMultiplierUsage.DSP48E;

                            default:
                                throw new NotSupportedException("Xilinx floating-point core: Multiplication not supported on this device family.");
                        }

                    case EFunction.AddSubtract:
                        if (DSP48EUsage != EDSP48EUsage.FullUsage)
                            throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage must be set to 'full usage' for addition/subtraction.");
                        switch (TargetDeviceFamily)
                        {
                            case EDeviceFamily.Virtex4:
                                if (Precision == EPrecision.Single ||
                                    Precision == EPrecision.Double)
                                    return EMultiplierUsage.DSP48;
                                else
                                    throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage is only supported for IEEE 754 single and double precision.");

                            case EDeviceFamily.Virtex5:
                            case EDeviceFamily.Virtex6:
                            case EDeviceFamily.Virtex6_LowPower:
                                if (Precision == EPrecision.Single ||
                                    Precision == EPrecision.Double)
                                    return EMultiplierUsage.DSP48E;
                                else
                                    throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage is only supported for IEEE 754 single and double precision.");

                            default:
                                throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage is not supported for this device family.");
                        }

                    default:
                        return EMultiplierUsage.LogicOnly;
                }
            }
        }

        public int MaximumLatency
        {
            get
            {
                switch (Function)
                {
                    case EFunction.AddSubtract:
                        switch (DSP48EUsage)
                        {
                            case EDSP48EUsage.FullUsage:
                                switch (MultiplierUsage)
                                {
                                    case EMultiplierUsage.DSP48:
                                        switch (Precision)
                                        {
                                            case EPrecision.Single:
                                                return 16;

                                            case EPrecision.Double:
                                                return 15;

                                            default:
                                                throw new NotSupportedException("Xilinx floating-point core: DSP48 usage requires IEEE 754 single or double precision.");
                                        }

                                    case EMultiplierUsage.DSP48E:
                                        switch (Precision)
                                        {
                                            case EPrecision.Single:
                                                return 11;

                                            case EPrecision.Double:
                                                return 14;

                                            default:
                                                throw new NotSupportedException("Xilinx floating-point core: DSP48E usage requires IEEE 754 single or double precision.");
                                        }

                                    default:
                                        throw new NotSupportedException("Xilinx floating-point core: Invalid DSP usage for addition/subtraction.");
                                }

                            case EDSP48EUsage.NoUsage:
                                switch (TargetDeviceFamily)
                                {
                                    case EDeviceFamily.Virtex5:
                                    case EDeviceFamily.Virtex6:
                                    case EDeviceFamily.Virtex6_LowPower:
                                    case EDeviceFamily.Automotive_Spartan6:
                                    case EDeviceFamily.Spartan6:
                                        switch (Optimization)
                                        {
                                            case EOptimization.LowLatency:
                                                if (TargetDeviceFamily == EDeviceFamily.Spartan6 ||
                                                    TargetDeviceFamily == EDeviceFamily.Automotive_Spartan6)
                                                    throw new NotSupportedException("Xilinx floating-point core: Low latency optimization not supported on Spartan-6 families.");

                                                if (Precision == EPrecision.Single ||
                                                    Precision == EPrecision.Double)
                                                    return 8;
                                                else
                                                    throw new NotSupportedException("Xilinx floating-point core: Low latency optimization requires IEEE 754 single or double precision.");

                                            case EOptimization.HighSpeed:
                                                if (FractionWidth <= 13)
                                                    return 8;
                                                else if (FractionWidth <= 14)
                                                    return 9;
                                                else if (FractionWidth <= 15)
                                                    return 10;
                                                else if (FractionWidth <= 17)
                                                    return 11;
                                                else if (FractionWidth <= 61)
                                                    return 12;
                                                else
                                                    return 13;

                                            default:
                                                throw new NotImplementedException();
                                        }

                                    default:
                                        if (FractionWidth <= 5)
                                            return 9;
                                        else if (FractionWidth <= 14)
                                            return 10;
                                        else if (FractionWidth <= 15)
                                            return 11;
                                        else if (FractionWidth <= 17)
                                            return 12;
                                        else if (FractionWidth <= 29)
                                            return 13;
                                        else if (FractionWidth <= 62)
                                            return 14;
                                        else
                                            return 15;
                                }

                            default:
                                throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage must be either 'full' or 'none' for addition/subtraction.");
                        }

                    case EFunction.Compare:
                        return 2;

                    case EFunction.Divide:
                        return FractionWidth + 4;

                    case EFunction.FixedToFloat:
                        if (OperandWidth <= 8)
                            return 5;
                        else if (OperandWidth <= 32)
                            return 6;
                        else
                            return 7;

                    case EFunction.FloatToFixed:
                        {
                            int w = Math.Max(FractionWidth + 1, ResultWidth);
                            if (w <= 15)
                                return 5;
                            else if (w <= 64)
                                return 6;
                            else
                                return 7;
                        }

                    case EFunction.FloatToFloat:
                        if (ResultExponentWidth < ExponentWidth ||
                            ResultFractionWidth < FractionWidth)
                            return 3;
                        else
                            return 2;

                    case EFunction.Multiply:
                        switch (MultiplierUsage)
                        {
                            case EMultiplierUsage.LogicOnly:
                                if (FractionWidth <= 5)
                                    return 5;
                                else if (FractionWidth <= 11)
                                    return 6;
                                else if (FractionWidth <= 23)
                                    return 7;
                                else if (FractionWidth <= 47)
                                    return 8;
                                else
                                    return 9;

                            case EMultiplierUsage.MULT18x18:
                                if (FractionWidth <= 17)
                                    return 4;
                                else if (FractionWidth <= 34)
                                    return 6;
                                else if (FractionWidth <= 51)
                                    return 7;
                                else
                                    return 8;

                            case EMultiplierUsage.DSP48A:
                                switch (DSP48EUsage)
                                {
                                    case EDSP48EUsage.MediumUsage:
                                        if (Precision == EPrecision.Single)
                                            return 9;
                                        else
                                            throw new NotSupportedException("Xilinx floating-point core: Medium DSP48A usage is only valid for IEEE 754 single precision.");

                                    case EDSP48EUsage.FullUsage:
                                        if (FractionWidth <= 17)
                                            return 6;
                                        else if (FractionWidth <= 34)
                                            return 11;
                                        else if (FractionWidth <= 51)
                                            return 18;
                                        else
                                            return 27;

                                    case EDSP48EUsage.MaxUsage:
                                        if (FractionWidth <= 17)
                                            return 5;
                                        else if (FractionWidth <= 34)
                                            return 10;
                                        else if (FractionWidth <= 51)
                                            return 17;
                                        else
                                            return 26;

                                    default:
                                        throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage must be either 'medium', 'full' or 'max'.");
                                }

                            case EMultiplierUsage.DSP48:
                                switch (DSP48EUsage)
                                {
                                    case EDSP48EUsage.MediumUsage:
                                        switch (Precision)
                                        {
                                            case EPrecision.Single:
                                                return 9;
                                            case EPrecision.Double:
                                                return 17;
                                            default:
                                                throw new NotSupportedException("Xilinx floating-point core: Medium DSP48A usage is only valid for IEEE 754 single/double precision.");
                                        }

                                    case EDSP48EUsage.FullUsage:
                                        if (FractionWidth <= 17)
                                            return 6;
                                        else if (FractionWidth <= 34)
                                            return 10;
                                        else if (FractionWidth <= 51)
                                            return 15;
                                        else
                                            return 22;

                                    case EDSP48EUsage.MaxUsage:
                                        if (FractionWidth <= 17)
                                            return 8;
                                        else if (FractionWidth <= 34)
                                            return 11;
                                        else if (FractionWidth <= 51)
                                            return 16;
                                        else
                                            return 23;

                                    default:
                                        throw new NotSupportedException("Xilinx floating-point core: DSP48EUsage must be either 'medium', 'full' or 'max'.");
                                }

                            case EMultiplierUsage.DSP48E:
                                switch (Optimization)
                                {
                                    case EOptimization.HighSpeed:
                                        switch (DSP48EUsage)
                                        {
                                            case EDSP48EUsage.MediumUsage:
                                                switch (Precision)
                                                {
                                                    case EPrecision.Single:
                                                        return 8;
                                                    case EPrecision.Double:
                                                        return 15;
                                                    default:
                                                        throw new NotSupportedException("Xilinx floating-point core: Medium DSP48A usage is only valid for IEEE 754 single/double precision.");
                                                }

                                            case EDSP48EUsage.FullUsage:
                                                switch (Precision)
                                                {
                                                    case EPrecision.Single:
                                                        return 8;
                                                    case EPrecision.Double:
                                                        return 15;
                                                    case EPrecision.Custom:
                                                        if (FractionWidth <= 17)
                                                            return 6;
                                                        else if (FractionWidth <= 24)
                                                            return 8;
                                                        else if (FractionWidth <= 34)
                                                            return 10;
                                                        else if (FractionWidth <= 41)
                                                            return 12;
                                                        else if (FractionWidth <= 51)
                                                            return 15;
                                                        else if (FractionWidth <= 58)
                                                            return 18;
                                                        else
                                                            return 22;
                                                    default:
                                                        throw new NotImplementedException();
                                                }

                                            case EDSP48EUsage.MaxUsage:
                                                switch (Precision)
                                                {
                                                    case EPrecision.Single:
                                                        return 6;
                                                    case EPrecision.Double:
                                                        return 16;
                                                    case EPrecision.Custom:
                                                        if (FractionWidth <= 17)
                                                            return 8;
                                                        else if (FractionWidth <= 24)
                                                            return 9;
                                                        else if (FractionWidth <= 34)
                                                            return 11;
                                                        else if (FractionWidth <= 41)
                                                            return 13;
                                                        else if (FractionWidth <= 51)
                                                            return 16;
                                                        else if (FractionWidth <= 58)
                                                            return 19;
                                                        else
                                                            return 23;
                                                    default:
                                                        throw new NotImplementedException();
                                                }

                                            default:
                                                throw new NotSupportedException();
                                        }

                                    case EOptimization.LowLatency:
                                        if (DSP48EUsage == EDSP48EUsage.MaxUsage &&
                                            Precision == EPrecision.Double)
                                            return 10;
                                        else
                                            throw new NotSupportedException("Xilinx floating-point core: Low latency optimization is only valid for maximum DSP48EUsage and IEEE 754 double precision.");

                                    default:
                                        throw new NotImplementedException();
                                }

                            default:
                                throw new NotImplementedException();
                        }

                    case EFunction.SquareRoot:
                        return FractionWidth + 4;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        protected override void PreInitialize()
        {
            _rin = new SLVSignal(ResultWidth);
            _rout = new SLVSignal(ResultWidth);
            _rpipe = new RegPipe(Latency, ResultWidth);
            Bind(() =>
                {
                    _rpipe.Clk = Clk;
                    _rpipe.Din = _rin;
                    _rpipe.Dout = _rout;
                });
        }

        protected override void Initialize()
        {
            if (Latency > 0)
                AddProcess(OnClock, Clk.ChangedEvent);
            AddProcess(Comb, _rout.ChangedEvent);
        }

        [DoNotAnalyze]
        private void OnClock()
        {
            FloatFormat fmt = GetInputFormat();
            FloatFormat rfmt = GetOutputFormat();

            double a = A.Cur.ToFloat(fmt);
            double b = B.Cur.ToFloat(fmt);
            StdLogicVector r;

            switch (Function)
            {
                case EFunction.AddSubtract:
                    if (AddSubSel == EAddSub.Add || 
                        (AddSubSel == EAddSub.Both && Operation.Cur[0] == '0'))
                    {
                        r = (a + b).ToSLV(fmt);
                    }
                    else if (AddSubSel == EAddSub.Subtract || 
                        (AddSubSel == EAddSub.Both && Operation.Cur[0] == '1'))
                    {
                        r = (a - b).ToSLV(fmt);
                    }
                    else
                    {
                        r = StdLogicVector.Xs(fmt.TotalWidth); break;
                    }
                    break;

                case EFunction.Compare:
                    switch (CompareSel)
                    {
                        case ECompareOp.ConditionCode:
                            {
                                StdLogicVector unord, gt, lt, eq;
                                unord = (double.IsNaN(a) || double.IsNaN(b)) ? "1" : "0";
                                gt = a > b ? "1" : "0";
                                lt = a < b ? "1" : "0";
                                eq = a == b ? "1" : "0";
                                r = unord.Concat(gt.Concat(lt.Concat(eq)));
                            }
                            break;

                        case ECompareOp.Equal:
                            r = a == b ? "1" : "0";
                            break;

                        case ECompareOp.GreaterThan:
                            r = a > b ? "1" : "0";
                            break;

                        case ECompareOp.GreaterThanOrEqual:
                            r = a >= b ? "1" : "0";
                            break;

                        case ECompareOp.LessThan:
                            r = a < b ? "1" : "0";
                            break;

                        case ECompareOp.LessThanOrEqual:
                            r = a <= b ? "1" : "0";
                            break;

                        case ECompareOp.NotEqual:
                            r = a != b ? "1" : "0";
                            break;

                        case ECompareOp.Programmable:
                            switch (Operation.Cur.ToString())
                            {
                                case sCC_Unordered: // unordered
                                    r = (double.IsNaN(a) || double.IsNaN(b)) ? "1" : "0";
                                    break;

                                case sCC_LessThan:
                                    r = a < b ? "1" : "0";
                                    break;

                                case sCC_Equal:
                                    r = a == b ? "1" : "0";
                                    break;

                                case sCC_LessThanOrEqual:
                                    r = a <= b ? "1" : "0";
                                    break;

                                case sCC_GreaterThan:
                                    r = a > b ? "1" : "0";
                                    break;

                                case sCC_NotEqual:
                                    r = a != b ? "1" : "0";
                                    break;

                                case sCC_GreaterThanOrEqual:
                                    r = a >= b ? "1" : "0";
                                    break;

                                default:
                                    r = "X";
                                    break;
                            }
                            break;

                        case ECompareOp.Unordered:
                            r = (double.IsNaN(a) || double.IsNaN(b)) ? "1" : "0";
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                    break;

                case EFunction.Divide:
                    r = (a - b).ToSLV(fmt);
                    break;

                case EFunction.FixedToFloat:
                case EFunction.FloatToFixed:
                    throw new NotImplementedException();

                case EFunction.FloatToFloat:
                    r = a.ToSLV(rfmt);
                    break;

                case EFunction.Multiply:
                    r = (a * b).ToSLV(fmt);
                    break;

                case EFunction.SquareRoot:
                    r = Math.Sqrt(a).ToSLV(fmt);
                    break;

                default:
                    throw new NotImplementedException();
            }

            _rin.Next = r;
        }

        public const string sCC_Unordered = "000100";
        public static readonly StdLogicVector CC_Unordered = sCC_Unordered;
        public const string sCC_LessThan = "001100";
        public static readonly StdLogicVector CC_LessThan = sCC_LessThan;
        public const string sCC_Equal = "010100";
        public static readonly StdLogicVector CC_Equal = sCC_Equal;
        public const string sCC_LessThanOrEqual = "011100";
        public static readonly StdLogicVector CC_LessThanOrEqual = sCC_LessThanOrEqual;
        public const string sCC_GreaterThan = "100100";
        public static readonly StdLogicVector CC_GreaterThan = sCC_GreaterThan;
        public const string sCC_NotEqual = "101100";
        public static readonly StdLogicVector CC_NotEqual = sCC_NotEqual;
        public const string sCC_GreaterThanOrEqual = "110100";
        public static readonly StdLogicVector CC_GreaterThanOrEqual = sCC_GreaterThanOrEqual;

        [DoNotAnalyze]
        private void Comb()
        {
            Result.Next = _rout.Cur;
        }

        protected override void OnAnalysis()
        {
            base.OnAnalysis();
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

        public override bool IsEquivalent(Component component)
        {
            var other = component as FloatingPointCore;
            if (other == null)
                return false;

            return this.CompareSel == other.CompareSel &&
                this.CyclesPerOperation == other.CyclesPerOperation &&
                this.DSP48EUsage == other.DSP48EUsage &&
                this.ExponentWidth == other.ExponentWidth &&
                this.FractionWidth == other.FractionWidth &&
                this.Function == other.Function &&
                this.Generator == other.Generator &&
                this.HasCE == other.HasCE &&
                this.HasDivideByZero == other.HasDivideByZero &&
                this.HasInvalidOp == other.HasInvalidOp &&
                this.HasOperationND == other.HasOperationND &&
                this.HasOperationRFD == other.HasOperationRFD &&
                this.HasOverflow == other.HasOverflow &&
                this.HasRdy == other.HasRdy &&
                this.HasSCLR == other.HasSCLR &&
                this.HasUnderflow == other.HasUnderflow &&
                this.Latency == other.Latency &&
                this.MultiplierUsage == other.MultiplierUsage &&
                this.OperandWidth == other.OperandWidth &&
                this.Optimization == other.Optimization &&
                this.Precision == other.Precision &&
                this.ResultExponentWidth == other.ResultExponentWidth &&
                this.ResultFractionWidth == other.ResultFractionWidth &&
                this.ResultPrecision == other.ResultPrecision &&
                this.ResultWidth == other.ResultWidth &&
                this.TargetDeviceFamily == other.TargetDeviceFamily &&
                this.TargetISEVersion == other.TargetISEVersion &&
                this.UseMaximumLatency == other.UseMaximumLatency;
        }

        public override int GetBehaviorHashCode()
        {
            return this.CompareSel.GetHashCode() ^
                this.CyclesPerOperation.GetHashCode() ^
                this.DSP48EUsage.GetHashCode() ^
                this.ExponentWidth.GetHashCode() ^
                this.FractionWidth.GetHashCode() ^
                this.Function.GetHashCode() ^
                this.Generator.GetHashCode() ^
                this.HasCE.GetHashCode() ^
                this.HasDivideByZero.GetHashCode() ^
                this.HasInvalidOp.GetHashCode() ^
                this.HasOperationND.GetHashCode() ^
                this.HasOperationRFD.GetHashCode() ^
                this.HasOverflow.GetHashCode() ^
                this.HasRdy.GetHashCode() ^
                this.HasSCLR.GetHashCode() ^
                this.HasUnderflow.GetHashCode() ^
                this.Latency.GetHashCode() ^
                this.MultiplierUsage.GetHashCode() ^
                this.OperandWidth.GetHashCode() ^
                this.Optimization.GetHashCode() ^
                this.Precision.GetHashCode() ^
                this.ResultExponentWidth.GetHashCode() ^
                this.ResultFractionWidth.GetHashCode() ^
                this.ResultPrecision.GetHashCode() ^
                this.ResultWidth.GetHashCode() ^
                this.TargetDeviceFamily.GetHashCode() ^
                this.TargetISEVersion.GetHashCode() ^
                this.UseMaximumLatency.GetHashCode();
        }
    }

    /// <summary>
    /// Maps floating-point arithmetic operations to the Xilinx floating-point core.
    /// </summary>
    public class FloatingPointXILMapper : IXILMapper
    {
        /// <summary>
        /// Provides core-specific configuration options.
        /// </summary>
        public class CoreConfiguration
        {
            public bool UseDedicatedAddSub { get; set; }
            public bool UseDedicatedCmp { get; set; }
            public bool EnableDeviceCapabilityDependentDSPUsage { get; set; }
            public float DSPUsageRatio { get; set; }
            public FloatingPointCore.EDSP48EUsage DSP48EUsage { get; set; }
            public bool UseMaximumLatency { get; set; }
            public int Latency { get; set; }
            public int CyclesPerOperation { get; set; }
            public bool SpecifyLatencyRatio { get; set; }
            public float LatencyRatio { get; set; }
            public bool HasOperationND { get; set; }
            public bool HasOperationRFD { get; set; }
            public bool HasRdy { get; set; }
            public bool HasSCLR { get; set; }
            public bool HasCE { get; set; }
            public bool HasUnderflow { get; set; }
            public bool HasOverflow { get; set; }
            public bool HasInvalidOp { get; set; }
            public bool HasDivideByZero { get; set; }
        }

        /// <summary>
        /// Manages core-specific configuration options, depending on floating-point precision and functional selection.
        /// </summary>
        public class CoreConfigurator
        {
            private CacheDictionary<Tuple<FloatingPointCore.EPrecision, FloatingPointCore.EFunction>, CoreConfiguration> _map;

            internal CoreConfigurator()
            {
                _map = new CacheDictionary<Tuple<FloatingPointCore.EPrecision, FloatingPointCore.EFunction>, CoreConfiguration>(CreateConfiguration);
            }

            private CoreConfiguration CreateConfiguration(Tuple<FloatingPointCore.EPrecision, FloatingPointCore.EFunction> key)
            {
                return new CoreConfiguration();
            }

            /// <summary>
            /// Retrieves the configuration options for a given precision and functional selection.
            /// </summary>
            public CoreConfiguration this[FloatingPointCore.EPrecision prec, FloatingPointCore.EFunction func]
            {
                get { return _map[Tuple.Create(prec, func)]; }
            }
        }

        /// <summary>
        /// Returns the configuration manager.
        /// </summary>
        public CoreConfigurator Config { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public FloatingPointXILMapper()
        {
            Config = new CoreConfigurator();

            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.AddSubtract].EnableDeviceCapabilityDependentDSPUsage = true;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.AddSubtract].DSPUsageRatio = 1.0f;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.AddSubtract].DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.AddSubtract].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Compare].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Compare].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Divide].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Divide].UseMaximumLatency = true;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Divide].CyclesPerOperation = 26;

            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FixedToFloat].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FixedToFloat].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFixed].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFixed].UseMaximumLatency = true;
        
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFloat].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.FloatToFloat].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Multiply].EnableDeviceCapabilityDependentDSPUsage = true;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Multiply].DSPUsageRatio = 1.0f;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Multiply].DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.Multiply].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.SquareRoot].DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.SquareRoot].UseMaximumLatency = true;
            Config[FloatingPointCore.EPrecision.Single, FloatingPointCore.EFunction.SquareRoot].CyclesPerOperation = 25;

            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.AddSubtract].EnableDeviceCapabilityDependentDSPUsage = true;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.AddSubtract].DSPUsageRatio = 1.0f;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.AddSubtract].DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.AddSubtract].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Compare].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Compare].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Divide].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Divide].UseMaximumLatency = true;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Divide].CyclesPerOperation = 55;

            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FixedToFloat].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FixedToFloat].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFixed].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFixed].UseMaximumLatency = true;
        
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFloat].DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.FloatToFloat].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Multiply].EnableDeviceCapabilityDependentDSPUsage = true;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Multiply].DSPUsageRatio = 1.0f;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Multiply].DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.Multiply].UseMaximumLatency = true;

            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.SquareRoot].DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.SquareRoot].UseMaximumLatency = true;
            Config[FloatingPointCore.EPrecision.Double, FloatingPointCore.EFunction.SquareRoot].CyclesPerOperation = 54;
        }

        private class XILMapping : IXILMapping
        {
            private ITransactionSite _taSite;
            private Func<ISignalSource<StdLogicVector>[], ISignalSink<StdLogicVector>[], IEnumerable<TAVerb>> _realize;
            private bool _swap;

            public XILMapping(
                ITransactionSite taSite,
                Func<ISignalSource<StdLogicVector>[], ISignalSink<StdLogicVector>[], IEnumerable<TAVerb>> realize,
                bool swap)
            {
                _taSite = taSite;
                _realize = realize;
                _swap = swap;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                if (_swap)
                    return _realize(new ISignalSource<StdLogicVector>[] { operands[1], operands[0] }, results);
                else
                    return _realize(operands, results);
            }

            public ITransactionSite TASite
            {
                get { return _taSite; }
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
                get { return ((FloatingPointCore)_taSite.Host).Latency; }
            }

            public string Description
            {
                get
                {
                    var fpu = (FloatingPointCore)_taSite.Host;                    
                    string text = "Xilinx " + fpu.ExponentWidth + "/" + fpu.FractionWidth + " bit floating-point, " + 
                        fpu.Latency + " stage ";

                    switch (fpu.Function)
                    {
                        case FloatingPointCore.EFunction.AddSubtract:
                            switch (fpu.AddSubSel)
                            {
                                case FloatingPointCore.EAddSub.Add:
                                    return text + " adder";

                                case FloatingPointCore.EAddSub.Both:
                                    return text + " adder/subtracter";

                                case FloatingPointCore.EAddSub.Subtract:
                                    return text + " subtracter";

                                default:
                                    throw new NotImplementedException();
                            }

                        case FloatingPointCore.EFunction.Compare:
                            return text + " comparator";

                        case FloatingPointCore.EFunction.Divide:
                            return text + " divider";

                        case FloatingPointCore.EFunction.FixedToFloat:
                            return "Xilinx " + fpu.ExponentWidth + "/" + fpu.FractionWidth + " => " +
                                fpu.ResultExponentWidth + "/" + fpu.ResultFractionWidth +
                                " bit, " + fpu.Latency + " stage fixed-point to floating-point converter";

                        case FloatingPointCore.EFunction.FloatToFixed:
                            return "Xilinx " + fpu.ExponentWidth + "/" + fpu.FractionWidth + " => " +
                                fpu.ResultExponentWidth + "/" + fpu.ResultFractionWidth +
                                " bit, " + fpu.Latency + " stage floating-point to fixed-point converter";

                        case FloatingPointCore.EFunction.FloatToFloat:
                            return "Xilinx " + fpu.ExponentWidth + "/" + fpu.FractionWidth + " => " +
                                fpu.ResultExponentWidth + "/" + fpu.ResultFractionWidth +
                                " bit, " + fpu.Latency + " stage floating-point to floating-point converter";

                        case FloatingPointCore.EFunction.Multiply:
                            return text + " multiplier";

                        case FloatingPointCore.EFunction.SquareRoot:
                            return text + " square root";

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
            yield return DefaultInstructionSet.Instance.Mul();
            yield return DefaultInstructionSet.Instance.Div();
            yield return DefaultInstructionSet.Instance.Sqrt();
            yield return DefaultInstructionSet.Instance.Convert();
            yield return DefaultInstructionSet.Instance.IsEq();
            yield return DefaultInstructionSet.Instance.IsGt();
            yield return DefaultInstructionSet.Instance.IsGte();
            yield return DefaultInstructionSet.Instance.IsLte();
            yield return DefaultInstructionSet.Instance.IsLt();
            yield return DefaultInstructionSet.Instance.IsNEq();
        }

        public IXILMapping TryMapOne(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, bool swap)
        {
            var fu = taSite.Host;
            FloatingPointCore fpu = (FloatingPointCore)fu;
            if (fpu == null)
                return null;

            if (operandTypes.Length != fpu.Arity)
                return null;

            if (fpu.Function == FloatingPointCore.EFunction.AddSubtract ||
                fpu.Function == FloatingPointCore.EFunction.Compare ||
                fpu.Function == FloatingPointCore.EFunction.Divide ||
                fpu.Function == FloatingPointCore.EFunction.FloatToFixed ||
                fpu.Function == FloatingPointCore.EFunction.FloatToFloat ||
                fpu.Function == FloatingPointCore.EFunction.Multiply ||
                fpu.Function == FloatingPointCore.EFunction.SquareRoot)
            {
                Type itype = null;
                switch (fpu.Precision)
                {
                    case FloatingPointCore.EPrecision.Single:
                        itype = typeof(float);
                        break;

                    case FloatingPointCore.EPrecision.Double:
                        itype = typeof(double);
                        break;

                    default:
                        return null;
                }

                foreach (TypeDescriptor otype in operandTypes)
                    if (!otype.CILType.Equals(itype))
                        return null;
            }

            Func<ISignalSource<StdLogicVector>[], ISignalSink<StdLogicVector>[], IEnumerable<TAVerb>> realize = null;
            ISignalSource<StdLogicVector> defOp = SignalSource.Create(StdLogicVector._0s(fpu.OperandWidth));
            ISignalSink<StdLogicVector> defR = SignalSink.Nil<StdLogicVector>();

            switch (fpu.Function)
            {
                case FloatingPointCore.EFunction.AddSubtract:
                    if (instr.Name.Equals(InstructionCodes.Add) &&
                        (fpu.AddSubSel == FloatingPointCore.EAddSub.Add ||
                        fpu.AddSubSel == FloatingPointCore.EAddSub.Both))
                    {
                        realize = (os, rs) => fpu.TASite.Add(os[0], os[1], rs[0]);
                    }
                    else if (instr.Name.Equals(InstructionCodes.Sub) &&
                        (fpu.AddSubSel == FloatingPointCore.EAddSub.Subtract ||
                        fpu.AddSubSel == FloatingPointCore.EAddSub.Both))
                    {
                        realize = (os, rs) => fpu.TASite.Sub(os[0], os[1], rs[0]);
                    }
                    else
                        return null;
                    break;

                case FloatingPointCore.EFunction.Compare:
                    if (instr.Name.Equals(InstructionCodes.IsLt))
                    {
                        realize = (os, rs) => fpu.TASite.IsLt(os[0], os[1], rs[0]);
                    }
                    else if (instr.Name.Equals(InstructionCodes.IsLte))
                    {
                        realize = (os, rs) => fpu.TASite.IsLte(os[0], os[1], rs[0]);
                    }
                    else if (instr.Name.Equals(InstructionCodes.IsEq))
                    {
                        realize = (os, rs) => fpu.TASite.IsEq(os[0], os[1], rs[0]);
                    }
                    else if (instr.Name.Equals(InstructionCodes.IsNEq))
                    {
                        realize = (os, rs) => fpu.TASite.IsNEq(os[0], os[1], rs[0]);
                    }
                    else if (instr.Name.Equals(InstructionCodes.IsGte))
                    {
                        realize = (os, rs) => fpu.TASite.IsGte(os[0], os[1], rs[0]);
                    }
                    else if (instr.Name.Equals(InstructionCodes.IsGt))
                    {
                        realize = (os, rs) => fpu.TASite.IsGt(os[0], os[1], rs[0]);
                    }
                    else
                    {
                        return null;
                    }
                    break;

                case FloatingPointCore.EFunction.Divide:
                    if (instr.Name.Equals(InstructionCodes.Div))
                    {
                        realize = (os, rs) => fpu.TASite.Div(os[0], os[1], rs[0]);
                    }
                    else
                        return null;
                    break;

                case FloatingPointCore.EFunction.FixedToFloat:
                    if (instr.Name.Equals(InstructionCodes.Convert))
                    {
                        if (!operandTypes[0].CILType.Equals(typeof(SFix)) &&
                            !operandTypes[0].CILType.Equals(typeof(Signed)))
                            return null;

                        FixFormat ffmt = SFix.GetFormat(operandTypes[0]);
                        if (ffmt.IntWidth != fpu.ExponentWidth ||
                            ffmt.FracWidth != fpu.FractionWidth)
                            return null;

                        switch (fpu.ResultPrecision)
                        {
                            case FloatingPointCore.EPrecision.Single:
                                if (!resultTypes[0].CILType.Equals(typeof(float)))
                                    return null;
                                break;

                            case FloatingPointCore.EPrecision.Double:
                                if (!resultTypes[0].CILType.Equals(typeof(double)))
                                    return null;
                                break;

                            default:
                                return null;
                        }

                        realize = (os, rs) => fpu.TASite.Fix2Float(os[0], rs[0]);
                    }
                    else
                        return null;
                    break;

                case FloatingPointCore.EFunction.FloatToFixed:
                    if (instr.Name.Equals(InstructionCodes.Convert))
                    {
                        if (!resultTypes[0].CILType.Equals(typeof(SFix)) &&
                            !resultTypes[0].CILType.Equals(typeof(Signed)))
                            return null;

                        FixFormat ffmt = SFix.GetFormat(resultTypes[0]);
                        if (ffmt.IntWidth != fpu.ResultExponentWidth ||
                            ffmt.FracWidth != fpu.ResultFractionWidth)
                            return null;

                        switch (fpu.Precision)
                        {
                            case FloatingPointCore.EPrecision.Single:
                                if (!operandTypes[0].CILType.Equals(typeof(float)))
                                    return null;
                                break;

                            case FloatingPointCore.EPrecision.Double:
                                if (!operandTypes[0].CILType.Equals(typeof(double)))
                                    return null;
                                break;

                            default:
                                return null;
                        }

                        realize = (os, rs) => fpu.TASite.Float2Fix(os[0], rs[0]);
                    }
                    else
                        return null;
                    break;

                case FloatingPointCore.EFunction.FloatToFloat:
                    if (instr.Name.Equals(InstructionCodes.Convert))
                    {
                        switch (fpu.Precision)
                        {
                            case FloatingPointCore.EPrecision.Single:
                                if (!operandTypes[0].CILType.Equals(typeof(float)))
                                    return null;
                                break;

                            case FloatingPointCore.EPrecision.Double:
                                if (!operandTypes[0].CILType.Equals(typeof(double)))
                                    return null;
                                break;

                            default:
                                return null;
                        }

                        switch (fpu.ResultPrecision)
                        {
                            case FloatingPointCore.EPrecision.Single:
                                if (!resultTypes[0].CILType.Equals(typeof(float)))
                                    return null;
                                break;

                            case FloatingPointCore.EPrecision.Double:
                                if (!resultTypes[0].CILType.Equals(typeof(double)))
                                    return null;
                                break;

                            default:
                                return null;
                        }

                        realize = (os, rs) => fpu.TASite.Float2Float(os[0], rs[0]);
                    }
                    else
                        return null;
                    break;

                case FloatingPointCore.EFunction.Multiply:
                    if (instr.Name.Equals(InstructionCodes.Mul))
                    {
                        realize = (os, rs) => fpu.TASite.Mul(os[0], os[1], rs[0]);
                    }
                    else
                        return null;
                    break;

                case FloatingPointCore.EFunction.SquareRoot:
                    if (instr.Name.Equals(InstructionCodes.Sqrt))
                    {
                        realize = (os, rs) => fpu.TASite.Sqrt(os[0], rs[0]);
                    }
                    else
                        return null;
                    break;

                default:
                    throw new NotImplementedException();
            }

            return new XILMapping(fpu.TASite, realize, swap);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            IXILMapping alt0, alt1 = null;
            alt0 = TryMapOne(taSite, instr, operandTypes, resultTypes, false);
            switch (instr.Name)
            {
                case InstructionCodes.Add:
                case InstructionCodes.Mul:
                case InstructionCodes.IsEq:
                case InstructionCodes.IsNEq:
                    alt1 = TryMapOne(taSite, instr, new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsGt:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsLt(), new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsGte:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsLte(), new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsLt:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsGt(), new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;

                case InstructionCodes.IsLte:
                    alt1 = TryMapOne(taSite, DefaultInstructionSet.Instance.IsGte(), new TypeDescriptor[] { operandTypes[1], operandTypes[0] }, resultTypes, true);
                    break;
            }
            if (alt0 != null)
                yield return alt0;
            if (alt1 != null)
                yield return alt1;
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            XilinxProject xproj = proj as XilinxProject;
            if (xproj == null)
                return null;

            Type otype = operandTypes[0].CILType;
            if (!operandTypes.All(t => t.CILType.Equals(otype)))
                return null;
            Type rtype = resultTypes[0].CILType;
            FloatingPointCore fpu = null;
            CoreConfiguration cfg = null;

            switch (instr.Name)
            {
                case InstructionCodes.Add:
                case InstructionCodes.Sub:
                    {
                        FloatingPointCore.EPrecision prec = FloatingPointCore.EPrecision.Single;
                        if (otype.Equals(typeof(float)))
                            prec = FloatingPointCore.EPrecision.Single;
                        else if (otype.Equals(typeof(double)))
                            prec = FloatingPointCore.EPrecision.Double;
                        else
                            return null;
                        fpu = new FloatingPointCore()
                        {
                            Function = FloatingPointCore.EFunction.AddSubtract,
                            Precision = prec,
                            ResultPrecision = prec,
                        };
                        cfg = Config[prec, FloatingPointCore.EFunction.AddSubtract];
                        if (cfg.UseDedicatedAddSub)
                        {
                            if (instr.Name.Equals(InstructionCodes.Add))
                                fpu.AddSubSel = FloatingPointCore.EAddSub.Add;
                            else
                                fpu.AddSubSel = FloatingPointCore.EAddSub.Subtract;
                        }
                        else
                        {
                            fpu.AddSubSel = FloatingPointCore.EAddSub.Both;
                        }
                    }
                    break;

                case InstructionCodes.IsEq:
                case InstructionCodes.IsGt:
                case InstructionCodes.IsGte:
                case InstructionCodes.IsLt:
                case InstructionCodes.IsLte:
                case InstructionCodes.IsNEq:
                    {
                        FloatingPointCore.EPrecision prec = FloatingPointCore.EPrecision.Single;
                        if (otype.Equals(typeof(float)))
                            prec = FloatingPointCore.EPrecision.Single;
                        else if (otype.Equals(typeof(double)))
                            prec = FloatingPointCore.EPrecision.Double;
                        else
                            return null;
                        fpu = new FloatingPointCore()
                        {
                            Function = FloatingPointCore.EFunction.Compare,
                            Precision = prec
                        };
                        cfg = Config[prec, FloatingPointCore.EFunction.Compare];
                        if (cfg.UseDedicatedCmp)
                        {
                            switch (instr.Name)
                            {
                                case InstructionCodes.IsEq:
                                    fpu.CompareSel = FloatingPointCore.ECompareOp.Equal;
                                    break;
                                case InstructionCodes.IsGt:
                                    fpu.CompareSel = FloatingPointCore.ECompareOp.GreaterThan;
                                    break;
                                case InstructionCodes.IsGte:
                                    fpu.CompareSel = FloatingPointCore.ECompareOp.GreaterThanOrEqual;
                                    break;
                                case InstructionCodes.IsLt:
                                    fpu.CompareSel = FloatingPointCore.ECompareOp.LessThan;
                                    break;
                                case InstructionCodes.IsLte:
                                    fpu.CompareSel = FloatingPointCore.ECompareOp.LessThanOrEqual;
                                    break;
                                case InstructionCodes.IsNEq:
                                    fpu.CompareSel = FloatingPointCore.ECompareOp.NotEqual;
                                    break;
                                default:
                                    throw new NotImplementedException();
                            }
                        }
                        else
                        {
                            fpu.CompareSel = FloatingPointCore.ECompareOp.Programmable;
                        }

                        fpu.ResultPrecision = FloatingPointCore.EPrecision.Custom;
                        fpu.ResultExponentWidth = 1;
                        fpu.ResultFractionWidth = 0;
                    }
                    break;

                case InstructionCodes.Mul:
                    {
                        FloatingPointCore.EPrecision prec = FloatingPointCore.EPrecision.Single;
                        if (otype.Equals(typeof(float)))
                            prec = FloatingPointCore.EPrecision.Single;
                        else if (otype.Equals(typeof(double)))
                            prec = FloatingPointCore.EPrecision.Double;
                        else
                            return null;
                        fpu = new FloatingPointCore()
                        {
                            Function = FloatingPointCore.EFunction.Multiply,
                            Precision = prec,
                            ResultPrecision = prec
                        };
                        cfg = Config[prec, FloatingPointCore.EFunction.Multiply];
                    }
                    break;

                case InstructionCodes.Div:
                    {
                        FloatingPointCore.EPrecision prec = FloatingPointCore.EPrecision.Single;
                        if (otype.Equals(typeof(float)))
                            prec = FloatingPointCore.EPrecision.Single;
                        else if (otype.Equals(typeof(double)))
                            prec = FloatingPointCore.EPrecision.Double;
                        else
                            return null;
                        fpu = new FloatingPointCore()
                        {
                            Function = FloatingPointCore.EFunction.Divide,
                            Precision = prec,
                            ResultPrecision = prec
                        };
                        cfg = Config[prec, FloatingPointCore.EFunction.Divide];
                    }
                    break;

                case InstructionCodes.Sqrt:
                    {
                        FloatingPointCore.EPrecision prec = FloatingPointCore.EPrecision.Single;
                        if (otype.Equals(typeof(float)))
                            prec = FloatingPointCore.EPrecision.Single;
                        else if (otype.Equals(typeof(double)))
                            prec = FloatingPointCore.EPrecision.Double;
                        else
                            return null;
                        fpu = new FloatingPointCore()
                        {
                            Function = FloatingPointCore.EFunction.SquareRoot,
                            Precision = prec,
                            ResultPrecision = prec
                        };
                        cfg = Config[prec, FloatingPointCore.EFunction.SquareRoot];
                    }
                    break;

                case InstructionCodes.Convert:
                    {
                        FloatingPointCore.EPrecision inprec = FloatingPointCore.EPrecision.Single;
                        bool infloat = false;
                        FixFormat infmt = null;
                        if (otype.Equals(typeof(float)))
                        {
                            inprec = FloatingPointCore.EPrecision.Single;
                            infloat = true;
                        }
                        else if (otype.Equals(typeof(double)))
                        {
                            inprec = FloatingPointCore.EPrecision.Double;
                            infloat = true;
                        }
                        else if (otype.Equals(typeof(SFix)) ||
                            otype.Equals(typeof(Signed)))
                        {
                            inprec = FloatingPointCore.EPrecision.Custom;
                            infloat = false;
                            infmt = SFix.GetFormat(operandTypes[0]);
                        }
                        else
                        {
                            return null;
                        }

                        FloatingPointCore.EPrecision outprec = FloatingPointCore.EPrecision.Single;
                        bool outfloat = false;
                        FixFormat outfmt = null;
                        if (rtype.Equals(typeof(float)))
                        {
                            outprec = FloatingPointCore.EPrecision.Single;
                            outfloat = true;
                        }
                        else if (rtype.Equals(typeof(double)))
                        {
                            outprec = FloatingPointCore.EPrecision.Double;
                            outfloat = true;
                        }
                        else if (rtype.Equals(typeof(SFix)) ||
                            rtype.Equals(typeof(Signed)))
                        {
                            outprec = FloatingPointCore.EPrecision.Custom;
                            outfloat = false;
                            outfmt = SFix.GetFormat(resultTypes[0]);
                        }
                        else
                        {
                            return null;
                        }

                        FloatingPointCore.EFunction func;
                        if (!infloat && !outfloat)
                            return null;
                        else if (infloat && outfloat)
                            func = FloatingPointCore.EFunction.FloatToFloat;
                        else if (infloat)
                            func = FloatingPointCore.EFunction.FloatToFixed;
                        else
                            func = FloatingPointCore.EFunction.FixedToFloat;

                        fpu = new FloatingPointCore()
                        {
                            Function = func,
                            Precision = inprec,
                            ResultPrecision = outprec
                        };
                        if (infmt != null)
                        {
                            fpu.ExponentWidth = infmt.IntWidth;
                            fpu.FractionWidth = infmt.FracWidth;
                        }
                        if (outfmt != null)
                        {
                            fpu.ResultExponentWidth = outfmt.IntWidth;
                            fpu.ResultFractionWidth = outfmt.FracWidth;
                        }
                        FloatingPointCore.EPrecision prec = infloat ? inprec : outprec;
                        cfg = Config[prec, FloatingPointCore.EFunction.Multiply];
                    }
                    break;

                default:
                    return null;
            }
            fpu.TargetDeviceFamily = xproj.DeviceFamily;
            fpu.TargetISEVersion = xproj.ISEVersion;
            if (cfg.EnableDeviceCapabilityDependentDSPUsage)
            {
                switch (fpu.Function)
                {
                    case FloatingPointCore.EFunction.Multiply:
                        switch (xproj.DeviceFamily)
                        {
                            case EDeviceFamily.Spartan3:
                            case EDeviceFamily.Spartan3A_3AN:
                            case EDeviceFamily.Spartan3E:
                            case EDeviceFamily.Automotive_Spartan3:
                            case EDeviceFamily.Automotive_Spartan3A:
                            case EDeviceFamily.Automotive_Spartan3E:
                                if (cfg.DSPUsageRatio < 0.5f)
                                    fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
                                else
                                    fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
                                break;

                            default:
                                if (cfg.DSPUsageRatio < 0.25f)
                                    fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
                                else if (cfg.DSPUsageRatio < 0.50f)
                                    fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.MediumUsage;
                                else if (cfg.DSPUsageRatio < 0.75f)
                                    fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.MaxUsage;
                                else
                                    fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
                                break;
                        }
                        break;

                    case FloatingPointCore.EFunction.AddSubtract:
                        if (fpu.Precision == FloatingPointCore.EPrecision.Custom)
                        {
                            fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
                        }
                        else
                        {
                            switch (xproj.DeviceFamily)
                            {
                                case EDeviceFamily.Virtex4:
                                case EDeviceFamily.Virtex5:
                                case EDeviceFamily.Virtex6:
                                case EDeviceFamily.Virtex6_LowPower:
                                    if (cfg.DSPUsageRatio < 0.5f)
                                        fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
                                    else
                                        fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.FullUsage;
                                    break;

                                default:
                                    fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
                                    break;
                            }
                        }
                        break;

                    default:
                        fpu.DSP48EUsage = FloatingPointCore.EDSP48EUsage.NoUsage;
                        break;
                }
            }
            else
            {
                fpu.DSP48EUsage = cfg.DSP48EUsage;
            }
            fpu.HasCE = cfg.HasCE;
            fpu.HasDivideByZero = cfg.HasDivideByZero;
            fpu.HasInvalidOp = cfg.HasInvalidOp;
            fpu.HasOperationND = cfg.HasOperationND;
            fpu.HasOperationRFD = cfg.HasOperationRFD;
            fpu.HasOverflow = cfg.HasOverflow;
            fpu.HasRdy = cfg.HasRdy;
            fpu.HasSCLR = cfg.HasSCLR;
            if (cfg.UseMaximumLatency)
            {
                fpu.UseMaximumLatency = true;
            }
            else if (cfg.SpecifyLatencyRatio)
            {
                fpu.UseMaximumLatency = false;
                fpu.Latency = (int)(cfg.LatencyRatio * fpu.MaximumLatency);
            }
            else
            {
                fpu.Latency = cfg.Latency;
            }
            IXILMapping result = TryMapOne(fpu.TASite, instr, operandTypes, resultTypes, false);
            Debug.Assert(result != null);
            return result;
        }
    }
}

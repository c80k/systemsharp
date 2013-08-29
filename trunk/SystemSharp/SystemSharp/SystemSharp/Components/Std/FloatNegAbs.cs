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
using System.Linq;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    public interface IFloatNegAbsTransactionSite: 
        ITransactionSite
    {
        IEnumerable<TAVerb> Neg(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result);
        IEnumerable<TAVerb> Abs(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result);
    }

    [DeclareXILMapper(typeof(FloatNegAbsXILMapper))]
    public class FloatNegAbs: Component
    {
        private class TransactionSite :
            IFloatNegAbsTransactionSite
        {
            private FloatNegAbs _host;
            private bool _established;

            public TransactionSite(FloatNegAbs host)
            {
                _host = host;
            }

            private IEnumerable<TAVerb> Do(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                if (_host.PipelineDepth == 0)
                {
                    yield return new TAVerb(this, ETVMode.Locked, () => { },
                        _host.DIn.Dual.Drive(operand).Par(result.Comb.Connect(_host.DOut.Dual.AsSignalSource())));
                }
                else
                {
                    yield return new TAVerb(this, ETVMode.Locked, () => { },
                        _host.DIn.Dual.Drive(operand));
                    for (int i = 1; i < _host.PipelineDepth; i++)
                        yield return new TAVerb(this, ETVMode.Shared, () => { });
                    yield return new TAVerb(this, ETVMode.Shared, () => { }, result.Comb.Connect(_host.DOut.Dual.AsSignalSource()));
                }
            }

            public IEnumerable<TAVerb> Neg(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                if (_host.Operation != EOperation.Neg)
                    throw new NotSupportedException();
                return Do(operand, result);
            }

            public IEnumerable<TAVerb> Abs(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                if (_host.Operation != EOperation.Abs)
                    throw new NotSupportedException();
                return Do(operand, result);
            }

            public Component Host
            {
                get { return _host; }
            }

            public string Name
            {
                get { return "FloatAbsNegTransactionSite"; }
            }

            public IEnumerable<TAVerb> DoNothing()
            {
                yield return new TAVerb(this, ETVMode.Locked, () => { },
                    _host.DIn.Dual.Drive(SignalSource.Create(StdLogicVector.DCs(_host.TotalWidth))));
            }

            public void Establish(IAutoBinder binder)
            {
                if (_established)
                    return;

                var fna = _host;
                fna.Clk = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, StdLogic._0);
                fna.DIn = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "DIn", null, StdLogicVector._0s(fna.TotalWidth));
                fna.DOut = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "DOut", null, StdLogicVector._0s(fna.TotalWidth));

                _established = true;
            }
        }

        public enum EOperation
        {
            Neg,
            Abs
        }

        public In<StdLogic> Clk { private get; set; }
        public In<StdLogicVector> DIn { private get; set; }
        public Out<StdLogicVector> DOut { private get; set; }

        [PerformanceRelevant]
        public int TotalWidth { [StaticEvaluation] get; private set; }

        [PerformanceRelevant]
        public EOperation Operation { [StaticEvaluation] get; private set; }

        [PerformanceRelevant]
        public int PipelineDepth { [StaticEvaluation] get; private set; }

        public override bool IsEquivalent(Component obj)
        {
            var other = obj as FloatNegAbs;
            if (other == null)
                return false;
            return TotalWidth == other.TotalWidth &&
                Operation == other.Operation &&
                PipelineDepth == other.PipelineDepth;
        }

        public override int GetBehaviorHashCode()
        {
            return TotalWidth ^
                Operation.GetHashCode() ^
                PipelineDepth;
        }

        public IFloatNegAbsTransactionSite TASite { get; private set; }

        private SLVSignal _pipeIn;
        private SLVSignal _pipeOut;
        private RegPipe _rpipe;

        public FloatNegAbs(int totalWidth, EOperation operation, int pipelineDepth)
        {
            TotalWidth = totalWidth;
            Operation = operation;
            PipelineDepth = pipelineDepth;

            _pipeIn = new SLVSignal(totalWidth);

            if (pipelineDepth > 0)
            {
                _pipeOut = new SLVSignal(totalWidth);
            }

            TASite = new TransactionSite(this);
        }

        private void NegProcess()
        {
            _pipeIn.Next = (!DIn.Cur[TotalWidth - 1]).Concat(DIn.Cur[TotalWidth - 2, 0]);
        }

        private void AbsProcess()
        {
            _pipeIn.Next = StdLogic._0.Concat(DIn.Cur[TotalWidth - 2, 0]);
        }

        private void DirectFeedProcess()
        {
            DOut.Next = _pipeIn.Cur;
        }

        private void PipeFedProcess()
        {
            DOut.Next = _pipeOut.Cur;
        }

        protected override void PreInitialize()
        {
            if (PipelineDepth > 0)
            {
                _rpipe = new RegPipe(PipelineDepth, TotalWidth)
                {
                    Clk = Clk,
                    Din = _pipeIn,
                    Dout = _pipeOut
                };
            }
        }

        protected override void Initialize()
        {
            switch (Operation)
            {
                case EOperation.Abs:
                    AddProcess(AbsProcess, DIn);
                    break;

                case EOperation.Neg:
                    AddProcess(NegProcess, DIn);
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (PipelineDepth > 0)
                AddProcess(PipeFedProcess, _pipeOut);
            else
                AddProcess(DirectFeedProcess, _pipeIn);
        }
    }

    public class FloatNegAbsXILMapper : IXILMapper
    {
        private class Mapping : IXILMapping
        {
            private FloatNegAbs _host;

            public Mapping(FloatNegAbs host)
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
                get { return 1; }
            }

            public int Latency
            {
                get { return _host.PipelineDepth; }
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                switch (_host.Operation)
                {
                    case FloatNegAbs.EOperation.Abs:
                        return _host.TASite.Abs(operands[0], results[0]);

                    case FloatNegAbs.EOperation.Neg:
                        return _host.TASite.Neg(operands[0], results[0]);

                    default:
                        throw new NotImplementedException();
                }
            }

            public string Description
            {
                get
                {
                    string text = _host.TotalWidth + " bit, " + _host.PipelineDepth + " stage floating-point ";
                    switch (_host.Operation)
                    {
                        case FloatNegAbs.EOperation.Abs:
                            return text + " absolute value";
                        case FloatNegAbs.EOperation.Neg:
                            return text + " negation";
                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Abs();
            yield return DefaultInstructionSet.Instance.Neg();
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            var fna = fu as FloatNegAbs;
            if (fna == null)
                yield break;

            if (operandTypes.Length != 1 || resultTypes.Length != 1)
                yield break;

            int totalWidth;
            if (operandTypes[0].CILType.Equals(typeof(float)) && resultTypes[0].CILType.Equals(typeof(float)))
                totalWidth = 32;
            else if (operandTypes[0].CILType.Equals(typeof(double)) && resultTypes[0].CILType.Equals(typeof(double)))
                totalWidth = 64;
            else
                yield break;

            if (fna.TotalWidth != totalWidth)
                yield break;

            switch (fna.Operation)
            {
                case FloatNegAbs.EOperation.Abs:
                    if (instr.Name != InstructionCodes.Abs)
                        yield break;
                    break;

                case FloatNegAbs.EOperation.Neg:
                    if (instr.Name != InstructionCodes.Neg)
                        yield break;
                    break;

                default:
                    throw new NotImplementedException();
            }
            yield return new Mapping(fna);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            if (operandTypes.Length != 1 || resultTypes.Length != 1)
                return null;

            int totalWidth;
            if (operandTypes[0].CILType.Equals(typeof(float)) && resultTypes[0].CILType.Equals(typeof(float)))
                totalWidth = 32;
            else if (operandTypes[0].CILType.Equals(typeof(double)) && resultTypes[0].CILType.Equals(typeof(double)))
                totalWidth = 64;
            else
                return null;

            FloatNegAbs fna;

            switch (instr.Name)
            {
                case InstructionCodes.Abs:
                    fna = new FloatNegAbs(totalWidth, FloatNegAbs.EOperation.Abs, 0);
                    break;

                case InstructionCodes.Neg:
                    fna = new FloatNegAbs(totalWidth, FloatNegAbs.EOperation.Neg, 0);
                    break;

                default:
                    return null;
            }

            return new Mapping(fna);
        }
    }
}

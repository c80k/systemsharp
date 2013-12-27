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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Assembler;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// Transaction site interface for slicing operations (i.e. cutting out sub-vectors from a bit-vectors)
    /// </summary>
    public interface ISlicerTransactionSite : ITransactionSite
    {
        /// <summary>
        /// Returns a transaction for performing a slice operation
        /// </summary>
        /// <param name="data">source of data to be sliced</param>
        /// <param name="result">sink for receiving the slice</param>
        IEnumerable<TAVerb> Slice(ISignalSource<StdLogicVector> data, ISignalSink<StdLogicVector> result);
    }

    /// <summary>
    /// A functional unit for performing slice operations on bit-vectors. The slice semantics are generalized in that
    /// it is possible to specify slice ranges which exceed the original operand bit-width. This way, the unit can
    /// be used for sign extension.
    /// This component is intended to be used during high-level synthesis for mapping basic arithmetic/logical instructions.
    /// </summary>
    [DeclareXILMapper(typeof(SlicerXILMapper))]
    public class Slicer: Component
    {
        private class SlicerTransactionSite :
            DefaultTransactionSite,
            ISlicerTransactionSite
        {
            private Slicer _host;

            public SlicerTransactionSite(Slicer host) :
                base(host)
            {
                _host = host;
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(ETVMode.Locked, _host.DIn.Dual.Stick(StdLogicVector.DCs(_host.InputWidth)));
            }

            public IEnumerable<TAVerb> Slice(ISignalSource<StdLogicVector> data, ISignalSink<StdLogicVector> result)
            {
                if (result.Sync != null)
                {
                    yield return Verb(ETVMode.Locked, _host.DIn.Dual.Drive(data));
                    yield return Verb(ETVMode.Locked, result.Comb.Connect(_host.DOut.Dual.AsSignalSource()));
                }
                else if (result.Comb != null)
                {
                    yield return Verb(ETVMode.Locked, 
                        _host.DIn.Dual.Drive(data),
                        result.Comb.Connect(_host.DOut.Dual.AsSignalSource()));
                }
            }

            public override void Establish(IAutoBinder binder)
            {
                _host.DIn = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, "DIn", null, StdLogicVector._0s(_host.InputWidth));
                _host.DOut = binder.GetSignal<StdLogicVector>(EPortUsage.Result, "DOut", null, StdLogicVector._0s(_host.HiOffset - _host.LoOffset + 1));
            }
        }

        /// <summary>
        /// Input bit-vector
        /// </summary>
        public In<StdLogicVector> DIn { private get; set; }

        /// <summary>
        /// Output slice
        /// </summary>
        public Out<StdLogicVector> DOut { private get; set; }

        /// <summary>
        /// Bit-width of input vector
        /// </summary>
        [PerformanceRelevant]
        public int InputWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// High slice offset
        /// </summary>
        [PerformanceRelevant]
        public int HiOffset { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Low slice offset
        /// </summary>
        [PerformanceRelevant]
        public int LoOffset { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Whether operand is signed
        /// </summary>
        [PerformanceRelevant]
        public bool IsSigned { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public ISlicerTransactionSite TASite { get; private set; }

        private StdLogicVector _hiPad0;
        private StdLogicVector _hiPad1;
        private StdLogicVector _loPad;
        private int _hiSlice;
        private int _loSlice;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="inputWidth">bit-width of operand</param>
        /// <param name="hiOffset">high slice offset</param>
        /// <param name="loOffset">low slice offset</param>
        /// <param name="signed">whether operand is signed</param>
        public Slicer(int inputWidth, int hiOffset, int loOffset, bool signed)
        {
            Contract.Requires<ArgumentOutOfRangeException>(hiOffset - loOffset >= -1);

            InputWidth = inputWidth;
            HiOffset = hiOffset;
            LoOffset = loOffset;
            IsSigned = signed;
            TASite = new SlicerTransactionSite(this);

            int msb = signed ? inputWidth - 2 : inputWidth - 1;
            int hiPadWidth = Math.Max(hiOffset, msb) - Math.Max(loOffset, msb + 1) + 1;
            int loPadWidth = Math.Min(hiOffset, -1) - Math.Min(loOffset, 0) + 1;
            _hiSlice = Math.Min(msb, hiOffset);
            _loSlice = Math.Max(0, loOffset);
            
            if (_loSlice > _hiSlice)
            {
                // degenerate case: actually no portion of input word is used
                _hiSlice = -1;
                _loSlice = 0;
            }            

            _hiPad0 = StdLogicVector._0s(hiPadWidth);
            _hiPad1 = StdLogicVector._1s(hiPadWidth);
            _loPad = StdLogicVector._0s(loPadWidth);

            Debug.Assert(hiPadWidth + loPadWidth + _hiSlice - _loSlice == hiOffset - loOffset);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="obj"/> is a <c>Slicer</c> with same parametrization.
        /// </summary>
        public override bool IsEquivalent(Component obj)
        {
            var other = obj as Slicer;
            return other != null &&
                InputWidth == other.InputWidth &&
                HiOffset == other.HiOffset &&
                LoOffset == other.LoOffset &&
                IsSigned == other.IsSigned;
        }

        public override int GetBehaviorHashCode()
        {
            return InputWidth ^
                HiOffset ^
                LoOffset ^
                IsSigned.GetHashCode();
        }

        private void ProcessUnsigned()
        {
            DOut.Next = _hiPad0
                .Concat(DIn.Cur[_hiSlice, _loSlice])
                .Concat(_loPad);
        }

        private void ProcessSigned()
        {
            if (DIn.Cur[InputWidth - 1] == '1')
            {
                DOut.Next = _hiPad1
                    .Concat(DIn.Cur[_hiSlice, _loSlice])
                    .Concat(_loPad);            
            }
            else
            {
                DOut.Next = _hiPad0
                    .Concat(DIn.Cur[_hiSlice, _loSlice])
                    .Concat(_loPad);
            }
        }

        private void ProcessZeroOutput()
        {
            DOut.Next = _hiPad0.Concat(_loPad);
        }

        protected override void Initialize()
        {
            Contract.Assert(DIn != null, "DIn unbound");
            Contract.Assert(((ISized)DIn).Size == InputWidth, "Wrong port size: DIn");

            if (_hiSlice < _loSlice)
                AddProcess(ProcessZeroOutput);
            else if (IsSigned)
                AddProcess(ProcessSigned, DIn);
            else
                AddProcess(ProcessUnsigned, DIn);
        }
    }

    /// <summary>
    /// A service for mapping bit-slice XIL instructions to hardware.
    /// </summary>
    public class SlicerXILMapper : IXILMapper
    {
        private class SlicerXILMapping : IXILMapping
        {
            private Slicer _host;

            public SlicerXILMapping(Slicer host)
            {
                _host = host;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _host.TASite.Slice(operands[0], results[0]);
            }

            public ITransactionSite TASite
            {
                get { return _host.TASite; }
            }

            public EMappingKind ResourceKind
            {
                get { return EMappingKind.LightweightResource; }
            }

            public int InitiationInterval
            {
                get { return 1; }
            }

            public int Latency
            {
                get { return 0; }
            }

            public string Description
            {
                get { return _host.InputWidth + " bit " + _host.HiOffset + " downto " + _host.LoOffset + " slicer"; }
            }
        }

        /// <summary>
        /// Returns convert, slicefixi, rempow2
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Convert();
            yield return DefaultInstructionSet.Instance.SliceFixI(default(Range));
            yield return DefaultInstructionSet.Instance.Rempow2(0);
        }

        private bool IsSliceable(TypeDescriptor type)
        {
            if (!type.IsComplete)
                return false;

            return 
                (type.CILType.Equals(typeof(Signed)) ||
                type.CILType.Equals(typeof(Unsigned)) ||
                type.CILType.Equals(typeof(SFix)) ||
                type.CILType.Equals(typeof(UFix)) ||
                type.CILType.Equals(typeof(StdLogicVector)));
        }

        private bool IsSigned(TypeDescriptor type)
        {
            return
                (type.CILType.Equals(typeof(Signed)) ||
                type.CILType.Equals(typeof(SFix)));
        }

        private bool IsUnsigned(TypeDescriptor type)
        {
            return
                (type.CILType.Equals(typeof(Unsigned)) ||
                type.CILType.Equals(typeof(UFix)));
        }

        private bool GetSliceParams(XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes,
            out bool isSigned, out int inputWidth, out int hiOffset, out int loOffset)
        {
            isSigned = false;
            inputWidth = 0;
            hiOffset = 0;
            loOffset = 0;

            if (instr.Name != InstructionCodes.Convert &&
                instr.Name != InstructionCodes.SliceFixI &&
                instr.Name != InstructionCodes.Rempow2)
                return false;

            bool o0fix = IsSigned(operandTypes[0]) || IsUnsigned(operandTypes[0]);
            bool r0fix = IsSigned(resultTypes[0]) || IsUnsigned(resultTypes[0]);
            bool u2s = o0fix && r0fix;
            bool typesEq = operandTypes[0].CILType.Equals(resultTypes[0].CILType);
            if (!u2s && !typesEq)
                return false;
            if (!IsSliceable(operandTypes[0]) || !IsSliceable(resultTypes[0]))
                return false;
            isSigned = IsSigned(operandTypes[0]) && IsSigned(resultTypes[0]);

            inputWidth = TypeLowering.Instance.GetWireWidth(operandTypes[0]);

            switch (instr.Name)
            {
                case InstructionCodes.Convert:
                    {
                        TypeDescriptor inWType = operandTypes[0];
                        TypeDescriptor outWType = resultTypes[0];
                        hiOffset = outWType.Constraints[0].FirstBound - inWType.Constraints[0].SecondBound;
                        loOffset = outWType.Constraints[0].SecondBound - inWType.Constraints[0].SecondBound;
                    }
                    break;

                case InstructionCodes.SliceFixI:
                    {
                        Range range = (Range)instr.Operand;
                        hiOffset = range.FirstBound;
                        loOffset = range.SecondBound;
                    }
                    break;

                case InstructionCodes.Rempow2:
                    {
                        if (!IsSigned(operandTypes[0]) ||
                            !IsSigned(resultTypes[0]))
                            return false;

                        var infmt = operandTypes[0].GetFixFormat();
                        int p2 = (int)instr.Operand;
                        if (infmt.IntWidth > p2 + 1)
                            return false;

                        goto case InstructionCodes.Convert;
                    }

                default:
                    throw new NotImplementedException();
            }

            return true;
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            Slicer slicer = fu as Slicer;
            if (slicer == null)
                yield break;

            bool isSigned;
            int inputWidth, hiOffset, loOffset;
            if (!GetSliceParams(instr, operandTypes, resultTypes, out isSigned, out inputWidth, out hiOffset, out loOffset))
                yield break;

            if (slicer.IsSigned != isSigned)
                yield break;

            if (slicer.InputWidth != inputWidth)
                yield break;

            if (hiOffset != slicer.HiOffset || loOffset != slicer.LoOffset)
                yield break;

            yield return new SlicerXILMapping(slicer);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            bool isSigned;
            int inputWidth, hiOffset, loOffset;
            if (!GetSliceParams(instr, operandTypes, resultTypes, out isSigned, out inputWidth, out hiOffset, out loOffset))
                return null;

            if (inputWidth == 0 || (hiOffset - loOffset + 1) <= 0)
                return null;

            var slicer = new Slicer(inputWidth, hiOffset, loOffset, isSigned);

            return new SlicerXILMapping(slicer);
        }
    }
}

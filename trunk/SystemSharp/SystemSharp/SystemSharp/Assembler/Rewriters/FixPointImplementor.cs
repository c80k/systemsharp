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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    class FixPointImplementorImpl: XILSRewriter
    {
        public FixPointImplementorImpl(IList<XILSInstr> instrs):
            base(instrs)
        {
            SetHandler(InstructionCodes.Neg, HandleNeg);
            SetHandler(InstructionCodes.Sqrt, HandleSqrt);
            SetHandler(InstructionCodes.Sin, HandleSinOrCos);
            SetHandler(InstructionCodes.Cos, HandleSinOrCos);
            SetHandler(InstructionCodes.ScSinCos, HandleScSinCos);
            SetHandler(InstructionCodes.Add, HandleAddSub);
            SetHandler(InstructionCodes.Sub, HandleAddSub);
            SetHandler(InstructionCodes.Mul, HandleMul);
            SetHandler(InstructionCodes.Div, HandleDiv);

            SetHandler(InstructionCodes.IsEq, HandleCmp);
            SetHandler(InstructionCodes.IsGt, HandleCmp);
            SetHandler(InstructionCodes.IsGte, HandleCmp);
            SetHandler(InstructionCodes.IsLt, HandleCmp);
            SetHandler(InstructionCodes.IsLte, HandleCmp);
            SetHandler(InstructionCodes.IsNEq, HandleCmp);
        }

        internal bool HaveXilinxCordic { get; set; }

        private bool IsFix(TypeDescriptor inType, out FixFormat format)
        {
            if (inType.CILType.Equals(typeof(SFix)) ||
                inType.CILType.Equals(typeof(UFix)))
            {
                format = inType.GetFixFormat();
                return true;
            }
            format = null;
            return false;
        }

        private InstructionDependency[] EqualizeTypes(XILSInstr i, bool makeSameSize, TypeDescriptor[] otypes)
        {
            var otype0 = i.OperandTypes[0];
            var otype1 = i.OperandTypes[1];
            FixFormat fmt0, fmt1;
            bool flag0 = IsFix(otype0, out fmt0);
            bool flag1 = IsFix(otype1, out fmt1);
            if (flag0 != flag1)
                throw new InvalidOperationException("Incompatible types");
            var preds = RemapPreds(i.Preds);
            if (flag0)
            {
                otypes[0] = otype0;
                otypes[1] = otype1;
                if (fmt0.IsSigned && !fmt1.IsSigned)
                {
                    var sample = (UFix)otype1.GetSampleInstance();
                    var signed = sample.SFixValue;
                    var stype1 = TypeDescriptor.GetTypeOf(signed);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, otype1, stype1));
                    otype1 = stype1;
                    fmt1 = signed.Format;
                    preds = new InstructionDependency[0];
                    otypes[1] = otype1;
                }
                if (!fmt0.IsSigned && fmt1.IsSigned)
                {
                    var sample = (UFix)otype0.GetSampleInstance();
                    var signed = sample.SFixValue;
                    var stype0 = TypeDescriptor.GetTypeOf(signed);
                    Emit(DefaultInstructionSet.Instance.Dig(1).CreateStk(preds, 2, otype0, otype1, otype1, otype0));
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, otype0, stype0));
                    otype0 = stype0;
                    Emit(DefaultInstructionSet.Instance.Dig(1).CreateStk(preds, 2, otype1, otype0, otype0, otype1));
                    fmt0 = signed.Format;
                    preds = new InstructionDependency[0];
                    otypes[0] = otype0;
                }
                if (makeSameSize)
                {
                    int intWidth = Math.Max(fmt0.IntWidth, fmt1.IntWidth);
                    int fracWidth = Math.Max(fmt0.FracWidth, fmt1.FracWidth);
                    object rsample;
                    if (fmt0.IsSigned)
                        rsample = SFix.FromDouble(0.0, intWidth, fracWidth);
                    else
                        rsample = UFix.FromDouble(0.0, intWidth, fracWidth);
                    var rtype = TypeDescriptor.GetTypeOf(rsample);
                    if (intWidth > fmt0.IntWidth || fracWidth > fmt0.FracWidth)
                    {
                        Emit(DefaultInstructionSet.Instance.Dig(1).CreateStk(preds, 2, otype0, otype1, otype1, otype0));
                        Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, otype0, rtype));
                        Emit(DefaultInstructionSet.Instance.Dig(1).CreateStk(preds, 2, otype1, rtype, rtype, otype1));
                        preds = new InstructionDependency[0];
                    }
                    if (intWidth > fmt1.IntWidth || fracWidth > fmt1.FracWidth)
                    {
                        Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, otype1, rtype));
                    }
                    otypes[0] = rtype;
                    otypes[1] = rtype;
                }
            }
            else
            {
                otypes[0] = i.OperandTypes[0];
                otypes[1] = i.OperandTypes[1];
            }
            return preds;
        }

        private TypeDescriptor GetNativeResultType(XILSInstr i, TypeDescriptor[] otypes)
        {
            Contract.Requires<ArgumentNullException>(i != null);
            Contract.Requires<ArgumentNullException>(otypes != null);
            Contract.Requires<ArgumentException>(i.OperandTypes.Length == otypes.Length);

            var exprs = otypes
                .Select(t => new LiteralReference(new Variable(t)))
                .ToArray();
            Expression rexpr;
            var oldMode = DesignContext.Instance.FixPoint.ArithSizingMode;
            DesignContext.Instance.FixPoint.ArithSizingMode = EArithSizingMode.VHDLCompliant;
            switch (i.Name)
            {
                case InstructionCodes.Add:
                    rexpr = exprs[0] + exprs[1];
                    break;

                case InstructionCodes.Sub:
                    rexpr = exprs[0] - exprs[1];
                    break;

                case InstructionCodes.Mul:
                    rexpr = exprs[0] * exprs[1];
                    break;

                case InstructionCodes.Div:
                    rexpr = exprs[0] / exprs[1];
                    break;

                case InstructionCodes.Neg:
                    rexpr = -exprs[0];
                    break;

                default:
                    throw new NotImplementedException();
            }
            var result = rexpr.ResultType;
            DesignContext.Instance.FixPoint.ArithSizingMode = oldMode;
            return result;
        }

        private void HandleNeg(XILSInstr i)
        {
            var preds = RemapPreds(i.Preds);
            var nrtype = GetNativeResultType(i, i.OperandTypes);
            var otype = i.OperandTypes[0];
            if (otype.CILType.Equals(typeof(UFix)) &&
                nrtype.CILType.Equals(typeof(SFix)))
            {
                var nrformat = nrtype.GetFixFormat();
                var itype = SFix.MakeType(nrformat.IntWidth, nrformat.FracWidth);
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, otype, itype));
                preds = new InstructionDependency[0];
                otype = itype;
            }
            Emit(i.Command.CreateStk(preds, 1, otype, nrtype));
            if (!nrtype.Equals(i.ResultTypes[0]))
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, nrtype, i.ResultTypes[0]));
        }

        private void HandleScSinCos(XILSInstr i)
        {
            var preds = RemapPreds(i.Preds);
            var infmt = i.OperandTypes[0].GetFixFormat();
            var outfmt = i.ResultTypes[0].GetFixFormat();
            if (infmt == null)
            {
                Emit(i.Command.CreateStk(preds, i.OperandTypes, i.ResultTypes));
            }
            else
            {
                var otype = i.OperandTypes[0];
                if (HaveXilinxCordic && infmt.IntWidth != 3)
                {
                    // Xilinx Cordic needs exactly 3 integer bits for operand
                    otype = SFix.MakeType(3, infmt.FracWidth);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(
                        preds, 1, i.OperandTypes[0], otype));
                }
                else if (infmt.IntWidth != 2)
                {
                    // Any reasonable core (e.g. LUT-based implementation) will require 2 integer operand bits
                    otype = SFix.MakeType(2, infmt.FracWidth);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(
                        preds, 1, i.OperandTypes[0], otype));
                }
                preds = new InstructionDependency[0];
                var rtype = i.ResultTypes[0];
                if (outfmt.IntWidth != 2)
                {
                    // we gonna need exactly 2 integer bits for results
                    rtype = SFix.MakeType(2, outfmt.FracWidth);
                }
                Emit(DefaultInstructionSet.Instance.ScSinCos().CreateStk(
                    preds, 1, otype, rtype, rtype));
                if (!rtype.Equals(i.ResultTypes[1]))
                {
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(
                        1, rtype, i.ResultTypes[1]));
                }
                if (!rtype.Equals(i.ResultTypes[0]))
                {
                    Emit(DefaultInstructionSet.Instance.Swap().CreateStk(
                        2, i.ResultTypes[0], rtype, rtype, i.ResultTypes[0]));
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(
                        1, rtype, i.ResultTypes[0]));
                    Emit(DefaultInstructionSet.Instance.Swap().CreateStk(
                        2, rtype, rtype, rtype, rtype));
                }
            }
        }

        private void HandleSqrt(XILSInstr i)
        {
            FixFormat oformat, rformat;
            IsFix(i.OperandTypes[0], out oformat);
            IsFix(i.ResultTypes[0], out rformat);
            var preds = RemapPreds(i.Preds);
            if (oformat == null)
            {
                Emit(i.Command.CreateStk(preds, i.OperandTypes, i.ResultTypes));
            }
            else
            {
                var nrtype = UFix.MakeType((oformat.IntWidth + 2) / 2, rformat.FracWidth);
                if (oformat.IntWidth % 2 == 0)
                {
                    var itype = UFix.MakeType(oformat.IntWidth + 1, oformat.FracWidth);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, i.OperandTypes[0], itype));
                    Emit(i.Command.CreateStk(1, itype, nrtype));
                }
                else
                {
                    Emit(i.Command.CreateStk(1, i.OperandTypes[0], nrtype));
                }
                if (!nrtype.Equals(i.ResultTypes[0]))
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, nrtype, i.ResultTypes[0]));
            }
        }

        private void HandleSinOrCos(XILSInstr i)
        {
            FixFormat oformat, rformat;
            IsFix(i.OperandTypes[0], out oformat);
            IsFix(i.ResultTypes[0], out rformat);
            var preds = RemapPreds(i.Preds);
            if (oformat == null)
            {
                Emit(i.Command.CreateStk(preds, 1, i.OperandTypes[0], i.ResultTypes[0]));
            }
            else
            {
                TypeDescriptor otype = i.OperandTypes[0];
                if (!i.OperandTypes[0].CILType.Equals(typeof(SFix)))
                {
                    var itype = SFix.MakeType(oformat.IntWidth + 1, oformat.FracWidth);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, i.OperandTypes, i.ResultTypes));
                    preds = new InstructionDependency[0];
                    otype = itype;
                }
                var nrtype = SFix.MakeType(2, rformat.FracWidth);
                Emit(i.Command.CreateStk(preds, 1, otype, nrtype));
                if (!i.ResultTypes[0].Equals(nrtype))
                {
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, nrtype, i.ResultTypes[0]));
                }
            }
        }

        private void HandleAddSub(XILSInstr i)
        {
            var otypes = new TypeDescriptor[2];
            var preds = EqualizeTypes(i, true, otypes);

            FixFormat rformat;
            IsFix(i.ResultTypes[0], out rformat);
            var nrtype = GetNativeResultType(i, otypes);
            Emit(i.Command.CreateStk(preds, 2, otypes[0], otypes[1], nrtype));
            if (!nrtype.Equals(i.ResultTypes[0]))
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, nrtype, i.ResultTypes[0]));
        }

        private void HandleMul(XILSInstr i)
        {
            var otypes = new TypeDescriptor[2];
            var preds = EqualizeTypes(i, false, otypes);

            FixFormat rformat;
            IsFix(i.ResultTypes[0], out rformat);
            var nrtype = GetNativeResultType(i, otypes);
            Emit(i.Command.CreateStk(preds, 2, otypes[0], otypes[1], nrtype));
            if (!nrtype.Equals(i.ResultTypes[0]))
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, nrtype, i.ResultTypes[0]));
        }

        private void HandleDiv(XILSInstr i)
        {
            var preds = i.Preds;
            var fmtDividend = i.OperandTypes[0].GetFixFormat();
            var fmtDivisor = i.OperandTypes[1].GetFixFormat();
            var fmtQuotient = i.ResultTypes[0].GetFixFormat();

            if (fmtDividend == null ||
                fmtDivisor == null ||
                fmtQuotient == null)
            {
                ProcessDefault(i);
                return;
            }

            if (!fmtDivisor.IsSigned)
            {
                // Xilinx divider wants it signed
                var signedType = SFix.MakeType(fmtDivisor.IntWidth + 1, fmtDivisor.FracWidth);
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, i.OperandTypes[1], signedType));
                var newi = i.Command.CreateStk(2, i.OperandTypes[0], signedType, i.ResultTypes[0]);
                HandleDiv(newi);
                return;
            }

            if (!fmtDividend.IsSigned)
            {
                // Xilinx divider wants it signed
                var signedType = SFix.MakeType(fmtDividend.IntWidth + 1, fmtDividend.FracWidth);
                Emit(DefaultInstructionSet.Instance.Swap().CreateStk(preds, 2, 
                    i.OperandTypes[0], i.OperandTypes[1],
                    i.OperandTypes[1], i.OperandTypes[0]));
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, i.OperandTypes[0], signedType));
                Emit(DefaultInstructionSet.Instance.Swap().CreateStk(2,
                    i.OperandTypes[1], signedType,
                    signedType, i.OperandTypes[1]));
                var newi = i.Command.CreateStk(2, signedType, i.OperandTypes[1], i.ResultTypes[0]);
                HandleDiv(newi);
                return;
            }

            if (!fmtQuotient.IsSigned)
            {
                // Xilinx divider wants it signed
                var signedType = SFix.MakeType(fmtQuotient.IntWidth + 1, fmtQuotient.FracWidth);
                var newi = i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], signedType);
                HandleDiv(newi);
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, signedType, i.ResultTypes[0]));
                return;
            }

            if (fmtDividend.TotalWidth < 4 ||
                fmtDivisor.TotalWidth < 4)
            {
                // Xilinx fixed point divider doesn't like divisions of less than 4 bits
                throw new NotImplementedException("Encountered fixed point division with less than 4 bits of either dividend or divisor. This is not supported, please adjust the division!");

                /*Emit(DefaultInstructionSet.Instance.Swap().CreateStk(preds, 2, 
                    i.OperandTypes[0], i.OperandTypes[1],
                    i.OperandTypes[1], i.OperandTypes[0]));
                int delta = 4 - fmtDividend.TotalWidth - 4;
                var newDividendType = fmtDividend.IsSigned ?
                    SFix.MakeType(
                Emit(i.Command.CreateStk(1, i.OperandTypes[1]*/
            }

            int hwQuotientTotalWidth = fmtDividend.TotalWidth;
            int hwQuotientFracWidth = fmtDividend.FracWidth - fmtDivisor.FracWidth;
            object hwQuotSample = fmtDividend.IsSigned ?
                (object)SFix.FromDouble(0.0, hwQuotientTotalWidth - hwQuotientFracWidth, hwQuotientFracWidth) :
                (object)UFix.FromDouble(0.0, hwQuotientTotalWidth - hwQuotientFracWidth, hwQuotientFracWidth);
            var hwQuotType = TypeDescriptor.GetTypeOf(hwQuotSample);
            TypeDescriptor hwQuotAndFracType;

            if (hwQuotientFracWidth >= fmtQuotient.FracWidth)
            {
                Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], hwQuotType));
                hwQuotAndFracType = hwQuotType;
            }
            else
            {
                int fracWidth = fmtQuotient.FracWidth - hwQuotientFracWidth;
                if (fracWidth > 54)
                {
                    // Xilinx divider doesn't like fractional width > 54
                    throw new NotImplementedException("Encountered fixed point division with more than 54 bits of fractional width. This is not supported, please adjust the division!");
                }
                var hwFracSample = UFix.FromDouble(0.0, -hwQuotientFracWidth, fmtQuotient.FracWidth);
                var hwFracType = TypeDescriptor.GetTypeOf(hwFracSample);
                Emit(DefaultInstructionSet.Instance.DivQF().CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], hwQuotType, hwFracType));
                var hwQuotSLV = Marshal.SerializeForHW(hwQuotSample);
                var hwFracSLV = Marshal.SerializeForHW(hwFracSample);
                var hwQuotSLVType = TypeDescriptor.GetTypeOf(hwQuotSLV);
                var hwFracSLVType = TypeDescriptor.GetTypeOf(hwFracSLV);
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, hwFracType, hwFracSLVType));
                Emit(DefaultInstructionSet.Instance.Dig(1).CreateStk(2, hwQuotType, hwFracSLVType, hwFracSLVType, hwQuotType));
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, hwQuotType, hwQuotSLVType));
                Emit(DefaultInstructionSet.Instance.Dig(1).CreateStk(2, hwFracSLVType, hwQuotSLVType, hwQuotSLVType, hwFracSLVType));
                var hwConcSLV = hwQuotSLV.Concat(hwFracSLV);
                var hwConcSLVType = TypeDescriptor.GetTypeOf(hwConcSLV);
                Emit(DefaultInstructionSet.Instance.Concat().CreateStk(2, hwQuotSLVType, hwFracSLVType, hwConcSLVType));
                object hwConc;
                if (fmtDividend.IsSigned)
                    hwConc = SFix.FromSigned(hwConcSLV.SignedValue, fmtQuotient.FracWidth);
                else
                    hwConc = UFix.FromUnsigned(hwConcSLV.UnsignedValue, fmtQuotient.FracWidth);
                var hwConcType = TypeDescriptor.GetTypeOf(hwConc);
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, hwConcSLVType, hwConcType));
                hwQuotAndFracType = hwConcType;
            }
            if (!hwQuotAndFracType.Equals(i.ResultTypes[0]))
            {
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, hwQuotAndFracType, i.ResultTypes[0]));
            }
        }

        private void HandleCmp(XILSInstr i)
        {
            var otypes = new TypeDescriptor[2];
            var preds = EqualizeTypes(i, true, otypes);
            Emit(i.Command.CreateStk(preds, 2, otypes[0], otypes[1], i.ResultTypes[0]));
        }
    }

    public class FixPointImplementor: IXILSRewriter
    {
        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            var impl = new FixPointImplementorImpl(instrs)
            {
                HaveXilinxCordic = this.HaveXilinxCordic
            };
            impl.Rewrite();
            return impl.OutInstructions;
        }

        public bool HaveXilinxCordic { get; set; }

        public override string ToString()
        {
            return "FixedPoint";
        }
    }
}

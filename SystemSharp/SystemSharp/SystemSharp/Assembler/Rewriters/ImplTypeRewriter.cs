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
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    class ImplTypeRewriterImpl : XILSRewriter
    {
        private static bool IsFixed(TypeDescriptor t)
        {
            return t.CILType.Equals(typeof(SFix)) ||
                t.CILType.Equals(typeof(UFix));
        }

        private static bool IsFloat(TypeDescriptor t)
        {
            return t.CILType.Equals(typeof(double)) ||
                t.CILType.Equals(typeof(float));
        }

        private static bool IsSFix(TypeDescriptor t)
        {
            return t.CILType.Equals(typeof(SFix));
        }

        private static bool IsUFix(TypeDescriptor t)
        {
            return t.CILType.Equals(typeof(UFix));
        }

        private static bool IsSigned(TypeDescriptor t)
        {
            return t.CILType.Equals(typeof(Signed));
        }

        private static bool IsUnsigned(TypeDescriptor t)
        {
            return t.CILType.Equals(typeof(Unsigned));
        }

        private static bool IsSLV(TypeDescriptor t)
        {
            return t.CILType.Equals(typeof(StdLogicVector));
        }

        private static TypeDescriptor MakeUFixSFix(TypeDescriptor t)
        {
            var fmt = UFix.GetFormat(t);
            var smp = SFix.FromDouble(0.0, fmt.IntWidth + 1, fmt.FracWidth);
            return TypeDescriptor.GetTypeOf(smp);
        }

        /// <summary>
        /// Returns a type which is able to represent either of two given types without loss of precision
        /// </summary>
        /// <param name="td1">first given type</param>
        /// <param name="td2">second given type</param>
        /// <returns></returns>
        private static TypeDescriptor GetCommonType(TypeDescriptor td1, TypeDescriptor td2)
        {
            if (td1.Equals(td2))
                return td1;

            if (IsSFix(td1) && IsUFix(td2))
            {
                var fmt1 = SFix.GetFormat(td1);
                var fmt2 = UFix.GetFormat(td2);
                return SFix.MakeType(
                    Math.Max(fmt1.IntWidth, fmt2.IntWidth + 1),
                    Math.Max(fmt1.FracWidth, fmt2.FracWidth));
            }
            else if (IsUFix(td1) && IsSFix(td2))
            {
                return GetCommonType(td2, td1);
            }
            else if (IsSFix(td1) && IsSFix(td2))
            {
                var fmt1 = SFix.GetFormat(td1);
                var fmt2 = SFix.GetFormat(td2);
                return SFix.MakeType(
                    Math.Max(fmt1.IntWidth, fmt2.IntWidth),
                    Math.Max(fmt1.FracWidth, fmt2.FracWidth));
            }
            else if (IsUFix(td1) && IsUFix(td2))
            {
                var fmt1 = UFix.GetFormat(td1);
                var fmt2 = UFix.GetFormat(td2);
                return UFix.MakeType(
                    Math.Max(fmt1.IntWidth, fmt2.IntWidth),
                    Math.Max(fmt1.FracWidth, fmt2.FracWidth));
            }
            else if (IsSigned(td1))
            {
                var fmt = SFix.GetFormat(td1);
                var td1x = SFix.MakeType(fmt.IntWidth, fmt.FracWidth);
                return GetCommonType(td1x, td2);
            }
            else if (IsSigned(td2))
            {
                return GetCommonType(td2, td1);
            }
            else if (IsUnsigned(td1))
            {
                var fmt = UFix.GetFormat(td1);
                var td1x = UFix.MakeType(fmt.IntWidth, fmt.FracWidth);
                return GetCommonType(td1x, td2);
            }
            else if (IsUnsigned(td2))
            {
                return GetCommonType(td2, td1);
            }
            else
            {
                throw new NotSupportedException(
                    "Cannot determine common type between " +
                    td1.ToString() + " and " + td2.ToString());
            }
        }

        public ImplTypeRewriterImpl(IList<XILSInstr> instrs) :
            base(instrs)
        {
            SetHandler(InstructionCodes.Add, ProcessAddSub);

            SetHandler(InstructionCodes.Convert, ProcessConvert);

            SetHandler(InstructionCodes.Dig, ProcessDig);
            SetHandler(InstructionCodes.Div, ProcessDiv);
            SetHandler(InstructionCodes.DivQF, KeepAsIs);
            SetHandler(InstructionCodes.Dup, KeepAsIs);

            SetHandler(InstructionCodes.Ld0, ProcessLd0);
            SetHandler(InstructionCodes.LdConst, ProcessLoadConstant);
            SetHandler(InstructionCodes.LdelemFixA, ProcessLdelem);
            SetHandler(InstructionCodes.LdelemFixAFixI, ProcessLdelem);
            SetHandler(InstructionCodes.LoadVar, ProcessLoadVariable);

            SetHandler(InstructionCodes.Mul, ProcessMul);

            SetHandler(InstructionCodes.Select, ProcessSelect);
            SetHandler(InstructionCodes.Sign, ProcessSign);
            SetHandler(InstructionCodes.Slice, ProcessSlice);
            SetHandler(InstructionCodes.SliceFixI, ProcessSlice);
            SetHandler(InstructionCodes.StoreVar, ProcessStoreVariable);
            SetHandler(InstructionCodes.Sub, ProcessAddSub);
            SetHandler(InstructionCodes.Swap, ProcessDig);
            SetHandler(InstructionCodes.ScSinCos, KeepAsIs);
        }

        private Dictionary<string, Variable> _localDic = new Dictionary<string, Variable>();

        public IEnumerable<Variable> Locals
        {
            get { return _localDic.Values; }
        }

        private InstructionDependency[] Convert(InstructionDependency[] preds, TypeDescriptor from, TypeDescriptor to)
        {
            if (from.Equals(to))
            {
                // nothing to do
                return preds;
            }
            else if (IsSFix(from) && IsUFix(to))
            {
                var fromFmt = SFix.GetFormat(from);
                var toFmt = UFix.GetFormat(to);
                int interIW = toFmt.IntWidth + 1;
                int interFW = toFmt.FracWidth;
                if (interIW != fromFmt.IntWidth ||
                    interFW != fromFmt.FracWidth)
                {
                    var interType = SFix.MakeType(interIW, interFW);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, interType));
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, interType, to));
                }
                else
                {
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, to));
                }
                return new InstructionDependency[0];
            }
            else if (IsUFix(from) && IsSFix(to))
            {
                var fromFmt = UFix.GetFormat(from);
                var toFmt = SFix.GetFormat(to);
                int interIW = toFmt.IntWidth - 1;
                int interFW = toFmt.FracWidth;
                if (interIW != fromFmt.IntWidth ||
                    interFW != fromFmt.FracWidth)
                {
                    var interType = UFix.MakeType(interIW, interFW);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, interType));
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(1, interType, to));
                }
                else
                {
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, to));
                }
                return new InstructionDependency[0];
            }
            else if (IsSLV(from))
            {
                int wfrom = TypeLowering.Instance.GetWireWidth(from);
                int wto = TypeLowering.Instance.GetWireWidth(to);
                if (wfrom == wto)
                {
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, to));
                }
                else
                {
                    var interType = StdLogicVector.MakeType(wto);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, interType));
                    Convert(interType, to);
                }
                return new InstructionDependency[0];
            }
            else if (IsSLV(to))
            {
                int wfrom = TypeLowering.Instance.GetWireWidth(from);
                int wto = TypeLowering.Instance.GetWireWidth(to);
                if (wfrom == wto)
                {
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, to));
                }
                else
                {
                    var interType = StdLogicVector.MakeType(wfrom);
                    Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, interType));
                    Convert(interType, to);
                }
                return new InstructionDependency[0];
            }
            else
            {
                Emit(DefaultInstructionSet.Instance.Convert().CreateStk(preds, 1, from, to));
                return new InstructionDependency[0];
            }
        }

        private void Convert(TypeDescriptor from, TypeDescriptor to)
        {
            Convert(new InstructionDependency[0], from, to);
        }

        private void Swap(InstructionDependency[] preds)
        {
            Emit(DefaultInstructionSet.Instance.Dig(1).CreateStk(preds, 2,
                TypeStack.ElementAt(1), TypeStack.Peek(),
                TypeStack.Peek(), TypeStack.ElementAt(1)));
        }

        private void Swap()
        {
            Swap(new InstructionDependency[0]);
        }

        private void Dig(InstructionDependency[] preds, int index)
        {
            var otypes = new TypeDescriptor[index + 1];
            var rtypes = new TypeDescriptor[index + 1];
            for (int i = 0; i < otypes.Length; i++)
            {
                otypes[i] = TypeStack.ElementAt(index - i);
                rtypes[i] = TypeStack.ElementAt(i == index ? index : index - i - 1);
            }
            Emit(DefaultInstructionSet.Instance.Dig(index).CreateStk(preds, otypes, rtypes));
        }

        private void Dig(int index)
        {
            Dig(new InstructionDependency[0], index);
        }

        private void ProcessAddSub(XILSInstr i)
        {
            var preds = RemapPreds(i.Preds);

            if (IsFixed(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]))
            {
                if (IsSFix(i.OperandTypes[0]) &&
                    IsUFix(i.OperandTypes[1]))
                {
                    var sfixt = MakeUFixSFix(i.OperandTypes[1]);
                    Convert(preds, i.OperandTypes[1], sfixt);
                    var inew = i.Command.CreateStk(2, i.OperandTypes[0], sfixt, i.ResultTypes[0]);
                    ProcessAddSub(inew);
                }
                else if (IsUFix(i.OperandTypes[0]) &&
                    IsSFix(i.OperandTypes[1]))
                {
                    var sfixt = MakeUFixSFix(i.OperandTypes[0]);
                    Swap(preds);
                    Convert(i.OperandTypes[0], sfixt);
                    Swap();
                    var inew = i.Command.CreateStk(2, sfixt, i.OperandTypes[1], i.ResultTypes[0]);
                    ProcessAddSub(inew);
                }
                else if (IsSFix(i.OperandTypes[0]) &&
                    IsSFix(i.OperandTypes[1]))
                {
                    var fmt0 = SFix.GetFormat(i.OperandTypes[0]);
                    var fmt1 = SFix.GetFormat(i.OperandTypes[1]);
                    int iw = Math.Max(fmt0.IntWidth, fmt1.IntWidth);
                    int fw = Math.Max(fmt0.FracWidth, fmt1.FracWidth);
                    var smp = SFix.FromDouble(0.0, iw, fw);
                    var to = TypeDescriptor.GetTypeOf(smp);
                    var fmte = SFix.GetFormat(to);
                    if (!fmte.Equals(fmt1))
                    {
                        Convert(preds, i.OperandTypes[1], to);
                        var inew = i.Command.CreateStk(2, i.OperandTypes[0], to, i.ResultTypes[0]);
                        ProcessAddSub(inew);
                    }
                    else if (!fmte.Equals(fmt0))
                    {
                        Swap(preds);
                        Convert(i.OperandTypes[0], to);
                        Swap();
                        var inew = i.Command.CreateStk(2, to, i.OperandTypes[1], i.ResultTypes[0]);
                        ProcessAddSub(inew);
                    }
                    else
                    {
                        dynamic s0 = i.OperandTypes[0].GetSampleInstance();
                        dynamic s1 = i.OperandTypes[1].GetSampleInstance();
                        object r = s0 + s1;
                        var rtype = TypeDescriptor.GetTypeOf(r);
                        Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], rtype));
                        if (!rtype.Equals(i.ResultTypes[0]))
                        {
                            Convert(rtype, i.ResultTypes[0]);
                        }
                    }
                }
                else if (IsUFix(i.OperandTypes[0]) &&
                    IsUFix(i.OperandTypes[1]) &&
                    IsSFix(i.ResultTypes[0]))
                {
                    var sfixt = MakeUFixSFix(i.OperandTypes[1]);
                    Convert(preds, i.OperandTypes[1], sfixt);
                    var inew = i.Command.CreateStk(2, i.OperandTypes[0], sfixt, i.ResultTypes[0]);
                    ProcessAddSub(inew);
                }
                else if (IsUFix(i.OperandTypes[0]) &&
                    IsUFix(i.OperandTypes[1]))
                {
                    var fmt0 = UFix.GetFormat(i.OperandTypes[0]);
                    var fmt1 = UFix.GetFormat(i.OperandTypes[1]);
                    int iw = Math.Max(fmt0.IntWidth, fmt1.IntWidth);
                    int fw = Math.Max(fmt0.FracWidth, fmt1.FracWidth);
                    var smp = UFix.FromDouble(0.0, iw, fw);
                    var to = TypeDescriptor.GetTypeOf(smp);
                    var fmte = UFix.GetFormat(to);
                    if (!fmte.Equals(fmt1))
                    {
                        Convert(preds, i.OperandTypes[1], to);
                        var inew = i.Command.CreateStk(2, i.OperandTypes[0], to, i.ResultTypes[0]);
                        ProcessAddSub(inew);
                    }
                    else if (!fmte.Equals(fmt0))
                    {
                        Swap(preds);
                        Convert(preds, i.OperandTypes[0], to);
                        Swap();
                        var inew = i.Command.CreateStk(2, to, i.OperandTypes[1], i.ResultTypes[0]);
                        ProcessAddSub(inew);
                    }
                    else
                    {
                        dynamic s0 = i.OperandTypes[0].GetSampleInstance();
                        dynamic s1 = i.OperandTypes[1].GetSampleInstance();
                        object r = s0 + s1;
                        var rtype = TypeDescriptor.GetTypeOf(r);
                        Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], rtype));
                        if (!rtype.Equals(i.ResultTypes[0]))
                        {
                            Convert(rtype, i.ResultTypes[0]);
                        }
                    }
                }
                else
                {
                    Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], i.ResultTypes[0]));
                }
            }
            else if (IsFloat(i.OperandTypes[0]) && IsFloat(i.OperandTypes[1]))
            {
                dynamic s0 = i.OperandTypes[0].GetSampleInstance();
                dynamic s1 = i.OperandTypes[1].GetSampleInstance();
                object r = s0 + s1;
                var rtype = TypeDescriptor.GetTypeOf(r);
                Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], rtype));
                if (!rtype.Equals(i.ResultTypes[0]))
                {
                    Convert(rtype, i.ResultTypes[0]);
                }
            }
            else if (IsFixed(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]) &&
                IsFixed(i.ResultTypes[0]))
            {
                Convert(preds, i.OperandTypes[1], i.OperandTypes[0]);
                var inew = i.Command.CreateStk(2, i.OperandTypes[0], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessAddSub(inew);
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]) &&
                IsFixed(i.ResultTypes[0]))
            {
                Swap(preds);
                Convert(i.OperandTypes[0], i.OperandTypes[1]);
                Swap();
                var inew = i.Command.CreateStk(2, i.OperandTypes[1], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessAddSub(inew);
            }
            else if (IsFixed(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]) &&
                IsFloat(i.ResultTypes[0]))
            {
                Swap(preds);
                Convert(i.OperandTypes[0], i.OperandTypes[1]);
                Swap();
                var inew = i.Command.CreateStk(2, i.OperandTypes[1], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessAddSub(inew);
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]) &&
                IsFloat(i.ResultTypes[0]))
            {
                Convert(preds, i.OperandTypes[1], i.OperandTypes[0]);
                var inew = i.Command.CreateStk(2, i.OperandTypes[0], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessAddSub(inew);
            }
            else if (IsSLV(i.OperandTypes[1]))
            {
                var signedType = SFix.MakeType(StdLogicVector.GetLength(i.OperandTypes[1]), 0);
                Convert(preds, i.OperandTypes[1], signedType);
                var inew = i.Command.CreateStk(2, i.OperandTypes[0], signedType, i.ResultTypes[0]);
                ProcessAddSub(inew);
            }
            else if (IsSLV(i.OperandTypes[0]))
            {
                var signedType = SFix.MakeType(StdLogicVector.GetLength(i.OperandTypes[0]), 0);
                Swap(preds);
                Convert(i.OperandTypes[0], signedType);
                Swap();
                var inew = i.Command.CreateStk(2, signedType, i.OperandTypes[1], i.ResultTypes[0]);
                ProcessAddSub(inew);
            }
            else
            {
                Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], i.ResultTypes[0]));
            }
        }

        private void ProcessMul(XILSInstr i)
        {
            var preds = RemapPreds(i.Preds);

            if (IsFixed(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]))
            {
                if (IsSFix(i.OperandTypes[0]) &&
                    IsUFix(i.OperandTypes[1]))
                {
                    var sfixt = MakeUFixSFix(i.OperandTypes[1]);
                    Convert(preds, i.OperandTypes[1], sfixt);
                    var inew = i.Command.CreateStk(2, i.OperandTypes[0], sfixt, i.ResultTypes[0]);
                    ProcessMul(inew);
                }
                else if (IsUFix(i.OperandTypes[0]) &&
                    IsSFix(i.OperandTypes[1]))
                {
                    Swap(preds);
                    var inew = i.Command.CreateStk(2, i.OperandTypes[1], i.OperandTypes[0], i.ResultTypes[0]);
                    ProcessMul(inew);
                }
                else
                {
                    dynamic s0 = i.OperandTypes[0].GetSampleInstance();
                    dynamic s1 = i.OperandTypes[1].GetSampleInstance();
                    object r = s0 * s1;
                    var rtype = TypeDescriptor.GetTypeOf(r);
                    Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], rtype));
                    if (!rtype.Equals(i.ResultTypes[0]))
                    {
                        Convert(rtype, i.ResultTypes[0]);
                    }
                }
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]))
            {
                dynamic s0 = i.OperandTypes[0].GetSampleInstance();
                dynamic s1 = i.OperandTypes[1].GetSampleInstance();
                object r = s0 * s1;
                var rtype = TypeDescriptor.GetTypeOf(r);
                Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], rtype));
                if (!rtype.Equals(i.ResultTypes[0]))
                {
                    Convert(rtype, i.ResultTypes[0]);
                }
            }
            else if (IsFixed(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]) &&
                IsFixed(i.ResultTypes[0]))
            {
                Convert(preds, i.OperandTypes[1], i.OperandTypes[0]);
                var inew = i.Command.CreateStk(2, i.OperandTypes[0], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessMul(inew);
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]) &&
                IsFixed(i.ResultTypes[0]))
            {
                Swap(preds);
                var inew = i.Command.CreateStk(2, i.OperandTypes[1], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessMul(inew);
            }
            else if (IsFixed(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]) &&
                IsFloat(i.ResultTypes[0]))
            {
                Swap(preds);
                var inew = i.Command.CreateStk(2, i.OperandTypes[1], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessMul(inew);
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]) &&
                IsFloat(i.ResultTypes[0]))
            {
                Convert(preds, i.OperandTypes[1], i.OperandTypes[0]);
                var inew = i.Command.CreateStk(2, i.OperandTypes[0], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessMul(inew);
            }
            else
            {
                // default handling: keep instruction as is.
                Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], i.ResultTypes[0]));
            }
        }

        private void ProcessDiv(XILSInstr i)
        {
            var preds = RemapPreds(i.Preds);

            if (IsFixed(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]))
            {
                if (IsSFix(i.OperandTypes[0]) &&
                    IsUFix(i.OperandTypes[1]))
                {
                    var sfixt = MakeUFixSFix(i.OperandTypes[1]);
                    Convert(preds, i.OperandTypes[1], sfixt);
                    var inew = i.Command.CreateStk(2, i.OperandTypes[0], sfixt, i.ResultTypes[0]);
                    ProcessDiv(inew);
                }
                else if (IsUFix(i.OperandTypes[0]) &&
                    IsSFix(i.OperandTypes[1]))
                {
                    var sfixt = MakeUFixSFix(i.OperandTypes[1]);
                    Swap(preds);
                    Convert(i.OperandTypes[0], sfixt);
                    Swap();
                    var inew = i.Command.CreateStk(2, sfixt, i.OperandTypes[1], i.ResultTypes[0]);
                    ProcessDiv(inew);
                }
                else
                {
                    dynamic s0 = i.OperandTypes[0].GetSampleInstance();
                    dynamic s1 = i.OperandTypes[1].GetSampleInstance(ETypeCreationOptions.NonZero);
                    object r = s0 / s1;
                    var rtype = TypeDescriptor.GetTypeOf(r);
                    Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], rtype));
                    if (!rtype.Equals(i.ResultTypes[0]))
                    {
                        Convert(rtype, i.ResultTypes[0]);
                    }
                }
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]))
            {
                dynamic s0 = i.OperandTypes[0].GetSampleInstance();
                dynamic s1 = i.OperandTypes[1].GetSampleInstance(ETypeCreationOptions.MultiplicativeNeutral);
                object r = s0 / s1;
                var rtype = TypeDescriptor.GetTypeOf(r);
                Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], rtype));
                if (!rtype.Equals(i.ResultTypes[0]))
                {
                    Convert(rtype, i.ResultTypes[0]);
                }
            }
            else if (IsFixed(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]) &&
                IsFixed(i.ResultTypes[0]))
            {
                Convert(preds, i.OperandTypes[1], i.OperandTypes[0]);
                var inew = i.Command.CreateStk(2, i.OperandTypes[0], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessDiv(inew);
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]) &&
                IsFixed(i.ResultTypes[0]))
            {
                Swap(preds);
                Convert(i.OperandTypes[0], i.ResultTypes[0]);
                Swap();
                var inew = i.Command.CreateStk(2, i.ResultTypes[0], i.OperandTypes[1], i.ResultTypes[0]);
                ProcessDiv(inew);
            }
            else if (IsFixed(i.OperandTypes[0]) &&
                IsFloat(i.OperandTypes[1]) &&
                IsFloat(i.ResultTypes[0]))
            {
                Swap(preds);
                Convert(i.OperandTypes[0], i.OperandTypes[1]);
                Swap();
                var inew = i.Command.CreateStk(2, i.OperandTypes[1], i.OperandTypes[1], i.ResultTypes[0]);
                ProcessDiv(inew);
            }
            else if (IsFloat(i.OperandTypes[0]) &&
                IsFixed(i.OperandTypes[1]) &&
                IsFloat(i.ResultTypes[0]))
            {
                Convert(preds, i.OperandTypes[1], i.OperandTypes[0]);
                var inew = i.Command.CreateStk(2, i.OperandTypes[0], i.OperandTypes[0], i.ResultTypes[0]);
                ProcessDiv(inew);
            }
            else
            {
                // default handling: keep instruction as is.
                Emit(i.Command.CreateStk(preds, 2, i.OperandTypes[0], i.OperandTypes[1], i.ResultTypes[0]));
            }
        }

        protected override void ProcessInstruction(XILSInstr i)
        {
            var rTypes = i.ResultTypes;
            var opTypes = new TypeDescriptor[i.OperandTypes.Length];
            for (int j = 0; j < opTypes.Length; j++)
                opTypes[j] = TypeStack.ElementAt(opTypes.Length - 1 - j);
            var inew = i.Command.CreateStk(i.Preds, opTypes, rTypes);
            base.ProcessInstruction(inew);
        }

        private TypeDescriptor GetNearestType(IEnumerable<TypeDescriptor> types, TypeDescriptor type)
        {
            var candidates = types.Where(t => t.CILType.Equals(type.CILType));
            if (type.CILType.Equals(typeof(SFix)) ||
                type.CILType.Equals(typeof(UFix)) ||
                type.CILType.Equals(typeof(Signed)) ||
                type.CILType.Equals(typeof(Unsigned)))
            {
                var fmt = type.GetFixFormat();
                candidates = candidates.OrderBy(t =>
                    Math.Abs(t.GetFixFormat().IntWidth - fmt.IntWidth));
                candidates = candidates.OrderBy(t =>
                    Math.Abs(t.GetFixFormat().FracWidth - fmt.FracWidth));
            }
            var result = candidates.FirstOrDefault();
            if (result == null)
                result = types.FirstOrDefault();
            return result;
        }

        private void ProcessLd0(XILSInstr i)
        {
            var targetConst = TypeConversions.ConvertValue(0, i.ResultTypes[0]);
            Emit(new XILSInstr(DefaultInstructionSet.Instance.LdConst(targetConst), 
                RemapPreds(i.Preds), 
                i.OperandTypes, i.ResultTypes));
        }

        private void KeepAsIs(XILSInstr i)
        {
            base.ProcessDefault(i);
        }

        protected override void ProcessDefault(XILSInstr i)
        {
            var opTypes = i.OperandTypes;
            var rTypes = i.ResultTypes;
            if (rTypes.Length > 0)
            {
                var defrTypes = i.Command.GetDefaultResultTypes(opTypes);
                if (!defrTypes.Contains(rTypes[0]))
                {
                    var interType = GetNearestType(defrTypes, rTypes[0]);
                    if (interType == null)
                        throw new ArgumentException("Cannot establish type conversion for " + i);
                    Emit(new XILSInstr(i.Command, RemapPreds(i.Preds), opTypes, new TypeDescriptor[] { interType }));
                    Convert(interType, rTypes[0]);
                    return;
                }
            }
            Emit(new XILSInstr(i.Command, RemapPreds(i.Preds), opTypes, rTypes));
        }

        private void ProcessConvert(XILSInstr i)
        {
            Convert(RemapPreds(i.Preds), i.OperandTypes[0], i.ResultTypes[0]);
        }

        private void ProcessSlice(XILSInstr i)
        {
            var opTypes = i.OperandTypes;
            var rTypes = i.ResultTypes;
            Emit(new XILSInstr(i.Command, RemapPreds(i.Preds), opTypes, rTypes));
        }

        private void ProcessLdelem(XILSInstr i)
        {
            var opTypes = i.OperandTypes;
            var rTypes = i.ResultTypes;
            Emit(new XILSInstr(i.Command, RemapPreds(i.Preds), opTypes, rTypes));
        }

        private void ProcessDig(XILSInstr i)
        {
            int digpos = i.StaticOperand == null ? 1 : (int)i.StaticOperand;
            var opTypes = i.OperandTypes;
            var rTypes = new TypeDescriptor[opTypes.Length];
            int k = 0;
            for (int j = 0; j < opTypes.Length; j++)
            {
                if (j != opTypes.Length - 1 - digpos)
                    rTypes[k++] = opTypes[j];
            }
            rTypes[k] = opTypes[opTypes.Length - 1 - digpos];
            Emit(new XILSInstr(i.Command, RemapPreds(i.Preds), opTypes, rTypes));
        }

        private void ProcessSign(XILSInstr i)
        {
            Emit(new XILSInstr(i.Command, RemapPreds(i.Preds), i.OperandTypes, i.ResultTypes));
        }

        private void ProcessLoadConstant(XILSInstr i)
        {
            var rTypes = i.ResultTypes;

            var constant = i.StaticOperand;
            var targetConstant = TypeConversions.ConvertValue(constant, rTypes[0]);

            var inew = new XILSInstr(
                DefaultInstructionSet.Instance.LdConst(targetConstant),
                RemapPreds(i.Preds),
                new TypeDescriptor[0],
                rTypes);
            Emit(inew);
        }

        private void ProcessLoadVariable(XILSInstr i)
        {
            var lit = (IStorableLiteral)i.StaticOperand;
            var local = lit as Variable;
            IStorableLiteral newLit;
            if (local != null)
            {
                newLit = _localDic[local.Name];
            }
            else
            {
                newLit = lit;
            }
            var inew = new XILSInstr(
                DefaultInstructionSet.Instance.LoadVar(newLit),
                RemapPreds(i.Preds),
                new TypeDescriptor[0],
                new TypeDescriptor[] { newLit.Type });
            inew.Index = i.Index;
            Emit(inew);
        }

        private void ProcessStoreVariable(XILSInstr i)
        {
            var lit = (IStorableLiteral)i.StaticOperand;
            var local = lit as Variable;
            IStorableLiteral newLit;
            if (local != null)
            {
                Variable newLocal;
                if (!_localDic.TryGetValue(local.Name, out newLocal))
                {
                    newLocal = new Variable(TypeStack.Peek())
                    {
                        Name = local.Name
                    };
                    _localDic[local.Name] = newLocal;
                }
                newLit = newLocal;
            }
            else
            {
                newLit = lit;
            }
            var preds = RemapPreds(i.Preds);
            if (!TypeStack.Peek().Equals(newLit.Type))
            {
                Convert(preds, TypeStack.Peek(), newLit.Type);
                preds = new InstructionDependency[0];
            }
            var inew = new XILSInstr(
                DefaultInstructionSet.Instance.StoreVar(newLit),
                preds,
                new TypeDescriptor[] { newLit.Type },
                new TypeDescriptor[0]);
            Emit(inew);
        }

        private void ProcessSelect(XILSInstr i)
        {
            var preds = RemapPreds(i.Preds);
            var ot1 = i.OperandTypes[0];
            var ot2 = i.OperandTypes[1];
            var rt = i.ResultTypes[0];
            var ct = GetCommonType(GetCommonType(ot1, ot2), rt);
            if (!ot2.Equals(ct))
            {
                // stack: [ x y cond ]
                Swap(preds);
                // stack: [ x cond y ]
                Convert(preds, ot2, ct);
                Swap();
                // stack: [ x y cond ]
                preds = new InstructionDependency[0];
            }
            if (!ot1.Equals(ct))
            {
                // stack: [ x y cond ]
                Dig(preds, 2);
                // stack: [ y cond x ]
                Convert(ot1, ct);
                Dig(2);
                // stack: [ cond x y ]
                Dig(2);
                // stack: [ x y cond ]
                preds = new InstructionDependency[0];
            }
            var inew = DefaultInstructionSet.Instance.Select().CreateStk(preds, 3, ct, ct, i.OperandTypes[2], ct);
            Emit(inew);
            if (!rt.Equals(ct))
            {
                Convert(ct, rt);
            }
        }

        public static XILSFunction Rewrite(XILSFunction func)
        {
            var rw = new ImplTypeRewriterImpl(func.Instructions);
            rw.Rewrite();
            var result = new XILSFunction(func.Name, func.Arguments, rw.Locals.ToArray(), rw.OutInstructions.ToArray());
            return result;
        }
    }

    public class ImplTypeRewriter: IXILSRewriter
    {
        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            var impl = new ImplTypeRewriterImpl(instrs);
            impl.Rewrite();
            return impl.OutInstructions;
        }

        public override string ToString()
        {
            return "ImplType";
        }
    }
}

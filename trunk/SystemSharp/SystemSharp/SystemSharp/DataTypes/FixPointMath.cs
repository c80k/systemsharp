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
using System.Reflection;
using System.Text;
using System.Threading;
using SystemSharp.Analysis;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.DataTypes
{
    public enum EOverflowMode
    {
        Wrap,
        Fail,
        Saturate
    }

    public enum EArithSizingMode
    {
        VHDLCompliant,
        Safe,
        InSizeIsOutSize
    }

    public class FixedPointSettings
    {
        public static EOverflowMode GlobalOverflowMode = EOverflowMode.Wrap;

        private static int _globalDefaultRadix = 2;
        public static int GlobalDefaultRadix
        {
            get { return _globalDefaultRadix; }
            set
            {
                if (value < 2)
                    throw new ArgumentException();
                _globalDefaultRadix = value;
            }
        }

        public static EArithSizingMode GlobalArithSizingMode = EArithSizingMode.VHDLCompliant;

        private PLSSlot _localOverflowMode;
        private PLSSlot _localDefaultRadix;
        private PLSSlot _localArithSizingMode;

        internal FixedPointSettings(DesignContext context)
        {
            _localOverflowMode = context.AllocPLS();
            _localDefaultRadix = context.AllocPLS();
            _localArithSizingMode = context.AllocPLS();
        }

        public EOverflowMode OverflowMode
        {
            [StaticEvaluationDoNotAnalyze]
            get 
            {
                if (_localOverflowMode.Value == null)
                    return GlobalOverflowMode;
                else
                    return (EOverflowMode)_localOverflowMode.Value; 
            }
            [IgnoreOnDecompilation(true)]
            set 
            { 
                _localOverflowMode.Value = value; 
            }
        }

        public int DefaultRadix
        {
            [StaticEvaluationDoNotAnalyze]
            get 
            {
                if (_localDefaultRadix.Value == null)
                    return GlobalDefaultRadix;
                else
                    return (int)_localDefaultRadix.Value; 
            }
            [IgnoreOnDecompilation(true)]
            set
            {
                if (value < 2)
                    throw new ArgumentException();
                _localDefaultRadix.Value = value;
            }
        }

        public EArithSizingMode ArithSizingMode
        {
            [StaticEvaluationDoNotAnalyze]
            get
            {
                if (_localArithSizingMode.Value == null)
                    return GlobalArithSizingMode;
                else
                    return (EArithSizingMode)_localArithSizingMode.Value;
            }
            [IgnoreOnDecompilation(true)]
            set
            {
                _localArithSizingMode.Value = value;
            }
        }
    }

    class RewriteIncrement : RewriteCall
    {
        public bool IsSigned { get; private set; }
        public bool IsDecrement { get; private set; }

        public RewriteIncrement(bool isSigned, bool isDecrement)
        {
            IsSigned = isSigned;
            IsDecrement = isDecrement;
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            if (args[0].Sample == null)
                return false;

            ISized one, rsample;
            if (IsSigned)
            {
                Signed sample = (Signed)args[0].Sample;
                Signed sone = Signed.FromLong(1, (int)sample.Size);
                one = sone;
                rsample = sample + sone;
            }
            else
            {
                Unsigned sample = (Unsigned)args[0].Sample;
                Unsigned uone = Unsigned.FromULong(1, (int)sample.Size);
                one = uone;
                rsample = sample + uone;
            }
            LiteralReference oneLit = LiteralReference.CreateConstant(one);
            Expression inc;
            if (IsDecrement)
                inc = args[0].Expr - oneLit;
            else
                inc = args[0].Expr + oneLit;
            inc.ResultType = TypeDescriptor.GetTypeOf(rsample);
            inc = IntrinsicFunctions.Resize(inc, (int)one.Size, TypeDescriptor.GetTypeOf(one));
            stack.Push(new StackElement(inc, one, Analysis.Msil.EVariability.ExternVariable));
            return true;
        }
    }

    public class FixFormat
    {
        public bool IsSigned { [StaticEvaluation] get; private set; }
        public int IntWidth { [StaticEvaluation] get; private set; }
        public int FracWidth { [StaticEvaluation] get; private set; }

        public FixFormat(bool isSigned, int intWidth, int fracWidth)
        {
            IsSigned = isSigned;
            IntWidth = intWidth;
            FracWidth = fracWidth;
        }

        public int TotalWidth
        {
            [StaticEvaluation] get { return IntWidth + FracWidth; }
        }

        public override int GetHashCode()
        {
            int shift = FracWidth % 31;
            int hash = (IntWidth << shift) | (IntWidth >> (32 - shift));
            if (IsSigned)
                hash = ~hash;
            return hash;
        }

        public override bool Equals(object obj)
        {
            FixFormat other = (FixFormat)obj;
            if (other == null)
                return false;
            return IsSigned == other.IsSigned &&
                IntWidth == other.IntWidth &&
                FracWidth == other.FracWidth;
        }

        public override string ToString()
        {
            string result = IsSigned ? "SFix" : "UFix";
            result += TotalWidth;
            result += "_";
            result += FracWidth;
            return result;
        }
    }

    static class FixFormatToRangeConverter
    {
        public static Range ConvertToRange(FixFormat fmt)
        {
            return new Range(fmt.IntWidth - 1, -fmt.FracWidth, EDimDirection.Downto);
        }
    }

    public static class FixPointExtensions
    {
        public static FixFormat GetFixFormat(this TypeDescriptor td)
        {
            if (td.CILType.Equals(typeof(Signed)) ||
                td.CILType.Equals(typeof(SFix)))
                return SFix.GetFormat(td);
            else if (td.CILType.Equals(typeof(Unsigned)) ||
                td.CILType.Equals(typeof(UFix)))
                return UFix.GetFormat(td);
            else
                return null;
        }

        public static TypeDescriptor ToType(this FixFormat fmt)
        {
            if (fmt.IsSigned)
                return TypeDescriptor.GetTypeOf(SFix.FromDouble(0.0, fmt.IntWidth, fmt.FracWidth));
            else
                return TypeDescriptor.GetTypeOf(UFix.FromDouble(0.0, fmt.IntWidth, fmt.FracWidth));
        }
    }
}

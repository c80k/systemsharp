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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
#if USE_INTX
using IntXLib;
#else
using System.Numerics;
#endif
using SystemSharp.Analysis;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.DataTypes
{
    class UFixSerializer : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            UFix ufix = (UFix)value;
            return ufix.UnsignedValue.SLVValue;
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            var fmt = UFix.GetFormat(targetType);
            return UFix.FromUnsigned(slv.UnsignedValue, fmt.FracWidth);
        }
    }

    class UFixAlgebraicType : AlgebraicTypeAttribute
    {
        public override object CreateInstance(ETypeCreationOptions options, object template)
        {
            if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral))
            {
                if (template == null)
                {
                    return UFix.One;
                }
                else
                {
                    var ufix = (UFix)template;
                    return UFix.FromDouble(1.0, ufix.Format.IntWidth, ufix.Format.FracWidth);
                }
            }
            else if (options.HasFlag(ETypeCreationOptions.NonZero))
            {
                if (template == null)
                {
                    return UFix.One;
                }
                else
                {
                    var ufix = (UFix)template;
                    return UFix.FromUnsigned(Unsigned.FromUInt(1, ufix.Format.TotalWidth), ufix.Format.FracWidth);
                }
            }
            else
            {
                if (template == null)
                {
                    return UFix.Zero;
                }
                else
                {
                    var ufix = (UFix)template;
                    return UFix.FromDouble(0.0, ufix.Format.IntWidth, ufix.Format.FracWidth);
                }
            }
        }
    }

    class UFixDivisionGuard : GuardedArgumentAttribute
    {
        public override object CorrectArgument(object arg)
        {
            var org = (UFix)arg;
            return UFix.FromDouble(1.0, org.Format.IntWidth, org.Format.FracWidth);
        }
    }

    [MapToIntrinsicType(EIntrinsicTypes.UFix)]
    [SLVSerializable(typeof(UFix), typeof(UFixSerializer))]
    [UFixAlgebraicType]
    public struct UFix
    {
        public static readonly UFix Zero = new UFix(Unsigned.Zero, 1, 0);
        public static readonly UFix One = new UFix(Unsigned.One, 1, 0);

        private Unsigned _value;
        private FixFormat _format;

        public static FixFormat GetFormat(TypeDescriptor td)
        {
            if (!td.CILType.Equals(typeof(UFix)) &&
                !td.CILType.Equals(typeof(Unsigned)))
                throw new InvalidOperationException();
            if (!td.IsComplete)
                throw new InvalidOperationException();
            Range range = td.Constraints[0];
            return new FixFormat(false, range.FirstBound + 1, -range.SecondBound);
        }

        [TypeParameter(typeof(FixFormatToRangeConverter))]        
        public FixFormat Format
        {
            [StaticEvaluation]
            get
            {
                if (_format == null)
                    _format = new FixFormat(false, 0, 0);
                return _format;
            }
            private set { _format = value; }
        }

        private UFix(Unsigned value, int intWidth, int fracWidth)
        {
            Contract.Requires(intWidth + fracWidth >= 0);
            Debug.Assert(value.Size <= intWidth + fracWidth);
            _value = value;
            _format = new FixFormat(false, intWidth, fracWidth);
        }

        private static FixFormat Equalize(UFix a, UFix b, out Unsigned an, out Unsigned bn)
        {
            an = a._value;
            bn = b._value;
            FixFormat dfmt;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    dfmt = new FixFormat(false,
                        Math.Max(a.Format.IntWidth, b.Format.IntWidth) + 1,
                        Math.Max(a.Format.FracWidth, b.Format.FracWidth));
                    an = an.Resize(dfmt.TotalWidth - 1);
                    bn = bn.Resize(dfmt.TotalWidth - 1);
                    if (a.Format.FracWidth > b.Format.FracWidth)
                        bn = bn << (int)(a.Format.FracWidth - b.Format.FracWidth);
                    else if (b.Format.FracWidth > a.Format.FracWidth)
                        an = an << (int)(b.Format.FracWidth - a.Format.FracWidth);
                    return dfmt;

                case EArithSizingMode.VHDLCompliant:
                    dfmt = new FixFormat(false,
                        Math.Max(a.Format.IntWidth, b.Format.IntWidth) + 1,
                        Math.Max(a.Format.FracWidth, b.Format.FracWidth));
                    an = an.Resize(dfmt.TotalWidth);
                    bn = bn.Resize(dfmt.TotalWidth);
                    if (a.Format.FracWidth > b.Format.FracWidth)
                        bn = bn << (int)(a.Format.FracWidth - b.Format.FracWidth);
                    else if (b.Format.FracWidth > a.Format.FracWidth)
                        an = an << (int)(b.Format.FracWidth - a.Format.FracWidth);
                    return dfmt;

                case EArithSizingMode.InSizeIsOutSize:
                    dfmt = a.Format;
                    bn = bn.Resize(dfmt.TotalWidth);
                    if (a.Format.FracWidth > b.Format.FracWidth)
                        bn = bn << (int)(a.Format.FracWidth - b.Format.FracWidth);
                    else if (b.Format.FracWidth > a.Format.FracWidth)
                        an = an << (int)(b.Format.FracWidth - a.Format.FracWidth);
                    return dfmt;

                default:
                    throw new NotImplementedException();
            }
        }

        public static UFix operator +(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            Unsigned rn = an + bn;
            return new UFix(rn, dfmt.IntWidth, dfmt.FracWidth);
        }

        public static UFix operator -(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            Unsigned rn = an - bn;
            return new UFix(rn, dfmt.IntWidth, dfmt.FracWidth);
        }

        public static SFix operator -(UFix a)
        {
            return (-a.SFixValue).Resize(a.Format.IntWidth + 1, a.Format.FracWidth);
        }

        public static UFix operator *(UFix a, UFix b)
        {
            FixFormat dfmt = new FixFormat(true,
                a.Format.IntWidth + b.Format.IntWidth,
                a.Format.FracWidth + b.Format.FracWidth);
            return new UFix(a._value * b._value, dfmt.IntWidth, dfmt.FracWidth);
        }

        public static UFix operator /(UFix a, [UFixDivisionGuard] UFix b)
        {
            Unsigned ar = 
                a._value.Resize(a.Format.TotalWidth + b.Format.TotalWidth) << 
                    b.Format.TotalWidth;
            return new UFix(ar / b._value, 
                a.Format.IntWidth + b.Format.FracWidth, 
                a.Format.FracWidth + b.Format.IntWidth);
        }

        public static UFix operator %(UFix a, [SFixDivisionGuard] UFix b)
        {
            int fracWidth = Math.Max(a.Format.FracWidth, b.Format.FracWidth);
            int padWidthA = fracWidth - a.Format.FracWidth;
            int padWidthB = fracWidth - b.Format.FracWidth;
            Unsigned ar = a.UnsignedValue.Resize(a.Format.TotalWidth + padWidthA) << padWidthA;
            Unsigned br = b.UnsignedValue.Resize(b.Format.TotalWidth + padWidthB) << padWidthB;
            var tmp = new UFix(
                ar % br,
                b.Format.IntWidth,
                fracWidth);
            return tmp;
        }

        public static bool operator <(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an < bn;
        }

        public static bool operator <=(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an <= bn;
        }

        public static bool operator ==(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an == bn;
        }

        public static bool operator !=(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an != bn;
        }

        public static bool operator >=(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an >= bn;
        }

        public static bool operator >(UFix a, UFix b)
        {
            Unsigned an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an > bn;
        }


        [TypeConversion(typeof(ulong), typeof(UFix))]
        public static UFix FromULong(ulong value, int intWidth)
        {
            return new UFix(Unsigned.FromULong(value, intWidth), intWidth, 0);
        }

        [TypeConversion(typeof(double), typeof(UFix))]
        public static UFix FromDouble(double value, int intWidth, int fracWidth)
        {
            double svalue = value * Math.Pow(2.0, fracWidth);
            ulong lvalue = (ulong)svalue;
            return new UFix(Unsigned.FromULong(lvalue, intWidth + fracWidth), intWidth, fracWidth);
        }

        [TypeConversion(typeof(Unsigned), typeof(UFix), true)]
        public static UFix FromUnsigned(Unsigned value, int fracWidth)
        {
            if (value.Size > int.MaxValue)
                throw new InvalidOperationException();

            return new UFix(value, (int)value.Size - fracWidth, fracWidth);
        }

        [TypeConversion(typeof(StdLogicVector), typeof(UFix))]
        public static UFix FromSLV(StdLogicVector slv, int fracWidth)
        {
            return FromUnsigned(slv.UnsignedValue, fracWidth);
        }

        [AssumeNotCalled]
        public static TypeDescriptor MakeType(int intWidth, int fracWidth)
        {
            var smp = FromDouble(0.0, intWidth, fracWidth);
            return TypeDescriptor.GetTypeOf(smp);
        }

        public Unsigned UnsignedValue
        {
            [TypeConversion(typeof(UFix), typeof(Unsigned))]
            get { return _value; }
        }

        public StdLogicVector SLVValue
        {
            [TypeConversion(typeof(UFix), typeof(StdLogicVector))]
            get { return UnsignedValue.SLVValue; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        public class UFixSFixConversion : RewriteCall, IDoNotAnalyze, ISideEffectFree
        {
            public UFixSFixConversion()
            {
            }

            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var ufixSample = (UFix)args[0].Sample;
                var ufixType = args[0].Expr.ResultType;
                var slvuType = TypeDescriptor.GetTypeOf(ufixSample.SLVValue);

                object[] outArgs;
                object rsample;
                stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out rsample);
                if (rsample == null)
                    throw new ArgumentException("Unable to infer result sample");

                var sfixSample = (SFix)rsample;
                var sfixType = TypeDescriptor.GetTypeOf(sfixSample);
                var slvsType = TypeDescriptor.GetTypeOf(sfixSample.SLVValue);
                var zlit = LiteralReference.CreateConstant(StdLogic._0);

                var eargs = args.Select(arg => arg.Expr).ToArray();
                var cast1 = IntrinsicFunctions.Cast(eargs, ufixType.CILType, slvuType, true);
                var cast2 = Expression.Concat(zlit, cast1);
                var cast3 = IntrinsicFunctions.Cast(new Expression[] { cast2 }, slvsType.CILType, sfixType, true);

                stack.Push(cast3, rsample);
                return true;
            }
        }

        public SFix SFixValue
        {
            [UFixSFixConversion]
#if USE_INTX
            get { return SFix.FromSigned(Signed.FromIntX(UnsignedValue.IntXValue, _format.TotalWidth + 1), _format.FracWidth); }
#else
            get { return SFix.FromSigned(Signed.FromBigInt(UnsignedValue.BigIntValue, _format.TotalWidth + 1), _format.FracWidth); }
#endif
        }

        public double DoubleValue
        {
            [TypeConversion(typeof(UFix), typeof(double))]
            get { return SFixValue.DoubleValue; }
        }

        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Resize)]
        [SideEffectFree]
        public UFix Resize(int newIntWidth, int newFracWidth)
        {
            var rounded = UnsignedValue.Resize(Math.Max(newIntWidth + newFracWidth, Format.TotalWidth));
            if (newFracWidth < Format.FracWidth)
                rounded >>= (Format.FracWidth - newFracWidth);
            else
                rounded <<= (newFracWidth - Format.FracWidth);
            var resized = rounded.Resize(newIntWidth + newFracWidth);
            return UFix.FromUnsigned(resized, newFracWidth);
        }

#if USE_INTX
        [TypeConversion(typeof(UFix), typeof(string))]
        public string ToString(int radix, IFormatProvider format, int precision = 10)
        {
            if (radix < 2)
                throw new ArgumentException();

            if (_format == null)
                return "?";

            IntX mul = radix;
            for (int i = 1; i < precision; i++)
                mul *= radix;
            IntX div = (IntX)1;
            if (Format.FracWidth > 0)
                div <<= Format.FracWidth;
            else
                mul <<= -Format.FracWidth;
            IntX normalized = _value.IntXValue * mul / div;
            string valstr = normalized.ToString((uint)radix);
            valstr = valstr.PadLeft(precision + 1, '0');
            string postComma = valstr.Substring(valstr.Length - precision);
            postComma = postComma.TrimEnd('0');
            string preComma = valstr.Substring(0, valstr.Length - precision);
            var nfi = (NumberFormatInfo)format.GetFormat(typeof(NumberFormatInfo));
            if (postComma.Length == 0)
                return preComma;
            else
                return preComma + nfi.NumberDecimalSeparator + postComma;
        }
#else
        [TypeConversion(typeof(UFix), typeof(string))]
        public string ToString(int radix, IFormatProvider format, int precision = 10)
        {
            if (radix < 2)
                throw new ArgumentException();

            if (_format == null)
                return "?";

            BigInteger mul = radix;
            for (int i = 1; i < precision; i++)
                mul *= radix;
            int rs = 0;
            if (Format.FracWidth > 0)
                rs = Format.FracWidth;
            else
                mul <<= -Format.FracWidth;
            var normalized = (_value.BigIntValue * mul) >> rs;
            string valstr;
            switch (radix)
            {
                case 10:
                    valstr = normalized.ToString("R");
                    break;

                case 16:
                    valstr = normalized.ToString("X");
                    break;

                case 2:
                    valstr = SLVValue.ToString();
                    break;

                default:
                    throw new NotSupportedException("Currently only radix 2, 10 or 16 supported");

            }
            valstr = valstr.PadLeft(precision + 1, '0');
            string postComma = valstr.Substring(valstr.Length - precision);
            postComma = postComma.TrimEnd('0');
            string preComma = valstr.Substring(0, valstr.Length - precision);
            var nfi = (NumberFormatInfo)format.GetFormat(typeof(NumberFormatInfo));
            if (postComma.Length == 0)
                return preComma;
            else
                return preComma + nfi.NumberDecimalSeparator + postComma;
        }
#endif

        [TypeConversion(typeof(UFix), typeof(string))]
        public string ToString(int radix, int precision = 10)
        {
            return ToString(radix, CultureInfo.CurrentCulture, precision);
        }

        [TypeConversion(typeof(UFix), typeof(string))]
        public string ToString(IFormatProvider format)
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix, CultureInfo.CurrentCulture);
        }

        [TypeConversion(typeof(UFix), typeof(string))]
        public override string ToString()
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix, CultureInfo.CurrentCulture);
        }

        public override bool Equals(object obj)
        {
            if (obj is UFix)
            {
                UFix other = (UFix)obj;
                Unsigned an, bn;
                UFix.Equalize(this, other, out an, out bn);
                return an.Equals(bn);
            }
            else if (obj is SFix)
            {
                SFix other = (SFix)obj;
                SFix me = Resize((int)Format.IntWidth + 1, (int)Format.FracWidth).SFixValue;
                return me.Equals(other);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int rot = (int)Format.IntWidth % 32;
            int hash = UnsignedValue.GetHashCode();
            return (hash << rot) | (hash >> (32 - rot));
        }
    }

    public static class UFixExtensions
    {
        public static Unsigned GetIntPart(this UFix ufix)
        {
            return ufix.SLVValue[ufix.Format.TotalWidth - 1, ufix.Format.FracWidth].UnsignedValue;
        }

        public static Unsigned GetFracPart(this UFix ufix)
        {
            return ufix.SLVValue[ufix.Format.FracWidth - 1, 0].UnsignedValue;
        }
    }
}

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
    class SFixSerializer : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            SFix sfix = (SFix)value;
            return sfix.SignedValue.SLVValue;
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            var fmt = SFix.GetFormat(targetType);
            return SFix.FromSigned(slv.SignedValue, fmt.FracWidth);
        }
    }

    class SFixAlgebraicType : AlgebraicTypeAttribute
    {
        public override object CreateInstance(ETypeCreationOptions options, object template)
        {
            if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral))
            {
                if (template == null)
                {
                    return SFix.One;
                }
                else
                {
                    var sfix = (SFix)template;
                    return SFix.FromDouble(1.0, sfix.Format.IntWidth, sfix.Format.FracWidth);
                }
            }
            else if (options.HasFlag(ETypeCreationOptions.NonZero))
            {
                if (template == null)
                {
                    return SFix.One;
                }
                else
                {
                    var sfix = (SFix)template;
                    return SFix.FromSigned(Signed.FromInt(1, sfix.Format.TotalWidth), sfix.Format.FracWidth);
                }
            }
            else
            {
                if (template == null)
                {
                    return SFix.Zero;
                }
                else
                {
                    var sfix = (SFix)template;
                    return SFix.FromDouble(0.0, sfix.Format.IntWidth, sfix.Format.FracWidth);
                }
            }
        }
    }

    class SFixDivisionGuard : GuardedArgumentAttribute
    {
        public override object CorrectArgument(object arg)
        {
            var org = (SFix)arg;
            return SFix.FromDouble(1.0, org.Format.IntWidth, org.Format.FracWidth);
        }
    }

    [MapToIntrinsicType(EIntrinsicTypes.SFix)]
    [SLVSerializable(typeof(SFix), typeof(SFixSerializer))]
    [SFixAlgebraicType]
    public struct SFix
    {
        public static readonly SFix Zero = new SFix(Signed.Zero, 1, 0);
        public static readonly SFix One = new SFix(Signed.One, 2, 0);

        private Signed _value;
        private FixFormat _format;

        [TypeParameter(typeof(FixFormatToRangeConverter))]        
        public FixFormat Format
        {
            [StaticEvaluation]
            get
            {
                if (_format == null)
                    _format = new FixFormat(true, 0, 0);
                return _format;
            }
            private set { _format = value; }
        }

        public static FixFormat GetFormat(TypeDescriptor td)
        {
            if (!td.CILType.Equals(typeof(SFix)) &&
                !td.CILType.Equals(typeof(Signed)))
                throw new InvalidOperationException();
            if (!td.IsComplete)
                throw new InvalidOperationException();
            Range range = td.Constraints[0];
            return new FixFormat(true, range.FirstBound + 1, -range.SecondBound);
        }

        private SFix(Signed value, int intWidth, int fracWidth)
        {
            Contract.Requires(intWidth + fracWidth >= 0);
            Debug.Assert(value.Size <= intWidth + fracWidth);
            _value = value;
            _format = new FixFormat(true, intWidth, fracWidth);
        }

        private static FixFormat Equalize(SFix a, SFix b, out Signed an, out Signed bn)
        {
            an = a._value;
            bn = b._value;
            FixFormat dfmt;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    dfmt = new FixFormat(true,
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
                    dfmt = new FixFormat(true,
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

        public static SFix operator +(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            Signed rn = an + bn;
            return new SFix(rn, dfmt.IntWidth, dfmt.FracWidth);
        }

        public static SFix operator -(SFix a)
        {
            Signed an = a.SignedValue;
            Signed rn = -an;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.InSizeIsOutSize:
                    return new SFix(rn, a._format.IntWidth, a._format.FracWidth);

                case EArithSizingMode.Safe:
                case EArithSizingMode.VHDLCompliant:
                    return new SFix(rn, a._format.IntWidth + 1, a._format.FracWidth);

                default:
                    throw new NotImplementedException();
            }
        }

        public static SFix operator -(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            Signed rn = an - bn;
            return new SFix(rn, dfmt.IntWidth, dfmt.FracWidth);
        }

        public static SFix operator *(SFix a, SFix b)
        {
            var tmp = new SFix(a._value * b._value,
                a.Format.IntWidth + b.Format.IntWidth,
                a.Format.FracWidth + b.Format.FracWidth);
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                case EArithSizingMode.VHDLCompliant:
                    return tmp;

                case EArithSizingMode.InSizeIsOutSize:
                    return tmp.Resize(a.Format.IntWidth, a.Format.FracWidth);

                default:
                    throw new NotImplementedException();
            }
        }

        public static SFix operator /(SFix a, [SFixDivisionGuard] SFix b)
        {
            Signed ar = 
                a.SignedValue.Resize(a.Format.TotalWidth + b.Format.TotalWidth) << b.Format.TotalWidth;
            var tmp = new SFix(ar / b._value, 
                a.Format.IntWidth + b.Format.FracWidth + 1, 
                a.Format.FracWidth + b.Format.IntWidth);
            if (DesignContext.Instance.FixPoint.ArithSizingMode == EArithSizingMode.InSizeIsOutSize)
                tmp = tmp.Resize(a.Format.IntWidth, b.Format.FracWidth);
            return tmp;
        }

        public static SFix operator %(SFix a, [SFixDivisionGuard] SFix b)
        {
            int fracWidth = Math.Max(a.Format.FracWidth, b.Format.FracWidth);
            int padWidthA = fracWidth - a.Format.FracWidth;
            int padWidthB = fracWidth - b.Format.FracWidth;
            Signed ar = a.SignedValue.Resize(a.Format.TotalWidth + padWidthA) << padWidthA;
            Signed br = b.SignedValue.Resize(b.Format.TotalWidth + padWidthB) << padWidthB;
            var tmp = new SFix(
                ar % br,
                b.Format.IntWidth,
                fracWidth);
            return tmp;
        }

        public static bool operator <(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an < bn;
        }

        public static bool operator <=(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an <= bn;
        }

        public static bool operator ==(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an == bn;
        }

        public static bool operator !=(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an != bn;
        }

        public static bool operator >=(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an >= bn;
        }

        public static bool operator >(SFix a, SFix b)
        {
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an > bn;
        }

        public static bool operator <(SFix a, UFix ub)
        {
            var b = ub.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an < bn;
        }

        public static bool operator <=(SFix a, UFix ub)
        {
            var b = ub.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an <= bn;
        }

        public static bool operator ==(SFix a, UFix ub)
        {
            var b = ub.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an == bn;
        }

        public static bool operator !=(SFix a, UFix ub)
        {
            var b = ub.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an != bn;
        }

        public static bool operator >=(SFix a, UFix ub)
        {
            var b = ub.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an >= bn;
        }

        public static bool operator >(SFix a, UFix ub)
        {
            var b = ub.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an > bn;
        }

        public static bool operator <(UFix ua, SFix b)
        {
            var a = ua.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an < bn;
        }

        public static bool operator <=(UFix ua, SFix b)
        {
            var a = ua.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an <= bn;
        }

        public static bool operator ==(UFix ua, SFix b)
        {
            var a = ua.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an == bn;
        }

        public static bool operator !=(UFix ua, SFix b)
        {
            var a = ua.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an != bn;
        }

        public static bool operator >=(UFix ua, SFix b)
        {
            var a = ua.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an >= bn;
        }

        public static bool operator >(UFix ua, SFix b)
        {
            var a = ua.SFixValue;
            Signed an, bn;
            FixFormat dfmt = Equalize(a, b, out an, out bn);
            return an > bn;
        }

        [TypeConversion(typeof(long), typeof(SFix))]
        public static SFix FromLong(long value, int intWidth)
        {
            return new SFix(Signed.FromLong(value, intWidth), intWidth, 0);
        }

        [TypeConversion(typeof(long), typeof(SFix))]
        public static SFix FromLong(long value, int intWidth, int fracWidth)
        {
            return new SFix(Signed.FromLong(value, intWidth + fracWidth) << fracWidth,
                intWidth, fracWidth);
        }

        [TypeConversion(typeof(double), typeof(SFix))]
        public static SFix FromDouble(double value, int intWidth, int fracWidth)
        {
            double svalue = value * Math.Pow(2.0, fracWidth);            
            long lvalue = (long)svalue;            
            return new SFix(Signed.FromLong(lvalue, intWidth + fracWidth), intWidth, fracWidth);
        }

        [TypeConversion(typeof(Signed), typeof(SFix), true)]
        public static SFix FromSigned(Signed value, int fracWidth)
        {
            if (value.Size > int.MaxValue)
                throw new InvalidOperationException();

            return new SFix(value, (int)value.Size - fracWidth, fracWidth);
        }

        [TypeConversion(typeof(StdLogicVector), typeof(SFix))]
        public static SFix FromSLV(StdLogicVector slv, int fracWidth)
        {
            return FromSigned(slv.SignedValue, fracWidth);
        }

        [AssumeNotCalled]
        public static TypeDescriptor MakeType(int intWidth, int fracWidth)
        {
            var smp = FromDouble(0.0, intWidth, fracWidth);
            return TypeDescriptor.GetTypeOf(smp);
        }

        public Signed SignedValue
        {
            [TypeConversion(typeof(SFix), typeof(Signed))]
            get { return _value; }
        }

        public StdLogicVector SLVValue
        {
            [TypeConversion(typeof(SFix), typeof(StdLogicVector))]
            get { return SignedValue.SLVValue; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        public class SFixUFixConversion : RewriteCall, IDoNotAnalyze, ISideEffectFree
        {
            public SFixUFixConversion()
            {
            }

            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var sfixSample = (SFix)args[0].Sample;
                var sfixType = args[0].Expr.ResultType;
                var slvsType = TypeDescriptor.GetTypeOf(sfixSample.SLVValue);

                object[] outArgs;
                object rsample;
                stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out rsample);
                if (rsample == null)
                    throw new ArgumentException("Unable to infer result sample");

                var ufixSample = (UFix)rsample;
                var ufixType = TypeDescriptor.GetTypeOf(ufixSample);
                var slvuType = TypeDescriptor.GetTypeOf(ufixSample.SLVValue);

                var eargs = args.Select(arg => arg.Expr).ToArray();
                var cast1 = IntrinsicFunctions.Cast(eargs, sfixType.CILType, slvsType, false);
                var cast2 = Expression.Slice(cast1,
                    LiteralReference.CreateConstant(ufixSample.Format.TotalWidth - 1),
                    LiteralReference.CreateConstant(0));
                var cast3 = IntrinsicFunctions.Cast(new Expression[] { cast2 }, slvuType.CILType, ufixType, false);

                stack.Push(cast3, rsample);
                return true;
            }
        }

#if USE_INTX
        public UFix UFixValue
        {
            [SFixUFixConversion]
            get { return UFix.FromUnsigned(Unsigned.FromIntX(SignedValue.IntXValue, _format.TotalWidth - 1), _format.FracWidth); }
        }
#else
        public UFix UFixValue
        {
            [SFixUFixConversion]
            get { return UFix.FromUnsigned(Unsigned.FromBigInt(SignedValue.BigIntValue, _format.TotalWidth - 1), _format.FracWidth); }
        }
#endif

        public double DoubleValue
        {
            [TypeConversion(typeof(SFix), typeof(double))]
            get
            {
                double svalue = (double)SignedValue.LongValue;
                double result = svalue * Math.Pow(2.0, -Format.FracWidth);
                return result;
            }
        }

#if USE_INTX
        public void Split(out bool negative, out Unsigned preComma, out Unsigned postComma)
        {
            IntX scale = new IntX(1) << (int)Format.FracWidth;
            IntX modRes;
            IntX abs = SignedValue.IntXValue;
            if (abs < 0)
            {
                negative = true;
                abs = -abs;
            }
            else
            {
                negative = false;
            }
            IntX div = IntX.DivideModulo(abs, scale, out modRes);
            preComma = Unsigned.FromIntX(div, _format.IntWidth);
            postComma = Unsigned.FromIntX(modRes, _format.FracWidth);
        }
#else
        // nyi
#endif

        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Resize)]
        [SideEffectFree]
        public SFix Resize(int newIntWidth, int newFracWidth)
        {
            var rounded = SignedValue.Resize(Math.Max(newIntWidth + newFracWidth, Format.TotalWidth));
            if (newFracWidth < Format.FracWidth)
                rounded >>= (Format.FracWidth - newFracWidth);
            else
                rounded <<= (newFracWidth - Format.FracWidth);
            var resized = rounded.Resize(newIntWidth + newFracWidth);
            return SFix.FromSigned(resized, newFracWidth);
        }

        public const int DynamicWidth = 0;
        public const int WidthOfDataType = -1;

#if USE_INTX
        [TypeConversion(typeof(SFix), typeof(string))]
        public string ToString(int radix, IFormatProvider format, int width = DynamicWidth, int precision = 10)
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
            IntX myvalue = _value.IntXValue;
            bool negative;
            if (myvalue < 0)
            {
                negative = true;
                myvalue = -myvalue;
            }
            else
            {
                negative = false;
            }
            IntX normalized = myvalue * mul / div;
            string valstr = normalized.ToString((uint)radix);
            valstr = valstr.PadLeft(precision + 1, '0');
            string postComma = valstr.Substring(valstr.Length - precision);
            postComma = postComma.TrimEnd('0');
            string preComma = valstr.Substring(0, valstr.Length - precision);
            var nfi = (NumberFormatInfo)format.GetFormat(typeof(NumberFormatInfo));
            if (negative)
                preComma = nfi.NegativeSign + preComma;
            if (postComma.Length == 0)
                return preComma;
            else
                return preComma + nfi.NumberDecimalSeparator + postComma;
        }
#else
        [TypeConversion(typeof(SFix), typeof(string))]
        public string ToString(int radix, IFormatProvider format, int width = DynamicWidth, int precision = 10)
        {
            if (radix < 2)
                throw new ArgumentException("Radix less than 2");

            if (_format == null)
                return "?";

            var mul = (BigInteger)radix;
            for (int i = 1; i < precision; i++)
                mul *= radix;
            int sr = 0;
            if (Format.FracWidth > 0)
                sr = Format.FracWidth;
            else
                mul <<= -Format.FracWidth;
            var myvalue = _value.BigIntValue;
            bool negative;
            if (myvalue < 0)
            {
                negative = true;
                myvalue = -myvalue;
            }
            else
            {
                negative = false;
            }
            var normalized = (myvalue * mul) >> sr;
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
            if (negative)
                preComma = nfi.NegativeSign + preComma;
            if (postComma.Length == 0)
                return preComma;
            else
                return preComma + nfi.NumberDecimalSeparator + postComma;
        }
#endif

        [TypeConversion(typeof(SFix), typeof(string))]
        public string ToString(int radix, int width = DynamicWidth, int precision = 10)
        {
            return ToString(radix, CultureInfo.CurrentCulture, width, precision);
        }

        [TypeConversion(typeof(SFix), typeof(string))]
        public override string ToString()
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix, CultureInfo.CurrentCulture);
        }

        [TypeConversion(typeof(SFix), typeof(string))]
        public string ToString(IFormatProvider format)
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix, format);
        }

        public override bool Equals(object obj)
        {
            if (obj is SFix)
            {
                SFix other = (SFix)obj;
                Signed an, bn;
                SFix.Equalize(this, other, out an, out bn);
                return an.Equals(bn);
            }
            else if (obj is UFix)
            {
                UFix otheru = (UFix)obj;
                SFix other = otheru.Resize((int)Format.IntWidth, (int)Format.FracWidth).SFixValue;
                Signed an, bn;
                SFix.Equalize(this, other, out an, out bn);
                return an.Equals(bn);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int rot = (int)Format.IntWidth % 32;
            int hash = SignedValue.GetHashCode();
            return (hash << rot) | (hash >> (32 - rot));
        }
    }

    public static class SFixExtensions
    {
        public static Signed GetIntPart(SFix sfix)
        {
            return sfix.SLVValue[sfix.Format.TotalWidth - 1, sfix.Format.FracWidth].SignedValue;
        }

        public static Unsigned GetFracPart(this SFix sfix)
        {
            return sfix.SLVValue[sfix.Format.FracWidth - 1, 0].UnsignedValue;
        }
    }
}

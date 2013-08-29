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
using System.Globalization;

namespace SystemSharp.DataTypes
{
    class SignedToSLV : ISerializer
    {
        #region ISerializer Member

        public StdLogicVector Serialize(object value)
        {
            Signed sval = (Signed)value;
            return sval.SLVValue;
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return slv.SignedValue;
        }

        #endregion
    }

    class SignedAlgebraicType : AlgebraicTypeAttribute
    {
        public override object CreateInstance(ETypeCreationOptions options, object template)
        {
            if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                options.HasFlag(ETypeCreationOptions.NonZero))
            {
                if (template == null)
                {
                    return Signed.One;
                }
                else
                {
                    var signed = (Signed)template;
                    return Signed.FromInt(1, signed.Size);
                }
            }
            else
            {
                if (template == null)
                {
                    return Signed.Zero;
                }
                else
                {
                    var signed = (Signed)template;
                    return Signed.FromInt(0, signed.Size);
                }
            }
        }
    }

    class SignedDivisionGuard : GuardedArgumentAttribute
    {
        public override object CorrectArgument(object arg)
        {
            return Signed.One;
        }
    }

#if USE_INTX
    /// <summary>
    /// This struct represents a signed integer of an arbitrary size.
    /// </summary>
    [MapToIntrinsicType(EIntrinsicTypes.Signed)]
    [SLVSerializable(typeof(Signed), typeof(SignedToSLV))]
    [SignedAlgebraicType]
    public struct Signed : ISized
    {
        public static readonly Signed Zero = new Signed(0, 1);
        public static readonly Signed One = new Signed(1, 2);

        private IntX _value;
        private int _size;

        /// <summary>
        /// Constructs the Signed struct from a value and a desired size.
        /// </summary>
        /// <param name="value">The integer value</param>
        /// <param name="size">The integer size (in bits)</param>
        private Signed(IntX value, int size)
        {
            Contract.Requires(size >= 0);
            Contract.Requires(
                value == null ||
                value.CompareTo(0) == 0 ||
                (value <= (((IntX)1) << (size - 1)) - 1 &&
                value >= -(((IntX)1) << (size - 1))));

            _value = value;
            _size = size;
        }

        private static long Trim(long value, int size)
        {
            if (size >= 64)
                return value;
            if (size == 0)
            {
                if (value != 0 &&
                    DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Fail)
                    throw new ArgumentException();
                return 0;
            }
            if (DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Wrap)
            {
                value <<= (int)(64 - size);
                value >>= (int)(64 - size);
                return value;
            }
            else
            {
                long max = (1L << (int)(size - 1)) - 1;
                long min = -(1L << (int)(size - 1));
                if (DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Fail)
                {
                    if (value > max || value < min)
                        throw new ArgumentException();
                }
                else if (DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Saturate)
                {
                    if (value > max)
                        value = max;
                    else if (value < min)
                        value = min;
                }
                else
                    throw new NotImplementedException();

                return value;
            }
        }

        private static IntX Trim(IntX value, int size)
        {
            if (value == null)
                return null;

            if (size == 0)
            {
                if (value != 0 &&
                    DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Fail)
                    throw new ArgumentException();
                return 0;
            }

            IntX rem = new IntX(1) << (int)size;
            IntX rem2 = rem >> 1;
            switch (DesignContext.Instance.FixPoint.OverflowMode)
            {
                case EOverflowMode.Wrap:
                    value = IntX.Modulo(value, rem, DivideMode.AutoNewton);
                    if (value >= rem2)
                        value -= rem;
                    else if (value < -rem2)
                        value += rem;
                    return value;

                case EOverflowMode.Fail:
                    if (value > rem2 - 1 || value < -rem2)
                        throw new ArgumentException();
                    return value;

                case EOverflowMode.Saturate:
                    if (value > rem2 - 1)
                        return rem2 - 1;
                    else if (value < -rem2)
                        return -rem2;
                    else
                        return value;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts a long value to a Signed value
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target size of the Signed value</param>
        /// <returns>The converted Signed value</returns>
        [TypeConversion(typeof(long), typeof(Signed))]
        [SideEffectFree]
        public static Signed FromLong(long value, int size)
        {
            return new Signed(new IntX(Trim(value, size)), size);
        }

        /// <summary>
        /// Converts an int value to a Signed value
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target size of the Signed value</param>
        /// <returns>The converted Signed value</returns>
        [TypeConversion(typeof(int), typeof(Signed))]
        [SideEffectFree]
        public static Signed FromInt(int value, int size)
        {
            return new Signed(new IntX(Trim(value, size)), size);
        }

        public static Signed FromIntX(IntX value, int size)
        {
            return new Signed(Trim(value, size), size);
        }

        /// <summary>
        /// Converts this Signed value to an StdLogicVector representation.
        /// </summary>        
        public StdLogicVector SLVValue
        {
            [TypeConversion(typeof(Signed), typeof(StdLogicVector))]
            [SideEffectFree]
            get
            {
                if (_value == null)
                    return StdLogicVector.Empty;

                uint[] digits;
                bool neg;
                _value.GetInternalState(out digits, out neg);
                StdLogic pad;
                if (neg)
                {
                    uint[] complDigits = (uint[])digits.Clone();
                    for (int i = 0; i < digits.Length; i++)
                        complDigits[i] = ~digits[i];
                    IntX compl = new IntX(complDigits, false) + 1;
                    compl.GetInternalState(out digits, out neg);
                    pad = StdLogic._1;
                }
                else
                {
                    pad = StdLogic._0;
                }
                StdLogic[] bits = new StdLogic[_size];
                int k = 0;
                for (int i = 0; i < digits.Length; i++)
                {
                    uint curw = digits[i];
                    for (int j = 0; j < 32 && k < _size; j++, k++)
                    {
                        if ((curw & 1) == 1)
                            bits[k] = StdLogic._1;
                        else
                            bits[k] = StdLogic._0;
                        curw >>= 1;
                    }
                }
                while (k < _size)
                {
                    bits[k++] = pad;
                }
                return StdLogicVector.FromStdLogic(bits);
            }
        }

        public IntX IntXValue
        {
            get { return _value == null ? 0 : _value; }
        }

        public long LongValue
        {
            [TypeConversion(typeof(Signed), typeof(long))]
            [SideEffectFree]
            get
            {
                if (_value == null)
                    return 0;

                uint[] digits;
                bool negative;
                _value.GetInternalState(out digits, out negative);
                long result;
                if (digits.Length == 0)
                    return 0;
                else if (digits.Length == 1)
                {
                    result = (long)digits[0];
                }
                else
                {
                    result = (long)digits[0] |
                        ((long)digits[1] << 32);
                    if (digits.Length >= 3 || result < 0)
                    {
                        if (digits.Length == 2 &&
                            result == long.MinValue &&
                            negative)
                        {
                            return long.MinValue;
                        }

                        switch (DesignContext.Instance.FixPoint.OverflowMode)
                        {
                            case EOverflowMode.Fail:
                                throw new InvalidOperationException("Signed value does not fit into a long");

                            case EOverflowMode.Saturate:
                                return negative ? long.MinValue : long.MaxValue;

                            case EOverflowMode.Wrap:
                                return negative ? -result : result;

                            default:
                                throw new NotImplementedException();
                        }
                    }
                }
                if (negative)
                    result = -result;
                return result;
            }
        }

        public string ToString(int radix, bool pad = false)
        {
            string result = _value.ToString((uint)radix);
            if (pad)
            {
                long maxDigits = Math.Max((long)Math.Ceiling((double)Size * Math.Log(2.0, radix)), 1);
                long npad = maxDigits - result.Length;
                result = StringHelpers.Zeros(npad) + result;
            }
            return result;
        }

        [TypeConversion(typeof(Signed), typeof(string))]
        [SideEffectFree]
        public override string ToString()
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix);
        }

        public override bool Equals(object obj)
        {
            if (obj is Signed)
            {
                Signed other = (Signed)obj;
                return (Size == other.Size) &&
                    object.Equals(_value, other._value);
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return 
                (_value == null ? 0 : _value.GetHashCode()) ^ 
                    (int)Size;
        }

        /// <summary>
        /// Returns the size (in bits) of this Signed value.
        /// </summary>
        [TypeParameter(typeof(IntToZeroBasedDownRangeConverter))]
        public int Size
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.PropertyRef, ESizedProperties.Size)]
            [SideEffectFree]
            get
            {
                return _size;
            }
        }

        /// <summary>
        /// Converts this Signed value to a new size.
        /// </summary>
        /// <param name="newWidth">The new size to which this Signed should be converted</param>
        /// <returns>The converted Signed value</returns>
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Resize)]
        [SideEffectFree]
        public Signed Resize(int newWidth)
        {
            IntX resized = Trim(_value, newWidth);
            return new Signed(resized, newWidth);
        }

        /// <summary>
        /// Adds two Signed values.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The sum</returns>
        public static Signed operator +(Signed a, Signed b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.VHDLCompliant:
                case EArithSizingMode.InSizeIsOutSize:
                    rsize = size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Signed(Trim(a.IntXValue + b.IntXValue, rsize), rsize);
        }

        /// <summary>
        /// Subtracts two Signed values.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The difference</returns>
        public static Signed operator -(Signed a, Signed b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.VHDLCompliant:
                case EArithSizingMode.InSizeIsOutSize:
                    rsize = size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Signed(Trim(a.IntXValue - b.IntXValue, rsize), rsize);
        }

        /// <summary>
        /// Negates a Signed value.
        /// </summary>
        /// <param name="a">The value</param>
        /// <returns>The negated value</returns>
        public static Signed operator -(Signed a)
        {
            int rsize = a.Size;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = a.Size + 1;
                    break;

                case EArithSizingMode.VHDLCompliant:
                case EArithSizingMode.InSizeIsOutSize:
                    rsize = a.Size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Signed(Trim(-a.IntXValue, rsize), rsize);
        }

        /// <summary>
        /// Multiplies two Signed values.
        /// </summary>
        /// <param name="a">The first factor</param>
        /// <param name="b">The second factor</param>
        /// <returns>The product</returns>
        public static Signed operator *(Signed a, Signed b)
        {
            int rsize = a.Size + b.Size;
            return new Signed(a.IntXValue * b.IntXValue, rsize);
        }

        /// <summary>
        /// Divides two Signed values.
        /// </summary>
        /// <param name="a">The dividend</param>
        /// <param name="b">The divisor</param>
        /// <returns>The quotient</returns>
        public static Signed operator /(Signed a, [SignedDivisionGuard] Signed b)
        {
            int rsize = a.Size + 1;
            return new Signed(a.IntXValue / b.IntXValue, rsize);
        }

        public static Signed operator %(Signed a, Signed b)
        {
            int rsize = b.Size;
            return new Signed(IntX.Modulo(a.IntXValue, b.IntXValue, DivideMode.AutoNewton), rsize);
        }

        public static void DivMod(Signed a, Signed b, out Signed quot, out Signed rem)
        {
            IntX div, modRes;
            div = IntX.DivideModulo(a.IntXValue, b.IntXValue, out modRes);
            quot = new Signed(div, (int)a.Size);
            rem = new Signed(modRes, (int)b.Size);
        }

        [RewriteIncrement(true, false)]
        [SideEffectFree]
        public static Signed operator ++(Signed a)
        {
            return (a + 1).Resize((int)a.Size);
        }

        [RewriteIncrement(true, true)]
        [SideEffectFree]
        public static Signed operator --(Signed a)
        {
            return (a - 1).Resize((int)a.Size);
        }

        /// <summary>
        /// Performs a left-shift on a Signed value.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The number of bits to shift left</param>
        /// <returns>The shifted value</returns>
        [SideEffectFree]
        public static Signed operator <<(Signed x, int count)
        {
            return new Signed(Trim(x.IntXValue << count, x.Size), x.Size);
        }

        /// <summary>
        /// Performs a logic right-shift on a Signed value.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The number of bits to shift right</param>
        /// <returns>The shifted value</returns>
        [SideEffectFree]
        public static Signed operator >>(Signed x, int count)
        {
            return new Signed(Trim(x.IntXValue >> count, x.Size), x.Size);
        }

        [SideEffectFree]
        public static implicit operator Signed(long value)
        {
            ulong test = (ulong)value;
            ulong sign = test & 0x8000000000000000;
            int size = 65;
            while ((test & 0x8000000000000000) == sign && size > 1)
            {
                test <<= 1;
                --size;
            }
            return FromLong(value, size);
        }

        public static bool operator <(Signed a, Signed b)
        {
            return a.IntXValue < b.IntXValue;
        }

        public static bool operator >(Signed a, Signed b)
        {
            return a.IntXValue > b.IntXValue;
        }

        public static bool operator <=(Signed a, Signed b)
        {
            return a.IntXValue <= b.IntXValue;
        }

        public static bool operator >=(Signed a, Signed b)
        {
            return a.IntXValue >= b.IntXValue;
        }

        public static bool operator ==(Signed a, Signed b)
        {
            return a.IntXValue == b.IntXValue;
        }

        public static bool operator !=(Signed a, Signed b)
        {
            return a.IntXValue != b.IntXValue;
        }

        public static TypeDescriptor MakeType(int width)
        {
            return TypeDescriptor.GetTypeOf(Signed.FromInt(0, width));
        }
    }
#else
    /// <summary>
    /// This struct represents a signed integer of an arbitrary size.
    /// </summary>
    [MapToIntrinsicType(EIntrinsicTypes.Signed)]
    [SLVSerializable(typeof(Signed), typeof(SignedToSLV))]
    [SignedAlgebraicType]
    public struct Signed : ISized
    {
        public static readonly Signed Zero = new Signed(0, 1);
        public static readonly Signed One = new Signed(1, 2);

        private BigInteger _value;
        private int _size;

        /// <summary>
        /// Constructs the Signed struct from a value and a desired size.
        /// </summary>
        /// <param name="value">The integer value</param>
        /// <param name="size">The integer size (in bits)</param>
        private Signed(BigInteger value, int size)
        {
            Contract.Requires(size >= 0);
            Contract.Requires(
                value == null ||
                value.CompareTo(0) == 0 ||
                (value <= (BigInteger.One << (size - 1)) - 1 &&
                value >= -(BigInteger.One << (size - 1))));

            _value = value;
            _size = size;
        }

        private static long Trim(long value, int size)
        {
            if (size >= 64)
                return value;
            if (size == 0)
            {
                if (value != 0 &&
                    DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Fail)
                    throw new ArgumentException();
                return 0;
            }
            if (DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Wrap)
            {
                value <<= (int)(64 - size);
                value >>= (int)(64 - size);
                return value;
            }
            else
            {
                long max = (1L << (int)(size - 1)) - 1;
                long min = -(1L << (int)(size - 1));
                if (DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Fail)
                {
                    if (value > max || value < min)
                        throw new ArgumentException();
                }
                else if (DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Saturate)
                {
                    if (value > max)
                        value = max;
                    else if (value < min)
                        value = min;
                }
                else
                    throw new NotImplementedException();

                return value;
            }
        }

        private static BigInteger Trim(BigInteger value, int size)
        {
            if (value == null)
                return BigInteger.Zero;

            if (size == 0)
            {
                if (value != 0 &&
                    DesignContext.Instance.FixPoint.OverflowMode == EOverflowMode.Fail)
                    throw new ArgumentException();
                return 0;
            }

            var rem = BigInteger.One << (int)size;
            var rem2 = rem >> 1;
            switch (DesignContext.Instance.FixPoint.OverflowMode)
            {
                case EOverflowMode.Wrap:
                    value = value & (rem - 1);
                    if (value >= rem2)
                        value -= rem;
                    else if (value < -rem2)
                        value += rem;
                    return value;

                case EOverflowMode.Fail:
                    if (value > rem2 - 1 || value < -rem2)
                        throw new ArgumentException();
                    return value;

                case EOverflowMode.Saturate:
                    if (value > rem2 - 1)
                        return rem2 - 1;
                    else if (value < -rem2)
                        return -rem2;
                    else
                        return value;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts a long value to a Signed value
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target size of the Signed value</param>
        /// <returns>The converted Signed value</returns>
        [TypeConversion(typeof(long), typeof(Signed))]
        [SideEffectFree]
        public static Signed FromLong(long value, int size)
        {
            return new Signed(Trim(value, size), size);
        }

        /// <summary>
        /// Converts an int value to a Signed value
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target size of the Signed value</param>
        /// <returns>The converted Signed value</returns>
        [TypeConversion(typeof(int), typeof(Signed))]
        [SideEffectFree]
        public static Signed FromInt(int value, int size)
        {
            return new Signed(Trim(value, size), size);
        }

        public static Signed FromBigInt(BigInteger value, int size)
        {
            return new Signed(Trim(value, size), size);
        }

        /// <summary>
        /// Converts this Signed value to an StdLogicVector representation.
        /// </summary>        
        public StdLogicVector SLVValue
        {
            [TypeConversion(typeof(Signed), typeof(StdLogicVector))]
            [SideEffectFree]
            get
            {
                var digits = _value.ToByteArray();
                StdLogic[] bits = new StdLogic[_size];
                int k = 0;
                for (int i = 0; i < digits.Length; i++)
                {
                    byte curw = digits[i];
                    for (int j = 0; j < 8 && k < _size; j++, k++)
                    {
                        if ((curw & 1) == 1)
                            bits[k] = StdLogic._1;
                        else
                            bits[k] = StdLogic._0;
                        curw >>= 1;
                    }
                }
                var pad = _value < 0 ? StdLogic._1 : StdLogic._0;
                while (k < _size)
                {
                    bits[k++] = pad;
                }
                return StdLogicVector.FromStdLogic(bits);
            }
        }

        public BigInteger BigIntValue
        {
            get { return _value; }
        }

        public long LongValue
        {
            [TypeConversion(typeof(Signed), typeof(long))]
            [SideEffectFree]
            get
            {
                if (_value == BigInteger.Zero)
                    return 0;

                var digits = _value.ToByteArray();
                if (digits.Length == 0)
                {
                    return 0;
                }
                else
                {
                    long result = (long)(sbyte)digits[digits.Length - 1];
                    int n = 2;
                    for (int i = digits.Length - 2; i >= 0; i--, n++)
                    {
                        if (n > 8)
                        {
                            switch (DesignContext.Instance.FixPoint.OverflowMode)
                            {
                                case EOverflowMode.Fail:
                                    throw new InvalidOperationException("Signed value does not fit into a long");

                                case EOverflowMode.Saturate:
                                    if (_value.Sign < 0)
                                        result = long.MinValue;
                                    else if (_value.Sign > 0)
                                        result = long.MaxValue;
                                    else
                                        throw new InvalidOperationException("Got multiple digits for zero value - impossible.");
                                    return result;

                                case EOverflowMode.Wrap:
                                    break;
                            }
                        }
                        result = (result << 8) | digits[i];
                    }
                    return result;
                }
            }
        }

        public string ToString(int radix, bool pad = false)
        {
            int maxDigits = Math.Max((int)Math.Ceiling((double)Size * Math.Log(2.0, radix)), 1);
            string format;
            switch (radix)
            {
                case 10:
                    format = pad ? string.Format("D{0}", maxDigits) : "R";
                    break;

                case 16:
                    format = pad ? string.Format("X{0}", maxDigits) : "X";
                    break;

                case 2:
                    {
                        var slv = SLVValue;
                        if (pad)
                        {
                            var padslv = _value.Sign < 0 ? 
                                StdLogicVector._0s(maxDigits - _size) : 
                                StdLogicVector._1s(maxDigits - _size);
                            slv = padslv.Concat(slv);
                        }
                        return slv.ToString();
                    }

                default:
                    throw new NotSupportedException("Currently only radix 2, 10 or 16 supported");

            }
            return _value.ToString(format);
        }

        [TypeConversion(typeof(Signed), typeof(string))]
        [SideEffectFree]
        public override string ToString()
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix);
        }

        public override bool Equals(object obj)
        {
            if (obj is Signed)
            {
                Signed other = (Signed)obj;
                return (Size == other.Size) &&
                    object.Equals(_value, other._value);
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode() ^ (int)Size;
        }

        /// <summary>
        /// Returns the size (in bits) of this Signed value.
        /// </summary>
        [TypeParameter(typeof(IntToZeroBasedDownRangeConverter))]
        public int Size
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.PropertyRef, ESizedProperties.Size)]
            [SideEffectFree]
            get
            {
                return _size;
            }
        }

        /// <summary>
        /// Converts this Signed value to a new size.
        /// </summary>
        /// <param name="newWidth">The new size to which this Signed should be converted</param>
        /// <returns>The converted Signed value</returns>
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Resize)]
        [SideEffectFree]
        public Signed Resize(int newWidth)
        {
            var resized = Trim(_value, newWidth);
            return new Signed(resized, newWidth);
        }

        /// <summary>
        /// Adds two Signed values.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The sum</returns>
        public static Signed operator +(Signed a, Signed b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.VHDLCompliant:
                case EArithSizingMode.InSizeIsOutSize:
                    rsize = size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Signed(Trim(a.BigIntValue + b.BigIntValue, rsize), rsize);
        }

        /// <summary>
        /// Subtracts two Signed values.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The difference</returns>
        public static Signed operator -(Signed a, Signed b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.VHDLCompliant:
                case EArithSizingMode.InSizeIsOutSize:
                    rsize = size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Signed(Trim(a.BigIntValue - b.BigIntValue, rsize), rsize);
        }

        /// <summary>
        /// Negates a Signed value.
        /// </summary>
        /// <param name="a">The value</param>
        /// <returns>The negated value</returns>
        public static Signed operator -(Signed a)
        {
            int rsize = a.Size;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = a.Size + 1;
                    break;

                case EArithSizingMode.VHDLCompliant:
                case EArithSizingMode.InSizeIsOutSize:
                    rsize = a.Size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Signed(Trim(-a.BigIntValue, rsize), rsize);
        }

        /// <summary>
        /// Multiplies two Signed values.
        /// </summary>
        /// <param name="a">The first factor</param>
        /// <param name="b">The second factor</param>
        /// <returns>The product</returns>
        public static Signed operator *(Signed a, Signed b)
        {
            int rsize = a.Size + b.Size;
            return new Signed(a.BigIntValue * b.BigIntValue, rsize);
        }

        /// <summary>
        /// Divides two Signed values.
        /// </summary>
        /// <param name="a">The dividend</param>
        /// <param name="b">The divisor</param>
        /// <returns>The quotient</returns>
        public static Signed operator /(Signed a, [SignedDivisionGuard] Signed b)
        {
            int rsize = a.Size + 1;
            return new Signed(a.BigIntValue / b.BigIntValue, rsize);
        }

        /// <summary>
        /// Computes the remainder of a divided by b.
        /// </summary>
        /// <param name="a">Dividend</param>
        /// <param name="b">Divisor</param>
        /// <returns>Remainder</returns>
        public static Signed operator %(Signed a, Signed b)
        {
            int rsize = b.Size;
            var div = 2 * b.BigIntValue;
            var rem = a.BigIntValue % div;
            if (rem > b.BigIntValue)
                rem -= div;
            else if (rem < -b.BigIntValue)
                rem += div;
            return new Signed(rem, rsize);
        }

        public static void DivMod(Signed a, Signed b, out Signed quot, out Signed rem)
        {
            BigInteger birem;
            var div = BigInteger.DivRem(a.BigIntValue, b.BigIntValue, out birem);
            quot = new Signed(div, (int)a.Size);
            rem = new Signed(birem, (int)b.Size);
        }

        [RewriteIncrement(true, false)]
        [SideEffectFree]
        public static Signed operator ++(Signed a)
        {
            return (a + 1).Resize((int)a.Size);
        }

        [RewriteIncrement(true, true)]
        [SideEffectFree]
        public static Signed operator --(Signed a)
        {
            return (a - 1).Resize((int)a.Size);
        }

        /// <summary>
        /// Performs a left-shift on a Signed value.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The number of bits to shift left</param>
        /// <returns>The shifted value</returns>
        [SideEffectFree]
        public static Signed operator <<(Signed x, int count)
        {
            return new Signed(Trim(x.BigIntValue << count, x.Size), x.Size);
        }

        /// <summary>
        /// Performs a logic right-shift on a Signed value.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The number of bits to shift right</param>
        /// <returns>The shifted value</returns>
        [SideEffectFree]
        public static Signed operator >>(Signed x, int count)
        {
            return new Signed(Trim(x.BigIntValue >> count, x.Size), x.Size);
        }

        [SideEffectFree]
        public static implicit operator Signed(long value)
        {
            ulong test = (ulong)value;
            ulong sign = test & 0x8000000000000000;
            int size = 65;
            while ((test & 0x8000000000000000) == sign && size > 1)
            {
                test <<= 1;
                --size;
            }
            return FromLong(value, size);
        }

        public static bool operator <(Signed a, Signed b)
        {
            return a.BigIntValue < b.BigIntValue;
        }

        public static bool operator >(Signed a, Signed b)
        {
            return a.BigIntValue > b.BigIntValue;
        }

        public static bool operator <=(Signed a, Signed b)
        {
            return a.BigIntValue <= b.BigIntValue;
        }

        public static bool operator >=(Signed a, Signed b)
        {
            return a.BigIntValue >= b.BigIntValue;
        }

        public static bool operator ==(Signed a, Signed b)
        {
            return a.BigIntValue == b.BigIntValue;
        }

        public static bool operator !=(Signed a, Signed b)
        {
            return a.BigIntValue != b.BigIntValue;
        }

        public static TypeDescriptor MakeType(int width)
        {
            return TypeDescriptor.GetTypeOf(Signed.FromInt(0, width));
        }
    }
#endif
}

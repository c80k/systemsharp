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

namespace SystemSharp.DataTypes
{
    class UnsignedSerializer : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            Unsigned number = (Unsigned)value;
            return number.SLVValue;
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return slv.UnsignedValue;
        }
    }

    class UnsignedAlgebraicType : AlgebraicTypeAttribute
    {
        public override object CreateInstance(ETypeCreationOptions options, object template)
        {
            if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                options.HasFlag(ETypeCreationOptions.NonZero))
            {
                if (template == null)
                {
                    return Unsigned.One;
                }
                else
                {
                    var unsigned = (Unsigned)template;
                    return Unsigned.FromUInt(1, unsigned.Size);
                }
            }
            else
            {
                if (template == null)
                {
                    return Unsigned.Zero;
                }
                else
                {
                    var unsigned = (Unsigned)template;
                    return Unsigned.FromUInt(0, unsigned.Size);
                }
            }
        }
    }

    class UnsignedDivisionGuard : GuardedArgumentAttribute
    {
        public override object CorrectArgument(object arg)
        {
            return Unsigned.One;
        }
    }

#if USE_INTX
    /// <summary>
    /// This struct represents an unsigned integer of an arbitrary size.
    /// </summary>
    [MapToIntrinsicType(EIntrinsicTypes.Unsigned)]
    [SLVSerializable(typeof(Unsigned), typeof(UnsignedSerializer))]
    [UnsignedAlgebraicType]
    public struct Unsigned : ISized
    {
        public static readonly Unsigned Zero = new Unsigned(0, 1);
        public static readonly Unsigned One = new Unsigned(1, 1);

        private IntX _value;
        private int _size;

        /// <summary>
        /// Constructs the Unsigned from a value and a desired size.
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="size">The desired size (in bits)</param>
        private Unsigned(IntX value, int size)
        {
            Contract.Requires(size >= 0);
            Contract.Requires(
                value == null ||
                value.CompareTo(0) == 0 ||
                (value <= (((IntX)1) << size) - 1));

            _value = value;
            _size = size;
        }

        private static ulong Trim(ulong value, int size)
        {
            if (size >= 64)
                return value;
            else
            {
                ulong max = (1UL << size) - 1;
                switch (DesignContext.Instance.FixPoint.OverflowMode)
                {
                    case EOverflowMode.Wrap:
                        value &= max;
                        break;

                    case EOverflowMode.Saturate:
                        if (value > max)
                            value = max;
                        break;

                    case EOverflowMode.Fail:
                        if (value > max)
                            throw new ArgumentException();
                        break;

                    default:
                        throw new NotImplementedException();
                }
                return value;
            }
        }

        private static IntX Trim(IntX value, int size)
        {
            IntX rem = new IntX(1) << size;
            switch (DesignContext.Instance.FixPoint.OverflowMode)
            {
                case EOverflowMode.Wrap:
                    value = IntX.Modulo(value, rem, DivideMode.AutoNewton);
                    if (value < 0)
                        value += rem;
                    break;

                case EOverflowMode.Saturate:
                    if (value > rem)
                        value = rem;
                    else if (value < 0)
                        value = 0;
                    break;

                case EOverflowMode.Fail:
                    if (value > rem || value < 0)
                        throw new ArgumentException();
                    break;
            }
            return value;
        }

        /// <summary>
        /// Converts a ulong value to an Unsigned value.
        /// </summary>
        /// <param name="value">The ulong value</param>
        /// <param name="size">The desired width</param>
        /// <returns>The Unsigned value</returns>
        [TypeConversion(typeof(ulong), typeof(Unsigned))]
        [SideEffectFree]
        public static Unsigned FromULong(ulong value, int size)
        {
            return new Unsigned(new IntX(Trim(value, size)), size);
        }

        /// <summary>
        /// Converts a uint value to an Unsigned value.
        /// </summary>
        /// <param name="value">The uint value</param>
        /// <param name="size">The desired width</param>
        /// <returns>The Unsigned value</returns>
        [TypeConversion(typeof(uint), typeof(Unsigned))]
        [SideEffectFree]
        public static Unsigned FromUInt(uint value, int size)
        {
            return new Unsigned(new IntX(Trim(value, size)), size);
        }

        [StaticEvaluation]
        public static Unsigned FromIntX(IntX value, int size)
        {
            return new Unsigned(Trim(value, size), size);
        }

        /// <summary>
        /// Converts this Unsigned value to an StdLogicVector.
        /// </summary>        
        public StdLogicVector SLVValue
        {
            [TypeConversion(typeof(Unsigned), typeof(StdLogicVector))]
            [SideEffectFree]
            get
            {
                if (_value == null)
                    return StdLogicVector.Empty;

                string encoded = _value.ToString(2);
                StringBuilder sb = new StringBuilder();
                char app;
                if (_value < new IntX(0))
                    app = '1';
                else
                    app = '0';
                for (int i = encoded.Length; i < _size; i++)
                    sb.Append(app);
                sb.Append(encoded);
                return (StdLogicVector)sb.ToString();
            }
        }

        public IntX IntXValue
        {
            get { return _value == null ? 0 : _value; }
        }

        public ulong ULongValue
        {
            [TypeConversion(typeof(Unsigned), typeof(ulong))]
            [SideEffectFree]
            get
            {
                if (_value == null)
                    return 0;

                uint[] digits;
                bool negative;
                _value.GetInternalState(out digits, out negative);
                if (digits.Length == 0)
                    return 0;
                else if (digits.Length == 1)
                    return digits[0];
                else
                    return (ulong)digits[0] |
                        (((ulong)digits[1]) << 32);
            }
        }

        public int IntValue
        {
            [TypeConversion(typeof(Unsigned), typeof(int))]
            [SideEffectFree]
            get
            {
                if (_value == null)
                    return 0;

                uint[] digits;
                bool negative;
                _value.GetInternalState(out digits, out negative);
                if (digits.Length == 0)
                    return 0;
                else
                    return (int)digits[0];
            }
        }

        public Signed SignedValue
        {
            [TypeConversion(typeof(Unsigned), typeof(Signed))]
            [SideEffectFree]
            get
            {
                return Signed.FromIntX(IntXValue, Size + 1);
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

        [TypeConversion(typeof(Unsigned), typeof(string))]
        [SideEffectFree]
        public override string ToString()
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix);
        }

        public override bool Equals(object obj)
        {
            if (obj is Unsigned)
            {
                Unsigned other = (Unsigned)obj;
                return (Size == other.Size &&
                    object.Equals(_value, other._value));
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
        /// Returns the size (in bits) of this Unsigned value.
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
        /// Resizes this Unsigned value to a desired target width.
        /// </summary>
        /// <param name="newWidth">The desired target width</param>
        /// <returns>The resized Unsigned value</returns>
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Resize)]
        [SideEffectFree]
        public Unsigned Resize(int newWidth)
        {
            if (newWidth < Size)
            {
                IntX one = new IntX(1);
                IntX rem = (one << newWidth);
                IntX newValue = IntX.Modulo(_value, rem, DivideMode.AutoNewton);
                return new Unsigned(newValue, newWidth);
            }
            else
            {
                return new Unsigned(_value, newWidth);
            }
        }

        /// <summary>
        /// Provides access to the internal binary encoding.
        /// </summary>
        /// <param name="pos">The bit index</param>
        /// <returns>The state of the indexed bit</returns>
        public bool this[int pos]
        {
            get
            {
                int widx = pos / 32;
                int bidx = pos % 32;
                uint[] digits;
                bool neg;
                _value.GetInternalState(out digits, out neg);
                if (widx >= digits.Length)
                    return false;
                else
                    return (digits[widx] & (1 << bidx)) != 0;
            }
            set
            {
                int widx = pos / 32;
                int bidx = pos % 32;
                uint[] digits;
                bool neg;
                _value.GetInternalState(out digits, out neg);
                if (widx < digits.Length)
                {
                    if (value)
                        digits[widx] |= (uint)(1 << bidx);
                    else
                        digits[widx] &= (uint)~(1 << bidx);
                    _value = new IntX(digits, neg);
                }
                else if (value)
                {
                    _value += (new IntX(1) << pos);
                }
            }
        }

        /// <summary>
        /// Converts an Unsigned value to a ulong value.
        /// </summary>
        /// <param name="value">The Unsigned value</param>
        /// <returns>The ulong value</returns>
        [TypeConversion(typeof(Unsigned), typeof(ulong))]
        [SideEffectFree]
        public static explicit operator ulong(Unsigned value)
        {
            uint[] digits;
            bool neg;
            value._value.GetInternalState(out digits, out neg);
            if (digits.Length == 0)
                return 0;
            else if (digits.Length == 1)
                return digits[0];
            else
                return ((ulong)digits[1] << 32) | digits[0];
        }

        /// <summary>
        /// Converts an Unsigned value to a uint value.
        /// </summary>
        /// <param name="value">The Unsigned value</param>
        /// <returns>The uint value</returns>
        [TypeConversion(typeof(Unsigned), typeof(uint))]
        [SideEffectFree]
        public static explicit operator uint(Unsigned value)
        {
            uint[] digits;
            bool neg;
            value._value.GetInternalState(out digits, out neg);
            if (digits.Length == 0)
                return 0;
            else
                return digits[0];
        }

        [SideEffectFree]
        public static implicit operator Unsigned(ulong value)
        {
            ulong test = value;
            int size = 64;
            while ((test & 0x8000000000000000) != 0 && size > 1)
            {
                test <<= 1;
                --size;
            }
            return Unsigned.FromULong(value, size);
        }

        /// <summary>
        /// Adds two Unsigned values.
        /// </summary>
        /// <param name="a">The first summand</param>
        /// <param name="b">The second summand</param>
        /// <returns>The sum</returns>
        public static Unsigned operator +(Unsigned a, Unsigned b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.InSizeIsOutSize:
                case EArithSizingMode.VHDLCompliant:
                    rsize = a.Size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Unsigned(Trim(a.IntXValue + b.IntXValue, rsize), rsize);
        }

        /// <summary>
        /// Subtracts two Unsigned values.
        /// </summary>
        /// <param name="a">The minuend</param>
        /// <param name="b">The subtrahend</param>
        /// <returns>The difference</returns>
        public static Unsigned operator -(Unsigned a, Unsigned b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.InSizeIsOutSize:
                case EArithSizingMode.VHDLCompliant:
                    rsize = size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Unsigned(Trim(a.IntXValue - b.IntXValue, rsize), rsize);
        }

        public static Signed operator -(Unsigned a)
        {
            return (-a.SignedValue).Resize(a.Size);
        }

        /// <summary>
        /// Multiplies two Unsigned values.
        /// </summary>
        /// <param name="a">The first factor</param>
        /// <param name="b">The second factor</param>
        /// <returns>The product</returns>
        public static Unsigned operator *(Unsigned a, Unsigned b)
        {
            int rsize = a.Size + b.Size;
            return new Unsigned(a.IntXValue * b.IntXValue, rsize);
        }

        /// <summary>
        /// Divides two Unsigned values.
        /// </summary>
        /// <param name="a">The dividend</param>
        /// <param name="b">The divisor</param>
        /// <returns>The quotient</returns>
        public static Unsigned operator /(Unsigned a, [UnsignedDivisionGuard] Unsigned b)
        {
            int rsize = a.Size;
            return new Unsigned(a.IntXValue / b.IntXValue, rsize);
        }

        public static Unsigned operator %(Unsigned a, Unsigned b)
        {
            int rsize = b.Size;
            return new Unsigned(IntX.Modulo(a.IntXValue, b.IntXValue, DivideMode.AutoNewton), rsize);
        }

        public static void DivMod(Unsigned a, Unsigned b, out Unsigned quot, out Unsigned rem)
        {
            IntX div, modRes;
            div = IntX.DivideModulo(a.IntXValue, b.IntXValue, out modRes);
            quot = new Unsigned(div, a.Size);
            rem = new Unsigned(modRes, b.Size);
        }

        [RewriteIncrement(false, false)]
        public static Unsigned operator ++(Unsigned a)
        {
            return (a + 1).Resize((int)a.Size);
        }

        [RewriteIncrement(false, true)]
        public static Unsigned operator --(Unsigned a)
        {
            return (a - 1).Resize((int)a.Size);
        }

        /// <summary>
        /// Shifts an Unsigned value to the left.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The amount of bits to shift</param>
        /// <returns>The shifted value</returns>
        public static Unsigned operator <<(Unsigned x, int count)
        {
            return new Unsigned(Trim(x.IntXValue << count, x.Size), x.Size);
        }

        /// <summary>
        /// Shifts an Unsigned value to the right.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The amount of bits to shift</param>
        /// <returns>The shifted value</returns>
        public static Unsigned operator >>(Unsigned x, int count)
        {
            return new Unsigned(Trim(x.IntXValue >> count, x.Size), x.Size);
        }

        public static bool operator <(Unsigned a, Unsigned b)
        {
            return a.IntXValue < b.IntXValue;
        }

        public static bool operator >(Unsigned a, Unsigned b)
        {
            return a.IntXValue > b.IntXValue;
        }

        public static bool operator <=(Unsigned a, Unsigned b)
        {
            return a.IntXValue <= b.IntXValue;
        }

        public static bool operator >=(Unsigned a, Unsigned b)
        {
            return a.IntXValue >= b.IntXValue;
        }

        public static bool operator ==(Unsigned a, Unsigned b)
        {
            return a.IntXValue == b.IntXValue;
        }

        public static bool operator !=(Unsigned a, Unsigned b)
        {
            return a.IntXValue != b.IntXValue;
        }

        public static TypeDescriptor MakeType(int size)
        {
            return TypeDescriptor.GetTypeOf(Unsigned.FromUInt(0, size));
        }
    }
#else
    /// <summary>
    /// An unsigned integer
    /// </summary>
    [MapToIntrinsicType(EIntrinsicTypes.Unsigned)]
    [SLVSerializable(typeof(Unsigned), typeof(UnsignedSerializer))]
    [UnsignedAlgebraicType]
    public struct Unsigned : ISized
    {
        public static readonly Unsigned Zero = new Unsigned(0, 1);
        public static readonly Unsigned One = new Unsigned(1, 1);

        private BigInteger _value;
        private int _size;

        /// <summary>
        /// Constructs the Unsigned from a value and a desired size.
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="size">The desired size (in bits)</param>
        private Unsigned(BigInteger value, int size)
        {
            Contract.Requires<ArgumentOutOfRangeException>(size >= 0, "size must be positive.");
            Contract.Requires<ArgumentOutOfRangeException>(
                value == null ||
                value.CompareTo(0) == 0 ||
                (value >= 0 && value <= ((BigInteger.One << size) - 1)), "value out of range");

            _value = value;
            _size = size;
        }

        private static ulong Trim(ulong value, int size)
        {
            if (size >= 64)
                return value;
            else
            {
                ulong max = (1UL << size) - 1;
                switch (DesignContext.Instance.FixPoint.OverflowMode)
                {
                    case EOverflowMode.Wrap:
                        value &= max;
                        break;

                    case EOverflowMode.Saturate:
                        if (value > max)
                            value = max;
                        break;

                    case EOverflowMode.Fail:
                        if (value > max)
                            throw new ArgumentException();
                        break;

                    default:
                        throw new NotImplementedException();
                }
                return value;
            }
        }

        private static BigInteger Trim(BigInteger value, int size)
        {
            BigInteger rem = BigInteger.One << size;
            switch (DesignContext.Instance.FixPoint.OverflowMode)
            {
                case EOverflowMode.Wrap:
                    value = value & (rem - 1);
                    if (value < 0)
                        value += rem;
                    break;

                case EOverflowMode.Saturate:
                    if (value > rem)
                        value = rem;
                    else if (value < 0)
                        value = 0;
                    break;

                case EOverflowMode.Fail:
                    if (value > rem || value < 0)
                        throw new ArgumentException();
                    break;
            }
            return value;
        }

        /// <summary>
        /// Converts a ulong value to an Unsigned value.
        /// </summary>
        /// <param name="value">The ulong value</param>
        /// <param name="size">The desired width</param>
        /// <returns>The Unsigned value</returns>
        [TypeConversion(typeof(ulong), typeof(Unsigned))]
        [SideEffectFree]
        public static Unsigned FromULong(ulong value, int size)
        {
            return new Unsigned(Trim(value, size), size);
        }

        /// <summary>
        /// Converts a <c>uint</c> value to an <c>Unsigned</c> value.
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <param name="size">The desired width</param>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="size"/> is 0 or less, or if
        /// <paramref name="value"/> is out of representable range.</exception>
        [TypeConversion(typeof(uint), typeof(Unsigned))]
        [SideEffectFree]
        public static Unsigned FromUInt(uint value, int size)
        {
            return new Unsigned(Trim(value, size), size);
        }

        /// <summary>
        /// Converts a non-negative <c>BigInteger</c> value to an <c>Unsigned</c> value.
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <param name="size">The desired width</param>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="size"/> is 0 or less, or if
        /// <paramref name="value"/> is negative or out of representable range.</exception>
        [StaticEvaluation]
        public static Unsigned FromBigInt(BigInteger value, int size)
        {
            return new Unsigned(Trim(value, size), size);
        }

        /// <summary>
        /// Returns the binary representation of this value.
        /// </summary>        
        public StdLogicVector SLVValue
        {
            [TypeConversion(typeof(Unsigned), typeof(StdLogicVector))]
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
                while (k < _size)
                {
                    bits[k++] = StdLogic._0;
                }
                return StdLogicVector.FromStdLogic(bits);
            }
        }

        /// <summary>
        /// Converts this value to <c>BigInteger</c>.
        /// </summary>
        public BigInteger BigIntValue
        {
            get { return _value; }
        }

        /// <summary>
        /// Converts this value to <c>ulong</c>. If this value is larger than 64 bits, an arithmetic overflow might
        /// occur. In that case, the behavior of this conversion depends on the currently selected arithmetic overflow mode
        /// (<seealso cref="FixedPointSettings"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">if this value is larger than 64 bits and current arithmetic overflow
        /// mode is <c>EOverflowMode.Fail</c></exception>.
        public ulong ULongValue
        {
            [TypeConversion(typeof(Unsigned), typeof(ulong))]
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
                    ulong result = (ulong)digits[digits.Length - 1];
                    int n = 2;
                    for (int i = digits.Length - 2; i >= 0; i--, n++)
                    {
                        if (n > 8)
                        {
                            switch (DesignContext.Instance.FixPoint.OverflowMode)
                            {
                                case EOverflowMode.Fail:
                                    throw new InvalidOperationException("Unsigned value does not fit into ulong");

                                case EOverflowMode.Saturate:
                                    return ulong.MaxValue;

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

        /// <summary>
        /// Converts this value to <c>ulong</c>. If this value is larger than 31 bits, an arithmetic overflow might
        /// occur. In that case, the behavior of this conversion depends on the currently selected arithmetic overflow mode
        /// (<seealso cref="FixedPointSettings"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">if this value is larger than 31 bits and current arithmetic overflow
        /// mode is <c>EOverflowMode.Fail</c></exception>.
        public int IntValue
        {
            [TypeConversion(typeof(Unsigned), typeof(int))]
            [SideEffectFree]
            get
            {
                var ul = ULongValue;
                if (ul <= int.MaxValue)
                    return (int)ul;
                switch (DesignContext.Instance.FixPoint.OverflowMode)
                {
                    case EOverflowMode.Fail:
                        throw new InvalidOperationException("Unsigned value does not fit into int");

                    case EOverflowMode.Saturate:
                        return int.MaxValue;

                    case EOverflowMode.Wrap:
                        return (int)ul;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Converts this value to <c>Signed</c>. Because of the signed format, the resulting
        /// value requires one more bit than this representation.
        /// </summary>
        public Signed SignedValue
        {
            [TypeConversion(typeof(Unsigned), typeof(Signed))]
            [SideEffectFree]
            get
            {
                return Signed.FromBigInt(BigIntValue, Size + 1);
            }
        }

        /// <summary>
        /// Converts this value to a textual representation.
        /// </summary>
        /// <param name="radix">number system base to use, only 2, 10 and 16 are supported so far</param>
        /// <param name="pad">whether to pad the returned string with leading zeroes</param>
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
                            var padslv = StdLogicVector._0s(maxDigits - _size);
                            slv = padslv.Concat(slv);
                        }
                        return slv.ToString();
                    }

                default:
                    throw new NotSupportedException("Currently only radix 2, 10 or 16 supported");

            }
            return _value.ToString(format);
        }

        /// <summary>
        /// Converts this value to a textual representation.
        /// </summary>
        /// <remarks>
        /// The number format base is determined by the currently selected default radix (<seealso cref="FixedPointSettings"/>).
        /// </remarks>
        [TypeConversion(typeof(Unsigned), typeof(string))]
        [SideEffectFree]
        public override string ToString()
        {
            return ToString(DesignContext.Instance.FixPoint.DefaultRadix);
        }

        /// <summary>
        /// Returns <c>true</c> iff <paramref name="obj"/> is another <c>Unsigned</c> with identical size and value.
        /// </remarks>
        public override bool Equals(object obj)
        {
            if (obj is Unsigned)
            {
                Unsigned other = (Unsigned)obj;
                return (Size == other.Size &&
                    object.Equals(_value, other._value));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode() ^ (int)Size;
        }

        /// <summary>
        /// Returns the size (in bits) of this Unsigned value.
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
        /// Resizes this Unsigned value to a desired target width.
        /// </summary>
        /// <param name="newWidth">The desired target width</param>
        /// <returns>The resized Unsigned value</returns>
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Resize)]
        [SideEffectFree]
        public Unsigned Resize(int newWidth)
        {
            return new Unsigned(Trim(BigIntValue, newWidth), newWidth);
        }

        /// <summary>
        /// Provides access to the internal binary encoding.
        /// </summary>
        /// <param name="pos">The bit index</param>
        /// <returns>The state of the indexed bit</returns>
        public bool this[int pos]
        {
            get
            {
                int widx = pos / 8;
                int bidx = pos % 8;
                byte[] digits = _value.ToByteArray();
                if (widx >= digits.Length)
                    return false;
                else
                    return (digits[widx] & (1 << bidx)) != 0;
            }
            set
            {
                int widx = pos / 8;
                int bidx = pos % 8;
                byte[] digits = _value.ToByteArray();
                if (widx < digits.Length)
                {
                    if (value)
                        digits[widx] |= (byte)(1 << bidx);
                    else
                        digits[widx] &= (byte)~(1 << bidx);
                    _value = new BigInteger(digits);
                }
                else if (value)
                {
                    _value += (BigInteger.One << pos);
                }
            }
        }

        /// <summary>
        /// Converts an Unsigned value to a ulong value.
        /// </summary>
        /// <param name="value">The Unsigned value</param>
        /// <returns>The ulong value</returns>
        [TypeConversion(typeof(Unsigned), typeof(ulong))]
        [SideEffectFree]
        public static explicit operator ulong(Unsigned value)
        {
            return value.ULongValue;
        }

        /// <summary>
        /// Converts an Unsigned value to a uint value.
        /// </summary>
        /// <param name="value">The Unsigned value</param>
        /// <returns>The uint value</returns>
        [TypeConversion(typeof(Unsigned), typeof(uint))]
        [SideEffectFree]
        public static explicit operator uint(Unsigned value)
        {
            ulong ul = value.ULongValue;
            if (ul <= uint.MaxValue)
                return (uint)ul;
            switch (DesignContext.Instance.FixPoint.OverflowMode)
            {
                case EOverflowMode.Fail:
                    throw new InvalidOperationException("Unsigned value does not fit into int");

                case EOverflowMode.Saturate:
                    return uint.MaxValue;

                case EOverflowMode.Wrap:
                    return (uint)ul;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts <paramref name="value"/> implcitly to <c>Unsigned</c>.
        /// </summary>
        [SideEffectFree]
        public static implicit operator Unsigned(ulong value)
        {
            return Unsigned.FromULong(value, 64);
        }

        /// <summary>
        /// Adds two Unsigned values.
        /// </summary>
        /// <param name="a">The first summand</param>
        /// <param name="b">The second summand</param>
        /// <returns>The sum</returns>
        public static Unsigned operator +(Unsigned a, Unsigned b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.InSizeIsOutSize:
                case EArithSizingMode.VHDLCompliant:
                    rsize = a.Size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Unsigned(Trim(a.BigIntValue + b.BigIntValue, rsize), rsize);
        }

        /// <summary>
        /// Subtracts two Unsigned values.
        /// </summary>
        /// <param name="a">The minuend</param>
        /// <param name="b">The subtrahend</param>
        /// <returns>The difference</returns>
        public static Unsigned operator -(Unsigned a, Unsigned b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize;
            switch (DesignContext.Instance.FixPoint.ArithSizingMode)
            {
                case EArithSizingMode.Safe:
                    rsize = size + 1;
                    break;

                case EArithSizingMode.InSizeIsOutSize:
                case EArithSizingMode.VHDLCompliant:
                    rsize = size;
                    break;

                default:
                    throw new NotImplementedException();
            }
            return new Unsigned(Trim(a.BigIntValue - b.BigIntValue, rsize), rsize);
        }

        /// <summary>
        /// Negates <paramref name="a"/>, returning a <c>Signed</c>.
        /// </summary>
        public static Signed operator -(Unsigned a)
        {
            return (-a.SignedValue).Resize(a.Size);
        }

        /// <summary>
        /// Multiplies two Unsigned values.
        /// </summary>
        /// <param name="a">The first factor</param>
        /// <param name="b">The second factor</param>
        /// <returns>The product</returns>
        public static Unsigned operator *(Unsigned a, Unsigned b)
        {
            int rsize = a.Size + b.Size;
            return new Unsigned(a.BigIntValue * b.BigIntValue, rsize);
        }

        /// <summary>
        /// Multiplies a double and an Unsigned value.
        /// </summary>
        /// <param name="a">The first factor</param>
        /// <param name="b">The second factor</param>
        /// <returns>The product</returns>
        public static double operator *(double a, Unsigned b)
        {
            return a * b.ULongValue;
        }

        /// <summary>
        /// Divides two Unsigned values.
        /// </summary>
        /// <param name="a">The dividend</param>
        /// <param name="b">The divisor</param>
        /// <returns>The quotient</returns>
        /// <exception cref="DivideByZeroException">if <paramref name="b"/> is zero.</exception>
        public static Unsigned operator /(Unsigned a, [UnsignedDivisionGuard] Unsigned b)
        {
            int rsize = a.Size;
            return new Unsigned(a.BigIntValue / b.BigIntValue, rsize);
        }

        /// <summary>
        /// Computes the remainder of a divided by b.
        /// </summary>
        /// <param name="a">Dividend</param>
        /// <param name="b">Divisor</param>
        /// <returns>Remainder</returns>
        /// <exception cref="DivideByZeroException">if <paramref name="b"/> is zero.</exception>
        public static Unsigned operator %(Unsigned a, Unsigned b)
        {
            int rsize = b.Size;
            return new Unsigned(a.BigIntValue % b.BigIntValue, rsize);
        }

        /// <summary>
        /// Computes quotient and remainder of the division <paramref name="a"/>/<paramref name="b"/>.
        /// </summary>
        /// <param name="a">The dividend</param>
        /// <param name="b">The divisor</param>
        /// <param name="quot">Reference to computed quotient</param>
        /// <param name="rem">Reference to computed remainder</param>
        /// <exception cref="DivideByZeroException">if <paramref name="b"/> is zero.</exception>
        public static void DivMod(Unsigned a, Unsigned b, out Unsigned quot, out Unsigned rem)
        {
            BigInteger birem;
            var div = BigInteger.DivRem(a.BigIntValue, b.BigIntValue, out birem);
            quot = new Unsigned(div, a.Size);
            rem = new Unsigned(birem, b.Size);
        }

        /// <summary>
        /// Increments <paramref name="a"/> by one. By definition, the result keeps its size. If the increment results in an
        /// arithmetic overflow, the behavior of this method 
        /// depends on the currently selected overflow mode (<seealso cref="FixedPointSettings"/>).
        /// </summary>
        [RewriteIncrement(false, false)]
        public static Unsigned operator ++(Unsigned a)
        {
            return (a + 1).Resize((int)a.Size);
        }

        /// <summary>
        /// Decrements <paramref name="a"/> by one. By definition, the result keeps its size. If <paramref name="a"/> is 0,
        /// the behavior of this method 
        /// depends on the currently selected overflow mode (<seealso cref="FixedPointSettings"/>).
        /// </summary>
        [RewriteIncrement(false, true)]
        public static Unsigned operator --(Unsigned a)
        {
            return (a - 1).Resize((int)a.Size);
        }

        /// <summary>
        /// Shifts an Unsigned value to the left.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The amount of bits to shift</param>
        /// <returns>The shifted value</returns>
        public static Unsigned operator <<(Unsigned x, int count)
        {
            return new Unsigned(Trim(x.BigIntValue << count, x.Size), x.Size);
        }

        /// <summary>
        /// Shifts an Unsigned value to the right.
        /// </summary>
        /// <param name="x">The value to be shifted</param>
        /// <param name="count">The amount of bits to shift</param>
        /// <returns>The shifted value</returns>
        public static Unsigned operator >>(Unsigned x, int count)
        {
            return new Unsigned(Trim(x.BigIntValue >> count, x.Size), x.Size);
        }

        /// <summary>
        /// Returns <c>true</c> iff <paramref name="a"/> is less than <paramref name="b"/>.
        /// </summary>
        public static bool operator <(Unsigned a, Unsigned b)
        {
            return a.BigIntValue < b.BigIntValue;
        }

        /// <summary>
        /// Returns <c>true</c> iff <paramref name="a"/> is greater than <paramref name="b"/>.
        /// </summary>
        public static bool operator >(Unsigned a, Unsigned b)
        {
            return a.BigIntValue > b.BigIntValue;
        }

        /// <summary>
        /// Returns <c>true</c> iff <paramref name="a"/> is less than or equal to <paramref name="b"/>.
        /// </summary>
        public static bool operator <=(Unsigned a, Unsigned b)
        {
            return a.BigIntValue <= b.BigIntValue;
        }

        /// <summary>
        /// Returns <c>true</c> iff <paramref name="a"/> is greater than or equal to <paramref name="b"/>.
        /// </summary>
        public static bool operator >=(Unsigned a, Unsigned b)
        {
            return a.BigIntValue >= b.BigIntValue;
        }

        /// <summary>
        /// Returns <c>true</c> iff <paramref name="a"/> equals <paramref name="b"/>.
        /// </summary>
        public static bool operator ==(Unsigned a, Unsigned b)
        {
            return a.BigIntValue == b.BigIntValue;
        }

        /// <summary>
        /// Returns <c>true</c> iff <paramref name="a"/> is not equal to <paramref name="b"/>.
        /// </summary>
        public static bool operator !=(Unsigned a, Unsigned b)
        {
            return a.BigIntValue != b.BigIntValue;
        }

        /// <summary>
        /// Creates a SysDOM type descriptor which describes unsigned numbers with <paramref name="size"/> integer bits.
        /// </summary>
        public static TypeDescriptor MakeType(int size)
        {
            return TypeDescriptor.GetTypeOf(Unsigned.FromUInt(0, size));
        }
    }
#endif

    /// <summary>
    /// This static class provides convenience methods for working with <c>Unsigned</c> values.
    /// </summary>
    public static class UnsignedExtensions
    {
        /// <summary>
        /// Returns the element at <paramref name="index"/> from <paramref name="array"/>.
        /// </summary>
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.GetArrayElement)]
        public static T Get<T>(this T[] array, Unsigned index)
        {
            return array[index.ULongValue];
        }
    }
}

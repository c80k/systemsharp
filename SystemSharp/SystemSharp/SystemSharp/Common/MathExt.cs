/**
 * Copyright 2011-2014 Christian Köllner
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Common
{
    /// <summary>
    /// This static class provides some specialized mathematical operations.
    /// </summary>
    public static class MathExt
    {
        /// <summary>
        /// Returns the smallest number P, such that P &gt;= <paramref name="number"/> and P is a power of 2.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="number"/> &lt; 0 or result would overflow</exception>
        /// <param name="number">a non-negative number</param>
        public static int CeilPow2(int number)
        {
            Contract.Requires<ArgumentOutOfRangeException>(number >= 0 && number <= int.MaxValue/2, "number");

            int pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        /// <summary>
        /// Returns the smallest number P, such that P &gt;= <paramref name="number"/> and P is a power of 2.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if result would overflow</exception>
        /// <param name="number">some number</param>
        public static uint CeilPow2(uint number)
        {
            Contract.Requires<ArgumentOutOfRangeException>(number <= uint.MaxValue / 2 + 1, "number");

            uint pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        /// <summary>
        /// Returns the smallest number P, such that P &gt;= <paramref name="number"/> and P is a power of 2.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if result would overflow</exception>
        /// <param name="number">some number</param>
        public static ulong CeilPow2(ulong number)
        {
            Contract.Requires<ArgumentOutOfRangeException>(number <= ulong.MaxValue / 2 + 1, "number");

            ulong pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        /// <summary>
        /// Returns the smallest number P, such that P &gt;= <paramref name="number"/> and P is a power of 2.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="number"/> &lt; 0 or result would overflow</exception>
        /// <param name="number">some number</param>
        public static ulong CeilPow2(double number)
        {
            Contract.Requires<ArgumentOutOfRangeException>(number >= 0 && number <= ulong.MaxValue / 2 + 1, "number");

            ulong pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        /// <summary>
        /// Returns the largest number P, such that P &lt;= <paramref name="number"/> and P is a power of 2.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="number"/> &lt; 0 or result would overflow</exception>
        /// <param name="number">a non-negative number</param>
        public static int FloorPow2(int number)
        {
            Contract.Requires<ArgumentOutOfRangeException>(number >= 0, "number");

            if (number > (int.MaxValue / 2 + 1))
                return int.MaxValue / 2 + 1;

            int pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;
            if (pow2 > number)
                pow2 /= 2;

            return pow2;
        }

        /// <summary>
        /// Returns the largest number P, such that P &lt;= <paramref name="number"/> and P is a power of 2.
        /// </summary>
        /// <param name="number">some number</param>
        public static ulong FloorPow2(ulong number)
        {
            if (number > (ulong.MaxValue / 2 + 1))
                return ulong.MaxValue / 2 + 1;

            ulong pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;
            if (pow2 > number)
                pow2 /= 2;

            return pow2;
        }

        /// <summary>
        /// Computes floor(log2(<paramref name="number"/>)) for positive numbers, returns -1 otherwise
        /// </summary>
        /// <param name="number">some number</param>
        public static int FloorLog2(long number)
        {
            int result = -1;
            while (number > 0)
            {
                number /= 2;
                result++;
            }
            return result;
        }

        /// <summary>
        /// Computes floor(log2(<paramref name="number"/>)) for positive numbers, returns -1 otherwise
        /// </summary>
        /// <param name="number">some number</param>
        public static int FloorLog2(ulong number)
        {
            int result = -1;
            while (number > 0)
            {
                number /= 2;
                result++;
            }
            return result;
        }

        /// <summary>
        /// Computes ceil(log2(<paramref name="number"/>)) for positive numbers, returns -1 otherwise
        /// </summary>
        /// <param name="number">some number</param>
        public static int CeilLog2(int number)
        {
            if (number <= 0)
                return -1;
            else if (IsPow2(number))
                return FloorLog2(number);
            else
                return FloorLog2(number) + 1;
        }

        /// <summary>
        /// Computes ceil(log2(<paramref name="number"/>)) for positive numbers, returns -1 otherwise
        /// </summary>
        /// <param name="number">some number</param>
        public static int CeilLog2(ulong number)
        {
            if (number == 0)
                return -1;
            else if (IsPow2(number))
                return FloorLog2(number);
            else
                return FloorLog2(number) + 1;
        }

        /// <summary>
        /// Computes ceil(log2(<paramref name="number"/>)) for positive numbers
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="number"/> &lt;= 0</exception>
        /// <param name="number">some number</param>
        public static int CeilLog2(double number)
        {
            Contract.Requires<ArgumentOutOfRangeException>(number > 0, "number");

            return (int)Math.Ceiling(Math.Log(number, 2.0));
        }

        /// <summary>
        /// Computes floor(log2(<paramref name="number"/>)) for positive numbers
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="number"/> &lt;= 0</exception>
        /// <param name="number">some number</param>
        public static int FloorLog2(double number)
        {
            Contract.Requires<ArgumentOutOfRangeException>(number > 0, "number");

            return (int)Math.Floor(Math.Log(number, 2.0));
        }

        /// <summary>
        /// Determines whether <paramref name="number"/> is a power of 2
        /// </summary>
        /// <param name="number">some number</param>
        public static bool IsPow2(long number)
        {
            return (number & (number - 1)) == 0;
        }

        /// <summary>
        /// Determines whether <paramref name="number"/> is a power of 2
        /// </summary>
        /// <param name="number">some number</param>
        public static bool IsPow2(ulong number)
        {
            return (number & (number - 1)) == 0;
        }

        /// <summary>
        /// Performs binary left rotation.
        /// </summary>
        /// <param name="value">value to rotate</param>
        /// <param name="bits">number of bits to rotate</param>
        /// <returns>rotated value</returns>
        public static int RotateLeft(this int value, int bits)
        {
            return (value << bits) | (value >> (32 - bits));
        }

        /// <summary>
        /// Performs binary right rotation.
        /// </summary>
        /// <param name="value">value to rotate</param>
        /// <param name="bits">number of bits to rotate</param>
        /// <returns>rotated value</returns>
        public static int RotateRight(this int value, int bits)
        {
            return (value >> bits) | (value << (32 - bits));
        }

        /// <summary>
        /// Performs binary left rotation.
        /// </summary>
        /// <param name="value">value to rotate</param>
        /// <param name="bits">number of bits to rotate</param>
        /// <returns>rotated value</returns>
        public static long RotateLeft(this long value, int bits)
        {
            return (value << bits) | (value >> (64 - bits));
        }

        /// <summary>
        /// Performs binary right rotation.
        /// </summary>
        /// <param name="value">value to rotate</param>
        /// <param name="bits">number of bits to rotate</param>
        /// <returns>rotated value</returns>
        public static long RotateRight(this long value, int bits)
        {
            return (value >> bits) | (value << (64 - bits));
        }

        /// <summary>
        /// Finds the largest integer N, such that N &lt;= <paramref name="number"/> and N is divisble by <paramref name="alignment"/>.
        /// </summary>
        /// <exception cref="DivideByZeroException">if <paramref name="alignment"/> == 0</exception>
        public static ulong Align(ulong number, ulong alignment)
        {
            Contract.Requires<DivideByZeroException>(alignment != 0, "alignment");

            return (number + alignment - 1) / alignment * alignment;
        }

        /// <summary>
        /// Returns the sign of <paramref name="number"/> as Signed: 
        /// Signed.FromInt(-1, 2) if <paramref name="number"/> &lt; 0, 
        /// Signed.FromInt(1, 2) if <paramref name="number"/> &gt; 0, 
        /// Signed.FromInt(0, 2) if <paramref name="number"/> == 0.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sign)]
        public static Signed SignedSign(float number)
        {
            if (number < 0.0f)
                return Signed.FromInt(-1, 2);
            else if (number > 0.0f)
                return Signed.FromInt(1, 2);
            else
                return Signed.FromInt(0, 2);
        }

        /// <summary>
        /// Returns the sign of <paramref name="number"/> as Signed: 
        /// Signed.FromInt(-1, 2) if <paramref name="number"/> &lt; 0, 
        /// Signed.FromInt(1, 2) if <paramref name="number"/> &gt; 0, 
        /// Signed.FromInt(0, 2) if <paramref name="number"/> == 0.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sign)]
        public static Signed SignedSign(double number)
        {
            if (number < 0.0)
                return Signed.FromInt(-1, 2);
            else if (number > 0.0)
                return Signed.FromInt(1, 2);
            else
                return Signed.FromInt(0, 2);
        }

        /// <summary>
        /// Returns the sign of <paramref name="number"/> as Signed: 
        /// Signed.FromInt(-1, 2) if <paramref name="number"/> &lt; 0, 
        /// Signed.FromInt(1, 2) if <paramref name="number"/> &gt; 0, 
        /// Signed.FromInt(0, 2) if <paramref name="number"/> == 0.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sign)]
        public static Signed SignedSign(Signed number)
        {
            if (number < Signed.Zero)
                return Signed.FromInt(-1, 2);
            else if (number > Signed.Zero)
                return Signed.FromInt(1, 2);
            else
                return Signed.FromInt(0, 2);
        }

        /// <summary>
        /// Returns the sign of <paramref name="number"/> as Signed: 
        /// Signed.FromInt(-1, 2) if <paramref name="number"/> &lt; 0, 
        /// Signed.FromInt(1, 2) if <paramref name="number"/> &gt; 0, 
        /// Signed.FromInt(0, 2) if <paramref name="number"/> == 0.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sign)]
        public static Signed SignedSign(SFix number)
        {
            if (number < SFix.Zero)
                return Signed.FromInt(-1, 2);
            else if (number > SFix.Zero)
                return Signed.FromInt(1, 2);
            else
                return Signed.FromInt(0, 2);
        }

#if USE_INTX
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static Unsigned Abs(Signed value)
        {
            return value.IntXValue < 0 ?
                Unsigned.FromIntX(-value.IntXValue, value.Size) :
                Unsigned.FromIntX(value.IntXValue, value.Size);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static Signed SignedAbs(Signed value)
        {
            return value.IntXValue < 0 ?
                (-value).Resize(value.Size + 1) :
                value.Resize(value.Size + 1);
        }
#else
        /// <summary>
        /// Returns the absolute value of <paramref name="value"/>. The result will have exactly the same bitwidth as the operand.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static Unsigned Abs(Signed value)
        {
            return value.BigIntValue.Sign < 0 ?
                Unsigned.FromBigInt(-value.BigIntValue, value.Size) :
                Unsigned.FromBigInt(value.BigIntValue, value.Size);
        }

        /// <summary>
        /// Returns the absolute value of <paramref name="value"/> as Signed datatype. Consequently, the most significant bit
        /// of the returned result is always 0. The result bitwidth will be 1 bit more than the operand bitwidth.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static Signed SignedAbs(Signed value)
        {
            return value.BigIntValue.Sign < 0 ?
                (-value).Resize(value.Size + 1) :
                value.Resize(value.Size + 1);
        }
#endif

        /// <summary>
        /// Returns the absolute value of <paramref name="value"/>. 
        /// The result will have exactly the same integer and fractional bitwidth as the operand.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static UFix Abs(SFix value)
        {
            return UFix.FromUnsigned(
                Abs(value.SignedValue),
                value.Format.FracWidth);
        }

        /// <summary>
        /// Returns the absolute value of <paramref name="value"/> as SFix datatype. Consequently, the most significant bit
        /// of the returned result is always 0. The result integer width is 1 bit more than the operand integer width.
        /// Fractional width stays the same.
        /// </summary>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static SFix SignedAbs(SFix value)
        {
            return SFix.FromSigned(
                SignedAbs(value.SignedValue), 
                value.Format.FracWidth);
        }

        /// <summary>
        /// Computes the square root of <paramref name="value"/>. The result format is as follows:
        /// Result integer width is ceil(<paramref name="value.Format.IntWidth"/> / 2).
        /// Result fractional width is <paramref name="value.Format.TotalWidth"/> - ceil(<paramref name="value.Format.IntWidth"/>).
        /// </summary>
        /// <remarks>
        /// This routine internally uses IEEE 754 double precision floating-point arithmetic. Therefore,
        /// it is not guaranteed that the result is precise, especially for high fractional widths (&gt; 52).
        /// </remarks>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sqrt)]
        public static UFix Sqrt(UFix value)
        {
            int iw = (value.Format.IntWidth + 1) / 2;
            return UFix.FromDouble(
                Math.Sqrt(value.DoubleValue),
                iw,
                value.Format.TotalWidth - iw);
        }

        /// <summary>
        /// Computes the sine of <paramref name="value"/>. The result integer width is always 2 bits, 
        /// result fractional width is specified by <paramref name="fracWidth"/>.
        /// </summary>
        /// <remarks>
        /// This routine internally uses IEEE 754 double precision floating-point arithmetic. Therefore,
        /// it is not guaranteed that the result is precise, especially for high fractional widths (&gt; 52).
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="fracWidth"/> &lt; 0</exception>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sin)]
        public static SFix Sin(SFix value, int fracWidth)
        {
            Contract.Requires<ArgumentOutOfRangeException>(fracWidth >= 0, "fracWidth");

            return SFix.FromDouble(
                Math.Sin(value.DoubleValue),
                2,
                fracWidth);
        }

        /// <summary>
        /// Computes the cosine of <paramref name="value"/>. The result integer width is always 2 bits, 
        /// result fractional width is specified by <paramref name="fracWidth"/>.
        /// </summary>
        /// <remarks>
        /// This routine internally uses IEEE 754 double precision floating-point arithmetic. Therefore,
        /// it is not guaranteed that the result is precise, especially for high fractional widths (&gt; 52).
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="fracWidth"/> &lt; 0</exception>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Cos)]
        public static SFix Cos(SFix value, int fracWidth)
        {
            Contract.Requires<ArgumentOutOfRangeException>(fracWidth >= 0, "fracWidth");

            return SFix.FromDouble(
                Math.Cos(value.DoubleValue),
                2,
                fracWidth);
        }

        /// <summary>
        /// Computes the sine of <paramref name="value"/>. The result integer width is always 2 bits, 
        /// result fractional width is that of <paramref name="value"/>.
        /// </summary>
        /// <remarks>
        /// This routine internally uses IEEE 754 double precision floating-point arithmetic. Therefore,
        /// it is not guaranteed that the result is precise, especially for high fractional widths (&gt; 52).
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="value.Format.FracWidth"/> &lt; 0</exception>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sin)]
        public static SFix Sin(SFix value)
        {
            Contract.Requires<ArgumentOutOfRangeException>(value.Format.FracWidth >= 0, "value.Format.FracWidth");

            return SFix.FromDouble(
                Math.Sin(value.DoubleValue),
                2,
                value.Format.FracWidth);
        }

        /// <summary>
        /// Computes the cosine of <paramref name="value"/>. The result integer width is always 2 bits, 
        /// result fractional width is that of <paramref name="value"/>.
        /// </summary>
        /// <remarks>
        /// This routine internally uses IEEE 754 double precision floating-point arithmetic. Therefore,
        /// it is not guaranteed that the result is precise, especially for high fractional widths (&gt; 52).
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="value.Format.FracWidth"/> &lt; 0</exception>
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Cos)]
        public static SFix Cos(SFix value)
        {
            Contract.Requires<ArgumentOutOfRangeException>(value.Format.FracWidth >= 0, "value.Format.FracWidth");

            return SFix.FromDouble(
                Math.Sin(value.DoubleValue),
                2,
                value.Format.FracWidth);
        }

        private class MapToScSinCos : RewriteCall, IDoNotAnalyze
        {
            public override bool Rewrite(Meta.CodeDescriptor decompilee, System.Reflection.MethodBase callee, Analysis.StackElement[] args, Analysis.IDecompiler stack, IFunctionBuilder builder)
            {
                object[] outArgs;
                object sample;
                if (!stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out sample))
                    throw new InvalidOperationException("Unable to create sample for ScSinCos call");

                var fcall = IntrinsicFunctions.XILOpCode(
                    new XILInstr(InstructionCodes.ScSinCos), 
                    TypeDescriptor.GetTypeOf(sample),
                    new Expression[] { args[0].Expr });

                stack.Push(fcall, sample);
                return true;
            }
        }

        /// <summary>
        /// Computes both sine and cosine of <paramref name="value"/>, specified in scaled radians. The conversion to radians
        /// is done by multiplying <paramref name="value"/> by PI. <paramref name="value"/> must be in the range from -1 to 1.
        /// The first element of the returned tuple is cosine, the second is sine. Integer width of sine/cosine result is
        /// 2, fractional width is specified by <paramref name="resultFracBits"/>.
        /// </summary>
        /// <remarks>
        /// This routine internally uses IEEE 754 double precision floating-point arithmetic. Therefore,
        /// it is not guaranteed that the result is precise, especially for high fractional widths (&gt; 52).
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="value"/> not between -1 and 1, or if
        /// <paramref name="resultFracBits"/> &lt; 0</exception>
        [MapToScSinCos]
        public static Tuple<SFix, SFix> ScSinCos(SFix value, int resultFracBits)
        {
            Contract.Requires<ArgumentOutOfRangeException>(value.DoubleValue >= -1.0, "value");
            Contract.Requires<ArgumentOutOfRangeException>(value.DoubleValue <= 1.0, "value");
            Contract.Requires<ArgumentOutOfRangeException>(resultFracBits >= 0, "resultFracBits");

            var result = Tuple.Create(
                SFix.FromDouble(
                    Math.Cos(Math.PI * value.DoubleValue),
                    2,
                    resultFracBits),
                SFix.FromDouble(
                    Math.Sin(Math.PI * value.DoubleValue),
                    2,
                    resultFracBits));

            return result;
        }

        /// <summary>
        /// Computes both sine and cosine of <paramref name="value"/>, specified in scaled radians. The conversion to radians
        /// is done by multiplying <paramref name="value"/> by PI. 
        /// The first element of the returned tuple is cosine, the second is sine.
        /// </summary>
        [MapToScSinCos]
        public static Tuple<double, double> ScSinCos(double value)
        {
            var result = Tuple.Create(
                    Math.Cos(Math.PI * value),
                    Math.Sin(Math.PI * value));

            return result;
        }

        private class MapToRempow2 : RewriteCall, IDoNotAnalyze
        {
            public override bool Rewrite(Meta.CodeDescriptor decompilee, System.Reflection.MethodBase callee, Analysis.StackElement[] args, Analysis.IDecompiler stack, IFunctionBuilder builder)
            {
                object[] outArgs;
                object sample;
                if (!stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out sample))
                    throw new InvalidOperationException("Unable to create sample for ScSinCos call");
                if (args[1].Variability != Analysis.Msil.EVariability.Constant)
                    throw new InvalidOperationException("n argument of Rempow2 must be a constant");

                var fcall = IntrinsicFunctions.XILOpCode(
                    new XILInstr(InstructionCodes.Rempow2, (int)args[1].Sample),
                    TypeDescriptor.GetTypeOf(sample),
                    new Expression[] { args[0].Expr });

                stack.Push(fcall, sample);
                return true;
            }
        }

        /// <summary>
        /// Computes the remainder <paramref name="value"/> mod 2^n.
        /// Result integer width is 2 + <paramref name="n"/>.
        /// Result fractional width is max(<paramref name="value.Format.FracWidth"/>, -n).
        /// </summary>
        [MapToRempow2]
        public static SFix Rempow2(SFix value, int n)
        {
            var rem = SFix.FromSigned(Signed.One, -n);
            return value % rem;
        }

        /// <summary>
        /// Computes the remainder <paramref name="value"/> mod 2^n.
        /// </summary>
        [MapToRempow2]
        public static double Rempow2(double value, int n)
        {
            return Math.IEEERemainder(value, 1 << (n+1));
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        private class MapToConditional : RewriteCall, IDoNotAnalyze
        {
            public override bool Rewrite(Meta.CodeDescriptor decompilee, System.Reflection.MethodBase callee, Analysis.StackElement[] args, Analysis.IDecompiler stack, SysDOM.IFunctionBuilder builder)
            {
 	            stack.Push(
                    Expression.Conditional(args[0].Expr, args[1].Expr, args[2].Expr),
                    args[1].Sample);
                return true;
            }
        }

        /// <summary>
        /// Returns <paramref name="trueVal"/> if <paramref name="cond"/> is true, otherwise <paramref name="falseVal"/>.
        /// </summary>
        [MapToConditional]
        public static T Select<T>(bool cond, T trueVal, T falseVal)
        {
            return cond ? trueVal : falseVal;
        }
    }
}

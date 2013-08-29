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
    public static class MathExt
    {
        public static int CeilPow2(int number)
        {
            Contract.Requires(number >= 0 && number <= int.MaxValue/2);

            int pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        public static uint CeilPow2(uint number)
        {
            Contract.Requires(number >= 0 && number <= uint.MaxValue / 2);

            uint pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        public static ulong CeilPow2(ulong number)
        {
            Contract.Requires(number >= 0 && number < ulong.MaxValue / 2 + 1);

            ulong pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        public static ulong CeilPow2(double number)
        {
            Contract.Requires(number >= 0 && number < 0.5 * double.MaxValue);

            ulong pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;

            return pow2;
        }

        public static int FloorPow2(int number)
        {
            Contract.Requires(number >= 0 && number < int.MaxValue / 2 + 1);

            int pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;
            if (pow2 > number)
                pow2 /= 2;

            return pow2;
        }

        public static ulong FloorPow2(ulong number)
        {
            Contract.Requires(number >= 0 && number < ulong.MaxValue / 2 + 1);

            ulong pow2 = 1;
            while (pow2 < number)
                pow2 *= 2;
            if (pow2 > number)
                pow2 /= 2;

            return pow2;
        }

        /// <summary>
        /// Computes floor(log2(number))
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int FloorLog2(long number)
        {
            int result = -1;
            while (number != 0)
            {
                number /= 2;
                result++;
            }
            return result;
        }

        /// <summary>
        /// Computes floor(log2(number))
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int FloorLog2(ulong number)
        {
            int result = -1;
            while (number != 0)
            {
                number /= 2;
                result++;
            }
            return result;
        }

        /// <summary>
        /// Computes ceil(log2(number))
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int CeilLog2(int number)
        {
            if (IsPow2(number))
                return FloorLog2(number);
            else
                return FloorLog2(number) + 1;
        }

        /// <summary>
        /// Computes ceil(log2(number))
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int CeilLog2(ulong number)
        {
            if (IsPow2(number))
                return FloorLog2(number);
            else
                return FloorLog2(number) + 1;
        }

        public static int CeilLog2(double number)
        {
            Contract.Requires(number >= 0);

            if (number == 0.0)
                return -1;
            return (int)Math.Ceiling(Math.Log(number, 2.0));
        }

        public static int FloorLog2(double number)
        {
            Contract.Requires(number >= 0);

            if (number == 0.0)
                return -1;
            return (int)Math.Floor(Math.Log(number, 2.0));
        }

        /// <summary>
        ///  Determines whether number is a power of 2
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static bool IsPow2(long number)
        {
            return (number & (number - 1)) == 0;
        }

        /// <summary>
        ///  Determines whether number is a power of 2
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static bool IsPow2(ulong number)
        {
            return (number & (number - 1)) == 0;
        }

        public static ulong Align(ulong number, ulong alignment)
        {
            return (number + alignment - 1) / alignment * alignment;
        }

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
        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static Unsigned Abs(Signed value)
        {
            return value.BigIntValue.Sign < 0 ?
                Unsigned.FromBigInt(-value.BigIntValue, value.Size) :
                Unsigned.FromBigInt(value.BigIntValue, value.Size);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static Signed SignedAbs(Signed value)
        {
            return value.BigIntValue.Sign < 0 ?
                (-value).Resize(value.Size + 1) :
                value.Resize(value.Size + 1);
        }
#endif

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static UFix Abs(SFix value)
        {
            return UFix.FromUnsigned(
                Abs(value.SignedValue),
                value.Format.FracWidth);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Abs)]
        public static SFix SignedAbs(SFix value)
        {
            return SFix.FromSigned(
                SignedAbs(value.SignedValue), 
                value.Format.FracWidth);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sqrt)]
        public static UFix Sqrt(UFix value)
        {
            int iw = (value.Format.IntWidth + 1) / 2;
            return UFix.FromDouble(
                Math.Sqrt(value.DoubleValue),
                iw,
                value.Format.TotalWidth - iw);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sin)]
        public static SFix Sin(SFix value, int fracWidth)
        {
            return SFix.FromDouble(
                Math.Sin(value.DoubleValue),
                2,
                fracWidth);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Cos)]
        public static SFix Cos(SFix value, int fracWidth)
        {
            return SFix.FromDouble(
                Math.Cos(value.DoubleValue),
                2,
                fracWidth);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Sin)]
        public static SFix Sin(SFix value)
        {
            return SFix.FromDouble(
                Math.Sin(value.DoubleValue),
                2,
                value.Format.FracWidth);
        }

        [MapToIntrinsicFunction(SysDOM.IntrinsicFunction.EAction.Cos)]
        public static SFix Cos(SFix value)
        {
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

        [MapToScSinCos]
        public static Tuple<SFix, SFix> ScSinCos(SFix value, int resultFracBits)
        {
            Contract.Requires<ArgumentException>(value.DoubleValue >= -1.0);
            Contract.Requires<ArgumentException>(value.DoubleValue <= 1.0);
            Contract.Requires<ArgumentException>(resultFracBits >= 1);

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

        [MapToScSinCos]
        public static Tuple<double, double> ScSinCos(double value)
        {
            //Contract.Requires<ArgumentException>(value >= -1.0);
            //Contract.Requires<ArgumentException>(value <= 1.0);

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

        [MapToRempow2]
        public static SFix Rempow2(SFix value, int n)
        {
            var rem = SFix.FromSigned(Signed.One, -n);
            return value % rem;
        }

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

        [MapToConditional]
        public static T Select<T>(bool cond, T trueVal, T falseVal)
        {
            return cond ? trueVal : falseVal;
        }
    }
}

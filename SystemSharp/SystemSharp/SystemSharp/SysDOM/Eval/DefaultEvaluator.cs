/**
 * Copyright 2011 Christian Köllner
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
using System.Linq;
using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;
using SystemSharp.Algebraic;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.SysDOM.Eval
{
    public class BreakEvaluationException : Exception
    {
    }

    public class DefaultEvaluator : IEvaluator
    {
        public static readonly DefaultEvaluator DefaultConstEvaluator = new DefaultEvaluator();

        public delegate object EvalConstantFunc(Constant constant);
        public delegate object EvalVariableFunc(Variable variable);
        public delegate object EvalFieldRefFunc(FieldRef fieldRef);
        public delegate object EvalThisRefFunc(ThisRef thisRef);
        public delegate object EvalSignalRefFunc(SignalRef signalRef);
        public delegate object EvalArrayRefFunc(ArrayRef arraylRef);
        public delegate object EvalFunctionFn(FunctionCall funcref, object[] args);

        public EvalConstantFunc DoEvalConstant;
        public EvalVariableFunc DoEvalVariable;
        public EvalFieldRefFunc DoEvalFieldRef;
        public EvalThisRefFunc DoEvalThisRef;
        public EvalSignalRefFunc DoEvalSignalRef;
        public EvalArrayRefFunc DoEvalArrayRef;
        public EvalFunctionFn DoEvalFunction;

        public DefaultEvaluator()
        {
            DoEvalConstant = DefaultEvalConstant;
            DoEvalVariable = DefaultEvalVariable;
            DoEvalFieldRef = DefaultEvalFieldRef;
            DoEvalThisRef = DefaultEvalThisRef;
            DoEvalSignalRef = DefaultEvalSignalRef;
            DoEvalArrayRef = DefaultEvalArrayRef;
            DoEvalFunction = (x, y) => { throw new BreakEvaluationException(); };
        }

        public object DefaultEvalConstant(Constant constant)
        {
            return Constant.DefaultEval(constant);
        }

        public object DefaultEvalVariable(Variable variable)
        {
            throw new BreakEvaluationException();
        }

        public object DefaultEvalSignalRef(SignalRef signalRef)
        {
            return SignalRef.DefaultEval(signalRef, this);
        }

        public object DefaultEvalFieldRef(FieldRef fieldRef)
        {
            return FieldRef.DefaultEval(fieldRef, this);
        }

        public object DefaultEvalThisRef(ThisRef thisRef)
        {
            return ThisRef.DefaultEval(thisRef);
        }

        public object DefaultEvalArrayRef(ArrayRef arrayRef)
        {
            return ArrayRef.DefaultEval(arrayRef, this);
        }

        #region IEvaluator Members

        public object EvalConstant(Constant constant)
        {
            return DoEvalConstant(constant);
        }

        public object EvalVariable(Variable variable)
        {
            return DoEvalVariable(variable);
        }

        public object EvalSignalRef(SignalRef signalRef)
        {
            return DoEvalSignalRef(signalRef);
        }

        public object EvalFieldRef(FieldRef fieldRef)
        {
            return DoEvalFieldRef(fieldRef);
        }

        public object EvalThisRef(ThisRef thisRef)
        {
            return DoEvalThisRef(thisRef);
        }

        public object EvalArrayRef(ArrayRef arrayRef)
        {
            return DoEvalArrayRef(arrayRef);
        }

        public object EvalFunction(FunctionCall funcref, object[] args, TypeDescriptor resultType)
        {
            if (funcref.Callee is IntrinsicFunction)
            {
                IntrinsicFunction ifun = (IntrinsicFunction)funcref.Callee;
                MethodBase mmodel = ifun.MethodModel;
                if (mmodel != null && mmodel.IsStatic)
                {
                    object result = mmodel.ConvertArgumentsAndInvoke(null, args);
                    return result;
                }
            }
            return DoEvalFunction(funcref, args);
        }

        private enum EResultCategory
        {
            SignedIntegral,
            UnsignedIntegral,
            FloatingPoint,
            Uncountable
        }

        private EResultCategory GetResultCategory(TypeDescriptor type)
        {
            Type ltype = type.CILType;
            if (ltype.Equals(typeof(sbyte)) || ltype.Equals(typeof(byte)) || ltype.Equals(typeof(short)) || ltype.Equals(typeof(ushort)) ||
                ltype.Equals(typeof(int)) || ltype.Equals(typeof(uint)) || ltype.Equals(typeof(long)) || ltype.Equals(typeof(bool)) ||
                ltype.Equals(typeof(char)))
                return EResultCategory.SignedIntegral;
            else if (ltype.Equals(typeof(ulong)))
                return EResultCategory.UnsignedIntegral;
            else if (ltype.Equals(typeof(float)) || ltype.Equals(typeof(double)))
                return EResultCategory.FloatingPoint;
            else
                return EResultCategory.Uncountable;
        }

        private bool TryCallIntrinsicOp(string name, out object result, params object[] args)
        {
            Type[] argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                object arg = args[i];
                argTypes[i] = arg == null ? typeof(object) : arg.GetType();
            }
            for (int i = 0; i < args.Length; i++)
            {
                MethodInfo mi = argTypes[i].GetMethod(name, argTypes);
                if (mi != null)
                {
                    result = mi.Invoke(null, args);
                    return true;
                }
            }
            result = null;
            return false;
        }

        public object Neg(object v, TypeDescriptor resultType)
        {
            object result;
            if (TryCallIntrinsicOp("op_UnaryNegation", out result, v))
                return result;

            TypeDescriptor nt = TypeDescriptor.GetTypeOf(v);
            EResultCategory rcat = GetResultCategory(nt);
            switch (rcat)
            {
                case EResultCategory.SignedIntegral:
                    {
                        long l = TypeConversions.ToLong(v);
                        return -l;
                    }

                case EResultCategory.UnsignedIntegral:
                    {
                        throw new ArgumentException();
                    }

                case EResultCategory.FloatingPoint:
                    {
                        double d = TypeConversions.ToDouble(v);
                        return -d;
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public object BoolNot(object v, TypeDescriptor resultType)
        {
            object result;
            if (TryCallIntrinsicOp("op_LogicalNot", out result, v))
                return result;

            return !(bool)v;
        }

        public object BitwiseNot(object v, TypeDescriptor resultType)
        {
            object result;
            if (TryCallIntrinsicOp("op_OnesComplement", out result, v))
                return result;

            return ~(int)v;
        }

        public object Add(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_Addition", out oresult, v1, v2))
                return oresult;

            TypeDescriptor t1 = TypeDescriptor.GetTypeOf(v1);
            TypeDescriptor t2 = TypeDescriptor.GetTypeOf(v2);
            EResultCategory rcat = GetResultCategory(t1);
            switch (rcat)
            {
                case EResultCategory.SignedIntegral:
                    {
                        long l1 = TypeConversions.ToLong(v1);
                        long l2 = TypeConversions.ToLong(v2);
                        return l1 + l2;
                    }

                case EResultCategory.UnsignedIntegral:
                    {
                        ulong u1 = TypeConversions.ToULong(v1);
                        ulong u2 = TypeConversions.ToULong(v2);
                        return u1 + u2;
                    }

                case EResultCategory.FloatingPoint:
                    {
                        double d1 = TypeConversions.ToDouble(v1);
                        double d2 = TypeConversions.ToDouble(v2);
                        return d1 + d2;
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public object Sub(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_Subtraction", out oresult, v1, v2))
                return oresult;

            TypeDescriptor t1 = TypeDescriptor.GetTypeOf(v1);
            TypeDescriptor t2 = TypeDescriptor.GetTypeOf(v2);
            EResultCategory rcat = GetResultCategory(t1);
            switch (rcat)
            {
                case EResultCategory.SignedIntegral:
                    {
                        long l1 = TypeConversions.ToLong(v1);
                        long l2 = TypeConversions.ToLong(v2);
                        return l1 - l2;
                    }

                case EResultCategory.UnsignedIntegral:
                    {
                        ulong u1 = TypeConversions.ToULong(v1);
                        ulong u2 = TypeConversions.ToULong(v2);
                        return u1 - u2;
                    }

                case EResultCategory.FloatingPoint:
                    {
                        double d1 = TypeConversions.ToDouble(v1);
                        double d2 = TypeConversions.ToDouble(v2);
                        return d1 - d2;
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public object Mul(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_Multiplication", out oresult, v1, v2))
                return oresult;

            TypeDescriptor t1 = TypeDescriptor.GetTypeOf(v1);
            TypeDescriptor t2 = TypeDescriptor.GetTypeOf(v2);
            EResultCategory rcat = GetResultCategory(t1);
            switch (rcat)
            {
                case EResultCategory.SignedIntegral:
                    {
                        long l1 = TypeConversions.ToLong(v1);
                        long l2 = TypeConversions.ToLong(v2);
                        return l1 * l2;
                    }

                case EResultCategory.UnsignedIntegral:
                    {
                        ulong u1 = TypeConversions.ToULong(v1);
                        ulong u2 = TypeConversions.ToULong(v2);
                        return u1 * u2;
                    }

                case EResultCategory.FloatingPoint:
                    {
                        double d1 = TypeConversions.ToDouble(v1);
                        double d2 = TypeConversions.ToDouble(v2);
                        return d1 * d2;
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public object Div(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_Division", out oresult, v1, v2))
                return oresult;

            TypeDescriptor t1 = TypeDescriptor.GetTypeOf(v1);
            TypeDescriptor t2 = TypeDescriptor.GetTypeOf(v2);
            EResultCategory rcat = GetResultCategory(t1);
            switch (rcat)
            {
                case EResultCategory.SignedIntegral:
                    {
                        long l1 = TypeConversions.ToLong(v1);
                        long l2 = TypeConversions.ToLong(v2);
                        return l1 / l2;
                    }

                case EResultCategory.UnsignedIntegral:
                    {
                        ulong u1 = TypeConversions.ToULong(v1);
                        ulong u2 = TypeConversions.ToULong(v2);
                        return u1 / u2;
                    }

                case EResultCategory.FloatingPoint:
                    {
                        double d1 = TypeConversions.ToDouble(v1);
                        double d2 = TypeConversions.ToDouble(v2);
                        return d1 / d2;
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public object Rem(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_Modulus", out oresult, v1, v2))
                return oresult;

            if (v1 is double && v2 is double)
                return Math.IEEERemainder((double)v1, (double)v2);
            else if (v1 is int && v2 is int)
                return (int)v1 % (int)v2;
            else
                throw new NotImplementedException();
        }

        public object And(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_BitwiseAnd", out oresult, v1, v2))
                return oresult;
            if (TryCallIntrinsicOp("op_LogicalAnd", out oresult, v1, v2))
                return oresult;

            if (v1 is int && v2 is int)
                return (int)v1 & (int)v2;
            else
                throw new NotImplementedException();
        }

        public object Or(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_BitwiseOr", out oresult, v1, v2))
                return oresult;
            if (TryCallIntrinsicOp("op_LogicalOr", out oresult, v1, v2))
                return oresult;

            if (v1 is int && v2 is int)
                return (int)v1 | (int)v2;
            else if (v1 is double && v2 is double)
                return Math.Max(Math.Abs((double)v1), Math.Abs((double)v2));
            else
                throw new NotImplementedException();
        }

        public object Xor(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_ExclusiveOr", out oresult, v1, v2))
                return oresult;

            if (v1 is int && v2 is int)
                return (int)v1 ^ (int)v2;
            else
                throw new NotImplementedException();
        }

        public object LShift(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_LeftShift", out oresult, v1, v2))
                return oresult;

            TypeDescriptor t1 = TypeDescriptor.GetTypeOf(v1);
            TypeDescriptor t2 = TypeDescriptor.GetTypeOf(v2);
            EResultCategory rcat = GetResultCategory(t1);
            switch (rcat)
            {
                case EResultCategory.SignedIntegral:
                    {
                        long v = TypeConversions.ToLong(v1);
                        int s = (int)TypeConversions.ToLong(v2);
                        return v << s;
                    }

                case EResultCategory.UnsignedIntegral:
                    {
                        ulong v = TypeConversions.ToULong(v1);
                        int s = (int)TypeConversions.ToLong(v2);
                        return v << s;
                    }

                case EResultCategory.FloatingPoint:
                    throw new ArgumentException();

                default:
                    throw new NotImplementedException();
            }
        }

        public object RShift(object v1, object v2, TypeDescriptor resultType)
        {
            object oresult;
            if (TryCallIntrinsicOp("op_RightShift", out oresult, v1, v2))
                return oresult;
            if (TryCallIntrinsicOp("op_SignedRightShift", out oresult, v1, v2))
                return oresult;
            if (TryCallIntrinsicOp("op_UnsignedRightShift", out oresult, v1, v2))
                return oresult;

            if (v1 is int && v2 is int)
                return (int)v1 >> (int)v2;
            else
                throw new NotImplementedException();
        }

        public object Exp(object v, TypeDescriptor resultType)
        {
            if (v is double)
                return Math.Exp((double)v);
            else
                throw new NotImplementedException();
        }

        public object Exp(object v1, object v2, TypeDescriptor resultType)
        {
            if (v1 is double && v2 is double)
                return Math.Pow((double)v1, (double)v2);
            else
                throw new NotImplementedException();
        }

        public object Log(object v, TypeDescriptor resultType)
        {
            if (v is double)
                return Math.Log((double)v);
            else
                throw new NotImplementedException();
        }

        public object Log(object v1, object v2, TypeDescriptor resultType)
        {
            if (v1 is double && v2 is double)
                return Math.Log((double)v1, (double)v2);
            else
                throw new NotImplementedException();
        }

        public object Abs(object v, TypeDescriptor resultType)
        {
            if (v is double)
                return Math.Abs((double)v);
            else
                throw new NotImplementedException();
        }

        public object Sin(object v, TypeDescriptor resultType)
        {
            if (v is double)
                return Math.Sin((double)v);
            else
                throw new NotImplementedException();
        }

        public object Cos(object v, TypeDescriptor resultType)
        {
            if (v is double)
                return Math.Cos((double)v);
            else
                throw new NotImplementedException();
        }

        public object E(TypeDescriptor resultType)
        {
            return Math.E;
        }

        public object PI(TypeDescriptor resultType)
        {
            return Math.PI;
        }

        public object ScalarOne(TypeDescriptor resultType)
        {
            return 1.0;
        }

        public object ScalarZero(TypeDescriptor resultType)
        {
            return 0.0;
        }

        public object True(TypeDescriptor resultType)
        {
            return true;
        }

        public object False(TypeDescriptor resultType)
        {
            return false;
        }

        public object Time(TypeDescriptor resultType)
        {
            throw new NotImplementedException();
        }

        public object Concat(object v1, object v2, TypeDescriptor resultType)
        {
            /*
            FixPointValue fpv1 = (FixPointValue)v1;
            FixPointValue fpv2 = (FixPointValue)v2;

            string bin1 = fpv1.UnscaledValue.ToString(2);
            string bin2 = fpv2.UnscaledValue.ToString(2);
            BigInteger bir = new BigInteger(bin1 + bin2, 2);
            return new FixPointValue(bir, fpv1.IsSigned, fpv1.Width + fpv2.Width, fpv2.FracBits);
             * */
            throw new NotImplementedException();
        }

        public object ExtendSign(object v, TypeDescriptor resultType)
        {
            return v;
        }

        public object Slice(object v, object lo, object hi, TypeDescriptor resultType)
        {
            /*if (v is FixPointValue)
            {
                FixPointValue fpv = (FixPointValue)v;
                BigInteger bi = fpv.UnscaledValue;
                string bin = bi.ToString(2);
                int ilo = (int)lo;
                int ihi = (int)hi;
                string slice = bin.Substring(ilo, ihi - ilo + 1);
                BigInteger bir = new BigInteger(slice, 2);
                return new FixPointValue(bir, false, ihi - ilo + 1, 0);
            }
            else*/ if (v is long)
            {
                long lv = (long)v;
                int ilo = (int)TypeConversions.ToLong(lo);
                int ihi = (int)TypeConversions.ToLong(hi);
                lv >>= ilo;
                //long mask = (1L << (ihi - ilo + 1)) - 1;
                //lv &= mask;
                return lv;
            }
            else
                throw new NotImplementedException();
        }

        public object IsLessThan(object v1, object v2, TypeDescriptor resultType)
        {
            try
            {
                return (dynamic)v1 < (dynamic)v2;
            }
            catch (RuntimeBinderException)
            {
                return null;
            }
        }

        public object IsLessThanOrEqual(object v1, object v2, TypeDescriptor resultType)
        {
            try
            {
                return (dynamic)v1 <= (dynamic)v2;
            }
            catch (RuntimeBinderException)
            {
                return null;
            }
        }

        public object IsEqual(object v1, object v2, TypeDescriptor resultType)
        {
            try
            {
                return (dynamic)v1 == (dynamic)v2;
            }
            catch (RuntimeBinderException)
            {
                return null;
            }
        }

        public object IsNotEqual(object v1, object v2, TypeDescriptor resultType)
        {
            try
            {
                return (dynamic)v1 == (dynamic)v2;
            }
            catch (RuntimeBinderException)
            {
                return null;
            }
        }

        public object IsGreaterThanOrEqual(object v1, object v2, TypeDescriptor resultType)
        {
            try
            {
                return (dynamic)v1 >= (dynamic)v2;
            }
            catch (RuntimeBinderException)
            {
                return null;
            }
        }

        public object IsGreaterThan(object v1, object v2, TypeDescriptor resultType)
        {
            try
            {
                return (dynamic)v1 >= (dynamic)v2;
            }
            catch (RuntimeBinderException)
            {
                return null;
            }
        }

        public object ConditionallyCombine(object cond, object thn, object els, TypeDescriptor resultType)
        {
            var truth = cond as bool?;
            if (truth != null)
                return (bool)truth ? thn : els;
            else
                throw new NotSupportedException("Ambiguous condition");
        }

        #endregion


        public object EvalLiteral(ILiteral lit)
        {
            throw new NotImplementedException();
        }
    }


}
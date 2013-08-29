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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
#if USE_INTX
using IntXLib;
#else
using System.Numerics;
#endif
using SystemSharp.Analysis;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;
using System.Threading.Tasks;

namespace SystemSharp.DataTypes
{
    public enum ESizedProperties
    {
        Size
    }

    /// <summary>
    /// This interface is used for data types which have an associated size.
    /// </summary>
    public interface ISized
    {
        int Size { get; }
    }

    public interface ISizeOf
    {
        int SizeOfThis { get; }
    }

    /// <summary>
    /// This interface represents an indexable data type.
    /// </summary>
    /// <typeparam name="TA">The indexable data type</typeparam>
    /// <typeparam name="TE">The data type of a single indexed element</typeparam>
    public interface IIndexable<TA, TE> :
        ISized
    {
        /// <summary>
        /// Performs an index operation on the data type.
        /// </summary>
        /// <param name="i">The index of the element to be retrieved</param>
        /// <returns>The element at index i</returns>
        TE this[int i]
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.Index)]
            [SideEffectFree]
            get;
        }

        /// <summary>
        /// Performs a slice operation on the data type.
        /// </summary>
        /// <param name="i0">The first index of the slice to be retrieved</param>
        /// <param name="i1">The second index of the slice to be retrieved</param>
        /// <returns>The slice defined by the closed interval [i0..i1]</returns>
        TA this[int i0, int i1]
        {
            [MapToSlice]
            [SideEffectFree]
            get;
        }
    }

    /// <summary>
    /// This interface represents a data type which supports concatenation.
    /// </summary>
    /// <typeparam name="TA">The concatenable data type</typeparam>
    /// <typeparam name="TE">The data type of a single, indexed element of that data type</typeparam>
    public interface IConcatenable<TA, TE>
    {
        /// <summary>
        /// Concatenates an instance of this data type with another one.
        /// </summary>
        /// <param name="other">The other instance which should be appended</param>
        /// <returns>The concatenation of both</returns>
        [MapToBinOp(BinOp.Kind.Concat)]
        [SideEffectFree]
        TA Concat(TA other);

        /// <summary>
        /// Concatenates an instance of this data type with a single element.
        /// </summary>
        /// <param name="other">The element to append</param>
        /// <returns>The concatenation of both</returns>
        [MapToBinOp(BinOp.Kind.Concat)]
        [SideEffectFree]
        TA Concat(TE other);
    }

    public static class ConcatExtensions
    {
        class ConcatRewriter : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, System.Reflection.MethodBase callee, Analysis.StackElement[] args, Analysis.IDecompiler stack, IFunctionBuilder builder)
            {
                Array arr = (Array)args[0].Sample;
                if (arr == null)
                    throw new InvalidOperationException("Unable to deduce array length");

                int numElems = arr.Length;
                Type tTE = arr.GetType().GetElementType();
                Type tTA;
                callee.IsFunction(out tTA);
                FunctionCall newCall = IntrinsicFunctions.NewArray(tTE, LiteralReference.CreateConstant(numElems), arr);
                FunctionSpec fspec = (FunctionSpec)newCall.Callee;
                IntrinsicFunction ifun = fspec.IntrinsicRep;
                ArrayParams aparams = (ArrayParams)ifun.Parameter;
                for (int i = 0; i < numElems; i++)
                {
                    aparams.Elements[i] = IntrinsicFunctions.GetArrayElement(
                        args[0].Expr,
                        LiteralReference.CreateConstant(i));
                }

                object sample = null;
                try
                {
                    sample = callee.Invoke(arr);
                }
                catch (Exception)
                {
                }

                Expression conv = IntrinsicFunctions.Cast(newCall, arr.GetType(), tTA);
                stack.Push(conv, sample);
                return true;
            }
        }

        [ConcatRewriter]
        public static TA Concat<TA, TE>(this TE[] arr) where TA : IConcatenable<TA, TE>
        {
            TA cur = default(TA);
            for (long i = 0; i < arr.LongLength; i++)
            {
                cur = cur.Concat(arr[i]);
            }
            return cur;
        }
    }

    class StdLogicSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            char c = (StdLogic)value;
            return (StdLogicVector)("" + c);
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return slv[0];
        }
    }

    /// <summary>
    /// This data type represents a single bit of four-valued logic, similiar to IEEE 1164.
    /// </summary>
    [MapToIntrinsicType(EIntrinsicTypes.StdLogic)]
    [SLVSerializable(typeof(StdLogic), typeof(StdLogicSLV))]
    public struct StdLogic :
        IResolvable<StdLogic>,
        ISizeOf
    {
        /// <summary>
        /// The logic values which can be taken.
        /// </summary>
        public enum EValues
        {
            /// <summary>
            /// Uninitialized / 'U'
            /// </summary>
            Uninitialized,

            /// <summary>
            /// Unknown / 'X'
            /// </summary>
            Unknown,

            /// <summary>
            /// Logic '0'
            /// </summary>
            Logic0,

            /// <summary>
            /// Logic '1'
            /// </summary>
            Logic1,

            /// <summary>
            /// High impedance / 'Z'
            /// </summary>
            HighZ,

            /// <summary>
            /// Weak / 'W'
            /// </summary>
            Weak,

            /// <summary>
            /// Weak 0 / 'L'
            /// </summary>
            Weak0,

            /// <summary>
            /// Weak 1 / 'H'
            /// </summary>
            Weak1,

            /// <summary>
            /// Don't care / '-'
            /// </summary>
            DontCare
        }

        public static readonly char[] Literals = { 'U', 'X', '0', '1', 'Z', 'W', 'L', 'H', '-' };

        private static readonly string[] ResolutionMapStr =
        {
            "UUUUUUUUU",
            "UXXXXXXXX",
            "UX0X0000X",
            "UXX11111X",
            "UX01ZWLHX",
            "UX01WWWWX",
            "UX01LWLWX",
            "UX01HWWHX",
            "UXXXXXXXX"
        };

        private static StdLogic[,] ResolutionMap;

        static StdLogic()
        {
            ResolutionMap = new StdLogic[Literals.Length, Literals.Length];
            for (int i = 0; i < Literals.Length; i++)
            {
                for (int j = 0; j < Literals.Length; j++)
                    ResolutionMap[(int)ToEnum(Literals[i]), (int)ToEnum(Literals[j])] = ToEnum(ResolutionMapStr[i][j]);
            }
        }

        [ModelElement]
        public static readonly StdLogic U = new StdLogic(EValues.Uninitialized);

        [ModelElement]
        public static readonly StdLogic X = new StdLogic(EValues.Unknown);

        [ModelElement]
        public static readonly StdLogic _0 = new StdLogic(EValues.Logic0);

        [ModelElement]
        public static readonly StdLogic _1 = new StdLogic(EValues.Logic1);

        [ModelElement]
        public static readonly StdLogic Z = new StdLogic(EValues.HighZ);

        [ModelElement]
        public static readonly StdLogic W = new StdLogic(EValues.Weak);

        [ModelElement]
        public static readonly StdLogic L = new StdLogic(EValues.Weak0);

        [ModelElement]
        public static readonly StdLogic H = new StdLogic(EValues.Weak1);

        [ModelElement]
        public static readonly StdLogic DC = new StdLogic(EValues.DontCare);

        private EValues _value;

        [TypeConversion(typeof(StdLogic), typeof(char))]
        [SideEffectFree]
        public static char ToChar(EValues value)
        {
            return Literals[(int)value];
        }

        [TypeConversion(typeof(StdLogic), typeof(EValues))]
        [SideEffectFree]
        public static EValues ToEnum(char c)
        {
            for (int i = 0; i < Literals.Length; i++)
                if (Literals[i] == c)
                    return (EValues)i;

            throw new ArgumentException("Illegal literal");
        }

        private StdLogic(EValues value)
        {
            _value = value;
        }

        [TypeConversion(typeof(StdLogic), typeof(string))]
        [SideEffectFree]
        public override string ToString()
        {
            return "" + ToChar(_value);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is StdLogic)
            {
                StdLogic sl = (StdLogic)obj;
                return _value == sl._value;
            }
            else
                return false;
        }

        [StaticEvaluation]
        public static implicit operator StdLogic(EValues value)
        {
            return new StdLogic(value);
        }

        [StaticEvaluation]
        public static implicit operator EValues(StdLogic sl)
        {
            return sl._value;
        }

        /// <summary>
        /// Converts a character implicitly to StdLogic.
        /// </summary>
        /// <param name="value">One of 'U', 'X', '0', '1', 'Z', 'W', 'L', 'H', '-'</param>
        /// <returns>The StdLogic representation</returns>
        [StaticEvaluation]
        public static implicit operator StdLogic(char value)
        {
            return ToEnum(value);
        }

        [StaticEvaluation]
        public static implicit operator char(StdLogic sl)
        {
            return ToChar(sl);
        }

        /// <summary>
        /// Converts a boolean value implicitly to StdLogic
        /// </summary>
        /// <param name="b">The boolean value</param>
        /// <returns>The StdLogic representation ('0' for false, '1' for true)</returns>
        [TypeConversion(typeof(bool), typeof(StdLogic))]
        [SideEffectFree]
        public static implicit operator StdLogic(bool b)
        {
            return b ? _1 : _0;
        }

        //[MapToIntrinsicFunction(typeof(StdLogic), typeof(bool))]
        [AutoConversion(AutoConversion.EAction.Exclude)]
        [SideEffectFree]
        public static implicit operator bool(StdLogic sl)
        {
            //if (!sl.Is0 && !sl.Is1)
            //    throw new InvalidOperationException("Unambigous conversion of StdLogic to bool not possible");

            return sl == '1';
        }

        public StdLogic Resolve(StdLogic x, StdLogic y)
        {
            return ResolutionMap[(int)(EValues)x, (int)(EValues)y];
        }

        /// <summary>
        /// Returns true if this instance represents either '0' or 'L'
        /// </summary>
        public bool Is0
        {
            get
            {
                return _value == EValues.Logic0 || _value == EValues.Weak0;
            }
        }

        /// <summary>
        /// Returns true if this instance represents either '1' or 'H'
        /// </summary>
        public bool Is1
        {
            get
            {
                return _value == EValues.Logic1 || _value == EValues.Weak1;
            }
        }

        /// <summary>
        /// Converts this StdLogic value to "weak" logic.
        /// </summary>
        /// <remarks>
        /// '0' is converted to 'L',
        /// '1' converted to 'H',
        /// all other values remain the same.
        /// </remarks>
        public StdLogic Weak
        {
            [SideEffectFree]
            get
            {
                switch (_value)
                {
                    case EValues.Logic0: return L;
                    case EValues.Logic1: return H;
                    default: return this;
                }
            }
        }

        public int SizeOfThis
        {
            [SideEffectFree]
            get
            {
                return 1;
            }
        }

        [SLVConcatRewriter]
        [SideEffectFree]
        public StdLogicVector Concat(StdLogic sl)
        {
            return StdLogicVector.FromStdLogic(sl, this);
        }

        [SLVConcatRewriter]
        [SideEffectFree]
        public StdLogicVector Concat(StdLogicVector slv)
        {
            return StdLogicVector.FromStdLogic(this).Concat(slv);
        }

        /// <summary>
        /// Computes the logic complement (NOT) of an StdLogic instance.
        /// </summary>
        /// <param name="sl">The StdLogic value to be complemented</param>
        /// <returns></returns>
        public static StdLogic operator !(StdLogic sl)
        {
            switch ((EValues)sl)
            {
                case EValues.Uninitialized: return X;
                case EValues.Unknown: return X;
                case EValues.Logic0: return _1;
                case EValues.Logic1: return _0;
                case EValues.Weak: return X;
                case EValues.Weak0: return _1;
                case EValues.Weak1: return _0;
                case EValues.DontCare: return X;
                default: throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Computes the logic conjunction (AND) of two StdLogic values.
        /// </summary>
        /// <param name="sa">The first value</param>
        /// <param name="sb">The second value</param>
        /// <returns>The logic conjunction</returns>
        public static StdLogic operator &(StdLogic sa, StdLogic sb)
        {
            bool _0a = sa.Is0;
            bool _1a = sa.Is1;
            bool _0b = sb.Is0;
            bool _1b = sb.Is1;
            if (!(_0a || _1a) || !(_0b || _1b))
                return X;
            return (_1a && _1b) ? _1 : _0;
        }

        /// <summary>
        /// Computes the logic disjunction (OR) of two StdLogic values.
        /// </summary>
        /// <param name="sa">The first value</param>
        /// <param name="sb">The second value</param>
        /// <returns>The logic disjunction</returns>
        public static StdLogic operator |(StdLogic sa, StdLogic sb)
        {
            bool _0a = sa.Is0;
            bool _1a = sa.Is1;
            bool _0b = sb.Is0;
            bool _1b = sb.Is1;
            if (!(_0a || _1a) || !(_0b || _1b))
                return X;
            return (_1a || _1b) ? _1 : _0;
        }

        /// <summary>
        /// Computes the logic anti-valence (XOR) of two StdLogic values.
        /// </summary>
        /// <param name="sa">The first value</param>
        /// <param name="sb">The second value</param>
        /// <returns>The logic anti-valence</returns>
        public static StdLogic operator ^(StdLogic sa, StdLogic sb)
        {
            bool _0a = sa.Is0;
            bool _1a = sa.Is1;
            bool _0b = sb.Is0;
            bool _1b = sb.Is1;
            if (!(_0a || _1a) || !(_0b || _1b))
                return X;
            return (_1a ^ _1b) ? _1 : _0;
        }

        /// <summary>
        /// Computes the logic equivalence of two StdLogic values.
        /// </summary>
        /// <param name="sa">The first value</param>
        /// <param name="sb">The second value</param>
        /// <returns>The logic equivalence</returns>
        public static StdLogic Eq(StdLogic sa, StdLogic sb)
        {
            bool _0a = sa.Is0;
            bool _1a = sa.Is1;
            bool _0b = sb.Is0;
            bool _1b = sb.Is1;
            if (!(_0a || _1a) || !(_0b || _1b))
                return X;
            return (_1a == _1b) ? _1 : _0;
        }

        /// <summary>
        /// Returns true if two StdLogic values are equal.
        /// </summary>
        /// <param name="sa">The first value</param>
        /// <param name="sb">The second value</param>
        /// <returns>True if both values are equal</returns>
        public static bool operator ==(StdLogic sa, StdLogic sb)
        {
            return sa.Equals(sb);
        }

        /// <summary>
        /// Returns true if two StdLogic values are unequal.
        /// </summary>
        /// <param name="sa">The first value</param>
        /// <param name="sb">The second value</param>
        /// <returns>True if the values are not equal</returns>
        public static bool operator !=(StdLogic sa, StdLogic sb)
        {
            return !sa.Equals(sb);
        }
    }

    class SLVSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            return (StdLogicVector)value;
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return slv;
        }
    }

    class BoolSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            return (StdLogicVector)((bool)value ? "1" : "0");
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return (bool)slv[0];
        }
    }

    class IntSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            return StdLogicVector.FromLong((int)value, 32);
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return (int)slv.LongValue;
        }
    }

    class LongSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            return StdLogicVector.FromLong((long)value, 64);
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return slv.LongValue;
        }
    }

    class CharSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            return StdLogicVector.FromLong((char)value, 16);
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return (char)slv.LongValue;
        }
    }

    class StringSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            string s = (string)value;
            StdLogicVector cur = StdLogicVector.Empty;
            foreach (char c in s)
            {
                StdLogicVector cslv = StdLogicVector.FromLong(c, 16);
                cur = cslv.Concat(cur);
            }
            return cur;
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            string s = "";
            long numChars = (slv.Size + 15) / 16;
            for (int i = 0; i < numChars; i++)
            {
                char c = (char)slv[16 * i + 15, 16 * i].LongValue;
                s = c + s;
            }
            return s;
        }
    }

    class FloatSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            return IEEE754Support.ToSLV((double)(float)value, FloatFormat.SingleFormat);
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return IEEE754Support.ToFloat(slv, FloatFormat.SingleFormat);
        }
    }

    class DoubleSLV : ISerializer
    {
        public StdLogicVector Serialize(object value)
        {
            return IEEE754Support.ToSLV((double)value, FloatFormat.DoubleFormat);
        }

        public object Deserialize(StdLogicVector slv, TypeDescriptor targetType)
        {
            return IEEE754Support.ToFloat(slv, FloatFormat.DoubleFormat);
        }
    }

    /// <summary>
    /// Represents a vector of four-valued logic, similiar to IEEE 1164.
    /// </summary>
    [MapToIntrinsicType(EIntrinsicTypes.StdLogicVector)]
    [SLVSerializable(typeof(StdLogicVector), typeof(SLVSLV))]
    [SLVSerializable(typeof(bool), typeof(BoolSLV))]
    [SLVSerializable(typeof(int), typeof(IntSLV))]
    [SLVSerializable(typeof(long), typeof(LongSLV))]
    [SLVSerializable(typeof(char), typeof(CharSLV))]
    [SLVSerializable(typeof(string), typeof(StringSLV))]
    [SLVSerializable(typeof(float), typeof(FloatSLV))]
    [SLVSerializable(typeof(double), typeof(DoubleSLV))]
    public struct StdLogicVector :
        IResolvable<StdLogicVector>,
        IIndexable<StdLogicVector, StdLogic>,
        IConcatenable<StdLogicVector, StdLogic>,
        ISizeOf
    {
        /// <summary>
        /// A zero-sized logic vector
        /// </summary>
        public static StdLogicVector Empty = new StdLogicVector();

        private StdLogic[] _value;

        public static StdLogicVector AllSame(StdLogic v, long count)
        {
            StdLogic[] result = new StdLogic[count];
            for (long i = 0; i < count; i++)
                result[i] = v;
            return result;
        }

        /// <summary>
        /// Returns a logic vector which consists of logic '0's of a specified length
        /// </summary>
        /// <param name="count">The desired length</param>
        /// <returns>The vector consisting of '0's</returns>
        [StaticEvaluation]
        public static StdLogicVector _0s(long count)
        {
            return AllSame(StdLogic._0, count);
        }

        /// <summary>
        /// Returns a logic vector which consists of logic '1's of a specified length
        /// </summary>
        /// <param name="count">The desired length</param>
        /// <returns>The vector consisting of '1's</returns>
        [StaticEvaluation]
        public static StdLogicVector _1s(long count)
        {
            return AllSame(StdLogic._1, count);
        }

        /// <summary>
        /// Returns a logic vector which consists of logic 'Z's of a specified length
        /// </summary>
        /// <param name="count">The desired length</param>
        /// <returns>The vector consisting of 'Z's</returns>
        [StaticEvaluation]
        public static StdLogicVector Zs(long count)
        {
            return AllSame(StdLogic.Z, count);
        }

        /// <summary>
        /// Returns a logic vector which consists of logic 'X's of a specified length
        /// </summary>
        /// <param name="count">The desired length</param>
        /// <returns>The vector consisting of 'X's</returns>
        [StaticEvaluation]
        public static StdLogicVector Xs(long count)
        {
            return AllSame(StdLogic.X, count);
        }

        /// <summary>
        /// Returns a logic vector which consists of logic 'U's of a specified length
        /// </summary>
        /// <param name="count">The desired length</param>
        /// <returns>The vector consisting of 'U's</returns>
        [StaticEvaluation]
        public static StdLogicVector Us(long count)
        {
            return AllSame(StdLogic.U, count);
        }

        /// <summary>
        /// Returns a logic vector which consists of logic '-'s of a specified length
        /// </summary>
        /// <param name="count">The desired length</param>
        /// <returns>The vector consisting of '-'s</returns>
        [StaticEvaluation]
        public static StdLogicVector DCs(long count)
        {
            return AllSame(StdLogic.DC, count);
        }

        public static StdLogicVector OneHot(int count, int pos)
        {
            StdLogicVector result = StdLogicVector._0s(count);
            result[pos] = '1';
            return result;
        }

        public static StdLogicVector OneCold(int count, int pos)
        {
            StdLogicVector result = StdLogicVector._1s(count);
            result[pos] = '0';
            return result;
        }

        private StdLogicVector(StdLogic[] content)
        {
            _value = content;
        }

        private StdLogicVector(int size)
        {
            _value = new StdLogic[size];
        }

        public static StdLogicVector FromStdLogic(params StdLogic[] bits)
        {
            return new StdLogicVector(bits);
        }

        [TypeConversion(typeof(StdLogicVector), typeof(string))]
        [SideEffectFree]
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (_value != null)
            {
                for (int i = _value.Length - 1; i >= 0; i--)
                {
                    sb.Append((char)_value[i]);
                }
            }
            return sb.ToString();
        }

        public override int GetHashCode()
        {
            int hash = 0;
            if (_value != null)
            {
                foreach (StdLogic v in _value)
                {
                    hash ^= v.GetHashCode();
                    hash *= 11;
                }
            }
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj is StdLogicVector)
            {
                StdLogicVector slv = (StdLogicVector)obj;
                if (_value == null && slv._value == null)
                    return true;
                if (_value == null || slv._value == null)
                    return false;
                if (_value.LongLength != slv._value.LongLength)
                    return false;
                int size = _value.Length;
                for (long i = 0; i < size; i++)
                {
                    if (_value[i] != slv._value[i])
                        return false;
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Returns the length (in bits) of this StdLogicVector.
        /// </summary>
        [TypeParameter(typeof(IntToZeroBasedDownRangeConverter))]
        public int Size
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.PropertyRef, ESizedProperties.Size)]
            [SideEffectFree]
            get
            {
                return _value == null ? 0 : _value.Length;
            }
        }

        private class IndexWriter : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                ArrayRef aref = new ArrayRef(args[0].Expr, args[1].Expr.ResultType, args[1].Expr);
                builder.Store(aref, args[2].Expr);
                return true;
            }
        }

        /// <summary>
        /// Provides access to a single bit within the StdLogicVector.
        /// </summary>
        /// <param name="index">The index of the desired bit</param>
        /// <returns>The indexed bit</returns>
        public StdLogic this[int index]
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.Index)]
            [SideEffectFree]
            get
            {
                if (index < 0 || index >= Size)
                    throw new ArgumentException("Given index is out of range");

                return _value[index];
            }
            [IndexWriter]
            set
            {
                if (index < 0 || index >= Size)
                    throw new ArgumentException("Given index is out of range");

                _value[index] = value;
            }
        }

        private class NonIntIndexer : RewriteCall
        {
            public Type SrcType { get; private set; }

            public NonIntIndexer(Type srcType)
            {
                SrcType = srcType;
            }

            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var convidx = IntrinsicFunctions.Cast(args[1].Expr, SrcType, typeof(int));
                var result = IntrinsicFunctions.Index(args[0].Expr, convidx, typeof(StdLogic));
                object[] outArgs;
                object rsample;
                stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out rsample);
                stack.Push(result, rsample);
                return true;
            }
        }

        private class SliceWriter : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                Expression slice = IntrinsicFunctions.MkDownRange(args[1].Expr, args[2].Expr);
                ArrayRef aref = new ArrayRef(args[0].Expr, args[2].Expr.ResultType, slice);
                builder.Store(aref, args[2].Expr);
                return true;
            }
        }

        /// <summary>
        /// Provides access to a single bit within the StdLogicVector.
        /// </summary>
        /// <param name="index">The index of the desired bit</param>
        /// <returns>The indexed bit</returns>
        public StdLogic this[Unsigned index]
        {
            [NonIntIndexer(typeof(Unsigned))]
            [SideEffectFree]
            get
            {
                ulong uindex = index.ULongValue;
                if (uindex >= (ulong)Size)
                    throw new ArgumentException("Given index is out of range");

                return _value[uindex];
            }
            [SliceWriter]
            set
            {
                ulong uindex = index.ULongValue;
                if (uindex >= (ulong)Size)
                    throw new ArgumentException("Given index is out of range");

                _value[uindex] = value;
            }
        }

        /// <summary>
        /// Provides access to a single bit within the StdLogicVector.
        /// </summary>
        /// <param name="index">The index of the desired bit</param>
        /// <returns>The indexed bit</returns>
        public StdLogic this[Signed index]
        {
            [NonIntIndexer(typeof(Signed))]
            [SideEffectFree]
            get
            {
                long iindex = index.LongValue;
                if (iindex < 0 || iindex >= Size)
                    throw new ArgumentException("Given index is out of range");

                return _value[iindex];
            }
            [SliceWriter]
            set
            {
                long iindex = index.LongValue;
                if (iindex >= Size)
                    throw new ArgumentException("Given index is out of range");

                _value[iindex] = value;
            }
        }

        /// <summary>
        /// Provides access to a slice of bits within the StdLogicVector.
        /// </summary>
        /// <param name="i0">The first index of the desired slice.</param>
        /// <param name="i1">The second index of the desired slice.</param>
        /// <returns>The indexed slice</returns>
        public StdLogicVector this[int i0, int i1]
        {
            [MapToSlice]
            [SideEffectFree]
            get
            {
                int size = Size;
                int subsize = i0 - i1 + 1;
                if (i0 >= size || i1 < 0 || subsize < 0)
                    throw new ArgumentException("Specified indices are out of range");
                StdLogic[] result = new StdLogic[subsize];
                if (subsize > 0)
                    Array.Copy(_value, i1, result, 0, subsize);
                return result;
            }
            [SliceWriter]
            set
            {
                int size = Size;
                int subsize = i0 - i1 + 1;
                if (i0 >= size || i1 < 0 || subsize < 0)
                    throw new ArgumentException("Specified indices are out of range");
                if (subsize != value.Size)
                    throw new ArgumentException("Vector dimensions do not match");

                if (subsize > 0)
                    Array.Copy(value._value, 0, _value, i1, subsize);
            }
        }

        /// <summary>
        /// Provides access to a slice of bits within the StdLogicVector.
        /// </summary>
        /// <param name="i0">The first index of the desired slice.</param>
        /// <param name="i1">The second index of the desired slice.</param>
        /// <returns>The indexed slice</returns>
        public StdLogicVector this[Signed i0, Signed i1]
        {
            [MapToSlice]
            [SideEffectFree]
            get
            {
                int size = Size;
                int ii0 = (int)i0.LongValue;
                int ii1 = (int)i1.LongValue;
                int subsize = ii0 - ii1 + 1;
                if (ii0 >= size || ii1 < 0 || subsize < 0)
                    throw new ArgumentException("Specified indices are out of range");
                StdLogic[] result = new StdLogic[subsize];
                if (subsize > 0)
                    Array.Copy(_value, ii1, result, 0, subsize);
                return result;
            }
            [SliceWriter]
            set
            {
                int size = Size;
                int ii0 = (int)i0.LongValue;
                int ii1 = (int)i1.LongValue;
                int subsize = ii0 - ii1 + 1;
                if (ii0 >= size || ii1 < 0 || subsize < 0)
                    throw new ArgumentException("Specified indices are out of range");
                if (subsize != value.Size)
                    throw new ArgumentException("Vector dimensions do not match");

                if (subsize > 0)
                    Array.Copy(value._value, 0, _value, ii1, subsize);
            }
        }

        /// <summary>
        /// Provides access to a slice of bits within the StdLogicVector.
        /// </summary>
        /// <param name="i0">The first index of the desired slice.</param>
        /// <param name="i1">The second index of the desired slice.</param>
        /// <returns>The indexed slice</returns>
        public StdLogicVector this[Unsigned i0, Unsigned i1]
        {
            [MapToSlice]
            [SideEffectFree]
            get
            {
                int size = Size;
                int ii0 = (int)i0.ULongValue;
                int ii1 = (int)i1.ULongValue;
                int subsize = ii0 - ii1 + 1;
                if (ii0 >= size || ii1 < 0 || subsize < 0)
                    throw new ArgumentException("Specified indices are out of range");
                StdLogic[] result = new StdLogic[subsize];
                if (subsize > 0)
                    Array.Copy(_value, ii1, result, 0, subsize);
                return result;
            }
            [SliceWriter]
            set
            {
                int size = Size;
                int ii0 = (int)i0.ULongValue;
                int ii1 = (int)i1.ULongValue;
                int subsize = ii0 - ii1 + 1;
                if (ii0 >= size || ii1 < 0 || subsize < 0)
                    throw new ArgumentException("Specified indices are out of range");
                if (subsize != value.Size)
                    throw new ArgumentException("Vector dimensions do not match");

                if (subsize > 0)
                    Array.Copy(value._value, 0, _value, ii1, subsize);
            }
        }

        /// <summary>
        /// Appends an StdLogicVector to this StdLogicVector.
        /// </summary>
        /// <param name="sv">The StdLogicVector to be appended</param>
        /// <returns>The concatenation of both</returns>
        [SLVConcatRewriter]
        [SideEffectFree]
        public StdLogicVector Concat(StdLogicVector sv)
        {
            StdLogic[] result = new StdLogic[Size + sv.Size];
            if (sv.Size > 0)
                Array.Copy(sv._value, 0, result, 0, sv.Size);
            if (Size > 0)
                Array.Copy(_value, 0, result, sv.Size, Size);
            return result;
        }

        /// <summary>
        /// Appends an StdLogic to this StdLogicVector.
        /// </summary>
        /// <param name="sl">The StdLogic to be appended</param>
        /// <returns>The concatenation of both</returns>
        [SLVConcatRewriter]
        [SideEffectFree]
        public StdLogicVector Concat(StdLogic sl)
        {
            StdLogic[] result = new StdLogic[Size + 1];
            result[0] = sl;
            if (_value != null)
                Array.Copy(_value, 0, result, 1, Size);
            return result;
        }

        public int SizeOfThis
        {
            [SideEffectFree]
            get { return Size; }
        }

        /// <summary>
        /// Implicitly converts a string representation to a logic vector.
        /// </summary>
        /// <param name="value">The string literal to be converted</param>
        /// <returns>Its StdLogicVector representation</returns>
        [TypeConversion(typeof(string), typeof(StdLogicVector))]
        [SideEffectFree]
        public static implicit operator StdLogicVector(string value)
        {
            if (value == null)
                throw new ArgumentException("value must not be null");

            if (value.Length == 0)
                return StdLogicVector.Empty;

            StdLogic[] result;
            if (value[0] == 'x')
            {
                result = new StdLogic[4 * (value.Length - 1)];
                int k = 0;
                for (int i = 1; i < value.Length; i++)
                {
                    int digit;
                    char v = value[value.Length - i - 1];
                    if (v >= '0' && v <= '9')
                        digit = (v - '0');
                    else if (v >= 'A' && v <= 'F')
                        digit = (v - 'A' + 10);
                    else if (v >= 'a' && v <= 'f')
                        digit = (v - 'a' + 10);
                    else
                        throw new ArgumentException("Illegal hex number");
                    for (int j = 0; j < 4; j++)
                    {
                        if ((digit & 1) == 1)
                            result[k] = StdLogic._1;
                        else
                            result[k] = StdLogic._0;
                        digit >>= 1;
                        ++k;
                    }
                }
            }
            else
            {
                // Try to convert each character, ToEnum will throw an argument
                // exception if there is an illegal one.
                result = new StdLogic[value.Length];
                for (int i = 0; i < value.Length; i++)
                {
                    result[i] = value[value.Length - i - 1];
                }
            }
            return result;
        }

        [TypeConversion(typeof(StdLogic[]), typeof(StdLogicVector))]
        [SideEffectFree]
        public static implicit operator StdLogicVector(StdLogic[] vs)
        {
            return new StdLogicVector(vs);
        }

        /// <summary>
        /// Computes the bitwise complement of an StdLogicVector
        /// </summary>
        /// <param name="a">The vector to complement</param>
        /// <returns>The bitwise complement</returns>
        [SideEffectFree]
        public static StdLogicVector operator !(StdLogicVector a)
        {
            StdLogicVector result = new StdLogicVector(a.Size);
            for (int i = 0; i < a.Size; i++)
                result[i] = !a[i];
            return result;
        }

        /// <summary>
        /// Computes the bitwise complement of an StdLogicVector
        /// </summary>
        /// <param name="a">The vector to complement</param>
        /// <returns>The bitwise complement</returns>
        [SideEffectFree]
        public static StdLogicVector operator ~(StdLogicVector a)
        {
            StdLogicVector result = new StdLogicVector(a.Size);
            for (int i = 0; i < a.Size; i++)
                result[i] = !a[i];
            return result;
        }

        /// <summary>
        /// Computes the arithmetic sum of two StdLogicVectors, assuming integer arithmetic in two's complement representation.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The sum</returns>
        [SideEffectFree]
        public static StdLogicVector operator +(StdLogicVector a, StdLogicVector b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize = size;
            StdLogic carry = StdLogic._0;
            StdLogicVector result = new StdLogicVector(rsize);
            for (int i = 0; i < rsize; i++)
            {
                StdLogic ai = (i < a.Size) ? a[i] : StdLogic._0;
                StdLogic bi = (i < b.Size) ? b[i] : StdLogic._0;
                StdLogic tmp = ai ^ bi;
                result[i] = (carry ^ tmp);
                carry = (tmp & carry) | (ai & bi);
            }
            return result;
        }

        /// <summary>
        /// Computes the arithmetic difference of two StdLogicVectors, assuming integer arithmetic in two's complement representation.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The difference</returns>
        [SideEffectFree]
        public static StdLogicVector operator -(StdLogicVector a, StdLogicVector b)
        {
            int size = Math.Max(a.Size, b.Size);
            int rsize = size;
            StdLogic borrow = StdLogic._0;
            StdLogicVector result = new StdLogicVector(rsize);
            for (int i = 0; i < rsize; i++)
            {
                StdLogic ai = (i < a.Size) ? a[i] : StdLogic._0;
                StdLogic bi = (i < b.Size) ? b[i] : StdLogic._0;
                StdLogic tmp = ai ^ bi;
                result[i] = (borrow ^ tmp);
                borrow = (!tmp & borrow) | (!ai & bi);
            }
            return result;
        }

        /// <summary>
        /// Computes the bitwise conjunction (AND) of two StdLogicVectors.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The conjunction</returns>
        [SideEffectFree]
        public static StdLogicVector operator &(StdLogicVector a, StdLogicVector b)
        {
            if (a.Size != b.Size)
                throw new ArgumentException("Vectors of different sizes");

            int size = a.Size;
            StdLogicVector result = new StdLogicVector(size);
            for (int i = 0; i < size; i++)
            {
                StdLogic ai = a[i];
                StdLogic bi = b[i];
                result[i] = (ai & bi);
            }
            return result;
        }

        /// <summary>
        /// Computes the bitwise disjunction (OR) of two StdLogicVectors.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The disjunction</returns>
        [SideEffectFree]
        public static StdLogicVector operator |(StdLogicVector a, StdLogicVector b)
        {
            if (a.Size != b.Size)
                throw new ArgumentException("Vectors of different sizes");

            int size = a.Size;
            StdLogicVector result = new StdLogicVector(size);
            for (int i = 0; i < size; i++)
            {
                StdLogic ai = a[i];
                StdLogic bi = b[i];
                result[i] = (ai | bi);
            }
            return result;
        }

        /// <summary>
        /// Computes the bitwise anti-valence (XOR) of two StdLogicVectors.
        /// </summary>
        /// <param name="a">The first operand</param>
        /// <param name="b">The second operand</param>
        /// <returns>The anti-valence</returns>
        [SideEffectFree]
        public static StdLogicVector operator ^(StdLogicVector a, StdLogicVector b)
        {
            if (a.Size != b.Size)
                throw new ArgumentException("Vectors of different sizes");

            int size = a.Size;
            StdLogicVector result = new StdLogicVector(size);
            for (int i = 0; i < size; i++)
            {
                StdLogic ai = a[i];
                StdLogic bi = b[i];
                result[i] = (ai ^ bi);
            }
            return result;
        }

        [SideEffectFree]
        public static bool operator ==(StdLogicVector a, StdLogicVector b)
        {
            return a.Equals(b);
        }

        [SideEffectFree]
        public static bool operator !=(StdLogicVector a, StdLogicVector b)
        {
            return !a.Equals(b);
        }

        /// <summary>
        /// Performs the bitwise resolution of two StdLogicVectors
        /// </summary>
        /// <param name="x">The first operand</param>
        /// <param name="y">The second operand</param>
        /// <returns>The resolution of both operands</returns>
        [SideEffectFree]
        public StdLogicVector Resolve(StdLogicVector x, StdLogicVector y)
        {
            if (x.Size != y.Size)
                throw new ArgumentException("Vector sizes not the same");

            int size = x.Size;
            StdLogicVector result = new StdLogicVector(size);
            for (int i = 0; i < size; i++)
            {
                StdLogic ai = x[i];
                StdLogic bi = y[i];
                result[i] = ai.Resolve(ai, bi);
            }
            return result;
        }

        /// <summary>
        /// Returns true iff each vector element is either '0' or '1'
        /// </summary>
        public bool IsProper
        {
            [SideEffectFree]
            get
            {
                if (_value == null)
                    return true;
                foreach (char c in _value)
                {
                    if (c != '0' && c != '1')
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Converts this StdLogicVector to an int, assuming a two's complement encoding with the MSB denoting the sign.
        /// </summary>
        public int IntValue
        {
            [TypeConversion(typeof(StdLogicVector), typeof(int))]
            [SideEffectFree]
            get
            {
                if (Size > 32)
                    throw new InvalidOperationException("Vector too long");
                if (_value == null)
                    return 0;

                int curv = 0;
                int mask = 1;
                foreach (char c in _value)
                {
                    if (c == '1')
                        curv |= mask;
                    mask <<= 1;
                }
                // This shift operation introduces the correct sign
                int shift = (32 - (int)Size);
                curv <<= shift;
                curv >>= shift;
                return curv;
            }
        }

        /// <summary>
        /// Converts this StdLogicVector to a long, assuming a two's complement encoding with the MSB denoting the sign.
        /// </summary>        
        public long LongValue
        {
            [TypeConversion(typeof(StdLogicVector), typeof(long))]
            [SideEffectFree]
            get
            {
                if (Size > 64)
                    throw new InvalidOperationException("Vector too long");
                if (_value == null)
                    return 0;

                long curv = 0;
                long mask = 1;
                foreach (char c in _value)
                {
                    if (c == '1')
                        curv |= mask;
                    mask <<= 1;
                }
                // This shift operation introduces the correct sign
                int shift = (64 - (int)Size);
                curv <<= shift;
                curv >>= shift;
                return curv;
            }
        }

        /// <summary>
        /// Converts this StdLogicVector to a ulong.
        /// </summary>
        public ulong ULongValue
        {
            [TypeConversion(typeof(StdLogicVector), typeof(ulong))]
            [SideEffectFree]
            get
            {
                if (Size > 64)
                    throw new InvalidOperationException("Vector too long");
                if (_value == null)
                    return 0;

                ulong curv = 0;
                ulong mask = 1;
                foreach (char c in _value)
                {
                    if (c == '1')
                        curv |= mask;
                    mask <<= 1;
                }
                return curv;
            }
        }

        internal StdLogicVector ProperValue
        {
            get
            {
                if (_value == null)
                    return Empty;
                StdLogic[] result = new StdLogic[_value.Length];
                for (int i = 0; i < _value.Length; i++)
                {
                    result[i] = _value[i].Is1 ? StdLogic._1 : StdLogic._0;
                }
                return result;
            }
        }

        public Unsigned UnsignedValue
        {
            [TypeConversion(typeof(StdLogicVector), typeof(Unsigned))]
            [SideEffectFree]
            get
            {
                if (Size > int.MaxValue)
                    throw new InvalidOperationException();

                if (Size == 0)
                    return 0;

#if USE_INTX
                IntX value = IntX.Parse(ProperValue.ToString(), 2);
                return Unsigned.FromIntX(value, (int)Size);
#else
                var value = BigInteger.Zero;
                var msb = BigInteger.One;
                for (int i = 0; i < Size; i++)
                {
                    if (this[i].Is1)
                        value |= msb;
                    msb <<= 1;
                }
                return Unsigned.FromBigInt(value, (int)Size);
#endif
            }
        }

        public Signed SignedValue
        {
            [TypeConversion(typeof(StdLogicVector), typeof(Signed))]
            [SideEffectFree]
            get
            {
                if (Size > int.MaxValue)
                    throw new InvalidOperationException();

                if (Size == 0)
                    return 0;

#if USE_INTX
                IntX value;
                if (this[Size - 1].Equals(StdLogic._1))
                {
                    // negative
                    StdLogicVector abs = !ProperValue + "1";
                    value = IntX.Parse("-" + abs.ToString(), 2);
                }
                else
                {
                    value = IntX.Parse(ProperValue.ToString(), 2);
                }
                return Signed.FromIntX(value, (int)Size);
#else
                var digits = new byte[(Size + 7) / 8];
                byte bi = 1;
                int by = 0;
                bool last = false;
                for (int i = 0; i < Size; i++)
                {
                    last = this[i].Is1;
                    if (last)
                        digits[by] |= bi;
                    bi <<= 1;
                    if (bi == 0)
                    {
                        bi = 1;
                        ++by;
                    }
                }
                if (last && bi != 1)
                {
                    while (bi != 0)
                    {
                        digits[by] |= bi;
                        bi <<= 1;
                    }
                }
                var v = new BigInteger(digits);
                return Signed.FromBigInt(v, (int)Size);
#endif
            }
        }

        /// <summary>
        /// Converts this StdLogicVector to a weak representation.
        /// </summary>
        /// <seealso cref="StdLogic.Weak"/>
        public StdLogicVector Weak
        {
            [SideEffectFree]
            get
            {
                StdLogicVector result = new StdLogicVector();
                for (int i = 0; i < Size; i++)
                {
                    result[i] = _value[i].Weak;
                }
                return result;
            }
        }

        private class FromXRewriter : RewriteCall
        {
            Type _srcType;

            public FromXRewriter(Type srcType)
            {
                _srcType = srcType;
            }

            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                Expression[] eargs = args.Select(arg => arg.Expr).ToArray();
                object[] vargs = args.Select(arg => arg.Sample).ToArray();

                object sample = null;
                try
                {
                    sample = callee.Invoke(vargs);
                }
                catch (Exception)
                {
                }
                TypeDescriptor type;
                if (sample != null)
                    type = TypeDescriptor.GetTypeOf(sample);
                else
                    type = typeof(StdLogicVector);

                FunctionCall fcall = IntrinsicFunctions.Cast(eargs, _srcType, type);

                stack.Push(fcall, sample);
                return true;
            }
        }

        /// <summary>
        /// Converts a long value to its two's complement enconding.
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target bit width</param>
        /// <returns>The binary encoding</returns>
        [FromXRewriter(typeof(long))]
        [SideEffectFree]
        public static StdLogicVector FromLong(long value, int size)
        {
            return Signed.FromLong(value, size).SLVValue;
        }

        /// <summary>
        /// Converts an int value to its two's complement enconding.
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target bit width</param>
        /// <returns>The binary encoding</returns>
        [FromXRewriter(typeof(int))]
        [SideEffectFree]
        public static StdLogicVector FromInt(int value, int size)
        {
            return Signed.FromInt(value, size).SLVValue;
        }

        /// <summary>
        /// Converts a ulong value to its binary enconding.
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target bit width</param>
        /// <returns>The binary encoding</returns>
        [FromXRewriter(typeof(ulong))]
        [SideEffectFree]
        public static StdLogicVector FromULong(ulong value, int size)
        {
            return Unsigned.FromULong(value, size).SLVValue;
        }

        /// <summary>
        /// Converts a uint value to its binary enconding.
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="size">The target bit width</param>
        /// <returns>The binary encoding</returns>
        [FromXRewriter(typeof(uint))]
        [SideEffectFree]
        public static StdLogicVector FromUInt(uint value, int size)
        {
            return Unsigned.FromUInt(value, size).SLVValue;
        }

        public static StdLogicVector Serialize(object value)
        {
            Type srcType = value.GetType();
            ISerializer ser = SLVSerializable.TryGetSerializer(srcType);
            if (ser == null)
                throw new ArgumentException("Serialization not supported for type " + srcType);
            return ser.Serialize(value);
        }

        private class DeserializeRewriter : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var tgtType = (TypeDescriptor)args[1].Sample;
                stack.Push(
                    IntrinsicFunctions.Cast(args[0].Expr, args[0].Expr.ResultType.CILType, tgtType),
                    Deserialize((StdLogicVector)args[0].Sample, tgtType));
                return true;
            }
        }

        [DeserializeRewriter]
        public static object Deserialize(StdLogicVector slv, TypeDescriptor tgtType)
        {
            ISerializer ser = SLVSerializable.TryGetSerializer(tgtType.CILType);
            if (ser == null)
                throw new ArgumentException("Serialization not supported for type " + tgtType);
            return ser.Deserialize(slv, tgtType);
        }

        public static TypeDescriptor MakeType(long slvlength)
        {
            return TypeDescriptor.GetTypeOf(_0s(slvlength));
        }

        public static int GetLength(TypeDescriptor type)
        {
            Contract.Requires(type.CILType.Equals(typeof(StdLogicVector)));
            Contract.Requires(type.IsComplete);

            return (int)type.TypeParams[0];
        }
    }

    public class SizedResolvedSignal<TA, TE> :
        ResolvedSignal<TA>, IIndexable<InOut<TA>, InOut<TE>>
        where TA : IResolvable<TA>, IIndexable<TA, TE>, IConcatenable<TA, TE>
    {
        private class SliceAccessProxy :
            InOut<TA>,
            ISignal
        {
            private SizedResolvedSignal<TA, TE> _signal;
            private int _from;
            private int _to;

            public SliceAccessProxy(SizedResolvedSignal<TA, TE> signal, int from, int to)
            {
                _signal = signal;
                _from = from;
                _to = to;
            }

            public TA Cur
            {
                [SignalProperty(SignalRef.EReferencedProperty.Cur)]
                get
                {
                    return _signal.Cur[_from, _to];
                }
            }

            public TA Pre
            {
                [SignalProperty(SignalRef.EReferencedProperty.Pre)]
                get
                {
                    return _signal.Pre[_from, _to];
                }
            }

            public TA Next
            {
                [SignalProperty(SignalRef.EReferencedProperty.Next)]
                set
                {
                    _signal.Next = _signal.Next[_signal.Size - 1, _from + 1].Concat(value).Concat(_signal.Next[_to - 1, 0]);
                }
            }

            public AbstractEvent ChangedEvent
            {
                [SignalProperty(SignalRef.EReferencedProperty.ChangedEvent)]
                get { return _signal.ChangedEvent; }
            }

            In<TA> Out<TA>.Dual
            {
                get
                {
                    return this;
                }
            }

            Out<TA> In<TA>.Dual
            {
                get
                {
                    return this;
                }
            }

            public object InitialValueObject
            {
                get { return _signal.InitialValue[_from, _to]; }
            }

            public object PreObject
            {
                get { return Pre; }
            }

            public object CurObject
            {
                get { return Cur; }
            }

            public object NextObject
            {
                set { Next = (TA)value; }
            }

            public TypeDescriptor ElementType
            {
                get { return TypeDescriptor.GetTypeOf(InitialValueObject); }
            }

            public SignalRef ToSignalRef(SignalRef.EReferencedProperty prop)
            {
                var index = new IndexSpec((DimSpec)new Range(_from, _to, EDimDirection.Downto));
                return new SignalRef(_signal.Descriptor, prop,
                    index.AsExpressions(), index, true);
            }
        }

        private class ElementAccessProxy :
            InOut<TE>,
            ISignal
        {
            private SizedResolvedSignal<TA, TE> _signal;
            private int _index;

            public ElementAccessProxy(SizedResolvedSignal<TA, TE> signal, int index)
            {
                _signal = signal;
                _index = index;
            }

            public TE Cur
            {
                [SignalProperty(SignalRef.EReferencedProperty.Cur)]
                get { return _signal.Cur[_index]; }
            }

            public TE Pre
            {
                [SignalProperty(SignalRef.EReferencedProperty.Pre)]
                get { return _signal.Pre[_index]; }
            }

            public TE Next
            {
                [SignalProperty(SignalRef.EReferencedProperty.Next)]
                set
                {
                    _signal.Next = _signal.Next[_signal.Size - 1, _index + 1].Concat(value).Concat(_signal.Next[_index - 1, 0]);
                }
            }

            public AbstractEvent ChangedEvent
            {
                [SignalProperty(SignalRef.EReferencedProperty.ChangedEvent)]
                get { return _signal.ChangedEvent; }
            }

            In<TE> Out<TE>.Dual
            {
                get { return this; }
            }

            Out<TE> In<TE>.Dual
            {
                get { return this; }
            }

            public object InitialValueObject
            {
                get { return _signal.InitialValue[_index]; }
            }

            public object PreObject
            {
                get { return Pre; }
            }

            public object CurObject
            {
                get { return Cur; }
            }

            public object NextObject
            {
                set { Next = (TE)value; }
            }

            public TypeDescriptor ElementType
            {
                get { return TypeDescriptor.GetTypeOf(InitialValueObject); }
            }

            public SignalRef ToSignalRef(SignalRef.EReferencedProperty prop)
            {
                var index = new IndexSpec((DimSpec)_index);
                return new SignalRef(_signal.Descriptor, prop,
                    index.AsExpressions(), index, true);
            }
        }

        public SizedResolvedSignal(int size)
        {
            Size = size;
        }

        public int Size
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.PropertyRef, ESizedProperties.Size)]
            [SideEffectFree]
            get;
            private set;
        }

        public override TA InitialValue
        {
            get
            {
                return base.InitialValue;
            }
            set
            {
                if (value.Size != Size)
                    throw new ArgumentException("Wrong vector size");

                base.InitialValue = value;
            }
        }

        public override TA Next
        {
            internal get
            {
                return base.Next;
            }
            [SignalProperty(SignalRef.EReferencedProperty.Next)]
            set
            {
                if (value.Size != Size)
                    throw new ArgumentException("Wrong vector size");

                base.Next = value;
            }
        }

        public InOut<TA> this[int i0, int i1]
        {
            [MapToSlice]
            [SideEffectFree]
            get
            {
                return new SliceAccessProxy(this, i0, i1);
            }
        }

        public InOut<TE> this[int index]
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.Index)]
            [SideEffectFree]
            get
            {
                return new ElementAccessProxy(this, index);
            }
        }

        [AssumeNotCalled]
        public override ISignal ApplyIndex(IndexSpec idx)
        {
            if (idx.Indices.Length == 0)
            {
                return this;
            }
            else if (idx.Indices.Length == 1)
            {
                DimSpec idx0 = idx.Indices[0];
                switch (idx0.Kind)
                {
                    case DimSpec.EKind.Index:
                        return (ISignal)this[(int)idx0];

                    case DimSpec.EKind.Range:
                        {
                            Range rng = (Range)idx0;
                            return (ISignal)this[rng.FirstBound, rng.SecondBound];
                        }

                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }

    public static class StdLogicExtensions
    {
        [SignalProperty(SignalRef.EReferencedProperty.RisingEdge)]
        public static bool RisingEdge(this In<StdLogic> slin)
        {
            return slin.Pre == '0' && slin.Cur == '1';
        }

        [SignalProperty(SignalRef.EReferencedProperty.FallingEdge)]
        public static bool FallingEdge(this In<StdLogic> slin)
        {
            return slin.Pre == '1' && slin.Cur == '0';
        }

        /// <summary>
        /// Returns '1' iff any bit of slv is set, '0' else
        /// </summary>
        public static StdLogic Any(this StdLogicVector slv)
        {
            var cpy = slv;
            for (int i = 0; i < cpy.Size; i++)
            {
                if (cpy[i] == StdLogic._1)
                    return StdLogic._1;
            }
            return StdLogic._0;
        }

        /// <summary>
        /// Returns '1' iff all bits of slv are set, '0' else
        /// </summary>
        public static StdLogic All(this StdLogicVector slv)
        {
            for (int i = 0; i < slv.Size; i++)
            {
                if (slv[i] == StdLogic._0)
                    return StdLogic._0;
            }
            return StdLogic._1;
        }
    }
}

namespace SystemSharp.Components
{
    public class SLSignal :
        ResolvedSignal<StdLogic>
    {
        public SLSignal()
        {
            InitialValue = 'U';
        }
    }

    public class SLVSignal : SizedResolvedSignal<StdLogicVector, StdLogic>
    {
        public SLVSignal(int size) :
            base(size)
        {
            InitialValue = StdLogicVector.Us(size);
        }

        public SLVSignal(StdLogicVector initValue) :
            base(initValue.Size)
        {
            InitialValue = initValue;
        }
    }

    public static class SLVExtensions
    {
        public static int Size(this InOut<StdLogicVector> port)
        {
            return ((SLVSignal)port).Size;
        }

        public static int Size(this In<StdLogicVector> port)
        {
            return ((SLVSignal)port).Size;
        }

        public static int Size(this Out<StdLogicVector> port)
        {
            return ((SLVSignal)port).Size;
        }

        public static async Task<StdLogicVector> Add(this Task<StdLogicVector> a, Task<StdLogicVector> b)
        {
            var av = await a;
            var bv = await b;
            return av + bv;
        }
    }

    class SLVConcatRewriter : RewriteCall
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            // Little on-the-fly optimization: if one vector is empty, no need to instantiate a concat operator
            var slv0 = args[0].Sample as StdLogicVector?;
            var slv1 = args[1].Sample as StdLogicVector?;
            var sl0 = args[0].Sample as StdLogic?;
            var sl1 = args[1].Sample as StdLogic?;
            if (slv0 != null && slv0.Value.Size == 0)
            {
                if (slv1 != null)
                {
                    stack.Push(args[1]);
                }
                else
                {
                    slv1 = sl1.Value.Concat(StdLogicVector.Empty);
                    stack.Push(IntrinsicFunctions.Cast(args[1].Expr, typeof(StdLogic), TypeDescriptor.GetTypeOf(slv1.Value)), slv1.Value);
                }
            }
            else if (slv1 != null && slv1.Value.Size == 0)
            {
                if (slv0 != null)
                {
                    stack.Push(args[0]);
                }
                else
                {
                    slv0 = sl0.Value.Concat(StdLogicVector.Empty);
                    stack.Push(IntrinsicFunctions.Cast(args[0].Expr, typeof(StdLogic), TypeDescriptor.GetTypeOf(slv0.Value)), slv0.Value);
                }
            }
            else if (slv0 != null && slv1 != null)
            {
                stack.Push(Expression.Concat(args[0].Expr, args[1].Expr), slv0.Value.Concat(slv1.Value));
            }
            else if (slv0 != null && sl1 != null)
            {
                stack.Push(Expression.Concat(args[0].Expr, args[1].Expr), slv0.Value.Concat(sl1.Value));
            }
            else if (sl0 != null && slv1 != null)
            {
                stack.Push(Expression.Concat(args[0].Expr, args[1].Expr), sl0.Value.Concat(slv1.Value));
            }
            else if (sl0 != null && sl1 != null)
            {
                stack.Push(Expression.Concat(args[0].Expr, args[1].Expr), sl0.Value.Concat(sl1.Value));
            }
            return true;
        }
    }

}

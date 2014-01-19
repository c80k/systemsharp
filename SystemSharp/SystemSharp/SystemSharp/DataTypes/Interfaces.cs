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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.SysDOM;

namespace SystemSharp.DataTypes
{
    /// <summary>
    /// A resolution interface for resolvable data types.
    /// </summary>
    /// <typeparam name="T">The resolvable data type</typeparam>
    public interface IResolvable<T>
    {
        /// <summary>
        /// Computes the resolution of this value and another value.
        /// </summary>
        /// <param name="x">second value</param>
        T Resolve(T x);
    }

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
    /// <typeparam name="TE">The data type of a single indexed element</typeparam>
    public interface IIndexable<TE> :
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
        /// <param name="r">The range to retrieve</param>
        /// <returns>The slice</returns>
        IIndexable<TE> this[Range r]
        {
            [MapToSlice]
            [SideEffectFree]
            get;
        }
    }

    public interface IMatrixIndexable<TE>
    {
        int Size0 { get; }
        int Size1 { get; }

        TE this[int i, int j] { get; }
        IIndexable<TE> this[int i, Range rj] { get; }
        IIndexable<TE> this[Range ri, int rj] { get; }
        IMatrixIndexable<TE> this[Range ri, Range rj] { get; }
    }

    public interface IMultiIndexable<TE>
    {
        Vector<int> Sizes { get; }
        IMultiIndexable<TE> this[params DimSpec[] indices] { get; }
        TE GetElement();
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
}

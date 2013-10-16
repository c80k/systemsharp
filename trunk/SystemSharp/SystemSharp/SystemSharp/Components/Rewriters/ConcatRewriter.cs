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
using System.Linq;
using System.Text;
using System.Reflection;
using SystemSharp.Analysis;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// Rewrites .NET compiler-generated code for string concatenation to the SysDOM-intrinsic StringConcat operation.
    /// </summary>
    /// <remarks>
    /// .NET compiler-generated code for string concatenation conceptually allocates a new array, assigns each individual string 
    /// (or object) to an array element and finally calls <c>string.Concat(...)</c>. This scheme is ugly if we want to reconstruct a
    /// clean SysDOM representation, allowing us to generate source code for a different target language. Thus, this rewriter does a
    /// little bit of pattern matching: It eliminates the array allocation/element assignment procedure and directly puts the operands
    /// into the StringConcat function instead.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    class ConcatRewriter: RewriteCall
    {
        private void MakeStringArray(Expression[] elements)
        {
            TypeDescriptor stype = (TypeDescriptor)typeof(string);
            for (int i = 0; i < elements.Length; i++)
            {
                TypeDescriptor rtype = elements[i].ResultType;
                if (!rtype.Equals(stype))
                {
                    elements[i] = IntrinsicFunctions.Cast(
                        elements[i],
                        rtype.CILType, typeof(string));
                }
            }
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, 
            IDecompiler decomp, IFunctionBuilder builder)
        {
            Expression[] elements = null;
            if (args.Length == 1)
            {
                Expression valarr = args[0].Expr;
                valarr = decomp.ResolveVariableReference(decomp.CurrentILIndex, valarr);
                FunctionCall newarrCall = valarr as FunctionCall;
                if (newarrCall != null)
                {
                    FunctionSpec fspec = newarrCall.Callee as FunctionSpec;
                    IntrinsicFunction ifun = fspec == null ? null : fspec.IntrinsicRep;
                    if (ifun != null && ifun.Action == IntrinsicFunction.EAction.NewArray)
                    {
                        ArrayParams aparams = (ArrayParams)ifun.Parameter;
                        if (aparams.IsStatic)
                        {
                            newarrCall.IsInlined = true;
                            for (int i = 0; i < aparams.Elements.Length; i++)
                            {
                                aparams.Elements[i].IsInlined = true;
                            }
                            elements = aparams.Elements;
                        }
                    }
                }
            }
            else
            {
                elements = args.Select(arg => arg.Expr).ToArray();
            }
            if (elements == null)
            {
                throw new NotImplementedException();
            }
            MakeStringArray(elements);
            decomp.Push(
                IntrinsicFunctions.StringConcat(elements),
                "");
            return true;
        }
    }
}

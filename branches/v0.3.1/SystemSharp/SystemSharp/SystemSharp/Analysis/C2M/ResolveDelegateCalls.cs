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
 * 
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Analysis.C2M
{
    /// <summary>
    /// This rewriter will detect calls to delegates and replace such calls with the according direct method call.
    /// </summary>
    public class ResolveDelegateCalls: RewriteCall
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            if (args[0].Variability == Msil.EVariability.ExternVariable)
                throw new NotSupportedException("Delegate calls must have constant target!");

            Delegate deleg = (Delegate)args[0].Sample;
            if (deleg == null)
                return false;

            StackElement[] callArgs;
            if (deleg.Method.IsStatic)
            {
                callArgs = new StackElement[args.Length - 1];
                Array.Copy(args, 1, callArgs, 0, args.Length - 1);
            }
            else
            {
                callArgs = new StackElement[args.Length];
                Array.Copy(args, callArgs, args.Length);
                callArgs[0].Sample = deleg.Target;
                callArgs[0].Expr = LiteralReference.CreateConstant(deleg.Target);
                callArgs[0].Variability = Msil.EVariability.Constant;
            }
            stack.ImplementCall(deleg.Method, callArgs);
            return true;
        }

        /// <summary>
        /// Registers the rewriter with the AttributeInjector, for calls of the Delegate.Invoke method.
        /// </summary>
        internal static void Register()
        {
            AttributeInjector.InjectMethodByNameAttr(typeof(Delegate), "Invoke", new ResolveDelegateCalls(), true);
        }
    }
}

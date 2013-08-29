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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Meta;
using System.Reflection;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Transactions
{
#if false
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class TransactionSpec: RewriteMethodDefinition
    {
        public override IDecompilationResult Rewrite(DesignContext ctx, CodeDescriptor code, object instance, object[] arguments)
        {
            MSILDecompiler decomp = new MSILDecompiler(code, instance, arguments);
            decomp.Template.DisallowConditionals = true;
            decomp.RewriteContext = this;
            return decomp.Decompile();
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class Dualize : RewriteCall
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            TransactionSpec tspec = stack.RewriteContext as TransactionSpec;
            if (tspec == null)
                throw new InvalidOperationException("The .Dual property may only be used by methods tagged with a TransactionSpec attribute.");

            throw new NotImplementedException();
        }
    }
#endif
}

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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Algebraic;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.SysDOM
{
    public static class ConstructionHelpers
    {
        private static Expression ToExpression(object arg)
        {
            if (arg is Expression)
            {
                var ex = (Expression)arg;
                if (ex.ResultType.CILType.Equals(typeof(string)))
                    return ex;
                else
                    return IntrinsicFunctions.Cast(ex, ex.ResultType.CILType, typeof(string));
            }
            else if (arg is ILiteral)
            {
                return ToExpression(new LiteralReference((ILiteral)arg));
            }
            else
            {
                return LiteralReference.CreateConstant(arg.ToString());
            }
        }

        public static void ReportLine(this IAlgorithmBuilder builder, params object[] args)
        {
            var arg = Expression.Concat(args.Select(_ => ToExpression(_)).ToArray());
            builder.Call(IntrinsicFunctions.ReportLine(arg), arg);
        }
    }
}

/**
 * Copyright 2014 Christian Köllner
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

namespace SystemSharp.SysDOM
{
    public static class ExpressionCompiler
    {
        public static TDelegate Compile<TDelegate>(this Expression e)
        {
            throw new NotImplementedException();
        }

        public static Func<T1, T2> Compile<T1, T2>(this Func<Expression, Expression> f)
        {
            throw new NotImplementedException();
        }

        public static Func<T1, T2, T3> Compile<T1, T2, T3>(this Func<Expression, Expression, Expression> f)
        {
            throw new NotImplementedException();
        }
    }
}

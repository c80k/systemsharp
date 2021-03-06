﻿/**
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

namespace SystemSharp.SysDOM
{
    /// <summary>
    /// This static class provides extension methods which simplify the usage of expressions.
    /// </summary>
    public static class ExpressionHelper
    {
        /// <summary>
        /// Returns <c>true</c> if the expression is a binary operation.
        /// </summary>
        public static bool IsBinOp(this Expression e, BinOp.Kind op)
        {
            BinOp bop = e as BinOp;
            if (bop == null)
                return false;
            return bop.Operation == op;
        }

        /// <summary>
        /// Returns <c>true</c> if the expression is a unary operation.
        /// </summary>
        public static bool IsUnOp(this Expression e, UnOp.Kind op)
        {
            UnOp uop = e as UnOp;
            if (uop == null)
                return false;
            return uop.Operation == op;
        }

        /// <summary>
        /// Returns <c>true</c> if the expression is a constant literal.
        /// </summary>
        public static bool IsConstantLiteral(this Expression e)
        {
            LiteralReference lr = e as LiteralReference;
            if (lr == null)
                return false;
            Constant c = lr.ReferencedObject as Constant;
            if (c == null)
                return false;
            else
                return true;
        }

        /// <summary>
        /// Returns the constant value if the expression is a constant literal, otherwise <c>null</c>.
        /// </summary>
        public static object AsConstantLiteralValue(this Expression e)
        {
            LiteralReference lr = e as LiteralReference;
            if (lr == null)
                return null;
            Constant c = lr.ReferencedObject as Constant;
            if (c == null)
                return null;
            else
                return c.ConstantValue;
        }
    }
}

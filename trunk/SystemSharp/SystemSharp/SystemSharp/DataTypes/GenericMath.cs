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
using LinqEx = System.Linq.Expressions.Expression;

namespace SystemSharp.DataTypes
{
    public static class GenericMath<T>
    {
        private static Func<T, T> MkUnary(Func<LinqEx, LinqEx> gen)
        {
            var x = LinqEx.Parameter(typeof(T));
            var y = gen(x);
            var lambda = LinqEx.Lambda<Func<T, T>>(y, x);
            return lambda.Compile();
        }

        private static Func<T, T, T> MkBinary(Func<LinqEx, LinqEx, LinqEx> gen)
        {
            var x = LinqEx.Parameter(typeof(T));
            var y = LinqEx.Parameter(typeof(T));
            var z = gen(x, y);
            var lambda = LinqEx.Lambda<Func<T, T, T>>(z, x, y);
            return lambda.Compile();
        }

        private static Lazy<Func<T, T>> _negate = new Lazy<Func<T, T>>(() => MkUnary(LinqEx.Negate));
        private static Lazy<Func<T, T>> _not = new Lazy<Func<T, T>>(() => MkUnary(LinqEx.Not));
        private static Lazy<Func<T, T, T>> _add = new Lazy<Func<T, T, T>>(() => MkBinary(LinqEx.Add));
        private static Lazy<Func<T, T, T>> _sub = new Lazy<Func<T, T, T>>(() => MkBinary(LinqEx.Subtract));
        private static Lazy<Func<T, T, T>> _mul = new Lazy<Func<T, T, T>>(() => MkBinary(LinqEx.Multiply));
        private static Lazy<Func<T, T, T>> _div = new Lazy<Func<T, T, T>>(() => MkBinary(LinqEx.Divide));
        private static Lazy<Func<T, T, T>> _and = new Lazy<Func<T, T, T>>(() => MkBinary(LinqEx.And));
        private static Lazy<Func<T, T, T>> _or = new Lazy<Func<T, T, T>>(() => MkBinary(LinqEx.Or));
        private static Lazy<Func<T, T, T>> _xor = new Lazy<Func<T, T, T>>(() => MkBinary(LinqEx.ExclusiveOr));

        public static Func<T, T> Negate
        {
            get { return _negate.Value; }
        }

        public static Func<T, T> Not
        {
            get { return _not.Value; }
        }

        public static Func<T, T, T> Add
        {
            get { return _add.Value; }
        }

        public static Func<T, T, T> Subtract
        {
            get { return _sub.Value; }
        }

        public static Func<T, T, T> Multiply
        {
            get { return _mul.Value; }
        }

        public static Func<T, T, T> Divide
        {
            get { return _div.Value; }
        }

        public static Func<T, T, T> And
        {
            get { return _and.Value; }
        }

        public static Func<T, T, T> Or
        {
            get { return _or.Value; }
        }

        public static Func<T, T, T> Xor
        {
            get { return _xor.Value; }
        }
    }
}

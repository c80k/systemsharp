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
using System.Linq;
using System.Text;
using SystemSharp.Algebraic;
using SystemSharp.Meta;

namespace SystemSharp.SysDOM
{
    /// <summary>
    /// Performs an action on the given expression.
    /// </summary>
    public delegate void MatchAction(Expression e);

    /// <summary>
    /// Creates a new expression.
    /// </summary>
    public delegate Expression ExpressionGenerator();

    /// <summary>
    /// Establishes a predicate over expressions, thus matching expressions with certain properties.
    /// </summary>
    public class Matching
    {
        private Expression.MatchFunction _func;
        private ExpressionGenerator _gen;
        private Expression _expr;

        /// <summary>
        /// Constructs a new matching.
        /// </summary>
        public Matching()
        {
            _func = x => true;
            _gen = GetExpression;
        }

        /// <summary>
        /// Returns the expression that matches the predicate of this matching instance.
        /// </summary>
        public Expression Result
        {
            get { return _expr; }
        }

        private void SetExpression(Expression e)
        {
            _expr = e;
        }

        /// <summary>
        /// Returns the expression that matches the predicate of this matching instance.
        /// </summary>
        public Expression GetExpression()
        {
            return _expr;
        }

        private bool Match(Expression e)
        {
            SetExpression(e);
            return _func(e);
        }

        /// <summary>
        /// Converts the matching to a predicate function.
        /// </summary>
        public static implicit operator Expression.MatchFunction(Matching m)
        {
            return m.Match;
        }

        /// <summary>
        /// Converts a predicate function to a matching instance.
        /// </summary>
        public static implicit operator Matching(Expression.MatchFunction mf)
        {
            return new Matching() { _func = mf };
        }

        /// <summary>
        /// Converts the expression to a node type matching.
        /// </summary>
        public static implicit operator Matching(Expression e)
        {
            Matching result = Node(e);
            result._gen = () => e;
            return result;
        }

        /// <summary>
        /// Converts the matching to an expression generator.
        /// </summary>
        public static implicit operator Generation(Matching m)
        {
            return m._gen;
        }

        /// <summary>
        /// Creates a node type matching predicate from the expression.
        /// </summary>
        public static Expression.MatchFunction Node(Expression e)
        {
            return x => e.NodeEquals(x);
        }

        /// <summary>
        /// Creates a structural matching predicate from the expression.
        /// </summary>
        public static Expression.MatchFunction Deep(Expression e)
        {
            return x => e.DeepEquals(x);
        }

        private bool MatchAny(Expression e, MatchAction ma)
        {
            ma(e);
            return true;
        }

        /// <summary>
        /// Combines three matching predicates, such that every predicate must be satisfied.
        /// </summary>
        /// <param name="fnp">node-level predicate</param>
        /// <param name="fnc1">left child predicate</param>
        /// <param name="fnc2">right child predicate</param>
        public static Expression.MatchFunction Parent2Children(Expression.MatchFunction fnp,
            Expression.MatchFunction fnc1, Expression.MatchFunction fnc2)
        {
            return x => fnp(x) && fnc1(x.Children.ElementAt(0)) && fnc2(x.Children.ElementAt(1));
        }

        /// <summary>
        /// Creates a matching for the specified kind of unary operations.
        /// </summary>
        /// <param name="kind">kind of unary operation to match</param>
        public Matching MUnOp(UnOp.Kind kind)
        {
            UnOp cmp = new UnOp() { Operation = kind };
            Matching newm = new Matching();
            newm._func = e => e.NodeEquals(cmp) && 
                    this.Match(e.Children.ElementAt(0));
            newm._gen = () => newm._expr == null ? 
                    new UnOp() { Operation = kind, Operand = _gen() } : 
                    newm._expr;
            return newm;
        }

        /// <summary>
        /// Creates a matching for the specified kind of binary operation.
        /// </summary>
        /// <param name="kind">kind of binary operation to match</param>
        /// <param name="peer">right child matching</param>
        public Matching MBinOp(BinOp.Kind kind, Matching peer)
        {
            BinOp cmp = new BinOp() { Operation = kind };
            Matching newm = new Matching();
            newm._func = e => e.NodeEquals(cmp) && 
                    this.Match(e.Children.ElementAt(0)) && 
                    peer.Match(e.Children.ElementAt(1));
            newm._gen = () => newm._expr == null ? 
                new BinOp() { Operation = kind, Operand1 = _gen(), Operand2 = peer._gen() } : newm._expr;
            return newm;
        }

        /// <summary>
        /// Constrains the matching, using an additional predicate.
        /// </summary>
        /// <param name="constraint">additional constraint</param>
        public Matching Constrain(Func<bool> constraint)
        {
            Matching result = new Matching();
            result._func = e => this.Match(e) && constraint();
            result._gen = this._gen;
            return result;
        }

        /// <summary>
        /// Creates a matching for the specified kind of binary operation.
        /// </summary>
        /// <param name="kind">kind of binary operation to match</param>
        public static Expression.MatchFunction BinOp(BinOp.Kind kind)
        {
            return Node(new BinOp() { Operation = kind });
        }

        /// <summary>
        /// Creates a matching for expression negation.
        /// </summary>
        /// <param name="fn">operand matching</param>
        public static Matching operator -(Matching fn)
        {
            return fn.MUnOp(UnOp.Kind.Neg);
        }

        /// <summary>
        /// Creates a matching for boolean expression inversion.
        /// </summary>
        /// <param name="fn">operand matching</param>
        public static Matching operator !(Matching fn)
        {
            return fn.MUnOp(UnOp.Kind.BoolNot);
        }

        /// <summary>
        /// Creates a matching for bit-wise expression complement.
        /// </summary>
        /// <param name="fn">operand matching</param>
        public static Matching operator ~(Matching fn)
        {
            return fn.MUnOp(UnOp.Kind.BitwiseNot);
        }

        /// <summary>
        /// Creates a matching for expression addition.
        /// </summary>
        /// <param name="fn1">left child matching</param>
        /// <param name="fn2">right child matching</param>
        public static Matching operator +(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Add, fn2);
        }

        /// <summary>
        /// Creates a matching for expression subtraction.
        /// </summary>
        /// <param name="fn1">left child matching</param>
        /// <param name="fn2">right child matching</param>
        public static Matching operator -(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Sub, fn2);
        }

        /// <summary>
        /// Creates a matching for expression multiplication.
        /// </summary>
        /// <param name="fn1">left child matching</param>
        /// <param name="fn2">right child matching</param>
        public static Matching operator *(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Mul, fn2);
        }

        /// <summary>
        /// Creates a matching for expression division.
        /// </summary>
        /// <param name="fn1">left child matching</param>
        /// <param name="fn2">right child matching</param>
        public static Matching operator /(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Div, fn2);
        }

        /// <summary>
        /// Creates a matching for expression division remainder.
        /// </summary>
        /// <param name="fn1">left child matching</param>
        /// <param name="fn2">right child matching</param>
        public static Matching operator %(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Rem, fn2);
        }

        /// <summary>
        /// Creates a matching for boolean/bit-wise expression conjunction.
        /// </summary>
        /// <param name="fn1">left child matching</param>
        /// <param name="fn2">right child matching</param>
        public static Matching operator &(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.And, fn2);
        }

        /// <summary>
        /// Creates a matching for boolean/bit-wise expression disjunction.
        /// </summary>
        /// <param name="fn1">left child matching</param>
        /// <param name="fn2">right child matching</param>
        public static Matching operator |(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Or, fn2);
        }

        /// <summary>
        /// Creates a matching for equality comparison.
        /// </summary>
        /// <param name="y">right operand matching</param>
        /// <returns></returns>
        public Matching Eq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.Eq, y);
        }

        /// <summary>
        /// Creates a matching for inequality comparison.
        /// </summary>
        /// <param name="y">right operand matching</param>
        /// <returns></returns>
        public Matching NEq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.NEq, y);
        }

        /// <summary>
        /// Creates a matching for "less than" comparison.
        /// </summary>
        /// <param name="y">right operand matching</param>
        /// <returns></returns>
        public Matching Lt(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.Lt, y);
        }

        /// <summary>
        /// Creates a matching for "less than or equal" comparison.
        /// </summary>
        /// <param name="y">right operand matching</param>
        /// <returns></returns>
        public Matching LtEq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.LtEq, y);
        }

        /// <summary>
        /// Creates a matching for "greater than" comparison.
        /// </summary>
        /// <param name="y">right operand matching</param>
        /// <returns></returns>
        public Matching Gt(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.Gt, y);
        }

        /// <summary>
        /// Creates a matching for "greater than or equal" comparison.
        /// </summary>
        /// <param name="y">right operand matching</param>
        /// <returns></returns>
        public Matching GtEq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.GtEq, y);
        }

        /// <summary>
        /// Creates a matching for type cast expressions.
        /// </summary>
        /// <param name="srcType">source type</param>
        /// <param name="dstType">destination type</param>
        public Matching Cast(Type srcType, TypeDescriptor dstType)
        {
            var cast = IntrinsicFunctions.Cast((Expression)null, srcType, dstType);
            Matching newm = new Matching();
            newm._func = e => e.NodeEquals(cast) &&
                    this.Match(e.Children.ElementAt(0));
            newm._gen = () => newm._expr == null ?
                    IntrinsicFunctions.Cast(_gen(), srcType, dstType) :
                    newm._expr;
            return newm;
        }
    }

    /// <summary>
    /// Encapsulates and constructs expression generators.
    /// </summary>
    public class Generation
    {
        /// <summary>
        /// Gets or sets the generator function.
        /// </summary>
        public ExpressionGenerator Generator { get; set; }

        /// <summary>
        /// Converts this instance to a generator function.
        /// </summary>
        public static implicit operator ExpressionGenerator(Generation gen)
        {
            return gen.Generator;
        }

        /// <summary>
        /// Converts the generator function to a <c>Generation</c> instance.
        /// </summary>
        public static implicit operator Generation(ExpressionGenerator g)
        {
            return new Generation() { Generator = g };
        }

        /// <summary>
        /// Constructs a generator that returns the supplied expression.
        /// </summary>
        public static ExpressionGenerator Copy(Expression e)
        {
            return () => e;
        }

        /// <summary>
        /// Constructs a generator for unary expressions.
        /// </summary>
        /// <param name="kind">kind of unary expression to construct</param>
        /// <param name="g">operand generator</param>
        public static ExpressionGenerator UnOp(UnOp.Kind kind, ExpressionGenerator g)
        {
            return () => new UnOp()
            {
                Operation = kind,
                Operand = g()
            };
        }

        /// <summary>
        /// Constructs a generator for binary expressions.
        /// </summary>
        /// <param name="kind">kind of binary expression to construct</param>
        /// <param name="g1">left operand generator</param>
        /// <param name="g2">right operand generator</param>
        public static ExpressionGenerator BinOp(BinOp.Kind kind,
            ExpressionGenerator g1, ExpressionGenerator g2)
        {
            return () => new BinOp()
            {
                Operation = kind,
                Operand1 = g1(),
                Operand2 = g2()
            };
        }

        /// <summary>
        /// Constructs an expression negation generator.
        /// </summary>
        /// <param name="g">operand generator</param>
        public static Generation operator -(Generation g)
        {
            return UnOp(SystemSharp.SysDOM.UnOp.Kind.Neg, g);
        }

        /// <summary>
        /// Constructs a boolean expression inversion generator.
        /// </summary>
        /// <param name="g">operand generator</param>
        public static Generation operator !(Generation g)
        {
            return UnOp(SystemSharp.SysDOM.UnOp.Kind.BoolNot, g);
        }

        /// <summary>
        /// Constructs a bit-wise expression complement generator.
        /// </summary>
        /// <param name="g">operand generator</param>
        public static Generation operator ~(Generation g)
        {
            return UnOp(SystemSharp.SysDOM.UnOp.Kind.BitwiseNot, g);
        }

        /// <summary>
        /// Constructs an expression addition generator.
        /// </summary>
        /// <param name="g1">left operand generator</param>
        /// <param name="g2">right operand generator</param>
        public static Generation operator +(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Add, g1, g2);
        }

        /// <summary>
        /// Constructs an expression subtraction generator.
        /// </summary>
        /// <param name="g1">left operand generator</param>
        /// <param name="g2">right operand generator</param>
        public static Generation operator -(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Sub, g1, g2);
        }

        /// <summary>
        /// Constructs an expression multiplication generator.
        /// </summary>
        /// <param name="g1">left operand generator</param>
        /// <param name="g2">right operand generator</param>
        public static Generation operator *(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Mul, g1, g2);
        }

        /// <summary>
        /// Constructs an expression division generator.
        /// </summary>
        /// <param name="g1">left operand generator</param>
        /// <param name="g2">right operand generator</param>
        public static Generation operator /(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Div, g1, g2);
        }

        /// <summary>
        /// Constructs an expression division remainder generator.
        /// </summary>
        /// <param name="g1">left operand generator</param>
        /// <param name="g2">right operand generator</param>
        public static Generation operator %(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Rem, g1, g2);
        }
    }
}

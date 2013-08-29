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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Algebraic;
using SystemSharp.Meta;

namespace SystemSharp.SysDOM
{
    public delegate void MatchAction(Expression e);
    public delegate Expression ExpressionGenerator();

    public class Matching
    {
        private Expression.MatchFunction _func;
        private ExpressionGenerator _gen;
        private Expression _expr;

        public Matching()
        {
            _func = x => true;
            _gen = GetExpression;
        }

        public Expression Result
        {
            get { return _expr; }
        }

        private void SetExpression(Expression e)
        {
            _expr = e;
        }

        public Expression GetExpression()
        {
            return _expr;
        }

        private bool Match(Expression e)
        {
            SetExpression(e);
            return _func(e);
        }

        public static implicit operator Expression.MatchFunction(Matching m)
        {
            return m.Match;
        }

        public static implicit operator Matching(Expression.MatchFunction mf)
        {
            return new Matching() { _func = mf };
        }

        public static implicit operator Matching(Expression e)
        {
            Matching result = Node(e);
            result._gen = () => e;
            return result;
        }

        public static implicit operator Generation(Matching m)
        {
            return m._gen;
        }

        public static Expression.MatchFunction Node(Expression e)
        {
            return x => e.NodeEquals(x);
        }

        public static Expression.MatchFunction Deep(Expression e)
        {
            return x => e.DeepEquals(x);
        }

        private bool MatchAny(Expression e, MatchAction ma)
        {
            ma(e);
            return true;
        }

        public static Expression.MatchFunction Parent2Children(Expression.MatchFunction fnp,
            Expression.MatchFunction fnc1, Expression.MatchFunction fnc2)
        {
            return x => fnp(x) && fnc1(x.Children.ElementAt(0)) && fnc2(x.Children.ElementAt(1));
        }

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

        public Matching Constrain(Func<bool> constraint)
        {
            Matching result = new Matching();
            result._func = e => this.Match(e) && constraint();
            result._gen = this._gen;
            return result;
        }

        public static Expression.MatchFunction BinOp(BinOp.Kind kind)
        {
            return Node(new BinOp() { Operation = kind });
        }

        public static Matching operator -(Matching fn)
        {
            return fn.MUnOp(UnOp.Kind.Neg);
        }

        public static Matching operator !(Matching fn)
        {
            return fn.MUnOp(UnOp.Kind.BoolNot);
        }

        public static Matching operator ~(Matching fn)
        {
            return fn.MUnOp(UnOp.Kind.BitwiseNot);
        }

        public static Matching operator +(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Add, fn2);
        }

        public static Matching operator -(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Sub, fn2);
        }

        public static Matching operator *(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Mul, fn2);
        }

        public static Matching operator /(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Div, fn2);
        }

        public static Matching operator %(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Rem, fn2);
        }

        public static Matching operator &(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.And, fn2);
        }

        public static Matching operator |(Matching fn1, Matching fn2)
        {
            return fn1.MBinOp(SystemSharp.SysDOM.BinOp.Kind.Or, fn2);
        }

        public Matching Eq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.Eq, y);
        }

        public Matching NEq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.NEq, y);
        }

        public Matching Lt(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.Lt, y);
        }

        public Matching LtEq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.LtEq, y);
        }

        public Matching Gt(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.Gt, y);
        }

        public Matching GtEq(Matching y)
        {
            return MBinOp(SystemSharp.SysDOM.BinOp.Kind.GtEq, y);
        }

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

    public class Generation
    {
        public ExpressionGenerator Generator { get; set; }

        public static implicit operator ExpressionGenerator(Generation gen)
        {
            return gen.Generator;
        }

        public static implicit operator Generation(ExpressionGenerator g)
        {
            return new Generation() { Generator = g };
        }

        public static ExpressionGenerator Copy(Expression e)
        {
            return () => e;
        }

        public static ExpressionGenerator UnOp(UnOp.Kind kind, ExpressionGenerator g)
        {
            return () => new UnOp()
            {
                Operation = kind,
                Operand = g()
            };
        }

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

        public static Generation operator -(Generation g)
        {
            return UnOp(SystemSharp.SysDOM.UnOp.Kind.Neg, g);
        }

        public static Generation operator !(Generation g)
        {
            return UnOp(SystemSharp.SysDOM.UnOp.Kind.BoolNot, g);
        }

        public static Generation operator ~(Generation g)
        {
            return UnOp(SystemSharp.SysDOM.UnOp.Kind.BitwiseNot, g);
        }

        public static Generation operator +(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Add, g1, g2);
        }

        public static Generation operator -(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Sub, g1, g2);
        }

        public static Generation operator *(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Mul, g1, g2);
        }

        public static Generation operator /(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Div, g1, g2);
        }

        public static Generation operator %(Generation g1, Generation g2)
        {
            return BinOp(SystemSharp.SysDOM.BinOp.Kind.Rem, g1, g2);
        }
    }
}

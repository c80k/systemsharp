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
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM.Eval;

namespace SystemSharp.SysDOM
{

    /// <summary>
    /// This static class provides simplification rules for expressions.
    /// </summary>
    public static class SimplificationRules
    {
        private static bool IsZero(Expression e)
        {
            return e.IsConst() &&
                object.Equals(
                    TypeConversions.ConvertValue(
                        e.Eval(DefaultEvaluator.DefaultConstEvaluator),
                        typeof(double)), 0.0);
        }

        private static bool IsOne(Expression e)
        {
            return e.IsConst() &&
                object.Equals(
                    TypeConversions.ConvertValue(
                        e.Eval(DefaultEvaluator.DefaultConstEvaluator),
                        typeof(double)), 1.0);
        }

        private static bool IsMOne(Expression e)
        {
            return e.IsConst() &&
                object.Equals(
                    TypeConversions.ConvertValue(
                        e.Eval(DefaultEvaluator.DefaultConstEvaluator),
                        typeof(double)), -1.0);
        }

        private static bool HasZeroRange(Expression e)
        {
            if (e.ResultType.Rank == 0)
                return false;

            return e.ResultType.Constraints.Any(_ => _.Size == 0);
        }

        /// <summary>
        /// Matches any constant zero-valued expression.
        /// </summary>
        public static readonly Matching MatchZero = (Matching)IsZero;

        /// <summary>
        /// Matches any constant one-valued expression.
        /// </summary>
        public static readonly Matching MatchOne = (Matching)IsOne;

        /// <summary>
        /// Matches any constant expression of value -1.
        /// </summary>
        public static readonly Matching MatchMOne = (Matching)IsMOne;

        /// <summary>
        /// Matches any expression having an empty result type.
        /// </summary>
        public static readonly Matching MatchZeroRange = (Matching)HasZeroRange;

        /// <summary>
        /// Returns a rule that replaces --x with x.
        /// </summary>
        public static ReplacementRule ElimMultiMinus
        {
            get
            {
                Matching a = new Matching();
                return new ReplacementRule(-(-a), a);
            }
        }

        /// <summary>
        /// Returns a rule that replaces ~~x with x.
        /// </summary>
        public static ReplacementRule ElimMultiBitNot
        {
            get
            {
                Matching a = new Matching();
                return new ReplacementRule(~~a, a);
            }
        }

        /// <summary>
        /// Returns a rule that replaces !!x with x.
        /// </summary>
        public static ReplacementRule ElimMultiBoolNot
        {
            get
            {
                Matching a = new Matching();
                return new ReplacementRule(!!a, a);
            }
        }

        /// <summary>
        /// Returns a rule that replaces 1*x with x.
        /// </summary>
        public static ReplacementRule ElimOneTimes
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchOne * x, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x*1 with x.
        /// </summary>
        public static ReplacementRule ElimTimesOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x * MatchOne, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces (-1)*x with -x.
        /// </summary>
        public static ReplacementRule ElimMOneTimes
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchMOne * x, -x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x*(-1) with -x.
        /// </summary>
        public static ReplacementRule ElimTimesMOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x * MatchMOne, -x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces 0*x with 0.
        /// </summary>
        public static ReplacementRule ElimZeroTimes
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero * x, MatchZero);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x*0 with 0.
        /// </summary>
        public static ReplacementRule ElimTimesZero
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x * MatchZero, MatchZero);
            }
        }

        /// <summary>
        /// Returns a rule that replaces 0/x with 0.
        /// </summary>
        public static ReplacementRule ElimZeroDiv
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero / x, MatchZero);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x/1 with x.
        /// </summary>
        public static ReplacementRule ElimDivOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x / MatchOne, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x/(-1) with -x.
        /// </summary>
        public static ReplacementRule ElimDivMOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x / -MatchOne, -x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces 0+x with x.
        /// </summary>
        public static ReplacementRule ElimZeroPlus
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero + x, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x+0 with x.
        /// </summary>
        public static ReplacementRule ElimPlusZero
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x + MatchZero, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces 0-x with -x.
        /// </summary>
        public static ReplacementRule ElimZeroMinus
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero - x, -x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x-0 with x.
        /// </summary>
        public static ReplacementRule ElimMinusZero
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x - MatchZero, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x-(-y) with x+y.
        /// </summary>
        public static ReplacementRule ElimMinusNeg
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(x - (-y), x + y);
            }
        }

        /// <summary>
        /// Returns a rule that replaces -0 with 0.
        /// </summary>
        public static ReplacementRule ElimSignedZero
        {
            get
            {
                return new ReplacementRule(-MatchZero, MatchZero);
            }
        }

        /// <summary>
        /// Returns a rule that replaces -(x*y) with (-x)*y.
        /// </summary>
        public static ReplacementRule RwNegProd
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(-(x * y), -x * y);
            }
        }

        /// <summary>
        /// Returns a rule that replaces -(x+y) with x-y.
        /// </summary>
        public static ReplacementRule RwNegSum
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(-(x + y), y - x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x==y) with x!=y.
        /// </summary>
        public static ReplacementRule NotEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.Eq(y), x.NEq(y));
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x!=y) with x==y.
        /// </summary>
        public static ReplacementRule NotNEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.NEq(y), x.Eq(y));
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x&lt;y) with x&gt;=y.
        /// </summary>
        public static ReplacementRule NotLt
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.Lt(y), x.GtEq(y));
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x&lt;=y) with x&gt;y.
        /// </summary>
        public static ReplacementRule NotLtEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.LtEq(y), x.Gt(y));
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x&gt;y) with x&lt;=y.
        /// </summary>
        public static ReplacementRule NotGt
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.Gt(y), x.LtEq(y));
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x&gt;=y) with x&lt;y.
        /// </summary>
        public static ReplacementRule NotGtEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.GtEq(y), x.Lt(y));
            }
        }

        /// <summary>
        /// Returns a rule that replaces any constant expression which evaluates to <c>true</c>
        /// with <c>SpecialConstant.True</c>.
        /// </summary>
        public static ReplacementRule TrueLit
        {
            get
            {
                Matching trueLit = LiteralReference.CreateConstant(true);
                Matching trueSC = SpecialConstant.True;
                return new ReplacementRule(trueLit, trueSC);
            }
        }

        /// <summary>
        /// Returns a rule that replaces any constant expression which evaluates to <c>false</c>
        /// with <c>SpecialConstant.False</c>.
        /// </summary>
        public static ReplacementRule FalseLit
        {
            get
            {
                Matching falseLit = LiteralReference.CreateConstant(false);
                Matching falseSC = SpecialConstant.False;
                return new ReplacementRule(falseLit, falseSC);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x == true with x.
        /// </summary>
        public static ReplacementRule EqTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x.Eq(_true), x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x != true with !x.
        /// </summary>
        public static ReplacementRule NotEqTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x.NEq(_true), !x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x == false with !x.
        /// </summary>
        public static ReplacementRule EqFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x.Eq(_false), !x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x != false with x.
        /// </summary>
        public static ReplacementRule NotEqFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x.NEq(_false), x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces <c>x == StdLogic._1</c> with <c>(bool)x</c>.
        /// </summary>
        public static ReplacementRule EqSLTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = LiteralReference.CreateConstant(StdLogic._1);
                return new ReplacementRule(x.Eq(_true), x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        /// <summary>
        /// Returns a rule that replaces <c>x == StdLogic._0</c> with <c>!((bool)x)</c>.
        /// </summary>
        public static ReplacementRule EqSLFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = LiteralReference.CreateConstant(StdLogic._0);
                return new ReplacementRule(x.Eq(_false), !x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        /// <summary>
        /// Returns a rule that replaces <c>x != StdLogic._1</c> with <c>!((bool)x)</c>.
        /// </summary>
        public static ReplacementRule NEqSLTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = LiteralReference.CreateConstant(StdLogic._1);
                return new ReplacementRule(x.NEq(_true), !x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        /// <summary>
        /// Returns a rule that replaces <c>x != StdLogic._0</c> with <c>(bool)x</c>.
        /// </summary>
        public static ReplacementRule NEqSLFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = LiteralReference.CreateConstant(StdLogic._0);
                return new ReplacementRule(x.NEq(_false), x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        /// <summary>
        /// Returns a rule that replaces x & true with x.
        /// </summary>
        public static ReplacementRule AndTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x & _true, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces true & x with x.
        /// </summary>
        public static ReplacementRule TrueAnd
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(_true & x, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x & false with false.
        /// </summary>
        public static ReplacementRule AndFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x & _false, _false);
            }
        }

        /// <summary>
        /// Returns a rule that replaces false & x with false.
        /// </summary>
        public static ReplacementRule FalseAnd
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(_false & x, _false);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x | false with x.
        /// </summary>
        public static ReplacementRule OrFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x | _false, x);
            }
        }

        /// <summary>
        /// Returns a rule that repalces false | x with x.
        /// </summary>
        public static ReplacementRule FalseOr
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(_false | x, x);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x | true with true.
        /// </summary>
        public static ReplacementRule OrTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x | _true, _true);
            }
        }

        /// <summary>
        /// Returns a rule that replaces true | x with true.
        /// </summary>
        public static ReplacementRule TrueOr
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(_true | x, _true);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x | (x & y) with x.
        /// </summary>
        public static ReplacementRule Absorption1
        {
            get
            {
                Matching x1 = new Matching();
                Matching x2 = new Matching();
                Matching y = new Matching();
                return new ReplacementRule((x1 | (x2 & y))
                    .Constrain(() => x1.GetExpression().Equals(x2.GetExpression())), x1);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x & (x | y) with x.
        /// </summary>
        public static ReplacementRule Absorption2
        {
            get
            {
                Matching x1 = new Matching();
                Matching x2 = new Matching();
                Matching y = new Matching();
                return new ReplacementRule((x1 & (x2 | y))
                    .Constrain(() => x1.GetExpression().Equals(x2.GetExpression())), x1);
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x|y) with (!x)&(!y).
        /// </summary>
        public static ReplacementRule DeMorgan1
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!(x | y), !x & !y);
            }
        }

        /// <summary>
        /// Returns a rule that replaces !(x&y) with (!x)|(!y).
        /// </summary>
        public static ReplacementRule DeMorgan2
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!(x & y), !x | !y);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x|((!x)&y) with x|y.
        /// </summary>
        public static ReplacementRule BoolMisc1
        {
            get
            {
                Matching x1 = new Matching();
                Matching x2 = new Matching();
                Matching y = new Matching();
                return new ReplacementRule((x1 | (!x2 & y))
                    .Constrain(() => x1.GetExpression().Equals(x2.GetExpression())), x1 | y);
            }
        }

        /// <summary>
        /// Returns a rule that replaces x&(!x|y) with x&y.
        /// </summary>
        public static ReplacementRule BoolMisc2
        {
            get
            {
                Matching x1 = new Matching();
                Matching x2 = new Matching();
                Matching y = new Matching();
                return new ReplacementRule((x1 & (!x2 | y))
                    .Constrain(() => x1.GetExpression().Equals(x2.GetExpression())), x1 & y);
            }
        }

        /// <summary>
        /// Returns a rule that replaces (x&y)|(x&!y) with x.
        /// </summary>
        public static ReplacementRule BoolMisc3
        {
            get
            {
                Matching x1 = new Matching();
                Matching x2 = new Matching();
                Matching y1 = new Matching();
                Matching y2 = new Matching();
                return new ReplacementRule(((x1 & y1) | (x2 & !y2))
                    .Constrain(() =>
                        x1.GetExpression().Equals(x2.GetExpression()) &&
                        y1.GetExpression().Equals(y2.GetExpression())),
                        x1);
            }
        }

        /// <summary>
        /// Returns a rule that replaces (x|y)&(x|!y) with x.
        /// </summary>
        public static ReplacementRule BoolMisc4
        {
            get
            {
                Matching x1 = new Matching();
                Matching x2 = new Matching();
                Matching y1 = new Matching();
                Matching y2 = new Matching();
                return new ReplacementRule(((x1 | y1) & (x2 | !y2))
                    .Constrain(() =>
                        x1.GetExpression().Equals(x2.GetExpression()) &&
                        y1.GetExpression().Equals(y2.GetExpression())),
                        x1);
            }
        }

        /// <summary>
        /// Returns a rule that eliminates concatenations with zero-sized vectors (zero-sized vector at left).
        /// </summary>
        public static ReplacementRule ElimConcatWithZeroRanged1
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(
                    x.MBinOp(BinOp.Kind.Concat, MatchZeroRange),
                    x);
            }
        }

        /// <summary>
        /// Returns a rule that eliminates concatenations with zero-sized vectors (zero-sized vector at right).
        /// </summary>
        public static ReplacementRule ElimConcatWithZeroRanged2
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(
                    MatchZeroRange.MBinOp(BinOp.Kind.Concat, x),
                    x);
            }
        }

        /// <summary>
        /// Tries to simplify the expression by applying a catalogue of simplifying replacement rules.
        /// </summary>
        /// <param name="e">expression to simplify</param>
        /// <returns>simplified expression</returns>
        public static Expression Simplify(this Expression e)
        {
            ReplacementRule[] rules = new ReplacementRule[]
            {
                ElimMultiMinus,
                ElimMultiBitNot,
                ElimMultiBoolNot,
                ElimOneTimes, ElimTimesOne,
                ElimMOneTimes, ElimTimesMOne,
                ElimZeroTimes, ElimTimesZero,
                ElimZeroDiv,
                ElimDivOne, ElimDivMOne,
                ElimZeroPlus, ElimPlusZero,
                ElimZeroMinus, ElimMinusZero, ElimMinusNeg,
                ElimSignedZero,
                RwNegProd, RwNegSum,
                TrueLit, FalseLit,
                NotEq, NotNEq, NotLt, NotLtEq, NotGt, NotGtEq,
                EqTrue, NotEqTrue, EqFalse, NotEqFalse,
                //EqSLTrue, NEqSLTrue, EqSLFalse, NEqSLFalse,
                AndTrue, TrueAnd, AndFalse, FalseAnd,
                OrTrue, TrueOr, OrFalse, FalseOr,
                Absorption1, Absorption2, DeMorgan1, DeMorgan2,
                BoolMisc1, BoolMisc2, BoolMisc3, BoolMisc4
                //,ElimConcatWithZeroRanged1, ElimConcatWithZeroRanged2                 
            };
            bool hit;
            do
            {
                hit = false;
                foreach (ReplacementRule rule in rules)
                {
                    bool localHit;
                    e = rule.ApplyRepeatedly(e, out localHit);
                    if (localHit)
                        hit = true;
                }
            } while (hit);
            return e;
        }

        /// <summary>
        /// Tries to simplify the expression by applying replacement rules for multi-valued logic.
        /// </summary>
        /// <param name="e">expression to simplify</param>
        /// <returns>simplified expression</returns>
        public static Expression SimplifyMultiValuedLogic(this Expression e)
        {
            ReplacementRule[] rules = new ReplacementRule[]
            {
                EqSLTrue, NEqSLTrue, EqSLFalse, NEqSLFalse
            };
            bool hit;
            do
            {
                hit = false;
                foreach (ReplacementRule rule in rules)
                {
                    bool localHit;
                    e = rule.ApplyRepeatedly(e, out localHit);
                    if (localHit)
                        hit = true;
                }
            } while (hit);
            return e;
        }

        /// <summary>
        /// Substitutes each occurence of expression <paramref name="x"/> with expression <paramref name="y"/>.
        /// </summary>
        public static Expression Substitute(this Expression e, Expression x, Expression y)
        {
            ReplacementRule rr = new ReplacementRule(te => te.DeepEquals(x), () => y);
            bool hit;
            Expression result = rr.ApplyOnce(e, out hit);
            return result;
        }

        /// <summary>
        /// Tries to simplify each matrix element by applying a catalogue of simplification rules.
        /// </summary>
        /// <param name="m">matrix to simplify</param>
        /// <returns>simplified matrix</returns>
        public static Matrix Simplify(this Matrix m)
        {
            Matrix mnew = new Matrix(m.NumRows, m.NumCols);
            foreach (Matrix.Entry elem in m.Elements)
            {
                mnew[elem.Row, elem.Col] = elem.Value.Simplify();
            }
            return mnew;
        }
    }

    /// <summary>
    /// An expression visitor for determining whether a given expression is constant.
    /// </summary>
    class ExpressionConstPredicate : IExpressionVisitor<bool>
    {
        /// <summary>
        /// Specifies the semantics of the passed set of objects.
        /// </summary>
        public enum EMode
        {
            /// <summary>
            /// The set contains all variable literals.
            /// </summary>
            GivenVariables,

            /// <summary>
            /// The set contains all constant literals.
            /// </summary>
            GivenConstants
        }

        private HashSet<object> _variables;
        private EMode _mode;

        /// <summary>
        /// Constructs the visitor.
        /// </summary>
        /// <param name="variables">a set of objects</param>
        /// <param name="mode">whether the passed set of objects contains all constants or all variables</param>
        public ExpressionConstPredicate(HashSet<object> variables, EMode mode)
        {
            _variables = variables;
            _mode = mode;
        }

        #region IExpressionVisitor<bool> Members

        public bool TransformLiteralReference(LiteralReference expr)
        {
            if (!(expr.ReferencedObject is IStorable))
                return true;
            bool flag = _variables.Contains(expr.ReferencedObject);
            if (_mode == EMode.GivenConstants)
                return flag;
            else
                return !flag;
        }

        public bool TransformSpecialConstant(SpecialConstant expr)
        {
            return true;
        }

        public bool TransformUnOp(UnOp expr)
        {
            return expr.Operand.Accept(this);
        }

        public bool TransformBinOp(BinOp expr)
        {
            return expr.Operand1.Accept(this) && expr.Operand2.Accept(this);
        }

        public bool TransformFunction(FunctionCall expr)
        {
            foreach (Expression arg in expr.Arguments)
                if (!arg.Accept(this))
                    return false;
            return true;
        }

        public bool TransformTernOp(TernOp expr)
        {
            return expr.Operands[0].Accept(this) &&
                expr.Operands[1].Accept(this) &&
                expr.Operands[2].Accept(this);
        }

        #endregion
    }

    class ExpressionConstRules : IExpressionVisitor<bool>
    {
        public enum ERuleMode
        {
            ContinueIfTrue,
            ContinueIfFalse,
            FinalRule
        }

        private struct Rule
        {
            public ERuleMode mode;
            public Func<object, bool> pred;
        }

        private List<Rule> _rules = new List<Rule>();

        public bool AssumeFunctionCallsAreConstant { get; set; }

        public ExpressionConstRules()
        {
        }

        public void AddRule(Func<object, bool> pred, ERuleMode mode)
        {
            _rules.Add(new Rule() { mode = mode, pred = pred });
        }

        public void CompleteByAssumingConst()
        {
            AddRule(x => true, ERuleMode.FinalRule);
        }

        public void CompleteByAssumingNonConst()
        {
            AddRule(x => false, ERuleMode.FinalRule);
        }

        #region IExpressionVisitor<bool> Members

        public bool TransformLiteralReference(LiteralReference expr)
        {
            foreach (Rule rule in _rules)
            {
                bool flag = rule.pred(expr.ReferencedObject);
                switch (rule.mode)
                {
                    case ERuleMode.ContinueIfFalse:
                        if (flag)
                            return true;
                        break;

                    case ERuleMode.ContinueIfTrue:
                        if (!flag)
                            return false;
                        break;

                    default:
                        return flag;
                }
            }
            throw new InvalidOperationException("No rule to determine whether " + expr.ToString() + " is const");
        }

        public bool TransformSpecialConstant(SpecialConstant expr)
        {
            return true;
        }

        public bool TransformUnOp(UnOp expr)
        {
            return expr.Operand.Accept(this);
        }

        public bool TransformBinOp(BinOp expr)
        {
            return expr.Operand1.Accept(this) && expr.Operand2.Accept(this);
        }

        public bool TransformFunction(FunctionCall expr)
        {
            if (!AssumeFunctionCallsAreConstant)
                return false;

            foreach (Expression arg in expr.Arguments)
                if (!arg.Accept(this))
                    return false;
            return true;
        }

        public bool TransformTernOp(TernOp expr)
        {
            return expr.Operands[0].Accept(this) &&
                expr.Operands[1].Accept(this) &&
                expr.Operands[2].Accept(this);
        }

        #endregion
    }

    class ConstantFolder : IExpressionTransformer
    {
        public Expression TransformLiteralReference(LiteralReference expr)
        {
            return expr;
        }

        public Expression TransformSpecialConstant(SpecialConstant expr)
        {
            return expr;
        }

        public Expression TransformUnOp(UnOp expr)
        {
            if (expr.IsConst())
            {
                object result = expr.Eval(new DefaultEvaluator());
                return LiteralReference.CreateConstant(result);
            }
            else
                return expr;
        }

        public Expression TransformBinOp(BinOp expr)
        {
            if (expr.IsConst())
            {
                object result = expr.Eval(new DefaultEvaluator());
                return LiteralReference.CreateConstant(result);
            }
            else
                return expr;
        }

        public Expression TransformTernOp(TernOp expr)
        {
            if (expr.IsConst())
            {
                object result = expr.Eval(new DefaultEvaluator());
                return LiteralReference.CreateConstant(result);
            }
            else
                return expr;
        }

        public Expression TransformFunction(FunctionCall expr)
        {
            Expression[] newArgs = new Expression[expr.Arguments.Length];
            for (int i = 0; i < newArgs.Length; i++)
                newArgs[i] = expr.Arguments[i].Transform(this);
            return expr.CloneThis(newArgs);
        }
    }

    class DerivativeBuilder : IExpressionTransformer
    {
        private LiteralReference _derVar;

        public DerivativeBuilder(LiteralReference derVar)
        {
            _derVar = derVar;
        }

        private Expression der(Expression x)
        {
            return x.Transform(this);
        }

        public Expression TransformLiteralReference(LiteralReference expr)
        {
            if (expr.Equals(_derVar))
                return SpecialConstant.ScalarOne;
            else
                return SpecialConstant.ScalarZero;
        }

        public Expression TransformSpecialConstant(SpecialConstant expr)
        {
            return SpecialConstant.ScalarZero;
        }

        public Expression TransformUnOp(UnOp x)
        {
            Expression dx0 = der(x.Operand);
            switch (x.Operation)
            {
                case UnOp.Kind.Abs:
                    return Expression.Conditional(
                        Expression.LessThan(x, SpecialConstant.ScalarZero),
                        -dx0, dx0);

                case UnOp.Kind.BitwiseNot:
                case UnOp.Kind.BoolNot:
                    throw new NotImplementedException();

                case UnOp.Kind.Exp:
                    return dx0 * x;

                case UnOp.Kind.ExtendSign:
                    throw new NotImplementedException();

                case UnOp.Kind.Identity:
                    return x;

                case UnOp.Kind.Log:
                    return dx0 / x;

                case UnOp.Kind.Neg:
                    return -dx0;

                default:
                    throw new NotImplementedException();
            }
        }

        public Expression TransformBinOp(BinOp x)
        {
            Expression dx0 = der(x.Operand1);
            Expression dx1 = der(x.Operand2);
            switch (x.Operation)
            {
                case BinOp.Kind.Add:
                    return dx0 + dx1;

                case BinOp.Kind.And:
                case BinOp.Kind.Concat:
                    throw new NotImplementedException();

                case BinOp.Kind.Div:
                    return (dx0 * x.Operand2 - x.Operand1 * dx1) / (x.Operand2 * x.Operand2);

                case BinOp.Kind.Eq:
                    throw new NotImplementedException();

                case BinOp.Kind.Exp:
                    return x * (dx1 * Expression.Log(x.Operand1) + dx0 * x.Operand2 / x.Operand1);

                case BinOp.Kind.Gt:
                case BinOp.Kind.GtEq:
                    throw new NotImplementedException();

                case BinOp.Kind.Log:
                    throw new NotImplementedException();

                case BinOp.Kind.LShift:
                case BinOp.Kind.Lt:
                case BinOp.Kind.LtEq:
                    throw new NotImplementedException();

                case BinOp.Kind.Mul:
                    return dx0 * x.Operand2 + dx1 * x.Operand1;

                case BinOp.Kind.NEq:
                case BinOp.Kind.Or:
                case BinOp.Kind.Rem:
                case BinOp.Kind.RShift:
                    throw new NotImplementedException();

                case BinOp.Kind.Sub:
                    return dx0 - dx1;

                case BinOp.Kind.Xor:
                    throw new NotImplementedException();

                default:
                    throw new NotImplementedException();
            }
        }

        public Expression TransformTernOp(TernOp x)
        {
            switch (x.Operation)
            {
                case TernOp.Kind.Conditional:
                    {
                        Expression d1 = der(x.Operands[1]).Simplify();
                        Expression d2 = der(x.Operands[2]).Simplify();
                        if (d1.Equals(d2))
                            return d1;
                        else
                            return Expression.Conditional(
                                x.Operands[0], d1, d2);
                    }

                case TernOp.Kind.Slice:
                    throw new NotImplementedException();

                default:
                    throw new NotImplementedException();
            }
        }

        public Expression TransformFunction(FunctionCall expr)
        {
            if (expr.Callee is IntrinsicFunction)
            {
                IntrinsicFunction ifun = (IntrinsicFunction)expr.Callee;
                switch (ifun.Action)
                {
                    case IntrinsicFunction.EAction.Sin:
                        {
                            Expression x = expr.Arguments[0];
                            FunctionCall f = IntrinsicFunctions.Cos(x);
                            return f * der(x);
                        }

                    case IntrinsicFunction.EAction.Cos:
                        {
                            Expression x = expr.Arguments[0];
                            FunctionCall f = IntrinsicFunctions.Sin(x);
                            return -f * der(x);
                        }

                    case IntrinsicFunction.EAction.Sqrt:
                        {
                            return der(expr.Arguments[0]) / expr;
                        }

                    case IntrinsicFunction.EAction.Sign:
                        return SpecialConstant.ScalarZero;

                    case IntrinsicFunction.EAction.GetArrayElement:
                        throw new ArgumentException("Expression is not differentiatable");

                    default:
                        throw new NotImplementedException();
                }
            }
            else
                throw new NotImplementedException("Only intrinsic functions can be derived");
        }
    }

    /// <summary>
    /// This static class provides extension methods which simplify the usage of expressions.
    /// </summary>
    public static class ExpressionExtensions
    {
#if false
        public static bool IsConst(this Expression e, HashSet<object> variables, ExpressionConstPredicate.EMode mode)
        {
            ExpressionConstPredicate pred = new ExpressionConstPredicate(variables, mode);
            return e.Accept(pred);
        }
#endif

        /// <summary>
        /// Returns <c>true</c> iff the expression has a constant value, assuming all literals to be variable.
        /// </summary>
        public static bool IsConst(this Expression e)
        {
            ExpressionConstPredicate pred = new ExpressionConstPredicate(new HashSet<object>(),
                ExpressionConstPredicate.EMode.GivenConstants);
            return e.Accept(pred);
        }

        internal static bool IsConst(this Expression e, ExpressionConstRules rules)
        {
            return e.Accept(rules);
        }

        /// <summary>
        /// Extracts all literal references from the expression.
        /// </summary>
        public static LiteralReference[] ExtractLiteralReferences(this Expression e)
        {
            LiteralReferenceExtractor lre = new LiteralReferenceExtractor();
            e.Match(lre.Match);
            return lre.Results;
        }

        internal static Expression Derive(this Expression e, LiteralReference derVar)
        {
            return e.Transform(new DerivativeBuilder(derVar));
        }

        internal static Expression FoldConstants(this Expression e)
        {
            return e.Transform(new ConstantFolder());
        }
    }

    internal class LiteralReferenceExtractor
    {
        private List<LiteralReference> _results = new List<LiteralReference>();

        public bool Match(Expression e)
        {
            if (e is LiteralReference)
            {
                LiteralReference lr = (LiteralReference)e;
                if (!_results.Contains(lr))
                    _results.Add(lr);
            }
            return false;
        }

        public LiteralReference[] Results
        {
            get
            {
                return _results.ToArray();
            }
        }
    }
}

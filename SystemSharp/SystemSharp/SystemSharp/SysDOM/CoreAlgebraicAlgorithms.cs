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

        public static readonly Matching MatchZero = (Matching)IsZero;
        public static readonly Matching MatchOne = (Matching)IsOne;
        public static readonly Matching MatchMOne = (Matching)IsMOne;
        public static readonly Matching MatchZeroRange = (Matching)HasZeroRange;

        public static ReplacementRule ElimMultiMinus
        {
            get
            {
                Matching a = new Matching();
                return new ReplacementRule(-(-a), a);
            }
        }

        public static ReplacementRule ElimMultiBitNot
        {
            get
            {
                Matching a = new Matching();
                return new ReplacementRule(~~a, a);
            }
        }

        public static ReplacementRule ElimMultiBoolNot
        {
            get
            {
                Matching a = new Matching();
                return new ReplacementRule(!!a, a);
            }
        }

        public static ReplacementRule ElimOneTimes
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchOne * x, x);
            }
        }

        public static ReplacementRule ElimTimesOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x * MatchOne, x);
            }
        }

        public static ReplacementRule ElimMOneTimes
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchMOne * x, -x);
            }
        }

        public static ReplacementRule ElimTimesMOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x * MatchMOne, -x);
            }
        }

        public static ReplacementRule ElimZeroTimes
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero * x, MatchZero);
            }
        }

        public static ReplacementRule ElimTimesZero
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x * MatchZero, MatchZero);
            }
        }

        public static ReplacementRule ElimZeroDiv
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero / x, MatchZero);
            }
        }

        public static ReplacementRule ElimDivOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x / MatchOne, x);
            }
        }

        public static ReplacementRule ElimDivMOne
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x / -MatchOne, -x);
            }
        }

        public static ReplacementRule ElimZeroPlus
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero + x, x);
            }
        }

        public static ReplacementRule ElimPlusZero
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x + MatchZero, x);
            }
        }

        public static ReplacementRule ElimZeroMinus
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(MatchZero - x, -x);
            }
        }

        public static ReplacementRule ElimMinusZero
        {
            get
            {
                Matching x = new Matching();
                return new ReplacementRule(x - MatchZero, x);
            }
        }

        public static ReplacementRule ElimMinusNeg
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(x - (-y), x + y);
            }
        }

        public static ReplacementRule ElimSignedZero
        {
            get
            {
                return new ReplacementRule(-MatchZero, MatchZero);
            }
        }

        public static ReplacementRule RwNegProd
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(-(x * y), -x * y);
            }
        }

        public static ReplacementRule RwNegSum
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(-(x + y), y - x);
            }
        }

        public static ReplacementRule NotEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.Eq(y), x.NEq(y));
            }
        }

        public static ReplacementRule NotNEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.NEq(y), x.Eq(y));
            }
        }

        public static ReplacementRule NotLt
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.Lt(y), x.GtEq(y));
            }
        }

        public static ReplacementRule NotLtEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.LtEq(y), x.Gt(y));
            }
        }

        public static ReplacementRule NotGt
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.Gt(y), x.LtEq(y));
            }
        }

        public static ReplacementRule NotGtEq
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!x.GtEq(y), x.Lt(y));
            }
        }

        public static ReplacementRule TrueLit
        {
            get
            {
                Matching trueLit = LiteralReference.CreateConstant(true);
                Matching trueSC = SpecialConstant.True;
                return new ReplacementRule(trueLit, trueSC);
            }
        }

        public static ReplacementRule FalseLit
        {
            get
            {
                Matching falseLit = LiteralReference.CreateConstant(false);
                Matching falseSC = SpecialConstant.False;
                return new ReplacementRule(falseLit, falseSC);
            }
        }

        public static ReplacementRule EqTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x.Eq(_true), x);
            }
        }

        public static ReplacementRule NotEqTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x.NEq(_true), !x);
            }
        }

        public static ReplacementRule EqFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x.Eq(_false), !x);
            }
        }

        public static ReplacementRule NotEqFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x.NEq(_false), x);
            }
        }

        public static ReplacementRule EqSLTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = LiteralReference.CreateConstant(StdLogic._1);
                return new ReplacementRule(x.Eq(_true), x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        public static ReplacementRule EqSLFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = LiteralReference.CreateConstant(StdLogic._0);
                return new ReplacementRule(x.Eq(_false), !x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        public static ReplacementRule NEqSLTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = LiteralReference.CreateConstant(StdLogic._1);
                return new ReplacementRule(x.NEq(_true), !x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        public static ReplacementRule NEqSLFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = LiteralReference.CreateConstant(StdLogic._0);
                return new ReplacementRule(x.NEq(_false), x.Cast(typeof(StdLogic), typeof(bool)));
            }
        }

        public static ReplacementRule AndTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x & _true, x);
            }
        }

        public static ReplacementRule TrueAnd
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(_true & x, x);
            }
        }

        public static ReplacementRule AndFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x & _false, _false);
            }
        }

        public static ReplacementRule FalseAnd
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(_false & x, _false);
            }
        }

        public static ReplacementRule OrFalse
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(x | _false, x);
            }
        }

        public static ReplacementRule FalseOr
        {
            get
            {
                Matching x = new Matching();
                Matching _false = SpecialConstant.False;
                return new ReplacementRule(_false | x, x);
            }
        }

        public static ReplacementRule OrTrue
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(x | _true, _true);
            }
        }

        public static ReplacementRule TrueOr
        {
            get
            {
                Matching x = new Matching();
                Matching _true = SpecialConstant.True;
                return new ReplacementRule(_true | x, _true);
            }
        }

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

        public static ReplacementRule DeMorgan1
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!(x | y), !x & !y);
            }
        }

        public static ReplacementRule DeMorgan2
        {
            get
            {
                Matching x = new Matching();
                Matching y = new Matching();
                return new ReplacementRule(!(x & y), !x | !y);
            }
        }

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

        public static Expression Substitute(this Expression e, Expression x, Expression y)
        {
            ReplacementRule rr = new ReplacementRule(te => te.DeepEquals(x), () => y);
            bool hit;
            Expression result = rr.ApplyOnce(e, out hit);
            return result;
        }

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

    public class ExpressionConstPredicate : IExpressionVisitor<bool>
    {
        public enum EMode
        {
            GivenVariables,
            GivenConstants
        }

        private HashSet<object> _variables;
        private EMode _mode;

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

    public class ExpressionConstRules : IExpressionVisitor<bool>
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

    public class ConstantFolder : IExpressionTransformer
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

    public class DerivativeBuilder : IExpressionTransformer
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

    public static class ExpressionExtensions
    {
        public static bool IsConst(this Expression e, HashSet<object> variables, ExpressionConstPredicate.EMode mode)
        {
            ExpressionConstPredicate pred = new ExpressionConstPredicate(variables, mode);
            return e.Accept(pred);
        }

        public static bool IsConst(this Expression e)
        {
            ExpressionConstPredicate pred = new ExpressionConstPredicate(new HashSet<object>(),
                ExpressionConstPredicate.EMode.GivenConstants);
            return e.Accept(pred);
        }

        public static bool IsConst(this Expression e, ExpressionConstRules rules)
        {
            return e.Accept(rules);
        }

        public static LiteralReference[] ExtractLiteralReferences(this Expression e)
        {
            LiteralReferenceExtractor lre = new LiteralReferenceExtractor();
            e.Match(lre.Match);
            return lre.Results;
        }

        public static Expression Derive(this Expression e, LiteralReference derVar)
        {
            return e.Transform(new DerivativeBuilder(derVar));
        }

        public static Expression FoldConstants(this Expression e)
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

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
using System.Runtime.CompilerServices;
using System.Text;
using SystemSharp.Algebraic;
using SystemSharp.Analysis;
using SystemSharp.Assembler;
using SystemSharp.Common;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.SysDOM
{
    /// <summary>
    /// Evaluator interface for expressions.
    /// </summary>
    public interface IEvaluator
    {
        /// <summary>
        /// Evaluates a constant literal.
        /// </summary>
        /// <param name="constant">constant literal</param>
        /// <returns>literal value</returns>
        object EvalConstant(Constant constant);

        /// <summary>
        /// Evaluates a variable literal.
        /// </summary>
        /// <param name="variable">variable literal</param>
        /// <returns>variable value</returns>
        object EvalVariable(Variable variable);

        /// <summary>
        /// Evaluates a signal reference.
        /// </summary>
        /// <param name="signalRef">signal reference</param>
        /// <returns>value of referenced signal property</returns>
        object EvalSignalRef(SignalRef signalRef);

        /// <summary>
        /// Evaluates a field reference.
        /// </summary>
        /// <param name="fieldRef">field reference</param>
        /// <returns>field value</returns>
        object EvalFieldRef(FieldRef fieldRef);

        /// <summary>
        /// Evaluates the "this" reference.
        /// </summary>
        /// <param name="thisRef">"this" reference</param>
        /// <returns>current instance</returns>
        object EvalThisRef(ThisRef thisRef);

        /// <summary>
        /// Evaluates an array reference.
        /// </summary>
        /// <param name="arrayRef">array reference</param>
        /// <returns>value of referenced array element</returns>
        object EvalArrayRef(ArrayRef arrayRef);

        /// <summary>
        /// Evaluates a literal.
        /// </summary>
        /// <param name="lit">literal</param>
        /// <returns>literal value</returns>
        object EvalLiteral(ILiteral lit);

        /// <summary>
        /// Evaluates a function call.
        /// </summary>
        /// <param name="funcref">called function</param>
        /// <param name="args">function arguments</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>function return value</returns>
        object EvalFunction(FunctionCall funcref, object[] args, TypeDescriptor resultType);

        /// <summary>
        /// Negates a value.
        /// </summary>
        /// <param name="v">value</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>negated value</returns>
        object Neg(object v, TypeDescriptor resultType);

        /// <summary>
        /// Computes the boolean complement of a value.
        /// </summary>
        /// <param name="v">value</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>boolean complement of value</returns>
        object BoolNot(object v, TypeDescriptor resultType);

        /// <summary>
        /// Computes the bitwise complement of a value.
        /// </summary>
        /// <param name="v">value</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>bitwise complement of the value</returns>
        object BitwiseNot(object v, TypeDescriptor resultType);

        /// <summary>
        /// Adds two values.
        /// </summary>
        /// <param name="v1">first summand</param>
        /// <param name="v2">second summand</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the sum</returns>
        object Add(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Subtracts two values.
        /// </summary>
        /// <param name="v1">value</param>
        /// <param name="v2">subtrahend</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the difference</returns>
        object Sub(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Multiplies two values.
        /// </summary>
        /// <param name="v1">first multiplicand</param>
        /// <param name="v2">second multiplicand</param>
        /// <param name="resultType"></param>
        /// <returns>the product</returns>
        object Mul(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Divides two values.
        /// </summary>
        /// <param name="v1">dividend</param>
        /// <param name="v2">divisor</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the quotient</returns>
        object Div(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Computes the division remainder of two values.
        /// </summary>
        /// <param name="v1">dividend</param>
        /// <param name="v2">divisor</param>
        /// <param name="resultType"></param>
        /// <returns>the remainder</returns>
        object Rem(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Computes the boolean or bit-wise conjunction of two values, depending on their types.
        /// </summary>
        /// <param name="v1">first value</param>
        /// <param name="v2">second value</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>boolean or bit-wise conjunction</returns>
        object And(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Computes the boolean or bit-wise disjunction of two values, depending on their types.
        /// </summary>
        /// <param name="v1">first value</param>
        /// <param name="v2">second value</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>boolean or bit-wise disjunction</returns>
        object Or(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Computes the boolean or bit-wise anti-valence of two values, depending on their types.
        /// </summary>
        /// <param name="v1">first value</param>
        /// <param name="v2">second value</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>boolean or bit-wise anti-valence</returns>
        object Xor(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Shifts a value logically to the left.
        /// </summary>
        /// <param name="v1">value to shift</param>
        /// <param name="v2">bit count to shift</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the shift result</returns>
        object LShift(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Shifts a value logically or arithmetically to the right, depending on whether it is of unsigned or signed type.
        /// </summary>
        /// <param name="v1">value to shift</param>
        /// <param name="v2">bit count to shift</param>
        /// <param name="resultType"></param>
        /// <returns>the shift result</returns>
        object RShift(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Concatenates two values.
        /// </summary>
        /// <param name="v1">"upper" part</param>
        /// <param name="v2">"lower" part</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>concatenated value</returns>
        object Concat(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Computes e^x.
        /// </summary>
        /// <param name="v">x</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>e to the power of x</returns>
        object Exp(object v, TypeDescriptor resultType);

        /// <summary>
        /// Computes x^y.
        /// </summary>
        /// <param name="v1">x</param>
        /// <param name="v2">y</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>x to the power of y</returns>
        object Exp(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Computes the natural logarithm of x.
        /// </summary>
        /// <param name="v">x</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the natural logarithm</returns>
        object Log(object v, TypeDescriptor resultType);

        /// <summary>
        /// Computes the logarithm of x to base b.
        /// </summary>
        /// <param name="v1">x</param>
        /// <param name="v2">b</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the logarithm</returns>
        object Log(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Computes the absolute value of x.
        /// </summary>
        /// <param name="v">x</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the absolute value of x</returns>
        object Abs(object v, TypeDescriptor resultType);

        /// <summary>
        /// Computes sin(x).
        /// </summary>
        /// <param name="v">x</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the sine</returns>
        object Sin(object v, TypeDescriptor resultType);

        /// <summary>
        /// Computes cos(x).
        /// </summary>
        /// <param name="v">x</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>the cosine</returns>
        object Cos(object v, TypeDescriptor resultType);

        /// <summary>
        /// Makes a value in two's complement representation wider, i.e. pads zeroes or ones to the left, depending on the sign.
        /// </summary>
        /// <param name="v">value</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>sign extension</returns>
        object ExtendSign(object v, TypeDescriptor resultType);

        /// <summary>
        /// Returns the fundamental constant e.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>e</returns>
        object E(TypeDescriptor resultType);

        /// <summary>
        /// Returns the fundamental constant Pi.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>Pi</returns>
        object PI(TypeDescriptor resultType);

        /// <summary>
        /// Returns the value of 1.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the value of 1 in expected result type</returns>
        object ScalarOne(TypeDescriptor resultType);

        /// <summary>
        /// Returns the value of 0.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the value of 0 in expected result type</returns>
        object ScalarZero(TypeDescriptor resultType);

        /// <summary>
        /// Returns the "true" constant.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the "true" constant in expected result type</returns>
        object True(TypeDescriptor resultType);

        /// <summary>
        /// Returns the "false" constant.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the "false" constant in expected result type</returns>
        object False(TypeDescriptor resultType);

        /// <summary>
        /// Performs a slice operation.
        /// </summary>
        /// <param name="v">vector value to slice</param>
        /// <param name="lo">lower slice index</param>
        /// <param name="hi">upper slice index</param>
        /// <param name="resultType">expected result type</param>
        /// <returns>slice result</returns>
        object Slice(object v, object lo, object hi, TypeDescriptor resultType);

        [Obsolete("Do not use.")]
        object Time(TypeDescriptor resultType);

        /// <summary>
        /// Returns a "true" representation if <paramref name="v1"/> is less than <paramref name="v2"/>, other wise a "false" representation.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the comparison result</returns>
        object IsLessThan(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Returns a "true" representation if <paramref name="v1"/> is less than or equal to <paramref name="v2"/>, other wise a "false" representation.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the comparison result</returns>
        object IsLessThanOrEqual(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Returns a "true" representation if <paramref name="v1"/> equals <paramref name="v2"/>, other wise a "false" representation.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the comparison result</returns>
        object IsEqual(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Returns a "true" representation if <paramref name="v1"/> is not equal to <paramref name="v2"/>, other wise a "false" representation.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the comparison result</returns>
        object IsNotEqual(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Returns a "true" representation if <paramref name="v1"/> is greater than or equal to <paramref name="v2"/>, other wise a "false" representation.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the comparison result</returns>
        object IsGreaterThanOrEqual(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Returns a "true" representation if <paramref name="v1"/> is greater than <paramref name="v2"/>, other wise a "false" representation.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the comparison result</returns>
        object IsGreaterThan(object v1, object v2, TypeDescriptor resultType);

        /// <summary>
        /// Returns <paramref name="thn"/> if <paramref name="cond"/> represents "true", otherwise <paramref name="els"/>.
        /// </summary>
        /// <param name="resultType">expected result type</param>
        /// <returns>the combination result</returns>
        object ConditionallyCombine(object cond, object thn, object els, TypeDescriptor resultType);
    }

    /// <summary>
    /// Anything that supports evaluation by an evaluator.
    /// </summary>
    public interface IEvaluable
    {
        /// <summary>
        /// Evaluates this symbol.
        /// </summary>
        /// <param name="eval">evaluator to use</param>
        /// <returns>evaluation result</returns>
        object Eval(IEvaluator eval);
    }

    #region Stringification

    /// <summary>
    /// Associativity classification of infix operators.
    /// </summary>
    public enum EOperatorAssociativity
    {
        /// <summary>
        /// The operator is left associative, thus evaluated from left to right
        /// </summary>
        LeftAssociative,

        /// <summary>
        /// The operator is left associative, thus evaluated from right to left
        /// </summary>
        RightAssociative,

        /// <summary>
        /// Associativity between operators of same predence is undefined and must be indicated using parentheses
        /// </summary>
        UseParenthesis
    }

    /// <summary>
    /// Service interface to decide the operator precedence and associativity in a particular concrete syntax.
    /// </summary>
    public interface IOperatorPrecedence
    {
        /// <summary>
        /// Returns the precendence order of a unary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>precedence order</returns>
        int GetOperatorOrder(UnOp.Kind op);

        /// <summary>
        /// Returns the precendence order of a binary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>precedence order</returns>
        int GetOperatorOrder(BinOp.Kind op);

        /// <summary>
        /// Returns the precendence order of a ternary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>precedence order</returns>
        int GetOperatorOrder(TernOp.Kind op);

        /// <summary>
        /// Returns the associativity of a unary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>associativity</returns>
        EOperatorAssociativity GetOperatorAssociativity(UnOp.Kind op);

        /// <summary>
        /// Returns the associativity of a binary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>associativity</returns>
        EOperatorAssociativity GetOperatorAssociativity(BinOp.Kind op);

        /// <summary>
        /// Returns the associativity of a ternary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>associativity</returns>
        EOperatorAssociativity GetOperatorAssociativity(TernOp.Kind op);
    }

    /// <summary>
    /// Produces a textual representation of a particular operator with given arguments in a particular conrete syntax.
    /// </summary>
    /// <param name="args">operator arguments</param>
    /// <returns>notated operator</returns>
    public delegate string NotateFunc(params string[] args);

    /// <summary>
    /// Produces a textual representation of a function call with given arguments in a particular conrete syntax.
    /// </summary>
    /// <param name="callee">called function</param>
    /// <param name="args">function arguments</param>
    /// <returns>notated function call</returns>
    public delegate string FunctionNotateFunc(ICallable callee, params string[] args);

    /// <summary>
    /// Produces a textual representation of a literal in a particular concrete syntax.
    /// </summary>
    /// <param name="literal">literal to notate</param>
    /// <param name="mode">describes how the literal is referenced</param>
    /// <returns>notated literal</returns>
    public delegate string LiteralNotateFunc(ILiteral literal, LiteralReference.EMode mode);

    /// <summary>
    /// Notates parentheses in a particular concrete syntax around and argument to disambiguate precedence among expression.
    /// </summary>
    /// <param name="arg">argument</param>
    /// <returns>argument in parentheses</returns>
    public delegate string BracketNotateFunc(string arg);

    /// <summary>
    /// This static class provides some default notators for expression stringification.
    /// </summary>
    public static class DefaultNotators
    {
        /// <summary>
        /// Notates a literal by returning its name. In case of reference by address, the name is surrounded by @{...}.
        /// </summary>
        /// <param name="literal">literal to notate</param>
        /// <param name="mode">describes how the literal is referenced</param>
        /// <returns>the default literal notation</returns>
        public static string LiteralName(ILiteral literal, LiteralReference.EMode mode)
        {
            var name = literal.ToString();
            if (mode == LiteralReference.EMode.ByAddress)
                name = "@{" + name + "}";
            return name;
        }

        /// <summary>
        /// Puts <paramref name="expr"/> between round parentheses.
        /// </summary>
        public static string Bracket(string expr)
        {
            return "(" + expr + ")";
        }

        /// <summary>
        /// Produces a prefix notation by concatenating <paramref name="symbol"/> with the first element of <paramref name="args"/>.
        /// </summary>
        public static string NotatePrefix(string symbol, params string[] args)
        {
            return symbol + args[0];
        }

        /// <summary>
        /// Returns a prefix-style notator, using the specified symbol as prefix.
        /// </summary>
        /// <param name="symbol">symbol to use as prefix</param>
        /// <returns>prefix notator</returns>
        public static NotateFunc Prefix(string symbol)
        {
            return (string[] args) => NotatePrefix(symbol, args);
        }

        /// <summary>
        /// Produces an infix notation by concatenating <c>args[0]</c> with <paramref name="symbol"/> and then <c>args[1]</c>.
        /// </summary>
        /// <param name="symbol">infix symbol</param>
        /// <param name="args">infix operator arguments</param>
        public static string NotateInfix(string symbol, params string[] args)
        {
            return args[0] + " " + symbol + " " + args[1];
        }

        /// <summary>
        /// Returns an infix-style notator, using the specified symbol as infix.
        /// </summary>
        /// <param name="symbol">symbol to use as infix</param>
        /// <returns>infix notator</returns>
        public static NotateFunc Infix(string symbol)
        {
            return (string[] args) => NotateInfix(symbol, args);
        }

        /// <summary>
        /// Notates a function call, using the default pattern <c>symbol '(' args[0], args[1], ... ')' </c>.
        /// </summary>
        /// <param name="symbol">function name</param>
        /// <param name="args">function arguments</param>
        /// <returns>the notated function call</returns>
        public static string NotateFunction(string symbol, params string[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(symbol);
            sb.Append("(");
            bool first = true;
            foreach (string arg in args)
            {
                if (first)
                    first = false;
                else
                    sb.Append(", ");
                sb.Append(arg);
            }
            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// Notates a function call, using the default pattern <c>callee.Name '(' args[0], args[1], ... ')' </c>.
        /// </summary>
        public static string NotateFunctionCall(ICallable callee, params string[] args)
        {
            return NotateFunction(callee.Name, args);
        }

        /// <summary>
        /// Returns a function call notator for the specified function name.
        /// </summary>
        /// <param name="symbol">function name</param>
        /// <returns>the default function call notator</returns>
        public static NotateFunc Function(string symbol)
        {
            return (string[] args) => NotateFunction(symbol, args);
        }
    }

    /// <summary>
    /// Service interface for operator stringification in a particular concrete syntax.
    /// </summary>
    public interface IOperatorNotation
    {
        /// <summary>
        /// Returns a notator for a particular unary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>the notator</returns>
        NotateFunc GetNotation(UnOp.Kind op);

        /// <summary>
        /// Returns a notator for a particular binary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>the notator</returns>
        NotateFunc GetNotation(BinOp.Kind op);

        /// <summary>
        /// Returns a notator for a particular ternary operation.
        /// </summary>
        /// <param name="op">kind of operation</param>
        /// <returns>the notator</returns>
        NotateFunc GetNotation(TernOp.Kind op);

        /// <summary>
        /// Returns a notator for function calls.
        /// </summary>
        /// <returns>the function call notator</returns>
        FunctionNotateFunc GetFunctionNotation();

        /// <summary>
        /// Returns the symbol of a special constant.
        /// </summary>
        /// <param name="constant">which special constant</param>
        /// <returns>special constant symbol</returns>
        string GetSpecialConstantSymbol(SpecialConstant.Kind constant);

        /// <summary>
        /// Returns a notator for literals.
        /// </summary>
        /// <returns>the literal notator</returns>
        LiteralNotateFunc GetLiteralNotation();

        /// <summary>
        /// Returns a notator for putting expressions between parentheses to disambiguate precedence.
        /// </summary>
        /// <returns>the notator</returns>
        BracketNotateFunc GetBracketNotation();
    }

    /// <summary>
    /// Service interface for expression stringification.
    /// </summary>
    public interface IStringifyInfo
    {
        /// <summary>
        /// Returns the operator precedence service.
        /// </summary>
        IOperatorPrecedence Precedence { get; }

        /// <summary>
        /// Returns the operator notation service.
        /// </summary>
        IOperatorNotation Notation { get; }

        /// <summary>
        /// Returns an action to be invoked whenever an expression is stringified.
        /// </summary>
        Action<Expression> OnStringifyExpression { get; }
    }

    #endregion

    #region DefaultStringification

    /// <summary>
    /// Operator precedence service implementation for the C# language.
    /// </summary>
    public class CSharpOperatorPrecedence : IOperatorPrecedence
    {
        public int GetOperatorOrder(UnOp.Kind op)
        {
            switch (op)
            {
                case UnOp.Kind.Neg: return 1;
                case UnOp.Kind.BoolNot: return 1;
                case UnOp.Kind.BitwiseNot: return 1;
                case UnOp.Kind.Log: return -1;
                case UnOp.Kind.Exp: return -1;
                case UnOp.Kind.Abs: return -1;
                case UnOp.Kind.ExtendSign: return -1;
                case UnOp.Kind.Sin: return -1;
                case UnOp.Kind.Cos: return -1;
                default: throw new NotImplementedException();
            }
        }

        public int GetOperatorOrder(BinOp.Kind op)
        {
            switch (op)
            {
                case BinOp.Kind.Add: return 3;
                case BinOp.Kind.And: return 7;
                case BinOp.Kind.Div: return 2;
                case BinOp.Kind.Exp: return 0;
                case BinOp.Kind.Concat:
                case BinOp.Kind.Log: return -1;
                case BinOp.Kind.Mul: return 2;
                case BinOp.Kind.Or: return 9;
                case BinOp.Kind.Rem: return 2;
                case BinOp.Kind.LShift: return 4;
                case BinOp.Kind.RShift: return 4;
                case BinOp.Kind.Sub: return 3;
                case BinOp.Kind.Xor: return 8;
                case BinOp.Kind.Eq:
                case BinOp.Kind.NEq:
                case BinOp.Kind.Lt:
                case BinOp.Kind.LtEq:
                case BinOp.Kind.Gt:
                case BinOp.Kind.GtEq: return 5;
                default: throw new NotImplementedException();
            }
        }

        public int GetOperatorOrder(TernOp.Kind op)
        {
            switch (op)
            {
                case TernOp.Kind.Slice: return 0; // although not present in C#, treat like array access x[i]
                case TernOp.Kind.Conditional: return 10;
                default: throw new NotImplementedException();
            }
        }

        public EOperatorAssociativity GetOperatorAssociativity(UnOp.Kind op)
        {
            return EOperatorAssociativity.RightAssociative;
        }

        public EOperatorAssociativity GetOperatorAssociativity(BinOp.Kind op)
        {
            return EOperatorAssociativity.LeftAssociative;
        }

        public EOperatorAssociativity GetOperatorAssociativity(TernOp.Kind op)
        {
            return EOperatorAssociativity.RightAssociative;
        }
    
    #endregion

        public static readonly CSharpOperatorPrecedence Instance = new CSharpOperatorPrecedence();
    }

    /// <summary>
    /// Operator notation service implementation for the C# language.
    /// </summary>
    public class CSharpOperatorNotation : IOperatorNotation
    {
        public NotateFunc GetNotation(UnOp.Kind op)
        {
            switch (op)
            {
                case UnOp.Kind.Neg: return DefaultNotators.Prefix("-");
                case UnOp.Kind.BoolNot: return DefaultNotators.Prefix("!");
                case UnOp.Kind.BitwiseNot: return DefaultNotators.Prefix("~");
                case UnOp.Kind.Exp: return DefaultNotators.Function("Math.Exp");
                case UnOp.Kind.Log: return DefaultNotators.Function("Math.Log");
                case UnOp.Kind.Abs: return DefaultNotators.Function("Math.Abs");
                case UnOp.Kind.ExtendSign: return DefaultNotators.Function("SystemSharp.Runtime.MathSupport.ExtendSign");
                case UnOp.Kind.Sin: return DefaultNotators.Function("Math.Sin");
                case UnOp.Kind.Cos: return DefaultNotators.Function("Math.Cos");
                default: throw new NotImplementedException();
            }
        }

        public NotateFunc GetNotation(BinOp.Kind op)
        {
            switch (op)
            {
                case BinOp.Kind.Add: return DefaultNotators.Infix("+");
                case BinOp.Kind.And: return DefaultNotators.Infix("&");
                case BinOp.Kind.Div: return DefaultNotators.Infix("/");
                case BinOp.Kind.Exp: return DefaultNotators.Function("Math.Pow");
                case BinOp.Kind.Log: return DefaultNotators.Function("Math.Log");
                case BinOp.Kind.Mul: return DefaultNotators.Infix("*");
                case BinOp.Kind.Or: return DefaultNotators.Infix("|");
                case BinOp.Kind.Rem: return DefaultNotators.Infix("%");
                case BinOp.Kind.LShift: return DefaultNotators.Infix("<<");
                case BinOp.Kind.RShift: return DefaultNotators.Infix(">>");
                case BinOp.Kind.Sub: return DefaultNotators.Infix("-");
                case BinOp.Kind.Xor: return DefaultNotators.Infix("^");
                case BinOp.Kind.Eq: return DefaultNotators.Infix("==");
                case BinOp.Kind.NEq: return DefaultNotators.Infix("!=");
                case BinOp.Kind.Lt: return DefaultNotators.Infix("<");
                case BinOp.Kind.LtEq: return DefaultNotators.Infix("<=");
                case BinOp.Kind.Gt: return DefaultNotators.Infix(">");
                case BinOp.Kind.GtEq: return DefaultNotators.Infix(">=");
                case BinOp.Kind.Concat: return DefaultNotators.Function("ObjectHelpers.Concat");
                default: throw new NotImplementedException();
            }
        }

        public string GetSpecialConstantSymbol(SpecialConstant.Kind constant)
        {
            switch (constant)
            {
                case SpecialConstant.Kind.E: return "Math.E";
                case SpecialConstant.Kind.PI: return "Math.PI";
                case SpecialConstant.Kind.ScalarOne: return "1.0";
                case SpecialConstant.Kind.ScalarZero: return "0.0";
                case SpecialConstant.Kind.True: return "true";
                case SpecialConstant.Kind.False: return "false";
                default: throw new NotImplementedException();
            }
        }

        public LiteralNotateFunc GetLiteralNotation()
        {
            return DefaultNotators.LiteralName;
        }

        public BracketNotateFunc GetBracketNotation()
        {
            return DefaultNotators.Bracket;
        }

        public NotateFunc GetNotation(TernOp.Kind op)
        {
            switch (op)
            {
                case TernOp.Kind.Conditional:
                    return (string[] args) => args[0] + " ? " + args[1] + " : " + args[2];

                case TernOp.Kind.Slice:
                    return DefaultNotators.Function("SystemSharp.Runtime.BitOps.Slice");

                default:
                    throw new NotImplementedException();
            }
        }

        public FunctionNotateFunc GetFunctionNotation()
        {
            return DefaultNotators.NotateFunctionCall;
        }

        public static readonly CSharpOperatorNotation Instance = new CSharpOperatorNotation();
    }

    /// <summary>
    /// Stringification service implementation for the C# language.
    /// </summary>
    public class CSharpStringifyInfo : IStringifyInfo
    {
        public IOperatorPrecedence Precedence { get { return CSharpOperatorPrecedence.Instance; } }
        public IOperatorNotation Notation { get { return CSharpOperatorNotation.Instance; } }
        public Action<Expression> OnStringifyExpression { get; set; }
        public static readonly CSharpStringifyInfo Instance = new CSharpStringifyInfo();
    }

    /// <summary>
    /// Common interface for all expressions.
    /// </summary>
    public interface IExpression
    {
        /// <summary>
        /// Evaluates this expression.
        /// </summary>
        /// <param name="eval">evaluator to use</param>
        /// <returns>expression value</returns>
        object Eval(IEvaluator eval);
    }

    /// <summary>
    /// A coarse classification of expression result types.
    /// </summary>
    public enum EResultTypeClass
    {
        /// <summary>
        /// An algebraic quantity, i.e. a conceptually continuous number.
        /// </summary>
        Algebraic,

        /// <summary>
        /// An integral quantity.
        /// </summary>
        Integral,

        /// <summary>
        /// A boolean quantity.
        /// </summary>
        Boolean,

        /// <summary>
        /// An object.
        /// </summary>
        ObjectReference,

        Unknown
    }

    /// <summary>
    /// This static class provides a service to convert CLI types to their result type classification.
    /// </summary>
    public static class ResultTypeClasses
    {
        /// <summary>
        /// Retrieves the result type classification of <paramref name="type"/>.
        /// </summary>
        public static EResultTypeClass FromType(Type type)
        {
            if (type.Equals(typeof(int)) || type.Equals(typeof(long)))
                return EResultTypeClass.Integral;
            else if (type.Equals(typeof(bool)))
                return EResultTypeClass.Boolean;
            else if (type.Equals(typeof(float)) || type.Equals(typeof(double)))
                return EResultTypeClass.Algebraic;
            else
                return EResultTypeClass.ObjectReference;
        }
    }

    /// <summary>
    /// Common interface of objects that supported attached attributes.
    /// </summary>
    [ContractClass(typeof(AttributedContractClass))]
    public interface IAttributed
    {
        /// <summary>
        /// Attaches an attribute to this instance. The object type is used to identify the attribute.
        /// </summary>
        /// <param name="attr">attribute to attach</param>
        void AddAttribute(object attr);

        /// <summary>
        /// Removes an attribute from this instance.
        /// </summary>
        /// <typeparam name="T">type of attribute, which is used to identify it</typeparam>
        /// <returns><c>true</c> if such an attribute was found, <c>false</c> if not</returns>
        bool RemoveAttribute<T>();

        /// <summary>
        /// Retrieves an attribute from this instance.
        /// </summary>
        /// <typeparam name="T">type of attribute, which is used to identify it</typeparam>
        /// <returns>the retrieved attribute</returns>
        T QueryAttribute<T>();

        /// <summary>
        /// Tells whether this instance has a specific attribute.
        /// </summary>
        /// <typeparam name="T">type of attribute, which is used to identify it</typeparam>
        /// <returns><c>true</c> if such attribute exists, <c>false</c> if not</returns>
        bool HasAttribute<T>();

        /// <summary>
        /// Enumerates all attributes of this instance.
        /// </summary>
        IEnumerable<object> Attributes { get; }

        /// <summary>
        /// Copies all attributes from another instance.
        /// </summary>
        /// <param name="other">instance to copy attributes from</param>
        void CopyAttributesFrom(IAttributed other);
    }

    [ContractClassFor(typeof(IAttributed))]
    abstract class AttributedContractClass: IAttributed
    {
        public void AddAttribute(object attr)
        {
            Contract.Requires<ArgumentNullException>(attr != null);
        }

        public bool RemoveAttribute<T>()
        {
            // There seems to be a ccrewrite bug which makes the following lines fail if commented in.
            // See http://social.msdn.microsoft.com/Forums/en-US/codecontracts/thread/f5f206b0-b472-4512-838f-523af6250581

            //Contract.Ensures(!HasAttribute<T>());
            //Contract.Ensures(Contract.OldValue(HasAttribute<T>()) == Contract.Result<bool>());
            return false;
        }

        public T QueryAttribute<T>()
        {
            return default(T);
        }

        public bool HasAttribute<T>()
        {
            return false;
        }

        public IEnumerable<object> Attributes
        {
            get 
            {
                Contract.Ensures(Contract.Result<IEnumerable<object>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public void CopyAttributesFrom(IAttributed other)
        {
            Contract.Requires<ArgumentNullException>(other != null);
        }
    }

    /// <summary>
    /// Provides a default implementation of the <c>IAttributed</c> interface.
    /// </summary>
    public class AttributedObject: 
        IAttributed
    {
        private Dictionary<Type, object> _attributes;

        public void AddAttribute(object attr)
        {
            if (_attributes == null)
                _attributes = new Dictionary<Type, object>();
            _attributes[attr.GetType()] = attr;
        }

        public bool RemoveAttribute<T>()
        {
            if (_attributes == null)
                return false;

            return _attributes.Remove(typeof(T));
        }

        public object QueryAttribute(Type type)
        {
            if (_attributes == null)
                return null;

            object result;
            if (_attributes.TryGetValue(type, out result))
                return result;

            return _attributes.Values.Where(v => type.IsAssignableFrom(v.GetType())).SingleOrDefault();
        }

        public T QueryAttribute<T>()
        {
            return (T)QueryAttribute(typeof(T));
        }

        public bool HasAttribute<T>()
        {
            return _attributes != null &&
                _attributes.Values.Any(v => v is T);
        }

        public IEnumerable<object> Attributes
        {
            get
            {
                if (_attributes == null)
                    return Enumerable.Empty<object>();
                else
                    return _attributes.Values;
            }
        }

        public void CopyAttributesFrom(IAttributed other)
        {
            foreach (var attr in other.Attributes)
                AddAttribute(attr);
        }
    }

    /// <summary>
    /// An expression.
    /// </summary>
    public abstract class Expression : 
        AttributedObject,
        IExpression
    {
        /// <summary>
        /// Matches an expression for some preficate.
        /// </summary>
        /// <param name="e">expression to match</param>
        /// <returns><c>true</c> if the expression matches the predicate, <c>false</c> if not</returns>
        public delegate bool MatchFunction(Expression e);

        /// <summary>
        /// Callback action to call whenever an expression is evaluated.
        /// </summary>
        /// <param name="expr">evaluated expression</param>
        /// <param name="value">expression value</param>
        public delegate void OnExpressionEvaluatedFn(Expression expr, object value);

        /// <summary>
        /// Evaluates this expression.
        /// </summary>
        /// <param name="eval">evaluator to use</param>
        /// <param name="efn">callback action to call whenever a (sub-)expression is evaluated</param>
        /// <returns>the value of this expression</returns>
        public abstract object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn);

        /// <summary>
        /// Evaluates this expression.
        /// </summary>
        /// <param name="eval">evaluator to use</param>
        /// <returns>the value of this expression</returns>
        public object Eval(IEvaluator eval)
        {
            return Eval(eval, (e, v) => { });
        }

        /// <summary>
        /// Computes a textual representation of this expression, using the specified stringification service.
        /// </summary>
        /// <param name="info">stringification service to use</param>
        /// <returns>textual representation of this expression</returns>
        public virtual string ToString(IStringifyInfo info)
        {
            if (info.OnStringifyExpression != null)
                info.OnStringifyExpression(this);

            return null;
        }

        public override string ToString()
        {
            return ToString(CSharpStringifyInfo.Instance);
        }

        private Expression[] _children;

        /// <summary>
        /// Returns the precedence order of this expression, using the specified precedence order service.
        /// </summary>
        /// <param name="prec">precedence order service to use</param>
        /// <returns>the precedence order of this expression</returns>
        public abstract int GetPrecedence(IOperatorPrecedence prec);

        /// <summary>
        /// Returns the number of operands.
        /// </summary>
        public virtual int Arity
        {
            get { return _children.Length; }
            protected set
            {
                _children = new Expression[value];
            }
        }
        
        /// <summary>
        /// Returns the expression operands.
        /// </summary>
        public virtual Expression[] Children
        {
            get { return _children; }
        }

        /// <summary>
        /// Clones this expression, using the supplied operands.
        /// </summary>
        /// <param name="newChildren">new epxression operands</param>
        /// <returns>a clone of this expression</returns>
        protected abstract Expression CloneThisImpl(Expression[] newChildren);

        /// <summary>
        /// Clones this expression, using the supplied operands.
        /// </summary>
        /// <param name="newChildren">new epxression operands</param>
        /// <returns>a clone of this expression</returns>
        public Expression CloneThis(Expression[] newChildren)
        {
            var result = CloneThisImpl(newChildren);
            result.CopyAttributesFrom(this);
            return result;
        }

        /// <summary>
        /// Returns a clone of this expression.
        /// </summary>
        public Expression Clone
        {
            get
            {
                Expression[] newChildren = new Expression[Arity];
                for (int i = 0; i < Arity; i++)
                    newChildren[i] = Children[i].Clone;
                return CloneThisImpl(newChildren);
            }
        }

        /// <summary>
        /// Accepts an expression visitor.
        /// </summary>
        /// <typeparam name="ResultType">visitor result type</typeparam>
        /// <param name="vtor">visitor to accept</param>
        /// <returns>result returned from visitor</returns>
        public abstract ResultType Accept<ResultType>(IExpressionVisitor<ResultType> vtor);

        /// <summary>
        /// Returns <c>true</c>, iff this expression has the same semantics like the supplied expression, without considering any operand.
        /// </summary>
        /// <param name="e">expression to compare to</param>
        public abstract bool NodeEquals(Expression e);

        /// <summary>
        /// Returns <c>true</c>, iff this expression has the same semantics like the supplied expression, considering the complete structure.
        /// </summary>
        /// <param name="e">expression to compare to</param>
        public abstract bool DeepEquals(Expression e);

        /// <summary>
        /// Returns the result type classification of this expression.
        /// </summary>
        public abstract EResultTypeClass ResultTypeClass { get; }

        /// <summary>
        /// Accepts an expression transformation visitor.
        /// </summary>
        /// <param name="xform">transformation visitor</param>
        /// <returns>the transformed expression</returns>
        public virtual Expression Transform(IExpressionTransformer xform)
        {
            return Accept(xform);
        }

        /// <summary>
        /// Searches and returns an expression that matches the given predicate, considering this expression
        /// and recursively all operands.
        /// </summary>
        /// <param name="fn">predicate</param>
        /// <returns>first matched expression, or <c>null</c> if no such exists</returns>
        public Expression Match(MatchFunction fn)
        {
            if (fn(this))
                return this;

            foreach (Expression e in Children)
            {
                Expression result = e.Match(fn);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Replaces all expressions tha match a given predicate with a generated expression, considering
        /// this expression and recursively all operands.
        /// </summary>
        /// <param name="fn">predicate</param>
        /// <param name="g">expression generator for matched expressions</param>
        /// <param name="hit">out parameter to receive whether any expression was matched</param>
        /// <returns>the replacement result expression</returns>
        public Expression Replace(MatchFunction fn, ExpressionGenerator g, out bool hit)
        {
            if (fn(this))
            {
                hit = true;
                return g();
            }

            if (true.Equals(Cookie))
            {
                throw new InvalidOperationException("Recursion");
            }
            Cookie = true;

            hit = false;
            Expression[] newChildren = new Expression[Arity];
            for (int i = 0; i < Children.Length; i++)
            {
                bool childHit;
                newChildren[i] = Children[i].Replace(fn, g, out childHit);
                if (childHit)
                {
                    hit = true;
                }
            }

            Cookie = null;

            return CloneThisImpl(newChildren);
        }

        /// <summary>
        /// Returns <c>true</c> if the operands of <paramref name="e"/> are equal to the operands of this
        /// expression, in terms of the <c>DeepEquals</c> method.
        /// </summary>
        /// <param name="e">expression to compare</param>
        protected bool ChildrenAreDeepEqualTo(Expression e)
        {
            Contract.Requires(e.Children != null);

            if (Children.Count() != e.Children.Count())
                return false;
            for (int i = 0; i < Children.Count(); i++)
            {
                if (!Children.ElementAt(i).DeepEquals(e.Children.ElementAt(i)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets or sets a user-defined object.
        /// </summary>
        public object Cookie { get; set; }

        protected TypeDescriptor _resultType;

        /// <summary>
        /// Returns the type descriptor of this expression evaluated.
        /// </summary>
        public virtual TypeDescriptor ResultType 
        { 
            get
            {
                if (_resultType != null)
                    return _resultType;

                switch (ResultTypeClass)
                {
                    case EResultTypeClass.Algebraic:
                        return null; //typeof(double);
                    case EResultTypeClass.Boolean:
                        return typeof(bool);
                    case EResultTypeClass.Integral:
                        return typeof(int);
                    default:
                        return null;
                }
            }
            set
            {
                if (value == null)
                    throw new ArgumentException();
                _resultType = value;
            }
        }

        /// <summary>
        /// Recursively sets of cookies to <c>null</c>.
        /// </summary>
        public void ClearCookies()
        {
            Cookie = null;
            foreach (Expression child in Children)
                child.ClearCookies();
        }

        private int _cachedHashCode;

        /// <summary>
        /// Computes a hash code for this expression.
        /// </summary>
        /// <returns>computed hash code</returns>
        protected abstract int ComputeHashCode();

        public override int GetHashCode()
        {
            if (_cachedHashCode == 0)
                _cachedHashCode = ComputeHashCode();
            return _cachedHashCode;
        }

        /// <summary>
        /// Returns the count of leaf expressions, i.e. expressions without any operands.
        /// </summary>
        public int LeafCount
        {
            get
            {
                if (Children.Length == 0)
                    return 1;
                int count = 0;
                for (int i = 0; i < Children.Length; i++)
                    count += Children[i].LeafCount;
                return count;
            }
        }

        /// <summary>
        /// Performs a consistency check of this expression.
        /// </summary>
        public void CheckConsistency()
        {
            System.Diagnostics.Debug.Assert(ResultType != null);
            System.Diagnostics.Debug.Assert(ResultTypeClass != EResultTypeClass.Unknown);
            foreach (Expression child in Children)
            {
                System.Diagnostics.Debug.Assert(child != null);
                child.CheckConsistency();
            }
        }

        public bool IsInlined { get; internal set; }

        public void Inline()
        {
            IsInlined = true;
        }

        /// <summary>
        /// Constructs an expression that represents the sum of the supplied expressions.
        /// </summary>
        /// <param name="e1">first summand</param>
        /// <param name="e2">second summand</param>
        /// <returns>the sum expression</returns>
        public static Expression operator +(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Add,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the difference of the supplied expressions.
        /// </summary>
        /// <param name="e1">first expression</param>
        /// <param name="e2">subtrahend</param>
        /// <returns>the difference expression</returns>
        public static Expression operator -(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Sub,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the product of the supplied expressions.
        /// </summary>
        /// <param name="e1">first multiplicand</param>
        /// <param name="e2">second multiplicand</param>
        /// <returns>the product expression</returns>
        public static Expression operator *(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Mul,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the quotient of the supplied expressions.
        /// </summary>
        /// <param name="e1">dividend</param>
        /// <param name="e2">divisor</param>
        /// <returns>the quotient expression</returns>
        public static Expression operator /(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Div,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the division remainder of the supplied expressions.
        /// </summary>
        /// <param name="e1">dividend</param>
        /// <param name="e2">divisor</param>
        /// <returns>the remainder expression</returns>
        public static Expression operator %(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Rem,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the logical or bit-wise conjunction of the supplied
        /// expressions, depending on their result types.
        /// </summary>
        /// <param name="e1">first expression</param>
        /// <param name="e2">second expression</param>
        /// <returns>the conjunction expression</returns>
        public static Expression operator &(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.And,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the logical or bit-wise disjunction of the supplied
        /// expressions, depending on their result types.
        /// </summary>
        /// <param name="e1">first expression</param>
        /// <param name="e2">second expression</param>
        /// <returns>the disjunction expression</returns>
        public static Expression operator |(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Or,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the logical or bit-wise anti-valence of the supplied
        /// expressions, depending on their result types.
        /// </summary>
        /// <param name="e1">first expression</param>
        /// <param name="e2">second expression</param>
        /// <returns>the anti-valence expression</returns>
        public static Expression operator ^(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Xor,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the negation of the supplied expression.
        /// </summary>
        /// <param name="e">expression to negate</param>
        /// <returns>the negation expression</returns>
        public static Expression operator -(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Neg,
                Operand = e
            };
        }

        /// <summary>
        /// Constructs an expression that represents the bit-wise complement of the supplied expression.
        /// </summary>
        /// <param name="e">expression to complement</param>
        /// <returns>the complement expression</returns>
        public static Expression operator ~(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.BitwiseNot,
                Operand = e
            };
        }

        /// <summary>
        /// Constructs an expression that represents the boolean inverse of the supplied expression.
        /// </summary>
        /// <param name="e">expression to invert</param>
        /// <returns>the inversion expression</returns>
        public static Expression operator !(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.BoolNot,
                Operand = e,
                ResultType = typeof(bool)
            };
        }

        /// <summary>
        /// Constructs an identity expression. The identity function maps any value to the same value.
        /// </summary>
        /// <param name="e">expression</param>
        /// <returns>the identity expression</returns>
        public static Expression Id(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Identity,
                Operand = e
            };
        }

        /// <summary>
        /// Constructs an expression that represents the e^x operation.
        /// </summary>
        /// <param name="x">x</param>
        /// <returns>the e^x operator expression</returns>
        public static Expression Exp(Expression x)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Exp,
                Operand = x
            };
        }

        /// <summary>
        /// Constructs an expression that represents the natural logarithm.
        /// </summary>
        /// <param name="x">x</param>
        /// <returns>the log(x) operator expression</returns>
        public static Expression Log(Expression x)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Log,
                Operand = x
            };
        }

        /// <summary>
        /// Constructs an expression that represents the absolute value function.
        /// </summary>
        /// <param name="x">x</param>
        /// <returns>the abs(x) operator expression</returns>
        public static Expression Abs(Expression x)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Abs,
                Operand = x
            };
        }

        /// <summary>
        /// Constructs an expression that represents the sign extension operator.
        /// </summary>
        /// <param name="e">operand</param>
        /// <param name="targetType">target type of sign-extended value</param>
        /// <returns>the sign extension operator expression</returns>
        public static Expression ExtendSign(Expression e, TypeDescriptor targetType)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.ExtendSign,
                Operand = e,
                ResultType = targetType
            };
        }

        /// <summary>
        /// Constructs an expression that represents the x^y operator.
        /// </summary>
        /// <param name="e1">x</param>
        /// <param name="e2">y</param>
        /// <returns>the x^y operator expression</returns>
        public static Expression Pow(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Exp,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the logarithm operator.
        /// </summary>
        /// <param name="e1">operand</param>
        /// <param name="e2">logarithm base</param>
        /// <returns>the logarithm operator expression</returns>
        public static Expression Log(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Log,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the "less than" operator
        /// </summary>
        /// <param name="e1">first operand</param>
        /// <param name="e2">second operand</param>
        /// <returns>the "less than" operator expression</returns>
        public static Expression LessThan(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Lt,
                Operand1 = e1,
                Operand2 = e2,
                ResultType = typeof(bool)
            };
        }

        /// <summary>
        /// Constructs an expression that represents the "less than or equal" operator
        /// </summary>
        /// <param name="e1">first operand</param>
        /// <param name="e2">second operand</param>
        /// <returns>the "less than or equal" operator expression</returns>
        public static Expression LessThanOrEqual(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.LtEq,
                Operand1 = e1,
                Operand2 = e2,
                ResultType = typeof(bool)
            };
        }

        /// <summary>
        /// Constructs an expression that represents the equality operator
        /// </summary>
        /// <param name="e1">first operand</param>
        /// <param name="e2">second operand</param>
        /// <returns>the equality operator expression</returns>
        public static Expression Equal(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Eq,
                Operand1 = e1,
                Operand2 = e2,
                ResultType = typeof(bool)
            };
        }

        /// <summary>
        /// Constructs an expression that represents the inequality operator
        /// </summary>
        /// <param name="e1">first operand</param>
        /// <param name="e2">second operand</param>
        /// <returns>the inequality operator expression</returns>
        public static Expression NotEqual(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.NEq,
                Operand1 = e1,
                Operand2 = e2,
                ResultType = typeof(bool)
            };
        }

        /// <summary>
        /// Constructs an expression that represents the "greater than" operator
        /// </summary>
        /// <param name="e1">first operand</param>
        /// <param name="e2">second operand</param>
        /// <returns>the "greater than" operator expression</returns>
        public static Expression GreaterThan(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Gt,
                Operand1 = e1,
                Operand2 = e2,
                ResultType = typeof(bool)
            };
        }

        /// <summary>
        /// Constructs an expression that represents the "greater than or equal" operator
        /// </summary>
        /// <param name="e1">first operand</param>
        /// <param name="e2">second operand</param>
        /// <returns>the "greater than or equal" operator expression</returns>
        public static Expression GreaterThanOrEqual(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.GtEq,
                Operand1 = e1,
                Operand2 = e2,
                ResultType = typeof(bool)
            };
        }

        /// <summary>
        /// Constructs an expression that represents the logical left shift operator.
        /// </summary>
        /// <param name="e1">shifted operand</param>
        /// <param name="e2">bit count to shift</param>
        /// <returns>the left shift operator expression</returns>
        public static Expression LShift(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.LShift,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the arithmetical or logical right shift
        /// operator, depending on whether the shift operand is signed.
        /// </summary>
        /// <param name="e1">shifted operand</param>
        /// <param name="e2">bit count to shift</param>
        /// <returns>right shift operator expression</returns>
        public static Expression RShift(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.RShift,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the concatenation operator.
        /// </summary>
        /// <param name="e1">"upper part" expression</param>
        /// <param name="e2">"lower part" expression</param>
        /// <returns>concatenation operator expression</returns>
        public static Expression Concat(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Concat,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        /// <summary>
        /// Constructs an expression that represents the concatenation operator.
        /// </summary>
        /// <param name="exprs">expressions to be concatenated, from first "upper part" to last "lower part"</param>
        /// <returns>concatenation operator expression</returns>
        public static Expression Concat(params Expression[] exprs)
        {
            if (exprs == null || exprs.Length < 2)
                throw new ArgumentException("Need at least one expression to concat");

            if (exprs.Length == 1)
                return exprs[0];

            var cur = exprs[0];
            for (int i = 1; i < exprs.Length; i++)
                cur = Concat(cur, exprs[i]);
            return cur;
        }

        /// <summary>
        /// Constructs an expression that represents the slice operator.
        /// </summary>
        /// <param name="e">operand</param>
        /// <param name="first">first slice index</param>
        /// <param name="second">second slice index</param>
        /// <returns>slice operator expression</returns>
        public static Expression Slice(Expression e, Expression first, Expression second)
        {
            TernOp result = new TernOp()
            {
                Operation = TernOp.Kind.Slice
            };
            result.Operands[0] = e;
            result.Operands[1] = first;
            result.Operands[2] = second;
            return result;
        }

        /// <summary>
        /// Constructs an expression that represents the conditional operator.
        /// </summary>
        /// <param name="cond">condition operand</param>
        /// <param name="first">operand to take if condition evaluates to "true"</param>
        /// <param name="second">operand to take if condition evaluates to "false"</param>
        /// <returns>conditional operator expression</returns>
        public static Expression Conditional(Expression cond, Expression first, Expression second)
        {
            if (!first.ResultType.Equals(second.ResultType))
                throw new ArgumentException();

            TernOp result = new TernOp()
            {
                Operation = TernOp.Kind.Conditional,
                ResultType = first.ResultType
            };
            result.Operands[0] = cond;
            result.Operands[1] = first;
            result.Operands[2] = second;
            return result;
        }

        /// <summary>
        /// Constructs an expression that represents a constant of type <c>double</c>.
        /// </summary>
        /// <param name="value">constant value</param>
        /// <returns>the constant value expression</returns>
        public static Expression Constant(double value)
        {
            if (value == 0.0)
                return SpecialConstant.ScalarZero;
            else if (value == 1.0)
                return SpecialConstant.ScalarOne;
            else
                return LiteralReference.Constant(value);
        }

        /// <summary>
        /// Constructs an expression that represents the sum if its operands.
        /// </summary>
        /// <param name="exprs">summands</param>
        /// <param name="signs">array of signs, <c>true</c> means that the operand should be subtracted instead of added</param>
        /// <returns>the sum expression</returns>
        public static Expression Sum(Expression[] exprs, bool[] signs)
        {
            if (exprs == null || signs == null || exprs.Length != signs.Length)
                throw new InvalidOperationException();

            if (exprs.Length == 0)
                return SpecialConstant.ScalarZero;

            if (exprs.Length == 1)
            {
                if (signs[0])
                    return -exprs[0];
                else
                    return exprs[0];
            }

            int rcount = (exprs.Length + 1) / 2;
            Expression[] rexprs = new Expression[rcount];
            bool[] rsigns = new bool[rcount];
            for (int i = 0; i < rcount; i++)
            {
                if (2*i+1 >= exprs.Length)
                {
                    rexprs[i] = exprs[2*i];
                }
                else
                {
                    if (signs[2 * i + 1])
                        rexprs[i] = exprs[2 * i] - exprs[2 * i + 1];
                    else
                        rexprs[i] = exprs[2 * i] + exprs[2 * i + 1];
                }
                rsigns[i] = signs[2 * i];
            }

            return Sum(rexprs, rsigns);
        }

        /// <summary>
        /// Constructs an expression that represents the sum of its operands.
        /// </summary>
        /// <param name="exprs">operands to sum up</param>
        /// <returns>the sum expression</returns>
        public static Expression Sum(Expression[] exprs)
        {
            return Sum(exprs, new bool[exprs.Length]);
        }

        /// <summary>
        /// Constructs an expression that represents the ceiling operator.
        /// </summary>
        /// <param name="expr">operand</param>
        /// <returns>the ceiling operator expression</returns>
        public static Expression Ceil(Expression expr)
        {
            return new UnOp() { Operation = UnOp.Kind.Ceil, Operand = expr };
        }

        /// <summary>
        /// Constructs an expression that represents the floor operator.
        /// </summary>
        /// <param name="expr">operand</param>
        /// <returns>the floor operator expression</returns>
        public static Expression Floor(Expression expr)
        {
            return new UnOp() { Operation = UnOp.Kind.Floor, Operand = expr };
        }

        /// <summary>
        /// Constructs an expression that represents the minimum operator.
        /// </summary>
        /// <param name="a">first operand</param>
        /// <param name="b">second operand</param>
        /// <returns>the minimum operator expression</returns>
        public static Expression Min(Expression a, Expression b)
        {
            return new BinOp() { Operation = BinOp.Kind.Min, Operand1 = a, Operand2 = b };
        }

        /// <summary>
        /// Constructs an expression that represents the maximum operator.
        /// </summary>
        /// <param name="a">first operand</param>
        /// <param name="b">second operand</param>
        /// <returns>the maximum operator expression</returns>
        public static Expression Max(Expression a, Expression b)
        {
            return new BinOp() { Operation = BinOp.Kind.Max, Operand1 = a, Operand2 = b };
        }
    }

    /// <summary>
    /// A replacement rule combines a matching predicate with an expression generator.
    /// </summary>
    public class ReplacementRule
    {
        private Expression.MatchFunction _matchFn;
        private ExpressionGenerator _gen;

        /// <summary>
        /// Constructs a new replacement rule.
        /// </summary>
        /// <param name="matchFn">expression match predicate</param>
        /// <param name="gen">expression generator for matched expressions</param>
        public ReplacementRule(Expression.MatchFunction matchFn, ExpressionGenerator gen)
        {
            _matchFn = matchFn;
            _gen = gen;
        }

        /// <summary>
        /// Constructs a new replacement rule.
        /// </summary>
        /// <param name="m">expression match predicate</param>
        /// <param name="g">expression generator for matched expressions</param>
        public ReplacementRule(Matching m, Generation g)
        {
            _matchFn = m;
            _gen = g;
        }

        /// <summary>
        /// Applies this replacement rule to the first matched expression of the expression hierarchy.
        /// </summary>
        /// <param name="e">expression to apply this rule to</param>
        /// <param name="hit">out parameter to receive whether an expression was matched</param>
        /// <returns>newly constructed expession with applied replacement</returns>
        public Expression ApplyOnce(Expression e, out bool hit)
        {
            return e.Replace(_matchFn, _gen, out hit);
        }

        /// <summary>
        /// Applies this replacement rule as often as possible to all expressions of the expression hierarchy.
        /// </summary>
        /// <param name="e">expression to apply this rule to</param>
        /// <param name="hit">out parameter to receive whether an expression was matched</param>
        /// <returns>newly constructed expession with applied replacement(s)</returns>
        public Expression ApplyRepeatedly(Expression e, out bool hit)
        {
            bool localHit;
            hit = false;
            do
            {
                e = e.Replace(_matchFn, _gen, out localHit);
                if (localHit)
                    hit = true;
            } while (localHit);
            return e;
        }
    }

    /// <summary>
    /// A reference expression to a model element.
    /// </summary>
    public abstract class ElementReference : Expression
    {
        public override int GetPrecedence(IOperatorPrecedence prec)
        {
            return -1;
        }
    }

    /// <summary>
    /// A reference expression to a literal.
    /// </summary>
    public class LiteralReference : ElementReference
    {
        /// <summary>
        /// Describes the way how the literal it referenced.
        /// </summary>
        public enum EMode
        {
            /// <summary>
            /// Direct reference
            /// </summary>
            Direct,

            /// <summary>
            /// Address of literal reference
            /// </summary>
            ByAddress
        }

        /// <summary>
        /// Constructs a new literal reference expression.
        /// </summary>
        /// <param name="referencedObject">literal to reference</param>
        /// <param name="mode">referencing mode</param>
        public LiteralReference(ILiteral referencedObject, EMode mode = EMode.Direct)
        {
            Arity = 0;
            ReferencedObject = referencedObject;
            Mode = mode;
        }

        /// <summary>
        /// The reference literal
        /// </summary>
        public ILiteral ReferencedObject { get; private set; }

        /// <summary>
        /// The way how the literal is referenced
        /// </summary>
        public EMode Mode { get; private set; }

        public override object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn)
        {
            var lit = ReferencedObject as Literal;
            if (lit != null)
            {
                object value = lit.Eval(eval);
                efn(this, value);
                return value;
            }
            else
            {
                object value = eval.EvalLiteral(ReferencedObject);
                efn(this, value);
                return value;
            }
        }

        public override string ToString(IStringifyInfo info)
        {
            base.ToString(info);
            return info.Notation.GetLiteralNotation()(ReferencedObject, Mode);
        }

        public override ResultType Accept<ResultType>(IExpressionVisitor<ResultType> xform)
        {
            return xform.TransformLiteralReference(this);
        }

        public override bool NodeEquals(Expression e)
        {
            if (e is LiteralReference)
            {
                LiteralReference lr = (LiteralReference)e;
                return ReferencedObject.Equals(lr.ReferencedObject);
            }
            else
            {
                return false;
            }
        }

        public override bool DeepEquals(Expression e)
        {
            return NodeEquals(e);
        }

        public override bool Equals(object obj)
        {
            if (obj is Expression)
                return NodeEquals((Expression)obj);
            else
                return false;
        }

        protected override int ComputeHashCode()
        {
            return 0x3abf55cf ^ ReferencedObject.GetHashCode();
        }

        public override TypeDescriptor ResultType
        {
            get { return ReferencedObject.Type; }
            set { base.ResultType = value; }
        }

        public override EResultTypeClass ResultTypeClass
        {
            get
            {
                if (Mode == EMode.ByAddress)
                    return EResultTypeClass.ObjectReference;
                return ResultTypeClasses.FromType(ReferencedObject.Type.CILType);
            }
        }

        protected override Expression CloneThisImpl(Expression[] newChildren)
        {
            return new LiteralReference(ReferencedObject, Mode);
        }

        public static LiteralReference CreateConstant(object value)
        {
            return new LiteralReference(new Constant(value), EMode.Direct);
        }

        public static implicit operator LiteralReference(double value)
        {
            return CreateConstant(value);
        }

        public static implicit operator LiteralReference(int value)
        {
            return CreateConstant(value);
        }
    }

    /// <summary>
    /// An expression that represents a special/fundamental constant.
    /// </summary>
    public class SpecialConstant : ElementReference
    {
        /// <summary>
        /// Choice of constants
        /// </summary>
        public enum Kind
        {
            /// <summary>
            /// Fundamental constant e
            /// </summary>
            E,

            /// <summary>
            /// Fundamental constant Pi
            /// </summary>
            PI,

            /// <summary>
            /// 0
            /// </summary>
            ScalarZero,

            /// <summary>
            /// 1
            /// </summary>
            ScalarOne,

            /// <summary>
            /// boolean "true" literal
            /// </summary>
            True,

            /// <summary>
            /// boolean "false" literal
            /// </summary>
            False
        }

        /// <summary>
        /// Constructs an instance of a special constant expression.
        /// </summary>
        public SpecialConstant()
        {
            Arity = 0;
        }

        /// <summary>
        /// Which constant is represented.
        /// </summary>
        public new Kind Constant { get; set; }

        public override object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn)
        {
            object value;
            switch (Constant)
            {
                case Kind.E: value = eval.E(ResultType); break;
                case Kind.PI: value = eval.PI(ResultType); break;
                case Kind.ScalarOne: value = eval.ScalarOne(ResultType); break;
                case Kind.ScalarZero: value = eval.ScalarZero(ResultType); break;
                case Kind.True: value = eval.True(ResultType); break;
                case Kind.False: value = eval.False(ResultType); break;
                default: throw new NotImplementedException();
            }
            efn(this, value);
            return value;
        }

        public override string ToString(IStringifyInfo info)
        {
            base.ToString(info);
            return info.Notation.GetSpecialConstantSymbol(Constant);
        }

        protected override int ComputeHashCode()
        {
            return Constant.GetHashCode();
        }

        public override bool NodeEquals(Expression e)
        {
            if (e is SpecialConstant)
            {
                SpecialConstant other = (SpecialConstant)e;
                return other.Constant == Constant;
            }
            else
            {
                return false;
            }
        }

        public override bool DeepEquals(Expression e)
        {
            return NodeEquals(e);
        }

        public override bool Equals(object obj)
        {
            if (obj is Expression)
                return NodeEquals((Expression)obj);
            else
                return false;
        }

        public override ResultType Accept<ResultType>(IExpressionVisitor<ResultType> xform)
        {
            return xform.TransformSpecialConstant(this);
        }

        public override EResultTypeClass ResultTypeClass
        {
            get
            {
                switch (Constant)
                {
                    case Kind.E:
                    case Kind.PI:
                    case Kind.ScalarOne:
                    case Kind.ScalarZero:
                        return EResultTypeClass.Algebraic;

                    case Kind.True:
                    case Kind.False:
                        return EResultTypeClass.Boolean;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        protected override Expression CloneThisImpl(Expression[] newChildren)
        {
            return new SpecialConstant()
            {
                Constant = this.Constant
            };
        }

        /// <summary>
        /// Returns symbolic "0" of result type <c>double</c>.
        /// </summary>
        public static SpecialConstant ScalarZero
        {
            get
            {
                return new SpecialConstant() 
                { 
                    Constant = Kind.ScalarZero,
                    ResultType = typeof(double)
                };
            }
        }

        /// <summary>
        /// Returns symbolic "1" of result type <c>double</c>.
        /// </summary>
        public static SpecialConstant ScalarOne
        {
            get
            {
                return new SpecialConstant() 
                { 
                    Constant = Kind.ScalarOne,
                    ResultType = typeof(double)
                };
            }
        }

        /// <summary>
        /// Returns "Pi" of result type <c>double</c>.
        /// </summary>
        public static SpecialConstant PI
        {
            get
            {
                return new SpecialConstant() 
                { 
                    Constant = Kind.PI,
                    ResultType = typeof(double)
                };
            }
        }

        /// <summary>
        /// Returns "e" of result type <c>double</c>.
        /// </summary>
        public static SpecialConstant E
        {
            get
            {
                return new SpecialConstant() 
                { 
                    Constant = Kind.E,
                    ResultType = typeof(double)
                };
            }
        }

        /// <summary>
        /// Returns "true" of result type <c>bool</c>.
        /// </summary>
        public static SpecialConstant True
        {
            get
            {
                return new SpecialConstant()
                {
                    Constant = Kind.True,
                    ResultType = typeof(bool)
                };
            }
        }

        /// <summary>
        /// Returns "false" of result type <c>bool</c>.
        /// </summary>
        public static SpecialConstant False
        {
            get
            {
                return new SpecialConstant()
                {
                    Constant = Kind.False,
                    ResultType = typeof(bool)
                };
            }
        }
    }

    /// <summary>
    /// A unary operator expression.
    /// </summary>
    public class UnOp : Expression
    {
        /// <summary>
        /// Choice of unary operators.
        /// </summary>
        public enum Kind
        {
            /// <summary>
            /// The identity function maps any value to itself.
            /// </summary>
            Identity,

            /// <summary>
            /// Negation
            /// </summary>
            Neg,

            /// <summary>
            /// Boolean inversion
            /// </summary>
            BoolNot,

            /// <summary>
            /// Bit-wise complement
            /// </summary>
            BitwiseNot,

            /// <summary>
            /// Sign extension
            /// </summary>
            ExtendSign,

            /// <summary>
            /// e^x
            /// </summary>
            Exp,

            /// <summary>
            /// Natural logarithm
            /// </summary>
            Log,

            /// <summary>
            /// Absolute value
            /// </summary>
            Abs,

            /// <summary>
            /// Sine
            /// </summary>
            Sin,

            /// <summary>
            /// Cosine
            /// </summary>
            Cos,

            /// <summary>
            /// Square-root
            /// </summary>
            Sqrt,

            /// <summary>
            /// Ceiling operator
            /// </summary>
            Ceil,

            /// <summary>
            /// Floor operator
            /// </summary>
            Floor
        }

        /// <summary>
        /// Constructs a new unary operation.
        /// </summary>
        public UnOp()
        {
            Arity = 1;
        }

        /// <summary>
        /// Gets or sets the kind of operation.
        /// </summary>
        public Kind Operation { get; set; }

        /// <summary>
        /// Gets or sets the operand.
        /// </summary>
        public Expression Operand 
        {
            get { return Children[0]; }
            set { Children[0] = value; }
        }

        public override object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn)
        {
            object operandValue = Operand.Eval(eval, efn);
            object value;
            switch (Operation)
            {
                case Kind.Neg: value = eval.Neg(operandValue, ResultType); break;
                case Kind.BoolNot: value = eval.BoolNot(operandValue, ResultType); break;
                case Kind.BitwiseNot: value = eval.BitwiseNot(operandValue, ResultType); break;
                case Kind.Exp: value = eval.Exp(operandValue, ResultType); break;
                case Kind.Log: value = eval.Log(operandValue, ResultType); break;
                case Kind.Abs: value = eval.Abs(operandValue, ResultType); break;
                case Kind.ExtendSign: value = eval.ExtendSign(operandValue, ResultType); break;
                case Kind.Sin: value = eval.Sin(operandValue, ResultType); break;
                case Kind.Cos: value = eval.Cos(operandValue, ResultType); break;
                default: throw new NotImplementedException();
            }
            efn(this, value);
            return value;
        }

        public override int GetPrecedence(IOperatorPrecedence prec)
        {
            return prec.GetOperatorOrder(Operation);
        }

        public override string ToString(IStringifyInfo info)
        {
            base.ToString(info);
            string operandStr = Operand.ToString(info);
            NotateFunc nfn = info.Notation.GetNotation(Operation);
            int prec = Operand.GetPrecedence(info.Precedence);
            int myprec = GetPrecedence(info.Precedence);
            EOperatorAssociativity myassoc = info.Precedence.GetOperatorAssociativity(Operation);
            UnOp operandAsUnOp = Operand as UnOp;
            bool sameOp = operandAsUnOp != null && operandAsUnOp.Operation == Operation;
            if (myprec >= 0 &&
                (prec > myprec ||
                    (prec == myprec &&
                        myassoc == EOperatorAssociativity.UseParenthesis)))
            {
                operandStr = info.Notation.GetBracketNotation()(operandStr);
            }
            return nfn(operandStr);
        }

        protected override int ComputeHashCode()
        {
            return 3 * Operand.GetHashCode() ^ (0x574bd807 + Operation.GetHashCode());
        }

        public override bool NodeEquals(Expression e)
        {
            if (e is UnOp)
            {
                UnOp other = (UnOp)e;
                return other.Operation == Operation;
            }
            else
            {
                return false;
            }
        }

        public override bool DeepEquals(Expression e)
        {
            return (NodeEquals(e) && ChildrenAreDeepEqualTo(e));
        }

        public override bool Equals(object obj)
        {
            if (obj is Expression)
                return DeepEquals((Expression)obj);
            else
                return false;
        }

        public override ResultType Accept<ResultType>(IExpressionVisitor<ResultType> xform)
        {
            return xform.TransformUnOp(this);
        }

        public override TypeDescriptor ResultType
        {
            get 
            { 
                if (base.ResultType != null)
                    return base.ResultType;

                switch (Operation)
                {
                    case Kind.Neg:
                        return TypeDescriptor.GetTypeOf(
                            -(dynamic)Operand.ResultType.GetSampleInstance(ETypeCreationOptions.AnyObject));

                    default:
                        throw new NotImplementedException();
                }
            }
            set { base.ResultType = value; }
        }

        public override EResultTypeClass ResultTypeClass
        {
            get
            {
                switch (Operation)
                {
                    case Kind.Abs:
                    case Kind.Identity:
                    case Kind.Neg:
                    case Kind.Sin:
                    case Kind.Cos:
                    case Kind.Sqrt:
                        return Operand.ResultTypeClass;

                    case Kind.BitwiseNot:
                    case Kind.ExtendSign:
                        return EResultTypeClass.Integral;

                    case Kind.BoolNot:
                        return EResultTypeClass.Boolean;

                    case Kind.Exp:
                    case Kind.Log:
                    case Kind.Ceil:
                    case Kind.Floor:
                        return EResultTypeClass.Algebraic;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        protected override Expression CloneThisImpl(Expression[] newChildren)
        {
            return new UnOp()
            {
                Operation = this.Operation,
                Operand = newChildren[0],
                ResultType = this.ResultType
            };
        }
    }

    /// <summary>
    /// Binary operator expression.
    /// </summary>
    public class BinOp : Expression
    {
        /// <summary>
        /// Choice of binary operators.
        /// </summary>
        public enum Kind
        {
            /// <summary>
            /// Addition
            /// </summary>
            Add,

            /// <summary>
            /// Subtraction
            /// </summary>
            Sub,

            /// <summary>
            /// Multiplication
            /// </summary>
            Mul,

            /// <summary>
            /// Division
            /// </summary>
            Div,

            /// <summary>
            /// Division remainder
            /// </summary>
            Rem,

            /// <summary>
            /// Boolean or bit-wise conjunction
            /// </summary>
            And,

            /// <summary>
            /// Boolean or bit-wise disjunction
            /// </summary>
            Or,

            /// <summary>
            /// Boolean or bit-wise anti-valence
            /// </summary>
            Xor,

            /// <summary>
            /// Logical left shift
            /// </summary>
            LShift,

            /// <summary>
            /// Arithmetical or logical right shift
            /// </summary>
            RShift,

            /// <summary>
            /// Concatenation
            /// </summary>
            Concat,

            /// <summary>
            /// e^x
            /// </summary>
            Exp,

            /// <summary>
            /// Natural logarithm
            /// </summary>
            Log,

            /// <summary>
            /// Equality comparison
            /// </summary>
            Eq,

            /// <summary>
            /// "Greater than" comparison
            /// </summary>
            Gt,

            /// <summary>
            /// "Greater than or equal" comparison
            /// </summary>
            GtEq,

            /// <summary>
            /// "Less than" comparison
            /// </summary>
            Lt,

            /// <summary>
            /// "Less than or equal" comparison
            /// </summary>
            LtEq,

            /// <summary>
            /// Inequality comparison
            /// </summary>
            NEq,

            /// <summary>
            /// Minimum operator
            /// </summary>
            Min,

            /// <summary>
            /// Maximum operator
            /// </summary>
            Max
        }

        /// <summary>
        /// Constructs a new instance of the binary operator expression.
        /// </summary>
        public BinOp()
        {
            Arity = 2;
        }

        /// <summary>
        /// Gets or sets the kind of operation.
        /// </summary>
        public Kind Operation { get; set; }

        /// <summary>
        /// Gets or sets the first operand.
        /// </summary>
        public Expression Operand1 
        {
            get { return Children[0]; }
            set { Children[0] = value; }

        }

        /// <summary>
        /// Gets or sets the second operand.
        /// </summary>
        public Expression Operand2 
        {
            get { return Children[1]; }
            set { Children[1] = value; }
        }

        public override object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn)
        {
            object ov1 = Operand1.Eval(eval, efn);
            object ov2 = Operand2.Eval(eval, efn);
            object value;
            switch (Operation)
            {
                case Kind.Add: value = eval.Add(ov1, ov2, ResultType); break;
                case Kind.Sub: value = eval.Sub(ov1, ov2, ResultType); break;
                case Kind.Mul: value = eval.Mul(ov1, ov2, ResultType); break;
                case Kind.Div: value = eval.Div(ov1, ov2, ResultType); break;
                case Kind.Rem: value = eval.Rem(ov1, ov2, ResultType); break;
                case Kind.And: value = eval.And(ov1, ov2, ResultType); break;
                case Kind.Or: value = eval.Or(ov1, ov2, ResultType); break;
                case Kind.Xor: value = eval.Xor(ov1, ov2, ResultType); break;
                case Kind.LShift: value = eval.LShift(ov1, ov2, ResultType); break;
                case Kind.RShift: value = eval.RShift(ov1, ov2, ResultType); break;
                case Kind.Concat: value = eval.Concat(ov1, ov2, ResultType); break;
                case Kind.Exp: value = eval.Exp(ov1, ov2, ResultType); break;
                case Kind.Log: value = eval.Log(ov1, ov2, ResultType); break;
                case Kind.Lt: value = eval.IsLessThan(ov1, ov2, ResultType); break;
                case Kind.LtEq: value = eval.IsLessThanOrEqual(ov1, ov2, ResultType); break;
                case Kind.NEq: value = eval.IsNotEqual(ov1, ov2, ResultType); break;
                case Kind.Eq: value = eval.IsEqual(ov1, ov2, ResultType); break;
                case Kind.Gt: value = eval.IsGreaterThan(ov1, ov2, ResultType); break;
                case Kind.GtEq: value = eval.IsGreaterThanOrEqual(ov1, ov2, ResultType); break;
                default: throw new NotImplementedException();
            }
            efn(this, value);
            return value;
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public override int GetPrecedence(IOperatorPrecedence prec)
        {
            return prec.GetOperatorOrder(Operation);
        }

        public override string ToString(IStringifyInfo info)
        {
            base.ToString(info);
            string opstr1 = Operand1.ToString(info);
            string opstr2 = Operand2.ToString(info);
            NotateFunc nfn = info.Notation.GetNotation(Operation);
            string aux = info.ToString();
            int prec1 = Operand1.GetPrecedence(info.Precedence);
            int prec2 = Operand2.GetPrecedence(info.Precedence);
            int myprec = GetPrecedence(info.Precedence);
            EOperatorAssociativity myassoc = info.Precedence.GetOperatorAssociativity(Operation);
            BinOp op1AsBinOp = Operand1 as BinOp;
            bool sameOp1 = op1AsBinOp != null && op1AsBinOp.Operation == Operation;
            BinOp op2AsBinOp = Operand2 as BinOp;
            bool sameOp2 = op2AsBinOp != null && op2AsBinOp.Operation == Operation;
            if (myprec >= 0 && (prec1 > myprec ||
                (prec1 == myprec &&
                    (myassoc == EOperatorAssociativity.RightAssociative ||
                     myassoc == EOperatorAssociativity.UseParenthesis))))
            {
                opstr1 = info.Notation.GetBracketNotation()(opstr1);
            }
            if (myprec >= 0 && (prec2 > myprec ||
                (prec2 == myprec &&
                    (myassoc == EOperatorAssociativity.LeftAssociative ||
                     myassoc == EOperatorAssociativity.UseParenthesis))))
            {
                opstr2 = info.Notation.GetBracketNotation()(opstr2);
            }
            return nfn(opstr1, opstr2);
        }

        protected override int ComputeHashCode()
        {
            return 5 * Operand1.GetHashCode() ^
                7 * Operand2.GetHashCode() ^
                (0x13bf0089 + Operation.GetHashCode());
        }

        public override bool NodeEquals(Expression e)
        {
            if (e is BinOp)
            {
                BinOp other = (BinOp)e;
                return Operation == other.Operation;
            }
            else
            {
                return false;
            }
        }

        public override bool DeepEquals(Expression e)
        {
            return NodeEquals(e) && ChildrenAreDeepEqualTo(e);
        }

        public override bool Equals(object obj)
        {
            if (obj is Expression)
                return DeepEquals((Expression)obj);
            else
                return false;
        }

        public override ResultType Accept<ResultType>(IExpressionVisitor<ResultType> xform)
        {
            return xform.TransformBinOp(this);
        }

        public override TypeDescriptor ResultType
        {
            get
            {
                if (_resultType != null)
                    return _resultType;

                dynamic o1, o2;
                TypeDescriptor rtype = null;
                switch (Operation)
                {
                    case Kind.Add:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 + o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.And:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 & o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.Div:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(
                                ETypeCreationOptions.NonZero | 
                                ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 / o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.Eq:
                    case Kind.Gt:
                    case Kind.GtEq:
                    case Kind.Lt:
                    case Kind.LtEq:
                    case Kind.NEq:
                        return typeof(bool);

                    case Kind.Mul:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 * o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.Or:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 | o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.Rem:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 % o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.Sub:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 - o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.Xor:
                        {
                            o1 = Operand1.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            o2 = Operand2.ResultType.GetSampleInstance(ETypeCreationOptions.ForceCreation);
                            dynamic r = o1 ^ o2;
                            rtype = TypeDescriptor.GetTypeOf(r);
                            break;
                        }

                    case Kind.Min:
                    case Kind.Max:
                        {
                            rtype = Operand1.ResultType;
                            break;
                        }

                    case Kind.RShift:
                    case Kind.LShift:
                    case Kind.Log:
                    case Kind.Exp:
                    case Kind.Concat:
                    default:
                        return base.ResultType;
                }
                if (Operand1.ResultType.IsComplete &&
                    Operand2.ResultType.IsComplete)
                    _resultType = rtype;
                else
                    _resultType = rtype.CILType; // cancel out information on complete type
                return _resultType;

            }
            set { base.ResultType = value; }
        }

        public override EResultTypeClass ResultTypeClass
        {
            get
            {
                switch (Operation)
                {
                    case Kind.Add:
                    case Kind.Div:
                    case Kind.Mul:
                    case Kind.Sub:
                    case Kind.Rem:
                    case Kind.Min:
                    case Kind.Max:
                        if (Operand1.ResultTypeClass != Operand2.ResultTypeClass)
                            throw new InvalidOperationException();
                        return Operand1.ResultTypeClass;

                    case Kind.And:
                    case Kind.LShift:
                    case Kind.RShift:
                    case Kind.Or:
                    case Kind.Xor:
                    case Kind.Concat:
                        return EResultTypeClass.Integral;

                    case Kind.Eq:
                    case Kind.Gt:
                    case Kind.GtEq:
                    case Kind.Lt:
                    case Kind.LtEq:
                    case Kind.NEq:
                        return EResultTypeClass.Boolean;

                    case Kind.Exp:
                    case Kind.Log:
                        return EResultTypeClass.Algebraic;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        protected override Expression CloneThisImpl(Expression[] newChildren)
        {
            return new BinOp()
            {
                Operation = this.Operation,
                Operand1 = newChildren[0],
                Operand2 = newChildren[1],
                ResultType = this.ResultType
            };
        }
    }

    /// <summary>
    /// A ternary operation expression.
    /// </summary>
    public class TernOp : Expression
    {
        /// <summary>
        /// Choice of ternary operators
        /// </summary>
        public enum Kind
        {
            /// <summary>
            /// Slice operation
            /// </summary>
            Slice,

            /// <summary>
            /// Conditional operation, i.e. C# operator <c>c ? x : y</c>
            /// </summary>
            Conditional
        }

        /// <summary>
        /// Constructs a new ternary operator expression.
        /// </summary>
        public TernOp()
        {
            Arity = 3;
        }

        /// <summary>
        /// Gets or sets the kind of operation.
        /// </summary>
        public Kind Operation { get; set; }

        /// <summary>
        /// Returns the operands.
        /// </summary>
        public Expression[] Operands
        {
            get { return Children; }
        }

        public override object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn)
        {
            object ov0 = Operands[0].Eval(eval, efn);
            switch (Operation)
            {
                case Kind.Slice:
                    {
                        object ov1 = Operands[1].Eval(eval, efn);
                        object ov2 = Operands[2].Eval(eval, efn);
                        object value = eval.Slice(ov0, ov1, ov2, ResultType);
                        efn(this, value);
                        return value;
                    }

                case Kind.Conditional:
                    {
                        object ov1 = Operands[1].Eval(eval, efn);
                        object ov2 = Operands[2].Eval(eval, efn);
                        object value = eval.ConditionallyCombine(ov0, ov1, ov2, ResultType);
                        efn(this, value);
                        return value;
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public override TypeDescriptor ResultType
        {
            get
            {
                if (_resultType == null)
                {
                    dynamic op0 = Operands[0].ResultType.GetSampleInstance();
                    dynamic op1 = Operands[1].ResultType.GetSampleInstance();
                    dynamic op2 = Operands[2].ResultType.GetSampleInstance();

                    dynamic rsample;
                    switch (Operation)
                    {
                        case Kind.Slice:
                            rsample = op0[op1, op2];
                            break;

                        case Kind.Conditional:
                            rsample = op1;
                            break;

                        default:
                            throw new NotImplementedException();
                    }

                    _resultType = TypeDescriptor.GetTypeOf(rsample);
                }
                return _resultType;
            }
            set
            {
                base.ResultType = value;
            }
        }

        public override string ToString(IStringifyInfo info)
        {
            base.ToString(info);
            string[] args = new string[3];
            int myprec = GetPrecedence(info.Precedence);
            for (int i = 0; i < 3; i++)
            {
                args[i] = Operands[i].ToString(info);
                int prec = Operands[i].GetPrecedence(info.Precedence);
                if (myprec >= 0 && prec > myprec)
                    args[i] = info.Notation.GetBracketNotation()(args[i]);
            }
            NotateFunc nfn = info.Notation.GetNotation(Operation);
            return nfn(args);
        }

        public override int GetPrecedence(IOperatorPrecedence prec)
        {
            return prec.GetOperatorOrder(Operation);
        }

        public override ResultType Accept<ResultType>(IExpressionVisitor<ResultType> vtor)
        {
            return vtor.TransformTernOp(this);
        }

        public override bool NodeEquals(Expression e)
        {
            if (e is TernOp)
            {
                TernOp other = (TernOp)e;
                return Operation.Equals(other.Operation);
            }
            else
            {
                return false;
            }
        }

        public override bool DeepEquals(Expression e)
        {
            return NodeEquals(e) && ChildrenAreDeepEqualTo(e);
        }

        public override EResultTypeClass ResultTypeClass
        {
            get
            {
                switch (Operation)
                {
                    case Kind.Slice:
                        return EResultTypeClass.Integral;

                    case Kind.Conditional:
                        return Operands[1].ResultTypeClass;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        protected override Expression CloneThisImpl(Expression[] newChildren)
        {
            TernOp copy = new TernOp()
            {
                Operation = this.Operation,
                ResultType = this.ResultType
            };
            newChildren.CopyTo(copy.Operands, 0);
            return copy;
        }

        protected override int ComputeHashCode()
        {
            return 5 * Operands[0].GetHashCode() ^
                7 * Operands[1].GetHashCode() ^
                13 * Operands[2].GetHashCode() ^
                (0x7baccc12 + Operation.GetHashCode());
        }
    }

    /// <summary>
    /// Common interface for everything which may be called as a function.
    /// </summary>
    public interface ICallable
    {
        /// <summary>
        /// Function name
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Reference to a SysDOM function.
    /// </summary>
    public class FunctionRef: ICallable
    {
        /// <summary>
        /// The function name
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The function itself
        /// </summary>
        public Function Implementation { get; private set; }

        /// <summary>
        /// Constructs a new SysDOM function reference.
        /// </summary>
        /// <param name="name">function name</param>
        /// <param name="impl">referenced function</param>
        public FunctionRef(string name, Function impl)
        {
            Name = name;
            Implementation = impl;
        }
    }

    /// <summary>
    /// A function specifier with possibly multiple representations in different domains.
    /// </summary>
    public class FunctionSpec : ICallable
    {
        /// <summary>
        /// Constructs a new function specifier.
        /// </summary>
        /// <param name="resultType">type of function return value</param>
        public FunctionSpec(TypeDescriptor resultType)
        {
            Contract.Requires(resultType != null);
            _resultType = resultType;
        }

        /// <summary>
        /// CLI method information on the specified function.
        /// </summary>
        public MethodBase CILRep { get; internal set; }

        /// <summary>
        /// SysDOM representation of the specified function without calling context specializations.
        /// </summary>
        public MethodDescriptor GenericSysDOMRep { get; internal set; }

        /// <summary>
        /// SysDOM representation of the specified function with calling context specializations.
        /// </summary>
        public MethodDescriptor SpecialSysDOMRep { get; internal set; }

        /// <summary>
        /// XIL-S representation of the specified function.
        /// </summary>
        public XILSFunction XILSRep { get; internal set; }

        /// <summary>
        /// XIL-3 representation of the specified function. 
        /// </summary>
        public XILSFunction XIL3Rep { get; internal set; }

        /// <summary>
        /// Intrinsic representation of the specified function.
        /// </summary>
        public IntrinsicFunction IntrinsicRep { get; internal set; }

        private TypeDescriptor _resultType;

        /// <summary>
        /// Returns the type of the function return value.
        /// </summary>
        public TypeDescriptor ResultType
        {
            get { return _resultType; }
        }

        /// <summary>
        /// Returns the function name.
        /// </summary>
        public string Name
        {
            get
            {
                if (CILRep != null)
                    return CILRep.Name;
                if (GenericSysDOMRep != null)
                    return GenericSysDOMRep.Name;
                if (SpecialSysDOMRep != null)
                    return SpecialSysDOMRep.Name;
                if (XILSRep != null)
                    return XILSRep.Name;
                if (XIL3Rep != null)
                    return XIL3Rep.Name;
                if (IntrinsicRep != null)
                    return IntrinsicRep.Name;
                throw new NotSupportedException();
            }
        }

        public override bool Equals(object obj)
        {
            FunctionSpec fspec = obj as FunctionSpec;
            if (fspec == null)
                return false;
            return object.Equals(CILRep, fspec.CILRep) &&
                object.Equals(SpecialSysDOMRep, fspec.SpecialSysDOMRep) &&
                object.Equals(GenericSysDOMRep, fspec.GenericSysDOMRep) &&
                object.Equals(XILSRep, fspec.XILSRep) &&
                object.Equals(XIL3Rep, fspec.XIL3Rep) &&
                object.Equals(IntrinsicRep, fspec.IntrinsicRep);
        }

        public override int GetHashCode()
        {
            int hash = CILRep == null ? 0 : CILRep.GetHashCode();
            hash ^= SpecialSysDOMRep == null ? 0 : SpecialSysDOMRep.GetHashCode();
            hash ^= GenericSysDOMRep == null ? 0 : GenericSysDOMRep.GetHashCode();
            hash ^= XILSRep == null ? 0 : XILSRep.GetHashCode();
            hash ^= XIL3Rep == null ? 0 : XIL3Rep.GetHashCode();
            hash ^= IntrinsicRep == null ? 0 : IntrinsicRep.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            List<string> result = new List<string>();
            if (IntrinsicRep != null)
                return IntrinsicRep.Name;
            if (CILRep != null)
                return CILRep.Name;
            if (XILSRep != null)
                return "XILS:" + XILSRep.Name;
            if (XIL3Rep != null)
                return "XIL3:" + XIL3Rep.Name;
            if (GenericSysDOMRep != null)
                return "SysDOM:" + GenericSysDOMRep.Name;
            return "<unknown function>";
        }
    }

    /// <summary>
    /// A function call expression.
    /// </summary>
    public class FunctionCall : Expression
    {
        /// <summary>
        /// Gets or sets the called function.
        /// </summary>
        public ICallable Callee { get; set; }

        /// <summary>
        /// Gets or sets the call arguments.
        /// </summary>
        public Expression[] Arguments 
        {
            get
            {
                return Children;
            }
            set
            {
                Arity = value.Length;
                Array.Copy(value, Children, Arity);
            }
        }

        /// <summary>
        /// Constructs a new function call expression.
        /// </summary>
        public FunctionCall()
        {
            SetResultTypeClass = EResultTypeClass.Unknown;
        }

        public override string ToString(IStringifyInfo info)
        {
            base.ToString(info);

            string[] argStrings = new string[Arguments.Length];
            for (int i = 0; i < Arguments.Length; i++)
                argStrings[i] = Arguments[i].ToString(info);

            return info.Notation.GetFunctionNotation()(Callee, argStrings);
        }

        protected override int ComputeHashCode()
        {
            int hash = Callee.GetHashCode();
            for (int n = 0; n < Arguments.Length; n++)
            {
                hash ^= (n + 2) * Arguments[n].GetHashCode();
            }
            return hash;
        }

        public override bool NodeEquals(Expression e)
        {
            if (e is FunctionCall)
            {
                FunctionCall other = (FunctionCall)e;
                return Callee.Equals(other.Callee);
            }
            else
            {
                return false;
            }
        }

        public override bool DeepEquals(Expression e)
        {
            return NodeEquals(e) && ChildrenAreDeepEqualTo(e);
        }

        public override bool Equals(object obj)
        {
            if (obj is Expression)
                return DeepEquals((Expression)obj);
            else
                return false;
        }

        public override object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn)
        {
            object[] args = new object[Arguments.Length];
            for (int i = 0; i < Arguments.Length; i++)
                args[i] = Arguments[i].Eval(eval, efn);
            object result = eval.EvalFunction(this, args, ResultType);
            if (efn != null)
                efn(this, result);
            return result;
        }

        public override int GetPrecedence(IOperatorPrecedence prec)
        {
            return -1;
        }

        public override ResultType Accept<ResultType>(IExpressionVisitor<ResultType> xform)
        {
            return xform.TransformFunction(this);
        }

        public EResultTypeClass SetResultTypeClass { private get; set; }

        public override EResultTypeClass ResultTypeClass
        {
            get { return SetResultTypeClass; }
        }

        protected override Expression CloneThisImpl(Expression[] newChildren)
        {
            return new FunctionCall()
            {
                Callee = this.Callee,
                ResultType = this.ResultType,
                SetResultTypeClass = this.ResultTypeClass,
                Arguments = newChildren
            };
        }
    }

    /// <summary>
    /// A placeholder expression which gets substituted at some later stage.
    /// </summary>
    public class LazyExpression : Expression
    {
        private Expression _placeHolder;

        /// <summary>
        /// Gets or sets the placeholder expression.
        /// </summary>
        public Expression PlaceHolder 
        {
            get { return _placeHolder; }
            set
            {
                Contract.Requires(value != null);
                Contract.Assume(_placeHolder == null);
                _placeHolder = value;
                Arity = value.Arity;
            }
        }

        public override object Eval(IEvaluator eval, Expression.OnExpressionEvaluatedFn efn)
        {
            return PlaceHolder.Eval(eval, efn);
        }

        public override string ToString(IStringifyInfo info)
        {
            base.ToString(info);

            if (PlaceHolder == null)
                return "<placeholder>";
            else
                return PlaceHolder.ToString();
        }

        public override int GetPrecedence(IOperatorPrecedence prec)
        {
            return PlaceHolder.GetPrecedence(prec);
        }

        protected override Expression CloneThisImpl(Expression[] newChildren)
        {
            return new LazyExpression()
            {
                PlaceHolder = this.PlaceHolder.CloneThis(newChildren),
                ResultType = this.ResultType
            };
        }

        public override ResultType Accept<ResultType>(IExpressionVisitor<ResultType> vtor)
        {
            return PlaceHolder.Accept(vtor);
        }

        public override Expression Transform(IExpressionTransformer xform)
        {
            if (PlaceHolder == null)
                return this;
            else
                return base.Transform(xform);
        }

        public override bool NodeEquals(Expression e)
        {
            return PlaceHolder.Equals(e);
        }

        public override bool DeepEquals(Expression e)
        {
            return PlaceHolder.DeepEquals(e);
        }

        public override EResultTypeClass ResultTypeClass
        {
            get { return PlaceHolder.ResultTypeClass; }
        }

        protected override int ComputeHashCode()
        {
            return PlaceHolder == null ? 0 : PlaceHolder.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (PlaceHolder != null)
            {
                return PlaceHolder.Equals(obj);
            }
            else
            {
                LazyExpression other = obj as LazyExpression;
                if (other == null)
                    return false;
                return other.PlaceHolder == null;
            }
        }

        public override TypeDescriptor ResultType
        {
            get
            {
                if (PlaceHolder == null)
                    return base.ResultType;
                else
                    return PlaceHolder.ResultType;
            }
            set
            {
                if (PlaceHolder == null)
                    base.ResultType = value;
                else
                    PlaceHolder.ResultType = value;
            }
        }
    }

    #region Transformation

    /// <summary>
    /// Visitor pattern interface for expressions.
    /// </summary>
    /// <typeparam name="ResultType">visitor method return type</typeparam>
    public interface IExpressionVisitor<ResultType>
    {
        ResultType TransformLiteralReference(LiteralReference expr);
        ResultType TransformSpecialConstant(SpecialConstant expr);
        ResultType TransformUnOp(UnOp expr);
        ResultType TransformBinOp(BinOp expr);
        ResultType TransformTernOp(TernOp expr);
        ResultType TransformFunction(FunctionCall expr);
    }

    /// <summary>
    /// A special expression visitor that transforms expressions into expressions.
    /// </summary>
    public interface IExpressionTransformer: IExpressionVisitor<Expression>
    {
    }

    #endregion

    /// <summary>
    /// A matrix whose elements a expressions.
    /// </summary>
    public class Matrix
    {
        private class ElementIndex
        {
            public int Row { get; set; }
            public int Col { get; set; }

            public ElementIndex(int row, int col)
            {
                Row = row;
                Col = col;
            }

            public override bool Equals(object obj)
            {
                if (obj is ElementIndex)
                {
                    ElementIndex idx = (ElementIndex)obj;
                    return idx.Row == Row && idx.Col == Col;
                }
                else
                    return false;
            }

            public override int GetHashCode()
            {
                return Row ^ (3 * Col);
            }

            public override string ToString()
            {
                return Row + "," + Col;
            }
        }

        public class Entry
        {
            public int Row { get; private set; }
            public int Col { get; private set; }
            public Expression Value { get; private set; }

            internal Entry(int row, int col, Expression value)
            {
                Row = row;
                Col = col;
                Value = value;
            }

            public override string ToString()
            {
                return "[" + Row + ", " + Col + "]: " + Value.ToString();
            }
        }

        /// <summary>
        /// Constructs a new expression matrix.
        /// </summary>
        /// <param name="numRows">number of rows</param>
        /// <param name="numCols">number of columns</param>
        public Matrix(int numRows, int numCols)
        {
            NumRows = numRows;
            NumCols = numCols;
        }

        /// <summary>
        /// The number of matrix rows
        /// </summary>
        public int NumRows { get; private set; }

        /// <summary>
        /// The number of matrix columns
        /// </summary>
        public int NumCols { get; private set; }

        private Dictionary<ElementIndex, Expression> _elements = new Dictionary<ElementIndex, Expression>();

        /// <summary>
        /// Gets or sets a matrix element.
        /// </summary>
        /// <param name="row">row</param>
        /// <param name="col">column</param>
        /// <returns>the matrix element at specified row/column pair</returns>
        public Expression this[int row, int col]
        {
            get
            {
                Expression result;
                if (!_elements.TryGetValue(new ElementIndex(row, col), out result))
                    result = SpecialConstant.ScalarZero;
                return result;
            }
            set
            {
                ElementIndex idx = new ElementIndex(row, col);
                if (value.Equals(SpecialConstant.ScalarZero))
                    _elements.Remove(idx);
                else
                    _elements[new ElementIndex(row, col)] = value;
            }
        }

        /// <summary>
        /// Enumerates all matrix elements.
        /// </summary>
        public IEnumerable<Entry> Elements
        {
            get
            {
                foreach (KeyValuePair<ElementIndex, Expression> kvp in _elements)
                {
                    yield return new Entry(kvp.Key.Row, kvp.Key.Col, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Returns a sub-matrix.
        /// </summary>
        /// <param name="row0">start row</param>
        /// <param name="col0">start column</param>
        /// <param name="numRows">height of sub-matrix</param>
        /// <param name="numCols">width of sub-matrix</param>
        /// <returns>the specified sub-matrix</returns>
        public Matrix GetSubRange(int row0, int col0, int numRows, int numCols)
        {
            Matrix result = new Matrix(numRows, numCols);
            int row1 = row0 + numRows;
            int col1 = col0 + numCols;
            IEnumerable<KeyValuePair<ElementIndex, Expression>> range =
                from KeyValuePair<ElementIndex, Expression> kvp in _elements
                where kvp.Key.Row >= row0 && kvp.Key.Row < row1 && kvp.Key.Col >= col0 && kvp.Key.Col < col1
                select kvp;
            foreach (KeyValuePair<ElementIndex, Expression> kvp in range)
                result[kvp.Key.Row, kvp.Key.Col] = kvp.Value;
            return result;
        }

        /// <summary>
        /// Returns the sub-matrix that originates from removing a certain row and a certain column.
        /// </summary>
        /// <param name="killRow">row to remove</param>
        /// <param name="killCol">column to remove</param>
        /// <returns>specified sub-matrix</returns>
        public Matrix KillRowCol(int killRow, int killCol)
        {
            Matrix result = new Matrix(NumRows - 1, NumCols - 1);
            foreach (KeyValuePair<ElementIndex, Expression> kvp in _elements)
            {
                int row = kvp.Key.Row;
                int col = kvp.Key.Col;
                if (row == killRow || col == killCol)
                    continue;
                if (row > killRow)
                    --row;
                if (col > killCol)
                    --col;
                result[row, col] = kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Returns the symbolic determinant of this matrix.
        /// </summary>
        public Expression Det
        {
            get
            {
                if (NumRows == 1 && NumCols == 1)
                    return this[0, 0];

                if (_elements.Count == 0)
                    return SpecialConstant.ScalarZero;

                Dictionary<int, int> rowDensity = new Dictionary<int, int>();
                Dictionary<int, int> colDensity = new Dictionary<int, int>();

                foreach (KeyValuePair<ElementIndex, Expression> kvp in _elements)
                {
                    int count = 0;

                    rowDensity.TryGetValue(kvp.Key.Row, out count);
                    ++count;
                    rowDensity[kvp.Key.Row] = count;

                    colDensity.TryGetValue(kvp.Key.Col, out count);
                    ++count;
                    colDensity[kvp.Key.Col] = count;
                }

                int bestRow, minRowDensity, bestCol, minColDensity;

                bestRow = rowDensity.First().Key;
                minRowDensity = rowDensity.First().Value;
                foreach (KeyValuePair<int, int> kvp in rowDensity)
                {
                    if (kvp.Value < minRowDensity)
                    {
                        bestRow = kvp.Key;
                        minRowDensity = kvp.Value;
                    }
                }

                bestCol = colDensity.First().Key;
                minColDensity = colDensity.First().Value;
                foreach (KeyValuePair<int, int> kvp in colDensity)
                {
                    if (kvp.Value < minColDensity)
                    {
                        bestCol = kvp.Key;
                        minColDensity = kvp.Value;
                    }
                }

                List<Expression> terms = new List<Expression>();
                List<bool> signs = new List<bool>();

                if (minRowDensity < minColDensity)
                {
                    // Develop along row bestRow
                    Matrix drowm = GetSubRange(bestRow, 0, 1, NumCols);
                    foreach (KeyValuePair<ElementIndex, Expression> kvp in drowm._elements)
                    {
                        Expression term = kvp.Value * KillRowCol(bestRow, kvp.Key.Col).Det;
                        bool sign = ((kvp.Key.Col + bestRow) % 2) == 1;
                        terms.Add(term);
                        signs.Add(sign);
                    }
                }
                else
                {
                    // Develop along column bestCol
                    Matrix dcolm = GetSubRange(0, bestCol, NumRows, 1);
                    foreach (KeyValuePair<ElementIndex, Expression> kvp in dcolm._elements)
                    {
                        Expression term = kvp.Value * KillRowCol(kvp.Key.Row, bestCol).Det;
                        bool sign = ((kvp.Key.Row + bestCol) % 2) == 1;
                        terms.Add(term);
                        signs.Add(sign);
                    }
                }

                return Expression.Sum(terms.ToArray(), signs.ToArray()).Simplify();
            }
        }

        /// <summary>
        /// Computes the matrix that one gets by replacing a certain column with the specified vector.
        /// </summary>
        /// <param name="col">column to replace</param>
        /// <param name="b">replacement vector</param>
        /// <returns>the matrix with replaced column</returns>
        public Matrix ReplaceCol(int col, Matrix b)
        {
            if (col < 0 || col >= NumCols ||
                b == null || b.NumCols != 1 || b.NumRows != NumRows)
                throw new InvalidOperationException();

            Matrix result = new Matrix(NumRows, NumCols);
            foreach (KeyValuePair<ElementIndex, Expression> kvp in _elements)
            {
                if (kvp.Key.Col != col)
                    result[kvp.Key.Row, kvp.Key.Col] = kvp.Value;
            }

            foreach (KeyValuePair<ElementIndex, Expression> kvp in b._elements)
            {
                result[kvp.Key.Row, col] = kvp.Value;
            }

            return result;
        }

        /// <summary>
        /// Solves the equation system "this" * x == b symbolically, using Cramer's rule.
        /// </summary>
        /// <param name="b">right side</param>
        /// <returns>x</returns>
        public Matrix SolveByCramer(Matrix b)
        {
            if (b == null || b.NumCols != 1 || b.NumRows != NumRows || NumRows != NumCols)
                throw new InvalidOperationException();

            Matrix result = new Matrix(NumRows, 1);
            Expression det = Det;
            foreach (KeyValuePair<ElementIndex, Expression> kvp in b._elements)
            {
                result[kvp.Key.Row, 0] = ReplaceCol(kvp.Key.Row, b).Det / det;
            }

            return result.Simplify();
        }

        /// <summary>
        /// Solves the equation system "this" * x == b symbolically, using LU decomposition.
        /// </summary>
        /// <param name="veilThreshold">threshold value for expression veiling, in order to avoid overly large expressions</param>
        /// <param name="createVariable">variable creation function</param>
        /// <param name="tempStmts">out parameter to receive the temporary assignments from veiling</param>
        /// <returns>x</returns>
        public Matrix ComputeLU(int veilThreshold, CreateVariableFn createVariable, out List<StoreStatement> tempStmts)
        {
            if (NumRows != NumCols)
                throw new InvalidOperationException("Not a square matrix");

            for (int i = 0; i < NumRows; i++)
                if (this[i, i].Equals(SpecialConstant.ScalarZero))
                    throw new InvalidOperationException("Found zero diagonal element. Apply pivoting first!");

            Matrix lu = new Matrix(NumRows, NumRows);
            LExManager lm = new LExManager()
            {
                VeilThreshold = veilThreshold,
                CreateVariable = createVariable
            };
            for (int i = 0; i < NumRows; i++)
            {
                for (int j = 0; j < NumRows; j++)
                {
                    lu[i, j] = this[i, j];
                }
            }
            for (int i = 0; i < NumRows; i++)
            {
                // Compute U
                for (int j = i; j < NumRows; j++)
                {
                    Expression[] terms = new Expression[i + 1];
                    terms[0] = lu[i, j];
                    bool[] signs = new bool[i + 1];
                    for (int k = 0; k < i; k++)
                    {
                        terms[k + 1] = lu[i, k] * lu[k, j];
                        signs[k + 1] = true;
                    }
                    lu[i, j] = lm.Veil(Expression.Sum(terms, signs).Simplify());
                }
                // Compute L
                for (int j = i + 1; j < NumRows; j++)
                {
                    Expression[] terms = new Expression[i + 1];
                    terms[0] = lu[j, i];
                    bool[] signs = new bool[i + 1];
                    for (int k = 0; k < i; k++)
                    {
                        terms[k + 1] = lu[j, k] * lu[k, i];
                        signs[k + 1] = true;
                    }
                    lu[j, i] = lm.Veil((Expression.Sum(terms, signs) / lu[i, i]).Simplify());
                }
            }
            tempStmts = lm.TempExpressions;
            return lu.Simplify();
        }

        /// <summary>
        /// Solves the equation system "this" * x == b symbolically, using LU decomposition, and
        /// returns a list of variable assignments which solve the specfied equation system.
        /// </summary>
        /// <param name="unknowns">variables that represent "x"</param>
        /// <param name="residual">right side of equation</param>
        /// <param name="createVariable">variable creation function</param>
        /// <param name="veilThreshold">threshold value for expression veiling, in order to avoid overly large expressions</param>
        /// <param name="tempVars">out parameter to receive all created temporary variable</param>
        /// <returns>list of assignments to solve the specified system</returns>
        public List<StoreStatement> ComputeLUAndSolve(Variable[] unknowns, Expression[] residual, 
            CreateVariableFn createVariable, int veilThreshold, out List<Variable> tempVars)
        {
            if (unknowns.Length != NumRows)
                throw new ArgumentException("Number of unknowns must equal dimension!");

            List<StoreStatement> stmts;
            Matrix lu = ComputeLU(veilThreshold, createVariable, out stmts);
            List<Variable> vars = new List<Variable>();
            foreach (StoreStatement stmt in stmts)
                vars.Add((Variable)stmt.Container);

            // Compute forward substitution
            int dim = lu.NumRows;
            Expression[] ys = new Expression[dim];
            for (int i = 0; i < dim; i++)
            {
                Expression[] terms = new Expression[i];
                for (int k = 0; k < i; k++)
                {
                    terms[k] = lu[i, k] * ys[k];
                }
                Expression yi = residual[i] - Expression.Sum(terms);
                yi = yi.Simplify();
                if (yi.Equals(SpecialConstant.ScalarZero))
                {
                    ys[i] = SpecialConstant.ScalarZero;
                }
                else
                {
                    Variable vyi = createVariable("_lud_y" + i, typeof(double));
                    ys[i] = (Expression)vyi;
                    vars.Add(vyi);
                    stmts.Add(new StoreStatement()
                    {
                        Container = vyi,
                        Value = yi
                    });
                }
            }

            // Compute backward substitution
            for (int i = dim - 1; i >= 0; i--)
            {
                Expression[] terms = new Expression[dim - i - 1];
                for (int k = i + 1; k < dim; k++)
                {
                    terms[k - i - 1] = lu[i, k] * unknowns[k];
                }
                Expression xi = (ys[i] - Expression.Sum(terms)) / lu[i, i];
                stmts.Add(new StoreStatement()
                {
                    Container = unknowns[i],
                    Value = xi.Simplify()
                });
            }

            tempVars = vars;
            return stmts;
        }

        public override string ToString()
        {
            string[,] selems = new string[NumRows, NumCols];
            int maxLen = 0;
            for (int i = 0; i < NumRows; i++)
            {
                for (int j = 0; j < NumCols; j++)
                {
                    string selem = this[i, j].ToString();
                    selems[i, j] = selem;
                    maxLen = Math.Max(maxLen, selem.Length);
                }
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < NumRows; i++)
            {
                sb.Append("[ ");
                for (int j = 0; j < NumCols; j++)
                {
                    if (j > 0)
                        sb.Append(" | ");

                    string selem = selems[i, j];
                    sb.Append(selem);
                    int toPad = maxLen - selem.Length;
                    while (toPad-- > 0)
                        sb.Append(" ");
                }
                sb.AppendLine(" ]");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a matrix filled with zeroes as elements.
        /// </summary>
        /// <param name="rows">number of rows</param>
        /// <param name="cols">number of columns</param>
        /// <returns>the zero matrix</returns>
        public static Matrix GetZeroes(int rows, int cols)
        {
            return new Matrix(rows, cols);
        }

        /// <summary>
        /// Returns a symmetric matrix filled with ones as elements.
        /// </summary>
        /// <param name="dim">dimension</param>
        /// <returns>the zero matrix</returns>
        public static Matrix GetZeroes(int dim)
        {
            return new Matrix(dim, dim);
        }

        /// <summary>
        /// Returns a unit matrix.
        /// </summary>
        /// <param name="dim">dimension</param>
        /// <returns>the unit matrix of specified dimension</returns>
        public static Matrix GetUnity(int dim)
        {
            Matrix result = new Matrix(dim, dim);
            for (int i = 0; i < dim; i++)
            {
                result[i, i] = SpecialConstant.ScalarOne;
            }
            return result;
        }

        /// <summary>
        /// Converts an array of expressions to a row vector.
        /// </summary>
        /// <param name="ev">array of expressions</param>
        /// <returns>the row vector</returns>
        public static Matrix RowVector(Expression[] ev)
        {
            Matrix m = new Matrix(ev.Length, 1);
            for (int i = 0; i < ev.Length; i++)
                m[i, 0] = ev[i];
            return m;
        }

        /// <summary>
        /// Flips the sign of each matrix element.
        /// </summary>
        /// <param name="m">matrix to negate</param>
        /// <returns>the negated matrix</returns>
        public static Matrix operator -(Matrix m)
        {
            Matrix r = new Matrix(m.NumRows, m.NumCols);
            foreach (KeyValuePair<ElementIndex, Expression> kvp in m._elements)
            {
                r[kvp.Key.Row, kvp.Key.Col] = -kvp.Value;
            }
            return r;
        }
    }

    /// <summary>
    /// Computes a symbolic matrix norm.
    /// </summary>
    /// <param name="m">matrix</param>
    /// <returns>symbolic matrix norm</returns>
    public delegate Expression Norm(Matrix m);

    /// <summary>
    /// Transforms one expression into another one.
    /// </summary>
    /// <param name="e">expression to transform</param>
    /// <returns>transformed expression</returns>
    public delegate Expression Manipulation(Expression e);

    /// <summary>
    /// This static class provides some standard norms on symbolic matrices.
    /// </summary>
    public static class Norms
    {
        /// <summary>
        /// Takes the absolute value of a matrix element.
        /// </summary>
        /// <param name="e">expression</param>
        /// <returns>absolute value expression</returns>
        public static Expression GetManhattan(Expression e)
        {
            return Expression.Abs(e);
        }

        /// <summary>
        /// Takes the square value of a matrix element.
        /// </summary>
        /// <param name="e">expression</param>
        /// <returns>square value expression</returns>
        public static Expression GetEuclidian(Expression e)
        {
            return e * e;
        }

        /// <summary>
        /// Computes a symbolic matrix norm by applying a transformation to each matrix element
        /// and summing up all transformed elements.
        /// </summary>
        /// <param name="m">matrix</param>
        /// <param name="manip">element-wise transformation</param>
        /// <returns>symbolic norm</returns>
        public static Expression FromElementFn(Matrix m, Manipulation manip)
        {
            List<Expression> terms = new List<Expression>();
            foreach (Matrix.Entry elem in m.Elements)
            {
                terms.Add(manip(elem.Value));
            }
            return Expression.Sum(terms.ToArray(), new bool[terms.Count]);
        }

        /// <summary>
        /// Computes the symbolic Manhattan norm of a matrix.
        /// </summary>
        /// <param name="m">matrix</param>
        /// <returns>the symbolic Manhattan norm</returns>
        public static Expression GetManhattan(Matrix m)
        {
            return FromElementFn(m, GetManhattan);
        }

        /// <summary>
        /// Computes the symbolic Euclidian norm of a matrix.
        /// </summary>
        /// <param name="m">matrix</param>
        /// <returns>the symbolic Euclidian norm</returns>
        public static Expression GetEuclidian(Matrix m)
        {
            return FromElementFn(m, GetEuclidian);
        }

        /// <summary>
        /// Returns the Manhattan norm.
        /// </summary>
        public static Norm Manhattan
        {
            get { return x => GetManhattan(x); }
        }

        /// <summary>
        /// Returns the Euclidian norm.
        /// </summary>
        public static Norm Euclidian
        {
            get { return x => GetEuclidian(x); }
        }
    }

}

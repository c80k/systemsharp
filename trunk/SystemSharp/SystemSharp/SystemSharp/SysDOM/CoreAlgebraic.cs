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
    public interface IEvaluator
    {
        object EvalConstant(Constant constant);
        object EvalVariable(Variable variable);
        object EvalSignalRef(SignalRef signalRef);
        object EvalFieldRef(FieldRef fieldRef);
        object EvalThisRef(ThisRef thisRef);
        object EvalArrayRef(ArrayRef arrayRef);
        object EvalLiteral(ILiteral lit);
        object EvalFunction(FunctionCall funcref, object[] args, TypeDescriptor resultType);
        object Neg(object v, TypeDescriptor resultType);
        object BoolNot(object v, TypeDescriptor resultType);
        object BitwiseNot(object v, TypeDescriptor resultType);
        object Add(object v1, object v2, TypeDescriptor resultType);
        object Sub(object v1, object v2, TypeDescriptor resultType);
        object Mul(object v1, object v2, TypeDescriptor resultType);
        object Div(object v1, object v2, TypeDescriptor resultType);
        object Rem(object v1, object v2, TypeDescriptor resultType);
        object And(object v1, object v2, TypeDescriptor resultType);
        object Or(object v1, object v2, TypeDescriptor resultType);
        object Xor(object v1, object v2, TypeDescriptor resultType);
        object LShift(object v1, object v2, TypeDescriptor resultType);
        object RShift(object v1, object v2, TypeDescriptor resultType);
        object Concat(object v1, object v2, TypeDescriptor resultType);
        object Exp(object v, TypeDescriptor resultType);
        object Exp(object v1, object v2, TypeDescriptor resultType);
        object Log(object v, TypeDescriptor resultType);
        object Log(object v1, object v2, TypeDescriptor resultType);
        object Abs(object v, TypeDescriptor resultType);
        object Sin(object v, TypeDescriptor resultType);
        object Cos(object v, TypeDescriptor resultType);
        object ExtendSign(object v, TypeDescriptor resultType);
        object E(TypeDescriptor resultType);
        object PI(TypeDescriptor resultType);
        object ScalarOne(TypeDescriptor resultType);
        object ScalarZero(TypeDescriptor resultType);
        object True(TypeDescriptor resultType);
        object False(TypeDescriptor resultType);
        object Slice(object v, object lo, object hi, TypeDescriptor resultType);
        object Time(TypeDescriptor resultType);
        object IsLessThan(object v1, object v2, TypeDescriptor resultType);
        object IsLessThanOrEqual(object v1, object v2, TypeDescriptor resultType);
        object IsEqual(object v1, object v2, TypeDescriptor resultType);
        object IsNotEqual(object v1, object v2, TypeDescriptor resultType);
        object IsGreaterThanOrEqual(object v1, object v2, TypeDescriptor resultType);
        object IsGreaterThan(object v1, object v2, TypeDescriptor resultType);
        object ConditionallyCombine(object cond, object thn, object els, TypeDescriptor resultType);
    }

    public interface IEvaluable
    {
        object Eval(IEvaluator eval);
    }

    #region Stringification

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

    public interface IOperatorPrecedence
    {
        int GetOperatorOrder(UnOp.Kind op);
        int GetOperatorOrder(BinOp.Kind op);
        int GetOperatorOrder(TernOp.Kind op);
        EOperatorAssociativity GetOperatorAssociativity(UnOp.Kind op);
        EOperatorAssociativity GetOperatorAssociativity(BinOp.Kind op);
        EOperatorAssociativity GetOperatorAssociativity(TernOp.Kind op);
    }

    public delegate string NotateFunc(params string[] args);
    public delegate string FunctionNotateFunc(ICallable callee, params string[] args);
    public delegate string LiteralNotateFunc(ILiteral literal, LiteralReference.EMode mode);
    public delegate string BracketNotateFunc(string arg);

    public static class DefaultNotators
    {
        public static string LiteralName(ILiteral literal, LiteralReference.EMode mode)
        {
            var name = literal.ToString();
            if (mode == LiteralReference.EMode.ByAddress)
                name = "@{" + name + "}";
            return name;
        }

        public static string Bracket(string expr)
        {
            return "(" + expr + ")";
        }

        public static string NotatePrefix(string symbol, params string[] args)
        {
            return symbol + args[0];
        }

        public static NotateFunc Prefix(string symbol)
        {
            return (string[] args) => NotatePrefix(symbol, args);
        }

        public static string NotateInfix(string symbol, params string[] args)
        {
            return args[0] + " " + symbol + " " + args[1];
        }

        public static NotateFunc Infix(string symbol)
        {
            return (string[] args) => NotateInfix(symbol, args);
        }

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

        public static string NotateFunctionCall(ICallable callee, params string[] args)
        {
            return NotateFunction(callee.Name, args);
        }

        public static NotateFunc Function(string symbol)
        {
            return (string[] args) => NotateFunction(symbol, args);
        }
    }

    public interface IOperatorNotation
    {
        NotateFunc GetNotation(UnOp.Kind op);
        NotateFunc GetNotation(BinOp.Kind op);
        NotateFunc GetNotation(TernOp.Kind op);
        FunctionNotateFunc GetFunctionNotation();
        string GetSpecialConstantSymbol(SpecialConstant.Kind constant);
        LiteralNotateFunc GetLiteralNotation();
        BracketNotateFunc GetBracketNotation();
    }

    public interface IStringifyInfo
    {
        IOperatorPrecedence Precedence { get; }
        IOperatorNotation Notation { get; }
        Action<Expression> OnStringifyExpression { get; }
    }

    #endregion

    #region DefaultStringification

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

    public class CSharpStringifyInfo : IStringifyInfo
    {
        public IOperatorPrecedence Precedence { get { return CSharpOperatorPrecedence.Instance; } }
        public IOperatorNotation Notation { get { return CSharpOperatorNotation.Instance; } }
        public Action<Expression> OnStringifyExpression { get; set; }
        public static readonly CSharpStringifyInfo Instance = new CSharpStringifyInfo();
    }

    public interface IExpression
    {
        object Eval(IEvaluator eval);
    }

    public enum EResultTypeClass
    {
        Algebraic,
        Integral,
        Boolean,
        ObjectReference,
        Unknown
    }

    public static class ResultTypeClasses
    {
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

    [ContractClass(typeof(AttributedContractClass))]
    public interface IAttributed
    {
        void AddAttribute(object attr);
        bool RemoveAttribute<T>();
        T QueryAttribute<T>();
        bool HasAttribute<T>();
        IEnumerable<object> Attributes { get; }
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

    public abstract class Expression : 
        AttributedObject,
        IExpression
    {
        public delegate bool MatchFunction(Expression e);
        public delegate void OnExpressionEvaluatedFn(Expression expr, object value);

        public abstract object Eval(IEvaluator eval, OnExpressionEvaluatedFn efn);

        public object Eval(IEvaluator eval)
        {
            return Eval(eval, (e, v) => { });
        }

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

        public abstract int GetPrecedence(IOperatorPrecedence prec);

        public virtual int Arity
        {
            get { return _children.Length; }
            protected set
            {
                _children = new Expression[value];
            }
        }
        
        public virtual Expression[] Children
        {
            get { return _children; }
        }

        protected abstract Expression CloneThisImpl(Expression[] newChildren);

        public Expression CloneThis(Expression[] newChildren)
        {
            var result = CloneThisImpl(newChildren);
            result.CopyAttributesFrom(this);
            return result;
        }

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

        public abstract ResultType Accept<ResultType>(IExpressionVisitor<ResultType> vtor);
        public abstract bool NodeEquals(Expression e);
        public abstract bool DeepEquals(Expression e);
        public abstract EResultTypeClass ResultTypeClass { get; }

        public virtual Expression Transform(IExpressionTransformer xform)
        {
            return Accept(xform);
        }

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

        public object Cookie { get; set; }

        protected TypeDescriptor _resultType;

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
                //Contract.Requires(value != null);
                if (value == null)
                    throw new ArgumentException();
                _resultType = value;
            }
        }

        public void ClearCookies()
        {
            Cookie = null;
            foreach (Expression child in Children)
                child.ClearCookies();
        }

        private int _cachedHashCode;

        protected abstract int ComputeHashCode();

        public override int GetHashCode()
        {
            if (_cachedHashCode == 0)
                _cachedHashCode = ComputeHashCode();
            return _cachedHashCode;
        }

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

        public static Expression operator +(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Add,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator -(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Sub,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator *(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Mul,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator /(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Div,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator %(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Rem,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator &(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.And,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator |(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Or,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator ^(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Xor,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression operator -(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Neg,
                Operand = e
            };
        }

        public static Expression operator ~(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.BitwiseNot,
                Operand = e
            };
        }

        public static Expression operator !(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.BoolNot,
                Operand = e,
                ResultType = typeof(bool)
            };
        }

        public static Expression Id(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Identity,
                Operand = e
            };
        }

        public static Expression Exp(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Exp,
                Operand = e
            };
        }

        public static Expression Log(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Log,
                Operand = e
            };
        }

        public static Expression Abs(Expression e)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.Abs,
                Operand = e
            };
        }

        public static Expression ExtendSign(Expression e, TypeDescriptor targetType)
        {
            return new UnOp()
            {
                Operation = UnOp.Kind.ExtendSign,
                Operand = e,
                ResultType = targetType
            };
        }

        public static Expression Pow(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Exp,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression Log(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Log,
                Operand1 = e1,
                Operand2 = e2
            };
        }

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

        public static Expression LShift(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.LShift,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression RShift(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.RShift,
                Operand1 = e1,
                Operand2 = e2
            };
        }

        public static Expression Concat(Expression e1, Expression e2)
        {
            return new BinOp()
            {
                Operation = BinOp.Kind.Concat,
                Operand1 = e1,
                Operand2 = e2
            };
        }

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

        public static Expression Constant(double value)
        {
            if (value == 0.0)
                return SpecialConstant.ScalarZero;
            else if (value == 1.0)
                return SpecialConstant.ScalarOne;
            else
                return LiteralReference.Constant(value);
        }

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

        public static Expression Sum(Expression[] exprs)
        {
            return Sum(exprs, new bool[exprs.Length]);
        }

        public static Expression Ceil(Expression expr)
        {
            return new UnOp() { Operation = UnOp.Kind.Ceil, Operand = expr };
        }

        public static Expression Floor(Expression expr)
        {
            return new UnOp() { Operation = UnOp.Kind.Floor, Operand = expr };
        }

        public static Expression Min(Expression a, Expression b)
        {
            return new BinOp() { Operation = BinOp.Kind.Min, Operand1 = a, Operand2 = b };
        }

        public static Expression Max(Expression a, Expression b)
        {
            return new BinOp() { Operation = BinOp.Kind.Max, Operand1 = a, Operand2 = b };
        }
    }

    public class ReplacementRule
    {
        private Expression.MatchFunction _matchFn;
        private ExpressionGenerator _gen;

        public ReplacementRule(Expression.MatchFunction matchFn, ExpressionGenerator gen)
        {
            _matchFn = matchFn;
            _gen = gen;
        }

        public ReplacementRule(Matching m, Generation g)
        {
            _matchFn = m;
            _gen = g;
        }

        public Expression ApplyOnce(Expression e, out bool hit)
        {
            return e.Replace(_matchFn, _gen, out hit);
        }

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

    public abstract class ElementReference : Expression
    {
        public override int GetPrecedence(IOperatorPrecedence prec)
        {
            return -1;
        }
    }

    public class LiteralReference : ElementReference
    {
        public enum EMode
        {
            Direct,
            ByAddress
        }

        public LiteralReference(ILiteral referencedObject, EMode mode = EMode.Direct)
        {
            Arity = 0;
            ReferencedObject = referencedObject;
            Mode = mode;
        }

        public ILiteral ReferencedObject { get; private set; }
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

    public class SpecialConstant : ElementReference
    {
        public enum Kind
        {
            E,
            PI,
            ScalarZero,
            ScalarOne,
            True,
            False
        }

        public SpecialConstant()
        {
            Arity = 0;
        }

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
                return false;
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

    public class UnOp : Expression
    {
        public enum Kind
        {
            Identity,
            Neg,
            BoolNot,
            BitwiseNot,
            ExtendSign,
            Exp,
            Log,
            Abs,
            Sin,
            Cos,
            Sqrt,
            Ceil,
            Floor
        }

        public UnOp()
        {
            Arity = 1;
        }

        public Kind Operation { get; set; }

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

    public class BinOp : Expression
    {
        public enum Kind
        {
            Add,
            Sub,
            Mul,
            Div,
            Rem,
            And,
            Or,
            Xor,
            LShift,
            RShift,
            Concat,
            Exp,
            Log,
            Eq,
            Gt,
            GtEq,
            Lt,
            LtEq,
            NEq,
            Min,
            Max
        }

        public BinOp()
        {
            Arity = 2;
        }

        public Kind Operation { get; set; }

        public Expression Operand1 
        {
            get { return Children[0]; }
            set { Children[0] = value; }

        }
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
                return false;
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

    public class TernOp : Expression
    {
        public enum Kind
        {
            Slice,
            Conditional
        }

        public TernOp()
        {
            Arity = 3;
        }

        public Kind Operation { get; set; }

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
                return false;
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

    public interface ICallable
    {
        string Name { get; }
    }

    public class FunctionRef: ICallable
    {
        public string Name { get; private set; }
        public Function Implementation { get; private set; }

        public FunctionRef(string name, Function impl)
        {
            Name = name;
            Implementation = impl;
        }
    }

    public class FunctionSpec : ICallable
    {
        public FunctionSpec(TypeDescriptor resultType)
        {
            Contract.Requires(resultType != null);
            _resultType = resultType;
        }

        public MethodBase CILRep { get; internal set; }
        public MethodDescriptor GenericSysDOMRep { get; internal set; }
        public MethodDescriptor SpecialSysDOMRep { get; internal set; }
        public XILSFunction XILSRep { get; internal set; }
        public XILSFunction XIL3Rep { get; internal set; }
        public IntrinsicFunction IntrinsicRep { get; internal set; }

        private TypeDescriptor _resultType;
        public TypeDescriptor ResultType
        {
            get { return _resultType; }
        }

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

    public class FunctionCall : Expression
    {
        public ICallable Callee { get; set; }

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
                return false;
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
            get
            {
                return SetResultTypeClass;
            }
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

    public class LazyExpression : Expression
    {
        private Expression _placeHolder;
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
                return PlaceHolder.Equals(obj);
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

    public interface IExpressionVisitor<ResultType>
    {
        ResultType TransformLiteralReference(LiteralReference expr);
        ResultType TransformSpecialConstant(SpecialConstant expr);
        ResultType TransformUnOp(UnOp expr);
        ResultType TransformBinOp(BinOp expr);
        ResultType TransformTernOp(TernOp expr);
        ResultType TransformFunction(FunctionCall expr);
        //ResultType TransformDerivative(Derivative expr);
    }

    public interface IExpressionTransformer: IExpressionVisitor<Expression>
    {
    }

    #endregion

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

        public Matrix(int numRows, int numCols)
        {
            NumRows = numRows;
            NumCols = numCols;
        }

        public int NumRows { get; private set; }
        public int NumCols { get; private set; }

        private Dictionary<ElementIndex, Expression> _elements = new Dictionary<ElementIndex, Expression>();

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

        public static Matrix GetZeroes(int rows, int cols)
        {
            return new Matrix(rows, cols);
        }

        public static Matrix GetZeroes(int dim)
        {
            return new Matrix(dim, dim);
        }

        public static Matrix GetUnity(int dim)
        {
            Matrix result = new Matrix(dim, dim);
            for (int i = 0; i < dim; i++)
            {
                result[i, i] = SpecialConstant.ScalarOne;
            }
            return result;
        }

        public static Matrix RowVector(Expression[] ev)
        {
            Matrix m = new Matrix(ev.Length, 1);
            for (int i = 0; i < ev.Length; i++)
                m[i, 0] = ev[i];
            return m;
        }

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

    public delegate Expression Norm(Matrix m);
    public delegate Expression Manipulation(Expression e);

    public static class Norms
    {
        public static Expression GetManhattan(Expression e)
        {
            return Expression.Abs(e);
        }

        public static Expression GetEuclidian(Expression e)
        {
            return e * e;
        }

        public static Expression FromElementFn(Matrix m, Manipulation manip)
        {
            List<Expression> terms = new List<Expression>();
            foreach (Matrix.Entry elem in m.Elements)
            {
                terms.Add(manip(elem.Value));
            }
            return Expression.Sum(terms.ToArray(), new bool[terms.Count]);
        }

        public static Expression GetManhattan(Matrix m)
        {
            return FromElementFn(m, GetManhattan);
        }

        public static Expression GetManhattan(object x)
        {
            if (x is Expression)
                return GetManhattan((Expression)x);
            else
                return GetManhattan((Matrix)x);
        }

        public static Expression GetEuclidian(Matrix m)
        {
            return FromElementFn(m, GetEuclidian);
        }

        public static Expression GetEuclidian(object x)
        {
            if (x is Expression)
                return GetEuclidian((Expression)x);
            else
                return GetEuclidian((Matrix)x);
        }

        public static Norm Manhattan
        {
            get
            {
                return x => GetManhattan(x);
            }
        }

        public static Norm Euclidian
        {
            get
            {
                return x => GetEuclidian(x);
            }
        }
    }

}

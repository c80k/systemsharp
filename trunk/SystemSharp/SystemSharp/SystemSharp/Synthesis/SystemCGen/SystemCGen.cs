

/**
 * Copyright 2011-2012 Christian Köllner
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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using SystemSharp.Algebraic;
using SystemSharp.Analysis;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Meta.M2M;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.Util;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Analysis;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.Synthesis.SystemCGen
{
    public class SystemCGenerator :
        ICodeGenerator,
        IOperatorNotation,
        IOperatorPrecedence,
        IStringifyInfo
    {
        public interface IGeneratorInformation
        {
            string CurrentFile { get; }
        }

        private class SysCExtraLib
        {
            public string Name { get; private set; }
            public byte[] FileContent { get; private set; }

            public SysCExtraLib(string name, byte[] fileContent)
            {
                Name = name;
                FileContent = fileContent;
            }

            public SysCExtraLib(string name)
            {
                Name = name;
            }
        }

        private class SysCPkg
        {
            public string Name { get; private set; }
            public byte[] FileContent { get; private set; }

            public SysCPkg(string name, byte[] fileContent)
            {
                Name = name;
                FileContent = fileContent;
            }

            public SysCPkg(string name)
            {
                Name = name;
            }
        }

        private class SysCLib
        {
            public string Name { get; private set; }
            public List<SysCPkg> Packages { get; private set; }
            public SysCLib(string name)
            {
                Name = name;
                Packages = new List<SysCPkg>();
            }
        }

        private class GeneratorInfo : IGeneratorInformation
        {
            public GeneratorInfo(string file)
            {
                CurrentFile = file;
            }

            #region IGeneratorInformation Member

            public string CurrentFile { get; private set; }

            #endregion
        }

        private static List<SysCLib> _stdLibraries = new List<SysCLib>();
        private static List<SysCLib> _extraLibraries = new List<SysCLib>();

        private Time SimTime;

        //      ALTERAR !!!
        private static void InitializeStdLibraries()
        {
            SysCLib systemc = new SysCLib("systemc");
            _stdLibraries.Add(systemc);
            //   TODO
            //SysCLib work = new SysCLib("work");
            //work.Packages.Add(new SysCPkg("sc_logic_not", SystemSharp.Properties.Resources.sc_logic_not));
            //work.Packages.Add(new SysCPkg("logic_vector_ArithOp", SystemSharp.Properties.Resources.logic_vector_ArithOp));
        }

        static SystemCGenerator()
        {
            //InitializeTypeMap();
            InitializeStdLibraries();
        }

        private IComponentDescriptor _curComponent;

        private string Convert(Type SType, TypeDescriptor TTypeD, params string[] args)
        {
            List<Type> itypes = new List<Type>();
            string result = SystemCTypes.Convert(SType, TTypeD, itypes, args);
            foreach (Type itype in itypes)
            {
                TypeInfo ti;
                LookupType(itype, out ti);
            }
            return result;
        }

        #region IOperatorPrecedence Members

        public int GetOperatorOrder(UnOp.Kind op)
        {
            switch (op)
            {
                case UnOp.Kind.Abs: return 0;
                case UnOp.Kind.BitwiseNot: return 0;
                case UnOp.Kind.BoolNot: return 0;
                case UnOp.Kind.Exp: return 0;
                case UnOp.Kind.ExtendSign: return -1;
                case UnOp.Kind.Identity: return -1;
                case UnOp.Kind.Log: return -1;
                case UnOp.Kind.Neg: return 2;
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
                case BinOp.Kind.And: return 6;
                case BinOp.Kind.Concat: return 3;
                case BinOp.Kind.Div: return 1;
                case BinOp.Kind.Eq: return 5;
                case BinOp.Kind.Exp: return 0;
                case BinOp.Kind.Gt: return 5;
                case BinOp.Kind.GtEq: return 5;
                case BinOp.Kind.Log: return -1;
                case BinOp.Kind.LShift: return 4;
                case BinOp.Kind.Lt: return 5;
                case BinOp.Kind.LtEq: return 5;
                case BinOp.Kind.Mul: return 1;
                case BinOp.Kind.NEq: return 5;
                case BinOp.Kind.Or: return 6;
                case BinOp.Kind.Rem: return 1;
                case BinOp.Kind.RShift: return 4;
                case BinOp.Kind.Sub: return 3;
                case BinOp.Kind.Xor: return 6;
                default: throw new NotImplementedException();
            }
        }

        public int GetOperatorOrder(TernOp.Kind op)
        {
            switch (op)
            {
                case TernOp.Kind.Conditional: return -1;
                //case TernOp.Kind.Slice: return -1;
                default: throw new NotImplementedException();
            }
        }

        public EOperatorAssociativity GetOperatorAssociativity(UnOp.Kind op)
        {
            return EOperatorAssociativity.UseParenthesis;
        }

        public EOperatorAssociativity GetOperatorAssociativity(BinOp.Kind op)
        {
            return EOperatorAssociativity.UseParenthesis;
        }

        public EOperatorAssociativity GetOperatorAssociativity(TernOp.Kind op)
        {
            return EOperatorAssociativity.RightAssociative;
        }

        #endregion

        #region IOperatorNotation Members

        //      ALTERAR !!!
        public string GetSpecialConstantSymbol(SpecialConstant.Kind constant)
        {
            switch (constant)
            {
                case SpecialConstant.Kind.E: return "MATH_E";
                case SpecialConstant.Kind.False: return "false";
                case SpecialConstant.Kind.PI: return "MATH_PI";
                case SpecialConstant.Kind.ScalarOne: return "1.0";
                case SpecialConstant.Kind.ScalarZero: return "0.0";
                case SpecialConstant.Kind.True: return "true";
                default: throw new NotImplementedException();
            }
        }

        //      Simplified version
        public NotateFunc GetNotation(UnOp.Kind op)
        {
            switch (op)
            {
                case UnOp.Kind.BitwiseNot: return DefaultNotators.Prefix("~");
                case UnOp.Kind.BoolNot: return DefaultNotators.Prefix("!");
                case UnOp.Kind.Exp: return DefaultNotators.Function("exp");
                case UnOp.Kind.ExtendSign: return DefaultNotators.Function("XTS");
                case UnOp.Kind.Identity: return DefaultNotators.Prefix("");
                case UnOp.Kind.Log: return DefaultNotators.Function("log");
                case UnOp.Kind.Neg: return DefaultNotators.Prefix("-");
                case UnOp.Kind.Sin: return DefaultNotators.Function("sin");
                case UnOp.Kind.Cos: return DefaultNotators.Function("cos");
                default: throw new NotImplementedException();
            }
        }

        //      Simplified version
        public NotateFunc GetNotation(BinOp.Kind op)
        {
            switch (op)
            {
                case BinOp.Kind.Add: return DefaultNotators.Infix("+");
                case BinOp.Kind.And: return DefaultNotators.Infix("&&");
                case BinOp.Kind.Concat: return DefaultNotators.Function("concat");
                case BinOp.Kind.Div: return DefaultNotators.Infix("/");
                case BinOp.Kind.Eq: return DefaultNotators.Infix("==");
                case BinOp.Kind.Exp: return DefaultNotators.Infix("pow"); // pow(a, b) ???? #include "math.h" !!!!
                case BinOp.Kind.Gt: return DefaultNotators.Infix(">");
                case BinOp.Kind.GtEq: return DefaultNotators.Infix(">=");
                case BinOp.Kind.Log: return DefaultNotators.Function("log");
                case BinOp.Kind.LShift: return DefaultNotators.Infix("<<");
                case BinOp.Kind.Lt: return DefaultNotators.Infix("<");
                case BinOp.Kind.LtEq: return DefaultNotators.Infix("<=");
                case BinOp.Kind.Mul: return DefaultNotators.Infix("*");
                case BinOp.Kind.NEq: return DefaultNotators.Infix("!=");
                case BinOp.Kind.Or: return DefaultNotators.Infix("||");
                case BinOp.Kind.Rem: return DefaultNotators.Infix("%");
                case BinOp.Kind.RShift: return DefaultNotators.Infix(">>");
                case BinOp.Kind.Sub: return DefaultNotators.Infix("-");
                case BinOp.Kind.Xor: return DefaultNotators.Infix("^");
                default: throw new NotImplementedException();
            }
        }

        //      Simplified version
        public NotateFunc GetNotation(TernOp.Kind op)
        {
            switch (op)
            {
                case TernOp.Kind.Conditional:
                    return (string[] args) => args[1] + " ? " + args[0] + " : " + args[2];

                case TernOp.Kind.Slice:
                    return (string[] args) => args[0] + "(" + args[1] + ", " + args[2] + ")";

                default:
                    throw new NotImplementedException();
            }
        }

        private string NotateFunctionCallInternal(ICallable callee, IEnumerable<string> args)
        {
            StringBuilder sb = new StringBuilder();
            var fspec = (FunctionSpec)callee;
            string mname = _sim.GetUniqueName(callee.Name, fspec.GenericSysDOMRep);
            sb.Append(mname);
            if (args.Count() > 0)
            {
                sb.Append("(");
                sb.Append(string.Join(", ", args));
                sb.Append(")");
            }
            else
                sb.Append("()");
            return sb.ToString();
        }

        private string NotateFunctionCall(ICallable callee, params string[] args)
        {
            FunctionSpec fspec = (FunctionSpec)callee;
            if (fspec.IntrinsicRep != null)
            {
                IntrinsicFunction ifun = fspec.IntrinsicRep;
                switch (ifun.Action)
                {
                    case IntrinsicFunction.EAction.Convert:
                        {
                            CastParams parms = (CastParams)ifun.Parameter;
                            return Convert(parms.SourceType, parms.DestType, args);
                        }

                    case IntrinsicFunction.EAction.Cos:
                        return DefaultNotators.NotateFunction("cos", args);

                    //case IntrinsicFunction.EAction.CurveGradient:
                    //    // actually not supported/allowed
                    //    return DefaultNotators.NotateFunction("-- CurveGradient", args);

                    //case IntrinsicFunction.EAction.CurveLERP:
                    //    // actually not supported/allowed
                    //    return DefaultNotators.NotateFunction("-- CurveLERP", args);

                    //case IntrinsicFunction.EAction.CurveLERP_ZF:
                    //    // actually not supported/allowed
                    //    return DefaultNotators.NotateFunction("-- CurveLERP_ZF", args);

                    case IntrinsicFunction.EAction.GetArrayElement:
                        string[] aux = args[0].Split('.');
                        if (aux.Length > 1)
                            return aux[0] + "[" + args[1] + "]." + aux[1];
                        else
                            return args[0] + "[" + args[1] + "]";
                    // args[0] -> nome do array/ tipo de dados???; args[1] -> índice ???
                    case IntrinsicFunction.EAction.Index:

                        return args[0] + "[" + args[1] + "]";

                    //      ????
                    case IntrinsicFunction.EAction.GetArrayLength:
                        //  .length returns the BIT WIDTH!!!!
                        return args[0] + ".length()";

                    case IntrinsicFunction.EAction.NewArray:
                        {
                            ArrayParams aparams = (ArrayParams)ifun.Parameter;
                            if (aparams.IsStatic)
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.Append("(");
                                bool first = true;

                                foreach (Expression elem in aparams.Elements)
                                {
                                    if (first)
                                        first = false;
                                    else
                                        sb.Append(", ");
                                    if (elem == null)
                                    {
                                        // fixme
                                        sb.Append("<null>");
                                    }
                                    else
                                    {
                                        sb.Append(elem.ToString(this));
                                    }
                                }
                                sb.Append(")");
                                return sb.ToString();
                            }
                            else
                            {
                                return "// new array of type " + aparams.ElementType.Name + " with " + args[0] + " elements";
                            }
                        }

                    case IntrinsicFunction.EAction.NewObject:
                    //return "new(" + args[0] + ")";

                    case IntrinsicFunction.EAction.PropertyRef:
                        {
                            object prop = ifun.Parameter;
                            if (prop is DesignContext.EProperties)
                            {
                                DesignContext.EProperties rprop = (DesignContext.EProperties)prop;
                                switch (rprop)
                                {
                                    case DesignContext.EProperties.CurTime:
                                        return "sc_time_stamp()";

                                    case DesignContext.EProperties.State:
                                        // actually not supported/allowed
                                        return "-- SimState not supported";

                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                            else if (prop is ESizedProperties)
                            {
                                ESizedProperties rprop = (ESizedProperties)prop;
                                switch (rprop)
                                {
                                    case ESizedProperties.Size:
                                        return args[0] + ".length()";

                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                            else
                                throw new NotImplementedException();
                        }
                    //break;

                    case IntrinsicFunction.EAction.Report:
                    //return "cout << " + args[0];
                    case IntrinsicFunction.EAction.ReportLine:
                        {
                            // BUG: na separacao de sub-strings, a separacao é feita indiscriminadamente pelo caracter '+'
                            //string[] parameters = args[0].Split('+');
                            //StringBuilder sb = new StringBuilder();
                            //sb.Append("cout << " + args[0]);
                            ////for (int i = 0; i < parameters.Length; i++)
                            ////{
                            ////    sb.Append("string(" + parameters[i] + ")" + " + ");
                            ////}
                            //sb.Append(" << endl");
                            //return sb.ToString();
                            return "cout << " + args[0] + " << endl";
                        }


                    case IntrinsicFunction.EAction.Sign:
                        return DefaultNotators.NotateFunction("SIGN", args);

                    case IntrinsicFunction.EAction.SimulationContext:
                        // actually not supported/allowed
                        return "// SimContext not supported";

                    case IntrinsicFunction.EAction.Sin:
                        return DefaultNotators.NotateFunction("sin", args);

                    case IntrinsicFunction.EAction.Slice:
                        {
                            if (ifun.Parameter != null)
                            {
                                // static slice range
                                var range = (Range)ifun.Parameter;
                                //if (range.Direction == EDimDirection.Downto)
                                //    return args[0] + "(" + range.SecondBound + ", " + range.FirstBound + ")";
                                //else                                
                                return args[0] + "(" + range.FirstBound + ", " + range.SecondBound + ")";
                            }
                            else
                            {
                                // dynamic slice range
                               // if()
                                return args[0] + "(" + args[1] + ", " + args[2] + ")";
                            }
                        }

                    case IntrinsicFunction.EAction.Sqrt:
                        return DefaultNotators.NotateFunction("sqrt", args);

                    case IntrinsicFunction.EAction.StringConcat:
                        {
                            StringBuilder sb = new StringBuilder();
                            for (int i = 0; i < args.Length; i++)
                            {
                                if (i > 0)
                                    sb.Append(" << ");  // not general case...(suitable for "cout")
                                sb.Append(args[i]);
                            }
                            return sb.ToString();
                        }

                    case IntrinsicFunction.EAction.Wait:
                        {
                            WaitParams wparams = (WaitParams)ifun.Parameter;
                            switch (wparams.WaitKind)
                            {
                                case WaitParams.EWaitKind.WaitFor:
                                    return "wait(" + args[0] + ")";

                                case WaitParams.EWaitKind.WaitOn:
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.Append("wait()");
                                        //bool first = true;
                                        //foreach (string arg in args)
                                        //{
                                        //    if (first)
                                        //        first = false;
                                        //    else
                                        //        sb.Append(", ");
                                        //    sb.Append(arg);
                                        //}
                                        return sb.ToString();
                                    }

                                case WaitParams.EWaitKind.WaitUntil:
                                    {
                                        args[0] = args[0].Replace("posedge", "posedge_event");
                                        args[0] = args[0].Replace("negedge", "negedge_event");
                                        return "wait(" + args[0] + ")";
                                    }
                                default:
                                    return "WAIT NAO RECONHECIDO";
                                //throw new NotImplementedException();
                            }
                        }

                    case IntrinsicFunction.EAction.Resize:
                        {
                            string result = args[0];
                            //ResizeParams rparams = (ResizeParams)ifun.Parameter;
                            //string result = "resize(" + args[0] + ", ";
                            //if (rparams == null)
                            //{
                            //    if (args.Length == 2)
                            //    {
                            //        result += args[1];
                            //    }
                            //    else
                            //    {
                            //        result += (args[1] + "-1");
                            //        if (args.Length > 2)
                            //        {
                            //            result += ", -" + args[2];
                            //        }
                            //    }
                            //}
                            //else
                            //{
                            //    result += (rparams.NewIntWidth - 1);
                            //    if (rparams.NewFracWidth > int.MinValue)
                            //        result += ", " + (-rparams.NewFracWidth);
                            //}
                            //result += ")";
                            return result;
                        }

                    case IntrinsicFunction.EAction.Barrier:
                        {
                            return "// barrier";
                        }

                    //      necessary??
                    case IntrinsicFunction.EAction.MkDownRange:
                        {
                            return "(" + args[0] + " ," + args[1] + ")";
                        }

                    case IntrinsicFunction.EAction.MkUpRange:
                        {
                            return "(" + args[0] + " ," + args[1] + ")";
                        }

                    case IntrinsicFunction.EAction.Abs:
                        {
                            return "abs(" + args[0] + ")";
                        }

                    default:
                        throw new NotImplementedException();
                }
            }
            else if (fspec.CILRep != null)
            {
                MethodBase method = fspec.CILRep;
                if (!method.IsStatic && !method.IsConstructor)
                {
                    return NotateFunctionCallInternal(callee, args.Skip(1));
                }
                else
                {
                    return NotateFunctionCallInternal(callee, args);
                }
            }
            else
            {
                return NotateFunctionCallInternal(callee, args);
            }
        }

        public FunctionNotateFunc GetFunctionNotation()
        {
            return NotateFunctionCall;
        }

        private string SystemCifyConstant(object value)
        {
            return GetValueID(value);
        }

        private string SystemCifyConstant(object value, string name)
        {
            return GetValueID(value, name);
        }

        class LiteralStringifier : ILiteralVisitor
        {
            private SystemCGenerator _SysCg;
            public string Result { get; private set; }
            public LiteralReference.EMode Mode { get; private set; }

            public LiteralStringifier(SystemCGenerator SysCg, LiteralReference.EMode mode)
            {
                _SysCg = SysCg;
                Mode = mode;
            }

            #region ILiteralVisitor Member

            public void VisitConstant(Constant constant)
            {
                object value = constant.ConstantValue;
                if (constant.Type.CILType.IsEnum)
                    Result = _SysCg.GetTypeDescriptorName(constant.Type) + "::" + _SysCg.SystemCifyConstant(value);
                else
                    Result = _SysCg.SystemCifyConstant(value);
            }

            public void VisitVariable(Variable variable)
            {
                Result = _SysCg.MakeIDName(variable.Name, variable);
            }

            public void VisitFieldRef(FieldRef fieldRef)
            {
                Result = _SysCg.MakeIDName(fieldRef.Name, fieldRef.FieldDesc);
            }

            public void VisitThisRef(ThisRef thisRef)
            {
                Result = "this";
            }

            public void VisitSignalRef(SignalRef signalRef)
            {
                ISignalOrPortDescriptor desc = signalRef.Desc;
                SignalRef remappedRef = signalRef;
                if (_SysCg._curComponent != null &&
                    !(signalRef.Desc is SignalArgumentDescriptor))
                {
                    /*
                    desc = _SysCg._curComponent.FindSignalOrPort(signalRef.Desc);
                    if (desc == null)
                        throw new InvalidOperationException("Referenced signal unknown to declaring component");
                     * */
                    remappedRef = signalRef.RelateToComponent(_SysCg._curComponent);
                    if (remappedRef == null)
                        throw new InvalidOperationException("Referenced signal unknown to declaring component");
                }
                string name = remappedRef.Name;

                string[] aux;
                StringBuilder sb = new StringBuilder();
                StringBuilder sb1 = new StringBuilder();
                var udesc = desc.GetUnindexedContainer();
                sb.Append(_SysCg.MakeIDName(name, desc.GetUnindexedContainer().GetBoundSignal()));
                if (!remappedRef.IsStaticIndex ||
                        !remappedRef.IndexSample.Equals(udesc.ElementType.Index))
                {
                    foreach (Expression[] indexSpec in remappedRef.GetFullIndices())
                    {
                        if (indexSpec.Length == 0)
                            continue;

                        bool first = true;
                        foreach (Expression index in indexSpec)
                        {
                            if (first)
                                first = false;
                            else
                                sb.Append(", ");
                            //sb.Append(index.ToString(_SysCg));
                            aux = index.ToString(_SysCg).Split(' ');
                            if (aux.Length == 3)
                                sb1.Append("(" + aux[0] + ", " + aux[2] + ")");
                            else
                            if (aux.Length == 1)
                                sb1.Append("[" + aux[0] + "]");
                            //else
                            //    sb.Append("Erro na geracao de incdices!");
                        }
                    }
                }
                //sb.Append(")");
                switch (signalRef.Prop)
                {
                    case SignalRef.EReferencedProperty.ChangedEvent:
                        sb.Append(".event()");
                        Result = sb.ToString();
                        break;

                    case SignalRef.EReferencedProperty.Cur:
                        if (desc.ElementType.CILType.IsArray)
                        {
                            if (sb1.Length > 0)
                                sb.Append(sb1);
                            sb.Append(".read()");
                        }
                        else
                        {
                            sb.Append(".read()");
                            if (sb1.Length > 0)
                                sb.Append(sb1);
                        }                       
                        Result = sb.ToString();
                        break;
                    case SignalRef.EReferencedProperty.Instance:
                    case SignalRef.EReferencedProperty.Next:
                        sb.Append(sb1);
                        Result = sb.ToString();
                        break;

                    case SignalRef.EReferencedProperty.Pre:
                    //sb.Append("'pre");
                    //Result = sb.ToString();
                    //break;

                    case SignalRef.EReferencedProperty.RisingEdge:
                        Result = sb.ToString() + ".posedge()";
                        break;

                    case SignalRef.EReferencedProperty.FallingEdge:
                        Result = sb.ToString() + ".negedge()";
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            public void VisitArrayRef(ArrayRef arrayRef)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(arrayRef.ArrayExpr.ToString(_SysCg));
                var indices = arrayRef.Indices.Select(idx => idx.ToString(_SysCg));
                sb.Append("[");
                sb.Append(string.Join(", ", indices));
                sb.Append("]");
                Result = sb.ToString();
            }

            #endregion
        }

        private string NotateLiteral(ILiteral literal, LiteralReference.EMode mode)
        {
            LiteralStringifier ls = new LiteralStringifier(this, mode);
            literal.Accept(ls);
            return ls.Result;
        }

        public LiteralNotateFunc GetLiteralNotation()
        {
            return NotateLiteral;
        }

        public BracketNotateFunc GetBracketNotation()
        {
            return DefaultNotators.Bracket;
        }

        #endregion

        #region IStringifyInfo Members

        public IOperatorPrecedence Precedence
        {
            get { return this; }
        }

        public IOperatorNotation Notation
        {
            get { return this; }
        }

        public Action<Expression> OnStringifyExpression
        {
            get
            {
                return x =>
                {
                    TypeInfo ti;
                    if (x.ResultType != null)
                        LookupType(x.ResultType.CILType, out ti);
                };
            }
        }

        #endregion

        private class SystemCStatementsGen : IStatementVisitor
        {
            private IndentedTextWriter _tw;
            private SystemCGenerator _SysCg;

            public SystemCStatementsGen(IndentedTextWriter tw, SystemCGenerator SysCg)
            {
                _tw = tw;
                _SysCg = SysCg;
            }

            //      ALTERADA !!!
            private void GenerateForLoop(LoopBlock loop)
            {
                _tw.Write("for(");
                _tw.Write(loop.Initializer.Value.ToString(_SysCg));
                _tw.Write("; ");
                _tw.Write(loop.HeadCondition.ToString(_SysCg));
                _tw.Write("; ");
                _tw.Write(loop.Step.Value.ToString(_SysCg));
                _tw.Write(")");

                Expression counterStop = loop.CounterStop;
                GenerateLoop(loop);
                loop.Trailer.Accept(this);
            }

            //      ALTERADA 
            private void GenerateWhileLoop(LoopBlock loop)
            {
                _tw.Write("while(");
                _tw.Write(loop.HeadCondition.ToString(_SysCg));
                _tw.Write(")");
                GenerateLoop(loop);
                loop.Trailer.Accept(this);
            }

            private void GenerateGenericLoop(LoopBlock loop)
            {
                _tw.WriteLine("while(true)");
                GenerateLoop(loop);
                if (loop.Trailer != null)
                    loop.Trailer.Accept(this);
            }

            //      ALTERADA
            private void GenerateLoop(LoopBlock stmt)
            {
                _tw.WriteLine("{");
                _tw.Indent++;
                stmt.Body.AcceptIfEnabled(this);
                _tw.Indent--;
                _tw.WriteLine();
                _tw.WriteLine("}");
                //if (stmt.Label != null)
                //    _tw.Write(" " + stmt.Label);
                //_tw.WriteLine(";");
            }

            //      ALTERADA
            private void GenerateComments(Statement stmt)
            {
                string comment = stmt.Comment;
                if (comment == null)
                    return;
                string[] lines = comment.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    _tw.WriteLine("// " + line);
                }
            }

            #region IStatementVisitor Members

            //      PERCEBER MELHOR...
            public void AcceptCompoundStatement(CompoundStatement stmt)
            {
                GenerateComments(stmt);

                LoopBlock forLoop = stmt.AsForLoop(EForLoopLevel.StrictOneInc);
                if (forLoop != null)
                {
                    if (forLoop.Label != null)
                        _tw.WriteLine(forLoop.Label + ": ");
                    GenerateForLoop(forLoop);
                }
                else
                {
                    foreach (Statement substmt in stmt.Statements)
                        substmt.AcceptIfEnabled(this);
                }

            }

            //      PERCEBER MELHOR...
            public void AcceptLoopBlock(LoopBlock stmt)
            {
                if (stmt.Label != null)
                    _tw.WriteLine(stmt.Label + ": ");
                GenerateComments(stmt);

                LoopBlock whileLoop = stmt.AsWhileLoop();
                if (whileLoop != null)
                {
                    GenerateWhileLoop(whileLoop);
                }
                else
                {
                    GenerateGenericLoop(stmt);
                }
            }

            //      PERCEBER MELHOR...
            public void AcceptBreakLoop(BreakLoopStatement stmt)
            {
                GenerateComments(stmt);
                
                _tw.WriteLine();
                _tw.Write("break");
                //if (stmt.Loop.Label != null)
                //    _tw.Write(" " + stmt.Loop.Label);
                _tw.WriteLine("; // AcceptBreakLoop");
            }

            //      PERCEBER MELHOR...
            public void AcceptContinueLoop(ContinueLoopStatement stmt)
            {
                GenerateComments(stmt);
                if (stmt.Loop.Label != null)
                {
                    _tw.Write("goto ");
                    _tw.Write(" " + stmt.Loop.Label);
                }
                else
                    _tw.Write("continue");
                _tw.WriteLine(";");
            }

            //      ALTERADA
            public void AcceptIf(IfStatement stmt)
            {
                GenerateComments(stmt);
                for (int i = 0; i < stmt.Conditions.Count; i++)
                {
                    if (i == 0)
                    {
                        _tw.Write("if (");
                    }
                    else
                    {
                        _tw.Write("else if (");
                    }
                    Expression cond = stmt.Conditions[i];
                    string strcond = cond.ToString(_SysCg);
                    _tw.Write(strcond);
                    _tw.WriteLine(")");
                    _tw.WriteLine("{");
                    _tw.Indent++;
                    stmt.Branches[i].AcceptIfEnabled(this);
                    _tw.Indent--;
                    _tw.WriteLine("}");
                }
                if (stmt.Branches.Count > stmt.Conditions.Count)
                {
                    _tw.WriteLine("else");
                    _tw.WriteLine("{");
                    _tw.Indent++;
                    stmt.Branches.Last().AcceptIfEnabled(this);
                    _tw.Indent--;
                    _tw.WriteLine("}");
                }
            }

            //      ALTERADA
            public void AcceptCase(CaseStatement stmt)
            {
                if (stmt.Label != null)
                {
                    _tw.WriteLine(stmt.Label + ": ");
                }

                GenerateComments(stmt);

                if (stmt.Selector.ResultType.CILType.IsEnum || stmt.Selector.ResultType.CILType.Equals(typeof(int)) || stmt.Selector.ResultType.CILType.Equals(typeof(char)))
                {
                    _tw.Write("switch(");
                    _tw.Write(stmt.Selector.ToString(_SysCg));
                    _tw.WriteLine(") {");
                    _tw.Indent++;                

                    for (int i = 0; i < stmt.Cases.Count; i++)
                    {
                        _tw.Write("case ");
                        _tw.Write(stmt.Cases[i].ToString(_SysCg));
                        _tw.WriteLine(" :");
                        _tw.Indent++;
                        stmt.Branches[i].AcceptIfEnabled(this);
                        if (!stmt.Branches[i].ToString().Contains("BREAK CASE"))
                            _tw.WriteLine("break;");
                        _tw.Indent--;
                    }

                    if (stmt.Branches.Count > stmt.Cases.Count)
                    {
                        _tw.WriteLine("default: ");
                        _tw.Indent++;
                        stmt.Branches.Last().AcceptIfEnabled(this);
                        _tw.Indent--;
                    }
                _tw.Indent--;
                _tw.WriteLine("}");
                }
                else
                {
                    IfStatement aux = stmt.ConvertToIfStatement();
                   
                    AcceptIf(aux);                   
                }
            }

            //      ALTERADA
            public void AcceptStore(StoreStatement stmt)
            {
                GenerateComments(stmt);
                IStorableLiteral literal = stmt.Container;
                if (stmt.Container == null)
                    _tw.Write("// <???>");
                else
                    _tw.Write(_SysCg.GetLiteralNotation()((Literal)stmt.Container, LiteralReference.EMode.Direct));
                if (literal.StoreMode == EStoreMode.Transfer)
                {
                    if (stmt.Value.ResultType.CILType.IsEnum)
                        //_tw.Write(".write(" + _SysCg.GetTypeDescriptorName(stmt.Value.ResultType) + "::" + stmt.Value.ToString(_SysCg) + ")");
                        _tw.Write(".write(" + stmt.Value.ToString(_SysCg) + ")"); // Alteração a 15/04/2013
                    else
                        _tw.Write(".write(" + stmt.Value.ToString(_SysCg) + ")");
                }
                else
                {
                    if (stmt.Value.ResultType.CILType.IsEnum)
                        _tw.Write(" = " + stmt.Value.ToString(_SysCg) + ")");
                        //_tw.Write(" = " + _SysCg.GetTypeDescriptorName(stmt.Value.ResultType) + "::" + stmt.Value.ToString(_SysCg));
                    else
                        _tw.Write(" = " + stmt.Value.ToString(_SysCg));
                }
                _tw.WriteLine(";");
            }

            public void AcceptNop(NopStatement stmt)
            {
            }

            public void AcceptSolve(SolveStatement stmt)
            {
                _tw.WriteLine("// solve unsupported");
            }

            public void AcceptBreakCase(BreakCaseStatement stmt)
            {
                _tw.WriteLine("break;");
            }

            public void AcceptGotoCase(GotoCaseStatement stmt)
            {
                _tw.WriteLine("// goto case unsupported");
            }

            //      ????
            public void AcceptGoto(GotoStatement stmt)
            {
                GenerateComments(stmt);
                _tw.Write("goto");
                if (stmt.Label != null)
                    _tw.Write(" " + stmt.Label);
                _tw.WriteLine(";");
            }

            public void AcceptReturn(ReturnStatement stmt)
            {
                GenerateComments(stmt);
                if (stmt.ReturnValue != null)
                {
                    _tw.Write("return");
                    _tw.Write(" " + stmt.ReturnValue.ToString(_SysCg));
                }
                _tw.WriteLine(";");
            }

            //      REVER
            public void AcceptThrow(ThrowStatement stmt)
            {
                _tw.WriteLine("// throw not supported");
            }

            public void AcceptCall(CallStatement stmt)
            {
                GenerateComments(stmt);
                FunctionCall tmp = new FunctionCall()
                {
                    Callee = stmt.Callee,
                    Arguments = stmt.Arguments
                };
                _tw.Write(tmp.ToString(_SysCg));
                _tw.WriteLine(";");
            }

            #endregion
        }

        //      ALTERADAS
        #region SystemC keywords
        public static readonly string[] Keywords = { "alignas ", "alignof ", "and", "and_eq", "asm", "auto", 
                                                     "bitand", "bitor", "bool", "break", "case", "catch", "char", 
                                                     "char16_t", "char32_t", "class", "compl", "const", "constexpr", 
                                                     "const_cast", "continue", "decltype", "default", "delete", "do", 
                                                     "double", "dynamic_cast", "else", "enum", "explicit", "export", 
                                                     "extern", "false", "float", "for", "friend", "goto", "if", "inline", 
                                                     "int", "long", "mutable", "namespace", "new", "noexcept", "not", 
                                                     "not_eq", "nullptr ", "operator", "or", "or_eq", "private", "protected", 
                                                     "public", "register", "reinterpret_cast", "return", "short", "signed", 
                                                     "sizeof", "static", "static_assert", "static_cast", "struct", "switch", 
                                                     "template", "this", "thread_local", "throw", "true", "try", "typedef", 
                                                     "typeid", "typename", "union", "unsigned", "using", "virtual", "void", 
                                                     "volatile", "wchar_t", "while", "xor", "xor_eq" };
        #endregion

        private ScopedIdentifierManager _sim = new ScopedIdentifierManager(false);
        private Dictionary<string, HashSet<string>> _pkgRefs = new Dictionary<string, HashSet<string>>();
        private HashSet<Tuple<string, string>> _synthPkgRefs = new HashSet<Tuple<string, string>>();

        public SystemCGenerator()
        {
            InitKeywords();
            CreateFileHeader = DefaultCreateFileHeader;
        }

        private void InitKeywords()
        {
            foreach (string name in Keywords)
                _sim.GetUniqueName(name, new object(), true);
        }

        //      ALTERADA
        public void DefaultCreateFileHeader(IGeneratorInformation geni, IndentedTextWriter tw)
        {
            tw.WriteLine("/**");
            tw.WriteLine("* This file was automatically generated by the System# framework.");
            tw.WriteLine("* Generated file: " + geni.CurrentFile);
            tw.WriteLine("* Creation time:  " + DateTime.Now.ToString());
            tw.WriteLine("* */");
            tw.WriteLine();
        }

        private enum ESystemCIdState
        {
            Beginning,
            Inside,
            Underscore
        }

        string MakeIDName(string name, bool rootScope = false)
        {
            return MakeIDName(name, name.ToLower(), rootScope);
        }

        string MakeValidSystemCIdentifier(string name)
        {
            if (name == null)
                return "???";

            StringBuilder goodName = new StringBuilder();
            ESystemCIdState cur = ESystemCIdState.Beginning;
            foreach (char c in name)
            {
                switch (cur)
                {
                    case ESystemCIdState.Beginning:
                        if (char.IsLetter(c))
                        {
                            goodName.Append(c);
                            cur = ESystemCIdState.Inside;
                        }
                        else
                        {
                            goodName.Append('m');
                            if (char.IsDigit(c))
                            {
                                goodName.Append(c);
                                cur = ESystemCIdState.Inside;
                            }
                            else if (c == '_')
                            {
                                goodName.Append('_');
                                cur = ESystemCIdState.Underscore;
                            }
                            else
                            {
                                cur = ESystemCIdState.Inside;
                            }
                        }
                        break;

                    case ESystemCIdState.Inside:
                        if (char.IsLetterOrDigit(c))
                        {
                            goodName.Append(c);
                            cur = ESystemCIdState.Inside;
                        }
                        else
                        {
                            goodName.Append('_');
                            cur = ESystemCIdState.Underscore;
                        }
                        break;

                    case ESystemCIdState.Underscore:
                        if (char.IsLetterOrDigit(c))
                        {
                            goodName.Append(c);
                            cur = ESystemCIdState.Inside;
                        }
                        else
                        {
                            cur = ESystemCIdState.Underscore;
                        }
                        break;
                }
            }
            return goodName.ToString().TrimEnd('_');
        }

        string MakeIDName(string name, object item, bool rootScope = false)
        {
            string goodName = MakeValidSystemCIdentifier(name);
            return _sim.GetUniqueName(goodName, item, rootScope);
        }

        string MakeIDName(string name, object item, ScopedIdentifierManager sim)
        {
            string goodName = MakeValidSystemCIdentifier(name);
            return sim.GetUniqueName(goodName, item, false);
        }

        private string GetComponentName(IComponentDescriptor cd)
        {
            string name;
            if (cd.Name != null && cd.Name.Length > 0)
                name = cd.Name;
            else
                name = "top";
            name = MakeIDName(name, cd, true);
            return name;
        }

        public string GetComponentID(IComponentDescriptor cd)
        {
            return GetComponentName(cd);
        }

        //      ALTERADA
        private string MakeSysCHeaderFileName(string name)
        {
            return name + ".h";
        }

        private string MakeSysCSourceFileName(string name)
        {
            return name + ".cpp";
        }

        private string PortDirectionToString(EFlowDirection dir)
        {
            switch (dir)
            {
                case EFlowDirection.In: return "in";
                case EFlowDirection.Out: return "out";
                case EFlowDirection.InOut: return "inout";
                default: throw new NotImplementedException();
            }
        }

        private bool LookupType(Type type, out TypeInfo ti)
        {
            if (SystemCTypes.LookupType(type, out ti))
            {
                if (ti.Libraries != null)
                {
                    for (int i = 0; i < ti.Libraries.Length; i++)
                    {
                        string lib = ti.Libraries[i];
                        if (!(_stdLibraries.Exists(element => element.Name == lib)))
                            _stdLibraries.Add(new SysCLib(lib));
                        //if (!ti.IsNotSynthesizable)
                        //    _synthPkgRefs.Add(Tuple.Create(lib, pkg));
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private string GetTypeDescriptorName(TypeDescriptor td)
        {
            Contract.Requires(td != null);

            // Type database uses unconstrained types
            if (td.Rank > 0 && !td.IsUnconstrained)
                td = td.MakeUnconstrainedType();

            TypeInfo ti;
            if (LookupType(td.CILType, out ti))
            {
                return ti.Name;
            }

            string name;
            if (td.CILType.IsArray)
                name = "array_" + td.CILType.GetElementType().Name;
            else
                name = td.CILType.Name;
            return MakeIDName(name, td, true);
        }

        private string GetRangeSuffix(Range range)
        {
            string result = "[" + range.Size + "]";
            //switch (range.Direction)
            //{
            //    case EDimDirection.Downto: result += "downto"; break;
            //    case EDimDirection.To: result += "to"; break;
            //    default: throw new NotImplementedException();
            //}
            //result += " " + range.SecondBound + ")";
            return result;
        }

        private string GetTypeDescriptorCompletedName(TypeDescriptor td)
        {
            Contract.Requires(td != null);

            TypeInfo ti;
            if (!td.CILType.IsArray)
            {
                if (td.CILType.IsEnum)
                {
                    return "int";
                }

                if (LookupType(td.CILType, out ti))
                {
                    return ti.DeclareCompletedType(td);
                }
            }
            else
            {
                if (LookupType(td.Element0Type.CILType, out ti))
                {
                    return ti.DeclareCompletedType(td);
                }
            }
            string name = GetTypeDescriptorName(td);
            //if (td.Rank > 0)
            //{
            //    if (td.Constraints != null)
            //    {
            //        if (!td.CILType.IsArray)
            //            name += GetRangeSuffix(td.Constraints[0]);
            //    }
            //    else
            //        name += "(???)";
            //}
            return name;
        }

        private void DeclareArrayInitializer(Array array, int dim, long[] indices, StringBuilder sb, string name)
        {
            long length = array.GetLongLength(dim);
            
            //sb.Append(" /*");
            //if (length == 1)
            //{
            //    sb.Append("0 => ");
            //}
            int linelen = 0;
            for (long i = 0; i < length; i++)
            {
                if (i > 0)
                    sb.Append(";");
                ++linelen;
                if (linelen < 200)
                {
                    sb.Append(" ");
                }
                else
                {
                    sb.AppendLine();
                    linelen = 0;
                }

                indices[dim] = i;
                if (dim < array.Rank - 1)
                {
                    DeclareArrayInitializer(array, dim + 1, indices, sb, name);
                }
                else
                {
                    object element = array.GetValue(indices);
                    string value = GetValueID(element);
                    if(dim == 0)
                        sb.Append(name + "[" + i + "] = " + value);
                    else
                        sb.Append(name + "[" + dim + "][" + i + "] = " + value);
                    linelen += value.Length;
                }
            }
            sb.Append(";");
            sb.AppendLine();
        }

        // Not used in principle...
        private void DeclareArrayInitializer(Array array, int dim, long[] indices, StringBuilder sb)
        {
            long length = array.GetLongLength(dim);

            sb.Append(" /*");
            //if (length == 1)
            //{
            //    sb.Append("0 => ");
            //}
            int linelen = 0;
            for (long i = 0; i < length; i++)
            {
                if (i > 0)
                    sb.Append(",");
                ++linelen;
                if (linelen < 200)
                {
                    sb.Append(" ");
                }
                else
                {
                    sb.AppendLine();
                    linelen = 0;
                }

                indices[dim] = i;
                if (dim < array.Rank - 1)
                {
                    DeclareArrayInitializer(array, dim + 1, indices, sb);
                }
                else
                {
                    object element = array.GetValue(indices);
                    string value = GetValueID(element);
                    sb.Append(value);
                    linelen += value.Length;
                }
            }
            sb.Append("*/");
        }

        private string GetValueID(object obj)
        {
            if (obj is Array)
            {
                Array array = (Array)obj;
                long[] indices = new long[array.Rank];
                StringBuilder sb = new StringBuilder();
                DeclareArrayInitializer(array, 0, indices, sb);
                return sb.ToString();
            }
            else
            {
                string result;
                if (SystemCTypes.GetValueOf(obj, out result))
                    return result;
                else
                    return obj.ToString();
            }
        }

        private string GetValueID(object obj, string name)
        {
            if (obj is Array)
            {
                Array array = (Array)obj;
                long[] indices = new long[array.Rank];
                StringBuilder sb = new StringBuilder();
                DeclareArrayInitializer(array, 0, indices, sb, name);
                return sb.ToString();
            }
            else
            {
                string result;
                if (SystemCTypes.GetValueOf(obj, out result))
                    return result;
                else
                    return obj.ToString();
            }
        }

        //      ALTERADA
        private void DeclarePortList(IComponentDescriptor cd, IndentedTextWriter tw, bool extScope)
        {
            if (cd.GetPorts().Count() == 0)
                return;
            ScopedIdentifierManager xsim = _sim;
            if (extScope)
            {
                xsim = _sim.Fork();
                xsim.PushScope();
            }
            foreach (IPortDescriptor pd in cd.GetPorts())
            {
                string pid = MakeIDName(pd.Name, pd.BoundSignal, xsim);

                if (!pd.ElementType.CILType.IsArray)
                {
                    tw.WriteLine("sc_" + PortDirectionToString(pd.Direction) + "<" +
                          GetTypeDescriptorCompletedName(pd.ElementType) + "> " + pid + ";");
                }
                else
                {
                    tw.WriteLine("sc_vector< sc_" + PortDirectionToString(pd.Direction) + "<" +
                        GetTypeDescriptorCompletedName(pd.ElementType.Element0Type) + "> > " + pid +
                        ";");
                }
            }


        }

        //      ALTERADA
        private void DeclareComponent(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string subcompname = GetComponentName(((ComponentDescriptor)cd).Instance.Representant.Descriptor);
            string name = MakeIDName(GetComponentName(cd), cd) + "_";
            tw.WriteLine(subcompname + " " + name + ";");
        }

        //      ALTERADA
        private void DeclareComponentInstance(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = MakeIDName(GetComponentName(cd), cd) + "_";
            //string subcompname = GetComponentName(((ComponentDescriptor)cd).Instance.Representant.Descriptor);
            tw.Write(name + "(" + "\"" + name + "\"" + ")");
        }

        private void DeclareSignalVecInstance(ISignalDescriptor sd, IndentedTextWriter tw)
        {
            string sname = MakeIDName(sd.Name, sd);
            tw.Write(sname + "(" + "\"" + sname + "\", " + sd.ElementType.TypeParams[0] + ")");
        }

        //      ADDED
        private void PortBinding(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            //string rname = GetComponentName(((ComponentDescriptor)cd).Instance.Representant.Descriptor);
            string rname = MakeIDName(GetComponentName(cd), cd) + "_";
            // Alternative 1
            if (cd.GetPorts() != null)
            {

                IComponentDescriptor owner = (IComponentDescriptor)cd.Owner;
                //bool first = true;
                var xsim = _sim.Fork();
                xsim.PushScope();
                foreach (IPortDescriptor pd in cd.GetPorts())
                {
                    tw.Write(rname + ".");
                    var ownerRef = pd
                        .BoundSignal
                        .AsSignalRef(SignalRef.EReferencedProperty.Instance);
                    if (ownerRef == null)
                        throw new InvalidOperationException("Bound signal unknown to instantiating component");

                    var litstr = new LiteralStringifier(this, LiteralReference.EMode.Direct);
                    var temp = _curComponent;
                    _curComponent = owner;
                    litstr.VisitSignalRef(ownerRef);
                    var rhs = litstr.Result;
                    _curComponent = temp;
                    string pid = MakeIDName(pd.Name, pd.BoundSignal.RemoveIndex(), xsim);
                    tw.WriteLine(pid + ".bind(" + rhs + ");");


                }
                tw.WriteLine();
            }

            //// Alternative 2 
            //if (cd.GetPorts() != null)
            //{
            //    tw.Write(rname + "(");
            //    IComponentDescriptor owner = (IComponentDescriptor)cd.Owner;
            //    bool first = true;
            //    var xsim = _sim.Fork();
            //    xsim.PushScope();
            //    foreach (IPortDescriptor pd in cd.GetPorts())
            //    {
            //        if (first)
            //            first = false;
            //        else
            //            tw.Write(",");

            //        var ownerRef = pd
            //            .BoundSignal
            //            .AsSignalRef(SignalRef.EReferencedProperty.Instance);
            //        if (ownerRef == null)
            //            throw new InvalidOperationException("Bound signal unknown to instantiating component");

            //        var litstr = new LiteralStringifier(this, LiteralReference.EMode.Direct);
            //        var temp = _curComponent;
            //        _curComponent = owner;
            //        litstr.VisitSignalRef(ownerRef);
            //        var rhs = litstr.Result;
            //        _curComponent = temp;
            //        string pid = MakeIDName(pd.Name, pd.BoundSignal.RemoveIndex(), xsim);
            //        tw.Write(rhs);
            //    }
            //    tw.WriteLine(");");
            //}
        }

        //      ALTERADA
        private void GenerateMethodBody(MethodDescriptor cd, IndentedTextWriter tw)
        {
            _sim.PushScope();
            //tw.Indent++;
            //for (int i = 0; i < cd.Implementation.LocalVariables.Count; i++)
            //{
            //    var local = cd.Implementation.LocalVariables[i];
            //    string typestr = GetTypeDescriptorCompletedName(local.Type);
            //    string varname = MakeIDName(local.Name, local);
            //    tw.Write(typestr + " " + varname);
            //    if (cd.ValueRangeConstraints[i].IsConstrained)
            //    {
            //        CodeDescriptor.ValueRangeConstraint vrc = cd.ValueRangeConstraints[i];
            //        tw.Write("<" + (Math.Log(Math.Max(Math.Abs(vrc.MaxValue), Math.Abs(vrc.MinValue))) / Math.Log(2) + 1) + ">");
            //    }
            //    else
            //    {
            //        TypeInfo ti;
            //        if (LookupType(local.Type.CILType, out ti) &&
            //            ti.RangeSpec == TypeInfo.ERangeSpec.ByRange &&
            //            ti.ShowDefaultRange)
            //        {
            //            tw.Write(" <" + (ti.DefaultRange.SecondBound - ti.DefaultRange.FirstBound + 1) + ">");
            //        }
            //    }
            //    tw.WriteLine(";");
            //}

            cd.Implementation.Body.PreprocessForLoopKindRecognition();
            cd.Implementation.Body.AcceptIfEnabled(new SystemCStatementsGen(tw, this));

            tw.Indent--;
            _sim.PopScope();
        }

        private void GenerateMethodBody(ProcessDescriptor cd, IndentedTextWriter tw)
        {
            _sim.PushScope();
            //tw.WriteLine("// Local Variables");
            //tw.Indent++;
            //for (int i = 0; i < cd.Implementation.LocalVariables.Count; i++)
            //{
            //    var local = cd.Implementation.LocalVariables[i];
            //    string typestr = GetTypeDescriptorCompletedName(local.Type);
            //    string varname = MakeIDName(local.Name, local);
            //    tw.Write(typestr + " " + varname);
            //    if (cd.ValueRangeConstraints[i].IsConstrained)
            //    {
            //        CodeDescriptor.ValueRangeConstraint vrc = cd.ValueRangeConstraints[i];
            //        tw.Write("<" + (Math.Log(Math.Max(Math.Abs(vrc.MaxValue), Math.Abs(vrc.MinValue))) / Math.Log(2) + 1) + ">");
            //    }
            //    else
            //    {
            //        TypeInfo ti;
            //        if (LookupType(local.Type.CILType, out ti) &&
            //            ti.RangeSpec == TypeInfo.ERangeSpec.ByRange &&
            //            ti.ShowDefaultRange)
            //        {
            //            tw.Write(" <" + (ti.DefaultRange.SecondBound - ti.DefaultRange.FirstBound + 1) + ">");
            //        }
            //    }
            //    tw.WriteLine(";");
            //}

            cd.Implementation.Body.PreprocessForLoopKindRecognition();
            cd.Implementation.Body.AcceptIfEnabled(new SystemCStatementsGen(tw, this));

            tw.Indent--;
            _sim.PopScope();
        }

        private static SignalRef DieWithUnknownSensitivityIfNull(SignalRef sref)
        {
            if (sref == null)
                throw new InvalidOperationException("Sensitivity signal unknown to declaring component");
            return sref;
        }

        private void DeclareProcess(ProcessDescriptor pd, IndentedTextWriter tw)
        {
            bool simuOnly = pd.HasAttribute<SimulationOnly>();
            if (simuOnly)
                SwitchOffSynthesis(tw);

            string pname = MakeIDName(pd.Name, pd);
            tw.WriteLine("void " + pname + "()");
            tw.WriteLine("{");

            for (int i = 0; i < pd.Implementation.LocalVariables.Count; i++)
            {
                var local = pd.Implementation.LocalVariables[i];
                string typestr = GetTypeDescriptorCompletedName(local.Type);
                string varname = MakeIDName(local.Name, local);
                //if(typestr == "int")
                //    tw.Write("static const " + typestr + " " + varname + "// initialization missing!!!");
                //else
                    tw.Write("static " + typestr + " " + varname);
                if (pd.ValueRangeConstraints[i].IsConstrained)
                {
                    CodeDescriptor.ValueRangeConstraint vrc = pd.ValueRangeConstraints[i];
                    tw.Write("<" + (Math.Log(Math.Max(Math.Abs(vrc.MaxValue), Math.Abs(vrc.MinValue))) / Math.Log(2) + 1) + ">");
                }
                else
                {
                    TypeInfo ti;
                    if (LookupType(local.Type.CILType, out ti) &&
                        ti.RangeSpec == TypeInfo.ERangeSpec.ByRange &&
                        ti.ShowDefaultRange)
                    {
                        //tw.Write(" <" + (ti.DefaultRange.SecondBound - ti.DefaultRange.FirstBound + 1) + ">");
                        tw.Write(" [" + (ti.DefaultRange.SecondBound - ti.DefaultRange.FirstBound + 1) + "]");
                    }
                }
                tw.WriteLine(";");
            }

            if (pd.Kind == Components.Process.EProcessKind.Threaded)
            {
                tw.Indent++;
                tw.WriteLine("while(true)");
                tw.WriteLine("{");
                GenerateMethodBody(pd, tw);
                tw.WriteLine("}");
                tw.Indent--;
            }
            else
                GenerateMethodBody(pd, tw);

            tw.WriteLine("}");

            if (simuOnly)
                SwitchOnSynthesis(tw);
        }

        //      ADDED
        private void InitializeProcess(ProcessDescriptor pd, IndentedTextWriter tw)
        {
            bool simuOnly = pd.HasAttribute<SimulationOnly>();
            if (simuOnly)
                SwitchOffSynthesis(tw);

            string pname = MakeIDName(pd.Name, pd);

            if (pd.Kind == Components.Process.EProcessKind.Triggered &&
                    pd.Sensitivity.Length > 0)
            {
                tw.WriteLine("SC_METHOD(" + pname + ");");
                tw.Indent++;
                tw.Write("sensitive");
                var sens = pd.Sensitivity.Select(spd =>
                        DieWithUnknownSensitivityIfNull(spd
                            .AsSignalRef(SignalRef.EReferencedProperty.Instance)
                            .RelateToComponent(_curComponent))
                        .AssimilateIndices()
                        .Desc)
                        .Distinct();
                foreach (var spd in sens)
                {
                    tw.Write(" << ");
                    tw.Write(MakeIDName(spd.Name, spd.GetBoundSignal()));
                }
                tw.WriteLine(";");
                tw.Indent--;

            }
            else if (pd.Kind == Components.Process.EProcessKind.Threaded)
            {
                tw.WriteLine("SC_THREAD(" + pname + ");");
                if (pd.Sensitivity.Length > 0)
                {
                    tw.Indent++;
                    tw.Write("sensitive");
                    var sens = pd.Sensitivity.Select(spd =>
                            DieWithUnknownSensitivityIfNull(spd
                                .AsSignalRef(SignalRef.EReferencedProperty.Instance)
                                .RelateToComponent(_curComponent))
                            .AssimilateIndices()
                            .Desc)
                            .Distinct();
                    foreach (var spd in sens)
                    {
                        tw.Write(" << ");
                        tw.Write(MakeIDName(spd.Name, spd.GetBoundSignal()));
                    }
                    tw.WriteLine(";");
                    tw.Indent--;
                }
            }

            if (simuOnly)
                SwitchOnSynthesis(tw);
        }

        //      ALTERADA
        private void DeclareField(FieldDescriptor field, IndentedTextWriter tw)
        {
            string fname = MakeIDName(field.Name, field);
            if ((field.Type.IsStatic) && (field.Type.Name == "Int32"))
            {
                tw.Write("static const " + GetTypeDescriptorCompletedName(field.Type) + " " + fname);
                if (field.ConstantValue != null)
                    tw.WriteLine(" = " + SystemCifyConstant(field.ConstantValue) + ";");
                //tw.WriteLine("//    " + field.Type.Name);
            }
            else
                tw.Write(GetTypeDescriptorCompletedName(field.Type) + " " + fname);

            if (field.Type.CILType.IsArray && !field.Type.CILType.Equals(typeof(StdLogicVector))
               && !field.Type.CILType.Equals(typeof(SFix)) && !field.Type.Element0Type.CILType.Equals(typeof(UFix))
               && !field.Type.CILType.Equals(typeof(Signed)) && !field.Type.CILType.Equals(typeof(Unsigned)))
            {
                tw.WriteLine(GetRangeSuffix(field.Type.Constraints[0]) + ";");
            }
            else
                tw.WriteLine(";");
        }

        //      ADDED
        private void InitializeField(FieldDescriptor field, IndentedTextWriter tw)
        {
            string fname = MakeIDName(field.Name, field);

            if ((field.ConstantValue != null) && !((field.Type.IsStatic) && (field.Type.Name == "Int32")))
            {
                if (field.Type.CILType.IsEnum)
                {
                    string enumname = GetTypeDescriptorName(field.Type);
                    tw.WriteLine(fname + " = " + enumname + "::" + SystemCifyConstant(field.ConstantValue) + ";");
                }
                else if (field.Type.CILType.IsArray)
                {
                    tw.Write(SystemCifyConstant(field.ConstantValue, fname));
                }
                else
                    tw.WriteLine(fname + " = " + SystemCifyConstant(field.ConstantValue) + ";");
            }
        }

        private void InitializeSignal(ISignalDescriptor signal, IndentedTextWriter tw)
        {
            string sname = MakeIDName(signal.Name, signal);

            int i = 0;
            if (signal.ElementType.CILType.IsArray)
            {
                //tw.WriteLine("// " + sname);
                foreach (ISignalDescriptor elem in signal.GetSignals())
                {
                    if (elem.InitialValue != null)
                    {
                        tw.WriteLine(sname + "[" + i + "]" + ".write(" + SystemCifyConstant(elem.InitialValue) + ");");
                    }
                    i++;
                }
            }
            else if (signal.InitialValue != null)
            {
                if (signal.ElementType.CILType.IsEnum)
                {
                    string enumname = GetTypeDescriptorName(signal.ElementType);
                    tw.WriteLine(sname + ".write(" + enumname + "::" + SystemCifyConstant(signal.InitialValue) + ");");
                }
                else
                    tw.WriteLine(sname + ".write(" + SystemCifyConstant(signal.InitialValue) + ");");
            }
        }

        //      ALTERADA - equivalent to "DeclareEntity()" and "GenerateArchitecture()" in VHDLGen.cs
        private void DeclareAndGenerateModule(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = GetComponentName(cd);

            //      Module Declaration
            tw.WriteLine("SC_MODULE(" + name + ")");
            tw.WriteLine("{");
            tw.Indent++;

            tw.WriteLine();
            tw.WriteLine("// Port List" );
            //      Port Declarations
            DeclarePortList(cd, tw, false);
            tw.WriteLine();

            tw.WriteLine("// Sub-Components");
            //      Sub-Components Declaration
            var components = cd.GetChildComponents();
            //    .Cast<ComponentDescriptor>()
            //    .Select(c => c.Instance.Representant)
            //    .Distinct()
            //    .Select(c => c.Descriptor);

            foreach (IComponentDescriptor scd in components)
            {
                DeclareComponent(scd, tw);
            }
            if (cd.GetChildComponents().Count() > 0)
                tw.WriteLine();

            tw.WriteLine("// Local Channels");
            //      Local Channel Declaration
            foreach (ISignalOrPortDescriptor sd in cd.GetSignals())
            {
                //object initVal = sd.InitialValue;
                //string initSuffix = "";
                //if (initVal != null)
                //    initSuffix = ".write( " + GetValueID(initVal);
                string sname = MakeIDName(sd.Name, sd);

                if (sd.ElementType.CILType.IsEnum)
                {
                    tw.WriteLine("sc_signal<int> " + sname + ";");
                }
                else if (sd.ElementType.CILType.IsArray)
                {
                    tw.WriteLine("sc_vector< sc_signal<" + GetTypeDescriptorCompletedName(sd.ElementType.Element0Type)
                        + "> > " + sname + ";");
                }
                else
                {
                    tw.WriteLine("sc_signal<" + GetTypeDescriptorCompletedName(sd.ElementType) + "> " + sname + ";");

                }

            }
            if (cd.GetSignals().Count() > 0)
                tw.WriteLine();

            tw.WriteLine("// Constants");
            //      Constant Declarations
            foreach (FieldDescriptor field in cd.GetConstants())
            {
                DeclareField(field, tw);
            }
            if (cd.GetConstants().Count() > 0)
                tw.WriteLine();

            tw.WriteLine("// Variables");
            //      Variables Declaration
            foreach (FieldDescriptor field in cd.GetVariables())
            {

                DeclareField(field, tw);
            }
            if (cd.GetVariables().Count() > 0)
                tw.WriteLine();

            tw.WriteLine("// Processes");
            //      Process Declaration
            foreach (ProcessDescriptor pd in cd.GetProcesses())
            {
                DeclareProcess(pd, tw);
                tw.WriteLine();
            }

            //      Other Methods Declaration
            //tw.WriteLine("// Funcoes/Procedimentos: ");
            //foreach (MethodDescriptor md in cd.GetMethods())
            //{
            //    GenerateMethodImpl(md, tw);
            //    tw.WriteLine();
            //}
            //if (cd.GetMethods().Count() > 0)
            //    tw.WriteLine();

            tw.WriteLine("// Active functions/methods ");
            foreach (MethodDescriptor md in cd.GetActiveMethods())
            {
                GenerateMethodImpl(md, tw);
                tw.WriteLine();
            }
            if (cd.GetMethods().Count() > 0)
                tw.WriteLine();           


            //      Constructors
            tw.WriteLine("// Constructor");
            GenerateCtor(cd, tw);

            tw.Indent--;
            tw.WriteLine("};");
            tw.WriteLine("#endif");
        }

        //      ALTERADA
        public void GenerateComponent(IProject project, IComponentDescriptor cd)
        {
            string name = GetComponentName(cd);
            string fname = MakeSysCHeaderFileName(name);
            string path = project.AddFile(fname);
            bool IsTopComponent = (name == "top0") ? true : false;
            project.AddFileAttribute(fname, cd);
            if (cd.Library != null)
                project.SetFileLibrary(fname, cd.Library);
            _sim.PushScope();
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            IndentedTextWriter tw = new IndentedTextWriter(sw, "  ");
            _curComponent = cd;
            ClearDependencies();
            DeclareAndGenerateModule(cd, tw);

            tw.Flush();
            sw = new StreamWriter(path);
            tw = new IndentedTextWriter(sw, "  ");

            CreateFileHeader(new GeneratorInfo(fname), tw);
            GeneratePreProcDir(cd, tw);
            GenerateDependencies(cd, tw, null);
            tw.Flush();
            ms.Seek(0, SeekOrigin.Begin);
            ms.CopyTo(sw.BaseStream);
            ms.Close();
            tw.Close();
            sw.Close();
            _curComponent = null;
            _sim.PopScope();

            InstanceDescriptor icd = cd as InstanceDescriptor;
            if (icd != null)
            {
                object[] attrs = icd.Instance.GetType().GetCustomAttributes(typeof(ComponentPurpose), true);
                if (attrs.Length > 0)
                {
                    ComponentPurpose purpose = (ComponentPurpose)attrs.First();
                    project.AddFileAttribute(fname, purpose.Purpose);
                }
            }

            if (IsTopComponent)
            {
                GenerateMainFile(project, cd);
            }

        }

        private static string DirectionToString(ArgumentDescriptor.EArgDirection dir)
        {
            switch (dir)
            {
                case ArgumentDescriptor.EArgDirection.In:
                    return "in";
                case ArgumentDescriptor.EArgDirection.InOut:
                    return "inout";
                case ArgumentDescriptor.EArgDirection.Out:
                    return "out";
                default:
                    throw new NotImplementedException();
            }
        }

        private void GenerateMethodHeader(MethodDescriptor md, IndentedTextWriter tw)
        {
            string mname = _sim.GetUniqueName(md.Name, md);
            MethodInfo method = (MethodInfo)md.Method;
            if (method.ReturnType.Equals(typeof(void)))
            {
                tw.Write("void");
            }
            else
            {
                //if (!md.HasAttribute<ISideEffectFree>())
                //    tw.Write("impure ");
                //tw.Write("function");
                tw.Write(GetTypeDescriptorName(md.ReturnType));
            }
            tw.Write(" " + mname);
            ArgumentDescriptor[] args = md.GetArguments().ToArray();
            if (args.Length > 0)
            {
                tw.Write("(");
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                        tw.Write(", ");

                    ArgumentDescriptor argd = args[i];
                    SignalArgumentDescriptor sargd = argd as SignalArgumentDescriptor;
                    object refobj = argd.Argument;
                    if (sargd != null)
                        refobj = sargd.SignalInstance.Descriptor;
                    string name = MakeIDName(argd.Name, refobj);
                    if (sargd != null)
                    {
                        // Ports are passed by reference to a function 
                        if (sargd.Direction != ArgumentDescriptor.EArgDirection.In)
                        {
                            tw.WriteLine();
                            tw.WriteLine("// the next argument is not declared as an input argument");
                            tw.WriteLine("// generated code is probably not correct.");
                        }
                        tw.Write("sc_signal" + "<" +
                            GetTypeDescriptorCompletedName(sargd.ElementType) + ">& " + name);

                    }
                    else
                    {
                        TypeDescriptor argType = argd.Argument.Type;
                        if (argType.IsByRef)
                            argType = argType.Element0Type;

                        tw.Write(GetTypeDescriptorCompletedName(argType) + " " + name);
                    }
                }
                tw.Write(")");
            }
            else
                tw.WriteLine("()");
            //if (!method.ReturnType.Equals(typeof(void)))
            //{
            //    //TypeDescriptor td = md.ArgTypes
            //    tw.Write(" return ");
            //    tw.Write(GetTypeDescriptorName(md.ReturnType));
            //}

        }

        private void GenerateMethodDecl(MethodDescriptor md, IndentedTextWriter tw)
        {
            GenerateMethodHeader(md, tw);
            tw.WriteLine(";");
        }

        private void GenerateMethodImpl(MethodDescriptor md, IndentedTextWriter tw)
        {
            _sim.PushScope();
            GenerateMethodHeader(md, tw);
            tw.WriteLine();
            tw.WriteLine("{");
            GenerateMethodBody(md, tw);
            tw.WriteLine("}");
            _sim.PopScope();
        }

        private bool IsNotSynthesizable(TypeDescriptor td)
        {
            TypeInfo ti;
            if (SystemCTypes.LookupType(td.CILType, out ti))
            {
                if (ti.IsNotSynthesizable)
                    return true;
            }
            if (td.Rank > 0 && SystemCTypes.LookupType(td.Element0Type.CILType, out ti))
            {
                if (ti.IsNotSynthesizable)
                    return true;
            }
            return false;
        }

        private void SwitchOffSynthesis(IndentedTextWriter tw)
        {
            tw.WriteLine("--synthesis translate_off");
        }

        private void SwitchOnSynthesis(IndentedTextWriter tw)
        {
            tw.WriteLine("--synthesis translate_on");
        }

        private void GenerateTypeDecl(TypeDescriptor td, IndentedTextWriter tw)
        {
            if (td.IsConstrained && td.IsComplete)
                return;
            if (td.CILType.IsPrimitive)
                return;

            string tname = GetTypeDescriptorName(td);

            if (td.CILType.IsEnum)
            {
                tw.WriteLine("typedef enum " + tname + " {");
                tw.Indent++;
                //tw.Write("enum " + tname + " { ");
                string[] names = td.CILType.GetEnumNames();
                bool first = true;
                foreach (string name in names)
                {
                    if (first)
                        first = false;
                    else
                        tw.Write(", ");
                    tw.Write(MakeIDName(name));
                }
                tw.WriteLine("} " + ";");
                //tw.Indent--;
                //tw.WriteLine("}");
            }
            else if (!td.CILType.IsPrimitive)
            {
                tw.WriteLine();
                //tw.Indent++;
                tw.WriteLine("typedef struct {");
                tw.Indent++;
                FieldInfo[] fields =
                    td.CILType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in fields)
                {
                    string fnamestr = MakeIDName(field.Name, field);
                    TypeDescriptor ftype = td.GetFieldType(field);
                    string ftypestr = GetTypeDescriptorCompletedName(ftype);
                    tw.WriteLine(ftypestr + " " + fnamestr + ";");
                }
                //tw.Indent--;
                tw.WriteLine("} " + tname + ";");
                tw.Indent--;
            }
        }

        private void ClearDependencies()
        {
            _pkgRefs.Clear();
            _synthPkgRefs.Clear();
        }

        private static string GetLibrary(IPackageOrComponentDescriptor desc)
        {
            var lib = desc.Library;
            if (lib == null)
                lib = "work";
            return lib;
        }

        //      ALTERADA - Rever conceitos de Library e Package...
        private void GenerateDependencies(PackageDescriptor pd, IndentedTextWriter tw)
        {
            // Due to their compile-time overhead, fixed-point data types are omitted from the 
            // default SystemC include file. To enable fixed-point data types, SC_INCLUDE_FX
            // must be defined prior to including the SystemC header file

            tw.WriteLine("#define SC_INCLUDE_FX");
            

            foreach (SysCLib lib in _stdLibraries)
            {
                if (lib.Name != "#define SC_INCLUDE_FX")
                    tw.WriteLine("#include " + "\"" + lib.Name + "\"");
            }

            tw.WriteLine("//");

            if (pd != null)
            {
                var components = pd.GetChildComponents()
                   .Cast<ComponentDescriptor>()
                   .Select(c => c.Instance.Representant)
                   .Distinct()
                   .Select(c => c.Descriptor);

                foreach (ComponentDescriptor scd in components)
                {
                    tw.WriteLine("#include " + "\"" + GetComponentName(scd) + ".h" + "\"");
                }
            }

            tw.WriteLine("#include <iostream>");
            //tw.WriteLine("#include \"logic_vector_ArithOp.h\"");
            //tw.WriteLine("#include \"sc_logic_not.h\"");

            tw.WriteLine("using namespace sc_core;");
            tw.WriteLine("using namespace sc_dt;");
            tw.WriteLine("using namespace std;");
            tw.WriteLine();

        }

        private void GenerateDependencies(IComponentDescriptor desc, IndentedTextWriter tw, string top)
        {

            // Due to their compile-time overhead, fixed-point data types are omitted from the 
            // default SystemC include file. To enable fixed-point data types, SC_INCLUDE_FX
            // must be defined prior to including the SystemC header file
        
            tw.WriteLine("#define SC_INCLUDE_FX");
            

            foreach (SysCLib lib in _stdLibraries)
            {
                if (lib.Name != "#define SC_INCLUDE_FX")
                    tw.WriteLine("#include " + "\"" + lib.Name + "\"");
            }
            IPackageOrComponentDescriptor aux = (IPackageOrComponentDescriptor)desc;
            if (aux != null)
            {
                var pds = aux.Dependencies
                .Where(pd => pd != aux && !pd.IsEmpty)
                .GroupBy(pd => GetLibrary(pd));

                foreach (var grp in pds)
                {
                    //tw.WriteLine("library {0};", grp.Key);

                    foreach (var pd in grp)
                    {
                        tw.WriteLine("#include \"" + MakeIDName(pd.PackageName, pd) + ".h\"");
                    }
                }

                //foreach (PackageDescriptor pack in ((DescriptorBase)desc).GetDesign().GetPackages())
                //{
                //    tw.WriteLine("// " + pack.PackageName);
                //}

                var components = desc.GetChildComponents()
                   .Cast<ComponentDescriptor>()
                   .Select(c => c.Instance.Representant)
                   .Distinct()
                   .Select(c => c.Descriptor);

                foreach (ComponentDescriptor scd in components)
                {
                    tw.WriteLine("#include " + "\"" + GetComponentName(scd) + ".h" + "\"");
                }
            }
            else
            {
                tw.WriteLine("#include " + "\"" + top + ".h" + "\"");
            }

            tw.WriteLine("#include <iostream>");
            tw.WriteLine("#include \"sc_lv_add_ons.h\"");
            tw.WriteLine("#include \"sc_logic_add_ons.h\"");
            tw.WriteLine("using namespace sc_core;");
            tw.WriteLine("using namespace sc_dt;");
            tw.WriteLine("using namespace std;");
            tw.WriteLine();

        }


        //private void GenerateStdIncludes(IComponentDescriptor desc, IndentedTextWriter tw, string top)
        //{

        //    foreach (SysCLib lib in _stdLibraries)
        //    {
        //        tw.WriteLine("#include " + "\"" + lib.Name + "\"");
        //    }

        //    if (desc != null)
        //    {
        //        var components = desc.GetChildComponents()
        //           .Cast<ComponentDescriptor>()
        //           .Select(c => c.Instance.Representant)
        //           .Distinct()
        //           .Select(c => c.Descriptor);

        //        foreach (ComponentDescriptor scd in components)
        //        {
        //            tw.WriteLine("#include " + "\"" + GetComponentName(scd) + ".h" + "\"");
        //        }
        //    }
        //    else
        //    {
        //        tw.WriteLine("#include " + "\"" + top + ".h" + "\"");
        //    }

        //    tw.WriteLine("#include <iostream>");
        //    //tw.WriteLine("#include <iomanip>");
        //    tw.WriteLine("using namespace sc_core;");
        //    tw.WriteLine("using namespace sc_dt;");
        //    tw.WriteLine("using namespace std;");
        //    tw.WriteLine();
        //}

        private void GenerateTypeDecls(DescriptorBase desc, IndentedTextWriter tw)
        {
            var types = desc.GetTypes().Where(t => (!t.IsConstrained || !t.IsComplete) && !t.CILType.IsPrimitive);
            var synthTypes = types.Where(t => !IsNotSynthesizable(t));
            var nonSynthTypes = types.Where(t => IsNotSynthesizable(t));

            foreach (var td in synthTypes)
            {
                GenerateTypeDecl(td, tw);
            }
            if (nonSynthTypes.Any())
            {
                SwitchOffSynthesis(tw);
                foreach (var td in nonSynthTypes)
                {
                    GenerateTypeDecl(td, tw);
                }
                SwitchOnSynthesis(tw);
            }
        }


        // TODO
        public void GeneratePackage(IProject project, PackageDescriptor pd)
        {
            string name = MakeIDName(pd.PackageName, pd);

            string fname = MakeSysCHeaderFileName(name);
            string path = project.AddFile(fname);
            project.AddFileAttribute(fname, pd);
            if (pd.Library != null)
                project.SetFileLibrary(fname, pd.Library);
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            IndentedTextWriter tw = new IndentedTextWriter(sw, "  ");

            string cfname = MakeSysCSourceFileName(name);
            string path1 = project.AddFile(cfname);
            project.AddFileAttribute(cfname, pd);
            if (pd.Library != null)
                project.SetFileLibrary(cfname, pd.Library);
            MemoryStream ms1 = new MemoryStream();
            StreamWriter sw1 = new StreamWriter(ms1);
            IndentedTextWriter tw1 = new IndentedTextWriter(sw1, "  ");

            ClearDependencies();

            //tw.Indent++;
            tw1.WriteLine("#include \"" + fname + "\"");
            tw1.WriteLine();

            GenerateTypeDecls(pd, tw);

            foreach (MethodDescriptor md in pd.GetMethods())
            {
                GenerateMethodDecl(md, tw);
                tw.WriteLine();
                GenerateMethodImpl(md, tw1);
                tw1.WriteLine();
            }

            foreach (FieldDescriptor fd in pd.GetConstants())
            {
                DeclareField(fd, tw);
            }
            tw.Indent--;
            //tw.Indent++;
            tw.WriteLine("#endif");

            tw.Flush();
            sw = new StreamWriter(path);
            tw = new IndentedTextWriter(sw, "  ");

            tw1.Flush();
            sw1 = new StreamWriter(path1);
            tw1 = new IndentedTextWriter(sw1, "  ");

            CreateFileHeader(new GeneratorInfo(fname), tw);
            CreateFileHeader(new GeneratorInfo(cfname), tw1);
            GeneratePreProcDir(pd, tw);
            GenerateDependencies(pd, tw);
            //_extraLibraries.Add(new SysCLib(fname));

            tw.Flush();
            ms.Seek(0, SeekOrigin.Begin);
            ms.CopyTo(sw.BaseStream);
            ms.Close();
            tw.Close();
            sw.Close();

            tw1.Flush();
            ms1.Seek(0, SeekOrigin.Begin);
            ms1.CopyTo(sw1.BaseStream);
            ms1.Close();
            tw1.Close();
            sw1.Close();

        }

        public void Initialize(IProject project, DesignContext context)
        {
            SubprogramsDontDriveSignals sdds = new SubprogramsDontDriveSignals();
            sdds.ApplyTo(context);
            SimTime = context.CurTime;
        }

        //      ADDED
        private void GenerateCtor(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = GetComponentName(cd);
            bool first = true;
            tw.Write("SC_CTOR(" + name + ")");
            tw.Indent++;

            //      Initialization List
            foreach (ISignalDescriptor signal in cd.GetSignals())
            {
                if (signal.ElementType.CILType.IsArray)
                {
                    if (first)
                    {
                        first = false;
                        tw.Write(": ");
                    }
                    else
                        tw.Write(", ");

                    DeclareSignalVecInstance(signal, tw);
                }
            }

            if (cd.GetPorts() != null)
            {
                var xsim = _sim.Fork();
                xsim.PushScope();
                foreach (IPortDescriptor pd in cd.GetPorts())
                {
                    if (pd.ElementType.CILType.IsArray)
                    {
                        if (first)
                        {
                            first = false;
                            tw.Write(": ");
                        }
                        else
                            tw.Write(", ");


                        string pname = MakeIDName(pd.Name, pd.BoundSignal.RemoveIndex(), xsim);
                        tw.Write(pname + "(" + "\"" + pname + "\", " + pd.ElementType.TypeParams[0] + ")");
                    }

                }

            }

            var components = cd.GetChildComponents();
                //.Cast<ComponentDescriptor>()
                //.Select(c => c.Instance.Representant)
                //.Distinct()
                //.Select(c => c.Descriptor);

            foreach (IComponentDescriptor scd in components)
            {
                if (first)
                {
                    first = false;
                    tw.Write(": ");
                }
                else
                    tw.Write(", ");
                DeclareComponentInstance(scd, tw);
            }

            tw.WriteLine();
            tw.Indent--;
            tw.WriteLine("{");
            tw.Indent++;

            // Initialize Ports
            if (cd.GetPorts() != null)
            {
                var xsim = _sim.Fork();
                xsim.PushScope();
                foreach (IPortDescriptor pd in cd.GetPorts())
                {
                    if ((pd.Direction == EFlowDirection.Out) && (pd.InitialValue != null) && !pd.ElementType.CILType.IsArray)
                    {
                        string pname = MakeIDName(pd.Name, pd.BoundSignal.RemoveIndex(), xsim);
                        tw.WriteLine(pname + ".initialize(" + SystemCifyConstant(pd.InitialValue) + ");");
                    }
                }
            }

            tw.WriteLine();

            //      Process Registration and Sensitivity List
            foreach (ProcessDescriptor pd in cd.GetProcesses())
            {
                InitializeProcess(pd, tw);
            }
            if (cd.GetProcesses().Count() > 0)
                tw.WriteLine();

            //      Module Variable/Constant Initialization
            foreach (FieldDescriptor field in cd.GetVariables())
            {
                InitializeField(field, tw);
            }
            if (cd.GetVariables().Count() > 0)
                tw.WriteLine();

            foreach (FieldDescriptor field in cd.GetConstants())
            {
                InitializeField(field, tw);
            }
            if (cd.GetVariables().Count() > 0)
                tw.WriteLine();

            // Signals Initialization
            foreach (ISignalDescriptor signal in cd.GetSignals())
            {
                InitializeSignal(signal, tw);
            }
            if (cd.GetSignals().Count() > 0)
                tw.WriteLine();

            //      Port Binding
            foreach (IComponentDescriptor scd in cd.GetChildComponents())
            {
                PortBinding(scd, tw);
            }

            tw.Indent--;
            tw.WriteLine("}");
        }

        //      ADDED
        private void GenerateMainFile(IProject project, IComponentDescriptor cd)
        {
            string fname = MakeSysCSourceFileName("main");
            string path = project.AddFile(fname);
            StreamWriter sw = new StreamWriter(path);
            IndentedTextWriter tw = new IndentedTextWriter(sw, "  ");
            string SimTimeUnit;

            CreateFileHeader(new GeneratorInfo(fname), tw);
            GenerateDependencies(null, tw, GetComponentName(cd));

            //  Get Simulation Time
            switch (SimTime.Unit)
            {
                case ETimeUnit.fs:
                    SimTimeUnit = "SC_FS";
                    break;
                case ETimeUnit.ps:
                    SimTimeUnit = "SC_PS";
                    break;
                case ETimeUnit.ns:
                    SimTimeUnit = "SC_NS";
                    break;
                case ETimeUnit.us:
                    SimTimeUnit = "SC_US";
                    break;
                case ETimeUnit.ms:
                    SimTimeUnit = "SC_MS";
                    break;
                case ETimeUnit.sec:
                    SimTimeUnit = "SC_SEC";
                    break;
                default:
                    throw new NotImplementedException();
            }

            tw.WriteLine();
            tw.WriteLine("int sc_main(int argc, char* argv[])");
            tw.WriteLine("{");
            tw.Indent++;
            tw.WriteLine("sc_report_handler::set_actions (SC_WARNING, SC_DO_NOTHING);");
            tw.WriteLine();
            tw.WriteLine(GetComponentName(cd) + " "
                + GetComponentName(((ComponentDescriptor)cd).Instance.Representant.Descriptor)
                + "(\"" + GetComponentName(cd) + "\");");

            tw.WriteLine();
            tw.WriteLine("sc_start(" + SimTime.Value + ", " + SimTimeUnit + ");");
            tw.WriteLine();
            tw.WriteLine("return 0;");
            tw.Indent--;
            tw.WriteLine("}");

            tw.Flush();
            tw.Close();
            sw.Close();
        }

        //      ADDED
        private void GeneratePreProcDir(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = GetComponentName(cd).ToUpper() + "_H";
            tw.WriteLine("#ifndef " + name);
            tw.WriteLine("#define " + name);
        }

        private void GeneratePreProcDir(PackageDescriptor pd, IndentedTextWriter tw)
        {
            string name = pd.PackageName.ToUpper() + "_H";
            name = name.Replace('.', '_');
            tw.WriteLine("#ifndef " + name);
            tw.WriteLine("#define " + name);
        }

        public Action<IGeneratorInformation, IndentedTextWriter> CreateFileHeader { get; set; }
    }
}

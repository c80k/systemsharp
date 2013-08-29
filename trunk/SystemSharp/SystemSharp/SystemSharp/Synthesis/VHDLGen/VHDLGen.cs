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

namespace SystemSharp.Synthesis.VHDLGen
{
    public class VHDLGenerator: 
        ICodeGenerator,
        IOperatorNotation,
        IOperatorPrecedence,
        IStringifyInfo
    {
        public interface IGeneratorInformation
        {
            string CurrentFile { get; }
        }

        private class VHDPkg
        {
            public string Name { get; private set; }
            public byte[] FileContent { get; private set; }

            public VHDPkg(string name, byte[] fileContent)
            {
                Name = name;
                FileContent = fileContent;
            }

            public VHDPkg(string name)
            {
                Name = name;
            }
        }

        private class VHDLib
        {
            public string Name { get; private set; }
            public List<VHDPkg> Packages { get; private set; }

            public VHDLib(string name)
            {
                Name = name;
                Packages = new List<VHDPkg>();
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

        private static List<VHDLib> _stdLibraries = new List<VHDLib>();

        private static void InitializeStdLibraries()
        {
            VHDLib ieee = new VHDLib("ieee");
            ieee.Packages.Add(new VHDPkg("std_logic_1164"));
            ieee.Packages.Add(new VHDPkg("std_logic_unsigned"));
            ieee.Packages.Add(new VHDPkg("numeric_std"));
            ieee.Packages.Add(new VHDPkg("math_real"));
            _stdLibraries.Add(ieee);
            VHDLib ieee_p = new VHDLib("ieee_proposed");
            ieee_p.Packages.Add(new VHDPkg("fixed_pkg"));
            ieee_p.Packages.Add(new VHDPkg("float_pkg"));
            //ieee_p.Packages.Add(new VHDPkg("fixed_float_types"));
            _stdLibraries.Add(ieee_p);
            VHDLib work = new VHDLib("work");
            work.Packages.Add(new VHDPkg("image_pkg", SystemSharp.Properties.Resources.image_pkg));
            work.Packages.Add(new VHDPkg("sim_pkg", SystemSharp.Properties.Resources.sim_pkg));
            work.Packages.Add(new VHDPkg("synth_pkg", SystemSharp.Properties.Resources.synth_pkg));
            //work.Packages.Add(new VHDPkg("fixed_float_types", SystemSharp.Properties.Resources.fixed_float_types_c));
            //work.Packages.Add(new VHDPkg("fixed_pkg", SystemSharp.Properties.Resources.fixed_pkg_c));
            //work.Packages.Add(new VHDPkg("float_pkg", SystemSharp.Properties.Resources.float_pkg_c));
            _stdLibraries.Add(work);
        }

        static VHDLGenerator()
        {
            //InitializeTypeMap();
            InitializeStdLibraries();
        }

        private IComponentDescriptor _curComponent;

        private string Convert(Type SType, TypeDescriptor TTypeD, params string[] args)
        {
            List<Type> itypes = new List<Type>();
            string result = VHDLTypes.Convert(SType, TTypeD, itypes, args);
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
                    case UnOp.Kind.Sqrt: return -1;
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
                    case TernOp.Kind.Slice: return -1;
                    default: throw new NotImplementedException();
                }
            }

            public EOperatorAssociativity GetOperatorAssociativity(UnOp.Kind op)
            {
                return EOperatorAssociativity.UseParenthesis;
            }

            public EOperatorAssociativity GetOperatorAssociativity(BinOp.Kind op)
            {
                switch (op)
                {
                    case BinOp.Kind.Add:
                    case BinOp.Kind.And:
                    case BinOp.Kind.Concat:
                    case BinOp.Kind.Mul:
                    case BinOp.Kind.Or:
                    case BinOp.Kind.Sub:
                    case BinOp.Kind.Xor:
                        return EOperatorAssociativity.LeftAssociative;

                    case BinOp.Kind.Div:
                    case BinOp.Kind.Eq:
                    case BinOp.Kind.Exp:
                    case BinOp.Kind.Gt:
                    case BinOp.Kind.GtEq:
                    case BinOp.Kind.Log:
                    case BinOp.Kind.LShift:
                    case BinOp.Kind.Lt:
                    case BinOp.Kind.LtEq:
                    case BinOp.Kind.Max:
                    case BinOp.Kind.Min:
                    case BinOp.Kind.NEq:
                    case BinOp.Kind.Rem:
                    case BinOp.Kind.RShift:
                        return EOperatorAssociativity.UseParenthesis;

                    default:
                        throw new NotImplementedException();
                }
            }

            public EOperatorAssociativity GetOperatorAssociativity(TernOp.Kind op)
            {
                return EOperatorAssociativity.RightAssociative;
            }

            #endregion

        #region IOperatorNotation Members

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

            public NotateFunc GetNotation(UnOp.Kind op)
            {
                switch (op)
                {
                    case UnOp.Kind.Abs: return DefaultNotators.Prefix("abs ");
                    case UnOp.Kind.BitwiseNot: return DefaultNotators.Prefix("not ");
                    case UnOp.Kind.BoolNot: return DefaultNotators.Prefix("not ");
                    case UnOp.Kind.Exp: return DefaultNotators.Function("EXP");
                    case UnOp.Kind.ExtendSign: return DefaultNotators.Function("XTS");
                    case UnOp.Kind.Identity: return DefaultNotators.Prefix("");
                    case UnOp.Kind.Log: return DefaultNotators.Function("LOG");
                    case UnOp.Kind.Neg: return DefaultNotators.Prefix("-");
                    case UnOp.Kind.Sin: return DefaultNotators.Function("SIN");
                    case UnOp.Kind.Cos: return DefaultNotators.Function("COS");
                    case UnOp.Kind.Sqrt: return DefaultNotators.Function("SQRT");
                    default: throw new NotImplementedException();
                }
            }

            public NotateFunc GetNotation(BinOp.Kind op)
            {
                switch (op)
                {
                    case BinOp.Kind.Add: return DefaultNotators.Infix("+");
                    case BinOp.Kind.And: return DefaultNotators.Infix("and");
                    case BinOp.Kind.Concat: return DefaultNotators.Infix("&");
                    case BinOp.Kind.Div: return DefaultNotators.Infix("/");
                    case BinOp.Kind.Eq: return DefaultNotators.Infix("=");
                    case BinOp.Kind.Exp: return DefaultNotators.Infix("**");
                    case BinOp.Kind.Gt: return DefaultNotators.Infix(">");
                    case BinOp.Kind.GtEq: return DefaultNotators.Infix(">=");
                    case BinOp.Kind.Log: return DefaultNotators.Function("LOG");
                    case BinOp.Kind.LShift: return DefaultNotators.Infix("sll");
                    case BinOp.Kind.Lt: return DefaultNotators.Infix("<");
                    case BinOp.Kind.LtEq: return DefaultNotators.Infix("<=");
                    case BinOp.Kind.Mul: return DefaultNotators.Infix("*");
                    case BinOp.Kind.NEq: return DefaultNotators.Infix("/=");
                    case BinOp.Kind.Or: return DefaultNotators.Infix("or");
                    case BinOp.Kind.Rem: return DefaultNotators.Infix("rem");
                    case BinOp.Kind.RShift: return DefaultNotators.Infix("srr");
                    case BinOp.Kind.Sub: return DefaultNotators.Infix("-");
                    case BinOp.Kind.Xor: return DefaultNotators.Infix("xor");
                    default: throw new NotImplementedException();
                }
            }

            public NotateFunc GetNotation(TernOp.Kind op)
            {
                switch (op)
                {
                    case TernOp.Kind.Conditional:
                        return (string[] args) => args[1] + " when " + args[0] + " else " + args[2];

                    case TernOp.Kind.Slice:
                        return (string[] args) => args[0] + "(" + args[1] + " downto " + args[2] + ")";

                    default:
                        throw new NotImplementedException();
                }
            }

            private string NotateFunctionCallInternal(ICallable callee, IEnumerable<string> args)
            {
                Contract.Requires<ArgumentNullException>(callee != null);
                Contract.Requires<ArgumentNullException>(args != null);

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
                            return DefaultNotators.NotateFunction("COS", args);

                        case IntrinsicFunction.EAction.GetArrayElement:
                        case IntrinsicFunction.EAction.Index:
                            return args[0] + "(" + args[1] + ")";

                        case IntrinsicFunction.EAction.GetArrayLength:
                            return args[0] + "'length";

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
                                    return "-- new array of type " + aparams.ElementType.Name + " with " + args[0] + " elements";
                                }
                            }

                        case IntrinsicFunction.EAction.NewObject:
                            // actually not supported/allowed
                            return "-- new " + args[0];

                        case IntrinsicFunction.EAction.PropertyRef:
                            {
                                object prop = ifun.Parameter;
                                if (prop is DesignContext.EProperties)
                                {
                                    DesignContext.EProperties rprop = (DesignContext.EProperties)prop;
                                    switch (rprop)
                                    {
                                        case DesignContext.EProperties.CurTime:
                                            return "NOW";

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
                                            return args[0] + "'length";

                                        default:
                                            throw new NotImplementedException();
                                    }
                                }
                                else
                                    throw new NotImplementedException();                                
                            }
                            //break;

                        case IntrinsicFunction.EAction.Report:
                        case IntrinsicFunction.EAction.ReportLine:
                            if (args.Length > 0)
                                return "report " + args[0];
                            else
                                return "";

                        case IntrinsicFunction.EAction.Sign:
                            return DefaultNotators.NotateFunction("SIGN", args);

                        case IntrinsicFunction.EAction.SimulationContext:
                            // actually not supported/allowed
                            return "-- SimContext not supported";

                        case IntrinsicFunction.EAction.Sin:
                            return DefaultNotators.NotateFunction("SIN", args);

                        case IntrinsicFunction.EAction.Slice:
                            {
                                if (ifun.Parameter != null)
                                {
                                    // static slice range
                                    var range = (Range)ifun.Parameter;
                                    return args[0] + "(" + range.FirstBound + " downto " + range.SecondBound + ")";
                                }
                                else
                                {
                                    // dynamic slice range
                                    return args[0] + "(" + args[1] + " downto " + args[2] + ")";
                                }
                            }

                        case IntrinsicFunction.EAction.Sqrt:
                            return DefaultNotators.NotateFunction("SQRT", args);

                        case IntrinsicFunction.EAction.StringConcat:
                            {
                                StringBuilder sb = new StringBuilder();
                                for (int i = 0; i < args.Length; i++)
                                {
                                    if (i > 0)
                                        sb.Append(" & ");
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
                                        return "wait for " + args[0];

                                    case WaitParams.EWaitKind.WaitOn:
                                        {
                                            StringBuilder sb = new StringBuilder();
                                            sb.Append("wait on ");
                                            bool first = true;
                                            foreach (string arg in args)
                                            {
                                                if (first)
                                                    first = false;
                                                else
                                                    sb.Append(", ");
                                                sb.Append(arg);
                                            }
                                            return sb.ToString();
                                        }
                                        
                                    case WaitParams.EWaitKind.WaitUntil:
                                        {
                                            return "wait until " + args[0];
                                        }
                                    default:
                                        throw new NotImplementedException();
                                }
                            }

                        case IntrinsicFunction.EAction.Resize:
                            {
                                ResizeParams rparams = (ResizeParams)ifun.Parameter;
                                string result = "resize(" + args[0] + ", ";
                                if (rparams == null)
                                {
                                    if (args.Length == 2)
                                    {
                                        result += args[1];
                                    }
                                    else
                                    {
                                        result += (args[1] + "-1");
                                        if (args.Length > 2)
                                        {
                                            result += ", -" + args[2];
                                        }
                                    }
                                }
                                else
                                {
                                    result += (rparams.NewIntWidth-1);
                                    if (rparams.NewFracWidth > int.MinValue)
                                        result += ", " + (-rparams.NewFracWidth);
                                }
                                result += ")";
                                return result;
                            }

                        case IntrinsicFunction.EAction.Barrier:
                            {
                                return "-- barrier";
                            }

                        case IntrinsicFunction.EAction.MkDownRange:
                            {
                                return args[0] + " downto " + args[1];
                            }

                        case IntrinsicFunction.EAction.MkUpRange:
                            {
                                return args[0] + " to " + args[1];
                            }

                        case IntrinsicFunction.EAction.Abs:
                            {
                                return "abs " + args[0];
                            }

                        case IntrinsicFunction.EAction.FileOpenRead:
                            {
                                return "file_open({0}, " + args[0] + ", READ_MODE)";
                            }

                        case IntrinsicFunction.EAction.FileOpenWrite:
                            {
                                return "file_open({0}, " + args[0] + ", WRITE_MODE)";
                            }

                        case IntrinsicFunction.EAction.FileClose:
                            {
                                return "file_close(" + args[0] + ")";
                            }

                        case IntrinsicFunction.EAction.FileRead:
                            {
                                throw new NotImplementedException();
                            }

                        case IntrinsicFunction.EAction.FileReadLine:
                            {
                                throw new NotImplementedException();
                            }

                        case IntrinsicFunction.EAction.FileWrite:
                            {
                                return "write(" + args[0] + "_buf, " + args[1] + ")";
                            }

                        case IntrinsicFunction.EAction.FileWriteLine:
                            {
                                string s = "";
                                if (args.Length > 1)
                                    s = "write(" + args[0] + "_buf, " + args[1] + "); ";
                                s += "writeline(" + args[0] + ", " + args[0] + "_buf)";
                                return s;
                            }

                        case IntrinsicFunction.EAction.ExitProcess:
                            {
                                return "wait";
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

            private string VHDLifyConstant(object value)
            {
                return GetValueID(value);
            }

            class LiteralStringifier: ILiteralVisitor
            {
                private VHDLGenerator _vhdg;
                public string Result { get; private set; }
                public LiteralReference.EMode Mode { get; private set; }

                public LiteralStringifier(VHDLGenerator vhdg, LiteralReference.EMode mode)
                {
                    _vhdg = vhdg;
                    Mode = mode;
                }

                #region ILiteralVisitor Member

                public void VisitConstant(Constant constant)
                {
                    object value = constant.ConstantValue;
                    Result = _vhdg.VHDLifyConstant(value);
                }

                public void VisitVariable(Variable variable)
                {
                    Result = _vhdg.MakeIDName(variable.Name, variable);
                }

                public void VisitFieldRef(FieldRef fieldRef)
                {
                    Result = _vhdg.MakeIDName(fieldRef.Name, fieldRef.FieldDesc);
                }

                public void VisitThisRef(ThisRef thisRef)
                {
                    Result = "<this>";
                }

                public void VisitSignalRef(SignalRef signalRef)
                {
                    ISignalOrPortDescriptor desc = signalRef.Desc;
                    SignalRef remappedRef = signalRef.AssimilateIndices();
                    if (_vhdg._curComponent != null && 
                        !(signalRef.Desc is SignalArgumentDescriptor))
                    {
                        remappedRef = signalRef.RelateToComponent(_vhdg._curComponent);
                        if (remappedRef == null)
                            throw new InvalidOperationException("Referenced signal unknown to declaring component");
                    }
                    string name = remappedRef.Name;
                    StringBuilder sb = new StringBuilder();
                    var udesc = desc.GetUnindexedContainer();
                    sb.Append(_vhdg.MakeIDName(name, udesc.GetBoundSignal()));
                    if (!remappedRef.IsStaticIndex ||
                        !remappedRef.IndexSample.Equals(udesc.ElementType.Index))
                    {
                        foreach (Expression[] indexSpec in remappedRef.GetFullIndices())
                        {
                            if (indexSpec.Length == 0)
                                continue;

                            sb.Append("(");
                            bool first = true;
                            foreach (Expression index in indexSpec.Reverse())
                            {
                                if (first)
                                    first = false;
                                else
                                    sb.Append(", ");
                                sb.Append(index.ToString(_vhdg));
                            }
                            sb.Append(")");
                        }
                    }
                    switch (signalRef.Prop)
                    {
                        case SignalRef.EReferencedProperty.ChangedEvent:
                            Result = "-- changed event not supported";
                            break;

                        case SignalRef.EReferencedProperty.Cur:
                        case SignalRef.EReferencedProperty.Instance:
                        case SignalRef.EReferencedProperty.Next:
                            Result = sb.ToString();
                            break;

                        case SignalRef.EReferencedProperty.Pre:
                            sb.Append("'pre");
                            Result = sb.ToString();
                            break;

                        case SignalRef.EReferencedProperty.RisingEdge:
                            Result = "rising_edge(" + sb.ToString() + ")";
                            break;

                        case SignalRef.EReferencedProperty.FallingEdge:
                            Result = "falling_edge(" + sb.ToString() + ")";
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }

                public void VisitArrayRef(ArrayRef arrayRef)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(arrayRef.ArrayExpr.ToString(_vhdg));
                    var indices = arrayRef.Indices.Select(idx => idx.ToString(_vhdg));
                    sb.Append("(");
                    sb.Append(string.Join(", ", indices));
                    sb.Append(")");
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

        private class VHDLStatementsGen : IStatementVisitor
        {
            private IndentedTextWriter _tw;
            private VHDLGenerator _vhdg;

            public VHDLStatementsGen(IndentedTextWriter tw, VHDLGenerator vhdg)
            {
                _tw = tw;
                _vhdg = vhdg;
            }

            private void GenerateForLoop(LoopBlock loop)
            {
                _tw.Write("for ");
                _tw.Write(_vhdg.GetLiteralNotation()(loop.CounterVariable, LiteralReference.EMode.Direct));
                _tw.Write(" in ");
                _tw.Write(loop.CounterStart.ToString(_vhdg));
                Expression counterStop = loop.CounterStop;
                switch (loop.CounterDirection)
                {
                    case LoopBlock.ECounterDirection.IncrementOne:
                        _tw.Write(" to ");
                        break;

                    case LoopBlock.ECounterDirection.DecrementOne:
                        _tw.Write(" downto ");
                        break;

                    default:
                        throw new NotImplementedException();
                }
                _tw.Write(counterStop.ToString(_vhdg));
                if (loop.CounterLimitKind == LoopBlock.ELimitKind.ExcludingStopValue)
                {
                    switch (loop.CounterDirection)
                    {
                        case LoopBlock.ECounterDirection.IncrementOne:
                            _tw.Write(" - 1");
                            break;

                        case LoopBlock.ECounterDirection.DecrementOne:
                            _tw.Write(" + 1");
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }
                _tw.Write(" ");
                GenerateLoop(loop);
                loop.Trailer.Accept(this);
            }

            private void GenerateWhileLoop(LoopBlock loop)
            {
                _tw.Write("while ");
                _tw.Write(loop.HeadCondition.ToString(_vhdg));
                _tw.Write(" ");
                GenerateLoop(loop);
                loop.Trailer.Accept(this);
            }

            private void GenerateLoop(LoopBlock stmt)
            {
                _tw.WriteLine("loop");
                _tw.Indent++;
                stmt.Body.AcceptIfEnabled(this);
                _tw.Indent--;
                _tw.Write("end loop");
                if (stmt.Label != null)
                    _tw.Write(" " + stmt.Label);
                _tw.WriteLine(";");
            }

            private void GenerateComments(Statement stmt)
            {
                string comment = stmt.Comment;
                if (comment == null)
                    return;
                string[] lines = comment.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    _tw.WriteLine("-- " + line);
                }
            }

            #region IStatementVisitor Members

            public void AcceptCompoundStatement(CompoundStatement stmt)
            {
                GenerateComments(stmt);
                LoopBlock forLoop = stmt.AsForLoop(EForLoopLevel.StrictOneInc);
                if (forLoop != null)
                {
                    if (forLoop.Label != null)
                        _tw.Write(forLoop.Label + ": ");
                    GenerateForLoop(forLoop);
                }
                else
                {
                    foreach (Statement substmt in stmt.Statements)
                        substmt.AcceptIfEnabled(this);
                }
            }

            public void AcceptLoopBlock(LoopBlock stmt)
            {
                if (stmt.Label != null)
                    _tw.Write(stmt.Label + ": ");
                GenerateComments(stmt);

                LoopBlock whileLoop = stmt.AsWhileLoop();
                if (whileLoop != null)
                {
                    GenerateWhileLoop(whileLoop);
                }
                else
                {
                    GenerateLoop(stmt);
                }
            }

            public void AcceptBreakLoop(BreakLoopStatement stmt)
            {
                GenerateComments(stmt);
                _tw.Write("exit");
                if (stmt.Loop.Label != null)
                    _tw.Write(" " + stmt.Loop.Label);
                _tw.WriteLine(";");
            }

            public void AcceptContinueLoop(ContinueLoopStatement stmt)
            {
                GenerateComments(stmt);
                _tw.Write("next");
                if (stmt.Loop.Label != null)
                    _tw.Write(" " + stmt.Loop.Label);
                _tw.WriteLine(";");
            }

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
                        _tw.Write("elsif (");
                    }
                    Expression cond = stmt.Conditions[i];
                    string strcond = cond.ToString(_vhdg);
                    _tw.Write(strcond);
                    _tw.WriteLine(") then");
                    _tw.Indent++;
                    stmt.Branches[i].AcceptIfEnabled(this);
                    _tw.Indent--;
                }
                if (stmt.Branches.Count > stmt.Conditions.Count)
                {
                    _tw.WriteLine("else");
                    _tw.Indent++;
                    stmt.Branches.Last().AcceptIfEnabled(this);
                    _tw.Indent--;
                }
                _tw.WriteLine("end if;");
            }

            public void AcceptCase(CaseStatement stmt)
            {
                if (stmt.Label != null)
                {
                    _tw.Write(stmt.Label + ": ");
                }
                GenerateComments(stmt);
                _tw.Write("case ");
                _tw.Write(stmt.Selector.ToString(_vhdg));
                _tw.WriteLine(" is");
                _tw.Indent++;
                for (int i = 0; i < stmt.Cases.Count; i++)
                {
                    _tw.Write("when ");
                    _tw.Write(stmt.Cases[i].ToString(_vhdg));
                    _tw.WriteLine(" =>");
                    _tw.Indent++;
                    stmt.Branches[i].AcceptIfEnabled(this);
                    _tw.Indent--;
                }
                if (stmt.Branches.Count > stmt.Cases.Count)
                {
                    _tw.WriteLine("when others =>");
                    _tw.Indent++;
                    stmt.Branches.Last().AcceptIfEnabled(this);
                    _tw.Indent--;
                }
                _tw.Indent--;
                _tw.WriteLine("end case;");
            }

            public void AcceptStore(StoreStatement stmt)
            {
                GenerateComments(stmt);
                IStorableLiteral literal = stmt.Container;
                if (stmt.Container == null)
                    _tw.Write("<???>");
                else if (literal.Type.HasIntrinsicTypeOverride &&
                    literal.Type.IntrinsicTypeOverride == EIntrinsicTypes.File)
                {
                    string file = _vhdg.GetLiteralNotation()((Literal)stmt.Container, LiteralReference.EMode.Direct);
                    string open = stmt.Value.ToString(_vhdg);
                    string line = string.Format(open, file);
                    _tw.Write(line);
                }
                else
                {
                    _tw.Write(_vhdg.GetLiteralNotation()((Literal)stmt.Container, LiteralReference.EMode.Direct));
                    if (literal.StoreMode == EStoreMode.Transfer)
                        _tw.Write(" <= ");
                    else
                        _tw.Write(" := ");
                    _tw.Write(stmt.Value.ToString(_vhdg));
                }
                _tw.WriteLine(";");
            }

            public void AcceptNop(NopStatement stmt)
            {
            }

            public void AcceptSolve(SolveStatement stmt)
            {
                _tw.WriteLine("-- solve unsupported");
            }

            public void AcceptBreakCase(BreakCaseStatement stmt)
            {
                _tw.WriteLine("-- break");
            }

            public void AcceptGotoCase(GotoCaseStatement stmt)
            {
                _tw.WriteLine("-- goto case unsupported");
            }

            public void AcceptGoto(GotoStatement stmt)
            {
                _tw.WriteLine("-- goto unsupported");
            }

            public void AcceptReturn(ReturnStatement stmt)
            {
                GenerateComments(stmt);
                _tw.Write("return");
                if (stmt.ReturnValue != null)
                    _tw.Write(" " + stmt.ReturnValue.ToString(_vhdg));
                _tw.WriteLine(";");
            }

            public void AcceptThrow(ThrowStatement stmt)
            {
                _tw.WriteLine("-- throw not supported");
            }

            public void AcceptCall(CallStatement stmt)
            {
                GenerateComments(stmt);
                FunctionCall tmp = new FunctionCall()
                {
                    Callee = stmt.Callee,
                    Arguments = stmt.Arguments
                };
                _tw.Write(tmp.ToString(_vhdg));
                _tw.WriteLine(";");
            }

            #endregion
        }

        #region VHDL keywords
        public static readonly string[] Keywords = { "abs", "access", "after", "alias", "all", "and", "architecture",
            "array", "assert", "attribute", "begin", "block", "body", "buffer", "bus", "case", "component", "configuration",
            "constant", "disconnect", "downto", "else", "elsif", "end", "entity", "exit", "file", "for", "function", "generate",
            "generic", "group", "guarded", "if", "impure", "in", "inertial", "inout", "is", "label", "library", "linkage",
            "literal", "loop", "map", "mod", "nand", "new", "next", "nor", "not", "null", "of", "on", "open", "or", "others",
            "out", "package", "port", "postponed", "procedure", "process", "pure", "range", "record", "register", "reject",
            "return", "rol", "ror", "select", "severity", "signal", "shared", "sla", "sli", "sra", "srl", "subtype", "then",
            "to", "transport", "type", "unaffected", "units", "until", "use", "variable", "wait", "when", "while", "with",
            "xnor", "xor" };
        #endregion

        private ScopedIdentifierManager _sim = new ScopedIdentifierManager(false);
        private Dictionary<string, HashSet<string>> _pkgRefs = new Dictionary<string, HashSet<string>>();
        private HashSet<Tuple<string, string>> _synthPkgRefs = new HashSet<Tuple<string, string>>();

        public VHDLGenerator()
        {
            InitKeywords();
            CreateFileHeader = DefaultCreateFileHeader;
        }

        private void InitKeywords()
        {
            foreach (string name in Keywords)
                _sim.GetUniqueName(name, new object(), true);
        }

        public void DefaultCreateFileHeader(IGeneratorInformation geni, IndentedTextWriter tw)
        {
            tw.WriteLine("-- This file was automatically generated by the System# framework.");
            tw.WriteLine("-- Generated file: " + geni.CurrentFile + ".vhd");
            tw.WriteLine("-- Creation time:  " + DateTime.Now.ToString());
            tw.WriteLine();
        }

        private enum EVHDLIdState
        {
            Beginning,
            Inside,
            Underscore
        }

        string MakeIDName(string name, bool rootScope = false)
        {
            return MakeIDName(name, name.ToLower(), rootScope);
        }

        string MakeValidVHDLIdentifier(string name)
        {
            if (name == null)
                return "???";

            StringBuilder goodName = new StringBuilder();
            EVHDLIdState cur = EVHDLIdState.Beginning;
            foreach (char c in name)
            {
                switch (cur)
                {
                    case EVHDLIdState.Beginning:
                        if (char.IsLetter(c))
                        {
                            goodName.Append(c);
                            cur = EVHDLIdState.Inside;
                        }
                        else
                        {
                            goodName.Append('m');
                            if (char.IsDigit(c))
                            {
                                goodName.Append(c);
                                cur = EVHDLIdState.Inside;
                            }
                            else if (c == '_')
                            {
                                goodName.Append('_');
                                cur = EVHDLIdState.Underscore;
                            }
                            else
                            {
                                cur = EVHDLIdState.Inside;
                            }
                        }
                        break;

                    case EVHDLIdState.Inside:
                        if (char.IsLetterOrDigit(c))
                        {
                            goodName.Append(c);
                            cur = EVHDLIdState.Inside;
                        }
                        else
                        {
                            goodName.Append('_');
                            cur = EVHDLIdState.Underscore;
                        }
                        break;

                    case EVHDLIdState.Underscore:
                        if (char.IsLetterOrDigit(c))
                        {
                            goodName.Append(c);
                            cur = EVHDLIdState.Inside;
                        }
                        else
                        {
                            cur = EVHDLIdState.Underscore;
                        }
                        break;
                }
            }
            return goodName.ToString().TrimEnd('_');
        }

        string MakeIDName(string name, object item, bool rootScope = false)
        {
            string goodName = MakeValidVHDLIdentifier(name);
            return _sim.GetUniqueName(goodName, item, rootScope);
        }

        string MakeIDName(string name, object item, ScopedIdentifierManager sim)
        {
            string goodName = MakeValidVHDLIdentifier(name);
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

        private string MakeVHDSourceFileName(string name)
        {
            return name + ".vhd";
        }

        private string PortDirectionToString(EPortDirection dir)
        {
            switch (dir)
            {
                case EPortDirection.In: return "in";
                case EPortDirection.Out: return "out";
                case EPortDirection.InOut: return "inout";
                default: throw new NotImplementedException();
            }
        }

        private bool LookupType(Type type, out TypeInfo ti)
        {
            if (VHDLTypes.LookupType(type, out ti))
            {
                if (ti.Libraries != null)
                {
                    for (int i = 0; i < ti.Libraries.Length; i += 2)
                    {
                        string lib = ti.Libraries[i];
                        string pkg = ti.Libraries[i + 1];
                        _pkgRefs.Add(lib, pkg);
                        if (!ti.IsNotSynthesizable)
                            _synthPkgRefs.Add(Tuple.Create(lib, pkg));
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
            string result = "(" + range.FirstBound + " ";
            switch (range.Direction)
            {
                case EDimDirection.Downto: result += "downto"; break;
                case EDimDirection.To: result += "to"; break;
                default: throw new NotImplementedException();
            }
            result += " " + range.SecondBound + ")";
            return result;
        }

        private string GetTypeDescriptorCompletedName(TypeDescriptor td)
        {
            Contract.Requires(td != null);

            TypeInfo ti;
            if (LookupType(td.CILType, out ti))
            {
                return ti.DeclareCompletedType(td);
            }

            string name = GetTypeDescriptorName(td);
            if (td.Rank > 0)
            {
                if (td.Constraints != null)
                    name += GetRangeSuffix(td.Constraints[0]);
                else
                    name += "(???)";
            }
            return name;
        }

        private void DeclareArrayInitializer(Array array, int dim, long[] indices, StringBuilder sb)
        {
            long length = array.GetLongLength(dim);
            sb.Append("(");
            if (length == 1)
            {
                sb.Append("0 => ");
            }
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
            sb.Append(")");
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
            else if (obj == null)
            {
                return "?null?";
            }
            else
            {
                string result;
                if (VHDLTypes.GetValueOf(obj, out result))
                    return result;
                else
                    return obj.ToString();
            }
        }

        private void DeclarePortList(IComponentDescriptor cd, IndentedTextWriter tw, bool extScope)
        {
            if (cd.GetPorts().Count() == 0)
                return;
            tw.WriteLine("port (");
            tw.Indent++;
            bool first = true;
            ScopedIdentifierManager xsim = _sim;
            if (extScope)
            {
                xsim = _sim.Fork();
                xsim.PushScope();
            }
            foreach (IPortDescriptor pd in cd.GetPorts())
            {
                if (first)
                    first = false;
                else
                    tw.WriteLine(";");

                string pid = MakeIDName(pd.Name, pd.BoundSignal, xsim);

                tw.Write(pid + ": " + PortDirectionToString(pd.Direction) + " " +
                    GetTypeDescriptorCompletedName(pd.ElementType));
                if (pd.Direction != EPortDirection.In &&
                    pd.InitialValue != null)
                {
                    tw.Write(" := ");
                    tw.Write(GetValueID(pd.InitialValue));
                }
            }
            tw.WriteLine(");");
        }

        private void DeclareEntity(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = GetComponentName(cd);
            tw.WriteLine("entity " + name + " is");
            tw.Indent++;
            DeclarePortList(cd, tw, false);
            tw.Indent--;
            tw.Indent--;
            tw.WriteLine("end " + name + ";");
        }

        private void DeclareComponent(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = GetComponentName(cd);
            tw.WriteLine("component " + name + " is");
            tw.Indent++;
            DeclarePortList(cd, tw, true);
            tw.Indent--;
            tw.Indent--;
            tw.WriteLine("end component;");
        }

        private void DeclareComponentInstance(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = GetComponentName(cd);
            string rname = GetComponentName(((ComponentDescriptor)cd).Instance.Representant.Descriptor);
            if (_curComponent != null && // may happen if called prior to actual VHDL generation in OnSynthesis() method of a component
                GetLibrary(_curComponent) != GetLibrary(cd))
                rname = "entity " + GetLibrary(cd) + "." + rname;
            tw.WriteLine("inst_" + name + ": " + rname);
            tw.Indent++;
            tw.WriteLine("port map(");
            tw.Indent++;
            IComponentDescriptor owner = (IComponentDescriptor)cd.Owner;
            bool first = true;
            var xsim = _sim.Fork();
            xsim.PushScope();
            foreach (IPortDescriptor pd in cd.GetPorts())
            {
                if (first)
                    first = false;
                else
                    tw.WriteLine(",");

                var ownerRef = pd
                    .BoundSignal
                    .AsSignalRef(SignalRef.EReferencedProperty.Cur);
                if (ownerRef == null)
                    throw new InvalidOperationException("Bound signal unknown to instantiating component");

                var litstr = new LiteralStringifier(this, LiteralReference.EMode.Direct);
                var temp = _curComponent;
                _curComponent = owner;
                litstr.VisitSignalRef(ownerRef);
                var rhs = litstr.Result;
                _curComponent = temp;
                
                string pid = MakeIDName(pd.Name, pd.BoundSignal.RemoveIndex(), xsim);
                /*string sigExpr = MakeIDName(spd.Name, pd.BoundSignal.RemoveIndex());
                if (pd.BoundSignal is InstanceDescriptor &&
                    ((InstanceDescriptor)pd.BoundSignal).Index != null)
                    sigExpr += ((InstanceDescriptor)pd.BoundSignal).Index.ToString();
                tw.Write(pid + " => " + sigExpr);*/
                tw.Write(pid + " => " + rhs);
            }
            tw.WriteLine(");");
            tw.Indent--;
            tw.Indent--;
        }

        private void GenerateMethodBody(CodeDescriptor cd, IndentedTextWriter tw)
        {
            _sim.PushScope();
            tw.Indent++;
            for (int i = 0; i < cd.Implementation.LocalVariables.Count; i++)
            {
                var local = cd.Implementation.LocalVariables[i];
                string typestr = GetTypeDescriptorCompletedName(local.Type);                
                string varname = MakeIDName(local.Name, local);

                if (local.Type.HasIntrinsicTypeOverride &&
                    local.Type.IntrinsicTypeOverride == EIntrinsicTypes.File)
                {
                    string bufname = varname + "_buf";
                    tw.WriteLine("file " + varname + ": TEXT;");
                    tw.WriteLine("variable " + bufname + ": LINE;");
                }
                else
                {
                    tw.Write("variable " + varname + ": " + typestr);
                    if (cd.ValueRangeConstraints[i].IsConstrained)
                    {
                        CodeDescriptor.ValueRangeConstraint vrc = cd.ValueRangeConstraints[i];
                        tw.Write(" range " + vrc.MinValue + " to " + vrc.MaxValue);
                    }
                    else
                    {
                        TypeInfo ti;
                        if (LookupType(local.Type.CILType, out ti) &&
                            ti.RangeSpec == TypeInfo.ERangeSpec.ByRange &&
                            ti.ShowDefaultRange)
                        {
                            tw.Write(" range " + ti.DefaultRange.FirstBound + " to " + ti.DefaultRange.SecondBound);
                        }
                    }
                    tw.WriteLine(";");
                }
            }
            tw.Indent--;
            tw.WriteLine("begin");
            tw.Indent++;
            var impl = cd.Implementation.MakeIntegerIndices();
            impl.Body.PreprocessForLoopKindRecognition();
            impl.Body.AcceptIfEnabled(new VHDLStatementsGen(tw, this));
            tw.Indent--;
            tw.Write("end");
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

            List<ConcurrentStatement> cstmts;
            if (pd.Implementation
                .MakeIntegerIndices()
                .TryAsConcurrentStatements(out cstmts))
            {
                foreach (var cstmt in cstmts)
                {
                    var ls = new LiteralStringifier(this, LiteralReference.EMode.Direct);
                    ls.VisitSignalRef(cstmt.TargetSignal);
                    tw.Write(ls.Result);
                    tw.Write(" <= ");
                    tw.Write(cstmt.SourceExpression.ToString(this));
                    tw.WriteLine(";");
                }
                tw.WriteLine();
            }
            else
            {
                string pname = MakeIDName(pd.Name, pd);
                tw.Write(pname + ": process");
                if (pd.Kind == Components.Process.EProcessKind.Triggered &&
                    pd.Sensitivity.Length > 0)
                {
                    tw.Write("(");
                    bool first = true;
                    var sens = pd.Sensitivity.Select(spd => 
                        DieWithUnknownSensitivityIfNull(spd
                            .AsSignalRef(SignalRef.EReferencedProperty.Instance)
                            .RelateToComponent(_curComponent))
                        .AssimilateIndices()
                        .Desc)
                        .Distinct();
                    foreach (var spd in sens)
                    {
                        if (first)
                            first = false;
                        else
                            tw.Write(", ");
                        tw.Write(MakeIDName(spd.Name, spd.GetBoundSignal()));
                    }
                    tw.Write(")");
                }
                tw.WriteLine();
                GenerateMethodBody(pd, tw);
                tw.WriteLine(" process;");
            }

            if (simuOnly)
                SwitchOnSynthesis(tw);
        }

        private void DeclareField(FieldDescriptor field, IndentedTextWriter tw)
        {
            string fname = MakeIDName(field.Name, field);
            if (field.ConstantValue != null)
            {
                string prefix = field.IsConstant ? "constant " : "shared variable ";
                tw.WriteLine(prefix + fname + ": " +
                    GetTypeDescriptorCompletedName(field.Type) + " := " +
                    VHDLifyConstant(field.ConstantValue) + ";");
            }
            else
            {
                tw.WriteLine("shared variable " + fname + ": " +
                    GetTypeDescriptorCompletedName(field.Type) + ";");
            }
        }

        private void GenerateArchitecture(IComponentDescriptor cd, IndentedTextWriter tw)
        {
            string name = GetComponentName(cd);
            tw.WriteLine("architecture behavioral of " + name + " is");
            tw.Indent++;

            foreach (TypeDescriptor td in cd.GetTypes())
            {
                GenerateTypeDecl(td, tw);
            }
            if (cd.GetTypes().Count() > 0)
                tw.WriteLine();

            var components = cd.GetChildComponents()
                .Cast<ComponentDescriptor>()
                .Select(c => c.Instance.Representant)
                .Distinct()
                .Select(c => c.Descriptor);

            foreach (ComponentDescriptor scd in components)
            {
                DeclareComponent(scd, tw);
                tw.WriteLine();
            }

            foreach (FieldDescriptor field in cd.GetConstants())
            {
                DeclareField(field, tw);
            }
            if (cd.GetConstants().Count() > 0)
                tw.WriteLine();

            foreach (FieldDescriptor field in cd.GetVariables())
            {
                DeclareField(field, tw);
            }
            if (cd.GetVariables().Count() > 0)
                tw.WriteLine();

            foreach (ISignalOrPortDescriptor sd in cd.GetSignals())
            {
                object initVal = sd.InitialValue;
                string initSuffix = "";
                if (initVal != null)
                    initSuffix = " := " + GetValueID(initVal);
                string sname = MakeIDName(sd.Name, sd);
                tw.WriteLine("signal " + sname + ": " +
                    GetTypeDescriptorCompletedName(sd.ElementType) + initSuffix + ";");
            }
            if (cd.GetSignals().Count() > 0)
                tw.WriteLine();

            foreach (MethodDescriptor md in cd.GetActiveMethods())
            {
                GenerateMethodImpl(md, tw);
                tw.WriteLine();
            }
            if (cd.GetActiveMethods().Count() > 0)
                tw.WriteLine();

            tw.Indent--;
            tw.WriteLine("begin");
            tw.Indent++;

            foreach (IComponentDescriptor scd in cd.GetChildComponents())
            {
                DeclareComponentInstance(scd, tw);
                tw.WriteLine();
            }

            foreach (ProcessDescriptor pd in cd.GetProcesses())
            {
                DeclareProcess(pd, tw);
                tw.WriteLine();
            }

            tw.Indent--;
            tw.WriteLine("end behavioral;");
        }

        public void GenerateComponent(IProject project, IComponentDescriptor cd)
        {
            string name = GetComponentName(cd);
            string fname = MakeVHDSourceFileName(name);
            string path = project.AddFile(fname);
            project.AddFileAttribute(fname, cd);
            if (cd.Library != null)
                project.SetFileLibrary(fname, cd.Library);
            _sim.PushScope();
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            IndentedTextWriter tw = new IndentedTextWriter(sw, "  ");
            _curComponent = cd;
            ClearDependencies();
            DeclareEntity(cd, tw);
            tw.WriteLine();
            GenerateArchitecture(cd, tw);
            tw.Flush();
            sw = new StreamWriter(path);
            tw = new IndentedTextWriter(sw, "  ");

            CreateFileHeader(new GeneratorInfo(name), tw);
            GenerateDependencies((IPackageOrComponentDescriptor)cd, tw);
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

            var gi = new VHDLGenInfo(name, "inst_" + name, "behavioral", fname);
            var d = (DescriptorBase)cd;
            d.AddAttribute(gi);
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
                tw.Write("procedure");
            }
            else
            {
                if (!md.HasAttribute<ISideEffectFree>())
                    tw.Write("impure ");
                tw.Write("function");
            }
            tw.Write(" " + mname);
            ArgumentDescriptor[] args = md.GetArguments().ToArray();
            if (args.Length > 0)
            {
                tw.Write("(");
                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                        tw.Write("; ");

                    ArgumentDescriptor argd = args[i];
                    SignalArgumentDescriptor sargd = argd as SignalArgumentDescriptor;
                    object refobj = argd.Argument;
                    if (sargd != null)
                        refobj = sargd.SignalInstance.Descriptor;
                    string name = MakeIDName(argd.Name, refobj);
                    if (sargd != null)
                    {
                        if (sargd.Direction != ArgumentDescriptor.EArgDirection.In)
                        {
                            tw.WriteLine();
                            tw.WriteLine("-- the next argument is not declared as an input argument");
                            tw.WriteLine("-- generated code is probably not correct.");
                        }
                        tw.Write("signal " + name + ": " + DirectionToString(sargd.FlowDirection) + " ");
                        tw.Write(GetTypeDescriptorCompletedName(sargd.ElementType));
                    }
                    else
                    {
                        TypeDescriptor argType = argd.Argument.Type;
                        if (argType.IsByRef)
                            argType = argType.Element0Type;

                        tw.Write(name + ": " + DirectionToString(argd.Direction) + " " +
                            GetTypeDescriptorName(argType));
                    }
                }
                tw.Write(")");
            }
            if (!method.ReturnType.Equals(typeof(void)))
            {
                //TypeDescriptor td = md.ArgTypes
                tw.Write(" return ");
                tw.Write(GetTypeDescriptorName(md.ReturnType));
            }
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
            tw.WriteLine(" is");
            GenerateMethodBody(md, tw);
            MethodInfo method = (MethodInfo)md.Method;
            if (method.ReturnType.Equals(typeof(void)))
                tw.Write(" procedure ");
            else
                tw.Write(" function ");
            string mname = _sim.GetUniqueName(md.Name, md);
            tw.WriteLine(mname + ";");
            _sim.PopScope();
        }

        private bool IsNotSynthesizable(TypeDescriptor td)
        {
            TypeInfo ti;
            if (VHDLTypes.LookupType(td.CILType, out ti))
            {
                if (ti.IsNotSynthesizable)
                    return true;
            }
            if (td.Rank > 0 && VHDLTypes.LookupType(td.Element0Type.CILType, out ti))
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
            tw.Write("type " + tname + " is");
            if (td.Rank > 0)
            {
                string ename = GetTypeDescriptorCompletedName(td.Element0Type);
                tw.WriteLine(" array(integer range <>) of " + ename + ";");
            }
            else if (td.CILType.IsEnum)
            {
                tw.Write(" (");
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
                tw.WriteLine(");");
            }
            else if (!td.CILType.IsPrimitive)
            {
                tw.WriteLine();
                tw.Indent++;
                tw.WriteLine("record");
                tw.Indent++;
                FieldInfo[] fields = 
                    td.CILType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in fields)
                {
                    string fnamestr = MakeIDName(field.Name, field);
                    TypeDescriptor ftype = td.GetFieldType(field);
                    string ftypestr = GetTypeDescriptorCompletedName(ftype);
                    tw.WriteLine(fnamestr + ": " + ftypestr + ";");
                }
                tw.Indent--;
                tw.WriteLine("end record;");
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

        private void GenerateDependencies(IPackageOrComponentDescriptor desc, IndentedTextWriter tw)
        {
            foreach (var kvp in _pkgRefs.OrderBy(kvp => kvp.Key))
            {
                bool allNonSynth = kvp.Value.All(s => !_synthPkgRefs.Contains(Tuple.Create(kvp.Key, s)));
                if (allNonSynth)
                    SwitchOffSynthesis(tw);
                tw.WriteLine("library " + kvp.Key + ";");
                foreach (string pkg in kvp.Value.OrderBy(s => s))
                {
                    bool nonSynth = !allNonSynth && !_synthPkgRefs.Contains(Tuple.Create(kvp.Key, pkg));
                    if (nonSynth)
                        SwitchOffSynthesis(tw);
                    tw.WriteLine("use " + kvp.Key + "." + pkg + ".all;");
                    if (nonSynth)
                        SwitchOnSynthesis(tw);
                }
                if (allNonSynth)
                    SwitchOnSynthesis(tw);
            }

            var pds = desc.Dependencies
                .Where(pd => pd != desc && !pd.IsEmpty)
                .GroupBy(pd => GetLibrary(pd));

            foreach (var grp in pds)
            {
                tw.WriteLine("library {0};", grp.Key);

                foreach (var pd in grp)
                {
                    tw.WriteLine("use {0}.{1}.all;", grp.Key, MakeIDName(pd.PackageName, pd));
                }
            }

            tw.WriteLine("library {0};", GetLibrary(desc));

            tw.WriteLine();
        }

        private void GenerateTypeDecls(DescriptorBase desc, IndentedTextWriter tw)
        {
            var types = desc.GetTypes().Where(t => (!t.IsConstrained || !t.IsComplete) && !t.CILType.IsPrimitive);
            var synthTypes = types.Where(t => !IsNotSynthesizable(t));
            var nonSynthTypes = types.Where(t => IsNotSynthesizable(t));

            foreach (var td in synthTypes)
            {
                tw.WriteLine("-- " + td.Name);
                GenerateTypeDecl(td, tw);
            }
            if (nonSynthTypes.Any())
            {
                SwitchOffSynthesis(tw);
                foreach (var td in nonSynthTypes)
                {
                    tw.WriteLine("-- " + td.Name);
                    GenerateTypeDecl(td, tw);
                }
                SwitchOnSynthesis(tw);
            }
        }

        public void GeneratePackage(IProject project, PackageDescriptor pd)
        {
            string name = MakeIDName(pd.PackageName, pd);
            string fname = MakeVHDSourceFileName(name);
            string path = project.AddFile(fname);
            project.AddFileAttribute(fname, pd);
            if (pd.Library != null)
                project.SetFileLibrary(fname, pd.Library);
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            IndentedTextWriter tw = new IndentedTextWriter(sw, "  ");
            ClearDependencies();
            tw.WriteLine("package " + name + " is");
            tw.Indent++;
            GenerateTypeDecls(pd, tw);
            foreach (MethodDescriptor md in pd.GetActiveMethods())
            {
                GenerateMethodDecl(md, tw);
            }
            foreach (FieldDescriptor fd in pd.GetConstants())
            {
                DeclareField(fd, tw);
            }
            tw.Indent--;
            tw.WriteLine("end;");
            tw.WriteLine();
            tw.WriteLine("package body " + name + " is");
            tw.Indent++;
            foreach (MethodDescriptor md in pd.GetActiveMethods())
            {
                GenerateMethodImpl(md, tw);
                tw.WriteLine();
            }
            tw.Indent--;
            tw.WriteLine("end package body;");
            tw.Flush();

            sw = new StreamWriter(path);
            tw = new IndentedTextWriter(sw, "  ");
            CreateFileHeader(new GeneratorInfo(name), tw);
            GenerateDependencies(pd, tw);
            tw.Flush();
            ms.Seek(0, SeekOrigin.Begin);
            ms.CopyTo(sw.BaseStream);
            ms.Close();
            tw.Close();
            sw.Close();
        }

        public void Initialize(IProject project, DesignContext context)
        {
            SubprogramsDontDriveSignals sdds = new SubprogramsDontDriveSignals();
            sdds.ApplyTo(context);

            foreach (VHDLib lib in _stdLibraries)
            {
                foreach (VHDPkg pkg in lib.Packages)
                {
                    if (pkg.FileContent == null)
                        continue;
                    string fname = MakeVHDSourceFileName(pkg.Name);
                    string path = project.AddFile(fname);
                    FileStream fs = new FileStream(path, FileMode.Create);
                    fs.Write(pkg.FileContent, 0, pkg.FileContent.Length);
                    fs.Close();
                }
            }
        }

        public Action<IGeneratorInformation, IndentedTextWriter> CreateFileHeader { get; set; }
    }
}

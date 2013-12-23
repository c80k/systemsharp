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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using SystemSharp.Common;
using SystemSharp.SysDOM;
using SystemSharp.Meta;
using SystemSharp.Algebraic;
using SystemSharp.Analysis;

namespace SystemSharp.SysDOM.Eval
{
    [Obsolete("This class is subject to re-design and has not been maintained for a long time.")]
    public class StatementInterpreter: IStatementVisitor
    {
        private static Dictionary<MethodBase, Function> _funMap = new Dictionary<MethodBase, Function>();

        public IEvaluator Evaluator { get; set; }
        private Dictionary<IStorable, object> _varValues = new Dictionary<IStorable,object>();
        private Statement _execLeaf;
        public object ReturnValue { get; private set; }

        public StatementInterpreter()
        {
            Evaluator = new DefaultEvaluator()
            {
                DoEvalVariable = EvaluateVariable,
                DoEvalFunction = EvalFunction
            };
            /*
            DecompilerTemplate = new MSILDecompilerTemplate()
            {
                GenerateThisVariable = true
            };
             * */
        }

        public void DeclareArgument(IStorable arg, object value)
        {
            _varValues[arg] = value;
        }

        public StatementInterpreter Clone()
        {
            return new StatementInterpreter()
            {
                Evaluator = this.Evaluator
                //,DecompilerTemplate = this.DecompilerTemplate
            };
        }

        public void Interprete(Function func, params object[] args)
        {
            int j = 0;
            if (func.ThisVariable != null)
            {
                DeclareArgument(func.ThisVariable, args[0]);
                j = 1;
            }
            for (int i = 0; i < func.InputVariables.Count; i++)
            {
                DeclareArgument(func.InputVariables[i], args[j]);
                j++;
            }
            func.Body.Accept(this);
            while (_execLeaf != null)
            {
                Statement next = _execLeaf.Successor;
                if (next != null)
                    next.Accept(this);
                else
                    break;
            }
        }

        public object Interprete(MethodBase method, object instance, params object[] args)
        {
            if (method.IsStatic && instance != null)
                throw new ArgumentException("Instance given but method to be interpreted is static");

            if (!method.IsStatic && instance == null)
                throw new ArgumentException("Instance is null but method to be interpreted is not static");

            object[] aargs = args;
            if (!method.IsStatic)
            {
                aargs = new object[args.Length+1];
                aargs[0] = instance;
                Array.Copy(args, 0, aargs, 1, args.Length);
            }

            return InterpreteInternal(method, aargs);
        }

        internal object InterpreteInternal(MethodBase method, object[] args)
        {
            if (!method.IsStatic)
            {
                object instance = args[0];
                Type clazz = instance.GetType();
                if (method.DeclaringType.IsInterface)
                {
                    InterfaceMapping imap = clazz.GetInterfaceMap(method.DeclaringType);
                    for (int i = 0; i < imap.InterfaceMethods.Length; i++)
                    {
                        if (imap.InterfaceMethods[i].Equals(method))
                        {
                            method = imap.TargetMethods[i];
                            break;
                        }
                    }
                }
                else
                {
                    Type[] argTypes = (from ParameterInfo pi in method.GetParameters()
                                       select pi.ParameterType).ToArray();
                    MethodInfo[] mis = clazz.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MethodInfo overwritten = clazz.GetMethod(method.Name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, argTypes, null);
                    if (overwritten != null)
                        method = overwritten;
                }
            }

            /*
            if (!_funMap.TryGetValue(method, out func))
            {
                MethodDescriptor md = new MethodDescriptor(method);
                MSILDecompiler decomp = new MSILDecompiler(md, null, args);
                decomp.Template.GenerateThisVariable = true;
                func = decomp.Decompile().Decompiled;
                _funMap[method] = func;
            }

            Interprete(func, args);
            return ReturnValue;
             * */

            throw new NotImplementedException();
        }

        private object EvalFunction(ICallable callee, object[] args)
        {
            if (callee is IntrinsicFunction)
            {
                IntrinsicFunction ifun = (IntrinsicFunction)callee;
                MethodBase mmodel = ifun.MethodModel;
                if (mmodel != null)
                {
                    if (mmodel.IsStatic)
                    {
                        return mmodel.ConvertArgumentsAndInvoke(null, args);
                    }
                    else
                    {
                        object[] eargs = new object[args.Length-1];
                        Array.Copy(args, 1, eargs, 0, eargs.Length);
                        return mmodel.ConvertArgumentsAndInvoke(args[0], eargs);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (callee is MSILFunctionRef)
            {
                MSILFunctionRef mfr = (MSILFunctionRef)callee;
                return InterpreteInternal(mfr.Method, args);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private object EvalFunction(FunctionCall funcref, object[] args)
        {
            return EvalFunction(funcref.Callee, args);
        }

        private object EvaluateVariable(Variable variable)
        {
            return _varValues[variable];
        }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            stmt.Statements.First().Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body.Accept(this);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            _execLeaf = stmt;
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            _execLeaf = stmt;
        }

        public void AcceptIf(IfStatement stmt)
        {
            for (int i = 0; i < stmt.Conditions.Count; i++)
            {
                Expression cond = stmt.Conditions[i];
                bool value = (bool)cond.Eval(Evaluator);
                if (value)
                {
                    stmt.Branches[i].Accept(this);
                    return;
                }
            }
            if (stmt.Branches.Count > stmt.Conditions.Count)
                stmt.Branches.Last().Accept(this);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            object sel = stmt.Selector.Eval(Evaluator);
            for (int i = 0; i < stmt.Cases.Count; i++)
            {
                if (sel.Equals(stmt.Cases[i]))
                {
                    stmt.Branches[i].Accept(this);
                    return;
                }
            }
            if (stmt.Branches.Count > stmt.Cases.Count)
                stmt.Branches.Last().Accept(this);
            return;
        }

        public void AcceptStore(StoreStatement stmt)
        {
            object value = stmt.Value.Eval(Evaluator);
            _varValues[stmt.Container] = value;
            _execLeaf = stmt;
        }

        public void AcceptNop(NopStatement stmt)
        {
            _execLeaf = stmt;
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            throw new NotImplementedException();
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            _execLeaf = stmt;
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            _execLeaf = stmt;
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            _execLeaf = stmt;
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            if (stmt.ReturnValue != null)
                ReturnValue = stmt.ReturnValue.Eval(Evaluator);
            _execLeaf = stmt;
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            _execLeaf = stmt;
        }

        public void AcceptCall(CallStatement stmt)
        {
            object[] evalArgs = new object[stmt.Arguments.Length];
            for (int i = 0; i < stmt.Arguments.Length; i++)
                evalArgs[i] = stmt.Arguments[i].Eval(Evaluator);

            EvalFunction(stmt.Callee, evalArgs);
        }
    }
}

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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Collections;
using SystemSharp.Synthesis.Util;

namespace SystemSharp.SysDOM.Transformations
{
    class FunctionInliner: DefaultTransformer
    {
        private Function _root;
        private Stack<Function> _funStack = new Stack<Function>();
        private Stack<ILIndexRef> _cilRefStack = new Stack<ILIndexRef>();
        private Stack<IStorableLiteral> _retStack = new Stack<IStorableLiteral>();
        private Stack<List<GotoStatement>> _getOutStack = new Stack<List<GotoStatement>>();
        private CacheDictionary<Tuple<Function, IStorableLiteral>, IStorableLiteral> _locals;
        private int _nextLocIndex;
        private ScopedIdentifierManager _sim;
        private List<Function> _inlinedFunctions = new List<Function>();

        private void PushCILRef(ILIndexRef cilRef)
        {
            if (cilRef == null)
                return;

            if (!_cilRefStack.Any())
            {
                _cilRefStack.Push(cilRef);
            }
            else
            {
                ILIndexRef top = new ILIndexRef(cilRef.Method, cilRef.ILIndex)
                {
                    Caller = _cilRefStack.Peek()
                };
                _cilRefStack.Push(top);
            }
        }

        private void PopCILRef(ILIndexRef cilRef)
        {
            if (cilRef == null)
                return;

            _cilRefStack.Pop();
        }

        protected override void CopyAttributesToLastStatement(Statement stmt)
        {
            base.CopyAttributesToLastStatement(stmt);
            var cilRef = stmt.QueryAttribute<ILIndexRef>();
            if (cilRef != null)
            {
                var top = _cilRefStack.Any() ? _cilRefStack.Peek() : null;
                cilRef = new ILIndexRef(cilRef.Method, cilRef.ILIndex)
                {
                    Caller = top
                };
                LastStatement.AddAttribute(cilRef);
            }
        }

        public FunctionInliner(Function root)
        {
            _root = root;
            _funStack.Push(root);
            _sim = new ScopedIdentifierManager(true);
            _locals = new CacheDictionary<Tuple<Function, IStorableLiteral>, IStorableLiteral>(CreateLocal);
        }

        private IStorableLiteral CreateLocal(Tuple<Function, IStorableLiteral> key)
        {
            var orgVar = key.Item2;
            string name = _sim.GetUniqueName(orgVar.Name, key);
            Variable var = new Variable(orgVar.Type)
            {
                Name = name,
                LocalIndex = _nextLocIndex++
            };
            DeclareLocal(var);
            return var;
        }

        protected override Statement Root
        {
            get { return _root.Body; }
        }

        protected override void DeclareAlgorithm()
        {
            foreach (Variable var in _root.InputVariables)
            {
                //DeclareInput(_locals[Tuple.Create(_root, var)]);
                var key = Tuple.Create(_root, var);
                _sim.GetUniqueName(var.Name, key);
                DeclareInput(var);
            }
            foreach (Variable var in _root.OutputVariables)
            {
                //DeclareOutput(_locals[Tuple.Create(_root, var)]);
                var key = Tuple.Create(_root, var);
                _sim.GetUniqueName(var.Name, key);
                DeclareOutput(var);
            }
            /*foreach (var local in _root.LocalVariables)
            {
                DeclareLocal(_locals[Tuple.Create(_root, local)]);
            }*/
            base.DeclareAlgorithm();
        }

        public override void AcceptCall(CallStatement stmt)
        {
            var cilRef = stmt.QueryAttribute<ILIndexRef>();
            PushCILRef(cilRef);
            IStorableLiteral retv;
            bool success = TryInline(stmt.Callee, stmt.Arguments, out retv);
            PopCILRef(cilRef);
            if (!success)
            {
                base.AcceptCall(stmt);
            }
        }

        public override void AcceptReturn(ReturnStatement stmt)
        {
            if (_funStack.Count > 1)
            {
                if (stmt.ReturnValue != null)
                {
                    Store(_retStack.Peek(), stmt.ReturnValue.Transform(this));
                    CopyAttributesToLastStatement(stmt);
                }
                var getOut = Goto();
                _getOutStack.Peek().Add(getOut);
            }
            else
            {
                base.AcceptReturn(stmt);
            }
        }

        public override void AcceptStore(StoreStatement stmt)
        {
            if (stmt.Container is Variable)
            {
                var key = Tuple.Create(_funStack.Peek(), stmt.Container);
                var var = _locals[key];
                Store(var, stmt.Value.Transform(this));
                CopyAttributesToLastStatement(stmt);
            }
            else
            {
                base.AcceptStore(stmt);
            }
        }

        private bool TryInline(ICallable callable, Expression[] args, out IStorableLiteral retv)
        {
            FunctionSpec fspec = callable as FunctionSpec;
            retv = null;
            if (fspec != null)
            {
                // Do not inline intrinsic functions
                if (fspec.IntrinsicRep != null)
                    return false;

                var md = fspec.SpecialSysDOMRep;
                if (md != null)
                {
                    var fun = md.Implementation;

                    InlineVerifier.CheckAllLocalsDeclared(fun);

                    // Recursion?
                    if (_funStack.Contains(fun))
                        return false;

                    if (fun.OutputVariables.Count == 0)
                    {
                        if (!fspec.ResultType.CILType.Equals(typeof(void)))
                        {
                            string name = _sim.GetUniqueName("$ret", new object());
                            IStorableLiteral rv = new Variable(fspec.ResultType)
                            {
                                Name = name
                            };
                            retv = _locals[Tuple.Create(fun, rv)];
                        }
                    }
                    else if (fun.OutputVariables.Count == 1)
                    {
                        retv = _locals[Tuple.Create(fun, fun.OutputVariables[0])];
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }

                    // account for possible "this" argument
                    int j = args.Length - fun.InputVariables.Count;

                    for (int i = j; i < args.Length; i++)
                    {
                        Expression rhs = args[i].Transform(this);
                        var lhs = _locals[Tuple.Create(fun, fun.InputVariables[i - j])];
                        Store(lhs, rhs);
                    }
                    if (retv != null)
                        _retStack.Push(retv);
                    _funStack.Push(fun);
                    _getOutStack.Push(new List<GotoStatement>());
                    fun.Body.Accept(this);
                    _funStack.Pop();
                    if (retv != null)
                        _retStack.Pop();
                    Nop();
                    foreach (var getOut in _getOutStack.Pop())
                    {
                        getOut.Target = LastStatement;
                    }

                    _inlinedFunctions.Add(fun);

                    return true;
                }
                else
                {
                    //System.Diagnostics.Debug.Assert(false, "function not decompiled - bug?");
                }
            }
            return false;
        }

        public override Expression TransformFunction(FunctionCall expr)
        {
            var cilRef = expr.QueryAttribute<ILIndexRef>();
            PushCILRef(cilRef);
            IStorableLiteral retv;
            bool success = TryInline(expr.Callee, expr.Children, out retv);
            PopCILRef(cilRef);
            if (success)
                return new LiteralReference((Literal)retv);
            else
                return base.TransformFunction(expr);
        }

        public override void VisitVariable(Variable variable)
        {
            var v = _locals[Tuple.Create(_funStack.Peek(), (IStorableLiteral)variable)];
            SetCurrentLiteral((Literal)v);
        }

        public IEnumerable<Function> InlinedFunctions
        {
            get { return _inlinedFunctions; }
        }
    }

    class InlineVerifier : DefaultTransformer
    {
        private Function _root;

        public InlineVerifier(Function root)
        {
            _root = root;
        }

        protected override Statement Root
        {
            get { return _root.Body; }
        }

        public override void VisitVariable(Variable variable)
        {
            Debug.Assert(_root.LocalVariables.Contains(variable) ||
                _root.InputVariables.Contains(variable) ||
                _root.OutputVariables.Contains(variable));
            base.VisitVariable(variable);
        }

        public static void CheckAllLocalsDeclared(Function fun)
        {
            var iv = new InlineVerifier(fun);
            iv.GetAlgorithm();
        }
    }

    public static class FunctionInlining
    {
        public static Function InlineCalls(this Function fun)
        {
            FunctionInliner fi = new FunctionInliner(fun);
            Function result = fi.GetAlgorithm();
            return result;
        }

        public static Function InlineCalls(this Function fun, out IEnumerable<Function> inlinedFunctions)
        {
            InlineVerifier.CheckAllLocalsDeclared(fun);
            FunctionInliner fi = new FunctionInliner(fun);
            Function result = fi.GetAlgorithm();
            result.Name = fun.Name;
            inlinedFunctions = fi.InlinedFunctions; 
            InlineVerifier.CheckAllLocalsDeclared(result);
            return result;
        }
    }
}

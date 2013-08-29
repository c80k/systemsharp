/**
 * Copyright 2012-2013 Christian Köllner
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Analysis.M2M;
using SystemSharp.Analysis.Msil;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Synthesis.Util;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.Analysis
{
    public static class AsyncStateMachines
    {
        public static Type GetStateMachineType(this MethodBase method)
        {
            return method.GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType;
        }

        /// <summary>
        /// If the given action represents an async method, redirect the action to the MoveNext method of the underlying IAsyncStateMachine
        /// </summary>
        /// <param name="action">any Action</param>
        /// <returns>the possibly redirected Action to MoveNext</returns>
        public static Action UnwrapEntryPoint(this Action action)
        {
            var asma = action.Method.GetCustomAttribute<AsyncStateMachineAttribute>();
            if (asma == null)
                return action;

            var asm = Activator.CreateInstance(asma.StateMachineType);

            return (Action)Action.CreateDelegate(typeof(Action), asm, "MoveNext");
        }

        public static MethodBase UnwrapEntryPoint(this MethodBase method)
        {
            var asma = method.GetCustomAttribute<AsyncStateMachineAttribute>();
            if (asma == null)
                return method;

            return asma.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public static bool IsAsync(this MethodBase method)
        {
            return method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;
        }

        public static bool IsAsync(this Action action)
        {
            return action.Method.IsAsync();
        }

        public static bool IsMoveNext(this MethodBase method)
        {
            return method.Name == "MoveNext" && !method.DeclaringType.IsVisible;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class TickAttribute : Attribute, IDoNotAnalyze
    {
    }

    class RewriteGetResult :
        RewriteCall,
        IDoNotCallOnDecompilation
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            Type returnType;
            if (callee.ReturnsSomething(out returnType))
            {
                dynamic awaiter = args[0].Sample;
                if ((object)awaiter != null)
                {
                    if (!awaiter.IsCompleted)
                        throw new InvalidOperationException("Task not completed - what are you awaiting for?");

                    object resultSample = awaiter.GetResult();
                    var resultType = resultSample.GetType();
                    var fspec = new FunctionSpec(resultType)
                    {
                        IntrinsicRep = IntrinsicFunctions.GetAsyncResult(awaiter)
                    };
                    var fcall = new FunctionCall()
                    {
                        Callee = fspec,
                        Arguments = new Expression[0],
                        ResultType = resultType
                    };
                    stack.Push(fcall, resultSample);
                }
            }
            return true;
        }
    }

    class RewriteIsCompleted :
        RewriteCall,
        IDoNotCallOnDecompilation
    {
        public RewriteIsCompleted()
        {
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            var ctx = stack.QueryAttribute<AsyncMethodDecompiler>();
            if (ctx == null)
                throw new InvalidOperationException("Method must be decompiled using AsyncMethodDecompiler.");

            var style = ctx.ImplStyle;

            if (style == EAsyncImplStyle.Sequential)
            {
                ctx.ImplementAwait(decompilee, callee, args, stack, builder);
            }

            bool flag = style == EAsyncImplStyle.Sequential;
            var lr = LiteralReference.CreateConstant(flag);
            stack.Push(new StackElement(lr, flag, Msil.EVariability.Constant));
            return true;
        }
    }

    public enum EAsyncImplStyle
    {
        Sequential,
        FSM
    }

    public class AsyncMethodDecompiler
    {
        internal void ImplementAwait(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            var awaiterCallExpr = stack.ResolveVariableReference(stack.CurrentILIndex, args[0].Expr);
            var awaiterCall = awaiterCallExpr as FunctionCall;
            if (awaiterCall == null)
                throw new InvalidOperationException("Unable to resolve awaited object.");
            var waitObject = awaiterCall.Arguments[0];
            var awaiterType = args[0].Expr.ResultType.CILType;
            var rw = awaiterType.GetCustomOrInjectedAttribute<RewriteAwait>();
            if (rw == null)
            {
                var fcall = waitObject as FunctionCall;
                if (fcall != null)
                {
                    var fspec = fcall.Callee as FunctionSpec;
                    if (fspec != null && fspec.CILRep != null)
                        rw = fspec.CILRep.GetCustomOrInjectedAttribute<RewriteAwait>();
                }
            }
            if (rw != null)
                rw.Rewrite(decompilee, waitObject, stack, builder);
            else
                throw new InvalidOperationException("Unable to find await implementor");
        }

        private StateInfo ForkSI(int state)
        {
            var si = _curSI.Fork(state);
            if (_stateInfos.ContainsKey(si))
            {
                si = _stateInfos[si];
            }
            else
            {
                _stateQ.Enqueue(si);
                _stateInfos[si] = si;
            }
            return si;
        }

        private StateInfo ForkNextSI()
        {
            return ForkSI(_nextState == -1 ? 0 : _nextState);
        }

        private StateInfo ForkInitialSI()
        {
            return ForkSI(0);
        }

        private void ImplementJoin(JoinParams jp, IAlgorithmBuilder builder, StateInfo sin)
        {
            Contract.Requires<ArgumentNullException>(jp != null);
            Contract.Requires<ArgumentNullException>(builder != null);
            Contract.Requires<ArgumentNullException>(sin != null);

            var jspec = new FunctionSpec(typeof(bool))
            {
                IntrinsicRep = IntrinsicFunctions.Join(jp)
            };
            var jcall = new FunctionCall()
            {
                Callee = jspec,
                Arguments = new Expression[0],
                ResultType = typeof(bool)
            };

            builder.If(jcall);
            var pi1 = new ProceedWithStateInfo()
            {
                TargetState = sin,
                TargetWaitState = false,
                LambdaTransition = true
            };
            var pspec1 = new FunctionSpec(typeof(void))
            {
                IntrinsicRep = IntrinsicFunctions.ProceedWithState(pi1)
            };
            builder.Call(pspec1, LiteralReference.CreateConstant(pi1));
            builder.Else();
            var sin2 = sin.Fork(sin.ILState);
            var pi2 = new ProceedWithStateInfo()
            {
                TargetState = sin,
                TargetWaitState = true,
                LambdaTransition = false
            };
            var pspec2 = new FunctionSpec(typeof(void))
            {
                IntrinsicRep = IntrinsicFunctions.ProceedWithState(pi2)
            };
            builder.Call(pspec2, LiteralReference.CreateConstant(pi2));
            builder.EndIf();

            if (_curCoFSM != null)
                _curCoFSM.Dependencies.Add(jp.JoinedTask);
        }

        private Task GetTaskFromAwaiter(object awaiter)
        {
            var awaiterType = awaiter.GetType();
            var taskField = awaiterType.GetField("m_task", BindingFlags.Instance | BindingFlags.NonPublic);
            if (taskField == null)
                return null;
            var task = taskField.GetValue(awaiter) as Task;
            return task;
        }

        class AwaitOnCompletedRewriter : 
            RewriteCall,
            IDoNotCallOnDecompilation
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var ctx = stack.QueryAttribute<AsyncMethodDecompiler>();
                if (ctx == null)
                    throw new InvalidOperationException("Method must be decompiled using AsyncMethodDecompiler.");
                if (ctx.ImplStyle == EAsyncImplStyle.Sequential)
                    return true;

                var awaiterCallExpr = stack.ResolveVariableReference(stack.CurrentILIndex, args[1].Expr);
                var awaiterCall = awaiterCallExpr as FunctionCall;
                if (awaiterCall != null)
                {
                    var waitObject = awaiterCall.Arguments[0];
                    var asyncCall = waitObject as FunctionCall;
                    if (asyncCall != null)
                    {
                        var cspec = asyncCall.Callee as FunctionSpec;
                        if (cspec != null && 
                            cspec.CILRep != null &&
                            cspec.CILRep.HasCustomOrInjectedAttribute<TickAttribute>())
                        {
                            var si = ctx.ForkNextSI();
                            var pi = new ProceedWithStateInfo()
                            {
                                TargetState = si,
                                TargetWaitState = false,
                                LambdaTransition = false
                            };
                            var fspec = new FunctionSpec(typeof(void))
                            {
                                IntrinsicRep = IntrinsicFunctions.ProceedWithState(pi)
                            };
                            builder.Call(fspec, LiteralReference.CreateConstant(pi));
                            return true;
                        }
                    }
                }

                var awaiter = args[1].Sample;
                var task = ctx.GetTaskFromAwaiter(awaiter);

                if (task != null)
                {
                    if (!task.IsCompleted)
                        throw new InvalidOperationException("Task not completed - what are you awaiting for?");

                    var sin = ctx.ForkNextSI();
                    sin.HasWaitState = true;
                    var jp = new JoinParams()
                    {
                        JoinedTask = task,
                        Continuation = sin
                    };
                    sin.JP = jp;
                    ctx.ImplementJoin(jp, builder, sin);
                }

                return true;
            }
        }

        class TaskAwaiterAwaitOnCompletedRewriter :
            RewriteCall,
            IDoNotCallOnDecompilation
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var ctx = stack.QueryAttribute<AsyncMethodDecompiler>();
                if (ctx == null)
                    throw new InvalidOperationException("Method must be decompiled using AsyncMethodDecompiler.");
                if (ctx.ImplStyle != EAsyncImplStyle.FSM)
                    throw new InvalidOperationException("Awaiting other tasks is only possible when decompiling to FSM.");

                ctx.ImplementAwait(decompilee, callee, args, stack, builder);

                return true;
            }
        }

        class SetResultRewriter :
            RewriteCall,
            IDoNotCallOnDecompilation
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var ctx = stack.QueryAttribute<AsyncMethodDecompiler>();
                if (ctx.ImplStyle == EAsyncImplStyle.FSM)
                {
                    if (args.Length == 2 &&
                        ctx._curCoFSM.ResultVar != null)
                    {
                        builder.Store(ctx._curCoFSM.ResultVar, args[1].Expr);
                        if (args[1].Expr.ResultType.IsComplete)
                            ctx._curCoFSM.ResultVar.UpgradeType(args[1].Expr.ResultType);
                    }
                    
                    var si = ctx.ForkInitialSI();
                    var pi = new ProceedWithStateInfo()
                    {
                        TargetState = si,
                        TargetWaitState = false,
                        LambdaTransition = false
                    };

                    if (ctx._curCoFSM != null &&
                        ctx._curCoFSM.DoneVar != null)
                    {
                        var tr = LiteralReference.CreateConstant(true);
                        builder.Store(ctx._curCoFSM.DoneVar, tr);
                        pi.TargetState = null;
                    }

                    var fspec = new FunctionSpec(typeof(void))
                    {
                        IntrinsicRep = IntrinsicFunctions.ProceedWithState(pi)
                    };
                    builder.Call(fspec, LiteralReference.CreateConstant(pi));
                }
                return true;
            }
        }

        class SetExceptionRewriter :
            RewriteCall,
            IDoNotCallOnDecompilation
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                return true;
            }
        }

        class StateAccessRewriter :
            RewriteFieldAccess
        {
            public override void RewriteRead(CodeDescriptor decompilee, FieldInfo field, object instance, IDecompiler stack, IFunctionBuilder builder)
            {
                throw new InvalidOperationException("State machine is not expected to read state inside state handler");
            }

            public override void RewriteWrite(CodeDescriptor decompilee, FieldInfo field, object instance, StackElement value, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                me._nextState = (int)value.Sample;
            }
        }

        class AwaiterAccessRewriter :
            RewriteFieldAccess
        {
            public override void RewriteRead(CodeDescriptor decompilee, FieldInfo field, object instance, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                var awaiter = field.GetValue(me._fsmInstance);
                var lr = LiteralReference.CreateConstant(awaiter);
                stack.Push(new StackElement(lr, awaiter, Msil.EVariability.Constant));
            }

            public override void RewriteWrite(CodeDescriptor decompilee, FieldInfo field, object instance, StackElement value, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                try
                {
                    field.SetValue(me._fsmInstance, value.Sample);
                }
                catch (Exception)
                {
                    var task = me.GetTaskFromAwaiter(value.Sample);
                    var awaiterType = field.FieldType;
                    var targetAwaiter = Activator.CreateInstance(awaiterType);
                    var targetAwaiterField = awaiterType.GetField("m_task", BindingFlags.Instance | BindingFlags.NonPublic);
                    targetAwaiterField.SetValue(targetAwaiter, task);
                    field.SetValue(me._fsmInstance, targetAwaiter);
                }
            }
        }

        class LocalFieldAccessRewriter :
            RewriteFieldAccess
        {
            public override void RewriteRead(CodeDescriptor decompilee, FieldInfo field, object instance, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                var v = me._locFields[field.Name];
                if (!me._declared.Contains(v))
                {
                    builder.DeclareLocal(v);
                    me._declared.Add(v);
                }
                var lr = (LiteralReference)v;
                stack.Push(lr, v.Type.GetSampleInstance(ETypeCreationOptions.ReturnNullIfUnavailable));
            }

            public override void RewriteWrite(CodeDescriptor decompilee, FieldInfo field, object instance, StackElement value, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                var v = me._locFields[field.Name];
                if (!me._declared.Contains(v))
                {
                    builder.DeclareLocal(v);
                    me._declared.Add(v);
                }
                builder.Store(v, value.Expr);
                if (value.Sample != null)
                    v.UpgradeType(TypeDescriptor.GetTypeOf(value.Sample));
                if (me._curSI != null)
                    me._curSI.LVState[field.Name] = value.Sample;
            }
        }

        class ArgFieldAccessRewriter :
            RewriteFieldAccess
        {
            public override void RewriteRead(CodeDescriptor decompilee, FieldInfo field, object instance, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                var v = me._argFields[field.Name];
                if (!me._declared.Contains(v))
                {
                    builder.DeclareLocal(v);
                    me._declared.Add(v);
                }
                var lr = (LiteralReference)v;
                stack.Push(lr, v.Type.GetSampleInstance(ETypeCreationOptions.ForceCreation));
            }

            public override void RewriteWrite(CodeDescriptor decompilee, FieldInfo field, object instance, StackElement value, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                var v = me._argFields[field.Name];
                if (!me._declared.Contains(v))
                {
                    builder.DeclareLocal(v);
                    me._declared.Add(v);
                }
                builder.Store(v, value.Expr);
                if (value.Sample != null)
                    v.UpgradeType(TypeDescriptor.GetTypeOf(value.Sample));
                if (me._curSI != null)
                    me._curSI.LVState[field.Name] = value.Sample;
            }
        }

        class TaskAccessRewriter :
            RewriteFieldAccess
        {
            public override void RewriteRead(CodeDescriptor decompilee, FieldInfo field, object instance, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                var elem = (StackElement)me._tasks[field.Name];
                stack.Push(elem);
            }

            public override void RewriteWrite(CodeDescriptor decompilee, FieldInfo field, object instance, StackElement value, IDecompiler stack, IFunctionBuilder builder)
            {
                var me = stack.QueryAttribute<AsyncMethodDecompiler>();
                me._tasks[field.Name] = value;
            }
        }

        class Result : IDecompilationResult
        {
            private Function _decompiled;
            private ICollection<MethodCallInfo> _calledMethods;
            private ICollection<FieldRefInfo> _referencedFields;

            public Result(
                Function decompiled,
                ICollection<MethodCallInfo> calledMethods,
                ICollection<FieldRefInfo> referencedFields)
            {
                _decompiled = decompiled;
                _calledMethods = calledMethods;
                _referencedFields = referencedFields;
            }

            public Function Decompiled
            {
                get { return _decompiled; }
            }

            public ICollection<MethodCallInfo> CalledMethods
            {
                get { return _calledMethods; }
            }

            public ICollection<FieldRefInfo> ReferencedFields
            {
                get { return _referencedFields; }
            }
        }

        class StateAssigner : DefaultTransformer
        {
            private AsyncMethodDecompiler _me;
            private Statement _body;
            private bool _singleState;

            public StateAssigner(AsyncMethodDecompiler me, Statement body, bool singleState = false)
            {
                _me = me;
                _body = body;
                _singleState = singleState;
            }

            protected override Statement Root
            {
                get { return _body; }
            }

            public override void AcceptCall(CallStatement stmt)
            {
                var fspec = stmt.Callee as FunctionSpec;
                if (fspec != null &&
                    fspec.IntrinsicRep != null &&
                    fspec.IntrinsicRep.Action == IntrinsicFunction.EAction.ProceedWithState)
                {
                    var pi = (ProceedWithStateInfo)fspec.IntrinsicRep.Parameter;
                    var si = _me._stateInfos[pi.TargetState];
                    if (pi.LambdaTransition)
                    {
                        si.Result.Decompiled.Body.Accept(this);
                    }
                    else if (!_singleState)
                    {
                        var lhs = SignalRef.Create(_me._nextStateSignal, SignalRef.EReferencedProperty.Next);
                        var rhs = LiteralReference.CreateConstant(pi.TargetWaitState ? si.WaitStateValue : si.StateValue);
                        Store(lhs, rhs);
                    }
                }
                else
                {
                    base.AcceptCall(stmt);
                }
            }
        }

        class CoStateAssigner : DefaultTransformer
        {
            private AsyncMethodDecompiler _me;
            private IList<Variable> _activeVars;
            private Statement _body;
            private int _ownState;

            public CoStateAssigner(AsyncMethodDecompiler me, IList<Variable> activeVars, Statement body, int ownState)
            {
                _me = me;
                _activeVars = activeVars;
                _body = body;
                _ownState = ownState;
            }

            protected override Statement Root
            {
                get { return _body; }
            }

            public override void AcceptCall(CallStatement stmt)
            {
                var fspec = stmt.Callee as FunctionSpec;
                if (fspec != null &&
                    fspec.IntrinsicRep != null &&
                    fspec.IntrinsicRep.Action == IntrinsicFunction.EAction.ProceedWithState)
                {
                    var pi = (ProceedWithStateInfo)fspec.IntrinsicRep.Parameter;
                    if (pi.TargetState == null)
                    {
                        // final state of co fsm
                        var fa = LiteralReference.CreateConstant(false);
                        Store(_activeVars[_ownState], fa);
                    }
                    else
                    {
                        var si = _me._stateInfos[pi.TargetState];
                        if (pi.LambdaTransition)
                        {
                            si.Result.Decompiled.Body.Accept(this);
                        }
                        else
                        {
                            int index = (int)(pi.TargetWaitState ? si.WaitStateValue : si.StateValue);
                            var tr = LiteralReference.CreateConstant(true);
                            var fa = LiteralReference.CreateConstant(false);
                            Store(_activeVars[index], tr);
                            Store(_activeVars[_ownState], fa);
                        }
                    }
                }
                else
                {
                    base.AcceptCall(stmt);
                }
            }
        }

        class StateWeaver : DefaultTransformer
        {
            private AsyncMethodDecompiler _me;
            private Statement _body;

            public StateWeaver(AsyncMethodDecompiler me, Statement body)
            {
                _me = me;
                _body = body;
            }

            protected override Statement Root
            {
                get { return _body; }
            }

            public override void AcceptCall(CallStatement stmt)
            {
                var fspec = stmt.Callee as FunctionSpec;
                if (fspec != null &&
                    fspec.IntrinsicRep != null &&
                    fspec.IntrinsicRep.Action == IntrinsicFunction.EAction.Fork)
                {
                    var task = (Task)fspec.IntrinsicRep.Parameter;
                    var cofsm = _me._coFSMs.Map[task];
                    var call = stmt.Arguments[0] as FunctionCall;
                    var args = call.Arguments;
                    for (int i = 1; i < args.Length; i++)
                    {
                        var argType = args[i].ResultType.CILType;
                        if (argType.IsGenericType &&
                            argType.GetGenericTypeDefinition().Equals(typeof(Task<>)))
                        {
                            // nop
                        }
                        else
                        {
                            Store(cofsm.Arguments[i - 1], args[i]);
                        }
                    }
                    cofsm.InitialHandler.Accept(this);
                }
                else
                {
                    base.AcceptCall(stmt);
                }
            }

            private CoFSM _matchedCoFSM;

            private bool MatchJoin(Expression e)
            {
                var fcall = e as FunctionCall;
                if (fcall == null)
                    return false;
                var fspec = fcall.Callee as FunctionSpec;
                if (fspec != null &&
                    fspec.IntrinsicRep != null &&
                    fspec.IntrinsicRep.Action == IntrinsicFunction.EAction.Join)
                {
                    var jp = (JoinParams)fspec.IntrinsicRep.Parameter;
                    var task = jp.JoinedTask;
                    _matchedCoFSM = _me._coFSMs.Map[task];
                    return true;
                }
                return false;
            }

            private Expression GenerateJoin()
            {
                return (LiteralReference)_matchedCoFSM.DoneVar;
            }

            private bool MatchGetAsyncResult(Expression e)
            {
                var fcall = e as FunctionCall;
                if (fcall == null)
                    return false;
                var fspec = fcall.Callee as FunctionSpec;
                if (fspec != null &&
                    fspec.IntrinsicRep != null &&
                    fspec.IntrinsicRep.Action == IntrinsicFunction.EAction.GetAsyncResult)
                {
                    var task = _me.GetTaskFromAwaiter(fspec.IntrinsicRep.Parameter);
                    _matchedCoFSM = _me._coFSMs.Map[task];
                    return true;
                }
                return false;
            }

            private Expression GenerateGetAsyncResult()
            {
                return (LiteralReference)_matchedCoFSM.ResultVar;
            }

            public override Function GetAlgorithm()
            {
                var r = base.GetAlgorithm();
                r.Body.ReplaceExpressions(MatchJoin, GenerateJoin);
                r.Body.ReplaceExpressions(MatchGetAsyncResult, GenerateGetAsyncResult);
                return r;
            }
        }

        private DesignContext _context;
        private CodeDescriptor _code;
        private object _instance;
        private object _fsmInstance;
        private object[] _arguments;
        private Dictionary<string, Variable> _argFields;
        private MethodCode _cfg;
        private MSILCodeBlock[] _stateTargets;
        private MethodCode[] _stateCFGs;
        private Dictionary<string, Variable> _locFields;
        private Dictionary<string, object> _tasks;
        private HashSet<Variable> _declared;
        private int _nextState;
        private Array _stateValues;
        private SignalDescriptor _nextStateSignal;
        private MSILDecompilerTemplate _curTempl;
        private StateInfo _curSI;
        private Dictionary<StateInfo, StateInfo> _stateInfos;
        private Queue<StateInfo> _stateQ;
        private CoFSM _curCoFSM;
        private CoFSMs _coFSMs;

        public ICollection<MethodCallInfo> CalledMethods { get; private set; }

        public EAsyncImplStyle ImplStyle { get; private set; }

        static AsyncMethodDecompiler()
        {
            var type = typeof(AsyncVoidMethodBuilder);
            var aoc1 = type.GetMethod("AwaitOnCompleted");
            var aoc2 = type.GetMethod("AwaitUnsafeOnCompleted");
            var rwc = new AwaitOnCompletedRewriter();
            AttributeInjector.Inject(aoc1, rwc);
            AttributeInjector.Inject(aoc2, rwc);
            var sr = type.GetMethod("SetResult");
            var rwsr = new SetResultRewriter();
            AttributeInjector.Inject(sr, rwsr);
            var se = type.GetMethod("SetException");
            var serw = new SetExceptionRewriter();
            AttributeInjector.Inject(se, serw);

            type = typeof(AsyncTaskMethodBuilder);
            aoc1 = type.GetMethod("AwaitOnCompleted");
            aoc2 = type.GetMethod("AwaitUnsafeOnCompleted");
            AttributeInjector.Inject(aoc1, rwc);
            AttributeInjector.Inject(aoc2, rwc);
            sr = type.GetMethod("SetResult");
            AttributeInjector.Inject(sr, rwsr);
            se = type.GetMethod("SetException");
            AttributeInjector.Inject(se, serw);

            var tarwc = new TaskAwaiterAwaitOnCompletedRewriter();
            type = typeof(TaskAwaiter);
            AttributeInjector.Inject(type.GetMethod("get_IsCompleted"), new RewriteIsCompleted(), true);
            AttributeInjector.Inject(type.GetMethod("OnCompleted"), tarwc, true);
            AttributeInjector.Inject(type.GetMethod("UnsafeOnCompleted"), tarwc, true);
            AttributeInjector.Inject(type.GetMethod("GetResult"), new RewriteGetResult(), true);
            type = typeof(TaskAwaiter<>);
            AttributeInjector.Inject(type.GetMethod("get_IsCompleted"), new RewriteIsCompleted(), true);
            AttributeInjector.Inject(type.GetMethod("OnCompleted"), tarwc, true);
            AttributeInjector.Inject(type.GetMethod("UnsafeOnCompleted"), tarwc, true);
            AttributeInjector.Inject(type.GetMethod("GetResult"), new RewriteGetResult(), true);

            AttributeInjector.Inject(typeof(TaskAwaiter),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType));
            AttributeInjector.Inject(typeof(TaskAwaiter<>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType));
            AttributeInjector.Inject(typeof(Task),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType));
            AttributeInjector.Inject(typeof(Task<>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType));
        }

        public AsyncMethodDecompiler(DesignContext ctx, CodeDescriptor code, object instance, object[] arguments)
        {
            _context = ctx;
            _code = code;
            _instance = instance;
            _arguments = arguments;
            ImplStyle = code.AsyncMethod.HasCustomOrInjectedAttribute<TransformIntoFSM>() ? EAsyncImplStyle.FSM : EAsyncImplStyle.Sequential;
        }

        private void InitializeCFGs()
        {
            _cfg = MethodCode.Create(_code.Method);
            _stateTargets = StateTargetFinder.FindStateTargets(_cfg);
            var cii = new AsyncMethodCustomInstructionInfo(
                _cfg, ImplStyle == EAsyncImplStyle.FSM ?
                    AsyncMethodCustomInstructionInfo.EAssumption.NeverCompleted :
                    AsyncMethodCustomInstructionInfo.EAssumption.AlwaysCompleted);
            _stateCFGs = _stateTargets.Select(t => new MethodCode(cii, t.StartIndex)).ToArray();
        }

        private void InjectAttributes(string prefix)
        {
            var fsmType = _code.AsyncMethod.GetStateMachineType();
            _fsmInstance = Activator.CreateInstance(fsmType);
            AttributeInjector.Inject(fsmType, new HideDeclaration());

            var thisField = fsmType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.Name.Contains("this") && f.FieldType.Equals(_code.AsyncMethod.DeclaringType))
                .FirstOrDefault();
            if (thisField != null)
            {
                AttributeInjector.Inject(thisField, new StaticEvaluation());
                thisField.SetValue(_fsmInstance, _instance);
            }

            var stateField = fsmType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.Name.Contains("state") && f.FieldType.Equals(typeof(int)))
                .FirstOrDefault();
            if (stateField != null)
            {
                AttributeInjector.Inject(stateField, new StateAccessRewriter());
            }

            var awaiterFields = fsmType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.Name.Contains("$awaiter"));

            foreach (var awaiterField in awaiterFields)
            {
                AttributeInjector.Inject(awaiterField, new AwaiterAccessRewriter());
            }

            _locFields = new Dictionary<string, Variable>();
            _declared = new HashSet<Variable>();
            _tasks = new Dictionary<string, object>();
            var fields = fsmType.GetFields();
            var lfar = new LocalFieldAccessRewriter();
            var afar = new ArgFieldAccessRewriter();
            var tar = new TaskAccessRewriter();
            var argNames = _code.AsyncMethod
                .GetParameters()
                .Select(p => p.Name)
                .ToArray();
            foreach (var field in fields)
            {
                var name = field.Name;

                bool isTask = false;

                if (field.FieldType.IsGenericType &&
                    field.FieldType.GetGenericTypeDefinition().Equals(typeof(Task<>)))
                {
                    // it is a task variable
                    AttributeInjector.Inject(field, tar);
                    AttributeInjector.Inject(field.FieldType,
                        new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType));
                    AttributeInjector.Inject(field.FieldType, new HideDeclaration());
                    isTask = true;
                }

                if (field.FieldType.IsGenericType &&
                    field.FieldType.GetGenericTypeDefinition().Equals(typeof(AsyncTaskMethodBuilder<>)))
                {
                    var builderType = field.FieldType;
                    var aoc1 = builderType.GetMethod("AwaitOnCompleted");
                    var aoc2 = builderType.GetMethod("AwaitUnsafeOnCompleted");
                    var rwc = new AwaitOnCompletedRewriter();
                    AttributeInjector.Inject(aoc1, rwc);
                    AttributeInjector.Inject(aoc2, rwc);
                    var sr = builderType.GetMethod("SetResult");
                    var rwsr = new SetResultRewriter();
                    AttributeInjector.Inject(sr, rwsr);
                    var se = builderType.GetMethod("SetException");
                    var serw = new SetExceptionRewriter();
                    AttributeInjector.Inject(se, serw);
                }

                int pindex = Array.IndexOf(argNames, name);
                if (pindex >= 0)
                {
                    field.SetValue(_fsmInstance, _arguments[pindex]);
                    if (isTask)
                    {
                        var task = _arguments[pindex];
                        var lr = LiteralReference.CreateConstant(task);
                        _tasks[field.Name] = new StackElement(lr, task, EVariability.Constant);
                    }
                    else
                    {
                        AttributeInjector.Inject(field, afar);
                    }
                    continue;
                }

                if (isTask)
                    continue;

                int beg = name.IndexOf('<');
                int end = name.LastIndexOf('>');
                if (beg == -1 || end == -1 || (end - beg) <= 1)
                    continue;

                var locName = prefix + name.Substring(beg + 1, end - beg - 1);

                var v = new Variable(field.FieldType)
                {
                    Name = locName
                };
                _locFields[field.Name] = v;
                AttributeInjector.Inject(field, lfar);
            }

            foreach (var local in _code.Method.GetMethodBody().LocalVariables)
            {
                var locType = local.LocalType;
                if (locType.IsGenericType &&
                    locType.GetGenericTypeDefinition().Equals(typeof(TaskAwaiter<>)))
                {
                    AttributeInjector.Inject(locType, new HideDeclaration());
                }
            }
        }

        public IDecompilationResult DecompileSequential()
        {
            _cfg = MethodCode.Create(_code.Method);
            var targets = StateTargetFinder.FindStateTargets(_cfg);
            var entry = targets[0];
            var entryCFG = MethodCode.Create(_code.Method, entry.StartIndex);
            var decomp = new MSILDecompiler(_code, entryCFG, _fsmInstance);
            decomp.Template.AddAttribute(this);
            decomp.Template.DisallowReturnStatements = true;
            var result = decomp.Decompile();
            return result;
        }

        private MethodDescriptor ConstructMethodDescriptor(MethodCallInfo mci, bool special)
        {
            MethodBase method = mci.GetStrongestOverride();
            MethodDescriptor md = new MethodDescriptor(
                method,
                mci.EvaluatedArgumentsWithoutThis,
                special ? mci.ArgumentVariabilities : VariabilityPattern.CreateDefault(method).Pattern);
            var caller = mci.CallerTemplate.Decompilee;
            var callerPd = caller as ProcessDescriptor;
            var callerMd = caller as MethodDescriptor;
            if (callerPd != null)
                md.CallingProcess = callerPd;
            else
                md.CallingProcess = callerMd.CallingProcess;

            return md;
        }

        private CoFSMs DecompileCoFSMs(IEnumerable<MethodCallInfo> calledMethods)
        {
            var result = default(CoFSMs);
            result.Map = new Dictionary<Task, CoFSM>();

            var q = new Queue<MethodCallInfo>();
            foreach (var mci in calledMethods)
            {
                if (mci.Method.IsAsync())
                    q.Enqueue(mci);
            }
            var scim = new ScopedIdentifierManager();
            while (q.Any())
            {
                var mci = q.Dequeue();
                var md = ConstructMethodDescriptor(mci, true);
                var task = (Task)mci.ResultSample;
                var dec = new AsyncMethodDecompiler(_context, md, mci.Instance, md.ArgValueSamples);
                string prefix = scim.GetUniqueName(md.Name, task);
                prefix += "$_";
                var r = dec.DecompileToCoFSM(prefix);
                r.CoTask = task;
                result.Map[task] = r;
                foreach (var callee in r.CalledMethods)
                {
                    var calleeTask = (Task)mci.ResultSample;
                    if (callee.Method.IsAsync() && !result.Map.ContainsKey(calleeTask))
                        q.Enqueue(callee);
                }
            }

            result.CreateOrder();

            return result;
        }

        private CoFSM DecompileToCoFSM(string prefix)
        {
            ImplStyle = EAsyncImplStyle.FSM;
            InjectAttributes(prefix);
            InitializeCFGs();

            CoFSM cofsm = new CoFSM();

            var args = _code.AsyncMethod.GetParameters();
            cofsm.Arguments = new Variable[args.Length];
            _argFields = new Dictionary<string, Variable>();
            for (int i = 0; i < args.Length; i++)
            {
                string name = prefix + "_" + args[i].Name;
                var argv = new Variable(TypeDescriptor.GetTypeOf(_arguments[i]))
                {
                    Name = name
                };
                _argFields[args[i].Name] = argv;
                cofsm.Arguments[i] = argv;
            }

            int numILStates = _stateCFGs.Length;
            var mym = _code as MethodDescriptor;
            cofsm.Method = mym;
            cofsm.Dependencies = new HashSet<Task>();

            // Create result variable and done flag
            cofsm.DoneVar = new Variable(typeof(bool)) {
                Name = prefix + "_$done"
            };
            if (_code.AsyncMethod.ReturnType.IsGenericType &&
                _code.AsyncMethod.ReturnType.GetGenericTypeDefinition().Equals(typeof(Task<>)))
            {
                var resultType = _code.AsyncMethod.ReturnType.GetGenericArguments()[0];
                if (!resultType.Equals(typeof(void)))
                {
                    cofsm.ResultVar = new Variable(resultType)
                    {
                        Name = prefix + "_$result"
                    };

                    var builderType = typeof(AsyncTaskMethodBuilder<>).MakeGenericType(resultType);
                    AttributeInjector.Inject(builderType,
                        new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType));
                    var aoc1 = builderType.GetMethod("AwaitOnCompleted");
                    var aoc2 = builderType.GetMethod("AwaitUnsafeOnCompleted");
                    var rwc = new AwaitOnCompletedRewriter();
                    AttributeInjector.Inject(aoc1, rwc);
                    AttributeInjector.Inject(aoc2, rwc);
                    var sr = builderType.GetMethod("SetResult");
                    var rwsr = new SetResultRewriter();
                    AttributeInjector.Inject(sr, rwsr);
                    var se = builderType.GetMethod("SetException");
                    var serw = new SetExceptionRewriter();
                    AttributeInjector.Inject(se, serw);
                    AttributeInjector.Inject(builderType, new HideDeclaration());

                    var awaiterType = typeof(TaskAwaiter<>).MakeGenericType(resultType);
                    AttributeInjector.Inject(awaiterType,
                        new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType));
                    AttributeInjector.Inject(awaiterType.GetMethod("get_IsCompleted"), new RewriteIsCompleted());
                    AttributeInjector.Inject(awaiterType.GetMethod("OnCompleted"), new AwaitOnCompletedRewriter());
                    AttributeInjector.Inject(awaiterType.GetMethod("UnsafeOnCompleted"), new AwaitOnCompletedRewriter());
                    AttributeInjector.Inject(awaiterType.GetMethod("GetResult"), new RewriteGetResult());
                    AttributeInjector.Inject(awaiterType, new HideDeclaration());
                }
            }

            // Decompile state handlers
            _stateInfos = new Dictionary<StateInfo, StateInfo>();
            var decomp = new MSILDecompiler(_code, _stateCFGs[0], _fsmInstance);
            _curTempl = decomp.Template;
            _curTempl.AddAttribute(this);
            _curTempl.DisallowReturnStatements = true;
            _curTempl.DisallowConditionals = true;
            _curTempl.DisallowLoops = true;
            var lvState = _curTempl.ExportLocalVariableState();
            var startSI = new StateInfo(-1);
            foreach (var kvp in _locFields)
                startSI.LVState[kvp.Key] = kvp.Value.Type.GetSampleInstance(ETypeCreationOptions.ForceCreation);
            _stateInfos[startSI] = startSI;
            _curSI = startSI;
            var stateList = new List<StateInfo>();
            stateList.Add(startSI);
            _stateQ = new Queue<StateInfo>();
            _curCoFSM = cofsm;
            startSI.Result = decomp.Decompile();
            while (_stateQ.Any())
            {
                var nextSI = _stateQ.Dequeue();
                _curSI = nextSI.Fork(nextSI.ILState);
                decomp = new MSILDecompiler(_code, _stateCFGs[nextSI.ILState + 1], _fsmInstance);
                _curTempl = decomp.Template;
                _curTempl.AddAttribute(this);
                _curTempl.DisallowReturnStatements = true;
                _curTempl.DisallowConditionals = true;
                _curTempl.DisallowLoops = true;
                _curSI.Result = decomp.Decompile();
                stateList.Add(_curSI);
                _stateInfos[_curSI] = _curSI;
            }

            // Create state active variables
            cofsm.StateActiveVars = new List<Variable>();
            int j = 0;
            for (int i = 0; i < stateList.Count; i++)
            {
                if (stateList[i].HasWaitState)
                {
                    cofsm.StateActiveVars.Add(new Variable(typeof(bool))
                    {
                        Name = prefix + "_$waitStateActive" + i
                    });
                    stateList[i].WaitStateValue = j++;
                }
                cofsm.StateActiveVars.Add(new Variable(typeof(bool))
                {
                    Name = prefix + "_$stateActive" + i
                });
                stateList[i].StateValue = j++;
            }

            int numStates = j;

            // Replace ProceedWithState calls with actual states
            var states = new Statement[numStates];

            j = 0;
            for (int i = 0; i < stateList.Count; i++)
            {
                if (stateList[i].HasWaitState)
                {
                    var wsb = new DefaultAlgorithmBuilder();
                    ImplementJoin(stateList[i].JP, wsb, stateList[i]);
                    //wsb.Call(stateList[i].JoinSpec, new Expression[0]);
                    var join = wsb.Complete();
                    states[j++] = join.Body;
                }
                states[j++] = stateList[i].Result.Decompiled.Body;
            }

            for (j = 0; j < states.Length; j++)
            {
                var orgBody = states[j];
                var xform = new CoStateAssigner(this, cofsm.StateActiveVars, orgBody, j);
                var state = xform.GetAlgorithm();
                states[j] = state.Body;
            }

            var calledMethods = stateList
                .SelectMany(b => b.Result.CalledMethods)
                .Distinct()
                .ToList();
            var calledSyncMethods = calledMethods
                .Where(mci => !mci.Method.IsAsync())
                .ToList();
            cofsm.CalledMethods = calledSyncMethods;

            // State handlers
            var alg = new DefaultAlgorithmBuilder();
            alg.Store(cofsm.DoneVar, LiteralReference.CreateConstant(false));
            for (int i = states.Length - 1; i >= 1; i--)
            {
                var lrsa = (LiteralReference)cofsm.StateActiveVars[i];
                alg.If(lrsa);
                alg.InlineCall(states[i]);
                alg.EndIf();
            }
            var handlerAlg = alg.Complete();
            cofsm.HandlerBody = handlerAlg.Body;
            alg = new DefaultAlgorithmBuilder();
            alg.Store(cofsm.DoneVar, LiteralReference.CreateConstant(false));
            alg.InlineCall(states[0]);
            cofsm.InitialHandler = alg.Complete().Body;

            return cofsm;
        }

        public IDecompilationResult DecompileToFSM()
        {
            InitializeCFGs();

            int numILStates = _stateCFGs.Length;
            var design = _code.GetDesign();
            var owner = _code.Owner as ComponentDescriptor;
            var myps = _code as ProcessDescriptor;

            // Decompile state handlers
            _stateInfos = new Dictionary<StateInfo, StateInfo>();
            var decomp = new MSILDecompiler(_code, _stateCFGs[1], _fsmInstance);
            _curTempl = decomp.Template;
            _curTempl.AddAttribute(this);
            _curTempl.DisallowReturnStatements = true;
            _curTempl.DisallowConditionals = true;
            _curTempl.DisallowLoops = true;
            var lvState = _curTempl.ExportLocalVariableState();
            var startSI = new StateInfo(0);
            foreach (var kvp in _locFields)
                startSI.LVState[kvp.Key] = kvp.Value.Type.GetSampleInstance(ETypeCreationOptions.ForceCreation);
            _stateInfos[startSI] = startSI;
            _curSI = startSI;
            var stateList = new List<StateInfo>();
            stateList.Add(startSI);
            _stateQ = new Queue<StateInfo>();
            startSI.Result = decomp.Decompile();
            while (_stateQ.Any())
            {
                var nextSI = _stateQ.Dequeue();
                _curSI = nextSI.Fork(nextSI.ILState);
                decomp = new MSILDecompiler(_code, _stateCFGs[nextSI.ILState + 1], _fsmInstance);
                _curTempl = decomp.Template;
                _curTempl.AddAttribute(this);
                _curTempl.DisallowReturnStatements = true;
                _curTempl.DisallowConditionals = true;
                _curTempl.DisallowLoops = true;
                _curSI.Result = decomp.Decompile();
                stateList.Add(_curSI);
                _stateInfos[_curSI] = _curSI;
            }

            // Create enumeration type for state
            string prefix = _code.Name;
            string enumName = "t_" + prefix + "_state";
            var stateNames = new List<string>();
            for (int i = 0; i < stateList.Count; i++)
            {
                string name;
                if (stateList[i].HasWaitState)
                {
                    name = enumName + "_await_" + i;
                    stateNames.Add(name);
                }
                name = enumName + "_" + i;
                stateNames.Add(name);
            }
            int numStates = stateNames.Count;
            Statement[] states;
            SignalDescriptor stateSignal = null;

            if (numStates > 1)
            {
                var enumType = design.CreateEnum(enumName, stateNames);
                _stateValues = enumType.CILType.GetEnumValues();
                var enumDefault = _stateValues.GetValue(0);
                int j = 0;
                for (int i = 0; i < stateList.Count; i++)
                {
                    if (stateList[i].HasWaitState)
                        stateList[i].WaitStateValue = _stateValues.GetValue(j++);
                    stateList[i].StateValue = _stateValues.GetValue(j++);
                }

                // Create signals for state
                string stateSignalName = prefix + "_state";
                stateSignal = owner.CreateSignalInstance(stateSignalName, enumDefault);
                _nextStateSignal = stateSignal;

                // Implement state joins
                states = new Statement[numStates];
                j = 0;
                for (int i = 0; i < stateList.Count; i++)
                {
                    if (stateList[i].HasWaitState)
                    {
                        var wsb = new DefaultAlgorithmBuilder();
                        ImplementJoin(stateList[i].JP, wsb, stateList[i]);
                        var join = wsb.Complete();
                        states[j++] = join.Body;
                    }
                    states[j++] = stateList[i].Result.Decompiled.Body;
                }
                // Replace ProceedWithState calls with actual states
                for (j = 0; j < states.Length; j++)
                {
                    var orgBody = states[j];
                    var xform = new StateAssigner(this, orgBody);
                    var state = xform.GetAlgorithm();
                    states[j] = state.Body;
                }
            }
            else
            {
                // Implement state joins
                states = new Statement[1];
                // Replace ProceedWithState calls with actual states
                var xform = new StateAssigner(this, stateList[0].Result.Decompiled.Body, true);
                var state = xform.GetAlgorithm();
                states[0] = state.Body;
            }

            var calledMethods = stateList
                .SelectMany(b => b.Result.CalledMethods)
                .Distinct()
                .ToList();
            var calledSyncMethods = calledMethods
                .Where(mci => !mci.Method.IsAsync())
                .ToList();

            var referencedFields = stateList
                .SelectMany(b => b.Result.ReferencedFields)
                .Distinct()
                .ToList();

            var referencedLocals = stateList
                .SelectMany(b => b.Result.Decompiled.LocalVariables)
                .Distinct();

            _coFSMs = DecompileCoFSMs(calledMethods);

            // Extract clock edge
            var predFunc = _context.CurrentProcess.Predicate;
            var predInstRef = LiteralReference.CreateConstant(predFunc.Target);
            var arg = new StackElement(predInstRef, predFunc.Target, EVariability.ExternVariable);
            var edge = _curTempl.GetCallExpression(predFunc.Method, arg).Expr;

            // Synchronous process
            var alg = new DefaultAlgorithmBuilder();
            foreach (var v in referencedLocals)
            {
                alg.DeclareLocal(v);
            }
            alg.If(edge);

            // Co state machines
            foreach (var cofsm in _coFSMs.Order)
            {
                foreach (var argv in cofsm.Arguments)
                    alg.DeclareLocal(argv);
                alg.DeclareLocal(cofsm.DoneVar);
                if (cofsm.ResultVar != null)
                    alg.DeclareLocal(cofsm.ResultVar);

                _curCoFSM = cofsm;
                var weaver = new StateWeaver(this, cofsm.HandlerBody);
                alg.InlineCall(weaver.GetAlgorithm().Body); 
            }

            // Main state machine switch statement
            if (numStates > 1)
            {
                var switchStmt = alg.Switch(
                    SignalRef.Create(stateSignal, SignalRef.EReferencedProperty.Cur));
                {
                    for (int i = 0; i < states.Length; i++)
                    {
                        var stateValue = _stateValues.GetValue(i);
                        alg.Case(LiteralReference.CreateConstant(stateValue));
                        var weaver = new StateWeaver(this, states[i]);
                        alg.InlineCall(weaver.GetAlgorithm().Body);
                        alg.Break(switchStmt);
                        alg.EndCase();
                    }
                }
                alg.EndSwitch();
            }
            else
            {
                var weaver = new StateWeaver(this, states[0]);
                alg.InlineCall(weaver.GetAlgorithm().Body);
            }
            alg.EndIf();
            var syncPS = alg.Complete();
            syncPS.Name = prefix + "$sync";
            myps.Kind = Process.EProcessKind.Triggered;
            myps.Sensitivity = new ISignalOrPortDescriptor[] { ((SignalBase)predFunc.Target).Descriptor };

            return new Result(syncPS, calledSyncMethods, referencedFields);
        }

        public IDecompilationResult Decompile()
        {
            InjectAttributes("");
            switch (ImplStyle)
            {
                case EAsyncImplStyle.Sequential:
                    return DecompileSequential();

                case EAsyncImplStyle.FSM:
                    return DecompileToFSM();

                default:
                    throw new NotImplementedException();
            }
        }
    }
}

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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Common;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Eval;

namespace SystemSharp.Components
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    class MapToWaitOn : RewriteCall, IDoNotAnalyze
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args,
            IDecompiler decomp, IFunctionBuilder builder)
        {
            Expression valarr = args[0].Expr;
            valarr = decomp.ResolveVariableReference(decomp.CurrentILIndex, valarr);
            FunctionCall newarrCall = valarr as FunctionCall;
            if (newarrCall != null)
            {
                FunctionSpec fspec = newarrCall.Callee as FunctionSpec;
                IntrinsicFunction ifun = fspec == null ? null : fspec.IntrinsicRep;
                if (ifun != null && ifun.Action == IntrinsicFunction.EAction.NewArray)
                {
                    ArrayParams aparams = (ArrayParams)ifun.Parameter;
                    if (aparams.IsStatic)
                    {
                        newarrCall.IsInlined = true;
                        foreach (Expression expr in aparams.Elements)
                            expr.IsInlined = true;
                        FunctionSpec waitf = new FunctionSpec(typeof(void))
                        {
                            CILRep = callee,
                            IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitOn)
                        };
                        builder.Call(
                            waitf,
                            aparams.Elements);
                        return true;
                    }
                }
            }
            return false;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    class MapToWaitFor : RewriteCall, IDoNotAnalyze
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args,
            IDecompiler decomp, IFunctionBuilder builder)
        {
            FunctionSpec fspec = new FunctionSpec(typeof(void))
            {
                CILRep = callee,
                IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitFor)
            };
            builder.Call(fspec,
                args.Select(arg => arg.Expr).ToArray());
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class MapToWaitNTicksRewriteAwait : RewriteAwait, IDoNotAnalyze
    {
        private static int ictr = 0;

        public override bool Rewrite(CodeDescriptor decompilee, Expression waitObject, IDecompiler stack, IFunctionBuilder builder)
        {
            if (stack.HasAttribute<Analysis.M2M.HLS>())
            {
                return true;
            }

            var curps = DesignContext.Instance.CurrentProcess;
            SLSignal clk = (SLSignal)curps.Sensitivity[0].Owner;
            SignalRef srEdge;
            if (curps.Predicate.Equals((Func<bool>)clk.RisingEdge))
                srEdge = SignalRef.Create(clk.Descriptor, SignalRef.EReferencedProperty.RisingEdge);
            else
                srEdge = SignalRef.Create(clk.Descriptor, SignalRef.EReferencedProperty.FallingEdge);
            var lrEdge = new LiteralReference(srEdge);

            int nwait = 0;
            var nwaitEx = waitObject.Children[0];
            bool nwaitConst = nwaitEx.IsConst();
            if (nwaitConst)
            {
                nwait = (int)TypeConversions.ConvertValue(
                        nwaitEx.Eval(DefaultEvaluator.DefaultConstEvaluator),
                        typeof(int));
            }

            var fspec = new FunctionSpec(typeof(void))
            {
                IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitUntil)
            };

            Variable v = null;
            LiteralReference lrV = null;

            if (!nwaitConst || nwait > 3)
            {
                v = new Variable(typeof(int))
                {
                    Name = "_wait_i" + (ictr++)
                };
                builder.DeclareLocal(v);
                lrV = new LiteralReference(v);
                builder.Store(v, LiteralReference.CreateConstant((int)0));
                var loop = builder.Loop();
                builder.If(Expression.Equal(lrV, nwaitEx));
                {
                    builder.Break(loop);
                }
                builder.EndIf();
            }
            int ncalls = 1;
            if (nwaitConst && nwait <= 3)
                ncalls = nwait;
            for (int i = 0; i < ncalls; i++)
            {
                builder.Call(fspec, lrEdge);
            }
            if (!nwaitConst || nwait > 3)
            {
                builder.Store(v, lrV + LiteralReference.CreateConstant((int)1));
                builder.EndLoop();
            }
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    class MapToWaitNTicksRewriteCall : RewriteCall, IDoNotAnalyze
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            var amd = stack.QueryAttribute<AsyncMethodDecompiler>();
            if (amd != null && amd.ImplStyle == EAsyncImplStyle.FSM)
            {
                return false;
            }

            object[] outArgs;
            object result;
            stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out result);

            var fspec = new FunctionSpec(typeof(Task))
            {
                CILRep = callee
            };
            var fcall = new FunctionCall()
            {
                Callee = fspec,
                Arguments = args.Select(a => a.Expr).ToArray(),
                ResultType = TypeDescriptor.GetTypeOf(result)
            };

            stack.Push(fcall, result);

            return true;
        }
    }
}

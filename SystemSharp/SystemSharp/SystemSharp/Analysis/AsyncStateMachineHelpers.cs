/**
 * Copyright 2013 Christian Köllner
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
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using SDILReader;
using SystemSharp.Meta;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.Analysis
{
    /// <summary>
    /// Very preliminary and subject to change, therefore not documented, sorry...
    /// </summary>
    class JoinParams
    {
        public Task JoinedTask { get; set; }
        public StateInfo Continuation { get; set; }
    }

    /// <summary>
    /// Very preliminary and subject to change, therefore not documented, sorry...
    /// </summary>
    class StateInfo
    {
        public int ILState { get; private set; }
        public bool HasWaitState { get; set; }
        public Dictionary<string, object> LVState { get; private set; }
        public object StateValue { get; set; }
        public object WaitStateValue { get; set; }
        public JoinParams JP { get; set; }
        public IDecompilationResult Result { get; set; }

        public StateInfo(int ilState)
        {
            ILState = ilState;
            LVState = new Dictionary<string, object>();
        }

        public override bool Equals(object obj)
        {
            var other = obj as StateInfo;
            if (other == null)
                return false;

            return other.ILState == ILState /*&&
                    LVState.Values.SequenceEqual(other.LVState.Values)*/;
        }

        public override int GetHashCode()
        {
            return ILState.GetHashCode() /*^ 
                    LVState.Values.GetSequenceHashCode()*/;
        }

        public override string ToString()
        {
            return "<" + ILState + "| " + string.Join(",", LVState.Select(kvp => kvp.Key + "=" + kvp.Value)) + ">";
        }

        public StateInfo Fork(int ilState)
        {
            var fork = new StateInfo(ilState);
            foreach (var kvp in LVState)
                fork.LVState[kvp.Key] = kvp.Value;
            if (ilState == ILState && HasWaitState)
            {
                fork.HasWaitState = true;
                fork.JP = JP;
            }
            return fork;
        }
    }

    /// <summary>
    /// Very preliminary and subject to change, therefore not documented, sorry...
    /// </summary>
    class ProceedWithStateInfo
    {
        public StateInfo TargetState { get; set; }
        public bool TargetWaitState { get; set; }
        public bool LambdaTransition { get; set; }

        public override string ToString()
        {
            string text = "";
            if (LambdaTransition)
                text += "lambda ";
            text += "to ";
            if (TargetWaitState)
                text += "wait ";
            text += "state ";
            text += TargetState.ILState;
            return text;
        }
    }

    /// <summary>
    /// Very preliminary and subject to change, therefore not documented, sorry...
    /// </summary>
    class CoFSM
    {
        public Task CoTask;
        public ISet<Task> Dependencies;
        public MethodDescriptor Method;
        public Variable[] Arguments;
        public Variable ResultVar;
        public Variable DoneVar;
        public List<Variable> StateActiveVars;
        public Statement InitialHandler;
        public Statement HandlerBody;
        public ICollection<MethodCallInfo> CalledMethods;
        public int Order;
    }

    /// <summary>
    /// Very preliminary and subject to change, therefore not documented, sorry...
    /// </summary>
    struct CoFSMs
    {
        public Dictionary<Task, CoFSM> Map;
        public List<CoFSM> Order;

        private int PostOrderDFS(Task task, int cur)
        {
            var cofsm = Map[task];
            var deps = cofsm.Dependencies;
            foreach (var dep in deps)
            {
                var depco = Map[dep];
                if (depco.Order == 0)
                    cur = PostOrderDFS(depco.CoTask, cur);
            }
            cofsm.Order = ++cur;
            return cur;
        }

        public void CreateOrder()
        {
            int cur = 0;
            foreach (var task in Map.Keys)
                cur = PostOrderDFS(task, cur);
            Order = Map.Values.OrderBy(v => v.Order).ToList();
        }
    }

    static class StateTargetFinder
    {
        enum EStateLocMode
        {
            LocatingStateField,
            LocatingStateLocal
        }

        private static bool IsLdc_I(ILInstruction cili, out int value)
        {
            if (cili.Code == OpCodes.Ldc_I4)
            {
                value = (int)cili.Operand;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_0)
            {
                value = 0;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_1)
            {
                value = 1;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_2)
            {
                value = 2;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_3)
            {
                value = 3;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_4)
            {
                value = 4;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_5)
            {
                value = 5;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_6)
            {
                value = 6;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_7)
            {
                value = 7;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_8)
            {
                value = 8;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_M1)
            {
                value = -1;
                return true;
            }
            else if (cili.Code == OpCodes.Ldc_I4_S)
            {
                value = (sbyte)cili.Operand;
                return true;
            }

            value = int.MaxValue;
            return false;
        }

        private static bool IsStloc(ILInstruction cili, out int value)
        {
            if (cili.Code == OpCodes.Stloc)
            {
                value = (int)cili.Operand;
                return true;
            }
            else if (cili.Code == OpCodes.Stloc_S)
            {
                value = (byte)cili.Operand;
                return true;
            }
            else if (cili.Code == OpCodes.Stloc_0)
            {
                value = 0;
                return true;
            }
            else if (cili.Code == OpCodes.Stloc_1)
            {
                value = 1;
                return true;
            }
            else if (cili.Code == OpCodes.Stloc_2)
            {
                value = 2;
                return true;
            }
            else if (cili.Code == OpCodes.Stloc_3)
            {
                value = 3;
                return true;
            }

            value = int.MaxValue;
            return false;
        }

        private static bool IsLdloc(ILInstruction cili, out int value)
        {
            if (cili.Code == OpCodes.Ldloc)
            {
                value = (int)cili.Operand;
                return true;
            }
            else if (cili.Code == OpCodes.Ldloc_S)
            {
                value = (byte)cili.Operand;
                return true;
            }
            else if (cili.Code == OpCodes.Ldloc_0)
            {
                value = 0;
                return true;
            }
            else if (cili.Code == OpCodes.Ldloc_1)
            {
                value = 1;
                return true;
            }
            else if (cili.Code == OpCodes.Ldloc_2)
            {
                value = 2;
                return true;
            }
            else if (cili.Code == OpCodes.Ldloc_3)
            {
                value = 3;
                return true;
            }

            value = int.MaxValue;
            return false;
        }

        /// <summary>
        /// Given a control-flow graph of an asynchronous method, finds all state entry points.
        /// This is done using some pattern matching and knowledge on how the compiler translates asynchronous methods.
        /// Therefore, some special cases might not be resolved correctly, and the code might break down with future compiler revisions.
        /// </summary>
        /// <param name="cfg">a control-flow graph</param>
        /// <returns>all state entry points</returns>
        public static MSILCodeBlock[] FindStateTargets(MethodCode cfg)
        {
            var entry = cfg.BasicBlocks[0];
            FieldInfo statefield = null;
            int stateLocalIndex = -1;
            int stateValue = int.MinValue;
            int stateOffset = 0;
            var mode = EStateLocMode.LocatingStateField;
            var q = new Queue<Tuple<int, MSILCodeBlock>>();
            foreach (var cili in entry.Range)
            {
                switch (mode)
                {
                    case EStateLocMode.LocatingStateField:
                        if (cili.Code == OpCodes.Ldfld)
                        {
                            // this must be the state field
                            statefield = (FieldInfo)cili.Operand;
                            mode = EStateLocMode.LocatingStateLocal;
                        }
                        break;

                    case EStateLocMode.LocatingStateLocal:
                        {
                            int index;
                            if (IsStloc(cili, out index))
                                stateLocalIndex = index;
                        }
                        break;
                }

                int value;
                if (IsLdc_I(cili, out value))
                {
                    stateValue = value;
                }

                if (cili.Code == OpCodes.Sub)
                {
                    stateOffset = stateValue;
                }
                else if (cili.Code == OpCodes.Beq ||
                    cili.Code == OpCodes.Beq_S)
                {
                    // State -3 exists only in debug builds and skips the whole FSM => irrelevant
                    if (stateValue != -3)
                    {
                        q.Enqueue(Tuple.Create(stateValue, entry.Successors[1]));
                    }
                    q.Enqueue(Tuple.Create(-1, entry.Successors[0]));
                }
                else if (cili.Code == OpCodes.Switch)
                {
                    bool have_1 = false;
                    for (int i = 1; i < entry.Successors.Length; i++)
                    {
                        int state = i - 1 + stateOffset;
                        // State -3 exists only in debug builds and skips the whole FSM => irrelevant
                        // State -2 is completion state => also irrelevant
                        if (state >= -1)
                        {
                            q.Enqueue(Tuple.Create(state, entry.Successors[i]));

                            if (state == -1)
                                have_1 = true;
                        }
                    }
                    if (!have_1)
                        q.Enqueue(Tuple.Create(-1, entry.Successors[0]));
                }
            }
            var stateDic = new SortedDictionary<int, MSILCodeBlock>();
            while (q.Any())
            {
                var tup = q.Dequeue();
                int state = tup.Item1;
                var block = tup.Item2;

                int value;
                if (IsLdloc(block.Range.First(), out value) &&
                    value == stateLocalIndex)
                {
                    if (block.Range.Count != 3)
                        throw new NotSupportedException("Decompilation of await pattern failed: expected 3 instructions in this block.");

                    if (!IsLdc_I(block.Range[1], out stateValue))
                        throw new NotSupportedException("Decompilation of await pattern failed: expected Ldc_I<x> op-code.");

                    if (block.Range[2].Code != OpCodes.Beq &&
                        block.Range[2].Code != OpCodes.Beq_S)
                        throw new NotSupportedException("Decompilation of await pattern failed: expected Beq/Beq_S op-code.");

                    q.Enqueue(Tuple.Create(stateValue, block.Successors[1]));
                    q.Enqueue(Tuple.Create(-1, block.Successors[0]));
                }
                else
                {
                    stateDic[state] = block;
                }
            }
            for (int state = -1; state < stateDic.Count - 1; state++)
            {
                if (!stateDic.ContainsKey(state))
                    throw new NotSupportedException("Decompilation of await pattern failed: expected consecutive states from -1 to " + (stateDic.Count-1));
            }
            return stateDic.Values.ToArray();
        }
    }
}

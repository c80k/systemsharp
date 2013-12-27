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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using SDILReader;
using SystemSharp.Common;

namespace SystemSharp.Analysis.Msil
{
    /// <summary>
    /// Static helper class for dealing with the StackBehavior type
    /// </summary>
    public static class StackOperands
    {
        /// <summary>
        /// Returns the number of elements being pushed or popped by a specific StackBehavior.
        /// </summary>
        /// <param name="sb">a StackBehavior</param>
        /// <returns>number of elements being pushed (i.e. positive number) or -(number of elements being popped) (i.e. negative number)</returns>
        public static int GetNumOperands(StackBehaviour sb)
        {
            switch (sb)
            {
                case StackBehaviour.Pop0:
                case StackBehaviour.Push0:
                case StackBehaviour.Varpop:
                case StackBehaviour.Varpush:
                    return 0;

                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    return -1;

                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    return -2;

                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return -3;

                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    return 1;

                case StackBehaviour.Push1_push1:
                    return 2;

                default:
                    throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    /// Static helper class for dealing with the stack behavior of IL instructions.
    /// </summary>
    public static class StackInfluenceAnalysis
    {
        /// <summary>
        /// Determines, how many operands a given CIL instruction will remove from the stack, and how many it will push onto the stack.
        /// </summary>
        /// <param name="ili">a CIL instruction</param>
        /// <param name="method">the method inside whose context the given instruction is executed</param>
        /// <param name="npop">number of removed stack operands</param>
        /// <param name="npush">number of stack operands pushed onto the stack</param>
        public static void GetStackBilance(ILInstruction ili, MethodBase method, out int npop, out int npush)
        {
            if (ili.Code.Equals(OpCodes.Ret))
            {
                Type returnType;
                if (method.IsFunction(out returnType))
                    npop = 1;
                else
                    npop = 0;
                npush = 0;
            }
            else if (ili.Code.Equals(OpCodes.Call) ||
                ili.Code.Equals(OpCodes.Calli) ||
                ili.Code.Equals(OpCodes.Callvirt) ||
                ili.Code.Equals(OpCodes.Newobj))
            {
                MethodBase mi = (MethodBase)ili.Operand;
                ParameterInfo[] pis = mi.GetParameters();
                npop = pis.Length;
                if (ili.Code.Equals(OpCodes.Calli))
                    ++npop;
                if (mi.CallingConvention.HasFlag(CallingConventions.HasThis) &&
                    !ili.Code.Equals(OpCodes.Newobj))
                    ++npop;
                Type returnType;
                if (mi.IsFunction(out returnType) ||
                    ili.Code.Equals(OpCodes.Newobj))
                    npush = 1;
                else
                    npush = 0;
            }
            else
            {
                npop = -StackOperands.GetNumOperands(ili.Code.StackBehaviourPop);
                npush = StackOperands.GetNumOperands(ili.Code.StackBehaviourPush);
            }
        }

        /// <summary>
        /// Determines the set of CIL instructions which potentially compute a particular stack element.
        /// </summary>
        /// <param name="ilIndex">index of CIL instruction</param>
        /// <param name="stackLevel">index of stack element (0 is top)</param>
        /// <param name="cfg">control-flow graph</param>
        /// <returns>indices of CIL instructions which come into consideration for producing specified stack element</returns>
        public static IEnumerable<int> GetStackElementDefinitions(int ilIndex, int stackLevel, MethodCode cfg)
        {
            Queue<Tuple<int, int>> q = new Queue<Tuple<int, int>>();
            bool[] visited = new bool[cfg.Instructions.Count];
            foreach (ILInstruction pred in cfg.GetPredecessors(ilIndex))
            {
                q.Enqueue(Tuple.Create(pred.Index, stackLevel));
            }
            while (q.Count > 0)
            {
                Tuple<int, int> cur = q.Dequeue();
                int curIndex = cur.Item1;
                int curLevel = cur.Item2;
                visited[curIndex] = true;
                int npush, npop;
                GetStackBilance(cfg.Instructions[curIndex], cfg.Method, out npop, out npush);
                if (npush + curLevel >= 1)
                {
                    yield return curIndex;
                }
                else
                {
                    int delta = npush - npop;
                    int nextLevel = curLevel + delta;
                    if (nextLevel > 0)
                        throw new InvalidOperationException("Illegal stack behavior detected in method " + cfg.Method);
                    foreach (ILInstruction pred in cfg.GetPredecessors(curIndex))
                    {
                        if (!visited[pred.Index])
                            q.Enqueue(Tuple.Create(pred.Index, nextLevel));
                    }
                }
            }
        }

    }
}

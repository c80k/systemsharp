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
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Reflection.Emit;
using SDILReader;
using SystemSharp.Collections;
using SystemSharp.Common;

namespace SystemSharp.Analysis.Msil
{
    /// <summary>
    /// Represents the state of the program stack at a specific program location.
    /// </summary>
    /// <typeparam name="TElem">Type for representing the state of a stack element, local variable or method parameter</typeparam>
    [ContractClass(typeof(AbstractStackStateContractClass<>))]
    public abstract class AbstractStackState<TElem>
    {
        /// <summary>
        /// Returns the state of the stack element at position index.
        /// </summary>
        /// <param name="index">Position on stack. 0 is top-most, 1 is below top-most and so on.</param>
        /// <returns>The state of the specified stack element</returns>
        public abstract TElem this[int index] { get; }

        /// <summary>
        /// Returns the current depth of the program stack.
        /// </summary>
        public abstract int Depth { get; }

        /// <summary>
        /// Returns the method parameter count.
        /// </summary>
        public abstract int NumArguments { get; }

        /// <summary>
        /// Returns the number of local variables.
        /// </summary>
        public abstract int NumLocals { get; }

        /// <summary>
        /// Returns the state of a local variable
        /// </summary>
        /// <param name="index">0-based index of local variable</param>
        /// <returns>the state of that local variable</returns>
        public abstract TElem GetLocal(int index);

        /// <summary>
        /// Returns the state of a method parameter.
        /// </summary>
        /// <param name="index">0-based index of method parameter</param>
        /// <returns>the state of that method parameter</returns>
        public abstract TElem GetArgument(int index);

        /// <summary>
        /// Constructs the stack state which results from pushing an element to the program stack.
        /// </summary>
        /// <param name="objs">state of element to push</param>
        /// <returns>the resulting stack state</returns>
        public virtual AbstractStackState<TElem> Push(TElem objs)
        {
            return new PushStackState<TElem>(this, objs);
        }

        /// <summary>
        /// Constructs the stack state which results from removing the top-most element from the program stack.
        /// </summary>
        /// <returns>the resulting stack state</returns>
        public virtual AbstractStackState<TElem> Pop()
        {
            return new PopStackState<TElem>(this);
        }

        /// <summary>
        /// Constructs the stack state which results from assigning a new value to a local variable.
        /// </summary>
        /// <param name="localIndex">0-based index of local variable</param>
        /// <param name="rvalues">state of assigned value</param>
        /// <returns>the resulting stack state</returns>
        public virtual AbstractStackState<TElem> Assign(int localIndex, TElem rvalues)
        {
            return new AsmtStackState<TElem>(this, localIndex, rvalues);
        }

        /// <summary>
        /// Constructs the stack state which results from assigning a new value to a method parameter
        /// </summary>
        /// <param name="argIndex">0-based index of method parameter</param>
        /// <param name="rvalues">state of assigned value</param>
        /// <returns>the resulting stack state</returns>
        public virtual AbstractStackState<TElem> AssignArg(int argIndex, TElem rvalues)
        {
            return new ArgAsmtStackState<TElem>(this, argIndex, rvalues);
        }
    }

    [ContractClassFor(typeof(AbstractStackState<>))]
    abstract class AbstractStackStateContractClass<TElem> : AbstractStackState<TElem>
    {
        public override TElem this[int index]
        {
            get 
            {
                Contract.Requires(index >= 0);
                Contract.Requires(index < Depth);
                return default(TElem);
            }
        }

        public override int Depth
        {
            get { return 0; }
        }

        public override int NumArguments
        {
            get { return 0; }
        }

        public override int NumLocals
        {
            get { return 0; }
        }

        public override TElem GetLocal(int index)
        {
            Contract.Requires(index >= 0);
            Contract.Requires(index < NumLocals);
            return default(TElem);
        }

        public override TElem GetArgument(int index)
        {
            Contract.Requires(index >= 0);
            Contract.Requires(index < NumArguments);
            return default(TElem);
        }
    }

    /// <summary>
    /// Represents a stack state which does not store all state information by itself, but instead relies on the state information saved in the predecessor state.
    /// </summary>
    /// <typeparam name="TElem">Type for representing the state of a stack element, local variable or method parameter</typeparam>
    public abstract class DependentStackState<TElem> : AbstractStackState<TElem>
    {
        /// <summary>
        /// The predecessor state
        /// </summary>
        public AbstractStackState<TElem> Pre { get; private set; }

        /// <summary>
        /// Constructs an instance based on predecessor state pre.
        /// </summary>
        /// <param name="pre">the predecessor state</param>
        public DependentStackState(AbstractStackState<TElem> pre)
        {
            Pre = pre;
        }

        public override TElem this[int index]
        {
            get
            {
                return Pre[index];
            }
        }

        public override TElem GetLocal(int index)
        {
            return Pre.GetLocal(index);
        }

        public override TElem GetArgument(int index)
        {
            return Pre.GetArgument(index);
        }

        public override int Depth
        {
            get { return Pre.Depth; }
        }

        public override int NumLocals
        {
            get { return Pre.NumLocals; }
        }

        public override int NumArguments
        {
            get { return Pre.NumArguments; }
        }
    }

    /// <summary>
    /// Represents a stack state which results from pushing a value onto the program stack of a predecessor state.
    /// </summary>
    /// <typeparam name="TElem">Type for representing the state of a stack element, local variable or method parameter</typeparam>
    public class PushStackState<TElem> : DependentStackState<TElem>
    {
        private TElem _top;

        /// <summary>
        /// Constructs an instance based on a predecessor state and an element state.
        /// </summary>
        /// <param name="pre">the predecessor state</param>
        /// <param name="top">the element state</param>
        public PushStackState(AbstractStackState<TElem> pre, TElem top):
            base(pre)
        {
            _top = top;
        }

        public override TElem this[int index]
        {
            get
            {
                if (index == 0)
                    return _top;
                else
                    return Pre[index - 1];
            }
        }

        public override int Depth
        {
            get { return Pre.Depth + 1; }
        }

        public override string ToString()
        {
            return Pre.ToString() + ".push";
        }

        public override AbstractStackState<TElem> Pop()
        {
            return Pre;
        }
    }

    /// <summary>
    /// Represents a stack state which results from removing the top-most value from the program stack of a predecessor state.
    /// </summary>
    /// <typeparam name="TElem">Type for representing the state of a stack element, local variable or method parameter</typeparam>
    public class PopStackState<TElem> : DependentStackState<TElem>
    {
        /// <summary>
        /// Constructs an instance based on a predecessor state.
        /// </summary>
        /// <param name="pre">the predecessor state</param>
        public PopStackState(AbstractStackState<TElem> pre):
            base(pre)
        {
        }

        public override TElem this[int index]
        {
            get { return Pre[index + 1]; }
        }

        public override int Depth
        {
            get { return Pre.Depth - 1; }
        }

        public override string ToString()
        {
            return Pre.ToString() + ".pop";
        }
    }

    /// <summary>
    /// Represents a stack state which results from assigning a value to a local variable of a predecessor state.
    /// </summary>
    /// <typeparam name="TElem">Type for representing the state of a stack element, local variable or method parameter</typeparam>
    public class AsmtStackState<TElem> : DependentStackState<TElem>
    {
        private int _localIndex;
        private TElem _rvalues;

        /// <summary>
        /// Constructs an instance based on predecessor state, index of assigned local variable and state of assigned value.
        /// </summary>
        /// <param name="pre">the predecessor state</param>
        /// <param name="localIndex">index of local variable</param>
        /// <param name="rvalues">state of assigned value</param>
        public AsmtStackState(AbstractStackState<TElem> pre, int localIndex, TElem rvalues):
            base(pre)
        {
            _localIndex = localIndex;
            _rvalues = rvalues;
        }

        public override TElem GetLocal(int index)
        {
            if (index == _localIndex)
                return _rvalues;
            else
                return Pre.GetLocal(index);
        }

        public override string ToString()
        {
            return Pre.ToString() + ".{loc" + _localIndex + "}";
        }
    }

    /// <summary>
    /// Represents a stack state which results from assigning a value to a method parameter of a predecessor state.
    /// </summary>
    /// <typeparam name="TElem">Type for representing the state of a stack element, local variable or method parameter</typeparam>
    public class ArgAsmtStackState<TElem> : DependentStackState<TElem>
    {
        private int _argIndex;
        private TElem _rvalues;

        /// <summary>
        /// Constructs an instance based on predecessor state, index of method parameter and state of assigned value
        /// </summary>
        /// <param name="pre">the predecessor state</param>
        /// <param name="argIndex">index of method parameter</param>
        /// <param name="rvalues">state of assigned value</param>
        public ArgAsmtStackState(AbstractStackState<TElem> pre, int argIndex, TElem rvalues):
            base(pre)
        {
            _argIndex = argIndex;
            _rvalues = rvalues;
        }

        public override TElem GetArgument(int index)
        {
            if (index == _argIndex)
                return _rvalues;
            else
                return Pre.GetArgument(index);
        }

        public override string ToString()
        {
            return Pre.ToString() + ".{arg" + _argIndex + "}";
        }
    }

    /// <summary>
    /// Represents a self-contained stack state, thus storing all required state information by itself.
    /// </summary>
    /// <typeparam name="TElem">Type for representing the state of a stack element, local variable or method parameter</typeparam>
    /// <remarks>FIXME: poor software design</remarks>
    public abstract class IndependentStackStateBase<TElem> : AbstractStackState<TElem>
    {
        /// <summary>
        /// List of local variables - must be assigned inside overriding class!
        /// </summary>
        protected List<TElem> _locals;

        /// <summary>
        /// List of method parameters - must be assigned inside overriding class!
        /// </summary>
        protected List<TElem> _args;

        /// <summary>
        /// List of stack elements - must be assigned inside overriding class!
        /// </summary>
        protected List<TElem> _stack;

        public IndependentStackStateBase()
        {
        }

        public override TElem this[int index]
        {
            get { return _stack[index]; }
        }

        public override TElem GetLocal(int index)
        {
            return _locals[index];
        }

        public override TElem GetArgument(int index)
        {
            return _args[index];
        }

        public override int Depth
        {
            get { return _stack.Count; }
        }

        public override int NumLocals
        {
            get { return _locals.Count; }
        }

        public override int NumArguments
        {
            get { return _args.Count; }
        }

        public override string ToString()
        {
            return "{" + _stack.Count + "}";
        }
    }

    /// <summary>
    /// Provides a basic framework for static code analysis of CIL (formerly MSIL) code. Inherit from this class to implement your own analysis.
    /// </summary>
    /// <typeparam name="TElem">The type which represents an element of the execution stack.</typeparam>
    /// <remarks>The idea of fix point analysis is as follows: Each instruction modifies the state of the program stack. If we propagate some initial state
    /// from one instruction to the next (according to program flow), we can infer state information on each program position. If we encounter a conditional branch,
    /// we will take both directions, i.e. analysis will fork (since analysis is static). Wherever two program flows join again, this will lead to two program states
    /// being merged. Starting from each merged state, we recompute the analysis until we get convergence. This is called the fix point, since merging any state with the
    /// newly computed state results in a state which is equivalent to the merged state.
    /// <para>FIXME: poor software design</para>
    /// </remarks>
    [ContractClass(typeof(FixPointAnalyzerContractClass<>))]
    public abstract class FixPointAnalyzer<TElem>
    {
        /// <summary>
        /// Returns the method which is subject to analysis.
        /// </summary>
        public MethodBase Method { get; private set; }

        /// <summary>
        /// Returns true iff the analysis was completed.
        /// </summary>
        public bool AnalysisDone { get; private set; }

        /// <summary>
        /// Maps each CIL op-code to an according propagation function - must be populated by overriding class!
        /// </summary>
        protected Dictionary<OpCode, Func<ILInstruction, AbstractStackState<TElem>, AbstractStackState<TElem>>> _pmap =
            new Dictionary<OpCode, Func<ILInstruction, AbstractStackState<TElem>, AbstractStackState<TElem>>>();

        /// <summary>
        /// Assigns a stack state to each instruction index.
        /// </summary>
        protected AbstractStackState<TElem>[] _stackStates;

        /// <summary>
        /// Constructs an instance based on a specific method.
        /// </summary>
        /// <param name="method">the method to be analyzed</param>
        public FixPointAnalyzer(MethodBase method)
        {
            Contract.Requires<ArgumentNullException>(method != null);

            Method = method;
        }

        /// <summary>
        /// Propagates a stack state by computing the effects of executing a specific instruction.
        /// </summary>
        /// <param name="ili"></param>
        /// <param name="pre"></param>
        /// <returns></returns>
        protected virtual AbstractStackState<TElem> Propagate(ILInstruction ili, AbstractStackState<TElem> pre)
        {
            int preDepth = pre.Depth;
            AbstractStackState<TElem> post = _pmap[ili.Code](ili, pre);
            int postDepth = post.Depth;
            int npush, npop;
            StackInfluenceAnalysis.GetStackBilance(ili, Method, out npop, out npush);
            Debug.Assert(postDepth - preDepth == npush - npop);
            return post;
        }

        /// <summary>
        /// Creates an initial stack state.
        /// </summary>
        /// <returns>the initial stack state</returns>
        protected abstract AbstractStackState<TElem> CreateInitialStackState();

        /// <summary>
        /// Merges two stack states.
        /// </summary>
        /// <remarks>Merging stack states is necessary wherever program flow joins.</remarks>
        /// <param name="a">a stack state</param>
        /// <param name="b">another stack state</param>
        /// <param name="m">the merged stack state</param>
        /// <returns>false iff m and a are equivalent in a problem-specific sense.</returns>
        protected abstract bool Merge(
            AbstractStackState<TElem> a, 
            AbstractStackState<TElem> b, 
            out AbstractStackState<TElem> m);

        /// <summary>
        /// Returns the control-flow graph of the analyzed method.
        /// </summary>
        protected MethodCode CFG { get; private set; }

        /// <summary>
        /// Executes the analysis.
        /// </summary>
        public virtual void Run()
        {
            Contract.Requires(!AnalysisDone);
            Contract.Ensures(AnalysisDone);

            MethodFacts myFacts = FactUniverse.Instance.GetFacts(Method);
            CFG = myFacts.CFG;

            _stackStates = new AbstractStackState<TElem>[CFG.Instructions.Count];
            AbstractStackState<TElem> initial = CreateInitialStackState();
            Queue<Tuple<AbstractStackState<TElem>, ILInstruction>> q = new Queue<Tuple<AbstractStackState<TElem>, ILInstruction>>();
            q.Enqueue(Tuple.Create(initial, CFG.Instructions[0]));
            int marshal = CFG.Instructions.Count - 1;
            while (q.Count > 0)
            {
                Tuple<AbstractStackState<TElem>, ILInstruction> cur = q.Dequeue();
                AbstractStackState<TElem> preState = cur.Item1;
                ILInstruction ili = cur.Item2;
                AbstractStackState<TElem> nextState = Propagate(ili, preState);
                ILInstruction[] succs = CFG.GetSuccessorsOf(ili);
                if (_stackStates[ili.Index] == null)
                    _stackStates[ili.Index] = nextState;
                else
                {
                    bool changed = Merge(_stackStates[ili.Index], nextState, out _stackStates[ili.Index]);
                    if (!changed)
                        continue;
                }                
                foreach (ILInstruction nextILI in succs)
                {
                    if (nextILI.Index < marshal) // skip pseudo ret at the end
                        q.Enqueue(Tuple.Create(_stackStates[ili.Index], nextILI));
                }
            }

            AnalysisDone = true;
        }
    }

    [ContractClassFor(typeof(FixPointAnalyzer<>))]
    abstract class FixPointAnalyzerContractClass<TElem> : FixPointAnalyzer<TElem>
    {
        FixPointAnalyzerContractClass(MethodBase method) :
            base(method)
        {
        }

        protected override AbstractStackState<TElem> CreateInitialStackState()
        {
            Contract.Ensures(Contract.Result<AbstractStackState<TElem>>() != null);
            return null;
        }

        protected override bool Merge(AbstractStackState<TElem> a, AbstractStackState<TElem> b, out AbstractStackState<TElem> m)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Contract.Requires(a.Depth == b.Depth);
            Contract.Requires(a.NumLocals == b.NumLocals);
            Contract.Requires(a.NumArguments == b.NumArguments);
            Contract.Ensures(Contract.ValueAtReturn(out m) != null);

            m = null; //so that Code Contracts can be deactivated completely, otherwise the Compiler complains that m is unassigned on return

            return false;
        }
    }
}

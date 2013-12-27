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
 * 
 * CHANGE LOG
 * ==========
 * 2011-08-15 CK -conservative assumption for ldelem, ldind: variable result
 *               -fixed bug in HandleCall
 * 2012-07-26 CK -"constant" variability for methods with StaticEvaluation attribute
 * 2013-01-08 CK -added concept of localized fields, i.e. field which are treated like local variables
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using SDILReader;
using SystemSharp.Collections;
using SystemSharp.Common;

namespace SystemSharp.Analysis.Msil
{
    /// <summary>
    /// Variability classification
    /// </summary>
    /// <remarks>
    /// Variability is associated with stack elements, local variables and method arguments.
    /// </remarks>
    public enum EVariability
    {
        /// <summary>
        /// The element has a constant value.
        /// </summary>
        Constant,

        /// <summary>
        /// The element gets assigned different values, but these values do not depend on external input.
        /// </summary>
        LocalVariable,

        /// <summary>
        /// The element gets assigned different values which might depend on external input.
        /// </summary>
        ExternVariable
    }

    public static class VariabilityOperations
    {
        /// <summary>
        /// Returns the stronger of two variability classifications in order Constant, LocalVariable, ExternVariable
        /// </summary>
        /// <param name="a">first variability</param>
        /// <param name="b">second variability</param>
        /// <returns>stronger of both variabilities</returns>
        public static EVariability Stronger(EVariability a, EVariability b)
        {
            switch (a)
            {
                case EVariability.ExternVariable:
                    return EVariability.ExternVariable;

                case EVariability.LocalVariable:
                    return b == EVariability.ExternVariable ?
                        EVariability.ExternVariable : EVariability.LocalVariable;

                case EVariability.Constant:
                    return b;

                default:
                    throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    /// This data structure captures the variability of an element together with its reaching definitions
    /// </summary>
    public class VariabilityInfo
    {
        /// <summary>
        /// The variability classification
        /// </summary>
        public EVariability Variability { get; private set; }

        /// <summary>
        /// Reaching definitions in terms of 0-based instruction indices
        /// </summary>
        public IEnumerable<int> Definitions { get; private set; }

        /// <summary>
        /// Constructs a new instance given a variability and reaching definitions
        /// </summary>
        /// <param name="variability">Variability classification</param>
        /// <param name="definitions">Reaching definitions</param>
        public VariabilityInfo(EVariability variability, IEnumerable<int> definitions)
        {
            Variability = variability;
            Definitions = definitions;
        }

        /// <summary>
        /// Two instances are defined to be equal iff they have the same variability and the same set of reaching definitions.
        /// </summary>
        /// <param name="obj">some object, possibly an instance of VariabilityInfo</param>
        /// <returns>whether both instances meet the equality criterion</returns>
        public override bool Equals(object obj)
        {
            VariabilityInfo vi = obj as VariabilityInfo;
            if (vi == null)
                return false;
            return Variability == vi.Variability &&
                Definitions.SequenceEqual(vi.Definitions);
        }

        public override int GetHashCode()
        {
            return Variability.GetHashCode() ^
                Definitions.GetSequenceHashCode();
        }

        /// <summary>
        /// Merges two instances under assumption of a single reaching definition
        /// </summary>
        /// <param name="a">first instance</param>
        /// <param name="b">second instance</param>
        /// <param name="def">the one and only reaching definition</param>
        /// <returns>merged instance, based on the stronger of both variabilities</returns>
        public static VariabilityInfo MergeByNewDef(VariabilityInfo a, VariabilityInfo b, int def)
        {
            return CreateBySingleDef(
                VariabilityOperations.Stronger(a.Variability, b.Variability),
                def);
        }

        /// <summary>
        /// Constructs an instance based on a variability and a single reaching definition
        /// </summary>
        /// <param name="var">variability classification</param>
        /// <param name="def">one and only reaching definition</param>
        /// <returns>an instance representing the supplied arguments</returns>
        public static VariabilityInfo CreateBySingleDef(EVariability var, int def)
        {
            return new VariabilityInfo(var, new int[]{ def });
        }

        /// <summary>
        /// Merges two instances based on the stronger of both variabilities and the set union of their reaching definitions
        /// </summary>
        /// <param name="a">first instance</param>
        /// <param name="b">second instance</param>
        /// <returns>instance representing the merged information of both instances</returns>
        public static VariabilityInfo MergeDefs(VariabilityInfo a, VariabilityInfo b)
        {
            Contract.Requires(a.Definitions != null);
            Contract.Requires(b.Definitions != null);

            return new VariabilityInfo(
                VariabilityOperations.Stronger(a.Variability, b.Variability),
                a.Definitions.Union(b.Definitions).Distinct().ToArray());
        }

        /// <summary>
        /// Local variables are initially assumed to be of constant variability without any reaching definition, since this represent their initial specification.
        /// </summary>
        public static readonly VariabilityInfo DefaultLocalInit = 
            new VariabilityInfo(EVariability.Constant, new int[] { 0 });

        /// <summary>
        /// Method arguments are always classified as ExternVariable
        /// </summary>
        public static readonly VariabilityInfo DefaultArgumentInit =
            new VariabilityInfo(EVariability.ExternVariable, new int[] { 0 });
    }

    /// <summary>
    /// Classifies stack states into those having a unique successor (e.g. arithmetic/logic instructions, unconditions jumps) 
    /// and those who don't (e.g. conditional jumps).
    /// </summary>
    interface IUniqueSuccessorInfo
    {
        /// <summary>
        /// Tells whether current element has a unique successor or not.
        /// </summary>
        bool HasUniqueSuccessor { get; }
    }

    /// <summary>
    /// Augments a stack state with unique successor information
    /// </summary>
    class ModifyUniqueSuccessorStackState :
        DependentStackState<VariabilityInfo>,
        IUniqueSuccessorInfo
    {
        public bool HasUniqueSuccessor { get; private set; }

        /// <summary>
        /// Constructs a new stack state based on an existing state
        /// </summary>
        /// <param name="pre">existing state</param>
        /// <param name="isUnique">whether that state has a unique successor</param>
        public ModifyUniqueSuccessorStackState(AbstractStackState<VariabilityInfo> pre, bool isUnique) :
            base(pre)
        {
            HasUniqueSuccessor = isUnique;
        }
    }

    static class StackStateOfVariabilityExtensions
    {
        /// <summary>
        /// Tells whether a given stack state has a unique successor
        /// </summary>
        /// <param name="state">stack state</param>
        /// <returns>whether stack state has a unique successor</returns>
        public static bool HasUniqueSuccessor(this AbstractStackState<VariabilityInfo> state)
        {
            IUniqueSuccessorInfo vstate = state as IUniqueSuccessorInfo;
            while (vstate == null)
            {
                DependentStackState<VariabilityInfo> dstate = (DependentStackState<VariabilityInfo>)state;
                state = dstate.Pre;
                vstate = state as IUniqueSuccessorInfo;
            }
            return vstate.HasUniqueSuccessor;
        }

        /// <summary>
        /// Constructs a stack state indicating a unique successor, based on an existing stat6e
        /// </summary>
        /// <param name="state">a stack state</param>
        /// <returns>the desired stack state</returns>
        public static AbstractStackState<VariabilityInfo> UniqueSuccessor(this AbstractStackState<VariabilityInfo> state)
        {
            if (!HasUniqueSuccessor(state))
                return new ModifyUniqueSuccessorStackState(state, true);
            else
                return state;
        }

        /// <summary>
        /// Constructs a stack state indicating a non-unique successor, based on an existing state
        /// </summary>
        /// <param name="state">a stack state</param>
        /// <returns>the desired stack state</returns>
        public static AbstractStackState<VariabilityInfo> AmbiguousSuccessor(this AbstractStackState<VariabilityInfo> state)
        {
            if (HasUniqueSuccessor(state))
                return new ModifyUniqueSuccessorStackState(state, false);
            else
                return state;
        }
    }

    /// <summary>
    /// A debugging aid: Any method tagged with this attribute will cause the analysis stage to trigger a breakpoint just before variability analysis.
    /// </summary>
    public class BreakOnVariabilityAnalysis: Attribute
    {
    }

    /// <summary>
    /// Represents a sequence of variability classifications
    /// </summary>
    public class VariabilityPattern
    {
        /// <summary>
        /// The empty sequence
        /// </summary>
        public static readonly VariabilityPattern NoArgs = new VariabilityPattern(new EVariability[0]);

        /// <summary>
        /// Creates an instance which represents the arguments of a given method. I.e. each argument is assument to be ExternVariable.
        /// </summary>
        /// <param name="method">A method</param>
        /// <returns>an instance representing the supplied method</returns>
        public static VariabilityPattern CreateDefault(MethodBase method)
        {
            var pis = method.GetParameters();
            var pat = Enumerable.Repeat(EVariability.ExternVariable, pis.Length).ToArray();
            return new VariabilityPattern(pat);
        }

        /// <summary>
        /// The sequence of variabilities being represented
        /// </summary>
        public EVariability[] Pattern { get; private set; }

        /// <summary>
        /// Constructs a new instance based on a supplied sequence
        /// </summary>
        /// <param name="pattern"></param>
        public VariabilityPattern(EVariability[] pattern)
        {
            Pattern = pattern;
        }

        /// <summary>
        /// Two instances are defined to be equal iff the represent exactly the same sequence.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            var other = obj as VariabilityPattern;
            if (other == null)
                return false;
            return Pattern.SequenceEqual(other.Pattern);
        }

        public override int GetHashCode()
        {
            return Pattern.GetSequenceHashCode();
        }
    }

    /// <summary>
    /// This class analyzes the variabilities of stack elements and local variables of a given method.
    /// </summary>
    public class VariabilityAnalyzer : FixPointAnalyzer<VariabilityInfo>
    {
        private class IndependentStackState :
            IndependentStackStateBase<VariabilityInfo>,
            IUniqueSuccessorInfo
        {
            internal IndependentStackState(MethodBase method, FieldInfo[] localizedFields)
            {
                _stack = new List<VariabilityInfo>();
                int numLocals = method.GetMethodBody().LocalVariables.Count;
                numLocals += localizedFields.Length;
                _locals = Enumerable.Repeat(VariabilityInfo.DefaultLocalInit, numLocals).ToList();
                ParameterInfo[] arguments = method.GetParameters();
                bool hasThis = method.CallingConvention.HasFlag(CallingConventions.HasThis);
                IEnumerable<VariabilityInfo> args = Enumerable.Repeat(
                    VariabilityInfo.DefaultArgumentInit,
                    numLocals + (hasThis ? 1 : 0)).ToList();
                _args = args.ToList();
            }

            internal IndependentStackState(List<VariabilityInfo> stack, 
                List<VariabilityInfo> locals, List<VariabilityInfo> args,
                bool hasUniqueSuccessor)
            {
                _stack = stack;
                _locals = locals;
                _args = args;
                HasUniqueSuccessor = hasUniqueSuccessor;
            }

            public bool HasUniqueSuccessor { get; private set; }
            public EVariability EntryVariability { get; private set; }
        }

        private VariabilityPattern _callPattern;
        private int _localizedFieldsBaseIndex;
        private FieldInfo[] _localizedFields;

        /// <summary>
        /// Constructs a new analysis algorithm based on a supplied method and a sequence of variabilities which is assumed to hold for the method arguments.
        /// </summary>
        /// <param name="method">a method</param>
        /// <param name="callPattern">the assumed variabilities of the method arguments</param>
        public VariabilityAnalyzer(MethodBase method, VariabilityPattern callPattern) :
            base(method)
        {
            _callPattern = callPattern;
            if (Attribute.IsDefined(method.DeclaringType, typeof(CompilerGeneratedAttribute)))
                _localizedFields = method.DeclaringType.GetFields();
            else
                _localizedFields = new FieldInfo[0];
            _localizedFieldsBaseIndex = method.GetMethodBody().LocalVariables.Count;
            InitializePropagators();
        }

        protected override AbstractStackState<VariabilityInfo> CreateInitialStackState()
        {
            return new IndependentStackState(Method, _localizedFields);
        }

        public override void Run()
        {
            if (Attribute.IsDefined(Method, typeof(BreakOnVariabilityAnalysis)))
            {
                Debugger.Break();
            }

            base.Run();
        }

        private bool Merge(VariabilityInfo a, VariabilityInfo b, out VariabilityInfo m)
        {
            m = VariabilityInfo.MergeDefs(a, b);
            if (m.Variability == EVariability.Constant && m.Definitions.Count() > 1)
            {
                IEnumerable<int> lcas = CFG.GetLCASet(m.Definitions);
                IEnumerable<int> inter = m.Definitions.Intersect(lcas);
                if (inter.Any())
                {
                    m = new VariabilityInfo(EVariability.LocalVariable, m.Definitions);
                }
                else
                {
                    m = new VariabilityInfo(
                        lcas.All(i => _stackStates[i].HasUniqueSuccessor()) ?
                            EVariability.Constant : EVariability.LocalVariable,
                        m.Definitions);
                }
            }
            return !a.Equals(m);
        }

        private bool Merge(Func<int, VariabilityInfo> a, Func<int, VariabilityInfo> b, List<VariabilityInfo> m, int count)
        {
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                VariabilityInfo mi;
                if (Merge(a(i), b(i), out mi))
                    changed = true;
                m.Add(mi);
            }
            return changed;
        }

        protected override bool Merge(
            AbstractStackState<VariabilityInfo> a, 
            AbstractStackState<VariabilityInfo> b, 
            out AbstractStackState<VariabilityInfo> m)
        {
            List<VariabilityInfo> stack = new List<VariabilityInfo>();
            bool changed = Merge(i => a[i], i => b[i], stack, a.Depth);
            List<VariabilityInfo> locals = new List<VariabilityInfo>();
            if (Merge(i => a.GetLocal(i), i => b.GetLocal(i), locals, a.NumLocals))
                changed = true;
            List<VariabilityInfo> args = new List<VariabilityInfo>();
            if (Merge(i => a.GetArgument(i), i => b.GetArgument(i), args, a.NumArguments))
                changed = true;
            if (a.HasUniqueSuccessor() != b.HasUniqueSuccessor())
                changed = true;
            m = new IndependentStackState(stack, locals, args, 
                a.HasUniqueSuccessor() && b.HasUniqueSuccessor());
            return changed;
        }

        private AbstractStackState<VariabilityInfo> HandleBinOp(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            VariabilityInfo a = pre[0];
            VariabilityInfo b = pre[1];
            VariabilityInfo r = VariabilityInfo.MergeByNewDef(a, b, ili.Index);
            return pre.Pop().Pop().Push(r).UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleBinBranch(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            VariabilityInfo a = pre[0];
            VariabilityInfo b = pre[1];
            var next = pre.Pop().Pop();
            if (VariabilityOperations.Stronger(a.Variability, b.Variability) == EVariability.Constant)
                next = next.UniqueSuccessor();
            else
                next = next.AmbiguousSuccessor();
            return next;
        }

        private AbstractStackState<VariabilityInfo> HandleUnBranch(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            VariabilityInfo c = pre[0];
            var next = pre.Pop();
            if (c.Variability == EVariability.Constant)
                next = next.UniqueSuccessor();
            else
                next = next.AmbiguousSuccessor();
            return next;
        }

        private AbstractStackState<VariabilityInfo> Pop1(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre.Pop().UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> Pop2(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre.Pop().Pop().UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> Pop3(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre.Pop().Pop().Pop().UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> PushV(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre.Push(VariabilityInfo.CreateBySingleDef(EVariability.ExternVariable, ili.Index)).UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> PushC(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre.Push(VariabilityInfo.CreateBySingleDef(EVariability.Constant, ili.Index)).UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> Nop(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre.UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> UpdateStackState(ILInstruction ili, VariabilityInfo newVar, AbstractStackState<VariabilityInfo> cur)
        {
            MethodFacts myFacts = FactUniverse.Instance.GetFacts(Method);
            foreach (LocalMutation lm in myFacts.GetLocalMutations(ili.Index))
            {
                IModifiesStackState mss = lm as IModifiesStackState;
                if (mss != null)
                {
                    cur = mss.ModifyStackState(cur, newVar, VariabilityInfo.MergeDefs);
                }
            }
            return cur;
        }

        private AbstractStackState<VariabilityInfo> HandleCall(ILInstruction ili, AbstractStackState<VariabilityInfo> pre, bool isCalli)
        {
            MethodBase callee = (MethodBase)ili.Operand;
            bool hasThis = callee.CallingConvention.HasFlag(CallingConventions.HasThis);
            EVariability callVar = EVariability.Constant;
            ParameterInfo[] args = callee.GetParameters();
            MethodFacts myFacts = FactUniverse.Instance.GetFacts(Method);
            MethodFacts calleeFacts = FactUniverse.Instance.GetFacts(callee);
            AbstractStackState<VariabilityInfo> next = pre;
            if (hasThis)
            {
                callVar = VariabilityOperations.Stronger(callVar, pre[0].Variability);
                next = pre.Pop();
            }
            for (int i = 0; i < args.Length; i++)
            {
                callVar = VariabilityOperations.Stronger(callVar, next[0].Variability);
                next = next.Pop();
            }
            if (!calleeFacts.IsSideEffectFree)
                callVar = EVariability.ExternVariable;
            if (calleeFacts.IsStaticEvaluation)
                callVar = EVariability.Constant;
            if (isCalli)
                next = pre.Pop();
            VariabilityInfo callVarI = VariabilityInfo.CreateBySingleDef(callVar, ili.Index);
            next = UpdateStackState(ili, callVarI, next);
            Type returnType;
            if (callee.IsFunction(out returnType))
                next = next.Push(callVarI);
            return next.UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleCpblk(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            VariabilityInfo numBytes = pre[0];
            VariabilityInfo source = pre[1];
            VariabilityInfo dest = pre[2];
            AbstractStackState<VariabilityInfo> next = UpdateStackState(ili, source, pre);
            next = next.Pop().Pop().Pop();
            return next.UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleCpobj(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            VariabilityInfo source = pre[0];
            VariabilityInfo dest = pre[1];
            AbstractStackState<VariabilityInfo> next = UpdateStackState(ili, source, pre);
            next = next.Pop().Pop();
            return next.UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleDup(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre.Push(pre[0]).UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleLdArg(ILInstruction ili, int narg, AbstractStackState<VariabilityInfo> pre)
        {
            MethodFacts myFacts = FactUniverse.Instance.GetFacts(Method);
            EVariability varia;
            if (Method.CallingConvention.HasFlag(CallingConventions.HasThis))
            {
                if (narg == 0)
                    varia = EVariability.Constant;
                else
                    varia = _callPattern.Pattern[narg - 1];
            }
            else
            {
                varia = _callPattern.Pattern[narg];
            }
            return pre.Push(VariabilityInfo.CreateBySingleDef(varia, ili.Index)).UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleLdelem(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return PushV(ili, pre.Pop().Pop());
        }

        private AbstractStackState<VariabilityInfo> HandleLdfld(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            FieldInfo field = (FieldInfo)ili.Operand;
            int index = Array.IndexOf(_localizedFields, field);
            if (index >= 0)
            {
                return HandleLdloc(null, pre.Pop(), _localizedFieldsBaseIndex + index);
            }
            else
            {
                FieldFacts fieldFacts = FactUniverse.Instance.GetFacts(field);
                EVariability fieldVar = fieldFacts.IsWritten || fieldFacts.IsSubMutated ?
                    EVariability.ExternVariable : EVariability.Constant;
                VariabilityInfo newVar = VariabilityInfo.CreateBySingleDef(fieldVar, ili.Index);
                return pre
                    .Pop()
                    .Push(newVar)
                    .UniqueSuccessor();
            }
        }

        private AbstractStackState<VariabilityInfo> HandleLdind(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return PushV(ili, pre.Pop());
        }

        private AbstractStackState<VariabilityInfo> HandleLdloc(ILInstruction ili, AbstractStackState<VariabilityInfo> pre, int index)
        {
            Contract.Requires<ArgumentNullException>(pre != null);
            Contract.Requires<ArgumentException>(index >= 0);

            return pre.Push(pre.GetLocal(index)).UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleLdsfld(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            FieldInfo field = (FieldInfo)ili.Operand;
            FieldFacts fieldFacts = FactUniverse.Instance.GetFacts(field);
            EVariability fieldVar = fieldFacts.IsWritten || fieldFacts.IsSubMutated ?
                EVariability.ExternVariable : EVariability.Constant;
            VariabilityInfo newVar = VariabilityInfo.CreateBySingleDef(fieldVar, ili.Index);
            return pre
                .Push(newVar)
                .UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleNewarr(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            return pre
                .Pop()
                .Push(VariabilityInfo.CreateBySingleDef(EVariability.ExternVariable, ili.Index))
                .UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleNewobj(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            ConstructorInfo ctor = (ConstructorInfo)ili.Operand;
            ParameterInfo[] args = ctor.GetParameters();
            AbstractStackState<VariabilityInfo> next = pre;
            for (int i = 0; i < args.Length; i++)
                next = next.Pop();
            return next
                .Push(VariabilityInfo.CreateBySingleDef(EVariability.ExternVariable, ili.Index))
                .UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleRet(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            Type returnType;
            if (Method.IsFunction(out returnType))
                return pre.Pop().UniqueSuccessor();
            else
                return pre.UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleStarg(ILInstruction ili, AbstractStackState<VariabilityInfo> pre, int index)
        {
            return pre.AssignArg(index, pre[0]).Pop().UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleStind(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            VariabilityInfo value = pre[0];
            VariabilityInfo address = pre[1];
            return UpdateStackState(ili, value, pre).Pop().Pop().UniqueSuccessor();
        }

        private AbstractStackState<VariabilityInfo> HandleStfld(ILInstruction ili, AbstractStackState<VariabilityInfo> pre)
        {
            var field = (FieldInfo)ili.Operand;
            int index = Array.IndexOf(_localizedFields, field);
            if (index < 0)
            {
                return Pop2(ili, pre);
            }
            else
            {
                VariabilityInfo value = pre[0];
                return pre.Assign(index + _localizedFieldsBaseIndex, value).Pop().Pop().UniqueSuccessor();
            }
        }

        private AbstractStackState<VariabilityInfo> HandleStloc(ILInstruction ili, AbstractStackState<VariabilityInfo> pre, int index)
        {
            VariabilityInfo value = pre[0];
            return pre.Assign(index, value).Pop().UniqueSuccessor();
        }

        private void InitializePropagators()
        {
            _pmap[OpCodes.Add] = HandleBinOp;
            _pmap[OpCodes.Add_Ovf] = HandleBinOp;
            _pmap[OpCodes.Add_Ovf_Un] = HandleBinOp;
            _pmap[OpCodes.And] = HandleBinOp;
            _pmap[OpCodes.Arglist] = PushV;
            _pmap[OpCodes.Beq] = HandleBinBranch;
            _pmap[OpCodes.Beq_S] = HandleBinBranch;
            _pmap[OpCodes.Bge] = HandleBinBranch;
            _pmap[OpCodes.Bge_S] = HandleBinBranch;
            _pmap[OpCodes.Bge_Un] = HandleBinBranch;
            _pmap[OpCodes.Bge_Un_S] = HandleBinBranch;
            _pmap[OpCodes.Beq] = HandleBinBranch;
            _pmap[OpCodes.Bgt] = HandleBinBranch;
            _pmap[OpCodes.Bgt_S] = HandleBinBranch;
            _pmap[OpCodes.Bgt_Un] = HandleBinBranch;
            _pmap[OpCodes.Bgt_Un_S] = HandleBinBranch;
            _pmap[OpCodes.Ble] = HandleBinBranch;
            _pmap[OpCodes.Ble_S] = HandleBinBranch;
            _pmap[OpCodes.Ble_Un] = HandleBinBranch;
            _pmap[OpCodes.Ble_Un_S] = HandleBinBranch;
            _pmap[OpCodes.Blt] = HandleBinBranch;
            _pmap[OpCodes.Blt_S] = HandleBinBranch;
            _pmap[OpCodes.Blt_Un] = HandleBinBranch;
            _pmap[OpCodes.Blt_Un_S] = HandleBinBranch;
            _pmap[OpCodes.Bne_Un] = HandleBinBranch;
            _pmap[OpCodes.Bne_Un_S] = HandleBinBranch;
            _pmap[OpCodes.Box] = Nop;
            _pmap[OpCodes.Br] = Nop;
            _pmap[OpCodes.Br_S] = Nop;
            _pmap[OpCodes.Break] = Nop;
            _pmap[OpCodes.Brfalse] = HandleUnBranch;
            _pmap[OpCodes.Brfalse_S] = HandleUnBranch;
            _pmap[OpCodes.Brtrue] = HandleUnBranch;
            _pmap[OpCodes.Brtrue_S] = HandleUnBranch;
            _pmap[OpCodes.Call] = (i, s) => HandleCall(i, s, false);
            _pmap[OpCodes.Calli] = (i, s) => HandleCall(i, s, true);
            _pmap[OpCodes.Callvirt] = (i, s) => HandleCall(i, s, false);
            _pmap[OpCodes.Castclass] = Nop;
            _pmap[OpCodes.Ceq] = HandleBinOp;
            _pmap[OpCodes.Cgt] = HandleBinOp;
            _pmap[OpCodes.Cgt_Un] = HandleBinOp;
            _pmap[OpCodes.Ckfinite] = Nop;
            _pmap[OpCodes.Clt] = HandleBinOp;
            _pmap[OpCodes.Clt_Un] = HandleBinOp;
            _pmap[OpCodes.Constrained] = Nop;
            _pmap[OpCodes.Conv_I] = Nop;
            _pmap[OpCodes.Conv_I1] = Nop;
            _pmap[OpCodes.Conv_I2] = Nop;
            _pmap[OpCodes.Conv_I4] = Nop;
            _pmap[OpCodes.Conv_I8] = Nop;
            _pmap[OpCodes.Conv_Ovf_I] = Nop;
            _pmap[OpCodes.Conv_Ovf_I_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_I1] = Nop;
            _pmap[OpCodes.Conv_Ovf_I1_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_I2] = Nop;
            _pmap[OpCodes.Conv_Ovf_I2_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_I4] = Nop;
            _pmap[OpCodes.Conv_Ovf_I4_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_I8] = Nop;
            _pmap[OpCodes.Conv_Ovf_I8_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_U] = Nop;
            _pmap[OpCodes.Conv_Ovf_U_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_U1] = Nop;
            _pmap[OpCodes.Conv_Ovf_U1_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_U2] = Nop;
            _pmap[OpCodes.Conv_Ovf_U2_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_U4] = Nop;
            _pmap[OpCodes.Conv_Ovf_U4_Un] = Nop;
            _pmap[OpCodes.Conv_Ovf_U8] = Nop;
            _pmap[OpCodes.Conv_Ovf_U8_Un] = Nop;
            _pmap[OpCodes.Conv_R_Un] = Nop;
            _pmap[OpCodes.Conv_R4] = Nop;
            _pmap[OpCodes.Conv_R8] = Nop;
            _pmap[OpCodes.Conv_U] = Nop;
            _pmap[OpCodes.Conv_U1] = Nop;
            _pmap[OpCodes.Conv_U2] = Nop;
            _pmap[OpCodes.Conv_U4] = Nop;
            _pmap[OpCodes.Conv_U8] = Nop;
            _pmap[OpCodes.Cpblk] = HandleCpblk;
            _pmap[OpCodes.Cpobj] = HandleCpobj;
            _pmap[OpCodes.Div] = HandleBinOp;
            _pmap[OpCodes.Div_Un] = HandleBinOp;
            _pmap[OpCodes.Dup] = HandleDup;
            _pmap[OpCodes.Endfilter] = Pop1;
            _pmap[OpCodes.Endfinally] = Nop;
            _pmap[OpCodes.Initblk] = Pop3;
            _pmap[OpCodes.Initobj] = Pop1;
            _pmap[OpCodes.Isinst] = Nop;
            _pmap[OpCodes.Jmp] = Nop;
            _pmap[OpCodes.Ldarg] = (ili, pre) => HandleLdArg(ili, (int)ili.Operand, pre);
            _pmap[OpCodes.Ldarg_0] = (ili, pre) => HandleLdArg(ili, 0, pre);
            _pmap[OpCodes.Ldarg_1] = (ili, pre) => HandleLdArg(ili, 1, pre);
            _pmap[OpCodes.Ldarg_2] = (ili, pre) => HandleLdArg(ili, 2, pre);
            _pmap[OpCodes.Ldarg_3] = (ili, pre) => HandleLdArg(ili, 3, pre);
            _pmap[OpCodes.Ldarg_S] = (ili, pre) => HandleLdArg(ili, (byte)ili.Operand, pre);
            _pmap[OpCodes.Ldarga] = PushV;
            _pmap[OpCodes.Ldarga_S] = PushV;
            _pmap[OpCodes.Ldc_I4] = PushC;
            _pmap[OpCodes.Ldc_I4_0] = PushC;
            _pmap[OpCodes.Ldc_I4_1] = PushC;
            _pmap[OpCodes.Ldc_I4_2] = PushC;
            _pmap[OpCodes.Ldc_I4_3] = PushC;
            _pmap[OpCodes.Ldc_I4_4] = PushC;
            _pmap[OpCodes.Ldc_I4_5] = PushC;
            _pmap[OpCodes.Ldc_I4_6] = PushC;
            _pmap[OpCodes.Ldc_I4_7] = PushC;
            _pmap[OpCodes.Ldc_I4_8] = PushC;
            _pmap[OpCodes.Ldc_I4_M1] = PushC;
            _pmap[OpCodes.Ldc_I4_S] = PushC;
            _pmap[OpCodes.Ldc_I8] = PushC;
            _pmap[OpCodes.Ldc_R4] = PushC;
            _pmap[OpCodes.Ldc_R8] = PushC;
            _pmap[OpCodes.Ldelem] = HandleLdelem;
            _pmap[OpCodes.Ldelem_I] = HandleLdelem;
            _pmap[OpCodes.Ldelem_I1] = HandleLdelem;
            _pmap[OpCodes.Ldelem_I2] = HandleLdelem;
            _pmap[OpCodes.Ldelem_I4] = HandleLdelem;
            _pmap[OpCodes.Ldelem_I8] = HandleLdelem;
            _pmap[OpCodes.Ldelem_R4] = HandleLdelem;
            _pmap[OpCodes.Ldelem_R8] = HandleLdelem;
            _pmap[OpCodes.Ldelem_Ref] = HandleLdelem;
            _pmap[OpCodes.Ldelem_U1] = HandleLdelem;
            _pmap[OpCodes.Ldelem_U2] = HandleLdelem;
            _pmap[OpCodes.Ldelem_U4] = HandleLdelem;
            _pmap[OpCodes.Ldelema] = HandleLdelem;
            _pmap[OpCodes.Ldfld] = HandleLdfld;
            _pmap[OpCodes.Ldflda] = HandleLdfld;
            _pmap[OpCodes.Ldftn] = PushV;
            _pmap[OpCodes.Ldind_I] = HandleLdind;
            _pmap[OpCodes.Ldind_I1] = HandleLdind;
            _pmap[OpCodes.Ldind_I2] = HandleLdind;
            _pmap[OpCodes.Ldind_I4] = HandleLdind;
            _pmap[OpCodes.Ldind_I8] = HandleLdind;
            _pmap[OpCodes.Ldind_R4] = HandleLdind;
            _pmap[OpCodes.Ldind_R8] = HandleLdind;
            _pmap[OpCodes.Ldind_Ref] = HandleLdind;
            _pmap[OpCodes.Ldind_U1] = HandleLdind;
            _pmap[OpCodes.Ldind_U2] = HandleLdind;
            _pmap[OpCodes.Ldind_U4] = HandleLdind;
            _pmap[OpCodes.Ldlen] = Nop;
            _pmap[OpCodes.Ldloc] = (i, p) => HandleLdloc(i, p, (int)i.Operand);
            _pmap[OpCodes.Ldloc_0] = (i, p) => HandleLdloc(i, p, 0);
            _pmap[OpCodes.Ldloc_1] = (i, p) => HandleLdloc(i, p, 1);
            _pmap[OpCodes.Ldloc_2] = (i, p) => HandleLdloc(i, p, 2);
            _pmap[OpCodes.Ldloc_3] = (i, p) => HandleLdloc(i, p, 3);
            _pmap[OpCodes.Ldloc_S] = (i, p) => HandleLdloc(i, p, (byte)i.Operand);
            _pmap[OpCodes.Ldloca] = (i, p) => HandleLdloc(i, p, (int)i.Operand);
            _pmap[OpCodes.Ldloca_S] = (i, p) => HandleLdloc(i, p, (byte)i.Operand);
            _pmap[OpCodes.Ldnull] = PushC;
            _pmap[OpCodes.Ldobj] = HandleLdind;
            _pmap[OpCodes.Ldsfld] = HandleLdsfld;
            _pmap[OpCodes.Ldsflda] = HandleLdsfld;
            _pmap[OpCodes.Ldstr] = (i, p) => PushC(i, p);
            _pmap[OpCodes.Ldtoken] = PushV;
            _pmap[OpCodes.Ldvirtftn] = Nop;
            _pmap[OpCodes.Leave] = Nop;
            _pmap[OpCodes.Leave_S] = Nop;
            _pmap[OpCodes.Localloc] = Nop;
            _pmap[OpCodes.Mkrefany] = Nop;
            _pmap[OpCodes.Mul] = HandleBinOp;
            _pmap[OpCodes.Mul_Ovf] = HandleBinOp;
            _pmap[OpCodes.Mul_Ovf_Un] = HandleBinOp;
            _pmap[OpCodes.Neg] = Nop;
            _pmap[OpCodes.Newarr] = HandleNewarr;
            _pmap[OpCodes.Newobj] = HandleNewobj;
            _pmap[OpCodes.Nop] = Nop;
            _pmap[OpCodes.Not] = Nop;
            _pmap[OpCodes.Or] = HandleBinOp;
            _pmap[OpCodes.Pop] = Pop1;
            _pmap[OpCodes.Prefix1] = Nop;
            _pmap[OpCodes.Prefix2] = Nop;
            _pmap[OpCodes.Prefix3] = Nop;
            _pmap[OpCodes.Prefix4] = Nop;
            _pmap[OpCodes.Prefix5] = Nop;
            _pmap[OpCodes.Prefix6] = Nop;
            _pmap[OpCodes.Prefix7] = Nop;
            _pmap[OpCodes.Prefixref] = Nop;
            _pmap[OpCodes.Readonly] = Nop;
            _pmap[OpCodes.Refanytype] = Nop;
            _pmap[OpCodes.Refanyval] = Nop;
            _pmap[OpCodes.Rem] = HandleBinOp;
            _pmap[OpCodes.Rem_Un] = HandleBinOp;
            _pmap[OpCodes.Ret] = HandleRet;
            _pmap[OpCodes.Rethrow] = Nop;
            _pmap[OpCodes.Shl] = HandleBinOp;
            _pmap[OpCodes.Shr] = HandleBinOp;
            _pmap[OpCodes.Shr_Un] = HandleBinOp;
            _pmap[OpCodes.Sizeof] = PushC;
            _pmap[OpCodes.Starg] = (i, p) => HandleStarg(i, p, (int)i.Operand);
            _pmap[OpCodes.Starg_S] = (i, p) => HandleStarg(i, p, (byte)i.Operand);
            _pmap[OpCodes.Stelem] = Pop3;
            _pmap[OpCodes.Stelem_I] = Pop3;
            _pmap[OpCodes.Stelem_I1] = Pop3;
            _pmap[OpCodes.Stelem_I2] = Pop3;
            _pmap[OpCodes.Stelem_I4] = Pop3;
            _pmap[OpCodes.Stelem_I8] = Pop3;
            _pmap[OpCodes.Stelem_R4] = Pop3;
            _pmap[OpCodes.Stelem_R8] = Pop3;
            _pmap[OpCodes.Stelem_Ref] = Pop3;
            _pmap[OpCodes.Stfld] = HandleStfld;
            _pmap[OpCodes.Stind_I] = HandleStind;
            _pmap[OpCodes.Stind_I1] = HandleStind;
            _pmap[OpCodes.Stind_I2] = HandleStind;
            _pmap[OpCodes.Stind_I4] = HandleStind;
            _pmap[OpCodes.Stind_I8] = HandleStind;
            _pmap[OpCodes.Stind_R4] = HandleStind;
            _pmap[OpCodes.Stind_R8] = HandleStind;
            _pmap[OpCodes.Stind_Ref] = HandleStind;
            _pmap[OpCodes.Stloc] = (i, p) => HandleStloc(i, p, (int)i.Operand);
            _pmap[OpCodes.Stloc_0] = (i, p) => HandleStloc(i, p, 0);
            _pmap[OpCodes.Stloc_1] = (i, p) => HandleStloc(i, p, 1);
            _pmap[OpCodes.Stloc_2] = (i, p) => HandleStloc(i, p, 2);
            _pmap[OpCodes.Stloc_3] = (i, p) => HandleStloc(i, p, 3);
            _pmap[OpCodes.Stloc_S] = (i, p) => HandleStloc(i, p, (byte)i.Operand);
            _pmap[OpCodes.Stobj] = HandleStind;
            _pmap[OpCodes.Stsfld] = Pop1;
            _pmap[OpCodes.Sub] = HandleBinOp;
            _pmap[OpCodes.Sub_Ovf] = HandleBinOp;
            _pmap[OpCodes.Sub_Ovf_Un] = HandleBinOp;
            _pmap[OpCodes.Switch] = HandleUnBranch;
            _pmap[OpCodes.Tailcall] = Nop;
            _pmap[OpCodes.Throw] = Pop1;
            _pmap[OpCodes.Unaligned] = Nop;
            _pmap[OpCodes.Unbox] = Nop;
            _pmap[OpCodes.Unbox_Any] = Nop;
            _pmap[OpCodes.Volatile] = Nop;
            _pmap[OpCodes.Xor] = HandleBinOp;
        }

        /// <summary>
        /// Retrieves the variability of a stack element at a given instruction index and stack index
        /// </summary>
        /// <param name="ilIndex">index of CIL instruction</param>
        /// <param name="stackDepth">index of stack element (0 is top)</param>
        /// <returns>variability classification of desired stack element</returns>
        public EVariability GetStackElementVariability(int ilIndex, int stackDepth)
        {
            return _stackStates[ilIndex][stackDepth].Variability;
        }

        /// <summary>
        /// Retrieves the variability of a local variable at a given instruction index
        /// </summary>
        /// <param name="ilIndex">index of CIL instruction</param>
        /// <param name="localIndex">index of local variable</param>
        /// <returns>variability of desired variable at desired CIL index</returns>
        public EVariability GetLocalVariableVariability(int ilIndex, int localIndex)
        {
            return _stackStates[ilIndex].GetLocal(localIndex).Variability;
        }
    }
}

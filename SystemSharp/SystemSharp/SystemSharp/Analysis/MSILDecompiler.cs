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
 * 2011-08-15 CK -fixed CheckDetainAssignment() to handle multiple program flow predecessors
 *               -fixed hash code computation in LocalVariableState
 *               -fixed bug in HandleLdLoc when address of local variable is loaded
 *               -new feature: unrolling along certain local variables can be selectively disabled
 * 2011-08-16 CK -various fixes
 *               -OpCodes.Pop support (bogus!)
 * 2011-09-13 CK -modified HandleDup(): no variable allocation for simple expressions              
 * 2011-09-27 CK -enabled RewriteCall attribute for constructors
 *               -rewrite conditionals with boolean type from (f ? a : b) to ((f && a) || (!f && b))
 * 2011-10-01 CK -rework of MethodCallInfo handling
 * 2011-10-26 CK -fixed handling of Ldobj instruction
 * 2011-12-10 CK -removed any code relying on post dominators
 * 2011-12-13 CK -removed any references to the type library. New concept: type dependencies are 
 *                computed during post-processing
 * 2011-12-16 CK -fixed issue in HandleStLoc: value supposed to be stored is now converted to type
 *                of the local variable before it is stored inside _localVarState
 * 2011-12-19 CK -added proper conversions in HandleCondBranchIf in case of non-boolean decision value
 *               -fixed issue in ImplementSpilloverIf: condition was not updated after decompilation of 
 *                branches
 *               -fixed control flow reconstruction issue
 * 2012-01-29 CK -added ImplementIf, no dependency from AccumulatedStackBilance (seems to be bogus...)
 * 2012-02-12 CK -fixed GetTypeOf to account for bool
 *               -removed some assertions which don't seem to affect correct decompilation.
 *               -fixed OnStoreField to handle int constants assigned to bool
 * 2012-02-13 CK -fixed GetFieldDescriptor to handle boolean values correctly
 *               -re-activated variability analysis in CheckDetainAssignment
 * 2012-02-14 CK -fixed HandleDup() in case of an address is on the stack top
 * 2012-02-20 CK -new feature: IL indices are stored as attributes of SysDOM expressions/statements
 * 2012-02-27 CK -bugfix: successive loop block were not recognized correctly
 * 2012-02-29 CK -bugfix: re-ordered statements inside ImplementBranch. Otherwise, some block might be doubled.
 * 2012-03-02 CK -bugfix: ImplementBranch did not nest BBs inside switch statements
 *               -added support for enumeration-typed switch conditions
 * 2012-03-03 CK -bugfix: Switch targets were not resolved correctly
 * 2012-03-16 CK -bugfix: type inference for local variables was broken in some cases
 * 2012-04-12 CK -fixed TryGetReturnValueSample to return false if null is specified for a value-type parameter
 * 2012-04-13 CK -TryGetReturnValueSample is now called even if SideEffectFree attribute is not set
 * 2012-04-15 CK -changed implementation of GetFieldDescriptor: now managed and canonicalized inside FieldFacts
 * 2012-04-16 CK -fixed UnrollAt to detect unroll markers inside do-while loop headers
 *               -fixed DeclareAlgorithm to upgrade types of input/output variables accordingly
 * 2012-04-17 CK -fixed OnStoreField: boolean samples need to be converted from int to bool prior to UpgradeType
 * 2012-04-19 CK -added support generic and personalized (for inlining) decompilations
 * 2012-04-20 CK -removed Instance property from FieldRef, since instance is part of FieldDescriptor
 * 2012-04-22 CK -added driving process support for FieldDescriptor
 * 2012-04-25 CK -fixed recognition of switch statement successors
 *               -fixed upgrading of byref types
 * 2012-05-05 CK -MakeReturnValueSample will not invoke target method if IDoNotCallOnDecompilation attribute is present
 * 2012-05-16 CK -fixed issue with incorrectly structured loops
 * 2012-06-07 CK -fixed DeclareBlock: loop exit recognition did not work for do-style loops
 * 2012-07-24 CK -fixed DeclareBlock: If branch was incorrectly pushed on nextStack, such that branch was not nested
 * 2012-07-26 CK -fixed conditional execution handling in case of compile-time branch conditions (gives unconditional target)
 * 2012-08-23 CK -account for new FieldDescriptor/CILFieldDescriptor distinction
 * 2013-01-06 CK -UpgradeType inside OnStLoc
 *               -allowed RewriteFieldAccess attribute for FieldInfo
 * 2013-01-08 CK -removed restriction "and mode is by address" for LiteralReference inside HandleDup
 * 2013-01-09 CK -simplified implementation of If statements, added supported for initobj
 * 2013-01-30 CK -do not ignore local variables of reference type of they have intrinsic type override
 * 2013-04-11 CK -fixed issue where no statement was generated when "ret" appears somewhere "in the middle"
 *               -fixed issue where else-branch of "if" statement with single dominator was not implemented
 * 2013-05-17 CK -feature: drop local variables with an "empty" type, such as StdLogicVector of size 0
 * 2013-07-24 CK -cosmetic improvement of OnStLoc
 * 2013-08-12 CK -OnStLoc fix: based decision whether to skip assignment on variable type rather than rhs type (because 'null' might be assigned)
 * 2013-08-13 CK -OnStLoc fix: changed behavior again - decision now based on either IllegalRuntimeType or rhs sample being 'null'
 *               -modified OnLoad[Static]Field, OnStore[Static]Field not to add field references if field is of IllegalRuntimeType
 * */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using SDILReader;
using SystemSharp.Algebraic;
using SystemSharp.Analysis.Msil;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Eval;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.Analysis
{
    public interface IOnDecompilation
    {
        void OnDecompilation(MSILDecompilerTemplate decomp);
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = true, AllowMultiple = true)]
    public class BreakOnDecompilation : Attribute, IOnDecompilation
    {
        #region IOnDecompilation Member

        public void OnDecompilation(MSILDecompilerTemplate decomp)
        {
            System.Diagnostics.Debugger.Break();
        }

        #endregion

        private class BreakOnCall : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                System.Diagnostics.Debugger.Break();
                return true;
            }
        }

        [BreakOnCall]
        public static void StopHere()
        {
        }
    }

    public class MSILFunctionRef : ICallable
    {
        public MethodBase Method { get; private set; }

        public MSILFunctionRef(MethodBase method)
        {
            Method = method;
        }

        public string Name
        {
            get { return Method.Name; }
        }

        public override string ToString()
        {
            return "msil:" + Name;
        }
    }

    public class MethodCallInfo
    {
        public FunctionSpec FunSpec { get; private set; }

        public MethodBase Method
        {
            get { return FunSpec.CILRep; }
        }

        public Expression[] Arguments { get; private set; }
        public EVariability[] ArgumentVariabilities { get; private set; }
        public object Instance { get; private set; }
        public EVariability InstanceVariability { get; private set; }
        public MSILDecompilerTemplate CallerTemplate { get; private set; }
        public object ResultSample { get; private set; }

        public MethodCallInfo(FunctionSpec funSpec,
            object instance, EVariability instanceVariability,
            Expression[] args, EVariability[] argVar,
            MSILDecompilerTemplate callerTemplate,
            object resultSample)
        {
            Contract.Requires(funSpec != null && args != null &&
                args.All(a => a != null));

            FunSpec = funSpec;
            Instance = instance;
            InstanceVariability = instanceVariability;
            Arguments = args;
            ArgumentVariabilities = argVar;
            CallerTemplate = callerTemplate;
            ResultSample = resultSample;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (Method.IsStatic)
            {
                sb.Append(Method.Name);
                sb.Append("(");
                sb.Append(string.Join<Expression>(", ", Arguments));
                sb.Append(")");
            }
            else
            {
                sb.Append(Arguments[0]);
                sb.Append("[");
                if (Instance != null &&
                    InstanceVariability == EVariability.Constant)
                {
                    sb.Append(Instance.ToString());
                }
                else
                {
                    sb.Append("?");
                }
                sb.Append("]");
                sb.Append(".");
                sb.Append(Method.Name);
                sb.Append("(");
                sb.Append(string.Join<Expression>(", ", Arguments.Skip(1)));
                sb.Append(")");
            }
            return sb.ToString();
        }

        public object[] EvaluatedArguments
        {
            get
            {
                return Arguments
                    .Select(a => a.ResultType.GetSampleInstance(ETypeCreationOptions.ReturnNullIfUnavailable))
                    .ToArray();
            }
        }

        public Expression CalledInstance
        {
            get
            {
                if (Method.IsStatic)
                    return null;
                else
                    return Arguments[0];
            }
        }

        public void Resolve(MethodDescriptor genericImpl, MethodDescriptor specialImpl)
        {
            Contract.Requires(genericImpl != null);
            Contract.Requires(specialImpl != null);

            FunSpec.GenericSysDOMRep = genericImpl;
            FunSpec.SpecialSysDOMRep = specialImpl;
        }

        public object[] EvaluatedArgumentsWithoutThis
        {
            get
            {
                if (Method.IsStatic)
                {
                    return EvaluatedArguments;
                }
                else
                {
                    object[] result = new object[EvaluatedArguments.Length - 1];
                    Array.Copy(EvaluatedArguments, 1, result, 0, result.Length);
                    return result;
                }
            }
        }

        public bool IsInstanceDetermined
        {
            get
            {
                return !Method.IsStatic &&
                    Instance != null &&
                    InstanceVariability == EVariability.Constant;
            }
        }

        public MethodBase GetStrongestOverride()
        {
            if (IsInstanceDetermined && Method.IsVirtual)
            {
                var seq = Instance.GetType().GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public)
                    .SelectMany(m => m.GetAncestorDefinitions());
                var sel = seq.Where(m => m.Name.Equals(Method.Name));
                var bases = sel.Select(m => m.GetBaseDefinition());

                var ovmethod = Instance.GetType().GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public)
                    .Where(m => m.GetAncestorDefinitions().Contains(Method))
                    .Single();

                if (Method.DeclaringType.IsAssignableFrom(ovmethod.DeclaringType))
                    return ovmethod;
                else
                    return Method;
            }
            else
            {
                return Method;
            }
        }

        public void Inherit(MSILDecompilerTemplate templ)
        {
            templ.DisallowConditionals = CallerTemplate.DisallowConditionals;
            templ.TryToEliminateLoops = CallerTemplate.TryToEliminateLoops;
            templ.CopyAttributesFrom(CallerTemplate);
        }
    }

    public class FieldRefInfo
    {
        public FieldInfo Field { get; private set; }
        public bool IsRead { get; internal set; }
        public bool IsWritten { get; internal set; }
        public StackElement Instance { get; private set; }
        public List<StackElement> RHSs { get; private set; }

        public FieldRefInfo(FieldInfo field, StackElement instance)
        {
            if (field == null)
                throw new ArgumentException("field null");

            Field = field;
            Instance = instance;
            RHSs = new List<StackElement>();
        }

        public override string ToString()
        {
            string result = Instance.ToString() + ".";
            result += Field.Name + "{";
            if (IsRead)
                result += "r";
            if (IsWritten)
                result += "w";
            return result;
        }

        public override bool Equals(object obj)
        {
            if (obj is FieldRefInfo)
            {
                FieldRefInfo fri = (FieldRefInfo)obj;
                return Field.Equals(fri.Field) &&
                    Instance.Equals(fri.Instance);
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return Field.GetHashCode() ^ Instance.GetHashCode();
        }

        public void Merge(FieldRefInfo other)
        {
            Contract.Requires(other.RHSs != null);

            if (!Equals(other))
                throw new InvalidOperationException("Only equal objects of FieldRefInfo may be merged");

            RHSs.AddRange(other.RHSs);
        }

        TypeDescriptor _type;

        public TypeDescriptor Type
        {
            get
            {
                if (_type == null)
                {
                    object sample = RHSs.Where(se => se.Sample != null).Select(se => se.Sample).FirstOrDefault();
                    if (sample != null)
                        _type = TypeDescriptor.GetTypeOf(sample);
                    else
                        _type = (TypeDescriptor)Field.FieldType;
                }
                return _type;
            }
        }

        private bool _haveValueSample;
        private object _valueSample;

        public object ValueSample
        {
            get
            {
                if (_haveValueSample)
                    return _valueSample;

                return Field.GetValue(Instance.Sample);
            }
            set
            {
                _valueSample = value;
                _haveValueSample = true;
            }
        }
    }

    public class AssignmentInfo
    {
        /// <summary>
        /// The index of the IL instruction
        /// </summary>
        public int ILIndex { get; private set; }

        /// <summary>
        /// The byte offset of the IL instruction
        /// </summary>
        /// 
        public int ILOffset { get; private set; }

        /// <summary>
        /// The assignment target ("left hand side")
        /// </summary>
        public Variable LHS { get; private set; }

        /// <summary>
        /// The assigned expression ("right hand side")
        /// </summary>
        public Expression RHS { get; private set; }

        /// <summary>
        /// An assigned value sample (may be null)
        /// </summary>
        public object RHSSample { get; private set; }

        public AssignmentInfo(int ilIndex, int ilOffset, Variable lhs, Expression rhs, object rhsSample)
        {
            ILIndex = ilIndex;
            ILOffset = ilOffset;
            LHS = lhs;
            RHS = rhs;
            RHSSample = rhsSample;
        }
    }

    public struct StackElement
    {
        public Expression Expr;
        public object Sample;
        public EVariability Variability;

        public StackElement(Expression expr, object sample, EVariability variability)
        {
            Expr = expr;
            Sample = sample;
            Variability = variability;
        }

        public override string ToString()
        {
            return Expr.ToString() + " <" + (Sample ?? "?") + ">";
        }

        public override bool Equals(object obj)
        {
            if (obj is StackElement)
            {
                StackElement other = (StackElement)obj;
                return object.Equals(Expr, other.Expr) &&
                    object.Equals(Sample, other.Sample);
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            if (Expr != null)
                hash = Expr.GetHashCode();
            if (Sample != null)
                hash ^= (3 * Sample.GetHashCode());
            return hash;
        }
    }

    [ContractClass(typeof(DecompilerContractClass))]
    public interface IDecompiler: IAttributed
    {
        StackElement Pop();
        void Push(StackElement elem);
        void Push(Expression expr, object sample);

        /// <summary>
        /// Checks whether a given expression refers to a variable and returns the variable's content
        /// if the variable is uniquely assigned at the specified position in the IL code. If the expression
        /// does not refer to a variable or if the assignment is not unique, the expression itself is returned.
        /// </summary>
        /// <param name="ilIndex">the index of the current IL instruction</param>
        /// <param name="expr">an expression</param>
        /// <returns>either the resolution or the supplied expression.</returns>
        Expression ResolveVariableReference(int ilIndex, Expression expr);

        /// <summary>
        /// Returns the index of the current IL instruction
        /// </summary>
        int CurrentILIndex { get; }

        /// <summary>
        /// Determines the expression representing the call of a specified method.
        /// </summary>
        /// <param name="method">The moethod to call</param>
        /// <param name="args">The method arguments (if the method is non-static, the first argument must be the object on which the method is called)</param>
        /// <returns>The expression and (if possible) a return value sample which represents the method call</returns>
        StackElement GetCallExpression(MethodInfo method, params StackElement[] args);

        /// <summary>
        /// Implements a method call to the specified method mb.
        /// </summary>
        /// <param name="mb">The method to be called</param>
        /// <param name="args">Argument list</param>
        void ImplementCall(MethodBase mb, params StackElement[] args);

        void HideLocal(LocalVariableInfo lvi);
        void DoNotUnroll(int localIndex);
        bool TryGetReturnValueSample(MethodInfo callee, StackElement[] inArgs, out object[] outArgs, out object result);

        /// <summary>
        /// Prevents loop unrolling at given loop header
        /// </summary>
        /// <param name="ilIndex">IL index of loop header</param>
        void DoNotUnrollAt(int ilIndex);

        /// <summary>
        /// Enforces loop unrolling at given loop header
        /// </summary>
        /// <param name="ilIndex">IL index of loop header</param>
        void DoUnrollAt(int ilIndex);
    }

    [ContractClassFor(typeof(IDecompiler))]
    abstract class DecompilerContractClass : IDecompiler
    {
        public StackElement Pop()
        {
            throw new NotImplementedException();
        }

        public void Push(StackElement elem)
        {
            Contract.Requires<ArgumentException>(elem.Expr != null && elem.Expr.ResultType != null);
            throw new NotImplementedException();
        }

        public void Push(Expression expr, object sample)
        {
            Contract.Requires<ArgumentNullException>(expr != null);
            throw new NotImplementedException();
        }

        public Expression ResolveVariableReference(int ilIndex, Expression expr)
        {
            Contract.Requires<ArgumentNullException>(expr != null);
            Contract.Ensures(Contract.Result<Expression>() != null);
            throw new NotImplementedException();
        }

        public int CurrentILIndex
        {
            get { throw new NotImplementedException(); }
        }

        public StackElement GetCallExpression(MethodInfo method, params StackElement[] args)
        {
            Contract.Requires<ArgumentNullException>(method != null);
            Contract.Requires<ArgumentNullException>(args != null);
            throw new NotImplementedException();
        }

        public void ImplementCall(MethodBase mb, params StackElement[] args)
        {
            Contract.Requires<ArgumentNullException>(mb != null);
            Contract.Requires<ArgumentNullException>(args != null);
            throw new NotImplementedException();
        }

        public void HideLocal(LocalVariableInfo lvi)
        {
            Contract.Requires<ArgumentNullException>(lvi != null);
            throw new NotImplementedException();
        }

        public void DoNotUnroll(int localIndex)
        {
            throw new NotImplementedException();
        }

        public bool TryGetReturnValueSample(MethodInfo callee, StackElement[] inArgs, out object[] outArgs, out object result)
        {
            Contract.Requires<ArgumentNullException>(callee != null);
            Contract.Requires<ArgumentNullException>(inArgs != null);
            throw new NotImplementedException();
        }

        public void DoNotUnrollAt(int ilIndex)
        {
            throw new NotImplementedException();
        }

        public void DoUnrollAt(int ilIndex)
        {
            throw new NotImplementedException();
        }

        public void AddAttribute(object attr)
        {
            throw new NotImplementedException();
        }

        public bool RemoveAttribute<T>()
        {
            throw new NotImplementedException();
        }

        public T QueryAttribute<T>()
        {
            throw new NotImplementedException();
        }

        public bool HasAttribute<T>()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> Attributes
        {
            get { throw new NotImplementedException(); }
        }

        public void CopyAttributesFrom(IAttributed other)
        {
            throw new NotImplementedException();
        }
    }

    public static class DecompilerStacks
    {
        class PrivateDecompilerStack : 
            AttributedObject,
            IDecompiler
        {
            private Stack<StackElement> _stack = new Stack<StackElement>();
            private IDecompiler _root;

            public PrivateDecompilerStack(IDecompiler root)
            {
                _root = root;
            }

            public StackElement Pop()
            {
                return _stack.Pop();
            }

            public void Push(StackElement elem)
            {
                _stack.Push(elem);
            }

            public void Push(Expression expr, object sample)
            {
                _stack.Push(new StackElement(expr, sample, EVariability.ExternVariable));
            }

            public Expression ResolveVariableReference(int ilIndex, Expression expr)
            {
                return _root.ResolveVariableReference(ilIndex, expr);
            }

            public int CurrentILIndex
            {
                get { return _root.CurrentILIndex; }
            }

            public StackElement GetCallExpression(MethodInfo method, params StackElement[] args)
            {
                return _root.GetCallExpression(method, args);
            }

            public void ImplementCall(MethodBase mb, params StackElement[] args)
            {
                throw new NotImplementedException();
            }

            public void HideLocal(LocalVariableInfo lvi)
            {
                throw new NotImplementedException();
            }

            public void DoNotUnroll(int localIndex)
            {
                throw new NotImplementedException();
            }

            public bool TryGetReturnValueSample(MethodInfo callee, StackElement[] inArgs, out object[] outArgs, out object result)
            {
                throw new NotImplementedException();
            }

            public void DoNotUnrollAt(int ilIndex)
            {
                throw new NotImplementedException();
            }

            public void DoUnrollAt(int ilIndex)
            {
                throw new NotImplementedException();
            }
        }

        public static IDecompiler CreatePrivateStack(this IDecompiler stack)
        {
            return new PrivateDecompilerStack(stack);
        }
    }

    public class LocalVariableState
    {
        public static readonly LocalVariableState Empty = new LocalVariableState();

        private Dictionary<int, object> _state = new Dictionary<int, object>();
        private int _hash;

        internal LocalVariableState(IDictionary<int, object> state, ISet<int> doNotUnroll)
        {
            foreach (var kvp in state)
            {
                if (doNotUnroll.Contains(kvp.Key))
                    continue;

                _state[kvp.Key] = kvp.Value;
                _hash ^= kvp.Key;
                if (kvp.Value != null)
                    _hash ^= kvp.Value.GetHashCode();
                _hash = (int)(((uint)_hash << 1) | ((uint)_hash >> 31));
            }
        }

        internal LocalVariableState()
        {
            _hash = 0;
        }

        internal Dictionary<int, object> State
        {
            get { return _state; }
        }

        public override bool Equals(object obj)
        {
            LocalVariableState other = obj as LocalVariableState;
            if (other == null)
                return false;
            if (!_state.Keys.OrderBy(k => k)
                .SequenceEqual(other.State.Keys.OrderBy(k => k)))
                return false;
            foreach (var kvp in _state)
            {
                if (!object.Equals(_state[kvp.Key], (other._state[kvp.Key])))
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        public override string ToString()
        {
            return string.Join(", ", _state.Select(kvp => "l" + kvp.Key + "=" + kvp.Value));
        }
    }

    public class ILIndexRef : IComparable<ILIndexRef>
    {
        public enum EComparisonResult
        {
            Equal,
            Less,
            Greater,
            Incomparable
        }

        public MethodBase Method { get; private set; }
        public int ILIndex { get; private set; }
        public ILIndexRef Caller { get; internal set; }

        public ILIndexRef(MethodBase method, int ilIndex)
        {
            Contract.Requires(method != null);

            Method = method;
            ILIndex = ilIndex;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ILIndexRef;
            if (other == null)
                return false;
            return Method.Equals(other.Method) &&
                ILIndex == other.ILIndex;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode() ^ ILIndex;
        }

        public override string ToString()
        {
            string result = ILIndex + "@" + Method.Name;
            if (Caller != null)
                result = result + " <- " + Caller.ToString();
            return result;
        }

        public int Depth
        {
            get
            {
                int depth = 0;
                ILIndexRef cur = this;
                while (cur != null)
                {
                    cur = cur.Caller;
                    depth++;
                }
                return depth;
            }
        }

        public EComparisonResult TryCompareTo(ILIndexRef other)
        {
            int mydepth = Depth;
            int itsdepth = other.Depth;
            ILIndexRef mycur = this;
            ILIndexRef itscur = other;
            while (!mycur.Method.Equals(itscur.Method))
            {
                if (mydepth > itsdepth)
                {
                    mycur = mycur.Caller;
                    --mydepth;
                    if (mycur == null)
                        return EComparisonResult.Incomparable;
                }
                else
                {
                    itscur = itscur.Caller;
                    --itsdepth;
                    if (itscur == null)
                        return EComparisonResult.Incomparable;
                }
            }
            if (ILIndex > other.ILIndex)
                return EComparisonResult.Greater;
            else if (ILIndex < other.ILIndex)
                return EComparisonResult.Less;
            else
                return EComparisonResult.Equal;
        }

        public int CompareTo(ILIndexRef other)
        {
            switch (TryCompareTo(other))
            {
                case EComparisonResult.Equal:
                    return 0;
                case EComparisonResult.Greater:
                    return 1;
                case EComparisonResult.Incomparable:
                    return 0;
                case EComparisonResult.Less:
                    return -1;
                default:
                    throw new NotImplementedException();
            }
        }

        public static bool operator >(ILIndexRef a, ILIndexRef b)
        {
            if (a == null || b == null)
                return false;
            return a.TryCompareTo(b) == EComparisonResult.Greater;
        }

        public static bool operator >=(ILIndexRef a, ILIndexRef b)
        {
            if (a == null || b == null)
                return false;
            var cr = a.TryCompareTo(b);
            return cr == EComparisonResult.Greater || cr == EComparisonResult.Equal;
        }

        public static bool operator <=(ILIndexRef a, ILIndexRef b)
        {
            if (a == null || b == null)
                return false;
            var cr = a.TryCompareTo(b);
            return cr == EComparisonResult.Less || cr == EComparisonResult.Equal;
        }

        public static bool operator <(ILIndexRef a, ILIndexRef b)
        {
            if (a == null || b == null)
                return false;
            return a.TryCompareTo(b) == EComparisonResult.Less;
        }
    }

    public class MSILDecompilerTemplate :
        AlgorithmTemplate,
        IDecompiler
    {
        private delegate void Handler();
        private delegate Expression SpecialOpHandler(Expression[] args, TypeDescriptor rtype);

        private enum EBranchImpl
        {
            Nested,
            ControlStatement,
            Empty,
            Deferred
        }

        private class IfInfo
        {
            public Expression Condition;
            public object ConditionSample;
            public MSILCodeBlock ConditionBlock;
            public MSILCodeBlock ThenTarget;
            public MSILCodeBlock ElseTarget;

            // If condition evaluates to a constant at compile-time,
            // this member will be set.
            public MSILCodeBlock UnconditionalTarget;

            public bool IsConditional;
        }

        private class LoopInfo
        {
            public MSILCodeBlock Header;
            public LoopBlock Loop;
        }

        private struct SwitchInfo
        {
            public MSILCodeBlock Condition;
            public MSILCodeBlock[] SwitchTargets;
            public CaseStatement Case;
            public long SelOffset;
        }

        private struct BreakInfo
        {
            public Statement Stmt;
            public MSILCodeBlock BreakNext;
        }

        protected class LocVarInfo
        {
            public Variable Var;
            public Func<bool> IsReferenced;
        }

        private Dictionary<string, SpecialOpHandler> _specialOpHandlers =
            new Dictionary<string, SpecialOpHandler>();

        private void InitSpecialOpHandlers()
        {
            _specialOpHandlers["op_Implicit"] = (x, t) => IntrinsicFunctions.Cast(x[0], x[0].ResultType.CILType, t);
            _specialOpHandlers["op_explicit"] = (x, t) => IntrinsicFunctions.Cast(x[0], x[0].ResultType.CILType, t);
            _specialOpHandlers["op_Addition"] = (x, t) => x[0] + x[1];
            _specialOpHandlers["op_Subtraction"] = (x, t) => x[0] - x[1];
            _specialOpHandlers["op_Multiply"] = (x, t) => x[0] * x[1];
            _specialOpHandlers["op_Division"] = (x, t) => x[0] / x[1];
            _specialOpHandlers["op_Modulus"] = (x, t) => x[0] % x[1];
            _specialOpHandlers["op_ExclusiveOr"] = (x, t) => x[0] ^ x[1];
            _specialOpHandlers["op_BitwiseAnd"] = (x, t) => x[0] & x[1];
            _specialOpHandlers["op_BitwiseOr"] = (x, t) => x[0] | x[1];
            _specialOpHandlers["op_LogicalAnd"] = (x, t) => x[0] & x[1];
            _specialOpHandlers["op_LogicalOr"] = (x, t) => x[0] | x[1];
            _specialOpHandlers["op_LeftShift"] = (x, t) => Expression.LShift(x[0], x[1]);
            _specialOpHandlers["op_RightShift"] = (x, t) => Expression.RShift(x[0], x[1]);
            _specialOpHandlers["op_SignedRightShift"] = (x, t) => Expression.RShift(x[0], x[1]);
            _specialOpHandlers["op_UnsignedRightShift"] = (x, t) => Expression.RShift(x[0], x[1]);
            _specialOpHandlers["op_Equality"] = (x, t) => Expression.Equal(x[0], x[1]);
            _specialOpHandlers["op_GreaterThan"] = (x, t) => Expression.GreaterThan(x[0], x[1]);
            _specialOpHandlers["op_LessThan"] = (x, t) => Expression.LessThan(x[0], x[1]);
            _specialOpHandlers["op_Inequality"] = (x, t) => Expression.NotEqual(x[0], x[1]);
            _specialOpHandlers["op_GreaterThanOrEqual"] = (x, t) => Expression.GreaterThanOrEqual(x[0], x[1]);
            _specialOpHandlers["op_LessThanOrEqual"] = (x, t) => Expression.LessThanOrEqual(x[0], x[1]);
            _specialOpHandlers["op_UnaryNegation"] = (x, t) => -x[0];
            _specialOpHandlers["op_OnesComplement"] = (x, t) => ~x[0];
            _specialOpHandlers["op_LogicalNot"] = (x, t) => !x[0];
        }

        Dictionary<OpCode, Handler> _hdlMap = new Dictionary<OpCode, Handler>();
        private CodeDescriptor _decompilee;
        private MethodBase _method;
        private MethodBody _body;
        private MethodCode _code;
        private MSILCodeBlock _entry;
        private Literal[] _arglist;
        private LiteralReference _thisRef;
        private Dictionary<string, IStorableLiteral> _argmap = new Dictionary<string, IStorableLiteral>();
        private Dictionary<Tuple<int, int>, LocVarInfo> _locmap = new Dictionary<Tuple<int, int>, LocVarInfo>();
        private HashSet<int> _hiddenLocals = new HashSet<int>();

        /// <summary>
        /// The currently decompiled basic block
        /// </summary>
        private MSILCodeBlock _curBB;

        private Stack<MSILCodeBlock> _nextStack = new Stack<MSILCodeBlock>();
        private Stack<StackElement> _estk = new Stack<StackElement>();
        private ILInstruction _curILI;
        private bool _curILIValid;
        private int _beginOfLastStatement;
        private Stack<LoopInfo> _loopStack = new Stack<LoopInfo>();
        private Stack<SwitchInfo> _switchStack = new Stack<SwitchInfo>();
        private Stack<BreakInfo> _breakStack = new Stack<BreakInfo>();
        private Stack<ILInstruction> _forkStack = new Stack<ILInstruction>();
        private Stack<IfInfo> _ifStack = new Stack<IfInfo>();

        /// <summary>
        /// Maps IL indices to the appropriate statement.
        /// </summary>
        private Dictionary<int, Statement> _stmtMap = new Dictionary<int, Statement>();

        private List<KeyValuePair<GotoStatement, int>> _gotoTargets =
            new List<KeyValuePair<GotoStatement, int>>();
        private List<MethodCallInfo> _calledMethods = new List<MethodCallInfo>();
        private Dictionary<FieldRefInfo, FieldRefInfo> _referencedFields =
            new Dictionary<FieldRefInfo, FieldRefInfo>();
        private List<AssignmentInfo> _varAsmts = new List<AssignmentInfo>();
        private List<StackElement> _retAsmts = new List<StackElement>();

        /// <summary>
        /// Holds the expressions in store which are intended as replacements for local variables references.
        /// </summary>
        /// <remarks>
        /// This field is used in conjunction with the elimination of local variables.
        /// </remarks>
        /// <seealso cref="DataflowAnalyzer"/>
        private Dictionary<int, StackElement> _readPointExprs = new Dictionary<int, StackElement>();

        private DefaultEvaluator _eval = new DefaultEvaluator();
        private Dictionary<int, object> _localVarState = new Dictionary<int, object>();
        private HashSet<int> _doNotUnrollVars = new HashSet<int>();
        private Dictionary<int, bool> _unrollHeaders = new Dictionary<int, bool>();

        private HashSet<int> _processedIndices = new HashSet<int>();

        public MSILDecompilerTemplate()
        {
            InitHandlerMap();
            _eval.DoEvalVariable = EvaluateVariable;
            //_eval.DoEvalSignalRef = x => { throw new BreakEvaluationException(); };
            //FIXME: override _eval.DoEvalFieldRef to consider only constant fields
            MaxUnrollDepth = 100;
        }

        public CodeDescriptor Decompilee
        {
            get { return _decompilee; }
            internal set { _decompilee = value; }
        }

        public MethodBase Method
        {
            get { return _method; }
            internal set { _method = value; }
        }

        public MethodCode Code
        {
            get { return _code; }
            set
            {
                _code = value;
                _entry = _code.EntryCB;
            }
        }

        public Type MethodReturnType
        {
            get
            {
                if (_method is MethodInfo)
                {
                    MethodInfo mi = (MethodInfo)_method;
                    return mi.ReturnType;
                }
                else
                    return typeof(void);
            }
        }

        public bool TreatReturnValueAsVariable { get; set; }
        public bool DisallowReturnStatements { get; set; }
        public bool GenerateThisVariable { get; set; }
        public bool NestLoopsDeeply { get; set; }
        public bool TryToEliminateLoops { get; set; }
        public int MaxUnrollDepth { get; set; }
        public bool DisallowConditionals { get; set; }
        public bool DisallowLoops { get; set; }

        public object Instance { get; internal set; }
        public object[] ArgumentValues { get; internal set; }
        public EVariability[] ArgumentVariabilities { get; internal set; }

        public TypeLibrary TypeLib { get; private set; }

        protected override string FunctionName
        {
            get { return _decompilee.Name; }
        }

        private HashSet<Variable> _evalSet = new HashSet<Variable>();

        private object EvaluateVariable(Variable variable)
        {
            object result;
            if (_localVarState.TryGetValue(variable.LocalIndex, out result))
                return result;
            else
                throw new BreakEvaluationException();
        }

        private MSILCodeBlock skipNOPs(MSILCodeBlock cb)
        {
            if (cb.StartIndex == cb.EndIndex &&
                _code.Instructions[cb.StartIndex].Code.Equals(OpCodes.Nop))
                return _code.GetBasicBlockStartingAt(cb.StartIndex + 1);
            else
                return cb;
        }

        private static bool IsNopOrBranch(OpCode opcode)
        {
            return (opcode.Equals(OpCodes.Nop) ||
                opcode.FlowControl == FlowControl.Branch ||
                opcode.FlowControl == FlowControl.Return);
        }

        private MSILCodeBlock TrackBranchChain(MSILCodeBlock cb)
        {
            while (cb.StartIndex == cb.EndIndex &&
                IsNopOrBranch(_code.Instructions[cb.StartIndex].Code) &&
                cb.Successors.Length == 1)
            {
                cb = cb.Successors.Single();
            }
            return cb;
        }

        private EBranchImpl ImplementBranch(MSILCodeBlock targetBlock)
        {
            MSILCodeBlock altTarget = TrackBranchChain(targetBlock);

            if (_switchStack.Count > 0)
            {
                SwitchInfo si = _switchStack.Peek();
                int index = Array.IndexOf(si.SwitchTargets, targetBlock);
                if (index >= 0)
                {
                    if (si.Condition == _curBB)
                    {
                        MSILCodeBlock cur = _curBB;
                        DeclareBlock(targetBlock);
                        _curBB = cur;
                        return EBranchImpl.Nested;
                    }
                    else
                    {
                        GotoCase(si.Case, index);
                        return EBranchImpl.ControlStatement;
                    }
                }
            }

            var loopHeaders = _loopStack.Where(li => li.Header == targetBlock);
            if (loopHeaders.Any())
            {
                LoopInfo li = loopHeaders.First();
                Continue(li.Loop);
                return EBranchImpl.ControlStatement;
            }

            foreach (BreakInfo bi in _breakStack)
            {
                if (TrackBranchChain(bi.BreakNext) == altTarget)
                {
                    if (bi.Stmt is LoopBlock)
                    {
                        // FIXME: Actually covered by code above
                        Break((LoopBlock)bi.Stmt);
                    }
                    else
                    {
                        Break((CaseStatement)bi.Stmt);
                    }
                    return EBranchImpl.ControlStatement;
                }
            }

            if (_nextStack.Any() &&
                _nextStack.Peek() == targetBlock)
            {
                // We're inside an if statement, target block follows that statement
                return EBranchImpl.Empty;
            }

            if (_curBB.Dominatees.Contains(targetBlock))
            {
                // The only way to reach targetBlock is by _curBB
                // => implementation can be nested.
                MSILCodeBlock cur = _curBB;
                DeclareBlock(targetBlock);
                _curBB = cur;
                return EBranchImpl.Nested;
            }

            if (targetBlock.IsLoop)
            {
                // Must be an unrolled loop. Otherwise, a "continue"
                // statement would have been generated.
                targetBlock.UnrollDepth++;
                MSILCodeBlock cur = _curBB;
                DeclareBlock(targetBlock);
                _curBB = cur;
                targetBlock.UnrollDepth--;
                return EBranchImpl.Nested;
            }

            if (_curBB.Successors.Contains(targetBlock))
            {
                // Natural control flow out of the current into the next block
                return EBranchImpl.Empty;
            }

            GotoStatement stmt = Goto();
            _gotoTargets.Add(new KeyValuePair<GotoStatement, int>(stmt, targetBlock.StartIndex));
            return EBranchImpl.ControlStatement;
        }

        public void Push(StackElement se)
        {
            Type exprType = se.Expr.ResultType.CILType;
            if (se.Sample != null && se.Expr.ResultType != null &&
                !exprType.IsPointer && !exprType.IsByRef &&
                !exprType.IsAssignableFrom(se.Sample.GetType()))
                throw new ArgumentException();
            if (_beginOfLastStatement < 0)
                _beginOfLastStatement = _curILI.Index;
            se.Expr.AddAttribute(new ILIndexRef(Method, CurrentILIndex));
            _estk.Push(se);
        }

        public void Push(Expression e, object sample)
        {
            EVariability var = _curILIValid ?
                VARA.GetStackElementVariability(_curILI.Index, 0) :
                EVariability.ExternVariable;
            if (sample != null &&
                var == EVariability.Constant &&
                !(e is LiteralReference))
            {
                Push(new StackElement(
                    LiteralReference.CreateConstant(sample),
                    sample, var));
            }
            else
            {
                if (sample != null)
                    e.ResultType = TypeDescriptor.GetTypeOf(sample);
                Push(new StackElement(e, sample, var));
            }
        }

        public StackElement Pop()
        {
            return _estk.Pop();
        }

        public StackElement Peek()
        {
            return _estk.Peek();
        }

        public override void Call(ICallable callee, Expression[] arguments)
        {
            base.Call(callee, arguments);
            var lrs = arguments
                .Select(e => e as LiteralReference)
                .Where(e => e != null);
            foreach (var lr in lrs)
            {
                ReferenceLocalsInLiteralReference(lr, () => true);
            }
            RememberBeginOfLastStatement();
        }

        public override void ElseIf(Expression cond)
        {
            base.ElseIf(cond);
            RememberBeginOfLastStatement();
        }

        public override IfStatement If(Expression cond)
        {
            if (!cond.ResultType.CILType.IsPrimitive)
                throw new InvalidOperationException();

            IfStatement stmt = base.If(cond);
            RememberBeginOfLastStatement(stmt);
            return stmt;
        }

        public override void Return()
        {
            if (DisallowReturnStatements)
            {
                ImplementBranch(_code.BasicBlocks.Last());
            }
            else
            {
                base.Return();
            }
        }

        public override void Solve(EquationSystem eqsys)
        {
            base.Solve(eqsys);
            RememberBeginOfLastStatement();
        }

        private void ReferenceLocal(Variable v, Func<bool> fn)
        {
            if (v.LocalIndex < 0)
                return;

            LocVarInfo lvi = _locmap[Tuple.Create(v.LocalIndex, v.LocalSubIndex)];
            Func<bool> ppr = lvi.IsReferenced;
            lvi.IsReferenced = () => ppr() || fn();
        }

        private void ReferenceLocalsInLiteralReference(LiteralReference lr, Func<bool> fn)
        {
            Variable v = lr.ReferencedObject as Variable;
            ArrayRef ar = lr.ReferencedObject as ArrayRef;
            if (v != null)
            {
                ReferenceLocal(v, fn);
            }
            else if (ar != null)
            {
                ReferenceLocalsInArrayRef(ar, fn);
            }
        }

        private void ReferenceLocalsInArrayRef(ArrayRef ar, Func<bool> fn)
        {
            LiteralReference lr = ar.ArrayExpr as LiteralReference;
            if (lr != null)
                ReferenceLocalsInLiteralReference(lr, fn);
        }

        public override void Store(IStorableLiteral var, Expression val)
        {
            base.Store(var, val);
            Func<bool> pr = () => !LastStatement.IsEliminated && !val.IsInlined;
            Variable v = var as Variable;
            ArrayRef ar = var as ArrayRef;
            if (v != null)
            {
                ReferenceLocal(v, pr);
            }
            else if (ar != null)
            {
                ReferenceLocalsInArrayRef(ar, pr);
            }
            RememberBeginOfLastStatement();
        }

        public override CaseStatement Switch(Expression selector)
        {
            CaseStatement stmt = base.Switch(selector);
            RememberBeginOfLastStatement(stmt);
            return stmt;
        }

        public override void Throw(Expression expr)
        {
            base.Throw(expr);
            RememberBeginOfLastStatement();
        }

        protected void RememberBeginOfLastStatement(Statement stmt)
        {
            // Expression stack should be empty
            // Debug.Assert(_estk.Count == 0);
            // ...should, but found some code which didn't leave the stack empty prior to branches.
            //Debug.Assert(_beginOfLastStatement >= 0);
            int begin = _beginOfLastStatement;
            if (begin < 0)
                begin = _curILI.Index;
            int prev = begin - 1;
            if (prev >= 0 && _code.Instructions[prev].Code.Equals(OpCodes.Nop))
            {
                --begin;
            }
            _stmtMap[begin] = stmt;
            if (_code.Instructions[begin].Code.Equals(OpCodes.Nop))
            {
                _stmtMap[begin + 1] = stmt;
            }
            stmt.AddAttribute(new ILIndexRef(Method, begin));
            _beginOfLastStatement = -1;
        }

        protected void RememberBeginOfLastStatement()
        {
            Statement stmt = LastStatement;
            RememberBeginOfLastStatement(stmt);
        }

        protected virtual void ReportError(string message)
        {
            Console.Error.WriteLine(message);
            //throw new InvalidOperationException(message);
        }

        private FieldRefInfo AddFieldReference(FieldInfo field, StackElement inst, StackElement? value, bool isWrite)
        {
            FieldRefInfo fri = new FieldRefInfo(field, inst);
            FieldRefInfo friv;
            _referencedFields.TryGetValue(fri, out friv);
            if (friv == null)
            {
                _referencedFields[fri] = fri;
            }
            else
            {
                fri = friv;
            }

            if (isWrite)
                fri.IsWritten = true;
            else
                fri.IsRead = true;

            if (value.HasValue)
                fri.RHSs.Add(value.Value);

            return fri;
        }

        private object MakeReturnValueSample(MethodInfo callee, StackElement[] args/*, bool assumeNoSideFx*/)
        {
            object result = null;
            object[] outArgs = null;
            bool haveResult = false;
            ParameterInfo[] pis = callee.GetParameters();
            Type returnType;
            bool isFunction = callee.ReturnsSomething(out returnType);
            bool callRelevant = isFunction || pis.Any(pi => pi.ParameterType.IsByRef);
            if (callRelevant && !callee.HasCustomOrInjectedAttribute<IDoNotCallOnDecompilation>())
            {
                if (TryGetReturnValueSample(callee, args, out outArgs, out result))
                {
                    haveResult = true;
                }
            }
            if (!haveResult && isFunction && returnType.IsValueType)
            {
                result = Activator.CreateInstance(callee.ReturnType);
            }
            bool hasThis = callee.CallingConvention.HasFlag(CallingConventions.HasThis);
            foreach (ParameterInfo pi in pis)
            {
                int pos = pi.Position;
                if (hasThis)
                    ++pos;
                LiteralReference lrarg = args[pos].Expr as LiteralReference;
                if (pi.ParameterType.IsByRef && lrarg != null)
                {
                    Variable varg = lrarg.ReferencedObject as Variable;
                    if (varg != null)
                    {
                        if (haveResult)
                        {
                            RememberStore(varg,
                                new StackElement(
                                    LiteralReference.CreateConstant(outArgs[pos]),
                                    args[pos].Sample, EVariability.Constant));
                        }
                        _localVarState[varg.LocalIndex] = haveResult ? outArgs[pos] : null;
                        if (haveResult && outArgs[pos] != null)
                        {
                            varg.UpgradeType(TypeDescriptor.GetTypeOf(outArgs[pos]));
                            args[pos].Sample = outArgs[pos];
                        }
                    }
                    FieldRef fref = lrarg.ReferencedObject as FieldRef;
                    if (fref != null)
                    {
                        var cfd = fref.FieldDesc as CILFieldDescriptor;
                        if (haveResult && outArgs[pos] != null)
                        {
                            fref.FieldDesc.UpgradeType(TypeDescriptor.GetTypeOf(outArgs[pos]));
                            if (cfd != null)
                                cfd.AddDriver(DesignContext.Instance.CurrentProcess);
                            fref.FieldDesc.IsConstant = false;
                        }
                        else
                        {
                            if (cfd != null)
                                cfd.AddReader(DesignContext.Instance.CurrentProcess);
                        }
                    }
                }
            }
            return result;
        }

        public StackElement GetCallExpression(MethodInfo method, params StackElement[] args)
        {
            Type returnType;
            if (!method.IsFunction(out returnType))
                throw new InvalidOperationException("GetCallExpression() is only allowed on methods returning a value");

            bool tmp = _curILIValid;
            _curILIValid = false;
            OnCall(method, args, returnType);
            _curILIValid = tmp;
            return Pop();
        }

        protected virtual void OnCall(MethodBase callee, StackElement[] args, Type returnType)
        {
            object rsample = MakeReturnValueSample((MethodInfo)callee, args);

            if (callee is MethodInfo &&
                !returnType.Equals(typeof(void)) &&
                FactUniverse.Instance.HaveFacts(callee) &&
                FactUniverse.Instance.GetFacts(callee).IsSideEffectFree &&
                args.All(arg => arg.Variability == EVariability.Constant))
            {
                // All arguments are constants
                Expression cex = LiteralReference.CreateConstant(rsample);
                Push(cex, rsample);
                return;
            }

            RewriteCall rwcall = null;
            if (callee.CallingConvention.HasFlag(CallingConventions.HasThis))
                rwcall = (RewriteCall)callee.GetCustomOrInjectedAttribute(args[0].Sample, typeof(RewriteCall));
            if (rwcall == null)
                rwcall = callee.GetCustomOrInjectedAttribute<RewriteCall>();
            if (rwcall == null && callee.IsGenericMethod && !callee.IsGenericMethodDefinition)
            {
                var mi = (MethodInfo)callee;
                var gencallee = mi.GetGenericMethodDefinition();
                if (gencallee.CallingConvention.HasFlag(CallingConventions.HasThis))
                    rwcall = (RewriteCall)gencallee.GetCustomOrInjectedAttribute(args[0].Sample, typeof(RewriteCall));
                if (rwcall == null)
                    rwcall = gencallee.GetCustomOrInjectedAttribute<RewriteCall>();
            }
            if (rwcall != null)
            {
                bool rw = rwcall.Rewrite(Decompilee, callee, args, this, this);
                if (rw)
                    return;
            }

            Expression[] eargs = args.Select(x => x.Expr).ToArray();
            object[] oargs = args.Select(x => x.Sample).ToArray();
            EVariability[] vargs = args.Select(x => x.Variability).ToArray();

            if (callee.IsSpecialName)
            {
                SpecialOpHandler handler;
                if (_specialOpHandlers.TryGetValue(callee.Name, out handler))
                {
                    object[] attrs = callee.GetCustomAndInjectedAttributes(typeof(AutoConversion));
                    AutoConversion aconv = null;
                    if (attrs.Length > 0)
                        aconv = (AutoConversion)attrs.Last();
                    if (aconv == null || aconv.Action == AutoConversion.EAction.Include)
                    {
                        TypeDescriptor tdRet;
                        if (rsample != null)
                            tdRet = TypeDescriptor.GetTypeOf(rsample);
                        else
                        {
                            Type tRet;
                            callee.IsFunction(out tRet);
                            tdRet = TypeDescriptor.MakeType(tRet);
                        }
                        Push(handler(eargs, tdRet), rsample);
                        return;
                    }
                }
            }

            FunctionSpec fref;
            if (returnType.Equals(typeof(void)))
            {
                fref = new FunctionSpec(typeof(void))
                {
                    CILRep = callee
                };
                Call(fref, eargs);
            }
            else
            {
                TypeDescriptor rtype;
                if (rsample != null)
                    rtype = TypeDescriptor.GetTypeOf(rsample);
                else
                    rtype = returnType;
                fref = new FunctionSpec(rtype)
                {
                    CILRep = callee
                };
                var fcall = new FunctionCall()
                {
                    Callee = fref,
                    Arguments = eargs,
                    ResultType = rtype,
                    SetResultTypeClass = EResultTypeClass.ObjectReference
                };
                Push(fcall, rsample);
                if (callee.IsAsync())
                {
                    var fork = IntrinsicFunctions.Fork(rsample);
                    var forkRef = new FunctionSpec(typeof(void))
                    {
                        IntrinsicRep = fork
                    };
                    Call(forkRef, new Expression[] { fcall });
                }
            }
            bool hasThis = callee.CallingConvention.HasFlag(CallingConventions.HasThis);
            MethodCallInfo mci = new MethodCallInfo(fref,
                hasThis ? args[0].Sample : null,
                hasThis ? args[0].Variability : EVariability.Constant,
                eargs, vargs.Skip(hasThis ? 1 : 0).ToArray(), this,
                rsample);
            _calledMethods.Add(mci);
        }

        private TypeDescriptor GetTypeOf(Type type, object sample)
        {
            if (sample != null)
            {
                object csample = TypeConversions.ConvertValue(sample, type);
                return TypeDescriptor.GetTypeOf(csample);
            }
            else
            {
                return (TypeDescriptor)type;
            }
        }

        private FieldDescriptor GetFieldDescriptor(FieldInfo field, object fieldInst, out bool isWritten)
        {
            var facts = FactUniverse.Instance.GetFacts(field);
            isWritten = facts.IsWritten;
            return facts.GetDescriptor(fieldInst);
        }

        protected virtual void OnLoadField(FieldInfo field, StackElement objref)
        {
            Type fieldType = field.FieldType;
            var rfa = fieldType.GetCustomOrInjectedAttribute<RewriteFieldAccess>();
            if (rfa == null)
            {
                rfa = field.GetCustomOrInjectedAttribute<RewriteFieldAccess>();
            }
            if (rfa == null)
            {
                bool isWritten;
                var fd = GetFieldDescriptor(field, objref.Sample, out isWritten) as CILFieldDescriptor;
                fd.AddReader(DesignContext.Instance.CurrentProcess);
                object sample = fd.ConstantValue;
                if ((isWritten || sample == null || !sample.GetType().IsValueType) &&
                    !(fd.Type.HasIntrinsicTypeOverride && fd.Type.IntrinsicTypeOverride == EIntrinsicTypes.IllegalRuntimeType))
                {
                    AddFieldReference(field, objref, null, false);
                    var fref = new FieldRef(fd);
                    Push(fref, sample);
                }
                else
                {
                    Push(LiteralReference.CreateConstant(sample), sample);
                }
            }
            else
            {
                rfa.RewriteRead(Decompilee, field, objref.Sample, this, this);
            }
        }

        protected virtual void OnStoreField(FieldInfo field, StackElement objref, StackElement value)
        {
            Type fieldType = field.FieldType;
            var rfa = field.GetCustomOrInjectedAttribute<RewriteFieldAccess>();
            if (rfa == null)
                rfa = fieldType.GetCustomOrInjectedAttribute<RewriteFieldAccess>();
            if (rfa == null)
            {
                object convSample = TypeConversions.ConvertValue(value.Sample, field.FieldType);
                bool isWritten;
                var fd = GetFieldDescriptor(field, objref.Sample, out isWritten) as CILFieldDescriptor;
                fd.IsConstant = false;
                if (value.Sample != null)
                {
                    fd.UpgradeType(TypeDescriptor.GetTypeOf(convSample));
                }
                fd.AddDriver(DesignContext.Instance.CurrentProcess);
                if (!fd.Type.HasIntrinsicTypeOverride ||
                    fd.Type.IntrinsicTypeOverride != EIntrinsicTypes.IllegalRuntimeType)
                {
                    AddFieldReference(field, objref, value, true);
                }
                FieldRef fref = new FieldRef(fd);
                if (value.Variability == EVariability.Constant)
                {
                    Store(fref, LiteralReference.CreateConstant(convSample));
                }
                else
                {
                    Store(fref, value.Expr);
                }
            }
            else
            {
                rfa.RewriteWrite(Decompilee, field, Instance, value, this, this);
            }
        }

        protected virtual void OnLoadStaticField(FieldInfo field)
        {
            Type fieldType = field.FieldType;
            RewriteFieldAccess rfa = (RewriteFieldAccess)fieldType.GetCustomOrInjectedAttribute(typeof(RewriteFieldAccess));
            if (rfa == null)
            {
                rfa = (RewriteFieldAccess)field.GetCustomAttributes(typeof(RewriteFieldAccess), true).FirstOrDefault();
            }
            if (rfa == null)
            {
                object sample = null;
                try
                {
                    sample = field.GetValue(null);
                }
                catch (Exception)
                {
                }
                bool isWritten;
                var fd = GetFieldDescriptor(field, null, out isWritten);
                if ((isWritten || sample == null || !sample.GetType().IsValueType) &&
                    !(fd.Type.HasIntrinsicTypeOverride && fd.Type.IntrinsicTypeOverride == EIntrinsicTypes.IllegalRuntimeType))
                {
                    AddFieldReference(field, new StackElement(), null, false);
                    var fref = new FieldRef(fd);
                    Push(fref, sample);
                }
                else
                {
                    Push(
                        LiteralReference.CreateConstant(sample), 
                        sample);
                }
            }
            else
            {
                rfa.RewriteRead(Decompilee, field, Instance, this, this);
            }
        }

        protected void OnStoreStaticField(FieldInfo field, StackElement value)
        {
            Type fieldType = field.FieldType;
            RewriteFieldAccess rfa = (RewriteFieldAccess)fieldType.GetCustomOrInjectedAttribute(typeof(RewriteFieldAccess));
            if (rfa == null)
            {
                bool isWritten;
                FieldDescriptor fd = GetFieldDescriptor(field, null, out isWritten);
                fd.IsConstant = false;
                if (value.Sample != null)
                {
                    fd.UpgradeType(TypeDescriptor.GetTypeOf(value.Sample));
                }
                if (!fd.Type.HasIntrinsicTypeOverride ||
                    fd.Type.IntrinsicTypeOverride != EIntrinsicTypes.IllegalRuntimeType)
                {
                    AddFieldReference(field, new StackElement(), value, true);
                }
                FieldRef fref = new FieldRef(fd);
                Store(fref, value.Expr);
            }
            else
            {
                rfa.RewriteWrite(Decompilee, field, Instance, value, this, this);
            }
        }

        private void RememberStore(Variable item, StackElement value)
        {
            AssignmentInfo ai = new AssignmentInfo(
                _curILI.Index,
                _curILI.Offset,
                (Variable)item,
                value.Expr,
                value.Sample);
            //Debug.Assert(!_varAsmts.Any(a => a.ILIndex == ai.ILIndex));
            _varAsmts.Add(ai);
        }

        private bool UnrollAt(MSILCodeBlock cb)
        {
            var innerHeaders =
                cb.Range.Where(i => _unrollHeaders.ContainsKey(i.Index))
                .Select(i => _unrollHeaders[i.Index]);
            if (innerHeaders.Any())
                return innerHeaders.FirstOrDefault();
            var inBodySuccessors = cb.Successors
                .Select(b => TrackBranchChain(b))
                .Where(b => _unrollHeaders.ContainsKey(b.Range[0].Index));
            if (!inBodySuccessors.Any())
                return TryToEliminateLoops;
            else
                return _unrollHeaders[inBodySuccessors.First().Range[0].Index];
        }

        public void DoNotUnrollAt(int ilIndex)
        {
            _unrollHeaders[ilIndex] = false;
        }

        public void DoUnrollAt(int ilIndex)
        {
            _unrollHeaders[ilIndex] = true;
        }

        #region Handlers
        private void Unsupported()
        {
            throw new NotImplementedException("Instruction " + _curILI.Code.ToString() + " is not yet supported");
        }

        private void HandleCondBranchIf(Expression expr, object condSample, EVariability condVar, bool neglogic)
        {
            Debug.Assert(expr.HasAttribute<ILIndexRef>());
            var orgExpr = expr;

            if (expr.ResultType.CILType.IsEnum)
            {
                object zeroValue = expr.ResultType.CILType.GetEnumValues().GetValue(0);
                LiteralReference lrZero = LiteralReference.CreateConstant(zeroValue);
                if (neglogic)
                    expr = Expression.Equal(expr, lrZero);
                else
                    expr = Expression.NotEqual(expr, lrZero);
                expr.ResultType = typeof(bool);
            }
            else if (!expr.ResultType.CILType.Equals(typeof(bool)))
            {
                if (!expr.ResultType.CILType.IsPrimitive)
                    throw new ArgumentException();

                object zero = TypeConversions.ConvertLong(0, expr.ResultType.CILType);
                Expression zeroExpr = LiteralReference.CreateConstant(zero);
                if (neglogic)
                {
                    expr = Expression.Equal(expr, zeroExpr);
                    condSample = object.Equals(condSample, zero);
                }
                else
                {
                    expr = Expression.NotEqual(expr, zeroExpr);
                    condSample = !object.Equals(condSample, zero);
                }
            }
            else if (neglogic)
            {
                expr = !expr;
                condSample = TypeConversions.PrimitiveNot(condSample);
            }

            if (expr != orgExpr)
                expr.CopyAttributesFrom(orgExpr);
            Debug.Assert(expr.HasAttribute<ILIndexRef>());

            MSILCodeBlock[] succs = _curBB.Successors;
            MSILCodeBlock ctgt = succs.First();
            MSILCodeBlock brtgt = succs.Last();

            IfInfo ifi = new IfInfo()
            {
                Condition = expr,
                ConditionSample = condSample,
                ConditionBlock = _curBB,
                ThenTarget = brtgt,
                ElseTarget = ctgt
            };

            var vars = expr.ExtractLiteralReferences()
                .Where(lr => lr.ReferencedObject is Variable)
                .Select(lr => (Variable)lr.ReferencedObject);

            if ((condVar == EVariability.Constant ||
                condVar == EVariability.LocalVariable) &&
                condSample is bool &&
                vars.All(v => !IsUnrollInhibited(v.LocalIndex)))
            {
                bool flag = (bool)condSample;
                MSILCodeBlock tgt = flag ? brtgt : ctgt;
                if (condVar == EVariability.Constant
                    || UnrollAt(brtgt) || UnrollAt(ctgt))
                {
                    ifi.UnconditionalTarget = tgt;
                }
            }
            _ifStack.Push(ifi);
            _curBB.IsCondition = true;
        }

        private void HandleBranchIf(Expression expr, object sample, EVariability condVar)
        {
            expr.AddAttribute(new ILIndexRef(Method, CurrentILIndex));
            HandleCondBranchIf(expr, sample, condVar, false);
        }

        private void HandleBranch()
        {
            // will be treated within DeclareBlock by a call to
            // ImplementBranch
        }

        private void HandleBox()
        {
            Type targetType = (Type)_curILI.Operand;
            StackElement top = Pop();
            object boxSample = null;
            Type sourceType = sourceType = top.Expr.ResultType.CILType;
            try
            {
                boxSample = TypeConversions.ConvertValue(top.Sample, targetType);
            }
            catch (Exception)
            {
            }
            Push(IntrinsicFunctions.Cast(top.Expr, sourceType, targetType), boxSample);
        }

        public virtual void ImplementCall(MethodBase mb, StackElement[] args)
        {
            ParameterInfo[] pis = mb.GetParameters();
            int numArgs = args.Length;
            int j = numArgs - 1;
            if (mb.CallingConvention.HasFlag(CallingConventions.HasThis))
                j--;
            for (int i = numArgs - 1; i >= 0; i--, j--)
            {
                if (j >= 0)
                {
                    Type ptype = pis[j].ParameterType;
                    if (ptype.IsByRef || ptype.IsPointer)
                        ptype = ptype.GetElementType();
                    Type rtype = args[i].Expr.ResultType.CILType;
                    if (rtype.IsByRef || rtype.IsPointer)
                        rtype = rtype.GetElementType();
                    if (!ptype.IsAssignableFrom(rtype))
                    {
                        LiteralReference lr = args[i].Expr as LiteralReference;
                        Constant c = lr != null ? lr.ReferencedObject as Constant : null;
                        if (c == null)
                        {
                            object sample = args[i].Sample != null ?
                                TypeConversions.ConvertValue(args[i].Sample, ptype)
                                : null;
                            Expression conv = IntrinsicFunctions.Cast(args[i].Expr, rtype, ptype);
                            args[i].Sample = sample;
                            args[i].Expr = conv;
                        }
                        else
                        {
                            object convv = TypeConversions.ConvertValue(c.ConstantValue, ptype);
                            args[i].Expr = LiteralReference.CreateConstant(convv);
                            args[i].Sample = convv;
                        }
                    }
                }
            }
            if (!mb.IsStatic)
            {
                Type tthis = args[0].Expr.ResultType.CILType;
                if (mb.DeclaringType.IsAssignableFrom(tthis))
                {
                    // This is nearly always the case. However, TaskAwaiter vs. 
                    // TaskAwaiter<VoidTaskResult> is an exception...

                    Type[] types = new Type[pis.Length];
                    for (int i = 0; i < pis.Length; i++)
                        types[i] = pis[i].ParameterType;
                    MethodInfo mb2 = tthis.GetMethod(mb.Name, types);
                    if (mb2 != null)
                        mb = mb2;
                }
            }
            Type returnType = typeof(void);
            if (mb is MethodInfo)
            {
                MethodInfo mi = (MethodInfo)mb;
                returnType = mi.ReturnType;
            }
            OnCall(mb, args, returnType);
        }

        private void HandleCall()
        {
            MethodBase mb = (MethodBase)_curILI.Operand;
            ParameterInfo[] pis = mb.GetParameters();
            int numArgs = pis.Length;
            int j = numArgs - 1;
            if (!mb.IsStatic && !mb.IsConstructor)
                ++numArgs;
            StackElement[] args = new StackElement[numArgs];
            for (int i = numArgs - 1; i >= 0; i--, j--)
            {
                args[i] = Pop();
            }
            ImplementCall(mb, args);
        }

        private void HandleConstrained()
        {
            Type tconstraint = (Type)_curILI.Operand;
            StackElement top = Pop();
            top.Expr.ResultType = TypeDescriptor.MakeType(tconstraint);
            Push(top);
        }

        private void HandleLdArg(int index, bool byAddr)
        {
            Literal arg = _arglist[index];
            LiteralReference lr = new LiteralReference(
                arg,
                byAddr ? LiteralReference.EMode.ByAddress : LiteralReference.EMode.Direct);
            if (byAddr)
            {
                Variable v = arg as Variable;
                if (v == null)
                    throw new InvalidOperationException("Trying to address argument being not of type 'Variable'");
                v.IsAccessedByAddress = true;
            }
            object sample;
            if (!Method.IsStatic)
            {
                --index;
            }
            if (index < 0)
            {
                sample = _thisRef.Eval(_eval);
            }
            else
            {
                sample = ArgumentValues[index];
            }
            Push(lr, sample);
        }

        protected virtual void OnLdLoc(LocVarInfo lvi, bool byAddr)
        {
            Variable loc = lvi.Var;
            object sample = null;
            _localVarState.TryGetValue(lvi.Var.LocalIndex, out sample);
            /*
            object[] samples = GetLocalVarAssignedValues(loc);
            object sample = samples.FirstOrDefault();
             * */
            LiteralReference lr = new LiteralReference(
                loc,
                byAddr ? LiteralReference.EMode.ByAddress : LiteralReference.EMode.Direct);
            if (byAddr)
                loc.IsAccessedByAddress = true;
            Push(lr, sample);
        }

        private void HandleLdind()
        {
            StackElement address = Pop();
            Expression addressExpr = address.Expr;
            StackElement value;
            if (CheckForVariableReplacement(out value))
            {
                Push(value);
            }
            else if (addressExpr is LiteralReference)
            {
                LiteralReference lr = (LiteralReference)addressExpr;
                Debug.Assert(lr.Mode == LiteralReference.EMode.ByAddress);
                LiteralReference lrDeref = new LiteralReference(lr.ReferencedObject, LiteralReference.EMode.Direct);
                Push(lrDeref, address.Sample);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void HandleLdobj()
        {
            StackElement address = Pop();
            LiteralReference lrIndirect = address.Expr as LiteralReference;
            if (lrIndirect == null)
                throw new NotSupportedException("Expected literal reference");
            Debug.Assert(lrIndirect.Mode == LiteralReference.EMode.ByAddress);
            LiteralReference lrDirect = new LiteralReference(lrIndirect.ReferencedObject, LiteralReference.EMode.Direct);
            Push(lrDirect, address.Sample);
        }

        private LocVarInfo GetLocVarInfo(int localIndex, bool byAddr)
        {
            int subIndex;
            if (byAddr)
            {
                List<int> usePoints = Facts.DFA.GetUsePointsOfRefPoint(_curILI.Index);
                IEnumerable<int> subIndices = usePoints.Select(i => Facts.DFA.GetRenamingIndex(localIndex, i)).Distinct();
                if (subIndices.Count() > 1)
                    ReportError("Ambiguous renaming of local variable with index " + localIndex + " at IL offset " + _curILI.Offset);
                subIndex = subIndices.FirstOrDefault();
            }
            else
            {
                subIndex = Facts.DFA.GetRenamingIndex(localIndex, _curILI.Index);
            }
            Tuple<int, int> key = Tuple.Create(localIndex, subIndex);
            LocVarInfo lvi;
            if (!_locmap.TryGetValue(key, out lvi))
            {
                int numSub = Facts.DFA.GetNumRenamings(localIndex);
                string name = "local" + localIndex;
                if (numSub > 1)
                    name += "_" + subIndex;


                Variable locvar = new Variable(
                    TypeDescriptor.MakeType(
                    _body.LocalVariables[localIndex].LocalType))
                {
                    Name = name,
                    LocalIndex = localIndex,
                    LocalSubIndex = subIndex
                };
                lvi = new LocVarInfo()
                {
                    Var = locvar,
                    IsReferenced = () => false
                };
                _locmap[key] = lvi;
            }

            return lvi;
        }

        private bool CheckForVariableReplacement(int localIndex, out StackElement value)
        {
            EVariability var = VARA.GetLocalVariableVariability(_curILI.Index, localIndex);
            if (CheckForVariableReplacement(out value))
            {
                return true;
            }
            else if (TryToEliminateLoops &&
                var != EVariability.ExternVariable &&
                !IsUnrollInhibited(localIndex) &&
                _localVarState.ContainsKey(localIndex))
            {
                StackElement result = new StackElement(
                    LiteralReference.CreateConstant(_localVarState[localIndex]),
                    _localVarState[localIndex],
                    var);
                value = result;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CheckForVariableReplacement(out StackElement value)
        {
            return _readPointExprs.TryGetValue(_curILI.Index, out value);
        }

        private void HandleLdLoc(int index, bool byAddr)
        {
            StackElement value;
            if (/*!byAddr &&*/ CheckForVariableReplacement(index, out value))
            {
                // The local variable is about to be eliminated, so inline the internally buffered r-value.
                Push(value);
            }
            else
            {
                LocVarInfo lvi = GetLocVarInfo(index, byAddr);
                OnLdLoc(lvi, byAddr);
            }
        }

        protected virtual void OnStLoc(LocVarInfo lvi, StackElement value)
        {
            Variable loc = lvi.Var;
            if (value.Expr.ResultType.IsComplete)
                loc.UpgradeType(value.Expr.ResultType);
            StackElement lhs;
            lhs.Expr = loc;
            lhs.Sample = null;
            lhs.Variability = EVariability.ExternVariable;
            EqualizeTypes(ref lhs, ref value, false);

            if (value.Sample != null &&
                !(loc.Type.HasIntrinsicTypeOverride && loc.Type.IntrinsicTypeOverride == EIntrinsicTypes.IllegalRuntimeType))
            {
                var expr = value.Expr;
                if (value.Variability == EVariability.Constant)
                {
                    expr = LiteralReference.CreateConstant(value.Sample);
                }
                else if (!expr.ResultType.CILType.Equals(loc.Type.CILType))
                {
                    expr = IntrinsicFunctions.Cast(expr, expr.ResultType.CILType, loc.Type);
                }
                Store(loc, expr);
                LastStatement.EliminationPredicate = () => value.Expr.IsInlined;
            }

            RememberStore(loc, value);
        }

        private bool CheckDetainAssignment(StackElement value, int localIndex)
        {
            int readPoint;
            if (Facts.DFA.IsReadAfterWrite(_curILI.Index, out readPoint))
            {
                // The local variable is about to be eliminated, so store the r-value internally.
                _readPointExprs[readPoint] = value;
                return true;
            }
            else if (value.Expr.ResultType.IsEmptyType)
            {
                // e.g. an empty StdLogicVector => value does not carry any information
                // we need no variable for it.
                _readPointExprs[readPoint] = value;
                return true;
            }
            else if (Facts.DFA.IsWrittenAndNeverRead(_curILI.Index))
            {
                // nothing to do
                return true;
            }
            else if (TryToEliminateLoops && localIndex >= 0)
            {
                if (!IsUnrollInhibited(localIndex)
                    && VARA.GetLocalVariableVariability(_curILI.Index, localIndex) !=
                    EVariability.ExternVariable)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void HandleStLoc(int index)
        {
            StackElement value = Pop();
            if (value.Sample != null)
            {
                var stype = value.Sample.GetType();
                if (stype.IsGenericType &&
                    stype.GetGenericTypeDefinition().Equals(typeof(TaskAwaiter<>)))
                {
                    // Ugly special case. Decompiler behaves correctly. But MS compiler relies on 
                    // TaskAwaiter being the same like TaskAwaiter<System.Threading.Tasks.VoidTaskResult>.
                    // Which is not. Moreover, System.Threading.Tasks.VoidTaskResult is not public...
                    // woraround: don't convert anything
                }
                else
                {
                    value.Sample = TypeConversions.ConvertValue(
                        value.Sample,
                        _body.LocalVariables[index].LocalType);
                }
            }
            _localVarState[index] = value.Sample;
            if (CheckDetainAssignment(value, index))
            {
                // nop
            }
            else
            {
                LocVarInfo lvi = GetLocVarInfo(index, false);
                OnStLoc(lvi, value);
            }
        }

        private void HandleLdelem(bool byAddr)
        {
            StackElement index = Pop();
            StackElement array = Pop();
            LiteralReference arraySelf = (LiteralReference)array.Expr;
            object sample = null;
            Array a = array.Sample as Array;
            if (a != null && a.LongLength > 0)
            {
                sample = a.GetValue(0);
            }
            LiteralReference arrayRef = new LiteralReference(
                new ArrayRef(arraySelf, array.Expr.ResultType.Element0Type, index.Expr),
                byAddr ? LiteralReference.EMode.ByAddress : LiteralReference.EMode.Direct);
            Push(arrayRef, sample);
        }

        private void HandleLdfld()
        {
            FieldInfo fi = (FieldInfo)_curILI.Operand;
            StackElement oref = Pop();
            OnLoadField(fi, oref);
        }

        private void HandleStfld()
        {
            StackElement value = Pop();
            StackElement objref = Pop();
            FieldInfo fi = (FieldInfo)_curILI.Operand;
            OnStoreField(fi, objref, value);
        }

        private void HandleLoadStaticField()
        {
            FieldInfo fi = (FieldInfo)_curILI.Operand;
            OnLoadStaticField(fi);
        }

        private void HandleStoreStaticField()
        {
            FieldInfo fi = (FieldInfo)_curILI.Operand;
            StackElement value = Pop();
            OnStoreStaticField(fi, value);
        }

        private void HandleArrayLength()
        {
            StackElement array = Pop();
            FunctionCall funcref = IntrinsicFunctions.GetArrayLength(array.Expr);
            object sample = null;
            try
            {
                Array a = (Array)array.Sample;
                sample = a.LongLength;
            }
            catch (Exception)
            {
            }
            Push(funcref, sample);
        }

        private void HandlePop()
        {
            Pop();
        }

        private void HandleReturn()
        {
            if (DisallowReturnStatements)
            {
#if false
                // Try to reach the exit node by breaking the current loop or case statement
                if (NestLoopsDeeply)
                {
                    // Loops may incorporate a trailer, so BreakInfo.BreakNext is not reliable

                    foreach (BreakInfo bi in _breakStack)
                    {
                        MSILCodeBlock next = skipNOPs(bi.BreakNext);
                        if (next.Code.Instructions.First().Code == OpCodes.Ret)
                        {
                            if (bi.Stmt is LoopBlock)
                                Break((LoopBlock)bi.Stmt);
                            else if (bi.Stmt is CaseStatement)
                                Break((CaseStatement)bi.Stmt);
                            else
                                throw new NotImplementedException();
                            return;
                        }
                    }
                }

                // If we're at the end of the code anyway, no statement need to be generated
                if (_curBB.Successors.Length == 1 &&
                    _curBB.Successors.First() == _code.BasicBlocks.Last())
                {
                    return;
                }

                // If nothing helps, jump to the exit node
                GotoStatement stmt = Goto();
                _gotoTargets.Add(new KeyValuePair<GotoStatement, int>(
                    stmt, _code.BasicBlocks.Last().StartIndex));
#endif
                ImplementBranch(_code.BasicBlocks.Last());

                return;
            }

            if (!MethodReturnType.Equals(typeof(void)))
            {
                StackElement retval = Pop();
                _retAsmts.Add(retval);
                if (TreatReturnValueAsVariable)
                {
                    Store((IStorableLiteral)_arglist.Last(), retval.Expr);
                    Return();
                }
                else
                {
                    Return(retval.Expr);
                }
            }
            else
            {
                Return();
            }
        }

        private void HandleShl()
        {
            StackElement numbits = Pop();
            StackElement value = Pop();
            object sample = TypeConversions.PrimitiveShl(value.Sample, numbits.Sample);
            Expression result = Expression.LShift(value.Expr, numbits.Expr);
            result.ResultType = value.Expr.ResultType;
            Push(result, sample);
        }

        private void HandleShr()
        {
            StackElement numbits = Pop();
            StackElement value = Pop();
            object sample = TypeConversions.PrimitiveShr(value.Sample, numbits.Sample);
            Expression result = Expression.RShift(value.Expr, numbits.Expr);
            result.ResultType = value.Expr.ResultType;
            Push(result, sample);
        }

        private void HandleStArg(int index)
        {
            IStorableLiteral arg = (IStorableLiteral)_arglist[index];
            StackElement value = Pop();
            Store(arg, value.Expr);
        }

        private void HandleStelem()
        {
            StackElement value = Pop();
            StackElement index = Pop();
            StackElement aref = Pop();
            Array array = aref.Sample as Array;
            if (array != null && array.LongLength > 0 && value.Sample != null)
            {
                array.SetValue(value.Sample, 0);
            }
            FunctionCall newarrCall =
                ResolveVariableReference(CurrentILIndex, aref.Expr) as FunctionCall;
            if (newarrCall != null)
            {
                FunctionSpec fspec = newarrCall.Callee as FunctionSpec;
                IntrinsicFunction ifun = fspec == null ? null : fspec.IntrinsicRep;
                if (ifun != null && ifun.Action == IntrinsicFunction.EAction.NewArray)
                {
                    ArrayParams aparams = (ArrayParams)ifun.Parameter;
                    try
                    {
                        object indexEvaluated = index.Expr.Eval(_eval);
                        long indexLong = TypeConversions.ToLong(indexEvaluated);
                        if (indexLong >= 0 && indexLong < aparams.Elements.LongLength)
                        {
                            aparams.Elements[indexLong] = value.Expr;
                        }
                        else
                        {
                            // The decompiled code will throw an IndexOutOfRangeException during execution if
                            // this branch is reached. Mark array parameters as non-static, this is the conservative assumption.
                            aparams.IsStatic = false;
                        }
                    }
                    catch (BreakEvaluationException)
                    {
                        aparams.IsStatic = false;
                    }
                }
            }
            ArrayRef lhs = new ArrayRef(aref.Expr, value.Expr.ResultType, index.Expr);
            Store(lhs, value.Expr);
            LastStatement.EliminationPredicate = () => value.Expr.IsInlined;
        }

        private void HandleStobj()
        {
            StackElement value = Pop();
            StackElement address = Pop();
            if (!CheckDetainAssignment(value, -1))
            {
                Expression addressExpr = address.Expr;
                if (addressExpr is LiteralReference)
                {
                    LiteralReference lr = (LiteralReference)addressExpr;
                    ArrayRef aref = lr.ReferencedObject as ArrayRef;
                    if (aref != null)
                    {
                        LiteralReference aself = aref.ArrayExpr as LiteralReference;
                        Variable avar = aself.ReferencedObject as Variable;
                        if (avar != null)
                        {
                            Array a = GetLocalVarAssignedValues(avar).FirstOrDefault() as Array;
                            if (a != null && a.LongLength > 0 && value.Sample != null)
                            {
                                a.SetValue(value.Sample, 0);
                            }
                        }
                    }
                    Debug.Assert(
                        lr.Mode == LiteralReference.EMode.ByAddress ||
                        lr.ResultType.CILType.IsByRef);
                    LiteralReference lrDirect = new LiteralReference(lr.ReferencedObject, LiteralReference.EMode.Direct);
                    EqualizeTypes(ref address, ref value, false);
                    IStorableLiteral item = (IStorableLiteral)lr.ReferencedObject;
                    Store(item, value.Expr);
                    LastStatement.EliminationPredicate = () => value.Expr.IsInlined;
                    if (item is Variable)
                    {
                        RememberStore((Variable)item, value);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void HandleSwitch()
        {
            StackElement cond = Pop();
            long offsetValue = 0;
            var condExpr = cond.Expr;
            // Special case: switch(x - ESomeEnumeration.FirstValue)
            if (cond.Expr is BinOp)
            {
                var bop = cond.Expr as BinOp;
                Debug.Assert(bop.Operation == BinOp.Kind.Sub);
                var offset = bop.Operand2 as LiteralReference;
                Debug.Assert(offset != null);
                var offsetLit = offset.ReferencedObject as Constant;
                Debug.Assert(offsetLit != null);
                offsetValue = (long)TypeConversions.ConvertValue(
                    offsetLit.ConstantValue, typeof(long));
                condExpr = bop.Operand1;
            }
            CaseStatement cstmt = Switch(condExpr);
            int[] targets = (int[])_curILI.Operand;
            var targetsAndDefault = targets.Concat(Enumerable.Repeat(_code.GetNextOffset(_curILI.Offset), 1));
            var targetBlocks = targetsAndDefault.Select(offs => _code.GetBasicBlockStartingAt(_code.InstructionInfo[offs].Index));
            SwitchInfo si = new SwitchInfo()
            {
                Case = cstmt,
                Condition = _curBB,
                SwitchTargets = targetBlocks.ToArray(),
                SelOffset = offsetValue
            };
            _switchStack.Push(si);
            _curBB.IsSwitch = true;
        }

        private void HandleNewArr()
        {
            StackElement length = Pop();
            Type elemType = (Type)_curILI.Operand;
            Array sample = null;
            try
            {
                object numElemsObj = length.Expr.Eval(_eval);
                long numElems = TypeConversions.ToLong(numElemsObj);
                sample = Array.CreateInstance(elemType, numElems);
            }
            catch (BreakEvaluationException)
            {
            }
            Push(IntrinsicFunctions.NewArray(elemType, length.Expr, sample), sample);
        }

        private void HandleNewObj()
        {
            ConstructorInfo ctor = (ConstructorInfo)_curILI.Operand;
            ParameterInfo[] pis = ctor.GetParameters();
            int numArgs = pis.Length;
            StackElement[] args = new StackElement[numArgs];
            for (int i = numArgs - 1; i >= 0; i--)
                args[i] = Pop();

            RewriteCall rwcall = (RewriteCall)ctor.GetCustomOrInjectedAttribute(typeof(RewriteCall));
            if (rwcall != null)
            {
                if (rwcall.Rewrite(Decompilee, ctor, args, this, this))
                    return;
            }

            Push(IntrinsicFunctions.NewObject(ctor, args.Select(a => a.Expr).ToArray()), null);
        }

        private Expression ConvertValue(object value, Type newType)
        {
            return LiteralReference.CreateConstant(TypeConversions.ConvertValue(value, newType));
        }

        private void HandleCompare()
        {
            StackElement v2 = Pop();
            StackElement v1 = Pop();
            EqualizeTypes(ref v1, ref v2, true);
            object o1 = v1.Sample;
            object o2 = v2.Sample;
            Expression e1 = v1.Expr;
            Expression e2 = v2.Expr;
            if (_curILI.Code.Equals(OpCodes.Ceq))
                Push(Expression.Equal(e1, e2), TypeConversions.PrimitiveEqual(o1, o2));
            else if (_curILI.Code.Equals(OpCodes.Clt))
                Push(Expression.LessThan(e1, e2), TypeConversions.PrimitiveLessThan(o1, o2));
            else if (_curILI.Code.Equals(OpCodes.Clt_Un))
                Push(Expression.LessThan(e1, e2), TypeConversions.PrimitiveLessThan_Un(o1, o2));
            else if (_curILI.Code.Equals(OpCodes.Cgt))
                Push(Expression.GreaterThan(e1, e2), TypeConversions.PrimitiveGreaterThan(o1, o2));
            else if (_curILI.Code.Equals(OpCodes.Cgt_Un))
                Push(Expression.GreaterThan(e1, e2), TypeConversions.PrimitiveGreaterThan_Un(o1, o2));
            else
                throw new InvalidOperationException();
        }

        private void EqualizeTypes(ref StackElement se1, ref StackElement se2, bool admitBidir)
        {
            if (se1.Sample == null ||
                se2.Sample == null)
                return;

            Expression e1 = se1.Expr;
            Expression e2 = se2.Expr;
            Type type1 = e1.ResultType.CILType;
            if (type1.IsByRef || type1.IsPointer)
                type1 = type1.GetElementType();
            Type type2 = e2.ResultType.CILType;
            if (type2.IsByRef || type2.IsPointer)
                type2 = type2.GetElementType();
            if (!type1.IsAssignableFrom(type2) &&
                (!admitBidir || !type2.IsAssignableFrom(type1)))
            {
                LiteralReference lr1 = e1 as LiteralReference;
                LiteralReference lr2 = e2 as LiteralReference;
                Constant c1 = lr1 != null ? lr1.ReferencedObject as Constant : null;
                Constant c2 = lr2 != null ? lr2.ReferencedObject as Constant : null;
                if (c1 != null && c1.Type.CILType.IsPrimitive)
                {
                    object convv = TypeConversions.ConvertValue(c1.ConstantValue, e2.ResultType.CILType);
                    e1 = LiteralReference.CreateConstant(convv);
                    se1.Expr = e1;
                    se1.Sample = convv;
                }
                else if (c2 != null && c2.Type.CILType.IsPrimitive)
                {
                    object convv = TypeConversions.ConvertValue(c2.ConstantValue, e1.ResultType.CILType);
                    e2 = LiteralReference.CreateConstant(convv);
                    se2.Expr = e2;
                    se2.Sample = convv;
                }
                else
                    throw new NotImplementedException();
            }
        }

        private delegate Expression BinOpExpr(Expression e1, Expression e2);
        private delegate object BinOpSample(object o1, object o2);

        private void HandleBinOp(BinOpExpr eh, BinOpSample sh)
        {
            StackElement v2 = Pop();
            StackElement v1 = Pop();
            EqualizeTypes(ref v1, ref v2, true);
            Expression re = eh(v1.Expr, v2.Expr);
            object ro = sh(v1.Sample, v2.Sample);
            Push(re, ro);
        }

        private void HandleBinBranch(BinOpExpr eh, BinOpSample sh)
        {
            StackElement v2 = Pop();
            StackElement v1 = Pop();
            EqualizeTypes(ref v1, ref v2, true);
            HandleBranchIf(
                eh(v1.Expr, v2.Expr),
                sh(v1.Sample, v2.Sample),
                VariabilityOperations.Stronger(v1.Variability, v2.Variability));
        }

        private void HandleUnBranch(bool negLogic)
        {
            StackElement v = Pop();
            HandleCondBranchIf(v.Expr, v.Sample, v.Variability, negLogic);
        }

        private delegate Expression UnOpExpr(Expression e);
        private delegate object UnOpSample(object o);

        private void HandleUnOp(UnOpExpr eh, UnOpSample sh)
        {
            StackElement v = Pop();
            Expression re = eh(v.Expr);
            object ro = sh(v.Sample);
            Push(re, ro);
        }

        private Variable AllocLocalVariable(TypeDescriptor type)
        {
            int index = _nextLocIndex++;
            Variable tmp = new Variable(type)
            {
                Name = "tmp" + index,
                LocalIndex = index,
                LocalSubIndex = 0
            };
            LocVarInfo lvi = new LocVarInfo()
            {
                Var = tmp,
                IsReferenced = () => true
            };
            _locmap[Tuple.Create(index, 0)] = lvi;
            return tmp;
        }

        private void KillLocalVariable(Variable loc)
        {
            _locmap.Remove(Tuple.Create(loc.LocalIndex, loc.LocalSubIndex));
        }

        private void HandleDup()
        {
            StackElement top = Pop();
            var toplr = top.Expr as LiteralReference;
            if (toplr != null)
            {
                Push(top);
                Push(top);
            }
            else
            {
                Variable tmp = AllocLocalVariable(top.Expr.ResultType);
                Store(tmp, top.Expr);
                Push(tmp, top.Sample);
                Push(tmp, top.Sample);
            }
        }

        private void HandleConv(Type targetType)
        {
            StackElement el = Pop();
            Expression result = IntrinsicFunctions.Cast(el.Expr, el.Expr.ResultType.CILType, targetType);
            object sample = null;
            if (el.Sample != null)
                sample = TypeConversions.ConvertValue(el.Sample, targetType);
            Push(result, sample);
        }

        private void HandleInitobj()
        {
            Pop();
        }

        #endregion

        #region HandlerMap
        private void InitHandlerMap()
        {
            _hdlMap[OpCodes.Add] = () => HandleBinOp((x, y) => x + y, TypeConversions.PrimitiveAdd);
            _hdlMap[OpCodes.Add_Ovf] = () => HandleBinOp((x, y) => x + y, TypeConversions.PrimitiveAdd);
            _hdlMap[OpCodes.Add_Ovf_Un] = () => HandleBinOp((x, y) => x + y, TypeConversions.PrimitiveAdd);
            _hdlMap[OpCodes.And] = () => HandleBinOp((x, y) => x & y, TypeConversions.PrimitiveAnd);
            _hdlMap[OpCodes.Arglist] = Unsupported;
            _hdlMap[OpCodes.Beq] = () => HandleBinBranch(Expression.Equal, TypeConversions.PrimitiveEqual);
            _hdlMap[OpCodes.Beq_S] = () => HandleBinBranch(Expression.Equal, TypeConversions.PrimitiveEqual);
            _hdlMap[OpCodes.Bge] = () => HandleBinBranch(Expression.GreaterThanOrEqual, TypeConversions.PrimitiveGreaterThanOrEqual);
            _hdlMap[OpCodes.Bge_S] = () => HandleBinBranch(Expression.GreaterThanOrEqual, TypeConversions.PrimitiveGreaterThanOrEqual);
            _hdlMap[OpCodes.Bge_Un] = () => HandleBinBranch(Expression.GreaterThanOrEqual, TypeConversions.PrimitiveGreaterThanOrEqual);
            _hdlMap[OpCodes.Bge_Un_S] = () => HandleBinBranch(Expression.GreaterThanOrEqual, TypeConversions.PrimitiveGreaterThanOrEqual);
            _hdlMap[OpCodes.Bgt] = () => HandleBinBranch(Expression.GreaterThan, TypeConversions.PrimitiveGreaterThan);
            _hdlMap[OpCodes.Bgt_S] = () => HandleBinBranch(Expression.GreaterThan, TypeConversions.PrimitiveGreaterThan);
            _hdlMap[OpCodes.Bgt_Un] = () => HandleBinBranch(Expression.GreaterThan, TypeConversions.PrimitiveGreaterThan);
            _hdlMap[OpCodes.Bgt_Un_S] = () => HandleBinBranch(Expression.GreaterThan, TypeConversions.PrimitiveGreaterThan);
            _hdlMap[OpCodes.Ble] = () => HandleBinBranch(Expression.LessThanOrEqual, TypeConversions.PrimitiveLessThanOrEqual);
            _hdlMap[OpCodes.Ble_S] = () => HandleBinBranch(Expression.LessThanOrEqual, TypeConversions.PrimitiveLessThanOrEqual);
            _hdlMap[OpCodes.Ble_Un] = () => HandleBinBranch(Expression.LessThanOrEqual, TypeConversions.PrimitiveLessThanOrEqual);
            _hdlMap[OpCodes.Ble_Un_S] = () => HandleBinBranch(Expression.LessThanOrEqual, TypeConversions.PrimitiveLessThanOrEqual);
            _hdlMap[OpCodes.Blt] = () => HandleBinBranch(Expression.LessThan, TypeConversions.PrimitiveLessThan);
            _hdlMap[OpCodes.Blt_S] = () => HandleBinBranch(Expression.LessThan, TypeConversions.PrimitiveLessThan);
            _hdlMap[OpCodes.Blt_Un] = () => HandleBinBranch(Expression.LessThan, TypeConversions.PrimitiveLessThan);
            _hdlMap[OpCodes.Blt_Un_S] = () => HandleBinBranch(Expression.LessThan, TypeConversions.PrimitiveLessThan);
            _hdlMap[OpCodes.Bne_Un] = () => HandleBinBranch(Expression.NotEqual, TypeConversions.PrimitiveUnequal);
            _hdlMap[OpCodes.Bne_Un_S] = () => HandleBinBranch(Expression.NotEqual, TypeConversions.PrimitiveUnequal);
            _hdlMap[OpCodes.Box] = HandleBox;
            _hdlMap[OpCodes.Br] = HandleBranch;
            _hdlMap[OpCodes.Br_S] = HandleBranch;
            _hdlMap[OpCodes.Break] = () => { };
            _hdlMap[OpCodes.Brfalse] = () => HandleUnBranch(true);
            _hdlMap[OpCodes.Brfalse_S] = () => HandleUnBranch(true);
            _hdlMap[OpCodes.Brtrue] = () => HandleUnBranch(false);
            _hdlMap[OpCodes.Brtrue_S] = () => HandleUnBranch(false);
            _hdlMap[OpCodes.Call] = HandleCall;
            _hdlMap[OpCodes.Calli] = HandleCall;
            _hdlMap[OpCodes.Callvirt] = HandleCall;
            _hdlMap[OpCodes.Castclass] = () => { };
            _hdlMap[OpCodes.Ceq] = HandleCompare;
            _hdlMap[OpCodes.Cgt] = HandleCompare;
            _hdlMap[OpCodes.Cgt_Un] = HandleCompare;
            _hdlMap[OpCodes.Ckfinite] = () => { throw new NotImplementedException(); };
            _hdlMap[OpCodes.Clt] = HandleCompare;
            _hdlMap[OpCodes.Clt_Un] = HandleCompare;
            _hdlMap[OpCodes.Constrained] = HandleConstrained;
            _hdlMap[OpCodes.Conv_I] = () => { HandleConv(typeof(int)); };
            _hdlMap[OpCodes.Conv_I1] = () => { HandleConv(typeof(sbyte)); };
            _hdlMap[OpCodes.Conv_I2] = () => { HandleConv(typeof(short)); };
            _hdlMap[OpCodes.Conv_I4] = () => { HandleConv(typeof(Int32)); };
            _hdlMap[OpCodes.Conv_I8] = () => { HandleConv(typeof(long)); };
            _hdlMap[OpCodes.Conv_Ovf_I] = () => { HandleConv(typeof(int)); };
            _hdlMap[OpCodes.Conv_Ovf_I_Un] = () => { HandleConv(typeof(uint)); };
            _hdlMap[OpCodes.Conv_Ovf_I1] = () => { HandleConv(typeof(sbyte)); };
            _hdlMap[OpCodes.Conv_Ovf_I1_Un] = () => { HandleConv(typeof(byte)); };
            _hdlMap[OpCodes.Conv_Ovf_I2] = () => { HandleConv(typeof(short)); };
            _hdlMap[OpCodes.Conv_Ovf_I2_Un] = () => { HandleConv(typeof(ushort)); };
            _hdlMap[OpCodes.Conv_Ovf_I4] = () => { HandleConv(typeof(Int32)); };
            _hdlMap[OpCodes.Conv_Ovf_I4_Un] = () => { HandleConv(typeof(UInt32)); };
            _hdlMap[OpCodes.Conv_Ovf_I8] = () => { HandleConv(typeof(long)); };
            _hdlMap[OpCodes.Conv_Ovf_I8_Un] = () => { HandleConv(typeof(ulong)); };
            _hdlMap[OpCodes.Conv_Ovf_U] = () => { HandleConv(typeof(uint)); };
            _hdlMap[OpCodes.Conv_Ovf_U_Un] = () => { HandleConv(typeof(uint)); };
            _hdlMap[OpCodes.Conv_Ovf_U1] = () => { HandleConv(typeof(sbyte)); };
            _hdlMap[OpCodes.Conv_Ovf_U1_Un] = () => { HandleConv(typeof(byte)); };
            _hdlMap[OpCodes.Conv_Ovf_U2] = () => { HandleConv(typeof(short)); };
            _hdlMap[OpCodes.Conv_Ovf_U2_Un] = () => { HandleConv(typeof(ushort)); };
            _hdlMap[OpCodes.Conv_Ovf_U4] = () => { HandleConv(typeof(Int32)); };
            _hdlMap[OpCodes.Conv_Ovf_U4_Un] = () => { HandleConv(typeof(UInt32)); };
            _hdlMap[OpCodes.Conv_Ovf_U8] = () => { HandleConv(typeof(long)); };
            _hdlMap[OpCodes.Conv_Ovf_U8_Un] = () => { HandleConv(typeof(ulong)); };
            _hdlMap[OpCodes.Conv_R_Un] = () => { HandleConv(typeof(float)); };
            _hdlMap[OpCodes.Conv_R4] = () => { HandleConv(typeof(float)); };
            _hdlMap[OpCodes.Conv_R8] = () => { HandleConv(typeof(double)); };
            _hdlMap[OpCodes.Conv_U] = () => { HandleConv(typeof(uint)); };
            _hdlMap[OpCodes.Conv_U1] = () => { HandleConv(typeof(byte)); };
            _hdlMap[OpCodes.Conv_U2] = () => { HandleConv(typeof(ushort)); };
            _hdlMap[OpCodes.Conv_U4] = () => { HandleConv(typeof(UInt32)); };
            _hdlMap[OpCodes.Conv_U8] = () => { HandleConv(typeof(ulong)); };
            _hdlMap[OpCodes.Cpblk] = Unsupported;
            _hdlMap[OpCodes.Cpobj] = Unsupported;
            _hdlMap[OpCodes.Div] = () => HandleBinOp((x, y) => x / y, TypeConversions.PrimitiveDiv);
            _hdlMap[OpCodes.Div_Un] = () => HandleBinOp((x, y) => x / y, TypeConversions.PrimitiveDiv);
            _hdlMap[OpCodes.Dup] = HandleDup;
            _hdlMap[OpCodes.Endfilter] = Unsupported;
            _hdlMap[OpCodes.Endfinally] = Unsupported;
            _hdlMap[OpCodes.Initblk] = Unsupported;
            _hdlMap[OpCodes.Initobj] = HandleInitobj;
            _hdlMap[OpCodes.Isinst] = Unsupported;
            _hdlMap[OpCodes.Jmp] = Unsupported;
            _hdlMap[OpCodes.Ldarg] = () => HandleLdArg((int)_curILI.Operand, false);
            _hdlMap[OpCodes.Ldarg_0] = () => HandleLdArg(0, false);
            _hdlMap[OpCodes.Ldarg_1] = () => HandleLdArg(1, false);
            _hdlMap[OpCodes.Ldarg_2] = () => HandleLdArg(2, false);
            _hdlMap[OpCodes.Ldarg_3] = () => HandleLdArg(3, false);
            _hdlMap[OpCodes.Ldarg_S] = () => HandleLdArg((byte)_curILI.Operand, false);
            _hdlMap[OpCodes.Ldarga] = () => HandleLdArg((int)_curILI.Operand, true);
            _hdlMap[OpCodes.Ldarga_S] = () => HandleLdArg((byte)_curILI.Operand, true);
            _hdlMap[OpCodes.Ldc_I4] = () => Push(LiteralReference.CreateConstant(_curILI.Operand), _curILI.Operand);
            _hdlMap[OpCodes.Ldc_I4_0] = () => Push(LiteralReference.CreateConstant((int)0), (int)0);
            _hdlMap[OpCodes.Ldc_I4_1] = () => Push(LiteralReference.CreateConstant((int)1), (int)1);
            _hdlMap[OpCodes.Ldc_I4_2] = () => Push(LiteralReference.CreateConstant((int)2), (int)2);
            _hdlMap[OpCodes.Ldc_I4_3] = () => Push(LiteralReference.CreateConstant((int)3), (int)3);
            _hdlMap[OpCodes.Ldc_I4_4] = () => Push(LiteralReference.CreateConstant((int)4), (int)4);
            _hdlMap[OpCodes.Ldc_I4_5] = () => Push(LiteralReference.CreateConstant((int)5), (int)5);
            _hdlMap[OpCodes.Ldc_I4_6] = () => Push(LiteralReference.CreateConstant((int)6), (int)6);
            _hdlMap[OpCodes.Ldc_I4_7] = () => Push(LiteralReference.CreateConstant((int)7), (int)7);
            _hdlMap[OpCodes.Ldc_I4_8] = () => Push(LiteralReference.CreateConstant((int)8), (int)8);
            _hdlMap[OpCodes.Ldc_I4_M1] = () => Push(LiteralReference.CreateConstant((int)-1), (int)-1);
            _hdlMap[OpCodes.Ldc_I4_S] = () => Push(LiteralReference.CreateConstant((int)(sbyte)_curILI.Operand), (int)(sbyte)_curILI.Operand);
            _hdlMap[OpCodes.Ldc_I8] = () => Push(LiteralReference.CreateConstant((long)_curILI.Operand), _curILI.Operand);
            _hdlMap[OpCodes.Ldc_R4] = () => Push(LiteralReference.CreateConstant((float)_curILI.Operand), _curILI.Operand);
            _hdlMap[OpCodes.Ldc_R8] = () => Push(LiteralReference.CreateConstant((double)_curILI.Operand), _curILI.Operand);
            _hdlMap[OpCodes.Ldelem] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_I] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_I1] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_I2] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_I4] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_I8] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_R4] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_R8] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_Ref] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_U1] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_U2] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelem_U4] = () => HandleLdelem(false);
            _hdlMap[OpCodes.Ldelema] = () => HandleLdelem(true);
            _hdlMap[OpCodes.Ldfld] = HandleLdfld;
            _hdlMap[OpCodes.Ldflda] = HandleLdfld;
            _hdlMap[OpCodes.Ldftn] = Unsupported;
            _hdlMap[OpCodes.Ldind_I] = HandleLdind;
            _hdlMap[OpCodes.Ldind_I1] = HandleLdind;
            _hdlMap[OpCodes.Ldind_I2] = HandleLdind;
            _hdlMap[OpCodes.Ldind_I4] = HandleLdind;
            _hdlMap[OpCodes.Ldind_I8] = HandleLdind;
            _hdlMap[OpCodes.Ldind_R4] = HandleLdind;
            _hdlMap[OpCodes.Ldind_R8] = HandleLdind;
            _hdlMap[OpCodes.Ldind_Ref] = HandleLdind;
            _hdlMap[OpCodes.Ldind_U1] = HandleLdind;
            _hdlMap[OpCodes.Ldind_U2] = HandleLdind;
            _hdlMap[OpCodes.Ldind_U4] = HandleLdind;
            _hdlMap[OpCodes.Ldlen] = () => HandleArrayLength();
            _hdlMap[OpCodes.Ldloc] = () => HandleLdLoc((int)_curILI.Operand, false);
            _hdlMap[OpCodes.Ldloc_0] = () => HandleLdLoc(0, false);
            _hdlMap[OpCodes.Ldloc_1] = () => HandleLdLoc(1, false);
            _hdlMap[OpCodes.Ldloc_2] = () => HandleLdLoc(2, false);
            _hdlMap[OpCodes.Ldloc_3] = () => HandleLdLoc(3, false);
            _hdlMap[OpCodes.Ldloc_S] = () => HandleLdLoc((byte)_curILI.Operand, false);
            _hdlMap[OpCodes.Ldloca] = () => HandleLdLoc((int)_curILI.Operand, true);
            _hdlMap[OpCodes.Ldloca_S] = () => HandleLdLoc((byte)_curILI.Operand, true);
            _hdlMap[OpCodes.Ldnull] = () => Push(LiteralReference.CreateConstant(null), null);
            _hdlMap[OpCodes.Ldobj] = HandleLdobj;
            _hdlMap[OpCodes.Ldsfld] = HandleLoadStaticField;
            _hdlMap[OpCodes.Ldsflda] = HandleLoadStaticField;
            _hdlMap[OpCodes.Ldstr] = () => Push(LiteralReference.CreateConstant(_curILI.Operand), (string)_curILI.Operand);
            _hdlMap[OpCodes.Ldtoken] = Unsupported;
            _hdlMap[OpCodes.Ldvirtftn] = Unsupported;
            _hdlMap[OpCodes.Leave] = HandleBranch;
            _hdlMap[OpCodes.Leave_S] = HandleBranch;
            _hdlMap[OpCodes.Localloc] = Unsupported;
            _hdlMap[OpCodes.Mkrefany] = Unsupported;
            _hdlMap[OpCodes.Mul] = () => HandleBinOp((x, y) => x * y, TypeConversions.PrimitiveMul);
            _hdlMap[OpCodes.Mul_Ovf] = () => HandleBinOp((x, y) => x * y, TypeConversions.PrimitiveMul);
            _hdlMap[OpCodes.Mul_Ovf_Un] = () => HandleBinOp((x, y) => x * y, TypeConversions.PrimitiveMul);
            _hdlMap[OpCodes.Neg] = () => HandleUnOp(x => -x, TypeConversions.PrimitiveNeg);
            _hdlMap[OpCodes.Newarr] = HandleNewArr;
            _hdlMap[OpCodes.Newobj] = HandleNewObj;
            _hdlMap[OpCodes.Nop] = () => { };
            _hdlMap[OpCodes.Not] = () => HandleUnOp(x => ~x, TypeConversions.PrimitiveNot);
            _hdlMap[OpCodes.Or] = () => HandleBinOp((x, y) => x | y, TypeConversions.PrimitiveOr);
            _hdlMap[OpCodes.Pop] = HandlePop;
            _hdlMap[OpCodes.Prefix1] = () => { };
            _hdlMap[OpCodes.Prefix2] = () => { };
            _hdlMap[OpCodes.Prefix3] = () => { };
            _hdlMap[OpCodes.Prefix4] = () => { };
            _hdlMap[OpCodes.Prefix5] = () => { };
            _hdlMap[OpCodes.Prefix6] = () => { };
            _hdlMap[OpCodes.Prefix7] = () => { };
            _hdlMap[OpCodes.Prefixref] = () => { };
            _hdlMap[OpCodes.Readonly] = Unsupported;
            _hdlMap[OpCodes.Refanytype] = Unsupported;
            _hdlMap[OpCodes.Refanyval] = Unsupported;
            _hdlMap[OpCodes.Rem] = () => HandleBinOp((x, y) => x % y, TypeConversions.PrimitiveRem);
            _hdlMap[OpCodes.Rem_Un] = () => HandleBinOp((x, y) => x % y, TypeConversions.PrimitiveRem);
            _hdlMap[OpCodes.Ret] = () => HandleReturn();
            _hdlMap[OpCodes.Rethrow] = Unsupported;
            _hdlMap[OpCodes.Shl] = () => HandleShl();
            _hdlMap[OpCodes.Shr] = () => HandleShr();
            _hdlMap[OpCodes.Shr_Un] = () => HandleShr();
            _hdlMap[OpCodes.Sizeof] = Unsupported;
            _hdlMap[OpCodes.Starg] = () => HandleStArg((int)_curILI.Operand);
            _hdlMap[OpCodes.Starg_S] = () => HandleStArg((byte)_curILI.Operand);
            _hdlMap[OpCodes.Stelem] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_I] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_I1] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_I2] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_I4] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_I8] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_R4] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_R8] = () => HandleStelem();
            _hdlMap[OpCodes.Stelem_Ref] = () => HandleStelem();
            _hdlMap[OpCodes.Stfld] = HandleStfld;
            _hdlMap[OpCodes.Stind_I] = HandleStobj;
            _hdlMap[OpCodes.Stind_I1] = HandleStobj;
            _hdlMap[OpCodes.Stind_I2] = HandleStobj;
            _hdlMap[OpCodes.Stind_I4] = HandleStobj;
            _hdlMap[OpCodes.Stind_I8] = HandleStobj;
            _hdlMap[OpCodes.Stind_R4] = HandleStobj;
            _hdlMap[OpCodes.Stind_R8] = HandleStobj;
            _hdlMap[OpCodes.Stind_Ref] = HandleStobj;
            _hdlMap[OpCodes.Stloc] = () => HandleStLoc((int)_curILI.Operand);
            _hdlMap[OpCodes.Stloc_0] = () => HandleStLoc(0);
            _hdlMap[OpCodes.Stloc_1] = () => HandleStLoc(1);
            _hdlMap[OpCodes.Stloc_2] = () => HandleStLoc(2);
            _hdlMap[OpCodes.Stloc_3] = () => HandleStLoc(3);
            _hdlMap[OpCodes.Stloc_S] = () => HandleStLoc((byte)_curILI.Operand);
            _hdlMap[OpCodes.Stobj] = HandleStobj;
            _hdlMap[OpCodes.Stsfld] = HandleStoreStaticField;
            _hdlMap[OpCodes.Sub] = () => HandleBinOp((x, y) => x - y, TypeConversions.PrimitiveSub);
            _hdlMap[OpCodes.Sub_Ovf] = () => HandleBinOp((x, y) => x - y, TypeConversions.PrimitiveSub);
            _hdlMap[OpCodes.Sub_Ovf_Un] = () => HandleBinOp((x, y) => x - y, TypeConversions.PrimitiveSub);
            _hdlMap[OpCodes.Switch] = HandleSwitch;
            _hdlMap[OpCodes.Tailcall] = Unsupported;
            _hdlMap[OpCodes.Throw] = Unsupported;
            _hdlMap[OpCodes.Unaligned] = () => { };
            _hdlMap[OpCodes.Unbox] = () => { };
            _hdlMap[OpCodes.Unbox_Any] = () => { };
            _hdlMap[OpCodes.Volatile] = () => { };
            _hdlMap[OpCodes.Xor] = () => HandleBinOp((x, y) => x ^ y, TypeConversions.PrimitiveXor);
        }
        #endregion

        private int _recurseCount;

        private Expression Simplify(Expression e)
        {
            Contract.Requires<ArgumentNullException>(e != null);

            var es = e.Simplify();
            es.CopyAttributesFrom(e);
            return es;
        }

        private bool ImplementIf(IfInfo ifi, out StackElement spill)
        {
            Expression cond;
            object sample = null;
            if (ifi.UnconditionalTarget != null)
            {
                DeclareBlock(ifi.UnconditionalTarget);
                if (_estk.Count > 0)
                {
                    spill = Pop();
                    return true;
                }
                else
                {
                    spill = default(StackElement);
                    return false;
                }
            }
            else
            {
                bool isSimple = true;
                Variable tmp;
                var e1 = default(StackElement);
                var e2 = default(StackElement);
                IAlgorithmBuilder inner1, inner2;
                bool haveSpill = false;
                IfStatement ifstmt = If(ifi.Condition);
                {
                    DeclareBlock(ifi.ThenTarget);
                    if (_estk.Count > 0)
                    {
                        e1 = Pop();
                        isSimple = !HaveAnyStatement;
                        haveSpill = true;
                    }
                    inner1 = BeginSubAlgorithm();
                }
                Else();
                {
                    DeclareBlock(ifi.ElseTarget);
                    if (_estk.Count > 0)
                    {
                        Debug.Assert(haveSpill);
                        e2 = Pop();
                        isSimple = isSimple && !HaveAnyStatement;
                    }
                    else
                    {
                        Debug.Assert(!haveSpill);
                    }
                    inner2 = BeginSubAlgorithm();
                }
                EndIf();

                // The branches might add logical conjunctions/disjunctions to the condition.
                // => Update condition.
                ifstmt.Conditions[0] = ifi.Condition;

                if (haveSpill)
                {
                    EqualizeTypes(ref e1, ref e2, true);
                    if (isSimple && e1.Expr.ResultType.CILType.Equals(typeof(bool)))
                    {
                        // Special treatment for boolean-valued conditionals:
                        //   y = f ? a : b
                        // is equivalent to:
                        //   y = (f && a) || (!f && b)
                        cond = (ifi.Condition & e1.Expr) | (!ifi.Condition & e2.Expr);
                        cond = Simplify(cond);
                        cond.ResultType = typeof(bool);
                        RemoveLastStatement();
                        ifi.IsConditional = true;
                    }
                    else if (isSimple && !DisallowConditionals)
                    {
                        cond = Expression.Conditional(ifi.Condition, e1.Expr, e2.Expr);
                        RemoveLastStatement();
                        ifi.IsConditional = true;
                    }
                    else
                    {
                        tmp = AllocLocalVariable(e1.Expr.ResultType);
                        inner1.Store(tmp, e1.Expr);
                        inner2.Store(tmp, e2.Expr);
                        cond = tmp;
                    }

                    if (ifi.ConditionSample != null)
                        sample = (bool)ifi.ConditionSample ? e1.Sample : e2.Sample;

                    spill = new StackElement(cond, sample, EVariability.ExternVariable);
                    return true;
                }
                else
                {
                    spill = default(StackElement);
                    return false;
                }
            }
        }

        private StackElement ImplementSpilloverIf(IfInfo ifi)
        {
            Expression cond;
            object sample = null;
            if (ifi.UnconditionalTarget != null)
            {
                DeclareBlock(ifi.UnconditionalTarget);
                StackElement top = Pop();
                cond = top.Expr;
                sample = top.Sample;
            }
            else
            {
                bool isSimple = true;
                Variable tmp;
                StackElement e1, e2;
                IAlgorithmBuilder inner1, inner2;
                IfStatement ifstmt = If(ifi.Condition);
                {
                    DeclareBlock(ifi.ThenTarget);
                    e1 = Pop();
                    isSimple = !HaveAnyStatement;
                    inner1 = BeginSubAlgorithm();
                }
                Else();
                {
                    DeclareBlock(ifi.ElseTarget);
                    e2 = Pop();
                    isSimple = isSimple && !HaveAnyStatement;
                    inner2 = BeginSubAlgorithm();
                }
                EndIf();

                // The branches might add logical conjunctions/disjunctions to the condition.
                // => Update condition.
                ifstmt.Conditions[0] = ifi.Condition;

                EqualizeTypes(ref e1, ref e2, true);
                if (isSimple && e1.Expr.ResultType.CILType.Equals(typeof(bool)))
                {
                    // Special treatment for boolean-valued conditionals:
                    //   y = f ? a : b
                    // is equivalent to:
                    //   y = (f && a) || (!f && b)
                    cond = (ifi.Condition & e1.Expr) | (!ifi.Condition & e2.Expr);
                    cond = Simplify(cond);
                    cond.ResultType = typeof(bool);
                    RemoveLastStatement();
                    ifi.IsConditional = true;
                }
                else if (isSimple && !DisallowConditionals)
                {
                    cond = Expression.Conditional(ifi.Condition, e1.Expr, e2.Expr);
                    RemoveLastStatement();
                    ifi.IsConditional = true;
                }
                else
                {
                    tmp = AllocLocalVariable(e1.Expr.ResultType);
                    inner1.Store(tmp, e1.Expr);
                    inner2.Store(tmp, e2.Expr);
                    cond = tmp;
                }

                if (ifi.ConditionSample != null)
                    sample = (bool)ifi.ConditionSample ? e1.Sample : e2.Sample;
            }

            return new StackElement(cond, sample, EVariability.ExternVariable);
        }

        private void DeclareBlock(MSILCodeBlock cb)
        {
            //Debug.Assert(++_recurseCount < 600);
            if (++_recurseCount >= 600)
            {
                Debugger.Break();
            }

            bool isLoopBlock = cb.IsLoop && 
                ((!UnrollAt(cb) && !DisallowLoops) || cb.UnrollDepth >= MaxUnrollDepth);
            if (isLoopBlock)
            {
                LoopBlock block = Loop();
                block.Label = "L" + cb.StartIndex;
                LoopInfo li = new LoopInfo()
                {
                    Header = cb,
                    Loop = block
                };
                _loopStack.Push(li);
                // This works for while-style loops only:
                var whileExits = cb.Successors.Where(
                    sb => sb.Header != cb &&
                    sb != cb);
                // This one is for do-style loops:
                var backJumps = cb.Predecessors
                    .Where(sb => sb.Header == cb)
                    .Cast<MSILCodeBlock>();
                var doExits = backJumps.SelectMany(
                    bj => bj.Successors
                        .Where(sb => sb.Header == cb.Header && sb != cb));
                var loopExits = whileExits.Union(doExits);
                Debug.Assert(loopExits.Count() <= 1, "Loop has more than one exit.");
                // Infinite loops don't have an exit, so check
                var loopExit = loopExits.FirstOrDefault();
                if (loopExit == null)
                    loopExit = _code.BasicBlocks.Last();
                _breakStack.Push(new BreakInfo()
                {
                    Stmt = block,
                    BreakNext = loopExit
                });
            }

            IList<ILInstruction> seq = cb.Range;
            _curBB = cb;
            //_beginOfLastStatement = -1;
            foreach (ILInstruction ili in seq)
            {
                // Compilation of switch statements leads to weird control flow.
                // This may currently lead to duplicated decompilations
                //Debug.Assert(_processedIndices.Add(ili.Index));

                _curILI = ili;
                _curILIValid = true;
                _hdlMap[ili.Code]();
                _curILIValid = false;
            }

            if (_ifStack.Count > 0 &&
                _ifStack.Peek().ConditionBlock == cb)
            {
                IfInfo ifi = _ifStack.Peek();
                if (cb.Dominatees.Length == 3 &&
                    cb.Dominatees[2].StackBilance < 0)
                {
                    //FIXME: Assertions below would fire sometimes. However, the decompilation seems to
                    //be correct.
                    /*
                    Debug.Assert(cb.Dominatees[0].AccumulatedStackBilance >= 0);
                    Debug.Assert(cb.Dominatees[0].AccumulatedStackBilance <= 1);
                    Debug.Assert(cb.Dominatees[1].AccumulatedStackBilance >= 0);
                    Debug.Assert(cb.Dominatees[1].AccumulatedStackBilance <= 1);
                    Debug.Assert(cb.Dominatees[0].AccumulatedStackBilance ==
                        cb.Dominatees[1].AccumulatedStackBilance);
                     * */
                    Debug.Assert(cb.SuccessorsWithoutExitBlock.Intersect(cb.Dominatees).Count() == 2);

                    MSILCodeBlock foll = cb.Dominatees[2];
                    _nextStack.Push(foll);
                    Push(ImplementSpilloverIf(ifi));
                    DeclareBlock(foll);
                    _nextStack.Pop();
                }
                else if (cb.Dominatees.Length == 1 &&
                    cb.Dominatees[0].StackBilance > 0)
                {
                    MSILCodeBlock dom = cb.Dominatees[0];
                    Debug.Assert(dom.StackBilance == 1);
                    Debug.Assert(_ifStack.Count >= 2);
                    Debug.Assert(dom == ifi.ThenTarget || dom == ifi.ElseTarget);
                    DeclareBlock(dom);
                    Debug.Assert(_estk.Count >= 1);
                    IfInfo ifi0 = _ifStack.ElementAt(1);
                    if (dom == ifi.ThenTarget)
                    {
                        Debug.Assert(ifi.ElseTarget == ifi0.ThenTarget);
                        if (ifi.ElseTarget == ifi0.ThenTarget)
                        {
                            ifi0.Condition = (ifi0.Condition | !ifi.Condition).Simplify();
                            ifi0.ConditionSample = TypeConversions.PrimitiveOr(
                                ifi0.ConditionSample,
                                TypeConversions.PrimitiveNot(ifi.ConditionSample));
                        }
                        //Debug.Assert(ifi.ElseTarget == ifi0.ElseTarget);
                        //ifi0.Condition = ifi0.Condition & ifi.Condition;
                    }
                    else
                    {
                        Debug.Assert(ifi.ThenTarget == ifi0.ThenTarget);
                        if (ifi.ThenTarget == ifi0.ThenTarget)
                        {
                            ifi0.Condition = (ifi0.Condition | ifi.Condition).Simplify();
                            ifi0.ConditionSample = TypeConversions.PrimitiveOr(
                                ifi0.ConditionSample, ifi.ConditionSample);
                        }
                        //Debug.Assert(ifi.ThenTarget == ifi0.ThenTarget);
                        //ifi0.Condition = ifi0.Condition | ifi.Condition;
                    }
                    _ifStack.Pop();
                    _ifStack.Pop();
                    ifi0.Condition.AddAttribute(new ILIndexRef(Method, CurrentILIndex));
                    _ifStack.Push(ifi0);
                    _ifStack.Push(ifi);
                }
                else if (cb.Dominatees.Length == 1)
                {
                    /* Possibilities:
                     * 
                     * (1) If (...)
                     *       If (Condition)
                     *         ThenTarget (== dom)
                     *       EndIf
                     *     EndIf (== ElseTarget)
                     *       
                     * (2) If (...)
                     *       If (!Condition)
                     *         ElseTarget (== dom)
                     *       EndIf
                     *     EndIf (== ThenTarget)
                     *      
                     * (3) L: Loop
                     *       If (Condition)
                     *         continue L (== ThenTarget)
                     *       EndIf
                     *       ElseTarget (== dom)
                     *     EndLoop
                     *     
                     * (4) L: Loop
                     *       If (!Condition)
                     *         continue L (== ElseTarget)
                     *       EndIf
                     *       ThenTarget (== dom)
                     *     EndLoop
                     * */

                    MSILCodeBlock dom = cb.Dominatees.Single();
                    if (ifi.ThenTarget.IsLoop)
                    {
                        Debug.Assert(dom == ifi.ElseTarget);

                        if (ifi.UnconditionalTarget == null)
                        {
                            If(ifi.Condition);
                        }
                        if (ifi.UnconditionalTarget != ifi.ElseTarget)
                        {
                            // "continue"
                            ImplementBranch(ifi.ThenTarget);
                        }
                        if (ifi.UnconditionalTarget == null)
                        {
                            EndIf();
                        }
                        ImplementBranch(ifi.ElseTarget);
                    }
                    else if (ifi.ElseTarget.IsLoop)
                    {
                        Debug.Assert(dom == ifi.ThenTarget);
                        Debugger.Break(); // not sure whether this branch is correct...

                        if (ifi.UnconditionalTarget == null)
                        {
                            If(Simplify((!ifi.Condition)));
                        }
                        if (ifi.UnconditionalTarget != ifi.ElseTarget)
                        {
                            // "continue"
                            ImplementBranch(ifi.ThenTarget);
                        }
                        if (ifi.UnconditionalTarget == null)
                        {
                            EndIf();
                        }
                        ImplementBranch(ifi.ElseTarget);
                    }
                    else if (dom == ifi.ThenTarget)
                    {
                        //Debugger.Break(); // what about ElseTarget???

                        if (ifi.UnconditionalTarget == null)
                        {
                            If(ifi.Condition);
                        }
                        if (ifi.UnconditionalTarget == ifi.ThenTarget)
                        {
                            ImplementBranch(ifi.ThenTarget);
                        }
                        if (ifi.UnconditionalTarget == null)
                        {
                            EndIf();
                        }
                    }
                    else
                    {
                        Debug.Assert(dom == ifi.ElseTarget);
                        //Debugger.Break(); // what about ThenTarget???

                        if (ifi.UnconditionalTarget == null)
                        {
                            If(Simplify((!ifi.Condition)));
                        }
                        if (ifi.UnconditionalTarget != ifi.ThenTarget)
                        {
                            ImplementBranch(ifi.ElseTarget);
                        }
                        if (ifi.UnconditionalTarget == null)
                        {
                            EndIf();
                        }
                    }
                }
                else if (cb.Dominatees.Length == 2)
                {
                    /* Three possibilities:
                     * 
                     * (1) If (Condition)
                     *       ThenTarget
                     *     EndIf
                     *     ElseTarget
                     *     
                     * (2) If (!Condition)
                     *       ElseTarget
                     *     EndIf
                     *     ThenTarget
                     *     
                     * (3) If (Condition)
                     *       ThenTarget
                     *       <end of execution>
                     *     Else
                     *       ElseTarget
                     *       <end of execution>
                     *     EndIf
                     *     <not reached>
                     *     
                     * */

                    if (ifi.UnconditionalTarget != null)
                    {
                        ImplementBranch(ifi.UnconditionalTarget);
                    }
                    else if (ifi.ThenTarget.IsAncestor(ifi.ElseTarget))
                    {
                        // ThenTarget comes before ElseTarget => option 1
                        _nextStack.Push(ifi.ElseTarget);
                        If(ifi.Condition);
                        ImplementBranch(ifi.ThenTarget);
                        EndIf();
                        _nextStack.Pop();
                        ImplementBranch(ifi.ElseTarget);
                    }
                    else if (ifi.ElseTarget.IsAncestor(ifi.ThenTarget))
                    {
                        // ThenTarget comes after ElseTarget => option 2
                        _nextStack.Push(ifi.ThenTarget);
                        If(Simplify((!ifi.Condition)));
                        ImplementBranch(ifi.ElseTarget);
                        EndIf();
                        _nextStack.Pop();
                        ImplementBranch(ifi.ThenTarget);
                    }
                    else
                    {
                        // both are unrelated => option 3
                        _nextStack.Push(Code.BasicBlocks.Last());
                        if (ifi.UnconditionalTarget != null)
                        {
                            ImplementBranch(ifi.UnconditionalTarget);
                        }
                        else
                        {
                            If(ifi.Condition);
                            {
                                ImplementBranch(ifi.ThenTarget);
                            }
                            Else();
                            {
                                ImplementBranch(ifi.ElseTarget);
                            }
                            EndIf();
                        }
                        _nextStack.Pop();
                    }
                }
                else if (cb.Dominatees.Length == 0)
                {
                    /* None of the successors is dominated by the condition block.
                     * The only explanation is that each successors belongs to a loop header.
                     * 
                     * ===> ImplementBranch will probably insert "continue" statements.
                     * */

                    _nextStack.Push(Code.BasicBlocks.Last());
                    if (ifi.UnconditionalTarget != null)
                    {
                        ImplementBranch(ifi.UnconditionalTarget);
                    }
                    else
                    {
                        If(ifi.Condition);
                        {
                            ImplementBranch(ifi.ThenTarget);
                        }
                        Else();
                        {
                            ImplementBranch(ifi.ElseTarget);
                        }
                        EndIf();
                    }
                    _nextStack.Pop();
                }
                else
                {
                    // Standard case: two branches
                    // FIXME: Actually, we'd expect cb.Dominatees.Length == 3.
                    // However, some weird situation occurs for switch statements.
                    //   switch (prefix)
                    // is compiled like:
                    //   if (prefix < 0)
                    //     goto default case
                    //   else if (prefix > max)
                    //     goto default case
                    //   else switch (prefix)
                    //     case 0: ...
                    //     ...
                    //     default case: ...
                    // 
                    // This makes the if statement being additionally dominated by default case AND switch statement successor.                                        
                    Debug.Assert(cb.Dominatees.Length >= 3);

                    MSILCodeBlock foll = cb.Dominatees.Except(cb.Successors).Last();

                    _nextStack.Push(foll);

                    StackElement spill;
                    if (ImplementIf(ifi, out spill))
                    {
                        Push(spill);
                    }
                    _nextStack.Pop();
                    if (!foll.IsExitBlock)
                    {
                        _curBB = cb;
                        ImplementBranch(foll);
                    }
                }
                _ifStack.Pop();
            }
            else if (_switchStack.Count > 0 && _switchStack.Peek().Condition == cb)
            {
                MSILCodeBlock[] succs = cb.SuccessorsWithoutExitBlock;
                var cont = Code.GetBasicBlockStartingAt(cb.EndIndex + 1);
                var foll = TrackBranchChain(cont);
                _nextStack.Push(foll);
                _breakStack.Push(new BreakInfo()
                {
                    Stmt = _switchStack.Peek().Case,
                    BreakNext = _nextStack.Peek()
                });

                var si = _switchStack.Peek();
                for (long i = 0; i < succs.Length; i++)
                {
                    if (i == succs.Length - 1)
                    {
                        DefaultCase();
                    }
                    else
                    {
                        var litValue = TypeConversions.ConvertValue(
                            i + si.SelOffset,
                            si.Case.Selector.ResultType.CILType);
                        Case(LiteralReference.CreateConstant(litValue));
                    }
                    ImplementBranch(si.SwitchTargets[i]);
                    EndCase();
                }
                EndSwitch();
                _nextStack.Pop();
                _breakStack.Pop();
                _switchStack.Pop();
            }
            else
            {
                var succ = cb.SuccessorsWithoutExitBlock.SingleOrDefault();
                if (succ != null)
                {
                    ImplementBranch(succ);
                }
            }

            if (isLoopBlock)
            {
                EndLoop();
                LoopInfo li = _loopStack.Pop();
                var bi = _breakStack.Pop();
                if (bi.BreakNext != _code.BasicBlocks.Last())
                    DeclareBlock(bi.BreakNext);
            }

            --_recurseCount;
        }

        private void CreateJumpLabels()
        {
            //int labelIdx = 0;
            foreach (KeyValuePair<GotoStatement, int> kvp in _gotoTargets)
            {
                int targetIdx = kvp.Value;
                Statement target;
                if (_stmtMap.TryGetValue(targetIdx, out target))
                {
                    kvp.Key.Target = target;
                    if (target.Label == null)
                    {
                        target.Label = "label" + targetIdx;
                        //++labelIdx;
                    }
                }
                else
                {
                    ReportError("unable to reach target " + kvp.Value);
                    //throw new NotImplementedException("unable to reach target " + kvp.Value);
                }
            }
        }

        private int _nextLocIndex;
        private int _firstTempLocalIndex;

        private MethodFacts _facts;
        public MethodFacts Facts
        {
            get
            {
                if (_facts == null)
                    _facts = FactUniverse.Instance.GetFacts(Method);
                return _facts;
            }
        }

        private VariabilityAnalyzer _vara;
        public VariabilityAnalyzer VARA
        {
            get
            {
                if (_vara == null)
                    _vara = Facts.GetVARA(new VariabilityPattern(ArgumentVariabilities));
                return _vara;
            }
        }

        protected override void DeclareAlgorithm()
        {
            // Initialization tasks
            InitSpecialOpHandlers();

            // Extract arguments
            ParameterInfo[] args = _method.GetParameters();
            List<Literal> arglist = new List<Literal>();
            if (!_method.IsStatic)
            {
                if (GenerateThisVariable)
                {
                    Variable thisv = new Variable(TypeDescriptor.MakeType(_method.DeclaringType))
                    {
                        Name = "@this"
                    };
                    _thisRef = thisv;
                    DeclareThis(thisv);
                }
                else
                {
                    _thisRef = new ThisRef(_method.DeclaringType, Instance);
                }
                arglist.Add((Literal)_thisRef.ReferencedObject);
            }
            ArgumentDescriptor[] argds = Decompilee.GetArguments().ToArray();
            foreach (ParameterInfo arg in args)
            {
                var argl = argds[arg.Position].Argument;
                _argmap[arg.Name] = argl;
                if (arg.IsOut || arg.IsRetval)
                    DeclareOutput(argl);
                else
                    DeclareInput(argl);
                arglist.Add((Literal)argl);
                int pos = arg.Position;
                ArgumentValues[pos] = TypeConversions.ConvertValue(ArgumentValues[pos], arg.ParameterType);
                if (ArgumentValues[pos] != null)
                {
                    var argType = TypeDescriptor.GetTypeOf(ArgumentValues[pos]);
                    if (arg.IsOut)
                        argType = argType.AsByRefType();
                    var argv = argl as Variable;
                    if (argv != null)
                        argv.UpgradeType(argType);
                }
            }
            if (!MethodReturnType.Equals(typeof(void)) &&
                TreatReturnValueAsVariable)
            {
                Variable retv = new Variable(MethodReturnType)
                {
                    Name = "@return",
                    LocalIndex = -1
                };
                _argmap[retv.Name] = retv;
                arglist.Add(retv);
                DeclareOutput(retv);
            }
            _arglist = arglist.ToArray();
            _body = _method.GetMethodBody();

            // Extract local variables
            IList<LocalVariableInfo> locals = _body.LocalVariables;
            foreach (LocalVariableInfo local in locals)
            {
                _nextLocIndex = Math.Max(_nextLocIndex, local.LocalIndex + 1);
                var rwdecl = local.LocalType.GetCustomOrInjectedAttribute<RewriteDeclaration>();
                if (rwdecl != null)
                    rwdecl.ImplementDeclaration(local, this);
            }
            _firstTempLocalIndex = _nextLocIndex;

            // Extract loop unroll information
            foreach (int i in Facts.UnrollHeaders)
                _unrollHeaders[i] = true;
            foreach (int i in Facts.NonUnrollHeaders)
                _unrollHeaders[i] = false;

            // Recurse
            _nextStack.Push(_code.BasicBlocks.Last());
            DeclareBlock(_entry);

            // Post-process
            CreateJumpLabels();
            CompleteLocalTypes();
            DeclareReferencedLocals();
        }

        private void CompleteLocalTypes()
        {
            foreach (KeyValuePair<Tuple<int, int>, LocVarInfo> kvp in _locmap)
            {
                LocVarInfo lvi = kvp.Value;
                object[] values = GetLocalVarAssignedValues(lvi.Var);
                Type vtype = lvi.Var.Type.CILType;
                IEnumerable<object> nnvalues = values.Where(x => x != null && x.GetType().Equals(vtype));
                object nnvalue = nnvalues.FirstOrDefault();
                if (nnvalue != null)
                {
                    TypeDescriptor td = TypeDescriptor.GetTypeOf(nnvalue);
                    bool conflict = false;
                    foreach (object nnvalue_ in nnvalues)
                    {
                        TypeDescriptor tdTemp = TypeDescriptor.GetTypeOf(nnvalue_);
                        if (!tdTemp.Equals(td))
                        {
                            ReportError(Method.Name + ": Conflicting static type properties for local variable " +
                                lvi.Var.Name + " (" + td.Name + " vs. " + tdTemp.Name + ")");
                            conflict = true;
                        }
                    }
                    if (!conflict)
                        lvi.Var.UpgradeType(td);
                }
            }
        }

        private void DeclareReferencedLocals()
        {
            int nextIndex = 0;
            foreach (LocVarInfo lvi in _locmap.Values)
            {
                if (lvi.IsReferenced() && !_hiddenLocals.Contains(lvi.Var.LocalIndex))
                {
                    Variable v = lvi.Var;
                    DeclareLocal(v);
                    ++nextIndex;
                }
            }
        }

        protected StackElement ThisRef
        {
            get
            {
                return new StackElement(_thisRef, Instance,
                    FactUniverse.Instance.GetFacts(Method.DeclaringType).IsMutable ?
                    EVariability.ExternVariable : EVariability.Constant);
            }
        }

        public ICollection<MethodCallInfo> CalledMethods
        {
            get
            {
                Contract.Assume(_calledMethods != null);

                return new ReadOnlyCollection<MethodCallInfo>(_calledMethods.ToList());
            }
        }

        public ICollection<FieldRefInfo> ReferencedFields
        {
            get
            {
                return new ReadOnlyCollection<FieldRefInfo>(_referencedFields.Values.ToList());
            }
        }

        public Expression[] GetLocalVarAssignedExprs(IStorable item)
        {
            Expression[] result = (from AssignmentInfo ai in _varAsmts
                                   where ai.LHS.Equals(item)
                                   select ai.RHS).ToArray();
            return result;
        }

        public object[] GetLocalVarAssignedValues(IStorable item)
        {
            object[] result = (from AssignmentInfo ai in _varAsmts
                               where ai.LHS.Equals(item)
                               select ai.RHSSample).ToArray();
            return result;
        }

        public Expression[] GetLocalVarAssignedExprs(Variable v, int ilIndex)
        {
            IEnumerable<int> writePoints =
                Facts.DFA.GetAssignmentsForReadPoint(v.LocalIndex, ilIndex);
            IEnumerable<Expression> result =
                writePoints.Select(wp =>
                    _varAsmts
                        .Where(asmt => asmt.ILIndex == wp)
                        .Select(asmt => asmt.RHS)
                        .FirstOrDefault());
            return result.ToArray();
        }

        public Expression GetLocalVarAssignedExprAtInstrIndex(int index)
        {
            return (from AssignmentInfo ai in _varAsmts
                    where ai.ILIndex == index
                    select ai.RHS).FirstOrDefault();
        }

        public ReadOnlyCollection<object> ReturnedValues
        {
            get
            {
                return new ReadOnlyCollection<object>(_retAsmts.Select(x => x.Sample).ToList());
            }
        }

        public ReadOnlyCollection<Variable> LocalVariables
        {
            get
            {
                return new ReadOnlyCollection<Variable>(
                    (from LocVarInfo lvi in _locmap.Values
                     where lvi.IsReferenced()
                     select lvi.Var).ToList());
            }
        }

        public ReadOnlyCollection<IStorable> Arguments
        {
            get
            {
                Contract.Assume(_arglist != null);

                return new ReadOnlyCollection<IStorable>(_arglist.Select(x => (IStorable)x).ToList());
            }
        }

        public Expression ResolveVariableReference(int ilIndex, Expression expr)
        {
            LiteralReference lref = expr as LiteralReference;
            if (lref != null)
            {
                Variable v = lref.ReferencedObject as Variable;
                if (v != null)
                {
                    Expression[] rvalues = GetLocalVarAssignedExprs(v, ilIndex);

                    // Check if variable is uniquely assigned
                    if (rvalues.Length != 1)
                        return expr;

                    Expression rvalue = rvalues.Single();

                    // Check if r-value was already decompiled
                    if (rvalue != null)
                        return rvalue;
                }
            }
            return expr;
        }

        public int CurrentILIndex { get { return _curILI.Index; } }

        public LocalVariableState ExportLocalVariableState()
        {
            return new LocalVariableState(_localVarState, _doNotUnrollVars);
        }

        public void ImportLocalVariableState(LocalVariableState lvs)
        {
            _localVarState.Clear();
            foreach (var kvp in lvs.State)
                _localVarState[kvp.Key] = kvp.Value;
            _processedIndices.Clear();
        }

        public void HideLocal(LocalVariableInfo lvi)
        {
            _hiddenLocals.Add(lvi.LocalIndex);
        }

        public void DoNotUnroll(int localIndex)
        {
            _doNotUnrollVars.Add(localIndex);
        }

        public bool IsUnrollInhibited(int localIndex)
        {
            return _doNotUnrollVars.Contains(localIndex);
        }

        public bool TryGetReturnValueSample(MethodInfo callee, StackElement[] inArgs, out object[] outArgs, out object result)
        {
            outArgs = new object[inArgs.Length];
            result = null;

            int offs = 0;
            if (callee.CallingConvention.HasFlag(CallingConventions.HasThis))
            {
                object sample = inArgs[0].Sample;
                if (sample == null)
                    return false;
                outArgs[0] = sample;
                offs = 1;
            }
            foreach (var pi in callee.GetParameters())
            {
                int idx = pi.Position + offs;
                object sample = inArgs[idx].Sample;
                var ga = pi.GetCustomOrInjectedAttribute<IGuardedArgument>();
                if (ga != null)
                    sample = ga.CorrectArgument(sample);

                if (sample == null && pi.ParameterType.IsValueType)
                    return false;

                outArgs[idx] = sample;
            }
            try
            {
                result = callee.Invoke(outArgs);
                return true;
            }
            catch (TargetException)
            {
                result = null;
                return false;
            }
            catch (TargetInvocationException)
            {
                result = null;
                return false;
            }
        }

        private AttributedObject _attrHost = new AttributedObject();

        public void AddAttribute(object attr)
        {
            _attrHost.AddAttribute(attr);
        }

        public bool RemoveAttribute<T>()
        {
            return _attrHost.RemoveAttribute<T>();
        }

        public T QueryAttribute<T>()
        {
            return _attrHost.QueryAttribute<T>();
        }

        public bool HasAttribute<T>()
        {
            return _attrHost.HasAttribute<T>();
        }

        public IEnumerable<object> Attributes
        {
            get { return _attrHost.Attributes; }
        }

        public void CopyAttributesFrom(IAttributed other)
        {
            _attrHost.CopyAttributesFrom(other);
        }
    }

    public interface IDecompilationResult
    {
        Function Decompiled { get; }
        ICollection<MethodCallInfo> CalledMethods { get; }
        ICollection<FieldRefInfo> ReferencedFields { get; }
        //IEnumerable<object> GetLocalVarAssignedValues(IStorable var);
        //IEnumerable<object> ReturnedValues { get; }
    }

    public class MSILDecompiler : IDecompilationResult
    {
        public CodeDescriptor CodeDesc { get; private set; }
        public MethodCode Code { get; private set; }
        public MethodBase Method { get; private set; }
        public object Instance { get; private set; }
        public object[] ArgumentValues { get; private set; }
        public EVariability[] ArgumentVariabilities { get; private set; }
        public MSILDecompilerTemplate Template { get; set; }

        public Function Decompiled { get; private set; }

        public ICollection<MethodCallInfo> CalledMethods
        {
            get { return Template.CalledMethods; }
        }

        public ICollection<FieldRefInfo> ReferencedFields
        {
            get { return Template.ReferencedFields; }
        }

        public IEnumerable<object> GetLocalVarAssignedValues(IStorable var)
        {
            return Template.GetLocalVarAssignedValues(var);
        }

        public IEnumerable<object> ReturnedValues
        {
            get { return Template.ReturnedValues; }
        }

        public MSILDecompiler(CodeDescriptor codeDesc, object instance,
            object[] argValues, EVariability[] argVariabilities)
        {
            CodeDesc = codeDesc;
            Method = codeDesc.Method;
            Code = MethodCode.Create(codeDesc.Method);
            Instance = instance;
            ArgumentValues = argValues;
            ArgumentVariabilities = argVariabilities;
            Template = new MSILDecompilerTemplate();
        }

        public MSILDecompiler(CodeDescriptor codeDesc, MethodCode code, object instance)
        {
            CodeDesc = codeDesc;
            Method = codeDesc.Method;
            Code = code;
            Instance = instance;
            ArgumentValues = new object[0];
            ArgumentVariabilities = new EVariability[0];

            Template = new MSILDecompilerTemplate();
        }

        public MSILDecompiler(CodeDescriptor codeDesc, MethodCode code, object instance,
            object[] argValues, EVariability[] argVariabilities)
        {
            CodeDesc = codeDesc;
            Method = codeDesc.Method;
            Code = code;
            Instance = instance;
            ArgumentValues = argValues;
            ArgumentVariabilities = argVariabilities;

            Template = new MSILDecompilerTemplate();
        }

        public IDecompilationResult Decompile()
        {
            Template.Instance = Instance;
            Template.ArgumentValues = ArgumentValues;
            Template.ArgumentVariabilities = ArgumentVariabilities;
            Template.Decompilee = CodeDesc;
            Template.Method = Method;
            Template.Code = Code;

            IEnumerable<IOnDecompilation> odcattrs = Method.GetCustomAndInjectedAttributes<IOnDecompilation>();
            if (CodeDesc.AsyncMethod != null)
                odcattrs = odcattrs.Concat(CodeDesc.AsyncMethod.GetCustomAndInjectedAttributes<IOnDecompilation>());

            foreach (var odcattr in odcattrs)
                odcattr.OnDecompilation(Template);

            Function result = Template.GetAlgorithm();

            // Remove eliminated statements
            result.Body = result.Body.Clone;

            // Beautify
            result.Body = result.Body.BeautifyIfStatements();

            Decompiled = result;

            if (CodeDesc != null)
                CodeDesc.Implementation = result;

            return this;
        }
    }
}

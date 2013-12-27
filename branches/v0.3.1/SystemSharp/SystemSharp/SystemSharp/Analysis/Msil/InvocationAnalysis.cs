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
using ElementSources = System.Collections.Generic.IEnumerable<SystemSharp.Analysis.ElementSource>;
using StackState = SystemSharp.Analysis.Msil.AbstractStackState<System.Collections.Generic.IEnumerable<SystemSharp.Analysis.ElementSource>>;

namespace SystemSharp.Analysis.Msil
{
    static class StackStates
    {
        /// <summary>
        /// Constructs the stack state which results from pushing a given value to the stack
        /// </summary>
        /// <param name="me">current stack state</param>
        /// <param name="obj">value to push</param>
        /// <returns>the resulting stack state</returns>
        public static StackState Push(this StackState me, ElementSource obj)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(obj != null);

            return new PushStackState<ElementSources>(me, Enumerable.Repeat(obj, 1));
        }

        /// <summary>
        /// Constructs the stack state which results from assigning a given value to a specific local variable
        /// </summary>
        /// <param name="me">current stack state</param>
        /// <param name="localIndex">0-based index of local variable receiving the assignment</param>
        /// <param name="rvalue">value to assign</param>
        /// <returns>the resulting stack state</returns>
        public static StackState Assign(this StackState me, int localIndex, ElementSource rvalue)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(rvalue != null);
            Contract.Requires<ArgumentException>(localIndex >= 0);

            return new AsmtStackState<ElementSources>(me, localIndex, Enumerable.Repeat(rvalue, 1));
        }

        /// <summary>
        /// Constructs the stack state which results from possibly assigning a given value to a specific local variable.
        /// I.e. afterwards, the variable nondeterministically retains its former value or the new one.
        /// </summary>
        /// <param name="me">current stack state</param>
        /// <param name="localIndex">0-based index of local variable receiving the potential assignment</param>
        /// <param name="rvalue">value which is possibly assigned</param>
        /// <returns>the resulting stack state</returns>
        public static StackState AddAssign(this StackState me, int localIndex, ElementSource rvalue)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(rvalue != null);
            Contract.Requires<ArgumentException>(localIndex >= 0);

            return new AsmtStackState<ElementSources>(me, localIndex,
                me.GetLocal(localIndex).Union(Enumerable.Repeat(rvalue, 1)));
        }

        /// <summary>
        /// Constructs the stack state which results from possibly assigning one of multiple given values to a specific local variable.
        /// I.e. afterwards, the variable nondeterministically retains its former value or one of the new values.
        /// </summary>
        /// <param name="me">current stack state</param>
        /// <param name="localIndex">0-based index of local variable receiving the potential assignment</param>
        /// <param name="rvalue">values to be possibly assigned</param>
        /// <returns>the resulting stack state</returns>
        public static StackState AddAssign(this StackState me, int localIndex, ElementSources rvalues)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(rvalues != null);
            Contract.Requires<ArgumentException>(localIndex >= 0);

            return new AsmtStackState<ElementSources>(me, localIndex, me.GetLocal(localIndex).Union(rvalues));
        }

        /// <summary>
        /// Constructs the stack state which results from assigning a specific value to a given method argument.
        /// </summary>
        /// <param name="me">current stack state</param>
        /// <param name="argIndex">0-based index of argument being assigned</param>
        /// <param name="rvalue">value to be assigned</param>
        /// <returns>the resulting stack state</returns>
        public static StackState AssignArg(this StackState me, int argIndex, ElementSource rvalue)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(rvalue != null);
            Contract.Requires<ArgumentException>(argIndex >= 0);

            return new ArgAsmtStackState<ElementSources>(me, argIndex, Enumerable.Repeat(rvalue, 1));
        }

        /// <summary>
        /// Constructs the stack state which results from possibly assigning a specific value to a given method argument.
        /// I.e. afterwards, the argument nondeterministically retains its former value or the new one.
        /// </summary>
        /// <param name="me">current stack state</param>
        /// <param name="argIndex">0-based index of argument receiving the possible assignment</param>
        /// <param name="rvalue">value to be possibly assigned</param>
        /// <returns>the resulting stack state</returns>
        public static StackState AddAssignArg(this StackState me, int argIndex, ElementSource rvalue)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(rvalue != null);
            Contract.Requires<ArgumentException>(argIndex >= 0);

            return new ArgAsmtStackState<ElementSources>(me, argIndex, 
                me.GetArgument(argIndex).Union(Enumerable.Repeat(rvalue, 1)));
        }

        /// <summary>
        /// Constructs the stack state which results from possibly assigning one of multiple values to a given method argument.
        /// I.e. afterwards, the argument nondeterministically retains its former value or one of the supplied values.
        /// </summary>
        /// <param name="me">current stack state</param>
        /// <param name="argIndex">0-based index of argument receiving the possible assignment</param>
        /// <param name="rvalues">values to be possibly assigned</param>
        /// <returns>the resulting stack state</returns>
        public static StackState AddAssignArg(this StackState me, int argIndex, ElementSources rvalues)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(rvalues != null);
            Contract.Requires<ArgumentException>(argIndex >= 0);

            return new ArgAsmtStackState<ElementSources>(me, argIndex,
                me.GetArgument(argIndex).Union(rvalues));
        }
    }

    /// <summary>
    /// Represents a precise program location where some method is called.
    /// </summary>
    public class CallSite
    {
        /// <summary>
        /// Returns the calling method.
        /// </summary>
        public MethodBase Caller { get; private set; }

        /// <summary>
        /// Returns the called method.
        /// </summary>
        public MethodBase Callee { get; private set; }

        /// <summary>
        /// Returns the IL location ("bytecode offset") where the call is made.
        /// </summary>
        public int ILIndex { get; private set; }

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="caller">the calling method</param>
        /// <param name="callee">the called method</param>
        /// <param name="ilIndex">the IL location ("bytecode offset") where the call is made</param>
        public CallSite(MethodBase caller, MethodBase callee, int ilIndex)
        {
            Caller = caller;
            Callee = callee;
            ILIndex = ilIndex;
        }
    }

    /// <summary>
    /// Implements a rather comprehensive static program analysis for determining which fields and local variables are potentially read and/or modified, 
    /// which local variables are potentially referenced by their addresses, which methods are potentially called, which classes are potentially instantiated, 
    /// and which CIL types are referenced in some way.
    /// </summary>
    public class InvocationAnalyzer: FixPointAnalyzer<ElementSources>
    {
        private class IndependentStackState : IndependentStackStateBase<ElementSources>
        {
            internal IndependentStackState(MethodBase method)
            {
                _stack = new List<ElementSources>();
                ElementSources noRef = Enumerable.Repeat((ElementSource)ElementSource.NoRef, 1);
                int numLocals = method.GetMethodBody().LocalVariables.Count;
                _locals = Enumerable.Repeat(noRef, numLocals).ToList();
                ParameterInfo[] arguments = method.GetParameters();
                IEnumerable<ElementSources> args =
                    arguments.Select(ai => Enumerable.Repeat(ElementSource.ForArgument(ai), 1));
                bool hasThis = method.CallingConvention.HasFlag(CallingConventions.HasThis);
                if (hasThis)
                    args = Enumerable.Repeat(Enumerable.Repeat((ElementSource)new ThisSource(method), 1), 1).Union(args);
                _args = args.ToList();
            }

            internal IndependentStackState(List<ElementSources> stack, List<ElementSources> locals, List<ElementSources> args)
            {
                _stack = stack;
                _locals = locals;
                _args = args;
            }
        }

        /// <summary>
        /// Returns an observation stream of all element sources encountered during analysis.
        /// </summary>
        public IObservable<ElementSource> ElementSources { get; private set; }

        /// <summary>
        /// Returns an observation stream of all apparently equivalent element sources encountered during analysis.
        /// </summary>
        public IObservable<Tuple<ElementSource, ElementSources>> Equivalences { get; private set; }

        /// <summary>
        /// Returns an observation stream of all fields which are read by analyzed method.
        /// </summary>
        public IObservable<FieldInfo> ReadFields { get; private set; }

        /// <summary>
        /// Returns an observation stream of all fields which are written by analyzed method.
        /// </summary>
        public IObservable<FieldInfo> WrittenFields { get; private set; }

        /// <summary>
        /// Returns an observation stream of all fields which are read or written by analyzed method.
        /// </summary>
        public IObservable<FieldInfo> ReferencedFields { get; private set; }

        /// <summary>
        /// Returns an observation stream of all methods which are called by analyzed method.
        /// </summary>
        public IObservable<CallSite> CalledMethods { get; private set; }

        /// <summary>
        /// Returns an observation stream of all constructors which are called by analyzed method.
        /// </summary>
        public IObservable<ConstructorInfo> ConstructedObjects { get; private set; }

        /// <summary>
        /// Returns an observation stream of all methods and constructors which are called by analyzed method.
        /// </summary>
        public IObservable<MethodBase> CalledMethodBases { get; private set; }

        /// <summary>
        /// Returns an observation stream of all array types which are created by analyzed method.
        /// </summary>
        public IObservable<Type> ConstructedArrays { get; private set; }

        /// <summary>
        /// Returns an observation stream of all types which are references by analyzed method.
        /// </summary>
        public IObservable<Type> ReferencedTypes { get; private set; }

        /// <summary>
        /// Returns an observation stream of all element mutations which are performed by analyzed method.
        /// </summary>
        public IObservable<ElementMutation> Mutations { get; private set; }

        /// <summary>
        /// Returns an observation stream of all local variable mutations which are performed by analyzed method.
        /// </summary>
        public IObservable<LocalMutation> LocalMutations { get; private set; }

        /// <summary>
        /// Returns an observation stream of all address loads performed by analyzed method.
        /// </summary>
        public IObservable<IndirectLoad> IndirectLoads { get; private set; }

        private Subject<ElementSource> _objectSources = new Subject<ElementSource>();
        private Subject<Tuple<ElementSource, ElementSources>> _equivalences = new Subject<Tuple<ElementSource, ElementSources>>();
        private Subject<FieldInfo> _readFields = new Subject<FieldInfo>();
        private Subject<FieldInfo> _writtenFields = new Subject<FieldInfo>();
        private Subject<FieldInfo> _referencedFields = new Subject<FieldInfo>();
        private Subject<CallSite> _calledMethods = new Subject<CallSite>();
        private Subject<ConstructorInfo> _constructedObjects = new Subject<ConstructorInfo>();
        private Subject<Type> _constructedArrays = new Subject<Type>();
        private Subject<Type> _referencedTypes = new Subject<Type>();
        private Subject<ElementMutation> _mutations = new Subject<ElementMutation>();
        private Subject<LocalMutation> _localMutations = new Subject<LocalMutation>();
        private Subject<IndirectLoad> _indirectLoads = new Subject<IndirectLoad>();

        /// <summary>
        /// Constructs an instance based on the method to be analyzed.
        /// </summary>
        /// <param name="method">the method to be analyzed</param>
        public InvocationAnalyzer(MethodBase method):
            base(method)
        {
            ElementSources = _objectSources.AsObservable();
            Equivalences = _equivalences;
            ReadFields = _readFields.AsObservable();
            WrittenFields = _writtenFields.AsObservable();
            ReferencedFields = _referencedFields.AsObservable();
            CalledMethods = _calledMethods.AsObservable();
            ConstructedObjects = _constructedObjects.AsObservable();
            ConstructedArrays = _constructedArrays.AsObservable();
            ReferencedTypes = _referencedTypes.AsObservable();
            Mutations = _mutations.AsObservable();
            LocalMutations = _localMutations.AsObservable();
            IndirectLoads = _indirectLoads.AsObservable();
            CalledMethodBases = CalledMethods.Cast<MethodBase>().Concat(ConstructedObjects.Cast<MethodBase>());
            InitializePropagators();
        }

        private void InitializePropagators()
        {
            _pmap[OpCodes.Add] = Pop2PushNoRef;
            _pmap[OpCodes.Add_Ovf] = Pop2PushNoRef;
            _pmap[OpCodes.Add_Ovf_Un] = Pop2PushNoRef;
            _pmap[OpCodes.And] = Pop2PushNoRef;
            _pmap[OpCodes.Arglist] = Unsupported;
            _pmap[OpCodes.Beq] = Pop2;
            _pmap[OpCodes.Beq_S] = Pop2;
            _pmap[OpCodes.Bge] = Pop2;
            _pmap[OpCodes.Bge_S] = Pop2;
            _pmap[OpCodes.Bge_Un] = Pop2;
            _pmap[OpCodes.Bge_Un_S] = Pop2;
            _pmap[OpCodes.Bgt] = Pop2;
            _pmap[OpCodes.Bgt_S] = Pop2;
            _pmap[OpCodes.Bgt_Un] = Pop2;
            _pmap[OpCodes.Bgt_Un_S] = Pop2;
            _pmap[OpCodes.Ble] = Pop2;
            _pmap[OpCodes.Ble_S] = Pop2;
            _pmap[OpCodes.Ble_Un] = Pop2;
            _pmap[OpCodes.Ble_Un_S] = Pop2;
            _pmap[OpCodes.Blt] = Pop2;
            _pmap[OpCodes.Blt_S] = Pop2;
            _pmap[OpCodes.Blt_Un] = Pop2;
            _pmap[OpCodes.Blt_Un_S] = Pop2;
            _pmap[OpCodes.Bne_Un] = Pop2;
            _pmap[OpCodes.Bne_Un_S] = Pop2;
            _pmap[OpCodes.Box] = HandleBox;
            _pmap[OpCodes.Br] = Nop;
            _pmap[OpCodes.Br_S] = Nop;
            _pmap[OpCodes.Break] = Nop;
            _pmap[OpCodes.Brfalse] = Pop1;
            _pmap[OpCodes.Brfalse_S] = Pop1;
            _pmap[OpCodes.Brtrue] = Pop1;
            _pmap[OpCodes.Brtrue_S] = Pop1;
            _pmap[OpCodes.Call] = (i, p) => HandleCall(i, p, false);
            _pmap[OpCodes.Calli] = (i, p) => HandleCall(i, p, true);
            _pmap[OpCodes.Callvirt] = (i, p) => HandleCall(i, p, false);
            _pmap[OpCodes.Castclass] = HandleCast;
            _pmap[OpCodes.Ceq] = Pop2PushNoRef;
            _pmap[OpCodes.Cgt] = Pop2PushNoRef;
            _pmap[OpCodes.Cgt_Un] = Pop2PushNoRef;
            _pmap[OpCodes.Ckfinite] = Nop;
            _pmap[OpCodes.Clt] = Pop2PushNoRef;
            _pmap[OpCodes.Clt_Un] = Pop2PushNoRef;
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
            _pmap[OpCodes.Div] = Pop2PushNoRef;
            _pmap[OpCodes.Div_Un] = Pop2PushNoRef;
            _pmap[OpCodes.Dup] = HandleDup;
            _pmap[OpCodes.Endfilter] = Pop1;
            _pmap[OpCodes.Endfinally] = Nop;
            _pmap[OpCodes.Initblk] = Pop3;
            _pmap[OpCodes.Initobj] = Pop1;
            _pmap[OpCodes.Isinst] = HandleIsinst;
            _pmap[OpCodes.Jmp] = Nop;
            _pmap[OpCodes.Ldarg] = (i, p) => HandleLdarg(i, p, (int)i.Operand);
            _pmap[OpCodes.Ldarg_0] = (i, p) => HandleLdarg(i, p, 0);
            _pmap[OpCodes.Ldarg_1] = (i, p) => HandleLdarg(i, p, 1);
            _pmap[OpCodes.Ldarg_2] = (i, p) => HandleLdarg(i, p, 2);
            _pmap[OpCodes.Ldarg_3] = (i, p) => HandleLdarg(i, p, 3);
            _pmap[OpCodes.Ldarg_S] = (i, p) => HandleLdarg(i, p, (byte)i.Operand);
            _pmap[OpCodes.Ldarga] = (i, p) => HandleLdarga(i, p, (int)i.Operand);
            _pmap[OpCodes.Ldarga_S] = (i, p) => HandleLdarga(i, p, (byte)i.Operand);
            _pmap[OpCodes.Ldc_I4] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_0] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_1] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_2] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_3] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_4] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_5] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_6] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_7] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_8] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_M1] = PushNoRef;
            _pmap[OpCodes.Ldc_I4_S] = PushNoRef;
            _pmap[OpCodes.Ldc_I8] = PushNoRef;
            _pmap[OpCodes.Ldc_R4] = PushNoRef;
            _pmap[OpCodes.Ldc_R8] = PushNoRef;
            _pmap[OpCodes.Ldelem] = HandleLdelem;
            _pmap[OpCodes.Ldelem_I] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_I1] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_I2] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_I4] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_I8] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_R4] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_R8] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_Ref] = HandleLdelem;
            _pmap[OpCodes.Ldelem_U1] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_U2] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelem_U4] = Pop2PushNoRef;
            _pmap[OpCodes.Ldelema] = HandleLdelema;
            _pmap[OpCodes.Ldfld] = HandleLdfld;
            _pmap[OpCodes.Ldflda] = HandleLdflda;
            _pmap[OpCodes.Ldftn] = HandleLdftn;
            _pmap[OpCodes.Ldind_I] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_I1] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_I2] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_I4] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_I8] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_R4] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_R8] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_Ref] = (i, p) => HandleLdind(i, p, true);
            _pmap[OpCodes.Ldind_U1] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_U2] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldind_U4] = (i, p) => HandleLdind(i, p, false);
            _pmap[OpCodes.Ldlen] = Pop1PushNoRef;
            _pmap[OpCodes.Ldloc] = (i, p) => HandleLdloc(i, p, (int)i.Operand);
            _pmap[OpCodes.Ldloc_0] = (i, p) => HandleLdloc(i, p, 0);
            _pmap[OpCodes.Ldloc_1] = (i, p) => HandleLdloc(i, p, 1);
            _pmap[OpCodes.Ldloc_2] = (i, p) => HandleLdloc(i, p, 2);
            _pmap[OpCodes.Ldloc_3] = (i, p) => HandleLdloc(i, p, 3);
            _pmap[OpCodes.Ldloc_S] = (i, p) => HandleLdloc(i, p, (byte)i.Operand);
            _pmap[OpCodes.Ldloca] = (i, p) => HandleLdloca(i, p, (int)i.Operand);
            _pmap[OpCodes.Ldloca_S] = (i, p) => HandleLdloca(i, p, (byte)i.Operand);
            _pmap[OpCodes.Ldnull] = HandleLdnull;
            _pmap[OpCodes.Ldobj] = (i, p) => HandleLdind(i, p, true);
            _pmap[OpCodes.Ldsfld] = HandleLdsfld;
            _pmap[OpCodes.Ldsflda] = HandleLdsflda;
            _pmap[OpCodes.Ldstr] = HandleLdstr;
            _pmap[OpCodes.Ldtoken] = PushNoRef;
            _pmap[OpCodes.Ldvirtftn] = Pop1PushAnyPtr;
            _pmap[OpCodes.Leave] = Nop;
            _pmap[OpCodes.Leave_S] = Nop;
            _pmap[OpCodes.Localloc] = Pop1PushAnyPtr;
            _pmap[OpCodes.Mkrefany] = Pop1PushNoRef;
            _pmap[OpCodes.Mul] = Pop2PushNoRef;
            _pmap[OpCodes.Mul_Ovf] = Pop2PushNoRef;
            _pmap[OpCodes.Mul_Ovf_Un] = Pop2PushNoRef;
            _pmap[OpCodes.Neg] = Pop1PushNoRef;
            _pmap[OpCodes.Newarr] = HandleNewarr;
            _pmap[OpCodes.Newobj] = HandleNewobj;
            _pmap[OpCodes.Nop] = Nop;
            _pmap[OpCodes.Not] = Pop1PushNoRef;
            _pmap[OpCodes.Or] = Pop2PushNoRef;
            _pmap[OpCodes.Pop] = Pop1;
            _pmap[OpCodes.Prefix1] = Unsupported;
            _pmap[OpCodes.Prefix2] = Unsupported;
            _pmap[OpCodes.Prefix3] = Unsupported;
            _pmap[OpCodes.Prefix4] = Unsupported;
            _pmap[OpCodes.Prefix5] = Unsupported;
            _pmap[OpCodes.Prefix6] = Unsupported;
            _pmap[OpCodes.Prefix7] = Unsupported;
            _pmap[OpCodes.Prefixref] = Unsupported;
            _pmap[OpCodes.Readonly] = Nop;
            _pmap[OpCodes.Refanytype] = HandleRefanytype;
            _pmap[OpCodes.Refanyval] = Pop1PushAnyPtr;
            _pmap[OpCodes.Rem] = Pop2PushNoRef;
            _pmap[OpCodes.Rem_Un] = Pop2PushNoRef;
            _pmap[OpCodes.Ret] = HandleRet;
            _pmap[OpCodes.Rethrow] = Nop;
            _pmap[OpCodes.Shl] = Pop2PushNoRef;
            _pmap[OpCodes.Shr] = Pop2PushNoRef;
            _pmap[OpCodes.Shr_Un] = Pop2PushNoRef;
            _pmap[OpCodes.Sizeof] = Pop1PushNoRef;
            _pmap[OpCodes.Starg] = (i, p) => HandleStarg(i, p, (int)i.Operand);
            _pmap[OpCodes.Starg_S] = (i, p) => HandleStarg(i, p, (byte)i.Operand);
            _pmap[OpCodes.Stelem] = HandleStelem;
            _pmap[OpCodes.Stelem_I] = HandleStelem;
            _pmap[OpCodes.Stelem_I1] = HandleStelem;
            _pmap[OpCodes.Stelem_I2] = HandleStelem;
            _pmap[OpCodes.Stelem_I4] = HandleStelem;
            _pmap[OpCodes.Stelem_I8] = HandleStelem;
            _pmap[OpCodes.Stelem_R4] = HandleStelem;
            _pmap[OpCodes.Stelem_R8] = HandleStelem;
            _pmap[OpCodes.Stelem_Ref] = HandleStelem;
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
            _pmap[OpCodes.Stsfld] = HandleStsfld;
            _pmap[OpCodes.Sub] = Pop2PushNoRef;
            _pmap[OpCodes.Sub_Ovf] = Pop2PushNoRef;
            _pmap[OpCodes.Sub_Ovf_Un] = Pop2PushNoRef;
            _pmap[OpCodes.Switch] = Pop1;
            _pmap[OpCodes.Tailcall] = Nop;
            _pmap[OpCodes.Throw] = Pop1;
            _pmap[OpCodes.Unaligned] = Nop;
            _pmap[OpCodes.Unbox] = Pop1PushNoRef;
            _pmap[OpCodes.Unbox_Any] = Pop1PushNoRef;
            _pmap[OpCodes.Volatile] = Nop;
            _pmap[OpCodes.Xor] = Pop2PushNoRef;
        }

        protected override AbstractStackState<ElementSources> CreateInitialStackState()
        {
            return new IndependentStackState(Method);
        }

        private static bool Merge(Func<int, ElementSources> a,
            Func<int, ElementSources> b,
            int count,
            out List<ElementSources> m)
        {
            bool changed = false;
            m = new List<ElementSources>();
            for (int i = 0; i < count; i++)
            {
                ElementSources ai = a(i);
                ElementSources elems = ai.Union(b(i)).Distinct();
                if (elems.Count() != ai.Count())
                    changed = true;
                m.Add(elems.ToArray());
            }
            return changed;
        }

        protected override bool Merge(StackState a, StackState b, out StackState m)
        {
            int depth = a.Depth;

            List<ElementSources> stack = new List<ElementSources>();
            List<ElementSources> locals = new List<ElementSources>();
            List<ElementSources> args = new List<ElementSources>();
            bool changed = Merge(i => a[i], i => b[i], depth, out stack);
            if (Merge(i => a.GetLocal(i), i => b.GetLocal(i), a.NumLocals, out locals))
                changed = true;
            if (Merge(i => a.GetArgument(i), i => b.GetArgument(i), a.NumArguments, out args))
                changed = true;
            m = new IndependentStackState(stack, locals, args);
            Debug.Assert(m.Depth == a.Depth);
            Debug.Assert(m.NumLocals == a.NumLocals);
            Debug.Assert(m.NumArguments == a.NumArguments);

            return changed;
        }

        private void PublishObjectSource(ElementSource source)
        {
            _objectSources.OnNext(source);
        }

        private void PublishType(Type type)
        {
            _referencedTypes.OnNext(type);
        }

        private void PublishNewObject(ConstructorInfo ctor)
        {
            _constructedObjects.OnNext(ctor);
        }

        private void PublishNewArray(Type elemType)
        {
            _constructedArrays.OnNext(elemType);
        }

        private void PublishEquivalence(ElementSources sources, ElementSource rsource)
        {
            _equivalences.OnNext(Tuple.Create(rsource, sources));
        }

        private void PublishMutation(ElementMutation mutation)
        {
            _mutations.OnNext(mutation);
        }

        private void PublishLocalMutation(LocalMutation mutation)
        {
            _localMutations.OnNext(mutation);
        }

        private void PublishIndirectLoad(IndirectLoad iload)
        {
            _indirectLoads.OnNext(iload);
        }

        private void PublishMethodCall(MethodBase method, int ilIndex)
        {
            _calledMethods.OnNext(new CallSite(Method, method, ilIndex));
        }

        #region Propagation handlers

        private StackState Pop2PushNoRef(ILInstruction ili, StackState pre)
        {
            return pre.Pop().Pop().Push(ElementSource.NoRef);
        }

        private StackState Pop2(ILInstruction ili, StackState pre)
        {
            return pre.Pop().Pop();
        }

        private StackState Pop1(ILInstruction ili, StackState pre)
        {
            return pre.Pop();
        }

        private StackState Unsupported(ILInstruction ili, StackState pre)
        {
            throw new NotSupportedException();
        }

        private StackState HandleBox(ILInstruction ili, StackState pre)
        {
            Type targetType = (Type)ili.Operand;
            BoxSite boxs = new BoxSite(Method, ili.Index, targetType);
            PublishType(targetType);
            PublishObjectSource(boxs);
            return pre.Pop().Push(boxs);
        }

        private StackState Nop(ILInstruction ili, StackState pre)
        {
            return pre;
        }

        private StackState Pop3(ILInstruction ili, StackState pre)
        {
            return pre.Pop().Pop().Pop();
        }

        private StackState HandleCast(ILInstruction ili, StackState pre)
        {
            Type targetClass = (Type)ili.Operand;
            PublishType(targetClass);
            return pre;
        }

        private StackState HandleCall(ILInstruction ili, StackState pre, bool isCalli)
        {
            MethodBase callee = (MethodBase)ili.Operand;
            PublishType(callee.DeclaringType);
            PublishMethodCall(callee, ili.Index);
            ParameterInfo[] args = callee.GetParameters();
            int firstArg = callee.CallingConvention.HasFlag(CallingConventions.HasThis) ? -1 : 0;
            int numParams = args.Length - firstArg;
            int offs = isCalli ? -1 : 0;
            int numElems = numParams - offs;
            StackState next = pre;
            for (int i = 0; i < numElems; i++)
            {
                ElementSources top = next[0];
                int j = i + offs;
                if (j >= 0)
                {
                    int argIdx = args.Length - j - 1;
                    ElementSource rsource;
                    if (argIdx >= 0)
                    {
                        ParameterInfo arg = args[argIdx];
                        rsource = ElementSource.ForArgument(arg);
                        if (arg.ParameterType.IsByRef)
                        {
                            foreach (ElementSource source in top)
                            {
                                Debug.Assert(source is PointerSource);
                                PointerSource psource = (PointerSource)source;
                                ElementSource csource = new ArgumentReturnSource(arg);
                                IMutationSource msrc = psource as IMutationSource;
                                if (msrc != null)
                                {
                                    ElementSources csources = Enumerable.Repeat(csource, 1);
                                    foreach (ElementMutation mut in msrc.CreateMutations(ili.Index, arg.IsOut, csources))
                                    {
                                        if (mut is LocalMutation)
                                            PublishLocalMutation((LocalMutation)mut);
                                        if (mut is IModifiesStackState)
                                        {
                                            IModifiesStackState mss = (IModifiesStackState)mut;
                                            next = mss.ModifyStackState(next, csources, (a, b) => a.Union(b).Distinct());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // "this"
                        rsource = new ThisSource(callee);
                    }
                    ElementSources lsources = top.SelectMany(s =>
                    {
                        if (s is AddressOfArgumentSource)
                            return pre.GetArgument(((AddressOfArgumentSource)s).Argument.Position);
                        else if (s is AddressOfLocalVariableSource)
                            return pre.GetLocal(((AddressOfLocalVariableSource)s).LocalIndex);
                        else
                            return Enumerable.Repeat(s, 1);
                    });
                    PublishEquivalence(lsources, rsource);
                }
                next = next.Pop();
            }
            Type returnType;
            if (callee.IsFunction(out returnType))
            {
                next = next.Push(new MethodReturnSource((MethodInfo)callee));
            }
            return next;
        }

        private StackState HandleCpblk(ILInstruction ili, StackState pre)
        {
            var numBytes = pre[0];
            var sources = pre[1];
            var dests = pre[2];
            DerefSource dsource = new DerefSource(sources.Cast<PointerSource>());
            foreach (ElementSource dest in dests)
            {
                Debug.Assert(dest is PointerSource);
                IMutationSource msrc = dest as IMutationSource;
                if (msrc != null)
                {
                    foreach (ElementMutation mut in msrc.CreateMutations(ili.Index, dests.Count() == 1, sources))
                    {
                        if (mut is LocalMutation)
                            PublishLocalMutation((LocalMutation)mut);
                        else
                            PublishMutation(mut);
                    }
                }
                else
                {
                    PointerSource pdest = (PointerSource)dest;
                    IndirectMutation mut = new IndirectMutation(pdest, Enumerable.Repeat(dsource, 1));
                    PublishMutation(mut);
                }
            }
            StackState next = pre.Pop().Pop().Pop();
            return next;
        }

        private StackState HandleCpobj(ILInstruction ili, StackState pre)
        {
            var sources = pre[0];
            var dests = pre[1];
            DerefSource dsource = new DerefSource(sources.Cast<PointerSource>());
            foreach (ElementSource dest in dests)
            {
                IMutationSource msrc = dest as IMutationSource;
                if (msrc != null)
                {
                    foreach (ElementMutation mut in msrc.CreateMutations(ili.Index, dests.Count() == 1, sources))
                    {
                        if (mut is LocalMutation)
                            PublishLocalMutation((LocalMutation)mut);
                        else
                            PublishMutation(mut);
                    }
                }
                else
                {
                    PointerSource pdest = (PointerSource)dest;
                    IndirectMutation mut = new IndirectMutation(pdest, Enumerable.Repeat(dsource, 1));
                    PublishMutation(mut);
                }
            }
            
            StackState next = pre.Pop().Pop();
            return next;
        }

        private StackState HandleDup(ILInstruction ili, StackState pre)
        {
            return pre.Push(pre[0]);
        }

        private StackState HandleIsinst(ILInstruction ili, StackState pre)
        {
            Type testType = (Type)ili.Operand;
            PublishType(testType);
            return pre.Pop().Push(ElementSource.NoRef);
        }

        private StackState Pop1PushNoRef(ILInstruction ili, StackState pre)
        {
            return pre.Pop().Push(ElementSource.NoRef);
        }

        private StackState HandleLdarg(ILInstruction ili, StackState pre, int index)
        {
            if (Method.CallingConvention.HasFlag(CallingConventions.HasThis))
                --index;
            if (index < 0)
            {
                if (Method.DeclaringType.IsValueType)
                    return pre.Push(new ThisPointerSource(Method));
                else
                    return pre.Push(new ThisSource(Method));
            }
            else
            {
                ParameterInfo arg = Method.GetParameters()[index];
                return pre.Push(ElementSource.ForArgument(arg));
            }
        }

        private StackState HandleLdarga(ILInstruction ili, StackState pre, int index)
        {
            if (Method.CallingConvention.HasFlag(CallingConventions.HasThis))
                --index;
            if (index < 0)
                throw new InvalidProgramException("Method trying to load address of 'this' argument");
            ParameterInfo arg = Method.GetParameters()[index];
            return pre.Push(new AddressOfArgumentSource(arg));
        }

        private StackState PushNoRef(ILInstruction ili, StackState pre)
        {
            return pre.Push(ElementSource.NoRef);
        }

        private StackState HandleLdelem(ILInstruction ili, StackState pre)
        {
            var index = pre[0];
            var array = pre[1];
            Type elemType = (Type)ili.Operand;
            ElementSource elem = new ArrayElementSource(array.Cast<ObjectSource>());
            return pre.Pop().Pop().Push(elem);
        }

        private StackState HandleLdelema(ILInstruction ili, StackState pre)
        {
            var index = pre[0];
            var array = pre[1];
            Type elemType = (Type)ili.Operand;
            ElementSource elem = new AddressOfArrayElementSource(array.Cast<ObjectSource>());
            return pre.Pop().Pop().Push(elem);
        }

        private StackState HandleLdfld(ILInstruction ili, StackState pre)
        {
            FieldInfo field = (FieldInfo)ili.Operand;
            PublishType(field.FieldType);
            _readFields.OnNext(field);
            ElementSources instances = pre[0];
            ElementSource elem;
            if (field.FieldType.IsPrimitive)
                elem = ElementSource.NoRef;
            else
                elem = new FieldSource(instances, field);
            return pre.Pop().Push(elem);
        }

        private StackState HandleLdflda(ILInstruction ili, StackState pre)
        {
            FieldInfo field = (FieldInfo)ili.Operand;
            PublishType(field.FieldType);
            _referencedFields.OnNext(field);
            ElementSources instances = pre[0];
            ElementSource elem = new AddressOfFieldSource(instances, field);
            return pre.Pop().Push(elem);
        }

        private StackState HandleLdftn(ILInstruction ili, StackState pre)
        {
            MethodBase method = (MethodBase)ili.Operand;
            PublishMethodCall(method, ili.Index);
            return pre.Push(ElementSource.AnyPtr);
        }

        private StackState HandleLdind(ILInstruction ili, StackState pre, bool isRefResult)
        {
            ElementSources addresses = pre[0];
            Debug.Assert(addresses.All(src => src is ElementSource));
            IEnumerable<PointerSource> pointers = addresses.Cast<PointerSource>();
            DerefSource dsource = new DerefSource(pointers);
            PublishIndirectLoad(new IndirectLoad(Method, ili.Index, dsource));
            if (isRefResult)
                return pre.Pop().Push(dsource);
            else
                return pre.Pop().Push(ElementSource.NoRef);
        }

        private StackState HandleLdloc(ILInstruction ili, StackState pre, int index)
        {
            Contract.Requires<ArgumentNullException>(ili != null);
            Contract.Requires<ArgumentNullException>(pre != null);
            Contract.Requires<ArgumentException>(index >= 0);

            ElementSources lvalues = pre.GetLocal(index);
            return pre.Push(lvalues);
        }

        private StackState HandleLdloca(ILInstruction ili, StackState pre, int index)
        {
            AddressOfLocalVariableSource alsrc = new AddressOfLocalVariableSource(Method, index);
            return pre.Push(alsrc);
        }

        private StackState HandleLdnull(ILInstruction ili, StackState pre)
        {
            return pre.Push(ElementSource.Nil);
        }

        private StackState HandleLdsfld(ILInstruction ili, StackState pre)
        {
            FieldInfo field = (FieldInfo)ili.Operand;
            PublishType(field.FieldType);
            _readFields.OnNext(field);
            ElementSource elem;
            if (field.FieldType.IsPrimitive)
                elem = ElementSource.NoRef;
            else
                elem = new FieldSource(Enumerable.Empty<ObjectSource>(), field);
            return pre.Push(elem);
        }

        private StackState HandleLdsflda(ILInstruction ili, StackState pre)
        {
            FieldInfo field = (FieldInfo)ili.Operand;
            PublishType(field.FieldType);
            _referencedFields.OnNext(field);
            AddressOfFieldSource afsrc = new AddressOfFieldSource(Enumerable.Empty<ObjectSource>(), field);
            return pre.Push(afsrc);
        }

        private StackState HandleLdstr(ILInstruction ili, StackState pre)
        {
            StringSource ssrc = new StringSource((string)ili.Operand);
            return pre.Push(ssrc);
        }

        private StackState Pop1PushAnyPtr(ILInstruction ili, StackState pre)
        {
            return pre.Pop().Push(ElementSource.AnyPtr);
        }

        private StackState HandleNewarr(ILInstruction ili, StackState pre)
        {
            Type elemType = (Type)ili.Operand;
            PublishType(elemType.MakeArrayType());
            PublishNewArray(elemType);
            NewArraySite elem = new NewArraySite(Method, ili.Index, elemType);
            return pre.Pop().Push(elem);
        }

        private StackState HandleNewobj(ILInstruction ili, StackState pre)
        {
            ConstructorInfo ctor = (ConstructorInfo)ili.Operand;
            PublishType(ctor.DeclaringType);
            PublishNewObject(ctor);
            NewSite elem = new NewSite(Method, ili.Index, ctor);
            ElementSources[] args = new ElementSources[ctor.GetParameters().Length];
            StackState inter = pre;
            for (int i = 0; i < args.Length; i++)
            {
                args[args.Length - i - 1] = pre[i];
                inter = inter.Pop();
            }
            inter = inter.Push(elem);
            for (int i = 0; i < args.Length; i++)
            {
                inter = inter.Push(args[i]);
            }
            inter = HandleCall(ili, inter, false);
            return inter.Push(elem);
        }

        private StackState HandleRefanytype(ILInstruction ili, StackState pre)
        {
            return pre.Pop().Push(ElementSource.TypeToken);
        }

        private StackState HandleRet(ILInstruction ili, StackState pre)
        {
            Type returnType;
            if (Method.IsFunction(out returnType))
            {
                PublishType(returnType);
                ElementSources retVals = pre[0];
                MethodReturnSource mrsrc = new MethodReturnSource((MethodInfo)Method);
                PublishEquivalence(retVals, mrsrc);
                return pre.Pop();
            }
            else
                return pre;
        }

        private StackState HandleStarg(ILInstruction ili, StackState pre, int index)
        {
            var rvalues = pre[0];
            return pre.Pop().AssignArg(index, rvalues);
        }

        private StackState HandleStelem(ILInstruction ili, StackState pre)
        {
            var values = pre[0];
            //var indices = pre[1];
            var arrays = pre[2];
            Debug.Assert(arrays.All(s => s is ObjectSource));
            var ovalues = values.Cast<ObjectSource>();
            foreach (ObjectSource array in arrays.Cast<ObjectSource>())
            {
                var wam = new WriteArrayMutation(array, ovalues);
                PublishMutation(wam);
            }
            return pre.Pop().Pop().Pop();
        }

        private StackState HandleStfld(ILInstruction ili, StackState pre)
        {
            var values = pre[0];
            var instances = pre[1];
            FieldInfo field = (FieldInfo)ili.Operand;
            PublishType(field.FieldType);
            _writtenFields.OnNext(field);
            foreach (ElementSource instance in instances)
            {
                var sfm = new StoreFieldMutation(instance, field, values);
                PublishMutation(sfm);
            }
            return pre.Pop().Pop();
        }

        private StackState HandleStind(ILInstruction ili, StackState pre)
        {
            var values = pre[0];
            var addresses = pre[1];
            StackState next = pre.Pop().Pop();
            foreach (PointerSource address in addresses.Cast<PointerSource>())
            {
                if (address is RefArgumentSource)
                {
                    var rasrc = address as RefArgumentSource;
                    var arsrc = new ArgumentReturnSource(rasrc.Argument);
                    PublishEquivalence(values, arsrc);
                    var im = new IndirectMutation(address, values);
                    PublishMutation(im);
                }
                else if (address is AddressOfArgumentSource)
                {
                    var aasrc = address as AddressOfArgumentSource;
                    if (addresses.Count() == 1)
                        next = next.AssignArg(aasrc.Argument.Position, values);
                    else
                        next = next.AddAssignArg(aasrc.Argument.Position, values);
                    PublishLocalMutation(new ArgumentMutation(Method, ili.Index, 
                        addresses.Count() == 1, aasrc.Argument.Position, values));
                }
                else if (addresses is AddressOfArrayElementSource)
                {
                    var aaesrc = addresses as AddressOfArrayElementSource;
                    foreach (ObjectSource array in aaesrc.ArraySources)
                    {
                        var wam = new WriteArrayMutation(array, values);
                        PublishMutation(wam);
                    }
                }
                else if (addresses is AddressOfFieldSource)
                {
                    var afsrc = address as AddressOfFieldSource;
                    foreach (ObjectSource instance in afsrc.Instances)
                    {
                        var fm = new StoreFieldMutation(instance, afsrc.Field, values);
                        PublishMutation(fm);
                    }
                }
                else if (address is AddressOfLocalVariableSource)
                {
                    var alsrc = address as AddressOfLocalVariableSource;
                    if (addresses.Count() == 1)
                        next = next.Assign(alsrc.LocalIndex, values);
                    else
                        next = next.AddAssign(alsrc.LocalIndex, values);
                    PublishLocalMutation(new LocalVariableMutation(Method, ili.Index,
                        addresses.Count() == 1, alsrc.LocalIndex, values));
                }
                else
                {
                    var im = new IndirectMutation(address, values);
                    PublishMutation(im);
                }
            }
            return pre.Pop().Pop();
        }

        private StackState HandleStloc(ILInstruction ili, StackState pre, int index)
        {
            var values = pre[0];
            return pre.Pop().Assign(index, values);
        }

        private StackState HandleStsfld(ILInstruction ili, StackState pre)
        {
            var values = pre[0];
            FieldInfo field = (FieldInfo)ili.Operand;
            PublishType(field.FieldType);
            _writtenFields.OnNext(field);
            var sfm = new StoreFieldMutation(null, field, values);
            PublishMutation(sfm);
            return pre.Pop();
        }

        #endregion

        public override void Run()
        {
            base.Run();
            _stackStates = null;
            _calledMethods.OnCompleted();
            _constructedArrays.OnCompleted();
            _constructedObjects.OnCompleted();
            _equivalences.OnCompleted();
            _mutations.OnCompleted();
            _objectSources.OnCompleted();
            _readFields.OnCompleted();
            _referencedFields.OnCompleted();
            _referencedTypes.OnCompleted();
            _writtenFields.OnCompleted();
        }
    }
}

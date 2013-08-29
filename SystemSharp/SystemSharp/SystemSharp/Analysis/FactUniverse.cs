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
using System.Reactive;
using System.Reactive.Subjects;
using System.Reflection;
using System.Reactive.Linq;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Analysis.Msil;
using System.Diagnostics.Contracts;
using System.Security;
using System.Reactive.Concurrency;
using SystemSharp.Components;
using SystemSharp.Reporting;
using SDILReader;
using System.Diagnostics;

namespace SystemSharp.Analysis
{
    public interface IAssumeCalled
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited=true, AllowMultiple=true)]
    public class AssumeCalled : Attribute, IAssumeCalled
    {
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class AssumeConst : Attribute
    {
    }

    public enum EMethodDiscoveryStage
    {
        FirstDiscovery,
        Processing
    }

    public interface IOnMethodDiscovery
    {
        void OnDiscovery(MethodBase method, EMethodDiscoveryStage stage);
    }

    public interface IOnMethodCall
    {
        void OnMethodCall(MethodFacts callerFacts, MethodBase callee, int ilIndex);
    }

    [AttributeUsage(AttributeTargets.Method|AttributeTargets.Constructor, Inherited = true, AllowMultiple = true)]
    public class BreakOnMethodDiscovery: Attribute, IOnMethodDiscovery
    {
        #region IOnMethodDiscovery Member

        public void OnDiscovery(MethodBase method, EMethodDiscoveryStage stage)
        {
            System.Diagnostics.Debugger.Break();
        }

        #endregion
    }

    public interface IOnTypeDiscovery
    {
        void OnDiscovery(Type type);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = true)]
    public class BreakOnTypeDiscovery : Attribute, IOnTypeDiscovery
    {
        #region IOnMethodDiscovery Member

        public void OnDiscovery(Type type)
        {
            System.Diagnostics.Debugger.Break();
        }

        #endregion
    }

    public class FactUniverse
    {
        public static FactUniverse Instance
        {
            get { return DesignContext.Instance.Universe; }
        }

        public IObservable<TypeFacts> KnownTypes { get; private set; }
        public IObservable<TypeFacts> KnownInterfaces { get; private set; }
        public IObservable<TypeFacts> KnownAbstractClasses { get; private set; }
        public IObservable<TypeFacts> KnownValueTypes { get; private set; }
        public IObservable<TypeFacts> KnownInstantiables { get; private set; }
        public IObservable<MethodFacts> KnownMethods { get; private set; }
        public IObservable<MethodFacts> KnownConstructors { get; private set; }
        public IObservable<MethodFacts> KnownMethodBases { get; private set; }
        public IObservable<AllocationSite> KnownAllocationSites { get; private set; }

        public bool IsCompleted { get; private set; }

        public const string UniverseCompletedErrorMsg = "Fact universe is already completed";

        private ObservableSet<AllocationSite> _allocSites = new ObservableSet<AllocationSite>();

        private ObservableCache<Type, TypeFacts> _knownTypes;
        private ObservableCache<MethodBase, MethodFacts> _knownMethods;
        private ObservableCache<FieldInfo, FieldFacts> _knownFields;

        private TypeFacts CreateTypeFacts(Type type)
        {
            IOnTypeDiscovery disc = (IOnTypeDiscovery)type.GetCustomOrInjectedAttribute(typeof(IOnTypeDiscovery));
            if (disc != null)
                disc.OnDiscovery(type);

            return new TypeFacts(this, type);
        }

        private MethodFacts CreateMethodFacts(MethodBase method)
        {
            IOnMethodDiscovery disc = (IOnMethodDiscovery)method.GetCustomOrInjectedAttribute(typeof(IOnMethodDiscovery));
            if (disc != null)
                disc.OnDiscovery(method, EMethodDiscoveryStage.FirstDiscovery);

            return new MethodFacts(this, method);
        }

        private FieldFacts CreateFieldFacts(FieldInfo field)
        {
            return new FieldFacts(this, field);
        }

        public FactUniverse()
        {
            _knownTypes = new ObservableCache<Type, TypeFacts>(
                ReflectionHelper.TypeEqualityComparer, CreateTypeFacts);
            _knownMethods = new ObservableCache<MethodBase, MethodFacts>(
                ReflectionHelper.MethodEqualityComparer, CreateMethodFacts);
            _knownFields = new ObservableCache<FieldInfo, FieldFacts>(
                ReflectionHelper.FieldEqualityComparer, CreateFieldFacts);

            KnownTypes = _knownTypes;
            KnownMethodBases = _knownMethods;

            _knownTypes.AutoDo(OnNewType);
            KnownInterfaces = KnownTypes
                .Where(type => type.TheType.IsInterface);

            KnownAbstractClasses = KnownTypes
                .Where(type => type.TheType.IsAbstract);

            KnownValueTypes = KnownTypes
                .Where(type => type.TheType.IsValueType);

            KnownInstantiables = KnownTypes
                .Where(type => !type.TheType.IsInterface && !type.TheType.IsAbstract && !type.TheType.IsGenericParameter);

            KnownMethods = KnownMethodBases
                .Where(method => method.Method is MethodInfo);
            KnownConstructors = KnownMethodBases
                .Where(method => method.Method is ConstructorInfo);

            KnownMethodBases.ObserveOn(Scheduler.CurrentThread).AutoDo(OnNewMethod);

            KnownAllocationSites = _allocSites.AsObservable();
        }

        public void AddType(Type type)
        {
            Contract.Requires(!IsCompleted, UniverseCompletedErrorMsg);
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (Type atype in type.GetGenericArguments())
                    AddType(atype);
                type = type.GetGenericTypeDefinition();
            }
            if (type.IsArray || type.IsPointer || type.IsByRef)
                type = type.GetElementType();
            _knownTypes.Cache(type);
        }

        public void AddMethod(MethodBase method)
        {
            Contract.Requires(!IsCompleted, UniverseCompletedErrorMsg);
            var entryPoint = method.UnwrapEntryPoint();
            _knownMethods.Cache(entryPoint);
        }

        private void AddMethod(CallSite callSite)
        {
            var attrs = callSite.Callee.GetCustomAndInjectedAttributes<IOnMethodCall>();
            if (attrs.Length > 0)
            {
                MethodFacts callerFacts = GetFacts(callSite.Caller);
                foreach (var attr in attrs)
                {
                    attr.OnMethodCall(callerFacts, callSite.Callee, callSite.ILIndex);
                }
            }
            AddMethod(callSite.Callee);
        }

        public void AddConstructor(ConstructorInfo ctor)
        {
            Contract.Requires(!IsCompleted, UniverseCompletedErrorMsg);
            _knownMethods.Cache(ctor);
        }

        public bool HaveFacts(MethodBase method)
        {
            var entryPoint = method.UnwrapEntryPoint(); 
            return _knownMethods.IsCached(entryPoint);
        }

        public void Complete()
        {
            Contract.Requires(!IsCompleted, UniverseCompletedErrorMsg);
            _knownTypes.Complete();
            _knownMethods.Complete();
            _allocSites.Complete();
            Debug.Assert(_knownMethods.Values.All(mf => !mf.IsDecompilable || mf.INVA.AnalysisDone));
            IsCompleted = true;
            PostProcess();
        }

        private void OnNewType(TypeFacts typeFacts)
        {
            Type type = typeFacts.TheType;

            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo method in methods)
            {
                if (method.GetCustomOrInjectedAttribute(typeof(IAssumeCalled)) != null ||
                    method.Name.Contains('<')) // Anonymous methods don't give a chance to attach an attribute.
                {
                    AddMethod(method);
                }
            }
            foreach (Type itype in type.GetInterfaces())
                AddType(itype);
            Type btype = type.BaseType;
            while (btype != null && !btype.Equals(type))
            {
                AddType(btype);
                type = btype;
                btype = type.BaseType;
            }
        }

        private void OnFieldSeen(FieldInfo field)
        {
            _knownFields.Cache(field);
        }

        private void OnNewMethod(MethodFacts mfacts)
        {
            mfacts.OnNewMethodCalled = true;
            MethodBase method = mfacts.Method;
            AddType(method.DeclaringType);
            IOnMethodDiscovery disc = (IOnMethodDiscovery)method.GetCustomOrInjectedAttribute(typeof(IOnMethodDiscovery));
            if (disc != null)
                disc.OnDiscovery(method, EMethodDiscoveryStage.Processing);
            RealizationsOf(method).AutoDo(AddMethod);
            if (mfacts.IsDecompilable)
            {
                mfacts.INVA.CalledMethods.AutoDo(AddMethod);
                mfacts.INVA.ConstructedObjects.AutoDo(AddConstructor);
                mfacts.INVA.ReferencedTypes.AutoDo(AddType);
                mfacts.INVA.ReadFields.AutoDo(OnFieldSeen);
                mfacts.INVA.WrittenFields.AutoDo(OnFieldSeen);
                mfacts.INVA.ReferencedFields.AutoDo(OnFieldSeen);
                mfacts.INVA.ElementSources
                    .Where(src => src is AllocationSite)
                    .Select(src => (AllocationSite)src)
                    .AutoDo(src => _allocSites.Add(src));
                mfacts.INVA.Run();
                if (method is MethodInfo)
                    mfacts.Realizations = RealizationsOf((MethodInfo)method).ToBufferedEnumerable();
                else
                    mfacts.Realizations = Enumerable.Repeat(method, 1);
            }
        }

        public IObservable<Type> RealizationsOf(Type type)
        {
            return KnownTypes
                .Where(t => type.IsAssignableFrom(t.TheType))
                .Select(t => t.TheType);
        }

        public IObservable<MethodBase> RealizationsOf(MethodInfo method)
        {
            if (method.DeclaringType.IsInterface)
            {
                return KnownTypes
                    .Where(t => !(method.DeclaringType.IsGenericType && t.TheType.IsArray)
                        && !t.TheType.IsGenericParameter && !t.TheType.IsInterface
                        && method.DeclaringType.IsAssignableFrom(t.TheType))
                    .Select(t => t.TheType.GetInterfaceMap(method.DeclaringType))
                    .Select(im => Tuple.Create(im, im.InterfaceMethods.ToList().IndexOf(method)))
                    .Where(tup => tup.Item2 >= 0)
                    .Select(tup => tup.Item1.TargetMethods[tup.Item2]);
            }
            else if (method.IsVirtual)
            {
                return
                    RealizationsOf(method.DeclaringType)
                        .SelectMany(t => t.GetMethods(
                                BindingFlags.Public | BindingFlags.NonPublic |
                                BindingFlags.Instance | BindingFlags.Static)
                            .Where(m => m.GetBaseDefinition().MethodHandle == method.MethodHandle));
            }
            else
                return Observable.Return(method);
        }

        public IObservable<MethodBase> RealizationsOf(MethodBase method)
        {
            if (method is MethodInfo)
            {
                IObservable<MethodBase> result = RealizationsOf((MethodInfo)method);
                return result;
            }
            else
                return Observable.Return(method);
        }

        public TypeFacts GetFacts(Type type)
        {
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                type = type.GetGenericTypeDefinition();
            if (type.IsArray || type.IsPointer || type.IsByRef)
                type = type.GetElementType();
            return _knownTypes[type];
        }

        public MethodFacts GetFacts(MethodBase method)
        {
            var entryPoint = method.UnwrapEntryPoint();
            return _knownMethods[entryPoint];
        }

        public FieldFacts GetFacts(FieldInfo field)
        {
            return _knownFields[field];
        }

        private void PostProcess()
        {
            Queue<MethodFacts> q = new Queue<MethodFacts>();
            foreach (MethodFacts mf in _knownMethods.Values)
            {
                foreach (ElementMutation mut in mf.Mutations)
                {
                    foreach (ElementSource term in mut.GetMutatedTerminals())
                    {
                        if (term is IMutableSource)
                        {
                            ((IMutableSource)term).IndicateMutated();
                        }
                    }
                    foreach (ElementSource term in mut.GetSubMutatedTerminals())
                    {
                        if (term is IMutableSource)
                        {
                            ((IMutableSource)term).IndicateSubMutated();
                        }
                    }
                }
                if (mf.IsMutator || mf.IsUnsafe)
                    q.Enqueue(mf);
            }
            while (q.Count > 0)
            {
                MethodFacts mf = q.Dequeue();
                foreach (MethodBase c in mf.CallingMethods)
                {
                    MethodFacts mfc = _knownMethods[c];
                    if (!mfc.IsIndirectMutator)
                    {
                        mfc.IsIndirectMutator = true;
                        q.Enqueue(mfc);
                    }
                }
            }
            /*MethodOrdering.ComputeCallOrder(_knownMethods.Values);
            var order = _knownMethods.Values.OrderBy(mf => mf.CallOrder);
            foreach (MethodFacts mf in order)
            {
                if (mf.IsDecompilable)
                    mf.AnalyzeVariabilities();
            }*/
        }

        public void CreateHTMLReport(string path)
        {
            HTMLReport rep = new HTMLReport(path);
            rep.BeginDocument("Fact universe");

            rep.AddSection(1, "Types");            
            rep.BeginTable(1, "Name", "Realizations", "Mutable");
            KnownTypes
                .ToEnumerable()
                .OrderBy(t => t.TheType.FullName)
                .ToObservable()
                .AutoDo(t =>
                rep.AddRow(t.TheType.FullName,
                    string.Join(", ", RealizationsOf(t.TheType).ToEnumerable().Select(tr => tr.FullName)),
                    t.IsMutable ? "yes" : "no"));
            rep.EndTable();

            rep.AddSection(1, "Methods");
            KnownMethodBases
                .ToEnumerable()
                .OrderBy(m => m.Method.Name)
                .ToObservable()
                .AutoDo(mf =>
                    {
                        rep.AddSection(2, mf.Method.ToString());
                        rep.AddText("Declaring type: " + mf.Method.DeclaringType.FullName);
                        rep.AddSection(3, "Callees:");
                        rep.AddText(string.Join(", ", mf.CalledMethods.Select(cm => cm.ToString())));
                        rep.AddSection(3, "Callers:");
                        rep.AddText(string.Join(", ", mf.CallingMethods.Select(cm => cm.ToString())));
                        if (mf.Method.GetParameters().Length > 0)
                        {
                            rep.AddSection(3, "Argument candidates:");
                            rep.BeginBulletList();
                            foreach (ParameterInfo pi in mf.Method.GetParameters())
                            {
                                rep.BeginListItem();
                                rep.AddText(pi.Name + ": ");
                                var acs = mf.GetArgumentCandidates(pi);
                                if (acs.Any())
                                {
                                    rep.BeginBulletList();
                                    foreach (ElementSource src in acs)
                                    {
                                        rep.AddListItem(src.ToString());
                                    }
                                    rep.EndBulletList();
                                }
                                rep.EndListItem();
                            }
                            rep.EndBulletList();
                        }
                        if (mf.Mutations.Any())
                        {
                            rep.AddSection(3, "Mutations:");
                            rep.BeginBulletList();
                            foreach (ElementMutation mut in mf.Mutations)
                            {
                                rep.BeginListItem();
                                if (mut is StoreFieldMutation)
                                    rep.AddText("store field");
                                else if (mut is WriteArrayMutation)
                                    rep.AddText("write array");
                                else if (mut is IndirectMutation)
                                    rep.AddText("indirect store");
                                else
                                    rep.AddText("???");

                                rep.BeginBulletList();
                                rep.AddListItem("Mutatee: " + mut.Mutatee);

                                var mt = mut.GetMutatedTerminals();
                                if (mt.Any())
                                {
                                    rep.AddListItem("Mutated terminals:");
                                    rep.BeginBulletList();
                                    foreach (ElementSource src in mt)
                                        rep.AddListItem(src.ToString());
                                    rep.EndBulletList();
                                }

                                var st = mut.GetSubMutatedTerminals();
                                if (st.Any())
                                {
                                    rep.AddListItem("Sub-mutated terminals:");
                                    rep.BeginBulletList();
                                    foreach (ElementSource src in st)
                                        rep.AddListItem(src.ToString());
                                    rep.EndBulletList();
                                }

                                rep.EndBulletList();
                                rep.EndListItem();
                            }
                            rep.EndBulletList();
                        }
                    });

            rep.EndDocument();
            rep.Close();
        }
    }
}

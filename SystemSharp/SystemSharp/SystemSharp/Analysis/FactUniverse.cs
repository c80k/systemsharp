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
    /// <summary>
    /// Marker interface which instructs design analysis stage to assume that a certain method is called, 
    /// taking all side effects into account which could arise from calling that method.
    /// </summary>
    public interface IAssumeCalled
    {
    }

    /// <summary>
    /// Any method marked with this attribute is assumed to be called during design runtime,
    /// taking all side effects into account which could arise from calling that method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited=true, AllowMultiple=true)]
    public class AssumeCalled : Attribute, IAssumeCalled
    {
    }

    /// <summary>
    /// Any field marked with this attribute is assumed to stay constant throughout design runtime
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class AssumeConst : Attribute
    {
    }

    /// <summary>
    /// Processing stage of a method during design analysis
    /// </summary>
    public enum EMethodDiscoveryStage
    {
        /// <summary>
        /// The method is discovered for the first time
        /// </summary>
        FirstDiscovery,

        /// <summary>
        /// The method is now ready for processing
        /// </summary>
        Processing
    }

    /// <summary>
    /// This marker interface instructs design analysis to call OnDiscovery method upon discovery of tagged method.
    /// </summary>
    public interface IOnMethodDiscovery
    {
        /// <summary>
        /// Will be called when a tagged method is discovered by design analysis.
        /// </summary>
        /// <param name="method">the discovered method</param>
        /// <param name="stage">processing stage of discovered method</param>
        void OnDiscovery(MethodBase method, EMethodDiscoveryStage stage);
    }

    /// <summary>
    /// This marker interface instructs design analysis to call OnMethodCall upon each occurence of a call to a tagged method.
    /// </summary>
    public interface IOnMethodCall
    {
        /// <summary>
        /// Will be called for each occurence of a call to a tagged method.
        /// </summary>
        /// <param name="callerFacts">information about the method caller</param>
        /// <param name="callee">the called method</param>
        /// <param name="ilIndex">instruction index of method call inside caller code</param>
        void OnMethodCall(MethodFacts callerFacts, MethodBase callee, int ilIndex);
    }

    /// <summary>
    /// This is a debugging aid: Each method tagged with this attribute causes design analysis to trigger a breakpoint when the tagged method is discovered.
    /// </summary>
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

    /// <summary>
    /// This marker interface instructs design analysis to call OnDiscovery whenever a tagged type is discovered.
    /// </summary>
    public interface IOnTypeDiscovery
    {
        /// <summary>
        /// Will be called for the discovered type
        /// </summary>
        /// <param name="type">discovered type</param>
        void OnDiscovery(Type type);
    }

    /// <summary>
    /// This is a debugging aid: Each type tagged with this attribute causes design analysis to trigger a breakpoint when the tagged type is discovered.
    /// </summary>
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

    /// <summary>
    /// This class captures information on all relevant objects discovered during analysis, i.e. types, instantiated classes, called methods,
    /// and memory allocations.
    /// </summary>
    public class FactUniverse
    {
        /// <summary>
        /// An instance of this class is tied to each DesignContext instance. Therefore returns the instance belonging to the current design context.
        /// </summary>
        public static FactUniverse Instance
        {
            get { return DesignContext.Instance.Universe; }
        }

        /// <summary>
        /// All types found by design analysis
        /// </summary>
        public IObservable<TypeFacts> KnownTypes { get; private set; }

        /// <summary>
        /// All interface types found by design analysis
        /// </summary>
        public IObservable<TypeFacts> KnownInterfaces { get; private set; }

        /// <summary>
        /// All abstract classes found by design analysis
        /// </summary>
        public IObservable<TypeFacts> KnownAbstractClasses { get; private set; }

        /// <summary>
        /// All value types found by design analysis
        /// </summary>
        public IObservable<TypeFacts> KnownValueTypes { get; private set; }

        /// <summary>
        /// All instantiable types found by design analysis, i.e. only non-abstract, non-interface types with concrete type parameters
        /// </summary>
        public IObservable<TypeFacts> KnownInstantiables { get; private set; }

        /// <summary>
        /// All called methods found by design analysis
        /// </summary>
        public IObservable<MethodFacts> KnownMethods { get; private set; }

        /// <summary>
        /// All called constructors found by design analysis
        /// </summary>
        public IObservable<MethodFacts> KnownConstructors { get; private set; }

        /// <summary>
        /// The union of all called methods and constructors found by design analysis
        /// </summary>
        public IObservable<MethodFacts> KnownMethodBases { get; private set; }

        /// <summary>
        /// All program locations where new instances are created found by design analysis
        /// </summary>
        public IObservable<AllocationSite> KnownAllocationSites { get; private set; }

        /// <summary>
        /// Whether analysis stage is complete
        /// </summary>
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

        /// <summary>
        /// Adds a new type to the universe
        /// </summary>
        /// <param name="type">type to add</param>
        public void AddType(Type type)
        {
            Contract.Requires<ArgumentNullException>(type != null);
            Contract.Requires<InvalidOperationException>(!IsCompleted, UniverseCompletedErrorMsg);
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

        /// <summary>
        /// Adds a new method to the universe
        /// </summary>
        /// <param name="method">method to add</param>
        public void AddMethod(MethodBase method)
        {
            Contract.Requires<ArgumentNullException>(method != null);
            Contract.Requires<InvalidOperationException>(!IsCompleted, UniverseCompletedErrorMsg);
            var entryPoint = method.UnwrapEntryPoint();
            _knownMethods.Cache(entryPoint);
        }

        /// <summary>
        /// Adds a new method to the universe, identified by CallSite
        /// </summary>
        /// <param name="callSite">a CallSite instance</param>
        private void AddMethod(CallSite callSite)
        {
            Contract.Requires<ArgumentNullException>(callSite != null);

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

        /// <summary>
        /// Adds a constructor to the universe
        /// </summary>
        /// <param name="ctor">the constructor to add</param>
        public void AddConstructor(ConstructorInfo ctor)
        {
            Contract.Requires<InvalidOperationException>(!IsCompleted, UniverseCompletedErrorMsg);
            _knownMethods.Cache(ctor);
        }

        /// <summary>
        /// Queries whether the universe contains information on a particular method or constructor
        /// </summary>
        /// <param name="method">method or constructor</param>
        /// <returns>whether the universe contains information on supplied method/constructor</returns>
        public bool HaveFacts(MethodBase method)
        {
            var entryPoint = method.UnwrapEntryPoint(); 
            return _knownMethods.IsCached(entryPoint);
        }

        /// <summary>
        /// Completes the analysis
        /// </summary>
        public void Complete()
        {
            Contract.Requires<InvalidOperationException>(!IsCompleted, UniverseCompletedErrorMsg);

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

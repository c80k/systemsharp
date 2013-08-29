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
 * 
 * CHANGE LOG
 * ==========
 * 2011-08-15 CK fixed unimplemented Register() method of ArgumentReturnSource
 * 2011-08-16 CK fixed Equals() method of LocalMutation and derived classes
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
using SystemSharp.Collections;
using SystemSharp.Common;
using System.Reactive.Concurrency;
using SystemSharp.Analysis.Msil;

namespace SystemSharp.Analysis
{
    public abstract class ElementSource
    {
        public static readonly NoRefSource NoRef = new NoRefSource();
        public static readonly AnyPointerSource AnyPtr = new AnyPointerSource();
        public static readonly NullSource Nil = new NullSource();
        public static readonly TypeTokenSource TypeToken = new TypeTokenSource();

        public static ElementSource ForArgument(ParameterInfo arg)
        {
            if (arg.ParameterType.IsByRef)
                return new RefArgumentSource(arg);
            else
                return new ArgumentSource(arg);
        }
    }

    public abstract class ObjectSource : ElementSource
    {
    }

    public abstract class PointerSource : ElementSource
    {
    }

    public abstract class TypedRefSource : ElementSource
    {
    }

    public interface IInternalSource
    {
    }

    public interface ITerminalSource
    {
    }

    public interface ITrackableSource
    {
    }

    public interface IDereferenceableSource: ITrackableSource
    {
        ObjectSource Dereference();
    }

    public interface IMethodRemappable
    {
        ElementSource RemapMethod(MethodBase method);
    }

    public interface IMethodRemappable<T> : IMethodRemappable
    {
        new T RemapMethod(MethodBase method);
    }

    public interface IMethodArgumentSource
    {
        ParameterInfo Argument { get; }
    }

    public interface IResolvableSource: ITrackableSource
    {
        IEnumerable<ElementSource> Resolve();
        void Register(IEnumerable<ElementSource> sources);
    }

    public interface IHostedSource
    {
        IEnumerable<ElementSource> Hosts { get; }
    }

    public interface IMutableSource
    {
        void IndicateMutated();
        void IndicateSubMutated();
    }

    public interface IMutationSource
    {
        IEnumerable<ElementMutation> CreateMutations(int ilIndex, bool strictly, IEnumerable<ElementSource> rvalues);
    }

    public interface IModifiesStackState
    {
        AbstractStackState<T> ModifyStackState<T>(AbstractStackState<T> pre, T newState, Func<T, T, T> mergeFun);
    }

    public interface IMethodLocation
    {
        MethodBase Method { get; }
        int ILIndex { get; }
    }

    public static class ElementSourceHelpers
    {
        public static MethodBase Method(this IMethodArgumentSource source)
        {
            return (MethodBase)source.Argument.Member;
        }

        public static IEnumerable<ElementSource> Track(this ITrackableSource source)
        {
            ElementSource src = (ElementSource)source;
            IDereferenceableSource ds = src as IDereferenceableSource;
            if (ds != null)
                src = ds.Dereference();
            IResolvableSource rs = src as IResolvableSource;
            if (rs != null)
                return rs.Resolve();
            else
                return Enumerable.Repeat<ElementSource>(src, 1);
        }

        public static IEnumerable<ElementSource> TrackToTerminals(this ElementSource source)
        {
            HashSet<ElementSource> visited = new HashSet<ElementSource>();
            List<ElementSource> result = new List<ElementSource>();
            Queue<ElementSource> q = new Queue<ElementSource>();
            q.Enqueue(source);
            while (q.Count > 0)
            {
                ElementSource cur = q.Dequeue();
                if (visited.Contains(cur))
                    continue;
                visited.Add(cur);
                if (cur is ITerminalSource)
                {
                    result.Add(cur);
                }
                else if (cur is ITrackableSource)
                {
                    ITrackableSource ts = (ITrackableSource)cur;
                    foreach (ElementSource next in ts.Track())
                        q.Enqueue(next);
                }
                else
                {
                    throw new InvalidOperationException("Encountered untrackable source");
                }
            }
            return result;
        }

        public static IEnumerable<ElementSource> GetHosts(this ElementSource source)
        {
            if (source is IHostedSource)
                return ((IHostedSource)source).Hosts;
            else
                return Enumerable.Empty<ElementSource>();
        }

        public static IEnumerable<ElementSource> GetHosts(this IEnumerable<ElementSource> sources)
        {
            Contract.Requires(sources != null);

            return sources.SelectMany(s => GetHosts(s));
        }
    }

    public class NoRefSource : 
        ElementSource, 
        ITerminalSource
    {
        public override bool Equals(object obj)
        {
            return obj is NoRefSource;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString()
        {
            return "not a reference";
        }
    }

    public class AnyPointerSource : 
        PointerSource, 
        ITerminalSource
    {
        public override bool Equals(object obj)
        {
            return obj is AnyPointerSource;
        }

        public override int GetHashCode()
        {
            return 1;
        }

        public override string ToString()
        {
            return "some pointer";
        }
    }

    public class NullSource : 
        ObjectSource, 
        ITerminalSource
    {
        public override bool Equals(object obj)
        {
            return obj is NullSource;
        }

        public override int GetHashCode()
        {
            return 2;
        }

        public override string ToString()
        {
            return "null";
        }
    }

    public class TypeTokenSource : 
        ObjectSource, 
        ITerminalSource
    {
        public override bool Equals(object obj)
        {
            return obj is TypeTokenSource;
        }

        public override int GetHashCode()
        {
            return 3;
        }

        public override string ToString()
        {
            return "some type";
        }
    }

    public class StringSource : 
        ObjectSource, 
        ITerminalSource
    {
        public string Value { get; private set; }

        public StringSource(string value)
        {
            Value = value;
        }

        public override bool Equals(object obj)
        {
            StringSource ssrc = obj as StringSource;
            if (ssrc == null)
                return false;
            return object.Equals(Value, ssrc.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return "\"" + Value.ToString() + "\"";
        }
    }

    public class ConstObjectSource :
        ObjectSource,
        ITerminalSource
    {
        public object ObjectValue { get; private set; }

        public ConstObjectSource(object value)
        {
            ObjectValue = value;
        }

        public override bool Equals(object obj)
        {
            ConstObjectSource cos = obj as ConstObjectSource;
            if (cos == null)
                return false;
            return object.Equals(ObjectValue, cos.ObjectValue);
        }

        public override int GetHashCode()
        {
            return ObjectValue.GetHashCode();
        }
    }

    public class FieldSource : 
        ObjectSource, 
        ITerminalSource, 
        IHostedSource,
        IMutableSource
    {
        public IEnumerable<ElementSource> Instances { get; private set; }
        public FieldInfo Field { get; private set; }

        public FieldSource(IEnumerable<ElementSource> instances, FieldInfo field)
        {
            Instances = instances;
            Field = field;
            FactUniverse.Instance.GetFacts(field);
        }

        public override bool Equals(object obj)
        {
            FieldSource fsrc = obj as FieldSource;
            if (fsrc == null)
                return false;
            return Instances.SequenceEqual(fsrc.Instances) &&
                object.Equals(Field, fsrc.Field);
        }

        public override int GetHashCode()
        {
            return Instances.GetSequenceHashCode() ^ Field.GetHashCode();
        }

        public override string ToString()
        {
            return "field " + Field;
        }

        #region IHostedSource Member

        public IEnumerable<ElementSource> Hosts
        {
            get { return Instances; }
        }

        #endregion

        #region IMutableSource Member

        public void IndicateMutated()
        {
            FactUniverse.Instance.GetFacts(Field).IsWritten = true;
        }

        public void IndicateSubMutated()
        {
            FactUniverse.Instance.GetFacts(Field).IsSubMutated = true;
        }

        #endregion
    }

    public class AddressOfFieldSource :
        PointerSource,
        IDereferenceableSource, 
        IMutationSource
    {
        public IEnumerable<ElementSource> Instances { get; private set; }
        public FieldInfo Field { get; private set; }

        public AddressOfFieldSource(IEnumerable<ElementSource> instances, FieldInfo field)
        {
            Instances = instances;
            Field = field;
        }

        public override bool Equals(object obj)
        {
            AddressOfFieldSource fsrc = obj as AddressOfFieldSource;
            if (fsrc == null)
                return false;
            return Instances.SequenceEqual(Instances) &&
                object.Equals(Field, fsrc.Field);
        }

        public override int GetHashCode()
        {
            return ~Instances.GetSequenceHashCode() ^ Field.GetHashCode();
        }

        #region IDereferenceableSource Member

        public ObjectSource Dereference()
        {
            return new FieldSource(Instances, Field);
        }

        #endregion

        #region IMutationSource Member

        public IEnumerable<ElementMutation> CreateMutations(int ilIndex, bool strictly, IEnumerable<ElementSource> rvalues)
        {
            foreach (ElementSource inst in Instances)
                yield return new StoreFieldMutation(inst, Field, rvalues);
        }

        #endregion
    }

    public abstract class AllocationSite :
        ObjectSource,
        ITerminalSource,
        IMethodLocation
    {
        public MethodBase Method { get; private set; }
        public int ILIndex { get; private set; }

        public AllocationSite(MethodBase method, int ilIndex)
        {
            Method = method;
            ILIndex = ilIndex;
        }

        public override bool Equals(object obj)
        {
            AllocationSite asite = obj as AllocationSite;
            if (asite == null)
                return false;
            return object.Equals(Method, asite.Method) &&
                ILIndex == asite.ILIndex;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode() ^ ILIndex;
        }
    }

    public class NewSite : AllocationSite
    {
        public ConstructorInfo Allocated { get; private set; }

        public Type AllocatedType
        {
            get { return Allocated.DeclaringType; }
        }

        public NewSite(MethodBase method, int ilIndex, ConstructorInfo allocated) :
            base(method, ilIndex)
        {
            Allocated = allocated;
        }

        public override string ToString()
        {
            return Method.ToString() + "@" + ILIndex + ": " +
                "new " + Allocated.ToString();
        }
    }

    public class NewArraySite : AllocationSite
    {
        public Type ElementType { get; private set; }

        public NewArraySite(MethodBase method, int ilIndex, Type elementType) :
            base(method, ilIndex)
        {
            ElementType = elementType;
        }

        public override string ToString()
        {
            return Method.ToString() + "@" + ILIndex + ": " +
                "newarray <" + ElementType.ToString() + ">";
        }
    }

    public class BoxSite : AllocationSite
    {
        public Type Boxed { get; private set; }

        public BoxSite(MethodBase method, int ilIndex, Type boxed) :
            base(method, ilIndex)
        {
            Boxed = boxed;
        }

        public override string ToString()
        {
            return Method.ToString() + "@" + ILIndex + ": " +
                "box (" + Boxed.ToString() + ")";
        }
    }

    public class ArgumentSource :
        ObjectSource,
        IMethodRemappable<ArgumentSource>,
        IMethodArgumentSource,
        IResolvableSource
    {
        public ParameterInfo Argument { get; private set; }

        public ArgumentSource(ParameterInfo argument)
        {
            Contract.Requires(!argument.ParameterType.IsByRef);
            Argument = argument;
        }

        public override bool Equals(object obj)
        {
            ArgumentSource asrc = obj as ArgumentSource;
            if (asrc == null)
                return false;
            return object.Equals(Argument, asrc.Argument);
        }

        public override int GetHashCode()
        {
            return Argument.GetHashCode();
        }

        public override string ToString()
        {
            return Argument.ToString();
        }

        public ArgumentSource RemapMethod(MethodBase method)
        {
            ParameterInfo remapArg = method.GetParameters()[Argument.Position];
            return new ArgumentSource(remapArg);
        }

        #region IMethodRemappable Member

        ElementSource IMethodRemappable.RemapMethod(MethodBase method)
        {
            return RemapMethod(method);
        }

        #endregion

        #region IResolvableSource Member

        public IEnumerable<ElementSource> Resolve()
        {
            return FactUniverse.Instance.GetFacts(this.Method()).GetArgumentCandidates(Argument);
        }

        public void Register(IEnumerable<ElementSource> sources)
        {
            FactUniverse univ = FactUniverse.Instance;
            univ
                .RealizationsOf(this.Method())
                .AutoDo(rm =>
                {
                    ArgumentSource re = RemapMethod(rm);
                    ParameterInfo rpi = re.Argument;
                    MethodFacts rf = univ.GetFacts(rm);
                    rf.RegisterArgumentCandidates(rpi, sources);
                });
        }

        #endregion
    }

    public class RefArgumentSource :
        PointerSource,
        IMethodRemappable<RefArgumentSource>,
        IMethodArgumentSource,
        IResolvableSource
    {
        public ParameterInfo Argument { get; private set; }

        public RefArgumentSource(ParameterInfo argument)
        {
            Contract.Requires(argument.ParameterType.IsByRef);
            Argument = argument;
        }

        public override bool Equals(object obj)
        {
            RefArgumentSource asrc = obj as RefArgumentSource;
            if (asrc == null)
                return false;
            return object.Equals(Argument, asrc.Argument);
        }

        public override int GetHashCode()
        {
            return Argument.GetHashCode();
        }

        public override string ToString()
        {
            return Argument.ToString();
        }

        public RefArgumentSource RemapMethod(MethodBase method)
        {
            ParameterInfo remapArg = method.GetParameters()[Argument.Position];
            return new RefArgumentSource(remapArg);
        }

        #region IMethodRemappable Member

        ElementSource IMethodRemappable.RemapMethod(MethodBase method)
        {
            return RemapMethod(method);
        }

        #endregion

        #region IResolvableSource Member

        public IEnumerable<ElementSource> Resolve()
        {
            return FactUniverse.Instance.GetFacts((MethodBase)Argument.Member).GetArgumentCandidates(Argument);
        }

        public void Register(IEnumerable<ElementSource> sources)
        {
            FactUniverse univ = FactUniverse.Instance;
            univ
                .RealizationsOf(this.Method())
                .AutoDo(rm =>
                {
                    RefArgumentSource re = RemapMethod(rm);
                    ParameterInfo rpi = re.Argument;
                    MethodFacts rf = univ.GetFacts(rm);
                    rf.RegisterArgumentCandidates(rpi, sources);
                });
        }

        #endregion
    }

    public class ArgumentReturnSource :
        ObjectSource,
        IMethodRemappable<ArgumentReturnSource>,
        IMethodArgumentSource,
        IResolvableSource
    {
        public ParameterInfo Argument { get; private set; }

        public ArgumentReturnSource(ParameterInfo argument)
        {
            Contract.Requires(argument.ParameterType.IsByRef);
            Argument = argument;
        }

        public override bool Equals(object obj)
        {
            ArgumentReturnSource asrc = obj as ArgumentReturnSource;
            if (asrc == null)
                return false;
            return object.Equals(Argument, asrc.Argument);
        }

        public override int GetHashCode()
        {
            return Argument.GetHashCode();
        }

        public override string ToString()
        {
            return Argument.ToString();
        }

        public ArgumentReturnSource RemapMethod(MethodBase method)
        {
            ParameterInfo remapArg = method.GetParameters()[Argument.Position];
            return new ArgumentReturnSource(remapArg);
        }

        #region IMethodRemappable Member

        ElementSource IMethodRemappable.RemapMethod(MethodBase method)
        {
            return RemapMethod(method);
        }

        #endregion

        #region IResolvableSource Member

        public IEnumerable<ElementSource> Resolve()
        {
            return FactUniverse.Instance.GetFacts((MethodBase)Argument.Member).GetArgumentReturnCandidates(Argument);
        }

        public void Register(IEnumerable<ElementSource> sources)
        {
            FactUniverse.Instance.GetFacts((MethodBase)Argument.Member).RegisterArgumentReturnCandidates(Argument, sources);
        }

        #endregion
    }

    public class AddressOfArgumentSource :
        PointerSource,
        IMethodRemappable<AddressOfArgumentSource>,
        IMethodArgumentSource,
        IDereferenceableSource,
        IMutationSource
    {
        public ParameterInfo Argument { get; private set; }

        public AddressOfArgumentSource(ParameterInfo argument)
        {
            Contract.Requires(!argument.ParameterType.IsByRef);
            Argument = argument;
        }

        public override bool Equals(object obj)
        {
            AddressOfArgumentSource asrc = obj as AddressOfArgumentSource;
            if (asrc == null)
                return false;
            return object.Equals(Argument, asrc.Argument);
        }

        public override int GetHashCode()
        {
            return Argument.GetHashCode();
        }

        public override string ToString()
        {
            return "&" + Argument.ToString();
        }

        public AddressOfArgumentSource RemapMethod(MethodBase method)
        {
            ParameterInfo remapArg = method.GetParameters()[Argument.Position];
            return new AddressOfArgumentSource(remapArg);
        }

        #region IMethodRemappable Member

        ElementSource IMethodRemappable.RemapMethod(MethodBase method)
        {
            return RemapMethod(method);
        }

        #endregion

        #region IDereferenceableSource Member

        public ObjectSource Dereference()
        {
            return new ArgumentSource(Argument);
        }

        #endregion

        #region IMutationSource Member

        public IEnumerable<ElementMutation> CreateMutations(int ilIndex, bool strictly, IEnumerable<ElementSource> rvalues)
        {
            yield return new ArgumentMutation((MethodBase)Argument.Member, ilIndex, strictly, Argument.Position, rvalues);
        }

        #endregion
    }

    public class ThisSource :
        ObjectSource,
        IMethodRemappable<ThisSource>,
        IResolvableSource
    {
        public MethodBase Method { get; private set; }

        public ThisSource(MethodBase method)
        {
            Method = method;
        }

        public override bool Equals(object obj)
        {
            ThisSource tsrc = obj as ThisSource;
            if (tsrc == null)
                return false;
            return object.Equals(Method, tsrc.Method);
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode();
        }

        public override string ToString()
        {
            return Method.ToString() + ".this";
        }

        public ThisSource RemapMethod(MethodBase method)
        {
            return new ThisSource(Method);
        }

        #region IMethodRemappable Member

        ElementSource IMethodRemappable.RemapMethod(MethodBase method)
        {
            return RemapMethod(method);
        }

        #endregion

        #region IResolvableSource Member

        public IEnumerable<ElementSource> Resolve()
        {
            return FactUniverse.Instance.GetFacts(Method).GetThisCandidates();
        }

        public void Register(IEnumerable<ElementSource> sources)
        {
            FactUniverse univ = FactUniverse.Instance;
            univ
                .RealizationsOf(Method)
                .Select(m => univ.GetFacts(m))
                .AutoDo(mf => mf.RegisterThisCandidates(sources));
            univ
                .KnownAllocationSites
                .Where(asite => asite is NewSite && 
                    Method.DeclaringType.IsAssignableFrom(asite.Method.DeclaringType))
                .ObserveOn(Scheduler.CurrentThread)
                .AutoDo(asite => univ.GetFacts(asite.Method).RegisterThisCandidate(asite));
        }

        #endregion
    }

    public class ThisPointerSource :
        PointerSource,
        IMethodRemappable<ThisPointerSource>,
        IResolvableSource
    {
        public MethodBase Method { get; private set; }

        public ThisPointerSource(MethodBase method)
        {
            Method = method;
        }

        public override bool Equals(object obj)
        {
            ThisPointerSource tsrc = obj as ThisPointerSource;
            if (tsrc == null)
                return false;
            return object.Equals(Method, tsrc.Method);
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode();
        }

        public override string ToString()
        {
            return Method.ToString() + ".this";
        }

        public ThisPointerSource RemapMethod(MethodBase method)
        {
            return new ThisPointerSource(Method);
        }

        #region IMethodRemappable Member

        ElementSource IMethodRemappable.RemapMethod(MethodBase method)
        {
            return RemapMethod(method);
        }

        #endregion

        #region IResolvableSource Member

        public IEnumerable<ElementSource> Resolve()
        {
            return FactUniverse.Instance.GetFacts(Method).GetThisCandidates();
        }

        public void Register(IEnumerable<ElementSource> sources)
        {
            FactUniverse univ = FactUniverse.Instance;
            univ
                .RealizationsOf(Method)
                .Select(m => univ.GetFacts(m))
                .AutoDo(mf => mf.RegisterThisCandidates(sources));
            univ
                .KnownAllocationSites
                .Where(asite => asite is NewSite &&
                    Method.DeclaringType.IsAssignableFrom(asite.Method.DeclaringType))
                .ObserveOn(Scheduler.CurrentThread)
                .AutoDo(asite => univ.GetFacts(asite.Method).RegisterThisCandidate(asite));
        }

        #endregion
    }

    public class ArrayElementSource :
        ObjectSource,
        ITerminalSource,
        IHostedSource
    {
        public IEnumerable<ObjectSource> ArraySources { get; private set; }

        public ArrayElementSource(IEnumerable<ObjectSource> arraySources)
        {
            ArraySources = arraySources;
        }

        public override bool Equals(object obj)
        {
            ArrayElementSource aesrc = obj as ArrayElementSource;
            if (aesrc == null)
                return false;
            return Enumerable.SequenceEqual(ArraySources, aesrc.ArraySources);
        }

        public override int GetHashCode()
        {
            return ArraySources.GetSequenceHashCode();
        }

        public override string ToString()
        {
            return "{" + string.Join(",", ArraySources) + "}.[x]";
        }

        #region IHostedSource Member

        public IEnumerable<ElementSource> Hosts
        {
            get { return ArraySources; }
        }

        #endregion
    }

    public class AddressOfArrayElementSource :
        PointerSource,
        IDereferenceableSource,
        IMutationSource
    {
        public IEnumerable<ObjectSource> ArraySources { get; private set; }

        public AddressOfArrayElementSource(IEnumerable<ObjectSource> arraySources)
        {
            ArraySources = arraySources;
        }

        public override bool Equals(object obj)
        {
            AddressOfArrayElementSource aesrc = obj as AddressOfArrayElementSource;
            if (aesrc == null)
                return false;
            return Enumerable.SequenceEqual(ArraySources, aesrc.ArraySources);
        }

        public override int GetHashCode()
        {
            return ~ArraySources.GetSequenceHashCode();
        }

        public override string ToString()
        {
            return "&{" + string.Join(",", ArraySources) + "}.[x]";
        }

        #region IDereferenceableSource Member

        public ObjectSource Dereference()
        {
            return new ArrayElementSource(ArraySources);
        }

        #endregion

        #region IMutationSource Member

        public IEnumerable<ElementMutation> CreateMutations(int ilIndex, bool strictly, IEnumerable<ElementSource> rvalues)
        {
            foreach (ObjectSource array in ArraySources)
                yield return new WriteArrayMutation(array, rvalues);
        }

        #endregion
    }

    public class MethodReturnSource :
        ObjectSource,
        IMethodRemappable<MethodReturnSource>,
        IResolvableSource
    {
        public MethodInfo Method { get; private set; }

        public MethodReturnSource(MethodInfo method)
        {
            Method = method;
        }

        public override bool Equals(object obj)
        {
            MethodReturnSource mrsrc = obj as MethodReturnSource;
            if (mrsrc == null)
                return false;
            return object.Equals(Method, mrsrc.Method);
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode();
        }

        public override string ToString()
        {
            return "return of " + Method.ToString();
        }

        public MethodReturnSource RemapMethod(MethodBase method)
        {
            return new MethodReturnSource((MethodInfo)method);
        }

        #region IMethodRemappable Member

        ElementSource IMethodRemappable.RemapMethod(MethodBase method)
        {
            return RemapMethod(method);
        }

        #endregion

        #region IResolvableSource Member

        public IEnumerable<ElementSource> Resolve()
        {
            return FactUniverse.Instance.GetFacts(Method).GetReturnCandidates();
        }

        public void Register(IEnumerable<ElementSource> sources)
        {
            FactUniverse univ = FactUniverse.Instance;
            univ
                .RealizationsOf(Method)
                .Select(m => univ.GetFacts(m))
                .AutoDo(mf => mf.RegisterReturnCandidates(sources));
        }

        #endregion
    }

    public class AddressOfLocalVariableSource :
        PointerSource,
        IInternalSource,
        IMutationSource
    {
        public MethodBase Method { get; private set; }
        public int LocalIndex { get; private set; }

        public AddressOfLocalVariableSource(MethodBase method, int localIndex)
        {
            Method = method;
            LocalIndex = localIndex;
        }

        public override bool Equals(object obj)
        {
            AddressOfLocalVariableSource alsrc = obj as AddressOfLocalVariableSource;
            if (alsrc == null)
                return false;
            return object.Equals(Method, alsrc.Method) &&
                LocalIndex == alsrc.LocalIndex;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode() ^ LocalIndex;
        }

        public override string ToString()
        {
            return "&" + Method.ToString() + ".local" + LocalIndex;
        }

        #region IMutationSource Member

        public IEnumerable<ElementMutation> CreateMutations(int ilIndex, bool strictly, IEnumerable<ElementSource> rvalues)
        {
            yield return new LocalVariableMutation(Method, ilIndex, strictly, LocalIndex, rvalues);
        }

        #endregion
    }

    public class DerefSource :
        ObjectSource,
        IResolvableSource
    {
        public IEnumerable<PointerSource> Sources { get; private set; }

        public DerefSource(IEnumerable<PointerSource> sources)
        {
            Debug.Assert(sources.All(s => s is PointerSource));
            Sources = sources;
        }

        public override bool Equals(object obj)
        {
            DerefSource isrc = obj as DerefSource;
            if (isrc == null)
                return false;
            return Enumerable.SequenceEqual(Sources, isrc.Sources);
        }

        public override int GetHashCode()
        {
            return ~Sources.GetSequenceHashCode();
        }

        public override string ToString()
        {
            return "*{" + string.Join(",", Sources) + "}";
        }

        #region IResolvableSource Member

        public IEnumerable<ElementSource> Resolve()
        {
            return Sources
                .Where(s => s is ITrackableSource)
                .SelectMany(s => ((ITrackableSource)s).Track());
        }

        public void Register(IEnumerable<ElementSource> sources)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public abstract class ElementMutation
    {
        public IEnumerable<ElementSource> RValues { get; private set; }

        public ElementMutation(IEnumerable<ElementSource> rvalues)
        {
            RValues = rvalues;
        }

        public abstract ElementSource Mutatee { get; }

        public IEnumerable<ElementSource> GetMutatedTerminals()
        {
            return Mutatee.TrackToTerminals();
        }

        public IEnumerable<ElementSource> GetSubMutatedTerminals()
        {
            IEnumerable<ElementSource> curset = GetMutatedTerminals().GetHosts().SelectMany(h => h.TrackToTerminals());
            HashSet<ElementSource> visited = new HashSet<ElementSource>();
            Queue<ElementSource> q = new Queue<ElementSource>(curset);
            while (q.Count > 0)
            {
                ElementSource src = q.Dequeue();
                if (visited.Contains(src))
                    continue;
                visited.Add(src);
                yield return src;
                foreach (ElementSource host in src.GetHosts())
                {
                    foreach (ElementSource thost in host.TrackToTerminals())
                    {
                        q.Enqueue(thost);
                    }
                }
            }
        }
    }

    public class StoreFieldMutation : ElementMutation
    {
        public ElementSource Instance { get; private set; }
        public FieldInfo Field { get; private set; }

        public StoreFieldMutation(ElementSource instance, FieldInfo field,
            IEnumerable<ElementSource> rvalues) :
            base(rvalues)
        {
            Contract.Requires(field != null);
            Contract.Requires((instance == null) == (field.IsStatic));
            Instance = instance;
            Field = field;
        }

        public override bool Equals(object obj)
        {
            StoreFieldMutation sfm = obj as StoreFieldMutation;
            if (sfm == null)
                return false;
            return object.Equals(Instance, sfm.Instance) &&
                object.Equals(Field, sfm.Field);
        }

        public override int GetHashCode()
        {
            return (Instance == null ? 0 : Instance.GetHashCode()) ^
                Field.GetHashCode();
        }

        public override ElementSource Mutatee
        {
            get { return Instance == null ? 
                new FieldSource(Enumerable.Empty<ElementSource>(), Field) :
                new FieldSource(new ElementSource[1] { Instance }, Field);
            }
        }
    }

    public class WriteArrayMutation : ElementMutation
    {
        public ObjectSource Array { get; private set; }

        public WriteArrayMutation(ObjectSource array, IEnumerable<ElementSource> rvalues) :
            base(rvalues)
        {
            Contract.Requires(array != null);
            Array = array;
            _element = new ArrayElementSource(new ObjectSource[] { Array });
        }

        public override bool Equals(object obj)
        {
            WriteArrayMutation wam = obj as WriteArrayMutation;
            if (wam == null)
                return false;
            return object.Equals(Array, wam.Array);
        }

        public override int GetHashCode()
        {
            return Array.GetHashCode();
        }

        private ArrayElementSource _element;
        public override ElementSource Mutatee
        {
            get { return _element; }
        }
    }

    public class IndirectMutation : ElementMutation
    {
        public PointerSource Source { get; private set; }

        public IndirectMutation(PointerSource source, IEnumerable<ElementSource> rvalues) :
            base(rvalues)
        {
            Contract.Requires(source != null);
            Source = source;
        }

        public override bool Equals(object obj)
        {
            IndirectMutation im = obj as IndirectMutation;
            return object.Equals(Source, im.Source);
        }

        public override int GetHashCode()
        {
            return Source.GetHashCode();
        }

        public override ElementSource Mutatee
        {
            get { return new DerefSource(new PointerSource[] { Source }); }
        }
    }

    public abstract class LocalMutation : 
        ElementMutation,
        IMethodLocation
    {
        public MethodBase Method { get; private set; }
        public int ILIndex { get; private set; }
        public bool OverwritesStrictly { get; private set; }

        public LocalMutation(MethodBase method, int ilIndex, bool strictly, IEnumerable<ElementSource> rvalues):
            base(rvalues)
        {
            Method = method;
            ILIndex = ilIndex;
            OverwritesStrictly = strictly;
        }

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode() ^ ILIndex;
        }

        public override ElementSource Mutatee
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class LocalVariableMutation : 
        LocalMutation,
        IModifiesStackState
    {
        public int LocalIndex { get; private set; }

        public LocalVariableMutation(MethodBase method, int ilIndex, bool strictly, int localIndex, IEnumerable<ElementSource> rvalues) :
            base(method, ilIndex, strictly, rvalues)
        {
            LocalIndex = localIndex;
        }

        public override string ToString()
        {
            return Method.ToString() + "@" + ILIndex + ": local" + LocalIndex;
        }

        public override bool Equals(object obj)
        {
            LocalVariableMutation other = obj as LocalVariableMutation;
            if (other == null)
                return false;
            return Method.Equals(other.Method) &&
                ILIndex == other.ILIndex &&
                LocalIndex == other.LocalIndex;
        }

        #region IModifiesStackState Member

        public AbstractStackState<T> ModifyStackState<T>(AbstractStackState<T> pre, T newState, Func<T, T, T> mergeFun)
        {
            if (OverwritesStrictly)
                return pre.Assign(LocalIndex, newState);
            else
                return pre.Assign(LocalIndex, mergeFun(pre.GetLocal(LocalIndex), newState));
        }

        #endregion
    }

    public class ArgumentMutation : 
        LocalMutation,
        IModifiesStackState
    {
        public int ArgumentPosition { get; private set; }

        public ArgumentMutation(MethodBase method, int ilIndex, bool strictly, int argPos, IEnumerable<ElementSource> rvalues) :
            base(method, ilIndex, strictly, rvalues)
        {
            ArgumentPosition = argPos;
        }

        public override string ToString()
        {
            return Method.ToString() + "@" + ILIndex + ": arg" + ArgumentPosition;
        }

        public override bool Equals(object obj)
        {
            ArgumentMutation other = obj as ArgumentMutation;
            if (other == null)
                return false;
            return Method.Equals(other.Method) &&
                ILIndex == other.ILIndex &&
                ArgumentPosition == other.ArgumentPosition;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode() ^
                ILIndex ^ ArgumentPosition;
        }

        #region IModifiesStackState Member

        public AbstractStackState<T> ModifyStackState<T>(AbstractStackState<T> pre, T newState, Func<T, T, T> mergeFun)
        {
            if (OverwritesStrictly)
                return pre.AssignArg(ILIndex, newState);
            else
                return pre.AssignArg(ILIndex, mergeFun(pre.GetArgument(ArgumentPosition), newState));
        }

        #endregion
    }

    public class IndirectLoad :
        IMethodLocation
    {
        public MethodBase Method { get; private set; }
        public int ILIndex { get; private set; }
        public DerefSource Source { get; private set; }

        public IndirectLoad(MethodBase method, int ilIndex, DerefSource source)
        {
            Method = method;
            ILIndex = ilIndex;
            Source = source;
        }

        public override bool Equals(object obj)
        {
            IndirectLoad other = obj as IndirectLoad;
            return Method.Equals(other.Method) &&
                ILIndex == other.ILIndex;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode() ^ ILIndex;
        }

        public override string ToString()
        {
            return Method.ToString() + "@" + ILIndex + ": load " + Source.ToString();
        }
    }
}

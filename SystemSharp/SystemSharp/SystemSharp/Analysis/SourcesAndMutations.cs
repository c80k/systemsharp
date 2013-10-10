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
    /// <summary>
    /// This base class is intended as a symbolic abstraction of any data item. As opposed to SysDOM expressions,
    /// the intent is to track references and pointers on their way through program code.
    /// </summary>
    public abstract class ElementSource
    {
        /// <summary>
        /// Anything which is not a reference and not a pointer (and therefore not interesting for tracking)
        /// </summary>
        public static readonly NoRefSource NoRef = new NoRefSource();

        /// <summary>
        /// Anything which is a pointer
        /// </summary>
        public static readonly AnyPointerSource AnyPtr = new AnyPointerSource();

        /// <summary>
        /// Any null reference
        /// </summary>
        public static readonly NullSource Nil = new NullSource();

        /// <summary>
        /// A CLI type token
        /// </summary>
        public static readonly TypeTokenSource TypeToken = new TypeTokenSource();

        /// <summary>
        /// Constructs an instance for a given method argument description
        /// </summary>
        /// <param name="arg">method argument description</param>
        /// <returns>an instance of ElementSource which best describes the supplied argument description</returns>
        public static ElementSource ForArgument(ParameterInfo arg)
        {
            if (arg.ParameterType.IsByRef)
                return new RefArgumentSource(arg);
            else
                return new ArgumentSource(arg);
        }
    }

    /// <summary>
    /// Abstract base class for any item which is a CLI System.Object
    /// </summary>
    public abstract class ObjectSource : ElementSource
    {
    }

    /// <summary>
    /// Abstract base class for any item which is a pointer
    /// </summary>
    public abstract class PointerSource : ElementSource
    {
    }

    /// <summary>
    /// Marks data items which are only valid inside the executing method
    /// </summary>
    public interface IInternalSource
    {
    }

    /// <summary>
    /// Marks items which directly describe the origin of data
    /// </summary>
    public interface ITerminalSource
    {
    }

    /// <summary>
    /// Marks data items which can be further tracked until an ITerminalSource is reached
    /// </summary>
    public interface ITrackableSource
    {
    }

    /// <summary>
    /// Marks a trackable data item whose tracking operation if dereferencing
    /// </summary>
    public interface IDereferenceableSource: ITrackableSource
    {
        ObjectSource Dereference();
    }

    /// <summary>
    /// Marks a data item which must be remapped to more concrete method
    /// </summary>
    public interface IMethodRemappable
    {
        ElementSource RemapMethod(MethodBase method);
    }

    /// <summary>
    /// Marks a data item which must be remapped to more concrete method
    /// </summary>
    public interface IMethodRemappable<T> : IMethodRemappable
    {
        new T RemapMethod(MethodBase method);
    }

    /// <summary>
    /// Marks a data item which stems from a method argument
    /// </summary>
    public interface IMethodArgumentSource
    {
        ParameterInfo Argument { get; }
    }

    /// <summary>
    /// Marks a trackable data item which can be resolved in order to retrieve its possible contents
    /// </summary>
    public interface IResolvableSource: ITrackableSource
    {
        /// <summary>
        /// Returns possible contents of this data item
        /// </summary>
        IEnumerable<ElementSource> Resolve();

        /// <summary>
        /// Registers possible contents of this data item
        /// </summary>
        void Register(IEnumerable<ElementSource> sources);
    }

    /// <summary>
    /// Marks a data item which lives inside an instance. E.g. a field content cannot exist for itself.
    /// </summary>
    public interface IHostedSource
    {
        /// <summary>
        /// Returns possible instances which host the data item
        /// </summary>
        IEnumerable<ElementSource> Hosts { get; }
    }

    /// <summary>
    /// Marks a data item which can be modified during runtime
    /// </summary>
    public interface IMutableSource
    {
        /// <summary>
        /// Tells the data item that it is possibly modified.
        /// </summary>
        void IndicateMutated();

        /// <summary>
        /// Tells the data item that one of its sub items (e.g. fields of a class/struct) is modified.
        /// </summary>
        void IndicateSubMutated();
    }

    /// <summary>
    /// Marks a data item which can be used to modify other data items
    /// </summary>
    public interface IMutationSource
    {
        /// <summary>
        /// Returns a sequence of possible modifications which might be caused by this data item
        /// </summary>
        /// <param name="ilIndex">index of instruction performing the modification</param>
        /// <param name="strictly">whether this instruction will definitely cause a modification (true) or just possibly (false)</param>
        /// <param name="rvalues">possible right-hand side values of modification</param>
        /// <returns>resulting modifications</returns>
        IEnumerable<ElementMutation> CreateMutations(int ilIndex, bool strictly, IEnumerable<ElementSource> rvalues);
    }

    /// <summary>
    /// Marks a modification which alters the stack state in some way
    /// </summary>
    public interface IModifiesStackState
    {
        /// <summary>
        /// Creates a new stack state which reflects the modification, based on a given stack state
        /// </summary>
        /// <typeparam name="T">type of stack element described by stack state</typeparam>
        /// <param name="pre">previous stack state</param>
        /// <param name="newState">possible right-hand side value(s) of modification</param>
        /// <param name="mergeFun">functor which merges two sets of right-hand side values</param>
        /// <returns>the resulting stack state</returns>
        AbstractStackState<T> ModifyStackState<T>(AbstractStackState<T> pre, T newState, Func<T, T, T> mergeFun);
    }

    /// <summary>
    /// Describes a program location inside a method
    /// </summary>
    public interface IMethodLocation
    {
        /// <summary>
        /// The method or constructor
        /// </summary>
        MethodBase Method { get; }

        /// <summary>
        /// The instruction index
        /// </summary>
        int ILIndex { get; }
    }

    public static class ElementSourceHelpers
    {
        /// <summary>
        /// Returns the method or constructor of a method argument source
        /// </summary>
        /// <param name="source">method argument source</param>
        /// <returns>associated method or constructor</returns>
        public static MethodBase Method(this IMethodArgumentSource source)
        {
            return (MethodBase)source.Argument.Member;
        }

        /// <summary>
        /// Performs one tracking step on a trackable data item
        /// </summary>
        /// <param name="source">trackable data item</param>
        /// <returns>possible contents</returns>
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

        /// <summary>
        /// Tracks a data item to its origins
        /// </summary>
        /// <param name="source">any data item</param>
        /// <returns>possible contents</returns>
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

        /// <summary>
        /// Returns all possible hosts of a given data item
        /// </summary>
        /// <param name="source">data item</param>
        /// <returns>all possible hosts</returns>
        public static IEnumerable<ElementSource> GetHosts(this ElementSource source)
        {
            if (source is IHostedSource)
                return ((IHostedSource)source).Hosts;
            else
                return Enumerable.Empty<ElementSource>();
        }

        /// <summary>
        /// Returns all possible hosts of multiple data items
        /// </summary>
        /// <param name="source">multiple data items</param>
        /// <returns>all possible hosts</returns>
        public static IEnumerable<ElementSource> GetHosts(this IEnumerable<ElementSource> sources)
        {
            Contract.Requires(sources != null);

            return sources.SelectMany(s => GetHosts(s));
        }
    }

    /// <summary>
    /// Describes a data item which is not an object and not a pointer
    /// </summary>
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

    /// <summary>
    /// Describes a data item which is a not further specified pointer
    /// </summary>
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

    /// <summary>
    /// Describes the null reference
    /// </summary>
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

    /// <summary>
    /// Describes a CLI type token
    /// </summary>
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

    /// <summary>
    /// Describes a string literal
    /// </summary>
    public class StringSource : 
        ObjectSource, 
        ITerminalSource
    {
        /// <summary>
        /// String value
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Constructs a new instance based on a string value
        /// </summary>
        /// <param name="value">string value</param>
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

    /// <summary>
    /// Describes a somehow constant object
    /// </summary>
    public class ConstObjectSource :
        ObjectSource,
        ITerminalSource
    {
        /// <summary>
        /// The constant object
        /// </summary>
        public object ObjectValue { get; private set; }

        /// <summary>
        /// Constructs a new instance based on an object
        /// </summary>
        /// <param name="value">the object</param>
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

    /// <summary>
    /// Describes a field content
    /// </summary>
    public class FieldSource : 
        ObjectSource, 
        ITerminalSource, 
        IHostedSource,
        IMutableSource
    {
        /// <summary>
        /// Possible instances hosting the field
        /// </summary>
        public IEnumerable<ElementSource> Instances { get; private set; }

        /// <summary>
        /// Associated field
        /// </summary>
        public FieldInfo Field { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="instances">possible hosting instances</param>
        /// <param name="field">field under consideration</param>
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

    /// <summary>
    /// Describes the address of a field
    /// </summary>
    public class AddressOfFieldSource :
        PointerSource,
        IDereferenceableSource, 
        IMutationSource
    {
        /// <summary>
        /// Possible instances hosting the field
        /// </summary>
        public IEnumerable<ElementSource> Instances { get; private set; }

        /// <summary>
        /// Associated field
        /// </summary>
        public FieldInfo Field { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="instances">possible hosting instances</param>
        /// <param name="field">field under consideration</param>
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

    /// <summary>
    /// Describes a program location where a new object is allocated
    /// </summary>
    public abstract class AllocationSite :
        ObjectSource,
        ITerminalSource,
        IMethodLocation
    {
        public MethodBase Method { get; private set; }
        public int ILIndex { get; private set; }

        /// <summary>
        /// Constructs a new instance based on the program location
        /// </summary>
        /// <param name="method">the method where the allocation happens</param>
        /// <param name="ilIndex">the instruction index which performs the allocation</param>
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

    /// <summary>
    /// Describes a program location where a non-array object is allocated
    /// </summary>
    public class NewSite : AllocationSite
    {
        /// <summary>
        /// The constructor used for allocation
        /// </summary>
        public ConstructorInfo Allocated { get; private set; }

        /// <summary>
        /// Returns the type of object being allocated
        /// </summary>
        public Type AllocatedType
        {
            get { return Allocated.DeclaringType; }
        }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method where allocation happens</param>
        /// <param name="ilIndex">instruction index of allocation</param>
        /// <param name="allocated">constructor called for allocation</param>
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

    /// <summary>
    /// Describes a program location where a new array is allocated
    /// </summary>
    public class NewArraySite : AllocationSite
    {
        /// <summary>
        /// The type of an array element
        /// </summary>
        public Type ElementType { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method where allocation happens</param>
        /// <param name="ilIndex">instruction index of allocation</param>
        /// <param name="elementType">element type of allocated array</param>
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

    /// <summary>
    /// Describes a program location where a value type is boxed
    /// </summary>
    public class BoxSite : AllocationSite
    {
        /// <summary>
        /// Boxed type
        /// </summary>
        public Type Boxed { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method where boxing happens</param>
        /// <param name="ilIndex">instruction index of boxing</param>
        /// <param name="boxed">boxing result type</param>
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

    /// <summary>
    /// Describes a data item which stems from a method argument
    /// </summary>
    public class ArgumentSource :
        ObjectSource,
        IMethodRemappable<ArgumentSource>,
        IMethodArgumentSource,
        IResolvableSource
    {
        /// <summary>
        /// Associated method argument
        /// </summary>
        public ParameterInfo Argument { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="argument">method argument</param>
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

    /// <summary>
    /// Describes a data item which stems from a method argument passed by reference
    /// </summary>
    public class RefArgumentSource :
        PointerSource,
        IMethodRemappable<RefArgumentSource>,
        IMethodArgumentSource,
        IResolvableSource
    {
        /// <summary>
        /// Associated method argument
        /// </summary>
        public ParameterInfo Argument { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="argument">method argument</param>
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

    /// <summary>
    /// Describes a data item which is a method argument used to return data to the caller (i.e. passed by reference)
    /// </summary>
    public class ArgumentReturnSource :
        ObjectSource,
        IMethodRemappable<ArgumentReturnSource>,
        IMethodArgumentSource,
        IResolvableSource
    {
        /// <summary>
        /// Associated method argument
        /// </summary>
        public ParameterInfo Argument { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="argument">the method argument</param>
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

    /// <summary>
    /// Describes a data item which is the address of a method argument
    /// </summary>
    public class AddressOfArgumentSource :
        PointerSource,
        IMethodRemappable<AddressOfArgumentSource>,
        IMethodArgumentSource,
        IDereferenceableSource,
        IMutationSource
    {
        /// <summary>
        /// The method argument
        /// </summary>
        public ParameterInfo Argument { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="argument">the method argument</param>
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

    /// <summary>
    /// Describes the instance on which a method operates (i.e. "this")
    /// </summary>
    public class ThisSource :
        ObjectSource,
        IMethodRemappable<ThisSource>,
        IResolvableSource
    {
        /// <summary>
        /// The considered method
        /// </summary>
        public MethodBase Method { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">considered method</param>
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

    /// <summary>
    /// Describes the instance on which the method of a value type operates. Such an instance is formally a pointer, not an object.
    /// </summary>
    public class ThisPointerSource :
        PointerSource,
        IMethodRemappable<ThisPointerSource>,
        IResolvableSource
    {
        /// <summary>
        /// The considered method
        /// </summary>
        public MethodBase Method { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">the considered method</param>
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

    /// <summary>
    /// Describes a data item which is an array element
    /// </summary>
    public class ArrayElementSource :
        ObjectSource,
        ITerminalSource,
        IHostedSource
    {
        /// <summary>
        /// Possible array instances
        /// </summary>
        public IEnumerable<ObjectSource> ArraySources { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="arraySources">possible array instances</param>
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

    /// <summary>
    /// Describes a data item which is the address of an array element
    /// </summary>
    public class AddressOfArrayElementSource :
        PointerSource,
        IDereferenceableSource,
        IMutationSource
    {
        /// <summary>
        /// Possible array instances
        /// </summary>
        public IEnumerable<ObjectSource> ArraySources { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="arraySources">possible array instances</param>
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

    /// <summary>
    /// Describes a data item which is the return value of a method
    /// </summary>
    public class MethodReturnSource :
        ObjectSource,
        IMethodRemappable<MethodReturnSource>,
        IResolvableSource
    {
        /// <summary>
        /// Method returning the data item
        /// </summary>
        public MethodInfo Method { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method returning the data item</param>
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

    /// <summary>
    /// Describes a data item which is the address of a local variable
    /// </summary>
    public class AddressOfLocalVariableSource :
        PointerSource,
        IInternalSource,
        IMutationSource
    {
        /// <summary>
        /// Associated method
        /// </summary>
        public MethodBase Method { get; private set; }
        
        /// <summary>
        /// Index of local variable
        /// </summary>
        public int LocalIndex { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">associated method</param>
        /// <param name="localIndex">index of local variable</param>
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

    /// <summary>
    /// Describes a data item which is the result of dereferencing another data item
    /// </summary>
    public class DerefSource :
        ObjectSource,
        IResolvableSource
    {
        /// <summary>
        /// Possible pointers to dereference
        /// </summary>
        public IEnumerable<PointerSource> Sources { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="sources">possible pointers to dereference</param>
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

    /// <summary>
    /// Abstract base class for describing data modifications
    /// </summary>
    public abstract class ElementMutation
    {
        /// <summary>
        /// Possible right-hand side values of modification
        /// </summary>
        public IEnumerable<ElementSource> RValues { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="rvalues">possible right-hand side values of modification</param>
        public ElementMutation(IEnumerable<ElementSource> rvalues)
        {
            RValues = rvalues;
        }

        /// <summary>
        /// Data item being subject to modification
        /// </summary>
        public abstract ElementSource Mutatee { get; }

        /// <summary>
        /// Returns all terminal data items which might be directly affected by the modification
        /// </summary>
        public IEnumerable<ElementSource> GetMutatedTerminals()
        {
            return Mutatee.TrackToTerminals();
        }

        /// <summary>
        /// Returns all terminal data items which might be indirectly affected by the modification
        /// </summary>
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

    /// <summary>
    /// Describes the data modification of storing some value to a field
    /// </summary>
    public class StoreFieldMutation : ElementMutation
    {
        /// <summary>
        /// Instance of modified object
        /// </summary>
        public ElementSource Instance { get; private set; }

        /// <summary>
        /// Modified field
        /// </summary>
        public FieldInfo Field { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="instance">instance of modified object</param>
        /// <param name="field">modified field</param>
        /// <param name="rvalues">possible right-hand side values of modification</param>
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

    /// <summary>
    /// Describes the data modification of replacing an array element
    /// </summary>
    public class WriteArrayMutation : ElementMutation
    {
        /// <summary>
        /// Instance of modified array
        /// </summary>
        public ObjectSource Array { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="array">modified array</param>
        /// <param name="rvalues">possible right-hand side values</param>
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

    /// <summary>
    /// Describes the data modification of writing some value to a memory location
    /// </summary>
    public class IndirectMutation : ElementMutation
    {
        /// <summary>
        /// Pointer to the modified data
        /// </summary>
        public PointerSource Source { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="source">pointer to modified data</param>
        /// <param name="rvalues">possible right-hand side values of modification</param>
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

    /// <summary>
    /// Abstract base class for data modifications inside the local scope of a method
    /// </summary>
    public abstract class LocalMutation : 
        ElementMutation,
        IMethodLocation
    {
        /// <summary>
        /// Method performing the modification
        /// </summary>
        public MethodBase Method { get; private set; }

        /// <summary>
        /// Index of instruction performing the modification
        /// </summary>
        public int ILIndex { get; private set; }

        /// <summary>
        /// Whether the modification definitely overwrites the data (true) or may keep the old data (false)
        /// </summary>
        public bool OverwritesStrictly { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method performing the modification</param>
        /// <param name="ilIndex">instruction index performing the modification</param>
        /// <param name="strictly">whether the modification definitely overwrites the data (true) or may keep the old data (false)</param>
        /// <param name="rvalues">possible right-hand side values of modification</param>
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

    /// <summary>
    /// Describes the data modification of writing some value to a local variable
    /// </summary>
    public class LocalVariableMutation : 
        LocalMutation,
        IModifiesStackState
    {
        /// <summary>
        /// Index of local variable
        /// </summary>
        public int LocalIndex { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method performing the write operation</param>
        /// <param name="ilIndex">instruction index of write operation</param>
        /// <param name="strictly">whether the local variable is definitely overwritten (true) or may keep its old value (false)</param>
        /// <param name="localIndex">index of overwritten local variable</param>
        /// <param name="rvalues">possible right-hand side values of modification</param>
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

    /// <summary>
    /// Describes the data modification of writing some value to a method argument
    /// </summary>
    public class ArgumentMutation : 
        LocalMutation,
        IModifiesStackState
    {
        /// <summary>
        /// Index of method argument
        /// </summary>
        public int ArgumentPosition { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method performing the write operation</param>
        /// <param name="ilIndex">index of instruction performing the write operation</param>
        /// <param name="strictly">whether the argument will be definitely overwritten (true) or may keep its old value (false)</param>
        /// <param name="argPos">index of modified argument</param>
        /// <param name="rvalues">possible right-hand side values of modification</param>
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

    /// <summary>
    /// Describes the result of reading from a memory location
    /// </summary>
    public class IndirectLoad :
        IMethodLocation
    {
        /// <summary>
        /// Method performing the read operation
        /// </summary>
        public MethodBase Method { get; private set; }

        /// <summary>
        /// Index of instruction performing the read operation
        /// </summary>
        public int ILIndex { get; private set; }

        /// <summary>
        /// Dereferenced pointer
        /// </summary>
        public DerefSource Source { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="method">method performing the read operation</param>
        /// <param name="ilIndex">index of instruction performing the read operation</param>
        /// <param name="source">dereferenced pointer</param>
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

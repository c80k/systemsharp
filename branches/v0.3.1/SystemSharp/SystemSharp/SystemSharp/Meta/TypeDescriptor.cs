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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.SysDOM;

namespace SystemSharp.Meta
{
    /// <summary>
    /// Provides options for creating a value instance from a type descriptor.
    /// </summary>
    [Flags]
    public enum ETypeCreationOptions
    {
        /// <summary>
        /// Default option: Return any instance that suits the type descriptor.
        /// </summary>
        AnyObject = 0,

        /// <summary>
        /// Return an instance that behaves neutrally under addition (i.e. 0 of whatever type).
        /// </summary>
        AdditiveNeutral = 1,

        /// <summary>
        /// Return an instance that behaves neutrally under multiplication (i.e. 1 of whatever type).
        /// </summary>
        MultiplicativeNeutral = 2,

        /// <summary>
        /// Always create an instance, even if type information is incomplete, such that the type is ambiguous.
        /// </summary>
        ForceCreation = 4,

        /// <summary>
        /// Return <c>null</c> if type information is incomplete (instead of throwing an exception).
        /// </summary>
        ReturnNullIfUnavailable = 8,

        /// <summary>
        /// Return an instance which is different from 0 in an algebraic sense, i.e. the opposite of <c>AdditiveNeutral</c>.
        /// </summary>
        NonZero = 16
    }

    /// <summary>
    /// Factory interface for algebraic types, i.e. types which support basic arithmetics, including 
    /// additive neutral and multiplicative neutral elements.
    /// </summary>
    public interface IAlgebraicTypeFactory
    {
        /// <summary>
        /// Creates a value.
        /// </summary>
        /// <param name="options">creation options</param>
        /// <param name="template">exemplary value of another instance</param>
        /// <returns>a value according to the creation options</returns>
        object CreateInstance(ETypeCreationOptions options, object template);
    }

    /// <summary>
    /// Abstract attribute base class to be attached to any algebraic data type, i.e. a type which support basic arithmetics, 
    /// including additive neutral and multiplicative neutral elements.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Struct, Inherited = true)]
    public abstract class AlgebraicTypeAttribute :
        Attribute,
        IAlgebraicTypeFactory
    {
        /// <summary>
        /// Creates a value.
        /// </summary>
        /// <param name="options">creation options</param>
        /// <param name="template">exemplary value of another instance</param>
        /// <returns>a value according to the creation options</returns>
        public abstract object CreateInstance(ETypeCreationOptions options, object template);
    }

    class RootTypeLibrary :
        DescriptorBase
    {
        public static readonly RootTypeLibrary Instance = new RootTypeLibrary();
    }

    /// <summary>
    /// Describes a type.
    /// </summary>
    /// <remarks>
    /// In .NET/CLI, a type is completely described by an instance of <c>System.Reflection.Type</c>. However, for System#
    /// this information is not sufficient. E.g. we consider a <c>Signed</c> value of 12 bits length to have a different type
    /// than a <c>Signed</c> value of 16 bits length. This is solved by so-called type parameters. A type parameter is a property
    /// of a type whose per-instance value is considered to contribute to its type. Thus, type descriptors provide a more
    /// detailed type system which was specifically designed for hardware/embedded modelling.
    /// </remarks>
    public class TypeDescriptor :
        DescriptorBase
    {
        /// <summary>
        /// Type descriptor of <c>typeof(void)</c>.
        /// </summary>
        public static readonly TypeDescriptor VoidType = new TypeDescriptor(typeof(void));

        /// <summary>
        /// Type descriptor of <c>typeof(object)</c>.
        /// </summary>
        public static readonly TypeDescriptor NullType = new TypeDescriptor(typeof(object));

        /// <summary>
        /// The underlying CLI type information.
        /// </summary>
        public Type CILType { get; private set; }

        /// <summary>
        /// Whether the descriptor describes a System#-intrinsic type.
        /// </summary>
        public bool HasIntrinsicTypeOverride { get; private set; }

        /// <summary>
        /// The System#-intrinsic type symbol.
        /// </summary>
        public EIntrinsicTypes IntrinsicTypeOverride { get; private set; }

        /// <summary>
        /// The package in which this type descriptor is logically contained.
        /// </summary>
        public PackageDescriptor Package { get; internal set; }

        /// <summary>
        /// All type parameters.
        /// </summary>
        public object[] TypeParams { get; private set; }

        /// <summary>
        /// All type parameters converted to range constraints.
        /// </summary>
        public Range[] Constraints { get; private set; }

        /// <summary>
        /// Whether this type descriptor was artificially constructed during analysis of an array.
        /// </summary>
        public bool IsArtificial { get; private set; }

        /// <summary>
        /// Whether the described type does not have any type parameters.
        /// </summary>
        public bool IsUnconstrained { get; private set; }

        /// <summary>
        /// The type of an array element if this descriptor describes an array.
        /// </summary>
        public TypeDescriptor Element0Type { get; private set; }
        private object _sample;
        private Dictionary<MemberInfo, TypeDescriptor> _memberTypes = new Dictionary<MemberInfo, TypeDescriptor>();

        static TypeDescriptor()
        {
            NativeAlgebraicTypes.RegisterAttributes();
        }

        private TypeDescriptor()
        {
        }

        /// <summary>
        /// Constructs a type descriptor.
        /// </summary>
        /// <param name="sample">sample value from which to extract the type information</param>
        /// <param name="asPointer">whether to construct a pointer type</param>
        /// <param name="asReference">whether to construct a reference type</param>
        public TypeDescriptor(object sample, bool asPointer = false, bool asReference = false)
        {
            _sample = sample;
            CILType = sample.GetType();
            if (asReference)
                CILType = CILType.MakeByRefType();
            if (asPointer)
                CILType = CILType.MakePointerType();

            InitTypeParams();
            ComputeDependentTypes();
            Owner = RootTypeLibrary.Instance;
        }

        /// <summary>
        /// Constructs a type descriptor.
        /// </summary>
        /// <param name="cilType">CLI type</param>
        /// <param name="asPointer">whether to construct a pointer type</param>
        /// <param name="asReference">whether to construct a reference type</param>
        public TypeDescriptor(Type cilType, bool asPointer = false, bool asReference = false)
        {
            CILType = cilType;
            if (asReference)
                CILType = CILType.MakeByRefType();
            if (asPointer)
                CILType = CILType.MakePointerType();

            InitTypeParams();
            ComputeDependentTypes();
            Owner = RootTypeLibrary.Instance;
        }

        internal TypeDescriptor Clone()
        {
            var copy = new TypeDescriptor();
            copy.CILType = CILType;
            copy._sample = _sample;
            copy.InitTypeParams();
            copy.ComputeDependentTypes();
            copy.IsUnconstrained = IsUnconstrained;
            return copy;
        }

        private void InitTypeParams()
        {
            var mtit = CILType.GetCustomOrInjectedAttribute<MapToIntrinsicType>();
            if (mtit != null)
            {
                HasIntrinsicTypeOverride = true;
                IntrinsicTypeOverride = mtit.IntrinsicType;
            }
            else if (CILType.GetCustomOrInjectedAttribute<CompilerGeneratedAttribute>() != null)
            {
                HasIntrinsicTypeOverride = true;
                IntrinsicTypeOverride = EIntrinsicTypes.IllegalRuntimeType;
            }
            else
            {
                HasIntrinsicTypeOverride = false;
            }

            if (CILType.IsArray)
            {
                int rank = CILType.GetArrayRank();
                TypeParams = new object[rank];
                if (_sample != null)
                {
                    Array array = (Array)_sample;
                    for (int i = 0; i < rank; i++)
                        TypeParams[i] = array.GetLength(i);
                }
            }
            else
            {
                List<object> typeParams = new List<object>();
                foreach (PropertyInfo pi in CILType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object[] tparams = pi.GetCustomAndInjectedAttributes(typeof(TypeParameter));
                    if (tparams.Length > 0)
                    {
                        object arg = _sample == null ? null : pi.GetGetMethod().Invoke(_sample, new object[0]);
                        typeParams.Add(arg);
                    }
                }
                TypeParams = typeParams.ToArray();
            }

            if (IsComplete)
            {
                if (CILType.IsArray)
                {
                    Range[] result = new Range[Rank];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = new Range(0, (int)TypeParams[i] - 1, EDimDirection.To);
                    }
                    Constraints = result;
                }
                else
                {
                    List<Range> result = new List<Range>();
                    foreach (PropertyInfo pi in CILType.GetProperties(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        object[] tparams = pi.GetCustomAndInjectedAttributes(typeof(TypeParameter));
                        if (tparams.Length > 0)
                        {
                            TypeParameter tparam = (TypeParameter)tparams[0];
                            MethodInfo miconv = tparam.RangeConverter.GetMethod("ConvertToRange",
                                BindingFlags.Static | BindingFlags.Public);
                            object arg = pi.GetGetMethod().Invoke(_sample, new object[0]);
                            Range carg = (Range)miconv.Invoke(null, new object[] { arg });
                            result.Add(carg);
                        }
                    }
                    Constraints = result.ToArray();
                }
            }

            Element0Type = GetElement0Type();
        }

        /// <summary>
        /// Returns <c>true</c> if all necessary information to fully describe the type is complete.
        /// </summary>
        public bool IsComplete
        {
            get { return _sample != null || TypeParams.Length == 0 || IsUnconstrained; }
        }

        /// <summary>
        /// Returns the rank of the described array, or 0 if the type is not an array.
        /// </summary>
        public int Rank
        {
            get { return TypeParams.Length; }
        }

        /// <summary>
        /// Returns <c>true</c> if this descriptor describes an array and has type parameters.
        /// </summary>
        public bool IsConstrained
        {
            get { return Rank > 0 && !IsUnconstrained; }
        }

        /// <summary>
        /// Returns <c>true</c> if this descriptor describes a type reference.
        /// </summary>
        public bool IsByRef
        {
            get { return CILType.IsByRef; }
        }

        /// <summary>
        /// Represents the described multi-dimensional array as array of arrays. Only applicable to array types.
        /// </summary>
        public TypeDescriptor[] MakeRank1Types()
        {
            if (Rank == 0)
                throw new InvalidOperationException("This operation is possible for types with Rank >= 1");

            if (Rank == 1)
                return new TypeDescriptor[] { this };

            if (!CILType.IsArray)
                throw new InvalidOperationException("This operation is possible for arrays");

            TypeDescriptor[] result = new TypeDescriptor[Rank];
            Type innerType = CILType.GetElementType();
            TypeDescriptor tdElem = Element0Type;
            for (int i = result.Length - 1; i >= 0; i--)
            {
                innerType = innerType.MakeArrayType();
                result[i] = new TypeDescriptor()
                {
                    _sample = this._sample,
                    CILType = innerType,
                    HasIntrinsicTypeOverride = false,
                    TypeParams = new object[] { TypeParams[i] },
                    Constraints = this.Constraints == null ? null : new Range[] { Constraints[i] },
                    IsArtificial = true,
                    IsUnconstrained = false,
                    Element0Type = tdElem
                };
                tdElem = result[i];
            }
            return result;
        }

        /// <summary>
        /// Clones this type and clears the constraints of the first dimension. Only applicable to constrained array types.
        /// </summary>
        public TypeDescriptor MakeUnconstrainedType()
        {
            if (Rank == 0)
                throw new InvalidOperationException("This operation is only possible for types with Rank > 0");

            if (IsUnconstrained)
                throw new InvalidOperationException("This type is already unconstrained");

            TypeDescriptor tdu;
            if (_sample != null)
                tdu = new TypeDescriptor(_sample);
            else
                tdu = new TypeDescriptor(CILType);
            tdu.IsUnconstrained = true;
            return tdu;
        }

        private TypeDescriptor GetElement0Type()
        {
            if (_sample == null)
            {
                var etype = CILType.GetElementType();
                return etype == null ? null : new TypeDescriptor(etype);
            }

            if (CILType.IsArray)
            {
                Array array = (Array)_sample;
                if (array.Length == 0)
                    return new TypeDescriptor(CILType.GetElementType());

                object sample = array.GetValue(new int[array.Rank]);
                if (sample == null)
                    return new TypeDescriptor(CILType.GetElementType());
                else
                    return new TypeDescriptor(sample);
            }
            else if (CILType.IsByRef)
            {
                return new TypeDescriptor(_sample);
            }
            else if (Constraints.Length > 0)
            {
                var indexers = CILType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.GetIndexParameters().Length == Constraints.Length &&
                            p.GetIndexParameters().All(ip => ip.ParameterType == typeof(int)));
                if (indexers.Any())
                {
                    var indexer = indexers.First();
                    if (Constraints.All(c => c.Size > 0))
                    {
                        object[] index = Constraints.Select(r => (object)r.FirstBound).ToArray();
                        try
                        {
                            var indexSample = indexer.GetValue(_sample, index);
                            return TypeDescriptor.GetTypeOf(indexSample);
                        }
                        catch (Exception)
                        {
                            return indexer.PropertyType;
                        }
                    }
                    else
                    {
                        return indexer.PropertyType;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the type descriptors of all elements of the described type.
        /// </summary>
        /// <returns></returns>
        public TypeDescriptor[] GetDependentTypes()
        {
            if (CILType.IsArray)
                return new TypeDescriptor[] { Element0Type };
            else
                return _memberTypes.Values.ToArray();
        }

        private void ComputeDependentTypes()
        {
            if (!CILType.IsPrimitive)
            {
                var fields = CILType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (FieldInfo field in fields)
                {
                    bool consider = (CILType.IsValueType && !HasIntrinsicTypeOverride) ||
                        field.HasCustomOrInjectedAttribute<DependentType>();
                    if (!consider)
                        continue;

                    object fieldVal = _sample == null ? null : field.GetValue(_sample);
                    if (fieldVal == null)
                        _memberTypes[field] = new TypeDescriptor(field.FieldType);
                    else
                        _memberTypes[field] = new TypeDescriptor(fieldVal);
                }

                var properties = CILType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public);

                foreach (PropertyInfo prop in properties)
                {
                    bool consider = prop.HasCustomOrInjectedAttribute<DependentType>();
                    if (!consider)
                        continue;

                    object propVal = _sample == null ? null : prop.GetValue(_sample, new object[0]);
                    if (propVal == null)
                        _memberTypes[prop] = new TypeDescriptor(prop.PropertyType);
                    else
                        _memberTypes[prop] = new TypeDescriptor(propVal);
                }
            }
        }

        private string CheckStatic()
        {
            if (HasIntrinsicTypeOverride)
                return null;

            if (CILType.IsArray)
            {
                TypeDescriptor tdref = GetElement0Type();
                string innerCheck = tdref.CheckStatic();
                if (innerCheck != null)
                    return innerCheck;
                if (_sample == null)
                    return "No sample - not able to determine whether type descriptor is static";
                Array array = (Array)_sample;
                int[] indices = new int[array.Rank];
                do
                {
                    object elem = array.GetValue(indices);
                    if (elem == null)
                        return "Null array element";

                    TypeDescriptor td = new TypeDescriptor(elem);
                    if (!tdref.Equals(td))
                        return "Different element types in array";

                    int dim;
                    for (dim = 0; dim < indices.Length; dim++)
                    {
                        ++indices[dim];
                        if (indices[dim] < array.GetLength(dim))
                            break;
                        indices[dim] = 0;
                    }
                    if (dim == indices.Length)
                        break;
                }
                while (true);

                return null;
            }
            else
            {
                if (!CILType.IsValueType && !CILType.IsPrimitive)
                    return "Type is neither primitive nor a value type";

                TypeDescriptor[] deps = GetDependentTypes();
                foreach (TypeDescriptor dep in deps)
                {
                    string innerCheck = dep.CheckStatic();
                    if (innerCheck != null)
                        return innerCheck;
                }

                return null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the described type complies with the static System# type system.
        /// </summary>
        public bool IsStatic
        {
            get { return CheckStatic() == null; }
        }

        /// <summary>
        /// Checks whether the described type complies with the static System# type system and throws an exception
        /// if it is not.
        /// </summary>
        public void AssertStatic()
        {
            string msg = CheckStatic();
            if (msg != null)
                throw new InvalidOperationException("Type " + CILType.Name + ", sample instance " + _sample + ": " + msg);
        }

        /// <summary>
        /// Returns <c>true</c> if the described type is not able to carry any information because it has a 0-sized constraint.
        /// </summary>
        public bool IsEmptyType
        {
            get
            {
                return Constraints.Any(_ => _.Size == 0);
            }
        }

        public override string ToString()
        {
            string result = CILType.Name;
            if (IsUnconstrained)
                result += "[<>]";
            else
            {
                foreach (object arg in TypeParams)
                {
                    result += "[";
                    if (arg == null)
                        result += "?";
                    else
                        result += arg.ToString();
                    result += "]";
                }
            }
            return result;
        }

        public override string Name
        {
            get { return ToString(); }
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            //if (!IsComplete)
            //    return false;

            if (obj is TypeDescriptor)
            {
                TypeDescriptor td = (TypeDescriptor)obj;
                if (_sample != null && _sample == td._sample)
                    return true;
                if (td.IsComplete != td.IsComplete)
                    return false;
                if (!CILType.Equals(td.CILType))
                    return false;
                if (IsArtificial != td.IsArtificial)
                    return false;
                if (IsStatic != td.IsStatic)
                    return false;
                if (IsUnconstrained != td.IsUnconstrained)
                    return false;
                int start = IsUnconstrained ? 1 : 0;
                for (int i = start; i < TypeParams.Length; i++)
                {
                    if (!object.Equals(TypeParams[i], td.TypeParams[i]))
                        return false;
                }
                TypeDescriptor[] mydeps = GetDependentTypes();
                TypeDescriptor[] hisdeps = td.GetDependentTypes();
                if (mydeps.Length != hisdeps.Length)
                    return false;
                for (int i = 0; i < mydeps.Length; i++)
                {
                    if (!object.Equals(mydeps[i], hisdeps[i]))
                        return false;
                }
                return true;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            int hash = CILType.GetHashCode();
            if (!IsUnconstrained)
            {
                foreach (object arg in TypeParams)
                {
                    if (arg != null)
                        hash ^= arg.GetHashCode();
                    hash *= 3;
                }
            }

            TypeDescriptor[] deps = GetDependentTypes();
            foreach (TypeDescriptor dep in deps)
            {
                hash ^= dep.GetHashCode();
                hash *= 3;
            }

            return hash;
        }

        /// <summary>
        /// Returns the dimensional specification of the described type.
        /// </summary>
        public IndexSpec Index
        {
            get
            {
                if (Element0Type == null)
                    return new IndexSpec(Constraints.Select(c => (DimSpec)c));
                else
                    return new IndexSpec(Element0Type.Index.Indices.Concat(Constraints.Select(c => (DimSpec)c)));
            }
        }

        /// <summary>
        /// Creates a type descriptor from a CLI type.
        /// </summary>
        [Pure]
        public static TypeDescriptor MakeType(Type type)
        {
            Contract.Requires<ArgumentNullException>(type != null);
            return new TypeDescriptor(type);
        }

        /// <summary>
        /// Creates a type descriptor from an instance.
        /// </summary>
        [Pure]
        public static TypeDescriptor GetTypeOf(object instance)
        {
            Contract.Requires<ArgumentNullException>(instance != null);
            return new TypeDescriptor(instance);
        }

        /// <summary>
        /// Creates a type descriptor from a type and an optional instance.
        /// </summary>
        [Pure]
        public static TypeDescriptor MakeType(object instance, Type type)
        {
            Contract.Requires<ArgumentNullException>(type != null);
            Contract.Requires<ArgumentNullException>(instance == null || instance.GetType().Equals(type));

            if (instance == null)
                return MakeType(type);
            else
                return GetTypeOf(instance);
        }

        /// <summary>
        /// Represents the described type as reference type.
        /// </summary>
        public TypeDescriptor AsByRefType()
        {
            if (_sample != null)
                return new TypeDescriptor(_sample, false, true);
            else
                return new TypeDescriptor(CILType, false, true);
        }

        /// <summary>
        /// Represents the describes type as pointer type.
        /// </summary>
        public TypeDescriptor AsPointerType()
        {
            if (_sample != null)
                return new TypeDescriptor(_sample, true, false);
            else
                return new TypeDescriptor(CILType, true, false);
        }

        /// <summary>
        /// Implicitly converts the CLI type to a type descriptor.
        /// </summary>
        public static implicit operator TypeDescriptor(Type type)
        {
            return MakeType(type);
        }

        /// <summary>
        /// Creates a sample instance of the described type.
        /// </summary>
        /// <param name="options">creation options</param>
        public object GetSampleInstance(ETypeCreationOptions options = ETypeCreationOptions.AnyObject)
        {
            Contract.Requires(
                !(options.HasFlag(ETypeCreationOptions.AdditiveNeutral) && options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral)) &&
                !(options.HasFlag(ETypeCreationOptions.AdditiveNeutral) && options.HasFlag(ETypeCreationOptions.NonZero)) &&
                !(options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) && options.HasFlag(ETypeCreationOptions.NonZero)));

            if (IsArtificial)
                throw new InvalidOperationException("Cannot construct a sample instance from an artificial type");

            if (options.HasFlag(ETypeCreationOptions.AdditiveNeutral) ||
                options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                options.HasFlag(ETypeCreationOptions.NonZero))
            {
                var atf = CILType.GetCustomOrInjectedAttribute<IAlgebraicTypeFactory>();
                return atf.CreateInstance(options, _sample);
            }

            if (_sample != null)
                return _sample;

            if (options.HasFlag(ETypeCreationOptions.ReturnNullIfUnavailable))
                return null;

            if (!options.HasFlag(ETypeCreationOptions.ForceCreation) && (IsConstrained || !IsStatic))
                throw new InvalidOperationException("Cannot construct sample instance from constrained incomplete or non-static type");

            object inst = Activator.CreateInstance(CILType);
            return inst;
        }

        /// <summary>
        /// Returns a type descriptor for a field of the described type.
        /// </summary>
        /// <param name="field">field of the described type</param>
        /// <returns>descriptor for specified field</returns>
        public TypeDescriptor GetFieldType(FieldInfo field)
        {
            TypeDescriptor result;
            if (!_memberTypes.TryGetValue(field, out result))
                result = TypeDescriptor.MakeType(field.FieldType);
            return result;
        }

        /// <summary>
        /// If type is a tuple type, returns the types of the individual tuple items. Otherwise, returns an array which contains the type itself.
        /// </summary>
        /// <returns>Tuple item items.</returns>
        public TypeDescriptor[] Unpick()
        {
            if (HasIntrinsicTypeOverride &&
                IntrinsicTypeOverride == EIntrinsicTypes.Tuple)
            {
                return GetDependentTypes();
            }
            else
            {
                return new TypeDescriptor[] { this };
            }
        }
    }

}

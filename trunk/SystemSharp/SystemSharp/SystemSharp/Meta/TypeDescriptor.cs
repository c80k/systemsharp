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
    [Flags]
    public enum ETypeCreationOptions
    {
        AnyObject = 0,
        AdditiveNeutral = 1,
        MultiplicativeNeutral = 2,
        ForceCreation = 4,
        ReturnNullIfUnavailable = 8,
        NonZero = 16
    }

    public interface IAlgebraicTypeFactory
    {
        object CreateInstance(ETypeCreationOptions options, object template);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Struct, Inherited = true)]
    public abstract class AlgebraicTypeAttribute :
        Attribute,
        IAlgebraicTypeFactory
    {
        public abstract object CreateInstance(ETypeCreationOptions options, object template);
    }

    class RootTypeLibrary :
        DescriptorBase
    {
        public static readonly RootTypeLibrary Instance = new RootTypeLibrary();
    }

    public class TypeDescriptor :
        DescriptorBase
    {
        public static readonly TypeDescriptor VoidType = new TypeDescriptor(typeof(void));
        public static readonly TypeDescriptor NullType = new TypeDescriptor(typeof(object));

        public Type CILType { get; private set; }
        public bool HasIntrinsicTypeOverride { get; private set; }
        public EIntrinsicTypes IntrinsicTypeOverride { get; private set; }
        public PackageDescriptor Package { get; internal set; }
        public object[] TypeParams { get; private set; }
        public Range[] Constraints { get; private set; }
        public bool IsArtificial { get; private set; }
        public bool IsUnconstrained { get; private set; }
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

        public bool IsComplete
        {
            get { return _sample != null || TypeParams.Length == 0 || IsUnconstrained; }
        }

        public int Rank
        {
            get { return TypeParams.Length; }
        }

        public bool IsConstrained
        {
            get { return Rank > 0 && !IsUnconstrained; }
        }

        public bool IsByRef
        {
            get { return CILType.IsByRef; }
        }

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

        public string CheckStatic()
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

        public bool IsStatic
        {
            get { return CheckStatic() == null; }
        }

        public void AssertStatic()
        {
            string msg = CheckStatic();
            if (msg != null)
                throw new InvalidOperationException("Type " + CILType.Name + ", sample instance " + _sample + ": " + msg);
        }

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

        [Pure]
        public static TypeDescriptor MakeType(Type type)
        {
            Contract.Requires<ArgumentNullException>(type != null);
            return new TypeDescriptor(type);
        }

        [Pure]
        public static TypeDescriptor GetTypeOf(object instance)
        {
            Contract.Requires<ArgumentNullException>(instance != null);
            return new TypeDescriptor(instance);
        }

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

        public TypeDescriptor AsByRefType()
        {
            if (_sample != null)
                return new TypeDescriptor(_sample, false, true);
            else
                return new TypeDescriptor(CILType, false, true);
        }

        public TypeDescriptor AsPointerType()
        {
            if (_sample != null)
                return new TypeDescriptor(_sample, true, false);
            else
                return new TypeDescriptor(CILType, true, false);
        }

        public static implicit operator TypeDescriptor(Type type)
        {
            return MakeType(type);
        }

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

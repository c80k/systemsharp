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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Synthesis
{
    /// <summary>
    /// Service interface for serializing and deserializing objects to and from <c>StdLogicVector</c> instances.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serializes an object as <c>StdLogicVector</c>
        /// </summary>
        /// <param name="value">object to serialize</param>
        /// <returns>object representation as <c>StdLogicVector</c></returns>
        StdLogicVector Serialize(object value);

        /// <summary>
        /// Deserializes an object from an <c>StdLogicVector</c> instance.
        /// </summary>
        /// <param name="slv">logic vector a deserialize</param>
        /// <param name="targetType">target type for deserialization</param>
        /// <returns>the deserialized instance of <paramref name="targetType"/></returns>
        object Deserialize(StdLogicVector slv, TypeDescriptor targetType);
    }

    /// <summary>
    /// Indicates that the tagged class or struct is capable of serializing and deserializing itself to and from
    /// <c>StdLogicVector</c> instances.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class SLVSerializable : Attribute
    {
        /// <summary>
        /// Type of object to serialize/deserialize
        /// </summary>
        public Type ObjectType { get; private set; }

        /// <summary>
        /// Type of serializer implementation
        /// </summary>
        public Type SerializerType { get; private set; }

        /// <summary>
        /// Constructs the attribute.
        /// </summary>
        /// <param name="objectType">type of object to serialize/deserialize</param>
        /// <param name="serializerType">Type of serializer implementation. The type must implement
        /// <c>ISerializer</c> and provide a public default constructor.</param>
        public SLVSerializable(Type objectType, Type serializerType)
        {
            ObjectType = objectType;
            SerializerType = serializerType;
        }

        /// <summary>
        /// Tries to find a serializer for the specified type. To do so, it looks for the <c>SLVSerializable</c> attribute
        /// in both the type attributes and <c>StdLogicVector</c> attributes.
        /// </summary>
        /// <param name="type">type of object to serialize/deserialize</param>
        /// <returns>a suitable serializer instance, or <c>null</c> if noch such was found</returns>
        public static ISerializer TryGetSerializer(Type type)
        {
            object[] tattrs = type.GetCustomAttributes(typeof(SLVSerializable), false);
            object[] sattrs = typeof(StdLogicVector).GetCustomAttributes(typeof(SLVSerializable), false);
            SLVSerializable result = tattrs.Union(sattrs).Cast<SLVSerializable>().Where(a => a.ObjectType.Equals(type)).FirstOrDefault();
            if (result == null)
                return null;
            else
                return (ISerializer)Activator.CreateInstance(result.SerializerType);
        }
    }

    public interface IMarshalInfo
    {
        /// <summary>
        /// The target memory's word size (in bits).
        /// </summary>
        uint WordSize { get; }

        /// <summary>
        /// If true, multiple array elements may be packed into a single memory word.
        /// </summary>
        bool UseArraySubWordAlignment { get; }

        /// <summary>
        /// If true, array dimensions are aligned to powers of two (simplifies indexing).
        /// </summary>
        bool UseArrayDimPow2Alignment { get; }

        /// <summary>
        /// If true, each data item is forced to a start address which is a multiple of the next power of 2 with respect to its size.
        /// </summary>
        bool UseStrongPow2Alignment { get; }

        /// <summary>
        /// Determines the minimum alignment constraint for data items, in multiples of memory words.
        /// </summary>
        uint Alignment { get; }
    }

    /// <summary>
    /// Default implementation of <c>IMarshalInfo</c>
    /// </summary>
    public class DefaultMarshalInfo : IMarshalInfo
    {
        /// <summary>
        /// Gets or sets the word size. Pre-initialized value is 64.
        /// </summary>
        public uint WordSize { get; set;  }

        /// <summary>
        /// Gets or sets the usage flag for array sub-word alignment. Pre-initialized value is <c>true</c>.
        /// </summary>
        public bool UseArraySubWordAlignment { get; set; }

        /// <summary>
        /// Gets or sets the usage flag for power-of-2 alignment of arrays. Pre-initialized value is <c>true</c>.
        /// </summary>
        public bool UseArrayDimPow2Alignment { get; set; }

        /// <summary>
        /// Gets or sets the usage flag for strong power-of-2 alignment. Pre-initialized value is <c>false</c>.
        /// </summary>
        public bool UseStrongPow2Alignment { get; set;  }

        /// <summary>
        /// Gets or sets the word alignment. Pre-initialized value is 1.
        /// </summary>
        public uint Alignment { get; set;  }

        public DefaultMarshalInfo()
        {
            WordSize = 64;
            Alignment = 1;
            UseArraySubWordAlignment = true;
            UseArrayDimPow2Alignment = true;
            UseStrongPow2Alignment = false;
        }
    }

    /// <summary>
    /// A default implementation of <c>IMarshalInfo</c> for <c>StdLogicVector</c> serialization.
    /// </summary>
    public class HWMarshalInfo : IMarshalInfo
    {
        /// <summary>
        /// Always 1
        /// </summary>
        public uint WordSize
        {
            get { return 1; }
        }

        /// <summary>
        /// Always <c>true</c>
        /// </summary>
        public bool UseArraySubWordAlignment
        {
            get { return true; }
        }

        /// <summary>
        /// Always <c>false</c>
        /// </summary>
        public bool UseArrayDimPow2Alignment
        {
            get { return false; }
        }

        /// <summary>
        /// Always <c>false</c>
        /// </summary>
        public bool UseStrongPow2Alignment
        {
            get { return false; }
        }

        /// <summary>
        /// Always 1
        /// </summary>
        public uint Alignment
        {
            get { return 1; }
        }

        private HWMarshalInfo()
        {
        }

        /// <summary>
        /// The one and only instance
        /// </summary>
        public static readonly IMarshalInfo Instance = new HWMarshalInfo();
    }

    /// <summary>
    /// Represents the memory layout of a field instance.
    /// </summary>
    public class FieldLocation
    {
        /// <summary>
        /// Layouted field
        /// </summary>
        public FieldInfo Field { get; private set; }

        /// <summary>
        /// Memory layout of the field
        /// </summary>
        public MemoryLayout FieldLayout { get; private set; }

        /// <summary>
        /// Start offset inside the superordinate layout
        /// </summary>
        public ulong Offset { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="field">layouted field</param>
        /// <param name="fieldLayout">memory layout of the field</param>
        /// <param name="offset">start offset inside the superordinate layout</param>
        public FieldLocation(FieldInfo field, MemoryLayout fieldLayout, ulong offset)
        {
            Field = field;
            FieldLayout = fieldLayout;
            Offset = offset;
        }
    }

    /// <summary>
    /// Abstract base class for memory layouts
    /// </summary>
    [ContractClass(typeof(MemoryLayoutContractClass))]
    public abstract class MemoryLayout
    {
        /// <summary>
        /// The layouted type descriptor.
        /// </summary>
        public TypeDescriptor LayoutedType { get; private set; }

        /// <summary>
        /// Size of the layout in words, whereby word size is platform-dependent.
        /// </summary>
        public ulong Size { get; internal set;  }

        /// <summary>
        /// Size of the layout in bits.
        /// </summary>
        public ulong SizeInBits { get; internal set; }

        internal MemoryLayout(TypeDescriptor layoutedType)
        {
            LayoutedType = layoutedType;
        }

        /// <summary>
        /// Serializes an instance of the layouted type.
        /// </summary>
        /// <param name="instance">object to serialize</param>
        /// <returns>array of words, whereby each word is represented by an <c>StdLogicVector</c> instance</returns>
        public abstract StdLogicVector[] SerializeInstance(object instance);
    }

    [ContractClassFor(typeof(MemoryLayout))]
    abstract class MemoryLayoutContractClass: MemoryLayout
    {
        public MemoryLayoutContractClass() :
            base(null)
        {
        }

        public override StdLogicVector[] SerializeInstance(object instance)
        {
            Contract.Requires<ArgumentNullException>(instance != null);
            Contract.Requires<ArgumentException>(TypeDescriptor.GetTypeOf(instance).Equals(LayoutedType));
            return null;
        }
    }

    /// <summary>
    /// Memory layout for primitive types.
    /// </summary>
    public class PrimMemoryLayout: MemoryLayout
    {
        /// <summary>
        /// Word size in bits.
        /// </summary>
        public int WordSize { get; private set; }

        internal PrimMemoryLayout(TypeDescriptor layoutedType, int wordSize):
            base(layoutedType)
        {
            WordSize = wordSize;
        }

        public override StdLogicVector[] SerializeInstance(object instance)
        {
            StdLogicVector plain = StdLogicVector.Serialize(instance);
            StdLogicVector[] result = new StdLogicVector[Size];
            for (int i = 0; i < (int)Size; i++)
            {
                int upper = (i + 1) * WordSize - 1;
                if (upper >= plain.Size)
                {
                    result[i] = StdLogicVector._0s(upper - plain.Size + 1).Concat(plain[plain.Size - 1, i * WordSize]);
                }
                else
                {
                    result[i] = plain[(i + 1) * WordSize - 1, i * WordSize];
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Memory layout for enumeration types.
    /// </summary>
    public class EnumMemoryLayout : MemoryLayout
    {
        private uint _wordSize;

        internal EnumMemoryLayout(TypeDescriptor layoutedType, IMarshalInfo minfo) :
            base(layoutedType)
        {
            Contract.Requires(layoutedType.CILType.IsEnum);
            SizeInBits = (ulong)NumBits;
            _wordSize = minfo.WordSize;
            Size = (SizeInBits + minfo.WordSize - 1) / minfo.WordSize;
        }

        /// <summary>
        /// Returns the number of bits which are required an instance of the layouted enum.
        /// </summary>
        public int NumBits
        {
            get { return MathExt.CeilLog2(LayoutedType.CILType.GetEnumValues().Length); }
        }

        public override StdLogicVector[] SerializeInstance(object instance)
        {
            Contract.Assert(instance.GetType().Equals(LayoutedType.CILType));
            var values = LayoutedType.CILType.GetEnumValues();
            int index = Array.IndexOf(values, instance);
            int wsize = (int)_wordSize;
            int alignedNum = (NumBits + wsize - 1) / wsize * wsize;
            StdLogicVector vec = StdLogicVector.FromInt(index, alignedNum);
            StdLogicVector[] result = new StdLogicVector[Size];
            for (int i = 0; i < (int)Size; i++)
            {
                result[i] = vec[i + wsize - 1, i];
            }
            return result;
        }
    }

    /// <summary>
    /// Memory layout for structs.
    /// </summary>
    public class StructMemoryLayout: MemoryLayout
    {
        private Dictionary<FieldInfo, FieldLocation> _locations = new Dictionary<FieldInfo, FieldLocation>();

        internal StructMemoryLayout(TypeDescriptor layoutedType) :
            base(layoutedType)
        {
        }

        internal void AddLocation(FieldLocation loc)
        {
            _locations[loc.Field] = loc;
        }

        /// <summary>
        /// Returns the start offset of a particular field (must be member of the layouted struct).
        /// </summary>
        /// <param name="field">field to look for</param>
        /// <returns>start offset of the field inside the memory layout</returns>
        public ulong GetFieldOffset(FieldInfo field)
        {
            return _locations[field].Offset;
        }

        /// <summary>
        /// Returns the memory layout of a particular field (must be member of the layouted struct).
        /// </summary>
        /// <param name="field">field to look for</param>
        /// <returns>start offset of the field inside the memory layout</returns>
        public MemoryLayout GetFieldLayout(FieldInfo field)
        {
            return _locations[field].FieldLayout;
        }

        public override StdLogicVector[] SerializeInstance(object instance)
        {
            StdLogicVector[] result = new StdLogicVector[Size];

            foreach (var kvp in _locations)
            {
                StdLogicVector[] fieldData =
                    kvp.Value.FieldLayout.SerializeInstance(
                        kvp.Key.GetValue(instance));
                Array.Copy(fieldData, 0, result, (int)kvp.Value.Offset, fieldData.Length);
            }

            return result;
        }
    }

    /// <summary>
    /// Dummy memory layout which does not consume any storage space.
    /// </summary>
    public class EmptyMemoryLayout : MemoryLayout
    {
        internal EmptyMemoryLayout(TypeDescriptor layoutedType) :
            base(layoutedType)
        {
        }

        public override StdLogicVector[] SerializeInstance(object instance)
        {
            return new StdLogicVector[0];
        }
    }

    /// <summary>
    /// Memory layout for arrays.
    /// </summary>
    public class ArrayMemoryLayout : MemoryLayout
    {
        /// <summary>
        /// Platform-specific word size
        /// </summary>
        public uint WordSize { get; private set; }

        /// <summary>
        /// For each dimension the amount of words to get from one index to the next.
        /// </summary>
        public ulong[] Strides { get; private set; }

        /// <summary>
        /// In case of array sub-word packing: the amount of bits to get from one array element to the next.
        /// </summary>
        public ulong SubStride { get; private set; }

        /// <summary>
        /// Number of array elements per word
        /// </summary>
        public uint ElementsPerWord { get; private set; }

        /// <summary>
        /// Number of word per array element
        /// </summary>
        public uint WordsPerElement { get; private set; }

        /// <summary>
        /// Memory layout of a single array element
        /// </summary>
        public MemoryLayout ElementLayout { get; private set; }

        internal ArrayMemoryLayout(TypeDescriptor layoutedType, 
            uint wordSize,
            ulong[] strides, ulong subStride, uint elementsPerWord, uint wordsPerElement, MemoryLayout elementLayout) :
            base(layoutedType)
        {
            Contract.Requires(layoutedType != null);
            Contract.Requires(wordSize >= 1);
            Contract.Requires(strides != null);
            Contract.Requires(strides.Length >= 1);
            Contract.Requires(strides.Take(strides.Length - 1).All(n => n >= 1));
            Contract.Requires(elementLayout != null);

            WordSize = wordSize;
            Strides = strides;
            SubStride = subStride;
            ElementsPerWord = elementsPerWord;
            WordsPerElement = wordsPerElement;
            ElementLayout = elementLayout;
        }

        public override StdLogicVector[] SerializeInstance(object instance)
        {
            Array array = (Array)instance;
            IEnumerable<object> elements = array.Cast<object>();
            int rank = Strides.Length;
            int[] curSrcIndex = new int[rank];
            int curDstIndex = 0;
            int curSubIndex = 0;
            StdLogicVector[] result = Enumerable.Repeat(
                StdLogicVector._0s(WordSize), array.GetLength(0) * (int)Strides[0])
                .ToArray();
            foreach (object element in elements)
            {
                StdLogicVector[] elementData = ElementLayout.SerializeInstance(element);
                curDstIndex = 0;
                for (int i = 0; i < rank; i++)
                {
                    curDstIndex += (int)Strides[i] * curSrcIndex[i];
                }
                if (SubStride == 0)
                {
                    Array.Copy(elementData, 0, result, curDstIndex, elementData.Length);
                }
                else
                {
                    Debug.Assert(elementData.Length == 1);
                    result[curDstIndex][(curSubIndex+1) * (int)SubStride - 1, curSubIndex * (int)SubStride] = 
                        elementData[0][(int)SubStride - 1, 0];
                    if (++curSubIndex == ElementsPerWord)
                    {
                        curSubIndex = 0;
                    }
                    else
                    {
                        continue;
                    }
                }
                for (int i = rank - 1; i >= 0; i--)
                {
                    if (++curSrcIndex[i] == array.GetLength(i))
                        curSrcIndex[i] = 0;
                    else
                        break;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// This static class provides convenience methods for serialization/deserialization and memory layout.
    /// </summary>
    public static class Marshal
    {
        /// <summary>
        /// Converts from measure "bits" to "words", depending on the platform-specific word size.
        /// </summary>
        /// <param name="numBits">number of bits</param>
        /// <param name="info">marshalling information</param>
        /// <returns>number of words</returns>
        private static ulong BitsToWords(long numBits, IMarshalInfo info)
        {
            return (ulong)((numBits + info.WordSize - 1) / info.WordSize);
        }

        private static MemoryLayout CreatePrimLayout(long numBits, TypeDescriptor type, IMarshalInfo info)
        {
            ulong numWords = BitsToWords(numBits, info);
            return new PrimMemoryLayout(type, (int)info.WordSize)
            {
                Size = numWords,
                SizeInBits = (ulong)numBits
            };
        }

        /// <summary>
        /// Computes a memory layout for a given type descriptor.
        /// </summary>
        /// <param name="td">type descriptor to layout</param>
        /// <param name="info">marshalling information</param>
        /// <returns>memory layout</returns>
        public static MemoryLayout Layout(TypeDescriptor td, IMarshalInfo info)
        {
            Type type = td.CILType;
            ISerializer ser = SLVSerializable.TryGetSerializer(type);
            if (ser != null)
            {
                object sample = td.GetSampleInstance();
                return CreatePrimLayout(ser.Serialize(sample).Size, td, info);
            }
            if (ser == null && td.HasIntrinsicTypeOverride)
                throw new InvalidOperationException("Type " + type.Name + " has intrinsic type override but no serializer");
            if (type.IsEnum)
            {
                return new EnumMemoryLayout(td, info);
            }
            if (type.IsArray)
            {
                td.AssertStatic();
                TypeDescriptor elemTd = td.Element0Type;
                MemoryLayout elemLayout = Layout(elemTd, info);
                ulong subStride = elemLayout.SizeInBits;
                if (subStride == 0)
                {
                    return new EmptyMemoryLayout(td)
                    {
                        Size = 0,
                        SizeInBits = 0
                    };
                }
                ulong[] strides = new ulong[type.GetArrayRank()];
                ulong elemsPerWord = (ulong)info.WordSize / elemLayout.SizeInBits;
                ulong wordsPerElem = elemLayout.Size;
                if (elemsPerWord > 1)
                {
                    if (info.UseArraySubWordAlignment)
                    {
                        if (info.UseArrayDimPow2Alignment)
                        {
                            elemsPerWord = MathExt.FloorPow2(elemsPerWord);
                            subStride = info.WordSize / elemsPerWord;
                        }
                    }
                    else
                    {
                        elemsPerWord = 1;
                    }
                }
                ulong dimSize = (ulong)(int)td.TypeParams.Last(); 
                ulong dimWords;
                if (elemsPerWord <= 1)
                {
                    subStride = 0;
                    if (info.UseArrayDimPow2Alignment)
                        wordsPerElem = MathExt.CeilPow2(wordsPerElem);
                    dimWords = wordsPerElem * dimSize;
                }
                else
                {
                    wordsPerElem = 0;
                    dimWords = (dimSize + elemsPerWord - 1) / elemsPerWord;
                }
                strides[strides.Length-1] = wordsPerElem;
                for (int i = strides.Length-2; i >= 0; i--)
                {
                    if (info.UseArrayDimPow2Alignment)
                        dimWords = MathExt.CeilPow2(dimWords);
                    strides[i] = dimWords;
                    dimSize = (ulong)(int)td.TypeParams[i];
                    dimWords *= dimSize;
                }
                return new ArrayMemoryLayout(td, info.WordSize, strides, subStride, (uint)elemsPerWord, (uint)wordsPerElem, elemLayout)
                {
                    Size = dimWords,
                    SizeInBits = dimWords * info.WordSize
                };
            }
            if (type.IsValueType && !type.IsPrimitive)
            {
                StructMemoryLayout ml = new StructMemoryLayout(td);
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                ulong offset = 0;
                foreach (FieldInfo field in fields)
                {
                    TypeDescriptor fieldTd = td.GetFieldType(field);
                    MemoryLayout fieldLayout = Layout(fieldTd, info);
                    FieldLocation fieldLoc = new FieldLocation(field, fieldLayout, offset);
                    ml.AddLocation(fieldLoc);
                    offset += fieldLayout.Size;
                }
                ml.Size = offset;
                ml.SizeInBits = offset * info.WordSize;
                return ml;
            }

            throw new InvalidOperationException("Unable to create data layout for type " + type.Name);
        }

        /// <summary>
        /// Computes a serialization of a value with respect to a specific marshaling information.
        /// </summary>
        /// <param name="value">value to serialize</param>
        /// <param name="minfo">marshalling information</param>
        /// <returns></returns>
        public static StdLogicVector[] Serialize(object value, IMarshalInfo minfo)
        {
            var layout = Layout(TypeDescriptor.GetTypeOf(value), minfo);
            return layout.SerializeInstance(value);
        }

        /// <summary>
        /// Computes a serialization of a value, assuming a word size of 1.
        /// </summary>
        /// <param name="value">value to serialize</param>
        /// <returns>its serialization</returns>
        public static StdLogicVector SerializeForHW(object value)
        {
            // For pure optimization reasons: Shortcut for two most
            // common datatypes.
            if (value is StdLogicVector)
                return (StdLogicVector)value;
            else if (value is StdLogic)
                return ((StdLogic)value).Concat("");

            var slvs = Serialize(value, HWMarshalInfo.Instance);
            return slvs.Select(slv => slv[0]).ToArray();
        }
    }

    /// <summary>
    /// A memory region is part of a memory region hierarchy and contains memory mappings of constants and variables.
    /// </summary>
    public class MemoryRegion
    {
        /// <summary>
        /// Marshalling information
        /// </summary>
        public IMarshalInfo MarshalInfo { get; private set; }

        /// <summary>
        /// Parent region
        /// </summary>
        public MemoryRegion Parent { get; private set; }

        /// <summary>
        /// Base address of this region
        /// </summary>
        public ulong BaseAddress { get; private set; }

        private ulong _requiredSize;

        /// <summary>
        /// Gets or sets the required size of this region.
        /// </summary>
        public ulong RequiredSize 
        {
            get { return _requiredSize; }
            set
            {
                if (IsSealed)
                    throw new InvalidOperationException();

                _requiredSize = value;
            }
        }

        internal void Seal()
        {
            if (IsSealed)
                throw new InvalidOperationException();

            IsSealed = true;
            Items = new ReadOnlyCollection<MemoryMappedStorage>(Items);
        }

        /// <summary>
        /// Returns <c>true</c> if the region is sealed, i.e. it is not mutable anymore.
        /// </summary>
        public bool IsSealed { get; private set; }

        /// <summary>
        /// The list of mapped items.
        /// </summary>
        public IList<MemoryMappedStorage> Items { get; private set; }

        internal MemoryRegion(IMarshalInfo marshalInfo)
        {
            Items = new List<MemoryMappedStorage>();
            MarshalInfo = marshalInfo;
        }

        internal MemoryRegion(MemoryRegion parent, ulong baseAddress)
        {
            Items = new List<MemoryMappedStorage>();
            Parent = parent;
            MarshalInfo = parent.MarshalInfo;
            BaseAddress = baseAddress;
        }

        /// <summary>
        /// Maps a variable to this region.
        /// </summary>
        /// <param name="v">variable to map</param>
        /// <returns>storage mapping of the variable</returns>
        public MemoryMappedStorage Map(Variable v)
        {
            Contract.Requires(v != null);
            var result = new MemoryMappedStorage(v, this, Marshal.Layout(v.Type, MarshalInfo));
            Items.Add(result);
            return result;
        }

        /// <summary>
        /// Maps a constant to this region.
        /// </summary>
        /// <param name="data">constant value to map</param>
        /// <returns>storage mapping of the constant</returns>
        public MemoryMappedStorage Map(object data)
        {
            Contract.Requires(data != null);
            var result = new MemoryMappedStorage(data, this, Marshal.Layout(TypeDescriptor.GetTypeOf(data), MarshalInfo));
            Items.Add(result);
            return result;
        }

        /// <summary>
        /// Maps a data item to this region.
        /// </summary>
        /// <param name="item">object which is used a key to identify the data item</param>
        /// <param name="dataType">type of actual data</param>
        /// <returns>sotrage mapping of the data item</returns>
        public MemoryMappedStorage Map(object item, TypeDescriptor dataType)
        {
            Contract.Requires(item != null);
            Contract.Requires(dataType != null);
            var result = new MemoryMappedStorage(item, dataType, this, Marshal.Layout(dataType, MarshalInfo));
            Items.Add(result);
            return result;
        }

        /// <summary>
        /// Returns the required address with for this region.
        /// </summary>
        public int AddressWidth
        {
            get
            {
                if (RequiredSize == 0)
                    return 0;
                else
                    return MathExt.CeilLog2(RequiredSize); 
            }
        }

        public override string ToString()
        {
            return string.Format("MemRgn[base: {0:X} size: {1:X}]", BaseAddress, RequiredSize);
        }
    }

    /// <summary>
    /// Represents a constant, variable or data item which is mapped to a memory region.
    /// </summary>
    public class MemoryMappedStorage
    {
        /// <summary>
        /// Kind of mapping
        /// </summary>
        public enum EKind
        {
            Variable,
            Data,
            DataItem
        }

        /// <summary>
        /// The kind of mapping
        /// </summary>
        public EKind Kind { get; private set; }

        /// <summary>
        /// Mapped variable (in case of a variable mapping)
        /// </summary>
        public IStorable Variable { get; private set; }

        /// <summary>
        /// Mapped constant value (in case of a constant value mapping)
        /// </summary>
        public object Data { get; private set; }

        /// <summary>
        /// Mapped data item (in case of a data item mapping)
        /// </summary>
        public object DataItem { get; private set; }

        /// <summary>
        /// Type of item data (in case of data item mapping)
        /// </summary>
        public TypeDescriptor DataItemType { get; private set; }

        /// <summary>
        /// Region to which this mapping refers
        /// </summary>
        public MemoryRegion Region { get; private set; }

        /// <summary>
        /// Start offset of the mapping
        /// </summary>
        public ulong Offset { get; set; }

        /// <summary>
        /// Memory layout of the mapped data
        /// </summary>
        public MemoryLayout Layout { get; private set; }

        internal MemoryMappedStorage(IStorable variable, MemoryRegion region, MemoryLayout layout)
        {
            Kind = EKind.Variable;
            Variable = variable;
            Region = region;
            Layout = layout;
        }

        internal MemoryMappedStorage(object data, MemoryRegion region, MemoryLayout layout)
        {
            Kind = EKind.Data;
            Data = data;
            Region = region;
            Layout = layout;
        }

        internal MemoryMappedStorage(object dataItem, TypeDescriptor dataItemType, MemoryRegion region, MemoryLayout layout)
        {
            Kind = EKind.DataItem;
            DataItem = dataItem;
            DataItemType = dataItemType;
            Region = region;
            Layout = layout;
        }

        /// <summary>
        /// Returns the size of the memory layout used for this mapping.
        /// </summary>
        public ulong Size
        {
            get { return Layout.Size; }
        }

        /// <summary>
        /// Returns the base address of this mapping.
        /// </summary>
        public Unsigned BaseAddress
        {
            get { return Unsigned.FromULong(Region.BaseAddress + Offset, Region.AddressWidth); }
        }
    }

    /// <summary>
    /// Common interface for memory layout algorithms.
    /// </summary>
    public interface IMemoryLayoutAlgorithm
    {
        /// <summary>
        /// Computes a memory layout for a given region.
        /// </summary>
        /// <param name="region">region to layout</param>
        void Layout(MemoryRegion region);
    }

    /// <summary>
    /// Provides an entry point for memory mapping.
    /// </summary>
    public class MemoryMapper
    {
        /// <summary>
        /// The root region for memory mapping.
        /// </summary>
        public MemoryRegion DefaultRegion { get; private set; }

        /// <summary>
        /// Gets or sets the marshalling information.
        /// </summary>
        public IMarshalInfo MarshalInfo { get; set; }

        /// <summary>
        /// Gets or sets the memory layout algorithm. Pre-initialized with an instance of
        /// <c>DefaultMemoryLayoutAlgorithm</c>.
        /// </summary>
        public IMemoryLayoutAlgorithm LayoutAlgorithm { get; set; }

        /// <summary>
        /// Constructs an instance of the memory mapper.
        /// </summary>
        public MemoryMapper()
        {
            MarshalInfo = new DefaultMarshalInfo();
            LayoutAlgorithm = new DefaultMemoryLayoutAlgorithm();
            DefaultRegion = new MemoryRegion(MarshalInfo);
        }

        /// <summary>
        /// Computes the overall memory layout.
        /// </summary>
        public void DoLayout()
        {
            LayoutAlgorithm.Layout(DefaultRegion);
        }
    }

    /// <summary>
    /// This static class provides extension methods to work with marshalling information.
    /// </summary>
    public static class MarshalInfoExtensions
    {
        /// <summary>
        /// Returns a type descriptor for describing a single word for the marshalling information.
        /// </summary>
        /// <param name="mi">marshalling information</param>
        /// <returns>descriptor of a single word</returns>
        public static TypeDescriptor GetRawWordType(this IMarshalInfo mi)
        {
            return TypeDescriptor.GetTypeOf(
                StdLogicVector._0s(mi.WordSize));
        }
    }
}
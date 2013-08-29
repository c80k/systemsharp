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
    public interface ISerializer
    {
        StdLogicVector Serialize(object value);
        object Deserialize(StdLogicVector slv, TypeDescriptor targetType);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class SLVSerializable : Attribute
    {
        public Type ObjectType { get; private set; }
        public Type SerializerType { get; private set; }

        public SLVSerializable(Type objectType, Type serializerType)
        {
            ObjectType = objectType;
            SerializerType = serializerType;
        }

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
        /// The target memory's word size (in bits)
        /// </summary>
        uint WordSize { get; }

        /// <summary>
        /// If true, multiple array elements may be packed into a single memory word
        /// </summary>
        bool UseArraySubWordAlignment { get; }

        /// <summary>
        /// If true, array dimensions are aligned to powers of two (simplifies indexing)
        /// </summary>
        bool UseArrayDimPow2Alignment { get; }

        /// <summary>
        /// If true, each data item is forced to a start address which is a multiple of the next power of 2 with respect to its size
        /// </summary>
        bool UseStrongPow2Alignment { get; }

        /// <summary>
        /// Determines the minimum alignment constraint for data items, in multiples of memory words.
        /// </summary>
        uint Alignment { get; }
    }

    public class DefaultMarshalInfo : IMarshalInfo
    {
        public uint WordSize { get; set;  }
        public bool UseArraySubWordAlignment { get; set; }
        public bool UseArrayDimPow2Alignment { get; set; }
        public bool UseStrongPow2Alignment { get; set;  }
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

    public class HWMarshalInfo : IMarshalInfo
    {
        public uint WordSize
        {
            get { return 1; }
        }

        public bool UseArraySubWordAlignment
        {
            get { return true; }
        }

        public bool UseArrayDimPow2Alignment
        {
            get { return false; }
        }

        public bool UseStrongPow2Alignment
        {
            get { return false; }
        }

        public uint Alignment
        {
            get { return 1; }
        }

        private HWMarshalInfo()
        {
        }

        public static readonly IMarshalInfo Instance = new HWMarshalInfo();
    }

    public class FieldLocation
    {
        public FieldInfo Field { get; private set; }
        public MemoryLayout FieldLayout { get; private set; }
        public ulong Offset { get; private set; }

        public FieldLocation(FieldInfo field, MemoryLayout fieldLayout, ulong offset)
        {
            Field = field;
            FieldLayout = fieldLayout;
            Offset = offset;
        }
    }

    [ContractClass(typeof(MemoryLayoutContractClass))]
    public abstract class MemoryLayout
    {
        public TypeDescriptor LayoutedType { get; private set; }
        public ulong Size { get; internal set;  }
        public ulong SizeInBits { get; internal set; }

        internal MemoryLayout(TypeDescriptor layoutedType)
        {
            LayoutedType = layoutedType;
        }

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

    public class PrimMemoryLayout: MemoryLayout
    {
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

        public ulong GetFieldOffset(FieldInfo field)
        {
            return _locations[field].Offset;
        }

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

    public class ArrayMemoryLayout : MemoryLayout
    {
        public uint WordSize { get; private set; }
        public ulong[] Strides { get; private set; }
        public ulong SubStride { get; private set; }
        public uint ElementsPerWord { get; private set; }
        public uint WordsPerElement { get; private set; }
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

    public static class Marshal
    {
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

        public static StdLogicVector[] Serialize(object value, IMarshalInfo minfo)
        {
            var layout = Layout(TypeDescriptor.GetTypeOf(value), minfo);
            return layout.SerializeInstance(value);
        }

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

    public class MemoryRegion
    {
        public IMarshalInfo MarshalInfo { get; private set; }
        public MemoryRegion Parent { get; private set; }
        public ulong BaseAddress { get; private set; }

        private ulong _requiredSize;
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

        public bool IsSealed { get; private set; }

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

        public MemoryMappedStorage Map(Variable v)
        {
            Contract.Requires(v != null);
            var result = new MemoryMappedStorage(v, this, Marshal.Layout(v.Type, MarshalInfo));
            Items.Add(result);
            return result;
        }

        public MemoryMappedStorage Map(object data)
        {
            Contract.Requires(data != null);
            var result = new MemoryMappedStorage(data, this, Marshal.Layout(TypeDescriptor.GetTypeOf(data), MarshalInfo));
            Items.Add(result);
            return result;
        }

        public MemoryMappedStorage Map(object item, TypeDescriptor dataType)
        {
            Contract.Requires(item != null);
            Contract.Requires(dataType != null);
            var result = new MemoryMappedStorage(item, dataType, this, Marshal.Layout(dataType, MarshalInfo));
            Items.Add(result);
            return result;
        }

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

    public class MemoryMappedStorage
    {
        public enum EKind
        {
            Variable,
            Data,
            DataItem
        }

        public EKind Kind { get; private set; }
        public IStorable Variable { get; private set; }
        public object Data { get; private set; }
        public object DataItem { get; private set; }
        public TypeDescriptor DataItemType { get; private set; }
        public MemoryRegion Region { get; private set; }
        public ulong Offset { get; set; }
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

        public ulong Size
        {
            get { return Layout.Size; }
        }

        public Unsigned BaseAddress
        {
            get { return Unsigned.FromULong(Region.BaseAddress + Offset, Region.AddressWidth); }
        }
    }

    public interface IMemoryLayoutAlgorithm
    {
        void Layout(MemoryRegion region);
    }

    public class MemoryMapper
    {
        public MemoryRegion DefaultRegion { get; private set; }
        public IMarshalInfo MarshalInfo { get; set; }
        public IMemoryLayoutAlgorithm LayoutAlgorithm { get; set; }

        public MemoryMapper()
        {
            MarshalInfo = new DefaultMarshalInfo();
            LayoutAlgorithm = new DefaultMemoryLayoutAlgorithm();
            DefaultRegion = new MemoryRegion(MarshalInfo);
        }

        public void DoLayout()
        {
            LayoutAlgorithm.Layout(DefaultRegion);
        }
    }

    public static class MarshalInfoExtensions
    {
        public static TypeDescriptor GetRawWordType(this IMarshalInfo mi)
        {
            return TypeDescriptor.GetTypeOf(
                StdLogicVector._0s(mi.WordSize));
        }
    }
}
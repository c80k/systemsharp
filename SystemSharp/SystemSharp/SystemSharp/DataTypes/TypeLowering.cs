/**
 * Copyright 2011-2012 Christian Köllner
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
using System.Text;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.DataTypes
{
    public class TypeLowering
    {
        public abstract class TypeLoweringInfo
        {
            public TypeLoweringInfo(Type orgType)
            {
                OrgType = orgType;
            }

            public Type OrgType { get; private set; }
            public abstract bool IsHardwareType { get; }
            public abstract bool IsWireType { get; }
            public abstract bool IsSigned { get; }
            public abstract bool HasHardwareType { get; }
            public abstract bool HasWireType { get; }
            public abstract bool HasFixedSize { get; }
            public abstract int FixedSize { get; }
            public abstract TypeDescriptor MakeWireType(TypeDescriptor ctype);
            public abstract TypeDescriptor MakeHardwareType(TypeDescriptor ctype);
            public abstract StdLogicVector ConvertValueToWireType(object value);
            public abstract object ConvertValueToHardwareType(object value);
        }

        class SLType : TypeLoweringInfo
        {
            public SLType() :
                base(typeof(StdLogic))
            {
            }

            public override bool IsHardwareType
            {
                get { return true; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return false; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return true; }
            }

            public override int FixedSize
            {
                get { return 1; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                return TypeDescriptor.GetTypeOf((StdLogicVector)"0");
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return ctype;
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                StdLogic sl = (StdLogic)value;
                return StdLogicVector.FromStdLogic(sl);
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return value;
            }
        }

        class SLVType : TypeLoweringInfo
        {
            public SLVType() :
                base(typeof(StdLogicVector))
            {
            }

            public override bool IsHardwareType
            {
                get { return true; }
            }

            public override bool IsWireType
            {
                get { return true; }
            }

            public override bool IsSigned
            {
                get { return false; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return false; }
            }

            public override int FixedSize
            {
                get { return -1; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                return ctype;
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return ctype;
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                return (StdLogicVector)value;
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return ConvertValueToWireType(value);
            }
        }

        class SignedType : TypeLoweringInfo
        {
            public SignedType() :
                base(typeof(Signed))
            {
            }

            public override bool IsHardwareType
            {
                get { return true; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return true; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return false; }
            }

            public override int FixedSize
            {
                get { return -1; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(ctype.Constraints[0].Size));
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return ctype;
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                return StdLogicVector.Serialize(value);
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return value;
            }
        }

        class UnsignedType : TypeLoweringInfo
        {
            public UnsignedType() :
                base(typeof(Unsigned))
            {
            }

            public override bool IsHardwareType
            {
                get { return true; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return false; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return false; }
            }

            public override int FixedSize
            {
                get { return -1; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(ctype.Constraints[0].Size));
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return ctype;
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                return StdLogicVector.Serialize(value);
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return value;
            }
        }

        class SFixType : TypeLoweringInfo
        {
            public SFixType() :
                base(typeof(SFix))
            {
            }

            public override bool IsHardwareType
            {
                get { return true; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return true; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return false; }
            }

            public override int FixedSize
            {
                get { return -1; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                FixFormat fmt = (FixFormat)ctype.TypeParams[0];
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(fmt.TotalWidth));
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return ctype;
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                return ((SFix)value).SignedValue.SLVValue;
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return value;
            }
        }

        class UFixType : TypeLoweringInfo
        {
            public UFixType() :
                base(typeof(UFix))
            {
            }

            public override bool IsHardwareType
            {
                get { return true; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return false; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return false; }
            }

            public override int FixedSize
            {
                get { return -1; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                FixFormat fmt = (FixFormat)ctype.TypeParams[0];
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(fmt.TotalWidth));
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return ctype;
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                return ((UFix)value).UnsignedValue.SLVValue;
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return value;
            }
        }

        class NativeIntegralType : TypeLoweringInfo
        {
            private bool _isSigned;
            private int _size;

            public NativeIntegralType(Type orgType, bool isSigned, int size) :
                base(orgType)
            {
                _isSigned = isSigned;
                _size = size;
            }

            public override bool IsHardwareType
            {
                get { return false; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return _isSigned; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return true; }
            }

            public override int FixedSize
            {
                get { return _size; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(_size));
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                if (_isSigned)
                    return TypeDescriptor.GetTypeOf(Signed.FromInt(0, _size));
                else
                    return TypeDescriptor.GetTypeOf(Unsigned.FromUInt(0, _size));
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                return StdLogicVector.Serialize(value);
            }

            public override object ConvertValueToHardwareType(object value)
            {
                if (_isSigned)
                {
                    long longVal = (long)Convert.ChangeType(value, typeof(long));
                    return Signed.FromLong(longVal, _size);
                }
                else
                {
                    ulong ulongVal = (ulong)Convert.ChangeType(value, typeof(ulong));
                    return Unsigned.FromULong(ulongVal, _size);
                }
            }
        }

        class NativeFloatType : TypeLoweringInfo
        {
            private int _size;

            public NativeFloatType(Type orgType, int size) :
                base(orgType)
            {
                _size = size;
            }

            public override bool IsHardwareType
            {
                get { return true; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return true; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return true; }
            }

            public override int FixedSize
            {
                get { return _size; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(_size));
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return OrgType;
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                return StdLogicVector.Serialize(value);
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return value;
            }
        }

        class NativeBool : TypeLoweringInfo
        {
            public NativeBool() :
                base(typeof(bool))
            {
            }

            public override bool IsHardwareType
            {
                get { return false; }
            }

            public override bool IsWireType
            {
                get { return false; }
            }

            public override bool IsSigned
            {
                get { return false; }
            }

            public override bool HasHardwareType
            {
                get { return true; }
            }

            public override bool HasWireType
            {
                get { return true; }
            }

            public override bool HasFixedSize
            {
                get { return true; }
            }

            public override int FixedSize
            {
                get { return 1; }
            }

            public override TypeDescriptor MakeWireType(TypeDescriptor ctype)
            {
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(1));
            }

            public override TypeDescriptor MakeHardwareType(TypeDescriptor ctype)
            {
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s(1));
            }

            public override StdLogicVector ConvertValueToWireType(object value)
            {
                bool flag = (bool)value;
                return flag ? "1" : "0";
            }

            public override object ConvertValueToHardwareType(object value)
            {
                return ConvertValueToWireType(value);
            }
        }

        public static readonly TypeLowering Instance = new TypeLowering();

        private Dictionary<Type, TypeLoweringInfo> _tiLookup = new Dictionary<Type, TypeLoweringInfo>();

        public TypeLowering()
        {
            DeclareType(new SLType());
            DeclareType(new SLVType());
            DeclareType(new NativeBool());
            DeclareType(new NativeIntegralType(typeof(sbyte), true, 8));
            DeclareType(new NativeIntegralType(typeof(byte), false, 8));
            DeclareType(new NativeIntegralType(typeof(short), true, 16));
            DeclareType(new NativeIntegralType(typeof(ushort), false, 16));
            DeclareType(new NativeIntegralType(typeof(int), true, 32));
            DeclareType(new NativeIntegralType(typeof(uint), false, 32));
            DeclareType(new NativeIntegralType(typeof(long), true, 64));
            DeclareType(new NativeIntegralType(typeof(ulong), false, 64));
            DeclareType(new NativeFloatType(typeof(float), 32));
            DeclareType(new NativeFloatType(typeof(double), 64));
            DeclareType(new SignedType());
            DeclareType(new UnsignedType());
            DeclareType(new SFixType());
            DeclareType(new UFixType());
        }

        public void DeclareType(TypeLoweringInfo tli)
        {
            _tiLookup[tli.OrgType] = tli;
        }

        public TypeDescriptor GetWireType(TypeDescriptor otype)
        {
            Contract.Requires(otype != null);
            Contract.Requires(otype.IsUnconstrained || otype.IsComplete, "Incomplete type");

            TypeLoweringInfo tli;
            if (_tiLookup.TryGetValue(otype.CILType, out tli))
            {
                if (!tli.HasWireType)
                {
                    throw new InvalidOperationException("Not convertible to a wire type " + otype.Name);
                }
                return tli.MakeWireType(otype);
            }
            else
            {
                MemoryLayout layout = Marshal.Layout(otype, HWMarshalInfo.Instance);
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s((long)layout.SizeInBits));
            }            
        }

        public bool HasWireType(TypeDescriptor otype)
        {
            Contract.Requires(otype != null);
            Contract.Requires(otype.IsUnconstrained || otype.IsComplete, "Incomplete type");

            TypeLoweringInfo tli;
            if (_tiLookup.TryGetValue(otype.CILType, out tli))
                return true; // Assume that Marshal will find a solution

            return tli.HasWireType;
        }

        public int GetWireWidth(TypeDescriptor otype)
        {
            TypeDescriptor wtype = GetWireType(otype);
            return wtype.Constraints[0].FirstBound - wtype.Constraints[0].SecondBound + 1;
        }

        public TypeDescriptor GetHardwareType(TypeDescriptor otype)
        {
            Contract.Requires(otype != null);
            Contract.Requires(otype.IsUnconstrained || otype.IsComplete, "Incomplete type");

            TypeLoweringInfo tli;
            if (_tiLookup.TryGetValue(otype.CILType, out tli))
            {
                if (!tli.HasHardwareType)
                    throw new InvalidOperationException("Not convertible to a hardware type " + otype.Name);

                return tli.MakeHardwareType(otype);
            }
            else
            {
                MemoryLayout layout = Marshal.Layout(otype, HWMarshalInfo.Instance);
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s((long)layout.SizeInBits));
            }
        }

        public TypeDescriptor[] GetWireTypes(IEnumerable<TypeDescriptor> otypes)
        {
            return otypes.Select(t => GetWireType(t)).ToArray();
        }

        public object ConvertToHardwareType(object value)
        {
            Contract.Requires(value != null);
            
            Type otype = value.GetType();

            TypeLoweringInfo tli;
            if (_tiLookup.TryGetValue(otype, out tli))
            {
                if (!tli.HasHardwareType)
                    throw new InvalidOperationException("Not convertible to a hardware type " + otype.Name);

                return tli.ConvertValueToHardwareType(value);
            }
            else
            {
                MemoryLayout layout = Marshal.Layout(otype, HWMarshalInfo.Instance);
                StdLogicVector[] ser = layout.SerializeInstance(value);
                Debug.Assert(ser.Length == 1);
                return ser[0];
            }
        }
    }
}

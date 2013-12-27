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
using System.Text;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.DataTypes
{
    /// <summary>
    /// Provides services to represent datatypes of higher abstraction levels by hardware-near types.
    /// </summary>
    public class TypeLowering
    {
        /// <summary>
        /// Abstract base class for implementing a type lowering service for a particular type.
        /// </summary>
        public abstract class TypeLoweringInfo
        {
            /// <summary>
            /// Constructs an instance.
            /// </summary>
            /// <param name="orgType">Datatype for which this service is implemented.</param>
            public TypeLoweringInfo(Type orgType)
            {
                OrgType = orgType;
            }

            /// <summary>
            /// Datatype for which this service is implemented.
            /// </summary>
            public Type OrgType { get; private set; }

            /// <summary>
            /// Returns <c>true</c> if <c>OrgType</c> is a hardware type.
            /// </summary>
            public abstract bool IsHardwareType { get; }

            /// <summary>
            /// Returns <c>true</c> if <c>OrgType</c> is a wire type.
            /// </summary>
            public abstract bool IsWireType { get; }

            /// <summary>
            /// Returns <c>true</c> if <c>OrgType</c> described signed numbers.
            /// </summary>
            public abstract bool IsSigned { get; }

            /// <summary>
            /// Returns <c>true</c> of <c>OrgType</c> has a hardware type representations.
            /// </summary>
            public abstract bool HasHardwareType { get; }

            /// <summary>
            /// Returns <c>true</c> if <c>OrgType</c> has a wire type representation.
            /// </summary>
            public abstract bool HasWireType { get; }

            /// <summary>
            /// Returns <c>true</c> if the serialization of any <c>OrgType</c> instance has the same size.
            /// </summary>
            public abstract bool HasFixedSize { get; }

            /// <summary>
            /// Returns the fixed size of any <c>OrgType</c> instance serizalization.
            /// </summary>
            public abstract int FixedSize { get; }

            /// <summary>
            /// Constructs a wire type for <paramref name="ctype"/>, whereby <paramref name="ctype"/> must describe <c>OrgType</c>.
            /// </summary>
            public abstract TypeDescriptor MakeWireType(TypeDescriptor ctype);

            /// <summary>
            /// Constructs a hardware type for <paramref name="ctype"/>, whereby <paramref name="ctype"/> must describe <c>OrgType</c>.
            /// </summary>
            public abstract TypeDescriptor MakeHardwareType(TypeDescriptor ctype);

            /// <summary>
            /// Serializes <paramref name="value"/>.
            /// </summary>
            public abstract StdLogicVector ConvertValueToWireType(object value);

            /// <summary>
            /// Serializes <paramref name="value"/> as any hardware type of choice.
            /// </summary>
            public abstract object ConvertValueToHardwareType(object value);
        }

        /// <summary>
        /// Type lowering service for <c>StdLogic</c>
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>StdLogicVector</c>
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>Signed</c>
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>Unsigned</c>
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>SFix</c>
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>UFix</c>
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>sbyte</c>, <c>byte</c>, <c>short</c>, <c>ushort</c>, <c>int</c>, <c>uint</c>,
        /// <c>long</c>, <c>ulong</c>.
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>float</c> and <c>double</c>
        /// </summary>
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

        /// <summary>
        /// Type lowering service for <c>bool</c>
        /// </summary>
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

        /// <summary>
        /// The singleton instance of the type lowering service
        /// </summary>
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

        /// <summary>
        /// Adds another type lowering service
        /// </summary>
        /// <param name="tli">type lowering service to add</param>
        public void DeclareType(TypeLoweringInfo tli)
        {
            _tiLookup[tli.OrgType] = tli;
        }

        /// <summary>
        /// Returns a wire-level type for <paramref name="otype"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="otype"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="otype"/> is incomplete or is not convertible to a wire-level type.</exception>
        public TypeDescriptor GetWireType(TypeDescriptor otype)
        {
            Contract.Requires<ArgumentNullException>(otype != null, "otype");
            Contract.Requires<ArgumentException>(otype.IsUnconstrained || otype.IsComplete, "Incomplete type");

            TypeLoweringInfo tli;
            if (_tiLookup.TryGetValue(otype.CILType, out tli))
            {
                if (!tli.HasWireType)
                {
                    throw new ArgumentException("Not convertible to a wire type " + otype.Name);
                }
                return tli.MakeWireType(otype);
            }
            else
            {
                MemoryLayout layout = Marshal.Layout(otype, HWMarshalInfo.Instance);
                return TypeDescriptor.GetTypeOf(StdLogicVector._0s((long)layout.SizeInBits));
            }            
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="otype"/> has a wire-level type.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="otype"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="otype"/> is incomplete.</exception>
        public bool HasWireType(TypeDescriptor otype)
        {
            Contract.Requires<ArgumentNullException>(otype != null, "otype");
            Contract.Requires<ArgumentException>(otype.IsUnconstrained || otype.IsComplete, "Incomplete type");

            TypeLoweringInfo tli;
            if (_tiLookup.TryGetValue(otype.CILType, out tli))
                return true; // Assume that Marshal will find a solution

            return tli.HasWireType;
        }

        /// <summary>
        /// Returns the size of a wire-level serizalization of <paramref name="otype"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="otype"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="otype"/> is incomplete or is not convertible to a wire-level type.</exception>
        public int GetWireWidth(TypeDescriptor otype)
        {
            TypeDescriptor wtype = GetWireType(otype);
            return wtype.Constraints[0].FirstBound - wtype.Constraints[0].SecondBound + 1;
        }

        /// <summary>
        /// Returns a hardware-level type for <paramref name="otype"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="otype"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="otype"/> is incomplete or is not convertible to a hardware-level type.</exception>
        public TypeDescriptor GetHardwareType(TypeDescriptor otype)
        {
            Contract.Requires<ArgumentNullException>(otype != null, "otype");
            Contract.Requires<ArgumentException>(otype.IsUnconstrained || otype.IsComplete, "Incomplete type");

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

        /// <summary>
        /// Converts <paramref name="value"/> to a hardware-level serialization.
        /// </summary>
        /// <exception cref="ArgumentNullException">if <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">if <paramref name="value"/> does not have any known hardware-level serialization.</exception>
        public object ConvertToHardwareType(object value)
        {
            Contract.Requires<ArgumentNullException>(value != null, "value");
            
            Type otype = value.GetType();

            TypeLoweringInfo tli;
            if (_tiLookup.TryGetValue(otype, out tli))
            {
                if (!tli.HasHardwareType)
                    throw new ArgumentException("Not convertible to a hardware type " + otype.Name);

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

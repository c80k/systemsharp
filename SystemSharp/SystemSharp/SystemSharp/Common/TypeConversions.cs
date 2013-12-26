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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SystemSharp.DataTypes;
using SystemSharp.Meta;

namespace SystemSharp.Common
{
    public static class TypeConversions
    {
        /// <summary>
        /// Converts <paramref name="v"/> to IEEE 754 double precision format, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix and UFix are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="v"/> == null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion from type of <paramref name="v"/>.</exception>
        public static double ToDouble(object v)
        {
            Contract.Requires<ArgumentNullException>(v != null, "v");
            return (double)ConvertValue(v, typeof(double));
        }

        /// <summary>
        /// Converts <paramref name="v"/> to "long" datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix and UFix are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="v"/> == null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion from type of <paramref name="v"/>.</exception>
        public static long ToLong(object v)
        {
            Contract.Requires<ArgumentNullException>(v != null, "v");
            return (long)ConvertValue(v, typeof(long));
        }

        /// <summary>
        /// Converts <paramref name="v"/> to "ulong" datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix and UFix are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="v"/> == null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion from type of <paramref name="v"/>.</exception>
        public static ulong ToULong(object v)
        {
            Contract.Requires<ArgumentNullException>(v != null, "v");
            return (ulong)ConvertValue(v, typeof(ulong));
        }

        /// <summary>
        /// Converts <paramref name="src"/> to the enumeration literal of <paramref name="dstType"/> whose ordinal number
        /// is <paramref name="src"/>. Values being a combination of multiple literals of a [Flag] enum are NOT supported.
        /// </summary>
        /// <param name="src">ordinal value</param>
        /// <param name="dstType">target enumeration type</param>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="ArgumentException">if <paramref name="dstType"/> is not an enum</exception>
        /// <exception cref="ArgumentOutOfRangeException">if there is no corresponding literal</exception>
        public static object ConvertToEnum(long src, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");
            Contract.Requires<ArgumentException>(dstType.IsEnum, "Destination type is not an enum type");

            Array enumValues = dstType.GetEnumValues();
            FieldInfo[] fields = dstType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            FieldInfo valueField = fields[0];
            object conv = null;
            foreach (object enumValue in enumValues)
            {
                long lval = ToLong(valueField.GetValue(enumValue));
                if (lval == src)
                {
                    conv = enumValue;
                    break;
                }
            }
            if (conv == null)
                throw new ArgumentOutOfRangeException("No matching enum value");

            return conv;
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types and enum types are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertLong(long src, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.Equals(typeof(bool)))
                return src != 0 ? true : false;
            if (dstType.Equals(typeof(sbyte)))
                return (sbyte)src;
            else if (dstType.Equals(typeof(byte)))
                return (byte)src;
            else if (dstType.Equals(typeof(char)))
                return (char)src;
            else if (dstType.Equals(typeof(short)))
                return (short)src;
            else if (dstType.Equals(typeof(ushort)))
                return (ushort)src;
            else if (dstType.Equals(typeof(int)))
                return (int)src;
            else if (dstType.Equals(typeof(uint)))
                return (uint)src;
            else if (dstType.Equals(typeof(long)))
                return src;
            else if (dstType.Equals(typeof(ulong)))
                return (ulong)src;
            else if (dstType.Equals(typeof(double)))
                return (double)src;
            else if (dstType.Equals(typeof(float)))
                return (float)src;
            else if (dstType.IsEnum)
                return ConvertToEnum(src, dstType);
            else if (dstType.Equals(typeof(object)))
                return src;
            else
                throw new NotImplementedException(string.Format("Don't know how to convert 'long' value to {0}", dstType));
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertLong(long src, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.CILType.Equals(typeof(Signed)))
                return Signed.FromLong(src, SFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(Unsigned)))
                return Unsigned.FromBigInt(new System.Numerics.BigInteger(src), UFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(SFix)))
                return SFix.FromSigned(Signed.FromLong(src, SFix.GetFormat(dstType).IntWidth), SFix.GetFormat(dstType).FracWidth);
            else if (dstType.CILType.Equals(typeof(StdLogicVector)))
                return StdLogicVector.FromLong(src, StdLogicVector.GetLength(dstType));
            else
                return ConvertLong(src, dstType.CILType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types and enum types are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertULong(ulong src, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.Equals(typeof(sbyte)))
                return (sbyte)src;
            else if (dstType.Equals(typeof(byte)))
                return (byte)src;
            else if (dstType.Equals(typeof(short)))
                return (short)src;
            else if (dstType.Equals(typeof(ushort)))
                return (ushort)src;
            else if (dstType.Equals(typeof(int)))
                return (int)src;
            else if (dstType.Equals(typeof(uint)))
                return (uint)src;
            else if (dstType.Equals(typeof(long)))
                return (long)src;
            else if (dstType.Equals(typeof(ulong)))
                return (ulong)src;
            else
                throw new NotImplementedException();
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertULong(ulong src, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.CILType.Equals(typeof(Unsigned)))
                return Unsigned.FromULong(src, UFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(Signed)))
                return Signed.FromBigInt(new System.Numerics.BigInteger(src), UFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(UFix)))
                return UFix.FromUnsigned(Unsigned.FromULong(src, UFix.GetFormat(dstType).IntWidth), UFix.GetFormat(dstType).FracWidth);
            else
                return ConvertULong(src, dstType.CILType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types and enum types are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertSigned(Signed src, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.Equals(typeof(double)))
                return SFix.FromSigned(src, 0).DoubleValue;
            else
                return ConvertValue(src.LongValue, dstType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertSigned(Signed src, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.CILType.Equals(typeof(Signed)))
                return src.Resize(SFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(SFix)))
                return SFix.FromSigned(src.Resize(SFix.GetFormat(dstType).TotalWidth), SFix.GetFormat(dstType).FracWidth);
            else
                return ConvertSigned(src, dstType.CILType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types and enum types are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertUnsigned(Unsigned src, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.Equals(typeof(double)))
                return UFix.FromUnsigned(src, 0).DoubleValue;
            else
                return ConvertValue(src.ULongValue, dstType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertUnsigned(Unsigned src, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.CILType.Equals(typeof(Unsigned)))
                return src.Resize(UFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(UFix)))
                return UFix.FromUnsigned(src.Resize(UFix.GetFormat(dstType).TotalWidth), UFix.GetFormat(dstType).FracWidth);
            else
                return ConvertUnsigned(src, dstType.CILType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types and enum types are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertSFix(SFix src, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.Equals(typeof(double)))
                return src.DoubleValue;
            else if (dstType.Equals(typeof(sbyte)))
                return (sbyte)src.Resize(8, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(byte)))
                return (byte)src.UFixValue.Resize(8, 0).UnsignedValue.ULongValue;
            else if (dstType.Equals(typeof(short)))
                return (short)src.Resize(16, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(ushort)))
                return (byte)src.UFixValue.Resize(16, 0).UnsignedValue.ULongValue;
            else if (dstType.Equals(typeof(int)))
                return (int)src.Resize(32, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(uint)))
                return (uint)src.UFixValue.Resize(32, 0).UnsignedValue.ULongValue;
            else if (dstType.Equals(typeof(long)))
                return src.Resize(64, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(ulong)))
                return src.UFixValue.Resize(64, 0).UnsignedValue.ULongValue;
            else
                throw new NotImplementedException();
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertSFix(SFix src, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.CILType.Equals(typeof(SFix)))
                return src.Resize(SFix.GetFormat(dstType).IntWidth, SFix.GetFormat(dstType).FracWidth);
            else if (dstType.CILType.Equals(typeof(Signed)))
                return src.SignedValue.Resize(SFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(StdLogicVector)))
                return src.SLVValue[StdLogicVector.GetLength(dstType) - 1, 0];
            else
                return ConvertSFix(src, dstType.CILType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types and enum types are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertUFix(UFix src, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.Equals(typeof(double)))
                return src.DoubleValue;
            else if (dstType.Equals(typeof(sbyte)))
                return (sbyte)src.SFixValue.Resize(8, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(byte)))
                return (byte)src.Resize(8, 0).UnsignedValue.ULongValue;
            else if (dstType.Equals(typeof(short)))
                return (short)src.SFixValue.Resize(16, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(ushort)))
                return (byte)src.Resize(16, 0).UnsignedValue.ULongValue;
            else if (dstType.Equals(typeof(int)))
                return (int)src.SFixValue.Resize(32, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(uint)))
                return (uint)src.Resize(32, 0).UnsignedValue.ULongValue;
            else if (dstType.Equals(typeof(long)))
                return src.SFixValue.Resize(64, 0).SignedValue.LongValue;
            else if (dstType.Equals(typeof(ulong)))
                return src.Resize(64, 0).UnsignedValue.ULongValue;
            else
                throw new NotImplementedException();
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertUFix(UFix src, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.CILType.Equals(typeof(UFix)))
                return src.Resize(UFix.GetFormat(dstType).IntWidth, UFix.GetFormat(dstType).FracWidth);
            else if (dstType.CILType.Equals(typeof(Unsigned)))
                return src.UnsignedValue.Resize(UFix.GetFormat(dstType).IntWidth);
            else if (dstType.CILType.Equals(typeof(StdLogicVector)))
                return src.SLVValue[StdLogicVector.GetLength(dstType) - 1, 0];
            else
                return ConvertUFix(src, dstType.CILType);
        }

        /// <summary>
        /// Converts <paramref name="src"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion to <paramref name="dstType"/></exception>
        public static object ConvertDouble(double src, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");

            if (dstType.CILType.Equals(typeof(SFix)))
            {
                var fmt = SFix.GetFormat(dstType);
                return SFix.FromDouble(src, fmt.IntWidth, fmt.FracWidth);
            }
            else if (dstType.CILType.Equals(typeof(UFix)))
            {
                var fmt = UFix.GetFormat(dstType);
                return UFix.FromDouble(src, fmt.IntWidth, fmt.FracWidth);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts <paramref name="srcValue"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types and enum types are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion from type of <paramref name="srcValue"/> to <paramref name="dstType"/></exception>
        public static object ConvertValue(object srcValue, Type dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");
            Contract.Requires<ArgumentNullException>(!dstType.IsValueType || srcValue != null, "srcValue");

            if (srcValue == null)
                return null;

            if (dstType.IsPointer || dstType.IsByRef)
                dstType = dstType.GetElementType();

            Type srcType = srcValue.GetType();

            if (srcType.Equals(dstType))
                return srcValue;

            if (!dstType.IsPrimitive && 
                !srcType.IsPrimitive &&
                dstType.IsAssignableFrom(srcType))
                return srcValue;

            if (srcValue is bool)
            {
                return ConvertLong((bool)srcValue ? 1 : 0, dstType);
            }
            else if (srcValue is sbyte ||
                srcValue is byte ||
                srcValue is short ||
                srcValue is ushort ||
                srcValue is char ||
                srcValue is int ||
                srcValue is uint ||
                srcValue is long)
            {
                long inter = (long)Convert.ChangeType(srcValue, typeof(long));
                return ConvertLong(inter, dstType);
            }
            else if (srcValue is ulong)
            {
                ulong inter = ToULong(srcValue);
                return ConvertULong(inter, dstType);
            }
            else if (srcValue is Signed)
            {
                return ConvertSigned((Signed)srcValue, dstType);
            }
            else if (srcValue is Unsigned)
            {
                return ConvertUnsigned((Unsigned)srcValue, dstType);
            }
            else if (srcValue is SFix)
            {
                return ConvertSFix((SFix)srcValue, dstType);
            }
            else if (srcValue is UFix)
            {
                return ConvertUFix((UFix)srcValue, dstType);
            }
            else if ((srcType.IsGenericType &&
                srcType.GetGenericTypeDefinition().Equals(typeof(TaskAwaiter<>)) &&
                dstType.Equals(typeof(TaskAwaiter))) ||
                (dstType.IsGenericType &&
                dstType.GetGenericTypeDefinition().Equals(typeof(TaskAwaiter<>)) &&
                srcType.Equals(typeof(TaskAwaiter))))
            {
                // Thanks, Microsoft... :-/

                var sourceAwaiterType = srcValue.GetType();
                var sourceAwaiterField = sourceAwaiterType.GetField("m_task", BindingFlags.Instance | BindingFlags.NonPublic);
                var task = sourceAwaiterField.GetValue(srcValue);
                var targetAwaiter = Activator.CreateInstance(dstType);
                var targetAwaiterField = dstType.GetField("m_task", BindingFlags.Instance | BindingFlags.NonPublic);
                targetAwaiterField.SetValue(targetAwaiter, task);
                return targetAwaiter;
            }
            else
            {
                return Convert.ChangeType(srcValue, dstType);
            }
        }

        /// <summary>
        /// Converts <paramref name="srcValue"/> to <paramref name="dstType"/> datatype, possibly with loss of precision or overflow.
        /// </summary>
        /// <remarks>Currently, conversions between all primitive numeric CIL types, enum types, and System#-intrinsic datatypes
        /// Signed, Unsigned, SFix, UFix and StdLogicVector are supported.</remarks>
        /// <exception cref="ArgumentNullException">if <paramref name="dstType"/> is null</exception>
        /// <exception cref="NotImplementedException">if there is no known conversion from type of <paramref name="srcValue"/> to <paramref name="dstType"/></exception>
        public static object ConvertValue(object srcValue, TypeDescriptor dstType)
        {
            Contract.Requires<ArgumentNullException>(dstType != null, "dstType");
            Contract.Requires<ArgumentNullException>(!dstType.CILType.IsValueType || srcValue != null, "srcValue");

            if (srcValue == null)
                return null;

            if (!dstType.IsConstrained)
                return ConvertValue(srcValue, dstType.CILType);

            if (TypeDescriptor.GetTypeOf(srcValue).Equals(dstType))
                return srcValue;

            if (srcValue is bool ||
                srcValue is sbyte ||
                srcValue is byte ||
                srcValue is short ||
                srcValue is ushort ||
                srcValue is char ||
                srcValue is int ||
                srcValue is uint ||
                srcValue is long)
            {
                long inter = ToLong(srcValue);
                return ConvertLong(inter, dstType);
            }
            else if (srcValue is ulong)
            {
                ulong inter = ToULong(srcValue);
                return ConvertULong(inter, dstType);
            }
            else if (srcValue is double)
            {
                return ConvertDouble((double)srcValue, dstType);
            }
            else if (srcValue is float)
            {
                return ConvertDouble((float)srcValue, dstType);
            }
            else if (srcValue is Signed)
            {
                return ConvertSigned((Signed)srcValue, dstType);
            }
            else if (srcValue is Unsigned)
            {
                return ConvertUnsigned((Unsigned)srcValue, dstType);
            }
            else if (srcValue is SFix)
            {
                return ConvertSFix((SFix)srcValue, dstType);
            }
            else if (srcValue is UFix)
            {
                return ConvertUFix((UFix)srcValue, dstType);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts each parameter of <paramref name="args"/> to the type expected by <paramref name="method"/> and
        /// invokes <paramref name="method"/> on <paramref name="instance"/>.
        /// </summary>
        /// <returns>method call return value, or null if void method</returns>
        /// <exception cref="ArgumentNullException">if method or args is null, if instance is required and null, 
        /// or if some method argument is required and null</exception>
        /// <exception cref="ArgumentOutOfRangeException">if parameter count inside args array does not match method parameter count</exception>
        /// <exception cref="NotImplementedException">if unknown/impossible conversion from any argument</exception>
        public static object ConvertArgumentsAndInvoke(this MethodBase method, object instance, params object[] args)
        {
            Contract.Requires<ArgumentNullException>(method != null, "method");
            Contract.Requires<ArgumentNullException>(method.IsConstructor || method.IsStatic || instance != null, "instance");
            Contract.Requires<ArgumentNullException>(args != null, "args");
            Contract.Requires<ArgumentOutOfRangeException>(args.Length == method.GetParameters().Length, 
                "length of args must match parameter count of method");

            object[] cargs = new object[args.Length];
            ParameterInfo[] pis = method.GetParameters();
            for (int i = 0; i < args.Length; i++)
            {
                cargs[i] = ConvertValue(args[i], pis[i].ParameterType);
            }
            object result;
            if (method is MethodInfo)
            {
                result = method.Invoke(instance, cargs);
            }
            else if (method is ConstructorInfo)
            {
                result = Activator.CreateInstance(method.DeclaringType, cargs);
            }
            else
            {
                throw new NotSupportedException();
            }
            Array.Copy(cargs, args, args.Length);
            return result;
        }

        public enum ETypeClass
        {
            Boolean,
            SignedIntegral,
            UnsignedIntegral,
            Float,
            Double,
            Enum,
            Other
        }

        private static void EnsureTypesMatch(object v1, object v2)
        {
            if (!v1.GetType().Equals(v2.GetType()))
                throw new ArgumentException("The types of the specified arguments do not match");
        }

        /// <summary>
        /// Adds <paramref name="v1"/> and <paramref name="v2"/> either by primitive addition or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveAdd(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            var r = (dynamic)v1 + (dynamic)v2;
            return r;
        }

        /// <summary>
        /// Computes bitwise "and" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveAnd(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            var r = (dynamic)v1 & (dynamic)v2;
            return r;
        }

        /// <summary>
        /// Computes bitwise "or" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveOr(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            var r = (dynamic)v1 | (dynamic)v2;
            return r;
        }

        /// <summary>
        /// Computes bitwise "xor" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveXor(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            var r = (dynamic)v1 ^ (dynamic)v2;
            return r;
        }

        /// <summary>
        /// Subtracts <paramref name="v1"/> and <paramref name="v2"/> either by primitive subtraction or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveSub(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            var r = (dynamic)v1 - (dynamic)v2;
            return r;
        }

        /// <summary>
        /// Multiplies <paramref name="v1"/> and <paramref name="v2"/> either by primitive multiplaction or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveMul(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 * (dynamic)v2;
        }

        /// <summary>
        /// Divides <paramref name="v1"/> and <paramref name="v2"/> either by primitive division or 
        /// by invoking an appropriate operator. Returns null if either operand is null. Returns 0 in the representation of
        /// <paramref name="v1"/>'s type in case of division by zero.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        /// <exception cref="NotImplementedException">if division by zero was detected, but 0 could not be converted the <paramref name="v1"/>'s type</exception>
        public static object PrimitiveDiv(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            try
            {
                return (dynamic)v1 / (dynamic)v2;
            }
            catch (DivideByZeroException)
            {
                return ConvertValue(0, v1.GetType());
            }
        }

        /// <summary>
        /// Computes the modulus of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null. Returns 0 in the representation of
        /// <paramref name="v1"/>'s type in case of division by zero.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        /// <exception cref="NotImplementedException">if division by zero was detected, but 0 could not be converted the <paramref name="v1"/>'s type</exception>
        public static object PrimitiveRem(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            try
            {
                return (dynamic)v1 % (dynamic)v2;
            }
            catch (DivideByZeroException)
            {
                return ConvertValue(0, v1.GetType());
            }
        }

        /// <summary>
        /// Computes "shift left" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveShl(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 << (dynamic)v2;
        }

        /// <summary>
        /// Computes "shift right" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveShr(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 >> (dynamic)v2;
        }

        /// <summary>
        /// Computes "less than" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveLessThan(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 < (dynamic)v2;
        }

        /// <summary>
        /// Computes "less than or equal" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveLessThanOrEqual(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 <= (dynamic)v2;
        }

        /// <summary>
        /// Computes "equality" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveEqual(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 == (dynamic)v2;
        }

        /// <summary>
        /// Computes "inequality" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveUnequal(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 != (dynamic)v2;
        }

        /// <summary>
        /// Computes "greater than or equal" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveGreaterThanOrEqual(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 >= (dynamic)v2;
        }

        /// <summary>
        /// Computes "greater than" of <paramref name="v1"/> and <paramref name="v2"/> either by primitive operation or 
        /// by invoking an appropriate operator. Returns null if either operand is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveGreaterThan(object v1, object v2)
        {
            if (v1 == null || v2 == null)
                return null;

            return (dynamic)v1 > (dynamic)v2;
        }

        /// <summary>
        /// Negates <paramref name="v"/> by primitive operation or 
        /// by invoking an appropriate operator. Returns null if <paramref name="v"/> is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveNeg(object v)
        {
            if (v == null)
                return null;

            return -(dynamic)v;
        }

        /// <summary>
        /// Computes "logical not" of <paramref name="v"/> by primitive operation or 
        /// by invoking an appropriate operator. Returns null if <paramref name="v"/> is null.
        /// </summary>
        /// <exception cref="RuntimeBinderException">if there is no operator implemented to perform the operation</exception>
        public static object PrimitiveNot(object v)
        {
            if (v == null)
                return null;

            return !(dynamic)v;
        }
    }
}

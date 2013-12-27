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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Synthesis.VHDLGen
{
    /// <summary>
    /// Provides information on a certain type which is necessary for VHDL code generation.
    /// </summary>
    public class TypeInfo
    {
        /// <summary>
        /// Type classification
        /// </summary>
        public enum ERangeSpec
        {
            /// <summary>
            /// The described type has a value range which is specified using the VHDL "range" keyword.
            /// </summary>
            ByRange,

            /// <summary>
            /// The described type is a single- or multi-dimensional array.
            /// </summary>
            BySize
        }

        /// <summary>
        /// VHDL name of the type.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Type classification.
        /// </summary>
        public ERangeSpec RangeSpec { get; private set; }

        /// <summary>
        /// Default value range of the type.
        /// </summary>
        public Range DefaultRange { get; private set; }

        /// <summary>
        /// Whether the default range should be rendered during code generation.
        /// </summary>
        public bool ShowDefaultRange { get; private set; }

        /// <summary>
        /// All libraries which are required for this type.
        /// </summary>
        public string[] Libraries { get; private set; }

        /// <summary>
        /// Whether the type of not synthesizable.
        /// </summary>
        public bool IsNotSynthesizable { get; private set; }

        private TypeInfo(string name)
        {
            Name = name;
            RangeSpec = ERangeSpec.BySize;
        }

        private TypeInfo(string name, Range defaultRange)
        {
            Name = name;
            RangeSpec = ERangeSpec.ByRange;
            DefaultRange = defaultRange;
            ShowDefaultRange = true;
        }

        private TypeInfo(string name, ERangeSpec rangeSpec)
        {
            Name = name;
            RangeSpec = rangeSpec;
        }

        private TypeInfo(string name, params string[] libs)
        {
            Name = name;
            RangeSpec = ERangeSpec.BySize;
            Libraries = libs;
        }

        private TypeInfo(string name, bool isNotSynthesizable, params string[] libs)
        {
            Name = name;
            RangeSpec = ERangeSpec.BySize;
            IsNotSynthesizable = isNotSynthesizable;
            Libraries = libs;
        }

        private TypeInfo(string name, bool isNotSynthesizable, ERangeSpec rangeSpec, Range defaultRange, params string[] libs)
        {
            Name = name;
            RangeSpec = rangeSpec;
            DefaultRange = defaultRange;
            ShowDefaultRange = true;
            IsNotSynthesizable = isNotSynthesizable;
            Libraries = libs;
        }

        private TypeInfo(string name, Range defaultRange, params string[] libs)
        {
            Name = name;
            RangeSpec = ERangeSpec.ByRange;
            DefaultRange = defaultRange;
            ShowDefaultRange = true;
            RangeSpec = ERangeSpec.BySize;
            Libraries = libs;
        }

        private TypeInfo(string name, ERangeSpec rangeSpec, params string[] libs)
        {
            Name = name;
            RangeSpec = rangeSpec;
            Libraries = libs;
        }

        private TypeInfo(string name, ERangeSpec rangeSpec, bool isNotSynthesizable, params string[] libs)
        {
            Name = name;
            RangeSpec = rangeSpec;
            IsNotSynthesizable = isNotSynthesizable;
            Libraries = libs;
        }

        protected string GetRangeSuffix(Range range)
        {
            string result = "(" + range.FirstBound + " ";
            switch (range.Direction)
            {
                case EDimDirection.Downto: result += "downto"; break;
                case EDimDirection.To: result += "to"; break;
                default: throw new NotImplementedException();
            }
            result += " " + range.SecondBound + ")";
            return result;
        }

        /// <summary>
        /// Creates a VHDL type specification for a complete type, i.e. including all indices.
        /// </summary>
        /// <param name="td">Type descriptor to declare. It is assumed that the described type matched the
        /// type which is described by this instance.</param>
        /// <returns>VHDL fragment for type declaration</returns>
        public virtual string DeclareCompletedType(TypeDescriptor td)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Name);
            if (td.Rank > 0)
            {
                if (td.Constraints != null)
                    sb.Append(GetRangeSuffix(td.Constraints[0]));
                else
                    sb.Append("(???)");
            }
            else if (RangeSpec == ERangeSpec.BySize && ShowDefaultRange)
            {
                sb.Append(GetRangeSuffix(DefaultRange));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Creates a type information instance for a single- or multi-dimensional type.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateSizedType(string name)
        {
            return new TypeInfo(name);
        }

        /// <summary>
        /// Creates a type information instance for a single- or multi-dimensional type.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <param name="libs">all libraries which are necessary to work with the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateSizedType(string name, params string[] libs)
        {
            return new TypeInfo(name, libs);
        }

        /// <summary>
        /// Creates a type information instance for a single- or multi-dimensional type.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <param name="isNotSynthesizable">whether the type is not synthesizable</param>
        /// <param name="libs">all libraries which are necessary to work with the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateSizedType(string name, bool isNotSynthesizable, params string[] libs)
        {
            return new TypeInfo(name, isNotSynthesizable, libs);
        }

        /// <summary>
        /// Creates a type information instance for a single- or multi-dimensional type.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <param name="isNotSynthesizable">whether the type is not synthesizable</param>
        /// <param name="defaultRange">default index range of the type</param>
        /// <param name="libs">all libraries which are necessary to work with the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateSizedType(string name, bool isNotSynthesizable, Range defaultRange, params string[] libs)
        {
            return new TypeInfo(name, isNotSynthesizable, ERangeSpec.BySize, defaultRange, libs);
        }

        /// <summary>
        /// Creates a type information for a type with specifiable value range.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateRangedType(string name)
        {
            return new TypeInfo(name, ERangeSpec.ByRange);
        }

        /// <summary>
        /// Creates a type information for a type with specifiable value range.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <param name="libs">all libraries which are necessary to work with the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateRangedType(string name, params string[] libs)
        {
            return new TypeInfo(name, ERangeSpec.ByRange, libs);
        }

        /// <summary>
        /// Creates a type information for a type with specifiable value range.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <param name="isNotSynthesizable">whether the type is not synthesizable</param>
        /// <param name="libs">all libraries which are necessary to work with the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateRangedType(string name, bool isNotSynthesizable, params string[] libs)
        {
            return new TypeInfo(name, ERangeSpec.ByRange, isNotSynthesizable, libs);
        }

        /// <summary>
        /// Creates a type information for a type with specifiable value range.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <param name="defaultRange">default value range of the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateRangedType(string name, Range defaultRange)
        {
            return new TypeInfo(name, defaultRange);
        }

        /// <summary>
        /// Creates a type information for a type with specifiable value range.
        /// </summary>
        /// <param name="name">VHDL name of the type</param>
        /// <param name="defaultRange">default value range of the type</param>
        /// <param name="libs">all libraries which are necessary to work with the type</param>
        /// <returns>the newly created type information</returns>
        public static TypeInfo CreateRangedType(string name, Range defaultRange, params string[] libs)
        {
            return new TypeInfo(name, defaultRange, libs);
        }
    }

    /// <summary>
    /// Provides information on the mapping from System# types to VHDL types and conversions between those types.
    /// </summary>
    public class VHDLTypes
    {
        class Conversion : Attribute
        {
            public Type SType { get; private set; }
            public Type TType { get; private set; }

            public Conversion(Type stype, Type ttype)
            {
                SType = stype;
                TType = ttype;
            }
        }

        #region ValueOf

        public static string ValueOf(bool value)
        {
            return value ? "true" : "false";
        }

        public static string ValueOf(sbyte value)
        {
            return value.ToString();
        }

        public static string ValueOf(byte value)
        {
            return value.ToString();
        }

        public static string ValueOf(short value)
        {
            return value.ToString();
        }

        public static string ValueOf(ushort value)
        {
            return value.ToString();
        }

        public static string ValueOf(int value)
        {
            return value.ToString();
        }

        public static string ValueOf(uint value)
        {
            return ValueOf(Unsigned.FromULong(value, 32));
        }

        public static string ValueOf(long value)
        {
            return ValueOf(Signed.FromLong(value, 64));
        }

        public static string ValueOf(ulong value)
        {
            return ValueOf(Unsigned.FromULong(value, 64));
        }

        public static string ValueOf(StdLogic value)
        {
            return "'" + value.ToString() + "'";
        }

        public static string ValueOf(StdLogicVector value)
        {
            if (value.Size == 0)
                return "(others => '0')";
            else
                return "std_logic_vector'(\"" + value.ToString() + "\")";
        }

        public static string ValueOf(Unsigned value)
        {
            return "unsigned(" + ValueOf(value.SLVValue) + ")";
        }

        public static string ValueOf(Signed value)
        {
            return "signed(" + ValueOf(value.SLVValue) + ")";
        }

        public static string ValueOf(SFix value)
        {
            return "to_sfixed(std_logic_vector'(\"" + value.SLVValue.ToString() + "\"), " + (value.Format.IntWidth - 1) + ", " + (-value.Format.FracWidth) + ")";
        }

        public static string ValueOf(UFix value)
        {
            return "to_ufixed(std_logic_vector'(\"" + value.SLVValue.ToString() + "\"), " + (value.Format.IntWidth - 1) + ", " + (-value.Format.FracWidth) + ")";
        }

        public static string ValueOf(string value)
        {
            return "string'(\"" + value + "\")";
        }

        public static string ValueOf(char value)
        {
            return "'" + value + "'";
        }

        public static string ValueOf(Time value)
        {
            // VHDL doesn't support decimal durations, i.e. 5.123 us is syntactically wrong.
            // Instead, we have to write it as 5123 ns. The following loop refines the time
            // unit step-by-step from from sec -> ms -> us -> ns -> ps -> fs until we can safely
            // truncate the fractional digits.
            while (value.Value != 0.0 &&
                Math.Abs(Math.Floor(value.Value) - value.Value) / value.Value > 5e-16)
            {
                switch (value.Unit)
                {
                    case ETimeUnit.fs:
                        break;
                    case ETimeUnit.ms:
                        value = new Time(value.ScaleTo(ETimeUnit.us), ETimeUnit.us);
                        continue;
                    case ETimeUnit.ns:
                        value = new Time(value.ScaleTo(ETimeUnit.ps), ETimeUnit.ps);
                        continue;
                    case ETimeUnit.ps:
                        value = new Time(value.ScaleTo(ETimeUnit.fs), ETimeUnit.fs);
                        continue;
                    case ETimeUnit.sec:
                        value = new Time(value.ScaleTo(ETimeUnit.ms), ETimeUnit.ms);
                        continue;
                    case ETimeUnit.us:
                        value = new Time(value.ScaleTo(ETimeUnit.ns), ETimeUnit.ns);
                        continue;
                    default:
                        throw new NotImplementedException();
                }
                break;
            }
            return ((long)value.Value).ToString() + value.Unit.ToString();
        }

        public static string ValueOf(float value)
        {
            return "to_float(" + value.ToString("0.0################E-##0", CultureInfo.InvariantCulture) + ", 8, 23)";
        }

        public static string ValueOf(double value)
        {
            return "to_float(" + value.ToString("0.0################E-##0", CultureInfo.InvariantCulture) + ", 11, 52)";
        }

        #endregion

        #region from bool

        [Conversion(typeof(bool), typeof(StdLogic))]
        public static string Convert_bool_StdLogic(string value, TypeDescriptor ttype)
        {
            return "to_std_logic(" + value + ")";
        }

        #endregion

        #region from sbyte

        [Conversion(typeof(sbyte), typeof(short))]
        public static string Convert_sbyte_short(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(sbyte), typeof(string))]
        public static string Convert_sbyte_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        [Conversion(typeof(sbyte), typeof(Signed))]
        public static string Convert_sbyte_Signed(string value, TypeDescriptor ttype)
        {
            return Convert_int_Signed(value, "8", ttype);
        }

        [Conversion(typeof(sbyte), typeof(float))]
        public static string Convert_sbyte_float(string value, TypeDescriptor ttype)
        {
            return Convert_int_float(value, ttype);
        }

        [Conversion(typeof(sbyte), typeof(double))]
        public static string Convert_sbyte_double(string value, TypeDescriptor ttype)
        {
            return Convert_int_double(value, ttype);
        }

        #endregion

        #region from byte

        [Conversion(typeof(byte), typeof(short))]
        public static string Convert_byte_short(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(byte), typeof(ushort))]
        public static string Convert_byte_ushort(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(byte), typeof(string))]
        public static string Convert_byte_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        [Conversion(typeof(byte), typeof(Signed))]
        public static string Convert_byte_Unsigned(string value, TypeDescriptor ttype)
        {
            return Convert_ushort_Unsigned(value, "8", ttype);
        }

        [Conversion(typeof(byte), typeof(Signed))]
        public static string Convert_byte_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return Convert_int_Signed(value, width, ttype);
        }

        [Conversion(typeof(byte), typeof(float))]
        public static string Convert_byte_float(string value, TypeDescriptor ttype)
        {
            return Convert_int_float(value, ttype);
        }

        [Conversion(typeof(byte), typeof(double))]
        public static string Convert_byte_double(string value, TypeDescriptor ttype)
        {
            return Convert_int_double(value, ttype);
        }

        #endregion

        #region from short

        [Conversion(typeof(short), typeof(int))]
        public static string Convert_short_int(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(short), typeof(string))]
        public static string Convert_short_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        [Conversion(typeof(short), typeof(Signed))]
        public static string Convert_short_Signed(string value, TypeDescriptor ttype)
        {
            return Convert_int_Signed(value, "16", ttype);
        }

        [Conversion(typeof(short), typeof(Signed))]
        public static string Convert_short_Signed(string value, string width, TypeDescriptor ttype)
        {
            return Convert_int_Signed(value, width, ttype);
        }

        [Conversion(typeof(short), typeof(float))]
        public static string Convert_short_float(string value, TypeDescriptor ttype)
        {
            return Convert_int_float(value, ttype);
        }

        [Conversion(typeof(short), typeof(double))]
        public static string Convert_short_double(string value, TypeDescriptor ttype)
        {
            return Convert_int_double(value, ttype);
        }

        #endregion

        #region from ushort

        [Conversion(typeof(ushort), typeof(int))]
        public static string Convert_ushort_int(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(ushort), typeof(string))]
        public static string Convert_ushort_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        [Conversion(typeof(ushort), typeof(Unsigned))]
        public static string Convert_ushort_Unsigned(string value, TypeDescriptor ttype)
        {
            return Convert_ushort_Unsigned(value, "16", ttype);
        }

        [Conversion(typeof(ushort), typeof(Unsigned))]
        public static string Convert_ushort_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return "to_unsigned(" + value + ", " + width + ")";
        }

        [Conversion(typeof(ushort), typeof(float))]
        public static string Convert_ushort_float(string value, TypeDescriptor ttype)
        {
            return Convert_int_float(value, ttype);
        }

        [Conversion(typeof(ushort), typeof(double))]
        public static string Convert_ushort_double(string value, TypeDescriptor ttype)
        {
            return Convert_int_double(value, ttype);
        }

        #endregion

        #region from int

        [Conversion(typeof(int), typeof(string))]
        public static string Convert_int_string(string value, TypeDescriptor ttype)
        {
            return "image(" + value + ")";
        }

        [Conversion(typeof(int), typeof(Signed))]
        public static string Convert_int_Signed(string value, TypeDescriptor ttype)
        {
            return Convert_int_Signed(value, "32", ttype);
        }

        [Conversion(typeof(int), typeof(Signed))]
        public static string Convert_int_Signed(string value, string width, TypeDescriptor ttype)
        {
            return "to_signed(" + value + ", " + width + ")";
        }

        [Conversion(typeof(int), typeof(Unsigned))]
        public static string Convert_int_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return "to_unsigned(" + value + ", " + width + ")";
        }

        [Conversion(typeof(int), typeof(uint))]
        public static string Convert_int_uint(string value, TypeDescriptor ttype)
        {
            return "to_unsigned(" + value + ", 32)";
        }
        [Conversion(typeof(int), typeof(float))]
        public static string Convert_int_float(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 8, 23)";
        }

        [Conversion(typeof(int), typeof(double))]
        public static string Convert_int_double(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 11, 52)";
        }

        #endregion

        #region from uint

        [Conversion(typeof(uint), typeof(int))]
        public static string Convert_uint_int(string value, TypeDescriptor ttype)
        {
            return Convert_Unsigned_int(value, ttype);
        }

        [Conversion(typeof(uint), typeof(ulong))]
        public static string Convert_uint_ulong(string value, TypeDescriptor ttype)
        {
            return Resize(value, "64");
        }

        [Conversion(typeof(uint), typeof(Unsigned))]
        public static string Convert_uint_Unsigned(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(uint), typeof(Unsigned))]
        public static string Convert_uint_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return Resize(value, width);
        }

        [Conversion(typeof(uint), typeof(StdLogicVector))]
        public static string Convert_uint_SLV(string value, string width, TypeDescriptor ttype)
        {
            return ResizeSLV("std_logic_vector(" + value + ")", width);
        }

        #endregion

        #region from long

        [Conversion(typeof(long), typeof(string))]
        public static string Convert_long_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        [Conversion(typeof(long), typeof(Signed))]
        public static string Convert_long_Signed(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(long), typeof(Signed))]
        public static string Convert_long_Signed(string value, string width, TypeDescriptor ttype)
        {
            return Resize(value, width);
        }

        [Conversion(typeof(long), typeof(StdLogicVector))]
        public static string Convert_long_SLV(string value, string width, TypeDescriptor ttype)
        {
            return ResizeSLV("std_logic_vector(" + value + ")", width);
        }

        [Conversion(typeof(long), typeof(StdLogicVector))]
        public static string Convert_long_SLV(string value, TypeDescriptor ttype)
        {
            return "std_logic_vector(" + value + ")";
        }

        #endregion

        #region from ulong

        [Conversion(typeof(ulong), typeof(string))]
        public static string Convert_ulong_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        [Conversion(typeof(ulong), typeof(Unsigned))]
        public static string Convert_ulong_Unsigned(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(ulong), typeof(Unsigned))]
        public static string Convert_ulong_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return Resize(value, width);
        }

        [Conversion(typeof(ulong), typeof(StdLogicVector))]
        public static string Convert_ulong_SLV(string value, string width, TypeDescriptor ttype)
        {
            return ResizeSLV("std_logic_vector(" + value + ")", width);
        }

        [Conversion(typeof(ulong), typeof(StdLogicVector))]
        public static string Convert_ulong_SLV(string value, TypeDescriptor ttype)
        {
            return "std_logic_vector(" + value + ")";
        }

        #endregion

        #region from char

        [Conversion(typeof(char), typeof(StdLogic))]
        public static string Convert_char_StdLogic(string value, TypeDescriptor ttype)
        {
            return "'" + value + "'";
        }

        #endregion

        #region from string


        //[Conversion(typeof(string), typeof(StdLogicVector))]
        //public static string Convert_string_SLV(string value)
        //{
        //    return value;
        //}


        #endregion

        #region from StdLogic

        [Conversion(typeof(StdLogic), typeof(string))]
        public static string Convert_SL_string(string value, TypeDescriptor ttype)
        {
            return "image(" + value + ")";
        }

        [Conversion(typeof(StdLogic), typeof(bool))]
        public static string Convert_SL_bool(string value, TypeDescriptor ttype)
        {
            return "((" + value + ") = '1')";
        }

        [Conversion(typeof(StdLogic), typeof(StdLogicVector))]
        public static string Convert_SL_SLV(string value, TypeDescriptor ttype)
        {

            return "to_std_logic_vector(" + value + ")";
        }

        #endregion

        #region from StdLogic[]

        [Conversion(typeof(StdLogic[]), typeof(StdLogicVector))]
        public static string Convert_aSL_SLV(string value, TypeDescriptor ttype)
        {
            return "(" + value + ")";
        }

        #endregion

        #region from StdLogicVector

        [Conversion(typeof(StdLogicVector), typeof(string))]
        public static string Convert_SLV_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        [Conversion(typeof(StdLogicVector), typeof(Signed))]
        public static string Convert_SLV_Signed(string value, TypeDescriptor ttype)
        {
            return "signed(" + value + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(Unsigned))]
        public static string Convert_SLV_Unsigned(string value, TypeDescriptor ttype)
        {
            return "unsigned(" + value + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(SFix))]
        public static string Convert_SLV_SFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            return "to_sfixed(" + value + ", " + intWidth + "-1, " + frac + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(SFix))]
        public static string Convert_SLV_SFix(string value, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            FixFormat fmt = SFix.GetFormat(ttype);
            return "to_sfixed(" + value + ", " + (fmt.IntWidth - 1) + ", " + frac + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(SFix))]
        public static string Convert_SLV_SFix(string value, TypeDescriptor ttype)
        {
            FixFormat fmt = SFix.GetFormat(ttype);
            return "to_sfixed(" + value + ", " + (fmt.IntWidth - 1) + ", " + (-fmt.FracWidth) + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(UFix))]
        public static string Convert_SLV_UFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            FixFormat fmt = UFix.GetFormat(ttype);
            return "to_ufixed(" + value + ", " + intWidth + "-1, " + frac + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(UFix))]
        public static string Convert_SLV_UFix(string value, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            FixFormat fmt = UFix.GetFormat(ttype);
            return "to_ufixed(" + value + ", " + (fmt.IntWidth - 1) + ", " + frac + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(UFix))]
        public static string Convert_SLV_UFix(string value, TypeDescriptor ttype)
        {
            FixFormat fmt = UFix.GetFormat(ttype);
            return "to_ufixed(" + value + ", " + (fmt.IntWidth - 1) + ", -" + fmt.FracWidth + ")";
        }

        [Conversion(typeof(StdLogicVector), typeof(float))]
        public static string Convert_SLV_float(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 8, 23)";
        }

        [Conversion(typeof(StdLogicVector), typeof(double))]
        public static string Convert_SLV_double(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 11, 52)";
        }

        [Conversion(typeof(StdLogicVector), typeof(StdLogic))]
        public static string Convert_SLV_SL(string value, TypeDescriptor ttype)
        {
            return value + "(0)";
        }

        #endregion

        #region from Signed

        [Conversion(typeof(Signed), typeof(sbyte))]
        public static string Convert_Signed_sbyte(string value, TypeDescriptor ttype)
        {
            return Convert_Signed_int(value, ttype);
        }

        [Conversion(typeof(Signed), typeof(byte))]
        public static string Convert_Signed_byte(string value, TypeDescriptor ttype)
        {
            return Convert_Signed_int(value, ttype);
        }

        [Conversion(typeof(Signed), typeof(short))]
        public static string Convert_Signed_short(string value, TypeDescriptor ttype)
        {
            return Convert_Signed_int(value, ttype);
        }

        [Conversion(typeof(Signed), typeof(ushort))]
        public static string Convert_Signed_ushort(string value, TypeDescriptor ttype)
        {
            return Convert_Signed_int(value, ttype);
        }

        [Conversion(typeof(Signed), typeof(int))]
        public static string Convert_Signed_int(string value, TypeDescriptor ttype)
        {
            return "to_integer(" + value + ")";
        }

        [Conversion(typeof(Signed), typeof(long))]
        public static string Convert_Signed_long(string value, TypeDescriptor ttype)
        {
            return Resize(value, "64");
        }

        [Conversion(typeof(Signed), typeof(StdLogicVector))]
        public static string Convert_Signed_SLV(string value, TypeDescriptor ttype)
        {
            return "std_logic_vector(" + value + ")";
        }

        [Conversion(typeof(Signed), typeof(float))]
        public static string Convert_Signed_float(string value, TypeDescriptor ttype)
        {
            return Convert_int_float(value, ttype);
        }

        [Conversion(typeof(Signed), typeof(double))]
        public static string Convert_Signed_double(string value, TypeDescriptor ttype)
        {
            return Convert_int_double(value, ttype);
        }

        #endregion

        #region from Unsigned

        [Conversion(typeof(Unsigned), typeof(int))]
        public static string Convert_Unsigned_int(string value, TypeDescriptor ttype)
        {
            return "to_integer(" + value + ")";
        }

        [Conversion(typeof(Unsigned), typeof(uint))]
        public static string Convert_Unsigned_uint(string value, TypeDescriptor ttype)
        {
            return Resize(value, "32");
        }

        [Conversion(typeof(Unsigned), typeof(ulong))]
        public static string Convert_Unsigned_ulong(string value, TypeDescriptor ttype)
        {
            return Resize(value, "64");
        }

        [Conversion(typeof(Unsigned), typeof(StdLogicVector))]
        public static string Convert_Unsigned_SLV(string value, TypeDescriptor ttype)
        {
            return "std_logic_vector(" + value + ")";
        }

        [Conversion(typeof(Unsigned), typeof(float))]
        public static string Convert_Unsigned_float(string value, TypeDescriptor ttype)
        {
            return Convert_int_float(value, ttype);
        }

        [Conversion(typeof(Unsigned), typeof(double))]
        public static string Convert_Unsigned_double(string value, TypeDescriptor ttype)
        {
            return Convert_int_double(value, ttype);
        }

        #endregion

        #region from Time

        [Conversion(typeof(Time), typeof(string))]
        public static string Convert_Time_string(string value, TypeDescriptor ttype)
        {
            return Convert_int_string(value, ttype);
        }

        #endregion

        #region from float/double

        [Conversion(typeof(float), typeof(string))]
        public static string Convert_float_string(string value, TypeDescriptor ttype)
        {
            return "image(to_real(" + value + "))";
        }

        [Conversion(typeof(double), typeof(string))]
        public static string Convert_double_string(string value, TypeDescriptor ttype)
        {
            return "image(to_real(" + value + "))";
        }

        [Conversion(typeof(float), typeof(SFix))]
        public static string Convert_float_SFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            return "to_sfixed(" + value + ", " + intWidth + "-1, " + frac + ")";
        }

        [Conversion(typeof(double), typeof(SFix))]
        public static string Convert_double_SFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            return "to_sfixed(" + value + ", " + intWidth + "-1, " + frac + ")";
        }

        [Conversion(typeof(float), typeof(StdLogicVector))]
        public static string Convert_float_SLV(string value, TypeDescriptor ttype)
        {
            return "to_slv(" + value + ")";
        }

        [Conversion(typeof(float), typeof(float))]
        public static string Convert_float_SFix(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(float), typeof(UFix))]
        public static string Convert_float_UFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            return "to_ufixed(" + value + ", " + intWidth + "-1, " + frac + ")";
        }

        [Conversion(typeof(double), typeof(UFix))]
        public static string Convert_double_UFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            string frac = fracWidth.StartsWith("-") ? fracWidth.Substring(1) : "-" + fracWidth;
            return "to_ufixed(" + value + ", " + intWidth + "-1, " + frac + ")";
        }

        [Conversion(typeof(double), typeof(StdLogicVector))]
        public static string Convert_double_SLV(string value, TypeDescriptor ttype)
        {
            return "to_slv(" + value + ")";
        }

        [Conversion(typeof(double), typeof(double))]
        public static string Convert_double_double(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(double), typeof(float))]
        public static string Convert_double_float(string value, TypeDescriptor ttype)
        {
            return "resize(" + value + ", 8, 23)";
        }

        [Conversion(typeof(float), typeof(double))]
        public static string Convert_float_double(string value, TypeDescriptor ttype)
        {
            return "resize(" + value + ", 11, 52)";
        }

        #endregion

        #region from SFix

        [Conversion(typeof(SFix), typeof(float))]
        public static string Convert_SFix_float(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 8, 23)";
        }

        [Conversion(typeof(SFix), typeof(double))]
        public static string Convert_SFix_double(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 11, 52)";
        }

        [Conversion(typeof(SFix), typeof(StdLogicVector))]
        public static string Convert_SFix_slv(string value, TypeDescriptor ttype)
        {
            return "to_slv(" + value + ")";
        }

        [Conversion(typeof(SFix), typeof(UFix))]
        public static string Convert_SFix_UFix(string value, TypeDescriptor ttype)
        {
            return Convert_SLV_UFix("to_slv(" + value + ")", ttype);
        }

        #endregion

        #region from UFix

        [Conversion(typeof(UFix), typeof(float))]
        public static string Convert_UFix_float(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 8, 23)";
        }

        [Conversion(typeof(UFix), typeof(double))]
        public static string Convert_UFix_double(string value, TypeDescriptor ttype)
        {
            return "to_float(" + value + ", 11, 52)";
        }

        [Conversion(typeof(UFix), typeof(StdLogicVector))]
        public static string Convert_UFix_slv(string value, TypeDescriptor ttype)
        {
            return "to_slv(" + value + ")";
        }

        [Conversion(typeof(UFix), typeof(SFix))]
        public static string Convert_UFix_SFix(string value, TypeDescriptor ttype)
        {
            var fmt = ttype.GetFixFormat();
            return Convert_SLV_SFix("to_slv(" + value + ")", ttype);
        }

        #endregion

        private static string Resize(string value, string width)
        {
            return "resize(" + value + ", " + width + ")";
        }

        /*private static string Resize(string value, string intWidth, string fracWidth)
        {
            return "resize(" + value + ", " + intWidth + "-1, -" + fracWidth + ")";
        }*/

        private static string ResizeSLV(string value, string width)
        {
            return "std_logic_vector(" + Resize("unsigned(" + value + ")", width) + ")";
        }

        private static Dictionary<Type, Dictionary<Type, List<MethodInfo>>> _converters =
            new Dictionary<Type, Dictionary<Type, List<MethodInfo>>>();

        private static Dictionary<Type, TypeInfo> _typeInfos =
            new Dictionary<Type, TypeInfo>();

        private static void InitConverters()
        {
            var convms = typeof(VHDLTypes).GetMethods().Where(mi => mi.Name.StartsWith("Convert"));
            foreach (MethodInfo convm in convms)
            {
                Conversion conva = (Conversion)Attribute.GetCustomAttribute(convm, typeof(Conversion));
                if (conva == null)
                    continue;
                Dictionary<Type, List<MethodInfo>> map;
                if (!_converters.TryGetValue(conva.SType, out map))
                {
                    map = new Dictionary<Type, List<MethodInfo>>();
                    _converters[conva.SType] = map;
                }
                List<MethodInfo> convs;
                if (!map.TryGetValue(conva.TType, out convs))
                {
                    convs = new List<MethodInfo>();
                    map[conva.TType] = convs;
                }
                convs.Add(convm);
            }
        }

        private static void RegisterType(Type type, TypeInfo ti)
        {
            _typeInfos[type] = ti;
        }

        private static void RegisterTypes()
        {
            RegisterType(typeof(bool), TypeInfo.CreateRangedType("boolean", "work", "synth_pkg"));
            RegisterType(typeof(sbyte), TypeInfo.CreateRangedType("integer", Range.Upto(sbyte.MinValue, sbyte.MaxValue)));
            RegisterType(typeof(byte), TypeInfo.CreateRangedType("integer", Range.Upto(byte.MinValue, byte.MaxValue)));
            RegisterType(typeof(short), TypeInfo.CreateRangedType("integer", Range.Upto(short.MinValue, short.MaxValue)));
            RegisterType(typeof(ushort), TypeInfo.CreateRangedType("integer", Range.Upto(ushort.MinValue, ushort.MaxValue)));
            RegisterType(typeof(int), TypeInfo.CreateRangedType("integer"));
            RegisterType(typeof(uint), TypeInfo.CreateSizedType("unsigned", false, Range.Downto(31, 0), "ieee", "numeric_std"));
            RegisterType(typeof(long), TypeInfo.CreateSizedType("signed", false, Range.Downto(63, 0), "ieee", "numeric_std"));
            RegisterType(typeof(ulong), TypeInfo.CreateSizedType("unsigned", false, Range.Downto(63, 0), "ieee", "numeric_std"));
            RegisterType(typeof(char), TypeInfo.CreateRangedType("character"));
            RegisterType(typeof(string), TypeInfo.CreateSizedType("string", true, "work", "image_pkg"));
            RegisterType(typeof(StdLogic), TypeInfo.CreateSizedType("std_logic", "ieee", "std_logic_1164", "work", "synth_pkg"));
            RegisterType(typeof(StdLogicVector), TypeInfo.CreateSizedType("std_logic_vector", "ieee", "std_logic_1164", "ieee", "std_logic_unsigned"));

            //RegisterType(typeof(Signed), TypeInfo.CreateSizedType("sfixed", "ieee", "numeric_std", "ieee_proposed", "fixed_pkg"));
            //RegisterType(typeof(Unsigned), TypeInfo.CreateSizedType("ufixed", "ieee", "numeric_std", "ieee_proposed", "fixed_pkg"));
            RegisterType(typeof(Signed), TypeInfo.CreateSizedType("signed", "ieee", "numeric_std"));
            RegisterType(typeof(Unsigned), TypeInfo.CreateSizedType("unsigned", "ieee", "numeric_std"));
            RegisterType(typeof(SFix), TypeInfo.CreateSizedType("sfixed", "ieee", "numeric_std", "ieee", "std_logic_1164", "ieee_proposed", "fixed_pkg"));
            RegisterType(typeof(UFix), TypeInfo.CreateSizedType("ufixed", "ieee", "numeric_std", "ieee", "std_logic_1164", "ieee_proposed", "fixed_pkg"));
            //RegisterType(typeof(float), TypeInfo.CreateRangedType("float32", true, "ieee", "math_real", "ieee_proposed", "float_pkg", "work", "sim_pkg"));
            //RegisterType(typeof(double), TypeInfo.CreateRangedType("float64", true, "ieee", "math_real", "ieee_proposed", "float_pkg", "work", "sim_pkg"));
            RegisterType(typeof(float), TypeInfo.CreateRangedType("float32", "ieee", "math_real", "ieee_proposed", "float_pkg"/*, "work", "sim_pkg"*/));
            RegisterType(typeof(double), TypeInfo.CreateRangedType("float64", "ieee", "math_real", "ieee_proposed", "float_pkg"/*, "work", "sim_pkg"*/));

            RegisterType(typeof(Time), TypeInfo.CreateRangedType("time"));

            RegisterType(typeof(StreamWriter), TypeInfo.CreateRangedType("TEXT", "std", "textio"));
            RegisterType(typeof(StreamReader), TypeInfo.CreateRangedType("TEXT", "std", "textio"));
        }

        static VHDLTypes()
        {
            InitConverters();
            RegisterTypes();
        }

        private class ConvInfo
        {
            public Type SrcType { get; private set; }
            public Type TgtType { get; private set; }
            public MethodInfo ConvMethod { get; private set; }

            public ConvInfo(Type srcType, Type tgtType, MethodInfo convMethod)
            {
                SrcType = srcType;
                TgtType = tgtType;
                ConvMethod = convMethod;
            }
        }

        private class SuccRel : IPropMap<Type, Type[]>
        {
            public Type[] this[Type elem]
            {
                get
                {
                    Dictionary<Type, List<MethodInfo>> map;
                    if (!_converters.TryGetValue(elem, out map))
                        return new Type[0];
                    else
                        return map.Keys.ToArray();
                }
                set { throw new InvalidOperationException(); }
            }

            public EAccess Access
            {
                get { return EAccess.ReadOnly; }
            }
        }

        private class DistRel : IPropMap<Tuple<Type, Type>, int>
        {
            public int this[Tuple<Type, Type> elem]
            {
                get { return 1; }
                set { throw new InvalidOperationException(); }
            }

            public EAccess Access
            {
                get { return EAccess.ReadOnly; }
            }
        }

        private static SuccRel _succRel = new SuccRel();
        private static DistRel _distRel = new DistRel();

        private class TypePaths
        {
            public Type RootType { get; private set; }
            public HashBasedPropMap<Type, Type> Preds { get; private set; }
            public HashBasedPropMap<Type, int> RootDist { get; private set; }

            public TypePaths(Type rootType)
            {
                RootType = rootType;
                Preds = new HashBasedPropMap<Type, Type>();
                RootDist = new HashBasedPropMap<Type, int>();
            }
        }

        private CacheDictionary<Type, TypePaths> _typePaths;
        private IEnumerable<Type> _allTypes;

        private TypePaths CreateTypePaths(Type type)
        {
            TypePaths paths = new TypePaths(type);
            Dijkstra.FindShortestPaths(_allTypes, type, _succRel, paths.RootDist, paths.Preds, _distRel);
            return paths;
        }

        private VHDLTypes()
        {
            _allTypes = _converters.Keys.Union(_converters.SelectMany(kvp => kvp.Value.Keys)).Distinct();
            _typePaths = new CacheDictionary<Type, TypePaths>(CreateTypePaths);
        }

        private static readonly VHDLTypes _instance = new VHDLTypes();

        private static string ConvertDFS(Type SType, Type TType, Stack<Dictionary<Type, List<MethodInfo>>> visited, params string[] args)
        {
            Dictionary<Type, List<MethodInfo>> map, tmap;
            if (!_converters.TryGetValue(SType, out map))
                return null;

            List<MethodInfo> convms;
            if (map.TryGetValue(TType, out convms))
            {
                MethodInfo convm = convms.Where(mi => mi.GetParameters().Length == args.Length).SingleOrDefault();
                if (convm != null)
                    return (string)convm.Invoke(null, args.Cast<object>().ToArray());
            }
            visited.Push(map);
            string full = null;
            foreach (var kvp in map)
            {
                Type ttype = kvp.Key;
                if (!_converters.TryGetValue(ttype, out tmap))
                    continue;
                if (visited.Contains(tmap))
                    continue;

                convms = kvp.Value;
                MethodInfo convm = convms.Where(mi => mi.GetParameters().Length == 1).SingleOrDefault();
                if (convm == null)
                    continue;
                string temp = (string)convm.Invoke(null, new object[] { args[0] });
                string[] targs = (string[])args.Clone();
                targs[0] = temp;
                full = ConvertDFS(ttype, TType, visited, targs);
                if (full != null)
                {
                    break;
                }
            }
            visited.Pop();
            return full;
        }
        
        /// <summary>
        /// Computes VHDL code to convert from a source type to a destination type.
        /// </summary>
        /// <param name="SType">source type</param>
        /// <param name="TTypeD">destination type descriptor</param>
        /// <param name="interTypes">list to gather possible intermediate conversions</param>
        /// <param name="args">conversion arguments</param>
        /// <returns>>VHDL code to convert from the source type to the destination type</returns>
        public static string Convert(Type SType, TypeDescriptor TTypeD, IList<Type> interTypes, params string[] args)
        {
            var TType = TTypeD.CILType;

            if (SType.Equals(TType))
                return args[0];

            TypePaths paths = _instance._typePaths[SType];
            if (paths.Preds[TType] == null)
                throw new NotSupportedException("Don't know any conversion from " + SType.Name + " to " + TType.Name);

            Type curT = TType;
            Stack<MethodInfo> convChain = new Stack<MethodInfo>();
            int nargs = args.Length;
            while (curT != SType)
            {
                interTypes.Add(curT);
                Type preT = paths.Preds[curT];
                List<MethodInfo> cands = _converters[preT][curT];
                MethodInfo convm = cands.Where(m => m.GetParameters().Length == nargs + 1).FirstOrDefault();
                if (convm != null)
                {
                    convChain.Push(convm);
                    nargs = 1;
                }
                else
                {
                    convm = cands.Where(m => m.GetParameters().Length == 2).FirstOrDefault();
                    if (convm == null)
                        throw new NotSupportedException("Don't know any conversion from " + SType.Name + " to " + TType.Name + " given the supplied parameters");
                    convChain.Push(convm);
                }
                curT = preT;
            }
            string[] curArgs = (string[])args.Clone();
            foreach (MethodInfo convm in convChain)
            {
                if (convm.GetParameters().Length == 2)
                {
                    curArgs[0] = (string)convm.Invoke(null, new object[] { curArgs[0], TTypeD });
                }
                else
                {
                    curArgs[0] = (string)convm.Invoke(null,
                        curArgs.Cast<object>().Concat(Enumerable.Repeat(TTypeD, 1)).ToArray());
                }
            }
            return curArgs[0];
        }

        /// <summary>
        /// Returns the VHDL representation of a constant of a certain type.
        /// </summary>
        /// <param name="value">constant to be represented in VHDL</param>
        /// <param name="svalue">out parameter to receive the VHDL representation</param>
        /// <returns><c>true</c> if a textual representation of the specified value could be found,
        /// <c>false</c> if not.</returns>
        public static bool GetValueOf(object value, out string svalue)
        {
            Type vtype = value.GetType();
            MethodInfo valueof = typeof(VHDLTypes).GetMethod("ValueOf", new Type[] { vtype });
            svalue = null;
            if (valueof == null)
                return false;
            svalue = (string)valueof.Invoke(null, new object[] { value });
            return true;
        }

        /// <summary>
        /// Looks for a certain type.
        /// </summary>
        /// <param name="type">type to lookup</param>
        /// <param name="ti">out parameter to receive the found type information</param>
        /// <returns><c>true</c> if type information could be found, <c>false</c> if not</returns>
        public static bool LookupType(Type type, out TypeInfo ti)
        {
            return _typeInfos.TryGetValue(type, out ti);
        }
    }
}

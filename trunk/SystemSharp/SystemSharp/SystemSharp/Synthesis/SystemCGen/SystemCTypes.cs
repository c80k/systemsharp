
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

namespace SystemSharp.Synthesis.SystemCGen
{
    public class TypeInfo
    {
        public enum ERangeSpec
        {
            ByRange,
            BySize
        }

        public string Name { get; private set; }
        public ERangeSpec RangeSpec { get; private set; }
        public Range DefaultRange { get; private set; }
        public bool ShowDefaultRange { get; private set; }
        public string[] Libraries { get; private set; }
        public bool IsNotSynthesizable { get; private set; }

        public TypeInfo(string name)
        {
            Name = name;
            RangeSpec = ERangeSpec.BySize;
        }

        public TypeInfo(string name, Range defaultRange)
        {
            Name = name;
            RangeSpec = ERangeSpec.ByRange;
            DefaultRange = defaultRange;
            ShowDefaultRange = true;
        }

        public TypeInfo(string name, ERangeSpec rangeSpec)
        {
            Name = name;
            RangeSpec = rangeSpec;
        }

        public TypeInfo(string name, params string[] libs)
        {
            Name = name;
            RangeSpec = ERangeSpec.BySize;
            Libraries = libs;
        }

        public TypeInfo(string name, bool isNotSynthesizable, params string[] libs)
        {
            Name = name;
            RangeSpec = ERangeSpec.BySize;
            IsNotSynthesizable = isNotSynthesizable;
            Libraries = libs;
        }

        public TypeInfo(string name, bool isNotSynthesizable, ERangeSpec rangeSpec, Range defaultRange, params string[] libs)
        {
            Name = name;
            RangeSpec = rangeSpec;
            DefaultRange = defaultRange;
            ShowDefaultRange = true;
            IsNotSynthesizable = isNotSynthesizable;
            Libraries = libs;
        }

        public TypeInfo(string name, Range defaultRange, params string[] libs)
        {
            Name = name;
            RangeSpec = ERangeSpec.ByRange;
            DefaultRange = defaultRange;
            ShowDefaultRange = true;
            RangeSpec = ERangeSpec.BySize;
            Libraries = libs;
        }

        public TypeInfo(string name, ERangeSpec rangeSpec, params string[] libs)
        {
            Name = name;
            RangeSpec = rangeSpec;
            Libraries = libs;
        }

        public TypeInfo(string name, ERangeSpec rangeSpec, bool isNotSynthesizable, params string[] libs)
        {
            Name = name;
            RangeSpec = rangeSpec;
            IsNotSynthesizable = isNotSynthesizable;
            Libraries = libs;
        }
                
        protected string GetRangeSuffix(Range range)
        {
            string result = "[" + range.Size + "]";            
            return result;
        }

        public virtual string DeclareCompletedType(TypeDescriptor td)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Name);

            if (td.Rank > 0)
            {
                if (td.Constraints != null)
                {
                    if (td.CILType.Equals(typeof(SFix)) || td.CILType.Equals(typeof(UFix)))
                        sb.Append("<" + td.GetFixFormat().TotalWidth + ", " + td.GetFixFormat().IntWidth + ">");
                    else if (td.CILType.Name.Equals("StdLogicVector")  || td.CILType.Equals(typeof(Signed)) || td.CILType.Equals(typeof(Unsigned)))
                        sb.Append("<" + td.Constraints[0].Size + ">");
                    else if (td.CILType.Name.Equals("StdLogicVector[]"))
                    {
                        long dim1 = td.Constraints[0].Size;
                        string dim2 = td.Element0Type.Index.Indices[0].ToString();
                        string[] aux = dim2.Split(' ');
                        int a = Convert.ToInt32(aux[0]);
                        int b = Convert.ToInt32(aux[2]); 
                        sb.Append("<" + (Math.Abs(a-b)+1) + ">");
                    }

                    //else
                    //    sb.Append(GetRangeSuffix(td.Constraints[0]));
                }
                else
                    sb.Append("(???)");
            }
            else if (RangeSpec == ERangeSpec.BySize && ShowDefaultRange)
            {
                sb.Append(GetRangeSuffix(DefaultRange));
            }
            return sb.ToString();
        }

        public static TypeInfo CreateSizedType(string name)
        {
            return new TypeInfo(name);
        }

        public static TypeInfo CreateSizedType(string name, params string[] libs)
        {
            return new TypeInfo(name, libs);
        }

        public static TypeInfo CreateSizedType(string name, bool isNotSynthesizable, params string[] libs)
        {
            return new TypeInfo(name, isNotSynthesizable, libs);
        }

        public static TypeInfo CreateSizedType(string name, bool isNotSynthesizable, Range defaultRange, params string[] libs)
        {
            return new TypeInfo(name, isNotSynthesizable, ERangeSpec.BySize, defaultRange, libs);
        }

        public static TypeInfo CreateRangedType(string name)
        {
            return new TypeInfo(name, ERangeSpec.ByRange);
        }

        public static TypeInfo CreateRangedType(string name, params string[] libs)
        {
            return new TypeInfo(name, ERangeSpec.ByRange, libs);
        }

        public static TypeInfo CreateRangedType(string name, bool isNotSynthesizable, params string[] libs)
        {
            return new TypeInfo(name, ERangeSpec.ByRange, isNotSynthesizable, libs);
        }

        public static TypeInfo CreateRangedType(string name, Range defaultRange)
        {
            return new TypeInfo(name, defaultRange);
        }

        public static TypeInfo CreateRangedType(string name, Range defaultRange, params string[] libs)
        {
            return new TypeInfo(name, defaultRange, libs);
        }
    }


    public class SystemCTypes
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
            return  "sc_logic('" + value.ToString() + "')";
        }        
        
        //      ????
        public static string ValueOf(StdLogicVector value)
        {
            return "sc_lv<" + value.Size + ">(\"" + value.ToString() + "\")";
            //char[] aux = value.ToString().ToCharArray();
            //Array.Reverse(aux);
            //return "\"" + new string(aux) + "\"";
        }

        //      ????
        public static string ValueOf(Unsigned value)
        {
            return "sc_biguint<" + value.Size + ">(" + value.BigIntValue + ")";
        }

        //      ????
        public static string ValueOf(Signed value)
        {
            return "sc_bigint<" + value.Size +  ">(" + value.BigIntValue + ")";
        }

        //      ????
        public static string ValueOf(SFix value)
        {
            return "sc_fixed<" + value.Format.TotalWidth + ", " + value.Format.IntWidth + ">(" + ValueOf(value.DoubleValue) + ")";
        }

        //      ????
        public static string ValueOf(UFix value)
        {
            return "sc_ufixed<" + value.Format.TotalWidth + ", " + value.Format.IntWidth + ">(" + ValueOf(value.DoubleValue) + ")";
        }

        public static string ValueOf(string value)
        {
            return "\"" + value + "\"";
        }

        public static string ValueOf(char value)
        {
            return value.ToString();
        }

        public static string ValueOf(Time value)
        {
            string TimeUnitsRepresentation;

            switch (value.Unit)
            {
                case ETimeUnit.fs:
                    value = new Time(value.ScaleTo(ETimeUnit.fs), ETimeUnit.fs);
                    TimeUnitsRepresentation = "SC_FS";
                    break;
                case ETimeUnit.ps:
                    value = new Time(value.ScaleTo(ETimeUnit.ps), ETimeUnit.ps);
                    TimeUnitsRepresentation = "SC_PS";
                    break;
                case ETimeUnit.ns:
                    value = new Time(value.ScaleTo(ETimeUnit.ns), ETimeUnit.ns);
                    TimeUnitsRepresentation = "SC_NS";
                    break;
                case ETimeUnit.us:
                    value = new Time(value.ScaleTo(ETimeUnit.us), ETimeUnit.us);
                    TimeUnitsRepresentation = "SC_US";
                    break;
                case ETimeUnit.ms:
                    value = new Time(value.ScaleTo(ETimeUnit.ms), ETimeUnit.ms);
                    TimeUnitsRepresentation = "SC_MS";
                    break;
                case ETimeUnit.sec:
                    value = new Time(value.ScaleTo(ETimeUnit.sec), ETimeUnit.sec);
                    TimeUnitsRepresentation = "SC_SEC";
                    break;
                default:
                    throw new NotImplementedException();
            }

            return "sc_time(" + ((long)value.Value).ToString() + ", " + TimeUnitsRepresentation + ")";
        }

        public static string ValueOf(float value)
        {
            return value.ToString();
        }

       
        public static string ValueOf(double value)
        {
            string aux = value.ToString();
            string result = aux.Replace(',', '.');
            return result;
        }

        #endregion

        #region from bool

        [Conversion(typeof(bool), typeof(StdLogic))]
        public static string Convert_bool_StdLogic(string value, TypeDescriptor ttype)
        {
            return "sc_logic(" + value + ")";
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
            return "sc_int<8>(" + value + ").to_string()" ;
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

        [Conversion(typeof(byte), typeof(Signed))]  // Signed or Unsigned ????
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

        //
        [Conversion(typeof(ushort), typeof(Unsigned))]
        public static string Convert_ushort_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return "sc_biguint<" + width + "> " + "(" + value + ")";
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
            return value;
        }
                
        [Conversion(typeof(int), typeof(Signed))]
        public static string Convert_int_Signed(string value, TypeDescriptor ttype)
        {
            //var fmt = SFix.GetFormat(ttype);
            //fmt.IntWidth
            //return "sc_bigint(" + value + ")";
            return Convert_int_Signed(value, "32", ttype);
        }

        [Conversion(typeof(int), typeof(Signed))]
        public static string Convert_int_Signed(string value, string width, TypeDescriptor ttype)
        {
            return "sc_bigint<" + width + "> " + "(" + value + ")";
        }
                
        [Conversion(typeof(int), typeof(Unsigned))]
        public static string Convert_int_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return "sc_biguint<" + width + "> " + "(" + value + ")";            
        }

        [Conversion(typeof(int), typeof(uint))]
        public static string Convert_int_uint(string value, TypeDescriptor ttype)
        {
            return "(unsigned int)" + value;
        }
        
        [Conversion(typeof(int), typeof(float))]
        public static string Convert_int_float(string value, TypeDescriptor ttype)
        {
            return "(float) " + value;
        }

        [Conversion(typeof(int), typeof(double))]
        public static string Convert_int_double(string value, TypeDescriptor ttype)
        {
            return "(double)" + value;
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
            return value;
        }

        [Conversion(typeof(uint), typeof(Unsigned))]
        public static string Convert_uint_Unsigned(string value, TypeDescriptor ttype)
        {
            return "sc_biguint<32> " + "(" + value + ")";
        }

        [Conversion(typeof(uint), typeof(Unsigned))]
        public static string Convert_uint_Unsigned(string value, string width, TypeDescriptor ttype)
        {
            return "sc_biguint<" + width + "> " + "(" + value + ")";
        }

        //      ????
        [Conversion(typeof(uint), typeof(StdLogicVector))]
        public static string Convert_uint_SLV(string value, string width, TypeDescriptor ttype)
        {
            return "sc_lv<" + width + ">" + "(" + value + ")";
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
            return Convert_int_Signed(value, width, ttype);
        }

        [Conversion(typeof(long), typeof(StdLogicVector))]
        public static string Convert_long_SLV(string value, string width, TypeDescriptor ttype)
        {
            return "sc_lv<" + width + ">" + "(" + value + ")";
        }

        [Conversion(typeof(long), typeof(StdLogicVector))]
        public static string Convert_long_SLV(string value, TypeDescriptor ttype)
        {
            return "sc_lv<64>" + "(" + value + ")";
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
            return Convert_int_Unsigned(value, width, ttype);
        }

        [Conversion(typeof(ulong), typeof(StdLogicVector))]
        public static string Convert_ulong_SLV(string value, string width, TypeDescriptor ttype)
        {
            return "sc_lv<" + width + ">" + "(" + value + ")";
        }

        [Conversion(typeof(ulong), typeof(StdLogicVector))]
        public static string Convert_ulong_SLV(string value, TypeDescriptor ttype)
        {
            return "sc_lv<64>" + "(" + value + ")";
        }

        #endregion

        #region from char

        [Conversion(typeof(char), typeof(StdLogic))]
        public static string Convert_char_StdLogic(string value, TypeDescriptor ttype)
        {
            return "\'" + value + "\'";
        }

        #endregion

        #region from string

        //[Conversion(typeof(string), typeof(StdLogicVector))]
        //public static string Convert_string_SLV(string value)
        //{
            
        //    return "sc_lv<" + value.Length + ">(" + value + ")";
        //}
        

        #endregion

        #region from StdLogic

        [Conversion(typeof(StdLogic), typeof(string))]
        public static string Convert_SL_string(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(StdLogic), typeof(bool))]
        public static string Convert_SL_bool(string value, TypeDescriptor ttype)
        {
            return "sc_logic('" + value + "').to_bool()";
        }

        [Conversion(typeof(StdLogic), typeof(StdLogicVector))]
        public static string Convert_SL_SLV(string value, TypeDescriptor ttype)
        {
            return "sc_lv<1>(" + value + ")";
        }

        #endregion

        #region from StdLogic[]

        [Conversion(typeof(StdLogic[]), typeof(StdLogicVector))]
        public static string Convert_aSL_SLV(string value, TypeDescriptor ttype)
        {
            string[] aux = value.Split(',');
            StringBuilder sb = new StringBuilder();
            
            sb.Append("(");
            for(int i=0; i < aux.Length; i++)
            {
                if (i==0)
                    sb.Append("sc_lv<1>" + aux[i] + ")");
                else if(i==(aux.Length - 1))
                    sb.Append(", sc_lv<1>(" + aux[i]);
                else
                    sb.Append(", sc_lv<1>(" + aux[i] + ")");
            }
            sb.Append(")");
            return sb.ToString();
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
            if (ttype.CILType.Name.Equals("Int64"))
                return "sc_bigint<64>(" + value + ")";
            else
            {
                return "sc_bigint<" + ttype.TypeParams[0] + ">(" + value + ")";
            }            
        }

        [Conversion(typeof(StdLogicVector), typeof(Unsigned))]
        public static string Convert_SLV_Unsigned(string value, TypeDescriptor ttype)
        {
            if (ttype.CILType.Name.Equals("Int64"))
                return "sc_biguint<64>(" + value + ")";
            else
            {
                return "sc_biguint<" + ttype.TypeParams[0] + ">(" + value + ")";
            }      
        }

        [Conversion(typeof(StdLogicVector), typeof(SFix))]
        public static string Convert_SLV_SFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            var fmt = ttype.GetFixFormat();
            return "lv_to_fixed(" + value + ", sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (0))";
            //return "sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (sc_bigint<" + fmt.TotalWidth + ">(" + value + "))";
        }

        [Conversion(typeof(StdLogicVector), typeof(SFix))]
        public static string Convert_SLV_SFix(string value, string fracWidth, TypeDescriptor ttype)
        {
            FixFormat fmt = SFix.GetFormat(ttype);
            return "lv_to_fixed(" + value + ", sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (0))";
            //return "sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (sc_bigint<" + fmt.TotalWidth + ">(" + value + "))";
        }

        [Conversion(typeof(StdLogicVector), typeof(SFix))]
        public static string Convert_SLV_SFix(string value, TypeDescriptor ttype)
        {
            FixFormat fmt = SFix.GetFormat(ttype);
            return "lv_to_fixed(" + value + ", sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (0))";
            //return "sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (sc_bigint<" + fmt.TotalWidth + ">(" + value + "))";
        }

        [Conversion(typeof(StdLogicVector), typeof(UFix))]
        public static string Convert_SLV_UFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            FixFormat fmt = UFix.GetFormat(ttype);
            return "lv_to_ufixed(" + value + ", sc_ufixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (0))";
            //return "sc_ufixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (sc_bigint<" + fmt.TotalWidth + ">(" + value + "))";
        }

        [Conversion(typeof(StdLogicVector), typeof(UFix))]
        public static string Convert_SLV_UFix(string value, string fracWidth, TypeDescriptor ttype)
        {
            FixFormat fmt = UFix.GetFormat(ttype);
            return "lv_to_ufixed(" + value + ", sc_ufixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (0))";
            //return "sc_ufixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (sc_bigint<" + fmt.TotalWidth + ">(" + value + "))";
        }

        [Conversion(typeof(StdLogicVector), typeof(UFix))]
        public static string Convert_SLV_UFix(string value, TypeDescriptor ttype)
        {
            FixFormat fmt = UFix.GetFormat(ttype);
            return "lv_to_ufixed(" + value + ", sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (0))";
            //return "sc_ufixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (sc_bigint<" + fmt.TotalWidth + ">(" + value + "))";
        }

        [Conversion(typeof(StdLogicVector), typeof(float))]
        public static string Convert_SLV_float(string value, TypeDescriptor ttype)
        {
            return value + ".to_double()";
        }

        [Conversion(typeof(StdLogicVector), typeof(double))]
        public static string Convert_SLV_double(string value, TypeDescriptor ttype)
        {
            return value + ".to_double()";
            //return "sc_lv<" + ttype.Constraints[0].Size + ">(" + value + ").to_double()";
        }

        //      ????
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

        //      ?????
        [Conversion(typeof(Signed), typeof(int))]
        public static string Convert_Signed_int(string value, TypeDescriptor ttype)
        {
            //return "to_integer(" + value + ")";
            return value + ".to_int()";
        }

        //      ?????
        [Conversion(typeof(Signed), typeof(long))]
        public static string Convert_Signed_long(string value, TypeDescriptor ttype)
        {
            //return Resize(value, "64");
            return value;
        }

        //      ?????
        [Conversion(typeof(Signed), typeof(StdLogicVector))]
        public static string Convert_Signed_SLV(string value, TypeDescriptor ttype)
        {
            return "sc_lv<" + ttype.Constraints[0].Size + ">(" + value + ")";
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
            return value + ".to_int()";
        }

        [Conversion(typeof(Unsigned), typeof(uint))]
        public static string Convert_Unsigned_uint(string value, TypeDescriptor ttype)
        {
            //return Resize(value, "32");
            return value + ".to_uint()";
        }

        [Conversion(typeof(Unsigned), typeof(ulong))]
        public static string Convert_Unsigned_ulong(string value, TypeDescriptor ttype)
        {
            //return Resize(value, "64");
            return value;
        }

        [Conversion(typeof(Unsigned), typeof(StdLogicVector))]
        public static string Convert_Unsigned_SLV(string value, TypeDescriptor ttype)
        {
            //return "std_logic_vector(" + value + ")";
            return value;
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
            return value;
            //return "image(to_real(" + value + "))";
        }

        [Conversion(typeof(double), typeof(string))]
        public static string Convert_double_string(string value, TypeDescriptor ttype)
        {
            return value;
            //return "image(to_real(" + value + "))";
        }

        [Conversion(typeof(float), typeof(SFix))]
        public static string Convert_float_SFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            var fmt = ttype.GetFixFormat();
            return "sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (" + value + ")";
        }

        [Conversion(typeof(double), typeof(SFix))]
        public static string Convert_double_SFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            var fmt = ttype.GetFixFormat();
            return "sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (" + value + ")";
        }

        [Conversion(typeof(float), typeof(StdLogicVector))]
        public static string Convert_float_SLV(string value, TypeDescriptor ttype)
        {
            return "sc_lv(" + value + ")";
        }

        [Conversion(typeof(float), typeof(float))]
        public static string Convert_float_SFix(string value, TypeDescriptor ttype)
        {
            return value;
        }

        [Conversion(typeof(float), typeof(UFix))]
        public static string Convert_float_UFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            var fmt = ttype.GetFixFormat();
            return "sc_ufixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (" + value + ")";
        }

        [Conversion(typeof(double), typeof(UFix))]
        public static string Convert_double_UFix(string value, string intWidth, string fracWidth, TypeDescriptor ttype)
        {
            var fmt = ttype.GetFixFormat();
            return "sc_ufixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (" + value + ")";           
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
            return value;
            //return "resize(" + value + ", 8, 23)";
        }

        [Conversion(typeof(float), typeof(double))]
        public static string Convert_float_double(string value, TypeDescriptor ttype)
        {
            //return "resize(" + value + ", 11, 52)";
            return value;
        }

        #endregion

        #region from SFix

        [Conversion(typeof(SFix), typeof(float))]
        public static string Convert_SFix_float(string value, TypeDescriptor ttype)
        {
            return value + ".to_float()";
        }

        [Conversion(typeof(SFix), typeof(double))]
        public static string Convert_SFix_double(string value, TypeDescriptor ttype)
        {
            return value + ".to_float()";
        }

        [Conversion(typeof(SFix), typeof(StdLogicVector))]
        public static string Convert_SFix_slv(string value, TypeDescriptor ttype)
        {
            return "sc_lv<" + ttype.Constraints[0].Size + 
                "> (static_cast<sc_bv_base>(" + value + "(" + (ttype.Constraints[0].Size - 1) + ", 0)))";
        }

        #endregion

        #region from UFix

        [Conversion(typeof(UFix), typeof(float))]
        public static string Convert_UFix_float(string value, TypeDescriptor ttype)
        {
            return value + ".to_float()";
        }

        [Conversion(typeof(UFix), typeof(double))]
        public static string Convert_UFix_double(string value, TypeDescriptor ttype)
        {
            return value + ".to_float()";
        }

        [Conversion(typeof(UFix), typeof(StdLogicVector))]
        public static string Convert_UFix_slv(string value, TypeDescriptor ttype)
        {
            return "sc_lv<" + ttype.Constraints[0].Size +
                "> (static_cast<sc_bv_base>(" + value + "(" + (ttype.Constraints[0].Size - 1) + ", 0)))";
        }

        [Conversion(typeof(UFix), typeof(SFix))]
        public static string Convert_UFix_SFix(string value, TypeDescriptor ttype)
        {
            var fmt = ttype.GetFixFormat();
            return "sc_fixed<" + fmt.TotalWidth + ", " + fmt.IntWidth + "> (" + value + ")";                    
        }

        #endregion

        //private static string Resize(string value, string width)
        //{
        //    return "resize(" + value + ", " + width + ")";
        //}

        /*private static string Resize(string value, string intWidth, string fracWidth)
        {
            return "resize(" + value + ", " + intWidth + "-1, -" + fracWidth + ")";
        }*/

        //private static string ResizeSLV(string value, string width)
        //{

        //    return "std_logic_vector(" + Resize("unsigned(" + value + ")", width) + ")";
        //}

        private static Dictionary<Type, Dictionary<Type, List<MethodInfo>>> _converters =
            new Dictionary<Type, Dictionary<Type, List<MethodInfo>>>();

        private static Dictionary<Type, TypeInfo> _typeInfos =
            new Dictionary<Type, TypeInfo>();

        private static void InitConverters()
        {
            var convms = typeof(SystemCTypes).GetMethods().Where(mi => mi.Name.StartsWith("Convert"));
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
            RegisterType(typeof(bool), TypeInfo.CreateRangedType("bool"));
            RegisterType(typeof(sbyte), TypeInfo.CreateRangedType("sc_int<8>", "math.h", "stdlib.h"));
            RegisterType(typeof(byte), TypeInfo.CreateRangedType("sc_uint<8>", "math.h", "stdlib.h"));
            RegisterType(typeof(short), TypeInfo.CreateRangedType("short", "math.h", "stdlib.h"));
            RegisterType(typeof(ushort), TypeInfo.CreateRangedType("unsigned short int", "math.h", "stdlib.h"));
            RegisterType(typeof(int), TypeInfo.CreateRangedType("int", "math.h", "stdlib.h"));
            RegisterType(typeof(uint), TypeInfo.CreateSizedType("unsigned int", "math.h", "stdlib.h"));
            RegisterType(typeof(long), TypeInfo.CreateSizedType("sc_int<64>", "math.h", "stdlib.h"));
            RegisterType(typeof(ulong), TypeInfo.CreateSizedType("sc_uint<64>", "math.h", "stdlib.h"));
            RegisterType(typeof(char), TypeInfo.CreateRangedType("char"));
            RegisterType(typeof(string), TypeInfo.CreateSizedType("string", "string.h"));
            RegisterType(typeof(StdLogic), TypeInfo.CreateRangedType("sc_logic"));
            RegisterType(typeof(StdLogicVector), TypeInfo.CreateSizedType("sc_lv"));

            ////RegisterType(typeof(Signed), TypeInfo.CreateSizedType("sfixed", "ieee", "numeric_std", "ieee_proposed", "fixed_pkg"));
            ////RegisterType(typeof(Unsigned), TypeInfo.CreateSizedType("ufixed", "ieee", "numeric_std", "ieee_proposed", "fixed_pkg"));
            RegisterType(typeof(Signed), TypeInfo.CreateSizedType("sc_bigint", "math.h", "stdlib.h"));
            RegisterType(typeof(Unsigned), TypeInfo.CreateSizedType("sc_biguint", "math.h", "stdlib.h"));
            RegisterType(typeof(SFix), TypeInfo.CreateSizedType("sc_fixed", "math.h", "stdlib.h", "#define SC_INCLUDE_FX"));
            RegisterType(typeof(UFix), TypeInfo.CreateSizedType("sc_ufixed", "math.h", "stdlib.h", "#define SC_INCLUDE_FX"));
            ////RegisterType(typeof(float), TypeInfo.CreateRangedType("float32", true, "ieee", "math_real", "ieee_proposed", "float_pkg", "work", "sim_pkg"));
            ////RegisterType(typeof(double), TypeInfo.CreateRangedType("float64", true, "ieee", "math_real", "ieee_proposed", "float_pkg", "work", "sim_pkg"));
            RegisterType(typeof(float), TypeInfo.CreateRangedType("float", "math.h", "stdlib.h"));
            RegisterType(typeof(double), TypeInfo.CreateRangedType("double", "math.h", "stdlib.h"));

            RegisterType(typeof(Time), TypeInfo.CreateRangedType("sc_time"));
        }

        static SystemCTypes()
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

        private SystemCTypes()
        {
            _allTypes = _converters.Keys.Union(_converters.SelectMany(kvp => kvp.Value.Keys)).Distinct();
            _typePaths = new CacheDictionary<Type, TypePaths>(CreateTypePaths);
        }

        private static readonly SystemCTypes _instance = new SystemCTypes();

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

        public static bool GetValueOf(object value, out string svalue)
        {
            Type vtype = value.GetType();
            MethodInfo valueof = typeof(SystemCTypes).GetMethod("ValueOf", new Type[] { vtype });
            svalue = null;
            if (valueof == null)
                return false;
            svalue = (string)valueof.Invoke(null, new object[] { value });
            return true;
        }

        public static bool LookupType(Type type, out TypeInfo ti)
        {
            return _typeInfos.TryGetValue(type, out ti);
        }
    }
}

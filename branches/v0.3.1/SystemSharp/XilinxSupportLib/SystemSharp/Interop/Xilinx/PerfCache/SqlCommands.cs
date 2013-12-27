/**
 * Copyright 2013 Christian Köllner
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
using System.Text.RegularExpressions;
using SystemSharp.Interop.Xilinx.CoreGen;

namespace SystemSharp.Interop.Xilinx.PerfCache
{
    static class SqlCommands
    {
        public static string GetSqlTypeOfCoregenProp(Type type)
        {
            if (type.IsEnum)
            {
                var props = PropEnum.EnumProps(type);
                int maxlen = props.Max(_ => _.IDs.Any() ? _.IDs.Values.Max(v => v.Length) : (_.EnumValue != null ? _.EnumValue.ToString().Length : 0));
                return "VARCHAR(" + maxlen + ")";
            }
            else if (type == typeof(sbyte) ||
                type == typeof(byte) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(int))
            {
                return "INTEGER";
            }
            else if (type == typeof(bool))
            {
                return "BOOLEAN";
            }
            else if (type == typeof(float) ||
                type == typeof(double))
            {
                return "DECIMAL";
            }
            else if (type == typeof(string))
            {
                return "TEXT";
            }
            else if (type == typeof(byte[]))
            {
                return "BLOB";
            }
            else
            {
                return "TEXT";
            }
        }

        public static string WrapSqlValue(object value, Type type)
        {
            if (value == null)
                return "NULL";
            else if (type == typeof(byte[]))
                return "'" + Convert.ToBase64String((byte[])value) + "'";
            else if (type == typeof(int))
                return value.ToString();
            else
                return "'" + value + "'";
        }

        public static object UnwrapSqlValue(object value, Type type)
        {
            if (value == null)
            {
                return null;
            }
            else if (type == typeof(byte[]))
            {
                return Convert.FromBase64String((string)value);
            }
            else
            {
                Debug.Assert(value.GetType() == type);
                return value;
            }
        }

        public static string WrapSqlValue(object value)
        {
            return WrapSqlValue(value, value.GetType());
        }

        public static string GetSql_TableName(this CoreGenDescription desc)
        {
            return Regex.Replace(desc.SelectCommand.Selection, "[^a-zA-Z0-9_]+", "", RegexOptions.Compiled);
        }

        public static string GetSql_CreateTable(this CoreGenDescription desc)
        {
            Contract.Requires(desc != null);
            if (desc.SelectCommand == null)
                throw new InvalidOperationException("no select command");

            var sb = new StringBuilder();
            sb.Append("CREATE TABLE IF NOT EXISTS ");
            sb.Append(desc.GetSql_TableName());
            sb.Append(" (");
            var primkeys = new List<string>();
            foreach (var setcmd in desc.SetCommands)
            {
                sb.Append(setcmd.AttrName);
                sb.Append(" ");
                string type = GetSqlTypeOfCoregenProp(setcmd.CILType);
                sb.Append(type);
                sb.AppendLine(" NOT NULL, ");
                primkeys.Add(setcmd.AttrName);
            }
            foreach (var csetcmd in desc.CSetCommands)
            {
                sb.Append(csetcmd.AttrName);
                sb.Append(" ");
                string type = GetSqlTypeOfCoregenProp(csetcmd.CILType);
                sb.Append(type);
                sb.AppendLine(" NOT NULL, ");
                primkeys.Add(csetcmd.AttrName);
            }
            var prprops = typeof(PerformanceRecord).GetProperties();
            foreach (var pi in prprops)
            {
                sb.Append(pi.Name);
                sb.Append(" ");
                sb.Append(GetSqlTypeOfCoregenProp(pi.PropertyType));
                sb.AppendLine(", ");
            }
            sb.Append("PRIMARY KEY (");
            sb.Append(string.Join(", ", primkeys));
            sb.Append(")");
            sb.AppendLine(");");
            return sb.ToString();
        }

        public static string GetSql_QueryRecord(this CoreGenDescription desc)
        {
            Contract.Requires(desc != null);
            if (desc.SelectCommand == null)
                throw new InvalidOperationException("no select command");

            var sb = new StringBuilder();
            sb.Append("SELECT ");
            sb.Append(string.Join(", ",
                typeof(PerformanceRecord).GetProperties().Select(_ => _.Name)));
            sb.Append(" FROM ");
            sb.AppendLine(desc.GetSql_TableName());
            sb.Append("WHERE ");
            var pairs = new List<string>();
            foreach (var setcmd in desc.SetCommands)
            {
                pairs.Add(setcmd.AttrName + "=" + WrapSqlValue(setcmd.AttrValue, setcmd.CILType));
            }
            foreach (var csetcmd in desc.CSetCommands)
            {
                pairs.Add(csetcmd.AttrName + "=" + WrapSqlValue(csetcmd.AttrValue, csetcmd.CILType));
            }
            sb.AppendLine(string.Join(" AND ", pairs));
            sb.AppendLine(";");
            return sb.ToString();
        }
    
        public static string GetSql_InsertRecord(this CoreGenDescription desc, PerformanceRecord prec)
        {
            Contract.Requires(desc != null);
            Contract.Requires(prec != null);
            if (desc.SelectCommand == null)
                throw new InvalidOperationException("no select command");

            var sb = new StringBuilder();
            sb.Append("INSERT INTO ");
            sb.Append(desc.GetSql_TableName());
            var keys = new List<string>();
            var values = new List<string>();
            foreach (var setcmd in desc.SetCommands)
            {
                keys.Add(setcmd.AttrName);
                values.Add(WrapSqlValue(setcmd.AttrValue, setcmd.CILType));
            }
            foreach (var csetcmd in desc.CSetCommands)
            {
                keys.Add(csetcmd.AttrName);
                values.Add(WrapSqlValue(csetcmd.AttrValue, csetcmd.CILType));
            }
            var prprops = typeof(PerformanceRecord).GetProperties();
            foreach (var pi in prprops)
            {
                keys.Add(pi.Name);
                values.Add(WrapSqlValue(pi.GetValue(prec, new object[0])));
            }
            sb.Append(" (");
            sb.Append(string.Join(", ", keys));
            sb.Append(") VALUES(");
            sb.Append(string.Join(", ", values));
            sb.AppendLine(");");
            return sb.ToString();
        }

        public static string GetSql_UpdateRecord(this CoreGenDescription desc, PerformanceRecord prec)
        {
            Contract.Requires(desc != null);
            Contract.Requires(prec != null);
            if (desc.SelectCommand == null)
                throw new InvalidOperationException("no select command");

            var sb = new StringBuilder();
            sb.Append("UPDATE ");
            sb.AppendLine(desc.GetSql_TableName());
            sb.Append("SET ");
            var prprops = typeof(PerformanceRecord).GetProperties();
            var values = new List<string>();
            foreach (var pi in prprops)
            {
                values.Add(pi.Name + "=" + WrapSqlValue(pi.GetValue(prec, new object[0])));
            }
            sb.AppendLine(string.Join(", ", values));
            sb.Append("WHERE ");
            var keys = new List<string>();
            foreach (var setcmd in desc.SetCommands)
            {
                keys.Add(setcmd.AttrName + "=" + WrapSqlValue(setcmd.AttrValue, setcmd.CILType));
            }
            foreach (var csetcmd in desc.CSetCommands)
            {
                keys.Add(csetcmd.AttrName + "=" + WrapSqlValue(csetcmd.AttrValue, csetcmd.CILType));
            }
            sb.Append(string.Join(" AND ", keys));
            sb.AppendLine(";");
            return sb.ToString();
        }
    }
}

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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using Microsoft.Win32;
using SystemSharp.Analysis.M2M;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.Interop.Xilinx.CoreGen;
using SystemSharp.Interop.Xilinx.IPCores;
using SystemSharp.Synthesis;

namespace SystemSharp.Interop.Xilinx
{
    enum EPropAssoc
    {
        ISE,
        CoreGen,
        CoreGenProj,
        XST,
        MAP,
        PAR,
        PARReport
    }

    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, AllowMultiple = true)]
    class PropID : Attribute
    {
        public EPropAssoc Assoc { get; private set; }
        public string ID { get; private set; }

        public PropID(EPropAssoc assoc, string id)
        {
            Assoc = assoc;
            ID = id;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    class PropValueType : Attribute
    {
        public Type Type { get; private set; }
        public object DefaultValue { get; private set; }

        public PropValueType(Type type)
        {
            Type = type;
            DefaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        public PropValueType(Type type, object defaultValue)
        {
            Type = type;
            DefaultValue = defaultValue;
        }
    }

    enum ECoreGenUsage
    {
        Select,
        CSet
    }

    [AttributeUsage(AttributeTargets.Property)]
    class CoreGenProp: Attribute
    {
        public ECoreGenUsage Usage { get; private set; }

        public CoreGenProp(ECoreGenUsage usage)
        {
            Usage = usage;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    class PresentOn : Attribute
    {
        public object PresenceCondition { get; private set; }

        public PresentOn(object presenceCondition)
        {
            PresenceCondition = presenceCondition;
        }
    }

    public enum EHDL
    {
        Verilog,
        VHDL
    }

    public enum EDesignFlow
    {
        Schematic,
        VHDL,
        Verilog
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class DeclareFamily : Attribute
    {
        public EDeviceFamily Family { get; private set; }

        public DeclareFamily(EDeviceFamily family)
        {
            Family = family;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public class DeclarePackage : Attribute
    {
        public EPackage Package { get; private set; }

        public DeclarePackage(EPackage package)
        {
            Package = package;
        }
    }

    public enum EISEVersion
    {
        [PropID(EPropAssoc.ISE, "11.1")]
        _11_1,

        [PropID(EPropAssoc.ISE, "11.2")]
        _11_2,

        [PropID(EPropAssoc.ISE, "11.3")]
        _11_3,

        [PropID(EPropAssoc.ISE, "11.4")]
        _11_4,

        [PropID(EPropAssoc.ISE, "11.5")]
        _11_5,

        [PropID(EPropAssoc.ISE, "12.1")]
        _12_1,

        [PropID(EPropAssoc.ISE, "12.2")]
        _12_2,

        [PropID(EPropAssoc.ISE, "12.3")]
        _12_3,

        [PropID(EPropAssoc.ISE, "12.4")]
        _12_4,

        [PropID(EPropAssoc.ISE, "13.1")]
        _13_1,

        [PropID(EPropAssoc.ISE, "13.2")]
        _13_2,

        [PropID(EPropAssoc.ISE, "13.3")]
        _13_3,

        [PropID(EPropAssoc.ISE, "13.4")]
        _13_4,

        [PropID(EPropAssoc.ISE, "14.1")]
        _14_1,

        [PropID(EPropAssoc.ISE, "14.2")]
        _14_2
    }

    public static class ISEVersions
    {
        public static IEnumerable<EISEVersion> GetISEVersions()
        {
            return typeof(EISEVersion)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(fi => (EISEVersion)fi.GetValue(null));
        }

        public static string GetVersionText(this EISEVersion ver)
        {
            return PropEnum.ToString(ver, EPropAssoc.ISE);
        }
    }

    class PropDesc
    {
        public object EnumValue { get; private set; }
        public Dictionary<EPropAssoc, string> IDs { get; private set; }
        public Type Type { get; private set; }
        public object DefaultValue { get; private set; }

        public PropDesc(object enumValue, Type type, object defaultValue)
        {
            EnumValue = enumValue;
            IDs = new Dictionary<EPropAssoc, string>();
            Type = type;
            DefaultValue = defaultValue;
        }

        public EPropAssoc Assocs
        {
            get { return IDs.Keys.Aggregate((x, y) => x | y); }
        }
    }

    class PropEnum
    {
        public static IList<PropDesc> EnumProps(Type type)
        {
            FieldInfo[] fields = type.GetFields();
            List<PropDesc> result = new List<PropDesc>();
            foreach (FieldInfo field in fields)
            {
                if (!field.IsStatic)
                    continue;
                object value = field.GetValue(null);
                object[] valueTypes = field.GetCustomAttributes(typeof(PropValueType), false);
                Type valueType = null;
                object defaultValue = null;
                foreach (PropValueType pvt in valueTypes)
                {
                    valueType = pvt.Type;
                    defaultValue = pvt.DefaultValue;
                }
                PropDesc pd = new PropDesc(value, valueType, defaultValue);
                object[] propIDs = field.GetCustomAttributes(typeof(PropID), false);
                foreach (PropID pid in propIDs)
                {
                    pd.IDs[pid.Assoc] = pid.ID;
                }
                result.Add(pd);
            }
            return result;
        }

        public static string ToString(object value, EPropAssoc assoc)
        {
            if (value is bool)
            {
                bool bvalue = (bool)value;
                return bvalue ? "true" : "false";
            }
            Type type = value.GetType();
            IList<PropDesc> allFields = EnumProps(type);
            PropDesc pd = (from PropDesc pdi in allFields
                           where pdi.EnumValue.Equals(value)
                           select pdi).SingleOrDefault();
            string result;
            if (pd == null || !pd.IDs.TryGetValue(assoc, out result))
            {
                if (value is double)
                {
                    double dbl = (double)value;
                    result = dbl.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    result = value.ToString();
                }
            }
            return result;
        }

        public static string ToString(PropertyInfo pi, EPropAssoc assoc)
        {
            object[] attrs = pi.GetCustomAttributes(typeof(PropID), true);
            foreach (PropID propID in attrs)
            {
                if (propID.Assoc == assoc)
                    return propID.ID;
            }
            throw new InvalidOperationException("No property ID found for given association");
        }

        public static PropDesc FindProp(EXilinxProjectProperties prop)
        {
            IList<PropDesc> allProps = PropEnum.EnumProps(typeof(EXilinxProjectProperties));
            PropDesc pd = (from PropDesc pdi in allProps
                           where pdi.EnumValue.Equals(prop)
                           select pdi).Single();
            return pd;
        }
    }

    class PropertyBag
    {
        public PropertyBag()
        {
            Properties = new Dictionary<EXilinxProjectProperties, object>();
        }

        public Dictionary<EXilinxProjectProperties, object> Properties { get; private set; }

        public object GetProperty(EXilinxProjectProperties prop)
        {
            PropDesc pd = PropEnum.FindProp(prop);
            object result;
            if (!Properties.TryGetValue(prop, out result))
                result = pd.DefaultValue;
            return result;
        }

        public void PutProperty(EXilinxProjectProperties prop, object value)
        {
            if (value == null)
                throw new ArgumentException("Value must be non-null");

            PropDesc pd = PropEnum.FindProp(prop);
            if (!pd.Type.IsInstanceOfType(value))
                throw new ArgumentException("Wrong argument type");

            Properties[prop] = value;
        }

        public PropertyBag Copy(EPropAssoc assoc)
        {
            PropertyBag result = new PropertyBag();
            foreach (var pd in PropEnum.EnumProps(typeof(EXilinxProjectProperties)))
            {
                EXilinxProjectProperties key = (EXilinxProjectProperties)pd.EnumValue;
                object value;
                if (pd.IDs.ContainsKey(assoc) && Properties.TryGetValue(key, out value))
                    result.PutProperty(key, value);
            }
            return result;
        }
    }

    public static class RegistryExtensions
    {
        public static RegistryKey OpenSubKeys(this RegistryKey key, params string[] subKeys)
        {
            RegistryKey cur = key;
            int i = 0;
            while (cur != null && i < subKeys.Length)
            {
                cur = cur.OpenSubKey(subKeys[i]);
                i++;
            }
            if (cur == null)
                return null;
            else
                return cur;
        }

        public static object GetValue(this RegistryKey key, params string[] subKeys)
        {
            RegistryKey cur = key;
            int i = 0;
            while (cur != null && i < subKeys.Length - 1)
            {
                cur = cur.OpenSubKey(subKeys[i]);
                i++;
            }
            if (cur == null)
                return null;
            else
                return cur.GetValue(subKeys.Last());
        }
    }

    public static class Tooling
    {
        public static string MakePartName(EDevice device, ESpeedGrade grade, EPackage package)
        {
            return device.ToString() +
                PropEnum.ToString(grade, EPropAssoc.ISE) + "-" +
                package.ToString();
        }
    }
}

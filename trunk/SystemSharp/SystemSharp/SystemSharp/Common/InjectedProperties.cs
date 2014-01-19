/**
 * Copyright 2011-2014 Christian Köllner
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SystemSharp.Analysis.C2M;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.SysDOM;

namespace SystemSharp.Common
{
    /// <summary>
    /// Provides services for late injection of attributes to CLI objects.
    /// </summary>
    /// <remarks>
    /// We know CLI attributes as a means of associating elements, such as types, methods or fields, with meta information.
    /// The respective element needs to be associated with the attribute at compile time. So once the code is compiled, there
    /// is no chance to associate it with any new attribute. This class provides a concept of associating CLI objects with 
    /// attributes during runtime. It does so by internally retaining weak hash maps of the augmented objects. Please note:
    /// In order to query a lately injected attribute, you cannot go by the CLI reflection API any more, because that API
    /// does not know about the existence of this class. Instead, you the methods of this class.
    /// </remarks>
    public static class AttributeInjector
    {
        private static ConditionalWeakTable<Type, List<Attribute>> _typeAttrs =
            new ConditionalWeakTable<Type, List<Attribute>>();
        private static ConditionalWeakTable<MethodBase, List<Attribute>> _methodAttrs =
            new ConditionalWeakTable<MethodBase, List<Attribute>>();
        private static ConditionalWeakTable<Type, Dictionary<string, List<Attribute>>> _methodByNameAttrs =
            new ConditionalWeakTable<Type, Dictionary<string, List<Attribute>>>();
        private static ConditionalWeakTable<object, Dictionary<RuntimeMethodHandle, List<Attribute>>> _methodInstAttrs =
            new ConditionalWeakTable<object, Dictionary<RuntimeMethodHandle, List<Attribute>>>();
        private static ConditionalWeakTable<PropertyInfo, List<Attribute>> _propAttrs =
            new ConditionalWeakTable<PropertyInfo, List<Attribute>>();
        private static ConditionalWeakTable<ParameterInfo, List<Attribute>> _paramAttrs =
            new ConditionalWeakTable<ParameterInfo, List<Attribute>>();
        private static ConditionalWeakTable<FieldInfo, List<Attribute>> _fieldAttrs =
            new ConditionalWeakTable<FieldInfo, List<Attribute>>();

        private static List<object> _retainRefList = new List<object>();

        /// <summary>
        /// Attaches an attribute to a type.
        /// </summary>
        /// <param name="type">type to be associated with an attribute</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void Inject(Type type, Attribute attribute, bool retainRef = false)
        {
            var list = _typeAttrs.GetOrCreateValue(type);
            if (!list.Contains(attribute))
                list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(type);
        }

        /// <summary>
        /// Attaches an attribute to a method.
        /// </summary>
        /// <param name="method">method to be associated with an attribute</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void Inject(MethodBase method, Attribute attribute, bool retainRef = false)
        {
            var list = _methodAttrs.GetOrCreateValue(method);
            list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(method);
        }

        /// <summary>
        /// Attaches an attribute to a field.
        /// </summary>
        /// <param name="field">field to be associated with an attribute</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void Inject(FieldInfo field, Attribute attribute, bool retainRef = false)
        {
            var list = _fieldAttrs.GetOrCreateValue(field);
            list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(field);
        }

        /// <summary>
        /// Attaches an attribute to a method, but only if the method is not yet associated with an equal instance of the attribute.
        /// </summary>
        /// <param name="method">type to be associated with an attribute</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void InjectOnce(MethodBase method, Attribute attribute, bool retainRef = false)
        {
            var list = _methodAttrs.GetOrCreateValue(method);
            if (!list.Contains(attribute))
                list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(method);
        }

        /// <summary>
        /// Attaches an attribute to a method, specified by declaring type and method name.
        /// </summary>
        /// <param name="type">declaring type of the method</param>
        /// <param name="methodName">name of the method</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void InjectMethodByNameAttr(Type type, string methodName, Attribute attribute, bool retainRef = false)
        {
            var dic = _methodByNameAttrs.GetOrCreateValue(type);
            var list = dic.Get(methodName);
            list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(type);
        }

        /// <summary>
        /// Attaches an attribute to a method of a certain instance, 
        /// but only if the method is not yet associated with an equal instance of the attribute.
        /// </summary>
        /// <param name="method">type to be associated with an attribute</param>
        /// <param name="instance">instance on which the method is called</param>
        /// <param name="attribute">attribute to attach</param>
        public static void InjectOnce(MethodBase method, object instance, Attribute attribute)
        {
            var dic = _methodInstAttrs.GetOrCreateValue(instance);
            var list = dic.Get(method.MethodHandle);
            if (!list.Contains(attribute))
                list.Add(attribute);
        }

        /// <summary>
        /// Attaches an attribute to each method with a particular name.
        /// </summary>
        /// <param name="type">declaring type of the methods</param>
        /// <param name="methodName">name of the method(s)</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void InjectEach(Type type, string methodName, Attribute attribute, bool retainRef = false)
        {
            foreach (MethodBase method in type.GetMethods().Where(x => x.Name == methodName))
            {
                Inject(method, attribute, retainRef);
            }
        }

        /// <summary>
        /// Attaches an attribute to each constructor of a certain type.
        /// </summary>
        /// <param name="type">type whose constructors are to be equipped with the attribute</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void InjectEachCtor(Type type, Attribute attribute, bool retainRef = false)
        {
            foreach (MethodBase method in type.GetConstructors())
            {
                Inject(method, attribute, retainRef);
            }
        }

        /// <summary>
        /// Attaches an attribute to a property with a certain name.
        /// </summary>
        /// <param name="type">declaring type of the property</param>
        /// <param name="propName">name of the property</param>
        /// <param name="attribute">attribute to attach</param>
        /// <param name="retainRef"><c>true</c> if the attribute should be retained even if the type information 
        /// is garbage-collected</param>
        public static void InjectToProperty(Type type, string propName, Attribute attribute, bool retainRef = false)
        {
            Contract.Requires(propName != null);

            PropertyInfo pi = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var list = _propAttrs.GetOrCreateValue(pi);
            if (!list.Contains(attribute))
                list.Add(attribute);

            if (retainRef)
                _retainRefList.Add(pi);
        }

        /// <summary>
        /// Attaches an attribute to a method parameter.
        /// </summary>
        /// <param name="pi">method parameter</param>
        /// <param name="attribute">attribute to attach</param>
        public static void Inject(ParameterInfo pi, Attribute attribute)
        {
            Contract.Requires(pi != null);
            Contract.Requires(attribute != null);

            var list = _paramAttrs.GetOrCreateValue(pi);
            if (!list.Contains(attribute))
                list.Add(attribute);
        }

        /// <summary>
        /// Attaches an attribute to a field
        /// </summary>
        /// <param name="fi">field</param>
        /// <param name="attribute">attribute to attach</param>
        public static void Inject(FieldInfo fi, Attribute attribute)
        {
            Contract.Requires(fi != null);
            Contract.Requires(attribute != null);

            _fieldAttrs.GetOrCreateValue(fi).Add(attribute);
        }

        /// <summary>
        /// Selects all attributes which are compatible with a certain type from an object enumeration.
        /// </summary>
        /// <param name="attrs">object enumeration</param>
        /// <param name="type">type of requested attribute</param>
        /// <returns>sub-sequence of attributes with desired type</returns>
        public static IEnumerable<Attribute> SelectAttributes(IEnumerable<object> attrs, Type type)
        {
            return attrs
                .Where(a => type.IsAssignableFrom(a.GetType()))
                .Cast<Attribute>();
        }

        /// <summary>
        /// Returns all attributes which were attached to a type by attribute injection.
        /// </summary>
        /// <param name="thisType">type of query</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>injected attributes</returns>
        public static Attribute[] GetInjectedAttributes(this Type thisType, Type type)
        {
            var r1 = SelectAttributes(thisType.GetBaseTypeChain()
                .SelectMany(t => _typeAttrs.GetOrCreateValue(t)), type);
            var r2 = Enumerable.Empty<Attribute>();
            if (thisType.IsGenericType &&
                !thisType.IsGenericTypeDefinition)
            {
                r2 = SelectAttributes(thisType.GetGenericTypeDefinition().GetBaseTypeChain()
                .SelectMany(t => _typeAttrs.GetOrCreateValue(t)), type);
            }
            return r1.Concat(r2).ToArray();
        }

        /// <summary>
        /// Returns all attributes which were attached to a method or constructor by attribute injection.
        /// </summary>
        /// <param name="method">method or constructor</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>injected attributes</returns>
        public static Attribute[] GetInjectedAttributes(this MethodBase method, Type type)
        {
            var r1 = _methodAttrs.GetOrCreateValue(method);
            var r2 = method.DeclaringType.GetBaseTypeChain().SelectMany(
                t => _methodByNameAttrs.GetOrCreateValue(t).Get(method.Name));
            var mi = method as MethodInfo;
            var r3 = method.IsGenericMethod ? 
                _methodAttrs.GetOrCreateValue(mi.GetGenericMethodDefinition()) : 
                Enumerable.Empty<Attribute>();
            var r4 = Enumerable.Empty<Attribute>();
            if (method.DeclaringType.IsGenericType &&
                !method.DeclaringType.IsGenericTypeDefinition)
            {
                var mtype = method.DeclaringType.GetGenericTypeDefinition();
                BindingFlags flags = BindingFlags.Default;
                if (method.CallingConvention.HasFlag(CallingConventions.HasThis))
                    flags |= BindingFlags.Instance;
                if (method.IsPublic)
                    flags |= BindingFlags.Public;
                else
                    flags |= BindingFlags.NonPublic;

                //FIXME: Disambiguate by parameter types
                MethodBase gmethod;
                if (method.IsConstructor)
                {
                    flags |= BindingFlags.CreateInstance;
                    gmethod = mtype.GetConstructors(flags)
                        .FirstOrDefault();
                }
                else
                {
                    gmethod = mtype.GetMethods(flags)
                        .Where(m => m.Name == method.Name)
                        .FirstOrDefault();
                }
                if (gmethod == null)
                    r4 = Enumerable.Empty<Attribute>();
                else
                    r4 = GetInjectedAttributes(gmethod, type);
            }
            return SelectAttributes(r1.Concat(r2).Concat(r3).Concat(r4), type).ToArray();
        }

        /// <summary>
        /// Returns all attributes which were attached to a certain method or 
        /// constructor for a specific instance by attribute injection.
        /// </summary>
        /// <param name="method">method or constructor</param>
        /// <param name="instance">instance on which method or constructor is called</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>injected attributes</returns>
        public static Attribute[] GetInjectedAttributes(this MethodBase method, object instance, Type type)
        {
            List<Attribute> result = _methodInstAttrs.GetOrCreateValue(instance).Get(method.MethodHandle);
            return SelectAttributes(result, type).ToArray();
        }

        /// <summary>
        /// Returns all attributes which were attached to a certain property.
        /// </summary>
        /// <param name="pi">property</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>injected attributes</returns>
        public static Attribute[] GetInjectedAttributes(this PropertyInfo pi, Type type)
        {
            var r1 = SelectAttributes(_propAttrs.GetOrCreateValue(pi), type);
            var r2 = Enumerable.Empty<Attribute>();
            if (pi.DeclaringType.IsGenericType &&
                !pi.DeclaringType.IsGenericTypeDefinition)
            {
                var attrs = pi.DeclaringType.GetGenericTypeDefinition()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => p.Name == pi.Name)
                    .SelectMany(p => _propAttrs.GetOrCreateValue(p));
                r2 = SelectAttributes(attrs, type);
            }
            return r1.Concat(r2).ToArray();
        }

        /// <summary>
        /// Returns all attributes which were attached to a certain method parameter by attribute injection.
        /// </summary>
        /// <param name="pi">method parameter</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>all injected attributed</returns>
        public static Attribute[] GetInjectedAttributes(this ParameterInfo pi, Type type)
        {
            var result = _paramAttrs.GetOrCreateValue(pi);
            return SelectAttributes(result, type).ToArray();
        }

        /// <summary>
        /// Returns all pre-compiled and injected attributes of a certain type.
        /// </summary>
        /// <param name="thisType">type to query for attributes</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled and injected attributes</returns>
        public static Attribute[] GetCustomAndInjectedAttributes(this Type thisType,
            Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(thisType.GetCustomAttributes(true), type);
            IEnumerable<Attribute> result2 = thisType.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <param name="thisType">type to query for attribute</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static Attribute GetCustomOrInjectedAttribute(this Type thisType, Type type)
        {
            return GetCustomAndInjectedAttributes(thisType, type).FirstOrDefault();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="thisType">type to query for attribute</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static T GetCustomOrInjectedAttribute<T>(this Type thisType)
        {
            return (T)(object)GetCustomOrInjectedAttribute(thisType, typeof(T));
        }

        /// <summary>
        /// Returns all pre-compiled or injected attributes of a certain method or constructor.
        /// </summary>
        /// <param name="method">method or constructor to query</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attributes</returns>
        public static Attribute[] GetCustomAndInjectedAttributes(this MethodBase method,
            Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(method, true), type);
            IEnumerable<Attribute> result2 = method.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        /// <summary>
        /// Returns all pre-compiled or injected attributes of a certain method or constructor.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="method">method or constructor to query</param>
        /// <returns>pre-commpiled or injected attributes</returns>
        public static T[] GetCustomAndInjectedAttributes<T>(this MethodBase method)
        {
            return GetCustomAndInjectedAttributes(method, typeof(T)).Cast<T>().ToArray();
        }

        /// <summary>
        /// Returns all pre-compiled or injected attributes of a certain method or constructor
        /// on a specific instance.
        /// </summary>
        /// <param name="method">method or constructor</param>
        /// <param name="instance">instance on which method or constructor is called</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attributes</returns>
        public static Attribute[] GetCustomAndInjectedAttributes(this MethodBase method,
            object instance, Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(method, true), type);
            IEnumerable<Attribute> result2 = method.GetInjectedAttributes(type);
            IEnumerable<Attribute> result3 = instance == null ? Enumerable.Empty<Attribute>() : method.GetInjectedAttributes(instance, type);
            return result1.Union(result2).Union(result3).ToArray();
        }

        /// <summary>
        /// Returns all pre-compiled or injected attributes of a certain field.
        /// </summary>
        /// <param name="field">field</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attributes</returns>
        public static Attribute[] GetCustomAndInjectedAttributes(this FieldInfo field, Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(field, true), type);
            IEnumerable<Attribute> result2 = field.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        /// <summary>
        /// Returns all pre-compiled or injected attributes of a certain field.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="field">field</param>
        /// <returns>pre-compiled or injected attributes</returns>
        public static T[] GetCustomAndInjectedAttributes<T>(this FieldInfo field)
        {
            return GetCustomAndInjectedAttributes(field, typeof(T)).Cast<T>().ToArray();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain method or constructor.
        /// </summary>
        /// <param name="method">method or constructor</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static Attribute GetCustomOrInjectedAttribute(this MethodBase method, Type type)
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(method, type);
            return attrs.FirstOrDefault();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain method or constructor.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="method">method or constructor</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static T GetCustomOrInjectedAttribute<T>(this MethodBase method) where T : Attribute
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(method, typeof(T));
            return (T)attrs.FirstOrDefault();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain method or constructor 
        /// on a specific instance.
        /// </summary>
        /// <param name="method">method or constructor</param>
        /// <param name="instance">instance on which method or constructor is called</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static Attribute GetCustomOrInjectedAttribute(this MethodBase method, object instance, Type type)
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(method, instance, type);
            return attrs.FirstOrDefault();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain field.
        /// </summary>
        /// <param name="field">field</param>
        /// <param name="type">type of attibute to query</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static Attribute GetCustomOrInjectedAttribute(this FieldInfo field, Type type)
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(field, type);
            return attrs.FirstOrDefault();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain field.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="field">field</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static T GetCustomOrInjectedAttribute<T>(this FieldInfo field) where T : Attribute
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(field, typeof(T));
            return (T)attrs.FirstOrDefault();
        }

        /// <summary>
        /// Returns <c>true</c> if the method or constructor has a pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <param name="method">method or constructor</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns><c>true</c> if there is such an attribute, <c>false</c> if not</returns>
        public static bool HasCustomOrInjectedAttribute(this MethodBase method, Type type)
        {
            return GetCustomOrInjectedAttribute(method, type) != null;
        }

        /// <summary>
        /// Returns <c>true</c> if the method or constructor has a pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="method">method or constructor</param>
        /// <returns><c>true</c> if there is such an attribute, <c>false</c> if not</returns>
        public static bool HasCustomOrInjectedAttribute<T>(this MethodBase method)
        {
            return HasCustomOrInjectedAttribute(method, typeof(T));
        }

        /// <summary>
        /// Returns <c>true</c> if the field has a pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <param name="field">field</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns><c>true</c> if there is such an attribute, <c>false</c> if not</returns>
        public static bool HasCustomOrInjectedAttribute(this FieldInfo field, Type type)
        {
            return GetCustomOrInjectedAttribute(field, type) != null;
        }

        /// <summary>
        /// Returns <c>true</c> if the field has a pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="field">field</param>
        /// <returns><c>true</c> if there is such an attribute, <c>false</c> if not</returns>
        public static bool HasCustomOrInjectedAttribute<T>(this FieldInfo field)
        {
            return HasCustomOrInjectedAttribute(field, typeof(T));
        }

        /// <summary>
        /// Returns all pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <param name="pi">property to query</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attributes</returns>
        public static Attribute[] GetCustomAndInjectedAttributes(this PropertyInfo pi, Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(pi, true), type);
            IEnumerable<Attribute> result2 = pi.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <param name="pi">property to query</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled to injected attribute of specified type, or <c>null</c> if no such exists</returns>
        public static Attribute GetCustomOrInjectedAttribute(this PropertyInfo pi, Type type)
        {
            return GetCustomAndInjectedAttributes(pi, type).FirstOrDefault();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="pi">property to query</param>
        /// <returns>pre-compiled to injected attribute of specified type, or <c>null</c> if no such exists</returns>
        public static T GetCustomOrInjectedAttribute<T>(this PropertyInfo pi) where T : Attribute
        {
            return (T)GetCustomOrInjectedAttribute(pi, typeof(T));
        }

        /// <summary>
        /// Returns <c>true</c> if the property has a pre-compiled or injected attribute of a certain type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="pi">property</param>
        /// <returns><c>true</c> if the property has an attribute of specified type, <c>false</c> if not</returns>
        public static bool HasCustomOrInjectedAttribute<T>(this PropertyInfo pi) where T : Attribute
        {
            return pi.GetCustomOrInjectedAttribute<T>() != null;
        }

        /// <summary>
        /// Returns all pre-compiled and injected attributes of the method parameter.
        /// </summary>
        /// <param name="pi">method parameter</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attributes</returns>
        public static Attribute[] GetCustomAndInjectedAttributes(this ParameterInfo pi,
            Type type)
        {
            var result1 = SelectAttributes(pi.GetCustomAttributes(true), type);
            var result2 = GetInjectedAttributes(pi, type);
            return result1.Union(result2).ToArray();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of the method parameter.
        /// </summary>
        /// <param name="pi">method parameter</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static Attribute GetCustomOrInjectedAttribute(this ParameterInfo pi, Type type)
        {
            return GetCustomAndInjectedAttributes(pi, type).FirstOrDefault();
        }

        /// <summary>
        /// Returns a single pre-compiled or injected attribute of the method parameter.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="pi">method parameter</param>
        /// <returns>pre-compiled or injected attribute, <c>null</c> if no such exists</returns>
        public static T GetCustomOrInjectedAttribute<T>(this ParameterInfo pi)
        {
            return (T)(object)GetCustomOrInjectedAttribute(pi, typeof(T));
        }

        /// <summary>
        /// Returns all attributes which were attached to the field by attribute injection.
        /// </summary>
        /// <param name="fi">field</param>
        /// <param name="type">type of attribute to query</param>
        /// <returns>all injected attributes of specified type</returns>
        public static Attribute[] GetInjectedAttributes(this FieldInfo fi, Type type)
        {
            var r1 = SelectAttributes(_fieldAttrs.GetOrCreateValue(fi), type);
            var r2 = Enumerable.Empty<Attribute>();
            if (fi.DeclaringType.IsGenericType &&
                !fi.DeclaringType.IsGenericTypeDefinition)
            {
                var flags = fi.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic;
                if (!fi.IsStatic)
                    flags |= BindingFlags.Instance;
                var fig = fi.DeclaringType.GetGenericTypeDefinition().GetField(fi.Name, flags);
                r2 = SelectAttributes(_fieldAttrs.GetOrCreateValue(fig), type);
            }
            return r1.Concat(r2).ToArray();
        }

        /// <summary>
        /// Returns all attributes with specified type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="obj">attribute container</param>
        /// <returns>all attributes of desired type</returns>
        public static T[] GetAttributes<T>(this IHasAttributes obj) where T : Attribute
        {
            return obj.GetAttributes()
                .Where(a => a is T)
                .Select(a => (T)a)
                .ToArray();
        }

        /// <summary>
        /// Returns a single attribute with specified type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="obj">attribute container</param>
        /// <returns>attribute of desired type, or <c>null</c> if no such exists</returns>
        public static T GetAttribute<T>(this IHasAttributes obj) where T : Attribute
        {
            return GetAttributes<T>(obj).SingleOrDefault();
        }

        /// <summary>
        /// Returns <c>true</c> if the attribute container has an attribute with specified type.
        /// </summary>
        /// <typeparam name="T">type of attribute to query</typeparam>
        /// <param name="obj">attribute container</param>
        /// <returns><c>true</c> if the container has an attribute of specified type, <c>false</c> if not</returns>
        public static bool HasAttribute<T>(this IHasAttributes obj) where T : Attribute
        {
            return GetAttribute<T>(obj) != null;
        }

        static AttributeInjector()
        {
            Inject(typeof(string),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.String), 
                true);

            Inject(typeof(StreamWriter),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.File),
                true);
            Inject(typeof(AsyncVoidMethodBuilder),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType),
                true);

            // bad: gets inherited by any derived class...
            //Inject(typeof(object),
            //    new MapToIntrinsicType(Meta.EIntrinsicTypes.IllegalRuntimeType),
            //    true);

            //Inject(typeof(string).GetMethod("Concat", new Type[] { typeof(object[]) }),
            //    new ConcatRewriter());

            Inject(typeof(StreamReader),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.File),
                true);

            ConcatRewriter crw = new ConcatRewriter();
            foreach (MethodInfo mi in typeof(string).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                InjectOnce(mi, crw, true);
            }

            InjectEach(typeof(Console), "Write",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.Report), true);

            InjectEach(typeof(Console), "WriteLine",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.ReportLine), true);

            InjectEach(typeof(Math), "Abs",
                new MapToUnOp(UnOp.Kind.Abs), true);

            InjectEach(typeof(Math), "Sin",
                new MapToUnOp(UnOp.Kind.Sin), true);

            InjectEach(typeof(Math), "Cos",
                new MapToUnOp(UnOp.Kind.Cos), true);

            InjectEach(typeof(Math), "Sqrt",
                new MapToUnOp(UnOp.Kind.Sqrt), true);

            InjectEach(typeof(Math), "Sign",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.Sign), true);

            InjectEach(typeof(object), "Equals",
                new MapToBinOp(BinOp.Kind.Eq), true);

            InjectEach(typeof(object), "Equals", new SideEffectFree(), true);
            InjectEach(typeof(object), "GetHashCode", new SideEffectFree(), true);

            Inject(typeof(int).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(int), typeof(string)), true);
            Inject(typeof(uint).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(uint), typeof(string)), true);
            Inject(typeof(byte).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(byte), typeof(string)), true);
            Inject(typeof(sbyte).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(sbyte), typeof(string)), true);
            Inject(typeof(char).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(char), typeof(string)), true);
            Inject(typeof(short).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(short), typeof(string)), true);
            Inject(typeof(ushort).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(ushort), typeof(string)), true);
            Inject(typeof(bool).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(bool), typeof(string)), true);
            Inject(typeof(long).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(long), typeof(string)), true);
            Inject(typeof(ulong).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(ulong), typeof(string)), true);
            Inject(typeof(float).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(float), typeof(string)), true);
            Inject(typeof(double).GetMethod("ToString", new Type[0]), new TypeConversion(typeof(double), typeof(string)), true);

            InjectEachCtor(typeof(StreamReader),
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileOpenRead), true);
            InjectEachCtor(typeof(StreamReader), new DoNotCallOnDecompilation(), true);

            InjectEach(typeof(StreamReader), "Close",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileClose), true);
            InjectEach(typeof(StreamReader), "Close", new DoNotCallOnDecompilation(), true);

            InjectEach(typeof(StreamReader), "Read",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileRead), true);
            InjectEach(typeof(StreamReader), "Read", new DoNotCallOnDecompilation(), true);

            InjectEach(typeof(StreamReader), "ReadLine",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileReadLine), true);
            InjectEach(typeof(StreamReader), "ReadLine", new DoNotCallOnDecompilation(), true);

            InjectEachCtor(typeof(StreamWriter),
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileOpenWrite), true);
            InjectEachCtor(typeof(StreamWriter), new DoNotCallOnDecompilation(), true);

            InjectEach(typeof(StreamWriter), "Close",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileClose), true);
            InjectEach(typeof(StreamWriter), "Close", new DoNotCallOnDecompilation(), true);

            InjectEach(typeof(StreamWriter), "Write",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileWrite), true);
            InjectEach(typeof(StreamWriter), "Write", new DoNotCallOnDecompilation(), true);

            InjectEach(typeof(StreamWriter), "WriteLine",
                new MapToIntrinsicFunction(IntrinsicFunction.EAction.FileWriteLine), true);
            InjectEach(typeof(StreamWriter), "WriteLine", new DoNotCallOnDecompilation(), true);

            Inject(typeof(Tuple<,>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.Tuple), true);
            InjectMethodByNameAttr(typeof(Tuple<,>), "get_Item1", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 0), true);
            InjectMethodByNameAttr(typeof(Tuple<,>), "get_Item2", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 1), true);
            InjectToProperty(typeof(Tuple<,>), "Item1", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,>), "Item2", new DependentType(), true);

            Inject(typeof(Tuple<,,>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.Tuple), true);
            InjectMethodByNameAttr(typeof(Tuple<,,>), "get_Item1", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 0), true);
            InjectMethodByNameAttr(typeof(Tuple<,,>), "get_Item2", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 1), true);
            InjectMethodByNameAttr(typeof(Tuple<,,>), "get_Item3", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 2), true);
            InjectToProperty(typeof(Tuple<,,>), "Item1", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,>), "Item2", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,>), "Item3", new DependentType(), true);

            Inject(typeof(Tuple<,,,>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.Tuple), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,>), "get_Item1", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 0), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,>), "get_Item2", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 1), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,>), "get_Item3", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 2), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,>), "get_Item4", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 3), true);
            InjectToProperty(typeof(Tuple<,,,>), "Item1", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,>), "Item2", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,>), "Item3", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,>), "Item4", new DependentType(), true);

            Inject(typeof(Tuple<,,,,>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.Tuple), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,>), "get_Item1", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 0), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,>), "get_Item2", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 1), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,>), "get_Item3", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 2), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,>), "get_Item4", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 3), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,>), "get_Item5", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 4), true);
            InjectToProperty(typeof(Tuple<,,,,>), "Item1", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,>), "Item2", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,>), "Item3", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,>), "Item4", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,>), "Item5", new DependentType(), true);

            Inject(typeof(Tuple<,,,,,>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.Tuple), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,>), "get_Item1", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 0), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,>), "get_Item2", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 1), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,>), "get_Item3", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 2), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,>), "get_Item4", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 3), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,>), "get_Item5", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 4), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,>), "get_Item6", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 5), true);
            InjectToProperty(typeof(Tuple<,,,,,>), "Item1", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,>), "Item2", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,>), "Item3", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,>), "Item4", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,>), "Item5", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,>), "Item6", new DependentType(), true);

            Inject(typeof(Tuple<,,,,,,>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.Tuple), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,>), "get_Item1", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 0), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,>), "get_Item2", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 1), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,>), "get_Item3", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 2), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,>), "get_Item4", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 3), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,>), "get_Item5", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 4), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,>), "get_Item6", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 5), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,>), "get_Item7", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 6), true);
            InjectToProperty(typeof(Tuple<,,,,,,>), "Item1", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,>), "Item2", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,>), "Item3", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,>), "Item4", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,>), "Item5", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,>), "Item6", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,>), "Item7", new DependentType(), true);

            Inject(typeof(Tuple<,,,,,,,>),
                new MapToIntrinsicType(Meta.EIntrinsicTypes.Tuple), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "get_Item1", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 0), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "get_Item2", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 1), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "get_Item3", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 2), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "get_Item4", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 3), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "get_Item5", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 4), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "get_Item6", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 5), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "get_Item7", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 6), true);
            InjectMethodByNameAttr(typeof(Tuple<,,,,,,,>), "Rest", new MapToIntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, 7), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Item1", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Item2", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Item3", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Item4", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Item5", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Item6", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Item7", new DependentType(), true);
            InjectToProperty(typeof(Tuple<,,,,,,,>), "Rest", new DependentType(), true);

            ResolveDelegateCalls.Register();
        }
    }

    /// <summary>
    /// An attribute container, i.e. an object which provides per-instance attributes.
    /// </summary>
    public interface IHasAttributes
    {
        /// <summary>
        /// Returns all attributes of this container.
        /// </summary>
        Attribute[] GetAttributes();
    }
}

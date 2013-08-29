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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SystemSharp.Analysis.C2M;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
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

        public static void Inject(Type type, Attribute attribute, bool retainRef = false)
        {
            var list = _typeAttrs.GetOrCreateValue(type);
            if (!list.Contains(attribute))
                list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(type);
        }

        public static void Inject(MethodBase method, Attribute attribute, bool retainRef = false)
        {
            var list = _methodAttrs.GetOrCreateValue(method);
            list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(method);
        }

        public static void Inject(FieldInfo field, Attribute attribute, bool retainRef = false)
        {
            var list = _fieldAttrs.GetOrCreateValue(field);
            list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(field);
        }

        public static void InjectOnce(MethodBase method, Attribute attribute, bool retainRef = false)
        {
            var list = _methodAttrs.GetOrCreateValue(method);
            if (!list.Contains(attribute))
                list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(method);
        }

        public static void InjectMethodByNameAttr(Type type, string methodName, Attribute attribute, bool retainRef = false)
        {
            var dic = _methodByNameAttrs.GetOrCreateValue(type);
            var list = dic.Get(methodName);
            list.Add(attribute);
            if (retainRef)
                _retainRefList.Add(type);
        }

        public static void InjectOnce(MethodBase method, object instance, Attribute attribute)
        {
            var dic = _methodInstAttrs.GetOrCreateValue(instance);
            var list = dic.Get(method.MethodHandle);
            if (!list.Contains(attribute))
                list.Add(attribute);
        }

        public static void InjectEach(Type type, string methodName, Attribute attribute, bool retainRef = false)
        {
            foreach (MethodBase method in type.GetMethods().Where(x => x.Name == methodName))
            {
                Inject(method, attribute, retainRef);
            }
        }

        public static void InjectEachCtor(Type type, Attribute attribute, bool retainRef = false)
        {
            foreach (MethodBase method in type.GetConstructors())
            {
                Inject(method, attribute, retainRef);
            }
        }

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

        public static void Inject(ParameterInfo pi, Attribute attribute)
        {
            Contract.Requires(pi != null);
            Contract.Requires(attribute != null);

            var list = _paramAttrs.GetOrCreateValue(pi);
            if (!list.Contains(attribute))
                list.Add(attribute);
        }

        public static void Inject(FieldInfo fi, Attribute attribute)
        {
            Contract.Requires(fi != null);
            Contract.Requires(attribute != null);

            _fieldAttrs.GetOrCreateValue(fi).Add(attribute);
        }

        public static IEnumerable<Attribute> SelectAttributes(IEnumerable<object> attrs, Type type)
        {
            return attrs
                .Where(a => type.IsAssignableFrom(a.GetType()))
                .Cast<Attribute>();
        }

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

        public static Attribute[] GetInjectedAttributes(this MethodBase method, object instance, Type type)
        {
            List<Attribute> result = _methodInstAttrs.GetOrCreateValue(instance).Get(method.MethodHandle);
            return SelectAttributes(result, type).ToArray();
        }

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

        public static Attribute[] GetInjectedAttributes(this ParameterInfo pi, Type type)
        {
            var result = _paramAttrs.GetOrCreateValue(pi);
            return SelectAttributes(result, type).ToArray();
        }

        public static Attribute[] GetCustomAndInjectedAttributes(this Type thisType,
            Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(thisType.GetCustomAttributes(true), type);
            IEnumerable<Attribute> result2 = thisType.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        public static Attribute GetCustomOrInjectedAttribute(this Type thisType, Type type)
        {
            return GetCustomAndInjectedAttributes(thisType, type).FirstOrDefault();
        }

        public static T GetCustomOrInjectedAttribute<T>(this Type thisType)
        {
            return (T)(object)GetCustomOrInjectedAttribute(thisType, typeof(T));
        }

        public static Attribute[] GetCustomAndInjectedAttributes(this MethodBase method,
            Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(method, true), type);
            IEnumerable<Attribute> result2 = method.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        public static T[] GetCustomAndInjectedAttributes<T>(this MethodBase method)
        {
            return GetCustomAndInjectedAttributes(method, typeof(T)).Cast<T>().ToArray();
        }

        public static Attribute[] GetCustomAndInjectedAttributes(this MethodBase method,
            object instance, Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(method, true), type);
            IEnumerable<Attribute> result2 = method.GetInjectedAttributes(type);
            IEnumerable<Attribute> result3 = instance == null ? Enumerable.Empty<Attribute>() : method.GetInjectedAttributes(instance, type);
            return result1.Union(result2).Union(result3).ToArray();
        }

        public static Attribute[] GetCustomAndInjectedAttributes(this FieldInfo field, Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(field, true), type);
            IEnumerable<Attribute> result2 = field.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        public static T[] GetCustomAndInjectedAttributes<T>(this FieldInfo field)
        {
            return GetCustomAndInjectedAttributes(field, typeof(T)).Cast<T>().ToArray();
        }

        public static Attribute GetCustomOrInjectedAttribute(this MethodBase method, Type type)
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(method, type);
            return attrs.FirstOrDefault();
        }

        public static T GetCustomOrInjectedAttribute<T>(this MethodBase method) where T : Attribute
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(method, typeof(T));
            return (T)attrs.FirstOrDefault();
        }

        public static Attribute GetCustomOrInjectedAttribute(this MethodBase method, object instance, Type type)
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(method, instance, type);
            return attrs.FirstOrDefault();
        }

        public static Attribute GetCustomOrInjectedAttribute(this FieldInfo field, Type type)
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(field, type);
            return attrs.FirstOrDefault();
        }

        public static T GetCustomOrInjectedAttribute<T>(this FieldInfo field) where T : Attribute
        {
            Attribute[] attrs = GetCustomAndInjectedAttributes(field, typeof(T));
            return (T)attrs.FirstOrDefault();
        }

        public static bool HasCustomOrInjectedAttribute(this MethodBase method, Type type)
        {
            return GetCustomOrInjectedAttribute(method, type) != null;
        }

        public static bool HasCustomOrInjectedAttribute<T>(this MethodBase method)
        {
            return HasCustomOrInjectedAttribute(method, typeof(T));
        }

        public static bool HasCustomOrInjectedAttribute(this FieldInfo field, Type type)
        {
            return GetCustomOrInjectedAttribute(field, type) != null;
        }

        public static bool HasCustomOrInjectedAttribute<T>(this FieldInfo field)
        {
            return HasCustomOrInjectedAttribute(field, typeof(T));
        }

        public static Attribute[] GetCustomAndInjectedAttributes(this PropertyInfo pi, Type type)
        {
            IEnumerable<Attribute> result1 = SelectAttributes(Attribute.GetCustomAttributes(pi, true), type);
            IEnumerable<Attribute> result2 = pi.GetInjectedAttributes(type);
            return result1.Union(result2).ToArray();
        }

        public static Attribute GetCustomOrInjectedAttribute(this PropertyInfo pi, Type type)
        {
            return GetCustomAndInjectedAttributes(pi, type).FirstOrDefault();
        }

        public static T GetCustomOrInjectedAttribute<T>(this PropertyInfo pi) where T : Attribute
        {
            return (T)GetCustomOrInjectedAttribute(pi, typeof(T));
        }

        public static bool HasCustomOrInjectedAttribute<T>(this PropertyInfo pi) where T : Attribute
        {
            return pi.GetCustomOrInjectedAttribute<T>() != null;
        }

        public static Attribute[] GetCustomAndInjectedAttributes(this ParameterInfo pi,
            Type type)
        {
            var result1 = SelectAttributes(pi.GetCustomAttributes(true), type);
            var result2 = GetInjectedAttributes(pi, type);
            return result1.Union(result2).ToArray();
        }

        public static Attribute GetCustomOrInjectedAttribute(this ParameterInfo pi, Type type)
        {
            return GetCustomAndInjectedAttributes(pi, type).FirstOrDefault();
        }

        public static T GetCustomOrInjectedAttribute<T>(this ParameterInfo pi)
        {
            return (T)(object)GetCustomOrInjectedAttribute(pi, typeof(T));
        }

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

        public static T[] GetAttributes<T>(this IHasAttributes obj) where T : Attribute
        {
            return obj.GetAttributes()
                .Where(a => a is T)
                .Select(a => (T)a)
                .ToArray();
        }

        public static T GetAttribute<T>(this IHasAttributes obj) where T : Attribute
        {
            return GetAttributes<T>(obj).SingleOrDefault();
        }

        public static bool HasAttribute<T>(this IHasAttributes obj) where T : Attribute
        {
            return GetAttribute<T>(obj) != default(T);
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

    public interface IHasAttributes
    {
        Attribute[] GetAttributes();
    }
}

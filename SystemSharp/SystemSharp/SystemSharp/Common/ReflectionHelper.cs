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
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics.Contracts;

namespace SystemSharp.Common
{
    public static class ReflectionHelper
    {
        /// <summary>
        /// This extension method returns the argument types of a given method.
        /// </summary>
        /// <remarks>
        /// If the method is non-static, this includes the type of the "this" argument which is transferred implicitly.
        /// The "this" argument type is then the first element of the returned array.
        /// </remarks>
        /// <param name="mb">The method to query</param>
        /// <returns>An array with the argument types of the method</returns>
        public static Type[] GetArgTypes(this MethodBase mb)
        {
            List<Type> result = new List<Type>();
            if (!mb.IsStatic)
            {
                result.Add(mb.DeclaringType);
            }
            result.AddRange(from ParameterInfo pi in mb.GetParameters()
                            select pi.ParameterType);
            return result.ToArray();
        }

        /// <summary>
        /// This extension method invokes a given method with a given argument list. If the method is non-static, the "this" argument is expected to be the first value within the args parameter.
        /// </summary>
        /// <param name="mb">the method to be invoked</param>
        /// <param name="args">the argument list</param>
        /// <returns>the return value of the invoked method, null if the method does not return any value.</returns>
        public static object Invoke(this MethodBase mb, params object[] args)
        {
            object result;
            if (mb.IsStatic || mb is ConstructorInfo)
            {
                result = mb.ConvertArgumentsAndInvoke(null, args);
            }
            else
            {
                object[] paramArgs = args.Skip(1).ToArray();
                result = mb.ConvertArgumentsAndInvoke(args[0], paramArgs);
                Array.Copy(paramArgs, 0, args, 1, paramArgs.Length);
            }
            return result;
        }

        /// <summary>
        /// Returns true if the method is a function, meaning it returns some value.
        /// </summary>
        /// <param name="mb">the method</param>
        /// <param name="returnType">receives the type of the method return value</param>
        /// <returns>true if the method returns some non-void value, false if not</returns>
        public static bool IsFunction(this MethodBase mb, out Type returnType)
        {
            if (mb is MethodInfo)
                returnType = ((MethodInfo)mb).ReturnType;
            else
                returnType = typeof(void);
            return !returnType.Equals(typeof(void));
        }

        /// <summary>
        /// Returns true if specified method is either a method with non-void return type or a constructor.
        /// </summary>
        /// <param name="returnType">type returned by method or class type instantiated by constructor, respectively</param>
        /// <exception cref="NotImplementedException">if <paramref name="mb"/> is neither method nor constructor
        /// (FIXME: can this actually happen?)</exception>
        public static bool IsFunctionOrCtor(this MethodBase mb, out Type returnType)
        {
            if (mb is MethodInfo)
                returnType = ((MethodInfo)mb).ReturnType;
            else if (mb is ConstructorInfo)
                returnType = mb.DeclaringType;
            else
                throw new NotImplementedException();
            return !returnType.Equals(typeof(void));
        }

        /// <summary>
        /// Returns true if specified method is either a method with non-void return type or a constructor.
        /// </summary>
        /// <param name="returnType">type returned by method or class type instantiated by constructor, respectively</param>
        public static bool ReturnsSomething(this MethodBase mb, out Type returnType)
        {
            if (mb is MethodInfo)
                returnType = ((MethodInfo)mb).ReturnType;
            else if (mb is ConstructorInfo)
                returnType = mb.DeclaringType;
            else
                returnType = typeof(void);
            return !returnType.Equals(typeof(void));
        }

        /// <summary>
        /// Tests whether a given type is enumerable. A type is said to be enumerable if it is either an integral primitive type 
        /// (including bool) or an enumeration type. Floating-point types are not enumerable.
        /// </summary>
        /// <param name="type">The type to test</param>
        /// <returns>true if the type is enumerable, false if not</returns>
        public static bool IsEnumerable(this Type type)
        {
            if (type.IsEnum)
                return true;

            if (type.IsPrimitive)
            {
                if (type.Equals(typeof(float)) ||
                    type.Equals(typeof(double)))
                    return false;
                else
                    return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Returns the chain of method base definitions, i.e. methods being overwritten by specified method.
        /// </summary>
        public static IEnumerable<MethodInfo> GetAncestorDefinitions(this MethodInfo method)
        {
            MethodInfo basemethod = method;
            do
            {
                method = basemethod;
                yield return method;
                basemethod = method.GetBaseDefinition();
            } while (!basemethod.Equals(method));
        }

        /// <summary>
        /// Returns the chain of method base definitions, i.e. methods being overwritten by specified method.
        /// </summary>
        public static IEnumerable<MethodBase> GetAncestorDefinitions(this MethodBase method)
        {
            if (method is MethodInfo)
                return GetAncestorDefinitions((MethodInfo)method);
            else
                return Enumerable.Repeat(method, 1);
        }

        /// <summary>
        /// Returns all base types and their respective type parameters of specified type.
        /// </summary>
        public static IEnumerable<Type> GetBaseTypeChain(this Type type)
        {
            Type oldType;
            do
            {
                yield return type;
                if (type.IsGenericType && !type.IsGenericTypeDefinition)
                    yield return type.GetGenericTypeDefinition();
                oldType = type;
                type = type.BaseType;
            } while (type != null && type != oldType);
        }

        /// <summary>
        /// Finds an implementation of a possibly abstract or interface method an a specific instance.
        /// </summary>
        /// <param name="method">Any method, may be abstract or an interface method</param>
        /// <param name="instance">A concrete object</param>
        /// <returns>A method which is defined on instance and implements the given method. null if no such method exists.</returns>
        public static MethodInfo FindImplementation(this MethodInfo method, object instance)
        {
            Contract.Requires<ArgumentNullException>(method != null, "method");
            Contract.Requires<ArgumentNullException>(instance != null, "instance");

            List<Type> argTypes = new List<Type>();
            argTypes.AddRange(
                method.GetParameters()
                .Select(p => p.ParameterType));
            argTypes.Add(method.ReturnType);
            Type delegType = System.Linq.Expressions.Expression.GetDelegateType(
                argTypes.ToArray());
            Delegate deleg = Delegate.CreateDelegate(delegType, instance, method, false);
            if (deleg == null)
                return null;
            return deleg.Method;
        }

        private class MethodEqualityComparerImpl : IEqualityComparer<MethodBase>
        {
            #region IEqualityComparer<RuntimeMethodHandle> Member

            public bool Equals(MethodBase x, MethodBase y)
            {
                return x.MethodHandle == y.MethodHandle;
            }

            public int GetHashCode(MethodBase obj)
            {
                return obj.MethodHandle.Value.ToInt64().GetHashCode();
            }

            #endregion
        }

        private class TypeEqualityComparerImpl : IEqualityComparer<Type>
        {
            #region IEqualityComparer<RuntimeTypeHandle> Member

            public bool Equals(Type x, Type y)
            {
                return x.TypeHandle.Equals(y.TypeHandle);
            }

            public int GetHashCode(Type obj)
            {
                return obj.TypeHandle.Value.ToInt64().GetHashCode();
            }

            #endregion
        }

        private class FieldEqualityComparerImpl : IEqualityComparer<FieldInfo>
        {
            #region IEqualityComparer<RuntimeTypeHandle> Member

            public bool Equals(FieldInfo x, FieldInfo y)
            {
                return x.FieldHandle == y.FieldHandle;
            }

            public int GetHashCode(FieldInfo obj)
            {
                return obj.FieldHandle.Value.ToInt64().GetHashCode();
            }

            #endregion
        }

        /// <summary>
        /// An equality comparer which defines methods to be the same if their method handles or equal.
        /// </summary>
        public static readonly IEqualityComparer<MethodBase> MethodEqualityComparer = 
            new MethodEqualityComparerImpl();

        /// <summary>
        /// An equality comparer which defines types to be the same if their type handles or equal.
        /// </summary>
        public static readonly IEqualityComparer<Type> TypeEqualityComparer =
            new TypeEqualityComparerImpl();

        /// <summary>
        /// An equality comparer which defines fields to be the same if their field handles or equal.
        /// </summary>
        public static readonly IEqualityComparer<FieldInfo> FieldEqualityComparer =
            new FieldEqualityComparerImpl();
    }
}

/**
 * Copyright 2014 Christian Köllner
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
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Common;

namespace SystemSharp.DataTypes.Meta
{
    [Flags]
    public enum EAlgebraicTypeProperties
    {
        None = 0,
        AdditionIsCommutative = 0x1,
        MultiplicationIsCommutative = 0x2
    }

    public interface IGenericTypeTraits<T>
    {
        bool IsZero(T value);
        bool IsOne(T value);
        bool IsMinusOne(T value);
        EAlgebraicTypeProperties Properties { get; }
    }

    public interface IGenericTypeTraitsProvider
    {
        IGenericTypeTraits<T> GetGenericTypeTraits<T>();
    }

    public interface ITypeInstanceTraits<T>
    {
        bool IsEmpty { get; }
        bool OneExists { get; }
        T GetZero();
        T GetOne();
    }

    public interface IHasTypeInstanceTraits<T>
    {
        ITypeInstanceTraits<T> InstanceTraits { get; }
    }

    public interface INonParamatricTypeTraits<T> :
        IGenericTypeTraits<T>,
        ITypeInstanceTraits<T>
    {
    }

    public static class GenericTypeTraits<T>
    {
        private static IGenericTypeTraits<T> _instance;
        public static IGenericTypeTraits<T> Instance 
        {
            get
            {
                if (_instance == null)
                    _instance = StdTypeTraits.Get<T>();
                return _instance;
            }
        }

        public static void Register(IGenericTypeTraits<T> traits)
        {
            _instance = traits;
        }
    }

    public static class TypeTraits
    {
        public static IGenericTypeTraits<T> GetGenericTraits<T>()
        {
            return GenericTypeTraits<T>.Instance;
        }

        public static ITypeInstanceTraits<T> GetInstanceTraits<T>(T instance)
        {
            var provider = instance as IHasTypeInstanceTraits<T>;
            if (provider != null)
                return provider.InstanceTraits;
            return StdTypeTraits.Get<T>();
        }
    }
}

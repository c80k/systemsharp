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
using System.Runtime.CompilerServices;
using System.Text;

namespace SystemSharp.Collections
{
    /// <summary>
    /// This static class provides extension methods which turn dictionaries into multi-maps, i.e. mapping keys to multiple values.
    /// </summary>
    public static class MultiMaps
    {
        /// <summary>
        /// Queries the multi-map for a given key and creates an new empty list if key doesn't exist.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value list</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <returns>value list associated with key</returns>
        public static List<TValue> Get<TKey, TValue>(this IDictionary<TKey, List<TValue>> map, TKey key)
        {
            List<TValue> list;
            if (!map.TryGetValue(key, out list))
            {
                list = new List<TValue>();
                map[key] = list;
            }
            return list;
        }

        /// <summary>
        /// Adds a single value to the multi-map.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value list</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <param name="value">value to add</param>
        public static void Add<TKey, TValue>(this IDictionary<TKey, List<TValue>> map, TKey key, TValue value)
        {
            List<TValue> list = Get(map, key);
            list.Add(value);
        }

        /// <summary>
        /// Adds a single value to the multi-map, but only if that value is not yet contained in the value list.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value list</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <param name="value">value to add</param>
        public static void AddIfNew<TKey, TValue>(this IDictionary<TKey, List<TValue>> map, TKey key, TValue value)
        {
            List<TValue> list = Get(map, key);
            if (!list.Contains(value))
                list.Add(value);
        }

        /// <summary>
        /// Queries the multi-map for a given key and creates an new empty set if key doesn't exist.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value set</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <returns>value set associated with key</returns>
        public static ISet<TValue> Get<TKey, TValue>(this IDictionary<TKey, ISet<TValue>> map, TKey key)
        {
            ISet<TValue> set;
            if (!map.TryGetValue(key, out set))
            {
                set = new HashSet<TValue>();
                map[key] = set;
            }
            return set;
        }

        /// <summary>
        /// Adds a single value to the multi-map.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value set</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <param name="value">value to add</param>
        /// <returns>true if the value was added, false if it was already contained</returns>
        public static bool Add<TKey, TValue>(this IDictionary<TKey, ISet<TValue>> map, TKey key, TValue value)
        {
            ISet<TValue> set = Get(map, key);
            return set.Add(value);
        }

        /// <summary>
        /// Queries the multi-map for a given key and creates an new empty set if key doesn't exist.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value set</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <returns>value set associated with key</returns>
        public static HashSet<TValue> Get<TKey, TValue>(this IDictionary<TKey, HashSet<TValue>> map, TKey key)
        {
            HashSet<TValue> set;
            if (!map.TryGetValue(key, out set))
            {
                set = new HashSet<TValue>();
                map[key] = set;
            }
            return set;
        }

        /// <summary>
        /// Adds a single value to the multi-map.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value set</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <param name="value">value to add</param>
        /// <returns>true if the value was added, false if it was already contained</returns>
        public static bool Add<TKey, TValue>(this IDictionary<TKey, HashSet<TValue>> map, TKey key, TValue value)
        {
            HashSet<TValue> set = Get(map, key);
            return set.Add(value);
        }

        /// <summary>
        /// Queries the multi-map for a given key and creates an new empty sorted set if key doesn't exist.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value set</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <returns>value set associated with key</returns>
        public static SortedSet<TValue> Get<TKey, TValue>(this IDictionary<TKey, SortedSet<TValue>> map, TKey key)
        {
            SortedSet<TValue> set;
            if (!map.TryGetValue(key, out set))
            {
                set = new SortedSet<TValue>();
                map[key] = set;
            }
            return set;
        }

        /// <summary>
        /// Adds a single value to the multi-map.
        /// </summary>
        /// <typeparam name="TKey">type of key</typeparam>
        /// <typeparam name="TValue">type of single value inside value set</typeparam>
        /// <param name="map">a multi-map</param>
        /// <param name="key">a key</param>
        /// <param name="value">value to add</param>
        /// <returns>true if the value was added, false if it was already contained</returns>
        public static bool Add<TKey, TValue>(this IDictionary<TKey, SortedSet<TValue>> map, TKey key, TValue value)
        {
            SortedSet<TValue> set = Get(map, key);
            return set.Add(value);
        }

        /// <summary>
        /// Queries the array-based multi-map for a given key and creates an new empty sorted set if key doesn't exist.
        /// </summary>
        /// <typeparam name="TValue">type of single value inside value list</typeparam>
        /// <param name="map">a array-based multi-map</param>
        /// <param name="key">a key</param>
        /// <returns>value set associated with key</returns>
        public static ISet<TValue> Get<TValue>(this ISet<TValue>[] map, int key)
        {
            ISet<TValue> set = map[key];
            if (set == null)
            {
                set = new HashSet<TValue>();
                map[key] = set;
            }
            return set;
        }

        /// <summary>
        /// Adds a single value to the array-based multi-map.
        /// </summary>
        /// <typeparam name="TValue">type of single value inside value set</typeparam>
        /// <param name="map">an array-based multi-map</param>
        /// <param name="key">a key</param>
        /// <param name="value">value to add</param>
        /// <returns>true if the value was added, false if it was already contained</returns>
        public static bool Add<TValue>(this ISet<TValue>[] map, int key, TValue value)
        {
            ISet<TValue> set = Get(map, key);
            return set.Add(value);
        }
    }
}

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

namespace SystemSharp.Collections
{
    /// <summary>
    /// A cache dictionary essentially behaves like a dictionary but will automatically create a new value whenever a key is not found.
    /// </summary>
    /// <typeparam name="TKey">type of key</typeparam>
    /// <typeparam name="TValue">type of value</typeparam>
    public class CacheDictionary<TKey, TValue>:
        IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private Dictionary<TKey, TValue> _map;
        private Func<TKey, TValue> _creator;

        private event Action<TKey, TValue> _onItemAdded;

        /// <summary>
        /// Triggered whenever a new key/value-pair was created
        /// </summary>
        public event Action<TKey, TValue> OnItemAdded
        {
            add { _onItemAdded += value; }
            remove { _onItemAdded -= value; }
        }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="creator">functor which creates a new value for non-existent key</param>
        public CacheDictionary(Func<TKey, TValue> creator)
        {
            _map = new Dictionary<TKey, TValue>();
            _creator = creator;
        }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="comparer">equality comparer for keys</param>
        /// <param name="creator">functor which creates a new value for non-existent key</param>
        public CacheDictionary(IEqualityComparer<TKey> comparer, Func<TKey, TValue> creator)
        {
            _map = new Dictionary<TKey, TValue>(comparer);
            _creator = creator;
        }

        /// <summary>
        /// Ensures that the key is present in the dictionary, possibly by creating a new value for it.
        /// </summary>
        /// <param name="key">key</param>
        public void Cache(TKey key)
        {
            var dummy = this[key];
        }

        /// <summary>
        /// Queries whether given key is already present in the dictionary
        /// </summary>
        /// <param name="key">a key</param>
        /// <returns>whether key is present</returns>
        public bool IsCached(TKey key)
        {
            return _map.ContainsKey(key);
        }

        /// <summary>
        /// Looks for a given key and either returns its cached value or creates and caches a new value for it.
        /// </summary>
        /// <param name="key">key to lookup</param>
        /// <returns>corresponding value</returns>
        public TValue this[TKey key]
        {
            get
            {
                TValue result;
                if (!_map.TryGetValue(key, out result))
                {
                    result = _creator(key);
                    _map[key] = result;
                    if (_onItemAdded != null)
                        _onItemAdded(key, result);
                }
                return result;
            }
        }

        /// <summary>
        /// Returns all cached keys
        /// </summary>
        public ICollection<TKey> Keys
        {
            get { return _map.Keys; }
        }

        /// <summary>
        /// Returns all cached values
        /// </summary>
        public ICollection<TValue> Values
        {
            get { return _map.Values; }
        }

        /// <summary>
        /// Clears the dictionary
        /// </summary>
        public void Clear()
        {
            _map.Clear();
        }

        #region IEnumerable<KeyValuePair<TKey,TValue>> Member

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _map.GetEnumerator();
        }

        #endregion

        #region IEnumerable Member

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _map.GetEnumerator();
        }

        #endregion
    }
}

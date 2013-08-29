/**
 * Copyright 2011 Christian Köllner
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
    public class CacheDictionary<TKey, TValue>:
        IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private Dictionary<TKey, TValue> _map;
        private Func<TKey, TValue> _creator;

        private event Action<TKey, TValue> _onItemAdded;
        public event Action<TKey, TValue> OnItemAdded
        {
            add { _onItemAdded += value; }
            remove { _onItemAdded -= value; }
        }

        public CacheDictionary(Func<TKey, TValue> creator)
        {
            _map = new Dictionary<TKey, TValue>();
            _creator = creator;
        }

        public CacheDictionary(IEqualityComparer<TKey> comparer, Func<TKey, TValue> creator)
        {
            _map = new Dictionary<TKey, TValue>(comparer);
            _creator = creator;
        }

        public void Cache(TKey key)
        {
            var dummy = this[key];
        }

        public bool IsCached(TKey key)
        {
            return _map.ContainsKey(key);
        }

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

        public ICollection<TKey> Keys
        {
            get { return _map.Keys; }
        }

        public ICollection<TValue> Values
        {
            get { return _map.Values; }
        }

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

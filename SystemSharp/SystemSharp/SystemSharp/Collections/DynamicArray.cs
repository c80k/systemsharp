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
    /// Syntactic sugar which emulates a dynamic array, i.e. a data structure where we can set an element at an arbitrary position.
    /// They structure automatically grows when an index beyond its current size is accessed.
    /// </summary>
    /// <typeparam name="T">element type</typeparam>
    public class DynamicArray<T>: IList<T>
    {
        private List<T> _items = new List<T>();

        /// <summary>
        /// Gets or sets element at given index
        /// </summary>
        /// <param name="index">index (&gt;= 0)</param>
        /// <returns>element at specified index</returns>
        public T this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentException("Index less than 0");

                if (index >= _items.Count)
                    return default(T);

                return _items[index];
            }
            set
            {
                if (index < 0)
                    throw new ArgumentException("Index less than 0");

                while (index >= _items.Count)
                {
                    _items.Add(default(T));
                }

                _items[index] = value;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throw new InvalidOperationException();
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        public void Add(T item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _items.Count; }
        }

        /// <summary>
        /// Returns false
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Throws an exception, removing elements is not allowed!
        /// </summary>
        public bool Remove(T item)
        {
            throw new InvalidOperationException();
        }
    }
}

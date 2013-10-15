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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace SystemSharp.Collections
{
    /// <summary>
    /// A priority queue implementation which resolves collisions (i.e. multiple elements for same key) using
    /// a user-defined resolution function.
    /// </summary>
    /// <typeparam name="T">datatype of priority queue element</typeparam>
    public class PriorityQueue<T>
    {
        /// <summary>
        /// Resolution function delegate
        /// </summary>
        /// <remarks>
        /// Depending on your use case, there are multiple possibilities of implementing a resolution function. The most simple
        /// and stupid option is to either keep the existing element (return <paramref name="v1"/>) or to replace the existing
        /// element by the new one (return <paramref name="v2"/>). A more advanced approach is to specify some collection or
        /// set type, e.g. a HashSet, and to construct the set-union of both parameters.
        /// </remarks>
        /// <param name="v1">an element</param>
        /// <param name="v2">another element</param>
        /// <returns>some element representing the union of both elements</returns>
        public delegate T ResolveFunc(T v1, T v2);

        private class PQItem
        {
            public long Key { get; set; }
            public T Value { get; set; }
            public PQItem[] Children { get; private set; }

            public PQItem(long key, T value)
            {
                Key = key;
                Value = value;
                Children = new PQItem[2];
            }
        }

        /// <summary>
        /// Resolution function which is called in case of an element being inserted for an existing key.
        /// It is mandatory to set this property to a non-null value when there is a possibility of key collisions.
        /// </summary>
        public ResolveFunc Resolve { get; set; }

        private PQItem _root;

        private void Enqueue(PQItem item, PQItem root)
        {
            Contract.Requires<ArgumentNullException>(item != null);
            Contract.Requires<ArgumentNullException>(root != null);

            if (item.Key == root.Key)
            {
                root.Value = Resolve(root.Value, item.Value);
            }
            else if (item.Key < root.Key)
            {
                if (root.Children[0] == null)
                    root.Children[0] = item;
                else
                    Enqueue(item, root.Children[0]);
            }
            else
            {
                if (root.Children[1] == null)
                    root.Children[1] = item;
                else
                    Enqueue(item, root.Children[1]);
            }
        }

        /// <summary>
        /// Inserts element <paramref name="value"/> for specified <paramref name="key"/>. Lower key means higher priority.
        /// </summary>
        /// <param name="key">key for which to insert the value</param>
        /// <param name="value">element to insert</param>
        public void Enqueue(long key, T value)
        {
            PQItem pqi = new PriorityQueue<T>.PQItem(key, value);
            if (_root == null)
                _root = pqi;
            else
                Enqueue(pqi, _root);
        }

        /// <summary>
        /// Removes and returns the element with highest priority, i.e. lowest key.
        /// </summary>
        public KeyValuePair<long, T> Dequeue()
        {
            if (_root == null)
                throw new InvalidOperationException("priority queue is empty!");

            PQItem cur = _root;
            PQItem parent = null;
            while (cur.Children[0] != null)
            {
                parent = cur;
                cur = cur.Children[0];
            }

            if (parent == null)
            {
                _root = cur.Children[1];
            }
            else
            {
                parent.Children[0] = cur.Children[1];
            }

            return new KeyValuePair<long, T>(cur.Key, cur.Value);
        }

        /// <summary>
        /// Returns the element with highest priority, i.e. lowest key, but does not remove it.
        /// </summary>
        public KeyValuePair<long, T> Peek()
        {
            if (_root == null)
                throw new InvalidOperationException("priority queue is empty!");

            PQItem cur = _root;
            PQItem parent = null;
            while (cur.Children[0] != null)
            {
                parent = cur;
                cur = cur.Children[0];
            }

            return new KeyValuePair<long, T>(cur.Key, cur.Value);
        }

        /// <summary>
        /// Whether queue contains any element.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return _root == null ? true : false;
            }
        }
    }
}

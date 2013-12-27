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
using System.Diagnostics;
using System.Linq;
using System.Text;
using SystemSharp.Collections.EmilStefanov;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.SetAlgorithms
{
    /// <summary>
    /// Adapter interface for set algorithms.
    /// </summary>
    /// <typeparam name="T">type of a set element</typeparam>
    public interface ISetAdapter<T>
    {
        /// <summary>
        /// Returns a property map which maps any element to a unique 0-based index.
        /// </summary>
        IPropMap<T, int> Index { get; }
    }

    /// <summary>
    /// An implementation of the union-find data structure.
    /// </summary>
    /// <typeparam name="T">type of set element</typeparam>
    public class UnionFind<T>
    {
        private IPropMap<T, int> _index;
        private List<T> _elems;
        private DisjointSets _impl;
        private int[] _repShuffle; // Used to pick the "right" representant for 
                                   // Havlak's loop analysis

        /// <summary>
        /// Constructs an instance of the union-find data structure.
        /// </summary>
        /// <param name="a">set adapter</param>
        /// <param name="elems">The list set elements. It is assumed that the list index of each set element
        /// matched the index returned by the set adapter.</param>
        public UnionFind(ISetAdapter<T> a, IList<T> elems)
        {
            _index = a.Index;
            _elems = new List<T>(elems);
            _repShuffle = new int[elems.Count];
            for (int i = 0; i < _elems.Count; i++)
            {
                Debug.Assert(_index[_elems[i]] == i);
                _repShuffle[i] = i;
            }
            _impl = new DisjointSets(elems.Count);
        }

        /// <summary>
        /// Finds the representant of a set element.
        /// </summary>
        /// <param name="elem">set element</param>
        /// <returns>representant</returns>
        public T Find(T elem)
        {
            int fidx = _impl.FindSet(_index[elem]);
            return _elems[_repShuffle[fidx]];
        }

        /// <summary>
        /// Unifies two set elements.
        /// </summary>
        /// <param name="e1">a set element</param>
        /// <param name="e2">another set element</param>
        public void Union(T e1, T e2)
        {
            _impl.Union(_impl.FindSet(_index[e1]), _impl.FindSet(_index[e2]));
            _repShuffle[_impl.FindSet(_index[e2])] = _index[e2];
        }

        /// <summary>
        /// Represents this data structure as a lookup, providing the representant of each element.
        /// </summary>
        public ILookup<T, T> ToLookup()
        {
            return _elems.ToLookup(e => Find(e));
        }
    }

    public static class SetOperations
    {
        /// <summary>
        /// Creates a union-find data structure.
        /// </summary>
        /// <typeparam name="T">type of a set element</typeparam>
        /// <param name="a">set adapter</param>
        /// <param name="elems">The list of set elements. It is assumed that the list index of each set element
        /// matched the index returned by the set adapter.</param>
        public static UnionFind<T> CreateUnionFind<T>(this ISetAdapter<T> a, IList<T> elems)
        {
            return new UnionFind<T>(a, elems);
        }
    }
}

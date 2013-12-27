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
using System.Linq;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.SetAlgorithms;

namespace SystemSharp.TreeAlgorithms
{
    /// <summary>
    /// Describes the access priviliges for property maps.
    /// </summary>
    public enum EAccess
    {
        NoAccess,
        ReadOnly,
        WriteOnly,
        ReadWrite
    }

    /// <summary>
    /// Property map interface.
    /// </summary>
    /// <typeparam name="TThis">type of accessed element</typeparam>
    /// <typeparam name="TValue">type of accessed property</typeparam>
    public interface IPropMap<TThis, TValue>
    {
        /// <summary>
        /// Gets or sets an element property.
        /// </summary>
        /// <param name="elem">element whose property is accessed</param>
        /// <returns>property value</returns>
        TValue this[TThis elem] { get; set; }

        /// <summary>
        /// Returns the access privilege for this property map.
        /// </summary>
        EAccess Access { get; }
    }

    /// <summary>
    /// Node types of Havlak's loop analysis algorithm.
    /// </summary>
    public enum ENodeType
    {
        Nonheader,
        Reducible,
        Self,
        Irreducible
    }

    /// <summary>
    /// Adapter interface to tree analysis algorithms.
    /// </summary>
    /// <typeparam name="T">type of a vertex</typeparam>
    public interface IGraphAdapter<T>
    {
        /// <summary>
        /// Returns a property map on the parent relation.
        /// </summary>
        IPropMap<T, T> Parent { get; }

        /// <summary>
        /// Returns a property map on the successor relation.
        /// </summary>
        IPropMap<T, T[]> Succs { get; }

        /// <summary>
        /// Returns a property map on the predecessor relation.
        /// </summary>
        IPropMap<T, T[]> Preds { get; }
        
        /// <summary>
        /// Returns a property map on temporary list storage.
        /// </summary>
        IPropMap<T, List<T>> TempList { get; }

        /// <summary>
        /// Returns a property map on the DFS pre-order index.
        /// </summary>
        IPropMap<T, int> PreOrderIndex { get; }

        /// <summary>
        /// Returns a property map on the DFS pre-order last index, as defined by Havlak's loop analysis algorithm.
        /// </summary>
        IPropMap<T, T> PreOrderLast { get; }

        /// <summary>
        /// Returns a property map on the DFS post-order index.
        /// </summary>
        IPropMap<T, int> PostOrderIndex { get; }

        /// <summary>
        /// Returns a property map on the node type, as defined by Havlak's loop analysis algorithm.
        /// </summary>
        IPropMap<T, ENodeType> Type { get; }

        /// <summary>
        /// Returns a property map on the backPreds relation, as defined by Havlak's loop analysis algorithm.
        /// </summary>
        IPropMap<T, HashSet<T>> BackPreds { get; }

        /// <summary>
        /// Returns a property map on the nonBackPreds relation, as defined by Havlak's loop analysis algorithm.
        /// </summary>
        IPropMap<T, HashSet<T>> NonBackPreds { get; }

        /// <summary>
        /// Returns a property map on the redBackIn relation, as defined by Havlak's loop analysis algorithm.
        /// </summary>
        IPropMap<T, HashSet<T>> RedBackIn { get; }

        /// <summary>
        /// Returns a property map on the otherIn relation, as defined by Havlak's loop analysis algorithm.
        /// </summary>
        IPropMap<T, HashSet<T>> OtherIn { get; }

        /// <summary>
        /// Returns a property map on the loop header relation.
        /// </summary>
        IPropMap<T, T> Header { get; }

        /// <summary>
        /// Returns a property map on the immediate dominator.
        /// </summary>
        IPropMap<T, T> IDom { get; }

        /// <summary>
        /// Returns a property map on the immediate dominatees.
        /// </summary>
        IPropMap<T, T[]> IDoms { get; }
    }

    public delegate TValue GetterFunc<TThis, TValue>(TThis elem);
    public delegate void SetterFunc<TThis, TValue>(TThis elem, TValue value);

    /// <summary>
    /// An <c>IPropMap&lt;&gt;</c> implementation using delegates.
    /// </summary>
    /// <typeparam name="TThis">type of element whose property is to be accessed</typeparam>
    /// <typeparam name="TValue">type of the property value which is accessed</typeparam>
    public class DelegatePropMap<TThis, TValue> :
        IPropMap<TThis, TValue>
    {
        private GetterFunc<TThis, TValue> _get;
        private SetterFunc<TThis, TValue> _set;

        /// <summary>
        /// Constructs a new instance capable of reading and writing property values.
        /// </summary>
        /// <param name="get">property getter delegate</param>
        /// <param name="set">property setter delegate</param>
        public DelegatePropMap(GetterFunc<TThis, TValue> get, SetterFunc<TThis, TValue> set)
        {
            _get = get;
            _set = set;
        }

        /// <summary>
        /// Constructs a new instance capable of reading property values.
        /// </summary>
        /// <param name="get">property getter delegate</param>
        public DelegatePropMap(GetterFunc<TThis, TValue> get)
        {
            _get = get;
        }

        /// <summary>
        /// Constructs a new instance capable of writing property values.
        /// </summary>
        /// <param name="set">property setter delegate</param>
        public DelegatePropMap(SetterFunc<TThis, TValue> set)
        {
            _set = set;
        }

        #region IPropMap<TThis,TValue> Members

        public TValue this[TThis elem]
        {
            get
            {
                if (_get == null)
                    throw new AccessViolationException("Reading the property is not allowed");
                return _get(elem);
            }
            set
            {
                if (_set == null)
                    throw new AccessViolationException("Writing the property is not allowed");
                _set(elem, value);
            }
        }

        #endregion

        public EAccess Access
        {
            get
            {
                if (_get == null && _set == null)
                    return EAccess.NoAccess;
                else if (_get == null)
                    return EAccess.WriteOnly;
                else if (_set == null)
                    return EAccess.ReadOnly;
                else
                    return EAccess.ReadWrite;
            }
        }
    }

    /// <summary>
    /// A hash table-based <c>IPropMap&lt;&gt;</c> implementation.
    /// </summary>
    /// <typeparam name="TThis">type of element whose property is to be accessed</typeparam>
    /// <typeparam name="TValue">type of the property value which is accessed</typeparam>
    public class HashBasedPropMap<TThis, TValue> :
        IPropMap<TThis, TValue>
    {
        private Dictionary<TThis, TValue> _map = new Dictionary<TThis, TValue>();

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public HashBasedPropMap()
        {
            Access = EAccess.ReadWrite;
        }

        public TValue this[TThis elem]
        {
            get
            {
                TValue result = default(TValue);
                _map.TryGetValue(elem, out result);
                return result;
            }
            set
            {
                _map[elem] = value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the underlying dictionary contains the given key.
        /// </summary>
        public bool ContainsKey(TThis key)
        {
            return _map.ContainsKey(key);
        }

        /// <summary>
        /// Enumerates all present keys of the underlying dictionary.
        /// </summary>
        public IEnumerable<TThis> Keys
        {
            get { return _map.Keys; }
        }

        /// <summary>
        /// Enumerates all present values of the underlying dictionary.
        /// </summary>
        public IEnumerable<TValue> Values
        {
            get { return _map.Values; }
        }

        /// <summary>
        /// Returns the count of present key-value pairs.
        /// </summary>
        public int Count
        {
            get { return _map.Count; }
        }

        /// <summary>
        /// Gets or sets the access privilege to the property map.
        /// </summary>
        public EAccess Access { get; set; }
    }

    /// <summary>
    /// A CacheDictionary&lt;&gt;-based <c>IPropMap&lt;&gt;</c> implementation.
    /// </summary>
    /// <typeparam name="TThis">type of element whose property is to be accessed</typeparam>
    /// <typeparam name="TValue">type of the property value which is accessed</typeparam>
    public class CacheBasedPropMap<TThis, TValue> :
        IPropMap<TThis, TValue>
    {
        private CacheDictionary<TThis, TValue> _map;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="creator">property value creator</param>
        public CacheBasedPropMap(Func<TThis, TValue> creator)
        {
            _map = new CacheDictionary<TThis, TValue>(creator);
        }

        public TValue this[TThis elem]
        {
            get
            {
                return _map[elem];
            }
            set
            {
                throw new AccessViolationException("Writing to CacheBasedPropMap is not allowed");
            }
        }

        /// <summary>
        /// Enumerates all present keys of this property map.
        /// </summary>
        public IEnumerable<TThis> Keys
        {
            get { return _map.Keys; }
        }

        /// <summary>
        /// Enumerates all present values of this property map.
        /// </summary>
        public IEnumerable<TValue> Values
        {
            get { return _map.Values; }
        }

        /// <summary>
        /// Returns always <c>EAccess.ReadOnly</c>.
        /// </summary>
        public EAccess Access
        {
            get { return EAccess.ReadOnly; }
        }
    }

    /// <summary>
    /// An array-based <c>IPropMap&lt;&gt;</c> implementation.
    /// </summary>
    /// <typeparam name="TKey">type of element whose property is to be accessed</typeparam>
    /// <typeparam name="TValue">type of the property value which is accessed</typeparam>
    public class ArrayBackedPropMap<TKey, TValue> :
        IPropMap<TKey, TValue>
    {
        private TValue[] _back;
        private Func<TKey, int> _indexFunc;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="back">array for storing property values</param>
        /// <param name="indexFunc">index function, mapping keys to array indices</param>
        /// <param name="access">desired access privilege</param>
        public ArrayBackedPropMap(TValue[] back, Func<TKey, int> indexFunc, EAccess access = EAccess.ReadWrite)
        {
            _back = back;
            _indexFunc = indexFunc;
            Access = access;
        }

        public TValue this[TKey elem]
        {
            get
            {
                if (Access == EAccess.WriteOnly || Access == EAccess.NoAccess)
                    throw new AccessViolationException("Read access not allowed");
                return _back[_indexFunc(elem)];
            }
            set
            {
                if (Access == EAccess.ReadOnly || Access == EAccess.NoAccess)
                    throw new AccessViolationException("Write access not allowed");
                _back[_indexFunc(elem)] = value;
            }
        }

        public EAccess Access { get; private set; }
    }

    /// <summary>
    /// This static class provides helper methods for creating property maps.
    /// </summary>
    public static class PropMaps
    {
        /// <summary>
        /// Creates a property map which is backed by an array.
        /// </summary>
        /// <typeparam name="TKey">type of element whose property is to be accessed</typeparam>
        /// <typeparam name="TValue">type of the property value which is accessed</typeparam>
        /// <param name="array">array for storing property values</param>
        /// <param name="indexFunc">index function, mapping keys to array indices</param>
        /// <param name="access">desired access privilege</param>
        public static IPropMap<TKey, TValue> CreateForArray<TKey, TValue>(
            TValue[] array, Func<TKey, int> indexFunc, EAccess access = EAccess.ReadWrite)
        {
            return new ArrayBackedPropMap<TKey, TValue>(array, indexFunc, access);
        }

        /// <summary>
        /// Creates a property map which is backed by an array.
        /// </summary>
        /// <typeparam name="TValue">type of element whose property is to be accessed</typeparam>
        /// <param name="array">array for storing property values</param>
        /// <param name="access">desired access privilege</param>
        public static IPropMap<int, TValue> CreateForArray<TValue>(TValue[] array, EAccess access)
        {
            return new ArrayBackedPropMap<int, TValue>(array, i => i, access);
        }
    }

    /// <summary>
    /// A default <c>IGraphAdapter&lt;&gt;</c> implementation, based on delegates.
    /// </summary>
    /// <typeparam name="T">type of vertex</typeparam>
    public class DefaultTreeAdapter<T> :
        IGraphAdapter<T>
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="getParent">node parent getter delegate</param>
        /// <param name="getChildren">node children getter delegate</param>
        /// <param name="setChildren">node children setter delegate</param>
        /// <param name="getPreds">node predecessors getter delegate</param>
        /// <param name="setPreds">node predecessors setter delegate</param>
        /// <param name="getTempList">node temporary list storage getter delegate</param>
        /// <param name="setTempList">node temporary list storage getter delegate</param>
        /// <param name="getPreOrderIndex">node pre-order index getter delegate</param>
        /// <param name="setPreOrderIndex">node pre-order index setter delegate</param>
        /// <param name="getPreOrderLast">node pre-order last index getter delegate</param>
        /// <param name="setPreOrderLast">node pre-order last index setter delegate</param>
        /// <param name="getPostOrderIndex">node post-order index getter delegate</param>
        /// <param name="setPostOrderIndex">node post-order index setter delegate</param>
        /// <param name="getType">node type getter delegate</param>
        /// <param name="setType">node type setter delegate</param>
        /// <param name="getBackPreds">node backPreds getter delegate</param>
        /// <param name="setBackPreds">node backPreds setter delegate</param>
        /// <param name="getNonBackPreds">node nonBackPreds getter delegate</param>
        /// <param name="setNonBackPreds">node nonBackPreds setter delegate</param>
        /// <param name="getRedBackIn">node redBackIn getter delegate</param>
        /// <param name="setRedBackIn">node redBackIn setter delegate</param>
        /// <param name="getOtherIn">node otherIn getter delegate</param>
        /// <param name="setOtherIn">node otherIn setter delegate</param>
        /// <param name="getHeader">node loop header getter delegate</param>
        /// <param name="setHeader">node loop header setter delegate</param>
        /// <param name="getIDom">node immediate dominator getter delegate</param>
        /// <param name="setIDom">node immediate dominator setter delegate</param>
        /// <param name="getIDoms">node immediate dominatees getter delegate</param>
        /// <param name="setIDoms">node immediate dominatees setter delegate</param>
        public DefaultTreeAdapter(GetterFunc<T,T> getParent,
            GetterFunc<T,T[]> getChildren,
            SetterFunc<T,T[]> setChildren,
            GetterFunc<T, T[]> getPreds,
            SetterFunc<T, T[]> setPreds,
            GetterFunc<T,List<T>> getTempList,
            SetterFunc<T,List<T>> setTempList,
            GetterFunc<T,int> getPreOrderIndex,
            SetterFunc<T,int> setPreOrderIndex,
            GetterFunc<T,T> getPreOrderLast,
            SetterFunc<T,T> setPreOrderLast,
            GetterFunc<T, int> getPostOrderIndex,
            SetterFunc<T, int> setPostOrderIndex,
            GetterFunc<T, ENodeType> getType,
            SetterFunc<T,ENodeType> setType,
            GetterFunc<T,HashSet<T>> getBackPreds,
            SetterFunc<T,HashSet<T>> setBackPreds,
            GetterFunc<T,HashSet<T>> getNonBackPreds,
            SetterFunc<T,HashSet<T>> setNonBackPreds,
            GetterFunc<T, HashSet<T>> getRedBackIn,
            SetterFunc<T, HashSet<T>> setRedBackIn,
            GetterFunc<T, HashSet<T>> getOtherIn,
            SetterFunc<T, HashSet<T>> setOtherIn,
            GetterFunc<T, T> getHeader,
            SetterFunc<T,T> setHeader,
            GetterFunc<T,T> getIDom,
            SetterFunc<T,T> setIDom,
            GetterFunc<T,T[]> getIDoms,
            SetterFunc<T,T[]> setIDoms
            )
        {
            Parent = new DelegatePropMap<T, T>(getParent);
            Succs = new DelegatePropMap<T, T[]>(getChildren, setChildren);
            Preds = new DelegatePropMap<T, T[]>(getPreds, setPreds);
            TempList = new DelegatePropMap<T, List<T>>(getTempList, setTempList);
            PreOrderIndex = new DelegatePropMap<T, int>(getPreOrderIndex, setPreOrderIndex);
            PreOrderLast = new DelegatePropMap<T, T>(getPreOrderLast, setPreOrderLast);
            PostOrderIndex = new DelegatePropMap<T, int>(getPostOrderIndex, setPostOrderIndex);
            Type = new DelegatePropMap<T, ENodeType>(getType, setType);
            BackPreds = new DelegatePropMap<T, HashSet<T>>(getBackPreds, setBackPreds);
            NonBackPreds = new DelegatePropMap<T, HashSet<T>>(getNonBackPreds, setNonBackPreds);
            RedBackIn = new DelegatePropMap<T, HashSet<T>>(getRedBackIn, setRedBackIn);
            OtherIn = new DelegatePropMap<T, HashSet<T>>(getOtherIn, setOtherIn);
            Header = new DelegatePropMap<T, T>(getHeader, setHeader);
            IDom = new DelegatePropMap<T, T>(getIDom, setIDom);
            IDoms = new DelegatePropMap<T, T[]>(getIDoms, setIDoms);
        }

        #region ITreeAdapter<T> Members

        public IPropMap<T, T> Parent { get; private set; }
        public IPropMap<T, T[]> Succs { get; private set; }
        public IPropMap<T, T[]> Preds { get; private set; }
        public IPropMap<T, List<T>> TempList { get; private set; }
        public IPropMap<T, int> PreOrderIndex { get; private set; }
        public IPropMap<T, T> PreOrderLast { get; private set; }
        public IPropMap<T, int> PostOrderIndex { get; private set; }
        public IPropMap<T, ENodeType> Type { get; private set; }
        public IPropMap<T, HashSet<T>> BackPreds { get; private set; }
        public IPropMap<T, HashSet<T>> NonBackPreds { get; private set; }
        public IPropMap<T, HashSet<T>> RedBackIn { get; private set; }
        public IPropMap<T, HashSet<T>> OtherIn { get; private set; }
        public IPropMap<T, T> Header { get; private set; }
        public IPropMap<T, T> IDom { get; private set; }
        //public IPropMap<T, T> IPDom { get; private set; }
        public IPropMap<T, T[]> IDoms { get; private set; }
        //public IPropMap<T, T[]> IPDoms { get; private set; }
        public IPropMap<T, int> IDomPreOrderIndex { get; private set; }
        public IPropMap<T, T> IDomPreOrderLast { get; private set; }

        #endregion
    }

    /// <summary>
    /// A has map-based <c>IGraphAdapter&lt;&gt;</c> implementation.
    /// </summary>
    /// <typeparam name="T">type of vertex</typeparam>
    public class HashingGraphAdapter<T> :
        IGraphAdapter<T>
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public HashingGraphAdapter()
        {
            Parent = new HashBasedPropMap<T, T>();
            Succs = new HashBasedPropMap<T, T[]>();
            Preds = new HashBasedPropMap<T, T[]>();
            TempList = new HashBasedPropMap<T, List<T>>();
            PreOrderIndex = new HashBasedPropMap<T, int>();
            PreOrderLast = new HashBasedPropMap<T, T>();
            PostOrderIndex = new HashBasedPropMap<T, int>();
            Type = new HashBasedPropMap<T, ENodeType>();
            BackPreds = new HashBasedPropMap<T, HashSet<T>>();
            NonBackPreds = new HashBasedPropMap<T, HashSet<T>>();
            RedBackIn = new HashBasedPropMap<T, HashSet<T>>();
            OtherIn = new HashBasedPropMap<T, HashSet<T>>();
            Header = new HashBasedPropMap<T, T>();
            IDom = new HashBasedPropMap<T, T>();
            IPDom = new HashBasedPropMap<T, T>();
            IDoms = new HashBasedPropMap<T, T[]>();
            IPDoms = new HashBasedPropMap<T, T[]>();
        }

        public IPropMap<T, T> Parent { get; private set; }
        public IPropMap<T, T[]> Succs  { get; private set; }
        public IPropMap<T, T[]> Preds  { get; private set; }
        public IPropMap<T, List<T>> TempList  { get; private set; }
        public IPropMap<T, int> PreOrderIndex  { get; private set; }
        public IPropMap<T, T> PreOrderLast  { get; private set; }
        public IPropMap<T, int> PostOrderIndex  { get; private set; }
        public IPropMap<T, ENodeType> Type  { get; private set; }
        public IPropMap<T, HashSet<T>> BackPreds  { get; private set; }
        public IPropMap<T, HashSet<T>> NonBackPreds  { get; private set; }
        public IPropMap<T, HashSet<T>> RedBackIn { get; private set; }
        public IPropMap<T, HashSet<T>> OtherIn { get; private set; }
        public IPropMap<T, T> Header { get; private set; }
        public IPropMap<T, T> IDom  { get; private set; }
        public IPropMap<T, T> IPDom  { get; private set; }
        public IPropMap<T, T[]> IDoms  { get; private set; }
        public IPropMap<T, T[]> IPDoms  { get; private set; }
    }

    /// <summary>
    /// This static class provides extension methods to simplify the usage of property maps.
    /// </summary>
    public static class TreeOperations
    {
        private class PreOrderSetAdapter<T> :
            ISetAdapter<T>
        {
            public PreOrderSetAdapter(IGraphAdapter<T> a)
            {
                Contract.Requires<ArgumentNullException>(a != null);

                Index = a.PreOrderIndex;
            }

            #region ISetAdapter<T> Members

            public IPropMap<T, int> Index { get; private set; }

            #endregion
        }

        /// <summary>
        /// Checks whether the property map grants the requested access privilege and throws an exception if it doesn't.
        /// </summary>
        /// <param name="access">required access privilege</param>
        /// <exception cref="InvalidOperationException">if requested access privilege is not supported</exception>
        public static void RequireAccess<TThis, TValue>(this IPropMap<TThis, TValue> pmap, EAccess access)
        {
            bool granted;
            switch (pmap.Access)
            {
                case EAccess.NoAccess:
                    granted = access == EAccess.NoAccess;
                    break;

                case EAccess.ReadOnly:
                    granted = access == EAccess.NoAccess || access == EAccess.ReadOnly;
                    break;

                case EAccess.WriteOnly:
                    granted = access == EAccess.NoAccess || access == EAccess.WriteOnly;
                    break;

                case EAccess.ReadWrite:
                    granted = true;
                    break;

                default:
                    throw new NotImplementedException();
            }
            if (!granted)
            {
                throw new InvalidOperationException("Need " + access.ToString() + 
                    " access to a property map, but got only " + pmap.Access.ToString() + " access");
            }
        }

        /// <summary>
        /// Sets all specified keys of the property map to the specified reset value.
        /// </summary>
        /// <param name="pmap">the property map</param>
        /// <param name="nodes">the elements whose property values are to be reset</param>
        /// <param name="value">reset value</param>
        public static void Reset<TThis, TValue>(this IPropMap<TThis, TValue> pmap,
            IEnumerable<TThis> nodes, TValue value)
        {
            pmap.RequireAccess(EAccess.WriteOnly);
            foreach (TThis node in nodes)
            {
                pmap[node] = value;
            }
        }

        /// <summary>
        /// Sets all specified keys of the property map to <c>default(TValue)</c>.
        /// </summary>
        /// <param name="pmap">the property map</param>
        /// <param name="nodes">the elements whose property values are to be reset</param>
        public static void Clear<TThis, TValue>(this IPropMap<TThis, TValue> pmap,
            IEnumerable<TThis> nodes)
        {
            pmap.Reset(nodes, default(TValue));
        }

        /// <summary>
        /// Creates a new list for each temporary list storage property.
        /// </summary>
        /// <param name="a">the graph adapter</param>
        /// <param name="nodes">enumeration of nodes whose temporary list storage properties are to be initialized</param>
        public static void CreateDefaultTempStorage<T>(this IGraphAdapter<T> a, IEnumerable<T> nodes)
        {
            a.TempList.RequireAccess(EAccess.WriteOnly);
            foreach (T node in nodes)
            {
                a.TempList[node] = new List<T>();
            }
        }

        /// <summary>
        /// Inverts a property map-based relation.
        /// </summary>
        /// <param name="a">graph adapter providing temporary list storage</param>
        /// <param name="srcrel">source property map</param>
        /// <param name="dstrel">destination property map for inverted relation</param>
        /// <param name="nodes">nodes to be considered for the inversion</param>
        public static void InvertRelation<T>(this IGraphAdapter<T> a, 
            IPropMap<T,T> srcrel, IPropMap<T,T[]> dstrel, IEnumerable<T> nodes)
        {
            a.CreateDefaultTempStorage(nodes);
            srcrel.RequireAccess(EAccess.ReadOnly);
            dstrel.RequireAccess(EAccess.WriteOnly);
            foreach (T node in nodes)
            {
                if (srcrel[node] != null)
                {
                    a.TempList[srcrel[node]].Add(node);
                }
            }
            foreach (T node in nodes)
            {
                dstrel[node] = a.TempList[node].ToArray();
            }
            a.TempList.Clear(nodes);
        }

        /// <summary>
        /// Inverts a property map-based relation.
        /// </summary>
        /// <param name="a">graph adapter providing temporary list storage</param>
        /// <param name="srcrel">source property map</param>
        /// <param name="dstrel">destination property map for inverted relation</param>
        /// <param name="nodes">nodes to be considered for the inversion</param>
        public static void InvertRelation<T>(this IGraphAdapter<T> a,
            IPropMap<T, T[]> srcrel, IPropMap<T, T[]> dstrel, IEnumerable<T> nodes)
        {
            a.CreateDefaultTempStorage(nodes);
            srcrel.RequireAccess(EAccess.ReadOnly);
            dstrel.RequireAccess(EAccess.WriteOnly);
            foreach (T node in nodes)
            {
                foreach (T adj in srcrel[node])
                {
                    a.TempList[adj].Add(node);
                }
            }
            foreach (T node in nodes)
            {
                dstrel[node] = a.TempList[node].ToArray();
            }
            a.TempList.Clear(nodes);
        }

        /// <summary>
        /// Inverts a parent relation, taking the successor relation as target.
        /// </summary>
        /// <param name="a">graph adapter providing the relations</param>
        /// <param name="nodes">nodes to be considered</param>
        public static void ComputeSuccessorsFromParent<T>(this IGraphAdapter<T> a, IEnumerable<T> nodes)
        {
            a.InvertRelation(a.Parent, a.Succs, nodes);
        }

        /// <summary>
        /// Inverts a successor relation, taking the predecessor relation as target.
        /// </summary>
        /// <param name="a">graph adapter providing the relations</param>
        /// <param name="nodes">nodes to be considered</param>
        public static void ComputePredecessorsFromSuccessors<T>(this IGraphAdapter<T> a, IEnumerable<T> nodes)
        {
            a.InvertRelation(a.Succs, a.Preds, nodes);
        }

        private static void PreOrderDFS<T>(this IGraphAdapter<T> a, 
            T[] result, T node, ref int index)
        {
            Contract.Requires<ArgumentNullException>(a != null);
            Contract.Requires<ArgumentNullException>(result != null);
            Contract.Requires<ArgumentException>(index >= 0);
            Contract.Requires<ArgumentException>(index < result.Length);

            result[index] = node;
            a.PreOrderIndex[node] = index++;
            foreach (T child in a.Succs[node])
            {
                if (a.PreOrderIndex[child] < 0)
                    a.PreOrderDFS(result, child, ref index);
            }
            a.PreOrderLast[node] = result[index - 1];
        }

        /// <summary>
        /// Performs a depth-first search on the successor relation, updating the pre-order index
        /// and pre-order last index relations of the graph adapter.
        /// </summary>
        /// <param name="a">graph adapter</param>
        /// <param name="nodes">nodes to be considered</param>
        /// <param name="start">start node</param>
        /// <returns>all visited nodes in pre-order</returns>
        public static T[] GetPreOrder<T>(this IGraphAdapter<T> a,
            IList<T> nodes, T start)
        {
            a.PreOrderIndex.Reset(nodes, -1);
            T[] result = new T[nodes.Count];
            int index = 0;
            a.PreOrderDFS(result, start, ref index);
            // if some nodes are unreachable, the last elements of the result
            // array are null. These should be eliminated:
            result = result.Where(x => x != null).ToArray();
            return result;
        }

        /// <summary>
        /// Performs a depth-first search on the successor relation, updating the pre-order index
        /// and pre-order last index relations of the graph adapter. All nodes without parent are
        /// taken as start nodes.
        /// </summary>
        /// <param name="a">graph adapter</param>
        /// <param name="nodes">nodes to be considered</param>
        /// <returns>all visited nodes in pre-order</returns>
        public static T[] GetPreOrder<T>(this IGraphAdapter<T> a,
            IList<T> nodes)
        {
            a.PreOrderIndex.Reset(nodes, -1);
            T[] result = new T[nodes.Count];            
            int index = 0;
            IEnumerable<T> starts = nodes.Where(x => a.Parent[x] == null);
            foreach (T start in starts)
            {
                a.PreOrderDFS(result, start, ref index);
            }
            // if some nodes are unreachable, the last elements of the result
            // array are null. These should be eliminated:
            result = result.Where(x => x != null).ToArray();
            return result;
        }

        private static void PostOrderDFS<T>(
            IPropMap<T, T[]> succs, IPropMap<T, int> indexMap,
            Stack<T> result, T node, ref int index)
        {
            Contract.Requires<ArgumentNullException>(succs != null);
            Contract.Requires<ArgumentNullException>(indexMap != null);
            Contract.Requires<ArgumentNullException>(result != null);

            if (indexMap[node] < 0)
            {
                indexMap[node] = index++;
                foreach (T succ in succs[node])
                {
                    PostOrderDFS(succs, indexMap, result, succ, ref index);
                }
            }
            result.Push(node);
        }

        /// <summary>
        /// Performs a depth-first search on the graph and retrieves a post-order sorting.
        /// </summary>
        /// <param name="succrel">successor relation</param>
        /// <param name="indexMap">index map for receiving the post order indices</param>
        /// <param name="nodes">nodes to consider</param>
        /// <param name="start">start node</param>
        /// <param name="reverse">whether a reverse post-order sorting is desired</param>
        /// <returns>all visited nodes in post-order sorting or inverse post-order sorting</returns>
        public static T[] GetPostOrder<T>(IPropMap<T, T[]> succrel, 
            IPropMap<T, int> indexMap,
            IList<T> nodes, T start, bool reverse)
        {
            indexMap.Reset(nodes, -1);
            Stack<T> result = new Stack<T>();
            int index = 0;
            PostOrderDFS(succrel, indexMap, result, start, ref index);
            indexMap.Reset(nodes, -1);
            List<T> order = new List<T>(nodes.Count);
            index = 0;
            foreach (T node in result)
            {
                if (indexMap[node] < 0)
                {
                    order.Add(node);
                    indexMap[node] = index++;
                }
            }
            if (reverse)
                order.Reverse();
            return order.ToArray();
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="w"/> is an ancestor of <c>v</c>.
        /// </summary>
        /// <param name="indexrel">pre-order index relation</param>
        /// <param name="lastrel">pre-order last index relation</param>
        public static bool IsAncestor<T>(
            IPropMap<T, int> indexrel, IPropMap<T, T> lastrel,
            T w, T v)
        {
            return indexrel[w] <= indexrel[v] &&
                indexrel[v] <= indexrel[lastrel[w]];
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="w"/> is an ancestor of <c>v</c>.
        /// </summary>
        public static bool IsAncestor<T>(this IGraphAdapter<T> a, T w, T v)
        {
            return IsAncestor(a.PreOrderIndex, a.PreOrderLast, w, v);
        }

        /// <summary>
        /// Determines the loop nesting structure of the given (control-flow) graph.
        /// </summary>
        /// <remarks>
        /// This is an implementation of Paul Havlak's loop analysis algorithm.
        /// Reference: ACM Transactions on Programming Languages and Systems, Vol. 19, No. 4, July 1997, Pages 557-567.
        /// </remarks>
        /// <param name="a">graph adapter</param>
        /// <param name="nodes">nodes to consider</param>
        /// <param name="start">entry node</param>
        public static void AnalyzeLoops<T>(this IGraphAdapter<T> a,
            IList<T> nodes, T start)
        {
            T[] order = a.GetPreOrder(nodes, start);

            // fix_loops part
#if false
            foreach (T w in order)
            {
                a.RedBackIn[w] = new HashSet<T>();
                a.OtherIn[w] = new HashSet<T>();
                foreach (T v in a.Preds[w])
                {
                    if (a.PreOrderIndex[w] < a.PreOrderIndex[v])
                        a.RedBackIn[w].Add(v);
                    else
                        a.OtherIn[w].Add(v);
                }
                if (a.RedBackIn[w].Count > 0 && a.OtherIn[w].Count > 1)
                {
                    throw new NotImplementedException();
                }
            }
#endif

            foreach (T w in order/*nodes*/)
            {
                a.BackPreds[w] = new HashSet<T>();
                a.NonBackPreds[w] = new HashSet<T>();
                a.Header[w] = start;
                a.Type[w] = ENodeType.Nonheader;
                foreach (T v in a.Preds[w])
                {
                    if (a.IsAncestor(w, v))
                        a.BackPreds[w].Add(v);
                    else
                        a.NonBackPreds[w].Add(v);
                }
            }
            a.Header[start] = default(T);
            HashSet<T> P = new HashSet<T>();
            UnionFind<T> uf = new PreOrderSetAdapter<T>(a).CreateUnionFind(order);
            for (int iw = order.Length - 1; iw >= 0; iw--)
            {
                T w = order[iw];
                P.Clear();
                foreach (T v in a.BackPreds[w])
                {
                    if (!v.Equals(w))
                        P.Add(uf.Find(v));
                    else
                        a.Type[w] = ENodeType.Self;
                }
                Queue<T> worklist = new Queue<T>(P);
                if (P.Count > 0)
                    a.Type[w] = ENodeType.Reducible;
                while (worklist.Count > 0)
                {
                    T x = worklist.Dequeue();
                    foreach (T y in a.NonBackPreds[x])
                    {
                        T y_ = uf.Find(y);
                        if (!a.IsAncestor(w, y_))
                        {
                            a.Type[w] = ENodeType.Irreducible;
                            a.NonBackPreds[w].Add(y_);
                        }
                        else if (!P.Contains(y_) && !w.Equals(y_))
                        {
                            P.Add(y_);
                            worklist.Enqueue(y_);
                        }
                    }
                }
                foreach (T x in P)
                {
                    a.Header[x] = w;
                    uf.Union(x, w);
                }
            }
        }

        /// <summary>
        /// Computes the immediate dominators of the specified graph nodes.
        /// </summary>
        /// <remarks>
        /// This is an algorithm from Cooper, Harvey and Kennedy for the computation 
        /// of immediate dominators in a control flow graph.
        /// Reference:
        /// <para>
        /// SOFTWARE—PRACTICE AND EXPERIENCE 2001; 4:1–10
        /// </para><para>
        /// Keith D. Cooper, Timothy J. Harvey and Ken Kennedy
        /// </para><para>
        /// A Simple, Fast Dominance Algorithm
        /// </para>
        /// </remarks>
        /// <param name="succrel">successor relation</param>
        /// <param name="predrel">predecessor relation</param>
        /// <param name="index">node index map</param>
        /// <param name="dom">immediate dominators relation</param>
        /// <param name="nodes">nodes to consider</param>
        /// <param name="start">start node</param>
        public static void ComputeImmediateDominators<T>(
            IPropMap<T, T[]> succrel, IPropMap<T, T[]> predrel,
            IPropMap<T, int> index,
            IPropMap<T, T> dom,
            IList<T> nodes, T start) where T: class
        {
            T[] order = GetPostOrder(succrel, index, nodes, start, true);
            dom[start] = start;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (T node in order)
                {
                    if (node.Equals(start))
                        continue;

                    T newIdom = null;
                    T[] preds = predrel[node];
                    foreach (T p in preds)
                    {
                        if (dom[p] != null)
                        {
                            if (newIdom == null)
                                newIdom = p;
                            else
                                newIdom = Intersect(dom, index, p, newIdom);
                        }
                    }
                    if (dom[node] != newIdom)
                    {
                        dom[node] = newIdom;
                        changed = true;
                    }
                }
            }
        }

        /** This is part of the dmonance algorithm from Cooper, Harvey and Kennedy
         * */
        private static T Intersect<T>(
            IPropMap<T, T> dom, IPropMap<T, int> index,
            T b1, T b2) where T: class
        {
            T finger1 = b1;
            T finger2 = b2;
            while (finger1 != finger2)
            {
                while (index[finger1] > index[finger2])
                    finger1 = dom[finger1];
                while (index[finger2] > index[finger1])
                    finger2 = dom[finger2];
            }
            return finger1;
        }

        /// <summary>
        /// Computes a the immediate dominators of all specified graph nodes.
        /// </summary>
        /// <param name="a">graph adapter</param>
        /// <param name="nodes">nodes to consider</param>
        /// <param name="entry">entry node</param>
        public static void ComputeImmediateDominators<T>(this IGraphAdapter<T> a,
            IList<T> nodes, T entry) where T: class
        {
            ComputeImmediateDominators(a.Succs, a.Preds, a.PostOrderIndex,
                a.IDom, nodes, entry);
            a.IDom[entry] = null;
            a.InvertRelation(a.IDom, a.IDoms, nodes);
            a.IDom[entry] = entry;
        }

        /*public static void ComputeImmediatePostDominators<T>(this IGraphAdapter<T> a,
            IList<T> nodes, T exit) where T : class
        {
            if (a.Preds[exit].Length == 0)
            { 
                // special case: exit code is not reachable.
                // This can happen due to infinite loops which do not return from the 
                // current method.

                // no fix yet...
            }

            ComputeImmediateDominators(a.Preds, a.Succs, a.PostOrderIndex,
                a.IPDom, nodes, exit);
            a.IPDom[exit] = null;
            a.InvertRelation(a.IPDom, a.IPDoms, nodes);
            a.IPDom[exit] = exit;
        }*/

        /// <summary>
        /// Computes the lowest common ancestor tree of any set of given nodes.
        /// </summary>
        /// <typeparam name="T">The node type</typeparam>
        /// <param name="nodes">The query set of nodes from which the LCA tree should be computed (>= 2 elements)</param>
        /// <param name="prel">The parent relation: assigns each node to its parent</param>
        /// <param name="root">The root node</param>
        /// <param name="rporel">The index relation: assigns each node to its postorder index</param>
        /// <param name="lprel">The LCA relation (output): assigns each node of the query set to its LCA parent</param>
        public static void GetLCATree<T>(IEnumerable<T> nodes, IPropMap<T, T> prel, T root, IPropMap<T, int> porel, 
            IPropMap<T, T> lprel)
        {
            PriorityQueue<Tuple<T, T>> pq = new PriorityQueue<Tuple<T, T>>();
            pq.Resolve = (x, y) =>
            {
                if (!object.Equals(x.Item1, x.Item2))
                    lprel[x.Item1] = x.Item2;
                if (!object.Equals(y.Item1, x.Item2))
                    lprel[y.Item1] = x.Item2;
                return Tuple.Create(x.Item2, x.Item2);
            };
            foreach (T node in nodes)
            {
                pq.Enqueue(-porel[node], Tuple.Create(node, node));
            }
            while (!pq.IsEmpty)
            {
                var kvp = pq.Dequeue();
                if (object.Equals(kvp.Value.Item2, root))
                    break;
                var par = prel[kvp.Value.Item2];
                var next = Tuple.Create(kvp.Value.Item1, par);
                pq.Enqueue(-porel[next.Item2], next);
            }
        }
    }

    /// <summary>
    /// This static class provides an implementation of Dijkstra's shortest path algorithm.
    /// </summary>
    public static class Dijkstra
    {
        /// <summary>
        /// Finds the shortest paths from a specified entry node to all other nodes.
        /// </summary>
        /// <typeparam name="T">node type</typeparam>
        /// <param name="nodes">nodes to consider</param>
        /// <param name="start">start node</param>
        /// <param name="succs">successor relation</param>
        /// <param name="rootDist">relation for storing the distance from each node to the start node</param>
        /// <param name="pred">predecessor relation</param>
        /// <param name="dist">adjacent node distance relation</param>
        public static void FindShortestPaths<T>(IEnumerable<T> nodes, T start,
            IPropMap<T, T[]> succs, IPropMap<T, int> rootDist, IPropMap<T, T> pred,
            IPropMap<Tuple<T, T>, int> dist)
        {
            foreach (T node in nodes)
            {
                rootDist[node] = int.MaxValue;
                pred[node] = default(T);
            }
            rootDist[start] = 0;

            PriorityQueue<IEnumerable<T>> q = new PriorityQueue<IEnumerable<T>>()
            {
                Resolve = (x, y) => x.Union(y).Distinct()
            };
            q.Enqueue(0, Enumerable.Repeat(start, 1));
            while (!q.IsEmpty)
            {
                var kvp = q.Dequeue();
                int d = (int)kvp.Key;
                IEnumerable<T> curs = kvp.Value;
                foreach (T cur in curs)
                {
                    if (d != rootDist[cur])
                        continue;
                    foreach (T succ in succs[cur])
                    {
                        int newDist = rootDist[cur] + dist[Tuple.Create(cur, succ)];
                        if (newDist < rootDist[succ])
                        {
                            rootDist[succ] = newDist;
                            pred[succ] = cur;
                            q.Enqueue(newDist, Enumerable.Repeat(succ, 1));
                        }
                    }
                }
            }
        }
    }
}

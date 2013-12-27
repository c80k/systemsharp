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
    /// Constructs a multi-map from observing a sequence of key/value tuples.
    /// </summary>
    /// <typeparam name="TKey">datatype of key</typeparam>
    /// <typeparam name="TValue">datatype of value</typeparam>
    public class ObservingMultiMap<TKey, TValue>
    {
        private class SingleValueObserver : IObserver<Tuple<TKey, TValue>>
        {
            private IDisposable _disp;
            public IDisposable Disp 
            {
                private get { return _disp; }
                set
                {
                    if (_completed)
                        value.Dispose();
                    else
                        _disp = value;
                }
            }

            private ObservingMultiMap<TKey, TValue> _impl;
            private bool _completed;

            public SingleValueObserver(ObservingMultiMap<TKey, TValue> impl)
            {
                _impl = impl;
            }

            private void Complete()
            {
                if (Disp != null)
                    Disp.Dispose();
                else
                    _completed = true;
            }

            #region IObserver<Tuple<TKey,TValue>> Member

            public void OnCompleted()
            {
                Complete();
            }

            public void OnError(Exception error)
            {
                Complete();
                throw error;
            }

            public void OnNext(Tuple<TKey, TValue> value)
            {
                _impl.Add(value.Item1, value.Item2);
            }

            #endregion
        }

        private class MultiValueObserver : IObserver<Tuple<TKey, IEnumerable<TValue>>>
        {
            private IDisposable _disp;
            public IDisposable Disp
            {
                private get { return _disp; }
                set
                {
                    if (_completed)
                        value.Dispose();
                    else
                        _disp = value;
                }
            }

            private ObservingMultiMap<TKey, TValue> _impl;
            private bool _completed;

            public MultiValueObserver(ObservingMultiMap<TKey, TValue> impl)
            {
                _impl = impl;
            }

            private void Complete()
            {
                if (Disp != null)
                    Disp.Dispose();
                else
                    _completed = true;
            }

            #region IObserver<Tuple<TKey,TValue>> Member

            public void OnCompleted()
            {
                Complete();
            }

            public void OnError(Exception error)
            {
                Complete();
                throw error;
            }

            public void OnNext(Tuple<TKey, IEnumerable<TValue>> value)
            {
                _impl.Add(value.Item1, value.Item2);
            }

            #endregion
        }

        private Dictionary<TKey, HashSet<TValue>> _rel = new Dictionary<TKey, HashSet<TValue>>();

        /// <summary>
        /// Adds a new element to the multi-map for a given key.
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="value">value</param>
        public void Add(TKey key, TValue value)
        {
            _rel.Add(key, value);
        }

        /// <summary>
        /// Adds multiple elements to the multi-map for a given key.
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="values">elements to add</param>
        public void Add(TKey key, IEnumerable<TValue> values)
        {
            foreach (TValue value in values)
                Add(key, value);
        }

        /// <summary>
        /// Returns all elements associated with specified key
        /// </summary>
        /// <param name="key">key</param>
        /// <returns>all associated elements, or empty enumeration if key not found</returns>
        public IEnumerable<TValue> this[TKey key]
        {
            get { return _rel.Get(key).AsEnumerable(); }
        }

        /// <summary>
        /// Attaches this instance to a key/value pair observation. Each new tuple will automatically get inserted in the
        /// underlying multi-map.
        /// </summary>
        /// <param name="obs">key/value pair observation</param>
        public void Subscribe(IObservable<Tuple<TKey, TValue>> obs)
        {
            SingleValueObserver svo = new SingleValueObserver(this);
            svo.Disp = obs.Subscribe(svo);
        }

        /// <summary>
        /// Attaches this instance to a key/multi-value pair observation. Each new tuple will automatically get inserted in the
        /// underlying multi-map.
        /// </summary>
        /// <param name="obs">key/multi-value pair observation</param>
        public void Subscribe(IObservable<Tuple<TKey, IEnumerable<TValue>>> obs)
        {
            MultiValueObserver mvo = new MultiValueObserver(this);
            mvo.Disp = obs.Subscribe(mvo);
        }
    }
}

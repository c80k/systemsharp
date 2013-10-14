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
using System.Collections;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using SystemSharp.Common;

namespace SystemSharp.Collections
{
    /// <summary>
    /// An observable set behaves like a set and additionally implements the IObservable&lt;T&gt; interface to let subscribers know about
    /// the contained elements. To fulfill the contract, it implements a Complete() method which broadcasts a completion event to all
    /// subscribers and puts the object to unmodifiable state.
    /// </summary>
    /// <typeparam name="T">type of contained elements</typeparam>
    public class ObservableSet<T> : ISet<T>, ICollection<T>, IEnumerable<T>, IEnumerable, 
        IObservable<T>
    {
        private HashSet<T> _set;
        private List<IObserver<T>> _observers = new List<IObserver<T>>();
        private bool _complete;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        public ObservableSet()
        {
            _set = new HashSet<T>();
        }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="comparer">element equality comparer</param>
        public ObservableSet(IEqualityComparer<T> comparer)
        {
            _set = new HashSet<T>(comparer);
        }

        private void Push(T item)
        {
            if (_complete)
                throw new InvalidOperationException("Already completed");
            IObserver<T>[] observers = _observers.ToArray();
            foreach (IObserver<T> obs in observers)
                obs.OnNext(item);
        }

        public bool Add(T item)
        {
            if (_set.Add(item))
            {
                Push(item);
                return true;
            }
            else
                return false;
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return _set.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return _set.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return _set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return _set.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return _set.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return _set.SetEquals(other);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public void UnionWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                Add(item);
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        /// <summary>
        /// This method is not implemented, since it is not possible to revoke elements with respect to subscribers.
        /// </summary>
        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            return _set.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _set.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _set.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _set.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _set.GetEnumerator();
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            foreach (T item in _set)
                observer.OnNext(item);
            if (_complete)
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }
            else
            {
                _observers.Add(observer);
                return Disposable.Create(() => _observers.Remove(observer));
            }
        }

        /// <summary>
        /// Broadcasts a completion message to all subscribers and puts the object to unmodifiable state.
        /// I.e. once called, adding new elements is not permitted anymore. May only be called once. 
        /// </summary>
        public void Complete()
        {
            if (_complete)
                throw new InvalidOperationException("Already completed");
            IObserver<T>[] observers = _observers.ToArray();
            foreach (IObserver<T> observer in observers)
            {
                foreach (T item in _set)
                    observer.OnCompleted();
            }
            _complete = true;
        }
    }
}

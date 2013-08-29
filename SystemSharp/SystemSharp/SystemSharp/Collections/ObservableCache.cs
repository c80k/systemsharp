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
using System.Reactive.Disposables;
using System.Diagnostics;

namespace SystemSharp.Collections
{
    public class ObservableCache<TKey, TValue>: 
        CacheDictionary<TKey, TValue>,
        IObservable<TValue>
    {
        private Func<TKey, TValue> _creator;
        private List<IObserver<TValue>> _observers = new List<IObserver<TValue>>();
        private bool _complete;

        public ObservableCache(Func<TKey, TValue> creator):
            base(creator)
        {
            _creator = creator;
            OnItemAdded += OnItemAddedHandler;
        }

        public ObservableCache(IEqualityComparer<TKey> comparer, Func<TKey, TValue> creator) :
            base(comparer, creator)
        {
            OnItemAdded += OnItemAddedHandler;
        }

        #region IObservable<TValue> Member

        public IDisposable Subscribe(IObserver<TValue> observer)
        {
            TValue[] values = Values.ToArray();
            IDisposable result = null;
            if (!_complete)
            {
                _observers.Add(observer);
                result = Disposable.Create(() => _observers.Remove(observer));
            }
            foreach (TValue item in values)
                observer.OnNext(item);
            if (_complete)
            {
                observer.OnCompleted();
                _observers.Remove(observer);
                return Disposable.Empty;
            }
            else
            {
                Debug.Assert(result != null);
                return result;
            }
        }

        #endregion

        private void OnItemAddedHandler(TKey key, TValue item)
        {
            if (_complete)
                throw new InvalidOperationException("Already completed");

            IObserver<TValue>[] observers = _observers.ToArray();
            foreach (IObserver<TValue> obs in observers)
                obs.OnNext(item);
        }

        public void Complete()
        {
            if (_complete)
                throw new InvalidOperationException("Already completed");
            IObserver<TValue>[] observers = _observers.ToArray();
            foreach (IObserver<TValue> observer in observers)
            {
                foreach (TValue item in Values)
                    observer.OnCompleted();
            }
            _complete = true;
        }
    }
}

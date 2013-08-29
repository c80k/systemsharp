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
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace SystemSharp.Collections
{
    public class AutoDisposeObserver<T> : IObserver<T>
    {
        private IDisposable _disp;
        private volatile bool _completed;

        public readonly object SyncRoot = new object();

        public IDisposable Disp
        {
            set
            {
                if (_completed)
                    value.Dispose();
                else
                    _disp = value;
            }
        }

        private void Dispose()
        {
            if (_disp != null)
                _disp.Dispose();
            else
                _completed = true;
        }

        public bool IsCompleted
        {
            get { return _completed; }
        }

        #region IObserver<T> Member

        public virtual void OnCompleted()
        {
            lock (SyncRoot)
            {
                Dispose();
            }
        }

        public virtual void OnError(Exception error)
        {
            lock (SyncRoot)
            {
                Dispose();
            }
        }

        public virtual void OnNext(T value)
        {
        }

        #endregion
    }

    class ObservingList<T>: AutoDisposeObserver<T>
    {
        internal List<T> TheList = new List<T>();

        public override void OnNext(T value)
        {
            TheList.Add(value);
        }
    }

    class AutoDisposeAction<T> : AutoDisposeObserver<T>
    {
        private Action<T> _action;
        private Action _onCompleted;
        private Action<Exception> _onError;

        public AutoDisposeAction(Action<T> action)
        {
            _action = action;
        }

        public AutoDisposeAction(Action<T> action, Action onCompleted, Action<Exception> onError)
        {
            _action = action;
            _onCompleted = onCompleted;
            _onError = onError;
        }

        public override void OnNext(T value)
        {
            _action(value);
        }

        public override void OnCompleted()
        {
            base.OnCompleted();
            if (_onCompleted != null)
                _onCompleted();
        }

        public override void OnError(Exception error)
        {
            base.OnError(error);
            if (_onError != null)
                _onError(error);
        }
    }

    class GuardedEnumerable<T> : IEnumerable<T>
    {
        public enum EStatus
        {
            Observing,
            Completed,
            Error
        }

        private IEnumerable<T> _back;

        public GuardedEnumerable(IEnumerable<T> back)
        {
            _back = back;
            Status = EStatus.Observing;
        }

        private void EnsureCompleted()
        {
            if (Status != EStatus.Completed)
                throw new InvalidOperationException("IEnumerable is not ready for enumeration because it is in state " + Status);
        }

        #region IEnumerable<T> Member

        public IEnumerator<T> GetEnumerator()
        {
            EnsureCompleted();
            return _back.GetEnumerator();
        }

        #endregion

        #region IEnumerable Member

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        public EStatus Status { get; set; }
    }

    public static class ObservableExtensions
    {
        public static IEnumerable<T> ToBufferedEnumerable<T>(this IObservable<T> obj)
        {
            List<T> list = new List<T>();
            GuardedEnumerable<T> result = new GuardedEnumerable<T>(list);
            obj.AutoDo(
                e => list.Add(e), 
                () => result.Status = GuardedEnumerable<T>.EStatus.Completed,
                e => result.Status = GuardedEnumerable<T>.EStatus.Error);
            return result;
        }

        public static void AutoDo<T>(this IObservable<T> obj, Action<T> onNext)
        {
            AutoDisposeAction<T> obs = new AutoDisposeAction<T>(onNext);
            IDisposable disp = obj.Subscribe(obs);
            lock (obs.SyncRoot)
            {
                if (obs.IsCompleted)
                    disp.Dispose();
                else
                    obs.Disp = disp;
            }
        }

        public static void AutoDo<T>(this IObservable<T> obj, Action<T> onNext, Action onCompleted, Action<Exception> onError)
        {
            AutoDisposeAction<T> obs = new AutoDisposeAction<T>(onNext, onCompleted, onError);
            IDisposable disp = obj.Subscribe(obs);
            lock (obs.SyncRoot)
            {
                if (obs.IsCompleted)
                    disp.Dispose();
                else
                    obs.Disp = disp;
            }
        }

        public static Dictionary<TKey, TValue> ToDictionary<T, TKey, TValue>(this IObservable<T> obj, Func<T, TKey> key, Func<T, TValue> val)
        {
            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
            obj.AutoDo(e => result[key(e)] = val(e));
            return result;
        }

        public static Dictionary<TKey, HashSet<TValue>> ToMultiMap<T, TKey, TValue>(this IObservable<T> obj, Func<T, TKey> key, Func<T, TValue> val)
        {
            Dictionary<TKey, HashSet<TValue>> result = new Dictionary<TKey, HashSet<TValue>>();
            obj.AutoDo(e => result.Add(key(e), val(e)));
            return result;
        }
    }
}

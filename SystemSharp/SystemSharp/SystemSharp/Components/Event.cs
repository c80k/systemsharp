/**
 * Copyright 2012-2013 Christian Köllner, David Hlavac
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
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    public abstract class AbstractEvent : DesignObject
    {

        public AbstractEvent(DesignObject owner)
        {
            Owner = owner;
        }


        /// <summary>
        /// The owner of this object.
        /// </summary>
        public DesignObject Owner { get; protected set; }

        public abstract IAwaitable GetAwaiter();


        public static MultiEvent operator | (AbstractEvent e1, AbstractEvent e2)
        {
            List<AbstractEvent> all = new List<AbstractEvent>();

            MultiEvent m1 = e1 as MultiEvent;
            MultiEvent m2 = e2 as MultiEvent;

            if (m1 != null) { all.AddRange(m1._events); }
            else { all.Add(e1); }

            if (m2 != null) { all.AddRange(m2._events); }
            else { all.Add(e2); }

            return new MultiEvent(null, all);

            //vorher:
            //return new MultiEvent(null, new List<AbstractEvent>() { e1, e2 });
        }
    }

    /// <summary>
    /// This class implements an event which is basically an object which can take one of two states: set and unset.
    /// </summary>
    /// <remarks>
    /// The state of an event remains consistent throughout a delta cycle. If an event state is set in the current
    /// delta cycle, it is usually reset within the next one, unless it is requested to be set again.
    /// </remarks>
    public class Event : AbstractEvent
    {
        [RewriteAwaitE]
        [MapToIntrinsicType(EIntrinsicTypes.IllegalRuntimeType)]
        private class Awaiter : IAwaitable
        {
            internal Event _event;

            public Awaiter(Event evt)
            {
                _event = evt;
            }

            public bool IsCompleted
            {
                [RewriteIsCompleted]
                get { return DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis ? true : false; }
            }

            public void OnCompleted(Action continuation)
            {
                var curps = DesignContext.Instance.CurrentProcess;
                curps.SetContinuation(continuation);
                if (_event._fireList != null)
                {
                    var list = _event._fireList.GetInvocationList();
                    for (int i = 0; i < list.Length; i++)
                        if (list[i].Target == curps)
                            return;
                }
                _event._fireList += curps.ContinueProcess;
            }

            [RewriteGetResult]
            public void GetResult()
            {
            }
        }

        private Action _fireList;
        private Awaiter _awaiter;

        public Event(DesignObject owner) :
            base(owner)
        {
            _awaiter = new Awaiter(this);
        }

        public override IAwaitable GetAwaiter()
        {
            return _awaiter;
        }

        public void Fire()
        {
            if (_fireList != null)
            {
                DesignContext.Instance.Schedule(_fireList, 0);
                _fireList = null;
            }
        }

        private class RewriteAwaitE : RewriteAwait
        {
            public override bool Rewrite(Meta.CodeDescriptor decompilee, SysDOM.Expression waitObject, Analysis.IDecompiler stack, SysDOM.IFunctionBuilder builder)
            {
                var evt = waitObject.ResultType.GetSampleInstance();
                var sevent = evt as Event;
                if (sevent == null)
                {
                    // This workaround is for the following situation:
                    //   Signal s;
                    //   await s;
                    // This will actually await the "changed event" which is associated with the signal. However, decompilation
                    // will treat the signal instance as the awaited object and pass a Signal instance instead of the event.
                    // The code will try to restore the Event instance from the Awaiter.
                    Awaiter awaiter = null;
                    try
                    {
                        awaiter = ((object)AwaitableExtensionMethods.GetAwaiter((dynamic)evt)) as Awaiter;
                    }
                    catch
                    {
                    }
                    if (awaiter == null)
                        throw new InvalidOperationException("Unable to resolve awaited MultiEvent");
                    sevent = awaiter._event;
                }

                var signal = (SignalBase)sevent.Owner;
                var signalRef = SignalRef.Create(signal, SignalRef.EReferencedProperty.Instance);
                var arg = (LiteralReference)signalRef;

                var fspec = new FunctionSpec(typeof(void))
                {
                    IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitOn)
                };
                builder.Call(fspec, arg);
                return true;
            }
        }
    }

    public class MultiEvent : AbstractEvent
    {
        private class OneTimeInvoker
        {
            private Action _action;
            private object _locker = new object();

            public OneTimeInvoker(Action action)
            {
                _action = action;
            }

            public void Invoke()
            {
                Action action;
                lock (_locker)
                {
                    action = _action;
                    _action = null;
                }
                if (action != null)
                {
                    action();
                }
            }
        }

        [RewriteAwaitME]
        [MapToIntrinsicType(EIntrinsicTypes.IllegalRuntimeType)]
        private class Awaiter : IAwaitable
        {
            private MultiEvent _event;

            public Awaiter(MultiEvent evt)
            {
                _event = evt;
            }

            public bool IsCompleted
            {
                [RewriteIsCompleted]
                get { return DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis ? true : false; }
            }

            public void OnCompleted(Action continuation)
            {
                var oti = new OneTimeInvoker(continuation);
                foreach (var evt in _event._events)
                    evt.GetAwaiter().OnCompleted(oti.Invoke);
            }

            [RewriteGetResult]
            public void GetResult()
            {
            }
        }

        private Awaiter _awaiter;
        public IEnumerable<AbstractEvent> _events { get; private set; }

        public MultiEvent(DesignObject owner, IEnumerable<AbstractEvent> events) :
            base(owner)
        {
            Owner = owner;
            _events = events;
            _awaiter = new Awaiter(this);
        }

        public override IAwaitable GetAwaiter()
        {
            return _awaiter;
        }

        private class RewriteAwaitME : RewriteAwait
        {
            public override bool Rewrite(Meta.CodeDescriptor decompilee, SysDOM.Expression waitObject, Analysis.IDecompiler stack, SysDOM.IFunctionBuilder builder)
            {
                var evt = waitObject.ResultType.GetSampleInstance();
                var mevent = evt as MultiEvent;
                if (mevent == null)
                    throw new InvalidOperationException("Unable to resolve awaited MultiEvent");

                var events = mevent._events.Cast<Event>();
                var signals = events.Select(e => (SignalBase)e.Owner);
                var signalRefs = signals.Select(s => SignalRef.Create(s, SignalRef.EReferencedProperty.Instance));
                var args = signalRefs.Select(sr => (LiteralReference)sr).ToArray();

                var fspec = new FunctionSpec(typeof(void))
                {
                    IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitOn)
                };
                builder.Call(fspec, args);
                return true;
            }
        }
    }

    public class PredicatedEvent : AbstractEvent
    {
        private class PredicatedInvoker
        {
            private AbstractEvent _baseEvent;
            private Action _action;
            private Func<bool> _pred;

            public PredicatedInvoker(AbstractEvent baseEvent, Action action, Func<bool> pred)
            {
                _baseEvent = baseEvent;
                _action = action;
                _pred = pred;
            }

            public void Invoke()
            {
                if (_pred())
                {
                    _action();
                }
                else
                {
                    _baseEvent.GetAwaiter().OnCompleted(Invoke);
                }
            }
        }

        [RewriteAwaitPE]
        [MapToIntrinsicType(EIntrinsicTypes.IllegalRuntimeType)]        
        private class Awaiter : IAwaitable
        {
            internal PredicatedEvent _event;

            public Awaiter(PredicatedEvent evt)
            {
                _event = evt;
            }

            public bool IsCompleted
            {
                [RewriteIsCompleted]
                get { return DesignContext.Instance.State == DesignContext.ESimState.DesignAnalysis ? true : false; }
            }

            public void OnCompleted(Action continuation)
            {
                var pi = new PredicatedInvoker(_event._baseEvent, continuation, _event._pred);
                _event._baseEvent.GetAwaiter().OnCompleted(pi.Invoke);
            }

            [RewriteGetResult]
            public void GetResult()
            {
            }
        }

        private class RewriteAwaitPE : RewriteAwait
        {
            public override bool Rewrite(Meta.CodeDescriptor decompilee, SysDOM.Expression waitObject, Analysis.IDecompiler stack, SysDOM.IFunctionBuilder builder)
            {
                var evt = waitObject.ResultType.GetSampleInstance();
                var peevent = evt as PredicatedEvent;
                if (peevent == null)
                    throw new InvalidOperationException("Unable to resolve awaited PredicatedEvent");

                var pred = peevent._pred;
                var rwc = pred.Method.GetCustomOrInjectedAttribute<RewriteCall>();
                if (rwc == null)
                    throw new InvalidOperationException("Awaited predicate is not synthesizable.");

                var lr = LiteralReference.CreateConstant(pred.Target);
                var se = new StackElement(lr, pred.Target, Analysis.Msil.EVariability.Constant);
                var pstk = stack.CreatePrivateStack();
                if (!rwc.Rewrite(decompilee, pred.Method, new StackElement[] { se }, pstk, builder))
                    throw new InvalidOperationException("Unable to implement awaited predicate.");

                var predEx = pstk.Pop();
                var fspec = new FunctionSpec(typeof(void))
                {
                    IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitUntil)
                };
                builder.Call(fspec, predEx.Expr);

                return true;
           }
        }

        private Awaiter _awaiter;
        private AbstractEvent _baseEvent;
        private Func<bool> _pred;

        public PredicatedEvent(DesignObject owner, AbstractEvent evt, Func<bool> pred) :
            base(owner)
        {
            Contract.Requires<ArgumentNullException>(evt != null);
            Contract.Requires<ArgumentNullException>(pred != null);

            Owner = owner;
            _baseEvent = evt;
            _pred = pred;
            _awaiter = new Awaiter(this);
        }

        public override IAwaitable GetAwaiter()
        {
            return _awaiter;
        }
    }
}

/**
 * Copyright 2011-2013 Christian Köllner, David Hlavac
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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.Common;
using SystemSharp.Components.Transactions;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    public interface ISensitive
    {
        IEnumerable<AbstractEvent> Sensitivity { get; }
    }

    public interface IImplementable
    {
        void Implement(IAlgorithmBuilder builder);
    }

    public interface IImplementableByExpr
    {
        Expression GetExpression();
        object GetSample();
    }

    public interface IProcess: 
        ISensitive, IImplementable
    {
        Action Operation { get; }
        IEnumerable<ISignal> DrivenSignals { get; }
    }

    public interface ISignalSource :
        ISensitive, IImplementableByExpr
    {
        Func<object> Operation { get; }
    }

    public interface ISignalSource<T> : 
        ISignalSource
    {
        new Func<T> Operation { get; }
    }

    public interface ICombSignalSink<T>
    {
        IProcess Connect(ISignalSource<T> source);
    }

    public interface ISyncSignalSink<T>
    {
        [RedirectRewriteCall]
        void Write(T value);
    }

    public interface ISignalSink<T>
    {
        ICombSignalSink<T> Comb { [StaticEvaluationDoNotAnalyze] get; }
        ISyncSignalSink<T> Sync { [StaticEvaluationDoNotAnalyze] get; }
    }

    public class DefaultSignalSink<T> : ISignalSink<T>
    {
        public ICombSignalSink<T> Comb { [StaticEvaluationDoNotAnalyze] get; private set; }
        public ISyncSignalSink<T> Sync { [StaticEvaluationDoNotAnalyze] get; private set; }

        public DefaultSignalSink(ICombSignalSink<T> comb)
        {
            Comb = comb;
        }

        public DefaultSignalSink(ISyncSignalSink<T> sync)
        {
            Sync = sync;
        }
    }

    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    class HideDeclaration : RewriteDeclaration, IDoNotAnalyze
    {
        public override void ImplementDeclaration(DescriptorBase container, System.Reflection.MemberInfo declSite)
        {
        }

        public override void ImplementDeclaration(LocalVariableInfo lvi, IDecompiler decomp)
        {
            decomp.HideLocal(lvi);
        }
    }

    public static class SignalSink
    {
        private class ToCombSink<T> : ICombSignalSink<T>
        {
            public Out<T> Signal { get; private set; }

            public ToCombSink(Out<T> signal)
            {
                Signal = signal;
            }

            public IProcess Connect(ISignalSource<T> source)
            {
                return Signal.Drive(source);
            }
        }

        private interface IHasSignalBase
        {
            SignalBase Signal { get; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
        private class WriteToSyncSignal : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                IHasSignalBase sink = (IHasSignalBase)args[0].Sample;
                SignalRef next = SignalRef.Create(sink.Signal, SignalRef.EReferencedProperty.Next);
                builder.Store(next, args[1].Expr);
                return true;
            }
        }

        private class ToSyncSink<T> : ISyncSignalSink<T>, IHasSignalBase
        {
            public Out<T> Signal { get; private set; }

            public ToSyncSink(Out<T> signal)
            {
                Signal = signal;
            }

            #region ISyncSignalSink<T> Member

            [WriteToSyncSignal]
            public void Write(T value)
            {
                Signal.Next = value;
            }

            #endregion

            #region IHasSignalBase Member

            SignalBase IHasSignalBase.Signal
            {
                get { return (SignalBase)Signal; }
            }

            #endregion
        }

        private class NilSink<T> : ICombSignalSink<T>
        {
            public IProcess Connect(ISignalSource<T> source)
            {
                return Processes.Nil();
            }
        }

        private class MultiSink<T> : ICombSignalSink<T>
        {
            public IEnumerable<ICombSignalSink<T>> Sinks { get; private set; }

            public MultiSink(IEnumerable<ICombSignalSink<T>> sinks)
            {
                Sinks = sinks;
            }

            public IProcess Connect(ISignalSource<T> source)
            {
                return Processes.Fork(
                    Sinks.Select(s => s.Connect(source)).ToArray());
            }
        }

        [StaticEvaluationDoNotAnalyze]
        public static ISignalSink<T> AsCombSink<T>(this Out<T> signal)
        {
            return new DefaultSignalSink<T>(new ToCombSink<T>(signal));
        }

        [StaticEvaluationDoNotAnalyze]
        [Obsolete("Synchronous sinks will be removed in next release")]
        public static ISignalSink<T> AsSyncSink<T>(this Out<T> signal)
        {
            return new DefaultSignalSink<T>(new ToSyncSink<T>(signal));
        }

        [StaticEvaluationDoNotAnalyze]
        public static ISignalSink<T> Nil<T>()
        {
            return new DefaultSignalSink<T>(new NilSink<T>());
        }

        [StaticEvaluationDoNotAnalyze]
        public static ISignalSink<T> Combine<T>(params ISignalSink<T>[] sinks)
        {
            return new DefaultSignalSink<T>(new MultiSink<T>(sinks.Select(s => s.Comb)));
        }
    }

    public static class SignalSource
    {
        private class FromSignalSource : ISignalSource
        {
            public ISignal Signal { get; private set; }
            public Func<object> Operation { get; private set; }
            public IEnumerable<AbstractEvent> Sensitivity { get; private set; }

            public FromSignalSource(ISignal signal)
            {
                Signal = signal;
                Operation = () => Signal.CurObject;
                Sensitivity = new AbstractEvent[] { Signal.ChangedEvent };
            }

            public Expression GetExpression()
            {
                return new LiteralReference(Signal.ToSignalRef(SignalRef.EReferencedProperty.Cur));
            }

            public object GetSample()
            {
                return ((ISignal)Signal).InitialValueObject;
            }
        }

        private class FromSignalSource<T> : ISignalSource<T>
        {
            public In<T> Signal { get; private set; }
            public Func<T> Operation { get; private set; }
            public IEnumerable<AbstractEvent> Sensitivity { get; private set; }
            
            Func<object> ISignalSource.Operation
            {
                get { return () => Operation(); }
            }

            public FromSignalSource(In<T> signal)
            {
                Signal = signal;
                Operation = () => Signal.Cur;
                Sensitivity = new AbstractEvent[] { Signal.ChangedEvent };
            }

            public Expression GetExpression()
            {
                ISignal signal = (ISignal)Signal;
                return new LiteralReference(signal.ToSignalRef(SignalRef.EReferencedProperty.Cur));
            }

            public object GetSample()
            {
                return ((ISignal)Signal).InitialValueObject;
            }
        }

        private abstract class ConstSignalSourceBase: 
            IImplementableByExpr
        {
            public Expression ValueExpr { get; set; }

            #region IImplementableByExpr Member

            public abstract Expression GetExpression();
            public abstract object GetSample();

            #endregion
        }

        private class ConstSignalSource :
            ConstSignalSourceBase,
            ISignalSource
        {
            private object _value;
            public Func<object> Operation { get; private set; }
            public IEnumerable<AbstractEvent> Sensitivity { get; private set; }

            public ConstSignalSource(object value)
            {
                _value = value;
                Operation = () => _value;
                Sensitivity = new AbstractEvent[0];
                ValueExpr = LiteralReference.CreateConstant(_value);
            }

            #region IImplementableByExpr Member

            public override Expression GetExpression()
            {
                return ValueExpr;
            }

            public override object GetSample()
            {
                return _value;
            }

            #endregion
        }

        private class ConstSignalSource<T> : 
            ConstSignalSourceBase,
            ISignalSource<T>
        {
            private T _value;
            public Func<T> Operation { get; private set; }
            public IEnumerable<AbstractEvent> Sensitivity { get; private set; }

            public ConstSignalSource(T value)
            {
                _value = value;
                Operation = () => _value;
                Sensitivity = new AbstractEvent[0];
                ValueExpr = LiteralReference.CreateConstant(_value);
            }

            #region IImplementableByExpr Member

            public override Expression GetExpression()
            {
                return ValueExpr;
            }

            public override object GetSample()
            {
                return _value;
            }

            #endregion

            #region ISignalSource Member

            Func<object> ISignalSource.Operation
            {
                get { return () => Operation(); }
            }

            #endregion
        }

        private class RewriteCreateConstSignalSource : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                if (args[0].Sample == null)
                    throw new InvalidOperationException("null sample");

                ConstSignalSourceBase src = (ConstSignalSourceBase)callee.Invoke(args[0].Sample);
                src.ValueExpr = args[0].Expr;
                Expression srcex = LiteralReference.CreateConstant(src);
                stack.Push(srcex, src);
                return true;
            }
        }

        [StaticEvaluation]
        public static ISignalSource<T> AsSignalSource<T>(this In<T> signal)
        {
            return new FromSignalSource<T>(signal);
        }

        [StaticEvaluation]
        public static ISignalSource AsSignalSource(this ISignal signal)
        {
            return new FromSignalSource(signal);
        }

        [RewriteCreateConstSignalSource]
        public static ISignalSource<T> Create<T>(T value)
        {
            return new ConstSignalSource<T>(value);
        }

        [RewriteCreateConstSignalSource]
        public static ISignalSource CreateUT(object value)
        {
            return new ConstSignalSource(value);
        }
    }

    public static class Processes
    {
        private class DrivingProcess : IProcess
        {
            public ISignal DrivenSignal { get; private set; }
            public ISignalSource Source { get; private set; }
            public Action Operation { get; private set; }
            public IEnumerable<ISignal> DrivenSignals { get; private set; }

            public IEnumerable<AbstractEvent> Sensitivity
            {
                get { return Source.Sensitivity; }
            }

            public DrivingProcess(ISignal drivenSignal, ISignalSource source)
            {
                DrivenSignal = drivenSignal;
                Source = source;
                Operation = () => DrivenSignal.NextObject = source.Operation();
                DrivenSignals = new ISignal[] { drivenSignal };
            }

            public void Implement(IAlgorithmBuilder builder)
            {
                IDescriptive idesc = (IDescriptive)DrivenSignal;
                ISignalOrPortDescriptor desc = (ISignalOrPortDescriptor)idesc.Descriptor;
                builder.Store(DrivenSignal.ToSignalRef(SignalRef.EReferencedProperty.Next),
                    Source.GetExpression());
            }
        }

        private class DrivingProcess<T> : IProcess
        {
            public Out<T> DrivenSignal { get; private set; }
            public ISignalSource<T> Source { get; private set; }
            public Action Operation { get; private set; }
            public IEnumerable<ISignal> DrivenSignals { get; private set; }

            public IEnumerable<AbstractEvent> Sensitivity
            {
                get { return Source.Sensitivity; }
            }

            public DrivingProcess(Out<T> drivenSignal, ISignalSource<T> source)
            {
                DrivenSignal = drivenSignal;
                Source = source;
                Operation = () => DrivenSignal.Next = source.Operation();
                DrivenSignals = new ISignal[] { (ISignal)drivenSignal };
            }

            public void Implement(IAlgorithmBuilder builder)
            {
                IDescriptive idesc = (IDescriptive)DrivenSignal;
                ISignalOrPortDescriptor desc = (ISignalOrPortDescriptor)idesc.Descriptor;
                builder.Store(
                    ((ISignal)DrivenSignal).ToSignalRef(SignalRef.EReferencedProperty.Next),
                    Source.GetExpression());
            }
        }

        private class ParProcess : IProcess
        {
            public IEnumerable<IProcess> ProcessDefs { get; private set; }
            public Action Operation { get; private set; }

            public IEnumerable<AbstractEvent> Sensitivity
            {
                get { return ProcessDefs.SelectMany(p => p.Sensitivity); }
            }

            public IEnumerable<ISignal> DrivenSignals
            {
                get { return ProcessDefs.SelectMany(p => p.DrivenSignals);  }
            }

            private void ExecOperation()
            {
                foreach (IProcess p in ProcessDefs)
                    p.Operation();
            }

            public ParProcess(IProcess p1, IProcess p2)
            {
                ProcessDefs = new IProcess[] { p1, p2 };
                Operation = ExecOperation;
            }

            public ParProcess(IEnumerable<IProcess> ps)
            {
                ProcessDefs = ps;
                Operation = ExecOperation;
            }

            public void Implement(IAlgorithmBuilder builder)
            {
                foreach (IProcess p in ProcessDefs)
                    p.Implement(builder);
            }
        }

        private class NilProcess : IProcess
        {
            private void Nop()
            {
            }

            public Action Operation
            {
                get { return Nop; }
            }

            public IEnumerable<ISignal> DrivenSignals
            {
                get { return Enumerable.Empty<ISignal>(); }
            }

            public IEnumerable<AbstractEvent> Sensitivity
            {
                get { return Enumerable.Empty<AbstractEvent>(); }
            }

            public void Implement(IAlgorithmBuilder builder)
            {
            }
        }

        [StaticEvaluation]
        public static IProcess Drive<T>(this Out<T> signal, ISignalSource<T> source)
        {
            Contract.Requires(signal != null);
            Contract.Requires(source != null);
            return new DrivingProcess<T>(signal, source);
        }

        [StaticEvaluation]
        public static IProcess DriveUT(this ISignal signal, ISignalSource source)
        {
            Contract.Requires(signal != null);
            Contract.Requires(source != null);
            return new DrivingProcess(signal, source);
        }

        [StaticEvaluation]
        public static IProcess Stick<T>(this Out<T> signal, T value)
        {
            Contract.Requires(signal != null);
            return Drive(signal, SignalSource.Create(value));
        }

        [StaticEvaluation]
        public static IProcess Par(this IProcess first, IProcess second)
        {
            Contract.Requires(first != null);
            Contract.Requires(second != null);
            return new ParProcess(first, second);
        }

        [StaticEvaluation]
        public static IProcess Fork(params IProcess[] ps)
        {
            Contract.Requires(ps != null);
            Contract.Requires(ps.All(p => p != null));
            return new ParProcess(ps);
        }

        [StaticEvaluation]
        public static IProcess Nil()
        {
            return new NilProcess();
        }
    }

    public class LazyProcess : IProcess
    {
        public IProcess ProcessDef { get; set; }

        #region IProcess Member

        public Action Operation
        {
            get { return ProcessDef.Operation; }
        }

        public IEnumerable<ISignal> DrivenSignals
        {
            get { return ProcessDef.DrivenSignals; }
        }

        #endregion

        #region ISensitive Member

        public IEnumerable<AbstractEvent> Sensitivity
        {
            get { return ProcessDef.Sensitivity; }
        }

        #endregion

        #region IImplementable Member

        public void Implement(IAlgorithmBuilder builder)
        {
            if (ProcessDef == null)
                throw new InvalidOperationException("No valid process definition");

            ProcessDef.Implement(builder);
        }

        #endregion
    }

    /// <summary>
    /// This class is used internally to wrap exceptions which are potentially thrown by a process.
    /// </summary>
    class ProcessFailedException : Exception
    {
        /// <summary>
        /// The process which threw the exception
        /// </summary>
        public Process Failee { get; private set; }

        /// <summary>
        /// Constructs an instance of a ProcessFailedException
        /// </summary>
        /// <param name="process">The process which threw an exception</param>
        /// <param name="cause">The actual exception which was thrown</param>
        public ProcessFailedException(Process process, Exception cause) :
            base("Process " + process.Name + " failed", cause)
        {
            Failee = process;
        }
    }

    /// <summary>
    /// This class represents a process.
    /// </summary>
    public class Process : DesignObject
    {
        /// <summary>
        /// The ThreadStopException when a process requests to sleep forever. It causes the process to be removed
        /// from the scheduler.
        /// </summary>
        private class StopThreadException : Exception
        {
        }

        /// <summary>
        /// Describes the kind of the process.
        /// </summary>
        public enum EProcessKind
        {
            /// <summary>
            /// A triggered process is the equivalent of a SystemC method process. Its method body is invoked as soon
            /// as any event inside the sensitivity list is signaled. A triggered process must not call any Wait() method.
            /// </summary>
            Triggered,

            /// <summary>
            /// A threaded process is the equivalent of a SystemC thread process. Its method body is invoked at simulation
            /// start. The process suspends its execution self-dependently by calling a Wait() method.
            /// </summary>
            Threaded
        }

        /// <summary>
        /// This field serves to conventiently specify an empty sensitivity list.
        /// </summary>
        public static readonly Event[] EmptySensitivity = new Event[0];

        /// <summary>
        /// Constructs a process instance
        /// </summary>
        /// <param name="owner">The component which hosts the process</param>
        /// <param name="kind">The process kind (triggered, threaded)</param>
        /// <param name="function">The process body</param>
        internal Process(Component owner, EProcessKind kind, Action function)
        {
            Owner = owner;
            Kind = kind;
            InitialAction = function;
            Context.OnStartOfSimulation += OnStartOfSimulation;
            _analysisGeneration = Context.CurrentRefinementCycle;
        }

        /// <summary>
        /// Returns a symbolic name of the process. The name is constructed from reflection.
        /// </summary>
        public string Name
        {
            get
            {
                return InitialAction.Method.DeclaringType.Name + "." + InitialAction.Method.Name;
            }
        }

        /// <summary>
        /// The current sensitivity list.
        /// </summary>
        private AbstractEvent[] _sensitivity;

        /// <summary>
        /// A helper field to realize process-local storage.
        /// </summary>
        private object[] _localStorage;

        private event Action _preWaitAction;
        private event Action _postWaitAction;
        private IProcess _duringProcess;
        //private TransactingComponent.TATarget[] _coFSMs;

        /// <summary>
        /// Returns the component which hosts this process.
        /// </summary>
        public Component Owner { get; private set; }

        /// <summary>
        /// Returns the kind of this process (triggered/threaded).
        /// </summary>
        public EProcessKind Kind { get; private set; }

        /// <summary>
        /// Returns the action (implementation) which is performed by this process.
        /// </summary>
        public Action CurrentContinuation { get; private set; }
        public Action InitialAction { get; private set; }

        /// <summary>
        /// The process sensitivity list
        /// </summary>
        public AbstractEvent[] Sensitivity
        {
            get
            {
                return _sensitivity;
            }
            internal set
            {
                _sensitivity = value;
            }
        }

        public Func<bool> Predicate { get; internal set; }

        internal void SetContinuation(Action cont)
        {
            CurrentContinuation -= cont;
            CurrentContinuation += cont;
        }

        /// <summary>
        /// This is the wrapper function which gets actually scheduled for a threaded process.
        /// </summary>
        private void TopLevelWrapperThreaded()
        {
            try
            {
                Context.CurrentProcess = this;
                InitialAction();
                if (CurrentContinuation == null)
                {
                    throw new InvalidOperationException("Busy process: " + Name);
                }
            }
            catch (StopThreadException)
            {
            }
        }

        /// <summary>
        /// This is the wrapper function which gets actually scheduled for a threaded process.
        /// </summary>
        private async void TopLevelWrapperTriggered()
        {
            try
            {
                while (true)
                {
                    Context.CurrentProcess = this;
                    InitialAction();
                    var me = new MultiEvent(null, Sensitivity);
                    await me; //await Sensitivity; would be "nicer", but does not compile even though an awaitable ExtensionMethod is declared for an Array of AbstractEvents
                }
            }
            catch (StopThreadException)
            {
            }
        }

        internal void ContinueProcess()
        {
            try
            {
                Context.CurrentProcess = this;
                var cc = CurrentContinuation;
                CurrentContinuation = null;
                cc();
                if (CurrentContinuation == null)
                    TopLevelWrapperThreaded();
            }
            catch (StopThreadException)
            {
            }
        }

        /// <summary>
        /// Wraps a given exception into a ProcessFailedException
        /// </summary>
        /// <param name="e">The exception to be wrapped</param>
        /// <returns>A ProcessFailedException with the actual exception as InnerException</returns>
        private Exception WrapException(Exception e)
        {
            if (e is BarrierPostPhaseException)
                return WrapException(e.InnerException);
            else if (e is ProcessFailedException)
                return e;
            else
                return new ProcessFailedException(this, e);
        }

        /// <summary>
        /// This method performs necessary startup processing when the simulation starts.
        /// </summary>
        private void OnStartOfSimulation()
        {
        }

        /// <summary>
        /// Schedules this process for future execution.
        /// </summary>
        /// <param name="delta">The relative number of ticks</param>
        internal void Schedule(long delta)
        {
            if (Kind == EProcessKind.Threaded)
                Context.Schedule(TopLevelWrapperThreaded, delta);
            else
                Context.Schedule(TopLevelWrapperTriggered, delta);
        }

        /// <summary>
        /// Initializes process-local storage.
        /// </summary>
        /// <param name="numSlots">the number of storage objects</param>
        internal void InitLocalStorage(int numSlots)
        {
            _localStorage = new object[numSlots];
        }

        /// <summary>
        /// Retrieves a process-local storage object.
        /// </summary>
        /// <param name="slot">The index of the object to be retrieved</param>
        /// <returns></returns>
        internal object GetLocal(int slot)
        {
            return _localStorage[slot];
        }

        /// <summary>
        /// Stores a process-local storage object.
        /// </summary>
        /// <param name="slot">The index of the object to be stored</param>
        /// <param name="value">The object to be stored</param>
        internal void StoreLocal(int slot, object value)
        {
            _localStorage[slot] = value;
        }

        /// <summary>
        /// Throws an exception if this process is not threaded
        /// </summary>
        private void EnsureThreaded()
        {
            //if (Kind != EProcessKind.Threaded)
            //    throw new InvalidOperationException("await is only allowed within a threaded process.");
        }

        /// <summary>
        /// Writes some debug output to the console.
        /// </summary>
        /// <param name="text">The text to be written</param>
        private void Log(string text)
        {
            Console.WriteLine("> " + Kind.ToString() + " process " + InitialAction.Method.DeclaringType.Name + "." + InitialAction.Method.Name + ": " + text);
        }

        /// <summary>
        /// Suspends execution for a specified period.
        /// </summary>
        /// <param name="delta">The desired suspension period</param>
        [Obsolete("not supported anymore", true)]
        internal void Wait(Time delta)
        {
            ///uncommented due to [Obsolete]

            //EnsureThreaded();
            //await delta;
        }

        /// <summary>
        /// Suspends execution until at least one of the specified events is set.
        /// </summary>
        /// <param name="events">the events to wait for</param>
        [Obsolete("not supported anymore", true)]
        internal void Wait(params AbstractEvent[] events)
        {
            ///uncommented due to [Obsolete]

            //EnsureThreaded();
            //await new MultiEvent(null, events);
        }

        [Obsolete("not supported anymore", true)]
        internal void Wait()
        {
            ///uncommented due to [Obsolete]

            //EnsureThreaded();

            //if (Predicate == null)
            //{
            //    throw new StopThreadException();
            //}

            //if (_preWaitAction != null)
            //    _preWaitAction();

            //AbstractEvent[] sens;
            //if (_duringProcess != null)
            //{
            //    _duringProcess.Operation();
            //    sens = Sensitivity.Concat(_duringProcess.Sensitivity).ToArray();
            //}
            //else
            //{
            //    sens = Sensitivity;
            //}

            //do
            //{
            //    await new MultiEvent(null, sens);
            //    if (_duringProcess != null)
            //    {
            //        _duringProcess.Operation();
            //    }
            //} while (!Predicate());

            //if (_postWaitAction != null)
            //    _postWaitAction();
        }

        [Obsolete("not supported anymore", true)]
        internal void Wait(IProcess during)
        {
            ///uncommented due to [Obsolete]

            //Contract.Requires(during != null);
            //RegisterDuringAction(during);
            //Wait();
        }

        /// <summary>
        /// Stops the process, regardless of whether it is triggered or threaded.
        /// </summary>
        internal void Stop()
        {
            throw new StopThreadException();
        }

        [Obsolete("Transaction concept need to be redesigned, method will be without effect.")]
        internal void RegisterPreWaitAction(Action a)
        {
            _preWaitAction += a;
        }

        [Obsolete("Transaction concept need to be redesigned, method will be without effect.")]
        internal void RegisterPostWaitAction(Action a)
        {
            _postWaitAction += a;
        }

        internal void RegisterDuringAction(IProcess action)
        {
            Contract.Requires(action != null);
            if (_duringProcess == null)
                _duringProcess = action;
            else
                _duringProcess = _duringProcess.Par(action);
        }

#if false
        internal void RegisterCoFSMs(TransactingComponent.TATarget[] targets)
        {
            _coFSMs = targets;
            foreach (TransactingComponent.TATarget tat in targets)
            {
                tat.Register(this);
            }
        }

        internal TransactingComponent.TATarget[] GetCoFSMs()
        {
            return _coFSMs;
        }
#endif

        public bool IsDoNotAnalyze
        {
            get { return InitialAction.Method.HasCustomOrInjectedAttribute<IDoNotAnalyze>(); }
        }

        private int _analysisGeneration;
        internal int AnalysisGeneration
        {
            get { return _analysisGeneration; }
            set { _analysisGeneration = value; }
        }
    }
}

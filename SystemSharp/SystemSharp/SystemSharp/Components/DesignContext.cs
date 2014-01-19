/**
 * Copyright 2011-2014 Christian Köllner, David Hlavac
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler;
using SystemSharp.Collections;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// This class encapsulates the simulation context. Each design has an associated simulation context.
    /// The simulation context keeps track of all constructed components and channels and implements the
    /// simulator kernel.
    /// </summary>
    [ModelElement]
    [MapToIntrinsicType(EIntrinsicTypes.DesignContext)]
    public class DesignContext : IDescriptive<DesignDescriptor, DesignContext>
    {
        /// <summary>
        /// This enumeration defines the possible model/simulation states
        /// </summary>
        public enum ESimState
        {
            /// <summary>
            /// This is the initial state.
            /// </summary>
            Construction,

            /// <summary>
            /// Elaboration in progress.
            /// </summary>
            Elaboration,

            /// <summary>
            /// Elaboration phase completed.
            /// </summary>
            SimulationReady,

            /// <summary>
            /// The simulation is currently running.
            /// </summary>
            Simulation,

            /// <summary>
            /// The simulation was paused.
            /// </summary>
            SimulationPaused,

            /// <summary>
            /// The simulation is about to stop.
            /// </summary>
            StopRequested,

            /// <summary>
            /// The simulation was stopped.
            /// </summary>
            Stopped,

            /// <summary>
            /// The simulation had to stop because of an error condition, e.g. a process threw an exception.
            /// </summary>
            Failed,

            /// <summary>
            /// The design is being analyzed
            /// </summary>
            DesignAnalysis,

            /// <summary>
            /// Design analysis was completed
            /// </summary>
            DesignAnalysisCompleted,

            /// <summary>
            /// Design refinements were executed
            /// </summary>
            RefinementsCompleted
        }

        /// <summary>
        /// Design context properties which are mapped to SysDOM-intrinsic functions
        /// </summary>
        public enum EProperties
        {
            /// <summary>
            /// Current state of simulation
            /// </summary>
            State,

            /// <summary>
            /// Current time
            /// </summary>
            CurTime
        }

        /// <summary>
        /// Current instance of the simulation context.
        /// </summary>        
        public static DesignContext Instance { [StaticEvaluation] get; private set; }

        private static Stack<DesignContext> _ctxStack = new Stack<DesignContext>();

        private Action _nextDeltaCycleHandlers;
        private Action _elaborationHandlers;
        private Action _endOfElaborationHandlers;
        private Action _endOfConstructionHandlers;
        private Action _startOfSimulationHandlers;
        private Action _simulationStoppedHandlers;
        private Action _simulationStoppingHandlers;
        private Action _analysisHandlers;
        private PriorityQueue<Action> _q = new PriorityQueue<Action>();
        private List<Component> _components = new List<Component>();
        private List<Process> _processes = new List<Process>();
        private int _pidCounter;
        private List<SignalBase> _signals = new List<SignalBase>();
        private int _numPLSSlots;
        private Stack<DesignObject> _objStack = new Stack<DesignObject>();
        private Time _resolution = new Time(1.0, ETimeUnit.ns);
        private ManualResetEventSlim _simRunningEvent;
        private CancellationTokenSource _cancelSimSource;
        private Queue<IRefinementCycle> _refinementQ = new Queue<IRefinementCycle>();

        static DesignContext()
        {
            Instance = new DesignContext();
        }

        /// <summary>
        /// Clears all state information of current design context, as if program execution would freshly start.
        /// </summary>
        [DoNotAnalyze]
        public static void Reset()
        {
            Instance = new DesignContext();
        }

        private static Action Resolve(Action cur, Action plus)
        {
            Contract.Requires(cur.GetInvocationList() != null);

            var list1 = plus.GetInvocationList();
            var list2 = cur.GetInvocationList();
            for (int i = 0; i < list1.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < list2.Length; j++)
                {
                    if (list1[i].Target == list2[j].Target)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    cur = (Action)Delegate.Combine(cur, list1[i]);
            }

            return cur;
        }

        /// <summary>
        /// Constructs a new simulation context.
        /// </summary>
        public DesignContext()
        {
            _q.Resolve = Resolve;
            State = ESimState.Construction;
            Scheduler = TaskScheduler.Default;
            Factory = new TaskFactory(new CancellationToken(),
                TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness,
                TaskContinuationOptions.LongRunning | TaskContinuationOptions.PreferFairness,
                Scheduler);
            Descriptor = new DesignDescriptor(this);
            _simRunningEvent = new ManualResetEventSlim();
            _cancelSimSource = new CancellationTokenSource();
            OnEndOfElaboration += ClaimTopLevelComponents;
            _fixPointSettings = new FixedPointSettings(this);
        }

        /// <summary>
        /// Current simulation state.
        /// </summary>
        private ESimState _state;
        public ESimState State
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.PropertyRef, EProperties.State)]
            get { return _state; }
            private set { _state = value; }
        }

        /// <summary>
        /// Current simulation time in raw tick units.
        /// </summary>
        public long Ticks { get; private set; }

        /// <summary>
        /// Simulation time when current <c>Simulate(...)</c> call will return, in raw tick units.
        /// </summary>
        public long StopTicks { get; private set; }

        /// <summary>
        /// Current simulation time.
        /// </summary>
        public Time CurTime
        {
            [MapToIntrinsicFunction(IntrinsicFunction.EAction.PropertyRef, EProperties.CurTime)]
            get
            {
                return (double)Ticks * Resolution;
            }
        }

        private FactUniverse _universe;

        /// <summary>
        /// The fact universe of this context
        /// </summary>
        public FactUniverse Universe
        {
            get
            {
                if (_universe == null)
                    _universe = new FactUniverse();
                return _universe;
            }
        }

        internal void InvalidateUniverse()
        {
            _universe = null;
        }

        /// <summary>
        /// This event is triggered when the simulator kernel begins the next delta cycle.
        /// </summary>
        public event Action OnNextDeltaCycle
        {
            [DoNotAnalyze]
            add
            {
                _nextDeltaCycleHandlers += value;
            }
            [DoNotAnalyze]
            remove
            {
                _nextDeltaCycleHandlers -= value;
            }
        }

        /// <summary>
        /// This event is triggered when the model is about to enter the elaboration phase.
        /// </summary>
        public event Action OnEndOfConstruction
        {
            [DoNotAnalyze]
            add
            {
                _endOfConstructionHandlers = value + _endOfConstructionHandlers;
            }
            [DoNotAnalyze]
            remove
            {
                _endOfConstructionHandlers -= value;
            }
        }

        /// <summary>
        /// This event is triggered when the model entered the elaboration phase.
        /// </summary>
        public event Action OnElaborate
        {
            [DoNotAnalyze]
            add
            {
                _elaborationHandlers += value;
            }
            [DoNotAnalyze]
            remove
            {
                _elaborationHandlers -= value;
            }
        }

        /// <summary>
        /// This event is triggered when the elaboration phase is completed.
        /// </summary>
        public event Action OnEndOfElaboration
        {
            [DoNotAnalyze]
            add
            {
                _endOfElaborationHandlers += value;
            }
            [DoNotAnalyze]
            remove
            {
                _endOfElaborationHandlers -= value;
            }
        }

        /// <summary>
        /// This event is triggered when the simulation is about to start.
        /// </summary>
        public event Action OnStartOfSimulation
        {
            [DoNotAnalyze]
            add
            {
                _startOfSimulationHandlers += value;
            }
            [DoNotAnalyze]
            remove
            {
                _startOfSimulationHandlers -= value;
            }
        }

        /// <summary>
        /// This event is triggered when the simulation stopped.
        /// </summary>
        public event Action OnSimulationStopped
        {
            [DoNotAnalyze]
            add
            {
                _simulationStoppedHandlers += value;
            }
            [DoNotAnalyze]
            remove
            {
                _simulationStoppedHandlers -= value;
            }
        }

        /// <summary>
        /// This event is triggered when the simulation is about to stop.
        /// </summary>
        public event Action OnSimulationStopping
        {
            [DoNotAnalyze]
            add
            {
                _simulationStoppingHandlers += value;
            }
            [DoNotAnalyze]
            remove
            {
                _simulationStoppingHandlers -= value;
            }
        }

        /// <summary>
        /// This event is triggered when the simulation is about to stop.
        /// </summary>
        public event Action OnAnalysis
        {
            [DoNotAnalyze]
            add
            {
                _analysisHandlers += value;
            }
            [DoNotAnalyze]
            remove
            {
                _analysisHandlers -= value;
            }
        }

        /// <summary>
        /// Returns a collection of components which are associated with this simulation context.
        /// </summary>
        public ReadOnlyCollection<Component> Components
        {
            [StaticEvaluation]
            get
            {
                Contract.Assume(_components != null);

                return new ReadOnlyCollection<Component>(_components);
            }
        }

        /// <summary>
        /// Returns a flat collection of all processes which are associated with this simulation context.
        /// </summary>
        public ReadOnlyCollection<Process> Processes
        {
            [StaticEvaluation]
            get
            {
                return new ReadOnlyCollection<Process>(_processes);
            }
        }

        /// <summary>
        /// Returns a flat collection of all signals which are associated with this simulation context.
        /// </summary>
        public ReadOnlyCollection<SignalBase> Signals
        {
            [StaticEvaluation]
            get
            {
                return new ReadOnlyCollection<SignalBase>(_signals);
            }
        }

        /// <summary>
        /// Registers a component with this simulation context.
        /// </summary>
        /// <param name="component">The component to register</param>
        internal void RegisterComponent(Component component)
        {
            _components.Add(component);
        }

        /// <summary>
        /// Unregisters a component with this simulation context.
        /// </summary>
        /// <param name="component">The component to unregister</param>
        internal void UnregisterComponent(Component component)
        {
            _components.Remove(component);
        }

        /// <summary>
        /// Registers a process with this simulation context.
        /// </summary>
        /// <param name="process">The process to register</param>
        internal int RegisterProcess(Process process)
        {
            _processes.Add(process);
            return ++_pidCounter;
        }

        /// <summary>
        /// Unregisters a process with this simulation context.
        /// </summary>
        /// <param name="process">The process to unregister</param>
        internal void UnregisterProcess(Process process)
        {
            _processes.Remove(process);
        }

        /// <summary>
        /// Registers a signal with this simulation context.
        /// </summary>
        /// <param name="signal">The signal to register</param>
        internal void RegisterSignal(SignalBase signal)
        {
            _signals.Add(signal);
        }

        /// <summary>
        /// Unregisters a signal with this simulation context.
        /// </summary>
        /// <param name="signal">The signal to unregister</param>
        internal void UnregisterSignal(SignalBase signal)
        {
            _signals.Remove(signal);
        }

        /// <summary>
        /// Schedule a handler for future execution.
        /// </summary>
        /// <param name="proc">The handler to be scheduled</param>
        /// <param name="delta">The relative period in raw tick units</param>
        internal void Schedule(Action proc, long delta)
        {
            if (delta < 0 || proc == null)
                throw new ArgumentException();

            lock (_q)
            {
                _q.Enqueue(Ticks + delta, proc);
            }
        }

        private ThreadLocal<Process> _currentProcess = new ThreadLocal<Process>();

        /// <summary>
        /// The currently executing process.
        /// </summary>
        public Process CurrentProcess
        {
            [DoNotAnalyze]
            get
            {
                return _currentProcess.Value;
            }

            internal set
            {
                _currentProcess.Value = value;
            }
        }

        /// <summary>
        /// Represents the desired time resolution.
        /// </summary>
        /// <value>
        /// Gets or sets the time resolution in multiples of the specified value.
        /// </value>
        public Time Resolution
        {
            [StaticEvaluation]
            get
            {
                return _resolution;
            }
            [DoNotAnalyze]
            set
            {
                if (State != ESimState.Construction)
                    throw new InvalidOperationException("Resolution may only be changed during construction");

                _resolution = value;
            }
        }

        /// <summary>
        /// Requests the current design to elaborate.
        /// </summary>
        [DoNotAnalyze]
        public void Elaborate()
        {
            if (State != ESimState.Construction)
                throw new InvalidOperationException("Elaboration already done");

            while (_endOfConstructionHandlers != null)
            {
                Action handlers = _endOfConstructionHandlers;
                _endOfConstructionHandlers = null;
                handlers();
            }

            State = ESimState.Elaboration;
            if (_elaborationHandlers != null)
                _elaborationHandlers();
            State = ESimState.DesignAnalysis;
            if (_analysisHandlers != null)
                _analysisHandlers();
            if (_endOfElaborationHandlers != null)
                _endOfElaborationHandlers();

            foreach (Process process in _processes)
                process.InitLocalStorage(_numPLSSlots);

            State = ESimState.SimulationReady;
        }

        internal void WaitUntilSimulationIsRunning()
        {
            _simRunningEvent.Wait();
        }

        private void CallSimulationStoppedHandlers()
        {
            if (_simulationStoppedHandlers != null)
                _simulationStoppedHandlers();
        }

        private void CallSimulationStoppingHandlers()
        {
            if (_simulationStoppingHandlers != null)
                _simulationStoppingHandlers();
        }

        /// <summary>
        /// Represents the first exception which caused the simulation to fail.
        /// </summary>
        public Exception FailReason { [DoNotAnalyze] get; private set; }

        internal TaskScheduler Scheduler { get; private set; }
        internal TaskFactory Factory { get; private set; }

        internal void Fail(Exception reason)
        {
            State = ESimState.Failed;
            FailReason = reason;
        }

        /// <summary>
        /// Executes the simulation for a specified amount of ticks.
        /// </summary>
        /// <param name="delta">The amount of raw ticks for which the simulation should be executed</param>
        [DoNotAnalyze]
        public void Simulate(long delta)
        {
            if (State == ESimState.SimulationReady)
            {
                State = ESimState.Simulation;
                if (_startOfSimulationHandlers != null)
                    _startOfSimulationHandlers();
            }
            else if (State != ESimState.SimulationPaused &&
                State != ESimState.StopRequested)
                throw new InvalidOperationException("Simulation not ready and not paused");


            State = ESimState.Simulation; // Added
            
            _simRunningEvent.Set();
            long stopTime = Ticks + delta;
            StopTicks = stopTime;

            try
            {
                //// "if-then-else" statement added
                //// if delta = 0 (simulate 1 Delta Cycle)...
                if (Ticks == stopTime &&
                        State != ESimState.StopRequested &&
                        State != ESimState.Failed &&
                        !_q.IsEmpty)
                {
                    var first_ev = _q.Peek();

                    if (first_ev.Key == Ticks)
                    {
                        var kvp = _q.Dequeue();

                        // Time advance
                        Ticks = kvp.Key;

                        // Evaluate phase
                        kvp.Value();

                        // Update phase
                        if (_nextDeltaCycleHandlers != null)
                            _nextDeltaCycleHandlers();

                        DeltaCycleCount++;
                    }
                }
                else
                {                    
                    while (Ticks < stopTime &&
                        State != ESimState.StopRequested &&
                        State != ESimState.Failed &&
                        !_q.IsEmpty)
                    {
                        var first_ev = _q.Peek();

                        if (first_ev.Key < stopTime)
                        {
                            var kvp = _q.Dequeue();

                            // Time advance
                            Ticks = kvp.Key;

                            // Evaluate phase
                            kvp.Value();

                            // Update phase
                            if (_nextDeltaCycleHandlers != null)
                                _nextDeltaCycleHandlers();
                            
                        }
                        else
                            Ticks = stopTime;
                    }                                 
                }
                // "if" statement added
                // Check event queue
                if (_q.IsEmpty)
                {
                    Ticks = stopTime;       //  Added
                    State = ESimState.StopRequested;
                    IsPendingActivity = false;
                    IsPendingActivityAtCurrentTime = false;
                    IsPendingActivityAtFutureTime = false;
                    TimeToPendingActivity = Time.Infinite;
                    return;
                }

                // Added 
                var next_ev = _q.Peek();
                long nextTime = next_ev.Key;

                if (nextTime > Ticks)
                {
                    IsPendingActivity = true;
                    IsPendingActivityAtCurrentTime = false;
                    IsPendingActivityAtFutureTime = true;
                    TimeToPendingActivity = (nextTime - Ticks) * Resolution;
                }
                else
                {
                    IsPendingActivity = true;
                    IsPendingActivityAtCurrentTime = true;
                    IsPendingActivityAtFutureTime = false;
                    TimeToPendingActivity = 0 * Resolution;
                }
            }
            catch (Exception)
            {
            }

            if (State == ESimState.Failed ||
                State == ESimState.StopRequested)
            {
                CallSimulationStoppingHandlers();
                if (State == ESimState.Failed)
                {
                    throw FailReason;
                }
                else // (State == ESimState.StopRequested)
                {
                    CallSimulationStoppedHandlers();
                    State = ESimState.Stopped;
                }
            }
            else
            {
                State = ESimState.SimulationPaused;
            }
        }

        /// <summary>
        /// Terminates the current simulation.
        /// </summary>
        [DoNotAnalyze]
        public void EndSimulation()
        {
            State = ESimState.StopRequested;
            Simulate(1);
        }

        /// <summary>
        /// Executes the simulation for a specified amount of time.
        /// </summary>
        /// <param name="delta">the amount of time the simulation should be executed</param>
        [DoNotAnalyze]
        public void Simulate(Time delta)
        {
            if (delta.IsInfinite)
                Simulate(long.MaxValue - Ticks);
            else
                Simulate(delta.GetTicks(this)); // Added
        }

        /// <summary>
        /// Returns <c>true</c> iff the next activity will occur at current simulation time.
        /// </summary>
        public bool IsPendingActivityAtCurrentTime
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns <c>true</c> iff the next activity will occur at future simulation time.
        /// </summary>
        public bool IsPendingActivityAtFutureTime
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns <c>true</c> iff the next activity will occur at current or future simulation time.
        /// </summary>
        public bool IsPendingActivity
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns time to the earliest pending activity. If there is no activity, returns <c>Time.Infinite</c>
        /// </summary>
        public Time TimeToPendingActivity
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns the number of executed delta cycles
        /// </summary>
        public int DeltaCycleCount
        {
            get;
            private set;
        }

        internal PLSSlot AllocPLS()
        {
            if (State != ESimState.Construction)
                throw new InvalidOperationException("PLS allocation is only allowed during construction");

            int slot = _numPLSSlots++;
            return new PLSSlot(this, slot);
        }

        /// <summary>
        /// Reports a message.
        /// </summary>
        /// <param name="level">classification of message</param>
        /// <param name="message">message to report</param>
        public void Report(EIssueClass level, string message)
        {
            if (Instance.State == ESimState.DesignAnalysis)
                return;

            Console.WriteLine(level.ToString() + ": " + message);
        }

        /// <summary>
        /// Exits the current process.
        /// </summary>
        [DoNotAnalyze]
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.ExitProcess)]
        public static void ExitProcess()
        {
            if (Instance.State == ESimState.DesignAnalysis)
                return;

            Instance.CurrentProcess.Stop();
        }

        /// <summary>
        /// Suspends the current process until one of the specified events is signaled.
        /// </summary>
        /// <param name="events">The list of events to wait for</param>
        //[MapToIntrinsicFunction(IntrinsicFunction.EAction.Wait)] //FIXME
        [DoNotAnalyze]
        [Obsolete("Use instead: await event; or await (event1 | event2 | ...); or await Any(events); or await Any(event1, event2, ...);", true)]
        public static void Wait(params EventSource[] events)
        {
            ///uncommented due to [Obsolete]

            //if (Instance.State == ESimState.DesignAnalysis)
            //    return;

            //Instance.CurrentProcess.Wait(events);
        }

        /// <summary>
        /// Suspends the current process until one of the events in its sensitivity list is signaled.
        /// If the list is empty, the process is suspended forever.
        /// </summary>
        [Obsolete("Use instead: await Tick;", true)]
        public static void Wait()
        {
            ///uncommented due to [Obsolete]

            //if (Instance.State == ESimState.DesignAnalysis)
            //    return;

            //Instance.CurrentProcess.Wait();
        }

        [Obsolete("Use instead: await numTicks.Ticks(); or await NTicks(numTicks);", true)]
        public static void Wait(int numTicks)
        {
        }

        /// <summary>
        /// Waits for a number of rising edges.
        /// </summary>
        /// <param name="clk">The clock signal</param>
        /// <param name="numTicks">The number of rising edges to wait</param>
        [Obsolete("Use instead: await NRisingEdges(clk, numTicks);", true)]
        public static void WaitRising(In<StdLogic> clk, int numTicks)
        {
            ///uncommented due to [Obsolete]

            //for (int n = 0; n < numTicks; n++)
            //{
            //    do
            //    {
            //        DesignContext.Wait(clk);
            //    } while (!clk.RisingEdge());
            //}
        }

        /// <summary>
        /// Waits for a number of falling edges.
        /// </summary>
        /// <param name="clk">The clock signal</param>
        /// <param name="numTicks">The number of falling edges to wait</param>
        [Obsolete("Use instead: await NFallingEdges(clk, numTicks);", true)]
        public static void WaitFalling(In<StdLogic> clk, int numTicks)
        {
            ///uncommented due to [Obsolete]

            //for (int n = 0; n < numTicks; n++)
            //{
            //    do
            //    {
            //        DesignContext.Wait(clk);
            //    } while (!clk.FallingEdge());
            //}
        }

        /// <summary>
        /// Converts a number of signals to an event list.
        /// </summary>
        /// <param name="signals">The signal(s) to be converted</param>
        /// <returns>An appropriate event list</returns>
        [StaticEvaluation]
        public static EventSource[] MakeEventList(params IInPort[] signals)
        {
            return MakeEventList((IEnumerable<IInPort>)signals);
        }

        /// <summary>
        /// Converts a number of signals to an event list.
        /// </summary>
        /// <param name="signals">The signal(s) to be converted</param>
        /// <returns>An appropriate event list</returns>
        [StaticEvaluation]
        public static EventSource[] MakeEventList(IEnumerable<IInPort> signals)
        {
            if (signals == null ||
                signals.Any(s => s == null))
                throw new ArgumentException("null reference inside sensitivity list");

            EventSource[] events = (from IInPort signal in signals
                                      select signal.ChangedEvent).ToArray();
            return events;
        }

        internal void StopInternal()
        {
            switch (State)
            {
                case ESimState.Construction:
                case ESimState.Elaboration:
                    throw new InvalidOperationException("Model not yet elaborated");

                case ESimState.Simulation:
                case ESimState.StopRequested:
                    // inside simulation
                    State = ESimState.StopRequested;
                    CurrentProcess.Stop();
                    break; // not reached

                case ESimState.SimulationPaused:
                    // outside simulation
                    State = ESimState.StopRequested;
                    CallSimulationStoppedHandlers();
                    State = ESimState.Stopped;
                    break;

                case ESimState.Stopped:
                    throw new InvalidOperationException("Simulation already stopped");

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Stops the current simulation.
        /// </summary>
        [DoNotAnalyze]
        public static void Stop()
        {
            Instance.StopInternal();
        }

        /// <summary>
        /// The type library of this context
        /// </summary>
        public TypeLibrary TypeLib
        {
            [StaticEvaluation]
            get { return Descriptor.TypeLib; }
        }

        /// <summary>
        /// Performs a behavioral analysis on the design.
        /// </summary>
        [DoNotAnalyze]
        public void CompleteAnalysis()
        {
            if (State == ESimState.DesignAnalysisCompleted)
                return;

            State = ESimState.DesignAnalysis;
            BehavioralAnalyzer.DoBehavioralAnalysis(this);
            State = ESimState.DesignAnalysisCompleted;
        }

        internal void RunRefinements(IProject targetProject)
        {
            State = ESimState.DesignAnalysis;
            while (_refinementQ.Any())
            {
                BeginRefinement();
                IRefinementCycle refine = _refinementQ.Dequeue();
                refine.Refine(this, targetProject);
                Elaborate();
                State = ESimState.DesignAnalysis;
                BehavioralAnalyzer.DoBehavioralAnalysis(this);
            }
            State = ESimState.RefinementsCompleted;
        }

        /// <summary>
        /// The descriptor of this design
        /// </summary>
        public DesignDescriptor Descriptor { get; private set; }

        /// <summary>
        /// The descriptor of this design
        /// </summary>
        DescriptorBase IDescriptive.Descriptor
        {
            get { return Descriptor; }
        }

        private void ClaimTopLevelComponents()
        {
            int num = 0;
            foreach (Component component in Components)
            {
                if (component.Descriptor.Owner == null)
                    Descriptor.AddChild(component.Descriptor, "top" + num);
                num++;
            }
        }

        private int _currentRefinementCycle;
        
        /// <summary>
        /// Refinement cycle counter
        /// </summary>
        public int CurrentRefinementCycle
        {
            get { return _currentRefinementCycle; }
        }

        /// <summary>
        /// Begins a refinement cycle, i.e. a design modification after analysis.
        /// </summary>
        public void BeginRefinement()
        {
            State = ESimState.Construction;
            _endOfConstructionHandlers = null;
            _endOfElaborationHandlers = null;
            _elaborationHandlers = null;

            _analysisHandlers = null;
            _endOfElaborationHandlers = null;
            ++_currentRefinementCycle;
        }

        /// <summary>
        /// Pushes the current design context onto a global stack and makes a clone of that context the current context.
        /// </summary>
        public static void Push()
        {
            _ctxStack.Push(Instance);
            Instance = new DesignContext();
        }

        /// <summary>
        /// Discards the current design context and restores the top element from the global design context stack as current design context.
        /// </summary>
        public static void Pop()
        {
            Instance = _ctxStack.Pop();
        }

        /// <summary>
        /// Enqueues a design refinement.
        /// </summary>
        /// <param name="refinement">refinement implementation</param>
        public void QueueRefinement(IRefinementCycle refinement)
        {
            _refinementQ.Enqueue(refinement);
        }

        private FixedPointSettings _fixPointSettings;

        /// <summary>
        /// Configuration parameters for fixed point arithmetic
        /// </summary>
        public FixedPointSettings FixPoint
        {
            get { return _fixPointSettings; }
        }

        private bool _suppressConsoleOutput;

        /// <summary>
        /// Whether to suppress <c>WriteLine(...)</c> messages.
        /// </summary>
        public static bool SuppressConsoleOutput
        {
            get { return Instance._suppressConsoleOutput; }
            set { Instance._suppressConsoleOutput = value; }
        }

        /// <summary>
        /// Writes text to the standard output, if <c>SuppressConsoleOutput</c> is <c>false</c>.
        /// </summary>
        /// <param name="line">text to write</param>
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.ReportLine)]
        public static void WriteLine(string line)
        {
            if (!SuppressConsoleOutput)
                Console.WriteLine(line);
        }

        /// <summary>
        /// Whether to capture additional (memory-intensive) tracing information on the creation of design objects.
        /// </summary>
        public bool CaptureDesignObjectOrigins { get; set; }
    }

    /// <summary>
    /// Interface for a design refinement
    /// </summary>
    public interface IRefinementCycle
    {
        /// <summary>
        /// Executes the desired refinement.
        /// </summary>
        /// <param name="context">design context being subject to refinement</param>
        /// <param name="targetProject">code generation target project</param>
        void Refine(DesignContext context, IProject targetProject);
    }
}

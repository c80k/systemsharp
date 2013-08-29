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
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// The abstract base class for defining components.
    /// </summary>
    public class Component :
        DesignObject,
        IDescriptive<ComponentDescriptor>,
        IContainmentImplementor
    {
        private class ComponentBehaviorComparer : IEqualityComparer<Component>
        {
            public bool Equals(Component x, Component y)
            {
                return x.IsEquivalent(y);
            }

            public int GetHashCode(Component obj)
            {
                return obj.GetBehaviorHashCode();
            }
        }

        public static readonly IEqualityComparer<Component> BehaviorComparer = new ComponentBehaviorComparer();
        private static readonly Event[] emptyEvents = new Event[0];

        /// <summary>
        /// Constructs a component instance.
        /// </summary>
        public Component()
        {
            Context.OnElaborate += Initialize;
            Context.OnEndOfConstruction += PreInitialize;
            Context.OnEndOfElaboration += PostInitialize;
            Context.OnAnalysis += OnAnalysis;
            Context.OnSimulationStopped += OnSimulationStopped;
            Context.RegisterComponent(this);
            Descriptor = new ComponentDescriptor(this);
            AutoBinder = new DefaultAutoBinder(this);
            Representant = this;
        }

        /// <summary>
        /// The containing component of this component.
        /// </summary>
        public Component Parent { [StaticEvaluation] get; private set; }

        #region await helper members
        /// <summary>
        /// Represents one tick
        /// </summary>
        public static PredicatedEvent Tick 
        { 
            [TickAttribute]
            get 
            {
                var curps = DesignContext.Instance.CurrentProcess;
                var sens = curps.Sensitivity;
                var pred = curps.Predicate;
                if (sens == null || pred == null)
                    throw new InvalidOperationException("Process " + curps.Name + " has no associated sensitivity and predicate. Tick is only allowed for clocked processes.");

                return new PredicatedEvent(null,
                    new MultiEvent(null, sens), pred);
            } 
        }

        /// <summary>
        /// Represents a number of ticks
        /// </summary>
        /// <returns></returns>
        [MapToWaitNTicksRewriteAwait]
        [MapToWaitNTicksRewriteCall]
        public static async Task NTicks(int numTicks) 
        { 
            await numTicks.Ticks(); 
        }
        
        /// <summary>
        /// Represents one rising edges.
        /// </summary>
        /// <param name="clk">The clock signal</param>
        [StaticEvaluationDoNotAnalyze]
        public static PredicatedEvent RisingEdge(In<StdLogic> clk)
        {
            return new PredicatedEvent(null, new MultiEvent(null, DesignContext.MakeEventList(clk)), clk.RisingEdge);
        }

        /// <summary>
        /// Represents a number of rising edges.
        /// </summary>
        /// <param name="clk">The clock signal</param>
        [StaticEvaluationDoNotAnalyze]
        public static async Task NRisingEdges(In<StdLogic> clk, int numTicks)
        {
            for (int n = 0; n < numTicks; n++)
            {
                await RisingEdge(clk);
            }
        }

        /// <summary>
        /// Represents one falling edges.
        /// </summary>
        /// <param name="clk">The clock signal</param>
        [StaticEvaluationDoNotAnalyze]
        public static PredicatedEvent FallingEdge(In<StdLogic> clk)
        {
            return new PredicatedEvent(null, new MultiEvent(null, DesignContext.MakeEventList(clk)), clk.FallingEdge);
        }

        /// <summary>
        /// Represents a number of falling edges.
        /// </summary>
        /// <param name="clk">The clock signal</param>
        [StaticEvaluationDoNotAnalyze]
        public static async Task NFallingEdges(In<StdLogic> clk, int numTicks)
        {
            for (int n = 0; n < numTicks; n++)
            {
                await FallingEdge(clk);
            }
        }

        /// <summary>
        /// A combinatory method for signals.
        /// </summary>
        [StaticEvaluationDoNotAnalyze]
        public static MultiEvent Any(params IInPort[] signals)
        {
            return Any(DesignContext.MakeEventList(signals));
        }

        /// <summary>
        /// A combinatory method for events.
        /// </summary>
        [StaticEvaluationDoNotAnalyze]
        public static MultiEvent Any(params AbstractEvent[] events)
        {
            return new MultiEvent(null, events);
        }
        #endregion

        /// <summary>
        /// Override this method to perform initialization tasks, such as registering a process.
        /// </summary>
        virtual protected void Initialize()
        {
        }

        /// <summary>
        /// This method gets called prior to Initialize().
        /// </summary>
        virtual protected void PreInitialize()
        {
        }

        /// <summary>
        /// This method gets called after Initialize().
        /// </summary>
        virtual protected void PostInitialize()
        {
        }

        /// <summary>
        /// This method gets called when the simulation stopped. Overwrite it to perform any cleanup tasks.
        /// </summary>
        virtual protected void OnSimulationStopped()
        {
        }

        protected void Bind(Action bindAction)
        {
            Context.OnEndOfConstruction += bindAction;
        }

        /// <summary>
        /// Registers a triggered process.
        /// </summary>
        /// <param name="func">The process body</param>
        /// <param name="sensitive">The sensitivity list</param>
        protected void AddProcess(Action func, params AbstractEvent[] sensitive)
        {
            Process process = new Process(
                this,
                Process.EProcessKind.Triggered,
                func)
            {
                Sensitivity = sensitive
            };
            Context.RegisterProcess(process);
            process.Schedule(0);
        }

        /// <summary>
        /// Registers a triggered process.
        /// </summary>
        /// <param name="func">The process body</param>
        /// <param name="sensitive">The sensitivity list</param>
        protected void AddProcess(Action func, params IInPort[] sensitive)
        {
            AddProcess(func, DesignContext.MakeEventList(sensitive));
        }

        /// <summary>
        /// Registers a triggered process without sensitivity. The process gets executed after the beginning of the simulation.
        /// </summary>
        /// <param name="func">The process body</param>
        protected void AddProcess(Action func)
        {
            Process process = new Process(
                this,
                Process.EProcessKind.Triggered,
                func);
            Context.RegisterProcess(process);
            process.Schedule(0);
        }

        /// <summary>
        /// Registers a threaded process.
        /// </summary>
        /// <param name="func">The process body</param>
        protected void AddThread(Action func)
        {
            Process process = new Process(
                this,
                Process.EProcessKind.Threaded,
                func);
            Context.RegisterProcess(process);
            process.Schedule(0);
        }

        protected Process AddClockedThreadInternal(Action func, Func<bool> predicate, params IInPort[] sensitive)
        {
            Process process = new Process(
                this,
                Process.EProcessKind.Threaded,
                func)
            {
                Predicate = predicate,
                Sensitivity = DesignContext.MakeEventList(sensitive)
            };
            Context.RegisterProcess(process);
            process.Schedule(0);
            return process;
        }

        protected void AddClockedThread(Action func, Func<bool> predicate, params IInPort[] sensitive)
        {
            AddClockedThreadInternal(func, predicate, sensitive);
        }

        /// <summary>
        /// Changes a triggered process sensitivity list and request it to be scheduled in the future.
        /// </summary>
        /// <param name="delta">The desired relative period when the process should be scheduled again.</param>
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Wait)]
        protected void NextTrigger(Time delta)
        {
            Process curp = Context.CurrentProcess;
            curp.Sensitivity = emptyEvents;
            curp.Schedule(delta.GetTicks(Context));
        }

        /// <summary>
        /// Changes a process sensitivity list.
        /// </summary>
        /// <param name="sensitive">The new sensitivity list</param>
        protected void NextTrigger(params AbstractEvent[] sensitive)
        {
            Process curp = Context.CurrentProcess;
            curp.Sensitivity = sensitive;
        }

        /// <summary>
        /// Changes a process sensitivity list.
        /// </summary>
        /// <param name="sensitive">The new sensitivity list</param>
        protected void NextTrigger(params IInPort[] sensitive)
        {
            Process curp = Context.CurrentProcess;
            curp.Sensitivity = DesignContext.MakeEventList(sensitive);
        }

        private static IEnumerable<FieldInfo> FindFields(Type type)
        {
            if (type != null)
            {
                return type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly)
                    .Concat(FindFields(type.BaseType));
            }
            else
            {
                return Enumerable.Empty<FieldInfo>();
            }
        }

        private static IEnumerable<PropertyInfo> FindProperties(Type type)
        {
            if (type != null)
            {
                return type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly)
                    .Concat(FindProperties(type.BaseType));
            }
            else
            {
                return Enumerable.Empty<PropertyInfo>();
            }
        }

        /// <summary>
        /// This method is called upon model analysis.
        /// </summary>
        /// <remarks>
        /// The standard implementation uses reflection to inspect all fields and registers them to the component descriptor.
        /// </remarks>
        virtual protected void OnAnalysis()
        {
            Type type = GetType();
            var fis = FindFields(type);
            foreach (FieldInfo fi in fis)
            {
                // Skip backing fields of properties (these are mapped to ports)
                // There is currently no elegant way of doing this. The only possibility is to check whether the
                // field name contains a "magic" character which is usually not allowed as an identifier name.
                if (fi.Name.Contains('<'))
                    continue;

                object fvalue = fi.GetValue(this);
                IContainmentImplementor ici = fvalue as IContainmentImplementor;
                if (ici != null)
                {
                    ici.SetOwner(Descriptor, fi, IndexSpec.Empty);
                }
            }

            var pis = FindProperties(type);
            foreach (PropertyInfo pi in pis)
            {
                Type propType = pi.PropertyType;
                RewriteDeclaration rwDecl = (RewriteDeclaration)Attribute.GetCustomAttribute(
                    propType, typeof(RewriteDeclaration), true);
                if (rwDecl != null)
                    rwDecl.ImplementDeclaration(Descriptor, pi);
            }
        }

        /// <summary>
        /// Internal method which is called upon synthesis.
        /// </summary>
        /// <param name="ctx">The synthesis context</param>
        internal void OnSynthesisInternal(ISynthesisContext ctx)
        {
            OnSynthesis(ctx);
        }

        /// <summary>
        /// This method is called upon synthesis. Overwrite it to perform custom synthesis tasks, such as generating
        /// a core generator script.
        /// </summary>
        /// <param name="ctx">The synthesis context</param>
        virtual protected void OnSynthesis(ISynthesisContext ctx)
        {
            ctx.DoBehavioralAnalysisAndGenerate(this);
        }

        /// <summary>
        /// The associated descriptor object.
        /// </summary>
        public ComponentDescriptor Descriptor { get; internal set; }

        DescriptorBase IDescriptive.Descriptor
        {
            get { return Descriptor; }
        }

        public virtual void SetOwner(DescriptorBase owner, MemberInfo declSite, IndexSpec indexSpec)
        {
            IComponentDescriptor cowner = (IComponentDescriptor)owner;
            FieldInfo field = (FieldInfo)declSite;
            if (Descriptor.Owner == null)
            {
                owner.AddChild(Descriptor, field, indexSpec);
            }
            else
            {
                Context.Report(EIssueClass.Error, "Component instance of " + GetType().Name + " is declared multiple times: " +
                    "first declaration is " + Descriptor.GetFullName() + ", second one in " + cowner.GetFullName() + ", field " +
                    declSite.Name);
            }
        }

        public IAutoBinder AutoBinder { get; set; }

        /// <summary>
        /// Tells whether this component is equivalent to another one.
        /// Equivalent means that both components have the same interface with the same ports, 
        /// port types and port type parameters and that moreover both behave in the same way.
        /// This check cannot be done in general (refer to the halting problem). Instead, it is up
        /// to the designer to implement specialized checks for derived components. The base
        /// implementation returns true only if it is compared to itself. IsEquivalent must use
        /// conservative assumptions. I.e. it may return "false" if in doubt, but must never return
        /// "true" if it cannot gurantee that the other component behaves identically.
        /// </summary>
        /// <param name="other">The component to compoare</param>
        /// <returns>true, if this component is equivalent to the other one, false if not (or if in doubt)</returns>
        public virtual bool IsEquivalent(Component component)
        {
            return component == this;
        }

        /// <summary>
        /// returns a hash code such that for any two components c1 and c2 the following condition holds:
        /// c1.GetBehaviorHashCode() != c2.GetBehaviorHashCode() implies !c1.IsEquivalent(c2)
        /// </summary>
        /// <returns>the hash code</returns>
        public virtual int GetBehaviorHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        /// <summary>
        /// A representative component such that IsEquivalent(Representant) is fulfilled.
        /// </summary>
        public Component Representant { get; internal set; }
    }

}
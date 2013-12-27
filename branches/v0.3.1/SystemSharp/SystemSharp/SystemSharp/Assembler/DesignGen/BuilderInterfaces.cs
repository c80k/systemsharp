/**
 * Copyright 2012-2013 Christian Köllner
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
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;

namespace SystemSharp.Assembler.DesignGen
{
    /// <summary>
    /// Factory pattern for interconnect builder algorithms
    /// </summary>
    public interface IInterconnectBuilderFactory
    {
        /// <summary>
        /// Creates an interconnect builder instance
        /// </summary>
        /// <param name="host">component to host the interconnect</param>
        /// <param name="binder">binder service</param>
        /// <returns>an interconnect builder instance</returns>
        IInterconnectBuilder Create(Component host, IAutoBinder binder);
    }

    /// <summary>
    /// Generic interface of interconnect builder algorithms
    /// </summary>
    /// <remarks>
    /// Interconnect construction follows scheduling and resource allocation during high-level synthesis. An implementation a such an algorithm
    /// essentially takes a specification of timed data flows between functional units as input and allocates registers to implement those
    /// data flows. The input flow matrix therefore contains flows to abstract data endpoints, which must be replaced by flows to concrete
    /// registers by the algorithm. A famous example is the wire routing algorithm of Hashimoto and Stevens which minimizes the number of
    /// required registers when applied to interconnect allocation.
    /// </remarks>
    public interface IInterconnectBuilder
    {
        /// <summary>
        /// Constructs interconnect based on a flow matrix
        /// </summary>
        /// <param name="flowSpec">input flow matrix describing the timed data flows between functional units</param>
        /// <param name="detailedFlow">output flow matrix describing the timed data flows between functional units and registers</param>
        void CreateInterconnect(FlowMatrix flowSpec, FlowMatrix detailedFlow);
    }

    /// <summary>
    /// Factory pattern for control path builder algorithms
    /// </summary>
    public interface IControlpathBuilderFactory
    {
        /// <summary>
        /// Creates a control path builder instance
        /// </summary>
        /// <param name="host">component to host the control path</param>
        /// <param name="binder">binder service</param>
        /// <returns>a control path builder instance</returns>
        IControlpathBuilder Create(Component host, IAutoBinder binder);
    }

    /// <summary>
    /// Generic interface of control path construction algorithms
    /// </summary>
    /// <remarks>
    /// Control path construction is the instantiation of an appropriate datapath controller for a fully constructed datapath. The purpose
    /// of the controller is to reconfigure the datapath in each c-step as prescribed by the flow matrix. More specifically, it directs
    /// the flows between each data source and sink. A good example architecture is the explicit finite state machine implementation.
    /// </remarks>
    public interface IControlpathBuilder
    {
        /// <summary>
        /// This method is called at a very early stage. It gives the control path builder a chance to customize the synthesis plan.
        /// It might be especially required to define the way how conditional and unconditional branches are mapped to functional units,
        /// since these need to interact tightly with the controller.
        /// </summary>
        /// <param name="plan"></param>
        void PersonalizePlan(HLSPlan plan);

        /// <summary>
        /// This method is called prior to resource allocation, after scheduling. It notifies the control path builder on the number of
        /// c-steps to let it prepare according data structures.
        /// </summary>
        /// <param name="cstepCount">number of c-steps which were determined by scheduling</param>
        void PrepareAllocation(long cstepCount);

        /// <summary>
        /// Creates the actual controller hardware based on the flow matrix
        /// </summary>
        /// <param name="flowSpec">flow matrix describing the timed dataflows between functional units and registers</param>
        /// <param name="procName">a unique name which may be used to prefix model elements which are created by the algorithm 
        /// (to avoid naming collisions)</param>
        void CreateControlpath(FlowMatrix flowSpec, string procName);
    }

    /// <summary>
    /// Factory pattern for datapath builder algorithms
    /// </summary>
    public interface IDatapathBuilderFactory
    {
        /// <summary>
        /// Creates a datapath builder
        /// </summary>
        /// <param name="host">component to host the datapath</param>
        /// <returns>a data path builder instance</returns>
        IDatapathBuilder Create(Component host);
    }

    /// <summary>
    /// Generic interface of datapath builders
    /// </summary>
    /// <remarks>
    /// A datapath builder does not perform any optimizations. It is a rather stupid piece of code which just takes care
    /// that a newly allocated functional unit gets correctly inserted and wired to the hosting component.
    /// </remarks>
    public interface IDatapathBuilder
    {
        /// <summary>
        /// Returns a binding service suitable for control path builders
        /// </summary>
        IAutoBinder FUBinder { get; }

        /// <summary>
        /// Returns a binding service suitable for interconnect builders
        /// </summary>
        IAutoBinder ICBinder { get; }

        /// <summary>
        /// Adds a newly allocated functional unit to the design
        /// </summary>
        /// <param name="fu">instance of functional unit</param>
        void AddFU(Component fu);
    }
}

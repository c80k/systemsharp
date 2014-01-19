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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// The common interface of all ports.
    /// </summary>
    public interface IPort : IExpressive
    {
    }

    /// <summary>
    /// This interface models a port with inbound dataflow.
    /// </summary>
    public interface IInPort : IPort
    {
        /// <summary>
        /// Returns the event which is signaled when there is new data or when the data changed its value.
        /// </summary>        
        EventSource ChangedEvent { [SignalProperty(SignalRef.EReferencedProperty.ChangedEvent)] get; }
    }

    /// <summary>
    /// This interface models a port with outbound dataflow.
    /// </summary>
    public interface IOutPort : IPort
    {
    }

    /// <summary>
    /// This interface models a port with bi-directional dataflow.
    /// </summary>
    public interface IInOutPort : IInPort, IOutPort
    {
    }

}

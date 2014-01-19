/**
 * Copyright 2011-2012 Christian Köllner
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
using System.Text;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// Mode flags for signal creation/retrieval by binder service
    /// </summary>
    [Flags]
    public enum EBinderFlags
    {
        /// <summary>
        /// An existing signal must be used.
        /// </summary>
        ExistingSignal = 1,

        /// <summary>
        /// A new signal must be created.
        /// </summary>
        CreateNewSignal = 2,

        /// <summary>
        /// If already created, use existing signal, otherwise create new signal.
        /// </summary>
        UseExistingOrCreateSignal = 3,

        /// <summary>
        /// Create a new port for request.
        /// </summary>
        CreateNewPort = 4,

        /// <summary>
        /// Input direction
        /// </summary>
        In = 8,

        /// <summary>
        /// Output direction
        /// </summary>
        Out = 16,

        /// <summary>
        /// Mixed input and output
        /// </summary>
        InOut = 24
    }

    /// <summary>
    /// Binder service interface
    /// </summary>
    /// <remarks>
    /// The purpose of a binder service is to create new or retrieve existing signals, ports, processes or types within a specific context
    /// of a hosting component.
    /// </remarks>
    public interface IAutoBinder
    {
        /// <summary>
        /// Retrieves an existing or creates a new signal/port, depending on specified flags.
        /// </summary>
        /// <param name="flags">creation flags</param>
        /// <param name="portUsage">intended usage of model element</param>
        /// <param name="name">name of created model element</param>
        /// <param name="domainID">reserved for future extensions</param>
        /// <param name="initialValue">initial data value</param>
        ISignalOrPortDescriptor GetSignal(EBinderFlags flags, EPortUsage portUsage, string name, string domainID, object initialValue);

        /// <summary>
        /// Creates a new signal.
        /// </summary>
        /// <param name="portUsage">intended usage of model element</param>
        /// <param name="portName">desired name</param>
        /// <param name="domainID">reserved for future extensions</param>
        /// <param name="initialValue">initial data value</param>
        SignalBase GetSignal(EPortUsage portUsage, string portName, string domainID, object initialValue);

        /// <summary>
        /// Creates a new process.
        /// </summary>
        /// <param name="kind">kind of process</param>
        /// <param name="func">behavior of process</param>
        /// <param name="sensitivity">sensitivity list of process</param>
        ProcessDescriptor CreateProcess(Process.EProcessKind kind, Function func, params ISignalOrPortDescriptor[] sensitivity);

        /// <summary>
        /// Creates a new enumeration type.
        /// </summary>
        /// <param name="name">desired name</param>
        /// <param name="literals">desired literals</param>
        TypeDescriptor CreateEnumType(string name, IEnumerable<string> literals);
    }

    public static class AutoBinderExtensions
    {
        /// <summary>
        /// Creates a typed signal.
        /// </summary>
        /// <typeparam name="T">type of signal data</typeparam>
        /// <param name="binder">binder service</param>
        /// <param name="portUsage">intended usage</param>
        /// <param name="portName">desired name</param>
        /// <param name="domainID">reserved for future extensions</param>
        /// <param name="initialValue">initial data value</param>
        public static Signal<T> GetSignal<T>(this IAutoBinder binder, EPortUsage portUsage, string portName, string domainID, T initialValue)
        {
            return (Signal<T>)binder.GetSignal(portUsage, portName, domainID, initialValue);
        }

        /// <summary>
        /// Retrieves an existing or creates a new typed signal/port, depending on specified flags.
        /// </summary>
        /// <typeparam name="T">type of signal data</typeparam>
        /// <param name="binder">binder service</param>
        /// <param name="flags">creation flags</param>
        /// <param name="portUsage">intended usage</param>
        /// <param name="portName">desired name</param>
        /// <param name="domainID">reserved for future extensions</param>
        /// <param name="initialValue">initial data value</param>
        public static Signal<T> GetSignal<T>(this IAutoBinder binder, EBinderFlags flags, EPortUsage portUsage, string portName, string domainID, T initialValue)
        {
            return (Signal<T>)binder.GetSignal(flags, portUsage, portName, domainID, initialValue);
        }
    }

    /// <summary>
    /// A default implementation of the binder service
    /// </summary>
    public class DefaultAutoBinder : IAutoBinder
    {
        private Component _host;
        private int _id;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="host">hosting component</param>
        public DefaultAutoBinder(Component host)
        {
            _host = host;
        }

        /// <summary>
        /// The default clock signal
        /// </summary>
        /// <remarks>
        /// You can define a particular clock signal by assigning the property. If you don't assign this property,
        /// you will leave it up to the default implementation to determine the clock signal. It will search all ports of the
        /// component and select the first one whose <c>Usage</c> property is set to <c>EPortUsage.Clock</c>.
        /// </remarks>
        private In<StdLogic> _defaultClock;
        public In<StdLogic> DefaultClock 
        {
            get
            {
                if (_defaultClock == null)
                {
                    var clockPorts = _host.Descriptor.GetPorts()
                        .Where(pd => pd.Usage == EPortUsage.Clock)
                        .OrderBy(pd => pd.Domain == null ? "" : pd.Domain);

                    if (clockPorts.Any())
                    {
                        SignalDescriptor sdClock = clockPorts.First().BoundSignal as SignalDescriptor;
                        if (sdClock != null)
                            _defaultClock = sdClock.Instance as In<StdLogic>;
                    }
                }
                return _defaultClock;
            }
            set
            {
                _defaultClock = value;
            }
        }

        private SignalBase FindClock(string domain)
        {
            var clockPorts = _host.Descriptor.GetPorts()
                .Where(pd => pd.Usage == EPortUsage.Clock && pd.Domain == domain)
                .Select(pd => ((SignalDescriptor)pd.BoundSignal).Instance);

            return clockPorts.First();
        }

        #region IAutoBinder Member

        public SignalBase GetSignal(EPortUsage portUsage, string portName, string domainID, object initialValue)
        {
            if (portUsage == EPortUsage.Clock)
            {
                return FindClock(domainID);
            }
            else if (portUsage == EPortUsage.State)
            {
                throw new NotSupportedException("State signal is not supported by default auto-binder");
            }
            else
            {
                int id = _id++;
                return _host.Descriptor.CreateSignalInstance("ags" + id + "_" + portName, initialValue).Instance;
            }
        }

        public ISignalOrPortDescriptor GetSignal(EBinderFlags flags, EPortUsage portUsage, string name, string domainID, object initialValue)
        {
            if (portUsage == EPortUsage.Clock)
            {
                return FindClock(domainID).Descriptor;
            }
            else if (portUsage == EPortUsage.State)
            {
                throw new NotSupportedException("State signal is not supported by default auto-binder");
            }
            else
            {
                if (flags.HasFlag(EBinderFlags.ExistingSignal))
                {
                    var result = _host.Descriptor.FindSignal(name);
                    if (result != null)
                        return null;
                }

                if (flags.HasFlag(EBinderFlags.CreateNewPort))
                {
                    EFlowDirection dir = EFlowDirection.In;
                    if (flags.HasFlag(EBinderFlags.In))
                        dir = EFlowDirection.In;
                    if (flags.HasFlag(EBinderFlags.Out))
                        dir = EFlowDirection.Out;
                    if (flags.HasFlag(EBinderFlags.InOut))
                        dir = EFlowDirection.InOut;
                    int id = _id++;
                    var port = _host.Descriptor.CreatePort("agp" + id + "_" + name, dir, 
                        TypeDescriptor.GetTypeOf(initialValue));
                    return port;
                }

                if (flags.HasFlag(EBinderFlags.CreateNewSignal))
                {
                    int id = _id++;
                    var signal = _host.Descriptor.CreateSignalInstance("ags" + id + "_" + name,
                        TypeDescriptor.GetTypeOf(initialValue));
                    return signal;
                }

                return null;
            }
        }

        public ProcessDescriptor CreateProcess(Process.EProcessKind kind, Function func, params ISignalOrPortDescriptor[] sensitivity)
        {
            return _host.Descriptor.CreateProcess(kind, func, sensitivity);
        }

        public TypeDescriptor CreateEnumType(string name, IEnumerable<string> literals)
        {
            return _host.Descriptor.GetDesign().CreateEnum(name, literals);
        }

        #endregion
    }
}

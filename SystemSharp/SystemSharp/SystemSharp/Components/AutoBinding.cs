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
    [Flags]
    public enum EBinderFlags
    {
        ExistingSignal = 1,
        CreateNewSignal = 2,
        UseExistingOrCreateSignal = 3,
        CreateNewPort = 4,
        In = 8,
        Out = 16,
        InOut = 24
    }

    public interface IAutoBinder
    {
        ISignalOrPortDescriptor GetSignal(EBinderFlags flags, EPortUsage portUsage, string name, string domainID, object initialValue);
        SignalBase GetSignal(EPortUsage portUsage, string portName, string domainID, object initialValue);
        ProcessDescriptor CreateProcess(Process.EProcessKind kind, Function func, params ISignalOrPortDescriptor[] sensitivity);
        TypeDescriptor CreateEnumType(string name, IEnumerable<string> literals);
    }

    public static class AutoBinderExtensions
    {
        public static Signal<T> GetSignal<T>(this IAutoBinder binder, EPortUsage portUsage, string portName, string domainID, T initialValue)
        {
            return (Signal<T>)binder.GetSignal(portUsage, portName, domainID, initialValue);
        }

        public static Signal<T> GetSignal<T>(this IAutoBinder binder, EBinderFlags flags, EPortUsage portUsage, string portName, string domainID, T initialValue)
        {
            return (Signal<T>)binder.GetSignal(flags, portUsage, portName, domainID, initialValue);
        }
    }

    public class DefaultAutoBinder : IAutoBinder
    {
        private Component _host;
        private int _id;

        public DefaultAutoBinder(Component host)
        {
            _host = host;
        }

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
                    EPortDirection dir = EPortDirection.In;
                    if (flags.HasFlag(EBinderFlags.In))
                        dir = EPortDirection.In;
                    if (flags.HasFlag(EBinderFlags.Out))
                        dir = EPortDirection.Out;
                    if (flags.HasFlag(EBinderFlags.InOut))
                        dir = EPortDirection.InOut;
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

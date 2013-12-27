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
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.DesignGen
{
    /// <summary>
    /// This class provides a default implementation of the IDatapathBuilder interface. By the way, it is questionable whether
    /// there will ever be a different implementation required.
    /// </summary>
    public class DefaultDatapathBuilder: IDatapathBuilder
    {
        private class FUBinderProxy : IAutoBinder
        {
            private DefaultDatapathBuilder _dpb;
            private IAutoBinder _orgBinder;
            private SignalBase _stateSignal;

            public FUBinderProxy(DefaultDatapathBuilder dpb, IAutoBinder orgBinder)
            {
                _dpb = dpb;
                _orgBinder = orgBinder;
            }

            public SignalBase GetSignal(EPortUsage portUsage, string portName, string domainID, object initialValue)
            {
                switch (portUsage)
                {
                    case EPortUsage.Clock:
                        return _dpb._clk;

                    case EPortUsage.State:
                        if (_stateSignal == null)
                        {
                            if (initialValue == null)
                                throw new InvalidOperationException("Need initial value of state signal in order to determine its type.");
                            string id = _dpb._psName + "_" + portName;
                            _stateSignal = _dpb._host.Descriptor.CreateSignalInstance(id, initialValue).Instance;
                        }
                        return _stateSignal;

                    default:
                        {
                            string id = _dpb._psName + "_" + portName + "_fuPin" + _dpb._nextFUPinIndex;
                            var result = (SignalBase)_dpb._host.Descriptor.CreateSignalInstance(id, initialValue).Instance;
                            ++_dpb._nextFUPinIndex;
                            return result;
                        }
                }
            }

            public ProcessDescriptor CreateProcess(Process.EProcessKind kind, Function func, params ISignalOrPortDescriptor[] sensitivity)
            {
                func.Name = _dpb._psName + "_FuPs" + _dpb._nextFUProcessIndex + "_" + func.Name;
                var result = _orgBinder.CreateProcess(kind, func, sensitivity);
                ++_dpb._nextFUProcessIndex;
                return result;
            }

            public TypeDescriptor CreateEnumType(string name, IEnumerable<string> literals)
            {
                return _dpb._host.Descriptor.GetDesign().CreateEnum(name, literals);
            }

            public ISignalOrPortDescriptor GetSignal(EBinderFlags flags, EPortUsage portUsage, string name, string domainID, object initialValue)
            {
                return _orgBinder.GetSignal(flags, portUsage, name, domainID, initialValue);
            }
        }

        private class ICRBinderProxy : IAutoBinder
        {
            private DefaultDatapathBuilder _dpb;
            private IAutoBinder _orgBinder;

            public ICRBinderProxy(DefaultDatapathBuilder dpb, IAutoBinder orgBinder)
            {
                _dpb = dpb;
                _orgBinder = orgBinder;
            }

            public SignalBase GetSignal(EPortUsage portUsage, string portName, string domainID, object initialValue)
            {
                switch (portUsage)
                {
                    case EPortUsage.Clock:
                        return _dpb._clk;

                    case EPortUsage.State:
                        throw new InvalidOperationException("State signal is not available to interconnect builder");

                    default:
                        {
                            string id = _dpb._psName + "_icr" + _dpb._nextICRIndex + "_" + portName;
                            var result = (SignalBase)_dpb._host.Descriptor.CreateSignalInstance(id, initialValue).Instance;
                            ++_dpb._nextICRIndex;
                            return result;
                        }
                }
            }

            public ProcessDescriptor CreateProcess(Process.EProcessKind kind, Function func, params ISignalOrPortDescriptor[] sensitivity)
            {
                func.Name = _dpb._psName + "_FuPs" + _dpb._nextFUProcessIndex + "_" + func.Name;
                var result = _orgBinder.CreateProcess(kind, func, sensitivity);
                ++_dpb._nextFUProcessIndex;
                return result;
            }

            public TypeDescriptor CreateEnumType(string name, IEnumerable<string> literals)
            {
                return _dpb._host.Descriptor.GetDesign().CreateEnum(name, literals);
            }

            public ISignalOrPortDescriptor GetSignal(EBinderFlags flags, EPortUsage portUsage, string name, string domainID, object initialValue)
            {
                return _orgBinder.GetSignal(flags, portUsage, name, domainID, initialValue);
            }
        }

        private Component _host;
        private SignalBase _clk;
        private string _psName;
        private int _nextFUPinIndex;
        private int _nextFUIndex;
        private int _nextICRIndex;
        private int _nextFUProcessIndex;

        public DefaultDatapathBuilder(Component host, SignalBase clk, string processName)
        {
            _host = host;
            _clk = clk;
            _psName = processName;
            FUBinder = new FUBinderProxy(this, host.AutoBinder);
            ICBinder = new ICRBinderProxy(this, host.AutoBinder);
        }

        public void AddFU(Component fu)
        {
            if (fu != _host)
            {
                string name = fu.GetType().Name;
                var fufu = fu as FunctionalUnit;
                if (fufu != null)
                    name = fufu.DisplayName;
                string id = _psName + "_" + name + "_fu" + _nextFUIndex;
                _host.Descriptor.AddChild(fu.Descriptor, id);
                ++_nextFUIndex;
            }
        }

        public IAutoBinder FUBinder { get; private set; }
        public IAutoBinder ICBinder { get; private set; }
    }
}

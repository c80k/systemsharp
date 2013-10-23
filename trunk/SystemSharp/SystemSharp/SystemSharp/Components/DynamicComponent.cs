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
using SystemSharp.Meta;

namespace SystemSharp.Components
{
    /// <summary>
    /// Experimental class for components supporting dynamic creation of ports and signals. Not really mature.
    /// </summary>
    public class DynamicComponent: Component
    {
        public PortBuilder CreatePort(EPortDirection dir, string name, object initialValue)
        {
            TypeDescriptor type = TypeDescriptor.GetTypeOf(initialValue);
            PortBuilder pd = new PortBuilder(dir, EPortUsage.Default, null, type);
            Descriptor.AddChild(pd, name);
            return pd;
        }

        public void BindPort(string name, SignalBase signal)
        {
            PortBuilder pd = Descriptor.FindPort(name) as PortBuilder;
            pd.Bind(signal.Descriptor);
        }

        public Signal<T> CreateSignal<T>(string name, T initialValue)
        {
            return (Signal<T>)Descriptor.CreateSignalInstance(name, initialValue).Instance;
        }

        public void AddComponent(Component child, string name)
        {
            Descriptor.AddChild(child.Descriptor, name);
        }

        public static DynamicComponent CloneComponentInterface(Component component)
        {
            DynamicComponent clone = new DynamicComponent();
            foreach (IPortDescriptor pd in component.Descriptor.GetPorts())
            {
                PortBuilder pb = clone.Descriptor.CreatePort(pd.Name, pd.Direction, pd.Usage, pd.ElementType);
                pb.Bind(pd.BoundSignal);
                clone.Descriptor.AddChild(pb, pb.Name);
            }
            return clone;
        }
    }
}

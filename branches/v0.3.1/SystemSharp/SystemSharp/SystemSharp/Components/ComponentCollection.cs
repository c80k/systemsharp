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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SystemSharp.DataTypes;
using SystemSharp.Meta;

namespace SystemSharp.Components
{
    /// <summary>
    /// A component which is an aggregate of multiple child components
    /// </summary>
    public class ComponentCollection: Component
    {
        private List<Component> _components;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="components">child components</param>
        public ComponentCollection(IEnumerable<Component> components)
        {
            _components = components.ToList();
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public ComponentCollection()
        {
        }

        /// <summary>
        /// Adds <paramref name="component"/> to this aggregate component.
        /// </summary>
        public void AddComponent(Component component)
        {
            _components.Add(component);
        }

        public override void SetOwner(DescriptorBase owner, MemberInfo declSite, IndexSpec indexSpec)
        {
            for (int i = 0; i < _components.Count; i++)
                _components[i].SetOwner(owner, declSite, new IndexSpec(i));
        }
    }
}

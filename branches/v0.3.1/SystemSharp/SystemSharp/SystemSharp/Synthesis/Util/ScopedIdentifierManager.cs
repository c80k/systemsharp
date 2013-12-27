/**
 * Copyright 2011-2013 Christian Köllner
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
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SystemSharp.Synthesis.Util
{
    /// <summary>
    /// Manages identifiers during code generation for hierarchically scoped languages, thus avoiding name collisions for generated
    /// identifier names.
    /// </summary>
    public class ScopedIdentifierManager
    {
        private Stack<Dictionary<string, object>> _scopes = new Stack<Dictionary<string, object>>();
        private Dictionary<string, object> _rootScope;

        public bool CaseSensitive { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="caseSensitive">whether target language is case sensitive</param>
        public ScopedIdentifierManager(bool caseSensitive = true)
        {
            CaseSensitive = caseSensitive;
            PushScope();
            _rootScope = _scopes.Peek();
        }

        private ScopedIdentifierManager(ScopedIdentifierManager other)
        {
            CaseSensitive = other.CaseSensitive;
            _rootScope = other._rootScope;
            _scopes.Push(_rootScope);
        }

        /// <summary>
        /// Begins a new scope.
        /// </summary>
        public void PushScope()
        {
            _scopes.Push(new Dictionary<string, object>());
        }

        /// <summary>
        /// Ends the current scope.
        /// </summary>
        public void PopScope()
        {
            _scopes.Pop();
        }

        /// <summary>
        /// Returns <c>true</c> if the given identifier name already exists in the current scope.
        /// </summary>
        /// <param name="name">identifier name</param>
        /// <param name="item">out parameter to receive the user-defined item which is associated with the name</param>
        public bool NameExists(string name, out object item)
        {
            if (!CaseSensitive)
                name = name.ToLower();

            foreach (Dictionary<string, object> map in _scopes)
            {
                if (map.TryGetValue(name, out item))
                    return true;
            }
            item = null;
            return false;
        }

        /// <summary>
        /// Creates an identifier name which is unique in the current scope.
        /// </summary>
        /// <param name="name">suggested name, taken as prefix</param>
        /// <param name="item">some object which uniquely identifies the object to which the name refers</param>
        /// <param name="rootScope"><c>true</c> if the identifier needs to be globally unique, 
        /// <c>false</c> if uniqueness inside the currently active scope is sufficient.</param>
        /// <returns></returns>
        public string GetUniqueName(string name, object item, bool rootScope = false)
        {
            string keyname = CaseSensitive ? name : name.ToLower();
            Dictionary<string, object> scope = rootScope ? _rootScope : _scopes.Peek();

            object item2;
            if (NameExists(name, out item2))
            {
                if (object.Equals(item, item2))
                    return name;

                int postfix = 1;
                string name2;
                do
                {
                    name2 = name + postfix;
                    ++postfix;
                } while (NameExists(name2, out item2) &&
                    !item.Equals(item2));

                if (item2 == null)
                {
                    keyname = CaseSensitive ? name2 : name2.ToLower();
                    scope[keyname] = item;
                }
                return name2;
            }
            else
            {
                scope[keyname] = item;
                return name;
            }
        }

        /// <summary>
        /// Creates a new instance which inherits its root scope from this instance.
        /// </summary>
        public ScopedIdentifierManager Fork()
        {
            return new ScopedIdentifierManager(this);
        }
    }
}

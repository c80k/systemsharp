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
using System.Linq;
using System.Text;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reflection;
using SDILReader;
using System.Reactive.Linq;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Analysis.Msil;
using System.Diagnostics.Contracts;

namespace SystemSharp.Analysis
{
    /// <summary>
    /// Describes context information on types used during runtime
    /// </summary>
    public class TypeFacts
    {
        /// <summary>
        /// Associated universe
        /// </summary>
        public FactUniverse Universe { get; private set; }

        /// <summary>
        /// Described type
        /// </summary>
        public Type TheType { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="universe">associated universe</param>
        /// <param name="type">described type</param>
        internal TypeFacts(FactUniverse universe, Type type)
        {
            Universe = universe;
            TheType = type;
        }

        private bool _isMutable;

        /// <summary>
        /// Whether the type has fields which are modified during runtime
        /// </summary>
        public bool IsMutable 
        {
            get { return _isMutable; }
            internal set { _isMutable = value; }
        }
    }
}

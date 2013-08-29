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
    public class TypeFacts
    {
        public FactUniverse Universe { get; private set; }
        public Type TheType { get; private set; }

        internal TypeFacts(FactUniverse universe, Type type)
        {
            Universe = universe;
            TheType = type;
        }

        private bool _isMutable;
        public bool IsMutable 
        {
            get { return _isMutable; }
            internal set { _isMutable = value; }
        }
    }
}

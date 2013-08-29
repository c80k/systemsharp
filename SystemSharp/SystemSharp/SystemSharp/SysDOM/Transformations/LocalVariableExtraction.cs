/**
 * Copyright 2013 Christian Köllner
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

namespace SystemSharp.SysDOM.Transformations
{
    class LocalVariableExtractor: DefaultTransformer
    {
        private Statement _root;
        private Dictionary<Variable, int> _knownVariables = new Dictionary<Variable,int>();

        public LocalVariableExtractor(Statement root)
        {
            _root = root;
        }

        protected override Statement Root
        {
            get { return _root; }
        }

        public override void VisitVariable(Variable variable)
        {
            if (!_knownVariables.ContainsKey(variable))
            {
                variable.LocalIndex = _knownVariables.Count;
                _knownVariables[variable] = variable.LocalIndex;
            }
            variable.LocalIndex = _knownVariables[variable];
            base.VisitVariable(variable);
        }

        public IEnumerable<Variable> SeenVariables
        {
            get { return _knownVariables.Keys; }
        }
    }

    public static class LocalVariableExtraction
    {
        public static IEnumerable<Variable> RenumerateLocalVariables(this Statement body)
        {
            var ext = new LocalVariableExtractor(body);
            ext.GetAlgorithm();
            return ext.SeenVariables;
        }
    }
}

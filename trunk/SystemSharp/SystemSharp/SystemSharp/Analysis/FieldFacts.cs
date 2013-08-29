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
using System.Reflection;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.Meta;

namespace SystemSharp.Analysis
{
    public class FieldFacts
    {
        private FieldDescriptor _nullInstDesc;
        private Dictionary<object, FieldDescriptor> _descMap = new Dictionary<object, FieldDescriptor>();

        public FactUniverse Universe { get; private set; }
        public FieldInfo Field { get; private set; }

        private void IndicateMutableType()
        {
            Type fdtype = Field.DeclaringType;
            Type btype = fdtype.BaseType;
            while (btype != null)
            {
                Universe.GetFacts(btype).IsMutable = true;
                btype = btype.BaseType;
            }
            foreach (Type iface in fdtype.GetInterfaces())
            {
                Universe.GetFacts(iface).IsMutable = true;
            }
            Universe.RealizationsOf(Field.DeclaringType).AutoDo(t => Universe.GetFacts(t).IsMutable = true);
        }

        private bool _isWritten;
        public bool IsWritten 
        {
            get { return _isWritten; }
            internal set 
            {
                if (value && Attribute.IsDefined(Field, typeof(AssumeConst)))
                    throw new InvalidOperationException();

                _isWritten = value;
                IndicateMutableType();
            }
        }

        private bool _isSubMutated;
        public bool IsSubMutated 
        {
            get { return _isSubMutated; }
            internal set 
            { 
                _isSubMutated = value;
                IndicateMutableType();
            }
        }

        public FieldDescriptor GetDescriptor(object inst)
        {
            if (inst == null)
            {
                if (_nullInstDesc == null)
                {
                    _nullInstDesc = new CILFieldDescriptor(Field, null)
                    {
                        IsConstant = !IsWritten && !IsSubMutated
                    };
                }
                return _nullInstDesc;
            }
            else
            {
                FieldDescriptor result;
                if (!_descMap.TryGetValue(inst, out result))
                {
                    result = new CILFieldDescriptor(Field, inst)
                    {
                        IsConstant = !IsWritten && !IsSubMutated
                    };
                    _descMap[inst] = result;
                }
                return result;
            }
        }

        public FieldFacts(FactUniverse universe, FieldInfo field)
        {
            Universe = universe;
            Field = field;
        }
    }
}

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
    /// <summary>
    /// This class captures the information available on a particular field
    /// </summary>
    public class FieldFacts
    {
        private FieldDescriptor _nullInstDesc;
        private Dictionary<object, FieldDescriptor> _descMap = new Dictionary<object, FieldDescriptor>();

        /// <summary>
        /// The associated universe
        /// </summary>
        public FactUniverse Universe { get; private set; }

        /// <summary>
        /// The field
        /// </summary>
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

        /// <summary>
        /// Whether the field is overwritten during runtime
        /// </summary>
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
        
        /// <summary>
        /// Whether any subordinate field of this field is overwritten during runtime.
        /// </summary>
        /// <remarks>
        /// Consider a struct:
        /// <c>
        /// struct InnerStruct
        /// {
        ///     public int X;
        /// }
        /// 
        /// class A
        /// {
        ///     public InnerStruct S;
        ///     
        ///     public static void Main()
        ///     {
        ///         var a = new A();
        ///         a.S.X = 5;
        ///     }
        /// }
        /// </c>
        /// For field A.S, we would get: IsWritten = false, IsSubMutated = true. 
        /// For field InnerStruct.X, we would get: IsWritten = true, IsSubMutated = false.
        /// </remarks>
        public bool IsSubMutated 
        {
            get { return _isSubMutated; }
            internal set 
            { 
                _isSubMutated = value;
                IndicateMutableType();
            }
        }

        /// <summary>
        /// Returns a SysDOM field descriptor for a particular instance
        /// </summary>
        /// <param name="inst">instance of class containing the field</param>
        /// <returns>SysDOM field descriptor</returns>
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

        /// <summary>
        /// Constructs an instance for a particular field
        /// </summary>
        /// <param name="universe">the associated universe</param>
        /// <param name="field">the field</param>
        public FieldFacts(FactUniverse universe, FieldInfo field)
        {
            Universe = universe;
            Field = field;
        }
    }
}

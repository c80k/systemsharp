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
using System.Reflection;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Meta;

namespace SystemSharp.SysDOM.Transformations
{
    /// <summary>
    /// Indicates that the tagged field should be converted to a local variable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class LocalizeField : Attribute
    {
    }

    class FieldLocalizer: DefaultTransformer
    {
        private CacheDictionary<FieldRef, Variable> _f2loc;
        private Function _root;
        private int _nextLocIndex;

        private Variable CreateVariableForField(FieldRef fref)
        {
            Variable var = new Variable(fref.Type)
            {
                Name = fref.Name,
                LocalIndex = _nextLocIndex++,
                InitialValue = fref.FieldDesc.ConstantValue
            };
            var.AddAttribute(fref);
            foreach (var attr in fref.Attributes)
                var.AddAttribute(attr);
            DeclareLocal(var);
            return var;
        }

        public FieldLocalizer(Function root)
        {
            _root = root;
            _f2loc = new CacheDictionary<FieldRef, Variable>(CreateVariableForField);
        }

        protected override Statement Root
        {
            get { return _root.Body; }
        }

        protected override void DeclareAlgorithm()
        {
            foreach (Variable var in _root.LocalVariables)
                DeclareLocal(var);
            foreach (Variable var in _root.InputVariables)
                DeclareInput(var);
            foreach (Variable var in _root.OutputVariables)
                DeclareOutput(var);
            _nextLocIndex = _root.LocalVariables.Any() ?
                _root.LocalVariables
                    .Where(v => v is Variable)
                    .Max(v => ((Variable)v).LocalIndex) + 1 :
                    0;
            base.DeclareAlgorithm();
        }

        public override Expression TransformLiteralReference(LiteralReference expr)
        {
            return base.TransformLiteralReference(expr);
            //FIXME: Code below is problematic when ReferencedObject is an array.
            //Array will be replaced by its content which is not the intended behavior.
#if false
            object constValue;
            if (expr.ReferencedObject.IsConst(out constValue))
            {
                return LiteralReference.CreateConstant(constValue);
            }
            else
            {
                return base.TransformLiteralReference(expr);
            }
#endif
        }

        public override void VisitFieldRef(FieldRef fieldRef)
        {
            if (fieldRef.HasAttribute<LocalizeField>())
                SetCurrentLiteral(_f2loc[fieldRef]);
            else
                SetCurrentLiteral(fieldRef);
        }

        public Variable[] Locals
        {
            get { return _f2loc.Values.ToArray(); }
        }
    }

    /// <summary>
    /// Provides a service for converting field accesses inside a function to local variables.
    /// </summary>
    public static class FieldLocalization
    {
        /// <summary>
        /// Converts all field accesses of the function to local variable accesses, creating a new variable
        /// for each accesses field.
        /// </summary>
        /// <param name="fun">function</param>
        /// <param name="locals">out parameter to receive the newly created local variables</param>
        /// <returns>the modified function</returns>
        public static Function ConvertFieldsToLocals(this Function fun, out Variable[] locals)
        {
            FieldLocalizer fl = new FieldLocalizer(fun);
            Function result = fl.GetAlgorithm();
            result.Name = fun.Name;
            locals = fl.Locals;
            return result;
        }
    }
}

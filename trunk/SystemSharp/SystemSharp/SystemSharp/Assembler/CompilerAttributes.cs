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
 * 
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    /// <summary>
    /// This attribute instructs the XIL compiler to translate calls to the attributed method in a user-defined manner.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited=true, AllowMultiple=false)]
    public abstract class CompileMethodCall: Attribute
    {
        /// <summary>
        /// Compiles the method call
        /// </summary>
        /// <param name="call">method call statement</param>
        /// <param name="backend">compiler interface to emit XIL instructions</param>
        public abstract void Compile(CallStatement call, ICompilerBackend backend);
    }

    /// <summary>
    /// This attribute instructs the XIL compiler to ignore calls to the attributed method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = true, AllowMultiple = true)]
    public class XILIgnore : CompileMethodCall
    {
        public override void Compile(CallStatement call, ICompilerBackend backend)
        {
        }
    }
}

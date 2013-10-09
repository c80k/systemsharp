/**
 * Copyright 2012 Christian Köllner
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

namespace SystemSharp.Analysis
{
    /// <summary>
    /// This marker interface instructs the decompiler to possibly modify a method argument sample
    /// </summary>
    /// <remarks>
    /// For example, take a division operation, such as division on SFix datatypes. If accidentally a value of 0 gets constructed as the divisor 
    /// sample, we'd get a DivisionByZeroException during decompilation. To avoid that, we can provide an implementation of this interface which will
    /// fix the argument sample if necessary.
    /// </remarks>
    public interface IGuardedArgument
    {
        object CorrectArgument(object arg);
    }

    /// <summary>
    /// This attribute provides an abstract implementation of the IGuardedArgument interface
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited=true)]
    public abstract class GuardedArgumentAttribute :
        Attribute,
        IGuardedArgument
    {
        /// <summary>
        /// You must override this method to perform all your required fix-ups of the argument sample
        /// </summary>
        /// <param name="arg">possibly invalid argument sample</param>
        /// <returns>valid argument sample</returns>
        public abstract object CorrectArgument(object arg);
    }
}

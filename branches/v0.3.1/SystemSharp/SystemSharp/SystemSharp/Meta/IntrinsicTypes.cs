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

namespace SystemSharp.Meta
{
    /// <summary>
    /// Provides symbols for all types which are considered "System#-intrinsic".
    /// </summary>
    public enum EIntrinsicTypes
    {
        /// <summary>
        /// System# DesignContext class
        /// </summary>
        DesignContext,

        /// <summary>
        /// CLI string/String class
        /// </summary>
        String,

        /// <summary>
        /// System# Time
        /// </summary>
        Time,

        /// <summary>
        /// System# StdLogic
        /// </summary>
        StdLogic,

        /// <summary>
        /// System# StdLogicVector
        /// </summary>
        StdLogicVector,

        /// <summary>
        /// System# SignalBase and all classes inheriting from it
        /// </summary>
        Signal,

        /// <summary>
        /// System# Signed
        /// </summary>
        Signed,

        /// <summary>
        /// System# Unsigned
        /// </summary>
        Unsigned,

        /// <summary>
        /// System# SFix
        /// </summary>
        SFix,

        /// <summary>
        /// System# UFix
        /// </summary>
        UFix,

        /// <summary>
        /// CLI StreamReader, StreamWriter
        /// </summary>
        File,

        /// <summary>
        /// All kinds of CLI Tuple
        /// </summary>
        Tuple,

        /// <summary>
        /// Any type which is kind of intrinsic but must not be used during runtime (at least for decompilation)
        /// </summary>
        IllegalRuntimeType
    }    
}

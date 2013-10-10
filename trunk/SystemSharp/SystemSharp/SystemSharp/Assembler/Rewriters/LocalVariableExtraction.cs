/**
 * Copyright 2012-2013 Christian Köllner
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
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    /// <summary>
    /// This static class provides helper methods for re-indexing all local variables which are referenced in XIL code
    /// </summary>
    public static class LocalVariableExtraction
    {
        /// <summary>
        /// Re-indexes all local variables which are referenced by a sequence of XIL instructions
        /// </summary>
        /// <remarks>
        /// Re-indexing means that the method assigns a new index to each local variable, starting from 0.
        /// This ensures that all indices form a contiguous sequence.
        /// </remarks>
        /// <param name="instrs">XIL instruction sequence</param>
        /// <returns>all referenced local variables</returns>
        public static IEnumerable<Variable> RenumerateLocalVariables(this IEnumerable<XILInstr> instrs)
        {
            var dic = new Dictionary<Variable, int>();
            foreach (var xi in instrs)
            {
                var variable = xi.Operand as Variable;
                if (variable == null)
                    continue;

                if (!dic.ContainsKey(variable))
                    dic[variable] = dic.Count;

                variable.LocalIndex = dic[variable];
            }

            return dic.Keys;
        }

        /// <summary>
        /// Re-indexes all local variables which are referenced by a sequence of XIL-S instructions
        /// </summary>
        /// <remarks>
        /// Re-indexing means that the method assigns a new index to each local variable, starting from 0.
        /// This ensures that all indices form a contiguous sequence.
        /// </remarks>
        /// <param name="instrs">XIL-S instruction sequence</param>
        /// <returns>all referenced local variables</returns>
        public static IEnumerable<Variable> RenumerateLocalVariables(this IEnumerable<XILSInstr> instrs)
        {
            return instrs.Select(_ => _.Command).RenumerateLocalVariables();
        }

        /// <summary>
        /// Re-indexes all local variables which are referenced by a sequence of XIL-3 instructions
        /// </summary>
        /// <remarks>
        /// Re-indexing means that the method assigns a new index to each local variable, starting from 0.
        /// This ensures that all indices form a contiguous sequence.
        /// </remarks>
        /// <param name="instrs">XIL-3 instruction sequence</param>
        /// <returns>all referenced local variables</returns>
        public static IEnumerable<Variable> RenumerateLocalVariables(this IEnumerable<XIL3Instr> instrs)
        {
            return instrs.Select(_ => _.Command).RenumerateLocalVariables();
        }
    }
}

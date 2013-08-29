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
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    public static class LocalVariableExtraction
    {
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

        public static IEnumerable<Variable> RenumerateLocalVariables(this IEnumerable<XILSInstr> instrs)
        {
            return instrs.Select(_ => _.Command).RenumerateLocalVariables();
        }

        public static IEnumerable<Variable> RenumerateLocalVariables(this IEnumerable<XIL3Instr> instrs)
        {
            return instrs.Select(_ => _.Command).RenumerateLocalVariables();
        }
    }
}

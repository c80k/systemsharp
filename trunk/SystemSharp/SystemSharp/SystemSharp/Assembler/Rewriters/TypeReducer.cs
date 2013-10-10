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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    class TypeReducerS : XILSRewriter
    {
        private static TypeDescriptor[] ReduceTypes(TypeDescriptor[] types)
        {
            Contract.Requires<ArgumentException>(types != null);
            return types
                .Select(t => TypeLowering.Instance.GetHardwareType(t))
                .ToArray();
        }

        public TypeReducerS(IList<XILSInstr> instrs) :
            base(instrs)
        {
        }

        protected override void ProcessDefault(XILSInstr i)
        {
            XILInstr cmd = i.Command;
            if (cmd.Name.Equals(InstructionCodes.LdConst))
            {
                object value = cmd.Operand;
                object lvalue = TypeLowering.Instance.ConvertToHardwareType(value);
                cmd = DefaultInstructionSet.Instance.LdConst(lvalue);
            }
            base.ProcessDefault(
                cmd.CreateStk(
                    i.Preds,
                    ReduceTypes(i.OperandTypes),
                    ReduceTypes(i.ResultTypes)));
        }

        protected override void ProcessBranch(XILSInstr i)
        {
            base.ProcessBranch(
                i.Command.CreateStk(
                    i.Preds,
                    ReduceTypes(i.OperandTypes),
                    ReduceTypes(i.ResultTypes)));
        }
    }

    /// <summary>
    /// This XIL-S code transformation converts each datatype to a hardware-compliant datatype.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class ReduceTypes :
        Attribute,
        IXILSRewriter
    {
        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            TypeReducerS trs = new TypeReducerS(instrs);
            trs.Rewrite();
            return trs.OutInstructions;
        }

        public override string ToString()
        {
            return "TypeReduction";
        }
    }
}

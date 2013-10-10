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
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler;
using SystemSharp.Assembler.Rewriters;

namespace SystemSharp.Assembler.Rewriters
{
    class TransitiveGotoEliminatorImpl: XILSRewriter
    {
        public TransitiveGotoEliminatorImpl(IList<XILSInstr> instrs) :
            base(instrs)
        {
            SetHandler(InstructionCodes.Goto, HandleBranch);
            SetHandler(InstructionCodes.BranchIfFalse, HandleBranch);
            SetHandler(InstructionCodes.BranchIfTrue, HandleBranch);
        }

        private void HandleBranch(XILSInstr instr)
        {
            var target = (BranchLabel)instr.StaticOperand;
            while (InInstructions[target.InstructionIndex].Name == InstructionCodes.Goto)
                target = (BranchLabel)InInstructions[target.InstructionIndex].StaticOperand;
            if (target.InstructionIndex == instr.Index + 1)
            {
                if (instr.Name == InstructionCodes.Goto)
                    Emit(DefaultInstructionSet.Instance.Nop().CreateStk(instr.Preds, instr.OperandTypes, instr.ResultTypes));
                else
                    Emit(DefaultInstructionSet.Instance.Pop().CreateStk(instr.Preds, instr.OperandTypes, instr.ResultTypes));
            }
            else
            {
                Emit(new XILInstr(instr.Name, target).CreateStk(instr.Preds, instr.OperandTypes, instr.ResultTypes));
            }
        }
    }

    /// <summary>
    /// This XIL-S code transformation tries to reduce the number of branch instructions inside the code by replacing 
    /// chains of branches with single branch instructions.
    /// </summary>
    /// <remarks>
    /// Consider the following example:
    /// <code>
    /// goto L1
    /// ...
    /// L1: brtrue L2
    /// ...
    /// L2:
    /// ...
    /// </code>
    /// Obviously, we can implement the same behavior with a more efficient code:
    /// <code>
    /// brtrue L2
    /// ...
    /// L2:
    /// ...
    /// </code>
    /// </remarks>
    public class TransitiveGotoEliminator : IXILSRewriter
    {
        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            var impl = new TransitiveGotoEliminatorImpl(instrs);
            impl.Rewrite();
            return impl.OutInstructions;
        }

        public override string ToString()
        {
            return "eliminate transitive jumps";
        }
    }
}

/**
 * Copyright 2012-2103 Christian Köllner
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
    class ConditionalBranchOptimizerImpl: XILSRewriter
    {
        public ConditionalBranchOptimizerImpl(IList<XILSInstr> instrs) :
            base(instrs)
        {
            SetHandler(InstructionCodes.BranchIfFalse, HandleCBranch);
            SetHandler(InstructionCodes.BranchIfTrue, HandleCBranch);
        }

        private bool _ignoreNext;

        private void HandleCBranch(XILSInstr xilsi)
        {
            var xilsi1 = InInstructions[xilsi.Index + 1];
            if (xilsi1.Name == InstructionCodes.Goto)
            {
                var target0 = (BranchLabel)xilsi.StaticOperand;
                if (target0.InstructionIndex == xilsi.Index + 2)
                {
                    var target1 = (BranchLabel)xilsi1.StaticOperand;
                    switch (xilsi.Name)
                    {
                        case InstructionCodes.BranchIfFalse:
                            Emit(DefaultInstructionSet.Instance.BranchIfTrue(target1).CreateStk(
                                RemapPreds(xilsi.Preds), xilsi.OperandTypes, xilsi.ResultTypes));
                            break;

                        case InstructionCodes.BranchIfTrue:
                            Emit(DefaultInstructionSet.Instance.BranchIfFalse(target1).CreateStk(
                                RemapPreds(xilsi.Preds), xilsi.OperandTypes, xilsi.ResultTypes));
                            break;
                    }
                    _ignoreNext = true;
                    return;
                }
            }
            ProcessBranch(xilsi);
        }

        protected override void ProcessInstruction(XILSInstr i)
        {
            if (_ignoreNext)
            {
                Emit(DefaultInstructionSet.Instance.Nop().CreateStk(RemapPreds(i.Preds), 0));
                _ignoreNext = false;
            }
            else
            {
                base.ProcessInstruction(i);
            }
        }
    }

    /// <summary>
    /// This XIL-S code transformation tries to reduce the amount of branch instructions.
    /// </summary>
    /// <remarks>
    ///  Precisely, it looks for the following pattern:
    /// <code>
    /// brtrue L1
    /// goto L2
    /// L1: ...
    /// L2: ...
    /// </code>
    /// Obviously, this pattern can be replaced by the following more efficient notation:
    /// <code>
    /// brfalse L2
    /// L1: ...
    /// L2: ...
    /// </code>
    /// Of course, the same applies to the dual pattern when we replace brtrue by brfalse.
    /// </remarks>
    public class ConditionalBranchOptimizer : IXILSRewriter
    {
        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            var impl = new ConditionalBranchOptimizerImpl(instrs);
            impl.Rewrite();
            return impl.OutInstructions;
        }

        public override string ToString()
        {
            return "optimize conditional branches";
        }
    }
}

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
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler;
using SystemSharp.Assembler.Rewriters;

namespace SystemSharp.Assembler.Rewriters
{
    class UnreachableInstructionEliminatorImpl : XILSRewriter
    {
        public UnreachableInstructionEliminatorImpl(IList<XILSInstr> instrs) :
            base(instrs)
        {
        }

        private bool[] _reach;

        protected override void PreProcess()
        {
            base.PreProcess();
            var q = new Queue<XILSInstr>();
            _reach = new bool[InInstructions.Count];
            q.Enqueue(InInstructions.First());
            while (q.Count > 0)
            {
                var xilsi = q.Dequeue();
                if (!_reach[xilsi.Index])
                {
                    _reach[xilsi.Index] = true;
                    switch (xilsi.Name)
                    {
                        case InstructionCodes.Goto:
                        case InstructionCodes.BranchIfTrue:
                        case InstructionCodes.BranchIfFalse:
                            {
                                var target = (BranchLabel)xilsi.StaticOperand;
                                q.Enqueue(InInstructions[target.InstructionIndex]);
                                if (xilsi.Name != InstructionCodes.Goto)
                                    goto default;
                            }
                            break;

                        default:
                            if (xilsi.Index + 1 < InInstructions.Count)
                                q.Enqueue(InInstructions[xilsi.Index + 1]);
                            break;
                    }
                }
            }
            foreach (var xilsi in InInstructions)
            {
                if (xilsi.Name == InstructionCodes.Nop)
                    _reach[xilsi.Index] = false;
            }
        }

        protected override void ProcessInstruction(XILSInstr i)
        {
            if (_reach[i.Index])
                base.ProcessInstruction(i);
        }
    }

    /// <summary>
    /// This XIL-S code transformation eliminates unreachable instructions
    /// </summary>
    public class UnreachableInstructionEliminator: IXILSRewriter
    {
        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            var impl = new UnreachableInstructionEliminatorImpl(instrs);
            impl.Rewrite();
            return impl.OutInstructions;
        }

        public override string ToString()
        {
            return "eliminate unreachable instructions";
        }
    }
}

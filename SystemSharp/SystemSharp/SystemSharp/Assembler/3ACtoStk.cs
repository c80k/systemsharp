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
using SystemSharp.Assembler;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    class _3ACtoStkImpl
    {
        private XIL3Function _inFunc;
        private List<int> _slotRemap = new List<int>();
        private List<int> _instRemap = new List<int>();
        private List<Variable> _interLocals = new List<Variable>();
        private List<XILSInstr> _outInstrs = new List<XILSInstr>();
        private List<BranchLabel> _labels = new List<BranchLabel>();

        public _3ACtoStkImpl(XIL3Function func)
        {
            _inFunc = func;
        }

        private void Emit(XILSInstr xilsi)
        {
            _outInstrs.Add(xilsi);
        }

        private void Process(XIL3Instr xil3i)
        {
            var preds = xil3i.Preds.Select(p => p.Remap(_slotRemap[p.PredIndex])).ToArray();
            _instRemap.Add(_outInstrs.Count);

            foreach (int oslot in xil3i.OperandSlots)
            {
                Emit(DefaultInstructionSet.Instance
                    .LoadVar(_interLocals[oslot]).CreateStk(preds, 0, _interLocals[oslot].Type));
            }
            _slotRemap.Add(_outInstrs.Count);
            var cmd = xil3i.Command;
            if (cmd.Name == InstructionCodes.BranchIfFalse ||
                cmd.Name == InstructionCodes.BranchIfTrue ||
                cmd.Name == InstructionCodes.Goto)
            {
                var target = (BranchLabel)cmd.Operand;
                var newTarget = new BranchLabel() { InstructionIndex = target.InstructionIndex };
                cmd = new XILInstr(cmd.Name, newTarget) { BackRef = cmd.BackRef };
                _labels.Add(newTarget);
            }
            Emit(cmd.CreateStk(preds, xil3i.OperandSlots.Length,
                xil3i.OperandSlots.Select(os => _interLocals[os].Type)
                .Concat(xil3i.ResultSlots.Select(rs => _interLocals[rs].Type))
                .ToArray()));
            foreach (int rslot in xil3i.ResultSlots.Reverse())
            {
                Emit(DefaultInstructionSet.Instance
                    .StoreVar(_interLocals[rslot]).CreateStk(1, _interLocals[rslot].Type));
            }
        }

        public XILSFunction Run()
        {
            int count = 0;
            foreach (var stype in _inFunc.SlotTypes)
                _interLocals.Add(new Variable(stype) { Name = _inFunc.Name + (count++) });

            foreach (var xil3i in _inFunc.Instructions)
                Process(xil3i);

            foreach (var label in _labels)
                label.InstructionIndex = _instRemap[label.InstructionIndex];

            var locals = _inFunc.Locals.Concat(_interLocals).ToArray();
            var result = new XILSFunction(
                _inFunc.Name,
                _inFunc.Arguments,
                locals,
                _outInstrs.ToArray());

            return result;
        }
    }

    public static class _3ACToStk
    {
        public static XILSFunction ToXILS(this XIL3Function func)
        {
            var xform = new _3ACtoStkImpl(func);
            return xform.Run();
        }
    }
}

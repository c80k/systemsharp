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
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    class ReadWriteDependencyInjectorImpl: XILSRewriter
    {
        private ControlFlowGraph<XILSInstr> _cfg;

        public ReadWriteDependencyInjectorImpl(IList<XILSInstr> instrs):
            base(instrs)
        {
            _cfg = Compilation.CreateCFG(instrs);
            SetHandler(InstructionCodes.LoadVar, HandleLoadVar);
            SetHandler(InstructionCodes.StoreVar, HandleStoreVar);
            SetHandler(InstructionCodes.LdelemFixA, HandleLoadElement);
            SetHandler(InstructionCodes.LdelemFixAFixI, HandleLoadElement);
            SetHandler(InstructionCodes.StelemFixA, HandleStoreElement);
            SetHandler(InstructionCodes.StelemFixAFixI, HandleStoreElement);
        }

        private void HandleLoadVar(XILSInstr xilsi)
        {
            var bb = _cfg.GetBasicBlockContaining(xilsi.Index);
            var lastStore = bb.Range
                .Take(xilsi.Index - bb.StartIndex)
                .Where(
                    i => i.Name == InstructionCodes.StoreVar &&
                        i.StaticOperand.Equals(xilsi.StaticOperand))
                .LastOrDefault();
            if (lastStore == null)
            {
                Emit(xilsi);
            }
            else
            {
                var preds = xilsi.Preds.Union(
                    new OrderDependency[] { new OrderDependency(lastStore.Index, OrderDependency.EKind.BeginAfter) })
                    .ToArray();
                Emit(xilsi.Command.CreateStk(preds, xilsi.OperandTypes, xilsi.ResultTypes));
            }
        }

        private void HandleStoreVar(XILSInstr xilsi)
        {
            var bb = _cfg.GetBasicBlockContaining(xilsi.Index);
            var lastStoreOrLoad = bb.Range
                .Take(xilsi.Index - bb.StartIndex)
                .Where(
                    i => (i.Name == InstructionCodes.StoreVar ||
                        i.Name == InstructionCodes.LoadVar) &&
                        i.StaticOperand.Equals(xilsi.StaticOperand))
                .LastOrDefault();

            if (lastStoreOrLoad == null)
            {
                Emit(xilsi);
            }
            else
            {
                var preds = xilsi.Preds.Union(
                    new InstructionDependency[] { new OrderDependency(lastStoreOrLoad.Index, OrderDependency.EKind.BeginAfter) })
                    .ToArray();
                Emit(xilsi.Command.CreateStk(preds, xilsi.OperandTypes, xilsi.ResultTypes));
            }
        }

        private void HandleLoadElement(XILSInstr xilsi)
        {
            var bb = _cfg.GetBasicBlockContaining(xilsi.Index);
            var lastStore = bb.Range
                .Take(xilsi.Index - bb.StartIndex)
                .Where(
                    i => (i.Name == InstructionCodes.LdelemFixA ||
                        i.Name == InstructionCodes.LdelemFixAFixI ||
                        i.Name == InstructionCodes.StelemFixA ||
                        i.Name == InstructionCodes.StelemFixAFixI) &&
                        ((FixedArrayRef)i.StaticOperand).ArrayObj == ((FixedArrayRef)xilsi.StaticOperand).ArrayObj)
                .LastOrDefault();
            if (lastStore == null)
            {
                Emit(xilsi);
            }
            else
            {
                var preds = xilsi.Preds.Union(
                    new OrderDependency[] { new OrderDependency(lastStore.Index, OrderDependency.EKind.BeginAfter) }).ToArray();
                Emit(xilsi.Command.CreateStk(preds, xilsi.OperandTypes, xilsi.ResultTypes));
            }
        }

        private void HandleStoreElement(XILSInstr xilsi)
        {
            var bb = _cfg.GetBasicBlockContaining(xilsi.Index);
            var lastStoreOrLoad = bb.Range
                .Take(xilsi.Index - bb.StartIndex)
                .Where(
                    i => (i.Name == InstructionCodes.LdelemFixA ||
                        i.Name == InstructionCodes.LdelemFixAFixI ||
                        i.Name == InstructionCodes.StelemFixA ||
                        i.Name == InstructionCodes.StelemFixAFixI) &&
                        ((FixedArrayRef)i.StaticOperand).ArrayObj == ((FixedArrayRef)xilsi.StaticOperand).ArrayObj)
                .LastOrDefault();

            if (lastStoreOrLoad == null)
            {
                Emit(xilsi);
            }
            else
            {
                var preds = xilsi.Preds.Union(
                    new OrderDependency[] { new OrderDependency(lastStoreOrLoad.Index, OrderDependency.EKind.BeginAfter) }).ToArray();
                Emit(xilsi.Command.CreateStk(preds, xilsi.OperandTypes, xilsi.ResultTypes));
            }
        }

        protected override void ProcessBranch(XILSInstr i)
        {
            var bb = _cfg.GetBasicBlockContaining(i.Index);
            var preds = i.Preds
                .Union(bb.Range
                    .Take(i.Index - bb.StartIndex)
                    .Select(j => new OrderDependency(j.Index, OrderDependency.EKind.CompleteAfter)))
                .ToArray();
            base.ProcessBranch(i.Command.CreateStk(preds, i.OperandTypes, i.ResultTypes));
        }
    }

    public class ReadWriteDependencyInjector : IXILSRewriter
    {
        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            var impl = new ReadWriteDependencyInjectorImpl(instrs);
            impl.Rewrite();
            return impl.OutInstructions;
        }
    }
}

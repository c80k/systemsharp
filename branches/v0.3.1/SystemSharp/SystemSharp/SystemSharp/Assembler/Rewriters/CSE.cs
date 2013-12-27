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
using System.Linq;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Collections;
using SystemSharp.Meta;
using SystemSharp.Analysis.M2M;

namespace SystemSharp.Assembler.Rewriters
{
    /// <summary>
    /// This XIL-3 code transformation identifies and eliminates common subexpressions.
    /// </summary>
    /// <remarks>
    /// In fact, the XIL-3 representation makes it quite straightforward to implement common subexpression elimination.
    /// The algorithm resembles pretty much the procedure described in the following paper:
    /// John Cocke. "Global Common Subexpression Elimination." 
    /// Proceedings of a Symposium on Compiler Construction, ACM SIGPLAN Notices 5(7), July 1970, pages 850-856.
    /// </remarks>
    public class CommonSubExpressionEliminator: XIL3Rewriter
    {
        private class XIL3Comparer : IEqualityComparer<XIL3Instr>
        {
            public bool Equals(XIL3Instr x, XIL3Instr y)
            {
                if (x.Name != y.Name)
                    return false;
                if (!object.Equals(x.StaticOperand, y.StaticOperand))
                    return false;
                if (!x.Preds.SequenceEqual(y.Preds))
                    return false;
                if (!x.OperandSlots.SequenceEqual(y.OperandSlots))
                    return false;
                if (!x.ResultSlots.Select(s => _func.SlotTypes[s]).SequenceEqual(y.ResultSlots.Select(s => _func.SlotTypes[s])))
                    return false;
                return true;
            }

            public int GetHashCode(XIL3Instr obj)
            {
                int hash = obj.Name.GetHashCode();
                if (obj.StaticOperand != null)
                    hash ^= obj.StaticOperand.GetHashCode();
                hash ^= obj.Preds.GetSequenceHashCode();
                hash ^= obj.OperandSlots.GetSequenceHashCode();
                return hash;
            }

            private XIL3Function _func;

            public XIL3Comparer(XIL3Function func)
            {
                _func = func;
            }
        }

        private Dictionary<XIL3Instr, XIL3Instr> _map;
        private HashSet<int> _bbBoundaries;

        public CommonSubExpressionEliminator()
        {
        }

        public override XIL3Function Rewrite(XIL3Function func)
        {
            _map = new Dictionary<XIL3Instr, XIL3Instr>(new XIL3Comparer(func));
            _bbBoundaries = new HashSet<int>(func.GetBasicBlockBoundaries());
            return base.Rewrite(func);
        }

        protected override void ProcessDefault(XIL3Instr i)
        {
            if (_bbBoundaries.Contains(i.Index))
                _map.Clear();

            var preds = RemapPreds(i.Preds);
            int[] operands = RemapOperandSlots(i.OperandSlots);
            var ir = i.Command.Create3AC(preds, operands, i.ResultSlots);
            XIL3Instr eqv;
            if (_map.TryGetValue(ir, out eqv))
            {
                for (int j = 0; j < i.ResultSlots.Length; j++ )
                    RemapSlot(i.ResultSlots[j], eqv.ResultSlots[j]);
            }
            else
            {
                int[] results = RemapResultSlots(i.ResultSlots);
                var ir2 = i.Command.Create3AC(preds, operands, results);
                Emit(ir2);
                _map[ir] = ir2;
            }
        }
    }

    /// <summary>
    /// This attribute instructs the XIL compiler to perform common subexpression elimination on the transformed code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited=false, AllowMultiple=false)]
    public class EliminateCommonSubexpressions: 
        Attribute,
        IXIL3Rewriter
    {
        public override string ToString()
        {
            return "CSE";
        }

        public XIL3Function Rewrite(XIL3Function func)
        {
            return new CommonSubExpressionEliminator().Rewrite(func);
        }
    }
}

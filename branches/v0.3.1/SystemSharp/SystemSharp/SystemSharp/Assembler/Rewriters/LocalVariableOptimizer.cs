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
using System.Diagnostics;
using System.Linq;
using SystemSharp.Analysis;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    /// <summary>
    /// This XIL-S code transformation tries to eliminate unnecessary local variables. E.g. if a local variable is assigned only once
    /// and read only once, we can replace it by its right-hand side value.
    /// </summary>
    public class LocalVariableOptimizer : XILSRewriter
    {
        /// <summary>
        /// The dataflow analyzer which serves as analysis back-end.
        /// </summary>
        public DataflowAnalyzer<XILSInstr> DflowAnalyzer { get; private set; }

        private Dictionary<int, Tuple<int, int>> _read2write = new Dictionary<int, Tuple<int, int>>();
        private List<Tuple<int, int>> _resultStack = new List<Tuple<int, int>>();

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="instructions">instruction list</param>
        /// <param name="dflowAnalyzer">dataflow analyzer which serves as analysis back-end</param>
        public LocalVariableOptimizer(IList<XILSInstr> instructions,
            DataflowAnalyzer<XILSInstr> dflowAnalyzer) :
            base(instructions)
        {
            DflowAnalyzer = dflowAnalyzer;
            SetHandler(InstructionCodes.StoreVar, HandleStoreVar);
            SetHandler(InstructionCodes.LoadVar, HandleLoadVar);
        }

        public override string ToString()
        {
            return "LocVarOpt";
        }

        private void HandleStoreVar(XILSInstr i)
        {
            int readPoint;
            Variable local = i.StaticOperand as Variable;
            if (DflowAnalyzer.IsReadAfterWrite(i.Index, out readPoint))
            {
                var preds = RemapPreds(i.Preds);

                // keep expression on the stack, but bury it at the very bottom.
                for (int j = 1; j < _resultStack.Count; j++)
                {
                    var resultTypes = TypeStack.Reverse().Skip(1).Concat(TypeStack.Skip(_resultStack.Count - 1)).ToArray();
                    Emit(DefaultInstructionSet.Instance.Dig(_resultStack.Count - 1).CreateStk(
                        preds,
                        TypeStack.Reverse().ToArray(),
                        resultTypes));
                    preds = new InstructionDependency[0];
                }
                _read2write[readPoint] = _resultStack[0];
            }            
            else if (local == null)
            {
                ProcessDefault(i);
            }
            else if (DflowAnalyzer.EliminableLocals.Contains(local.LocalIndex))
            {
                Emit(DefaultInstructionSet.Instance.Pop().CreateStk(1, local.Type));
            }
            else
            {
                ProcessDefault(i);
            }
        }

        private void HandleLoadVar(XILSInstr i)
        {
            Tuple<int, int> stackRef;            
            if (_read2write.TryGetValue(i.Index, out stackRef))
            {
                int stackP = _resultStack.IndexOf(stackRef);
                Debug.Assert(stackP >= 0);
                Debug.Assert(_resultStack.Count == TypeStack.Count);
                int depth = _resultStack.Count;
                int relP = depth - stackP - 1;
                if (relP > 0)
                {
                    var preds = RemapPreds(i.Preds);
                    TypeDescriptor[] opTypes = TypeStack.Take(depth - stackP).Reverse().ToArray();
                    Debug.Assert(opTypes.First().Equals(i.ResultTypes[0]));
                    TypeDescriptor[] rTypes = (TypeDescriptor[])opTypes.Clone();
                    for (int j = 1; j <= relP; j++)
                    {
                        var tmp = rTypes[j];
                        rTypes[j] = rTypes[j - 1];
                        rTypes[j - 1] = tmp;

                        var tmpi = _resultStack[stackP + j];
                        _resultStack[stackP + j] = _resultStack[stackP + j - 1];
                        _resultStack[stackP + j - 1] = tmpi;
                    }
                    TypeDescriptor[] allTypes = opTypes.Concat(rTypes).ToArray();
                    base.Emit(DefaultInstructionSet.Instance.Dig(relP).CreateStk(preds, opTypes.Length, allTypes));
                }
            }
            else
            {
                ProcessDefault(i);
            }
        }

        private void EmitDig(XILSInstr i, int stackIndex)
        {
            base.Emit(i);
            var elem = _resultStack[_resultStack.Count - stackIndex - 1];
            _resultStack.RemoveAt(_resultStack.Count - stackIndex - 1);
            _resultStack.Add(elem);
        }

        protected override void Emit(XILSInstr i)
        {
            if (i.Name == InstructionCodes.Dig)
            {
                EmitDig(i, (int)i.StaticOperand);
            }
            else if (i.Name == InstructionCodes.Dig)
            {
                EmitDig(i, (int)1);
            }
            else
            {
                _resultStack.RemoveRange(
                    _resultStack.Count - i.OperandTypes.Length,
                    i.OperandTypes.Length);
                base.Emit(i);
                for (int j = 0; j < i.ResultTypes.Length; j++)
                    _resultStack.Add(Tuple.Create(i.Index, j));
            }
        }
    }
}

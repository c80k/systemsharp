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

namespace SystemSharp.Assembler.Rewriters
{
    class ParLimiterImpl: 
        XILSRewriter
    {
        private class InstrQueue
        {
            private XILSInstr[] _q;
            private int _cur;

            public InstrQueue(int length)
            {
                _q = new XILSInstr[length];
            }

            public void Enqueue(XILSInstr i)
            {
                _q[_cur] = i;
                _cur = (_cur + 1) % _q.Length;
            }

            public XILSInstr OldestInstr
            {
                get { return _q[_cur]; }
            }

            public void Flush()
            {
                Array.Clear(_q, 0, _q.Length);
            }
        }

        private ControlFlowGraph<XILSInstr> _cfg;
        private int _lastBB;
        private Dictionary<string, InstrQueue> _seqQs;
        private string _name;

        public ParLimiterImpl(IList<XILSInstr> instrs, string name) :
            base(instrs)
        {
            _lastBB = -1;
            _seqQs = new Dictionary<string, InstrQueue>();
            _name = name;
        }

        public void AddParLimit(string opCode, int limit)
        {
            _seqQs[opCode] = new InstrQueue(limit);
        }

        protected override void PreProcess()
        {
            _cfg = Compilation.CreateCFG(InInstructions);
        }

        private void Flush()
        {
            foreach (var q in _seqQs.Values)
                q.Flush();
        }

        protected override void ProcessInstruction(XILSInstr i)
        {
            var bb = _cfg.GetBasicBlockContaining(i.Index);
            if (bb.StartIndex != _lastBB)
            {
                _lastBB = bb.StartIndex;
                Flush();
            }
            InstrQueue iq;
            var inew = i;
            if (_seqQs.TryGetValue(i.Name, out iq))
            {
                if (iq.OldestInstr != null)
                {
                    var newPreds = RemapPreds(i.Preds).Union(new InstructionDependency[] { 
                        new OrderDependency(iq.OldestInstr.Index, OrderDependency.EKind.BeginAfter) }).ToArray();
                    inew = new XILSInstr(i.Command, newPreds, i.OperandTypes, i.ResultTypes);
                    Emit(inew);
                }
                else
                {
                    base.ProcessInstruction(inew);
                }
                var nop = DefaultInstructionSet.Instance.Nop(1).CreateStk(RemapPreds(new InstructionDependency[] { 
                    new OrderDependency(i.Index, OrderDependency.EKind.BeginAfter) }), 0);
                Emit(nop);
                iq.Enqueue(nop);
            }
            else
            {
                base.ProcessInstruction(inew);
            }
        }

        public override string ToString()
        {
            return _name;
        }
    }

    /// <summary>
    /// This static class provides a factory method for XIL-S transformations which limit the maximum parallelism of
    /// constant-loading XIL instructions.
    /// </summary>
    public static class ParLimiter
    {
        private class Impl :
            IXILSRewriter
        {
            private List<Tuple<string, int>> _limits = new List<Tuple<string, int>>();
            private string _name;

            public void AddParLimit(string opCode, int limit)
            {
                _limits.Add(Tuple.Create(opCode, limit));
            }

            public override string ToString()
            {
                return _name;
            }

            public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
            {
                var sb = new StringBuilder();
                sb.Append("LimitPar");
                foreach (var tup in _limits)
                {
                    sb.Append("_" + tup.Item1 + "_" + tup.Item2);
                }
                _name = sb.ToString();
                var pl = new ParLimiterImpl(instrs, _name);
                foreach (var item in _limits)
                    pl.AddParLimit(item.Item1, item.Item2);

                pl.Rewrite();
                return pl.OutInstructions;
            }
        }

        /// <summary>
        /// Creates a XIL-S code transformation which limits the number of simultaneous constant-loading instructions.
        /// </summary>
        /// <remarks>
        /// This kind of transformation is useful for horizontally microcoded architectures where each constant needs to be stored
        /// in program ROM. The more constant-loading instructions run in parallel, the wider a row inside program ROM. Therefore,
        /// it makes sense to limit that kind of parallelism.
        /// </remarks>
        /// <param name="max">maximum admissible number of simultaneous constant-loading instructions</param>
        /// <returns>resulting transformation</returns>
        public static IXILSRewriter LimitConstantLoads(int max)
        {
            var pl = new Impl();
            pl.AddParLimit(InstructionCodes.LdConst, max);
            return pl;
        }
    }
}

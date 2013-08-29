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
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Collections;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.SchedulingAlgorithms
{
    public interface IFDSAdapater<T>
    {
        IPropMap<T, int> Index { get; }
        IPropMap<T, long> Latency { get; }
        IPropMap<T, ScheduleDependency<T>[]> Preds { get; }
        IPropMap<T, ScheduleDependency<T>[]> Succs { get; }
        IPropMap<T, long> ASAPIndex { get; }
        IPropMap<T, long> ALAPIndex { get; }
        IPropMap<T, object> IClass { get; }
        IPropMap<T, long> FDSIndex { get; }
        IPropMap<T, int[]> Operands { get; }
        IPropMap<T, int[]> Results { get; }
    }

    class ForceDirectedSchedulerImpl<T>
    {
        private class DG
        {
            protected ForceDirectedSchedulerImpl<T> _fds;
            private Dictionary<long, double> _map = new Dictionary<long, double>();

            public DG(ForceDirectedSchedulerImpl<T> fds)
            {
                _fds = fds;
            }

            public double this[long cstep]
            {
                get
                {
                    double result;
                    if (!_map.TryGetValue(cstep, out result))
                        result = 0.0;
                    return result;
                }
                set
                {
                    _map[cstep] = value;
                }
            }

            public virtual void Consume(T instr)
            {
                long asap = _fds.ASAP(instr);
                long alap = _fds.ALAP(instr);
                double prob = 1.0 / (alap - asap + 1);
                for (long cstep = asap; cstep <= alap; cstep++)
                {
                    this[cstep] += prob;
                }
            }

            public virtual void Unconsume(T instr)
            {
                long asap = _fds.ASAP(instr);
                long alap = _fds.ALAP(instr);
                double prob = 1.0 / (alap - asap + 1);
                for (long cstep = asap; cstep <= alap; cstep++)
                {
                    this[cstep] -= prob;
                }
            }
        }

        private class TransDG : DG
        { 
            public TransDG(ForceDirectedSchedulerImpl<T> fds):
                base(fds)
            {
            }

            private int GetNOpInOut(T instr, long cstep)
            {
                int n = _fds.Adapter.Results[instr].Length;
                foreach (int operand in _fds.Adapter.Operands[instr])
                {
                    var consumers = _fds._operandConsumers.Get(operand);
                    T bestConsumer = consumers[0];
                    double maxProb = _fds.GetProb(bestConsumer, cstep);
                    for (int i = 1; i < consumers.Count; i++)
                    {
                        T consumer = consumers[i];
                        double prob = _fds.GetProb(consumer, cstep);
                        if (prob > maxProb)
                        {
                            bestConsumer = consumer;
                            maxProb = prob;
                        }
                    }
                    if (_fds.Adapter.Index[bestConsumer] == _fds.Adapter.Index[instr])
                    {
                        ++n;
                    }
                }
                return n;
            }

            public override void Consume(T instr)
            {
                long asap = _fds.ASAP(instr);
                long alap = _fds.ALAP(instr);
                double prob = 1.0 / (alap - asap + 1);
                for (long cstep = asap; cstep <= alap; cstep++)
                {
                    this[cstep] += prob * GetNOpInOut(instr, cstep);
                }
            }

            public override void Unconsume(T instr)
            {
                long asap = _fds.ASAP(instr);
                long alap = _fds.ALAP(instr);
                double prob = 1.0 / (alap - asap + 1);
                for (long cstep = asap; cstep <= alap; cstep++)
                {
                    this[cstep] -= prob * GetNOpInOut(instr, cstep);
                }
            }
        }

        public IFDSAdapater<T> Adapter { get; private set; }
        public IList<T> Instructions { get; private set; }
        public SchedulingConstraints Constraints { get; private set; }

        public bool MinimizeBuses { get; set; }
        public bool MinimizeRegisters { get; set; }

        private long _firstIdx;
        private Dictionary<object, DG> _dg = new Dictionary<object, DG>();
        private double[] _id;
        private Dictionary<int, List<T>> _operandConsumers;
        private long[] _asap;
        private long[] _alap;

        private static bool InRange(long i, long min, long max)
        {
            return i >= min && i < max;
        }

        public ForceDirectedSchedulerImpl(IFDSAdapater<T> adapter, IList<T> instructions, SchedulingConstraints constraints)
        {
            Adapter = adapter;
            Instructions = instructions;
            Constraints = constraints;
            CheckInput();
        }

        private void CheckInput()
        {
            if (Instructions.Any())
            {
                T first = Instructions.First();
                _firstIdx = Adapter.Index[first];
                long maxIdx = _firstIdx + Instructions.Count;
                for (int i = 0; i < Instructions.Count; i++)
                {
                    T instr = Instructions[i];
                    Debug.Assert(i == Adapter.Index[instr] - _firstIdx);
                    Debug.Assert(Adapter.Preds[instr].All(pred => InRange(Adapter.Index[pred.Task], _firstIdx, maxIdx)));
                    Debug.Assert(Adapter.Succs[instr].All(succ => InRange(Adapter.Index[succ.Task], _firstIdx, maxIdx)));
                }
            }
        }

        private long IndexOf(T instr)
        {
            return Adapter.Index[instr] - _firstIdx;
        }

        private void Init()
        {
            _asap = new long[Instructions.Count];
            _alap = new long[Instructions.Count];
            foreach (T instr in Instructions)
            {
                Adapter.FDSIndex[instr] = long.MinValue;
                _asap[IndexOf(instr)] = Adapter.ASAPIndex[instr];
                _alap[IndexOf(instr)] = Adapter.ALAPIndex[instr];
            }
        }

        private long ASAP(T instr)
        {
            return _asap[IndexOf(instr)];
        }

        private long ALAP(T instr)
        {
            return _alap[IndexOf(instr)];
        }

        private void SetASAP(T instr, long asap)
        {
            _asap[IndexOf(instr)] = asap;
        }

        private void SetALAP(T instr, long alap)
        {
            _alap[IndexOf(instr)] = alap;
        }

        private bool IsScheduled(T instr)
        {
            return Adapter.FDSIndex[instr] > long.MinValue;
        }

        private double GetProb(T instr, long cstep)
        {
            long asap = ASAP(instr);
            long alap = ALAP(instr);
            if (cstep < asap || cstep > alap)
                return 0.0;
            else if (IsScheduled(instr))
                return 1.0;
            else
                return 1.0 / (alap - asap + 1);
        }

        private void PrepareBusMinimization()
        {
            _operandConsumers = new Dictionary<int, List<T>>();
            foreach (T instr in Instructions)
            {
                foreach (int operand in Adapter.Operands[instr])
                {
                    _operandConsumers.Add(operand, instr);
                }
            }
        }

        private DG CreateDG()
        {
            if (MinimizeBuses)
                return new TransDG(this);
            else
                return new DG(this);
        }

        private void ComputeDG()
        {
            foreach (T instr in Instructions)
            {
                object op = Adapter.IClass[instr];
                DG dg;
                if (!_dg.TryGetValue(op, out dg))
                {
                    dg = CreateDG();
                    _dg[op] = dg;
                }
                dg.Consume(instr);
            }
        }

        private void ComputeIDs()
        {
            _id = new double[Instructions.Count];
            ComputeIDs(Instructions);
        }

        private void ComputeIDs(IEnumerable<T> instrs)
        {
            foreach (T instr in instrs)
            {
                object op = Adapter.IClass[instr];
                DG dg = _dg[op];
                long asap = ASAP(instr);
                long alap = ALAP(instr);
                double h = alap - asap + 1;
                double id = 0.0;
                for (long cstep = asap; cstep <= alap; cstep++)
                {
                    id += dg[cstep] / h;
                }
                _id[IndexOf(instr)] = id;
            }
        }

        private double GetStorageDG(T instr)
        {
            long myAsap = Adapter.ASAPIndex[instr];
            long myAlap = Adapter.ALAPIndex[instr];
            double sum = 0.0;
            int count = 0;
            foreach (int result in Adapter.Results[instr])
            {
                var consumers = _operandConsumers[result];
                long maxAsap = ASAP(consumers[0]);
                long maxAlap = ALAP(consumers[0]);
                for (int i = 1; i < consumers.Count; i++)
                {
                    maxAsap = Math.Max(maxAsap, ASAP(consumers[i]));
                    maxAlap = Math.Max(maxAlap, ALAP(consumers[i]));
                }
                long asapLife = maxAsap - myAsap;
                long alapLife = maxAlap - myAlap;
                long maxLife = maxAlap - myAsap;
                double avgLife = (asapLife + alapLife + maxLife) / 3.0;
                long overlap = 0;
                if (myAlap < maxAsap)
                    overlap = maxAsap - myAlap;
                if (maxLife - overlap > double.Epsilon)
                {
                    double storageDG = (avgLife - overlap) / (maxLife - overlap);
                    sum += storageDG;
                    count++;
                }
            }
            if (count > 0)
                return sum / count;
            else
                return 0.0;
        }

        private double GetForce(T instr, long nt, long nb)
        {
            if (IsScheduled(instr))
            {
                long cstep = Adapter.FDSIndex[instr];
                if (cstep < nt || cstep > nb)
                {
                    // This extension is required for proper support of multi-cycle operations:
                    // If an instruction is already scheduled and the query interval does not
                    // intersect with its c-step, return an "infinite" force to prevent predecessor/
                    // successor instructions being scheduled "too close" to the instruction.
                    return double.PositiveInfinity;
                }
            }
            object op = Adapter.IClass[instr];
            double sum = 0.0;
            double h = nb - nt + 1;
            DG dg = _dg[op];
            for (long cstep = nt; cstep <= nb; cstep++)
            {
                sum += dg[cstep] / h;
            }
            return sum - _id[IndexOf(instr)];
        }

        private double GetSelfForce(T instr, long cstep)
        {
            double force = GetForce(instr, cstep, cstep);
            if (MinimizeRegisters)
                force += GetStorageDG(instr);
            return force;
        }

        private double GetTotalForce(T instr, long cstep)
        {
            double force = GetSelfForce(instr, cstep);

            // Predecessor forces
            foreach (var dpred in Adapter.Preds[instr])
            {
                T pred = dpred.Task;
                long t = ASAP(pred);
                force += GetForce(pred, t, cstep - dpred.MinDelay);
            }

            // Successor forces
            foreach (var dsucc in Adapter.Succs[instr])
            {
                T succ = dsucc.Task;
                long b = ALAP(succ);
                force += GetForce(succ, cstep + dsucc.MinDelay, b);
            }

            return force;
        }

        private void Recompute(T instr)
        {
            var s = new Stack<T>(
                Adapter.Preds[instr].Select(_ => _.Task)
                .Concat(Adapter.Succs[instr].Select(_ => _.Task)));
            var l = new List<T>();
            l.Add(instr);
            while (s.Any())
            {
                T cur = s.Pop();

                long asap = ASAP(cur);
                long alap = ALAP(cur);
                long oldAsap = asap;
                long oldAlap = alap;
                foreach (var pred in Adapter.Preds[cur])
                {
                    asap = Math.Max(asap, ASAP(pred.Task) + pred.MinDelay);
                    alap = Math.Min(alap, ALAP(pred.Task) + pred.MaxDelay);
                }
                //long curlat = Adapter.Latency[cur];
                foreach (var succ in Adapter.Succs[cur])
                {
                    alap = Math.Min(alap, ALAP(succ.Task) - succ.MinDelay);
                    asap = Math.Max(asap, ASAP(succ.Task) - succ.MaxDelay);
                }
                if (asap > alap)
                    throw new NotSchedulableException();
                if (asap != oldAsap || alap != oldAlap)
                {
                    object op = Adapter.IClass[cur];
                    DG dg = _dg[op];
                    dg.Unconsume(cur);
                    SetASAP(cur, asap);
                    SetALAP(cur, alap);
                    dg.Consume(cur);
                    l.Add(cur);
                    foreach (var pred in Adapter.Preds[cur])
                    {
                        s.Push(pred.Task);
                    }
                    foreach (var succ in Adapter.Succs[cur])
                    {
                        s.Push(succ.Task);
                    }
                }
            }
            ComputeIDs(l);
        }

        private void Pin(T instr, long cstep)
        {
            Adapter.FDSIndex[instr] = cstep;
            if (ASAP(instr) != ALAP(instr))
            {
                object op = Adapter.IClass[instr];
                DG dg = _dg[op];
                dg.Unconsume(instr);
                SetASAP(instr, cstep);
                SetALAP(instr, cstep);
                dg.Consume(instr);
                Recompute(instr);
            }
        }

        private void Schedule()
        {
            var unscheduled = new LinkedList<T>(Instructions);
            while (unscheduled.Any())
            {
                var bestNode = unscheduled.First;
                T bestInstr = bestNode.Value;
                long bestCStep = ASAP(bestInstr);
                double minForce = GetTotalForce(bestInstr, bestCStep);
                var curNode = bestNode;
                while (curNode != unscheduled.Last)
                {
                    T instr = curNode.Value;
                    long asap = ASAP(instr);
                    long alap = ALAP(instr);

                    if (asap == alap)
                        // there will never be a different option for this instruction
                        // => we can abbreviate the process a little bit
                        break;

                    if (asap > alap)
                        throw new NotSchedulableException();

                    for (long cstep = asap; cstep <= alap; cstep++)
                    {                        
                        double force = GetTotalForce(instr, cstep);
                        if (force < minForce)
                        {
                            bestNode = curNode;
                            bestInstr = instr;
                            bestCStep = cstep;
                            minForce = force;
                        }
                    }
                    curNode = curNode.Next;
                }
                if (double.IsPositiveInfinity(minForce))
                    throw new NotSchedulableException();

                Pin(bestInstr, bestCStep);
                unscheduled.Remove(bestNode);
            }
        }

        public void Run()
        {
            Init();
            if (MinimizeBuses)
                PrepareBusMinimization();
            ComputeDG();
            ComputeIDs();
            Schedule();
        }

        public static void Schedule(IFDSAdapater<T> adapter, IList<T> instructions, 
            SchedulingConstraints constraints,
            bool minimizeBuses = true, bool minimizeRegisters = false)
        {
            var sched = new ForceDirectedSchedulerImpl<T>(adapter, instructions, constraints)
            {
                MinimizeBuses = minimizeBuses,
                MinimizeRegisters = minimizeRegisters
            };
            sched.Run();
        }

        public static void Schedule(IFDSAdapater<T> adapter, IEnumerable<IList<T>> blocks, 
            SchedulingConstraints constraints,
            bool minimizeBuses = true, bool minimizeRegisters = false)
        {
            foreach (var block in blocks)
            {
                Schedule(adapter, block, constraints, minimizeBuses, minimizeRegisters);
            }
        }
    }

    public class ForceDirectedScheduler : 
        IBasicBlockSchedulingAlgorithm,
        ICFGSchedulingAlgorithm
    {
        private class FDSAdapter<T> : IFDSAdapater<T>
        {
            private ISchedulingAdapter<T> _scha;

            public FDSAdapter(ISchedulingAdapter<T> scha)
            {
                _scha = scha;
            }

            public IPropMap<T, int> Index
            {
                get { return _scha.Index; }
            }

            public IPropMap<T, long> Latency
            {
                get { return _scha.Latency; }
            }

            public IPropMap<T, ScheduleDependency<T>[]> Preds
            {
                get { return _scha.Preds; }
            }

            public IPropMap<T, ScheduleDependency<T>[]> Succs
            {
                get { return _scha.Succs; }
            }

            private HashBasedPropMap<T, long> _asapIndex = new HashBasedPropMap<T, long>();
            public IPropMap<T, long> ASAPIndex
            {
                get { return _asapIndex; }
            }

            private HashBasedPropMap<T, long> _alapIndex = new HashBasedPropMap<T, long>();
            public IPropMap<T, long> ALAPIndex
            {
                get { return _alapIndex; }
            }

            public IPropMap<T, object> IClass
            {
                get { return _scha.IClass; }
            }

            public IPropMap<T, long> FDSIndex
            {
                get { return _scha.CStep; }
            }

            public IPropMap<T, int[]> Operands
            {
                get { return _scha.Operands; }
            }

            public IPropMap<T, int[]> Results
            {
                get { return _scha.Results; }
            }
        }

        private class ASLAPAdapter<T> : ISchedulingAdapter<T>
        {
            private ISchedulingAdapter<T> _scha;
            private IPropMap<T, long> _aslapIndex;

            public ASLAPAdapter(ISchedulingAdapter<T> scha, IPropMap<T, long> aslapIndex)
            {
                _scha = scha;
                _aslapIndex = aslapIndex;
            }

            public IPropMap<T, int> Index
            {
                get { return _scha.Index; }
            }

            public IPropMap<T, ScheduleDependency<T>[]> Preds
            {
                get { return _scha.Preds; }
            }

            public IPropMap<T, ScheduleDependency<T>[]> Succs
            {
                get { return _scha.Succs; }
            }

            public IPropMap<T, int[]> Operands
            {
                get { return _scha.Operands; }
            }

            public IPropMap<T, int[]> Results
            {
                get { return _scha.Results; }
            }

            public IPropMap<T, long> Latency
            {
                get { return _scha.Latency; }
            }

            public IPropMap<T, long> CStep
            {
                get { return _aslapIndex; }
            }

            public IPropMap<T, object> IClass
            {
                get { return _scha.IClass; }
            }

            public bool TryPin(T task, long cstep, out long preHint, out long postHint)
            {
                return _scha.TryPin(task, cstep, out preHint, out postHint);
            }

            public void ClearSchedule()
            {
                _scha.ClearSchedule();
            }
        }

        public void Schedule<T>(IEnumerable<T> tasks, ISchedulingAdapter<T> scha, SchedulingConstraints constraints)
        {
            var fdsa = new FDSAdapter<T>(scha);
            var asapa = new ASLAPAdapter<T>(scha, fdsa.ASAPIndex);
            var alapa = new ASLAPAdapter<T>(scha, fdsa.ALAPIndex);
            ASAPScheduler.InstanceUnlimitedResources.Schedule(tasks, asapa, constraints);
            
            int maxConcurrency = tasks.GroupBy(i => asapa.CStep[i])
                .Sum(grp => grp.GroupBy(j => asapa.IClass[j]).Max(g => g.Count() - 1));
            
            long maxExtent = constraints.EndTime - constraints.StartTime + maxConcurrency;
            long scaledExtent = (long)Math.Ceiling((constraints.EndTime - constraints.StartTime) * constraints.SchedScale);
            long extent = Math.Min(maxExtent, scaledExtent);
            long oldStartTime = constraints.StartTime;
            constraints.EndTime = constraints.StartTime + extent;
            ALAPScheduler.InstanceUnlimitedResources.Schedule(tasks, alapa, constraints);
            constraints.StartTime = oldStartTime;
            ForceDirectedSchedulerImpl<T>.Schedule(fdsa, tasks.ToList(), constraints);
        }

        public void Schedule<T>(ControlFlowGraph<T> cfg, SchedulingConstraints constraints, ISchedulingAdapter<T> scha) where T : IInstruction
        {
            foreach (var bb in cfg.BasicBlocks)
            {
                if (bb.IsExitBlock)
                    break;

                var fdsa = new FDSAdapter<T>(scha);
                var asapa = new ASLAPAdapter<T>(scha, fdsa.ASAPIndex);
                var alapa = new ASLAPAdapter<T>(scha, fdsa.ALAPIndex);
                long oldStartTime = constraints.StartTime;
                ASAPScheduler.InstanceUnlimitedResources.Schedule(bb.Range, asapa, constraints);
                ALAPScheduler.InstanceUnlimitedResources.Schedule(bb.Range, alapa, constraints);
                constraints.StartTime = oldStartTime;
                ForceDirectedSchedulerImpl<T>.Schedule(fdsa, bb.Range, constraints);

                constraints.StartTime = constraints.EndTime;
            }
        }

        public static readonly ForceDirectedScheduler Instance = new ForceDirectedScheduler();
    }
}

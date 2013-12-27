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
using SystemSharp.Analysis;
using SystemSharp.Assembler.DesignGen;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.Synthesis;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Assembler
{
#if false
    /// <summary>
    /// Represents a data structure capable of storing schedule for XIL-3 instructions. It is intended to be modified by the actual
    /// scheduling algorithm and to provide efficient access to the scheduling result.
    /// </summary>
    public interface IXILSchedule
    {
        /// <summary>
        /// Total number of instructions
        /// </summary>
        int InstructionCount { get; }

        /// <summary>
        /// Number of c-steps required by schedule
        /// </summary>
        /// <remarks>
        /// c-step is short for control step. c-steps are a fundamental abstraction of high-level synthesis: any scheduling algorithm
        /// assigns each instruction to a discrete time step, the c-step. Having synchronous hardware in mind, each c-step lasts exactly one
        /// clock period. Therefore, we could also call that thing "clock step".
        /// </remarks>
        int CStepCount { get; }

        /// <summary>
        /// Maps instruction indes to XIL-3 instruction
        /// </summary>
        IPropMap<int, XIL3Instr> Instructions { get; }

        /// <summary>
        /// Maps instruction index to c-step (actual scheduling result)
        /// </summary>
        IPropMap<int, int> CStep { get; }

        /// <summary>
        /// Maps instruction index to initiation interval
        /// </summary>
        IPropMap<int, int> InitiationInterval { get; }

        /// <summary>
        /// Maps instruction index to latency
        /// </summary>
        IPropMap<int, int> Latency { get; }

        /// <summary>
        /// Returns all instructions mapped to a given c-step
        /// </summary>
        /// <param name="cstep">c-step to query</param>
        /// <returns>all instructions mapped to that c-step</returns>
        IEnumerable<int> GetInstructionsOfCStep(int cstep);
    }

    /// <summary>
    /// Default implementation of <see cref="IXILSchedule"/> interface
    /// </summary>
    public class XILScheduleMonitor : IXILSchedule
    {
        private int[] _csteps;
        private int _maxCStep;
        private int[] _ii;
        private int[] _lat;

        public XILScheduleMonitor(XIL3Function func)
        {
            Func = func;
            _maxCStep = -1;
            _csteps = new int[func.Instructions.Length];
            _ii = new int[func.Instructions.Length];
            _lat = new int[func.Instructions.Length];
            Instructions = PropMaps.CreateForArray(Func.Instructions, EAccess.ReadOnly);
            CStep = PropMaps.CreateForArray(_csteps, EAccess.ReadOnly);
            InitiationInterval = PropMaps.CreateForArray(_ii, EAccess.ReadOnly);
            Latency = PropMaps.CreateForArray(_lat, EAccess.ReadOnly);
        }

        public XIL3Function Func { get; private set; }

        public void Bind(XIL3Instr xil3i, int cstep, IXILMapping mapping)
        {
            _csteps[xil3i.Index] = cstep;
            _ii[xil3i.Index] = mapping.InitiationInterval;
            _lat[xil3i.Index] = mapping.Latency;
            int lat = Math.Max(1, mapping.Latency);
            _maxCStep = Math.Max(cstep + lat - 1, _maxCStep);
        }

        public int InstructionCount
        {
            get { return Func.Instructions.Length; }
        }

        public int CStepCount
        {
            get { return _maxCStep + 1; }
        }

        public IPropMap<int, XIL3Instr> Instructions { get; private set; }
        public IPropMap<int, int> CStep { get; private set; }
        public IPropMap<int, int> InitiationInterval { get; private set; }
        public IPropMap<int, int> Latency { get; private set; }

        public IEnumerable<int> GetInstructionsOfCStep(int cstep)
        {
            return from XIL3Instr xil3i in Func.Instructions
                   where _csteps[xil3i.Index] == cstep
                   select xil3i.Index;
        }
    }
#endif

    /// <summary>
    /// Provides a scheduling adapter for XIL-3 instructions
    /// </summary>
    public class XILSchedulingAdapter : ISchedulingAdapter<XIL3Instr>
    {
        private class DummyBinder : IAutoBinder
        {
            public SignalBase GetSignal(EPortUsage portUsage, string portName, string domainID, object initialValue)
            {
                return Signals.CreateInstance(initialValue);
            }

            public ProcessDescriptor CreateProcess(Process.EProcessKind kind, SysDOM.Function func, params ISignalOrPortDescriptor[] sensitivity)
            {
                return null;
            }

            public TypeDescriptor CreateEnumType(string name, IEnumerable<string> literals)
            {
                throw new InvalidOperationException("Type creation not excpected");
            }

            public ISignalOrPortDescriptor GetSignal(EBinderFlags flags, EPortUsage portUsage, string name, string domainID, object initialValue)
            {
                return null;
            }
        }

        private XIL3Function _func;
        private ControlFlowGraph<XIL3Instr> _cfg;
        private XILMapperManager _xmm;
        private XILAllocator _alloc;
        private DummyBinder _binder = new DummyBinder();
        private IXILMapping[] _mappingCache;
        private ScheduleDependency<XIL3Instr>[][] _predsBack;
        private ScheduleDependency<XIL3Instr>[][] _succsBack;
        private object[] _classifyCache;
        private EMappingKind[] _mappingKindCache;
        private long[] _cstepBack;
        private Dictionary<object, int> _maxFUs = new Dictionary<object, int>();
        private Dictionary<Tuple<object, long>, int> _resTable = new Dictionary<Tuple<object, long>, int>();

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="func">XIL-3 function</param>
        /// <param name="xmm">mapper manager</param>
        /// <param name="host">component instance to host allocated functional units</param>
        /// <param name="targetProject">code generation target project</param>
        public XILSchedulingAdapter(XIL3Function func, XILMapperManager xmm, Component host, IProject targetProject)
        {
            _func = func;
            _cfg = Compilation.CreateCFG(func.Instructions);
            _xmm = xmm;
            _alloc = xmm.CreateAllocator(host, targetProject);
            _operands = new DelegatePropMap<XIL3Instr, int[]>(i => i.OperandSlots);
            _results = new DelegatePropMap<XIL3Instr, int[]>(i => i.ResultSlots);
            _mappingCache = new IXILMapping[_func.Instructions.Length];
            _classifyCache = new object[_func.Instructions.Length];
            _mappingKindCache = new EMappingKind[_func.Instructions.Length];
            _latency = new DelegatePropMap<XIL3Instr, long>(i => GetMapping(i).Latency);
            _index = new DelegatePropMap<XIL3Instr, int>(i => i.Index);
            _iclass = new DelegatePropMap<XIL3Instr, object>(i => Classify(i));
            _cstepBack = new long[_func.Instructions.Length];
            _cstep = new ArrayBackedPropMap<XIL3Instr, long>(_cstepBack, i => i.Index);
            Init();
        }

        private ScheduleDependency<XIL3Instr> ToScheduleDependency(XIL3Instr instr, InstructionDependency dep)
        {
            var predInstr = _func.Instructions[dep.PredIndex];
            var odep = dep as OrderDependency;
            var tdep = dep as TimeDependency;
            if (odep != null)
            {
                switch (odep.Kind)
                {
                    case OrderDependency.EKind.BeginAfter:
                        return new ScheduleDependency<XIL3Instr>(predInstr, Latency[predInstr], int.MaxValue);

                    case OrderDependency.EKind.CompleteAfter:
                        return new ScheduleDependency<XIL3Instr>(predInstr, Latency[predInstr] - Latency[instr], int.MaxValue);

                    default:
                        throw new NotImplementedException();
                }
            }
            else if (tdep != null)
            {
                return new ScheduleDependency<XIL3Instr>(predInstr, tdep.MinDelay, tdep.MaxDelay);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void Init()
        {
            int numInstrs = _func.Instructions.Length;
            _predsBack = new ScheduleDependency<XIL3Instr>[numInstrs][];
            _succsBack = new ScheduleDependency<XIL3Instr>[numInstrs][];
            var tmp = new List<ScheduleDependency<XIL3Instr>>[numInstrs];
            var rslot2instr = new Dictionary<int, XIL3Instr>();
            foreach (XIL3Instr i in _func.Instructions)
            {
                foreach (int rslot in i.ResultSlots)
                {
                    rslot2instr[rslot] = i;
                }
                var bb = _cfg.GetBasicBlockContaining(i.Index);
                _predsBack[i.Index] = i.Preds
                    .Select(p => ToScheduleDependency(i, p))
                    .Concat(i.OperandSlots.Select(s => 
                            new ScheduleDependency<XIL3Instr>(
                                rslot2instr[s], 
                                Latency[rslot2instr[s]], 
                                int.MaxValue)))
                    .Distinct()
                    .Where(_ => bb.Contains(_.Task)) // only admit predecessors from same basic block
                    .ToArray();
                tmp[i.Index] = new List<ScheduleDependency<XIL3Instr>>();
            }
            foreach (XIL3Instr i in _func.Instructions)
            {
                foreach (var sdep in _predsBack[i.Index])
                {
                    tmp[sdep.Task.Index].Add(new ScheduleDependency<XIL3Instr>(i, sdep.MinDelay, sdep.MaxDelay));
                }
            }
            foreach (XIL3Instr i in _func.Instructions)
            {
                _succsBack[i.Index] = tmp[i.Index].ToArray();
            }
            _preds = PropMaps.CreateForArray<XIL3Instr, ScheduleDependency<XIL3Instr>[]>(_predsBack, i => i.Index, EAccess.ReadOnly);
            _succs = PropMaps.CreateForArray<XIL3Instr, ScheduleDependency<XIL3Instr>[]>(_succsBack, i => i.Index, EAccess.ReadOnly);
        }

        /// <summary>
        /// Returns the control-flow graph of scheduled instructions
        /// </summary>
        public ControlFlowGraph<XIL3Instr> CFG
        {
            get { return _cfg; }
        }

        private IPropMap<XIL3Instr, ScheduleDependency<XIL3Instr>[]> _preds;

        /// <summary>
        /// Maps each instruction to its predecessor dependencies
        /// </summary>
        public IPropMap<XIL3Instr, ScheduleDependency<XIL3Instr>[]> Preds
        {
            get { return _preds; }
        }

        private IPropMap<XIL3Instr, ScheduleDependency<XIL3Instr>[]> _succs;

        /// <summary>
        /// Maps each instruction to its successor dependencies
        /// </summary>
        public IPropMap<XIL3Instr, ScheduleDependency<XIL3Instr>[]> Succs
        {
            get { return _succs; }
        }

        private IPropMap<XIL3Instr, int[]> _operands;

        /// <summary>
        /// Maps each instruction to its operand slots
        /// </summary>
        public IPropMap<XIL3Instr, int[]> Operands
        {
            get { return _operands; }
        }

        private IPropMap<XIL3Instr, int[]> _results;

        /// <summary>
        /// Maps each instruction to its result slots
        /// </summary>
        public IPropMap<XIL3Instr, int[]> Results
        {
            get { return _results; }
        }

        /// <summary>
        /// Returns the resource allocator
        /// </summary>
        public XILAllocator Allocator
        {
            get { return _alloc; }
        }

        private IXILMapping GetMapping(XIL3Instr xil3i)
        {
            var mapping = _mappingCache[xil3i.Index];
            if (mapping == null)
            {
                TypeDescriptor[] otypes = xil3i.OperandSlots.Select(i => _func.SlotTypes[i]).ToArray();
                TypeDescriptor[] rtypes = xil3i.ResultSlots.Select(i => _func.SlotTypes[i]).ToArray();
                mapping = _alloc.TryMap(xil3i.Command, otypes, rtypes, _binder);
                if (mapping == null)
                    throw new XILSchedulingFailedException("Failed to map instruction: " + xil3i.Command);
                _mappingCache[xil3i.Index] = mapping;
            }
            return mapping;
        }

        private object Classify(XIL3Instr xil3i)
        {
            object iclass = _classifyCache[xil3i.Index];
            if (iclass == null)
            {
                TypeDescriptor[] otypes = xil3i.OperandSlots.Select(i => _func.SlotTypes[i]).ToArray();
                TypeDescriptor[] rtypes = xil3i.ResultSlots.Select(i => _func.SlotTypes[i]).ToArray();
                iclass = _alloc.Classify(xil3i.Command, otypes, rtypes, _binder);
                if (iclass == null)
                    throw new XILSchedulingFailedException("Failed to classify instruction: " + xil3i.Command);
                _classifyCache[xil3i.Index] = iclass;
            }
            return iclass;
        }

        private IPropMap<XIL3Instr, long> _latency;

        /// <summary>
        /// Maps each instruction to its latency
        /// </summary>
        public IPropMap<XIL3Instr, long> Latency
        {
            get { return _latency; }
        }

        private IPropMap<XIL3Instr, object> _iclass;

        /// <summary>
        /// Maps each instruction to its equivalence class (all instruction of same class can be mapped to same type
        /// of functional unit).
        /// </summary>
        public IPropMap<XIL3Instr, object> IClass
        {
            get { return _iclass; }
        }

        private IPropMap<XIL3Instr, int> _index;

        /// <summary>
        /// Maps each instruction to its positional index
        /// </summary>
        public IPropMap<XIL3Instr, int> Index
        {
            get { return _index; }
        }

        private IPropMap<XIL3Instr, long> _cstep;

        /// <summary>
        /// Maps each instruction to the c-step at which it is scheduled
        /// </summary>
        public IPropMap<XIL3Instr, long> CStep
        {
            get { return _cstep; }
        }

        /// <summary>
        /// Constrains the maximum quantity of functional units for a given instruction class.
        /// </summary>
        /// <param name="iclass">instruction class</param>
        /// <param name="max">upper limit of functional unit instances</param>
        public void SetMaxFUAllocation(object iclass, int max)
        {
            _maxFUs[iclass] = max;
        }

        public bool TryPin(XIL3Instr task, long cstep, out long preHint, out long postHint)
        {
            object iclass = Classify(task);
            var mapping = GetMapping(task);
            int max;
            if (mapping.ResourceKind != EMappingKind.LightweightResource &&
                _maxFUs.TryGetValue(iclass, out max))
            {
                int ii = GetMapping(task).InitiationInterval;
                bool found;
                long preStep = cstep, postStep = cstep;
                do
                {
                    found = true;
                    for (long i = preStep; i < preStep + ii; i++)
                    {
                        int alloc;
                        _resTable.TryGetValue(Tuple.Create(iclass, i), out alloc);
                        if (alloc == max)
                        {
                            found = false;
                            --preStep;
                            break;
                        }
                    }
                    for (long i = postStep; i < postStep + ii; i++)
                    {
                        int alloc;
                        _resTable.TryGetValue(Tuple.Create(iclass, i), out alloc);
                        if (alloc == max)
                        {
                            found = false;
                            ++postStep;
                            break;
                        }
                    }
                } while (!found);
                preHint = preStep;
                postHint = postStep;
                if (preStep == postStep)
                {
                    for (long i = cstep; i < cstep + ii; i++)
                    {
                        var key = Tuple.Create(iclass, i);
                        int alloc;
                        _resTable.TryGetValue(key, out alloc);
                        ++alloc;
                        _resTable[key] = alloc;
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                preHint = cstep;
                postHint = cstep;
                _cstepBack[task.Index] = cstep;
                return true;
            }
        }

        public void ClearSchedule()
        {
            _resTable.Clear();
            Array.Clear(_cstepBack, 0, _cstepBack.Length);
        }

        /// <summary>
        /// Returns the total number of c-steps required by the schedule.
        /// </summary>
        public long ComputeCStepCount()
        {
            return _mappingCache.Zip(_cstepBack, (m, n) => n + m.Latency).Max();
        }

        /// <summary>
        /// Performs resource allocation and binding.
        /// </summary>
        /// <param name="dpb">datapath builder to use</param>
        /// <returns>resulting flow matrix</returns>
        public FlowMatrix Allocate(IDatapathBuilder dpb)
        {
            // Step 1: create intermediate storage for each instruction output slot
            var tempSignals = new SLVSignal[_func.SlotTypes.Length];
            for (int i = 0; i < _func.SlotTypes.Length; i++)
            {
                int width = TypeLowering.Instance.GetWireWidth(_func.SlotTypes[i]);
                var initial = StdLogicVector.Us(width);
                tempSignals[i] = (SLVSignal)Signals.CreateInstance(initial);
                tempSignals[i].Descriptor.TagTemporary(i);
            }

            // Step 2: Allocate and establish unit bindings
            Action<IXILMapping> onAlloc = map => dpb.AddFU(map.TASite.Host);
            _alloc.OnFUAllocation += onAlloc;
            var mappings = new Dictionary<int, IXILMapping>();
            foreach (var xil3i in _func.Instructions)
            {
                TypeDescriptor[] otypes = xil3i.OperandSlots.Select(i => _func.SlotTypes[i]).ToArray();
                TypeDescriptor[] rtypes = xil3i.ResultSlots.Select(i => _func.SlotTypes[i]).ToArray();
                long cstep = CStep[xil3i];
                var mapping = _alloc.TryBind(xil3i, cstep, otypes, rtypes);
                if (mapping == null)
                    throw new XILSchedulingFailedException("Realization failed: " + xil3i.Command);
                mappings[xil3i.Index] = mapping;
                mapping.TASite.Establish(dpb.FUBinder);
                Allocator.Policy.TellMapping(xil3i, cstep, mapping);
            }
            _alloc.OnFUAllocation -= onAlloc;

            // Step 3: Assemble flow matrix
            var result = new FlowMatrix();
            foreach (var xil3i in _func.Instructions)
            {
                long cstep = CStep[xil3i];
                var mapping = mappings[xil3i.Index];
                result.AddNeutral(mapping.TASite.DoNothing());
                var impl = mapping.Realize(
                    xil3i.OperandSlots.Select(s => tempSignals[s].AsSignalSource<StdLogicVector>()).ToArray(),
                    xil3i.ResultSlots.Select(s => tempSignals[s].AsCombSink()).ToArray());
                if (impl.Count() - Latency[xil3i] > 1)
                    throw new XILSchedulingFailedException("Mapping length exceeds scheduled latency");
                result.Add((int)cstep, impl);
                result.AppendComment((int)cstep, ToComment(xil3i));
                _mappingCache[xil3i.Index] = mapping;
            }

            return result;
        }

        private string ToComment(XIL3Instr xil3i)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(xil3i.Index);
            sb.Append(": ");
            sb.Append(xil3i.Command.ToString());
            sb.Append(" (");
            sb.Append(string.Join(", ", xil3i.OperandSlots));
            sb.Append(") => (");
            sb.Append(string.Join(", ", xil3i.ResultSlots));
            sb.Append(")");
            if (xil3i.Preds.Length > 0)
            {
                sb.Append(" ");
                sb.Append(string.Join<InstructionDependency>(", ", xil3i.Preds));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a textual report on the found schedule
        /// </summary>
        public string GetScheduleReport()
        {
            var sb = new StringBuilder();
            var grouping = _func.Instructions
                .Select(i => Tuple.Create(i, CStep[i]))
                .GroupBy(tup => tup.Item2)
                .OrderBy(grp => grp.Key);
            foreach (var group in grouping)
            {
                sb.AppendLine(group.Key.ToString());
                foreach (var tup in group)
                {
                    sb.AppendLine("  " + ToComment(tup.Item1));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a textual report on the found resource binding
        /// </summary>
        public string GetBindingReport()
        {
            var sb = new StringBuilder();
            var grouping = _func.Instructions
                .Select(i => Tuple.Create(i, CStep[i]))
                .GroupBy(tup => tup.Item2)
                .OrderBy(grp => grp.Key);
            foreach (var group in grouping)
            {
                sb.AppendLine(group.Key.ToString());
                foreach (var tup in group)
                {
                    var xil3i = tup.Item1;
                    var mapping = _mappingCache[xil3i.Index];
                    sb.AppendLine("  " + ToComment(xil3i) + " on " + mapping.TASite.Name);
                }
            }
            return sb.ToString();
        }
    }
}

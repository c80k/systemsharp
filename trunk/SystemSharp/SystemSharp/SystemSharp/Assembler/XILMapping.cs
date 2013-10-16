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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.Synthesis;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Assembler
{
    /// <summary>
    /// Serves as an aggregate XIL mapper. From all registered mappers, this class selects the first suitable mapper.
    /// </summary>
    public class XILMapperManager : IXILMapper
    {
        private List<IXILMapper> _mappers = new List<IXILMapper>();
        private Dictionary<string, List<IXILMapper>> _mlookup = new Dictionary<string, List<IXILMapper>>();

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public XILMapperManager()
        {
        }

        /// <summary>
        /// Registers a XIL mapper
        /// </summary>
        /// <param name="mapper">mapper to register</param>
        public void AddMapper(IXILMapper mapper)
        {
            _mappers.Add(mapper);
            foreach (XILInstr instr in mapper.GetSupportedInstructions())
                _mlookup.Add(instr.Name, mapper);
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            return _mappers.SelectMany(m => m.GetSupportedInstructions());
        }

        public IEnumerable<IXILMapper> LookupMappers(XILInstr instr)
        {
            return _mlookup.Get(instr.Name);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var mappers = _mlookup.Get(instr.Name);
            return mappers.SelectMany(m => m.TryMap(taSite, instr, operandTypes, resultTypes));
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            List<IXILMapper> mappers = _mlookup.Get(instr.Name);
            foreach (IXILMapper mapper in mappers)
            {
                IXILMapping mapping = mapper.TryAllocate(host, instr, operandTypes, resultTypes, proj);
                if (mapping != null)
                {
                    return mapping;
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a functional unit allocator
        /// </summary>
        /// <param name="host">component which will host all created functional units</param>
        /// <param name="targetProject">project being generated</param>
        /// <returns>functional unit allocator</returns>
        public XILAllocator CreateAllocator(Component host, IProject targetProject)
        {
            return new XILAllocator(this, host, targetProject);
        }
    }

    /// <summary>
    /// Models the outcome of functional unit allocation policy
    /// </summary>
    public enum EAllocationDecision
    {
        /// <summary>
        /// Map instruction to existing functional unit (re-use)
        /// </summary>
        UseExisting,

        /// <summary>
        /// Map instruction to new functional unit
        /// </summary>
        AllocateNew
    }

    /// <summary>
    /// Generic interface of allocation policy for hardware functional units
    /// </summary>
    /// <remarks>
    /// The interaction between the resource allocator and allocation policy follows a two-step scheme: For each XIL-3 instruction
    /// to be mapped to hardware, the resource allocator asks (<see cref="SelectBestMapping"/>) the allocation policy to pick the most 
    /// suitable mapping from a sequence of possible mappings. The policy either selects a mapping or opts to create a new functional unit
    /// for the given instruction. In the latter case, the resource allocator constructs a suitable unit and maps the instruction to that unit.
    /// In any case, the resource allocator informs the allocation policy about the mapping via <see cref="TellMapping"/>.
    /// </remarks>
    public interface IAllocationPolicy
    {
        /// <summary>
        /// Selects the most suitable functional unit and the best mapping for a given instruction or opts to allocate a new functional unit.
        /// </summary>
        /// <remarks>
        /// The algorithm implementing the policy tries to solve an optimization problem: Since selected mapping implies interconnections
        /// between functional units, a good policy tries to minimize the quantities and sizes of datapath multiplexers or even tries to
        /// optimize the overall place-and-route result of the datapath being constructed. Moreover, there might be alternative mappings to the
        /// same functional unit. For example, addition is a commutative operation. Depending on how we connect operands to the ports of the adder,
        /// we might either re-use existing connections (good) or introduce new input multiplexers (bad). A limitation of this interface is that we are
        /// currently restricted a greedy heuristics: once a mapping decision is made, it cannot be altered in the future. Therefore, we cannot
        /// guarantee to find a globally optimal solution.
        /// </remarks>
        /// <param name="instr">XIL-3 instruction to be mapped</param>
        /// <param name="cstep">c-step at which instruction is scheduled</param>
        /// <param name="mappings">sequence of possible mappings</param>
        /// <param name="bestMapping">the mapping from <paramref name="mappings"/> found to be most suitable by the policy</param>
        /// <returns>whether it is best to use one of the supplied mappings or to allocate a new functional unit</returns>
        EAllocationDecision SelectBestMapping(XIL3Instr instr, long cstep, IEnumerable<IXILMapping> mappings, out IXILMapping bestMapping);

        /// <summary>
        /// Informs the allocation policy about the actual mapping used by resource allocator.
        /// </summary>
        /// <param name="instr">mapped XIL-3 instruction</param>
        /// <param name="cstep">c-step at which instruction is scheduled</param>
        /// <param name="mapping">selected mapping</param>
        void TellMapping(XIL3Instr instr, long cstep, IXILMapping mapping);
    }

    /// <summary>
    /// Factory pattern for hardware functional unit allocation policy
    /// </summary>
    public interface IAllocationPolicyFactory
    {
        /// <summary>
        /// Creates an allocation policy
        /// </summary>
        IAllocationPolicy Create();
    }

    /// <summary>
    /// Implements a default allocation policy, without any optimality.
    /// </summary>
    public class DefaultAllocationPolicy :
        IAllocationPolicy
    {
        private class FactoryImpl : IAllocationPolicyFactory
        {
            public IAllocationPolicy Create()
            {
                return new DefaultAllocationPolicy();
            }
        }

        /// <summary>
        /// Factory instance for creating a default allocation policy
        /// </summary>
        public static readonly IAllocationPolicyFactory Factory = new FactoryImpl();

        public EAllocationDecision SelectBestMapping(XIL3Instr instr, long cstep, IEnumerable<IXILMapping> mappings, out IXILMapping bestMapping)
        {
            bestMapping = mappings.First();
            return EAllocationDecision.UseExisting;
        }

        public void TellMapping(XIL3Instr instr, long cstep, IXILMapping mapping)
        {
        }
    }

    /// <summary>
    /// Resource allocator, responsible for mapping XIL-3 instructions to hardware functional units with the help of an allocation policy.
    /// </summary>
    public class XILAllocator
    {
        private XILMapperManager _xmm;
        private Dictionary<IXILMapper, List<ITransactionSite>> _taBindLookup = new Dictionary<IXILMapper, List<ITransactionSite>>();
        private Dictionary<IXILMapper, List<ITransactionSite>> _taMapLookup = new Dictionary<IXILMapper, List<ITransactionSite>>();
        private Dictionary<XILSInstr, IXILMapping> _xilMappings = new Dictionary<XILSInstr, IXILMapping>(XILSInstr.ContentComparer);
        private CacheDictionary<ITransactionSite, ReservationTable> _resTables;
        private Action<IXILMapping> _onAllocation;
        private Component _host;
        private IProject _targetProject;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="xmm">XIL mapper manager</param>
        /// <param name="host">component instance to host all created functional units</param>
        /// <param name="targetProject">target project</param>
        internal XILAllocator(XILMapperManager xmm, Component host, IProject targetProject)
        {
            _xmm = xmm;
            _host = host;
            _targetProject = targetProject;
            _resTables = new CacheDictionary<ITransactionSite, ReservationTable>(CreateReservationTable);
            Policy = new DefaultAllocationPolicy();
        }

        /// <summary>
        /// Resource allocation policy
        /// </summary>
        public IAllocationPolicy Policy { get; set; }

        private ReservationTable CreateReservationTable(ITransactionSite taSite)
        {
            return new ReservationTable();
        }

        /// <summary>
        /// Triggered whenever a new functional unit was created
        /// </summary>
        public event Action<IXILMapping> OnFUAllocation
        {
            add { _onAllocation += value; }
            remove { _onAllocation -= value; }
        }

        /// <summary>
        /// Tries to map the given XIL instruction to any suitable functional unit. This call will not create any actual hardware. It is used by the
        /// scheduler to query basic instruction metrics, namely initiation interval and latency.
        /// </summary>
        /// <param name="instr">XIL instruction</param>
        /// <param name="operandTypes">operand types of XIL instruction</param>
        /// <param name="resultTypes">result types of XIL instruction</param>
        /// <param name="binder">binder service</param>
        /// <returns>a hardware mapping for the supplied instruction or null if no such exists</returns>
        public IXILMapping TryMap(XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IAutoBinder binder)
        {
            var xilsi = instr.CreateStk(new InstructionDependency[0], operandTypes, resultTypes);

            IXILMapping mapping;
            if (_xilMappings.TryGetValue(xilsi, out mapping))
            {
                return mapping;
            }
            IEnumerable<IXILMapper> mappers = _xmm.LookupMappers(instr);
            foreach (IXILMapper mapper in mappers)
            {
                var tas = _taMapLookup.Get(mapper);
                foreach (var ta in tas)
                {
                    var mappings = mapper.TryMap(ta, instr, operandTypes, resultTypes);
                    if (mappings.Any())
                    {
                        mapping = mappings.First();
                        _xilMappings[xilsi] = mapping;
                        return mapping;
                    }
                }
            }
            foreach (IXILMapper mapper in mappers)
            {
                mapping = mapper.TryAllocate(_host, instr, operandTypes, resultTypes, _targetProject);
                if (mapping != null)
                {
                    _taMapLookup.Add(mapper, mapping.TASite);
                    _xilMappings[xilsi] = mapping;
                    return mapping;
                }
            }
            return null;
        }

        /// <summary>
        /// Tries to map and bind a given XIL-3 instruction to hardware
        /// </summary>
        /// <param name="instr">XIL-3 instruction to be mapped and bound</param>
        /// <param name="cstep">c-step at which instruction is scheduled</param>
        /// <param name="operandTypes">operand types of instruction</param>
        /// <param name="resultTypes">result types of instruction</param>
        /// <returns>a hardware mapping for the supplied instruction or null if no such exists</returns>
        public IXILMapping TryBind(XIL3Instr instr, long cstep, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            ReservationTable rtbl;
            var mappers = _xmm.LookupMappers(instr.Command);
            var viableMappings = new List<IXILMapping>();
            foreach (IXILMapper mapper in mappers)
            {
                var tas = _taBindLookup.Get(mapper);
                foreach (var ta in tas)
                {
                    var mappings = mapper.TryMap(ta, instr.Command, operandTypes, resultTypes);
                    rtbl = _resTables[ta];
                    foreach (var mapping in mappings)
                    {
                        if (mapping.InitiationInterval > 0)
                        {
                            if (!rtbl.IsReserved(cstep, cstep + mapping.InitiationInterval - 1))
                                viableMappings.Add(mapping);
                            else if (mapping.ResourceKind == EMappingKind.ExclusiveResource)
                                return null;
                        }
                        else
                        {
                            viableMappings.Add(mapping);
                        }
                    }
                }
            }
            bool allocateNew = true;
            IXILMapping bestMapping = null;
            int lat = 0;
            if (viableMappings.Count > 0)
            {
                lat = viableMappings.First().Latency;
                if (!viableMappings.All(m => m.Latency == lat))
                    throw new XILSchedulingFailedException("Mappings with different latencies exist for " + instr);

                if (viableMappings.All(m => m.ResourceKind == EMappingKind.ExclusiveResource))
                {
                    allocateNew = false;
                    bestMapping = viableMappings.First();
                }
                else if (viableMappings.All(m => m.ResourceKind == EMappingKind.LightweightResource))
                {
                    allocateNew = true;
                }
                else
                {
                    allocateNew = Policy.SelectBestMapping(instr, cstep, viableMappings, out bestMapping) ==
                        EAllocationDecision.AllocateNew;
                }
            }
            if (allocateNew)
            {
                foreach (IXILMapper mapper in mappers)
                {
                    bestMapping = mapper.TryAllocate(_host, instr.Command, operandTypes, resultTypes, _targetProject);
                    if (bestMapping != null)
                    {
                        if (viableMappings.Count > 0 && bestMapping.Latency != lat)
                            throw new XILSchedulingFailedException("Newly allocated mapping for " + instr + " has different latency.");

                        if (_onAllocation != null)
                            _onAllocation(bestMapping);
                        _taBindLookup.Add(mapper, bestMapping.TASite);
                        //bestMapping.TASite.Establish(binder);
                        break;
                    }
                }
            }
            if (bestMapping == null)
                return null;
            rtbl = _resTables[bestMapping.TASite];
            bool ok = rtbl.TryReserve(cstep, cstep + bestMapping.InitiationInterval - 1, instr);
            Debug.Assert(ok);
            return bestMapping;
        }

        private class ClassifyCookie
        {
            private XILInstr _instr;

            public ClassifyCookie(XILInstr instr)
            {
                Contract.Requires(instr != null);
                _instr = instr;
            }

            public override string ToString()
            {
                return "Cookie[" + _instr.ToString() + "]";
            }

            public override int GetHashCode()
            {
                return RuntimeHelpers.GetHashCode(_instr);
            }

            public override bool Equals(object obj)
            {
                //FIXME: Shouldn't that be "return obj == this"???

                var other = obj as ClassifyCookie;
                if (other == null)
                    return false;

                return _instr == other._instr;
            }
        }

        /// <summary>
        /// Classifies a given XIL instruction, such that all instructions which may be mapped to the same type of hardware functional unit
        /// belong to the same group.
        /// </summary>
        /// <param name="instr">XIL instruction</param>
        /// <param name="operandTypes">types of instruction operands</param>
        /// <param name="resultTypes">types of instruction results</param>
        /// <param name="binder">binder service</param>
        /// <returns>Opaque group identifier. Do not make any assumptions on the content or type of the returned object.
        /// The important thing is that any instructions belonging to the same group will see the same object.</returns>
        public object Classify(XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IAutoBinder binder)
        {
            var mapping = TryMap(instr, operandTypes, resultTypes, binder);
            if (mapping == null)
                return null;
            if (mapping.ResourceKind == EMappingKind.ExclusiveResource ||
                mapping.ResourceKind == EMappingKind.LightweightResource)
            {
                return new ClassifyCookie(instr);
            }
            else
            {
                return mapping.TASite;
            }
        }

        /// <summary>
        /// Returns the current resource allocation
        /// </summary>
        public IEnumerable<Tuple<Component, ReservationTable>> Allocation
        {
            get { return _resTables.Select(kvp => Tuple.Create(kvp.Key.Host, kvp.Value)); }
        }

        /// <summary>
        /// Computes statistics on resource allocation and returns them in a dedicated data structure
        /// </summary>
        /// <param name="scheduleLength">schedule length</param>
        /// <returns>resource allocation statistics</returns>
        public AllocationStatistics CreateAllocationStatistics(long scheduleLength)
        {
            return new AllocationStatistics(Allocation, scheduleLength);
        }
    }

    /// <summary>
    /// Reports statistics on hardware resource allocation
    /// </summary>
    public class AllocationStatistics
    {
        /// <summary>
        /// Reports statistics on a specific hardware functional unit instance
        /// </summary>
        public class FUStatistics
        {
            /// <summary>
            /// Constructs a new instance
            /// </summary>
            /// <param name="owner">superordinate statistics object</param>
            /// <param name="fu">hardware functional unit instance</param>
            /// <param name="reservation">reservation table</param>
            internal FUStatistics(AllocationStatistics owner, Component fu, ReservationTable reservation)
            {
                Owner = owner;
                FU = fu;
                Reservation = reservation;
            }

            /// <summary>
            /// Superordinate statistics object
            /// </summary>
            public AllocationStatistics Owner { get; private set; }

            /// <summary>
            /// Hardware functional unit instance
            /// </summary>
            public Component FU { get; private set; }

            /// <summary>
            /// Reservation table
            /// </summary>
            public ReservationTable Reservation { get; private set; }

            /// <summary>
            /// The occupation is the total number of c-steps where the functional unit is actually performing work.
            /// </summary>
            public long Occupation
            {
                get { return Reservation.GetOccupation(); }
            }

            /// <summary>
            /// The utilization is the ratio of occupation to total schedule length.
            /// </summary>
            public double Utilization
            {
                get { return Reservation.GetUtilization(Owner.ScheduleLength); }
            }
        }

        /// <summary>
        /// Reports aggregated statistics for a certain class of hardware functional units.
        /// </summary>
        public class FUTypeStatistics
        {
            /// <summary>
            /// Class of functional unit
            /// </summary>
            public Type FUType { get; private set; }

            /// <summary>
            /// List of per-instance statistics belonging to the class
            /// </summary>
            public IList<FUStatistics> FUs { get; private set; }

            /// <summary>
            /// Functional unit class name
            /// </summary>
            public string FUTypeName
            {
                get { return FUType.Name; }
            }

            /// <summary>
            /// Constructs an instance
            /// </summary>
            /// <param name="fuType">class of functional unit</param>
            internal FUTypeStatistics(Type fuType)
            {
                FUType = fuType;
                FUs = new List<FUStatistics>();
            }

            /// <summary>
            /// Average utilization of all per-instance utilizations
            /// </summary>
            public double AvgUtilization
            {
                get { return FUs.Average(fu => fu.Utilization); }
            }

        }

        private CacheDictionary<Type, FUTypeStatistics> _stats;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="allocation">resource allocation information</param>
        /// <param name="scheduleLength">schedule length</param>
        internal AllocationStatistics(IEnumerable<Tuple<Component, ReservationTable>> allocation,
            long scheduleLength)
        {
            Allocation = allocation;
            ScheduleLength = scheduleLength;
            _stats = new CacheDictionary<Type, FUTypeStatistics>(CreateFUTypeStats);
            Setup();
        }

        private FUTypeStatistics CreateFUTypeStats(Type fuType)
        {
            return new FUTypeStatistics(fuType);
        }

        /// <summary>
        /// Allocation information (data basis for these statistics)
        /// </summary>
        public IEnumerable<Tuple<Component, ReservationTable>> Allocation { get; private set; }

        /// <summary>
        /// Schedule length
        /// </summary>
        public long ScheduleLength { get; private set; }

        private void Setup()
        {
            foreach (var tup in Allocation)
            {
                FUTypeStatistics futs = _stats[tup.Item1.GetType()];
                futs.FUs.Add(new FUStatistics(this, tup.Item1, tup.Item2));
            }
        }

        /// <summary>
        /// Statistical (and detailed) data grouped by functional unit class
        /// </summary>
        public IEnumerable<FUTypeStatistics> FUTypeStats
        {
            get { return _stats.Values; }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var stats = FUTypeStats;
            foreach (var stat in stats)
            {
                sb.Append("FU type: ");
                sb.Append(stat.FUTypeName);
                sb.Append(", #");
                sb.Append(stat.FUs.Count);
                sb.Append(", utilization: ");
                sb.AppendLine(stat.AvgUtilization * 100.0 + "%");
                var fustats = stat.FUs.OrderBy(fustat => fustat.FU.Descriptor.Name);
                foreach (var fustat in fustats)
                {
                    sb.Append("  " + fustat.FU.Descriptor.Name + ": ");
                    sb.Append("occupation = " + fustat.Occupation);
                    sb.AppendLine(", utilization = " + fustat.Utilization);
                }
            }
            return sb.ToString();
        }
    }
}

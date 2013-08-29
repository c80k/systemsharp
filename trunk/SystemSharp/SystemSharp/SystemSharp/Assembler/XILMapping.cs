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
    public class XILMapperManager : IXILMapper
    {
        private List<IXILMapper> _mappers = new List<IXILMapper>();
        private Dictionary<string, List<IXILMapper>> _mlookup = new Dictionary<string, List<IXILMapper>>();

        public XILMapperManager()
        {
        }

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

        public XILAllocator CreateAllocator(Component host, IProject targetProject)
        {
            return new XILAllocator(this, host, targetProject);
        }
    }

    public enum EAllocationDecision
    {
        UseExisting,
        AllocateNew
    }

    public interface IAllocationPolicy
    {
        EAllocationDecision SelectBestMapping(XIL3Instr instr, long cstep, IEnumerable<IXILMapping> mappings, out IXILMapping bestMapping);
        void TellMapping(XIL3Instr instr, long cstep, IXILMapping mapping);
    }

    public interface IAllocationPolicyFactory
    {
        IAllocationPolicy Create();
    }

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

        internal XILAllocator(XILMapperManager xmm, Component host, IProject targetProject)
        {
            _xmm = xmm;
            _host = host;
            _targetProject = targetProject;
            _resTables = new CacheDictionary<ITransactionSite, ReservationTable>(CreateReservationTable);
            Policy = new DefaultAllocationPolicy();
        }

        public IAllocationPolicy Policy { get; set; }

        private ReservationTable CreateReservationTable(ITransactionSite taSite)
        {
            return new ReservationTable();
        }

        public event Action<IXILMapping> OnFUAllocation
        {
            add { _onAllocation += value; }
            remove { _onAllocation -= value; }
        }

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
                            if (!rtbl.IsReserved(cstep, cstep + mapping.InitiationInterval - 1, instr))
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
                var other = obj as ClassifyCookie;
                if (other == null)
                    return false;

                return _instr == other._instr;
            }
        }

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

        public IEnumerable<Tuple<Component, ReservationTable>> Allocation
        {
            get { return _resTables.Select(kvp => Tuple.Create(kvp.Key.Host, kvp.Value)); }
        }

        public AllocationStatistics CreateAllocationStatistics(long scheduleLength)
        {
            return new AllocationStatistics(Allocation, scheduleLength);
        }

        public void Reset()
        {
            _taBindLookup.Clear();
            _resTables.Clear();
        }
    }

    public class AllocationStatistics
    {
        public class FUStatistics
        {
            internal FUStatistics(AllocationStatistics owner, Component fu, ReservationTable reservation)
            {
                Owner = owner;
                FU = fu;
                Reservation = reservation;
            }

            public AllocationStatistics Owner { get; private set; }
            public Component FU { get; private set; }
            public ReservationTable Reservation { get; private set; }

            public long Occupation
            {
                get { return Reservation.GetOccupation(); }
            }

            public double Utilization
            {
                get { return Reservation.GetUtilization(Owner.ScheduleLength); }
            }
        }

        public class FUTypeStatistics
        {
            public Type FUType { get; private set; }
            public IList<FUStatistics> FUs { get; private set; }
            public string FUTypeName
            {
                get { return FUType.Name; }
            }

            internal FUTypeStatistics(Type fuType)
            {
                FUType = fuType;
                FUs = new List<FUStatistics>();
            }

            public double AvgUtilization
            {
                get { return FUs.Average(fu => fu.Utilization); }
            }

        }

        private CacheDictionary<Type, FUTypeStatistics> _stats;

        public AllocationStatistics(IEnumerable<Tuple<Component, ReservationTable>> allocation,
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

        public IEnumerable<Tuple<Component, ReservationTable>> Allocation { get; private set; }
        public long ScheduleLength { get; private set; }

        private void Setup()
        {
            foreach (var tup in Allocation)
            {
                FUTypeStatistics futs = _stats[tup.Item1.GetType()];
                futs.FUs.Add(new FUStatistics(this, tup.Item1, tup.Item2));
            }
        }

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

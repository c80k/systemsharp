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
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.HLS.AllocationPolicies
{
    /// <summary>
    /// Factory-pattern interface for the context-sensitive allocation policy
    /// </summary>
    public interface IContextSensitiveAllocationPolicyFactory :
        IAllocationPolicyFactory
    {
        /// <summary>
        /// Configures the maximum admissible cost of resource sharing is terms of a heuristic measure. That measure is
        /// derived from the multiplexer complexity which arises from sharing a functional unit.
        /// </summary>
        double MaxCost { get; set; }

        /// <summary>
        /// The returned dictionary configures the maximum admissible amount of functional units per component class
        /// (i.e. class type of functional unit is key, value is maximum amount). If a particular functional unit type
        /// is not present as key, an infinite number of admissible instances is assumed.
        /// </summary>
        Dictionary<object, int> FULimits { get; }
    }

    /// <summary>
    /// The context-sensitive allocation policy bases the allocation/sharing decision on a heuristic measure which
    /// estimates the expected multiplexer complexity, arising from sharing a particular function unit. It first selects
    /// the functional unit which causes the least complexity. If that complexity exceeds a user-configured maximum cost,
    /// a new functional unit is allocated.
    /// </summary>
    public class ContextSensitiveAllocation: IAllocationPolicy
    {
        private class FactoryImpl : IContextSensitiveAllocationPolicyFactory
        {
            public FactoryImpl()
            {
                MaxCost = 2.0;
                FULimits = new Dictionary<object, int>();
            }

            public double MaxCost { get; set; }
            public Dictionary<object, int> FULimits { get; private set; }

            public IAllocationPolicy Create()
            {
                return new ContextSensitiveAllocation()
                {
                    MaxCost = MaxCost,
                    FULimits = FULimits
                };
            }
        }

        private class Interlink : 
            ISignalSource<StdLogicVector>,
            ISignalSink<StdLogicVector>,
            ICombSignalSink<StdLogicVector>
        {
            private class SymProcess : IProcess
            {
                private SignalRef _dest;
                private Expression _expr;

                public SymProcess(SignalRef dest, Expression expr)
                {
                    _dest = dest;
                    _expr = expr;
                }

                public Action Operation
                {
                    get { throw new NotImplementedException(); }
                }

                public IEnumerable<ISignal> DrivenSignals
                {
                    get { throw new NotImplementedException(); }
                }

                public IEnumerable<AbstractEvent> Sensitivity
                {
                    get { throw new NotImplementedException(); }
                }

                public void Implement(IAlgorithmBuilder builder)
                {
                    builder.Store(_dest, _expr);
                }
            }

            private int _index;
            private SignalBuilder _desc;
            private SignalRef _srSrc;
            private SignalRef _srSnk;

            public Interlink(int index)
            {
                _index = index;
                _desc = new SignalBuilder(TypeDescriptor.GetTypeOf(StdLogicVector.Empty), StdLogicVector.Empty);
                _desc.AddAttribute(_index);
                _srSrc = SignalRef.Create(_desc, SignalRef.EReferencedProperty.Cur);
                _srSnk = SignalRef.Create(_desc, SignalRef.EReferencedProperty.Next);
            }

            public Func<StdLogicVector> Operation
            {
                get { throw new NotImplementedException(); }
            }

            Func<object> ISignalSource.Operation
            {
                get { throw new NotImplementedException(); }
            }

            public IEnumerable<AbstractEvent> Sensitivity
            {
                get { throw new NotImplementedException(); }
            }

            public SysDOM.Expression GetExpression()
            {
                return _srSrc;
            }

            public object GetSample()
            {
                throw new NotImplementedException();
            }

            public ICombSignalSink<StdLogicVector> Comb
            {
                get { return this; }
            }

            public ISyncSignalSink<StdLogicVector> Sync
            {
                get { return null; }
            }

            public IProcess Connect(ISignalSource<StdLogicVector> source)
            {
                return new SymProcess(_srSnk, source.GetExpression());
            }

            public ISignalOrPortDescriptor Driver { get; set; }
            public long DriveTime { get; set; }
        }

        /// <summary>
        /// Creates a new factory for constructing instances of this class.
        /// </summary>
        public static IContextSensitiveAllocationPolicyFactory CreateFactory()
        {
            return new FactoryImpl();
        }

        private Dictionary<int, IXILMapping> _rslot2mapping = new Dictionary<int, IXILMapping>();
        private Dictionary<int, Interlink> _interlinks = new Dictionary<int,Interlink>();
        private Dictionary<ISignalOrPortDescriptor, Dictionary<ISignalOrPortDescriptor, int>> _fanIn =
            new Dictionary<ISignalOrPortDescriptor,Dictionary<ISignalOrPortDescriptor,int>>();
        private Dictionary<object, HashSet<object>> _fuCount = new Dictionary<object, HashSet<object>>();

        private ContextSensitiveAllocation()
        {
        }

        /// <summary>
        /// Configures the maximum admissible cost of resource sharing is terms of a heuristic measure. That measure is
        /// derived from the multiplexer complexity which arises from sharing a functional unit.
        /// </summary>
        public double MaxCost { get; set; }

        /// <summary>
        /// The returned dictionary configures the maximum admissible amount of functional units per component class
        /// (i.e. class type of functional unit is key, value is maximum amount). If a particular functional unit type
        /// is not present as key, an infinite number of admissible instances is assumed.
        /// </summary>
        public Dictionary<object, int> FULimits { get; private set; }

        public EAllocationDecision SelectBestMapping(XIL3Instr instr, long cstep, IEnumerable<IXILMapping> mappings, out IXILMapping bestMapping)
        {
            foreach (int rslot in instr.ResultSlots)
            {
                _interlinks[rslot] = new Interlink(rslot);
            }

            bestMapping = null;
            double bestCost = double.MaxValue;

            foreach (var mapping in mappings)
            {
                var sources = instr.OperandSlots.Select(os => _interlinks[os]).ToArray();
                var sinks = instr.ResultSlots.Select(rs => _interlinks[rs]).ToArray();

                var verbs = mapping.Realize(sources, sinks);
                long curStep = cstep;
                double cost = 0.0;
                foreach (var verb in verbs)
                {
                    var pflow = verb.ToCombFlow();
                    foreach (var flow in pflow.Flows)
                    {
                        var sflow = flow as SignalFlow;
                        if (sflow == null)
                            continue;

                        if (sflow.Source.Desc.HasAttribute<int>() &&
                            !sflow.Target.Desc.HasAttribute<int>())
                        {
                            int index = sflow.Source.Desc.QueryAttribute<int>();
                            var driver = _interlinks[index].Driver;
                            if (driver == null)
                                continue;

                            var fanIn = _fanIn[sflow.Target.Desc];
                            foreach (int v in fanIn.Values)
                            {
                                cost += Math.Pow(2.0, -v);
                            }
                            int prevSlack;
                            if (fanIn.TryGetValue(driver, out prevSlack))
                            {
                                cost -= Math.Pow(2.0, -prevSlack);                                
                            }
                            int slack = (int)(curStep - _interlinks[index].DriveTime);
                            cost += Math.Pow(2.0, -slack);
                        }
                    }

                    curStep++;
                }

                cost /= instr.OperandSlots.Length;

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestMapping = mapping;
                }
            }

            object iclass = mappings.First().TASite.Host.GetType();
            var fuSet = _fuCount[iclass];
            int curCount = fuSet.Count;

            int limit;
            if (!FULimits.TryGetValue(iclass, out limit))
                limit = int.MaxValue;

            if (curCount < limit &&
                bestCost > MaxCost)
            {
                return EAllocationDecision.AllocateNew;
            }
            else
            {
                return EAllocationDecision.UseExisting;
            }
        }

        public void TellMapping(XIL3Instr instr, long cstep, IXILMapping mapping)
        {
            foreach (int rslot in instr.ResultSlots)
            {
                if (!_interlinks.ContainsKey(rslot))
                    _interlinks[rslot] = new Interlink(rslot);
            }

            var sources = instr.OperandSlots.Select(os => _interlinks[os]).ToArray();
            var sinks = instr.ResultSlots.Select(rs => _interlinks[rs]).ToArray();

            var verbs = mapping.Realize(sources, sinks);
            long curStep = cstep;
            foreach (var verb in verbs)
            {
                var pflow = verb.ToCombFlow();
                foreach (var flow in pflow.Flows)
                {
                    var sflow = flow as SignalFlow;
                    if (sflow == null)
                        continue;

                    if (sflow.Target.Desc.HasAttribute<int>())
                    {
                        int index = sflow.Target.Desc.QueryAttribute<int>();
                        _interlinks[index].Driver = sflow.Source.Desc;
                        _interlinks[index].DriveTime = curStep;
                    }
                    else if (sflow.Source.Desc.HasAttribute<int>())
                    {
                        Dictionary<ISignalOrPortDescriptor, int> fanIn;
                        if (!_fanIn.TryGetValue(sflow.Target.Desc, out fanIn))
                        {
                            fanIn = new Dictionary<ISignalOrPortDescriptor, int>();
                            _fanIn[sflow.Target.Desc] = fanIn;
                        }

                        int index = sflow.Source.Desc.QueryAttribute<int>();
                        var driver = _interlinks[index].Driver;
                        if (driver == null)
                            continue;
                        int slack = (int)(curStep - _interlinks[index].DriveTime);
                        int prevSlack;
                        if (!fanIn.TryGetValue(driver, out prevSlack) ||
                            prevSlack > slack)
                        {
                            fanIn[driver] = slack;
                        }
                    }
                }
            }

            object iclass = mapping.TASite.Host.GetType();
            HashSet<object> fuSet;
            if (!_fuCount.TryGetValue(iclass, out fuSet))
            {
                fuSet = new HashSet<object>();
                _fuCount[iclass] = fuSet;
            }
            fuSet.Add(mapping.TASite);
        }
    }
}

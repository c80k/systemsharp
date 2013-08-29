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
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.DesignGen
{
    public class TristateBusFSMControlpathBuilder : IControlpathBuilder
    {
        private class FactoryImpl : IControlpathBuilderFactory
        {
            public IControlpathBuilder Create(Component host, IAutoBinder binder)
            {
                return new TristateBusFSMControlpathBuilder(host, binder);
            }
        }

        private class TristateConcTemplate : AlgorithmTemplate
        {
            private class StateDependentFlow
            {
                public Flow WhenFlow { get; set; }
                public Flow ElseFlow { get; set; }
                public List<object> EnablingStates { get; private set; }

                public StateDependentFlow()
                {
                    EnablingStates = new List<object>();
                }
            }

            private TristateBusFSMControlpathBuilder _cpb;
            private FlowMatrix _flowSpec;
            private Dictionary<SignalRef, Flow> _elseFlows;
            private Dictionary<Flow, List<object>> _enablingStatesMap;

            public List<SignalRef> NonTristateTargets { get; private set; }

            public TristateConcTemplate(TristateBusFSMControlpathBuilder cpb, FlowMatrix flowSpec)
            {
                _cpb = cpb;
                _flowSpec = flowSpec;
                NonTristateTargets = new List<SignalRef>();
            }

            private void BuildFlowMap()
            {
                _elseFlows = new Dictionary<SignalRef, Flow>();
                _enablingStatesMap = new Dictionary<Flow, List<object>>();

                foreach (Flow flow in _flowSpec.NeutralFlow.Flows)
                {
                    if (FlowMatrix.IsDontCareFlow(flow))
                    {
                        var zflow = FlowMatrix.AsDontCareFlow((ValueFlow)flow, StdLogic.Z);
                        _elseFlows[flow.Target] = zflow;
                    }
                    else
                    {
                        NonTristateTargets.Add(flow.Target);
                    }
                }

                Array stateValues = _cpb._stateSignal.Descriptor.ElementType.CILType.GetEnumValues();

                for (int cstep = 0; cstep < stateValues.Length; cstep++)
                {
                    var state = stateValues.GetValue(cstep);
                    var pflow = _flowSpec.GetFlow(cstep);
                    foreach (var flow in pflow.Flows)
                    {
                        if (!_enablingStatesMap.ContainsKey(flow))
                            _enablingStatesMap[flow] = new List<object>();
                        _enablingStatesMap[flow].Add(state);
                    }
                }
            }

            protected override void DeclareAlgorithm()
            {
                BuildFlowMap();

                var grouped = _enablingStatesMap.GroupBy(kvp => kvp.Key.Target);

                SignalRef curStateRef = SignalRef.Create(_cpb._stateSignal, SignalRef.EReferencedProperty.Cur);
                LiteralReference lrCurState = new LiteralReference(curStateRef);
                Array stateValues = _cpb._stateSignal.Descriptor.ElementType.CILType.GetEnumValues();

                foreach (var group in grouped)
                {
                    if (group.Count() > 1)
                    {
                        var target = group.Key;
                        Flow elseFlow;
                        if (!_elseFlows.TryGetValue(target, out elseFlow))
                            continue;
                        foreach (var kvp in group)
                        {
                            var whenFlow = kvp.Key;
                            var states = kvp.Value;
                            var cond = Expression.Equal(lrCurState, LiteralReference.CreateConstant(states[0]));
                            for (int i = 1; i < states.Count; i++)
                                cond = cond | Expression.Equal(lrCurState, LiteralReference.CreateConstant(states[i]));
                            var conditional = Expression.Conditional(
                                cond,
                                whenFlow.GetRHS(),
                                elseFlow.GetRHS());
                            Store(target, conditional);
                        }
                    }
                    else
                    {
                        var onlyFlow = group.Single().Key;
                        if (!_elseFlows.ContainsKey(onlyFlow.Target))
                            continue;
                        Store(onlyFlow.Target, onlyFlow.GetRHS());
                    }
                }
            }

            protected override string FunctionName
            {
                get { return "TristateConc"; }
            }
        }

        private class FUDriveTemplate : AlgorithmTemplate
        {
            private TristateBusFSMControlpathBuilder _cpb;
            private FlowMatrix _flowSpec;
            private HashSet<SignalRef> _nonTristateTargets;

            public FUDriveTemplate(
                TristateBusFSMControlpathBuilder cpb, 
                FlowMatrix flowSpec,
                IEnumerable<SignalRef> nonTristateTargets)
            {
                _cpb = cpb;
                _flowSpec = flowSpec;
                _nonTristateTargets = new HashSet<SignalRef>(nonTristateTargets);
            }

            private void ImplementVerb(TAVerb verb)
            {
                if (verb.During != null)
                {
                    verb.During.Implement(this);
                }
            }

            private void ImplementFlow(Flow flow)
            {
                flow.ToProcess().Implement(this);
            }

            protected override void DeclareAlgorithm()
            {
                SignalRef curStateRef = SignalRef.Create(_cpb._stateSignal, SignalRef.EReferencedProperty.Cur);
                LiteralReference lrCurState = new LiteralReference(curStateRef);
                Array stateValues = _cpb._stateSignal.Descriptor.ElementType.CILType.GetEnumValues();

                // Insert neutral pre-sets
                var npflow = new ParFlow();
                foreach (var flow in _flowSpec.NeutralFlow.Flows)
                {
                    if (_nonTristateTargets.Contains(flow.Target))
                        npflow.Add(flow);
                }
                npflow.ToProcess().Implement(this);

                // State-dependent MUX
                Switch(lrCurState);
                {
                    for (int cstep = 0; cstep < stateValues.Length; cstep++)
                    {
                        Case(LiteralReference.CreateConstant(stateValues.GetValue(cstep)));
                        {
                            Comment(_flowSpec.GetComment(cstep));
                            var pflow = new ParFlow();
                            foreach (var flow in _flowSpec.GetFlow(cstep).Flows)
                            {
                                if (_nonTristateTargets.Contains(flow.Target))
                                    pflow.Add(flow);
                            }
                            pflow.ToProcess().Implement(this);
                        }
                        EndCase();
                    }
                    DefaultCase();
                    {
                        npflow.ToProcess().Implement(this);
                    }
                    EndCase();
                }
                EndSwitch();
            }

            protected override string FunctionName
            {
                get { return "CombFSM"; }
            }
        }

        private Component _host;
        private IAutoBinder _binder;
        private InlineBCUMapper _bcuMapper;
        private SignalBase _stateSignal;
        private Array _stateValues;

        private TristateBusFSMControlpathBuilder(Component host, IAutoBinder binder)
        {
            _host = host;
            _binder = binder;
        }

        public void PersonalizePlan(HLSPlan plan)
        {
            _bcuMapper = new InlineBCUMapper();
            plan.AddXILMapper(_bcuMapper);
        }

        public void PrepareAllocation(long cstepCount)
        {
            string[] stateNames = new string[cstepCount];
            for (int i = 0; i < cstepCount; i++)
                stateNames[i] = "CStep" + i;
            var design = _host.Descriptor.GetDesign();
            string typeName = "TState" + cstepCount;
            var tstate = design.GetTypes()
                .Where(t => t.Name == typeName)
                .FirstOrDefault();
            if (tstate == null)
                tstate = design.CreateEnum(typeName, stateNames);
            _stateValues = tstate.CILType.GetEnumValues();
            object defaultState = Activator.CreateInstance(tstate.CILType);
            _stateSignal = _binder.GetSignal(EPortUsage.State, "State", null, defaultState);
        }

        public void CreateControlpath(FlowMatrix flowSpec, string procName)
        {
            var sens = flowSpec.FlowSources.Select(sr => sr.Desc)
                .Concat(Enumerable.Repeat((ISignalOrPortDescriptor)_stateSignal.Descriptor, 1))
                .Distinct();
            var tscTempl = new TristateConcTemplate(this, flowSpec);
            var tscFunc = tscTempl.GetAlgorithm();
            _binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, tscFunc, sens.ToArray());
            var fudTempl = new FUDriveTemplate(this, flowSpec, tscTempl.NonTristateTargets);
            var fudFunc = fudTempl.GetAlgorithm();
            _binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, fudFunc, sens.ToArray());
        }

        public static readonly IControlpathBuilderFactory Factory = new FactoryImpl();
    }
}

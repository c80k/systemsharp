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
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.DesignGen
{
    /// <summary>
    /// A control path builder for classic finite state machine (FSM) implementations.
    /// </summary>
    public class FSMControlpathBuilder: IControlpathBuilder
    {
        private class FactoryImpl : IControlpathBuilderFactory
        {
            public IControlpathBuilder Create(Component host, IAutoBinder binder)
            {
                return new FSMControlpathBuilder(host, binder);
            }
        }

        private class FUDriveTemplate : AlgorithmTemplate
        {
            private FSMControlpathBuilder _cpb;
            private FlowMatrix _flowSpec;

            public FUDriveTemplate(FSMControlpathBuilder cpb, FlowMatrix flowSpec)
            {
                _cpb = cpb;
                _flowSpec = flowSpec;
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
                _flowSpec.NeutralFlow.ToProcess().Implement(this);

                // State-dependent MUX
                Switch(lrCurState);
                {
                    for (int cstep = 0; cstep < stateValues.Length; cstep++)
                    {
                        Case(LiteralReference.CreateConstant(stateValues.GetValue(cstep)));
                        {
                            Comment(_flowSpec.GetComment(cstep));
                            _flowSpec.GetFlow(cstep).ToProcess().Implement(this);
                        }
                        EndCase();
                    }
                    DefaultCase();
                    {
                        _flowSpec.NeutralFlow.ToProcess().Implement(this);
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

        private FSMControlpathBuilder(Component host, IAutoBinder binder)
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
            flowSpec.ReplaceDontCares();
            var sens = flowSpec.FlowSources.Select(sr => sr.Desc)
                .Concat(Enumerable.Repeat((ISignalOrPortDescriptor)_stateSignal.Descriptor, 1))
                .Distinct();
            var fudTempl = new FUDriveTemplate(this, flowSpec);
            var fudFunc = fudTempl.GetAlgorithm();
            _binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, fudFunc, sens.ToArray());
            _host.Descriptor.GetDocumentation().Documents.Add(new Document(procName + "_FSM_report.txt", flowSpec.GetMUXReport()));
        }

        /// <summary>
        /// Returns the factory for constructing instances of this class.
        /// </summary>
        public static readonly IControlpathBuilderFactory Factory = new FactoryImpl();
    }
}

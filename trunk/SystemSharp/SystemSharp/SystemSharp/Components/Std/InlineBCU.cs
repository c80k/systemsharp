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
using SystemSharp.Assembler;
using SystemSharp.Collections;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// A service for mapping XIL branching instructions (goto, brtrue, brfalse) to an inline branch control unit (BCU).
    /// An inline BCU does not require a separate component instance. Instead, the necessary control logic is directly
    /// inserted into the hosting component.
    /// </summary>
    public class InlineBCUMapper: IXILMapper
    {
        private class InlineBCUTransactionSite : DefaultTransactionSite
        {
            private class IncStateProcessBuilder : AlgorithmTemplate
            {
                private InlineBCUTransactionSite _taSite;

                public IncStateProcessBuilder(InlineBCUTransactionSite taSite)
                {
                    _taSite = taSite;
                }

                protected override void DeclareAlgorithm()
                {
                    SignalRef curState = SignalRef.Create(_taSite._curState, SignalRef.EReferencedProperty.Cur);
                    SignalRef incState = SignalRef.Create(_taSite._incState, SignalRef.EReferencedProperty.Next);
                    LiteralReference lrCurState = new LiteralReference(curState);
                    Array stateValues = _taSite._tState.GetEnumValues();
                    Switch(lrCurState);
                    {
                        for (int i = 0; i < stateValues.Length; i++)
                        {
                            object curValue = stateValues.GetValue(i);
                            object incValue = stateValues.GetValue((i + 1) % stateValues.Length);
                            Case(LiteralReference.CreateConstant(curValue));
                            {
                                Store(incState, LiteralReference.CreateConstant(incValue));
                            }
                            EndCase();
                        }
                    }
                    EndSwitch();
                }
            }

            private class SyncProcessBuilder : AlgorithmTemplate
            {
                private InlineBCUTransactionSite _taSite;
                private IAutoBinder _binder;

                public SyncProcessBuilder(InlineBCUTransactionSite taSite, IAutoBinder binder)
                {
                    _taSite = taSite;
                    _binder = binder;
                }

                protected override void DeclareAlgorithm()
                {
                    Signal<StdLogic> clkInst = _binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                    SignalRef clkRising = SignalRef.Create(clkInst.Descriptor, SignalRef.EReferencedProperty.RisingEdge);
                    LiteralReference lrClkRising = new LiteralReference(clkRising);
                    SignalRef altFlagP = SignalRef.Create(_taSite._brAltFlagP, SignalRef.EReferencedProperty.Cur);
                    LiteralReference lrAltFlagP = new LiteralReference(altFlagP);
                    SignalRef altFlagN = SignalRef.Create(_taSite._brAltFlagN, SignalRef.EReferencedProperty.Cur);
                    LiteralReference lrAltFlagN = new LiteralReference(altFlagN);
                    SignalRef curState = SignalRef.Create(_taSite._curState, SignalRef.EReferencedProperty.Next);
                    SignalRef incState = SignalRef.Create(_taSite._incState, SignalRef.EReferencedProperty.Cur);
                    LiteralReference lrIncState = new LiteralReference(incState);
                    SignalRef altState = SignalRef.Create(_taSite._altState, SignalRef.EReferencedProperty.Cur);
                    LiteralReference lrAltState = new LiteralReference(altState);
                    LiteralReference vcc = LiteralReference.CreateConstant((StdLogicVector)"1");
                    LiteralReference gnd = LiteralReference.CreateConstant((StdLogicVector)"0");
                    If(lrClkRising);
                    {
                        If(Expression.Equal(lrAltFlagP, vcc) | Expression.Equal(lrAltFlagN, gnd));
                        {
                            Store(curState, altState);
                        }
                        Else();
                        {
                            Store(curState, incState);
                        }
                        EndIf();
                    }
                    EndIf();
                }
            }

            private InlineBCUMapper _mapper;
            private SLVSignal _brAltFlagP;
            private SLVSignal _brAltFlagN;
            private bool _allocated;
            private Type _tState;
            private SignalDescriptor _curState;
            private SignalDescriptor _altState;
            private SignalDescriptor _incState;
            private SignalDescriptor _nextState;

            public InlineBCUTransactionSite(InlineBCUMapper mapper, Component host) :
                base(host)
            {
                _mapper = mapper;
            }

            private IProcess GetStateDriver(BranchLabel target)
            {
                Array enumValues = _tState.GetEnumValues();
                Debug.Assert(target.CStep >= 0 && target.CStep < enumValues.Length);
                object stateValue = enumValues.GetValue(target.CStep);
                return _altState.Instance.DriveUT(SignalSource.CreateUT(stateValue));
            }

            private IProcess GetNilDriver()
            {
                Array enumValues = _tState.GetEnumValues();
                object defaultState = Activator.CreateInstance(_tState);
                return _altState.Instance.DriveUT(SignalSource.CreateUT(defaultState));
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(ETVMode.Locked, 
                        _brAltFlagP.Drive(SignalSource.Create<StdLogicVector>("0")),
                        _brAltFlagN.Drive(SignalSource.Create<StdLogicVector>("1")),
                        GetNilDriver());
            }

            public IEnumerable<TAVerb> Branch(BranchLabel target)
            {
                yield return Verb(ETVMode.Locked, 
                        _brAltFlagP.Drive(SignalSource.Create<StdLogicVector>("1")),
                        _brAltFlagN.Drive(SignalSource.Create<StdLogicVector>("0")),
                        GetStateDriver(target));
            }

            public IEnumerable<TAVerb> BranchIf(ISignalSource<StdLogicVector> cond, BranchLabel target)
            {
                yield return Verb(ETVMode.Locked, 
                        _brAltFlagP.Drive(cond),
                        _brAltFlagN.Drive(SignalSource.Create<StdLogicVector>("1")),
                        GetStateDriver(target));
            }

            public IEnumerable<TAVerb> BranchIfNot(ISignalSource<StdLogicVector> cond, BranchLabel target)
            {
                yield return Verb(ETVMode.Locked, 
                        _brAltFlagP.Drive(SignalSource.Create<StdLogicVector>("0")),
                        _brAltFlagN.Drive(cond),
                        GetStateDriver(target));
            }

            public override void Establish(IAutoBinder binder)
            {
                if (_allocated)
                    return;

                _brAltFlagP = (SLVSignal)binder.GetSignal(EPortUsage.Default, "BCU_BrP", null, StdLogicVector._0s(1));
                _brAltFlagN = (SLVSignal)binder.GetSignal(EPortUsage.Default, "BCU_BrN", null, StdLogicVector._1s(1));

                _curState = binder.GetSignal(EPortUsage.State, "BCU_CurState", null, null).Descriptor;
                _tState = _curState.ElementType.CILType;
                Array enumValues = _tState.GetEnumValues();
                int numStates = enumValues.Length;
                object defaultState = Activator.CreateInstance(_tState);
                _incState = binder.GetSignal(EPortUsage.Default, "BCU_IncState", null, defaultState).Descriptor;
                _altState = binder.GetSignal(EPortUsage.Default, "BCU_AltState", null, defaultState).Descriptor;
                _nextState = binder.GetSignal(EPortUsage.Default, "BCU_NextState", null, defaultState).Descriptor;
                IncStateProcessBuilder ispb = new IncStateProcessBuilder(this);
                Function incStateFn = ispb.GetAlgorithm();
                incStateFn.Name = "BCU_IncState";
                binder.CreateProcess(Process.EProcessKind.Triggered, incStateFn, _curState);
                SyncProcessBuilder spb = new SyncProcessBuilder(this, binder);
                Function syncStateFn = spb.GetAlgorithm();
                syncStateFn.Name = "BCU_FSM";
                ISignalOrPortDescriptor sdClock = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0').Descriptor;
                binder.CreateProcess(Process.EProcessKind.Triggered, syncStateFn, sdClock);

                _allocated = true;
            }
        }

        private class InlineBCUMapping : IXILMapping
        {
            private InlineBCUTransactionSite _taSite;
            private string _instrName;
            private BranchLabel _target;

            public InlineBCUMapping(InlineBCUTransactionSite taSite, string instrName, BranchLabel target)
            {
                _instrName = instrName;
                _target = target;
                _taSite = taSite;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                switch (_instrName)
                {
                    case InstructionCodes.Goto:
                        return _taSite.Branch(_target);

                    case InstructionCodes.BranchIfTrue:
                        return _taSite.BranchIf(operands[0], _target);

                    case InstructionCodes.BranchIfFalse:
                        return _taSite.BranchIfNot(operands[0], _target);

                    default:
                        throw new NotImplementedException();
                }
            }

            public ITransactionSite TASite
            {
                get { return _taSite; }
            }

            public EMappingKind ResourceKind
            {
                get { return EMappingKind.ExclusiveResource; }
            }

            public int InitiationInterval
            {
                get { return 1; }
            }

            public int Latency
            {
                get { return 1; }
            }

            public string Description
            {
                get
                {
                    switch (_instrName)
                    {
                        case InstructionCodes.Goto:
                            return "branch control: goto";

                        case InstructionCodes.BranchIfTrue:
                            return "branch control: conditional branch (positive)";

                        case InstructionCodes.BranchIfFalse:
                            return "branch control: conditional branch (negative)";

                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public InlineBCUMapper()
        {
        }

        #region IXILMapper Member

        /// <summary>
        /// Returns goto, brtrue, brfalse
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Goto(null);
            yield return DefaultInstructionSet.Instance.BranchIfTrue(null);
            yield return DefaultInstructionSet.Instance.BranchIfFalse(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            //if (fu != _host)
            //    yield break;
            var taBM = taSite as InlineBCUTransactionSite;
            if (taBM == null)
                yield break;

            switch (instr.Name)
            {
                case InstructionCodes.Goto:
                    yield return new InlineBCUMapping(taBM, InstructionCodes.Goto, (BranchLabel)instr.Operand);
                    break;

                case InstructionCodes.BranchIfTrue:
                    yield return new InlineBCUMapping(taBM, InstructionCodes.BranchIfTrue, (BranchLabel)instr.Operand);
                    break;

                case InstructionCodes.BranchIfFalse:
                    yield return new InlineBCUMapping(taBM, InstructionCodes.BranchIfFalse, (BranchLabel)instr.Operand);
                    break;

                default:
                    yield break;
            }
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            switch (instr.Name)
            {
                case InstructionCodes.Goto:
                case InstructionCodes.BranchIfTrue:
                case InstructionCodes.BranchIfFalse:
                    return TryMap(new InlineBCUTransactionSite(this, host), instr, operandTypes, resultTypes).SingleOrDefault();

                default:
                    return null;
            }
        }

        #endregion
    }
}

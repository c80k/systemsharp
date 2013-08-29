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
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    public interface IBCUTransactionSite: ITransactionSite
    {
        IEnumerable<TAVerb> Branch(BranchLabel target);
        IEnumerable<TAVerb> BranchIf(ISignalSource<StdLogicVector> cond, BranchLabel target);
        IEnumerable<TAVerb> BranchIfNot(ISignalSource<StdLogicVector> cond, BranchLabel target);
    }

    public class BCUMapper : IXILMapper
    {
        private abstract class BCUMapping : IXILMapping
        {
            protected BCU _bcu;
            protected BranchLabel _target;

            public BCUMapping(BCU bcu, BranchLabel target)
            {
                _bcu = bcu;
                _target = target;
            }

            public abstract IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results);
            public abstract string Description { get; }

            public ITransactionSite TASite
            {
                get { return _bcu.TASite; }
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
                get { return _bcu.Latency; }
            }
        }

        private class GotoMapping : BCUMapping
        {
            public GotoMapping(BCU bcu, BranchLabel target):
                base(bcu, target)
            {
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _bcu.TASite.Branch(_target);
            }

            public override string Description
            {
                get { return "BCU: goto"; }
            }
        }

        private class BranchIfMapping: BCUMapping
        {
            public BranchIfMapping(BCU bcu, BranchLabel target) :
                base(bcu, target)
            {
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _bcu.TASite.BranchIf(operands[0], _target);
            }

            public override string Description
            {
                get { return "BCU: conditional branch (positive)"; }
            }
        }

        private class BranchIfNotMapping : BCUMapping
        {
            public BranchIfNotMapping(BCU bcu, BranchLabel target):
                base(bcu, target)
            {
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _bcu.TASite.BranchIfNot(operands[0], _target);
            }

            public override string Description
            {
                get { return "BCU: conditional branch (negative)"; }
            }
        }

        private BCU _host;
        private int _latency;

        public BCUMapper(BCU host, int latency = 1)
        {
            _host = host;
            _latency = latency;
        }

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Goto(null);
            yield return DefaultInstructionSet.Instance.BranchIfTrue(null);
            yield return DefaultInstructionSet.Instance.BranchIfFalse(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            BCU bcu = fu as BCU;
            if (bcu != _host)
                yield break;

            switch (instr.Name)
            {
                case InstructionCodes.Goto:
                case InstructionCodes.BranchIfTrue:
                case InstructionCodes.BranchIfFalse:
                    {
                        var target = (BranchLabel)instr.Operand;
                        switch (instr.Name)
                        {
                            case InstructionCodes.Goto:
                                yield return new GotoMapping(bcu, target);
                                yield break;

                            case InstructionCodes.BranchIfTrue:
                                yield return new BranchIfMapping(bcu, target);
                                yield break;

                            case InstructionCodes.BranchIfFalse:
                                yield return new BranchIfNotMapping(bcu, target);
                                yield break;

                            default:
                                throw new NotImplementedException();
                        }
                    }

                default:
                    yield break;
            }
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            switch (instr.Name)
            {
                case InstructionCodes.Goto:
                case InstructionCodes.BranchIfTrue:
                case InstructionCodes.BranchIfFalse:
                    return TryMap(_host.TASite, instr, operandTypes, resultTypes).Single();

                default:
                    return null;
            }
        }
    }

    public class BCU: Component
    {
        private class BCUTransactionSite : 
            DefaultTransactionSite,
            IBCUTransactionSite
        {
            private BCU _host;

            public BCUTransactionSite(BCU host) :
                base(host)
            {
                _host = host;
            }

            private TAVerb NopVerb()
            {
                return Verb(ETVMode.Locked,
                        _host.BrP.Dual.Drive(SignalSource.Create<StdLogicVector>("0")),
                        _host.BrN.Dual.Drive(SignalSource.Create<StdLogicVector>("1")),
                        _host.AltAddr.Dual.Drive(SignalSource.Create(StdLogicVector._0s(_host.AddrWidth))));
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return NopVerb();
            }

            public IEnumerable<TAVerb> Branch(BranchLabel target)
            {
                yield return Verb(ETVMode.Locked,
                    _host.BrP.Dual.Drive(SignalSource.Create<StdLogicVector>("1")),
                    _host.BrN.Dual.Drive(SignalSource.Create<StdLogicVector>("0")),
                    _host.AltAddr.Dual.Drive(
                        SignalSource.Create(
                            StdLogicVector.FromUInt(
                                (uint)target.CStep, _host.AddrWidth))));
                for (int i = 1; i < _host.Latency; i++)
                    yield return NopVerb();
            }

            public IEnumerable<TAVerb> BranchIf(ISignalSource<StdLogicVector> cond, BranchLabel target)
            {
                yield return Verb(ETVMode.Locked,
                    _host.BrP.Dual.Drive(cond),
                    _host.BrN.Dual.Drive(SignalSource.Create<StdLogicVector>("1")),
                    _host.AltAddr.Dual.Drive(
                        SignalSource.Create(
                            StdLogicVector.FromUInt(
                                (uint)target.CStep, _host.AddrWidth))));
                for (int i = 1; i < _host.Latency; i++)
                    yield return NopVerb();
            }

            public IEnumerable<TAVerb> BranchIfNot(ISignalSource<StdLogicVector> cond, BranchLabel target)
            {
                yield return Verb(ETVMode.Locked,
                    _host.BrP.Dual.Drive(SignalSource.Create<StdLogicVector>("0")),
                    _host.BrN.Dual.Drive(cond),
                    _host.AltAddr.Dual.Drive(
                        SignalSource.Create(
                            StdLogicVector.FromUInt(
                                (uint)target.CStep, _host.AddrWidth))));
                for (int i = 1; i < _host.Latency; i++)
                    yield return NopVerb();
            }
        }

        public In<StdLogic> Clk { private get; set; }
        public In<StdLogic> Rst { private get; set; }
        public In<StdLogicVector> BrP { internal get; set; }
        public In<StdLogicVector> BrN { internal get; set; }
        public In<StdLogicVector> AltAddr { internal get; set; }
        public Out<StdLogicVector> OutAddr { internal get; set; }

        [PerformanceRelevant]
        public int AddrWidth 
        { 
            [StaticEvaluation] get; [AssumeNotCalled] set; 
        }

        public StdLogicVector StartupAddr 
        { 
            [StaticEvaluation] get; [AssumeNotCalled] set; 
        }

        [PerformanceRelevant]
        public int Latency
        {
            [StaticEvaluation] get;
            private set;
        }

        public IBCUTransactionSite TASite { get; private set; }

        private SLVSignal _lastAddr;
        private SLVSignal _outAddr;
        private SLVSignal _rstq;
        private StdLogicVector _rstPat;

        public BCU(int latency = 1)
        {
            TASite = new BCUTransactionSite(this);
            Latency = latency;
        }

        private void ComputeOutAddr()
        {
            if (Rst.Cur == '1')
            {
                _outAddr.Next = StartupAddr;
            }
            else if (BrP.Cur == "1" || BrN.Cur == "0")
            {
                _outAddr.Next = AltAddr.Cur;
            }
            else
            {
                _outAddr.Next =
                    (_lastAddr.Cur.UnsignedValue + 
                    Unsigned.FromUInt(1, AddrWidth))
                    .Resize(AddrWidth).SLVValue;
            }
        }

        private void ComputeOutAddrWithRstQ()
        {
            if (Rst.Cur == '1')
            {
                _outAddr.Next = StartupAddr;
            }
            else if (_rstq.Cur[0] == '1' || (BrP.Cur != "1" && BrN.Cur != "0"))
            {
                _outAddr.Next =
                    (_lastAddr.Cur.UnsignedValue +
                    Unsigned.FromUInt(1, AddrWidth))
                    .Resize(AddrWidth).SLVValue;
            }
            else
            {
                _outAddr.Next = AltAddr.Cur;
            }
        }

        private void UpdateAddr()
        {
            if (Clk.RisingEdge())
            {
                _lastAddr.Next = _outAddr.Cur;
            }
        }

        private void SyncResetHandling()
        {
            if (Clk.RisingEdge())
            {
                if (Rst.Cur == '1')
                    _rstq.Next = _rstPat;
                else
                    _rstq.Next = StdLogic._0.Concat(_rstq.Cur[Latency - 2, 1]);
            }
        }

        private void DriveOutAddrComb()
        {
            OutAddr.Next = _outAddr.Cur;
        }

        private void DriveOutAddrDeferred()
        {
            OutAddr.Next = _lastAddr.Cur;
        }

        protected override void PreInitialize()
        {
            if (StartupAddr.Size != AddrWidth)
                throw new InvalidOperationException("BCU: Invalid startup address");

            _lastAddr = new SLVSignal(StdLogicVector._0s(AddrWidth));
            _outAddr = new SLVSignal(AddrWidth);

            if (Latency > 1)
            {
                _rstPat = StdLogicVector._1s(Latency - 1);
                _rstq = new SLVSignal(_rstPat);
            }
        }

        protected override void Initialize()
        {
            AddProcess(UpdateAddr, Clk);
            AddProcess(DriveOutAddrComb, _outAddr);
            if (Latency > 1)
            {
                AddProcess(SyncResetHandling, Clk);
                AddProcess(ComputeOutAddrWithRstQ, Rst, _rstq, BrP, BrN, AltAddr, _lastAddr);
            }
            else
            {
                AddProcess(ComputeOutAddr, Rst, BrP, BrN, AltAddr, _lastAddr);
            }
        }
    }
}

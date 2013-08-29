/**
 * Copyright 2011-2012 Christian Köllner
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
using SystemSharp.Assembler;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Std
{
    public interface ILocalStorageUnitTransactionSite :
        ITransactionSite
    {
        IEnumerable<TAVerb> Read(ISignalSink<StdLogicVector> result);
        IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> data);
    }

    public interface IVariableDataTraceProvider
    {
        object[] QueryTrace(Variable v);
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RegisteredVariable : Attribute
    { 
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class FeedThruVariable : Attribute
    {
    }

    class LocalStorageUnitTransactionSite :
        DefaultTransactionSite,
        ILocalStorageUnitTransactionSite
    {
        private LocalStorageUnit _host;

        public LocalStorageUnitTransactionSite(LocalStorageUnit host):
            base(host)
        {
            _host = host;
        }

        public override IEnumerable<TAVerb> DoNothing()
        {
            yield return Verb(ETVMode.Locked,
                _host.EnIn.Dual.Stick<StdLogic>('0'),
                _host.DIn.Dual.Stick(StdLogicVector.DCs(_host.DataWidth)));
        }

        public override void Establish(IAutoBinder binder)
        {
            var lsu = _host;
            lsu.Clk = binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
            lsu.EnIn = binder.GetSignal<StdLogic>(EPortUsage.Default, _host.MappedVariableName + "_StEn", null, '0');
            lsu.DIn = binder.GetSignal<StdLogicVector>(EPortUsage.Operand, _host.MappedVariableName + "_In", null, StdLogicVector._0s(lsu.DataWidth));
            lsu.DOut = binder.GetSignal<StdLogicVector>(EPortUsage.Result, _host.MappedVariableName + "_Out", null, StdLogicVector._0s(lsu.DataWidth));
        }

        #region ILocalStorageUnitTransactionSite Member

        public IEnumerable<TAVerb> Read(ISignalSink<StdLogicVector> result)
        {
            yield return Verb(ETVMode.Shared,
                result.Comb.Connect(_host.DOut.Dual.AsSignalSource()));
        }

        public IEnumerable<TAVerb> Write(ISignalSource<StdLogicVector> data)
        {
            yield return Verb(ETVMode.Locked,
                _host.EnIn.Dual.Stick<StdLogic>('1'),
                _host.DIn.Dual.Drive(data));
        }

        #endregion
    }

    [DeclareXILMapper(typeof(LocalStorageUnitXILMapper))]
    public class LocalStorageUnit: 
        FunctionalUnit,
        ISupportsDiagnosticOutput
    {
        public In<StdLogic> Clk { get; set; }
        public In<StdLogic> EnIn { get; set; }
        public In<StdLogicVector> DIn { get; set; }
        public Out<StdLogicVector> DOut { get; set; }

        [PerformanceRelevant]
        public int DataWidth { get; private set; }

        public Variable MappedVariable { get; private set; }

        private LocalStorageUnitTransactionSite _taSite;
        public ILocalStorageUnitTransactionSite TASite
        {
            get { return _taSite; }
        }

        public string MappedVariableName
        {
            [StaticEvaluation] get { return MappedVariable.Name; }
        }

        public IVariableDataTraceProvider TraceProvider { get; set; }

        public bool EnableDiagnostics { get; set; }

        private StdLogicVector[] _trace;
        public StdLogicVector[] Trace
        {
            get { return _trace; }
        }

        private bool _directFeed;

        [PerformanceRelevant]
        public bool IsDirectFeed
        {
            get { return _directFeed; }
        }

        public LocalStorageUnit(Variable mappedVariable, bool directFeed)
        {
            MappedVariable = mappedVariable;
            _directFeed = directFeed;
            DataWidth = (int)TypeLowering.Instance.GetWireType(mappedVariable.Type).Constraints[0].Size;
            StdLogicVector initial;
            if (mappedVariable.InitialValue == null)
                initial = StdLogicVector._0s(TypeLowering.Instance.GetWireWidth(mappedVariable.Type));
            else
                initial = Marshal.SerializeForHW(mappedVariable.InitialValue);
            _stg = new SLVSignal(DataWidth)
            {
                InitialValue = initial
            };
            _taSite = new LocalStorageUnitTransactionSite(this);
        }

        public override string DisplayName
        {
            get { return MappedVariable.Name; }
        }

        private SLVSignal _stg;
        private int _tracePos;

        private void DiagMonDouble()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                Console.WriteLine("Writing variable " + MappedVariableName + ": " + DIn.Cur.ToDouble());
            }
        }

        private int _fracWidth;
        private void DiagMonSFix()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                Console.WriteLine("Writing variable " + MappedVariableName + ": " + SFix.FromSigned(DIn.Cur.SignedValue, _fracWidth).DoubleValue);
            }
        }

        private void TraceMon()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                if (_tracePos < _trace.Length)
                {
                    var value = DIn.Cur;
                    Debug.Assert(value == _trace[_tracePos]);
                    _tracePos++;
                }
            }
        }

        private void Clocked()
        {
            if (Clk.RisingEdge() && EnIn.Cur == '1')
            {
                _stg.Next = DIn.Cur;
            }
        }

        private void Comb1()
        {
            DOut.Next = _stg.Cur;
        }

        private void Comb0()
        {
            if (EnIn.Cur == '1')
                DOut.Next = DIn.Cur;
            else
                DOut.Next = _stg.Cur;
        }

        protected override void Initialize()
        {
            AddProcess(Clocked, Clk, EnIn);
            if (_directFeed)
                AddProcess(Comb0, EnIn, DIn, _stg);
            else
                AddProcess(Comb1, _stg);
            if (EnableDiagnostics)
            {
                if (TraceProvider != null)
                {
                    object[] trace = TraceProvider.QueryTrace(MappedVariable);
                    _trace = trace.Select(data => Marshal.SerializeForHW(data)).ToArray();
                    AddProcess(TraceMon, Clk, EnIn);
                }
                if (MappedVariable.Type.CILType.Equals(typeof(double)))
                {
                    AddProcess(DiagMonDouble, Clk, EnIn);
                }
                else if (MappedVariable.Type.CILType.Equals(typeof(SFix)))
                {
                    var fmt = (FixFormat)MappedVariable.Type.TypeParams[0];
                    _fracWidth = fmt.FracWidth;
                    AddProcess(DiagMonSFix, Clk, EnIn);
                }
            }
        }
    }

    class LocalStorageUnitReadMapping : DefaultXILMapping
    {
        private LocalStorageUnit _host;

        public LocalStorageUnitReadMapping(LocalStorageUnit host) :
            base(host.TASite, EMappingKind.ExclusiveResource)
        {
            _host = host;
        }

        public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
        {
            return _host.TASite.Read(results[0]);
        }

        protected override IEnumerable<TAVerb> RealizeDefault()
        {
            return _host.TASite.Read(SignalSink.Nil<StdLogicVector>());
        }

        public override int InitiationInterval
        {
            get { return 0; }
        }

        public override int Latency
        {
            get { return 0; }
        }

        public override string Description
        {
            get { return _host.MappedVariableName + " variable reader"; }
        }
    }

    class LocalStorageUnitWriteMapping : DefaultXILMapping
    {
        private LocalStorageUnit _host;

        public LocalStorageUnitWriteMapping(LocalStorageUnit host) :
            base(host.TASite, EMappingKind.ExclusiveResource)
        {
            _host = host;
        }

        public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
        {
            return _host.TASite.Write(operands[0]);
        }

        protected override IEnumerable<TAVerb> RealizeDefault()
        {
            return _host.TASite.Write(SignalSource.Create(StdLogicVector._0s(_host.DataWidth)));
        }

        public override int InitiationInterval
        {
            get { return _host.IsDirectFeed ? 1 : 0; }
        }

        public override int Latency
        {
            get { return _host.IsDirectFeed ? 0 : 1; }
        }

        public override string Description
        {
            get { return _host.MappedVariableName + " variable reader"; }
        }
    }

    public class LocalStorageUnitXILMapper : IXILMapper
    {
        public bool EnableDirectFeed { get; set; }

        #region IXILMapper Member

        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.LoadVar(null);
            yield return DefaultInstructionSet.Instance.StoreVar(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            if (!instr.Name.Equals(InstructionCodes.LoadVar) &&
                !instr.Name.Equals(InstructionCodes.StoreVar))
                yield break;

            var tgtVar = instr.Operand as Variable;
            if (tgtVar == null)
                yield break;

            var fu = taSite.Host;
            LocalStorageUnit lsu = fu as LocalStorageUnit;
            if (lsu == null)
                yield break;
            if (!lsu.MappedVariable.Equals(tgtVar))
                yield break;

            if (instr.Name.Equals(InstructionCodes.LoadVar))
                yield return new LocalStorageUnitReadMapping(lsu);
            else
                yield return new LocalStorageUnitWriteMapping(lsu);
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            if (!instr.Name.Equals(InstructionCodes.LoadVar) &&
                !instr.Name.Equals(InstructionCodes.StoreVar))
                return null;

            var tgtVar = instr.Operand as Variable;
            if (tgtVar == null)
                return null;

            bool dfeed = EnableDirectFeed;
            if (tgtVar.HasAttribute<FeedThruVariable>())
                dfeed = true;
            else if (tgtVar.HasAttribute<RegisteredVariable>())
                dfeed = false;

            LocalStorageUnit lsu = new LocalStorageUnit(tgtVar, dfeed);

            if (instr.Name.Equals(InstructionCodes.LoadVar))
                return new LocalStorageUnitReadMapping(lsu);
            else
                return new LocalStorageUnitWriteMapping(lsu);
        }

        #endregion
    }
}

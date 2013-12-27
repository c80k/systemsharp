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
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.DesignGen
{
    /// <summary>
    /// This control-path builder creates horizontally microcoded architectures (HMAs).
    /// </summary>
    public class MicrocodeControlpathBuilder: IControlpathBuilder
    {
        private class FactoryImpl : IControlpathBuilderFactory
        {
            private int _maxSelWidth;
            private bool _staged;
            private bool _registered;

            public FactoryImpl(int maxSelWidth, bool staged, bool registered)
            {
                _maxSelWidth = maxSelWidth;
                _staged = staged;
                _registered = registered;
            }

            public IControlpathBuilder Create(Component host, IAutoBinder binder)
            {
                return new MicrocodeControlpathBuilder(host, binder, 1, _maxSelWidth, _staged, _registered);
            }
        }

        /// <summary>
        /// Returns a factory for creating instances of this class, using the default configuration <c>maxSelWidth = 6</c>,
        /// <c>staged = false</c>, <c>registered = false</c>.
        /// </summary>
        public static readonly IControlpathBuilderFactory Factory = new FactoryImpl(6, false, false);

        /// <summary>
        /// Creates a factory for creating instances of this class, using a user-defined configuration.
        /// </summary>
        /// <param name="maxSelWidth">maximum admissible selection input width of a control-word decoder</param>
        /// <param name="staged">A staged control-word decoder introduces an additional register at the decoding output, 
        /// allowing for faster clock speeds.</param>
        /// <param name="registered">Whether to insert an additional register inside the staged decoder - only meaningful if
        /// <paramref name="staged"/> is <c>true</c>.</param>
        public static IControlpathBuilderFactory CreateFactory(int maxSelWidth = 6, bool staged = false, bool registered = false)
        {
            return new FactoryImpl(maxSelWidth, staged, registered);
        }

        private class SyncTemplate : AlgorithmTemplate
        {
            private MicrocodeControlpathBuilder _cpb;

            public SyncTemplate(MicrocodeControlpathBuilder cpb)
            {
                _cpb = cpb;
            }

            protected override void DeclareAlgorithm()
            {
                Signal<StdLogic> clkInst = _cpb._binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                SignalRef clkRising = SignalRef.Create(clkInst.Descriptor, SignalRef.EReferencedProperty.RisingEdge);
                LiteralReference lrClkRising = new LiteralReference(clkRising);

                If(lrClkRising);
                {
                    Store(_cpb._rst.ToSignalRef(SignalRef.EReferencedProperty.Next),
                        LiteralReference.CreateConstant(StdLogic._0));
                }
                EndIf();
            }

            protected override string FunctionName
            {
                get { return "SyncResetLogic"; }
            }
        }

        private Component _host;
        private IAutoBinder _binder;

        private SLVSignal _pc;
        private SLVSignal _altAddr;
        private SLVSignal _brP;
        private SLVSignal _brN;
        private SLSignal _rdEn;
        private SLSignal _rst;
        private SLVSignal _curCW;
        private MicrocodeDesigner _mcd;
        private HLSPlan _plan;
        private BCU _bcu;
        private IBlockMemFactory _romFactory;
        private Component _rom;
        private IROM _romIf;
        private int _romLatency;
        private int _maxSelWidth;
        private bool _staged;
        private bool _registered;

        private MicrocodeControlpathBuilder(Component host, IAutoBinder binder, int romLatency, int maxSelWidth, bool staged, bool registered)
        {
            _host = host;
            _binder = binder;
            _mcd = new MicrocodeDesigner();
            _romLatency = romLatency;
            _maxSelWidth = maxSelWidth;
            _staged = staged;
            _registered = registered;
        }

        public void PersonalizePlan(HLSPlan plan)
        {
            _plan = plan;
            _romFactory = plan.ProgramROMFactory;
            _bcu = new BCU(_romLatency + (_staged ? (_registered ? 2 : 1) : 0));
            _plan.AddXILMapper(new BCUMapper(_bcu));
            _plan.XILMappers.RemoveAll(xm => xm is ConstLoadingXILMapper);
            _plan.AddXILMapper(new SignalConstLoadingXILMapper());
        }

        private void CreateState(int ncsteps)
        {
            int pcWidth = MathExt.CeilLog2(ncsteps);
            _pc = (SLVSignal)_binder.GetSignal(EPortUsage.State, "State", null, StdLogicVector._0s(pcWidth));
            _altAddr = (SLVSignal)_binder.GetSignal(EPortUsage.Default, "BCU_AltAddr", null, StdLogicVector._0s(pcWidth));
            _rdEn = (SLSignal)_binder.GetSignal(EPortUsage.Default, "ROM_RdEn", null, StdLogic._1);
            _brP = (SLVSignal)_binder.GetSignal<StdLogicVector>(EPortUsage.Default, "BCU_BrP", null, "0");
            _brN = (SLVSignal)_binder.GetSignal<StdLogicVector>(EPortUsage.Default, "BCU_BrN", null, "1");
            _rst = (SLSignal)_binder.GetSignal(EPortUsage.Default, "BCU_Rst", null, StdLogic._1);
        }

        private void PrepareBCU(int ncsteps)
        {
            _bcu.AddrWidth = MathExt.CeilLog2(ncsteps);
            _bcu.StartupAddr = StdLogicVector._0s(_bcu.AddrWidth);
            _bcu.Clk = (SLSignal)_binder.GetSignal(EPortUsage.Clock, "Clk", null, null);
            _bcu.Rst = _rst;
            _bcu.BrP = _brP;
            _bcu.BrN = _brN;
            _bcu.AltAddr = _altAddr;
            _bcu.OutAddr = _pc;
        }

        private void CreateROM(int ncsteps)
        {
            _romFactory.CreateROM(_pc.Size, _mcd.CWWidth, ncsteps, Math.Max(1, _romLatency - 1), out _rom, out _romIf);
            // latency - 1 because BCU accounts for an additional stage

            _host.Descriptor.AddChild(_rom.Descriptor, "ProgramROM");
            _romIf.Clk = (SLSignal)_binder.GetSignal(EPortUsage.Clock, "Clk", null, null);
            _romIf.Addr = _pc;
            _romIf.RdEn = _rdEn;
            _romIf.DataOut = _curCW;
        }

        public void PrepareAllocation(long cstepCount)
        {
            int ncsteps = (int)cstepCount;
            CreateState(ncsteps);
            PrepareBCU(ncsteps);
        }

        public void CreateControlpath(FlowMatrix flowSpec, string procName)
        {
            int ncsteps = flowSpec.NumCSteps;
            string report = _mcd.ComputeEncoding(flowSpec, _maxSelWidth);
            _curCW = (SLVSignal)_binder.GetSignal<StdLogicVector>(EPortUsage.Default, "CurCW", null,
                StdLogicVector._0s(_mcd.CWWidth));
            var clkInst = _binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
            if (_staged)
                _mcd.CreateStagedDecoder(_binder, _curCW, (SLSignal)clkInst, _registered);
            else
                _mcd.CreateDecoder(_binder, _curCW);
            CreateROM(ncsteps);
            for (int cstep = 0; cstep < ncsteps; cstep++)
            {
                var cw = _mcd.Encode(cstep, flowSpec.GetFlow(cstep));
                _romIf.PreWrite(StdLogicVector.FromUInt((uint)cstep, _pc.Size), cw);
            }

            var syncTempl = new SyncTemplate(this);
            var syncFunc = syncTempl.GetAlgorithm();
            _binder.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, syncFunc, clkInst.Descriptor);

            _host.Descriptor.GetDocumentation().Documents.Add(new Document(procName + "_HMA_report.txt", report));
        }
    }
}

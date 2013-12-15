using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.DesignGen
{
    /// <summary>
    /// This interconnect builder is based on the <c>HClustInterconnectBuilder</c>. As a preprocessing step,
    /// it splits each data-flow into a sequence of chained data-flows, each requiring a single clock step.
    /// In doing so, the inference of shift registers by FPGA synthesis is supported.
    /// </summary>
    public class StagePlexInterconnectBuilder: IInterconnectBuilder
    {
        private class FactoryImpl : IInterconnectBuilderFactory
        {
            public IInterconnectBuilder Create(Component host, IAutoBinder binder)
            {
                return new StagePlexInterconnectBuilder(host, binder);
            }
        }

        /// <summary>
        /// Returns a factory for creating instances of this class.
        /// </summary>
        public static readonly IInterconnectBuilderFactory Factory = new FactoryImpl();

        private Component _host;
        private IAutoBinder _binder;
        private HClustInterconnectBuilder _hcib;
        private List<SignalBase> _stageInSignals = new List<SignalBase>();
        private List<SignalBase> _stageOutSignals = new List<SignalBase>();

        private class SyncTemplate : AlgorithmTemplate
        {
            private StagePlexInterconnectBuilder _icb;

            public SyncTemplate(StagePlexInterconnectBuilder icb)
            {
                _icb = icb;
            }

            protected override void DeclareAlgorithm()
            {
                Signal<StdLogic> clkInst = _icb._host.AutoBinder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
                SignalRef clkRising = SignalRef.Create(clkInst.Descriptor, SignalRef.EReferencedProperty.RisingEdge);
                LiteralReference lrClkRising = new LiteralReference(clkRising);

                If(lrClkRising);
                {
                    for (int i = 0; i < _icb._stageInSignals.Count; i++)
                    {
                        var slotCurInst = _icb._stageOutSignals[i];
                        SignalRef slotCur = SignalRef.Create(slotCurInst, SignalRef.EReferencedProperty.Next);

                        var slotNextInst = _icb._stageInSignals[i];
                        SignalRef slotNext = SignalRef.Create(slotNextInst, SignalRef.EReferencedProperty.Cur);
                        LiteralReference lrSlotNext = new LiteralReference(slotNext);

                        Store(slotCur, lrSlotNext);
                    }
                }
                EndIf();
            }

            protected override string FunctionName
            {
                get { return "SyncStagesFSM"; }
            }
        }

        private StagePlexInterconnectBuilder(Component host, IAutoBinder binder)
        {
            _host = host;
            _binder = binder;
            _hcib = (HClustInterconnectBuilder)HClustInterconnectBuilder.Factory.Create(host, binder);
        }

        private int _tmpIdx;

        private IEnumerable<ITimedFlow> ConstructNetwork(SignalRef target, IEnumerable<ITimedFlow> flows)
        {
            var groupedByDelay = flows
                .GroupBy(tf => tf is TimedSignalFlow ? ((TimedSignalFlow)tf).Delay : 0)
                .OrderBy(grp => grp.Key);
            var curTarget = target;
            int remainingFanIn = flows.Count();
            long generation = 0;
            var pumpOut = new List<SignalFlow>();
            foreach (var delayGroup in groupedByDelay)
            {
                foreach (var tflow in delayGroup)
                {
                    var tsf = tflow as TimedSignalFlow;
                    var tvf = tflow as TimedValueFlow;
                    if (tsf != null)
                    {
                        long flowDelay = tsf.Delay - generation;
                        if (flowDelay == 0)
                        {
                            yield return new TimedSignalFlow(tsf.Source, curTarget, tsf.Time, 0);
                        }
                        else
                        {
                            SignalBase tmpSig = Signals.CreateInstance(tsf.Source.Desc.InitialValue);
                            tmpSig.Descriptor.TagTemporary(_tmpIdx++);
                            yield return new TimedSignalFlow(
                                tsf.Source, 
                                tmpSig.ToSignalRef(SignalRef.EReferencedProperty.Next), 
                                tsf.Time, 0);
                            yield return new TimedSignalFlow(
                                tmpSig.ToSignalRef(SignalRef.EReferencedProperty.Cur),
                                curTarget,
                                tsf.Time + flowDelay, 0);
                        }
                        long start = tsf.Time + tsf.Delay;
                        foreach (var pump in pumpOut)
                        {
                            yield return new TimedSignalFlow(pump.Source, pump.Target, start, 0);
                            start--;
                        }
                    }
                    else
                    {
                        // remark: as of now, delay is always 0
                        yield return new TimedValueFlow(tvf.Value, curTarget, tvf.Time);
                    }
                }
                long delay = delayGroup.Key;
                remainingFanIn -= delayGroup.Count();
#if false
                if (remainingFanIn > 1)
                {
                    var stageInSignal = _binder.GetSignal(EPortUsage.Default,
                        "icn_" + target.Desc.Name + "_" + generation + "_in",
                        null,
                        target.Desc.InitialValue);
                    var stageOutSignal = _binder.GetSignal(EPortUsage.Default,
                        "icn_" + target.Desc.Name + "_" + generation + "_out",
                        null,
                        target.Desc.InitialValue);
                    _stageInSignals.Add(stageInSignal);
                    _stageOutSignals.Add(stageOutSignal);
                    var pumpSource = new SignalRef(stageOutSignal.ToSignalRef(SignalRef.EReferencedProperty.Cur));
                    pumpOut.Add(new SignalFlow(pumpSource, curTarget));
                    curTarget = new SignalRef(stageInSignal.ToSignalRef(SignalRef.EReferencedProperty.Next));
                    ++generation;
                }
#endif
            }
        }

        private void CreateStagedInterconnect(FlowMatrix flowSpec, FlowMatrix detailedFlow)
        {
            flowSpec.Transitize();
            detailedFlow.AddNeutral(flowSpec.NeutralFlow);
            var tflows = flowSpec.GetTimedFlows();
            var grouped = tflows.GroupBy(tf => tf.Target);
            foreach (var group in grouped)
            {
                var net = ConstructNetwork(group.Key, group);
                foreach (var tflow in net)
                {
                    var tsf = tflow as TimedSignalFlow;
                    var tvf = tflow as TimedValueFlow;
                    if (tsf != null)
                        detailedFlow.Add((int)tflow.Time, new SignalFlow(tsf.Source, tsf.Target));
                    else
                        detailedFlow.Add((int)tflow.Time, new ValueFlow(tvf.Value, tvf.Target));
                }
            }
            for (int i = 0; i < _stageInSignals.Count; i++)
            {
                detailedFlow.AddNeutral(new SignalFlow(
                    _stageOutSignals[i].ToSignalRef(SignalRef.EReferencedProperty.Cur),
                    _stageInSignals[i].ToSignalRef(SignalRef.EReferencedProperty.Next)));
            }
        }

        private void InstantiateControlLogic()
        {
            var syncTempl = new SyncTemplate(this);
            var syncFunc = syncTempl.GetAlgorithm();
            Signal<StdLogic> clkInst = _binder.GetSignal<StdLogic>(EPortUsage.Clock, "Clk", null, '0');
            _host.Descriptor.CreateProcess(SystemSharp.Components.Process.EProcessKind.Triggered, syncFunc, clkInst.Descriptor);
        }

        public void CreateInterconnect(FlowMatrix flowSpec, FlowMatrix detailedFlow)
        {
            var temp = new FlowMatrix();
            CreateStagedInterconnect(flowSpec, temp);
            InstantiateControlLogic();
            _hcib.CreateInterconnect(temp, detailedFlow);
        }
    }
}

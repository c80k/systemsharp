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
 * 
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Assembler.DesignGen;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Std;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Transformations;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Analysis.M2M
{
    /// <summary>
    /// An interface for classes which rewrite lists of XIL-S instructions.
    /// </summary>
    public interface IXILSRewriter
    {
        /// <summary>
        /// Rewrites a list of XIL-S instructions.
        /// </summary>
        /// <param name="instrs">List of input instructions</param>
        /// <returns>List of output instructions</returns>
        IList<XILSInstr> Rewrite(IList<XILSInstr> instrs);
    }

    /// <summary>
    /// An interface for classes which rewrite lists of XIL-3 instructions.
    /// </summary>
    public interface IXIL3Rewriter
    {
        /// <summary>
        /// Rewrites a list of XIL-3 instructions.
        /// </summary>
        /// <param name="instrs">List of input instructions</param>
        /// <returns>List of output instructions</returns>
        XIL3Function Rewrite(XIL3Function func);
    }

    /// <summary>
    /// Indicates the ability of a XIL-S or XIL-3 rewriter to deliver a report on the last rewrite action. 
    /// This is intended for documentation purposes.
    /// </summary>
    public interface IReportingXILRewriter
    {
        /// <summary>
        /// Writes a report on the last rewrite action to a given stream.
        /// </summary>
        /// <param name="stm">The stream where to write the report.</param>
        void GetReport(Stream stm);
    }

    /// <summary>
    /// Describes the progress of a high-level synthesis process in terms of major milestones.
    /// </summary>
    public enum EHLSProgress
    {
        /// <summary>
        /// The XIL code was compiled.
        /// </summary>
        Compiled,

        /// <summary>
        /// The XIL code was rewritten and is now ready to be scheduled.
        /// </summary>
        AboutToSchedule,

        /// <summary>
        /// The XIL code was scheduled.
        /// </summary>
        Scheduled,

        /// <summary>
        /// The interconnect logic of the datapath was created.
        /// </summary>
        InterconnectCreated,

        /// <summary>
        /// The control path was created.
        /// </summary>
        ControlpathCreated
    }

    /// <summary>
    /// Callback to be invoked whenever the high-level synthesis process makes any progress.
    /// </summary>
    /// <param name="state">The originating HLS</param>
    /// <param name="progress">Reached milestone</param>
    public delegate void HLSProgressProc(IHLSState state, EHLSProgress progress);

    /// <summary>
    /// Represents the state of a high-level synthesis process-
    /// </summary>
    public interface IHLSState
    {
        /// <summary>
        /// Returns the synthesis plan.
        /// </summary>
        HLSPlan Plan { get; }

        /// <summary>
        /// Returns the associated design context.
        /// </summary>
        DesignContext Design { get; }

        /// <summary>
        /// Returns the hosting component of the algorithm being synthesized.
        /// </summary>
        Component Host { get; }

        /// <summary>
        /// Returns the process descriptor of the algorithm being synthesized.
        /// </summary>
        ProcessDescriptor Proc { get; }

        /// <summary>
        /// Returns the synthesis target project.
        /// </summary>
        IProject TargetProject { get; }

        /// <summary>
        /// Requests the state object to proceed with the next transformation stage.
        /// </summary>
        /// <remarks>
        /// This method is intended to be called from inside an OnProgress event handler.
        /// </remarks>
        void Proceed();

        /// <summary>
        /// Requests the state object to repeat the last transformation stage.
        /// Only makes sense if some parameters are altered
        /// </summary>
        /// <remarks>
        /// This method is intended to be called from inside an OnProgress event handler.
        /// </remarks>
        void Repeat();

        /// <summary>
        /// Requests the state object to cancel HLS.
        /// </summary>
        /// <remarks>
        /// This method is intended to be called from inside an OnProgress event handler.
        /// </remarks>
        void Cancel();

        /// <summary>
        /// The event is triggered whenever some milestone during HLS was reached.
        /// </summary>
        /// <remarks>
        /// The handler may call either of Proceed, Repeat or Cancel. If none of these methods is called, the HLS process will be continued as if Proceed would have been called.
        /// </remarks>
        event HLSProgressProc OnProgress;

        /// <summary>
        /// Returns the scheduling constraints object.
        /// </summary>
        SchedulingConstraints Constraints { get; }

        /// <summary>
        /// Returns the current interconnect builder.
        /// </summary>
        IInterconnectBuilder InterconnectBuilder { get; }

        /// <summary>
        /// Returns the current control path builder.
        /// </summary>
        IControlpathBuilder ControlpathBuilder { get; }

        /// <summary>
        /// Returns the preprocessed SysDOM function being subject to HLS.
        /// </summary>
        Function PreprocessedFunction { get; }

        /// <summary>
        /// Returns the XIL-S representation of the function being subject to HLS (i.e. after compiling its SysDOM representation).
        /// </summary>
        XILSFunction XILSInput { get; }

        /// <summary>
        /// Returns the XIL-S representation of the function being subject to HLS after application of all XIL-S code transformations.
        /// </summary>
        XILSFunction XILSTransformed { get; }

        /// <summary>
        /// Returns the XIL-3 representation of the function being subject to HLS (i.e. after transforming XIL-S to XIL-3).
        /// </summary>
        XIL3Function XIL3Input { get; }

        /// <summary>
        /// Returns the XIL-3 representation of the function being subject to HLS after application of all XIL-3 code transformations.
        /// </summary>
        XIL3Function XIL3Transformed { get; }

        /// <summary>
        /// Returns the scheduling adapter.
        /// </summary>
        XILSchedulingAdapter SchedulingAdapter { get; }

        /// <summary>
        /// Returns the data-flow matrix after scheduling (i.e. with intermediate variables as flow targets).
        /// </summary>
        FlowMatrix RawFlows { get; }

        /// <summary>
        /// Returns the data-flow matrix after interconnect allocation  (i.e. without intermediate variables as flow targets).
        /// </summary>
        FlowMatrix RealFlows { get; }
    }

    /// <summary>
    /// This class provides helper methods which observe any HLS process and create textual reports for documentation/troubleshooting purpose.
    /// </summary>
    /// <remarks>
    /// All reports will be attached to the SysDOM model. It is up to the target project (i.e. IProject implementation) to 
    /// query the documentation and to create according text files.
    /// </remarks>
    public class DocumentingHLSObserver
    {
        static void OnNewHLS(IHLSState state)
        {
            new DocumentingHLSObserver(state);
        }

        /// <summary>
        /// Globally enables the creation of HLS documentation.
        /// </summary>
        public static void Enable()
        {
            HLSPlan.OnBeginHLS += OnNewHLS;
        }

        /// <summary>
        /// Globally disables the creation of HLS documentation.
        /// </summary>
        public static void Disable()
        {
            HLSPlan.OnBeginHLS -= OnNewHLS;
        }

        private DocumentingHLSObserver(IHLSState state)
        {
            state.OnProgress += OnProgress;
        }

        private void Report(IHLSState state, string name, string text)
        {
            state.Host.Descriptor.GetDocumentation().Documents.Add(new Document(
                name + state.Proc.Name + ".txt", text));
        }

        private string GetProfilerReport(SchedulingConstraints constraints)
        {
            var sb = new StringBuilder();
            foreach (var prof in constraints.Profilers)
            {
                sb.AppendLine(prof.ToString());
            }
            return sb.ToString();
        }

        private void OnProgress(IHLSState state, EHLSProgress progress)
        {
            switch (progress)
            {
                case EHLSProgress.Compiled:
                    Report(state, "hls.0.xil-s-input", state.XILSInput.ToString());
                    Report(state, "hls.1.xil-s-transformed", state.XILSTransformed.ToString());
                    Report(state, "hls.2.xil-3-input", state.XIL3Input.ToString());
                    Report(state, "hls.3.xil-3-transformed", state.XIL3Transformed.ToString());
                    break;

                case EHLSProgress.Scheduled:
                    Report(state, "hls.4.schedule.", state.SchedulingAdapter.GetScheduleReport());
                    break;

                case EHLSProgress.InterconnectCreated:
                    Report(state, "hls.5.0.timedflows.", state.RawFlows.GetFlowReport());
                    Report(state, "hls.5.2.interconnectflows.", state.RealFlows.GetFlowReport());
                    Report(state, "hls.5.3.allocation.",
                        state.SchedulingAdapter.Allocator.CreateAllocationStatistics(state.RawFlows.NumCSteps).ToString());
                    Report(state, "hls.5.4.binding.",
                        state.SchedulingAdapter.GetBindingReport());
                    break;

                case EHLSProgress.ControlpathCreated:
                    Report(state, "hls.6.profilers.", GetProfilerReport(state.Constraints));
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// This static class is intended for extension methods dealing with synthesis plans.
    /// </summary>
    public static class HLSPlanExtensions
    {
        /// <summary>
        /// Returns synthesis plan of a given design descriptor. If none exists, a new one gets created.
        /// </summary>
        /// <param name="dd">a design descriptor</param>
        /// <returns>the synthesis plan</returns>
        /// <remarks>The synthesis plan is attached as an attribute to the descriptor.</remarks>
        public static HLSPlan GetHLSPlan(this DesignDescriptor dd)
        {
            Contract.Requires<ArgumentNullException>(dd != null);

            var plan = dd.QueryAttribute<HLSPlan>();
            if (plan == null)
            {
                plan = HLSPlan.CreateDefaultPlan();
                dd.AddAttribute(plan);
            }
            return plan;
        }
    }

    /// <summary>
    /// Represents a synthesis plan.
    /// </summary>
    /// <remarks>
    /// The synthesis plan represents the actual design flow which is applied to a clocked process (at algorithm level) in order to transform it into hardware. 
    /// It constitutes the "heart" of HLS, which currently consists of the following steps:
    /// <para>1. Compile the SysDOM representation to XIL-S code (only if necessary, i.e. if there is no XILSFunction attribute).</para>
    /// <para>2. Apply code transformations at XIL-S level.</para>
    /// <para>3. Transform to XIL-3 representation.</para>
    /// <para>4. Apply code transformations at XIL-3 level.</para>
    /// <para>5. Perform scheduling and allocation, based on registered XIL mappers.</para>
    /// <para>6. Perform interconnect allocation.</para>
    /// <para>7. Perform control path construction.</para>
    /// Each of those transformation steps can be configured in various ways - see documentation below.
    /// </remarks>
    public class HLSPlan
    {
        static HLSPlan()
        {
            DocumentingHLSObserver.Enable();
        }

        private class HLSState : IHLSState
        {
            private HLSPlan _plan;
            private DesignContext _design;
            private Component _host;
            private ProcessDescriptor _proc;
            private IProject _proj;

            public HLSState(HLSPlan plan, DesignContext design, Component host, ProcessDescriptor proc, IProject proj)
            {
                _plan = plan;
                _design = design;
                _host = host;
                _proc = proc;
                _proj = proj;
            }

            public HLSPlan Plan
            {
                get { return _plan; }
            }

            public DesignContext Design
            {
                get { return _design; }
            }

            public Component Host
            {
                get { return _host; }
            }

            public ProcessDescriptor Proc
            {
                get { return _proc; }
            }

            public IProject TargetProject
            {
                get { return _proj; }
            }

            internal bool _proceed;
            public void Proceed()
            {
                _proceed = true;
            }

            internal bool _repeat;
            public void Repeat()
            {
                _repeat = true;
            }

            internal bool _cancel;
            public void Cancel()
            {
                _cancel = true;
            }

            public event HLSProgressProc OnProgress;

            internal void NotifyProgress(EHLSProgress progress)
            {
                _proceed = true;
                _repeat = false;
                _cancel = false;
                if (OnProgress != null)
                    OnProgress(this, progress);
            }

            public SchedulingConstraints Constraints { get; internal set; }
            public IInterconnectBuilder InterconnectBuilder { get; internal set; }
            public IControlpathBuilder ControlpathBuilder { get; internal set; }
            public Function PreprocessedFunction { get; internal set; }
            public XILSFunction XILSInput { get; internal set; }
            public XILSFunction XILSTransformed { get; internal set; }
            public XIL3Function XIL3Input { get; internal set; }
            public XIL3Function XIL3Transformed { get; internal set; }
            public XILSchedulingAdapter SchedulingAdapter { get; internal set; }
            public FlowMatrix RawFlows { get; internal set; }
            public FlowMatrix RealFlows { get; internal set; }
        }

        /// <summary>
        /// The callback format used for OnBeginHLS events.
        /// </summary>
        /// <param name="state">The state interface</param>
        public delegate void BeginHLSFunc(IHLSState state);

        private static BeginHLSFunc _beginHLS;

        /// <summary>
        /// This event is triggered whenever a new HLS is started.
        /// </summary>
        public static event BeginHLSFunc OnBeginHLS
        {
            add { _beginHLS += value; }
            remove { _beginHLS -= value; }
        }
       
        /// <summary>
        /// The list of code transformations to be applied at XIL-S level.
        /// </summary>
        /// <remarks>You cannot modify this property. However, you can insert or remove transformations.
        /// </remarks>
        public List<IXILSRewriter> XILSTransformations { get; private set; }

        /// <summary>
        /// The list of code transformations to be applied at XIL-3 level.
        /// </summary>
        /// <remarks>You cannot modify this property. However, you can insert or remove transformations.
        /// </remarks>
        public List<IXIL3Rewriter> XIL3Transformations { get; private set; }

        /// <summary>
        /// The list of XIL mappers.
        /// </summary>
        /// <remarks>You cannot modify this property. However, you can insert or remove mappers.
        /// </remarks>
        public List<IXILMapper> XILMappers { get; private set; }

        /// <summary>
        /// Gets or sets the CFG-level scheduling algorithm.
        /// </summary>
        public ICFGSchedulingAlgorithm Scheduler { get; set; }

        /// <summary>
        /// Gets or sets the interconnect builder algorithm.
        /// </summary>
        public IInterconnectBuilderFactory InterconnectBuilder { get; set; }

        /// <summary>
        /// Gets or sets the control path builder algorithm.
        /// </summary>
        public IControlpathBuilderFactory ControlPathBuilder { get; set; }

        /// <summary>
        /// Gets or sets the program ROM factory, which is responsible for creating a platform-specific implementation (i.e. IP core) of the ROM.
        /// </summary>
        public IBlockMemFactory ProgramROMFactory { get; set; }

        /// <summary>
        /// Gets or sets the memory mapper, which is responsible for assigning memory and memory layout to array-typed objects.
        /// </summary>
        public MemoryMapper MemMapper { get; set; }

        /// <summary>
        /// Constructs an empty synthesis plan.
        /// </summary>
        public HLSPlan()
        {
            XILSTransformations = new List<IXILSRewriter>();
            XIL3Transformations = new List<IXIL3Rewriter>();
            XILMappers = new List<IXILMapper>();
            ProgramROMFactory = ROM.Factory;
        }

        /// <summary>
        /// Adds a XIL mapper which is identified by its type or the type of its functional unit.
        /// </summary>
        /// <param name="dpuType">The type of the XIL mapper or the type of the functional unit.</param>
        /// <remarks>If dpuType is the type of a XIL mapper, it will be instantiated by reflection. In this case, make sure
        /// that there is a constructor without arguments. If it is the type of a functional unit, its corresponding mapper
        /// class is identified by the DeclareXILMapper attribute. In that case, make sure that such attribute is declared.</remarks>
        public void AddXILMapper(Type dpuType)
        {
            Contract.Requires<ArgumentNullException>(dpuType != null);

            if (typeof(IXILMapper).IsAssignableFrom(dpuType))
            {
                // dpuType is an IXILMapper type => instantiate it directly.
                try
                {
                    var mapper = (IXILMapper)Activator.CreateInstance(dpuType);
                    AddXILMapper(mapper);
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Unable to create instance of type " + dpuType, e);
                }
            }
            else
            {
                // check for DeclareXILMapper attribute
                var decl = (DeclareXILMapper)
                    dpuType.GetCustomAndInjectedAttributes(typeof(DeclareXILMapper)).SingleOrDefault();
                if (decl == null)
                    throw new ArgumentException("Type " + dpuType + " does not implement IXILMapper, nor does it specify a DeclareXILMapper attribute.");
                AddXILMapper(decl.CreateMapper());
            }
        }

        /// <summary>
        /// Adds an already instantiated XIL mapper.
        /// </summary>
        /// <param name="mapper">A XIL mapper instance.</param>
        public void AddXILMapper(IXILMapper mapper)
        {
            XILMappers.Add(mapper);
        }

        private Action<IXILMapping> _onFUCreated;
        
        /// <summary>
        /// This event will be triggered whenever a new functional unit was created.
        /// </summary>
        public event Action<IXILMapping> OnFUCreated
        {
            add { _onFUCreated += value; }
            remove { _onFUCreated -= value; }
        }

        /// <summary>
        /// Copies the current plan to another instance.
        /// </summary>
        /// <param name="other">The instance to receive the plan.</param>
        public void CopyTo(HLSPlan other)
        {
            other.Scheduler = Scheduler;
            other.InterconnectBuilder = InterconnectBuilder;
            other.ControlPathBuilder = ControlPathBuilder;
            other.MemMapper = MemMapper;
            other.ProgramROMFactory = ProgramROMFactory;
            other.XILSTransformations.AddRange(XILSTransformations);
            other.XIL3Transformations.AddRange(XIL3Transformations);
            other.XILMappers.AddRange(XILMappers);
        }

        /// <summary>
        /// Creates a default synthesis plan.
        /// </summary>
        /// <returns>A default synthesis plan</returns>
        public static HLSPlan CreateDefaultPlan()
        {
            HLSPlan plan = new HLSPlan();
            plan.XILSTransformations.Add(new FixPointImplementor());
            XILMemoryMapper xmm = new XILMemoryMapper()
            {
                MapConstantsToNemory = false,
                MapVariablesToMemory = false
            };            
            //plan.XILSTransformations.Add(xmm);
            plan.MemMapper = xmm.Mapper;
            plan.XILSTransformations.Add(new ReduceTypes());
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Abs_int));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Neg_int));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Abs_double));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Neg_double));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Neg_float));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Cos_ScSinCos_double));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Sin_ScSinCos_double));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Cos_ScSinCos_single));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Sin_ScSinCos_single));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Cos_ScSinCos_fixpt));
            plan.XILSTransformations.Add(new ExpandXILS(EXILSExpansion.Expand_Sin_ScSinCos_fixpt));
            plan.XILSTransformations.Add(new TransitiveGotoEliminator());
            plan.XILSTransformations.Add(new UnreachableInstructionEliminator());
            plan.XILSTransformations.Add(new ConditionalBranchOptimizer());
            plan.XILSTransformations.Add(new UnreachableInstructionEliminator());
            plan.XILSTransformations.Add(new ImplTypeRewriter());
            plan.XILSTransformations.Add(new ReadWriteDependencyInjector());

            plan.XIL3Transformations.Add(new EliminateCommonSubexpressions());

            plan.AddXILMapper(typeof(ALU));
            plan.AddXILMapper(typeof(PortReaderXILMapper));
            plan.AddXILMapper(typeof(PortWriterXILMapper));
            plan.AddXILMapper(typeof(LocalStorageUnit));
            plan.AddXILMapper(typeof(ConstLoadingXILMapper));
            plan.AddXILMapper(typeof(NopMapper));
            plan.AddXILMapper(typeof(MUX2));
            plan.AddXILMapper(typeof(Slicer));
            plan.AddXILMapper(typeof(FixPMod2));
            plan.AddXILMapper(typeof(Shifter));
            //plan.AddXILMapper(typeof(ConcatenizerXILMapper));
            plan.AddXILMapper(typeof(InlineConcatMapper));
            plan.AddXILMapper(typeof(FixedAbs));
            plan.AddXILMapper(typeof(FloatNegAbs));
            plan.AddXILMapper(typeof(FloatSignAsSigned));
            plan.AddXILMapper(typeof(InlineFieldMapper));
            plan.AddXILMapper(typeof(InlineMemoryMapper));
            //plan.AddXILMapper(typeof(SinCosLUTCore));

            plan.Scheduler = ForceDirectedScheduler.Instance.ToFunctionScheduler();
            plan.InterconnectBuilder = MinRegInterconnectBuilder.Factory;
            plan.ControlPathBuilder = FSMControlpathBuilder.Factory;
            return plan;
        }

        private static void ComputeCStepsForBranchTargets(XILSchedulingAdapter xsa)
        {
            var cfg = xsa.CFG;
            for (int i = 0; i < cfg.Instructions.Count; i++)
            {
                XIL3Instr xil3i = cfg.Instructions[i];
                switch (xil3i.Name)
                {
                    case InstructionCodes.BranchIfFalse:
                    case InstructionCodes.BranchIfTrue:
                    case InstructionCodes.Goto:
                        {
                            var target = (BranchLabel)xil3i.StaticOperand;
                            var bb = cfg.GetBasicBlockContaining(target.InstructionIndex);
                            target.CStep = (int)bb.Range.Select(xi => xsa.CStep[xi]).Min();
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Gets or sets the scheduling constraints.
        /// </summary>
        public SchedulingConstraints SchedulingConstraints { get; set; }

        /// <summary>
        /// Gets or sets the functional unit allocation policy.
        /// </summary>
        public IAllocationPolicyFactory AllocationPolicy { get; set; }

        /// <summary>
        /// If set to "true", all field references inside the process being subject to HLS will be replaced with references to local variables.
        /// </summary>
        public bool ConvertFieldsToLocals { get; set; }

#if false
        // scheduled for removal.
        public XIL3Function ExecuteXILTransformations(XILSFunction fnasm)
        {
            fnasm.SanityCheck();
            IList<XILSInstr> instrs = fnasm.Instructions.ToList();
            foreach (IXILSRewriter rw in XILSTransformations)
            {
                instrs = rw.Rewrite(instrs);
                fnasm = new XILSFunction(fnasm.Name, fnasm.Arguments, fnasm.Locals, instrs.ToArray());
                fnasm.SanityCheck();
            }

            XIL3Function fnasm3 = fnasm.ToXIL3();

            foreach (IXIL3Rewriter rw in XIL3Transformations)
            {
                fnasm3 = rw.Rewrite(fnasm3);
                fnasm3.SanityCheck();
            }

            return fnasm3;
        }
#endif

        /// <summary>
        /// Executes the HLS design flow.
        /// </summary>
        /// <param name="design">the design</param>
        /// <param name="host">the hosting component</param>
        /// <param name="proc">the process being subject to HLS</param>
        /// <param name="targetProject">the target project</param>
        /// <remarks>Inside the hosting component, the process will be replaced by the synthesized hardware.</remarks>
        public void Execute(DesignContext design, Component host, ProcessDescriptor proc, IProject targetProject)
        {
            Contract.Requires<ArgumentNullException>(design != null);
            Contract.Requires<ArgumentNullException>(host != null);
            Contract.Requires<ArgumentNullException>(proc != null);
            Contract.Requires<ArgumentNullException>(targetProject != null);

            design.CurrentProcess = proc.Instance;

            var clk = proc.Sensitivity[0];
            SignalBase clkI;
            var sdClk = clk as SignalDescriptor;
            if (sdClk == null)
                clkI = ((SignalDescriptor)((PortDescriptor)clk).BoundSignal).Instance;
            else
                clkI = sdClk.Instance;
            
            var state = new HLSState(this, design, host, proc, targetProject);
            proc.AddAttribute(state);

            if (_beginHLS != null)
                _beginHLS(state);

            var dpb = new DefaultDatapathBuilder(host, clkI, proc.Name);
            state.InterconnectBuilder = InterconnectBuilder.Create(host, dpb.ICBinder);
            state.ControlpathBuilder = ControlPathBuilder.Create(host, dpb.FUBinder);
            state.ControlpathBuilder.PersonalizePlan(this);

            do
            {
                XILSFunction fnasm;
                if (!proc.HasAttribute<XILSFunction>())
                {
                    var func = proc.Implementation;
                    IEnumerable<Function> inlinedFunctions;
                    func = func.InlineCalls(out inlinedFunctions);
                    if (ConvertFieldsToLocals)
                    {
                        Variable[] newLocals;
                        func = func.ConvertFieldsToLocals(out newLocals);
                    }
                    state.PreprocessedFunction = func;

                    fnasm = state.PreprocessedFunction.Compile(DefaultInstructionSet.Instance);
                }
                else
                {
                    fnasm = proc.QueryAttribute<XILSFunction>();
                }
                fnasm.SanityCheck();
                state.XILSInput = fnasm;
                IList<XILSInstr> instrs = state.XILSInput.Instructions.ToList();
                foreach (var rw in XILSTransformations)
                {
                    instrs = rw.Rewrite(instrs);
                    fnasm = new XILSFunction(fnasm.Name, fnasm.Arguments, fnasm.Locals, instrs.ToArray());
                    fnasm.SanityCheck();
                }
                state.XILSTransformed = fnasm;

                XIL3Function fnasm3 = fnasm.ToXIL3();
                state.XIL3Input = fnasm3;

                foreach (IXIL3Rewriter rw in XIL3Transformations)
                {
                    fnasm3 = rw.Rewrite(fnasm3);
                    fnasm3.SanityCheck();
                }

                state.XIL3Transformed = fnasm3;
                state.NotifyProgress(EHLSProgress.Compiled);
            } while (state._repeat);
            if (state._cancel)
                return;

            SchedulingConstraints constraints;

            do
            {
                var xmm = new XILMapperManager();
                foreach (var dpu in Enumerable.Reverse(XILMappers))
                    xmm.AddMapper(dpu);

                DesignContext.Push();

                var xilsa = new XILSchedulingAdapter(state.XIL3Transformed, xmm, host, targetProject);
                if (AllocationPolicy != null)
                    xilsa.Allocator.Policy = AllocationPolicy.Create();
                if (_onFUCreated != null)
                    xilsa.Allocator.OnFUAllocation += _onFUCreated;
                state.SchedulingAdapter = xilsa;
                state.NotifyProgress(EHLSProgress.AboutToSchedule);

                constraints = SchedulingConstraints;
                if (constraints == null)
                {
                    if (proc.Implementation != null)
                        constraints = proc.Implementation.QueryAttribute<SchedulingConstraints>();
                    if (constraints == null)
                        constraints = new SchedulingConstraints();
                }
                state.Constraints = constraints;

                if (constraints.MinimizeNumberOfFUs)
                {
                    foreach (var instr in state.XIL3Transformed.Instructions)
                    {
                        xilsa.SetMaxFUAllocation(xilsa.IClass[instr], 1);
                    }
                }

                Scheduler.Schedule(xilsa.CFG, constraints, xilsa);
                DesignContext.Pop();

                state.NotifyProgress(EHLSProgress.Scheduled);
            } while (state._repeat);

            ComputeCStepsForBranchTargets(state.SchedulingAdapter);

            do
            {
                state.ControlpathBuilder.PrepareAllocation(state.SchedulingAdapter.ComputeCStepCount());
                var flowSpec = state.SchedulingAdapter.Allocate(dpb);
                state.RawFlows = flowSpec;
                var realFlow = new FlowMatrix();
                state.InterconnectBuilder.CreateInterconnect(flowSpec, realFlow);
                state.RealFlows = realFlow;
                state.NotifyProgress(EHLSProgress.InterconnectCreated);
            } while (state._repeat);
            if (state._cancel)
                return;

            Debug.Assert(state.RealFlows.FlowSources.All(sr => sr.Desc.Owner != null));
            Debug.Assert(state.RealFlows.FlowTargets.All(sr => sr.Desc.Owner != null));

            do
            {
                state.ControlpathBuilder.CreateControlpath(state.RealFlows, proc.Name);
                foreach (var prof in constraints.Profilers)
                    prof.ExtractFrom(state.XIL3Transformed, state.SchedulingAdapter);
                state.NotifyProgress(EHLSProgress.ControlpathCreated);
            } while (state._repeat);
            if (state._cancel)
                return;
        }

        /// <summary>
        /// Clones a given synthesis plan.
        /// </summary>
        /// <param name="other">a synthesis plan</param>
        /// <returns>a clone of that plan</returns>
        public static HLSPlan CopyPlan(HLSPlan other)
        {
            var plan = new HLSPlan();
            other.CopyTo(plan);
            return plan;
        }
    }

    /// <summary>
    /// Any process which has this attribute attached will be subject to high level synthesis.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited=false, AllowMultiple=false)]
    public class HLS: Attribute, IOnDecompilation
    {
        private class Refinement : IRefinementCycle
        {
            private ProcessDescriptor _pd;
            private PropertyInfo _planProp;
            private HLSPlan _plan;

            public Refinement(ProcessDescriptor pd, PropertyInfo planProp)
            {
                _pd = pd;
                _planProp = planProp;
            }

            public void Refine(DesignContext context, IProject targetProject)
            {
                ComponentDescriptor cd = (ComponentDescriptor)_pd.Owner;
                Component rtl = cd.Instance;

                // Remove synthesizing process
                cd.RemoveChild(_pd);
                if (_planProp != null)
                {
                    var owner = (ComponentDescriptor)_pd.Owner;
                    _plan = _planProp.GetValue(owner.Instance, new object[0]) as HLSPlan;
                    if (_plan == null)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendFormat("HLS error: declaring type {0} with property {1} does not return an appropriate HLS plan",
                            _pd.Method.DeclaringType.Name, _planProp);
                        throw new InvalidOperationException(sb.ToString());
                    }
                }
                else
                {
                    _plan = HLSPlan.CopyPlan(_pd.GetDesign().GetHLSPlan());
                }
                _plan.Execute(context, rtl, _pd, targetProject);
            }
        }

        private string _planPropName;

        /// <summary>
        /// Requests HLS for the given process, specifying a property name for retrieving the synthesis plan.
        /// </summary>
        /// <param name="planPropName">The name of a property which returns the synthesis plan to use.</param>
        /// <remarks>planPropName must refer to a non-indexed, static or non-static property of the declaring class with public "get" accessor, returning an instance of HLSPlan.</remarks>
        public HLS(string planPropName)
        {
            Contract.Requires<ArgumentNullException>(planPropName != null);

            _planPropName = planPropName;
        }

        /// <summary>
        /// Requests HLS for the given process, using the default synthesis plan.
        /// </summary>
        public HLS()
        {
        }

        public virtual void OnDecompilation(MSILDecompilerTemplate decomp)
        {
            ProcessDescriptor pd = decomp.Decompilee as ProcessDescriptor;
            if (pd == null)
                throw new InvalidOperationException("HLS for " + decomp.Decompilee.Name + " not possible: ain't a process");

            PropertyInfo planProp = null;
            if (_planPropName != null)
            {
                planProp = pd.Method.DeclaringType.GetProperty(_planPropName);
                if (planProp == null && pd.AsyncMethod != null)
                    planProp = pd.AsyncMethod.DeclaringType.GetProperty(_planPropName);
                if (planProp == null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("HLS error: declaring type {0} does not define property {1} defining the HLS plan",
                        pd.Method.DeclaringType.Name, _planPropName);
                    throw new InvalidOperationException(sb.ToString());
                }
            }
            DesignContext.Instance.QueueRefinement(new Refinement(pd, planProp));

            decomp.AddAttribute(this);
            decomp.DisallowConditionals = false;
            decomp.NestLoopsDeeply = false;

            // FIXME: Does it always make sense? Currently just a hack to get PhysicalModels working...
            // Answer: No, it doesn't make sense. To many loops need to be instrumented by ProgramFlow.DoNotUnroll()
            // ==> very error-prone...
            // decomp.TryToEliminateLoops = true;

        }
    }
}

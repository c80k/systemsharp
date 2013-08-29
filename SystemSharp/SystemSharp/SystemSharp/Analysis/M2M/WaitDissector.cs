/**
 * Copyright 2011 Christian Köllner
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
 * CHANGE LOG
 * ==========
 * 2011-08-15 CK -FSMTransformerOptions: Unrolling along certain local variables can be selectively disabled
 * 2013-08-28 CK -commented out obsolete code
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using SDILReader;
using SystemSharp.Analysis.Msil;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Analysis.M2M
{
#if false
    /* This code became obsolete with the shift towards the new C# 5.0 async/await paradigm. Processes implemented using the asynchronous concept
     * are essentially transformed to finite state machines by a compiler transformation and thus require a very different handling.
     * See AsyncStateMachines.cs
     * */

    interface IStateLookup
    {
        IStorableLiteral NextStateSignal { get; }
        Expression GetStateExpression(int ilIndex);
        void ImplementCoState(int issuePoint, IEnumerable<TAVerb> states, int step, IFunctionBuilder builder);
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class DissectionPoint : RewriteCall, IDoNotAnalyze
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            var stateLookup = stack.QueryAttribute<IStateLookup>();
            if (stateLookup == null)
            {
                var pd = decompilee as ProcessDescriptor;
                if (pd == null)
                {
                    var md = decompilee as MethodDescriptor;
                    if (md == null)
                        throw new InvalidOperationException("Unsupported code descriptor: " + decompilee);
                    pd = md.CallingProcess;
                }
                var pred = pd.Instance.Predicate;
                var predElem = stack.GetCallExpression(
                    pred.Method, new StackElement(LiteralReference.CreateConstant(pred.Target), pred.Target, EVariability.Constant));
                var fspec = new FunctionSpec(typeof(void))
                {
                    CILRep = callee,
                    IntrinsicRep = IntrinsicFunctions.Wait(WaitParams.EWaitKind.WaitUntil)
                };
                builder.Call(fspec, new Expression[] { predElem.Expr });
            }
            else
            {
                builder.Store(
                    stateLookup.NextStateSignal,
                    stateLookup.GetStateExpression(stack.CurrentILIndex));
                builder.Return();
            }
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class IssuePoint : RewriteCall
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            var stateLookup = stack.QueryAttribute<IStateLookup>();
            if (stateLookup == null)
            {
                //throw new InvalidOperationException("Process calling Issue(): Please use [TransformIntoFSM] attribute for proper translation.");
                return false;
            }
            else
            {
                IEnumerable<TAVerb> coStates = (IEnumerable<TAVerb>)args[1].Sample;
                stateLookup.ImplementCoState(stack.CurrentILIndex, coStates, 0, builder);
            }
            return true;
        }
    }

    public class WaitDissector
    {
        private IEnumerable<ILInstruction> _instructions;
        private List<int> _dissectionPoints = new List<int>();

        public class DissectResult
        {
            public IList<MethodCode> States { get; private set; }
            public IList<int> DissectionPoints { get; private set; }

            internal DissectResult(IList<MethodCode> states, IList<int> dissectionPoints)
            {
                States = states;
                DissectionPoints = dissectionPoints;
            }
        }

        public WaitDissector(IEnumerable<ILInstruction> instrs)
        {
            _instructions = instrs;
        }

        private void HandleCall(ILInstruction ili)
        {
            MethodBase mb = (MethodBase)ili.Operand;
            if (Attribute.IsDefined(mb, typeof(DissectionPoint)))
            {
                _dissectionPoints.Add(ili.Index);
            }
        }

        public void Dissect()
        {
            MSILProcessor msilp = new MSILProcessor();
            msilp[OpCodes.Call] = HandleCall;
            msilp.Process(_instructions);
        }

        public IList<int> DissectionPoints
        {
            get { return _dissectionPoints; }
        }

        private static void EnsureNotInitBehavior(List<ILInstruction> instrs, int firstExit, string methName)
        {
            for (int ilIndex = 0; ilIndex < firstExit; ilIndex++)
            {
                if (instrs[ilIndex].Code != OpCodes.Nop)
                {
                    DesignContext.Instance.Report(EIssueClass.Error,
                        "FSM specified within " + methName + " exhibits initialization behavior. This is currently not supported. " +
                        "Insert DesignContext.Wait() in the very first line to avoid the problem.");
                    return;
                }
            }
        }

        public static DissectResult Dissect(MethodBase mb)
        {
            MethodBodyReader mbr = new MethodBodyReader(mb);
            WaitDissector wd = new WaitDissector(mbr.instructions);
            wd.Dissect();
            EnsureNotInitBehavior(mbr.instructions, wd.DissectionPoints[0], mb.Name);
            List<MethodCode> states = new List<MethodCode>();
            HashSet<int> exits = new HashSet<int>(wd.DissectionPoints);            
            foreach (int ilIndex in wd.DissectionPoints)
            {
                MethodCode mc = MethodCode.Create(mb, ilIndex + 1, exits);
                states.Add(mc);
            }
            return new DissectResult(states, wd.DissectionPoints);
        }
    }

    class CoStateInfo
    {
        public Function StateAction;
        public IProcess DuringAction;
        public CoStateInfo Next;
        public int StateIndex;
        public LazyExpression StateExpr;
        public object StateValue;
    }

    class CoFSMInfo
    {
        public bool HasNeutralTA { get; set; }
        public Dictionary<Tuple<int, LocalVariableState>, IEnumerable<Tuple<TAVerb, CoStateInfo>>> Verbs { get; private set; }
        public List<CoStateInfo> FirstStateInfos { get; private set; }
        public List<CoStateInfo> StateInfos { get; private set; }
        public CoStateInfo FirstNeutral { get { return FirstStateInfos.First(); } }
        public int TotalStates { get; private set; }
        public Type CoStateType { get; set; }
        public SignalRef CoStateSignal { get; set; }
        public LazyLiteral NextCoState { get; private set; }
        public HashSet<ISignalOrPortDescriptor> Sensitivity { get; private set; }

        public CoFSMInfo()
        {
            Verbs = new Dictionary<Tuple<int, LocalVariableState>, IEnumerable<Tuple<TAVerb, CoStateInfo>>>();
            FirstStateInfos = new List<CoStateInfo>();
            StateInfos = new List<CoStateInfo>();
            Sensitivity = new HashSet<ISignalOrPortDescriptor>();
            NextCoState = new LazyLiteral();
        }

        private IEnumerable<Tuple<TAVerb, CoStateInfo>> CreateCoStateList(IEnumerable<TAVerb> verbs)
        {
            CoStateInfo prev = null;
            foreach (TAVerb coState in verbs)
            {
                CoStateInfo next = new CoStateInfo();
                if (prev != null)
                    prev.Next = next;
                else
                    FirstStateInfos.Add(next);
                StateInfos.Add(next);
                prev = next;
                next.StateIndex = TotalStates++;
                next.StateExpr = new LazyExpression();
                yield return Tuple.Create(coState, next);
            }
        }

        public bool AddVerbs(int issuePoint, LocalVariableState lvState, IEnumerable<TAVerb> verbs)
        {
            IEnumerable<Tuple<TAVerb, CoStateInfo>> result;
            var key = Tuple.Create(issuePoint, lvState);
            if (Verbs.TryGetValue(key, out result))
                return false;

            Verbs[key] = CreateCoStateList(verbs).ToArray();
            return true;
        }

        public IEnumerable<Tuple<TAVerb, CoStateInfo>> GetVerbs(int issuePoint, LocalVariableState lvState)
        {
            return Verbs[Tuple.Create(issuePoint, lvState)];
        }
    }

    class FSMTransformerTemplate : 
        AlgorithmTemplate,
        IStateLookup,
        IDecompilationResult
    {
        private class StateInfo
        {
            public int DissectionPoint;
            public LocalVariableState LVState;
            public MethodCode CFG;
            public Function StateFun;
            public LazyExpression StateExpr;
            public object StateValue;
            public int StateIndex;
        }

        private DesignContext _context;
        private CodeDescriptor _code;
        private MethodCode _methodCode;
        private object _instance;
        private object[] _arguments;
        private IList<MethodCode> _states;
        private IList<int> _dissectionPoints;
        private ILookup<int, MethodCode> _cfgLookup;
        private Type _stateType;
        private SignalRef _stateSignal;
        private LazyLiteral _nextStateSignal;
        private Dictionary<Tuple<int, LocalVariableState>, StateInfo> _stateLookup = 
            new Dictionary<Tuple<int, LocalVariableState>, StateInfo>();
        private Queue<StateInfo> _stateWorkQueue = new Queue<StateInfo>();

        private List<MethodCallInfo> _calledMethods = new List<MethodCallInfo>();
        private List<FieldRefInfo> _referencedFields = new List<FieldRefInfo>();
        private TypeBuilder _tComponent;
        private Dictionary<ITransactionSite, CoFSMInfo> _coFSMs = new Dictionary<ITransactionSite, CoFSMInfo>();
        private MSILDecompilerTemplate _templ;
        private ModuleBuilder _modBuilder;

        public FSMTransformerTemplate(DesignContext ctx, CodeDescriptor code, object instance, object[] arguments)
        {
            _context = ctx;
            _code = code;
            _instance = instance;
            _arguments = arguments;
            _methodCode = MethodCode.Create(_code.Method);
        }

        private void InitializeCoFSMs()
        {
            ProcessDescriptor pd = _code as ProcessDescriptor;
            if (pd != null)
            {
                TransactingComponent.TATarget[] coFSMs = pd.Instance.GetCoFSMs();
                if (coFSMs != null)
                {
                    foreach (TransactingComponent.TATarget target in coFSMs)
                    {
                        CoFSMInfo cfi = new CoFSMInfo();
                        _coFSMs[target.Target] = cfi;
                        if (target.NeutralTA != null)
                        {
                            cfi.HasNeutralTA = true;
                            AddCoStates(-1, LocalVariableState.Empty, target.NeutralTA);
                        }
                        else
                        {
                            cfi.HasNeutralTA = false;
                        }                        
                    }
                }
            }
        }

        private Type CreateEnum(string name, int numStates)
        {
            EnumBuilder tbState = _modBuilder.DefineEnum(name, TypeAttributes.Public, typeof(int));
            for (int i = 0; i < numStates; i++)
            {
                FieldBuilder fb = tbState.DefineLiteral("State" + i, i);
                fb.SetConstant(i);
            }
            Type tState = tbState.CreateType();
            return tState;
        }

        private void Initialize()
        {
            string cname = _code.Name;
            AssemblyName asmName = new AssemblyName();
            asmName.Name = "tmp_" + cname;
            AssemblyBuilder asmBuild = Thread.GetDomain().DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            _modBuilder = asmBuild.DefineDynamicModule("mod_" + cname);
            _nextStateSignal = new LazyLiteral();
        }

        private void CreateStateValues()
        {
            string cname = _code.Name;
            string ename = "E_" + cname + "_State";
            Type tState = CreateEnum(ename, _stateLookup.Count);
            _stateType = tState;
            TypeDescriptor tdState = (TypeDescriptor)tState;
            // do not add type - it will be assigned during post-processing
            //_code.Owner.AddChild(tdState, ename);
            Array enumValues = tState.GetEnumValues();
            foreach (StateInfo si in _stateLookup.Values)
            {
                int i = si.StateIndex;
                si.StateValue = enumValues.GetValue(i);
                si.StateExpr.PlaceHolder = LiteralReference.CreateConstant(si.StateValue);
            }
            ComponentDescriptor owner = (ComponentDescriptor)_code.Owner;
            string fname = "m_" + cname + "_State";
            var initVal = Activator.CreateInstance(tState);
            ISignalOrPortDescriptor sdState = owner.CreateSignalInstance(fname, initVal);
            Type tStateSignal = typeof(Signal<>).MakeGenericType(tState);
            _tComponent = _modBuilder.DefineType(owner.Instance.GetType().FullName, TypeAttributes.Public);
            FieldBuilder fbStateSignal = _tComponent.DefineField(fname, tStateSignal, FieldAttributes.Private);
            _stateSignal = new SignalRef(sdState, SignalRef.EReferencedProperty.Instance);
            _nextStateSignal.PlaceHolder = new SignalRef(sdState, SignalRef.EReferencedProperty.Next);
        }

        private void CreateCoStateValues()
        {            
            foreach (var kvp in _coFSMs)
            {
                ITransactionSite target = kvp.Key;
                CoFSMInfo cfi = kvp.Value;

                string ename, fname;
                if (target.Name == null)
                {
                    ename = "E_" + target.Host.Descriptor.Name + "_CoState";
                    fname = "m_" + target.Host.Descriptor.Name + "_CoState";
                }
                else
                {
                    ename = "E_" + target.Host.Descriptor.Name + "_" + target.Name + "_CoState";
                    fname = "m_" + target.Host.Descriptor.Name + "_" + target.Name + "_CoState";
                }
                
                Type tState = CreateEnum(ename, cfi.TotalStates);
                TypeDescriptor tdState = (TypeDescriptor)tState;
                ComponentDescriptor owner = (ComponentDescriptor)_code.Owner;
                owner.AddChild(tdState, tdState.Name);

                Type tStateSignal = typeof(Signal<>).MakeGenericType(tState);
                FieldBuilder fbStateSignal = _tComponent.DefineField(fname, tStateSignal, FieldAttributes.Private);
                Array enumValues = tState.GetEnumValues();
                object initVal;
                if (cfi.HasNeutralTA)
                    initVal = cfi.FirstNeutral.StateValue;
                else
                    initVal = enumValues.GetValue(0);
                var sdState = owner.CreateSignalInstance(fname, initVal);
                cfi.CoStateSignal = new SignalRef(sdState, SignalRef.EReferencedProperty.Instance);
                cfi.CoStateType = tStateSignal;
                cfi.NextCoState.PlaceHolder = new SignalRef(sdState, SignalRef.EReferencedProperty.Next);

                foreach (CoStateInfo csi in cfi.StateInfos)
                {
                    csi.StateValue = enumValues.GetValue(csi.StateIndex);
                    csi.StateExpr.PlaceHolder = LiteralReference.CreateConstant(csi.StateValue);
                }
            }
        }

        private void InitCoState(TAVerb tav, CoStateInfo csi)
        {
            MethodCode csc = MethodCode.Create(tav.Op.Method);
            MethodDescriptor md = new MethodDescriptor(tav.Op.Method, new object[0], new EVariability[0]);
            MSILDecompiler decomp = new MSILDecompiler(md, csc, tav.Op.Target);
            decomp.Template.DisallowReturnStatements = true;    
            decomp.Template.NestLoopsDeeply = true;
            decomp.Template.TryToEliminateLoops = true;
            decomp.Template.DisallowConditionals = true;            
            IDecompilationResult result = decomp.Decompile();
            csi.StateAction = result.Decompiled;
            csi.DuringAction = tav.During;
        }

        private void ImplementCoStateAction(CoFSMInfo cfi, CoStateInfo csi, IFunctionBuilder builder)
        {
            builder.InlineCall(csi.StateAction, new Expression[0], new Variable[0], true);
            builder.Store(cfi.NextCoState, csi.StateExpr);
            /*
            CoStateInfo next = csi.Next;
            if (next == null && cfi.HasNeutralTA)
                next = cfi.FirstNeutral;
            if (next != null)
            {
                builder.Store(cfi.NextCoState, next.StateExpr);
            }
             * */
        }

        private StateInfo GetStateInfo(int dissectionPoint, LocalVariableState lvState)
        {
            var key = Tuple.Create(dissectionPoint, lvState);
            StateInfo result;
            if (!_stateLookup.TryGetValue(key, out result))
            {
                result = new StateInfo()
                {
                    DissectionPoint = dissectionPoint,
                    LVState = lvState,
                    CFG = _cfgLookup[dissectionPoint].Single(),
                    StateIndex = _stateLookup.Keys.Count,
                    StateExpr = new LazyExpression()
                };
                _stateWorkQueue.Enqueue(result);
                _stateLookup[key] = result;
            }
            return result;
        }

        private void DecompileStates()
        {
            ProcessDescriptor pd = (ProcessDescriptor)_code;
            _templ = new MSILDecompilerTemplate()
            {
                Instance = pd.Instance.Owner,
                ArgumentValues = new object[0],
                Decompilee = _code,
                Method = _code.Method,
                Code = MethodCode.Create(_code.Method),
                DisallowReturnStatements = true,
                NestLoopsDeeply = true,
                TryToEliminateLoops = true,
                DisallowConditionals = true
            };
            LocalVariableState initialState = _templ.ExportLocalVariableState();
            GetStateInfo(_dissectionPoints[0], initialState);

            while (_stateWorkQueue.Any())
            {
                StateInfo si = _stateWorkQueue.Dequeue();
                MSILDecompiler decomp = new MSILDecompiler(_code, si.CFG, _instance)
                {
                    Template = _templ
                };
                _templ.AddAttribute(this);
                _templ.ImportLocalVariableState(si.LVState);
                IDecompilationResult result = decomp.Decompile();
                si.StateFun = result.Decompiled;
                _calledMethods.AddRange(result.CalledMethods);
                _referencedFields.AddRange(result.ReferencedFields);
            }
        }

        private void Dissect()
        {
            WaitDissector.DissectResult dresult = WaitDissector.Dissect(_code.Method);
            _states = dresult.States;
            _dissectionPoints = dresult.DissectionPoints;
            _cfgLookup = _dissectionPoints
                .Select((k, i) => Tuple.Create(k, _states[i]))
                .ToLookup(t => t.Item1, t => t.Item2);
        }

        protected override void DeclareAlgorithm()
        {
            Dissect();
            Initialize();
            InitializeCoFSMs();
            DecompileStates();
            CreateStateValues();
            CreateCoStateValues();

            SignalRef curState = new SignalRef(_stateSignal.Desc, SignalRef.EReferencedProperty.Cur);
            ProcessDescriptor pd = (ProcessDescriptor)_code;
            Func<bool> predFunc = pd.Instance.Predicate;
            LiteralReference predInstRef = LiteralReference.CreateConstant(predFunc.Target);
            StackElement arg = new StackElement(predInstRef, predFunc.Target, EVariability.ExternVariable);
            Expression cond = _templ.GetCallExpression(predFunc.Method, arg).Expr;
            Expression[] e0 = new Expression[0];
            Variable[] v0 = new Variable[0];

            If(cond);
            {
                foreach (var kvp in _coFSMs)
                {
                    CoFSMInfo cfi = kvp.Value;
                    SignalRef srCor = new SignalRef(cfi.CoStateSignal.Desc, SignalRef.EReferencedProperty.Cur);
                    Expression lrCo = new LiteralReference(srCor);
                    CaseStatement csc = Switch(lrCo);
                    {
                        for (int i = 0; i < cfi.TotalStates; i++)
                        {
                            CoStateInfo csi = cfi.StateInfos[i];
                            Expression stateValue = LiteralReference.CreateConstant(csi.StateValue);
                            CoStateInfo csin = csi.Next;
                            if (csin == null && cfi.HasNeutralTA)
                                csin = cfi.FirstNeutral;
                            Case(stateValue);
                            {
                                if (csin != null)
                                {
                                    ImplementCoStateAction(cfi, csin, this);
                                }
                                Break(csc);
                            }
                            EndCase();
                        }
                    }
                    EndSwitch();
                }

                CaseStatement cs = Switch(curState);
                {
                    IEnumerable<StateInfo> states = _stateLookup.Values.OrderBy(si => si.StateIndex);
                    foreach (StateInfo state in states)
                    {
                        Case(state.StateExpr.PlaceHolder);
                        {
                            InlineCall(state.StateFun, e0, v0, true);
                            Break(cs);
                        }
                        EndCase();
                    }
                }
                EndSwitch();
            }
            EndIf();
        }

        public IStorableLiteral NextStateSignal
        {
            get { return _nextStateSignal; }
        }

        public Expression GetStateExpression(int ilIndex)
        {
            LocalVariableState lvState = _templ.ExportLocalVariableState();
            return GetStateInfo(ilIndex, lvState).StateExpr;
        }

        private void AddCoStates(int issuePoint, LocalVariableState lvState, IEnumerable<TAVerb> states)
        {
            Contract.Requires(states != null);

            TAVerb first = states.First();
            CoFSMInfo cfi = _coFSMs[first.Target];
            if (cfi.AddVerbs(issuePoint, lvState, states))
            {
                foreach (var tup in cfi.GetVerbs(issuePoint, lvState))
                {
                    TAVerb tav = tup.Item1;
                    CoStateInfo csi = tup.Item2;
                    if (csi.StateAction == null)
                    {
                        InitCoState(tav, csi);
                        if (tav.During != null)
                        {
                            foreach (AbstractEvent ev in tav.During.Sensitivity)
                                cfi.Sensitivity.Add(((SignalBase)ev.Owner).Descriptor);
                        }
                    }
                }
            }
        }

        public void ImplementCoState(int issuePoint, IEnumerable<TAVerb> states, int step, IFunctionBuilder builder)
        {
            LocalVariableState lvState = _templ.ExportLocalVariableState();
            AddCoStates(issuePoint, lvState, states);
            TAVerb first = states.First();
            CoFSMInfo cfi = _coFSMs[first.Target];
            var tup = cfi.GetVerbs(issuePoint, lvState).ElementAt(step);
            CoStateInfo csi = tup.Item2;
            ImplementCoStateAction(cfi, csi, builder);
        }

        public override Function GetAlgorithm()
        {
            Decompiled = base.GetAlgorithm();
            Decompiled.Name = _code.Name;
            return Decompiled;
        }

        public Type StateStype
        {
            get { return _stateType; }
        }

        public IEnumerable<Tuple<ITransactionSite, CoFSMInfo>> CoFSMs
        {
            get 
            { 
                Contract.Assume(_coFSMs != null);

                return _coFSMs.Select(kvp => Tuple.Create(kvp.Key, kvp.Value)); 
            }
        }

    #region IDecompilationResult Member

        public Function Decompiled { get; private set; }

        public ICollection<MethodCallInfo> CalledMethods
        {
            get { return _calledMethods; }
        }

        public ICollection<FieldRefInfo> ReferencedFields
        {
            get { return _referencedFields; }
        }

        public IEnumerable<object> GetLocalVarAssignedValues(IStorable var)
        {
            // FSM transformation will convert each and every local variable
            // to a field. So the result is always empty.
            return new object[0];
        }

        public IEnumerable<object> ReturnedValues
        {
            // As we transform a process, there will never a return value.
            // A process must be a method of return type void.
            get { return new object[0]; }
        }

        #endregion
    }

    class FSMCombTemplate : AlgorithmTemplate
    {
        private CoFSMInfo _cfi;

        public FSMCombTemplate(CoFSMInfo cfi)
        {
            Contract.Requires(cfi != null);
            _cfi = cfi;
        }

        protected override void DeclareAlgorithm()
        {
            SignalRef curState = new SignalRef(_cfi.CoStateSignal.Desc, SignalRef.EReferencedProperty.Cur);
            CaseStatement cs = Switch(curState);
            {
                for (int i = 0; i < _cfi.TotalStates; i++)
                {
                    CoStateInfo csi = _cfi.StateInfos[i];
                    object stateValue = csi.StateValue;
                    Case(LiteralReference.CreateConstant(stateValue));
                    {
                        if (csi.DuringAction != null)
                            csi.DuringAction.Implement(this);
                    }
                    Break(cs);
                }
            }
            EndSwitch();
        }
    }

    class FSMTransformer
    {
        private DesignContext _context;
        private CodeDescriptor _code;
        private object _instance;
        private object[] _arguments;

        public ICollection<MethodCallInfo> CalledMethods { get; private set; }

        public FSMTransformer(DesignContext ctx, CodeDescriptor code, object instance, object[] arguments)
        {
            System.Diagnostics.Debug.Assert(arguments.Length == 1);
            _context = ctx;
            _code = code;
            _instance = instance;
            _arguments = arguments;
        }

        public IDecompilationResult Transform()
        {
            ProcessDescriptor pd = _code as ProcessDescriptor;
            FSMTransformerTemplate templ = 
                new FSMTransformerTemplate(_context, _code, _instance, _arguments);
            Function body = templ.GetAlgorithm();
            _code.Implementation = body;
            foreach (var tup in templ.CoFSMs)
            {
                CoFSMInfo cfi = tup.Item2;
                cfi.Sensitivity.Add(cfi.CoStateSignal.Desc);
                FSMCombTemplate cotempl = new FSMCombTemplate(cfi);
                Function comb = cotempl.GetAlgorithm();
                string name = _code.Name + "$comb";
                ProcessDescriptor pdcomb = new ProcessDescriptor(name)
                {
                    Kind = Process.EProcessKind.Triggered,
                    Implementation = comb,
                    GenuineImplementation = body,
                    Sensitivity = cfi.Sensitivity.ToArray()
                };
                _code.Owner.AddChild(pdcomb, name);
            }
            return templ;
        }
    }

    public class TransformIntoFSM : RewriteMethodDefinition
    {
        public override IDecompilationResult Rewrite(DesignContext ctx, CodeDescriptor code, object instance, object[] arguments)
        {
            ProcessDescriptor pd = (ProcessDescriptor)code;
            pd.Kind = Process.EProcessKind.Triggered;
            FSMTransformer trans = new FSMTransformer(ctx, code, instance, arguments);
            return trans.Transform();
        }
    }

    public static class FSMTransformerOptions
    {
        private class DoNotUnrollRewriter : RewriteCall
        {
            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                LiteralReference lvref = (LiteralReference)args[0].Expr;
                Variable lv = lvref.ReferencedObject as Variable;
                if (lv == null)
                    throw new InvalidOperationException("DoNotUnroll must be applied to a local variable!");
                stack.DoNotUnroll(lv.LocalIndex);
                return true;
            }
        }

        [DoNotUnrollRewriter]
        public static void DoNotUnroll<T>(out T localVariable)
        {
            localVariable = default(T);
        }
    }
#endif

    /// <summary>
    /// This attribute, attached to a clocked threaded process, will convert that process to a synthesizable finite state machine.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false, Inherited=false)]
    public class TransformIntoFSM: Attribute
    {
    }
}

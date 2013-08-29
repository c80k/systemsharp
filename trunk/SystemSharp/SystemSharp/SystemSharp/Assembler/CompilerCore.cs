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
using System.Diagnostics.Contracts;
using System.Linq;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Analysis.Msil;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.FU;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Eval;
using SystemSharp.SysDOM.Transformations;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Assembler
{
    public interface ICompilerBackend
    {
        IEnumerable<int> PredsInCurBB { get; }
        void InstallBarrier(params string[] instrCodes);
        void InstallBarrier();
        void Emit(XILSInstr instr);
    }

    public class Compiler: 
        IStatementVisitor, 
        IExpressionVisitor<int>,
        ILiteralVisitor
    {
        private class Backend : ICompilerBackend
        {
            public Backend(Compiler compiler)
            {
                _compiler = compiler;
            }

            private Compiler _compiler;

            public void Emit(XILSInstr instr)
            {
                _compiler.Emit(instr);
            }

            public IEnumerable<int> PredsInCurBB
            {
                get { return _compiler._curBB; }
            }

            public void InstallBarrier(params string[] instrCodes)
            {
                HashSet<string> set = new HashSet<string>(instrCodes);
                var preds = _compiler._curBB
                    .Where(i => set.Contains(_compiler._instrs[i].Name))
                    .Select(i => new OrderDependency(i, OrderDependency.EKind.BeginAfter))
                    .ToArray();
                Emit(DefaultInstructionSet.Instance.Barrier().CreateStk(preds, 0));
                foreach (string instrCode in instrCodes)
                    _compiler._barriers[instrCode] = _compiler._curBB.Last();                
            }

            public void InstallBarrier()
            {
                InstallBarrier(InstructionCodes.AllCodes.ToArray());
            }
        }

        private enum ELiteralAcceptMode
        {
            Read,
            Write
        }

        public IInstructionSet<XILInstr> ISet { get; private set; }
        public Function Compilee { get; private set; }
        private List<XILSInstr> _instrs = new List<XILSInstr>();
        private Stack<TypeDescriptor> _typeStack = new Stack<TypeDescriptor>();
        private Dictionary<Statement, BranchLabel> _stmt2label = new Dictionary<Statement, BranchLabel>();
        private IEvaluator _eval = new DefaultEvaluator();
        private ELiteralAcceptMode _litMode;
        private List<int> _curBB = new List<int>();
        private Dictionary<object, List<int>> _storesInCurBB = new Dictionary<object, List<int>>();
        private Dictionary<object, List<int>> _readsInCurBB = new Dictionary<object, List<int>>();
        private bool _closeBBOnNextInstr;
        private Backend _backend;
        private Dictionary<string, int> _barriers = new Dictionary<string, int>();
        
        public List<XILSInstr> Result
        {
            get { return _instrs; }
        }

        public Compiler(IInstructionSet<XILInstr> iset, Function func)
        {
            ISet = iset;
            Compilee = func;
            _backend = new Backend(this);
            ResetBarriers();
        }

        private void ResetBarriers()
        {
            foreach (string instrCode in InstructionCodes.AllCodes)
                _barriers[instrCode] = -1;
        }

        private BranchLabel CreateLabel()
        {
            return new BranchLabel();
        }

        private BranchLabel CreateLabel(Statement nextStmt)
        {
            BranchLabel label;
            if (!_stmt2label.TryGetValue(nextStmt, out label))
            {
                label = new BranchLabel();
                _stmt2label[nextStmt] = label;
            }
            return label;
        }

        private int NextInstructionIndex
        {
            get { return _instrs.Count; }
        }

        private ILIndexRef _curCILRef;
        private Expression _curExpr;

        private BranchLabel CreateLabelForNextInstruction(Statement nextStmt)
        {
            var ilIndexRef = nextStmt.QueryAttribute<ILIndexRef>();
            _curCILRef = ilIndexRef;

            BranchLabel label = CreateLabel(nextStmt);
            label.InstructionIndex = NextInstructionIndex;
            return label;
        }

        private void Emit(XILSInstr i)
        {
            i.Index = NextInstructionIndex;
            if (_curCILRef != null)
                i.CILRef = _curCILRef;
            _curBB.Add(i.Index);
            _instrs.Add(i);
        }

        private void Emit(XILInstr xili, object backRef, InstructionDependency[] preds, int numOperands, params TypeDescriptor[] resultTypes)
        {
            Contract.Requires(xili != null && preds != null && numOperands >= 0 &&
                resultTypes != null && resultTypes.All(t => t != null));

            xili.BackRef = backRef;
            TypeDescriptor[] operandTypes = new TypeDescriptor[numOperands];
            for (int i = numOperands - 1; i >= 0; i--)
                operandTypes[i] = _typeStack.Pop();
            for (int i = 0; i < resultTypes.Length; i++)
                _typeStack.Push(resultTypes[i]);
            Emit(xili.CreateStk(preds, operandTypes, resultTypes));
        }

        private void Emit(XILInstr xili, object backRef, int numOperands, params TypeDescriptor[] resultTypes)
        {
            if (_closeBBOnNextInstr)
            {
                Emit(xili, backRef, CloseBB(), numOperands, resultTypes);
                _closeBBOnNextInstr = false;
            }
            else
            {
                int pred = _barriers[xili.Name];
                if (pred >= 0)
                    Emit(xili, backRef, new InstructionDependency[] { new OrderDependency(pred, OrderDependency.EKind.BeginAfter) }, numOperands, resultTypes);
                else
                    Emit(xili, backRef, new InstructionDependency[0], numOperands, resultTypes);
            }
        }

        private InstructionDependency[] CloseBB()
        {
            var preds = _curBB.ToArray();
            _curBB.Clear();
            _storesInCurBB.Clear();
            _readsInCurBB.Clear();
            ResetBarriers();
            return preds.Select(p => new OrderDependency(p, OrderDependency.EKind.BeginAfter)).ToArray();
        }

        private void BeginBB()
        {
            _curBB.Clear();
            _storesInCurBB.Clear();
            _readsInCurBB.Clear();
            ResetBarriers();
        }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            foreach (Statement substmt in stmt.Statements)
                substmt.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            BranchLabel header = CreateLabelForNextInstruction(stmt);
            CloseBB();
            stmt.Body.Accept(this);
            Emit(ISet.Goto(header), stmt, CloseBB(), 0);
            BeginBB();
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            Emit(ISet.Goto(CreateLabel(stmt.Loop.Successor)), stmt, CloseBB(), 0);
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            Emit(ISet.Goto(CreateLabel(stmt.Loop)), stmt, CloseBB(), 0);
        }

        private void ImplementIf(IList<Expression> conds, IList<Statement> branches)
        {
            BranchLabel[] skipTargets = new BranchLabel[conds.Count];
            BranchLabel beyond = CreateLabel();
            for (int i = 0; i < conds.Count; i++)
            {
                Expression cond = conds[i].SimplifyMultiValuedLogic();
                cond.Accept(this);
                BranchLabel target = CreateLabel();
                skipTargets[i] = target;
                XILInstr bi = ISet.BranchIfFalse(target);
                Emit(bi, CloseBB(), 1);
                BeginBB();
                branches[i].Accept(this);
                target.InstructionIndex = NextInstructionIndex;
                if (i != branches.Count - 1)
                {
                    Emit(ISet.Goto(beyond), CloseBB(), 0);
                }
                BeginBB();
            }
            skipTargets.Last().InstructionIndex = NextInstructionIndex;
            if (conds.Count < branches.Count)
            {
                branches.Last().Accept(this);
                BeginBB();
            }
            beyond.InstructionIndex = NextInstructionIndex;
        }

        public void AcceptIf(IfStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            if (stmt.Label != null)
                _closeBBOnNextInstr = true;
            ImplementIf(stmt.Conditions, stmt.Branches);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            IfStatement ifStmt = stmt.ConvertToIfStatement();
            ImplementIf(ifStmt.Conditions, ifStmt.Branches);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            if (stmt.Container.Type.CILType.IsValueType ||
                stmt.Container.Type.HasIntrinsicTypeOverride)
            {
                CreateLabelForNextInstruction(stmt);
                if (stmt.Label != null)
                    _closeBBOnNextInstr = true;
                int rslot = stmt.Value.Accept(this);
                _litMode = ELiteralAcceptMode.Write;
                var lit = stmt.Container;
                lit.Accept(this);
                _litMode = ELiteralAcceptMode.Read;
            }
            else
            {
                // FIXME: Otherwise ignore it because sometimes, some bogus statements
                // like tmp := this occur.
            }
        }

        public void AcceptNop(NopStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            if (stmt.Label != null)
                _closeBBOnNextInstr = true;
            Emit(ISet.Nop(), stmt, 0);
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            throw new NotImplementedException();
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            Emit(ISet.Goto(CreateLabel(stmt.Successor)), CloseBB(), 0);
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            Emit(ISet.Goto(CreateLabel(stmt.CaseStmt.Branches[stmt.TargetIndex])),
                CloseBB(), 0);
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            Emit(ISet.Goto(CreateLabel(stmt.Target)), CloseBB(), 0);
            BeginBB();
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);
            if (stmt.ReturnValue != null)
            {
                stmt.ReturnValue.Accept(this);
                Emit(ISet.Return(), CloseBB(), 1);
            }
            else
            {
                Emit(ISet.Return(), CloseBB(), 0);
            }
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            throw new NotImplementedException();
        }

        public void AcceptCall(CallStatement stmt)
        {
            CreateLabelForNextInstruction(stmt);

            FunctionSpec fspec = stmt.Callee as FunctionSpec;
            if (fspec == null)
                throw new NotSupportedException();

            if (fspec.CILRep != null)
            {
                var cmc = fspec.CILRep.GetCustomOrInjectedAttribute<CompileMethodCall>();
                if (cmc != null)
                {
                    cmc.Compile(stmt, _backend);
                    return;
                }
            }

            IntrinsicFunction ifun = fspec.IntrinsicRep;
            if (ifun != null)
            {
                switch (ifun.Action)
                {
                    case IntrinsicFunction.EAction.Wait:
                        return; // ignore for now

                    default:
                        throw new NotSupportedException();
                }
            }

            throw new NotSupportedException();
        }

        private void ExtractCILIndex(Expression e)
        {
            var ilIndexRef = e.QueryAttribute<ILIndexRef>();
            if (ilIndexRef != null)
                _curCILRef = ilIndexRef;
        }

        public int TransformLiteralReference(LiteralReference expr)
        {
            _curExpr = expr;
            ExtractCILIndex(expr);
            var literal = expr.ReferencedObject;
            literal.Accept(this);
            return 0;
        }

        public int TransformSpecialConstant(SpecialConstant expr)
        {
            ExtractCILIndex(expr);
            object value = expr.Eval(_eval);
            Emit(ISet.LdConst(value), expr, 0, expr.ResultType);
            return 0;
        }

        public int TransformUnOp(UnOp expr)
        {
            expr.Children[0].Accept(this);
            ExtractCILIndex(expr);
            
            if (expr.Operation == UnOp.Kind.Identity)
                return 0;

            XILInstr xi;

            switch (expr.Operation)
            {
                case UnOp.Kind.Abs: xi = ISet.Abs(); break;
                case UnOp.Kind.BitwiseNot:
                case UnOp.Kind.BoolNot: xi = ISet.Not(); break;
                case UnOp.Kind.ExtendSign: xi = ISet.ExtendSign(); break;
                case UnOp.Kind.Neg: xi = ISet.Neg(); break;
                case UnOp.Kind.Cos: xi = ISet.Cos(); break;
                case UnOp.Kind.Sin: xi = ISet.Sin(); break;
                case UnOp.Kind.Sqrt: xi = ISet.Sqrt(); break;
                case UnOp.Kind.Exp:
                case UnOp.Kind.Log: 
                default: throw new NotImplementedException();
            }

            Emit(xi, expr, 1, expr.ResultType);

            return 0;
        }

        public int TransformBinOp(BinOp expr)
        {
            expr.Children[0].Accept(this);
            expr.Children[1].Accept(this);
            ExtractCILIndex(expr);

            XILInstr xi;

            switch (expr.Operation)
            {
                case BinOp.Kind.Add: xi = ISet.Add(); break;
                case BinOp.Kind.And: xi = ISet.And(); break;
                case BinOp.Kind.Concat: xi = ISet.Concat(); break;
                case BinOp.Kind.Div: xi = ISet.Div(); break;
                case BinOp.Kind.Eq: xi = ISet.IsEq(); break;
                case BinOp.Kind.Gt: xi = ISet.IsGt(); break;
                case BinOp.Kind.GtEq: xi = ISet.IsGte(); break;
                case BinOp.Kind.LShift: xi = ISet.LShift(); break;
                case BinOp.Kind.Lt: xi = ISet.IsLt(); break;
                case BinOp.Kind.LtEq: xi = ISet.IsLte(); break;
                case BinOp.Kind.Mul: xi = ISet.Mul(); break;
                case BinOp.Kind.NEq: xi = ISet.IsNEq(); break;
                case BinOp.Kind.Or: xi = ISet.Or(); break;
                case BinOp.Kind.Rem: xi = ISet.Rem(); break;
                case BinOp.Kind.RShift: xi = ISet.RShift(); break;
                case BinOp.Kind.Sub: xi = ISet.Sub(); break;
                case BinOp.Kind.Xor: xi = ISet.Xor(); break;

                case BinOp.Kind.Log:
                case BinOp.Kind.Exp: 
                default: throw new NotImplementedException();
            }

            Emit(xi, expr, 2, expr.ResultType);

            return 0;
        }

        public int TransformTernOp(TernOp expr)
        {
            ExtractCILIndex(expr);
            XILInstr xi;

            switch (expr.Operation)
            {
                case TernOp.Kind.Conditional: 
                    xi = ISet.Select();
                    expr.Children[1].Accept(this);
                    expr.Children[2].Accept(this);
                    expr.Children[0].Accept(this);
                    break;

                case TernOp.Kind.Slice:
                    xi = ISet.Slice();
                    expr.Children[0].Accept(this);
                    expr.Children[1].Accept(this);
                    expr.Children[2].Accept(this);
                    break;

                default: throw new NotImplementedException();
            }

            Emit(xi, expr, 3, expr.ResultType);

            return 0;
        }

        public int TransformFunction(FunctionCall expr)
        {
            FunctionSpec fspec = expr.Callee as FunctionSpec;
            if (fspec == null)
                throw new NotSupportedException();

            IntrinsicFunction ifun = fspec.IntrinsicRep;
            if (ifun == null)
                throw new NotSupportedException();

            switch (ifun.Action)
            {
                case IntrinsicFunction.EAction.Convert:
                    {
                        // only first child should be evaluated, following children are conversion arguments.
                        // These will be implicitly defined by the result type of the instruction
                        expr.Children[0].Accept(this);
                        ExtractCILIndex(expr);
                        var cparams = (CastParams)ifun.Parameter;
                        Emit(ISet.Convert(cparams.Reinterpret), expr, 1, expr.ResultType);
                    }
                    return 0;

                case IntrinsicFunction.EAction.Sign:
                    expr.Children[0].Accept(this);
                    ExtractCILIndex(expr);
                    Emit(ISet.Sign(), expr, 1, expr.ResultType);
                    return 0;

                case IntrinsicFunction.EAction.Resize:
                    expr.Children[0].Accept(this);
                    Emit(ISet.Convert(), expr, 1, expr.ResultType);
                    ExtractCILIndex(expr);
                    return 0;

                case IntrinsicFunction.EAction.Slice:
                    if (expr.Children.Length == 3)
                    {
                        expr.Children[0].Accept(this);
                        expr.Children[1].Accept(this);
                        expr.Children[2].Accept(this);
                        Emit(ISet.Slice(), expr, 3, expr.ResultType);
                    }
                    else
                    {
                        expr.Children[0].Accept(this);
                        var range = (Range)ifun.Parameter;
                        Emit(ISet.SliceFixI(range), expr, 1, expr.ResultType);
                    }
                    return 0;

                case IntrinsicFunction.EAction.Abs:
                    expr.Children[0].Accept(this);
                    Emit(ISet.Abs(), expr, 1, expr.ResultType);
                    return 0;

                case IntrinsicFunction.EAction.Sqrt:
                    expr.Children[0].Accept(this);
                    Emit(ISet.Sqrt(), expr, 1, expr.ResultType);
                    return 0;

                case IntrinsicFunction.EAction.GetArrayElement:
                    {
                        var aex = expr.Children[0];
                        var alr = (LiteralReference)aex;
                        var alit = alr.ReferencedObject;
                        var arr = (Array)aex.ResultType.GetSampleInstance();
                        var far = new FixedArrayRef(alit, arr);
                        expr.Children[1].Accept(this);
                        Emit(ISet.LdelemFixA(far), expr, 1, expr.ResultType);
                    }
                    return 0;

                case IntrinsicFunction.EAction.XILOpCode:
                    {
                        foreach (var child in expr.Children)
                            child.Accept(this);
                        var opcode = (XILInstr)ifun.Parameter;
                        Emit(opcode, expr, expr.Children.Length, expr.ResultType.Unpick());
                    }
                    return 0;

                case IntrinsicFunction.EAction.TupleSelect:
                    {
                        int item = (int)ifun.Parameter;
                        var lr = expr.Children[0] as LiteralReference;
                        if (lr == null)
                            throw new NotSupportedException("TupleSelect must refer to a literal");
                        var tuple = lr.ReferencedObject as Variable;
                        if (tuple == null)
                            throw new NotSupportedException("TupleSelect must refer to a local variable");
                        var itemVars = Unpick(tuple);
                        var itemLr = new LiteralReference(itemVars[item]);
                        TransformLiteralReference(itemLr);
                    }
                    return 0;

                default:
                    throw new NotImplementedException();
            }
        }

        private Dictionary<Variable, Variable[]> _tupleDic = new Dictionary<Variable, Variable[]>();

        private Variable[] Unpick(Variable v)
        {
            if (!_tupleDic.ContainsKey(v))
            {
                if (v.Type.HasIntrinsicTypeOverride &&
                    v.Type.IntrinsicTypeOverride == EIntrinsicTypes.Tuple)
                {
                    var itemTypes = v.Type.Unpick();
                    var itemVars = itemTypes.Select((t, i) => new Variable(t) { Name = v.Name + "$" + i }).ToArray();
                    _tupleDic[v] = itemVars;
                    return itemVars;
                }
                else
                {
                    return new Variable[] { v };
                }
            }
            else
            {
                return _tupleDic[v];
            }
        }

        #region ILiteralVisitor Member

        public void VisitConstant(Constant constant)
        {
            Emit(ISet.LdConst(constant.ConstantValue), _curExpr, 0, constant.Type);
        }

        public void VisitVariable(Variable variable)
        {
            if (variable.Type.HasIntrinsicTypeOverride &&
                variable.Type.IntrinsicTypeOverride == EIntrinsicTypes.Tuple)
            {
                var itemVars = Unpick(variable);
                foreach (var itemVar in itemVars.Reverse())
                {
                    VisitVariable(itemVar);
                }
            }
            else
            {
                InstructionDependency[] preds;
                switch (_litMode)
                {
                    case ELiteralAcceptMode.Read:
                        preds = _storesInCurBB.Get(variable).Select(
                            _ => new OrderDependency(_, OrderDependency.EKind.BeginAfter)).ToArray();
                        _readsInCurBB.Add(variable, NextInstructionIndex);
                        Emit(ISet.LoadVar(variable), _curExpr, preds, 0, variable.Type);
                        break;

                    case ELiteralAcceptMode.Write:
                        {
                            preds = _storesInCurBB.Get(variable)
                                .Union(_readsInCurBB.Get(variable))
                                .Select(_ => new OrderDependency(_, OrderDependency.EKind.BeginAfter))
                                .ToArray();
                            if (preds.Any())
                            {
                                int idx = NextInstructionIndex;
                                Emit(DefaultInstructionSet.Instance.Nop(0), _curExpr, preds, 0);
                                preds = new InstructionDependency[] { new OrderDependency(idx, OrderDependency.EKind.BeginAfter) };
                            }
                            _storesInCurBB.Add(variable, NextInstructionIndex);
                            Debug.Assert(variable.Type.Equals(_typeStack.Peek()));
                            Emit(ISet.StoreVar(variable), _curExpr, preds, 1);
                        }
                        break;
                }
            }
        }

        public void VisitFieldRef(FieldRef fieldRef)
        {
            InstructionDependency[] preds;
            switch (_litMode)
            {
                case ELiteralAcceptMode.Read:
                    preds = _storesInCurBB.Get(fieldRef)
                        .Select(_ => new OrderDependency(_, OrderDependency.EKind.BeginAfter))
                        .ToArray();
                    _readsInCurBB.Add(fieldRef, NextInstructionIndex);
                    Emit(ISet.LoadVar(fieldRef), _curExpr, preds, 0, fieldRef.Type);
                    break;

                case ELiteralAcceptMode.Write:
                    {
                        preds = _storesInCurBB.Get(fieldRef)
                            .Union(_readsInCurBB.Get(fieldRef))
                            .Select(_ => new OrderDependency(_, OrderDependency.EKind.BeginAfter))
                            .ToArray();
                        if (preds.Any())
                        {
                            int idx = NextInstructionIndex;
                            Emit(DefaultInstructionSet.Instance.Nop(0), _curExpr, preds, 0);
                            preds = new InstructionDependency[] { new OrderDependency(idx, OrderDependency.EKind.BeginAfter) };
                        }
                        _storesInCurBB.Add(fieldRef, NextInstructionIndex);
                        Emit(ISet.StoreVar(fieldRef), _curExpr, preds, 1);
                    }
                    break;
            }
        }

        public void VisitThisRef(ThisRef thisRef)
        {
            // Everything is expected to be inlined,
            // "this" is expected to be constant
            // => nothing to do
            // however...
            throw new NotSupportedException();
        }

        public void VisitSignalRef(SignalRef signalRef)
        {
            switch (_litMode)
            {
                case ELiteralAcceptMode.Read:
                    Emit(ISet.ReadPort(signalRef.Desc), _curExpr, 0, signalRef.Type);
                    break;

                case ELiteralAcceptMode.Write:
                    Emit(ISet.WritePort(signalRef.Desc), _curExpr, 1);
                    break;
            }
        }

        private class ArrayMod
        {
            public enum EMode
            {
                HaveIndices,
                AnyIndex,
                UnknownIndex
            }

            public Array TheArray { get; private set; }
            public long[] Indices { get; private set; }
            public EMode Mode { get; private set; }

            public ArrayMod(Array array, long[] indices)
            {
                TheArray = array;
                Indices = indices;
                Mode = EMode.HaveIndices;
            }

            public ArrayMod(Array array, EMode mode)
            {
                TheArray = array;
                Mode = mode;
            }

            public override bool Equals(object obj)
            {
                ArrayMod other = obj as ArrayMod;
                if (other == null)
                    return false;

                if (other.TheArray != TheArray)
                    return false;

                switch (Mode)
                {
                    case EMode.AnyIndex:
                        return other.Mode == EMode.AnyIndex;

                    case EMode.HaveIndices:
                        return other.Mode == EMode.HaveIndices &&
                            Indices.SequenceEqual(other.Indices);

                    case EMode.UnknownIndex:
                        return other.Mode == EMode.UnknownIndex;

                default:
                        throw new NotImplementedException();
                }
            }

            public override int GetHashCode()
            {
                int hash = TheArray.GetHashCode();
                if (Mode == EMode.HaveIndices)
                    hash ^= Indices.GetSequenceHashCode();
                return hash;
            }
        }

        public void VisitArrayRef(ArrayRef arrayRef)
        {
            LiteralReference lr = arrayRef.ArrayExpr as LiteralReference;
            object arrayObj;
            if (lr == null || !lr.ReferencedObject.IsConst(out arrayObj))
                throw new NotSupportedException("Array references must be fixed (referenced array must be known in advance)!");
            Array array = arrayObj as Array;
            if (array == null)
                throw new InvalidOperationException("Not an array");

            ELiteralAcceptMode saveMode = _litMode;
            _litMode = ELiteralAcceptMode.Read;
            FixedArrayRef far = arrayRef.AsFixed();
            if (!far.IndicesConst)
            {
                foreach (Expression index in arrayRef.Indices)
                {
                    index.Accept(this);
                }
            }
            _litMode = saveMode;

            int[] preds;
            switch (_litMode)
            {
                case ELiteralAcceptMode.Read:
                    if (far.IndicesConst)
                    {
                        ArrayMod amod1 = new ArrayMod(array, far.Indices);
                        ArrayMod amod2 = new ArrayMod(array, ArrayMod.EMode.UnknownIndex);
                        ArrayMod amod3 = new ArrayMod(array, ArrayMod.EMode.AnyIndex);
                        preds = _storesInCurBB.Get(amod1)
                            .Union(_storesInCurBB.Get(amod2))
                            .Union(_storesInCurBB.Get(amod3))
                            .Union(_readsInCurBB.Get(amod3))
                            .Distinct()
                            .ToArray();
                        _readsInCurBB.Add(amod1, NextInstructionIndex);
                        _readsInCurBB.Add(amod3, NextInstructionIndex);
                        Emit(ISet.LdelemFixAFixI(far), preds, 0, arrayRef.Type);
                    }
                    else
                    {
                        ArrayMod amod1 = new ArrayMod(array, ArrayMod.EMode.UnknownIndex);
                        ArrayMod amod2 = new ArrayMod(array, ArrayMod.EMode.AnyIndex);
                        preds = _storesInCurBB.Get(amod1)
                            .Union(_storesInCurBB.Get(amod2))
                            .Union(_readsInCurBB.Get(amod2))
                            .Distinct()
                            .ToArray();
                        _readsInCurBB.Add(amod1, NextInstructionIndex);
                        Emit(ISet.LdelemFixA(far), preds, arrayRef.Indices.Length, arrayRef.Type);
                    }                    
                    break;

                case ELiteralAcceptMode.Write:
                    if (far.IndicesConst)
                    {
                        ArrayMod amod1 = new ArrayMod(array, far.Indices);
                        ArrayMod amod2 = new ArrayMod(array, ArrayMod.EMode.UnknownIndex);
                        ArrayMod amod3 = new ArrayMod(array, ArrayMod.EMode.AnyIndex);
                        preds = _storesInCurBB.Get(amod1)
                            .Union(_storesInCurBB.Get(amod3))
                            .Union(_storesInCurBB.Get(amod2))
                            .Union(_readsInCurBB.Get(amod1))
                            .Union(_readsInCurBB.Get(amod2))
                            .Union(_readsInCurBB.Get(amod3))
                            .Distinct()
                            .ToArray();
                        _storesInCurBB.Add(amod1, NextInstructionIndex);
                        _storesInCurBB.Add(amod3, NextInstructionIndex);
                        Emit(ISet.StelemFixAFixI(far), preds, 1);
                    }
                    else
                    {
                        ArrayMod amod1 = new ArrayMod(array, ArrayMod.EMode.UnknownIndex);
                        ArrayMod amod2 = new ArrayMod(array, ArrayMod.EMode.AnyIndex);
                        preds = _storesInCurBB.Get(amod1)
                            .Union(_storesInCurBB.Get(amod2))
                            .Union(_readsInCurBB.Get(amod1))
                            .Union(_readsInCurBB.Get(amod2))
                            .Distinct()
                            .ToArray();
                        _storesInCurBB.Add(amod1, NextInstructionIndex);
                        Emit(ISet.StelemFixA(far), preds, 1 + arrayRef.Indices.Length);
                    }                    
                    break;
            }
        }

        #endregion

        private void PostProcessBranchLabels()
        {
            foreach (var kvp in _stmt2label)
            {
                if (kvp.Value.InstructionIndex < 0)
                {
                    var stmt = kvp.Key.Successor;
                    var label = _stmt2label[stmt];
                    while (label.InstructionIndex < 0)
                    {
                        stmt = stmt.Successor;
                        label = _stmt2label[stmt];
                    }
                    kvp.Value.InstructionIndex = label.InstructionIndex;
                }
            }
        }

        public void Run()
        {
            Compilee.Body.Accept(this);
            PostProcessBranchLabels();
        }
    }

    public static class Compilation
    {
        public static List<XILSInstr> TrimUnreachable(ControlFlowGraph<XILSInstr> cfg)
        {
            IEnumerable<BasicBlock<XILSInstr>> reachableBBs = cfg.BasicBlocks.Where(bb => bb.IsReachable);
            List<XILSInstr> reachableInstrs = reachableBBs.SelectMany(bb => bb.Range).ToList();
            return reachableInstrs;
        }

        public static List<XILSInstr> OptimizeLocals(ControlFlowGraph<XILSInstr> cfg)
        {
            var locals = cfg.Instructions.RenumerateLocalVariables();
            int numLocals = locals.Any() ? locals.Max(l => ((Variable)l).LocalIndex) + 1 : 0;
            var dfa = new DataflowAnalyzer<XILSInstr>(cfg, numLocals)
            {
                DoNotOptimizeAcrossBasicBlocks = true
            };
            dfa.Run();
            var lva = new LocalVariableOptimizer(cfg.Instructions, dfa);
            lva.Rewrite();
            var newLocals = locals.Where(v => !dfa.IsEliminable(((Variable)v).LocalIndex)).ToList();
            return lva.OutInstructions;
        }

        public static XILSFunction Compile(this Function func,
            IInstructionSet<XILInstr> iset)
        {
            var input = func;
            Variable[] inlinedLocals;
            input = input.ConvertFieldsToLocals(out inlinedLocals);
            var comp = new Compiler(iset, input);
            comp.Run();
            var instrs = comp.Result;

            var cfg = CreateCFG(instrs);
            var reachInstrs = TrimUnreachable(cfg);
            var optInstrs = OptimizeLocals(cfg);

            var inArgds = input.InputVariables.Select(
                (v, i) => new ArgumentDescriptor(v, ArgumentDescriptor.EArgDirection.In, EVariability.ExternVariable, i));
            int inCount = inArgds.Count();
            var outArgds = input.InputVariables.Select(
                (v, i) => new ArgumentDescriptor(v, ArgumentDescriptor.EArgDirection.Out,EVariability.ExternVariable, i + inCount));
            var argds = inArgds.Union(outArgds).ToArray();
            var locals = optInstrs.RenumerateLocalVariables();
            var result = new XILSFunction(func.Name, argds, locals.ToArray(), optInstrs.ToArray());
            result.SanityCheck();
            return result;
        }

        public static XILSFunction Compile(this Function func)
        {
            return Compile(func, DefaultInstructionSet.Instance);
        }

        public static ControlFlowGraph<XIL3Instr> CreateCFG(IList<XIL3Instr> ilist)
        {
            XIL3Instr marshal = DefaultInstructionSet
                .Instance
                .ExitMarshal()
                .Create3AC(
                    DefaultInstructionSet.Empty,
                    new int[0],
                    new int[0]);
            marshal.Index = ilist.Count;
            XIL3InstructionInfo iinfo = new XIL3InstructionInfo();
            return new ControlFlowGraph<XIL3Instr>(ilist, marshal, iinfo);
        }

        public static ControlFlowGraph<XILSInstr> CreateCFG(IList<XILSInstr> ilist)
        {
            XILSInstr marshal = DefaultInstructionSet
                .Instance
                .ExitMarshal()
                .CreateStk(
                    DefaultInstructionSet.Empty,
                    new TypeDescriptor[0],
                    new TypeDescriptor[0]);
            marshal.Index = ilist.Count;
            XILSInstructionInfo iinfo = new XILSInstructionInfo();
            return new ControlFlowGraph<XILSInstr>(ilist, marshal, iinfo);
        }
    }

    public class XILSchedulingFailedException : Exception
    {
        public XILSchedulingFailedException(string reason) :
            base(reason)
        {
        }
    }
}

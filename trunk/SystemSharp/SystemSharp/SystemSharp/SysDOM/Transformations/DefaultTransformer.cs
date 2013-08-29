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
using System.Linq;
using System.Text;

namespace SystemSharp.SysDOM.Transformations
{
    public abstract class DefaultTransformer:
        AlgorithmTemplate, 
        IStatementVisitor, 
        IExpressionTransformer,
        ILiteralVisitor
    {
        private Dictionary<LoopBlock, LoopBlock> _loopMap =
            new Dictionary<LoopBlock, LoopBlock>();
        private Dictionary<CaseStatement, CaseStatement> _caseMap =
            new Dictionary<CaseStatement, CaseStatement>();
        private Dictionary<Statement, Statement> _stmtMap =
            new Dictionary<Statement, Statement>();
        private List<Tuple<GotoStatement, GotoStatement>> _gotoList =
            new List<Tuple<GotoStatement, GotoStatement>>();
        private Literal _tlit;

        protected abstract Statement Root { get; }

        protected override void DeclareAlgorithm()
        {
            Root.Accept(this);
            foreach (var tup in _gotoList)
                tup.Item2.Target = _stmtMap[tup.Item1.Target];
        }

        private void LabelLastStmt(Statement stmt)
        {
            if (stmt.Label != null)
            {
                LastStatement.Label = stmt.Label;
            }
            _stmtMap[stmt] = LastStatement;
        }

        protected virtual void CopyAttributesToLastStatement(Statement stmt)
        {
            LastStatement.CopyAttributesFrom(stmt);
        }

        public virtual void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement child in stmt.Statements)
                child.Accept(this);
        }

        public virtual void AcceptLoopBlock(LoopBlock stmt)
        {
            _loopMap[stmt] = Loop();
            {
                stmt.Body.Accept(this);
            }
            EndLoop();
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        public virtual void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            Break(_loopMap[stmt.Loop]);
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        public virtual void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            Continue(_loopMap[stmt.Loop]);
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        public virtual void AcceptIf(IfStatement stmt)
        {
            If(stmt.Conditions[0].Transform(this));
            {
                stmt.Branches[0].Accept(this);
            }
            for (int i = 1; i < stmt.Conditions.Count; i++)
            {
                ElseIf(stmt.Conditions[i].Transform(this));
                {
                    stmt.Branches[i].Accept(this);
                }
            }
            if (stmt.Branches.Count > stmt.Conditions.Count)
            {
                Else();
                {
                    stmt.Branches.Last().Accept(this);
                }
            }
            EndIf();
            CopyAttributesToLastStatement(stmt);
        }

        public virtual void AcceptCase(CaseStatement stmt)
        {
            var caseStmt = Switch(stmt.Selector.Transform(this));
            _caseMap[stmt] = caseStmt;
            {
                for (int i = 0; i < stmt.Cases.Count; i++)
                {
                    Case(stmt.Cases[i].Transform(this));
                    {
                        stmt.Branches[i].Accept(this);
                    }
                    EndCase();
                }
            }
            if (stmt.Branches.Count > stmt.Cases.Count)
            {
                DefaultCase();
                {
                    stmt.Branches.Last().Accept(this);
                }
                EndCase();
            }
            EndSwitch();
            CopyAttributesToLastStatement(stmt);
        }

        public virtual void AcceptStore(StoreStatement stmt)
        {
            if (stmt.Container is Literal)
                _tlit = (Literal)stmt.Container;
            stmt.Container.Accept(this);
            Store((IStorableLiteral)_tlit, stmt.Value.Transform(this));
            CopyAttributesToLastStatement(stmt);
        }

        public virtual void AcceptNop(NopStatement stmt)
        {
            Nop();
            LabelLastStmt(stmt);
        }

        public virtual void AcceptSolve(SolveStatement stmt)
        {
            throw new NotImplementedException();
        }

        public virtual void AcceptBreakCase(BreakCaseStatement stmt)
        {
            Break(_caseMap[stmt.CaseStmt]);
            CopyAttributesToLastStatement(stmt);
        }

        public virtual void AcceptGotoCase(GotoCaseStatement stmt)
        {
            GotoCase(_caseMap[stmt.CaseStmt], stmt.TargetIndex);
            CopyAttributesToLastStatement(stmt);
        }

        public virtual void AcceptGoto(GotoStatement stmt)
        {
            GotoStatement gts = Goto();
            //if (stmt.Target != null)
            //    gts.Target = _stmtMap[stmt.Target];
            _gotoList.Add(Tuple.Create(stmt, gts));
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        public virtual void AcceptReturn(ReturnStatement stmt)
        {
            if (stmt.ReturnValue != null)
                Return(stmt.ReturnValue.Transform(this));
            else
                Return();
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        public virtual void AcceptThrow(ThrowStatement stmt)
        {
            Throw(stmt.ThrowExpr.Transform(this));
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        public virtual void AcceptCall(CallStatement stmt)
        {
            Call(stmt.Callee, 
                stmt.Arguments.Select(a => a.Transform(this)).ToArray());
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        public virtual Expression TransformLiteralReference(LiteralReference expr)
        {
            var lit = expr.ReferencedObject;
            lit.Accept(this);
            var result = new LiteralReference(_tlit, expr.Mode);
            result.CopyAttributesFrom(expr);
            return result;
        }

        public virtual Expression TransformSpecialConstant(SpecialConstant expr)
        {
            return expr;
        }

        public virtual Expression TransformUnOp(UnOp expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        public virtual Expression TransformBinOp(BinOp expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        public virtual Expression TransformTernOp(TernOp expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        public virtual Expression TransformFunction(FunctionCall expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        public virtual void VisitConstant(Constant constant)
        {
            _tlit = constant;
        }

        public virtual void VisitVariable(Variable variable)
        {
            _tlit = variable;
        }

        public virtual void VisitFieldRef(FieldRef fieldRef)
        {
            _tlit = fieldRef;
        }

        public virtual void VisitThisRef(ThisRef thisRef)
        {
            _tlit = thisRef;
        }

        public virtual void VisitSignalRef(SignalRef signalRef)
        {
            _tlit = signalRef;
        }

        public virtual void VisitArrayRef(ArrayRef arrayRef)
        {
            var newRef = new ArrayRef(
                arrayRef.ArrayExpr.Transform(this),
                arrayRef.Type,
                arrayRef.Indices.Select(i => i.Transform(this)).ToArray());
            newRef.CopyAttributesFrom(arrayRef);
            _tlit = newRef;
        }

        protected void SetCurrentLiteral(Literal lit)
        {
            _tlit = lit;
        }
    }
}

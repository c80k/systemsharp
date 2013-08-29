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
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SystemSharp.SysDOM.Transformations
{
    class RetRemover: 
        AlgorithmTemplate, 
        IStatementVisitor
    {
        private Dictionary<LoopBlock, LoopBlock> _loopMap =
            new Dictionary<LoopBlock, LoopBlock>();
        private Dictionary<CaseStatement, CaseStatement> _caseMap =
            new Dictionary<CaseStatement, CaseStatement>();
        private Dictionary<Statement, Statement> _stmtMap =
            new Dictionary<Statement, Statement>();
        private Statement _root;
        private List<GotoStatement> _gotoList = new List<GotoStatement>();
        private List<GotoStatement> _gotoEndList = new List<GotoStatement>();
        private Statement _lastStatement;

        public Variable ReturnVariable { get; private set; }

        public RetRemover(Statement root)
        {
            _root = root;
            IList<Statement> slist = root.AsStatementList();
            if (slist.Count > 0)
                _lastStatement = slist.Last();
        }

        private void PostProcess()
        {
            foreach (GotoStatement gts in _gotoList)
                gts.Target = _stmtMap[gts];

            Nop();
            foreach (GotoStatement gts in _gotoEndList)
                gts.Target = LastStatement;
        }

        protected override void DeclareAlgorithm()
        {
            _root.Accept(this);
            PostProcess();
        }

        private void LabelLastStmt(Statement stmt)
        {
            if (stmt.Label != null)
            {
                LastStatement.Label = stmt.Label;
            }
            _stmtMap[stmt] = LastStatement;
        }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement child in stmt.Statements)
                child.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            _loopMap[stmt] = Loop();
            {
                stmt.Body.Accept(this);
            }
            EndLoop();
            LabelLastStmt(stmt);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            Break(_loopMap[stmt.Loop]);
            LabelLastStmt(stmt);
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            Continue(_loopMap[stmt.Loop]);
            LabelLastStmt(stmt);
        }

        public void AcceptIf(IfStatement stmt)
        {
            If(stmt.Conditions[0]);
            {
                stmt.Branches[0].Accept(this);
            }
            for (int i = 1; i < stmt.Conditions.Count; i++)
            {
                ElseIf(stmt.Conditions[i]);
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
        }

        public void AcceptCase(CaseStatement stmt)
        {
            Switch(stmt.Selector);
            {
                for (int i = 0; i < stmt.Cases.Count; i++)
                {
                    Case(stmt.Cases[i]);
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
        }

        public void AcceptStore(StoreStatement stmt)
        {
            Store(stmt.Container, stmt.Value);
        }

        public void AcceptNop(NopStatement stmt)
        {
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            throw new NotImplementedException();
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            Break(_caseMap[stmt.CaseStmt]);
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            GotoCase(_caseMap[stmt.CaseStmt], stmt.TargetIndex);
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            GotoStatement gts = Goto();
            gts.Target = stmt.Target;
            _gotoList.Add(gts);
            LabelLastStmt(stmt);
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            if (stmt.ReturnValue != null)
            {
                if (ReturnVariable == null)
                {
                    ReturnVariable = new Variable(stmt.ReturnValue.ResultType)
                    {
                        Name = "$retval"
                    };
                }
                Store(ReturnVariable, stmt.ReturnValue);
            }
            if (stmt != _lastStatement)
            {
                GotoStatement gts = Goto();
                _gotoEndList.Add(gts);
            }
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            Throw(stmt.ThrowExpr);
            LabelLastStmt(stmt);
        }

        public void AcceptCall(CallStatement stmt)
        {
            Call(stmt.Callee, stmt.Arguments);
            LabelLastStmt(stmt);
        }
    }

    public static class RetRemoval
    {
        public static Statement RemoveRets(this Statement stmt, out Variable returnVariable)
        {
            Statement work = stmt.Clone;
            work.RemoveNops();
            RetRemover rr = new RetRemover(work);
            Function result = rr.GetAlgorithm();
            returnVariable = rr.ReturnVariable;
            return result.Body;
        }
    }
}

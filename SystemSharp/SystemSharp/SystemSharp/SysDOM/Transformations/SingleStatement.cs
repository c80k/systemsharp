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
    class SingleStatementGetter: IStatementVisitor
    {
        public Statement Result { get; private set; }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            if (stmt.Statements.Count == 1)
            {
                stmt.Statements.Single().Accept(this);
            }
            else
            {
                Result = null;
            }
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            Result = stmt;
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptIf(IfStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptCase(CaseStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptStore(StoreStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptNop(NopStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            Result = stmt;
        }

        public void AcceptCall(CallStatement stmt)
        {
            Result = stmt;
        }
    }

    class StatementListGetter : IStatementVisitor
    {
        public IList<Statement> Result { get; private set; }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            Result = stmt.Statements;
        }

        private void SimpleResult(Statement stmt)
        {
            Result = new Statement[] { stmt };
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptIf(IfStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptNop(NopStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            SimpleResult(stmt);
        }

        public void AcceptCall(CallStatement stmt)
        {
            SimpleResult(stmt);
        }
    }

    public static class SingleStatements
    {
        public static Statement AsSingleStatement(this Statement stmt)
        {
            SingleStatementGetter ssg = new SingleStatementGetter();
            stmt.Accept(ssg);
            return ssg.Result;
        }

        public static IList<Statement> AsStatementList(this Statement stmt)
        {
            StatementListGetter slg = new StatementListGetter();
            stmt.Accept(slg);
            return slg.Result;
        }
    }
}

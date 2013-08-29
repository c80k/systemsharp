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
    class StatementRemover: IStatementVisitor
    {
        public Statement Match { get; set; }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            stmt.Statements.Accept(this);
            List<Statement> list = new List<Statement>(
                stmt.Statements.Where(s => !s.Equals(Match)));
            stmt.Statements.Clear();
            stmt.Statements.AddRange(list);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body.Accept(this);
            if (stmt.Trailer != null)
                stmt.Trailer.Accept(this);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
        }

        public void AcceptIf(IfStatement stmt)
        {
            stmt.Branches.Accept(this);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            stmt.Branches.Accept(this);
        }

        public void AcceptStore(StoreStatement stmt)
        {
        }

        public void AcceptNop(NopStatement stmt)
        {
        }

        public void AcceptSolve(SolveStatement stmt)
        {
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
        }

        public void AcceptGoto(GotoStatement stmt)
        {
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
        }

        public void AcceptCall(CallStatement stmt)
        {
        }

        #endregion
    }

    public static class StatementRemoval
    {
        public static void RemoveAll(this Statement stmt, Statement toRemove)
        {
            StatementRemover sr = new StatementRemover()
            {
                Match = toRemove
            };
            stmt.Accept(sr);
        }
    }
}

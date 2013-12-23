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
using System.Linq;
using System.Text;

namespace SystemSharp.SysDOM.Transformations
{
    class HierarchyAnalyzer : IStatementVisitor
    {
        public Statement Grandchild { get; set; }
        public bool IsAncestor { get; private set; }

        private void Check(Statement stmt)
        {
            if (stmt == Grandchild)
            {
                IsAncestor = true;
                return;
            }
        }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            Check(stmt);
            stmt.Statements.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            Check(stmt);
            stmt.Body.Accept(this);
            if (stmt.Trailer != null)
                stmt.Trailer.Accept(this);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptIf(IfStatement stmt)
        {
            Check(stmt);
            stmt.Branches.Accept(this);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            Check(stmt);
            stmt.Branches.Accept(this);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptNop(NopStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            Check(stmt);
        }

        public void AcceptCall(CallStatement stmt)
        {
            Check(stmt);
        }

        #endregion
    }

    /// <summary>
    /// This static class provides a service for determining the containment relationship between statements.
    /// </summary>
    public static class HierarchyAnalysis
    {
        /// <summary>
        /// Determines whether a given statement is an ancestor of some other statement.
        /// </summary>
        /// <param name="stmt">An assumed ancestor</param>
        /// <param name="grandChild">Its assumed granchild</param>
        /// <returns><c>true</c> if <paramref name="stmt"/> is an ancestor of <paramref name="grandChild"/></returns>
        /// <remarks>A statement a is an ancestor of a statement b iff there exists a sequence
        /// s1, s2,... , sN of zero or more statements such that a contains s1, s1 contains s2,
        /// ... and sN contains b. Furthermore, each statement is per definition an ancestor
        /// of itself.
        /// </remarks>
        public static bool IsAncestor(this Statement stmt, Statement grandChild)
        {
            HierarchyAnalyzer ha = new HierarchyAnalyzer()
            {
                Grandchild = grandChild
            };
            stmt.Accept(ha);
            return ha.IsAncestor;
        }
    }
}

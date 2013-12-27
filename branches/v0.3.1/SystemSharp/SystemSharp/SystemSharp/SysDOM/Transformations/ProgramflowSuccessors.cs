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
    class InnermostAtomicStatementExtractor: IStatementVisitor
    {
        public Statement Result { get; private set; }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            if (stmt.Statements.Count > 0)
                stmt.Statements.First().Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body.Accept(this);
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

        #endregion
    }

    class ProgramflowSuccessorsGetter: IStatementVisitor
    {
        public IEnumerable<Statement> Result { get; private set; }

        private void SingleResult(Statement result)
        {
            Result = new Statement[] { result };
        }

        private Statement GetAtomicSuccessor(Statement stmt)
        {
            do
            {
                Statement succ = stmt.Successor;
                if (succ == null)
                    return null;
                Statement asucc = succ.GetInnermostAtomicStatement();
                if (asucc != null)
                    return asucc;
                stmt = succ;
            } while (true);
        }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            throw new InvalidOperationException();
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            throw new InvalidOperationException();            
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            SingleResult(stmt.Successor);
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            SingleResult(stmt.Loop.Body.GetInnermostAtomicStatement());
        }

        public void AcceptIf(IfStatement stmt)
        {
            List<Statement> result = new List<Statement>();
            foreach (Statement branch in stmt.Branches)
            {
                Statement abranch = branch.GetInnermostAtomicStatement();
                if (abranch != null)
                    result.Add(abranch);
            }
            Statement succ = GetAtomicSuccessor(stmt);
            if (succ != null)
                result.Add(succ);
            Result = result;
        }

        public void AcceptCase(CaseStatement stmt)
        {
            List<Statement> result = new List<Statement>();
            foreach (Statement branch in stmt.Branches)
            {
                Statement abranch = branch.GetInnermostAtomicStatement();
                if (abranch != null)
                    result.Add(abranch);
            }
            Statement succ = GetAtomicSuccessor(stmt);
            if (succ != null)
                result.Add(succ);
            Result = result;
        }

        public void AcceptStore(StoreStatement stmt)
        {
            SingleResult(GetAtomicSuccessor(stmt));
        }

        public void AcceptNop(NopStatement stmt)
        {
            SingleResult(GetAtomicSuccessor(stmt));
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            SingleResult(GetAtomicSuccessor(stmt));
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            SingleResult(GetAtomicSuccessor(stmt.CaseStmt));
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            Statement target = stmt.CaseStmt.Branches[stmt.TargetIndex];
            SingleResult(target.GetInnermostAtomicStatement());
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            SingleResult(stmt.Target.GetInnermostAtomicStatement());
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            Result = new Statement[0];
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            Result = new Statement[0];
        }

        public void AcceptCall(CallStatement stmt)
        {
            SingleResult(GetAtomicSuccessor(stmt));
        }

        #endregion
    }

    /// <summary>
    /// This static class provides a service for retrieving all possible program flow successors of a statement.
    /// </summary>
    public static class ProgramflowSuccessorsRetrieval
    {
        /// <summary>
        /// Returns the set of direct successors of a statement.
        /// </summary>
        /// <remarks>
        /// This operation is only valid on atomic statements, that is all statement types except <c>CompoundStatement</c> and 
        /// <c>LoopBlock</c>.
        /// Furthermore, only atomic statements constitute the result enumeration.
        /// </remarks>
        /// <param name="stmt">statement whose program flow successors shall be retrieved</param>
        /// <returns>an enumeration of all possible program flow successors</returns>
        public static IEnumerable<Statement> GetProgramflowSucessors(Statement stmt)
        {
            ProgramflowSuccessorsGetter psg = new ProgramflowSuccessorsGetter();
            stmt.Accept(psg);
            return psg.Result;
        }
    }

    class AtomicStatementExtractor: IStatementVisitor
    {
        private List<Statement> _result = new List<Statement>();

        public List<Statement> Result
        {
            get { return _result; }
        }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            stmt.Statements.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body.Accept(this);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptIf(IfStatement stmt)
        {
            _result.Add(stmt);
            stmt.Branches.Accept(this);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            _result.Add(stmt);
            stmt.Branches.Accept(this);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptNop(NopStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            _result.Add(stmt);
        }

        public void AcceptCall(CallStatement stmt)
        {
            _result.Add(stmt);
        }

        #endregion
    }

    /// <summary>
    /// This static class provides services for extracting all atomic statements from a statement.
    /// </summary>
    /// <remarks>
    /// A statement is considered "atomic" if it cannot be described by a composition of two or more statements.
    /// E.g. a <c>StoreStatement</c> and a <c>CallStatement</c> are atomic, since they cannot be sub-divided into
    /// smaller statements. A <c>CompoundStatement</c> is not atomic, since it consists of smaller statements.
    /// Similarly, a <c>LoopStatement</c> is not atomic, since it consists of a body (of course, the body may be an 
    /// atomic statement, but does not have to). <c>IfStatement</c> and <c>CaseStatement</c> are somewhat tricky,
    /// since they rely on a condition which must be considered atomic. However, their inner branches consitute
    /// further atomic statements. Therefore, those statements are considered atomic, and each inner branch is
    /// analyzed as well.
    /// </remarks>
    public static class AtomicStatementExtraction
    {
        /// <summary>
        /// Extracts the first innermost atomic statement inside a given statement.
        /// </summary>
        /// <param name="stmt"></param>
        /// <returns></returns>
        public static Statement GetInnermostAtomicStatement(this Statement stmt)
        {
            InnermostAtomicStatementExtractor ase = new InnermostAtomicStatementExtractor();
            stmt.Accept(ase);
            return ase.Result;
        }

        /// <summary>
        /// Extracts all atomic statements inside a given statement.
        /// </summary>
        /// <param name="stmt"></param>
        /// <returns>The list of atomic statements, the first entry being the entry.</returns>
        public static List<Statement> GetAtomicStatements(this Statement stmt)
        {
            AtomicStatementExtractor ase = new AtomicStatementExtractor();
            stmt.Accept(ase);
            return ase.Result;
        }
    }
}

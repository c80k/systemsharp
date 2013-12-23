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
    /** FIXME: The current implementation is not capable of detecting modifications of variables which are referenced as "out" arguments of some method!
     * */

    class VariableModificationDetector : IStatementVisitor
    {
        private IStorable _variable;

        public bool Result { get; private set; }

        public VariableModificationDetector(IStorable variable)
        {
            _variable = variable;
        }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement child in stmt.Statements)
                child.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body.Accept(this);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
        }

        public void AcceptIf(IfStatement stmt)
        {
            foreach (Statement branch in stmt.Branches)
                branch.Accept(this);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            foreach (Statement branch in stmt.Branches)
                branch.Accept(this);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            if (_variable.Equals(stmt.Container))
                Result = true;
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
    }

    /// <summary>
    /// This static class provides a service to detect whether a statement modifies a certain variable.
    /// </summary>
    public static class VariableModificationDetection
    {
        /// <summary>
        /// Returns <c>true</c> if the given statement modifies the given variable.
        /// </summary>
        /// <remarks>
        /// The current implementation is not capable of detecting modifications of variables which are referenced as "out" arguments of some method.
        /// </remarks>
        /// <param name="stmt">statement</param>
        /// <param name="variable">variable</param>
        public static bool Modifies(this Statement stmt, IStorable variable)
        {
            VariableModificationDetector vmd = new VariableModificationDetector(variable);
            stmt.Accept(vmd);
            return vmd.Result;
        }
    }
}

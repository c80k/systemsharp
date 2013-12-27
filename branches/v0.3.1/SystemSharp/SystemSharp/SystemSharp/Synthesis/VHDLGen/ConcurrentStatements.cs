/**
 * Copyright 2012 Christian Köllner
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
using SystemSharp.Meta;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.Synthesis.VHDLGen
{
    /// <summary>
    /// Models a VHDL concurrent statement.
    /// </summary>
    public class ConcurrentStatement
    {
        /// <summary>
        /// Signal transfer target.
        /// </summary>
        public SignalRef TargetSignal { get; private set; }

        /// <summary>
        /// Transfer expression.
        /// </summary>
        public Expression SourceExpression { get; private set; }

        /// <summary>
        /// Constructs a concurrent statement.
        /// </summary>
        /// <param name="targetSignal">signal transfer target</param>
        /// <param name="sourceExpression">transfer expression</param>
        public ConcurrentStatement(SignalRef targetSignal, Expression sourceExpression)
        {
            TargetSignal = targetSignal;
            SourceExpression = sourceExpression;
        }
    }

    class ConcurrentStatementExtractor : DefaultTransformer
    {
        private Statement _root;

        public ConcurrentStatementExtractor(Statement root)
        {
            _root = root;
            Success = true;
            Statements = new List<ConcurrentStatement>();
        }

        public bool Success { get; private set; }
        public List<ConcurrentStatement> Statements { get; private set; }

        protected override Statement Root
        {
            get { return _root; }
        }

        public override void AcceptCall(CallStatement stmt)
        {
            Success = false;
        }

        public override void AcceptCase(CaseStatement stmt)
        {
            Success = false;
        }

        public override void AcceptGoto(GotoStatement stmt)
        {
            Success = false;
        }

        public override void AcceptIf(IfStatement stmt)
        {
            Success = false;
        }

        public override void AcceptLoopBlock(LoopBlock stmt)
        {
            Success = false;
        }

        public override void AcceptSolve(SolveStatement stmt)
        {
            Success = false;
        }

        public override void AcceptStore(StoreStatement stmt)
        {
            var sref = stmt.Container as SignalRef;
            if (sref == null)
            {
                Success = false;
                return;
            }
            if (sref.Prop != SignalRef.EReferencedProperty.Next)
            {
                Success = false;
                return;
            }
            var cstmt = new ConcurrentStatement(sref, stmt.Value);
            Statements.Add(cstmt);
        }

        public override void AcceptReturn(ReturnStatement stmt)
        {
            Success = false;
        }

        public override void AcceptThrow(ThrowStatement stmt)
        {
            Success = false;
        }

        public void Run()
        {
            Reset();
            DeclareAlgorithm();
        }
    }

    /// <summary>
    /// This static class provides an extension method for converting a SysDOM function to concurrent statements.
    /// </summary>
    public static class ConcurrentStatementExtraction
    {
        /// <summary>
        /// Tries to convert the function to concurrent statements.
        /// </summary>
        /// <param name="func">function to convert</param>
        /// <param name="statements">out parameter to receive a list of concurrent statements</param>
        /// <returns><c>true</c> if conversion was successful, <c>false</c> if function does not match the concurrent
        /// statements pattern.</returns>
        public static bool TryAsConcurrentStatements(this Function func, out List<ConcurrentStatement> statements)
        {
            statements = null;
            if (func.InputVariables.Any() ||
                func.OutputVariables.Any())
                return false;

            var xform = new ConcurrentStatementExtractor(func.Body);
            xform.Run();
            if (!xform.Success)
                return false;

            statements = xform.Statements;
            return true;
        }
    }
}

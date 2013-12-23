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
    /// <summary>
    /// Provides the infrastructure for statement-level SysDOM-to-SysDOM transformations.
    /// The default implementation clones any statement and takes care of remapping branch labels.
    /// </summary>
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

        /// <summary>
        /// Returns the root statement to transform.
        /// </summary>
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

        /// <summary>
        /// Copies all attributes of the given statement to the last output statement.
        /// </summary>
        /// <param name="stmt">statement to copy attributes from</param>
        protected virtual void CopyAttributesToLastStatement(Statement stmt)
        {
            LastStatement.CopyAttributesFrom(stmt);
        }

        /// <summary>
        /// Transforms a compound statement. The default implementation re-directs to its child statements.
        /// </summary>
        /// <param name="stmt">compount statement</param>
        public virtual void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement child in stmt.Statements)
                child.Accept(this);
        }

        /// <summary>
        /// Transforms a loop block. The default implementation clones the loop.
        /// </summary>
        /// <param name="stmt">loop block statement</param>
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

        /// <summary>
        /// Transforms a loop break statement. The default implementation places a new break loop
        /// statement inside the current loop.
        /// </summary>
        /// <param name="stmt">loop break statement</param>
        public virtual void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            Break(_loopMap[stmt.Loop]);
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        /// <summary>
        /// Transforms a loop continue statement. The default implementation places a new continue loop
        /// statement inside the current loop.
        /// </summary>
        /// <param name="stmt"></param>
        public virtual void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            Continue(_loopMap[stmt.Loop]);
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        /// <summary>
        /// Transforms an "if" statement. The default implementation clones that statement.
        /// </summary>
        /// <param name="stmt">"if" statement</param>
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

        /// <summary>
        /// Transforms a "case" statement. The default implementation clones that statement.
        /// </summary>
        /// <param name="stmt">"case" statement</param>
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

        /// <summary>
        /// Transforms a "store" statement. The default implementation clones that statement.
        /// </summary>
        /// <param name="stmt">"store" statement</param>
        public virtual void AcceptStore(StoreStatement stmt)
        {
            if (stmt.Container is Literal)
                _tlit = (Literal)stmt.Container;
            stmt.Container.Accept(this);
            Store((IStorableLiteral)_tlit, stmt.Value.Transform(this));
            CopyAttributesToLastStatement(stmt);
        }

        /// <summary>
        /// Transforms a "nop" statement. The default implementation clones that statement.
        /// </summary>
        /// <param name="stmt">"nop" statement</param>
        public virtual void AcceptNop(NopStatement stmt)
        {
            Nop();
            LabelLastStmt(stmt);
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        /// <exception cref="NotImplementedException">always thrown</exception>
        public virtual void AcceptSolve(SolveStatement stmt)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Transforms a "break case" statement. The default implementation places a new "break case"
        /// statement inside the current case selection.
        /// </summary>
        /// <param name="stmt">"break case" statement</param>
        public virtual void AcceptBreakCase(BreakCaseStatement stmt)
        {
            Break(_caseMap[stmt.CaseStmt]);
            CopyAttributesToLastStatement(stmt);
        }

        /// <summary>
        /// Transforms a "goto case" statement. The default implementation places a new "goto case"
        /// statement inside the current case selection.
        /// </summary>
        /// <param name="stmt">"goto case" statement</param>
        public virtual void AcceptGotoCase(GotoCaseStatement stmt)
        {
            GotoCase(_caseMap[stmt.CaseStmt], stmt.TargetIndex);
            CopyAttributesToLastStatement(stmt);
        }

        /// <summary>
        /// Transforms a "goto" statement. The default implementation places a new "goto" statement
        /// at the current output position.
        /// </summary>
        /// <param name="stmt">"goto" statement</param>
        public virtual void AcceptGoto(GotoStatement stmt)
        {
            GotoStatement gts = Goto();
            //if (stmt.Target != null)
            //    gts.Target = _stmtMap[stmt.Target];
            _gotoList.Add(Tuple.Create(stmt, gts));
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        /// <summary>
        /// Transforms a "return" statement. The default implementation places a new "return" statement
        /// at the current output position.
        /// </summary>
        /// <param name="stmt">"return" statement</param>
        public virtual void AcceptReturn(ReturnStatement stmt)
        {
            if (stmt.ReturnValue != null)
                Return(stmt.ReturnValue.Transform(this));
            else
                Return();
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        /// <summary>
        /// Transforms a "throw" statement. The default implementation places a new "return" statement
        /// at the current output position.
        /// </summary>
        /// <param name="stmt">"throw" statement</param>
        public virtual void AcceptThrow(ThrowStatement stmt)
        {
            Throw(stmt.ThrowExpr.Transform(this));
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        /// <summary>
        /// Transforms a "call" statement. The default implementation clones the call.
        /// </summary>
        /// <param name="stmt">"call" statement</param>
        public virtual void AcceptCall(CallStatement stmt)
        {
            Call(stmt.Callee, 
                stmt.Arguments.Select(a => a.Transform(this)).ToArray());
            CopyAttributesToLastStatement(stmt);
            LabelLastStmt(stmt);
        }

        /// <summary>
        /// Transforms a literal reference expression. The default implementation hands over to a literal visitor
        /// and constructs a new literal reference based on the last literal, which can be modified using <c>SetCurrentLiteral</c>.
        /// </summary>
        /// <param name="expr">literal reference</param>
        /// <returns>transformation result</returns>
        public virtual Expression TransformLiteralReference(LiteralReference expr)
        {
            var lit = expr.ReferencedObject;
            lit.Accept(this);
            var result = new LiteralReference(_tlit, expr.Mode);
            result.CopyAttributesFrom(expr);
            return result;
        }

        /// <summary>
        /// Transforms a special constant expression. The default implementation clones it.
        /// </summary>
        /// <param name="expr">special constant expression</param>
        /// <returns>transformation result</returns>
        public virtual Expression TransformSpecialConstant(SpecialConstant expr)
        {
            return expr;
        }

        /// <summary>
        /// Transforms a unary expression. The default implementation clones it.
        /// </summary>
        /// <param name="expr">unary expression</param>
        /// <returns>transformation result</returns>
        public virtual Expression TransformUnOp(UnOp expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        /// <summary>
        /// Transforms a binary expression. The default implementation clones it.
        /// </summary>
        /// <param name="expr">binary expression</param>
        /// <returns>transformation result</returns>
        public virtual Expression TransformBinOp(BinOp expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        /// <summary>
        /// Transforms a ternary expression. The default implementation clones it.
        /// </summary>
        /// <param name="expr">ternary expression</param>
        /// <returns>transformation result</returns>
        public virtual Expression TransformTernOp(TernOp expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        /// <summary>
        /// Transforms a function call expression. The default implementation clones it.
        /// </summary>
        /// <param name="expr">function call expression</param>
        /// <returns>transformation result</returns>
        public virtual Expression TransformFunction(FunctionCall expr)
        {
            return expr.CloneThis(expr.Children.Select(e => e.Transform(this)).ToArray());
        }

        /// <summary>
        /// Visits a constant literal. The default implementation saves it to <c>_tlit</c>.
        /// </summary>
        /// <param name="constant">constant literal</param>
        public virtual void VisitConstant(Constant constant)
        {
            _tlit = constant;
        }

        /// <summary>
        /// Visits a variable literal. The default implementation saves it to <c>_tlit</c>.
        /// </summary>
        /// <param name="variable">variable literal</param>
        public virtual void VisitVariable(Variable variable)
        {
            _tlit = variable;
        }

        /// <summary>
        /// Visits a field reference literal. The default implementation saves it to <c>_tlit</c>.
        /// </summary>
        /// <param name="fieldRef">field reference literal</param>
        public virtual void VisitFieldRef(FieldRef fieldRef)
        {
            _tlit = fieldRef;
        }

        /// <summary>
        /// Visits the "this" reference literal. The default implementation saves it to <c>_tlit</c>.
        /// </summary>
        /// <param name="thisRef">"this" reference literal</param>
        public virtual void VisitThisRef(ThisRef thisRef)
        {
            _tlit = thisRef;
        }

        /// <summary>
        /// Visits a signal reference literal. The default implementation saves it to <c>_tlit</c>.
        /// </summary>
        /// <param name="signalRef">signal reference literal</param>
        public virtual void VisitSignalRef(SignalRef signalRef)
        {
            _tlit = signalRef;
        }

        /// <summary>
        /// Visits an array reference literal. The default implementation saves it to <c>_tlit</c>.
        /// </summary>
        /// <param name="arrayRef">array reference literal.</param>
        public virtual void VisitArrayRef(ArrayRef arrayRef)
        {
            var newRef = new ArrayRef(
                arrayRef.ArrayExpr.Transform(this),
                arrayRef.Type,
                arrayRef.Indices.Select(i => i.Transform(this)).ToArray());
            newRef.CopyAttributesFrom(arrayRef);
            _tlit = newRef;
        }

        /// <summary>
        /// Changes the literal to use for the default implementation of <c>TransformLiteralReference</c>.
        /// </summary>
        /// <param name="lit"></param>
        protected void SetCurrentLiteral(Literal lit)
        {
            _tlit = lit;
        }
    }
}

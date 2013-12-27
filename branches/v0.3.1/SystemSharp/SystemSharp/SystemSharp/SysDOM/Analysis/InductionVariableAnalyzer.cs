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
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.DataTypes;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Eval;
using SystemSharp.SysDOM.Transformations;
using SystemSharp.Analysis;

namespace SystemSharp.SysDOM.Analysis
{
    class Forifier : IStatementVisitor
    {
        public Statement Result { get; private set; }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            LoopBlock forLoop = stmt.AsForLoop(EForLoopLevel.Strict);
            if (forLoop != null)
            {
                Result = forLoop;
                if (forLoop.Trailer != null)
                    forLoop.Trailer = forLoop.Trailer.Forify();
            }
            else
            {
                for (int i = 0; i < stmt.Statements.Count; i++)
                {
                    stmt.Statements[i] = stmt.Statements[i].Forify();
                }
                Result = stmt;
            }
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body = stmt.Body.Forify();
            if (stmt.Trailer != null)
                stmt.Trailer = stmt.Trailer.Forify();
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
            for (int i = 0; i < stmt.Branches.Count; i++)
                stmt.Branches[i] = stmt.Branches[i].Forify();
            Result = stmt;
        }

        public void AcceptCase(CaseStatement stmt)
        {
            for (int i = 0; i < stmt.Branches.Count; i++)
                stmt.Branches[i] = stmt.Branches[i].Forify();
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

    static class Forification
    {
        public static Statement Forify(this Statement stmt)
        {
            Forifier forifier = new Forifier();
            stmt.Accept(forifier);
            return forifier.Result;
        }
    }

    class IVRange
    {
        public long MinValue { get; private set; }
        public long MaxValue { get; private set; }

        public IVRange(long minValue, long maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        // +

        public static IVRange operator +(long v, IVRange r)
        {
            return new IVRange(r.MinValue + v, r.MaxValue + v);
        }

        public static IVRange operator +(int v, IVRange r)
        {
            return new IVRange(r.MinValue + v, r.MaxValue + v);
        }

        public static IVRange operator +(IVRange r, long v)
        {
            return new IVRange(r.MinValue + v, r.MaxValue + v);
        }

        public static IVRange operator +(IVRange r, int v)
        {
            return new IVRange(r.MinValue + v, r.MaxValue + v);
        }

        public static IVRange operator +(IVRange r1, IVRange r2)
        {
            return new IVRange(
                r1.MinValue + r2.MinValue, 
                r1.MaxValue + r2.MaxValue);
        }

        // -

        public static IVRange operator -(long v, IVRange r)
        {
            return new IVRange(v - r.MinValue, v - r.MaxValue);
        }

        public static IVRange operator -(int v, IVRange r)
        {
            return new IVRange(v - r.MinValue, v - r.MaxValue);
        }

        public static IVRange operator -(IVRange r, long v)
        {
            return new IVRange(r.MinValue - v, r.MaxValue - v);
        }

        public static IVRange operator -(IVRange r, int v)
        {
            return new IVRange(r.MinValue - v, r.MaxValue - v);
        }

        public static IVRange operator -(IVRange r1, IVRange r2)
        {
            return new IVRange(r1.MinValue - r2.MaxValue,
                r1.MaxValue - r2.MinValue);
        }

        // *

        public static IVRange operator *(long v, IVRange r)
        {
            long a = v * r.MinValue;
            long b = v * r.MaxValue;
            return new IVRange(Math.Min(a, b), Math.Max(a, b));
        }

        public static IVRange operator *(int v, IVRange r)
        {
            long a = v * r.MinValue;
            long b = v * r.MaxValue;
            return new IVRange(Math.Min(a, b), Math.Max(a, b));
        }

        public static IVRange operator *(IVRange r, long v)
        {
            long a = v * r.MinValue;
            long b = v * r.MaxValue;
            return new IVRange(Math.Min(a, b), Math.Max(a, b));
        }

        public static IVRange operator *(IVRange r, int v)
        {
            long a = v * r.MinValue;
            long b = v * r.MaxValue;
            return new IVRange(Math.Min(a, b), Math.Max(a, b));
        }

        public static IVRange operator *(IVRange r1, IVRange r2)
        {
            long a = r1.MinValue * r2.MinValue;
            long b = r1.MinValue * r2.MaxValue;
            long c = r1.MaxValue * r2.MinValue;
            long d = r1.MaxValue * r2.MaxValue;
            return new IVRange(
                Math.Min(Math.Min(a, b), Math.Min(c, d)),
                Math.Min(Math.Max(a, b), Math.Max(c, d)));
        }

        public static IVRange Max(IVRange r1, IVRange r2)
        {
            return new IVRange(
                Math.Min(r1.MinValue, r2.MinValue),
                Math.Max(r1.MaxValue, r2.MaxValue));
        }

        public static IVRange ToRange(object obj)
        {
            if (obj is IVRange)
                return (IVRange)obj;
            else
            {
                long v = TypeConversions.ToLong(obj);
                return new IVRange(v, v);
            }
        }
    }

    /// <summary>
    /// Analyzes a SysDOM function for control variables inside loops and tries to infer their value range.
    /// </summary>
    public class InductionVariableAnalyzer
    {
        class StatementVisitor : IStatementVisitor
        {
            private InductionVariableAnalyzer _iva;
            private DefaultEvaluator _eval = new DefaultEvaluator();
            private Stack<Variable> _ivStack = new Stack<Variable>();
            private ExpressionConstRules _ecr = new ExpressionConstRules();

            public StatementVisitor(InductionVariableAnalyzer iva)
            {
                _iva = iva;
                _eval.DoEvalVariable = EvaluateVariable;
                //_eval.DoEvalFieldRef = x => { throw new BreakEvaluationException(); };
                _eval.DoEvalSignalRef = x => { throw new BreakEvaluationException(); };
                _ecr.AddRule(IsLiteralConst, ExpressionConstRules.ERuleMode.FinalRule);
            }

            public void AcceptCompoundStatement(CompoundStatement stmt)
            {
                foreach (Statement child in stmt.Statements)
                    child.Accept(this);
            }

            private bool IsLiteralConst(object literal)
            {
                FieldRef fr = literal as FieldRef;
                if (fr != null && fr.FieldDesc.IsConstant)
                    return true;
                if (!(literal is IStorable))
                    return true;
                Variable v = literal as Variable;
                if (v == null)
                    return false;
                return _ivStack.Contains(v);
            }

            private object EvaluateVariable(Variable v)
            {
                if (!_ivStack.Contains(v))
                    throw new BreakEvaluationException();
                IVRange range = _iva._ivLoopRange[v];
                return range;
            }

            public void AcceptLoopBlock(LoopBlock stmt)
            {
                if (stmt.Initializer != null &&
                    stmt.CounterStart != null &&
                    stmt.CounterStop != null &&
                    stmt.CounterStep != null)
                {
                    if (/*stmt.CounterStart.IsConst(_ecr) &&
                        stmt.CounterStop.IsConst(_ecr) &&*/
                        stmt.CounterStep.IsConst(_ecr))
                    {
                        try
                        {
                            Expression minExpr, maxExpr;
                            switch (stmt.CounterDirection)
                            {
                                case LoopBlock.ECounterDirection.Increment:
                                    minExpr = stmt.CounterStart;
                                    maxExpr = stmt.CounterStop + stmt.CounterStep;
                                    if (stmt.CounterLimitKind == LoopBlock.ELimitKind.ExcludingStopValue)
                                        maxExpr -= LiteralReference.CreateConstant((long)1);
                                    break;

                                case LoopBlock.ECounterDirection.Decrement:
                                    maxExpr = stmt.CounterStart;
                                    minExpr = stmt.CounterStop - stmt.CounterStep;
                                    if (stmt.CounterLimitKind == LoopBlock.ELimitKind.ExcludingStopValue)
                                        minExpr += LiteralReference.CreateConstant((long)1);
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }

                            object min = minExpr.Eval(_eval);
                            object max = maxExpr.Eval(_eval);
                            IVRange minR = IVRange.ToRange(min);
                            IVRange maxR = IVRange.ToRange(max);
                            IVRange loopRange = IVRange.Max(minR, maxR);

                            if (_iva._ivLoopRange.ContainsKey(stmt.CounterVariable))
                                _iva._ivLoopRange[stmt.CounterVariable] =
                                    IVRange.Max(_iva._ivLoopRange[stmt.CounterVariable], loopRange);
                            else
                                _iva._ivLoopRange[stmt.CounterVariable] = loopRange;
                            _iva._inductionVars.Add(stmt.CounterVariable, stmt);
                            _ivStack.Push(stmt.CounterVariable);
                            stmt.Body.Accept(this);
                            if (stmt.Trailer != null)
                                stmt.Trailer.Accept(this);
                            _ivStack.Pop();
                            return;
                        }
                        catch (BreakEvaluationException)
                        {
                        }
                        catch (InvalidCastException)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }

                    }
                    _iva._unconstrainedVars.Add(stmt.CounterVariable);
                }

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
                Variable v = stmt.Container as Variable;
                if (v != null)
                    _iva._unconstrainedVars.Add(v);
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

        HashSet<Variable> _unconstrainedVars = new HashSet<Variable>();
        Dictionary<Variable, HashSet<LoopBlock>> _inductionVars = new Dictionary<Variable, HashSet<LoopBlock>>();
        Dictionary<Variable, IVRange> _ivLoopRange = new Dictionary<Variable, IVRange>();

        InductionVariableAnalyzer(Statement stmt)
        {
            DoAnalysis(stmt);
        }

        private void DoAnalysis(Statement stmt)
        {
            stmt.Accept(new StatementVisitor(this));
        }

        /// <summary>
        /// Queries for a certain variable whether a value range contraint could be inferred.
        /// </summary>
        /// <param name="v">variable to query</param>
        /// <returns><c>true</c> iff there is a value range constraint for the queried variable</returns>
        public bool IsConstrained(Variable v)
        {
            return !_unconstrainedVars.Contains(v) &&
                _inductionVars.ContainsKey(v);
        }

        /// <summary>
        /// Retrieves the value range constraint for a specific variable.
        /// </summary>
        /// <param name="v">variable to query</param>
        /// <param name="minValue">minimum value</param>
        /// <param name="maxValue">maximum value</param>
        /// <exception cref="ArgumentException">if no value range constraint exists for the specified variable</exception>
        public void GetRange(Variable v, out long minValue, out long maxValue)
        {
            if (!IsConstrained(v))
                throw new ArgumentException("no value range constraint found", "v");
            IVRange range = _ivLoopRange[v];
            minValue = range.MinValue;
            maxValue = range.MaxValue;
        }

        /// <summary>
        /// Runs induction variable analysis on a particular statement, usually a function body.
        /// </summary>
        /// <param name="stmt">statement to analyse</param>
        /// <returns>analysis results as an instance of this class</returns>
        public static InductionVariableAnalyzer Run(Statement stmt)
        {
            Statement clone = stmt.Clone;
            clone.PreprocessForLoopKindRecognition();
            clone = clone.Forify();
            return new InductionVariableAnalyzer(clone);
        }
    }
}

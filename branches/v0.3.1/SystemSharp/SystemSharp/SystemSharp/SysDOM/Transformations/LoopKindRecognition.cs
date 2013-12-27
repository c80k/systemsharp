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
using SystemSharp.Common;
using SystemSharp.Algebraic;
using SystemSharp.SysDOM.Eval;

namespace SystemSharp.SysDOM.Transformations
{
    class Preprocessor : IStatementVisitor
    {
        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            List<Statement> newBody = new List<Statement>();
            int count = stmt.Statements.Count;
            for (int i = 0; i < count; i++)
            {
                stmt.Statements[i].Accept(this);

                if (count != 2 &&
                    i < count - 1 &&
                    (stmt.Statements[i] is StoreStatement) &&
                    (stmt.Statements[i + 1] is LoopBlock))
                {
                    stmt.Statements[i + 1].Accept(this);
                    CompoundStatement cs = new CompoundStatement();
                    cs.Statements.Add(stmt.Statements[i]);
                    cs.Statements.Add(stmt.Statements[i + 1]);
                    newBody.Add(cs);
                    i++; // skip loop statement as it is processed here
                }
                else
                    newBody.Add(stmt.Statements[i]);
            }
            stmt.Statements.Clear();
            stmt.Statements.AddRange(newBody);
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
    }

    public static class LoopRecognition
    {
        public static void PreprocessForLoopKindRecognition(this Statement stmt)
        {
            Preprocessor prep = new Preprocessor();
            stmt.Accept(prep);
        }
    }

    class LoopControlDetector: IStatementVisitor
    {
        private LoopBlock _loop;

        public bool Result { get; private set; }

        public LoopControlDetector(LoopBlock loop)
        {
            _loop = loop;
            Result = false;
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
            if (stmt.Loop == _loop)
                Result = true;
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            if (stmt.Loop == _loop)
                Result = true;
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
    }

    public static class LoopScopeDetection
    {
        public static bool NeedsLoopScope(this Statement stmt, LoopBlock loop)
        {
            LoopControlDetector lcd = new LoopControlDetector(loop);
            stmt.Accept(lcd);
            return lcd.Result;
        }
    }

    class BreakLoopReplacer: IStatementVisitor
    {
        private LoopBlock _offScopeLoop;
        private Stack<LoopBlock> _loopStack = new Stack<LoopBlock>();

        public BreakLoopReplacer(LoopBlock offScopeLoop)
        {
            _offScopeLoop = offScopeLoop;
        }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement child in stmt.Statements)
                child.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            _loopStack.Push(stmt);
            stmt.Body.Accept(this);
            _loopStack.Pop();
            if (stmt.Trailer != null)
                stmt.Trailer.Accept(this);
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            if (stmt.Loop == _offScopeLoop)
            {
                if (_loopStack.Any())
                {
                    stmt.Loop = _loopStack.Last();
                }
                else
                {
                    stmt.EliminationPredicate = () => true;
                }
            }
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
    }

    class WhileLoopRecognizer: IStatementVisitor
    {
        public LoopBlock Result { get; private set; }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            IfStatement cond = stmt.Body.AsSingleStatement() as IfStatement;
            if (cond == null)
                return;

            if (cond.Conditions.Count != 1 ||
                cond.Branches.Count != 2)
                return;

            IList<Statement> trueBranch = cond.Branches[0].AsStatementList();
            if (trueBranch.Count == 0 ||
                !trueBranch.Last().Equals(new ContinueLoopStatement() { Loop = stmt }))
                return;

            IList<Statement> falseBranch = cond.Branches[1].AsStatementList();
            if (falseBranch.Count == 0)
                return;

#if false
            BreakLoopStatement breaker = new BreakLoopStatement() { Loop = stmt };
            Statement trailer = cond.Branches[1].Clone;
            trailer.RemoveAll(breaker);
#endif

            Statement trailer = cond.Branches[1].Clone;
            BreakLoopReplacer blr = new BreakLoopReplacer(stmt);
            trailer.Accept(blr);

            if (trailer.NeedsLoopScope(stmt))
                return;
#if false
            CompoundStatement trailer = new CompoundStatement();
            trailer.Statements.AddRange(falseBranch.Take(falseBranch.Count - 1));
            if (trailer.NeedsLoopScope(stmt))
                return;

            BreakLoopStatement breaker = falseBranch.Last() as BreakLoopStatement;
            if (breaker == null)
                return;

            if (breaker.Loop.IsAncestor(stmt))
            {
                /* Consider the following situation:
                 * 
                 * L1: loop
                 *   some outer loop work
                 *   L2: loop
                 *     if someCondition then
                 *       do something
                 *       continue L2
                 *     else
                 *       do something different
                 *       break L1
                 *     end if
                 *   end loop L2
                 * end loop L1
                 * 
                 * As the inner break statement breaks the outer loop, this loop
                 * must be transformed into the following code:
                 * 
                 * L1: loop
                 *   some outer loop work
                 *   while someCondition loop
                 *     do something
                 *   end while
                 *   do something different
                 *   break L1
                 * end loop
                 * */

                if (breaker.Loop != stmt)
                {
                    trailer.Statements.Add(breaker);
                }
            }
            else
                return;
#endif

            CompoundStatement newBody = new CompoundStatement();
            newBody.Statements.AddRange(trueBranch.Take(trueBranch.Count - 1));

            LoopBlock whileBlock = (LoopBlock)stmt.Clone;
            whileBlock.HeadCondition = cond.Conditions[0];
            whileBlock.Body = newBody;
            whileBlock.Trailer = trailer;

            Result = whileBlock;
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
        }

        public void AcceptIf(IfStatement stmt)
        {
        }

        public void AcceptCase(CaseStatement stmt)
        {
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
    }

    /// <summary>
    /// Defines restrictions on "for" loop pattern matching.
    /// </summary>
    public enum EForLoopLevel
    {
        /// <summary>
        /// The default: anything having the structure of a for-loop (according to C++/C# semantics) will be recognized.
        /// </summary>
        Default,

        /// <summary>
        /// A "strict" for-loop comes with several restrictions:
        /// 1. The initializer must be an assignment to an integral variable (the counter variable).
        /// 2. The head condition must be a comparison of that counter variable to some expression
        /// 3. The step must either be an incrementation or a decrementation of the counter variable
        /// 4. The counter variable must not be assigned inside the loop body.
        /// </summary>
        Strict,

        /// <summary>
        /// A "strict" for-loop where additionally the counter increment is exactly +1 or -1.
        /// </summary>
        StrictOneInc
    }

    class ForLoopRecognizer : IStatementVisitor
    {
        public LoopBlock Result { get; private set; }
        public EForLoopLevel Level { get; set; }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            if (stmt.Statements.Count != 2)
                return;

            StoreStatement initializer = stmt.Statements[0] as StoreStatement;
            LoopBlock loop = stmt.Statements[1].AsWhileLoop();
            if (initializer == null || loop == null)
                return;

            IList<Statement> body = loop.Body.AsStatementList();
            if (body.Count == 0)
                return;

            StoreStatement step = body.Last() as StoreStatement;
            if (step == null)
                return;

            CompoundStatement newBody = new CompoundStatement();
            newBody.Statements.AddRange(body.Take(body.Count - 1));

            loop.Initializer = initializer;
            loop.Body = newBody;
            loop.Step = step;

            if (Level == EForLoopLevel.Strict ||
                Level == EForLoopLevel.StrictOneInc)
            {
                StoreStatement initStore = initializer;

                if (initStore.Container == null)
                    return;
                
                Variable counterVar = initStore.Container as Variable;
                if (counterVar == null)
                    return;

                loop.CounterVariable = counterVar;

                Type counterType = counterVar.Type.CILType;
                if (!counterType.IsEnumerable())
                    return;

                if (loop.Body.Modifies(counterVar))
                    return;

                loop.CounterStart = initStore.Value;

                if (step.Container == null)
                    return;

                if (!step.Container.Equals(counterVar))
                    return;

                Expression stepExpr = step.Value;

                Matching x = new Matching();
                Matching mctr = (LiteralReference)counterVar;
                Matching mctrInc = mctr + x;
                Matching mctrDec = mctr - x;
                if (((Expression.MatchFunction)mctrInc)(stepExpr))
                {
                    loop.CounterStep = x.Result;
                    loop.CounterDirection = LoopBlock.ECounterDirection.Increment;
                }
                else if (((Expression.MatchFunction)mctrDec)(stepExpr))
                {
                    loop.CounterStep = x.Result;
                    loop.CounterDirection = LoopBlock.ECounterDirection.Decrement;
                }
                else
                    return;

                BinOp cmp = loop.HeadCondition as BinOp;
                if (cmp == null)
                    return;

                LiteralReference lhs = cmp.Children[0] as LiteralReference;
                if (lhs == null)
                    return;
                if (!lhs.ReferencedObject.Equals(counterVar))
                    return;

                loop.CounterStop = cmp.Children[1];

                switch (loop.CounterDirection)
                {
                    case LoopBlock.ECounterDirection.Decrement:
                        switch (cmp.Operation)
                        {
                            case BinOp.Kind.Gt:
                            case BinOp.Kind.NEq:
                                loop.CounterLimitKind = LoopBlock.ELimitKind.ExcludingStopValue;
                                break;

                            case BinOp.Kind.GtEq:
                                loop.CounterLimitKind = LoopBlock.ELimitKind.IncludingStopValue;
                                break;

                            default:
                                return;
                        }
                        break;

                    case LoopBlock.ECounterDirection.Increment:
                        switch (cmp.Operation)
                        {
                            case BinOp.Kind.Lt:
                            case BinOp.Kind.NEq:
                                loop.CounterLimitKind = LoopBlock.ELimitKind.ExcludingStopValue;
                                break;

                            case BinOp.Kind.LtEq:
                                loop.CounterLimitKind = LoopBlock.ELimitKind.IncludingStopValue;
                                break;

                            default:
                                return;
                        }
                        break;
                }

                if (Level == EForLoopLevel.StrictOneInc)
                {
                    object inc;
                    try
                    {
                        inc = loop.CounterStep.Eval(new DefaultEvaluator());
                    }
                    catch (BreakEvaluationException)
                    {
                        return;
                    }
                    if (inc == null)
                        return;
                    long stepValue = TypeConversions.ToLong(inc);
                    if (stepValue == 1)
                    {
                        switch (loop.CounterDirection)
                        {
                            case LoopBlock.ECounterDirection.Increment:
                                loop.CounterDirection = LoopBlock.ECounterDirection.IncrementOne;
                                break;

                            case LoopBlock.ECounterDirection.Decrement:
                                loop.CounterDirection = LoopBlock.ECounterDirection.DecrementOne;
                                break;
                        }
                    }
                    else if (stepValue == -1)
                    {
                        switch (loop.CounterDirection)
                        {
                            case LoopBlock.ECounterDirection.Increment:
                                loop.CounterDirection = LoopBlock.ECounterDirection.DecrementOne;
                                break;

                            case LoopBlock.ECounterDirection.Decrement:
                                loop.CounterDirection = LoopBlock.ECounterDirection.IncrementOne;
                                break;
                        }
                    }
                    else
                        return;
                }
            }

            Result = loop;
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
        }

        public void AcceptIf(IfStatement stmt)
        {
        }

        public void AcceptCase(CaseStatement stmt)
        {
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
    }

    /// <summary>
    /// This static class provides services for detecting common loop patterns, such as "while" loop and "for" loop.
    /// If such pattern is detected, the statement can be rewritten to the explicit SysDOM representation of a "while"
    /// or "for" loop, respectively.
    /// </summary>
    public static class LoopKindDetection
    {
        /// <summary>
        /// Tries to interpret the statement as "while" loop.
        /// </summary>
        /// <param name="stmt">assumed "while" loop</param>
        /// <returns>an explicit SysDOM representation of the "while" loop, or <c>null</c> if the statement
        /// does not match the expected pattern</returns>
        public static LoopBlock AsWhileLoop(this Statement stmt)
        {
            WhileLoopRecognizer wlr = new WhileLoopRecognizer();
            stmt.Accept(wlr);
            return wlr.Result;
        }

        /// <summary>
        /// Tries to interpret the statement as "for" loop.
        /// </summary>
        /// <param name="stmt">assumed "for" loop</param>
        /// <param name="level">pattern matching restrictions</param>
        /// <returns>an explicit SysDOM representation of the "for" loop, or <c>null</c> if the statement
        /// does not match the expected pattern</returns>
        public static LoopBlock AsForLoop(this Statement stmt, EForLoopLevel level)
        {
            ForLoopRecognizer flr = new ForLoopRecognizer()
            {
                Level = level
            };
            stmt.Accept(flr);
            return flr.Result;
        }
    }
}

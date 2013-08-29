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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Algebraic;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.SysDOM
{
    public interface IStatementVisitor
    {
        void AcceptCompoundStatement(CompoundStatement stmt);
        void AcceptLoopBlock(LoopBlock stmt);
        void AcceptBreakLoop(BreakLoopStatement stmt);
        void AcceptContinueLoop(ContinueLoopStatement stmt);
        void AcceptIf(IfStatement stmt);
        void AcceptCase(CaseStatement stmt);
        void AcceptStore(StoreStatement stmt);
        void AcceptNop(NopStatement stmt);
        void AcceptSolve(SolveStatement stmt);
        void AcceptBreakCase(BreakCaseStatement stmt);
        void AcceptGotoCase(GotoCaseStatement stmt);
        void AcceptGoto(GotoStatement stmt);
        void AcceptReturn(ReturnStatement stmt);
        void AcceptThrow(ThrowStatement stmt);
        void AcceptCall(CallStatement stmt);
    }

    public abstract class Statement: AttributedObject
    {
        internal class CloneContext
        {
            Dictionary<Statement, Statement> _map = new Dictionary<Statement,Statement>();

            public Action Complete { get; set; }

            public CloneContext()
            {
            }

            public void Map(Statement oldStmt, Statement newStmt)
            {
                _map[oldStmt] = newStmt;
            }

            public Statement Map(Statement stmt)
            {
                Statement result;
                if (!_map.TryGetValue(stmt, out result))
                    result = stmt;
                return result;
            }
        }

        public delegate bool EliminationPredicateFunc();

        public abstract void Accept(IStatementVisitor visitor);        
        public Statement Successor { get; internal set; }
        public int ProgramCounter { get; internal set; }
        public string Label { get; set; }
        public EliminationPredicateFunc EliminationPredicate { get; set; }
        public string Comment { get; internal set; }

        public void AcceptIfEnabled(IStatementVisitor visitor)
        {
            if (!IsEliminated)
                Accept(visitor);
        }

        public bool IsEliminated
        {
            get { return EliminationPredicate(); }
        }

        public Statement()
        {
            EliminationPredicate = () => false;
        }

        public Statement Clone
        {
            get
            {
                CloneContext ctx = new CloneContext();
                Statement result = CloneInternal(ctx);
                if (ctx.Complete != null)
                    ctx.Complete();
                return result;
            }
        }

        internal Statement GetClone(CloneContext ctx)
        {
            Statement clone = CloneInternal(ctx);
            ctx.Map(this, clone);
            clone.Label = Label;
            clone.CopyAttributesFrom(this);
            return clone;
        }

        internal abstract Statement CloneInternal(CloneContext ctx);

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            ToString(0, sb);
            return sb.ToString();
        }

        public virtual void ToString(int indent, StringBuilder sb)
        {
            if (Label != null)
                sb.AppendLine(Label + ":");
        }

        protected void AppendAttributes(StringBuilder sb)
        {
            if (Attributes.Any())
            {
                sb.Append(" {");
                sb.Append(string.Join(", ", Attributes));
                sb.Append("}");
            }
        }

        protected void Indent(int n, StringBuilder sb)
        {
            for (int i = 0; i < n; i++)
                sb.Append("  ");
        }

        public void CheckConsistency()
        {
            Accept(new StatementConsistencyChecker());
        }

        public void ReplaceExpressions(
            Expression.MatchFunction matchFn,
            ExpressionGenerator exprGen)
        {
            Accept(new StmtExpressionSubstitution()
            {
                MatchFn = matchFn,
                ExprGen = exprGen
            });
        }
    }

    public abstract class ScopedStatement: Statement
    {
        public ScopedStatement()
        {
            Locals = new List<IStorableLiteral>();
        }

        public List<IStorableLiteral> Locals { get; private set; }
    }

    public abstract class MetaStatement : Statement
    {
    }

    public class CompoundStatement : ScopedStatement
    {
        public CompoundStatement()
        {
            Statements = new List<Statement>();
        }

        public List<Statement> Statements { get; private set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptCompoundStatement(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("{");
            AppendAttributes(sb);
            sb.AppendLine();
            foreach (Statement stmt in Statements)
            {
                stmt.ToString(indent + 1, sb);
            }
            Indent(indent, sb);
            sb.Append("}");
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            CompoundStatement clone = new CompoundStatement();
            ctx.Map(this, clone);
            foreach (Statement stmt in Statements)
            {
                if (!stmt.IsEliminated)
                    clone.Statements.Add(stmt.GetClone(ctx));
            }
            return clone;
        }
    }

    /// <summary>
    /// The LoopBlock models a loop.
    /// </summary>
    /// <remarks>
    /// The same class is used to model simple loops as well as while-, for- and do-loops.
    /// </remarks>
    public class LoopBlock : ScopedStatement
    {
        /// <summary>
        /// The head condition is evaluated at loop entry. The loop body is executed only if it evaluates to true.
        /// </summary>
        /// <remarks>
        /// while- and for-loops are associated with a head condition.
        /// </remarks>
        public Expression HeadCondition { get; set; }

        /// <summary>
        /// The tail condition is evaluated prio to the next iteration. The loop body is re-executed only if it evaluates to true.
        /// </summary>
        /// <remarks>
        /// do-loops are associated with a tail condition.
        /// </remarks>
        public Expression TailCondition { get; set; }

        /// <summary>
        /// The initializer is executed prior to the very first loop iteration.
        /// </summary>
        /// <remarks>
        /// for-loops are associated with an initializer.
        /// </remarks>
        public StoreStatement Initializer { get; set; }

        /// <summary>
        /// The trailer is executed after the very last loop iteration.
        /// </summary>
        /// <remarks>
        /// Trailers are an artifact of loop kind detection.
        /// </remarks>
        public Statement Trailer { get; set; }

        /// <summary>
        /// The step is executed after each loop iteration.
        /// </summary>
        /// <remarks>
        /// for-loops are associated with a step.
        /// </remarks>
        public StoreStatement Step { get; set; }

        /// <summary>
        /// The counter variable (given this is a "strict" for-loop)
        /// </summary>
        public Variable CounterVariable { get; set; }

        /// <summary>
        /// The counter start value (given this is a "strict" for-loop)
        /// </summary>
        public Expression CounterStart { get; set; }

        /// <summary>
        /// The counter stop value (given this is a "strict" for-loop)
        /// </summary>
        public Expression CounterStop { get; set; }

        public enum ELimitKind
        {
            /// <summary>
            /// Iteration terminates as soon as the counter exceeds StopValue
            /// </summary>
            IncludingStopValue,

            /// <summary>
            /// Iteration terminates as soon as the counter reaches or exceeds (in the sense of CounterDirection) StopValue
            /// </summary>
            ExcludingStopValue
        }

        /// <summary>
        /// The counter stop value limit interpretation (given this is a "strict" for-loop)
        /// </summary>
        public ELimitKind CounterLimitKind { get; set; }

        /// <summary>
        /// The counter increment (given this is a "strict" for-loop)
        /// </summary>
        public Expression CounterStep { get; set; }

        public enum ECounterDirection
        {
            Increment,
            Decrement,
            IncrementOne,
            DecrementOne
        }

        /// <summary>
        /// The counter direction (given this is a "strict" for-loop)
        /// </summary>
        public ECounterDirection CounterDirection { get; set; }

        /// <summary>
        /// The loop body.
        /// </summary>
        public Statement Body { get; set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptLoopBlock(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            if (Initializer != null)
            {
                sb.Append("FOR ");
                sb.Append(Initializer.ToString());
                sb.Append("; ");
                sb.Append(HeadCondition.ToString());
                sb.Append("; ");
                sb.Append(Step);
                sb.Append(" {");
            }
            else if (HeadCondition != null)
            {
                sb.Append("WHILE ");
                sb.Append(HeadCondition.ToString());
                sb.Append(" {");
            }
            else
            {
                sb.Append("LOOP {");
            }
            AppendAttributes(sb);
            sb.AppendLine();
            Body.ToString(indent+1, sb);
            Indent(indent, sb);
            sb.AppendLine("}");
            if (Trailer != null)
            {
                sb.Append(Trailer.ToString());
            }
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            LoopBlock loop = new LoopBlock();
            ctx.Map(this, loop);
            loop.Body = Body.GetClone(ctx);
            return loop;
        }
    }

    public abstract class SimpleStatement : Statement
    {
    }

    public abstract class BranchStatement : SimpleStatement
    {
    }

    public abstract class LoopControlStatement : BranchStatement
    {
        public LoopBlock Loop { get; set; }
    }

    public class BreakLoopStatement : LoopControlStatement
    {
        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptBreakLoop(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            string label = "";
            if (Loop == null)
                label = " ???";
            else if (Loop.Label != null)
                label = " " + Loop.Label;
            sb.Append("BREAK" + label);
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new BreakLoopStatement()
            {
                Loop = (LoopBlock)ctx.Map(Loop)
            };
        }

        public override bool Equals(object obj)
        {
            if (obj is BreakLoopStatement)
            {
                BreakLoopStatement other = (BreakLoopStatement)obj;
                return other.Loop == Loop;
            }
            else
                return false;
        }
    }

    public class ContinueLoopStatement : LoopControlStatement
    {
        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptContinueLoop(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            string label = "";
            if (Loop == null)
                label = " ???";
            else if (Loop.Label != null)
                label = " " + Loop.Label;
            sb.Append("CONTINUE" + label);
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new ContinueLoopStatement()
            {
                Loop = (LoopBlock)ctx.Map(Loop)
            };
        }

        public override bool Equals(object obj)
        {
            if (obj is ContinueLoopStatement)
            {
                ContinueLoopStatement other = (ContinueLoopStatement)obj;
                return other.Loop == Loop;
            }
            else
                return false;
        }
    }

    public class BreakCaseStatement : BranchStatement
    {
        public CaseStatement CaseStmt { get; set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptBreakCase(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("BREAK CASE");
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new BreakCaseStatement()
            {
                CaseStmt = (CaseStatement)ctx.Map(CaseStmt)
            };
        }
    }

    public class GotoCaseStatement : BranchStatement
    {
        public CaseStatement CaseStmt { get; set; }
        public int TargetIndex { get; set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptGotoCase(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            if (TargetIndex == CaseStmt.Cases.Count)
                sb.Append("GOTO DEFAULT CASE");
            else
                sb.Append("GOTO CASE " + CaseStmt.Cases[TargetIndex].ToString());
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new GotoCaseStatement()
            {
                CaseStmt = (CaseStatement)ctx.Map(CaseStmt),
                TargetIndex = this.TargetIndex
            };
        }
    }

    public class GotoStatement : BranchStatement
    {
        public Statement Target { get; set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptGoto(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            if (Target == null)
                sb.Append("GOTO ???");
            else
                sb.Append("GOTO " + Target.Label);
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            GotoStatement result = new GotoStatement();
            if (Target != null)
                ctx.Complete += () => result.Target = ctx.Map(Target);
            return result;
        }
    }

    public class ReturnStatement: BranchStatement
    {
        public Expression ReturnValue { get; set; }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("RETURN");
            if (ReturnValue != null)
                sb.Append(" " + ReturnValue.ToString());
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return this;
        }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptReturn(this);
        }
    }

    public class ThrowStatement : BranchStatement
    {
        public Expression ThrowExpr { get; set; }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("THROW " + ThrowExpr.ToString());
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new ThrowStatement()
            {
                ThrowExpr = this.ThrowExpr
            };
        }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptThrow(this);
        }
    }

    public class CallStatement : SimpleStatement
    {
        public ICallable Callee { get; set; }
        public Expression[] Arguments { get; set; }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("CALL " + Callee.ToString() + "(");
            for (int i = 0; i < Arguments.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(Arguments[i].ToString());
            }
            sb.Append(")");
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new CallStatement()
            {
                Callee = this.Callee,
                Arguments = this.Arguments
            };
        }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptCall(this);
        }
    }

    public class IfStatement : Statement
    {
        public IfStatement()
        {
            Conditions = new List<Expression>();
            Branches = new List<Statement>();
        }

        public List<Expression> Conditions { get; private set; }
        public List<Statement> Branches { get; private set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptIf(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            for (int i = 0; i < Conditions.Count; i++)
            {
                Expression cond = Conditions[i];
                Statement branch = Branches[i];
                Indent(indent, sb);
                if (i == 0)
                    sb.Append("IF");
                else
                    sb.Append("ELSIF");
                sb.Append(" (" + cond.ToString() + ")");
                AppendAttributes(sb);
                sb.AppendLine();
                branch.ToString(indent + 1, sb);
            }
            if (Branches.Count > Conditions.Count)
            {
                Indent(indent, sb);
                sb.AppendLine("ELSE");
                Branches.Last().ToString(indent + 1, sb);
            }
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            IfStatement clone = new IfStatement();
            ctx.Map(this, clone);
            foreach (Expression e in Conditions)
                clone.Conditions.Add(e);
            foreach (Statement b in Branches)
                clone.Branches.Add(b.GetClone(ctx));
            return clone;
        }
    }

    public class CaseStatement : Statement
    {
        public CaseStatement()
        {
            Cases = new List<Expression>();
            Branches = new List<Statement>();
        }

        public Expression Selector { get; set; }
        public List<Expression> Cases { get; private set; }
        public List<Statement> Branches { get; private set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptCase(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("SELECT CASE (" + Selector + ")");
            AppendAttributes(sb);
            sb.AppendLine();
            Indent(indent, sb);
            sb.AppendLine("{");
            for (int i = 0; i < Cases.Count; i++)
            {
                Expression cond = Cases[i];
                Statement branch = Branches[i];

                Indent(indent + 1, sb);
                sb.AppendLine("CASE " + cond + ":");
                branch.ToString(indent + 2, sb);
            }
            if (Branches.Count > Cases.Count)
            {
                Indent(indent + 1, sb);
                sb.AppendLine("DEFAULT:");
                Branches.Last().ToString(indent + 2, sb);
            }
            Indent(indent, sb);
            sb.AppendLine("}");
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            CaseStatement clone = new CaseStatement()
            {
                Selector = this.Selector
            };
            ctx.Map(this, clone);
            foreach (Expression e in Cases)
                clone.Cases.Add(e);
            foreach (Statement b in Branches)
                clone.Branches.Add(b.GetClone(ctx));
            return clone;
        }

        public IfStatement ConvertToIfStatement()
        {
            List<Expression> conds = new List<Expression>();
            foreach (Expression caseExpr in Cases)
            {
                Expression cond = Expression.Equal(Selector, caseExpr);
                conds.Add(cond);
            }
            IfStatement result = new IfStatement();
            result.Conditions.AddRange(conds);
            result.Branches.AddRange(Branches);
            return result;
        }
    }

    public class SolveStatement : MetaStatement
    {
        public EquationSystem EqSys { get; set; }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("SOLVE (dim =" + EqSys.Variables.Count + ")");
            AppendAttributes(sb);
            sb.AppendLine();
            Indent(indent + 1, sb);
            sb.Append("Unknowns: ");
            for (int i = 0; i < EqSys.Variables.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(EqSys.Variables[i]);
            }
            sb.AppendLine();
            Indent(indent+1, sb);
            sb.AppendLine("Equations:");
            for (int i = 0; i < EqSys.Equations.Count; i++)
            {
                Indent(indent + 2, sb);
                sb.AppendLine(EqSys.Equations[i].ToString());
            }
        }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptSolve(this);
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new SolveStatement()
            {
                EqSys = this.EqSys
            };
        }
    }

    public class StoreStatement: SimpleStatement
    {
        public IStorableLiteral Container { get; set; }
        public Expression Value { get; set; }

        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptStore(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            string starget = Container.ToString();
            sb.Append(starget + " := " + Value.ToString());
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new StoreStatement()
            {
                Container = this.Container,
                Value = this.Value
            };
        }
    }

    public class NopStatement : SimpleStatement
    {
        public override void Accept(IStatementVisitor visitor)
        {
            visitor.AcceptNop(this);
        }

        public override void ToString(int indent, StringBuilder sb)
        {
            base.ToString(indent, sb);
            Indent(indent, sb);
            sb.Append("NOP");
            AppendAttributes(sb);
            sb.AppendLine();
        }

        internal override Statement CloneInternal(CloneContext ctx)
        {
            return new NopStatement();
        }
    }

    public class StatementLinker
    {
        class Visitor : IStatementVisitor
        {
            #region IStatementVisitor Members

            public void AcceptCompoundStatement(CompoundStatement stmt)
            {
                Statement last = null;
                foreach (Statement child in stmt.Statements)
                {
                    if (last != null)
                        last.Successor = child;
                    last = child;
                }
                if (stmt.Statements.Count > 0)
                {
                    stmt.Statements.Last().Successor = stmt.Successor;
                }
                foreach (Statement child in stmt.Statements)
                {
                    child.Accept(this);
                }
            }

            public void AcceptLoopBlock(LoopBlock stmt)
            {
                stmt.Body.Successor = stmt.Body;
                stmt.Body.Accept(this);
            }

            public void AcceptBreakLoop(BreakLoopStatement stmt)
            {
                stmt.Successor = stmt.Loop.Successor;
            }

            public void AcceptContinueLoop(ContinueLoopStatement stmt)
            {
                stmt.Successor = stmt.Loop;
            }

            public void AcceptIf(IfStatement stmt)
            {
                foreach (Statement branch in stmt.Branches)
                {
                    branch.Successor = stmt.Successor;
                    branch.Accept(this);
                }
            }

            public void AcceptCase(CaseStatement stmt)
            {
                foreach (Statement branch in stmt.Branches)
                {
                    branch.Successor = stmt.Successor;
                    branch.Accept(this);
                }
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
                stmt.Successor = stmt.CaseStmt.Successor;
            }

            public void AcceptGotoCase(GotoCaseStatement stmt)
            {
                stmt.Successor = stmt.CaseStmt.Branches[stmt.TargetIndex];
            }

            public void AcceptGoto(GotoStatement stmt)
            {
                stmt.Successor = stmt.Target;
            }

            public void AcceptReturn(ReturnStatement stmt)
            {
                stmt.Successor = null;
            }

            public void AcceptThrow(ThrowStatement stmt)
            {
                stmt.Successor = null;
            }

            public void AcceptCall(CallStatement stmt)
            {
            }
            #endregion
        }

        public static void Link(Statement stmt)
        {
            stmt.Accept(new Visitor());
        }
    }

    public class StmtVariableReplacer : IStatementVisitor
    {
        private Dictionary<Literal, Literal> _rplMap = new Dictionary<Literal, Literal>();

        public void AddReplacement(Literal v, Literal v_)
        {
            _rplMap[v] = v_;
        }

        public Literal Lookup(Literal v)
        {
            Literal v_ = v;
            if (_rplMap.TryGetValue(v, out v_))
                return v_;
            else
                return v;
        }

        private Expression SubstExpression(Expression e)
        {
            foreach (KeyValuePair<Literal, Literal> kvp in _rplMap)
            {
                if (!kvp.Key.Equals(kvp.Value))
                {
                    e = e.Substitute(kvp.Key, kvp.Value);
                }
            }
            return e;
        }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement s in stmt.Statements)
                s.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body.Accept(this);
            for (int i = 0; i < stmt.Locals.Count; i++)
            {
                stmt.Locals[i] = (IStorableLiteral)Lookup((Literal)stmt.Locals[i]);
            }

        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            // nop
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            // nop
        }

        public void AcceptIf(IfStatement stmt)
        {
            foreach (Statement s in stmt.Branches)
                s.Accept(this);
            for (int i = 0; i < stmt.Conditions.Count; i++)
                stmt.Conditions[i] = SubstExpression(stmt.Conditions[i]);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            foreach (Statement s in stmt.Branches)
                s.Accept(this);
            for (int i = 0; i < stmt.Cases.Count; i++)
                stmt.Cases[i] = SubstExpression(stmt.Cases[i]);
            stmt.Selector = SubstExpression(stmt.Selector);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            stmt.Container = (IStorableLiteral)Lookup((Literal)stmt.Container);
            stmt.Value = SubstExpression(stmt.Value);
        }

        public void AcceptNop(NopStatement stmt)
        {
            // nop
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            EquationSystem eqs = new EquationSystem();
            foreach (Expression eq in stmt.EqSys.Equations)
                eqs.Equations.Add(SubstExpression(eq));
            foreach (Variable v in stmt.EqSys.Variables)
                eqs.Variables.Add((Variable)Lookup(v));
            stmt.EqSys = eqs;
        }


        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            // nop
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            // nop
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            // nop
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            // nop
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            stmt.ThrowExpr = SubstExpression(stmt.ThrowExpr);
        }

        public void AcceptCall(CallStatement stmt)
        {
            for (int i = 0; i < stmt.Arguments.Length; i++)
                stmt.Arguments[i] = SubstExpression(stmt.Arguments[i]);
        }
    }

    class StmtExpressionSubstitution : IStatementVisitor
    {
        public Expression.MatchFunction MatchFn { get; set; }
        public ExpressionGenerator ExprGen { get; set; }

        private Expression SubstExpression(Expression e)
        {
            bool hit;
            return e.Replace(MatchFn, ExprGen, out hit);
        }

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement s in stmt.Statements)
                s.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            stmt.Body.Accept(this);

        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            // nop
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            // nop
        }

        public void AcceptIf(IfStatement stmt)
        {
            foreach (Statement s in stmt.Branches)
                s.Accept(this);
            for (int i = 0; i < stmt.Conditions.Count; i++)
                stmt.Conditions[i] = SubstExpression(stmt.Conditions[i]);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            foreach (Statement s in stmt.Branches)
                s.Accept(this);
            for (int i = 0; i < stmt.Cases.Count; i++)
                stmt.Cases[i] = SubstExpression(stmt.Cases[i]);
            stmt.Selector = SubstExpression(stmt.Selector);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            stmt.Value = SubstExpression(stmt.Value);
        }

        public void AcceptNop(NopStatement stmt)
        {
            // nop
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            EquationSystem eqs = new EquationSystem();
            foreach (Expression eq in stmt.EqSys.Equations)
                eqs.Equations.Add(SubstExpression(eq));
            stmt.EqSys = eqs;
        }


        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            // nop
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            // nop
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            // nop
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            // nop
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            stmt.ThrowExpr = SubstExpression(stmt.ThrowExpr);
        }

        public void AcceptCall(CallStatement stmt)
        {
            for (int i = 0; i < stmt.Arguments.Length; i++)
                stmt.Arguments[i] = SubstExpression(stmt.Arguments[i]);
        }
    }

    public class StatementConsistencyChecker : IStatementVisitor
    {
        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement s in stmt.Statements)
                s.Accept(this);
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
            foreach (Expression e in stmt.Conditions)
                e.CheckConsistency();
            foreach (Statement s in stmt.Branches)
                s.Accept(this);
        }

        public void AcceptCase(CaseStatement stmt)
        {
            stmt.Selector.CheckConsistency();
            foreach (Expression e in stmt.Cases)
                e.CheckConsistency();
            foreach (Statement s in stmt.Branches)
                s.Accept(this);
        }

        public void AcceptStore(StoreStatement stmt)
        {
            stmt.Value.CheckConsistency();
        }

        public void AcceptNop(NopStatement stmt)
        {
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            stmt.EqSys.CheckConsistency();
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
            stmt.ThrowExpr.CheckConsistency();
        }

        public void AcceptCall(CallStatement stmt)
        {
            foreach (Expression arg in stmt.Arguments)
                arg.CheckConsistency();
        }
    }

    public abstract class FunctionBase : AttributedObject
    {
        public string Name { get; set; }
        public Variable ThisVariable { get; set; }
        public List<IStorableLiteral> InputVariables { get; private set; }
        public List<IStorableLiteral> OutputVariables { get; private set; }
        public List<IStorableLiteral> LocalVariables { get; private set; }

        public FunctionBase()
        {
            InputVariables = new List<IStorableLiteral>();
            OutputVariables = new List<IStorableLiteral>();
            LocalVariables = new List<IStorableLiteral>();
        }

        public abstract void CheckConsistency();
    }

    public class Function : FunctionBase
    {
        public Statement Body { get; set; }

        public Function()
        {
        }

        public override void CheckConsistency()
        {
            Body.CheckConsistency();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("sysdom:" + Name + "{");
            sb.Append(Body.ToString());
            sb.AppendLine("}");
            return sb.ToString();
        }
    }

    public class StateFunction : FunctionBase
    {
        public Statement[] States { get; set; }

        public StateFunction()
        {
        }

        public override void CheckConsistency()
        {
            foreach (var state in States)
                state.CheckConsistency();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("sysdom:" + Name + "{");
            for (int i = 0; i < States.Length; i++)
            {
                sb.AppendLine("State " + (i - 1) + ": {");
                sb.Append(States[i].ToString());
                sb.AppendLine("}");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }
    }

    [ContractClass(typeof(AlgorithmBuilderContractClass))]
    public interface IAlgorithmBuilder
    {
        void DeclareLocal(IStorableLiteral v);
        void Store(IStorableLiteral var, Expression val);
        IfStatement If(Expression cond);
        void ElseIf(Expression cond);
        void Else();
        void EndIf();
        LoopBlock Loop();
        void Break(LoopBlock loop);
        void Continue(LoopBlock loop);
        void EndLoop();
        void Solve(EquationSystem eqsys);
        void InlineCall(Function fn, Expression[] inArgs, Variable[] outArgs, bool shareLocals = false);
        CaseStatement Switch(Expression selector);
        void Case(Expression cond);
        void DefaultCase();
        void GotoCase(CaseStatement cstmt, int index);
        void Break(CaseStatement stmt);
        void EndCase();
        void EndSwitch();
        GotoStatement Goto();
        void Return();
        void Return(Expression returnValue);
        void Throw(Expression expr);
        void Call(ICallable callee, params Expression[] arguments);
        void Nop();
        Statement LastStatement { get; }
        void RemoveLastStatement();
        bool HaveAnyStatement { get; }
        IAlgorithmBuilder BeginSubAlgorithm();
        void Comment(string comment);
    }

    public interface IFunctionBuilder: IAlgorithmBuilder
    {
        Function ResultFunction { get; }
    }

    [ContractClassFor(typeof(IAlgorithmBuilder))]
    abstract class AlgorithmBuilderContractClass : IAlgorithmBuilder
    {
        public void DeclareLocal(IStorableLiteral v)
        {
            Contract.Requires(v != null);
        }

        public void Store(IStorableLiteral var, Expression val)
        {
            Contract.Requires(var != null);
            Contract.Requires(val != null);
        }

        public IfStatement If(Expression cond)
        {
            Contract.Requires(cond != null);
            Contract.Ensures(Contract.Result<IfStatement>() != null);
            return null;
        }

        public void ElseIf(Expression cond)
        {
            Contract.Requires(cond != null);
        }

        public void Else()
        {
        }

        public void EndIf()
        {
        }

        public LoopBlock Loop()
        {
            Contract.Ensures(Contract.Result<LoopBlock>() != null);
            return null;
        }

        public void Break(LoopBlock loop)
        {
            Contract.Requires(loop != null);
        }

        public void Continue(LoopBlock loop)
        {
            Contract.Requires(loop != null);
        }

        public void EndLoop()
        {
        }

        public void Solve(EquationSystem eqsys)
        {
            Contract.Requires(eqsys != null);
        }

        public void InlineCall(Function fn, Expression[] inArgs, Variable[] outArgs, bool shareLocals = false)
        {
            Contract.Requires(fn != null);
            Contract.Requires(inArgs != null);
            Contract.Requires(outArgs != null);
            Contract.Requires(inArgs.Length == fn.InputVariables.Count);
            Contract.Requires(outArgs.Length == fn.OutputVariables.Count);
        }

        public CaseStatement Switch(Expression selector)
        {
            Contract.Requires(selector != null);
            Contract.Ensures(Contract.Result<CaseStatement>() != null);
            return null;
        }

        public void Case(Expression cond)
        {
            Contract.Requires(cond != null);
        }

        public void DefaultCase()
        {
        }

        public void GotoCase(CaseStatement cstmt, int index)
        {
            Contract.Requires(cstmt != null);
            Contract.Requires(index >= 0);
            // Might not be valid during statement construction
            //Contract.Requires(index < cstmt.Cases.Count);
        }

        public void Break(CaseStatement stmt)
        {
            Contract.Requires(stmt != null);
        }

        public void EndCase()
        {
        }

        public void EndSwitch()
        {
        }

        public GotoStatement Goto()
        {
            Contract.Ensures(Contract.Result<GotoStatement>() != null);
            return null;
        }

        public void Return()
        {
        }

        public void Return(Expression returnValue)
        {
            Contract.Requires(returnValue != null);
        }

        public void Throw(Expression expr)
        {
            Contract.Requires(expr != null);
        }

        public void Call(ICallable callee, Expression[] arguments)
        {
            Contract.Requires(callee != null);
            Contract.Requires(arguments != null);
        }

        public void Nop()
        {
        }

        public Statement LastStatement
        {
            get 
            {
                Contract.Ensures(Contract.Result<Statement>() != null);
                return null;
            }
        }

        public void RemoveLastStatement()
        {
        }

        public bool HaveAnyStatement { get { return false; }  }

        public IAlgorithmBuilder BeginSubAlgorithm()
        {
            return null;
        }

        public void Comment(string comment)
        {
        }
    }

    public abstract class AbstractAlgorithmBuilder :
        IFunctionBuilder
    {
        private CompoundStatement _stmts = new CompoundStatement();
        private Stack<CompoundStatement> _cstack = new Stack<CompoundStatement>();
        private Stack<Statement> _sstack = new Stack<Statement>();

        public abstract Function ResultFunction { get; }

        public AbstractAlgorithmBuilder()
        {
        }

        protected virtual void Reset(CompoundStatement root)
        {
            _cstack.Clear();
            _sstack.Clear();
            _cstack.Push(root);
            _sstack.Push(root);
        }

        public virtual void Store(IStorableLiteral var, Expression val)
        {
            if (var == null || val == null)
                throw new ArgumentException();

            StoreStatement stmt = new StoreStatement()
            {
                Container = var,
                Value = val
            };
            _cstack.Peek().Statements.Add(stmt);
        }

        public virtual IAlgorithmBuilder BeginSubAlgorithm()
        {
            CompoundStatement sub = new CompoundStatement();
            _cstack.Peek().Statements.Add(sub);
            return new DependendAlgorithmBuilder(ResultFunction, sub);
        }

        public virtual IfStatement If(Expression cond)
        {
            IfStatement ifstmt = new IfStatement();
            CompoundStatement ifbranch = new CompoundStatement();
            ifstmt.Conditions.Add(cond);
            ifstmt.Branches.Add(ifbranch);
            _cstack.Peek().Statements.Add(ifstmt);
            _cstack.Push(ifbranch);
            _sstack.Push(ifstmt);
            return ifstmt;
        }

        public virtual void ElseIf(Expression cond)
        {
            IfStatement ifstmt = (IfStatement)_sstack.Peek();
            CompoundStatement branch = new CompoundStatement();
            ifstmt.Conditions.Add(cond);
            ifstmt.Branches.Add(branch);
            _cstack.Pop();
            _cstack.Push(branch);
        }

        public virtual void Else()
        {
            IfStatement ifstmt = (IfStatement)_sstack.Peek();
            CompoundStatement branch = new CompoundStatement();
            ifstmt.Branches.Add(branch);
            _cstack.Pop();
            _cstack.Push(branch);
        }

        public virtual void EndIf()
        {
            _cstack.Pop();
            _sstack.Pop();
        }

        public virtual LoopBlock Loop()
        {
            LoopBlock block = new LoopBlock();
            CompoundStatement body = new CompoundStatement();
            block.Body = body;
            _sstack.Push(block);
            _cstack.Peek().Statements.Add(block);
            _cstack.Push(body);
            return block;
        }

        public virtual void Break(LoopBlock loop)
        {
            BreakLoopStatement stmt = new BreakLoopStatement()
            {
                Loop = loop
            };
            _cstack.Peek().Statements.Add(stmt);
        }

        public virtual void Continue(LoopBlock loop)
        {
            ContinueLoopStatement stmt = new ContinueLoopStatement()
            {
                Loop = loop
            };
            _cstack.Peek().Statements.Add(stmt);
        }

        public void EndLoop()
        {
            _cstack.Pop();
            _sstack.Pop();
        }

        public virtual void Solve(EquationSystem eqsys)
        {
            SolveStatement stmt = new SolveStatement()
            {
                EqSys = eqsys
            };
            _cstack.Peek().Statements.Add(stmt);
        }

        public virtual void InlineCall(Function fn, Expression[] inArgs, Variable[] outArgs, bool shareLocals = false)
        {
            StmtVariableReplacer svr = new StmtVariableReplacer();
            for (int i = 0; i < inArgs.Length; i++)
            {
                var v = (Literal)fn.InputVariables[i];
                var v_ = v;
                if (v is Variable)
                    v_ = UniqueVariable((Variable)v);
                svr.AddReplacement(v, v_);
                DeclareLocal((IStorableLiteral)v_);
            }
            foreach (Variable v in fn.LocalVariables)
            {
                if (shareLocals)
                {
                    if (!ResultFunction.LocalVariables.Contains(v))
                        DeclareLocal(v);
                }
                else
                {
                    Variable v_ = UniqueVariable(v);
                    svr.AddReplacement(v, v_);
                    DeclareLocal(v_);
                }
            }
            for (int i = 0; i < outArgs.Length; i++)
            {
                var v = fn.OutputVariables[i];
                var v_ = outArgs[i];
                svr.AddReplacement((Literal)v, v_);
            }
            for (int i = 0; i < inArgs.Length; i++)
            {
                IStorableLiteral v_ = (IStorableLiteral)svr.Lookup((Literal)fn.InputVariables[i]);
                Expression vlr = (Variable)v_;
                if (!vlr.Equals(inArgs[i]))
                    Store(v_, inArgs[i]);
            }
            Statement body = fn.Body.Clone;
            body.Accept(svr);
            Variable returnVariable;
            Statement inlined = body.RemoveRets(out returnVariable);
            if (returnVariable != null)
                DeclareLocal(returnVariable);
            _cstack.Peek().Statements.Add(inlined);
        }

        public virtual void InlineCall(Statement stmt)
        {
            Variable returnVariable;
            Statement inlined = stmt.RemoveRets(out returnVariable);
            _cstack.Peek().Statements.Add(inlined);
        }

        public virtual CaseStatement Switch(Expression selector)
        {
            CaseStatement cstmt = new CaseStatement()
            {
                Selector = selector
            };
            _cstack.Peek().Statements.Add(cstmt);
            _sstack.Push(cstmt);
            return cstmt;
        }

        public virtual void Case(Expression cond)
        {
            CaseStatement cstmt = (CaseStatement)_sstack.Peek();
            CompoundStatement branch = new CompoundStatement();
            cstmt.Cases.Add(cond);
            cstmt.Branches.Add(branch);
            _cstack.Push(branch);
        }

        public void DefaultCase()
        {
            CaseStatement cstmt = (CaseStatement)_sstack.Peek();
            CompoundStatement branch = new CompoundStatement();
            cstmt.Branches.Add(branch);
            _cstack.Push(branch);
        }

        public virtual void GotoCase(CaseStatement cstmt, int index)
        {
            GotoCaseStatement gstmt = new GotoCaseStatement()
            {
                CaseStmt = cstmt,
                TargetIndex = index
            };
            _cstack.Peek().Statements.Add(gstmt);
        }

        public virtual void Break(CaseStatement stmt)
        {
            BreakCaseStatement bstmt = new BreakCaseStatement()
            {
                CaseStmt = stmt,
            };
            _cstack.Peek().Statements.Add(bstmt);
        }

        public virtual void EndCase()
        {
            _cstack.Pop();
        }

        public virtual void EndSwitch()
        {
            _sstack.Pop();
        }

        public virtual GotoStatement Goto()
        {
            GotoStatement stmt = new GotoStatement();
            _cstack.Peek().Statements.Add(stmt);
            return stmt;
        }

        public virtual void Return()
        {
            ReturnStatement stmt = new ReturnStatement();
            _cstack.Peek().Statements.Add(stmt);
        }

        public virtual void Return(Expression returnValue)
        {
            ReturnStatement stmt = new ReturnStatement()
            {
                ReturnValue = returnValue
            };
            _cstack.Peek().Statements.Add(stmt);
        }

        public virtual void Throw(Expression expr)
        {
            ThrowStatement stmt = new ThrowStatement()
            {
                ThrowExpr = expr
            };
            _cstack.Peek().Statements.Add(stmt);
        }

        public virtual void Call(ICallable callee, Expression[] arguments)
        {
            CallStatement stmt = new CallStatement()
            {
                Callee = callee,
                Arguments = arguments
            };
            _cstack.Peek().Statements.Add(stmt);
        }

        public virtual void Nop()
        {
            NopStatement stmt = new NopStatement();
            _cstack.Peek().Statements.Add(stmt);
        }

        protected Variable CreateUniqueLocal(string prefix, TypeDescriptor type)
        {
            Variable v = UniqueVariable(new Variable(type)
            {
                Name = prefix
            });
            DeclareLocal(v);
            return v;
        }

        protected Variable UniqueVariable(Variable var)
        {
            int count = 0;
            do
            {
                string tname = (count == 0) ? var.Name : var.Name + "_" + count;
                ++count;
                Variable tvar = new Variable(var.Type)
                {
                    Name = tname
                };
                if (ResultFunction.LocalVariables.Contains(tvar))
                    continue;
                if (ResultFunction.InputVariables.Contains(tvar))
                    continue;
                if (ResultFunction.OutputVariables.Contains(tvar))
                    continue;

                return tvar;
            } while (true);
        }

        public virtual void DeclareLocal(IStorableLiteral var)
        {
            Contract.Assume(_cstack.Count > 0);

            _cstack.Peek().Locals.Add(var);
            ResultFunction.LocalVariables.Add(var);
        }

        protected void DeclareThis(Variable var)
        {
            ResultFunction.ThisVariable = var;
        }

        protected void DeclareInput(IStorableLiteral var)
        {
            ResultFunction.InputVariables.Add(var);
        }

        protected void DeclareOutput(IStorableLiteral var)
        {
            ResultFunction.OutputVariables.Add(var);
        }

        public bool HaveAnyStatement
        {
            get { return _cstack.Peek().Statements.Any(); }
        }

        public Statement LastStatement
        {
            get { return _cstack.Peek().Statements.Last(); }
        }

        public void RemoveLastStatement()
        {
            List<Statement> stmts = _cstack.Peek().Statements;
            stmts.RemoveAt(stmts.Count - 1);
        }

        public Statement TopStatement
        {
            get { return _sstack.Peek(); }
        }

        public void Comment(string comment)
        {
            CompoundStatement top = _cstack.Peek();
            if (top.Statements.Any())
                top.Statements.Last().Comment = comment;
            else
                top.Comment = comment;
        }
    }

    class DependendAlgorithmBuilder: 
        AbstractAlgorithmBuilder
    {
        private Function _func;

        public DependendAlgorithmBuilder(Function func, CompoundStatement root)
        {
            _func = func;
            Reset(root);
        }

        public override Function ResultFunction
        {
            get { return _func; }
        }
    }

    public abstract class AlgorithmTemplate :
        AbstractAlgorithmBuilder,
        IFunctionBuilder
    {
        private Function _func;

        public AlgorithmTemplate()
        {
        }

        protected void Reset()
        {
            base.Reset(new CompoundStatement());
            _func = new Function();
        }

        protected abstract void DeclareAlgorithm();

        protected virtual string FunctionName
        {
            get { return null; }
        }

        public virtual Function GetAlgorithm()
        {
            Reset();
            DeclareAlgorithm();
            CompoundStatement stmt = (CompoundStatement)TopStatement;
            stmt.Statements.Add(new NopStatement());
            StatementLinker.Link(stmt);
            _func.Body = stmt;
            _func.Name = FunctionName;
            return _func;
        }

        public override Function ResultFunction
        {
            get { return _func; }
        }
    }

    public class DefaultAlgorithmBuilder : AbstractAlgorithmBuilder
    {
        public DefaultAlgorithmBuilder()
        {
            _func = new Function();
            Reset(new CompoundStatement());
        }

        private Function _func;

        public override Function ResultFunction
        {
            get { return _func; }
        }

        public Function Complete()
        {
            CompoundStatement stmt = (CompoundStatement)TopStatement;
            stmt.Statements.Add(new NopStatement());
            StatementLinker.Link(stmt);
            _func.Body = stmt;
            return _func;
        }
    }

    public static class MultiStatementAcceptance
    {
        public static void Accept(this IEnumerable<Statement> stmts, IStatementVisitor visitor)
        {
            foreach (Statement stmt in stmts)
            {
                stmt.Accept(visitor);
            }
        }
    }

    class NopRemover : IStatementVisitor
    {
        public enum EPass
        {
            Preprocess,
            Remove
        }

        private HashSet<Statement> _gotoTargets = new HashSet<Statement>();

        public EPass Pass { get; set; }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            switch (Pass)
            {
                case EPass.Preprocess:
                    stmt.Statements.Accept(this);
                    break;

                case EPass.Remove:
                    stmt.Statements.RemoveAll(
                        s => s is NopStatement && !_gotoTargets.Contains(s));
                    break;
            }
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
            switch (Pass)
            {
                case EPass.Preprocess:
                    _gotoTargets.Add(stmt.Target);
                    break;

                default:
                    break;
            }
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

    public static class NopRemoval
    {
        public static void RemoveNops(this Statement stmt)
        {
            NopRemover nrm = new NopRemover();
            stmt.Accept(nrm);
            nrm.Pass = NopRemover.EPass.Remove;
            stmt.Accept(nrm);
        }
    }

    public class NotConvertibleToInlineExpressionException : Exception
    {
    }

    class InlineExpressionConverter : IStatementVisitor
    {
        public Expression Result { get; private set; }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            if (stmt.Statements.Count == 1)
                stmt.Statements.Single().Accept(this);
            else
                throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptIf(IfStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptCase(CaseStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptStore(StoreStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptNop(NopStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            Result = stmt.ReturnValue;
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        public void AcceptCall(CallStatement stmt)
        {
            throw new NotConvertibleToInlineExpressionException();
        }

        #endregion
    }

    public static class InlineExpressionConversion
    {
        public static Expression ToInlineExpression(this Statement stmt)
        {
            stmt.RemoveNops();
            InlineExpressionConverter iec = new InlineExpressionConverter();
            stmt.Accept(iec);
            return iec.Result;
        }
    }
}

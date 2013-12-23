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
    /// <summary>
    /// Visitor pattern interface for statements.
    /// </summary>
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

    /// <summary>
    /// Abstract base class for a SysDOM statement.
    /// </summary>
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

        /// <summary>
        /// Accepts a statement visitor.
        /// </summary>
        /// <param name="visitor">visitor to accept</param>
        public abstract void Accept(IStatementVisitor visitor);

        /// <summary>
        /// The following statement.
        /// </summary>
        public Statement Successor { get; internal set; }

        /// <summary>
        /// The CIL bytecode offset of the statement.
        /// </summary>
        public int ProgramCounter { get; internal set; }

        /// <summary>
        /// Gets or sets a label which is used for branching instructions.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets an elimination predicate, i.e. a function delegate which returns <c>true</c>
        /// if the statement should be eliminated from the output.
        /// </summary>
        public EliminationPredicateFunc EliminationPredicate { get; set; }

        /// <summary>
        /// Comment on the statement.
        /// </summary>
        public string Comment { get; internal set; }

        /// <summary>
        /// Accepts a visitor, but only if the statement is not subject to elimination.
        /// </summary>
        /// <param name="visitor">visitor to accept</param>
        public void AcceptIfEnabled(IStatementVisitor visitor)
        {
            if (!IsEliminated)
                Accept(visitor);
        }

        /// <summary>
        /// Evaluates <c>EliminationPredicate</c> and returns the outcome.
        /// </summary>
        public bool IsEliminated
        {
            get { return EliminationPredicate(); }
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public Statement()
        {
            EliminationPredicate = () => false;
        }

        /// <summary>
        /// Returns a clone of this statement.
        /// </summary>
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

        /// <summary>
        /// Computes a user-readable textual representation of this statement.
        /// </summary>
        /// <param name="indent">indentation level</param>
        /// <param name="sb">string builder to render output to</param>
        public virtual void ToString(int indent, StringBuilder sb)
        {
            if (Label != null)
                sb.AppendLine(Label + ":");
        }

        /// <summary>
        /// Appends a user-readable textual representation of all attributes.
        /// </summary>
        /// <param name="sb">string builder to render output to</param>
        protected void AppendAttributes(StringBuilder sb)
        {
            if (Attributes.Any())
            {
                sb.Append(" {");
                sb.Append(string.Join(", ", Attributes));
                sb.Append("}");
            }
        }

        /// <summary>
        /// Appends whitespaces to the output, reflecting the given indentation level.
        /// </summary>
        /// <param name="n">indentation level</param>
        /// <param name="sb">string builder to render output to</param>
        protected void Indent(int n, StringBuilder sb)
        {
            for (int i = 0; i < n; i++)
                sb.Append("  ");
        }

        /// <summary>
        /// Performs a consistency check on the statement.
        /// </summary>
        public void CheckConsistency()
        {
            Accept(new StatementConsistencyChecker());
        }

        /// <summary>
        /// Replaces all expressions that match a certain predicate.
        /// </summary>
        /// <param name="matchFn">match predicate</param>
        /// <param name="exprGen">expression generator</param>
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

    /// <summary>
    /// Abstract base class for statements which have their own scope for local variables.
    /// </summary>
    public abstract class ScopedStatement: Statement
    {
        public ScopedStatement()
        {
            Locals = new List<IStorableLiteral>();
        }

        public List<IStorableLiteral> Locals { get; private set; }
    }

    [Obsolete("part of an out-dated concept")]
    public abstract class MetaStatement : Statement
    {
    }

    /// <summary>
    /// A composition of other statements with sequential execution semantics.
    /// </summary>
    public class CompoundStatement : ScopedStatement
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public CompoundStatement()
        {
            Statements = new List<Statement>();
        }

        /// <summary>
        /// Returns a list of all statements which are part of this statement.
        /// </summary>
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
    /// Represents a loop.
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

    /// <summary>
    /// A simple statement does never have any inner statement(s).
    /// </summary>
    public abstract class SimpleStatement : Statement
    {
    }

    /// <summary>
    /// A branch statement hands the program flow over to some different program location.
    /// </summary>
    public abstract class BranchStatement : SimpleStatement
    {
    }

    /// <summary>
    /// Abstract base class for "break loop" and "continue loop".
    /// </summary>
    public abstract class LoopControlStatement : BranchStatement
    {
        /// <summary>
        /// The loop this statement refers to.
        /// </summary>
        public LoopBlock Loop { get; set; }
    }

    /// <summary>
    /// Models a "break loop" statement.
    /// </summary>
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
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Models a "continue loop" statement.
    /// </summary>
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
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Models a "break case" statement.
    /// </summary>
    public class BreakCaseStatement : BranchStatement
    {
        /// <summary>
        /// The case block this statement refers to.
        /// </summary>
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

    /// <summary>
    /// Models to "goto case" statement.
    /// </summary>
    public class GotoCaseStatement : BranchStatement
    {
        /// <summary>
        /// The case block this statement refers to.
        /// </summary>
        public CaseStatement CaseStmt { get; set; }

        /// <summary>
        /// The target case branch index. First branch has index 0, second has index 1, and so on.
        /// </summary>
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

    /// <summary>
    /// Models a "goto" statement.
    /// </summary>
    public class GotoStatement : BranchStatement
    {
        /// <summary>
        /// The "goto" target.
        /// </summary>
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

    /// <summary>
    /// Models a "return from procedure/function" statement.
    /// </summary>
    public class ReturnStatement: BranchStatement
    {
        /// <summary>
        /// The return value expression, if any.
        /// </summary>
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

    /// <summary>
    /// Models a "throw exception" statement.
    /// </summary>
    public class ThrowStatement : BranchStatement
    {
        /// <summary>
        /// The thrown expression.
        /// </summary>
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

    /// <summary>
    /// Models a function/procedure call statement.
    /// </summary>
    public class CallStatement : SimpleStatement
    {
        /// <summary>
        /// The called function/procedure.
        /// </summary>
        public ICallable Callee { get; set; }

        /// <summary>
        /// The call arguments.
        /// </summary>
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

    /// <summary>
    /// Models an "if-then-elsif-...-else" statement.
    /// </summary>
    public class IfStatement : Statement
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public IfStatement()
        {
            Conditions = new List<Expression>();
            Branches = new List<Statement>();
        }

        /// <summary>
        /// The list of if/elsif conditions.
        /// </summary>
        public List<Expression> Conditions { get; private set; }

        /// <summary>
        /// The list of then/elsif/else branches.
        /// </summary>
        /// <remarks>
        /// For the statement to be valid, it must hold that the count of branches is equal to or one more than 
        /// the count of conditions. If the counts are equal, this means that the "else" branch is omitted.
        /// </remarks>
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

    /// <summary>
    /// Models a "case select" statement.
    /// </summary>
    public class CaseStatement : Statement
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public CaseStatement()
        {
            Cases = new List<Expression>();
            Branches = new List<Statement>();
        }

        /// <summary>
        /// The selector expression.
        /// </summary>
        public Expression Selector { get; set; }

        /// <summary>
        /// The list of case conditions.
        /// </summary>
        public List<Expression> Cases { get; private set; }

        /// <summary>
        /// The list of case actions.
        /// </summary>
        /// <remarks>
        /// For the statement to be valid, it must hold that the count of branches is equal to or one more than 
        /// the count of case conditions. If there is one more branch than conditions, the last branch is interpreted
        /// as default action.
        /// </remarks>
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

        /// <summary>
        /// Converts this statement to if-then-else form.
        /// </summary>
        /// <returns>semantically equivalent if-then-else form</returns>
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

    [Obsolete("part of an out-dated concept")]
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

    /// <summary>
    /// Models a field/variable assignment or a signal transfer.
    /// </summary>
    public class StoreStatement: SimpleStatement
    {
        /// <summary>
        /// The left-hand side: assignment target.
        /// </summary>
        public IStorableLiteral Container { get; set; }

        /// <summary>
        /// The right-hand side expression of assignment.
        /// </summary>
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

    /// <summary>
    /// Models a "do nothing" statement.
    /// </summary>
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

    /// <summary>
    /// Analyzes the statement tree and determines the <c>Successor</c> property of each statement.
    /// </summary>
    public static class StatementLinker
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

        /// <summary>
        /// Analyzes the statement tree and determines the <c>Successor</c> property of each statement.
        /// </summary>
        /// <param name="stmt">statement to complete</param>
        public static void Link(Statement stmt)
        {
            stmt.Accept(new Visitor());
        }
    }

    /// <summary>
    /// This statement visitor is capable of replacing each occurence of a certain literal
    /// with a different one.
    /// </summary>
    public class StmtVariableReplacer : IStatementVisitor
    {
        private Dictionary<Literal, Literal> _rplMap = new Dictionary<Literal, Literal>();

        /// <summary>
        /// Adds a replacement rule.
        /// </summary>
        /// <param name="v">literal to replace</param>
        /// <param name="v_">replacement</param>
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

    class StatementConsistencyChecker : IStatementVisitor
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

    /// <summary>
    /// Abstract base class for a SysDOM function.
    /// </summary>
    public abstract class FunctionBase : AttributedObject
    {
        /// <summary>
        /// Gets or sets the function name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the variable which is semantically equivalent to "this".
        /// </summary>
        public Variable ThisVariable { get; set; }

        /// <summary>
        /// The list of function input variables.
        /// </summary>
        public List<IStorableLiteral> InputVariables { get; private set; }

        /// <summary>
        /// The list of function output variables.
        /// </summary>
        public List<IStorableLiteral> OutputVariables { get; private set; }

        /// <summary>
        /// The list of local variables.
        /// </summary>
        public List<IStorableLiteral> LocalVariables { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public FunctionBase()
        {
            InputVariables = new List<IStorableLiteral>();
            OutputVariables = new List<IStorableLiteral>();
            LocalVariables = new List<IStorableLiteral>();
        }

        /// <summary>
        /// Performs a consistency check.
        /// </summary>
        public abstract void CheckConsistency();
    }

    /// <summary>
    /// Models a SysDOM function.
    /// </summary>
    public class Function : FunctionBase
    {
        /// <summary>
        /// The function body.
        /// </summary>
        public Statement Body { get; set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
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

    /// <summary>
    /// A state function has multiple entry points, whereby an internal state
    /// determines which of them to take upon function entry.
    /// </summary>
    public class StateFunction : FunctionBase
    {
        /// <summary>
        /// Gets or sets the state bodies.
        /// </summary>
        public Statement[] States { get; set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
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

    /// <summary>
    /// Algorithm builder interface for meta-programming.
    /// </summary>
    [ContractClass(typeof(AlgorithmBuilderContractClass))]
    public interface IAlgorithmBuilder
    {
        /// <summary>
        /// Declares a local variable.
        /// </summary>
        /// <param name="v">literal to take as local variable</param>
        void DeclareLocal(IStorableLiteral v);

        /// <summary>
        /// Emits an assignment.
        /// </summary>
        /// <param name="var">left-hand side</param>
        /// <param name="val">right-hand side</param>
        void Store(IStorableLiteral var, Expression val);

        /// <summary>
        /// Begins an "if-then(-else-if)(-else)" statement.
        /// </summary>
        /// <param name="cond">condition</param>
        /// <returns>the newly created "if-then(-else)" statement</returns>
        IfStatement If(Expression cond);

        /// <summary>
        /// Adds an "else-if" branch to the current "if-then(-elsif)(-else)" statement.
        /// </summary>
        /// <param name="cond">condition</param>
        void ElseIf(Expression cond);

        /// <summary>
        /// Adds an "else" branch to the current "if-then(-elsif)(-else)" statement.
        /// </summary>
        void Else();

        /// <summary>
        /// Ends the current "if-then(-elsif)(-else)" statement.
        /// </summary>
        void EndIf();

        /// <summary>
        /// Begins a loop statement.
        /// </summary>
        /// <returns>the newly created loop statement</returns>
        LoopBlock Loop();
        void Break(LoopBlock loop);

        /// <summary>
        /// Emits a "continue loop" statement.
        /// </summary>
        /// <param name="loop">the loop to continue</param>
        void Continue(LoopBlock loop);

        /// <summary>
        /// Ends the current loop statement.
        /// </summary>
        void EndLoop();

        [Obsolete("part of an out-dated concept.")]
        void Solve(EquationSystem eqsys);

        /// <summary>
        /// Inlines a function call.
        /// </summary>
        /// <param name="fn">function to call</param>
        /// <param name="inArgs">input arguments</param>
        /// <param name="outArgs">output arguments</param>
        /// <param name="shareLocals">whether the inlined function may share and access the present local variables</param>
        void InlineCall(Function fn, Expression[] inArgs, Variable[] outArgs, bool shareLocals = false);

        /// <summary>
        /// Begins a "switch-case" statement.
        /// </summary>
        /// <param name="selector">selector expression</param>
        /// <returns>the newly created "switch-case" statement</returns>
        CaseStatement Switch(Expression selector);

        /// <summary>
        /// Adds a case to the current "switch-case" statement.
        /// </summary>
        /// <param name="cond">case condition</param>
        void Case(Expression cond);

        /// <summary>
        /// Adds a default case to the current "switch-case" statement.
        /// </summary>
        void DefaultCase();

        /// <summary>
        /// Hands program flow over to a different branch of the a "switch-case" statement.
        /// </summary>
        /// <param name="cstmt">referred "switch-case" statement</param>
        /// <param name="index">0-based branch index</param>
        void GotoCase(CaseStatement cstmt, int index);

        /// <summary>
        /// Emits a "break case" statement.
        /// </summary>
        /// <param name="stmt">referred "switch-case" statement</param>
        void Break(CaseStatement stmt);

        /// <summary>
        /// Ends the current case of the current "switch-case" statement.
        /// </summary>
        void EndCase();

        /// <summary>
        /// Ends the current "switch-case" statement.
        /// </summary>
        void EndSwitch();

        /// <summary>
        /// Emits a "goto" statement.
        /// </summary>
        /// <returns>the newly created "goto" statement</returns>
        GotoStatement Goto();

        /// <summary>
        /// Emits a "return" statement.
        /// </summary>
        void Return();

        /// <summary>
        /// Emits a "return" statement.
        /// </summary>
        /// <param name="returnValue">expression describing the return value</param>
        void Return(Expression returnValue);

        /// <summary>
        /// Emits a "throw exception" statement.
        /// </summary>
        /// <param name="expr">expression describing the exception to throw</param>
        void Throw(Expression expr);

        /// <summary>
        /// Emits a function call statement.
        /// </summary>
        /// <param name="callee">function to call</param>
        /// <param name="arguments">arguments to pass</param>
        void Call(ICallable callee, params Expression[] arguments);

        /// <summary>
        /// Emits a "do nothing" statement.
        /// </summary>
        void Nop();

        /// <summary>
        /// Returns the last output statement.
        /// </summary>
        Statement LastStatement { get; }

        /// <summary>
        /// Removes the last output statement.
        /// </summary>
        void RemoveLastStatement();

        /// <summary>
        /// Returns <c>true</c> if there already exists any output statement.
        /// </summary>
        bool HaveAnyStatement { get; }

        /// <summary>
        /// Forks a new algorithm builder at the current position. 
        /// To achieve this, a compound statement is inserted as placeholder.
        /// </summary>
        /// <returns>the new algorithm builder</returns>
        IAlgorithmBuilder BeginSubAlgorithm();

        /// <summary>
        /// Adds a comment to the last output statement.
        /// </summary>
        /// <param name="comment">comment text</param>
        void Comment(string comment);
    }

    /// <summary>
    /// Extends the algorithm builder interface by the capability of creating a SysDOM function.
    /// </summary>
    public interface IFunctionBuilder: IAlgorithmBuilder
    {
        /// <summary>
        /// Returns the resulting SysDOM function.
        /// </summary>
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

    /// <summary>
    /// Provides an abstract base implementation of the <c>IFunctionBuilder</c> interface.
    /// </summary>
    public abstract class AbstractAlgorithmBuilder :
        IFunctionBuilder
    {
        private CompoundStatement _stmts = new CompoundStatement();
        private Stack<CompoundStatement> _cstack = new Stack<CompoundStatement>();
        private Stack<Statement> _sstack = new Stack<Statement>();

        /// <summary>
        /// Must be overridden to provide the result function.
        /// </summary>
        public abstract Function ResultFunction { get; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public AbstractAlgorithmBuilder()
        {
        }

        /// <summary>
        /// Resets the currently active statement hierarchy.
        /// </summary>
        /// <param name="root">new statement to take as root statement</param>
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

        /// <summary>
        /// Creates a new local variable.
        /// </summary>
        /// <param name="prefix">name prefix</param>
        /// <param name="type">descriptor of variable type</param>
        /// <returns>the newly created local variable</returns>
        protected Variable CreateUniqueLocal(string prefix, TypeDescriptor type)
        {
            Variable v = UniqueVariable(new Variable(type)
            {
                Name = prefix
            });
            DeclareLocal(v);
            return v;
        }

        /// <summary>
        /// Ensures that a given local variable does not have the same name as any existing input argument,
        /// local variable or output argument.
        /// </summary>
        /// <param name="var">variable to check</param>
        /// <returns>the passed variable if it is new or a newly created variable with same type but different name</returns>
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

        /// <summary>
        /// Indicates which variable should be semantically equivalent to "this".
        /// </summary>
        /// <param name="var">"this" variable</param>
        protected void DeclareThis(Variable var)
        {
            ResultFunction.ThisVariable = var;
        }

        /// <summary>
        /// Declares a literal as input argument.
        /// </summary>
        /// <param name="var">literal to declare as input argument</param>
        protected void DeclareInput(IStorableLiteral var)
        {
            ResultFunction.InputVariables.Add(var);
        }

        /// <summary>
        /// Declares a literal as output argument.
        /// </summary>
        /// <param name="var">literal to declare as output argument</param>
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

        /// <summary>
        /// Returns the top-level statement.
        /// </summary>
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

    /// <summary>
    /// An abstract algorithm builder implementation where the actual algorithm construction is
    /// done inside the to-be-overridden <c>DeclareAlgorithm</c> method.
    /// </summary>
    public abstract class AlgorithmTemplate :
        AbstractAlgorithmBuilder,
        IFunctionBuilder
    {
        private Function _func;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public AlgorithmTemplate()
        {
        }

        protected void Reset()
        {
            base.Reset(new CompoundStatement());
            _func = new Function();
        }

        /// <summary>
        /// Constructs the actual algorithm. Must be overridden.
        /// </summary>
        protected abstract void DeclareAlgorithm();

        /// <summary>
        /// Returns the function name. The default implementation returns <c>null</c>.
        /// </summary>
        protected virtual string FunctionName
        {
            get { return null; }
        }

        /// <summary>
        /// Constructs the function.
        /// </summary>
        /// <returns>the newly constructed function</returns>
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

    /// <summary>
    /// A default implementation of the algorithm builder interface.
    /// </summary>
    public class DefaultAlgorithmBuilder : AbstractAlgorithmBuilder
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public DefaultAlgorithmBuilder()
        {
            _func = new Function();
            Reset(new CompoundStatement());
        }

        private Function _func;

        /// <summary>
        /// Returns the resulting function.
        /// </summary>
        public override Function ResultFunction
        {
            get { return _func; }
        }

        /// <summary>
        /// Constructs the function.
        /// </summary>
        /// <returns>the newly constructed function</returns>
        public Function Complete()
        {
            CompoundStatement stmt = (CompoundStatement)TopStatement;
            stmt.Statements.Add(new NopStatement());
            StatementLinker.Link(stmt);
            _func.Body = stmt;
            return _func;
        }
    }

    /// <summary>
    /// This static class provides a helper extension for applying the visitor pattern to multiple statements at once.
    /// </summary>
    public static class MultiStatementAcceptance
    {
        /// <summary>
        /// Applies a visitor to each statement inside the enumeration.
        /// </summary>
        /// <param name="stmts">statements enumeration</param>
        /// <param name="visitor">visitor to apply</param>
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

    /// <summary>
    /// This static class provides a helper extension for removing all inner "do nothing" statements from a
    /// given statement.
    /// </summary>
    public static class NopRemoval
    {
        /// <summary>
        /// Removes all inner "do nothing" statements.
        /// </summary>
        /// <param name="stmt">the statement from which to remove all inner "do nothing" statements</param>
        public static void RemoveNops(this Statement stmt)
        {
            NopRemover nrm = new NopRemover();
            stmt.Accept(nrm);
            nrm.Pass = NopRemover.EPass.Remove;
            stmt.Accept(nrm);
        }
    }

    /// <summary>
    /// Thrown when the conversion to an inline expression is not possible.
    /// </summary>
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

    /// <summary>
    /// This static class provides a service for converting a statement to an inline expression.
    /// </summary>
    public static class InlineExpressionConversion
    {
        /// <summary>
        /// Converts the statement to an inline expression.
        /// </summary>
        /// <param name="stmt">statement to convert</param>
        /// <returns>semantically equivalent inline expression</returns>
        /// <exception cref="NotConvertibleToInlineExpressionException">if the statement is not convertible to an inline expression</exception>
        public static Expression ToInlineExpression(this Statement stmt)
        {
            stmt.RemoveNops();
            InlineExpressionConverter iec = new InlineExpressionConverter();
            stmt.Accept(iec);
            return iec.Result;
        }
    }
}

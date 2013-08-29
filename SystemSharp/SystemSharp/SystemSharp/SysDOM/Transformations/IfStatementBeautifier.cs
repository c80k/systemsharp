/**
 * Copyright 2011-2012 Christian Köllner
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
    class EmptyStatementDetector :
        IStatementVisitor
    {
        public bool IsEmpty { get; private set; }

        public EmptyStatementDetector()
        {
            IsEmpty = true;
        }

        #region IStatementVisitor Member

        public void AcceptCompoundStatement(CompoundStatement stmt)
        {
            foreach (Statement child in stmt.Statements)
                child.Accept(this);
        }

        public void AcceptLoopBlock(LoopBlock stmt)
        {
            IsEmpty = false;
        }

        public void AcceptBreakLoop(BreakLoopStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptContinueLoop(ContinueLoopStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptIf(IfStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptCase(CaseStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptStore(StoreStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptNop(NopStatement stmt)
        {
        }

        public void AcceptSolve(SolveStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptBreakCase(BreakCaseStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptGotoCase(GotoCaseStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptGoto(GotoStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptReturn(ReturnStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptThrow(ThrowStatement stmt)
        {
            IsEmpty = false;
        }

        public void AcceptCall(CallStatement stmt)
        {
            IsEmpty = false;
        }

        #endregion
    }

    public static class EmptyStatementDetection
    {
        /// <summary>
        /// Determines whether a given statement is empty, that is if it does not perform any operation.
        /// </summary>
        /// <param name="stmt">>A statement</param>
        /// <returns>true if the statement is empty, false if not</returns>
        public static bool IsEmpty(this Statement stmt)
        {
            EmptyStatementDetector esd = new EmptyStatementDetector();
            stmt.Accept(esd);
            return esd.IsEmpty;
        }
    }

    class IfStatementBeautifier: 
        DefaultTransformer,
        IStatementVisitor
    {
        private Statement _root;

        public IfStatementBeautifier(Statement root)
        {
            _root = root;
        }

        protected override Statement Root
        {
            get { return _root; }
        }

        protected override void DeclareAlgorithm()
        {
            _root.Accept(this);
        }

        public override void AcceptIf(IfStatement stmt)
        {
            if (stmt.Conditions.Count == 1 &&
                stmt.Branches.Count == 2 &&
                stmt.Branches[0].IsEmpty())
            {
                /* This case models the following situation:
                 * 
                 * if (someCondition)
                 * {
                 *   // empty
                 * }
                 * else
                 * {
                 *    someActions();
                 * }
                 * 
                 * Thus, it is rewritten to the following statement:
                 * 
                 * if (!someCondition)
                 * {
                 *    someActions();
                 * }
                 * */

                If((!stmt.Conditions[0]).Simplify());
                {
                    stmt.Branches[1].Accept(this);
                }
                EndIf();
            }
            else
            {
                Expression c0 = stmt.Conditions[0].Simplify();
                Matching x = new Matching();
                Matching not_x = !x;
                if (c0.Match(not_x) == c0 &&
                    stmt.Conditions.Count == 1 &&
                    stmt.Branches.Count == 2)
                {
                    /* This case models the following situation:
                     * 
                     * if (!someCondition)
                     * {
                     *   someThenAction();
                     * }
                     * else
                     * {
                     *   someElseAction();
                     * }
                     * 
                     * Thus, it is rewritten to the following statement:
                     * 
                     * if (someCondition)
                     * {
                     *    someElseAction();
                     * }
                     * else
                     * {
                     *    someThenAction();
                     * }
                     * */
                    If(c0.Children[0]);
                    {
                        stmt.Branches[1].Accept(this);
                    }
                    Else();
                    {
                        stmt.Branches[0].Accept(this);
                    }
                    EndIf();
                }
                else
                {
                    If(c0);
                    {
                        stmt.Branches[0].Accept(this);
                    }
                    for (int i = 1; i < stmt.Conditions.Count; i++)
                    {
                        ElseIf(stmt.Conditions[i].Simplify());
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
                }
            }
            CopyAttributesToLastStatement(stmt);
        }

    }

    public static class IfStatementBeautification
    {
        public static Statement BeautifyIfStatements(this Statement stmt)
        {
            IfStatementBeautifier isb = new IfStatementBeautifier(stmt);
            Function result = isb.GetAlgorithm();
            return result.Body;
        }
    }
}

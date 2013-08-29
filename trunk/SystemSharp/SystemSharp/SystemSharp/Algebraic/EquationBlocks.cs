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
using SystemSharp.Algebraic;
using SystemSharp.SysDOM;
using SystemSharp.Meta;
using GraphAlgorithms;

namespace SystemSharp.Algebraic
{
    public class EquationSystem
    {
        public EquationSystem()
        {
            Variables = new List<Variable>();
            Equations = new List<Expression>();
        }

        public List<Variable> Variables { get; private set; }
        public List<Expression> Equations { get; private set; }

        private Matrix _jac;
        private Expression[] _residual;

        public Matrix Jacobian
        {
            get
            {
                if (_jac == null)
                {
                    TypeDescriptor type;
                    foreach (Expression eq in Equations)
                        type = eq.ResultType;

                    _jac = new Matrix(Equations.Count, Variables.Count);
                    for (int i = 0; i < Equations.Count; i++)
                    {
                        for (int j = 0; j < Variables.Count; j++)
                        {
                            _jac[i, j] = Equations[i].Derive(Variables[j]).Simplify();
                            type = _jac[i,j].ResultType;
                        }
                    }
                }
                return _jac;
            }
        }

        public bool IsLinear
        {
            get
            {
                IEnumerable<LiteralReference> lrs =
                    from Variable v in Variables
                    select (LiteralReference)v;

                Matrix jac = Jacobian;
                for (int i = 0; i < jac.NumRows; i++)
                {
                    for (int j = 0; j < jac.NumCols; j++)
                    {
                        Expression e = jac[i, j];
                        if (e.ExtractLiteralReferences().Intersect(lrs).Count() > 0)
                            return false;
                    }
                }
                return true;
            }
        }

        public Expression[] Residual
        {
            get
            {
                if (_residual == null)
                {
                    _residual = new Expression[Equations.Count];
                    for (int i = 0; i < _residual.Length; i++)
                        _residual[i] = Equations[i];

                    for (int j = 0; j < Variables.Count; j++)
                    {
                        Variable v = Variables[j];
                        Expression ve = (Expression)v;
                        for (int i = 0; i < _residual.Length; i++)
                        {
                            _residual[i] = _residual[i].Substitute(ve, SpecialConstant.ScalarZero);
                        }
                    }
                }
                return _residual;
            }
        }

        public void CheckConsistency()
        {
            foreach (Expression e in Equations)
                e.CheckConsistency();
        }
    }

    public class EquationBlock
    {
        public EquationBlock()
        {
            EqSys = new EquationSystem();
            Successors = new List<EquationBlock>();
            Predecessors = new List<EquationBlock>();
        }

        public EquationSystem EqSys { get; private set; }
        public List<EquationBlock> Successors { get; private set; }
        public List<EquationBlock> Predecessors { get; private set; }
    }

    public class EquationDecomposition
    {
        private EquationDecomposition(EquationSystem eqsys)
        {
            /** This algorithm decomposes a given equation system into smaller equation blocks
             *  which can be solved subsequently. The algorithm is sometimes referred to as
             *  Tarjan's sorting or Dulmage-Mendelson decomposition.
             *  
             *  The algorithm consists of several steps. The description below will use the
             *  following notions:
             *    Var: The set of variables of the whole equation system
             *    Eqn: The set of equations of the whole equation system
             *    Var u Eqn: The set unification of Var and Eqn
             *  
             *  Step (1): Maximum matching
             *    Define an undirected graph G = (V, E) with V = Var u Eqn.
             *    Whenever an equation eqn \in Eqn references a variable var \in Var
             *    add the edge (eqn, var) to E.
             *    Compute a maximum matching M between variables and equations.
             *    M will now contain a set of equation/variable pairs.
             *    
             * Step (2): Finding strongly connected components
             *    Define a directed graph Gd = (V, Ed) with V = Var u Eqn.
             *    Whenever an equation eqn \in Eqn references a variable var \in Var
             *    add the edge (eqn, var) to Ed.
             *    For each equation/variable pair (eqn, var) in M add furthermore
             *    the edge (var, eqn) to Ed.
             *    Note: Matched equation/variable pairs will result in bi-directional edges in Ed.
             *    Compute the strongly connected components SCC of Gd.
             *    SCC is a set of subsets of V such that each subset represents one component.
             *    
             * Step (3): Building the tree of equation blocks
             *    Defined a directed graph Dt = (SCC, Et).
             *    For each equation eqn \in Eqn which is assigned to component c1 and
             *    references a variable var \in Var which is assigned to component c2
             *    add an edge (c1, c2) to Et of c1 and c2 are different.
             *    
             *    The resulting graph will have a tree/forest sutrcture. Each node
             *    represents an equation block. An edge b1 -> b2 represents a dependency in the
             *    that b2 must be solved prior to b1. A schedule can be found by topological 
             *    sorting the tree.
             * */

            if (eqsys == null ||
                eqsys.Equations == null ||
                eqsys.Variables == null)
                throw new ArgumentException("null reference in equation system argument");

            if (eqsys.Equations.Count != eqsys.Variables.Count)
                throw new ArgumentException("equation system is not well-constrained!");

            /** Assign each equation and each variable an index which will be a unique node
             * ID within the graphs. Indices are assigned as follows:
             *   - Equations get indices from 0...count(equations)-1
             *   - Variables get indices from count(equations)...count(equations)+count(variables)
             *   Note: Assuming a well-constrained equation system, count(equations) = count(variables)
             **/
            int curidx = 0;
            foreach (Expression eq in eqsys.Equations)
            {
                eq.Cookie = curidx++;
            }
            Dictionary<object, int> idx = new Dictionary<object, int>();
            foreach (Variable v in eqsys.Variables)
            {
                idx[v] = curidx++;
            }

            // Steps 1 and 2: Construct G and Gd
            Graph g = new Graph(curidx);
            Digraph dg = new Digraph(curidx);
            for (int i = 0; i < eqsys.Equations.Count; i++)
            {
                Expression eq = eqsys.Equations[i];
                LiteralReference[] lrs = eq.ExtractLiteralReferences();
                foreach (LiteralReference lr in lrs)
                {
                    int j;
                    if (idx.TryGetValue(lr.ReferencedObject, out j))
                    {
                        g.AddEdge(i, j);
                        dg.AddEdge(i, j);
                    }
                }
            }
            // Step 1: Maximum matching
            GraphAlgorithms.Matching m = g.GetMaximumMatching();
            bool success = m.IsMaximumCardinality;
            for (int i = 0; i < eqsys.Equations.Count; i++)
            {
                int k = m[i];
                if (k >= 0)
                    dg.AddEdge(k, i);
            }
            // Step 2: Strongly connected components
            StrongComponents sc = dg.GetStrongComponents();
            int numc = sc.NumComponents;
            EquationBlock[] blocks = new EquationBlock[numc];
            for (int i = 0; i < numc; i++)
            {
                blocks[i] = new EquationBlock();
            }

            // Step 3: Construct the tree
            Digraph dg2 = new Digraph(numc);
            for (int i = 0; i < eqsys.Equations.Count; i++)
            {
                int c = sc[i];
                blocks[c].EqSys.Equations.Add(eqsys.Equations[i]);
                int vi = m[i];
                Variable v = eqsys.Variables[vi - eqsys.Equations.Count];
                blocks[c].EqSys.Variables.Add(v);
                List<int> outset = dg.GetOutSet(i);
                foreach (int ovi in outset)
                {
                    int oc = sc[ovi];
                    if (c != oc)
                        dg2.AddEdge(c, oc);
                }
            }
            List<int> rootSet = new List<int>();
            List<int> sinkSet = new List<int>();
            for (int c = 0; c < numc; c++)
            {
                if (dg2.GetOutDegree(c) == 0)
                    rootSet.Add(c);
                if (dg2.GetInDegree(c) == 0)
                    sinkSet.Add(c);
                List<int> outset = dg2.GetOutSet(c);
                foreach (int oc in outset)
                    blocks[c].Predecessors.Add(blocks[oc]);
                List<int> inset = dg2.GetInSet(c);
                foreach (int ic in inset)
                    blocks[c].Successors.Add(blocks[ic]);
            }
            RootSet = (from int c in rootSet
                       select blocks[c]).ToArray();
            SinkSet = (from int c in sinkSet
                       select blocks[c]).ToArray();
            AllBlocks = blocks;
        }

        public EquationBlock[] RootSet { get; private set; }
        public EquationBlock[] SinkSet { get; private set; }
        public EquationBlock[] AllBlocks { get; private set; }

        public static EquationDecomposition Decompose(EquationSystem eqsys)
        {
            return new EquationDecomposition(eqsys);
        }
    }
}

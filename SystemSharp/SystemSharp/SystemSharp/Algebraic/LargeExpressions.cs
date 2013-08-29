/**
 * Copyright 2011 Christian Köllner
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
using SystemSharp.SysDOM;
using SystemSharp.Meta;

namespace SystemSharp.Algebraic
{
    public delegate Variable CreateVariableFn(string prefix, TypeDescriptor type);

    public class LExManager
    {
        private List<StoreStatement> _tstores = new List<StoreStatement>();
        private int _varCounter;
        public CreateVariableFn CreateVariable { get; set; }

        public LExManager()
        {
            VeilThreshold = 10;
            CreateVariable = DefaultCreateSymbol;
        }

        private Variable CreateSymbol(TypeDescriptor type)
        {
            return CreateVariable("__temp", type);
        }

        private Variable DefaultCreateSymbol(string prefix, TypeDescriptor type)
        {
            string varName = prefix + _varCounter;
            ++_varCounter;
            Variable v = new Variable(type)
            {
                Name = varName
            };
            return v;
        }

        public int VeilThreshold { get; set; }

        public Expression Veil(Expression e)
        {
            if (e.LeafCount < VeilThreshold)
                return e;

            Variable v = CreateSymbol(e.ResultType);
            StoreStatement stmt = new StoreStatement()
            {
                Container = v,
                Value = e
            };
            _tstores.Add(stmt);
            return v;
        }

        public List<StoreStatement> TempExpressions
        {
            get
            {
                return _tstores;
            }
        }
    }

    public class CSE
    {
        private Dictionary<Expression, int> _occMap = new Dictionary<Expression, int>();
        private Dictionary<Expression, Expression> _rplMap = new Dictionary<Expression, Expression>();
        private List<StoreStatement> _stores = new List<StoreStatement>();
        private Dictionary<object, Expression> _cse = new Dictionary<object, Expression>();
        private Dictionary<object, bool> _implemented = new Dictionary<object, bool>();
        private List<StoreStatement> _result = new List<StoreStatement>();

        public CreateVariableFn CreateTempVariable { get; set; }
        public int LeafCountThreshold { get; set; }

        public CSE()
        {
            LeafCountThreshold = 2;
        }

        public void AddExpression(Expression e)
        {
            TypeDescriptor type = e.ResultType;
            if (e.LeafCount < LeafCountThreshold)
                return;

            int count = 0;
            _occMap.TryGetValue(e, out count);
            _occMap[e] = count + 1;

            foreach (Expression ce in e.Children)
                AddExpression(ce);
        }

        public void AddStore(StoreStatement store)
        {
            _stores.Add(store);
            AddExpression(store.Value);
        }

        private void ImplementStore(Variable v, Expression e)
        {
            LiteralReference[] lrs = e.ExtractLiteralReferences();
            foreach (LiteralReference lr in lrs)
            {
                bool flag;
                if (_implemented.TryGetValue(lr.ReferencedObject, out flag) && !flag)
                {
                    ImplementStore((Variable)lr.ReferencedObject, _cse[lr]);
                    _implemented[lr.ReferencedObject] = true;
                }
            }
            _result.Add(new StoreStatement()
            {
                Container = v,
                Value = e
            });
        }

        public List<StoreStatement> ComputeCSE()
        {
            foreach (Expression e in _occMap.Keys)
                _rplMap[e] = e;

            foreach (KeyValuePair<Expression, int> kvp in _occMap)
            {
                if (kvp.Value > 1)
                {
                    Variable vtmp = CreateTempVariable("__cse", kvp.Key.ResultType);
                    _implemented[vtmp] = false;
                    Expression etmp = (Expression)vtmp;
                    _cse[etmp] = null;
                    Dictionary<Expression, Expression> rplMap2 = new Dictionary<Expression, Expression>();
                    foreach (KeyValuePair<Expression, Expression> kvp2 in _rplMap)
                    {
                        rplMap2[kvp2.Key] = kvp2.Value.Substitute(kvp.Key, etmp);
                    }
                    _rplMap = rplMap2;
                }
            }

            foreach (KeyValuePair<Expression, Expression> kvp in _rplMap)
            {
                if (_cse.ContainsKey(kvp.Value))
                    _cse[kvp.Value] = kvp.Key;
            }

            foreach (StoreStatement store in _stores)
            {
                Expression value;
                if (!_rplMap.TryGetValue(store.Value, out value))
                    value = store.Value;
                ImplementStore((Variable)store.Container, value);
            }

            return _result;
        }

        public static List<StoreStatement> DoCSE(IEnumerable<StoreStatement> stmts, CreateVariableFn createTempVar)
        {
            CSE cse = new CSE()
            {
                CreateTempVariable = createTempVar
            };
            foreach (StoreStatement stmt in stmts)
                cse.AddStore(stmt);
            return cse.ComputeCSE();
        }
    }
}


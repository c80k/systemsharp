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
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.Synthesis.VHDLGen
{
    class IntegerIndexTransformer: DefaultTransformer
    {
        private Statement _root;

        public IntegerIndexTransformer(Statement root)
        {
            _root = root;
        }

        protected override Statement Root
        {
            get { return _root; }
        }

        private Expression MakeIntegerResult(Expression ex)
        {
            var arg = ex.Accept(this);
            if (arg.ResultType.CILType.Equals(typeof(int)))
            {
                return arg;
            }
            else
            {
                var iarg = IntrinsicFunctions.Cast(ex, arg.ResultType.CILType, typeof(int));
                return iarg;
            }
        }

        public override SysDOM.Expression TransformFunction(SysDOM.FunctionCall expr)
        {
            var fspec = (FunctionSpec)expr.Callee;
            if (fspec.IntrinsicRep != null)
            {
                var ifun = fspec.IntrinsicRep;
                switch (ifun.Action)
                {
                    case IntrinsicFunction.EAction.GetArrayElement:
                    case IntrinsicFunction.EAction.Index:
                        expr.Arguments = expr.Arguments.Take(1).Concat(
                            expr.Arguments.Skip(1).Select(_ => MakeIntegerResult(_)))
                            .ToArray();
                        return expr;
                }
            }
            return base.TransformFunction(expr);
        }

        public override Expression TransformLiteralReference(LiteralReference expr)
        {
            var sref = expr.ReferencedObject as SignalRef;
            if (sref != null)
            {
                var iaa = sref.Indices.Select(ia => ia.Select(i => MakeIntegerResult(i.Accept(this))).ToArray());
                sref = new SignalRef(
                    sref.Desc,
                    sref.Prop,
                    iaa,
                    sref.IndexSample,
                    sref.IsStaticIndex);
                expr = new LiteralReference(sref, expr.Mode);
            }
            return expr;
        }
    }

    public static class IntegerIndexTransformation
    {
        public static Function MakeIntegerIndices(this Function func)
        {
            var xform = new IntegerIndexTransformer(func.Body);
            return xform.GetAlgorithm();
        }
    }
}

/**
 * Copyright 2013 Christian Köllner
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
using System.Threading.Tasks;

namespace SystemSharp.SysDOM.Transformations
{
    class StreamlineTransformer : DefaultTransformer
    {
        private StateFunction _sfun;

        public StreamlineTransformer(StateFunction sfun)
        {
            _sfun = sfun;
        }

        protected override Statement Root
        {
            get { return _sfun.States[0]; }
        }

        public override void AcceptCall(CallStatement stmt)
        {
            var fspec = stmt.Callee as FunctionSpec;
            if (fspec != null &&
                fspec.IntrinsicRep != null &&
                fspec.IntrinsicRep.Action == IntrinsicFunction.EAction.ProceedWithState)
            {
                int state = (int)fspec.IntrinsicRep.Parameter;
                int index = state + 1;
                _sfun.States[index].Accept(this);
                return;
            }
            base.AcceptCall(stmt);
        }
    }

    public static class StateFunctions
    {
        public static Function Streamline(this StateFunction sfun)
        {
            var xform = new StreamlineTransformer(sfun);
            return xform.GetAlgorithm();
        }
    }
}

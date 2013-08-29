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

namespace SystemSharp.Analysis
{
    public static class MethodOrdering
    {
        private static void OrderDFS(MethodFacts mf, ref int nextIndex)
        {
            if (mf.InDFS)
            {
                mf.CallOrder = int.MaxValue;
                mf.IsRecursive = true;
                return;
            }
            mf.InDFS = true;
            foreach (var callee in mf.CallingMethods.Select(m => FactUniverse.Instance.GetFacts(m)))
            {
                OrderDFS(callee, ref nextIndex);
            }
            mf.InDFS = false;
            if (!mf.Visited)
            {
                mf.CallOrder = nextIndex++;
                mf.Visited = true;
            }
        }

        public static void ComputeCallOrder(IEnumerable<MethodFacts> methods)
        {
            foreach (MethodFacts mf in methods)
                mf.CallOrder = -1;

            var roots = methods.Where(mf => !mf.CalledMethods.Any());
            int nextIndex = 0;
            foreach (var mf in roots)
            {
                OrderDFS(mf, ref nextIndex);
            }
        }
    }
}

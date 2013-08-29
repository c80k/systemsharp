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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.SysDOM;
using SystemSharp.Algebraic;
using SystemSharp.Components.Transactions;
using SystemSharp.Assembler;
using SystemSharp.Analysis;
using SystemSharp.DataTypes;

namespace SystemSharp.Components.FU
{
    public interface IOperandSource
    {
    }

    public class MUXHandle
    {
        public IOperandSource OperandSource { get; private set; }
        public StdLogicVector SelValue { get; internal set; }
        public int Index { get; internal set; }

        internal MUXHandle(IOperandSource source)
        {
            OperandSource = source;
        }
    }

    public abstract class StateEncoding
    {
        public abstract StdLogicVector Encode(long state, long totalStates);
        
        public long GetEncodingWidth(long totalStates)
        {
            return Encode(0, totalStates).Size;
        }
    }

    public class BinaryEncoding : StateEncoding
    {
        public override StdLogicVector Encode(long state, long totalStates)
        {
            int size = (int)Math.Ceiling(Math.Log(totalStates, 2.0));
            return StdLogicVector.FromLong(state, size);
        }
    }

    public class OneHotEncoding : StateEncoding
    {
        public override StdLogicVector Encode(long state, long totalStates)
        {
            return StdLogicVector._0s((int)(totalStates - state - 1)).Concat(
                ((StdLogicVector)"1").Concat(
                StdLogicVector._0s((int)state)));
        }
    }

    public static class StateEncodings
    {
        public static readonly BinaryEncoding Binary = new BinaryEncoding();
        public static readonly OneHotEncoding OneHot = new OneHotEncoding();
    }

    public class MUXBuilder
    {
        private Dictionary<IOperandSource, MUXHandle> _selections = new Dictionary<IOperandSource,MUXHandle>();

        public MUXHandle Select(IOperandSource source)
        {
            MUXHandle result;
            if (!_selections.TryGetValue(source, out result))
                result = new MUXHandle(source);
            return result;
        }

        public int NumInputs
        {
            get
            {
                return _selections.Count;
            }
        }

        public void Encode(StateEncoding enc)
        {
            int index = 0;
            int total = NumInputs;
            foreach (MUXHandle mh in _selections.Values)
            {
                mh.Index = index;
                mh.SelValue = enc.Encode(index, total);
            }
        }
    }
}

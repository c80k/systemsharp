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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.Std
{
    public class RegPipe : Component
    {
        public In<StdLogic> Clk { private get; set; }
        public In<StdLogic> En { private get; set; }
        public In<StdLogicVector> Din { private get; set; }
        public Out<StdLogicVector> Dout { private get; set; }

        private int _width;
        private int _depth;
        private int _belowbit;
        //private Signal1D<StdLogicVector> _stages;
        private SLVSignal _stages;

        public RegPipe(int depth, int width, bool useEn = false)
        {
            Contract.Requires(depth > 0 || !useEn);

            _width = width;
            _depth = depth;
            UseEn = useEn;
            if (depth > 1)
            {
                //_stages = new Signal1D<StdLogicVector>(depth - 1, 
                //    i => new SLVSignal(width) { InitialValue = StdLogicVector._0s(width) });
                int bits = width * depth;
                _belowbit = bits - width - 1;
                _stages = new SLVSignal(bits)
                {
                    InitialValue = StdLogicVector._0s(bits)
                };
            }
            else if (depth < 0)
            {
                throw new ArgumentException("Depth must be >= 0");
            }
        }

        [PerformanceRelevant]
        public bool UseEn { get; private set; }

        [PerformanceRelevant]
        public int Width
        {
            get { return _width; }
        }

        [PerformanceRelevant]
        public int Depth
        {
            get { return _depth; }
        }

        public override bool IsEquivalent(Component obj)
        {
            var other = obj as RegPipe;
            if (other == null)
                return false;
            return UseEn == other.UseEn &&
                Width == other.Width &&
                Depth == other.Depth;
        }

        public override int GetBehaviorHashCode()
        {
            return UseEn.GetHashCode() ^
                Width ^
                Depth;
        }

        protected override void Initialize()
        {
            if (_depth == 0)
            {
                AddProcess(DirectFeed, Din.ChangedEvent);
            }
            else if (_depth == 1)
            {
                if (UseEn)
                    AddProcess(OnClock1WithEn, Clk.ChangedEvent);
                else
                    AddProcess(OnClock1, Clk.ChangedEvent);
            }
            else
            {
                if (UseEn)
                    AddProcess(OnClockWithEn, Clk.ChangedEvent);
                else
                    AddProcess(OnClock, Clk.ChangedEvent);
                AddProcess(FeedOut, _stages);
            }
        }

        private void OnClock()
        {
            if (Clk.RisingEdge())
            {
                _stages.Next = _stages.Cur[_belowbit, 0].Concat(Din.Cur);
                /*_stages[0].Next = Din.Cur;
                for (int i = 1; i < _depth - 1; i++)
                    _stages[i].Next = _stages[i - 1].Cur;
                Dout.Next = _stages[_depth - 2].Cur;*/
            }
        }

        private void OnClockWithEn()
        {
            if (Clk.RisingEdge() && En.Cur == '1')
            {
                _stages.Next = _stages.Cur[_belowbit, 0].Concat(Din.Cur);
                /*_stages[0].Next = Din.Cur;
                for (int i = 1; i < _depth - 1; i++)
                    _stages[i].Next = _stages[i - 1].Cur;
                Dout.Next = _stages[_depth - 2].Cur;*/
            }
        }

        private void FeedOut()
        {
            Dout.Next = _stages.Cur[_belowbit + _width, _belowbit + 1];
        }

        private void DirectFeed()
        {
            Dout.Next = Din.Cur;
        }

        private void OnClock1()
        {
            if (Clk.RisingEdge())
            {
                Dout.Next = Din.Cur;
            }
        }

        private void OnClock1WithEn()
        {
            if (Clk.RisingEdge() && En.Cur == '1')
            {
                Dout.Next = Din.Cur;
            }
        }
    }
}

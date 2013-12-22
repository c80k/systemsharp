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
using SystemSharp.Common;

namespace SystemSharp.Synthesis
{
    /// <summary>
    /// Indicates that an attempt to synthesize a memory layout failed.
    /// </summary>
    public class MemoryLayoutFailedException : Exception
    {
    }

    /// <summary>
    /// Default implementation of a memory layout algorithm.
    /// </summary>
    public class DefaultMemoryLayoutAlgorithm: IMemoryLayoutAlgorithm
    {
        private class MemRgn : IComparable<MemRgn>
        {
            public ulong Offset { get; private set; }
            public ulong Size { get; private set; }

            public MemRgn(ulong offset, ulong size)
            {
                Offset = offset;
                Size = size;
            }

            public int CompareTo(MemRgn other)
            {
                long delta = (long)(Size - other.Size);
                if (delta < 0)
                    return -1;
                else if (delta > 0)
                    return 1;
                else
                    return 0;
            }
        }

        public void Layout(MemoryRegion region)
        {
            var itemsBySize = region.Items.OrderByDescending(item => item.Size);
            SortedSet<MemRgn> freeSet = new SortedSet<MemRgn>();
            MemRgn top = new MemRgn(0, long.MaxValue);
            freeSet.Add(top);
            uint alignment = region.MarshalInfo.Alignment;
            bool alignPow2 = region.MarshalInfo.UseStrongPow2Alignment;
            ulong requiredSize = 0;
            foreach (MemoryMappedStorage item in itemsBySize)
            {
                MemRgn req = new MemRgn(0, item.Size);
                var avail = freeSet.GetViewBetween(req, top);
                bool found = false;
                foreach (MemRgn rgn in avail)
                {
                    ulong offset = rgn.Offset;
                    offset = MathExt.Align(offset, alignment);
                    if (alignPow2)
                    {
                        ulong alignedSize = MathExt.CeilPow2(item.Size);
                        offset = (offset + alignedSize - 1) & ~(alignedSize - 1);
                    }
                    if (offset + item.Size <= rgn.Offset + rgn.Size)
                    {
                        item.Offset = offset;
                        freeSet.Remove(rgn);
                        if (rgn.Offset < offset)
                        {
                            MemRgn left = new MemRgn(rgn.Offset, offset - rgn.Offset);
                            freeSet.Add(left);
                        }
                        if (offset + item.Size < rgn.Offset + rgn.Size)
                        {
                            MemRgn right = new MemRgn(offset + item.Size, rgn.Offset + rgn.Size - offset - item.Size);
                            freeSet.Add(right);
                        }
                        requiredSize = Math.Max(requiredSize, offset + item.Size);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new MemoryLayoutFailedException();
                region.RequiredSize = requiredSize;
            }
            region.Seal();
        }
    }
}

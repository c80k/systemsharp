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
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SystemSharp.Assembler.DesignGen
{
    public static class MicrocodeAlgorithms
    {
        public static int[] EncodeTogether(
            int[] seq0, int numSymbols0, int[] seq1, int numSymbols1, 
            out int[] encMap0, out int[] encMap1)
        {
            int seqLength = seq0.Length;
            if (numSymbols0 == 0)
                numSymbols0 = 1;
            if (numSymbols1 == 0)
                numSymbols1 = 1;
            int[,] fwdMap = new int[numSymbols0, numSymbols1];
            int numSyms = 0;
            int[] result = new int[seqLength];
            encMap0 = new int[numSymbols0 * numSymbols1];
            encMap1 = new int[numSymbols0 * numSymbols1];
            for (int i = 0; i < seqLength; i++)
            {
                int sym0 = seq0[i];
                int sym1 = seq1[i];
                if (sym0 == 0 || sym1 == 0)
                    continue;
                int csym = fwdMap[sym0 - 1, sym1 - 1];
                if (csym == 0)
                {
                    csym = ++numSyms;
                    fwdMap[sym0 - 1, sym1 - 1] = csym;
                    encMap0[csym - 1] = sym0;
                    encMap1[csym - 1] = sym1;
                }
                result[i] = csym;
            }
            for (int i = 0; i < seqLength; i++)
            {
                int sym0 = seq0[i];
                int sym1 = seq1[i];
                if ((sym0 == 0) == (sym1 == 0))
                    continue;

                if (sym0 == 0)
                {
                    int csym = 0;
                    for (int tsym0 = 1; tsym0 <= numSymbols0; tsym0++)
                    {
                        csym = fwdMap[tsym0 - 1, sym1 - 1];
                        if (csym != 0)
                        {
                            result[i] = csym;
                            break;
                        }
                    }
                    if (csym == 0)
                    {
                        int tsym0 = 1;
                        csym = ++numSyms;
                        fwdMap[tsym0 - 1, sym1 - 1] = csym;
                        result[i] = csym;
                        encMap0[csym - 1] = tsym0;
                        encMap1[csym - 1] = sym1;
                    }
                }

                if (sym1 == 0)
                {
                    int csym = 0;
                    for (int tsym1 = 1; tsym1 <= numSymbols1; tsym1++)
                    {
                        csym = fwdMap[sym0 - 1, tsym1 - 1];
                        if (csym != 0)
                        {
                            result[i] = csym;
                            break;
                        }
                    }
                    if (csym == 0)
                    {
                        int tsym1 = 1;
                        csym = ++numSyms;
                        fwdMap[sym0 - 1, tsym1 - 1] = csym;
                        result[i] = csym;
                        encMap0[csym - 1] = sym0;
                        encMap1[csym - 1] = tsym1;
                    }
                }
            }
            Array.Resize(ref encMap0, numSyms);
            Array.Resize(ref encMap1, numSyms);

            // Verify result
            for (int i = 0; i < result.Length; i++)
            {
                int sym = result[i];
                if (sym == 0)
                    continue;
                int sym0 = encMap0[sym - 1];
                int sym1 = encMap1[sym - 1];
                Debug.Assert(seq0[i] == 0 || seq0[i] == sym0);
                Debug.Assert(seq1[i] == 0 || seq1[i] == sym1);
            }

            return result;
        }
    }
}

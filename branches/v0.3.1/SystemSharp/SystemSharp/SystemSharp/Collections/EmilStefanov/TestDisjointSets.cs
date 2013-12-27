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

namespace SystemSharp.Collections.EmilStefanov.Test
{
    public class DisjointSetsTester
    {
        public class TestFailedException : Exception
        {
            public TestFailedException(string reason):
                base(reason)
            {
            }
        }

        int _numElements;
        int _numSets;

        public DisjointSetsTester(int numElements, int numSets)
        {
            _numElements = numElements;
            _numSets = numSets;
        }

        public void RunTest()
        {
            DisjointSets ds = new DisjointSets(_numElements);
            int[] e2set = new int[_numElements];
            HashSet<int>[] sets = new HashSet<int>[_numSets];
            for (int i = 0; i < _numSets; i++)
            {
                sets[i] = new HashSet<int>();
            }
            Random rnd = new Random();
            for (int i = 0; i < _numElements; i++)
            {
                int nset = rnd.Next(_numSets);
                e2set[i] = nset;
                sets[nset].Add(i);
            }
            foreach (HashSet<int> set in sets)
            {
                Queue<int> q = new Queue<int>(set);
                if (q.Count == 0)
                    continue;
                int last = q.Dequeue();
                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    ds.Union(ds.FindSet(last), ds.FindSet(cur));
                    last = cur;
                }
            }

            int[] reps = new int[_numSets];
            for (int i = 0; i < _numSets; i++)
                reps[i] = -1;

            for (int i = 0; i < _numElements; i++)
            {
                int rep = ds.FindSet(i);
                int nset = e2set[i];
                if (reps[nset] == -1)
                    reps[nset] = rep;
                else if (reps[nset] != rep)
                    throw new TestFailedException("Test with " + _numElements + " elements and " + _numSets + " sets: wrong representant");
            }
        }

        public static void RunTests()
        {
            int numSets = 2;
            for (int numElements = 3; numElements <= 59049; numElements *= 3, numSets *= 2)
            {
                DisjointSetsTester tester = new DisjointSetsTester(numElements, numSets);
                tester.RunTest();
            }
        }
    }
}

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
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Assembler;
using SystemSharp.Assembler.Rewriters;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.DocGen;
using SystemSharp.Synthesis.VHDLGen;

namespace CompilerTest
{
    class GCD: Component
    {
        public In<StdLogic> Clk { private get; set; }
        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }
        public Out<StdLogicVector> R { private get; set; }

        [HLS]
        private async void ComputeGCD()
        {
            while (true)
            {
                int a = A.Cur.IntValue;
                int b = B.Cur.IntValue;
                int r;
                if (a == 0)
                {
                    r = 0;
                }
                else
                {
                    while (b != 0)
                    {
                        if (a > b)
                            a -= b;
                        else
                            b -= a;
                    }
                    r = a;
                }
                await Tick;
                R.Next = StdLogicVector.FromInt(r, 32);
            }
        }

        protected override void  Initialize()
        {
 	         AddClockedThread(ComputeGCD, Clk.RisingEdge, Clk);
        }
    }

    class Adder : Component
    {
        public In<StdLogic> Clk { private get; set; }
        public In<StdLogicVector> A { private get; set; }
        public In<StdLogicVector> B { private get; set; }
        public In<StdLogicVector> C { private get; set; }
        public In<StdLogicVector> D { private get; set; }
        public Out<StdLogicVector> R { private get; set; }

        [HLS]
        private async void ComputeSum()
        {
            while (true)
            {
                int a = A.Cur.IntValue;
                int b = B.Cur.IntValue;
                int c = C.Cur.IntValue;
                int d = D.Cur.IntValue;
                int t1 = a + b;
                int t2 = c + d;
                int t3 = t1 + t2;
                await Tick;
                R.Next = StdLogicVector.FromInt(t3, 32);
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(ComputeSum, Clk.RisingEdge, Clk);
        }
    }

    public class Testbench : Component
    {
        public static readonly Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

        private Clock _clkgen;
        private GCD _gcd;
        private Adder _adder;

        private SLSignal _clk = new SLSignal();
        private SLVSignal _a = new SLVSignal(32);
        private SLVSignal _b = new SLVSignal(32);
        private SLVSignal _c = new SLVSignal(32);
        private SLVSignal _d = new SLVSignal(32);
        private SLVSignal _r1 = new SLVSignal(32);
        private SLVSignal _r2 = new SLVSignal(32);

        public Testbench()
        {
            _clkgen = new Clock(ClockPeriod)
            {
                Clk = _clk
            };
            _gcd = new GCD()
            {
                Clk = _clk,
                A = _a,
                B = _b,
                R = _r1
            };
            _adder = new Adder()
            {
                Clk = _clk,
                A = _a,
                B = _b,
                C = _c,
                D = _d,
                R = _r2
            };
        }

        private async void Tester()
        {
            for (int a = 0; a < 10; a++)
            {
                for (int b = 0; b < 10; b++)
                {
                    _a.Next = StdLogicVector.FromInt(a, 32);
                    _b.Next = StdLogicVector.FromInt(b, 32);
                    int c = 3 * a;
                    int d = 2 * a - b;
                    _c.Next = StdLogicVector.FromInt(c, 32);
                    _d.Next = StdLogicVector.FromInt(d, 32);
                    await Tick;
                    int r1 = _r1.Cur.IntValue;
                    Console.WriteLine("The GCD of {0} and {1} is {2}.", a, b, r1);
                }
            }
        }

        protected override void Initialize()
        {
            AddClockedThread(Tester, _clk.RisingEdge, _clk);
        }

        public static void RunTest()
        {
            DesignContext.Reset();
            Testbench tb = new Testbench();
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(101 * Testbench.ClockPeriod);
            DesignContext.Instance.CompleteAnalysis();

            VHDLGenerator codeGen = new VHDLGenerator();
            DocumentationProject proj = new DocumentationProject("./doc");
            SynthesisEngine.Create(DesignContext.Instance, proj).Synthesize(codeGen);
            proj.Save();
        }
    }
}

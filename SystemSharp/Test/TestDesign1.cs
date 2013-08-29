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
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Interop.Xilinx;

namespace Test
{
    class TestDesign1: Component
    {
        public static int MaxWidth = 32;
        public static Time ClockPeriod = new Time(10.0, ETimeUnit.ns);

        [SideEffectFree]
        private void MakeVectors(int value1, int width1, int value2, int width2,
            out StdLogicVector r1, out StdLogicVector r2)
        {
            StdLogicVector v1 = StdLogicVector.FromLong(value1, width1);
            StdLogicVector v2 = StdLogicVector.FromLong(value2, width2);
            StdLogicVector v = v1.Concat(v2).Concat(StdLogicVector._0s(2 * MaxWidth - width1 - width2));
            r1 = v[2 * MaxWidth - 1, MaxWidth];
            r2 = v[MaxWidth - 1, 0];
        }
        
        private StdLogicVector CreateVector()
        {
            StdLogicVector[] r = new StdLogicVector[4];
            r[0] = StdLogicVector._0s(MaxWidth);
            r[1] = StdLogicVector._1s(MaxWidth);
            r[2] = StdLogicVector.DCs(MaxWidth);
            r[3] = StdLogicVector.Xs(MaxWidth);
            return r[0].Concat(r[1].Concat(r[2].Concat(r[3])));
        }

        private int _width1;
        private int _width2;

        private SLSignal _clk = new SLSignal();
        
        private Signal<int> _ctr = new Signal<int>()
        {
            InitialValue = 0
        };

        private SLVSignal _ctr1;
        private SLVSignal _ctr2;
        private SLVSignal _dummy;

        private Clock _clkgen;

        private ConsoleLogger<StdLogicVector> _ctr1Logger;
        private ConsoleLogger<StdLogicVector> _ctr2Logger;
        private ConsoleLogger<StdLogicVector> _dummyLogger;

        public TestDesign1(int width1, int width2)
        {
            _width1 = width1;
            _width2 = width2;
            _ctr1 = new SLVSignal(MaxWidth);
            _ctr2 = new SLVSignal(MaxWidth);
            _dummy = new SLVSignal(4 * MaxWidth);

            _clkgen = new Clock(ClockPeriod)
            {
                Clk = _clk
            };
            _ctr1Logger = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _ctr1
            };
            _ctr2Logger = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _ctr2
            };
            _dummyLogger = new ConsoleLogger<StdLogicVector>()
            {
                DataIn = _dummy
            };
        }

        private void CounterProcess()
        {
            if (_clk.RisingEdge())
                _ctr.Next = _ctr.Cur + 1;
        }

        private void TransferProcess()
        {
            if (_clk.RisingEdge())
            {
                StdLogicVector tmp1, tmp2;
                MakeVectors(_ctr.Cur, _width1, _ctr.Cur, _width2, out tmp1, out tmp2);
                _ctr1.Next = tmp1;
                _ctr2.Next = tmp2;
                _dummy.Next = CreateVector();
            }
        }

        protected override void Initialize()
        {
            AddProcess(CounterProcess, _clk);
            AddProcess(TransferProcess, _clk);
        }

        public static void RunTest()
        {
            DesignContext.Reset();

            TestDesign1 td1 = new TestDesign1(3, 4);
            FixedPointSettings.GlobalOverflowMode = EOverflowMode.Wrap;
            DesignContext.Instance.Elaborate();
            DesignContext.Instance.Simulate(20 * TestDesign1.ClockPeriod);

            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(@".\hdl_TestDesign1", "TestDesign1");
            project.PutProperty(EXilinxProjectProperties.DeviceFamily, EDeviceFamily.Spartan3);
            project.PutProperty(EXilinxProjectProperties.Device, EDevice.xc3s1500l);
            project.PutProperty(EXilinxProjectProperties.Package, EPackage.fg676);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, ESpeedGrade._4);
            project.PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(codeGen); ;
            project.Save();

            DesignContext.Reset();
        }
    }


}

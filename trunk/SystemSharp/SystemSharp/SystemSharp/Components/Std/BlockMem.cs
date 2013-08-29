/**
 * Copyright 2012-2013 Christian Köllner
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
using System.Linq;
using System.Text;
using SystemSharp.Common;
using SystemSharp.DataTypes;

namespace SystemSharp.Components.Std
{
    [ContractClass(typeof(ROMContractClass))]
    public interface IROM
    {
        In<StdLogic> Clk { get; set;  }
        In<StdLogicVector> Addr { get; set;  }
        In<StdLogic> RdEn { get; set; }
        Out<StdLogicVector> DataOut { get; set; }
        uint Depth { get; }
        uint Width { get; }

        void PreWrite(StdLogicVector addr, StdLogicVector data);
    }

    [ContractClassFor(typeof(IROM))]
    abstract class ROMContractClass : IROM
    {
        public In<StdLogic> Clk
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public In<StdLogicVector> Addr
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null);
                throw new NotImplementedException();
            }
        }

        public In<StdLogic> RdEn
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public Out<StdLogicVector> DataOut
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null);
                throw new NotImplementedException();
            }
        }

        public uint Depth
        {
            get { throw new NotImplementedException(); }
        }

        public uint Width
        {
            get { throw new NotImplementedException(); }
        }

        public void PreWrite(StdLogicVector addr, StdLogicVector data)
        {
            Contract.Requires<ArgumentException>(addr.ULongValue < Depth);
            Contract.Requires<ArgumentException>(data.Size == Width);
        }
    }

    [ContractClass(typeof(RAMContractClass))]
    public interface IRAM: IROM
    {
        In<StdLogicVector> WrEn { get; set;  }
        In<StdLogicVector> DataIn { get; set;  }
    }

    [ContractClassFor(typeof(IRAM))]
    abstract class RAMContractClass : IRAM
    {
        public In<StdLogicVector> WrEn
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null);
                throw new NotImplementedException();
            }
        }

        public In<StdLogicVector> DataIn
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null);
                throw new NotImplementedException();
            }
        }

        public In<StdLogic> Clk
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public In<StdLogicVector> Addr
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public In<StdLogic> RdEn
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public Out<StdLogicVector> DataOut
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public uint Depth
        {
            get { throw new NotImplementedException(); }
        }

        public uint Width
        {
            get { throw new NotImplementedException(); }
        }

        public void PreWrite(StdLogicVector addr, StdLogicVector data)
        {
            throw new NotImplementedException();
        }
    }

    public interface IBlockMemFactory
    {
        void CreateROM(int addrWidth, int dataWidth, int capacity, int readLatency, out Component part, out IROM rom);
        void CreateRAM(int addrWidth, int dataWidth, int capacity, int readLatency, int writeLatency,
            out Component part, out IRAM ram);
    }

    public class ROM: Component, IROM
    {
        private class FactoryImpl : IBlockMemFactory
        {
            public void CreateROM(int addrWidth, int dataWidth, int capacity, int readLatency, out Component part, out IROM rom)
            {
                if (readLatency != 1)
                    throw new NotSupportedException("Read latency must be 1");

                var romi = new ROM(1u << addrWidth, (uint)dataWidth);
                part = romi;
                rom = romi;
            }

            public void CreateRAM(int addrWidth, int dataWidth, int capacity, int readLatency, int writeLatency, out Component part, out IRAM ram)
            {
                throw new NotSupportedException("Only ROM can be created");
            }
        }

        public static readonly IBlockMemFactory Factory = new FactoryImpl();

        public In<StdLogic> Clk { get; set; }
        public In<StdLogicVector> Addr { get; set; }
        public In<StdLogic> RdEn { get; set; }
        public Out<StdLogicVector> DataOut { get; set; }

        private uint _depth;
        private uint _width;
        private StdLogicVector[] _content;

        public ROM(uint depth, uint width)
        {
            _depth = depth;
            _width = width;
            _content = Enumerable.Repeat(StdLogicVector.Us(width), (int)depth).ToArray();
        }

        public uint Depth { get { return _depth; } }
        public uint Width { get { return _width; } }

        private void ReadProcess()
        {
            if (Clk.RisingEdge() && RdEn.Cur == '1')
            {
                DataOut.Next = _content[Addr.Cur.UnsignedValue.IntValue];
            }
        }

        protected override void Initialize()
        {
            AddProcess(ReadProcess, Clk);
        }

        public void PreWrite(StdLogicVector addr, StdLogicVector data)
        {
            _content[addr.ULongValue] = data;
        }
    }

    public class SimpleDPRAM: Component
    {
        public In<StdLogic> Clk { get; set; }
        public In<StdLogicVector> Addr1 { get; set; }
        public In<StdLogicVector> Addr2 { get; set; }
        public In<StdLogic> WrEn { get; set; }
        public In<StdLogicVector> DataIn { get; set; }
        public Out<StdLogicVector> DataOut { get; set; }

        private uint _depth;
        private uint _width;
        private StdLogicVector[] _content;

        public SimpleDPRAM(uint depth, uint width)
        {
            _depth = MathExt.CeilPow2(depth);
            _width = width;
            _content = Enumerable.Repeat(StdLogicVector.Us(width), (int)_depth).ToArray();
        }

        public uint Depth { get { return _depth; } }
        public uint Width { get { return _width; } }
        public int AddrWidth { get { return MathExt.CeilLog2(_depth); } }

        private void Process()
        {
            if (Clk.RisingEdge())
            {
                if (WrEn.Cur == '1')
                {
                    _content[Addr2.Cur.UnsignedValue.IntValue] = DataIn.Cur;
                }
                DataOut.Next = _content[Addr1.Cur.UnsignedValue.IntValue];
            }
        }

        protected override void Initialize()
        {
            AddProcess(Process, Clk);
        }

        public void PreWrite(StdLogicVector addr, StdLogicVector data)
        {
            _content[addr.ULongValue] = data;
        }
    }
}

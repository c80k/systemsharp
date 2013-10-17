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
    /// <summary>
    /// Generic register transfer level interface for read-only memory
    /// </summary>
    [ContractClass(typeof(ROMContractClass))]
    public interface IROM
    {
        /// <summary>
        /// Clock signal input
        /// </summary>
        In<StdLogic> Clk { get; set;  }

        /// <summary>
        /// Address input
        /// </summary>
        In<StdLogicVector> Addr { get; set;  }

        /// <summary>
        /// Read enable
        /// </summary>
        In<StdLogic> RdEn { get; set; }

        /// <summary>
        /// Data output
        /// </summary>
        Out<StdLogicVector> DataOut { get; set; }

        /// <summary>
        /// ROM size (i.e. number of words)
        /// </summary>
        uint Depth { get; }

        /// <summary>
        /// Width of data word
        /// </summary>
        uint Width { get; }

        /// <summary>
        /// Pre-initializes the ROM with word <paramref name="data"/> at address <paramref name="addr"/>.
        /// Called during elaboration, never at model runtime.
        /// </summary>
        void PreWrite(StdLogicVector addr, StdLogicVector data);
    }

    [ContractClassFor(typeof(IROM))]
    abstract class ROMContractClass : IROM
    {
        public In<StdLogic> Clk
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public In<StdLogicVector> Addr
        {
            get { throw new NotImplementedException(); }
            set {
                Contract.Requires<ArgumentNullException>(value != null, "Addr");
                throw new NotImplementedException();
            }
        }

        public In<StdLogic> RdEn
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public Out<StdLogicVector> DataOut
        {
            get { throw new NotImplementedException(); }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "DataOut");
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
            Contract.Requires<ArgumentOutOfRangeException>(addr.ULongValue < Depth, "addr beyond ROM capacity");
            Contract.Requires<ArgumentException>(data.Size == Width, "wrong data word size");
        }
    }

    /// <summary>
    /// Generic register transfer level interface for random access memory
    /// </summary>
    [ContractClass(typeof(RAMContractClass))]
    public interface IRAM: IROM
    {
        /// <summary>
        /// Write enable
        /// </summary>
        In<StdLogicVector> WrEn { get; set;  }

        /// <summary>
        /// Data input
        /// </summary>
        In<StdLogicVector> DataIn { get; set;  }
    }

    [ContractClassFor(typeof(IRAM))]
    abstract class RAMContractClass : IRAM
    {
        public In<StdLogicVector> WrEn
        {
            get { throw new NotImplementedException(); }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "WrEn");
                throw new NotImplementedException();
            }
        }

        public In<StdLogicVector> DataIn
        {
            get { throw new NotImplementedException(); }
            set
            {
                Contract.Requires<ArgumentNullException>(value != null, "DataIn");
                throw new NotImplementedException();
            }
        }

        public In<StdLogic> Clk
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public In<StdLogicVector> Addr
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public In<StdLogic> RdEn
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public Out<StdLogicVector> DataOut
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
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

    /// <summary>
    /// Factory pattern for creating implementations of ROM/RAM components
    /// </summary>
    public interface IBlockMemFactory
    {
        /// <summary>
        /// Creates a ROM component
        /// </summary>
        /// <param name="addrWidth">desired address width</param>
        /// <param name="dataWidth">desired data width</param>
        /// <param name="capacity">desired ROM capacity (number of words)</param>
        /// <param name="readLatency">desired read latency</param>
        /// <param name="part">receives created component</param>
        /// <param name="rom">receives ROM interface</param>
        void CreateROM(int addrWidth, int dataWidth, int capacity, int readLatency, out Component part, out IROM rom);

        /// <summary>
        /// Creates a RAM component
        /// </summary>
        /// <param name="addrWidth">desired address width</param>
        /// <param name="dataWidth">desired data width</param>
        /// <param name="capacity">desired RAM capacity (number of words)</param>
        /// <param name="readLatency">desired read latency</param>
        /// <param name="writeLatency">desired write latency</param>
        /// <param name="part">receives created component</param>
        /// <param name="ram">receives RAM interface</param>
        void CreateRAM(int addrWidth, int dataWidth, int capacity, int readLatency, int writeLatency,
            out Component part, out IRAM ram);
    }

    /// <summary>
    /// A simple ROM implementation which supports simulation and synthesizable HDL generation.
    /// </summary>
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

        /// <summary>
        /// Factory for creating instances of this component, only ROM supported.
        /// Further restriction: Read latency must be 1.
        /// </summary>
        public static readonly IBlockMemFactory Factory = new FactoryImpl();

        public In<StdLogic> Clk { get; set; }
        public In<StdLogicVector> Addr { get; set; }
        public In<StdLogic> RdEn { get; set; }
        public Out<StdLogicVector> DataOut { get; set; }

        private uint _depth;
        private uint _width;
        private StdLogicVector[] _content;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="depth">desired ROM capacity (number of data words)</param>
        /// <param name="width">desired data word width</param>
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

    /// <summary>
    /// A simple dual-ported RAM implementation which supports simulation and synthesizable HDL generation.
    /// It provides separate read and write ports, but does not support conccurent reads or concurrent writes.
    /// </summary>
    public class SimpleDPRAM: Component
    {
        /// <summary>
        /// Clock signal input
        /// </summary>
        public In<StdLogic> Clk { get; set; }

        /// <summary>
        /// Address for reading
        /// </summary>
        public In<StdLogicVector> RdAddr { get; set; }

        /// <summary>
        /// Address for writing
        /// </summary>
        public In<StdLogicVector> WrAddr { get; set; }

        /// <summary>
        /// Write enable
        /// </summary>
        public In<StdLogic> WrEn { get; set; }

        /// <summary>
        /// Data to write
        /// </summary>
        public In<StdLogicVector> DataIn { get; set; }

        /// <summary>
        /// Read data
        /// </summary>
        public Out<StdLogicVector> DataOut { get; set; }

        private uint _depth;
        private uint _width;
        private StdLogicVector[] _content;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="depth">desired capacity (number of data words)</param>
        /// <param name="width">desired data word width</param>
        public SimpleDPRAM(uint depth, uint width)
        {
            _depth = MathExt.CeilPow2(depth);
            _width = width;
            _content = Enumerable.Repeat(StdLogicVector.Us(width), (int)_depth).ToArray();
        }

        /// <summary>
        /// Capacity (number of data words)
        /// </summary>
        public uint Depth { get { return _depth; } }

        /// <summary>
        /// Data word width
        /// </summary>
        public uint Width { get { return _width; } }

        /// <summary>
        /// Address width (automatically computed from capacity)
        /// </summary>
        public int AddrWidth { get { return MathExt.CeilLog2(_depth); } }

        private void Process()
        {
            if (Clk.RisingEdge())
            {
                if (WrEn.Cur == '1')
                {
                    _content[WrAddr.Cur.UnsignedValue.IntValue] = DataIn.Cur;
                }
                DataOut.Next = _content[RdAddr.Cur.UnsignedValue.IntValue];
            }
        }

        protected override void Initialize()
        {
            AddProcess(Process, Clk);
        }

        /// <summary>
        /// Pre-initializes the RAM with word <paramref name="data"/> at address <paramref name="addr"/>.
        /// Called during elaboration, never at model runtime.
        /// </summary>
        public void PreWrite(StdLogicVector addr, StdLogicVector data)
        {
            Contract.Requires<ArgumentOutOfRangeException>(addr.Size == AddrWidth, "invalid address width");
            Contract.Requires<ArgumentOutOfRangeException>(addr.ULongValue < Depth, "address beyond RAM capacity");
            Contract.Requires<ArgumentOutOfRangeException>(data.Size == Width, "invalid data width");

            _content[addr.ULongValue] = data;
        }
    }
}

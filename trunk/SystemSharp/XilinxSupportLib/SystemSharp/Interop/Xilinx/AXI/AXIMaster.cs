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
 * 
 * */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using XilinxSupportLib.SystemSharp.Interop.Xilinx.AXI;

namespace SystemSharp.Interop.Xilinx.AXI
{
    [ComponentPurpose(EComponentPurpose.SimulationOnly)]
    public class AXIMasterUserLogicTestbench : Component
    {
        protected SLSignal _sig_Bus2IP_Clk;
        protected SLSignal _sig_Bus2IP_Resetn;
        protected SLVSignal _sig_Bus2IP_Data;
        protected SLVSignal _sig_Bus2IP_BE;
        protected SLVSignal _sig_Bus2IP_RdCE;
        protected SLVSignal _sig_Bus2IP_WrCE;
        protected SLVSignal _sig_IP2Bus_Data;
        protected SLSignal _sig_IP2Bus_RdAck;
        protected SLSignal _sig_IP2Bus_WrAck;
        protected SLSignal _sig_IP2Bus_Error;
        protected SLSignal _sig_ip2bus_mstrd_req;
        protected SLSignal _sig_ip2bus_mstwr_req;
        protected SLVSignal _sig_ip2bus_mst_addr;
        protected SLVSignal _sig_ip2bus_mst_be;
        protected SLVSignal _sig_ip2bus_mst_length;
        protected SLSignal _sig_ip2bus_mst_type;
        protected SLSignal _sig_ip2bus_mst_lock;
        protected SLSignal _sig_ip2bus_mst_reset;
        protected SLSignal _sig_bus2ip_mst_cmdack;
        protected SLSignal _sig_bus2ip_mst_cmplt;
        protected SLSignal _sig_bus2ip_mst_error;
        protected SLSignal _sig_bus2ip_mst_rearbitrate;
        protected SLSignal _sig_bus2ip_mst_cmd_timeout;
        protected SLVSignal _sig_bus2ip_mstrd_d;
        protected SLVSignal _sig_bus2ip_mstrd_rem;
        protected SLSignal _sig_bus2ip_mstrd_sof_n;
        protected SLSignal _sig_bus2ip_mstrd_eof_n;
        protected SLSignal _sig_bus2ip_mstrd_src_rdy_n;
        protected SLSignal _sig_bus2ip_mstrd_src_dsc_n;
        protected SLSignal _sig_ip2bus_mstrd_dst_rdy_n;
        protected SLSignal _sig_ip2bus_mstrd_dst_dsc_n;
        protected SLVSignal _sig_ip2bus_mstwr_d;
        protected SLVSignal _sig_ip2bus_mstwr_rem;
        protected SLSignal _sig_ip2bus_mstwr_src_rdy_n;
        protected SLSignal _sig_ip2bus_mstwr_src_dsc_n;
        protected SLSignal _sig_ip2bus_mstwr_sof_n;
        protected SLSignal _sig_ip2bus_mstwr_eof_n;
        protected SLSignal _sig_bus2ip_mstwr_dst_rdy_n;
        protected SLSignal _sig_bus2ip_mstwr_dst_dsc_n;

        private AXIMasterUserLogic user_logic;
        public AXIMasterUserLogic UserLogic
        {
            get { return user_logic; }
        }

        private Clock _clockGen;

        public AXIMasterUserLogicTestbench(AXIMasterUserLogic userLogic)
        {
            user_logic = userLogic;

            _sig_Bus2IP_Clk = new SLSignal();
            _sig_Bus2IP_Resetn = new SLSignal() { InitialValue = '0' };
            _sig_Bus2IP_Data = new SLVSignal(userLogic.SLVDWidth) { InitialValue = StdLogicVector.Xs(userLogic.SLVDWidth) };
            _sig_Bus2IP_BE = new SLVSignal(userLogic.SLVDWidth / 8) { InitialValue = StdLogicVector._0s(userLogic.SLVDWidth / 8) };
            _sig_Bus2IP_RdCE = new SLVSignal(userLogic.NumRegs) { InitialValue = StdLogicVector._0s(userLogic.NumRegs) };
            _sig_Bus2IP_WrCE = new SLVSignal(userLogic.NumRegs) { InitialValue = StdLogicVector._0s(userLogic.NumRegs) };
            _sig_IP2Bus_Data = new SLVSignal(userLogic.SLVDWidth) { InitialValue = StdLogicVector._0s(userLogic.SLVDWidth) };
            _sig_IP2Bus_RdAck = new SLSignal() { InitialValue = '0' };
            _sig_IP2Bus_WrAck = new SLSignal() { InitialValue = '0' };
            _sig_IP2Bus_Error = new SLSignal() { InitialValue = '0' };
            _sig_ip2bus_mstrd_req = new SLSignal() { InitialValue = '0' };
            _sig_ip2bus_mstwr_req = new SLSignal() { InitialValue = '0' };
            _sig_ip2bus_mst_addr = new SLVSignal(userLogic.MasterAXIAddrWidth) { InitialValue = StdLogicVector.Us(userLogic.MasterAXIAddrWidth) };
            _sig_ip2bus_mst_be = new SLVSignal(userLogic.NativeDataWidth/8) { InitialValue = StdLogicVector._1s(userLogic.NativeDataWidth/8) };
            _sig_ip2bus_mst_length = new SLVSignal(userLogic.LengthWidth) { InitialValue = StdLogicVector._0s(userLogic.LengthWidth) };
            _sig_ip2bus_mst_type = new SLSignal() { InitialValue = '0' };
            _sig_ip2bus_mst_lock = new SLSignal() { InitialValue = '0' };
            _sig_ip2bus_mst_reset = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mst_cmdack = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mst_cmplt = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mst_error = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mst_rearbitrate = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mst_cmd_timeout = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mstrd_d = new SLVSignal(userLogic.NativeDataWidth);
            _sig_bus2ip_mstrd_rem = new SLVSignal(userLogic.NativeDataWidth / 8) { InitialValue = StdLogicVector._0s(userLogic.NativeDataWidth / 8) };
            _sig_bus2ip_mstrd_sof_n = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mstrd_eof_n = new SLSignal() { InitialValue = '0' };
            _sig_bus2ip_mstrd_src_rdy_n = new SLSignal() { InitialValue = '1' };
            _sig_bus2ip_mstrd_src_dsc_n = new SLSignal() { InitialValue = '1' };
            _sig_ip2bus_mstrd_dst_rdy_n = new SLSignal() { InitialValue = '1' };
            _sig_ip2bus_mstrd_dst_dsc_n = new SLSignal() { InitialValue = '1' };
            _sig_ip2bus_mstwr_d = new SLVSignal(userLogic.NativeDataWidth);
            _sig_ip2bus_mstwr_rem = new SLVSignal(userLogic.NativeDataWidth / 8) { InitialValue = StdLogicVector._0s(userLogic.NativeDataWidth / 8) };
            _sig_ip2bus_mstwr_src_rdy_n = new SLSignal() { InitialValue = '1' };
            _sig_ip2bus_mstwr_src_dsc_n = new SLSignal() { InitialValue = '1' };
            _sig_ip2bus_mstwr_sof_n = new SLSignal() { InitialValue = '1' };
            _sig_ip2bus_mstwr_eof_n = new SLSignal() { InitialValue = '1' };
            _sig_bus2ip_mstwr_dst_rdy_n = new SLSignal() { InitialValue = '1' };
            _sig_bus2ip_mstwr_dst_dsc_n = new SLSignal() { InitialValue = '1' };

            userLogic.Bus2IP_Clk = _sig_Bus2IP_Clk;
            userLogic.Bus2IP_BE = _sig_Bus2IP_BE;
            userLogic.Bus2IP_Clk = _sig_Bus2IP_Clk;
            userLogic.Bus2IP_Data = _sig_Bus2IP_Data;
            userLogic.Bus2IP_RdCE = _sig_Bus2IP_RdCE;
            userLogic.Bus2IP_Resetn = _sig_Bus2IP_Resetn;
            userLogic.Bus2IP_WrCE = _sig_Bus2IP_WrCE;
            userLogic.IP2Bus_Data = _sig_IP2Bus_Data;
            userLogic.IP2Bus_Error = _sig_IP2Bus_Error;
            userLogic.IP2Bus_RdAck = _sig_IP2Bus_RdAck;
            userLogic.IP2Bus_WrAck = _sig_IP2Bus_WrAck;
            userLogic.ip2bus_mstrd_req = _sig_ip2bus_mstrd_req;
            userLogic.ip2bus_mstwr_req = _sig_ip2bus_mstwr_req;
            userLogic.ip2bus_mst_addr = _sig_ip2bus_mst_addr;
            userLogic.ip2bus_mst_be = _sig_ip2bus_mst_be;
            userLogic.ip2bus_mst_length = _sig_ip2bus_mst_length;
            userLogic.ip2bus_mst_type = _sig_ip2bus_mst_type;
            userLogic.ip2bus_mst_lock = _sig_ip2bus_mst_lock;
            userLogic.ip2bus_mst_reset = _sig_ip2bus_mst_reset;
            userLogic.bus2ip_mst_cmdack = _sig_bus2ip_mst_cmdack;
            userLogic.bus2ip_mst_cmplt = _sig_bus2ip_mst_cmplt;
            userLogic.bus2ip_mst_error = _sig_bus2ip_mst_error;
            userLogic.bus2ip_mst_rearbitrate = _sig_bus2ip_mst_rearbitrate;
            userLogic.bus2ip_mst_cmd_timeout = _sig_bus2ip_mst_cmd_timeout;
            userLogic.bus2ip_mstrd_d = _sig_bus2ip_mstrd_d;
            userLogic.bus2ip_mstrd_rem = _sig_bus2ip_mstrd_rem;
            userLogic.bus2ip_mstrd_sof_n = _sig_bus2ip_mstrd_sof_n;
            userLogic.bus2ip_mstrd_eof_n = _sig_bus2ip_mstrd_eof_n;
            userLogic.bus2ip_mstrd_src_rdy_n = _sig_bus2ip_mstrd_src_rdy_n;
            userLogic.bus2ip_mstrd_src_dsc_n = _sig_bus2ip_mstrd_src_dsc_n;
            userLogic.ip2bus_mstrd_dst_rdy_n = _sig_ip2bus_mstrd_dst_rdy_n;
            userLogic.ip2bus_mstrd_dst_dsc_n = _sig_ip2bus_mstrd_dst_dsc_n;
            userLogic.ip2bus_mstwr_d = _sig_ip2bus_mstwr_d;
            userLogic.ip2bus_mstwr_rem = _sig_ip2bus_mstwr_rem;
            userLogic.ip2bus_mstwr_src_rdy_n = _sig_ip2bus_mstwr_src_rdy_n;
            userLogic.ip2bus_mstwr_src_dsc_n = _sig_ip2bus_mstwr_src_dsc_n;
            userLogic.ip2bus_mstwr_sof_n = _sig_ip2bus_mstwr_sof_n;
            userLogic.ip2bus_mstwr_eof_n = _sig_ip2bus_mstwr_eof_n;
            userLogic.bus2ip_mstwr_dst_rdy_n = _sig_bus2ip_mstwr_dst_rdy_n;
            userLogic.bus2ip_mstwr_dst_dsc_n = _sig_bus2ip_mstwr_dst_dsc_n;

            _clockGen = new Clock(new Time(10.0, ETimeUnit.ns))
            {
                Clk = _sig_Bus2IP_Clk
            };
        }

        protected override void Initialize()
        {
            AddClockedThread(TestProcess, _sig_Bus2IP_Clk.RisingEdge, _sig_Bus2IP_Clk);
        }

        protected override void OnAnalysis()
        {
            base.OnAnalysis();
            DesignContext.Instance.Descriptor.AddChild(Descriptor, "testbench");
        }

        protected async void ResetSlave()
        {
            _sig_Bus2IP_Resetn.Next = '0';
            await Tick;
            _sig_Bus2IP_Resetn.Next = '1';
            await Tick;
        }

        protected async void WriteBus(int reg, StdLogicVector be, StdLogicVector data)
        {
            _sig_Bus2IP_WrCE.Next = StdLogicVector.OneHot(user_logic.NumRegs, user_logic.NumRegs - reg - 1);
            _sig_Bus2IP_BE.Next = be;
            _sig_Bus2IP_Data.Next = data;
            for (int t = 0; t < 100; t++)
            {
                await Tick;
                if (_sig_IP2Bus_WrAck.Cur == '1' ||
                     _sig_IP2Bus_Error.Cur == '1')
                    break;
            }
            if (_sig_IP2Bus_Error.Cur == '1')
                Console.WriteLine("Bus write error @reg " + reg);
            if (_sig_IP2Bus_Error.Cur == '0' &&
                _sig_IP2Bus_WrAck.Cur == '0')
                Console.WriteLine("Timeout @reg " + reg);
            _sig_Bus2IP_WrCE.Next = StdLogicVector._0s(user_logic.NumRegs);
            _sig_Bus2IP_BE.Next = StdLogicVector._0s(user_logic.SLVDWidth / 8);
            _sig_Bus2IP_Data.Next = StdLogicVector.Xs(user_logic.SLVDWidth);
        }

        protected async Task<StdLogicVector> ReadBus(int reg)
        {
            StdLogicVector value;

            _sig_Bus2IP_RdCE.Next = StdLogicVector.OneHot(user_logic.NumRegs, user_logic.NumRegs - reg - 1);
            for (int t = 0; t < 100; t++)
            {
                await Tick;
                if (_sig_IP2Bus_RdAck.Cur == '1' ||
                     _sig_IP2Bus_Error.Cur == '1')
                    break;
            }
            if (_sig_IP2Bus_Error.Cur == '1')
                Console.WriteLine("Bus read error @reg " + reg);
            if (_sig_IP2Bus_Error.Cur == '0' &&
                _sig_IP2Bus_RdAck.Cur == '0')
                Console.WriteLine("Timeout @reg " + reg);
            _sig_Bus2IP_RdCE.Next = StdLogicVector._0s(user_logic.NumRegs);
            value = _sig_IP2Bus_Data.Cur;

            return value;
        }

        protected virtual void TestProcess()
        {
            ResetSlave();
        }
    }

    public abstract class AXIMasterUserLogic : Component
    {
        public enum EAccessMode
        {
            Read,
            Write,
            ReadWrite
        }

        public class UserPort
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        public In<StdLogic> Bus2IP_Clk { get; set; }
        public In<StdLogic> Bus2IP_Resetn { get; set; }
        public In<StdLogicVector> Bus2IP_Data { get; set; }
        public In<StdLogicVector> Bus2IP_BE { get; set; }
        public In<StdLogicVector> Bus2IP_RdCE { get; set; }
        public In<StdLogicVector> Bus2IP_WrCE { get; set; }
        public Out<StdLogicVector> IP2Bus_Data { get; set; }
        public Out<StdLogic> IP2Bus_RdAck { get; set; }
        public Out<StdLogic> IP2Bus_WrAck { get; set; }
        public Out<StdLogic> IP2Bus_Error { get; set; }
        public Out<StdLogic> ip2bus_mstrd_req { get; set; }
        public Out<StdLogic> ip2bus_mstwr_req { get; set; }
        public Out<StdLogicVector> ip2bus_mst_addr { get; set; }
        public Out<StdLogicVector> ip2bus_mst_be { get; set; }
        public Out<StdLogicVector> ip2bus_mst_length { get; set; }
        public Out<StdLogic> ip2bus_mst_type { get; set; }
        public Out<StdLogic> ip2bus_mst_lock { get; set; }
        public Out<StdLogic> ip2bus_mst_reset { get; set; }
        public In<StdLogic> bus2ip_mst_cmdack { get; set; }
        public In<StdLogic> bus2ip_mst_cmplt { get; set; }
        public In<StdLogic> bus2ip_mst_error { get; set; }
        public In<StdLogic> bus2ip_mst_rearbitrate { get; set; }
        public In<StdLogic> bus2ip_mst_cmd_timeout { get; set; }
        public In<StdLogicVector> bus2ip_mstrd_d { get; set; }
        public In<StdLogicVector> bus2ip_mstrd_rem { get; set; }
        public In<StdLogic> bus2ip_mstrd_sof_n { get; set; }
        public In<StdLogic> bus2ip_mstrd_eof_n { get; set; }
        public In<StdLogic> bus2ip_mstrd_src_rdy_n { get; set; }
        public In<StdLogic> bus2ip_mstrd_src_dsc_n { get; set; }
        public Out<StdLogic> ip2bus_mstrd_dst_rdy_n { get; set; }
        public Out<StdLogic> ip2bus_mstrd_dst_dsc_n { get; set; }
        public Out<StdLogicVector> ip2bus_mstwr_d { get; set; }
        public Out<StdLogicVector> ip2bus_mstwr_rem { get; set; }
        public Out<StdLogic> ip2bus_mstwr_src_rdy_n { get; set; }
        public Out<StdLogic> ip2bus_mstwr_src_dsc_n { get; set; }
        public Out<StdLogic> ip2bus_mstwr_sof_n { get; set; }
        public Out<StdLogic> ip2bus_mstwr_eof_n { get; set; }
        public In<StdLogic> bus2ip_mstwr_dst_rdy_n { get; set; }
        public In<StdLogic> bus2ip_mstwr_dst_dsc_n { get; set; }

        public int NumRegs { [StaticEvaluation] get; private set; }
        public int SLVDWidth { [StaticEvaluation] get; private set; }
        public string ImpEntityName { get; set; }
        public string ImpFileName { get; set; }
        public string CreationDate { get; set; }
        public string Version { get; set; }
        public string LibraryName { get; set; }
        public int SlaveAXIDataWidth { get; set; }
        public int SlaveAXIAddrWidth { get; set; }
        public string AXIMinSize { get; set; }
        public int UseWRSTRB { get; set; }
        public int DPhaseTimeout { get; set; }
        public string BaseAddr { get; set; }
        public string HighAddr { get; set; }
        public string DeviceFamily { get; set; }
        public int NumMem { get; set; }
        public int SLVAWidth { get; set; }
        public int UserSLVNumReg { get; set; }
        public string TopEntityName { get; set; }
        public string TopFileName { get; set; }
        public int MasterAXIAddrWidth { get; set; }
        public int MasterAXIDataWidth { get; set; }
        public int MaxBurstLen { get; set; }
        public int NativeDataWidth { get; set; }
        public int LengthWidth { get; set; }
        public int AddrPipeDepth { get; set; }
        public List<UserPort> UserPorts { get; private set; }

        private Out<StdLogic>[,] _writeBits;
        private In<StdLogic>[,] _readBits;
        private List<ISignal> _writeSignals = new List<ISignal>();
        private List<ISignal> _readSignals = new List<ISignal>();

        public AXIMasterUserLogic(int numRegs, int slvDWidth)
        {
            NumRegs = numRegs;
            SLVDWidth = slvDWidth;
            SlaveAXIDataWidth = SLVDWidth;
            SlaveAXIAddrWidth = 32;
            AXIMinSize = "000001FF";
            UseWRSTRB = 0;
            DPhaseTimeout = 8;
            BaseAddr = "FFFFFFFF";
            HighAddr = "00000000";
            NumMem = 1;
            SLVAWidth = 32;
            UserSLVNumReg = NumRegs;
            MasterAXIAddrWidth = SlaveAXIAddrWidth;
            MasterAXIDataWidth = SlaveAXIDataWidth;
            MaxBurstLen = 16;
            NativeDataWidth = SlaveAXIDataWidth;
            LengthWidth = 12;
            AddrPipeDepth = 1;
            UserPorts = new List<UserPort>();

            _writeBits = new Out<StdLogic>[NumRegs, SLVDWidth];
            _readBits = new In<StdLogic>[NumRegs, SLVDWidth];
        }

        protected void MapSignal(SLVSignal sig, EAccessMode mode, int reg, int startBit)
        {
            if (mode != EAccessMode.Read)
            {
                for (int i = 0; i < sig.Size(); i++)
                    _writeBits[reg, i + startBit] = sig[i];
                _writeSignals.Add(sig);
            }
            if (mode != EAccessMode.Write)
            {
                for (int i = 0; i < sig.Size(); i++)
                    _readBits[reg, i + startBit] = sig[i];
                _readSignals.Add(sig);
            }
        }

        protected void MapSignal(SLSignal sig, EAccessMode mode, int reg, int startBit)
        {
            if (mode != EAccessMode.Read)
            {
                _writeBits[reg, startBit] = sig;
                _writeSignals.Add(sig);
            }
            if (mode != EAccessMode.Write)
            {
                _readBits[reg, startBit] = sig;
                _readSignals.Add(sig);
            }
        }

        private int NumWriteSignals
        {
            [StaticEvaluation]
            get { return _writeSignals.Count; }
        }

        [StaticEvaluation]
        private ISignal GetWriteSignal(int i)
        {
            return _writeSignals[i];
        }

        [StaticEvaluation]
        private Out<StdLogic> GetWriteBit(int reg, int byteIdx, int bitIdx)
        {
            return _writeBits[reg, byteIdx * 8 + bitIdx];
        }

        [StaticEvaluation]
        private bool IsWriteBitPresent(int reg, int byteIdx, int bitIdx)
        {
            return GetWriteBit(reg, byteIdx, bitIdx) != null;
        }

        private void WriteProcess()
        {
            if (Bus2IP_Clk.RisingEdge())
            {
                if (Bus2IP_Resetn.Cur == '0')
                {
                    for (int i = 0; i < NumWriteSignals; i++)
                    {
                        ProgramFlow.Unroll();
                        GetWriteSignal(i).NextObject = GetWriteSignal(i).InitialValueObject;
                    }
                }
                else
                {
                    for (int reg = 0; reg < NumRegs; reg++)
                    {
                        ProgramFlow.Unroll();

                        if (Bus2IP_WrCE.Cur[NumRegs - reg - 1] == '1')
                        {
                            for (int byteIdx = 0; byteIdx < SLVDWidth / 8; byteIdx++)
                            {
                                ProgramFlow.Unroll();

                                if (Bus2IP_BE.Cur[byteIdx] == '1')
                                {
                                    for (int bitIdx = 0; bitIdx < 8; bitIdx++)
                                    {
                                        ProgramFlow.Unroll();
                                        if (IsWriteBitPresent(reg, byteIdx, bitIdx))
                                            GetWriteBit(reg, byteIdx, bitIdx).Next = Bus2IP_Data.Cur[8 * byteIdx + bitIdx];
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [StaticEvaluation]
        private In<StdLogic> GetReadBit(int reg, int bitIdx)
        {
            return _readBits[reg, bitIdx];
        }

        [StaticEvaluation]
        private bool IsReadBitPresent(int reg, int bitIdx)
        {
            return GetReadBit(reg, bitIdx) != null;
        }

        private void ReadProcess()
        {
            StdLogicVector result = StdLogicVector._0s(SLVDWidth);

            for (int reg = 0; reg < NumRegs; reg++)
            {
                ProgramFlow.Unroll();

                if (Bus2IP_RdCE.Cur[NumRegs - reg - 1] == '1')
                {
                    for (int bit = 0; bit < SLVDWidth; bit++)
                    {
                        ProgramFlow.Unroll();

                        if (IsReadBitPresent(reg, bit))
                            result[bit] |= GetReadBit(reg, bit).Cur;
                    }
                }
            }

            IP2Bus_Data.Next = result;
        }

        private void FlagsProcess()
        {
            IP2Bus_RdAck.Next = Bus2IP_RdCE.Cur.Any();
            IP2Bus_WrAck.Next = Bus2IP_WrCE.Cur.Any();
            IP2Bus_Error.Next = '0';
        }

        [DoNotCallOnDecompilation]
        protected async Task<StdLogicVector> MasterRead(StdLogicVector addr)
        {
            ip2bus_mstrd_req.Next = '1';
            ip2bus_mstwr_req.Next = '0';
            ip2bus_mst_type.Next = '0';
            ip2bus_mst_addr.Next = addr;
            ip2bus_mst_be.Next = StdLogicVector._1s(NativeDataWidth / 8);
            ip2bus_mst_length.Next = StdLogicVector._0s(LengthWidth);
            ip2bus_mst_lock.Next = '0';
            ip2bus_mst_reset.Next = '0';
            ip2bus_mstrd_dst_rdy_n.Next = '0';
            ip2bus_mstrd_dst_dsc_n.Next = '1';
            do
            {
                await Tick;
            } while (bus2ip_mst_cmdack.Cur != '1');
            ip2bus_mstrd_req.Next = '0';
            do
            {
                await Tick;
            } while (bus2ip_mst_cmplt.Cur != '1');
            ip2bus_mstrd_dst_rdy_n.Next = '1';

            return bus2ip_mstrd_d.Cur;
        }

        [DoNotCallOnDecompilation]
        protected async void MasterWrite(StdLogicVector addr, StdLogicVector data)
        {
            ip2bus_mstrd_req.Next = '0';
            ip2bus_mstwr_req.Next = '1';
            ip2bus_mst_type.Next = '0';
            ip2bus_mst_addr.Next = addr;
            ip2bus_mst_be.Next = StdLogicVector._1s(NativeDataWidth / 8);
            ip2bus_mst_length.Next = StdLogicVector._0s(LengthWidth);
            ip2bus_mst_lock.Next = '0';
            ip2bus_mst_reset.Next = '0';
            ip2bus_mstwr_d.Next = data;
            ip2bus_mstwr_rem.Next = StdLogicVector._0s(NativeDataWidth / 8);
            ip2bus_mstwr_sof_n.Next = '0';
            ip2bus_mstwr_eof_n.Next = '0';
            ip2bus_mstwr_src_rdy_n.Next = '0';
            ip2bus_mstwr_src_dsc_n.Next = '1';
            do
            {
                await Tick;
            } while (bus2ip_mst_cmdack.Cur != '1');
            do
            {
                ip2bus_mstwr_req.Next = '0';
                if (bus2ip_mstwr_dst_rdy_n.Cur == '0')
                    break;
                await Tick;
            } while (true);
            ip2bus_mstwr_src_rdy_n.Next = '1';
            ip2bus_mstwr_sof_n.Next = '1';
            ip2bus_mstwr_eof_n.Next = '1';
            await Tick;
            ProgramFlow.IOBarrier();
        }

        protected override void Initialize()
        {
            AddProcess(WriteProcess, Bus2IP_Clk);
            var sens = new List<IInPort>();
            sens.Add(Bus2IP_RdCE);
            sens.AddRange(_readSignals.Cast<IInPort>());
            AddProcess(ReadProcess, sens.ToArray());
            AddProcess(FlagsProcess, Bus2IP_RdCE, Bus2IP_WrCE);
        }

        private string GetTypeString(TypeDescriptor td)
        {
            if (td.CILType.Equals(typeof(StdLogic)))
                return "std_logic";
            else if (td.CILType.Equals(typeof(StdLogicVector)))
                return "std_logic_vector(" + td.Constraints[0].FirstBound + " downto " + td.Constraints[0].SecondBound + ")";
            else
                throw new NotSupportedException("Only StdLogic/StdLogicVector supported for user ports");
        }

        private string GetDirString(EPortDirection dir)
        {
            switch (dir)
            {
                case EPortDirection.In: return "in";
                case EPortDirection.Out: return "out";
                case EPortDirection.InOut: return "inout";
                default: throw new NotImplementedException();
            }
        }

        protected override void OnAnalysis()
        {
            base.OnAnalysis();
            foreach (var pd in Descriptor.GetPorts())
            {
                var pdd = pd as PortDescriptor;
                if (pdd != null && 
                    !pdd.DeclarationSite.DeclaringType.Equals(typeof(AXIMasterUserLogic)))
                {
                    var up = new UserPort()
                    {
                        Name = pdd.Name,
                        Type = GetDirString(pd.Direction) + " " + GetTypeString(pdd.ElementType)
                    };
                    UserPorts.Add(up);
                }
            }
        }

        protected override void PreInitialize()
        {
            base.PreInitialize();

            var lib = Descriptor.Library;
            if (lib == null)
                lib = "work";
            LibraryName = lib;
            ImpEntityName = "implementation";
            ImpFileName = lib + "_" + ImpEntityName + ".vhd";
            CreationDate = DateTime.Now.ToString();
            Version = "1.00.a";
            int index = LibraryName.IndexOf('_');
            if (index > 0)
                TopEntityName = LibraryName.Remove(index);
            else
                TopEntityName = LibraryName;
            TopFileName = lib + "_" + TopEntityName + ".vhd";
        }

        private void CopyEDKFile(string libRoot, string fileName, string libName, XilinxProject xproj)
        {
            string path = xproj.AddFile(fileName);
            xproj.SetFileLibrary(fileName, libName);
            File.Copy(Path.Combine(libRoot, fileName), path, true);
        }

        private void CopyEDKFiles(string ipRoot, string libName, XilinxProject xproj)
        {
            string libRoot = Path.Combine(ipRoot, libName, "hdl", "vhdl");
            string[] files = Directory.GetFiles(libRoot, "*.vhd");
            foreach (string filePath in files)
            {
                string file = Path.GetFileName(filePath);
                CopyEDKFile(libRoot, file, libName, xproj);
            }
        }

        protected override void OnSynthesis(ISynthesisContext ctx)
        {
            var xproj = ctx.Project as XilinxProject;
            if (xproj != null)
            {
                DeviceFamily = xproj.DeviceFamily.ToString();

                string path = ctx.Project.AddFile(ImpFileName);
                ctx.Project.SetFileLibrary(ImpFileName, LibraryName);
                var impTT = new AXIMasterImp()
                {
                    Master = this
                };
                string content = impTT.TransformText();
                File.WriteAllText(path, content);

                path = ctx.Project.AddFile(TopFileName);
                ctx.Project.SetFileLibrary(TopFileName, LibraryName);
                var topTT = new AXIMasterTop()
                {
                    Master = this
                };
                content = topTT.TransformText();
                File.WriteAllText(path, content);

                string ipPath = Path.Combine(xproj.ISEBinPath, "..", "..", "..", "EDK", "hw", "XilinxProcessorIPLib", "pcores");
                CopyEDKFiles(ipPath, "proc_common_v3_00_a", xproj);
                CopyEDKFiles(ipPath, "axi_lite_ipif_v1_01_a", xproj);
                CopyEDKFiles(ipPath, "axi_master_burst_v1_00_a", xproj);
            }
            base.OnSynthesis(ctx);
        }
    }
}

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
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Common;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.Rewriters
{
    class XILMemoryMapperImpl: 
        XILSRewriter
    {
        private Dictionary<object, MemoryMappedStorage> _dataLayout = new Dictionary<object, MemoryMappedStorage>();
        private Dictionary<Variable, MemoryMappedStorage> _variableLayout = new Dictionary<Variable, MemoryMappedStorage>();
        private readonly IInstructionSet<XILInstr> _iset = DefaultInstructionSet.Instance;

        public XILMemoryMapperImpl(IList<XILSInstr> instrs, MemoryMapper mapper) :
            base(instrs)
        {
            Mapper = mapper;
        }

        public MemoryMapper Mapper { get; private set; }
        public bool MapConstantsToMemory { get; set; }
        public bool MapVariablesToMemory { get; set; }

        private void InitHandlers()
        {
            SetHandler(InstructionCodes.LdelemFixA, HandleLdelemFixA);
            SetHandler(InstructionCodes.StelemFixA, HandleStelemFixA);
            SetHandler(InstructionCodes.LdelemFixAFixI, HandleLdelemFixAFixI);
            SetHandler(InstructionCodes.StelemFixAFixI, HandleStelemFixAFixI);
            if (MapConstantsToMemory)
            {
                SetHandler(InstructionCodes.LdConst, HandleLdConst);
                SetHandler(InstructionCodes.Ld0, HandleLd0);
            }
            if (MapVariablesToMemory)
            {
                SetHandler(InstructionCodes.LoadVar, HandleLoadVar);
                SetHandler(InstructionCodes.StoreVar, HandleStoreVar);
            }
        }

        private bool NeedsMemoryMapping(object data)
        {
            return Marshal.SerializeForHW(data).Size > 
                Mapper.DefaultRegion.AddressWidth;
        }

        private MemoryMappedStorage GetDataLayout(object data)
        {
            MemoryMappedStorage result;
            if (!_dataLayout.TryGetValue(data, out result))
            {
                result = Mapper.DefaultRegion.Map(data);
                _dataLayout[data] = result;
            }
            return result;
        }

        private MemoryMappedStorage GetVariableLayout(Variable var)
        {
            MemoryMappedStorage result;
            if (!_variableLayout.TryGetValue(var, out result))
            {
                result = Mapper.DefaultRegion.Map(var);
                _variableLayout[var] = result;
            }
            return result;
        }

        private object Create0(TypeDescriptor type)
        {
            var slv = StdLogicVector.Serialize(type.GetSampleInstance());
            slv = StdLogicVector._0s(slv.Size);
            return StdLogicVector.Deserialize(slv, type.CILType);
        }

        protected override void PreProcess()
        {
            InitHandlers();
            foreach (XILSInstr xilsi in InInstructions)
            {
                switch (xilsi.Name)
                {
                    case InstructionCodes.LdelemFixA:
                    case InstructionCodes.StelemFixA:
                        GetDataLayout((Array)xilsi.StaticOperand);
                        break;

                    case InstructionCodes.LdelemFixAFixI:
                    case InstructionCodes.StelemFixAFixI:
                        GetDataLayout(((FixedArrayRef)xilsi.StaticOperand).ArrayObj);
                        break;

                    case InstructionCodes.LdConst:
                        if (MapConstantsToMemory && NeedsMemoryMapping(xilsi.StaticOperand))
                            GetDataLayout(xilsi.StaticOperand);
                        break;

                    case InstructionCodes.Ld0:
                        if (MapConstantsToMemory)
                        {
                            object value = Create0(xilsi.ResultTypes[0]);
                            if (NeedsMemoryMapping(value))
                                GetDataLayout(value);
                        }
                        break;

                    case InstructionCodes.LoadVar:
                        if (MapVariablesToMemory)
                            GetVariableLayout((Variable)xilsi.StaticOperand);
                        break;

                    case InstructionCodes.StoreVar:
                        if (MapVariablesToMemory)
                            GetVariableLayout((Variable)xilsi.StaticOperand);
                        break;

                    default:
                        break;
                }
            }
            Mapper.DoLayout();
        }

        private void EmitIndexComputation(Array array)
        {
            MemoryMappedStorage mms = GetDataLayout(array);
            ArrayMemoryLayout layout = mms.Layout as ArrayMemoryLayout;
            if (layout.ElementsPerWord > 1)
                throw new NotImplementedException("Multiple elements per word not yet implemented");
            MemoryRegion region = mms.Region;
            IMarshalInfo minfo = region.MarshalInfo;

            int shiftWidth = MathExt.CeilLog2(mms.Region.AddressWidth);
            TypeDescriptor indexType = TypeDescriptor.GetTypeOf(mms.BaseAddress);

            for (int i = 0; i < layout.Strides.Length; i++)
            {
                TypeDescriptor orgIndexType;
                if (i > 0)
                {
                    orgIndexType = TypeStack.Skip(1).First();
                    Emit(_iset.Swap().CreateStk(2, orgIndexType, indexType, indexType, orgIndexType));
                }
                else
                {
                    orgIndexType = TypeStack.Peek();
                }

                ulong stride = layout.Strides[layout.Strides.Length - i - 1];

                if (MathExt.IsPow2(stride))
                {
                    Emit(_iset.Convert().CreateStk(1, orgIndexType, indexType));
                    int ishift = MathExt.CeilLog2(stride);
                    if (ishift > 0)
                    {
                        Unsigned shift = Unsigned.FromULong((ulong)ishift, shiftWidth);
                        TypeDescriptor shiftType = TypeDescriptor.GetTypeOf(shift);
                        Emit(_iset.LdConst(shift).CreateStk(0, shiftType));
                        Emit(_iset.LShift().CreateStk(2, indexType, shiftType, indexType));
                    }
                }
                else
                {
                    throw new NotImplementedException("Stride ain't a power of 2");
                }

                if (i > 0)
                {
                    Emit(_iset.Or().CreateStk(2, indexType, indexType, indexType));
                }
            }

            if (mms.BaseAddress.ULongValue != 0)
            {
                Emit(_iset.LdConst(mms.BaseAddress).CreateStk(0, indexType));
                if (minfo.UseStrongPow2Alignment)
                    Emit(_iset.Or().CreateStk(2, indexType, indexType, indexType));
                else
                    Emit(_iset.Add().CreateStk(2, indexType, indexType, indexType));
            }
        }

        private Unsigned ComputeConstAddress(Array array, long[] indices, uint nword)
        {
            MemoryMappedStorage mms = GetDataLayout(array);
            ArrayMemoryLayout layout = mms.Layout as ArrayMemoryLayout;
            if (layout.ElementsPerWord > 1)
                throw new NotImplementedException("Multiple elements per word not yet implemented");
            MemoryRegion region = mms.Region;
            IMarshalInfo minfo = region.MarshalInfo;

            Unsigned addr = mms.BaseAddress;
            for (int i = 0; i < indices.Length; i++)
            {
                Unsigned offs = Unsigned.FromULong((ulong)indices[i] * layout.Strides[i], mms.Region.AddressWidth);
                addr += offs;
            }
            addr += Unsigned.FromUInt(nword, mms.Region.AddressWidth);
            addr = addr.Resize(mms.Region.AddressWidth);

            return addr;
        }

        private void HandleLdelemFixA(XILSInstr xilsi)
        {
            Array array = (Array)xilsi.StaticOperand;
            var preds = RemapPreds(xilsi.Preds);
            MemoryMappedStorage mms = GetDataLayout(array);
            ArrayMemoryLayout layout = mms.Layout as ArrayMemoryLayout;
            if (layout.ElementsPerWord > 1)
                throw new NotImplementedException("Multiple elements per word not yet implemented");
            MemoryRegion region = mms.Region;
            IMarshalInfo minfo = region.MarshalInfo;

            EmitIndexComputation(array);

            TypeDescriptor indexType = TypeDescriptor.GetTypeOf(mms.BaseAddress);
            TypeDescriptor dwType = minfo.GetRawWordType();
            if (layout.WordsPerElement > 1)
            {
                Emit(_iset.Dup().CreateStk(1, indexType, indexType, indexType));
            }
            for (uint i = 0; i < layout.WordsPerElement; i++)
            {
                Emit(_iset.RdMem(region).CreateStk(preds, 1, indexType, dwType));
                if (i < layout.WordsPerElement - 1)
                {
                    Emit(_iset.Swap().CreateStk(2, indexType, dwType, dwType, indexType));
                    if (minfo.UseStrongPow2Alignment)
                    {
                        if (i + 1 < layout.WordsPerElement - 1)
                        {
                            Emit(_iset.Dup().CreateStk(1, indexType, indexType, indexType));
                        }
                        Unsigned inc = Unsigned.FromULong(i + 1, region.AddressWidth);
                        Emit(_iset.LdConst(inc).CreateStk(0, indexType));
                        Emit(_iset.Or().CreateStk(2, indexType, indexType, indexType));
                    }
                    else
                    {
                        Unsigned inc = Unsigned.FromULong(1, region.AddressWidth);
                        Emit(_iset.LdConst(inc).CreateStk(0, indexType));
                        Emit(_iset.Add().CreateStk(2, indexType, indexType, indexType));
                        if (i + 1 < layout.WordsPerElement - 1)
                        {
                            Emit(_iset.Dup().CreateStk(1, indexType, indexType, indexType));
                        }
                    }
                }
            }

            uint concatTypeSize = minfo.WordSize * layout.WordsPerElement; 
            TypeDescriptor concatType = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s(concatTypeSize));

            if (layout.WordsPerElement > 1)
            {
                TypeDescriptor[] stackTypes = new TypeDescriptor[layout.WordsPerElement + 1];
                for (uint i = 0; i < layout.WordsPerElement; i++)
                    stackTypes[i] = dwType;
                stackTypes[layout.WordsPerElement] = concatType;
                Emit(_iset.Concat().CreateStk((int)layout.WordsPerElement, stackTypes));
            }

            TypeDescriptor elemTypeRaw = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s((int)layout.ElementLayout.SizeInBits));
            if (concatTypeSize != layout.ElementLayout.SizeInBits)
            {
                Emit(_iset.Convert().CreateStk(1, concatType, elemTypeRaw));
            }
            TypeDescriptor elemType = layout.ElementLayout.LayoutedType;
            Emit(_iset.Convert().CreateStk(1, elemTypeRaw, elemType));
        }

        private void HandleLdelemFixAFixI(XILSInstr xilsi)
        {
            FixedArrayRef far = (FixedArrayRef)xilsi.StaticOperand;
            Array array = far.ArrayObj;
            long[] indices = far.Indices;
            var preds = RemapPreds(xilsi.Preds);
            MemoryMappedStorage mms = GetDataLayout(array);
            ArrayMemoryLayout layout = mms.Layout as ArrayMemoryLayout;
            if (layout.ElementsPerWord > 1)
                throw new NotImplementedException("Multiple elements per word not yet implemented");
            MemoryRegion region = mms.Region;
            IMarshalInfo minfo = region.MarshalInfo;

            TypeDescriptor dwType = minfo.GetRawWordType();
            for (uint i = 0; i < layout.WordsPerElement; i++)
            {
                Unsigned addr = ComputeConstAddress(array, indices, i);
                Emit(_iset.LdConst(addr)
                    .CreateStk(0, TypeDescriptor.GetTypeOf(addr)));
                Emit(_iset.RdMem(region)
                    .CreateStk(preds, 1, TypeDescriptor.GetTypeOf(addr), dwType));
            }
            uint concatTypeSize = minfo.WordSize * layout.WordsPerElement;
            TypeDescriptor concatType = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s(concatTypeSize));

            if (layout.WordsPerElement > 1)
            {
                TypeDescriptor[] stackTypes = new TypeDescriptor[layout.WordsPerElement + 1];
                for (uint i = 0; i < layout.WordsPerElement; i++)
                    stackTypes[i] = dwType;
                stackTypes[layout.WordsPerElement] = concatType;
                Emit(_iset.Concat().CreateStk((int)layout.WordsPerElement, stackTypes));
            }

            TypeDescriptor elemTypeRaw = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s((int)layout.ElementLayout.SizeInBits));
            if (concatTypeSize != layout.ElementLayout.SizeInBits)
            {
                Emit(_iset.Convert().CreateStk(1, concatType, elemTypeRaw));
            }
            TypeDescriptor elemType = layout.ElementLayout.LayoutedType;
            Emit(_iset.Convert().CreateStk(1, elemTypeRaw, elemType));
        }

        private void HandleStelemFixA(XILSInstr xilsi)
        {
            Array array = (Array)xilsi.StaticOperand;
            var preds = RemapPreds(xilsi.Preds);
            MemoryMappedStorage mms = GetDataLayout(array);
            ArrayMemoryLayout layout = mms.Layout as ArrayMemoryLayout;
            if (layout.ElementsPerWord > 1)
                throw new NotImplementedException("Multiple elements per word not yet implemented");
            MemoryRegion region = mms.Region;
            IMarshalInfo minfo = region.MarshalInfo;

            EmitIndexComputation(array);

            TypeDescriptor indexType = TypeDescriptor.GetTypeOf(mms.BaseAddress);
            TypeDescriptor elemType = layout.ElementLayout.LayoutedType;
            Emit(_iset.Swap().CreateStk(2, elemType, indexType, indexType, elemType));

            TypeDescriptor rawElemType = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s((long)layout.ElementLayout.SizeInBits));
            if (!elemType.Equals(rawElemType))
            {
                Emit(_iset.Convert().CreateStk(1, elemType, rawElemType));
            }

            uint concatSize = layout.WordsPerElement * minfo.WordSize;
            TypeDescriptor concatType = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s(concatSize));
            if (!concatType.Equals(rawElemType))
            {
                Emit(_iset.Convert().CreateStk(1, rawElemType, concatType));
            }

            TypeDescriptor rawWordType = minfo.GetRawWordType();
            int shiftSize = MathExt.CeilLog2(concatSize);

            if (layout.WordsPerElement > 1)
            {
                Emit(_iset.Swap().CreateStk(2, indexType, rawElemType, rawElemType, indexType));
                Emit(_iset.Dup().CreateStk(1, indexType, indexType, indexType));
                Emit(_iset.Dig(2).CreateStk(3, concatType, indexType, indexType, indexType, indexType, concatType));
                Emit(_iset.Dup().CreateStk(1, concatType, concatType, concatType));
            }
            for (uint i = 0; i < layout.WordsPerElement; i++)
            {
                Emit(_iset.Convert().CreateStk(1, concatType, rawWordType));
                if (i < layout.WordsPerElement - 1)
                {
                    Emit(_iset.Dig(2).CreateStk(3, indexType, concatType, rawWordType, concatType, rawWordType, indexType));
                }
                else
                {
                    Emit(_iset.Swap().CreateStk(2, indexType, rawWordType, rawWordType, indexType));
                }
                Emit(_iset.WrMem(region).CreateStk(preds, 2, rawWordType, indexType));
                if (i < layout.WordsPerElement - 1)
                {
                    Unsigned shift = Unsigned.FromULong(minfo.WordSize, shiftSize);
                    TypeDescriptor shiftType = TypeDescriptor.GetTypeOf(shift);
                    Emit(_iset.LdConst(shift).CreateStk(0, shiftType));
                    Emit(_iset.RShift().CreateStk(2, concatType, shiftType, concatType));
                    Emit(_iset.Swap().CreateStk(2, indexType, concatType, concatType, indexType));
                    if (minfo.UseStrongPow2Alignment)
                    {
                        if (i + 1 < layout.WordsPerElement)
                        {
                            Emit(_iset.Dup().CreateStk(1, indexType, indexType, indexType));
                        }
                        Unsigned inc = Unsigned.FromULong(i + 1, region.AddressWidth);
                        Emit(_iset.Or().CreateStk(2, indexType, indexType, indexType));
                        if (i + 1 < layout.WordsPerElement)
                        {
                            Emit(_iset.Dig(2).CreateStk(1, concatType, indexType, indexType, indexType, indexType, concatType));
                            Emit(_iset.Swap().CreateStk(2, concatType, indexType, indexType, concatType));
                        }
                    }
                    else
                    {
                        Unsigned inc = Unsigned.FromULong(1, region.AddressWidth);
                        Emit(_iset.LdConst(inc).CreateStk(0, indexType));
                        Emit(_iset.Add().CreateStk(2, indexType, indexType, indexType));
                        Emit(_iset.Swap().CreateStk(2, concatType, indexType, indexType, concatType));
                    }
                }
            }
        }

        private void HandleStelemFixAFixI(XILSInstr xilsi)
        {
            FixedArrayRef far = (FixedArrayRef)xilsi.StaticOperand;
            Array array = far.ArrayObj;
            long[] indices = far.Indices;
            var preds = RemapPreds(xilsi.Preds);
            MemoryMappedStorage mms = GetDataLayout(array);
            ArrayMemoryLayout layout = mms.Layout as ArrayMemoryLayout;
            if (layout.ElementsPerWord > 1)
                throw new NotImplementedException("Multiple elements per word not yet implemented");
            MemoryRegion region = mms.Region;
            IMarshalInfo minfo = region.MarshalInfo;

            TypeDescriptor elemType = layout.ElementLayout.LayoutedType;
            TypeDescriptor rawElemType = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s((long)layout.ElementLayout.SizeInBits));

            if (!elemType.Equals(rawElemType))
            {
                Emit(_iset.Convert().CreateStk(1, elemType, rawElemType));
            }

            uint concatSize = layout.WordsPerElement * minfo.WordSize;
            TypeDescriptor concatType = TypeDescriptor.GetTypeOf(
                StdLogicVector._0s(concatSize));
            if (!concatType.Equals(rawElemType))
            {
                Emit(_iset.Convert().CreateStk(1, rawElemType, concatType));
            }

            TypeDescriptor rawWordType = minfo.GetRawWordType();
            int shiftSize = MathExt.CeilLog2(concatSize);

            if (layout.WordsPerElement > 1)
            {
                Emit(_iset.Dup().CreateStk(1, concatType, concatType, concatType));
            }
            Unsigned shift = Unsigned.FromULong(minfo.WordSize, shiftSize);
            TypeDescriptor shiftType = TypeDescriptor.GetTypeOf(shift);
            TypeDescriptor dwType = minfo.GetRawWordType();
            for (uint i = 0; i < layout.WordsPerElement; i++)
            {
                Unsigned addr = ComputeConstAddress(array, indices, i);
                Emit(_iset.LdConst(addr)
                    .CreateStk(0, TypeDescriptor.GetTypeOf(addr)));
                Emit(_iset.WrMem(region)
                    .CreateStk(preds, 2, dwType, TypeDescriptor.GetTypeOf(addr)));
                if (i < layout.WordsPerElement - 1)
                {
                    Emit(_iset.LdConst(shift).CreateStk(0, shiftType));
                    Emit(_iset.RShift().CreateStk(2, concatType, shiftType, concatType));
                    if (i + 1 < layout.WordsPerElement - 1)
                    {
                        Emit(_iset.Dup().CreateStk(1, concatType, concatType, concatType));
                    }
                }
            }
        }

        private void ImplementLdConst(object value)
        {
            var mms = GetDataLayout(value);
            Unsigned addr = mms.BaseAddress;
            var addrType = TypeDescriptor.GetTypeOf(addr);
            int addrSize = addr.Size;
            var rawWordType = Mapper.MarshalInfo.GetRawWordType();
            var stackTypes = new TypeDescriptor[mms.Size + 1];
            for (ulong k = 0; k < mms.Size; k++)
            {
                Emit(_iset.RdMemFix(Mapper.DefaultRegion, addr).CreateStk(0, rawWordType));
                addr = (addr + Unsigned.FromULong(1, 1)).Resize(addrSize);
                stackTypes[k] = rawWordType;
            }
            uint concatSize = (uint)(mms.Size * Mapper.MarshalInfo.WordSize);
            var concatType = TypeDescriptor.GetTypeOf(StdLogicVector._0s(concatSize));
            if (mms.Size > 1)
            {
                stackTypes[mms.Size] = concatType;
                Emit(_iset.Concat().CreateStk(stackTypes.Length, stackTypes));
            }
            var valueType = TypeDescriptor.GetTypeOf(value);
            var slvValue = StdLogicVector.Serialize(value);
            var rawValueType = TypeDescriptor.GetTypeOf(slvValue);
            if (!rawValueType.Equals(concatType))
            {
                Emit(_iset.Convert().CreateStk(1, concatType, rawValueType));
            }
            if (!rawValueType.Equals(valueType))
            {
                Emit(_iset.Convert().CreateStk(1, rawValueType, valueType));
            }
        }

        private void HandleLdConst(XILSInstr xilsi)
        {
            object value = xilsi.StaticOperand;
            if (NeedsMemoryMapping(value))
            {
                ImplementLdConst(value);
            }
            else
            {
                Emit(xilsi);
            }
        }

        private void HandleLd0(XILSInstr xilsi)
        {
            object value = Create0(xilsi.ResultTypes[0]);
            if (NeedsMemoryMapping(value))
            {
                ImplementLdConst(value);
            }
            else
            {
                Emit(xilsi);
            }
        }

        private void HandleLoadVar(XILSInstr xilsi)
        {
            var var = (Variable)xilsi.StaticOperand;
            var mms = GetVariableLayout(var);
            Unsigned addr = mms.BaseAddress;
            var addrType = TypeDescriptor.GetTypeOf(addr);
            int addrSize = addr.Size;
            var rawWordType = Mapper.MarshalInfo.GetRawWordType();
            var stackTypes = new TypeDescriptor[mms.Size + 1];
            for (ulong k = 0; k < mms.Size; k++)
            {
                Emit(_iset.RdMemFix(Mapper.DefaultRegion, addr)
                    .CreateStk(RemapPreds(xilsi.Preds), 0, rawWordType));
                addr = (addr + Unsigned.FromULong(1, 1)).Resize(addrSize);
                stackTypes[k] = rawWordType;
            }
            uint concatSize = (uint)(mms.Size * Mapper.MarshalInfo.WordSize);
            var concatType = TypeDescriptor.GetTypeOf(StdLogicVector._0s(concatSize));
            if (mms.Size > 1)
            {
                stackTypes[mms.Size] = concatType;
                Emit(_iset.Concat().CreateStk(stackTypes.Length, stackTypes));
            }
            var valueType = var.Type;
            var slvValue = Marshal.SerializeForHW(valueType.GetSampleInstance());
            var rawValueType = TypeDescriptor.GetTypeOf(slvValue);
            if (!rawValueType.Equals(concatType))
            {
                Emit(_iset.Convert().CreateStk(1, concatType, rawValueType));
            }
            if (!rawValueType.Equals(valueType))
            {
                Emit(_iset.Convert().CreateStk(1, rawValueType, valueType));
            }
        }

        private void HandleStoreVar(XILSInstr xilsi)
        {
            var var = (Variable)xilsi.StaticOperand;
            var mms = GetVariableLayout(var);
            Unsigned addr = mms.BaseAddress;
            var addrType = TypeDescriptor.GetTypeOf(addr);
            int addrSize = addr.Size;
            var rawWordType = Mapper.MarshalInfo.GetRawWordType();
            uint concatSize = (uint)(mms.Size * Mapper.MarshalInfo.WordSize);
            var concatType = TypeDescriptor.GetTypeOf(StdLogicVector._0s(concatSize));
            var valueType = var.Type;
            var slvValue = Marshal.SerializeForHW(valueType.GetSampleInstance());
            var rawValueType = TypeDescriptor.GetTypeOf(slvValue);
            if (!rawValueType.Equals(valueType))
            {
                Emit(_iset.Convert().CreateStk(1, valueType, rawValueType));
            }
            if (!rawValueType.Equals(concatType))
            {
                Emit(_iset.Convert().CreateStk(1, rawValueType, concatType));
            }
            int shiftSize = MathExt.CeilLog2(concatSize);

            if (mms.Layout.Size > 1)
            {
                Emit(_iset.Dup().CreateStk(1, concatType, concatType, concatType));
            }
            Unsigned shift = Unsigned.FromULong(Mapper.MarshalInfo.WordSize, shiftSize);
            TypeDescriptor shiftType = TypeDescriptor.GetTypeOf(shift);
            for (ulong i = 0; i < mms.Layout.Size; i++)
            {
                if (!rawWordType.Equals(concatType))
                {
                    Emit(_iset.Convert().CreateStk(1, concatType, rawWordType));
                }
                Emit(_iset.WrMemFix(Mapper.DefaultRegion, addr).CreateStk(1, rawWordType));
                if (i < mms.Layout.Size - 1)
                {
                    if (i + 1 < mms.Layout.Size - 1)
                    {
                        Emit(_iset.Dup().CreateStk(1, concatType, concatType, concatType));
                    }
                    Emit(_iset.LdConst(shift).CreateStk(0, shiftType));
                    Emit(_iset.RShift().CreateStk(1, concatType, concatType));                    
                }
                addr = (addr + Unsigned.FromULong(1, 1)).Resize(addrSize);
            }            
        }

        protected override void PostProcess()
        {
            base.PostProcess();
            var bbs = XILSInstructionInfo.GetBasicBlocks(OutInstructions);
            var mmap = new Dictionary<MemoryRegion, int>();
            foreach (var bb in bbs)
            {
                foreach (var xilsi in bb.ToArray())
                {
                    MemoryRegion rgn = null;
                    switch (xilsi.Name)
                    {                         
                        case InstructionCodes.RdMem:
                        case InstructionCodes.WrMem:
                            rgn = (MemoryRegion)xilsi.StaticOperand;
                            break;

                        case InstructionCodes.RdMemFix:
                        case InstructionCodes.WrMemFix:
                            rgn = ((Tuple<MemoryRegion, Unsigned>)xilsi.StaticOperand).Item1;
                            break;
                    }
                    if (rgn != null)
                    {
                        int pred;
                        if (mmap.TryGetValue(rgn, out pred))
                        {
                            var preds = xilsi.Preds.Union(
                                new InstructionDependency[] {
                                    new OrderDependency(pred, OrderDependency.EKind.BeginAfter) }).ToArray();
                            var newXilsi = xilsi.Command.CreateStk(preds, xilsi.OperandTypes, xilsi.ResultTypes);
                            newXilsi.Index = xilsi.Index;
                            OutInstructions[xilsi.Index] = newXilsi;
                        }
                        mmap[rgn] = xilsi.Index;
                    }
                }
                mmap.Clear();
            }
        }
    }

    /// <summary>
    /// This XIL-S code transformation maps array operations to memory operations.
    /// Optionally, it can also map constants and variables to memory.
    /// </summary>
    public class XILMemoryMapper : 
        IXILSRewriter,
        IReportingXILRewriter
    {
        /// <summary>
        /// The memory mapper implements the serialization and data layout policy.
        /// </summary>
        public MemoryMapper Mapper { get; set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        public XILMemoryMapper()
        {
            Mapper = new MemoryMapper();
        }

        /// <summary>
        /// Whether to map constants to memory
        /// </summary>
        public bool MapConstantsToNemory { get; set; }

        /// <summary>
        /// Whether to map variables to memory
        /// </summary>
        public bool MapVariablesToMemory { get; set; }

        public override string ToString()
        {
            return "MemMapper";
        }

        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            XILMemoryMapperImpl xmm = new XILMemoryMapperImpl(instrs, Mapper)
            {
                MapConstantsToMemory = this.MapConstantsToNemory,
                MapVariablesToMemory = this.MapVariablesToMemory
            };
            xmm.Rewrite();
            return xmm.OutInstructions;
        }

        public void GetReport(Stream stm)
        {
            StreamWriter sw = new StreamWriter(stm);
            sw.WriteLine("Default memory region");
            foreach (MemoryMappedStorage mms in Mapper.DefaultRegion.Items)
            {
                switch (mms.Kind)
                {
                    case MemoryMappedStorage.EKind.Data:
                        sw.WriteLine("Data " + mms.Data);
                        break;

                    case MemoryMappedStorage.EKind.DataItem:
                        sw.WriteLine("Data item " + mms.DataItem + " of type " + mms.DataItemType);
                        break;

                    case MemoryMappedStorage.EKind.Variable:
                        sw.WriteLine("Variable " + mms.Variable.Name);
                        break;

                    default:
                        throw new NotImplementedException();
                }
                sw.WriteLine("  Offset: {0:X}, Size: {1:X}", mms.Offset, mms.Size);
            }
        }
    }
}

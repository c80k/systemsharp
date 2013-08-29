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
using System.Reflection;
using System.Text;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    public static class InstructionCodes
    {
        public const string Abs = "Abs";
        public const string Add = "Add";
        public const string And = "And";
        public const string BranchIfTrue = "BrTrue";
        public const string BranchIfFalse = "BrFalse";
        public const string Ceil = "Ceil";
        public const string Cmp = "Cmp";
        public const string Concat = "Concat";
        public const string Convert = "Convert";
        public const string Cos = "Cos";
        public const string ScCos = "ScCos";
        public const string Sin = "Sin";
        public const string ScSin = "ScSin";
        public const string SinCos = "SinCos";
        public const string ScSinCos = "ScSinCos";
        public const string Div = "Div";
        public const string DivQF = "DivQF";
        public const string Floor = "Floor";
        public const string Goto = "Goto";
        public const string IsEq = "IsEq";
        public const string IsGt = "IsGt";
        public const string IsGte = "IsGte";
        public const string IsLt = "IsLt";
        public const string IsLte = "IsLte";
        public const string IsNEq = "IsNEq";
        public const string LdConst = "LdConst";
        public const string Ld0 = "Ld0";
        public const string LShift = "LShift";
        public const string Max = "Max";
        public const string Min = "Min";
        public const string Mul = "Mul";
        public const string Or = "Or";
        public const string RdMemFix = "RdMemFix";
        public const string RdMem = "RdMem";
        public const string Rem = "Rem";
        public const string Rempow2 = "Rempow2";
        public const string RShift = "RShift";
        public const string RdPort = "RdPort";
        public const string Select = "Select";
        public const string Slice = "Slice";
        public const string SliceFixI = "SliceFixI";
        public const string Sqrt = "Sqrt";
        public const string Sub = "Sub";
        public const string Neg = "Neg";
        public const string Nop = "Nop";
        public const string Not = "Not";
        public const string WrMemFix = "WrMemFix";
        public const string WrMem = "WrMem";
        public const string WrPort = "WrPort";
        public const string Xor = "Xor";
        public const string ExtendSign = "Xts";
        public const string LoadVar = "Ldv";
        public const string StoreVar = "Stv";
        public const string Return = "Ret";
        public const string ExitMarshal = "$Exit";

        /// <summary>
        /// Computes r = x mod 2.0 with r > -1, r <= 1
        /// </summary>
        public const string Mod2 = "Mod2";

        /// <summary>
        /// Loads an element from a fixed array
        /// </summary>
        public const string LdelemFixA = "LdelemFixA";

        /// <summary>
        /// Loads an element having a fixed index from a fixed array
        /// </summary>
        public const string LdelemFixAFixI = "LdelemFixAFixI";

        /// <summary>
        /// Stores an element in a fixed array
        /// </summary>
        public const string StelemFixA = "StelemFix";

        /// <summary>
        /// Stores an element having a fixed index in a fixed array
        /// </summary>
        public const string StelemFixAFixI = "StelemFixAFixI";

        /// <summary>
        /// Loads the base address of a memory-mapped storage element
        /// </summary>
        public const string LdMemBase = "LdMemBase";

        /// <summary>
        /// Returns the sign of the given operand. 
        /// 1 if operand &gt; 0, 0 if operand == 0, -1 of operand &lt; 0
        /// </summary>
        public const string Sign = "Sign";

        // Stack only:
        public const string Pop = "Pop";
        public const string Dup = "Dup";
        public const string Swap = "Swap";
        public const string Dig = "Dig";

        // Prefix instructions:
        public const string Barrier = "Barrier";

        public static IEnumerable<string> AllCodes
        {
            get
            {
                Type myType = typeof(InstructionCodes);
                FieldInfo[] fields = myType.GetFields();
                return fields.Select(fi => (string)fi.GetValue(null));
            }
        }
    }

    public interface IInstructionSet<T>
    {
        T Goto(BranchLabel target);
        T BranchIfTrue(BranchLabel target);
        T BranchIfFalse(BranchLabel target);
        T LdConst(object value);
        T Ld0();
        T Abs();
        T Nop();
        T Not();
        T Neg();
        T ExtendSign();
        T Add();
        T Sub();
        T Mul();
        T Div();
        T DivQF();
        T And();
        T Or();
        T Xor();
        T Concat();
        T IsLt();
        T IsLte();
        T IsEq();
        T IsNEq();
        T IsGte();
        T IsGt();
        T LShift();
        T RShift();
        T Rem();
        T Rempow2(int i);
        T Select();
        T Slice();
        T SliceFixI(Range limits);
        T Convert(bool reinterpret = false);
        T Cos();
        T Sin();
        T ScCos();
        T ScSin();
        T SinCos();
        T ScSinCos();
        T Sqrt();
        T LoadVar(IStorableLiteral v);
        T StoreVar(IStorableLiteral v);
        T ReadPort(ISignalOrPortDescriptor sd);
        T WritePort(ISignalOrPortDescriptor sd);
        T Return();
        T ExitMarshal();
        T Pop();
        T Dup();
        T Swap();
        T Dig(int pos);
        T Barrier();
        T Mod2();
        T LdelemFixA(FixedArrayRef far);
        T LdelemFixAFixI(FixedArrayRef far);
        T StelemFixA(FixedArrayRef far);
        T StelemFixAFixI(FixedArrayRef far);
        T LdMemBase(MemoryMappedStorage mms);
        T RdMem(MemoryRegion region);
        T RdMemFix(MemoryRegion region, Unsigned addr);
        T WrMem(MemoryRegion region);
        T WrMemFix(MemoryRegion region, Unsigned addr);
        T Sign();
    }

    public class DefaultInstructionSet : IInstructionSet<XILInstr>
    {
        private DefaultInstructionSet()
        {
        }

        public static readonly DefaultInstructionSet Instance = new DefaultInstructionSet();

        public static readonly InstructionDependency[] Empty = new InstructionDependency[0];

        public XILInstr Goto(BranchLabel target)
        {
            return new XILInstr(InstructionCodes.Goto, target);
        }

        public XILInstr BranchIfTrue(BranchLabel target)
        {
            return new XILInstr(InstructionCodes.BranchIfTrue, target);
        }

        public XILInstr BranchIfFalse(BranchLabel target)
        {
            return new XILInstr(InstructionCodes.BranchIfFalse, target);
        }

        public XILInstr RdMemFix(MemoryRegion region, Unsigned addr)
        {
            return new XILInstr(InstructionCodes.RdMemFix, Tuple.Create(region, addr));
        }

        public XILInstr WrMemFix(MemoryRegion region, Unsigned addr)
        {
            return new XILInstr(InstructionCodes.WrMemFix, Tuple.Create(region, addr));
        }

        public XILInstr LdConst(object value)
        {
            return new XILInstr(InstructionCodes.LdConst, value);
        }

        public XILInstr Ld0()
        {
            return new XILInstr(InstructionCodes.Ld0);
        }

        public XILInstr Abs()
        {
            return new XILInstr(InstructionCodes.Abs);
        }

        public XILInstr Nop()
        {
            return new XILInstr(InstructionCodes.Nop);
        }

        public XILInstr Nop(int latency)
        {
            return new XILInstr(InstructionCodes.Nop, latency);
        }

        public XILInstr Not()
        {
            return new XILInstr(InstructionCodes.Not);
        }

        public XILInstr Neg()
        {
            return new XILInstr(InstructionCodes.Neg);
        }

        public XILInstr ExtendSign()
        {
            return new XILInstr(InstructionCodes.ExtendSign);
        }

        public XILInstr Add()
        {
            return new XILInstr(InstructionCodes.Add);
        }

        public XILInstr Sub()
        {
            return new XILInstr(InstructionCodes.Sub);
        }

        public XILInstr Mul()
        {
            return new XILInstr(InstructionCodes.Mul);
        }

        public XILInstr Div()
        {
            return new XILInstr(InstructionCodes.Div);
        }

        public XILInstr DivQF()
        {
            return new XILInstr(InstructionCodes.DivQF);
        }

        public XILInstr And()
        {
            return new XILInstr(InstructionCodes.And);
        }

        public XILInstr Or()
        {
            return new XILInstr(InstructionCodes.Or);
        }

        public XILInstr Xor()
        {
            return new XILInstr(InstructionCodes.Xor);
        }

        public XILInstr Concat()
        {
            return new XILInstr(InstructionCodes.Concat);
        }

        public XILInstr IsLt()
        {
            return new XILInstr(InstructionCodes.IsLt);
        }

        public XILInstr IsLte()
        {
            return new XILInstr(InstructionCodes.IsLte);
        }

        public XILInstr IsEq()
        {
            return new XILInstr(InstructionCodes.IsEq);
        }

        public XILInstr IsNEq()
        {
            return new XILInstr(InstructionCodes.IsNEq);
        }

        public XILInstr IsGte()
        {
            return new XILInstr(InstructionCodes.IsGte);
        }

        public XILInstr IsGt()
        {
            return new XILInstr(InstructionCodes.IsGt);
        }

        public XILInstr LShift()
        {
            return new XILInstr(InstructionCodes.LShift);
        }

        public XILInstr RShift()
        {
            return new XILInstr(InstructionCodes.RShift);
        }

        public XILInstr Rem()
        {
            return new XILInstr(InstructionCodes.Rem);
        }

        public XILInstr Rempow2(int n)
        {
            return new XILInstr(InstructionCodes.Rempow2, n);
        }

        /// <summary>
        /// Creates an instruction which conceptually performs the operation "c ? x : y"
        /// Operand 0: x
        /// Operand 1: y
        /// Operand 2: c
        /// </summary>
        /// <returns>The XIL instruction</returns>
        public XILInstr Select()
        {
            return new XILInstr(InstructionCodes.Select);
        }

        public XILInstr Slice()
        {
            return new XILInstr(InstructionCodes.Slice);
        }

        public XILInstr SliceFixI(Range range)
        {
            return new XILInstr(InstructionCodes.SliceFixI, range);
        }

        public XILInstr Convert(bool reinterpret = false)
        {
            return new XILInstr(InstructionCodes.Convert, reinterpret);
        }

        public XILInstr Cos()
        {
            return new XILInstr(InstructionCodes.Cos);
        }

        public XILInstr Sin()
        {
            return new XILInstr(InstructionCodes.Sin);
        }

        public XILInstr ScCos()
        {
            return new XILInstr(InstructionCodes.ScCos);
        }

        public XILInstr ScSin()
        {
            return new XILInstr(InstructionCodes.ScSin);
        }

        public XILInstr SinCos()
        {
            return new XILInstr(InstructionCodes.SinCos);
        }

        public XILInstr ScSinCos()
        {
            return new XILInstr(InstructionCodes.ScSinCos);
        }

        public XILInstr Sqrt()
        {
            return new XILInstr(InstructionCodes.Sqrt);
        }

        public XILInstr LoadVar(IStorableLiteral v)
        {
            return new XILInstr(InstructionCodes.LoadVar, v);
        }

        public XILInstr StoreVar(IStorableLiteral v)
        {
            return new XILInstr(InstructionCodes.StoreVar, v);
        }

        public XILInstr ReadPort(ISignalOrPortDescriptor sd)
        {
            return new XILInstr(InstructionCodes.RdPort, sd);
        }

        public XILInstr WritePort(ISignalOrPortDescriptor sd)
        {
            return new XILInstr(InstructionCodes.WrPort, sd);
        }

        public XILInstr Return()
        {
            return new XILInstr(InstructionCodes.Return);
        }

        public XILInstr ExitMarshal()
        {
            return new XILInstr(InstructionCodes.ExitMarshal);
        }

        public XILInstr Pop()
        {
            return new XILInstr(InstructionCodes.Pop);
        }

        public XILInstr Dup()
        {
            return new XILInstr(InstructionCodes.Dup);
        }

        public XILInstr Swap()
        {
            return new XILInstr(InstructionCodes.Swap);
        }

        public XILInstr Dig(int pos)
        {
            return new XILInstr(InstructionCodes.Dig, pos);
        }

        public XILInstr Barrier()
        {
            return new XILInstr(InstructionCodes.Barrier);
        }

        public XILInstr Mod2()
        {
            return new XILInstr(InstructionCodes.Mod2);
        }

        public XILInstr LdelemFixA(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.LdelemFixA, far);
        }

        public XILInstr LdelemFixAFixI(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.LdelemFixAFixI, far);
        }

        public XILInstr StelemFixA(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.StelemFixA, far);
        }

        public XILInstr StelemFixAFixI(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.StelemFixAFixI, far);
        }

        public XILInstr LdMemBase(MemoryMappedStorage mms)
        {
            return new XILInstr(InstructionCodes.LdMemBase, mms);
        }

        public XILInstr RdMem(MemoryRegion region)
        {
            return new XILInstr(InstructionCodes.RdMem, region);
        }

        public XILInstr RdMem(ITransactionSite site)
        {
            return new XILInstr(InstructionCodes.RdMem, site);
        }

        public XILInstr WrMem(MemoryRegion region)
        {
            return new XILInstr(InstructionCodes.WrMem, region);
        }

        public XILInstr WrMem(ITransactionSite site)
        {
            return new XILInstr(InstructionCodes.WrMem, site);
        }

        public XILInstr Sign()
        {
            return new XILInstr(InstructionCodes.Sign);
        }
    }
}

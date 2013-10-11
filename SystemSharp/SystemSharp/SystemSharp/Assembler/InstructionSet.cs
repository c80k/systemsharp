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
    /// <summary>
    /// All XIL opcodes
    /// </summary>
    public static class InstructionCodes
    {
        /// <summary>
        /// Absolute value
        /// </summary>
        public const string Abs = "Abs";

        /// <summary>
        /// Addition
        /// </summary>
        public const string Add = "Add";

        /// <summary>
        /// Logical and bitwise "and"
        /// </summary>
        public const string And = "And";

        /// <summary>
        /// Branch if operand is "true"
        /// </summary>
        public const string BranchIfTrue = "BrTrue";

        /// <summary>
        /// Branch if operand is "false"
        /// </summary>
        public const string BranchIfFalse = "BrFalse";

        /// <summary>
        /// Ceiling value
        /// </summary>
        public const string Ceil = "Ceil";

        /// <summary>
        /// Compare operands
        /// </summary>
        public const string Cmp = "Cmp";

        /// <summary>
        /// Concatenation
        /// </summary>
        public const string Concat = "Concat";

        /// <summary>
        /// Type conversion
        /// </summary>
        public const string Convert = "Convert";

        /// <summary>
        /// Cosine
        /// </summary>
        public const string Cos = "Cos";

        /// <summary>
        /// Cosine on scaled radians, i.e. ScCos(x) = Cos(2*PI*x)
        /// </summary>
        public const string ScCos = "ScCos";

        /// <summary>
        /// Sine
        /// </summary>
        public const string Sin = "Sin";

        /// <summary>
        /// Cosine on scaled radians, i.e. ScSin(x) = Sin(2*PI*x)
        /// </summary>
        public const string ScSin = "ScSin";

        /// <summary>
        /// Parallel sine and cosine
        /// </summary>
        public const string SinCos = "SinCos";

        /// <summary>
        /// Parallel sine and cosine, i.e. ScSinCos(x) = SinCos(2*PI*x)
        /// </summary>
        public const string ScSinCos = "ScSinCos";

        /// <summary>
        /// Division
        /// </summary>
        public const string Div = "Div";

        /// <summary>
        /// Division with separate quotient and fraction results
        /// </summary>
        public const string DivQF = "DivQF";

        /// <summary>
        /// Floor function
        /// </summary>
        public const string Floor = "Floor";

        /// <summary>
        /// Unconditional branch
        /// </summary>
        public const string Goto = "Goto";

        /// <summary>
        /// Comparison for equality
        /// </summary>
        public const string IsEq = "IsEq";

        /// <summary>
        /// Compare if "greater"
        /// </summary>
        public const string IsGt = "IsGt";

        /// <summary>
        /// Compare if "greater or equal"
        /// </summary>
        public const string IsGte = "IsGte";

        /// <summary>
        /// Compare if "less than"
        /// </summary>
        public const string IsLt = "IsLt";

        /// <summary>
        /// Compare if "less than or equal"
        /// </summary>
        public const string IsLte = "IsLte";

        /// <summary>
        /// Compare for inequality
        /// </summary>
        public const string IsNEq = "IsNEq";

        /// <summary>
        /// Load constant value
        /// </summary>
        public const string LdConst = "LdConst";

        /// <summary>
        /// Load zero
        /// </summary>
        public const string Ld0 = "Ld0";

        /// <summary>
        /// Shift left
        /// </summary>
        public const string LShift = "LShift";

        /// <summary>
        /// Take maximum of operands
        /// </summary>
        public const string Max = "Max";

        /// <summary>
        /// Take minimum of operands
        /// </summary>
        public const string Min = "Min";

        /// <summary>
        /// Multiplication
        /// </summary>
        public const string Mul = "Mul";

        /// <summary>
        /// Logical or bitwise "or"
        /// </summary>
        public const string Or = "Or";

        /// <summary>
        /// Read from memory at fixed address
        /// </summary>
        public const string RdMemFix = "RdMemFix";

        /// <summary>
        /// Read from memory
        /// </summary>
        public const string RdMem = "RdMem";

        /// <summary>
        /// Remainder
        /// </summary>
        public const string Rem = "Rem";

        /// <summary>
        /// Remainder where divisor is fixed power of 2
        /// </summary>
        public const string Rempow2 = "Rempow2";

        /// <summary>
        /// Shift right arithmetical if operand is signed, logical otherwise
        /// </summary>
        public const string RShift = "RShift";

        /// <summary>
        /// Read from port
        /// </summary>
        public const string RdPort = "RdPort";

        /// <summary>
        /// Select one of two operands based on condition operand
        /// </summary>
        public const string Select = "Select";

        /// <summary>
        /// Take sub-vector from logic vector
        /// </summary>
        public const string Slice = "Slice";

        /// <summary>
        /// Take sub-vector from logic vector with fixed offset
        /// </summary>
        public const string SliceFixI = "SliceFixI";

        /// <summary>
        /// Square-root
        /// </summary>
        public const string Sqrt = "Sqrt";

        /// <summary>
        /// Subtraction
        /// </summary>
        public const string Sub = "Sub";

        /// <summary>
        /// Negation
        /// </summary>
        public const string Neg = "Neg";

        /// <summary>
        /// Empty instruction
        /// </summary>
        public const string Nop = "Nop";

        /// <summary>
        /// Logical or bitwise "not"
        /// </summary>
        public const string Not = "Not";

        /// <summary>
        /// Write to memory at fixed address
        /// </summary>
        public const string WrMemFix = "WrMemFix";

        /// <summary>
        /// Write to memory
        /// </summary>
        public const string WrMem = "WrMem";

        /// <summary>
        /// Write to port
        /// </summary>
        public const string WrPort = "WrPort";

        /// <summary>
        /// Logical or bitwise "exclusive or"
        /// </summary>
        public const string Xor = "Xor";

        /// <summary>
        /// Extend sign
        /// </summary>
        public const string ExtendSign = "Xts";

        /// <summary>
        /// Load variable
        /// </summary>
        public const string LoadVar = "Ldv";

        /// <summary>
        /// Store to variable
        /// </summary>
        public const string StoreVar = "Stv";

        /// <summary>
        /// Return
        /// </summary>
        public const string Return = "Ret";

        /// <summary>
        /// Unique exit node for control-flow graph construction (not a real instruction)
        /// </summary>
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

        /// <summary>
        /// Remove topmost value from stack
        /// </summary>
        public const string Pop = "Pop";

        /// <summary>
        /// Duplicate topmost value on stack
        /// </summary>
        public const string Dup = "Dup";

        /// <summary>
        /// Swap two topmost values on stack
        /// </summary>
        public const string Swap = "Swap";

        /// <summary>
        /// Bring arbitrary value to stack top
        /// </summary>
        public const string Dig = "Dig";

        /// <summary>
        /// Barrier - does not perform any operation, but is used to indicate dependencies
        /// </summary>
        public const string Barrier = "Barrier";

        /// <summary>
        /// Returns all XIL opcodes
        /// </summary>
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

    /// <summary>
    /// Generic instruction set
    /// </summary>
    /// <remarks>
    /// Although XIL is intended to be extensible, opcodes are all well-defined. Therefore, this interface is actually a piece of
    /// overengineering and might be removed in future releases.
    /// </remarks>
    /// <typeparam name="T">container datatype for instructions</typeparam>
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

    /// <summary>
    /// Provides convenience method to create XIL instructions
    /// </summary>
    public class DefaultInstructionSet : IInstructionSet<XILInstr>
    {
        private DefaultInstructionSet()
        {
        }

        /// <summary>
        /// The one and only instance
        /// </summary>
        public static readonly DefaultInstructionSet Instance = new DefaultInstructionSet();

        /// <summary>
        /// Fast access to "no dependencies"
        /// </summary>
        public static readonly InstructionDependency[] Empty = new InstructionDependency[0];

        /// <summary>
        /// Creates an unconditional branch
        /// </summary>
        /// <param name="target">branch target</param>
        public XILInstr Goto(BranchLabel target)
        {
            return new XILInstr(InstructionCodes.Goto, target);
        }

        /// <summary>
        /// Creates a conditional branch, will be taken if operand is "true"
        /// </summary>
        /// <param name="target">branch target</param>
        public XILInstr BranchIfTrue(BranchLabel target)
        {
            return new XILInstr(InstructionCodes.BranchIfTrue, target);
        }

        /// <summary>
        /// Creates a conditional branch, will be taken if operand is "false"
        /// </summary>
        /// <param name="target">branch target</param>
        public XILInstr BranchIfFalse(BranchLabel target)
        {
            return new XILInstr(InstructionCodes.BranchIfFalse, target);
        }

        /// <summary>
        /// Creates a memory read instruction with fixed address
        /// </summary>
        /// <param name="region">memory region to read from</param>
        /// <param name="addr">address to read from</param>
        public XILInstr RdMemFix(MemoryRegion region, Unsigned addr)
        {
            return new XILInstr(InstructionCodes.RdMemFix, Tuple.Create(region, addr));
        }

        /// <summary>
        /// Creates a write memory instruction with fixed address
        /// </summary>
        /// <param name="region">memory region to write to</param>
        /// <param name="addr">address to write to</param>
        public XILInstr WrMemFix(MemoryRegion region, Unsigned addr)
        {
            return new XILInstr(InstructionCodes.WrMemFix, Tuple.Create(region, addr));
        }

        /// <summary>
        /// Creates an instruction which loads a constant with given value
        /// </summary>
        /// <param name="value">constant value to load</param>
        public XILInstr LdConst(object value)
        {
            return new XILInstr(InstructionCodes.LdConst, value);
        }

        /// <summary>
        /// Creates an instruction which loads the value 0
        /// </summary>
        public XILInstr Ld0()
        {
            return new XILInstr(InstructionCodes.Ld0);
        }

        /// <summary>
        /// Creates an instruction which computes the absolute value of its operand
        /// </summary>
        public XILInstr Abs()
        {
            return new XILInstr(InstructionCodes.Abs);
        }

        /// <summary>
        /// Creates an empty instruction
        /// </summary>
        public XILInstr Nop()
        {
            return new XILInstr(InstructionCodes.Nop);
        }

        /// <summary>
        /// Creates an empty instruction with given latency ("do nothing for N clock steps")
        /// </summary>
        /// <param name="latency">latency</param>
        public XILInstr Nop(int latency)
        {
            return new XILInstr(InstructionCodes.Nop, latency);
        }

        /// <summary>
        /// Creates an instruction which computes the logical or bitwise complement of its operand (depending on operand type)
        /// </summary>
        public XILInstr Not()
        {
            return new XILInstr(InstructionCodes.Not);
        }

        /// <summary>
        /// Creates an instruction which negates its operand
        /// </summary>
        public XILInstr Neg()
        {
            return new XILInstr(InstructionCodes.Neg);
        }

        /// <summary>
        /// Creates an instruction which extends the sign of its operand
        /// </summary>
        public XILInstr ExtendSign()
        {
            return new XILInstr(InstructionCodes.ExtendSign);
        }

        /// <summary>
        /// Creates an instruction which adds its operands
        /// </summary>
        public XILInstr Add()
        {
            return new XILInstr(InstructionCodes.Add);
        }

        /// <summary>
        /// Creates an instruction which subtracts its operands
        /// </summary>
        public XILInstr Sub()
        {
            return new XILInstr(InstructionCodes.Sub);
        }

        /// <summary>
        /// Creates an instruction which multiplicates its operands
        /// </summary>
        public XILInstr Mul()
        {
            return new XILInstr(InstructionCodes.Mul);
        }

        /// <summary>
        /// Creates an instruction which performs division on its operands
        /// </summary>
        public XILInstr Div()
        {
            return new XILInstr(InstructionCodes.Div);
        }

        /// <summary>
        /// Creates an instruction which performs division on its operands and delivers two results, namely quotient and fractional part
        /// </summary>
        public XILInstr DivQF()
        {
            return new XILInstr(InstructionCodes.DivQF);
        }

        /// <summary>
        /// Creates an operation which computes logical or bitwise conjunction of its operands (depending on operand type)
        /// </summary>
        public XILInstr And()
        {
            return new XILInstr(InstructionCodes.And);
        }

        /// <summary>
        /// Creates an operation which computes logical or bitwise disjunction of its operands (depending on operand type)
        /// </summary>
        public XILInstr Or()
        {
            return new XILInstr(InstructionCodes.Or);
        }

        /// <summary>
        /// Creates an operation which computes logical or bitwise "exclusive or" of its operands (depending on operand type)
        /// </summary>
        public XILInstr Xor()
        {
            return new XILInstr(InstructionCodes.Xor);
        }

        /// <summary>
        /// Creates an operation which concatenates its operands given as logic vectors
        /// </summary>
        public XILInstr Concat()
        {
            return new XILInstr(InstructionCodes.Concat);
        }

        /// <summary>
        /// Creates a comparison operation for "less than"
        /// </summary>
        public XILInstr IsLt()
        {
            return new XILInstr(InstructionCodes.IsLt);
        }

        /// <summary>
        /// Creates a comparison operation for "less than or equal"
        /// </summary>
        public XILInstr IsLte()
        {
            return new XILInstr(InstructionCodes.IsLte);
        }

        /// <summary>
        /// Creates a comparison operation for equality
        /// </summary>
        public XILInstr IsEq()
        {
            return new XILInstr(InstructionCodes.IsEq);
        }

        /// <summary>
        /// Creates a comparison operation for inequality
        /// </summary>
        public XILInstr IsNEq()
        {
            return new XILInstr(InstructionCodes.IsNEq);
        }

        /// <summary>
        /// Creates a comparison operation for "greater than or equal"
        /// </summary>
        public XILInstr IsGte()
        {
            return new XILInstr(InstructionCodes.IsGte);
        }

        /// <summary>
        /// Creates a comparison operation for "greater than"
        /// </summary>
        public XILInstr IsGt()
        {
            return new XILInstr(InstructionCodes.IsGt);
        }

        /// <summary>
        /// Creates a "shift left" instruction
        /// </summary>
        public XILInstr LShift()
        {
            return new XILInstr(InstructionCodes.LShift);
        }

        /// <summary>
        /// Creates a "shift right" instruction. Shift is arithmetic if operand is signed, logic otherwise.
        /// </summary>
        public XILInstr RShift()
        {
            return new XILInstr(InstructionCodes.RShift);
        }

        /// <summary>
        /// Creates an instruction to compute the modulus of its operands
        /// </summary>
        public XILInstr Rem()
        {
            return new XILInstr(InstructionCodes.Rem);
        }

        /// <summary>
        /// Creates an instruction to compute the modulus of its operand and 2^n (i.e. a fixed power of two)
        /// </summary>
        /// <param name="n">exponent</param>
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
        public XILInstr Select()
        {
            return new XILInstr(InstructionCodes.Select);
        }

        /// <summary>
        /// Creates an instruction which slices a sub-vector out of a logic vector
        /// </summary>
        public XILInstr Slice()
        {
            return new XILInstr(InstructionCodes.Slice);
        }

        /// <summary>
        /// Creates an instruction which slices a sub-vector out of a logic vector for a given fixed slice range
        /// </summary>
        /// <param name="range">slice range</param>
        public XILInstr SliceFixI(Range range)
        {
            return new XILInstr(InstructionCodes.SliceFixI, range);
        }

        /// <summary>
        /// Creates a type conversion instruction. The kind of conversion is almost always uniquely determined by operand and result types.
        /// The small rest of cases is disambiguated by the <paramref name="reinterpret"/> parameter.
        /// </summary>
        /// <remarks>
        /// To understand the difference between reinterpreting and non-reinterpreting conversions, consider the following example.
        /// Let's say we have an Unsigned[27] value we want to convert to a UFix[33,10] value (i.e. 33 bits total width, 10 bits
        /// fractional width, makes 23 bits integer width). There are two possible intentions of this conversions:
        /// <list type="table">
        /// <listheader><term><paramref name="reinterpret"/></term><description>meaning</description></listheader>
        /// <item>
        /// <term>true</term>
        /// <description>The unsigned bits should be reinterpreted as fixed point number, i.e. the lower 10 bits of the original value
        /// constitute the fractional bits of the converted number. This conversion will actually scale the number by 2^(-10).</description>
        /// </item>
        /// <item>
        /// <term>false</term>
        /// <description>The unsigned value should by reformatted as fixed point number. The fractional bits of the converted
        /// value will be all zeroes, since the original value is integral. Since the new integer width is only 23 bits,
        /// some bits might get truncated during conversion, causing arithmetic overflow.</description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="reinterpret">whether the conversion is intended as reinterpretation of raw data</param>
        public XILInstr Convert(bool reinterpret = false)
        {
            return new XILInstr(InstructionCodes.Convert, reinterpret);
        }

        /// <summary>
        /// Creates an instruction which computes the cosine of its operand, assuming unit radians
        /// </summary>
        public XILInstr Cos()
        {
            return new XILInstr(InstructionCodes.Cos);
        }

        /// <summary>
        /// Creates an instruction which computes the sine of its operand, given in radians
        /// </summary>
        public XILInstr Sin()
        {
            return new XILInstr(InstructionCodes.Sin);
        }

        /// <summary>
        /// Creates an instruction which computes the cosine of its operand, given in scaled radians (i.e. ScCos(x) = Cos(2*PI*x))
        /// </summary>
        public XILInstr ScCos()
        {
            return new XILInstr(InstructionCodes.ScCos);
        }

        /// <summary>
        /// Creates an instruction which computes the sine of its operand, given in scaled radians (i.e. ScCos(x) = Cos(2*PI*x))
        /// </summary>
        public XILInstr ScSin()
        {
            return new XILInstr(InstructionCodes.ScSin);
        }

        /// <summary>
        /// Creates an instruction which simultaeneously computes sine and cosine of its operand, given in radians
        /// </summary>
        public XILInstr SinCos()
        {
            return new XILInstr(InstructionCodes.SinCos);
        }

        /// <summary>
        /// Creates an instruction which simultaeneously computes sine and cosine of its operand, given in scaled radians 
        /// (i.e. ScSinCos(x) = SinCos(2*PI*x))
        /// </summary>
        public XILInstr ScSinCos()
        {
            return new XILInstr(InstructionCodes.ScSinCos);
        }

        /// <summary>
        /// Creates an instruction which computes the square root of its operand
        /// </summary>
        public XILInstr Sqrt()
        {
            return new XILInstr(InstructionCodes.Sqrt);
        }

        /// <summary>
        /// Creates an instruction which loads a variable
        /// </summary>
        public XILInstr LoadVar(IStorableLiteral v)
        {
            return new XILInstr(InstructionCodes.LoadVar, v);
        }

        /// <summary>
        /// Creates an instruction which stores its operand to a variable
        /// </summary>
        public XILInstr StoreVar(IStorableLiteral v)
        {
            return new XILInstr(InstructionCodes.StoreVar, v);
        }

        /// <summary>
        /// Creates an instruction which reads from a port
        /// </summary>
        public XILInstr ReadPort(ISignalOrPortDescriptor sd)
        {
            return new XILInstr(InstructionCodes.RdPort, sd);
        }

        /// <summary>
        /// Creates an instruction which stores its operand to a port
        /// </summary>
        public XILInstr WritePort(ISignalOrPortDescriptor sd)
        {
            return new XILInstr(InstructionCodes.WrPort, sd);
        }

        /// <summary>
        /// Creates a return instruction
        /// </summary>
        public XILInstr Return()
        {
            return new XILInstr(InstructionCodes.Return);
        }

        /// <summary>
        /// Creates a exit node (pseudo-instruction)
        /// </summary>
        public XILInstr ExitMarshal()
        {
            return new XILInstr(InstructionCodes.ExitMarshal);
        }

        /// <summary>
        /// Creates an instruction which removes the topmost stack element (XIL-S only)
        /// </summary>
        public XILInstr Pop()
        {
            return new XILInstr(InstructionCodes.Pop);
        }

        /// <summary>
        /// Creates an instruction which duplicates the topmost stack element (XIL-S only)
        /// </summary>
        public XILInstr Dup()
        {
            return new XILInstr(InstructionCodes.Dup);
        }

        /// <summary>
        /// Creates an instruction which swaps the two topmost stack elements (XIL-S only)
        /// </summary>
        public XILInstr Swap()
        {
            return new XILInstr(InstructionCodes.Swap);
        }

        /// <summary>
        /// Creates an instruction which brings an arbitrary stack element to the top (XIL-S only)
        /// </summary>
        public XILInstr Dig(int pos)
        {
            return new XILInstr(InstructionCodes.Dig, pos);
        }

        /// <summary>
        /// Creates a barrier (pseudo-instruction)
        /// </summary>
        public XILInstr Barrier()
        {
            return new XILInstr(InstructionCodes.Barrier);
        }

        /// <summary>
        /// Creates an instruction which computes the modulus of its operand and 2
        /// </summary>
        public XILInstr Mod2()
        {
            return new XILInstr(InstructionCodes.Mod2);
        }

        /// <summary>
        /// Creates an instruction which loads an element from a fixed array
        /// </summary>
        /// <param name="far">array specification</param>
        public XILInstr LdelemFixA(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.LdelemFixA, far);
        }

        /// <summary>
        /// Creates an instruction which loads an element from a fixed array at a fixed index
        /// </summary>
        /// <param name="far">array and index specification</param>
        public XILInstr LdelemFixAFixI(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.LdelemFixAFixI, far);
        }

        /// <summary>
        /// Creates an instruction which stores an element to a fixed array
        /// </summary>
        /// <param name="far">array specification</param>
        public XILInstr StelemFixA(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.StelemFixA, far);
        }

        /// <summary>
        /// Creates an instruction which stores an element to a fixed array at a fixed index
        /// </summary>
        /// <param name="far">array and index specification</param>
        public XILInstr StelemFixAFixI(FixedArrayRef far)
        {
            return new XILInstr(InstructionCodes.StelemFixAFixI, far);
        }

        /// <summary>
        /// Creates an instruction which loads the base address of a memory-mapped storage element
        /// </summary>
        /// <param name="mms">memory-mapped storage element</param>
        public XILInstr LdMemBase(MemoryMappedStorage mms)
        {
            return new XILInstr(InstructionCodes.LdMemBase, mms);
        }

        /// <summary>
        /// Creates an instruction which reads from memory
        /// </summary>
        /// <param name="region">memory region to read from</param>
        public XILInstr RdMem(MemoryRegion region)
        {
            return new XILInstr(InstructionCodes.RdMem, region);
        }

        /// <summary>
        /// Creates an instruction which reads from memory
        /// </summary>
        /// <param name="site">memory transaction site</param>
        public XILInstr RdMem(ITransactionSite site)
        {
            return new XILInstr(InstructionCodes.RdMem, site);
        }

        /// <summary>
        /// Creates an instruction which writes to memory
        /// </summary>
        /// <param name="region">memory region to write to</param>
        public XILInstr WrMem(MemoryRegion region)
        {
            return new XILInstr(InstructionCodes.WrMem, region);
        }

        /// <summary>
        /// Creates an instruction which writes to memory
        /// </summary>
        /// <param name="site">memory transaction site</param>
        public XILInstr WrMem(ITransactionSite site)
        {
            return new XILInstr(InstructionCodes.WrMem, site);
        }

        /// <summary>
        /// Creates an instruction which determines the sign of its operand. Produces 1 if operand &gt; 0, 0 if operand = 0 and -1 if operand &lt; 0.
        /// </summary>
        public XILInstr Sign()
        {
            return new XILInstr(InstructionCodes.Sign);
        }
    }
}

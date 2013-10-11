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
using System.Linq;
using System.Text;
using SystemSharp.Common;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    public static class XILResultTypes
    {
        /// <summary>
        /// Returns a sequence of adminissible result types, given instruction operand types.
        /// </summary>
        /// <param name="instr">XIL instruction</param>
        /// <param name="operandTypes">operand types</param>
        /// <returns>admissible result types</returns>
        public static IEnumerable<TypeDescriptor> GetDefaultResultTypes(this XILInstr instr, TypeDescriptor[] operandTypes)
        {
            switch (instr.Name)
            {
                case InstructionCodes.Abs:
                    if (operandTypes[0].CILType.Equals(typeof(float)) ||
                        operandTypes[0].CILType.Equals(typeof(double)) ||
                        operandTypes[0].CILType.Equals(typeof(int)) ||
                        operandTypes[0].CILType.Equals(typeof(long)) ||
                        operandTypes[0].CILType.Equals(typeof(sbyte)) ||
                        operandTypes[0].CILType.Equals(typeof(short)))
                    {
                        yield return operandTypes[0];
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(double)))
                    {
                        yield return typeof(double);
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(SFix)))
                    {
                        var fmt = SFix.GetFormat(operandTypes[0]);
                        var ssample = SFix.FromDouble(0.0, fmt.IntWidth + 1, fmt.FracWidth);
                        yield return TypeDescriptor.GetTypeOf(ssample);
                        var usample = UFix.FromDouble(0.0, fmt.IntWidth, fmt.FracWidth);
                        yield return TypeDescriptor.GetTypeOf(usample);
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(UFix)))
                    {
                        yield return operandTypes[0];
                    }
                    else
                    {
                        throw new NotSupportedException("Operand type not supported");
                    }
                    break;

                case InstructionCodes.Add:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance();
                        object r = o1 + o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.And:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance();
                        object r = o1 & o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.Barrier:
                case InstructionCodes.BranchIfFalse:
                case InstructionCodes.BranchIfTrue:
                    yield break;

                case InstructionCodes.Ceil:
                case InstructionCodes.Floor:
                case InstructionCodes.SinCos:
                    if (operandTypes[0].CILType.Equals(typeof(float)) ||
                        operandTypes[0].CILType.Equals(typeof(double)))
                    {
                        yield return operandTypes[0];
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;

                case InstructionCodes.Cos:
                case InstructionCodes.Sin:
                    if (operandTypes[0].CILType.Equals(typeof(float)) ||
                        operandTypes[0].CILType.Equals(typeof(double)))
                    {
                        yield return operandTypes[0];
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(UFix)) ||
                        operandTypes[0].CILType.Equals(typeof(SFix)))
                    {
                        var fmt = operandTypes[0].GetFixFormat();
                        // computation works for at most 26 fractional bits
                        double xinc = Math.Pow(2.0, Math.Max(-26, -fmt.FracWidth));
                        double yinc = 1.0 - Math.Cos(xinc);
                        int fw = -MathExt.FloorLog2(yinc);
                        // Xilinx Cordic doesn't like more than 48 result bits
                        if (fw > 48)
                            fw = 48;
                        while (fw >= 0)
                        {
                            yield return SFix.MakeType(2, fw);
                            --fw;
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;

                case InstructionCodes.ScSin:
                case InstructionCodes.ScCos:
                case InstructionCodes.ScSinCos:
                    if (operandTypes[0].CILType.Equals(typeof(float)) ||
                        operandTypes[0].CILType.Equals(typeof(double)))
                    {
                        yield return operandTypes[0];
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(UFix)) ||
                        operandTypes[0].CILType.Equals(typeof(SFix)))
                    {
                        var fmt = operandTypes[0].GetFixFormat();
                        // computation works for at most 26 fractional bits
                        double xinc = Math.Pow(2.0, Math.Max(-26, -fmt.FracWidth));
                        double yinc = 1.0 - Math.Cos(xinc);
                        int fw = -MathExt.FloorLog2(yinc);
                        // Xilinx Cordic doesn't like more than 48 result bits
                        if (fw > 48)
                            fw = 48;
                        while (fw >= 0)
                        {
                            yield return SFix.MakeType(2, fw);
                            --fw;
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                    break;

                case InstructionCodes.Sqrt:
                    if (operandTypes[0].CILType.Equals(typeof(float)) ||
                        operandTypes[0].CILType.Equals(typeof(double)))
                    {
                        yield return operandTypes[0];
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(UFix)))
                    {
                        var fmt = UFix.GetFormat(operandTypes[0]);
                        int iw = (fmt.IntWidth + 1) / 2;
                        yield return UFix.MakeType(iw, fmt.TotalWidth - iw);
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(SFix)))
                    {
                        var fmt = SFix.GetFormat(operandTypes[0]);
                        int iw = fmt.IntWidth / 2;
                        yield return UFix.MakeType(iw, fmt.TotalWidth - iw - 1);
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(Unsigned)))
                    {
                        var fmt = UFix.GetFormat(operandTypes[0]);
                        int iw = (fmt.IntWidth + 1) / 2;
                        yield return Unsigned.MakeType(iw);
                    }
                    else if (operandTypes[0].CILType.Equals(typeof(Signed)))
                    {
                        var fmt = SFix.GetFormat(operandTypes[0]);
                        int iw = fmt.IntWidth / 2;
                        yield return Unsigned.MakeType(iw);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    break;

                case InstructionCodes.Cmp:
                    throw new NotImplementedException();

                case InstructionCodes.Concat:
                    {
                        var v1 = (StdLogicVector)operandTypes[0].GetSampleInstance();
                        var v2 = (StdLogicVector)operandTypes[1].GetSampleInstance();
                        var c = v1.Concat(v2);
                        yield return TypeDescriptor.GetTypeOf(c);
                    }
                    break;

                case InstructionCodes.Convert:
                    throw new NotImplementedException();

                case InstructionCodes.Dig:
                case InstructionCodes.Dup:
                case InstructionCodes.Swap:
                    throw new NotSupportedException();

                case InstructionCodes.Div:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance(ETypeCreationOptions.NonZero);
                        object r = o1 / o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.ExitMarshal:
                case InstructionCodes.Goto:
                case InstructionCodes.Nop:
                case InstructionCodes.Pop:
                case InstructionCodes.Return:
                case InstructionCodes.StelemFixA:
                case InstructionCodes.StelemFixAFixI:
                case InstructionCodes.StoreVar:
                case InstructionCodes.WrMem:
                case InstructionCodes.WrMemFix:
                case InstructionCodes.WrPort:
                    yield break;

                case InstructionCodes.Mod2:
                    {
                        var fmt = operandTypes[0].GetFixFormat();
                        if (fmt == null)
                            throw new NotSupportedException("mod2 is only supported for fixed-point types");

                        for (int iw = 2; iw <= fmt.IntWidth; iw++)
                        {
                            yield return new FixFormat(fmt.IsSigned, iw, fmt.FracWidth).ToType();
                        }
                    }
                    break;

                case InstructionCodes.DivQF:
                case InstructionCodes.ExtendSign:
                case InstructionCodes.Ld0:
                case InstructionCodes.LdelemFixA:
                case InstructionCodes.LdelemFixAFixI:
                case InstructionCodes.LdMemBase:
                case InstructionCodes.LShift:
                case InstructionCodes.RdMem:
                case InstructionCodes.RdMemFix:
                case InstructionCodes.RShift:
                case InstructionCodes.Sign:
                case InstructionCodes.SliceFixI:
                    throw new NotImplementedException();

                case InstructionCodes.IsEq:
                case InstructionCodes.IsGt:
                case InstructionCodes.IsGte:
                case InstructionCodes.IsLt:
                case InstructionCodes.IsLte:
                case InstructionCodes.IsNEq:
                    yield return typeof(bool);
                    break;

                case InstructionCodes.LdConst:
                    yield return TypeDescriptor.GetTypeOf(instr.Operand);
                    break;

                case InstructionCodes.LoadVar:
                    {
                        var lit = (IStorableLiteral)instr.Operand;
                        yield return lit.Type;
                    }
                    break;

                case InstructionCodes.Max:
                case InstructionCodes.Min:
                    yield return operandTypes[0];
                    break;

                case InstructionCodes.Mul:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance();
                        object r = o1 * o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.Neg:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        object r = -o1 ;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.Not:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        object r = !o1;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.Or:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance();
                        object r = o1 | o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.RdPort:
                    {
                        var port = (ISignalOrPortDescriptor)instr.Operand;
                        yield return port.ElementType;
                    }
                    break;

                case InstructionCodes.Rem:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance(ETypeCreationOptions.NonZero);
                        object r = o1 % o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.Rempow2:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        int n = (int)instr.Operand;
                        object r = MathExt.Rempow2((dynamic)o1, n);
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.Select:
                    yield return operandTypes[1];
                    break;

                case InstructionCodes.Slice:
                    yield return typeof(StdLogicVector);
                    break;

                case InstructionCodes.Sub:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance();
                        object r = o1 - o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                case InstructionCodes.Xor:
                    {
                        dynamic o1 = operandTypes[0].GetSampleInstance();
                        dynamic o2 = operandTypes[1].GetSampleInstance();
                        object r = o1 ^ o2;
                        yield return TypeDescriptor.GetTypeOf(r);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}

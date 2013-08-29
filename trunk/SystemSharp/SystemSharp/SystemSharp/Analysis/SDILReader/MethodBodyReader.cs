/**
 * This file is part of System#.
 * 
 * It was taken and adapted from Sorin Serban's code project article at
 * http://www.codeproject.com/KB/cs/sdilreader.aspx
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
 * CHANGE LOG 
 * ==========
 * 2011-09-13 CK -fixed ReadDouble()/ReadSingle()
 * 2011-10-02 CK -fixed incorrect offset of two-byte instructions
 * 2013-04-12 CK -added some contract checking suggested by CodeContracts
 * * */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

namespace SDILReader
{
    public class MethodBodyReader
    {
        public List<SDILReader.ILInstruction> instructions = null;
        protected byte[] il = null;
        private MethodBase mi = null;

        #region il read methods
        private int ReadInt16(byte[] _il, ref int position)
        {
            Contract.Requires<ArgumentException>(position >= 0);

            return ((il[position++] | (il[position++] << 8)));
        }
        private ushort ReadUInt16(byte[] _il, ref int position)
        {
            Contract.Requires<ArgumentException>(position >= 0);

            return (ushort)((il[position++] | (il[position++] << 8)));
        }
        private int ReadInt32(byte[] _il, ref int position)
        {
            Contract.Requires<ArgumentException>(position >= 0);

            return (((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
        }
        private ulong ReadInt64(byte[] _il, ref int position)
        {
            return (ulong)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
        }
        private double ReadDouble(byte[] _il, ref int position)
        {
            Contract.Requires<ArgumentException>(position >= 0);
            Contract.Requires<ArgumentException>(position + 8 <= il.Length);

            MemoryStream ms = new MemoryStream(il, position, 8);
            BinaryReader br = new BinaryReader(ms);
            double result = br.ReadDouble();
            position += 8;
            return result;
        }
        private sbyte ReadSByte(byte[] _il, ref int position)
        {
            Contract.Requires<ArgumentException>(position >= 0);

            return (sbyte)il[position++];
        }
        private byte ReadByte(byte[] _il, ref int position)
        {
            Contract.Requires<ArgumentException>(position >= 0);

            return (byte)il[position++];
        }
        private Single ReadSingle(byte[] _il, ref int position)
        {
            Contract.Requires<ArgumentException>(position >= 0);
            Contract.Requires<ArgumentException>(position + 4 <= il.Length);

            MemoryStream ms = new MemoryStream(il, position, 4);
            BinaryReader br = new BinaryReader(ms);
            float result = br.ReadSingle();
            position += 4;
            return result;
            //return (Single)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
        }
        #endregion

        /// <summary>
        /// Constructs the array of ILInstructions according to the IL byte code.
        /// </summary>
        /// <param name="module"></param>
        private void ConstructInstructions(Module module)
        {
            byte[] il = this.il;
            int position = 0;
            instructions = new List<ILInstruction>();
            int index = 0;
            while (position < il.Length)
            {
                ILInstruction instruction = new ILInstruction()
                {
                    Index = index++
                };

                // get the operation code of the current instruction
                OpCode code = OpCodes.Nop;
                instruction.Offset = position;
                ushort value = il[position++];
                if (value != 0xfe)
                {
                    code = Globals.singleByteOpCodes[(int)value];
                }
                else
                {
                    value = il[position++];
                    code = Globals.multiByteOpCodes[(int)value];
                    value = (ushort)(value | 0xfe00);
                }
                instruction.Code = code;
                int metadataToken = 0;
                // get the operand of the current operation
                Type[] genArgs = mi is MethodInfo ? mi.GetGenericArguments() : new Type[0];
                switch (code.OperandType)
                {
                    case OperandType.InlineBrTarget:
                        metadataToken = ReadInt32(il, ref position);
                        metadataToken += position;
                        instruction.Operand = metadataToken;
                        break;
                    case OperandType.InlineField:
                        metadataToken = ReadInt32(il, ref position);
                        instruction.Operand = module.ResolveField(metadataToken,
                            mi.DeclaringType.GetGenericArguments(),
                            genArgs);
                        break;
                    case OperandType.InlineMethod:
                        metadataToken = ReadInt32(il, ref position);
                        try
                        {
                            instruction.Operand = module.ResolveMethod(metadataToken,
                                mi.DeclaringType.GetGenericArguments(),
                                genArgs);
                        }
                        catch
                        {
                            instruction.Operand = module.ResolveMember(metadataToken,
                                mi.DeclaringType.GetGenericArguments(),
                                genArgs);
                        }
                        break;
                    case OperandType.InlineSig:
                        metadataToken = ReadInt32(il, ref position);
                        instruction.Operand = module.ResolveSignature(metadataToken);
                        break;
                    case OperandType.InlineTok:
                        metadataToken = ReadInt32(il, ref position);
                        /*try
                        {
                            instruction.Operand = module.ResolveType(metadataToken);
                        }
                        catch (ArgumentException)
                        {

                        }*/
                        // CK: Occasionally throws exception. Metadata tokens are not processed anyhow
                        // SSS : see what to do here
                        break;
                    case OperandType.InlineType:
                        metadataToken = ReadInt32(il, ref position);
                        // now we call the ResolveType always using the generic attributes type in order
                        // to support decompilation of generic methods and classes
                        
                        // thanks to the guys from code project who commented on this missing feature

                        instruction.Operand = module.ResolveType(metadataToken, mi.DeclaringType.GetGenericArguments(), genArgs);
                        break;
                    case OperandType.InlineI:
                        {
                            instruction.Operand = ReadInt32(il, ref position);
                            break;
                        }
                    case OperandType.InlineI8:
                        {
                            instruction.Operand = ReadInt64(il, ref position);
                            break;
                        }
                    case OperandType.InlineNone:
                        {
                            instruction.Operand = null;
                            break;
                        }
                    case OperandType.InlineR:
                        {
                            instruction.Operand = ReadDouble(il, ref position);
                            break;
                        }
                    case OperandType.InlineString:
                        {
                            metadataToken = ReadInt32(il, ref position);
                            instruction.Operand = module.ResolveString(metadataToken);
                            break;
                        }
                    case OperandType.InlineSwitch:
                        {
                            int count = ReadInt32(il, ref position);
                            int[] casesAddresses = new int[count];
                            for (int i = 0; i < count; i++)
                            {
                                casesAddresses[i] = ReadInt32(il, ref position);
                            }
                            int[] cases = new int[count];
                            for (int i = 0; i < count; i++)
                            {
                                cases[i] = position + casesAddresses[i];
                            }
                            instruction.Operand = cases;
                            break;
                        }
                    case OperandType.InlineVar:
                        {
                            instruction.Operand = ReadUInt16(il, ref position);
                            break;
                        }
                    case OperandType.ShortInlineBrTarget:
                        {
                            instruction.Operand = ReadSByte(il, ref position) + position;
                            break;
                        }
                    case OperandType.ShortInlineI:
                        {
                            instruction.Operand = ReadSByte(il, ref position);
                            break;
                        }
                    case OperandType.ShortInlineR:
                        {
                            instruction.Operand = ReadSingle(il, ref position);
                            break;
                        }
                    case OperandType.ShortInlineVar:
                        {
                            instruction.Operand = ReadByte(il, ref position);
                            break;
                        }
                    default:
                        {
                            throw new Exception("Unknown operand type.");
                        }
                }
                instructions.Add(instruction);
            }
        }

        public object GetRefferencedOperand(Module module, int metadataToken)
        {
            AssemblyName[] assemblyNames = module.Assembly.GetReferencedAssemblies();
            for (int i=0; i<assemblyNames.Length; i++)
            {
                Module[] modules = Assembly.Load(assemblyNames[i]).GetModules();
                for (int j=0; j<modules.Length; j++)
                {
                    try
                    {
                        Type t = modules[j].ResolveType(metadataToken);
                        return t;
                    }
                    catch
                    {

                    }

                }
            }
            return null;
        //System.Reflection.Assembly.Load(module.Assembly.GetReferencedAssemblies()[3]).GetModules()[0].ResolveType(metadataToken)

        }
        /// <summary>
        /// Gets the IL code of the method
        /// </summary>
        /// <returns></returns>
        public string GetBodyCode()
        {
            string result = "";
            if (instructions != null)
            {
                for (int i = 0; i < instructions.Count; i++)
                {
                    result += instructions[i].GetCode() + "\n";
                }
            }
            return result;

        }

        /// <summary>
        /// MethodBodyReader constructor
        /// </summary>
        /// <param name="mi">
        /// The System.Reflection defined MethodInfo
        /// </param>
        public MethodBodyReader(MethodBase mi)
        {
            this.mi = mi;
            if (mi.GetMethodBody() != null)
            {
                il = mi.GetMethodBody().GetILAsByteArray();
                ConstructInstructions(mi.Module);
            }
        }
    }
}

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
 * */

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection.Emit;
using SystemSharp.Analysis;

namespace SDILReader
{
    public class ILInstruction: IInstruction
    {
        // Fields
        private OpCode code;
        private object operand;
        private byte[] operandData;
        private int offset;

        // Properties
        public OpCode Code
        {
            get { return code; }
            set { code = value; }
        }

        public object Operand
        {
            get { return operand; }
            set { operand = value; }
        }

        public byte[] OperandData
        {
            get { return operandData; }
            set { operandData = value; }
        }

        public int Offset
        {
            get { return offset; }
            set { offset = value; }
        }

        //koellner: Index property (used to number each ILInstruction
        public int Index { get; set; }

        /// <summary>
        /// Returns a friendly strign representation of this instruction
        /// </summary>
        /// <returns></returns>
        public string GetCode()
        {
            string result = "";
            result += GetExpandedOffset(offset) + " : " + code;
            if (operand != null)
            {
                switch (code.OperandType)
                {
                    case OperandType.InlineField:
                        System.Reflection.FieldInfo fOperand = ((System.Reflection.FieldInfo)operand);
                        result += " " + Globals.ProcessSpecialTypes(fOperand.FieldType.ToString()) + " " +
                            Globals.ProcessSpecialTypes(fOperand.ReflectedType.ToString()) +
                            "::" + fOperand.Name + "";
                        break;
                    case OperandType.InlineMethod:
                        if (operand is System.Reflection.MethodInfo)
                        {
                            System.Reflection.MethodInfo mOperand = (System.Reflection.MethodInfo)operand;
                            result += " ";
                            if (!mOperand.IsStatic) result += "instance ";
                            result += Globals.ProcessSpecialTypes(mOperand.ReturnType.ToString()) +
                                " " + Globals.ProcessSpecialTypes(mOperand.ReflectedType.ToString()) +
                                "::" + mOperand.Name + "()";
                        }
                        else if (operand is System.Reflection.ConstructorInfo)
                        {
                            System.Reflection.ConstructorInfo mOperand = (System.Reflection.ConstructorInfo)operand;
                            result += " ";
                            if (!mOperand.IsStatic) result += "instance ";
                            result += "void " +
                                Globals.ProcessSpecialTypes(mOperand.ReflectedType.ToString()) +
                                "::" + mOperand.Name + "()";
                        }
                        else
                        {
                            // not supported
                        }
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        result += " " + GetExpandedOffset((int)operand);
                        break;
                    case OperandType.InlineType:
                        result += " " + Globals.ProcessSpecialTypes(operand.ToString());
                        break;
                    case OperandType.InlineString:
                        if (operand.ToString() == "\r\n") result += " \"\\r\\n\"";
                        else result += " \"" + operand.ToString() + "\"";
                        break;
                    case OperandType.ShortInlineVar:
                        result += operand.ToString();
                        break;
                    case OperandType.InlineI:
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineR:
                        result += operand.ToString();
                        break;
                    case OperandType.InlineTok:
                        if (operand is Type)
                            result += ((Type)operand).FullName;
                        else
                            result += "not supported";
                        break;
                    case OperandType.InlineSwitch:
                        {
                            int[] targets = (int[])operand;
                            result += " " + targets.Length + ": ";
                            for (int i = 0; i < targets.Length; i++)
                            {
                                if (i > 0)
                                    result += ", ";
                                result += "(" + i + " => " + targets[i] + ")";
                            }
                        }
                        break;

                    default: result += "not supported"; break;
                }
            }
            return result;

        }

        /// <summary>
        /// Add enough zeros to a number as to be represented on 4 characters
        /// </summary>
        /// <param name="offset">
        /// The number that must be represented on 4 characters
        /// </param>
        /// <returns>
        /// </returns>
        private string GetExpandedOffset(long offset)
        {
            string result = offset.ToString();
            for (int i = 0; result.Length < 4; i++)
            {
                result = "0" + result;
            }
            return result;
        }

        public ILInstruction()
        {

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Index + " (" + Offset + "): ");
            sb.Append(Code);
            if (Operand != null)
            {
                sb.Append(" ");
                sb.Append(Operand);
            }
            return sb.ToString();
        }
    }
}

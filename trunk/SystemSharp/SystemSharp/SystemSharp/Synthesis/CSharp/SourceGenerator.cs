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
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using SDILReader;

namespace SystemSharp.Synthesis.CSharp
{
    public abstract class SourceGenerator
    {
        public abstract void Generate(System.Reflection.MethodInfo method, TextWriter wr);
    }

    public class CSharpGenerator : SourceGenerator
    {
        private delegate void ProcessInstruction(ILInstruction instr);

        private Stack<string> _stack = new Stack<string>();
        private System.Reflection.MethodInfo _method;
        private string[] _localVarNames;
        private TextWriter _out;
        private ProcessInstruction[] handlers1 = new ProcessInstruction[0x100];
        private ProcessInstruction[] handlers2 = new ProcessInstruction[0x100];

        private void WriteLine(string text)
        {
            _out.WriteLine(text);
        }

        private void NyiHandler(ILInstruction instr)
        {
            OpCode code = instr.Code;
            System.Diagnostics.Debug.WriteLine("Unsupported OpCode: " + code);
            StackBehaviour bpop = code.StackBehaviourPop;
            switch (bpop)
            {
                case StackBehaviour.Pop0: 
                    break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    _stack.Pop();
                    goto case StackBehaviour.Pop1_pop1;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    _stack.Pop();
                    goto case StackBehaviour.Pop1;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    _stack.Pop();
                    break;
                case StackBehaviour.Varpop:
                    System.Diagnostics.Debug.WriteLine("Variable number of stack pops, possible stack courrption!");
                    break;
            }
            StackBehaviour bpush = code.StackBehaviourPush;
            switch (bpush)
            {
                case StackBehaviour.Push0:
                    break;
                case StackBehaviour.Push1_push1:
                    _stack.Push("<unknown>");
                    goto case StackBehaviour.Push1;
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    _stack.Push("<unknown>");
                    break;
                case StackBehaviour.Varpush:
                    System.Diagnostics.Debug.WriteLine("Variable number of stack pushs, possible stack corruption!");
                    break;
            }
        }

        private void NopHandler(ILInstruction instr)
        {
        }

        private void LdArgHandler(ILInstruction instr, int arg)
        {
            if (arg == 0)
                _stack.Push("this");
            else
            {
                ParameterInfo[] pis = _method.GetParameters();
                _stack.Push(pis[arg - 1].Name);
            }
        }

        private void LdLocHandler(ILInstruction instr, int loc)
        {
            IList<LocalVariableInfo> lvis = _method.GetMethodBody().LocalVariables;
            _stack.Push(_localVarNames[loc]);
        }

        private void LdCHandler(ILInstruction instr)
        {
            _stack.Push(instr.Operand.ToString());
        }

        private void LdSCHandler(ILInstruction instr)
        {
            _stack.Push("\"" + instr.Operand.ToString() + "\"");
        }

        private void StLocHandler(ILInstruction instr, int loc)
        {
            string value = _stack.Pop();
            WriteLine(_localVarNames[loc] + " = " + value + ";");
        }

        private void CallHandler(ILInstruction instr)
        {
            MethodInfo calledMethod = (MethodInfo)instr.Operand;
            ParameterInfo[] pis = calledMethod.GetParameters();
            string[] argValues = new string[pis.Length];
            for (int i = pis.Length - 1; i >= 0; i--)
                argValues[i] = _stack.Pop();
            string callString = "";
            if ((calledMethod.CallingConvention & CallingConventions.HasThis) == CallingConventions.HasThis)
            {
                callString = _stack.Pop();
            }
            else
            {
                callString = calledMethod.DeclaringType.FullName;
            }
            callString += ".";
            PropertyInfo[] classPis = calledMethod.DeclaringType.GetProperties();
            foreach (PropertyInfo pi in classPis)
            {
                if (calledMethod.Equals(pi.GetGetMethod()))
                {
                    callString += pi.Name;
                    _stack.Push(callString);
                    return;
                }
                if (calledMethod.Equals(pi.GetSetMethod()))
                {
                    callString += pi.Name;
                    string value = argValues[0];
                    WriteLine(callString + " = " + value + ";");
                    return;
                }
            }
            callString += calledMethod.Name + "(";
            for (int i = 0; i < pis.Length; i++)
            {
                ParameterInfo pi = pis[i];
                string arg = argValues[i];

                if (i > 0)
                    callString += ", ";

                if (pi.IsOut)
                    callString += "out ";

                callString += arg;
            }
            callString += ")";
            if (calledMethod.ReturnType == typeof(void))
            {
                WriteLine(callString + ";");
            }
            else
            {
                _stack.Push(callString);
            }
        }

        private void BinOpHandler(string opName)
        {
            string op1 = _stack.Pop();
            string op2 = _stack.Pop();
            _stack.Push("(" + op1 + ") " + opName + " (" + op2 + ")");
        }

        private void UnOpHandler(string opName)
        {
            string op = _stack.Pop();
            _stack.Push(opName + "(" + op + ")");
        }

        private void NewArrHandler(ILInstruction instr)
        {
            Type objType = (Type)instr.Operand;
            string numElems = _stack.Pop();
            string cmd = "new " + objType.FullName + "[" + numElems + "]";
            _stack.Push(cmd);
        }

        private void StelemHandler(ILInstruction instr)
        {
            string value = _stack.Pop();
            string index = _stack.Pop();
            string array = _stack.Pop();
            string cmd = array + "[" + index + "] = " + value + ";";
            WriteLine(cmd);
        }

        private void RetHandler(ILInstruction instr)
        {
            if (_method.ReturnType == typeof(void))
                WriteLine("return;");
            else
            {
                string result = _stack.Pop();
                WriteLine("return" + result + ";");
            }
        }

        public CSharpGenerator()
        {
            int i;
            for (i = 0; i < handlers1.Length; i++)
                handlers1[i] = NyiHandler;
            for (i = 0; i < handlers2.Length; i++)
                handlers2[i] = NyiHandler;

            handlers1[0x00] = NopHandler;
            handlers1[0x02] = ((ILInstruction instr) => LdArgHandler(instr, 0));
            handlers1[0x03] = ((ILInstruction instr) => LdArgHandler(instr, 1));
            handlers1[0x04] = ((ILInstruction instr) => LdArgHandler(instr, 2));
            handlers1[0x05] = ((ILInstruction instr) => LdArgHandler(instr, 3));

            handlers1[0x06] = ((ILInstruction instr) => LdLocHandler(instr, 0));
            handlers1[0x07] = ((ILInstruction instr) => LdLocHandler(instr, 1));
            handlers1[0x08] = ((ILInstruction instr) => LdLocHandler(instr, 2));
            handlers1[0x09] = ((ILInstruction instr) => LdLocHandler(instr, 3));

            handlers1[0x0a] = ((ILInstruction instr) => StLocHandler(instr, 0));
            handlers1[0x0b] = ((ILInstruction instr) => StLocHandler(instr, 1));
            handlers1[0x0c] = ((ILInstruction instr) => StLocHandler(instr, 2));
            handlers1[0x0d] = ((ILInstruction instr) => StLocHandler(instr, 3));

            handlers1[0x14] = ((ILInstruction instr) => _stack.Push("null"));
            handlers1[0x15] = ((ILInstruction instr) => _stack.Push("-1"));
            handlers1[0x16] = ((ILInstruction instr) => _stack.Push("0"));
            handlers1[0x17] = ((ILInstruction instr) => _stack.Push("1"));
            handlers1[0x18] = ((ILInstruction instr) => _stack.Push("2"));
            handlers1[0x19] = ((ILInstruction instr) => _stack.Push("3"));
            handlers1[0x1a] = ((ILInstruction instr) => _stack.Push("4"));
            handlers1[0x1b] = ((ILInstruction instr) => _stack.Push("5"));
            handlers1[0x1c] = ((ILInstruction instr) => _stack.Push("6"));
            handlers1[0x1d] = ((ILInstruction instr) => _stack.Push("7"));
            handlers1[0x1e] = ((ILInstruction instr) => _stack.Push("8"));
            handlers1[0x1f] = LdCHandler;
            handlers1[0x20] = LdCHandler;
            handlers1[0x21] = LdCHandler;
            handlers1[0x22] = LdCHandler;
            handlers1[0x23] = LdCHandler;

            handlers1[0x28] = CallHandler;
            handlers1[0x29] = CallHandler;
            handlers1[0x2a] = RetHandler;

            handlers1[0x58] = ((ILInstruction instr) => BinOpHandler("+"));
            handlers1[0x59] = ((ILInstruction instr) => BinOpHandler("-"));
            handlers1[0x5a] = ((ILInstruction instr) => BinOpHandler("*"));
            handlers1[0x5b] = ((ILInstruction instr) => BinOpHandler("/"));
            handlers1[0x5c] = ((ILInstruction instr) => BinOpHandler("/"));
            handlers1[0x5d] = ((ILInstruction instr) => BinOpHandler("%"));
            handlers1[0x5e] = ((ILInstruction instr) => BinOpHandler("%"));
            handlers1[0x5f] = ((ILInstruction instr) => BinOpHandler("&"));
            handlers1[0x60] = ((ILInstruction instr) => BinOpHandler("|"));
            handlers1[0x61] = ((ILInstruction instr) => BinOpHandler("^"));
            handlers1[0x62] = ((ILInstruction instr) => BinOpHandler("<<"));
            handlers1[0x63] = ((ILInstruction instr) => BinOpHandler(">>"));
            handlers1[0x64] = ((ILInstruction instr) => BinOpHandler(">>"));
            handlers1[0x65] = ((ILInstruction instr) => UnOpHandler("-"));
            handlers1[0x66] = ((ILInstruction instr) => UnOpHandler("~"));

            handlers1[0x6f] = CallHandler;
            handlers1[0x72] = LdSCHandler;
            handlers1[0x8c] = NopHandler; // box
            handlers1[0x8d] = NewArrHandler;
            handlers1[0x9b] = StelemHandler;
            handlers1[0x9c] = StelemHandler;
            handlers1[0x9d] = StelemHandler;
            handlers1[0x9e] = StelemHandler;
            handlers1[0x9f] = StelemHandler;
            handlers1[0xa0] = StelemHandler;
            handlers1[0xa1] = StelemHandler;
            handlers1[0xa2] = StelemHandler;
        }

        public override void Generate(System.Reflection.MethodInfo method, TextWriter wr)
        {
            _method = method;
            _out = wr;
            MethodBody body = _method.GetMethodBody();
            IList<LocalVariableInfo> lvis = body.LocalVariables;
            int maxIndex = -1;
            foreach (LocalVariableInfo lvi in lvis)
            {
                maxIndex = Math.Max(maxIndex, lvi.LocalIndex);
            }
            _localVarNames = new string[maxIndex + 1];
            foreach (LocalVariableInfo lvi in lvis)
            {
                string name = "v" + lvi.LocalIndex;
                wr.WriteLine(lvi.LocalType.FullName + " " + name + ";");
                _localVarNames[lvi.LocalIndex] = name;
            }
            MethodBodyReader mbr = new MethodBodyReader(method);
            System.Diagnostics.Debug.WriteLine(mbr.GetBodyCode());
            foreach (ILInstruction instr in mbr.instructions)
            {
                OpCode code = instr.Code;
                if (code.Value < 0x100)
                    handlers1[code.Value](instr);
                else
                    handlers2[code.Value >> 8](instr);
//                wr.WriteLine(instr.Code.Name);
            }
        }
    }
}

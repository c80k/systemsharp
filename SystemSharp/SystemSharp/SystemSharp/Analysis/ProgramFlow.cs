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
 * 
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Analysis
{
    public static class ProgramFlow
    {
        public enum EBarrierType
        {
            Global,
            PortIO
        }

        private class CompileBarrier : CompileMethodCall
        {
            EBarrierType _type;

            public CompileBarrier(EBarrierType type)
            {
                _type = type;
            }

            public override void Compile(CallStatement call, ICompilerBackend backend)
            {
                switch (_type)
                {
                    case EBarrierType.Global:
                        backend.InstallBarrier();
                        break;

                    case EBarrierType.PortIO:
                        backend.InstallBarrier(InstructionCodes.WrPort, InstructionCodes.RdPort);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private class UnrollControl : RewriteCall,
            IOnMethodCall
        {
            private bool _unrollFlag;

            public UnrollControl(bool unrollFlag)
            {
                _unrollFlag = unrollFlag;
            }

            public void OnMethodCall(MethodFacts callerFacts, MethodBase callee, int ilIndex)
            {
                if (_unrollFlag)
                    callerFacts.UnrollHeaders.Add(ilIndex);
                else
                    callerFacts.NonUnrollHeaders.Add(ilIndex);
            }

            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                return true;
            }
        }

        [CompileBarrier(EBarrierType.Global)]
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Barrier, EBarrierType.Global)]
        public static void Barrier()
        {
        }

        [CompileBarrier(EBarrierType.PortIO)]
        [MapToIntrinsicFunction(IntrinsicFunction.EAction.Barrier, EBarrierType.PortIO)]
        public static void IOBarrier()
        {
        }

        [UnrollControl(true)]
        public static void Unroll()
        {
        }

        [UnrollControl(false)]
        public static void DoNotUnroll()
        { 
        }
    }
}

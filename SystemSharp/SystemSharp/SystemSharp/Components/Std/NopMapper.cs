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
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// A service for mapping the "nop" XIL instruction to hardware.
    /// </summary>
    public class NopMapper: IXILMapper
    {
        private class NopTASite : DefaultTransactionSite
        {
            private Component _host;

            public NopTASite(Component host):
                base(host)
            {
                _host = host;
            }

            public override string Name
            {
                get { return "nop"; }
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return Verb(ETVMode.Locked);
            }

            public IEnumerable<TAVerb> Nop(int latency = 1)
            {
                for (int i = 0; i < latency; i++)
                    yield return Verb(ETVMode.Shared);
            }

            public IEnumerable<TAVerb> Id(ISignalSource<StdLogicVector> operand, ISignalSink<StdLogicVector> result)
            {
                yield return Verb(ETVMode.Shared, result.Comb.Connect(operand));
            }
        }

        private class NopXILMapping : IXILMapping
        {
            private NopTASite _site;
            private int _latency;

            public NopXILMapping(NopTASite tasite, int latency)
            {
                _site = tasite;
                _latency = latency;
            }

            public IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _site.Nop(_latency);
            }

            public ITransactionSite TASite
            {
                get { return _site; }
            }

            public EMappingKind ResourceKind
            {
                get { return EMappingKind.ReplicatableResource; }
            }

            public int InitiationInterval
            {
                get { return 0; }
            }

            public int Latency
            {
                get { return _latency; }
            }

            public string Description
            {
                get { return "no operation"; }
            }
        }

        private class IdXILMapping : DefaultXILMapping
        {
            private NopTASite _site;

            public IdXILMapping(NopTASite tasite) :
                base(tasite, EMappingKind.ReplicatableResource)
            {
                _site = tasite;
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _site.Id(operands[0], results[0]);
            }

            protected override IEnumerable<TAVerb> RealizeDefault()
            {
                return _site.Id(SignalSource.Create<StdLogicVector>("0"), SignalSink.Nil<StdLogicVector>());
            }

            public override string Description
            {
                get { return "identity (no operation)"; }
            }
        }

        /// <summary>
        /// Returns nop, barrier, convert
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Nop();
            yield return DefaultInstructionSet.Instance.Barrier();
            yield return DefaultInstructionSet.Instance.Convert();
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public NopMapper()
        {
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            //if (fu != _host)
            //    yield break;

            var taNop = taSite as NopTASite;
            if (taNop == null)
                yield break;

            switch (instr.Name)
            {
                case InstructionCodes.Nop:
                case InstructionCodes.Barrier:
                    if (instr.Operand is int)
                        yield return new NopXILMapping(taNop, (int)instr.Operand);
                    else
                        yield return new NopXILMapping(taNop, 0);
                    break;

                case InstructionCodes.Convert:
                    if (TypeLowering.Instance.HasWireType(operandTypes[0]) &&
                        TypeLowering.Instance.HasWireType(resultTypes[0]) &&
                        !operandTypes[0].CILType.Equals(resultTypes[0].CILType))
                    {
                        TypeDescriptor owt = TypeLowering.Instance.GetWireType(operandTypes[0]);
                        TypeDescriptor rwt = TypeLowering.Instance.GetWireType(resultTypes[0]);
                        if (!owt.Equals(rwt))
                            yield break;

                        yield return new IdXILMapping(taNop);
                    }
                    else
                    {
                        yield break;
                    }
                    break;

                default:
                    yield break;
            }
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject proj)
        {
            switch (instr.Name)
            {
                case InstructionCodes.Nop:
                case InstructionCodes.Barrier:
                    if (instr.Operand is int)
                        return new NopXILMapping(new NopTASite(host), (int)instr.Operand);
                    else
                        return new NopXILMapping(new NopTASite(host), 0);

                case InstructionCodes.Convert:
                    if (TypeLowering.Instance.HasWireType(operandTypes[0]) &&
                        TypeLowering.Instance.HasWireType(resultTypes[0]) &&
                        !operandTypes[0].CILType.Equals(resultTypes[0].CILType))
                    {
                        TypeDescriptor owt = TypeLowering.Instance.GetWireType(operandTypes[0]);
                        TypeDescriptor rwt = TypeLowering.Instance.GetWireType(resultTypes[0]);
                        if (!owt.Equals(rwt))
                            return null;

                        return new IdXILMapping(new NopTASite(host));
                    }
                    else
                    {
                        return null;
                    }

                default:
                    return null;
            }
        }
    }
}


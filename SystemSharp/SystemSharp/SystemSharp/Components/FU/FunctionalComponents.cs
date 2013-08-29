/**
 * Copyright 2011-2012 Christian Köllner
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
using System.Diagnostics;
using System.Linq;
using SystemSharp.Assembler;
using SystemSharp.Collections;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Components.FU
{
    public interface IXILMapping
    {
        string Description { get; }
        ITransactionSite TASite { get; }
        EMappingKind ResourceKind { get; }
        int InitiationInterval { get; }
        int Latency { get; }
        IEnumerable<TAVerb> Realize(
            ISignalSource<StdLogicVector>[] operands,
            ISignalSink<StdLogicVector>[] results);
    }

    public abstract class DefaultXILMapping : IXILMapping
    {
        public ITransactionSite TASite { get; private set; }
        public EMappingKind ResourceKind { get; private set; }

        private bool _inited;
        private int _ii;
        public virtual int InitiationInterval
        {
            get
            {
                if (!_inited)
                    Initialize();
                return _ii;
            }
        }

        private int _lat;
        public virtual int Latency
        {
            get
            {
                if (!_inited)
                    Initialize();
                return _lat;
            }
        }

        public abstract IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results);
        protected abstract IEnumerable<TAVerb> RealizeDefault();
        public abstract string Description { get; }

        public DefaultXILMapping(ITransactionSite taSite, EMappingKind resourceKind)
        {
            TASite = taSite;
            ResourceKind = resourceKind;
        }

        private void Initialize()
        {
            Debug.Assert(!_inited);
            IEnumerable<TAVerb> rdef = RealizeDefault();
            TAVerb last = null;
            foreach (TAVerb verb in rdef)
            {
                if (verb.TMode == ETVMode.Locked)
                    ++_ii;
                ++_lat;
                last = verb;
            }
            if (last != null)
                --_lat;
            _inited = true;
        }
    }

    public enum EMappingKind
    {
        ExclusiveResource,
        ReplicatableResource,
        LightweightResource
    }

    public interface IXILMapper
    {
        IEnumerable<XILInstr> GetSupportedInstructions();
        IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes);
        IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=true)]
    public class DeclareXILMapper : Attribute
    {
        public Type XILMapperType { get; private set; }

        public DeclareXILMapper(Type xilMapperType)
        {
            XILMapperType = xilMapperType;
        }

        public IXILMapper CreateMapper()
        {
            return (IXILMapper)Activator.CreateInstance(XILMapperType);
        }
    }

    public abstract class FunctionalUnit : Component
    {
        public virtual string DisplayName
        {
            get { return GetType().Name; }
        }
    }

    public interface ISupportsDiagnosticOutput
    {
        bool EnableDiagnostics { get; set; }
    }
}

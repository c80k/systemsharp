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
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
    /// <summary>
    /// Describes a mapping of a XIL instruction to a hardware functional unit, represented by a transaction site.
    /// </summary>
    public interface IXILMapping
    {
        /// <summary>
        /// Textual description of mapping for debug/documentation purpose
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Transaction site to carry out the mapped instruction
        /// </summary>
        ITransactionSite TASite { get; }

        /// <summary>
        /// Classification of involved hardware resource
        /// </summary>
        EMappingKind ResourceKind { get; }

        /// <summary>
        /// The initiation interval is the amount of c-steps needed to start execution. 
        /// During initiation, the transaction site cannot start any other instruction.
        /// Typical are values of 0 or 1.
        /// </summary>
        int InitiationInterval { get; }

        /// <summary>
        /// The latency is the total amount of c-steps needed for the instruction to execute.
        /// The transaction site may support vertical parallelism, which means that another instruction
        /// may be initiated while the current instruction(s) is/are still executing.
        /// </summary>
        int Latency { get; }

        /// <summary>
        /// Realizes the mapping for given operand sources and result sinks.
        /// </summary>
        /// <param name="operands">operand sources</param>
        /// <param name="results">result sinks</param>
        /// <returns>a sequence of transaction verbs which represent the dataflows necessary to perform the mapping</returns>
        IEnumerable<TAVerb> Realize(
            ISignalSource<StdLogicVector>[] operands,
            ISignalSink<StdLogicVector>[] results);
    }

    /// <summary>
    /// Provides an abstract default implementation of IXILMapping.
    /// </summary>
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

        /// <summary>
        /// Realizes the mapping for given operand sources and result sinks. You must override this method in derived classes.
        /// </summary>
        /// <param name="operands">operand sources</param>
        /// <param name="results">result sinks</param>
        /// <returns>a sequence of transaction verbs which represent the dataflows necessary to perform the mapping</returns>
        public abstract IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results);

        /// <summary>
        /// Realizes the mapping using dummy operand sources and sinks. This is used by the internal implementation to
        /// determine initiation interval and latency. You must override this method in derived classes.
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<TAVerb> RealizeDefault();

        /// <summary>
        /// Provides a textual description of the mapping for debug/documentation purpose. You must override this method in derived classes.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="taSite">transaction site to carry out the mapping</param>
        /// <param name="resourceKind">resource classification</param>
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

    /// <summary>
    /// Provides a classification of hardware resources
    /// </summary>
    public enum EMappingKind
    {
        /// <summary>
        /// A hardware functional unit which needs access to some exclusive hardware resource and therefore cannot be cloned
        /// </summary>
        ExclusiveResource,

        /// <summary>
        /// A hardware functional unit which can be cloned in order to achieve more instruction-level parallelism
        /// </summary>
        ReplicatableResource,

        /// <summary>
        /// A trivial hardware functional unit which should be cloned for each assigned instruction rather than shared
        /// </summary>
        LightweightResource
    }

    /// <summary>
    /// Describes a service which maps XIL instructions to hardware functional units
    /// </summary>
    public interface IXILMapper
    {
        /// <summary>
        /// Returns a sequence of XIL instructions which are supported by this mapper. Only their op-codes are of
        /// importance, not their static operands.
        /// </summary>
        IEnumerable<XILInstr> GetSupportedInstructions();

        /// <summary>
        /// Tries to map XIL instruction <paramref name="instr"/> to transaction site <paramref name="taSite"/>.
        /// </summary>
        /// <remarks>
        /// Typically, the returned sequence contains either one mapping or is empty (because instruction cannot be mapped to
        /// given transaction site). However, some commutative operations like addition and multiplication in fact allow for two
        /// mapping possibilities by swapping the operands. In those cases, both mappings should be returned.
        /// </remarks>
        /// <param name="taSite">transaction site to carry out the mapping</param>
        /// <param name="instr">XIL instruction to map</param>
        /// <param name="operandTypes">operand types of instruction</param>
        /// <param name="resultTypes">result types of instruction</param>
        /// <returns>a sequence of possible mappings, empty sequence if instruction cannot be mapped</returns>
        IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes);

        /// <summary>
        /// Allocates a new hardware functional unit for XIL instruction <paramref name="instr"/> and returns a mapping to it.
        /// </summary>
        /// <param name="host">component instance to host the newly allocated unit</param>
        /// <param name="instr">XIL instruction to map</param>
        /// <param name="operandTypes">operand types of instruction</param>
        /// <param name="resultTypes">result types of instruction</param>
        /// <param name="targetProject">target project for code generation</param>
        /// <returns>a mapping or null if instruction can't be mapped</returns>
        IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject);
    }

    /// <summary>
    /// This attribute declares a XIL mapper for the hardware functional unit implementation it is attached to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=true)]
    public class DeclareXILMapper : Attribute
    {
        public Type XILMapperType { get; private set; }

        /// <summary>
        /// Constructs the attribute.
        /// </summary>
        /// <param name="xilMapperType">Type of class implementing the mapper service. The specified class must implement
        /// the IXILMapper interface and provide a default public constructor.</param>
        public DeclareXILMapper(Type xilMapperType)
        {
            Contract.Requires<ArgumentNullException>(xilMapperType != null, "xilMapperType");
            Contract.Requires<ArgumentException>(typeof(IXILMapper).IsAssignableFrom(xilMapperType),
                "Specified mapper type must implement IXILMapper.");
            Contract.Requires<ArgumentException>(xilMapperType.GetConstructor(new Type[0]) != null,
                "Specified mapper type must provide a default public constructor.");

            XILMapperType = xilMapperType;
        }

        public IXILMapper CreateMapper()
        {
            return (IXILMapper)Activator.CreateInstance(XILMapperType);
        }
    }

    /// <summary>
    /// Base class for implementing hardware functional units. However, it is not required that you implementation
    /// inherits from that class.
    /// </summary>
    public abstract class FunctionalUnit : Component
    {
        /// <summary>
        /// A human-friendly name used for debugging/documentation purpose.
        /// </summary>
        public virtual string DisplayName
        {
            get { return GetType().Name; }
        }
    }

    /// <summary>
    /// Marker interface for hardware functional units supporting an optional "verbose mode", i.e. producing 
    /// diagnostic output during runtime.
    /// </summary>
    public interface ISupportsDiagnosticOutput
    {
        /// <summary>
        /// Switches diagnostic mode on or off.
        /// </summary>
        bool EnableDiagnostics { get; set; }
    }
}

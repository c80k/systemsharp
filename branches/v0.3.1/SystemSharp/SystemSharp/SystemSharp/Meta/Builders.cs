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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.SysDOM;

namespace SystemSharp.Meta
{
    /// <summary>
    /// A component descriptor which exists in the SysDOM domain only, i.e. without a component instance.
    /// </summary>
    public class ComponentBuilder: 
        DescriptorBase,
        IPackageOrComponentDescriptor,
        IComponentDescriptor,
        IDependencyOrdered
    {
        private string _name;
        private PackageOrComponentDescriptor _container;

        internal ComponentBuilder(string name)
        {
            _name = name;
            _container = new PackageOrComponentDescriptor();
        }

        /// <summary>
        /// Adds a package dependency to this descriptor.
        /// </summary>
        /// <param name="pd">package descriptor to add as dependency</param>
        public void AddDependency(PackageDescriptor pd)
        {
            _container.AddDependency(pd);
        }

        /// <summary>
        /// Returns all packages this descriptor depends on.
        /// </summary>
        public IEnumerable<PackageDescriptor> Dependencies
        {
            get { return _container.Dependencies; }
        }

        public int DependencyOrder { get; internal set; }

        int IDependencyOrdered.DependencyOrder
        {
            get { return DependencyOrder; }
            set { DependencyOrder = value; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public string Library { get; set; }
    }

    /// <summary>
    /// A signal descriptor which exists in the SysDOM domain only, i.e. without any signal instance.
    /// </summary>
    public class SignalBuilder :
        DescriptorBase,
        ISignalOrPortDescriptor
    {
        private TypeDescriptor _elementType;

        /// <summary>
        /// Constructs a descriptor instance.
        /// </summary>
        /// <param name="elementType">type descriptor of signal value</param>
        /// <param name="initialValue">initial signal value</param>
        public SignalBuilder(TypeDescriptor elementType, object initialValue)
        {
            _elementType = elementType;
            InitialValue = initialValue;
        }

        /// <summary>
        /// Returns the type descriptor of signal value.
        /// </summary>
        public TypeDescriptor ElementType
        {
            get { return _elementType; }
        }

        /// <summary>
        /// Returns the expected implementation-level signal type.
        /// </summary>
        public TypeDescriptor InstanceType
        {
            get { return typeof(Signal<>).MakeGenericType(ElementType.CILType); }
        }

        /// <summary>
        /// Gets or sets the initial signal value.
        /// </summary>
        public object InitialValue { get; set; }
    }

    /// <summary>
    /// A port descriptor which exists in the SysDOM domain only, i.e. without any underlying component instance.
    /// </summary>
    public class PortBuilder :
        DescriptorBase,
        IPortDescriptor
    {
        private EPortDirection _dir;
        private EPortUsage _usage;
        private string _domain;
        private ISignalDescriptor _boundSignal;
        private TypeDescriptor _elementType;

        /// <summary>
        /// Constructs a descriptor instance.
        /// </summary>
        /// <param name="dir">data-flow direction of the port</param>
        /// <param name="usage">usage hint</param>
        /// <param name="domain">optional argument for future use</param>
        /// <param name="elementType">type descriptor of exchanged data</param>
        public PortBuilder(EPortDirection dir, EPortUsage usage, string domain, TypeDescriptor elementType)
        {
            _dir = dir;
            _usage = usage;
            _domain = domain;
            _elementType = elementType;
        }

        /// <summary>
        /// Returns the data-flow direction of the port.
        /// </summary>
        public EPortDirection Direction
        {
            get { return _dir; }
        }

        /// <summary>
        /// Returns the usage hint of the port.
        /// </summary>
        public EPortUsage Usage
        {
            get { return _usage; }
        }

        public string Domain
        {
            get { return _domain; }
        }

        /// <summary>
        /// Returns the signal descriptor this port is bound to, or <c>null</c> if none.
        /// </summary>
        public ISignalDescriptor BoundSignal
        {
            get { return _boundSignal; }
        }

        /// <summary>
        /// Returns the type descriptor of exchanged data.
        /// </summary>
        public TypeDescriptor ElementType
        {
            get { return _elementType; }
        }

        /// <summary>
        /// Returns the initial value.
        /// </summary>
        public object InitialValue
        {
            get { return BoundSignal.InitialValue; }
        }

        /// <summary>
        /// Binds this port to a signal.
        /// </summary>
        /// <param name="desc">descriptor of signal to bind</param>
        public void Bind(ISignalDescriptor desc)
        {
            _boundSignal = desc;
        }

        /// <summary>
        /// Returns the real or expected implementation-level type of the bound signal.
        /// </summary>
        public TypeDescriptor InstanceType
        {
            get { return BoundSignal.InstanceType; }
        }
    }

    /// <summary>
    /// A field descriptor which exists in the SysDOM domain only, i.e. without any underlying component instance.
    /// </summary>
    public class DOMFieldBuilder : FieldDescriptor
    {
        /// <summary>
        /// Construct a descriptor instance.
        /// </summary>
        /// <param name="type">type descriptor of the field</param>
        public DOMFieldBuilder(TypeDescriptor type):
            base(type)
        {
            Contract.Requires(type != null);
        }

        /// <summary>
        /// Constructs a descriptor instance with an initial field value.
        /// </summary>
        /// <param name="type">type descriptor of the field</param>
        /// <param name="initialValue">initial field value</param>
        public DOMFieldBuilder(TypeDescriptor type, object initialValue) :
            base(type)
        {
            Contract.Requires(type != null);
            ConstantValue = initialValue;
        }

        /// <summary>
        /// No available for this kind of descriptor, do not call.
        /// </summary>
        public override object Value
        {
            get { throw new NotSupportedException("Purely reflective field, no value available"); }
        }

        /// <summary>
        /// Returns always <c>false</c>, since static fields are not supported by this kind of descriptor.
        /// </summary>
        public override bool IsStatic
        {
            get { return false; }
        }

        public override bool Equals(object obj)
        {
            var other = obj as DOMFieldBuilder;
            if (other == null)
                return false;
            return Owner.Equals(other.Owner) &&
                Name.Equals(other.Name);
        }

        public override int GetHashCode()
        {
            return Owner.GetHashCode() ^
                Name.GetHashCode();
        }

        /// <summary>
        /// Gets or sets a predicate for determining whether the field is read in a given context.
        /// </summary>
        public Func<DesignContext, bool> IsReadFunc { get; set; }

        /// <summary>
        /// Gets or sets a predicate for determining whether the field is written in a given context.
        /// </summary>
        public Func<DesignContext, bool> IsWrittenFunc { get; set; }

        public override bool IsReadInCurrentContext(DesignContext context)
        {
            return IsReadFunc(context);
        }

        public override bool IsWrittenInCurrentContext(DesignContext context)
        {
            return IsWrittenFunc(context);
        }
    }

    /// <summary>
    /// This static class provides fabric methods for creating SysDOM descriptors.
    /// </summary>
    public static class DescriptorBuilders
    {
        /// <summary>
        /// Creates and adds a new SysDOM-only signal descriptor.
        /// </summary>
        /// <param name="me">component descriptor to host the new signal</param>
        /// <param name="name">name of new signal</param>
        /// <param name="dataType">type of signal value</param>
        /// <returns>the descriptor for the newly created signal</returns>
        public static SignalBuilder CreateSignal(this IComponentDescriptor me, string name, Type dataType)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(name != null);
            Contract.Requires<ArgumentNullException>(dataType != null);

            object initialValue = Activator.CreateInstance(dataType);
            SignalBuilder result = new SignalBuilder(dataType, initialValue);
            me.AddChild(result, name);
            return result;
        }

        /// <summary>
        /// Creates and adds a new signal, including its implementation-level instance.
        /// </summary>
        /// <param name="me">component descriptor to host the new signal</param>
        /// <param name="name">name of new signal</param>
        /// <param name="initialValue">initial value of new signal</param>
        /// <returns>the descriptor for the newly created signal</returns>
        public static SignalDescriptor CreateSignalInstance(this IComponentDescriptor me, string name, object initialValue)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(initialValue != null);
            Contract.Requires<ArgumentNullException>(name != null);

            SignalBase sinst = Signals.CreateInstance(initialValue);
            SignalDescriptor result = sinst.Descriptor;
            me.AddChild(result, name);
            return result;
        }

        /// <summary>
        /// Creates and adds a new port.
        /// </summary>
        /// <param name="me">component descriptor to host the new port</param>
        /// <param name="name">name of new port</param>
        /// <param name="dir">data-flow direction</param>
        /// <param name="usage">usage hint</param>
        /// <param name="dataType">type descriptor of exchanged data</param>
        /// <returns>the descriptor of the newly created port</returns>
        public static PortBuilder CreatePort(this IComponentDescriptor me, string name, EPortDirection dir, EPortUsage usage, TypeDescriptor dataType)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(name != null);
            Contract.Requires<ArgumentNullException>(dataType != null);

            PortBuilder result = new PortBuilder(dir, usage, null, dataType);
            me.AddChild(result, name);
            return result;
        }

        /// <summary>
        /// Creates and adds a new port.
        /// </summary>
        /// <param name="me">component descriptor to host the new port</param>
        /// <param name="name">name of new port</param>
        /// <param name="dir">data-flow direction</param>
        /// <param name="dataType">type descriptor of exchanged data</param>
        /// <returns>the descriptor of the newly created port</returns>
        public static PortBuilder CreatePort(this IComponentDescriptor me, string name, EPortDirection dir, TypeDescriptor dataType)
        {
            return CreatePort(me, name, dir, EPortUsage.Default, dataType);
        }

        /// <summary>
        /// Creates and adds a new process.
        /// </summary>
        /// <param name="me">component descriptor to host the new process</param>
        /// <param name="kind">kind of process</param>
        /// <param name="func">behavioral description of the new process</param>
        /// <param name="sensitivity">sensitivity list of the new process</param>
        /// <returns>the descriptor of the new process</returns>
        public static ProcessDescriptor CreateProcess(this IComponentDescriptor me, 
            Process.EProcessKind kind, Function func, params ISignalOrPortDescriptor[] sensitivity)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(func != null);
            Contract.Requires<ArgumentNullException>(sensitivity != null);

            ProcessDescriptor pd = new ProcessDescriptor(func.Name)
            {
                Kind = kind,
                Implementation = func,
                Sensitivity = sensitivity
            };
            me.AddChild(pd, pd.Name);
            return pd;
        }

        /// <summary>
        /// Creates and adds a new symbolic process (i.e. without specification of behavior).
        /// </summary>
        /// <param name="me">component descriptor to host the new process</param>
        /// <param name="kind">kind of process</param>
        /// <param name="name">name of the new process</param>
        /// <param name="sensitivity">sensitivity list of the new process</param>
        /// <returns>the descriptor of the new process</returns>
        public static ProcessDescriptor CreateProcess(this IComponentDescriptor me,
            Process.EProcessKind kind, string name, params ISignalOrPortDescriptor[] sensitivity)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(name != null);
            Contract.Requires<ArgumentNullException>(sensitivity != null);

            ProcessDescriptor pd = new ProcessDescriptor(name)
            {
                Kind = kind,
                Sensitivity = sensitivity
            };
            me.AddChild(pd, pd.Name);
            return pd;
        }

        /// <summary>
        /// Creates and adds a new field.
        /// </summary>
        /// <param name="me">component descriptor to host the new field</param>
        /// <param name="type">type descriptor of the new field</param>
        /// <param name="name">name of the new field</param>
        /// <returns>the descriptor of the new field</returns>
        public static DOMFieldBuilder CreateField(this IComponentDescriptor me,
            TypeDescriptor type, string name)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(type != null);
            Contract.Requires<ArgumentNullException>(name != null);

            var fb = new DOMFieldBuilder(type);
            me.AddChild(fb, name);
            return fb;
        }

        /// <summary>
        /// Creates and adds a new field.
        /// </summary>
        /// <param name="me">component descriptor to host the new field</param>
        /// <param name="type">type descriptor of the new field</param>
        /// <param name="initialValue">initial field value</param>
        /// <param name="name">name of the new field</param>
        /// <returns>the descriptor of the new field</returns>
        public static DOMFieldBuilder CreateField(this IComponentDescriptor me,
            TypeDescriptor type, object initialValue, string name)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(type != null);
            Contract.Requires<ArgumentNullException>(name != null);
            Contract.Requires<ArgumentException>(initialValue == null || TypeDescriptor.GetTypeOf(initialValue).Equals(type), "Field type must match type of initial value");

            var fb = new DOMFieldBuilder(type, initialValue);
            me.AddChild(fb, name);
            return fb;
        }
    }
}

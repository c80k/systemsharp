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

        public void AddDependency(PackageDescriptor pd)
        {
            _container.AddDependency(pd);
        }

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

    public class SignalBuilder :
        DescriptorBase,
        ISignalOrPortDescriptor
    {
        //private string _name;
        private TypeDescriptor _elementType;

        public SignalBuilder(/*string name, */TypeDescriptor elementType, object initialValue)
        {
            //_name = name;
            _elementType = elementType;
            InitialValue = initialValue;
        }

        /*public override string Name
        {
            get { return _name; }
        }*/

        public TypeDescriptor ElementType
        {
            get { return _elementType; }
        }

        public TypeDescriptor InstanceType
        {
            get { return typeof(Signal<>).MakeGenericType(ElementType.CILType); }
        }

        public object InitialValue { get; set; }
    }

    public class PortBuilder :
        DescriptorBase,
        IPortDescriptor
    {
        private EPortDirection _dir;
        private EPortUsage _usage;
        private string _domain;
        private ISignalDescriptor _boundSignal;
        private TypeDescriptor _elementType;

        public PortBuilder(EPortDirection dir, EPortUsage usage, string domain, TypeDescriptor elementType)
        {
            _dir = dir;
            _usage = usage;
            _domain = domain;
            _elementType = elementType;
        }

        public EPortDirection Direction
        {
            get { return _dir; }
        }

        public EPortUsage Usage
        {
            get { return _usage; }
        }

        public string Domain
        {
            get { return _domain; }
        }

        public ISignalDescriptor BoundSignal
        {
            get { return _boundSignal; }
        }

        public TypeDescriptor ElementType
        {
            get { return _elementType; }
        }

        public object InitialValue
        {
            get { return BoundSignal.InitialValue; }
        }

        public void Bind(ISignalDescriptor desc)
        {
            _boundSignal = desc;
        }

        public TypeDescriptor InstanceType
        {
            get { return BoundSignal.InstanceType; }
        }
    }

    public class DOMFieldBuilder : FieldDescriptor
    {
        public DOMFieldBuilder(TypeDescriptor type):
            base(type)
        {
            Contract.Requires(type != null);
        }

        public DOMFieldBuilder(TypeDescriptor type, object initialValue) :
            base(type)
        {
            Contract.Requires(type != null);
            ConstantValue = initialValue;
        }

        public override object Value
        {
            get { throw new NotSupportedException("Purely reflective field, no value available"); }
        }

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

        public Func<DesignContext, bool> IsReadFunc { get; set; }
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

    public static class DescriptorBuilders
    {
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

        public static PortBuilder CreatePort(this IComponentDescriptor me, string name, EPortDirection dir, EPortUsage usage, TypeDescriptor dataType)
        {
            Contract.Requires<ArgumentNullException>(me != null);
            Contract.Requires<ArgumentNullException>(name != null);
            Contract.Requires<ArgumentNullException>(dataType != null);

            PortBuilder result = new PortBuilder(dir, usage, null, dataType);
            me.AddChild(result, name);
            return result;
        }

        public static PortBuilder CreatePort(this IComponentDescriptor me, string name, EPortDirection dir, TypeDescriptor dataType)
        {
            return CreatePort(me, name, dir, EPortUsage.Default, dataType);
        }

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

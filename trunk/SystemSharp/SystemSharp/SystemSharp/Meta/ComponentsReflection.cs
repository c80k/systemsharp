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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SystemSharp.Analysis;
using SystemSharp.Analysis.Msil;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Meta
{
    /// <summary>
    /// Visitor interface for descriptors.
    /// </summary>
    public interface IDescriptorVisitor
    {
        /// <summary>
        /// Called for component descriptor.
        /// </summary>
        /// <param name="cd">the component descriptor</param>
        void VisitComponentDescriptor(ComponentDescriptor cd);

        /// <summary>
        /// Called for field descriptor.
        /// </summary>
        /// <param name="fd">the field descriptor</param>
        void VisitFieldDescriptor(FieldDescriptor fd);

        /// <summary>
        /// Called for signal descriptor.
        /// </summary>
        /// <param name="sd">the signal descriptor</param>
        void VisitSignalDescriptor(SignalDescriptor sd);

        /// <summary>
        /// Called for port descriptor.
        /// </summary>
        /// <param name="pd">the port descriptor</param>
        void VisitPortDescriptor(PortDescriptor pd);

        /// <summary>
        /// Called for method descriptor.
        /// </summary>
        /// <param name="md">the method descriptor</param>
        void VisitMethodDescriptor(MethodDescriptor md);

        /// <summary>
        /// Called for process descriptor.
        /// </summary>
        /// <param name="pd">the process descriptor</param>
        void VisitProcessDescriptor(ProcessDescriptor pd);
    }

    /// <summary>
    /// The default implementation of the descriptor visitor pattern redirects to user-defined delegates
    /// which are accessible by properties.
    /// </summary>
    public class DefaultDescriptorVisitor : IDescriptorVisitor
    {
        public delegate void ComponentDescriptorHandler(ComponentDescriptor cd);
        public delegate void FieldDescriptorHandler(FieldDescriptor fd);
        public delegate void SignalDescriptorHandler(SignalDescriptor sd);
        public delegate void PortDescriptorHandler(PortDescriptor cd);
        public delegate void MethodDescriptorHandler(MethodDescriptor md);
        public delegate void ProcessDescriptorHandler(ProcessDescriptor pd);

        public DefaultDescriptorVisitor()
        {
            OnComponent = (x) => { };
            OnField = (x) => { };
            OnSignal = (x) => { };
            OnPort = (x) => { };
            OnMethod = (x) => { };
            OnProcess = (x) => { };
        }

        /// <summary>
        /// Gets or sets the handler for component descriptors.
        /// </summary>
        public ComponentDescriptorHandler OnComponent { get; set; }

        /// <summary>
        /// Gets or sets the handler for field descriptors.
        /// </summary>
        public FieldDescriptorHandler OnField { get; set; }

        /// <summary>
        /// Gets or sets the handler for signal descriptors.
        /// </summary>
        public SignalDescriptorHandler OnSignal { get; set; }

        /// <summary>
        /// Gets or sets the handler for port descriptors.
        /// </summary>
        public PortDescriptorHandler OnPort { get; set; }

        /// <summary>
        /// Gets or sets the handler for method descriptors.
        /// </summary>
        public MethodDescriptorHandler OnMethod { get; set; }

        /// <summary>
        /// Gets or sets the handler for process descriptors.
        /// </summary>
        public ProcessDescriptorHandler OnProcess { get; set; }

        public void VisitComponentDescriptor(ComponentDescriptor cd)
        {
            OnComponent(cd);
        }

        public void VisitFieldDescriptor(FieldDescriptor fd)
        {
            OnField(fd);
        }

        public void VisitSignalDescriptor(SignalDescriptor sd)
        {
            OnSignal(sd);
        }

        public void VisitPortDescriptor(PortDescriptor pd)
        {
            OnPort(pd);
        }

        public void VisitMethodDescriptor(MethodDescriptor md)
        {
            OnMethod(md);
        }

        public void VisitProcessDescriptor(ProcessDescriptor pd)
        {
            OnProcess(pd);
        }
    }

    /// <summary>
    /// This interface describes model elements which are owned by some superordinate descriptor.
    /// </summary>
    [ContractClass(typeof(ContainmentImplementorContractClass))]
    public interface IContainmentImplementor
    {
        /// <summary>
        /// Associates this model element with its owning descriptor.
        /// </summary>
        /// <param name="owner">owning descriptor</param>
        /// <param name="declSite">declaration site of this element</param>
        /// <param name="indexSpec">index specifier within declaration site (if the latter is an array)</param>
        void SetOwner(DescriptorBase owner, MemberInfo declSite, IndexSpec indexSpec);
    }

    [ContractClassFor(typeof(IContainmentImplementor))]
    abstract class ContainmentImplementorContractClass : IContainmentImplementor
    {
        public void SetOwner(DescriptorBase owner, MemberInfo declSite, IndexSpec indexSpec)
        {
            Contract.Requires(owner != null);
            Contract.Requires(indexSpec != null);
        }
    }

    /// <summary>
    /// Basic interface of all descriptors.
    /// </summary>
    [ContractClass(typeof(DescriptorContractClass))]
    public interface IDescriptor: IAttributed
    {
        /// <summary>
        /// Returns the owning descriptor.
        /// </summary>
        DescriptorBase Owner { get; }

        /// <summary>
        /// Returns the name of the described element.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Adds a subordinate instance descriptor.
        /// </summary>
        /// <param name="desc">descriptor to add</param>
        /// <param name="declSite">declaration site of descriptor</param>
        /// <param name="indexSpec">index specifier within declaration site (if the latter is an array)</param>
        void AddChild(InstanceDescriptor desc, FieldInfo declSite, IndexSpec indexSpec);

        /// <summary>
        /// Adds a subordinate descriptor.
        /// </summary>
        /// <param name="desc">descriptor to add</param>
        /// <param name="name">name of described element</param>
        void AddChild(DescriptorBase desc, string name);

        /// <summary>
        /// Returns all subordinate descriptors.
        /// </summary>
        IEnumerable<DescriptorBase> Children { get; }

        /// <summary>
        /// Retrieves this descriptor's reference to the given descriptor.
        /// </summary>
        /// <typeparam name="T">type of descriptor</typeparam>
        /// <param name="child">subordinate descriptor</param>
        /// <returns>this descriptor's reference to the specified descriptor, which will be equivalent in terms of the <c>Equals</c> method.</returns>
        T Canonicalize<T>(T child) where T : DescriptorBase;
    }

    [ContractClassFor(typeof(IDescriptor))]
    abstract class DescriptorContractClass : 
        IDescriptor
    {
        public DescriptorBase Owner
        {
            get { throw new NotImplementedException(); }
        }

        public string Name
        {
            get { throw new NotImplementedException(); }
        }

        public void AddChild(InstanceDescriptor desc, FieldInfo declSite, IndexSpec indexSpec)
        {
            Contract.Requires<ArgumentNullException>(desc != null);
            Contract.Requires<ArgumentNullException>(indexSpec != null);
        }

        public void AddChild(DescriptorBase desc, string name)
        {
            Contract.Requires<ArgumentNullException>(desc != null);
            Contract.Requires<ArgumentNullException>(name != null);
        }

        public IEnumerable<DescriptorBase> Children
        {
            get 
            {
                Contract.Ensures(Contract.Result<IEnumerable<DescriptorBase>>() != null);
                throw new NotImplementedException(); 
            }
        }

        public T Canonicalize<T>(T child) where T : DescriptorBase
        {
            Contract.Requires<ArgumentNullException>(child != null);
            Contract.Ensures(child.Equals(Contract.Result<T>()));
            throw new NotImplementedException();
        }

        public void AddAttribute(object attr)
        {
            throw new NotImplementedException();
        }

        public bool RemoveAttribute<T>()
        {
            throw new NotImplementedException();
        }

        public T QueryAttribute<T>()
        {
            throw new NotImplementedException();
        }

        public bool HasAttribute<T>()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> Attributes
        {
            get { throw new NotImplementedException(); }
        }

        public void CopyAttributesFrom(IAttributed other)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A document of SysDOM's integrated documentation system.
    /// </summary>
    public class Document
    {
        /// <summary>
        /// Name of the document
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Content of the document
        /// </summary>
        public object Content { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="name">name of the document</param>
        /// <param name="content">document content</param>
        public Document(string name, object content)
        {
            Name = name;
            Content = content;
        }
    }

    /// <summary>
    /// A container of multiple <c>Document</c>s. Instances of this class may be as properties attached
    /// to any descriptor and consitute some user-defined documentation.
    /// </summary>
    public class Documentation
    {
        /// <summary>
        /// Returns the list of documents which are included in this container.
        /// </summary>
        public List<Document> Documents { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public Documentation()
        {
            Documents = new List<Document>();
        }
    }

    /// <summary>
    /// Base implementation of any descriptor.
    /// </summary>
    public abstract class DescriptorBase :
        AttributedObject,
        IDescriptor
    {
        private Dictionary<DescriptorBase, DescriptorBase> _children = new Dictionary<DescriptorBase, DescriptorBase>();

        /// <summary>
        /// Owner of this descriptor.
        /// </summary>
        public virtual DescriptorBase Owner { get; internal set; }

        private string _name;

        /// <summary>
        /// Returns the name of the described element.
        /// </summary>
        public virtual string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public DescriptorBase()
        {
            _name = "";
        }

        public override string ToString()
        {
            return Name;
        }

        public virtual void AddChild(InstanceDescriptor desc, FieldInfo declSite, IndexSpec indexSpec)
        {
            desc.Index = indexSpec;
            if (declSite != null)
                AddChild(desc, declSite.Name);
            else
                AddChild(desc, desc.Name);
        }

        public virtual void AddChild(DescriptorBase desc, string name)
        {
            desc.Owner = this;
            desc._name = name;
            _children[desc] = desc;
        }

        /// <summary>
        /// Removes a subordinate descriptor.
        /// </summary>
        /// <param name="desc">descriptor to remove</param>
        public void RemoveChild(DescriptorBase desc)
        {
            _children.Remove(desc);
        }

        public IEnumerable<DescriptorBase> Children
        {
            get { return _children.Values; }
        }

        public T Canonicalize<T>(T child) where T : DescriptorBase
        {
            DescriptorBase clone;
            if (_children.TryGetValue(child, out clone))
            {
                return (T)clone;
            }
            else
            {
                AddChild(child, child.Name);
                return child;
            }
        }

        /// <summary>
        /// Returns the root descriptor.
        /// </summary>
        public DesignDescriptor GetDesign()
        {
            DescriptorBase cur = this;
            while (!(cur is DesignDescriptor))
                cur = cur.Owner;
            return (DesignDescriptor)cur;
        }

        public bool IsActive { get; internal set; }

        /// <summary>
        /// Retrieves the documentation container for this descriptor.
        /// </summary>
        public Documentation GetDocumentation()
        {
            Documentation doc;
            if (!HasAttribute<Documentation>())
            {
                doc = new Documentation();
                AddAttribute(doc);
            }
            else
            {
                doc = QueryAttribute<Documentation>();
            }
            return doc;
        }
    }

    /// <summary>
    /// This static class provides convenience methods for working with descriptors.
    /// </summary>
    public static class Descriptors
    {
        private static IEnumerable<T> SelectChildren<T>(this IDescriptor me) where T : IDescriptor
        {
            return from IDescriptor d in me.Children
                   where d is T
                   select (T)d;
        }

        private static IEnumerable<T> SelectChildrenOrdered<T>(this IDescriptor me)
            where T : DescriptorBase, IOrdered
        {
            return from DescriptorBase d in me.Children
                   where d is T
                   orderby ((T)d).Order
                   select (T)d;
        }

        /// <summary>
        /// Returns all type descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<TypeDescriptor> GetTypes(this IDescriptor me)
        {
            return SelectChildren<TypeDescriptor>(me);
        }

        /// <summary>
        /// Returns all field descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<FieldDescriptor> GetFields(this IDescriptor me)
        {
            return SelectChildren<FieldDescriptor>(me);
        }

        /// <summary>
        /// Returns all non-constant field descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<FieldDescriptor> GetVariables(this IDescriptor me)
        {
            return GetFields(me).Where(fd => !fd.IsConstant);
        }

        /// <summary>
        /// Returns all constant field descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<FieldDescriptor> GetConstants(this IDescriptor me)
        {
            return GetFields(me).Where(fd => fd.IsConstant);
        }

        /// <summary>
        /// Returns all sub-component descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<IComponentDescriptor> GetChildComponents(this IDescriptor me)
        {
            return SelectChildren<IComponentDescriptor>(me);
        }

        /// <summary>
        /// Returns all channel descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<ChannelDescriptor> GetChannels(this IDescriptor me)
        {
            return SelectChildren<ChannelDescriptor>(me);
        }

        /// <summary>
        /// Returns all signal descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<ISignalDescriptor> GetSignals(this IDescriptor me)
        {
            return SelectChildren<ISignalDescriptor>(me);
        }

        /// <summary>
        /// Returns all port descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<IPortDescriptor> GetPorts(this IDescriptor me)
        {
            return SelectChildren<IPortDescriptor>(me);
        }

        /// <summary>
        /// Returns all signal argument descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<SignalArgumentDescriptor> GetSignalArguments(this IDescriptor me)
        {
            return SelectChildren<SignalArgumentDescriptor>(me);
        }

        /// <summary>
        /// Returns all method descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<MethodDescriptor> GetMethods(this IDescriptor me)
        {
            return SelectChildren<MethodDescriptor>(me);
        }

        /// <summary>
        /// Returns all active method descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<MethodDescriptor> GetActiveMethods(this IDescriptor me)
        {
            return SelectChildren<MethodDescriptor>(me).Where(md => md.IsActive);
        }

        /// <summary>
        /// Returns all process descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<ProcessDescriptor> GetProcesses(this IDescriptor me)
        {
            return SelectChildren<ProcessDescriptor>(me);
        }

        /// <summary>
        /// Returns all argument descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<ArgumentDescriptor> GetArguments(this IDescriptor me)
        {
            return SelectChildrenOrdered<ArgumentDescriptor>(me);
        }

        /// <summary>
        /// Returns all package descriptors owned by <paramref name="me"/>.
        /// </summary>
        public static IEnumerable<PackageDescriptor> GetPackages(this IDescriptor me)
        {
            return SelectChildren<PackageDescriptor>(me);
        }

        /// <summary>
        /// Retrieves the descriptor of the process with name <paramref name="name"/> from <paramref name="me"/>.
        /// </summary>
        public static ProcessDescriptor FindProcess(this IDescriptor me, string name)
        {
            return GetProcesses(me).Where(p => p.Name == name).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the descriptor of the method with name <paramref name="name"/> from <paramref name="me"/>.
        /// </summary>
        public static MethodDescriptor FindMethod(this IDescriptor me, string name)
        {
            return GetMethods(me).Where(p => p.Name == name).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the descriptor of the method with reflection info <paramref name="method"/> from <paramref name="me"/>.
        /// </summary>
        public static MethodDescriptor FindMethod(this IDescriptor me, MethodBase method)
        {
            return GetMethods(me).Where(p => p.Method.Equals(method)).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the descriptor of the component with name <paramref name="name"/> from <paramref name="me"/>.
        /// </summary>
        public static IComponentDescriptor FindComponent(this IDescriptor me, string name)
        {
            return GetChildComponents(me).Where(c => c.Name == name).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the descriptor of the field with name <paramref name="name"/> from <paramref name="me"/>.
        /// </summary>
        public static FieldDescriptor FindField(this IDescriptor me, string name)
        {
            return GetFields(me).Where(f => f.Name == name).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the descriptor of the port with name <paramref name="name"/> from <paramref name="me"/>.
        /// </summary>
        public static IPortDescriptor FindPort(this IComponentDescriptor me, string name)
        {
            return me.GetPorts().Where(p => p.Name == name).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the descriptor of the signal with name <paramref name="name"/> from <paramref name="me"/>.
        /// </summary>
        public static ISignalDescriptor FindSignal(this IComponentDescriptor me, string name)
        {
            return me.GetSignals().Where(s => s.Name == name).FirstOrDefault();
        }

        /// <summary>
        /// Computes the fully-qualified name of <paramref name="me"/>.
        /// </summary>
        public static string GetFullName(this IDescriptor me)
        {
            string result = "";
            IDescriptor cur = me;
            do
            {
                if (result.Length > 0)
                    result = "." + result;
                result = cur.Name + result;
                cur = cur.Owner;
            } while (cur != null);
            return result;
        }
    }

    /// <summary>
    /// Base class for all descriptors which describe an instance of <c>IDescriptive</c>.
    /// </summary>
    public abstract class InstanceDescriptor :
        DescriptorBase
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="instance">object to describe</param>
        public InstanceDescriptor(IDescriptive instance)
        {
            Instance = instance;
            Index = IndexSpec.Empty;
        }

        /// <summary>
        /// The described object
        /// </summary>
        public IDescriptive Instance { get; private set; }

        private IndexSpec _index;

        /// <summary>
        /// Index specifier of described object inside its owning descriptor
        /// </summary>
        public IndexSpec Index
        {
            get { return _index; }
            internal set
            {
                Contract.Requires(value != null);
                _index = value;
            }
        }

        public override string Name
        {
            get
            {
                string name = base.Name;
                if ((name == null || name == "") && Owner != null)
                    name = Owner.Name;
                return name;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Name);
            if (Index != null && Index.Indices.Length > 0)
            {
                sb.Append("(");
                sb.Append(Index.ToString());
                sb.Append(")");
            }
            return sb.ToString();
        }

        public InstanceDescriptor RemoveIndex()
        {
            if (Index.Indices.Length == 0)
                return this;

            return (InstanceDescriptor)Owner;
        }
    }

    /// <summary>
    /// Typed interface for all instance descriptors.
    /// </summary>
    /// <typeparam name="ObjectType">type of described instance</typeparam>
    public interface IInstanceDescriptor<ObjectType> : IDescriptor
    {
        /// <summary>
        /// The described instance
        /// </summary>
        ObjectType Instance { get; }

        /// <summary>
        /// Returns a clone, but with empty index specifier.
        /// </summary>
        InstanceDescriptor RemoveIndex();
    }

    /// <summary>
    /// Interface for all objects which are described by a descriptor.
    /// </summary>
    public interface IDescriptive
    {
        /// <summary>
        /// The descriptor for this instance.
        /// </summary>
        DescriptorBase Descriptor { get; }
    }

    /// <summary>
    /// Typed interface for all objects which are described by a descriptor.
    /// </summary>
    public interface IDescriptive<DescType> :
        IDescriptive
    {
        /// <summary>
        /// The descriptor for this instance.
        /// </summary>
        new DescType Descriptor { get; }
    }

    /// <summary>
    /// Describes a field.
    /// </summary>
    public abstract class FieldDescriptor : DescriptorBase
    {
        /// <summary>
        /// Type descriptor of the field
        /// </summary>
        public TypeDescriptor Type { get; protected set; }

        /// <summary>
        /// Whether the type descriptor of the field had to be guessed because of incomplete information.
        /// </summary>
        public bool TypeIsGuessed { get; protected set; }

        /// <summary>
        /// Whether the field is constant.
        /// </summary>
        public bool IsConstant { get; internal set; }

        /// <summary>
        /// The constant field value.
        /// </summary>
        public object ConstantValue { get; internal set; }

        /// <summary>
        /// Returns <c>true</c> if the field is read by some process in context <paramref name="context"/>.
        /// </summary>
        public abstract bool IsReadInCurrentContext(DesignContext context);

        /// <summary>
        /// Returns <c>true</c> if the field is written by some process in context <paramref name="context"/>.
        /// </summary>
        public abstract bool IsWrittenInCurrentContext(DesignContext context);

        /// <summary>
        /// Constructs a new field descriptor.
        /// </summary>
        /// <param name="type">type descriptor of the field</param>
        internal FieldDescriptor(TypeDescriptor type)
        {
            Type = type;
        }

        internal void UpgradeType(TypeDescriptor type)
        {
            Contract.Requires(type != null);
            Contract.Requires(Type == null || Type.CILType.Equals(type.CILType));

            if (Type == null || !Type.IsComplete || TypeIsGuessed)
            {
                Type = type;
                ConstantValue = type.GetSampleInstance(ETypeCreationOptions.AnyObject);
                TypeIsGuessed = false;
            }
        }

        /// <summary>
        /// Returns the current field value.
        /// </summary>
        public abstract object Value { get; }

        /// <summary>
        /// Whether the described field is static.
        /// </summary>
        public abstract bool IsStatic { get; }
    }

    /// <summary>
    /// Describes a field which is represented by a field of the Common Language Infrastructure (CLI).
    /// </summary>
    public class CILFieldDescriptor : FieldDescriptor
    {
        /// <summary>
        /// The corresponding CLI field
        /// </summary>
        public FieldInfo Field { get; private set; }

        /// <summary>
        /// The object which instantiates the field
        /// </summary>
        public object Instance { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="field">CLI field information</param>
        /// <param name="instance">object which instantiated the field</param>
        public CILFieldDescriptor(FieldInfo field, object instance) :
            base(field.FieldType)
        {
            Contract.Requires(field != null);

            Field = field;
            Instance = instance;

            var attrs = field.GetCustomAttributes(false);
            foreach (var attr in attrs)
                AddAttribute(attr);

            if (field.IsStatic || instance != null)
            {
                try
                {
                    object curValue = field.GetValue(instance);
                    ConstantValue = curValue;
                    if (curValue != null)
                    {
                        Type = TypeDescriptor.GetTypeOf(curValue);
                        TypeIsGuessed = true;
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public override string Name { get { return Field.Name; } }

        public override string ToString()
        {
            return Field.Name + ": " + Type.ToString();
        }

        /// <summary>
        /// Two field descriptors are considered equal iff they belong to the same instance and same CLI field information.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is CILFieldDescriptor)
            {
                var fd = (CILFieldDescriptor)obj;
                return Field.Equals(fd.Field) && Instance == fd.Instance;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Field.GetHashCode() ^
                (Instance == null ? 0 : Instance.GetHashCode());
        }

        /// <summary>
        /// Returns the current value of the field with respect to the corresponding field instance.
        /// </summary>
        public override object Value
        {
            get { return Field.GetValue(Instance); }
        }

        /// <summary>
        /// Returns <c>true</c> iff the CLI field is static.
        /// </summary>
        public override bool IsStatic
        {
            get { return Field.IsStatic; }
        }

        /// <summary>
        /// Returns all processes which perform write accesses on the field.
        /// </summary>
        public IEnumerable<SystemSharp.Components.Process> DrivingProcesses
        {
            get { return _drivingProcesses.AsEnumerable(); }
        }

        /// <summary>
        /// Returns all processes which perform read accesses on the field.
        /// </summary>
        public IEnumerable<SystemSharp.Components.Process> ReadingProcesses
        {
            get { return _readingProcesses.AsEnumerable(); }
        }

        private HashSet<SystemSharp.Components.Process> _drivingProcesses = new HashSet<Components.Process>();
        private HashSet<SystemSharp.Components.Process> _readingProcesses = new HashSet<Components.Process>();

        internal void AddDriver(SystemSharp.Components.Process driver)
        {
            _drivingProcesses.Add(driver);
        }

        internal void AddReader(SystemSharp.Components.Process reader)
        {
            _readingProcesses.Add(reader);
        }

        public override bool IsReadInCurrentContext(DesignContext context)
        {
            return ReadingProcesses.Contains(context.CurrentProcess);
        }

        public override bool IsWrittenInCurrentContext(DesignContext context)
        {
            return DrivingProcesses.Contains(context.CurrentProcess);
        }
    }

    /// <summary>
    /// The abstract base class for describing a channel. Each channel specialization should provide its own
    /// descriptor class that inherits from this class.
    /// </summary>
    public abstract class ChannelDescriptor :
        InstanceDescriptor
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="instance">channel to describe</param>
        public ChannelDescriptor(IDescriptive instance) :
            base(instance)
        {
        }

        /// <summary>
        /// The channel instance this descriptor is bound to.
        /// </summary>
        public Channel BoundChannel { get; internal set; }
    }

    /// <summary>
    /// Represents the common properties of either a signal or a port.
    /// </summary>
    public interface ISignalOrPortDescriptor :
        IDescriptor
    {
        /// <summary>
        /// Returns the type descriptor of the inderlying (if applicable: bound) signal instance.
        /// </summary>
        TypeDescriptor InstanceType { get; }

        /// <summary>
        /// Returns the type descriptor of a data element being communicated by the described signal or port.
        /// </summary>
        TypeDescriptor ElementType { get; }

        /// <summary>
        /// Returns the initial value of the (if applicable: bound) signal.
        /// </summary>
        object InitialValue { get; }
    }

    /// <summary>
    /// Describes a signal.
    /// </summary>
    public interface ISignalDescriptor :
        ISignalOrPortDescriptor,
        IInstanceDescriptor<SignalBase>
    {
    }

    /// <summary>
    /// The data flow direction of a port
    /// </summary>
    public enum EPortDirection
    {
        In,
        Out,
        InOut
    };

    /// <summary>
    /// Describes a port.
    /// </summary>
    public interface IPortDescriptor : ISignalOrPortDescriptor
    {
        /// <summary>
        /// Returns the data flow direction of the described port.
        /// </summary>
        EPortDirection Direction { get; }

        /// <summary>
        /// Returns a usage hint of the described port.
        /// </summary>
        EPortUsage Usage { get; }

        /// <summary>
        /// Reserved for future use or deprecation... ;-)
        /// </summary>
        string Domain { get; }

        /// <summary>
        /// Returns the descriptor of the port-bound signal, if any.
        /// </summary>
        ISignalDescriptor BoundSignal { get; }
    }

    /// <summary>
    /// Describes a port.
    /// </summary>
    public class PortDescriptor :
        DescriptorBase,
        IPortDescriptor
    {
        /// <summary>
        /// Constructs a port descriptor instance.
        /// </summary>
        /// <param name="declSite">CLI property information on the port</param>
        /// <param name="boundSignal">signal descriptor which is bound to the port</param>
        /// <param name="elementType">type descriptor of data which is communicated by the port</param>
        /// <param name="direction">data flow direction of the port</param>
        public PortDescriptor(
            PropertyInfo declSite,
            SignalDescriptor boundSignal,
            TypeDescriptor elementType,
            EPortDirection direction)
        {
            Contract.Requires<ArgumentNullException>(declSite != null, "declSite");
            Contract.Requires<ArgumentNullException>(boundSignal != null, "boundSignal");
            Contract.Requires<ArgumentNullException>(elementType != null, "elementType");

            DeclarationSite = declSite;
            BoundSignal = boundSignal;
            ElementType = elementType;
            Direction = direction;
            PortUsage usageAttr = DeclarationSite.GetCustomOrInjectedAttribute<PortUsage>();
            if (usageAttr != null)
            {
                Usage = usageAttr.Usage;
                Domain = usageAttr.Domain;
            }
        }

        public override string Name { get { return DeclarationSite.Name; } }

        /// <summary>
        /// CLI property information on the port.
        /// </summary>
        public PropertyInfo DeclarationSite { get; private set; }

        /// <summary>
        /// Type descriptor of data which is communicated by the port.
        /// </summary>
        public TypeDescriptor ElementType { get; private set; }

        /// <summary>
        /// Data flow direction of the port.
        /// </summary>
        public EPortDirection Direction { get; private set; }

        /// <summary>
        /// Usage hint of the port.
        /// </summary>
        public EPortUsage Usage { get; private set; }

        /// <summary>
        /// Reserved for future use or deprecation. ;-)
        /// </summary>
        public string Domain { get; private set; }

        /// <summary>
        /// Descriptor of signal which is bound to the port.
        /// </summary>
        public ISignalDescriptor BoundSignal { get; private set; }

        /// <summary>
        /// Returns the type descriptor of the bound signal instance.
        /// </summary>
        public TypeDescriptor InstanceType
        {
            get
            {
                return BoundSignal == null ?
                    (TypeDescriptor)typeof(Signal<>).MakeGenericType(ElementType.CILType) :
                    BoundSignal.InstanceType;
            }
        }

        /// <summary>
        /// Returns the initial signal value.
        /// </summary>
        public object InitialValue
        {
            get { return BoundSignal.InitialValue; }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Name);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Describes a signal.
    /// </summary>
    public class SignalDescriptor :
        ChannelDescriptor,
        ISignalDescriptor
    {
        /// <summary>
        /// Constructs a signal descriptor instance.
        /// </summary>
        /// <param name="instance">described signal instance</param>
        /// <param name="elementType"></param>
        public SignalDescriptor(
            SignalBase instance,
            TypeDescriptor elementType) :
            base(instance)
        {
            Contract.Requires<ArgumentNullException>(instance != null, "instance");
            Contract.Requires<ArgumentNullException>(elementType != null, "elementType");

            ElementType = elementType;
            Index = elementType.Index;
        }

        public TypeDescriptor ElementType { get; private set; }

        public TypeDescriptor InstanceType
        {
            get { return TypeDescriptor.GetTypeOf(SignalInstance); }
        }

        /// <summary>
        /// All port descriptors which are bound to the described signal.
        /// </summary>
        public PortDescriptor[] BoundPorts { get; internal set; }

        public SignalBase SignalInstance
        {
            get { return Instance; }
        }

        public new SignalBase Instance
        {
            get { return (SignalBase)base.Instance; }
        }

        public object InitialValue
        {
            get { return Instance.InitialValueObject; }
        }

        /// <summary>
        /// Two signal descriptors are considered equal iff they refer to the same signal instance.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is SignalDescriptor)
            {
                SignalDescriptor other = (SignalDescriptor)obj;
                return Instance == other.Instance;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            if (Instance != null)
                return Instance.GetHashCode();
            else
                return base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    /// <summary>
    /// Anything which relies on a particular ordering among similar instances inside the same descriptor.
    /// </summary>
    public interface IOrdered
    {
        /// <summary>
        /// Returns the 0-based order index.
        /// </summary>
        int Order { get; }
    }

    /// <summary>
    /// Describes a method argument.
    /// </summary>
    public class ArgumentDescriptor :
        DescriptorBase,
        IOrdered
    {
        /// <summary>
        /// Logical data direction of the argument
        /// </summary>
        public enum EArgDirection
        {
            In, Out, InOut
        }

        private IStorableLiteral _arg;
        private EArgDirection _dir;
        private EVariability _variability;
        private int _order;

        /// <summary>
        /// Constructs an argument descriptor instance.
        /// </summary>
        /// <param name="arg">argument literal</param>
        /// <param name="dir">logical data direction of argument</param>
        /// <param name="variability">variability classification of argument</param>
        /// <param name="order">0-based position of argument inside the argument list</param>
        public ArgumentDescriptor(IStorableLiteral arg, EArgDirection dir, EVariability variability, int order)
        {
            Contract.Requires<ArgumentNullException>(arg != null, "arg");

            _arg = arg;
            _dir = dir;
            _variability = variability;
            _order = order;
        }

        public override string Name
        {
            get { return _arg.Name; }
        }

        /// <summary>
        /// Returns the used argument literal.
        /// </summary>
        public IStorableLiteral Argument
        {
            get { return _arg; }
        }

        /// <summary>
        /// Returns the logical data-flow direction of the argument.
        /// </summary>
        public EArgDirection Direction
        {
            get { return _dir; }
        }

        /// <summary>
        /// Returns a sample value of the argument.
        /// </summary>
        public object Sample
        {
            get { return _arg.Type.GetSampleInstance(ETypeCreationOptions.AnyObject); }
        }

        /// <summary>
        /// Returns the variability classification of the argument.
        /// </summary>
        public EVariability Variability
        {
            get { return _variability; }
        }

        /// <summary>
        /// Returns the 0-based position of the argument inside the argument list.
        /// </summary>
        public int Order
        {
            get { return _order; }
        }

        public override string ToString()
        {
            return Direction + " " + Argument.Type + " " + Argument.Name;
        }

        class ArgumentTypeAndDirectionComparer : IEqualityComparer<ArgumentDescriptor>
        {
            #region IEqualityComparer<ArgumentDescriptor> Member

            public bool Equals(ArgumentDescriptor x, ArgumentDescriptor y)
            {
                if (x == null && y == null)
                    return true;
                if (x == null || y == null)
                    return false;
                return x.Argument.Type.Equals(y.Argument.Type) &&
                    x.Direction == y.Direction &&
                    x.Variability == y.Variability &&
                    (x.Variability != EVariability.Constant || object.Equals(x.Sample, y.Sample));
            }

            public int GetHashCode(ArgumentDescriptor obj)
            {
                return
                    obj.Argument.Type.GetHashCode() ^
                    obj.Direction.GetHashCode() ^
                    obj.Variability.GetHashCode() ^
                    (obj.Variability == EVariability.Constant ?
                    (obj.Sample == null ? 0 : obj.Sample.GetHashCode()) : 0);
            }

            #endregion
        }

        /// <summary>
        /// An equality comparer which compares argument descriptors based on their type and direction
        /// (i.e. not on name and not on position).
        /// </summary>
        public static readonly IEqualityComparer<ArgumentDescriptor> TypeAndDirectionComparer =
            new ArgumentTypeAndDirectionComparer();
    }

    /// <summary>
    /// Describes a method argument which is a signal.
    /// </summary>
    public class SignalArgumentDescriptor :
        ArgumentDescriptor,
        ISignalOrPortDescriptor
    {
        private EArgDirection _flowDir;

        /// <summary>
        /// Constructs a signal argument descriptor instance.
        /// </summary>
        /// <param name="sref">reference to signal which is passed to the method</param>
        /// <param name="dir">logical data-flow direction of the argument (usually <c>EArgDirection.In</c>)</param>
        /// <param name="flowDir">logical data-flow direction of the signal access (depending on the port direction if a port is accessed)</param>
        /// <param name="variability">variability classification of the argument</param>
        /// <param name="order">positiion of the argument inside the argument list</param>
        public SignalArgumentDescriptor(SignalRef sref, EArgDirection dir, EArgDirection flowDir, EVariability variability, int order) :
            base(sref, dir, variability, order)
        {
            Contract.Requires<ArgumentNullException>(sref != null, "sref");
            Contract.Requires<ArgumentOutOfRangeException>(sref.Prop == SignalRef.EReferencedProperty.Instance, 
                "Signal reference sref must reference signal instance.");

            _flowDir = flowDir;
        }

        /// <summary>
        /// Returns the type descriptor of the data which is communicated by the signal.
        /// </summary>
        public TypeDescriptor ElementType
        {
            get { return SignalInstance.ElementType; }
        }

        /// <summary>
        /// Returns a sample instance of the described argument.
        /// </summary>
        public SignalBase SignalInstance
        {
            get { return (SignalBase)Sample; }
        }

        /// <summary>
        /// Returns the type descriptor of a signal instance passed by the described argument.
        /// </summary>
        public TypeDescriptor InstanceType
        {
            get { return Argument.Type; }
        }

        /// <summary>
        /// Returns the data-flow direction of the signal access.
        /// </summary>
        public EArgDirection FlowDirection
        {
            get { return _flowDir; }
        }

        /// <summary>
        /// Returns <c>IndexSpec.Empty</c>.
        /// </summary>
        public IndexSpec Index
        {
            get { return IndexSpec.Empty; }
        }

        /// <summary>
        /// Returns <c>null</c>.
        /// </summary>
        public object InitialValue
        {
            get { return null; }
        }
    }

    /// <summary>
    /// This abstract class describes code in the CLI and SysDOM domain.
    /// </summary>
    public abstract class CodeDescriptor :
        DescriptorBase
    {
        /// <summary>
        /// Describes a possible constraint on the value range of a variable.
        /// </summary>
        public class ValueRangeConstraint
        {
            /// <summary>
            /// Whether <c>MinValue</c> and <c>MaxValue</c> are valid.
            /// </summary>
            public bool IsConstrained { get; private set; }

            /// <summary>
            /// The minimum value.
            /// </summary>
            public long MinValue { get; private set; }

            /// <summary>
            /// The maximum value.
            /// </summary>
            public long MaxValue { get; private set; }

            private ValueRangeConstraint()
            {
                IsConstrained = false;
            }

            /// <summary>
            /// Constructs a new constraint.
            /// </summary>
            /// <param name="minValue">minimum value</param>
            /// <param name="maxValue">maximum value</param>
            public ValueRangeConstraint(long minValue, long maxValue)
            {
                MinValue = minValue;
                MaxValue = maxValue;
                IsConstrained = true;
            }

            /// <summary>
            /// The one and only instance of "no constraint".
            /// </summary>
            public static readonly ValueRangeConstraint Unconstrained = new ValueRangeConstraint();
        }

        private HashSet<ISignalOrPortDescriptor> _drivenSignals = new HashSet<ISignalOrPortDescriptor>();

        /// <summary>
        /// CLI information on the user-visible async method, <c>null</c> if the described method is not async.
        /// </summary>
        public MethodInfo AsyncMethod { get; private set; }

        /// <summary>
        /// CLI information on the actual method implementation.
        /// </summary>
        public MethodBase Method { get; private set; }

        /// <summary>
        /// SysDOM description of the CLI method, possibly with optimizations with respect to the method's call context.
        /// </summary>
        public Function Implementation { get; internal set; }

        /// <summary>
        /// SysDOM description of the CLI method, without any optimization with respect to the method's call context.
        /// </summary>
        public Function GenuineImplementation { get; internal set; }

        /// <summary>
        /// Range constraints of the method's local variables.
        /// </summary>
        public ValueRangeConstraint[] ValueRangeConstraints { get; internal set; }

        /// <summary>
        /// Constructs a code descriptor instance.
        /// </summary>
        /// <param name="method">CLI information on method</param>
        public CodeDescriptor(MethodBase method)
        {
            if (method.IsAsync())
            {
                AsyncMethod = (MethodInfo)method;
                Method = method.UnwrapEntryPoint();
            }
            else
            {
                Method = method;
            }
            _name = method.Name;
        }

        /// <summary>
        /// Constructs a code descriptor instance without CLI information.
        /// </summary>
        /// <param name="name">name of the process or method</param>
        public CodeDescriptor(string name)
        {
            _name = name;
        }

        private string _name;
        public override string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Returns all signals and ports which are driven by the described code.
        /// </summary>
        public IEnumerable<ISignalOrPortDescriptor> DrivenSignals
        {
            get
            {
                Contract.Assume(_drivenSignals != null);

                return new ReadOnlyCollection<ISignalOrPortDescriptor>(_drivenSignals.ToList());
            }
        }

        /// <summary>
        /// Adds a port or signal descriptor to the list of driven signals.
        /// </summary>
        public void AddDrivenSignal(ISignalOrPortDescriptor signal)
        {
            _drivenSignals.Add(signal);
        }

        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Two code descriptors are considered equal iff the have the same owner and the same name.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is CodeDescriptor)
            {
                CodeDescriptor cd = (CodeDescriptor)obj;
                return Owner == cd.Owner &&
                    Name.Equals(cd.Name);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

    /// <summary>
    /// Describes a process.
    /// </summary>
    public class ProcessDescriptor : CodeDescriptor
    {
        /// <summary>
        /// Constructs a process descriptor instance, based on an existing process.
        /// </summary>
        /// <param name="method">CLI information on the method which implements the process</param>
        /// <param name="instance">process instance</param>
        public ProcessDescriptor(MethodBase method, SystemSharp.Components.Process instance) :
            base(method)
        {
            Contract.Requires<ArgumentNullException>(method != null, "method");
            Contract.Requires<ArgumentNullException>(instance != null, "instance");

            Instance = instance;
            Kind = instance.Kind;
        }

        /// <summary>
        /// Constructs a process descriptor instance, without requiring an underlying existing process.
        /// </summary>
        /// <param name="name">name of the process</param>
        public ProcessDescriptor(string name) :
            base(name)
        {
        }

        private ISignalOrPortDescriptor[] _sensitivity;

        /// <summary>
        /// The sensitivity list of the process.
        /// </summary>
        public ISignalOrPortDescriptor[] Sensitivity
        {
            get { return _sensitivity; }
            internal set 
            {
                Contract.Requires<ArgumentException>(value != null);
                _sensitivity = value.Select(s => s.GetUnindexedContainer()).ToArray(); 
            }
        }

        /// <summary>
        /// Kind of described process.
        /// </summary>
        public SystemSharp.Components.Process.EProcessKind Kind { get; set; }

        /// <summary>
        /// Instance of described process.
        /// </summary>
        public SystemSharp.Components.Process Instance { get; private set; }

        /// <summary>
        /// Two process descriptors are considered equal iff they have the same owner and the same name.
        /// </summary>
        public override bool Equals(object obj)
        {
            ProcessDescriptor pd = obj as ProcessDescriptor;
            if (pd == null)
                return false;
            return Name.Equals(pd.Name) &&
                Owner == pd.Owner;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Owner.GetHashCode();
        }
    }

    /// <summary>
    /// Describes a method.
    /// </summary>
    public class MethodDescriptor : CodeDescriptor
    {
        /// <summary>
        /// Constructs a method descriptor instance.
        /// </summary>
        /// <param name="method">CLI information on described method</param>
        /// <param name="argValueSamples">sample argument values</param>
        /// <param name="argVariabilities">variability classification of arguments</param>
        public MethodDescriptor(
            MethodBase method,
            object[] argValueSamples,
            EVariability[] argVariabilities) :
            base(method)
        {
            Contract.Requires<ArgumentNullException>(method != null, "method");
            Contract.Requires<ArgumentNullException>(argValueSamples != null, "argValueSamples");
            Contract.Requires<ArgumentNullException>(argVariabilities != null, "argVariabilities");
            Contract.Requires<ArgumentException>(argVariabilities.Length == method.GetParameters().Length, 
                "argVariabilities must contain exactly as many elements as there are method parameters.");

            Debug.Assert(argValueSamples.All(s => s != null));

            ArgValueSamples = argValueSamples;
            ArgVariabilities = argVariabilities;
            InitArguments();
        }

        /// <summary>
        /// Returns a descriptor of the method return type.
        /// </summary>
        public TypeDescriptor ReturnType
        {
            get
            {
                if (ReturnValueSample != null)
                {
                    return TypeDescriptor.GetTypeOf(ReturnValueSample);
                }
                else
                {
                    Type returnType;
                    Method.IsFunction(out returnType);
                    return TypeDescriptor.MakeType(returnType);
                }
            }
        }

        /// <summary>
        /// Sample instances of the method arguments.
        /// </summary>
        public object[] ArgValueSamples { get; private set; }

        /// <summary>
        /// Variability classifications of the method arguments.
        /// </summary>
        public EVariability[] ArgVariabilities { get; private set; }

        /// <summary>
        /// Sample instance of method return value.
        /// </summary>
        public object ReturnValueSample { get; internal set; }

        /// <summary>
        /// Process which calls the described method.
        /// </summary>
        public ProcessDescriptor CallingProcess { get; internal set; }

        private void InitArguments()
        {
            ParameterInfo[] args = Method.GetParameters();
            foreach (ParameterInfo arg in args)
            {
                ArgumentDescriptor.EArgDirection dir =
                    ArgumentDescriptor.EArgDirection.In;
                TypeDescriptor argType = arg.ParameterType;
                bool isByRef = false;
                if (argType.IsByRef)
                {
                    if (arg.IsOut)
                        dir = ArgumentDescriptor.EArgDirection.Out;
                    else
                        dir = ArgumentDescriptor.EArgDirection.InOut;
                    isByRef = true;
                }
                if (ArgValueSamples[arg.Position] != null)
                {
                    argType = TypeDescriptor.GetTypeOf(ArgValueSamples[arg.Position]);
                    if (isByRef)
                        argType = argType.AsByRefType();
                }

                IStorableLiteral argv;

                RewriteArgumentDeclaration rad =
                    (RewriteArgumentDeclaration)arg.ParameterType.GetCustomOrInjectedAttribute(
                        typeof(RewriteArgumentDeclaration));
                if (rad != null)
                {
                    argv = rad.ImplementDeclaration(
                        this,
                        ArgValueSamples[arg.Position],
                        arg);
                }
                else
                {
                    argv = new Variable(argType)
                    {
                        Name = arg.Name
                    };
                    ArgumentDescriptor argd = new ArgumentDescriptor(
                        argv,
                        dir,
                        ArgVariabilities[arg.Position],
                        arg.Position);
                    AddChild(argd, argv.Name);
                }
            }
        }

        /// <summary>
        /// Two method descriptors are considered equal iff the following conditions are fulfilled:
        /// 1.: Both have the same owner. 2.: Both describe the same CLI method. 3.: The type descriptors of their
        /// arguments are element-wise equal.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is MethodDescriptor)
            {
                MethodDescriptor other = (MethodDescriptor)obj;

                if (Owner != other.Owner)
                    return false;
                if (!Method.Equals(other.Method))
                    return false;

                return this.GetArguments()
                    .SequenceEqual(
                        other.GetArguments(),
                        ArgumentDescriptor.TypeAndDirectionComparer);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int hash = Owner.GetHashCode();
            hash ^= Method.GetHashCode();
            hash ^= this.GetArguments().Aggregate(hash, (h, a) =>
                ((h << 1) | (h >> 31)) ^
                ArgumentDescriptor.TypeAndDirectionComparer.GetHashCode(a));
            return hash;
        }
    }

    static class IntToZeroBasedUpRangeConverter
    {
        public static Range ConvertToRange(int arg)
        {
            return new Range(0, arg - 1, EDimDirection.To);
        }
    }

    static class IntToZeroBasedDownRangeConverter
    {
        public static Range ConvertToRange(int arg)
        {
            return new Range(arg - 1, 0, EDimDirection.Downto);
        }
    }

    /// <summary>
    /// Declares an instance member of a data type as a type parameter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    class TypeParameter : Attribute
    {
        public Type RangeConverter { get; private set; }

        public TypeParameter(Type rangeConverter)
        {
            RangeConverter = rangeConverter;
        }
    }

    /// <summary>
    /// This interface describes the common properties of packages and components.
    /// </summary>
    public interface IPackageOrComponentDescriptor
    {
        /// <summary>
        /// Adds a package to the dependencies of the described element.
        /// </summary>
        void AddDependency(PackageDescriptor pd);

        /// <summary>
        /// Returns all packages the described package or component depends on.
        /// </summary>
        IEnumerable<PackageDescriptor> Dependencies { get; }

        /// <summary>
        /// Returns the position of the descibed package or component with respect to the required compilation order.
        /// </summary>
        int DependencyOrder { get; }

        /// <summary>
        /// Returns the library name of the described element.
        /// </summary>
        string Library { get; }
    }

    interface IDependencyOrdered
    {
        int DependencyOrder { get; set; }
    }

    /// <summary>
    /// Base interface for component descriptors.
    /// </summary>
    public interface IComponentDescriptor :
        IDescriptor,
        IPackageOrComponentDescriptor
    {
    }

    class PackageOrComponentDescriptor :
        IPackageOrComponentDescriptor,
        IDependencyOrdered
    {
        private HashSet<PackageDescriptor> _dependencies = new HashSet<PackageDescriptor>();

        public PackageOrComponentDescriptor()
        {
        }

        public void AddDependency(PackageDescriptor pkg)
        {
            if (pkg == null)
                throw new ArgumentException();

            _dependencies.Add(pkg);
        }

        public IEnumerable<PackageDescriptor> Dependencies
        {
            get
            {
                return _dependencies.Where(pd => !pd.IsEmpty);
            }
        }

        public int DependencyOrder { get; internal set; }

        int IDependencyOrdered.DependencyOrder
        {
            get { return DependencyOrder; }
            set { DependencyOrder = value; }
        }

        public string Library { get; set; }
    }

    /// <summary>
    /// Describes a package.
    /// </summary>
    public class PackageDescriptor :
        DescriptorBase,
        IPackageOrComponentDescriptor,
        IDependencyOrdered
    {
        private PackageOrComponentDescriptor _container = new PackageOrComponentDescriptor();

        /// <summary>
        /// Name of described package.
        /// </summary>
        public string PackageName { get; private set; }

        /// <summary>
        /// Constructs a package descriptor instance.
        /// </summary>
        /// <param name="packageName">name of described package</param>
        public PackageDescriptor(string packageName)
        {
            Contract.Requires<ArgumentException>(packageName != null && packageName != "", "packageName");

            PackageName = packageName;
        }

        public override string Name
        {
            get { return PackageName; }
        }

        /// <summary>
        /// Determines whether the package is empty, that is if does not contain any method, constant or type.
        /// </summary>
        public bool IsEmpty
        {
            get { return Children.Count() == 0; }
        }

        public override string ToString()
        {
            return PackageName;
        }

        /// <summary>
        /// Two packages are considered equal iff they have the same name.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is PackageDescriptor)
            {
                PackageDescriptor pd = (PackageDescriptor)obj;
                return PackageName == pd.PackageName;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return PackageName.GetHashCode();
        }

        /// <summary>
        /// Adds another package on which the described package depends.
        /// </summary>
        public void AddDependency(PackageDescriptor pd)
        {
            if (pd == this)
                throw new ArgumentException("Cyclic dependency");

            _container.AddDependency(pd);
        }

        /// <summary>
        /// Returns all packages on which the described package depends.
        /// </summary>
        public IEnumerable<PackageDescriptor> Dependencies
        {
            get { return _container.Dependencies; }
        }

        /// <summary>
        /// Position at which the described package should be compiled with respect to other packages.
        /// </summary>
        public int DependencyOrder { get; internal set; }

        int IDependencyOrdered.DependencyOrder
        {
            get { return DependencyOrder; }
            set { DependencyOrder = value; }
        }

        /// <summary>
        /// Adds a descriptor to be logically contained inside the described package.
        /// </summary>
        /// <param name="desc">descriptor to be added</param>
        /// <param name="name">name of the element inside this package</param>
        public override void AddChild(DescriptorBase desc, string name)
        {
            var tdesc = desc as TypeDescriptor;
            Debug.Assert(tdesc == null || !tdesc.HasIntrinsicTypeOverride);

            base.AddChild(desc, name);
        }

        /// <summary>
        /// Gets or sets the library name of this package.
        /// </summary>
        public string Library { get; set; }
    }

    /*
    [Flags]
    public enum EComponentCoverage
    {
        Interface,
        Implementation
    }
    */

    /// <summary>
    /// Describes a component.
    /// </summary>
    public class ComponentDescriptor :
        InstanceDescriptor,
        IInstanceDescriptor<Component>,
        IPackageOrComponentDescriptor,
        IComponentDescriptor,
        IDependencyOrdered
    {
        private PackageOrComponentDescriptor _container = new PackageOrComponentDescriptor();

        /// <summary>
        /// The package in which the described component resides.
        /// </summary>
        public PackageDescriptor Package { get; internal set; }

        /// <summary>
        /// Constructs a component descriptor instance.
        /// </summary>
        /// <param name="instance">component instance</param>
        public ComponentDescriptor(Component instance) :
            base(instance)
        {
        }

        /// <summary>
        /// Gets or sets a flag which tells whether the component behavior is implemented outside the SysDOM scope.
        /// </summary>
        internal bool HasForeignImplementation { get; set; }

        /// <summary>
        /// Returns the instance of the described component.
        /// </summary>
        public new Component Instance
        {
            get { return (Component)base.Instance; }
        }

        /// <summary>
        /// Make the described component dependent on the given package.
        /// </summary>
        public void AddDependency(PackageDescriptor pd)
        {
            _container.AddDependency(pd);
        }

        /// <summary>
        /// Returns all packages the described component depends on.
        /// </summary>
        public IEnumerable<PackageDescriptor> Dependencies
        {
            get
            {
                return this.GetChildComponents()
                    .SelectMany(cc => ((IPackageOrComponentDescriptor)cc).Dependencies)
                    .Concat(_container.Dependencies)
                    .Distinct();
            }
        }

        //public EComponentCoverage Coverage { get; internal set; }
        //public string ImplementationDomain { get; internal set; }

        /// <summary>
        /// The position at which the component should be compiled with respect to the global dependencies between
        /// packages and components.
        /// </summary>
        public int DependencyOrder { get; internal set; }

        int IDependencyOrdered.DependencyOrder
        {
            get { return DependencyOrder; }
            set { DependencyOrder = value; }
        }

        public string Library { get; set; }
    }

    /// <summary>
    /// Describes the overall design. This is the root component of the design hierarchy.
    /// </summary>
    public class DesignDescriptor : DescriptorBase
    {
        /// <summary>
        /// The associated design context.
        /// </summary>
        public DesignContext Context { get; private set; }

        /// <summary>
        /// The type library of the design.
        /// </summary>
        public TypeLibrary TypeLib { get; private set; }

        internal DesignDescriptor(DesignContext design)
        {
            Context = design;
            TypeLib = new TypeLibrary(this);
        }

        /// <summary>
        /// Constructs a design descriptor instance.
        /// </summary>
        public DesignDescriptor()
        {
            TypeLib = new TypeLibrary(this);
        }

        public override string Name
        {
            get { return "Design"; }
        }

        private AssemblyBuilder _asmBuilder;

        /// <summary>
        /// Returns an assembly builder for dynamically-created behavior.
        /// </summary>
        public AssemblyBuilder AsmBuilder
        {
            get
            {
                if (_asmBuilder == null)
                {
                    AssemblyName asmName = new AssemblyName();
                    asmName.Name = "ReflectiveDesign";
                    _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
                }
                return _asmBuilder;
            }
        }

        private ModuleBuilder _modBuilder;

        /// <summary>
        /// Returns a module builder for dynamically-created behavior.
        /// </summary>
        private ModuleBuilder ModBuilder
        {
            get
            {
                if (_modBuilder == null)
                {
                    _modBuilder = AsmBuilder.DefineDynamicModule("ReflectiveDesignModule");
                }
                return _modBuilder;
            }
        }

        private Dictionary<HashableSequence<string>, TypeDescriptor> _enumLookup;
        private Dictionary<HashableSequence<string>, TypeDescriptor> EnumLookup
        {
            get
            {
                if (_enumLookup == null)
                    _enumLookup = new Dictionary<HashableSequence<string>, TypeDescriptor>();
                return _enumLookup;
            }
        }

        /// <summary>
        /// Creates an enumeration type for a given set of literals. If an enumeration with the specified literal names
        /// already exists, the existing one is returned.
        /// </summary>
        /// <param name="name">desired name of the enumeration type</param>
        /// <param name="fieldNames">enumeration literals</param>
        public TypeDescriptor CreateEnum(string name, IEnumerable<string> fieldNames)
        {
            var fieldSeq = new HashableSequence<string>(fieldNames);
            TypeDescriptor existing;
            if (EnumLookup.TryGetValue(fieldSeq, out existing))
                return existing;

            EnumBuilder tbe = null;
            try
            {
                tbe = ModBuilder.DefineEnum(name, TypeAttributes.Public, typeof(int));
                
            }
            catch (ArgumentException)
            {
                // assume existing type name
                int count = 0;
                while (true)
                {
                    string mname = name + "_" + count;
                    try
                    {
                        tbe = ModBuilder.DefineEnum(mname, TypeAttributes.Public, typeof(int));
                        break;
                    }
                    catch (ArgumentException)
                    {
                        ++count;
                    }
                }
            }
            int i = 0;
            foreach (string fieldName in fieldNames)
            {
                FieldBuilder fb = tbe.DefineLiteral(fieldName, i);
                fb.SetConstant(i);
                ++i;
            }
            Type te = tbe.CreateType();
            TypeDescriptor td = TypeDescriptor.MakeType(te);
            EnumLookup.Add(fieldSeq, td);
            return td;
        }

        /// <summary>
        /// Creates a new component inside the design.
        /// </summary>
        /// <param name="name">component name</param>
        public ComponentBuilder CreateComponent(string name)
        {
            return new ComponentBuilder(name);
        }
    }

    /// <summary>
    /// This static class provides convenience methods to work with descriptors.
    /// </summary>
    public static class DescriptorExtensions
    {
        /// <summary>
        /// Returns all sub-components which are directly or indirectly part of the given component descriptor.
        /// </summary>
        public static IEnumerable<IComponentDescriptor> GetAllAncestors(this IComponentDescriptor cd)
        {
            HashSet<IComponentDescriptor> result = new HashSet<IComponentDescriptor>();
            Queue<IComponentDescriptor> q = new Queue<IComponentDescriptor>();
            q.Enqueue(cd);
            while (q.Any())
            {
                IComponentDescriptor cur = q.Dequeue();
                if (!result.Add(cur))
                    continue;
                foreach (IComponentDescriptor child in cur.GetChildComponents())
                    q.Enqueue(child);
            }
            return result;
        }

        /// <summary>
        /// Returns the descriptor itself if it describes a signal, or the signal it is bound to if it describes a port.
        /// </summary>
        public static ISignalOrPortDescriptor GetBoundSignal(this ISignalOrPortDescriptor sd)
        {
            var pd = sd as IPortDescriptor;
            var sad = sd as SignalArgumentDescriptor;
            if (pd != null)
                return pd.BoundSignal;
            else if (sad != null)
                return sad.SignalInstance.Descriptor;
            else
                return sd;
        }

        /// <summary>
        /// Removes any index constraints from the described signal or port.
        /// </summary>
        /// <param name="sd">a possibly indexed signal or port descriptor</param>
        /// <param name="accIndex">out parameter to receive the index constraints of <paramref name="sd"/></param>
        /// <returns>the root signal or port descriptor, i.e. without any index</returns>
        public static ISignalOrPortDescriptor GetUnindexedContainer(this ISignalOrPortDescriptor sd, out IndexSpec accIndex)
        {
            var stk = new Stack<SignalDescriptor>();
            var sig = sd as SignalDescriptor;
            while (sig != null)
            {
                stk.Push(sig);
                sig = sig.Owner as SignalDescriptor;
            }
            bool first = true;
            if (stk.Count > 0)
            {
                var cur = stk.Pop();
                sd = cur;
                var idx = cur.Index;
                if (first)
                    idx = idx.BaseAtZero();
                else
                    first = false;
                while (stk.Count > 0)
                    idx = stk.Pop().Index.ApplyTo(idx);
                accIndex = idx;
            }
            else
            {
                //accIndex = sd.ElementType.Index.BaseAtZero();
                accIndex = IndexSpec.Empty;
            }
            return sd;
        }

        /// <summary>
        /// Removes any index constraints from the described signal or port.
        /// </summary>
        /// <param name="sd">a possibly indexed signal or port descriptor</param>
        /// <returns>the root signal or port descriptor, i.e. without any index</returns>
        public static ISignalOrPortDescriptor GetUnindexedContainer(this ISignalOrPortDescriptor sd)
        {
            IndexSpec dummy;
            return GetUnindexedContainer(sd, out dummy);
        }

        /// <summary>
        /// Represents the signal or port descriptor as signal reference.
        /// </summary>
        /// <param name="sd">signal or port descriptor</param>
        /// <param name="prop">property to reference</param>
        public static SignalRef AsSignalRef(this ISignalOrPortDescriptor sd, SignalRef.EReferencedProperty prop)
        {
            return new SignalRef(sd, prop);
        }

        /// <summary>
        /// Converts a signal reference with respect to a surrounding component.
        /// </summary>
        /// <param name="sref">signal reference</param>
        /// <param name="cd">surrounding component</param>
        /// <returns>a signal reference which is suitable for the surrounding component</returns>
        public static SignalRef RelateToComponent(this SignalRef sref, IComponentDescriptor cd)
        {
            var srefNorm = sref.AssimilateIndices();

            srefNorm = new SignalRef(
                srefNorm.Desc.GetBoundSignal(),
                srefNorm.Prop,
                srefNorm.Indices,
                srefNorm.IndexSample,
                srefNorm.IsStaticIndex);

            srefNorm = srefNorm.AssimilateIndices();

            if (cd.GetSignals().Contains(srefNorm.Desc))
                return srefNorm;

            if (srefNorm.IsStaticIndex)
            {
                foreach (var port in cd.GetPorts())
                {
                    var portSigRef = port.GetBoundSignal()
                        .AsSignalRef(sref.Prop)
                        .AssimilateIndices();

                    if (portSigRef.Desc.Equals(srefNorm.Desc))
                    {
                        try
                        {
                            var projIndex = portSigRef.IndexSample.Unproject(srefNorm.IndexSample);
                            return new SignalRef(
                                port, sref.Prop,
                                projIndex.AsExpressions(),
                                projIndex, true);
                        }
                        catch (ArgumentException)
                        {
                        }
                    }
                }
            }
            else
            {
                var port = cd.GetPorts()
                    .Where(p => p.BoundSignal.Equals(srefNorm.Desc))
                    .FirstOrDefault();

                return new SignalRef(
                    port, sref.Prop,
                    srefNorm.Indices, srefNorm.IndexSample,
                    false);
            }

            return null;
        }
    }
}

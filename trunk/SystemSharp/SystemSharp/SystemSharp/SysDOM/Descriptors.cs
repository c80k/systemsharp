/**
 * Copyright 2013-2014 Christian Köllner
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
using System.Threading.Tasks;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.SysDOM;

namespace SystemSharp.SysDOM
{
    /// <summary>
    /// Data flow directions.
    /// </summary>
    public enum EFlowDirection
    {
        In,
        Out,
        InOut
    };

    public interface IExpressive
    {
        Expression DescribingExpression { get; }
    }

    public interface IDescriptive: IExpressive
    {
        IDescriptor Descriptor { get; }
    }

    public interface IDescriptive<TDesc, TInst> : IDescriptive
        where TDesc : DescriptorBase, IInstanceDescriptor<TInst>
        where TInst : IDescriptive
    {
        new TDesc Descriptor { get; }
    }

    public class DescriptorDiscardedEventArgs: EventArgs
    {
        private IDescriptor _dicardedDescriptor;

        public DescriptorDiscardedEventArgs(IDescriptor discardedDescriptor)
        {
            _dicardedDescriptor = discardedDescriptor;
        }

        public IDescriptor DiscardedDescriptor
        {
            get { return _dicardedDescriptor; }
        }
    }

    public interface IDescriptor : 
        IAttributed
    {
        string Name { get; }
        IDescriptor Owner { get; }
        IEnumerable<IDescriptor> Children { get; }
        void AddChild(IDescriptor child);
        void Discard();
        event Action<DescriptorDiscardedEventArgs> Discarded;
    }

    public interface IInstanceDescriptor : IDescriptor
    {
        IDescriptive Instance { get; }
    }

    public interface IInstanceDescriptor<TInst> : IInstanceDescriptor
        where TInst: IExpressive
    {
        new TInst Instance { get; }
    }

    public interface IComponentDescriptor : 
        IInstanceDescriptor<Component>,
        INamedComponentCollection,
        INamedMethodCollection,
        IChannelContainer,
        INamedSignalCollection
    {
        IEnumerable<IProcessDescriptor> Processes { get; }
        IProcessDescriptor AddNewProcess(string name, params Expression[] sensitivity);
        IProcessDescriptor AddNewThread(string name);

        IEnumerable<IPortDescriptor> Ports { get; }
        IPortDescriptor AddNewPort(string name);

        IEnumerable<IFieldDescriptor> Fields { get; }
        IFieldDescriptor AddNewField(string name, TypeDescriptor fieldType);
    }

    public interface IChannelDescriptor : 
        IInstanceDescriptor,
        IChannelContainer
    {
    }

    public interface IChannelDescriptor<TChannel> : 
        IInstanceDescriptor,
        IInstanceDescriptor<TChannel>
    {
    }

    public interface ISignalDescriptor : 
        IChannelDescriptor<SignalBase>,
        INamedSignalCollection
    {
        IEventDescriptor ChangedEvent { get; }
    }

    public interface IEventDescriptor :
        IChannelDescriptor<Event>
    {
    }

    public interface IPortDescriptor : IDescriptor
    {
        Expression BindExpression { get; }
        void Bind(Expression bindExpr);
    }

    public interface IFieldDescriptor : IDescriptor
    {
    }

    public interface IDesignDescriptor : 
        IInstanceDescriptor<DesignContext>,
        INamedComponentCollection
    {
        IEnumerable<IPackageDescriptor> Packages { get; }
        IPackageDescriptor GetOrCreatePackage(string name);
    }

    public interface IPackageDescriptor : INamedMethodCollection
    {
        IEnumerable<TypeDescriptor> Types { get; }
        TypeDescriptor GetOrCreateEnum(string suggestedTypeName, params string[] literals);
    }

    public interface ICodeDescriptor : IDescriptor
    {
        Statement Body { get; set; }
    }

    public interface IMethodDescriptor : ICodeDescriptor
    {
        IEnumerable<IArgumentDescriptor> Arguments { get; }
        IArgumentDescriptor AddNewArgument(string name, int index, TypeDescriptor argumentType, EFlowDirection direction);
        ISignalArgumentDescriptor AddNewSignalArgument(string name, int index, TypeDescriptor elementType, EFlowDirection access);
        TypeDescriptor ReturnType { get; }
    }

    public interface IArgumentDescriptor : IDescriptor
    {
        TypeDescriptor ArgumentType { get; }
        EFlowDirection Direction { get; }
    }

    public interface ISignalArgumentDescriptor : IArgumentDescriptor
    {
        TypeDescriptor ElementType { get; }
        EFlowDirection SignalAccessDirection { get; }
    }

    public interface IProcessDescriptor : 
        ICodeDescriptor,
        IInstanceDescriptor<Process>
    {
        Process.EProcessKind ProcessKind { get; }
        IEnumerable<Expression> Sensitivity { get; }
    }

    public interface ITypeDescriptor : IDescriptor
    {
    }

    public interface IComponentContainer : IDescriptor
    {
        IEnumerable<IComponentDescriptor> Components { get; }
    }

    public interface INamedComponentCollection : IComponentContainer
    {
        IComponentDescriptor AddNewComponent(string name);
    }

    public interface IIndexedComponentCollection : IComponentContainer
    {
        IComponentDescriptor AddNewComponent(IndexSpec index);
    }

    public interface INamedMethodCollection : IDescriptor
    {
        IEnumerable<IMethodDescriptor> Methods { get; }
        IMethodDescriptor AddNewMethod(string name);
        IMethodDescriptor AddNewMethod(string name, TypeDescriptor returnType);
    }

    public interface IChannelContainer : IDescriptor
    {
        IEnumerable<IChannelDescriptor> Channels { get; }
    }

    public interface ISignalContainer : IDescriptor
    {
        IEnumerable<ISignalDescriptor> Signals { get; }
    }

    public interface INamedSignalCollection : ISignalContainer
    {
        ISignalDescriptor AddNewSignal(string name, TypeDescriptor elementType);
    }

    public interface IIndexedSignalCollection : ISignalContainer
    {
        ISignalDescriptor AddNewSignal(IndexSpec index, TypeDescriptor elementType);
    }

    /// <summary>
    /// Base implementation of any descriptor.
    /// </summary>
    public abstract class DescriptorBase :
        AttributedObject,
        IDescriptor
    {
        private Dictionary<IDescriptor, IDescriptor> _children =
            new Dictionary<IDescriptor, IDescriptor>();
        private IDescriptor _owner;
        private Action<DescriptorDiscardedEventArgs> _discardedAction;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public DescriptorBase()
        {
        }

        /// <summary>
        /// Returns the name of this descriptor.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Owner of this descriptor.
        /// </summary>
        public IDescriptor Owner
        {
            get { return _owner; }
            protected set { _owner = value; }
        }

        private void OnChildDiscarded(DescriptorDiscardedEventArgs args)
        {
            RemoveChild(args.DiscardedDescriptor);
        }

        public void AddChild(IDescriptor child)
        {
            if (!_children.ContainsKey(child))
            {
                _children[child] = child;
                child.Discarded += OnChildDiscarded;
            }
        }

        public IEnumerable<IDescriptor> Children
        {
            get { return _children.Values; }
        }

        public virtual void Discard()
        {
            if (_discardedAction != null)
                _discardedAction(new DescriptorDiscardedEventArgs(this));
            _owner = null;
            while (Children.Any())
                Children.First().Discard();
        }

        public event Action<DescriptorDiscardedEventArgs> Discarded
        {
            add { _discardedAction += value; }
            remove { _discardedAction -= value; }
        }

        /// <summary>
        /// Removes a subordinate descriptor.
        /// </summary>
        /// <param name="desc">descriptor to remove</param>
        private void RemoveChild(IDescriptor desc)
        {
            _children.Remove(desc);
        }

        protected T Intern<T>(T child) where T : class, IDescriptor
        {
            IDescriptor clone;
            if (_children.TryGetValue(child, out clone))
            {
                return (T)clone;
            }
            else
            {
                _children[child] = child;
                return child;
            }
        }

#if false
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
#endif
    }

    /// <summary>
    /// Base implementation of any descriptor except for <c>TypeDescriptor</c>.
    /// </summary>
    public abstract class AbstractDescriptor :
        DescriptorBase,
        IComparable<AbstractDescriptor>
    {
        private DescriptorNesting _implementation;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public AbstractDescriptor()
        {
        }

        /// <summary>
        /// Returns the name of this descriptor.
        /// </summary>
        public override string Name 
        {
            get
            {
                var impl = _implementation;
                if (impl == null)
                    return "<undefined>";
                else
                    return impl.DescriptorName;
            }
        }

        public virtual void Nest(IInstanceDescriptor owner, FieldInfo field)
        {
            owner.AddChild(this);
            Owner = owner;
            _implementation = new DescriptorNestingByField(
                owner.Instance, field);
        }

        public virtual void Nest(IInstanceDescriptor owner, PropertyInfo property)
        {
            owner.AddChild(this);
            Owner = owner;
            _implementation = new DescriptorNestingByProperty(
                owner.Instance, property);
        }

        public virtual void Nest(IComponentDescriptor owner, MethodInfo method)
        {
            owner.AddChild(this);
            Owner = owner;
            _implementation = new DescriptorNestingByMethod(owner.Instance, method);
        }

        public virtual void Nest(IPackageDescriptor owner, MethodInfo method)
        {
            owner.AddChild(this);
            Owner = owner;
            _implementation = new DescriptorNestingByMethod(method);
        }

        public virtual void Nest(IDescriptor owner, string name)
        {
            owner.AddChild(this);
            Owner = owner;
            _implementation = new DescriptorNestingByName(name);
        }

        public virtual void Nest(IDescriptor owner, IndexSpec index)
        {
            owner.AddChild(this);
            Owner = owner;
            _implementation = new DescriptorNestingByIndex(index);
        }

        public IndexSpec Index
        {
            get
            {
                var indexImpl = Implementation as DescriptorNestingByIndex;
                if (indexImpl == null)
                    return null;
                else
                    return indexImpl.Index;
            }
        }

        internal DescriptorNesting Implementation
        {
            get { return _implementation; }
        }

#if false
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
#endif

        public int CompareTo(AbstractDescriptor other)
        {
            if (Owner != other.Owner)
                return 0;

            return IndexSpec.IndexComparer.Compare(Index, other.Index);
        }
    }

    /// <summary>
    /// Describes the overall design. This is the root component of the design hierarchy.
    /// </summary>
    public class DesignDescriptor : 
        AbstractDescriptor,
        IDesignDescriptor
    {
        private DesignContext _instance;

        internal DesignDescriptor(DesignContext design)
        {
            _instance = design;
        }

        /// <summary>
        /// Returns the associated design context.
        /// </summary>
        public DesignContext Instance
        {
            get { return _instance; }
        }

        /// <summary>
        /// Returns the associated design context.
        /// </summary>
        IDescriptive IInstanceDescriptor.Instance
        {
            get { return _instance; }
        }

        public override string Name
        {
            get { return string.Format("root{0}", GetHashCode().ToString("X")); }
        }

        private AssemblyBuilder _asmBuilder;

        /// <summary>
        /// Returns an assembly builder for dynamically-created behavior.
        /// </summary>
        internal AssemblyBuilder AsmBuilder
        {
            get
            {
                if (_asmBuilder == null)
                {
                    AssemblyName asmName = new AssemblyName();
                    asmName.Name = string.Format("dynbehavior.{0}", Name);
                    _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
                }
                return _asmBuilder;
            }
        }

        private ModuleBuilder _modBuilder;

        /// <summary>
        /// Returns a module builder for dynamically-created behavior.
        /// </summary>
        internal ModuleBuilder ModBuilder
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

        public IEnumerable<IPackageDescriptor> Packages
        {
            get { return Children.OfType<IPackageDescriptor>(); }
        }

        public IPackageDescriptor GetOrCreatePackage(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IComponentDescriptor> Components
        {
            get { throw new NotImplementedException(); }
        }

        public IComponentDescriptor AddNewComponent(string name)
        {
            throw new NotImplementedException();
        }
    }

    public class PackageDescriptor : 
        AbstractDescriptor,
        IPackageDescriptor
    {
        public IEnumerable<TypeDescriptor> Types
        {
            get { return Children.OfType<TypeDescriptor>(); }
        }

        public IEnumerable<IMethodDescriptor> Methods
        {
            get { return Children.OfType<IMethodDescriptor>(); }
        }

        public IMethodDescriptor AddNewMethod(string name)
        {
            var md = new MethodDescriptor();
            md.Nest(this, name);
            return md;
        }

        public IMethodDescriptor AddNewMethod(string name, TypeDescriptor returnType)
        {
            var md = new MethodDescriptor(returnType);
            md.Nest(this, name);
            return md;
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
        /// <param name="suggestedTypeName">desired name of the enumeration type</param>
        /// <param name="literals">enumeration literals</param>
        public TypeDescriptor GetOrCreateEnum(string suggestedTypeName, params string[] literals)
        {
            var fieldSeq = new HashableSequence<string>(literals);
            TypeDescriptor existing;
            if (EnumLookup.TryGetValue(fieldSeq, out existing))
                return existing;

            EnumBuilder tbe = null;
            var modBuilder = this.GetDesign().ModBuilder;
            try
            {
                tbe = modBuilder.DefineEnum(suggestedTypeName, TypeAttributes.Public, typeof(int));
            }
            catch (ArgumentException)
            {
                // assume existing type name
                int count = 0;
                while (true)
                {
                    string mname = suggestedTypeName + "_" + count;
                    try
                    {
                        tbe = modBuilder.DefineEnum(mname, TypeAttributes.Public, typeof(int));
                        break;
                    }
                    catch (ArgumentException)
                    {
                        ++count;
                    }
                }
            }
            int i = 0;
            foreach (string fieldName in literals)
            {
                FieldBuilder fb = tbe.DefineLiteral(fieldName, i);
                fb.SetConstant(i);
                ++i;
            }
            Type te = tbe.CreateType();
            TypeDescriptor td = TypeDescriptor.MakeType(te);
            EnumLookup.Add(fieldSeq, td);
            td.Nest(this);
            return td;
        }
    }

    public class ComponentDescriptor :
        AbstractDescriptor,
        IComponentDescriptor
    {
        private Component _component;

        internal ComponentDescriptor(Component instance)
        {
            _component = instance;
        }

        public override Component Instance
        {
            get { return _component; }
        }

        IDescriptive IInstanceDescriptor.Instance
        {
            get { return _component; }
        }

        public override void Discard()
        {
            var ctx = this.GetDesign().Instance;
            ctx.UnregisterComponent(_component);
            base.Discard();
        }

        public IEnumerable<IProcessDescriptor> Processes
        {
            get { return Children.OfType<IProcessDescriptor>(); }
        }

        public IProcessDescriptor AddNewProcess(string name, params Expression[] sensitivity)
        {
            throw new NotImplementedException();
        }

        public IProcessDescriptor AddNewThread(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IPortDescriptor> Ports
        {
            get { return Children.OfType<IPortDescriptor>(); }
        }

        public IPortDescriptor AddNewPort(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IChannelDescriptor> Channels
        {
            get { return Children.OfType<IChannelDescriptor>(); }
        }

        public IEnumerable<ISignalDescriptor> Signals
        {
            get { return Children.OfType<ISignalDescriptor>(); }
        }

        public ISignalDescriptor AddNewSignal(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IComponentDescriptor> Components
        {
            get { throw new NotImplementedException(); }
        }

        public IComponentDescriptor AddNewComponent(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IMethodDescriptor> Methods
        {
            get { return Children.OfType<IMethodDescriptor>(); }
        }

        public IMethodDescriptor AddNewMethod(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IFieldDescriptor> Fields
        {
            get { return Children.OfType<IFieldDescriptor>(); }
        }

        public IFieldDescriptor AddNewField(string name, TypeDescriptor fieldType)
        {
            throw new NotImplementedException();
        }
    }

    public class ComponentCollectionDescriptor :
        AbstractDescriptor,
        IIndexedComponentCollection
    {
        public IEnumerable<IComponentDescriptor> Components
        {
            get { return Children.OfType<IComponentDescriptor>().OrderBy(_ => _); }
        }

        public IComponentDescriptor AddNewComponent(IndexSpec index)
        {
            var compo = new Component();
            compo.Descriptor.Nest(this, index);
            return compo.Descriptor;
        }
    }

    public abstract class ChannelDescriptor :
        AbstractDescriptor,
        IChannelDescriptor
    {
    }

    public class EventDescriptor :
        ChannelDescriptor,
        IEventDescriptor
    {
        private Event _instance;

        public EventDescriptor(Event instance)
        {
            _instance = instance;
        }
    }

    public class SignalDescriptor :
        ChannelDescriptor,
        ISignalDescriptor
    {
        private SignalBase _instance;

        internal SignalDescriptor(SignalBase instance)
        {
            _instance = instance;
        }

        public IDescriptive Instance
        {
            get { return _instance; }
        }

        SignalBase IInstanceDescriptor<SignalBase>.Instance
        {
            get { return _instance; }
        }

        public override void Discard()
        {
            this.GetDesign().Instance.UnregisterSignal(_instance);
            base.Discard();
        }

        public IEventDescriptor ChangedEvent
        {
            get { return _instance.ChangedEvent.DescribingExpression; }
        }

        public IEnumerable<ISignalDescriptor> Signals
        {
            get { throw new NotImplementedException(); }
        }

        public ISignalDescriptor AddNewSignal(string name, TypeDescriptor elementType)
        {
            throw new NotImplementedException();
        }
    }

    public class PortDescriptor :
        AbstractDescriptor,
        IPortDescriptor
    {
        internal PortDescriptor()
        {
        }

        public Expression BindExpression
        {
            get 
            {
                if (Implementation == null)
                    return null;

                var inst = Implementation.Instance;
                if (inst == null)
                    return null;

                return inst.DescribingExpression;
            }
        }

        public void Bind(Expression bindExpr)
        {
            if (Implementation == null)
                throw new InvalidOperationException("Port descriptor is not nested, binding not possible.");

            if (Implementation.Instance != null)
                throw new InvalidOperationException("Port is already bound.");

            Implementation.Instance = (IDescriptive)bindExpr.Eval();
        }

        public override void Discard()
        {
            base.Discard();
        }
    }

    public abstract class CodeDescriptor :
        AbstractDescriptor,
        ICodeDescriptor
    {
        public Statement Body { get; set; }
    }

    public class MethodDescriptor :
        AbstractDescriptor,
        IMethodDescriptor
    {
        private TypeDescriptor _returnType;

        internal MethodDescriptor()
        {
        }

        internal MethodDescriptor(TypeDescriptor returnType)
        {
            _returnType = returnType;
        }

        public IEnumerable<IArgumentDescriptor> Arguments
        {
            get { return Children.OfType<IArgumentDescriptor>().OrderBy(_ => _); }
        }

        public IArgumentDescriptor AddNewArgument(string name, int index, TypeDescriptor argumentType, EFlowDirection direction)
        {
            var arg = new ArgumentDescriptor(argumentType, direction);
            arg.Nest(this, new IndexSpec(index));
            return arg;
        }

        public ISignalArgumentDescriptor AddNewSignalArgument(string name, int index, TypeDescriptor elementType, EFlowDirection access)
        {
            var arg = new SignalArgumentDescriptor(typeof(SignalBase), EFlowDirection.In, elementType, access);
            arg.Nest(this, new IndexSpec(index));
            return arg;
        }

        public TypeDescriptor ReturnType
        {
            get { return _returnType; }
        }

        public Statement Body
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
    }

    public class ProcessDescriptor :
        AbstractDescriptor,
        IProcessDescriptor
    {
        private Process _instance;
        
        internal ProcessDescriptor(Process instance)
        {
            _instance = instance;
        }

        public Process.EProcessKind ProcessKind
        {
            get { return _instance.Kind; }
        }

        public IEnumerable<Expression> Sensitivity
        {
            get { return _instance.Sensitivity.Select(_ => _.DescribingExpression); }
        }

        public Statement Body { get; set; }

        public Process Instance
        {
            get { return _instance; }
        }

        IDescriptive IInstanceDescriptor.Instance
        {
            get { return _instance; }
        }
    }

    public class ArgumentDescriptor :
        AbstractDescriptor,
        IArgumentDescriptor
    {
        private TypeDescriptor _argumentType;
        private EFlowDirection _direction;

        internal ArgumentDescriptor(TypeDescriptor argumentType, EFlowDirection direction)
        {
            _argumentType = argumentType;
            _direction = direction;
        }

        public TypeDescriptor ArgumentType
        {
            get { return _argumentType; }
        }

        public EFlowDirection Direction
        {
            get { return _direction; }
        }
    }

    public class SignalArgumentDescriptor :
        ArgumentDescriptor,
        ISignalArgumentDescriptor
    {
        private TypeDescriptor _elementType;
        private EFlowDirection _access;

        internal SignalArgumentDescriptor(TypeDescriptor argumentType, EFlowDirection direction,
            TypeDescriptor elementType, EFlowDirection access):
            base(argumentType, direction)
        {
            _elementType = elementType;
            _access = access;
        }

        public TypeDescriptor ElementType
        {
            get { return _elementType; }
        }

        public EFlowDirection SignalAccessDirection
        {
            get { return _access; }
        }
    }

    public static class DescriptorExtensions
    {
        /// <summary>
        /// Returns the root descriptor.
        /// </summary>
        public static DesignDescriptor GetDesign(this IDescriptor desc)
        {
            var cur = desc;
            while (cur != null && !(cur is DesignDescriptor))
                cur = cur.Owner;
            return (DesignDescriptor)cur;
        }
    }
}

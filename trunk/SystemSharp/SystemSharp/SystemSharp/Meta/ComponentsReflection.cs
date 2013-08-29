﻿/**
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
    public interface IDescriptorVisitor
    {
        void VisitComponentDescriptor(ComponentDescriptor cd);
        void VisitFieldDescriptor(FieldDescriptor fd);
        void VisitSignalDescriptor(SignalDescriptor sd);
        void VisitPortDescriptor(PortDescriptor pd);
        void VisitMethodDescriptor(MethodDescriptor md);
        void VisitProcessDescriptor(ProcessDescriptor pd);
    }

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

        public ComponentDescriptorHandler OnComponent { get; set; }
        public FieldDescriptorHandler OnField { get; set; }
        public SignalDescriptorHandler OnSignal { get; set; }
        public PortDescriptorHandler OnPort { get; set; }
        public MethodDescriptorHandler OnMethod { get; set; }
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

    [ContractClass(typeof(ContainmentImplementorContractClass))]
    public interface IContainmentImplementor
    {
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

    [ContractClass(typeof(DescriptorContractClass))]
    public interface IDescriptor: IAttributed
    {
        DescriptorBase Owner { get; }
        string Name { get; }
        void AddChild(InstanceDescriptor desc, FieldInfo declSite, IndexSpec indexSpec);
        void AddChild(DescriptorBase desc, string name);
        IEnumerable<DescriptorBase> Children { get; }
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

    public class Document
    {
        public string Name { get; private set; }
        public object Content { get; private set; }

        public Document(string name, object content)
        {
            Name = name;
            Content = content;
        }
    }

    public class Documentation
    {
        public List<Document> Documents { get; private set; }

        public Documentation()
        {
            Documents = new List<Document>();
        }
    }

    public abstract class DescriptorBase :
        AttributedObject,
        IDescriptor
    {
        private Dictionary<DescriptorBase, DescriptorBase> _children = new Dictionary<DescriptorBase, DescriptorBase>();

        public virtual DescriptorBase Owner { get; internal set; }

        private string _name;
        public virtual string Name
        {
            get { return _name; }
        }

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

        public DesignDescriptor GetDesign()
        {
            DescriptorBase cur = this;
            while (!(cur is DesignDescriptor))
                cur = cur.Owner;
            return (DesignDescriptor)cur;
        }

        public bool IsActive { get; internal set; }

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

        public static IEnumerable<TypeDescriptor> GetTypes(this IDescriptor me)
        {
            return SelectChildren<TypeDescriptor>(me);
        }

        public static IEnumerable<FieldDescriptor> GetFields(this IDescriptor me)
        {
            return SelectChildren<FieldDescriptor>(me);
        }

        public static IEnumerable<FieldDescriptor> GetVariables(this IDescriptor me)
        {
            return GetFields(me).Where(fd => !fd.IsConstant);
        }

        public static IEnumerable<FieldDescriptor> GetConstants(this IDescriptor me)
        {
            return GetFields(me).Where(fd => fd.IsConstant);
        }

        public static IEnumerable<IComponentDescriptor> GetChildComponents(this IDescriptor me)
        {
            return SelectChildren<IComponentDescriptor>(me);
        }

        public static IEnumerable<ChannelDescriptor> GetChannels(this IDescriptor me)
        {
            return SelectChildren<ChannelDescriptor>(me);
        }

        public static IEnumerable<ISignalDescriptor> GetSignals(this IDescriptor me)
        {
            return SelectChildren<ISignalDescriptor>(me);
        }

        public static IEnumerable<IPortDescriptor> GetPorts(this IDescriptor me)
        {
            return SelectChildren<IPortDescriptor>(me);
        }

        public static IEnumerable<SignalArgumentDescriptor> GetSignalArguments(this IDescriptor me)
        {
            return SelectChildren<SignalArgumentDescriptor>(me);
        }

        public static IEnumerable<MethodDescriptor> GetMethods(this IDescriptor me)
        {
            return SelectChildren<MethodDescriptor>(me);
        }

        public static IEnumerable<MethodDescriptor> GetActiveMethods(this IDescriptor me)
        {
            return SelectChildren<MethodDescriptor>(me).Where(md => md.IsActive);
        }

        public static IEnumerable<ProcessDescriptor> GetProcesses(this IDescriptor me)
        {
            return SelectChildren<ProcessDescriptor>(me);
        }

        public static IEnumerable<ArgumentDescriptor> GetArguments(this IDescriptor me)
        {
            return SelectChildrenOrdered<ArgumentDescriptor>(me);
        }

        public static IEnumerable<PackageDescriptor> GetPackages(this IDescriptor me)
        {
            return SelectChildren<PackageDescriptor>(me);
        }

        public static ProcessDescriptor FindProcess(this IDescriptor me, string name)
        {
            return GetProcesses(me).Where(p => p.Name == name).FirstOrDefault();
        }

        public static MethodDescriptor FindMethod(this IDescriptor me, string name)
        {
            return GetMethods(me).Where(p => p.Name == name).FirstOrDefault();
        }

        public static MethodDescriptor FindMethod(this IDescriptor me, MethodBase method)
        {
            return GetMethods(me).Where(p => p.Method.Equals(method)).FirstOrDefault();
        }

        public static IComponentDescriptor FindComponent(this IDescriptor me, string name)
        {
            return GetChildComponents(me).Where(c => c.Name == name).FirstOrDefault();
        }

        public static FieldDescriptor FindField(this IDescriptor me, string name)
        {
            return GetFields(me).Where(f => f.Name == name).FirstOrDefault();
        }

        // Redundant by GetBoundSignal()
        /*public static ISignalOrPortDescriptor AsSignal(this ISignalOrPortDescriptor sd)
        {
            IPortDescriptor pd = sd as IPortDescriptor;
            if (pd == null)
                return sd;
            else
                return pd.BoundSignal;
        }*/

#if false
        /// <summary>
        /// Given some signal or port, this method searches the local signal and port lists to either find a 
        /// port which is equivalent to the given one (bound to the same signal) or the signal itself (if it is
        /// declared inside this component.
        /// </summary>
        /// <param name="desc">a signal or port descriptor</param>
        /// <returns>An equivalent signal or port descriptor which is valid within the scope of this component.</returns>
        public static ISignalOrPortDescriptor FindSignalOrPort(this IComponentDescriptor me, ISignalOrPortDescriptor desc)
        {
            // Shortcut for directly owned signals. The procedure does not involve a signal instance.
            // Therefore, it is suitable even for descriptors without signal instance.
            if (desc.Owner == me)
                return desc;

            if (desc is SignalArgumentDescriptor)
                return desc;

            if (me.GetSignals().Contains(desc) ||
                me.GetSignals().Contains(desc.GetUnindexedContainer()))
                return desc;

            IPortDescriptor pResult = me
                .GetPorts()
                .Where(p => p.BoundSignal.Equals(desc.GetBoundSignal()))
                .FirstOrDefault();
            if (pResult != null)
                return pResult;

            var ports = me
                .GetPorts()
                .Where(p => p.BoundSignal.GetUnindexedContainer().Equals(desc.GetBoundSignal().GetUnindexedContainer()));


            return null;
        }
#endif

        public static IPortDescriptor FindPort(this IComponentDescriptor me, string name)
        {
            return me.GetPorts().Where(p => p.Name == name).FirstOrDefault();
        }

        public static ISignalDescriptor FindSignal(this IComponentDescriptor me, string name)
        {
            return me.GetSignals().Where(s => s.Name == name).FirstOrDefault();
        }

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

    public abstract class InstanceDescriptor :
        DescriptorBase
    {
        public InstanceDescriptor(IDescriptive instance)
        {
            Instance = instance;
            Index = IndexSpec.Empty;
        }

        public IDescriptive Instance { get; private set; }

        private IndexSpec _index;
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

    public interface IInstanceDescriptor<ObjectType> : IDescriptor
    {
        ObjectType Instance { get; }
        InstanceDescriptor RemoveIndex();
    }

    public interface IDescriptive
    {
        DescriptorBase Descriptor { get; }
    }

    public interface IDescriptive<DescType> :
        IDescriptive
    {
        new DescType Descriptor { get; }
    }

    public abstract class FieldDescriptor : DescriptorBase
    {
        public TypeDescriptor Type { get; protected set; }
        public bool TypeIsGuessed { get; protected set; }
        public bool IsConstant { get; internal set; }
        public object ConstantValue { get; internal set; }
        public abstract bool IsReadInCurrentContext(DesignContext context);
        public abstract bool IsWrittenInCurrentContext(DesignContext context);

        public FieldDescriptor(TypeDescriptor type)
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

        public abstract object Value { get; }
        public abstract bool IsStatic { get; }
    }

    public class CILFieldDescriptor : FieldDescriptor
    {
        public FieldInfo Field { get; private set; }
        public object Instance { get; private set; }

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

        public override bool Equals(object obj)
        {
            if (obj is CILFieldDescriptor)
            {
                var fd = (CILFieldDescriptor)obj;
                return Field.Equals(fd.Field) && Instance == fd.Instance;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return Field.GetHashCode() ^
                (Instance == null ? 0 : Instance.GetHashCode());
        }

        public override object Value
        {
            get { return Field.GetValue(Instance); }
        }

        public override bool IsStatic
        {
            get { return Field.IsStatic; }
        }

        public IEnumerable<SystemSharp.Components.Process> DrivingProcesses
        {
            get { return _drivingProcesses.AsEnumerable(); }
        }

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

    public abstract class ChannelDescriptor :
        InstanceDescriptor
    {
        public ChannelDescriptor(IDescriptive instance) :
            base(instance)
        {
        }

        public Channel BoundChannel { get; internal set; }
    }

    public interface ISignalOrPortDescriptor :
        IDescriptor
    {
        TypeDescriptor InstanceType { get; }
        TypeDescriptor ElementType { get; }
        object InitialValue { get; }
    }

    public interface ISignalDescriptor :
        ISignalOrPortDescriptor,
        IInstanceDescriptor<SignalBase>
    {
    }

    public enum EPortDirection
    {
        In,
        Out,
        InOut
    };

    public interface IPortDescriptor : ISignalOrPortDescriptor
    {
        EPortDirection Direction { get; }
        EPortUsage Usage { get; }
        string Domain { get; }
        ISignalDescriptor BoundSignal { get; }
    }

    public class PortDescriptor :
        DescriptorBase,
        IPortDescriptor
    {
        public PortDescriptor(
            PropertyInfo declSite,
            SignalDescriptor boundSignal,
            TypeDescriptor elementType,
            EPortDirection direction)
        {
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

        public PropertyInfo DeclarationSite { get; private set; }
        public TypeDescriptor ElementType { get; private set; }
        public EPortDirection Direction { get; private set; }
        public EPortUsage Usage { get; private set; }
        public string Domain { get; private set; }
        public ISignalDescriptor BoundSignal { get; private set; }

        public TypeDescriptor InstanceType
        {
            get
            {
                return BoundSignal == null ?
                    (TypeDescriptor)typeof(Signal<>).MakeGenericType(ElementType.CILType) :
                    BoundSignal.InstanceType;
            }
        }

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

    public class SignalDescriptor :
        ChannelDescriptor,
        ISignalDescriptor
    {
        public SignalDescriptor(
            SignalBase instance,
            TypeDescriptor elementType) :
            base(instance)
        {
            Contract.Requires(elementType != null);

            ElementType = elementType;
            Index = elementType.Index;
        }

        public TypeDescriptor ElementType { get; private set; }

        public TypeDescriptor InstanceType
        {
            get { return TypeDescriptor.GetTypeOf(SignalInstance); }
        }

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

    public interface IOrdered
    {
        int Order { get; }
    }

    public class ArgumentDescriptor :
        DescriptorBase,
        IOrdered
    {
        public enum EArgDirection
        {
            In, Out, InOut
        }

        private IStorableLiteral _arg;
        private EArgDirection _dir;
        private EVariability _variability;
        private int _order;

        public ArgumentDescriptor(IStorableLiteral arg, EArgDirection dir, EVariability variability, int order)
        {
            _arg = arg;
            _dir = dir;
            _variability = variability;
            _order = order;
        }

        public override string Name
        {
            get { return _arg.Name; }
        }

        public IStorableLiteral Argument
        {
            get { return _arg; }
        }

        public EArgDirection Direction
        {
            get { return _dir; }
        }

        public object Sample
        {
            get { return _arg.Type.GetSampleInstance(ETypeCreationOptions.AnyObject); }
        }

        public EVariability Variability
        {
            get { return _variability; }
        }

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

        public static readonly IEqualityComparer<ArgumentDescriptor> TypeAndDirectionComparer =
            new ArgumentTypeAndDirectionComparer();
    }

    public class SignalArgumentDescriptor :
        ArgumentDescriptor,
        ISignalOrPortDescriptor
    {
        private EArgDirection _flowDir;

        public SignalArgumentDescriptor(SignalRef sref, EArgDirection dir, EArgDirection flowDir, EVariability variability, int order) :
            base(sref, dir, variability, order)
        {
            Contract.Requires(sref.Prop == SignalRef.EReferencedProperty.Instance);

            _flowDir = flowDir;
        }

        public TypeDescriptor ElementType
        {
            get { return SignalInstance.ElementType; }
        }

        public SignalBase SignalInstance
        {
            get { return (SignalBase)Sample; }
        }

        public TypeDescriptor InstanceType
        {
            get { return Argument.Type; }
        }

        public EArgDirection FlowDirection
        {
            get { return _flowDir; }
        }

        public IndexSpec Index
        {
            get { return IndexSpec.Empty; }
        }

        public object InitialValue
        {
            get { return null; }
        }
    }

    public abstract class CodeDescriptor :
        DescriptorBase
    {
        public class ValueRangeConstraint
        {
            public bool IsConstrained { get; private set; }
            public long MinValue { get; private set; }
            public long MaxValue { get; private set; }

            private ValueRangeConstraint()
            {
                IsConstrained = false;
            }

            public ValueRangeConstraint(long minValue, long maxValue)
            {
                MinValue = minValue;
                MaxValue = maxValue;
                IsConstrained = true;
            }

            public static ValueRangeConstraint Unconstrained = new ValueRangeConstraint();
        }

        private HashSet<ISignalOrPortDescriptor> _drivenSignals = new HashSet<ISignalOrPortDescriptor>();

        public MethodInfo AsyncMethod { get; private set; }
        public MethodBase Method { get; private set; }
        public Function Implementation { get; internal set; }
        public Function GenuineImplementation { get; internal set; }
        public ValueRangeConstraint[] ValueRangeConstraints { get; internal set; }

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

        public CodeDescriptor(string name)
        {
            _name = name;
        }

        private string _name;
        public override string Name
        {
            get { return _name; }
        }

        public IEnumerable<ISignalOrPortDescriptor> DrivenSignals
        {
            get
            {
                Contract.Assume(_drivenSignals != null);

                return new ReadOnlyCollection<ISignalOrPortDescriptor>(_drivenSignals.ToList());
            }
        }

        public void AddDrivenSignal(ISignalOrPortDescriptor signal)
        {
            _drivenSignals.Add(signal);
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is CodeDescriptor)
            {
                CodeDescriptor cd = (CodeDescriptor)obj;
                return Owner == cd.Owner &&
                    Name.Equals(cd.Name);
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

    public class ProcessDescriptor : CodeDescriptor
    {
        public ProcessDescriptor(MethodBase method, SystemSharp.Components.Process instance) :
            base(method)
        {
            Instance = instance;
            Kind = instance.Kind;
        }

        public ProcessDescriptor(string name) :
            base(name)
        {
        }

        private ISignalOrPortDescriptor[] _sensitivity;
        public ISignalOrPortDescriptor[] Sensitivity
        {
            get { return _sensitivity; }
            internal set 
            {
                Contract.Requires<ArgumentException>(value != null);
                _sensitivity = value.Select(s => s.GetUnindexedContainer()).ToArray(); 
            }
        }

        public SystemSharp.Components.Process.EProcessKind Kind { get; set; }
        public SystemSharp.Components.Process Instance { get; private set; }

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

    public class MethodDescriptor : CodeDescriptor
    {
        public MethodDescriptor(
            MethodBase method,
            object[] argValueSamples,
            EVariability[] argVariabilities) :
            base(method)
        {
            Contract.Requires(argVariabilities != null &&
                argVariabilities.Length == method.GetParameters().Length);
            Debug.Assert(argValueSamples.All(s => s != null));
            ArgValueSamples = argValueSamples;
            ArgVariabilities = argVariabilities;
            InitArguments();
        }

        public TypeDescriptor ReturnType
        {
            get
            {
                if (ReturnValueSample != null)
                    return TypeDescriptor.GetTypeOf(ReturnValueSample);
                else
                {
                    Type returnType;
                    Method.IsFunction(out returnType);
                    return TypeDescriptor.MakeType(returnType);
                }
            }
        }

        public object[] ArgValueSamples { get; private set; }
        public EVariability[] ArgVariabilities { get; private set; }
        public object ReturnValueSample { get; internal set; }
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
                return false;
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

    public static class IntToZeroBasedUpRangeConverter
    {
        public static Range ConvertToRange(int arg)
        {
            return new Range(0, arg - 1, EDimDirection.To);
        }
    }

    public static class IntToZeroBasedDownRangeConverter
    {
        public static Range ConvertToRange(int arg)
        {
            return new Range(arg - 1, 0, EDimDirection.Downto);
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class TypeParameter : Attribute
    {
        public Type RangeConverter { get; private set; }

        public TypeParameter(Type rangeConverter)
        {
            RangeConverter = rangeConverter;
        }
    }

    public interface IPackageOrComponentDescriptor
    {
        void AddDependency(PackageDescriptor pd);
        IEnumerable<PackageDescriptor> Dependencies { get; }
        int DependencyOrder { get; }
        string Library { get; }
    }

    interface IDependencyOrdered
    {
        int DependencyOrder { get; set; }
    }

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

    public class PackageDescriptor :
        DescriptorBase,
        IPackageOrComponentDescriptor,
        IDependencyOrdered
    {
        private PackageOrComponentDescriptor _container = new PackageOrComponentDescriptor();

        public string PackageName { get; private set; }

        public PackageDescriptor(string packageName)
        {
            Contract.Requires(packageName != null && packageName != "");
            if (packageName == null || packageName == "")
                throw new ArgumentException("Need a name for the package");

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

        public override bool Equals(object obj)
        {
            if (obj is PackageDescriptor)
            {
                PackageDescriptor pd = (PackageDescriptor)obj;
                return PackageName == pd.PackageName;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return PackageName.GetHashCode();
        }

        public void AddDependency(PackageDescriptor pd)
        {
            if (pd == this)
                throw new ArgumentException("Cyclic dependency");

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

        public override void AddChild(DescriptorBase desc, string name)
        {
            var tdesc = desc as TypeDescriptor;
            Debug.Assert(tdesc == null || !tdesc.HasIntrinsicTypeOverride);

            base.AddChild(desc, name);
        }

        public string Library { get; set; }
    }

    [Flags]
    public enum EComponentCoverage
    {
        Interface,
        Implementation
    }

    public class ComponentDescriptor :
        InstanceDescriptor,
        IInstanceDescriptor<Component>,
        IPackageOrComponentDescriptor,
        IComponentDescriptor,
        IDependencyOrdered
    {
        private PackageOrComponentDescriptor _container = new PackageOrComponentDescriptor();

        public PackageDescriptor Package { get; internal set; }

        public ComponentDescriptor(Component instance) :
            base(instance)
        {
        }

        internal bool HasForeignImplementation { get; set; }

        public new Component Instance
        {
            get { return (Component)base.Instance; }
        }

        public void AddDependency(PackageDescriptor pd)
        {
            _container.AddDependency(pd);
        }

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

        public EComponentCoverage Coverage { get; internal set; }
        public string ImplementationDomain { get; internal set; }

        public int DependencyOrder { get; internal set; }

        int IDependencyOrdered.DependencyOrder
        {
            get { return DependencyOrder; }
            set { DependencyOrder = value; }
        }

        public string Library { get; set; }
    }

    public class DesignDescriptor : DescriptorBase //, IComponentDescriptor
    {
        public DesignContext Context { get; private set; }
        public TypeLibrary TypeLib { get; private set; }

        internal DesignDescriptor(DesignContext design)
        {
            Context = design;
            TypeLib = new TypeLibrary(this);
        }

        public DesignDescriptor()
        {
            TypeLib = new TypeLibrary(this);
        }

        public override string Name
        {
            get { return "Design"; }
        }

        private AssemblyBuilder _asmBuilder;
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

        public ComponentBuilder CreateComponent(string name)
        {
            return new ComponentBuilder(name);
        }
    }

    public static class DescriptorExtensions
    {
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

        public static ISignalOrPortDescriptor GetUnindexedContainer(this ISignalOrPortDescriptor sd)
        {
            IndexSpec dummy;
            return GetUnindexedContainer(sd, out dummy);
        }

        public static SignalRef AsSignalRef(this ISignalOrPortDescriptor sd, SignalRef.EReferencedProperty prop)
        {
            return new SignalRef(sd, prop);
        }

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

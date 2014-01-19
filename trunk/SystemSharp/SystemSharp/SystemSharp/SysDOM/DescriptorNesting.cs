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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.DataTypes;

namespace SystemSharp.SysDOM
{
    abstract class DescriptorNesting
    {
        public abstract string DescriptorName { get; }
        public abstract IDescriptive Instance { get; set; }
    }

    abstract class DescriptorNestingByMember :
        DescriptorNesting
    {
        private MemberInfo _member;

        public DescriptorNestingByMember(MemberInfo member)
        {
            _member = member;
        }

        public override string DescriptorName
        {
            get { return Member.Name; }
        }

        public MemberInfo Member
        {
            get { return _member; }
        }
    }

    class DescriptorNestingByField :
        DescriptorNestingByMember
    {
        private IDescriptive _declaringInstance;

        public DescriptorNestingByField(IDescriptive declaringInstance, FieldInfo field) :
            base(field)
        {
            _declaringInstance = declaringInstance;
        }

        public FieldInfo Field
        {
            get { return (FieldInfo)Member; }
        }

        public override IDescriptive Instance
        {
            get { return (IDescriptive)Field.GetValue(_declaringInstance); }
            set { Field.SetValue(_declaringInstance, value); }
        }
    }

    class DescriptorNestingByProperty :
        DescriptorNestingByMember
    {
        private IDescriptive _declaringInstance;

        public DescriptorNestingByProperty(IDescriptive declaringInstance, PropertyInfo property) :
            base(property)
        {
            _declaringInstance = declaringInstance;
        }

        public PropertyInfo Property
        {
            get { return (PropertyInfo)Member; }
        }

        public override IDescriptive Instance
        {
            get { return (IDescriptive)Property.GetValue(_declaringInstance); }
            set { Property.SetValue(_declaringInstance, value); }
        }
    }

    class DescriptorNestingByMethod :
        DescriptorNestingByMember
    {
        private Component _declaringComponent;

        public DescriptorNestingByMethod(MethodInfo method) :
            base(method)
        {
            Contract.Requires<ArgumentException>(method.IsStatic, "expected static method");
        }

        public DescriptorNestingByMethod(Component declaringComponent, MethodInfo method) :
            base(method)
        {
            Contract.Requires<ArgumentException>(!method.IsStatic, "expected non-static method");
        }

        public MethodInfo Method
        {
            get { return (MethodInfo)Member; }
        }

        public override IDescriptive Instance
        {
            get { return null; }
            set { throw new InvalidOperationException("No instance container provided."); }
        }
    }

    class DescriptorNestingByName :
        DescriptorNesting
    {
        private string _name;
        private IDescriptive _instance;

        public DescriptorNestingByName(string name)
        {
            _name = name;
        }

        public override string DescriptorName
        {
            get { return _name; }
        }

        public override IDescriptive Instance
        {
            get { return _instance; }
            set { _instance = value; }
        }
    }

    class DescriptorNestingByIndex :
        DescriptorNesting
    {
        private IndexSpec _index;
        private IDescriptive _instance;

        public DescriptorNestingByIndex(IndexSpec index)
        {
            _index = index;
        }

        public override string DescriptorName
        {
            get
            {
                return string.Format("{0}{1}",
                    _instance.Descriptor.Owner.Name,
                    _index.ToString());
            }
        }

        public override IDescriptive Instance
        {
            get { return _instance; }
            set { _instance = value; }
        }

        public IndexSpec Index
        {
            get { return _index; }
        }
    }

    class DescriptorNestingByNameAndIndex :
        DescriptorNestingByIndex
    {
        private string _name;

        public DescriptorNestingByNameAndIndex(string name, IndexSpec index) :
            base(index)
        {
            _name = name;
        }

        public override string DescriptorName
        {
            get { return _name; }
        }
    }
}

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
using System.Runtime.CompilerServices;
using SystemSharp.Components.FU;
using SystemSharp.DataTypes;

namespace SystemSharp.Components.Transactions
{
    public enum ETVMode
    {
        Locked,
        Shared
    }

    public class TAVerb
    {
        public ITransactionSite Target { get; private set; }
        public Action Op { get; private set; }
        public IProcess During { get; private set; }
        public ETVMode TMode { get; private set; }

        public TAVerb(ITransactionSite target, ETVMode tmode, Action op)
        {
            Contract.Requires(target != null);
            Contract.Requires(op != null);

            Target = target;
            TMode = tmode;
            Op = op;
        }

        public TAVerb(ITransactionSite target, ETVMode tmode, Action op, IProcess during)
        {
            Contract.Requires(target != null);
            Contract.Requires(op != null);

            Target = target;
            TMode = tmode;
            Op = op;
            During = during;
        }

        public override bool Equals(object obj)
        {
            TAVerb other = obj as TAVerb;
            if (other == null)
                return false;
            return Op.Method.Equals(other.Op.Method) &&
                Target == other.Target;
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(Target) ^ Op.GetHashCode();
        }

        public ParFlow ToCombFlow()
        {
            if (During == null)
                return new ParFlow();
            else
                return During.ToFlow();
        }
    }

    public enum ETARole
    {
        Clock,
        Exchange,
        Parameter
    }

    [AttributeUsage(AttributeTargets.Property, Inherited=true, AllowMultiple=false)]
    public class TAPort : Attribute
    {
        public ETARole TARole { get; private set; }
        public object TAGroup { get; private set; }
        public object TAPortID { get; private set; }

        public TAPort(ETARole taRole, object taGroup, object taPortID)
        {
            TARole = taRole;
            TAGroup = taGroup;
            TAPortID = taPortID;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public class TAArg : Attribute
    {
        public object TAPortID { get; private set; }

        public TAArg(object taPortID)
        {
            TAPortID = taPortID;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class Transaction : StaticEvaluation
    {
        public object TAGroup { get; private set; }
        public bool IsNeutral { get; private set; }

        public Transaction(object taGroup, bool isNeutral = false)
        {
            TAGroup = taGroup;
            IsNeutral = isNeutral;
        }
    }

    public interface ITransactionSite
    {
        Component Host { [StaticEvaluation] get; }
        string Name { get; }

        [StaticEvaluation]
        IEnumerable<TAVerb> DoNothing();

        void Establish(IAutoBinder binder);
    }

    public abstract class DefaultTransactionSite: ITransactionSite
    {
        public Component Host { [StaticEvaluation] get; private set; }

        public DefaultTransactionSite(Component host)
        {
            Host = host;
        }

        public virtual string Name 
        {
            get { return "$" + RuntimeHelpers.GetHashCode(this).ToString(); }
        }

        [StaticEvaluation]
        public abstract IEnumerable<TAVerb> DoNothing();

        public virtual void Establish(IAutoBinder binder)
        {
        }

        private TAVerb Verb(ETVMode tmode, Action op, params IProcess[] during)
        {
            Contract.Requires<ArgumentNullException>(during != null);
            Contract.Requires<ArgumentNullException>(op != null);

            if (during.Length == 0)
            {
                return new TAVerb(this, tmode, op);
            }
            else
            {
                IProcess cur = during[0];
                for (int i = 1; i < during.Length; i++)
                    cur = cur.Par(during[i]);
                return new TAVerb(this, tmode, op, cur);
            }
        }

        public TAVerb Verb(ETVMode tmode, params IProcess[] during)
        {
            return Verb(tmode, () => { }, during);
        }
    }
}

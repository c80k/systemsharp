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
    /// <summary>
    /// The mode of a transaction verb
    /// </summary>
    public enum ETVMode
    {
        /// <summary>
        /// Verb belongs to initiation interval and therefore must not be combined with any other verb in locked mode.
        /// </summary>
        Locked,

        /// <summary>
        /// Verb can be combined with any other verb.
        /// </summary>
        Shared
    }

    /// <summary>
    /// A transaction verb is the base element of a clocked transaction. It describes the register
    /// transfers which are active during a single clock period.
    /// </summary>
    public class TAVerb
    {
        /// <summary>
        /// Transaction site on which the verb operates
        /// </summary>
        public ITransactionSite Target { get; private set; }

        /// <summary>
        /// Obsolete, will be removed
        /// </summary>
        [Obsolete("part of an out-dated concept, planned for removal")]
        public Action Op { get; private set; }

        /// <summary>
        /// Process which describes the active register transfers within the scope of this verb
        /// </summary>
        public IProcess During { get; private set; }

        /// <summary>
        /// Mode of this verb
        /// </summary>
        public ETVMode TMode { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="target">transaction site on which verb is operated</param>
        /// <param name="tmode">mode of verb</param>
        /// <param name="op">relict of an out-dated concept, please specify () => { }</param>
        [Obsolete("Please use other constructor")]
        public TAVerb(ITransactionSite target, ETVMode tmode, Action op)
        {
            Contract.Requires(target != null);
            Contract.Requires(op != null);

            Target = target;
            TMode = tmode;
            Op = op;
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="target">transaction site on which verb is operated</param>
        /// <param name="tmode">mode of verb</param>
        /// <param name="op">relict of an out-dated concept, please specify () => { }</param>
        /// <param name="during">process which describes the active register transfers within the scope of the created verb</param>
        public TAVerb(ITransactionSite target, ETVMode tmode, Action op, IProcess during)
        {
            Contract.Requires<ArgumentNullException>(target != null, "target");
            Contract.Requires<ArgumentNullException>(op != null, "op");

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

        /// <summary>
        /// Converts this verb to an aggregate flow representation
        /// </summary>
        public ParFlow ToCombFlow()
        {
            if (During == null)
                return new ParFlow();
            else
                return During.ToFlow();
        }
    }

    [Obsolete("relict of a never-realized concept, planned for removal")]
    public enum ETARole
    {
        Clock,
        Exchange,
        Parameter
    }

    [Obsolete("relict of a never-realized concept, planned for removal")]
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

    [Obsolete("relict of a never-realized concept, planned for removal")]
    [AttributeUsage(AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public class TAArg : Attribute
    {
        public object TAPortID { get; private set; }

        public TAArg(object taPortID)
        {
            TAPortID = taPortID;
        }
    }

    [Obsolete("relict of a never-realized concept, planned for removal")]
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

    /// <summary>
    /// General transaction site interface
    /// </summary>
    /// <remarks>
    /// A transaction site is a fundamental abstraction used during System# high-level synthesis.
    /// It embodies a conceptual hardware site which can perform one or more specific operations. 
    /// Clocked transactions (i.e. a sequences of transaction verbs) describe how to operate the hardware in order
    /// to achieve the desired behavior. Each possible operation is embodied by a specific method of the
    /// specialized transaction site. A common transaction of all transaction sites is the neutral transaction which is
    /// executed whenever there is actually nothing to do.
    /// </remarks>
    public interface ITransactionSite
    {
        /// <summary>
        /// Hardware unit hosting the transaction site
        /// </summary>
        Component Host { [StaticEvaluation] get; }

        /// <summary>
        /// Name of transaction site
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns the neutral transaction which is executed whenever there is nothing to do.
        /// </summary>
        [StaticEvaluation]
        IEnumerable<TAVerb> DoNothing();

        /// <summary>
        /// Connects the underlying hardware to its environment by binding its ports to signals
        /// created by a binder service.
        /// </summary>
        /// <param name="binder">binder service</param>
        void Establish(IAutoBinder binder);
    }

    /// <summary>
    /// An abstract base implementation of transaction site interface
    /// </summary>
    public abstract class DefaultTransactionSite: ITransactionSite
    {
        public Component Host { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="host">hosting component</param>
        public DefaultTransactionSite(Component host)
        {
            Host = host;
        }

        public virtual string Name 
        {
            get { return "$" + RuntimeHelpers.GetHashCode(this).ToString(); }
        }

        /// <summary>
        /// Returns the neutral transaction which is executed whenever there is nothing to do.
        /// You must override this method in your specialization.
        /// </summary>
        [StaticEvaluation]
        public abstract IEnumerable<TAVerb> DoNothing();

        /// <summary>
        /// Connects the underlying hardware to its environment by binding its ports to signals
        /// created by a binder service. The default implementation does nothing.
        /// </summary>
        /// <param name="binder">binder service</param>
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

        /// <summary>
        /// Creates a transaction verb.
        /// </summary>
        /// <param name="tmode">mode</param>
        /// <param name="during">one or multiple processes describing the active register transfers of the created verb</param>
        protected TAVerb Verb(ETVMode tmode, params IProcess[] during)
        {
            return Verb(tmode, () => { }, during);
        }
    }
}

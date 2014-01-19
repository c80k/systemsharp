/**
 * Copyright 2011-2014 Christian Köllner, David Hlavac
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
using System.Text;
using System.Threading.Tasks;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// This is the abstract base class for channels.
    /// </summary>
    /// <remarks>
    /// A channel encapsulates communication whereas a component models computation. The methods of a channel might
    /// be called concurrently from multiple processes, so the implementor must cater for thread-safety.
    /// </remarks>
    public abstract class Channel :
        DesignObject,
        IDescriptive<ChannelDescriptor, Channel>
    {
        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public Channel()
        {
        }

        /// <summary>
        /// Creates a SysDOM descriptor for this kind of channel. You must implement this method in you concrete channel class.
        /// </summary>
        protected abstract ChannelDescriptor CreateDescriptor();

        /// <summary>
        /// Returns the SysDOM descriptor describing this channel instance.
        /// </summary>
        private ChannelDescriptor _descriptor;
        public ChannelDescriptor Descriptor
        {
            get
            {
                if (_descriptor == null)
                    _descriptor = CreateDescriptor();
                return _descriptor;
            }
        }

        /// <summary>
        /// Returns the SysDOM descriptor describing this channel instance.
        /// </summary>
        IDescriptor IDescriptive.Descriptor
        {
            get { return Descriptor; }
        }
    }

    /// <summary>
    /// A complex channel is (similiar to SystemC) a channel which contains a component. The latter models
    /// some behavior which is implemented by this channel.
    /// </summary>
    public abstract class ComplexChannel : Channel
    {
        /// <summary>
        /// Creates the component which represents the channel's internal behavior.
        /// You must override this method in your concrete complex channel implementation class.
        /// </summary>
        protected abstract Component CreateInternalBehavior();

        /// <summary>
        /// Returns the component which represents the internal behavior of this channel.
        /// </summary>
        private Component _internalBehavior;
        public Component InternalBehavior
        {
            get { return _internalBehavior; }
        }

        private void SetupInternalBehavior()
        {
            _internalBehavior = CreateInternalBehavior();
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        public ComplexChannel()
        {
            Context.OnAnalysis += OnAnalysis;
            Context.OnEndOfConstruction += SetupInternalBehavior;
        }

        protected virtual void OnAnalysis()
        {
            InternalBehavior.Descriptor.Nest(Descriptor, "InternalBehavior");
        }
    }
}

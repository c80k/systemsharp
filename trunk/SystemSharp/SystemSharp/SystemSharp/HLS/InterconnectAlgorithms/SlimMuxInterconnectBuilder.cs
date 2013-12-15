/**
 * Copyright 2011-2012 Christian Köllner
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
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler.DesignGen
{
    /// <summary>
    /// This interconnect builder is based on the assumption that the fan-in of the largest multiplexer limits the design performance.
    /// It routes data transfers over potentially multiple chained registers, such that many small multiplexers are preferred over
    /// few large multiplexers.
    /// </summary>
    public class SlimMuxInterconnectBuilder: IInterconnectBuilder
    {
        private class FactoryImpl : IInterconnectBuilderFactory
        {
            public IInterconnectBuilder Create(Component host, IAutoBinder binder)
            {
                return new SlimMuxInterconnectBuilder(host, binder);
            }
        }

        /// <summary>
        /// Returns a factory for creating instances of this class.
        /// </summary>
        public static readonly IInterconnectBuilderFactory Factory = new FactoryImpl();

        private Component _host;
        private IAutoBinder _binder;
        private SlimMuxInterconnectHelper _smih;

        private SlimMuxInterconnectBuilder(Component host, IAutoBinder binder)
        {
            _host = host;
            _binder = binder;
            _smih = new SlimMuxInterconnectHelper(binder);
        }

        public void CreateInterconnect(FlowMatrix flowSpec, FlowMatrix detailedFlow)
        {
            flowSpec.Transitize();
            var tflows = flowSpec.GetTimedFlows();
            var sflows = tflows
                .Select(tf => tf as TimedSignalFlow)
                .Where(tf => tf != null);
            SlimMux<int, int, TimedSignalFlow>.ConstructInterconnect(_smih, sflows);
            var pipeen = _smih.ComputePipeEnMatrix(flowSpec.NumCSteps);
            var pipes = _smih.InstantiatePipes();
            int idx = 0;
            foreach (RegPipe rp in pipes)
            {
                string id = "icpipe" + idx;
                _host.Descriptor.AddChild(rp.Descriptor, id);
                ++idx;
            }
            var pflows = _smih.ToFlow(flowSpec.NumCSteps, flowSpec.NeutralFlow, pipeen);
            var vflows = tflows
                .Select(tf => tf as TimedValueFlow)
                .Where(tf => tf != null);


            detailedFlow.AddNeutral(flowSpec.NeutralFlow);
            foreach (var vf in vflows)
            {
                detailedFlow.Add((int)vf.Time, new ValueFlow(vf.Value, vf.Target));
            }
            int cstep = 0;
            foreach (var pf in pflows)
            {
                detailedFlow.Add(cstep, pf);
                cstep++;
            }

            _host.Descriptor.GetDocumentation().Documents.Add(
                new Document("SlimMuxInterconnectGraph.dotty",
                    _smih.GetInterconnectGraphForDotty()));
        }
    }
}

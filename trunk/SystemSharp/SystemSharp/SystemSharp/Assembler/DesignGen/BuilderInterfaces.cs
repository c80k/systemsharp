/**
 * Copyright 2012 Christian Köllner
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
using SystemSharp.Analysis.M2M;
using SystemSharp.Components;

namespace SystemSharp.Assembler.DesignGen
{
    public interface IInterconnectBuilderFactory
    {
        IInterconnectBuilder Create(Component host, IAutoBinder binder);
    }

    public interface IInterconnectBuilder
    {
        void CreateInterconnect(FlowMatrix flowSpec, FlowMatrix detailedFlow);
    }

    public interface IControlpathBuilderFactory
    {
        IControlpathBuilder Create(Component host, IAutoBinder binder);
    }

    public interface IControlpathBuilder
    {
        void PersonalizePlan(HLSPlan plan);
        void PrepareAllocation(long cstepCount);
        void CreateControlpath(FlowMatrix flowSpec, string procName);
    }

    public interface IDatapathBuilderFactory
    {
        IDatapathBuilder Create(Component host);
    }

    public interface IDatapathBuilder
    {
        IAutoBinder FUBinder { get; }
        IAutoBinder ICBinder { get; }
        void AddFU(Component fu);
    }
}

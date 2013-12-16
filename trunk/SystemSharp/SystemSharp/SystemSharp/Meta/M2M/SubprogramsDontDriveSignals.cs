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
using System.Linq;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Analysis.Msil;
using SystemSharp.Components;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Transformations;

namespace SystemSharp.Meta.M2M
{
    /// <summary>
    /// This transformation rewrites the design, such that no method drives a signal which is not declared in its argument
    /// list. This is accomplished by promoting any driven signal to a new method parameter and adapting all calls of that method.
    /// </summary>
    public class SubprogramsDontDriveSignals: MetaTransformation
    {
        private IEnumerable<SignalArgumentDescriptor> InspectMethod(MethodDescriptor md)
        {
            List<Statement> stmts = md.Implementation.Body.GetAtomicStatements();
            IEnumerable<StoreStatement> stores = stmts.Select(s => s as StoreStatement).Where(s => s != null);
            Dictionary<ISignalOrPortDescriptor, SignalArgumentDescriptor> map =
                new Dictionary<ISignalOrPortDescriptor, SignalArgumentDescriptor>();
            int order = md.GetArguments().Count();
            foreach (StoreStatement stmt in stores)
            {
                SignalRef sref = stmt.Container as SignalRef;
                if (sref == null)
                    continue;
                if (sref.Desc is SignalArgumentDescriptor)
                    continue;
                SignalArgumentDescriptor sad;
                if (!map.TryGetValue(sref.Desc, out sad))
                {
                    string name = "a_" + sref.Desc.Name;
                    SignalDescriptor sd = sref.Desc as SignalDescriptor;
                    PortDescriptor pd = sref.Desc as PortDescriptor;
                    SignalBase signalInst;
                    if (pd != null)
                        signalInst = ((SignalDescriptor)pd.BoundSignal).Instance;
                    else
                        signalInst = sd.Instance;
                    ArgumentDescriptor.EArgDirection flowDir;
                    if (pd == null)
                    {
                        flowDir = ArgumentDescriptor.EArgDirection.InOut;
                    }
                    else
                    {
                        switch (pd.Direction)
                        {
                            case EPortDirection.In:
                                flowDir = ArgumentDescriptor.EArgDirection.In;
                                break;
                            case EPortDirection.InOut:
                                flowDir = ArgumentDescriptor.EArgDirection.InOut;
                                break;
                            case EPortDirection.Out:
                                flowDir = ArgumentDescriptor.EArgDirection.Out;
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    sad = new SignalArgumentDescriptor(
                        SignalRef.Create(sref.Desc, SignalRef.EReferencedProperty.Instance),
                        ArgumentDescriptor.EArgDirection.In,
                        flowDir,
                        EVariability.Constant,
                        order++);
                    map[sref.Desc] = sad;
                }
                SignalRef srefArg = new SignalRef(sad, sref.Prop, sref.Indices, sref.IndexSample, sref.IsStaticIndex);
                stmt.Container = srefArg;
            }
            foreach (SignalArgumentDescriptor sad in map.Values)
                md.AddChild(sad, sad.Argument.Name);
            return map.Values.OrderBy(k => k.Order);
        }

        private void AdaptCalls(CodeDescriptor cd, Dictionary<object, IEnumerable<SignalArgumentDescriptor>> map)
        {
            List<Statement> stmts = cd.Implementation.Body.GetAtomicStatements();
            IEnumerable<CallStatement> calls = stmts.Select(s => s as CallStatement).Where(s => s != null);
            ComponentDescriptor owner = (ComponentDescriptor)cd.Owner;
            foreach (CallStatement stmt in calls)
            {
                var fspec = stmt.Callee as FunctionSpec;
                if (fspec == null)
                    continue;
                object key;
                /*if (fspec.SysDOMRep != null)
                    key = fspec.SysDOMRep;
                else*/
                if (fspec.CILRep != null)
                    key = fspec.CILRep;
                else
                    continue;
                IEnumerable<SignalArgumentDescriptor> addArgs;
                if (!map.TryGetValue(key, out addArgs))
                    continue;
                List<Expression> args = stmt.Arguments.ToList();
                foreach (SignalArgumentDescriptor sad in addArgs)
                { 
                    SignalBase sigInst = (SignalBase)sad.Sample;
                    var sigRef = sigInst.Descriptor
                        .AsSignalRef(SignalRef.EReferencedProperty.Instance)
                        .RelateToComponent(owner);
                    if (sigRef == null)
                        throw new InvalidOperationException("Signal not found in local component");
                    args.Add(sigRef);
                }
                stmt.Arguments = args.ToArray();
            }
        }

        public override void ApplyTo(DesignContext design)
        {
            foreach (ComponentDescriptor cd in design.Components.Select(c => c.Descriptor))
            {
                Dictionary<object, IEnumerable<SignalArgumentDescriptor>> map = 
                    new Dictionary<object, IEnumerable<SignalArgumentDescriptor>>();
                foreach (MethodDescriptor md in cd.GetMethods())
                {
                    IEnumerable<SignalArgumentDescriptor> addArgs = InspectMethod(md);
                    //map[md.Implementation] = addArgs;
                    map[md.Method] = addArgs;
                }
                foreach (MethodDescriptor md in cd.GetMethods())
                {
                    AdaptCalls(md, map);
                }
                foreach (ProcessDescriptor pd in cd.GetProcesses())
                {
                    AdaptCalls(pd, map);
                }
            }
        }
    }
}

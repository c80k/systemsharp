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
 * 
 * */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.SysDOM;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Interop.Xilinx
{
    public abstract class AbstractXilinxDevice
    {
        public abstract EDevice Device { get; }
        public abstract EPackage Package { get; }
        public ESpeedGrade SpeedGrade { get; set; }

        public Component TopLevelComponent { get; set; }
        public List<Component> Testbenches { get; private set; }

        public abstract IPropMap<string, XilinxPin> Pins { get; }
        public abstract IEnumerable<XilinxPin> PinList { get; }

        public AbstractXilinxDevice()
        {
            Testbenches = new List<Component>();
        }

        private IEnumerable<IComponentDescriptor> GetComponentSet()
        {
            HashSet<IComponentDescriptor> set = new HashSet<IComponentDescriptor>();
            if (TopLevelComponent != null)
                set.AddRange(TopLevelComponent.Descriptor.GetAllAncestors());
            foreach (Component tb in Testbenches)
                set.AddRange(tb.Descriptor.GetAllAncestors());
            return set;
        }

        private void CreateUCF(XilinxProject proj)
        {
            string path = proj.AddFile(proj.ProjectName + ".ucf");
            var sw = new StreamWriter(path);
            foreach (var pin in PinList)
            {
                if (pin.AssociatedSignal == null)
                    continue;

                string indexExpr = "";
                if (pin.AssociatedIndex.Length > 0)
                    indexExpr = pin
                     .AssociatedIndex
                     .Select(i => "[" + i + "]")
                     .Aggregate((x, y) => x + y);

                var sd = (ISignalOrPortDescriptor)((IDescriptive)pin.AssociatedSignal).Descriptor;
                var pd = sd.AsSignalRef(SignalRef.EReferencedProperty.Instance)
                    .RelateToComponent(TopLevelComponent.Descriptor)
                    .Desc;

                sw.WriteLine("NET \"{0}{1}\" LOC = {2};",
                    pd.Name, indexExpr, pin.Name);
            }
            foreach (IPortDescriptor pd in TopLevelComponent.Descriptor.GetPorts())
            {
                var sd = pd.BoundSignal;
                if (sd == null)
                    continue;

                var csa = sd.QueryAttribute<ClockSpecAttribute>();
                if (csa == null)
                    continue;

                sw.WriteLine("TIMESPEC TS_{0} = PERIOD \"{1}\" {2} {3};",
                    pd.Name, pd.Name, csa.Period.Value, csa.Period.Unit);
                sw.WriteLine("NET \"{0}\" TNM_NET = \"{1}\";",
                    pd.Name, pd.Name);
            }
            sw.Close();
        }

        public XilinxProject Synthesize(string destPath, string designName, ISEInfo info,
            IProject twinProject = null, EFlowStep step = EFlowStep.HDLGenAndIPCores)
        {
            // Now convert the design to VHDL and embed it into a Xilinx ISE project
            XilinxProject project = new XilinxProject(destPath, designName)
            {
                TwinProject = twinProject
            };

            project.ISEVersion = info.VersionTag;
            if (info.Path == null)
                project.SkipIPCoreSynthesis = true;
            else
                project.ISEBinPath = info.Path;

            project.PutProperty(EXilinxProjectProperties.DeviceFamily, Device.GetFamily());
            project.PutProperty(EXilinxProjectProperties.Device, Device);
            project.PutProperty(EXilinxProjectProperties.Package, Package);
            project.PutProperty(EXilinxProjectProperties.SpeedGrade, SpeedGrade);
            project.SetVHDLProfile();
            if (!step.HasFlag(EFlowStep.IPCores))
                project.SkipIPCoreSynthesis = true;
            project.TopLevelComponent = TopLevelComponent.Descriptor;
            CreateUCF(project);

            VHDLGenerator codeGen = new VHDLGenerator();
            SynthesisEngine.Create(DesignContext.Instance, project).Synthesize(GetComponentSet(), codeGen);
            project.Save();
            if (step.HasFlag(EFlowStep.XST) ||
                step.HasFlag(EFlowStep.NGDBuild) ||
                step.HasFlag(EFlowStep.Map) ||
                step.HasFlag(EFlowStep.PAR) ||
                step.HasFlag(EFlowStep.TRCE))
            {
                var flow = project.ConfigureFlow(TopLevelComponent);
                flow.Start(step);
            }
            return project;
        }

        public XilinxProject Synthesize(string destPath, string designName, EISEVersion iseVersion, 
            IProject twinProject = null, EFlowStep step = EFlowStep.HDLGenAndIPCores)
        {
            ISEInfo info = ISEDetector.LocateISEByVersion(iseVersion);
            if (info == null)
                info = new ISEInfo() { VersionTag = iseVersion };
            return Synthesize(destPath, designName, info, twinProject, step);
        }

        public XilinxProject Synthesize(string destPath, string designName, IProject twinProject = null, EFlowStep step = EFlowStep.IPCores)
        {
            EISEVersion iseVersion = EISEVersion._11_2;
            ISEInfo info = ISEDetector.DetectMostRecentISEInstallation();
            if (info != null)
                iseVersion = info.VersionTag;
            return Synthesize(destPath, designName, iseVersion, twinProject, step);
        }
    }


    public class XilinxPin
    {
        public string Name { get; private set; }
        public IPort AssociatedSignal { get; set; }
        public int[] AssociatedIndex { get; set; }

        public XilinxPin(string name)
        {
            Name = name;
            AssociatedIndex = new int[0];
        }

        public void Map(IPort signal, params int[] index)
        {
            AssociatedSignal = signal;
            AssociatedIndex = index;
        }
    }
}

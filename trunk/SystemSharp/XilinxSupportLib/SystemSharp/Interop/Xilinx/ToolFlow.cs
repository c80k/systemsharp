/**
 * Copyright 2012-2013 Christian Köllner
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
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Interop.Xilinx.MAP;
using SystemSharp.Interop.Xilinx.NGDBuild;
using SystemSharp.Interop.Xilinx.PAR;
using SystemSharp.Interop.Xilinx.TRCE;
using SystemSharp.Interop.Xilinx.XST;
using SystemSharp.Synthesis.VHDLGen;

namespace SystemSharp.Interop.Xilinx
{
    /// <summary>
    /// Represents the FPGA tool flow from synthesis to place-and-route.
    /// </summary>
    public class ToolFlow
    {
        /// <summary>
        /// The ISE project
        /// </summary>
        public XilinxProject Project { get; private set; }

        /// <summary>
        /// XST flow
        /// </summary>
        public XSTFlow XST { get; private set; }

        /// <summary>
        /// Gets or sets the XST script path.
        /// </summary>
        public string XSTScriptPath { get; set; }

        /// <summary>
        /// Gets or sets the XST logging path.
        /// </summary>
        public string XSTLogPath { get; set; }

        /// <summary>
        /// NGDBuild flow
        /// </summary>
        public NGDBuildFlow NGDBuild { get; private set; }

        /// <summary>
        /// Map flow
        /// </summary>
        public MAPFlow Map { get; private set; }

        /// <summary>
        /// PAR flow
        /// </summary>
        public PARFlow PAR { get; private set; }

        /// <summary>
        /// TRCE flow
        /// </summary>
        public TRCEFlow TRCE { get; private set; }

        /// <summary>
        /// PAR report path
        /// </summary>
        public string PARReportPath { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="project">ISE project</param>
        public ToolFlow(XilinxProject project)
        {
            Project = project;
            XST = new XSTFlow();
            NGDBuild = new NGDBuildFlow();
            Map = new MAPFlow();
            PAR = new PARFlow();
            TRCE = new TRCEFlow();
        }

        /// <summary>
        /// Configures the flow for the given component.
        /// </summary>
        /// <param name="top">top-level component of the design</param>
        public void Configure(Component top)
        {
            string flowRoot = Path.Combine(Project.ProjectPath, "flow");
            Directory.CreateDirectory(flowRoot);
            string xstRoot = Path.Combine(flowRoot, "xst");
            Directory.CreateDirectory(xstRoot);
            string xstProjPath = Path.Combine(xstRoot, "input.prj");
            var xstproj = new XSTProject(xstProjPath);
            string ucf = null;
            foreach (string file in Project.ProjectFiles)
            {
                string xstFile = file;
                string ext = Path.GetExtension(file);
                if (ext.Equals(".xco"))
                {
                    xstFile = Path.GetFileNameWithoutExtension(file) + ".vhd";
                }
                else if (ext.Equals(".ucf"))
                {
                    ucf = file;
                    continue;
                }
                xstproj.AddFile(xstFile);
            }
            xstproj.Save();
            string partName = Tooling.MakePartName(
                Project.Device, 
                Project.SpeedGrade, 
                Project.Package);
            XST.PartName = partName;
            string xstTempDir = Path.Combine(xstRoot, "inter");
            Directory.CreateDirectory(xstTempDir);
            XST.TempDir = xstTempDir;
            XST.XSTProjectPath = xstProjPath;
            XST.XstHdpDir = Project.ProjectPath;
            var gi = top.Descriptor.QueryAttribute<VHDLGenInfo>();
            XST.TopLevelUnitName = gi.EntityName;
            var bat = ProcessPool.Instance.CreateBatch();
            string xstScriptPath = Path.Combine(xstRoot, "synthesis.xst");
            string ngcPath = Path.Combine(xstRoot, "design.ngc");
            string logPath = Path.Combine(xstRoot, "synthesis.log");
            XST.OutputFile = ngcPath;
            XSTScriptPath = xstScriptPath;
            XSTLogPath = logPath;
            string ngdRoot = Path.Combine(flowRoot, "ngd");
            Directory.CreateDirectory(ngdRoot);
            string ngdTempDir = Path.Combine(ngdRoot, "inter");
            Directory.CreateDirectory(ngdTempDir);
            NGDBuild.PartName = partName;
            if (ucf != null)
                NGDBuild.UserConstraintsFile = Path.Combine(Project.ProjectPath, ucf);
            NGDBuild.DesignName = ngcPath;
            NGDBuild.IntermediateDir = ngdTempDir;
            string ngdFile = Path.Combine(ngdRoot, "design.ngd");
            NGDBuild.SearchDirs.Add(xstRoot);
            NGDBuild.NGDFile = ngdFile;
            string mapRoot = Path.Combine(flowRoot, "map");
            Directory.CreateDirectory(mapRoot);
            string ncdFile = Path.Combine(mapRoot, "design.ncd");
            string mapPcfFile = Path.Combine(mapRoot, "design.pcf");
            Map.PartName = partName;
            Map.InputFile = ngdFile;
            Map.OutputFile = ncdFile;
            string parRoot = Path.Combine(flowRoot, "par");
            string parNcdFile = Path.Combine(parRoot, "design.ncd");
            Directory.CreateDirectory(parRoot);
            PAR.InputFile = ncdFile;
            PAR.OutputFile = parNcdFile;
            PARReportPath = Path.Combine(parRoot, "design.par");
            TRCE.PhysicalConstraintsFile = mapPcfFile;
            TRCE.UserConstraintsFile = ucf;
            TRCE.PhysicalDesignFile = parNcdFile;
            string trceRoot = Path.Combine(flowRoot, "trce");
            Directory.CreateDirectory(trceRoot);
            string twrPath = Path.Combine(trceRoot, "design.twr");
            string twxPath = Path.Combine(trceRoot, "design.twx");
            TRCE.ReportFile = twrPath;
            TRCE.XMLReportFile = twxPath;
        }

        private ProcessPool.ToolBatch CreateBatch(EFlowStep steps)
        {
            var bat = ProcessPool.Instance.CreateBatch();
            if (steps.HasFlag(EFlowStep.XST))
                XST.SaveToXSTScriptAndAddToBatch(Project, bat, XSTScriptPath, XSTLogPath);
            if (steps.HasFlag(EFlowStep.NGDBuild))
                NGDBuild.AddToBatch(Project, bat);
            if (steps.HasFlag(EFlowStep.Map))
                Map.AddToBatch(Project, bat);
            if (steps.HasFlag(EFlowStep.PAR))
                PAR.AddToBatch(Project, bat);
            if (steps.HasFlag(EFlowStep.TRCE))
                TRCE.AddToBatch(Project, bat);
            return bat;
        }

        /// <summary>
        /// Queues the flow in the process pool.
        /// </summary>
        /// <param name="steps">flow items to execute</param>
        /// <returns>the created batch job</returns>
        public ProcessPool.ToolBatch Start(EFlowStep steps)
        {
            var bat = CreateBatch(steps);
            Project.AwaitRunningToolsToFinish();
            bat.Start();
            Project.AddRunningTool(bat);
            return bat;
        }

        /// <summary>
        /// Parses the generated resource reports.
        /// </summary>
        /// <param name="designRec">out parameter to receive the parsed performance data</param>
        /// <param name="deviceRec">out parameter to receive the parsed resource data</param>
        public void ParseResourceRecords(out PerformanceRecord designRec, out ResourceRecord deviceRec)
        {
            designRec = new PerformanceRecord();
            deviceRec = new ResourceRecord();

            UtilizationParser.ParseUtilization(PARReportPath, designRec);
            UtilizationParser.ParseTotals(PARReportPath, deviceRec);
            TWXParser.ParseMinPeriod(TRCE.XMLReportFile, designRec);
        }
    }
}

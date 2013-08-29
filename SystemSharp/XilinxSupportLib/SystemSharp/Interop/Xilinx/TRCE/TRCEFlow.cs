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
using System.Linq;
using System.Text;

namespace SystemSharp.Interop.Xilinx.TRCE
{
    public class TRCEFlow
    {
        public string PhysicalDesignFile { get; set; }
        public string PhysicalConstraintsFile { get; set; }
        public string UserConstraintsFile { get; set; }
        public string ReportFile { get; set; }
        public string XMLReportFile { get; set; }
        public bool ErrorReport { get; set; }
        public bool VerboseReport { get; set; }
        public int ReportLimit { get; set; }
        public bool TimingReport { get; set; }
        public int TimingReportLimit { get; set; }
        public bool ReportPathsPerEndpoint { get; set; }
        public int EndpointsLimit { get; set; }
        public int SpeedGrade { get; set; }
        public bool AdvancedAnalysis { get; set; }
        public bool ReportUnconstrainedPaths { get; set; }
        public int UnconstrainedPathsLimit { get; set; }
        public string StampFile { get; set; }
        public string TSIFile { get; set; }
        public bool NoDatasheet { get; set; }
        public bool TimegroupsSection { get; set; }
        public bool ReportFastestPaths { get; set; }
        public string FilterFile { get; set; }
        public bool TurnOffPackageFlightDelay { get; set; }
        public string ISEProjectFile { get; set; }

        public TRCEFlow()
        {
            ErrorReport = false;
            VerboseReport = true;
            ReportLimit = 5;
            TimingReport = true;
            TimingReportLimit = 5;
            ReportPathsPerEndpoint = false;
            EndpointsLimit = 5;
            AdvancedAnalysis = false;
            ReportUnconstrainedPaths = true;
            UnconstrainedPathsLimit = 5;
        }

        public ProcessPool.Tool AddToBatch(XilinxProject proj, ProcessPool.ToolBatch batch)
        {
            var cmd = new StringBuilder();
            if (VerboseReport || ErrorReport)
            {
                if (VerboseReport)
                    cmd.Append("-v ");
                else
                    cmd.Append("-e ");
                cmd.Append(ReportLimit);
            }
            if (TimingReport)
            {
                cmd.Append(" -l " + TimingReportLimit);
            }
            if (ReportPathsPerEndpoint)
            {
                cmd.Append(" -n " + EndpointsLimit);
            }
            if (SpeedGrade > 0)
            {
                cmd.Append(" -s " + SpeedGrade);
            }
            if (AdvancedAnalysis)
            {
                cmd.Append(" -a");
            }
            if (ReportUnconstrainedPaths)
            {
                cmd.Append(" -u " + UnconstrainedPathsLimit);
            }
            if (ReportFile != null)
            {
                cmd.Append(" -o \"" + ReportFile + "\"");
            }
            if (StampFile != null)
            {
                cmd.Append(" -stamp \"" + StampFile + "\"");
            }
            if (TSIFile != null)
            {
                cmd.Append(" -tsi \"" + TSIFile + "\"");
            }
            if (XMLReportFile != null)
            {
                cmd.Append(" -xml \"" + XMLReportFile + "\"");
            }
            if (NoDatasheet)
            {
                cmd.Append(" -nodatasheet");
            }
            if (TimegroupsSection)
            {
                cmd.Append(" -timegroups");
            }
            if (ReportFastestPaths)
            {
                cmd.Append(" -fastpaths");
            }
            if (FilterFile != null)
            {
                cmd.Append(" -filter \"" + FilterFile + "\"");
            }
            if (TurnOffPackageFlightDelay)
            {
                cmd.Append(" -noflight");
            }
            cmd.Append(" -intstyle silent");
            if (ISEProjectFile != null)
            {
                cmd.Append(" -ise \"" + ISEProjectFile + "\"");
            }
            cmd.Append(" \"" + PhysicalDesignFile + "\"");
            if (PhysicalConstraintsFile != null)
                cmd.Append(" \"" + PhysicalConstraintsFile + "\"");
            if (UserConstraintsFile != null)
                cmd.Append(" -ucf \"" + UserConstraintsFile + "\"");
            return batch.Add(proj.ISEBinPath, proj.ProjectPath, "trce", cmd.ToString());
        }
    }
}

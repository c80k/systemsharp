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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.Interop.Xilinx.CoreGen;
using SystemSharp.Interop.Xilinx.MAP;
using SystemSharp.Interop.Xilinx.NGDBuild;
using SystemSharp.Interop.Xilinx.PAR;
using SystemSharp.Interop.Xilinx.TRCE;
using SystemSharp.Interop.Xilinx.XST;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;

namespace SystemSharp.Interop.Xilinx
{
    [Flags]
    public enum EFlowStep
    {
        HDLGen = 0x1,
        IPCores = 0x2,
        XST = 0x4,
        NGDBuild = 0x8,
        Map = 0x10,
        PAR = 0x20,
        TRCE = 0x40,

        HDLGenAndIPCores = HDLGen | IPCores,
        SynthImplReport = XST | NGDBuild | Map | PAR | TRCE,
        All = HDLGenAndIPCores | SynthImplReport
    }

    public class XilinxProject : IProject
    {
        private static Dictionary<string, string> _fileExtToFileType = new Dictionary<string, string>();

        static XilinxProject()
        {
            _fileExtToFileType[".vhd"] = "FILE_VHDL";
            _fileExtToFileType[".xco"] = "FILE_COREGEN";
        }

        private static string GetFileType(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            string result = "";
            _fileExtToFileType.TryGetValue(ext, out result);
            return result;
        }

        private static string MakeUNIXPath(string file)
        {
            return file.Replace('\\', '/');
        }

        public XilinxProject(string projectPath, string projectName)
        {
            if (!Directory.Exists(projectPath))
                Directory.CreateDirectory(projectPath);

            ProjectPath = projectPath;
            ProjectName = projectName;
            ISEInfo info = ISEDetector.DetectMostRecentISEInstallation();
            if (info != null)
            {
                ISEVersion = info.VersionTag;
                ISEBinPath = info.Path;
            }
            PreInitializeProperties();
        }

        private PropertyBag _pbag = new PropertyBag();
        internal PropertyBag PBag
        {
            get { return _pbag; }
        }

        public string ProjectPath { get; private set; }
        public string ProjectName { get; private set; }
        public IProject TwinProject { get; set; }

        public Dictionary<EXilinxProjectProperties, object> Properties
        {
            get { return _pbag.Properties; }
        }

        public EISEVersion ISEVersion { get; set; }
        public string ISEBinPath { get; set; }

        private List<string> _projectFiles = new List<string>();
        public IList<string> ProjectFiles
        {
            get { return new ReadOnlyCollection<string>(_projectFiles); }
        }

        private Dictionary<string, HashSet<object>> _fileAttributes =
            new Dictionary<string, HashSet<object>>();
        private Queue<ProcessPool.ToolBatch> _runningTools = 
            new Queue<ProcessPool.ToolBatch>();

        public object GetProperty(EXilinxProjectProperties prop)
        {
            return _pbag.GetProperty(prop);
        }

        public void PutProperty(EXilinxProjectProperties prop, object value)
        {
            _pbag.PutProperty(prop, value);
        }

        public string AddFile(string file)
        {
            if (!_projectFiles.Contains(file))
                _projectFiles.Add(file);
            if (TwinProject != null)
                TwinProject.AddFile(file);
            return MakeFullPath(file);
        }

        public void AddFileAttribute(string file, object attr)
        {
            _fileAttributes.Add(file, attr);
            if (TwinProject != null)
                TwinProject.AddFileAttribute(file, attr);
        }

        public bool RemoveFile(string file)
        {
            if (TwinProject != null)
                throw new InvalidOperationException("File removal not allowed since twin project is set");

            return _projectFiles.Remove(file);
        }

        public string MakeFullPath(string file)
        {
            return ProjectPath + "\\" + file;
        }

        private CoreGenDescription CreateCoreGenFile(string path, EPropAssoc assoc)
        {
            CoreGenDescription cdesc = new CoreGenDescription(path);
            cdesc.FromProject(this, assoc);
            return cdesc;
        }

        public void AddNewCoreGenDescription(string name, out CoreGenDescription cgProj, out CoreGenDescription xco)
        {
            string cgprojPath = MakeFullPath(name + ".cgp");
            cgProj = CreateCoreGenFile(cgprojPath, EPropAssoc.CoreGenProj);
            cgProj.Store();

            string xcoFile = name + ".xco";
            AddFile(xcoFile);
            string xcoPath = MakeFullPath(xcoFile);
            var cdesc = CreateCoreGenFile(xcoPath, EPropAssoc.CoreGen);
            AddFileAttribute(xcoFile, cdesc);
            xco = cdesc;

            if (TwinProject != null)
            {
                string vhdFile = name + ".vhd";
                TwinProject.AddFile(vhdFile);
            }
        }

        public void SetVHDLProfile()
        {
            PutProperty(EXilinxProjectProperties.PreferredLanguage, EHDL.VHDL);
            PutProperty(EXilinxProjectProperties.DesignFlow, EDesignFlow.VHDL);
            PutProperty(EXilinxProjectProperties.SimulationOutputProducts, EHDL.VHDL);
            PutProperty(EXilinxProjectProperties.VHDLSim, true);
            PutProperty(EXilinxProjectProperties.FunctionalModelTargetLanguageArchWiz, EHDL.VHDL);
            PutProperty(EXilinxProjectProperties.FunctionalModelTargetLanguageCoreGen, EHDL.VHDL);
            PutProperty(EXilinxProjectProperties.FunctionalModelTargetLanguageSchematic, EHDL.VHDL);
        }

        private string CreateProjectID()
        {
            Random rand = new Random(DateTime.Now.Second);
            byte[] buffer = new byte[16];
            rand.NextBytes(buffer);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in buffer)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        private void PreInitializeProperties()
        {
            Properties[EXilinxProjectProperties.PROP_DesignName] = ProjectName;
            Properties[EXilinxProjectProperties.PROP_intProjectCreationTimestamp] = DateTime.Now.ToString("O");
            Properties[EXilinxProjectProperties.PROP_intWbtProjectID] = CreateProjectID();
        }

        private void PostInitializeProperties()
        {
            Properties[EXilinxProjectProperties.PROP_DevFamilyPMName] =
                PropEnum.ToString(
                    Properties[EXilinxProjectProperties.DeviceFamily], EPropAssoc.CoreGen);

            if (TopLevelComponent != null)
            {
                var gi = TopLevelComponent.QueryAttribute<VHDLGenInfo>();
                PutProperty(EXilinxProjectProperties.AutoImplementationTop, false);
                PutProperty(EXilinxProjectProperties.ImplementationTop, "Architecture|" + gi.EntityName + "|" + gi.DefaultArch);
                PutProperty(EXilinxProjectProperties.ImplementationTopInstancePath, VHDLGenInfo.GetInstancePath(TopLevelComponent));
                PutProperty(EXilinxProjectProperties.ImplementationTopFile, gi.FileName);
            }
        }

        public IEnumerable<T> LookupAttributes<T>(string file)
        {
            HashSet<object> attrs = _fileAttributes.Get(file);
            return from object attr in attrs
                   where attr is T
                   select (T)attr;
        }

        public T LookupAttribute<T>(string file)
        {
            return LookupAttributes<T>(file).SingleOrDefault();
        }

        public void Save()
        {
            PostInitializeProperties();

            string path = MakeFullPath(ProjectName + ".xise");
            XmlTextWriter wr = new XmlTextWriter(path, Encoding.UTF8);
            wr.Formatting = Formatting.Indented;
            wr.WriteStartDocument(false);
            //wr.WriteStartElement("project", "http://www.xilinx.com/XMLSchema");
            wr.WriteStartElement("project");
            wr.WriteAttributeString("xmlns", "http://www.xilinx.com/XMLSchema");
            wr.WriteAttributeString("xmlns:xil_pn", "http://www.xilinx.com/XMLSchema");
            wr.WriteStartElement("header");
            wr.WriteEndElement();

            wr.WriteStartElement("version");

            wr.WriteAttributeString("xil_pn:ise_version", PropEnum.ToString(ISEVersion, EPropAssoc.ISE));
            wr.WriteAttributeString("xil_pn:schema_version", "2");
            wr.WriteEndElement();

            var libraries = new HashSet<string>();
            string ucf = null;

            wr.WriteStartElement("files");
            foreach (string file in _projectFiles)
            {
                string type = GetFileType(file);
                wr.WriteStartElement("file");
                wr.WriteAttributeString("xil_pn:name", MakeUNIXPath(file));
                wr.WriteAttributeString("xil_pn:type", type);
                EComponentPurpose purpose = LookupAttribute<EComponentPurpose>(file);
                if (purpose == EComponentPurpose.SimulationAndSynthesis ||
                    purpose == EComponentPurpose.SimulationOnly)
                {
                    wr.WriteStartElement("association");
                    wr.WriteAttributeString("xil_pn:name", "BehavioralSimulation");
                    wr.WriteEndElement();
                }
                if (purpose == EComponentPurpose.SimulationAndSynthesis ||
                    purpose == EComponentPurpose.SynthesisOnly)
                {
                    wr.WriteStartElement("association");
                    wr.WriteAttributeString("xil_pn:name", "Implementation");
                    wr.WriteEndElement();
                }
                var library = LookupAttribute<LibraryAttribute>(file);
                if (library != null)
                {
                    wr.WriteStartElement("library");
                    wr.WriteAttributeString("xil_pn:name", library.Name);
                    libraries.Add(library.Name);
                    wr.WriteEndElement();
                }
                wr.WriteEndElement();
            }
            wr.WriteEndElement();

            wr.WriteStartElement("properties");
            IList<PropDesc> allProps = PropEnum.EnumProps(typeof(EXilinxProjectProperties));
            foreach (PropDesc pdesc in allProps)
            {
                string propID;
                if (!pdesc.IDs.TryGetValue(EPropAssoc.ISE, out propID))
                    continue;
                object propVal;
                //if (!Properties.TryGetValue(propID, out propVal))
                if (!Properties.TryGetValue((EXilinxProjectProperties)pdesc.EnumValue, out propVal))
                    propVal = pdesc.DefaultValue;
                bool isDefault = propVal.Equals(pdesc.DefaultValue);
                string propValStr = PropEnum.ToString(propVal, EPropAssoc.ISE);
                wr.WriteStartElement("property");
                wr.WriteAttributeString("xil_pn:name", propID);
                wr.WriteAttributeString("xil_pn:value", propValStr);
                wr.WriteAttributeString("xil_pn:valueState", isDefault ? "default" : "non-default");
                wr.WriteEndElement();
            }
            wr.WriteEndElement();

            wr.WriteStartElement("bindings");
            if (ucf != null && TopLevelComponent != null)
            {
                var gi = TopLevelComponent.QueryAttribute<VHDLGenInfo>();
                wr.WriteStartElement("binding");
                wr.WriteAttributeString("xil_pn:location", "/" + gi.EntityName);
                wr.WriteAttributeString("xil_pn:name", ucf);
                wr.WriteEndElement();
            }
            wr.WriteEndElement();

            wr.WriteStartElement("libraries");
            foreach (string lib in libraries)
            {
                wr.WriteStartElement("library");
                wr.WriteAttributeString("xil_pn:name", lib);
                wr.WriteEndElement();
            }
            wr.WriteEndElement();

            wr.WriteStartElement("autoManagedFiles");
            wr.WriteEndElement();

            wr.WriteEndElement();
            wr.Close();

            if (TwinProject != null)
                TwinProject.Save();
        }

        public void ExecuteCoreGen(string xcoPath, string cgprojPath)
        {
            //var cg = XilinxCoreGenPool.Instance.RequestCoreGen();
            var cg = new XilinxCoreGenerator();
            var batch = cg.Execute(this, xcoPath, cgprojPath);
            if (batch != null)
                _runningTools.Enqueue(batch);
        }

        public bool SkipIPCoreSynthesis { get; set; }

        private IComponentDescriptor _topLevelComponent;
        public IComponentDescriptor TopLevelComponent 
        {
            get { return _topLevelComponent; }
            set { _topLevelComponent = value; }
        }

        public EDeviceFamily DeviceFamily
        {
            get { return (EDeviceFamily)GetProperty(EXilinxProjectProperties.DeviceFamily); }
            set { PutProperty(EXilinxProjectProperties.DeviceFamily, value); }
        }

        public EDevice Device
        {
            get { return (EDevice)GetProperty(EXilinxProjectProperties.Device); }
            set { PutProperty(EXilinxProjectProperties.Device, value); }
        }


        public ESpeedGrade SpeedGrade
        {
            get { return (ESpeedGrade)GetProperty(EXilinxProjectProperties.SpeedGrade); }
            set { PutProperty(EXilinxProjectProperties.SpeedGrade, value); }
        }

        public EPackage Package
        {
            get { return (EPackage)GetProperty(EXilinxProjectProperties.Package); }
            set { PutProperty(EXilinxProjectProperties.Package, value); }
        }

        public void AwaitRunningToolsToFinish()
        {
            while (_runningTools.Any())
            {
                var batch = _runningTools.Dequeue();
                batch.Tools.Last().WaitForExit();
            }
        }

        internal void AddRunningTool(ProcessPool.ToolBatch bat)
        {
            _runningTools.Enqueue(bat);
        }

        public ToolFlow ConfigureFlow(Component top)
        {
            var flow = new ToolFlow(this);
            flow.Configure(top);
            return flow;
        }

#if false
        public void RunFlow(Component top, EFlowStep step)
        {
            AwaitRunningToolsToFinish();

            string xstRoot = Path.Combine(ProjectPath, "xst");
            Directory.CreateDirectory(xstRoot);
            string xstProjPath = Path.Combine(xstRoot, "input.prj");
            var xstproj = new XSTProject(xstProjPath);
            string ucf = null;
            foreach (string file in ProjectFiles)
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
            string partName = Tooling.MakePartName(Device, SpeedGrade, Package);
            var xst = new XSTFlow();
            xst.PartName = partName;
            string xstTempDir = Path.Combine(xstRoot, "inter");
            Directory.CreateDirectory(xstTempDir);
            xst.TempDir = xstTempDir;
            xst.XSTProjectPath = xstProjPath;
            xst.XstHdpDir = ProjectPath;
            xst.TopLevelUnitName = top.Descriptor.Name;
            var bat = ProcessPool.Instance.CreateBatch();
            string xstScriptPath = Path.Combine(xstRoot, "synthesis.xst");
            string ngcPath = Path.Combine(xstRoot, "design.ngc");
            string logPath = Path.Combine(xstRoot, "synthesis.log");
            xst.OutputFile = ngcPath;
            if (step.HasFlag(EFlowStep.XST))
                xst.SaveToXSTScriptAndAddToBatch(this, bat, xstScriptPath, logPath);
            string ngdRoot = Path.Combine(ProjectPath, "ngd");
            Directory.CreateDirectory(ngdRoot);
            string ngdTempDir = Path.Combine(ngdRoot, "inter");
            Directory.CreateDirectory(ngdTempDir);
            var ngdbuild = new NGDBuildFlow();
            ngdbuild.PartName = partName;
            if (ucf != null)
                ngdbuild.UserConstraintsFile = Path.Combine(ProjectPath, ucf);
            ngdbuild.DesignName = ngcPath;
            ngdbuild.IntermediateDir = ngdTempDir;
            string ngdFile = Path.Combine(ngdRoot, "design.ngd");
            ngdbuild.SearchDirs.Add(xstRoot);
            ngdbuild.NGDFile = ngdFile;
            if (step.HasFlag(EFlowStep.NGDBuild))
                ngdbuild.AddToBatch(this, bat);
            string mapRoot = Path.Combine(ProjectPath, "map");
            Directory.CreateDirectory(mapRoot);
            string ncdFile = Path.Combine(mapRoot, "design.ncd");
            var map = new MAPFlow();
            map.PartName = partName;                    
            map.InputFile = ngdFile;
            map.OutputFile = ncdFile;
            if (step.HasFlag(EFlowStep.Map))
                map.AddToBatch(this, bat);
            string parRoot = Path.Combine(ProjectPath, "par");
            string parNcdFile = Path.Combine(parRoot, "design.ncd");
            Directory.CreateDirectory(parRoot);
            var par = new PARFlow();
            par.InputFile = ncdFile;
            par.OutputFile = parNcdFile;
            if (step.HasFlag(EFlowStep.PAR))
                par.AddToBatch(this, bat);
            var trce = new TRCEFlow();
            trce.PhysicalDesignFile = parNcdFile;
            string trceRoot = Path.Combine(ProjectPath, "trce");
            Directory.CreateDirectory(trceRoot);
            string twrPath = Path.Combine(trceRoot, "design.twr");
            string twxPath = Path.Combine(trceRoot, "design.twx");
            trce.ReportFile = twrPath;
            trce.XMLReportFile = twxPath;
            if (step.HasFlag(EFlowStep.TRCE))
                trce.AddToBatch(this, bat);
            bat.Start();
            _runningTools.Enqueue(bat);
        }
#endif
    }

}

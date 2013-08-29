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
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;

namespace SystemSharp.Interop.Xilinx.CoreGen
{
    abstract class CoreGenCommand
    {
        private Type _type;

        public CoreGenCommand(Type type = null)
        {
            _type = type;
        }

        public Type CILType
        {
            get { return _type; }
        }

        public static CoreGenCommand Parse(string line)
        {
            CoreGenCommand cmd;
            cmd = SetCommand.TryParse(line);
            if (cmd != null)
                return cmd;
            cmd = CSetCommand.TryParse(line);
            if (cmd != null)
                return cmd;
            cmd = SelectCommand.TryParse(line);
            if (cmd != null)
                return cmd;
            cmd = GenerateCommand.TryParse(line);
            if (cmd != null)
                return cmd;

            // Not recognized
            return null;
        }
    }

    class SetCommand : CoreGenCommand
    {
        private static readonly Regex Pattern = new Regex(@"SET (?<attr>[^=]+) = (?<value>[^=]+)");

        public string AttrName { get; private set; }
        public string AttrValue { get; private set; }

        public static SetCommand TryParse(string line)
        {
            var match = Pattern.Match(line);
            if (!match.Success)
                return null;
            return new SetCommand(
                match.Result("${attr}"),
                match.Result("${value}"));
        }

        internal SetCommand(string attrName, string attrValue, Type type = null):
            base(type)
        {
            AttrName = attrName;
            AttrValue = attrValue;
        }

        public override string ToString()
        {
            return "SET " + AttrName + " = " + AttrValue;
        }

        public override bool Equals(object obj)
        {
            var other = obj as SetCommand;
            if (other == null)
                return false;

            return AttrName == other.AttrName &&
                AttrValue == other.AttrValue;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }

    class CSetCommand : CoreGenCommand
    {
        private static readonly Regex Pattern = new Regex(@"CSET (?<attr>[^=]+) = (?<value>[^=]+)");

        public string AttrName { get; private set; }
        public string AttrValue { get; private set; }

        public static CSetCommand TryParse(string line)
        {
            var match = Pattern.Match(line);
            if (!match.Success)
                return null;
            return new CSetCommand(
                match.Result("${attr}"),
                match.Result("${value}"));
        }

        internal CSetCommand(string attrName, string attrValue, Type type = null):
            base(type)
        {
            AttrName = attrName;
            AttrValue = attrValue;
        }

        public override string ToString()
        {
            return "CSET " + AttrName + "=" + AttrValue;
        }

        public override bool Equals(object obj)
        {
            var other = obj as CSetCommand;
            if (other == null)
                return false;

            return AttrName == other.AttrName &&
                AttrValue == other.AttrValue;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }

    class SelectCommand : CoreGenCommand
    {
        private static readonly Regex Pattern = new Regex(@"SELECT (?<sel>.+)");

        public string Selection { get; private set; }

        public static SelectCommand TryParse(string line)
        {
            var match = Pattern.Match(line);
            if (!match.Success)
                return null;
            return new SelectCommand(match.Result("${sel}"));
        }

        public SelectCommand(string selection, Type type = null):
            base(type)
        {
            Selection = selection;
        }

        public override string ToString()
        {
            return "SELECT " + Selection;
        }

        public override bool Equals(object obj)
        {
            var other = obj as SelectCommand;
            if (other == null)
                return false;

            return Selection == other.Selection;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }

    class GenerateCommand : CoreGenCommand
    {
        public override string ToString()
        {
            return "GENERATE";
        }

        public static GenerateCommand TryParse(string line)
        {
            if (line == "GENERATE")
                return new GenerateCommand();
            else
                return null;
        }

        public override bool Equals(object obj)
        {
            var other = obj as GenerateCommand;
            if (other == null)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }

    public class CoreGenDescription
    {
        private List<CoreGenCommand> _commands = new List<CoreGenCommand>();
        public string Path { get; private set; }

        public CoreGenDescription(string path)
        {
            Path = path;
        }

        internal IEnumerable<CoreGenCommand> Commands
        {
            get { return _commands.AsEnumerable(); }
        }

        internal SelectCommand SelectCommand
        {
            get { return (SelectCommand)_commands.Where(_ => _ is SelectCommand).FirstOrDefault(); }
        }

        internal IEnumerable<SetCommand> SetCommands
        {
            get { return _commands.Where(_ => _ is SetCommand).Cast<SetCommand>(); }
        }

        internal IEnumerable<CSetCommand> CSetCommands
        {
            get { return _commands.Where(_ => _ is CSetCommand).Cast<CSetCommand>(); }
        }

        public void Set(string attrName, string attrValue, Type attrValueType = null)
        {
            _commands.Add(new SetCommand(attrName, attrValue, attrValueType));
        }

        public void CSet(string attrName, string attrValue, Type attrValueType = null)
        {
            _commands.Add(new CSetCommand(attrName, attrValue, attrValueType));
        }

        public void Select(string selection, Type selectionValueType = null)
        {
            _commands.Add(new SelectCommand(selection, selectionValueType));
        }

        public void Generate()
        {
            _commands.Add(new GenerateCommand());
        }

        public void Store()
        {
            StreamWriter sw = new StreamWriter(Path);
            foreach (CoreGenCommand cmd in _commands)
            {
                sw.WriteLine(cmd.ToString());
            }
            sw.Close();
        }
    }

    public class COEDescription
    {
        public enum ETarget
        {
            BitCor,
            BlockMem,
            DistMem,
            FIR
        }

        public ETarget Target { get; private set; }
        public int Radix { get; set; }
        public Array Data { get; set; }

        public COEDescription(ETarget target)
        {
            Target = target;
            Radix = 16;
        }

        public void Store(string path)
        {
            StreamWriter sw = new StreamWriter(path);
            switch (Target)
            {
                case ETarget.BitCor: sw.Write("radix"); break;
                case ETarget.BlockMem:
                case ETarget.DistMem:
                    sw.Write("memory_initialization_radix"); break;
                case ETarget.FIR: sw.Write("Radix"); break;
                default: throw new NotImplementedException();
            }
            sw.WriteLine(" = " + Radix.ToString() + ";");
            string sep = "";
            switch (Target)
            {
                case ETarget.BitCor: sw.Write("pattern = "); break;
                case ETarget.BlockMem: sw.WriteLine("memory_initialization_vector="); sep = ","; break;
                case ETarget.DistMem: sw.Write("memory_initialization_vector = "); break;
                case ETarget.FIR: sw.Write("Coefdata= "); sep = ",";  break;
                default: throw new NotImplementedException();
            }
            int col = 0;
            foreach (object data in Data)
            {
                if (col == 8)
                {
                    col = 1;
                    sw.WriteLine(sep);
                }
                else
                {
                    if (col > 0)
                        sw.Write(sep + " ");
                    col++;
                }

                sw.Write(data.ToString());
            }
            sw.WriteLine(";");
            sw.Close();
        }
    }


    public class XilinxCoreGenerator
    {
        //private XilinxConsole _console;

        internal XilinxCoreGenerator(/*XilinxConsole console*/)
        {
            //_console = console;
        }

        public ProcessPool.ToolBatch Execute(XilinxProject proj, string xcoPath, string projPath)
        {
            if (proj.SkipIPCoreSynthesis)
                return null;

            //see: http://www.xilinx.com/itp/xilinx6/books/docs/cgn/cgn.pdf
            projPath = Path.GetFullPath(projPath);
            xcoPath = Path.GetFullPath(xcoPath);
            string dir = Path.GetDirectoryName(projPath);
            bool madeSubst = false;
            char drivel = default(char);
            string drive = null;
            try
            {
                var batch = ProcessPool.Instance.CreateBatch();
                if (projPath.Length >= 160)
                {
                    drivel = SubstManager.Instance.AllocateDrive();
                    drive = drivel + ":";
                    batch.Add("subst ", drive + " " + dir);
                    dir = drive + "\\";
                    projPath = dir + Path.GetFileName(projPath);
                    madeSubst = true;
                }
                string arguments = "-b \"" + xcoPath + "\" -p \"" + projPath + "\"";
                batch.Add(proj.ISEBinPath, dir, "coregen", arguments);
                if (madeSubst)
                {
                    batch.Add("subst", drive + " /D");
                }
                batch.Start();
                return batch;
            }
            finally
            {
                if (drivel != default(char))
                    SubstManager.Instance.ReleaseDrive(drivel);
            }
        }
    }
}

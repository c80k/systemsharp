using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Meta;

namespace SystemSharp.Synthesis.VHDLGen
{
    /// <summary>
    /// Provides information on the generated VHDL artifacts for a specific component.
    /// </summary>
    public class VHDLGenInfo
    {
        /// <summary>
        /// VHDL entity name
        /// </summary>
        public string EntityName { get; private set; }

        /// <summary>
        /// VHDL instance name
        /// </summary>
        public string InstanceName { get; private set; }

        /// <summary>
        /// VHDL default architecture
        /// </summary>
        public string DefaultArch { get; private set; }

        /// <summary>
        /// VHDL file name
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="entityName">VHDL entity name</param>
        /// <param name="instanceName">VHDL instance name</param>
        /// <param name="defaultArch">VHDL default architecture</param>
        /// <param name="fileName">VHDL file name</param>
        public VHDLGenInfo(string entityName, string instanceName,
            string defaultArch, string fileName)
        {
            EntityName = entityName;
            InstanceName = instanceName;
            DefaultArch = defaultArch;
            FileName = fileName;
        }

        /// <summary>
        /// Looks up the VHDL file path of a VHDL-generated component.
        /// </summary>
        /// <param name="desc">component descriptor</param>
        /// <returns>its VHDL file path</returns>
        public static string GetInstancePath(IComponentDescriptor desc)
        {
            var cur = (DescriptorBase)desc;
            string path = "";
            while (cur.Owner is IComponentDescriptor)
            {
                var gi = cur.QueryAttribute<VHDLGenInfo>();
                path = "/" + gi.InstanceName + path;
                cur = cur.Owner;
            }
            var git = cur.QueryAttribute<VHDLGenInfo>();
            path = "/" + git.EntityName;
            return path;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemSharp.Meta;

namespace SystemSharp.Synthesis.VHDLGen
{
    public class VHDLGenInfo
    {
        public string EntityName { get; private set; }
        public string InstanceName { get; private set; }
        public string DefaultArch { get; private set; }
        public string FileName { get; private set; }

        public VHDLGenInfo(string entityName, string instanceName,
            string defaultArch, string fileName)
        {
            EntityName = entityName;
            InstanceName = instanceName;
            DefaultArch = defaultArch;
            FileName = fileName;
        }

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

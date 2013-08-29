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
using SystemSharp.Components;

namespace SystemSharp.Interop.Xilinx.CoreGen
{
    public static class AutoCoreGen
    {
        internal static void FromProject(this CoreGenDescription desc, XilinxProject proj, EPropAssoc assoc)
        {
            PropertyBag pbag = proj.PBag.Copy(assoc);
            if (assoc == EPropAssoc.CoreGenProj)
            {
                string fname = Path.GetFileNameWithoutExtension(desc.Path);
                string wdir = "./tmp/" + fname + "/";
                pbag.PutProperty(EXilinxProjectProperties.CoreGen_WorkingDirectory, wdir);
            }
            IList<PropDesc> allProps = PropEnum.EnumProps(typeof(EXilinxProjectProperties));
            foreach (PropDesc pd in allProps)
            {
                if (!pd.IDs.ContainsKey(assoc))
                    continue;

                object value = pbag.GetProperty((EXilinxProjectProperties)pd.EnumValue);
                desc.Set(pd.IDs[assoc], PropEnum.ToString(value, assoc), value.GetType());
            }
        }

        public static void FromComponent(this CoreGenDescription desc, Component component)
        {
            Type type = component.GetType();
            var componentProps = type.GetProperties(BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            var cgProps = componentProps.Where(p => Attribute.IsDefined(p, typeof(CoreGenProp)));
            List<CoreGenCommand> cmds = new List<CoreGenCommand>();
            foreach (PropertyInfo pi in cgProps)
            {
                var cgProp = (CoreGenProp)Attribute.GetCustomAttribute(pi, typeof(CoreGenProp));
                var attrs = pi.GetCustomAttributes(typeof(PresentOn), true);
                var conds = attrs.Select(o => ((PresentOn)o).PresenceCondition);
                bool fulfilled = !conds.Any() ||
                    conds.Any(c => cgProps.Any(p => object.Equals(c, p.GetValue(component, new object[0]))));
                if (!fulfilled)
                    continue;
                object propValue = pi.GetValue(component, new object[0]);
                string propValueStr = PropEnum.ToString(propValue, EPropAssoc.CoreGen);
                switch (cgProp.Usage)
                {
                    case ECoreGenUsage.Select:
                        desc.Select(propValueStr, propValue.GetType());
                        break;

                    case ECoreGenUsage.CSet:
                        {
                            string propIDStr = PropEnum.ToString(pi, EPropAssoc.CoreGen);
                            desc.CSet(propIDStr, propValueStr, propValue.GetType());
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
            desc.Generate();
        }
    }
}

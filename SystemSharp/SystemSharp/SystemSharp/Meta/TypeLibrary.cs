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
using System.Linq;
using System.Text;
using SystemSharp.Components;

namespace SystemSharp.Meta
{
    public class TypeLibrary
    {
        private Dictionary<string, PackageDescriptor> _pkgMap =
            new Dictionary<string, PackageDescriptor>();
        private Dictionary<TypeDescriptor, TypeDescriptor> _types =
            new Dictionary<TypeDescriptor, TypeDescriptor>();
        private DesignDescriptor _design;

        public TypeLibrary(DesignDescriptor design)
        {
            _design = design;
        }

        public PackageDescriptor GetPackage(Type type)
        {
            string pkgName = type.Namespace;
            if (pkgName == null)
                pkgName = "DefaultPackage";
            PackageDescriptor pd;
            if (!_pkgMap.TryGetValue(pkgName, out pd))
            {
                pd = new PackageDescriptor(pkgName);
            }
            _pkgMap[pkgName] = pd;
            _design.AddChild(pd, pd.Name);
            return pd;
        }

        public TypeDescriptor Canonicalize(TypeDescriptor td)
        {
            TypeDescriptor tdOut;
            if (_types.TryGetValue(td, out tdOut))
            {
                return tdOut;
            }
            else
            {
                td = td.Clone();
                _types[td] = td;
                if (!td.HasIntrinsicTypeOverride)
                {
                    PackageDescriptor pd = GetPackage(td.CILType);
                    td.Package = pd;
                    pd.AddChild(td, td.Name);
                }
                return td;
            }
        }

        public TypeDescriptor AddType(TypeDescriptor td)
        {
            if (td.HasIntrinsicTypeOverride ||
                td.CILType.IsPrimitive ||
                td.CILType.IsPointer ||
                td.CILType.IsByRef ||
                (!td.CILType.IsValueType && !td.CILType.IsArray) ||
                td.CILType.Equals(typeof(void)))
                return td;
            
            //FIXME
            //if (!td.IsStatic)
            //    throw new InvalidOperationException("Only static types are allowed in a type library");

            Queue<TypeDescriptor> q = new Queue<TypeDescriptor>();
            q.Enqueue(td);
            while (q.Count > 0)
            {
                TypeDescriptor qtd = q.Dequeue();
                if (qtd.IsConstrained)
                    Canonicalize(qtd.MakeUnconstrainedType());
                else
                    Canonicalize(qtd);

                foreach (TypeDescriptor dep in qtd.GetDependentTypes())
                    q.Enqueue(dep);
            }

            return Canonicalize(td);
        }

        public IEnumerable<TypeDescriptor> AllRank0OrIncompleteTypes
        {
            get
            {
                return _types.Values.Where(x => !x.IsComplete || x.Rank == 0);
            }
        }

        public IEnumerable<PackageDescriptor> Packages
        {
            get
            {
                return _pkgMap.Values.Where(pd => !pd.IsEmpty);
            }
        }
    }
}

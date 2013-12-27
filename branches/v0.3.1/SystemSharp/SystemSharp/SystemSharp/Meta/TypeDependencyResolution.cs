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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Common;
using SystemSharp.SysDOM;

namespace SystemSharp.Meta
{
    /// <summary>
    /// This static class provides a service for inspecting a design context for structural or programatic use
    /// of data types. It will add according package dependencies for those types.
    /// </summary>
    public static class TypeDependencyResolution
    {
        private class StmtTypeResolver : 
            IStatementVisitor, 
            ILiteralVisitor
        {
            private Literal _result;
            private DesignDescriptor _design;
            private Stack<IPackageOrComponentDescriptor> _stack = new Stack<IPackageOrComponentDescriptor>();

            public IComponentDescriptor CurComponent { get; private set; }

            public void RequireType(TypeDescriptor type, DesignDescriptor design)
            {
                if (!type.HasIntrinsicTypeOverride &&
                    !type.CILType.IsPrimitive)
                {
                    design.TypeLib.AddType(type);
                    CurComponent.AddDependency(_design.TypeLib.GetPackage(type.CILType));
                }
            }

            public void VisitConstant(Constant constant)
            {
                _result = constant;
            }

            public void VisitVariable(Variable variable)
            {
                _result = variable;
            }

            public void VisitFieldRef(FieldRef fieldRef)
            {
                var fd = fieldRef.FieldDesc;
                if (fd.IsStatic)
                {
                    var cfd = fd as CILFieldDescriptor;
                    if (cfd != null)
                    {
                        var pkg = _design.TypeLib.GetPackage(cfd.Field.DeclaringType);
                        fd = pkg.Canonicalize(fd);
                        CurComponent.AddDependency(pkg);
                    }
                }
                else
                {
                    fd = CurComponent.Canonicalize(fd);
                }
                _result = new FieldRef(fd);
            }

            public void VisitThisRef(ThisRef thisRef)
            {
                _result = thisRef;
            }

            public void VisitSignalRef(SignalRef signalRef)
            {
                _result = signalRef;
            }

            public void VisitArrayRef(ArrayRef arrayRef)
            {
                Resolve(arrayRef.ArrayExpr);
                foreach (var idx in arrayRef.Indices)
                    Resolve(idx);
                _result = arrayRef;
            }

            private void Resolve(Expression expr)
            {
                var rtype = expr.ResultType;
                RequireType(rtype, _design);
                foreach (Expression child in expr.Children)
                    Resolve(child);
                var lr = expr as LiteralReference;
                if (lr != null)
                {
                    lr.ReferencedObject.Accept(this);
                }
                var call = expr as FunctionCall;
                if (call != null)
                {
                    ProcessCallable(call.Callee);
                }
            }

            public StmtTypeResolver(DesignDescriptor design, IComponentDescriptor component)
            {
                _design = design;
                CurComponent = component;
                _stack.Push((IPackageOrComponentDescriptor)component);
            }

            public void AcceptCompoundStatement(CompoundStatement stmt)
            {
                stmt.Statements.Accept(this);
            }

            public void AcceptLoopBlock(LoopBlock stmt)
            {
                stmt.Body.Accept(this);
                if (stmt.CounterVariable != null)
                    RequireType(stmt.CounterVariable.Type, _design);
                if (stmt.HeadCondition != null)
                    Resolve(stmt.HeadCondition);
                if (stmt.Initializer != null)
                    stmt.Initializer.Accept(this);
                if (stmt.Trailer != null)
                    stmt.Trailer.Accept(this);
            }

            public void AcceptBreakLoop(BreakLoopStatement stmt)
            {
            }

            public void AcceptContinueLoop(ContinueLoopStatement stmt)
            {
            }

            public void AcceptIf(IfStatement stmt)
            {
                foreach (var cond in stmt.Conditions)
                    Resolve(cond);
                foreach (var branch in stmt.Branches)
                    branch.Accept(this);
            }

            public void AcceptCase(CaseStatement stmt)
            {
                Resolve(stmt.Selector);
                foreach (var caseex in stmt.Cases)
                    Resolve(caseex);
                foreach (var branch in stmt.Branches)
                    branch.Accept(this);
            }

            public void AcceptStore(StoreStatement stmt)
            {
                RequireType(stmt.Container.Type, _design);
                stmt.Container.Accept(this);
                Resolve(stmt.Value);
            }

            public void AcceptNop(NopStatement stmt)
            {
            }

            public void AcceptSolve(SolveStatement stmt)
            {
                throw new NotImplementedException();
            }

            public void AcceptBreakCase(BreakCaseStatement stmt)
            {
            }

            public void AcceptGotoCase(GotoCaseStatement stmt)
            {
            }

            public void AcceptGoto(GotoStatement stmt)
            {
            }

            public void AcceptReturn(ReturnStatement stmt)
            {
                if (stmt.ReturnValue != null)
                    Resolve(stmt.ReturnValue);
            }

            public void AcceptThrow(ThrowStatement stmt)
            {
                if (stmt.ThrowExpr != null)
                    Resolve(stmt.ThrowExpr);
            }

            private void ProcessCallable(ICallable callable)
            {
                var fspec = callable as FunctionSpec;
                if (fspec != null && fspec.GenericSysDOMRep != null)
                {
                    var owner = (IPackageOrComponentDescriptor)fspec.GenericSysDOMRep.Owner;
                    var ownerpkg = owner as PackageDescriptor;
                    if (ownerpkg != null)
                    {
                        _design.AddChild(ownerpkg, ownerpkg.Name);
                        if (owner != _stack.Peek())
                            _stack.Peek().AddDependency(ownerpkg);
                    }

                    if (!fspec.GenericSysDOMRep.IsActive)
                    {
                        fspec.GenericSysDOMRep.IsActive = true;
                        _stack.Push(owner);
                        if (fspec.GenericSysDOMRep.Implementation != null)
                            fspec.GenericSysDOMRep.Implementation.Body.Accept(this);
                        _stack.Pop();
                    }
                }
            }

            public void AcceptCall(CallStatement stmt)
            {
                foreach (var arg in stmt.Arguments)
                    Resolve(arg);
                ProcessCallable(stmt.Callee);
            }
        }

        static void ResolveTypeDependencies(this CodeDescriptor code, StmtTypeResolver str)
        {
            code.Implementation.Body.Accept(str);
        }

        static void ResolveTypeDependencies(this IComponentDescriptor component, DesignDescriptor design)
        {
            foreach (var child in component.GetChildComponents())
            {
                ResolveTypeDependencies(child, design);
            }
            var processes = component.GetProcesses().ToArray();
            var str = new StmtTypeResolver(design, component);
            foreach (var process in processes)
            {
                ResolveTypeDependencies(process, str);
            }
            foreach (var port in component.GetPorts())
            {
                str.RequireType(port.ElementType, design);
            }
            foreach (var signal in component.GetSignals())
            {
                str.RequireType(signal.ElementType, design);
            }
            foreach (var field in component.GetFields())
            {
                str.RequireType(field.Type, design);
            }
        }

        private class DependencyBrowser
        {
            private DesignDescriptor _design;
            private int _curOrder;

            public DependencyBrowser(DesignDescriptor design)
            {
                _design = design;
                _curOrder = 0;
            }

            private void ResetOrders()
            {
                foreach (var pkg in _design.TypeLib.Packages)
                {
                    pkg.DependencyOrder = -2; // yes, -2 is ok
                }
                var set = _design.GetChildComponents().SelectMany(c => c.GetAllAncestors());
                foreach (var component in set)
                {
                    ((IDependencyOrdered)component).DependencyOrder = -1;
                }
            }

            private void PackageDFS(PackageDescriptor pkg)
            {
                if (pkg.DependencyOrder >= 0)
                    return;

                if (pkg.DependencyOrder == -1)
                    throw new InvalidOperationException("Recursive package dependency");
                pkg.DependencyOrder = -1;

                foreach (PackageDescriptor dpkg in pkg.Dependencies)
                    PackageDFS(dpkg);

                pkg.DependencyOrder = _curOrder++;
            }

            private void ComponentDFS(IComponentDescriptor component)
            {
                IDependencyOrdered ordered = component as IDependencyOrdered;
                if (ordered.DependencyOrder >= 0)
                    return;

                foreach (IComponentDescriptor child in component.GetChildComponents())
                    ComponentDFS(child);

                ordered.DependencyOrder = _curOrder++;
            }

            public void Browse()
            {
                ResetOrders();
                foreach (PackageDescriptor pkg in _design.TypeLib.Packages)
                    PackageDFS(pkg);
                foreach (IComponentDescriptor component in _design.GetChildComponents())
                    ComponentDFS(component);
            }
        }

        /// <summary>
        /// Inspects the design for structural or programatic use of data types. It ensures that every component and
        /// package holds according dependencies to all the types it is using.
        /// </summary>
        public static void ResolveTypeDependencies(this DesignDescriptor design)
        {
            foreach (var component in design.GetChildComponents().ToArray())
            {
                ResolveTypeDependencies(component, design);
            }
            DependencyBrowser browser = new DependencyBrowser(design);
            browser.Browse();
        }
    }
}

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
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.CodeDom.Compiler;
using System.Diagnostics;
using SystemSharp.Components;
using SystemSharp.Analysis;
using SystemSharp.Meta;

namespace SystemSharp.Synthesis
{
    public interface IProject
    {
        string AddFile(string name);
        void AddFileAttribute(string name, object attr);
        void Save();
    }

    public class LibraryAttribute
    {
        public LibraryAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }

    public static class ProjectExtensions
    {
        public static void SetFileLibrary(this IProject project, string file, string library)
        {
            project.AddFileAttribute(file, new LibraryAttribute(library));
        }
    }


    public interface ICodeGenerator
    {
        /// <summary>
        /// Returns the identifier which the code generator will use to identify the generated component within the code.
        /// </summary>
        /// <param name="cdesc">Descriptor of the queried component</param>
        /// <returns>The identifier</returns>
        string GetComponentID(IComponentDescriptor cdesc);

        /// <summary>
        /// Notifies the generator about the begin of a new code generation task.
        /// </summary>
        /// <param name="project">The target project which will receive the generation artifacts</param>
        /// <param name="context">The design which is about to be generated</param>
        void Initialize(IProject project, DesignContext context);

        /// <summary>
        /// Instructs the generator to generate code for a specific component.
        /// </summary>
        /// <param name="project">The target project</param>
        /// <param name="cdesc">The component descriptor</param>
        void GenerateComponent(IProject project, IComponentDescriptor cdesc);

        /// <summary>
        /// Instructs the generator to generate code for a specific package.
        /// </summary>
        /// <param name="project">The target project</param>
        /// <param name="pdesc">The package descriptor</param>
        void GeneratePackage(IProject project, PackageDescriptor pdesc);
    }

    public interface ISynthesisContext
    {
        IProject Project { get; }
        ICodeGenerator CodeGen { get; }
        void DoBehavioralAnalysisAndGenerate(Component component);
    }

    class DefaultSynthesisContext : ISynthesisContext
    {
        public IProject Project { get; private set; }
        public ICodeGenerator CodeGen { get; private set; }
        public DesignContext ModelContext { get; private set; }

        private IEnumerable<IComponentDescriptor> _componentSet;

        public DefaultSynthesisContext(DesignContext simCtx, IProject project, ICodeGenerator codeGen)
        {
            ModelContext = simCtx;
            Project = project;
            CodeGen = codeGen;
            _componentSet = ModelContext.Components.Select(cd => cd.Descriptor);
        }

        public DefaultSynthesisContext(DesignContext simCtx, IProject project, 
            IComponentDescriptor top, ICodeGenerator codeGen)
        {
            ModelContext = simCtx;
            Project = project;
            CodeGen = codeGen;
            _componentSet = top.GetAllAncestors();
        }

        public DefaultSynthesisContext(DesignContext simCtx, IProject project,
            IEnumerable<IComponentDescriptor> componentSet, ICodeGenerator codeGen)
        {
            ModelContext = simCtx;
            Project = project;
            CodeGen = codeGen;
            _componentSet = componentSet;
        }

        public void DoBehavioralAnalysisAndGenerate(Component component)
        {
            component.Descriptor.HasForeignImplementation = false;
        }

        public void Synthesize()
        {
            var repSet = new Dictionary<Component, Component>(Component.BehaviorComparer);
            foreach (var cdesc in _componentSet)
            {
                var cd = cdesc as ComponentDescriptor;
                if (cd == null)
                    continue;
                var component = cd.Instance;
                if (repSet.ContainsKey(component))
                    component.Representant = repSet[component];
                else
                    repSet[component] = component;
            }

            foreach (Component component in repSet.Keys)
            {
                component.Descriptor.HasForeignImplementation = true;
                component.OnSynthesisInternal(this);
            }
            ModelContext.CompleteAnalysis();
            ModelContext.Descriptor.ResolveTypeDependencies();
            CodeGen.Initialize(Project, ModelContext);
            foreach (var component in repSet.Keys)
            {
                var cdd = component.Descriptor;
                if (cdd != null && cdd.HasForeignImplementation)
                    continue;
                CodeGen.GenerateComponent(Project, cdd);
            }
            foreach (PackageDescriptor pd in ModelContext.Descriptor.GetPackages())
            {
                if (pd.IsEmpty)
                    continue;
                CodeGen.GeneratePackage(Project, pd);
            }
        }
    }

    public class SynthesisEngine
    {
        private DesignContext _ctx;
        private IProject _project;

        private SynthesisEngine(DesignContext ctx, IProject project)
        {
            _ctx = ctx;
            _project = project;
            _ctx.RunRefinements(_project);
        }

        public static SynthesisEngine Create(DesignContext ctx, IProject project)
        {
            return new SynthesisEngine(ctx, project);
        }

        public void Synthesize(ICodeGenerator codeGen)
        {
            DefaultSynthesisContext sctx = new DefaultSynthesisContext(_ctx, _project, codeGen);
            sctx.Synthesize();
        }

        public void Synthesize(Component top, ICodeGenerator codeGen)
        {
            DefaultSynthesisContext sctx = new DefaultSynthesisContext(_ctx, _project, top.Descriptor, codeGen);
            sctx.Synthesize();
        }

        public void Synthesize(
            IEnumerable<IComponentDescriptor> componentSet, 
            ICodeGenerator codeGen)
        {
            DefaultSynthesisContext sctx = new DefaultSynthesisContext(_ctx, _project, componentSet, codeGen);
            sctx.Synthesize();
        }
    }
}

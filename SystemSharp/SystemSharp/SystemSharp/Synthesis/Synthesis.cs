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
    /// <summary>
    /// Interface for project generators.
    /// </summary>
    /// <remarks>
    /// As with most common hardware/software development tools, a project is essentially a collection of source
    /// code files. In most cases, there is an additional project file which refers those files and contains 
    /// project-specific settings. The file format depends highly on the target tool. It is the project generator's
    /// duty to generate such a project file. The source files themselves are created by a code generator which 
    /// comes separate from the project generator.
    /// </remarks>
    public interface IProject
    {
        /// <summary>
        /// Adds a file to the project.
        /// </summary>
        /// <param name="name">relative file path including extension</param>
        /// <returns>absolute file path including extension</returns>
        string AddFile(string name);

        /// <summary>
        /// Associates a file with an attribute.
        /// </summary>
        /// <remarks>
        /// Attributes are a means of providing content- and project-specific meta information
        /// on a particular file. The concrete class and meaning of the passed attribute
        /// depends on the code generator.
        /// </remarks>
        /// <param name="name">relative file path including extension</param>
        /// <param name="attr">attribute to associate</param>
        void AddFileAttribute(string name, object attr);

        /// <summary>
        /// Completes the project. Afterwards, no additional calls of <c>AddFile</c> or
        /// <c>AddFileAttribute</c> will be made.
        /// </summary>
        /// <remarks>
        /// If there is any project file to create, the interface implementor should generate
        /// this now.
        /// </remarks>
        void Save();
    }

    /// <summary>
    /// Project-specific attribute which associates a file with the library name
    /// the file belongs to.
    /// </summary>
    public class LibraryAttribute
    {
        /// <summary>
        /// Constructs a library attribute.
        /// </summary>
        /// <param name="name">library name</param>
        public LibraryAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Library name
        /// </summary>
        public string Name { get; private set; }
    }

    /// <summary>
    /// This static class provides extension methods to work with project generators.
    /// </summary>
    public static class ProjectExtensions
    {
        /// <summary>
        /// Associates a project file with a library name.
        /// </summary>
        /// <param name="project">the project generator</param>
        /// <param name="file">relative file path including extension</param>
        /// <param name="library">library name to associate</param>
        public static void SetFileLibrary(this IProject project, string file, string library)
        {
            project.AddFileAttribute(file, new LibraryAttribute(library));
        }
    }

    /// <summary>
    /// Source code generator interface.
    /// </summary>
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
        /// <param name="project">The target project for the generated artifacts</param>
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

    /// <summary>
    /// The synthesis context interface provides the "glue" between project generator,
    /// code generator and design.
    /// </summary>
    public interface ISynthesisContext
    {
        /// <summary>
        /// Returns the target generator project.
        /// </summary>
        IProject Project { get; }

        /// <summary>
        /// Returns the target code generator.
        /// </summary>
        ICodeGenerator CodeGen { get; }

        /// <summary>
        /// Instructs the synthesis engine to generate to particular component.
        /// </summary>
        /// <param name="component">component to generate</param>
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

    /// <summary>
    /// The synthesis engine generates source code and a project from a design.
    /// </summary>
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

        /// <summary>
        /// Creates a new synthesis engine.
        /// </summary>
        /// <param name="ctx">the design to generate</param>
        /// <param name="project">the target project generator</param>
        /// <returns>the newly created synthesis engine</returns>
        public static SynthesisEngine Create(DesignContext ctx, IProject project)
        {
            return new SynthesisEngine(ctx, project);
        }

        /// <summary>
        /// Synthesizes the design using the specified code generator.
        /// </summary>
        /// <param name="codeGen">code generator to use</param>
        public void Synthesize(ICodeGenerator codeGen)
        {
            DefaultSynthesisContext sctx = new DefaultSynthesisContext(_ctx, _project, codeGen);
            sctx.Synthesize();
        }

        /// <summary>
        /// Synthesis the design, starting from a user-defined top-level component downwards, using the
        /// specified code generator.
        /// </summary>
        /// <param name="top">top-level component</param>
        /// <param name="codeGen">code generator to use</param>
        public void Synthesize(Component top, ICodeGenerator codeGen)
        {
            DefaultSynthesisContext sctx = new DefaultSynthesisContext(_ctx, _project, top.Descriptor, codeGen);
            sctx.Synthesize();
        }

        /// <summary>
        /// Synthesizes the design, starting from a user-defined set of top-level components downwards,
        /// using the specified code generator.
        /// </summary>
        /// <param name="componentSet">set of top-level components</param>
        /// <param name="codeGen">code generator to use</param>
        public void Synthesize(
            IEnumerable<IComponentDescriptor> componentSet, 
            ICodeGenerator codeGen)
        {
            DefaultSynthesisContext sctx = new DefaultSynthesisContext(_ctx, _project, componentSet, codeGen);
            sctx.Synthesize();
        }
    }
}

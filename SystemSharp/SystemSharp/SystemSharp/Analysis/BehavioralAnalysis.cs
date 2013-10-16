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

/* If the compiler complains about missing namespace System.Reactive, this is because you are missing the
 * Microsoft Reactive extensions ("Rx"). Please follow instructions below.
 * */
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
/* Actually, NuGet should automatically detect the missing references and install them from the Internet. 
 * It *should*... *actually*... Unfortunately, I observed some weird behavior where even re-installing Rx 
 *  did not fix the issue. If you get this particular problem, try this:
 * 
 *   1. In solution explorer, expand "References" and delete all references starting with "System.Reactive".
 *      REMARK: These should me marked with a yellow explanation mark symbol - if they're not you either don't 
 *              have a problem, or you have a different problem.
 *   2. Open the package manager console (Tools -> Library Package Manager -> Package Manager Console)
 *      - Make sure "SystemSharp" is selected as "Default project"
 *      - Type: 
 *        PM> Uninstall-Package Rx-Main
 *   3. In solution explorer, double click "packages.config" which will open a text editor.
 *      - Delete all lines starting with "<package id="Rx-"
 *      - Save
 *   4. Change back to the package manager console
 *      - Type: 
 *        PM> Install-Package Rx-Main
 *      
 * The last step should make the references to System.Reactive reappear without exclamation mark.
 * If it doesn't - I'm sorry - I have no clue...
 * */

using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using SystemSharp.Algebraic;
using SystemSharp.Analysis;
using SystemSharp.Analysis.Msil;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Components.Transactions;
using SystemSharp.Meta;
using SystemSharp.Meta.M2M;
using SystemSharp.SysDOM;
using SystemSharp.SysDOM.Analysis;
using SystemSharp.SysDOM.Eval;

namespace SystemSharp.Analysis
{
    class BehavioralAnalyzer        
    {
        private class MethodKey
        {
            private MethodDescriptor _md;

            public MethodKey(MethodDescriptor md)
            {
                _md = md;
            }

            public override bool Equals(object obj)
            {
                var other = obj as MethodKey;
                if (other == null)
                    return false;

                return _md.Method.Equals(other._md.Method) &&
                    _md.GetArguments()
                        .SequenceEqual(
                            other._md.GetArguments(), 
                        ArgumentDescriptor.TypeAndDirectionComparer);
            }

            public override int GetHashCode()
            {
                int hash = _md.Method.GetHashCode();
                hash ^= _md.GetArguments().Aggregate(hash, (h, a) => 
                    ((h << 1) | (h >> 31)) ^ 
                    ArgumentDescriptor.TypeAndDirectionComparer.GetHashCode(a));
                return hash;
            }
        }

        private DesignContext _context;
        private Queue<MethodCallInfo> _methodQ = new Queue<MethodCallInfo>();
        private Dictionary<MethodKey, Function> _methodMap = new Dictionary<MethodKey, Function>();
        private List<CodeDescriptor> _allMethods = new List<CodeDescriptor>();

        private void AnalyzeProcess(Process process)
        {
            Action func = process.InitialAction;
            MethodInfo method = func.Method;
            List<ISignalOrPortDescriptor> sens = new List<ISignalOrPortDescriptor>();
            if (process.Sensitivity != null)
            {
                foreach (AbstractEvent e in process.Sensitivity)
                {
                    DesignObject owner = e.Owner;
                    if (!(owner is SignalBase))
                    {
                        _context.Report(EIssueClass.Error,
                            "Process " + method.Name + " is sensitive to an event which is not owned by " +
                            "a signal. This is currently not supported by the behavioral model.");
                    }
                    else
                    {
                        var signal = (SignalBase)owner;
                        var spdesc = signal.Descriptor;
                        if (spdesc == null)
                            throw new InvalidOperationException();
                        var sref = spdesc.AsSignalRef(SignalRef.EReferencedProperty.Instance)
                            .RelateToComponent(process.Owner.Descriptor);
                        if (sref == null)
                            ReportError("Did not find a matching port for signal " + signal.Descriptor.Name);
                        else
                            sens.Add(sref.Desc);
                    }
                }
            }
            ProcessDescriptor pd = new ProcessDescriptor(method, process)
            {
                Sensitivity = sens.ToArray(),
                Owner = process.Owner.Descriptor
            };
            //DecompileAndEnqueueCallees(pd, process.Owner, new object[] { process.Owner });
            DecompileAndEnqueueCallees(pd, func.Target, new object[] { process.Owner });
            process.Owner.Descriptor.AddChild(pd, method.Name);
        }

        private void ReportError(string message)
        {
            _context.Report(EIssueClass.Error, message);
        }

        private void DecompileAndEnqueueCallees(CodeDescriptor cd, object instance, object[] arguments, MethodCallInfo mymci = null)
        {
            IPackageOrComponentDescriptor owner = cd.Owner as IPackageOrComponentDescriptor;

            var rmd = cd.Method.GetCustomOrInjectedAttribute<RewriteMethodDefinition>();

            IDecompilationResult result;

            var md = cd as MethodDescriptor;
            var pd = cd as ProcessDescriptor;
            if (pd != null)
                _context.CurrentProcess = pd.Instance;
            else
                _context.CurrentProcess = md.CallingProcess.Instance;

            EVariability[] argVar;
            if (md == null)
                argVar = new EVariability[0];
            else
                argVar = md.ArgVariabilities;
            if (rmd != null)
            {
                result = rmd.Rewrite(_context, cd, instance, arguments);
            }
            else if (cd.Method.IsMoveNext())
            {
                var decomp = new AsyncMethodDecompiler(_context, cd, instance, arguments);
                result = decomp.Decompile();
                cd.Implementation = result.Decompiled;
                cd.GenuineImplementation = result.Decompiled;
            }
            else
            {
                var decomp = new MSILDecompiler(cd, instance, arguments, argVar);
                if (cd is ProcessDescriptor)
                {
                    decomp.Template.DisallowReturnStatements = true;
                }
                decomp.Template.DisallowConditionals = true;
                if (mymci != null)
                    mymci.Inherit(decomp.Template);
                result = decomp.Decompile();
                cd.Implementation = result.Decompiled;
                cd.GenuineImplementation = result.Decompiled;
            }

            foreach (var mci in result.CalledMethods)
            {
                _methodQ.Enqueue(mci);
            }
            foreach (var fri in result.ReferencedFields)
            {
                AnalyzeFieldRef(fri, cd);
            }

            _allMethods.Add(cd);
        }

        private MethodDescriptor ConstructMethodDescriptor(MethodCallInfo mci, bool special)
        {
            MethodBase method = mci.GetStrongestOverride();
            MethodDescriptor md = new MethodDescriptor(
                method, 
                mci.EvaluatedArgumentsWithoutThis, 
                special ? mci.ArgumentVariabilities : VariabilityPattern.CreateDefault(method).Pattern);
            var caller = mci.CallerTemplate.Decompilee;
            var callerPd = caller as ProcessDescriptor;
            var callerMd = caller as MethodDescriptor;
            if (callerPd != null)
                md.CallingProcess = callerPd;
            else
                md.CallingProcess = callerMd.CallingProcess;

            return md;
        }

        private void Resolve(MethodCallInfo mci)
        {
            Function genericImpl;
            var md = ConstructMethodDescriptor(mci, false);
            if (md == null)
                return;
            object instance = mci.Instance;
            bool hasThis = md.Method.CallingConvention.HasFlag(CallingConventions.HasThis);
            if (hasThis)
            {
                if (instance != null)
                {
                    var owner = instance as Component;
                    if (owner == null)
                    {
                        ReportError("Method " + md.Method.Name + " is not declared inside a component.");
                    }
                    else
                    {
                        md.Owner = owner.Descriptor;
                    }
                }
                else
                {
                    ReportError("Method " + md.Method.Name + ": unable to resolve declaring component instance.");
                }
            }
            else
            {
                md.Owner = GetPackageDescriptor(md.Method.DeclaringType);
            }
            var key = new MethodKey(md);
            if (_methodMap.TryGetValue(key, out genericImpl))
            {
                md.Implementation = genericImpl;
            }
            else
            {
                DecompileAndEnqueueCallees(md, instance, md.ArgValueSamples, mci);
                System.Diagnostics.Debug.Assert(md.Implementation != null);
                _methodMap[key] = md.Implementation;
            }

            System.Diagnostics.Debug.Assert(md.Implementation != null);
            if (md.Owner != null)
                md = md.Owner.Canonicalize(md);

            var mds = ConstructMethodDescriptor(mci, true);
            mds.Owner = md.Owner;
            DecompileAndEnqueueCallees(mds, instance, mds.ArgValueSamples, mci);
            System.Diagnostics.Debug.Assert(mds.Implementation != null);
            mci.Resolve(md, mds);
        }

        private void ProcessMethodWorkSet()
        {
            while (_methodQ.Count > 0)
            {
                MethodCallInfo mci = _methodQ.Dequeue();
                MethodBase method = mci.GetStrongestOverride();
                if (FactUniverse.Instance.HaveFacts(method) &&
                    FactUniverse.Instance.GetFacts(method).IsDecompilable)
                {
                    Resolve(mci);
                }
            }
        }

        private PackageDescriptor GetPackageDescriptor(Type type)
        {
            return _context.TypeLib.GetPackage(type);
        }

        private void AnalyzeFieldRef(FieldRefInfo fri, CodeDescriptor code)
        {
            Type fieldType = fri.Field.FieldType;
            object[] attrs = fieldType.GetCustomAttributes(typeof(MapToIntrinsicType), false);
            if (attrs.Length == 0)
            {
                if (fri.IsWritten && !fieldType.IsValueType)
                {
                    ReportError("Illegal write access to field " +
                        fri.Field.Name + " of class " + fri.Field.DeclaringType.Name + ": At runtime, " +
                        "only value types may be written.");
                    return;
                }
                if (fri.IsWritten && fri.Field.IsStatic)
                {
                    ReportError("Illegal write access to field " +
                        fri.Field.Name + " of class " + fri.Field.DeclaringType.Name + ": At runtime, " +
                        "only non-static fields may be written.");
                    return;
                }
                if (fri.IsRead && !fieldType.IsValueType && !fieldType.IsArray)
                {
                    //FIXME
                    // E.g. DesignContext.Instance is allowed...
                    /*
                    ReportError("Illegal read access to field " + fri.Field.Name + " of class " +
                        fri.Field.DeclaringType.Name + ": At runtime, only value types and arrays " +
                        "may be accessed.");
                     * */
                    return;
                }
            }
        }

        private void PostProcess()
        {
            foreach (CodeDescriptor cd in _allMethods)
            {
                var iva = InductionVariableAnalyzer.Run(cd.GenuineImplementation.Body);
                var locals = cd.Implementation.LocalVariables;
                var vrcs = new CodeDescriptor.ValueRangeConstraint[locals.Count];
                for (int i = 0; i < locals.Count; i++)
                {
                    var loc = locals[i] as Variable;
                    if (loc != null && iva.IsConstrained(loc))
                    {
                        long min, max;
                        iva.GetRange(loc, out min, out max);
                        vrcs[i] = new CodeDescriptor.ValueRangeConstraint(min, max);
                    }
                    else
                        vrcs[i] = CodeDescriptor.ValueRangeConstraint.Unconstrained;
                }
                cd.ValueRangeConstraints = vrcs;
            }
        }

        private BehavioralAnalyzer(DesignContext context)
        {
            _context = context;
            context.InvalidateUniverse();
            foreach (Component component in context.Components)
            {
                component.Descriptor.Package = GetPackageDescriptor(component.GetType());
                FactUniverse.Instance.AddType(component.GetType());
            }

            IList<Process> processes = context.Processes;
            foreach (Process process in processes)
            {
                if (process.Owner.Descriptor.HasForeignImplementation ||
                    process.IsDoNotAnalyze)
                    continue;

                //if (process.InitialAction.IsAsync())
                //    FactUniverse.Instance.AddMethod(process.InitialAction.Method);
                //var entryPoint = process.InitialAction.UnwrapEntryPoint();
                FactUniverse.Instance.AddMethod(process.InitialAction.Method);
                FactUniverse.Instance.GetFacts(process.InitialAction.Method).
                    RegisterThisCandidate(new ConstObjectSource(process.InitialAction.Target));
#if false
                TransactingComponent.TATarget[] coFSMs = process.GetCoFSMs();
                if (coFSMs != null)
                {
                    foreach (TransactingComponent.TATarget coFSM in coFSMs)
                    {
                        foreach (TAVerb verb in coFSM.NeutralTA)
                        {
                            FactUniverse.Instance.AddMethod(verb.Op.Method);
                        }
                    }
                }
#endif
            }
            FactUniverse.Instance.Complete();
            
            foreach (Process process in processes)
            {
                if (process.Owner.Descriptor.HasForeignImplementation ||
                    process.IsDoNotAnalyze)
                    continue;
                if (process.AnalysisGeneration < context.CurrentRefinementCycle)
                    continue;
                AnalyzeProcess(process);
            }

            ProcessMethodWorkSet();
            PostProcess();

            //SubprogramsDontDriveSignals sdds = new SubprogramsDontDriveSignals();
            //sdds.ApplyTo(context);
        }

        internal static DesignDescriptor DoBehavioralAnalysis(DesignContext context)
        {
            new BehavioralAnalyzer(context);
            return new DesignDescriptor(context);
        }
    }
}

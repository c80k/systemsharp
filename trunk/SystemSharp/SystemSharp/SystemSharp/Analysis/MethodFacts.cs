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
using System.Reactive;
using System.Reactive.Subjects;
using System.Reflection;
using SDILReader;
using System.Reactive.Linq;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Analysis.Msil;
using System.Diagnostics.Contracts;
using System.Security;
using SystemSharp.Components;

namespace SystemSharp.Analysis
{
    /// <summary>
    /// This class provides analysis information on methods and constructors.
    /// </summary>
    public class MethodFacts
    {
        /// <summary>
        /// The associated universe
        /// </summary>
        public FactUniverse Universe { get; private set; }

        /// <summary>
        /// The method or constructor
        /// </summary>
        public MethodBase Method { get; private set; }

        internal bool OnNewMethodCalled { get; set; }

        /// <summary>
        /// Whether the represented method has the UnverifiableCodeAttribute attached
        /// </summary>
        public bool IsUnsafe
        {
            get { return Attribute.IsDefined(Method.Module, typeof(UnverifiableCodeAttribute)); }
        }

        /// <summary>
        /// Whether we can and should actually decompile the method. That is, the method is non-abstract, non-interface, is not unsafe and
        /// does not have any IDoNotAnalyze-derived attribute attached.
        /// </summary>
        public bool IsDecompilable
        {
            get
            {
                return !IsUnsafe &&
                    !Method.HasCustomOrInjectedAttribute(typeof(IDoNotAnalyze)) &&
                    Method.GetMethodBody() != null;
            }
        }

        private MethodCode _cfg;

        /// <summary>
        /// Returns a control-flow graph for the represented method
        /// </summary>
        public MethodCode CFG
        {
            get
            {
                if (_cfg == null)
                    _cfg = MethodCode.Create(Method);
                return _cfg;
            }
        }

        private DataflowAnalyzer<ILInstruction> _dfa;

        /// <summary>
        /// Returns a data-flow analyzer for the represented method
        /// </summary>
        public DataflowAnalyzer<ILInstruction> DFA
        {
            get
            {
                if (_dfa == null)
                {
                    _dfa = new DataflowAnalyzer<ILInstruction>(CFG, CFG.NumLocals);
                    _dfa.Run();
                }
                return _dfa;
            }
        }

        private InvocationAnalyzer _inva;

        /// <summary>
        /// Returns an invocation analyzer for the represented method
        /// </summary>
        public InvocationAnalyzer INVA
        {
            get
            {
                if (_inva == null)
                {
                    _inva = new InvocationAnalyzer(Method);
                    _readFields = _inva.ReadFields.ToBufferedEnumerable();
                    _writtenFields = _inva.WrittenFields.ToBufferedEnumerable();
                    _referencedFields = _inva.ReferencedFields.ToBufferedEnumerable();
                    _referencedTypes = _inva.ReferencedTypes.ToBufferedEnumerable();
                    _mutations = _inva.Mutations.ToBufferedEnumerable();
                    _calledMethods = _inva.CalledMethods.Select(cs => cs.Callee).ToBufferedEnumerable();
                    _constructedObjects = _inva.ConstructedObjects.ToBufferedEnumerable();
                    _constructedArrays = _inva.ConstructedArrays.ToBufferedEnumerable();
                    _localMutations = _inva.LocalMutations.ToMultiMap(m => m.ILIndex, m => m);
                    _inva.Equivalences
                        .Where(tup => tup.Item1 is IResolvableSource)
                        .AutoDo(tup => ((IResolvableSource)tup.Item1).Register(tup.Item2));
                }
                return _inva;
            }
        }

        private IEnumerable<FieldInfo> _readFields;

        /// <summary>
        /// Returns all fields which are read by this method
        /// </summary>
        public IEnumerable<FieldInfo> ReadFields 
        {
            get
            {
                Contract.Requires<InvalidOperationException>(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _readFields;
            }
        }

        private IEnumerable<FieldInfo> _writtenFields;

        /// <summary>
        /// Returns all field which are written by this method
        /// </summary>
        public IEnumerable<FieldInfo> WrittenFields 
        {
            get
            {
                Contract.Requires<InvalidOperationException>(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _writtenFields;
            }
        }

        private IEnumerable<FieldInfo> _referencedFields;
        private IEnumerable<Type> _referencedTypes;

        private IEnumerable<ElementMutation> _mutations;

        /// <summary>
        /// Returns all mutations which are possibly performed by this method
        /// </summary>
        public IEnumerable<ElementMutation> Mutations
        {
            get
            {
                Contract.Requires<InvalidOperationException>(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _mutations;
            }
        }

        /// <summary>
        /// Whether this method performs any mutation
        /// </summary>
        public bool IsMutator { get { return Mutations.Count() > 0; } }

        /// <summary>
        /// Whether this method directly or indirectly calls any other method which performs any mutation
        /// </summary>
        public bool IsIndirectMutator { get; internal set; }

        private IEnumerable<MethodBase> _calledMethods;

        /// <summary>
        /// Returns all methods and constructors which might be called by this method, possibly abstract and/or interface methods
        /// </summary>
        public IEnumerable<MethodBase> CalledMethods 
        {
            get
            {
                Contract.Requires<InvalidOperationException>(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _calledMethods;
            }
        }

        /// <summary>
        /// Returns all concrete methods and constructors which might be called by this method, 
        /// i.e. abstract and/or interface methods will be resolved to realizations first.
        /// </summary>
        public IEnumerable<MethodBase> CalledRealizations
        {
            get { return CalledMethods.SelectMany(m => Universe.GetFacts(m).Realizations); }
        }

        private IEnumerable<MethodBase> _callers;

        /// <summary>
        /// Returns all methods and contructors which might call this method
        /// </summary>
        public IEnumerable<MethodBase> CallingMethods
        {
            get
            {
                Contract.Requires<InvalidOperationException>(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                if (_callers == null)
                {
                    _callers = Universe.KnownMethodBases
                        .Where(mf => mf.CalledRealizations
                            .Contains(Method))
                        .Select(mf => mf.Method)
                        .ToEnumerable();
                }
                return _callers;
            }
        }

        private IEnumerable<ConstructorInfo> _constructedObjects;

        /// <summary>
        /// Returns all constructors which might be called by this method
        /// </summary>
        public IEnumerable<ConstructorInfo> ConstructedObjects
        {
            get
            {
                Contract.Requires<InvalidOperationException>(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _constructedObjects;
            }
        }

        private IEnumerable<Type> _constructedArrays;

        /// <summary>
        /// All possible realizations of this method.
        /// </summary>
        /// <remarks>
        /// This method might be abstract or an interface method. In that case, we want to know which methods override or implement this method.
        /// This is what the property is supposed to capture.
        /// </remarks>
        public IEnumerable<MethodBase> Realizations { get; internal set; }

        /// <summary>
        /// Index of this method inside the call tree
        /// </summary>
        public int CallOrder { get; internal set; }

        /// <summary>
        /// Whether this method is part of a possible recursion
        /// </summary>
        public bool IsRecursive { get; set; }

        /// <summary>
        /// Internally used for constructing the call tree
        /// </summary>
        internal bool Visited { get; set; }

        /// <summary>
        /// Internally used for constructing the call tree
        /// </summary>
        internal bool InDFS { get; set; }

        private ObservingMultiMap<ParameterInfo, ElementSource> _argumentCandidates;
        private ObservingMultiMap<ParameterInfo, ElementSource> _argumentReturnCandidates;
        private HashSet<ElementSource> _thisCandidates;
        private HashSet<ElementSource> _returnCandidates;

        internal void RegisterArgumentCandidates(ParameterInfo arg, IEnumerable<ElementSource> sources)
        {
            if (_argumentCandidates == null)
                _argumentCandidates = new ObservingMultiMap<ParameterInfo, ElementSource>();

            _argumentCandidates.Add(arg, sources);
        }

        internal void RegisterArgumentReturnCandidates(ParameterInfo arg, IEnumerable<ElementSource> sources)
        {
            if (_argumentReturnCandidates == null)
                _argumentReturnCandidates = new ObservingMultiMap<ParameterInfo, ElementSource>();

            _argumentReturnCandidates.Add(arg, sources);
        }

        /// <summary>
        /// Returns all possible contents of a particular argument upon method entry
        /// </summary>
        /// <param name="arg">argument</param>
        /// <returns>all possible contents</returns>
        public IEnumerable<ElementSource> GetArgumentCandidates(ParameterInfo arg)
        {            
            return _argumentCandidates == null ? Enumerable.Empty<ElementSource>() : _argumentCandidates[arg];
        }
        /// <summary>
        /// Returns all possible contents of a particular argument upon method exit
        /// </summary>
        /// <param name="arg">argument</param>
        /// <returns>all possible contents</returns>
        public IEnumerable<ElementSource> GetArgumentReturnCandidates(ParameterInfo arg)
        {
            return _argumentReturnCandidates == null ? Enumerable.Empty<ElementSource>() : _argumentReturnCandidates[arg];
        }

        internal void RegisterThisCandidate(ElementSource source)
        {
            if (_thisCandidates == null)
                _thisCandidates = new HashSet<ElementSource>();

            _thisCandidates.Add(source);
        }

        internal void RegisterThisCandidates(IEnumerable<ElementSource> sources)
        {
            if (_thisCandidates == null)
                _thisCandidates = new HashSet<ElementSource>();

            foreach (ElementSource source in sources)
                _thisCandidates.Add(source);
        }

        /// <summary>
        /// Returns all possible instances on which this method might be called
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ElementSource> GetThisCandidates()
        {
            return _thisCandidates == null ? Enumerable.Empty<ElementSource>() : _thisCandidates.AsEnumerable();
        }

        internal void RegisterReturnCandidates(IEnumerable<ElementSource> sources)
        {
            if (_returnCandidates == null)
                _returnCandidates = new HashSet<ElementSource>();

            foreach (ElementSource source in sources)
                _returnCandidates.Add(source);
        }

        /// <summary>
        /// Returns all possible objects this method might return
        /// </summary>
        /// <returns>all possible objects this method might return</returns>
        public IEnumerable<ElementSource> GetReturnCandidates()
        {
            return _returnCandidates == null ? Enumerable.Empty<ElementSource>() : _returnCandidates.AsEnumerable();
        }

        /// <summary>
        /// Returns all instructions which might define a stack element at a certain program location
        /// </summary>
        /// <param name="ilIndex">instruction index</param>
        /// <param name="stackLevel">stack index (0 is top)</param>
        /// <returns>all instructions which might define given program location</returns>
        public IEnumerable<int> GetStackElementDefinitions(int ilIndex, int stackLevel)
        {
            return StackInfluenceAnalysis.GetStackElementDefinitions(ilIndex, stackLevel, CFG);
        }

        private Dictionary<int, HashSet<LocalMutation>> _localMutations;
        
        /// <summary>
        /// Returns all possible mutations this method performs on local variables at a particular program location
        /// </summary>
        /// <param name="ilIndex">instruction index</param>
        /// <returns>all possible mutations of local variables</returns>
        public IEnumerable<LocalMutation> GetLocalMutations(int ilIndex)
        {
            return _localMutations.Get(ilIndex);
        }

        private Dictionary<VariabilityPattern, VariabilityAnalyzer> _varaMap =
            new Dictionary<VariabilityPattern, VariabilityAnalyzer>();

        /// <summary>
        /// Returns a variability analyzer for certain argument variabilities
        /// </summary>
        /// <param name="varPattern">argument variabilities</param>
        /// <returns>the variability analyzer</returns>
        public VariabilityAnalyzer GetVARA(VariabilityPattern varPattern)
        {
            VariabilityAnalyzer result;
            if (!_varaMap.TryGetValue(varPattern, out result))
            {
                result = new VariabilityAnalyzer(Method, varPattern);
                result.Run();
                _varaMap[varPattern] = result;
            }
            return result;
        }

        /// <summary>
        /// Whether this method has an ISideEffectFree-implementing attribute attached
        /// </summary>
        public bool IsSideEffectFree
        {
            get { return Method.GetCustomOrInjectedAttribute(typeof(ISideEffectFree)) != null; }
        }

        /// <summary>
        /// Whether this method has the StaticEvaluation attribute attached
        /// </summary>
        public bool IsStaticEvaluation
        {
            get { return Method.GetCustomOrInjectedAttribute(typeof(StaticEvaluation)) != null; }
        }

        /// <summary>
        /// List of loop headers (identified by first instruction indices) to unroll
        /// </summary>
        public List<int> UnrollHeaders { get; private set; }

        /// <summary>
        /// List of loop headers (identified by first instruction indices) not to unroll
        /// </summary>
        public List<int> NonUnrollHeaders { get; private set; }

        internal MethodFacts(FactUniverse facts, MethodBase method)
        {
            Universe = facts;
            Method = method;
            _calledMethods = Enumerable.Empty<MethodBase>();
            _constructedArrays = Enumerable.Empty<Type>();
            _constructedObjects = Enumerable.Empty<ConstructorInfo>();
            _mutations = Enumerable.Empty<ElementMutation>();
            _readFields = Enumerable.Empty<FieldInfo>();
            _referencedFields = Enumerable.Empty<FieldInfo>();
            _referencedTypes = Enumerable.Empty<Type>();
            _writtenFields = Enumerable.Empty<FieldInfo>();
            Realizations = Enumerable.Empty<MethodBase>();
            UnrollHeaders = new List<int>();
            NonUnrollHeaders = new List<int>();
        }
    }
}

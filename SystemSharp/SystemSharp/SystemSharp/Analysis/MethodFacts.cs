/**
 * Copyright 2011 Christian Köllner
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
    public class MethodFacts
    {
        public FactUniverse Universe { get; private set; }
        public MethodBase Method { get; private set; }

        internal bool OnNewMethodCalled { get; set; }

        public bool IsUnsafe
        {
            get { return Attribute.IsDefined(Method.Module, typeof(UnverifiableCodeAttribute)); }
        }

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
        public IEnumerable<FieldInfo> ReadFields 
        {
            get
            {
                Contract.Requires(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _readFields;
            }
        }

        private IEnumerable<FieldInfo> _writtenFields;
        public IEnumerable<FieldInfo> WrittenFields 
        {
            get
            {
                Contract.Requires(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _writtenFields;
            }
        }

        private IEnumerable<FieldInfo> _referencedFields;
        private IEnumerable<Type> _referencedTypes;

        private IEnumerable<ElementMutation> _mutations;
        public IEnumerable<ElementMutation> Mutations
        {
            get
            {
                Contract.Requires(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _mutations;
            }
        }

        public bool IsMutator { get { return Mutations.Count() > 0; } }
        public bool IsIndirectMutator { get; internal set; }

        private IEnumerable<MethodBase> _calledMethods;
        public IEnumerable<MethodBase> CalledMethods 
        {
            get
            {
                Contract.Requires(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _calledMethods;
            }
        }

        public IEnumerable<MethodBase> CalledRealizations
        {
            get { return CalledMethods.SelectMany(m => Universe.GetFacts(m).Realizations); }
        }

        private IEnumerable<MethodBase> _callers;
        public IEnumerable<MethodBase> CallingMethods
        {
            get
            {
                Contract.Requires(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
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
        public IEnumerable<ConstructorInfo> ConstructedObjects
        {
            get
            {
                Contract.Requires(Universe.IsCompleted, FactUniverse.UniverseCompletedErrorMsg);
                return _constructedObjects;
            }
        }

        private IEnumerable<Type> _constructedArrays;

        public IEnumerable<MethodBase> Realizations { get; internal set; }

        public int CallOrder { get; internal set; }
        public bool IsRecursive { get; set; }
        internal bool Visited { get; set; }
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

        public IEnumerable<ElementSource> GetArgumentCandidates(ParameterInfo arg)
        {            
            return _argumentCandidates == null ? Enumerable.Empty<ElementSource>() : _argumentCandidates[arg];
        }

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

        public IEnumerable<ElementSource> GetReturnCandidates()
        {
            return _returnCandidates == null ? Enumerable.Empty<ElementSource>() : _returnCandidates.AsEnumerable();
        }

        public IEnumerable<int> GetStackElementDefinitions(int ilIndex, int stackLevel)
        {
            return StackInfluenceAnalysis.GetStackElementDefinitions(ilIndex, stackLevel, CFG);
        }

        private Dictionary<int, HashSet<LocalMutation>> _localMutations;
        public IEnumerable<LocalMutation> GetLocalMutations(int ilIndex)
        {
            return _localMutations.Get(ilIndex);
        }

        private Dictionary<VariabilityPattern, VariabilityAnalyzer> _varaMap =
            new Dictionary<VariabilityPattern, VariabilityAnalyzer>();

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

        public bool IsSideEffectFree
        {
            get { return Method.GetCustomOrInjectedAttribute(typeof(ISideEffectFree)) != null; }
        }

        public bool IsStaticEvaluation
        {
            get { return Method.GetCustomOrInjectedAttribute(typeof(StaticEvaluation)) != null; }
        }

        public List<int> UnrollHeaders { get; private set; }
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

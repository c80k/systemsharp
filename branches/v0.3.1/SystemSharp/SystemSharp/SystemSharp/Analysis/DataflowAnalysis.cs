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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using SystemSharp.Collections;
using SystemSharp.Components;
using SystemSharp.SetAlgorithms;
using SystemSharp.SysDOM;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Analysis
{
    /// <summary>
    /// This class captures the set of reaching definitions for each variable of a given context
    /// </summary>
    class VarAssignmentSet
    {
        public enum EMergeResult
        {
            NoChange,
            Change
        }

        private HashSet<int>[] _asmts;
        private bool[] _fixedLocals;
        private bool[] _sideFx;
        private bool _succMerge;

        /// <summary>
        /// Constructs a new instance for a specific number of local variables
        /// </summary>
        /// <param name="numVariables">number of local variables</param>
        public VarAssignmentSet(int numVariables)
        {
            _asmts = new HashSet<int>[numVariables];
            _sideFx = new bool[numVariables];
            _fixedLocals = new bool[numVariables];
        }

        /// <summary>
        /// Indicates that there are unmanageable side effects on all local variables, denying the validity of reaching definition information
        /// </summary>
        public void IndicateSideEffects()
        {
            for (int i = 0; i < _sideFx.Length; i++)
            {
                if (_asmts[i] != null)
                    _sideFx[i] = true;
            }
        }

        /// <summary>
        /// Queries whether there is a possible side effect on a particular variable
        /// </summary>
        /// <param name="localIndex">local variable index</param>
        /// <returns>whether there is a side effect on specified variable</returns>
        public bool HasSideEffects(int localIndex)
        {
            return _sideFx[localIndex];
        }

        /// <summary>
        /// Constructs an initial state by indiciating each variable to be reached by a virtual entry point with index -1
        /// </summary>
        public void SetInitial()
        {
            for (int i = 0; i < _asmts.Length; i++)
                _asmts.Add(i, -1);
        }

        /// <summary>
        /// Updates the state by adding a reaching definition to a certain variable
        /// </summary>
        /// <param name="localIndex">local variable index</param>
        /// <param name="rhs">index of reaching definition</param>
        public void Assign(int localIndex, int rhs)
        {
            if (_asmts[localIndex] != null)
                _asmts[localIndex].Clear();
            _asmts.Add(localIndex, rhs);
            _sideFx[localIndex] = false;
            _fixedLocals[localIndex] = true;
        }

        /// <summary>
        /// Merges another state into this state by taking the set unions of all their respective reaching definitions
        /// </summary>
        /// <param name="other">another state</param>
        /// <returns>indication whether this state was changed by the merging procedure</returns>
        public EMergeResult Merge(VarAssignmentSet other)
        {
            EMergeResult result = _succMerge ? EMergeResult.NoChange : EMergeResult.Change;
            _succMerge = true;

            for (int i = 0; i < other._asmts.Length; i++)
            {
                if (_fixedLocals[i])
                    continue;

                HashSet<int> rhss = other._asmts[i];
                if (rhss != null)
                {
                    foreach (int rhs in rhss)
                    {
                        if (_asmts.Add(i, rhs))
                            result = EMergeResult.Change;
                    }
                }
                if (other._sideFx[i] && !_sideFx[i])
                {
                    _sideFx[i] = true;
                    result = EMergeResult.Change;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns all reaching definition for a given local variable
        /// </summary>
        /// <param name="localIndex">local variable index</param>
        /// <returns>all its reaching definitions</returns>
        public IEnumerable<int> GetStorePoints(int localIndex)
        {
            return _asmts.Get(localIndex);
        }
    }

    /// <summary>
    /// A debugging aid: Any method tagged with this attribute will cause the analysis stage to trigger a breakpoint before data-flow analysis.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BreakOnDataflowAnalysis :
        Attribute
    {
    }

    /// <summary>
    /// This class implements a data-flow analysis on a given control-flow graph.
    /// </summary>
    /// <typeparam name="Ti"></typeparam>
    public class DataflowAnalyzer<Ti>
        where Ti: IInstruction
    {
        /// <summary>
        /// This class describes a write/read location pair which can be eliminated
        /// </summary>
        public class ReadAfterWritePair
        {
            /// <summary>
            /// The index of the write instruction (consecutive number, not IL offset!)
            /// </summary>
            public int StorePoint { get; private set; }

            /// <summary>
            /// The index of the read instruction (consecutive number, not IL offset!)
            /// </summary>
            public int ReadPoint { get; private set; }

            public ReadAfterWritePair(int storePoint, int readPoint)
            {
                StorePoint = storePoint;
                ReadPoint = readPoint;
            }
        }

        class SetAdapter : ISetAdapter<int>
        {
            private IPropMap<int, int> _indexMap;

            public SetAdapter(IPropMap<int, int> indexMap)
            {
                _indexMap = indexMap;
            }

            #region ISetAdapter<int> Member

            public IPropMap<int, int> Index
            {
                get { return _indexMap; }
            }

            #endregion
        }

        /// <summary>
        /// Instruction information service
        /// </summary>
        public IInstructionInfo<Ti> InstructionInfo { get; private set; }

        /// <summary>
        /// Extended instruction information service
        /// </summary>
        public IExtendedInstructionInfo<Ti> XInstructionInfo { get; private set; }

        /// <summary>
        /// Control-flow graph the be analyzed
        /// </summary>
        public ControlFlowGraph<Ti> Code { get; private set; }

        private int _numLocals;
        private VarAssignmentSet[] _preConds;
        private VarAssignmentSet[] _postConds;
        private bool[] _inhibitElimination;
        private Ti _curILI;
        private VarAssignmentSet _curPreCond;
        private VarAssignmentSet _curPostCond;

        /// <summary>
        /// Maps store points to the set of use points of a variable.
        /// </summary>
        private Dictionary<int, HashSet<int>> _varUses = new Dictionary<int, HashSet<int>>();

        /// <summary>
        /// Maps each local variable to the set of locations where it is read.
        /// </summary>
        private HashSet<int>[] _loadPoints;

        /// <summary>
        /// Maps each local variable to the set of locations where it is written.
        /// </summary>
        private HashSet<int>[] _storePoints;

        /// <summary>
        /// Maps each program location to the local variable it references (that means whose address it loads).
        /// If a certain location does not reference any local variable, the array element has the value -1.
        /// </summary>
        private int[] _refPoints;

        /// <summary>
        /// Maps each point where a local variable is referenced by address to the locations it is actually read or written.
        /// </summary>
        private Dictionary<int, List<int>> _refUses;

        private Dictionary<int, int> _writeToRead;
        private HashSet<int> _writtenAndNeverRead;
        private HashSet<int> _eliminableLocals;

        /// <summary>
        /// Maps each tuple of a local variable index and a store/read point to its renaming index.
        /// </summary>
        private Dictionary<Tuple<int,int>, int> _renamings;

        /// <summary>
        /// Indicates a local variable to be indispensable, e.g. because it is accessed by memory location
        /// </summary>
        /// <param name="localIndex">index of local variable to be marked as indispensable</param>
        private void InhitbitElimination(int localIndex)
        {
            _inhibitElimination[localIndex] = true;
        }

        /// <summary>
        /// Queries whether a certain variable is indispensable
        /// </summary>
        /// <param name="localIndex">index of local variable</param>
        /// <returns>whether specified local variable is indispensable</returns>
        private bool IsEliminationInhibited(int localIndex)
        {
            return _inhibitElimination[localIndex];
        }

        private void HandleCall()
        {
            _curPostCond.IndicateSideEffects();
        }

        private void HandleLdLoc(int localIndex)
        {
            foreach (int storePoint in _curPreCond.GetStorePoints(localIndex))
                _varUses.Add(storePoint, _curILI.Index);
            _loadPoints[localIndex].Add(_curILI.Index);
        }

        private void HandleLdLoca(int localIndex)
        {
            _refPoints[_curILI.Index] = localIndex;
        }

        private void HandleStLoc(int localIndex)
        {
            _curPostCond.Assign(localIndex, _curILI.Index);
            _storePoints[localIndex].Add(_curILI.Index);
        }

        /// <summary>
        /// Constructs a new instance for a given control-flow graph
        /// </summary>
        /// <param name="cfg">control-flow graph</param>
        /// <param name="numLocals">number of local variables</param>
        public DataflowAnalyzer(ControlFlowGraph<Ti> cfg, int numLocals)
        {
            InstructionInfo = cfg.InstructionInfo;
            XInstructionInfo = InstructionInfo as IExtendedInstructionInfo<Ti>;
            Code = cfg;
            _numLocals = numLocals;
            DetectReadAfterWrite = true;
            DetectWriteAndNeverRead = true;
        }

        /// <summary>
        /// Detect read-after-write dependencies (and allow to eliminate them if possible)
        /// </summary>
        public bool DetectReadAfterWrite { get; set; }

        /// <summary>
        /// Detect writes to variables which are never read (and allow to eliminate them if possible)
        /// </summary>
        public bool DetectWriteAndNeverRead { get; set; }

        /// <summary>
        /// Disallow optimizations where read and write access reside in different basic blocks
        /// </summary>
        public bool DoNotOptimizeAcrossBasicBlocks { get; set; }

        private IEnumerable<int> GetReferencedLocals(IEnumerable<int> defPoints, out bool containsUndefined)
        {
            Contract.Requires(defPoints != null);

            IEnumerable<int> tmp = defPoints.Select(i => _refPoints[i]).Distinct();
            IEnumerable<int> result = tmp.Where(i => i != -1);
            containsUndefined = tmp.Contains(-1);
            return result;
        }

        private void ProcessInstruction()
        {
            switch (InstructionInfo.Classify(_curILI))
            {
                case EInstructionClass.Call:
                    HandleCall();
                    break;

                case EInstructionClass.LocalVariableAccess:
                    {
                        int localIndex;
                        ELocalVariableAccess lva = InstructionInfo.IsLocalVariableAccess(_curILI, out localIndex);
                        switch (lva)
                        {
                            case ELocalVariableAccess.ReadVariable:
                                HandleLdLoc(localIndex);
                                break;

                            case ELocalVariableAccess.WriteVariable:
                                HandleStLoc(localIndex);
                                break;

                            case ELocalVariableAccess.AddressOfVariable:
                                HandleLdLoca(localIndex);
                                if (XInstructionInfo == null)
                                    InhitbitElimination(localIndex);
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        private void ProcessInstructionExtended()
        {
            IEnumerable<ReferenceInfo> inds = XInstructionInfo.GetIndirections(_curILI, Code);
            foreach (ReferenceInfo ri in inds)
            {
                if (ri.Mode.HasFlag(ReferenceInfo.EMode.Read))
                {
                    bool containsUndefined;
                    IEnumerable<int> refLocals = GetReferencedLocals(ri.ReachingDefinitions, out containsUndefined);
                    foreach (int localIndex in refLocals)
                    {
                        if (ri.Kind == ReferenceInfo.EKind.Indirect)
                            InhitbitElimination(localIndex);
                        HandleLdLoc(localIndex);
                    }
                }

                if (ri.Mode.HasFlag(ReferenceInfo.EMode.Write))
                {
                    bool containsUndefined;
                    IEnumerable<int> refLocals = GetReferencedLocals(ri.ReachingDefinitions, out containsUndefined);
                    if (refLocals.Count() > 1)
                    {
                        foreach (int localIndex in refLocals)
                        {
                            InhitbitElimination(localIndex);
                        }
                    }
                    else if (refLocals.Count() == 1)
                    {
                        int localIndex = refLocals.Single();
                        if (ri.Kind == ReferenceInfo.EKind.Indirect)
                            InhitbitElimination(localIndex);
                        HandleStLoc(localIndex);
                    }
                }

                foreach (int ldaPoint in ri.ReachingDefinitions)
                {
                    _refUses.Add(ldaPoint, _curILI.Index);
                }
            }
        }

        private void IterateToFixPoint()
        {
            List<Ti> instrs = Code.Instructions;
            _preConds = new VarAssignmentSet[instrs.Count];
            _postConds = new VarAssignmentSet[instrs.Count];
            _refPoints = new int[instrs.Count];
            _refUses = new Dictionary<int, List<int>>();
            _inhibitElimination = new bool[_numLocals];
            Queue<int> q = new Queue<int>();
            _loadPoints = new HashSet<int>[_numLocals];
            _storePoints = new HashSet<int>[_numLocals];
            for (int i = 0; i < _numLocals; i++)
            {
                if (Code.IsLocalPinned(i))
                    InhitbitElimination(i);
                _loadPoints[i] = new HashSet<int>();
                _storePoints[i] = new HashSet<int>();
            }
            for (int i = 0; i < instrs.Count; i++)
            {
                _preConds[i] = new VarAssignmentSet(_numLocals);
                _postConds[i] = new VarAssignmentSet(_numLocals);
                _refPoints[i] = -1;
            }
            int startIndex = Code.EntryCB.StartIndex;
            _preConds[startIndex].SetInitial();
            q.Enqueue(startIndex);

            IExtendedInstructionInfo<Ti> xinfo = InstructionInfo as IExtendedInstructionInfo<Ti>;
            while (q.Count > 0)
            {
                int iidx = q.Dequeue();
                _curILI = Code.Instructions[iidx];
                _curPreCond = _preConds[iidx];
                _curPostCond = _postConds[iidx];
                _curPostCond.Merge(_curPreCond);
                ProcessInstruction();
                if (xinfo != null)
                {
                    ProcessInstructionExtended();
                }
                Ti[] succs = Code.GetSuccessorsOf(_curILI);
                foreach (Ti succ in succs)
                {
                    int siidx = succ.Index;
                    if (_preConds[siidx].Merge(_curPostCond) ==
                        VarAssignmentSet.EMergeResult.Change)
                    {
                        q.Enqueue(siidx);
                    }
                }
            }

            // Post-process to make sure each variable has at least one assignment location
            // Variables which are not assigned in the course of the program are assumed
            // to possess a "virtual assignment" location at IL index -1.
            for (int i = 0; i < _numLocals; i++)
            {
                if (_storePoints[i].Count == 0)
                    _storePoints[i].Add(-1);
            }
        }

        private void DetectEliminableAccesses()
        {
            _writeToRead = new Dictionary<int, int>();
            _eliminableLocals = new HashSet<int>();
            _writtenAndNeverRead = new HashSet<int>();

            for (int localIndex = 0; localIndex < _numLocals; localIndex++)
            {
                if (IsEliminationInhibited(localIndex))
                    continue;

                HashSet<int> storePoints = _storePoints[localIndex];
                int numEliminatedAccesses = 0;
                foreach (int storePoint in storePoints)
                {
                    if (storePoint < 0)
                        continue;

                    var storeBB = Code.GetBasicBlockContaining(storePoint);
                    HashSet<int> uses = _varUses.Get(storePoint);

                    // Assignment never used?
                    if (DetectWriteAndNeverRead && uses.Count == 0)
                    {
                        _writtenAndNeverRead.Add(storePoint);
                        numEliminatedAccesses++;
                    }
                    // Used exactly once?
                    else if (DetectReadAfterWrite && uses.Count == 1)
                    {
                        int readPoint = uses.Single();
                        var readBB = Code.GetBasicBlockContaining(readPoint);

                        // If variable is read within a different basic block than is
                        // is stored, do not optimize.
                        if (DoNotOptimizeAcrossBasicBlocks && !readBB.Equals(storeBB))
                            continue;

                        VarAssignmentSet preCond = _preConds[readPoint];
                        if (preCond.HasSideEffects(localIndex))
                            continue;
                        IEnumerable<int> readPointStores = preCond.GetStorePoints(localIndex);
                        // Variable definition must be unambiguous
                        if (readPointStores.Count() != 1)
                            continue;

                        Debug.Assert(readPointStores.Single() == storePoint);

                        //_readAfterWritePairs.Add(new ReadAfterWritePair(storePoint, readPoint));
                        _writeToRead[storePoint] = readPoint;
                        numEliminatedAccesses++;
                    }
                }

                if (numEliminatedAccesses == storePoints.Count)
                {
                    // All variable accesses could be eliminated
                    // ==> local variable is completely superfluous
                    _eliminableLocals.Add(localIndex);
                }
            }
        }

        private void RenameLocals()
        {
            HashBasedPropMap<int, int> indexMap = new HashBasedPropMap<int, int>();
            int nextIndex = 0;

            for (int localIndex = 0; localIndex < _numLocals; localIndex++)
            {
                HashSet<int> storePoints = _storePoints[localIndex];            
                foreach (int storePoint in storePoints)
                {
                    if (!indexMap.ContainsKey(storePoint))
                    {
                        indexMap[storePoint] = nextIndex++;
                    }
                    HashSet<int> uses = _varUses.Get(storePoint);
                    foreach (int usePoint in uses)
                    {
                        if (!indexMap.ContainsKey(usePoint))
                        {
                            indexMap[usePoint] = nextIndex++;
                        }
                    }
                }
            }

            ISetAdapter<int> a = new SetAdapter(indexMap);
            IEnumerable<int> orderedKeys = indexMap.Keys.OrderBy(key => indexMap[key]);
            UnionFind<int> uf = new UnionFind<int>(a, orderedKeys.ToList());

            for (int localIndex = 0; localIndex < _numLocals; localIndex++)
            {
                HashSet<int> storePoints = _storePoints[localIndex];
                foreach (int storePoint in storePoints)
                {
                    HashSet<int> uses = _varUses.Get(storePoint);
                    foreach (int usePoint in uses)
                    {
                        uf.Union(storePoint, usePoint);
                    }
                }
            }

            _renamings = new Dictionary<Tuple<int, int>, int>();
            for (int localIndex = 0; localIndex < _numLocals; localIndex++)
            {
                HashSet<int> storePoints = _storePoints[localIndex];
                nextIndex = 0;
                foreach (int storePoint in storePoints)
                {
                    int repr = uf.Find(storePoint);
                    int index;
                    if (!_renamings.TryGetValue(Tuple.Create(localIndex, repr), out index))
                    {
                        index = nextIndex++;
                        _renamings[Tuple.Create(localIndex, repr)] = index;
                    }
                    _renamings[Tuple.Create(localIndex, storePoint)] = index;
                    HashSet<int> uses = _varUses.Get(storePoint);
                    foreach (int usePoint in uses)
                    {
                        _renamings[Tuple.Create(localIndex, usePoint)] = index;
                    }
                }
            }
        }

        /// <summary>
        /// Executes data-flow analysis
        /// </summary>
        public void Run()
        {
            IHasAttributes attrs = Code as IHasAttributes;
            if (attrs != null && attrs.HasAttribute<BreakOnDataflowAnalysis>())
            {
                Debugger.Break();
            }

            // Fixpoint iteration until the set of pre- and postconditions of each
            // instruction is complete.
            IterateToFixPoint();

            // Analyze load points to determine which read accesses can be inlined.
            DetectEliminableAccesses();

            // Try to split locals into distinct variables
            RenameLocals();
        }

        /// <summary>
        /// Queries whether there is a unique read-after-write dependency
        /// </summary>
        /// <param name="writePoint">instruction index of write access</param>
        /// <param name="readPoint">instruction index of read access, undefined if there is no unique dependency</param>
        /// <returns>whether there is a unique read-after-write dependency</returns>
        public bool IsReadAfterWrite(int writePoint, out int readPoint)
        {
            return _writeToRead.TryGetValue(writePoint, out readPoint);
        }

        /// <summary>
        /// Queries whether a certain local variable may be eliminated, i.e. by replacing it with its right-hand-side expression
        /// </summary>
        /// <param name="localIndex">index of local variable</param>
        /// <returns>whether given variable is eliminable</returns>
        public bool IsEliminable(int localIndex)
        {
            return _eliminableLocals.Contains(localIndex);
        }

        /// <summary>
        /// Queries whether a write access is actually superfluous because the assigned value is never read
        /// </summary>
        /// <param name="writePoint">index of writing instruction</param>
        /// <returns>whether write access is superfluous</returns>
        public bool IsWrittenAndNeverRead(int writePoint)
        {
            return _writtenAndNeverRead.Contains(writePoint);
        }

        /// <summary>
        /// Returns all locations of superfluous write accesses
        /// </summary>
        public ISet<int> WrittenAndNeverReadPoints
        {
            get { return _writtenAndNeverRead; }
        }

        /// <summary>
        /// Returns all eliminable local variables
        /// </summary>
        public ISet<int> EliminableLocals
        {
            get { return _eliminableLocals; }
        }

        /// <summary>
        /// Retrieves possible IL locations which write a to given local variable and reach a specified IL location.
        /// </summary>
        /// <param name="localIndex">The local variable index</param>
        /// <param name="ilIndex">An instruction index (not byte offset!) whose reaching variable definitions should be retrieved</param>
        /// <returns>An enumeration of IL instruction indices (not byte offsets) which write to the variable in the given context</returns>
        public IEnumerable<int> GetAssignmentsForReadPoint(int localIndex, int ilIndex)
        {
            return _preConds[ilIndex].GetStorePoints(localIndex);
        }

        /// <summary>
        /// Retrieves possible IL locations which write to a given local variable and reach all instructions which follow a specified IL location.
        /// </summary>
        /// <param name="localIndex">The local variable index</param>
        /// <param name="preIlIndex">The instruction index (not byte offset!)</param>
        /// <returns>An enumeration of IL instruction indices (not byte offsets) which write to the variable in the given context</returns>
        public IEnumerable<int> GetAssignmentsForReadPointFrom(int localIndex, int preIlIndex)
        {
            return _postConds[preIlIndex].GetStorePoints(localIndex);
        }

        /// <summary>
        /// Retrieves the number of renamed variables which emerged from a local variable.
        /// </summary>
        /// <param name="localIndex">The local variable index</param>
        /// <returns>The number of renamed variables</returns>
        public int GetNumRenamings(int localIndex)
        {
            HashSet<int> storePoints = _storePoints[localIndex];
            HashSet<int> renames = new HashSet<int>();
            foreach (int storePoint in storePoints)
            {
                renames.Add(_renamings[Tuple.Create(localIndex, storePoint)]);
            }
            return renames.Count;
        }

        /// <summary>
        /// Retrieves the renamed variable (sub-)index which is accessed at a given program location.
        /// </summary>
        /// <param name="ilIndex">The local vairable index</param>
        /// <param name="ilIndex">The program location (IL index, not offset) which accesses the local variable.</param>
        /// <returns>The renaming index</returns>
        /// <remarks>
        /// The variable name is determined uniquely by the tuple (localIndex, renamingIndex) where localIndex is the index of the
        /// local variable, and renamingIndex is the value returned by this method.
        /// </remarks>
        public int GetRenamingIndex(int localIndex, int ilIndex)
        {
            int result;
            if (!_renamings.TryGetValue(Tuple.Create(localIndex, ilIndex), out result))
                result = 0;
            return result;
        }

        private static List<int> _emptyList = new List<int>();

        /// <summary>
        /// Retrieves the program locations which derefence a local variable which is referenced at a given program location.
        /// </summary>
        /// <param name="ilIndex">A program location which references (loads the address of) a local variable</param>
        /// <returns>The set of program location which actually access the variable by the loaded address</returns>
        public List<int> GetUsePointsOfRefPoint(int ilIndex)
        {
            List<int> result;
            if (!_refUses.TryGetValue(ilIndex, out result))
                result = _emptyList;
            return result;
        }
    }
}

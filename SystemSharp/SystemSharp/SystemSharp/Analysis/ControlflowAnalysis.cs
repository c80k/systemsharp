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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.TreeAlgorithms;

namespace SystemSharp.Analysis
{
    public interface IInstruction
    {
        int Index { get; set;  }
    }

    public enum EInstructionClass
    {
        Branch,
        LocalVariableAccess,
        Call,
        Other
    }

    public enum EBranchBehavior
    { 
        NoBranch,
        UBranch,
        CBranch,
        Switch,
        Return,
        Throw
    }

    public enum ELocalVariableAccess
    {
        NoAccess,
        ReadVariable,
        WriteVariable,
        AddressOfVariable
    }

    public interface IInstructionResource
    {
        bool ConflictsWith(IInstructionResource other);
    }

    public enum EInstructionResourceAccess
    {
        NoResource,
        Reading,
        Writing
    }

    public interface IInstructionInfo<Ti>
    {
        EInstructionClass Classify(Ti i);
        EBranchBehavior IsBranch(Ti i, out IEnumerable<int> targets);
        ELocalVariableAccess IsLocalVariableAccess(Ti i, out int localIndex);
        EInstructionResourceAccess UsesResource(Ti i, out IInstructionResource resource);
    }

    public static class InstructionInfoExtensions
    {
        public static IEnumerable<int> GetSuccessors<Ti>(this IInstructionInfo<Ti> ii, Ti i)
            where Ti: IInstruction
        {
            IEnumerable<int> targets;
            IEnumerable<int> foll = Enumerable.Repeat(i.Index, 1);
            switch (ii.IsBranch(i, out targets))
            {
                case EBranchBehavior.CBranch:
                case EBranchBehavior.Switch:
                    return targets.Concat(foll);

                case EBranchBehavior.NoBranch:
                    return foll;

                case EBranchBehavior.Return:
                case EBranchBehavior.Throw:
                    return Enumerable.Empty<int>();

                case EBranchBehavior.UBranch:
                    return targets;

                default:
                    throw new NotImplementedException();
            }
        }
    }

    public class ReferenceInfo
    {
        [Flags]
        public enum EMode
        {
            NoAccess = 0x0,
            Read = 0x1,
            Write = 0x2
        }

        public enum EKind
        {
            Assignment,
            Indirect
        }

        public EMode Mode { get; private set; }
        public EKind Kind { get; private set; }
        public IEnumerable<int> ReachingDefinitions { get; private set; }

        public ReferenceInfo(EMode mode, EKind kind, IEnumerable<int> reachingDefinitions)
        {
            Mode = mode;
            Kind = kind;
            ReachingDefinitions = reachingDefinitions;
        }
    }

    public interface IExtendedInstructionInfo<Ti>: IInstructionInfo<Ti>
        where Ti : IInstruction
    {
        IEnumerable<ReferenceInfo> GetIndirections(Ti i, ControlFlowGraph<Ti> cfg);
    }

    public class ControlFlowGraph<Ti> 
        where Ti: IInstruction
    {
        private bool[] _branches;
        private bool[] _branchTargets;
        private Ti[][] _successors;
        private Ti[][] _predecessors;
        private Ti _marshal;

        /// <summary>
        /// IL index of the first instruction to be executed
        /// </summary>
        private int _entryPoint;

        /// <summary>
        /// Additional IL indices of instructions which should be considered as exit instructions,
        /// thus instructions which cause the code to return control to the caller.
        /// </summary>
        private ISet<int> _exitPoints;

        public IInstructionInfo<Ti> InstructionInfo { get; private set; }
        public List<Ti> Instructions { get; private set; }
        public BasicBlock<Ti> EntryCB { get; private set; }
        public BasicBlock<Ti>[] BasicBlocks { get; private set; }

        public ControlFlowGraph(IEnumerable<Ti> instructions, Ti marshal, IInstructionInfo<Ti> iinfo, int entryPoint = 0, ISet<int> exitPoints = null)
        {
            InstructionInfo = iinfo;
            Instructions = new List<Ti>(instructions);
            Instructions.Add(marshal);
            int numInstrs = Instructions.Count;
            _marshal = marshal;
            _entryPoint = entryPoint;
            _exitPoints = exitPoints;
            _branches = new bool[numInstrs];
            _branchTargets = new bool[numInstrs];
            _successors = new Ti[numInstrs][];
            _successors[numInstrs - 1] = new Ti[0];
            _predecessors = new Ti[numInstrs][];
            Setup();
        }

        public virtual bool IsLocalPinned(int local)
        {
            return false;
        }

        private void MarkAsBranch(Ti ili)
        {
            Contract.Requires<ArgumentException>(ili.Index >= 0);
            Contract.Requires<ArgumentException>(ili.Index < Instructions.Count);

            _branches[ili.Index] = true;
        }

        public bool IsBranch(int index)
        {
            Contract.Requires<ArgumentException>(index >= 0);
            Contract.Requires<ArgumentException>(index < Instructions.Count);

            return _branches[index];
        }

        private void MarkAsBranchTarget(Ti ili)
        {
            Contract.Requires<ArgumentException>(ili.Index >= 0);
            Contract.Requires<ArgumentException>(ili.Index < Instructions.Count);

            _branchTargets[ili.Index] = true;
        }

        public bool IsBranchTarget(int index)
        {
            return _branchTargets[index];
        }

        private void AnalyzeBranches()
        {
            MarkAsBranchTarget(Instructions[_entryPoint]);
            Ti[] term = new Ti[] { _marshal };
            MarkAsBranch(_marshal);
            MarkAsBranchTarget(_marshal);
            foreach (Ti ili in Instructions)
            {
                if (_exitPoints != null && _exitPoints.Contains(ili.Index))
                {
                    _successors[ili.Index] = term;
                    MarkAsBranch(ili);
                }
                else if (ili.Index == _marshal.Index)
                {
                    _successors[ili.Index] = new Ti[0];
                }
                else
                {
                    IEnumerable<int> targets;
                    EBranchBehavior bb = InstructionInfo.IsBranch(ili, out targets);
                    bool includeNext = false;
                    bool isBranch = false;
                    bool isTerm = false;
                    switch (bb)
                    {
                        case EBranchBehavior.NoBranch:
                            includeNext = true;
                            isBranch = false;
                            isTerm = false;
                            break;

                        case EBranchBehavior.UBranch:
                            includeNext = false;
                            isBranch = true;
                            isTerm = false;
                            break;

                        case EBranchBehavior.CBranch:
                            includeNext = true;
                            isBranch = true;
                            isTerm = false;
                            break;

                        case EBranchBehavior.Return:
                        case EBranchBehavior.Throw:
                            includeNext = false;
                            isBranch = true;
                            isTerm = true;
                            break;

                        case EBranchBehavior.Switch:
                            includeNext = true;
                            isBranch = true;
                            isTerm = false;
                            break;

                        default:
                            throw new NotImplementedException();
                    }

                    if (isBranch)
                        MarkAsBranch(ili);

                    foreach (int target in targets)
                        MarkAsBranchTarget(Instructions[target]);

                    if (isTerm && ili.Index < Instructions.Count - 1)
                    {
                        _successors[ili.Index] = term;
                    }
                    else if (includeNext)
                    {
                        List<Ti> succs = new List<Ti>();
                        succs.Add(Instructions[ili.Index + 1]);
                        succs.AddRange(targets.Select(t => Instructions[t]));
                        _successors[ili.Index] = succs.ToArray();
                    }
                    else
                    {
                        _successors[ili.Index] = targets.Select(t => Instructions[t]).ToArray();
                    }
                }
            }
        }

        protected void ComputePredecessors()
        {
            List<Ti>[] tmp = new List<Ti>[Instructions.Count];
            for (int i = 0; i < tmp.Length; i++)
            {
                tmp[i] = new List<Ti>();
            }
            foreach (Ti i in Instructions)
            {
                Ti[] succs = GetSuccessorsOf(i);
                foreach (Ti j in succs)
                {
                    tmp[j.Index] .Add(i);
                }
            }
            for (int i = 0; i < tmp.Length; i++)
            {
                _predecessors[i] = tmp[i].ToArray();
            }
        }

        public Ti[] GetSuccessors(int index)
        {
            return _successors[index];
        }

        public Ti[] GetSuccessorsOf(Ti ili)
        {
            return _successors[ili.Index];
        }

        public Ti[] GetPredecessors(int index)
        {
            return _predecessors[index];
        }

        public Ti[] GetPredecessorsOf(Ti ili)
        {
            return _predecessors[ili.Index];
        }

        public int GetIDomIndex(int index)
        {
            BasicBlock<Ti> bb = GetBasicBlockContaining(index);
            if (bb.StartIndex == index)
            {
                BasicBlock<Ti> bbidom = bb.IDom;
                if (bbidom == bb)
                    return index;
                else
                    return bbidom.EndIndex;
            }
            else
            {
                return GetPredecessors(index).Single().Index;
            }
        }

        private int _maxBBSize;
        public int MaxBBSize
        {
            get
            {
                if (_maxBBSize == 0)
                    _maxBBSize = BasicBlocks.Max(bb => bb.Range.Count);
                return _maxBBSize;
            }
        }

        public int GetPostOrderIndex(int index)
        {
            BasicBlock<Ti> bb = GetBasicBlockContaining(index);
            return bb.PostOrderIndex * MaxBBSize + index;
        }

        public IEnumerable<int> GetLCASet(IEnumerable<int> query)
        {
            List<int> result = new List<int>();
            TreeOperations.GetLCATree(query,
                new DelegatePropMap<int, int>(GetIDomIndex),
                0,
                new DelegatePropMap<int, int>(GetPostOrderIndex),
                new DelegatePropMap<int, int>((i, p) => { result.Add(p); }));
            return result;
        }

        private bool IsEndOfBB(int index)
        {
            return IsBranch(index) ||
                IsBranchTarget(index + 1);
        }

        public BasicBlock<Ti> GetBasicBlockStartingAt(int index)
        {
            return BasicBlocks.Where(bb => bb.StartIndex == index).Single();
        }

        public BasicBlock<Ti> GetBasicBlockContaining(int index)
        {
            return BasicBlocks.Where(bb => bb.StartIndex <= index && bb.EndIndex >= index).Single();
        }

        protected virtual BasicBlock<Ti> CreateBasicBlock(int startIndex, int lastIndex)
        {
            return new BasicBlock<Ti>(startIndex, lastIndex, this);
        }

        private void InferBasicBlocks()
        {
            List<BasicBlock<Ti>> bbs = new List<BasicBlock<Ti>>();
            int curIndex = 0;
            do
            {
                int startIndex = curIndex;
                int lastIndex = curIndex;
                while (!IsEndOfBB(lastIndex))
                    ++lastIndex;
                BasicBlock<Ti> bb = CreateBasicBlock(startIndex, lastIndex);
                if (curIndex == Instructions.Count - 1)
                {
                    bb.IsExitBlock = true;
                }
                else
                {
                    IEnumerable<int> targets;
                    EBranchBehavior bbh = InstructionInfo.IsBranch(Instructions[lastIndex], out targets);
                    if (bbh == EBranchBehavior.Switch)
                    {
                        // Special case: evil switch instructions
                        bb.IsSwitch = true;
                    }
                }

                bbs.Add(bb);

                curIndex = lastIndex + 1;
            } while (curIndex < Instructions.Count);
            BasicBlocks = bbs.ToArray();
        }

        private void MarkSwitchTargets()
        {
            foreach (BasicBlock<Ti> cb in BasicBlocks)
            {
                if (cb.IsSwitch)
                {
                    BasicBlock<Ti>[] succs = cb.Successors;
                    foreach (BasicBlock<Ti> succ in succs)
                        succ.IsSwitchTarget = true;
                }
            }
        }

        protected virtual void AnalyzeLoops()
        {
            IGraphAdapter<BasicBlock<Ti>> a = BasicBlock<Ti>.LoopAnalysisAdapter;
            BasicBlock<Ti>[] reachable = a.GetPreOrder(BasicBlocks, EntryCB);
            a.InvertRelation(a.Succs, a.Preds, reachable);
            a.AnalyzeLoops(reachable, EntryCB);
        }

        protected virtual void ComputeDominators()
        {
            IGraphAdapter<BasicBlock<Ti>> a = BasicBlock<Ti>.DominanceAnalysisAdapter;
            a.InvertRelation(a.Succs, a.Preds, BasicBlocks);
            a.ComputeImmediateDominators(BasicBlocks, EntryCB);
        }

        /*protected virtual void ComputePostDominators()
        {
            BasicBlock<Ti> exit = BasicBlocks.Last();
            IGraphAdapter<BasicBlock<Ti>> a = BasicBlock<Ti>.PostDominanceAnalysisAdapter;
            a.InvertRelation(a.Succs, a.Preds, BasicBlocks);
            a.ComputeImmediatePostDominators(BasicBlocks, exit);
        }*/

        protected virtual void ComputeBasicBlocks()
        {
            AnalyzeBranches();
            ComputePredecessors();
            InferBasicBlocks();
            MarkSwitchTargets();
            BasicBlock<Ti> entry = GetBasicBlockStartingAt(_entryPoint);
            EntryCB = entry;
        }

        protected virtual void Setup()
        {
            ComputeBasicBlocks();
            AnalyzeLoops();
            ComputeDominators();
            //ComputePostDominators();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (BasicBlock<Ti> bb in BasicBlocks)
            {
                sb.AppendLine(bb.ToString());
            }
            return sb.ToString();
        }
    }

    public class BasicBlock<Ti> : 
        IComparable<BasicBlock<Ti>>
        where Ti: IInstruction
    {
        public int StartIndex { get; private set; }
        public int EndIndex { get; private set; }
        public ControlFlowGraph<Ti> Code { get; private set; }
        public bool IsExitBlock { get; internal set; }

        public bool IsLoop
        {
            get { return Type == ENodeType.Reducible || Type == ENodeType.Self; }
        }

        public bool IsReachable
        {
            get { return PreOrderIndex >= 0; }
        }

        public bool IsConditional { get; internal set; }
        public bool IsCondition { get; internal set; }
        public bool IsBranch { get; internal set; }
        public bool IsSwitch { get; internal set; }
        public bool IsSwitchTarget { get; internal set; }

        public BasicBlock<Ti>[] Predecessors { get; private set; }
        public BasicBlock<Ti> IDom { get; private set; }
        //public BasicBlock<Ti> IPDom { get; private set; }
        public BasicBlock<Ti>[] Dominatees { get; private set; }
        //public BasicBlock<Ti>[] PostDominatees { get; private set; }
        public int PostOrderIndex { get; private set; }
        public int PreOrderIndex { get; private set; }
        public BasicBlock<Ti> PostOrderParent { get; private set; }
        public BasicBlock<Ti>[] PostOrderChildren { get; private set; }
        public BasicBlock<Ti> PreOrderLast { get; private set; }
        public ENodeType Type { get; private set; }
        public HashSet<BasicBlock<Ti>> BackPreds { get; private set; }
        public HashSet<BasicBlock<Ti>> NonBackPreds { get; private set; }
        public HashSet<BasicBlock<Ti>> RedBackIn { get; private set; }
        public HashSet<BasicBlock<Ti>> OtherIn { get; private set; }
        public BasicBlock<Ti> Header { get; private set; }

        internal int UnrollDepth { get; set; }

        private List<BasicBlock<Ti>> _predecessors = new List<BasicBlock<Ti>>();
        private List<BasicBlock<Ti>> _dominatees = new List<BasicBlock<Ti>>();
        private List<BasicBlock<Ti>> _tempList;

        public BasicBlock(int startIndex, int endIndex, ControlFlowGraph<Ti> cfg)
        {
            Code = cfg;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public IList<Ti> Range
        {
            get
            {
                Contract.Requires(EndIndex - StartIndex + 1 >= 0);
                Contract.Requires(EndIndex < Code.Instructions.Count);

                return Code.Instructions.GetRange(StartIndex, EndIndex - StartIndex + 1);
            }
        }

        private static void Indent(StringBuilder sb, int indent)
        {
            for (int i = 0; i < indent; i++)
                sb.Append("  ");
        }

        internal string ToStringInternal(int indent)
        {
            StringBuilder sb = new StringBuilder();
            Indent(sb, indent);
            if (IsLoop)
                sb.Append("LOOP ");
            if (IsConditional)
                sb.Append("COND ");
            if (IsCondition)
                sb.Append("IF ");
            if (IsBranch)
                sb.Append("THEN/ELSE ");
            sb.AppendFormat("{{ T:{0} H:{1} Pre:{2} Post:{3}\n", 
                Type,
                Header == null ? "?" : Header.StartIndex.ToString(),
                PreOrderIndex, PostOrderIndex);
            foreach (Ti ili in Range)
            {
                Indent(sb, indent + 1);
                sb.AppendLine(ili.ToString());
            }
            for (int i = 0; i < indent; i++)
                sb.Append("  ");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public override string ToString()
        {
            return ToStringInternal(0);
        }

        public BasicBlock<Ti>[] Successors
        {
            get
            {
                Ti[] succs = Code.GetSuccessors(EndIndex);
                BasicBlock<Ti>[] result = succs.Select(i => Code.GetBasicBlockStartingAt(i.Index)).ToArray();
                return result;
            }
        }

        public BasicBlock<Ti>[] SuccessorsWithoutSwitchTargets
        {
            get
            {
                BasicBlock<Ti>[] succs = Successors;
                if (IsSwitch)
                {
                    return succs;
                }
                else
                {
                    return succs.Where(x => !x.IsSwitchTarget && !x.IsExitBlock).ToArray();
                }
            }
        }

        public BasicBlock<Ti>[] SuccessorsWithoutExitBlock
        {
            get
            {
                BasicBlock<Ti>[] succs = Successors;
                if (IsSwitch)
                {
                    return succs;
                }
                else
                {
                    return succs.Where(x => !x.IsExitBlock).ToArray();
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is BasicBlock<Ti>)
            {
                BasicBlock<Ti> cb = (BasicBlock<Ti>)obj;
                if (cb.Code != Code)
                    return false;
                return StartIndex == cb.StartIndex &&
                    EndIndex == cb.EndIndex;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return StartIndex.GetHashCode() * 13 ^ EndIndex.GetHashCode();
        }

        #region IComparable<CodeBlock<Ti>> Members

        public int CompareTo(BasicBlock<Ti> other)
        {
            if (StartIndex < other.StartIndex)
            {
                if (EndIndex < other.EndIndex)
                    return -1;
                else if (EndIndex >= other.EndIndex)
                    return 0;
                else
                    throw new InvalidOperationException("Intervals overlap");
            }
            else if (StartIndex == other.StartIndex)
            {
                return 0;
            }
            else
            {
                if (StartIndex > other.EndIndex)
                    return 1;
                else if (EndIndex <= other.EndIndex)
                    return 0;
                else
                    throw new InvalidOperationException("Intervals overlap");
            }
        }

        #endregion

        public bool BelongsToLoop(BasicBlock<Ti> header)
        {
            BasicBlock<Ti> cur = this;
            while (cur != null && !cur.Equals(header))
            {
                cur = cur.Header;
            }
            return (cur != null);
        }

        public bool Contains(int index)
        {
            return index >= StartIndex && index <= EndIndex;
        }

        public bool Contains(Ti i)
        {
            return Contains(i.Index);
        }

        public bool IsAncestor(BasicBlock<Ti> grandChild)
        {
            return LoopAnalysisAdapter.IsAncestor(this, grandChild);
        }

        public static IGraphAdapter<BasicBlock<Ti>> LoopAnalysisAdapter
        {
            get
            {
                return new DefaultTreeAdapter<BasicBlock<Ti>>(
                    null,
                    x => x.SuccessorsWithoutSwitchTargets, null,
                    x => x.Predecessors, (x, y) => x.Predecessors = y,
                    x => x._tempList, (x, l) => x._tempList = l,
                    x => x.PreOrderIndex, (x, i) => x.PreOrderIndex = i,
                    x => x.PreOrderLast, (x, y) => x.PreOrderLast = y,
                    x => x.PostOrderIndex, (x, i) => { throw new InvalidOperationException(); },
                    x => x.Type, (x, t) => x.Type = t,
                    x => x.BackPreds, (x, s) => x.BackPreds = s,
                    x => x.NonBackPreds, (x, s) => x.NonBackPreds = s,
                    x => x.RedBackIn, (x, s) => x.RedBackIn = s,
                    x => x.OtherIn, (x, s) => x.OtherIn = s,
                    x => x.Header, (x, y) => x.Header = y,
                    x => x.IDom, (x, y) => x.IDom = y,
                    //x => x.IPDom, (x, y) => x.IPDom = y,
                    x => x.Dominatees, (x, y) => x.Dominatees = y
                    //,x => x.PostDominatees, (x, y) => x.PostDominatees = y
                    );
            }
        }

        public static IGraphAdapter<BasicBlock<Ti>> DominanceAnalysisAdapter
        {
            get
            {
                return new DefaultTreeAdapter<BasicBlock<Ti>>(
                    null,
                    x => x.SuccessorsWithoutExitBlock, null,
                    x => x.Predecessors, (x, y) => x.Predecessors = y,
                    x => x._tempList, (x, l) => x._tempList = l,
                    x => x.PreOrderIndex, (x, i) => x.PreOrderIndex = i,
                    x => x.PreOrderLast, (x, y) => x.PreOrderLast = y,
                    x => x.PostOrderIndex, (x, i) => x.PostOrderIndex = i,
                    x => x.Type, (x, t) => x.Type = t,
                    x => x.BackPreds, (x, s) => x.BackPreds = s,
                    x => x.NonBackPreds, (x, s) => x.NonBackPreds = s,
                    x => x.RedBackIn, (x, s) => x.RedBackIn = s,
                    x => x.OtherIn, (x, s) => x.OtherIn = s,
                    x => x.Header, (x, y) => x.Header = y,
                    x => x.IDom, (x, y) => x.IDom = y,
                    //x => x.IPDom, (x, y) => x.IPDom = y,
                    x => x.Dominatees, (x, y) => x.Dominatees = y
                    //,x => x.PostDominatees, (x, y) => x.PostDominatees = y
                    );
            }
        }

        /*public static IGraphAdapter<BasicBlock<Ti>> PostDominanceAnalysisAdapter
        {
            get
            {
                return new DefaultTreeAdapter<BasicBlock<Ti>>(
                    null,
                    x => x.Successors, null,
                    x => x.Predecessors, (x, y) => x.Predecessors = y,
                    x => x._tempList, (x, l) => x._tempList = l,
                    x => x.PreOrderIndex, (x, i) => x.PreOrderIndex = i,
                    x => x.PreOrderLast, (x, y) => x.PreOrderLast = y,
                    x => x.PostOrderIndex, (x, i) => x.PostOrderIndex = i,
                    x => x.Type, (x, t) => x.Type = t,
                    x => x.BackPreds, (x, s) => x.BackPreds = s,
                    x => x.NonBackPreds, (x, s) => x.NonBackPreds = s,
                    x => x.RedBackIn, (x, s) => x.RedBackIn = s,
                    x => x.OtherIn, (x, s) => x.OtherIn = s,
                    x => x.Header, (x, y) => x.Header = y,
                    x => x.IDom, (x, y) => x.IDom = y,
                    x => x.IPDom, (x, y) => x.IPDom = y,
                    x => x.Dominatees, (x, y) => x.Dominatees = y,
                    x => x.PostDominatees, (x, y) => x.PostDominatees = y
                    );
            }
        }*/
    }
}

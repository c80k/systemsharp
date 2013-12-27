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
    /// <summary>
    /// This interface associates instructions with a unique, 0-based indices
    /// </summary>
    public interface IInstruction
    {
        int Index { get; set;  }
    }

    /// <summary>
    /// Classifies instructions into categories
    /// </summary>
    public enum EInstructionClass
    {
        /// <summary>
        /// A branch instruction
        /// </summary>
        Branch,

        /// <summary>
        /// An access to a local variable
        /// </summary>
        LocalVariableAccess,

        /// <summary>
        /// A method call
        /// </summary>
        Call,

        /// <summary>
        /// Everything else
        /// </summary>
        Other
    }

    /// <summary>
    /// Classifies branch instructions into categories
    /// </summary>
    public enum EBranchBehavior
    { 
        /// <summary>
        /// Not a branch instruction
        /// </summary>
        NoBranch,

        /// <summary>
        /// Unconditional branch
        /// </summary>
        UBranch,

        /// <summary>
        /// Conditional branch
        /// </summary>
        CBranch,

        /// <summary>
        /// Switch (i.e. case select) instruction
        /// </summary>
        Switch,

        /// <summary>
        /// Method return instruction
        /// </summary>
        Return,

        /// <summary>
        /// Exception throw instruction
        /// </summary>
        Throw
    }

    /// <summary>
    /// Classifies local variable access instructions into categories
    /// </summary>
    public enum ELocalVariableAccess
    {
        /// <summary>
        /// Not a local variable access
        /// </summary>
        NoAccess,

        /// <summary>
        /// Read access to local variable
        /// </summary>
        ReadVariable,

        /// <summary>
        /// Write acces to local variable
        /// </summary>
        WriteVariable,

        /// <summary>
        /// Address of local variable
        /// </summary>
        AddressOfVariable
    }

    /// <summary>
    /// Abstract representation of a resource which might be accessed by an instruction
    /// </summary>
    public interface IInstructionResource
    {
        /// <summary>
        /// Tells whether this resource potentially conflicts with another resource (e.g. because they are the same)
        /// </summary>
        /// <param name="other">another resource</param>
        /// <returns>whether resources potentially conflict</returns>
        bool ConflictsWith(IInstructionResource other);
    }

    /// <summary>
    /// Classifies resource accesses into categories
    /// </summary>
    public enum EInstructionResourceAccess
    {
        /// <summary>
        /// Not a resource access
        /// </summary>
        NoResource,

        /// <summary>
        /// Reading resource access
        /// </summary>
        Reading,

        /// <summary>
        /// Writing resource access
        /// </summary>
        Writing
    }

    /// <summary>
    /// Generic interface for services classifying instructions of some type
    /// </summary>
    /// <typeparam name="Ti">data type representing instructions being classified</typeparam>
    public interface IInstructionInfo<Ti>
    {
        /// <summary>
        /// Classifies a given instruction into categories
        /// </summary>
        /// <param name="i">an instruction</param>
        /// <returns>instruction category</returns>
        EInstructionClass Classify(Ti i);

        /// <summary>
        /// Classifies an instruction into branch categories
        /// </summary>
        /// <param name="i">an instruction</param>
        /// <param name="targets">possible branch targets</param>
        /// <returns>branch category</returns>
        EBranchBehavior IsBranch(Ti i, out IEnumerable<int> targets);

        /// <summary>
        /// Classifies an instruction into local variable access categories
        /// </summary>
        /// <param name="i">an instruction</param>
        /// <param name="localIndex">index of local variable being accessed (undefined, of no local variable is accessed)</param>
        /// <returns>local variable access category</returns>
        ELocalVariableAccess IsLocalVariableAccess(Ti i, out int localIndex);

        /// <summary>
        /// Classifies an instruction into resource access categories
        /// </summary>
        /// <param name="i">an instruction</param>
        /// <param name="resource">resource being accessed by instruction (null, if no resource is accessed)</param>
        /// <returns>resource access category</returns>
        EInstructionResourceAccess UsesResource(Ti i, out IInstructionResource resource);
    }

    public static class InstructionInfoExtensions
    {
        /// <summary>
        /// Retrieves all possible successors of a given instruction
        /// </summary>
        /// <typeparam name="Ti">data type representing an instruction</typeparam>
        /// <param name="ii">instruction information service</param>
        /// <param name="i">an instruction</param>
        /// <returns>all possible successors in terms of their instruction indices</returns>
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

    /// <summary>
    /// This class captures information on instructions which reference memory locations (i.e. indirections)
    /// </summary>
    public class ReferenceInfo
    {
        /// <summary>
        /// The flag field indicates whether memory location is read and/or written
        /// </summary>
        [Flags]
        public enum EMode
        {
            NoAccess = 0x0,
            Read = 0x1,
            Write = 0x2
        }

        /// <summary>
        /// Classifies whether memory location is part of an assignment or just loaded
        /// </summary>
        public enum EKind
        {
            Assignment,
            Indirect
        }

        /// <summary>
        /// Tells whether memory location is read and/or written
        /// </summary>
        public EMode Mode { get; private set; }

        /// <summary>
        /// Tells whether memory location is part of an assignment or just loaded
        /// </summary>
        public EKind Kind { get; private set; }

        /// <summary>
        /// All reaching definitions, i.e. indices of all instructions which might contribute the actual memory location being referenced
        /// </summary>
        public IEnumerable<int> ReachingDefinitions { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="mode">whether memory location is read and/or written</param>
        /// <param name="kind">whether memory location is part of an assignment or just loaded</param>
        /// <param name="reachingDefinitions">all reaching definitions, 
        /// i.e. indices of all instructions which might contribute the actual memory location being referenced</param>
        public ReferenceInfo(EMode mode, EKind kind, IEnumerable<int> reachingDefinitions)
        {
            Mode = mode;
            Kind = kind;
            ReachingDefinitions = reachingDefinitions;
        }
    }

    /// <summary>
    /// An extended instruction information service, supporting information on instructions dealing with memory locations
    /// </summary>
    /// <typeparam name="Ti">type of instructions being represented</typeparam>
    public interface IExtendedInstructionInfo<Ti>: IInstructionInfo<Ti>
        where Ti : IInstruction
    {
        /// <summary>
        /// Retrieves all indirections which an instruction might undertake
        /// </summary>
        /// <param name="i">an instruction</param>
        /// <param name="cfg">containing control-flow graph</param>
        /// <returns>all possible indirections</returns>
        IEnumerable<ReferenceInfo> GetIndirections(Ti i, ControlFlowGraph<Ti> cfg);
    }

    /// <summary>
    /// This data structure represents a control-flow graph
    /// </summary>
    /// <typeparam name="Ti">Type of instructions being represented</typeparam>
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

        /// <summary>
        /// Instruction information service
        /// </summary>
        public IInstructionInfo<Ti> InstructionInfo { get; private set; }

        /// <summary>
        /// Instruction sequence
        /// </summary>
        public List<Ti> Instructions { get; private set; }

        /// <summary>
        /// Entry point
        /// </summary>
        public BasicBlock<Ti> EntryCB { get; private set; }

        /// <summary>
        /// All basic blocks
        /// </summary>
        public BasicBlock<Ti>[] BasicBlocks { get; private set; }

        /// <summary>
        /// Constructs a new instance based on an instruction sequence
        /// </summary>
        /// <param name="instructions">instruction sequence</param>
        /// <param name="marshal">the instruction which should be treated as exit point</param>
        /// <param name="iinfo">instruction information service</param>
        /// <param name="entryPoint">optional entry point for graph construction (as instruction index)</param>
        /// <param name="exitPoints">optional set of additional exit points (as instruction indices)</param>
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

        /// <summary>
        /// Tells whether a local variable is pinned, see http://msdn.microsoft.com/en-us/library/f58wzh21%28v=vs.110%29.aspx
        /// </summary>
        /// <remarks>
        /// Default implementation returns always false. Override method to implement desired behavior. This feature was never
        /// tested in reality and probably should be removed in future releases, since pinned variables are only needed in unsafe
        /// code, and unsafe code is a very bad idea for hardware modeling anyway.
        /// </remarks>
        /// <param name="local">index of local variable</param>
        /// <returns>whether variable is pinned</returns>
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

        /// <summary>
        /// Given an instruction index, returns all possible successor instructions
        /// </summary>
        /// <param name="index">instruction index</param>
        /// <returns>all possible successor instruction</returns>
        public Ti[] GetSuccessors(int index)
        {
            return _successors[index];
        }

        /// <summary>
        /// Given an instruction, returns all possible successor instructions
        /// </summary>
        /// <param name="ili">an instruction</param>
        /// <returns>all possible successor instructions</returns>
        public Ti[] GetSuccessorsOf(Ti ili)
        {
            return _successors[ili.Index];
        }

        /// <summary>
        /// Given an instruction index, returns all possible predecessor instructions
        /// </summary>
        /// <param name="index">instruction index</param>
        /// <returns>all possible predecessor instruction</returns>
        public Ti[] GetPredecessors(int index)
        {
            return _predecessors[index];
        }

        /// <summary>
        /// Given an instruction, returns all possible predecessor instructions
        /// </summary>
        /// <param name="ili">an instruction</param>
        /// <returns>all possible predecessor instructions</returns>
        public Ti[] GetPredecessorsOf(Ti ili)
        {
            return _predecessors[ili.Index];
        }

        /// <summary>
        /// Given an instruction index, returns the index of its immediate dominator
        /// </summary>
        /// <param name="ili">instruction index</param>
        /// <returns>immediate dominator index</returns>
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
        
        /// <summary>
        /// Returns the number of instructions inside the largest basic block
        /// </summary>
        public int MaxBBSize
        {
            get
            {
                if (_maxBBSize == 0)
                    _maxBBSize = BasicBlocks.Max(bb => bb.Range.Count);
                return _maxBBSize;
            }
        }

        /// <summary>
        /// Given an instruction index, returns its associated post-order index
        /// </summary>
        /// <remarks>
        /// The post order index is the index assigned to a graph node during post-order traversal. 
        /// See http://en.wikipedia.org/wiki/Tree_traversal
        /// </remarks>
        /// <param name="index">instruction index</param>
        /// <returns>its post-order index</returns>
        public int GetPostOrderIndex(int index)
        {
            BasicBlock<Ti> bb = GetBasicBlockContaining(index);
            return bb.PostOrderIndex * MaxBBSize + index;
        }

        /// <summary>
        /// Given a set of instruction indices, returns the set of their lowest common ancestors.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the basic block which begins at the supplied instruction index
        /// </summary>
        /// <param name="index">instruction index</param>
        /// <returns>the basic block which begins at the supplied instruction index</returns>
        public BasicBlock<Ti> GetBasicBlockStartingAt(int index)
        {
            return BasicBlocks.Where(bb => bb.StartIndex == index).Single();
        }

        /// <summary>
        /// Returns the basic block which contains a given instruction
        /// </summary>
        /// <param name="index">instruction index</param>
        /// <returns>basic block which contains an instruction having the supplied index</returns>
        public BasicBlock<Ti> GetBasicBlockContaining(int index)
        {
            return BasicBlocks.Where(bb => bb.StartIndex <= index && bb.EndIndex >= index).Single();
        }

        /// <summary>
        /// Creates a basic block for a range of instructions. Override this if you want to create a specialized basic block implementation.
        /// </summary>
        /// <remarks>
        /// This method does not check whether the given instruction range formally meets the criterion of a basic block, e.g. having no
        /// conditional branches except for the last instruction. It is up to the caller to supply valid arguments.
        /// </remarks>
        /// <param name="startIndex">index of first instruction inside the basic block</param>
        /// <param name="lastIndex">index of last instruction inside the basic block</param>
        /// <returns>a basic block for the specified instruction range</returns>
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

        /// <summary>
        /// Analyzes the loop nesting structure of the control-flow graph.
        /// The default implementation directs the tasks to Havlak's algorithm.
        /// </summary>
        protected virtual void AnalyzeLoops()
        {
            IGraphAdapter<BasicBlock<Ti>> a = BasicBlock<Ti>.LoopAnalysisAdapter;
            BasicBlock<Ti>[] reachable = a.GetPreOrder(BasicBlocks, EntryCB);
            a.InvertRelation(a.Succs, a.Preds, reachable);
            a.AnalyzeLoops(reachable, EntryCB);
        }

        /// <summary>
        /// Computes the immediate dominator of each basic block.
        /// The default implementation directs the task to the Cooper-Harvey-Kennedy algorithm.
        /// </summary>
        protected virtual void ComputeDominators()
        {
            IGraphAdapter<BasicBlock<Ti>> a = BasicBlock<Ti>.DominanceAnalysisAdapter;
            a.InvertRelation(a.Succs, a.Preds, BasicBlocks);
            a.ComputeImmediateDominators(BasicBlocks, EntryCB);
        }

        /// <summary>
        /// Constructs basic blocks from the instruction sequence.
        /// </summary>
        protected virtual void ComputeBasicBlocks()
        {
            AnalyzeBranches();
            ComputePredecessors();
            InferBasicBlocks();
            MarkSwitchTargets();
            BasicBlock<Ti> entry = GetBasicBlockStartingAt(_entryPoint);
            EntryCB = entry;
        }

        /// <summary>
        /// Constructs all essential data structures. The default implementation computes basic blocks,
        /// loop nesting structure and immediate dominators. Override if you need to compute more.
        /// </summary>
        protected virtual void Setup()
        {
            ComputeBasicBlocks();
            AnalyzeLoops();
            ComputeDominators();
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

    /// <summary>
    /// This class represents a basic block.
    /// </summary>
    /// <typeparam name="Ti">Data type of instructions inside the basic block.</typeparam>
    public class BasicBlock<Ti> : 
        IComparable<BasicBlock<Ti>>
        where Ti: IInstruction
    {
        /// <summary>
        /// Index of first instruction covered by this basic block
        /// </summary>
        public int StartIndex { get; private set; }

        /// <summary>
        /// Index of last instruction covered by this basic block
        /// </summary>
        public int EndIndex { get; private set; }

        /// <summary>
        /// Containing control-flow graph
        /// </summary>
        public ControlFlowGraph<Ti> Code { get; private set; }

        /// <summary>
        /// Tells whether this basic block is an exit node
        /// </summary>
        public bool IsExitBlock { get; internal set; }

        /// <summary>
        /// Tells whether this basic block is a loop header
        /// </summary>
        public bool IsLoop
        {
            get { return Type == ENodeType.Reducible || Type == ENodeType.Self; }
        }

        /// <summary>
        /// Tells whether this basic block is reachable from the entry point
        /// </summary>
        public bool IsReachable
        {
            get { return PreOrderIndex >= 0; }
        }

        /// <summary>
        /// Tells whether this basic block end with a conditional branch
        /// </summary>
        public bool IsCondition { get; internal set; }

        /// <summary>
        /// Tells whether this basic block ends with a branch
        /// </summary>
        public bool IsBranch { get; internal set; }

        /// <summary>
        /// Tells whether this basic block ends with a switch (case select) instruction
        /// </summary>
        public bool IsSwitch { get; internal set; }

        /// <summary>
        /// Tells whether this basic block is directly reachable from a switch (case select) instruction
        /// </summary>
        public bool IsSwitchTarget { get; internal set; }

        /// <summary>
        /// All predecessors of this basic block
        /// </summary>
        public BasicBlock<Ti>[] Predecessors { get; private set; }

        /// <summary>
        /// The immediate dominator of this basic block
        /// </summary>
        public BasicBlock<Ti> IDom { get; private set; }

        /// <summary>
        /// All basic blocks whose immediate dominator is this basic block
        /// </summary>
        public BasicBlock<Ti>[] Dominatees { get; private set; }

        /// <summary>
        /// Post-order index of this basic block
        /// </summary>
        /// <remarks>
        /// The post-order index is determined by post-order traversal of the control-flow graph.
        /// See http://en.wikipedia.org/wiki/Tree_traversal
        /// </remarks>
        public int PostOrderIndex { get; private set; }

        /// <summary>
        /// Pre-order index of this basic block
        /// </summary>
        /// <remarks>
        /// The pre-order index is determined by pre-order traversal of the control-flow graph.
        /// See http://en.wikipedia.org/wiki/Tree_traversal
        /// </remarks>
        public int PreOrderIndex { get; private set; }

        /// <summary>
        /// The parent of this block in the post-order traversal tree
        /// </summary>
        public BasicBlock<Ti> PostOrderParent { get; private set; }

        /// <summary>
        /// The children of this block in the post-order traversal tree
        /// </summary>
        public BasicBlock<Ti>[] PostOrderChildren { get; private set; }

        /// <summary>
        /// The last basic block visited during pre-order traversal. See Cooper-Harvey-Kennedy paper for formal definition.
        /// </summary>
        public BasicBlock<Ti> PreOrderLast { get; private set; }

        /// <summary>
        /// Loop header classification due to Havlak's loop analysis algorithm
        /// </summary>
        public ENodeType Type { get; private set; }

        /// <summary>
        /// As defined in Havlak's paper on loop analysis
        /// </summary>
        public HashSet<BasicBlock<Ti>> BackPreds { get; private set; }

        /// <summary>
        /// As defined in Havlak's paper on loop analysis
        /// </summary>
        public HashSet<BasicBlock<Ti>> NonBackPreds { get; private set; }

        /// <summary>
        /// As defined in Havlak's paper on loop analysis
        /// </summary>
        public HashSet<BasicBlock<Ti>> RedBackIn { get; private set; }

        /// <summary>
        /// As defined in Havlak's paper on loop analysis
        /// </summary>
        public HashSet<BasicBlock<Ti>> OtherIn { get; private set; }

        /// <summary>
        /// If this basic block is part of a loop, that loop is identified by its header.
        /// </summary>
        public BasicBlock<Ti> Header { get; private set; }

        /// <summary>
        /// Used internally during decompilation to keep track of loop unrolling
        /// </summary>
        internal int UnrollDepth { get; set; }

        private List<BasicBlock<Ti>> _predecessors = new List<BasicBlock<Ti>>();
        private List<BasicBlock<Ti>> _dominatees = new List<BasicBlock<Ti>>();
        private List<BasicBlock<Ti>> _tempList;

        /// <summary>
        /// Constructs a basic block covering a specific instruction range
        /// </summary>
        /// <param name="startIndex">index of first instruction covered by basic block</param>
        /// <param name="endIndex">index of last instruction covered by basic block</param>
        /// <param name="cfg">containing control-flow graph</param>
        public BasicBlock(int startIndex, int endIndex, ControlFlowGraph<Ti> cfg)
        {
            Code = cfg;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        /// <summary>
        /// Returns the instruction range covered by this basic block
        /// </summary>
        public IList<Ti> Range
        {
            get
            {
                Contract.Requires<ArgumentOutOfRangeException>(EndIndex - StartIndex + 1 >= 0);
                Contract.Requires<ArgumentOutOfRangeException>(EndIndex < Code.Instructions.Count);

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

        /// <summary>
        /// Returns all possible direct successors of this basic block
        /// </summary>
        public BasicBlock<Ti>[] Successors
        {
            get
            {
                Ti[] succs = Code.GetSuccessors(EndIndex);
                BasicBlock<Ti>[] result = succs.Select(i => Code.GetBasicBlockStartingAt(i.Index)).ToArray();
                return result;
            }
        }

        /// <summary>
        /// Returns all possible direct successors of this basic block, except for switch targets and the exit block
        /// </summary>
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

        /// <summary>
        /// Returns all possible direct successors of this basic block, except for the exit block
        /// </summary>
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

        /// <summary>
        /// Two basic blocks are defined to be equal iff they belong to the same control-flow graph and cover the same range of instructions.
        /// </summary>
        /// <param name="obj">another object</param>
        /// <returns>whether other instance equals this instance</returns>
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
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return StartIndex.GetHashCode() * 13 ^ EndIndex.GetHashCode();
        }

        #region IComparable<CodeBlock<Ti>> Members

        /// <summary>
        /// Compares this basic block to another basic block. The ordering is defined by the indices of their respective first instructions.
        /// </summary>
        /// <param name="other">some other basic block</param>
        /// <returns>-1 if this basic block comes before other basic block, 0 if they are the same, 1 is this basic block comes after other basic block</returns>
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

        /// <summary>
        /// Tests whether this basic block is part of a particular loop
        /// </summary>
        /// <param name="header">loop, specified by its header</param>
        /// <returns>whether this basic block is part of specified loop</returns>
        public bool BelongsToLoop(BasicBlock<Ti> header)
        {
            BasicBlock<Ti> cur = this;
            while (cur != null && !cur.Equals(header))
            {
                cur = cur.Header;
            }
            return (cur != null);
        }

        /// <summary>
        /// Tests whether this basic block contains a particular instruction
        /// </summary>
        /// <param name="index">instruction index</param>
        /// <returns>whether this basic block contains the instruction with specified index</returns>
        public bool Contains(int index)
        {
            return index >= StartIndex && index <= EndIndex;
        }

        /// <summary>
        /// Tests whether this basic block containts a particular instruction
        /// </summary>
        /// <param name="i">an instruction</param>
        /// <returns>whether this basic block contains the specified instruction</returns>
        public bool Contains(Ti i)
        {
            return Contains(i.Index);
        }

        /// <summary>
        /// Tests whether this basic block is an ancestor of another block, due to the pre-order traversal tree
        /// </summary>
        /// <param name="grandChild">another basic block</param>
        /// <returns>whether this basic block is an ancestor of specified basic block</returns>
        public bool IsAncestor(BasicBlock<Ti> grandChild)
        {
            return LoopAnalysisAdapter.IsAncestor(this, grandChild);
        }

        /// <summary>
        /// An adapter suitable for loop nesting analysis
        /// </summary>
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
                    x => x.Dominatees, (x, y) => x.Dominatees = y
                    );
            }
        }

        /// <summary>
        /// An adapter suitable for dominance analysis
        /// </summary>
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
                    x => x.Dominatees, (x, y) => x.Dominatees = y
                    );
            }
        }
    }
}

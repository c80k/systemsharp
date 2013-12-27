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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using SDILReader;
using SystemSharp.Analysis.Msil;
using SystemSharp.Components;

namespace SystemSharp.Analysis
{
    /// <summary>
    /// This helper class is for enumerating CIL opcodes
    /// </summary>
    public static class OpCodeReflector
    {
        /// <summary>
        /// Enumerates all CIL opcodes
        /// </summary>
        public static IEnumerable<OpCode> AllOpCodes
        {
            get
            {
                Type ot = typeof(OpCodes);
                FieldInfo[] fields = ot.GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (FieldInfo field in fields)
                {
                    yield return (OpCode)field.GetValue(null);
                }
            }
        }
    }

    /// <summary>
    /// This class implements the classification service for CIL instructions
    /// </summary>
    public class ILInstructionInfo : IExtendedInstructionInfo<ILInstruction>
    {
        private delegate EInstructionClass ClassifyHandler(ILInstruction ili);
        private delegate EBranchBehavior IsBranchHandler(ILInstruction ili, out IEnumerable<int> targets);
        private delegate ELocalVariableAccess IsLocalVariableAccessHandler(ILInstruction ili, out int localIndex);
        private delegate IEnumerable<ReferenceInfo> GetIndirectionsHandler(ILInstruction ili, ControlFlowGraph<ILInstruction> cfg);

        /// <summary>
        /// The method or constructor for which the service is constructed
        /// </summary>
        public MethodBase Method { get; private set; }

        /// <summary>
        /// The method body of the associated method or constructor
        /// </summary>
        public MethodBody Body { get; private set; }

        /// <summary>
        /// The instruction list of the associated method
        /// </summary>
        public List<ILInstruction> Instructions { get; private set; }

        /// <summary>
        /// The artificial exit instruction which serves as the single exit point of the method/constructor
        /// </summary>
        public ILInstruction Marshal { get; private set; }

        private ILInstruction[] _imap;
        private IEnumerable<ILInstruction> _noTarget = new ILInstruction[0];
        private Dictionary<OpCode, ClassifyHandler> _classifyHdlMap;
        private Dictionary<OpCode, IsBranchHandler> _isBranchHdlMap;
        private Dictionary<OpCode, IsLocalVariableAccessHandler> _isLvaHdlMap;
        private Dictionary<OpCode, GetIndirectionsHandler> _getIndHdlMap;

        /// <summary>
        /// Constructs an instance based on a method or constructor
        /// </summary>
        /// <param name="mi">a method or constructor</param>
        public ILInstructionInfo(MethodBase mi)
        {
            Method = mi;
            MethodBodyReader mbr = new MethodBodyReader(mi);
            Instructions = mbr.instructions;
            MethodBody body = mi.GetMethodBody();
            Body = body;
            byte[] msil = body.GetILAsByteArray();
            Marshal = new ILInstruction()
            {
                Offset = msil.Length,
                Index = Instructions.Count,
                Code = OpCodes.Ret
            };
            _imap = new ILInstruction[msil.Length + 1];
            foreach (ILInstruction ili in Instructions)
            {
                _imap[ili.Offset] = ili;
            }
            _imap[msil.Length] = Marshal;
            InitHandlerMaps();
        }

        /// <summary>
        /// Returns the instruction at a specific bytecode offset
        /// </summary>
        /// <param name="offset">a bytecode offset (NOT: instruction index!)</param>
        /// <returns>the instruction at specified bytecode offset</returns>
        public ILInstruction this[int offset]
        {
            get { return _imap[offset]; }
        }

        private IEnumerable<ILInstruction> SingleTarget(ILInstruction tgt)
        {
            return new ILInstruction[] { tgt };
        }

        private IEnumerable<ILInstruction> NoTarget()
        {
            return _noTarget;
        }

        #region Classify handlers

        private EInstructionClass ClassifyDefault(ILInstruction i)
        {
            return EInstructionClass.Other;
        }

        private EInstructionClass ClassifyBranch(ILInstruction i)
        {
            return EInstructionClass.Branch;
        }

        private EInstructionClass ClassifyLVA(ILInstruction i)
        {
            return EInstructionClass.LocalVariableAccess;
        }

        private EInstructionClass ClassifyCall(ILInstruction i)
        {
            return EInstructionClass.Call;
        }

        private EInstructionClass ClassifyUnsupported(ILInstruction i)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IsBranch handlers

        private EBranchBehavior IsBranchHandleStd(ILInstruction ili, out IEnumerable<int> targets)
        {
            // Handle NOPs like a branch to the next instruction
            // This will make the chained branch elimination also eliminate NOPs
            if (ili.Code.Value == OpCodes.Nop.Value)
            {
                targets = new int[] { ili.Index + 1 };
                return EBranchBehavior.UBranch;
            }
            else
            {
                targets = new int[0];
                return EBranchBehavior.NoBranch;
            }
        }

        private EBranchBehavior IsBranchHandleCBranch(ILInstruction ili, out IEnumerable<int> targets)
        {
            int target = (int)ili.Operand;
            ILInstruction ilit = this[target];
            Debug.Assert(ilit != null);
            targets = new int[] { ilit.Index };
            return EBranchBehavior.CBranch;
        }

        private EBranchBehavior IsBranchHandleUBranch(ILInstruction ili, out IEnumerable<int> targets)
        {
            int target = (int)ili.Operand;
            ILInstruction ilit = this[target];
            Debug.Assert(ilit != null);
            targets = new int[] { ilit.Index };
            return EBranchBehavior.UBranch;
        }

        private EBranchBehavior IsBranchHandleRet(ILInstruction ili, out IEnumerable<int> targets)
        {
            targets = new int[] { Marshal.Index };
            return EBranchBehavior.Return;
        }

        private EBranchBehavior IsBranchHandleThrow(ILInstruction ili, out IEnumerable<int> targets)
        {
            targets = new int[] { Marshal.Index };
            return EBranchBehavior.Throw;
        }

        private EBranchBehavior IsBranchHandleSwitch(ILInstruction ili, out IEnumerable<int> targets)
        {
            Contract.Requires(ili.Operand != null);

            int[] targetOffsets = (int[])ili.Operand;
            targets = targetOffsets.Select(offs => this[offs].Index);
            return EBranchBehavior.Switch;
        }

        private EBranchBehavior IsBranchUnsupported(ILInstruction ili, out IEnumerable<int> targets)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IsLocalVariableAccess handlers

        private ELocalVariableAccess IsLVA(int inIndex, ELocalVariableAccess inResult, out int localIndex)
        {
            localIndex = inIndex;
            return inResult;
        }

        #endregion

        #region GetIndirections handlers

        private IEnumerable<ReferenceInfo> NoIndirections(ILInstruction i, ControlFlowGraph<ILInstruction> cfg)
        {
            return new ReferenceInfo[0];
        }

        private IEnumerable<ReferenceInfo> HandleCall(ILInstruction ili, ControlFlowGraph<ILInstruction> cfg)
        {
            MethodBase mb = (MethodBase)ili.Operand;
            List<ReferenceInfo.EMode> modes = new List<ReferenceInfo.EMode>();
            if (!mb.IsStatic && !mb.IsConstructor)
            {
                modes.Add(ReferenceInfo.EMode.Read | ReferenceInfo.EMode.Write);
            }
            ParameterInfo[] pis = mb.GetParameters();
            foreach (ParameterInfo pi in pis)
            {
                if (pi.ParameterType.IsByRef)
                {
                    if (pi.IsOut)
                        modes.Add(ReferenceInfo.EMode.Write);
                    else
                        modes.Add(ReferenceInfo.EMode.Read | 
                            ReferenceInfo.EMode.Write);
                }
                else
                {
                    modes.Add(ReferenceInfo.EMode.NoAccess);
                }
            }
            if (ili.Code.Equals(OpCodes.Calli))
            {
                modes.Add(ReferenceInfo.EMode.Read | ReferenceInfo.EMode.Write);
            }
            return modes.Select((m, i) => new ReferenceInfo(m, ReferenceInfo.EKind.Indirect,
                StackInfluenceAnalysis.GetStackElementDefinitions(ili.Index, i - modes.Count + 1, (MethodCode)cfg)));
        }

        private IEnumerable<ReferenceInfo> HandleLdind(ILInstruction ili, ControlFlowGraph<ILInstruction> cfg)
        {
            yield return new ReferenceInfo(ReferenceInfo.EMode.Read, ReferenceInfo.EKind.Assignment,
                StackInfluenceAnalysis.GetStackElementDefinitions(ili.Index, 0, (MethodCode)cfg));
        }

        private IEnumerable<ReferenceInfo> HandleStind(ILInstruction ili, ControlFlowGraph<ILInstruction> cfg)
        {
            yield return new ReferenceInfo(ReferenceInfo.EMode.Write, ReferenceInfo.EKind.Assignment,
                StackInfluenceAnalysis.GetStackElementDefinitions(ili.Index, -1, (MethodCode)cfg));
        }

        #endregion

        private void InitHandlerMaps()
        {
            #region Classify handler map
            _classifyHdlMap = new Dictionary<OpCode, ClassifyHandler>();
            foreach (OpCode oc in OpCodeReflector.AllOpCodes)
            {
                _classifyHdlMap[oc] = ClassifyDefault;
            }
            _classifyHdlMap[OpCodes.Beq] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Beq_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bge] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bge_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bge_Un] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bge_Un_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bgt] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bgt_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bgt_Un] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bgt_Un_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Ble] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Ble_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Ble_Un] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Ble_Un_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Blt] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Blt_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Blt_Un] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Blt_Un_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bne_Un] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Bne_Un_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Br] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Br_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Brfalse] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Brfalse_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Brtrue] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Brtrue_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Jmp] = ClassifyUnsupported;
            _classifyHdlMap[OpCodes.Leave] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Leave_S] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Ret] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Rethrow] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Switch] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Tailcall] = ClassifyUnsupported;
            _classifyHdlMap[OpCodes.Throw] = ClassifyBranch;
            _classifyHdlMap[OpCodes.Call] = ClassifyCall;
            _classifyHdlMap[OpCodes.Calli] = ClassifyCall;
            _classifyHdlMap[OpCodes.Callvirt] = ClassifyCall;
            _classifyHdlMap[OpCodes.Ldloc] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Ldloc_0] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Ldloc_1] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Ldloc_2] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Ldloc_3] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Ldloc_S] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Ldloca] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Ldloca_S] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Stloc_0] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Stloc_1] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Stloc_2] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Stloc_3] = ClassifyLVA;
            _classifyHdlMap[OpCodes.Stloc_S] = ClassifyLVA;

            #endregion
            #region IsBranch handler map
            _isBranchHdlMap = new Dictionary<OpCode, IsBranchHandler>();
            foreach (OpCode oc in OpCodeReflector.AllOpCodes)
            {
                _isBranchHdlMap[oc] = IsBranchHandleStd;
            }
            _isBranchHdlMap[OpCodes.Beq] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Beq_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bge] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bge_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bge_Un] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bge_Un_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bgt] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bgt_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bgt_Un] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bgt_Un_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Ble] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Ble_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Ble_Un] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Ble_Un_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Blt] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Blt_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Blt_Un] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Blt_Un_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bne_Un] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Bne_Un_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Br] = IsBranchHandleUBranch;
            _isBranchHdlMap[OpCodes.Br_S] = IsBranchHandleUBranch;
            _isBranchHdlMap[OpCodes.Brfalse] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Brfalse_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Brtrue] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Brtrue_S] = IsBranchHandleCBranch;
            _isBranchHdlMap[OpCodes.Jmp] = IsBranchUnsupported;
            _isBranchHdlMap[OpCodes.Leave] = IsBranchHandleUBranch;
            _isBranchHdlMap[OpCodes.Leave_S] = IsBranchHandleUBranch;
            _isBranchHdlMap[OpCodes.Ret] = IsBranchHandleRet;
            _isBranchHdlMap[OpCodes.Rethrow] = IsBranchHandleThrow;
            _isBranchHdlMap[OpCodes.Switch] = IsBranchHandleSwitch;
            _isBranchHdlMap[OpCodes.Tailcall] = IsBranchUnsupported;
            _isBranchHdlMap[OpCodes.Throw] = IsBranchHandleThrow;
            #endregion
            #region IsLocalVariableAccess handler map
            _isLvaHdlMap = new Dictionary<OpCode, IsLocalVariableAccessHandler>();
            foreach (OpCode oc in OpCodeReflector.AllOpCodes)
            {
                _isLvaHdlMap[oc] = (ILInstruction i, out int li) => IsLVA(-1, ELocalVariableAccess.NoAccess, out li);
            }
            _isLvaHdlMap[OpCodes.Ldloc] = (ILInstruction i, out int li) => IsLVA((int)i.Operand, ELocalVariableAccess.ReadVariable, out li);
            _isLvaHdlMap[OpCodes.Ldloc_0] = (ILInstruction i, out int li) => IsLVA(0, ELocalVariableAccess.ReadVariable, out li);
            _isLvaHdlMap[OpCodes.Ldloc_1] = (ILInstruction i, out int li) => IsLVA(1, ELocalVariableAccess.ReadVariable, out li);
            _isLvaHdlMap[OpCodes.Ldloc_2] = (ILInstruction i, out int li) => IsLVA(2, ELocalVariableAccess.ReadVariable, out li);
            _isLvaHdlMap[OpCodes.Ldloc_3] = (ILInstruction i, out int li) => IsLVA(3, ELocalVariableAccess.ReadVariable, out li);
            _isLvaHdlMap[OpCodes.Ldloc_S] = (ILInstruction i, out int li) => IsLVA((byte)i.Operand, ELocalVariableAccess.ReadVariable, out li);
            _isLvaHdlMap[OpCodes.Ldloca] = (ILInstruction i, out int li) => IsLVA((int)i.Operand, ELocalVariableAccess.AddressOfVariable, out li);
            _isLvaHdlMap[OpCodes.Ldloca_S] = (ILInstruction i, out int li) => IsLVA((byte)i.Operand, ELocalVariableAccess.AddressOfVariable, out li);
            _isLvaHdlMap[OpCodes.Stloc] = (ILInstruction i, out int li) => IsLVA((int)i.Operand, ELocalVariableAccess.WriteVariable, out li);
            _isLvaHdlMap[OpCodes.Stloc_0] = (ILInstruction i, out int li) => IsLVA(0, ELocalVariableAccess.WriteVariable, out li);
            _isLvaHdlMap[OpCodes.Stloc_1] = (ILInstruction i, out int li) => IsLVA(1, ELocalVariableAccess.WriteVariable, out li);
            _isLvaHdlMap[OpCodes.Stloc_2] = (ILInstruction i, out int li) => IsLVA(2, ELocalVariableAccess.WriteVariable, out li);
            _isLvaHdlMap[OpCodes.Stloc_3] = (ILInstruction i, out int li) => IsLVA(3, ELocalVariableAccess.WriteVariable, out li);
            _isLvaHdlMap[OpCodes.Stloc_S] = (ILInstruction i, out int li) => IsLVA((byte)i.Operand, ELocalVariableAccess.WriteVariable, out li);
            #endregion
            #region GetIndirections handler map
            _getIndHdlMap = new Dictionary<OpCode, GetIndirectionsHandler>();
            foreach (OpCode oc in OpCodeReflector.AllOpCodes)
            {
                _getIndHdlMap[oc] = NoIndirections;
            }
            _getIndHdlMap[OpCodes.Call] = HandleCall;
            _getIndHdlMap[OpCodes.Calli] = HandleCall;
            _getIndHdlMap[OpCodes.Callvirt] = HandleCall;
            _getIndHdlMap[OpCodes.Ldind_I] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_I1] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_I2] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_I4] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_I8] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_R4] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_R8] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_Ref] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_U1] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_U2] = HandleLdind;
            _getIndHdlMap[OpCodes.Ldind_U4] = HandleLdind;
            _getIndHdlMap[OpCodes.Stind_I] = HandleStind;
            _getIndHdlMap[OpCodes.Stind_I1] = HandleStind;
            _getIndHdlMap[OpCodes.Stind_I2] = HandleStind;
            _getIndHdlMap[OpCodes.Stind_I4] = HandleStind;
            _getIndHdlMap[OpCodes.Stind_I8] = HandleStind;
            _getIndHdlMap[OpCodes.Stind_R4] = HandleStind;
            _getIndHdlMap[OpCodes.Stind_R8] = HandleStind;
            _getIndHdlMap[OpCodes.Stind_Ref] = HandleStind;
            _getIndHdlMap[OpCodes.Stobj] = HandleStind;
            #endregion
        }

        public virtual EInstructionClass Classify(ILInstruction i)
        {
            return _classifyHdlMap[i.Code](i);
        }

        public virtual EBranchBehavior IsBranch(ILInstruction i, out IEnumerable<int> targets)
        {
            return _isBranchHdlMap[i.Code](i, out targets);
        }

        public virtual ELocalVariableAccess IsLocalVariableAccess(ILInstruction i, out int localIndex)
        {
            return _isLvaHdlMap[i.Code](i, out localIndex);
        }

        public virtual EInstructionResourceAccess UsesResource(ILInstruction i, out IInstructionResource resource)
        {
            throw new NotImplementedException();
        }

        public virtual IEnumerable<ReferenceInfo> GetIndirections(ILInstruction i, ControlFlowGraph<ILInstruction> cfg)
        {
            return _getIndHdlMap[i.Code](i, cfg);
        }
    }

    /// <summary>
    /// This is a debugging aid: Any method or constructor which this attribute attached will cause design analysis to
    /// trigger a breakpoint before control-flow analysis is performed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, Inherited = true)]
    public class BreakOnControlflowAnalysis : Attribute
    {
    }

    /// <summary>
    /// A control-flow graph specialization for CLI methods
    /// </summary>
    public class MethodCode : 
        ControlFlowGraph<ILInstruction>,
        IHasAttributes
    {
        private int[] _preStackDepth;
        private int[] _postStackDepth;

        public MethodBase Method
        {
            get { return ((ILInstructionInfo)InstructionInfo).Method; }
        }

        public IList<LocalVariableInfo> LocalVariables { get; private set; }

        private static IEnumerable<ILInstruction> ReadInstructions(MethodBase mi)
        {
            MethodBodyReader mbr = new MethodBodyReader(mi);
            return mbr.instructions;
        }

        public static MethodCode Create(MethodBase mi, int entryPoint = 0, ISet<int> exitPoints = null)
        {
            ILInstructionInfo iinfo = new ILInstructionInfo(mi);
            return new MethodCode(iinfo, entryPoint, exitPoints);
        }

        public MethodCode(ILInstructionInfo iinfo, int entryPoint = 0, ISet<int> exitPoints = null)
            : base(iinfo.Instructions, iinfo.Marshal, iinfo, entryPoint, exitPoints)
        {
            LocalVariables = iinfo.Body.LocalVariables;
        }

        public new ILInstructionInfo InstructionInfo
        {
            get { return (ILInstructionInfo)base.InstructionInfo; }
        }

        public new MSILCodeBlock EntryCB
        {
            get { return (MSILCodeBlock)base.EntryCB; }
        }

        public new MSILCodeBlock[] BasicBlocks
        {
            get { return base.BasicBlocks.Cast<MSILCodeBlock>().ToArray(); }
        }

        public new MSILCodeBlock GetBasicBlockStartingAt(int index)
        {
            return (MSILCodeBlock)base.GetBasicBlockStartingAt(index);
        }

        public new MSILCodeBlock GetBasicBlockContaining(int index)
        {
            return (MSILCodeBlock)base.GetBasicBlockContaining(index);
        }

        public int NumLocals
        {
            get { return LocalVariables.Count; }
        }

        public override bool IsLocalPinned(int local)
        {
            return LocalVariables[local].IsPinned;
        }

        public int GetNextOffset(int offset)
        {
            int index = InstructionInfo[offset].Index + 1;
            if (index == Instructions.Count)
                return InstructionInfo.Marshal.Offset;
            else
                return Instructions[index].Offset;
        }

        public int GetPrevOffset(int offset)
        {
            int index = InstructionInfo[offset].Index - 1;
            if (index == -1)
                return -1;
            else
                return Instructions[index].Offset;
        }

        private void ComputeStackDepths(int startIndex, int stack)
        {
            int index = startIndex;
            int count = Instructions.Count;
            while (index < count)
            {
                ILInstruction ili = Instructions[index];
                int npop, npush;
                StackInfluenceAnalysis.GetStackBilance(ili, Method, out npop, out npush);
                int delta = npush - npop;

                _preStackDepth[ili.Index] = stack;
                stack += delta;
                if (stack < 0)
                    throw new InvalidOperationException("The MSIL code is ill-formed (stack underrun)");

                if (_postStackDepth[ili.Index] < 0)
                    _postStackDepth[ili.Index] = stack;
                else if (_postStackDepth[ili.Index] != stack)
                    throw new InvalidOperationException("The MSIL code is ill-formed (inconsistent stack behavior)");
                else
                    return;

                ILInstruction[] succs = GetSuccessorsOf(ili);
                if (succs.Length == 0)
                    return;
                else if (succs.Length == 1)
                {
                    index = succs[0].Index;
                    if (index == InstructionInfo.Marshal.Index)
                        return;
                }
                else
                {
                    foreach (ILInstruction succ in succs)
                    {
                        if (succ.Index != InstructionInfo.Marshal.Index)
                            ComputeStackDepths(succ.Index, stack);
                    }
                    return;
                }
            }
        }

        private void ComputeStackDepths()
        {
            _preStackDepth = new int[Instructions.Count];
            _postStackDepth = new int[Instructions.Count];
            for (int i = 0; i < _postStackDepth.Length; i++)
                _postStackDepth[i] = -1;
            ComputeStackDepths(EntryCB.StartIndex, 0);
            for (int i = 0; i < _postStackDepth.Length; i++)
            {
                if (_postStackDepth[i] < 0)
                    _postStackDepth[i] = 0;
            }
        }

        public int GetPreStackDepth(int index)
        {
            return _preStackDepth[index];
        }

        public int GetPostStackDepth(int index)
        {
            return _postStackDepth[index];
        }

        protected override void Setup()
        {
            if (Method.HasCustomOrInjectedAttribute<BreakOnControlflowAnalysis>())
                Debugger.Break();

            base.Setup();
            ComputeStackDepths();
        }

        protected override BasicBlock<ILInstruction> CreateBasicBlock(int startIndex, int lastIndex)
        {
            return new MSILCodeBlock(startIndex, lastIndex, this);
        }

        public Attribute[] GetAttributes()
        {
            return Method.GetCustomAttributes(true)
                .Cast<Attribute>()
                .ToArray();
        }
    }

    /// <summary>
    /// A specialization for basic blocks of CLI methods
    /// </summary>
    public class MSILCodeBlock : BasicBlock<ILInstruction>
    {
        public MSILCodeBlock(int startIndex, int endIndex, MethodCode mcode) :
            base(startIndex, endIndex, mcode)
        {
        }

        public new MethodCode Code
        {
            get { return (MethodCode)base.Code; }
        }

        public new MSILCodeBlock[] Successors
        {
            get { return base.Successors.Cast<MSILCodeBlock>().ToArray(); }
        }

        public new MSILCodeBlock[] SuccessorsWithoutExitBlock
        {
            get { return base.SuccessorsWithoutExitBlock.Cast<MSILCodeBlock>().ToArray(); }
        }

        public new MSILCodeBlock[] Dominatees
        {
            get { return base.Dominatees.Cast<MSILCodeBlock>().ToArray(); }
        }

        /// <summary>
        /// Returns the difference between operands pushed on the stack and operands popped from the stack.
        /// I.e. if this value is greater than 0, the basic block will leave new operands on the stack.
        /// </summary>
        public int StackBilance
        {
            get
            {
                return Code.GetPostStackDepth(EndIndex) -
                    Code.GetPreStackDepth(StartIndex);
            }
        }
    }
}

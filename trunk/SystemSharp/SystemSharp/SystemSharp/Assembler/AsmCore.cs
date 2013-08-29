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
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    public class XILInstr
    {
        public string Name { get; private set; }
        public object Operand { get; private set; }
        public object BackRef { get; set; }

        public XILInstr(string name, object operand)
        {
            Name = name;
            Operand = operand;
        }

        public XILInstr(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            string result = Name;
            if (Operand != null)
                result += " [" + Operand.ToString() + "]";
            return result;
        }

        public override bool Equals(object obj)
        {
            XILInstr xili = obj as XILInstr;
            if (xili == null)
                return false;

            return Name.Equals(xili.Name) &&
                object.Equals(Operand, xili.Operand);
        }

        public override int GetHashCode()
        {
            int hash = Name.GetHashCode();
            if (Operand != null)
                hash ^= Operand.GetHashCode();
            return hash;
        }

        public XIL3Instr Create3AC(InstructionDependency[] preds, int[] operandSlots, int[] resultSlots)
        {
            return new XIL3Instr(this, preds, operandSlots, resultSlots);
        }

        public XILSInstr CreateStk(InstructionDependency[] preds, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            return new XILSInstr(this, preds, operandTypes, resultTypes);
        }

        public XILSInstr CreateStk(InstructionDependency[] preds, int numOperands, params TypeDescriptor[] types)
        {
            Debug.Assert(Name != InstructionCodes.Dig || numOperands == ((int)Operand + 1));

            TypeDescriptor[] operandTypes = new TypeDescriptor[numOperands];
            TypeDescriptor[] resultTypes = new TypeDescriptor[types.Length - numOperands];
            Array.Copy(types, operandTypes, numOperands);
            Array.Copy(types, numOperands, resultTypes, 0, types.Length - numOperands);
            return new XILSInstr(this, preds, operandTypes, resultTypes);
        }

        public XILSInstr CreateStk(int numOperands, params TypeDescriptor[] types)
        {
            return CreateStk(new InstructionDependency[0], numOperands, types);
        }
    }

    public abstract class InstructionDependency
    {
        public int PredIndex { get; private set; }

        public InstructionDependency(int predIndex)
        {
            PredIndex = predIndex;
        }

        public abstract InstructionDependency Remap(int newPredIndex);
    }

    public class OrderDependency : InstructionDependency
    {
        public enum EKind
        {
            BeginAfter,
            CompleteAfter
        }

        public EKind Kind { get; private set; }

        public OrderDependency(int predIndex, EKind kind):
            base(predIndex)
        {
            Kind = kind;
        }

        public override bool Equals(object obj)
        {
            var other = obj as OrderDependency;
            if (other == null)
                return false;

            return Kind == other.Kind &&
                PredIndex == other.PredIndex;
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode() ^ PredIndex;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case EKind.BeginAfter: return "begin after " + PredIndex;
                case EKind.CompleteAfter: return "complete after " + PredIndex;
                default: throw new NotImplementedException();
            }
        }

        public override InstructionDependency Remap(int newPredIndex)
        {
            return new OrderDependency(newPredIndex, Kind);
        }
    }

    public class TimeDependency : InstructionDependency
    {
        public long MinDelay { get; private set; }
        public long MaxDelay { get; private set; }

        public TimeDependency(int predIndex, long minDelay, long maxDelay):
            base(predIndex)
        {
            MinDelay = minDelay;
            MaxDelay = maxDelay;
        }

        public override bool Equals(object obj)
        {
            var other = obj as TimeDependency;
            if (other == null)
                return false;

            return PredIndex == other.PredIndex &&
                MinDelay == other.MinDelay &&
                MaxDelay == other.MaxDelay;
        }

        public override int GetHashCode()
        {
            return PredIndex.GetHashCode() ^
                (MinDelay.GetHashCode() << 1) ^
                MaxDelay.GetHashCode();
        }

        public override string ToString()
        {
            return "from " + PredIndex + " " + MinDelay + "-" + MaxDelay + " c-steps";                 
        }

        public override InstructionDependency Remap(int newPredIndex)
        {
            return new TimeDependency(newPredIndex, MinDelay, MaxDelay);
        }
    }

    public interface IXILxInstr : IInstruction
    {
        string Name { get; }
        XILInstr Command { get; }
        object StaticOperand { get; }
        InstructionDependency[] Preds { get; }
    }

    public class XIL3Instr: IXILxInstr
    {
        public XILInstr Command { get; private set; }
        public int Index { get; set; }
        public InstructionDependency[] Preds { get; private set; }
        public int[] OperandSlots { get; private set; }
        public int[] ResultSlots { get; private set; }
        public ILIndexRef CILRef { get; internal set; }

        public object StaticOperand
        {
            get { return Command.Operand; }
        }

        public string Name
        {
            get { return Command.Name; }
        }

        public XIL3Instr(XILInstr command,
            InstructionDependency[] preds, int[] operandSlots, int[] resultSlots)
        {
            Command = command;
            Preds = preds;
            OperandSlots = operandSlots;
            ResultSlots = resultSlots;
            Index = -1;
            CILRef = null;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Command.ToString());
            sb.Append(" (");
            sb.Append(string.Join(", ", OperandSlots));
            sb.Append(") => (");
            sb.Append(string.Join(", ", ResultSlots));
            sb.Append(")");
            if (Preds.Length > 0)
            {
                sb.Append(" ");
                sb.Append(string.Join<InstructionDependency>(", ", Preds));
            }
            if (CILRef != null)
            {
                sb.Append(" {");
                sb.Append(CILRef.ToString());
                sb.Append("}");
            }
            return sb.ToString();
        }
    }

    public class XILSInstr: IXILxInstr
    {
        public XILInstr Command { get; private set; }
        public int Index { get; set; }
        public InstructionDependency[] Preds { get; private set; }
        public TypeDescriptor[] OperandTypes { get; private set; }
        public TypeDescriptor[] ResultTypes { get; private set; }
        public ILIndexRef CILRef { get; internal set; }

        public object StaticOperand
        {
            get { return Command.Operand; }
        }

        public string Name
        {
            get { return Command.Name; }
        }

        public XILSInstr(XILInstr command, InstructionDependency[] preds,
            TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            Contract.Requires(command != null && preds != null &&
                operandTypes != null && operandTypes.All(t => t != null) &&
                resultTypes != null && resultTypes.All(t => t != null));

            Command = command;
            Preds = preds;
            OperandTypes = operandTypes;
            ResultTypes = resultTypes;

            CILRef = null;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Command.ToString());
            sb.Append("<");
            sb.Append(string.Join<TypeDescriptor>(", ", OperandTypes));
            sb.Append(" => ");
            sb.Append(string.Join<TypeDescriptor>(", ", ResultTypes));
            sb.Append(">");
            if (Preds.Length > 0)
            {
                sb.Append(" ");
                sb.Append(string.Join<InstructionDependency>(", ", Preds));
            }
            if (CILRef != null)
            {
                sb.Append(" {");
                sb.Append(CILRef.ToString());
                sb.Append("}");
            }
            return sb.ToString();
        }

        private class CompareByContent : IEqualityComparer<XILSInstr>
        {
            public bool Equals(XILSInstr x, XILSInstr y)
            {
                return x.Command.Equals(y.Command) &&
                    x.OperandTypes.SequenceEqual(y.OperandTypes) &&
                    x.ResultTypes.SequenceEqual(y.ResultTypes);
            }

            public int GetHashCode(XILSInstr obj)
            {
                int hash = obj.Command.GetHashCode();
                hash ^= obj.OperandTypes.GetSequenceHashCode();
                hash ^= obj.ResultTypes.GetSequenceHashCode();
                return hash;
            }
        }

        private class CompareByIndex : IEqualityComparer<XILSInstr>
        {
            public bool Equals(XILSInstr x, XILSInstr y)
            {
                return x.Index == y.Index;
            }

            public int GetHashCode(XILSInstr obj)
            {
                return obj.Index;
            }
        }

        public static readonly IEqualityComparer<XILSInstr> ContentComparer = new CompareByContent();
        public static readonly IEqualityComparer<XILSInstr> IndexComparer = new CompareByIndex();
    }

    public class BranchLabel
    {
        private int _instructionIndex;
        public int InstructionIndex 
        {
            get { return _instructionIndex; }
            set
            {
                Contract.Requires(value >= 0);
                _instructionIndex = value;
            }
        }

        public int CStep { get; internal set; }

        public BranchLabel()
        {
            _instructionIndex = -1;
            CStep = -1;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(InstructionIndex);
            if (CStep >= 0)
            {
                sb.AppendFormat(" cstep:{0}", CStep);
            }
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is BranchLabel)
            {
                BranchLabel other = (BranchLabel)obj;
                return InstructionIndex == other.InstructionIndex;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return InstructionIndex.GetHashCode();
        }
    }

    public class XIL3Function : ICallable
    {
        public XIL3Function(string name, ArgumentDescriptor[] args, Variable[] locals, 
            XIL3Instr[] instrs, TypeDescriptor[] slotTypes)
        {
            Contract.Requires(args != null && args.All(a => a != null) &&
                locals != null && locals.All(l => l != null) &&
                instrs != null && instrs.All(i => i != null) &&
                slotTypes != null && slotTypes.All(t => t != null));

            Name = name;
            Arguments = args;
            Locals = locals;
            Instructions = instrs;
            SlotTypes = slotTypes;
        }

        public ArgumentDescriptor[] Arguments { get; private set; }
        public Variable[] Locals { get; private set; }
        public XIL3Instr[] Instructions { get; private set; }
        public TypeDescriptor[] SlotTypes { get; private set; }

        public int NumSlots
        {
            get { return SlotTypes.Length; }
        }

        #region ICallable Member

        public string Name { get; private set; }

        #endregion

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("xil3:" + Name + "(");
            sb.Append(string.Join<ArgumentDescriptor>(", ", Arguments));
            sb.AppendLine(")");
            sb.AppendLine("{");
            foreach (Variable local in Locals)
            {
                sb.Append("  " + local.Type + " " + local.Name);
            }
            sb.AppendLine();
            sb.AppendLine(NumSlots + " slots");
            for (int i = 0; i < NumSlots; i++)
            {
                sb.AppendLine(i + ": " + SlotTypes[i].ToString());
            }
            sb.AppendLine();
            string fmt = "D" + ((Instructions.Length - 1).ToString()).Length.ToString();
            for (int i = 0; i < Instructions.Length; i++)
            {
                sb.Append(i.ToString(fmt) + ": ");
                var instr = Instructions[i];
                sb.AppendLine(instr.ToString());
            }
            sb.AppendLine("}");
            sb.AppendLine("Assignment list:");
            sb.AppendLine("{");
            string[] exprs = new string[NumSlots];
            for (int i = 0; i < Instructions.Length; i++)
            {
                var instr = Instructions[i];

                switch (instr.Name)
                {
                    case InstructionCodes.LdConst:
                        exprs[instr.ResultSlots[0]] = instr.StaticOperand.ToString();
                        continue;

                    case InstructionCodes.Convert:
                        exprs[instr.ResultSlots[0]] = exprs[instr.OperandSlots[0]];
                        continue;

                    case InstructionCodes.Add:
                        exprs[instr.ResultSlots[0]] = "(" + exprs[instr.OperandSlots[0]] + ") + (" + exprs[instr.OperandSlots[1]] + ")";
                        continue;

                    case InstructionCodes.Sub:
                        exprs[instr.ResultSlots[0]] = "(" + exprs[instr.OperandSlots[0]] + ") - (" + exprs[instr.OperandSlots[1]] + ")";
                        continue;

                    case InstructionCodes.Mul:
                        exprs[instr.ResultSlots[0]] = "(" + exprs[instr.OperandSlots[0]] + ") * (" + exprs[instr.OperandSlots[1]] + ")";
                        continue;

                    case InstructionCodes.Div:
                        exprs[instr.ResultSlots[0]] = "(" + exprs[instr.OperandSlots[0]] + ") / (" + exprs[instr.OperandSlots[1]] + ")";
                        continue;
                }

                string expr = instr.Name;
                if (instr.StaticOperand != null)
                {
                    expr += "<";
                    expr += instr.StaticOperand.ToString();
                    expr += ">";
                }
                if (instr.OperandSlots.Length > 0)
                {
                    expr += "(";
                    string args = string.Join(", ", instr.OperandSlots.Select(os => exprs[os]));
                    expr += args;
                    expr += ")";
                }
                if (instr.ResultSlots.Length == 0)
                {
                    sb.AppendLine("  " + expr);
                }
                else if (instr.ResultSlots.Length == 1)
                {
                    exprs[instr.ResultSlots[0]] = expr;
                }
                else
                {
                    foreach (int rs in instr.ResultSlots)
                        exprs[rs] = expr + "[" + rs + "]";
                }
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        public TypeDescriptor[] GetSlotTypes(int[] slots)
        {
            return slots.Select(i => SlotTypes[i]).ToArray();
        }

        public int[] GetBasicBlockBoundaries()
        {
            HashSet<int> bbs = new HashSet<int>();
            bbs.Add(0);
            for (int i = 0; i < Instructions.Length; i++)
            {
                XIL3Instr xil3i = Instructions[i];
                switch (xil3i.Name)
                {
                    case InstructionCodes.BranchIfFalse:
                    case InstructionCodes.BranchIfTrue:
                    case InstructionCodes.Goto:
                        {
                            BranchLabel target = (BranchLabel)xil3i.StaticOperand;
                            bbs.Add(target.InstructionIndex);
                            bbs.Add(i + 1);
                        }
                        break;

                    default:
                        break;
                }
            }
            return bbs.OrderBy(i => i).ToArray();
        }

        public void SanityCheck()
        {
            foreach (var xil3i in Instructions)
            {
                switch (xil3i.Command.Name)
                {
                    case InstructionCodes.StoreVar:
                        {
                            var local = xil3i.StaticOperand as Variable;
                            if (local != null)
                            {
                                Debug.Assert(Locals.Contains(local));
                            }
                        }
                        break;
                }
            }
        }
    }

    public class XILSFunction : ICallable
    {
        public XILSFunction(string name, ArgumentDescriptor[] args, Variable[] locals, XILSInstr[] instrs)
        {
            Name = name;
            Arguments = args;
            Locals = locals;
            Instructions = instrs;
        }

        public ArgumentDescriptor[] Arguments { get; private set; }
        public Variable[] Locals { get; private set; }
        public XILSInstr[] Instructions { get; private set; }

        #region ICallable Member

        public string Name { get; private set; }

        #endregion

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("xils:" + Name + "(");
            sb.Append(string.Join<ArgumentDescriptor>(", ", Arguments));
            sb.AppendLine(")");
            sb.AppendLine("{");
            foreach (Variable local in Locals)
            {
                sb.Append("  " + local.Type + " " + local.Name);
            }
            sb.AppendLine();
            string fmt = "D" + ((Instructions.Length - 1).ToString()).Length.ToString();
            for (int i = 0; i < Instructions.Length; i++)
            {
                sb.Append(i.ToString(fmt) + ": ");
                XILSInstr instr = Instructions[i];
                sb.AppendLine(instr.ToString());
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        public void SanityCheck()
        {
            foreach (var xilsi in Instructions)
            {
                switch (xilsi.Command.Name)
                {
                    case InstructionCodes.StoreVar:
                        {
                            var local = xilsi.StaticOperand as Variable;
                            if (local != null)
                            {
                                Debug.Assert(Locals.Contains(local));
                            }
                        }
                        break;
                }
            }
        }
    }

    public class XILAssembly
    {
        public Component Top { get; set; }
        public MemoryMapper MemoryLayout { get; set; }
        public TypeDescriptor[] OperandTypes { get; set; }
        public XIL3Instr[] Instructions { get; set; }

        public TypeDescriptor[] GetOperandTypes(XIL3Instr i)
        {
            Contract.Requires(i.OperandSlots != null);

            return (from int slot in i.OperandSlots
                    select OperandTypes[slot]).ToArray();
        }

        public TypeDescriptor[] GetResultTypes(XIL3Instr i)
        {
            Contract.Requires(i.OperandSlots != null);

            return (from int slot in i.ResultSlots
                    select OperandTypes[slot]).ToArray();
        }
    }

    public class MemAccessInfo
    {
        public enum EMemAccessKind
        {
            Read,
            Write
        }

        public enum EDependencyKind
        {
            NoDependency,
            ReadAfterWrite,
            WriteAfterWrite,
            WriteAfterRead
        }

        public EMemAccessKind Kind { get; private set; }
        public ulong MinAddress { get; private set; }
        public ulong MaxAddress { get; private set; }

        public MemAccessInfo(EMemAccessKind kind, ulong minAddress, ulong maxAddress)
        {
            Kind = kind;
            MinAddress = MinAddress;
            MaxAddress = MaxAddress;
        }

        public bool Overlaps(MemAccessInfo mac)
        {
            return MaxAddress >= mac.MinAddress && mac.MaxAddress >= MinAddress;
        }

        public static EDependencyKind AanalyzeDependency(MemAccessInfo first, MemAccessInfo second)
        {
            bool overlap = first.Overlaps(second);
            if (!overlap)
                return EDependencyKind.NoDependency;
            if (first.Kind == EMemAccessKind.Read && second.Kind == EMemAccessKind.Read)
                return EDependencyKind.NoDependency;
            else if (first.Kind == EMemAccessKind.Write && second.Kind == EMemAccessKind.Write)
                return EDependencyKind.WriteAfterWrite;
            else if (first.Kind == EMemAccessKind.Write)
                return EDependencyKind.ReadAfterWrite;
            else
                return EDependencyKind.WriteAfterRead;
        }
    }

    class VariableResource : IInstructionResource
    {
        public Variable Var { get; private set; }

        public VariableResource(Variable v)
        {
            Var = v;
        }

        public bool ConflictsWith(IInstructionResource other)
        {
            if (other is VariableResource)
            {
                VariableResource vr = (VariableResource)other;
                return Var.Equals(vr.Var);
            }
            else
                return false;
        }
    }

    public class XILInstructionInfo: IInstructionInfo<XILInstr>
    {
        public bool IsMemAccess(XILInstr i, out MemAccessInfo mac)
        {
            switch (i.Name)
            {
                case InstructionCodes.RdMemFix:
                    {
                        ulong addr = (ulong)i.Operand;
                        mac = new MemAccessInfo(MemAccessInfo.EMemAccessKind.Read,
                            addr, addr);
                        return true;
                    }

                case InstructionCodes.WrMemFix:
                    {
                        ulong addr = (ulong)i.Operand;
                        mac = new MemAccessInfo(MemAccessInfo.EMemAccessKind.Write,
                            addr, addr);
                        return true;
                    }

                default:
                    mac = null;
                    return false;
            }
        }

        public EInstructionClass Classify(XILInstr i)
        {
            switch (i.Name)
            {
                case InstructionCodes.BranchIfFalse:
                case InstructionCodes.BranchIfTrue:
                case InstructionCodes.Goto:
                case InstructionCodes.Return:
                    return EInstructionClass.Branch;

                case InstructionCodes.LoadVar:
                case InstructionCodes.StoreVar:
                    return (i.Operand is Variable) ?
                        EInstructionClass.LocalVariableAccess :
                        EInstructionClass.Other;

                default:
                    return EInstructionClass.Other;
            }
        }

        public EBranchBehavior IsBranch(XILInstr i, out IEnumerable<int> targets)
        {
            switch (i.Name)
            {
                case InstructionCodes.BranchIfFalse:
                case InstructionCodes.BranchIfTrue:
                    {
                        BranchLabel label = (BranchLabel)i.Operand;
                        targets = new int[] { label.InstructionIndex };
                        return EBranchBehavior.CBranch;
                    }

                case InstructionCodes.Goto:
                    {
                        BranchLabel label = (BranchLabel)i.Operand;
                        targets = new int[] { label.InstructionIndex };
                        return EBranchBehavior.UBranch;
                    }

                case InstructionCodes.Return:
                case InstructionCodes.ExitMarshal:
                    targets = new int[0];
                    return EBranchBehavior.Return;

                default:
                    targets = new int[0];
                    return EBranchBehavior.NoBranch;
            }
        }

        public ELocalVariableAccess IsLocalVariableAccess(XILInstr i, out int localIndex)
        {
            localIndex = -1;
            var v = i.Operand as Variable;
            if (v == null)
                return ELocalVariableAccess.NoAccess;

            switch (i.Name)
            {
                case InstructionCodes.LoadVar:
                    localIndex = v.LocalIndex;
                    return ELocalVariableAccess.ReadVariable;

                case InstructionCodes.StoreVar:
                    localIndex = v.LocalIndex;
                    return ELocalVariableAccess.WriteVariable;

                default:
                    return ELocalVariableAccess.NoAccess;
            }
        }

        public EInstructionResourceAccess UsesResource(XILInstr i, out IInstructionResource resource)
        {
            switch (i.Name)
            {
                case InstructionCodes.LoadVar:
                    resource = new VariableResource((Variable)i.Operand);
                    return EInstructionResourceAccess.Reading;

                case InstructionCodes.StoreVar:
                    resource = new VariableResource((Variable)i.Operand);
                    return EInstructionResourceAccess.Writing;

                case InstructionCodes.RdMemFix:
                    throw new NotImplementedException();

                case InstructionCodes.WrMemFix:
                    throw new NotImplementedException();

                default:
                    resource = null;
                    return EInstructionResourceAccess.NoResource;
            }
        }
    }

    public class XILSInstructionInfo : IInstructionInfo<XILSInstr>
    {
        private XILInstructionInfo _xilii = new XILInstructionInfo();

        public EInstructionClass Classify(XILSInstr i)
        {
            return _xilii.Classify(i.Command);
        }

        public EBranchBehavior IsBranch(XILSInstr i, out IEnumerable<int> targets)
        {
            return _xilii.IsBranch(i.Command, out targets);
        }

        public ELocalVariableAccess IsLocalVariableAccess(XILSInstr i, out int localIndex)
        {
            return _xilii.IsLocalVariableAccess(i.Command, out localIndex);
        }

        public EInstructionResourceAccess UsesResource(XILSInstr i, out IInstructionResource resource)
        {
            return _xilii.UsesResource(i.Command, out resource);
        }

        public static IEnumerable<int> GetBasicBlockBoundaries(IEnumerable<XILSInstr> instrs)
        {
            HashSet<int> bbs = new HashSet<int>();
            bbs.Add(0);
            foreach (var xilsi in instrs)
            {
                switch (xilsi.Name)
                {
                    case InstructionCodes.BranchIfFalse:
                    case InstructionCodes.BranchIfTrue:
                    case InstructionCodes.Goto:
                        {
                            BranchLabel target = (BranchLabel)xilsi.StaticOperand;
                            bbs.Add(target.InstructionIndex);
                            bbs.Add(xilsi.Index + 1);
                        }
                        break;

                    default:
                        break;
                }
            }
            return bbs.OrderBy(i => i);
        }

        public static IEnumerable<IEnumerable<XILSInstr>> GetBasicBlocks(IEnumerable<XILSInstr> instrs)
        {
            var bbs = GetBasicBlockBoundaries(instrs);
            int nbbs = bbs.Count();
            return bbs.Take(nbbs - 1)
                .Zip(bbs.Skip(1), (i, j) => Tuple.Create(i, j))
                .Select(tup => instrs.Skip(tup.Item1).Take(tup.Item2 - tup.Item1));
        }
    }

    public class XIL3InstructionInfo : IInstructionInfo<XIL3Instr>
    {
        private XILInstructionInfo _xilii = new XILInstructionInfo();

        public EInstructionClass Classify(XIL3Instr i)
        {
            return _xilii.Classify(i.Command);
        }

        public EBranchBehavior IsBranch(XIL3Instr i, out IEnumerable<int> targets)
        {
            return _xilii.IsBranch(i.Command, out targets);
        }

        public ELocalVariableAccess IsLocalVariableAccess(XIL3Instr i, out int localIndex)
        {
            return _xilii.IsLocalVariableAccess(i.Command, out localIndex);
        }

        public EInstructionResourceAccess UsesResource(XIL3Instr i, out IInstructionResource resource)
        {
            return _xilii.UsesResource(i.Command, out resource);
        }
    }
}

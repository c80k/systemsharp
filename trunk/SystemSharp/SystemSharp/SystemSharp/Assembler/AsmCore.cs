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
    /// <summary>
    /// Models a XIL instruction
    /// </summary>
    public class XILInstr
    {
        /// <summary>
        /// Opcode
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Optional static operand
        /// </summary>
        public object Operand { get; private set; }

        /// <summary>
        /// Optional back-reference object which helps tracking code transformations
        /// </summary>
        public object BackRef { get; set; }

        /// <summary>
        /// Constructs a new instance with an optional static operand
        /// </summary>
        /// <param name="name">opcode</param>
        /// <param name="operand">optional static operand</param>
        public XILInstr(string name, object operand)
        {
            Name = name;
            Operand = operand;
        }

        /// <summary>
        /// Constructs a new instance without static operand
        /// </summary>
        /// <param name="name">opcode</param>
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

        /// <summary>
        /// Creates a XIL-3 representation of this instruction
        /// </summary>
        /// <param name="preds">instruction dependencies</param>
        /// <param name="operandSlots">operand slots</param>
        /// <param name="resultSlots">result slots</param>
        /// <returns>the XIL-3 representation</returns>
        public XIL3Instr Create3AC(InstructionDependency[] preds, int[] operandSlots, int[] resultSlots)
        {
            return new XIL3Instr(this, preds, operandSlots, resultSlots);
        }

        /// <summary>
        /// Creates a XIL-S representation of this instruction
        /// </summary>
        /// <param name="preds">instruction dependencies</param>
        /// <param name="operandTypes">operand types</param>
        /// <param name="resultTypes">result types</param>
        /// <returns>the XIL-S representation</returns>
        public XILSInstr CreateStk(InstructionDependency[] preds, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            return new XILSInstr(this, preds, operandTypes, resultTypes);
        }

        /// <summary>
        /// Creates a XIL-S representation of this instruction
        /// </summary>
        /// <param name="preds">instruction dependencies</param>
        /// <param name="numOperands">number of operands</param>
        /// <param name="types">type vector, the first numOperands describing the operand types, the rest describing the result types</param>
        /// <returns>the XIL-S representation</returns>
        public XILSInstr CreateStk(InstructionDependency[] preds, int numOperands, params TypeDescriptor[] types)
        {
            Debug.Assert(Name != InstructionCodes.Dig || numOperands == ((int)Operand + 1));

            TypeDescriptor[] operandTypes = new TypeDescriptor[numOperands];
            TypeDescriptor[] resultTypes = new TypeDescriptor[types.Length - numOperands];
            Array.Copy(types, operandTypes, numOperands);
            Array.Copy(types, numOperands, resultTypes, 0, types.Length - numOperands);
            return new XILSInstr(this, preds, operandTypes, resultTypes);
        }

        /// <summary>
        /// Creates a XIL-S representation of this instruction without any dependency
        /// </summary>
        /// <param name="numOperands">number of operands</param>
        /// <param name="types">type vector, the first numOperands describing the operand types, the rest describing the result types</param>
        /// <returns>the XIL-S representation</returns>
        public XILSInstr CreateStk(int numOperands, params TypeDescriptor[] types)
        {
            return CreateStk(new InstructionDependency[0], numOperands, types);
        }
    }

    /// <summary>
    /// Abstract base class for control and data dependencies.
    /// </summary>
    /// <remarks>
    /// A dependency indicates that the execution of some instruction somehow depends on the completion of some other
    /// instruction, thus enforcing a certain execution order and limiting the possible parallelism of instructions.
    /// </remarks>
    public abstract class InstructionDependency
    {
        /// <summary>
        /// The index of the instruction we're depending on
        /// </summary>
        public int PredIndex { get; private set; }

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="predIndex">the index of the instruction we're depending on</param>
        public InstructionDependency(int predIndex)
        {
            PredIndex = predIndex;
        }

        /// <summary>
        /// Creates a semantically equivalent dependency for a different instruction index
        /// </summary>
        /// <param name="newPredIndex">new instruction index</param>
        /// <returns>a semantically equivalent dependency for given instruction index</returns>
        public abstract InstructionDependency Remap(int newPredIndex);
    }

    /// <summary>
    /// An order dependency specifies that an instruction must either not start before another instruction has completed
    /// or may not complete before that other instruction.
    /// </summary>
    public class OrderDependency : InstructionDependency
    {
        /// <summary>
        /// Kind of dependency
        /// </summary>
        public enum EKind
        {
            /// <summary>
            /// The dependent instruction must not begin before other instruction has completed.
            /// </summary>
            BeginAfter,

            /// <summary>
            /// The dependent instruction must not complete before other instruction has completed.
            /// </summary>
            CompleteAfter
        }

        /// <summary>
        /// The kind of dependency
        /// </summary>
        public EKind Kind { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="predIndex">index of other instruction we're depending on</param>
        /// <param name="kind">kind of dependency</param>
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

    /// <summary>
    /// A time dependency indicates that an instruction must begin within a certain time interval after another instruction
    /// began executing.
    /// </summary>
    public class TimeDependency : InstructionDependency
    {
        /// <summary>
        /// Minimum time to wait until dependent instruction may begin executing.
        /// </summary>
        public long MinDelay { get; private set; }

        /// <summary>
        /// Maximum time to wait until dependent instruction must begin executing.
        /// </summary>
        public long MaxDelay { get; private set; }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="predIndex">index of other instruction we're depending on</param>
        /// <param name="minDelay">minimum time to wait until dependent instruction may begin executing</param>
        /// <param name="maxDelay">maximum time to wait until dependent instruction must begin executing.</param>
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

    /// <summary>
    /// Common interface for both XIL-S and XIL-3 instructions
    /// </summary>
    public interface IXILxInstr : IInstruction
    {
        /// <summary>
        /// Opcode
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Underlying XIL instruction
        /// </summary>
        XILInstr Command { get; }

        /// <summary>
        /// Optional static operand
        /// </summary>
        object StaticOperand { get; }

        /// <summary>
        /// Instruction dependencies
        /// </summary>
        InstructionDependency[] Preds { get; }
    }

    /// <summary>
    /// A XIL-3 instruction
    /// </summary>
    /// <remarks>
    /// XIL-3 instruction presume a three-address-code execution model, whereby the number three must be not taken too literally.
    /// XIL-3 instructions are allowed to consume more or less than 2 operands, and they are also allowed to produce more or less than
    /// 1 result. But they all operate on a set of theoretically unlimited registers, which are identified by numbers. These are called
    /// slots.
    /// </remarks>
    public class XIL3Instr: IXILxInstr
    {
        /// <summary>
        /// The underlying XIL instruction
        /// </summary>
        public XILInstr Command { get; private set; }

        /// <summary>
        /// Instruction index
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Instruction dependencies
        /// </summary>
        public InstructionDependency[] Preds { get; private set; }

        /// <summary>
        /// Slots where to obtain the operands from
        /// </summary>
        public int[] OperandSlots { get; private set; }

        /// <summary>
        /// Slots where to put the results
        /// </summary>
        public int[] ResultSlots { get; private set; }

        /// <summary>
        /// Optional back-reference to original CIL code for debugging purpose
        /// </summary>
        public ILIndexRef CILRef { get; internal set; }

        public object StaticOperand
        {
            get { return Command.Operand; }
        }

        public string Name
        {
            get { return Command.Name; }
        }

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="command">underlying XIL instruction</param>
        /// <param name="preds">instruction dependencies</param>
        /// <param name="operandSlots">operand slots</param>
        /// <param name="resultSlots">result slots</param>
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

    /// <summary>
    /// A XIL-S instruction
    /// </summary>
    /// <remarks>
    /// XIL-S instructions presume a stack machine execution model - very much like Java bytecode and CIL. A XIL-S instruction
    /// pops 0 or more operands from stack stack and finally pushes 0 or more results onto the stack. Stack elements are not restricted
    /// to have any particular size (as opposed to Java VM, where the stack is strictly 32 bit words). In fact, a stack element can represent
    /// any data item, from a simple bool to a 10^6 bit wide fixed point number. Therefore it is of utmost importance that any XIL-S
    /// instruction clearly indicates the types of operands it expects on the stack and the types of operands it will put on the stack.
    /// </remarks>
    public class XILSInstr: IXILxInstr
    {
        /// <summary>
        /// Underlying XIL instruction
        /// </summary>
        public XILInstr Command { get; private set; }

        /// <summary>
        /// Instruction index
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Instruction dependencies
        /// </summary>
        public InstructionDependency[] Preds { get; private set; }

        /// <summary>
        /// Types of operands expected on the stack, from bottom (index 0) to top
        /// </summary>
        public TypeDescriptor[] OperandTypes { get; private set; }

        /// <summary>
        /// Result types to be pushed on the stack, from bottom (index 0) to top
        /// </summary>
        public TypeDescriptor[] ResultTypes { get; private set; }

        /// <summary>
        /// Optional back-reference to CIL instruction for debugging purpose
        /// </summary>
        public ILIndexRef CILRef { get; internal set; }

        public object StaticOperand
        {
            get { return Command.Operand; }
        }

        public string Name
        {
            get { return Command.Name; }
        }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="command">XIL instruction</param>
        /// <param name="preds">instruction dependencies</param>
        /// <param name="operandTypes">operand types expected on the stack</param>
        /// <param name="resultTypes">result types to appear on the stack</param>
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

        /// <summary>
        /// A comparer which defines two instructions to be equal iff their opcodes, static operands, operand and result types are equal
        /// </summary>
        public static readonly IEqualityComparer<XILSInstr> ContentComparer = new CompareByContent();

        /// <summary>
        /// A comparer which defines two instructions to be equal iff they have the same instruction index
        /// </summary>
        public static readonly IEqualityComparer<XILSInstr> IndexComparer = new CompareByIndex();
    }

    /// <summary>
    /// A branch label
    /// </summary>
    /// <remarks>
    /// Branch labels are used to identify the targets of XIL, XIL-S and XIL-3 branch instructions. A branch label is assigned
    /// as static operand.
    /// </remarks>
    public class BranchLabel
    {
        private int _instructionIndex;

        /// <summary>
        /// Index of target instruction, -1 if undefined
        /// </summary>
        public int InstructionIndex 
        {
            get { return _instructionIndex; }
            set
            {
                Contract.Requires(value >= 0);
                _instructionIndex = value;
            }
        }

        /// <summary>
        /// corresponding c-step after scheduling, -1 if undefined
        /// </summary>
        public int CStep { get; internal set; }

        /// <summary>
        /// Constructs a new instance with undefined target instruction
        /// </summary>
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
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return InstructionIndex.GetHashCode();
        }
    }

    /// <summary>
    /// A XIL-3 function
    /// </summary>
    public class XIL3Function : ICallable
    {
        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="name">function name</param>
        /// <param name="args">function arguments</param>
        /// <param name="locals">local variables</param>
        /// <param name="instrs">instruction sequence</param>
        /// <param name="slotTypes">datatypes of slots</param>
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

        /// <summary>
        /// Function arguments
        /// </summary>
        public ArgumentDescriptor[] Arguments { get; private set; }

        /// <summary>
        /// Local variables
        /// </summary>
        public Variable[] Locals { get; private set; }

        /// <summary>
        /// Instruction sequence
        /// </summary>
        public XIL3Instr[] Instructions { get; private set; }

        /// <summary>
        /// Datatypes of slots
        /// </summary>
        public TypeDescriptor[] SlotTypes { get; private set; }

        /// <summary>
        /// Number of slots
        /// </summary>
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

        /// <summary>
        /// Returns the datatypes of a specified subset of slots
        /// </summary>
        /// <param name="slots">desired slot subset</param>
        /// <returns>the datatypes for the queried subset</returns>
        public TypeDescriptor[] GetSlotTypes(int[] slots)
        {
            return slots.Select(i => SlotTypes[i]).ToArray();
        }

        /// <summary>
        /// Segments the instruction sequence into basic blocks and returns their boundaries.
        /// </summary>
        /// <returns>Instruction indices of basic block boundaries</returns>
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

        /// <summary>
        /// Checks the data structure for consistency.
        /// </summary>
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

    /// <summary>
    /// A XIL-S function
    /// </summary>
    public class XILSFunction : ICallable
    {
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="name">function name</param>
        /// <param name="args">function arguments</param>
        /// <param name="locals">local variables</param>
        /// <param name="instrs">instruction sequence</param>
        public XILSFunction(string name, ArgumentDescriptor[] args, Variable[] locals, XILSInstr[] instrs)
        {
            Name = name;
            Arguments = args;
            Locals = locals;
            Instructions = instrs;
        }

        /// <summary>
        /// Function arguments
        /// </summary>
        public ArgumentDescriptor[] Arguments { get; private set; }

        /// <summary>
        /// Local variables
        /// </summary>
        public Variable[] Locals { get; private set; }

        /// <summary>
        /// Instruction sequence
        /// </summary>
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

        /// <summary>
        /// Checks the data structure for consistency.
        /// </summary>
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

    /// <summary>
    /// Models a memory access
    /// </summary>
    public class MemAccessInfo
    {
        /// <summary>
        /// Kind of memory access
        /// </summary>
        public enum EMemAccessKind
        {
            Read,
            Write
        }

        /// <summary>
        /// Kind of data dependency
        /// </summary>
        public enum EDependencyKind
        {
            NoDependency,
            ReadAfterWrite,
            WriteAfterWrite,
            WriteAfterRead
        }

        /// <summary>
        /// Kind of memory access
        /// </summary>
        public EMemAccessKind Kind { get; private set; }

        /// <summary>
        /// Lower boundary for memory address
        /// </summary>
        public ulong MinAddress { get; private set; }

        /// <summary>
        /// Upper boundary for memory address
        /// </summary>
        public ulong MaxAddress { get; private set; }

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="kind">kind of access</param>
        /// <param name="minAddress">lower boundary for memory address</param>
        /// <param name="maxAddress">upper boundary for memory address</param>
        public MemAccessInfo(EMemAccessKind kind, ulong minAddress, ulong maxAddress)
        {
            Kind = kind;
            MinAddress = MinAddress;
            MaxAddress = MaxAddress;
        }

        /// <summary>
        /// Checks whether this access might refer to the same address as another access
        /// </summary>
        /// <param name="mac">another memory access</param>
        /// <returns>whether both accesses might overlap (i.e. refer to the same memory location)</returns>
        public bool Overlaps(MemAccessInfo mac)
        {
            return MaxAddress >= mac.MinAddress && mac.MaxAddress >= MinAddress;
        }

        /// <summary>
        /// Analyzes the possible data dependency which might arise from two memory accesses.
        /// </summary>
        /// <param name="first">some memory access</param>
        /// <param name="second">some other memory access</param>
        /// <returns>kind of data dependency</returns>
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

    /// <summary>
    /// Models a local variable as instruction resource
    /// </summary>
    class VariableResource : IInstructionResource
    {
        /// <summary>
        /// The local variable
        /// </summary>
        public Variable Var { get; private set; }

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="v">local variable</param>
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
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Implements the instruction information service for XIL instructions
    /// </summary>
    public class XILInstructionInfo: IInstructionInfo<XILInstr>
    {
        /// <summary>
        /// Checks whether a given XIL instruction is a memory access
        /// </summary>
        /// <param name="i">XIL instruction</param>
        /// <param name="mac">memory access information (null if instruction is not a memory access)</param>
        /// <returns>whether given XIL instruction is a memory access</returns>
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

    /// <summary>
    /// Implements the instruction information service for XIL-S instructions
    /// </summary>
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

        /// <summary>
        /// Segments a sequence of XIL-S instructions into basic blocks and returns their boundaries.
        /// </summary>
        /// <param name="instrs">sequence of XIL-S instructions</param>
        /// <returns>instruction indices of basic block boundaries</returns>
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

        /// <summary>
        /// Segments a sequence of XIL-S instructions into basic blocks and returns an enumeration of basic block instruction lists
        /// </summary>
        /// <param name="instrs">sequence of XIL-S instruction</param>
        /// <returns>an enumeration of enumerations with each sequence enumerating the instructions which belong to a basic block</returns>
        public static IEnumerable<IEnumerable<XILSInstr>> GetBasicBlocks(IEnumerable<XILSInstr> instrs)
        {
            var bbs = GetBasicBlockBoundaries(instrs);
            int nbbs = bbs.Count();
            return bbs.Take(nbbs - 1)
                .Zip(bbs.Skip(1), (i, j) => Tuple.Create(i, j))
                .Select(tup => instrs.Skip(tup.Item1).Take(tup.Item2 - tup.Item1));
        }
    }

    /// <summary>
    /// Implements the instruction information service for XIL-3 instructions
    /// </summary>
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

/**
 * Copyright 2012 Christian Köllner
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
using SystemSharp.Analysis.M2M;
using SystemSharp.Common;
using SystemSharp.Meta;

namespace SystemSharp.Assembler.Rewriters
{
    class TypeReplacerImpl: XILSRewriter
    {
        public TypeReplacerImpl(IList<XILSInstr> instructions) :
            base(instructions)
        {
        }

        public TypeDescriptor GenuineType { get; set; }
        public TypeDescriptor ReplacementType { get; set; }

        protected override void ProcessInstruction(XILSInstr i)
        {
            var otypes = i.OperandTypes.Select(t => t.Equals(GenuineType) ? ReplacementType : t).ToArray();
            var rtypes = i.ResultTypes.Select(t => t.Equals(GenuineType) ? ReplacementType : t).ToArray();
            XILSInstr inew;
            if (i.Name == InstructionCodes.LdConst &&
                i.StaticOperand != null &&
                i.StaticOperand.GetType().Equals(GenuineType.CILType))
            {
                var cnew = DefaultInstructionSet.Instance.LdConst(TypeConversions.ConvertValue(i.StaticOperand, ReplacementType.CILType));
                inew = cnew.CreateStk(i.Preds, otypes, rtypes);
            }
            else
            {
                inew = i.Command.CreateStk(i.Preds, otypes, rtypes);
            }
            base.ProcessInstruction(inew);
        }
    }

    public class TypeReplacer : IXILSRewriter
    {
        public TypeDescriptor GenuineType { get; set; }
        public TypeDescriptor ReplacementType { get; set; }

        public override string ToString()
        {
            return "TypeReplacer";
        }

        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            var impl = new TypeReplacerImpl(instrs)
            {
                GenuineType = GenuineType,
                ReplacementType = ReplacementType
            };
            impl.Rewrite();
            return impl.OutInstructions;
        }

        public static IXILSRewriter ReplaceDoubleByFloat()
        {
            return new TypeReplacer()
            {
                GenuineType = typeof(double),
                ReplacementType = typeof(float)
            };
        }
    }
}

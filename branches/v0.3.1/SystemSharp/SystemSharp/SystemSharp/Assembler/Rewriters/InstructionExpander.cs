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
using System.Linq;
using System.Text;
using SystemSharp.Analysis.M2M;
using SystemSharp.Common;
using SystemSharp.DataTypes;
using SystemSharp.Meta;

namespace SystemSharp.Assembler.Rewriters
{
    /// <summary>
    /// An expansion pattern replaces a XIL-S instruction by a custom sequence of new XIL-S instructions.
    /// </summary>
    public abstract class ExpansionPattern
    {
        /// <summary>
        /// The XIL opcodes this expansion pattern is valid for
        /// </summary>
        public abstract IEnumerable<string> HandlerKeys { get; }

        /// <summary>
        /// Expands a given XIL-S instruction to a sequence
        /// </summary>
        /// <param name="instr">XIL-S instruction to expand</param>
        /// <param name="preds">remapped dependencies</param>
        /// <returns>expanded sequence</returns>
        public abstract IEnumerable<XILSInstr> Expand(XILSInstr instr, InstructionDependency[] preds);

        /// <summary>
        /// Applies the pattern to a sequence of instructions by replacing each instruction by its expanded sequence where appropriate.
        /// </summary>
        /// <param name="instrs">sequence of instructions</param>
        /// <returns>expanded sequence</returns>
        public List<XILSInstr> Apply(IList<XILSInstr> instrs)
        {
            InstructionExpander xpander = new InstructionExpander(instrs, this);
            xpander.Rewrite();
            return xpander.OutInstructions;
        }
    }

    /// <summary>
    /// Default implementation of an expansion pattern
    /// </summary>
    public class DefaultExpansionPattern: ExpansionPattern
    {
        /// <summary>
        /// Template instruction to match
        /// </summary>
        public XILSInstr Match { get; private set; }

        /// <summary>
        /// Statically pre-defined expansion sequence
        /// </summary>
        public IEnumerable<XILSInstr> Expansion { get; private set; }

        /// <summary>
        /// Dynamically defined expansion functor
        /// </summary>
        public Func<TypeDescriptor, TypeDescriptor, InstructionDependency[], IEnumerable<XILSInstr>> ExpansionCreator { get; private set; }

        /// <summary>
        /// Creates an expansion pattern based on a statically pre-defined expansion sequence
        /// </summary>
        /// <param name="match">template instruction to match</param>
        /// <param name="expansion">its expansion sequence</param>
        public DefaultExpansionPattern(XILSInstr match, IEnumerable<XILSInstr> expansion)
        {
            Match = match;
            Expansion = expansion;
        }

        /// <summary>
        /// Creates an expansion pattern based on a functor-defined expansion sequence
        /// </summary>
        /// <param name="match">template instruction to match</param>
        /// <param name="expansionCreator">functor to create expansion</param>
        public DefaultExpansionPattern(XILSInstr match, Func<TypeDescriptor, TypeDescriptor, InstructionDependency[], IEnumerable<XILSInstr>> expansionCreator)
        {
            Match = match;
            ExpansionCreator = expansionCreator;
        }

        public override IEnumerable<string> HandlerKeys
        {
            get { yield return Match.Name; }
        }

        private IEnumerable<XILSInstr> Expand(TypeDescriptor joker, TypeDescriptor rtype, InstructionDependency[] preds)
        {
            if (ExpansionCreator != null)
            {
                foreach (var instr in ExpansionCreator(joker, rtype, preds))
                    yield return instr;
            }
            else
            {
                bool first = true;
                foreach (XILSInstr xilsi in Expansion)
                {
                    TypeDescriptor[] otypes = (TypeDescriptor[])xilsi.OperandTypes.Clone();
                    TypeDescriptor[] rtypes = (TypeDescriptor[])xilsi.ResultTypes.Clone();
                    for (int i = 0; i < otypes.Length; i++)
                    {
                        if (otypes[i].IsConstrained && !otypes[i].IsComplete)
                            otypes[i] = joker;
                    }
                    for (int i = 0; i < rtypes.Length; i++)
                    {
                        if (rtypes[i].IsConstrained && !rtypes[i].IsComplete)
                            rtypes[i] = joker;
                    }
                    yield return xilsi.Command.CreateStk(preds, otypes, rtypes);
                    if (first)
                    {
                        preds = new InstructionDependency[0];
                        first = false;
                    }
                }
            }
        }

        public override IEnumerable<XILSInstr> Expand(XILSInstr instr, InstructionDependency[] preds)
        {
            if (instr.Name != Match.Name)
                return null;

            TypeDescriptor joker = null;
            Debug.Assert(instr.OperandTypes.Length == Match.OperandTypes.Length);
            Debug.Assert(instr.ResultTypes.Length == Match.ResultTypes.Length);
            for (int i = 0; i < instr.OperandTypes.Length; i++)
            {
                TypeDescriptor otypei = instr.OperandTypes[i];
                TypeDescriptor otypem = Match.OperandTypes[i];
                if (!otypei.CILType.Equals(otypem.CILType))
                    return null;
                if (otypem.IsConstrained && !otypem.IsComplete)
                {
                    if (otypei.IsConstrained)
                    {
                        joker = otypei;
                        otypei = otypei.MakeUnconstrainedType();
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            for (int i = 0; i < instr.ResultTypes.Length; i++)
            {
                TypeDescriptor rtypei = instr.ResultTypes[i];
                TypeDescriptor rtypem = Match.ResultTypes[i];
                if (!rtypei.CILType.Equals(rtypem.CILType))
                    return null;
                if (rtypem.IsConstrained && !rtypem.IsComplete)
                {
                    if (rtypei.IsConstrained)
                    {
                        if (joker == null)
                            joker = rtypei;
                        rtypei = rtypei.MakeUnconstrainedType();
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            return Expand(
                joker, 
                instr.ResultTypes.Length == 0 ? null : instr.ResultTypes[0],
                preds);
        }
    }

    /// <summary>
    /// This XIL-S code transformation rewrites code based on an expansion pattern
    /// </summary>
    public class InstructionExpander: XILSRewriter
    {
        /// <summary>
        /// Expansion pattern to use
        /// </summary>
        public ExpansionPattern Pattern { get; private set; }

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="instrs">instruction list</param>
        /// <param name="pattern">expansion pattern to apply</param>
        public InstructionExpander(IList<XILSInstr> instrs, ExpansionPattern pattern) :
            base(instrs)
        {
            Pattern = pattern;
            foreach (string key in pattern.HandlerKeys)
                SetHandler(key, Expand);
        }

        private void Expand(XILSInstr xilsi)
        {
            var preds = RemapPreds(xilsi.Preds);
            var expansion = Pattern.Expand(xilsi, preds);
            if (expansion == null)
            {
                ProcessDefault(xilsi);
            }
            else
            {
                foreach (XILSInstr xilsix in expansion)
                {
                    Emit(xilsix);
                }

                if (xilsi.ResultTypes.Length > 0 &&
                    !TypeStack.Peek().Equals(xilsi.ResultTypes.Last()))
                {
                    Emit(
                        DefaultInstructionSet.Instance.Convert()
                            .CreateStk(1, TypeStack.Peek(), xilsi.ResultTypes.Last()));
                }
            }
        }
    }

    /// <summary>
    /// Default instruction expansions
    /// </summary>
    public enum EXILSExpansion
    {
        /// <summary>
        /// Replaces abs(x) by select(x &lt; 0, -x, x) for datatype double. Useful if target platform has no direct implementation of absolute value function.
        /// </summary>
        Expand_Abs_double,

        /// <summary>
        /// Replaces -x by (0-x) for datatype double. Useful if target platform has no direct implementation of negation function.
        /// </summary>
        Expand_Neg_double,

        /// <summary>
        /// Replaces -x by (0-x) for datatype float. Useful if target platform has no direct implementation of negation function.
        /// </summary>
        Expand_Neg_float,

        /// <summary>
        /// Replaces abs(x) by select(x &lt; 0, -x, x) for datatype int. Useful if target platform has no direct implementation of absolute value function.
        /// </summary>
        Expand_Abs_int,

        /// <summary>
        /// Replaces -x by (0-x) for datatype int. Useful if target platform has no direct implementation of negation function.
        /// </summary>
        Expand_Neg_int,

        /// <summary>
        /// Replaces cos(x) by scsincos(x) for datatype double.
        /// </summary>
        Expand_Cos_ScSinCos_double,

        /// <summary>
        /// Replaces sin(x) by scsincos(x) for datatype double.
        /// </summary>
        Expand_Sin_ScSinCos_double,

        /// <summary>
        /// Replaces cos(x) by scsincos(x) for datatype float.
        /// </summary>
        Expand_Cos_ScSinCos_single,

        /// <summary>
        /// Replaces sin(x) by scsincos(x) for datatype float.
        /// </summary>
        Expand_Sin_ScSinCos_single,

        /// <summary>
        /// Replaces cos(x) by scsincos(x) for datatype SFix.
        /// </summary>
        Expand_Cos_ScSinCos_fixpt,

        /// <summary>
        /// Replaces sin(x) by scsincos(x) for datatype SFix.
        /// </summary>
        Expand_Sin_ScSinCos_fixpt
    }

    /// <summary>
    /// This attribute instructs the XIL compiler to apply one of the default code transformations
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited=true, AllowMultiple=true)]
    public class ExpandXILS : 
        Attribute,
        IXILSRewriter
    {
        private EXILSExpansion _expansion;

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="expansion">which default transformation to apply</param>
        public ExpandXILS(EXILSExpansion expansion)
        {
            _expansion = expansion;
        }

        public override string ToString()
        {
            return _expansion.ToString();
        }

        public IList<XILSInstr> Rewrite(IList<XILSInstr> instrs)
        {
            switch (_expansion)
            {
                case EXILSExpansion.Expand_Abs_double:
                    return DefaultExpansionPatterns.Rewrite_Abs_double.Apply(instrs);

                case EXILSExpansion.Expand_Abs_int:
                    return DefaultExpansionPatterns.Rewrite_Abs_int.Apply(instrs);

                case EXILSExpansion.Expand_Neg_double:
                    return DefaultExpansionPatterns.Rewrite_Neg_double.Apply(instrs);

                case EXILSExpansion.Expand_Neg_float:
                    return DefaultExpansionPatterns.Rewrite_Neg_float.Apply(instrs);

                case EXILSExpansion.Expand_Neg_int:
                    return DefaultExpansionPatterns.Rewrite_Neg_int.Apply(instrs);

                case EXILSExpansion.Expand_Cos_ScSinCos_double:
                    return DefaultExpansionPatterns.Rewrite_Cos_ScSinCos_double(4, 28).Apply(instrs);
                    //return DefaultExpansionPatterns.Rewrite_Cos_ScSinCos_double(4, 24).Apply(instrs);

                case EXILSExpansion.Expand_Sin_ScSinCos_double:
                    return DefaultExpansionPatterns.Rewrite_Sin_ScSinCos_double(4, 28).Apply(instrs);
                    //return DefaultExpansionPatterns.Rewrite_Sin_ScSinCos_double(4, 24).Apply(instrs);

                case EXILSExpansion.Expand_Cos_ScSinCos_single:
                    return DefaultExpansionPatterns.Rewrite_Cos_ScSinCos_single(4, 23).Apply(instrs);
                    //return DefaultExpansionPatterns.Rewrite_Cos_ScSinCos_single(4, 18).Apply(instrs);

                case EXILSExpansion.Expand_Sin_ScSinCos_single:
                    return DefaultExpansionPatterns.Rewrite_Sin_ScSinCos_single(4, 23).Apply(instrs);
                    //return DefaultExpansionPatterns.Rewrite_Sin_ScSinCos_single(4, 18).Apply(instrs);

                case EXILSExpansion.Expand_Cos_ScSinCos_fixpt:
                    return DefaultExpansionPatterns.Rewrite_Cos_ScSinCos_fixpt().Apply(instrs);

                case EXILSExpansion.Expand_Sin_ScSinCos_fixpt:
                    return DefaultExpansionPatterns.Rewrite_Sin_ScSinCos_fixpt().Apply(instrs);

                default:
                    throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    /// This static class provides some default code transformation patterns which are useful for transforming code to hardware
    /// </summary>
    public static class DefaultExpansionPatterns
    {
        /// <summary>
        /// Returns an expansion pattern which replaces abs(x) by select(x &lt; 0, -x, x) for datatype double. 
        /// Useful if target platform has no direct implementation of absolute value function.
        /// </summary>
        public static readonly ExpansionPattern Rewrite_Abs_double = new DefaultExpansionPattern(
            DefaultInstructionSet.Instance.Abs().CreateStk(1, typeof(double), typeof(double)),
            new XILSInstr[] {
                DefaultInstructionSet.Instance.Dup().CreateStk(1, typeof(double), typeof(double), typeof(double)),
                DefaultInstructionSet.Instance.Dup().CreateStk(1, typeof(double), typeof(double), typeof(double)),
                DefaultInstructionSet.Instance.Neg().CreateStk(1, typeof(double), typeof(double)),
                DefaultInstructionSet.Instance.Swap().CreateStk(2, typeof(double), typeof(double), typeof(double), typeof(double)),
                DefaultInstructionSet.Instance.LdConst(0.0).CreateStk(0, typeof(double)),
                DefaultInstructionSet.Instance.IsLt().CreateStk(2, typeof(double), typeof(double), typeof(bool)),
                DefaultInstructionSet.Instance.Select().CreateStk(3, typeof(double), typeof(double), typeof(bool), typeof(double))
            });

        /// <summary>
        /// Returns an expansion pattern which replaces abs(x) by select(x &lt; 0, -x, x) for datatype int. 
        /// Useful if target platform has no direct implementation of absolute value function.
        /// </summary>
        public static readonly ExpansionPattern Rewrite_Abs_int = new DefaultExpansionPattern(
            DefaultInstructionSet.Instance.Abs().CreateStk(1, typeof(Signed), typeof(Signed)),
            new XILSInstr[] {
                DefaultInstructionSet.Instance.Dup().CreateStk(1, typeof(Signed), typeof(Signed), typeof(Signed)),
                DefaultInstructionSet.Instance.Dup().CreateStk(1, typeof(Signed), typeof(Signed), typeof(Signed)),
                DefaultInstructionSet.Instance.Neg().CreateStk(1, typeof(Signed), typeof(Signed)),
                DefaultInstructionSet.Instance.Swap().CreateStk(2, typeof(Signed), typeof(Signed), typeof(Signed), typeof(Signed)),
                DefaultInstructionSet.Instance.Ld0().CreateStk(0, typeof(Signed)),
                DefaultInstructionSet.Instance.IsLt().CreateStk(2, typeof(Signed), typeof(Signed), typeof(bool)),
                DefaultInstructionSet.Instance.Select().CreateStk(3, typeof(Signed), typeof(Signed), typeof(bool), typeof(Signed))
            });

        /// <summary>
        /// Returns an expansion pattern which replaces -x by (0-x) for datatype double. 
        /// Useful if target platform has no direct implementation of negation function.
        /// </summary>
        public static readonly ExpansionPattern Rewrite_Neg_double = new DefaultExpansionPattern(
            DefaultInstructionSet.Instance.Neg().CreateStk(1, typeof(double), typeof(double)),
            new XILSInstr[] {
                DefaultInstructionSet.Instance.LdConst(0.0).CreateStk(0, typeof(double)),
                DefaultInstructionSet.Instance.Swap().CreateStk(2, typeof(double), typeof(double), typeof(double), typeof(double)),
                DefaultInstructionSet.Instance.Sub().CreateStk(2, typeof(double), typeof(double), typeof(double))
            });

        /// <summary>
        /// Returns an expansion pattern which replaces -x by (0-x) for datatype float. 
        /// Useful if target platform has no direct implementation of negation function.
        /// </summary>
        public static readonly ExpansionPattern Rewrite_Neg_float = new DefaultExpansionPattern(
            DefaultInstructionSet.Instance.Neg().CreateStk(1, typeof(float), typeof(float)),
            new XILSInstr[] {
                DefaultInstructionSet.Instance.LdConst(0.0f).CreateStk(0, typeof(float)),
                DefaultInstructionSet.Instance.Swap().CreateStk(2, typeof(float), typeof(float), typeof(float), typeof(float)),
                DefaultInstructionSet.Instance.Sub().CreateStk(2, typeof(float), typeof(float), typeof(float))
            });

        /// <summary>
        /// Returns an expansion pattern which replaces -x by (0-x) for datatype int. 
        /// Useful if target platform has no direct implementation of negation function.
        /// </summary>
        public static readonly ExpansionPattern Rewrite_Neg_int = new DefaultExpansionPattern(
            DefaultInstructionSet.Instance.Neg().CreateStk(1, typeof(Signed), typeof(Signed)),
            new XILSInstr[] {
                DefaultInstructionSet.Instance.Ld0().CreateStk(0, typeof(Signed)),
                DefaultInstructionSet.Instance.Swap().CreateStk(2, typeof(Signed), typeof(Signed), typeof(Signed), typeof(Signed)),
                DefaultInstructionSet.Instance.Sub().CreateStk(2, typeof(Signed), typeof(Signed), typeof(Signed))
            });

        /// <summary>
        /// Returns an expansion pattern which replaces cos(x) by sccos(y). y=(1/PI)*x is converted from double to fixed point, 
        /// the result is converted back to double. 
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        /// <param name="convIntWidth">desired fixed-point conversion integer width</param>
        /// <param name="convFracWidth">desired fixed-point conversion fractional width</param>
        public static ExpansionPattern Rewrite_Cos_double(int convIntWidth, int convFracWidth)
        {
            TypeDescriptor tmpSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, convIntWidth, convFracWidth));
            TypeDescriptor scaSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 3, convFracWidth));
            TypeDescriptor scaSFixT2 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 2, convFracWidth));
            DefaultInstructionSet iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Cos().CreateStk(1, typeof(double), typeof(double)),
                new XILSInstr[] {
                    iset.LdConst(1.0 / Math.PI).CreateStk(0, typeof(double)),
                    iset.Mul().CreateStk(2, typeof(double), typeof(double), typeof(double)),
                    iset.Convert().CreateStk(1, typeof(double), tmpSFixT),
                    iset.Convert().CreateStk(1, tmpSFixT, scaSFixT),
                    iset.ScCos().CreateStk(1, scaSFixT, scaSFixT2),
                    iset.Convert().CreateStk(1, scaSFixT2, typeof(double))
                });
        }

        /// <summary>
        /// Returns an expansion pattern which replaces sin(x) by scsin(y). y=(1/PI)*x is converted from double to fixed point, 
        /// the result is converted back to double. 
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        /// <param name="convIntWidth">desired fixed-point conversion integer width</param>
        /// <param name="convFracWidth">desired fixed-point conversion fractional width</param>
        public static ExpansionPattern Rewrite_Sin_double(int convIntWidth, int convFracWidth)
        {
            TypeDescriptor tmpSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, convIntWidth, convFracWidth));
            TypeDescriptor scaSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 3, convFracWidth));
            TypeDescriptor scaSFixT2 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 2, convFracWidth));
            DefaultInstructionSet iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Sin().CreateStk(1, typeof(double), typeof(double)),
                new XILSInstr[] {
                    iset.LdConst(1.0 / Math.PI).CreateStk(0, typeof(double)),
                    iset.Mul().CreateStk(2, typeof(double), typeof(double), typeof(double)),
                    iset.Convert().CreateStk(1, typeof(double), tmpSFixT),
                    iset.Convert().CreateStk(1, tmpSFixT, scaSFixT),
                    iset.ScSin().CreateStk(1, scaSFixT, scaSFixT2),
                    iset.Convert().CreateStk(1, scaSFixT2, typeof(double))
                });
        }

        /// <summary>
        /// Returns an expansion pattern which replaces cos(x) by scsincos(y). y=(1/PI)*x is converted from double to fixed point, 
        /// the result tuple is converted back to double. 
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        /// <param name="convIntWidth">desired fixed-point conversion integer width</param>
        /// <param name="convFracWidth">desired fixed-point conversion fractional width</param>
        public static ExpansionPattern Rewrite_Cos_ScSinCos_double(int convIntWidth, int convFracWidth)
        {
            TypeDescriptor tmpSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, convIntWidth, convFracWidth));
            TypeDescriptor scaSFixT3 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 3, convFracWidth));
            TypeDescriptor scaSFixT2 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 2, convFracWidth));
            DefaultInstructionSet iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Cos().CreateStk(1, typeof(double), typeof(double)),
                new XILSInstr[] {
                    iset.LdConst(1.0 / Math.PI).CreateStk(0, typeof(double)),
                    iset.Mul().CreateStk(2, typeof(double), typeof(double), typeof(double)),
                    iset.Convert().CreateStk(1, typeof(double), tmpSFixT),
                    iset.Mod2().CreateStk(1, tmpSFixT, scaSFixT3),
                    iset.ScSinCos().CreateStk(1, scaSFixT3, scaSFixT2, scaSFixT2),
                    iset.Pop().CreateStk(1, scaSFixT2),
                    iset.Convert().CreateStk(1, scaSFixT2, typeof(double))
                });
        }

        /// <summary>
        /// Returns an expansion pattern which replaces sin(x) by scsincos(y). y=(1/PI)*x is converted from double to fixed point, 
        /// the result tuple is converted back to double. 
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        /// <param name="convIntWidth">desired fixed-point conversion integer width</param>
        /// <param name="convFracWidth">desired fixed-point conversion fractional width</param>
        public static ExpansionPattern Rewrite_Sin_ScSinCos_double(int convIntWidth, int convFracWidth)
        {
            TypeDescriptor tmpSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, convIntWidth, convFracWidth));
            TypeDescriptor scaSFixT3 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 3, convFracWidth));
            TypeDescriptor scaSFixT2 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 2, convFracWidth));
            DefaultInstructionSet iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Sin().CreateStk(1, typeof(double), typeof(double)),
                new XILSInstr[] {
                    iset.LdConst(1.0 / Math.PI).CreateStk(0, typeof(double)),
                    iset.Mul().CreateStk(2, typeof(double), typeof(double), typeof(double)),
                    iset.Convert().CreateStk(1, typeof(double), tmpSFixT),
                    iset.Mod2().CreateStk(1, tmpSFixT, scaSFixT3),
                    iset.ScSinCos().CreateStk(1, scaSFixT3, scaSFixT2, scaSFixT2),
                    iset.Swap().CreateStk(2, scaSFixT2, scaSFixT2, scaSFixT2, scaSFixT2),
                    iset.Pop().CreateStk(1, scaSFixT2),
                    iset.Convert().CreateStk(1, scaSFixT2, typeof(double))
                });
        }

        /// <summary>
        /// Returns an expansion pattern which replaces cos(x) by scsincos(y). y=(1/PI)*x is converted from float to fixed point, 
        /// the result tuple is converted back to float. 
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        /// <param name="convIntWidth">desired fixed-point conversion integer width</param>
        /// <param name="convFracWidth">desired fixed-point conversion fractional width</param>
        public static ExpansionPattern Rewrite_Cos_ScSinCos_single(int convIntWidth, int convFracWidth)
        {
            TypeDescriptor tmpSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, convIntWidth, convFracWidth));
            TypeDescriptor scaSFixT3 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 3, convFracWidth));
            TypeDescriptor scaSFixT2 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 2, convFracWidth));
            DefaultInstructionSet iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Cos().CreateStk(1, typeof(float), typeof(float)),
                new XILSInstr[] {
                    iset.LdConst((float)(1.0 / Math.PI)).CreateStk(0, typeof(float)),
                    iset.Mul().CreateStk(2, typeof(float), typeof(float), typeof(float)),
                    iset.Convert().CreateStk(1, typeof(float), tmpSFixT),
                    iset.Mod2().CreateStk(1, tmpSFixT, scaSFixT3),
                    iset.ScSinCos().CreateStk(1, scaSFixT3, scaSFixT2, scaSFixT2),
                    iset.Pop().CreateStk(1, scaSFixT2),
                    iset.Convert().CreateStk(1, scaSFixT2, typeof(float))
                });
        }

        /// <summary>
        /// Returns an expansion pattern which replaces sin(x) by scsincos(y). y=(1/PI)*x is converted from float to fixed point, 
        /// the result tuple is converted back to float. 
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        /// <param name="convIntWidth">desired fixed-point conversion integer width</param>
        /// <param name="convFracWidth">desired fixed-point conversion fractional width</param>
        public static ExpansionPattern Rewrite_Sin_ScSinCos_single(int convIntWidth, int convFracWidth)
        {
            TypeDescriptor tmpSFixT = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, convIntWidth, convFracWidth));
            TypeDescriptor scaSFixT3 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 3, convFracWidth));
            TypeDescriptor scaSFixT2 = TypeDescriptor.GetTypeOf(
                SFix.FromDouble(0.0, 2, convFracWidth));
            DefaultInstructionSet iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Sin().CreateStk(1, typeof(float), typeof(float)),
                new XILSInstr[] {
                    iset.LdConst((float)(1.0 / Math.PI)).CreateStk(0, typeof(float)),
                    iset.Mul().CreateStk(2, typeof(float), typeof(float), typeof(float)),
                    iset.Convert().CreateStk(1, typeof(float), tmpSFixT),
                    iset.Mod2().CreateStk(1, tmpSFixT, scaSFixT3),
                    iset.ScSinCos().CreateStk(1, scaSFixT3, scaSFixT2, scaSFixT2),
                    iset.Swap().CreateStk(2, scaSFixT2, scaSFixT2, scaSFixT2, scaSFixT2),
                    iset.Pop().CreateStk(1, scaSFixT2),
                    iset.Convert().CreateStk(1, scaSFixT2, typeof(float))
                });
        }

        private static IEnumerable<XILSInstr> Rewrite_Cos_ScSinCos_fixpt(TypeDescriptor joker, TypeDescriptor rtype, InstructionDependency[] preds)
        {
            var iset = DefaultInstructionSet.Instance;
            var fmt = joker.GetFixFormat();
            var rfmt = rtype.GetFixFormat();
            int pifw = fmt.FracWidth;
            var pitype = SFix.MakeType(0, pifw);
            int muliw = fmt.IntWidth;
            var multype = SFix.MakeType(muliw, fmt.FracWidth + pifw);
            int fw = Math.Max(5, rfmt.FracWidth + 1); // Xilinx Cordic needs at least 8 input bits
            fw = Math.Min(45, fw); // Xilinx Cordic likes at most 48 input bits
            var cuttype = SFix.MakeType(3, fw);
            var modtype = SFix.MakeType(3, fw); // Actually, 1 integer bit less is required. However, Xilinx Cordic needs the additional bit.
            int fwr = Math.Max(6, rfmt.FracWidth); // Xilinx Cordic needs at least 8 output bits (?)
            fwr = Math.Min(46, fwr); // Xilinx Cordic likes at most 48 output bits (?)
            var sintype = SFix.MakeType(2, fwr);
            if (muliw <= 1)
            {
                return new XILSInstr[] 
                {
                    iset.LdConst(SFix.FromDouble(1.0 / Math.PI, 0, pifw)).CreateStk(preds, 0, pitype),
                    iset.Mul().CreateStk(2, joker, pitype, multype),
                    iset.Convert().CreateStk(1, multype, modtype),
                    iset.ScSinCos().CreateStk(1, modtype, sintype, sintype),
                    iset.Pop().CreateStk(1, sintype)
                };
            }
            else
            {
                return new XILSInstr[] 
                {
                    iset.LdConst(SFix.FromDouble(1.0 / Math.PI, 0, pifw)).CreateStk(preds, 0, pitype),
                    iset.Mul().CreateStk(2, joker, pitype, multype),
                    iset.Convert().CreateStk(1, multype, cuttype),
                    iset.Mod2().CreateStk(1, cuttype, modtype),
                    iset.ScSinCos().CreateStk(1, modtype, sintype, sintype),
                    iset.Pop().CreateStk(1, sintype)
                };
            }
        }

        private static IEnumerable<XILSInstr> Rewrite_Sin_ScSinCos_fixpt(TypeDescriptor joker, TypeDescriptor rtype, InstructionDependency[] preds)
        {
            var iset = DefaultInstructionSet.Instance;
            var fmt = joker.GetFixFormat();
            var rfmt = rtype.GetFixFormat();
            int pifw = fmt.FracWidth;
            var pitype = SFix.MakeType(0, pifw);
            int muliw = fmt.IntWidth;
            var multype = SFix.MakeType(muliw, fmt.FracWidth + pifw);
            int fw = Math.Max(5, rfmt.FracWidth + 1); // Xilinx Cordic needs at least 8 input bits
            fw = Math.Min(45, fw); // Xilinx Cordic likes at most 48 input bits
            var cuttype = SFix.MakeType(3, fw);
            var modtype = SFix.MakeType(3, fw); // Actually, 1 integer bit less is required. However, Xilinx Cordic needs the additional bit.
            int fwr = Math.Max(6, rfmt.FracWidth); // Xilinx Cordic needs at least 8 output bits (?)
            fwr = Math.Min(46, fwr); // Xilinx Cordic likes at most 48 output bits (?)
            var sintype = SFix.MakeType(2, fwr);
            if (muliw <= 1)
            {
                return new XILSInstr[] 
                {
                    iset.LdConst(SFix.FromDouble(1.0 / Math.PI, 0, pifw)).CreateStk(preds, 0, pitype),
                    iset.Mul().CreateStk(2, joker, pitype, multype),
                    iset.Convert().CreateStk(1, multype, modtype),
                    iset.ScSinCos().CreateStk(1, modtype, sintype, sintype),
                    iset.Swap().CreateStk(2, sintype, sintype, sintype, sintype),
                    iset.Pop().CreateStk(1, sintype)
                };
            }
            else
            {
                return new XILSInstr[] 
                {
                    iset.LdConst(SFix.FromDouble(1.0 / Math.PI, 0, pifw)).CreateStk(preds, 0, pitype),
                    iset.Mul().CreateStk(2, joker, pitype, multype),
                    iset.Convert().CreateStk(1, multype, cuttype),
                    iset.Mod2().CreateStk(1, cuttype, modtype),
                    iset.ScSinCos().CreateStk(1, modtype, sintype, sintype),
                    iset.Swap().CreateStk(2, sintype, sintype, sintype, sintype),
                    iset.Pop().CreateStk(1, sintype)
                };
            }
        }

        /// <summary>
        /// Returns an expansion pattern which replaces cos(x) by scsincos(y), y=(1/PI)*x.
        /// x must be a fixed-point arithmetic operand. The intermediate accuracy of the multiplication is
        /// determined automatically, considering the accuracies of input and output type. Moreover, the word lengths
        /// are constrained to fit the specific needs of Xilinx Cordic IP core.
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        public static ExpansionPattern Rewrite_Cos_ScSinCos_fixpt()
        {
            var iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Cos().CreateStk(1, typeof(SFix), typeof(SFix)), Rewrite_Cos_ScSinCos_fixpt);
        }

        /// <summary>
        /// Returns an expansion pattern which replaces sin(x) by scsincos(y), y=(1/PI)*x.
        /// x must be a fixed-point arithmetic operand. The intermediate accuracy of the multiplication is
        /// determined automatically, considering the accuracies of input and output type. Moreover, the word lengths
        /// are constrained to fit the specific needs of Xilinx Cordic IP core.
        /// Useful if target platform implements sin/cos on fixed-point, scaled-radian basis only.
        /// </summary>
        public static ExpansionPattern Rewrite_Sin_ScSinCos_fixpt()
        {
            var iset = DefaultInstructionSet.Instance;
            return new DefaultExpansionPattern(
                iset.Sin().CreateStk(1, typeof(SFix), typeof(SFix)), Rewrite_Sin_ScSinCos_fixpt);
        }
    }
}

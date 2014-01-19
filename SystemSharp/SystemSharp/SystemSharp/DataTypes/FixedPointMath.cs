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
using System.Threading;
using SystemSharp.Analysis;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM;

namespace SystemSharp.DataTypes
{
    /// <summary>
    /// Treatment options for arithmetic overflows
    /// </summary>
    public enum EOverflowMode
    {
        /// <summary>
        /// Binary wrap in two's complement representation. 
        /// This is the default behavior of integer arithmetic in most programming languages.
        /// </summary>
        Wrap,

        /// <summary>
        /// Throw an exception whenever there is an arithmetic overflow.
        /// </summary>
        Fail,

        /// <summary>
        /// Saturate towards the minimum/maximum representable value.
        /// </summary>
        Saturate
    }

    /// <summary>
    /// Options for determining the integer and fractional widths of results in the context of
    /// fixed-point arithmetic operators.
    /// </summary>
    public enum EArithSizingMode
    {
        /// <summary>
        /// The results are sized exactly the same way like VHDL.
        /// </summary>
        VHDLCompliant,

        /// <summary>
        /// The results are sized in a safe way, such that there is never an arithmetic overflow (except for division by 0) and
        /// no loss of precision (except for divisions and transcendent functions).
        /// </summary>
        Safe,

        /// <summary>
        /// The total result width matches the total width of the first operand.
        /// </summary>
        InSizeIsOutSize
    }

    /// <summary>
    /// Provides features for getting and setting fixed-point arithmetic options.
    /// </summary>
    public class FixedPointSettings
    {
        /// <summary>
        /// Gets or sets the default treatment of arithmetic overflows, i.e. when there is no process-local treatment
        /// specified. The initial mode is <c>EOverflowMode.Wrap</c>.
        /// </summary>
        public static EOverflowMode GlobalOverflowMode = EOverflowMode.Wrap;

        private static int _globalDefaultRadix = 2;

        /// <summary>
        /// Gets or sets the default radix for fixed-point-to-string conversions, i.e. when there is no process-local
        /// radix set. The initial radix is 2 (i.e. binary representation).
        /// </summary>
        public static int GlobalDefaultRadix
        {
            get { return _globalDefaultRadix; }
            set
            {
                if (value < 2)
                    throw new ArgumentException();
                _globalDefaultRadix = value;
            }
        }

        /// <summary>
        /// Gets or sets the default sizing mode for results of fixed-point arithmetic operations, i.e. when there is
        /// no process-local sizing mode specified. The initial value is <c>EArithSizingMode.VHDLCompliant</c>.
        /// </summary>
        public static EArithSizingMode GlobalArithSizingMode = EArithSizingMode.VHDLCompliant;

        private PLSSlot _localOverflowMode;
        private PLSSlot _localDefaultRadix;
        private PLSSlot _localArithSizingMode;

        internal FixedPointSettings(DesignContext context)
        {
            _localOverflowMode = context.AllocPLS();
            _localDefaultRadix = context.AllocPLS();
            _localArithSizingMode = context.AllocPLS();
        }

        /// <summary>
        /// Gets or sets the process-local treatment of arithmetic overflows.
        /// </summary>
        public EOverflowMode OverflowMode
        {
            [StaticEvaluationDoNotAnalyze]
            get 
            {
                if (_localOverflowMode.Value == null)
                    return GlobalOverflowMode;
                else
                    return (EOverflowMode)_localOverflowMode.Value; 
            }
            [IgnoreOnDecompilation(true)]
            set 
            { 
                _localOverflowMode.Value = value; 
            }
        }

        /// <summary>
        /// Gets or sets the process-local radix for fixed-point-to-string conversions.
        /// </summary>
        public int DefaultRadix
        {
            [StaticEvaluationDoNotAnalyze]
            get 
            {
                if (_localDefaultRadix.Value == null)
                    return GlobalDefaultRadix;
                else
                    return (int)_localDefaultRadix.Value; 
            }
            [IgnoreOnDecompilation(true)]
            set
            {
                if (value < 2)
                    throw new ArgumentException();
                _localDefaultRadix.Value = value;
            }
        }

        /// <summary>
        /// Gets or sets the process-local sizing mode for results of fixed-point arithmetic operations.
        /// </summary>
        public EArithSizingMode ArithSizingMode
        {
            [StaticEvaluationDoNotAnalyze]
            get
            {
                if (_localArithSizingMode.Value == null)
                    return GlobalArithSizingMode;
                else
                    return (EArithSizingMode)_localArithSizingMode.Value;
            }
            [IgnoreOnDecompilation(true)]
            set
            {
                _localArithSizingMode.Value = value;
            }
        }
    }

    class RewriteIncrement : RewriteCall
    {
        public bool IsSigned { get; private set; }
        public bool IsDecrement { get; private set; }

        public RewriteIncrement(bool isSigned, bool isDecrement)
        {
            IsSigned = isSigned;
            IsDecrement = isDecrement;
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            if (args[0].Sample == null)
                return false;

            ISized one, rsample;
            if (IsSigned)
            {
                Signed sample = (Signed)args[0].Sample;
                Signed sone = Signed.FromLong(1, (int)sample.Size);
                one = sone;
                rsample = sample + sone;
            }
            else
            {
                Unsigned sample = (Unsigned)args[0].Sample;
                Unsigned uone = Unsigned.FromULong(1, (int)sample.Size);
                one = uone;
                rsample = sample + uone;
            }
            LiteralReference oneLit = LiteralReference.CreateConstant(one);
            Expression inc;
            if (IsDecrement)
                inc = args[0].Expr - oneLit;
            else
                inc = args[0].Expr + oneLit;
            inc.ResultType = TypeDescriptor.GetTypeOf(rsample);
            inc = IntrinsicFunctions.Resize(inc, (int)one.Size, TypeDescriptor.GetTypeOf(one));
            stack.Push(new StackElement(inc, one, Analysis.Msil.EVariability.ExternVariable));
            return true;
        }
    }

    /// <summary>
    /// Describes a fixed-point number format.
    /// </summary>
    public class FixFormat
    {
        /// <summary>
        /// Whether described number is signed.
        /// </summary>
        public bool IsSigned { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Integer width
        /// </summary>
        public int IntWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Fractional width
        /// </summary>
        public int FracWidth { [StaticEvaluation] get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="isSigned">whether format describes signed numbers</param>
        /// <param name="intWidth">integer width</param>
        /// <param name="fracWidth">fractional width</param>
        public FixFormat(bool isSigned, int intWidth, int fracWidth)
        {
            IsSigned = isSigned;
            IntWidth = intWidth;
            FracWidth = fracWidth;
        }

        /// <summary>
        /// total number of bits required to represent an number in this format, i.e. <c>IntWidth + FracWidth</c>.
        /// </summary>
        public int TotalWidth
        {
            [StaticEvaluation] get { return IntWidth + FracWidth; }
        }

        public override int GetHashCode()
        {
            int shift = FracWidth % 31;
            int hash = (IntWidth << shift) | (IntWidth >> (32 - shift));
            if (IsSigned)
                hash = ~hash;
            return hash;
        }

        /// <summary>
        /// Two fixed-point formats are defined to be equal iff the have the same signedness and integer/fractional widths match.
        /// </summary>
        public override bool Equals(object obj)
        {
            FixFormat other = (FixFormat)obj;
            if (other == null)
                return false;
            return IsSigned == other.IsSigned &&
                IntWidth == other.IntWidth &&
                FracWidth == other.FracWidth;
        }

        public override string ToString()
        {
            string result = IsSigned ? "SFix" : "UFix";
            result += TotalWidth;
            result += "_";
            result += FracWidth;
            return result;
        }
    }

    static class FixFormatToRangeConverter
    {
        /// <summary>
        /// Converts <paramref name="fmt"/> to a range repesentation in the way it is typically used in VHDL designs.
        /// </summary>
        public static Range ConvertToRange(FixFormat fmt)
        {
            return new Range(fmt.IntWidth - 1, -fmt.FracWidth, EDimDirection.Downto);
        }
    }

    /// <summary>
    /// This static class provides convenience methods to work with fixed-point number descriptions.
    /// </summary>
    public static class FixPointExtensions
    {
        /// <summary>
        /// Extracts the fixed-point format from a SysDOM type descriptor, given that it actually describes a fixed-point number type.
        /// </summary>
        public static FixFormat GetFixFormat(this TypeDescriptor td)
        {
            if (td.CILType.Equals(typeof(Signed)) ||
                td.CILType.Equals(typeof(SFix)))
                return SFix.GetFormat(td);
            else if (td.CILType.Equals(typeof(Unsigned)) ||
                td.CILType.Equals(typeof(UFix)))
                return UFix.GetFormat(td);
            else
                return null;
        }

        /// <summary>
        /// Converts the format to a SysDOM type descriptor.
        /// </summary>
        public static TypeDescriptor ToType(this FixFormat fmt)
        {
            if (fmt.IsSigned)
                return TypeDescriptor.GetTypeOf(SFix.FromDouble(0.0, fmt.IntWidth, fmt.FracWidth));
            else
                return TypeDescriptor.GetTypeOf(UFix.FromDouble(0.0, fmt.IntWidth, fmt.FracWidth));
        }
    }
}

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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.Meta;

namespace SystemSharp.DataTypes
{
    /// <summary>
    /// This class is used to represent variable floating point formats.
    /// </summary>
    public class FloatFormat
    {
        /// <summary>
        /// The exponent width (in bits).
        /// </summary>
        public int ExponentWidth { get; private set; }

        /// <summary>
        /// The mantissa width (in bits).
        /// </summary>
        public int FractionWidth { get; private set; }

        /// <summary>
        /// Constructs a floating point format.
        /// </summary>
        /// <param name="exponentWidth">The desired exponent width</param>
        /// <param name="fractionWidth">The desired mantiasse width</param>
        public FloatFormat(int exponentWidth, int fractionWidth)
        {
            ExponentWidth = exponentWidth;
            FractionWidth = fractionWidth;
        }

        /// <summary>
        /// Computes the total amount of bits which are needed to encode a floating point number of this format.
        /// </summary>
        public int TotalWidth
        {
            get
            {
                return ExponentWidth + FractionWidth + 1;
            }
        }

        /// <summary>
        /// Computes the bias value as defined by IEEE754.
        /// </summary>
        public int Bias
        {
            get
            {
                return (1 << (ExponentWidth - 1)) - 1;
            }
        }

        /// <summary>
        /// The IEEE754 single format (32 bits in total).
        /// </summary>
        public static readonly FloatFormat SingleFormat = new FloatFormat(8, 23);

        /// <summary>
        /// The IEEE754 double format (64 bits in total).
        /// </summary>
        public static readonly FloatFormat DoubleFormat = new FloatFormat(11, 52);

        /// <summary>
        /// The minimum IEEE754 single extended format (43 bits in total).
        /// </summary>
        public static readonly FloatFormat SingleExtendedFormat = new FloatFormat(11, 31);

        /// <summary>
        /// The minimum IEEE754 double extended format (79 bits in total).
        /// </summary>
        public static readonly FloatFormat DoubleExtendedFormat = new FloatFormat(15, 63);
    }

    /// <summary>
    /// This class provides various helper methods which convert from IEEE754 floating point number to their
    /// raw encoding and vice versa.
    /// </summary>
    public static class IEEE754Support
    {
        /// <summary>
        /// Converts a double to its raw encoding.
        /// </summary>
        /// <param name="value">The value to be encoded</param>
        /// <returns>The binary encoding</returns>
        public static ulong Float64ToBits(double value)
        {
            byte[] buf = new byte[8];
            MemoryStream msw = new MemoryStream(buf);
            BinaryWriter bw = new BinaryWriter(msw);
            bw.Write(value);
            bw.Close();
            msw.Close();
            MemoryStream msr = new MemoryStream(buf);
            BinaryReader br = new BinaryReader(msr);
            return br.ReadUInt64();
        }

        /// <summary>
        /// Converts a binary encoding to a double.
        /// </summary>
        /// <param name="value">The binary encoding</param>
        /// <returns>The double representation</returns>
        public static double BitsToFloat64(ulong value)
        {
            byte[] buf = new byte[8];
            MemoryStream msw = new MemoryStream(buf);
            BinaryWriter bw = new BinaryWriter(msw);
            bw.Write(value);
            bw.Close();
            msw.Close();
            MemoryStream msr = new MemoryStream(buf);
            BinaryReader br = new BinaryReader(msr);
            return br.ReadDouble();
        }

        /// <summary>
        /// Converts a binary encoding, given as an StdLogicVector with respect to a floating point format to its
        /// double representation.
        /// </summary>
        /// <param name="slv">The binary encoding</param>
        /// <param name="fmt">The floating point format to be assumed</param>
        /// <returns>The double representation</returns>
        public static double ToFloat(this StdLogicVector slv, FloatFormat fmt)
        {
            if (slv.Size != fmt.TotalWidth)
                throw new ArgumentException("Vector does not match specified floating point format");
            slv = slv.ProperValue;

            StdLogicVector mantissa = slv[fmt.FractionWidth - 1, 0];
            StdLogicVector exponent = slv[fmt.FractionWidth + fmt.ExponentWidth - 1, fmt.FractionWidth];
            StdLogic sign = slv[fmt.FractionWidth + fmt.ExponentWidth];

            int exp = (int)exponent.ULongValue - fmt.Bias;

            if (exponent.Equals(StdLogicVector._0s(fmt.ExponentWidth)))
            {
                // denormalized
                long mant = mantissa.LongValue;
                double result = (double)mant * Math.Pow(2.0, exp - 1);
                return result;
            }
            else if (exponent.Equals(StdLogicVector._1s(fmt.ExponentWidth)))
            {
                // Infinity / NaN
                if (mantissa.Equals(StdLogicVector._0s(fmt.FractionWidth)))
                {
                    // infinity
                    if (sign == '1')
                        return double.NegativeInfinity;
                    else
                        return double.PositiveInfinity;
                }
                else
                {
                    // NaN
                    return double.NaN;
                }
            }
            else
            {
                // normalized
                StdLogicVector number = StdLogicVector._1s(1).Concat(mantissa);
                ulong mant = number.ULongValue;
                double result = (double)mant * Math.Pow(2.0, exp - fmt.FractionWidth);
                if (sign == '1')
                    result = -result;
                return result;
            }
        }

        [TypeConversion(typeof(StdLogicVector), typeof(float))]
        public static float ToFloat(this StdLogicVector slv)
        {
            return (float)ToFloat(slv, FloatFormat.SingleFormat);
        }

        [TypeConversion(typeof(StdLogicVector), typeof(double))]
        public static double ToDouble(this StdLogicVector slv)
        {
            return ToFloat(slv, FloatFormat.DoubleFormat);
        }

        /// <summary>
        /// Converts a double value to its binary encoding, given a floating point format.
        /// </summary>
        /// <param name="value">The value to be encoded</param>
        /// <param name="fmt">The floating point format to be assumed</param>
        /// <returns>The binary encoding</returns>
        public static StdLogicVector ToSLV(this double value, FloatFormat fmt)
        {
            StdLogicVector sign;
            StdLogicVector exponent;
            StdLogicVector mantissa;
            if (double.IsInfinity(value))
            {
                sign = double.IsNegativeInfinity(value) ? (StdLogicVector)"1" : (StdLogicVector)"0";
                exponent = StdLogicVector._1s(fmt.ExponentWidth);
                mantissa = StdLogicVector._0s(fmt.FractionWidth);
            }
            else if (double.IsNaN(value))
            {
                sign = (StdLogicVector)"0";
                exponent = StdLogicVector._1s(fmt.ExponentWidth);
                mantissa = StdLogicVector._1s(fmt.FractionWidth);
            }
            else
            {
                sign = value < 0.0 ? (StdLogicVector)"1" : (StdLogicVector)"0";
                double absvalue = Math.Abs(value);
                int exp = 0;
                while (absvalue >= 2.0)
                {
                    absvalue *= 0.5;
                    exp++;
                }
                while (absvalue > 0.0 && absvalue < 1.0)
                {
                    absvalue *= 2.0;
                    exp--;
                }
                if (absvalue == 0.0)
                {
                    return StdLogicVector._0s(fmt.TotalWidth);
                }
                else if (exp <= -fmt.Bias)
                {
                    // denomalized
                    exponent = StdLogicVector._0s(fmt.ExponentWidth);
                    absvalue *= (double)(1L << (fmt.FractionWidth+1));
                    long mant = (long)absvalue;
                    mantissa = StdLogicVector.FromLong(mant, fmt.FractionWidth);
                }
                else
                {

                    absvalue -= 1.0;
                    absvalue *= (double)(1L << fmt.FractionWidth);
                    long mant = (long)absvalue;
                    mantissa = StdLogicVector.FromLong(mant, fmt.FractionWidth);
                    exponent = StdLogicVector.FromLong(exp + fmt.Bias, fmt.ExponentWidth);
                }
            }
            return sign.Concat(exponent.Concat(mantissa));
        }

        [TypeConversion(typeof(double), typeof(StdLogicVector))]
        public static StdLogicVector ToSLV(this double value)
        {
            return ToSLV(value, FloatFormat.DoubleFormat);
        }

        [TypeConversion(typeof(float), typeof(StdLogicVector))]
        public static StdLogicVector ToSLV(this float value)
        {
            return ToSLV(value, FloatFormat.SingleFormat);
        }

        public static FloatFormat GetFloatFormat(this TypeDescriptor td)
        {
            if (td.CILType.Equals(typeof(float)))
                return FloatFormat.SingleFormat;
            else if (td.CILType.Equals(typeof(double)))
                return FloatFormat.DoubleFormat;
            else
                throw new NotSupportedException("Type " + td + " is not a floating-point type");
        }
    }
}

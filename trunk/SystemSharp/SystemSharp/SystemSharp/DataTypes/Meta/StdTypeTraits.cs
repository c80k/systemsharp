/**
 * Copyright 2014 Christian Köllner
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
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SystemSharp.DataTypes.Meta
{
    class SByteTraits: INonParamatricTypeTraits<sbyte>
    {
        public static readonly SByteTraits Instance = new SByteTraits();

        public bool IsZero(sbyte value)
        {
            return value == 0;
        }

        public bool IsOne(sbyte value)
        {
            return value == 1;
        }

        public bool IsMinusOne(sbyte value)
        {
            return value == -1;
        }

        public EAlgebraicTypeProperties Properties
        {
            get 
            { 
                return EAlgebraicTypeProperties.AdditionIsCommutative | 
                    EAlgebraicTypeProperties.MultiplicationIsCommutative; 
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public sbyte GetZero()
        {
            return 0;
        }

        public sbyte GetOne()
        {
            return 1;
        }
    }

    class ByteTraits : INonParamatricTypeTraits<byte>
    {
        public static readonly ByteTraits Instance = new ByteTraits();

        public bool IsZero(byte value)
        {
            return value == 0;
        }

        public bool IsOne(byte value)
        {
            return value == 1;
        }

        public bool IsMinusOne(byte value)
        {
            return false;
        }

        public EAlgebraicTypeProperties Properties
        {
            get
            {
                return EAlgebraicTypeProperties.AdditionIsCommutative |
                    EAlgebraicTypeProperties.MultiplicationIsCommutative;
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public byte GetZero()
        {
            return 0;
        }

        public byte GetOne()
        {
            return 1;
        }
    }

    class ShortTraits : INonParamatricTypeTraits<short>
    {
        public static readonly ShortTraits Instance = new ShortTraits();

        public bool IsZero(short value)
        {
            return value == 0;
        }

        public bool IsOne(short value)
        {
            return value == 1;
        }

        public bool IsMinusOne(short value)
        {
            return value == -1;
        }

        public EAlgebraicTypeProperties Properties
        {
            get
            {
                return EAlgebraicTypeProperties.AdditionIsCommutative |
                    EAlgebraicTypeProperties.MultiplicationIsCommutative;
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public short GetZero()
        {
            return 0;
        }

        public short GetOne()
        {
            return 1;
        }
    }

    class UShortTraits : INonParamatricTypeTraits<ushort>
    {
        public static readonly UShortTraits Instance = new UShortTraits();

        public bool IsZero(ushort value)
        {
            return value == 0;
        }

        public bool IsOne(ushort value)
        {
            return value == 1;
        }

        public bool IsMinusOne(ushort value)
        {
            return false;
        }

        public EAlgebraicTypeProperties Properties
        {
            get
            {
                return EAlgebraicTypeProperties.AdditionIsCommutative |
                    EAlgebraicTypeProperties.MultiplicationIsCommutative;
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public ushort GetZero()
        {
            return 0;
        }

        public ushort GetOne()
        {
            return 1;
        }
    }

    class IntTraits : INonParamatricTypeTraits<int>
    {
        public static readonly IntTraits Instance = new IntTraits();

        public bool IsZero(int value)
        {
            return value == 0;
        }

        public bool IsOne(int value)
        {
            return value == 1;
        }

        public bool IsMinusOne(int value)
        {
            return value == -1;
        }

        public EAlgebraicTypeProperties Properties
        {
            get
            {
                return EAlgebraicTypeProperties.AdditionIsCommutative |
                    EAlgebraicTypeProperties.MultiplicationIsCommutative;
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public int GetZero()
        {
            return 0;
        }

        public int GetOne()
        {
            return 1;
        }
    }

    class UIntTraits : INonParamatricTypeTraits<uint>
    {
        public static readonly UIntTraits Instance = new UIntTraits();

        public bool IsZero(uint value)
        {
            return value == 0;
        }

        public bool IsOne(uint value)
        {
            return value == 1;
        }

        public bool IsMinusOne(uint value)
        {
            return false;
        }

        public EAlgebraicTypeProperties Properties
        {
            get
            {
                return EAlgebraicTypeProperties.AdditionIsCommutative |
                    EAlgebraicTypeProperties.MultiplicationIsCommutative;
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public uint GetZero()
        {
            return 0;
        }

        public uint GetOne()
        {
            return 1;
        }
    }

    class LongTraits : INonParamatricTypeTraits<long>
    {
        public static readonly LongTraits Instance = new LongTraits();

        public bool IsZero(long value)
        {
            return value == 0;
        }

        public bool IsOne(long value)
        {
            return value == 1;
        }

        public bool IsMinusOne(long value)
        {
            return value == -1;
        }

        public EAlgebraicTypeProperties Properties
        {
            get
            {
                return EAlgebraicTypeProperties.AdditionIsCommutative |
                    EAlgebraicTypeProperties.MultiplicationIsCommutative;
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public long GetZero()
        {
            return 0;
        }

        public long GetOne()
        {
            return 1;
        }
    }

    class ULongTraits : INonParamatricTypeTraits<ulong>
    {
        public static readonly ULongTraits Instance = new ULongTraits();

        public bool IsZero(ulong value)
        {
            return value == 0;
        }

        public bool IsOne(ulong value)
        {
            return value == 1;
        }

        public bool IsMinusOne(ulong value)
        {
            return false;
        }

        public EAlgebraicTypeProperties Properties
        {
            get
            {
                return EAlgebraicTypeProperties.AdditionIsCommutative |
                    EAlgebraicTypeProperties.MultiplicationIsCommutative;
            }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public ulong GetZero()
        {
            return 0;
        }

        public ulong GetOne()
        {
            return 1;
        }
    }

    class FloatTraits : INonParamatricTypeTraits<float>
    {
        public static readonly FloatTraits Instance = new FloatTraits();

        public bool IsZero(float value)
        {
            return value == 0.0f;
        }

        public bool IsOne(float value)
        {
            return value == 1.0f;
        }

        public bool IsMinusOne(float value)
        {
            return value == -1.0f;
        }

        public EAlgebraicTypeProperties Properties
        {
            get { return EAlgebraicTypeProperties.None; }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public float GetZero()
        {
            return 0;
        }

        public float GetOne()
        {
            return 1;
        }
    }

    class DoubleTraits : INonParamatricTypeTraits<double>
    {
        public static readonly DoubleTraits Instance = new DoubleTraits();

        public bool IsZero(double value)
        {
            return value == 0.0;
        }

        public bool IsOne(double value)
        {
            return value == 1.0;
        }

        public bool IsMinusOne(double value)
        {
            return value == -1.0;
        }

        public EAlgebraicTypeProperties Properties
        {
            get { return EAlgebraicTypeProperties.None; }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public double GetZero()
        {
            return 0;
        }

        public double GetOne()
        {
            return 1;
        }
    }

    class DecimalTraits : INonParamatricTypeTraits<decimal>
    {
        public static readonly DecimalTraits Instance = new DecimalTraits();

        public bool IsZero(decimal value)
        {
            return value == 0m;
        }

        public bool IsOne(decimal value)
        {
            return value == 1m;
        }

        public bool IsMinusOne(decimal value)
        {
            return value == -1m;
        }

        public EAlgebraicTypeProperties Properties
        {
            get { return EAlgebraicTypeProperties.None; }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public decimal GetZero()
        {
            return 0;
        }

        public decimal GetOne()
        {
            return 1;
        }
    }

    class BigIntegerTraits : INonParamatricTypeTraits<BigInteger>
    {
        public static readonly BigIntegerTraits Instance = new BigIntegerTraits();

        public bool IsZero(BigInteger value)
        {
            return value == 0;
        }

        public bool IsOne(BigInteger value)
        {
            return value == 1;
        }

        public bool IsMinusOne(BigInteger value)
        {
            return value == -1;
        }

        public EAlgebraicTypeProperties Properties
        {
            get { return EAlgebraicTypeProperties.AdditionIsCommutative |
                EAlgebraicTypeProperties.MultiplicationIsCommutative; }
        }

        public bool OneExists
        {
            get { return true; }
        }

        public BigInteger GetZero()
        {
            return 0;
        }

        public BigInteger GetOne()
        {
            return 1;
        }
    }

    static class StdTypeTraits
    {
        public static INonParamatricTypeTraits<T> Get<T>()
        {
            if (typeof(T) == typeof(sbyte))
                return (INonParamatricTypeTraits<T>)SByteTraits.Instance;
            else if (typeof(T) == typeof(byte))
                return (INonParamatricTypeTraits<T>)ByteTraits.Instance;
            else if (typeof(T) == typeof(short))
                return (INonParamatricTypeTraits<T>)ShortTraits.Instance;
            else if (typeof(T) == typeof(ushort))
                return (INonParamatricTypeTraits<T>)UShortTraits.Instance;
            else if (typeof(T) == typeof(int))
                return (INonParamatricTypeTraits<T>)IntTraits.Instance;
            else if (typeof(T) == typeof(uint))
                return (INonParamatricTypeTraits<T>)UIntTraits.Instance;
            else if (typeof(T) == typeof(long))
                return (INonParamatricTypeTraits<T>)LongTraits.Instance;
            else if (typeof(T) == typeof(ulong))
                return (INonParamatricTypeTraits<T>)ULongTraits.Instance;
            else if (typeof(T) == typeof(decimal))
                return (INonParamatricTypeTraits<T>)DecimalTraits.Instance;
            else if (typeof(T) == typeof(float))
                return (INonParamatricTypeTraits<T>)FloatTraits.Instance;
            else if (typeof(T) == typeof(double))
                return (INonParamatricTypeTraits<T>)DoubleTraits.Instance;
            else if (typeof(T) == typeof(BigInteger))
                return (INonParamatricTypeTraits<T>)BigIntegerTraits.Instance;
            else
                return null;
        }
    }
}

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
using SystemSharp.Components;

namespace SystemSharp.Meta
{
    class NativeAlgebraicTypes
    {
        class NativeBool : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return true;
                else
                    return false;
            }
        }

        class NativeSByte : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (sbyte)1;
                else
                    return (sbyte)0;
            }
        }

        class NativeByte : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (byte)1;
                else
                    return (byte)0;
            }
        }

        class NativeShort : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (short)1;
                else
                    return (short)0;
            }
        }

        class NativeUShort : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (ushort)1;
                else
                    return (ushort)0;
            }
        }

        class NativeInt : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (int)1;
                else
                    return (int)0;
            }
        }

        class NativeUInt : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (uint)1;
                else
                    return (uint)0;
            }
        }

        class NativeLong : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (long)1;
                else    
                    return (long)0;
            }
        }

        class NativeULong : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return (ulong)1;
                else
                    return (ulong)0;
            }
        }

        class NativeDouble : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral))
                    return 1.0;
                else
                    return 0.0;
            }
        }

        class NativeFloat : AlgebraicTypeAttribute
        {
            public override object CreateInstance(ETypeCreationOptions options, object template)
            {
                if (options.HasFlag(ETypeCreationOptions.MultiplicativeNeutral) ||
                    options.HasFlag(ETypeCreationOptions.NonZero))
                    return 1.0f;
                else
                    return 0.0f;
            }
        }

        public static void RegisterAttributes()
        {
            AttributeInjector.Inject(typeof(bool), new NativeBool());
            AttributeInjector.Inject(typeof(sbyte), new NativeSByte());
            AttributeInjector.Inject(typeof(byte), new NativeByte());
            AttributeInjector.Inject(typeof(short), new NativeShort());
            AttributeInjector.Inject(typeof(ushort), new NativeUShort());
            AttributeInjector.Inject(typeof(int), new NativeInt());
            AttributeInjector.Inject(typeof(uint), new NativeUInt());
            AttributeInjector.Inject(typeof(long), new NativeLong());
            AttributeInjector.Inject(typeof(ulong), new NativeULong());
            AttributeInjector.Inject(typeof(double), new NativeDouble());
            AttributeInjector.Inject(typeof(float), new NativeFloat());
        }
    }
}

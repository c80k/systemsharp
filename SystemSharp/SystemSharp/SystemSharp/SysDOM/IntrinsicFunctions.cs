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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using SystemSharp.Algebraic;
using SystemSharp.Analysis;
using SystemSharp.Assembler;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;
using SystemSharp.SysDOM.Eval;

namespace SystemSharp.SysDOM
{
    /// <summary>
    /// Parameters of the <c>IntrinsicFunction.EAction.NewObject</c> op-code.
    /// </summary>
    public class NewObjectParams
    {
        /// <summary>
        /// Used constructor.
        /// </summary>
        public ConstructorInfo Constructor { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="ctor">constructor to use</param>
        public NewObjectParams(ConstructorInfo ctor)
        {
            Constructor = ctor;
        }

        /// <summary>
        /// Returns the CLI type of the constructed object.
        /// </summary>
        public Type Class 
        {
            get { return Constructor.DeclaringType; }
        }
    }

    /// <summary>
    /// Parameters of the <c>IntrinsicFunction.EAction.NewArray</c> op-code.
    /// </summary>
    public class ArrayParams
    {
        /// <summary>
        /// Type of an array element.
        /// </summary>
        public Type ElementType { get; private set; }

        /// <summary>
        /// Array elements, if the constructed array is static.
        /// </summary>
        public Expression[] Elements { get; private set; }

        private bool _isStatic;

        /// <summary>
        /// Whether the constructed array is static.
        /// </summary>
        /// <remarks>
        /// A static array is an array whose elements are determined during code analysis,
        /// at least symbolically. It is possible to revoke the static property of this instance
        /// by setting this property to <c>false</c>, but it is not possible to convert a
        /// non-static array to a static one.
        /// </remarks>
        public bool IsStatic
        {
            get { return _isStatic; }
            set
            {
                if (!_isStatic && value)
                    throw new InvalidOperationException("Only static => non-static transitions are allowed.");
                _isStatic = value;
            }
        }

        /// <summary>
        /// Constructs an instance for describing a dynamic array.
        /// </summary>
        /// <param name="elementType">type of array element</param>
        public ArrayParams(Type elementType)
        {
            ElementType = elementType;
        }

        /// <summary>
        /// Constructs an instance for describing a static array.
        /// </summary>
        /// <param name="elementType">type of array element</param>
        /// <param name="staticLength">statically determined array length</param>
        public ArrayParams(Type elementType, long staticLength)
        {
            ElementType = elementType;
            Elements = new Expression[staticLength];
            _isStatic = true;
        }
    }

    /// <summary>
    /// Parameters of the <c>IntrinsicFunction.EAction.Convert</c> op-code.
    /// </summary>
    public class CastParams
    {
        /// <summary>
        /// Source type of the conversion.
        /// </summary>
        public Type SourceType { get; private set; }

        /// <summary>
        /// Destination type of the conversion.
        /// </summary>
        public TypeDescriptor DestType { get; private set; }

        /// <summary>
        /// Whether the conversion has re-interpret semantics.
        /// </summary>
        public bool Reinterpret { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="sourceType">source type of the conversion</param>
        /// <param name="destType">destination type of the conversion</param>
        /// <param name="reinterpret"><c>true</c> if the conversion has re-interpret semantics</param>
        public CastParams(Type sourceType, TypeDescriptor destType, bool reinterpret)
        {
            SourceType = sourceType;
            DestType = destType;
            Reinterpret = reinterpret;
        }

        public override string ToString()
        {
            return Reinterpret ? 
                "reinterpret " + SourceType.Name + " as " + DestType.Name :
                SourceType.Name + " => " + DestType.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is CastParams)
            {
                CastParams parms = (CastParams)obj;
                return SourceType.Equals(parms.SourceType) &&
                    DestType.Equals(parms.DestType) &&
                    Reinterpret == parms.Reinterpret;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return (SourceType.GetHashCode() * 3) ^
                DestType.GetHashCode() ^
                Reinterpret.GetHashCode();
        }
    }

    /// <summary>
    /// Parameters of the <c>IntrinsicFunction.EAction.ReadPort</c> and 
    /// <c>IntrinsicFunction.EAction.WritePort</c> op-codes.
    /// </summary>
    public class PortParams
    {
        /// <summary>
        /// Underlying signal instance being accessed.
        /// </summary>
        public SignalBase Port { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="port">underlying signal instance being accessed</param>
        public PortParams(SignalBase port)
        {
            Port = port;
        }
    }

    /// <summary>
    /// Parameters of the <c>IntrinsicFunction.EAction.Wait</c> op-code.
    /// </summary>
    public class WaitParams
    {
        /// <summary>
        /// Kind of wait operation
        /// </summary>
        public enum EWaitKind
        {
            /// <summary>
            /// Wait for a specified amount of time.
            /// </summary>
            WaitFor,

            /// <summary>
            /// Wait until a specified predicate becomes satisfied.
            /// </summary>
            WaitUntil,

            /// <summary>
            /// Wait on an event.
            /// </summary>
            WaitOn
        }

        /// <summary>
        /// Kind of wait operation.
        /// </summary>
        public EWaitKind WaitKind { get; private set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="waitKind">the kind of wait operation</param>
        public WaitParams(EWaitKind waitKind)
        {
            WaitKind = waitKind;
        }
    }

    /// <summary>
    /// Parameters of the <c>IntrinsicFunction.EAction.ReadMem</c> and 
    /// <c>IntrinsicFunction.EAction.WriteMem</c> op-codes.
    /// </summary>
    public class MemParams
    {
        /// <summary>
        /// Accessed memory region .
        /// </summary>
        public MemoryRegion Region { get; private set; }

        /// <summary>
        /// Minimum accessed address.
        /// </summary>
        public ulong MinAddress { get; private set; }

        /// <summary>
        /// Maximum accessed address.
        /// </summary>
        public ulong MaxAddress { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="region">accessed memory region</param>
        /// <param name="minAddr">minimum accessed address</param>
        /// <param name="maxAddr">maximum accessed address</param>
        public MemParams(MemoryRegion region, ulong minAddr, ulong maxAddr)
        {
            Region = region;
            MinAddress = minAddr;
            MaxAddress = maxAddr;
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="region">accessed memory region</param>
        /// <param name="addr">accessed address</param>
        public MemParams(MemoryRegion region, ulong addr)
        {
            Region = region;
            MinAddress = addr;
            MaxAddress = addr;
        }
    }

    /// <summary>
    /// Parameters of the <c>IntrinsicFunction.EAction.Resize</c> op-code.
    /// </summary>
    public class ResizeParams
    {
        /// <summary>
        /// New integer width after resize.
        /// </summary>
        public int NewIntWidth { get; private set; }

        /// <summary>
        /// New fractional width after resize.
        /// </summary>
        public int NewFracWidth { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="newIntWidth">new integer width after resize</param>
        /// <param name="newFracWidth">new fractional width after resize</param>
        public ResizeParams(int newIntWidth, int newFracWidth)
        {
            NewIntWidth = newIntWidth;
            NewFracWidth = newFracWidth;
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="newWidth">new width after resize</param>
        public ResizeParams(int newWidth)
        {
            NewIntWidth = newWidth;
            NewFracWidth = int.MinValue;
        }
    }

    /// <summary>
    /// Describes a SysDOM-intrinsic function.
    /// </summary>
    public class IntrinsicFunction
    {
        /// <summary>
        /// Function op-code
        /// </summary>
        public enum EAction
        {
            /// <summary>
            /// Read an array element.
            /// </summary>
            GetArrayElement,

            /// <summary>
            /// Get the length of an array.
            /// </summary>
            GetArrayLength,

            /// <summary>
            /// Create a new object.
            /// </summary>
            NewObject,

            /// <summary>
            /// Create a new array.
            /// </summary>
            NewArray,

            /// <summary>
            /// Absolute value function.
            /// </summary>
            Abs,

            /// <summary>
            /// Sine
            /// </summary>
            Sin,

            /// <summary>
            /// Cosine
            /// </summary>
            Cos,

            /// <summary>
            /// Square-root
            /// </summary>
            Sqrt,

            /// <summary>
            /// Signum function
            /// </summary>
            Sign,

            /// <summary>
            /// Wait operation
            /// </summary>
            Wait,

            /// <summary>
            /// Type conversion
            /// </summary>
            Convert,

            /// <summary>
            /// Slice operation
            /// </summary>
            Slice,

            /// <summary>
            /// Index operation
            /// </summary>
            Index,

            /// <summary>
            /// Reference to intrinsic property
            /// </summary>
            PropertyRef,

            /// <summary>
            /// Get instance of simulation context.
            /// </summary>
            SimulationContext,

            /// <summary>
            /// Print diagnostic output message, no line break.
            /// </summary>
            Report,

            /// <summary>
            /// Print diagnostic output message line.
            /// </summary>
            ReportLine,

            /// <summary>
            /// Concatenate strings.
            /// </summary>
            StringConcat,

            /// <summary>
            /// Read from port.
            /// </summary>
            ReadPort,

            /// <summary>
            /// Write to port.
            /// </summary>
            WritePort,

            /// <summary>
            /// Read from memory.
            /// </summary>
            ReadMem,

            /// <summary>
            /// Write to memory.
            /// </summary>
            WriteMem,

            /// <summary>
            /// Resize integer/fixed-point number.
            /// </summary>
            Resize,

            /// <summary>
            /// Data-flow barrier
            /// </summary>
            Barrier,

            /// <summary>
            /// Create downward range.
            /// </summary>
            MkDownRange,

            /// <summary>
            /// Create upward range.
            /// </summary>
            MkUpRange,

            /// <summary>
            /// XIL op-code
            /// </summary>
            XILOpCode,

            /// <summary>
            /// Open file for reading.
            /// </summary>
            FileOpenRead,

            /// <summary>
            /// Open file for writing.
            /// </summary>
            FileOpenWrite,

            /// <summary>
            /// Close file.
            /// </summary>
            FileClose,

            /// <summary>
            /// Read from file.
            /// </summary>
            FileRead,

            /// <summary>
            /// Read line from file.
            /// </summary>
            FileReadLine,

            /// <summary>
            /// Write to file.
            /// </summary>
            FileWrite,

            /// <summary>
            /// Write line to file.
            /// </summary>
            FileWriteLine,

            /// <summary>
            /// Exit current process.
            /// </summary>
            ExitProcess,

            /// <summary>
            /// Set next entry state (async pattern).
            /// </summary>
            ProceedWithState,

            /// <summary>
            /// Fork new state machine (async pattern).
            /// </summary>
            Fork,

            /// <summary>
            /// Join state machine (async pattern).
            /// </summary>
            Join,

            /// <summary>
            /// Get state machine result (async pattern).
            /// </summary>
            GetAsyncResult,

            /// <summary>
            /// Select tuple item.
            /// </summary>
            TupleSelect
        }

        /// <summary>
        /// Intrinsic function op-code.
        /// </summary>
        public EAction Action { get; private set; }

        /// <summary>
        /// Function parameter.
        /// </summary>
        public object Parameter { get; private set; }

        /// <summary>
        /// Representing CLI method.
        /// </summary>
        public MethodBase MethodModel { get; internal set; }

        internal IntrinsicFunction(EAction action)
        {
            Action = action;
        }

        internal IntrinsicFunction(EAction action, object parameter)
        {
            Action = action;
            Parameter = parameter;
        }

        /// <summary>
        /// Returns the op-code name of this function.
        /// </summary>
        public string Name 
        {
            get { return Action.ToString(); }
        }
        
        public override string ToString()
        {
            string result = "intrinsic:" + Action.ToString();
            if (Parameter != null)
            {
                result += "[" + Parameter.ToString() + "]";
            }
            return result;
        }
    }

    /// <summary>
    /// This static class provides factory methods for intrinsic functions.
    /// </summary>
    public static class IntrinsicFunctions
    {
        private static FunctionSpec MakeFun(IntrinsicFunction ifun, TypeDescriptor returnType)
        {
            Contract.Requires<ArgumentNullException>(ifun != null);
            Contract.Requires<ArgumentNullException>(returnType != null);
            return new FunctionSpec(returnType)
            {
                IntrinsicRep = ifun
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of reading an array element.
        /// </summary>
        /// <param name="arrayRef">expression representing the array</param>
        /// <param name="index">expression representing the index</param>
        public static FunctionCall GetArrayElement(Expression arrayRef, Expression index)
        {
            TypeDescriptor elementType = arrayRef.ResultType.Element0Type;
            return new FunctionCall()
            {
                Callee = MakeFun(new IntrinsicFunction(
                    IntrinsicFunction.EAction.GetArrayElement,
                    new ArrayParams(elementType.CILType))
                    {
                        MethodModel = IntrinsicFunctionModels.GetArrayElementModel.Method
                    }, elementType),
                Arguments = new Expression[] { arrayRef, index },
                ResultType = elementType,
                SetResultTypeClass = EResultTypeClass.ObjectReference,
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of retrieving the length of an array.
        /// </summary>
        /// <param name="arrayRef">expression representing the array</param>
        public static FunctionCall GetArrayLength(Expression arrayRef)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.GetArrayLength),
                    typeof(int)),
                Arguments = new Expression[] { arrayRef },
                ResultType = typeof(int),
                SetResultTypeClass = EResultTypeClass.Integral
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of creating a new object.
        /// </summary>
        /// <param name="ctor">CLI constructor to call</param>
        /// <param name="args">expressions representing the constructor arguments</param>
        /// <returns></returns>
        public static FunctionCall NewObject(ConstructorInfo ctor, Expression[] args)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.NewObject,
                    new NewObjectParams(ctor)), ctor.DeclaringType),
                Arguments = args,
                ResultType = ctor.DeclaringType,
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of creating a new array.
        /// </summary>
        /// <param name="elementType">type of array element</param>
        /// <param name="numElements">expression representing the array length</param>
        /// <param name="sample">sample instance of the created array</param>
        /// <returns></returns>
        public static FunctionCall NewArray(Type elementType, Expression numElements, Array sample)
        {
            ArrayParams aparams;
            TypeDescriptor arrayType;
            if (sample != null)
            {
                long numElementsLong = sample.LongLength;
                aparams = new ArrayParams(elementType, numElementsLong);
                arrayType = TypeDescriptor.GetTypeOf(sample);
            }
            else
            {
                aparams = new ArrayParams(elementType);
                arrayType = elementType.MakeArrayType();
            }

            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.NewArray, aparams),
                    arrayType),
                Arguments = new Expression[] { numElements },
                ResultType = arrayType,
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of the sine function.
        /// </summary>
        /// <param name="x">expression representing the argument</param>
        public static FunctionCall Sin(Expression x)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Sin),
                    x.ResultType),
                Arguments = new Expression[] { x },
                ResultType = x.ResultType,
                SetResultTypeClass = EResultTypeClass.Algebraic
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of the cosine function.
        /// </summary>
        /// <param name="x">expression representing the argument</param>
        public static FunctionCall Cos(Expression x)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Cos),
                    x.ResultType),
                Arguments = new Expression[] { x },
                ResultType = x.ResultType,
                SetResultTypeClass = EResultTypeClass.Algebraic
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of the square-root function.
        /// </summary>
        /// <param name="x">expression representing the argument</param>
        public static FunctionCall Sqrt(Expression x)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Sqrt),
                    typeof(double)),
                Arguments = new Expression[] { x },
                ResultType = typeof(double),
                SetResultTypeClass = EResultTypeClass.Algebraic
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of the signum function.
        /// </summary>
        /// <param name="x">expression representing the argument</param>
        public static FunctionCall Sign(Expression x)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Sign),
                    x.ResultType),
                Arguments = new Expression[] { x },
                ResultType = x.ResultType,
                SetResultTypeClass = EResultTypeClass.Algebraic
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of a wait operation.
        /// </summary>
        /// <param name="waitKind">kind of wait operation</param>
        public static IntrinsicFunction Wait(WaitParams.EWaitKind waitKind)
        {
            return new IntrinsicFunction(IntrinsicFunction.EAction.Wait,
                    new WaitParams(waitKind));
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of reading from a port.
        /// </summary>
        /// <param name="port">underlying accessed signal instance</param>
        public static FunctionCall ReadPort(SignalBase port)
        {
            TypeDescriptor type = TypeDescriptor.GetTypeOf(port.InitialValueObject);
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.ReadPort,
                    new PortParams(port)), type),
                Arguments = new Expression[0],
                ResultType = type,
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of writing to a port.
        /// </summary>
        /// <param name="port">underlying accessed signal instance</param>
        public static IntrinsicFunction WritePort(SignalBase port)
        {
            return new IntrinsicFunction(IntrinsicFunction.EAction.WritePort,
                    new PortParams(port));
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of creating a downward range.
        /// </summary>
        /// <param name="hi">expression representing the upper range index</param>
        /// <param name="lo">expression representing the lower range index</param>
        public static FunctionCall MkDownRange(Expression hi, Expression lo)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.MkDownRange),
                    typeof(Range)),
                Arguments = new Expression[] { hi, lo },
                ResultType = typeof(Range),
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of creating an upward range.
        /// </summary>
        /// <param name="hi">expression representing the upper range index</param>
        /// <param name="lo">expression representing the lower range index</param>
        public static FunctionCall MkUpRange(Expression hi, Expression lo)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.MkUpRange),
                    typeof(Range)),
                Arguments = new Expression[] { hi, lo },
                ResultType = typeof(Range),
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of a string concatenation operation.
        /// </summary>
        /// <param name="exprs">expressions representing the strings to be concatenated, from left-most to right-most</param>
        public static FunctionCall StringConcat(Expression[] exprs)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.StringConcat, null),
                    typeof(string)),
                Arguments = exprs,
                ResultType = TypeDescriptor.MakeType(typeof(string)),
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of a type conversion.
        /// </summary>
        /// <param name="expr">expression representing the value to be converted</param>
        /// <param name="srcType">source type of conversion</param>
        /// <param name="dstType">destination type of conversion</param>
        /// <param name="reinterpret">whether the conversion has re-interpret semantics</param>
        public static Expression Cast(Expression expr, Type srcType, TypeDescriptor dstType, bool reinterpret = false)
        {
            if (expr != null && expr.ResultType.Equals(dstType))
                return expr;

            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Convert, 
                    new CastParams(srcType, dstType, reinterpret)), dstType),
                Arguments = new Expression[] { expr },
                ResultType = dstType,
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of a type conversion with parameters.
        /// </summary>
        /// <param name="exprs">expressions representing the value to be converted and additional conversion parameters</param>
        /// <param name="srcType">source type of conversion</param>
        /// <param name="dstType">destination type of conversion</param>
        /// <param name="reinterpret">whether the conversion has re-interpret semantics</param>
        public static FunctionCall Cast(Expression[] exprs, Type srcType, TypeDescriptor dstType, bool reinterpret = false)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Convert,
                    new CastParams(srcType, dstType, reinterpret)), dstType),
                Arguments = exprs,
                ResultType = dstType,
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of a fixed-point number resizing operation.
        /// </summary>
        /// <param name="expr">expression representing the value to be resized</param>
        /// <param name="newIntWidth">new integer width</param>
        /// <param name="newFracWidth">new fractional width</param>
        /// <param name="resultType">result type descriptor</param>
        public static FunctionCall Resize(Expression expr, int newIntWidth, int newFracWidth, TypeDescriptor resultType)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Resize,
                    new ResizeParams(newIntWidth, newFracWidth)), resultType),
                Arguments = new Expression[] { expr },
                ResultType = resultType,
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of an integral number resizing operation.
        /// </summary>
        /// <param name="expr">expression representing the value to be resized</param>
        /// <param name="newWidth">new integer width</param>
        /// <param name="resultType">result type descriptor</param>
        public static FunctionCall Resize(Expression expr, int newWidth, TypeDescriptor resultType)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Resize,
                    new ResizeParams(newWidth)), resultType),
                Arguments = new Expression[] { expr },
                ResultType = resultType,
                SetResultTypeClass = EResultTypeClass.ObjectReference
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of a bit vector indexed read operation.
        /// </summary>
        /// <param name="subj">expression representing the vector to be indexed</param>
        /// <param name="index">expression representing the accessed index</param>
        /// <param name="resultType">result type descriptor</param>
        public static FunctionCall Index(Expression subj, Expression index, TypeDescriptor resultType)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.Index),
                    resultType),
                Arguments = new Expression[] { subj, index },
                ResultType = resultType
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of executing a XIL instruction.
        /// </summary>
        /// <param name="xi">XIL instruction to execute</param>
        /// <param name="resultType">result type descriptor</param>
        /// <param name="args">expressions representing the instruction arguments</param>
        public static FunctionCall XILOpCode(XILInstr xi, TypeDescriptor resultType, params Expression[] args)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.XILOpCode, xi),
                    resultType),
                Arguments = args,
                ResultType = resultType
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionCall</c> representation of reading a tuple item.
        /// </summary>
        /// <param name="index">0-based index of accessed tuple element</param>
        /// <param name="resultType">result type descriptor</param>
        /// <param name="tup">expression representing the accessed tuple</param>
        public static FunctionCall TupleSelect(int index, TypeDescriptor resultType, Expression tup)
        {
            return new FunctionCall()
            {
                Callee = MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.TupleSelect, index),
                    resultType),
                Arguments = new Expression[] { tup },
                ResultType = resultType
            };
        }

        /// <summary>
        /// Constructs a <c>FunctionSpec</c> representation of outputting a diagnostic text message line.
        /// </summary>
        /// <param name="arg">expression representing the message to write</param>
        public static FunctionSpec ReportLine(Expression arg)
        {
            return MakeFun(
                    new IntrinsicFunction(IntrinsicFunction.EAction.ReportLine),
                    typeof(void));
        }

        internal static IntrinsicFunction ProceedWithState(ProceedWithStateInfo pi)
        {
            return new IntrinsicFunction(
                IntrinsicFunction.EAction.ProceedWithState,
                pi);
        }

        internal static IntrinsicFunction Fork(object task)
        {
            return new IntrinsicFunction(
                IntrinsicFunction.EAction.Fork,
                task);
        }

        internal static IntrinsicFunction Join(object task)
        {
            Contract.Requires(task != null);
            if (task == null)
                throw new ArgumentException("task == null");

            return new IntrinsicFunction(
                IntrinsicFunction.EAction.Join,
                task);
        }

        internal static IntrinsicFunction GetAsyncResult(object awaiter)
        {
            return new IntrinsicFunction(
                IntrinsicFunction.EAction.GetAsyncResult,
                awaiter);
        }
    }

    static class IntrinsicFunctionModels
    {
        public delegate object GetArrayElementFunc(Array array, int index);

        public static object GetArrayElement(Array array, int index)
        {
            return array.GetValue(index);
        }

        public static readonly GetArrayElementFunc GetArrayElementModel = GetArrayElement;
    }

#if false
    public static class CMathIntrinsicFunctions
    {
        private delegate FunctionCall UnaryFunctionCreator(Expression x);

        private static Dictionary<string, UnaryFunctionCreator> _unaryFunctions =
            new Dictionary<string, UnaryFunctionCreator>();

        static CMathIntrinsicFunctions()
        {
            _unaryFunctions["sin"] = IntrinsicFunctions.Sin;
            _unaryFunctions["cos"] = IntrinsicFunctions.Cos;
            _unaryFunctions["sqrt"] = IntrinsicFunctions.Sqrt;
            _unaryFunctions["sign"] = IntrinsicFunctions.Sign;
        }

        public static FunctionCall Create(string name, Expression[] args)
        {
            UnaryFunctionCreator ctor = _unaryFunctions[name];
            Debug.Assert(args.Length == 1);
            return ctor(args[0]);
        }
    }
#endif
}

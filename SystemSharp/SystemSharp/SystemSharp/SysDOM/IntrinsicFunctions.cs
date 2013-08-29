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
    public class NewObjectParams
    {
        public ConstructorInfo Constructor { get; private set; }

        public NewObjectParams(ConstructorInfo ctor)
        {
            Constructor = ctor;
        }

        public Type Class 
        {
            get
            {
                return Constructor.DeclaringType;
            }
        }
    }

    public class ArrayParams
    {
        public Type ElementType { get; private set; }
        public Expression[] Elements { get; private set; }

        private bool _isStatic;
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

        public bool IsInlined { get; set; }

        public ArrayParams(Type elementType)
        {
            ElementType = elementType;
        }

        public ArrayParams(Type elementType, long staticLength)
        {
            ElementType = elementType;
            Elements = new Expression[staticLength];
            _isStatic = true;
        }
    }

    public class CastParams
    {
        public Type SourceType { get; private set; }
        public TypeDescriptor DestType { get; private set; }
        public bool Reinterpret { get; private set; }

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

    public class PortParams
    {
        public SignalBase Port { get; private set; }

        public PortParams(SignalBase port)
        {
            Port = port;
        }
    }

    public class WaitParams
    {
        public enum EWaitKind
        {
            WaitFor,
            WaitUntil,
            WaitOn
        }

        public EWaitKind WaitKind { get; private set; }

        public WaitParams(EWaitKind waitKind)
        {
            WaitKind = waitKind;
        }
    }

    public class MemParams
    {
        public MemoryRegion Region { get; private set; }
        public ulong MinAddress { get; private set; }
        public ulong MaxAddress { get; private set; }

        public MemParams(MemoryRegion region, ulong minAddr, ulong maxAddr)
        {
            Region = region;
            MinAddress = minAddr;
            MaxAddress = maxAddr;
        }

        public MemParams(MemoryRegion region, ulong addr)
        {
            Region = region;
            MinAddress = addr;
            MaxAddress = addr;
        }
    }

    public class ResizeParams
    {
        public int NewIntWidth { get; private set; }
        public int NewFracWidth { get; private set; }

        public ResizeParams(int newIntWidth, int newFracWidth)
        {
            NewIntWidth = newIntWidth;
            NewFracWidth = newFracWidth;
        }

        public ResizeParams(int newWidth)
        {
            NewIntWidth = newWidth;
            NewFracWidth = int.MinValue;
        }
    }

    public class IntrinsicFunction
    {
        public enum EAction
        {
            GetArrayElement,
            GetArrayLength,
            NewObject,
            NewArray,
            Abs,
            Sin,
            Cos,
            Sqrt,
            Sign,
            Wait,
            Convert,
            Slice,
            Index,
            PropertyRef,
            SimulationContext,
            Report,
            ReportLine,
            StringConcat,
            ReadPort,
            WritePort,
            ReadMem,
            WriteMem,
            Resize,
            Barrier,
            MkDownRange,
            MkUpRange,
            XILOpCode,
            FileOpenRead,
            FileOpenWrite,
            FileClose,
            FileRead,
            FileReadLine,
            FileWrite,
            FileWriteLine,
            ExitProcess,
            ProceedWithState,
            Fork,
            Join,
            GetAsyncResult,
            TupleSelect
        }

        public EAction Action { get; private set; }
        public object Parameter { get; private set; }
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

        public string Name 
        {
            get
            {
                return Action.ToString();
            }
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

        public static IntrinsicFunction Wait(WaitParams.EWaitKind waitKind)
        {
            return new IntrinsicFunction(IntrinsicFunction.EAction.Wait,
                    new WaitParams(waitKind));
        }

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

        public static IntrinsicFunction WritePort(SignalBase port)
        {
            return new IntrinsicFunction(IntrinsicFunction.EAction.WritePort,
                    new PortParams(port));
        }

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

        public static IntrinsicFunction Fork(object task)
        {
            return new IntrinsicFunction(
                IntrinsicFunction.EAction.Fork,
                task);
        }

        public static IntrinsicFunction Join(object task)
        {
            Contract.Requires(task != null);
            if (task == null)
                throw new ArgumentException("task == null");

            return new IntrinsicFunction(
                IntrinsicFunction.EAction.Join,
                task);
        }

        public static IntrinsicFunction GetAsyncResult(object awaiter)
        {
            return new IntrinsicFunction(
                IntrinsicFunction.EAction.GetAsyncResult,
                awaiter);
        }
    }

    public static class IntrinsicFunctionModels
    {
        public delegate object GetArrayElementFunc(Array array, int index);

        public static object GetArrayElement(Array array, int index)
        {
            return array.GetValue(index);
        }

        public static readonly GetArrayElementFunc GetArrayElementModel = GetArrayElement;
    }

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
}

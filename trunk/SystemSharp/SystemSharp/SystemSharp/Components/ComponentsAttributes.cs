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
 * 
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SystemSharp.Algebraic;
using SystemSharp.Analysis;
using SystemSharp.Analysis.Msil;
using SystemSharp.Common;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;

namespace SystemSharp.Components
{
    /// <summary>
    /// If this attribute is attached to an interface, struct or class, it instructs the model analysis to treat any field, property or
    /// local variable of that particular type in a special way when found inside a component.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public abstract class RewriteDeclaration : Attribute
    {
        /// <summary>
        /// Implements the special treatment of a particular field or property.
        /// </summary>
        /// <param name="container">descriptor of containing component</param>
        /// <param name="declSite">found field or property</param>
        public virtual void ImplementDeclaration(DescriptorBase container, MemberInfo declSite)
        {
            throw new NotSupportedException("Declaration as member is not allowed");
        }

        /// <summary>
        /// Implements the special treatment of a particular local variable.
        /// </summary>
        /// <param name="lvi">information on local variable</param>
        /// <param name="decomp">decompiler instance</param>
        public virtual void ImplementDeclaration(LocalVariableInfo lvi, IDecompiler decomp)
        {
            throw new NotSupportedException("Declaration as local variable is not allowed");
        }
    }

    /// <summary>
    /// If this attribute is attached to an interface, struct or class, it instructs the model analysis to treat any property
    /// of that particular type in a special way when found inside a component.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public abstract class RewritePropertyDeclaration : RewriteDeclaration
    {
        /// <summary>
        /// Implements the special treatment of a particular field or property.
        /// </summary>
        /// <param name="container">descriptor of containing component</param>
        /// <param name="declSite">found property</param>
        public abstract void ImplementDeclaration(DescriptorBase container, PropertyInfo declSite);

        public sealed override void ImplementDeclaration(DescriptorBase container, MemberInfo declSite)
        {
            ImplementDeclaration(container, (PropertyInfo)declSite);
        }
    }

    /// <summary>
    /// If this attribute is attached to any interface, struct or class, it instructs the model analysis to treat any property of
    /// that particular type as a port declaration when found inside a component.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class MapToPort : RewritePropertyDeclaration
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="direction">data direction</param>
        public MapToPort(EPortDirection direction)
        {
            Direction = direction;
        }

        /// <summary>
        /// Data direction
        /// </summary>
        public EPortDirection Direction { get; private set; }

        /// <summary>
        /// Port descriptor
        /// </summary>
        public PortDescriptor Descriptor { get; private set; }

        /// <summary>
        /// Reference to bound signal
        /// </summary>
        public SignalRef SignalReference { get; private set; }

        private void CreateChildPDs(PortDescriptor pd, SignalDescriptor sd)
        {
            foreach (SignalDescriptor sdc in sd.GetSignals())
            {
                PortDescriptor pdc = new PortDescriptor(
                    pd.DeclarationSite,
                    sdc.SignalInstance.Descriptor,
                    sdc.SignalInstance.ElementType,
                    Direction);
                pd.AddChild(pdc, sdc.Name);
                CreateChildPDs(pdc, sdc);
            }
        }

        public override void ImplementDeclaration(DescriptorBase container, PropertyInfo declSite)
        {
            ComponentDescriptor cd = container as ComponentDescriptor;
            if (cd == null)
                throw new ArgumentException("Cannot implement property " + declSite.Name + " as port because it is not declared inside a component!");

            Channel boundChannel = (Channel)declSite.GetValue(cd.Instance, new object[0]);

            // Ignore unbound ports
            if (boundChannel == null)
            {
                AttributeInjector.InjectOnce(
                    declSite.GetGetMethod(true), cd.Instance, new AssumeNotCalled());
                return;
            }

            if (boundChannel is SignalBase)
            {
                SignalBase boundSignal = (SignalBase)boundChannel;
                SignalDescriptor boundSignalDesc = boundSignal.Descriptor;
                TypeDescriptor elType = boundSignalDesc.ElementType;
                Descriptor = new PortDescriptor(
                    declSite,
                    boundSignalDesc,
                    elType,
                    Direction);
                CreateChildPDs(Descriptor, boundSignalDesc);
                container.AddChild(Descriptor, Descriptor.DeclarationSite.Name);
                IPackageOrComponentDescriptor pcd = container as IPackageOrComponentDescriptor;
                if (pcd != null && elType.Package != null)
                    pcd.AddDependency(elType.Package);

                IPortDescriptor port = cd.FindPort(declSite.Name);

                // Accesses to port-like properties are statically evaluated
                AttributeInjector.InjectOnce(
                    declSite.GetGetMethod(true),
                    cd.Instance,
                    new StaticEvaluation(x => new SignalRef(port, SignalRef.EReferencedProperty.Instance)));
            }
            else
            {
                throw new NotImplementedException("Non-signal ports are not yet supported");
            }
        }
    }

    /// <summary>
    /// If this attribute is attached to any method or constructor, it instructs the decompiler to treat any call to that
    /// method or constructor in a special way.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = true)]
    public abstract class RewriteCall : Attribute
    {
        /// <summary>
        /// Implements the special treatment of a method call.
        /// </summary>
        /// <param name="decompilee">descriptor of code being decompiled</param>
        /// <param name="callee">called method or constructor</param>
        /// <param name="args">arguments</param>
        /// <param name="stack">decompiler context</param>
        /// <param name="builder">algorithm builder</param>
        /// <returns></returns>
        public abstract bool Rewrite(
            CodeDescriptor decompilee,
            MethodBase callee,
            StackElement[] args,
            IDecompiler stack,
            IFunctionBuilder builder);
    }

    /// <summary>
    /// If this attribute is attached to a method, it indicates that the method represents a special signal property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class SignalProperty : RewriteCall, IDoNotAnalyze
    {
        public SignalRef.EReferencedProperty Prop { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="prop">kind of property being represented</param>
        public SignalProperty(SignalRef.EReferencedProperty prop)
        {
            Prop = prop;
        }

        public override bool Rewrite(
            CodeDescriptor decompilee,
            MethodBase callee,
            StackElement[] args,
            IDecompiler stack,
            IFunctionBuilder builder)
        {
            if (args.Length == 0)
                throw new InvalidOperationException("The attribute SignalProperty was applied to the wrong method");

            LiteralReference refExpr = args[0].Expr as LiteralReference;
            if (refExpr == null)
                throw new InvalidOperationException("Unable to resolve port/signal reference expression");

            ISignal sigSample = args[0].Sample as ISignal;

            SignalRef sigRef = null;
            LambdaLiteralVisitor llv = new LambdaLiteralVisitor()
            {
                OnVisitConstant = x =>
                {
                    sigRef = ((ISignal)x.ConstantValue).ToSignalRef(Prop);
                },
                OnVisitSignalRef = x =>
                {
                    sigRef = new SignalRef(x.Desc, Prop, x.Indices, x.IndexSample, x.IsStaticIndex);
                },
                OnVisitVariable = x =>
                {
                    SignalArgumentDescriptor desc = decompilee.GetSignalArguments().Where(y => y.Name.Equals(x.Name)).Single();
                    sigRef = new SignalRef(desc, Prop);
                },
                OnVisitFieldRef = x =>
                {
                    sigRef = ((ISignal)x.FieldDesc.ConstantValue).ToSignalRef(Prop);
                },
                OnVisitThisRef = x => { throw new InvalidOperationException(); },
                OnVisitArrayRef = x => { throw new InvalidOperationException(); }
            };
            refExpr.ReferencedObject.Accept(llv);

            ParameterInfo[] pis = callee.GetParameters();
            switch (Prop)
            {
                case SignalRef.EReferencedProperty.ChangedEvent:
                case SignalRef.EReferencedProperty.Cur:
                case SignalRef.EReferencedProperty.FallingEdge:
                case SignalRef.EReferencedProperty.Pre:
                case SignalRef.EReferencedProperty.RisingEdge:
                    //if (pis.Length != 0 || callee.IsStatic)
                    //    throw new InvalidOperationException("The attribute SignalProperty was applied to the wrong method.");

                    switch (Prop)
                    {
                        case SignalRef.EReferencedProperty.Cur:
                        case SignalRef.EReferencedProperty.Pre:
                            stack.Push(
                                sigRef,
                                sigSample != null ? sigSample.InitialValueObject : null);
                            break;

                        case SignalRef.EReferencedProperty.ChangedEvent:
                            stack.Push(
                                sigRef,
                                null);
                            break;

                        case SignalRef.EReferencedProperty.FallingEdge:
                        case SignalRef.EReferencedProperty.RisingEdge:
                            stack.Push(
                                sigRef,
                                false);
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                    break;

                case SignalRef.EReferencedProperty.Next:
                    if (pis.Length != 1 || callee.IsStatic)
                        throw new InvalidOperationException("The attribute SignalProperty was applied to the wrong method.");

                    builder.Store(sigRef, args[1].Expr);
                    decompilee.AddDrivenSignal(sigRef.Desc);
                    break;

                default:
                    throw new NotImplementedException();
            }
            return true;
        }
    }

    /// <summary>
    /// If this attribute is attached to an interface or class, it indicates that a field of that particular type represents a signal
    /// instance.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class SignalField : RewriteFieldAccess
    {
        public override void RewriteRead(CodeDescriptor decompilee, FieldInfo field, object instance, IDecompiler stack, IFunctionBuilder builder)
        {
            SignalBase sigInst = (SignalBase)field.GetValue(instance);
            SignalRef sigRef = new SignalRef(sigInst.Descriptor, SignalRef.EReferencedProperty.Instance);
            stack.Push(sigRef, sigInst);
        }

        public override void RewriteWrite(CodeDescriptor decompilee, FieldInfo field, object instance, StackElement value, IDecompiler stack, IFunctionBuilder builder)
        {
            throw new InvalidOperationException("Writing to signal-typed fields is not allowed at runtime");
        }
    }

    /// <summary>
    /// This attribute is attached to getter methods of indexer properties of array-typed signals.
    /// It takes care for the decompilation of such method calls, such that they get properly represented
    /// inside the SysDOM representation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class SignalIndexer : RewriteCall
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public SignalIndexer()
        {
        }

        public override bool Rewrite(
            CodeDescriptor decompilee,
            MethodBase callee,
            StackElement[] args,
            IDecompiler stack,
            IFunctionBuilder builder)
        {
            if (args.Length < 2)
                throw new InvalidOperationException("The attribute SignalIndexer was applied to the wrong method");

            LiteralReference refExpr = args[0].Expr as LiteralReference;
            if (refExpr == null)
                throw new InvalidOperationException("Unable to resolve port/signal reference expression");

            DimSpec[] dimSamples = new DimSpec[args.Length - 1];
            Expression[] indices = args.Skip(1).Select(arg => arg.Expr).ToArray();
            bool isStatic = true;
            for (int i = 1; i < args.Length; i++)
            {
                var convidx = TypeConversions.ConvertValue(args[i].Sample, typeof(int));
                if (convidx != null)
                {
                    dimSamples[i - 1] = (int)convidx;
                }
                else if (args[i].Sample is Range)
                {
                    dimSamples[i - 1] = (Range)args[i].Sample;
                }
                else
                {
                    dimSamples = null;
                    break;
                }

                // EVariability.LocalVariable is not static as well, since variables
                // inside for loops will have that variability.
                if (args[i].Variability != EVariability.Constant)
                    isStatic = false;
            }
            IndexSpec indexSpec = null;
            indexSpec = new IndexSpec(dimSamples);

            SignalRef sigRef = null;
            LambdaLiteralVisitor llv = new LambdaLiteralVisitor()
            {
                OnVisitConstant = x =>
                {
                    sigRef = new SignalRef(
                        ((SignalBase)x.ConstantValue).Descriptor,
                        SignalRef.EReferencedProperty.Instance,
                        new Expression[][] { indices }, indexSpec, isStatic);
                },
                OnVisitFieldRef = x => { throw new InvalidOperationException(); },
                OnVisitSignalRef = x =>
                {
                    sigRef = new SignalRef(
                        x.Desc,
                        x.Prop,
                        x.Indices.Concat(new Expression[][] { indices }),
                        indexSpec.ApplyTo(x.IndexSample),
                        x.IsStaticIndex && isStatic);
                },
                OnVisitVariable = x => { throw new InvalidOperationException(); },
                OnVisitThisRef = x => { throw new InvalidOperationException(); }
            };
            refExpr.ReferencedObject.Accept(llv);

            object rsample = null;

            Type[] argTypes = args
                .Skip(1)
                .Select(a => a.Expr.ResultType.CILType)
                .ToArray();
            object[] argSamples = args
                .Select(a => a.Sample)
                .ToArray();
            MethodInfo indexerSampleMethod = callee.DeclaringType.GetMethod(
                "GetIndexerSample",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                argTypes,
                null);
            if (indexerSampleMethod != null && argSamples.All(a => a != null))
            {
                rsample = indexerSampleMethod.Invoke(argSamples);
            }
            else
            {
                try
                {
                    rsample = callee.Invoke(args.Select(x => x.Sample).ToArray());
                }
                catch (TargetInvocationException)
                {
                }
            }
            stack.Push(sigRef, rsample);
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class TypeConversion : RewriteCall, IDoNotAnalyze, ISideEffectFree
    {
        public Type SourceType { get; private set; }
        public Type DestType { get; private set; }
        public bool Reinterpret { get; private set; }

        public TypeConversion(Type sourceType, Type destType, bool reinterpret = false)
        {
            SourceType = sourceType;
            DestType = destType;
            Reinterpret = reinterpret;
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            Type returnType;
            bool flag = callee.IsFunction(out returnType);
            Debug.Assert(flag);
            var atype = args[0].Expr.ResultType.CILType;
            if (atype.IsPointer)
                atype = atype.GetElementType();
            Debug.Assert(atype.Equals(SourceType));
            object[] outArgs;
            object rsample;
            stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out rsample);
            TypeDescriptor rtype;
            if (rsample != null)
                rtype = TypeDescriptor.GetTypeOf(rsample);
            else
                rtype = returnType;
            Debug.Assert(rtype.CILType.Equals(DestType));
            Expression[] eargs = args.Select(arg => arg.Expr).ToArray();
            var fcall = IntrinsicFunctions.Cast(eargs, SourceType, rtype, Reinterpret);
            stack.Push(fcall, rsample);
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MapToIntrinsicFunction : RewriteCall, IDoNotAnalyze
    {
        public IntrinsicFunction.EAction Kind { get; private set; }
        public object Parameter { get; private set; }
        public bool SkipFirstArg { get; private set; }

        public MapToIntrinsicFunction(IntrinsicFunction.EAction kind)
        {
            Kind = kind;
            if (kind == IntrinsicFunction.EAction.Slice)
                throw new ArgumentException("Use MapToSlice instead");
        }

        public MapToIntrinsicFunction(IntrinsicFunction.EAction kind, object param)
        {
            Kind = kind;
            Parameter = param;
            if (kind == IntrinsicFunction.EAction.Slice)
                throw new ArgumentException("Use MapToSlice instead");
        }

        public MapToIntrinsicFunction(IntrinsicFunction.EAction kind, bool skipFirstArg)
        {
            Kind = kind;
            SkipFirstArg = skipFirstArg;
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            IntrinsicFunction ifun = new IntrinsicFunction(Kind, Parameter)
            {
                MethodModel = callee
            };

            int skip = SkipFirstArg ? 1 : 0;
            Expression[] eargs = args.Skip(skip).Select(arg => arg.Expr).ToArray();
            Type returnType;
            if (!callee.IsFunctionOrCtor(out returnType))
            {
                FunctionSpec fspec = new FunctionSpec(typeof(void))
                {
                    CILRep = callee,
                    IntrinsicRep = ifun
                };
                builder.Call(fspec, eargs);
            }
            else
            {
                object[] outArgs;
                object rsample = null;
                if (callee is MethodInfo &&
                    !callee.HasCustomOrInjectedAttribute<IDoNotCallOnDecompilation>())
                {
                    stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out rsample);
                }
                TypeDescriptor rtype;
                if (rsample != null)
                    rtype = TypeDescriptor.GetTypeOf(rsample);
                else
                    rtype = returnType;
                FunctionSpec fspec = new FunctionSpec(rtype)
                {
                    CILRep = callee,
                    IntrinsicRep = ifun
                };
                FunctionCall fcall = new FunctionCall()
                {
                    Callee = fspec,
                    Arguments = eargs,
                    ResultType = rtype,
                    SetResultTypeClass = EResultTypeClass.ObjectReference
                };
                stack.Push(fcall, rsample);
            }
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MapToSlice : RewriteCall, IDoNotAnalyze
    {
        public MapToSlice()
        {
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            object[] outArgs;
            object rsample = null;
            if (callee is MethodInfo &&
                !callee.HasCustomOrInjectedAttribute<IDoNotCallOnDecompilation>())
            {
                stack.TryGetReturnValueSample((MethodInfo)callee, args, out outArgs, out rsample);
            }
            Type returnType;
            callee.IsFunctionOrCtor(out returnType);
            TypeDescriptor rtype;
            if (rsample != null)
                rtype = TypeDescriptor.GetTypeOf(rsample);
            else
                rtype = returnType;

            IntrinsicFunction ifun;
            FunctionCall fcall;
            if (args[1].Variability == EVariability.Constant &&
                args[2].Variability == EVariability.Constant)
            {
                // constant arguments case
                int first = (int)TypeConversions.ConvertValue(args[1].Sample, typeof(int));
                int second = (int)TypeConversions.ConvertValue(args[2].Sample, typeof(int));
                var range = new Range(first, second, EDimDirection.Downto);
                ifun = new IntrinsicFunction(IntrinsicFunction.EAction.Slice, range)
                {
                    MethodModel = callee
                };
                var fspec = new FunctionSpec(rtype)
                {
                    CILRep = callee,
                    IntrinsicRep = ifun
                };
                fcall = new FunctionCall()
                {
                    Callee = fspec,
                    Arguments = new Expression[] { args[0].Expr },
                    ResultType = rtype,
                    SetResultTypeClass = EResultTypeClass.ObjectReference
                };
            }
            else
            {
                ifun = new IntrinsicFunction(IntrinsicFunction.EAction.Slice)
                {
                    MethodModel = callee
                };
                var fspec = new FunctionSpec(rtype)
                {
                    CILRep = callee,
                    IntrinsicRep = ifun
                };
                fcall = new FunctionCall()
                {
                    Callee = fspec,
                    Arguments = args.Select(arg => arg.Expr).ToArray(),
                    ResultType = rtype,
                    SetResultTypeClass = EResultTypeClass.ObjectReference
                };
            }
            stack.Push(fcall, rsample);
            return true;
        }
    }

    /// <summary>
    /// This attribute declares a method to be suitable for static evaluation.
    /// </summary>
    /// <remarks>
    /// Static evaluation means that the method is called by the System# framework during model analysis and that the return value is used directly instead
    /// of generating a call to the method.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = true)]
    public class StaticEvaluation : RewriteCall, ISideEffectFree
    {
        public StaticEvaluation()
        {
            ResultGen = x => LiteralReference.CreateConstant(x);
        }

        public StaticEvaluation(Func<object, Expression> resultGen)
        {
            ResultGen = resultGen;
        }

        public Func<object, Expression> ResultGen { get; private set; }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            Type returnType;
            if (!callee.ReturnsSomething(out returnType))
                throw new InvalidOperationException("The StaticEvaluation attribute may only be applied to methods returning some result. Use IgnoreOnDecompilation instead.");

            object result = null;
            try
            {
                result = callee.Invoke(args.Select(arg => arg.Sample).ToArray());
            }
            catch (TargetInvocationException)
            {
            }
            Expression resultExpr = ResultGen(result);
            stack.Push(new StackElement(resultExpr, result, EVariability.Constant));
            return true;
        }
    }

    /// <summary>
    /// This attribute indicates that a method is never called during runtime. If the analysis engine finds a call to the method anyhow, 
    /// it will throw an exception.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AssumeNotCalled : RewriteCall, ISideEffectFree
    {
        public AssumeNotCalled()
        {
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            throw new InvalidOperationException("Method " + callee.ToString() + " was assumed not to be called at runtime. " +
                "However, a call to this method was found in " + decompilee.Method.ToString());
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class IgnoreOnDecompilation : RewriteCall, IDoNotCallOnDecompilation
    {
        private bool _call;

        public IgnoreOnDecompilation(bool invokeOnDecompilation = false)
        {
            _call = invokeOnDecompilation;
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            Type returnType;
            if (callee.ReturnsSomething(out returnType))
                throw new InvalidOperationException("The IgnoreOnDecompilation attribute may only be applied to methods returning a void result. Use StaticEvaluation instead.");

            if (_call)
            {
                callee.Invoke(args.Select(a => a.Sample).ToArray());
            }

            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class StaticEvaluationDoNotAnalyze : StaticEvaluation, IDoNotAnalyze
    {
    }

    public abstract class RewriteArgumentDeclaration : Attribute
    {
        public abstract IStorableLiteral ImplementDeclaration(CodeDescriptor container, object sample, ParameterInfo pi);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class SignalArgument : RewriteArgumentDeclaration
    {
        public override IStorableLiteral ImplementDeclaration(CodeDescriptor container, object sample, ParameterInfo pi)
        {
            var signal = sample as SignalBase;
            if (signal == null)
                throw new ArgumentException("Signal instance null");

            Type etype = pi.ParameterType;
            ArgumentDescriptor.EArgDirection flowDir;
            if (typeof(IInOutPort).IsAssignableFrom(etype))
                flowDir = ArgumentDescriptor.EArgDirection.InOut;
            else if (typeof(IInPort).IsAssignableFrom(etype))
                flowDir = ArgumentDescriptor.EArgDirection.In;
            else if (typeof(IOutPort).IsAssignableFrom(etype))
                flowDir = ArgumentDescriptor.EArgDirection.Out;
            else
                throw new NotImplementedException();

            var sref = new SignalRef(signal.Descriptor, SignalRef.EReferencedProperty.Instance);

            SignalArgumentDescriptor desc = new SignalArgumentDescriptor(
                sref,
                ArgumentDescriptor.EArgDirection.In,
                flowDir,
                EVariability.Constant,
                pi.Position);
            container.AddChild(desc, sref.Name);

            return sref;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class RewriteMethodDefinition : Attribute
    {
        public abstract IDecompilationResult Rewrite(DesignContext ctx, CodeDescriptor code, object instance, object[] arguments);
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class MapToUnOp : RewriteCall
    {
        public UnOp.Kind Kind { get; private set; }

        public MapToUnOp(UnOp.Kind kind)
        {
            Kind = kind;
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            object[] vargs = args.Select(arg => arg.Sample).ToArray();
            Expression[] eargs = args.Select(arg => arg.Expr).ToArray();
            object sample = null;
            try
            {
                sample = callee.Invoke(vargs);
            }
            catch (Exception)
            {
            }
            Expression result = new UnOp()
            {
                Operation = Kind
            };
            Array.Copy(eargs, result.Children, 1);
            if (sample != null)
                result.ResultType = TypeDescriptor.GetTypeOf(sample);
            else
            {
                Type rtype;
                callee.IsFunction(out rtype);
                result.ResultType = (TypeDescriptor)rtype;
            }
            stack.Push(result, sample);
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class MapToBinOp : RewriteCall
    {
        public BinOp.Kind Kind { get; private set; }

        public MapToBinOp(BinOp.Kind kind)
        {
            Kind = kind;
        }

        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            object[] vargs = args.Select(arg => arg.Sample).ToArray();
            Expression[] eargs = args.Select(arg => arg.Expr).ToArray();
            object sample = null;
            try
            {
                sample = callee.Invoke(vargs);
            }
            catch (Exception)
            {
            }
            Expression result = new BinOp()
            {
                Operation = Kind
            };
            Array.Copy(eargs, result.Children, 2);
            if (sample != null)
                result.ResultType = TypeDescriptor.GetTypeOf(sample);
            else
            {
                Type rtype;
                callee.IsFunction(out rtype);
                result.ResultType = (TypeDescriptor)rtype;
            }
            stack.Push(result, sample);
            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class MapToTernOp : Attribute
    {
        public TernOp.Kind Kind { get; private set; }
        public int[] ArgPattern { get; private set; }

        public MapToTernOp(TernOp.Kind kind, int[] argPattern)
        {
            Kind = kind;
            ArgPattern = argPattern;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, Inherited = true)]
    public class MapToIntrinsicType : Attribute
    {
        public EIntrinsicTypes IntrinsicType { get; private set; }

        public MapToIntrinsicType(EIntrinsicTypes type)
        {
            IntrinsicType = type;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class AutoConversion : Attribute
    {
        public enum EAction
        {
            Include,
            Exclude
        }

        public EAction Action { get; private set; }
        public string TargetLanguage { get; private set; }

        public AutoConversion(EAction action)
        {
            Action = action;
        }

        public AutoConversion(EAction action, string targetLanguage)
        {
            Action = action;
            TargetLanguage = targetLanguage;
        }
    }

    public enum EPortUsage
    {
        Default,
        Clock,
        Reset,
        Operand,
        Result,
        State
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PortUsage : Attribute
    {
        public EPortUsage Usage { get; private set; }
        public string Domain { get; private set; }

        public PortUsage(EPortUsage usage)
        {
            Usage = usage;
        }

        public PortUsage(EPortUsage usage, string domain)
        {
            Usage = usage;
            Domain = domain;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple=false, Inherited=true)]
    public abstract class RewriteFieldAccess : Attribute
    {
        public abstract void RewriteRead(
            CodeDescriptor decompilee,
            FieldInfo field,
            object instance,
            IDecompiler stack,
            IFunctionBuilder builder);

        public abstract void RewriteWrite(
            CodeDescriptor decompilee,
            FieldInfo field,
            object instance,
            StackElement value,
            IDecompiler stack,
            IFunctionBuilder builder);
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ModelElement : RewriteFieldAccess
    {
        public override void RewriteRead(CodeDescriptor decompilee, FieldInfo field, object instance, IDecompiler stack, IFunctionBuilder builder)
        {
            object value = field.GetValue(instance);
            stack.Push(LiteralReference.CreateConstant(value),
                value);
        }

        public override void RewriteWrite(CodeDescriptor decompilee, FieldInfo field, object instance, StackElement value, IDecompiler stack, IFunctionBuilder builder)
        {
            throw new InvalidOperationException("Write access to model elements is not allowed after elaboration");
        }
    }

    public enum EComponentPurpose
    {
        SimulationAndSynthesis,
        SimulationOnly,
        SynthesisOnly
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ComponentPurpose : Attribute
    {
        public EComponentPurpose Purpose { get; private set; }

        public ComponentPurpose(EComponentPurpose purpose)
        {
            Purpose = purpose;
        }
    }

    public interface ISideEffectFree
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class SideEffectFree : Attribute, ISideEffectFree
    {
    }

    public interface IDoNotAnalyze
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class DoNotAnalyze : Attribute, IDoNotAnalyze
    {
    }

    public interface IDoNotCallOnDecompilation
    {
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public class DoNotCallOnDecompilation : Attribute, IDoNotCallOnDecompilation
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RedirectRewriteCall : RewriteCall
    {
        public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
        {
            object instance = args[0].Sample;
            if (instance == null)
                throw new InvalidOperationException("Failed to determine instance of " + args[0].Expr.ToString());
            RewriteCall rw = (RewriteCall)instance.GetType().GetCustomOrInjectedAttribute(typeof(RewriteCall));
            if (rw == null)
                throw new InvalidOperationException("Realization " + instance.GetType() + " does not implement RewriteCall attribute");
            return rw.Rewrite(decompilee, callee, args, stack, builder);
        }
    }

    public class SimulationOnly : Attribute, IOnDecompilation
    {
        public void OnDecompilation(MSILDecompilerTemplate decomp)
        {
            decomp.Decompilee.AddAttribute(this);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public abstract class RewriteAwait : Attribute
    {
        public abstract bool Rewrite(
            CodeDescriptor decompilee,
            Expression waitObject,
            IDecompiler stack,
            IFunctionBuilder builder);
    }

    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true)]
    public class DependentType : Attribute
    {
    }
}

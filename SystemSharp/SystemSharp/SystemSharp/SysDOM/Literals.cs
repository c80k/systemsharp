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
using System.Runtime.CompilerServices;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Collections;
using SystemSharp.Common;
using SystemSharp.Components;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM.Eval;

namespace SystemSharp.SysDOM
{
    /// <summary>
    /// Base interface of things that may be used a left-hand side of an assignment.
    /// </summary>
    public interface IStorable
    {
        string Name { get; }
        EStoreMode StoreMode { get; }
    }

    /// <summary>
    /// Assignment semantics
    /// </summary>
    public enum EStoreMode
    {
        /// <summary>
        /// Value assignment
        /// </summary>
        Assign,

        /// <summary>
        /// Value-to-signal transfer
        /// </summary>
        Transfer
    }

    /// <summary>
    /// Base interface for literals.
    /// </summary>
    public interface ILiteral
    {
        /// <summary>
        /// Accepts a literal visitor.
        /// </summary>
        /// <param name="visitor">visitor to accept</param>
        void Accept(ILiteralVisitor visitor);

        /// <summary>
        /// Returns the type descriptor of the data which is represented by this literal.
        /// </summary>
        TypeDescriptor Type { get; }
    }

    /// <summary>
    /// Base interface for literals which may be used as left-hand side of an assignment.
    /// </summary>
    public interface IStorableLiteral:
        ILiteral, IStorable, IEvaluable
    {
    }

    /// <summary>
    /// Abstract base class for literals.
    /// </summary>
    public abstract class Literal : 
        AttributedObject,
        IEvaluable, ILiteral
    {
        public abstract void Accept(ILiteralVisitor visitor);
        public abstract TypeDescriptor Type { get; }

        #region IEvaluable Member

        public abstract object Eval(IEvaluator eval);

        #endregion

        /// <summary>
        /// Implicitly converts a literal to an expression.
        /// </summary>
        public static implicit operator LiteralReference(Literal lit)
        {
            return new LiteralReference(lit, LiteralReference.EMode.Direct);
        }
    }

    /// <summary>
    /// A literal representing a constant value.
    /// </summary>
    public class Constant : Literal
    {
        /// <summary>
        /// The constant value.
        /// </summary>
        public object ConstantValue { get; private set; }

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="value">constant value to be represented</param>
        public Constant(object value)
        {
            ConstantValue = value;
        }

        public override TypeDescriptor Type
        {
            get
            {
                if (ConstantValue == null)
                    return TypeDescriptor.NullType;
                else
                    return TypeDescriptor.GetTypeOf(ConstantValue);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is Constant)
            {
                Constant other = (Constant)obj;
                return object.Equals(ConstantValue, other.ConstantValue);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            if (ConstantValue == null)
                return 0;
            else
                return ConstantValue.GetHashCode();
        }

        public override string ToString()
        {
            if (ConstantValue is string)
                return "\"" + ConstantValue + "\"";
            else if (ConstantValue is char)
                return "'" + ConstantValue + "'";
            else if (ConstantValue == null)
                return "<null>";
            else
                return ConstantValue.ToString();
        }

        public override void Accept(ILiteralVisitor visitor)
        {
            visitor.VisitConstant(this);
        }

        public override object Eval(IEvaluator eval)
        {
            return eval.EvalConstant(this);
        }

        public static object DefaultEval(Constant constant)
        {
            return constant.ConstantValue;
        }
    }

    /// <summary>
    /// A local variable.
    /// </summary>
    public class Variable :
        Literal, IStorableLiteral
    {
        /// <summary>
        /// Constructs a local variable.
        /// </summary>
        /// <param name="type">type of data stored in the variable</param>
        public Variable(TypeDescriptor type)
        {
            _type = type;
            LocalIndex = -1;
            LocalSubIndex = -1;
        }

        /// <summary>
        /// Gets or sets the variable name.
        /// </summary>
        public string Name { get; set; }

        private TypeDescriptor _type;
        public override TypeDescriptor Type
        {
            get { return _type; }
        }

        public EStoreMode StoreMode 
        { 
            get { return EStoreMode.Assign; } 
        }

        /// <summary>
        /// Adjusts thee variable type.
        /// </summary>
        /// <param name="type">new variable type, which must be a refinement of its former type</param>
        public void UpgradeType(TypeDescriptor type)
        {
            _type = type;
        }

        /// <summary>
        /// Gets or sets the variable index.
        /// </summary>
        public int LocalIndex { get; set; }

        /// <summary>
        /// Gets or sets the variable sub-index (as a result of SSA analysis)
        /// </summary>
        public int LocalSubIndex { get; set; }

        /// <summary>
        /// Gets or sets the variable's initial value.
        /// </summary>
        public object InitialValue { get; set; }

        /// <summary>
        /// Gets or sets an indication whether the variable is accessed by address.
        /// </summary>
        public bool IsAccessedByAddress { get; set; }

        public override string ToString()
        {
            return Name == null ? "<anonymous>" : Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is Variable)
            {
                Variable v = (Variable)obj;
                return Name.Equals(v.Name);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override void Accept(ILiteralVisitor visitor)
        {
            visitor.VisitVariable(this);
        }

        public override object Eval(IEvaluator eval)
        {
            return eval.EvalVariable(this);
        }
    }

    /// <summary>
    /// A field reference literal.
    /// </summary>
    public class FieldRef :
        Literal, IStorableLiteral
    {
        /// <summary>
        /// Referenced field descriptor.
        /// </summary>
        public FieldDescriptor FieldDesc { get; internal set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="field">referenced field descriptor</param>
        public FieldRef(FieldDescriptor field)
        {
            FieldDesc = field;
            CopyAttributes();
        }

        private void CopyAttributes()
        {
            foreach (var attr in FieldDesc.Attributes)
                AddAttribute(attr);
        }

        /// <summary>
        /// Returns the name of the field descriptor.
        /// </summary>
        public string Name
        {
            get { return FieldDesc.Name; }
        }

        public override TypeDescriptor Type
        {
            get { return FieldDesc.Type; }
        }

        public EStoreMode StoreMode
        {
            get { return EStoreMode.Assign; }
        }

        public override string ToString()
        {
            string result = "";
            result += Name;
            return result;
        }

        public override int GetHashCode()
        {
            return FieldDesc.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is FieldRef)
            {
                FieldRef fref = (FieldRef)obj;
                return FieldDesc.Equals(fref.FieldDesc);
            }
            else
            {
                return false;
            }
        }

        public override void Accept(ILiteralVisitor visitor)
        {
            visitor.VisitFieldRef(this);
        }

        public override object Eval(IEvaluator eval)
        {
            return eval.EvalFieldRef(this);
        }

        public static object DefaultEval(FieldRef fieldRef, IEvaluator eval)
        {
            return fieldRef.FieldDesc.Value;
        }
    }

    /// <summary>
    /// A literal which represents the currently active instance during non-static method execution.
    /// </summary>
    public class ThisRef : Literal
    {
        /// <summary>
        /// Declaring class.
        /// </summary>
        public Type ClassContext { get; private set; }

        /// <summary>
        /// Active instance.
        /// </summary>
        public object Instance { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="classContext">declaring class</param>
        /// <param name="instance">active instance</param>
        public ThisRef(Type classContext, object instance)
        {
            if (classContext == null)
                throw new ArgumentException("classContext null");

            if (instance == null)
                throw new ArgumentException("instance is null");

            ClassContext = classContext;
            Instance = instance;
        }

        /// <summary>
        /// Returns "this"
        /// </summary>
        public string Name
        {
            get { return "this"; }
        }

        public override TypeDescriptor Type
        {
            get { return ClassContext; }
        }

        public override string ToString()
        {
            return "this";
        }

        public override int GetHashCode()
        {
            return ClassContext.GetHashCode() ^ RuntimeHelpers.GetHashCode(Instance);
        }

        public override bool Equals(object obj)
        {
            if (obj is ThisRef)
            {
                ThisRef tref = (ThisRef)obj;
                return ClassContext.Equals(tref.ClassContext) &&
                    Instance == tref.Instance;
            }
            else
            {
                return false;
            }
        }

        public override void Accept(ILiteralVisitor visitor)
        {
            visitor.VisitThisRef(this);
        }

        public override object Eval(IEvaluator eval)
        {
            return eval.EvalThisRef(this);
        }

        public static object DefaultEval(ThisRef thisRef)
        {
            return thisRef.Instance;
        }
    }

    /// <summary>
    /// This static class provides extension methods for converting dimensional specifiers and
    /// index specifiers to expressions.
    /// </summary>
    public static class DimExtensions
    {
        /// <summary>
        /// Converts the dimensional specifier to an expression.
        /// </summary>
        /// <param name="dimSpec">dimensional specifier to convert</param>
        /// <returns>expression representing the dimensional specifier</returns>
        public static Expression AsExpression(this DimSpec dimSpec)
        {
            switch (dimSpec.Kind)
            {
                case DimSpec.EKind.Index:
                    return LiteralReference.CreateConstant((int)dimSpec);

                case DimSpec.EKind.Range:
                    return LiteralReference.CreateConstant((Range)dimSpec);

                default: throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts an index specifier to an array of expressions.
        /// </summary>
        /// <param name="indexSpec">index specifier to convert</param>
        /// <returns>nested array of expressions, each representing an individual index</returns>
        public static Expression[][] AsExpressions(this IndexSpec indexSpec)
        {
            return indexSpec.Indices.Select(idx => new Expression[] { idx.AsExpression() }).ToArray();
        }
    }

    /// <summary>
    /// Signal reference literal
    /// </summary>
    public class SignalRef :
        Literal, IStorableLiteral
    {
        /// <summary>
        /// Selects, which property of the signal is referenced
        /// </summary>
        public enum EReferencedProperty
        {
            /// <summary>
            /// The signal instance itself
            /// </summary>
            Instance,

            /// <summary>
            /// The "Next" property
            /// </summary>
            Next,

            /// <summary>
            /// The "Cur" property
            /// </summary>
            Cur,

            /// <summary>
            /// The "Pre" property
            /// </summary>
            Pre,

            /// <summary>
            /// The signal change event
            /// </summary>
            ChangedEvent,

            /// <summary>
            /// The rising edge event
            /// </summary>
            RisingEdge,

            /// <summary>
            /// The falling edge event
            /// </summary>
            FallingEdge
        }

        /// <summary>
        /// The referenced descriptor
        /// </summary>
        public ISignalOrPortDescriptor Desc { get; private set; }

        /// <summary>
        /// The referenced property
        /// </summary>
        public EReferencedProperty Prop { get; private set; }

        /// <summary>
        /// Indices to apply to the signal
        /// </summary>
        public IEnumerable<Expression[]> Indices { get; private set; }

        /// <summary>
        /// Sample index
        /// </summary>
        public IndexSpec IndexSample { get; private set; }

        /// <summary>
        /// Whether the applied signal index is static (i.e. constant)
        /// </summary>
        public bool IsStaticIndex { get; private set; }

        /// <summary>
        /// Returns the name of the signal descriptor.
        /// </summary>
        public string Name
        {
            get { return Desc.Name; }
        }

        public override TypeDescriptor Type
        {
            get
            {
                IndexSpec indexSpec;
                var udesc = Desc.GetUnindexedContainer(out indexSpec);
                int dimRed0 = indexSpec.MinTargetDimension - indexSpec.MinSourceDimension;
                int dimRed1 = Indices.Sum(idx => idx.Count(e => !e.ResultType.CILType.Equals(typeof(Range))));
                int dimRed = dimRed0 + dimRed1;
                var elemType = udesc.ElementType;
                while (dimRed > 0)
                {
                    if (dimRed >= elemType.Rank)
                    {
                        dimRed -= elemType.Rank;
                        elemType = elemType.Element0Type;
                    }
                    else
                    {
                        // element type is not well-defined
                        elemType = null;
                        break;
                    }
                }

                switch (Prop)
                {
                    case EReferencedProperty.ChangedEvent:
                        return (TypeDescriptor)typeof(EventSource);

                    case EReferencedProperty.Instance:
                        if (Indices.Count() == 0)
                            return (TypeDescriptor)Desc.InstanceType;
                        else
                            return (TypeDescriptor)typeof(Signal<>).MakeGenericType(elemType.CILType);

                    case EReferencedProperty.Cur:
                    case EReferencedProperty.Next:
                    case EReferencedProperty.Pre:
                        return elemType;

                    case EReferencedProperty.RisingEdge:
                    case EReferencedProperty.FallingEdge:
                        return (TypeDescriptor)typeof(bool);

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Returns always <c>EStoreMode.Transfer</c>.
        /// </summary>
        public EStoreMode StoreMode
        {
            get { return EStoreMode.Transfer; }
        }

        private void PopulateIndexList(List<Expression[]> result, IndexSpec indexSpec, TypeDescriptor elemType)
        {
            int count = indexSpec.Indices.Length;
            var curSeq = indexSpec.Indices.Reverse();
            while (curSeq.Any())
            {
                var seq = curSeq.Take(elemType.Rank).Reverse();
                result.Add(seq
                        .Select(ds => ds.Kind == DimSpec.EKind.Index ?
                                LiteralReference.CreateConstant((int)ds) :
                                LiteralReference.CreateConstant((Range)ds))
                        .ToArray());
                if (seq.Any(ds => ds.Kind == DimSpec.EKind.Range))
                    break;
                curSeq = curSeq.Skip(elemType.Rank);
                elemType = elemType.Element0Type;
            }
        }

        public List<Expression[]> GetFullIndices()
        {
            List<Expression[]> result = new List<Expression[]>();
            if (IsStaticIndex)
            {
                var asmRef = AssimilateIndices();
                var elemType = asmRef.Desc.ElementType;
                PopulateIndexList(result, asmRef.IndexSample, elemType);
            }
            else
            {
                IndexSpec accIndex;
                var udesc = Desc.GetUnindexedContainer(out accIndex);
                PopulateIndexList(result, accIndex, udesc.ElementType);
                result.AddRange(Indices);
            }
            return result;
        }

        /// <summary>
        /// Constructs a signal reference literal.
        /// </summary>
        /// <param name="desc">referenced descriptor</param>
        /// <param name="prop">referenced signal property</param>
        public SignalRef(ISignalOrPortDescriptor desc, EReferencedProperty prop)
        {
            Contract.Requires(desc != null);

            Desc = desc;
            Prop = prop;
            Indices = new List<Expression[]>();
            IndexSample = new IndexSpec();
            IsStaticIndex = true;
        }

        /// <summary>
        /// Constructs a signal reference literal.
        /// </summary>
        /// <param name="desc">referenced descriptor</param>
        /// <param name="prop">referenced signal property</param>
        /// <param name="indices">indices to apply</param>
        /// <param name="indexSample">sample index</param>
        /// <param name="isStaticIndex">whether the indices are static (i.e.) constant, in which case the sample index can be taken
        /// for granted</param>
        public SignalRef(ISignalOrPortDescriptor desc, EReferencedProperty prop, 
            IEnumerable<Expression[]> indices, IndexSpec indexSample, bool isStaticIndex)
        {
            Contract.Requires(desc != null);
            Contract.Requires(indices != null);
            Contract.Requires(indexSample != null);
            Contract.Requires(indices.Count() == indexSample.Indices.Length);

            Desc = desc;
            Prop = prop;
            Indices = indices.ToList();
            IndexSample = indexSample;
            IsStaticIndex = isStaticIndex;
        }

        /// <summary>
        /// Constructs a signal reference literal, based on another one.
        /// </summary>
        /// <param name="other">signal reference literal to copy from</param>
        public SignalRef(SignalRef other)
        {
            Desc = other.Desc;
            Prop = other.Prop;
            Indices = new List<Expression[]>(other.Indices);
            IndexSample = other.IndexSample;
            IsStaticIndex = other.IsStaticIndex;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (Desc.Name == null || Desc.Name.Length == 0)
            {
                if (Desc.HasAttribute<int>())
                {
                    sb.Append("$" + Desc.QueryAttribute<int>());
                }
                else
                {
                    sb.Append("?");
                }
            }
            else
            {
                sb.Append(Desc.Name);
            }
            sb.Append("'");
            sb.Append(Prop.ToString());
            foreach (Expression[] idx in GetFullIndices())
            {
                sb.Append("(");
                sb.Append(string.Join<Expression>(", ", idx));
                sb.Append(")");
            }

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is SignalRef)
            {
                SignalRef other = (SignalRef)obj;
                return Desc.Equals(other.Desc) &&
                    Indices.SequenceEqual(other.Indices, Arrays.CreateArrayEqualityComparer<Expression>());
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Desc.GetHashCode() ^
                Indices.GetSequenceHashCode(idx => idx.GetSequenceHashCode());
        }

        public override void Accept(ILiteralVisitor visitor)
        {
            visitor.VisitSignalRef(this);
        }

        public override object Eval(IEvaluator eval)
        {
            return eval.EvalSignalRef(this);
        }

        public static object DefaultEval(SignalRef signalRef, IEvaluator eval)
        {
            SignalDescriptor sd = signalRef.Desc as SignalDescriptor;
            if (sd == null)
                throw new BreakEvaluationException();
            SignalBase sinst = sd.SignalInstance;
            dynamic sobj = sinst;
            if (signalRef.Indices != null && signalRef.Indices.Count() > 0)
            {
                foreach (Expression[] indices in signalRef.Indices)
                {
                    object[] indexvs = indices.Select(x => x.Eval(eval)).ToArray();
                    Type[] indexts = indexvs.Select(x => x.GetType()).ToArray();
                    Type vtype = sobj.GetType();
                    PropertyInfo prop = vtype.GetProperty("Item", indexts);
                    if (prop == null)
                        throw new InvalidOperationException("Indexer property not found");
                    sobj = prop.GetValue(sobj, indexvs);
                }
            }
            switch (signalRef.Prop)
            {
                case SignalRef.EReferencedProperty.ChangedEvent:
                    return sobj.ChangedEvent;

                case SignalRef.EReferencedProperty.Cur:
                    if (sinst.Context.State == DesignContext.ESimState.Simulation)
                        return sobj.Cur;
                    else
                        return sobj.InitialValue;

                case SignalRef.EReferencedProperty.FallingEdge:
                    if (sinst.Context.State == DesignContext.ESimState.Simulation)
                        return ((In<StdLogic>)sobj).FallingEdge();
                    else
                        return false;

                case SignalRef.EReferencedProperty.RisingEdge:
                    if (sinst.Context.State == DesignContext.ESimState.Simulation)
                        return ((In<StdLogic>)sobj).RisingEdge();
                    else
                        return false;

                case SignalRef.EReferencedProperty.Instance:
                    return sobj;

                case SignalRef.EReferencedProperty.Next:
                    throw new InvalidOperationException();

                case SignalRef.EReferencedProperty.Pre:
                    if (sinst.Context.State == DesignContext.ESimState.Simulation)
                        return sobj.Pre;
                    else
                        return sobj.InitialValue;

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns a signal instance which is described by this literal.
        /// </summary>
        public ISignal ToSignal()
        {
            var sd = Desc as SignalDescriptor;
            if (sd == null)
                throw new InvalidOperationException("Underlying signal descriptor is not an instance descriptor");
            if (IndexSample == null)
                throw new InvalidOperationException("No index sample");
            return sd.Instance.ApplyIndex(IndexSample);
        }

        /// <summary>
        /// Removes all indices from the underlying descriptor and adds them to this reference literal.
        /// </summary>
        /// <returns>a possibly modified, but semantically equivalent signal reference literal</returns>
        public SignalRef AssimilateIndices()
        {
            if (!IsStaticIndex)
                return this;

            IndexSpec rootIndex;
            var udesc = Desc.GetUnindexedContainer(out rootIndex);
            var myIndex = IndexSample.Project(rootIndex);
            
            return new SignalRef(
                udesc, Prop,
                myIndex.AsExpressions(),
                myIndex, true);
        }

        /// <summary>
        /// Creates a new signal reference literal.
        /// </summary>
        /// <param name="desc">referenced descriptor</param>
        /// <param name="prop">referenced property</param>
        public static SignalRef Create(ISignalOrPortDescriptor desc, EReferencedProperty prop)
        {
            Contract.Requires(desc != null);
            return new SignalRef(desc, prop);
        }

        /// <summary>
        /// Creates a new signal reference literal.
        /// </summary>
        /// <param name="signal">reference signal instance</param>
        /// <param name="prop">referenced property</param>
        public static SignalRef Create(SignalBase signal, EReferencedProperty prop)
        {
            Contract.Requires(signal != null);
            return Create(signal.Descriptor, prop);
        }

        /// <summary>
        /// Returns the signal reference which results from applying an index to this one.
        /// </summary>
        /// <param name="index">index specifier to apply</param>
        /// <returns>the indexed reference</returns>
        public SignalRef ApplyIndex(IndexSpec index)
        {
            if (!IsStaticIndex)
                throw new InvalidOperationException("Only applicable to static indices");

            var asmRef = AssimilateIndices();
            var resultIndex = index.Project(asmRef.IndexSample);
            return new SignalRef(asmRef.Desc, Prop, resultIndex.AsExpressions(), resultIndex, true);
        }
    }

    /// <summary>
    /// An array element reference literal.
    /// </summary>
    public class ArrayRef :
        Literal, IStorableLiteral
    {
        /// <summary>
        /// Expression representing the array.
        /// </summary>
        public Expression ArrayExpr { get; private set; }

        /// <summary>
        /// Expressions representing the array indices.
        /// </summary>
        public Expression[] Indices { get; private set; }

        /// <summary>
        /// Constructs a new array element reference literal.
        /// </summary>
        /// <param name="arrayExpr">expression representing the referenced array</param>
        /// <param name="elemType">element type descriptor</param>
        /// <param name="indices">expressions representing the accessed array indices</param>
        public ArrayRef(Expression arrayExpr, TypeDescriptor elemType, params Expression[] indices)
        {
            if (arrayExpr == null || indices == null || elemType == null)
                throw new ArgumentException();

            ArrayExpr = arrayExpr;
            Indices = indices;
            _type = elemType;
        }

        public override void Accept(ILiteralVisitor visitor)
        {
            visitor.VisitArrayRef(this);
        }

        private TypeDescriptor _type;
        public override TypeDescriptor Type
        {
            get { return _type; }
        }

        public EStoreMode StoreMode
        {
            get { return EStoreMode.Assign; }
        }

        public override object Eval(IEvaluator eval)
        {
            return eval.EvalArrayRef(this);
        }

        public static object DefaultEval(ArrayRef arrayRef, IEvaluator eval)
        {
            Array array = (Array)arrayRef.ArrayExpr.Eval(eval);
            long[] indices = arrayRef.Indices.Select(i => 
                TypeConversions.ToLong(i.Eval(eval))).ToArray();
            return array.GetValue(indices);
        }

        #region IStorable Member

        public string Name
        {
            get { return ArrayExpr.ToString(); }
        }

        #endregion

        public override bool Equals(object obj)
        {
            ArrayRef aref = obj as ArrayRef;
            if (aref == null)
                return false;

            return
                object.Equals(ArrayExpr, aref.ArrayExpr) &&
                    Enumerable.SequenceEqual(Indices, aref.Indices);
        }

        public override int GetHashCode()
        {
            int hash = ArrayExpr.GetHashCode();
            foreach (Expression index in Indices)
                hash ^= index.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ArrayExpr.ToString());
            sb.Append("[");
            sb.Append(string.Join<Expression>(", ", Indices));
            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Converts this instance to a fixed array reference.
        /// </summary>
        public FixedArrayRef AsFixed()
        {
            LiteralReference lr = ArrayExpr as LiteralReference;
            if (lr == null)
                return null;
            object obj;
            if (!lr.ReferencedObject.IsConst(out obj))
                return null;
            Array array = obj as Array;
            if (array == null)
                return null;
            long[] constIndices = new long[Indices.Length];
            IEvaluator eval = new DefaultEvaluator();
            for (int i = 0; i < Indices.Length; i++)
            {
                Expression index = Indices[i];
                if (!index.IsConst())
                {
                    constIndices = null;
                    break;
                }
                constIndices[i] = TypeConversions.ToLong(index.Eval(eval));
            }
            return new FixedArrayRef(lr.ReferencedObject, array, constIndices);
        }
    }

    /// <summary>
    /// Describes an array element reference whose base array instance is fixed and known during code analysis.
    /// </summary>
    public class FixedArrayRef
    {
        /// <summary>
        /// Literal describing the referenced array.
        /// </summary>
        public ILiteral ArrayLit { get; private set; }

        /// <summary>
        /// Instance of referenced array.
        /// </summary>
        public Array ArrayObj { get; private set; }

        /// <summary>
        /// Array indices
        /// </summary>
        public long[] Indices { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the array indices are constant.
        /// </summary>
        public bool IndicesConst
        {
            get { return Indices != null; }
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="arrayLit">literal describing the referenced array</param>
        /// <param name="array">array instance</param>
        /// <param name="indices">constant indices</param>
        public FixedArrayRef(ILiteral arrayLit, Array array, long[] indices)
        {
            ArrayLit = arrayLit;
            ArrayObj = array;
            Indices = indices;
        }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="arrayLit">literal describing the referenced array</param>
        /// <param name="array">array instance</param>
        public FixedArrayRef(ILiteral arrayLit, Array array)
        {
            ArrayLit = arrayLit;
            ArrayObj = array;
        }

        public override string ToString()
        {
            int id = RuntimeHelpers.GetHashCode(ArrayObj);
            StringBuilder sb = new StringBuilder();
            sb.Append("array");
            sb.Append(id);
            sb.Append("<");
            sb.Append(ArrayObj.GetType());
            sb.Append(">[");
            sb.Append(Indices == null ? "?" : string.Join(",", Indices));
            sb.Append("]");
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            FixedArrayRef other = obj as FixedArrayRef;
            if (other == null)
                return false;
            return ArrayObj == other.ArrayObj &&
                ((Indices == null && other.Indices == null) || 
                (Indices != null && other.Indices != null && Indices.SequenceEqual(other.Indices)));
        }

        public override int GetHashCode()
        {
            return ArrayObj.GetHashCode() ^
                (Indices == null ? 0 : Indices.GetSequenceHashCode());
        }

        /// <summary>
        /// Returns the element type descriptor.
        /// </summary>
        public TypeDescriptor ElementType
        {
            get
            {
                var firstElem = ArrayObj.GetValue(new int[ArrayObj.Rank]);
                var elemType = TypeDescriptor.GetTypeOf(firstElem);
                return elemType;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if this reference may alias another fixed array reference.
        /// </summary>
        /// <param name="other">other fixed array reference</param>
        public bool MayAlias(FixedArrayRef other)
        {
            if (ArrayObj != other.ArrayObj)
                return false;
            if (!IndicesConst || !other.IndicesConst)
                return true;
            return Indices.SequenceEqual(other.Indices);
        }
    }

    /// <summary>
    /// A placeholder literal.
    /// </summary>
    public class LazyLiteral :
        Literal, IStorableLiteral
    {
        /// <summary>
        /// Gets or sets the actual literal which is encapsulated by this literal.
        /// </summary>
        public IStorableLiteral PlaceHolder { get; set; }

        public override void Accept(ILiteralVisitor visitor)
        {
            if (PlaceHolder != null)
                PlaceHolder.Accept(visitor);
        }

        public override TypeDescriptor Type
        {
            get { return PlaceHolder.Type; }
        }

        public EStoreMode StoreMode
        {
            get { return PlaceHolder.StoreMode; }
        }

        public override object Eval(IEvaluator eval)
        {
            return PlaceHolder.Eval(eval);
        }

        #region IStorable Member

        public string Name
        {
            get { return PlaceHolder.Name; }
        }

        #endregion

        public override string ToString()
        {
            if (PlaceHolder == null)
                return "<placeholder>";
            else
                return PlaceHolder.ToString();
        }
    }

    /// <summary>
    /// Visitor pattern interface for literals.
    /// </summary>
    public interface ILiteralVisitor
    {
        void VisitConstant(Constant constant);
        void VisitVariable(Variable variable);
        void VisitFieldRef(FieldRef fieldRef);
        void VisitThisRef(ThisRef thisRef);
        void VisitSignalRef(SignalRef signalRef);
        void VisitArrayRef(ArrayRef arrayRef);
    }

    /// <summary>
    /// A default implementation of <c>ILiteralVisitor</c> which re-directs to delegates.
    /// </summary>
    public class LambdaLiteralVisitor: ILiteralVisitor
    {
        public delegate void VisitConstantFunc(Constant constant);
        public delegate void VisitVariableFunc(Variable variable);
        public delegate void VisitFieldRefFunc(FieldRef fieldRef);
        public delegate void VisitThisRefFunc(ThisRef thisRef);
        public delegate void VisitSignalRefFunc(SignalRef signalRef);
        public delegate void VisitArrayRefFunc(ArrayRef arrayRef);

        /// <summary>
        /// Gets or sets the delegate for constant visiting.
        /// </summary>
        public VisitConstantFunc OnVisitConstant { get; set; }

        /// <summary>
        /// Gets or sets the delegate for vairable visiting.
        /// </summary>
        public VisitVariableFunc OnVisitVariable { get; set; }

        /// <summary>
        /// Gets or sets the delegate for field reference visiting.
        /// </summary>
        public VisitFieldRefFunc OnVisitFieldRef { get; set; }

        /// <summary>
        /// Gets or sets the delegate for visiting the "this" reference.
        /// </summary>
        public VisitThisRefFunc OnVisitThisRef { get; set; }

        /// <summary>
        /// Gets or sets the delegate for signal reference visiting.
        /// </summary>
        public VisitSignalRefFunc OnVisitSignalRef { get; set; }

        /// <summary>
        /// Gets or sets the delegate for array reference visiting.
        /// </summary>
        public VisitArrayRefFunc OnVisitArrayRef { get; set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public LambdaLiteralVisitor()
        {
            OnVisitConstant = x => { };
            OnVisitVariable = x => { };
            OnVisitFieldRef = x => { };
            OnVisitThisRef = x => { };
            OnVisitSignalRef = x => { };
            OnVisitArrayRef = x => { };
        }

        #region ILiteralVisitor Member

        public void VisitConstant(Constant constant)
        {
            OnVisitConstant(constant);
        }

        public void VisitVariable(Variable variable)
        {
            OnVisitVariable(variable);
        }

        public void VisitFieldRef(FieldRef fieldRef)
        {
            OnVisitFieldRef(fieldRef);
        }

        public void VisitThisRef(ThisRef thisRef)
        {
            OnVisitThisRef(thisRef);
        }

        public void VisitSignalRef(SignalRef signalRef)
        {
            OnVisitSignalRef(signalRef);
        }

        public void VisitArrayRef(ArrayRef arrayRef)
        {
            OnVisitArrayRef(arrayRef);
        }

        #endregion
    }

    class IsConstLiteralVisitor : ILiteralVisitor
    {
        public bool Result { get; private set; }
        public object ConstValue { get; private set; }

        public void VisitConstant(Constant constant)
        {
            Result = true;
            ConstValue = constant.ConstantValue;
        }

        public void VisitVariable(Variable variable)
        {
            Result = false;
        }

        public void VisitFieldRef(FieldRef fieldRef)
        {
            var cfd = fieldRef.FieldDesc as CILFieldDescriptor;
            if (cfd != null)
            {
                FieldFacts facts = FactUniverse.Instance.GetFacts(cfd.Field);
                ConstValue = fieldRef.FieldDesc.ConstantValue;
                Result = !facts.IsWritten;
            }
            else
            {
                Result = false;
            }
        }

        public void VisitThisRef(ThisRef thisRef)
        {
            Result = true;
            ConstValue = thisRef.Instance;
        }

        public void VisitSignalRef(SignalRef signalRef)
        {
            switch (signalRef.Prop)
            {
                case SignalRef.EReferencedProperty.Instance:
                case SignalRef.EReferencedProperty.ChangedEvent:
                    {
                        Result = true;
                        SignalDescriptor sd = signalRef.Desc as SignalDescriptor;
                        if (sd != null)
                        {
                            switch (signalRef.Prop)
                            {
                                case SignalRef.EReferencedProperty.Instance:
                                    ConstValue = sd.SignalInstance;
                                    break;

                                case SignalRef.EReferencedProperty.ChangedEvent:
                                    ConstValue = sd.SignalInstance.ChangedEvent;
                                    break;

                                default:
                                    throw new NotImplementedException();
                            }
                        }
                    }
                    break;

                default:
                    Result = false;
                    break;
            }
        }

        public void VisitArrayRef(ArrayRef arrayRef)
        {
            Result = false;
        }
    }

    /// <summary>
    /// This static class provides extension methods to simplify the usage of literals.
    /// </summary>
    public static class LiteralExtensions
    {
        /// <summary>
        /// Returns <c>true</c> if the literal depicts a constant.
        /// </summary>
        /// <param name="lit">literal</param>
        /// <param name="constValue">out parameter to receive the constant value, if the literal 
        /// depicts a constant, otherwise <c>null</c></param>
        public static bool IsConst(this ILiteral lit, out object constValue)
        {
            IsConstLiteralVisitor vtor = new IsConstLiteralVisitor();
            lit.Accept(vtor);
            constValue = vtor.ConstValue;
            return vtor.Result;
        }
    }
}

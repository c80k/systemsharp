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
    public interface IStorable
    {
        string Name { get; }
        EStoreMode StoreMode { get; }
    }

    public enum EStoreMode
    {
        Assign,
        Transfer
    }

    public interface ILiteral
    {
        void Accept(ILiteralVisitor visitor);
        TypeDescriptor Type { get; }
    }

    public interface IStorableLiteral:
        ILiteral, IStorable, IEvaluable
    {
    }

    public abstract class Literal : 
        AttributedObject,
        IEvaluable, ILiteral
    {
        public abstract void Accept(ILiteralVisitor visitor);
        public abstract TypeDescriptor Type { get; }

        #region IEvaluable Member

        public abstract object Eval(IEvaluator eval);

        #endregion

        public static implicit operator LiteralReference(Literal lit)
        {
            return new LiteralReference(lit, LiteralReference.EMode.Direct);
        }
    }

    public class Constant : Literal
    {
        public object ConstantValue { get; private set; }

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
                return false;
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

    public class Variable :
        Literal, IStorableLiteral
    {
        public Variable(TypeDescriptor type)
        {
            _type = type;
            LocalIndex = -1;
            LocalSubIndex = -1;
        }

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

        public void UpgradeType(TypeDescriptor type)
        {
            _type = type;
        }

        public int LocalIndex { get; set; }
        public int LocalSubIndex { get; set; }
        public object InitialValue { get; set; }

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

    public class FieldRef :
        Literal, IStorableLiteral
    {
        //public FieldInfo Field { get; private set; }
        //public Expression Instance { get; private set; }
        public FieldDescriptor FieldDesc { get; internal set; }

        public FieldRef(FieldDescriptor field)
        {
            FieldDesc = field;
            CopyAttributes();
        }

        /*public FieldRef(FieldDescriptor field, Expression instance)
        {
            FieldDesc = field;
            Instance = instance;
            CopyAttributes();
        }*/

        private void CopyAttributes()
        {
            foreach (var attr in FieldDesc.Attributes)
                AddAttribute(attr);
        }

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
            //if (Instance != null)
            //    result = Instance.ToString() + ".";
            result += Name;
            return result;
        }

        public override int GetHashCode()
        {
            //if (Instance == null)
                return FieldDesc.GetHashCode();
            //else
            //    return Instance.GetHashCode() ^ FieldDesc.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is FieldRef)
            {
                FieldRef fref = (FieldRef)obj;
                return /*((Instance == null && fref.Instance == null) ||
                    (Instance != null && Instance.Equals(fref.Instance))) &&*/
                    FieldDesc.Equals(fref.FieldDesc);
            }
            else
                return false;
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

    public class ThisRef : Literal
    {
        public Type ClassContext { get; private set; }
        public object Instance { get; private set; }

        public ThisRef(Type classContext, object instance)
        {
            if (classContext == null)
                throw new ArgumentException("classContext null");

            if (instance == null)
                throw new ArgumentException("instance is null");

            ClassContext = classContext;
            Instance = instance;
        }

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
                return false;
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

    public static class DimExtensions
    {
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

        public static Expression[][] AsExpressions(this IndexSpec indexSpec)
        {
            return indexSpec.Indices.Select(idx => new Expression[] { idx.AsExpression() }).ToArray();
        }
    }

    public class SignalRef :
        Literal, IStorableLiteral
    {
        public enum EReferencedProperty
        {
            Instance,
            Next,
            Cur,
            Pre,
            ChangedEvent,
            RisingEdge,
            FallingEdge
        }

        public ISignalOrPortDescriptor Desc { get; private set; }
        public EReferencedProperty Prop { get; private set; }
        public IEnumerable<Expression[]> Indices { get; private set; }
        public IndexSpec IndexSample { get; private set; }
        public bool IsStaticIndex { get; private set; }

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
                int dimRed0 = indexSpec.TargetDimension - indexSpec.SourceDimension;
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

#if false
                TypeDescriptor elemType = Desc.ElementType;
                foreach (Expression[] index in Indices)
                {
                    if (index.Length == 0)
                        continue;
                    else if (index.Length == 1)
                    {
                        dynamic sample = elemType.GetSampleInstance();
                        elemType = TypeDescriptor.GetTypeOf(sample[0]);
                        //elemType = elemType.Element0Type;
                    }
                    else if (index.Length == 2)
                    {
                        elemType = elemType.MakeUnconstrainedType();
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
#endif

                switch (Prop)
                {
                    case EReferencedProperty.ChangedEvent:
                        return (TypeDescriptor)typeof(AbstractEvent);

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

        public SignalRef(ISignalOrPortDescriptor desc, EReferencedProperty prop)
        {
            Contract.Requires(desc != null);

            Desc = desc;
            Prop = prop;
            Indices = new List<Expression[]>();
            IndexSample = new IndexSpec();
            IsStaticIndex = true;
        }

        public SignalRef(ISignalOrPortDescriptor desc, EReferencedProperty prop, 
            IEnumerable<Expression[]> indices, IndexSpec indexSample, bool isStaticIndex)
        {
            Contract.Requires(desc != null);
            Contract.Requires(indices != null);
            Contract.Requires(indexSample != null);
            Contract.Requires(indices.Count() == indexSample.Indices.Length);

            /*if (isStaticIndex)
            {
                IndexSpec rootIndex;
                desc.GetUnindexedContainer(out rootIndex);
                try
                {
                    indexSample.ApplyTo(rootIndex);
                }
                catch (ArgumentException)
                {
                    throw new ArgumentException("Invalid index sample");
                }
            }*/

            Desc = desc;
            Prop = prop;
            Indices = indices.ToList();
            IndexSample = indexSample;
            IsStaticIndex = isStaticIndex;
        }

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

        public ISignal ToSignal()
        {
            var sd = Desc as SignalDescriptor;
            if (sd == null)
                throw new InvalidOperationException("Underlying signal descriptor is not an instance descriptor");
            if (IndexSample == null)
                throw new InvalidOperationException("No index sample");
            return sd.Instance.ApplyIndex(IndexSample);
        }

        public SignalRef AssimilateIndices()
        {
            if (!IsStaticIndex)
                return this;

            IndexSpec rootIndex;
            var udesc = Desc.GetUnindexedContainer(out rootIndex);
            var myIndex = IndexSample.ApplyTo(rootIndex);
            
            return new SignalRef(
                udesc, Prop,
                myIndex.AsExpressions(),
                myIndex, true);
        }

        public static SignalRef Create(ISignalOrPortDescriptor desc, EReferencedProperty prop)
        {
            Contract.Requires(desc != null);
            return new SignalRef(desc, prop);
        }

        public static SignalRef Create(SignalBase signal, EReferencedProperty prop)
        {
            Contract.Requires(signal != null);
            return Create(signal.Descriptor, prop);
        }

        public SignalRef ApplyIndex(IndexSpec index)
        {
            if (!IsStaticIndex)
                throw new InvalidOperationException("Only applicable to static indices");

            var asmRef = AssimilateIndices();
            var resultIndex = index.ApplyTo(asmRef.IndexSample);
            return new SignalRef(asmRef.Desc, Prop, resultIndex.AsExpressions(), resultIndex, true);
        }
    }

    public class ArrayRef :
        Literal, IStorableLiteral
    {
        public Expression ArrayExpr { get; private set; }
        public Expression[] Indices { get; private set; }

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
            //get { return ArrayExpr.ResultType.Element0Type; }
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

    public class FixedArrayRef
    {
        public ILiteral ArrayLit { get; private set; }
        public Array ArrayObj { get; private set; }
        public long[] Indices { get; private set; }

        public bool IndicesConst
        {
            get { return Indices != null; }
        }

        public FixedArrayRef(ILiteral arrayLit, Array array, long[] indices)
        {
            ArrayLit = arrayLit;
            ArrayObj = array;
            Indices = indices;
        }

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

        public TypeDescriptor ElementType
        {
            get
            {
                var firstElem = ArrayObj.GetValue(new int[ArrayObj.Rank]);
                var elemType = TypeDescriptor.GetTypeOf(firstElem);
                return elemType;
            }
        }

        public bool MayAlias(FixedArrayRef other)
        {
            if (ArrayObj != other.ArrayObj)
                return false;
            if (!IndicesConst || !other.IndicesConst)
                return true;
            return Indices.SequenceEqual(other.Indices);
        }
    }

    public class LazyLiteral :
        Literal, IStorableLiteral
    {
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

    public interface ILiteralVisitor
    {
        void VisitConstant(Constant constant);
        void VisitVariable(Variable variable);
        void VisitFieldRef(FieldRef fieldRef);
        void VisitThisRef(ThisRef thisRef);
        void VisitSignalRef(SignalRef signalRef);
        void VisitArrayRef(ArrayRef arrayRef);
    }

    public class LambdaLiteralVisitor: ILiteralVisitor
    {
        public delegate void VisitConstantFunc(Constant constant);
        public delegate void VisitVariableFunc(Variable variable);
        public delegate void VisitFieldRefFunc(FieldRef fieldRef);
        public delegate void VisitThisRefFunc(ThisRef thisRef);
        public delegate void VisitSignalRefFunc(SignalRef signalRef);
        public delegate void VisitArrayRefFunc(ArrayRef arrayRef);

        public VisitConstantFunc OnVisitConstant { get; set; }
        public VisitVariableFunc OnVisitVariable { get; set; }
        public VisitFieldRefFunc OnVisitFieldRef { get; set; }
        public VisitThisRefFunc OnVisitThisRef { get; set; }
        public VisitSignalRefFunc OnVisitSignalRef { get; set; }
        public VisitArrayRefFunc OnVisitArrayRef { get; set; }

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

    public static class LiteralExtensions
    {
        public static bool IsConst(this ILiteral lit, out object constValue)
        {
            IsConstLiteralVisitor vtor = new IsConstLiteralVisitor();
            lit.Accept(vtor);
            constValue = vtor.ConstValue;
            return vtor.Result;
        }
    }
}

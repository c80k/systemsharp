/**
 * Copyright 2011-2014 Christian Köllner, David Hlavac
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.SysDOM;
using LinqEx = System.Linq.Expressions.Expression;

namespace SystemSharp.Components
{
    /// <summary>
    /// This interface models a signal input port.
    /// </summary>
    [MapToPort(EFlowDirection.In)]
    [SignalArgument]
    [SignalField]
    public interface In : IInPort
    {
        /// <summary>
        /// Returns the current signal value.
        /// </summary>        
        object Cur { [SignalProperty(SignalRef.EReferencedProperty.Cur)] get; }

        /// <summary>
        /// Returns the previous signal value (which the signal had in the last delta cycle).
        /// </summary>        
        object Pre { [SignalProperty(SignalRef.EReferencedProperty.Pre)] get; }
    }

    /// <summary>
    /// Models a signal input port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    public interface IIn<T> :
        In
    {
        /// <summary>
        /// Returns the current signal value.
        /// </summary>        
        new T Cur { [SignalProperty(SignalRef.EReferencedProperty.Cur)] get; }

        /// <summary>
        /// Returns the previous signal value (which the signal had in the last delta cycle).
        /// </summary>        
        new T Pre { [SignalProperty(SignalRef.EReferencedProperty.Pre)] get; }
    }

    /// <summary>
    /// Models a signal input port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    [MapToPort(EFlowDirection.In)]
    [SignalArgument]
    [SignalField]
    public abstract class In<T> :
        DesignObject,
        IIn<T>
    {
        /// <summary>
        /// Returns the current signal value.
        /// </summary>        
        public abstract T Cur { [SignalProperty(SignalRef.EReferencedProperty.Cur)] get; }

        /// <summary>
        /// Returns the previous signal value (which the signal had in the last delta cycle).
        /// </summary>        
        public abstract T Pre { [SignalProperty(SignalRef.EReferencedProperty.Pre)] get; }

        public abstract EventSource ChangedEvent { get; }

        object In.Cur
        {
            get { return Cur; }
        }

        object In.Pre
        {
            get { return Pre; }
        }

        public abstract Expression DescribingExpression { get; }

        /// <summary>
        /// Constructs a signal interface that provides the negated signal value.
        /// </summary>
        /// <param name="x">signal whose value to negate</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement negation.</exception>
        public static In<T> operator -(In<T> x)
        {
            return new UnaryOpIn<T>(x, e => -e, GenericMath<T>.Negate);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise complemented signal value.
        /// </summary>
        /// <param name="x">signal whose value to negate</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the complement operator.</exception>
        public static In<T> operator !(In<T> x)
        {
            return new UnaryOpIn<T>(x, e => !e, GenericMath<T>.Not);
        }

        /// <summary>
        /// Constructs a signal interface that provides the sum of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first summand</param>
        /// <param name="y">signal that provides the second summand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement addition.</exception>
        public static In<T> operator +(In<T> x, In<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex + ey, GenericMath<T>.Add);
        }

        /// <summary>
        /// Constructs a signal interface that provides the difference of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the subtrahend</param>
        /// <param name="y">signal that provides the subtractor</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement subtraction.</exception>
        public static In<T> operator -(In<T> x, In<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex - ey, GenericMath<T>.Subtract);
        }

        /// <summary>
        /// Constructs a signal interface that provides the product of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first multiplicand</param>
        /// <param name="y">signal that provides the second multiplicand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement multiplication.</exception>
        public static In<T> operator *(In<T> x, In<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex * ey, GenericMath<T>.Multiply);
        }

        /// <summary>
        /// Constructs a signal interface that provides the quotient of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the dividend</param>
        /// <param name="y">signal that provides the divisor</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement division.</exception>
        public static In<T> operator /(In<T> x, In<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex / ey, GenericMath<T>.Divide);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise conjunction of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "and" operator.</exception>
        public static In<T> operator &(In<T> x, In<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex & ey, GenericMath<T>.And);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise disjunction of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "or" operator.</exception>
        public static In<T> operator |(In<T> x, In<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex | ey, GenericMath<T>.Or);
        }

        /// <summary>
        /// Constructs a signal interface that provides the bit-wise antivalence of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "exclusive or" operator.</exception>
        public static In<T> operator ^(In<T> x, In<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex ^ ey, GenericMath<T>.Xor);
        }
    }

    /// <summary>
    /// This interface models a signal output port.
    /// </summary>
    [MapToPort(EFlowDirection.Out)]
    [SignalArgument]
    [SignalField]
    public interface Out : IOutPort
    {
        /// <summary>
        /// Writes the signal value.
        /// </summary>        
        object Next { [SignalProperty(SignalRef.EReferencedProperty.Next)] set; }
    }

    /// <summary>
    /// Models a signal output port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    public interface IOut<T> :
        Out
    {
        /// <summary>
        /// Writes the signal value.
        /// </summary>        
        public new T Next { [SignalProperty(SignalRef.EReferencedProperty.Next)] set; }
    }

    /// <summary>
    /// Models a signal output port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    [MapToPort(EFlowDirection.Out)]
    [SignalArgument]
    [SignalField]
    public abstract class Out<T> :
        DesignObject,
        IOut<T>
    {
        /// <summary>
        /// Writes the signal value.
        /// </summary>        
        public abstract T Next { [SignalProperty(SignalRef.EReferencedProperty.Next)] set; }

        object Out.Next
        {
            set { Next = (T)value; }
        }

        public abstract Expression DescribingExpression { get; }
    }

    /// <summary>
    /// Models a bi-directional signal port of a specific data type.
    /// </summary>
    /// <typeparam name="T">The type of the data which is transported by the signal.</typeparam>
    [MapToPort(EFlowDirection.InOut)]
    [SignalArgument]
    [SignalField]
    public abstract class InOut<T> :
        DesignObject,
        IInOutPort,
        IIn<T>, IOut<T>
    {
        #region private types

        private class InProxy : In<T>
        {
            private InOut<T> _inout;

            public InProxy(InOut<T> inout)
            {
                _inout = inout;
            }

            public override T Cur
            {
                get { return _inout.Cur; }
            }

            public override T Pre
            {
                get { return _inout.Pre; }
            }

            public override EventSource ChangedEvent
            {
                get { return _inout.ChangedEvent; }
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }
        }

        private class OutProxy : Out<T>
        {
            private InOut<T> _inout;

            public OutProxy(InOut<T> inout)
            {
                _inout = inout;
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }

            public override T Next
            {
                set { _inout.Next = value; }
            }
        }

        #endregion private types

        /// <summary>
        /// Returns the current signal value.
        /// </summary>        
        public abstract T Cur { [SignalProperty(SignalRef.EReferencedProperty.Cur)] get; }

        /// <summary>
        /// Returns the previous signal value (which the signal had in the last delta cycle).
        /// </summary>        
        public abstract T Pre { [SignalProperty(SignalRef.EReferencedProperty.Pre)] get; }

        /// <summary>
        /// Writes the signal value.
        /// </summary>        
        public abstract T Next { [SignalProperty(SignalRef.EReferencedProperty.Next)] set; }

        public abstract EventSource ChangedEvent { get; }

        EventSource IInPort.ChangedEvent
        {
            get { return ChangedEvent; }
        }

        public abstract Expression DescribingExpression { get; }

        object In.Cur
        {
            get { return Cur; }
        }

        object In.Pre
        {
            get { return Pre; }
        }

        object Out.Next
        {
            set { Next = (T)value; }
        }

        /// <summary>
        /// Constructs a signal view that provides the negated signal value.
        /// </summary>
        /// <param name="x">signal whose value to negate</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement negation.</exception>
        public static In<T> operator -(InOut<T> x)
        {
            return new UnaryOpIn<T>(x, e => -e, GenericMath<T>.Negate);
        }

        /// <summary>
        /// Constructs a signal view that provides the bit-wise complemented signal value.
        /// </summary>
        /// <param name="x">signal whose value to negate</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the complement operator.</exception>
        public static In<T> operator !(InOut<T> x)
        {
            return new UnaryOpIn<T>(x, e => !e, GenericMath<T>.Not);
        }

        /// <summary>
        /// Constructs a signal view that provides the sum of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first summand</param>
        /// <param name="y">signal that provides the second summand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement addition.</exception>
        public static In<T> operator +(InOut<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex + ey, GenericMath<T>.Add);
        }

        /// <summary>
        /// Constructs a signal view that provides the difference of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the subtrahend</param>
        /// <param name="y">signal that provides the subtractor</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement subtraction.</exception>
        public static In<T> operator -(InOut<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex - ey, GenericMath<T>.Subtract);
        }

        /// <summary>
        /// Constructs a signal view that provides the product of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first multiplicand</param>
        /// <param name="y">signal that provides the second multiplicand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement multiplication.</exception>
        public static In<T> operator *(InOut<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex * ey, GenericMath<T>.Multiply);
        }

        /// <summary>
        /// Constructs a signal view that provides the quotient of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the dividend</param>
        /// <param name="y">signal that provides the divisor</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement division.</exception>
        public static In<T> operator /(InOut<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex / ey, GenericMath<T>.Divide);
        }

        /// <summary>
        /// Constructs a signal view that provides the bit-wise conjunction of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "and" operator.</exception>
        public static In<T> operator &(InOut<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex & ey, GenericMath<T>.And);
        }

        /// <summary>
        /// Constructs a signal view that provides the bit-wise disjunction of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "or" operator.</exception>
        public static In<T> operator |(InOut<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex | ey, GenericMath<T>.Or);
        }

        /// <summary>
        /// Constructs a signal view that provides the bit-wise antivalence of two signal values.
        /// </summary>
        /// <param name="x">signal that provides the first operand</param>
        /// <param name="y">signal that provides the second operand</param>
        /// <exception cref="NotSupportedException">If the signal data type does not implement the "exclusive or" operator.</exception>
        public static In<T> operator ^(InOut<T> x, InOut<T> y)
        {
            return new BinaryOpIn<T>(x, y, (ex, ey) => ex ^ ey, GenericMath<T>.Xor);
        }

        /// <summary>
        /// Implicitly converts the bidirectional signal view to an input view.
        /// </summary>
        public static implicit operator In<T>(InOut<T> inout)
        {
            return new InProxy(inout);
        }

        /// <summary>
        /// Implicitly converts the bidirectional signal view to an input view.
        /// </summary>
        public static implicit operator Out<T>(InOut<T> inout)
        {
            return new OutProxy(inout);
        }
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) input port.
    /// </summary>
    /// <typeparam name="TElem">type of single data element</typeparam>
    public interface IVIn<TElem> :
        IIn<Vector<TElem>>,
        IIndexable<In<TElem>>
    {
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) input port.
    /// </summary>
    /// <typeparam name="TElem">type of single data element</typeparam>
    [MapToPort(EFlowDirection.In)]
    [SignalArgument]
    [SignalField]
    public abstract class VIn<TElem> :
        In<Vector<TElem>>,
        IVIn<TElem>
    {
        public abstract Vector<TElem> Cur { get; }
        public abstract Vector<TElem> Pre { get; }
        public abstract EventSource ChangedEvent { get; }
        public abstract Expression DescribingExpression { get; }
        public abstract In<TElem> this[int i] { get; }
        public abstract new VIn<TElem> this[Range r] { get; }
        public abstract int Size { get; }

        IIndexable<In<TElem>> IIndexable<In<TElem>>.this[Range r]
        {
            get { return this[r]; }
        }
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) output port.
    /// </summary>
    /// <typeparam name="TElem">type of single data element</typeparam>
    public interface IVOut<TElem> :
        IOut<Vector<TElem>>,
        IIndexable<Out<TElem>>
    {
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) output port.
    /// </summary>
    /// <typeparam name="TElem">type of single data element</typeparam>
    [MapToPort(EFlowDirection.Out)]
    [SignalArgument]
    [SignalField]
    public abstract class VOut<TElem> :
        Out<Vector<TElem>>,
        IVOut<TElem>
    {
        public abstract Out<TElem> this[int i] { get; }
        public abstract new VOut<TElem> this[Range r] { get; }
        public abstract int Size { get; }

        IIndexable<Out<TElem>> IIndexable<Out<TElem>>.this[Range r]
        {
            get { return this[r]; }
        }
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) bidirectioal port.
    /// </summary>
    /// <typeparam name="TElem">type of single data element</typeparam>
    public interface IVInOut<TElem> :
        IIndexable<InOut<TElem>>,
        IIn<Vector<TElem>>,
        IOut<Vector<TElem>>
    {
    }

    /// <summary>
    /// Models a vector-valued (i.e. one-dimensional) input/output port.
    /// </summary>
    /// <typeparam name="TElem">type of single data element</typeparam>
    [MapToPort(EFlowDirection.InOut)]
    [SignalArgument]
    [SignalField]
    public abstract class VInOut<TElem> :
        InOut<IIndexable<TElem>>,
        IVInOut<TElem>,
        IVIn<TElem>,
        IVOut<TElem>
    {
        #region private types

        private class InProxy : VIn<TElem>
        {
            private VInOut<TElem> _inout;

            public InProxy(VInOut<TElem> inout)
            {
                _inout = inout;
            }

            public override Vector<TElem> Cur
            {
                get { return _inout.Cur; }
            }

            public override Vector<TElem> Pre
            {
                get { return _inout.Pre; }
            }

            public override EventSource ChangedEvent
            {
                get { return _inout.ChangedEvent; }
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }

            public override In<TElem> this[int i]
            {
                get { return _inout[i]; }
            }

            public override VIn<TElem> this[Range r]
            {
                get { return _inout[r]; }
            }

            public override int Size
            {
                get { return _inout.Size; }
            }
        }

        private class OutProxy : VOut<TElem>
        {
            private VInOut<TElem> _inout;

            public OutProxy(VInOut<TElem> inout)
            {
                _inout = inout;
            }

            public override Out<TElem> this[int i]
            {
                get { return _inout[i]; }
            }

            public override VOut<TElem> this[Range r]
            {
                get { return _inout[r]; }
            }

            public override int Size
            {
                get { return _inout.Size; }
            }

            public override Vector<TElem> Next
            {
                set { _inout.Next = value; }
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }
        }

        #endregion private types

        public abstract Vector<TElem> Cur { get; }
        public abstract Vector<TElem> Pre { get; }
        public abstract Vector<TElem> Next { set; }

        public abstract InOut<TElem> this[int i] { get; }
        public abstract new VInOut<TElem> this[Range r] { get; }
        public abstract int Size { get; }

        IIndexable<InOut<TElem>> IIndexable<InOut<TElem>>.this[Range r]
        {
            get { return this[r]; }
        }

        IIndexable<In<TElem>> IIndexable<In<TElem>>.this[Range r]
        {
            get { return this[r]; }
        }

        IIndexable<Out<TElem>> IIndexable<Out<TElem>>.this[Range r]
        {
            get { return this[r]; }
        }

        /// <summary>
        /// Implicitly converts the bidirectional signal view to an input view.
        /// </summary>
        public static implicit operator VIn<TElem>(VInOut<TElem> inout)
        {
            return new InProxy(inout);
        }

        /// <summary>
        /// Implicitly converts the bidirectional signal view to an output view.
        /// </summary>
        public static implicit operator VOut<TElem>(VInOut<TElem> inout)
        {
            return new OutProxy(inout);
        }
    }

    public interface IMIn<T> :
        IIn<Matrix<T>>,
        IMatrixIndexable<In<T>>
    {
    }

    public interface IMOut<T> :
        IOut<Matrix<T>>,
        IMatrixIndexable<Out<T>>
    {
    }

    public interface IMInOut<T> :
        IMatrixIndexable<InOut<T>>,
        IIn<Matrix<T>>,
        IOut<Matrix<T>>
    {
    }

    public abstract class MIn<TElem> :
        In<Matrix<TElem>>,
        IMIn<TElem>
    {
        public abstract In<TElem> this[int i, int j] { get; }
        public new abstract VIn<TElem> this[int i, Range rj] { get; }
        public new abstract VIn<TElem> this[Range ri, int j] { get; }
        public new abstract MIn<TElem> this[Range ri, Range rj] { get; }

        IIndexable<In<TElem>> IMatrixIndexable<In<TElem>>.this[int i, Range rj]
        {
            get { return this[i, rj]; }
        }

        IIndexable<In<TElem>> IMatrixIndexable<In<TElem>>.this[Range ri, int j]
        {
            get { return this[ri, j]; }
        }

        IMatrixIndexable<In<TElem>> IMatrixIndexable<In<TElem>>.this[Range ri, Range rj]
        {
            get { return this[ri, rj]; }
        }
    }

    public abstract class MOut<TElem> :
        Out<Matrix<TElem>>,
        IMOut<TElem>
    {
        public abstract Out<TElem> this[int i, int j] { get; }
        public new abstract VOut<TElem> this[int i, Range rj] { get; }
        public new abstract VOut<TElem> this[Range ri, int j] { get; }
        public new abstract MOut<TElem> this[Range ri, Range rj] { get; }

        IIndexable<Out<TElem>> IMatrixIndexable<Out<TElem>>.this[int i, Range rj]
        {
            get { return this[i, rj]; }
        }

        IIndexable<Out<TElem>> IMatrixIndexable<In<TElem>>.this[Range ri, int j]
        {
            get { return this[ri, j]; }
        }

        IMatrixIndexable<Out<TElem>> IMatrixIndexable<In<TElem>>.this[Range ri, Range rj]
        {
            get { return this[ri, rj]; }
        }
    }

    /// <summary>
    /// Models a matrix-valued (i.e. two-dimensional) input/output port.
    /// </summary>
    /// <typeparam name="TElem">type of single data element</typeparam>
    [MapToPort(EFlowDirection.InOut)]
    [SignalArgument]
    [SignalField]
    public abstract class MInOut<TElem> :
        InOut<Matrix<TElem>>,
        IMInOut<TElem>,
        IMIn<TElem>,
        IMOut<TElem>
    {
        #region private types

        private class InProxy : MIn<TElem>
        {
            private MInOut<TElem> _inout;

            public InProxy(MInOut<TElem> inout)
            {
                _inout = inout;
            }

            public override Matrix<TElem> Cur
            {
                get { return _inout.Cur; }
            }

            public override Matrix<TElem> Pre
            {
                get { return _inout.Pre; }
            }

            public override EventSource ChangedEvent
            {
                get { return _inout.ChangedEvent; }
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }

            public override In<TElem> this[int i, int j]
            {
                get { return _inout[i, j]; }
            }

            public override VIn<TElem> this[int i, Range rj]
            {
                get { return _inout[i, rj]; }
            }

            public override VIn<TElem> this[Range ri, int j]
            {
                get { return _inout[ri, j]; }
            }

            public override MIn<TElem> this[Range ri, Range rj]
            {
                get { return _inout[ri, rj]; }
            }

            public override int Size0
            {
                get { return _inout.Size0; }
            }

            public override int Size1
            {
                get { return _inout.Size1; }
            }
        }

        private class OutProxy : MOut<TElem>
        {
            private MInOut<TElem> _inout;

            public OutProxy(MInOut<TElem> inout)
            {
                _inout = inout;
            }

            public override Out<TElem> this[int i, int j]
            {
                get { return _inout[i, j]; }
            }

            public override VOut<TElem> this[int i, Range rj]
            {
                get { return _inout[i, rj]; }
            }

            public override VOut<TElem> this[Range ri, int j]
            {
                get { return _inout[ri, j]; }
            }

            public override MOut<TElem> this[Range ri, Range rj]
            {
                get { return _inout[ri, rj]; }
            }

            public override int Size0
            {
                get { return _inout.Size0; }
            }

            public override int Size1
            {
                get { return _inout.Size1; }
            }

            public override Matrix<TElem> Next
            {
                set { _inout.Next = value; }
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }
        }

        #endregion private types

        public abstract int Size0 { get; }
        public abstract int Size1 { get; }

        public abstract Matrix<TElem> Pre { get; }
        public abstract Matrix<TElem> Cur { get; }
        public abstract Matrix<TElem> Next { set; }

        public abstract InOut<TElem> this[int i, int j] { get; }
        public new abstract VInOut<TElem> this[int i, Range rj] { get; }
        public new abstract VInOut<TElem> this[Range ri, int j] { get; }
        public new abstract MInOut<TElem> this[Range ri, Range rj] { get; }

        IIndexable<InOut<TElem>> IMatrixIndexable<InOut<TElem>>.this[int i, Range rj]
        {
            get { return this[i, rj]; }
        }

        IIndexable<InOut<TElem>> IMatrixIndexable<InOut<TElem>>.this[Range ri, int j]
        {
            get { return this[ri, j]; }
        }

        IMatrixIndexable<InOut<TElem>> IMatrixIndexable<InOut<TElem>>.this[Range ri, Range rj]
        {
            get { return this[ri, rj]; }
        }

        /// <summary>
        /// Implicitly converts the bidirectional signal view to an input view.
        /// </summary>
        public static implicit operator MIn<TElem>(MInOut<TElem> inout)
        {
            return new InProxy(inout);
        }

        /// <summary>
        /// Implicitly converts the bidirectional signal view to an output view.
        /// </summary>
        public static implicit operator MOut<TElem>(MInOut<TElem> inout)
        {
            return new OutProxy(inout);
        }
    }

    public interface IXIn<T> :
        IIn<Box<T>>,
        IMultiIndexable<In<T>>
    {
    }

    public interface IXOut<T> :
        IOut<Box<T>>,
        IMultiIndexable<Out<T>>
    {
    }

    public interface IXInOut<T> :
        IMultiIndexable<InOut<T>>,
        IIn<Box<T>>,
        IOut<Box<T>>
    {
    }

    public abstract class XIn<T> :
        In<Box<T>>,
        IXIn<T>
    {
        public abstract Vector<int> Sizes { get; }
        public abstract new XIn<T> this[params DimSpec[] indices] { get; }
        public abstract In<T> GetElement();

        IMultiIndexable<In<T>> IMultiIndexable<In<T>>.this[params DimSpec[] indices]
        {
            get { return this[indices]; }
        }
    }

    public abstract class XOut<T> :
        Out<Box<T>>,
        IXOut<T>
    {
        public abstract Vector<int> Sizes { get; }
        public new abstract XOut<T> this[params DimSpec[] indices] { get; }
        public abstract Out<T> GetElement();

        IMultiIndexable<Out<T>> IMultiIndexable<Out<T>>.this[params DimSpec[] indices]
        {
            get { return this[indices]; }
        }
    }

    public abstract class XInOut<TElem> :
        InOut<Box<TElem>>,
        IXInOut<TElem>,
        IXIn<TElem>,
        IXOut<TElem>
    {
        #region private types

        private class InProxy : XIn<TElem>
        {
            private XInOut<TElem> _inout;

            public InProxy(XInOut<TElem> inout)
            {
                _inout = inout;
            }

            public override Vector<int> Sizes
            {
                get { return _inout.Sizes; }
            }

            public override XIn<TElem> this[params DimSpec[] indices]
            {
                get { return _inout[indices]; }
            }

            public override In<TElem> GetElement()
            {
                return _inout.GetElement();
            }

            public override Box<TElem> Cur
            {
                get { return _inout.Cur; }
            }

            public override Box<TElem> Pre
            {
                get { return _inout.Pre; }
            }

            public override EventSource ChangedEvent
            {
                get { return _inout.ChangedEvent; }
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }
        }

        private class OutProxy : XOut<TElem>
        {
            private XInOut<TElem> _inout;

            public OutProxy(XInOut<TElem> inout)
            {
                _inout = inout;
            }

            public override Vector<int> Sizes
            {
                get { return _inout.Sizes; }
            }

            public override XOut<TElem> this[params DimSpec[] indices]
            {
                get { return _inout[indices]; }
            }

            public override Out<TElem> GetElement()
            {
                return _inout.GetElement();
            }

            public override Box<TElem> Next
            {
                set { _inout.Next = value; }
            }

            public override Expression DescribingExpression
            {
                get { return _inout.DescribingExpression; }
            }
        }

        #endregion private types

        public abstract Vector<int> Sizes { get; }
        public abstract new XInOut<TElem> this[params DimSpec[] indices] { get; }
        public abstract InOut<TElem> GetElement();

        IMultiIndexable<InOut<TElem>> IMultiIndexable<InOut<T>>.this[params DimSpec[] indices]
        {
            get { return this[indices]; }
        }

        IMultiIndexable<In<TElem>> IMultiIndexable<In<T>>.this[params DimSpec[] indices]
        {
            get { throw new NotImplementedException(); }
        }

        In<TElem> IMultiIndexable<In<TElem>>.GetElement()
        {
            throw new NotImplementedException();
        }

        IMultiIndexable<Out<TElem>> IMultiIndexable<Out<T>>.this[params DimSpec[] indices]
        {
            get { throw new NotImplementedException(); }
        }

        Out<TElem> IMultiIndexable<Out<TElem>>.GetElement()
        {
            throw new NotImplementedException();
        }

        public static implicit operator XIn<TElem>(XInOut<TElem> inout)
        {
            return new InProxy(inout);
        }

        public static implicit operator XOut<TElem>(XInOut<TElem> inout)
        {
            return new OutProxy(inout);
        }
    }

}

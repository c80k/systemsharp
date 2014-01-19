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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemSharp.DataTypes;
using SystemSharp.SysDOM;
using LinqEx = System.Linq.Expressions.Expression;

namespace SystemSharp.Components
{
    static class EventHelper
    {
        public static EventSource CreateChangedEvent<T>(IIn<T> provider, EventSource eventSource)
        {
            return new PredicatedEvent(null, eventSource, 
                () => !EqualityComparer<T>.Default.Equals(provider.Pre, provider.Cur),
                null);
        }
    }

    class UnaryOpIn<T> : In<T>
    {
        private IIn<T> _source;
        private Func<Expression, Expression> _operator;
        private Func<T, T> _elementOperator;
        private EventSource _changedEvent;

        public UnaryOpIn(IIn<T> source, Func<Expression, Expression> op, Func<T, T> elementOperator)
        {
            _source = source;
            _operator = op;
            _elementOperator = op.Compile<T, T>();
            _changedEvent = EventHelper.CreateChangedEvent(this, source.ChangedEvent);
        }

        public override T Cur
        {
            get { return _elementOperator(_source.Cur); }
        }

        public override T Pre
        {
            get { return _elementOperator(_source.Pre); }
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public override SysDOM.Expression DescribingExpression
        {
            get { return _operator(_source.DescribingExpression); }
        }
    }

    class BinaryOpIn<T> : In<T>
    {
        private IIn<T> _left;
        private IIn<T> _right;
        private Func<Expression, Expression, Expression> _operator;
        private Func<T, T, T> _elementOperator;
        private EventSource _changedEvent;

        public BinaryOpIn(IIn<T> left, IIn<T> right, Func<Expression, Expression, Expression> op,
            Func<T, T, T> elementOperator)
        {
            _left = left;
            _right = right;
            _operator = op;
            _elementOperator = elementOperator;
            _changedEvent = EventHelper.CreateChangedEvent(this, left.ChangedEvent | right.ChangedEvent);
        }

        public override T Cur
        {
            get { return _elementOperator(_left.Cur, _right.Cur); }
        }

        public override T Pre
        {
            get { return _elementOperator(_left.Pre, _right.Pre); }
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public override Expression DescribingExpression
        {
            get { return _operator(_left.DescribingExpression, _right.DescribingExpression); }
        }
    }

    class UnaryOpVIn<T> : VIn<T>
    {
        private IVIn<T> _source;
        private Func<Expression, Expression> _operator;
        private Func<T, T> _elementOperator;
        private Func<Vector<T>, Vector<T>> _vectorOperator;
        private EventSource _changedEvent;

        public UnaryOpVIn(IVIn<T> source, 
            Func<Expression, Expression> op, 
            Func<T, T> elementOperator, 
            Func<Vector<T>, Vector<T>> vectorOperator)
        {
            _source = source;
            _operator = op;
            _elementOperator = elementOperator;
            _vectorOperator = vectorOperator;
            _changedEvent = EventHelper.CreateChangedEvent(this, source.ChangedEvent);
        }

        public override Vector<T> Cur
        {
            get { return _vectorOperator(_source.Cur); }
        }

        public override Vector<T> Pre
        {
            get { return _vectorOperator(_source.Pre); }
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public override In<T> this[int i]
        {
            get 
            { 
                var indexer = new VInIndexer<T>(_source, i);
                return new UnaryOpIn<T>(indexer, _operator, _elementOperator); 
            }
        }

        public override VIn<T> this[Range r]
        {
            get { return new VInRangeIndexer<T>(this, r); }
        }

        public override int Size
        {
            get { return _source.Size; }
        }

        public override Expression DescribingExpression
        {
            get { throw new NotImplementedException(); }
        }
    }

    class BinaryOpVIn<T> : VIn<T>
    {
        private IVIn<T> _leftSource;
        private IVIn<T> _rightSource;
        private Func<Expression, Expression, Expression> _operator;
        private Func<T, T, T> _elementOperator;
        private Func<Vector<T>, Vector<T>, Vector<T>> _vectorOperator;
        private EventSource _changedEvent;

        public BinaryOpVIn(IVIn<T> leftSource, IVIn<T> rightSource,
            Func<Expression, Expression, Expression> op,
            Func<T, T, T> elementOperator,
            Func<Vector<T>, Vector<T>, Vector<T>> vectorOperator)
        {
            _leftSource = leftSource;
            _rightSource = rightSource;
            _operator = op;
            _elementOperator = elementOperator;
            _vectorOperator = vectorOperator;
            _changedEvent = EventHelper.CreateChangedEvent(this, 
                _leftSource.ChangedEvent | _rightSource.ChangedEvent);
        }

        public override Vector<T> Cur
        {
            get { return _vectorOperator(_leftSource.Cur, _rightSource.Cur); }
        }

        public override Vector<T> Pre
        {
            get { return _vectorOperator(_leftSource.Pre, _rightSource.Pre); }
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public override In<T> this[int i]
        {
            get
            {
                var leftIndexer = new VInIndexer<T>(_leftSource, i);
                var rightIndexer = new VInIndexer<T>(_rightSource, i);
                return new BinaryOpIn<T>(leftIndexer, rightIndexer, _operator, _elementOperator);
            }
        }

        public override VIn<T> this[Range r]
        {
            get { return new VInRangeIndexer<T>(this, r); }
        }

        public override int Size
        {
            get { return _leftSource.Size; }
        }

        public override Expression DescribingExpression
        {
            get { throw new NotImplementedException(); }
        }
    }

    class VInIndexer<T> : In<T>
    {
        private IVIn<T> _source;
        private int _offset;

        public VInIndexer(IVIn<T> source, int offset)
        {
            _source = source;
            _offset = offset;
        }

        protected override EventSource GetSourceEvent()
        {
            return _source.ChangedEvent;
        }

        public override T Cur
        {
            get { return _source.Cur[_offset]; }
        }

        public override T Pre
        {
            get { return _source.Pre[_offset]; }
        }
    }

    class VInRangeIndexer<T> : VIn<T>
    {
        private VIn<T> _source;
        private Range _projRange;
        private EventSource _changedEvent;

        public VInRangeIndexer(VIn<T> source, Range projRange)
        {
            _source = source;
            _projRange = projRange;
            _changedEvent = EventHelper.CreateChangedEvent(this, source.ChangedEvent);
        }

        public override Vector<T> Cur
        {
            get { return _source.Cur[_projRange]; }
        }

        public override Vector<T> Pre
        {
            get { return _source.Pre[_projRange]; }
        }

        public override EventSource ChangedEvent
        {
            get { return _changedEvent; }
        }

        public override Expression DescribingExpression
        {
            get { throw new NotImplementedException(); }
        }

        public override In<T> this[int i]
        {
            get { return new VInIndexer<T>(_source, _projRange.Unproject(i)); }
        }

        public override VIn<T> this[Range r]
        {
            get { return new VInRangeIndexer<T>(_source, _projRange.Unproject(r)); }
        }

        public override int Size
        {
            get { return (int)_projRange.Size; }
        }
    }
}

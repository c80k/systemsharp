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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemSharp.Collections
{
    public class HashableSequence<T>
    {
        private IEnumerable<T> _items;

        public HashableSequence(IEnumerable<T> items)
        {
            _items = items;
        }

        public override int GetHashCode()
        {
            return _items.GetSequenceHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as HashableSequence<T>;
            if (other == null)
                return false;

            return _items.SequenceEqual(other._items);
        }

        public override string ToString()
        {
            return string.Join(", ", _items);
        }
    }
}

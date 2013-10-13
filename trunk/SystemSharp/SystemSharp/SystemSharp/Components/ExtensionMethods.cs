﻿/**
 * Copyright 2011-2013 Christian Köllner, David Hlavac
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

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace SystemSharp.Components
{
    public static class AwaitableExtensionMethods
    {
        public static IAwaitable GetAwaiter(this IInPort signal)
        {
            return signal.ChangedEvent.GetAwaiter();
        }

        public static IAwaitable GetAwaiter(this IEnumerable<AbstractEvent> events)
        {
            return new MultiEvent(null, events).GetAwaiter();
        }

        public static IAwaitable GetAwaiter(this AbstractEvent[] events)
        {
            //because of a compiler bug (see http://stackoverflow.com/questions/14198019/await-array-by-implementing-extension-method-for-array)
            //await events[] cannot be compiled, even though this extension method allows it an intellisense says that "(awaitable) AbstractEvent[]"

            return new MultiEvent(null, events).GetAwaiter();
        }

        public static IAwaitable GetAwaiter(this IEnumerable<IInPort> signals)
        {
            return new MultiEvent(null, DesignContext.MakeEventList(signals)).GetAwaiter();
        }
        
        public static IAwaitable GetAwaiter(this IInPort[] signals)
        {
            //because of a compiler bug (see http://stackoverflow.com/questions/14198019/await-array-by-implementing-extension-method-for-array)
            //await signals[] cannot be compiled, even though this extension method allows it an intellisense says that "(awaitable) IInPort[]"

            return new MultiEvent(null, DesignContext.MakeEventList(signals)).GetAwaiter();
        }

        [MapToWaitNTicksRewriteAwait]
        [MapToWaitNTicksRewriteCall]
        public static async Task Ticks(this int n)
        {
            for (int i = 0; i < n; i++)
            {
                await Component.Tick;
            }
        }
    }
}
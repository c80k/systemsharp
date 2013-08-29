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


namespace SystemSharp.Components.Std
{
    public class ConsoleLogger<T> : Component
    {
        public In<T> DataIn { private get; set; }

        private void Process()
        {
            DesignContext.WriteLine(Context.CurTime.ToString() + ": " + DataIn.Cur.ToString());
        }

        protected override void Initialize()
        {
            AddProcess(Process, DataIn.ChangedEvent);
        }
    }
}
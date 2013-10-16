/**
 * Copyright 2011-2012 Christian Köllner
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
using SystemSharp.Assembler;
using SystemSharp.Collections;

namespace SystemSharp.Components.FU
{
    /// <summary>
    /// A reservation table manages the reservation time intervals of a single functional unit.
    /// </summary>
    public class ReservationTable    
    {
        /// <summary>
        /// A single reservation record: start and end time of reservation, associated instruction
        /// </summary>
        public class Reservation
        {
            /// <summary>
            /// Start time of reservation
            /// </summary>
            public long StartTime { get; private set; }

            /// <summary>
            /// End time of reservation
            /// </summary>
            public long EndTime { get; private set; }

            /// <summary>
            /// Associated instruction
            /// </summary>
            public XIL3Instr Instr { get; private set; }

            /// <summary>
            /// Constructs a new instance
            /// </summary>
            /// <param name="startTime">start time of reservation</param>
            /// <param name="endTime">end time of reservation</param>
            /// <param name="instr">associated instruction</param>
            public Reservation(long startTime, long endTime, XIL3Instr instr)
            {
                StartTime = startTime;
                EndTime = endTime;
                Instr = instr;
            }

            public override string  ToString()
            {
 	             return string.Format("{0} - {1}: {2}", StartTime, EndTime, Instr.Name);
            }
        }

        private List<Reservation> _resList = new List<Reservation>();
        private IntervalSet _rset = new IntervalSet();

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public ReservationTable()
        {
        }

        /// <summary>
        /// Tests whether there is any reservation which overlaps the specified time interval.
        /// </summary>
        /// <param name="startTime">start time to test</param>
        /// <param name="endTime">end time to test</param>
        /// <returns>true if there is any reservation which overlaps the specified time interval</returns>
        public bool IsReserved(long startTime, long endTime)
        {
            if (startTime > endTime)
                return false;

            if (_rset.Intersects((int)startTime, (int)endTime))
                return true;

            return false;
        }

        /// <summary>
        /// Tries to add a reservation for the specified time interval. If there is an existing reservation for that interval,
        /// the state of this object will not be changed.
        /// </summary>
        /// <param name="startTime">reservation start time</param>
        /// <param name="endTime">reservation end time</param>
        /// <param name="instr">associated instruction</param>
        /// <returns>true if reservation was successful, i.e. no colliding reservation</returns>
        public bool TryReserve(long startTime, long endTime, XIL3Instr instr)
        {
            if (IsReserved(startTime, endTime))
                return false;

            _rset.Add((int)startTime, (int)endTime);
            _resList.Add(new Reservation(startTime, endTime, instr));

            return true;
        }

        /// <summary>
        /// Returns the list of reservation records (not sorted)
        /// </summary>
        public IList<Reservation> Reservations
        {
            get { return _resList; }
        }

        /// <summary>
        /// Occupation is defined as the amount of c-steps where this table is reserved.
        /// </summary>
        public long GetOccupation()
        {
            return Reservations.Sum(res => res.EndTime - res.StartTime + 1);
        }

        /// <summary>
        /// Utilization is defined as the ratio occupation and total schedule length.
        /// </summary>
        /// <param name="ncsteps">total schedule length</param>
        public double GetUtilization(long ncsteps)
        {
            return (double)GetOccupation() / ncsteps;
        }
    }
}

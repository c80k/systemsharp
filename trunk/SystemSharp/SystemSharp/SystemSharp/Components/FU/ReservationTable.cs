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
    public class ReservationTable
    {        
        public class Reservation
        {
            public long StartTime { get; private set; }
            public long EndTime { get; private set; }
            public XIL3Instr Instr { get; private set; }

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

        public ReservationTable()
        {
        }

        public bool IsReserved(long startTime, long endTime, XIL3Instr instr)
        {
            if (startTime > endTime)
                return false;

            if (_rset.Intersects((int)startTime, (int)endTime))
                return true;

            return false;
        }

        public bool TryReserve(long startTime, long endTime, XIL3Instr instr)
        {
            if (IsReserved(startTime, endTime, instr))
                return false;

            _rset.Add((int)startTime, (int)endTime);
            _resList.Add(new Reservation(startTime, endTime, instr));

            return true;
        }

        public IList<Reservation> Reservations
        {
            get { return _resList; }
        }
    }

    public static class ReservationTableStatistics
    {
        public static long GetOccupation(this ReservationTable rtbl)
        {
            return rtbl.Reservations.Sum(res => res.EndTime - res.StartTime + 1);
        }

        public static double GetUtilization(this ReservationTable rtbl, long ncsteps)
        {
            return (double)GetOccupation(rtbl) / ncsteps;
        }
    }
}

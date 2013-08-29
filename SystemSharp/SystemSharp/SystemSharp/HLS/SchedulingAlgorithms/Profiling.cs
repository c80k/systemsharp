/**
 * Copyright 2012-2013 Christian Köllner
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
using System.Reflection;
using System.Text;
using SystemSharp.Analysis;
using SystemSharp.Analysis.Msil;
using SystemSharp.Components;
using SystemSharp.Meta;
using SystemSharp.SchedulingAlgorithms;
using SystemSharp.SysDOM;

namespace SystemSharp.Assembler
{
    public class ScheduleProfiler
    {
        public string Name { get; private set; }
        internal ILIndexRef FirstILIndex { get; set; }
        internal ILIndexRef LastILIndex { get; set; }

        public long FirstCStep { get; internal set; }
        public long LastCStep { get; internal set; }
        public bool IsValid { get; internal set; }
        
        public long CStepSpan
        {
            get { return LastCStep - FirstCStep + 1; }
        }

        public ScheduleProfiler(string name)
        {
            Name = name;
        }

        internal void ExtractFrom(XIL3Function func, XILSchedulingAdapter xsa)
        {
            if (FirstILIndex == null || LastILIndex == null)
            {
                IsValid = false;
                return;
            }
            var firstInstr = func.Instructions.Where(i => i.CILRef >= FirstILIndex).FirstOrDefault();
            var lastInstr = func.Instructions.Where(i => i.CILRef <= LastILIndex).LastOrDefault();
            if (firstInstr == null || lastInstr == null)
            {
                FirstCStep = 1;
                LastCStep = 0;
                IsValid = false;
                return;
            }
            FirstCStep = xsa.CStep[firstInstr];
            LastCStep = xsa.CStep[lastInstr];
            IsValid = true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Name + ": from ");
            sb.Append(FirstILIndex == null ? "?" : FirstILIndex.ToString());
            sb.Append(" to ");
            sb.Append(LastILIndex == null ? "?" : LastILIndex.ToString());
            sb.Append(", valid: " + IsValid + ", ");
            sb.Append("first c-step: " + FirstCStep + ", ");
            sb.Append("last c-step: " + LastCStep);
            if (IsValid)
            {
                sb.Append(", span: " + CStepSpan);
            }
            return sb.ToString();
        }
    }

    public static class SchedulingProfilers
    {
        private class ProfileRewriter: RewriteCall
        {
            private bool _isBegin;

            public ProfileRewriter(bool isBegin)
            {
                _isBegin = isBegin;
            }

            public override bool Rewrite(CodeDescriptor decompilee, MethodBase callee, StackElement[] args, IDecompiler stack, IFunctionBuilder builder)
            {
                var elemProf = args[0];
                if (elemProf.Variability != EVariability.Constant)
                    throw new NotSupportedException("Profiler must be derivable as constant value");

                var prof = (ScheduleProfiler)elemProf.Sample;
                var ilRef = new ILIndexRef(decompilee.Method, stack.CurrentILIndex);
                if (_isBegin)
                {
                    if (prof.FirstILIndex != null)
                        throw new InvalidOperationException("Profiling of " + prof.Name + " already has start position at " + prof.FirstILIndex);
                    prof.FirstILIndex = ilRef;
                }
                else
                {
                    if (prof.LastILIndex != null)
                        throw new InvalidOperationException("Profiling of " + prof.Name + " already has end position at " + prof.LastILIndex);
                    prof.LastILIndex = ilRef;
                }

                var constraints = builder.ResultFunction.QueryAttribute<SchedulingConstraints>();
                if (constraints == null)
                {
                    constraints = new SchedulingConstraints();
                    builder.ResultFunction.AddAttribute(constraints);
                }
                if (!constraints.Profilers.Contains(prof))
                    constraints.Profilers.Add(prof);

                return true;
            }
        }

        [ProfileRewriter(true)]
        public static void StartProfile(ScheduleProfiler prof)
        {
        }

        [ProfileRewriter(false)]
        public static void StopProfile(ScheduleProfiler prof)
        {
        }
    }
}

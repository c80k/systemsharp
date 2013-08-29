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
 * 
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SystemSharp.DSE
{
    public class Alternative
    {
        public Action StepAction { get; private set; }
        public string Name { get; private set; }

        internal Alternative(Action stepAction, string name)
        {
            StepAction = stepAction;
            Name = name;
        }
    }

    public class DSETask
    {
        private List<Alternative> _alternatives = new List<Alternative>();

        public string Name { get; private set; }

        public DSETask(string name)
        {
            Name = name;
        }

        public Alternative AddAlternative(Action stepAction, string name)
        {
            var alt = new Alternative(stepAction, name);
            _alternatives.Add(alt);
            return alt;
        }

        public IEnumerable<Alternative> Alternatives
        {
            get { return _alternatives.AsEnumerable(); }
        }
    }

    public interface IDSEObserver
    {
        void NotifySpaceSize(long totalNumAlts);
        void OnBeginFlow(IEnumerable<Alternative> activeAlts, long progress);
        void OnBeginTask(DSETask task, Alternative alt, long progress);
        void OnEndOfDSE();
    }

    public class DesignSpaceExplorer
    {
        private List<DSETask> _tasks = new List<DSETask>();

        public DSETask AddTask(string name)
        {
            var task = new DSETask(name);
            _tasks.Add(task);
            return task;
        }

        public IEnumerable<Alternative> Enumerate(IDSEObserver obs)
        {
            var enums = _tasks.Select(t => t.Alternatives.GetEnumerator()).ToArray();
            bool emptyAlts = false;
            foreach (var iter in enums)
            {
                if (!iter.MoveNext())
                {
                    emptyAlts = true;
                    break;
                }
            }
            if (emptyAlts)
                yield break;

            long progress = 0;
            bool end = false;
            do
            {
                if (obs != null)
                    obs.OnBeginFlow(enums.Select(iter => iter.Current), progress);

                for (int i = 0; i < enums.Length; i++)
                {
                    if (obs != null)
                        obs.OnBeginTask(_tasks[i], enums[i].Current, progress++);
                    yield return enums[i].Current;
                }

                end = true;
                foreach (var iter in enums)
                {
                    if (iter.MoveNext())
                    {
                        end = false;
                        break;
                    }
                    else
                    {
                        iter.Reset();
                        iter.MoveNext();
                    }
                }
            } while (!end);

            if (obs != null)
                obs.OnEndOfDSE();
        }

        public void Explore(IDSEObserver obs = null)
        {
            if (obs != null)
            {
                long spaceSize = Enumerate(null).LongCount();
                obs.NotifySpaceSize(spaceSize);
            }

            foreach (var alt in Enumerate(obs))
                alt.StepAction();
        }
    }
}

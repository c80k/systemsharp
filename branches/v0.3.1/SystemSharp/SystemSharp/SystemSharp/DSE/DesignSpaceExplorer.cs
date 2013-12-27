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
    /// <summary>
    /// Encapsulates a possible task action during design-space exploration.
    /// </summary>
    public class Alternative
    {
        /// <summary>
        /// Action to be performed by this alternative
        /// </summary>
        public Action StepAction { get; private set; }

        /// <summary>
        /// User defined name of this alternative
        /// </summary>
        public string Name { get; private set; }

        internal Alternative(Action stepAction, string name)
        {
            StepAction = stepAction;
            Name = name;
        }
    }

    /// <summary>
    /// Encapsulates a single step during design-space exploration, consisting of one or more alternatives.
    /// </summary>
    public class DSETask
    {
        private List<Alternative> _alternatives = new List<Alternative>();

        /// <summary>
        /// User-defined name of this task
        /// </summary>
        public string Name { get; private set; }

        internal DSETask(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Adds an alternative to this task
        /// </summary>
        /// <param name="stepAction">action to be performed</param>
        /// <param name="name">user-defined name</param>
        /// <returns>instance representing the added alternative</returns>
        public Alternative AddAlternative(Action stepAction, string name)
        {
            var alt = new Alternative(stepAction, name);
            _alternatives.Add(alt);
            return alt;
        }

        /// <summary>
        /// Returns all currently available alternatives for this task.
        /// </summary>
        public IEnumerable<Alternative> Alternatives
        {
            get { return _alternatives.AsEnumerable(); }
        }
    }

    /// <summary>
    /// An exploration observer gets notified about all currently performed actions during design-space exploration.
    /// </summary>
    public interface IDSEObserver
    {
        /// <summary>
        /// Tells about the size of the design space.
        /// </summary>
        /// <param name="totalNumAlts">total number of alternatives which will be executed during exploration</param>
        void NotifySpaceSize(long totalNumAlts);

        /// <summary>
        /// Notifies about the start of the next design flow, i.e. execution of the first task.
        /// </summary>
        /// <param name="activeAlts">active alternatives for the new flow</param>
        /// <param name="progress">number of alternatives which were executed so far</param>
        void OnBeginFlow(IEnumerable<Alternative> activeAlts, long progress);

        /// <summary>
        /// Notifies about the start of the new task during the current design flow.
        /// </summary>
        /// <param name="task">task which is about to be executed</param>
        /// <param name="alt">alternative selected from task</param>
        /// <param name="progress">number of alternatives which were executed so far</param>
        void OnBeginTask(DSETask task, Alternative alt, long progress);

        /// <summary>
        /// Notifies about the completion of the whole exploration.
        /// </summary>
        void OnEndOfDSE();
    }

    /// <summary>
    /// Provides capabilities for configuring and exploring a design space.
    /// </summary>
    /// <remarks>
    /// Design space exploration is the process of generating and evaluating multiple realizations of some design.
    /// It is up to the user a describe the actual design space and to perform the actual steps of generating and
    /// evaluating the current realization alternative. The purpose of this class is just to enumerate the different
    /// realization alternatives which are configured. The design space model is as follows: the generation of some
    /// alternative is composed of multiple tasks, whereby each task consists of some alternative actions. Therefore,
    /// the design space consists of all possible sequences of actions which arise from the description. Let's say
    /// a realization consists of tasks T1 and T2, whereby T1 is a choice between actions A1 and A2. Similarly,
    /// T2 is a choice between B1 and B2. The design space is then defined by the 4 combinations [A1, B1], [A1, B2],
    /// [A2, B1], and [A2, B2].
    /// </remarks>
    public class DesignSpaceExplorer
    {
        private List<DSETask> _tasks = new List<DSETask>();

        /// <summary>
        /// Adds the next task to this instance.
        /// </summary>
        /// <param name="name">user-defined name of task</param>
        /// <returns>the added task, providing detailed configuration</returns>
        public DSETask AddTask(string name)
        {
            var task = new DSETask(name);
            _tasks.Add(task);
            return task;
        }

        /// <summary>
        /// Enumerates all possible sequences of alternatives
        /// </summary>
        /// <param name="obs">optional exploration observer</param>
        /// <returns>a concatenation of all possible sequences of exploration alternatives</returns>
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

        /// <summary>
        /// Performs a design-space exploration
        /// </summary>
        /// <param name="obs">optional exploration observer</param>
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

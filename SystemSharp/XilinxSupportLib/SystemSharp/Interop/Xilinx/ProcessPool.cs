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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SystemSharp.Interop.Xilinx
{
    /// <summary>
    /// Provides infrastructure for executing multiple command-line tools in parallel.
    /// </summary>
    public class ProcessPool
    {
        /// <summary>
        /// Describes the exeuction state of a particular tool.
        /// </summary>
        public enum EToolState
        {
            /// <summary>
            /// The tool has not yet started.
            /// </summary>
            WaitingForExecution,

            /// <summary>
            /// The tool failed to start.
            /// </summary>
            StartFailure,

            /// <summary>
            /// The tool is running.
            /// </summary>
            Running,

            /// <summary>
            /// The tool has exited.
            /// </summary>
            Exited
        }

        /// <summary>
        /// Encapsulates the execution of a command-line tool.
        /// </summary>
        public class Tool
        {
            public static int DefaultCompletionTimeout = 180000;

            private ProcessPool _pool;
            private Process _ps;
            private ManualResetEvent _isStarted;
            private ManualResetEvent _isExited;
            private object _syncRoot;
            private volatile EToolState _state;
            private string _name;

            internal Tool(ProcessPool pool, Process ps)
            {
                _pool = pool;
                _ps = ps;
                _isStarted = new ManualResetEvent(false);
                _isExited = new ManualResetEvent(false);
                _syncRoot = new object();

                _ps.OutputDataReceived += DataReceivedEventHandler;
                _ps.ErrorDataReceived += ErrorReceivedEventHandler;
                _ps.EnableRaisingEvents = true;

                ConsoleID = -1;
            }

            ~Tool()
            {
                _isStarted.Close();
                _isExited.Close();
            }

            /// <summary>
            /// An ID of the console the tool is running in.
            /// </summary>
            public int ConsoleID { get; internal set; }

            private void DataReceivedEventHandler(object sender, DataReceivedEventArgs e)
            {
                lock (_syncRoot)
                {
                    _pool.OnToolOutput(this, e.Data);
                }
            }

            private void ErrorReceivedEventHandler(object sender, DataReceivedEventArgs e)
            {
                lock (_syncRoot)
                {
                    _pool.OnToolErrorOutput(this, e.Data);
                }
            }

            internal void Run()
            {
                using (_ps)
                {
                    bool started;
                    lock (_syncRoot)
                    {
                        started = _ps.Start();
                        if (started)
                        {
                            try
                            {
                                _name = _ps.ProcessName;
                                _ps.PriorityClass = ProcessPriorityClass.BelowNormal;
                            }
                            catch (Exception)
                            {
                                // The properties are only accessible if the process is running.
                                // We're actually in a race condition: It might happen that the
                                // process already exited when we access those properties. In
                                // this case, an exception will be thrown.
                            }
                            _state = EToolState.Running;
                            _pool.OnToolSpawned(this);
                            _ps.BeginOutputReadLine();
                            _ps.BeginErrorReadLine();
                        }
                        else
                        {
                            _state = EToolState.StartFailure;
                            _pool.OnFailedToolStart(this);
                        }
                        _isStarted.Set();
                    }
                    if (started)
                    {
                        _ps.WaitForExit();
                        lock (_syncRoot)
                        {
                            ExitCode = _ps.ExitCode;
                            _state = EToolState.Exited;
                            _pool.OnToolExited(this);
                        }
                    }
                    _isExited.Set();
                }
            }

            /// <summary>
            /// Returns the current execution state of the tool.
            /// </summary>
            public EToolState State
            {
                get { return _state; }
            }

            /// <summary>
            /// Returns the exit code of the tool.
            /// </summary>
            public int ExitCode { get; private set; }

            /// <summary>
            /// Suspends the current thread until the tool has exited.
            /// </summary>
            public void WaitForExit()
            {
                if (!_isExited.WaitOne(DefaultCompletionTimeout))
                {
                    Debug.WriteLine("Process " + _ps.ProcessName + " did not complete - about to kill");
                    _ps.Kill();
                }
            }

            /// <summary>
            /// Returns the tool name.
            /// </summary>
            public string Name
            {
                get { return _name; }
            }
        }

        /// <summary>
        /// Represents a batch job of tools.
        /// </summary>
        public class ToolBatch
        {
            private ProcessPool _pool;
            private List<Tool> _tools = new List<Tool>();
            private bool _sealed;

            internal ToolBatch(ProcessPool pool)
            {
                _pool = pool;
                _consoleID = -1;
            }

            private int _consoleID;

            /// <summary>
            /// Gets or sets the console ID the batch is executing in.
            /// </summary>
            public int ConsoleID
            {
                get { return _consoleID; }
                set
                {
                    _consoleID = value;
                    foreach (var tool in _tools)
                    {
                        tool.ConsoleID = value;
                    }
                }
            }

            /// <summary>
            /// Adds a tool to the batch job.
            /// </summary>
            /// <param name="iseDir">ISE installation path</param>
            /// <param name="projDir">project directory</param>
            /// <param name="toolName">tool name</param>
            /// <param name="arguments">command line arguments</param>
            /// <returns>a model of the added tool</returns>
            public Tool Add(string iseDir, string projDir, string toolName, string arguments)
            {
                if (_sealed)
                    throw new InvalidOperationException("Tool batch is already queued");

                string exe = Path.Combine(iseDir, toolName);
                var ps = new System.Diagnostics.Process();
                ps.StartInfo.Arguments = arguments;
                ps.StartInfo.CreateNoWindow = true;
                ps.StartInfo.FileName = exe;
                ps.StartInfo.RedirectStandardError = true;
                ps.StartInfo.RedirectStandardInput = true;
                ps.StartInfo.RedirectStandardOutput = true;
                if (projDir != null)
                    ps.StartInfo.WorkingDirectory = projDir;
                ps.StartInfo.UseShellExecute = false;
                var tool = new Tool(_pool, ps);
                _tools.Add(tool);
                return tool;
            }

            /// <summary>
            /// Adds a tool to the batch job.
            /// </summary>
            /// <param name="toolName">tool name</param>
            /// <param name="arguments">command line arguments</param>
            /// <returns>a model of the added tool</returns>
            public Tool Add(string toolName, string arguments)
            {
                return Add("", null, toolName, arguments);
            }

            /// <summary>
            /// Enumerates all currently added tools.
            /// </summary>
            public IEnumerable<Tool> Tools
            {
                get { return new ReadOnlyCollection<Tool>(_tools); }
            }

            internal void Run()
            {
                foreach (var tool in _tools)
                {
                    tool.Run();
                }
            }

            /// <summary>
            /// Queues the batch job in the process pool.
            /// </summary>
            public void Start()
            {
                _sealed = true;
                _pool._psq.Add(this);
            }
        }

        private static int _maxParallelProcesses;

        /// <summary>
        /// Configures the maximum number of tools which are allowed to execute in parallel.
        /// </summary>
        public static int MaxParallelProcessesPreset
        {
            get
            {
                if (_maxParallelProcesses == 0)
                    _maxParallelProcesses = SubstManager.Instance.MaxDrives;
                return _maxParallelProcesses;
            }
            set
            {
                _maxParallelProcesses = value;
            }
        }

        private static object _syncRoot = new object();
        private static ProcessPool _instance;

        /// <summary>
        /// Returns the singleton process pool instance.
        /// </summary>
        public static ProcessPool Instance
        {
            get
            {
                lock (_syncRoot)
                {
                    if (_instance == null)
                        _instance = new ProcessPool(MaxParallelProcessesPreset);
                    return _instance;
                }
            }
        }

        private BlockingCollection<ToolBatch> _psq;
        private Action<Tool> _onToolSpawned;
        private Action<Tool> _onFailedToolStart;
        private Action<Tool, string> _onToolOutput;
        private Action<Tool, string> _onToolErrorOutput;
        private Action<Tool> _onToolExited;

        /// <summary>
        /// The maximum number of tools which are allowed to execute in parallel.
        /// </summary>
        public int MaxParallelProcesses { get; private set; }

        /// <summary>
        /// Constructs a new process pool.
        /// </summary>
        /// <param name="maxParallelProcesses">maximum number of tools which are allowed to execute in parallel</param>
        public ProcessPool(int maxParallelProcesses)
        {
            _psq = new BlockingCollection<ToolBatch>();

            MaxParallelProcesses = maxParallelProcesses;
            for (int i = 0; i < maxParallelProcesses; i++)
            {
                new Task(Worker, i).Start();
            }

            ConnectStandardConsole();
        }

        private void Worker(object id)
        {
            int nid = (int)id;

            while (true)
            {
                var batch = _psq.Take();
                batch.ConsoleID = nid;
                batch.Run();
            }
        }

        private void OnToolSpawned(Tool tool)
        {
            if (_onToolSpawned != null)
                _onToolSpawned(tool);
        }

        private void OnFailedToolStart(Tool tool)
        {
            if (_onFailedToolStart != null)
                _onFailedToolStart(tool);
        }

        private void OnToolOutput(Tool tool, string output)
        {
            if (_onToolOutput != null)
                _onToolOutput(tool, output);
        }

        private void OnToolErrorOutput(Tool tool, string output)
        {
            if (_onToolErrorOutput != null)
                _onToolErrorOutput(tool, output);
        }

        private void OnToolExited(Tool tool)
        {
            if (_onToolExited != null)
                _onToolExited(tool);
        }

        /// <summary>
        /// Queues a single tool.
        /// </summary>
        /// <param name="iseDir">ISE installation path</param>
        /// <param name="projDir">project directory</param>
        /// <param name="toolName">tool name</param>
        /// <param name="arguments">command line arguments</param>
        /// <returns>a model of the queued tool</returns>
        public Tool ToolExec(string iseDir, string projDir, string toolName, string arguments)
        {
            var batch = new ToolBatch(this);
            var tool = batch.Add(iseDir, projDir, toolName, arguments);
            batch.Start();
            return tool;
        }

        /// <summary>
        /// Queues a single tool.
        /// </summary>
        /// <param name="toolName">tool name</param>
        /// <param name="arguments">command line arguments</param>
        /// <returns>a model of the queued tool</returns>
        public Tool ToolExec(string toolName, string arguments)
        {
            return ToolExec("", null, toolName, arguments);
        }

        /// <summary>
        /// Creates a new batch job.
        /// </summary>
        public ToolBatch CreateBatch()
        {
            return new ToolBatch(this);
        }

        /// <summary>
        /// Triggered whenever a new tool has started.
        /// </summary>
        public event Action<Tool> ToolSpawned
        {
            add { _onToolSpawned += value; }
            remove { _onToolSpawned -= value; }
        }

        /// <summary>
        /// Triggered whenever a tool has failed to start.
        /// </summary>
        public event Action<Tool> FailedToolStart
        {
            add { _onFailedToolStart += value; }
            remove { _onFailedToolStart -= value; }
        }

        /// <summary>
        /// Triggered whenever a tool writes to the console.
        /// </summary>
        public event Action<Tool, string> ToolOutput
        {
            add { _onToolOutput += value; }
            remove { _onToolOutput -= value; }
        }

        /// <summary>
        /// Triggered whenever a tool writes to the error console.
        /// </summary>
        public event Action<Tool, string> ToolErrorOutput
        {
            add { _onToolErrorOutput += value; }
            remove { _onToolErrorOutput -= value; }
        }

        /// <summary>
        /// Triggered whenever a tool has exited.
        /// </summary>
        public event Action<Tool> ToolExited
        {
            add { _onToolExited += value; }
            remove { _onToolExited -= value; }
        }

        private void DefaultToolOutputHandler(Tool tool, string output)
        {
            Console.WriteLine("[{0}] {1}", tool.ConsoleID, output);
        }

        private void DefaultToolErrorOutputHandler(Tool tool, string output)
        {
            Console.Error.WriteLine("[{0}] {1}", tool.ConsoleID, output);
        }

        /// <summary>
        /// Directs all tool output to the standard console of this process.
        /// </summary>
        public void ConnectStandardConsole()
        {
            ToolOutput += DefaultToolOutputHandler;
            ToolErrorOutput += DefaultToolErrorOutputHandler;
        }

        /// <summary>
        /// Disconnects all tool output from the standard console of this process.
        /// </summary>
        public void DisconnectStandardConsole()
        {
            ToolOutput -= DefaultToolOutputHandler;
            ToolErrorOutput -= DefaultToolErrorOutputHandler;
        }
    }

}

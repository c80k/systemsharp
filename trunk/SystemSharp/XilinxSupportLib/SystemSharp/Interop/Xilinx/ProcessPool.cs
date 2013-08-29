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
    public class ProcessPool
    {
        public enum EToolState
        {
            WaitingForExecution,
            StartFailure,
            Running,
            Exited
        }

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

            public EToolState State
            {
                get { return _state; }
            }

            public int ExitCode { get; private set; }

            public void WaitForExit()
            {
                if (!_isExited.WaitOne(DefaultCompletionTimeout))
                {
                    Debug.WriteLine("Process " + _ps.ProcessName + " did not complete - about to kill");
                    _ps.Kill();
                }
            }

            public string Name
            {
                get { return _name; }
            }
        }

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

            public Tool Add(string toolName, string arguments)
            {
                return Add("", null, toolName, arguments);
            }

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

            public void Start()
            {
                _sealed = true;
                _pool._psq.Add(this);
            }
        }

        private static int _maxParallelProcesses;
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

        public int MaxParallelProcesses { get; private set; }

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

        public Tool ToolExec(string iseDir, string projDir, string toolName, string arguments)
        {
            var batch = new ToolBatch(this);
            var tool = batch.Add(iseDir, projDir, toolName, arguments);
            batch.Start();
            return tool;
        }

        public Tool ToolExec(string toolName, string arguments)
        {
            return ToolExec("", null, toolName, arguments);
        }

        public ToolBatch CreateBatch()
        {
            return new ToolBatch(this);
        }

        public event Action<Tool> ToolSpawned
        {
            add { _onToolSpawned += value; }
            remove { _onToolSpawned -= value; }
        }

        public event Action<Tool> FailedToolStart
        {
            add { _onFailedToolStart += value; }
            remove { _onFailedToolStart -= value; }
        }

        public event Action<Tool, string> ToolOutput
        {
            add { _onToolOutput += value; }
            remove { _onToolOutput -= value; }
        }

        public event Action<Tool, string> ToolErrorOutput
        {
            add { _onToolErrorOutput += value; }
            remove { _onToolErrorOutput -= value; }
        }

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

        public void ConnectStandardConsole()
        {
            ToolOutput += DefaultToolOutputHandler;
            ToolErrorOutput += DefaultToolErrorOutputHandler;
        }

        public void DisconnectStandardConsole()
        {
            ToolOutput -= DefaultToolOutputHandler;
            ToolErrorOutput -= DefaultToolErrorOutputHandler;
        }
    }

}

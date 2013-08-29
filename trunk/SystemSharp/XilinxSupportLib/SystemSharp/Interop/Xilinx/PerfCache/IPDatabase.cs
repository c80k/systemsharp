#if false
/**
 * Copyright 2012 Christian Köllner
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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using SystemSharp.Interop.Xilinx.CoreGen;

namespace SystemSharp.Interop.Xilinx.PerfCache
{
    public class IPDatabaseException : Exception
    {
        public IPDatabaseException(string message) :
            base(message)
        {
        }
    }

    public class IPDatabase
    {
        private static object _lock = new object();
        private static IPDatabase _instance;

        public static IPDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new IPDatabase();
                    return _instance;
                }
            }
        }

        private string _DBRootDir;
        private string DBRootDir
        {
            get
            {
                if (_DBRootDir == null)
                {
                    string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _DBRootDir = Path.Combine(appdata, @"SystemSharp\XilinxInterop");
                }
                return _DBRootDir;
            }
        }

        private string _DBRootPath;
        private string DBRootPath
        {
            get
            {
                if (_DBRootPath == null)
                {
                    _DBRootPath = Path.Combine(DBRootDir, "IPCoreDB.sqlite");
                }
                return _DBRootPath;
            }
        }

        private IPDatabase()
        {
        }

        private SQLiteConnection Connect()
        {
            Directory.CreateDirectory(DBRootDir);
            string connStr = "Data Source=\"" + DBRootPath + "\"";
            var conn = new SQLiteConnection(connStr);
            conn.Open();
            return conn;
        }

        private void EnsureCreateTable(SQLiteConnection conn, CoreGenDescription desc)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = desc.GetSql_CreateTable();
            if (cmd.ExecuteNonQuery() < 0)
                throw new IPDatabaseException("Unable to create table");
        }

        private PerformanceRecord QueryPerformanceRecord(SQLiteConnection conn, CoreGenDescription desc)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = desc.GetSql_QueryRecord();
            var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var result = new PerformanceRecord();
                var props = typeof(PerformanceRecord).GetProperties();
                foreach (var pi in props)
                {
                    object value = SqlCommands.UnwrapSqlValue(reader[pi.Name], pi.PropertyType);
                    pi.SetValue(result, value, new object[0]);
                }
                return result;
            }
            else
            {
                return null;
            }
        }

        private void InsertPerformanceRecord(SQLiteConnection conn, CoreGenDescription desc, PerformanceRecord prec)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = desc.GetSql_InsertRecord(prec);
            if (cmd.ExecuteNonQuery() < 0)
                throw new IPDatabaseException("unable to insert performance record");
        }

        private void UpdatePerformanceRecord(SQLiteConnection conn, CoreGenDescription desc, PerformanceRecord prec)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = desc.GetSql_UpdateRecord(prec);
            if (cmd.ExecuteNonQuery() < 0)
                throw new IPDatabaseException("unable to update performance record");
        }

        public PerformanceRecord QueryPerformanceRecord(CoreGenDescription desc)
        {
            using (var conn = Connect())
            {
                EnsureCreateTable(conn, desc);
                return QueryPerformanceRecord(conn, desc);
            }
        }

        public void UpdatePerformanceRecord(CoreGenDescription desc, PerformanceRecord prec)
        {
            using (var conn = Connect())
            {
                EnsureCreateTable(conn, desc);
                if (QueryPerformanceRecord(conn, desc) == null)
                {
                    InsertPerformanceRecord(conn, desc, prec);
                }
                else
                {
                    UpdatePerformanceRecord(conn, desc, prec);
                }
            }
        }
    }
}
#endif

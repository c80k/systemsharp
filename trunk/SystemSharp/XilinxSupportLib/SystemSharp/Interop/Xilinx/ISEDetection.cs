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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SystemSharp.Interop.Xilinx
{
    /// <summary>
    /// Provides information on an ISE installation.
    /// </summary>
    public class ISEInfo
    {
        /// <summary>
        /// Gets or sets the installation path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the textual version.
        /// </summary>
        public string VersionText { get; set; }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public EISEVersion VersionTag { get; set; }
    }

    /// <summary>
    /// This static class provides methods for detecting existing ISE installations.
    /// </summary>
    public static class ISEDetector
    {
        private static Dictionary<string, EISEVersion> _versionMap = new Dictionary<string, EISEVersion>();

        static ISEDetector()
        {
            foreach (PropDesc prop in PropEnum.EnumProps(typeof(EISEVersion)))
                _versionMap[prop.IDs[EPropAssoc.ISE]] = (EISEVersion)prop.EnumValue;
        }

        /// <summary>
        /// Tries to convert an ISE version text to the version enum value.
        /// </summary>
        /// <param name="text">version text</param>
        /// <param name="version">out parameter to receive the parsed version</param>
        /// <returns><c>true</c> if the version text was recognized</returns>
        public static bool GetISEVersionFromText(string text, out EISEVersion version)
        {
            return _versionMap.TryGetValue(text, out version);
        }

        private static IEnumerable<string> CombineDirs(int pos, params string[][] dirs)
        {
            for (int i = 0; i < dirs[pos].Length; i++)
            {
                if (pos + 1 == dirs.Length)
                {
                    yield return dirs[pos][i];
                }
                else
                {
                    foreach (string subdir in CombineDirs(pos + 1, dirs))
                    {
                        yield return Path.Combine(dirs[pos][i], subdir);
                    }
                }
            }
        }

        private static IEnumerable<string> CombineDirs(params string[][] dirs)
        {
            return CombineDirs(0, dirs);
        }

        /// <summary>
        /// Detects and enumerates all ISE installations.
        /// </summary>
        public static IEnumerable<ISEInfo> DetectISEInstallations()
        {
            string[] rootDirs = new string[] { "C:\\Xilinx" };
            string[] dsDirs = new string[] { "ISE_DS", "" };
            string[] iseDirs = new string[] { "ISE" };
            string[] binDirs = new string[] { "bin" };
            string[] ntDirs =
                Environment.Is64BitOperatingSystem ?
                new string[] { "nt64", "nt" } :
                new string[] { "nt" };
            string[] subdirs = null;
            try
            {
                subdirs = Directory.EnumerateDirectories(rootDirs[0]).ToArray();
            }
            catch (Exception)
            {
                yield break;
            }
            string probeExe = "coregen.exe";
            string fileSetTxt = "fileset.txt";
            foreach (string subdir in subdirs)
            {
                string verText = subdir.Split(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar).Last();

                foreach (string path in CombineDirs(new string[] { subdir }, dsDirs, iseDirs, binDirs, ntDirs))
                {
                    string probePath = Path.Combine(path, probeExe);
                    bool exists = false;
                    try
                    {
                        exists = File.Exists(probePath);
                    }
                    catch (Exception)
                    {
                    }
                    if (exists)
                    {
                        string[] dirs = path.Split(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);

                        // Combine doesn't add this:
                        dirs[0] = dirs[0] + Path.DirectorySeparatorChar;

                        string iseHome = Path.Combine(dirs.Take(dirs.Length - 2).ToArray());
                        string fileSetPath = Path.Combine(iseHome, fileSetTxt);
                        try
                        {
                            string text = File.ReadAllText(fileSetPath);
                            Regex regex = new Regex(@"version=(?<version>\d\d\.\d)");
                            var matches = regex.Matches(text);
                            if (matches.Count > 0)
                            {
                                verText = matches[matches.Count - 1].Result("${version}");
                            }
                        }
                        catch (Exception)
                        {
                        }
                        ISEInfo info = new ISEInfo();
                        info.Path = path;
                        info.VersionText = verText;
                        EISEVersion version;
                        GetISEVersionFromText(info.VersionText, out version);
                        info.VersionTag = version;
                        yield return info;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Detects and returns the ISE installation with the most recent version.
        /// </summary>
        public static ISEInfo DetectMostRecentISEInstallation()
        {
            return DetectISEInstallations()
                .OrderByDescending(i => i.VersionText)
                .FirstOrDefault();
        }

        /// <summary>
        /// Tries to locate an ISE installation by the specified version.
        /// </summary>
        /// <returns>information on located ISE installation, or <c>null</c> if no such was found</returns>
        public static ISEInfo LocateISEByVersion(EISEVersion version)
        {
            return DetectISEInstallations()
                .Where(i => i.VersionTag == version)
                .FirstOrDefault();
        }
    }
}

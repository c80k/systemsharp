﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Experimental.IO.Interop;

namespace Microsoft.Experimental.IO {

    /// <summary>
    ///     Provides methods for creating, deleting, moving and enumerating directories and 
    ///     subdirectories with long paths, that is, paths that exceed 259 characters.
    /// </summary>
    public static class LongPathDirectory {

        /// <summary>
        ///     Creates the specified directory.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path of the directory to create.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> contains one or more directories that could not be
        ///     found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        /// <remarks>
        ///     Note: Unlike <see cref="Directory.CreateDirectory(System.String)"/>, this method only creates 
        ///     the last directory in <paramref name="path"/>.
        /// </remarks>
        public static void Create(string path) {

            string normalizedPath = LongPathCommon.NormalizeLongPath(path);

            if (!NativeMethods.CreateDirectory(normalizedPath, IntPtr.Zero)) {

                // To mimic Directory.CreateDirectory, we don't throw if the directory (not a file) already exists
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != NativeMethods.ERROR_ALREADY_EXISTS || !LongPathDirectory.Exists(path)) {
                    throw LongPathCommon.GetExceptionFromWin32Error(errorCode);
                }
            }
        }

        /// <summary>
        ///     Deletes the specified empty directory.
        /// </summary>
        /// <param name="path">
        ///      A <see cref="String"/> containing the path of the directory to delete.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> could not be found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> refers to a directory that is read-only.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> refers to a directory that is not empty.
        ///     <para>
        ///         -or-    
        ///     </para>
        ///     <paramref name="path"/> refers to a directory that is in use.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        public static void Delete(string path) {

            string normalizedPath = LongPathCommon.NormalizeLongPath(path);

            if (!NativeMethods.RemoveDirectory(normalizedPath)) {
                throw LongPathCommon.GetExceptionFromLastWin32Error();
            }
        }

        /// <summary>
        ///     Returns a value indicating whether the specified path refers to an existing directory.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path to check.
        /// </param>
        /// <returns>
        ///     <see langword="true"/> if <paramref name="path"/> refers to an existing directory; 
        ///     otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///     Note that this method will return false if any error occurs while trying to determine 
        ///     if the specified directory exists. This includes situations that would normally result in 
        ///     thrown exceptions including (but not limited to); passing in a directory name with invalid 
        ///     or too many characters, an I/O error such as a failing or missing disk, or if the caller
        ///     does not have Windows or Code Access Security (CAS) permissions to to read the directory.
        /// </remarks>
        public static bool Exists(string path) {

            bool isDirectory;
            if (LongPathCommon.Exists(path, out isDirectory)) {

                return isDirectory;
            }

            return false;
        }

        /// <summary>
        ///     Returns a enumerable containing the directory names of the specified directory.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path of the directory to search.
        /// </param>
        /// <returns>
        ///     A <see cref="IEnumerable{T}"/> containing the directory names within <paramref name="path"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> contains one or more directories that could not be
        ///     found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        public static IEnumerable<string> EnumerateDirectories(string path) {

            return EnumerateDirectories(path, (string)null);
        }

        /// <summary>
        ///     Returns a enumerable containing the directory names of the specified directory that 
        ///     match the specified search pattern.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path of the directory to search.
        /// </param>
        /// <param name="searchPattern">
        ///     A <see cref="String"/> containing search pattern to match against the names of the 
        ///     directories in <paramref name="path"/>, otherwise, <see langword="null"/> or an empty 
        ///     string ("") to use the default search pattern, "*".
        /// </param>
        /// <returns>
        ///     A <see cref="IEnumerable{T}"/> containing the directory names within <paramref name="path"/>
        ///     that match <paramref name="searchPattern"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> contains one or more directories that could not be
        ///     found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern) {

            return EnumerateFileSystemEntries(path, searchPattern, includeDirectories: true, includeFiles: false);
        }

        /// <summary>
        ///     Returns a enumerable containing the file names of the specified directory.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path of the directory to search.
        /// </param>
        /// <returns>
        ///     A <see cref="IEnumerable{T}"/> containing the file names within <paramref name="path"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> contains one or more directories that could not be
        ///     found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        public static IEnumerable<string> EnumerateFiles(string path) {

            return EnumerateFiles(path, (string)null);
        }

        /// <summary>
        ///     Returns a enumerable containing the file names of the specified directory that 
        ///     match the specified search pattern.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path of the directory to search.
        /// </param>
        /// <param name="searchPattern">
        ///     A <see cref="String"/> containing search pattern to match against the names of the 
        ///     files in <paramref name="path"/>, otherwise, <see langword="null"/> or an empty 
        ///     string ("") to use the default search pattern, "*".
        /// </param>
        /// <returns>
        ///     A <see cref="IEnumerable{T}"/> containing the file names within <paramref name="path"/>
        ///     that match <paramref name="searchPattern"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> contains one or more directories that could not be
        ///     found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        public static IEnumerable<string> EnumerateFiles(string path, string searchPattern) {

            return EnumerateFileSystemEntries(path, searchPattern, includeDirectories: false, includeFiles: true);
        }

        /// <summary>
        ///     Returns a enumerable containing the file and directory names of the specified directory.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path of the directory to search.
        /// </param>
        /// <returns>
        ///     A <see cref="IEnumerable{T}"/> containing the file and directory names within 
        ///     <paramref name="path"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> contains one or more directories that could not be
        ///     found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        public static IEnumerable<string> EnumerateFileSystemEntries(string path) {

            return EnumerateFileSystemEntries(path, (string)null);
        }

        /// <summary>
        ///     Returns a enumerable containing the file and directory names of the specified directory 
        ///     that match the specified search pattern.
        /// </summary>
        /// <param name="path">
        ///     A <see cref="String"/> containing the path of the directory to search.
        /// </param>
        /// <param name="searchPattern">
        ///     A <see cref="String"/> containing search pattern to match against the names of the 
        ///     files and directories in <paramref name="path"/>, otherwise, <see langword="null"/> 
        ///     or an empty string ("") to use the default search pattern, "*".
        /// </param>
        /// <returns>
        ///     A <see cref="IEnumerable{T}"/> containing the file and directory names within 
        ///     <paramref name="path"/>that match <paramref name="searchPattern"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="path"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="path"/> is an empty string (""), contains only white 
        ///     space, or contains one or more invalid characters as defined in 
        ///     <see cref="Path.GetInvalidPathChars()"/>.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> contains one or more components that exceed
        ///     the drive-defined maximum length. For example, on Windows-based 
        ///     platforms, components must not exceed 255 characters.
        /// </exception>
        /// <exception cref="PathTooLongException">
        ///     <paramref name="path"/> exceeds the system-defined maximum length. 
        ///     For example, on Windows-based platforms, paths must not exceed 
        ///     32,000 characters.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     <paramref name="path"/> contains one or more directories that could not be
        ///     found.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        ///     The caller does not have the required access permissions.
        /// </exception>
        /// <exception cref="IOException">
        ///     <paramref name="path"/> is a file.
        ///     <para>
        ///         -or-
        ///     </para>
        ///     <paramref name="path"/> specifies a device that is not ready.
        /// </exception>
        public static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern) {

            return EnumerateFileSystemEntries(path, searchPattern, includeDirectories: true, includeFiles: true);
        }

        private static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, bool includeDirectories, bool includeFiles) {

            string normalizedSearchPattern = LongPathCommon.NormalizeSearchPattern(searchPattern);
            string normalizedPath = LongPathCommon.NormalizeLongPath(path);

            // First check whether the specified path refers to a directory and exists
            FileAttributes attributes;
            int errorCode = LongPathCommon.TryGetDirectoryAttributes(normalizedPath, out attributes);
            if (errorCode != 0) {
                throw LongPathCommon.GetExceptionFromWin32Error(errorCode);
            }

            return EnumerateFileSystemIterator(normalizedPath, normalizedSearchPattern, includeDirectories, includeFiles);
        }

        private static IEnumerable<string> EnumerateFileSystemIterator(string normalizedPath, string normalizedSearchPattern, bool includeDirectories, bool includeFiles) {
            // NOTE: Any exceptions thrown from this method are thrown on a call to IEnumerator<string>.MoveNext()

            string path = LongPathCommon.RemoveLongPathPrefix(normalizedPath);

            NativeMethods.WIN32_FIND_DATA findData;
            using (SafeFindHandle handle = BeginFind(Path.Combine(normalizedPath, normalizedSearchPattern), out findData)) {
                if (handle == null)
                    yield break;

                do {
                    string currentFileName = findData.cFileName;

                    if (IsDirectory(findData.dwFileAttributes)) {
                        if (includeDirectories && !IsCurrentOrParentDirectory(currentFileName)) {
                            yield return Path.Combine(path, currentFileName);
                        }
                    }
                    else {
                        if (includeFiles) {
                            yield return Path.Combine(path, currentFileName);
                        }
                    }
                } while (NativeMethods.FindNextFile(handle, out findData));

                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != NativeMethods.ERROR_NO_MORE_FILES)
                    throw LongPathCommon.GetExceptionFromWin32Error(errorCode);
            }
        }

        private static SafeFindHandle BeginFind(string normalizedPathWithSearchPattern, out NativeMethods.WIN32_FIND_DATA findData) {
            
            SafeFindHandle handle = NativeMethods.FindFirstFile(normalizedPathWithSearchPattern, out findData);
            if (handle.IsInvalid) {

                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != NativeMethods.ERROR_FILE_NOT_FOUND)
                    throw LongPathCommon.GetExceptionFromWin32Error(errorCode);

                return null;
            }

            return handle;  
        }

        internal static bool IsDirectory(FileAttributes attributes) {

            return (attributes & FileAttributes.Directory) == FileAttributes.Directory;
        }

        private static bool IsCurrentOrParentDirectory(string directoryName) {

            Contract.Requires<ArgumentNullException>(directoryName != null);
            return directoryName.Equals(".", StringComparison.OrdinalIgnoreCase) || directoryName.Equals("..", StringComparison.OrdinalIgnoreCase);
        }
    }
}

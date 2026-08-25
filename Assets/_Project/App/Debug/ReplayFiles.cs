using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CoH.Core.Diagnostics;
using UnityEngine;

namespace CoH.App
{
    /// <summary>
    /// Replays on disk.
    ///
    /// Under persistentDataPath rather than inside the project, because a
    /// replay is a record of something that happened on a machine, not an asset
    /// the game is built from. Committing them would fill the repository with
    /// snapshots of afternoons.
    ///
    /// Every path this produces is reported back to the caller so it can be
    /// shown; a tool that saves a file and does not say where is a tool nobody
    /// can use.
    /// </summary>
    public static class ReplayFiles
    {
        /// <summary>Where replays live. Created on first use.</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Debug", "Replays");

        public static string EnsureFolder()
        {
            string folder = Folder;

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return folder;
        }

        /// <summary>
        /// Writes a replay and returns the full path.
        ///
        /// The name carries a clock reading, which is the one place a timestamp
        /// belongs: it makes two exports distinguishable in a folder listing and
        /// takes no part whatsoever in replaying anything.
        /// </summary>
        public static string Save(ReplayRecord record, string label = "")
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            string folder = EnsureFolder();
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            string safeLabel = string.IsNullOrEmpty(label) ? "match" : Sanitise(label);
            string path = Path.Combine(folder, safeLabel + "-" + stamp + ReplayFormat.FileExtension);

            File.WriteAllText(path, ReplayFile.Write(record));
            return path;
        }

        /// <summary>Reads a replay. Throws a named error rather than a mess of nulls.</summary>
        public static ReplayRecord Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("No replay path given.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("There is no replay at " + path, path);
            }

            return ReplayFile.Read(File.ReadAllText(path));
        }

        /// <summary>
        /// Every replay in the folder, newest first.
        ///
        /// A listing rather than a file picker: a real one would mean a
        /// platform dependency, and a development tool that only ever opens its
        /// own folder does not need one.
        /// </summary>
        public static IReadOnlyList<string> List()
        {
            string folder = Folder;

            if (!Directory.Exists(folder))
            {
                return Array.Empty<string>();
            }

            List<string> files = new List<string>(
                Directory.GetFiles(folder, "*" + ReplayFormat.FileExtension));

            // Ordinal, so the listing is the same on every machine. The names
            // begin with a label and a sortable stamp, so this is newest first.
            files.Sort((left, right) => string.CompareOrdinal(right, left));
            return files;
        }

        /// <summary>Writes any text next to the replays, for a state dump or a report.</summary>
        public static string SaveText(string fileName, string contents)
        {
            string folder = EnsureFolder();
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(folder, Sanitise(fileName) + "-" + stamp + ".txt");

            File.WriteAllText(path, contents ?? string.Empty);
            return path;
        }

        private static string Sanitise(string name)
        {
            char[] characters = name.ToCharArray();

            for (int index = 0; index < characters.Length; index++)
            {
                if (!char.IsLetterOrDigit(characters[index]) &&
                    characters[index] != '_' && characters[index] != '-')
                {
                    characters[index] = '_';
                }
            }

            return new string(characters);
        }
    }
}

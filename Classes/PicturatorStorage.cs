using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StandalonePicturator.Classes;

// Manages persistent application storage, automatic directory migration, and atomic file saving
public static class PicturatorStorage
{
    private static readonly Lazy<string> root = new(Initialize);

    // Resolves a relative filename to the active application storage directory
    public static string FilePath(string name) => Path.Combine(root.Value, name);

    // Resolves the persistent data folder across installs and migrates legacy library data once
    private static string Initialize()
    {
        string overridePath = Environment.GetEnvironmentVariable("STYX_PICTURATOR_DATA");
        string target = overridePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "StyxHavenVN", 
            "SliderPicturator");

        Directory.CreateDirectory(target);

        // If an explicit override environment path is set, skip migration checks
        if (overridePath != null) return target;

        // Skip migration if already performed previously
        if (File.Exists(Path.Combine(target, "migration.complete"))) return target;

        // Search candidate directories for legacy picturator library files
        var candidates = new List<string> { AppContext.BaseDirectory };
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && directory.Name != "bin")
        {
            directory = directory.Parent;
        }

        if (directory != null)
        {
            candidates.AddRange(
                Directory.EnumerateFiles(directory.FullName, "picturator_library.json", SearchOption.AllDirectories)
                         .Select(Path.GetDirectoryName));
        }

        // Locate the most recently modified legacy library
        string source = candidates.Distinct()
            .Where(p => File.Exists(Path.Combine(p, "picturator_library.json")))
            .OrderByDescending(p => File.GetLastWriteTimeUtc(Path.Combine(p, "picturator_library.json")))
            .FirstOrDefault() ?? AppContext.BaseDirectory;

        // Copy legacy session and library files into the target storage folder
        foreach (string name in new[] { "picturator_session.json", "picturator_library.json" })
        {
            string old = Path.Combine(source, name);
            string current = Path.Combine(target, name);

            if (File.Exists(old) && !File.Exists(current))
            {
                File.Copy(old, current);
            }
        }

        // Mark migration as completed to avoid recurring directory traversal
        File.WriteAllText(Path.Combine(target, "migration.complete"), source);
        return target;
    }

    // Atomically writes text to disk: writes to a temporary file, creates a .bak backup, and replaces the target
    public static void Write(string path, string text)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, text);

        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", true);
        }

        File.Move(temp, path, true);
    }
}
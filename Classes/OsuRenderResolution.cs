using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace StandalonePicturator.Classes;

public static class OsuRenderResolution
{
    public static double? Read(string beatmapPath)
    {
        var directories = new List<string>();
        foreach (var process in Process.GetProcessesByName("osu!")) {
            using (process) {
                try { directories.Add(Path.GetDirectoryName(process.MainModule.FileName)); } catch { }
            }
        }
        if (!string.IsNullOrEmpty(beatmapPath)) {
            var directory = new FileInfo(beatmapPath).Directory;
            while (directory != null) {
                if (File.Exists(Path.Combine(directory.FullName, "osu!.exe"))) { directories.Add(directory.FullName); break; }
                directory = directory.Parent;
            }
        }
        foreach (var directory in directories.Distinct()) {
            try {
                var file = Path.Combine(directory, "osu!." + Environment.UserName + ".cfg");
                if (!File.Exists(file)) {
                    // A helper/test process may run as a different Windows account.
                    // Only fall back when the install has exactly one user configuration.
                    var candidates = Directory.GetFiles(directory, "osu!.*.cfg");
                    if (candidates.Length != 1) continue;
                    file = candidates[0];
                }
                var settings = new Dictionary<string, string>();
                foreach (var line in File.ReadLines(file)) {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2) settings[parts[0].Trim()] = parts[1].Trim();
                }
                var key = settings.TryGetValue("Fullscreen", out var mode) && mode == "1" ? "HeightFullscreen" : "Height";
                if (settings.TryGetValue(key, out var value) && int.TryParse(value, out int height) && height >= 120) return height;
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return null;
    }
}

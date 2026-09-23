using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace StandalonePicturator.Classes;

// Reads the native vertical render resolution (window/screen height) from osu! configuration files
public static class OsuRenderResolution
{
    public static double? Read(string beatmapPath)
    {
        var directories = new List<string>();

        // 1. Detect running osu! instances and extract their installation directories
        foreach (var process in Process.GetProcessesByName("osu!"))
        {
            using (process)
            {
                try
                {
                    directories.Add(Path.GetDirectoryName(process.MainModule.FileName));
                }
                catch
                {
                    //
                }
            }
        }

        // 2. Fallback: Traverse upwards from beatmap directory to locate osu!.exe
        if (!string.IsNullOrEmpty(beatmapPath))
        {
            var directory = new FileInfo(beatmapPath).Directory;
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "osu!.exe")))
                {
                    directories.Add(directory.FullName);
                    break;
                }

                directory = directory.Parent;
            }
        }

        // 3. Parse osu!.<username>.cfg to read the active render height
        foreach (var directory in directories.Distinct())
        {
            try
            {
                var file = Path.Combine(directory, $"osu!.{Environment.UserName}.cfg");

                if (!File.Exists(file))
                {
                    // Fall back if there is only a single user config in the folder
                    var candidates = Directory.GetFiles(directory, "osu!.*.cfg");
                    if (candidates.Length != 1) continue;

                    file = candidates[0];
                }

                var settings = new Dictionary<string, string>();
                foreach (var line in File.ReadLines(file))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        settings[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                // Query fullscreen or windowed height key
                bool isFullscreen = settings.TryGetValue("Fullscreen", out var mode) && mode == "1";
                string key = isFullscreen ? "HeightFullscreen" : "Height";

                if (settings.TryGetValue(key, out var value) && int.TryParse(value, out int height) && height >= 120)
                {
                    return height;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }
}
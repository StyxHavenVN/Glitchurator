using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using StandalonePicturator.Classes.BeatmapHelper;

namespace StandalonePicturator.Classes;

public static class EditorSliderSelection
{
    // Resolves sliders from raw .osu hitobject lines or editor timestamps.
    public static List<HitObject> Resolve(string text, Beatmap map)
    {
        var raw = new List<HitObject>();

        // 1. Try parsing directly as raw .osu hitobject lines
        foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Regex.IsMatch(line.Trim(), @"^-?\d+,-?\d+,")) continue;

            var item = new HitObject(line.Trim());
            if (!item.IsSlider) continue;

            if (map != null) item.CalculateSliderTemporalLength(map.BeatmapTiming, false);
            raw.Add(item);
        }

        if (raw.Count > 0) return raw;

        if (map == null)
        {
            throw new InvalidOperationException("Choose the saved .osu file first. Editor timestamps do not contain slider geometry.");
        }

        var result = new List<HitObject>();

        // 2. Parse editor timestamp format and query objects from the loaded beatmap
        foreach (Match match in Regex.Matches(text, @"(?<m>\d+):(?<s>\d{2}):(?<ms>\d{3})\s*\((?<ids>[\d,\s]+)\)"))
        {
            double time = double.Parse(match.Groups["m"].Value) * 60000 
                        + double.Parse(match.Groups["s"].Value) * 1000 
                        + double.Parse(match.Groups["ms"].Value);

            int[] ids = match.Groups["ids"].Value
                .Split(',')
                .Select(s => int.Parse(s.Trim()))
                .ToArray();

            // Locate candidate hitobjects near the timestamp
            var candidates = map.HitObjects.Where(h => Math.Abs(h.Time - time) < 0.5).ToList();
            if (candidates.Count == 0)
            {
                candidates = map.HitObjects.Where(h => h.IsSlider && Math.Abs(h.Time - time) <= 1).ToList();
            }

            var first = candidates.Count == 1 
                ? candidates[0] 
                : candidates.SingleOrDefault(h => h.IsSlider && h.ComboIndex == ids[0]);

            int cursor = first == null ? -1 : map.HitObjects.IndexOf(first);
            if (cursor < 0)
            {
                throw new InvalidOperationException($"No unique slider selection at {time:0} ms in the chosen .osu file. Save editor changes and choose the same difficulty.");
            }

            // Track successive combo indices in selection sequence
            for (int i = 0; i < ids.Length; i++)
            {
                if (i > 0)
                {
                    cursor = map.HitObjects.FindIndex(cursor + 1, h => h.ComboIndex == ids[i]);
                }

                if (cursor < 0)
                {
                    throw new InvalidOperationException("Cannot resolve all selected objects in the saved map.");
                }

                var item = map.HitObjects[cursor];
                if (item.IsSlider && !result.Contains(item))
                {
                    result.Add(item);
                }
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("No sliders found. Copy a slider timestamp in osu! editor with Ctrl+C, then click Load copied sliders.");
        }

        return result.Select(h => h.DeepCopy()).ToList();
    }

    // Brings osu! to foreground, simulates Ctrl+C, and reads the copied selection from clipboard
    public static async Task<string> CopyFromEditor()
    {
        IntPtr target = IntPtr.Zero;

        // Locate osu! window handle
        foreach (var process in Process.GetProcessesByName("osu!"))
        {
            using (process)
            {
                target = process.MainWindowHandle;
                if (target == IntPtr.Zero)
                {
                    EnumWindows((handle, _) =>
                    {
                        GetWindowThreadProcessId(handle, out uint id);
                        if (id == process.Id && IsWindowVisible(handle))
                        {
                            target = handle;
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                if (target != IntPtr.Zero) break;
            }
        }

        if (target == IntPtr.Zero)
        {
            throw new InvalidOperationException("osu! is not open. Open its editor, or copy a selection and use Import clipboard.");
        }

        var previous = GetForegroundWindow();
        uint sequence = GetClipboardSequenceNumber();

        try
        {
            // Restore window if minimized and bring to foreground
            if (IsIconic(target)) ShowWindow(target, 9);
            if (!SetForegroundWindow(target))
            {
                throw new InvalidOperationException("Cannot focus osu!. Copy the selection with Ctrl+C, then use Import clipboard.");
            }

            await Task.Delay(180);
            if (GetForegroundWindow() != target)
            {
                throw new InvalidOperationException("osu! lost focus. No shortcut was sent.");
            }

            // Synthesize Ctrl+C keystroke sequence
            Input Key(ushort key, bool up) => new()
            {
                Type = 1,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        Key = key,
                        Flags = up ? 2u : 0u
                    }
                }
            };

            var inputs = new[]
            {
                Key(0x11, false), // Ctrl Down
                Key(0x43, false), // C Down
                Key(0x43, true),  // C Up
                Key(0x11, true)   // Ctrl Up
            };

            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            {
                throw new InvalidOperationException("Cannot copy from osu!. Use Ctrl+C and Import clipboard instead.");
            }

            // Poll for clipboard sequence update
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(100);
                if (GetClipboardSequenceNumber() != sequence && Clipboard.ContainsText())
                {
                    return Clipboard.GetText();
                }
            }

            throw new InvalidOperationException("osu! did not copy a selection. Select one or more sliders in the editor and try again.");
        }
        finally
        {
            // Restore original active window
            if (previous != IntPtr.Zero && GetForegroundWindow() == target)
            {
                SetForegroundWindow(previous);
            }
        }
    }

    #region Win32 P/Invoke

    private delegate bool WindowCallback(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Key;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public UIntPtr Extra;
    }

    #endregion
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Classes;

// Configuration settings for generating and injecting extreme matrix sliders
public sealed class ExtremeSliderSettings
{
    public string MapPath { get; set; } = "";
    public string Source { get; set; } = ExtremeSlider.Example;
    public double Time { get; set; } = 11827;
    public double Duration { get; set; } = 1000;
    public double ScaleX { get; set; } = 1;
    public double ScaleY { get; set; } = 1;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public int Lines { get; set; } = 32;
}

public static class ExtremeSlider
{
    // Sample Aspire-style Catmull slider utilizing extreme coordinates
    public const string Example = "163,0,11827,2,0,C|900000000:-750|-700000000:-50|-800000000:170|100000000:-600,1,3735026248.47201";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // Parses and transforms a raw slider hitobject into an extreme coordinate slider
    public static HitObject Create(ExtremeSliderSettings settings)
    {
        // 1. Validate inputs
        if (!double.IsFinite(settings.Time) || settings.Time < 1 || settings.Time > int.MaxValue - 60001 ||
            !double.IsFinite(settings.Duration) || settings.Duration < 2 || settings.Duration > 60000)
        {
            throw new ArgumentException("Start time must be >= 1 ms; duration must be 2–60000 ms.");
        }

        if (!double.IsFinite(settings.ScaleX) || !double.IsFinite(settings.ScaleY) ||
            Math.Abs(settings.ScaleX) > 100 || Math.Abs(settings.ScaleY) > 100)
        {
            throw new ArgumentException("X/Y scales must be finite and within -100 to 100.");
        }

        var values = settings.Source.Trim().Split(',');
        if (values.Length < 8 || !int.TryParse(values[3], out int type) || (type & 2) == 0)
        {
            throw new ArgumentException("Paste a slider line from [HitObjects].");
        }

        var curve = values[5].Split('|');
        if (curve.Length < 2 || curve.Length > 513 || curve[0] is not ("C" or "L"))
        {
            throw new ArgumentException("This tab supports C (Catmull) or L (Linear), with up to 512 anchors.");
        }

        // 2. Parse head and control anchor coordinates
        double Number(string text) => double.Parse(text, NumberStyles.Float, Culture);

        var head = new Vector2(Number(values[0]), Number(values[1]));
        var points = new List<Vector2> { head };

        foreach (var item in curve.Skip(1))
        {
            var pair = item.Split(':');
            if (pair.Length != 2)
            {
                throw new ArgumentException("Anchors must use X:Y format.");
            }

            points.Add(new Vector2(Number(pair[0]), Number(pair[1])));
        }

        // 3. Transform anchor positions relative to slider head
        for (int i = 0; i < points.Count; i++)
        {
            var p = new Vector2(
                head.X + (points[i].X - head.X) * settings.ScaleX + settings.OffsetX,
                head.Y + (points[i].Y - head.Y) * settings.ScaleY + settings.OffsetY
            ).Rounded();

            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) ||
                Math.Abs(p.X) > 1_000_000_000 || Math.Abs(p.Y) > 1_000_000_000)
            {
                throw new ArgumentException("Transformed coordinates must be within +/-1,000,000,000.");
            }

            points[i] = p;
        }

        // 4. Calculate total cumulative path length
        double length = 0;
        for (int i = 1; i < points.Count; i++)
        {
            length += (points[i] - points[i - 1]).Length;
        }

        if (!double.IsFinite(length) || length <= 0)
        {
            throw new ArgumentException("Slider path length must be positive.");
        }

        // 5. Construct slider HitObject (SliderVelocity = NaN disables tick processing)
        var slider = new HitObject(Math.Round(settings.Time), 0, SampleSet.None, SampleSet.None)
        {
            IsCircle = false,
            IsSlider = true,
            IsSpinner = false,
            IsHoldNote = false,
            Repeat = 1,
            SliderType = curve[0] == "C" ? PathType.Catmull : PathType.Linear,
            PixelLength = length,
            TemporalLength = Math.Round(settings.Duration),
            SliderVelocity = double.NaN
        };

        slider.SetAllCurvePoints(points);
        return slider;
    }

    // Generates a Catmull matrix pattern oscillating between extreme coordinate bounds
    public static string Matrix(int lines, bool vertical)
    {
        lines = Math.Clamp(lines, 2, 128);
        var anchors = new List<string>();

        for (int i = 0; i <= lines; i++)
        {
            double across = Math.Round(-100 + 712d * i / lines);
            double far = i % 2 == 0 ? 900_000_000 : -900_000_000;

            anchors.Add(vertical 
                ? $"{across.ToString(Culture)}:{far.ToString(Culture)}" 
                : $"{far.ToString(Culture)}:{across.ToString(Culture)}");
        }

        return "256,192,11827,2,0,C|" + string.Join("|", anchors) + ",1,1";
    }

    // Injects the extreme slider and required anti-lag timing points into the target beatmap
    public static HitObject Write(ExtremeSliderSettings settings, string destination)
    {
        var slider = Create(settings);
        var editor = new BeatmapEditor(settings.MapPath);
        var map = editor.Beatmap;

        // Append diff name indicator when saving to an alternate target
        if (!string.Equals(Path.GetFullPath(settings.MapPath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            string version = map.Metadata.TryGetValue("Version", out var current) ? current.ToString() : "";
            map.Metadata["Version"] = new TValue(version + " [Giant Matrix]");
            map.Metadata["BeatmapID"] = new TValue("0");
        }

        var timing = map.BeatmapTiming;
        double start = slider.Time;

        // Base timing points at slider start
        var red = timing.GetRedlineAtTime(start).Copy();
        red.Offset = start;

        var green = timing.GetTimingPointAtTime(start).Copy();
        green.Offset = start;
        green.Uninherited = false;
        green.MpB = timing.GetSvAtTime(start);

        // Pre-tick timing point placed 1 ms prior to suppress slider ticks
        var on = red.Copy();
        on.Offset = start - 1;
        on.OmitFirstBarLine = true;

        double multiplier = map.Difficulty.TryGetValue("SliderMultiplier", out var m) ? m.DoubleValue : 1.4;
        on.MpB = 100 * multiplier * slider.TemporalLength / slider.PixelLength;

        var noTicks = on.Copy();
        noTicks.Uninherited = false;
        noTicks.MpB = double.NaN;

        // Shift slider start time backward by 1 ms
        slider.Time -= 1;

        // Clean up conflicting timing points at injection timestamps
        foreach (var point in timing.TimingPoints.Where(t => t.Offset == start - 1 || t.Offset == start).ToList())
        {
            timing.Remove(point);
        }

        timing.Add(on);
        timing.Add(noTicks);
        timing.Add(red);
        timing.Add(green);
        timing.Sort();

        // Clear color overrides to preserve native beatmap combos
        map.SpecialColours.Remove("SliderTrackOverride");
        map.SpecialColours.Remove("SliderBorder");

        // Replace existing slider at this timestamp while keeping neighboring hitobjects intact
        map.HitObjects.RemoveAll(h => h.IsSlider && (h.Time == start || h.Time == start - 1));
        map.HitObjects.Add(slider);
        map.SortHitObjects();

        // Backup and save beatmap
        if (File.Exists(destination))
        {
            File.Copy(destination, destination + ".bak", true);
        }

        editor.SaveFile(destination);
        return slider;
    }
}
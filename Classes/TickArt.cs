using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Classes;

public sealed class TickArtSettings
{
    public string MapPath { get; set; } = "";
    public double Time { get; set; } = 1000;
    public double Duration { get; set; } = 4000;
    public double Spacing { get; set; } = 6;
    public bool OverrideTickRate { get; set; }
    public double TickRate { get; set; } = 4;
    public List<Vector2> Points { get; set; } = TickArt.Star();
}

public sealed record TickArtPlan(HitObject Slider, double Spacing, double Sv, double BeatLength, double TickRate, List<Vector2> Ticks);

public static class TickArt
{
    public static List<Vector2> Star() => new() {
        new(80, 45), new(120, 62), new(134, 98), new(256, 170),
        new(385, 64), new(402, 28), new(438, 16), new(422, 55), new(385, 76),
        new(278, 192), new(401, 288), new(430, 294), new(456, 344),
        new(413, 322), new(395, 302), new(254, 216), new(136, 297),
        new(121, 334), new(74, 353), new(92, 306), new(130, 285),
        new(230, 190), new(118, 115), new(90, 108), new(80, 45)
    };

    public static TickArtPlan Create(TickArtSettings s, double multiplier, double mapTickRate)
    {
        if (!double.IsFinite(s.Time) || s.Time < 1 || s.Time > int.MaxValue - 60001 || !double.IsFinite(s.Duration) || s.Duration < 20 || s.Duration > 60000)
            throw new ArgumentException("Start time must be >= 1 ms; duration must be 20–60000 ms.");
        double rate = s.OverrideTickRate ? s.TickRate : mapTickRate;
        if (!double.IsFinite(rate) || rate <= 0 || rate > 16 || !double.IsFinite(multiplier) || multiplier <= 0 || !double.IsFinite(s.Spacing) || s.Spacing < 1 || s.Spacing > 500)
            throw new ArgumentException("Tick spacing: 1–500 px; tick rate: greater than 0, up to 16.");
        if (s.Points == null || s.Points.Count < 2 || s.Points.Count > 4096) throw new ArgumentException("Draw between 2 and 4096 points.");
        var points = s.Points.Select(p => p.Rounded()).ToList();
        if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X) > 4096 || Math.Abs(p.Y) > 4096)) throw new ArgumentException("Drawing points must be within +/-4096 osu!px.");
        double length = 0;
        for (int i = 1; i < points.Count; i++) length += (points[i] - points[i - 1]).Length;
        if (length <= 0 || length > 100000) throw new ArgumentException("Path length must be greater than 0 and up to 100000 px.");
        double sv = Math.Clamp(s.Spacing * rate / (100 * multiplier), .1, 10);
        double spacing = 100 * multiplier * sv / rate;
        double duration = Math.Round(s.Duration);
        double beatLength = 100 * multiplier * sv * duration / length;
        var slider = new HitObject(Math.Round(s.Time), 0, SampleSet.None, SampleSet.None) {
            IsCircle = false, IsSlider = true, IsSpinner = false, IsHoldNote = false, Repeat = 1,
            SliderType = PathType.Linear, PixelLength = length, TemporalLength = duration, SliderVelocity = -100 / sv
        };
        slider.SetAllCurvePoints(points);
        var ticks = new List<Vector2>();
        var path = slider.GetSliderPath();
        // Ordinary native tick spacing, excluding the final 10 ms of the slider.
        for (double d = spacing; d < length - length / duration * 10; d += spacing) {
            if (ticks.Count >= 10000) throw new ArgumentException("More than 10000 ticks: increase spacing or shorten the path.");
            ticks.Add(path.PositionAt(d / length));
        }
        return new TickArtPlan(slider, spacing, sv, beatLength, rate, ticks);
    }

    public static TickArtPlan ForMap(TickArtSettings s)
    {
        if (!File.Exists(s.MapPath)) return Create(s, 1.4, 1);
        var map = new BeatmapEditor(s.MapPath).Beatmap;
        if (map.Version < 8) throw new ArgumentException("Select a v8 or newer map for SV-based ticks.");
        return Create(s, map.Difficulty["SliderMultiplier"].DoubleValue, map.Difficulty["SliderTickRate"].DoubleValue);
    }

    public static TickArtPlan Write(TickArtSettings s, string destination)
    {
        var editor = new BeatmapEditor(s.MapPath);
        var map = editor.Beatmap;
        if (map.Version < 8) throw new ArgumentException("A v8 or newer beatmap is required.");
        var plan = Create(s, map.Difficulty["SliderMultiplier"].DoubleValue, map.Difficulty["SliderTickRate"].DoubleValue);
        var slider = plan.Slider;
        double start = slider.Time;
        var timing = map.BeatmapTiming;
        var restoreRed = timing.GetRedlineAtTime(start).Copy(); restoreRed.Offset = start;
        var restoreGreen = timing.GetTimingPointAtTime(start).Copy(); restoreGreen.Offset = start;
        restoreGreen.Uninherited = false; restoreGreen.MpB = timing.GetSvAtTime(start);
        var red = restoreRed.Copy(); red.Offset = start - 1; red.MpB = plan.BeatLength; red.OmitFirstBarLine = true;
        var green = restoreGreen.Copy(); green.Offset = start - 1; green.MpB = -100 / plan.Sv;
        foreach (var tp in timing.TimingPoints.Where(t => t.Offset == start - 1 || t.Offset == start).ToList()) timing.Remove(tp);
        timing.Add(red); timing.Add(green); timing.Add(restoreRed); timing.Add(restoreGreen); timing.Sort();
        // Tick Art explicitly keeps native ticks; do not inject NaN here.
        if (s.OverrideTickRate) map.Difficulty["SliderTickRate"] = new TValue(plan.TickRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
        slider.Time -= 1;
        map.HitObjects.RemoveAll(h => h.IsSlider && (h.Time == start || h.Time == start - 1));
        map.HitObjects.Add(slider); map.SortHitObjects();
        map.SpecialColours.Remove("SliderTrackOverride"); map.SpecialColours.Remove("SliderBorder");
        if (!string.Equals(Path.GetFullPath(destination), Path.GetFullPath(s.MapPath), StringComparison.OrdinalIgnoreCase)) {
            map.Metadata["Version"] = new TValue(map.Metadata["Version"] + " [Tick Art]");
            map.Metadata["BeatmapID"] = new TValue("0");
        }
        if (File.Exists(destination)) File.Copy(destination, destination + ".bak", true);
        editor.SaveFile(destination);
        return plan;
    }
}

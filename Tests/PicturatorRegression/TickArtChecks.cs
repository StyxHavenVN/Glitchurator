using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StandalonePicturator.Classes;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.View.TickArt;
using V = StandalonePicturator.Classes.MathUtil.Vector2;

internal static class TickArtChecks
{
    public static void Run(string work)
    {
        var s = new TickArtSettings { Points = new() { new(0, 0), new(100, 0), new(100, 100) }, Duration = 1000, Spacing = 20 };
        var plan = TickArt.Create(s, 1.4, 1);
        Require(plan.Ticks.Count == 9 && plan.Spacing == 20, "Tick count and spacing");
        Require((plan.Ticks[4] - new V(100, 0)).Length < .001 && (plan.Ticks[5] - new V(100, 20)).Length < .001, "Ticks follow corner by arc length");
        s.Spacing = 1;
        Require(Math.Abs(TickArt.Create(s, 1.4, 1).Spacing - 14) < .001, "Native SV lower clamp");
        s.OverrideTickRate = true; s.TickRate = 4;
        Require(Math.Abs(TickArt.Create(s, 1.4, 1).Spacing - 3.5) < .001, "Dense ticks with opt-in rate");
        s.Spacing = 6; s.Points = TickArt.Star();
        string source = Path.Combine(work, "tick-source.osu"), target = Path.Combine(work, "tick-generated.osu");
        File.WriteAllText(source, """
osu file format v14

[General]
AudioFilename:test.mp3
Mode:0

[Metadata]
Title:Tick fixture
Artist:Test
Creator:Test
Version:Test

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,1,0,80,1,0
0,-50,4,1,0,80,0,0

[HitObjects]
100,100,1005,1,0,0:0:0:0:
""");
        s.MapPath = source;
        string original = File.ReadAllText(source);
        var generated = TickArt.Write(s, target);
        var map = new BeatmapEditor(target).Beatmap;
        var slider = map.HitObjects.Single(h => h.IsSlider);
        var red = map.BeatmapTiming.TimingPoints.Single(t => t.Offset == 999 && t.Uninherited);
        var green = map.BeatmapTiming.TimingPoints.Single(t => t.Offset == 999 && !t.Uninherited);
        Require(double.IsFinite(green.MpB) && green.MpB < 0, "Ticks must not be disabled by NaN");
        double sv = -100 / green.MpB;
        Require(Math.Abs(slider.PixelLength * red.MpB / (140 * sv) - s.Duration) < .001, "Export duration");
        Require(Math.Abs(140 * sv / map.Difficulty["SliderTickRate"].DoubleValue - generated.Spacing) < .001, "Export tick spacing");
        Require(map.BeatmapTiming.GetSvAtTime(1000) == -50 && map.HitObjects.Any(h => h.IsCircle && h.Time == 1005), "Restore SV and preserve neighbor");
        Require(slider.Repeat == 1 && !slider.IsCircle && File.ReadAllText(source) == original, "Flags and source preservation");
        s.OverrideTickRate = false; TickArt.Write(s, target);
        Require(new BeatmapEditor(target).Beatmap.Difficulty["SliderTickRate"].DoubleValue == 1, "Default export retains map tick rate");
        var view = new TickArtView { Width = 1000, Height = 780 };
        view.Measure(new Size(1000, 780)); view.Arrange(new Rect(0, 0, 1000, 780)); view.UpdateLayout();
        var image = new RenderTargetBitmap(1000, 780, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(work, "tick-art.png")); encoder.Save(stream);
        Console.WriteLine($"Tick Art checks PASS: spacing, corner positions, SV limits, tick-rate opt-in, export timing, native ticks, neighbors, source and UI. Dense star: {generated.Ticks.Count} ticks.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}

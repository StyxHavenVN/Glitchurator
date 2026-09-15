using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StandalonePicturator.Classes;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.View.ExtremeSlider;

internal static class ExtremeChecks
{
    public static void Run(string work)
    {
        var settings = new ExtremeSliderSettings();
        var example = ExtremeSlider.Create(settings);
        Check(example.SliderType == PathType.Catmull && example.IsSlider && !example.IsCircle && example.Repeat == 1, "Catmull flags");
        Check(example.CurvePoints[0].X == 900000000 && example.CurvePoints[1].X == -700000000, "Large signed anchors preserved");
        var parsed = new HitObject(example.GetLine());
        Check(parsed.CurvePoints.SequenceEqual(example.CurvePoints), "Large anchors round trip");
        foreach (bool vertical in new[] { true, false }) {
            settings.Source = ExtremeSlider.Matrix(32, vertical);
            var matrix = ExtremeSlider.Create(settings);
            Check(matrix.CurvePoints.Count == 33, "Matrix point count");
            Check(matrix.PixelLength > 1e9, "Matrix path length");
        }
        settings.Source = ExtremeSlider.Example;
        settings.ScaleX = 2;
        try { ExtremeSlider.Create(settings); throw new Exception("Missing coordinate limit"); }
        catch (ArgumentException) { }
        settings.ScaleX = 1;
        settings.MapPath = Path.Combine(work, "extreme-source.osu");
        File.WriteAllText(settings.MapPath, """
osu file format v14

[General]
AudioFilename: test.mp3
Mode: 0

[Metadata]
Title:Extreme fixture
Artist:Test
Creator:Test
Version:Source

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
100,100,12000,1,0,0:0:0:0:
""");
        string before = File.ReadAllText(settings.MapPath);
        string output = Path.Combine(work, "extreme-generated.osu");
        var generated = ExtremeSlider.Write(settings, output);
        Check(File.ReadAllText(settings.MapPath) == before, "Save-as preserves source map");
        var map = new BeatmapEditor(output).Beatmap;
        var saved = map.HitObjects.Single(h => h.IsSlider);
        Check(saved.Time == settings.Time - 1 && saved.Repeat == 1 && !saved.IsCircle, "Injected slider flags/time");
        Check(map.HitObjects.Any(h => h.IsCircle && h.Time == 12000), "Neighbor note preserved");
        var red = map.BeatmapTiming.TimingPoints.Single(t => t.Offset == saved.Time && t.Uninherited);
        Check(Math.Abs(red.MpB * saved.PixelLength / 140 - settings.Duration) < .001, "Duration injection");
        Check(map.BeatmapTiming.TimingPoints.Any(t => t.Offset == saved.Time && double.IsNaN(t.MpB)), "NaN ticks");
        Check(map.BeatmapTiming.GetSvAtTime(settings.Time) == -50, "Restored SV");
        var view = new ExtremeSliderView { Width = 1000, Height = 820 };
        view.Measure(new Size(1000, 820)); view.Arrange(new Rect(0, 0, 1000, 820));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); view.UpdateLayout();
        var image = new RenderTargetBitmap(1000, 820, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(work, "extreme-tab.png")); encoder.Save(stream);
        Console.WriteLine($"Extreme checks PASS: raw Catmull, matrix presets, limits, export round trip, timing and UI; length {generated.PixelLength:R}");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}

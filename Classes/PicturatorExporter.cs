using System;
using System.Drawing;
using System.IO;
using System.Linq;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.Classes;

public sealed class PicturatorExportRequest : IDisposable
{
    public string Path { get; init; }
    public Bitmap Image { get; private set; }
    public HitObject BallSlider { get; init; }
    public HitObject Shape { get; init; }
    public BallGraphPoint[] MotionGraph { get; init; }
    public Vector2 Start { get; init; }
    public Vector2 ImageStart { get; set; }
    public double Time { get; init; }
    public double Duration { get; init; }
    public double Resolution { get; init; }
    public long Viewport { get; init; }
    public int Quality { get; init; }
    public double MinimumTumourLength { get; init; } = 12;
    public double NativeRadiusPixels { get; init; }
    public NativeGlitchSnapshot NativeGlitch { get; private set; }
    public double[,] CompositeField { get; private set; }

    public static PicturatorExportRequest Capture(SliderPicturatorVm vm)
    {
        if (vm.Bm == null) throw new InvalidOperationException("Import a shape slider or image first.");
        var ball = vm.CreateBallSlider();
        if (vm.HasSliderBall && ball == null)
            throw new InvalidOperationException("Import a shape slider or a separate path for the sliderball.");
        var layers = vm.VisibleLayers.ToArray();
        if (layers.Length == 0) throw new InvalidOperationException("Show at least one layer before exporting.");
        var request = new PicturatorExportRequest {
            MinimumTumourLength = vm.MinimumTumourLength,
            NativeGlitch = vm.CaptureNativeGlitch(),
            NativeRadiusPixels = vm.NativeSliderShading ? Beatmap.GetHitObjectRadius(vm.TargetCS) * (vm.YResolution - 16) / 480 : 0,
            MotionGraph = vm.BallGraphEnabled ? vm.BallGraphPoints.ToArray() : null,
            Path = vm.BeatmapPath, Image = (Bitmap)vm.Bm.Clone(), BallSlider = ball,
            Shape = vm.SelectedSlider?.DeepCopy(), Start = ball?.Pos ?? new Vector2(vm.SliderStartX, vm.SliderStartY),
            ImageStart = new Vector2(vm.ImageStartX, vm.ImageStartY), Time = vm.TimeCode,
            Duration = vm.Duration, Resolution = vm.YResolution, Viewport = vm.ViewportSize,
            Quality = Math.Clamp(vm.Quality, 1, 101)
        };
        if (layers.Length > 1 || layers[0] != vm) {
            var bounds = layers[0].PreviewImageBounds;
            foreach (var layer in layers.Skip(1)) bounds.Union(layer.PreviewImageBounds);
            double factor = (vm.YResolution - 16) / 480;
            int width = checked((int)Math.Ceiling(bounds.Width * factor) + 2);
            int height = checked((int)Math.Ceiling(bounds.Height * factor) + 2);
            if ((long)width * height > 16000000) { request.Dispose(); throw new InvalidOperationException("Combined image is too large. Move the layers closer or reduce their size."); }
            var field = new double[width, height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) field[x,y] = 1.2;
            foreach (var layer in layers) {
                double radius = layer.NativeSliderShading ? Beatmap.GetHitObjectRadius(layer.TargetCS) * (layer.YResolution - 16) / 480 : 0;
                using var glitch = layer.CaptureNativeGlitch();
                double[,] source = null;
                var recolor = Tools.SlideratorStuff.SliderPicturator.Recolor(layer.Bm, Color.White, Color.White, Color.Black,
                    null, true, true, true, true, true, true, layer.Quality, radius, glitch?.Build(radius, layer.Quality), f => source = f);
                recolor.Item1.Dispose();
                var rect = layer.PreviewImageBounds;
                int left = (int)Math.Round((rect.X - bounds.X) * factor), top = (int)Math.Round((rect.Y - bounds.Y) * factor);
                int w = Math.Max(1, (int)Math.Round(rect.Width * factor)), h = Math.Max(1, (int)Math.Round(rect.Height * factor));
                for (int y = 0; y < h && top+y < height; y++) for (int x = 0; x < w && left+x < width; x++) {
                    double value = source[Math.Min(source.GetLength(0)-1, x*source.GetLength(0)/w), Math.Min(source.GetLength(1)-1, y*source.GetLength(1)/h)];
                    if (value <= 1) field[left+x,top+y] = value;
                }
            }
            request.Image.Dispose(); request.NativeGlitch?.Dispose();
            request.Image = new Bitmap(width, height);
            request.NativeGlitch = null;
            request.ImageStart = new Vector2(bounds.X, bounds.Y);
            request.CompositeField = field;
        }
        return request;
    }

    public void Dispose() { Image?.Dispose(); NativeGlitch?.Dispose(); }
}

public static class PicturatorExporter
{
    public static System.Collections.Generic.List<Vector2> GeneratePath(PicturatorExportRequest request, double circleSize)
    {
        if (!double.IsFinite(request.Duration) || request.Duration < 2 || request.Duration > 60000)
            throw new ArgumentOutOfRangeException(nameof(request.Duration));
        var ball = request.BallSlider?.DeepCopy();
        if (ball != null) ball.TemporalLength = Math.Round(request.Duration);
        var (points, _) = Tools.SlideratorStuff.SliderPicturator.Picturate(
            request.Image, Color.White, Color.White, Color.Black,
            circleSize, request.Start, request.ImageStart,
            ball, request.Resolution, request.Viewport, true, true, true, true, true, true, request.Quality,
            request.MotionGraph == null ? null : t => BallMotionGraph.Evaluate(request.MotionGraph, t), request.NativeRadiusPixels, request.CompositeField ?? request.NativeGlitch?.Build(request.NativeRadiusPixels, request.Quality), request.MinimumTumourLength);
        if (points == null || points.Count < 2)
            throw new InvalidOperationException("Cannot generate a slider path from this image.");
        // Use exactly the integer anchors that will be written to the .osu file.
        for (int i = 0; i < points.Count; i++) points[i] = points[i].Rounded();
        return points;
    }

    public static HitObject Export(PicturatorExportRequest request)
    {
        if (!double.IsFinite(request.Duration) || request.Duration < 2 || request.Duration > 60000)
            throw new ArgumentOutOfRangeException(nameof(request.Duration), "Duration must be 2–60000 ms.");
        var editor = new BeatmapEditor(request.Path);
        var beatmap = editor.Beatmap;
        double startTime = Math.Round(request.Time);
        double duration = Math.Round(request.Duration);
        var points = GeneratePath(request, beatmap.Difficulty["CircleSize"].DoubleValue);
        double length = 0;
        for (int i = 1; i < points.Count; i++) length += (points[i] - points[i - 1]).Length;
        if (!double.IsFinite(length) || length <= 0)
            throw new InvalidOperationException("Invalid slider length.");
        var slider = new HitObject(startTime, 0, SampleSet.None, SampleSet.None) {
            IsCircle = false, IsSlider = true, IsSpinner = false, IsHoldNote = false, Repeat = 1,
            SliderType = PathType.Linear, PixelLength = length, TemporalLength = duration,
            SliderVelocity = double.NaN, NewCombo = request.Shape?.NewCombo ?? false,
            ComboSkip = request.Shape?.ComboSkip ?? 0
        };
        slider.SetAllCurvePoints(points);
        var timing = beatmap.BeatmapTiming;
        var restoreRed = timing.GetRedlineAtTime(startTime).Copy();
        var restoreGreen = timing.GetTimingPointAtTime(startTime).Copy();
        double originalSv = timing.GetSvAtTime(startTime);
        restoreRed.Offset = startTime;
        restoreGreen.Offset = startTime;
        restoreGreen.Uninherited = false;
        restoreGreen.MpB = originalSv;
        var injectedRed = restoreRed.Copy();
        injectedRed.Offset = startTime - 1;
        injectedRed.OmitFirstBarLine = true;
        double multiplier = beatmap.Difficulty.TryGetValue("SliderMultiplier", out var value) ? value.DoubleValue : 1.4;
        injectedRed.MpB = 100.0 * multiplier * duration / slider.PixelLength;
        slider.Time -= 1;
        var injectedGreen = injectedRed.Copy();
        injectedGreen.Uninherited = false;
        injectedGreen.MpB = double.NaN;
        foreach (var tp in timing.TimingPoints.Where(tp => tp.Offset == slider.Time || tp.Offset == startTime).ToList()) timing.Remove(tp);
        timing.Add(injectedRed);
        timing.Add(injectedGreen);
        timing.Add(restoreRed);
        timing.Add(restoreGreen);
        timing.Sort();
        beatmap.SpecialColours.Remove("SliderTrackOverride");
        beatmap.SpecialColours.Remove("SliderBorder");
        beatmap.HitObjects.RemoveAll(h => h.IsSlider && (h.Time == startTime || h.Time == startTime - 1));
        beatmap.HitObjects.Add(slider);
        beatmap.SortHitObjects();
        // Generate and validate everything before touching the user's file.
        File.Copy(request.Path, request.Path + ".bak", true);
        editor.SaveFile();
        return slider;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StandalonePicturator.Classes;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Viewmodel;
using StandalonePicturator.View.SliderPicturator;
using Vector2 = StandalonePicturator.Classes.MathUtil.Vector2;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Near(double actual, double expected, double tolerance, string message) => Check(Math.Abs(actual - expected) <= tolerance, $"{message}: {actual} != {expected}");
    private static void Pump(int milliseconds) {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    private static void CheckMotion(HitObject exported, HitObject target, int duration, string label, Func<double, double> motion = null)
    {
        motion ??= t => t;
        var path = exported.GetSliderPath();
        var desired = target.GetSliderPath();
        var points = exported.GetAllCurvePoints();
        var distances = new double[points.Count];
        for (int i = 1; i < points.Count; i++) {
            float dx = (float)points[i].X - (float)points[i - 1].X;
            float dy = (float)points[i].Y - (float)points[i - 1].Y;
            distances[i] = distances[i - 1] + (float)Math.Sqrt(dx * dx + dy * dy);
        }
        double worst = 0, floatWorst = 0;
        for (int ms = 1; ms < duration; ms++) {
            var expected = desired.PositionAt(motion((double)ms / duration));
            worst = Math.Max(worst, (path.SliderballPositionAt(ms, duration) - expected).Length);
            double distance = exported.PixelLength * ms / duration;
            int index = Array.BinarySearch(distances, distance);
            if (index < 0) index = ~index;
            index = Math.Clamp(index, 0, points.Count - 1);
            floatWorst = Math.Max(floatWorst, (points[index] - expected).Length);
        }
        Console.WriteLine($"{label}: {points.Count} anchors, double error {worst:F3}px, float error {floatWorst:F3}px");
        Check(worst < 2, label + " double path sample alignment");
        Check(floatWorst < 2, label + " float path sample alignment");
        Near((path.PositionAt(1) - desired.PositionAt(motion(1))).Length, 0, 1, label + " final endpoint");
    }
    private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    [STAThread]
    private static void Main(string[] args)
    {
        GlitchChecks.Run();
        if (args.Contains("--glitch-only")) return;
        var app = new StandalonePicturator.App();
        app.InitializeComponent();
        string work = Path.Combine(AppContext.BaseDirectory, "fixtures");
        Directory.CreateDirectory(work);
        string library = Path.Combine(AppContext.BaseDirectory, "picturator_library.json");
        if (File.Exists(library)) File.Move(library, Path.Combine(work, "previous-library-" + DateTime.UtcNow.Ticks + ".json"));
        string session = Path.Combine(AppContext.BaseDirectory, "picturator_session.json");
        if (File.Exists(session)) File.Move(session, Path.Combine(work, "previous-session-" + DateTime.UtcNow.Ticks + ".json"));
        if (args.Contains("--segment-only")) { SegmentChecks.Run(work); return; }
        if (args.Contains("--extreme-only")) { ExtremeChecks.Run(work); return; }
        if (args.Contains("--tick-only")) { TickArtChecks.Run(work); return; }
        if (args.Contains("--library-only")) { LibraryChecks.Run(work); return; }
        using (var mask = new System.Drawing.Bitmap(65, 65)) {
            using (var g = System.Drawing.Graphics.FromImage(mask)) {
                g.Clear(System.Drawing.Color.Black);
                g.FillRectangle(System.Drawing.Brushes.White, 12, 12, 41, 41);
            }
            var field = NativeSliderMask.Build(mask, 20, 101);
            Near(field[0, 0], 1.2, 0, "Native background lies outside slider radius");
            Near(field[32, 32], 0, .01, "Native body center");
            Check(field[12, 32] > .94 && field[14, 32] > .8, "Native border and outer edge samples");
            Check(field[20, 32] > field[26, 32] && field[26, 32] > field[32, 32], "Native radial gradient is not a flat mask");
            var solid = NativeSliderMask.Build(mask, 20, 101);
            Check(Enumerable.Range(12, 41).All(y => solid[32, y] <= 1), "Native shading never inserts scanline gaps");
            Check(NativeSliderMask.PreviewColour(1.2).A == 0, "Native preview background transparency");
        }        var source = new HitObject("100,100,1000,2,0,B|180:40|240:180|300:100,1,230");
        var vm = new SliderPicturatorVm { SelectedSlider = source, HasSliderBall = true, Duration = 120, IsGlitchOn = false };
        var original = source.GetAllCurvePoints().ToArray();
        vm.MovePicture(43, -80);
        vm.SliderScale = 1.7;
        var ball = vm.CreateBallSlider();
        Near(ball.Pos.X, 143, 0.001, "Scaled ball head X");
        Near(ball.Pos.Y, 20, 0.001, "Scaled ball head Y");
        var sourceEnd = source.GetSliderPath().PositionAt(1);
        var expectedEnd = new Vector2(143, 20) + (sourceEnd - source.Pos) * 1.7;
        Near((ball.GetSliderPath().PositionAt(1) - expectedEnd).Length, 0, 0.001, "Trimmed curve endpoint follows mask transform");
        Check(source.GetAllCurvePoints().SequenceEqual(original), "Source curve must not be mutated");
        var oldDelta = new Vector2(vm.SliderStartX - vm.ImageStartX, vm.SliderStartY - vm.ImageStartY);
        vm.ImageStartX += 10;
        Near(vm.SliderStartX - vm.ImageStartX, oldDelta.X, 0.001, "Image X moves head");
        vm.SliderStartY += 8;
        Near(vm.SliderStartY - vm.ImageStartY, oldDelta.Y, 0.001, "Head Y moves image");
        vm.BallOffsetX = 12; vm.BallOffsetY = -7;
        vm.SaveSession();
        var restored = new SliderPicturatorVm();
        Near(restored.SliderStartX, vm.SliderStartX, 0.001, "Session head X");
        Near(restored.ImageStartY, vm.ImageStartY, 0.001, "Session image Y");
        Near(restored.BallOffsetX, 12, 0.001, "Session ball offset");
        Near(restored.CreateBallSlider().TemporalLength, 120, 0, "Session duration");
        vm.BallPathSlider = new HitObject("10,20,0,2,0,L|40:20,2,30");
        ball = vm.CreateBallSlider();
        Near((ball.GetSliderPath().PositionAt(1) - ball.Pos).Length, 0, 0.001, "Source repeat ends back at start");
        Check(ball.Repeat == 1 && ball.IsSlider && !ball.IsCircle, "Transformed slider flags");
        vm.BallPathSlider = null; vm.BallOffsetX = vm.BallOffsetY = 0;

        string map = Path.Combine(work, "sample.osu");
        File.WriteAllText(map, """
            osu file format v14

            [General]
            AudioFilename: audio.mp3
            Mode: 0

            [Metadata]
            Title: Regression
            Artist: Test
            Creator: Test
            Version: Picturator

            [Difficulty]
            HPDrainRate:5
            CircleSize:4.1
            OverallDifficulty:5
            ApproachRate:5
            SliderMultiplier:1.4
            SliderTickRate:1

            [Events]

            [TimingPoints]
            0,500,4,1,0,70,1,0
            900,-50,4,2,3,65,0,1
            1001,400,3,1,0,80,1,0

            [Colours]
            SliderTrackOverride : 10,20,30
            SliderBorder : 255,255,255

            [HitObjects]
            200,100,1002,2,0,L|300:100,1,100
            100,100,1000,2,0,L|200:100,1,100
            """);
        vm.BeatmapPath = map;
        vm.TimeCode = 1000;
        using (var request = PicturatorExportRequest.Capture(vm)) {
            var output = PicturatorExporter.Export(request);
            var parsed = new BeatmapEditor(map).Beatmap;
            var saved = parsed.HitObjects.Single(h => h.Time == 999);
            Check(saved.IsSlider && !saved.IsCircle && saved.Repeat == 1, "Serialized hitobject bitmask/repeat");
            Check(parsed.HitObjects.Any(h => h.Time == 1002), "Neighbor slider must remain");
            Check(parsed.BeatmapTiming.TimingPoints.Any(t => t.Offset == 1001 && t.MpB == 400), "Neighbor timing must remain");
            Check(!parsed.SpecialColours.ContainsKey("SliderBorder") && !parsed.SpecialColours.ContainsKey("SliderTrackOverride"), "Native skin colours");
            var red = parsed.BeatmapTiming.TimingPoints.Single(t => t.Offset == 999 && t.Uninherited);
            var green = parsed.BeatmapTiming.TimingPoints.Single(t => t.Offset == 999 && !t.Uninherited);
            Check(double.IsNaN(green.MpB) && red.OmitFirstBarLine, "NaN tick suppression at T-1");
            Near(red.MpB * saved.PixelLength / 140, vm.Duration, 0.0001, "Requested duration from exported timing");
            Near(parsed.BeatmapTiming.GetSvAtTime(1000), -50, 0, "Restore SV");
            var restore = parsed.BeatmapTiming.TimingPoints.Single(t => t.Offset == 1000 && !t.Uninherited);
            Check(restore.SampleIndex == 3 && restore.Volume == 65, "Restore timing sample metadata");
            var actualPath = saved.GetSliderPath();
            var desiredPath = request.BallSlider.GetSliderPath();
            double worst = 0;
            for (int ms = 1; ms <= vm.Duration; ms++) {
                var actual = actualPath.SliderballPositionAt(ms, (int)vm.Duration);
                var desired = desiredPath.PositionAt(ms / vm.Duration);
                worst = Math.Max(worst, (actual - desired).Length);
            }
            Console.WriteLine($"Export anchors: {saved.CurvePoints.Count}; maximum ball deviation: {worst:F3}px");
            Check(worst < 2, "Exported ball must follow the transformed preview at every millisecond");
            Near((actualPath.PositionAt(1) - desiredPath.PositionAt(1)).Length, 0, 1, "Final ball endpoint");
        }

        foreach (int duration in new[] { 2, 3, 1000 }) {
            vm.Duration = duration;
            using var request = PicturatorExportRequest.Capture(vm);
            var generated = PicturatorExporter.Export(request);
            CheckMotion(generated, request.BallSlider, duration, $"Duration {duration}");
        }
        vm.Duration = 240;
        vm.SliderScale = 0.7;
        vm.YResolution = 720;
        vm.IsGlitchOn = true;
        vm.BallPathSlider = new HitObject("30,40,0,2,0,L|90:40|90:90,2,110");
        using (var request = PicturatorExportRequest.Capture(vm)) {
            var generated = PicturatorExporter.Export(request);
            CheckMotion(generated, request.BallSlider, 240, "Glitch, repeat, scale, 720p");
        }
        vm.HasSliderBall = false;
        using (var request = PicturatorExportRequest.Capture(vm)) {
            var generated = PicturatorExporter.Export(request);
            Check(generated.Repeat == 1 && generated.PixelLength > 0, "Static picturator export");
        }
        vm.HasSliderBall = true;
        vm.BallPathSlider = null;
        vm.SliderScale = 1.7;
        vm.YResolution = 1080;
        vm.IsGlitchOn = false;
        vm.Duration = 1000;
        vm.Quality = 15;
        vm.Quality = 60;
        vm.Quality = 25;
        string imageFile = Path.Combine(work, "import.png");
        using (var sampleImage = new System.Drawing.Bitmap(80, 50)) {
            using var graphics = System.Drawing.Graphics.FromImage(sampleImage);
            graphics.Clear(System.Drawing.Color.Black);
            graphics.FillEllipse(System.Drawing.Brushes.White, 10, 10, 60, 30);
            sampleImage.Save(imageFile, System.Drawing.Imaging.ImageFormat.Png);
        }
        var imageVm = new SliderPicturatorVm { PictureFile = imageFile, SliderScale = 1 };
        double imageHeadX = imageVm.SliderStartX - imageVm.ImageStartX;
        double imageHeadY = imageVm.SliderStartY - imageVm.ImageStartY;
        imageVm.SliderScale = 2;
        Near(imageVm.SliderStartX - imageVm.ImageStartX, imageHeadX * 2, 0.0001, "Bitmap scale keeps relative head X");
        Near(imageVm.SliderStartY - imageVm.ImageStartY, imageHeadY * 2, 0.0001, "Bitmap scale keeps relative head Y");
        imageVm.MovePicture(24, -15);
        imageVm.BallPathSlider = new HitObject("0,0,0,2,0,L|60:0,1,60");
        imageVm.HasSliderBall = true;
        imageVm.GlitchAmount = 0;
        imageVm.GlitchFrequency = 0;
        imageVm.SaveSession();
        var loadedImage = new SliderPicturatorVm();
        Check(loadedImage.SelectedSlider == null && loadedImage.Bm.Width == 160, "Restore imported bitmap with scale");
        Near(loadedImage.ImageStartX, imageVm.ImageStartX, 0.0001, "Restore bitmap placement");
        Check(loadedImage.BallPathSlider != null, "Restore custom ball route with bitmap");
        Near(loadedImage.GlitchFrequency, 0, 0, "Restore zero glitch frequency");
        vm.SaveSession();
        // Motion graph: right-continuous jumps, holds, backwards travel and saved sessions.
        var stairs = new[] {
            new BallGraphPoint(0, 0, BallGraphCurve.Hold), new BallGraphPoint(.16, .25, BallGraphCurve.Hold),
            new BallGraphPoint(.35, .5, BallGraphCurve.Hold), new BallGraphPoint(.63, 1, BallGraphCurve.Hold),
            new BallGraphPoint(.79, 0, BallGraphCurve.Hold), new BallGraphPoint(1, 1)
        };
        Near(BallMotionGraph.Evaluate(stairs, .159), 0, 0, "Before step");
        Near(BallMotionGraph.Evaluate(stairs, .16), .25, 0, "Exactly at step");
        Near(BallMotionGraph.Evaluate(stairs, .78), 1, 0, "Hold at end");
        Near(BallMotionGraph.Evaluate(stairs, .79), 0, 0, "Jump back to start");
        var jump = BallMotionGraph.Normalize(new[] {
            new BallGraphPoint(0, .2), new BallGraphPoint(.5, .2),
            new BallGraphPoint(.5, .9), new BallGraphPoint(1, .3)
        });
        Near(BallMotionGraph.Evaluate(jump, .5), .9, 0, "Duplicate time jumps to last point");
        Near(BallMotionGraph.Evaluate(jump, .75), .6, .0001, "Backwards interpolation");
        var invalid = BallMotionGraph.Normalize(new[] { new BallGraphPoint(double.NaN, 0), new BallGraphPoint(.4, 2) });
        Check(invalid.All(p => double.IsFinite(p.Time) && p.Position == 1) && invalid[0].Time == 0 && invalid[^1].Time == 1, "Normalize invalid/partial graph");
        vm.ReplaceBallGraph(stairs);
        vm.Duration = 2000;
        Near(vm.EvaluateBallProgress(320 / vm.Duration), .25, 0, "Duration scales graph times proportionally");
        vm.Duration = 1000;
        vm.SaveSession();
        var graphSession = new SliderPicturatorVm();
        Check(graphSession.BallGraphEnabled && graphSession.BallGraphPoints.SequenceEqual(stairs), "Graph session roundtrip");
        vm.EditBallGraphPoint(0, .8, .1, BallGraphCurve.Hold);
        Near(vm.BallGraphPoints[0].Time, 0, 0, "First time handle stays at zero");
        vm.EditBallGraphPoint(1, .95, .25, BallGraphCurve.Hold);
        Near(vm.BallGraphPoints[1].Time, .35, 0, "Handles cannot cross neighboring times");
        vm.ReplaceBallGraph(stairs);
        using (var request = PicturatorExportRequest.Capture(vm)) {
            vm.ReplaceBallGraph(BallMotionGraph.Default()); // Must not mutate in-flight export.
            Check(request.MotionGraph.SequenceEqual(stairs), "Export graph snapshot");
            var output = PicturatorExporter.Export(request);
            var serialized = new BeatmapEditor(map).Beatmap.HitObjects.Single(h => h.Time == 999);
            CheckMotion(serialized, request.BallSlider, 1000, "Staircase graph", t => BallMotionGraph.Evaluate(stairs, t));
        }
        vm.ReplaceBallGraph(jump);
        using (var request = PicturatorExportRequest.Capture(vm)) {
            var output = PicturatorExporter.Export(request);
            CheckMotion(output, request.BallSlider, 1000, "Jump and reverse graph", t => BallMotionGraph.Evaluate(jump, t));
            Near((output.Pos - request.BallSlider.GetSliderPath().PositionAt(.2)).Length, 0, 1, "Graph starts inside slider body");
        }
        vm.ReplaceBallGraph(new[] { new BallGraphPoint(0, .4, BallGraphCurve.Hold), new BallGraphPoint(1, .4) });
        using (var request = PicturatorExportRequest.Capture(vm)) {
            var output = PicturatorExporter.Export(request);
            CheckMotion(output, request.BallSlider, 1000, "Stationary graph", _ => .4);
        }
        vm.Duration = 120;
        foreach (var mode in Enum.GetValues<BallGraphCurve>().Where(c => c != BallGraphCurve.Hold)) {
            var curvePoints = new[] { new BallGraphPoint(0, .15, mode), new BallGraphPoint(1, .85) };
            vm.ReplaceBallGraph(curvePoints);
            using var request = PicturatorExportRequest.Capture(vm);
            var output = PicturatorExporter.Export(request);
            CheckMotion(output, request.BallSlider, 120, "Graph " + mode, t => BallMotionGraph.Evaluate(curvePoints, t));
        }
        vm.BallGraphEnabled = false;
        Near(vm.EvaluateBallProgress(.37), .37, 0, "Disabled graph uses linear timing");
        File.WriteAllText(session, "{\"Duration\":1000}");
        var legacy = new SliderPicturatorVm();
        Check(!legacy.BallGraphEnabled && legacy.BallGraphPoints.SequenceEqual(BallMotionGraph.Default()), "Old session migrates to linear graph");
        vm.NativeSliderShading = true; vm.SaveSession();
        var oldSettings = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(session));
        oldSettings["NativeScanlineSpacing"] = 3;
        File.WriteAllText(session, oldSettings.ToString());
        var shadingSession = new SliderPicturatorVm();
        Check(shadingSession.NativeSliderShading, "Native shading session roundtrip");
        shadingSession.SaveSession();
        Check(!File.ReadAllText(session).Contains("NativeScanlineSpacing"), "Old stripe setting is ignored and removed on save");

        vm.ReplaceBallGraph(stairs); vm.Duration = 1000; vm.SetPreviewProgress(.48, true); vm.SaveSession();
        // Render the actual WPF view without opening an interactive window.
        Pump(1000);
        var view = new SliderPicturatorView { DataContext = vm, Width = 960, Height = 640 };
        view.Measure(new Size(960, 640)); view.Arrange(new Rect(0, 0, 960, 640));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Pump(600);
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(960, 640, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(work, "preview.png"))) encoder.Save(stream);
        var graphLayout = new System.Windows.Controls.Grid { Width = 1160, Height = 680, DataContext = vm };
        graphLayout.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        graphLayout.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var graphEditor = new BallGraphEditor(); var graphPreview = new PicturatorPreview();
        System.Windows.Controls.Grid.SetColumn(graphPreview, 1);
        graphLayout.Children.Add(graphEditor); graphLayout.Children.Add(graphPreview);
        graphLayout.Measure(new Size(1160, 680)); graphLayout.Arrange(new Rect(0, 0, 1160, 680));
        graphLayout.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Pump(200); graphLayout.UpdateLayout();
        void ClickGraph(string label) => Descendants<System.Windows.Controls.Button>(graphEditor)
            .Single(b => Equals(b.Content, label)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        int pointCount = vm.BallGraphPoints.Count;
        ClickGraph("Add point");
        Check(vm.BallGraphPoints.Count == pointCount + 1, "Graph Add button inserts point");
        var timeInput = Descendants<System.Windows.Controls.TextBox>(graphEditor).Single(t => t.Name == "GraphTimeInput");
        var positionInput = Descendants<System.Windows.Controls.TextBox>(graphEditor).Single(t => t.Name == "GraphPositionInput");
        timeInput.Text = "550"; positionInput.Text = "32";
        ClickGraph("Apply mốc");
        Check(vm.BallGraphPoints.Any(p => Math.Abs(p.Time - .55) < .00001 && Math.Abs(p.Position - .32) < .00001), "Numeric graph editor updates time and position");
        Near(vm.GetPreviewProgress(), .55, .00001, "Graph editor seeks preview");
        timeInput.Text = "NaN"; positionInput.Text = "400";
        ClickGraph("Apply mốc");
        Check(vm.BallGraphPoints.All(p => double.IsFinite(p.Time) && p.Position <= 1), "Invalid numeric graph input rejected");
        ClickGraph("Delete point");
        Check(vm.BallGraphPoints.Count == pointCount, "Graph Delete button removes selected point");
        ClickGraph("Linear");
        Check(vm.BallGraphPoints.SequenceEqual(BallMotionGraph.Default()), "Graph reset button");
        ClickGraph("Steps");
        Check(vm.BallGraphPoints.SequenceEqual(stairs), "Graph staircase preset button");
        vm.SetPreviewProgress(.48, true);
        graphLayout.UpdateLayout();        var graphBitmap = new RenderTargetBitmap(1160, 680, 96, 96, PixelFormats.Pbgra32); graphBitmap.Render(graphLayout);
        var graphEncoder = new PngBitmapEncoder(); graphEncoder.Frames.Add(BitmapFrame.Create(graphBitmap));
        using (var stream = File.Create(Path.Combine(work, "motion-graph.png"))) graphEncoder.Save(stream);
        Check(vm.BmImage != null && !vm.IsProcessingPreview, "Async preview completes");
        var background = new byte[4];
        vm.BmImage.CopyPixels(new Int32Rect(0, 0, 1, 1), background, 4, 0);
        Check(background[3] == 0, "Preview background is transparent");
        Console.WriteLine($"PASS: {checks} checks. Preview: {Path.Combine(work, "preview.png")}");
        app.Shutdown();
    }
}

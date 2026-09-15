using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StandalonePicturator.Classes;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Viewmodel;
using StandalonePicturator.View.SliderPicturator;

internal static class SegmentChecks
{
    public static void Run(string work)
    {
        var vm = new SliderPicturatorVm { AutoOsuResolution = false, YResolution = 128, Quality = 15, Duration = 40,
            IsGlitchOn = false, HasSliderBall = true,
            SelectedSlider = new HitObject("100,100,1000,2,0,L|160:100,1,60") };
        vm.ReplaceBallGraph(new[] { new BallGraphPoint(0, 0), new BallGraphPoint(.4, .8), new BallGraphPoint(1, .2) });
        int previousCount = int.MaxValue;
        foreach (int density in new[] { 1, 8, 12 }) {
            vm.MinimumTumourLength = density;
            using var request = PicturatorExportRequest.Capture(vm);
            vm.MinimumTumourLength = 5;
            Require(request.MinimumTumourLength == density, "Snapshot density changed with VM");
            var points = PicturatorExporter.GeneratePath(request, vm.TargetCS);
            Require(points.Count < previousCount, "Longer pacing legs must reduce actual segments");
            previousCount = points.Count;
            var slider = request.BallSlider.DeepCopy();
            slider.SliderType = StandalonePicturator.Classes.BeatmapHelper.Enums.PathType.Linear;
            slider.Repeat = 1;
            slider.SetAllCurvePoints(points);
            slider.PixelLength = Enumerable.Range(1, points.Count - 1).Sum(i => (points[i] - points[i - 1]).Length);
            var path = slider.GetSliderPath();
            var desired = request.BallSlider.GetSliderPath();
            double worst = 0;
            for (int ms = 1; ms <= 40; ms++) {
                var target = desired.PositionAt(BallMotionGraph.Evaluate(request.MotionGraph, ms / 40d));
                worst = Math.Max(worst, (path.SliderballPositionAt(ms, 40) - target).Length);
            }
            Require(worst < 2, "Segment density changed ball timing/position");
            Console.WriteLine($"Minimum tumour length {density}: {points.Count - 1} segments; max ball error {worst:F3}px");
        }
        vm.MinimumTumourLength = 12;
        vm.SaveSession();
        var restored = new SliderPicturatorVm();
        Require(restored.MinimumTumourLength == 12, "Session must restore density");
        restored.MinimumTumourLength = 99;
        Require(restored.MinimumTumourLength == 12, "Density upper clamp");
        restored.MinimumTumourLength = -1;
        Require(restored.MinimumTumourLength == 1, "Density lower clamp");
        var graph = new BallGraphEditor { DataContext = vm, Width = 1100, Height = 760 };
        graph.Measure(new Size(1100, 760)); graph.Arrange(new Rect(0, 0, 1100, 760)); graph.UpdateLayout();
        Pump(500);
        var button = Children(graph).OfType<System.Windows.Controls.Button>().Single(b => Equals(b.Content, "Calculate segments"));
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        for (int i = 0; i < 100 && !button.IsEnabled; i++) Pump(100);
        Require(button.IsEnabled, "Count must complete asynchronously");
        Require(Children(graph).OfType<System.Windows.Controls.TextBlock>().Any(t => t.Text.StartsWith("Estimated total segments:")), "Count button must display its result");
        var originalBitmap = vm.Bm;
        var originalHead = vm.CreateBallSlider().Pos;
        vm.BallOffsetX = 17; vm.BallOffsetY = -12; vm.BallPathScale = 1.25;
        var moved = vm.CreateBallSlider();
        Require((moved.Pos - originalHead - new StandalonePicturator.Classes.MathUtil.Vector2(17, -12)).Length < .001, "Independent ball translation");
        Require(Math.Abs(moved.PixelLength - 75 * vm.SliderScale) < .001, "Independent ball path scale");
        Require(ReferenceEquals(originalBitmap, vm.Bm), "Ball editing must not rasterize or move native image");
        vm.SaveSession();
        var loaded = new SliderPicturatorVm();
        Require(loaded.BallPathScale == 1.25 && loaded.BallOffsetX == 17 && !loaded.AutoOsuResolution, "Path and resolution session settings");
        var preview = new PicturatorPreview { DataContext = vm, Width = 900, Height = 650 };
        preview.Measure(new Size(900, 650)); preview.Arrange(new Rect(0, 0, 900, 650)); preview.UpdateLayout();
        var panel = Children(preview).OfType<BallPathPanel>().Single();
        Require(panel.Visibility == Visibility.Visible, "Ball enabled must show path panel");
        var handle = Children(panel).OfType<System.Windows.Controls.Primitives.Thumb>().Single();
        handle.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(70, 45) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
        Require(System.Windows.Controls.Canvas.GetLeft(panel) == 82 && System.Windows.Controls.Canvas.GetTop(panel) == 77, "Path panel must drag independently");
        vm.HasSliderBall = false; Pump(50);
        Require(panel.Visibility == Visibility.Collapsed, "Ball disabled hides panel");
        vm.ResetBallPlacement(); Pump(300);
        Require(vm.BallPathScale == 1 && vm.BallOffsetX == 0 && vm.BallOffsetY == 0, "Reset ball alignment");
        var previewImage = new RenderTargetBitmap(900, 650, 96, 96, PixelFormats.Pbgra32);
        previewImage.Render(preview);
        var previewEncoder = new PngBitmapEncoder(); previewEncoder.Frames.Add(BitmapFrame.Create(previewImage));
        using (var previewFile = File.Create(Path.Combine(work, "ball-path-panel.png"))) previewEncoder.Save(previewFile);
        var image = new RenderTargetBitmap(1100, 760, 96, 96, PixelFormats.Pbgra32);
        image.Render(graph);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(work, "segment-graph.png")); encoder.Save(file);
        double? osuHeight = OsuRenderResolution.Read(vm.BeatmapPath);
        if (osuHeight.HasValue) {
            var head = vm.CreateBallSlider().Pos;
            int oldWidth = vm.Bm.Width;
            vm.AutoOsuResolution = true;
            Require(vm.YResolution == osuHeight.Value, "Auto resolution must use actual osu configuration");
            Require((vm.CreateBallSlider().Pos - head).Length == 0, "Resolution sync must not translate ball");
            Require(vm.Bm.Width != oldWidth, "Resolution sync must rebuild the picture mask");
            Console.WriteLine($"Resolution sync PASS: {osuHeight.Value}px; ball origin preserved");
        }
        Console.WriteLine("Segment checks PASS: actual counts, motion, snapshot, session and graph render");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Children(child)) yield return descendant;
        }
    }

    private static void Pump(int milliseconds)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}

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

internal static class LibraryChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(string work)
    {
        string map = Path.Combine(work, "library.osu");
        File.WriteAllText(map, """
osu file format v14
[General]
AudioFilename:test.mp3
Mode:0
[Metadata]
Title:Library
Artist:Test
Creator:Test
Version:Test
[Difficulty]
HPDrainRate:5
CircleSize:5
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1
[TimingPoints]
0,500,4,1,0,80,1,0
0,-50,4,1,0,80,0,0
[HitObjects]
100,100,1000,2,0,L|240:100,1,140
150,150,1500,1,0,0:0:0:0:
200,200,2000,2,0,L|480:200,2,280
120,120,11827,2,0,L|260:120,1,140
""");
        var beatmap = new BeatmapEditor(map).Beatmap;
        Check(EditorSliderSelection.Resolve("00:01:000 (1,3)", beatmap).Count == 2, "Multiple editor selections");
        Check(EditorSliderSelection.Resolve("00:00:999 (1) -", beatmap)[0].Time == 1000, "Editor timestamp rounding within one millisecond");
        Check(EditorSliderSelection.Resolve("00:11:827 (2) - ", beatmap)[0].Time == 11827, "Timestamp takes priority over different combo numbering");
        bool rejected = false;
        try { EditorSliderSelection.Resolve("00:01:010 (1)", beatmap); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Must not choose a nearby timestamp");
        var vm = new SliderPicturatorVm { BeatmapPath = map, AutoOsuResolution = false, IsGlitchOn = false };
        vm.ImportSliderText("00:11:827 (2) - ");
        Check(vm.TimeCode == 11827 && vm.SelectedSlider.Time == 11827 && vm.Duration == 250, "Copied timestamp fills timing and loads geometry");
        vm.RemoveLibraryItem();
        vm.ImportSliderText("00:01:000 (1,3)");
        Check(vm.LibraryItems.Count == 2 && vm.TimeCode == 2000 && vm.Duration == 1000, "Import repeat duration and time");
        var first = vm.LibraryItems[0]; var second = vm.LibraryItems[1];
        vm.ActiveLibraryItem = first;
        Check(vm.TimeCode == 1000 && vm.Duration == 250, "First slider duration");
        Check(vm.VisibleLayers.Count() == 2, "Imported layers visible together");
        using (var combined = PicturatorExportRequest.Capture(vm)) {
            Check(combined.CompositeField != null && combined.Image.Height > vm.Bm.Height, "Export combines both layer bounds");
            Check(combined.Time == 1000 && combined.Duration == 250, "Composition uses selected timing");
        }
        second.IsVisible = false;
        Check(vm.VisibleLayers.Count() == 1, "Hide removes a layer from composition");
        using (var single = PicturatorExportRequest.Capture(vm)) Check(single.CompositeField == null, "Single visible layer retains native export");
        first.IsVisible = false;
        bool emptyRejected = false;
        try { using var empty = PicturatorExportRequest.Capture(vm); } catch (InvalidOperationException) { emptyRejected = true; }
        Check(emptyRejected, "All-hidden export rejected");
        first.IsVisible = true; second.IsVisible = true;
        vm.Duration = 2;
        using (var combined = PicturatorExportRequest.Capture(vm)) {
            var points = PicturatorExporter.GeneratePath(combined, 5);
            Check(points.Count > 2, "Composite shader field generates an export path");
        }
        vm.Duration = 250;
        vm.ChainAllVisibleBallPaths = true; vm.BallSwitchMilliseconds = 4; vm.Duration = 40;
        using (var multiplex = PicturatorExportRequest.Capture(vm)) {
            Check(multiplex.MultiplexMotion.Count == 2, "Two independent ball routes");
            Check(multiplex.MultiplexMotion.RouteAt(0) == 0 && multiplex.MultiplexMotion.RouteAt(.1) == 1 && multiplex.MultiplexMotion.RouteAt(.2) == 0, "Round-robin switches every four milliseconds");
            var anchors = PicturatorExporter.GeneratePath(multiplex, 5);
            var exported = multiplex.BallSlider.DeepCopy();
            exported.SetAllCurvePoints(anchors);
            exported.PixelLength = 0;
            for (int i = 1; i < anchors.Count; i++) exported.PixelLength += (anchors[i] - anchors[i-1]).Length;
            var exportedPath = exported.GetSliderPath();
            double worst = 0;
            for (int ms = 1; ms < 40; ms++) worst = Math.Max(worst,
                (exportedPath.SliderballPositionAt(ms, 40) - multiplex.MultiplexMotion.PositionAt(ms / 40d)).Length);
            Console.WriteLine($"Multiplex export: {anchors.Count} anchors; maximum integer-ms position error {worst:F3}px");
            Check(worst < 2, "Exported ball follows alternating targets including switch boundaries");
            second.IsVisible = false;
            Check(multiplex.MultiplexMotion.Count == 2 && vm.CreateMultiplexMotion().Count == 1, "Snapshot remains independent of visibility edits");
            second.IsVisible = true;
        }
        foreach(double interval in new[]{1d,.1}) {
            vm.BallSwitchMilliseconds=interval;
            using var hidden=PicturatorExportRequest.CaptureBallOnly(vm);
            Check(hidden.Image.Width==1 && hidden.CompositeField[0,0]>1,"Hidden-body export has no picture samples");
            var points=PicturatorExporter.GeneratePath(hidden,5);
            var output=hidden.BallSlider.DeepCopy(); output.SetAllCurvePoints(points);
            output.PixelLength=Enumerable.Range(1,points.Count-1).Sum(i=>(points[i]-points[i-1]).Length);
            int samples=(int)Math.Ceiling(vm.Duration/hidden.SampleInterval);
            double error=0; var path=output.GetSliderPath();
            for(int i=1;i<samples;i++) error=Math.Max(error,(path.SliderballPositionAt(i,samples)-hidden.MultiplexMotion.PositionAt(i/(double)samples)).Length);
            Check(error<2,"Hidden-body motion follows requested sample targets");
            Console.WriteLine($"Hidden body {interval} ms: {points.Count} anchors; model error {error:F3}px");
        }
        vm.BallSwitchMilliseconds=4;
        vm.DuplicateLibraryItem();
        Check(vm.CreateMultiplexMotion().Count == 3, "Three or more ball routes supported");
        vm.RemoveLibraryItem(); vm.ActiveLibraryItem = first;
        vm.ChainAllVisibleBallPaths = false; vm.Duration = 250;
        vm.SliderScale = 1.4; vm.BallOffsetX = 17; first.Name = "My slider";
        vm.ActiveLibraryItem = second;
        Check(vm.SliderScale == 1 && vm.BallOffsetX == 0, "Independent transforms");
        vm.MovePicture(-40, -80);
        vm.ActiveLibraryItem = first;
        Check(vm.SliderScale == 1.4 && vm.BallOffsetX == 17, "Retained transforms");
        Check(vm.VisibleLayers.First().PreviewImageBounds.IntersectsWith(vm.VisibleLayers.Last().PreviewImageBounds), "Layers can overlap on the same canvas");
        string png = Path.Combine(work, "library-image.png");
        using (var bitmap = new System.Drawing.Bitmap(40, 40)) {
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.Clear(System.Drawing.Color.White); bitmap.Save(png);
        }
        vm.ImportImages(new[] { png });
        Check(vm.LibraryItems.Count == 3 && vm.SelectedSlider == null && vm.Bm != null, "PNG clears shape and loads bitmap");
        vm.ActiveLibraryItem = first;
        vm.DuplicateLibraryItem();
        vm.BallOffsetX = 30;
        vm.ActiveLibraryItem = first;
        Check(vm.BallOffsetX == 17, "Duplicate edits isolated");
        vm.ChainAllVisibleBallPaths = true; vm.BallSwitchMilliseconds = 7;
        vm.SaveSession();
        vm.LibraryItems[2].IsVisible = false;
        var restored = new SliderPicturatorVm();
        Check(restored.LibraryItems.Count == 4 && restored.ActiveLibraryItem.Name == "My slider" && restored.BallOffsetX == 17, "Library persistence");
        Check(!restored.LibraryItems[2].IsVisible && restored.VisibleLayers.Count() == 3, "Visibility persists");
        Check(restored.ChainAllVisibleBallPaths && restored.BallSwitchMilliseconds == 7, "Multiplex settings persist");
        var view = new SliderPicturatorView { DataContext = restored, Width = 1380, Height = 760 };
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        int attempts = 0;
        timer.Tick += (_, _) => { if (restored.VisibleLayers.All(l => !l.IsProcessingPreview && l.BmImage != null) || ++attempts > 100) { timer.Stop(); frame.Continue = false; } };
        timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
        Check(restored.BmImage != null, "Restored bitmap preview completes");
        view.Measure(new Size(1380,760)); view.Arrange(new Rect(0,0,1380,760)); view.UpdateLayout();
        var image = new RenderTargetBitmap(1380,760,96,96,PixelFormats.Pbgra32); image.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (var output = File.Create(Path.Combine(work,"library-preview.png"))) encoder.Save(output);
        while (restored.LibraryItems.Count > 0) restored.RemoveLibraryItem();
        Check(restored.Bm == null && restored.ActiveLibraryItem == null, "Remove final item clears preview");
        Console.WriteLine("PASS: selection timestamps, durations, independent edits, PNG, duplicate, persistence, removal and UI render");
    }
}

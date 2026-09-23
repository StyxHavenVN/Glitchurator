using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StandalonePicturator.Viewmodel;
using StandalonePicturator.View.SliderPicturator;

internal static class SharedMapChecks
{
    static System.Collections.Generic.IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i); if(child is T t)yield return t; foreach(var nested in Children<T>(child))yield return nested; }
    }
    public static void Run(string work)
    {
        var main=new StandalonePicturator.MainWindow();
        var vm=(SliderPicturatorVm)main.DataContext;
        var analyzer=(SliderAnalyzerVm)((FrameworkElement)main.FindName("Analyzer")).DataContext;
        string map=Path.Combine(work,"library.osu"), other=Path.Combine(work,"shared-map.osu");File.Copy(map,other,true);
        vm.BeatmapPath=map;
        if(analyzer.OsuPath!=map)throw new Exception("Picturator to Analyzer shared map");
        analyzer.OsuPath=other;
        if(vm.BeatmapPath!=other)throw new Exception("Analyzer to Picturator shared map");
        vm.ImportSliderText("00:01:000 (1,3)");vm.HasSliderBall=true;vm.ChainAllVisibleBallPaths=true;vm.BallSwitchMilliseconds=.1;
        vm.Duration=20;
        using(var request=StandalonePicturator.Classes.PicturatorExportRequest.CaptureBallOnly(vm)) {
            StandalonePicturator.Classes.PicturatorExporter.Export(request);
            var saved=new StandalonePicturator.Classes.BeatmapHelper.BeatmapEditor(other).Beatmap;
            var output=saved.HitObjects.Single(h=>h.IsSlider && h.Time==1999);
            if(output.Repeat!=1 || output.PixelLength<=0 || !File.Exists(other+".bak"))throw new Exception("Ball-only serialized export and backup");
        }
        main.Show();main.UpdateLayout();
        var tabs=Children<TabControl>(main).First();
        if(tabs.Items.Count!=3)throw new Exception("Visible tab count");
        tabs.SelectedIndex=1;main.UpdateLayout();
        var view=Children<SliderballView>(main).Single();
        if(view.DataContext!=vm)throw new Exception("Sliderball shares library and map");
        var image=new RenderTargetBitmap((int)main.ActualWidth,(int)main.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(main);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var output=File.Create(Path.Combine(work,"sliderball-tab.png")))encoder.Save(output);
        main.Close();
        Console.WriteLine("PASS: three tabs, shared map in both directions, shared Sliderball context and UI render");
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using StandalonePicturator.Classes;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.SliderPicturator;

public partial class SliderballView : UserControl
{
    public SliderballView() { InitializeComponent(); }
    private SliderPicturatorVm Model => DataContext as SliderPicturatorVm;
    private void Remove_Click(object sender,RoutedEventArgs e) => Model?.RemoveLibraryItem();
    private void Graph_Click(object sender,RoutedEventArgs e)
    {
        if(Model==null)return;
        var grid=new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(2,GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new BallGraphEditor { BallOnly=true });
        var preview=new PicturatorPreview { BallOnly=true }; Grid.SetColumn(preview,1);grid.Children.Add(preview);
        new Window { Title="Sliderball graph — hidden body",Content=grid,DataContext=Model,Width=1180,Height=760,Owner=Window.GetWindow(this) }.ShowDialog();
    }
    private async void Export_Click(object sender,RoutedEventArgs e)
    {
        if(Model==null || !ExportButton.IsEnabled)return;
        if(!File.Exists(Model.BeatmapPath)) { Status.Text="Choose the shared .osu file first.";return; }
        ExportButton.IsEnabled=false; Status.Text="Generating motion without picture scanlines…";
        try {
            Model.SyncOsuResolution();
            using var request=PicturatorExportRequest.CaptureBallOnly(Model);
            var slider=await Task.Run(()=>PicturatorExporter.Export(request));
            Status.Text=$"Exported one slider with {slider.CurvePoints.Count:N0} anchors. Original map backed up as .bak.";
        } catch(Exception ex) { Status.Text=ex.Message; }
        finally { ExportButton.IsEnabled=true; }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using StandalonePicturator.Classes;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.SliderPicturator;

public partial class SliderPicturatorView : System.Windows.Controls.UserControl
{
    public static readonly string ToolName = "Slider Picturator";
    public static readonly string ToolDescription = "Convert an image into a slider with an aligned sliderball path.";

    public SliderPicturatorView()
    {
        InitializeComponent();
        DataContext = new SliderPicturatorVm();
    }

    public SliderPicturatorVm ViewModel => (SliderPicturatorVm)DataContext;

    private void ImportClipboard_Click(object sender, RoutedEventArgs e) => ViewModel.ImportClipboardSliders();
    private void DuplicateItem_Click(object sender, RoutedEventArgs e) => ViewModel.DuplicateLibraryItem();
    private void RemoveItem_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveLibraryItem();
    private void Library_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) ViewModel.ImportImages(files);
        e.Handled = true;
    }

    private void OpenGraph_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetPreviewProgress(ViewModel.GetPreviewProgress(), true);
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(8) };
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var graph = new BallGraphEditor { Margin = new Thickness(0, 0, 8, 0) };
        var preview = new PicturatorPreview();
        System.Windows.Controls.Grid.SetColumn(preview, 1);
        grid.Children.Add(graph); grid.Children.Add(preview);
        var window = new Window {
            Title = "Sliderball Graph — time and position along the slider", Width = 1180, Height = 740,
            MinWidth = 860, MinHeight = 560, Owner = Window.GetWindow(this), WindowStartupLocation = WindowStartupLocation.CenterOwner,
            DataContext = ViewModel, Content = grid
        };
        window.ShowDialog();
    }

    private void ResetBall_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetBallPlacement();
        ViewModel.SaveSession();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!InjectButton.IsEnabled) return;
        if (!File.Exists(ViewModel.BeatmapPath)) {
            var dialog = new OpenFileDialog { Filter = "osu! beatmap (*.osu)|*.osu", Title = "Choose the destination beatmap" };
            if (dialog.ShowDialog() != true) return;
            ViewModel.BeatmapPath = dialog.FileName;
        }
        InjectButton.IsEnabled = false;
        try {
            // Snapshot on the UI thread: editing the preview cannot change a running export.
            ViewModel.SyncOsuResolution();
            using var request = PicturatorExportRequest.Capture(ViewModel);
            await Task.Run(() => PicturatorExporter.Export(request));
            MessageBox.Show("Slider Picturator exported. The previous file is backed up as .bak.", "Done");
        } catch (Exception ex) {
            MessageBox.Show(ex.Message, "Cannot create slider");
        } finally { InjectButton.IsEnabled = true; }
    }
}
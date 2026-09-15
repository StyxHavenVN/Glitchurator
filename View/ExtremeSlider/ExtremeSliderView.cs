using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Newtonsoft.Json;
using StandalonePicturator.Classes;

namespace StandalonePicturator.View.ExtremeSlider;

public sealed class ExtremeSliderView : UserControl
{
    private ExtremeSliderSettings settings = new();
    private readonly TextBox source = Box(true), output = Box(true), map = Box();
    private readonly Dictionary<string, TextBox> numbers = new();
    private readonly Canvas diagram = new() { Height = 225, Background = new SolidColorBrush(Color.FromRgb(16, 23, 31)), ClipToBounds = true };
    private readonly TextBlock status = Text("");
    private static string SessionPath => System.IO.Path.Combine(AppContext.BaseDirectory, "extreme_slider_session.json");

    public ExtremeSliderView()
    {
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(Text("Giant & Matrix Slider • experimental", 23));
        root.Children.Add(Text("C = Catmull. Extreme coordinates extend beyond the playfield. These presets are starting points for osu! Stable; matrix appearance depends on the renderer, GPU and resolution."));
        var fileRow = new DockPanel();
        var select = Button("Choose map…", () => {
            var dialog = new OpenFileDialog { Filter = "osu! beatmap|*.osu" };
            if (dialog.ShowDialog() == true) map.Text = dialog.FileName;
        });
        DockPanel.SetDock(select, Dock.Right); fileRow.Children.Add(select); fileRow.Children.Add(map); root.Children.Add(fileRow);
        var presets = new WrapPanel();
        presets.Children.Add(Button("Original C preset", () => { source.Text = Classes.ExtremeSlider.Example; ResetTransform(); Generate(); }));
        presets.Children.Add(Button("Vertical matrix", () => Preset(true)));
        presets.Children.Add(Button("Horizontal matrix", () => Preset(false)));
        root.Children.Add(presets);
        var fields = new WrapPanel();
        foreach (var entry in new[] { ("Time", "Start (ms)"), ("Duration", "Duration (ms)"), ("ScaleX", "X scale"), ("ScaleY", "Y scale"), ("OffsetX", "X offset"), ("OffsetY", "Y offset"), ("Lines", "Preset line count") }) {
            var column = new StackPanel { Width = 118, Margin = new Thickness(0, 0, 8, 0) };
            column.Children.Add(Text(entry.Item2)); var input = Box(); numbers[entry.Item1] = input;
            column.Children.Add(input); fields.Children.Add(column);
        }
        root.Children.Add(fields);
        root.Children.Add(Text("Source [HitObjects] line — edit X:Y anchors directly (C or L)"));
        source.Height = 90; root.Children.Add(source);
        var actions = new WrapPanel();
        actions.Children.Add(Button("Generate / update", Generate));
        actions.Children.Add(Button("Copy slider", () => { if (GenerateCore()) { Clipboard.SetText(output.Text); status.Text = "Slider copied. Export .osu to include timing for the chosen duration."; } }));
        actions.Children.Add(Button("Export .osu copy…", Export)); root.Children.Add(actions);
        root.Children.Add(status);
        root.Children.Add(Text("Anchor diagram (X/Y normalized separately) — not an in-game shader or matrix simulation"));
        root.Children.Add(diagram);
        root.Children.Add(Text("Generated slider (length recalculated from anchor distances)"));
        output.IsReadOnly = true; output.Height = 85; root.Children.Add(output);
        root.Children.Add(Text("Export uses timing at T-1, a NaN greenline to suppress ticks, and restores timing/SV at T. Native skin colors are retained. Use a different output filename to test separately."));
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        LoadSession(); FillFields();
        Loaded += (_, _) => Generate();
        diagram.SizeChanged += (_, _) => { if (!string.IsNullOrEmpty(output.Text)) Draw(); };
    }

    private static TextBlock Text(string value, double size = 12) => new() { Text = value, FontSize = size, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 5, 2, 5) };
    private static TextBox Box(bool multiline = false) => new() { AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(2), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(42, 42, 48)) };
    private static Button Button(string title, Action action) { var button = new Button { Content = title, Margin = new Thickness(3), Padding = new Thickness(9, 5, 9, 5) }; button.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Giant & Matrix"); } }; return button; }
    private void ResetTransform() { numbers["ScaleX"].Text = "1"; numbers["ScaleY"].Text = "1"; numbers["OffsetX"].Text = "0"; numbers["OffsetY"].Text = "0"; }
    private void Preset(bool vertical) {
        int count = int.Parse(numbers["Lines"].Text, CultureInfo.InvariantCulture);
        if (count < 2 || count > 128) throw new ArgumentException("Preset line count must be 2–128.");
        source.Text = Classes.ExtremeSlider.Matrix(count, vertical); ResetTransform(); Generate();
    }
    private void FillFields() {
        map.Text = settings.MapPath; source.Text = settings.Source;
        foreach (var field in numbers) field.Value.Text = Convert.ToString(typeof(ExtremeSliderSettings).GetProperty(field.Key).GetValue(settings), CultureInfo.InvariantCulture);
    }
    private ExtremeSliderSettings ReadFields() {
        var next = new ExtremeSliderSettings { Source = source.Text, MapPath = map.Text };
        foreach (var field in numbers) {
            var property = typeof(ExtremeSliderSettings).GetProperty(field.Key);
            if (property.PropertyType == typeof(int)) property.SetValue(next, int.Parse(field.Value.Text, CultureInfo.InvariantCulture));
            else property.SetValue(next, double.Parse(field.Value.Text, CultureInfo.InvariantCulture));
        }
        return next;
    }
    private bool GenerateCore() {
        try {
            var next = ReadFields(); var slider = Classes.ExtremeSlider.Create(next);
            settings = next; output.Text = slider.GetLine(); SaveSession(); Draw();
            status.Text = $"{slider.CurvePoints.Count + 1} anchor • Length {slider.PixelLength:N2} • Duration {slider.TemporalLength:N0} ms. Preview is a diagram; the in-game effect is unverified.";
            return true;
        } catch (Exception ex) { output.Clear(); diagram.Children.Clear(); status.Text = ex.Message; return false; }
    }
    private void Generate() => GenerateCore();
    private void Draw() {
        if (diagram.ActualWidth <= 0) return;
        var points = Classes.ExtremeSlider.Create(settings).GetAllCurvePoints();
        double minX = points.Min(p => p.X), minY = points.Min(p => p.Y);
        double width = Math.Max(1, points.Max(p => p.X) - minX), height = Math.Max(1, points.Max(p => p.Y) - minY);
        diagram.Children.Clear();
        var line = new Polyline { Stroke = Brushes.Turquoise, StrokeThickness = 1.2 };
        foreach (var point in points) line.Points.Add(new Point(12 + (point.X - minX) / width * (diagram.ActualWidth - 24), 12 + (point.Y - minY) / height * (diagram.Height - 24)));
        diagram.Children.Add(line);
        foreach (var point in line.Points.Take(129)) {
            var dot = new Ellipse { Width = 5, Height = 5, Fill = Brushes.Gold };
            Canvas.SetLeft(dot, point.X - 2.5); Canvas.SetTop(dot, point.Y - 2.5); diagram.Children.Add(dot);
        }
    }
    private void Export() {
        if (!GenerateCore()) return;
        if (!File.Exists(settings.MapPath)) throw new ArgumentException("Choose a source beatmap before exporting.");
        var dialog = new SaveFileDialog { Filter = "osu! beatmap|*.osu", InitialDirectory = System.IO.Path.GetDirectoryName(settings.MapPath), FileName = System.IO.Path.GetFileNameWithoutExtension(settings.MapPath) + " [Giant Matrix].osu" };
        if (dialog.ShowDialog() != true) return;
        Classes.ExtremeSlider.Write(settings, dialog.FileName);
        status.Text = "Exported: " + dialog.FileName + ". Reload in osu! to check the effect.";
    }
    private void SaveSession() => File.WriteAllText(SessionPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
    private void LoadSession() { try { if (File.Exists(SessionPath)) settings = JsonConvert.DeserializeObject<ExtremeSliderSettings>(File.ReadAllText(SessionPath)) ?? new(); } catch { settings = new(); } }
}

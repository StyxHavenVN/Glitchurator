using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using StandalonePicturator.Classes;
using V = StandalonePicturator.Classes.MathUtil.Vector2;

namespace StandalonePicturator.View.TickArt;

public sealed class TickArtView : UserControl
{
    private TickArtSettings settings = new();
    private readonly TextBox map = Input(), time = Input(), duration = Input(), spacing = Input(), rate = Input();
    private readonly CheckBox overrideRate = new() { Content = "Override tick rate for the entire exported map", Foreground = Brushes.Gold, Margin = new Thickness(4) };
    private readonly TextBlock status = Label("");
    private readonly TickSurface surface = new();
    private static string SessionPath => Path.Combine(AppContext.BaseDirectory, "tick_art_session.json");
    public TickArtView()
    {
        try { if (File.Exists(SessionPath)) settings = JsonConvert.DeserializeObject<TickArtSettings>(File.ReadAllText(SessionPath)) ?? new(); } catch { settings = new(); }
        settings.Points ??= new();
        var root = new DockPanel { Margin = new Thickness(10), Background = new SolidColorBrush(Color.FromRgb(24, 27, 33)) };
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Tick Art — draw with slider ticks", FontSize = 23, Foreground = Brushes.Turquoise, Margin = new Thickness(4) });
        var mapRow = new DockPanel(); var choose = Button("Choose map…", () => {
            var dialog = new OpenFileDialog { Filter = "osu! beatmap|*.osu" };
            if (dialog.ShowDialog() == true) { map.Text = dialog.FileName; Update(); }
        });
        DockPanel.SetDock(choose, Dock.Right); mapRow.Children.Add(choose); mapRow.Children.Add(map); top.Children.Add(mapRow);
        var fields = new WrapPanel();
        foreach (var pair in new[] { ("Start (ms)", time), ("Duration (ms)", duration), ("Tick spacing (px)", spacing), ("New tick rate", rate) }) {
            var column = new StackPanel { Width = 130, Margin = new Thickness(3) };
            column.Children.Add(Label(pair.Item1)); column.Children.Add(pair.Item2); fields.Children.Add(column);
        }
        top.Children.Add(fields); top.Children.Add(overrideRate);
        top.Children.Add(Label("Click to add points; hold the left mouse button to draw. Points form one slider. Right click removes the last point."));
        var actions = new WrapPanel();
        actions.Children.Add(Button("Star / X preset", () => { settings.Points = Classes.TickArt.Star(); Update(); }));
        actions.Children.Add(Button("Undo point", Undo));
        actions.Children.Add(Button("Clear drawing", () => { settings.Points.Clear(); Update(); }));
        actions.Children.Add(Button("Close path", () => { if (settings.Points.Count > 1) settings.Points.Add(settings.Points[0]); Update(); }));
        actions.Children.Add(Button("Update ticks", () => Update()));
        actions.Children.Add(Button("Export .osu…", Export)); top.Children.Add(actions);
        status.TextWrapping = TextWrapping.Wrap; top.Children.Add(status);
        var bottom = Label("Preview shows estimated tick positions, not skin glow or oversized slider backgrounds. Keeping the map tick rate limits spacing to SV 0.1–10x.");
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); root.Children.Add(surface); Content = root;
        map.Text = settings.MapPath;
        time.Text = settings.Time.ToString(CultureInfo.InvariantCulture); duration.Text = settings.Duration.ToString(CultureInfo.InvariantCulture);
        spacing.Text = settings.Spacing.ToString(CultureInfo.InvariantCulture); rate.Text = settings.TickRate.ToString(CultureInfo.InvariantCulture); overrideRate.IsChecked = settings.OverrideTickRate;
        surface.AddPoint = p => {
            if (settings.Points.Count < 4096 && (settings.Points.Count == 0 || (settings.Points.Last() - p).Length >= 3)) { settings.Points.Add(p); Update(false); }
        };
        surface.Finished = () => Update(); surface.Undo = Undo;
        foreach (var input in new[] { map, time, duration, spacing, rate }) input.LostKeyboardFocus += (_, _) => Update();
        overrideRate.Checked += (_, _) => Update(); overrideRate.Unchecked += (_, _) => Update();
        Loaded += (_, _) => Update();
        Update(false);
    }
    private static TextBlock Label(string text) => new() { Text = text, Foreground = Brushes.LightGray, Margin = new Thickness(4), TextWrapping = TextWrapping.Wrap };
    private static TextBox Input() => new() { Margin = new Thickness(3), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(40, 42, 49)) };
    private static Button Button(string text, Action action) { var b = new Button { Content = text, Margin = new Thickness(3), Padding = new Thickness(8, 5, 8, 5) }; b.Click += (_, _) => action(); return b; }
    private void Undo() { if (settings.Points.Count > 0) settings.Points.RemoveAt(settings.Points.Count - 1); Update(); }
    private TickArtPlan Update(bool save = true)
    {
        surface.Points = settings.Points; surface.Plan = null;
        try {
            settings.MapPath = map.Text;
            settings.Time = double.Parse(time.Text, CultureInfo.InvariantCulture); settings.Duration = double.Parse(duration.Text, CultureInfo.InvariantCulture);
            settings.Spacing = double.Parse(spacing.Text, CultureInfo.InvariantCulture); settings.TickRate = double.Parse(rate.Text, CultureInfo.InvariantCulture);
            settings.OverrideTickRate = overrideRate.IsChecked == true;
            // Read map settings once after editing, not for every freehand mouse sample.
            var plan = save ? Classes.TickArt.ForMap(settings) : Classes.TickArt.Create(settings, cachedMultiplier, cachedRate);
            if (save && File.Exists(settings.MapPath)) {
                var bm = new StandalonePicturator.Classes.BeatmapHelper.BeatmapEditor(settings.MapPath).Beatmap;
                cachedMultiplier = bm.Difficulty["SliderMultiplier"].DoubleValue; cachedRate = bm.Difficulty["SliderTickRate"].DoubleValue;
            }
            surface.Plan = plan;
            status.Text = $"{plan.Ticks.Count:N0} tick • Actual spacing {plan.Spacing:F2} px • SV {plan.Sv:F3}× • {settings.Points.Count} drawing points" +
                (File.Exists(settings.MapPath) ? "" : "• No map selected: assuming SliderMultiplier 1.4 / TickRate 1") +
                (settings.OverrideTickRate ? "• The new tick rate affects every slider in the exported map." : "");
            if (save) Save(); return plan;
        } catch (Exception ex) { status.Text = ex.Message; if (save && settings.Points.Count < 2) Save(); return null; }
        finally { surface.InvalidateVisual(); }
    }
    private double cachedMultiplier = 1.4, cachedRate = 1;
    private void Save() { try { File.WriteAllText(SessionPath, JsonConvert.SerializeObject(settings, Formatting.Indented)); } catch (IOException ex) { status.Text = "Cannot save session: " + ex.Message; } }
    private void Export() {
        try {
            if (Update() == null) return;
            if (!File.Exists(settings.MapPath)) { status.Text = "Choose a source map before exporting."; return; }
            var dialog = new SaveFileDialog { Filter = "osu! beatmap|*.osu", InitialDirectory = Path.GetDirectoryName(settings.MapPath), FileName = Path.GetFileNameWithoutExtension(settings.MapPath) + " [Tick Art].osu" };
            if (dialog.ShowDialog() != true) return;
            var plan = Classes.TickArt.Write(settings, dialog.FileName);
            status.Text = $"Exported {plan.Ticks.Count} ticks: {dialog.FileName}. Reload in osu! to view it with your skin.";
        } catch (Exception ex) { status.Text = ex.Message; }
    }

    private sealed class TickSurface : FrameworkElement
    {
        public List<V> Points = new(); public TickArtPlan Plan;
        public Action<V> AddPoint; public Action Finished, Undo;
        private double Scale => Math.Max(.001, Math.Min(ActualWidth / 512, ActualHeight / 384));
        private double X => (ActualWidth - 512 * Scale) / 2; private double Y => (ActualHeight - 384 * Scale) / 2;
        public TickSurface() {
            MinHeight = 160; ClipToBounds = true; Cursor = Cursors.Pen;
            MouseLeftButtonDown += (_, e) => { CaptureMouse(); Add(e); e.Handled = true; };
            MouseMove += (_, e) => { if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) Add(e); };
            MouseLeftButtonUp += (_, _) => { ReleaseMouseCapture(); Finished?.Invoke(); };
            MouseRightButtonDown += (_, e) => { Undo?.Invoke(); e.Handled = true; };
        }
        private void Add(MouseEventArgs e) { var p = e.GetPosition(this); AddPoint?.Invoke(new V(Math.Round(Math.Clamp((p.X - X) / Scale, 0, 512)), Math.Round(Math.Clamp((p.Y - Y) / Scale, 0, 384)))); }
        protected override void OnRender(DrawingContext dc) {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(14, 20, 28)), null, new Rect(RenderSize));
            Point Screen(V p) => new(X + p.X * Scale, Y + p.Y * Scale);
            var grid = new Pen(new SolidColorBrush(Color.FromRgb(35, 45, 55)), 1);
            for (int x = 0; x <= 512; x += 64) dc.DrawLine(grid, Screen(new(x, 0)), Screen(new(x, 384)));
            for (int y = 0; y <= 384; y += 64) dc.DrawLine(grid, Screen(new(0, y)), Screen(new(512, y)));
            for (int i = 1; i < Points.Count; i++) dc.DrawLine(new Pen(Brushes.DimGray, 1), Screen(Points[i - 1]), Screen(Points[i]));
            if (Plan != null) foreach (var tick in Plan.Ticks) dc.DrawEllipse(Brushes.White, null, Screen(tick), 2.5, 2.5);
            if (Points.Count > 0) dc.DrawEllipse(null, new Pen(Brushes.Gold, 2), Screen(Points[0]), 7, 7);
        }
    }
}

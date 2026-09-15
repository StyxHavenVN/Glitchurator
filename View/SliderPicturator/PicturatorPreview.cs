using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StandalonePicturator.Classes.BeatmapHelper.SliderPathStuff;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.SliderPicturator;

/// <summary>Edits in osu! coordinates; the window's size never changes exported coordinates.</summary>
public sealed class PicturatorPreview : UserControl
{
    private readonly PreviewSurface surface = new();
    private readonly Slider timeline = new() { Minimum = 0, Maximum = 1, Margin = new Thickness(6), Width = 180 };
    private readonly ToggleButton play = new() { Content = "Play", Padding = new Thickness(10, 4, 10, 4) };
    private readonly TextBlock timeLabel = new() { Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch elapsed = new();
    private SliderPicturatorVm model;
    private double startProgress;

    public PicturatorPreview()
    {
        var root = new DockPanel();
        var hint = new TextBlock {
            Text = "Drag image: move • Drag square / scroll: scale\nShift + drag: move ball path • Ctrl + scroll: thickness (CS)\nPreview shows placement; verify shader/glitch appearance in osu!.",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6), FontSize = 11
        };
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);
        var toolbar = new WrapPanel { Margin = new Thickness(4) };
        var fit = new Button { Content = "Fit view", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 4, 8, 4) };
        fit.Click += (_, _) => surface.Fit();
        toolbar.Children.Add(play);
        toolbar.Children.Add(timeline);
        toolbar.Children.Add(timeLabel);
        toolbar.Children.Add(fit);
        DockPanel.SetDock(toolbar, Dock.Bottom);
        root.Children.Add(toolbar);
                var previewLayer = new Grid();
        previewLayer.Children.Add(surface);
        var overlays = new Canvas { ClipToBounds = true };
        var pathPanel = new BallPathPanel();
        Canvas.SetLeft(pathPanel, 12); Canvas.SetTop(pathPanel, 32);
        overlays.Children.Add(pathPanel);
        previewLayer.Children.Add(overlays);
        root.Children.Add(previewLayer);
        Content = root;
        play.Checked += (_, _) => { startProgress = timeline.Value; elapsed.Restart(); };
        play.Unchecked += (_, _) => elapsed.Stop();
        timeline.PreviewMouseLeftButtonDown += (_, _) => play.IsChecked = false;
        timeline.ValueChanged += (_, _) => UpdateProgress();
        timer.Tick += (_, _) => {
            if (play.IsChecked == true && model != null) {
                timeline.Value = (startProgress + elapsed.Elapsed.TotalMilliseconds / Math.Max(2, model.Duration)) % 1;
            }
        };
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => { Attach(); timer.Start(); };
        Unloaded += (_, _) => {
            timer.Stop(); play.IsChecked = false;
            if (model != null) { model.PropertyChanged -= Changed; model.PreviewProgressChanged -= Seek; }
            model = null;
        };
    }

    private void Attach()
    {
        if (model != null) { model.PropertyChanged -= Changed; model.PreviewProgressChanged -= Seek; }
        model = DataContext as SliderPicturatorVm;
        model?.SyncOsuResolution();
        surface.Model = model;
        if (model != null) { model.PropertyChanged += Changed; model.PreviewProgressChanged += Seek; timeline.Value = model.GetPreviewProgress(); }
        surface.RefreshPath();
        surface.Fit();
        UpdateProgress();
    }

    private void Changed(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SliderPicturatorVm.SliderStartX) or nameof(SliderPicturatorVm.SliderStartY)
            or nameof(SliderPicturatorVm.SliderScale) or nameof(SliderPicturatorVm.BallOffsetX)
            or nameof(SliderPicturatorVm.BallPathScale) or nameof(SliderPicturatorVm.BallOffsetY) or nameof(SliderPicturatorVm.BallPathSlider)
            or nameof(SliderPicturatorVm.BallGraphEnabled) or nameof(SliderPicturatorVm.SelectedSlider) or nameof(SliderPicturatorVm.HasSliderBall)) surface.RefreshPath();
        surface.InvalidateVisual();
        UpdateProgress();
    }

    private void Seek(double progress, bool pause)
    {
        if (pause) play.IsChecked = false;
        timeline.Value = progress;
    }

    private void UpdateProgress()
    {
        model?.SetPreviewProgress(timeline.Value);
        surface.Progress = timeline.Value;
        surface.InvalidateVisual();
        timeLabel.Text = $"{timeline.Value * (model?.Duration ?? 1000):F0} ms";
    }

    private sealed class PreviewSurface : FrameworkElement
    {
        public SliderPicturatorVm Model { get; set; }
        public double Progress { get; set; }
        private SliderPath? path;
        private Geometry route;
        private Rect world = new(-128, -96, 768, 576);
        private Point previous;
        private Point resizeOrigin;
        private Vector resizeVector;
        private double originalScale;
        private int dragMode;
        private Rect handle;
        private Rect ballFrame;
        private Point ballCenter;
        private double ballScreenRadius;
        private readonly Stopwatch dragThrottle = Stopwatch.StartNew();

        public PreviewSurface()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = Cursors.SizeAll;
            MouseLeftButtonDown += BeginDrag;
            MouseMove += Drag;
            MouseLeftButtonUp += (_, e) => { ApplyDrag(e.GetPosition(this), true); dragMode = 0; ReleaseMouseCapture(); };
            LostMouseCapture += (_, _) => dragMode = 0;
            MouseWheel += (_, e) => {
                if (Model?.Bm == null || Model.ActiveLibraryItem?.IsVisible == false) return;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) Model.TargetCS = Math.Clamp(Model.TargetCS + Math.Sign(e.Delta) * 0.1, 0, 10);
                else Model.SliderScale = Math.Clamp(Model.SliderScale * (e.Delta > 0 ? 1.05 : 1 / 1.05), 0.2, 3);
                e.Handled = true;
            };
        }

        public void RefreshPath()
        {
            var slider = Model?.CreateBallSlider();
            path = slider?.GetSliderPath();
            route = null;
            if (path.HasValue) {
                var points = path.Value.CalculatedPath;
                if (points.Count > 1) {
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open()) {
                        context.BeginFigure(new Point(points[0].X, points[0].Y), false, false);
                        context.PolyLineTo(points.Skip(1).Select(p => new Point(p.X, p.Y)).ToArray(), true, false);
                    }
                    geometry.Freeze();
                    route = geometry;
                }
            }
            InvalidateVisual();
        }

        public void Fit()
        {
            var bounds = new Rect(0, 0, 512, 384);
            if (Model != null) foreach (var layer in Model.VisibleLayers) bounds.Union(layer.PreviewImageBounds);
            if (route != null) bounds.Union(route.Bounds);
            bounds.Inflate(64, 64);
            world = bounds;
            InvalidateVisual();
        }

        private double Zoom => Math.Max(0.001, Math.Min(ActualWidth / world.Width, ActualHeight / world.Height));
        private Point ToScreen(Point point) => new((point.X - world.X) * Zoom + (ActualWidth - world.Width * Zoom) / 2,
            (point.Y - world.Y) * Zoom + (ActualHeight - world.Height * Zoom) / 2);
        private Point ToWorld(Point point) => new((point.X - (ActualWidth - world.Width * Zoom) / 2) / Zoom + world.X,
            (point.Y - (ActualHeight - world.Height * Zoom) / 2) / Zoom + world.Y);
        private Rect ScreenRect(Rect rect) => new(ToScreen(rect.TopLeft), ToScreen(rect.BottomRight));

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 23, 30)), null, new Rect(RenderSize));
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(35, 45, 55)), 1);
            for (int x = 0; x <= 512; x += 64) dc.DrawLine(gridPen, ToScreen(new Point(x, 0)), ToScreen(new Point(x, 384)));
            for (int y = 0; y <= 384; y += 64) dc.DrawLine(gridPen, ToScreen(new Point(0, y)), ToScreen(new Point(512, y)));
            dc.DrawRectangle(null, new Pen(Brushes.SlateGray, 1), ScreenRect(new Rect(0, 0, 512, 384)));
            DrawText(dc, "osu! 512 × 384", new Point(8, 8), Brushes.SlateGray);
            if (Model != null) foreach (var layer in Model.VisibleLayers)
                if (layer.BmImage != null) dc.DrawImage(layer.BmImage, ScreenRect(layer.PreviewImageBounds));
            handle = Rect.Empty; ballFrame = Rect.Empty;
            if (Model?.Bm == null) {
                DrawText(dc, "Import a slider or image to start editing", new Point(16, 42), Brushes.LightGray);
                return;
            }
            if (Model.ActiveLibraryItem?.IsVisible == false) return;
            var rect = ScreenRect(Model.PreviewImageBounds);
            dc.DrawRectangle(null, new Pen(Brushes.Turquoise, 1), rect);
            handle = new Rect(rect.BottomRight - new Vector(6, 6), new Size(12, 12));
            dc.DrawRectangle(Brushes.Turquoise, new Pen(Brushes.White, 1), handle);
            ballFrame = Rect.Empty;
            if (route != null) {
                ballFrame = ScreenRect(route.Bounds);
                ballFrame.Inflate(10, 10);
                dc.DrawRectangle(null, new Pen(Brushes.Gold, 1) { DashStyle = DashStyles.Dot }, ballFrame);
                var origin = ToScreen(new Point(0, 0));
                dc.PushTransform(new MatrixTransform(Zoom, 0, 0, Zoom, origin.X, origin.Y));
                dc.DrawGeometry(null, new Pen(Brushes.Gold, 1.5 / Zoom) { DashStyle = DashStyles.Dash }, route);
                dc.Pop();
            }
            var pos = path?.PositionAt(Model.EvaluateBallProgress(Progress));
            var ball = ToScreen(pos.HasValue ? new Point(pos.Value.X, pos.Value.Y) : new Point(Model.SliderStartX, Model.SliderStartY));
            double radius = Model.GetPreviewBallRadius() * Zoom;
            ballCenter = ball; ballScreenRadius = radius;
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 255, 210, 65)), new Pen(Brushes.Gold, 2), ball, radius, radius);
            dc.DrawEllipse(Brushes.White, null, ball, 3, 3);
            DrawText(dc, $"X {Model.SliderStartX:F1}  Y {Model.SliderStartY:F1}  •  {Model.SliderScale:F2}×  •  CS {Model.TargetCS:F1}",
                new Point(8, ActualHeight - 26), Brushes.Turquoise);
        }

        private void DrawText(DrawingContext dc, string text, Point point, Brush brush) => dc.DrawText(new FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip), point);

        private void BeginDrag(object sender, MouseButtonEventArgs e)
        {
            if (Model?.Bm == null || Model.ActiveLibraryItem?.IsVisible == false) return;
            Focus();
            var screen = e.GetPosition(this);
            previous = ToWorld(screen);
                        var innerFrame = ballFrame;
            if (!innerFrame.IsEmpty) innerFrame.Inflate(-Math.Min(7, innerFrame.Width / 3), -Math.Min(7, innerFrame.Height / 3));
            bool ballHit = Model.HasSliderBall && (screen - ballCenter).Length <= ballScreenRadius;
            bool frameHit = !ballFrame.IsEmpty && ballFrame.Contains(screen) && !innerFrame.Contains(screen);
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || ballHit || frameHit) dragMode = 3;
            else if (handle.Contains(screen)) {
                dragMode = 2;
                originalScale = Model.SliderScale;
                resizeOrigin = Model.SelectedSlider != null ? new Point(Model.SliderStartX, Model.SliderStartY) : Model.PreviewImageBounds.TopLeft;
                resizeVector = previous - resizeOrigin;
            } else if (Model.PreviewImageBounds.Contains(previous)) dragMode = 1;
            else return;
            CaptureMouse();
            e.Handled = true;
        }

        private void Drag(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) ApplyDrag(e.GetPosition(this), false);
        }

        private void ApplyDrag(Point screen, bool final)
        {
            if (dragMode == 0 || Model == null) return;
            var current = ToWorld(screen);
            var delta = current - previous;
            if (dragMode == 1) Model.MovePicture(delta.X, delta.Y);
            else if (dragMode == 3) { Model.BallOffsetX += delta.X; Model.BallOffsetY += delta.Y; }
            else if (dragMode == 2 && (final || dragThrottle.ElapsedMilliseconds >= 60)) {
                double factor = Vector.Multiply(current - resizeOrigin, resizeVector) / Math.Max(1, resizeVector.LengthSquared);
                Model.SliderScale = Math.Clamp(originalScale * factor, 0.2, 3);
                dragThrottle.Restart();
            }
            previous = current;
            InvalidateVisual();
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using StandalonePicturator.Classes;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.SliderPicturator;

// Interactive timeline and curve editor for non-linear sliderball motion graphs
public sealed class BallGraphEditor : UserControl
{
    public bool BallOnly { get; set; }

    private readonly GraphSurface graph = new();
    private readonly TextBox time = new() { Name = "GraphTimeInput", Width = 74, Margin = new Thickness(4) };
    private readonly TextBox position = new() { Name = "GraphPositionInput", Width = 64, Margin = new Thickness(4) };
    private readonly ComboBox curve = new()
    {
        Name = "GraphCurveInput",
        Foreground = Brushes.Black,
        Background = Brushes.White,
        Width = 144,
        Margin = new Thickness(4),
        DisplayMemberPath = "Value",
        SelectedValuePath = "Key"
    };

    private readonly TextBlock status = new() { Foreground = Brushes.Gold, Margin = new Thickness(6), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock segmentStatus = new() { Text = "Total segments not calculated", Foreground = Brushes.Turquoise, Margin = new Thickness(5), TextWrapping = TextWrapping.Wrap };
    private readonly Button countSegments = new() { Content = "Calculate segments", Margin = new Thickness(4), Padding = new Thickness(8, 4, 8, 4) };

    private SliderPicturatorVm model;
    private bool refreshing;
    private int segmentRevision;
    private readonly System.Windows.Threading.DispatcherTimer segmentTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };

    public static readonly KeyValuePair<BallGraphCurve, string>[] MappingToolsModes =
    {
        new(BallGraphCurve.SingleCurve, "Single curve"),
        new(BallGraphCurve.SingleCurve2, "Single curve 2"),
        new(BallGraphCurve.SingleCurve3, "Single curve 3"),
        new(BallGraphCurve.DoubleCurve, "Double curve"),
        new(BallGraphCurve.DoubleCurve2, "Double curve 2"),
        new(BallGraphCurve.DoubleCurve3, "Double curve 3"),
        new(BallGraphCurve.HalfSine, "Half sine"),
        new(BallGraphCurve.Wave, "Wave"),
        new(BallGraphCurve.Parabola, "Parabola"),
        new(BallGraphCurve.Linear, "Linear"),
        new(BallGraphCurve.Hold, "Hold")
    };

    public BallGraphEditor()
    {
        var root = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(22, 23, 29)) };
        var tools = new WrapPanel { Margin = new Thickness(6) };

        var enabled = new CheckBox
        {
            Content = "Enable graph",
            Foreground = Brushes.Turquoise,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4)
        };
        enabled.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(SliderPicturatorVm.BallGraphEnabled)) { Mode = BindingMode.TwoWay });
        tools.Children.Add(enabled);

        AddButton(tools, "Linear", () =>
        {
            model?.ReplaceBallGraph(BallMotionGraph.Default());
            graph.Selected = 0;
            RefreshFields();
        });

        AddButton(tools, "Steps", () =>
        {
            model?.ReplaceBallGraph(new[]
            {
                new BallGraphPoint(0, 0, BallGraphCurve.Hold),
                new BallGraphPoint(0.16, 0.25, BallGraphCurve.Hold),
                new BallGraphPoint(0.35, 0.5, BallGraphCurve.Hold),
                new BallGraphPoint(0.63, 1.0, BallGraphCurve.Hold),
                new BallGraphPoint(0.79, 0, BallGraphCurve.Hold),
                new BallGraphPoint(1.0, 1.0)
            });
            graph.Selected = 0;
            RefreshFields();
        });

        AddButton(tools, "Add point", () =>
        {
            if (model != null)
            {
                graph.AddPoint(model.GetPreviewProgress(), model.EvaluateBallProgress(model.GetPreviewProgress()));
            }
        });

        AddButton(tools, "Delete point", () => graph.DeleteSelected());

        DockPanel.SetDock(tools, Dock.Top);
        root.Children.Add(tools);

        // Segment density / pacing controls
        var segmentPanel = new StackPanel { Margin = new Thickness(6, 0, 6, 4) };
        var segmentTools = new WrapPanel();
        segmentTools.Children.Add(Label("Minimum tumour length"));

        var density = new Slider
        {
            Name = "GraphMinimumTumourLength",
            Minimum = 1,
            Maximum = 12,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width = 150,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5)
        };
        density.SetBinding(Slider.ValueProperty, new Binding(nameof(SliderPicturatorVm.MinimumTumourLength)) { Mode = BindingMode.TwoWay });
        segmentTools.Children.Add(density);

        var densityValue = Label(string.Empty);
        densityValue.SetBinding(TextBlock.TextProperty, new Binding(nameof(SliderPicturatorVm.MinimumTumourLength)) { StringFormat = "{0:F1}" });
        segmentTools.Children.Add(densityValue);

        AddButton(segmentTools, "Fewer segments", () => { if (model != null) model.MinimumTumourLength = 12; });
        AddButton(segmentTools, "More segments", () => { if (model != null) model.MinimumTumourLength = 1; });

        countSegments.Click += async (_, _) => await CountSegmentsAsync();
        segmentTimer.Tick += async (_, _) =>
        {
            segmentTimer.Stop();
            if (countSegments.IsEnabled) await CountSegmentsAsync();
            else segmentTimer.Start();
        };

        segmentTools.Children.Add(countSegments);
        segmentPanel.Children.Add(segmentTools);
        segmentPanel.Children.Add(segmentStatus);

        var segmentHint = Label("Increase length for fewer segments; decrease for more. Relative off-image pacing length (1–12), not original slider pixels.");
        segmentHint.TextWrapping = TextWrapping.Wrap;
        segmentHint.FontSize = 11;
        segmentPanel.Children.Add(segmentHint);

        DockPanel.SetDock(segmentPanel, Dock.Top);
        root.Children.Add(segmentPanel);

        // Numeric point parameter inputs
        var inputs = new WrapPanel { Margin = new Thickness(6) };
        inputs.Children.Add(Label("Time (ms)"));
        inputs.Children.Add(time);
        inputs.Children.Add(Label("Position (%)"));
        inputs.Children.Add(position);

        curve.ItemsSource = MappingToolsModes;
        inputs.Children.Add(curve);
        AddButton(inputs, "Apply", ApplyFields);

        DockPanel.SetDock(inputs, Dock.Bottom);
        root.Children.Add(inputs);
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        var hint = Label("• Left click: add point | Drag point: edit live\n• Drag the small middle handle to bend the curve (Wave: down adds cycles, up removes cycles)\n• Right click: curve menu | Shift: snap to 1/16");
        hint.TextWrapping = TextWrapping.Wrap;
        hint.FontSize = 11;
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);

        root.Children.Add(graph);
        Content = root;

        graph.SelectionChanged += RefreshFields;
        graph.RequestFocusInput += () => { position.Focus(); position.SelectAll(); };
        time.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyFields(); };
        position.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyFields(); };
        curve.SelectionChanged += (_, _) => { if (!refreshing) ApplyFields(); };

        Loaded += (_, _) => Attach();
        DataContextChanged += (_, _) => Attach();
        Unloaded += (_, _) => { segmentTimer.Stop(); Detach(); };
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = Brushes.LightGray,
        Margin = new Thickness(5),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static void AddButton(Panel parent, string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(3),
            Padding = new Thickness(8, 4, 8, 4)
        };
        button.Click += (_, _) => action();
        parent.Children.Add(button);
    }

    private void Detach()
    {
        if (model != null)
        {
            model.PropertyChanged -= Changed;
            model.PreviewProgressChanged -= Playback;
        }
        model = null;
        graph.Model = null;
    }

    private void Attach()
    {
        Detach();
        model = DataContext as SliderPicturatorVm;
        graph.Model = model;

        if (model != null)
        {
            model.PropertyChanged += Changed;
            model.PreviewProgressChanged += Playback;
        }

        RefreshFields();
        graph.InvalidateVisual();
    }

    private void Playback(double progress, bool seek) => graph.InvalidateVisual();

    private void Changed(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(SliderPicturatorVm.BmImage) 
            and not nameof(SliderPicturatorVm.SegmentCount)
            and not nameof(SliderPicturatorVm.IsProcessingPreview))
        {
            segmentRevision++;
            if (model?.Bm != null)
            {
                segmentTimer.Stop();
                segmentTimer.Start();
            }
            segmentStatus.Text = "Waiting to update total segments…";
        }

        if (e.PropertyName is nameof(SliderPicturatorVm.BallGraphPoints) 
            or nameof(SliderPicturatorVm.Duration) 
            or nameof(SliderPicturatorVm.BallGraphEnabled))
        {
            RefreshFields();
            graph.InvalidateVisual();
        }
    }

    private async System.Threading.Tasks.Task CountSegmentsAsync()
    {
        segmentTimer.Stop();
        if (model?.Bm == null || !countSegments.IsEnabled) return;

        countSegments.IsEnabled = false;
        int revision = segmentRevision;
        var owner = model;

        try
        {
            using var request = BallOnly ? PicturatorExportRequest.CaptureBallOnly(owner) : PicturatorExportRequest.Capture(owner);
            double circleSize = owner.TargetCS;
            segmentStatus.Text = "Calculating slider path…";

            var result = await System.Threading.Tasks.Task.Run(() =>
            {
                if (System.IO.File.Exists(request.Path))
                {
                    circleSize = new StandalonePicturator.Classes.BeatmapHelper.BeatmapEditor(request.Path)
                        .Beatmap.Difficulty["CircleSize"].DoubleValue;
                }
                var path = PicturatorExporter.GeneratePath(request, circleSize);
                return path.Count - 1;
            });

            await Dispatcher.InvokeAsync(() =>
            {
                if (model == owner && revision == segmentRevision)
                {
                    segmentStatus.Text = $"Estimated total segments: {result:N0} • Length {request.MinimumTumourLength:F1}";
                }
                else
                {
                    segmentStatus.Text = "Settings changed — recalculation pending.";
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => segmentStatus.Text = "Cannot calculate segments: " + ex.Message);
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => countSegments.IsEnabled = true);
        }
    }

    private void RefreshFields()
    {
        if (model == null) return;
        refreshing = true;

        graph.Selected = Math.Clamp(graph.Selected, 0, model.BallGraphPoints.Count - 1);
        var point = model.BallGraphPoints[graph.Selected];

        time.Text = (point.Time * model.Duration).ToString("0.###", CultureInfo.CurrentCulture);
        position.Text = (point.Position * 100).ToString("0.###", CultureInfo.CurrentCulture);
        time.IsReadOnly = graph.Selected == 0 || graph.Selected == model.BallGraphPoints.Count - 1;
        curve.SelectedValue = point.Curve;

        status.Text = $"Point {graph.Selected + 1}/{model.BallGraphPoints.Count} • {point.Time * model.Duration:0.###} ms • {point.Position * 100:0.###}%"
            + (point.Curve == BallGraphCurve.Wave ? $" • Cycles: {BallMotionGraph.GetWaveCycles(point.Curvature):F0}" : string.Empty)
            + (model.BallGraphEnabled ? string.Empty : " • (Graph disabled: linear)");

        refreshing = false;
    }

    private static bool Parse(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result)
        || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private void ApplyFields()
    {
        if (model == null || refreshing) return;

        if (!Parse(time.Text, out double ms) || !Parse(position.Text, out double pos)
            || !double.IsFinite(ms) || !double.IsFinite(pos)
            || ms < 0 || ms > model.Duration || pos < 0 || pos > 100)
        {
            status.Text = $"Enter time from 0 to {model.Duration:0} ms and position from 0 to 100%.";
            return;
        }

        var mode = curve.SelectedValue is BallGraphCurve c ? c : BallGraphCurve.Linear;
        model.EditBallGraphPoint(graph.Selected, ms / model.Duration, pos / 100, mode, model.BallGraphPoints[graph.Selected].Curvature);
        model.SetPreviewProgress(model.BallGraphPoints[graph.Selected].Time, true);
    }

    // Canvas element rendering grid, spline interpolation, and interactive handles
    private sealed class GraphSurface : FrameworkElement
    {
        public SliderPicturatorVm Model { get; set; }
        public int Selected { get; set; }
        public event Action SelectionChanged;
        public event Action RequestFocusInput;

        private bool draggingPoint;
        private bool draggingHandle;
        private int dragSegmentIndex = -1;
        private Point dragStartPoint;
        private double dragInitialCurvature;

        private Rect Plot => new(46, 20, Math.Max(1, ActualWidth - 60), Math.Max(1, ActualHeight - 55));
        private Point Screen(double t, double p) => new(Plot.Left + t * Plot.Width, Plot.Bottom - p * Plot.Height);
        private Point Value(Point p) => new(Math.Clamp((p.X - Plot.Left) / Plot.Width, 0, 1), Math.Clamp((Plot.Bottom - p.Y) / Plot.Height, 0, 1));

        public GraphSurface()
        {
            Focusable = true;
            ClipToBounds = true;

            MouseLeftButtonDown += (_, e) =>
            {
                if (Model == null) return;
                Focus();
                var screenPoint = e.GetPosition(this);

                // 1. Primary anchor hit detection
                int hitPt = Hit(screenPoint);
                if (hitPt >= 0)
                {
                    Selected = hitPt;
                    draggingPoint = true;
                    CaptureMouse();
                    SelectionChanged?.Invoke();
                    Model.SetPreviewProgress(Model.BallGraphPoints[hitPt].Time, true);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                // 2. Midpoint tension/curvature handle detection
                int hitHandle = HitHandle(screenPoint);
                if (hitHandle >= 0)
                {
                    Selected = hitHandle;
                    draggingHandle = true;
                    dragSegmentIndex = hitHandle;
                    dragStartPoint = screenPoint;
                    dragInitialCurvature = Model.BallGraphPoints[hitHandle].Curvature;
                    CaptureMouse();
                    SelectionChanged?.Invoke();
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                // 3. Click empty graph area to add a new point
                if (Plot.Contains(screenPoint))
                {
                    var v = Value(screenPoint);
                    AddPoint(SnapTime(v.X), v.Y);
                    draggingPoint = true;
                    CaptureMouse();
                    InvalidateVisual();
                    e.Handled = true;
                }
            };

            MouseMove += (_, e) =>
            {
                if (Model == null) return;
                var screenPoint = e.GetPosition(this);

                // Live primary anchor drag
                if (draggingPoint && e.LeftButton == MouseButtonState.Pressed)
                {
                    var v = Value(screenPoint);
                    double p = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? Math.Round(v.Y * 20) / 20.0 : v.Y;
                    Model.UpdatePointLive(Selected, SnapTime(v.X), p);
                    Model.SetPreviewProgress(Model.BallGraphPoints[Selected].Time, true);
                    InvalidateVisual();
                }
                // Live segment curvature/tension handle drag
                else if (draggingHandle && e.LeftButton == MouseButtonState.Pressed)
                {
                    double dy = screenPoint.Y - dragStartPoint.Y;
                    var segPoint = Model.BallGraphPoints[dragSegmentIndex];

                    double newCurv;
                    if (segPoint.Curve == BallGraphCurve.Wave)
                    {
                        // Wave mode: dragging down increases cycles
                        double delta = dy / (Plot.Height * 0.45);
                        newCurv = Math.Clamp(dragInitialCurvature - delta, -1.0, 1.0);
                    }
                    else
                    {
                        // Standard curves: dragging up increases curvature
                        double delta = -dy / (Plot.Height * 0.45);
                        newCurv = Math.Clamp(dragInitialCurvature + delta, -1.0, 1.0);
                    }

                    Model.UpdateCurvatureLive(dragSegmentIndex, newCurv);
                    InvalidateVisual();
                }
                else
                {
                    if (Hit(screenPoint) >= 0) Cursor = Cursors.Hand;
                    else if (HitHandle(screenPoint) >= 0) Cursor = Cursors.SizeNS;
                    else Cursor = Cursors.Arrow;
                }
            };

            MouseLeftButtonUp += (_, _) =>
            {
                if (draggingPoint || draggingHandle)
                {
                    draggingPoint = false;
                    draggingHandle = false;
                    ReleaseMouseCapture();
                    Model?.CommitGraphChanges();
                    SelectionChanged?.Invoke();
                    InvalidateVisual();
                }
            };

            LostMouseCapture += (_, _) => { draggingPoint = false; draggingHandle = false; };
            KeyDown += (_, e) => { if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; } };
            MouseRightButtonDown += (_, e) => OpenMappingToolsMenu(e.GetPosition(this));
        }

        private double SnapTime(double t) => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            ? Math.Round(t * 16) / 16.0
            : Math.Round(t * Model.Duration) / Math.Max(1.0, Model.Duration);

        private int Hit(Point screen)
        {
            if (Model == null) return -1;
            int best = -1;
            double distance = 100;

            for (int i = 0; i < Model.BallGraphPoints.Count; i++)
            {
                var p = Model.BallGraphPoints[i];
                double d = (Screen(p.Time, p.Position) - screen).LengthSquared;
                if (d <= distance)
                {
                    best = i;
                    distance = d;
                }
            }
            return best;
        }

        private Point GetHandleScreenPos(int segIndex)
        {
            var a = Model.BallGraphPoints[segIndex];
            var b = Model.BallGraphPoints[segIndex + 1];
            double tMid = (a.Time + b.Time) / 2.0;

            double pMid = a.Curve == BallGraphCurve.Wave
                ? Math.Clamp((a.Position + b.Position) / 2.0 + a.Curvature * 0.25, 0.05, 0.95)
                : BallMotionGraph.Interpolate(a, b, 0.5);

            return Screen(tMid, pMid);
        }

        private int HitHandle(Point screen)
        {
            if (Model == null || Model.BallGraphPoints.Count < 2) return -1;
            for (int i = 0; i < Model.BallGraphPoints.Count - 1; i++)
            {
                var hp = GetHandleScreenPos(i);
                if ((hp - screen).LengthSquared <= 100) return i;
            }
            return -1;
        }

        public void AddPoint(double t, double p)
        {
            if (Model == null) return;
            var points = Model.BallGraphPoints.ToList();
            int index = t >= 1.0 ? points.Count - 1 : Math.Max(1, points.FindLastIndex(n => n.Time <= t) + 1);

            points.Insert(index, new BallGraphPoint(t, p, BallGraphCurve.Linear));
            Selected = index;
            Model.ReplaceBallGraph(points);
            SelectionChanged?.Invoke();
            InvalidateVisual();
            Model.SetPreviewProgress(t, true);
        }

        public void DeleteSelected()
        {
            if (Model == null || Selected <= 0 || Selected >= Model.BallGraphPoints.Count - 1) return;
            var points = Model.BallGraphPoints.ToList();
            points.RemoveAt(Selected);
            Selected--;
            Model.ReplaceBallGraph(points);
            SelectionChanged?.Invoke();
            InvalidateVisual();
        }

        private void OpenMappingToolsMenu(Point screen)
        {
            if (Model == null) return;
            int hit = Hit(screen);
            if (hit < 0) hit = HitHandle(screen);
            if (hit < 0)
            {
                double t = Value(screen).X;
                hit = Math.Clamp(Model.BallGraphPoints.ToList().FindLastIndex(p => p.Time <= t), 0, Model.BallGraphPoints.Count - 2);
            }

            Selected = hit;
            SelectionChanged?.Invoke();
            InvalidateVisual();

            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 43)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 70)),
                BorderThickness = new Thickness(1)
            };

            var delete = new MenuItem
            {
                Header = "Delete",
                Foreground = Brushes.White,
                IsEnabled = Selected > 0 && Selected < Model.BallGraphPoints.Count - 1
            };
            delete.Click += (_, _) => DeleteSelected();
            menu.Items.Add(delete);
            menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(55, 55, 65)) });

            var currentCurve = Model.BallGraphPoints[Selected].Curve;
            foreach (var mode in MappingToolsModes)
            {
                bool isCurrent = currentCurve == mode.Key;
                var item = new MenuItem
                {
                    Header = (isCurrent ? "●  " : "○  ") + mode.Value,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isCurrent ? Brushes.Cyan : Brushes.White
                };
                item.Click += (_, _) =>
                {
                    var p = Model.BallGraphPoints[Selected];
                    Model.EditBallGraphPoint(Selected, p.Time, p.Position, mode.Key, 0);
                    SelectionChanged?.Invoke();
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(55, 55, 65)) });

            var typeVal = new MenuItem { Header = "Type in value...", Foreground = Brushes.LightBlue };
            typeVal.Click += (_, _) => RequestFocusInput?.Invoke();
            menu.Items.Add(typeVal);

            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(21, 23, 29)), null, new Rect(RenderSize));
            if (Model == null) return;
            var plot = Plot;

            // 1. Horizontal grid lines (0, 0.25, 0.5, 0.75, 1.0)
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 44, 54)), 1);
            for (int i = 0; i <= 4; i++)
            {
                double p = i / 4.0;
                dc.DrawLine(gridPen, Screen(0, p), Screen(1, p));
                string label = (p == 0 || p == 1) ? $"{p:0}" : $"{p:0.##}";
                Text(dc, label, new Point(14, Screen(0, p).Y - 8), Brushes.Gray);
            }

            // 2. osu! beat snap divisor ticks on the X axis
            var tickPenWhite = new Pen(Brushes.White, 1.5);
            var tickPenRed = new Pen(new SolidColorBrush(Color.FromRgb(235, 75, 75)), 1);
            var tickPenBlue = new Pen(new SolidColorBrush(Color.FromRgb(60, 140, 255)), 1);

            for (int i = 0; i <= 16; i++)
            {
                double t = i / 16.0;
                var ptBottom = Screen(t, 0);
                var pen = (i % 4 == 0) ? tickPenWhite : (i % 2 == 0) ? tickPenRed : tickPenBlue;
                double tickHeight = (i % 4 == 0) ? 9 : 5;
                dc.DrawLine(pen, ptBottom, new Point(ptBottom.X, ptBottom.Y + tickHeight));
            }

            // 3. Interpolated curve generation & fill geometry
            var points = Model.BallGraphPoints;
            var line = new List<Point> { Screen(points[0].Time, points[0].Position) };

            for (int i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];

                if (a.Curve == BallGraphCurve.Hold)
                {
                    line.Add(Screen(b.Time, a.Position));
                }
                else if (b.Time > a.Time)
                {
                    int steps = (a.Curve == BallGraphCurve.Wave) ? 180 : 64;
                    for (int j = 1; j <= steps; j++)
                    {
                        double u = (double)j / steps;
                        line.Add(Screen(a.Time + (b.Time - a.Time) * u, BallMotionGraph.Interpolate(a, b, u)));
                    }
                }
                line.Add(Screen(b.Time, b.Position));
            }

            var fill = new StreamGeometry();
            using (var c = fill.Open())
            {
                c.BeginFigure(Screen(0, 0), true, true);
                c.PolyLineTo(line, true, false);
                c.LineTo(Screen(1, 0), true, false);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(35, 0, 245, 220)), null, fill);

            var curvePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 235, 215)), 2);
            for (int i = 1; i < line.Count; i++)
            {
                dc.DrawLine(curvePen, line[i - 1], line[i]);
            }

            // 4. Live playback tracker
            double currentT = Model.GetPreviewProgress();
            dc.DrawLine(new Pen(Brushes.Gold, 1.2), Screen(currentT, 0), Screen(currentT, 1));
            dc.DrawEllipse(Brushes.Gold, null, Screen(currentT, Model.EvaluateBallProgress(currentT)), 4, 4);

            // 5. Tension/curvature segment handles
            var handleFill = new SolidColorBrush(Color.FromRgb(0, 235, 215));
            var handleStroke = new Pen(Brushes.White, 1.2);
            for (int i = 0; i < points.Count - 1; i++)
            {
                var hp = GetHandleScreenPos(i);
                dc.DrawEllipse(handleFill, handleStroke, hp, 3.5, 3.5);
            }

            // 6. Primary anchor points
            for (int i = 0; i < points.Count; i++)
            {
                var ptPos = Screen(points[i].Time, points[i].Position);
                bool isSel = (i == Selected);
                Brush fillBrush = isSel ? Brushes.Gold : new SolidColorBrush(Color.FromRgb(25, 30, 36));
                Pen strokePen = new Pen(isSel ? Brushes.White : new SolidColorBrush(Color.FromRgb(0, 240, 220)), 2);
                dc.DrawEllipse(fillBrush, strokePen, ptPos, 5.5, 5.5);
            }
        }

        private void Text(DrawingContext dc, string text, Point p, Brush brush) =>
            dc.DrawText(new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip),
                p);
    }
}
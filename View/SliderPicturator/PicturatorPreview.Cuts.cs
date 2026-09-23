using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StandalonePicturator.Classes;

namespace StandalonePicturator.View.SliderPicturator;

public sealed partial class PicturatorPreview
{
    private sealed partial class PreviewSurface
    {
        private bool cutArmed;
        private string cutShape = "Rectangle";
        private Point? cutStart;
        private readonly List<Point> cutGuide = new();

        // Sets up interactive in-place shape cutting and context menus on the preview canvas
        private void InitializeCuts()
        {
            // 1. Context menu on right-clicking an active layer
            MouseRightButtonDown += (_, e) =>
            {
                if (BallOnly || Model?.Bm == null || Model.ActiveLibraryItem?.IsVisible == false
                    || !Model.PreviewImageBounds.Contains(ToWorld(e.GetPosition(this))))
                {
                    return;
                }

                Focus();
                var menu = new ContextMenu();

                void AddMenuItem(string text, Action action)
                {
                    var item = new MenuItem { Header = text };
                    item.Click += (_, _) => action();
                    menu.Items.Add(item);
                }

                AddMenuItem("Cut…", ShowCutDialog);
                menu.Items.Add(new Separator());
                AddMenuItem("Undo cut     Ctrl+Z", () => Model.UndoShapeCut());
                AddMenuItem("Clear all cuts", () => Model.ClearShapeCuts());
                AddMenuItem("Cancel selection     Esc", CancelCut);

                menu.PlacementTarget = this;
                menu.IsOpen = true;
                e.Handled = true;
            };

            // 2. Click-to-start drawing cutting shape
            PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!cutArmed || Model?.Bm == null) return;

                Focus();
                cutStart = ToWorld(e.GetPosition(this));
                cutGuide.Clear();
                UpdateSelection(cutStart.Value);
                CaptureMouse();
                e.Handled = true;
            };

            // 3. Drag-to-expand cutting polygon
            PreviewMouseMove += (_, e) =>
            {
                if (cutStart == null || e.LeftButton != MouseButtonState.Pressed) return;

                UpdateSelection(ToWorld(e.GetPosition(this)));
                e.Handled = true;
            };

            // 4. Release to commit cutout shape
            PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (cutStart == null) return;

                UpdateSelection(ToWorld(e.GetPosition(this)));
                var bounds = Model.PreviewImageBounds;
                var polygon = new List<Point>();

                // Normalize cutout polygon vertices relative to the active layer bounds [0, 1]
                foreach (var p in cutGuide)
                {
                    polygon.Add(new Point((p.X - bounds.X) / bounds.Width, (p.Y - bounds.Y) / bounds.Height));
                }

                // Shoelace formula to verify non-negligible polygon area
                double area = 0;
                for (int i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    area += a.X * b.Y - b.X * a.Y;
                }

                cutStart = null;
                cutGuide.Clear();
                ReleaseMouseCapture();

                if (polygon.Count >= 3 && Math.Abs(area) * bounds.Width * bounds.Height > 2)
                {
                    Model.AddShapeCut(new ShapeCut { Kind = "Polygon", Points = polygon });
                }

                e.Handled = true;
                InvalidateVisual();
            };

            // 5. Hotkey cancellations and undos
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CancelCut();
                    e.Handled = true;
                }
                else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    if (cutStart != null)
                    {
                        cutStart = null;
                        cutGuide.Clear();
                        ReleaseMouseCapture();
                        InvalidateVisual();
                    }
                    else
                    {
                        Model?.UndoShapeCut();
                    }

                    e.Handled = true;
                }
            };

            LostMouseCapture += (_, _) =>
            {
                if (cutStart != null)
                {
                    cutStart = null;
                    cutGuide.Clear();
                    InvalidateVisual();
                }
            };

            DataContextChanged += (_, _) => CancelCut();
        }

        // Recomputes polygon vertices matching the chosen shape generator
        private void UpdateSelection(Point end)
        {
            if (cutShape == "Freehand")
            {
                if (cutGuide.Count == 0 || (cutGuide[^1] - end).Length > 0.5)
                {
                    cutGuide.Add(end);
                }
            }
            else
            {
                cutGuide.Clear();
                cutGuide.AddRange(CutShapes.Create(cutShape, cutStart.Value, end));
            }

            InvalidateVisual();
        }

        private void CancelCut()
        {
            cutArmed = false;
            cutStart = null;
            cutGuide.Clear();
            ReleaseMouseCapture();
            Cursor = Cursors.SizeAll;
            InvalidateVisual();
        }

        public void CancelActiveCut() => CancelCut();

        // Renders dashed guide overlays and hotkey helper hints while cut selection is active
        private void DrawCutGuide(DrawingContext dc)
        {
            if (!cutArmed) return;

            DrawText(dc, $"{cutShape} cut: drag and release to erase inside • Ctrl+Z undo • Esc cancel",
                new Point(8, ActualHeight - 66), Brushes.OrangeRed);

            if (cutGuide.Count < 2) return;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(ToScreen(cutGuide[0]), true, true);
                for (int i = 1; i < cutGuide.Count; i++)
                {
                    ctx.LineTo(ToScreen(cutGuide[i]), true, false);
                }
            }

            dc.DrawGeometry(
                new SolidColorBrush(Color.FromArgb(65, 255, 80, 70)),
                new Pen(Brushes.OrangeRed, 1.5) { DashStyle = DashStyles.Dash },
                geometry);
        }

        // Opens the full vector CutEditorWindow dialog modal
        private void ShowCutDialog()
        {
            CancelCut();
            var editor = new CutEditorWindow(Model.BmImage) { Owner = Window.GetWindow(this) };
            if (editor.ShowDialog() != true) return;

            foreach (var cut in editor.Cuts)
            {
                Model.AddShapeCut(cut);
            }

            Focus();
        }
    }
}
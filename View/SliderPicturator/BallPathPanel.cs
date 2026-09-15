using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.SliderPicturator;

public sealed class BallPathPanel : Border
{
    public BallPathPanel()
    {
        Name = "BallPathPositionPanel";
        Width = 240; Padding = new Thickness(8); CornerRadius = new CornerRadius(5);
        Background = new SolidColorBrush(Color.FromArgb(240, 27, 32, 40));
        BorderBrush = Brushes.Gold; BorderThickness = new Thickness(1);
        SetBinding(VisibilityProperty, new Binding(nameof(SliderPicturatorVm.HasSliderBall)) { Converter = new BooleanToVisibilityConverter() });
        var content = new StackPanel();
        var header = new Grid { Height = 27, Background = Brushes.DarkGoldenrod };
        header.Children.Add(new TextBlock { Text = "Sliderball path • drag panel", Foreground = Brushes.White, Margin = new Thickness(5) });
        var thumb = new Thumb { Opacity = 0, Cursor = System.Windows.Input.Cursors.SizeAll };
        thumb.DragDelta += (_, e) => {
            if (Parent is not Canvas canvas) return;
            Canvas.SetLeft(this, Math.Clamp(Canvas.GetLeft(this) + e.HorizontalChange, 0, Math.Max(0, canvas.ActualWidth - ActualWidth)));
            Canvas.SetTop(this, Math.Clamp(Canvas.GetTop(this) + e.VerticalChange, 0, Math.Max(0, canvas.ActualHeight - ActualHeight)));
        };
        header.Children.Add(thumb); content.Children.Add(header);
        void AddInput(string label, string property) {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
            row.Children.Add(new TextBlock { Text = label, Width = 105, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            var input = new TextBox { Name = property + "Input", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(42, 43, 48)) };
            input.SetBinding(TextBox.TextProperty, new Binding(property) { Mode = BindingMode.TwoWay, StringFormat = "F2", ValidatesOnExceptions = true });
            input.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource(); };
            row.Children.Add(input); content.Children.Add(row);
        }
        AddInput("X offset (osu!px)", nameof(SliderPicturatorVm.BallOffsetX));
        AddInput("Y offset (osu!px)", nameof(SliderPicturatorVm.BallOffsetY));
        AddInput("Ball path scale", nameof(SliderPicturatorVm.BallPathScale));
        var reset = new Button { Content = "Reset path alignment", Margin = new Thickness(0, 5, 0, 0) };
        reset.Click += (_, _) => (DataContext as SliderPicturatorVm)?.ResetBallPlacement();
        content.Children.Add(reset);
        content.Children.Add(new TextBlock { Text = "Drag the ball or the yellow frame edge to move the path.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, FontSize = 11 });
        Child = content;
    }
}

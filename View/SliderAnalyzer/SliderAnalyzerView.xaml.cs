using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.SliderAnalyzer
{
    public partial class SliderAnalyzerView : UserControl
    {
        private Point _playfieldStartPoint;
        private bool _isPlayfieldDragging;

        private Point _macroStartPoint;
        private bool _isMacroDragging;

        public SliderAnalyzerView()
        {
            InitializeComponent();
            DataContext = new SliderAnalyzerVm();
        }

        // --- Sự kiện cho PLAYFIELD VIEW ---
        private void Playfield_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ApplyZoom(PlayfieldScale, e.Delta > 0 ? 1.15 : 0.85);
            e.Handled = true;
        }

        private void Playfield_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.CaptureMouse();
                _isPlayfieldDragging = true;
                _playfieldStartPoint = e.GetPosition(element);
            }
        }

        private void Playfield_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.ReleaseMouseCapture();
                _isPlayfieldDragging = false;
            }
        }

        private void Playfield_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPlayfieldDragging && sender is UIElement element)
            {
                Point currentPoint = e.GetPosition(element);
                PlayfieldTranslate.X += currentPoint.X - _playfieldStartPoint.X;
                PlayfieldTranslate.Y += currentPoint.Y - _playfieldStartPoint.Y;
                _playfieldStartPoint = currentPoint;
            }
        }

        private void ResetPlayfieldZoom_Click(object sender, RoutedEventArgs e)
        {
            PlayfieldScale.ScaleX = 1; PlayfieldScale.ScaleY = 1;
            PlayfieldTranslate.X = 0; PlayfieldTranslate.Y = 0;
        }

        // --- Sự kiện cho MACRO VIEW ---
        private void Macro_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ApplyZoom(MacroScale, e.Delta > 0 ? 1.15 : 0.85);
            e.Handled = true;
        }

        private void Macro_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.CaptureMouse();
                _isMacroDragging = true;
                _macroStartPoint = e.GetPosition(element);
            }
        }

        private void Macro_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.ReleaseMouseCapture();
                _isMacroDragging = false;
            }
        }

        private void Macro_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMacroDragging && sender is UIElement element)
            {
                Point currentPoint = e.GetPosition(element);
                MacroTranslate.X += currentPoint.X - _macroStartPoint.X;
                MacroTranslate.Y += currentPoint.Y - _macroStartPoint.Y;
                _macroStartPoint = currentPoint;
            }
        }

        private void ResetMacroZoom_Click(object sender, RoutedEventArgs e)
        {
            MacroScale.ScaleX = 1; MacroScale.ScaleY = 1;
            MacroTranslate.X = 0; MacroTranslate.Y = 0;
        }

        // --- Hàm hỗ trợ Zoom chung ---
        private void ApplyZoom(ScaleTransform scaleTransform, double factor)
        {
            double newScaleX = scaleTransform.ScaleX * factor;
            double newScaleY = scaleTransform.ScaleY * factor;
            if (newScaleX >= 0.2 && newScaleX <= 15.0)
            {
                scaleTransform.ScaleX = newScaleX;
                scaleTransform.ScaleY = newScaleY;
            }
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.HollowSlider
{
    public partial class HollowSliderView : UserControl
    {
        public HollowSliderView()
        {
            InitializeComponent();
            DataContext = new HollowSliderVm();
        }

        public HollowSliderVm ViewModel => (HollowSliderVm)DataContext;

        private void Inject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ViewModel.BeatmapPath) || !File.Exists(ViewModel.BeatmapPath))
            {
                MessageBox.Show("Choose a .osu file first.", "Notice");
                return;
            }

            try
            {
                var editor = new BeatmapEditor(ViewModel.BeatmapPath);
                var beatmap = editor.Beatmap;

                // Xóa override màu để giữ trọn vẹn skin native
                beatmap.SpecialColours.Remove("SliderTrackOverride");
                beatmap.SpecialColours.Remove("SliderBorder");

                int sides = ViewModel.SelectedShapeIndex switch {
                    1 => 4,  // Square
                    2 => 3,  // Triangle
                    3 => 6,  // Hexagon
                    _ => 36  // Donut
                };

                double time = ViewModel.TimeCode;
                double duration = ViewModel.Duration > 0 ? ViewModel.Duration : 1000;

                // Xóa slider cũ cùng mốc thời gian
                beatmap.HitObjects.RemoveAll(h => Math.Abs(h.Time - time) < 5);

                // 1. Tạo đối tượng Hollow Slider
                var ho = Classes.Tools.NegativeSpaceSlider.GenerateHollowPolygon(
                    new Vector2(ViewModel.CenterX, ViewModel.CenterY),
                    ViewModel.OuterRadius,
                    ViewModel.InnerRadius,
                    sides,
                    time,
                    ViewModel.RotationAngle
                );

                // =========================================================================
                // CƠ CHẾ TIMING CHUẨN OLIBOMBY (REDLINE T-1 + GREENLINE NAN)
                // =========================================================================
                var timing = beatmap.BeatmapTiming;
                var tpAfter = timing.GetRedlineAtTime(ho.Time).Copy();
                var tpOn = tpAfter.Copy();
                tpAfter.Offset = ho.Time;
                tpOn.Offset = ho.Time - 1;
                tpAfter.OmitFirstBarLine = true;
                tpOn.OmitFirstBarLine = true;

                double sliderMultiplier = beatmap.Difficulty.ContainsKey("SliderMultiplier")
                    ? beatmap.Difficulty["SliderMultiplier"].DoubleValue
                    : 1.4;

                tpOn.MpB = 100.0 * sliderMultiplier * duration / ho.PixelLength;
                ho.SliderVelocity = double.NaN;
                ho.Time -= 1;

                double prevSv = timing.GetSvAtTime(time);

                var oldTps = timing.TimingPoints
                    .Where(tp => Math.Abs(tp.Offset - ho.Time) < 0.5 || Math.Abs(tp.Offset - time) < 0.5)
                    .ToList();
                foreach (var tp in oldTps) timing.Remove(tp);

                timing.Add(tpOn);

                var svTp = tpOn.Copy();
                svTp.Uninherited = false;
                svTp.MpB = double.NaN;
                svTp.Offset = ho.Time;
                timing.Add(svTp);

                timing.Add(tpAfter);

                var restoreSvTp = tpAfter.Copy();
                restoreSvTp.Uninherited = false;
                restoreSvTp.MpB = prevSv;
                restoreSvTp.Offset = time;
                timing.Add(restoreSvTp);

                timing.Sort();

                beatmap.HitObjects.Add(ho);
                beatmap.SortHitObjects();
                editor.SaveFile();

                MessageBox.Show($"Hollow slider injected at {time}ms.\nPress Ctrl+L in osu! editor to view it.", "Thành công");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error");
            }
        }
    }
}
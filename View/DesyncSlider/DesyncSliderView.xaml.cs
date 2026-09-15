using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.View.DesyncSlider
{
    public partial class DesyncSliderView : UserControl
    {
        public DesyncSliderView()
        {
            InitializeComponent();
            DataContext = new DesyncSliderVm();
        }

        public DesyncSliderVm ViewModel => (DesyncSliderVm)DataContext;

        private void Inject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ViewModel.BeatmapPath) || !File.Exists(ViewModel.BeatmapPath))
            {
                MessageBox.Show("Choose a .osu file first.", "Notice");
                return;
            }

            if (ViewModel.VisualSlider == null)
            {
                MessageBox.Show("No slider loaded. Select it in osu! and press Ctrl+C.", "Notice");
                return;
            }

            try
            {
                var editor = new BeatmapEditor(ViewModel.BeatmapPath);
                var beatmap = editor.Beatmap;

                double time = ViewModel.TimeCode;
                double duration = ViewModel.Duration > 0 ? ViewModel.Duration : 1000;

                // Xóa slider cũ tại mốc này để tránh đè nốt
                beatmap.HitObjects.RemoveAll(h => Math.Abs(h.Time - time) < 5);

                Vector2 targetPoint;
                if (ViewModel.DesyncMode == 0) // Stationary Point
                {
                    targetPoint = new Vector2(ViewModel.StationaryX, ViewModel.StationaryY);
                }
                else // Parallel Offset
                {
                    var pts = ViewModel.VisualSlider.GetAllCurvePoints();
                    targetPoint = pts.Last() + new Vector2(ViewModel.OffsetX, ViewModel.OffsetY);
                }

                // TẠO DUY NHẤT 1 SLIDER (SINGLE QUANTUM SLIDER)
                var singleQuantumHo = Classes.Tools.DesyncSlider.CreateSingleQuantumDesync(
                    ViewModel.VisualSlider,
                    targetPoint,
                    time,
                    duration
                );

                // THIẾT LẬP TIMING ĐỂ KHÓA HITBOX TẠI VỊ TRÍ DESYNC MÀ KHÔNG GIẬT LAG
                var timing = beatmap.BeatmapTiming;
                var tpAfter = timing.GetRedlineAtTime(singleQuantumHo.Time).Copy();
                var tpOn = tpAfter.Copy();
                tpAfter.Offset = singleQuantumHo.Time;
                tpOn.Offset = singleQuantumHo.Time - 1;
                tpAfter.OmitFirstBarLine = true;
                tpOn.OmitFirstBarLine = true;

                double sliderMultiplier = beatmap.Difficulty.ContainsKey("SliderMultiplier")
                    ? beatmap.Difficulty["SliderMultiplier"].DoubleValue
                    : 1.4;

                // Điều tốc để toàn bộ thời lượng trải dài trên khoảng ngoại suy
                tpOn.MpB = 100.0 * sliderMultiplier * duration / singleQuantumHo.PixelLength;
                singleQuantumHo.SliderVelocity = double.NaN; // Xóa tick chống tụt FPS
                singleQuantumHo.Time -= 1;

                double prevSv = timing.GetSvAtTime(time);

                // Dọn dẹp timing points cũ xung quanh mốc inject
                var oldTps = timing.TimingPoints
                    .Where(tp => Math.Abs(tp.Offset - singleQuantumHo.Time) < 0.5 || Math.Abs(tp.Offset - time) < 0.5)
                    .ToList();
                foreach (var tp in oldTps) timing.Remove(tp);

                timing.Add(tpOn);

                var svTp = tpOn.Copy();
                svTp.Uninherited = false;
                svTp.MpB = double.NaN;
                svTp.Offset = singleQuantumHo.Time;
                timing.Add(svTp);

                timing.Add(tpAfter);

                var restoreSvTp = tpAfter.Copy();
                restoreSvTp.Uninherited = false;
                restoreSvTp.MpB = prevSv;
                restoreSvTp.Offset = time;
                timing.Add(restoreSvTp);

                timing.Sort();

                // CHỈ THÊM DUY NHẤT 1 HITOBJECT VÀO BEATMAP
                beatmap.HitObjects.Add(singleQuantumHo);
                beatmap.SortHitObjects();
                editor.SaveFile();

                MessageBox.Show($"Single Quantum Slider created at {time}ms!\n• Actual PixelLength: {singleQuantumHo.PixelLength:F1}px\n• Timeline: one object.", "Thành công");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error");
            }
        }
    }
}
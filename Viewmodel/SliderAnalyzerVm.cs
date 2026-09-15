using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;
using StandalonePicturator.Classes.Tools.SlideratorStuff;
using HitObject = StandalonePicturator.Classes.BeatmapHelper.HitObject;

namespace StandalonePicturator.Viewmodel
{
    public class SliderItemModel
    {
        public HitObject HitObject { get; set; }
        public double Time => HitObject.Time;
        public string FormattedTime => TimeSpan.FromMilliseconds(HitObject.Time).ToString(@"mm\:ss\.fff");
        public string DisplayText => $"[{FormattedTime}] ({HitObject.Time}ms) - {HitObject.SliderType} - {HitObject.PixelLength:F0}px";
        public override string ToString() => DisplayText;
    }

    public class SliderAnalyzerVm : BindableBase
    {
        private CancellationTokenSource _renderCts;
        private readonly object _renderLock = new();

        private string osuPath;
        public string OsuPath
        {
            get => osuPath;
            set { if (Set(ref osuPath, value)) LoadSlidersFromBeatmap(); }
        }

        private string analysisResult;
        public string AnalysisResult { get => analysisResult; set => Set(ref analysisResult, value); }

        private InteropBitmap playfieldBitmap;
        public InteropBitmap PlayfieldBitmap { get => playfieldBitmap; set => Set(ref playfieldBitmap, value); }

        private InteropBitmap macroBitmap;
        public InteropBitmap MacroBitmap { get => macroBitmap; set => Set(ref macroBitmap, value); }

        public ObservableCollection<SliderItemModel> AllSliders { get; } = new();

        private SliderItemModel selectedSliderItem;
        public SliderItemModel SelectedSliderItem
        {
            get => selectedSliderItem;
            set { if (Set(ref selectedSliderItem, value) && value != null) TriggerAsyncAnalysis(value.HitObject); }
        }

        public ObservableCollection<string> ControlPointsList { get; } = new();
        public HitObject AnalyzedSlider { get; private set; }
        private Bitmap generatedSliderMask;

        public CommandImplementation BrowseOsuCommand { get; }
        public CommandImplementation GenerateSimilarCommand { get; }

        public SliderAnalyzerVm()
        {
            AnalysisResult = "No slider selected. Click 'Browse .osu' to open a map.";
            BrowseOsuCommand = new CommandImplementation(_ => SelectOsuFile());
            GenerateSimilarCommand = new CommandImplementation(_ => ApplyPicturatorToAnalyzedSlider());
        }

        private void SelectOsuFile()
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "osu! Beatmap (*.osu)|*.osu" };
            if (ofd.ShowDialog() == true) OsuPath = ofd.FileName;
        }

        public void LoadSlidersFromBeatmap()
        {
            if (string.IsNullOrEmpty(OsuPath) || !File.Exists(OsuPath)) return;
            try
            {
                var editor = new BeatmapEditor(OsuPath);
                var sliders = editor.Beatmap.HitObjects.Where(h => h.IsSlider).OrderBy(s => s.Time).ToList();

                AllSliders.Clear();
                foreach (var slider in sliders) AllSliders.Add(new SliderItemModel { HitObject = slider });

                if (AllSliders.Count > 0) SelectedSliderItem = AllSliders.First();
                else
                {
                    AnalysisResult = "This beatmap contains no sliders.";
                    ControlPointsList.Clear();
                    PlayfieldBitmap = null;
                    MacroBitmap = null;
                }
            }
            catch (Exception ex) { MessageBox.Show($"File read error: {ex.Message}", "Error"); }
        }

        private void TriggerAsyncAnalysis(HitObject slider)
        {
            lock (_renderLock)
            {
                _renderCts?.Cancel();
                _renderCts?.Dispose();
                _renderCts = new CancellationTokenSource();
            }

            var token = _renderCts.Token;
            AnalyzedSlider = slider;

            // Nạp danh sách điểm neo lên UI
            ControlPointsList.Clear();
            var points = AnalyzedSlider.GetAllCurvePoints();
            int totalPoints = points.Count;

            // Chỉ load tối đa 5000 điểm lên UI ListBox để tránh đơ UI của WPF
            int displayLimit = Math.Min(totalPoints, 5000);
            for (int i = 0; i < displayLimit; i++)
            {
                ControlPointsList.Add($"Point [{i}]:  X = {points[i].X,8:F1}   Y = {points[i].Y,8:F1}");
            }
            if (totalPoints > 5000) ControlPointsList.Add($"... {totalPoints - 5000} more points hidden for performance ...");

            AnalysisResult = $"[SLIDER PROPERTIES]\n" +
                             $"• Timestamp: {TimeSpan.FromMilliseconds(slider.Time):mm\\:ss\\.fff} ({slider.Time} ms)\n" +
                             $"• Curve type: {slider.SliderType}\n" +
                             $"• PixelLength: {slider.PixelLength:F2} px\n" +
                             $"• Start position: ({slider.Pos.X:F1}, {slider.Pos.Y:F1})\n" +
                              $"• Anchor count: {totalPoints:N0} points\n" +
                             $"• Velocity (SV): {slider.SliderVelocity:F2}x";

            // Xử lý đồ họa đa luồng
            Task.Run(() =>
            {
                try
                {
                    var editor = new BeatmapEditor(OsuPath);
                    double cs = editor.Beatmap.Difficulty["CircleSize"].DoubleValue;

                    // LẤY DỮ LIỆU TỪ LÕI OLIBOMBY (Giữ nguyên gốc 100%)
                    List<Vector2> rawCalculatedPath = new List<Vector2>();
                    try { rawCalculatedPath = slider.GetSliderPath().CalculatedPath.ToList(); } catch { }
                    if (rawCalculatedPath == null || rawCalculatedPath.Count < 2) rawCalculatedPath = slider.GetAllCurvePoints();

                    var playBmp = GeneratePlayfieldBitmap(rawCalculatedPath, cs, token);
                    var macroBmp = GenerateMacroBitmap(points, token); // Truyền Control Points (điểm neo) vào Macro

                    if (token.IsCancellationRequested) return;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        UpdateBitmaps(playBmp, macroBmp);
                    });
                }
                catch { }
            }, token);
        }

        // --- KHUNG TRÁI: GÓC NHÌN NGƯỜI CHƠI (Playfield) ---
        // Giới hạn trong khung 512x384, cắt bỏ các tọa độ quá khổng lồ để GDI+ không crash
        private Bitmap GeneratePlayfieldBitmap(List<Vector2> rawPath, double cs, CancellationToken token)
        {
            int width = 512, height = 384;
            Bitmap bmp = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.FromArgb(15, 15, 15));

                using (Pen gridPen = new Pen(System.Drawing.Color.FromArgb(40, 40, 40), 1))
                {
                    for (int x = 0; x < width; x += 64) g.DrawLine(gridPen, x, 0, x, height);
                    for (int y = 0; y < height; y += 64) g.DrawLine(gridPen, 0, y, width, y);
                }

                if (rawPath != null && rawPath.Count > 1)
                {
                    // TỐI ƯU HÓA 1: DOWNSAMPLING (Bỏ bớt điểm nếu quá dày đặc)
                    int step = Math.Max(1, rawPath.Count / 4000); 
                    var safePoints = new List<PointF>();

                    for (int i = 0; i < rawPath.Count; i += step)
                    {
                        var p = rawPath[i];
                        if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.X)) continue;

                        // TỐI ƯU HÓA 2: CLAMPING (Chặn đứng tràn bộ nhớ GDI+)
                        float clampedX = (float)Math.Clamp(p.X, -3000, 3000);
                        float clampedY = (float)Math.Clamp(p.Y, -3000, 3000);
                        safePoints.Add(new PointF(clampedX, clampedY));
                    }

                    if (safePoints.Count > 1)
                    {
                        float radius = (float)Beatmap.GetHitObjectRadius(cs);
                        PointF[] pts = safePoints.ToArray();

                        using (Pen borderPen = new Pen(System.Drawing.Color.FromArgb(81, 43, 212), radius * 2 + 8))
                        {
                            borderPen.StartCap = LineCap.Round; borderPen.EndCap = LineCap.Round; borderPen.LineJoin = LineJoin.Round;
                            g.DrawLines(borderPen, pts);
                        }
                        using (Pen bodyPen = new Pen(System.Drawing.Color.White, radius * 2))
                        {
                            bodyPen.StartCap = LineCap.Round; bodyPen.EndCap = LineCap.Round; bodyPen.LineJoin = LineJoin.Round;
                            g.DrawLines(bodyPen, pts);
                        }
                    }
                }
            }
            generatedSliderMask = (Bitmap)bmp.Clone();
            return bmp;
        }

        // --- KHUNG PHẢI: GÓC NHÌN ĐẤNG SÁNG TẠO (Macro Space) ---
        // Thuật toán co giãn không gian (Coordinate Scaling) giống y hệt bản Python
        private Bitmap GenerateMacroBitmap(List<Vector2> controlPoints, CancellationToken token)
        {
            int bmpSize = 800; // Khung vuông 800x800
            Bitmap bmp = new Bitmap(bmpSize, bmpSize);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.FromArgb(20, 25, 30));

                if (controlPoints == null || controlPoints.Count == 0) return bmp;

                // 1. TÌM VÙNG KHÔNG GIAN (BOUNDING BOX) BAO TRÙM TẤT CẢ TỌA ĐỘ
                double minX = 0, minY = 0, maxX = 512, maxY = 384; 
                foreach (var pt in controlPoints)
                {
                    if (double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X)) continue;
                    minX = Math.Min(minX, pt.X);
                    maxX = Math.Max(maxX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxY = Math.Max(maxY, pt.Y);
                }

                // Thêm Padding để không sát lề
                double padX = Math.Max((maxX - minX) * 0.1, 50);
                double padY = Math.Max((maxY - minY) * 0.1, 50);
                minX -= padX; maxX += padX;
                minY -= padY; maxY += padY;

                // 2. TÍNH TỶ LỆ THU NHỎ (SCALING FACTOR)
                double scaleX = bmpSize / (maxX - minX);
                double scaleY = bmpSize / (maxY - minY);
                double scale = Math.Min(scaleX, scaleY);

                double offsetX = (bmpSize - (maxX - minX) * scale) / 2.0;
                double offsetY = (bmpSize - (maxY - minY) * scale) / 2.0;

                // Hàm nội bộ: Chuyển đổi tọa độ osu! thành tọa độ trên ảnh 800x800
                PointF ToMacro(double x, double y)
                {
                    float mx = (float)((x - minX) * scale + offsetX);
                    float my = (float)((y - minY) * scale + offsetY);
                    return new PointF(mx, my);
                }

                // 3. Vẽ ô Playfield 512x384 (Vùng hiển thị thực tế trong game)
                PointF pfTL = ToMacro(0, 0);
                PointF pfBR = ToMacro(512, 384);
                
                // Đảm bảo ô Playfield luôn được nhìn thấy (độ dày tối thiểu 1px)
                float pfWidth = Math.Max(1f, pfBR.X - pfTL.X);
                float pfHeight = Math.Max(1f, pfBR.Y - pfTL.Y);
                
                using (SolidBrush pfBrush = new SolidBrush(System.Drawing.Color.FromArgb(40, 0, 255, 100)))
                {
                    g.FillRectangle(pfBrush, pfTL.X, pfTL.Y, pfWidth, pfHeight);
                }
                using (Pen pfPen = new Pen(System.Drawing.Color.LimeGreen, 1.5f))
                {
                    g.DrawRectangle(pfPen, pfTL.X, pfTL.Y, pfWidth, pfHeight);
                }

                // 4. Vẽ đường dây nhện các Control Points (Giảm tải điểm nếu > 3000 điểm)
                int step = Math.Max(1, controlPoints.Count / 3000);
                List<PointF> macroPts = new List<PointF>();
                
                for (int i = 0; i < controlPoints.Count; i += step)
                {
                    var p = controlPoints[i];
                    if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.X)) continue;
                    macroPts.Add(ToMacro(p.X, p.Y));
                }

                if (macroPts.Count > 1)
                {
                    using (Pen wirePen = new Pen(System.Drawing.Color.FromArgb(180, 0, 200, 255), 1.5f))
                    {
                        g.DrawLines(wirePen, macroPts.ToArray());
                    }
                }

                // Vẽ các điểm chấm tròn
                foreach (var pt in macroPts)
                {
                    g.FillEllipse(System.Drawing.Brushes.Orange, pt.X - 2.5f, pt.Y - 2.5f, 5, 5);
                }
            }
            return bmp;
        }

        private void UpdateBitmaps(Bitmap playBmp, Bitmap macroBmp)
        {
            IntPtr hPlay = playBmp.GetHbitmap();
            IntPtr hMacro = macroBmp.GetHbitmap();

            try
            {
                PlayfieldBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromHBitmap(
                    hPlay, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                MacroBitmap = (InteropBitmap)Imaging.CreateBitmapSourceFromHBitmap(
                    hMacro, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                RaisePropertyChanged(nameof(PlayfieldBitmap));
                RaisePropertyChanged(nameof(MacroBitmap));
            }
            finally
            {
                SliderPicturatorVm.DeleteObject(hPlay);
                SliderPicturatorVm.DeleteObject(hMacro);
                playBmp?.Dispose();
                macroBmp?.Dispose();
            }
        }

        private void ApplyPicturatorToAnalyzedSlider()
        {
            if (AnalyzedSlider == null || generatedSliderMask == null)
            {
                MessageBox.Show("Select a slider from the list first.", "Error");
                return;
            }
            try
            {
                var editor = new BeatmapEditor(OsuPath);
                var beatmap = editor.Beatmap;

                double circleSize = beatmap.Difficulty["CircleSize"].DoubleValue;
                System.Drawing.Color sliderColor = System.Drawing.Color.White;
                System.Drawing.Color borderColor = System.Drawing.Color.FromArgb(81, 43, 212);
                System.Drawing.Color backgroundColor = System.Drawing.Color.Black;

                // TÁI TẠO BẰNG THUẬT TOÁN OLIBOMBY (Giữ nguyên)
                var (sliderPath, frameDist) = SliderPicturator.Picturate(
                    generatedSliderMask, sliderColor, borderColor, backgroundColor,
                    circleSize, AnalyzedSlider.Pos, new Vector2(0, 0), AnalyzedSlider, 1080, 32768,
                    true, true, true, true, true, true, 1);

                var picturatedHo = new HitObject(AnalyzedSlider.Time, 0, SampleSet.None, SampleSet.None)
                {
                    IsCircle = false, IsSpinner = false, IsHoldNote = false, IsSlider = true
                };

                picturatedHo.SetAllCurvePoints(sliderPath);
                picturatedHo.SliderType = PathType.Linear;
                picturatedHo.PixelLength = CalculateLength(sliderPath);
                picturatedHo.SliderVelocity = double.NaN;

                beatmap.HitObjects.RemoveAll(h => h.IsSlider && Math.Abs(h.Time - AnalyzedSlider.Time) < 1);
                beatmap.HitObjects.Add(picturatedHo);
                beatmap.SortHitObjects();

                editor.SaveFile();
                MessageBox.Show($"Picturated slider created at {AnalyzedSlider.Time}ms.", "Done");
                LoadSlidersFromBeatmap();
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error"); }
        }

        private double CalculateLength(List<Vector2> points)
        {
            double length = 0;
            for (int i = 1; i < points.Count; i++) length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }
    }
}

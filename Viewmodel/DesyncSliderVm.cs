using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.MathUtil;
using HitObject = StandalonePicturator.Classes.BeatmapHelper.HitObject;

namespace StandalonePicturator.Viewmodel
{
    public class DesyncSliderVm : BindableBase
    {
        private static readonly string SessionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "picturator_session.json");

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private string beatmapPath;
        public string BeatmapPath
        {
            get => beatmapPath;
            set { if (Set(ref beatmapPath, value)) SaveSession(); }
        }

        private double timeCode = 11828;
        public double TimeCode
        {
            get => timeCode;
            set { if (Set(ref timeCode, value)) SaveSession(); }
        }

        private double duration = 1000;
        public double Duration
        {
            get => duration;
            set { if (Set(ref duration, value)) SaveSession(); }
        }

        private int desyncMode = 0;
        public int DesyncMode
        {
            get => desyncMode;
            set { if (Set(ref desyncMode, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double stationaryX = 256;
        public double StationaryX
        {
            get => stationaryX;
            set { if (Set(ref stationaryX, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double stationaryY = 192;
        public double StationaryY
        {
            get => stationaryY;
            set { if (Set(ref stationaryY, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double offsetX = 100;
        public double OffsetX
        {
            get => offsetX;
            set { if (Set(ref offsetX, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double offsetY = 0;
        public double OffsetY
        {
            get => offsetY;
            set { if (Set(ref offsetY, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private string visualSliderInfo = "No visual slider loaded (Ctrl+C in osu!)";
        public string VisualSliderInfo
        {
            get => visualSliderInfo;
            set => Set(ref visualSliderInfo, value);
        }

        private HitObject visualSlider;
        public HitObject VisualSlider
        {
            get => visualSlider;
            set
            {
                if (Set(ref visualSlider, value))
                {
                    if (value != null)
                    {
                        VisualSliderInfo = $"Loaded: [{TimeSpan.FromMilliseconds(value.Time):mm\\:ss\\.fff}] ({value.SliderType}, {value.PixelLength:F0}px)";
                        TimeCode = value.Time;
                        Duration = value.TemporalLength > 0 ? value.TemporalLength : 1000;
                    }
                    RegeneratePreview();
                    SaveSession();
                }
            }
        }

        private InteropBitmap previewImage;
        public InteropBitmap PreviewImage
        {
            get => previewImage;
            set => Set(ref previewImage, value);
        }

        public CommandImplementation SelectBeatmapCommand { get; }
        public CommandImplementation ImportVisualCommand { get; }

        public DesyncSliderVm()
        {
            SelectBeatmapCommand = new CommandImplementation(_ => {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "osu! Beatmap (*.osu)|*.osu" };
                if (ofd.ShowDialog() == true) BeatmapPath = ofd.FileName;
            });

            ImportVisualCommand = new CommandImplementation(_ => ImportVisualFromClipboard());

            LoadSession();
            RegeneratePreview();
        }

        // HÀM BÓC TÁCH TIMESTAMP TỪ CLIPBOARD (VD: "00:11:828 (1) - " -> 11828 ms)
        private static bool TryParseOsuTimestamp(string text, out double timeMs)
        {
            timeMs = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var match = Regex.Match(text, @"(\d+):(\d{2}):(\d{3})");
            if (match.Success)
            {
                int minutes = int.Parse(match.Groups[1].Value);
                int seconds = int.Parse(match.Groups[2].Value);
                int millis = int.Parse(match.Groups[3].Value);
                timeMs = minutes * 60000 + seconds * 1000 + millis;
                return true;
            }

            if (double.TryParse(text.Trim(), out double ms))
            {
                timeMs = ms;
                return true;
            }

            return false;
        }

        private void ImportVisualFromClipboard()
        {
            HitObject loadedSlider = null;
            double targetTime = TimeCode;

            // 1. Kiểm tra Clipboard
            if (Clipboard.ContainsText())
            {
                string clipText = Clipboard.GetText().Trim();

                // Trường hợp A: Clipboard chứa chuỗi Timestamp từ osu! Editor (vd: "00:11:828 (1) - ")
                if (TryParseOsuTimestamp(clipText, out double parsedTime))
                {
                    targetTime = parsedTime;
                    TimeCode = parsedTime;
                }

                // Trường hợp B: Clipboard chứa raw hitobject line
                foreach (var line in clipText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var ho = new HitObject(line);
                        if (ho.IsSlider)
                        {
                            loadedSlider = ho;
                            break;
                        }
                    }
                    catch { }
                }
            }

            // 2. Tìm slider khớp mốc thời gian trong file .osu đã chọn
            if (loadedSlider == null && !string.IsNullOrEmpty(BeatmapPath) && File.Exists(BeatmapPath))
            {
                try
                {
                    var editor = new BeatmapEditor(BeatmapPath);
                    var sliders = editor.Beatmap.HitObjects.Where(h => h.IsSlider).OrderBy(s => s.Time).ToList();
                    if (sliders.Count > 0)
                    {
                        // Tìm slider có mốc thời gian sát với targetTime nhất (sai số <= 100ms)
                        var matched = sliders.FirstOrDefault(s => Math.Abs(s.Time - targetTime) <= 100)
                                   ?? (targetTime > 0 ? sliders.OrderBy(s => Math.Abs(s.Time - targetTime)).First() : sliders.First());

                        loadedSlider = matched;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Beatmap load error: {ex.Message}", "Error");
                    return;
                }
            }

            // 3. Cập nhật kết quả
            if (loadedSlider != null)
            {
                VisualSlider = loadedSlider;
                TimeCode = loadedSlider.Time;
                Duration = loadedSlider.TemporalLength > 0 ? loadedSlider.TemporalLength : 1000;
                MessageBox.Show($"Slider loaded at [{TimeSpan.FromMilliseconds(loadedSlider.Time):mm\\:ss\\.fff}]!\n• Type: {loadedSlider.SliderType}\n• Length: {loadedSlider.PixelLength:F0}px", "Thành công");
            }
            else
            {
                MessageBox.Show("No sliders found.\nChoose the correct .osu beatmap above.", "Notice");
            }
        }

        public void RegeneratePreview()
        {
            int w = 512, h = 384;
            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(22, 22, 28));

                using (Pen gridPen = new Pen(Color.FromArgb(40, 40, 52), 1f))
                {
                    g.DrawRectangle(gridPen, 0, 0, 511, 383);
                    g.DrawLine(gridPen, 0, 192, 512, 192);
                    g.DrawLine(gridPen, 256, 0, 256, 384);
                }

                // 1. Vẽ Thân Slider Ảo (Mắt nhìn - Màu tím pastel)
                if (VisualSlider != null)
                {
                    var pts = VisualSlider.GetAllCurvePoints();
                    if (pts.Count > 1)
                    {
                        PointF[] gdiPts = pts.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
                        using (Pen bodyPen = new Pen(Color.FromArgb(180, 120, 90, 220), 48f))
                        {
                            bodyPen.StartCap = LineCap.Round;
                            bodyPen.EndCap = LineCap.Round;
                            bodyPen.LineJoin = LineJoin.Round;
                            g.DrawLines(bodyPen, gdiPts);
                        }
                        using (Pen borderPen = new Pen(Color.FromArgb(220, 200, 180, 255), 3f))
                        {
                            borderPen.StartCap = LineCap.Round;
                            borderPen.EndCap = LineCap.Round;
                            g.DrawLines(borderPen, gdiPts);
                        }
                    }
                }

                // 2. Vẽ Hitbox Thực Tế (Tay bấm - Màu vàng neon)
                if (DesyncMode == 0) // Stationary
                {
                    float r = 50f;
                    using (SolidBrush hbBrush = new SolidBrush(Color.FromArgb(80, 255, 215, 0)))
                    using (Pen hbPen = new Pen(Color.FromArgb(255, 215, 0), 2.5f) { DashStyle = DashStyle.Dash })
                    {
                        g.FillEllipse(hbBrush, (float)StationaryX - r, (float)StationaryY - r, r * 2, r * 2);
                        g.DrawEllipse(hbPen, (float)StationaryX - r, (float)StationaryY - r, r * 2, r * 2);
                        g.FillEllipse(Brushes.White, (float)StationaryX - 4, (float)StationaryY - 4, 8, 8);
                    }
                }
                else if (DesyncMode == 1 && VisualSlider != null) // Offset
                {
                    var pts = VisualSlider.GetAllCurvePoints();
                    if (pts.Count > 1)
                    {
                        PointF[] offsetPts = pts.Select(p => new PointF((float)(p.X + OffsetX), (float)(p.Y + OffsetY))).ToArray();
                        using (Pen offPen = new Pen(Color.FromArgb(255, 215, 0), 3f) { DashStyle = DashStyle.Dash })
                        {
                            g.DrawLines(offPen, offsetPts);
                        }
                    }
                }
            }

            IntPtr hBmp = bmp.GetHbitmap();
            try
            {
                PreviewImage = (InteropBitmap)Imaging.CreateBitmapSourceFromHBitmap(
                    hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBmp);
                bmp.Dispose();
            }
        }

        private void SaveSession()
        {
            try
            {
                string json = File.Exists(SessionFilePath) ? File.ReadAllText(SessionFilePath) : "{}";
                dynamic obj = JsonConvert.DeserializeObject(json) ?? new System.Dynamic.ExpandoObject();
                obj.BeatmapPath = this.BeatmapPath;
                File.WriteAllText(SessionFilePath, JsonConvert.SerializeObject(obj, Formatting.Indented));
            }
            catch { }
        }

        private void LoadSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    dynamic obj = JsonConvert.DeserializeObject(File.ReadAllText(SessionFilePath));
                    if (obj != null && obj.BeatmapPath != null)
                    {
                        this.beatmapPath = (string)obj.BeatmapPath;
                        RaisePropertyChanged(nameof(BeatmapPath));
                    }
                }
            }
            catch { }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;
using StandalonePicturator.Classes.Tools.SlideratorStuff;
using Newtonsoft.Json;
using HitObject = StandalonePicturator.Classes.BeatmapHelper.HitObject;

namespace StandalonePicturator.Viewmodel
{
    public partial class SliderPicturatorVm : BindableBase
    {
        #region Properties
        private static readonly string SessionFilePath = StandalonePicturator.Classes.PicturatorStorage.FilePath("picturator_session.json");

        private CancellationTokenSource previewTokenSource;
        private bool isProcessingPreview;

        [JsonIgnore]
        public bool IsProcessingPreview
        {
            get => isProcessingPreview;
            set => Set(ref isProcessingPreview, value);
        }

        private string beatmapPath;
        public string BeatmapPath
        {
            get => beatmapPath;
            set
            {
                if (Set(ref beatmapPath, value)) {
                    Path = value;
                    OnBeatmapChanged(value);
                    SaveSession();
                }
            }
        }

        [JsonIgnore]
        public string Path { get; set; }

        [JsonIgnore]
        public bool Quick { get; set; }

        private long viewportSize = 32768;
        public long ViewportSize
        {
            get => viewportSize;
            set => Set(ref viewportSize, value);
        }

        private int quality = 25;
        public int Quality
        {
            get => quality;
            set
            {
                if (Set(ref quality, value)) {
                    RegeneratePreview();
                    SaveSession();
                }
            }
        }

        private long segmentCount;
        public long SegmentCount
        {
            get => segmentCount;
            set => Set(ref segmentCount, value);
        }

        [JsonIgnore]
        public IEnumerable<long> ViewportSizes => new List<long> { 16384, 32768, 65536 };

        private double yResolution = 1080;
        public double YResolution
        {
            get => yResolution;
            set { if (double.IsFinite(value) && value >= 120 && Set(ref yResolution, value)) { RefreshSource(); SaveSession(); } }
        }

        private double sliderStartX = 244;
        public double SliderStartX
        {
            get => sliderStartX;
            set => SetPlacement(value, SliderStartY, true);
        }

        private double sliderStartY = 78;
        public double SliderStartY
        {
            get => sliderStartY;
            set => SetPlacement(SliderStartX, value, true);
        }

        private double imageStartX = 193.01;
        public double ImageStartX
        {
            get => imageStartX;
            set => SetPlacement(value, ImageStartY, false);
        }

        private double imageStartY = -14.99;
        public double ImageStartY
        {
            get => imageStartY;
            set => SetPlacement(ImageStartX, value, false);
        }

        private double targetCS = 4.1;
        public double TargetCS
        {
            get => targetCS;
            set
            {
                if (double.IsFinite(value) && value >= 0 && value <= 10 && Set(ref targetCS, value)) {
                    RefreshSource();
                    SaveSession();
                }
            }
        }

        private double sliderScale = 1.0;
        public double SliderScale
        {
            get => sliderScale;
            set
            {
                if (!double.IsFinite(value) || value < 0.2 || value > 3 || value == sliderScale) return;
                double ratio = value / sliderScale;
                if (SelectedSlider == null && Bm != null) {
                    // A bitmap scales about its top-left corner; retain the head's position inside it.
                    sliderStartX = imageStartX + (sliderStartX - imageStartX) * ratio;
                    sliderStartY = imageStartY + (sliderStartY - imageStartY) * ratio;
                    RaisePropertyChanged(nameof(SliderStartX));
                    RaisePropertyChanged(nameof(SliderStartY));
                }
                Set(ref sliderScale, value);
                RefreshSource();
                SaveSession();
            }
        }

        // =========================================================================
        // THÔNG SỐ TÙY CHỌN HIỆU ỨNG NHIỄU SLIDER (GLITCH EFFECT)
        // =========================================================================
        [JsonIgnore]
        public bool IsAnyGlitchEnabled => IsGlitchOn || LayeredGlitch;

        private bool isGlitchOn = false;
        public bool IsGlitchOn
        {
            get => isGlitchOn;
            set
            {
                if (Set(ref isGlitchOn, value)) {
                    RaisePropertyChanged(nameof(IsAnyGlitchEnabled));
                    RefreshSource();
                    SaveSession();
                }
            }
        }

        private double glitchAmount = 45.0; // Độ văng ngang (osupx)
        public double GlitchAmount
        {
            get => glitchAmount;
            set
            {
                if (Set(ref glitchAmount, value)) {
                    RefreshSource();
                    SaveSession();
                }
            }
        }

        private double glitchFrequency = 40.0; // Tần suất xuất hiện (%)
        public double GlitchFrequency
        {
            get => glitchFrequency;
            set
            {
                if (Set(ref glitchFrequency, value)) {
                    RefreshSource();
                    SaveSession();
                }
            }
        }

        private int glitchThickness = 3; // Chiều cao lát cắt (pixels)
        public int GlitchThickness
        {
            get => glitchThickness;
            set
            {
                if (Set(ref glitchThickness, value)) {
                    RefreshSource();
                    SaveSession();
                }
            }
        }

        private int glitchSeed = 1337;
        public int GlitchSeed
        {
            get => glitchSeed;
            set
            {
                if (Set(ref glitchSeed, value)) {
                    RefreshSource();
                    SaveSession();
                }
            }
        }

        private bool blackOn = false;
        public bool BlackOn
        {
            get => blackOn;
            set { if (Set(ref blackOn, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private bool borderOn = false;
        public bool BorderOn
        {
            get => borderOn;
            set { if (Set(ref borderOn, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private bool redOn = true;
        public bool RedOn
        {
            get => redOn;
            set { if (Set(ref redOn, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private bool greenOn = true;
        public bool GreenOn
        {
            get => greenOn;
            set { if (Set(ref greenOn, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private bool blueOn = true;
        public bool BlueOn
        {
            get => blueOn;
            set { if (Set(ref blueOn, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private bool alphaOn = true;
        public bool AlphaOn
        {
            get => alphaOn;
            set { if (Set(ref alphaOn, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(255, 255, 255, 255);
        public System.Windows.Media.Color BorderColor
        {
            get => borderColor;
            set
            {
                if (Set(ref borderColor, value)) {
                    RefreshSource();
                    SaveSession();
                }
            }
        }

        private System.Windows.Media.Color trackColorPickerColor = System.Windows.Media.Color.FromArgb(255, 36, 36, 46);
        public System.Windows.Media.Color TrackColorPickerColor
        {
            get => trackColorPickerColor;
            set {
                if (Set(ref trackColorPickerColor, value)) {
                    CurrentTrackColor = Color.FromArgb(value.R, value.G, value.B);
                    SaveSession();
                }
            }
        }

        private Color currentTrackColor = Color.FromArgb(36, 36, 46);
        public Color CurrentTrackColor
        {
            get => currentTrackColor;
            set
            {
                if (Set(ref currentTrackColor, value)) {
                    SaveSession();
                }
            }
        }

        private bool hasSliderBall = false;
        public bool HasSliderBall
        {
            get => hasSliderBall;
            set
            {
                if (Set(ref hasSliderBall, value)) {
                    RaisePropertyChanged(nameof(SliderBallStatusText));
                    SaveSession();
                }
            }
        }

        [JsonIgnore]
        public string SliderBallStatusText => HasSliderBall 
            ? "● Sliderball: ON (follows the path)" 
            : "○ Sliderball: OFF (static picture)";

        // Gộp tất cả các hình dạng layer đang bật vào 1 quỹ đạo slider duy nhất
        private bool chainAllVisibleBallPaths = false;
        public bool ChainAllVisibleBallPaths
        {
            get => chainAllVisibleBallPaths;
            set
            {
                if (Set(ref chainAllVisibleBallPaths, value)) {
                    SaveSession();
                }
            }
        }

        private HitObject ballPathSlider;
        public HitObject BallPathSlider
        {
            get => ballPathSlider;
            set { if (Set(ref ballPathSlider, value)) { BallPathSliderLine = value?.ToString() ?? ""; SaveSession(); } }
        }

        private double timeCode = 0;
        public double TimeCode
        {
            get => timeCode;
            set { if (double.IsFinite(value) && Set(ref timeCode, Math.Round(value))) SaveSession(); }
        }

        private double duration = 1000;
        public double Duration
        {
            get => duration;
            set { if (double.IsFinite(value) && value >= 2 && value <= 60000 && Set(ref duration, Math.Round(value))) SaveSession(); }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private Bitmap bm;
        [JsonIgnore]
        public Bitmap Bm
        {
            get => bm;
            set { var previous = bm; if (Set(ref bm, value)) { previous?.Dispose(); cleanGlitchSource?.Dispose(); cleanGlitchSource = null; } }
        }

        private InteropBitmap bmImage;
        [JsonIgnore]
        public InteropBitmap BmImage
        {
            get => bmImage;
            set => Set(ref bmImage, value);
        }

        private string pictureFile;
        public string PictureFile
        {
            get => pictureFile;
            set {
                Set(ref pictureFile, value);
                if (!string.IsNullOrEmpty(value) && File.Exists(value)) {
                    try {
                        SelectedSlider = null;
                        SelectedSliderLine = "";
                        RefreshSource();
                    } catch { }
                }
                SaveSession();
            }
        }

        private string importedSliderInfo = "No shape slider loaded";
        [JsonIgnore]
        public string ImportedSliderInfo
        {
            get => importedSliderInfo;
            set => Set(ref importedSliderInfo, value);
        }

        private HitObject selectedSlider;
        [JsonIgnore]
        public HitObject SelectedSlider
        {
            get => selectedSlider;
            set
            {
                if (Set(ref selectedSlider, value)) {
                    if (value != null) {
                        ImportedSliderInfo = $"Loaded: [{TimeSpan.FromMilliseconds(value.Time):mm\\:ss\\.fff}] ({value.SliderType}, {value.PixelLength:F0}px)";
                        SelectedSliderLine = value.ToString();
                        Set(ref sliderStartX, value.Pos.X, nameof(SliderStartX));
                        Set(ref sliderStartY, value.Pos.Y, nameof(SliderStartY));
                        BallOffsetX = 0;
                        BallOffsetY = 0;
                        BallPathSlider = null;
                        RasterizeSliderToImage(value);
                    } else {
                        ImportedSliderInfo = "No shape slider loaded";
                        SelectedSliderLine = "";
                    }
                    SaveSession();
                }
            }
        }

        private string selectedSliderLine = "";
        public string SelectedSliderLine
        {
            get => selectedSliderLine;
            set => Set(ref selectedSliderLine, value);
        }

        private string ballPathSliderLine = "";
        public string BallPathSliderLine
        {
            get => ballPathSliderLine;
            set => Set(ref ballPathSliderLine, value);
        }

        [JsonIgnore]
        public CommandImplementation SelectBeatmapCommand { get; }
        [JsonIgnore]
        public CommandImplementation UploadFileCommand { get; }
        [JsonIgnore]
        public CommandImplementation ImportShapeCommand { get; }
        [JsonIgnore]
        public CommandImplementation ImportBallPathCommand { get; }
        [JsonIgnore]
        public CommandImplementation RemoveCommand { get; }
        [JsonIgnore]
        public CommandImplementation SaveProgressCommand { get; }
        [JsonIgnore]
        public CommandImplementation RandomizeGlitchSeedCommand { get; }
        #endregion

        private readonly bool detachedLayer;
        public SliderPicturatorVm() : this(false) { }
        private SliderPicturatorVm(bool detached)
        {
            detachedLayer = detached;
            SelectBeatmapCommand = new CommandImplementation(_ => BrowseBeatmap());
            UploadFileCommand = new CommandImplementation(_ => SetFile());
            ImportShapeCommand = new CommandImplementation(_ => ImportSliderShape());
            ImportBallPathCommand = new CommandImplementation(_ => ImportBallPath());
            RandomizeGlitchSeedCommand = new CommandImplementation(_ => {
                GlitchSeed = new Random().Next(100, 99999);
            });
            RemoveCommand = new CommandImplementation(_ => {
                SelectedSlider = null;
                BallPathSlider = null;
                SelectedSliderLine = "";
                BallPathSliderLine = "";
                Bm = null;
                PictureFile = "";
                RegeneratePreview();
                SegmentCount = 0;
                RaisePropertyChanged(nameof(BmImage));
                SaveSession();
                MessageBox.Show("Slider removed.", "Notice");
            });
            SaveProgressCommand = new CommandImplementation(_ => {
                SaveSession();
                MessageBox.Show("Session saved.", "Save session");
            });

            if (!detached) { LoadSession(); InitializeLibrary(); }
        }

        private void BrowseBeatmap()
        {
            OpenFileDialog ofd = new OpenFileDialog {
                Filter = "osu! Beatmap (*.osu)|*.osu",
                Title = "Choose the saved editor beatmap"
            };
            if (ofd.ShowDialog() == true) {
                BeatmapPath = ofd.FileName;
            }
        }

        private void OnBeatmapChanged(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try {
                var editor = new BeatmapEditor(path);
                if (editor.Beatmap.Difficulty.ContainsKey("CircleSize")) {
                    TargetCS = editor.Beatmap.Difficulty["CircleSize"].DoubleValue;
                }
            } catch { }
        }

        private void SetFile()
        {
            var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true, Title = "Import images" };
            if (dialog.ShowDialog() == true) ImportImages(dialog.FileNames);
        }

        // THUẬT TOÁN TẠO NHIỄU GLITCH XOAY THEO HƯỚNG BẤT KỲ (DIRECTION ROTATION)
        // THUẬT TOÁN TẠO NHIỄU GLITCH NGUYÊN BẢN (SPURIOUS SCANLINE PROTRUSIONS & SLICE TEARING) CÓ HỖ TRỢ DIRECTION
        public static void ApplyGlitch(Bitmap bmp, double amountOsuPx, double frequencyPercent, int thickness, int seed, double resolution = 1080, double angleDegrees = 0)
        {
            if (bmp == null || amountOsuPx <= 0 || frequencyPercent <= 0) return;

            if (Math.Abs(angleDegrees) < 0.5)
            {
                ApplyGlitchCore(bmp, amountOsuPx, frequencyPercent, thickness, seed, resolution);
                return;
            }

            int origW = bmp.Width;
            int origH = bmp.Height;
            int diag = (int)Math.Ceiling(Math.Sqrt(origW * origW + origH * origH)) + 8;

            using (Bitmap rotBmp = new Bitmap(diag, diag, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(rotBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.TranslateTransform(diag / 2f, diag / 2f);
                    g.RotateTransform((float)-angleDegrees);
                    g.TranslateTransform(-origW / 2f, -origH / 2f);
                    g.DrawImage(bmp, 0, 0);
                }

                ApplyGlitchCore(rotBmp, amountOsuPx, frequencyPercent, thickness, seed, resolution);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.TranslateTransform(origW / 2f, origH / 2f);
                    g.RotateTransform((float)angleDegrees);
                    g.TranslateTransform(-diag / 2f, -diag / 2f);
                    g.DrawImage(rotBmp, 0, 0);
                }
            }
        }

        private static void ApplyGlitchCore(Bitmap bmp, double amountOsuPx, double frequencyPercent, int thickness, int seed, double resolution = 1080)
        {
            double scaleFactor = (resolution - 16.0) / 480.0;
            int maxDisplacement = (int)Math.Max(5, amountOsuPx * scaleFactor);
            Random rand = new Random(seed);

            int w = bmp.Width;
            int h = bmp.Height;

            // 1. Scanline Glitch Spikes (Tia răng cưa)
            using (Graphics g = Graphics.FromImage(bmp))
            {
                using (Pen whitePen = new Pen(Color.White, 1f))
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (rand.NextDouble() * 100.0 < frequencyPercent * 0.45)
                        {
                            int xMin = -1, xMax = -1;
                            for (int x = 0; x < w; x++)
                            {
                                if (bmp.GetPixel(x, y).R > 100)
                                {
                                    if (xMin == -1) xMin = x;
                                    xMax = x;
                                }
                            }

                            if (xMin != -1 && xMax != -1)
                            {
                                int spikeH = rand.Next(1, Math.Max(2, thickness + 1));
                                whitePen.Width = spikeH;

                                if (rand.NextDouble() < 0.70)
                                {
                                    int spikeLen = rand.Next((int)(maxDisplacement * 0.35), maxDisplacement + 1);
                                    int targetX = Math.Max(0, xMin - spikeLen);
                                    g.DrawLine(whitePen, targetX, y, xMin, y);
                                }

                                if (rand.NextDouble() < 0.70)
                                {
                                    int spikeLen = rand.Next((int)(maxDisplacement * 0.35), maxDisplacement + 1);
                                    int targetX = Math.Min(w - 1, xMax + spikeLen);
                                    g.DrawLine(whitePen, xMax, y, targetX, y);
                                }
                            }
                        }
                    }
                }

                // 2. Horizontal Cutout Slits (Vết rách)
                using (Pen blackPen = new Pen(Color.Black, 1f))
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (rand.NextDouble() * 100.0 < frequencyPercent * 0.20)
                        {
                            int xMin = -1, xMax = -1;
                            for (int x = 0; x < w; x++)
                            {
                                if (bmp.GetPixel(x, y).R > 100)
                                {
                                    if (xMin == -1) xMin = x;
                                    xMax = x;
                                }
                            }

                            if (xMin != -1 && xMax - xMin > 30)
                            {
                                int slitLen = rand.Next(15, Math.Min(80, (xMax - xMin) / 2));
                                int slitX = rand.Next(xMin + 5, xMax - slitLen - 5);
                                blackPen.Width = rand.Next(1, Math.Max(2, thickness));
                                g.DrawLine(blackPen, slitX, y, slitX + slitLen, y);
                            }
                        }
                    }
                }
            }

            // 3. Slice Displacement (Giật trượt lát cắt)
            using (Bitmap copy = (Bitmap)bmp.Clone())
            {
                int y = 0;
                while (y < h)
                {
                    if (rand.NextDouble() * 100.0 < frequencyPercent * 0.30)
                    {
                        int sliceH = rand.Next(1, Math.Max(2, thickness * 2));
                        int dx = rand.Next(-maxDisplacement / 2, maxDisplacement / 2 + 1);

                        for (int curY = y; curY < Math.Min(h, y + sliceH); curY++)
                        {
                            for (int x = 0; x < w; x++)
                            {
                                int srcX = x - dx;
                                Color c = (srcX >= 0 && srcX < w) ? copy.GetPixel(srcX, curY) : Color.Black;
                                bmp.SetPixel(x, curY, c);
                            }
                        }
                        y += sliceH;
                    }
                    else
                    {
                        y++;
                    }
                }
            }
        }

        public void RasterizeSliderToImage(HitObject slider)
        {
            if (slider == null) return;

            try
            {
                List<Vector2> rawPath = null;
                try { rawPath = slider.GetSliderPath().CalculatedPath.ToList(); } catch { }
                if (rawPath == null || rawPath.Count < 2) rawPath = slider.GetAllCurvePoints();
                if (rawPath == null || rawPath.Count < 2) return;

                var scaledPath = rawPath.Select(p => new Vector2(
                    SliderStartX + (p.X - slider.Pos.X) * SliderScale,
                    SliderStartY + (p.Y - slider.Pos.Y) * SliderScale)).ToList();
                float baseRadius = (float)Beatmap.GetHitObjectRadius(TargetCS);
                
                double glitchMargin = IsGlitchOn ? GlitchAmount + 20.0 : 0.0;
                double margin = baseRadius + 15.0 + glitchMargin;

                double sMinX = scaledPath.Min(p => p.X) - margin;
                double sMinY = scaledPath.Min(p => p.Y) - margin;
                double sMaxX = scaledPath.Max(p => p.X) + margin;
                double sMaxY = scaledPath.Max(p => p.Y) + margin;

                double boxW = sMaxX - sMinX;
                double boxH = sMaxY - sMinY;

                double scaleFactor = (YResolution - 16.0) / 480.0;

                int bmpW = Math.Max(20, (int)Math.Ceiling(boxW * scaleFactor));
                int bmpH = Math.Max(20, (int)Math.Ceiling(boxH * scaleFactor));

                Bitmap maskBmp = new Bitmap(bmpW, bmpH);
                using (Graphics g = Graphics.FromImage(maskBmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Black);

                    PointF[] pts = scaledPath.Select(p => new PointF(
                        (float)((p.X - sMinX) * scaleFactor),
                        (float)((p.Y - sMinY) * scaleFactor)
                    )).ToArray();

                    using (Pen p = new Pen(Color.White, baseRadius * 2f * (float)scaleFactor))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        p.LineJoin = LineJoin.Round;
                        g.DrawLines(p, pts);
                    }
                }

                Set(ref imageStartX, sMinX, nameof(ImageStartX));
                Set(ref imageStartY, sMinY, nameof(ImageStartY));
                SetPictureMask(maskBmp);
                PictureFile = $"(CS {TargetCS:F1}, Scale {SliderScale:F2}x{(IsGlitchOn ? ", Glitch" : "")})";
                RaisePropertyChanged(nameof(PictureFile));
                RaisePropertyChanged(nameof(Bm));
                RaisePropertyChanged(nameof(ImageStartX));
                RaisePropertyChanged(nameof(ImageStartY));
                RaisePropertyChanged(nameof(SliderStartX));
                RaisePropertyChanged(nameof(SliderStartY));

                RegeneratePreview();
                SaveSession();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Image error: {ex.Message}", "Error");
            }
        }

        public static Bitmap CleanImageBackground(Bitmap source)
        {
            int w = source.Width;
            int h = source.Height;
            Bitmap result = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            bool alreadyHasAlpha = false;
            for (int y = 0; y < Math.Min(h, 20); y++) {
                for (int x = 0; x < Math.Min(w, 20); x++) {
                    if (source.GetPixel(x, y).A < 30) { alreadyHasAlpha = true; break; }
                }
                if (alreadyHasAlpha) break;
            }

            if (alreadyHasAlpha) {
                using (Graphics g = Graphics.FromImage(result)) g.DrawImage(source, 0, 0);
                return result;
            }

            Color cornerCol = source.GetPixel(0, 0);
            bool[,] isBg = new bool[w, h];
            Queue<System.Drawing.Point> queue = new Queue<System.Drawing.Point>();

            bool IsBgPixel(Color c) {
                int dr = c.R - cornerCol.R; int dg = c.G - cornerCol.G; int db = c.B - cornerCol.B;
                return (dr * dr + dg * dg + db * db) < (45 * 45) || (c.R < 35 && c.G < 35 && c.B < 35);
            }

            void EnqueueIfBg(int x, int y) {
                if (!isBg[x, y] && IsBgPixel(source.GetPixel(x, y))) {
                    isBg[x, y] = true;
                    queue.Enqueue(new System.Drawing.Point(x, y));
                }
            }

            for (int x = 0; x < w; x++) { EnqueueIfBg(x, 0); EnqueueIfBg(x, h - 1); }
            for (int y = 0; y < h; y++) { EnqueueIfBg(0, y); EnqueueIfBg(w - 1, y); }

            int[] dx = { 1, -1, 0, 0 }; int[] dy = { 0, 0, 1, -1 };
            while (queue.Count > 0) {
                var pt = queue.Dequeue();
                for (int i = 0; i < 4; i++) {
                    int nx = pt.X + dx[i]; int ny = pt.Y + dy[i];
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && !isBg[nx, ny]) {
                        if (IsBgPixel(source.GetPixel(nx, ny))) {
                            isBg[nx, ny] = true;
                            queue.Enqueue(new System.Drawing.Point(nx, ny));
                        }
                    }
                }
            }

            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    if (isBg[x, y]) result.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                    else result.SetPixel(x, y, source.GetPixel(x, y));
                }
            }
            return result;
        }

        private async void ImportSliderShape() => await LoadSelectedSlidersAsync();

        private void ImportBallPath()
        {
            try {
                if (!Clipboard.ContainsText()) throw new InvalidOperationException("Select a slider in osu! editor and press Ctrl+C first.");
                var map = File.Exists(BeatmapPath) ? new BeatmapEditor(BeatmapPath).Beatmap : null;
                var paths = StandalonePicturator.Classes.EditorSliderSelection.Resolve(Clipboard.GetText(), map);
                if (paths == null || paths.Count == 0) throw new InvalidOperationException("No valid sliders found in clipboard.");

                if (paths.Count == 1)
                {
                    BallPathSlider = paths[0];
                    BallPathSliderLine = paths[0].GetLine();
                }
                else
                {
                    // Gộp tất cả sliders được chọn thành 1 quỹ đạo liên tục duy nhất
                    var combined = new List<Vector2>();
                    foreach (var slider in paths)
                    {
                        var calc = slider.GetSliderPath().CalculatedPath;
                        if (calc == null || calc.Count < 2) calc = slider.GetAllCurvePoints();
                        if (calc == null || calc.Count < 2) continue;

                        var exp = new List<Vector2>(calc);
                        for (int r = 1; r < Math.Max(1, slider.Repeat); r++)
                        {
                            var span = (r % 2 == 1) ? calc.AsEnumerable().Reverse() : calc;
                            exp.AddRange(span.Skip(1));
                        }
                        combined.AddRange(exp);
                    }

                    var compound = paths[0].DeepCopy();
                    compound.IsCircle = false;
                    compound.IsSlider = true;
                    compound.Repeat = 1;
                    compound.SliderType = PathType.Linear;
                    compound.SetAllCurvePoints(combined);
                    compound.PixelLength = 0;
                    for (int i = 1; i < combined.Count; i++) 
                        compound.PixelLength += (combined[i] - combined[i - 1]).Length;

                    BallPathSlider = compound;
                    BallPathSliderLine = compound.GetLine();
                }

                HasSliderBall = true;
                SaveSession();
                LibraryStatus = paths.Count == 1 
                    ? "Separate ball path imported from the editor selection." 
                    : $"Combined {paths.Count} sliders into 1 unified ball path.";
            } catch (Exception ex) { LibraryStatus = ex.Message; }
        }

        public async void RegeneratePreview()
        {
            previewTokenSource?.Cancel();
            previewTokenSource?.Dispose();
            previewTokenSource = new CancellationTokenSource();
            var ct = previewTokenSource.Token;
            if (Bm == null) { BmImage = null; IsProcessingPreview = false; return; }
            IsProcessingPreview = true;
            using var snapshot = (Bitmap)Bm.Clone();
            using var glitchSnapshot = CaptureNativeGlitch();
            int snapshotQuality = Math.Clamp(Quality, 1, 101);
            double nativeRadius = NativeSliderShading ? Beatmap.GetHitObjectRadius(TargetCS) * (YResolution - 16) / 480 : 0;
            try {
                await Task.Delay(100, ct);
                var result = await Task.Run(() => SliderPicturator.Recolor(
                    snapshot, Color.White, Color.White, Color.Black, null,
                    true, true, true, true, true, true, snapshotQuality, nativeRadius, glitchSnapshot?.Build(nativeRadius, snapshotQuality)));
                using var newBitmap = result.Item1;
                if (nativeRadius <= 0) ClearPreviewBackground(newBitmap, snapshot);
                if (ct.IsCancellationRequested) return;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    if (ct.IsCancellationRequested) return;
                    IntPtr handle = newBitmap.GetHbitmap();
                    try {
                        var image = (InteropBitmap)Imaging.CreateBitmapSourceFromHBitmap(
                            handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        if (image.CanFreeze) image.Freeze();
                        BmImage = image;
                        SegmentCount = result.Item2;
                    } finally { DeleteObject(handle); }
                });
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    if (!ct.IsCancellationRequested) ImportedSliderInfo = "Preview: " + ex.Message;
                });
            } finally {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    if (!ct.IsCancellationRequested) IsProcessingPreview = false;
                });
            }
        }

        private static System.Windows.Media.Color ParseMediaColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return System.Windows.Media.Colors.White;
            hex = hex.Trim().TrimStart('#');
            if (hex.Length == 8)
            {
                byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                return System.Windows.Media.Color.FromArgb(a, r, g, b);
            }
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return System.Windows.Media.Color.FromArgb(255, r, g, b);
            }
            return System.Windows.Media.Colors.White;
        }

        private SessionData CaptureSessionData() => new SessionData {
            NativeSliderShading = this.NativeSliderShading,
            Effects = layerEffects.Copy(),
            AutoOsuResolution = AutoOsuResolution,
            BallPathScale = BallPathScale,
            MinimumTumourLength = MinimumTumourLength,
            BallGraphEnabled = this.BallGraphEnabled,
            BallGraphPoints = this.BallGraphPoints.ToList(),
            BallOffsetX = this.BallOffsetX,
            BallOffsetY = this.BallOffsetY,
            BeatmapPath = this.BeatmapPath,
            PictureFile = this.PictureFile,
            TimeCode = this.TimeCode,
            Duration = this.Duration,
            YResolution = this.YResolution,
            SliderStartX = this.SliderStartX,
            SliderStartY = this.SliderStartY,
            ImageStartX = this.ImageStartX,
            ImageStartY = this.ImageStartY,
            TargetCS = this.TargetCS,
            SliderScale = this.SliderScale,
            Quality = this.Quality,
            HasSliderBall = this.HasSliderBall,
            ChainAllVisibleBallPaths = this.ChainAllVisibleBallPaths,
            BallSwitchMilliseconds = this.BallSwitchMilliseconds,
            IsGlitchOn = this.IsGlitchOn,
            GlitchAmount = this.GlitchAmount,
            GlitchFrequency = this.GlitchFrequency,
            GlitchThickness = this.GlitchThickness,
            GlitchSeed = this.GlitchSeed,
            BlackOn = this.BlackOn,
            BorderOn = this.BorderOn,
            RedOn = this.RedOn,
            GreenOn = this.GreenOn,
            BlueOn = this.BlueOn,
            AlphaOn = this.AlphaOn,
            BorderColorHex = this.BorderColor.ToString(),
            TrackColorPickerColorHex = this.TrackColorPickerColor.ToString(),
            SelectedSliderLine = this.SelectedSliderLine,
            BallPathSliderLine = this.BallPathSliderLine
        };

        public void SaveSession()
        {
            if (loadingSession || detachedLayer) return;
            try {
                StandalonePicturator.Classes.PicturatorStorage.Write(SessionFilePath, JsonConvert.SerializeObject(CaptureSessionData(), Formatting.Indented));
                SaveLibrary();
            } catch { }
        }

        public void LoadSession()
        {
            try {
                if (File.Exists(SessionFilePath)) RestoreSessionData(JsonConvert.DeserializeObject<SessionData>(File.ReadAllText(SessionFilePath)));
            } catch { }
        }

        private void RestoreSessionData(SessionData data)
        {
            if (data == null) return;
            loadingSession = true;
            try {
                previewTokenSource?.Cancel();
                selectedSlider = null;
                ImportedSliderInfo = "No shape slider loaded";
                ballPathSlider = null;
                pictureFile = data.PictureFile;
                Bm = null;
                BmImage = null;
                SegmentCount = 0;
                IsProcessingPreview = false;
                loadingSession = true;
                nativeSliderShading = data.NativeSliderShading;
                layerEffects = data.Effects?.Copy() ?? new StandalonePicturator.Classes.LayerEffects(); NotifyEffects();
                RaisePropertyChanged(nameof(NativeSliderShading));
                autoOsuResolution = data.AutoOsuResolution;
                ballPathScale = double.IsFinite(data.BallPathScale) ? Math.Clamp(data.BallPathScale, .1, 5) : 1;
                minimumTumourLength = double.IsFinite(data.MinimumTumourLength) ? Math.Clamp(data.MinimumTumourLength, 1, 12) : 12;
                RaisePropertyChanged(nameof(AutoOsuResolution));
                RaisePropertyChanged(nameof(BallPathScale));
                RaisePropertyChanged(nameof(MinimumTumourLength));
                ballGraphEnabled = data.BallGraphEnabled;
                ballGraphPoints = Array.AsReadOnly(StandalonePicturator.Classes.BallMotionGraph.Normalize(data.BallGraphPoints));
                RaisePropertyChanged(nameof(BallGraphEnabled));
                RaisePropertyChanged(nameof(BallGraphPoints));
                ballOffsetX = data.BallOffsetX;
                ballOffsetY = data.BallOffsetY;
                RaisePropertyChanged(nameof(BallOffsetX));
                RaisePropertyChanged(nameof(BallOffsetY));
                this.beatmapPath = data.BeatmapPath;
                this.Path = data.BeatmapPath;
                this.timeCode = data.TimeCode;
                this.duration = double.IsFinite(data.Duration) ? Math.Clamp(Math.Round(data.Duration), 2, 60000) : 1000;
                this.yResolution = double.IsFinite(data.YResolution) && data.YResolution >= 120 ? data.YResolution : 1080;
                this.sliderStartX = data.SliderStartX;
                this.sliderStartY = data.SliderStartY;
                this.imageStartX = data.ImageStartX;
                this.imageStartY = data.ImageStartY;
                this.targetCS = double.IsFinite(data.TargetCS) ? Math.Clamp(data.TargetCS, 0, 10) : 4.1;
                this.sliderScale = double.IsFinite(data.SliderScale) && data.SliderScale > 0 ? Math.Clamp(data.SliderScale, 0.2, 3) : 1.0;
                this.quality = data.Quality > 0 ? data.Quality : 25;
                this.hasSliderBall = data.HasSliderBall;
                this.chainAllVisibleBallPaths = data.ChainAllVisibleBallPaths;
                ballSwitchMilliseconds = Math.Clamp(data.BallSwitchMilliseconds, .1, 32);
                RaisePropertyChanged(nameof(BallSwitchMilliseconds));
                this.isGlitchOn = data.IsGlitchOn;
                this.glitchAmount = Math.Clamp(data.GlitchAmount, 0, 150);
                this.glitchFrequency = Math.Clamp(data.GlitchFrequency, 0, 90);
                this.glitchThickness = data.GlitchThickness > 0 ? data.GlitchThickness : 3;
                this.glitchSeed = data.GlitchSeed > 0 ? data.GlitchSeed : 1337;

                this.blackOn = false;
                this.borderOn = false;
                this.redOn = true;
                this.greenOn = true;
                this.blueOn = true;
                this.alphaOn = true;

                this.SelectedSliderLine = data.SelectedSliderLine ?? "";
                this.BallPathSliderLine = data.BallPathSliderLine ?? "";

                if (!string.IsNullOrEmpty(data.BorderColorHex)) {
                    try { this.borderColor = ParseMediaColor(data.BorderColorHex); } catch { }
                }
                if (!string.IsNullOrEmpty(data.TrackColorPickerColorHex)) {
                    try { 
                        this.trackColorPickerColor = ParseMediaColor(data.TrackColorPickerColorHex);
                        this.currentTrackColor = Color.FromArgb(this.trackColorPickerColor.R, this.trackColorPickerColor.G, this.trackColorPickerColor.B);
                    } catch { }
                }

                RaisePropertyChanged(nameof(BeatmapPath));
                RaisePropertyChanged(nameof(TimeCode));
                RaisePropertyChanged(nameof(Duration));
                RaisePropertyChanged(nameof(YResolution));
                RaisePropertyChanged(nameof(SliderStartX));
                RaisePropertyChanged(nameof(SliderStartY));
                RaisePropertyChanged(nameof(ImageStartX));
                RaisePropertyChanged(nameof(ImageStartY));
                RaisePropertyChanged(nameof(TargetCS));
                RaisePropertyChanged(nameof(SliderScale));
                RaisePropertyChanged(nameof(Quality));
                RaisePropertyChanged(nameof(IsGlitchOn));
                RaisePropertyChanged(nameof(GlitchAmount));
                RaisePropertyChanged(nameof(GlitchFrequency));
                RaisePropertyChanged(nameof(GlitchThickness));
                RaisePropertyChanged(nameof(GlitchSeed));
                RaisePropertyChanged(nameof(BorderColor));
                RaisePropertyChanged(nameof(TrackColorPickerColor));
                RaisePropertyChanged(nameof(HasSliderBall));
                RaisePropertyChanged(nameof(ChainAllVisibleBallPaths));
                RaisePropertyChanged(nameof(SliderBallStatusText));

                if (!string.IsNullOrEmpty(SelectedSliderLine)) {
                    try {
                        var ho = new HitObject(SelectedSliderLine);
                        selectedSlider = ho;
                        importedSliderInfo = $"Loaded: [{TimeSpan.FromMilliseconds(ho.Time):mm\\:ss\\.fff}] ({ho.SliderType}, {ho.PixelLength:F0}px)";
                        RaisePropertyChanged(nameof(ImportedSliderInfo));
                        RasterizeSliderToImage(ho);
                    } catch { }
                }

                if (selectedSlider == null && File.Exists(data.PictureFile)) PictureFile = data.PictureFile;

                if (!string.IsNullOrEmpty(BallPathSliderLine)) {
                    try { ballPathSlider = new HitObject(BallPathSliderLine); } catch { }
                }
            } catch { }
            finally { 
                loadingSession = false; 
                RaisePropertyChanged(nameof(SelectedSlider)); 
                RaisePropertyChanged(nameof(BallPathSlider)); 
                RaisePropertyChanged(nameof(PictureFile)); 
            }
        }

        private class SessionData
        {
            public StandalonePicturator.Classes.LayerEffects Effects { get; set; } = new();
            public bool NativeSliderShading { get; set; } = true;
            public bool AutoOsuResolution { get; set; } = true;
            public double BallPathScale { get; set; } = 1;
            public double MinimumTumourLength { get; set; } = 12;
            public bool BallGraphEnabled { get; set; }
            public List<StandalonePicturator.Classes.BallGraphPoint> BallGraphPoints { get; set; }
            public double BallOffsetX { get; set; }
            public double BallOffsetY { get; set; }
            public string BeatmapPath { get; set; }
            public string PictureFile { get; set; }
            public double TimeCode { get; set; }
            public double Duration { get; set; }
            public double YResolution { get; set; }
            public double SliderStartX { get; set; }
            public double SliderStartY { get; set; }
            public double ImageStartX { get; set; }
            public double ImageStartY { get; set; }
            public double TargetCS { get; set; } = 4.1;
            public double SliderScale { get; set; }
            public int Quality { get; set; }
            public bool HasSliderBall { get; set; }
            public bool ChainAllVisibleBallPaths { get; set; }
            public double BallSwitchMilliseconds { get; set; } = 4;
            public bool IsGlitchOn { get; set; }
            public double GlitchAmount { get; set; } = 45;
            public double GlitchFrequency { get; set; } = 40;
            public int GlitchThickness { get; set; }
            public int GlitchSeed { get; set; }
            public bool BlackOn { get; set; }
            public bool BorderOn { get; set; }
            public bool RedOn { get; set; }
            public bool GreenOn { get; set; }
            public bool BlueOn { get; set; }
            public bool AlphaOn { get; set; }
            public string BorderColorHex { get; set; }
            public string TrackColorPickerColorHex { get; set; }
            public string SelectedSliderLine { get; set; }
            public string BallPathSliderLine { get; set; }
        }
    }
}


using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Viewmodel
{
    public class HollowSliderVm : BindableBase
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

        private double timeCode = 10000;
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

        private double centerX = 256;
        public double CenterX
        {
            get => centerX;
            set { if (Set(ref centerX, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double centerY = 192;
        public double CenterY
        {
            get => centerY;
            set { if (Set(ref centerY, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private int selectedShapeIndex = 0; // 0 = Donut, 1 = Box/Square, 2 = Triangle, 3 = Hexagon
        public int SelectedShapeIndex
        {
            get => selectedShapeIndex;
            set { if (Set(ref selectedShapeIndex, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double outerRadius = 100;
        public double OuterRadius
        {
            get => outerRadius;
            set { if (Set(ref outerRadius, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double innerRadius = 45;
        public double InnerRadius
        {
            get => innerRadius;
            set { if (Set(ref innerRadius, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private double rotationAngle = 0;
        public double RotationAngle
        {
            get => rotationAngle;
            set { if (Set(ref rotationAngle, value)) { RegeneratePreview(); SaveSession(); } }
        }

        private InteropBitmap previewImage;
        public InteropBitmap PreviewImage
        {
            get => previewImage;
            set => Set(ref previewImage, value);
        }

        public CommandImplementation SelectBeatmapCommand { get; }

        public HollowSliderVm()
        {
            SelectBeatmapCommand = new CommandImplementation(_ => {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "osu! Beatmap (*.osu)|*.osu" };
                if (ofd.ShowDialog() == true) BeatmapPath = ofd.FileName;
            });

            LoadSession();
            RegeneratePreview();
        }

        public void RegeneratePreview()
        {
            int w = 512, h = 384;
            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(20, 20, 25)); // Nền playfield tối

                // Vẽ lưới toạ độ tâm
                using (Pen gridPen = new Pen(Color.FromArgb(40, 40, 50), 1f))
                {
                    g.DrawLine(gridPen, 0, 192, 512, 192);
                    g.DrawLine(gridPen, 256, 0, 256, 384);
                }

                int sides = SelectedShapeIndex switch {
                    1 => 4,  // Square
                    2 => 3,  // Triangle
                    3 => 6,  // Hexagon
                    _ => 36  // Circle / Donut
                };

                var ho = Classes.Tools.NegativeSpaceSlider.GenerateHollowPolygon(
                    new Vector2(CenterX, CenterY), OuterRadius, InnerRadius, sides, 0, RotationAngle);

                // Dùng GraphicsPath FillMode.Alternate để mô phỏng chính xác Stencil Buffer của osu!
                using (GraphicsPath path = new GraphicsPath(FillMode.Alternate))
                {
                    var pts = ho.GetAllCurvePoints();
                    PointF[] gdiPts = new PointF[pts.Count];
                    for (int i = 0; i < pts.Count; i++) gdiPts[i] = new PointF((float)pts[i].X, (float)pts[i].Y);

                    path.AddLines(gdiPts);

                    // Ruột slider màu skin mặc định
                    using (SolidBrush bodyBrush = new SolidBrush(Color.FromArgb(200, 60, 60, 75)))
                    using (Pen borderPen = new Pen(Color.FromArgb(0, 160, 255), 3f)) // Viền sáng xanh mô phỏng Selection
                    {
                        g.FillPath(bodyBrush, path);
                        g.DrawPath(borderPen, path);
                    }
                }
            }

            IntPtr hBmp = bmp.GetHbitmap();
            try {
                PreviewImage = (InteropBitmap)Imaging.CreateBitmapSourceFromHBitmap(
                    hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            } finally {
                DeleteObject(hBmp);
                bmp.Dispose();
            }
        }

        private void SaveSession()
        {
            try {
                string json = File.Exists(SessionFilePath) ? File.ReadAllText(SessionFilePath) : "{}";
                dynamic obj = JsonConvert.DeserializeObject(json) ?? new System.Dynamic.ExpandoObject();
                obj.BeatmapPath = this.BeatmapPath;
                File.WriteAllText(SessionFilePath, JsonConvert.SerializeObject(obj, Formatting.Indented));
            } catch { }
        }

        private void LoadSession()
        {
            try {
                if (File.Exists(SessionFilePath)) {
                    dynamic obj = JsonConvert.DeserializeObject(File.ReadAllText(SessionFilePath));
                    if (obj != null && obj.BeatmapPath != null) {
                        this.beatmapPath = (string)obj.BeatmapPath;
                        RaisePropertyChanged(nameof(BeatmapPath));
                    }
                }
            } catch { }
        }
    }
}
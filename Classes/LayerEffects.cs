using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Point = System.Windows.Point; // Chỉ định rõ Point thuộc System.Windows để tránh xung đột với System.Drawing

namespace StandalonePicturator.Classes
{
    public class ShapeCut
    {
        public string Kind { get; set; }
        public double Width { get; set; }
        public double Angle { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<Point> Points { get; set; } = new();

        public ShapeCut Copy() => new ShapeCut
        {
            Kind = this.Kind,
            Width = this.Width,
            Angle = this.Angle,
            X = this.X,
            Y = this.Y,
            Points = this.Points != null ? new List<Point>(this.Points) : new List<Point>()
        };
    }

    public class LayerEffects
    {
        public bool Layered { get; set; } = false;
        public bool Random { get; set; } = false;
        public double Angle { get; set; } = 0;
        public double? BodyAngle { get; set; }
        public double? BorderAngle { get; set; }
        public string RotationTarget { get; set; } = "Both";
        public double Spacing { get; set; } = 3.0;
        public int Tiers { get; set; } = 3;
        public double OuterDensity { get; set; } = 22.0;
        public double MiddleDensity { get; set; } = 60.0;
        public double Core { get; set; } = 45.0;
        public List<ShapeCut> Cuts { get; set; } = new();

        public LayerEffects Copy() => new LayerEffects
        {
            Layered = this.Layered,
            Random = this.Random,
            Angle = this.Angle,
            BodyAngle = this.BodyAngle,
            BorderAngle = this.BorderAngle,
            RotationTarget = this.RotationTarget,
            Spacing = this.Spacing,
            Tiers = this.Tiers,
            OuterDensity = this.OuterDensity,
            MiddleDensity = this.MiddleDensity,
            Core = this.Core,
            Cuts = this.Cuts.Select(c => c.Copy()).ToList()
        };

        private static int Hash(int seed, int a, int b = 0)
        {
            unchecked
            {
                int h = seed * 314159 + a * 37 + b * 101;
                h = (h ^ (h >> 13)) * 1274126177;
                return Math.Abs(h ^ (h >> 16));
            }
        }

        public void ApplyMask(Bitmap mask, double radius, bool layered, double amount, double frequency, int seed, double scaleFactor, int thickness = 3)
        {
            if (mask == null) return;

            if (layered && amount > 0)
            {
                AlignedScanlineGlitch.ApplyMask(mask, radius, this, amount, frequency, seed, scaleFactor, thickness);
            }

            if (Cuts != null && Cuts.Count > 0)
            {
                ApplyCutsToBitmap(mask);
            }
        }

        public void Apply(double[,] field, bool isGlitchOn, double amount, double frequency, int seed, double scaleFactor, int thickness = 3, double radius = 32)
        {
            if (field == null) return;
            int origW = field.GetLength(0);
            int origH = field.GetLength(1);
            if (origW == 0 || origH == 0) return;

            if (isGlitchOn && amount > 0)
            {
                AlignedScanlineGlitch.Apply(field, this, amount, frequency, seed, scaleFactor, thickness, radius);
            }

            if (Cuts != null && Cuts.Count > 0)
            {
                ApplyCutsToField(field);
            }
        }

        private void ApplyCutsToBitmap(Bitmap bmp)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            using (System.Drawing.Brush clearBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
            {
                int w = bmp.Width, h = bmp.Height;
                foreach (var cut in Cuts)
                {
                    float cy = (float)(cut.Y * h);
                    float cutW = (float)cut.Width;
                    if (cut.Kind == "Polygon" && cut.Points?.Count >= 3)
                    {
                        g.FillPolygon(clearBrush, cut.Points.Select(p => new System.Drawing.PointF((float)(p.X*w),(float)(p.Y*h))).ToArray());
                    }
                    else if (cut.Kind == "Band")
                    {
                        g.FillRectangle(clearBrush, 0, cy - cutW / 2f, w, cutW);
                    }
                    else if (cut.Kind == "Below")
                    {
                        g.FillRectangle(clearBrush, 0, cy, w, h - cy);
                    }
                    else if (cut.Kind == "Above")
                    {
                        g.FillRectangle(clearBrush, 0, 0, w, cy);
                    }
                }
            }
        }

        private void ApplyCutsToField(double[,] field)
        {
            int w = field.GetLength(0), h = field.GetLength(1);
            foreach (var cut in Cuts)
            {
                int cy = (int)(cut.Y * h);
                int cutW = (int)cut.Width;
                if (cut.Kind == "Polygon" && cut.Points?.Count >= 3)
                {
                    int x0=Math.Max(0,(int)Math.Floor(cut.Points.Min(p=>p.X)*w));
                    int x1=Math.Min(w-1,(int)Math.Ceiling(cut.Points.Max(p=>p.X)*w));
                    int y0=Math.Max(0,(int)Math.Floor(cut.Points.Min(p=>p.Y)*h));
                    int y1=Math.Min(h-1,(int)Math.Ceiling(cut.Points.Max(p=>p.Y)*h));
                    for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                        if(CutShapes.Contains(cut.Points,(x+.5)/w,(y+.5)/h)) field[x,y]=1.2;
                }
                else if (cut.Kind == "Band")
                {
                    for (int y = Math.Max(0, cy - cutW / 2); y <= Math.Min(h - 1, cy + cutW / 2); y++)
                        for (int x = 0; x < w; x++) field[x, y] = 1.2;
                }
                else if (cut.Kind == "Below")
                {
                    for (int y = Math.Max(0, cy); y < h; y++)
                        for (int x = 0; x < w; x++) field[x, y] = 1.2;
                }
                else if (cut.Kind == "Above")
                {
                    for (int y = 0; y <= Math.Min(h - 1, cy); y++)
                        for (int x = 0; x < w; x++) field[x, y] = 1.2;
                }
                else if (cut.Kind == "Freehand" && cut.Points != null && cut.Points.Count > 1)
                {
                    double radSq = (cut.Width / 2.0) * (cut.Width / 2.0);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            for (int p = 0; p < cut.Points.Count; p++)
                            {
                                double px = cut.Points[p].X * w;
                                double py = cut.Points[p].Y * h;
                                if ((x - px) * (x - px) + (y - py) * (y - py) <= radSq)
                                {
                                    field[x, y] = 1.2;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}





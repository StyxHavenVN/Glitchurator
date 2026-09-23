using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Point = System.Windows.Point; // Explicit alias to avoid ambiguity with System.Drawing.Point

namespace StandalonePicturator.Classes
{
    // Represents a geometric cut region or eraser stroke applied to the image/field
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

    // Configures scanline glitch effects, multi-tier slicing, and spatial erasure masks
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

        // Deterministic integer hash for procedural displacement
        private static int Hash(int seed, int a, int b = 0)
        {
            unchecked
            {
                int h = seed * 314159 + a * 37 + b * 101;
                h = (h ^ (h >> 13)) * 1274126177;
                return Math.Abs(h ^ (h >> 16));
            }
        }

        // Applies scanline glitch and geometric cutouts to a GDI+ bitmap mask
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

        // Applies scanline glitch and geometric cutouts to a 2D scalar/distance field
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

        // Renders cutouts directly onto a bitmap using black fill
        private void ApplyCutsToBitmap(Bitmap bmp)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            using (System.Drawing.Brush clearBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
            {
                int w = bmp.Width;
                int h = bmp.Height;

                foreach (var cut in Cuts)
                {
                    float cy = (float)(cut.Y * h);
                    float cutW = (float)cut.Width;

                    if (cut.Kind == "Polygon" && cut.Points?.Count >= 3)
                    {
                        var points = cut.Points
                            .Select(p => new System.Drawing.PointF((float)(p.X * w), (float)(p.Y * h)))
                            .ToArray();
                        g.FillPolygon(clearBrush, points);
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

        // Masks grid cells in the distance field by setting values outside threshold (1.2)
        private void ApplyCutsToField(double[,] field)
        {
            int w = field.GetLength(0);
            int h = field.GetLength(1);

            foreach (var cut in Cuts)
            {
                int cy = (int)(cut.Y * h);
                int cutW = (int)cut.Width;

                // Polygon cut: tests inside points using bounding-box optimization
                if (cut.Kind == "Polygon" && cut.Points?.Count >= 3)
                {
                    int x0 = Math.Max(0, (int)Math.Floor(cut.Points.Min(p => p.X) * w));
                    int x1 = Math.Min(w - 1, (int)Math.Ceiling(cut.Points.Max(p => p.X) * w));
                    int y0 = Math.Max(0, (int)Math.Floor(cut.Points.Min(p => p.Y) * h));
                    int y1 = Math.Min(h - 1, (int)Math.Ceiling(cut.Points.Max(p => p.Y) * h));

                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            if (CutShapes.Contains(cut.Points, (x + 0.5) / w, (y + 0.5) / h))
                            {
                                field[x, y] = 1.2;
                            }
                        }
                    }
                }
                // Horizontal band cut
                else if (cut.Kind == "Band")
                {
                    int minY = Math.Max(0, cy - cutW / 2);
                    int maxY = Math.Min(h - 1, cy + cutW / 2);

                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            field[x, y] = 1.2;
                        }
                    }
                }
                // Clears everything below horizontal line
                else if (cut.Kind == "Below")
                {
                    for (int y = Math.Max(0, cy); y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            field[x, y] = 1.2;
                        }
                    }
                }
                // Clears everything above horizontal line
                else if (cut.Kind == "Above")
                {
                    for (int y = 0; y <= Math.Min(h - 1, cy); y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            field[x, y] = 1.2;
                        }
                    }
                }
                // Freehand brush strokes using distance-to-point test
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
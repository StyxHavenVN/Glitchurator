using System;
using System.Collections.Generic;
using System.Linq;

namespace StandalonePicturator.Classes
{
    public enum BallGraphCurve
    {
        Linear = 0,
        Hold = 1,
        EaseIn = 2,
        EaseOut = 3,
        Smooth = 4,
        HalfSine = 5,
        Wave = 6,
        Parabola = 7,
        SingleCurve = 8,
        SingleCurve2 = 9,
        SingleCurve3 = 10,
        DoubleCurve = 11,
        DoubleCurve2 = 12,
        DoubleCurve3 = 13
    }

    public record BallGraphPoint
    {
        public double Time { get; set; }
        public double Position { get; set; }
        public BallGraphCurve Curve { get; set; }
        public double Curvature { get; set; } // Tham số độ cong / tần số sóng điều khiển bởi nốt nhỏ

        public BallGraphPoint() : this(0, 0) { }

        public BallGraphPoint(double time, double position, BallGraphCurve curve = BallGraphCurve.Linear, double curvature = 0)
        {
            Time = time;
            Position = position;
            Curve = curve;
            Curvature = curvature;
        }
    }

    public static class BallMotionGraph
    {
        public static BallGraphPoint[] Default() => new[]
        {
            new BallGraphPoint(0, 0, BallGraphCurve.Linear),
            new BallGraphPoint(1, 1, BallGraphCurve.Linear)
        };

        public static BallGraphPoint[] Normalize(IEnumerable<BallGraphPoint> source)
        {
            var raw = (source ?? Default())
                .Where(p => p != null && double.IsFinite(p.Time) && double.IsFinite(p.Position))
                .Select(p => new BallGraphPoint(
                    Math.Clamp(p.Time, 0, 1),
                    Math.Clamp(p.Position, 0, 1),
                    p.Curve,
                    Math.Clamp(p.Curvature, -1, 1)))
                .OrderBy(p => p.Time)
                .ToList();

            if (raw.Count == 0) return Default();

            if (raw[0].Time > 0) raw.Insert(0, new BallGraphPoint(0, raw[0].Position, raw[0].Curve, raw[0].Curvature));
            else raw[0] = new BallGraphPoint(0, raw[0].Position, raw[0].Curve, raw[0].Curvature);

            if (raw[^1].Time < 1) raw.Add(new BallGraphPoint(1, raw[^1].Position, BallGraphCurve.Linear, 0));
            else raw[^1] = new BallGraphPoint(1, raw[^1].Position, raw[^1].Curve, raw[^1].Curvature);

            return raw.ToArray();
        }

        // TÍNH TOÁN SỐ CHU KỲ SÓNG (WAVE CYCLES): Kéo xuống (c < 0) tăng sóng, kéo lên (c > 0) giảm sóng
        public static double GetWaveCycles(double curvature)
        {
            if (curvature <= 0)
            {
                // Từ 0 đến -1: Tăng từ 6 chu kỳ (Hình 3) lên tới 36 chu kỳ dày đặc (Hình 2)
                return 6.0 + (-curvature) * 30.0;
            }
            else
            {
                // Từ 0 đến 1: Giảm từ 6 chu kỳ xuống tối thiểu 1 chu kỳ
                return Math.Max(1.0, 6.0 - curvature * 5.0);
            }
        }

        public static double Interpolate(BallGraphPoint a, BallGraphPoint b, double u)
        {
            u = Math.Clamp(u, 0, 1);
            double p1 = a.Position;
            double p2 = b.Position;
            double diff = p2 - p1;
            double c = a.Curvature;

            switch (a.Curve)
            {
                case BallGraphCurve.Hold:
                    return (u >= 1.0) ? p2 : p1;

                // THUẬT TOÁN WAVE CHUẨN MAPPING TOOLS: BẢO TOÀN ĐỈNH/ĐÁY VÀ NỐI KHỚP ĐIỂM CUỐI
                case BallGraphCurve.Wave:
                {
                    int k = (int)Math.Round(GetWaveCycles(c));
                    int m = 2 * k + 1; // Số nửa chu kỳ lẻ đảm bảo f(0) == p1 và f(1) == p2
                    if (Math.Abs(diff) > 0.0001)
                    {
                        return p1 + diff * ((1.0 - Math.Cos(m * Math.PI * u)) / 2.0);
                    }
                    else
                    {
                        double amp = 0.25 * (1.0 + Math.Abs(c) * 0.5);
                        return Math.Clamp(p1 + amp * Math.Sin(2 * k * Math.PI * u), 0, 1);
                    }
                }

                case BallGraphCurve.Parabola:
                {
                    double bend = (c == 0 ? 0.35 : c * 1.2);
                    double height = Math.Max(Math.Abs(diff), 0.25);
                    return Math.Clamp(p1 + diff * u + 4.0 * bend * height * u * (1.0 - u), 0, 1);
                }

                case BallGraphCurve.SingleCurve:
                case BallGraphCurve.EaseIn:
                case BallGraphCurve.EaseOut:
                {
                    double p = 2.0;
                    if (c > 0) return p1 + diff * (1.0 - Math.Pow(1.0 - u, p + c * 3.0));
                    if (c < 0) return p1 + diff * Math.Pow(u, p - c * 3.0);
                    return a.Curve == BallGraphCurve.EaseOut
                        ? p1 + diff * (1.0 - Math.Pow(1.0 - u, 2.0))
                        : p1 + diff * Math.Pow(u, 2.0);
                }

                case BallGraphCurve.SingleCurve2:
                {
                    double p = 3.0;
                    if (c > 0) return p1 + diff * (1.0 - Math.Pow(1.0 - u, p + c * 4.0));
                    if (c < 0) return p1 + diff * Math.Pow(u, p - c * 4.0);
                    return p1 + diff * Math.Pow(u, p);
                }

                case BallGraphCurve.SingleCurve3:
                {
                    double p = 4.5;
                    if (c > 0) return p1 + diff * (1.0 - Math.Pow(1.0 - u, p + c * 5.0));
                    if (c < 0) return p1 + diff * Math.Pow(u, p - c * 5.0);
                    return p1 + diff * Math.Pow(u, p);
                }

                case BallGraphCurve.DoubleCurve:
                case BallGraphCurve.Smooth:
                {
                    double p = 2.0 + Math.Abs(c) * 2.0;
                    return p1 + diff * (u < 0.5 ? 0.5 * Math.Pow(2 * u, p) : 1.0 - 0.5 * Math.Pow(2 * (1 - u), p));
                }

                case BallGraphCurve.DoubleCurve2:
                {
                    double p = 3.0 + Math.Abs(c) * 3.0;
                    return p1 + diff * (u < 0.5 ? 0.5 * Math.Pow(2 * u, p) : 1.0 - 0.5 * Math.Pow(2 * (1 - u), p));
                }

                case BallGraphCurve.DoubleCurve3:
                {
                    double p = 4.5 + Math.Abs(c) * 4.0;
                    return p1 + diff * (u < 0.5 ? 0.5 * Math.Pow(2 * u, p) : 1.0 - 0.5 * Math.Pow(2 * (1 - u), p));
                }

                case BallGraphCurve.HalfSine:
                {
                    if (c > 0) return p1 + diff * (1.0 - Math.Cos(u * Math.PI / 2.0));
                    return p1 + diff * Math.Sin(u * Math.PI / 2.0);
                }

                default: // Linear
                {
                    if (Math.Abs(c) > 0.001)
                    {
                        return c > 0
                            ? p1 + diff * (1.0 - Math.Pow(1.0 - u, 1.0 + c * 3.0))
                            : p1 + diff * Math.Pow(u, 1.0 - c * 3.0);
                    }
                    return p1 + diff * u;
                }
            }
        }

        public static double Evaluate(IReadOnlyList<BallGraphPoint> points, double time)
        {
            if (points == null || points.Count == 0) return Math.Clamp(time, 0, 1);
            time = Math.Clamp(time, 0, 1);

            int right = 0;
            while (right < points.Count && points[right].Time < time) right++;

            if (right == 0) return points[0].Position;
            if (right >= points.Count) return points[^1].Position;

            var a = points[right - 1];
            var b = points[right];

            if (b.Time <= a.Time) return b.Position;
            double u = (time - a.Time) / (b.Time - a.Time);
            return Math.Clamp(Interpolate(a, b, u), 0, 1);
        }
    }
}
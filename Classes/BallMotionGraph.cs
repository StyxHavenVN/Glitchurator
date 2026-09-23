using System;
using System.Collections.Generic;
using System.Linq;

namespace StandalonePicturator.Classes
{
    // Curve types used for segment easing between keypoints
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

    // Motion timeline keyframe with normalized coordinates [0, 1]
    public record BallGraphPoint
    {
        public double Time { get; set; }
        public double Position { get; set; }
        public BallGraphCurve Curve { get; set; }
        public double Curvature { get; set; } // Shape/frequency modifier in range [-1, 1]

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
        // Default linear 0 -> 1 motion path
        public static BallGraphPoint[] Default() => new[]
        {
            new BallGraphPoint(0, 0, BallGraphCurve.Linear),
            new BallGraphPoint(1, 1, BallGraphCurve.Linear)
        };

        // Clamps, sorts by time, and ensures boundary points exist at Time = 0 and Time = 1
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

            // Anchor start point at Time = 0
            if (raw[0].Time > 0) raw.Insert(0, new BallGraphPoint(0, raw[0].Position, raw[0].Curve, raw[0].Curvature));
            else raw[0] = new BallGraphPoint(0, raw[0].Position, raw[0].Curve, raw[0].Curvature);

            // Anchor end point at Time = 1
            if (raw[^1].Time < 1) raw.Add(new BallGraphPoint(1, raw[^1].Position, BallGraphCurve.Linear, 0));
            else raw[^1] = new BallGraphPoint(1, raw[^1].Position, raw[^1].Curve, raw[^1].Curvature);

            return raw.ToArray();
        }

        // Maps curvature to oscillation frequency: negative values increase density (up to 36), positive values decrease it
        public static double GetWaveCycles(double curvature)
        {
            if (curvature <= 0)
            {
                return 6.0 + (-curvature) * 30.0; // [-1, 0] -> [36, 6] cycles
            }
            else
            {
                return Math.Max(1.0, 6.0 - curvature * 5.0); // (0, 1] -> [6, 1] cycles
            }
        }

        // Interpolates position between two points using local progress u in [0, 1]
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

                case BallGraphCurve.Wave:
                {
                    int k = (int)Math.Round(GetWaveCycles(c));
                    int m = 2 * k + 1; // Odd half-cycles guarantee matching start and end values
                    if (Math.Abs(diff) > 0.0001)
                    {
                        return p1 + diff * ((1.0 - Math.Cos(m * Math.PI * u)) / 2.0);
                    }
                    else
                    {
                        // Oscillation around a static baseline
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

                // Power-based single easings (quadratic, cubic, quartic)
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

                // Symmetric S-curves (EaseInOut)
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

                default: // Linear interpolation with optional curvature bias
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

        // Finds the active keyframe segment and evaluates position at global normalized time [0, 1]
        public static double Evaluate(IReadOnlyList<BallGraphPoint> points, double time)
        {
            if (points == null || points.Count == 0) return Math.Clamp(time, 0, 1);
            time = Math.Clamp(time, 0, 1);

            // Locate segment boundary
            int right = 0;
            while (right < points.Count && points[right].Time < time) right++;

            if (right == 0) return points[0].Position;
            if (right >= points.Count) return points[^1].Position;

            var a = points[right - 1];
            var b = points[right];

            if (b.Time <= a.Time) return b.Position;

            // Normalized progress within the segment
            double u = (time - a.Time) / (b.Time - a.Time);
            return Math.Clamp(Interpolate(a, b, u), 0, 1);
        }
    }
}
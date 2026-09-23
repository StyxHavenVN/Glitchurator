using System;
using System.Collections.Generic;
using System.Windows;

namespace StandalonePicturator.Classes;

public static class CutShapes
{
    // Generates a closed geometric polygon inside the bounding box defined by start and end points
    public static List<Point> Create(string kind, Point start, Point end)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);

        // Constrain to square if required
        if (kind == "Square")
        {
            w = h = Math.Min(w, h);
            x = end.X < start.X ? start.X - w : start.X;
            y = end.Y < start.Y ? start.Y - h : start.Y;
        }

        var result = new List<Point>();

        // Helper to map normalized UV coordinates [0, 1] into bounding box space
        void Add(double u, double v) => result.Add(new Point(x + u * w, y + v * h));

        switch (kind)
        {
            case "Triangle":
                Add(0.5, 0.0);
                Add(1.0, 1.0);
                Add(0.0, 1.0);
                break;

            // 5-pointed star with alternating outer and inner radii
            case "Star":
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 5;
                    double r = (i % 2 == 0) ? 0.5 : 0.22;
                    Add(0.5 + r * Math.Cos(a), 0.5 + r * Math.Sin(a));
                }
                break;

            // Parametric cardioid heart equation sampled at 96 steps
            case "Heart":
                for (int i = 0; i < 96; i++)
                {
                    double a = i * 2 * Math.PI / 96;
                    double heartX = 0.5 + Math.Pow(Math.Sin(a), 3) / 2.0;
                    double heartY = (13 - (13 * Math.Cos(a) - 5 * Math.Cos(2 * a) - 2 * Math.Cos(3 * a) - Math.Cos(4 * a))) / 30.0;
                    Add(heartX, heartY);
                }
                break;

            // Sampled ellipse path (64 vertices)
            case "Ellipse":
                for (int i = 0; i < 64; i++)
                {
                    double a = i * Math.PI / 32;
                    Add(0.5 + 0.5 * Math.Cos(a), 0.5 + 0.5 * Math.Sin(a));
                }
                break;

            // Default fallback: Rectangle
            default:
                Add(0.0, 0.0);
                Add(1.0, 0.0);
                Add(1.0, 1.0);
                Add(0.0, 1.0);
                break;
        }

        return result;
    }

    // Ray casting / Even-odd rule to test if a point (x, y) lies inside the polygon
    public static bool Contains(IReadOnlyList<Point> points, double x, double y)
    {
        if (points == null || points.Count < 3) return false;

        bool inside = false;

        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            var a = points[i];
            var b = points[j];

            // Toggle state if horizontal ray intersects current edge
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
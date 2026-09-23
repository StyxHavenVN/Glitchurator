using System;
using System.Drawing;
using StandalonePicturator.Classes.BeatmapHelper;

namespace StandalonePicturator.Classes;

// Generates procedural scanline, spike, and slice glitch distortions on slider distance fields
public sealed class NativeGlitchSnapshot : IDisposable
{
    private readonly Bitmap source;
    private readonly double amount;
    private readonly double frequency;
    private readonly int thickness;
    private readonly int seed;
    private readonly double resolution;
    private readonly LayerEffects effects;
    private readonly bool isGlitchOn;
    private readonly double angle;

    public NativeGlitchSnapshot(
        Bitmap source, 
        double amount, 
        double frequency, 
        int thickness, 
        int seed, 
        double resolution, 
        LayerEffects effects = null, 
        bool isGlitchOn = true, 
        double angle = 0)
    {
        this.source = source != null ? (Bitmap)source.Clone() : null;
        this.amount = amount;
        this.frequency = frequency;
        this.thickness = thickness;
        this.seed = seed;
        this.resolution = resolution;
        this.effects = effects?.Copy();
        this.isGlitchOn = isGlitchOn;
        this.angle = angle;
    }

    // Builds the 2D distance field and applies angle-oriented glitch distortion
    public double[,] Build(double nativeRadius, int quality)
    {
        if (source == null) return new double[0, 0];

        double glitchAngle = angle;
        bool rotate = Math.Abs(glitchAngle) >= 0.5 && (effects == null || !effects.Layered);

        // Rasterize base distance field from image
        var field = NativeSliderMask.Build(source, nativeRadius, quality, effects?.Layered != true);

        // 1. Multi-tier layered glitch mode
        if (effects != null && effects.Layered)
        {
            effects.Apply(field, true, amount, frequency, seed, (resolution - 16) / 480, thickness, nativeRadius);
        }
        // 2. Legacy procedural glitch with optional rotation
        else if (isGlitchOn)
        {
            if (rotate)
            {
                var rotField = RotateField(field, -glitchAngle);
                ApplyLegacyGlitch(rotField);
                field = UnrotateField(rotField, field.GetLength(0), field.GetLength(1), glitchAngle);
            }
            else
            {
                ApplyLegacyGlitch(field);
            }
        }

        // Apply geometric cutouts/erasures if defined
        if (effects != null && !effects.Layered && effects.Cuts.Count > 0)
        {
            effects.Apply(field, false, 0, 0, 0, 1);
        }

        return field;
    }

    // Applies spikes, horizontal slits, and slice displacements to the scalar field
    private void ApplyLegacyGlitch(double[,] field)
    {
        int w = field.GetLength(0);
        int h = field.GetLength(1);
        if (w == 0 || h == 0 || amount <= 0 || frequency <= 0) return;

        double scaleFactor = (resolution - 16.0) / 480.0;
        int maxDisplacement = (int)Math.Max(5, amount * scaleFactor);
        Random rand = new Random(seed);

        // 1. Edge Spikes: Extends horizontal protrusions outward from slider edges
        for (int y = 0; y < h; y++)
        {
            if (rand.NextDouble() * 100.0 < frequency * 0.45)
            {
                int xMin = -1, xMax = -1;
                for (int x = 0; x < w; x++)
                {
                    if (field[x, y] <= 1.0)
                    {
                        if (xMin == -1) xMin = x;
                        xMax = x;
                    }
                }

                if (xMin != -1 && xMax != -1)
                {
                    int spikeH = rand.Next(1, Math.Max(2, thickness + 1));

                    // Left-side spike
                    if (rand.NextDouble() < 0.70)
                    {
                        int spikeLen = rand.Next((int)(maxDisplacement * 0.35), maxDisplacement + 1);
                        int targetX = Math.Max(0, xMin - spikeLen);
                        for (int sy = y; sy < Math.Min(h, y + spikeH); sy++)
                        {
                            for (int sx = targetX; sx <= xMin; sx++)
                            {
                                field[sx, sy] = 0.0;
                            }
                        }
                    }

                    // Right-side spike
                    if (rand.NextDouble() < 0.70)
                    {
                        int spikeLen = rand.Next((int)(maxDisplacement * 0.35), maxDisplacement + 1);
                        int targetX = Math.Min(w - 1, xMax + spikeLen);
                        for (int sy = y; sy < Math.Min(h, y + spikeH); sy++)
                        {
                            for (int sx = xMax; sx <= targetX; sx++)
                            {
                                field[sx, sy] = 0.0;
                            }
                        }
                    }
                }
            }
        }

        // 2. Cutout Slits: Carves out horizontal negative spaces inside the slider body
        for (int y = 0; y < h; y++)
        {
            if (rand.NextDouble() * 100.0 < frequency * 0.20)
            {
                int xMin = -1, xMax = -1;
                for (int x = 0; x < w; x++)
                {
                    if (field[x, y] <= 1.0)
                    {
                        if (xMin == -1) xMin = x;
                        xMax = x;
                    }
                }

                if (xMin != -1 && xMax - xMin > 30)
                {
                    int slitLen = rand.Next(15, Math.Min(80, (xMax - xMin) / 2));
                    int slitX = rand.Next(xMin + 5, xMax - slitLen - 5);
                    int slitH = rand.Next(1, Math.Max(2, thickness));

                    for (int sy = y; sy < Math.Min(h, y + slitH); sy++)
                    {
                        for (int sx = slitX; sx <= slitX + slitLen; sx++)
                        {
                            field[sx, sy] = 1.2;
                        }
                    }
                }
            }
        }

        // 3. Slice Displacement: Randomly shifts horizontal scanline blocks left or right
        double[,] copy = (double[,])field.Clone();
        int curY = 0;

        while (curY < h)
        {
            if (rand.NextDouble() * 100.0 < frequency * 0.30)
            {
                int sliceH = rand.Next(1, Math.Max(2, thickness * 2));
                int dx = rand.Next(-maxDisplacement / 2, maxDisplacement / 2 + 1);

                for (int sy = curY; sy < Math.Min(h, curY + sliceH); sy++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int srcX = x - dx;
                        field[x, sy] = (srcX >= 0 && srcX < w) ? copy[srcX, sy] : 1.2;
                    }
                }

                curY += sliceH;
            }
            else
            {
                curY++;
            }
        }
    }

    // Rotates the 2D field into an expanded bounding grid to allow arbitrary glitch angles
    private static double[,] RotateField(double[,] src, double angle)
    {
        int origW = src.GetLength(0);
        int origH = src.GetLength(1);
        int diag = (int)Math.Ceiling(Math.Sqrt(origW * origW + origH * origH)) + 8;
        double[,] rot = new double[diag, diag];

        for (int y = 0; y < diag; y++)
        {
            for (int x = 0; x < diag; x++)
            {
                rot[x, y] = 1.2;
            }
        }

        double rad = -angle * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        for (int y = 0; y < diag; y++)
        {
            for (int x = 0; x < diag; x++)
            {
                double dx = x - diag / 2.0;
                double dy = y - diag / 2.0;
                int srcX = (int)Math.Round(origW / 2.0 + dx * cos - dy * sin);
                int srcY = (int)Math.Round(origH / 2.0 + dx * sin + dy * cos);

                if (srcX >= 0 && srcX < origW && srcY >= 0 && srcY < origH)
                {
                    rot[x, y] = src[srcX, srcY];
                }
            }
        }

        return rot;
    }

    // Projects the rotated and glitched field back to its original dimensions
    private static double[,] UnrotateField(double[,] rot, int origW, int origH, double angle)
    {
        int diag = rot.GetLength(0);
        double[,] res = new double[origW, origH];
        double rad = angle * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        for (int y = 0; y < origH; y++)
        {
            for (int x = 0; x < origW; x++)
            {
                double dx = x - origW / 2.0;
                double dy = y - origH / 2.0;
                int srcX = (int)Math.Round(diag / 2.0 + dx * cos - dy * sin);
                int srcY = (int)Math.Round(diag / 2.0 + dx * sin + dy * cos);

                if (srcX >= 0 && srcX < diag && srcY >= 0 && srcY < diag)
                {
                    res[x, y] = rot[srcX, srcY];
                }
                else
                {
                    res[x, y] = 1.2;
                }
            }
        }

        return res;
    }

    public void Dispose()
    {
        source?.Dispose();
    }
}
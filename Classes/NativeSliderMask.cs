using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace StandalonePicturator.Classes;

/// <summary>Converts a silhouette to radial samples of osu!'s native slider shader.</summary>
public static class NativeSliderMask
{
    public static unsafe double[,] Build(Bitmap mask, double radiusPixels, int quality, bool quantize = true)
    {
        if (!double.IsFinite(radiusPixels) || radiusPixels <= 0) throw new ArgumentOutOfRangeException(nameof(radiusPixels));
        int w = mask.Width, h = mask.Height;
        // An explicit empty border also handles opaque images touching their bitmap edges.
        int width = w + 2, height = h + 2;
        var distance = new double[width, height];
        var data = mask.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try {
            for (int y = 0; y < h; y++) {
                var row = (uint*)((byte*)data.Scan0 + y * data.Stride);
                for (int x = 0; x < w; x++) {
                    uint p = row[x];
                    uint brightness = Math.Max((p >> 16) & 255, Math.Max((p >> 8) & 255, p & 255));
                    distance[x + 1, y + 1] = brightness * (p >> 24) >= 128 * 255 ? 1e12 : 0;
                }
            }
        } finally { mask.UnlockBits(data); }
        int size = Math.Max(width, height);
        var input = new double[size]; var output = new double[size];
        var sites = new int[size]; var cuts = new double[size + 1];
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) input[y] = distance[x, y];
            Transform(input, output, height, sites, cuts);
            for (int y = 0; y < height; y++) distance[x, y] = output[y];
        }
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) input[x] = distance[x, y];
            Transform(input, output, width, sites, cuts);
            for (int x = 0; x < width; x++) distance[x, y] = output[x];
        }
        var result = new double[w, h];
        quality = Math.Clamp(quality, 1, 101);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            double d = distance[x + 1, y + 1];
            if (d == 0) { result[x, y] = 1.2; continue; }
            double radial = Math.Clamp(1 - (Math.Sqrt(d) - .5) / radiusPixels, 0, 1);
            result[x, y] = quantize ? Math.Round(radial * quality) / quality : radial;
        }
        return result;
    }

    // Exact squared Euclidean distance transform, separable in X and Y, O(width*height).
    private static void Transform(double[] input, double[] output, int count, int[] sites, double[] cuts)
    {
        int k = 0; sites[0] = 0; cuts[0] = double.NegativeInfinity; cuts[1] = double.PositiveInfinity;
        for (int q = 1; q < count; q++) {
            double intersection;
            while (true) {
                int v = sites[k];
                intersection = ((input[q] + (double)q * q) - (input[v] + (double)v * v)) / (2 * (q - v));
                if (intersection > cuts[k] || k == 0) break;
                k--;
            }
            sites[++k] = q; cuts[k] = intersection; cuts[k + 1] = double.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < count; q++) {
            while (cuts[k + 1] < q) k++;
            double delta = q - sites[k]; output[q] = delta * delta + input[sites[k]];
        }
    }

    public static Color PreviewColour(double radial)
    {
        if (radial > 1) return Color.Transparent;
        // Neutral approximation only; actual colour and selection glow come from osu!/the skin.
        if (radial >= .81) {
            double opacity = radial > .94 ? Math.Clamp((1 - radial) / .06, 0, 1) : 1;
            return Color.FromArgb((int)(220 * opacity), 205, 214, 224);
        }
        int shade = (int)(30 + 35 * Math.Pow(radial / .81, 1.6));
        return Color.FromArgb(205, shade, shade + 3, shade + 12);
    }
}


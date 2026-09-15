using System;
using System.Drawing;
using StandalonePicturator.Viewmodel;

namespace StandalonePicturator.Classes;

/// <summary>Applies legacy horizontal tearing to existing shading, without rebuilding its edges.</summary>
public sealed class NativeGlitchSnapshot : IDisposable
{
    private readonly Bitmap source;
    private readonly double amount, frequency, resolution;
    private readonly int thickness, seed;

    public NativeGlitchSnapshot(Bitmap source, double amount, double frequency, int thickness, int seed, double resolution)
    {
        this.source = (Bitmap)source.Clone();
        this.amount = amount; this.frequency = frequency; this.thickness = thickness;
        this.seed = seed; this.resolution = resolution;
    }

    public double[,] Build(double radius, int quality)
    {
        var field = NativeSliderMask.Build(source, radius, quality);
        if (amount <= 0 || frequency <= 0) return field;
        quality = Math.Clamp(quality, 1, 101);
        using var encoded = new Bitmap(source.Width, source.Height);
        // Red is occupancy for the original glitch algorithm. Green stores the exact
        // quantized shader sample. White spikes become body samples; black cuts stay empty.
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                encoded.SetPixel(x, y, field[x, y] > 1 ? Color.Black :
                    Color.FromArgb(255, (int)Math.Round(field[x, y] * quality), 0));
        SliderPicturatorVm.ApplyGlitch(encoded, amount, frequency, thickness, seed, resolution);
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++) {
                var pixel = encoded.GetPixel(x, y);
                field[x, y] = pixel.R <= 100 ? 1.2 : pixel.G == 255 ? 0 : (double)pixel.G / quality;
            }
        return field;
    }

    public void Dispose() => source.Dispose();
}

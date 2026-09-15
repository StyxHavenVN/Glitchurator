using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using StandalonePicturator.Classes;
using StandalonePicturator.Viewmodel;
using StandalonePicturator.Classes.Tools.SlideratorStuff;

internal static class GlitchChecks
{
    public static void Run()
    {
        using var mask = new Bitmap(360, 240);
        using (var g = Graphics.FromImage(mask)) {
            g.Clear(Color.Black);
            using var pen = new Pen(Color.White, 64) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, 80, 70, 270, 170);
        }
        using var legacy = (Bitmap)mask.Clone();
        SliderPicturatorVm.ApplyGlitch(legacy, 43, 45, 3, 1337, 496);
        var clean = NativeSliderMask.Build(mask, 32, 101);
        using var snapshot = new NativeGlitchSnapshot(mask, 43, 45, 3, 1337, 496);
        var field = snapshot.Build(32, 101);
        var repeat = snapshot.Build(32, 101);
        using var disabled = new NativeGlitchSnapshot(mask, 43, 0, 3, 1337, 496);
        var off = disabled.Build(32, 101);
        int spikes = 0, cuts = 0, unchanged = 0;
        for (int y = 0; y < mask.Height; y++) for (int x = 0; x < mask.Width; x++) {
            Require((field[x, y] <= 1) == (legacy.GetPixel(x, y).R > 100), "Legacy silhouette differs");
            Require(field[x, y] == repeat[x, y], "Seed must be deterministic");
            Require(off[x, y] == clean[x, y], "Disabled glitch must preserve native samples exactly");
            if (field[x, y] <= 1 && clean[x, y] > 1) spikes++;
            if (field[x, y] > 1 && clean[x, y] <= 1) cuts++;
            if (field[x, y] == clean[x, y] && clean[x, y] <= 1) unchanged++;
        }
        Require(spikes > 0 && cuts > 0 && unchanged > 2000, "Need spikes, cuts and preserved body");
        // Every output shader value is transported from this row (or a legacy white spike).
        // Recomputing a distance field around cuts would introduce additional border values.
        using var slit = new Bitmap(180, 100);
        using (var g = Graphics.FromImage(slit)) { g.Clear(Color.Black); g.FillRectangle(Brushes.White, 30, 20, 120, 60); }
        var baseField = NativeSliderMask.Build(slit, 20, 101);
        using var tear = new NativeGlitchSnapshot(slit, 25, 80, 1, 42, 496);
        var torn = tear.Build(20, 101);
        for (int y = 0; y < slit.Height; y++) {
            var values = Enumerable.Range(0, slit.Width).Select(x => baseField[x, y]).Append(0).Append(1.2).ToHashSet();
            for (int x = 0; x < slit.Width; x++) Require(values.Contains(torn[x, y]), "Tears must not generate new border shading");
        }
        var rendered = SliderPicturator.Recolor(legacy, Color.White, Color.White, Color.Black,
            blackOff: true, borderOff: true, opaqueOff: true, nativeRadiusPixels: 32, nativeFieldOverride: field);
        using var preview = rendered.Item1;
        for (int y = 0; y < mask.Height; y++) for (int x = 0; x < mask.Width; x++)
            Require(preview.GetPixel(x, y).ToArgb() == NativeSliderMask.PreviewColour(field[x, y]).ToArgb(), "Preview must consume shared field");
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "glitch-restored.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var comparison = new Bitmap(720, 240);
        for (int y = 0; y < 240; y++) for (int x = 0; x < 360; x++) {
            comparison.SetPixel(x, y, NativeSliderMask.PreviewColour(clean[x, y]));
            comparison.SetPixel(x + 360, y, NativeSliderMask.PreviewColour(field[x, y]));
        }
        comparison.Save(path);
        Console.WriteLine($"Glitch checks PASS: {spikes} spike pixels, {cuts} cut pixels, {unchanged} unchanged body pixels. {path}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

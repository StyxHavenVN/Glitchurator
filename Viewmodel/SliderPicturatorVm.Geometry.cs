using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Viewmodel;

public partial class SliderPicturatorVm
{
    // Keep the uncut silhouette so glitch does not introduce new native shader borders.
    private bool autoOsuResolution = true;
    public bool AutoOsuResolution {
        get => autoOsuResolution;
        set { if (Set(ref autoOsuResolution, value)) { if (value) SyncOsuResolution(); SaveSession(); } }
    }
    public void SyncOsuResolution()
    {
        if (!AutoOsuResolution) return;
        var height = StandalonePicturator.Classes.OsuRenderResolution.Read(BeatmapPath);
        if (height.HasValue) YResolution = height.Value;
    }

    private double ballPathScale = 1;
    public double BallPathScale {
        get => ballPathScale;
        set { if (double.IsFinite(value) && Set(ref ballPathScale, Math.Clamp(value, .1, 5))) SaveSession(); }
    }
    public void ResetBallPlacement()
    {
        BallPathSlider = null;
        BallOffsetX = 0; BallOffsetY = 0; BallPathScale = 1;
        HasSliderBall = true;
        SyncOsuResolution();
    }
    private Bitmap cleanGlitchSource;

    private void SetPictureMask(Bitmap mask)
    {
        Bitmap clean = IsGlitchOn ? (Bitmap)mask.Clone() : null;
        try {
            if (IsGlitchOn) ApplyGlitch(mask, GlitchAmount, GlitchFrequency, GlitchThickness, GlitchSeed, YResolution);
            Bm = mask;
            cleanGlitchSource = clean;
        } catch { clean?.Dispose(); throw; }
    }

    public StandalonePicturator.Classes.NativeGlitchSnapshot CaptureNativeGlitch() =>
        NativeSliderShading && IsGlitchOn && cleanGlitchSource != null
            ? new StandalonePicturator.Classes.NativeGlitchSnapshot(cleanGlitchSource,
                GlitchAmount, GlitchFrequency, GlitchThickness, GlitchSeed, YResolution)
            : null;

    private bool nativeSliderShading = true;
    public bool NativeSliderShading {
        get => nativeSliderShading;
        set { if (Set(ref nativeSliderShading, value)) { RegeneratePreview(); SaveSession(); } }
    }
    private bool loadingSession;
    private double ballOffsetX;
    private double ballOffsetY;

    public double BallOffsetX {
        get => ballOffsetX;
        set { if (double.IsFinite(value) && Set(ref ballOffsetX, value)) SaveSession(); }
    }
    public double BallOffsetY {
        get => ballOffsetY;
        set { if (double.IsFinite(value) && Set(ref ballOffsetY, value)) SaveSession(); }
    }

    // Move both origins in one transaction, so changing either position keeps alignment.
    private void SetPlacement(double x, double y, bool sliderOrigin)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        double dx = x - (sliderOrigin ? sliderStartX : imageStartX);
        double dy = y - (sliderOrigin ? sliderStartY : imageStartY);
        if (dx == 0 && dy == 0) return;
        sliderStartX += dx;
        sliderStartY += dy;
        imageStartX += dx;
        imageStartY += dy;
        RaisePropertyChanged(nameof(SliderStartX));
        RaisePropertyChanged(nameof(SliderStartY));
        RaisePropertyChanged(nameof(ImageStartX));
        RaisePropertyChanged(nameof(ImageStartY));
        SaveSession();
    }

    public void MovePicture(double dx, double dy) => SetPlacement(ImageStartX + dx, ImageStartY + dy, false);

    private void RefreshSource()
    {
        if (SelectedSlider != null) { RasterizeSliderToImage(SelectedSlider); return; }
        if (!File.Exists(PictureFile)) return;
        using var original = new Bitmap(PictureFile);
        using var clean = CleanImageBackground(original);
        int width = Math.Max(1, (int)Math.Round(original.Width * SliderScale));
        int height = Math.Max(1, (int)Math.Round(original.Height * SliderScale));
        var resized = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(resized)) graphics.DrawImage(clean, 0, 0, width, height);
        SetPictureMask(resized);
        RegeneratePreview();
    }

    // This affects only the WPF preview. Export still scans the original white-on-black mask.
    private static unsafe void ClearPreviewBackground(Bitmap preview, Bitmap mask)
    {
        var rectangle = new System.Drawing.Rectangle(0, 0, mask.Width, mask.Height);
        var pixels = preview.LockBits(rectangle, System.Drawing.Imaging.ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var source = mask.LockBits(rectangle, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try {
            for (int y = 0; y < mask.Height; y++) {
                var output = (uint*)((byte*)pixels.Scan0 + y * pixels.Stride);
                var input = (uint*)((byte*)source.Scan0 + y * source.Stride);
                for (int x = 0; x < mask.Width; x++) {
                    uint brightness = Math.Max((input[x] >> 16) & 255, Math.Max((input[x] >> 8) & 255, input[x] & 255));
                    uint alpha = Math.Min(brightness, input[x] >> 24);
                    output[x] = (output[x] & 0x00ffffff) | (alpha << 24);
                }
            }
        } finally {
            preview.UnlockBits(pixels);
            mask.UnlockBits(source);
        }
    }
    public HitObject CreateBallSlider()
    {
        if (!HasSliderBall) return null;

        if (ChainAllVisibleBallPaths) {
            var first = VisibleLayers.Where(l => l.HasSliderBall).Select(l => l.CreateSingleBallSlider()).FirstOrDefault(s => s != null);
            if (first != null) { first.Time = TimeCode; first.TemporalLength = Duration; return first; }
        }
        return CreateSingleBallSlider();
    }

    public HitObject CreateSingleBallSlider()
    {
        if (!HasSliderBall) return null;
        var source = BallPathSlider ?? SelectedSlider;
        if (source == null) return null;
        var points = source.GetSliderPath().CalculatedPath.Select(p => new Vector2(
            SliderStartX + BallOffsetX + (p.X - source.Pos.X) * SliderScale * BallPathScale,
            SliderStartY + BallOffsetY + (p.Y - source.Pos.Y) * SliderScale * BallPathScale)).ToList();
        if (points.Count < 2) return null;

        var repeated = new List<Vector2>(points);
        for (int repeat = 1; repeat < (BallGraphEnabled ? 1 : Math.Max(1, source.Repeat)); repeat++) {
            var span = repeat % 2 == 1 ? points.AsEnumerable().Reverse() : points;
            repeated.AddRange(span.Skip(1));
        }
        var slider = source.DeepCopy();
        slider.IsCircle = false;
        slider.IsSlider = true;
        slider.IsSpinner = false;
        slider.IsHoldNote = false;
        slider.Repeat = 1;
        slider.SliderType = PathType.Linear;
        slider.SetAllCurvePoints(repeated);
        slider.PixelLength = 0;
        for (int i = 1; i < repeated.Count; i++) slider.PixelLength += (repeated[i] - repeated[i - 1]).Length;
        slider.TemporalLength = Duration;
        slider.Time = TimeCode;
        return slider;
    }

    private string previewRadiusMap;
    private double? previewMapCs;

    public double GetPreviewBallRadius()
    {
        if (previewRadiusMap != BeatmapPath) {
            previewRadiusMap = BeatmapPath;
            previewMapCs = null;
            if (File.Exists(BeatmapPath)) {
                try {
                    var map = new BeatmapEditor(BeatmapPath).Beatmap;
                    if (map.Difficulty.TryGetValue("CircleSize", out var cs)) previewMapCs = cs.DoubleValue;
                } catch { }
            }
        }
        // TargetCS controls the drawn mask's thickness. Native ball size comes from the map.
        return Beatmap.GetHitObjectRadius(previewMapCs ?? TargetCS);
    }
    [JsonIgnore]
    public System.Windows.Rect PreviewImageBounds {
        get {
            double factor = (YResolution - 16.0) / 480.0;
            // Match Picturate's rounding and editor-to-gameplay sample origin.
            double x = -104 + Math.Round((Math.Round(ImageStartX) + 104) * factor) / factor;
            double y = -52 + Math.Round((Math.Round(ImageStartY) + 52) * factor) / factor;
            return new System.Windows.Rect(x, y, (Bm?.Width ?? 0) / factor, (Bm?.Height ?? 0) / factor);
        }
    }
}
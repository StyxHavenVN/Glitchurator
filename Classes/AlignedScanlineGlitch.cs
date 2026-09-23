using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace StandalonePicturator.Classes;

// Both the silhouette preview and native export use this immutable-source scan.
internal static class AlignedScanlineGlitch
{
    // Render body and border at their own angles; source-based ownership keeps the other part stable.
    public static void Apply(double[,] field, LayerEffects settings, double amount, double frequency,
        int seed, double scale, int thickness, double radius)
    {
        double bodyAngle = settings.BodyAngle ?? settings.Angle;
        double borderAngle = settings.BorderAngle ?? settings.Angle;
        var bodySettings = settings.Copy();
        bodySettings.Angle = bodyAngle;
        var body = (double[,])field.Clone();
        var border = (double[,])field.Clone();
        ApplySingle(body,bodySettings,amount,frequency,seed,scale,thickness,radius);
        var borderSettings = settings.Copy();
        borderSettings.Angle = borderAngle;
        ApplySingle(border,borderSettings,amount,frequency,seed,scale,thickness,radius);
        // Ownership is determined from the unchanged source silhouette. A different
        // angle cannot reclassify pixels or regenerate the other component's pattern.
        double boundary = Math.Max(4*scale,radius*(1-Math.Clamp(settings.Core/100,.15,.8)));
        for(int y=0;y<field.GetLength(1);y++) for(int x=0;x<field.GetLength(0);x++) {
            bool isBody = field[x,y]<=1 && (1-field[x,y])*radius+.5 >= boundary;
            field[x,y] = isBody ? body[x,y] : border[x,y];
        }
    }

    // Build aligned radial contours, shade the body, then apply bounded scanline displacement.
    private static void ApplySingle(double[,] field, LayerEffects settings, double amount, double frequency,
        int seed, double scale, int thickness, double radius)
    {        if (amount <= 0 || frequency <= 0 || scale <= 0) return;
        int w = field.GetLength(0), h = field.GetLength(1);
        double angle = settings.Angle * Math.PI / 180;
        double c = Math.Cos(angle), s = Math.Sin(angle);
        int rw = (int)Math.Ceiling(Math.Abs(w * c) + Math.Abs(h * s)) + 4;
        int rh = (int)Math.Ceiling(Math.Abs(w * s) + Math.Abs(h * c)) + 4;
        var source = new double[rw, rh];
        for (int y = 0; y < rh; y++) for (int x = 0; x < rw; x++) {
            int sx = (int)Math.Round((x - rw / 2.0) * c - (y - rh / 2.0) * s + w / 2.0);
            int sy = (int)Math.Round((x - rw / 2.0) * s + (y - rh / 2.0) * c + h / 2.0);
            source[x,y] = sx >= 0 && sx < w && sy >= 0 && sy < h ? field[sx,sy] : 1.2;
        }
        var output = (double[,])source.Clone();
        // Pixel-width contours are built before quality quantisation, which erased thin bands.
        // .86 uses the solid native border, rather than the fading fringe at .955.
        double stroke = Math.Max(1.5, Math.Min(4, thickness) * scale);
        double pitch = Math.Max(stroke + Math.Max(1, scale), settings.Spacing * scale + stroke);
        double borderDepth = Math.Max(4 * scale, radius * (1 - Math.Clamp(settings.Core / 100, .15, .8)));
        int tiers = Math.Clamp(settings.Tiers, 1, Math.Max(1,(int)(borderDepth / (2.5 * scale))));
        double tierPitch = borderDepth / tiers;
        double contourWidth = Math.Max(1.25 * scale, tierPitch * .52);
        double displacement = Math.Min(tierPitch * .2, amount * scale * .025);
        for (int y = 0; y < rh; y++) {
            int row = (int)Math.Floor(y / pitch);
            for (int x = 0; x < rw; x++) {
                double radial = source[x,y];
                if (radial > 1) continue;
                double depth = (1 - radial) * radius + .5;
                if (depth >= borderDepth) {
                    double body = Math.Clamp((depth-borderDepth) / Math.Max(1,radius-borderDepth),0,1);
                    double scan = .5 + .5 * Math.Cos(2*Math.PI*y/Math.Max(2,2*scale));
                    output[x,y] = .12 + .48 * (1-body) + .07 * scan;
                    continue;
                }
                int tier = Math.Min(tiers-1,(int)(depth/tierPitch));
                double densitySetting = tier == 0 ? settings.OuterDensity : settings.MiddleDensity;
                double density = densitySetting <= 0 ? 0 : (.55+.45*Math.Clamp(densitySetting/100,0,1))*Math.Clamp(frequency/100,0,1);
                double hash = Noise(seed,row,tier);
                bool present = settings.Random ? hash < density :
                    Math.Floor((row+1+tier)*density)>Math.Floor((row+tier)*density);
                double phase = (y + tier * pitch * .22 + (settings.Random ? hash*pitch*.35 : 0)) % pitch;
                double center = (tier+.5)*tierPitch + (settings.Random ? (hash-.5)*displacement : 0);
                bool contour = Math.Abs(depth-center) <= contourWidth*.5;
                output[x,y] = contour && present && phase < stroke ? .86 : 1.2;
            }
        }
        // Slice the complete shaded slider, not just its contour. Read from a frozen
        // layer so shifting a row never smears its own previously written pixels.
        var shaded = (double[,])output.Clone();
        double overall = Math.Clamp(frequency / 100, 0, 1);
        double solid = Math.Clamp(settings.Core / 100, 0, 1);
        int maximumShift = Math.Max(1, (int)Math.Round(Math.Min(amount * scale * .04, radius * .08)));
        for (int y = 0; y < rh; y++) {
            int row = (int)Math.Floor(y / pitch);
            double phase = y - row * pitch;
            double noise = settings.Random ? Noise(seed,row,91) : (row % 7) / 6.0;
            bool active = settings.Random ? Noise(seed,row,92) < overall :
                Math.Floor((row+1)*overall) > Math.Floor(row*overall);
            if (!active) continue;
            int shift = (int)Math.Round((noise*2-1)*maximumShift);
            bool slit = phase >= stroke && phase < stroke + Math.Max(1, scale);
            for (int x = 0; x < rw; x++) {
                int sx = x-shift;
                double value = sx >= 0 && sx < rw ? shaded[sx,y] : 1.2;
                double original = sx >= 0 && sx < rw ? source[sx,y] : 1.2;
                if (original <= 1 && original < .8 && value < .81) {
                    // Fine interrupted cuts cross the body too. Core controls their
                    // length/density instead of exempting the entire middle from glitch.
                    double segment = (x + row*17) % Math.Max(12, radius*1.6);
                    if (slit && segment < Math.Max(2*scale, radius*(1-solid)*.65))
                        value = 1.2;
                    else if (value <= 1)
                        value = Math.Clamp(value + (noise-.5)*.23, .04, .78);
                }
                // Never drag transparent gaps or another contour across the solid body.
                bool bodyPixel = source[x,y] <= 1 && (1-source[x,y])*radius+.5 >= borderDepth;
                if (bodyPixel && value > 1 && !slit) value = shaded[x,y];
                output[x,y] = source[x,y] > 1 ? 1.2 : value;
            }
        }
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {

            int rx = (int)Math.Round((x-w/2.0)*c + (y-h/2.0)*s + rw/2.0);
            int ry = (int)Math.Round(-(x-w/2.0)*s + (y-h/2.0)*c + rh/2.0);
            // Copy only changed samples: rotation must not resample untouched native shading.
            if (rx >= 0 && rx < rw && ry >= 0 && ry < rh && output[rx,ry] != source[rx,ry])
                field[x,y] = output[rx,ry];
        }
    }

    private static double Noise(int seed, int row, int tier)
    {
        unchecked {
            uint h = (uint)(seed ^ row * 73856093 ^ tier * 19349663);
            h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15;
            return (h & 0xffffff) / 16777216.0;
        }
    }

    // Reuse the export shader algorithm to produce the matching binary silhouette mask.
    public static unsafe void ApplyMask(Bitmap mask, double radius, LayerEffects settings,
        double amount, double frequency, int seed, double scale, int thickness)
    {
        var field = NativeSliderMask.Build(mask, Math.Max(1, radius), 101, false);
        Apply(field, settings, amount, frequency, seed, scale, thickness, radius);
        var data = mask.LockBits(new Rectangle(0,0,mask.Width,mask.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try {
            for (int y=0; y<mask.Height; y++) {
                uint* row = (uint*)((byte*)data.Scan0 + y*data.Stride);
                for (int x=0; x<mask.Width; x++) row[x] = field[x,y] <= 1 ? 0xffffffff : 0xff000000;
            }
        } finally { mask.UnlockBits(data); }
    }
}










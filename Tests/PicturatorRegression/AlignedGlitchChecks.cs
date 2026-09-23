using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using StandalonePicturator.Classes;

internal static class AlignedGlitchChecks
{
    public static void Run()
    {
        using var mask = new Bitmap(420,300);
        using (var g = Graphics.FromImage(mask)) {
            g.Clear(Color.Black);
            using var pen = new Pen(Color.White,65) { StartCap=LineCap.Round, EndCap=LineCap.Round, LineJoin=LineJoin.Round };
            g.DrawLines(pen,new Point[] {new(95,235),new(140,100),new(300,75),new(340,230)});
        }
        var clean = NativeSliderMask.Build(mask,32.5,101,false);
        var settings = new LayerEffects {Layered=true,Spacing=4,Tiers=4,OuterDensity=85,MiddleDensity=100,Core=45};
        double[,] Make(double frequency=90,int thickness=1) {
            var f=(double[,])clean.Clone(); settings.Apply(f,true,35,frequency,123,1,thickness,32.5); return f;
        }
        var aligned=Make(); var same=Make(); var off=Make(0); var thick=Make(90,3);
        int changed=0, thicknessChanges=0, bodyCuts=0;
        for(int y=0;y<300;y++) for(int x=0;x<420;x++) {
            if(aligned[x,y]!=same[x,y] || off[x,y]!=clean[x,y]) throw new Exception("Determinism / zero density");
            
            if(aligned[x,y]!=clean[x,y]) changed++;
            if(clean[x,y]<.4 && aligned[x,y]>1) bodyCuts++;
            if(aligned[x,y]!=thick[x,y]) thicknessChanges++;
        }
        if(bodyCuts<20) throw new Exception("Glitch did not cut through the body");
        if(changed<100 || thicknessChanges<100) throw new Exception("Missing glitch / thickness response");
        using var snapshot=new NativeGlitchSnapshot(mask,35,90,1,123,496,settings);
        var exported=snapshot.Build(32.5,8);
        for(int y=0;y<300;y++) for(int x=0;x<420;x++)
            if(exported[x,y]!=aligned[x,y]) throw new Exception("Native export mismatch");
        // Low quality must retain readable contours at both small and large slider radii.
        foreach (double radius in new[] {12.0,32.5,64.0}) {
            using var ellipse = new Bitmap(520,320);
            using(var g=Graphics.FromImage(ellipse)) {
                g.Clear(Color.Black);
                using var pen=new Pen(Color.White,(float)(radius*2));
                g.DrawEllipse(pen,80,80,360,160);
            }
            using var snap=new NativeGlitchSnapshot(ellipse,122,80,4,123,496,
                new LayerEffects {Layered=true,Spacing=2,Tiers=3,OuterDensity=22,MiddleDensity=60,Core=45});
            var low=snap.Build(radius,8); var high=snap.Build(radius,101);
            int border=0,occupied=0;
            for(int y=0;y<320;y++) for(int x=0;x<520;x++) {
                if(low[x,y]!=high[x,y]) throw new Exception("Quality removed layered detail");
                if(low[x,y]<=1) occupied++;
                if(low[x,y]>=.81 && low[x,y]<=.94) border++;
            }
            if(border < occupied*.08) throw new Exception("Border coverage too faint");
            using var render=new Bitmap(520,320);
            for(int y=0;y<320;y++) for(int x=0;x<520;x++)
                render.SetPixel(x,y,low[x,y]>1 ? Color.Black : NativeSliderMask.PreviewColour(low[x,y]));
            string folder=Path.Combine(AppContext.BaseDirectory,"fixtures");
            Directory.CreateDirectory(folder);
            render.Save(Path.Combine(folder,$"layered-ellipse-{radius}.png"));
            Console.WriteLine($"Radius {radius}: solid-border coverage {100.0*border/occupied:F1}% at quality 8.");
        }
        var damagedSettings = new LayerEffects {Layered=true,Angle=3,Spacing=1,Tiers=2,Core=44,OuterDensity=100,MiddleDensity=87};
        using(var shapeSnapshot=new NativeGlitchSnapshot(mask,70,90,5,123,496,damagedSettings)) {
            var fixedShape=shapeSnapshot.Build(32.5,8);
            int leaks=0;
            for(int y=0;y<300;y++) for(int x=0;x<420;x++) {
                if(clean[x,y]>1 && fixedShape[x,y]<=1) leaks++;
            }
            if(leaks>150) throw new Exception("Scanline displacement broke the silhouette");
        }
        settings.BodyAngle=0; settings.BorderAngle=0;
        var baseline=Make();
        settings.BodyAngle=65;
        var bodyOnly=Make();
        settings.BodyAngle=0; settings.BorderAngle=-45;
        var borderOnly=Make();
        int bodyChanges=0,borderChanges=0;
        double boundary=32.5*(1-settings.Core/100);
        for(int y=0;y<300;y++) for(int x=0;x<420;x++) {
            bool body=clean[x,y]<=1 && (1-clean[x,y])*32.5+.5>=boundary;
            if(!body && baseline[x,y]!=bodyOnly[x,y]) throw new Exception("Body rotation altered border");
            if(body && baseline[x,y]!=borderOnly[x,y]) throw new Exception("Border rotation altered body");
            if(body && baseline[x,y]!=bodyOnly[x,y]) bodyChanges++;
            if(!body && baseline[x,y]!=borderOnly[x,y]) borderChanges++;
        }
        if(bodyChanges<100 || borderChanges<100) throw new Exception("Independent rotation had no effect");
        var restored=Newtonsoft.Json.JsonConvert.DeserializeObject<LayerEffects>(Newtonsoft.Json.JsonConvert.SerializeObject(settings));
        if(restored.BodyAngle!=0 || restored.BorderAngle!=-45) throw new Exception("Angle persistence");
        settings.BodyAngle=null; settings.BorderAngle=null;
        settings.Random=true; var random=Make(); settings.Random=false; settings.Angle=35; var rotated=Make();
        using var result=new Bitmap(1260,300);
        var fields=new[]{aligned,random,rotated};
        for(int i=0;i<3;i++) for(int y=0;y<300;y++) for(int x=0;x<420;x++)
            result.SetPixel(i*420+x,y,fields[i][x,y]>1?Color.FromArgb(16,23,30):NativeSliderMask.PreviewColour(fields[i][x,y]));
        string output=Path.Combine(AppContext.BaseDirectory,"fixtures","aligned-glitch.png");
        Directory.CreateDirectory(Path.GetDirectoryName(output)); result.Save(output);
        Console.WriteLine($"Aligned glitch checks passed; {changed} changed pixels. Render: {output}");
    }
}








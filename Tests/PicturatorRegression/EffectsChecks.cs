using System;
using System.Drawing;
using System.IO;
using System.Linq;
using StandalonePicturator.Classes;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Viewmodel;

internal static class EffectsChecks
{
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    public static void Run(string work)
    {
        using var mask=new Bitmap(300,180);
        using(var g=Graphics.FromImage(mask)) {
            g.Clear(Color.Black);
            using var pen=new Pen(Color.White,68) { StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLines(pen,new[]{new Point(50,60),new Point(230,115),new Point(130,45),new Point(80,140)});
        }
        var original=NativeSliderMask.Build(mask,34,101);
        var settings=new LayerEffects { Spacing=3,OuterDensity=20,MiddleDensity=55,Core=40,Tiers=4 };
        double[,] Render(bool random,double angle) {
            var f=(double[,])original.Clone(); settings.Random=random; settings.Angle=angle; settings.Apply(f,true,12,50,123,1); return f;
        }
        var aligned=Render(false,0); var repeat=Render(false,0); var random=Render(true,0); var rotated=Render(false,90);
        int differences=0,rotation=0;
        for(int y=0;y<180;y++)for(int x=0;x<300;x++) {
            Check(aligned[x,y]==repeat[x,y],"Deterministic rows");
            if(original[x,y]<=.6)Check(aligned[x,y]==original[x,y],"Solid core preserved");
            if(aligned[x,y]!=random[x,y])differences++;
            if(aligned[x,y]!=rotated[x,y])rotation++;
        }
        Check(differences>100 && rotation>100,"Randomization and direction alter pattern");
        using(var montage=new Bitmap(900,180)) {
            var fields=new[]{aligned,random,rotated};
            for(int k=0;k<3;k++)for(int y=0;y<180;y++)for(int x=0;x<300;x++) {
                var color=NativeSliderMask.PreviewColour(fields[k][x,y]);
                montage.SetPixel(k*300+x,y,color.A==0?Color.FromArgb(17,23,30):Color.FromArgb(color.A,color.R,color.G,color.B));
            }
            montage.Save(Path.Combine(work,"layered-glitch.png"));
        }
        settings.Cuts.Add(new ShapeCut { Kind="Band",X=.5,Y=.5,Width=20 });
        var cut=(double[,])original.Clone(); settings.Apply(cut,false,0,0,0,1);
        Check(Enumerable.Range(0,300).All(x=>cut[x,90]>1),"Band cut width");
        settings.Cuts.Clear(); settings.Cuts.Add(new ShapeCut { Kind="Below",X=.5,Y=.5 });
        cut=(double[,])original.Clone(); settings.Apply(cut,false,0,0,0,1);
        Check(Enumerable.Range(0,300).All(x=>cut[x,130]>1) && cut[100,60]<=1,"Half-plane crop");
        settings.Cuts.Clear(); settings.Cuts.Add(new ShapeCut { Kind="Freehand",Width=15,Points=new(){new(.1,.5),new(.9,.5)} });
        cut=(double[,])original.Clone(); settings.Apply(cut,false,0,0,0,1);
        Check(cut[150,90]>1,"Freehand eraser follows polyline");
        var vm=new SliderPicturatorVm { AutoOsuResolution=false,YResolution=128,IsGlitchOn=false,SelectedSlider=new HitObject("100,100,1000,2,0,L|200:100,1,100") };
        vm.GlitchAngle=37;vm.GlitchTiers=5;vm.GlitchOuterDensity=15;
        vm.AddShapeCut(new ShapeCut { X=.5,Y=.5,Width=10 });
        using var snapshot=vm.CaptureNativeGlitch();
        Check(snapshot!=null,"Cuts included without glitch enabled");
        var before=snapshot.Build(8,25);vm.ClearShapeCuts();
        var after=snapshot.Build(8,25);
        Check(before.Cast<double>().SequenceEqual(after.Cast<double>()),"Immutable cut snapshot");
        vm.AddShapeCut(new ShapeCut { Kind="Below",X=.5,Y=.5 }); vm.SaveSession();
        var restored=new SliderPicturatorVm();
        Check(restored.CutCount==1 && restored.GlitchAngle==37 && restored.GlitchTiers==5 && restored.GlitchOuterDensity==15,"Effects persistence");
        restored.UndoShapeCut(); Check(restored.CutCount==0,"Undo cut");
        Console.WriteLine("PASS: aligned/random/rotated tiers, solid core, band/half/freehand cuts, snapshots and persistence");
    }
}

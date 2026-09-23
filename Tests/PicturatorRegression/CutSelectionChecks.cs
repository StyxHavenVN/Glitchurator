using System;
using System.Drawing;
using StandalonePicturator.Classes;
using P=System.Windows.Point;

internal static class CutSelectionChecks
{
    public static void Run()
    {
        foreach(string shape in new[]{"Rectangle","Square","Ellipse","Triangle","Star","Heart"}) {
            var points=CutShapes.Create(shape,new P(.2,.2),new P(.8,.8));
            if(!CutShapes.Contains(points,.5,.5) || CutShapes.Contains(points,.05,.05))
                throw new Exception("Invalid selection "+shape);
            var effects=new LayerEffects();
            effects.Cuts.Add(new ShapeCut{Kind="Polygon",Points=points});
            using var mask=new Bitmap(100,100);
            using(var g=Graphics.FromImage(mask))g.Clear(Color.White);
            var field=NativeSliderMask.Build(mask,50,101);
            effects.ApplyMask(mask,50,false,0,0,0,1);
            effects.Apply(field,false,0,0,0,1);
            if(mask.GetPixel(50,50).R!=0 || field[50,50]<=1 || mask.GetPixel(5,5).R!=255 || field[5,5]>1)
                throw new Exception("Preview/export cut mismatch "+shape);
            var saved=Newtonsoft.Json.JsonConvert.DeserializeObject<LayerEffects>(Newtonsoft.Json.JsonConvert.SerializeObject(effects));
            if(saved.Cuts[0].Points.Count!=points.Count)throw new Exception("Cut persistence");
            saved.Cuts.RemoveAt(saved.Cuts.Count-1);
            var clean=new double[100,100];
            saved.Apply(clean,false,0,0,0,1);
            if(clean[50,50]!=0)throw new Exception("Undo cut");
        }
        var square=CutShapes.Create("Square",new P(80,70),new P(20,30));
        if(square[0]!=new P(40,30))throw new Exception("Reverse square drag");
        var source=new System.Windows.Media.Imaging.WriteableBitmap(500,350,96,96,System.Windows.Media.PixelFormats.Bgra32,null);
        var pixels=new byte[500*350*4];
        for(int y=0;y<350;y++)for(int x=0;x<500;x++){
            double r=Math.Sqrt(Math.Pow((x-250)/1.5,2)+Math.Pow(y-175,2));
            int i=(y*500+x)*4;byte color=r>90&&r<125?(byte)160:(byte)20;
            pixels[i]=color;pixels[i+1]=color;pixels[i+2]=color;pixels[i+3]=255;
        }
        source.WritePixels(new System.Windows.Int32Rect(0,0,500,350),pixels,2000,0);
        var window=new StandalonePicturator.View.SliderPicturator.CutEditorWindow(source);
        var content=(System.Windows.FrameworkElement)window.Content;
        content.Measure(new System.Windows.Size(1060,710));content.Arrange(new System.Windows.Rect(0,0,1060,710));content.UpdateLayout();
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap(1060,710,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        image.Render(content);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        string output=System.IO.Path.Combine(AppContext.BaseDirectory,"cut-editor.png");
        using(var file=System.IO.File.Create(output))encoder.Save(file);
        window.Close();
        Console.WriteLine("Editor rendered: "+output);
        Console.WriteLine("Selection shape, mask/export, persistence and cut removal checks passed.");
    }
}


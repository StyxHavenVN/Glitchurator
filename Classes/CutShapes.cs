using System;
using System.Collections.Generic;
using System.Windows;

namespace StandalonePicturator.Classes;

public static class CutShapes
{
    // Build a closed shape inside the drag rectangle; Freehand points are collected by the editor.
    public static List<Point> Create(string kind, Point start, Point end)
    {
        double x=Math.Min(start.X,end.X), y=Math.Min(start.Y,end.Y);
        double w=Math.Abs(end.X-start.X), h=Math.Abs(end.Y-start.Y);
        if(kind=="Square") { w=h=Math.Min(w,h); x=end.X<start.X?start.X-w:start.X; y=end.Y<start.Y?start.Y-h:start.Y; }
        var result=new List<Point>();
        void Add(double u,double v)=>result.Add(new Point(x+u*w,y+v*h));
        if(kind=="Triangle") { Add(.5,0);Add(1,1);Add(0,1); }
        else if(kind=="Star") for(int i=0;i<10;i++) {
            double a=-Math.PI/2+i*Math.PI/5,r=i%2==0?.5:.22;
            Add(.5+r*Math.Cos(a),.5+r*Math.Sin(a));
        }
        else if(kind=="Heart") for(int i=0;i<96;i++) {
            double a=i*2*Math.PI/96;
            Add(.5+Math.Pow(Math.Sin(a),3)/2,(13-(13*Math.Cos(a)-5*Math.Cos(2*a)-2*Math.Cos(3*a)-Math.Cos(4*a)))/30);
        }
        else if(kind=="Ellipse") for(int i=0;i<64;i++) { double a=i*Math.PI/32;Add(.5+.5*Math.Cos(a),.5+.5*Math.Sin(a)); }
        else { Add(0,0);Add(1,0);Add(1,1);Add(0,1); }
        return result;
    }
    // Even-odd polygon hit test used to erase pixels inside arbitrary closed selections.
    public static bool Contains(IReadOnlyList<Point> points,double x,double y)
    {
        bool inside=false;
        for(int i=0,j=points.Count-1;i<points.Count;j=i++) {
            var a=points[i]; var b=points[j];
            if((a.Y>y)!=(b.Y>y) && x<(b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X) inside=!inside;
        }
        return inside;
    }
}


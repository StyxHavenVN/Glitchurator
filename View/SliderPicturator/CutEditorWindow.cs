using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StandalonePicturator.Classes;

namespace StandalonePicturator.View.SliderPicturator;

public sealed class CutEditorWindow : Window
{
    private readonly EditorCanvas editor;
    public IReadOnlyList<ShapeCut> Cuts => editor.Cuts;
    // Edit cuts locally in this window; the caller applies them only when Done returns true.
    public CutEditorWindow(ImageSource image)
    {
        Title="Cut editor";Width=1100;Height=780;MinWidth=720;MinHeight=500;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=new SolidColorBrush(Color.FromRgb(30,30,30));
        var root=new DockPanel {Margin=new Thickness(16)};Content=root;
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var help=new TextBlock {Text="Drag to select • Drag inside to move • Drag corner handles to resize • Ctrl+Z undo • Delete cuts selection",
            Foreground=Brushes.LightGray,Margin=new Thickness(8)};
        bottom.Children.Add(help);
        var tools=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Center};bottom.Children.Add(tools);
        editor=new EditorCanvas(image);
        foreach(string shape in new[]{"Rectangle","Square","Ellipse","Triangle","Star","Heart","Freehand"}) {
            var button=new RadioButton {Content=shape,GroupName="CutShape",Margin=new Thickness(8),Padding=new Thickness(8),Foreground=Brushes.White};
            button.Checked+=(_,_)=>editor.SetShape(shape);tools.Children.Add(button);
            if(shape=="Rectangle")button.IsChecked=true;
        }
        var actions=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,10,0,0)};bottom.Children.Add(actions);
        void Button(string text,Action action) {
            var b=new Button {Content=text,Padding=new Thickness(16,8,16,8),Margin=new Thickness(5)};
            b.Click+=(_,_)=>{action();editor.Focus();};actions.Children.Add(b);
        }
        Button("Cut selection",editor.Commit);
        Button("Undo",editor.Undo);
        Button("Reset",editor.Reset);
        Button("Done",()=>{editor.Commit();DialogResult=true;});
        Button("Cancel",()=>DialogResult=false);
        root.Children.Add(editor);
        PreviewKeyDown+=(_,e)=> {
            if(e.Key==Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){editor.Undo();e.Handled=true;}
            if(e.Key==Key.Delete){editor.Commit();e.Handled=true;}
            if(e.Key==Key.Escape){editor.ClearSelection();e.Handled=true;}
        };
    }

    private sealed class EditorCanvas : FrameworkElement
    {
        private readonly ImageSource image;
        public List<ShapeCut> Cuts {get;}=new();
        private List<Point> selection=new();
        private List<Point> original;
        private string shape="Rectangle";
        private Point start,anchor;
        private int mode;
        private Rect imageRect;
        public EditorCanvas(ImageSource image) {
            this.image=image;Focusable=true;Cursor=Cursors.Cross;ClipToBounds=true;
            MouseLeftButtonDown+=Down;MouseMove+=Move;
            MouseLeftButtonUp+=(_,e)=>{mode=0;ReleaseMouseCapture();e.Handled=true;};
            LostMouseCapture+=(_,_)=>mode=0;
        }
        public void SetShape(string value){shape=value;ClearSelection();}
        public void ClearSelection(){selection.Clear();mode=0;ReleaseMouseCapture();InvalidateVisual();}
        // Save a non-empty polygon in normalized image coordinates so cuts survive image scaling.
    public void Commit(){
            if(selection.Count<3)return;
            double area=0;for(int i=0;i<selection.Count;i++){var a=selection[i];var b=selection[(i+1)%selection.Count];area+=a.X*b.Y-b.X*a.Y;}
            if(Math.Abs(area)>0.00001)Cuts.Add(new ShapeCut {Kind="Polygon",Points=new List<Point>(selection)});
            ClearSelection();
        }
        public void Undo(){if(selection.Count>0)ClearSelection();else if(Cuts.Count>0){Cuts.RemoveAt(Cuts.Count-1);InvalidateVisual();}}
        public void Reset(){Cuts.Clear();ClearSelection();}
        private Point Normal(Point p)=>new(Math.Clamp((p.X-imageRect.X)/Math.Max(1,imageRect.Width),0,1),Math.Clamp((p.Y-imageRect.Y)/Math.Max(1,imageRect.Height),0,1));
        private Point Screen(Point p)=>new(imageRect.X+p.X*imageRect.Width,imageRect.Y+p.Y*imageRect.Height);
        private Rect Bounds()=>selection.Count==0?Rect.Empty:new Rect(new Point(selection.Min(p=>p.X),selection.Min(p=>p.Y)),new Point(selection.Max(p=>p.X),selection.Max(p=>p.Y)));
        private static Point[] Corners(Rect b)=>new[]{b.TopLeft,b.TopRight,b.BottomRight,b.BottomLeft};
        // Hit-test corner handles and selection interior before starting resize, move, or a new shape.
    private void Down(object sender,MouseButtonEventArgs e){
            if(!imageRect.Contains(e.GetPosition(this)))return;
            Focus();start=Normal(e.GetPosition(this));original=new List<Point>(selection);
            var bounds=Bounds();mode=1;
            if(!bounds.IsEmpty){
                var corners=Corners(bounds);
                for(int i=0;i<4;i++)if((Screen(corners[i])-e.GetPosition(this)).Length<12){mode=3;anchor=corners[(i+2)%4];break;}
                if(mode==1 && CutShapes.Contains(selection,start.X,start.Y))mode=2;
            }
            if(mode==1){selection.Clear();selection.Add(start);}
            CaptureMouse();InvalidateVisual();e.Handled=true;
        }
        // Update the pending selection only; dragging never modifies the source image.
    private void Move(object sender,MouseEventArgs e){
            if(mode==0 || e.LeftButton!=MouseButtonState.Pressed)return;
            var p=Normal(e.GetPosition(this));
            if(mode==1){
                if(shape=="Freehand"){if((Screen(p)-Screen(selection[^1])).Length>2)selection.Add(p);}
                else selection=CutShapes.Create(shape,start,p);
            }else if(mode==2){
                double dx=Math.Clamp(p.X-start.X,-original.Min(v=>v.X),1-original.Max(v=>v.X));
                double dy=Math.Clamp(p.Y-start.Y,-original.Min(v=>v.Y),1-original.Max(v=>v.Y));
                selection=original.Select(v=>new Point(v.X+dx,v.Y+dy)).ToList();
            }else{
                var old=new Rect(new Point(original.Min(v=>v.X),original.Min(v=>v.Y)),new Point(original.Max(v=>v.X),original.Max(v=>v.Y)));
                var next=new Rect(anchor,p);
                if(old.Width>0 && old.Height>0)selection=original.Select(v=>new Point(next.X+(v.X-old.X)/old.Width*next.Width,next.Y+(v.Y-old.Y)/old.Height*next.Height)).ToList();
            }
            InvalidateVisual();e.Handled=true;
        }
        private Geometry Geometry(IEnumerable<Point> points){
            var list=points.ToList();var geometry=new StreamGeometry();
            if(list.Count<2)return geometry;
            using(var ctx=geometry.Open()){ctx.BeginFigure(Screen(list[0]),true,true);foreach(var p in list.Skip(1))ctx.LineTo(Screen(p),true,false);}
            return geometry;
        }
        // Fit the image to the editing area and overlay pending cuts, selection, and resize handles.
    protected override void OnRender(DrawingContext dc){
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(22,25,30)),null,new Rect(RenderSize));
            if(image==null)return;
            double fit=Math.Min(Math.Max(1,ActualWidth-48)/image.Width,Math.Max(1,ActualHeight-48)/image.Height);
            imageRect=new Rect((ActualWidth-image.Width*fit)/2,(ActualHeight-image.Height*fit)/2,image.Width*fit,image.Height*fit);
            dc.DrawRectangle(Brushes.Black,null,imageRect);dc.DrawImage(image,imageRect);
            foreach(var cut in Cuts)dc.DrawGeometry(Brushes.Black,null,Geometry(cut.Points));
            if(selection.Count<2)return;
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(65,255,80,70)),new Pen(Brushes.White,1.5),Geometry(selection));
            var b=Bounds();dc.DrawRectangle(null,new Pen(Brushes.White,1){DashStyle=DashStyles.Dash},new Rect(Screen(b.TopLeft),Screen(b.BottomRight)));
            foreach(var p in Corners(b)){var s=Screen(p);dc.DrawRectangle(Brushes.White,new Pen(Brushes.Black,1),new Rect(s.X-5,s.Y-5,10,10));}
        }
    }
}


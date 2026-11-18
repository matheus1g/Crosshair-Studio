using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CrosshairStudio
{
    internal static class Icons
    {
        private static readonly Dictionary<string,string> Paths = new Dictionary<string,string> {
            {"home","M2,6 8,2 14,6 14,14 10,14 10,10 6,10 6,14 2,14 Z"},
            {"edit","M3,4 L12,4 M3,8 L12,8 M3,12 L12,12 M6,2 L6,6 M10,6 L10,10 M5,10 L5,14"},
            {"library","M2,2 H6 V6 H2 Z M10,2 H14 V6 H10 Z M2,10 H6 V14 H2 Z M10,10 H14 V14 H10 Z"},
            {"profile","M5,5 C5,1 11,1 11,5 C11,9 5,9 5,5 M2,14 C2,8 14,8 14,14"},
            {"compare","M2,3 H14 V13 H2 Z M8,3 V13"},
            {"settings","M8,5 A3,3 0 1 0 8,11 A3,3 0 1 0 8,5 M8,1 V3 M8,13 V15 M1,8 H3 M13,8 H15 M3,3 L4,4 M12,12 L13,13 M3,13 L4,12 M12,4 L13,3"},
            {"target","M8,3 A5,5 0 1 0 8,13 A5,5 0 1 0 8,3 M8,1 V5 M8,11 V15 M1,8 H5 M11,8 H15"},
            {"monitor","M2,2 H14 V11 H2 Z M8,11 V14 M5,14 H11"},
            {"keyboard","M1,4 H15 V12 H1 Z M4,7 H5 M7,7 H9 M11,7 H12 M5,10 H11"},
            {"next","M3,8 H13 M9,4 L13,8 9,12"},
            {"star","M8,1 10,5.5 15,6 11.5,9.3 12.5,14 8,11.8 3.5,14 4.5,9.3 1,6 6,5.5 Z"}
        };
        public static FrameworkElement Get(string name, double width)
        {
            string path;
            if (!Paths.TryGetValue(name, out path)) path = Paths["target"];
            var picture = new System.Windows.Shapes.Path { Data=Geometry.Parse(path), Stretch=Stretch.Uniform, StrokeThickness=1.35, Width=width, Height=width, StrokeLineJoin=PenLineJoin.Round };
            picture.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(System.Windows.Controls.Control),1) });
            return picture;
        }
    }

    internal sealed class Preview : FrameworkElement
    {
        public Preset Preset;
        public Pose Pose = new Pose(1,1,0);
        public bool Grid = true;
        public bool TargetMarks = true;
        public string BackgroundMode = "Lab";
        public ImageSource BackgroundImage;
        public double Zoom = 3;
        public double Aspect;
        public double PixelWidth;
        private Drawing drawing;
        private static readonly Brush LabBg = new RadialGradientBrush(Color.FromRgb(38,34,64),Color.FromRgb(13,15,24)) { RadiusX = .77, RadiusY = .82 };
        private static readonly Brush Plain = new SolidColorBrush(Color.FromRgb(16,20,30));
        private static readonly Brush Light = new SolidColorBrush(Color.FromRgb(207,217,231));
        private static readonly Pen GridLine = new Pen(new SolidColorBrush(Color.FromArgb(34,135,147,190)),1);
        private static readonly Pen AccentLine = new Pen(new SolidColorBrush(Color.FromArgb(95,156,143,217)),1);
        private static readonly Pen CircleLine = new Pen(new SolidColorBrush(Color.FromArgb(100,149,138,205)),1) { DashStyle = DashStyles.Dot };
        private static readonly Pen CenterLine = new Pen(new SolidColorBrush(Color.FromArgb(90,152,146,217)),1) { DashStyle = DashStyles.Dash };
        private static readonly Brush Tick = new SolidColorBrush(Color.FromArgb(125,174,170,216));

        public void Refresh()
        {
            drawing = Preset == null ? null : Reticle.Scene(Preset,Pose);
            InvalidateVisual();
        }
        public double RenderScale
        {
            get
            {
                double viewport = Viewport().Width;
                return PixelWidth > 0 && viewport > 0 ? Zoom * viewport / PixelWidth : Zoom;
            }
        }
        private Rect Viewport()
        {
            double w = ActualWidth, h = ActualHeight;
            if (Aspect <= 0 || h <= 0) return new Rect(0,0,w,h);
            if (w / h > Aspect) return new Rect((w-h*Aspect)/2,0,h*Aspect,h);
            return new Rect(0,(h-w/Aspect)/2,w,w/Aspect);
        }
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 1 || h < 1) return;
            var area = new Rect(0,0,w,h);
            var view = Viewport();
            var center = new Point(w / 2, h / 2);
            dc.PushClip(new RectangleGeometry(area, 10, 10));
            dc.DrawRectangle(Plain, null, area);
            dc.PushClip(new RectangleGeometry(view));
            dc.DrawRectangle(BackgroundMode == "Light" ? Light : BackgroundMode == "Dark" ? Plain : LabBg, null, view);
            if (BackgroundMode == "Image" && BackgroundImage != null)
            {
                double factor = Math.Max(view.Width / BackgroundImage.Width, view.Height / BackgroundImage.Height);
                double iw = BackgroundImage.Width*factor, ih=BackgroundImage.Height*factor;
                dc.DrawImage(BackgroundImage,new Rect(center.X-iw/2,center.Y-ih/2,iw,ih));
            }
            if (Grid)
            {
                for (double x = center.X % 24; x < w; x += 24) dc.DrawLine(GridLine,new Point(x,0),new Point(x,h));
                for (double y = center.Y % 24; y < h; y += 24) dc.DrawLine(GridLine,new Point(0,y),new Point(w,y));
            }
            if (TargetMarks && w > 150 && h > 120)
            {
                double radius = Math.Min(view.Width,view.Height) * .26;
                dc.DrawEllipse(null,CircleLine,center,radius,radius);
                dc.DrawEllipse(null,GridLine,center,radius*.67,radius*.67);
                dc.DrawLine(CenterLine,new Point(center.X,view.Top+16),new Point(center.X,view.Bottom-16));
                dc.DrawLine(CenterLine,new Point(view.Left+16,center.Y),new Point(view.Right-16,center.Y));
                Mark(dc,view.Left+15,view.Top+15,1,1); Mark(dc,view.Right-15,view.Top+15,-1,1);
                Mark(dc,view.Left+15,view.Bottom-15,1,-1); Mark(dc,view.Right-15,view.Bottom-15,-1,-1);
            }
            if (drawing != null)
            {
                double scale = RenderScale;
                if (w < 240 || h < 100) scale = Math.Min(scale, Math.Min(w,h) * .42 / Reticle.Extent(drawing));
                dc.PushTransform(new TranslateTransform(center.X,center.Y)); dc.PushTransform(new ScaleTransform(scale,scale));
                dc.DrawDrawing(drawing); dc.Pop(); dc.Pop();
            }
            dc.Pop(); dc.Pop();
        }
        private static void Mark(DrawingContext dc,double x,double y,int sx,int sy)
        {
            dc.DrawLine(AccentLine,new Point(x+sx*11,y),new Point(x,y));
            dc.DrawLine(AccentLine,new Point(x,y),new Point(x,y+sy*11));
            dc.DrawEllipse(Tick,null,new Point(x+sx*16,y+sy*16),1,1);
        }
    }
}

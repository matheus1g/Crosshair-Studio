using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;

namespace CrosshairStudio
{
    internal struct Pose
    {
        public double Scale, Alpha, Angle;
        public Pose(double scale, double alpha, double angle) { Scale = scale; Alpha = alpha; Angle = angle; }
    }

    internal static class Reticle
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";
        public static Color ParseColor(string text)
        {
            text = (text ?? "").Trim();
            if (text.StartsWith("#"))
            {
                string hex = text.Substring(1);
                if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
                if (hex.Length == 6 || hex.Length == 8)
                {
                    uint value;
                    if (uint.TryParse(hex, NumberStyles.HexNumber, Invariant, out value))
                    {
                        if (hex.Length == 6) value = (value << 8) | 255;
                        return Color.FromArgb((byte)value, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8));
                    }
                }
            }
            var match = Regex.Match(text, @"^(rgb|rgba)\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})(?:\s*,\s*(\d{1,3}(?:\.\d+)?|\.\d+))?\s*\)$", RegexOptions.IgnoreCase);
            if (match.Success && (match.Groups[1].Value.Length == 4) == match.Groups[5].Success)
            {
                int r = int.Parse(match.Groups[2].Value), g = int.Parse(match.Groups[3].Value), b = int.Parse(match.Groups[4].Value);
                double a = match.Groups[5].Success ? double.Parse(match.Groups[5].Value, Invariant) : 1;
                if (a > 1) a /= 255;
                if (r <= 255 && g <= 255 && b <= 255 && a <= 1) return Color.FromArgb((byte)Math.Round(a * 255), (byte)r, (byte)g, (byte)b);
            }
            throw new InvalidDataException("Use #RRGGBB, #RRGGBBAA, rgb(r,g,b) ou rgba(r,g,b,a).");
        }
        public static string NormalizeColor(string value)
        {
            var c = ParseColor(value); return Hex(c) + (c.A == 255 ? "" : c.A.ToString("X2", Invariant));
        }
        private static string Hex(Color c) { return string.Format(Invariant, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

        public static Pose Evaluate(Motion m, double elapsed, bool active)
        {
            if (!active || m.Kind == "None") return new Pose(1, 1, 0);
            double t = Math.Max(0, elapsed / m.Seconds);
            t = m.Loop ? t % 1 : Math.Min(1, t);
            double wave = (1 - Math.Cos(t * Math.PI * 2)) / 2;
            switch (m.Kind)
            {
                case "Pulse": return new Pose(1 + wave * .28, 1, 0);
                case "Fade": return new Pose(1, 1 - wave * .68, 0);
                case "Spin": return new Pose(1, 1, t * 360);
                case "Grow": return new Pose(.75 + .55 * wave, 1, 0);
                case "Keyframes":
                    if (m.Frames.Count == 0) break;
                    var first = m.Frames[0]; var last = m.Frames[m.Frames.Count - 1];
                    t *= 100;
                    if (t <= first.Time) return ToPose(first);
                    if (t >= last.Time) return ToPose(last);
                    for (int i = 1; i < m.Frames.Count; i++)
                    {
                        var right = m.Frames[i];
                        if (t > right.Time) continue;
                        var left = m.Frames[i - 1];
                        double blend = (t - left.Time) / (right.Time - left.Time);
                        return new Pose(Mix(left.Scale, right.Scale, blend) / 100, Mix(left.Alpha, right.Alpha, blend) / 100, Mix(left.Angle, right.Angle, blend));
                    }
                    break;
            }
            return new Pose(1, 1, 0);
        }
        public static double AnimationExtent(Motion motion)
        {
            switch (motion.Kind)
            {
                case "Pulse": return 1.28;
                case "Grow": return 1.3;
                case "Spin": return Math.Sqrt(2);
                case "Keyframes":
                    if (motion.Frames.Count==0) return 1;
                    double rotation=motion.Frames.Any(f=>f.Angle!=0) ? Math.Sqrt(2) : 1;
                    return rotation*motion.Frames.Max(f=>f.Scale)/100;
                default: return 1;
            }
        }
        private static Pose ToPose(Keyframe f) { return new Pose(f.Scale / 100, f.Alpha / 100, f.Angle); }
        private static double Mix(double a, double b, double t) { return a + (b - a) * t; }

        public static DrawingGroup Scene(Preset preset, Pose pose)
        {
            var drawing = new DrawingGroup();
            using (var dc = drawing.Open())
            {
                dc.PushOpacity(pose.Alpha);
                dc.PushTransform(new ScaleTransform(pose.Scale, pose.Scale));
                dc.PushTransform(new RotateTransform(pose.Angle));
                foreach (var layer in preset.Layers) if (layer.Visible && layer.Opacity > 0) DrawLayer(dc, layer);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
            drawing.Freeze();
            return drawing;
        }

        private static void DrawLayer(DrawingContext dc, Layer l)
        {
            dc.PushTransform(new TranslateTransform(l.X, l.Y));
            dc.PushTransform(new RotateTransform(l.Rotation));
            dc.PushTransform(new ScaleTransform(l.Mirror ? -1 : 1, 1));
            dc.PushOpacity(l.Opacity / 100);
            if (l.Shape == "PNG" || l.Shape == "SVG")
            {
                ImageSource image = Assets.Get(l);
                double ratio = l.Size * 2 / Math.Max(image.Width, image.Height);
                var vector = image as DrawingImage;
                if (vector == null)
                    dc.DrawImage(image, new Rect(-image.Width * ratio / 2, -image.Height * ratio / 2, image.Width * ratio, image.Height * ratio));
                else
                {
                    dc.PushTransform(new ScaleTransform(ratio, ratio));
                    dc.DrawDrawing(vector.Drawing);
                    dc.Pop();
                }
            }
            else
            {
                bool filled;
                Geometry g = Shape(l, out filled);
                var main = ParseColor(l.Color);
                var border = ParseColor(l.OutlineColor);
                Brush color = string.IsNullOrEmpty(l.Gradient) ? (Brush)new SolidColorBrush(main)
                    : new LinearGradientBrush(main, ParseColor(l.Gradient), new Point(0, .5), new Point(1, .5));
                double stroke = filled ? 0 : l.Thickness;
                if (l.Shadow > 0)
                {
                    dc.PushTransform(new TranslateTransform(l.Shadow * .65, l.Shadow * .65));
                    Paint(dc, g, filled, new SolidColorBrush(Color.FromArgb(117, 0, 0, 0)), stroke + l.Shadow);
                    dc.Pop();
                }
                if (l.Glow > 0) Halo(dc, g, main, stroke, l.Glow, .27);
                if (l.Blur > 0) Halo(dc, g, main, stroke, l.Blur, .35);
                if (l.Outline > 0) Paint(dc, g, filled, new SolidColorBrush(border), stroke + l.Outline * 2);
                Paint(dc, g, filled, color, stroke);
                if (l.InnerBorder > 0)
                {
                    if (filled)
                    {
                        dc.DrawGeometry(null, new Pen(new SolidColorBrush(border), l.InnerBorder), g);
                        if (l.Outline > 0) dc.DrawGeometry(null, new Pen(new SolidColorBrush(border), l.Outline * 2), g);
                    }
                    else if (stroke > l.InnerBorder)
                        Paint(dc, g, false, new SolidColorBrush(border), stroke - l.InnerBorder);
                }
            }
            dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        }

        private static void Paint(DrawingContext dc, Geometry g, bool fill, Brush brush, double stroke)
        {
            var pen = stroke > 0 ? new Pen(brush, stroke) { LineJoin = PenLineJoin.Round } : null;
            dc.DrawGeometry(fill ? brush : null, pen, g);
        }
        private static void Halo(DrawingContext dc, Geometry g, Color c, double line, double radius, double strength)
        {
            for (int i = 4; i > 0; i--)
            {
                var brush = new SolidColorBrush(Color.FromArgb((byte)(c.A * strength / 4), c.R, c.G, c.B));
                Paint(dc, g, false, brush, line + radius * i / 2);
            }
        }
        private static Geometry Shape(Layer l, out bool filled)
        {
            double s = l.Size, gap = l.Gap, outer = gap + s;
            filled = false;
            switch (l.Shape)
            {
                case "Dot": filled = true; return new EllipseGeometry(new Point(), s / 2, s / 2);
                case "Ring": return new EllipseGeometry(new Point(), s, s);
                case "Square": return new RectangleGeometry(new Rect(-s, -s, 2 * s, 2 * s));
                case "Diamond": return Lines(true, new[] { P(0,-s),P(s,0),P(0,s),P(-s,0) });
                case "Chevron": return Lines(false, new[] { P(-s,s*.55),P(0,-s*.55),P(s,s*.55) });
                case "Line": return Lines(false, new[] { P(-s,0),P(s,0) });
                case "X":
                    return Lines(false, new[] { P(-outer,-outer),P(-gap,-gap) }, new[] { P(gap,gap),P(outer,outer) },
                        new[] { P(-outer,outer),P(-gap,gap) }, new[] { P(gap,-gap),P(outer,-outer) });
                case "Corners":
                    return Lines(false, new[] { P(-outer,-gap),P(-outer,-outer),P(-gap,-outer) }, new[] { P(outer,-gap),P(outer,-outer),P(gap,-outer) },
                        new[] { P(-outer,gap),P(-outer,outer),P(-gap,outer) }, new[] { P(outer,gap),P(outer,outer),P(gap,outer) });
                default:
                    var arms = new List<Point[]> { new[] { P(-outer,0),P(-gap,0) }, new[] { P(gap,0),P(outer,0) }, new[] { P(0,gap),P(0,outer) } };
                    if (l.Shape != "T") arms.Add(new[] { P(0,-outer),P(0,-gap) });
                    return Lines(false, arms.ToArray());
            }
        }

        private static Point P(double x, double y) { return new Point(x, y); }
        private static Geometry Lines(bool closed, params Point[][] paths)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
                foreach (var path in paths)
                {
                    context.BeginFigure(path[0], true, closed);
                    for (int i = 1; i < path.Length; i++) context.LineTo(path[i], true, false);
                }
            return geometry;
        }

        public static RenderTargetBitmap Bitmap(Drawing drawing, int size, double scale)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(size / 2.0, size / 2.0));
                dc.PushTransform(new ScaleTransform(scale, scale));
                dc.DrawDrawing(drawing);
                dc.Pop(); dc.Pop();
            }
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); return bitmap;
        }
        public static double Extent(Drawing drawing)
        {
            Rect b = drawing.Bounds;
            return b.IsEmpty ? 1 : Math.Max(1, Math.Max(Math.Max(Math.Abs(b.Left), Math.Abs(b.Right)), Math.Max(Math.Abs(b.Top), Math.Abs(b.Bottom))));
        }
        public static void ExportPng(Preset p, string path)
        {
            var scene = Scene(p, new Pose(1,1,0));
            int size = (int)Math.Max(128, Math.Ceiling(Extent(scene) * 2 + 8));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Bitmap(scene, size, 1)));
            using (var output = File.Create(path)) encoder.Save(output);
        }
        public static string ExportSvg(Preset p)
        {
            var scene = Scene(p, new Pose(1, 1, 0));
            double half = Math.Ceiling(Extent(scene) + 4);
            var xml = new XElement(SvgNamespace + "svg", new XAttribute("viewBox", Join(new[] {-half,-half,2*half,2*half})));
            var definitions = new XElement(SvgNamespace + "defs");
            xml.Add(definitions);
            AppendSvg(xml, scene, definitions);
            return xml.ToString();
        }
        private static void AppendSvg(XElement xml, Drawing drawing, XElement defs)
        {
            var g = drawing as DrawingGroup;
            if (g != null)
            {
                var group = new XElement(SvgNamespace + "g");
                if (g.Opacity != 1) group.SetAttributeValue("opacity", N(g.Opacity));
                if (g.Transform != null && !g.Transform.Value.IsIdentity)
                {
                    var m = g.Transform.Value;
                    group.SetAttributeValue("transform", "matrix(" + Join(new[] {m.M11,m.M12,m.M21,m.M22,m.OffsetX,m.OffsetY}) + ")");
                }
                foreach (var child in g.Children) AppendSvg(group, child, defs);
                xml.Add(group); return;
            }
            var item = drawing as GeometryDrawing;
            if (item == null) throw new InvalidDataException("A exportação SVG exige camadas vetoriais. Use PNG para camadas raster.");
            var path = new XElement(SvgNamespace + "path");
            var flattened = item.Geometry.GetFlattenedPathGeometry(.04, ToleranceType.Absolute);
            var buffer = new StringBuilder();
            foreach (var figure in flattened.Figures)
            {
                buffer.Append("M ").Append(N(figure.StartPoint.X)).Append(' ').Append(N(figure.StartPoint.Y)).Append(' ');
                foreach (var segment in figure.Segments)
                {
                    var line = segment as LineSegment;
                    var poly = segment as PolyLineSegment;
                    if (line != null) buffer.Append("L ").Append(N(line.Point.X)).Append(' ').Append(N(line.Point.Y)).Append(' ');
                    if (poly != null) foreach (var point in poly.Points)
                        buffer.Append("L ").Append(N(point.X)).Append(' ').Append(N(point.Y)).Append(' ');
                }
                if (figure.IsClosed) buffer.Append("Z ");
            }
            if (buffer.Length == 0) return;
            path.SetAttributeValue("fill-rule", flattened.FillRule == FillRule.Nonzero ? "nonzero" : "evenodd");
            path.SetAttributeValue("d", buffer.ToString().Trim());
            SetSvgBrush(path, "fill", item.Brush, defs);
            if (item.Pen != null)
            {
                SetSvgBrush(path, "stroke", item.Pen.Brush, defs);
                path.SetAttributeValue("stroke-width", N(item.Pen.Thickness));
                path.SetAttributeValue("stroke-linejoin", item.Pen.LineJoin == PenLineJoin.Round ? "round" : "miter");
            }
            xml.Add(path);
        }
        private static void SetSvgBrush(XElement path, string property, Brush brush, XElement defs)
        {
            var solid = brush as SolidColorBrush;
            var gradient = brush as LinearGradientBrush;
            if (solid != null)
            {
                path.SetAttributeValue(property, Hex(solid.Color));
                if (solid.Color.A < 255 || solid.Opacity < 1) path.SetAttributeValue(property + "-opacity", N(solid.Opacity * solid.Color.A / 255.0));
            }
            else if (gradient != null)
            {
                string id = "g" + defs.Elements().Count();
                var element = new XElement(SvgNamespace + "linearGradient", new XAttribute("id",id), new XAttribute("x2","100%"), new XAttribute("y2","0%"));
                foreach (var stop in gradient.GradientStops)
                    element.Add(new XElement(SvgNamespace + "stop", new XAttribute("offset",N(stop.Offset)), new XAttribute("stop-color",Hex(stop.Color)), new XAttribute("stop-opacity",N(stop.Color.A/255.0))));
                defs.Add(element);
                path.SetAttributeValue(property, "url(#" + id + ")");
            }
            else path.SetAttributeValue(property, "none");
        }
        private static string N(double value) { return value.ToString("0.####", Invariant); }
        private static string Join(IEnumerable<double> values) { return string.Join(" ", values.Select(N)); }
    }

    internal static class Assets
    {
        public const int MaxImage = 1024 * 1024;
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();
        public static Layer Import(string path)
        {
            string ext = Path.GetExtension(path).ToUpperInvariant();
            if (ext != ".PNG" && ext != ".SVG") throw new InvalidDataException("Selecione um PNG ou SVG.");
            byte[] bytes;
            using (var input = File.OpenRead(path))
            {
                if (input.Length > MaxImage) throw new InvalidDataException("A imagem excede 1 MiB.");
                using (var buffer = new MemoryStream()) { input.CopyTo(buffer); bytes = buffer.ToArray(); }
            }
            var layer = new Layer { Name = Store.Limit(Path.GetFileNameWithoutExtension(path), 45, "Imagem"), Shape = ext.Substring(1), Image = Convert.ToBase64String(bytes), Size = 24 };
            Get(layer); return layer;
        }
        public static ImageSource Get(Layer layer)
        {
            if (string.IsNullOrEmpty(layer.Image) || layer.Image.Length > (MaxImage * 4 / 3 + 8)) throw new InvalidDataException("Imagem inválida ou grande demais.");
            string key = layer.Shape + layer.Image;
            ImageSource asset;
            if (Cache.TryGetValue(key, out asset)) return asset;
            byte[] bytes = Convert.FromBase64String(layer.Image);
            if (bytes.Length > MaxImage) throw new InvalidDataException("Imagem grande demais.");
            asset = layer.Shape == "PNG" ? (ImageSource)Png(bytes) : Svg(bytes);
            asset.Freeze();
            if (Cache.Count >= 16) Cache.Clear();
            Cache[key] = asset; return asset;
        }
        private static BitmapSource Png(byte[] bytes)
        {
            byte[] magic = {137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82};
            if (bytes.Length < 33 || !magic.SequenceEqual(bytes.Take(16)) || bytes[16] != 0 || bytes[17] != 0 || bytes[20] != 0 || bytes[21] != 0)
                throw new InvalidDataException("Cabeçalho PNG inválido.");
            int w = bytes[18] * 256 + bytes[19], h = bytes[22] * 256 + bytes[23];
            if (w < 1 || h < 1 || w > 2048 || h > 2048) throw new InvalidDataException("PNG deve ter até 2048 × 2048 pixels.");
            using (var stream = new MemoryStream(bytes))
                return new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }

        private static ImageSource Svg(byte[] bytes)
        {
            XDocument svg;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 160000 };
            using (var stream = new MemoryStream(bytes)) using (var reader = XmlReader.Create(stream,settings)) svg = XDocument.Load(reader);
            XElement root = svg.Root;
            if (root == null || root.Name.LocalName != "svg" || root.Descendants().Take(257).Count() > 256)
                throw new InvalidDataException("SVG inválido ou complexo demais.");
            var view = Numbers(Attr(root,"viewBox","0 0 64 64"));
            if (view.Length != 4 || view[2] <= 0 || view[3] <= 0) throw new InvalidDataException("viewBox inválido.");
            double width = view[2], height = view[3];
            var group = new DrawingGroup();
            var center = new TranslateTransform(-view[0]-width/2, -view[1]-height/2);
            using (var dc = group.Open())
            {
                dc.DrawRectangle(Brushes.Transparent,null,new Rect(-width/2,-height/2,width,height));
                dc.PushTransform(center);
                DrawSvg(dc, root, 0, "#000000", "none", 1);
                dc.Pop();
            }
            Rect bounds = group.Bounds;
            if (bounds.IsEmpty || bounds.Width > 25000 || bounds.Height > 25000) throw new InvalidDataException("SVG fora da área permitida.");
            return new DrawingImage(group);
        }

        private static void DrawSvg(DrawingContext dc, XElement node, int depth, string fill, string stroke, double weight)
        {
            if (depth > 12) throw new InvalidDataException("SVG excede a profundidade permitida.");
            string name = node.Name.LocalName;
            if (name == "title" || name == "desc" || name == "metadata") return;
            if (node.Attribute("style") != null || node.Attribute("class") != null || node.Attribute("filter") != null ||
                node.Attribute("mask") != null || node.Attribute("clip-path") != null || node.Attribute("display") != null)
                throw new InvalidDataException("Use somente atributos SVG de apresentação simples.");
            fill = Attr(node,"fill",fill); stroke = Attr(node,"stroke",stroke); weight = Value(node,"stroke-width",weight);
            dc.PushTransform(SvgTransform(Attr(node,"transform","")));
            dc.PushOpacity(Store.Clamp(Value(node,"opacity",1),0,1,1));
            Geometry geometry;
            switch (name)
            {
                case "svg": case "g":
                    foreach (var item in node.Elements()) DrawSvg(dc,item,depth+1,fill,stroke,weight);
                    dc.Pop(); dc.Pop(); return;
                case "circle":
                    geometry = new EllipseGeometry(new Point(Value(node,"cx",0), Value(node,"cy",0)), Value(node,"r",0), Value(node,"r",0)); break;
                case "ellipse":
                    geometry = new EllipseGeometry(new Point(Value(node,"cx",0), Value(node,"cy",0)), Value(node,"rx",0), Value(node,"ry",0)); break;
                case "rect":
                    geometry = new RectangleGeometry(new Rect(Value(node,"x",0),Value(node,"y",0),Value(node,"width",0),Value(node,"height",0)),
                        Value(node,"rx",0),Value(node,"ry",0)); break;
                case "line":
                    geometry = new LineGeometry(new Point(Value(node,"x1",0),Value(node,"y1",0)),new Point(Value(node,"x2",0),Value(node,"y2",0))); break;
                case "polygon": case "polyline":
                    var points = Numbers(Attr(node,"points",""));
                    if (points.Length < 4 || points.Length % 2 != 0) throw new InvalidDataException("Pontos SVG inválidos.");
                    var poly = new StreamGeometry();
                    using (var pen = poly.Open())
                    {
                        pen.BeginFigure(new Point(points[0],points[1]), true, name == "polygon");
                        for (int i = 2; i < points.Length; i += 2) pen.LineTo(new Point(points[i],points[i+1]),true,false);
                    }
                    geometry = poly; break;
                case "path":
                    string data = Attr(node,"d","");
                    if (data.Length > 18000 || !Regex.IsMatch(data,@"^[MmZzLlHhVvCcSsQqTtAa\d.,+\-\sEe]+$")) throw new InvalidDataException("Caminho SVG inválido.");
                    foreach (Match token in Regex.Matches(data,@"[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?"))
                        Number(token.Value);
                    geometry = Geometry.Parse((Attr(node,"fill-rule","nonzero") == "evenodd" ? "F0 " : "F1 ") + data);
                    break;
                default: throw new InvalidDataException("Elemento SVG não suportado: " + name);
            }
            var fillBrush = name == "line" ? null : SvgBrush(fill, Value(node,"fill-opacity",1));
            var strokeBrush = SvgBrush(stroke, Value(node,"stroke-opacity",1));
            var outline = strokeBrush == null || weight <= 0 ? null : new Pen(strokeBrush, weight);
            if (outline != null && Attr(node,"stroke-linejoin","") == "round") outline.LineJoin = PenLineJoin.Round;
            dc.DrawGeometry(fillBrush, outline, geometry);
            dc.Pop(); dc.Pop();
        }
        private static Brush SvgBrush(string value, double alpha)
        {
            if (value == "none") return null;
            if (!value.StartsWith("#") && !value.StartsWith("rgb"))
            {
                if (!Regex.IsMatch(value, @"^[a-zA-Z]{1,24}$")) throw new InvalidDataException("Use uma cor sólida no SVG.");
                var named = typeof(Colors).GetProperties().FirstOrDefault(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
                if (named == null) throw new InvalidDataException("Cor SVG desconhecida.");
                return new SolidColorBrush((Color)named.GetValue(null,null)) { Opacity = Store.Clamp(alpha,0,1,1) };
            }
            return new SolidColorBrush(Reticle.ParseColor(value)) { Opacity = Store.Clamp(alpha,0,1,1) };
        }
        private static Transform SvgTransform(string code)
        {
            if (code.Length == 0) return Transform.Identity;
            var group = new TransformGroup();
            var regex = new Regex(@"\G\s*,?\s*(matrix|translate|scale|rotate)\s*\(([^)]*)\)\s*");
            int index = 0;
            while (index < code.Length && group.Children.Count < 12)
            {
                Match match = regex.Match(code,index);
                if (!match.Success) throw new InvalidDataException("Transformação SVG inválida.");
                var v = Numbers(match.Groups[2].Value);
                Transform t = null;
                switch (match.Groups[1].Value)
                {
                    case "matrix": if (v.Length == 6) t = new MatrixTransform(v[0],v[1],v[2],v[3],v[4],v[5]); break;
                    case "translate": if (v.Length == 1 || v.Length == 2) t = new TranslateTransform(v[0],v.Length == 2 ? v[1] : 0); break;
                    case "scale": if (v.Length == 1 || v.Length == 2) t = new ScaleTransform(v[0],v.Length == 2 ? v[1] : v[0]); break;
                    case "rotate": if (v.Length == 1 || v.Length == 3) t = new RotateTransform(v[0],v.Length == 3 ? v[1] : 0,v.Length == 3 ? v[2] : 0); break;
                }
                if (t == null) throw new InvalidDataException("Parâmetros de transformação SVG inválidos.");
                group.Children.Insert(0,t); index += match.Length;
            }
            if (index != code.Length) throw new InvalidDataException("SVG tem transformações demais.");
            return group;
        }
        private static double[] Numbers(string text)
        {
            return Regex.Split(text.Trim(),@"[\s,]+").Where(s => s.Length > 0).Select(Number).ToArray();
        }
        private static double Value(XElement node, string key, double fallback)
        {
            var a = node.Attribute(key); return a == null ? fallback : Number(a.Value);
        }
        private static double Number(string v)
        {
            double n;
            if (!double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out n) || double.IsNaN(n) || Math.Abs(n) > 8192)
                throw new InvalidDataException("Número SVG inválido.");
            return n;
        }
        private static string Attr(XElement x,string key,string fallback)
        {
            var a = x.Attribute(key); return a == null ? fallback : a.Value;
        }
    }
}

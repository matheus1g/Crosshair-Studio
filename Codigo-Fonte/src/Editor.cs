using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace CrosshairStudio
{
    internal partial class StudioWindow
    {
        private Preview editorPreview;
        private TextBlock coordinates;
        private Button undoButton, redoButton;
        private TextBox xBox, yBox;
        private double zoom=3;
        private bool gridOn=true, snap;
        private string background="Lab", resolution="Canvas";
        private ImageSource previewBackground;
        private string lastEdit="";
        private DateTime lastEditAt;
        private string ShapeName(string key)
        {
            switch (key)
            {
                case "Cross": return T("Cruz","Cross");
                case "Dot": return T("Ponto","Dot");
                case "Ring": return T("Anel","Ring");
                case "Line": return T("Linha","Line");
                case "Square": return T("Quadrado","Square");
                case "Diamond": return T("Losango","Diamond");
                case "Chevron": return T("Seta","Chevron");
                case "Corners": return T("Cantos","Corners");
                default: return key;
            }
        }
        private void Editor(StackPanel body)
        {
            PageHeading(body,T("Editor de miras","Crosshair editor"),T("Construa a mira em camadas e ajuste cada elemento em tempo real.","Build a layered crosshair and adjust each part in real time."));
            var toolbar = new DockPanel { Margin=VerticalSpace(0,12) };
            var save=Button(T("Salvar mira","Save crosshair"),SaveDraft,true);
            save.Margin=new Thickness(12,0,0,0);
            DockPanel.SetDock(save,Dock.Right); toolbar.Children.Add(save);
            var name = TextField(T("Nome da mira","Crosshair name"),draft.Name,value=>Edit("preset-name",delegate { draft.Name=Store.Limit(value,60,T("Nova mira","New crosshair")); }),60);
            toolbar.Children.Add(name); body.Children.Add(toolbar);

            var commands = new WrapPanel { Margin=VerticalSpace(0,13) };
            var history=Row();
            history.Margin=new Thickness(0,0,12,6);
            undoButton=Button("↶",delegate { Undo(false); }); undoButton.ToolTip=T("Desfazer (Ctrl+Z)","Undo (Ctrl+Z)");
            redoButton=Button("↷",delegate { Undo(true); }); redoButton.ToolTip=T("Refazer (Ctrl+Y)","Redo (Ctrl+Y)");
            AutomationProperties.SetName(undoButton,(string)undoButton.ToolTip); AutomationProperties.SetName(redoButton,(string)redoButton.ToolTip);
            history.Children.Add(undoButton); history.Children.Add(redoButton); commands.Children.Add(history);
            var files=Row();
            files.Margin=new Thickness(0,0,16,6);
            files.Children.Add(Button(T("Nova","New"),NewDraft));
            files.Children.Add(Button(T("Importar JSON","Import JSON"),ImportPreset));
            files.Children.Add(Button(T("Exportar arquivo","Export file"),delegate { ExportPreset(draft); }));
            commands.Children.Add(files);
            var sharing=Row();
            sharing.Margin=new Thickness(0,0,0,6);
            var copy=Button(T("Copiar código","Copy code"),delegate { CopyCode(draft); });
            Ref(copy,Control.ForegroundProperty,"Accent"); sharing.Children.Add(copy);
            var paste=Button(T("Colar código","Paste code"),ImportCode);
            Ref(paste,Control.ForegroundProperty,"Accent"); sharing.Children.Add(paste);
            commands.Children.Add(sharing); body.Children.Add(commands);

            var workspace=Columns(170,18,-1,18,264);
            workspace.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var inspectorGap=new RowDefinition { Height=new GridLength(0) };
            workspace.RowDefinitions.Add(inspectorGap);
            workspace.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var left=Col();
            left.Children.Add(Label(T("Camadas","Layers"),15,"Text",true));
            var add=Select(T("Adicionar forma","Add shape"),new[] { T("+ Adicionar","+ Add") }.Concat(Catalog.Shapes.Take(10).Select(ShapeName)),0,i=> { if(i>0) AddShape(Catalog.Shapes[i-1]); });
            add.Margin=VerticalSpace(9,6); left.Children.Add(add);
            var image=Button(T("+ PNG ou SVG","+ PNG or SVG"),ImportLayer); image.Margin=VerticalSpace(0,9); left.Children.Add(image);
            for (int i=draft.Layers.Count-1;i>=0;i--)
            {
                int index=i; var layer=draft.Layers[i];
                string layerName=Catalog.Shapes.Contains(layer.Name) ? ShapeName(layer.Name) : layer.Name;
                if (layer.Group.LengthOrZero()>0) layerName=layer.Group+" / "+layerName;
                if (!layer.Visible) layerName="◌  "+layerName;
                if (layer.Locked) layerName="▪  "+layerName;
                var button=Button(layerName,delegate { layerIndex=index; Navigate("editor",false,true); });
                button.ToolTip=layerName; button.Style=(Style)FindResource("NavButton");
                button.Foreground=(Brush)FindResource("Text"); button.HorizontalContentAlignment=HorizontalAlignment.Left;
                var clipped=Label(layerName,12); clipped.TextWrapping=TextWrapping.NoWrap; clipped.TextTrimming=TextTrimming.CharacterEllipsis;
                button.Content=clipped; button.Tag=i==layerIndex ? "Selected" : null; left.Children.Add(button);
            }
            var order = new WrapPanel { Margin=VerticalSpace(8) };
            foreach (var spec in new[] { Tuple.Create("↑",T("Subir","Move up"),(Action)(()=>MoveLayer(1))),
                Tuple.Create("↓",T("Descer","Move down"),(Action)(()=>MoveLayer(-1))),
                Tuple.Create("⧉",T("Duplicar","Duplicate"),(Action)DuplicateLayer),
                Tuple.Create("×",T("Remover","Remove"),(Action)RemoveLayer) })
            {
                var b=Button(spec.Item1,spec.Item3); b.Width=26; b.Padding=new Thickness(0); b.Margin=new Thickness(0,0,3,0);
                b.IsEnabled=CurrentLayer()!=null; b.ToolTip=spec.Item2; AutomationProperties.SetName(b,spec.Item2); order.Children.Add(b);
            }
            left.Children.Add(order);
            var layerList=new ScrollViewer { Content=left, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, MaxHeight=500 };
            Add(workspace,Panel(layerList,13),0);

            var middle=Col();
            var previewHead=new DockPanel { Margin=new Thickness(1,0,2,10) };
            var stamp=Mono(T("VISUALIZAÇÃO AO VIVO","LIVE PREVIEW"),10,"Cyan"); DockPanel.SetDock(stamp,Dock.Right); previewHead.Children.Add(stamp);
            previewHead.Children.Add(Label("Playground",15,"Text",true)); middle.Children.Add(previewHead);
            editorPreview=PreviewOf(draft,330,zoom,gridOn,true);
            ApplyPreviewOptions();
            editorPreview.Focusable=true; AutomationProperties.SetName(editorPreview,T("Canvas da mira","Crosshair canvas"));
            WireCanvas(editorPreview);
            middle.Children.Add(editorPreview);
            var tools=new WrapPanel { Margin=VerticalSpace(9,2) };
            var zoomPick=Select("Zoom",new[] {"1×","2×","3×","4×","6×","8×"},Array.IndexOf(new[]{1d,2,3,4,6,8},zoom),
                index=> { zoom=new[]{1d,2,3,4,6,8}[index]; ApplyPreviewOptions(); }); zoomPick.Width=64;
            var bg=Select(T("Fundo","Background"),new[] {"Lab",T("Escuro","Dark"),T("Claro","Light"),T("Imagem…","Image…")},Array.IndexOf(new[]{"Lab","Dark","Light","Image"},background),
                index=> {
                    if (index==3)
                    {
                        var dialog=new OpenFileDialog { Filter="PNG|*.png", Title=T("Escolher fundo do playground","Choose playground background") };
                        if (dialog.ShowDialog(this)!=true) { Navigate("editor",false,true); return; }
                        previewBackground=Assets.Get(Assets.Import(dialog.FileName));
                    }
                    background=new[]{"Lab","Dark","Light","Image"}[index]; ApplyPreviewOptions();
                }); bg.Width=91;
            var res=Select(T("Resolução simulada","Simulated resolution"),new[] { T("Canvas","Canvas"),"1920 × 1080","2560 × 1440","3440 × 1440","3840 × 2160" },
                Array.IndexOf(new[]{"Canvas","1920","2560","3440","3840"},resolution),
                index=> { resolution=new[]{"Canvas","1920","2560","3440","3840"}[index]; ApplyPreviewOptions(); }); res.Width=121;
            foreach (var tool in new[] {zoomPick,bg,res}) { tool.Margin=new Thickness(0,0,6,6); tools.Children.Add(tool); }
            middle.Children.Add(tools);
            var canvasOptions=new WrapPanel();
            var gridFlag=Check(T("Grade","Grid"),gridOn,b=> { gridOn=b; ApplyPreviewOptions(); });
            var snapFlag=Check("Snap",snap,b=>snap=b);
            foreach (var flag in new[] { gridFlag,snapFlag }) { flag.Margin=new Thickness(0,0,11,0); canvasOptions.Children.Add(flag); }
            coordinates=Mono(""); coordinates.Margin=new Thickness(0,2,0,0); canvasOptions.Children.Add(coordinates); middle.Children.Add(canvasOptions);
            Add(workspace,Panel(middle,14),2);

            xBox=yBox=null;
            var inspector=Col();
            inspector.Children.Add(Label(T("Propriedades","Properties"),15,"Text",true));
            var selected=CurrentLayer();
            if (selected == null)
            {
                var message=Label(T("Adicione uma camada para começar.","Add a layer to get started."),13,"Muted"); message.Margin=VerticalSpace(9); inspector.Children.Add(message);
            }
            else BuildInspector(inspector,selected);
            var inspectorCard=Panel(new ScrollViewer { Content=inspector, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, MaxHeight=500 },14);
            Add(workspace,inspectorCard,4);
            workspace.SizeChanged+=delegate
            {
                bool compact=workspace.ActualWidth<755;
                if(compact==(Grid.GetRow(inspectorCard)==2)) return;
                workspace.ColumnDefinitions[3].Width=new GridLength(compact?0:18);
                workspace.ColumnDefinitions[4].Width=new GridLength(compact?0:264);
                inspectorGap.Height=new GridLength(compact?18:0);
                Grid.SetRow(inspectorCard,compact?2:0);
                Grid.SetColumn(inspectorCard,compact?0:4);
                Grid.SetColumnSpan(inspectorCard,compact?3:1);
            };
            body.Children.Add(workspace);
            var motion=Section(T("Animação e contraste","Motion and contrast"),T("A prévia usa o mesmo desenho da sobreposição.","The preview uses the same drawing as the overlay."));
            AnimationControls(motion);
            var motionCard=Panel(motion,15);
            motionCard.Margin=VerticalSpace(18);
            body.Children.Add(motionCard);
            UpdateEditorButtons();
        }
        private void BuildInspector(StackPanel panel,Layer l)
        {
            panel.Children.Add(Field(T("Nome","Name"),TextField(T("Nome da camada","Layer name"),l.Name,value=>Edit("layer-name",delegate { l.Name=Store.Limit(value,45,ShapeName(l.Shape)); },true),45)));
            panel.Children.Add(Field(T("Grupo","Group"),TextField(T("Grupo da camada","Layer group"),l.Group,value=>Edit("group",delegate { l.Group=Store.Limit(value,35,""); },true),35)));
            var state=Row();
            state.Children.Add(Check(T("Visível","Visible"),l.Visible,b=>Edit("visible",()=>l.Visible=b,true)));
            var locked=Check(T("Bloqueada","Locked"),l.Locked,b=>Edit("locked",()=>l.Locked=b,true)); locked.Margin=new Thickness(8,5,0,5); state.Children.Add(locked);
            panel.Children.Add(state);
            if (!string.IsNullOrEmpty(l.Group))
            {
                var group=Row();
                group.Children.Add(Button(T("Alternar grupo","Toggle group"),()=>Edit("group visible",delegate {
                    bool show=!draft.Layers.Where(layer=>layer.Group==l.Group).All(layer=>layer.Visible);
                    foreach (var member in draft.Layers.Where(layer=>layer.Group==l.Group)) member.Visible=show;
                },true)));
                panel.Children.Add(group);
            }
            var props=Col(); props.IsEnabled=!l.Locked; panel.Children.Add(props);
            bool asset=l.Shape=="PNG" || l.Shape=="SVG";
            if (!asset) props.Children.Add(Field(T("Forma","Shape"),Select(T("Forma","Shape"),Catalog.Shapes.Take(10).Select(ShapeName),Array.IndexOf(Catalog.Shapes,l.Shape),i=>Edit("shape",()=>l.Shape=Catalog.Shapes[i],true))));
            props.Children.Add(Number(T("Tamanho","Size"),l.Size,1,96,.5,v=>Edit("size",()=>l.Size=v)));
            if (!asset)
            {
                if (l.Shape!="Dot") props.Children.Add(Number(T("Espessura","Thickness"),l.Thickness,.5,28,.5,v=>Edit("weight",()=>l.Thickness=v)));
                if (new[]{"Cross","T","X","Corners"}.Contains(l.Shape)) props.Children.Add(Number(T("Vão interno","Gap"),l.Gap,0,48,.5,v=>Edit("gap",()=>l.Gap=v)));
                props.Children.Add(Field(T("Cor","Color"),TextField(T("Cor HEX ou RGB","HEX or RGB color"),l.Color,v=>Edit("color",()=>l.Color=Reticle.NormalizeColor(v)),42)));
                props.Children.Add(Field(T("Gradiente opcional","Optional gradient"),TextField(T("Cor final do gradiente","Gradient end color"),l.Gradient,v=>Edit("gradient",()=>l.Gradient=string.IsNullOrWhiteSpace(v) ? "" : Reticle.NormalizeColor(v)),42)));
                props.Children.Add(Field(T("Cor do contorno","Outline color"),TextField(T("Cor do contorno","Outline color"),l.OutlineColor,v=>Edit("outline-color",()=>l.OutlineColor=Reticle.NormalizeColor(v)),42)));
                props.Children.Add(Number(T("Contorno externo","Outer outline"),l.Outline,0,12,.25,v=>Edit("outline",()=>l.Outline=v)));
                props.Children.Add(Number(T("Borda interna","Inner border"),l.InnerBorder,0,8,.25,v=>Edit("inner",()=>l.InnerBorder=v)));
                props.Children.Add(Number("Glow",l.Glow,0,12,.5,v=>Edit("glow",()=>l.Glow=v)));
                props.Children.Add(Number(T("Sombra","Shadow"),l.Shadow,0,12,.5,v=>Edit("shadow",()=>l.Shadow=v)));
                props.Children.Add(Number(T("Suavização","Soften"),l.Blur,0,8,.5,v=>Edit("blur",()=>l.Blur=v)));
            }
            props.Children.Add(Number(T("Opacidade (%)","Opacity (%)"),l.Opacity,0,100,1,v=>Edit("alpha",()=>l.Opacity=v)));
            props.Children.Add(Number(T("Rotação (°)","Rotation (°)"),l.Rotation,-360,360,1,v=>Edit("rotation",()=>l.Rotation=v)));
            xBox=TextField("X",Format(l.X),v=>Edit("x",()=>l.X=Position(Parse(v),snap)),10);
            yBox=TextField("Y",Format(l.Y),v=>Edit("y",()=>l.Y=Position(Parse(v),snap)),10);
            var coords=Columns(-1,8,-1); Add(coords,Field("X",xBox)); Add(coords,Field("Y",yBox),2); props.Children.Add(coords);
            var align=new WrapPanel();
            align.Children.Add(Check(T("Espelhar X","Mirror X"),l.Mirror,b=>Edit("mirror",()=>l.Mirror=b)));
            align.Children.Add(Button(T("Centralizar","Center"),()=>Edit("center-layer",delegate { l.X=0;l.Y=0; },true)));
            props.Children.Add(align);
        }
        private void AnimationControls(StackPanel body)
        {
            string[] keys={"None","Pulse","Fade","Spin","Grow","Keyframes"};
            var controls=new WrapPanel();
            var kind=Select(T("Animação","Motion"),new[] {T("Sem animação","No motion"),T("Pulsação","Pulse"),"Fade",T("Rotação","Spin"),T("Variação de tamanho","Grow"),"Keyframes"},
                Array.IndexOf(keys,draft.Motion.Kind),index=>Edit("motion-kind",()=>draft.Motion.Kind=keys[index],true));
            kind.Width=155; kind.Margin=new Thickness(0,0,9,7); controls.Children.Add(kind);
            var duration=TextField(T("Duração em segundos","Duration in seconds"),Format(draft.Motion.Seconds),text=>Edit("duration",()=>draft.Motion.Seconds=Store.Clamp(Parse(text),.25,30,2)),8);
            duration.Width=62; duration.Margin=new Thickness(0,0,9,7); controls.Children.Add(duration);
            controls.Children.Add(Check("Loop",draft.Motion.Loop,value=>Edit("loop",()=>draft.Motion.Loop=value)));
            var play=Button(T("Reproduzir prévia","Play preview"),delegate { previewMotion=true; motionTime.Restart(); ConfigureFrame(); });
            play.Margin=new Thickness(12,0,6,7); controls.Children.Add(play);
            controls.Children.Add(Button(T("Pausar prévia","Pause preview"),delegate { previewMotion=false; ApplyPreviewOptions(); ConfigureFrame(); }));
            body.Children.Add(controls);
            if (draft.Motion.Kind=="Keyframes")
            {
                var frameLabel=Label(T("Keyframes · tempo % / escala % / opacidade % / ângulo °","Keyframes · time % / scale % / opacity % / angle °"),12,"Muted");
                body.Children.Add(frameLabel);
                string current=string.Join(Environment.NewLine,draft.Motion.Frames.Select(f=>string.Join(" ",new[]{f.Time,f.Scale,f.Alpha,f.Angle}.Select(v=>v.ToString("0.##",CultureInfo.InvariantCulture)))));
                var keyframes=TextField("Keyframes",current,value=>
                {
                    var result=new List<Keyframe>();
                    foreach(var line in value.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts=line.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                        if(parts.Length!=4) throw new InvalidDataException(T("Use quatro números por linha.","Use four numbers per line."));
                        result.Add(new Keyframe { Time=Store.Clamp(Parse(parts[0]),0,100,0), Scale=Store.Clamp(Parse(parts[1]),25,200,100),
                            Alpha=Store.Clamp(Parse(parts[2]),0,100,100), Angle=Store.Clamp(Parse(parts[3]),-360,360,0) });
                    }
                    if(result.Count>24) throw new InvalidDataException(T("Máximo de 24 keyframes.","Maximum 24 keyframes."));
                    Edit("frames",()=>draft.Motion.Frames=result.OrderBy(f=>f.Time).ToList());
                },900,true);
                keyframes.Height=81; keyframes.Margin=VerticalSpace(5,7); body.Children.Add(keyframes);
                var sample=Button(T("Usar exemplo de keyframes","Use keyframe example"),()=>Edit("frames-example",delegate
                {
                    draft.Motion.Frames=new List<Keyframe> {new Keyframe(),new Keyframe {Time=50,Scale=125,Alpha=50,Angle=45},new Keyframe {Time=100}};
                },true)); sample.HorizontalAlignment=HorizontalAlignment.Left; body.Children.Add(sample);
            }
            var contrast=Button(T("Aplicar sugestão de contraste","Apply contrast suggestion"),delegate
            {
                var layer=CurrentLayer();
                if (layer==null || layer.Locked || layer.Shape=="PNG" || layer.Shape=="SVG")
                    throw new InvalidDataException(T("Selecione uma forma vetorial desbloqueada.","Select an unlocked vector shape."));
                bool light=background=="Light" || (background=="Image" && PreviewLuminance()>.45);
                Edit("contrast",delegate { layer.Color=light ? "#111827" : "#FFFFFF"; layer.OutlineColor=light ? "#FFFFFF" : "#10121A"; layer.Gradient=""; layer.Outline=1; },true);
                Notice(T("Sugestão aplicada ao fundo selecionado.","Suggestion applied for the selected background."));
            }); contrast.Margin=VerticalSpace(5); contrast.HorizontalAlignment=HorizontalAlignment.Left; body.Children.Add(contrast);
        }
        private double PreviewLuminance()
        {
            var image=previewBackground as System.Windows.Media.Imaging.BitmapSource;
            if (image==null) return 0;
            var formatted=new System.Windows.Media.Imaging.FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
            int width=Math.Min(9,formatted.PixelWidth), height=Math.Min(9,formatted.PixelHeight);
            var sample=new byte[width*height*4];
            formatted.CopyPixels(new Int32Rect((formatted.PixelWidth-width)/2,(formatted.PixelHeight-height)/2,width,height),sample,width*4,0);
            double total=0;
            for(int i=0;i<sample.Length;i+=4) total+=(.0722*sample[i]+.7152*sample[i+1]+.2126*sample[i+2])/255;
            return total/(width*height);
        }
        private void ApplyPreviewOptions()
        {
            if (editorPreview==null) return;
            editorPreview.Pose=new Pose(1,1,0); editorPreview.Zoom=zoom; editorPreview.Grid=gridOn;
            editorPreview.BackgroundMode=background; editorPreview.BackgroundImage=previewBackground;
            editorPreview.PixelWidth=resolution=="Canvas"?0:int.Parse(resolution,CultureInfo.InvariantCulture);
            editorPreview.Aspect=resolution=="3440"?3440.0/1440:resolution=="Canvas"?0:16.0/9;
            editorPreview.Refresh();
        }
        private void WireCanvas(Preview preview)
        {
            Point start=new Point();
            double x=0,y=0;
            bool dragging=false;
            preview.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
                var l=CurrentLayer(); if(l==null || l.Locked) return;
                preview.Focus(); preview.CaptureMouse(); start=e.GetPosition(preview); x=l.X; y=l.Y; dragging=false; e.Handled=true;
            };
            preview.MouseMove+=delegate(object sender,MouseEventArgs e) {
                if(!preview.IsMouseCaptured || e.LeftButton!=MouseButtonState.Pressed) return;
                Vector delta=e.GetPosition(preview)-start; var l=CurrentLayer(); if(l==null || l.Locked) return;
                if(!dragging)
                {
                    if(delta.Length<2) return;
                    Checkpoint("drag",true); dragging=true;
                }
                l.X=Position(x+delta.X/Math.Max(.01,preview.RenderScale),snap);
                l.Y=Position(y+delta.Y/Math.Max(.01,preview.RenderScale),snap);
                RefreshDraft();
            };
            preview.MouseLeftButtonUp+=delegate { if(preview.IsMouseCaptured) preview.ReleaseMouseCapture(); };
            preview.KeyDown+=delegate(object sender, KeyEventArgs e) {
                var l=CurrentLayer(); if(l==null || l.Locked) return;
                double step=snap || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?4:1;
                double dx=e.Key==Key.Left?-step:e.Key==Key.Right?step:0, dy=e.Key==Key.Up?-step:e.Key==Key.Down?step:0;
                if(dx==0 && dy==0) return;
                Edit("nudge",delegate { l.X=Position(l.X+dx,snap); l.Y=Position(l.Y+dy,snap); }); e.Handled=true;
            };
        }
        private static double Position(double x,bool grid) { return Store.Clamp(grid?Math.Round(x/4)*4:Math.Round(x,1),-128,128,0); }
        private Layer CurrentLayer() { return layerIndex>=0 && layerIndex<draft.Layers.Count ? draft.Layers[layerIndex] : null; }
        private void AddShape(string shape) { AddLayer(new Layer { Shape=shape,Name=ShapeName(shape) }); }
        private void ImportLayer()
        {
            if(draft.Layers.Count>=32) throw new InvalidDataException(T("Limite de 32 camadas.","Maximum 32 layers."));
            var dialog=new OpenFileDialog { Filter="PNG ou SVG|*.png;*.svg" };
            if(dialog.ShowDialog(this)==true) AddLayer(Assets.Import(dialog.FileName));
        }
        private void AddLayer(Layer layer)
        {
            if(draft.Layers.Count>=32) throw new InvalidDataException(T("Limite de 32 camadas.","Maximum 32 layers."));
            Edit("new-layer",delegate { draft.Layers.Add(layer); layerIndex=draft.Layers.Count-1; },true);
        }
        private void DuplicateLayer()
        {
            var original=CurrentLayer(); if(original==null) return;
            var copy=original.Copy(); copy.Name=Store.Limit(copy.Name,38,"")+T(" cópia"," copy"); copy.Locked=false;
            AddLayer(copy);
        }
        private void MoveLayer(int delta)
        {
            var layer=CurrentLayer(); int index=layerIndex+delta;
            if(layer==null || layer.Locked || index<0 || index>=draft.Layers.Count) return;
            Edit("order",delegate { draft.Layers.RemoveAt(layerIndex); draft.Layers.Insert(index,layer); layerIndex=index; },true);
        }
        private void RemoveLayer()
        {
            var l=CurrentLayer(); if(l==null || l.Locked) return;
            Edit("remove",delegate { draft.Layers.RemoveAt(layerIndex); layerIndex=Math.Min(layerIndex,draft.Layers.Count-1); },true);
        }
        private void Edit(string key,Action action,bool rebuild=false)
        {
            Checkpoint(key,rebuild); action(); RefreshDraft();
            if(rebuild) Navigate("editor",false,true);
        }
        private void Checkpoint(string key,bool force)
        {
            var now=DateTime.UtcNow;
            if(force || key!=lastEdit || (now-lastEditAt).TotalMilliseconds>520)
            {
                undo.Add(Store.Clone(draft)); if(undo.Count>25) undo.RemoveAt(0); redo.Clear();
            }
            lastEdit=key; lastEditAt=now;
        }
        private void RefreshDraft()
        {
            dirty=true;
            if(editorPreview!=null) { editorPreview.Preset=draft; editorPreview.Refresh(); }
            if(coordinates!=null)
            {
                var l=CurrentLayer();
                coordinates.Text=l==null?"":("X "+Format(l.X)+"  /  Y "+Format(l.Y));
                if(l!=null && xBox!=null && !xBox.IsKeyboardFocusWithin) xBox.Text=Format(l.X);
                if(l!=null && yBox!=null && !yBox.IsKeyboardFocusWithin) yBox.Text=Format(l.Y);
            }
            UpdateEditorButtons(); RefreshRuntime(true);
        }
        private void UpdateEditorButtons()
        {
            if(undoButton!=null) undoButton.IsEnabled=undo.Count>0;
            if(redoButton!=null) redoButton.IsEnabled=redo.Count>0;
            if(coordinates!=null)
            {
                var l=CurrentLayer(); coordinates.Text=l==null?"":"X "+Format(l.X)+"  /  Y "+Format(l.Y);
            }
        }
        private void Undo(bool forward)
        {
            var source=forward?redo:undo; var target=forward?undo:redo;
            if(source.Count==0) return;
            target.Add(Store.Clone(draft)); draft=source[source.Count-1]; source.RemoveAt(source.Count-1);
            layerIndex=Math.Min(layerIndex,draft.Layers.Count-1); lastEdit="";
            RefreshDraft(); Navigate("editor",false,true);
        }
        private bool DiscardDraft()
        {
            return !dirty || MessageBox.Show(this,T("Descartar as alterações da mira que ainda não foram salvas?","Discard unsaved crosshair changes?"),
                "Crosshair Studio",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
        }
        private void LoadDraft(Preset p)
        {
            draft=Store.Clone(p); layerIndex=0; dirty=false; undo.Clear(); redo.Clear(); lastEdit="";
        }
        private void NewDraft()
        {
            if(!DiscardDraft()) return;
            var preset=new Preset { Name=T("Nova mira","New crosshair") }; preset.Layers.Add(new Layer { Group="" });
            LoadDraft(preset); RefreshRuntime(true); Navigate("editor",false);
        }
        private void SaveDraft()
        {
            var clean=Store.Validate(Store.Clone(draft));
            int old=data.Presets.FindIndex(p=>p.Id==draft.Id);
            if(old>=0)
            {
                clean.Favorite=data.Presets[old].Favorite;
                if(Store.Encode(data.Presets[old])!=Store.Encode(clean))
                {
                    data.History.Insert(0,new Revision { When=DateTime.Now.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture), Preset=data.Presets[old] });
                    if(data.History.Count>20) data.History.RemoveAt(data.History.Count-1);
                }
                data.Presets[old]=clean;
            }
            else AddPreset(clean);
            dirty=false; ActivatePreset(clean); SaveNow();
            if(!pendingSave) Notice(T("Mira salva e aplicada  /  ","Crosshair saved and applied  /  ")+clean.Name);
        }
        private void AddPreset(Preset p)
        {
            if(data.Presets.Count>=160) throw new InvalidDataException(T("A biblioteca comporta até 160 miras.","The library holds up to 160 crosshairs."));
            data.Presets.Add(p);
        }
        private void ImportPreset()
        {
            var dialog=new OpenFileDialog { Filter="Crosshair Studio JSON|*.json" };
            if(dialog.ShowDialog(this)!=true) return;
            var p=Store.Validate(Store.Decode<Preset>(Store.Read(dialog.FileName,Store.MaxBytes)));
            AcceptImport(p);
        }
        private void AcceptImport(Preset p, bool replaceDraft=false)
        {
            p.Id=Guid.NewGuid().ToString("N"); AddPreset(p); ActivatePreset(p);
            if(page=="editor" && (replaceDraft || !dirty)) { LoadDraft(p); Navigate("editor",false,true); }
            else if(page=="library") Navigate("library",false,true);
        }
        private void ExportPreset(Preset preset)
        {
            var dialog=new SaveFileDialog { Filter=T("Configuração JSON|*.json|PNG transparente|*.png|SVG vetorial|*.svg","JSON configuration|*.json|Transparent PNG|*.png|Vector SVG|*.svg"), FileName="crosshair" };
            if(dialog.ShowDialog(this)!=true) return;
            var p=Store.Validate(Store.Clone(preset));
            switch(Path.GetExtension(dialog.FileName).ToLowerInvariant())
            {
                case ".png": Reticle.ExportPng(p,dialog.FileName); break;
                case ".svg": Store.Atomic(dialog.FileName,Reticle.ExportSvg(p),false); break;
                case ".json": Store.Atomic(dialog.FileName,Store.Encode(p),false); break;
                default: throw new InvalidDataException(T("Escolha JSON, PNG ou SVG.","Choose JSON, PNG or SVG."));
            }
            Notice(T("Mira exportada.","Crosshair exported."));
        }
        private StackPanel Section(string title,string description)
        {
            var stack=Col(); stack.Children.Add(Label(title,17,"Text",true));
            if(!string.IsNullOrEmpty(description))
            {
                var text=Label(description,12,"Muted"); text.Margin=VerticalSpace(4,10); stack.Children.Add(text);
            }
            return stack;
        }
        private StackPanel Field(string title,UIElement control)
        {
            var panel=Col(); panel.Margin=VerticalSpace(5,5); var label=Label(title,11,"Muted",true); label.Margin=VerticalSpace(0,4);
            panel.Children.Add(label); panel.Children.Add(control); return panel;
        }
        private TextBox TextField(string title,string value,Action<string> commit,int max=0,bool multi=false)
        {
            var box=new TextBox { Text=value??"", MaxLength=max, AcceptsReturn=multi, TextWrapping=multi?TextWrapping.Wrap:TextWrapping.NoWrap };
            AutomationProperties.SetName(box,title); box.ToolTip=title;
            if(commit==null) return box;
            Action write=delegate {
                if(box.Text==value) return;
                commit(box.Text); value=box.Text;
            };
            box.LostKeyboardFocus+=delegate { Safe(write); };
            box.KeyDown+=delegate(object s,KeyEventArgs e) {
                if(e.Key==Key.Enter && (!multi || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))) { Safe(write); e.Handled=true; }
                if(e.Key==Key.Escape) { box.Text=value??""; e.Handled=true; }
            };
            return box;
        }
        private ComboBox Select(string title,IEnumerable<string> choices,int selected,Action<int> onSelect)
        {
            var combo=new ComboBox { ItemsSource=choices.ToArray(),SelectedIndex=selected,ToolTip=title };
            AutomationProperties.SetName(combo,title);
            combo.SelectionChanged+=delegate { if(combo.SelectedIndex>=0) Safe(()=>onSelect(combo.SelectedIndex)); };
            return combo;
        }
        private CheckBox Check(string title,bool state,Action<bool> onChange)
        {
            var flag=new CheckBox { Content=title,IsChecked=state };
            flag.Checked+=delegate { Safe(()=>onChange(true)); }; flag.Unchecked+=delegate { Safe(()=>onChange(false)); };
            return flag;
        }
        private FrameworkElement Number(string label,double value,double min,double max,double step,Action<double> onChange)
        {
            var panel=Col(); panel.Margin=VerticalSpace(4,4);
            var line=new DockPanel();
            var field=new TextBox { Text=Format(value), Width=57,MinHeight=26,MaxLength=12,Padding=new Thickness(5,3,5,3),TextAlignment=TextAlignment.Right };
            AutomationProperties.SetName(field,label); DockPanel.SetDock(field,Dock.Right); line.Children.Add(field);
            var text=Label(label,11,"Muted",true); text.VerticalAlignment=VerticalAlignment.Center; line.Children.Add(text); panel.Children.Add(line);
            var slider=new Slider { Minimum=min, Maximum=max, Value=value, SmallChange=step, LargeChange=step*5, IsMoveToPointEnabled=true };
            AutomationProperties.SetName(slider,label);
            bool synchronizing=false;
            slider.ValueChanged+=delegate {
                if(synchronizing) return;
                double quantized=Store.Clamp(Math.Round(slider.Value/step)*step,min,max,value);
                if(quantized==value) return;
                onChange(quantized); value=quantized; field.Text=Format(value);
            };
            Action write=delegate {
                double parsed=Store.Clamp(Parse(field.Text),min,max,value);
                if(parsed!=value) { onChange(parsed); value=parsed; }
                synchronizing=true; slider.Value=parsed; synchronizing=false; field.Text=Format(parsed);
            };
            field.LostKeyboardFocus+=delegate { if(field.Text!=Format(value)) Safe(write); };
            field.KeyDown+=delegate(object s, KeyEventArgs e) {
                if(e.Key==Key.Enter) { Safe(write); e.Handled=true; }
                if(e.Key==Key.Escape) { field.Text=Format(value); e.Handled=true; }
            };
            panel.Children.Add(slider); return panel;
        }
        private static string Format(double value) { return value.ToString("0.##",CultureInfo.InvariantCulture); }
        private double Parse(string value)
        {
            double parsed;
            if(!double.TryParse((value??"").Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed))
                throw new InvalidDataException(T("Informe um número válido.","Enter a valid number."));
            return parsed;
        }
    }
    internal static class StringHelpers
    {
        public static int LengthOrZero(this string value) { return value==null?0:value.Length; }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;

namespace CrosshairStudio
{
    internal static class SelfTest
    {
        private static readonly List<string> Report = new List<string>();
        private static int failed;
        private static void Check(bool condition,string context) { if(!condition) throw new InvalidOperationException(context); }
        private static void Reject(Action attempt)
        {
            try { attempt(); } catch (Exception e) { if(e is OutOfMemoryException) throw; return; }
            throw new InvalidOperationException("Expected invalid input rejection");
        }
        private static void Test(string name, Action test)
        {
            try { test(); Report.Add("PASS " + name); }
            catch(DesktopUnavailable e) { Report.Add("SKIP "+name+": "+e.Message); }
            catch(Exception e) { Report.Add("FAIL "+name+" "+e); failed++; }
        }
        private sealed class DesktopUnavailable : Exception { public DesktopUnavailable(string message) : base(message) {} }
        public static int Run(string report,bool desktop)
        {
            var root=Path.GetDirectoryName(Path.GetFullPath(report));
            var tmp=Path.Combine(root,"test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tmp);
            try
            {
                Test("settings save, profiles and recovery",delegate {
                    string f=Path.Combine(tmp,"settings.json");
                    var config=Settings.Fresh(); config.Profiles.Add(new Profile { Name="Teste",Executables=new List<string>{"sample.exe"},PresetId="orbit",X=-28,Scale=75, Monitor=@"\\.\DISPLAY2" });
                    config.AutoProfile=true;
                    var oldSettings=Store.Encode(config).Replace("\"Executables\":[\"sample.exe\"]","\"Executable\":\"sample.exe\"");
                    Check(oldSettings.Contains("\"Executable\":\"sample.exe\""),"test fixture uses legacy property");
                    Store.Atomic(f,oldSettings,true);
                    config.Profiles[1].Executables.Add(" C:/Games/Other.exe ");
                    config.Profiles[1].Executables.Add("SAMPLE.EXE");
                    config.Profiles[1].X=-30; Store.Atomic(f,Store.Encode(config),true);
                    string notice;
                    var current=Store.Load(f,out notice);
                    Check(current.Profiles[1].X==-30 && notice=="","roundtrip");
                    Check(current.Profiles[1].Executables.SequenceEqual(new[]{"sample.exe",@"C:\Games\Other.exe"}),"normalized and deduplicated list");
                    Check(Desktop.MatchProfile(current,@"D:\Apps\SAMPLE.EXE")==1 &&
                        Desktop.MatchProfile(current,@"C:\Games\Other.exe")==1 &&
                        Desktop.MatchProfile(current,@"D:\Games\Other.exe")==0,"same profile across name and path");
                    current.Profiles[0].Executables.Add("sample.exe");
                    Check(Desktop.MatchProfile(current,@"D:\Apps\SAMPLE.EXE")==0,"first matching profile retains priority");
                    current.ManualProfile=1;
                    Check(Desktop.MatchProfile(current,"different.exe")==1,"fallback to last manual profile");
                    current.AutoProfile=false;
                    Check(Desktop.MatchProfile(current,@"D:\Apps\SAMPLE.EXE")==1,"manual profile ignores association");
                    File.WriteAllText(f,"{bad");
                    var recovered=Store.Load(f,out notice);
                    Check(recovered.Profiles[1].X==-28 && recovered.Profiles[1].Monitor==@"\\.\DISPLAY2" && notice.Length>0,"recovery");
                    Check(recovered.Profiles[1].Executables.SequenceEqual(new[]{"sample.exe"}),"legacy association migrated");
                    Check(Desktop.MatchProfile(recovered,@"C:\Test\SAMPLE.EXE")==1 && Desktop.MatchProfile(recovered,"different.exe")==0,"foreground matching");
                    Check(!Desktop.Matches(@"C:\First\sample.exe",@"C:\Other\sample.exe"),"full path exact match");
                });
                Test("portable share code and invalid input",delegate {
                    var p=Catalog.Create()[0]; var copy=Store.Unshare(Store.Share(p));
                    Check(copy.Layers.Count==2 && copy.Layers[0].Color==p.Layers[0].Color,"share code preserved layers");
                    Reject(()=>Store.Unshare("CS1-@@@"));
                    Reject(()=>Store.Decode<Preset>("{\"__type\":\"System.Version\"}"));
                    Reject(()=>Store.Decode<Preset>("{\"\\u005f\\u005ftype\":\"System.Version\"}"));
                    Reject(()=>Store.Decode<Preset>("{\"Layers\":[{\"\\u005f\\u005ftype\":\"System.Version\"}]}"));
                    p.Layers[0].Color="url(https://example.test)";
                    Reject(()=>Store.Validate(p));
                    Check(Reticle.NormalizeColor("rgba(4,5,6,0.5)")=="#04050680","RGBA");
                });
                Test("compact CSX codes, legacy CS1 and exact roundtrip",delegate {
                    var prism=Catalog.Create().Single(p=>p.Id=="prism");
                    string legacy="CS1-"+Convert.ToBase64String(Encoding.UTF8.GetBytes(Store.Encode(prism))).TrimEnd('=').Replace('+','-').Replace('/','_');
                    string code=Store.Share(prism);
                    Check(code.StartsWith("CSX-") && code.Length<120 && code.Length*8<legacy.Length,"compact Prism code");
                    var fromLegacy=Store.Unshare(legacy);
                    Check(fromLegacy.Id==prism.Id && fromLegacy.Layers[1].Outline==.75,"legacy fields");
                    Check(Store.Share(Store.Unshare(" "+code.ToLowerInvariant().Replace("-","-\n").Replace("csx-\n","csx-")+" "))==code,"paste variations");
                    int last=code.Length-2;
                    Reject(()=> Store.Unshare(code.Substring(0,last)+(code[last]=='0'?'1':'0')+code.Substring(last+1)));
                    Reject(()=> Store.Unshare("CSGO-TpORA-p9Ley-TLQ3P-HzXJY-U9z6A"));
                    Reject(()=> Store.Unshare("0;s;1;P;c;5;o;1"));
                    Report.Add("INFO Prism code "+code.Length+" chars (legacy "+legacy.Length+"): "+code);
                    prism.Name="Prism / Nós 🎯"; prism.Category="Custom category"; prism.Favorite=true;
                    var layer=prism.Layers[0];
                    layer.Gradient="#FF0080F0"; layer.OutlineColor="#05192832"; layer.Color="#374BA0CC";
                    layer.Group="Sobreposição"; layer.Visible=false; layer.Locked=true; layer.Mirror=true;
                    layer.Y=-7.1275; layer.X=5.39; layer.Rotation=-360;
                    layer.Blur=2; layer.Glow=2.5; layer.Shadow=1.25; layer.InnerBorder=.5; layer.Opacity=37.125;
                    prism.Motion=new Motion { Kind="Keyframes", Loop=false, Seconds=1.835 };
                    prism.Motion.Frames.Add(new Keyframe { Time=12.5, Scale=155.3, Alpha=59.512, Angle=-32.19 });
                    var copy=Store.Unshare(Store.Share(prism));
                    Check(copy.Id!=prism.Id,"portable ID");
                    copy.Id=prism.Id;
                    Check(Store.Encode(copy)==Store.Encode(Store.Validate(prism)),"full fidelity");
                });
                Test("keyframes, playback and keyboard chords",delegate {
                    var m=new Motion { Kind="Keyframes", Seconds=2, Loop=false, Frames=new List<Keyframe> { new Keyframe(),new Keyframe {Time=100, Scale=140, Alpha=20, Angle=90} } };
                    var pose=Reticle.Evaluate(m,1,true);
                    Check(Math.Abs(pose.Scale-1.2)<.001 && Math.Abs(pose.Alpha-.6)<.001 && pose.Angle==45,"interpolation");
                    Check(Reticle.Evaluate(m,9,true).Angle==90 && Reticle.Evaluate(m,1,false).Angle==0,"end and pause");
                    uint mod,key; Check(Hotkeys.TryParse("Alt+Ctrl+F8",out mod,out key) && mod==3 && key==119,"registered chord encoding");
                    Check(!Hotkeys.TryParse("Ctrl+Ctrl+A",out mod,out key) && !Hotkeys.TryParse("Alt+F12",out mod,out key),"blocked keys");
                    var center=Desktop.Center(new Drawing.Rectangle(-2560,-480,2560,1440),4,-10);
                    Check(center.X==-1276 && center.Y==230,"negative virtual coordinates");
                });
                Test("PNG transparency, SVG export and SVG import controls",delegate {
                    var p=Catalog.Create()[0];
                    p.Layers[0].Gradient="#FF0000";
                    string png=Path.Combine(tmp,"good.png");
                    Reticle.ExportPng(p,png);
                    var image=(BitmapSource)Assets.Get(Assets.Import(png));
                    var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
                    byte[] corner=new byte[4]; converted.CopyPixels(new Int32Rect(0,0,1,1),corner,4,0);
                    Check(corner[3]==0,"transparent corner");
                    converted.CopyPixels(new Int32Rect(image.PixelWidth/2,image.PixelHeight/2,1,1),corner,4,0);
                    Check(corner[3]>200,"opaque center");
                    string svg=Reticle.ExportSvg(p);
                    var doc=System.Xml.Linq.XDocument.Parse(svg);
                    Check(doc.Descendants().Any(e=>e.Name.LocalName=="linearGradient") && doc.Descendants().Any(e=>e.Name.LocalName=="path"),"valid vector export: "+svg);
                    string input=Path.Combine(tmp,"test.svg");
                    File.WriteAllText(input,"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 32 32\"><g transform=\"translate(2,3)\"><circle cx=\"14\" cy=\"13\" r=\"2\" fill=\"#22d3ee\"/></g></svg>");
                    var imported=Assets.Import(input); p.Layers.Add(imported);
                    Check(Reticle.Extent(Reticle.Scene(p,new Pose(1,1,0)))>10,"vector is rendered");
                    p.Layers.Add(Assets.Import(png));
                    var shared=Store.Unshare(Store.Share(p));
                    Check(shared.Layers[2].Image==imported.Image && shared.Layers[3].Image==p.Layers[3].Image,"shared image assets");
                    Reject(()=>Reticle.ExportSvg(p));
                    File.WriteAllText(input,"<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///test'>]><svg>&x;</svg>");
                    Reject(()=>Assets.Import(input));
                    File.WriteAllText(input,"<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"https://example.test/im.png\"/></svg>");
                    Reject(()=>Assets.Import(input));
                });
                if(desktop)
                {
                    Test("WPF pages, editor undo/redo, configuration",delegate {
                        using (var scope=new WindowCheck(Path.Combine(tmp,"window.json"))) scope.Window.RunPageChecks(root);
                    });
                    Test("Win32 overlay pixels, transparency, click-through and no activation",DesktopOverlay);
                    Test("OS-wide hotkey registration and conflict",NativeHotkeys);
                    Test("live editor and native overlay integration",delegate { LiveEditor(tmp); });
                    Test("idle overlay resource sample",delegate { MeasureIdle(tmp); });
                }
                else Report.Add("SKIP interactive desktop checks (pass --desktop)");
            }
            finally
            {
                File.WriteAllLines(report,Report,Encoding.UTF8);
                Directory.Delete(tmp,true);
            }
            return failed==0 ? 0 : 1;
        }
        private static void NativeHotkeys()
        {
            using(var first=new Overlay())
            using(var second=new Overlay())
            {
                const uint mods=2|1|4|0x4000;
                uint key=0;
                foreach(uint candidate in new uint[]{0x87,0x86,0x85,0x50,0x47,0x54})
                    if(Native.RegisterHotKey(first.Handle,1,mods,candidate)) { key=candidate; break; }
                if(key==0 && System.Runtime.InteropServices.Marshal.GetLastWin32Error()==5)
                    throw new DesktopUnavailable("Windows denies RegisterHotKey on this test desktop (Win32 5)");
                Check(key!=0,"global registration, Win32 error "+System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                try { Check(!Native.RegisterHotKey(second.Handle,2,mods,key),"conflict recognized by Windows"); }
                finally { Native.UnregisterHotKey(first.Handle,1); }
                Check(Native.RegisterHotKey(second.Handle,2,mods,key),"global shortcut released");
                Native.UnregisterHotKey(second.Handle,2);
            }
        }
        private static void DesktopOverlay()
        {
            using (var window=new NativeTestWindow())
            using (var overlay=new Overlay())
            {
                var screen=Desktop.Monitor("");
                var center=Desktop.Center(screen.Bounds,0,0);
                window.Form.Location=new Drawing.Point(center.X-63,center.Y-63);
                window.Form.Show();
                window.Form.Refresh();
                Pump(180);
                var point=new NativePoint {X=center.X,Y=center.Y};
                var foreground=Native.GetForegroundWindow();
                var p=new Preset(); p.Layers.Add(new Layer { Shape="Dot",Color="#00FF00",Size=15,Outline=0 });
                overlay.Draw(Reticle.Scene(p,new Pose(1,1,0)),new Profile(),0);
                System.Windows.Forms.Application.DoEvents();
                Pump(280);
                Check(Native.IsWindowVisible(overlay.Handle),"overlay Windows visibility");
                long style=Native.GetWindowLongPtr(overlay.Handle,-20).ToInt64();
                Check((style & 0x8080028)==0x8080028,"layered, topmost, no-activate, transparent style");
                var after=Native.GetForegroundWindow();
                Check(after!=overlay.Handle,"overlay took focus");
                if(foreground==window.Form.Handle) Check(after==foreground,"overlay changed focus: "+foreground+" to "+after);
                Check(Native.WindowFromPoint(point)==window.Form.Handle,"underlying window receives hit at opaque center");
                using(var sample=new Drawing.Bitmap(23,23))
                {
                    using(var graphics=Drawing.Graphics.FromImage(sample))
                    {
                        IntPtr screenDc=Native.GetDC(IntPtr.Zero), destination=graphics.GetHdc();
                        try { Check(Native.BitBlt(destination,0,0,23,23,screenDc,center.X-11,center.Y-11,0x40cc0020),"desktop screen sample"); }
                        finally { graphics.ReleaseHdc(destination); Native.ReleaseDC(IntPtr.Zero,screenDc); }
                    }
                    var green=sample.GetPixel(11,11); var magenta=sample.GetPixel(0,0);
                    Check(green.G>220 && green.R<30 && magenta.R>200 && magenta.B>200,
                        "actual screen pixels: "+green+" and "+magenta);
                }
                overlay.Hide(); Check(!Native.IsWindowVisible(overlay.Handle),"hidden overlay HWND");
            }
        }
        private sealed class NativeTestWindow : IDisposable
        {
            public readonly System.Windows.Forms.Form Form = new System.Windows.Forms.Form {
                Size=new Drawing.Size(126,126), StartPosition=System.Windows.Forms.FormStartPosition.Manual, FormBorderStyle=System.Windows.Forms.FormBorderStyle.None,
                BackColor=Drawing.Color.Magenta, TopMost=true, ShowInTaskbar=false };
            public void Dispose() { Form.Dispose(); }
        }
        private static void LiveEditor(string tmp)
        {
            string file=Path.Combine(tmp,"live.json");
            var settings=Settings.Fresh();
            foreach (var action in settings.Hotkeys.Keys.ToList()) settings.Hotkeys[action]="";
            Store.Atomic(file,Store.Encode(settings),false);
            using(var scope=new WindowCheck(file,false)) scope.Window.VerifyLiveEditor();
            string warning;
            var saved=Store.Load(file,out warning);
            Check(warning=="" && saved.Overlay && saved.Presets[0].Layers[1].Color=="#FFE082","live result persisted");
        }
        private static void MeasureIdle(string tmp)
        {
            var settings=Settings.Fresh(); settings.Overlay=true;
            var file=Path.Combine(tmp,"perf.json"); Store.Atomic(file,Store.Encode(settings),false);
            using(var window=new WindowCheck(file,false))
            using(var process=Process.GetCurrentProcess())
            {
                window.Window.StartInTray(); Pump(350);
                int draws=window.Window.RenderedFrames;
                Check(draws>=1,"static overlay rendered before measurement");
                var start=process.TotalProcessorTime; var elapsed=Stopwatch.StartNew();
                Pump(1550);
                process.Refresh();
                Check(window.Window.RenderedFrames==draws,"static overlay produced redraws during idle");
                double cpu=(process.TotalProcessorTime-start).TotalMilliseconds/elapsed.Elapsed.TotalMilliseconds*100;
                Report.Add("INFO tray/static sampling "+elapsed.ElapsedMilliseconds+" ms; CPU "+cpu.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"% of one logical core; working set "+Math.Round(process.WorkingSet64/1048576d)+" MiB; redraws 0");
            }
        }
        private sealed class WindowCheck : IDisposable
        {
            public readonly StudioWindow Window;
            public WindowCheck(string file,bool test=true)
            {
                Window=new StudioWindow(file,test); Window.ShowActivated=false; Window.Show(); Pump(100);
            }
            public void Dispose() { Window.CloseForTest(); Pump(20); }
        }
        internal static void Pump(int millis)
        {
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(millis) };
            timer.Tick+=delegate { timer.Stop(); frame.Continue=false; };
            timer.Start(); Dispatcher.PushFrame(frame);
        }
        public static int Previews(string folder)
        {
            Directory.CreateDirectory(folder);
            var settingsPath=Path.Combine(folder,"preview-"+Guid.NewGuid().ToString("N")+".json");
            using(var scope=new WindowCheck(settingsPath)) scope.Window.RunPageChecks(folder);
            return 0;
        }
    }

    internal partial class StudioWindow
    {
        internal int RenderedFrames { get { return overlay.Frames; } }
        internal void CloseForTest() { exiting=true; dirty=false; Close(); }
        internal void VerifyLiveEditor()
        {
            if(overlay.Visible) throw new InvalidOperationException("overlay started unexpectedly");
            Toggle();
            if(!overlay.Visible) throw new InvalidOperationException("overlay toggle failed");
            Navigate("editor",false);
            layerIndex=1;
            int before=overlay.Frames;
            Edit("test color",()=>CurrentLayer().Color="#FFE082");
            if(!overlay.Visible || overlay.Frames<=before) throw new InvalidOperationException("editor did not redraw the live overlay");
            SaveDraft();
            if(pendingSave || ActivePreset().Layers[1].Color!="#FFE082") throw new InvalidOperationException("editor action was not saved");
        }
        internal void RunPageChecks(string folder)
        {
            data.Profiles.Add(new Profile { Name="Segundo perfil",PresetId=data.Presets[1].Id });
            Navigate("home",false); UpdateLayout();
            var picker=FindNamedControl<ComboBox>((DependencyObject)scroll.Content,"Trocar perfil");
            if(picker==null) throw new InvalidOperationException("missing overview profile selector");
            picker.SelectedIndex=2;
            if(data.AutoProfile || data.ManualProfile!=1 || ActiveProfile().Name!="Segundo perfil" || ActivePreset()!=data.Presets[1])
                throw new InvalidOperationException("overview profile was not activated");
            picker=FindNamedControl<ComboBox>((DependencyObject)scroll.Content,"Trocar perfil");
            picker.SelectedIndex=0;
            if(!data.AutoProfile) throw new InvalidOperationException("automatic profile selection was not restored");
            SelectProfile(0,true);
            PaintPreview(Path.Combine(folder,"dashboard.png"));
            Navigate("editor",false);
            PaintPreview(Path.Combine(folder,"editor.png"));
            var editor=(StackPanel)scroll.Content;
            if(FindNamedControl<Button>(editor,"Copiar código")==null || FindNamedControl<Button>(editor,"Colar código")==null)
                throw new InvalidOperationException("missing editor sharing actions");
            var workspace=(Grid)editor.Children[editor.Children.Count-2];
            var motionCard=(FrameworkElement)editor.Children[editor.Children.Count-1];
            double gap=motionCard.TranslatePoint(new Point(),editor).Y-workspace.TranslatePoint(new Point(),editor).Y-workspace.ActualHeight;
            if(gap<17) throw new InvalidOperationException("motion card touches the editor workspace");
            double initialWidth=Width;
            Width=MinWidth; UpdateLayout(); SelfTest.Pump(30);
            if(Grid.GetRow(workspace.Children[2])!=2) throw new InvalidOperationException("inspector does not reflow at minimum width");
            Width=initialWidth; UpdateLayout();
            int old=draft.Layers.Count;
            AddShape("Ring");
            if(draft.Layers.Count!=old+1) throw new InvalidOperationException("editor add");
            Undo(false);
            if(draft.Layers.Count!=old) throw new InvalidOperationException("editor undo");
            Undo(true);
            if(draft.Layers.Count!=old+1) throw new InvalidOperationException("editor redo");
            CurrentLayer().Locked=true;
            RemoveLayer();
            if(draft.Layers.Count!=old+1) throw new InvalidOperationException("layer lock");
            SaveDraft();
            if(pendingSave || data.History.Count!=1 || ActivePreset().Layers.Count!=old+1)
                throw new InvalidOperationException("save integration");
            foreach(var view in new[]{"library","compare","settings"}) { Navigate(view,false); UpdateLayout(); }
            VerifyProfileControls();
            data.Light=true; ApplyTheme(); UpdateLayout();
            data.English=true; BuildChrome(); Navigate("home",false); UpdateLayout();
        }
        private void VerifyProfileControls()
        {
            Navigate("profiles",false); UpdateLayout();
            var pageRoot=(DependencyObject)scroll.Content;
            var input=FindNamedControl<TextBox>(pageRoot,"Nome ou caminho do executável");
            var add=FindNamedControl<Button>(pageRoot,"Adicionar");
            if(input==null || add==null) throw new InvalidOperationException("missing profile executable controls");
            var click=System.Windows.Controls.Primitives.ButtonBase.ClickEvent;
            input.Text="alpha.exe"; add.RaiseEvent(new RoutedEventArgs(click));
            input.Text=@"C:\Games\beta.exe"; add.RaiseEvent(new RoutedEventArgs(click));
            input.Text="ALPHA.EXE"; add.RaiseEvent(new RoutedEventArgs(click));
            input.Text="notes.txt"; add.RaiseEvent(new RoutedEventArgs(click));
            var profile=data.Profiles[editingProfile];
            if(!profile.Executables.SequenceEqual(new[]{"alpha.exe",@"C:\Games\beta.exe"}))
                throw new InvalidOperationException("profile executable list did not accumulate unique .exe entries");
            profile.OnlyForeground=true; data.Overlay=true;
            foreground=@"D:\Games\alpha.exe";
            if(!ShouldShow()) throw new InvalidOperationException("first associated app did not show overlay");
            foreground=@"C:\Games\beta.exe";
            if(!ShouldShow()) throw new InvalidOperationException("second associated app did not show overlay");
            foreground=@"D:\Games\beta.exe";
            if(ShouldShow()) throw new InvalidOperationException("unassociated app showed overlay");
            var remove=FindNamedControl<Button>(pageRoot,"Remover alpha.exe");
            if(remove==null) throw new InvalidOperationException("missing per-executable remove action");
            remove.RaiseEvent(new RoutedEventArgs(click));
            SaveNow();
            string warning;
            if(!Store.Load(file,out warning).Profiles[editingProfile].Executables.SequenceEqual(new[]{@"C:\Games\beta.exe"}) || warning!="")
                throw new InvalidOperationException("removed executable did not stay removed after reload");
            var navButton=nav["profiles"];
            var hover=navButton.Template.FindName("Hover",navButton) as Border;
            var surface=navButton.Template.FindName("Surface",navButton) as Border;
            if(hover==null || surface==null || !hover.RenderSize.Equals(surface.RenderSize) ||
                !hover.RenderSize.Equals(navButton.RenderSize))
                throw new InvalidOperationException("button hover does not cover the full selected row");
        }
        private static TControl FindNamedControl<TControl>(DependencyObject parent,string name) where TControl: Control
        {
            var control=parent as TControl;
            if(control!=null && AutomationProperties.GetName(control)==name) return control;
            foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                var match=FindNamedControl<TControl>(child,name);
                if(match!=null) return match;
            }
            return null;
        }
        private void PaintPreview(string filePath)
        {
            UpdateLayout(); SelfTest.Pump(60);
            var image=new RenderTargetBitmap((int)shell.ActualWidth,(int)shell.ActualHeight,96,96,PixelFormats.Pbgra32);
            image.Render(shell);
            var encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using(var stream=File.Create(filePath)) encoder.Save(stream);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CrosshairStudio
{
    internal partial class StudioWindow : Window
    {
        private readonly string file;
        private Settings data;
        private readonly Overlay overlay;
        private readonly DispatcherTimer poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        private readonly DispatcherTimer frame = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer saveDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(460) };
        private readonly Stopwatch motionTime = Stopwatch.StartNew();
        private readonly Dictionary<string,Button> nav = new Dictionary<string,Button>();
        private readonly Grid shell = new Grid();
        private readonly Grid header = new Grid();
        private readonly ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly TextBlock feedback = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly Button status = new Button();
        private readonly TextBlock context = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly List<Preview> animatedPreviews = new List<Preview>();
        private Forms.NotifyIcon tray;
        private Forms.ToolStripMenuItem trayToggle;
        private Hotkeys hotkeys;
        private string page = "home", foreground = "", lastLog = "";
        private int effective;
        private bool exiting, pendingSave, drawingError;
        private bool motionRunning;
        private Preset lastRendered;
        private int reservation;
        private bool previewMotion = true;
        private Preset draft;
        private int layerIndex;
        private bool dirty;
        private readonly List<Preset> undo = new List<Preset>();
        private readonly List<Preset> redo = new List<Preset>();
        private readonly bool testMode;
        private static readonly string[] Actions = {"toggle","next","previous","editor","center","monitor","profile","animate"};

        public StudioWindow(string settingsFile, bool test = false)
        {
            file = settingsFile; testMode = test;
            string warning;
            data = Store.Load(file,out warning);
            effective = data.ManualProfile;
            overlay = new Overlay();
            Title = "Crosshair Studio";
            string fontFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts") + Path.DirectorySeparatorChar;
            FontFamily = Directory.Exists(fontFolder) ? new FontFamily(new Uri(fontFolder),"./#Inter") : new FontFamily("Segoe UI");
            Width = 1220; Height = 850; MinWidth = 950; MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            WindowChrome.SetWindowChrome(this,new WindowChrome { CaptionHeight=0, ResizeBorderThickness=new Thickness(6), GlassFrameThickness=new Thickness(0), CornerRadius=new CornerRadius(0) });
            using (Stream theme = Assembly.GetExecutingAssembly().GetManifestResourceStream("CrosshairStudio.Theme"))
                Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(theme));
            using (var icon = AppIcon()) Icon = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            SetResourceReference(BackgroundProperty,"Base");
            SetResourceReference(ForegroundProperty,"Text");
            Ref(shell,System.Windows.Controls.Panel.BackgroundProperty,"Base");
            ApplyTheme();
            Content = shell;
            BuildChrome();
            saveDelay.Tick += delegate { saveDelay.Stop(); SaveNow(); };
            poll.Tick += delegate { Poll(); };
            frame.Tick += delegate { TickFrame(); };
            SourceInitialized += delegate { if (!testMode) { hotkeys = new Hotkeys(new WindowInteropHelper(this).Handle, RunAction); hotkeys.RegisterAll(data.Hotkeys); CreateTray(); } };
            Loaded += delegate { if (!testMode) RefreshRuntime(true); };
            StateChanged += delegate
            {
                if (WindowState == WindowState.Minimized && data.ToTray && !testMode) Hide();
                ConfigureFrame();
            };
            IsVisibleChanged += delegate { ConfigureFrame(); };
            PreviewKeyDown += WindowKeys;
            Closing += OnClosing;
            if (!testMode) SystemEvents.DisplaySettingsChanged += DisplayChanged;
            Navigate("home",false);
            if (warning.Length > 0) Notice(warning);
        }

        private string T(string pt, string en) { return data.English ? en : pt; }
        private static Brush ColorBrush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        private void ApplyTheme()
        {
            string[] keys = {"Base","Sidebar","Panel","Raised","Hover","Line","Text","Muted","Accent","Primary","PrimaryHover","Cyan","Active","On"};
            string[] lite = {"#EDF0F7","#F4F5FA","#FFFFFF","#EBEDF6","#DADDEF","#D4D8E6","#17192A","#545A71","#6534C4","#7045D5","#8053E5","#00728B","#EAE2FF","#1D7444"};
            for (int i = 0; i < keys.Length; i++)
            {
                if (data.Light) Resources[keys[i]] = ColorBrush(lite[i]); else Resources.Remove(keys[i]);
            }
            shell.LayoutTransform = new ScaleTransform(data.InterfaceScale / 100, data.InterfaceScale / 100);
        }
        private static void Ref(FrameworkElement e, DependencyProperty p, string name) { e.SetResourceReference(p,name); }
        private TextBlock Label(string text, double size = 14, string color = "Text", bool bold = false)
        {
            var label = new TextBlock { Text=text, FontSize=size, FontWeight=bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping=TextWrapping.Wrap };
            Ref(label,TextBlock.ForegroundProperty,color); return label;
        }
        private TextBlock Mono(string text, double size = 11, string color = "Muted")
        {
            var label = Label(text,size,color);
            label.FontFamily = new FontFamily("Consolas"); return label;
        }
        private Button Button(string title, Action click, bool primary = false)
        {
            var button = new Button { Content = title };
            AutomationProperties.SetName(button,title);
            if (primary) button.Style = (Style)FindResource("PrimaryButton");
            button.Click += delegate { Safe(click); }; return button;
        }
        private Button IconButton(string icon, string title, Action click)
        {
            var b = Button(title,click);
            var row = Row();
            var image = Icons.Get(icon,15); image.Margin = new Thickness(0,0,8,0); row.Children.Add(image);
            var label = new TextBlock { Text=title, FontSize=13 }; label.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground") { Source=b });
            row.Children.Add(label); b.Content = row; return b;
        }
        private Border Panel(UIElement content, double padding = 16)
        {
            var border = new Border { Child=content, Padding=new Thickness(padding), CornerRadius=new CornerRadius(12), BorderThickness=new Thickness(1) };
            Ref(border,Border.BackgroundProperty,"Panel"); Ref(border,Border.BorderBrushProperty,"Line"); return border;
        }
        private static StackPanel Row() { return new StackPanel { Orientation=Orientation.Horizontal }; }
        private static StackPanel Col() { return new StackPanel(); }
        private static Thickness VerticalSpace(double top, double bottom = 0) { return new Thickness(0,top,0,bottom); }
        private static void Add(Grid grid, UIElement child, int column = 0, int row = 0)
        { Grid.SetColumn(child,column); Grid.SetRow(child,row); grid.Children.Add(child); }
        private static Grid Columns(params double[] widths)
        {
            var grid = new Grid();
            foreach (double width in widths)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width < 0 ? new GridLength(-width,GridUnitType.Star) : new GridLength(width) });
            return grid;
        }
        private void BuildChrome()
        {
            var oldStatus=status.Parent as StackPanel; if(oldStatus!=null) oldStatus.Children.Remove(status);
            var oldContext=context.Parent as StackPanel; if(oldContext!=null) oldContext.Children.Remove(context);
            var oldFooter=feedback.Parent as Border; if(oldFooter!=null) oldFooter.Child=null;
            var oldStage=scroll.Parent as DockPanel; if(oldStage!=null) oldStage.Children.Remove(scroll);
            var oldHeader=header.Parent as Border; if(oldHeader!=null) oldHeader.Child=null;
            nav.Clear(); shell.Children.Clear(); shell.ColumnDefinitions.Clear(); shell.RowDefinitions.Clear();
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(203) });
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            shell.RowDefinitions.Add(new RowDefinition { Height=new GridLength(58) });
            shell.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
            header.Children.Clear(); header.ColumnDefinitions.Clear(); header.Background = Brushes.Transparent;
            header.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var headerLine = new Border { BorderThickness=new Thickness(0,0,0,1), Child=header };
            Ref(headerLine,Border.BackgroundProperty,"Sidebar"); Ref(headerLine,Border.BorderBrushProperty,"Line");
            Add(shell,headerLine); Grid.SetColumnSpan(headerLine,2);
            var wordmark = Row(); wordmark.Margin = new Thickness(19,0,0,0); wordmark.VerticalAlignment=VerticalAlignment.Center;
            var brand = new Border { Width=31,Height=31,CornerRadius=new CornerRadius(9),Background=ColorBrush("#292040"), BorderBrush=ColorBrush("#523C78"), BorderThickness=new Thickness(1), Child=Logo() };
            wordmark.Children.Add(brand);
            var mark = Label("Crosshair Studio",15,"Text",true); mark.Margin=new Thickness(10,0,0,0); mark.VerticalAlignment=VerticalAlignment.Center; wordmark.Children.Add(mark);
            Add(header,wordmark);
            header.MouseLeftButtonDown -= DragHeader; header.MouseLeftButtonDown += DragHeader;
            var right = Row(); right.VerticalAlignment=VerticalAlignment.Center; right.Margin = new Thickness(0,0,8,0);
            status.Margin=new Thickness(0,0,16,0); status.Padding=new Thickness(11,4,11,4); status.MinHeight=27;
            status.Click -= ToggleHeader; status.Click += ToggleHeader; right.Children.Add(status);
            right.Children.Add(WindowButton("−",T("Minimizar","Minimize"),delegate { WindowState=WindowState.Minimized; }));
            right.Children.Add(WindowButton("□",T("Maximizar ou restaurar","Maximize or restore"),Maximize));
            right.Children.Add(WindowButton("×",T("Fechar","Close"),Close));
            Add(header,right,1);

            var sidebar = new DockPanel { LastChildFill=true };
            var sidebarBorder = new Border { Child=sidebar,Padding=new Thickness(13,16,13,14),BorderThickness=new Thickness(0,0,1,0) };
            Ref(sidebarBorder,Border.BorderBrushProperty,"Line"); Ref(sidebarBorder,Border.BackgroundProperty,"Sidebar");
            Add(shell,sidebarBorder,0,1);
            var foot = Col();
            Ref(context,TextBlock.ForegroundProperty,"Muted"); context.FontSize=11; context.Margin=new Thickness(3,7,3,3);
            var version = Mono(Assembly.GetExecutingAssembly().GetName().Version.ToString(3)+"  /  WINDOWS",10); version.Margin = new Thickness(3,3,0,0);
            foot.Children.Add(context); foot.Children.Add(version);
            DockPanel.SetDock(foot,Dock.Bottom); sidebar.Children.Add(foot);
            var links = Col();
            AddNav(links,"home","home",T("Visão geral","Overview"));
            AddNav(links,"editor","edit",T("Editor","Editor"));
            AddNav(links,"library","library",T("Biblioteca","Library"));
            AddNav(links,"profiles","profile",T("Perfis de jogo","Game profiles"));
            AddNav(links,"compare","compare",T("Comparar miras","Compare"));
            var settings = AddNav(links,"settings","settings",T("Configurações","Settings"));
            settings.Margin=new Thickness(0,12,0,0);
            sidebar.Children.Add(links);

            var stage = new DockPanel();
            var footer = new Border { Height=34, Padding=new Thickness(24,7,24,0), BorderThickness=new Thickness(0,1,0,0), Child=feedback };
            Ref(footer,Border.BorderBrushProperty,"Line"); Ref(feedback,TextBlock.ForegroundProperty,"Muted"); feedback.FontSize=12;
            DockPanel.SetDock(footer,Dock.Bottom); stage.Children.Add(footer);
            stage.Children.Add(scroll); Add(shell,stage,1,1);
        }
        private void ToggleHeader(object sender, RoutedEventArgs e) { Safe(Toggle); }
        private static System.Windows.Shapes.Path Logo()
        {
            return new System.Windows.Shapes.Path { Data=Geometry.Parse("M8,2 V5 M8,11 V14 M2,8 H5 M11,8 H14 M8,6.5 A1.5,1.5 0 1 0 8,9.5 A1.5,1.5 0 1 0 8,6.5"),
                Stroke=ColorBrush("#C7B2FF"), StrokeThickness=1.55, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
        }
        private Button WindowButton(string glyph,string name,Action action)
        {
            var b = Button(glyph,action); b.Width=36; b.MinHeight=32; b.Margin=new Thickness(1,0,0,0);
            b.Padding=new Thickness(0); b.FontSize=18; b.Background=Brushes.Transparent; b.BorderBrush=Brushes.Transparent;
            AutomationProperties.SetName(b,name); b.ToolTip=name; return b;
        }
        private void DragHeader(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (e.ClickCount == 2) Maximize();
            else if (WindowState == WindowState.Normal) DragMove();
        }
        private void Maximize()
        {
            if (WindowState == WindowState.Maximized) { WindowState=WindowState.Normal; return; }
            WindowState=WindowState.Maximized;
        }
        private Button AddNav(StackPanel panel,string key,string icon,string label)
        {
            var button=IconButton(icon,label,delegate { Navigate(key); });
            button.Style=(Style)FindResource("NavButton"); nav[key]=button; panel.Children.Add(button); return button;
        }
        private void Navigate(string key, bool transition = true, bool keepScroll = false)
        {
            if (key == "editor" && draft == null) LoadDraft(ActivePreset());
            page=key;
            homeState=null; homeGame=null;
            foreach (var pair in nav) pair.Value.Tag=pair.Key == key ? "Selected" : null;
            animatedPreviews.Clear();
            double offset=scroll.VerticalOffset;
            var body=Col(); body.Margin=new Thickness(28,24,27,26);
            switch (key)
            {
                case "home": Home(body); break;
                case "editor": Editor(body); break;
                case "library": Library(body); break;
                case "profiles": Profiles(body); break;
                case "compare": Compare(body); break;
                case "settings": Preferences(body); break;
            }
            scroll.Content=body;
            scroll.ScrollToVerticalOffset(keepScroll ? offset : 0);
            if (transition && !data.Performance)
                body.BeginAnimation(OpacityProperty,new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(105)));
            SetStatus(); ConfigureFrame();
        }
        private void PageHeading(StackPanel panel,string title,string detail)
        {
            panel.Children.Add(Label(title,31,"Text",true));
            var item=Label(detail,13,"Muted"); item.Margin=VerticalSpace(5,19); panel.Children.Add(item);
        }
        private Preview PreviewOf(Preset preset, double height, double zoom, bool grid = true, bool animated = false)
        {
            var preview = new Preview { Preset=preset, Height=height, Zoom=zoom, Grid=grid, TargetMarks=grid };
            preview.Refresh();
            if (animated) animatedPreviews.Add(preview);
            return preview;
        }
        private Profile ActiveProfile() { return data.Profiles[effective]; }
        private Preset ActivePreset() { return data.Presets.First(p=>p.Id==ActiveProfile().PresetId); }
        private Preset LivePreset()
        {
            var active=ActivePreset();
            return draft != null && draft.Id == active.Id ? draft : active;
        }
        private bool Excluded() { return data.Exclusions.Any(x=>Desktop.Matches(x,foreground)); }
        private bool ShouldShow()
        {
            var p=ActiveProfile();
            return data.Overlay && p.Enabled && !Excluded() && (!p.OnlyForeground || Desktop.MatchesProfile(p,foreground));
        }
        private void RefreshRuntime(bool changed)
        {
            if (testMode) return;
            if (changed) { lastRendered=null; drawingError=false; }
            bool watch=data.AutoProfile || data.Exclusions.Count>0 || data.Profiles.Any(p=>p.OnlyForeground);
            if (watch) { poll.Start(); Poll(changed); }
            else { poll.Stop(); foreground=""; SetEffective(data.ManualProfile); UpdateOverlay(changed); }
            SetStatus(); ConfigureFrame();
        }
        private void Poll(bool dirtyFrame = false)
        {
            string found=Desktop.ForegroundExecutable();
            if (found != foreground || dirtyFrame)
            {
                foreground=found; SetEffective(Desktop.MatchProfile(data,found));
                UpdateOverlay(dirtyFrame); SetStatus(); ConfigureFrame();
            }
        }
        private void SetEffective(int value)
        {
            if (effective==value) return;
            effective=value; lastRendered=null; drawingError=false; motionTime.Restart();
            if (page=="home") Navigate("home",false,true);
        }
        private void UpdateOverlay(bool force = false)
        {
            if (!ShouldShow()) { overlay.Hide(); motionRunning = false; return; }
            if (drawingError) return;
            var preset=LivePreset();
            bool animated=data.Animate && ActiveProfile().Animated && !data.Performance && preset.Motion.Kind!="None";
            if (lastRendered!=preset || !overlay.Visible || animated!=motionRunning)
            {
                motionTime.Restart(); motionRunning=animated;
                var baseDrawing=Reticle.Scene(preset,new Pose(1,1,0));
                reservation=animated ? (int)Math.Ceiling(Reticle.Extent(baseDrawing)*Reticle.AnimationExtent(preset.Motion)) : 0;
                force=true;
            }
            if (!force && (!animated || (!preset.Motion.Loop && motionTime.Elapsed.TotalSeconds > preset.Motion.Seconds+.08))) return;
            try
            {
                var pose=Reticle.Evaluate(preset.Motion,motionTime.Elapsed.TotalSeconds,animated);
                overlay.Draw(Reticle.Scene(preset,pose),ActiveProfile(),reservation);
                lastRendered=preset;
            }
            catch (Exception ex)
            {
                drawingError=true; overlay.Hide(); Log("overlay",ex);
                Notice(T("O Windows não conseguiu exibir a sobreposição.","Windows could not display the overlay."));
            }
        }
        private void ConfigureFrame()
        {
            if (testMode) return;
            bool ui=IsVisible && WindowState!=WindowState.Minimized && previewMotion && animatedPreviews.Any(v=>CanAdvance(v.Preset));
            bool os=ShouldShow() && motionRunning && !drawingError && (LivePreset().Motion.Loop || motionTime.Elapsed.TotalSeconds<=LivePreset().Motion.Seconds+.08);
            if (!data.Performance && (ui || os))
            {
                frame.Interval=TimeSpan.FromMilliseconds(1000.0/30); frame.Start();
            }
            else frame.Stop();
        }
        private bool CanAdvance(Preset preset)
        {
            return preset!=null && preset.Motion.Kind!="None" && (preset.Motion.Loop || motionTime.Elapsed.TotalSeconds<=preset.Motion.Seconds+.08);
        }
        private void TickFrame()
        {
            if (ShouldShow() && motionRunning) UpdateOverlay();
            if (IsVisible && WindowState!=WindowState.Minimized && previewMotion)
            {
                foreach (var preview in animatedPreviews)
                {
                    if (preview.Preset == null || preview.Preset.Motion.Kind == "None") continue;
                    bool moving=preview.Preset.Motion.Loop || motionTime.Elapsed.TotalSeconds<=preview.Preset.Motion.Seconds+.08;
                    if (!moving) continue;
                    preview.Pose=Reticle.Evaluate(preview.Preset.Motion,motionTime.Elapsed.TotalSeconds,true); preview.Refresh();
                }
            }
            if (!animatedPreviews.Any(p=>CanAdvance(p.Preset)) && !CanAdvance(lastRendered)) ConfigureFrame();
        }
        private void SetStatus()
        {
            string title=overlay.Visible ? T("Overlay ativo","Overlay on") : data.Overlay ? T("Overlay pausado","Overlay paused") : T("Overlay desligado","Overlay off");
            var state=Row();
            var dot=new System.Windows.Shapes.Ellipse { Width=7,Height=7,Margin=new Thickness(0,0,7,0),VerticalAlignment=VerticalAlignment.Center };
            Ref(dot,System.Windows.Shapes.Shape.FillProperty,overlay.Visible ? "On" : "Muted"); state.Children.Add(dot);
            state.Children.Add(Label(title,11,overlay.Visible ? "On" : "Muted",true));
            status.Content=state; AutomationProperties.SetName(status,title);
            if(homeState!=null)
            {
                homeState.Text=overlay.Visible ? T("Em exibição","Visible") : data.Overlay ? T("Pausado pelo perfil ou exceção","Paused by profile or exclusion") : T("Aguardando ativação","Waiting for activation");
                Ref(homeState,TextBlock.ForegroundProperty,overlay.Visible?"On":"Muted");
            }
            if(homeGame!=null)
            {
                if(data.AutoProfile && !Desktop.MatchesProfile(ActiveProfile(),foreground))
                    homeGame.Text=T("Sem correspondência · perfil manual em uso","No match · using manual profile");
                else if(foreground.Length>0)
                    homeGame.Text=T("Em primeiro plano: ","In foreground: ")+Path.GetFileName(foreground);
                else homeGame.Text=T("Seleção manual","Manual selection");
            }
            string name=ActiveProfile().Name;
            context.Text=T("Perfil  /  ","Profile  /  ")+name;
            if (trayToggle!=null) trayToggle.Text=data.Overlay ? T("Desligar mira","Turn overlay off") : T("Ligar mira","Turn overlay on");
        }
        private void Toggle() { data.Overlay=!data.Overlay; Changed(); if (page=="home") Navigate("home",false,true); }
        private void Changed()
        {
            pendingSave=true; saveDelay.Stop(); saveDelay.Start(); RefreshRuntime(true);
        }
        private void SaveNow()
        {
            if (!pendingSave) return;
            try { Store.Atomic(file,Store.Encode(data),true); pendingSave=false; }
            catch (Exception ex) { Log("save",ex); Notice(T("Falha ao gravar as configurações.","Could not save settings.")); }
        }
        private void Notice(string text) { feedback.Text=text; }
        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                if (ex is OutOfMemoryException) throw;
                Log("action",ex);
                if (ex is IOException && !(ex is InvalidDataException)) Notice(T("Falha ao acessar o arquivo selecionado.","Could not access the selected file."));
                else if (ex is InvalidDataException || ex is FormatException) Notice(ex.Message);
                else Notice(T("A ação falhou. Consulte o diagnóstico em Configurações.","The action failed. See diagnostics in Settings."));
            }
        }
        private void Log(string operation,Exception ex)
        {
            lastLog=operation + " / " + ex.GetType().Name;
            try
            {
                string path=Path.Combine(Path.GetDirectoryName(file),"diagnostics.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path) && new FileInfo(path).Length>65536) File.Delete(path);
                File.AppendAllText(path,DateTime.UtcNow.ToString("s",CultureInfo.InvariantCulture)+"Z "+lastLog+Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        private void DisplayChanged(object sender,EventArgs e) { Dispatcher.BeginInvoke(new Action(delegate { if (!exiting) RefreshRuntime(true); })); }
        private System.Drawing.Icon trayIcon;
        private static System.Drawing.Icon AppIcon()
        {
            using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location))
                return icon == null ? (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone() : (System.Drawing.Icon)icon.Clone();
        }
        private void CreateTray()
        {
            trayIcon = AppIcon();
            tray=new Forms.NotifyIcon { Text="Crosshair Studio", Icon=trayIcon, Visible=true };
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add(T("Abrir estúdio","Open studio"),null,delegate { ShowStudio(); });
            trayToggle=(Forms.ToolStripMenuItem)menu.Items.Add("Overlay",null,delegate { Safe(Toggle); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(T("Sair","Quit"),null,delegate { exiting=true; Close(); });
            tray.ContextMenuStrip=menu;
            tray.DoubleClick += delegate { ShowStudio(); };
        }
        private void ShowStudio() { Show(); if (WindowState==WindowState.Minimized) WindowState=WindowState.Normal; Activate(); }
        internal void StartInTray() { Hide(); ConfigureFrame(); }
        private void DisposeTray()
        {
            if (tray!=null) { tray.Visible=false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); tray=null; trayToggle=null; }
            if (trayIcon!=null) { trayIcon.Dispose(); trayIcon=null; }
        }
        private void OnClosing(object sender,CancelEventArgs e)
        {
            if (data.ToTray && !exiting && !testMode)
            {
                e.Cancel=true; Hide(); return;
            }
            if (dirty && !testMode && MessageBox.Show(this,T("Sair e descartar as alterações ainda não salvas?","Quit and discard unsaved changes?"),
                "Crosshair Studio",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)
            { exiting=false; e.Cancel=true; ShowStudio(); return; }
            exiting=true;
            SaveNow(); frame.Stop(); poll.Stop(); saveDelay.Stop();
            if (!testMode) SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            if (hotkeys!=null) hotkeys.Dispose();
            DisposeTray(); overlay.Dispose();
        }

        private void RunAction(string action)
        {
            Safe(delegate
            {
                switch (action)
                {
                    case "toggle": Toggle(); break;
                    case "editor": ShowStudio(); Navigate("editor"); break;
                    case "next": CyclePreset(1); break;
                    case "previous": CyclePreset(-1); break;
                    case "center": ActiveProfile().X=0; ActiveProfile().Y=0; Changed(); Notice(T("Mira centralizada.","Crosshair centered.")); if (page=="profiles") Navigate(page,false,true); break;
                    case "monitor":
                        var monitors=Forms.Screen.AllScreens;
                        int screen=Array.FindIndex(monitors,x=>x.DeviceName==Desktop.Monitor(ActiveProfile().Monitor).DeviceName);
                        ActiveProfile().Monitor=monitors[(screen+1)%monitors.Length].DeviceName;
                        Changed(); Notice(T("Monitor alterado.","Monitor changed.")); if (page=="profiles" || page=="home") Navigate(page,false,true); break;
                    case "profile": SelectProfile((effective+1)%data.Profiles.Count,true); break;
                    case "animate": data.Animate=!data.Animate; Changed(); Notice(data.Animate ? T("Animações ligadas.","Animations on.") : T("Animações pausadas.","Animations paused.")); break;
                }
            });
        }
        private void CyclePreset(int delta)
        {
            var pool=data.Presets.Where(p=>p.Favorite).ToList();
            if (pool.Count==0) pool=data.Presets;
            if (delta<0 && pool.All(p=>p.Id!=ActivePreset().Id)) delta=0;
            int idx=pool.FindIndex(p=>p.Id==ActivePreset().Id);
            if (delta==0) idx=pool.Count-1;
            else idx=(idx+delta+pool.Count)%pool.Count;
            ActivatePreset(pool[idx]); if (page=="home") Navigate("home",false,true);
        }
        private void ActivatePreset(Preset p)
        {
            ActiveProfile().PresetId=p.Id; Changed();
            Notice(T("Mira ativa  /  ","Active crosshair  /  ")+p.Name);
        }
        private void SelectProfile(int index,bool manual)
        {
            data.ManualProfile=index;
            if (manual) data.AutoProfile=false;
            SetEffective(index); Changed();
            if (page=="profiles" || page=="home" || page=="settings") Navigate(page,false,true);
            Notice(T("Perfil ativo  /  ","Active profile  /  ")+ActiveProfile().Name);
        }
        private void WindowKeys(object sender, KeyEventArgs e)
        {
            if (page!="editor" || Keyboard.Modifiers!=ModifierKeys.Control || e.OriginalSource is TextBox) return;
            if (e.Key==Key.Z) { Undo(false); e.Handled=true; }
            if (e.Key==Key.Y) { Undo(true); e.Handled=true; }
            if (e.Key==Key.S) { Safe(SaveDraft); e.Handled=true; }
        }
    }
}

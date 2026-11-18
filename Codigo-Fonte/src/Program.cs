using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

[assembly: AssemblyTitle("Crosshair Studio")]
[assembly: AssemblyDescription("Editor de miras e overlay para Windows")]
[assembly: AssemblyProduct("Crosshair Studio")]
[assembly: AssemblyVersion("0.3.2.0")]
[assembly: AssemblyFileVersion("0.3.2.0")]

namespace CrosshairStudio
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--self-test") return SelfTest.Run(args[1], args.Length > 2 && args[2] == "--desktop");
            if (args.Length == 2 && args[0] == "--previews") return SelfTest.Previews(args[1]);
            bool fresh;
            using (var mutex = new Mutex(true, @"Local\CrosshairStudio.Desktop", out fresh))
            {
                if (!fresh)
                {
                    try { using (var signal=EventWaitHandle.OpenExisting(@"Local\CrosshairStudio.Show")) signal.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return 0;
                }
                using (var signal = new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\CrosshairStudio.Show"))
                {
                    try
                    {
                        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                        string portable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"userdata");
                        string folder = Directory.Exists(portable) ? portable :
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Crosshair Studio");
                        var window = new StudioWindow(Path.Combine(folder,"settings.json"));
                        app.MainWindow = window;
                        var registered=ThreadPool.RegisterWaitForSingleObject(signal,delegate {
                            if(!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(delegate {
                                window.Show(); if(window.WindowState==WindowState.Minimized) window.WindowState=WindowState.Normal; window.Activate();
                            }));
                        },null,Timeout.Infinite,false);
                        try
                        {
                            if (args.Length>0 && args[0]=="--tray")
                            {
                                window.ShowActivated=false;
                                window.ShowInTaskbar=false;
                                window.Loaded += delegate {
                                    window.StartInTray(); window.ShowInTaskbar=true; window.ShowActivated=true;
                                };
                            }
                            return app.Run(window);
                        }
                        finally { registered.Unregister(null); }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Crosshair Studio não pôde iniciar.\n\n"+ex.GetType().Name,
                            "Crosshair Studio",MessageBoxButton.OK,MessageBoxImage.Error);
                        return 1;
                    }
                }
            }
        }
    }
}

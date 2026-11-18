using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace CrosshairStudio
{
    internal sealed class Overlay : Forms.NativeWindow, IDisposable
    {
        private const int ExTopmost = 8, ExTransparent = 32, ExToolWindow = 128, ExLayered = 0x80000, ExNoActivate = 0x8000000;
        private IntPtr dc, dib, oldBitmap, pixels;
        private int surfaceSize;
        private byte[] buffer;
        public bool Visible { get; private set; }
        public int Frames { get; private set; }
        public double LastMilliseconds { get; private set; }
        public Overlay()
        {
            CreateHandle(new Forms.CreateParams { Caption = "Crosshair Studio Overlay", Style = unchecked((int)0x80000000),
                ExStyle = ExTopmost | ExTransparent | ExToolWindow | ExLayered | ExNoActivate });
        }
        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg == 0x0084) { m.Result = new IntPtr(-1); return; }
            if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        private void CreateSurface(int size)
        {
            ReleaseSurface();
            var header = new BitmapHeader { HeaderSize = 40, Width = size, Height = -size, Planes = 1, Bits = 32 };
            dc = Native.CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) throw new Win32Exception();
            dib = Native.CreateDIBSection(dc, ref header, 0, out pixels, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) throw new Win32Exception();
            oldBitmap = Native.SelectObject(dc, dib);
            if (oldBitmap == IntPtr.Zero || oldBitmap == new IntPtr(-1)) throw new Win32Exception();
            surfaceSize = size;
            buffer = new byte[size * size * 4];
        }
        public void Draw(Drawing drawing, Profile profile, int reservation)
        {
            var watch = Stopwatch.StartNew();
            var screen = Desktop.Monitor(profile.Monitor);
            double scale = profile.Scale / 100;
            double extent = reservation > 0 ? reservation : Reticle.Extent(drawing);
            int size = Math.Max(32, Math.Min(3072, (int)Math.Ceiling((extent * scale * 2 + 8) / 32) * 32));
            if (surfaceSize != size) CreateSurface(size);
            Reticle.Bitmap(drawing, size, scale).CopyPixels(buffer, size * 4, 0);
            Marshal.Copy(buffer, 0, pixels, buffer.Length);
            var center = Desktop.Center(screen.Bounds, profile.X, profile.Y);
            var dst = new NativePoint { X = center.X - size / 2, Y = center.Y - size / 2 };
            var src = new NativePoint();
            var bounds = new NativePoint { X = size, Y = size };
            var blend = new Blend { Constant = 255, Format = 1 };
            if (!Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref bounds, dc, ref src, 0, ref blend, 2))
                throw new Win32Exception();
            if (!Visible)
            {
                Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0053);
                Visible = true;
            }
            Frames++;
            LastMilliseconds = watch.Elapsed.TotalMilliseconds;
        }
        public void Hide()
        {
            if (!Visible) return;
            Native.ShowWindow(Handle, 0);
            Visible = false;
        }
        private void ReleaseSurface()
        {
            if (oldBitmap != IntPtr.Zero && oldBitmap != new IntPtr(-1)) Native.SelectObject(dc, oldBitmap);
            if (dib != IntPtr.Zero) Native.DeleteObject(dib);
            if (dc != IntPtr.Zero) Native.DeleteDC(dc);
            oldBitmap = dib = dc = pixels = IntPtr.Zero;
            surfaceSize = 0; buffer = null;
        }
        public void Dispose() { Hide(); ReleaseSurface(); DestroyHandle(); }
    }

    internal static class Desktop
    {
        public static Forms.Screen Monitor(string id)
        {
            return Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == id) ?? Forms.Screen.PrimaryScreen;
        }
        public static System.Drawing.Point Center(System.Drawing.Rectangle r, int x, int y)
        {
            return new System.Drawing.Point(r.Left + r.Width / 2 + x, r.Top + r.Height / 2 + y);
        }
        public static string ForegroundExecutable()
        {
            uint processId;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out processId);
            if (processId == 0) return "";
            IntPtr handle = Native.OpenProcess(0x1000, false, processId);
            if (handle == IntPtr.Zero) return "";
            try
            {
                var path = new StringBuilder(2048);
                uint capacity = (uint)path.Capacity;
                return Native.QueryFullProcessImageName(handle, 0, path, ref capacity) ? path.ToString() : "";
            }
            finally { Native.CloseHandle(handle); }
        }
        public static bool Matches(string association, string executable)
        {
            if (string.IsNullOrEmpty(association) || string.IsNullOrEmpty(executable)) return false;
            string actual = association.IndexOfAny(new[] {'\\','/',':'}) < 0 ? Path.GetFileName(executable) : executable;
            return string.Equals(association, actual, StringComparison.OrdinalIgnoreCase);
        }
        public static int MatchProfile(Settings config, string executable)
        {
            if (config.AutoProfile)
            {
                int index = config.Profiles.FindIndex(p => MatchesProfile(p, executable));
                if (index >= 0) return index;
            }
            return config.ManualProfile;
        }
        public static bool MatchesProfile(Profile profile, string executable)
        {
            return !string.IsNullOrEmpty(executable) && profile.Executables.Any(association => Matches(association, executable));
        }
        public static string StartupFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Crosshair Studio.lnk");
        }
        public static void SetStartup(bool enabled)
        {
            string path = StartupFile();
            if (!enabled) { if (File.Exists(path)) File.Delete(path); return; }
            CreateShortcut(path, Process.GetCurrentProcess().MainModule.FileName, "--tray");
        }
        public static void CreateShortcut(string path, string target, string arguments)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Type type = Type.GetTypeFromProgID("WScript.Shell", true);
            dynamic shell = Activator.CreateInstance(type);
            dynamic link = null;
            try
            {
                link = shell.CreateShortcut(path); link.TargetPath = target; link.Arguments = arguments;
                link.WorkingDirectory = Path.GetDirectoryName(target); link.Save();
            }
            finally
            {
                if (link != null) Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    internal sealed class Hotkeys : IDisposable
    {
        private readonly HwndSource source;
        private readonly Dictionary<int, string> actions = new Dictionary<int, string>();
        private readonly Action<string> callback;
        private int counter = 120;
        public readonly Dictionary<string, string> Failures = new Dictionary<string, string>();
        public Hotkeys(IntPtr hwnd, Action<string> onAction)
        {
            source = HwndSource.FromHwnd(hwnd); callback = onAction; source.AddHook(Handle);
        }
        public void RegisterAll(Dictionary<string,string> configuration)
        {
            Clear();
            foreach (var pair in configuration)
            {
                if (pair.Value.Length == 0) continue;
                uint mods, key;
                if (!TryParse(pair.Value, out mods, out key)) { Failures[pair.Key] = "Combinação inválida."; continue; }
                int id = ++counter;
                if (!Native.RegisterHotKey(source.Handle, id, mods | 0x4000, key))
                    Failures[pair.Key] = Marshal.GetLastWin32Error()==5 ? "Windows recusou o acesso a atalhos globais (5)." : "Combinação já em uso ou reservada pelo Windows.";
                else actions[id] = pair.Key;
            }
        }
        public static bool TryParse(string value, out uint mods, out uint key)
        {
            mods = key = 0;
            string[] parts = (value ?? "").Split('+').Select(x => x.Trim().ToUpperInvariant()).ToArray();
            if (parts.Length < 2 || parts.Length > 4) return false;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                uint mod = parts[i] == "CTRL" ? 2u : parts[i] == "ALT" ? 1u : parts[i] == "SHIFT" ? 4u : 0u;
                if (mod == 0 || (mods & mod) != 0) return false;
                mods |= mod;
            }
            if ((mods & 3) == 0) return false;
            string last = parts[parts.Length - 1];
            int f;
            if (last.Length == 1 && ((last[0] >= 'A' && last[0] <= 'Z') || (last[0] >= '0' && last[0] <= '9'))) key = last[0];
            else if (last.StartsWith("F") && int.TryParse(last.Substring(1),out f) && f >= 1 && f <= 24 && f != 12) key = (uint)(111 + f);
            return key != 0;
        }
        private IntPtr Handle(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            string action;
            if (message == 0x0312 && actions.TryGetValue(wParam.ToInt32(), out action))
            {
                handled = true; callback(action);
            }
            return IntPtr.Zero;
        }
        private void Clear()
        {
            if (!source.IsDisposed) foreach (var id in actions.Keys) Native.UnregisterHotKey(source.Handle, id);
            actions.Clear(); Failures.Clear(); counter = 120;
        }
        public void Dispose() { Clear(); if (!source.IsDisposed) source.RemoveHook(Handle); }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    internal struct Blend { public byte Operation, Flags, Constant, Format; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapHeader
    {
        public uint HeaderSize;
        public int Width, Height;
        public ushort Planes, Bits;
        public uint Compression, ImageSize;
        public int X, Y;
        public uint Used, Important;
    }
    internal static class Native
    {
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref NativePoint dst, ref NativePoint size,
            IntPtr srcDc, ref NativePoint src, int key, ref Blend blend, uint flags);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(NativePoint point);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
        [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("gdi32.dll", SetLastError=true)] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError=true)] internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapHeader bitmap, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError=true)] internal static extern bool BitBlt(IntPtr dest,int x,int y,int width,int height,IntPtr source,int sx,int sy,uint flags);
        [DllImport("kernel32.dll")] internal static extern IntPtr OpenProcess(uint access, bool inherit, uint process);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] internal static extern bool QueryFullProcessImageName(IntPtr handle, uint flags, StringBuilder path, ref uint size);
    }
}

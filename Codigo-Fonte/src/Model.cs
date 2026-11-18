using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CrosshairStudio
{
    public sealed class Layer
    {
        public string Name { get; set; }
        public string Shape { get; set; }
        public string Color { get; set; }
        public string Gradient { get; set; }
        public string OutlineColor { get; set; }
        public double Size { get; set; }
        public double Thickness { get; set; }
        public double Gap { get; set; }
        public double Rotation { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Opacity { get; set; }
        public double Outline { get; set; }
        public double InnerBorder { get; set; }
        public double Glow { get; set; }
        public double Shadow { get; set; }
        public double Blur { get; set; }
        public bool Visible { get; set; }
        public bool Locked { get; set; }
        public bool Mirror { get; set; }
        public string Group { get; set; }
        public string Image { get; set; }

        public Layer()
        {
            Name = "Cruz"; Shape = "Cross"; Color = "#A6FFD8"; Gradient = ""; OutlineColor = "#111525";
            Size = 11; Thickness = 2; Gap = 5; Opacity = 100; Outline = 1; Visible = true;
        }
        public Layer Copy() { return (Layer)MemberwiseClone(); }
    }

    public sealed class Keyframe
    {
        public double Time { get; set; }
        public double Scale { get; set; }
        public double Alpha { get; set; }
        public double Angle { get; set; }
        public Keyframe() { Scale = 100; Alpha = 100; }
    }

    public sealed class Motion
    {
        public string Kind { get; set; }
        public double Seconds { get; set; }
        public bool Loop { get; set; }
        public List<Keyframe> Frames { get; set; }
        public Motion() { Kind = "None"; Seconds = 2; Loop = true; Frames = new List<Keyframe>(); }
    }

    public sealed class Preset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public bool Favorite { get; set; }
        public List<Layer> Layers { get; set; }
        public Motion Motion { get; set; }
        public Preset()
        {
            Id = Guid.NewGuid().ToString("N"); Name = "Nova mira"; Category = "Personalizada";
            Layers = new List<Layer>(); Motion = new Motion();
        }
    }

    public sealed class Profile
    {
        public string Name { get; set; }
        public List<string> Executables { get; set; }
        public string Executable { set { LegacyExecutable = value; } }
        internal string LegacyExecutable { get; set; }
        public string PresetId { get; set; }
        public string Monitor { get; set; }
        public bool Enabled { get; set; }
        public bool Animated { get; set; }
        public bool OnlyForeground { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public double Scale { get; set; }
        internal const int ExecutableMaxLength = 1024;
        public Profile() { Name = "Padrão"; Enabled = true; Animated = true; Scale = 100; Executables = new List<string>(); }
        internal static string NormalizeExecutable(string value) { return (value ?? "").Trim().Replace('/', '\\'); }
    }

    public sealed class Revision
    {
        public string When { get; set; }
        public Preset Preset { get; set; }
    }

    public sealed class Settings
    {
        public int Format { get; set; }
        public List<Preset> Presets { get; set; }
        public List<Profile> Profiles { get; set; }
        public List<Revision> History { get; set; }
        public Dictionary<string, string> Hotkeys { get; set; }
        public List<string> Exclusions { get; set; }
        public int ManualProfile { get; set; }
        public bool Overlay { get; set; }
        public bool AutoProfile { get; set; }
        public bool ToTray { get; set; }
        public bool Animate { get; set; }
        public bool Performance { get; set; }
        public bool Light { get; set; }
        public bool English { get; set; }
        public double InterfaceScale { get; set; }

        public Settings()
        {
            Format = 1; Presets = new List<Preset>(); Profiles = new List<Profile>(); History = new List<Revision>();
            Hotkeys = DefaultHotkeys(); Exclusions = new List<string>(); ToTray = true; Animate = true; InterfaceScale = 100;
        }
        public static Dictionary<string, string> DefaultHotkeys()
        {
            return new Dictionary<string, string> {
                {"toggle","Ctrl+Alt+F8"}, {"next","Ctrl+Alt+F9"}, {"previous","Ctrl+Alt+F7"},
                {"editor","Ctrl+Alt+F10"}, {"center","Ctrl+Alt+F6"}, {"monitor","Ctrl+Alt+F5"},
                {"profile","Ctrl+Alt+F4"}, {"animate","Ctrl+Alt+F11"}
            };
        }
        public static Settings Fresh()
        {
            var config = new Settings();
            config.Presets = Catalog.Create();
            config.Profiles.Add(new Profile { PresetId = config.Presets[0].Id });
            return config;
        }
    }

    internal static class Catalog
    {
        public static readonly string[] Shapes = { "Cross", "Dot", "Ring", "Line", "T", "X", "Square", "Diamond", "Chevron", "Corners", "PNG", "SVG" };
        public static readonly string[] Categories = { "FPS", "Minimal", "Tactical", "Classic", "Dot", "Circle", "Creative", "Personalizada" };
        public static List<Preset> Create()
        {
            return new List<Preset> {
                Make("vector","Vector Mint","FPS",true,"Cross","#A6FFD8",13,2,6,true),
                Make("signal","Signal White","Minimal",true,"Cross","#F4F5FC",7,1,5,false),
                Make("pin","Pinpoint","Dot",true,"Dot","#FF8FA6",3,2,0,false),
                Make("orbit","Orbit Cyan","Circle",true,"Ring","#54E5FD",9,2,0,true),
                Make("sentry","Sentry T","Tactical",false,"T","#F4D18B",11,2,5,true),
                Make("classic","Classic Lime","Classic",false,"Cross","#C6FF86",16,2,7,false),
                Make("spectra","Spectra X","Creative",false,"X","#BC9BFF",12,2,4,true),
                Make("prism","Prism","Tactical",false,"Diamond","#EDF0FF",11,2,1,true),
                Make("bracket","Brackets","FPS",false,"Corners","#FCA5A5",11,2,5,true),
                Make("ember","Ember Dot","Dot",false,"Dot","#FFBB70",5,2,0,false),
                Make("aperture","Aperture","Circle",false,"Ring","#C4B5FD",15,1.5,0,false),
                Make("arrow","Arrowhead","Tactical",false,"Chevron","#96F9E3",9,2,0,false),
                Make("frame","Frame 01","Classic",false,"Square","#8DBCFF",10,1.5,0,true),
                Make("horizon","Horizon","Minimal",false,"Line","#F4F5FC",15,1,0,false),
                Make("nova","Nova Coral","Creative",false,"X","#FFB9AC",20,1,8,false),
                Make("alpine","Alpine","FPS",false,"Cross","#B9DEFF",10,3,4,true)
            };
        }
        private static Preset Make(string id, string name, string category, bool favorite, string shape, string color, double size, double weight, double gap, bool dot)
        {
            var preset = new Preset { Id = id, Name = name, Category = category, Favorite = favorite };
            preset.Layers.Add(new Layer { Name = shape, Shape = shape, Color = color, Size = size, Thickness = weight, Gap = gap });
            if (dot) preset.Layers.Add(new Layer { Name = "Ponto central", Shape = "Dot", Color = color, Size = 2, Outline = 0.75 });
            return preset;
        }
    }

    internal static class Store
    {
        public const int MaxBytes = 16 * 1024 * 1024;
        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 24 };
        }
        public static string Encode<T>(T value) { return Serializer().Serialize(value); }
        public static T Decode<T>(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaxBytes) throw new InvalidDataException("JSON inválido ou grande demais.");
            // Reject type metadata before the serializer sees any user-controlled objects.
            string normalized = Regex.Replace(text, @"\\u([0-9a-fA-F]{4})",
                match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
            if (Regex.IsMatch(normalized, @"""__type""\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new InvalidDataException("JSON contém uma propriedade reservada.");
            var result = Serializer().Deserialize<T>(text);
            if (object.ReferenceEquals(result, null)) throw new InvalidDataException("JSON vazio.");
            return result;
        }
        public static T Clone<T>(T value) { return Decode<T>(Encode(value)); }
        public static string Limit(string value, int max, string fallback)
        {
            value = (value ?? "").Trim();
            return value.Length == 0 ? fallback : value.Substring(0, Math.Min(max, value.Length));
        }
        public static double Clamp(double value, double low, double high, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(low, Math.Min(high, value));
        }
        public static Preset Validate(Preset p)
        {
            p.Id = Limit(p.Id, 64, Guid.NewGuid().ToString("N")); p.Name = Limit(p.Name, 60, "Nova mira");
            p.Category = Limit(p.Category, 24, "Personalizada");
            if (p.Layers == null || p.Layers.Count > 32) throw new InvalidDataException("O limite é de 32 camadas por mira.");
            p.Layers.RemoveAll(l => l == null);
            foreach (var l in p.Layers)
            {
                if (!Catalog.Shapes.Contains(l.Shape)) throw new InvalidDataException("Forma desconhecida.");
                l.Name = Limit(l.Name, 45, l.Shape); l.Group = Limit(l.Group, 35, "");
                l.Color = Reticle.NormalizeColor(l.Color); l.OutlineColor = Reticle.NormalizeColor(l.OutlineColor);
                if (!string.IsNullOrWhiteSpace(l.Gradient)) l.Gradient = Reticle.NormalizeColor(l.Gradient);
                l.Size = Clamp(l.Size, 1, 96, 10); l.Thickness = Clamp(l.Thickness, 0.5, 28, 2);
                l.Gap = Clamp(l.Gap, 0, 48, 5); l.X = Clamp(l.X, -128, 128, 0); l.Y = Clamp(l.Y, -128, 128, 0);
                l.Rotation = Clamp(l.Rotation, -360, 360, 0); l.Opacity = Clamp(l.Opacity, 0, 100, 100);
                l.Outline = Clamp(l.Outline, 0, 12, 0); l.InnerBorder = Clamp(l.InnerBorder, 0, 8, 0); l.Glow = Clamp(l.Glow, 0, 12, 0); l.Shadow = Clamp(l.Shadow, 0, 12, 0); l.Blur = Clamp(l.Blur, 0, 8, 0);
                if (l.Shape == "PNG" || l.Shape == "SVG") Assets.Get(l);
                else l.Image = null;
            }
            if (p.Motion == null) p.Motion = new Motion();
            var m = p.Motion;
            if (!new[] {"None","Pulse","Fade","Spin","Grow","Keyframes"}.Contains(m.Kind)) m.Kind = "None";
            m.Seconds = Clamp(m.Seconds, 0.25, 30, 2);
            if (m.Frames == null) m.Frames = new List<Keyframe>();
            if (m.Frames.Count > 24) throw new InvalidDataException("O limite é de 24 keyframes.");
            m.Frames.RemoveAll(f => f == null);
            foreach (var f in m.Frames)
            {
                f.Time = Clamp(f.Time, 0, 100, 0); f.Scale = Clamp(f.Scale, 25, 200, 100);
                f.Alpha = Clamp(f.Alpha, 0, 100, 100); f.Angle = Clamp(f.Angle, -360, 360, 0);
            }
            m.Frames = m.Frames.OrderBy(f => f.Time).ToList();
            return p;
        }

        public static Settings Validate(Settings s)
        {
            if (s.Format != 1 || s.Presets == null || s.Profiles == null || s.Presets.Count == 0 ||
                s.Profiles.Count == 0 || s.Presets.Count > 160 || s.Profiles.Count > 80)
                throw new InvalidDataException("Formato de backup incompatível.");
            foreach (var p in s.Presets) { if (p == null) throw new InvalidDataException("Mira inválida."); Validate(p); }
            if (s.Presets.Select(p => p.Id).Distinct().Count() != s.Presets.Count) throw new InvalidDataException("IDs de mira repetidos.");
            foreach (var profile in s.Profiles)
            {
                if (profile == null) throw new InvalidDataException("Perfil inválido.");
                profile.Name = Limit(profile.Name, 60, "Padrão");
                // Older settings and backups stored a single path in Executable.
                var executables = profile.Executables ?? new List<string>();
                profile.Executables = executables.Concat(new[] {profile.LegacyExecutable})
                    .Select(Profile.NormalizeExecutable)
                    .Where(value=>value.Length>0 && value.Length<=Profile.ExecutableMaxLength)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                profile.LegacyExecutable = null;
                profile.Monitor = Limit(profile.Monitor, 128, ""); profile.Scale = Clamp(profile.Scale, 25, 200, 100);
                profile.X = (int)Clamp(profile.X, -4096, 4096, 0); profile.Y = (int)Clamp(profile.Y, -4096, 4096, 0);
                if (!s.Presets.Any(p => p.Id == profile.PresetId)) profile.PresetId = s.Presets[0].Id;
            }
            s.ManualProfile = Math.Max(0, Math.Min(s.Profiles.Count - 1, s.ManualProfile));
            s.InterfaceScale = Clamp(s.InterfaceScale, 80, 120, 100);
            s.Exclusions = (s.Exclusions ?? new List<string>()).Where(e => !string.IsNullOrWhiteSpace(e)).Take(80).Select(e => Limit(e, 1024, "")).ToList();
            var keys = Settings.DefaultHotkeys();
            if (s.Hotkeys != null) foreach (var key in keys.Keys.ToList())
                if (s.Hotkeys.ContainsKey(key)) keys[key] = Limit(s.Hotkeys[key], 45, "");
            s.Hotkeys = keys;
            if (s.History == null) s.History = new List<Revision>();
            s.History = s.History.Where(r => r != null && r.Preset != null).Take(20).ToList();
            foreach (var r in s.History) Validate(r.Preset);
            return s;
        }

        public static string Read(string path, int limit)
        {
            using (var input = File.OpenRead(path))
            {
                if (input.Length > limit) throw new InvalidDataException("Arquivo maior que o limite.");
                using (var reader = new StreamReader(input, new UTF8Encoding(false, true), true))
                    return reader.ReadToEnd();
            }
        }
        public static void Atomic(string path, string text, bool backup)
        {
            if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new InvalidDataException("Limite de armazenamento atingido.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, backup ? path + ".bak" : null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static Settings Load(string path, out string warning)
        {
            warning = "";
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return Settings.Fresh();
            try { return Validate(Decode<Settings>(Read(path, MaxBytes))); }
            catch (Exception ex)
            {
                if (ex is OutOfMemoryException) throw;
                try
                {
                    var recovered = Validate(Decode<Settings>(Read(path + ".bak", MaxBytes)));
                    if (File.Exists(path)) File.Move(path, path + ".invalid-" + Guid.NewGuid().ToString("N"));
                    warning = "Configurações recuperadas da cópia anterior.";
                    return recovered;
                }
                catch (Exception backupError)
                {
                    if (backupError is OutOfMemoryException) throw;
                    warning = "Configurações inválidas. Preferências padrão carregadas.";
                    return Settings.Fresh();
                }
            }
        }
        public static string Share(Preset preset)
        {
            return ShareCode.Encode(preset);
        }
        public static Preset Unshare(string code)
        {
            return ShareCode.Decode(code);
        }
    }
}

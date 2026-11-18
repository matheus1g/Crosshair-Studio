using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;

namespace CrosshairStudio
{
    internal static class ShareCode
    {
        private const string Prefix = "CSX-";
        private const string LegacyPrefix = "CS1-";
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const int ChecksumBytes = 4;
        public const int MaxCodeLength = 4 * 1024 * 1024;
        private const int MaxPayload = (MaxCodeLength - 4) / 48 * 25 - ChecksumBytes;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        // Wire indices and defaults are part of version 1 of the CSX format.
        private static readonly string[] Shapes = { "Cross","Dot","Ring","Line","T","X","Square","Diamond","Chevron","Corners","PNG","SVG" };
        private static readonly string[] Categories = { "FPS","Minimal","Tactical","Classic","Dot","Circle","Creative","Personalizada" };
        private static readonly string[] Motions = { "None","Pulse","Fade","Spin","Grow","Keyframes" };
        private const string DefaultColor = "#A6FFD8", DefaultOutline = "#111525";
        private static readonly NumberField[] Numbers = {
            new NumberField(11, l=>l.Size, (l,n)=>l.Size=n),
            new NumberField(2, l=>l.Thickness, (l,n)=>l.Thickness=n),
            new NumberField(5, l=>l.Gap, (l,n)=>l.Gap=n),
            new NumberField(0, l=>l.Rotation, (l,n)=>l.Rotation=n),
            new NumberField(0, l=>l.X, (l,n)=>l.X=n),
            new NumberField(0, l=>l.Y, (l,n)=>l.Y=n),
            new NumberField(100, l=>l.Opacity, (l,n)=>l.Opacity=n),
            new NumberField(1, l=>l.Outline, (l,n)=>l.Outline=n),
            new NumberField(0, l=>l.InnerBorder, (l,n)=>l.InnerBorder=n),
            new NumberField(0, l=>l.Glow, (l,n)=>l.Glow=n),
            new NumberField(0, l=>l.Shadow, (l,n)=>l.Shadow=n),
            new NumberField(0, l=>l.Blur, (l,n)=>l.Blur=n)
        };
        private const int NameBit=1<<12, ColorBit=1<<13, GradientBit=1<<14, OutlineBit=1<<15,
            GroupBit=1<<16, HiddenBit=1<<17, LockedBit=1<<18, MirrorBit=1<<19;

        private sealed class NumberField
        {
            public readonly double Default;
            public readonly Func<Layer,double> Get;
            public readonly Action<Layer,double> Set;
            public NumberField(double value, Func<Layer,double> get, Action<Layer,double> set)
            { Default=value; Get=get; Set=set; }
        }

        public static bool HasPrefix(string value)
        {
            return value.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(LegacyPrefix,StringComparison.OrdinalIgnoreCase);
        }

        public static string Encode(Preset preset)
        {
            var p=Store.Validate(Store.Clone(preset));
            using(var stream=new MemoryStream())
            using(var output=new BinaryWriter(stream,Utf8,true))
            {
                output.Write((byte)1);
                WriteText(output,p.Name);
                int category=Array.IndexOf(Categories,p.Category);
                output.Write((byte)(category<0?255:category));
                if(category<0) WriteText(output,p.Category);
                var motion=p.Motion;
                bool hasMotion=motion.Kind!="None" || motion.Seconds!=2 || !motion.Loop || motion.Frames.Count>0;
                output.Write((byte)(p.Layers.Count|(p.Favorite?64:0)|(hasMotion?128:0)));
                foreach(var layer in p.Layers) WriteLayer(output,layer);
                if(hasMotion) WriteMotion(output,motion);
                output.Flush();
                if(stream.Length>MaxPayload) throw new InvalidDataException("A mira é grande demais para um código. Use Exportar arquivo e escolha JSON.");
                byte[] payload=stream.ToArray();
                using(var sha=SHA256.Create()) output.Write(sha.ComputeHash(payload),0,ChecksumBytes);
                output.Flush();
                return ToText(stream.ToArray());
            }
        }

        public static Preset Decode(string code)
        {
            code=(code??"").Trim();
            if(code.Length>MaxCodeLength || !HasPrefix(code)) throw InvalidCode();
            try
            {
                if(code.StartsWith(LegacyPrefix,StringComparison.OrdinalIgnoreCase))
                {
                    string body=code.Substring(LegacyPrefix.Length).Replace('-','+').Replace('_','/');
                    body=body.PadRight(((body.Length+3)/4)*4,'=');
                    return Store.Validate(Store.Decode<Preset>(Utf8.GetString(Convert.FromBase64String(body))));
                }
                return DecodeCompact(code);
            }
            catch(FormatException) { throw InvalidCode(); }
            catch(ArgumentException) { throw InvalidCode(); }
            catch(EndOfStreamException) { throw InvalidCode(); }
            catch(InvalidOperationException) { throw InvalidCode(); }
            catch(OverflowException) { throw InvalidCode(); }
            catch(System.Xml.XmlException) { throw InvalidCode(); }
            catch(NotSupportedException) { throw InvalidCode(); }
        }
        private static InvalidDataException InvalidCode()
        {
            return new InvalidDataException("Código de mira inválido ou incompleto. Use um código CSX- ou CS1- do Crosshair Studio.");
        }
        private static Preset DecodeCompact(string code)
        {
            byte[] bytes=FromText(code);
            int length=bytes.Length-ChecksumBytes;
            if(length<4 || length>MaxPayload) throw InvalidCode();
            using(var sha=SHA256.Create())
            {
                var hash=sha.ComputeHash(bytes,0,length);
                for(int i=0;i<ChecksumBytes;i++) if(bytes[length+i]!=hash[i]) throw InvalidCode();
            }
            using(var stream=new MemoryStream(bytes,0,length,false))
            using(var input=new BinaryReader(stream,Utf8))
            {
                if(input.ReadByte()!=1) throw new InvalidDataException("Este código CSX requer uma versão mais recente do Crosshair Studio.");
                var p=new Preset { Name=ReadText(input,60) };
                int category=input.ReadByte();
                p.Category=category==255?ReadText(input,24):GetChoice(Categories,category);
                int header=input.ReadByte();
                int count=header&63;
                if(count>32) throw InvalidCode();
                p.Favorite=(header&64)!=0;
                for(int i=0;i<count;i++) p.Layers.Add(ReadLayer(input));
                if((header&128)!=0) p.Motion=ReadMotion(input);
                if(stream.Position!=length) throw InvalidCode();
                return Store.Validate(p);
            }
        }

        private static void WriteLayer(BinaryWriter output, Layer layer)
        {
            int bits=0;
            for(int i=0;i<Numbers.Length;i++) if(Numbers[i].Get(layer)!=Numbers[i].Default) bits|=1<<i;
            if(layer.Name!=layer.Shape) bits|=NameBit;
            if(layer.Color!=DefaultColor) bits|=ColorBit;
            if(!string.IsNullOrEmpty(layer.Gradient)) bits|=GradientBit;
            if(layer.OutlineColor!=DefaultOutline) bits|=OutlineBit;
            if(!string.IsNullOrEmpty(layer.Group)) bits|=GroupBit;
            if(!layer.Visible) bits|=HiddenBit;
            if(layer.Locked) bits|=LockedBit;
            if(layer.Mirror) bits|=MirrorBit;
            output.Write((byte)Array.IndexOf(Shapes,layer.Shape));
            WriteUnsigned(output,bits);
            for(int i=0;i<Numbers.Length;i++) if((bits&(1<<i))!=0) WriteNumber(output,Numbers[i].Get(layer));
            if((bits&NameBit)!=0) WriteText(output,layer.Name);
            if((bits&ColorBit)!=0) WriteColor(output,layer.Color);
            if((bits&GradientBit)!=0) WriteColor(output,layer.Gradient);
            if((bits&OutlineBit)!=0) WriteColor(output,layer.OutlineColor);
            if((bits&GroupBit)!=0) WriteText(output,layer.Group);
            if(layer.Image!=null)
            {
                byte[] image=Convert.FromBase64String(layer.Image);
                if(output.BaseStream.Position+image.Length>MaxPayload)
                    throw new InvalidDataException("As imagens deixam a mira grande demais para um código. Use Exportar arquivo e escolha JSON.");
                WriteUnsigned(output,image.Length);
                output.Write(image);
            }
        }
        private static Layer ReadLayer(BinaryReader input)
        {
            string shape=GetChoice(Shapes,input.ReadByte());
            int bits=ReadUnsigned(input);
            if((bits>>20)!=0) throw InvalidCode();
            var layer=new Layer { Shape=shape, Name=shape, Color=DefaultColor, OutlineColor=DefaultOutline, Gradient="", Group="",
                Visible=(bits&HiddenBit)==0, Locked=(bits&LockedBit)!=0, Mirror=(bits&MirrorBit)!=0 };
            for(int i=0;i<Numbers.Length;i++)
                Numbers[i].Set(layer,(bits&(1<<i))!=0?ReadNumber(input):Numbers[i].Default);
            if((bits&NameBit)!=0) layer.Name=ReadText(input,45);
            if((bits&ColorBit)!=0) layer.Color=ReadColor(input);
            if((bits&GradientBit)!=0) layer.Gradient=ReadColor(input);
            if((bits&OutlineBit)!=0) layer.OutlineColor=ReadColor(input);
            if((bits&GroupBit)!=0) layer.Group=ReadText(input,35);
            if(shape=="PNG" || shape=="SVG")
                layer.Image=Convert.ToBase64String(ReadBytes(input,Assets.MaxImage));
            return layer;
        }
        private static void WriteMotion(BinaryWriter output, Motion motion)
        {
            output.Write((byte)(Array.IndexOf(Motions,motion.Kind)|(motion.Loop?0:128)));
            WriteNumber(output,motion.Seconds);
            output.Write((byte)motion.Frames.Count);
            foreach(var frame in motion.Frames)
            {
                WriteNumber(output,frame.Time); WriteNumber(output,frame.Scale);
                WriteNumber(output,frame.Alpha); WriteNumber(output,frame.Angle);
            }
        }
        private static Motion ReadMotion(BinaryReader input)
        {
            int bits=input.ReadByte();
            var motion=new Motion { Kind=GetChoice(Motions,bits&127), Loop=(bits&128)==0, Seconds=ReadNumber(input) };
            int count=input.ReadByte();
            if(count>24) throw InvalidCode();
            for(int i=0;i<count;i++)
                motion.Frames.Add(new Keyframe { Time=ReadNumber(input), Scale=ReadNumber(input), Alpha=ReadNumber(input), Angle=ReadNumber(input) });
            return motion;
        }

        private static string GetChoice(string[] choices,int index)
        {
            if(index>=choices.Length) throw InvalidCode();
            return choices[index];
        }
        private static void WriteText(BinaryWriter output,string text)
        {
            byte[] bytes=Utf8.GetBytes(text);
            WriteUnsigned(output,bytes.Length);
            output.Write(bytes);
        }
        private static string ReadText(BinaryReader input,int maxLength)
        {
            string text=Utf8.GetString(ReadBytes(input,maxLength*4));
            if(text.Length>maxLength) throw InvalidCode();
            return text;
        }
        private static byte[] ReadBytes(BinaryReader input,int max)
        {
            int count=ReadUnsigned(input);
            if(count>max || count>input.BaseStream.Length-input.BaseStream.Position) throw InvalidCode();
            var bytes=input.ReadBytes(count);
            if(bytes.Length!=count) throw InvalidCode();
            return bytes;
        }
        private static void WriteColor(BinaryWriter output,string text)
        {
            Color c=Reticle.ParseColor(text);
            output.Write(c.R); output.Write(c.G); output.Write(c.B); output.Write(c.A);
        }
        private static string ReadColor(BinaryReader input)
        {
            byte r=input.ReadByte(), g=input.ReadByte(), b=input.ReadByte(), a=input.ReadByte();
            return string.Format("#{0:X2}{1:X2}{2:X2}",r,g,b)+(a==255?"":a.ToString("X2"));
        }
        private static void WriteNumber(BinaryWriter output,double value)
        {
            int cents=(int)Math.Round(value*100);
            if(cents/100.0==value) WriteUnsigned(output,(cents>=0?2*cents:-2*cents-1)+1);
            else { output.Write((byte)0); output.Write(value); }
        }
        private static double ReadNumber(BinaryReader input)
        {
            int value=ReadUnsigned(input);
            if(value==0) return input.ReadDouble();
            int encoded=value-1;
            return ((encoded&1)==0?encoded/2:-(encoded/2)-1)/100.0;
        }
        private static void WriteUnsigned(BinaryWriter output,int value)
        {
            while(value>=128) { output.Write((byte)((value&127)|128)); value>>=7; }
            output.Write((byte)value);
        }
        private static int ReadUnsigned(BinaryReader input)
        {
            int result=0;
            for(int shift=0;shift<=21;shift+=7)
            {
                int part=input.ReadByte();
                result|=(part&127)<<shift;
                if((part&128)==0) return result;
            }
            throw InvalidCode();
        }
        private static string ToText(byte[] data)
        {
            var text=new StringBuilder(Prefix);
            int buffer=0,bits=0,position=0;
            foreach(byte value in data)
            {
                buffer=(buffer<<8)|value; bits+=8;
                while(bits>=5)
                {
                    bits-=5;
                    AppendCharacter(text,ref position,Alphabet[(buffer>>bits)&31]);
                }
            }
            if(bits>0) AppendCharacter(text,ref position,Alphabet[(buffer<<(5-bits))&31]);
            return text.ToString();
        }
        private static void AppendCharacter(StringBuilder text,ref int position,char value)
        {
            if(position>0 && position%5==0) text.Append('-');
            text.Append(value); position++;
        }
        private static byte[] FromText(string code)
        {
            using(var stream=new MemoryStream(Math.Min((code.Length-Prefix.Length)*5/8,MaxPayload+ChecksumBytes)))
            {
                int buffer=0,bits=0;
                for(int i=Prefix.Length;i<code.Length;i++)
                {
                    char c=code[i];
                    if(c=='-' || char.IsWhiteSpace(c)) continue;
                    int value=Alphabet.IndexOf(char.ToUpperInvariant(c));
                    if(value<0) throw InvalidCode();
                    buffer=(buffer<<5)|value; bits+=5;
                    if(bits<8) continue;
                    bits-=8;
                    if(stream.Length>=MaxPayload+ChecksumBytes) throw InvalidCode();
                    stream.WriteByte((byte)(buffer>>bits));
                }
                if(bits>=5 || (buffer&((1<<bits)-1))!=0) throw InvalidCode();
                return stream.ToArray();
            }
        }
    }
}

using AvaloniaToolbox.Core.IO;
using ImageLibrary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PartyPatcher.Formats
{
    public class AtbFile
    {
        public List<AtbTextureInfo> Textures = new List<AtbTextureInfo>();
        public List<BankData> Banks = new List<BankData>();
        public List<PatternData> Patterns = new List<PatternData>();

        public ushort NumReferences { get; set; }

        public int FileIndex = 0;

        public AtbFile() { }

        public AtbFile(Stream stream)
        {
            using (var reader = new FileReader(stream)) {
                Read(reader);
            }
        }

        public AtbFile(string fileName) {
            using (var reader = new FileReader(fileName)) {
                Read(reader);
            }
        }

        public byte[] ToBytes()
        {
            var mem = new MemoryStream();
            Save(mem);
            return mem.ToArray();
        }

        public string ToJson()
        {
          return JsonSerializer.Serialize(this, new JsonSerializerOptions()
            {
                WriteIndented = true,
                IncludeFields = true,
            });
        }

        public void FromJson(string json, out List<TexFormat> formats)
        {
            formats = new();

            var atb = JsonSerializer.Deserialize<AtbFile>(json, new JsonSerializerOptions()
            {
                WriteIndented = true,
                IncludeFields = true,
            });

            this.Banks = atb.Banks;
            this.Patterns = atb.Patterns;
            this.NumReferences = atb.NumReferences;
            for (int i = 0; i < atb.Textures.Count; i++)
                formats.Add(atb.Textures[i].Format);
        }

        public void Save(string fileName)
        {
            using (var writer = new FileWriter(fileName)) {
                Write(writer);
            }
        }

        public void Save(Stream stream)
        {
            using (var writer = new FileWriter(stream)) {
                Write(writer);
            }
        }

        public void FromFolder(string folder)
        {
            foreach (var file in Directory.GetFiles(folder))
                FromFile(file);
        }

        public void AddTexture(string file, TexFormat format = TexFormat.C8)
        {
            var img = Image.Load<Rgba32>(file);
            AtbTextureInfo tex = AddTexture(img, format);
        }

        public void FromFile(string file, TexFormat format = TexFormat.C8)
        {
            var img = Image.Load<Rgba32>(file);
            AtbTextureInfo tex = AddTexture(img, format);

            Patterns.Add(new PatternData()
            {
                CenterX = (short)(tex.Width / 2),
                CenterY = (short)(tex.Height / 2),
                Width = (short)tex.Width,
                Height = (short)tex.Height,
                Layers = new List<LayerData>()
                    {
                        new LayerData()
                        {
                            Alpha = 255,
                            Flip = 0,
                            TextureIndex = (short)(this.Textures.Count - 1),
                            TexCoordWidth = (short)tex.Width,
                            TexCoordHeight = (short)tex.Height,
                            TexCoordTopLeftX = 0,
                            TexCoordTopLeftY = 0,
                            ShiftX = 0,
                            ShiftY = 0,
                            VertexTopLeftX = 0,
                            VertexTopLeftY = 0,
                            VertexTopRightX = (short)tex.Width,
                            VertexTopRightY = 0,
                            VertexBottomRightX = (short)tex.Width,
                            VertexBottomLeftY = (short)tex.Height,
                            VertexBottomLeftX = 0,
                            VertexBottomRightY = (short)tex.Height,
                        },
                    },
            });
            Banks.Add(new BankData()
            {
                AnimFrames = new List<AnimFrame>()
                    {
                        new AnimFrame()
                        {
                            PatternIndex = (short)(this.Textures.Count - 1),
                            FrameLength = 16,
                            ShiftX = 0,
                            ShiftY = 0,
                            Flip = 0,
                        },
                    },
            });
        }

        public AtbTextureInfo AddTexture(Image<Rgba32> image, TexFormat imgFormat = TexFormat.C8)
        {
            var gcnFormat = AtbTextureInfo.FormatList[imgFormat];

            AtbTextureInfo tex = new AtbTextureInfo();

            ImageFormatGcn format = new ImageFormatGcn(gcnFormat);
            format.Palette = new ImageLibrary.Formats.Encoders.Gcn.GcnPalette(ImageFormatGcn.GcnPaletteFormats.RGB5A3);
            if (imgFormat == TexFormat.C4 || imgFormat == TexFormat.C8)
            {
                var raw = format.EncodeWithPalette(image.GetSourceInBytes(), (uint)image.Width, (uint)image.Height);
                var size = format.GetSize((uint)image.Width, (uint)image.Height);

                tex.PaletteData = format.Palette.GetBytes();
                tex.ImageData = raw;
            }
            else
            {
                var raw = format.Encode(image.GetSourceInBytes(), (uint)image.Width, (uint)image.Height);
                var size = format.GetSize((uint)image.Width, (uint)image.Height);

                tex.PaletteData = new byte[0];
                tex.ImageData = raw;
            }

            tex.Bpp = 8;
            tex.Format = imgFormat;
            tex.Width = (ushort)image.Width;
            tex.Height = (ushort)image.Height;
            this.Textures.Add(tex);

            return tex;
        }

        void Read(FileReader reader)
        {
            reader.SetByteOrder(true);
            ushort numBanks = reader.ReadUInt16();
            ushort numPatterns = reader.ReadUInt16();
            ushort numTextures = reader.ReadUInt16();
            NumReferences = reader.ReadUInt16();
            uint bankOffset = reader.ReadUInt32();
            uint patternDataOffset = reader.ReadUInt32();
            uint textureDataOffset = reader.ReadUInt32();

            for (int i = 0; i < numBanks; i++)
            {
                reader.SeekBegin(bankOffset + (i * 0x8));
                Banks.Add(new BankData(reader));
            }

            for (int i = 0; i < numPatterns; i++)
            {
                reader.SeekBegin(patternDataOffset + (i * 0x10));
                Patterns.Add(new PatternData(reader));
            }

            for (int i = 0; i < numTextures; i++)
            {
                reader.SeekBegin(textureDataOffset + (i * 0x14));

                AtbTextureInfo tex = new AtbTextureInfo();
                Textures.Add(tex);

                tex.Bpp = reader.ReadByte();
                tex.Format = (TexFormat)reader.ReadByte();
                ushort PaletteSize = reader.ReadUInt16();
                tex.Width = reader.ReadUInt16();
                tex.Height = reader.ReadUInt16();
                uint ImageSize = reader.ReadUInt32();
                uint PaletteOffset = reader.ReadUInt32();
                uint ImageOffset = reader.ReadUInt32();

                if (PaletteSize != 0)
                {
                    using (reader.TemporarySeek(PaletteOffset, SeekOrigin.Begin))
                        tex.PaletteData = reader.ReadBytes(PaletteSize * 2);
                }
                if (ImageSize != 0)
                {
                    using (reader.TemporarySeek(ImageOffset, SeekOrigin.Begin))
                        tex.ImageData = reader.ReadBytes((int)ImageSize);
                }
            }
        }

        void Write(FileWriter writer)
        {
            writer.SetByteOrder(true);
            writer.Write((ushort)Banks.Count);
            writer.Write((ushort)Patterns.Count);
            writer.Write((ushort)Textures.Count);
            writer.Write(NumReferences);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            if (Patterns.Count > 0) {
                long patternsPos = writer.Position;

                writer.WriteUint32Offset(0xC);
                for (int i = 0; i < Patterns.Count; i++)
                    Patterns[i].Write(writer);

                for (int i = 0; i < Patterns.Count; i++)
                {
                    if (Patterns[i].Layers.Count > 0)
                    {
                        writer.WriteUint32Offset(patternsPos + 0xC + (i * 0x10));
                        foreach (var layer in Patterns[i].Layers)
                            writer.WriteStruct(layer);
                    }
                }
            }
            if (Banks.Count > 0) {
                long banksPos = writer.Position;

                writer.WriteUint32Offset(0x8);
                for (int i = 0; i < Banks.Count; i++)
                    Banks[i].Write(writer);

                for (int i = 0; i < Banks.Count; i++)
                {
                    if (Banks[i].AnimFrames.Count > 0)
                    {
                        writer.WriteUint32Offset(banksPos + 4 + (i * 0x08));
                        foreach (var frame in Banks[i].AnimFrames)
                            writer.WriteStruct(frame);
                    }
                }
            }
            if (Textures.Count > 0) {
                writer.WriteUint32Offset(0x10);
                long texPos = writer.Position;
                for (int i = 0; i < Textures.Count; i++)
                {
                    writer.Write(Textures[i].Bpp);
                    writer.Write((byte)Textures[i].Format);
                    if (Textures[i].PaletteData != null)
                        writer.Write((ushort)(Textures[i].PaletteData.Length / 2));
                    else
                        writer.Write((ushort)0);
                    writer.Write(Textures[i].Width);
                    writer.Write(Textures[i].Height);
                    writer.Write(Textures[i].ImageData.Length);
                    writer.Write(0);
                    writer.Write(0);
                }

                writer.Align(32, 0x88);
                for (int i = 0; i < Textures.Count; i++)
                {
                    writer.Align(8);
                    writer.WriteUint32Offset(texPos + 0x0C + (0x14 * i));
                    if (Textures[i].PaletteData != null)
                        writer.Write(Textures[i].PaletteData);

                    writer.Align(8);
                    writer.WriteUint32Offset(texPos + 0x10 + (0x14 * i));
                    writer.Write(Textures[i].ImageData);
                }
            }
        }
    }

    public class AtbTextureInfo
    {
        public byte Bpp;
        public TexFormat Format;
        public ushort Width;
        public ushort Height;

        [JsonIgnore]
        public byte[] ImageData;
        [JsonIgnore]
        public byte[] PaletteData = new byte[0];

        public static ImageFormatGcn.GcnTextureFormats GetGcnFormat(TexFormat format)
        {
            switch (format)
            {
                case TexFormat.C8: return ImageFormatGcn.GcnTextureFormats.C8;
            }

            return ImageFormatGcn.GcnTextureFormats.C8;
        }

        public static Dictionary<TexFormat, ImageFormatGcn.GcnTextureFormats> FormatList = new()
        {
            {  TexFormat.RGBA8, ImageFormatGcn.GcnTextureFormats.RGBA32 },
            {  TexFormat.RGB565, ImageFormatGcn.GcnTextureFormats.RGB565 },
            {  TexFormat.RGB5A3, ImageFormatGcn.GcnTextureFormats.RGB5A3 },
            {  TexFormat.C8, ImageFormatGcn.GcnTextureFormats.C8 },
            {  TexFormat.C4, ImageFormatGcn.GcnTextureFormats.C4 },
            {  TexFormat.I4, ImageFormatGcn.GcnTextureFormats.I4 },
            {  TexFormat.IA4, ImageFormatGcn.GcnTextureFormats.IA4 },
            {  TexFormat.I8, ImageFormatGcn.GcnTextureFormats.I8 },
            {  TexFormat.IA8, ImageFormatGcn.GcnTextureFormats.IA8 },
            {  TexFormat.CMPR, ImageFormatGcn.GcnTextureFormats.CMPR },
        };
    }

    public enum TexFormat : byte
    {
        RGBA8,
        RGB565,
        RGB5A3,
        C8,
        C4,
        IA8,
        IA4,
        I8,
        I4,
        A8,
        CMPR,
    }

    public class BankData
    {
        public List<AnimFrame> AnimFrames = new List<AnimFrame>();

        public BankData() { }
        public BankData(FileReader reader)
        {
            short frameCount = reader.ReadInt16();
            reader.ReadInt16(); //padding
            uint frameArrayOffset = reader.ReadUInt32();

            if (frameCount > 0) {
                reader.SeekBegin(frameArrayOffset);
                AnimFrames = reader.ReadMultipleStructs<AnimFrame>(frameCount);
            }
        }

        public void Write(FileWriter writer)
        {
            writer.Write((ushort)AnimFrames.Count);
            writer.Write((ushort)0);
            writer.Write(0);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class AnimFrame
    {
        public short PatternIndex;  
        public short FrameLength;
        public short ShiftX;
        public short ShiftY;
        public short Flip;
        public short Unk;
    }

    public class PatternData
    {
        public List<LayerData> Layers = new List<LayerData>();

        public short CenterX;
        public short CenterY;
        public short Width;
        public short Height;
        public short Padding;

        public PatternData() { }

        public PatternData(FileReader reader)
        {
            short numLayers = reader.ReadInt16();
            CenterX = reader.ReadInt16();
            CenterY = reader.ReadInt16();
            Width = reader.ReadInt16();
            Height = reader.ReadInt16();
            reader.ReadInt16(); //PADDING
            uint layerArrayOffset = reader.ReadUInt32();

            if (numLayers > 0)
            {
                reader.SeekBegin(layerArrayOffset);
                Layers = reader.ReadMultipleStructs<LayerData>(numLayers);
            }
        }

        public void Write(FileWriter writer)
        {
            writer.Write((ushort)Layers.Count);
            writer.Write(CenterX);
            writer.Write(CenterY);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write((ushort)0);
            writer.Write(0);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class LayerData
    {
        public byte Alpha;
        public byte Flip;
        public short TextureIndex;
        public short TexCoordTopLeftX;
        public short TexCoordTopLeftY;
        public short TexCoordWidth;
        public short TexCoordHeight;
        public short ShiftX;
        public short ShiftY;
        public short VertexTopLeftX;
        public short VertexTopLeftY;
        public short VertexTopRightX;
        public short VertexTopRightY;
        public short VertexBottomRightX;
        public short VertexBottomRightY;
        public short VertexBottomLeftX;
        public short VertexBottomLeftY;
    }
}

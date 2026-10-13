
using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PartyPatcher.Formats
{
    public class MsmPool
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class PoolHeader
        {
            public uint SoundMacrosOffset;
            public uint TablesOffset;
            public uint KeymapsOffset;
            public uint LayersOffset;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class ObjectHeader
        {
            public uint Size;
            public ushort ObjectId;
            public ushort Padding;
        }
        public class SoundMacro
        {
            public ushort ObjectId { get; set; }
            public List<ISoundMacroCommand> Commands { get; set; } = new();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SoundCommand
        {
            public byte Byte1;
            public byte Byte2;
            public byte Byte3;
            public CmdOp Op;
            public uint Param;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ADSR
        {
            public ushort Attack;
            public ushort Decay;
            public ushort Sustain;
            public ushort Release;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ADSRDLS
        {
            public uint Attack;
            public uint Decay;
            public ushort Sustain;
            public ushort Release;
            public uint VelToAttack;
            public uint KeyToDecay;
        }
        public class Table
        {
            public ushort ObjectId { get; set; }
            public byte[] RawData { get; set; } = Array.Empty<byte>(); // Has curves
            public ADSR? Adsr { get; set; }
            public ADSRDLS? AdsrDls { get; set; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Keymap
        {
            public ushort Macro;
            public byte Transpose;
            public byte Pan;
            public byte PrioOffset;
            public byte Pad1;
            public byte Pad2;
            public byte Pad3;
        }

        public class KeymapList
        {
            public ushort ObjectId { get; set; }
            public Keymap[] Keymaps { get; set; } = new Keymap[128];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct LayerMapping
        {
            public ushort Macro;
            public byte KeyLo;
            public byte KeyHi;
            public byte Transpose;
            public byte Volume;
            public byte PrioOffset;
            public byte Span;
            public byte Pan;
            public byte Pad1;
            public byte Pad2;
            public byte Pad3;
        }

        public class LayerList
        {
            public ushort ObjectId { get; set; }
            public List<LayerMapping> Mappings { get; set; } = new();
        }

        public PoolHeader Header { get; set; } = new();
        public List<SoundMacro> SoundMacros { get; } = new();
        public List<Table> Tables { get; } = new();
        public List<KeymapList> Keymaps { get; } = new();
        public List<LayerList> Layers { get; } = new();

        public MsmPool() { }
        public MsmPool(string filePath)
        {
            using var reader = new FileReader(filePath);
            Read(reader);
        }

        public MsmPool(Stream stream)
        {
            using var reader = new FileReader(stream);
            Read(reader);
        }

        public byte[] SaveBytes()
        {
            var mem = new MemoryStream();
            Save(mem);
            return mem.ToArray();
        }

        public void Save(Stream stream)
        {
            using (var writer = new FileWriter(stream))
                Write(writer);
        }
        public void Save(string filePath)
        {
            using (var writer = new FileWriter(filePath))
                Write(writer);
        }

        private void Read(FileReader reader)
        {
            reader.SetByteOrder(true);

            Header = reader.ReadStruct<PoolHeader>();

            // Sound Macros
            if (Header.SoundMacrosOffset != 0)
            {
                reader.SeekBegin(Header.SoundMacrosOffset);
                while (!ReadToEnd(reader))
                    SoundMacros.Add(ReadSoundMacro(reader));
            }

            // Tables
            if (Header.TablesOffset != 0)
            {
                reader.SeekBegin(Header.TablesOffset);
                while (!ReadToEnd(reader))
                    Tables.Add(ReadTable(reader));
            }

            // Keymaps
            if (Header.KeymapsOffset != 0)
            {
                reader.SeekBegin(Header.KeymapsOffset);
                while (!ReadToEnd(reader))
                    Keymaps.Add(ReadKeymapList(reader));
            }

            // Layers
            if (Header.LayersOffset != 0)
            {
                reader.SeekBegin(Header.LayersOffset);
                while (!ReadToEnd(reader))
                    Layers.Add(ReadLayerList(reader));
            }
        }

        private SoundMacro ReadSoundMacro(FileReader reader)
        {
            long start = reader.Position;
            var obj = reader.ReadStruct<ObjectHeader>();
            var macro = new SoundMacro { ObjectId = obj.ObjectId };

            var numCmds = (obj.Size - 8) / 8;
            for (int i = 0; i < numCmds; i++)
            {
                uint[] values = reader.ReadUInt32s(2);

                byte[] cmdBytes = BitConverter.GetBytes(values[0])
                    .Concat(BitConverter.GetBytes(values[1]))
                    .ToArray();
                CmdOp op = (CmdOp)cmdBytes[0];

                using var ms = new MemoryStream(cmdBytes.AsSpan().Slice(1).ToArray());
                using var cmdReader = new FileReader(ms);
                macro.Commands.Add(ISoundMacroCommand.ReadCommand(cmdReader, op));
            }
            reader.SeekBegin(start + obj.Size);
            return macro;
        }

        private void WriteSoundMacro(FileWriter writer, SoundMacro macro)
        {
            long start = writer.Position;
            var header = new ObjectHeader { ObjectId = macro.ObjectId };
            writer.WriteStruct(header);

            foreach (var cmd in macro.Commands)
                ISoundMacroCommand.WriteCommand(writer, cmd);

            long end = writer.Position;
            header.Size = (uint)(end - start);
            writer.SeekBegin(start);
            writer.WriteStruct(header);
            writer.SeekBegin(end);
        }

        private Table ReadTable(FileReader reader)
        {
            long start = reader.Position;
            var obj = reader.ReadStruct<ObjectHeader>();
            var table = new Table { ObjectId = obj.ObjectId };

            if (obj.Size == 0x10)
                table.Adsr = reader.ReadStruct<ADSR>();
            else if (obj.Size == 0x1C)
                table.AdsrDls = reader.ReadStruct<ADSRDLS>();
            else
                table.RawData = reader.ReadBytes((int)obj.Size - 8);

            reader.SeekBegin(start + obj.Size);
            return table;
        }

        private void WriteTable(FileWriter writer, Table table)
        {
            long start = writer.Position;
            var header = new ObjectHeader { ObjectId = table.ObjectId };

            if (table.Adsr.HasValue)
            {
                header.Size = 0x10;
                writer.WriteStruct(header);
                writer.WriteStruct(table.Adsr.Value);
            }
            else if (table.AdsrDls.HasValue)
            {
                header.Size = 0x1C;
                writer.WriteStruct(header);
                writer.WriteStruct(table.AdsrDls.Value);
            }
            else
            {
                header.Size = 8 + (uint)table.RawData.Length;
                writer.WriteStruct(header);
                writer.Write(table.RawData);
            }

            writer.SeekBegin(start);
            writer.WriteStruct(header);
            writer.SeekBegin(start + header.Size);
        }

        private KeymapList ReadKeymapList(FileReader reader)
        {
            long start = reader.Position;
            var obj = reader.ReadStruct<ObjectHeader>();
            var kmList = new KeymapList { ObjectId = obj.ObjectId };

            for (int i = 0; i < 128; i++)
                kmList.Keymaps[i] = reader.ReadStruct<Keymap>();

            reader.SeekBegin(start + obj.Size);
            return kmList;
        }

        private void WriteKeymapList(FileWriter writer, KeymapList kmList)
        {
            long start = writer.Position;
            var header = new ObjectHeader
            {
                ObjectId = kmList.ObjectId,
                Size = 8 + 128 * 8
            };

            writer.WriteStruct(header);
            foreach (var km in kmList.Keymaps)
                writer.WriteStruct(km);

            writer.SeekBegin(start);
            writer.WriteStruct(header);
            writer.SeekBegin(start + header.Size);
        }

        private LayerList ReadLayerList(FileReader reader)
        {
            long start = reader.Position;
            var obj = reader.ReadStruct<ObjectHeader>();
            var layerList = new LayerList { ObjectId = obj.ObjectId };

            uint count = reader.ReadUInt32();
            for (uint i = 0; i < count; i++)
                layerList.Mappings.Add(reader.ReadStruct<LayerMapping>());

            reader.SeekBegin(start + obj.Size);
            return layerList;
        }

        private void WriteLayerList(FileWriter writer, LayerList layerList)
        {
            long start = writer.Position;
            var header = new ObjectHeader { ObjectId = layerList.ObjectId };

            writer.WriteStruct(header);
            writer.Write((uint)layerList.Mappings.Count);

            foreach (var mapping in layerList.Mappings)
                writer.WriteStruct(mapping);

            long end = writer.Position;
            header.Size = (uint)(end - start);
            writer.SeekBegin(start);
            writer.WriteStruct(header);
            writer.SeekBegin(end);
        }

        public void Write(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            using var writer = new FileWriter(fs);
            Write(writer);
        }

        public void Write(Stream stream)
        {
            using var writer = new FileWriter(stream);
            Write(writer);
        }

        private void Write(FileWriter writer)
        {
            writer.SetByteOrder(true); // Big Endian

            long headerPos = writer.Position;
            writer.WriteStruct(Header);

            // Sound Macros
            if (SoundMacros.Count > 0)
            {
                Header.SoundMacrosOffset = (uint)writer.Position;
                foreach (var macro in SoundMacros)
                    WriteSoundMacro(writer, macro);

                writer.Write(uint.MaxValue);
            }
            // Tables
            if (Tables.Count > 0)
            {
                Header.TablesOffset = (uint)writer.Position;
                foreach (var table in Tables)
                    WriteTable(writer, table);

                writer.Write(uint.MaxValue);
            }
            // Keymaps
            if (Keymaps.Count > 0)
            {
                Header.KeymapsOffset = (uint)writer.Position;
                foreach (var km in Keymaps)
                    WriteKeymapList(writer, km);

                writer.Write(uint.MaxValue);
            }
            // Layers
            if (Layers.Count > 0)
            {
                Header.LayersOffset = (uint)writer.Position;
                foreach (var layer in Layers)
                    WriteLayerList(writer, layer);
            }

            writer.Write(uint.MaxValue);
            writer.Align(32);

            long endPos = writer.Position;
            writer.SeekBegin(headerPos);
            writer.WriteStruct(Header);
            writer.SeekBegin(endPos);
        }

        static bool ReadToEnd(FileReader reader)
        {
            using (reader.TemporarySeek(reader.Position, SeekOrigin.Begin))
            {
                return reader.ReadUInt32() == uint.MaxValue;
            }
        }
    }
}

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
    public class MsmProj
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class ProjHeader
        {
            public uint GroupEndOff;
            public ushort GroupId;
            public ushort Type;
            public uint SoundMacroIdsOff;
            public uint SamplIdsOff;
            public uint TableIdsOff;
            public uint KeymapIdsOff;
            public uint LayerIdsOff;
            public uint PageTableOff;
            public uint DrumTableOff;
            public uint MidiSetupsOff;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class SFXGroupIndex
        {
            public ushort SfxId;
            public ushort ObjId;
            public byte Priority;
            public byte MaxVoices;

            public byte DefVel;
            public byte Panning;
            public byte DefKey;
            public byte Pad;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class SongGroupIndex
        {
            public ushort ObjId;
            public byte Priority;
            public byte MaxVoices;
            public byte ProgramNo;
            public byte Pad;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MusyX1MIDISetup
        {
            public byte ProgramNo;
            public byte Volume;
            public byte Panning;
            public byte Reverb;
            public byte Chorus;
        }

        public class MidiList
        {
            public ushort SongID;
            public ushort Padding;
            public MusyX1MIDISetup[] Entries = new MusyX1MIDISetup[16];
        }

        public class ProjectEntry
        {
            internal ProjHeader Header;
            // List of macro IDs used from the pool data
            public List<ushort> SoundMacroIds = new();
            // List of IDs used for the sample data
            public List<ushort> SampleIds = new();

            public List<ushort> TableIds = new();
            public List<ushort> KeymapIds = new();
            public List<ushort> LayerIds = new();

            public List<SongGroupIndex> NormalPages { get; set; } = new();
            public List<SongGroupIndex> DrumTable { get; set; } = new();
            public List<MidiList> MidiLists { get; set; } = new();

            public List<SFXGroupIndex> SFXPages { get; set; } = new();
        }

        public List<ProjectEntry> Entries { get; } = new();

        public MsmProj() { }
        public MsmProj(Stream stream)
        {
            using (var reader = new FileReader(stream))
                Read(reader);
        }
        public MsmProj(string filePath)
        {
            using (var reader = new FileReader(filePath))
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
            while (!ReadToEnd(reader))
            {
                ProjectEntry entry = new();
                long entryStart = reader.Position;
                entry.Header = reader.ReadStruct<ProjHeader>();

                reader.SeekBegin(entryStart + entry.Header.SoundMacroIdsOff);
                entry.SoundMacroIds = ReadIDs(reader);

                reader.SeekBegin(entryStart + entry.Header.SamplIdsOff);
                entry.SampleIds = ReadIDs(reader);

                reader.SeekBegin(entryStart + entry.Header.TableIdsOff);
                entry.TableIds = ReadIDs(reader);

                reader.SeekBegin(entryStart + entry.Header.KeymapIdsOff);
                entry.KeymapIds = ReadIDs(reader);

                reader.SeekBegin(entryStart + entry.Header.LayerIdsOff);
                entry.LayerIds = ReadIDs(reader);

                if (entry.Header.Type == 0) // Song
                {
                    reader.SeekBegin(entryStart + entry.Header.PageTableOff);
                    entry.NormalPages = ReadSongIndexList(reader);

                    reader.SeekBegin(entryStart + entry.Header.DrumTableOff);
                    entry.DrumTable = ReadSongIndexList(reader);

                    reader.SeekBegin(entryStart + entry.Header.MidiSetupsOff);
                    entry.MidiLists = ReadMidiLists(reader, entryStart + entry.Header.GroupEndOff);
                }
                else if (entry.Header.Type == 1) // SFX
                {
                    reader.SeekBegin(entryStart + entry.Header.PageTableOff);
                    ushort pageCount = reader.ReadUInt16();
                    reader.ReadUInt16(); // padding

                    for (int i = 0; i < pageCount; i++)
                        entry.SFXPages.Add(reader.ReadStruct<SFXGroupIndex>());
                }
                reader.SeekBegin(entryStart + entry.Header.GroupEndOff);
                Entries.Add(entry);
            }
        }

        private void Write(FileWriter writer)
        {
            writer.SetByteOrder(true);
            foreach (var entry in Entries)
            {
                long entryStart = writer.Position;

                writer.WriteStruct(entry.Header);

                long dataStart = writer.Position;

                // SoundMacro IDs
                entry.Header.SoundMacroIdsOff = (uint)(writer.Position - entryStart);
                WriteIDList(writer, entry.SoundMacroIds);

                // Sample IDs
                entry.Header.SamplIdsOff = (uint)(writer.Position - entryStart);
                WriteIDList(writer, entry.SampleIds);

                // Table IDs
                entry.Header.TableIdsOff = (uint)(writer.Position - entryStart);
                WriteIDList(writer, entry.TableIds);

                // Keymap IDs
                entry.Header.KeymapIdsOff = (uint)(writer.Position - entryStart);
                WriteIDList(writer, entry.KeymapIds);

                // Layer IDs
                entry.Header.LayerIdsOff = (uint)(writer.Position - entryStart);
                WriteIDList(writer, entry.LayerIds);

                writer.Align(4);
                if (entry.Header.Type == 0) // Song
                {
                    entry.Header.PageTableOff = (uint)(writer.Position - entryStart);
                    WriteSongIndexList(writer, entry.NormalPages);

                    entry.Header.DrumTableOff = (uint)(writer.Position - entryStart);
                    WriteSongIndexList(writer, entry.DrumTable);

                    writer.Align(4);
                    entry.Header.MidiSetupsOff = (uint)(writer.Position - entryStart);
                    WriteMidiLists(writer, entry.MidiLists);

                }
                else // SFX
                {
                    entry.Header.PageTableOff = (uint)(writer.Position - entryStart);
                    writer.Write((ushort)entry.SFXPages.Count);
                    writer.Write((ushort)0); // pad

                    foreach (var page in entry.SFXPages)
                        writer.WriteStruct(page);
                }

                entry.Header.GroupEndOff = (uint)(writer.Position - entryStart);

                long currentPos = writer.Position;
                writer.SeekBegin(entryStart);
                writer.WriteStruct(entry.Header);
                writer.SeekBegin(currentPos);
            }

            writer.Write(uint.MaxValue);
            writer.Align(16);
        }

        private static void WriteIDList(FileWriter writer, List<ushort> ids)
        {
            foreach (var id in ids)
                writer.Write(id);
            writer.Write(ushort.MaxValue);
        }

        private static void WriteSongIndexList(FileWriter writer, List<SongGroupIndex> list)
        {
            foreach (var item in list)
                writer.WriteStruct(item);
            writer.Write(uint.MaxValue);
            writer.Write(uint.MaxValue);
        }

        private static void WriteMidiLists(FileWriter writer, List<MidiList> lists)
        {
            foreach (var midi in lists)
            {
                writer.Write(midi.SongID);
                writer.Write(midi.Padding);

                for (int i = 0; i < 16; i++)
                    writer.WriteStruct(midi.Entries[i]);
            }
        }

        static bool ReadToEnd(FileReader reader)
        {
            using (reader.TemporarySeek(reader.Position, SeekOrigin.Begin)) {
                return reader.ReadUInt32() == uint.MaxValue;
            }
        }

        static bool ReadToEnd16(FileReader reader)
        {
            using (reader.TemporarySeek(reader.Position, SeekOrigin.Begin))
            {
                return reader.ReadUInt16() == ushort.MaxValue;
            }
        }

        static List<ushort> ReadIDs(FileReader reader)
        {
            List<ushort> ids = new();
            while (!reader.EndOfStream)
            {
                ushort id = reader.ReadUInt16();
                if (id == ushort.MaxValue)
                    break;
                ids.Add(id);
            }
            return ids;
        }

        private static List<SongGroupIndex> ReadSongIndexList(FileReader reader)
        {
            var list = new List<SongGroupIndex>();
            while (!ReadToEnd16(reader))
            {
                list.Add(reader.ReadStruct<SongGroupIndex>());
            }
            return list;
        }

        private static List<MidiList> ReadMidiLists(FileReader reader, long endOffset)
        {
            var lists = new List<MidiList>();
            while (reader.Position + 4 < endOffset)
            {
                var midi = new MidiList
                {
                    SongID = reader.ReadUInt16(),
                    Padding = reader.ReadUInt16(),
                    Entries = new MusyX1MIDISetup[16]
                };

                for (int i = 0; i < 16; i++)
                    midi.Entries[i] = reader.ReadStruct<MusyX1MIDISetup>();

                lists.Add(midi);
            }
            return lists;
        }
    }
}

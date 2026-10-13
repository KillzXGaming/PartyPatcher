using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VGAudio.Containers.Wave;
using VGAudio.Formats;
using VGAudio.Formats.GcAdpcm;

namespace PartyPatcher.Formats
{
    public class MsmFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MsmHeader
        {
            public Magic Magic = "GSND";
            public uint Version = 2;
            public uint EndOfs; // end of file
            public uint EndSize;
            public uint InfoOfs = 96;
            public uint InfoSize;
            public uint AuxParamOfs;
            public uint AuxParamSize;
            public uint GrpInfoOfs;
            public uint GrpInfoSize;
            public uint MusOfs;
            public uint MusSize;
            public uint SeOfs;
            public uint SeSize;
            public uint GrpDataOfs; // start data for groups
            public uint GrpDataSize;
            public uint SampDataOfs; // start data for groups
            public uint SampSize;
            public uint DummyMusOfs;
            public uint DummyMusSize;
            public uint GrpSetOfs;
            public uint GrpSetSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MSM_INFO
        {
            public byte Voices;
            public byte Music;
            public byte Sfx;
            public byte GrpMax;

            public ushort MusMax;
            public ushort SeMax;

            public byte MusChanMax;
            public byte StackDepthA;
            public byte StackDepthB;
            public byte SurroundF;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] unkC = new byte[2];
            public byte AuxParamA;
            public byte AuxParamB;

            public uint MinMem;
            public uint AramSize;
            public uint GrpBufSizeA;
            public uint GrpBufSizeB;
            public uint DummyMusSize;
            public uint Unk24;

            public byte BaseGrpNum;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 23)]
            public byte[] BaseGrp = new byte[23];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MSM_AUXPARAM_REVERBHI
        {
            public uint TempDisableFX;
            public float Coloration;
            public float Mix;
            public float Time;
            public float Damping;
            public float PreDelay;
            public float Crosstalk;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MSM_AUXPARAM_REVERBSTD
        {
            public uint TempDisableFX;
            public float Coloration;
            public float Mix;
            public float Time;
            public float Damping;
            public float PreDelay;
            public float Crosstalk;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MSM_AUXPARAM_CHORUS
        {
            public uint BaseDelay;
            public uint Variation;
            public uint Period;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MSM_AUXPARAM_DELAY
        {
            public uint Delay1;
            public uint Delay2;
            public uint Delay3;

            public uint Feedback1;
            public uint Feedback2;
            public uint Feedback3;

            public uint Output1;
            public uint Output2;
            public uint Output3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MSM_GRP_INFO
        {
            public ushort Gid;
            public byte StackNo;
            public byte SubGrpId;
            public int DataOfs;
            public int DataSize;
            public int SampOfs;
            public int SampSize;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] Pad;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MSM_GRP_HEAD
        {
            public int PoolOfs;
            public int ProjOfs;
            public int SdirOfs;
            public int SngOfs;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MSM_SE
        {
            public ushort Gid;
            public ushort FxId;
            public byte Vol;
            public byte Pan;
            public short PitchBend;
            public byte Span;
            public byte Reverb;
            public byte Chorus;
            public byte EmitterF;
            public byte EmiComp;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] Pad;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MSM_MUS
        {
            public ushort SgId;
            public ushort Sid;
            public uint SongOfs;
            public uint SongSize;
            public byte SongGrp;
            public byte Vol;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] Pad;
        }

        public class AuxParam
        {
            public object Data { get; set; }

            public void Read(FileReader r)
            {
                long start = r.Position;

                byte type = r.ReadByte();
                r.ReadBytes(3);

                Data = type switch
                {
                    0 => r.ReadStruct<MSM_AUXPARAM_REVERBHI>(),
                    1 => r.ReadStruct<MSM_AUXPARAM_REVERBSTD>(),
                    2 => r.ReadStruct<MSM_AUXPARAM_CHORUS>(),
                    3 => r.ReadStruct<MSM_AUXPARAM_DELAY>(),
                };

                r.SeekBegin(start + 40);
            }

            public void Write(FileWriter writer)
            {
                long start = writer.Position;

                int type = 0;
                if      (Data is MSM_AUXPARAM_REVERBHI) type = 0;
                else if (Data is MSM_AUXPARAM_REVERBSTD) type = 1;
                else if (Data is MSM_AUXPARAM_CHORUS) type = 2;
                else if (Data is MSM_AUXPARAM_DELAY) type = 3;
                else
                    throw new Exception($"Invalid data for AUX param!");

                writer.Write((byte)type);
                writer.Write(new byte[3]);
                if (Data is MSM_AUXPARAM_REVERBHI) 
                    writer.WriteStruct((MSM_AUXPARAM_REVERBHI)this.Data);
                else if (Data is MSM_AUXPARAM_REVERBSTD)
                    writer.WriteStruct((MSM_AUXPARAM_REVERBSTD)this.Data);
                else if (Data is MSM_AUXPARAM_CHORUS)
                    writer.WriteStruct((MSM_AUXPARAM_CHORUS)this.Data);
                else if (Data is MSM_AUXPARAM_DELAY)
                    writer.WriteStruct((MSM_AUXPARAM_DELAY)this.Data);

                writer.SeekBegin(start + 40);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class ADPCMParms
        {
            public ushort m_bytesPerFrame;
            public byte m_ps;
            public byte m_lps;
            public short m_hist2;
            public short m_hist1;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public short[] m_coefs = new short[16];
        }
        

        public class MusGroupEntry
        {
            public MSM_GRP_INFO Header;

            public byte[] PoolData = new byte[0]; // Pool of commands
            public byte[] ProjData = new byte[0];  // Project info
            public byte[] SDirData = new byte[0]; // DSP audio info
            public byte[] SngData = new byte[0]; // Midi like audio (can convert with amuse to midi)
            public byte[] SampleData = new byte[0]; // DSP sample data

            public byte[] GetMusSng(MSM_MUS mus)
            {
                var sngSlice = this.SngData.AsSpan();
                return sngSlice.Slice((int)mus.SongOfs, (int)mus.SongSize).ToArray();
            }

            public void DumpProject(string folder)
            {
                string name = Header.Gid.ToString("D4");

                Directory.CreateDirectory(Path.Combine(folder, name));

                var sdirs = this.GetSDirs();
                int idx = 0;
                foreach (var sdir in sdirs)
                {
                    try
                    {
                        sdir.Export(Path.Combine(folder, name, $"{idx.ToString("D4")}.wav"));
                    }
                    catch { }
                    idx++;
                }
                /*
                File.WriteAllBytes(Path.Combine(folder, $"{name}.proj"), ProjData);
                File.WriteAllBytes(Path.Combine(folder, $"{name}.pool"), PoolData);
                File.WriteAllBytes(Path.Combine(folder, $"{name}.sdir"), SDirData);
                File.WriteAllBytes(Path.Combine(folder, $"{name}.samp"), SampleData);*/
            }

            public void ImportProject(string folder)
            {
                string name = Header.Gid.ToString("X4");

                Directory.CreateDirectory(folder);
                ProjData = File.ReadAllBytes(Path.Combine(folder, $"{name}.proj"));
                PoolData = File.ReadAllBytes(Path.Combine(folder, $"{name}.pool"));
                SDirData = File.ReadAllBytes(Path.Combine(folder, $"{name}.sdir"));
                SampleData = File.ReadAllBytes(Path.Combine(folder, $"{name}.samp"));
            }

            public List<SDIREntry> GetSDirs()
            {
                var samplesSlice = this.SampleData.AsSpan();

                List<SDIREntry> sdirs = new();
                using (var reader = new FileReader(SDirData))
                {
                    reader.SetByteOrder(true);
                    while (!reader.EndOfStream)
                    {
                        uint temp = reader.ReadUInt32();
                        if (temp == 0xffffffff) // Terminator
                            break;

                        reader.Seek(-4, SeekOrigin.Current);

                        SDIREntry entry = new();
                        entry.Header = reader.ReadStruct<SDIR_ENTRY>();
                        sdirs.Add(entry);

                        if (entry.Header.m_adpcmParmOffset != 0) {
                            using (reader.TemporarySeek(entry.Header.m_adpcmParmOffset, SeekOrigin.Begin))
                                entry.ADPCMParms = reader.ReadStruct<ADPCMParms>();
                        }

                        uint remSamples = entry.Header.m_numSample;
                        uint numFrames = (remSamples + 13) / 14;
                        uint sampleSize = numFrames * 8;

                        entry.Samples = samplesSlice.Slice(
                           (int)entry.Header.m_sampleOff, (int)sampleSize).ToArray();
                    }
                }
                return sdirs;
            }

            public void RebuildSDirs(List<SDIREntry> entries)
            {
                var mem = new MemoryStream();
                var memSamples = new MemoryStream();
                using (var writer = new FileWriter(mem))
                using (var sampleWriter = new FileWriter(memSamples))
                {
                    writer.SetByteOrder(true);

                    foreach (var entry in entries)
                        writer.WriteStruct(entry.Header);
                    writer.Write(0xffffffff);

                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].ADPCMParms != null)
                        {
                            writer.WriteUint32Offset(i * 32 + 28);
                            writer.WriteStruct(entries[i].ADPCMParms);
                        }
                    }
                    for (int i = 0; i < entries.Count; i++)
                    {
                       // sampleWriter.SeekBegin(entries[i].Header.m_sampleOff);
                            
                        if (entries[i].Samples != null)
                        {
                            sampleWriter.Align(32);
                            using (writer.TemporarySeek(i * 32 + 4, SeekOrigin.Begin))
                                writer.Write((uint)sampleWriter.Position);

                            sampleWriter.Write(entries[i].Samples);
                        }
                    }
                    writer.Align(32);
                    sampleWriter.Align(32);
                }
                this.SDirData = mem.ToArray();
                this.SampleData = memSamples.ToArray();
            }
        }

        // SDIR
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SDIR_ENTRY
        {
            public ushort m_sfxId;
            public ushort pad;
            public uint m_sampleOff;
            public uint m_unk;

            public byte m_pitch;
            public byte pad2;
            public ushort sampleRate;

            public uint m_numSampleFlags; // Top 8 bits is SampleFormat
            public uint m_loopStartSample;
            public uint m_loopLengthSamples;
            public uint m_adpcmParmOffset;

            public uint m_numSample
            {
                get => m_numSampleFlags & 0xffffff;
                set
                {
                    const uint formatMask = 0xFF00_0000u;
                    const uint sampleMask = 0x00FF_FFFFu;
                    m_numSampleFlags = (m_numSampleFlags & formatMask) | (value & sampleMask);
                }
            }
            public uint m_sampleFormat // Always 0 (DSP) for GCN mario party
            {
                get => m_numSampleFlags >> 24;
                set
                {
                    const uint sampleMask = 0x00FF_FFFFu;
                    m_numSampleFlags = (m_numSampleFlags & sampleMask) | ((value & 0xFFu) << 24);

                }
            }
        }

        public class SDIREntry
        {
            public SDIR_ENTRY Header;
            public ADPCMParms ADPCMParms; 
            public byte[] Samples;

            // Creates sample entry from a given wav file
            public static SDIREntry Create(string filePath, ushort sfxId)
            {
                SDIREntry sdir = new()
                {
                    Header = new SDIR_ENTRY()
                    {
                        m_pitch = 60,
                        m_sfxId = sfxId,
                        m_sampleFormat = 0, // GCN DSP
                    },
                };
                sdir.Import(filePath);
                return sdir;
            }

            public void Export(string filePath)
            {
                WaveWriter wave = new WaveWriter();
                wave.WriteToStream(ToAudioFormat(), new FileStream(filePath, FileMode.Create, FileAccess.Write));
            }

            public void Import(string filePath)
            {
                // Todo detect and support raw dsp too
                WaveReader wave = new WaveReader();
                FromAudioFormat(wave.ReadFormat(File.OpenRead(filePath)));
            }

            private void FromAudioFormat(IAudioFormat audioFormat, byte volume = 108)
            {
                var format = new GcAdpcmFormat().EncodeFromPcm16(audioFormat.ToPcm16());
                this.Header.m_loopStartSample = (uint)format.LoopStart;
                this.Samples = format.Channels[0].Adpcm;
                this.ADPCMParms = new ADPCMParms()
                {
                    m_bytesPerFrame = 8, // 8 bytes per frame for DSP
                    m_lps = 0,
                    m_coefs = format.Channels[0].Coefs,
                };

                try
                {
                    this.ADPCMParms.m_hist1 = format.Channels[0].GetHist1(format.SampleCount);
                    this.ADPCMParms.m_hist2 = format.Channels[0].GetHist2(format.SampleCount);
                    this.ADPCMParms.m_ps = format.Channels[0].GetPredScale(format.SampleCount);
                }
                catch
                {

                }

                this.Header.sampleRate = (ushort)format.SampleRate;
                this.Header.m_numSample = (ushort)format.SampleCount;
                this.Header.m_sampleFormat = 0;
            }

            public IAudioFormat ToAudioFormat()
            {
                GcAdpcmChannel[] channels = new GcAdpcmChannel[1];
                for (int i = 0; i < channels.Length; i++)
                {
                    var sampleCount = this.Header.m_numSample;
                    channels[i] = new GcAdpcmChannel(

                        this.Samples,
                        this.ADPCMParms == null ? new short[16] : this.ADPCMParms.m_coefs,
                        (int)sampleCount);
                }
                return new GcAdpcmFormat(channels, (int)this.Header.sampleRate);
            }
        }

        public MsmHeader Header;
        public List<MSM_INFO> Infos = new();
        public List<AuxParam> AuxParams = new();
        public List<MusGroupEntry> Groups = new();
        public List<MSM_MUS> MusEntries = new();
        public List<MSM_SE> SeEntries = new();
        public byte[] DummyData = new byte[0];
        public byte[] GrpSet;

        public MsmFile(Stream stream)
        {
            using (var reader = new FileReader(stream))
                Read(reader);
        }
        public MsmFile(string filePath)
        {
            using (var reader = new FileReader(filePath))
                Read(reader);
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

        public void Dump(string folder)
        {
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.Combine(folder, "Groups"));
            Directory.CreateDirectory(Path.Combine(folder, "Songs"));

            foreach (var g in this.Groups)
                g.DumpProject(Path.Combine(folder, "Groups"));
            foreach (var m in this.MusEntries)
            {
               if (m.SongGrp != 255)
                {
                    var sng = this.Groups[m.SongGrp].GetMusSng(m);
                    var id = $"{m.SongGrp}_{m.SgId.ToString("X4")}_{m.Sid}";
                    File.WriteAllBytes(Path.Combine(folder, "Songs", $"{id}.sng"), sng);
                }
            }

            var options = new JsonSerializerOptions() { WriteIndented = true, IncludeFields = true };
            var grpJson = JsonSerializer.Serialize(this.GrpSet.ToList(), options);
            var infoJson = JsonSerializer.Serialize(this.Infos, options);
            var seJson = JsonSerializer.Serialize(this.SeEntries, options);
            var musJson = JsonSerializer.Serialize(this.MusEntries, options);
            var auxJson = JsonSerializer.Serialize(this.AuxParams, options);
            File.WriteAllText(Path.Combine(folder, "grp_set.json"), grpJson);
            File.WriteAllText(Path.Combine(folder, "info.json"), infoJson);
            File.WriteAllText(Path.Combine(folder, "sound_effects.json"), seJson);
            File.WriteAllText(Path.Combine(folder, "songs.json"), musJson);
            File.WriteAllText(Path.Combine(folder, "aux_params.json"), auxJson);
        }

        private void Read(FileReader reader)
        {
            reader.SetByteOrder(true);
            Header = reader.ReadStruct<MsmHeader>();

            reader.SeekBegin(Header.InfoOfs);
            Infos = reader.ReadMultipleStructs<MSM_INFO>(Header.InfoSize / 64);

            reader.SeekBegin(Header.AuxParamOfs);
            for (int i = 0; i < Header.AuxParamSize / 40; i++)
            {
                AuxParam param = new();
                param.Read(reader);
                AuxParams.Add(param);
            }

            reader.SeekBegin(Header.GrpInfoOfs);
            for (int i = 0; i < Header.GrpInfoSize / 32; i++)
            {
                MusGroupEntry grp = new();
                grp.Header = reader.ReadStruct<MSM_GRP_INFO>();
                Groups.Add(grp);

                // Read group data
                if (grp.Header.DataSize > 0)
                {
                    using (reader.TemporarySeek(grp.Header.DataOfs + Header.GrpDataOfs, SeekOrigin.Begin))
                    {
                        var start = reader.Position;
                        var dataHeader = reader.ReadStruct<MSM_GRP_HEAD>();

                        var poolSize = dataHeader.ProjOfs - dataHeader.PoolOfs;
                        var projSize = dataHeader.SdirOfs - dataHeader.ProjOfs;
                        var sdirSize = dataHeader.SngOfs - dataHeader.SdirOfs;
                        var sndSize = grp.Header.DataSize - dataHeader.SngOfs;

                        reader.SeekBegin(start + dataHeader.PoolOfs);
                        grp.PoolData = reader.ReadBytes(poolSize);

                        reader.SeekBegin(start + dataHeader.ProjOfs);
                        grp.ProjData = reader.ReadBytes(projSize);

                        reader.SeekBegin(start + dataHeader.SdirOfs);
                        grp.SDirData = reader.ReadBytes(sdirSize);

                        reader.SeekBegin(start + dataHeader.SngOfs);
                        grp.SngData = reader.ReadBytes(sndSize);
                    }
                }
                // Read sample buffer
                if (grp.Header.SampSize > 0)
                {
                    using (reader.TemporarySeek(grp.Header.SampOfs + Header.SampDataOfs, SeekOrigin.Begin))
                    {
                        grp.SampleData = reader.ReadBytes(grp.Header.SampSize);
                    }
                }

               // var sdirs = grp.GetSDirs();
                //grp.RebuildSDirs(sdirs);

                Console.WriteLine($"{i} Gid {grp.Header.Gid} StackNo {grp.Header.StackNo} SubGrpId {grp.Header.SubGrpId}");
            }

            reader.SeekBegin(Header.MusOfs);
            MusEntries = reader.ReadMultipleStructs<MSM_MUS>(Header.MusSize / 16);

            reader.SeekBegin(Header.SeOfs);
            SeEntries = reader.ReadMultipleStructs<MSM_SE>(Header.SeSize / 16);

            reader.SeekBegin(Header.DummyMusOfs);
            DummyData = reader.ReadBytes((int)Header.DummyMusSize);

            reader.SeekBegin(Header.GrpSetOfs);
            GrpSet = reader.ReadBytes((int)Header.GrpSetSize);

            int numGrpSets = GrpSet[0];
            int grpStride = GrpSet[1]; // 6

            for (int i = 0; i < numGrpSets; i++)
            {
                int id = GrpSet[2 + i * grpStride];

              //  Console.WriteLine($"id {id}");
            }

            var mem_sizeA = this.Infos[0].GrpBufSizeA * this.Infos[0].StackDepthA;
            var mem_sizeB = this.Infos[0].GrpBufSizeB * this.Infos[0].StackDepthB;

            int totalA = 0; // 240064
            int totalB = 0; // 86528

            foreach (var g in this.Groups)
            {
                if (g.Header.StackNo == 0)
                    totalA++;
                else
                    totalB++;
            }
            Console.WriteLine();
        }

        private void Write(FileWriter writer)
        {
            Infos[0].GrpMax = (byte)this.Groups.Count;
            Infos[0].MusMax = (byte)this.MusEntries.Count;
          //  Infos[0].SeMax = (ushort)this.SeEntries.Count;
            Infos[0].DummyMusSize = (uint)this.DummyData.Length;

            writer.SetByteOrder(true);
            writer.WriteStruct(this.Header);

            writer.SeekBegin(96);

            writer.WriteUint32Offset(16);
            writer.WriteMultiStruct(Infos);
            writer.WriteSectionSizeU32(20, Infos.Count * 64);

            writer.WriteUint32Offset(24);
            foreach (var param in AuxParams)
                param.Write(writer);
            writer.WriteSectionSizeU32(28, AuxParams.Count * 40);

            var grpStart = writer.Position;
            writer.WriteUint32Offset(32);
            foreach (var grp in Groups)
                writer.WriteStruct(grp.Header);
            writer.WriteSectionSizeU32(36, Groups.Count * 32);

            writer.WriteUint32Offset(40);
            writer.WriteMultiStruct(MusEntries);
            writer.WriteSectionSizeU32(44, MusEntries.Count * 16);

            writer.WriteUint32Offset(48);
            writer.WriteMultiStruct(SeEntries);
            writer.WriteSectionSizeU32(52, SeEntries.Count * 16);

            var grpDataStart = writer.Position;
            writer.WriteUint32Offset(56);
            for (int i = 0; i < Groups.Count; i++)
            {
                var grp = Groups[i];
                var dataStart = writer.Position;
                // data ofs
                if (grp.PoolData?.Length > 0)
                {
                    writer.WriteUint32Offset(grpStart + i * 32 + 4, grpDataStart);

                    // Write header
                    var ofs = 32;
                    writer.Write(ofs); ofs += grp.PoolData.Length;  // pool ofs
                    writer.Write(ofs); ofs += grp.ProjData.Length;  // proj ofs
                    writer.Write(ofs); ofs += grp.SDirData.Length;  // sdir ofs
                    writer.Write(ofs); ofs += grp.SngData.Length;  // snd ofs
                    writer.Write(new byte[16]);
                    writer.Write(grp.PoolData);
                    writer.Write(grp.ProjData);
                    writer.Write(grp.SDirData);
                    writer.Write(grp.SngData);
                }
                // data size
                writer.WriteSectionSizeU32(grpStart + i * 32 + 8, writer.Position - dataStart);
            }
            writer.WriteSectionSizeU32(60, writer.Position - grpDataStart);

            // Sample data
            var sampleDataStart = writer.Position;

            writer.WriteUint32Offset(64);
            for (int i = 0; i < Groups.Count; i++)
            {
                var dataStart = writer.Position;
                // sample ofs
                if (this.Groups[i].SampleData?.Length > 0)
                {
                    writer.Align(8);
                    writer.WriteUint32Offset(grpStart + i * 32 + 12, sampleDataStart);
                    writer.Write(this.Groups[i].SampleData);
                }
                // sample size
                writer.WriteSectionSizeU32(grpStart + i * 32 + 16, writer.Position - dataStart);
            }
            writer.WriteSectionSizeU32(68, writer.Position - sampleDataStart);

            writer.WriteUint32Offset(72);
            writer.Write(this.DummyData);
            writer.WriteSectionSizeU32(76, this.DummyData.Length);

            writer.WriteUint32Offset(80);
            writer.Write(this.GrpSet);
            writer.WriteSectionSizeU32(84, this.GrpSet.Length);
        }
    }
}

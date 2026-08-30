using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VGAudio;
using VGAudio.Containers.Wave;
using VGAudio.Formats;
using VGAudio.Formats.GcAdpcm;
using VGAudio.Formats.Pcm16;

namespace PartyPatcher.Formats
{
    public class PdtFile
    {
        const int STREAM_STEREO_FLAG = 1;
        const int STREAM_LOOP_FLAG = 2;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MsmHeader
        {
            public ushort Version;
            public ushort StreamMax;
            public int ChannelMax;
            public int SampleFrq;
            public int MaxBufs;
            public uint StreamPackListOfs;
            public uint AdpcmParamOfs;
            public uint StreamPackOfs;
            public uint SampleOfs;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MsmStreamPack
        {
            public byte flag;
            public byte vol;
            public byte pan;
            public byte span;

            public byte auxA;
            public byte auxB;
            public ushort SampleRate;

            public uint loopOfsEnd;
            public uint loopOfsStart;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class MsmStream
        {
            public int sampleOfs;
            public short adpcmParamIdx;
            public ushort pad;
        }

        public class StreamEntry
        {
            public MsmStreamPack Info = new MsmStreamPack();

            public StreamChannel[] Channels = new StreamChannel[2];
        }

        public struct StreamChannel
        {
            public short[] Coefs;
            public byte[] Adpcm;
        }

        public StreamEntry[] StreamEntries = new StreamEntry[0];

        private MsmHeader _header;

        public PdtFile(Stream stream)
        {
            using (var reader = new FileReader(stream))
                Read(reader);
        }
        public PdtFile(string filePath)
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

        public void Import(int streamIdx, string filePath)
        {
            WaveReader wave = new WaveReader();
            FromAudioFormat(streamIdx, wave.ReadFormat(File.OpenRead(filePath)));
        }

        public void Export(int streamIdx, string filePath)
        {
            WaveWriter wave = new WaveWriter();
            wave.WriteToStream(ToAudioFormat(streamIdx), new FileStream(filePath, FileMode.Create, FileAccess.Write));
        }

        public void AddStream(StreamEntry entry)
        {
            var list = StreamEntries.ToList();
            list.Add(entry);
            StreamEntries = list.ToArray();
        }

        private void FromAudioFormat(int streamIdx, IAudioFormat audioFormat, byte volume = 108)
        {
            var slot = StreamEntries[streamIdx];
            var format = new GcAdpcmFormat().EncodeFromPcm16(audioFormat.ToPcm16());
            slot.Info.loopOfsStart = (uint)format.LoopStart;
            slot.Info.SampleRate = (ushort)format.SampleRate;
            slot.Info.auxA = 0;
            slot.Info.auxB = 0;
            slot.Info.vol = volume;
            slot.Info.pan = 64;
            slot.Info.flag = 0;
            if (format.ChannelCount == 2)
                slot.Info.flag |= STREAM_STEREO_FLAG;
            slot.Info.flag |= STREAM_LOOP_FLAG;

            for (int i = 0; i < format.ChannelCount; i++)
            {
                slot.Channels[i] = new StreamChannel()
                {
                    Coefs = format.Channels[i].Coefs,
                    Adpcm = format.Channels[i].Adpcm,
                };
            }
            // Mono
            if (format.ChannelCount == 1) 
                slot.Channels[1] = new StreamChannel(); 
            // Matches total sample byte size
            slot.Info.loopOfsEnd = (uint)(slot.Channels.Sum(x => x.Adpcm.Length));
        }

        private IAudioFormat ToAudioFormat(int streamIdx)
        {
            var slot = StreamEntries[streamIdx];

            GcAdpcmChannel[] channels = new GcAdpcmChannel[this._header.ChannelMax];
            for (int i = 0; i < channels.Length; i++)
            { 
                var sampleCount = (slot.Channels[i].Adpcm.Length / 8) * 14;
                channels[i] = new GcAdpcmChannel(

                    slot.Channels[i].Adpcm,
                    slot.Channels[i].Coefs,
                    (int)sampleCount);
            }
            return new GcAdpcmFormat(channels, (int)slot.Info.SampleRate);
        }

        private void Read(FileReader reader)
        {
            reader.SetByteOrder(true);
            _header = reader.ReadStruct<MsmHeader>();
            reader.SeekBegin(_header.StreamPackListOfs);
            uint[] slots = reader.ReadUInt32s(_header.StreamMax);

            StreamEntries = new StreamEntry[_header.StreamMax];
            for (uint i = 0; i < slots.Length; i++)
            {
                if (slots[i] == 0)
                    continue;

                reader.SeekBegin(slots[i]);
                StreamEntries[i] = new StreamEntry()
                {
                    Info = reader.ReadStruct<MsmStreamPack>(),
                };
                var stream1 = reader.ReadStruct<MsmStream>();
                var stream2 = reader.ReadStruct<MsmStream>();

                reader.SeekBegin(_header.AdpcmParamOfs + stream1.adpcmParamIdx * 32);
                var adpcmParam0 = reader.ReadInt16s(16);

                reader.SeekBegin(_header.AdpcmParamOfs + stream2.adpcmParamIdx * 32);
                var adpcmParam1 = reader.ReadInt16s(16);

                var sampler1Size = stream2.sampleOfs - stream1.sampleOfs;
                var sampler2Size = StreamEntries[i].Info.loopOfsEnd - sampler1Size;

                reader.SeekBegin(stream1.sampleOfs);
                var channel0Samples = reader.ReadBytes((int)sampler1Size);
                var channel1Samples = reader.ReadBytes((int)sampler1Size);

                StreamEntries[i].Channels[0] = new StreamChannel()
                {
                    Coefs = adpcmParam0,
                    Adpcm = channel0Samples,
                }; 
                StreamEntries[i].Channels[1] = new StreamChannel()
                {
                    Coefs = adpcmParam1,
                    Adpcm = channel1Samples,
                };
            }
        }

        private void Write(FileWriter writer)
        {
            _header.StreamMax = (ushort)this.StreamEntries.Length;

            writer.SetByteOrder(true);
            writer.WriteStruct(_header);

            var pos = writer.Position;
            writer.WriteUint32Offset(16); // streamPackListOfs
            writer.Write(new uint[this.StreamEntries.Length]);
            writer.Align(16);

            var stream_headers = new List<MsmStream>();

            // AdpcmParams
            writer.WriteUint32Offset(20); // adpcmParamOfs
            for (uint i = 0; i < this.StreamEntries.Length; i++)
            {
                if (this.StreamEntries[i] == null)
                    continue;

                // 2 channels
                if ((this.StreamEntries[i].Info.flag & STREAM_STEREO_FLAG) != 0)
                {
                    writer.Write(StreamEntries[i].Channels[0].Coefs);
                    writer.Write(StreamEntries[i].Channels[1].Coefs);

                    stream_headers.Add(new MsmStream() { adpcmParamIdx = (short)stream_headers.Count });
                    stream_headers.Add(new MsmStream() { adpcmParamIdx = (short)stream_headers.Count });
                }
                else
                {
                    writer.Write(StreamEntries[i].Channels[0].Coefs);
                    var coefIdx = stream_headers.Count;
                    stream_headers.Add(new MsmStream() { adpcmParamIdx = (short)coefIdx });
                    stream_headers.Add(new MsmStream() { adpcmParamIdx = (short)coefIdx });
                }
            }

            writer.WriteUint32Offset(24); // streamPack
            var packStart = writer.Position;
            int idx = 0;
            for (uint i = 0; i < this.StreamEntries.Length; i++)
            {
                if (this.StreamEntries[i] == null)
                    continue;

                writer.WriteUint32Offset(pos + i * 4);
                var stream1 = stream_headers[idx * 2 + 0];
                var stream2 = stream_headers[idx * 2 + 1];

                writer.WriteStruct(this.StreamEntries[i].Info);
                writer.WriteStruct(stream1);
                writer.WriteStruct(stream2);
                idx++;
            }

            idx = 0;
            writer.Align(32);
            writer.WriteUint32Offset(28); // sampleData
            for (uint i = 0; i < this.StreamEntries.Length; i++)
            {
                if (this.StreamEntries[i] == null)
                    continue;

                // 2 channels
                if ((this.StreamEntries[i].Info.flag & STREAM_STEREO_FLAG) != 0)
                {
                    writer.Align(8);
                    writer.WriteUint32Offset(packStart + idx * 32 + 16);
                    writer.Write(this.StreamEntries[i].Channels[0].Adpcm);

                    writer.Align(8);
                    writer.WriteUint32Offset(packStart + idx * 32 + 16 + 8);
                    writer.Write(this.StreamEntries[i].Channels[1].Adpcm);
                }
                else // Mono
                {
                    writer.Align(8);
                    writer.WriteUint32Offset(packStart + idx * 32 + 16);
                    writer.WriteUint32Offset(packStart + idx * 32 + 16 + 8);
                    writer.Write(this.StreamEntries[i].Channels[0].Adpcm);
                }
                idx++;
            }
        }
    }
}

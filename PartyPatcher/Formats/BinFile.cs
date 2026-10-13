using AvaloniaToolbox.Core;
using AvaloniaToolbox.Core.Compression;
using AvaloniaToolbox.Core.IO;
using PartyPatcher.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartyPatcher.Formats
{
    public class BinFile
    {
        public List<FileEntry> Files = new List<FileEntry>();

        private string _fileName;

        public static bool Identify(string ext, System.IO.Stream stream)
        {
            if (stream.Length < 16)
                return false;

            if (ext == ".bin" || ext == ".dat")
            {
                using (var reader = new FileReader(stream, true))
                {
                    reader.SetByteOrder(true);
                    uint count = reader.ReadUInt32();
                    uint offset = reader.ReadUInt32();
                    if (offset == 4 + (4 * count) || offset == 8 + (4 * count))
                        return true;
                }
            }
            return false;
        }

        public enum CompressionType
        {
            None = 0,
            LZSS = 1,
            SLIDE = 2,
            FSLIDE_Alt = 3,
            FLIDE = 4,
            RLE = 5,
            INFLATE = 7,
        }

        public BinFile() { }

        public BinFile(string FileName)
        {
            using (var stream = new System.IO.FileStream(FileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                Read(FileName, stream);
            }
        }
        public BinFile(Stream stream, string fileName)
        {
            Read(fileName, stream);
        }

        public void Save(string filePath, IProgress progress = null)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                Save(fs, progress);
            }
        }

        void Read(string FileName, System.IO.Stream stream)
        {
            _fileName = FileName;
            using (var reader = new FileReader(stream))
            {
                reader.SetByteOrder(true);
                uint numFiles = reader.ReadUInt32();
                uint[] offsets = reader.ReadUInt32s((int)numFiles);

                for (int i = 0; i < numFiles; i++)
                {
                    reader.SeekBegin(offsets[i]);
                    uint compSize = 0;
                    if (i < numFiles - 1)
                        compSize = offsets[i + 1] - offsets[i] - 8;
                    else
                        compSize = (uint)reader.BaseStream.Length - offsets[i] - 8;

                    var file = new FileEntry();
                    file.DecompressedSize = reader.ReadUInt32();
                    file.CompressionType = (CompressionType)reader.ReadUInt32();
                    Files.Add(file);

                    byte[] data = reader.ReadBytes((int)compSize);
                    file.FileData = new MemoryStream(data);
                }
                UpdateFileNames(FileName);
            }
        }

        private void UpdateFileNames(string FileName)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(FileName);
            for (int i = 0; i < Files.Count; i++)
            {
                string comp = "";
                switch (Files[i].CompressionType)
                {
                    case CompressionType.LZSS: comp = ".lz"; break;
                    case CompressionType.SLIDE: comp = ".s"; break;
                    case CompressionType.FLIDE: comp = ".fs"; break;
                    case CompressionType.FSLIDE_Alt: comp = ".fsa"; break;
                    case CompressionType.INFLATE: comp = ".z"; break;
                }
                Files[i].FileName = $"{name}{string.Format("{0:00}", i)}{comp}";

                var decomp = Files[i].DecompressData(Files[i].FileData);
                using (var fileReader = new FileReader(decomp))
                {
                    fileReader.SetByteOrder(true);
                    string magic = fileReader.ReadString(4, Encoding.ASCII);
                    if (magic == "HSFV")
                    {
                        fileReader.SeekBegin(12);
                        bool hasFog = fileReader.ReadUInt32() > 0;

                        fileReader.SeekBegin(44);
                        bool hasMesh = fileReader.ReadUInt32() > 0;

                        fileReader.SeekBegin(76);
                        bool hasObjects = fileReader.ReadUInt32() > 0;

                        fileReader.SeekBegin(84);
                        bool hasTexture = fileReader.ReadUInt32() > 0;

                        fileReader.SeekBegin(180);
                        bool hasMotion = fileReader.ReadUInt32() > 0;

                        if (hasMesh)
                            Files[i].FileName = $"MESH_{Files[i].FileName}.hsf";
                        else if (hasMotion)
                            Files[i].FileName = $"MOTION_{Files[i].FileName}.hsf";
                        else if (hasObjects)
                            Files[i].FileName = $"OBJ_{Files[i].FileName}.hsf";
                        else if (hasTexture)
                            Files[i].FileName = $"TEX_{Files[i].FileName}.hsf";
                        else if (hasFog)
                            Files[i].FileName = $"FOG_{Files[i].FileName}.hsf";
                        else
                            Files[i].FileName = $"{Files[i].FileName}.hsf";

                        Files[i].ImageKey = "model";
                    }
                    else if (fileReader.BaseStream.Length > 16)
                    {
                        fileReader.SeekBegin(12);
                        uint offset = fileReader.ReadUInt32();
                        if (offset == 20)
                            Files[i].FileName = $"{Files[i].FileName}.atb";
                        else
                            Files[i].FileName = $"{Files[i].FileName}.dat";
                    }
                    else
                        Files[i].FileName = $"{Files[i].FileName}.dat";
                }
            }
        }

        public void Save(System.IO.Stream stream, IProgress progress = null)
        {
            using (var writer = new FileWriter(stream))
            {
                writer.SetByteOrder(true);
                writer.Write(Files.Count);
                writer.Write(new uint[Files.Count]); //reserve space for offsets
                for (int i = 0; i < Files.Count; i++)
                {
                    this.Files[i].CompressionType = CompressionType.LZSS;
                    progress?.SetProgress(((float)i / (Files.Count - 1)) * 100,
                        $"Compressing {i} / {Files.Count}");

                    uint uncomp_size = (uint)Files[i].FileData.Length;

                    writer.WriteUint32Offset(4 + (i * 4));
                    writer.Write(uncomp_size);
                    writer.Write((uint)Files[i].CompressionType);
                    writer.Write(Files[i].FileData.ToArray());
                }
            }
        }

        public bool AddFile(ArchiveFileInfo archiveFileInfo)
        {
            CompressionType type = CompressionType.None;
            if (archiveFileInfo.FileName.Contains(".lz"))
                type = CompressionType.LZSS;
            if (archiveFileInfo.FileName.Contains(".s"))
                type = CompressionType.SLIDE;
            if (archiveFileInfo.FileName.Contains(".fs"))
                type = CompressionType.FLIDE;
            if (archiveFileInfo.FileName.Contains(".fsa"))
                type = CompressionType.FSLIDE_Alt;
            if (archiveFileInfo.FileName.Contains(".z"))
                type = CompressionType.INFLATE;

            Files.Add(new FileEntry()
            {
                FileData = archiveFileInfo.FileData,
                CompressionType = type,
            });
            UpdateFileNames(_fileName);
            return false;
        }

        public bool DeleteFile(ArchiveFileInfo archiveFileInfo)
        {
            Files.Remove((FileEntry)archiveFileInfo);
            return true;
        }

        public class FileEntry : ArchiveFileInfo
        {
            public string ImageKey { get; set; }

            public CompressionType CompressionType { get; set; }

            public uint DecompressedSize;

            public override Stream DecompressData(Stream compressed)
            {
                byte[] data = compressed.ToArray();
                switch (CompressionType)
                {
                    case CompressionType.LZSS:
                        data = DecompressLZSS(data, (int)DecompressedSize);
                        break;
                    case CompressionType.SLIDE:
                    case CompressionType.FLIDE:
                    case CompressionType.FSLIDE_Alt:
                        data = DecompressSlide(data, (int)DecompressedSize);
                        break;
                    case CompressionType.RLE:
                        data = DecompressRLE(data, (int)DecompressedSize);
                        break;
                    case CompressionType.INFLATE:
                        data = ZLIB.Decompress(ByteUtil.SubArray(data, 8));
                        break;
                    default:
                        break;
                }
                return new MemoryStream(data);
            }

            public override Stream CompressData(Stream decompressed)
            {
                var uncomp_size = (uint)decompressed.Length;
                byte[] savedBytes = decompressed.ToArray();
                switch (this.CompressionType)
                {
                    case CompressionType.LZSS:
                        byte[] dst = new byte[savedBytes.Length * 2];
                        var length = LZSS_c.Encode(dst, savedBytes, savedBytes.Length);

                        byte[] final = new byte[length];
                        Array.Copy(dst, final, final.Length);

                        savedBytes = final;
                        break;
                    case CompressionType.INFLATE:
                        {
                            var compressed = ZLIB.Compress(savedBytes);

                            List<byte> output = new List<byte>();
                            output.AddRange(WriteBigEndian(uncomp_size));
                            output.AddRange(WriteBigEndian((uint)compressed.Length));
                            output.AddRange(compressed);
                            savedBytes = output.ToArray();
                            output.Clear();
                        }
                        break;
                    case CompressionType.SLIDE:
                    case CompressionType.FSLIDE_Alt:
                    case CompressionType.FLIDE:
                        {
                            List<byte> output = new List<byte>();
                            output.AddRange(WriteBigEndian(uncomp_size));
                            output.AddRange(CompressSlide(savedBytes, uncomp_size));
                            savedBytes = output.ToArray();
                            output.Clear();
                        }
                        break;
                    default:
                        throw new Exception($"Compression not supported! {this.CompressionType}");
                }
                return new MemoryStream(savedBytes);
            }
        }

        //From https://github.com/Sage-of-Mirrors/HoneyBee/blob/master/HoneyBee/src/archive/Compression.cs#L78
        private static byte[] DecompressLZSS(byte[] compressed_data, int uncompressed_size)
        {
            int WINDOW_SIZE = 1024;
            int WINDOW_START = 0x3BE;
            int MIN_MATCH_LEN = 3;

            int src_offset = 0;
            int dest_offset = 0;
            int window_offset = WINDOW_START;

            byte[] dest = new byte[uncompressed_size];
            byte[] window_buffer = new byte[WINDOW_SIZE];

            ushort cur_code_byte = 0;

            while (dest_offset < uncompressed_size)
            {
                if ((cur_code_byte & 0x100) == 0)
                {
                    cur_code_byte = compressed_data[src_offset++];
                    cur_code_byte |= 0xFF00;
                }

                if ((cur_code_byte & 0x001) == 1)
                {
                    dest[dest_offset] = compressed_data[src_offset];
                    window_buffer[window_offset] = compressed_data[src_offset];

                    src_offset++;
                    dest_offset++;

                    window_offset = (window_offset + 1) % WINDOW_SIZE;
                }

                else
                {
                    byte byte1 = compressed_data[src_offset++];
                    byte byte2 = compressed_data[src_offset++];

                    int offset = ((byte2 & 0xC0) << 2) | byte1;
                    int length = (byte2 & 0x3F) + MIN_MATCH_LEN;

                    byte val = 0;
                    for (int i = 0; i < length; i++)
                    {
                        val = window_buffer[offset % WINDOW_SIZE];
                        window_buffer[window_offset] = val;

                        window_offset = (window_offset + 1) % WINDOW_SIZE;
                        dest[dest_offset] = val;

                        dest_offset++;
                        offset++;
                    }
                }

                cur_code_byte >>= 1;
            }


            return dest;
        }

        private static byte[] DecompressRLE(byte[] compressed_data, int uncompressed_size)
        {
            int dest_offset = 0;
            int src_offset = 0;
            int code_byte = 0;
            byte repeat_length;
            int i;

            byte[] dest = new byte[uncompressed_size];
            while (dest_offset < uncompressed_size)
            {
                code_byte = compressed_data[src_offset];
                src_offset++;
                repeat_length = (byte)(code_byte & 0x7F);

                if ((code_byte & 0x80) != 0)
                {
                    i = 0;
                    while (i < repeat_length)
                    {
                        dest[dest_offset] = compressed_data[src_offset];
                        dest_offset++;
                        src_offset++;
                        i++;
                    }
                }
                else
                {
                    byte repeated_byte = compressed_data[src_offset];
                    src_offset++;

                    i = 0;
                    while (i < repeat_length)
                    {
                        dest[dest_offset] = repeated_byte;
                        dest_offset++;
                        i++;
                    }
                }
            }
            return dest;
        }

        //From https://github.com/gamemasterplc/mpbintools/blob/master/bindump.c#L240
        private static byte[] DecompressSlide(byte[] compressed_data, int uncompressed_size)
        {
            int src_offset = 4;
            int dest_offset = 0;
            int code_word = 0;
            int num_code_word_bits_left = 0;
            int i = 0;

            byte[] dest = new byte[uncompressed_size];

            while (dest_offset < uncompressed_size)
            {
                if (num_code_word_bits_left == 0)
                {
                    code_word = ReadBigEndian(compressed_data, src_offset);
                    src_offset += 4;
                    num_code_word_bits_left = 32;
                }

                if ((code_word & 0x80000000) != 0)
                {
                    dest[dest_offset] = compressed_data[src_offset];
                    src_offset++;
                    dest_offset++;
                }
                else
                {
                    //Interpret Next 2 Bytes as a Backwards Distance and Length
                    byte byte1 = compressed_data[src_offset++];
                    byte byte2 = compressed_data[src_offset++];

                    int dist_back = (((byte1 & 0x0F) << 8) | byte2) + 1;
                    int copy_length = ((byte1 & 0xF0) >> 4) + 2;

                    //Special Case Where the Upper 4 Bits of byte1 are 0
                    if (copy_length == 2)
                    {
                        copy_length = compressed_data[src_offset++] + 18;
                    }

                    byte value;
                    i = 0;

                    while (i < copy_length && dest_offset < uncompressed_size)
                    {
                        if (dist_back > dest_offset)
                        {
                            value = 0;
                        }
                        else
                        {
                            value = dest[dest_offset - dist_back];
                        }
                        dest[dest_offset] = value;
                        dest_offset++;
                        i++;
                    }
                }
                code_word = code_word << 1;
                num_code_word_bits_left--;
            }

            return dest;
        }

        private static int ReadBigEndian(byte[] data, int offset)
        {
            int value = data[offset + 0] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3];
            return value;
        }

        private static byte[] CompressSlide(byte[] input, uint uncomp_len)
        {
            Ret r = new Ret();

            int dstSize = 0;
            int percent = -1;
            byte[] dst = new byte[96];// 8 codes * 3 bytes maximum

            uint validBitCount = 0; //number of valid bits left in "code" byte
            uint currCodeByte = 0;
            int offset = 0;

            List<byte> output = new List<byte>();

            var mem = new System.IO.MemoryStream();

            while (r.srcPos < uncomp_len)
            {
                uint numBytes;
                uint matchPos = 0;
                uint srcPosBak;

                numBytes = nintendoEnc(input, (int)uncomp_len, r.srcPos, ref matchPos);
                if (numBytes < 3)
                {
                    //straight copy
                    dst[r.dstPos] = input[r.srcPos];
                    output.Add(dst[r.dstPos]);
                    r.dstPos++;
                    r.srcPos++;
                    //set flag for straight copy
                    currCodeByte |= (0x80000000 >> (int)validBitCount);
                }
                else
                {
                    //RLE part
                    uint dist = (uint)(r.srcPos - matchPos - 1);
                    byte byte1, byte2, byte3;

                    if (numBytes >= 0x12)  // 3 byte encoding
                    {
                        byte1 = (byte)(0 | (dist >> 8));
                        byte2 = (byte)(dist & 0xff);
                        dst[r.dstPos++] = byte1;
                        dst[r.dstPos++] = byte2;
                        // maximum runlength for 3 byte encoding
                        if (numBytes > 0xff + 0x12)
                            numBytes = 0xff + 0x12;
                        byte3 = (byte)(numBytes - 0x12);
                        dst[r.dstPos++] = byte3;
                    }
                    else  // 2 byte encoding
                    {
                        byte1 = (byte)(((numBytes - 2) << 4) | (dist >> 8));
                        byte2 = (byte)(dist & 0xff);
                        dst[r.dstPos++] = byte1;
                        dst[r.dstPos++] = byte2;
                    }
                    r.srcPos += (int)numBytes;
                }
                validBitCount++;
                //write eight codes
                if (validBitCount == 32)
                {
                    WriteBigEndian(mem, currCodeByte, offset);
                    WriteFileArray(mem, dst, offset + 4, r.dstPos);

                    dstSize += r.dstPos + 4;
                    offset += r.dstPos + 4;

                    srcPosBak = (uint)r.srcPos;
                    currCodeByte = 0;
                    validBitCount = 0;
                    r.dstPos = 0;
                }
            }
            if (validBitCount > 0)
            {
                WriteBigEndian(mem, currCodeByte, offset);
                WriteFileArray(mem, dst, offset + 4, r.dstPos);

                dstSize += r.dstPos + 4;
                offset += r.dstPos + 4;

                currCodeByte = 0;
                validBitCount = 0;
                r.dstPos = 0;
            }
            return mem.ToArray();
        }

        private static void WriteFileArray(System.IO.MemoryStream mem, byte[] data, int offset, int len)
        {
            using (var writer = new FileWriter(mem, true))
            {
                writer.SetByteOrder(true);
                writer.SeekBegin(offset);
                for (int i = 0; i < len; i++)
                    writer.Write(data[i]);
            }
        }

        private static void WriteBigEndian(System.IO.MemoryStream mem, uint data, int offset)
        {
            using (var writer = new FileWriter(mem, true))
            {
                writer.SetByteOrder(true);
                writer.SeekBegin(offset);
                writer.Write(data);
            }
        }

        private static byte[] WriteBigEndian(uint value)
        {
            uint bigEndian = (uint)System.Net.IPAddress.HostToNetworkOrder((int)value);
            return BitConverter.GetBytes(bigEndian);
        }

        class Ret
        {
            public int srcPos;
            public int dstPos;
        }

        static uint numBytes1 = 0;
        static uint matchPos = 0;
        static int prevFlag = 0;
        private static uint nintendoEnc(byte[] src, int size, int pos, ref uint pMatchPos)
        {
            int startPos = pos - 0x1000;
            uint numBytes = 1;

            // if prevFlag is set, it means that the previous position was determined by look-ahead try.
            // so just use it. this is not the best optimization, but nintendo's choice for speed.
            if (prevFlag == 1)
            {
                pMatchPos = matchPos;
                prevFlag = 0;
                return numBytes1;
            }
            prevFlag = 0;
            numBytes = simpleEnc(src, size, pos, ref matchPos);
            pMatchPos = matchPos;

            // if this position is RLE encoded, then compare to copying 1 byte and next position(pos+1) encoding
            if (numBytes >= 3)
            {
                numBytes1 = simpleEnc(src, size, pos + 1, ref matchPos);
                // if the next position encoding is +2 longer than current position, choose it.
                // this does not guarantee the best optimization, but fairly good optimization with speed.
                if (numBytes1 >= numBytes + 2)
                {
                    numBytes = 1;
                    prevFlag = 1;
                }
            }
            return numBytes;
        }

        private static uint simpleEnc(byte[] src, int size, int pos, ref uint pMatchPos)
        {
            int startPos = pos - 0x1000;
            uint numBytes = 1;
            uint matchPos = 0;
            int i = 0;
            int j = 0;

            if (startPos < 0)
                startPos = 0;
            for (i = startPos; i < pos; i++)
            {
                for (j = 0; j < size - pos; j++)
                {
                    if (src[i + j] != src[j + pos])
                        break;
                }
                if (j > numBytes)
                {
                    numBytes = (uint)j;
                    matchPos = (uint)i;
                }
            }

            pMatchPos = matchPos;
            if (numBytes == 2)
                numBytes = 1;
            return numBytes;
        }
    }
}

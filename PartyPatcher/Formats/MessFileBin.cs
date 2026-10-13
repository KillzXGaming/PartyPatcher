    using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PartyPatcher.Formats
{
    public class MessFileBin : IMessFile
    {
        const uint START_POS = 4;

        public List<MessageData> MessageFiles = new List<MessageData>();

        public MessFileBin() { }

        public MessFileBin(string fileName, Encoding encoding, uint version = 4) {
            Read(new FileReader(fileName), encoding, version);
        }

        public MessFileBin(System.IO.Stream stream, Encoding encoding, uint version = 4) {
            Read(new FileReader(stream), encoding, version);
        }

        public void Replace(int groupIdx, int messageIdx, string message)
        {
            Console.WriteLine($"Bin messages are not patchable yet!");
        }
        public void AddMessage(int groupIdx, string message)
        {
            Console.WriteLine($"Bin messages are not patchable yet!");
        }
        public int AddGroup()
        {
            var idx = this.MessageFiles.Count;
            this.MessageFiles.Add(new MessageData());
            return idx;
        }

        public static string Export(MessFileBin message)
        {
            return JsonSerializer.Serialize(message, new JsonSerializerOptions()
            {
                IncludeFields = true,
                WriteIndented = true,
            });
        }

        public static MessFileBin Import(string fileName) {
            return JsonSerializer.Deserialize<MessFileBin>(System.IO.File.ReadAllText(fileName));
        }

        public void Save(string fileName, uint version = 4, Encoding encoding = null)
        {
            using (var  writer = new FileWriter(fileName)) {
                Write(writer, encoding == null ? Encoding.UTF8 : encoding, version);
            }
        }

        public void Save(System.IO.Stream stream, uint version = 4, Encoding encoding = null)
        {
            using (var writer = new FileWriter(stream)) {
                Write(writer, encoding == null ? Encoding.UTF8 : encoding, version);
            }
        }

        void Read(FileReader reader, Encoding encoding, uint version = 4)
        {
            reader.SetByteOrder(true);
            uint numFiles = reader.ReadUInt32();
            for (int i = 0; i < numFiles; i++)
            {
                reader.SeekBegin(4 + (i * 4));
                uint offset = reader.ReadUInt32() + START_POS;
                uint nextOffset = reader.ReadUInt32() + START_POS;
                if (i == numFiles - 1)
                    nextOffset = (uint)reader.BaseStream.Length;
                    
                uint size = nextOffset - offset;

                reader.SeekBegin(offset);
                byte[] data = reader.ReadBytes((int)size);
                MessageFiles.Add(ReadMessageData(data, encoding, version));
            }
        }

        void Write(FileWriter writer, Encoding encoding, uint version)
        {
            var files = MessageFiles.ToList();

            writer.SetByteOrder(true);
            writer.Write(files.Count);
            writer.Write(new uint[files.Count]);
            for (int i = 0; i < files.Count; i++)
            {
                writer.WriteUint32Offset(4 + (i * 4), START_POS);
                WriteMessageData(writer, files[i], encoding, version);
            }
        }

        private void WriteMessageData(FileWriter writer, MessageData messData, Encoding encoding, uint version)
        {
            if (messData.Entries.Count == 0)
                writer.Write(messData.Data);
            else
            {
                writer.Write(messData.Entries.Count);

                long startPos = writer.Position;
                writer.Write(new uint[messData.Entries.Count]);
                for (int i = 0; i < messData.Entries.Count; i++)
                {
                    var bytes = MessFileDat.GetMessageStringBytes(messData.Entries[i].Item2);

                    writer.WriteUint32Offset(startPos + (i * 4), startPos);
                    writer.Write(messData.Entries[i].Item1); // ID
                    writer.Write(bytes);
                    writer.Write((byte)0);
                    writer.Align(4);
                }
            }
        }

        private MessageData ReadMessageData(byte[] data, Encoding encoding, uint version = 4)
        {
            MessageData messageData = new MessageData();
            messageData.Data = data;
            using (var reader = new FileReader(data))
            {
                reader.SetByteOrder(true);
                uint numValues = reader.ReadUInt32();
                uint firstOffset = reader.ReadUInt32();

                if (firstOffset != (numValues * 4))
                    return messageData;

                //Message files 
                for (int i = 0; i < numValues; i++)
                {
                    reader.SeekBegin(4 + (i * 4));
                    uint offset = reader.ReadUInt32() + START_POS;

                    reader.SeekBegin(offset);
                    uint id = reader.ReadUInt32();
                    string text = MessFileDat.GetMessageString(MessFileDat.ReadString(reader), encoding);
                    messageData.Entries.Add(Tuple.Create(id, text));
                }
            }
            messageData.Data = new byte[0];
            return messageData;
        }

        public class MessageData
        {
            public List<Tuple<uint, string>> Entries = new();

            public byte[] Data;
        }
    }
}

using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PartyPatcher.Formats
{
    public class MessFile
    {
        public uint Version = 4;

        public List<MessageData> MessageFiles = new List<MessageData>();

        public MessFile() { }

        public MessFile(string fileName, Encoding encoding, uint version = 4) {
            Read(new FileReader(fileName), encoding, version);
        }

        public MessFile(System.IO.Stream stream, Encoding encoding, uint version = 4) {
            Read(new FileReader(stream), encoding, version);
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

        public static string Export(MessFile message) {
            return JsonSerializer.Serialize(message, new JsonSerializerOptions()
            {
                WriteIndented = true,
            });
        }

        public static MessFile Import(string fileName) {
            return JsonSerializer.Deserialize<MessFile>(System.IO.File.ReadAllText(fileName));
        }

        void Read(FileReader reader, Encoding encoding, uint version = 4)
        {
            reader.SetByteOrder(true);
            uint numFiles = reader.ReadUInt32();

            Version = version;
            for (int i = 0; i < numFiles; i++)
            {
                reader.SeekBegin(4 + (i * 4));
                uint offset = reader.ReadUInt32();
                uint nextOffset = reader.ReadUInt32();
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
            long sectionSizePos = writer.Position;

            writer.Write(uint.MaxValue);
            for (int i = 0; i < files.Count; i++)
            {
                writer.WriteUint32Offset(4 + (i * 4));
                WriteMessageData(writer, files[i], encoding, version);
            }
            writer.WriteSectionSizeU32(sectionSizePos, writer.BaseStream.Length);
        }

        private void WriteMessageData(FileWriter writer, MessageData messData, Encoding encoding, uint version)
        {
            if (messData.Entries.Count == 0)
                writer.Write(messData.Data);
            else
            {
                long startPos = writer.Position;
                writer.Write(messData.Entries.Count);
                writer.Write(new uint[messData.Entries.Count]);
                long sectionSizePos = writer.Position;
                writer.Write(uint.MaxValue);
                for (int i = 0; i < messData.Entries.Count; i++)
                {
                    var bytes = GetMessageStringBytes(messData.Entries[i]);

                    writer.WriteUint32Offset(startPos + 4 + (i * 4), startPos);
                    writer.Write((byte)0xB);
                    writer.Write(bytes);
                    writer.Write((byte)0);
                    writer.Align(4);
                }
                writer.WriteSectionSizeU32(sectionSizePos, writer.Position - startPos);
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

                if (firstOffset != 8 + (numValues * 4))
                    return messageData;

                //Message files 
                for (int i = 0; i < numValues; i++)
                {
                    reader.SeekBegin(4 + (i * 4));
                    uint offset = reader.ReadUInt32();

                    reader.SeekBegin(offset);
                    Trace.Assert(reader.ReadByte() == 0xb); //0xb at start of every string

                    messageData.Entries.Add(GetMessageString(ReadString(reader, encoding)));
                }
            }
            messageData.Data = new byte[0];
            return messageData;
        }

        private static byte[] ReadString(FileReader reader, Encoding encoding)
        {
            List<byte> values = new List<byte>();
            while (!reader.EndOfStream)
            {
                byte cha = reader.ReadByte();
                if (cha == 0)
                    break;
                values.Add(cha);
            }
            return values.ToArray();
        }


        private static void WriteString(FileWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                writer.Write((byte)0);
                return;
            }
            writer.Write(Encoding.UTF8.GetBytes(text));
            writer.Write((byte)0);
        }

        static bool colorTagActive = false;
        static bool startQuoteActive = false;

        static string GetMessageString(byte[] values)
        {
            List<char> text = new List<char>();
            for (int i = 0; i < values.Length; i++)
            {
                var val = values[i];

                if (CharacterTable.ContainsKey(val))
                    text.Add(CharacterTable[val]);
                else if (val == 0x0C)
                {
                    int count = 1;
                    while (values[i + 1] == 0x0C)
                    {
                        count++;
                        i++;
                    }
                    text.AddRange($"[Align_{count}]");
                }
                else if (val == 0x0D) text.AddRange("[Select2]");
                else if (val == 0x0F) text.AddRange("[Select]");
                else if (val == 0x1C)
                {
                    text.AddRange($"[Voice:{(DialogCodes)values[i + 1]}]");
                    i++;
                }
                else if (val == 0x1E)
                {
                    //End of color tag
                    if (values[i + 1] == (byte)ColorCodes.END)
                        text.AddRange($"[COLOR_END]");
                    else
                        text.AddRange($"[COLOR_START:({(ColorCodes)values[i + 1]})]");

                    i++;
                }
                else if (val == 0x1F)
                {
                    text.AddRange($"[INSERT:{(RuntimeCodes)values[i + 1]}]");
                    i++;
                }
                else if (val == 0x0E)
                {
                    text.AddRange($"[ICON:{(IconCodes)values[i + 1]}]");
                    i++;
                }
                else if (val == 0xFF)
                {
                    text.AddRange($"[WAIT]");
                }
                else
                    text.Add((char)val);
            }
            return new string(text.ToArray());
        }

        static byte[] GetMessageStringBytes(string text)
        {
            List<byte> values = new List<byte>();
            int i = 0;

            while (i < text.Length)
            {
                char cha = text[i];
                if (cha == '[')
                {
                    int end = text.IndexOf(']', i);
                    if (end == -1)
                        throw new FormatException($"Unclosed tag starting at position {i}");

                    string token = text.Substring(i + 1, end - i - 1);
                    var mAlign = Regex.Match(token, @"^Align_(\d+)$");
                    if (mAlign.Success)
                    {
                        int count = int.Parse(mAlign.Groups[1].Value, CultureInfo.InvariantCulture);
                        values.AddRange(Enumerable.Repeat((byte)0x0C, count));
                        i = end + 1;
                        continue;
                    }

                    if (token.Equals("Select", StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(0x0F);
                        i = end + 1;
                        continue;
                    }
                    if (token.Equals("Select2", StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(0x0D);
                        i = end + 1;
                        continue;
                    }
                    if (token.Equals("COLOR_END", StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(0x1E);
                        values.Add((byte)ColorCodes.END);
                        i = end + 1;
                        continue;
                    }
                    if (token.Equals("WAIT", StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(0xFF);
                        i = end + 1;
                        continue;
                    }

                    var mDialog = Regex.Match(token, @"^Voice:(.+)$");
                    if (mDialog.Success)
                    {
                        string enumName = mDialog.Groups[1].Value;
                        if (!Enum.TryParse<DialogCodes>(enumName, out var dlg))
                            throw new FormatException($"Unknown DialogCodes value '{enumName}' at position {i}");

                        values.Add(0x1C);
                        values.Add((byte)dlg);
                        i = end + 1;
                        continue;
                    }

                    if (token == string.Empty)
                    {
                        values.Add(0x1E);
                        values.Add((byte)ColorCodes.END);
                        i = end + 1;
                        continue;
                    }

                    var mColor = Regex.Match(token, @"^COLOR_START:\((.+)\)$");
                    if (mColor.Success)
                    {
                        string enumName = mColor.Groups[1].Value;
                        if (!Enum.TryParse<ColorCodes>(enumName, out var col))
                            throw new FormatException($"Unknown ColorCodes value '{enumName}' at position {i}");

                        values.Add(0x1E);
                        values.Add((byte)col);
                        i = end + 1;
                        continue;
                    }
                    var mInsert = Regex.Match(token, @"^INSERT:(.+)$");
                    if (mInsert.Success)
                    {
                        string enumName = mInsert.Groups[1].Value;
                        if (!Enum.TryParse<RuntimeCodes>(enumName, out var rt))
                            throw new FormatException($"Unknown RuntimeCodes value '{enumName}' at position {i}");

                        values.Add(0x1F);
                        values.Add((byte)rt);
                        i = end + 1;
                        continue;
                    }
                    var mIcon = Regex.Match(token, @"^ICON:(.+)$");
                    if (mIcon.Success)
                    {
                        string enumName = mIcon.Groups[1].Value;
                        if (!Enum.TryParse<IconCodes>(enumName, out var ic))
                            throw new FormatException($"Unknown IconCodes value '{enumName}' at position {i}");

                        values.Add(0x0E);
                        values.Add((byte)ic);
                        i = end + 1;
                        continue;
                    }
                    throw new FormatException($"Unrecognised tag [{token}] at position {i}");
                }
                else
                {
                    if (CharacterTable.Values.Any(x => x == cha))
                    {
                        var charCode = CharacterTable.FirstOrDefault(x => x.Value == cha).Key;
                        values.Add((byte)charCode);
                    }
                    else
                        values.Add((byte)cha);
                }
                i++; 
            }
            return values.ToArray();
        }

        private static List<byte> TryParseSpecial(string text, ref int i, string type, Type enumType)
        {
            List<byte> values = new List<byte>();

            int sizeCheck = type.Length + 2; //Include : and [
            if (type == "COLOR")
                sizeCheck = type.Length + 3;

            if (text.Length > i + sizeCheck && text.Substring(i + 1, type.Length) == type)
            {
                //Go from the start of the tag and split at the end to get our value
                var valeText = text.Remove(0, i + sizeCheck).Split(']').FirstOrDefault();
                if (type == "COLOR")
                     valeText = text.Remove(0, i + sizeCheck).Split(')').FirstOrDefault();

                var valueCode = (byte)Enum.Parse(enumType, valeText);

                if (type == "Dialog")
                    values.Add(0x1C);
                if (type == "COLOR")
                    values.Add(0x1E);
                if (type == "INSERT")
                    values.Add(0x1F);
                if (type == "ICON")
                    values.Add(0x0E);

                if (type == "COLOR")
                    colorTagActive = true;

                values.Add(valueCode);

                i += (sizeCheck + valeText.Length);
                if (type != "COLOR")
                    i++;
            }
            else if (text.Length > i + 8 && text.Substring(i + 1, 7) == "Select2")
            {
                values.Add(0x0D);
                i += 8;
            }
            else if (text.Length > i + 7 && text.Substring(i + 1, 6) == "Select")
            {
                values.Add(0x0F);
                i += 7;
            }
            else if (text.Length > i + 9 && text.Substring(i + 1, 5) == "Align")
            {
                var numValues = int.Parse(text.Substring(i + 7, 1));
                byte[] valueCodes = new byte[numValues];
                for (int v = 0; v < numValues; v++)
                    valueCodes[v] = 0x0C;

                values.AddRange(valueCodes);
                i += 8;
            }

            return values;
        }

        static Dictionary<byte, char> CharacterTable = new Dictionary<byte, char>()
        {
            { 0x0A, '\n' },
            { 0x3D, '-' },

            { 0x5B, '`' },
            { 0x5C, '\'' },
            { 0x5D, '(' },
            { 0x5E, ')' },

            { 0x3F, '/' },

            { 0x7E, '&' },

            { 0x83, '@' },

            { 0x7B, ':' },
            { 0x82, ',' },
            { 0x84, '~' }, //Todo. Seems to be used in numbers so add a random character for now
            { 0x85, '.' },
            { 0xC2, '!' },
            { 0xC3, '?' },
            { 0x1D, '*' }, //Bold characters
            { 0xFF, '\r' },
            { 0x20, '\t' }, //Tabs for the start of some text
            { 0x10, ' ' },
            { 0xC0, '{' }, //start
            { 0xC1, '}' }, //end

            /*





            */
        };

        enum RuntimeCodes : byte
        {
            Option1 = 0x01,
            Option2 = 0x02,
            Option3 = 0x03,
            Option4 = 0x04,
            Option5 = 0x05,
            Option6 = 0x06,
            Option7 = 0x07,
            Option8 = 0x08,
        }

        //Can control sound effects
        enum DialogCodes : byte
        {
            Toad_Normal = 0x01,
            Toad_Excite = 0x02,
            Toad_Disappoint = 0x03,
            Goomba_Normal = 0x04,
            Goomba_Excite = 0x05,
            Goomba_Disappoint = 0x06,
            Shyguy_Normal = 0x07,
            Shyguy_Excite = 0x08,
            Shyguy_Disappoint = 0x09,
            Boo_Normal = 0x0A,
            Boo_Excite = 0x0B,
            Boo_Disappoint = 0x0C,
            Koopa_Normal = 0x0D,
            Koopa_Excite = 0x0E,
            Koopa_Disappoint = 0x0F,
            Bowser = 0x10,
            KoopaKid = 0x11,
            Thwomp = 0x13,
            Whomp = 0x14,
        }

        enum IconCodes : byte
        {
            ControlStick = 0x01,
            A = 0x03,
            B = 0x04,
            X = 0x05,
            Y = 0x06,
            R = 0x07,
            L = 0x09,
            Z = 0x0C,
            Coin = 0x13,
        }

        enum ColorCodes : byte
        {
            BLACK = 0x01,
            BLUE = 0x02,
            PINK = 0x03,
            RED = 0x04,
            GREEN = 0x05,
            YELLOW = 0x07,
            END = 0x08,
        }

        public enum FileListMP4
        {
            IndexTable = 0,
            CharacterList = 1,
            HiddenBlock = 2,
            BattleSpace = 3,
            BowserSpace = 4,
            WrapSpace = 5,
            ItemSpace = 6,
            Lottery = 7,
            BooHouse = 8,
            ItemList = 9,
            DiceRollMenu = 10,
            Toad_MerryGoGame = 11,
            Toad_SpaceRocketGame = 12,
            Toad_Star = 13,
            Toad_RollerCoaster = 14,
            Toad_HostBoardDialog = 15,
            Toad_ItemShop = 16,
            System = 17,
            ModeSelect = 18,
            ItemInfo = 19,
            Goomba_Roulette = 20,
            LuckMiniGame = 21,
            BoardStart = 22,
            Map3Event = 23,
            MiniGameNames = 24,
            MG_446 = 25,
            DebugMessage = 26,
            PartyMode = 27,
            Setup = 28,
            Miracle = 29,
            MiniGameKoopa = 30,
            StoryMode = 31,
            BowserStory = 32,
            Map4Event = 33,
            MiniGameInst = 34,
            E3 = 35,
            SAF = 36,
            MiniGameInst_Sys = 37,
            BoardResults = 38,
            BoardResults2 = 39,
            Map5Event = 40,
            MiniGameMode = 41,
            MG_445 = 42,
            MG_447 = 43,
            MG_448 = 44,
            MG_449 = 45,
            MG_450 = 46,
            Tutorial = 47,
            OptionRoom = 48,
            Map6Event = 49,
            Charley = 50,
            PresentRoom = 51,
            ExtraRoom = 52,
            StaffPost = 53,
            StaffName = 54,
            OpeningDemo = 55,
            MiniGameExInst = 56,
        }

        public class MessageData
        {
            public List<string> Entries = new List<string>();

            public byte[] Data;
        }
    }
}

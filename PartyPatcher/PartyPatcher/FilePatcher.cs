using PartyPatcher.Formats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PartyPatcher
{
    public class FilePatcher
    {
        public class MessageEntry : PatchEntry
        {
            // The message group.
            public string Group { get; set; } = "Group";
            // The message label which is stored in the output message.h header file to use with code.
            public string Label { get; set; }
            // The message to 
            public string Message { get; set; }

            public int ReplaceGroupIdx { get; set; } = -1;
            public int ReplaceMessageIdx { get; set; } = -1;
        }

        public class MusicEntry : PatchEntry
        {
            // The music label which is stored in the output music.h header file to use with code.
            public string Label { get; set; }
            // The file path for the .wav to add
            public string FilePath { get; set; }
        }

        public class RawBinPatchEntry : BinPatchEntry
        {
            // The file path for the file to inject
            public string FilePath { get; set; } = "";
        }
        public class SpritePatchEntry : BinPatchEntry
        {
            // The image list for atb sprites.
            public List<string> Images { get; set; }
            // Optional sprite sheet
            public string SpriteSheetFile { get; set; }
        }
        public class BinPatchEntry : PatchEntry
        {
            // Bin file to edit or create as new
            public string BinFile { get; set; } = "";
            // The name to label the entry
            public string Name { get; set; } = "";
            // The file to replace in bin if set, else adds new file.
            public int? ReplaceID { get; set; }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(SpritePatchEntry), "sprite")]
        [JsonDerivedType(typeof(RawBinPatchEntry), "file")]
        [JsonDerivedType(typeof(MusicEntry), "music")]
        [JsonDerivedType(typeof(MessageEntry), "message")]
        public class PatchEntry
        {

        }

        public enum FileType
        {
            Raw,
            Sprite,
            HSF,
        }

        public class PatchFile
        {
            public List<PatchEntry> Patches { get; set; } = new();
        }

        public static void Patch(string gamePath, string modPath)
        {
            Directory.CreateDirectory(modPath);
            Directory.CreateDirectory(Path.Combine(modPath, "sound"));
            Directory.CreateDirectory(Path.Combine(modPath, "data"));
            Directory.CreateDirectory(Path.Combine(modPath, "mess"));

            List<PatchFile> patchFiles = new();
            foreach (var file in Directory.GetFiles("mods", "*.json", SearchOption.AllDirectories))
            {
                if (file.EndsWith("Patch.json"))
                {
                    PatchFile patch = new();
                    patchFiles.Add(patch);

                    var modPatches = JsonSerializer.Deserialize<List<PatchFile>>(File.ReadAllText(file));
                    patch.Patches.AddRange(modPatches.SelectMany(x => x.Patches));
                }
            }

            var patches = patchFiles.SelectMany(x => x.Patches);

            PatchBins(patches.OfType<BinPatchEntry>().ToList(), gamePath, modPath);
            PatchMusic(patches.OfType<MusicEntry>().ToList(), gamePath, modPath);
            PatchMessages(patches.OfType<MessageEntry>().ToList(), gamePath, modPath);
        }

        static void PatchMusic(List<MusicEntry> files, string gamePath, string modPath)
        {
            if (files.Count == 0)
                return;

            string music_path = CreateBackup(Path.Combine(gamePath, "sound", "mpgcstr.pdt"));
            PdtFile pdt = new PdtFile("mpgcstr.pdt");

            foreach (var f in files)
            {
                PdtFile.StreamEntry entry = new();
                pdt.AddStream(entry);
                pdt.Import(pdt.StreamEntries.Length - 1, f.FilePath);
            }

            pdt.Save(Path.Combine(modPath, "sound", "mpgcstr.pdt"));
        }

        static void PatchMessages(List<MessageEntry> files, string gamePath, string modPath)
        {
            if (files.Count == 0)
                return;

            string mess_path = CreateBackup(Path.Combine(gamePath, "mess", "board_e.dat"));
            MessFile mess = new MessFile(mess_path, Encoding.UTF8, 4);

            Dictionary<string, int> groups = new();
            foreach (var f in files)
            {
                if (f.ReplaceGroupIdx != -1)
                {
                    if (f.ReplaceMessageIdx != -1)
                    {
                        // Replace group and message
                        mess.MessageFiles[f.ReplaceGroupIdx].Entries[f.ReplaceMessageIdx] = f.Message;
                    }
                    else
                        // Add new message to the group
                        mess.MessageFiles[f.ReplaceGroupIdx].Entries.Add(f.Message);
                }
                else
                {
                    // Add new group
                    if (!groups.ContainsKey(f.Group))
                    {
                        groups.Add(f.Group, mess.MessageFiles.Count);
                        mess.MessageFiles.Add(new MessFile.MessageData());
                    }
                    int grpIdx = groups[f.Group];
                    mess.MessageFiles[grpIdx].Entries.Add(f.Message);
                }
            }
            mess.Save(Path.Combine(modPath, "mess", "board_e.dat"));
        }

        static void PatchBins(List<BinPatchEntry> patches, string gamePath, string modPath)
        {
            var _headerWriter = new StreamWriter(Path.Combine(modPath, "files.h"));


            _headerWriter.WriteLine("#ifndef DATANUM_FILE_H");
            _headerWriter.WriteLine("#define DATANUM_FILE_H");

            _headerWriter.WriteLine("#include \"datadir_enum.h\"");

            _headerWriter.WriteLine("enum {");

            void WriteHeaderEntry(BinPatchEntry patch, int idx)
            {
                string name = Path.GetFileNameWithoutExtension(patch.Name);
                string bin_name = Path.GetFileNameWithoutExtension(patch.BinFile);

                string stype = "";
                if (patch is SpritePatchEntry) stype = "SPR_";
                if (name.EndsWith(".hsf")) stype = "HSF_";

                string value = $"DATA_MAKE_NUM(DATADIR_{bin_name.ToUpper()}, {idx})";

                _headerWriter.WriteLine($"{bin_name.ToUpper()}_{stype}{name.Replace(" ", "_").ToUpper()} = {value},");
            }

            // Organize per bin
            Dictionary<string, List<BinPatchEntry>> patchableBins = new();
            foreach (BinPatchEntry f in patches)
            {
                if (string.IsNullOrEmpty(f.BinFile))
                    continue;

                if (!patchableBins.ContainsKey(f.BinFile))
                    patchableBins.Add(f.BinFile, new List<BinPatchEntry>());
                patchableBins[f.BinFile].Add(f);
            }
            foreach (var binPatch in patchableBins)
            {
                string binFile = Path.Combine(gamePath, "data", binPatch.Key);
                // New bin file
                if (!File.Exists(binFile))
                {
                    BinFile bin = new BinFile();
                    foreach (var file in binPatch.Value)
                    {
                        WriteHeaderEntry(file, bin.Files.Count);
                        bin.Files.Add(new BinFile.FileEntry()
                        {
                            CompressionType = BinFile.CompressionType.LZSS,
                            FileData = LoadModFile(file),
                        });
                    }
                    bin.Save(Path.Combine(modPath, "data", Path.GetFileName(binFile)));
                }
                else // Edit existing
                {
                    // Use as backup copy first so user can patch to the same directory if needed
                    binFile = CreateBackup(binFile);
                    BinFile bin = new BinFile(binFile);
                    foreach (var file in binPatch.Value)
                    {
                        // Replace
                        if (file.ReplaceID.HasValue && file.ReplaceID.Value != -1 && file.ReplaceID.Value < bin.Files.Count)
                        {
                            WriteHeaderEntry(file, file.ReplaceID.Value);
                            bin.Files[file.ReplaceID.Value].FileData = LoadModFile(file);
                        }
                        else // Add as new
                        {
                            WriteHeaderEntry(file, bin.Files.Count);
                            bin.Files.Add(new BinFile.FileEntry()
                            {
                                CompressionType = BinFile.CompressionType.LZSS,
                                FileData = LoadModFile(file),
                            });
                        }
                    }
                    bin.Save(Path.Combine(modPath, "data", Path.GetFileName(binFile)));
                }
            }
            _headerWriter.WriteLine("};");
            _headerWriter.WriteLine("#endif");

            _headerWriter.Close();
        }

        static Stream LoadModFile(PatchEntry entry)
        {
            if (entry is SpritePatchEntry sprite)
            {
                var spriteSheetPath = sprite.SpriteSheetFile != null ?
                    Path.Combine("mods", sprite.SpriteSheetFile) : "";

                List<TexFormat> formats = new();

                var atb = new AtbFile();
                if (File.Exists(spriteSheetPath))
                    atb.FromJson(File.ReadAllText(spriteSheetPath), out formats);

                int index = 0;
                foreach (var file in sprite.Images)
                {
                    // Use original format from the json if it is loaded
                    var texFormat = index < formats.Count ? formats[index] : TexFormat.C8;

                    // Add only texture if pattern data is loaded from json
                    if (atb.Patterns.Count > 0)
                        atb.AddTexture(Path.Combine("mods", file), texFormat);
                    else
                        atb.FromFile(Path.Combine("mods", file), texFormat);
                }
                return new MemoryStream(atb.ToBytes());
            }
            if (entry is RawBinPatchEntry raw)
            {
                if (!File.Exists(Path.Combine("mods", raw.FilePath)))
                    throw new Exception($"Failed to find file {raw.FilePath}!");

                // Load raw
                return File.OpenRead(Path.Combine("mods", raw.FilePath));
            }
            return new MemoryStream();
        }

        static string CreateBackup(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"Failed to find file {path}!");

            Directory.CreateDirectory("original_mp4");

            // We want to transfer edited files to the "original" folder first incase user edits the game directory
            string target_path = Path.Combine("original_mp4", Path.GetFileName(path));
            if (!File.Exists(target_path))
                File.Copy(path, target_path, true);

            return target_path;
        }
    }
}

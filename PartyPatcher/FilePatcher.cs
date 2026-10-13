using PartyPatcher.Formats;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PartyPatcher
{
    // Patches the game file system from configured json and mod data
    // Users can configure what mods they want to patch, which will get patched from the original files
    // Currently includes messages, sounds, and bin data (raw file data and sprites to/from pngs)
    public class FilePatcher
    {
        public class MessageEntry
        {
            public string MessageFilePath { get; set; } = "board_e.dat";

            // The message group.
            public string Group { get; set; } = "Group";
            // The message label which is stored in the output message.h header file to use with code.
            public string Label { get; set; }
            // The message to 
            public string Message { get; set; }

            public int ReplaceGroupIdx { get; set; } = -1;
            public int ReplaceMessageIdx { get; set; } = -1;
        }

        public class MusicEntry
        {
            public string PdtPath { get; set; } = "mpgcstr.pdt";
            // The music label which is stored in the output music.h header file to use with code.
            public string Label { get; set; } = "";
            // The file path for the .wav to add
            public string FilePath { get; set; } = "";
            // The folder containing the mod.
            public string Folder { get; set; }
            // The file to replace in pdt if set, else adds new entry.
            public int? ReplaceID { get; set; }
        }

        public class SoundEntry
        {
            public string MsmPath { get; set; } = "mpgcsnd.msm";
            // The sound label which is stored in the output music.h header file to use with code.
            public string Label { get; set; } = "";
            // The file path for the .wav to add
            public string FilePath { get; set; } = "";
            // The folder containing the mod.
            public string Folder { get; set; }
            public int GroupId { get; set; }
            public int SoundId { get; set; }
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(SpritePatchEntry), "sprite")]
        [JsonDerivedType(typeof(RawPatchEntry), "file")]
        public class BinPatchEntry
        {
            // Bin file to edit or create as new
            public string BinFile { get; set; } = "";
            // The name to label the entry
            public string Name { get; set; } = "";
            // The file to replace in bin if set, else adds new file.
            public int? ReplaceID { get; set; }

            // Base folder for reading modded data
            [JsonIgnore]
            public string Folder;
        }
        public class RawPatchEntry : BinPatchEntry
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

        public enum FileType
        {
            Raw,
            Sprite,
            HSF,
        }

        public class PatchFile
        {
            public string FileName { get; set; }
            public List<BinPatchEntry> BinPatches { get; set; } = new();
            // msm
            public List<SoundEntry> SoundPatches { get; set; } = new();
            // pdt
            public List<MusicEntry> MusicPatches { get; set; } = new();
            public List<MessageEntry> MessagesPatches { get; set; } = new();

            public static PatchFile Load(string path)
            {
                return JsonSerializer.Deserialize<PatchFile>(File.ReadAllText(path));
            }

            public void Save(string path)
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions()
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                });
                File.WriteAllText(path, json);
            }
        }

        public static void Patch(List<PatchFile> patchFiles, string gamePath, string prefix, string modPath)
        {
            Directory.CreateDirectory(modPath);
            Directory.CreateDirectory(Path.Combine(modPath, "sound"));
            Directory.CreateDirectory(Path.Combine(modPath, "data"));
            Directory.CreateDirectory(Path.Combine(modPath, "mess"));

            PatchBins(patchFiles, gamePath, prefix, modPath);
            PatchMusic(patchFiles.SelectMany(x => x.MusicPatches).ToList(), gamePath, prefix, modPath);
            PatchMessages(patchFiles.SelectMany(x => x.MessagesPatches).ToList(), gamePath, prefix, modPath);
            PatchSounds(patchFiles.SelectMany(x => x.SoundPatches).ToList(), gamePath, prefix, modPath);
        }

        static void PatchSounds(List<SoundEntry> files, string gamePath, string prefix, string modPath)
        {
            if (files.Count == 0) return;

            // Write a header file to track all the modified stream tracks
            // Users can directly access streams rather than hardcoding IDs
            var _headerWriter = new StreamWriter(Path.Combine(modPath, "sounds_msm.h"));
            _headerWriter.WriteLine("#ifndef SOUNDS_MSM_FILE_H");
            _headerWriter.WriteLine("#define SOUNDS_MSM_FILE_H");
            _headerWriter.WriteLine("enum {");

            Dictionary<string, MsmFile> msmFiles = new();
            foreach (var f in files)
            {
                if (!msmFiles.ContainsKey(f.MsmPath))
                {
                    string stream_path = CreateBackup(Path.Combine(gamePath, "sound", f.MsmPath), prefix);
                    msmFiles.Add(f.MsmPath, new MsmFile(stream_path));
                }
                MsmFile msm = msmFiles[f.MsmPath];

                string soundWavPath = Path.Combine(f.Folder, f.FilePath);
                if (!File.Exists(soundWavPath))
                    continue;

                // Upper name with full path, no extension
            /*    string fileName = Path.GetFileNameWithoutExtension(f.FilePath);
                string dirName = Path.GetDirectoryName(f.FilePath).Replace("/", "_");
                string name = f.FilePath.Replace("/", "_").Split(".").FirstOrDefault().ToUpper();
                _headerWriter.WriteLine($"{name} = {f.SoundId},");
                */

                var g = msm.Groups[f.GroupId];
                var sdirs = g.GetSDirs();
                if (sdirs.Count <= f.SoundId)
                {
                    Console.WriteLine($"Invalid sound ID {f.SoundId} of sounds {sdirs.Count} in group {f.GroupId}");
                    continue;
                }

                sdirs[f.SoundId].Import(soundWavPath);

                g.RebuildSDirs(sdirs);
            }

            foreach (var f in msmFiles)
                f.Value.Save(Path.Combine(modPath, "sound", $"{f.Key}"));

            _headerWriter.WriteLine("};");
            _headerWriter.WriteLine("#endif // SOUNDS_MSM_FILE_H");
            _headerWriter.Close();
        }

        static void PatchMusic(List<MusicEntry> files, string gamePath, string prefix, string modPath)
        {
            // Write a header file to track all the modified stream tracks
            // Users can directly access streams rather than hardcoding IDs
            var _headerWriter = new StreamWriter(Path.Combine(modPath, "streams.h"));
            _headerWriter.WriteLine("#ifndef STREAMS_FILE_H");
            _headerWriter.WriteLine("#define STREAMS_FILE_H");
            _headerWriter.WriteLine("enum {");

            Dictionary<string, PdtFile> pdtFiles = new();
            foreach (var f in files)
            {
                if (!pdtFiles.ContainsKey(f.PdtPath))
                {
                    string stream_path = CreateBackup(Path.Combine(gamePath, "sound", f.PdtPath), prefix);
                    pdtFiles.Add(f.PdtPath, new PdtFile(stream_path));
                }
                PdtFile pdt = pdtFiles[f.PdtPath];

                string soundWavPath = Path.Combine(f.Folder, f.FilePath);
                if (!File.Exists(soundWavPath))
                {
                    Console.WriteLine($"Failed to find {soundWavPath}");
                    continue;
                }

                int id = pdt.StreamEntries.Length;

                if (f.ReplaceID.HasValue && f.ReplaceID.Value < pdt.StreamEntries.Length)
                {
                    pdt.Import(f.ReplaceID.Value, soundWavPath);
                }
                else // add new stream ID
                {
                    // Upper name with full path, no extension
                    // Add as an enum for new entries to access easily
                    string fileName = Path.GetFileNameWithoutExtension(soundWavPath);
                    string dirName = Path.GetDirectoryName(soundWavPath).Replace("/", "_");
                    string name = f.FilePath.Replace("/", "_").Split(".").FirstOrDefault().ToUpper();
                    _headerWriter.WriteLine($"{name} = {id},");

                    PdtFile.StreamEntry entry = new();
                    pdt.AddStream(entry);
                    pdt.Import(id, soundWavPath);
                }
            }
            foreach (var pdt in pdtFiles)
                pdt.Value.Save(Path.Combine(modPath, "sound", pdt.Key));

            _headerWriter.WriteLine("};");
            _headerWriter.WriteLine("#endif // STREAMS_FILE_H");
            _headerWriter.Close();
        }

        static void PatchMessages(List<MessageEntry> files, string gamePath, string prefix, string modPath)
        {
            Dictionary<string, MessFileDat> messFiles = new();

            Dictionary<string, int> groups = new();
            foreach (var f in files)
            {
                if (!messFiles.ContainsKey(f.MessageFilePath))
                {
                    string mess_path = CreateBackup(Path.Combine(gamePath, "mess", $"{f.MessageFilePath}"), prefix);
                    messFiles.Add(f.MessageFilePath,new MessFileDat(mess_path, Encoding.UTF8, 4));
                }

                var mess = messFiles[f.MessageFilePath];

                if (f.ReplaceGroupIdx != -1)
                {
                    if (f.ReplaceMessageIdx != -1)
                    {
                        // Replace group and message
                        mess.MessageFiles[f.ReplaceGroupIdx].Entries[$"{f.Group}_{f.ReplaceMessageIdx}"] = f.Message;
                    }
                    else
                        // Add new message to the group
                        mess.MessageFiles[f.ReplaceGroupIdx].Entries.Add($"{f.Group}_{mess.MessageFiles.Count}", f.Message);
                }
                else
                {
                    // Add new group
                    if (!groups.ContainsKey(f.Group))
                    {
                        groups.Add(f.Group, mess.MessageFiles.Count);
                        mess.MessageFiles.Add(new MessFileDat.MessageData());
                    }
                    int grpIdx = groups[f.Group];
                    mess.MessageFiles[grpIdx].Entries.Add($"{f.Group}_{mess.MessageFiles.Count}", f.Message);
                }
            }

            foreach (var f in messFiles)
                f.Value.Save(Path.Combine(modPath, "mess", Path.GetFileName(f.Key)));
        }

        static void PatchBins(List<PatchFile> files, string gamePath, string prefix, string modPath)
        {
            // Write a header file to track all the modified files
            // Users can directly access files via the file macro name rather than hardcoding IDs
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
            foreach (PatchFile patch in files)
            {
                foreach (var f in patch.BinPatches)
                {
                    if (string.IsNullOrEmpty(f.BinFile))
                        continue;

                    if (!patchableBins.ContainsKey(f.BinFile))
                        patchableBins.Add(f.BinFile, new List<BinPatchEntry>());
                    patchableBins[f.BinFile].Add(f);
                }
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
                        Console.WriteLine($"Replacing {file.BinFile} File{file.ReplaceID}");

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
                    binFile = CreateBackup(binFile, prefix);
                    BinFile bin = new BinFile(binFile);
                    foreach (var file in binPatch.Value)
                    {
                        // Replace
                        if (file.ReplaceID.HasValue && file.ReplaceID.Value != -1 && file.ReplaceID.Value < bin.Files.Count)
                        {
                            Console.WriteLine($"Replacing   {file.BinFile} File{file.ReplaceID}");

                            WriteHeaderEntry(file, file.ReplaceID.Value);
                            bin.Files[file.ReplaceID.Value].FileData = LoadModFile(file);
                        }
                        else // Add as new
                        {
                            Console.WriteLine($"Adding {file.BinFile} File{bin.Files.Count}");

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

        static Stream LoadModFile(BinPatchEntry entry)
        {
            if (entry is SpritePatchEntry sprite)
            {
                var spriteSheetPath = sprite.SpriteSheetFile != null ?
                    Path.Combine(entry.Folder, sprite.SpriteSheetFile) : "";

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
                        atb.AddTexture(Path.Combine(entry.Folder, file), texFormat);
                    else
                        atb.FromFile(Path.Combine(entry.Folder, file), texFormat);
                }
                return new MemoryStream(atb.ToBytes());
            }
            if (entry is RawPatchEntry raw)
            {
                if (!File.Exists(raw.FilePath))
                    throw new Exception($"Failed to find file {raw.FilePath}!");

                // Load raw
                return File.OpenRead(raw.FilePath);
            }
            return new MemoryStream();
        }

        static string CreateBackup(string path, string prefix)
        {
            if (!File.Exists(path))
                throw new Exception($"Failed to find file {path}!");

            Directory.CreateDirectory($"original_{prefix}");

            // We want to transfer edited files to the "original" folder first incase user edits the game directory
            string target_path = Path.Combine($"original_{prefix}", Path.GetFileName(path));
            if (!File.Exists(target_path))
                File.Copy(path, target_path, true);

            return target_path;
        }
    }
}

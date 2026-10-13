using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using static PartyPatcher.FilePatcher;

namespace PartyModManagerModBuilder
{

    public class ModEntry
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Author { get; set; }

        public PatchFile PatchFile { get; set; } = new PatchFile();

        public static ModEntry Load(string path) {
            var modEntry = JsonSerializer.Deserialize<ModEntry>(File.ReadAllText(path));
            var folder = Path.GetDirectoryName(path);
            foreach (var bin in modEntry.PatchFile.BinPatches)
                bin.Folder = folder;
            foreach (var snd in modEntry.PatchFile.SoundPatches)
                snd.Folder = folder;
            foreach (var ms in modEntry.PatchFile.MusicPatches)
                ms.Folder = folder;

            return modEntry;
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
}

// See https://aka.ms/new-console-template for more information
using AvaloniaToolbox.Core.IO;
using PartyModManagerModBuilder;
using PartyPatcher;
using PartyPatcher.Formats;
using System.Text;

// Message dump
foreach (var arg in args)
{
    if (arg.EndsWith("board.dat") || arg.EndsWith("board_e.dat") ||
        arg.EndsWith("mini.dat") || arg.EndsWith("mini_e.dat"))
    {
        MessFile mess = new(arg, Encoding.UTF8, 4);
        var json = MessFile.Export(mess);
        File.WriteAllText(Path.GetFileName(arg) + ".json", json);
        return;
    }
    if (arg.EndsWith("mpgcsnd.msm"))
    {
        MsmFile msm = new(arg);
        msm.Dump(Path.GetFileNameWithoutExtension(arg));
    }
    if (arg.EndsWith("bin"))
    {
        string folder = Path.GetFileNameWithoutExtension(arg);
        Directory.CreateDirectory(folder);

        BinFile binFile = new(arg);
        foreach (var file in binFile.Files)
        {
            if (file.FileName.EndsWith(".atb"))
            {
                try
                {
                    var data = file.DecompressData(file.FileData);
                    AtbFile atb = new(data);

                    string dir = Path.Combine(folder, Path.GetFileNameWithoutExtension(file.FileName));
                    Directory.CreateDirectory(dir);

                    atb.ExportJson(Path.Combine(dir, $"Sheet.json"));
                    for (int i = 0; i < atb.Textures.Count; i++)
                        atb.Export(atb.Textures[i], Path.Combine(dir, $"Texture{i}.png"));
                }
                catch
                {
                    file.Export(Path.Combine(folder, file.FileName));
                }

            }
            else
            {
                file.Export(Path.Combine(folder, file.FileName));
            }
        }
    }
}

Console.WriteLine("Hello, World!");

var config = Config.Load("MP4.json");
if (!Directory.Exists(config.GamePath))
{
    Console.WriteLine($"No game path set!");
    return;
}

// Look through all the mods and load each meta json for patching
List<FilePatcher.PatchFile> patches = new();
foreach (var f in Directory.GetFiles("mods", "*.json", SearchOption.AllDirectories))
{
    if (f.EndsWith("Meta.json"))
        patches.Add(ModEntry.Load(f).PatchFile);
}

if (patches.Count == 0)
{
    Console.WriteLine($"No patches found in mods folder!");
    return;
}


FilePatcher.Patch(patches, config.GamePath, "mp4", "output");

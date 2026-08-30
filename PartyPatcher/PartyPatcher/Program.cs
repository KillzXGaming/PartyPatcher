// See https://aka.ms/new-console-template for more information
using PartyPatcher;

Console.WriteLine("Hello, World!");

var config = Config.Load("MP4.json");
if (!File.Exists(config.GamePath))
{
    Console.WriteLine($"No game path set!");
    return;
}
FilePatcher.Patch(config.GamePath, "output");
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PartyPatcher
{
    public class Config
    {
        public string GamePath { get; set; } 

        public static Config Load(string path)
        {
            if (!File.Exists(path))
                new Config().Save(path);

            return JsonSerializer.Deserialize<Config>(File.ReadAllText(path));
        }

        public void Save(string path)
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }
}

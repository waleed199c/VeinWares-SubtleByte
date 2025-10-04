using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Config
{
    public static class ItemStackConfig
    {
        public class StackConfigEntry
        {
            public int PrefabGUID { get; set; }
            public int StackSize { get; set; }
        }

        public static List<StackConfigEntry> Entries { get; private set; } = new();

        public static void Load()
        {
            var folder = Path.Combine(BepInEx.Paths.ConfigPath, "VeinWares SubtleByte");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, "ItemStackConfig.json");

            if (!File.Exists(path))
            {
                Entries = new List<StackConfigEntry> {
                    new StackConfigEntry { PrefabGUID = 1900061533, StackSize = 4000 }
                };
                var json = JsonSerializer.Serialize(new { Items = Entries }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                ModLogger.Warn($"[Config] Default Config 404 created default config");
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<ItemStackFile>(json);
                Entries = parsed?.Items ?? new List<StackConfigEntry>();
                ModLogger.Info($"[Config] Loaded {Entries.Count} item stack entries.");
            }
            catch
            {
                ModLogger.Error($"[Config] Failed to load config");
            }
        }

        private class ItemStackFile
        {
            public List<StackConfigEntry> Items { get; set; }
        }
    }
}

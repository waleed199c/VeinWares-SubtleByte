using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using VeinWares.SubtleByte.Utilities;

#nullable enable

namespace VeinWares.SubtleByte.Config
{
    public static class ItemStackConfig
    {
        private const string FileName = "ItemStackConfig.json";

        private static readonly string ConfigDirectory = Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte");
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, FileName);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public sealed record StackConfigEntry
        {
            [JsonPropertyName("prefabGuid")]
            public int PrefabGuid { get; init; }

            [JsonPropertyName("stackSize")]
            public int StackSize { get; init; }

            [JsonPropertyName("label")]
            public string? Label { get; init; }
        }

        private sealed record ItemStackConfigFile
        {
            [JsonPropertyName("metadata")]
            public Dictionary<string, string>? Metadata { get; init; }

            [JsonPropertyName("items")]
            public List<StackConfigEntry> Items { get; init; } = new();
        }

        public static IReadOnlyList<StackConfigEntry> Entries { get; private set; } = Array.Empty<StackConfigEntry>();

        public static void Load()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                if (!File.Exists(ConfigPath))
                {
                    CreateDefaultConfig();
                    ModLogger.Info($"[Config] Created default {FileName} with 1 sample entry.", verboseOnly: false);
                }

                var json = File.ReadAllText(ConfigPath);
                var file = JsonSerializer.Deserialize<ItemStackConfigFile>(json, JsonOptions);

                Entries = file?.Items?.Count > 0
                    ? file.Items
                    : Array.Empty<StackConfigEntry>();

                ModLogger.Info($"[Config] Loaded {Entries.Count} item stack {(Entries.Count == 1 ? "entry" : "entries")}.", verboseOnly: false);
            }
            catch (Exception ex)
            {
                Entries = Array.Empty<StackConfigEntry>();
                ModLogger.Error($"[Config] Failed to load {FileName}: {ex.Message}");
            }
        }

        private static void CreateDefaultConfig()
        {
            var file = new ItemStackConfigFile
            {
                Metadata = new Dictionary<string, string>
                {
                    ["notes"] = "Add each prefab GUID you want to override and the new stack size.",
                    ["tip"] = "Remove the label field once you are done if you do not need it."
                },
                Items = new List<StackConfigEntry>
                {
                    new()
                    {
                        PrefabGuid = 1900061533,
                        StackSize = 4000,
                        Label = "Example: Pristine Hide"
                    }
                }
            };

            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
    }
}

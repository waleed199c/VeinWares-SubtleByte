using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;                // for Paths.ConfigPath
using ProjectM;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace VeinWares.SubtleByte.Config
{
    // One stat line in JSON
    internal sealed class PrestigeStatLine
    {
        [JsonPropertyName("statType")]
        public string statType { get; set; } = "PhysicalPower";

        [JsonPropertyName("modification")]
        public string modification { get; set; } = "Add";

        [JsonPropertyName("value")]
        public float value { get; set; } = 0f;

        [JsonPropertyName("attributeCap")]
        public string attributeCap { get; set; } = "Uncapped";
    }

    // Whole file shape

    internal sealed class PrestigeConfigFile
    {
        [JsonPropertyName("hijacked_buff_guid")]
        public int hijacked_buff_guid { get; set; } = 103615205;

        [JsonPropertyName("levels")]
        public Dictionary<string, List<PrestigeStatLine>> levels { get; set; } = new();

        // keep example block to avoid errors if present
        [JsonPropertyName("EXAMPLE_ALL_OPTIONS")]
        public Dictionary<string, object> EXAMPLE_ALL_OPTIONS { get; set; } = new();
    }

    internal static class SubtleBytePrestigeConfig
    {
        public static string ConfigDir => Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte");
        public static string ConfigPath => Path.Combine(ConfigDir, "SubtleBytePrestigeConfig.json");

        private static PrestigeConfigFile _cfg = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static void LoadOrCreate()
        {
            try
            {
                // ensure folder exists
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                if (!File.Exists(ConfigPath))
                {
                    _cfg = CreateDefault();
                    File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_cfg, JsonOpts));
                    Core.Log.LogInfo($"[PrestigeConfig] Created default at: {ConfigPath}");
                    return;
                }

                var text = File.ReadAllText(ConfigPath);
                _cfg = JsonSerializer.Deserialize<PrestigeConfigFile>(text, JsonOpts) ?? CreateDefault();
                Core.Log.LogInfo("[PrestigeConfig] Loaded SubtleBytePrestigeConfig.json");
            }
            catch (Exception e)
            {
                Core.Log.LogError($"[PrestigeConfig] Failed to load: {e.Message}");
                _cfg = CreateDefault();
            }
        }

        public static void Reload() => LoadOrCreate();

        public static int HijackedBuffGuid()
            => _cfg?.hijacked_buff_guid ?? -1104282069;

        // Merge rules from levels 2..targetLevel (inclusive)
        public static List<PrestigeStatLine> GetMergedRules(int targetLevel)
        {
            var outList = new List<PrestigeStatLine>();
            if (_cfg?.levels == null) return outList;

            for (int lvl = 2; lvl <= targetLevel; lvl++)
            {
                if (_cfg.levels.TryGetValue(lvl.ToString(), out var lines) && lines != null)
                    outList.AddRange(lines);
            }
            return outList;
        }

        // --- builders ---
        private static PrestigeConfigFile CreateDefault()
        {
            var cfg = new PrestigeConfigFile
            {
                hijacked_buff_guid = 103615205,
                levels = new Dictionary<string, List<PrestigeStatLine>>()
            };

            // Level 2: your test
            cfg.levels["2"] = new List<PrestigeStatLine>
            {
                new PrestigeStatLine { statType="PhysicalPower", modification="Add", value=100f, attributeCap="Uncapped" }
            };

            // 3..10 empty
            for (int lvl = 3; lvl <= 10; lvl++)
                cfg.levels[lvl.ToString()] = new List<PrestigeStatLine>();

            // Example block (ignored by runtime; just for users to copy from)
            cfg.EXAMPLE_ALL_OPTIONS = new Dictionary<string, object>
            {
                ["_notes"] = new[]
                {
                    "Copy any line from sample_lines into levels[\"2\"..\"10\"].",
                    "statType must be a valid UnitStatType name.",
                    "modification: Add | Multiply | Set",
                    "attributeCap: SoftCapped | HardCapped | Uncapped"
                },
                ["sample_lines"] = new object[]
                {
                    new { statType="PhysicalPower",  modification="Add",      value=50.0,   attributeCap="Uncapped" },
                    new { statType="SpellPower",     modification="Add",      value=50.0,   attributeCap="Uncapped" },
                    new { statType="MaxHealth",      modification="Add",      value=250.0,  attributeCap="Uncapped" },
                    new { statType="MovementSpeed",  modification="Multiply", value=0.03,   attributeCap="Uncapped" },
                    new { statType="CooldownRecoveryRate", modification="Multiply", value=0.05, attributeCap="Uncapped" },
                    new { statType="ResourceYield",  modification="Multiply", value=0.10,   attributeCap="Uncapped" },
                    new { statType="PrimaryAttackSpeed", modification="Multiply", value=0.05, attributeCap="Uncapped" },
                    new { statType="AbilityAttackSpeed", modification="Multiply", value=0.05, attributeCap="Uncapped" },
                    new { statType="PhysicalResistance", modification="Add",  value=10.0,   attributeCap="Uncapped" },
                    new { statType="SpellResistance",    modification="Add",  value=10.0,   attributeCap="Uncapped" },
                    new { statType="FireResistance",     modification="Add",  value=10.0,   attributeCap="Uncapped" },
                    new { statType="HolyResistance",     modification="Add",  value=10.0,   attributeCap="Uncapped" },
                    new { statType="GarlicResistance",   modification="Add",  value=10.0,   attributeCap="Uncapped" },
                    new { statType="SilverResistance",   modification="Add",  value=10.0,   attributeCap="Uncapped" },
                    new { statType="PassiveHealthRegen", modification="Add",  value=2.0,    attributeCap="Uncapped" },
                    new { statType="HealthRecovery",     modification="Add",  value=2.0,    attributeCap="Uncapped" },
                    new { statType="SiegePower",         modification="Add",  value=25.0,   attributeCap="Uncapped" },
                    new { statType="ResourcePower",      modification="Add",  value=25.0,   attributeCap="Uncapped" },
                    new { statType="SunResistance",      modification="Add",  value=5.0,    attributeCap="Uncapped" },
                    new { statType="SunChargeTime",      modification="Add",  value=120.0,  attributeCap="Uncapped" }
                }
            };

            return cfg;
        }
    }
}

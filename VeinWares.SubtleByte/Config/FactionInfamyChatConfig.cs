using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;

namespace VeinWares.SubtleByte.Config;

internal static class FactionInfamyChatConfig
{
    private const string FileName = "FactionInfamyAmbushChat.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static readonly object Sync = new();

    private static ManualLogSource? _log;
    private static AmbushChatConfiguration _configuration = CreateDefault();
    private static string? _configPath;

    public static void Initialize(ManualLogSource log)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        lock (Sync)
        {
            _log = log;
            _configPath = Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte", FileName);
            EnsureConfigLoaded();
        }
    }

    public static void Shutdown()
    {
        lock (Sync)
        {
            _log = null;
            _configPath = null;
            _configuration = CreateDefault();
        }
    }

    public static string GetAmbushMessage(string factionId, int difficultyTier, float hateValue, int levelOffset)
    {
        lock (Sync)
        {
            var template = ResolveTemplate(factionId);
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var factionName = string.IsNullOrWhiteSpace(factionId) ? "Unknown" : factionId;
            var formatted = template
                .Replace("{Faction}", factionName, StringComparison.OrdinalIgnoreCase)
                .Replace("{Tier}", difficultyTier.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("{Hate}", hateValue.ToString("0.##", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("{LevelOffset}", levelOffset.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

            return formatted;
        }
    }

    private static string ResolveTemplate(string factionId)
    {
        if (!string.IsNullOrWhiteSpace(factionId) &&
            _configuration.Factions.TryGetValue(factionId, out var entry) &&
            !string.IsNullOrWhiteSpace(entry.AmbushMessage))
        {
            return entry.AmbushMessage!;
        }

        return _configuration.DefaultMessage;
    }

    private static void EnsureConfigLoaded()
    {
        if (string.IsNullOrWhiteSpace(_configPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_configPath))
            {
                WriteDefaultFile(_configPath);
                return;
            }

            var content = File.ReadAllText(_configPath);
            var parsed = JsonSerializer.Deserialize<AmbushChatConfiguration>(content, JsonOptions);
            if (parsed != null)
            {
                parsed.Normalise();
                _configuration = parsed;
                return;
            }

            _log?.LogWarning("[Infamy] Failed to parse ambush chat config, using defaults.");
            _configuration = CreateDefault();
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Infamy] Error loading ambush chat config: {ex.Message}");
            _configuration = CreateDefault();
        }
    }

    private static void WriteDefaultFile(string path)
    {
        var defaults = CreateDefault();
        defaults.Normalise();
        var json = JsonSerializer.Serialize(defaults, JsonOptions);
        File.WriteAllText(path, json);
        _configuration = defaults;
        _log?.LogInfo($"[Infamy] Created default ambush chat config at '{path}'.");
    }

    private static AmbushChatConfiguration CreateDefault()
    {
        return new AmbushChatConfiguration
        {
            DefaultMessage = "{Faction} retaliatory squad is hunting you! Difficulty tier {Tier}.",
            Factions = new Dictionary<string, AmbushChatEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bandits"] = new() { AmbushMessage = "Bandit hunters are closing in! Tier {Tier}." },
                ["Blackfangs"] = new() { AmbushMessage = "Blackfang assassins strike from the shadows! Tier {Tier}." },
                ["Militia"] = new() { AmbushMessage = "Militia patrol dispatched to your location. Tier {Tier}." },
                ["Gloomrot"] = new() { AmbushMessage = "Gloomrot technology squad mobilised! Tier {Tier}." },
                ["Legion"] = new() { AmbushMessage = "The Legion answers your crimes. Tier {Tier}." },
                ["Undead"] = new() { AmbushMessage = "Undead champions rise to challenge you. Tier {Tier}." },
                ["Werewolf"] = new() { AmbushMessage = "Howls echo as werewolves stalk you. Tier {Tier}." }
            }
        };
    }

    private sealed class AmbushChatConfiguration
    {
        public string DefaultMessage { get; set; } = string.Empty;

        public Dictionary<string, AmbushChatEntry> Factions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Normalise()
        {
            DefaultMessage ??= string.Empty;
            if (Factions == null)
            {
                Factions = new Dictionary<string, AmbushChatEntry>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            foreach (var pair in Factions)
            {
                if (pair.Value == null)
                {
                    Factions[pair.Key] = new AmbushChatEntry();
                }
            }
        }
    }

    private sealed class AmbushChatEntry
    {
        public string? AmbushMessage { get; set; }
    }
}

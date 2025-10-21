using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;

#nullable enable

namespace VeinWares.SubtleByte.Config;

internal static class FactionInfamyChatConfig
{
    private const string FileName = "FactionInfamyAmbushChat.json";
    private const string DefaultColor = "#FFFFFF";
    private const int MaxTier = 5;

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
            return FormatMessage(factionId, difficultyTier, hateValue, levelOffset);
        }
    }

    public static string GetTierAnnouncement(string factionId, int tier, float hateValue)
    {
        lock (Sync)
        {
            return FormatMessage(factionId, tier, hateValue, levelOffset: 0);
        }
    }

    private static string FormatMessage(string factionId, int tier, float hateValue, int levelOffset)
    {
        var (template, color) = ResolveTemplate(factionId, tier);
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var factionName = string.IsNullOrWhiteSpace(factionId) ? "Unknown" : factionId;
        var formatted = template
            .Replace("{Faction}", factionName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Tier}", tier.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{Hate}", hateValue.ToString("0.##", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{LevelOffset}", FormatLevelOffset(levelOffset), StringComparison.OrdinalIgnoreCase);

        formatted = formatted.Trim();
        if (formatted.Length == 0)
        {
            return string.Empty;
        }

        return WrapWithColor(formatted, color);
    }

    private static (string Template, string Color) ResolveTemplate(string factionId, int tier)
    {
        tier = Math.Clamp(tier, 1, MaxTier);

        var color = _configuration.DefaultColor;
        var template = _configuration.DefaultTierMessages.TryGetValue(tier, out var defaultTemplate)
            ? defaultTemplate
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(factionId) &&
            _configuration.Factions.TryGetValue(factionId, out var entry))
        {
            color = entry.Color ?? color;

            if (entry.TierMessages.TryGetValue(tier, out var tierTemplate) && !string.IsNullOrWhiteSpace(tierTemplate))
            {
                template = tierTemplate;
            }
        }

        return (template, color);
    }

    private static string WrapWithColor(string message, string color)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var resolved = NormaliseColor(color, _configuration.DefaultColor);
        return $"<color={resolved}>{message}</color>";
    }

    private static string FormatLevelOffset(int levelOffset)
    {
        return levelOffset.ToString("+0;-0;0", CultureInfo.InvariantCulture);
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
        var json = JsonSerializer.Serialize(defaults, JsonOptions);
        File.WriteAllText(path, json);
        _configuration = defaults;
        _log?.LogInfo($"[Infamy] Created default ambush chat config at '{path}'.");
    }

    private static AmbushChatConfiguration CreateDefault()
    {
        var configuration = new AmbushChatConfiguration
        {
            DefaultColor = DefaultColor,
            DefaultTierMessages = CreateDefaultTierMessages(),
            Factions = new Dictionary<string, AmbushChatEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bandits"] = new()
                {
                    Color = "#E07A15",
                    TierMessages = CreateTierMessages(
                        "Bandit scouts gossip about you. Tier {Tier}, hate {Hate}.",
                        "Bandit raiders mark your territory. Tier {Tier}, hate {Hate}.",
                        "Bandit warbands are gathering. Tier {Tier}, hate {Hate}.",
                        "Bandit captains call the hunt. Tier {Tier}. Level offset {LevelOffset}.",
                        "The Bandit elite ride for you! Tier {Tier}. Level offset {LevelOffset}.")
                },
                ["Blackfangs"] = new()
                {
                    Color = "#8E44AD",
                    TierMessages = CreateTierMessages(
                        "Blackfang informants whisper your name. Tier {Tier}, hate {Hate}.",
                        "Blackfang stalkers keep you in their sights. Tier {Tier}, hate {Hate}.",
                        "Blackfang killers sharpen their blades. Tier {Tier}, hate {Hate}.",
                        "Blackfang lieutenants ready a strike. Tier {Tier}. Level offset {LevelOffset}.",
                        "Blackfang assassins descend! Tier {Tier}. Level offset {LevelOffset}.")
                },
                ["Militia"] = new()
                {
                    Color = "#4E8DDE",
                    TierMessages = CreateTierMessages(
                        "Militia patrols note your movements. Tier {Tier}, hate {Hate}.",
                        "Militia sergeants brief the troops. Tier {Tier}, hate {Hate}.",
                        "Militia commanders rally forces. Tier {Tier}, hate {Hate}.",
                        "Militia banners rise against you. Tier {Tier}. Level offset {LevelOffset}.",
                        "Militia champions march to end you! Tier {Tier}. Level offset {LevelOffset}.")
                },
                ["Gloomrot"] = new()
                {
                    Color = "#76C7C5",
                    TierMessages = CreateTierMessages(
                        "Gloomrot scouts calibrate their scanners. Tier {Tier}, hate {Hate}.",
                        "Gloomrot engineers draft retaliation plans. Tier {Tier}, hate {Hate}.",
                        "Gloomrot strike teams energize weapons. Tier {Tier}, hate {Hate}.",
                        "Gloomrot directors demand your capture. Tier {Tier}. Level offset {LevelOffset}.",
                        "Gloomrot apex hunters deploy! Tier {Tier}. Level offset {LevelOffset}.")
                },
                ["Legion"] = new()
                {
                    Color = "#C0392B",
                    TierMessages = CreateTierMessages(
                        "Legion sentries glare in your direction. Tier {Tier}, hate {Hate}.",
                        "Legion captains tighten their ranks. Tier {Tier}, hate {Hate}.",
                        "Legion warlocks weave countermeasures. Tier {Tier}, hate {Hate}.",
                        "Legion generals decree retaliation. Tier {Tier}. Level offset {LevelOffset}.",
                        "Legion champions march with fury! Tier {Tier}. Level offset {LevelOffset}.")
                },
                ["Undead"] = new()
                {
                    Color = "#7F8C8D",
                    TierMessages = CreateTierMessages(
                        "The dead murmur about your presence. Tier {Tier}, hate {Hate}.",
                        "Undead lieutenants stir from crypts. Tier {Tier}, hate {Hate}.",
                        "Undead warbands claw at their graves. Tier {Tier}, hate {Hate}.",
                        "Undead commanders knit a hunting party. Tier {Tier}. Level offset {LevelOffset}.",
                        "Undead champions rise to claim you! Tier {Tier}. Level offset {LevelOffset}.")
                },
                ["Werewolf"] = new()
                {
                    Color = "#A67C52",
                    TierMessages = CreateTierMessages(
                        "Distant howls signal your scent. Tier {Tier}, hate {Hate}.",
                        "Werewolf packs circle the woods. Tier {Tier}, hate {Hate}.",
                        "Werewolf alphas bare their fangs. Tier {Tier}, hate {Hate}.",
                        "Werewolf elders call the moon-hunt. Tier {Tier}. Level offset {LevelOffset}.",
                        "Werewolf legends race for your blood! Tier {Tier}. Level offset {LevelOffset}.")
                }
            }
        };

        configuration.Normalise();
        return configuration;
    }

    private static Dictionary<int, string> CreateDefaultTierMessages()
    {
        return CreateTierMessages(
            "{Faction} are beginning to notice you. Tier {Tier}, hate {Hate}.",
            "{Faction} put you on their watchlist. Tier {Tier}, hate {Hate}.",
            "{Faction} marshal a response. Tier {Tier}, hate {Hate}.",
            "{Faction} are preparing an elite hunt. Tier {Tier}. Level offset {LevelOffset}.",
            "{Faction} unleash their deadliest hunters! Tier {Tier}. Level offset {LevelOffset}.");
    }

    private static Dictionary<int, string> CreateTierMessages(
        string tier1,
        string tier2,
        string tier3,
        string tier4,
        string tier5)
    {
        return new Dictionary<int, string>
        {
            [1] = tier1,
            [2] = tier2,
            [3] = tier3,
            [4] = tier4,
            [5] = tier5
        };
    }

    private static string NormaliseColor(string? value, string fallback)
    {
        var effectiveFallback = string.IsNullOrWhiteSpace(fallback) ? DefaultColor : fallback.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return effectiveFallback;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
        {
            trimmed = "#" + trimmed;
        }

        var hex = trimmed[1..];
        if (hex.Length is not 6 and not 8)
        {
            return effectiveFallback;
        }

        for (var i = 0; i < hex.Length; i++)
        {
            if (!Uri.IsHexDigit(hex[i]))
            {
                return effectiveFallback;
            }
        }

        return "#" + hex.ToUpperInvariant();
    }

    private static Dictionary<int, string> NormaliseTierMessages(Dictionary<int, string>? source, IReadOnlyDictionary<int, string> fallback)
    {
        var result = new Dictionary<int, string>(MaxTier);

        for (var tier = 1; tier <= MaxTier; tier++)
        {
            if (source != null && source.TryGetValue(tier, out var provided) && !string.IsNullOrWhiteSpace(provided))
            {
                result[tier] = provided.Trim();
                continue;
            }

            if (fallback.TryGetValue(tier, out var fallbackMessage))
            {
                result[tier] = fallbackMessage;
            }
            else
            {
                result[tier] = string.Empty;
            }
        }

        return result;
    }

    private sealed class AmbushChatConfiguration
    {
        private const string DefaultColorValue = "#FFFFFF";

        public string DefaultColor { get; set; } = DefaultColorValue;

        public Dictionary<int, string> DefaultTierMessages { get; set; } = new();

        public Dictionary<string, AmbushChatEntry> Factions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Normalise()
        {
            DefaultColor = NormaliseColor(DefaultColor, DefaultColorValue);
            DefaultTierMessages = NormaliseTierMessages(DefaultTierMessages, CreateDefaultTierMessages());

            if (Factions == null)
            {
                Factions = new Dictionary<string, AmbushChatEntry>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            foreach (var key in Factions.Keys.ToArray())
            {
                var entry = Factions[key] ?? new AmbushChatEntry();
                entry.Normalise(DefaultColor, DefaultTierMessages);
                Factions[key] = entry;
            }
        }
    }

    private sealed class AmbushChatEntry
    {
        public string? Color { get; set; }

        public Dictionary<int, string> TierMessages { get; set; } = new();

        public void Normalise(string fallbackColor, IReadOnlyDictionary<int, string> fallbackTierMessages)
        {
            Color = NormaliseColor(Color, fallbackColor);
            TierMessages = NormaliseTierMessages(TierMessages, fallbackTierMessages);
        }
    }
}

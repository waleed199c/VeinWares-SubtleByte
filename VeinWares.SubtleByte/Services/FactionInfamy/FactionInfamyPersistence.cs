using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using VeinWares.SubtleByte.Models.FactionInfamy;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyPersistence
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static string ConfigDirectory => Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte", "Infamy");
    private static string LegacyDirectory => Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte");
    private static string SavePath => Path.Combine(ConfigDirectory, "playerInfamyLevel.json");
    private static string LegacySavePath => Path.Combine(LegacyDirectory, "playerInfamyLevel.json");

    public static Dictionary<ulong, PlayerHateData> Load()
    {
        try
        {
            EnsureLegacySaveMigrated();

            if (!File.Exists(SavePath))
            {
                return new Dictionary<ulong, PlayerHateData>();
            }

            var json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<ulong, PlayerHateData>();
            }

            var payload = JsonSerializer.Deserialize<Dictionary<string, PlayerHateRecord>>(json, Options);
            if (payload is null)
            {
                return new Dictionary<ulong, PlayerHateData>();
            }

            return payload
                .Select(static pair => new KeyValuePair<ulong, PlayerHateData>(
                    ulong.TryParse(pair.Key, out var steamId) ? steamId : 0,
                    FromRecord(pair.Value)))
                .Where(static pair => pair.Key != 0)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"[InfamyPersistence] Failed to load hate data: {ex.Message}");
            return new Dictionary<ulong, PlayerHateData>();
        }
    }

    public static void Save(Dictionary<string, PlayerHateRecord> snapshot, int backupCount)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (File.Exists(SavePath) && backupCount > 0)
            {
                CreateRollingBackups(backupCount);
            }

            var json = JsonSerializer.Serialize(snapshot, Options);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"[InfamyPersistence] Failed to save hate data: {ex.Message}");
        }
    }

    private static PlayerHateData FromRecord(PlayerHateRecord record)
    {
        var data = new PlayerHateData
        {
            LastCombatStart = record.LastCombatStart,
            LastCombatEnd = record.LastCombatEnd,
            InCombat = false
        };

        if (record.Factions is null)
        {
            return data;
        }

        foreach (var faction in record.Factions)
        {
            var factionRecord = faction.Value;
            if (factionRecord is null)
            {
                continue;
            }

            var entry = new HateEntry
            {
                Hate = factionRecord.Hate,
                LastAmbush = factionRecord.LastAmbush,
                LastUpdated = factionRecord.LastUpdated,
                LastAnnouncedTier = factionRecord.LastAnnouncedTier
            };
            data.SetHate(faction.Key, entry);
        }

        return data;
    }

    private static void CreateRollingBackups(int backupCount)
    {
        try
        {
            for (var index = backupCount - 1; index >= 0; index--)
            {
                var source = index == 0 ? SavePath : GetBackupPath(index - 1);
                var destination = GetBackupPath(index);

                if (File.Exists(source))
                {
                    File.Copy(source, destination, overwrite: true);
                }
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"[InfamyPersistence] Failed to rotate backups: {ex.Message}");
        }
    }

    private static void EnsureLegacySaveMigrated()
    {
        try
        {
            if (File.Exists(SavePath) || !File.Exists(LegacySavePath))
            {
                return;
            }

            Directory.CreateDirectory(ConfigDirectory);

            File.Move(LegacySavePath, SavePath);
            ModLogger.Info("[InfamyPersistence] Migrated legacy hate data to Infamy directory.");

            var legacyBackups = Directory
                .EnumerateFiles(LegacyDirectory, "playerInfamyLevel.json.bak*")
                .ToList();

            foreach (var backup in legacyBackups)
            {
                var fileName = Path.GetFileName(backup);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                var destination = Path.Combine(ConfigDirectory, fileName);

                if (File.Exists(destination))
                {
                    continue;
                }

                File.Move(backup, destination);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"[InfamyPersistence] Failed to migrate legacy hate data: {ex.Message}");
        }
    }

    private static string GetBackupPath(int index)
    {
        var suffix = index.ToString().PadLeft(2, '0');
        var fileName = $"playerInfamyLevel.json.bak{suffix}";
        return Path.Combine(ConfigDirectory, fileName);
    }
}

internal sealed class PlayerHateRecord
{
    public Dictionary<string, HateEntryRecord> Factions { get; set; } = new();

    public DateTime LastCombatStart { get; set; }

    public DateTime LastCombatEnd { get; set; }
}

internal sealed class HateEntryRecord
{
    public float Hate { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime LastAmbush { get; set; }

    public int LastAnnouncedTier { get; set; }
}

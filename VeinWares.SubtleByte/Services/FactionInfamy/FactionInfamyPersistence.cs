using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using VeinWares.SubtleByte.Models.FactionInfamy;
using VeinWares.SubtleByte.Utilities;

#nullable enable

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyPersistence
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly object PathResolutionLock = new();
    private static string? _baseConfigPath;

    private static string BaseConfigPath
    {
        get
        {
            if (_baseConfigPath is not null)
            {
                return _baseConfigPath;
            }

            lock (PathResolutionLock)
            {
                if (_baseConfigPath is null)
                {
                    _baseConfigPath = ResolveBaseConfigPath();
                }

                return _baseConfigPath;
            }
        }
    }

    private static string ConfigDirectory => Path.Combine(BaseConfigPath, "VeinWares SubtleByte", "Infamy");
    private static string SavePath => Path.Combine(ConfigDirectory, "playerInfamyLevel.json");
    private static string LegacySavePath => Path.Combine(BaseConfigPath, "VeinWares SubtleByte", "playerInfamyLevel.json");

    private static string ResolveBaseConfigPath()
    {
        if (!string.IsNullOrWhiteSpace(Paths.ConfigPath))
        {
            return NormalizePath(Paths.ConfigPath);
        }

        if (!string.IsNullOrWhiteSpace(Paths.BepInExRootPath))
        {
            var candidate = Path.Combine(Paths.BepInExRootPath, "config");
            ModLogger.Warn($"[InfamyPersistence] Paths.ConfigPath was empty; using BepInEx root fallback: {candidate}");
            return NormalizePath(candidate);
        }

        var baseDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            var candidate = Path.Combine(baseDirectory, "BepInEx", "config");
            ModLogger.Warn($"[InfamyPersistence] Paths.ConfigPath was empty; using application base directory fallback: {candidate}");
            return NormalizePath(candidate);
        }

        var processDirectory = Environment.CurrentDirectory;
        var fallback = Path.Combine(processDirectory, "BepInEx", "config");
        ModLogger.Warn($"[InfamyPersistence] Falling back to process directory for config path: {fallback}");
        return NormalizePath(fallback);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    public static Dictionary<ulong, PlayerHateData> Load()
    {
        try
        {
            var loadPath = GetLoadPath();
            if (loadPath is null)
            {
                EnsureSaveFileExists();
                return new Dictionary<ulong, PlayerHateData>();
            }

            var json = File.ReadAllText(loadPath);
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

    private static string? GetLoadPath()
    {
        if (File.Exists(SavePath))
        {
            return SavePath;
        }

        if (!File.Exists(LegacySavePath))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            File.Move(LegacySavePath, SavePath);
            ModLogger.Info("[InfamyPersistence] Migrated hate data to Infamy directory");
            return SavePath;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"[InfamyPersistence] Failed to migrate hate data: {ex.Message}");
            return LegacySavePath;
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

    private static void EnsureSaveFileExists()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (File.Exists(SavePath))
            {
                return;
            }

            var emptyPayload = JsonSerializer.Serialize(
                new Dictionary<string, PlayerHateRecord>(),
                Options);

            File.WriteAllText(SavePath, emptyPayload);
            ModLogger.Info("[InfamyPersistence] Created new hate data save file");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"[InfamyPersistence] Failed to create hate data save file: {ex.Message}");
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

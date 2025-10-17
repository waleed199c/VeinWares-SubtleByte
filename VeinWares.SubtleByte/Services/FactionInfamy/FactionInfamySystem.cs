using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Models.FactionInfamy;

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamySystem
{
    private static readonly ConcurrentDictionary<ulong, PlayerHateData> PlayerHate = new();
    private static ManualLogSource? _log;
    private static FactionInfamyConfigSnapshot _config;
    private static bool _initialized;
    private static bool _dirty;

    public static bool Enabled => _initialized;

    public static int AutosaveBackupCount { get; private set; }

    public static void Initialize(FactionInfamyConfigSnapshot config, ManualLogSource log)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _log = log;
        _config = config;
        AutosaveBackupCount = config.AutosaveBackupCount;

        PlayerHate.Clear();
        var loaded = FactionInfamyPersistence.Load();
        foreach (var pair in loaded)
        {
            PlayerHate[pair.Key] = pair.Value;
            FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(pair.Key, pair.Value));
        }

        _dirty = false;
        _initialized = true;
        _log.LogInfo("[Infamy] Faction Infamy system initialised.");
    }

    public static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        FlushPersistence();
        PlayerHate.Clear();
        _initialized = false;
        _log?.LogInfo("[Infamy] Faction Infamy system shut down.");
    }

    public static void Tick(float deltaTime)
    {
        if (!_initialized || deltaTime <= 0f)
        {
            return;
        }

        if (_config.HateDecayPerSecond <= 0f)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var removalThreshold = 0.01f;

        var playersToClear = new List<ulong>();

        foreach (var pair in PlayerHate)
        {
            var data = pair.Value;
            if (!IsEligibleForCooldown(data, now))
            {
                continue;
            }

            if (data.RunCooldown(_config.HateDecayPerSecond, deltaTime, removalThreshold))
            {
                _dirty = true;
                if (data.FactionHate.Count == 0)
                {
                    playersToClear.Add(pair.Key);
                }
                else
                {
                    FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(pair.Key, data));
                }
            }
        }

        for (var i = 0; i < playersToClear.Count; i++)
        {
            var steamId = playersToClear[i];
            if (PlayerHate.TryRemove(steamId, out _))
            {
                FactionInfamyRuntime.NotifyPlayerHateCleared(steamId);
            }
        }
    }

    public static void RegisterHateGain(ulong steamId, string factionId, float baseHate)
    {
        if (!_initialized || steamId == 0UL || string.IsNullOrWhiteSpace(factionId) || baseHate <= 0f)
        {
            return;
        }

        var adjusted = baseHate * _config.HateGainMultiplier;
        var data = PlayerHate.GetOrAdd(steamId, static _ => new PlayerHateData());
        var entry = data.GetHate(factionId);
        var newHate = Math.Clamp(entry.Hate + adjusted, 0f, _config.MaximumHate);
        entry.Hate = newHate;
        entry.LastUpdated = DateTime.UtcNow;
        data.SetHate(factionId, entry);
        _dirty = true;
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
    }

    public static void RegisterHateGain(IEnumerable<ulong> steamIds, string factionId, float baseHate)
    {
        if (steamIds is null)
        {
            throw new ArgumentNullException(nameof(steamIds));
        }

        foreach (var steamId in steamIds)
        {
            RegisterHateGain(steamId, factionId, baseHate);
        }
    }

    public static void RegisterDeath(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        if (PlayerHate.TryRemove(steamId, out _))
        {
            _dirty = true;
            FactionInfamyRuntime.NotifyPlayerHateCleared(steamId);
        }
    }

    public static void RegisterCombatStart(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        var data = PlayerHate.GetOrAdd(steamId, static _ => new PlayerHateData());
        data.LastCombatStart = DateTime.UtcNow;
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
    }

    public static void RegisterCombatEnd(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        if (!PlayerHate.TryGetValue(steamId, out var data))
        {
            return;
        }

        data.LastCombatEnd = DateTime.UtcNow;
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
    }

    public static void RegisterAmbush(ulong steamId, string factionId)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(factionId))
        {
            return;
        }

        var data = PlayerHate.GetOrAdd(steamId, static _ => new PlayerHateData());
        var entry = data.GetHate(factionId);
        entry.LastAmbush = DateTime.UtcNow;
        data.SetHate(factionId, entry);
        _dirty = true;
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
    }

    public static bool TryGetPlayerHate(ulong steamId, out FactionInfamyPlayerSnapshot snapshot)
    {
        if (!_initialized)
        {
            snapshot = new FactionInfamyPlayerSnapshot(steamId, new Dictionary<string, HateEntry>(), DateTime.MinValue, DateTime.MinValue);
            return false;
        }

        if (PlayerHate.TryGetValue(steamId, out var data))
        {
            snapshot = CreateSnapshot(steamId, data);
            return true;
        }

        snapshot = new FactionInfamyPlayerSnapshot(steamId, new Dictionary<string, HateEntry>(), DateTime.MinValue, DateTime.MinValue);
        return false;
    }

    public static void ClearPlayerHate(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        if (PlayerHate.TryRemove(steamId, out _))
        {
            _dirty = true;
            FactionInfamyRuntime.NotifyPlayerHateCleared(steamId);
        }
    }

    public static void FlushPersistence()
    {
        if (!_initialized || !_dirty)
        {
            return;
        }

        try
        {
            var snapshot = CreateSerializableSnapshot();
            FactionInfamyPersistence.Save(snapshot, AutosaveBackupCount);
            _dirty = false;
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Infamy] Failed to flush infamy persistence: {ex.Message}");
        }
    }

    private static bool IsEligibleForCooldown(PlayerHateData data, DateTime now)
    {
        if (data.LastCombatEnd == DateTime.MinValue)
        {
            return true;
        }

        var elapsed = now - data.LastCombatEnd;
        return elapsed >= _config.CooldownGrace;
    }

    private static Dictionary<string, PlayerHateRecord> CreateSerializableSnapshot()
    {
        return PlayerHate
            .Where(static pair => pair.Value.FactionHate.Count > 0)
            .ToDictionary(
                static pair => pair.Key.ToString(),
                static pair => new PlayerHateRecord
                {
                    LastCombatStart = pair.Value.LastCombatStart,
                    LastCombatEnd = pair.Value.LastCombatEnd,
                    Factions = pair.Value.ExportSnapshot()
                        .ToDictionary(
                            static faction => faction.Key,
                            static faction => new HateEntryRecord
                            {
                                Hate = faction.Value.Hate,
                                LastAmbush = faction.Value.LastAmbush,
                                LastUpdated = faction.Value.LastUpdated
                            })
                });
    }

    private static FactionInfamyPlayerSnapshot CreateSnapshot(ulong steamId, PlayerHateData data)
    {
        return new FactionInfamyPlayerSnapshot(steamId, data.ExportSnapshot(), data.LastCombatStart, data.LastCombatEnd);
    }
}

internal readonly record struct FactionInfamyPlayerSnapshot(
    ulong SteamId,
    IReadOnlyDictionary<string, HateEntry> HateByFaction,
    DateTime LastCombatStart,
    DateTime LastCombatEnd);

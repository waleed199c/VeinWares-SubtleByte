using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Models.Wanted;

namespace VeinWares.SubtleByte.Services.Wanted;

internal static class WantedSystem
{
    private static readonly ConcurrentDictionary<ulong, PlayerHeatData> PlayerHeat = new();
    private static ManualLogSource? _log;
    private static WantedConfigSnapshot _config;
    private static bool _initialized;
    private static bool _dirty;

    public static bool Enabled => _initialized;

    public static int AutosaveBackupCount { get; private set; }

    public static void Initialize(WantedConfigSnapshot config, ManualLogSource log)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _log = log;
        _config = config;
        AutosaveBackupCount = config.AutosaveBackupCount;

        PlayerHeat.Clear();
        var loaded = WantedPersistence.Load();
        foreach (var pair in loaded)
        {
            PlayerHeat[pair.Key] = pair.Value;
        }

        _dirty = false;
        _initialized = true;
        _log.LogInfo("[Wanted] Wanted system initialised.");
    }

    public static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        FlushPersistence();
        PlayerHeat.Clear();
        _initialized = false;
        _log?.LogInfo("[Wanted] Wanted system shut down.");
    }

    public static void Tick(float deltaTime)
    {
        if (!_initialized || deltaTime <= 0f)
        {
            return;
        }

        if (_config.HeatDecayPerSecond <= 0f)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var removalThreshold = 0.01f;

        foreach (var pair in PlayerHeat)
        {
            var data = pair.Value;
            if (!IsEligibleForCooldown(data, now))
            {
                continue;
            }

            if (data.RunCooldown(_config.HeatDecayPerSecond, deltaTime, removalThreshold))
            {
                _dirty = true;
            }
        }
    }

    public static void RegisterHeatGain(ulong steamId, string factionId, float baseHeat)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(factionId) || baseHeat <= 0f)
        {
            return;
        }

        var adjusted = baseHeat * _config.HeatGainMultiplier;
        var data = PlayerHeat.GetOrAdd(steamId, static _ => new PlayerHeatData());
        var entry = data.GetHeat(factionId);
        var newHeat = MathF.Clamp(entry.Heat + adjusted, 0f, _config.MaximumHeat);
        entry.Heat = newHeat;
        entry.LastUpdated = DateTime.UtcNow;
        data.SetHeat(factionId, entry);
        _dirty = true;
    }

    public static void RegisterDeath(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        if (PlayerHeat.TryRemove(steamId, out _))
        {
            _dirty = true;
        }
    }

    public static void RegisterCombatStart(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        var data = PlayerHeat.GetOrAdd(steamId, static _ => new PlayerHeatData());
        data.LastCombatStart = DateTime.UtcNow;
    }

    public static void RegisterCombatEnd(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        if (!PlayerHeat.TryGetValue(steamId, out var data))
        {
            return;
        }

        data.LastCombatEnd = DateTime.UtcNow;
    }

    public static void RegisterAmbush(ulong steamId, string factionId)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(factionId))
        {
            return;
        }

        var data = PlayerHeat.GetOrAdd(steamId, static _ => new PlayerHeatData());
        var entry = data.GetHeat(factionId);
        entry.LastAmbush = DateTime.UtcNow;
        data.SetHeat(factionId, entry);
        _dirty = true;
    }

    public static bool TryGetPlayerHeat(ulong steamId, out WantedPlayerSnapshot snapshot)
    {
        if (!_initialized)
        {
            snapshot = new WantedPlayerSnapshot(steamId, new Dictionary<string, HeatEntry>(), DateTime.MinValue, DateTime.MinValue);
            return false;
        }

        if (PlayerHeat.TryGetValue(steamId, out var data))
        {
            snapshot = new WantedPlayerSnapshot(steamId, data.ExportSnapshot(), data.LastCombatStart, data.LastCombatEnd);
            return true;
        }

        snapshot = new WantedPlayerSnapshot(steamId, new Dictionary<string, HeatEntry>(), DateTime.MinValue, DateTime.MinValue);
        return false;
    }

    public static void ClearPlayerHeat(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        if (PlayerHeat.TryRemove(steamId, out _))
        {
            _dirty = true;
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
            WantedPersistence.Save(snapshot, AutosaveBackupCount);
            _dirty = false;
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Wanted] Failed to flush wanted persistence: {ex.Message}");
        }
    }

    private static bool IsEligibleForCooldown(PlayerHeatData data, DateTime now)
    {
        if (data.LastCombatEnd == DateTime.MinValue)
        {
            return true;
        }

        var elapsed = now - data.LastCombatEnd;
        return elapsed >= _config.CooldownGrace;
    }

    private static Dictionary<string, PlayerHeatRecord> CreateSerializableSnapshot()
    {
        return PlayerHeat
            .Where(static pair => pair.Value.FactionHeat.Count > 0)
            .ToDictionary(
                static pair => pair.Key.ToString(),
                static pair => new PlayerHeatRecord
                {
                    LastCombatStart = pair.Value.LastCombatStart,
                    LastCombatEnd = pair.Value.LastCombatEnd,
                    Factions = pair.Value.ExportSnapshot()
                        .ToDictionary(
                            static faction => faction.Key,
                            static faction => new HeatEntryRecord
                            {
                                Heat = faction.Value.Heat,
                                LastAmbush = faction.Value.LastAmbush,
                                LastUpdated = faction.Value.LastUpdated
                            })
                });
    }
}

internal readonly record struct WantedPlayerSnapshot(
    ulong SteamId,
    IReadOnlyDictionary<string, HeatEntry> HeatByFaction,
    DateTime LastCombatStart,
    DateTime LastCombatEnd);

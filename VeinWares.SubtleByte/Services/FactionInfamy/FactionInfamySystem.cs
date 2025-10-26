using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Models.FactionInfamy;
using VeinWares.SubtleByte.Utilities;

#nullable enable

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamySystem
{
    private static readonly ConcurrentDictionary<ulong, PlayerHateData> PlayerHate = new();
    private static ManualLogSource? _log;
    private static FactionInfamyConfigSnapshot _config = null!;
    private static bool _initialized;
    private static bool _dirty;
    private static TimeSpan _autosaveAccumulator;
    private static TimeSpan _autosaveInterval;
    private static int _autosaveBackupCount;
    private static TimeSpan _combatCooldown;
    private static TimeSpan _ambushCooldown;
    private static TimeSpan _ambushLifetime;
    private static int _ambushChancePercent;
    private static float _minimumAmbushHate;
    private static float _maximumHate;
    private static bool _enableAmbushVisualBuffs;
    private static bool _enableHalloweenAmbush;
    private static bool _ambushesRespectTerritory;
    private static bool _disableBloodConsumeOnSpawn;
    private static bool _disableCharmOnSpawn;
    private static bool _enableNativeDropTables;
    private static float _prestigeLevelBonusPerTier;
    private static float _prestigeEliteMultiplier;
    private static int _halloweenScarecrowMinimum;
    private static int _halloweenScarecrowMaximum;
    private static int _halloweenScarecrowRareMultiplier;
    private static int _halloweenScarecrowRareChancePercent;
    private static int _seasonalFollowUpChancePercent;
    private static int _seasonalFollowUpMinimum;
    private static int _seasonalFollowUpMaximum;
    private static bool _enableEliteAmbush;
    private static bool _enableAmbushKnockbackResistance;
    private static float _eliteHealthMultiplier;
    private static float _eliteDamageReductionMultiplier;
    private static float _eliteResistanceMultiplier;
    private static float _elitePowerMultiplier;
    private static float _eliteAttackSpeedMultiplier;
    private static float _eliteSpellSpeedMultiplier;
    private static float _eliteMoveSpeedMultiplier;
    private static float _eliteKnockbackResistanceMultiplier;
    private static float _eliteRepresentativeHealthRatio;
    private static float _eliteRepresentativeHealthAdditive;
    private static float _eliteRepresentativeDamageReductionRatio;
    private static float _eliteRepresentativeDamageReductionAdditive;
    private static float _eliteRepresentativeResistanceRatio;
    private static float _eliteRepresentativeResistanceAdditive;
    private static float _eliteRepresentativePowerRatio;
    private static float _eliteRepresentativePowerAdditive;
    private static float _eliteRepresentativeAttackSpeedRatio;
    private static float _eliteRepresentativeAttackSpeedAdditive;
    private static float _eliteRepresentativeSpellSpeedRatio;
    private static float _eliteRepresentativeSpellSpeedAdditive;
    private static float _eliteRepresentativeMoveSpeedRatio;
    private static float _eliteRepresentativeMoveSpeedAdditive;
    private static float _eliteRepresentativeKnockbackResistanceRatio;
    private static float _eliteRepresentativeKnockbackResistanceAdditive;

    public static bool Enabled => _initialized;

    internal static int AmbushChancePercent => _ambushChancePercent;

    internal static TimeSpan AmbushLifetime => _ambushLifetime;

    internal static float MinimumAmbushHateThreshold => _minimumAmbushHate;

    internal static TimeSpan AmbushCooldown => _ambushCooldown;

    internal static float MaximumHate => _maximumHate;

    internal static bool AmbushVisualBuffsEnabled => _enableEliteAmbush && _enableAmbushVisualBuffs;

    internal static bool ShouldApplyAmbushVisualBuffs(bool isElite)
    {
        return AmbushVisualBuffsEnabled && isElite;
    }

    internal static bool HalloweenAmbushEnabled => _enableHalloweenAmbush;

    internal static bool SuppressBloodConsumeOnSpawn => _disableBloodConsumeOnSpawn;

    internal static bool SuppressCharmOnSpawn => _disableCharmOnSpawn;

    internal static bool NativeDropTablesEnabled => _enableNativeDropTables;

    internal static bool AmbushTerritoryProtectionEnabled => _ambushesRespectTerritory;

    internal static float PrestigeLevelBonusPerTier => _prestigeLevelBonusPerTier;

    internal static float PrestigeEliteMultiplier => _prestigeEliteMultiplier;

    internal static int HalloweenScarecrowMinimum => _halloweenScarecrowMinimum;

    internal static int HalloweenScarecrowMaximum => _halloweenScarecrowMaximum;

    internal static int HalloweenScarecrowRareMultiplier => _halloweenScarecrowRareMultiplier;

    internal static int HalloweenScarecrowRareChancePercent => _halloweenScarecrowRareChancePercent;

    internal static int SeasonalFollowUpChancePercent => _seasonalFollowUpChancePercent;

    internal static int SeasonalFollowUpMinimum => _seasonalFollowUpMinimum;

    internal static int SeasonalFollowUpMaximum => _seasonalFollowUpMaximum;

    internal static bool EliteAmbushEnabled => _enableEliteAmbush;

    internal static bool AmbushKnockbackResistanceEnabled => _enableAmbushKnockbackResistance;

    internal static float EliteHealthMultiplier => _eliteHealthMultiplier;

    internal static float EliteDamageReductionMultiplier => _eliteDamageReductionMultiplier;

    internal static float EliteResistanceMultiplier => _eliteResistanceMultiplier;

    internal static float ElitePowerMultiplier => _elitePowerMultiplier;

    internal static float EliteAttackSpeedMultiplier => _eliteAttackSpeedMultiplier;

    internal static float EliteSpellSpeedMultiplier => _eliteSpellSpeedMultiplier;

    internal static float EliteMoveSpeedMultiplier => _eliteMoveSpeedMultiplier;

    internal static float EliteKnockbackResistanceMultiplier => _eliteKnockbackResistanceMultiplier;

    internal static float EliteRepresentativeHealthRatio => _eliteRepresentativeHealthRatio;

    internal static float EliteRepresentativeHealthAdditive => _eliteRepresentativeHealthAdditive;

    internal static float EliteRepresentativeDamageReductionRatio => _eliteRepresentativeDamageReductionRatio;

    internal static float EliteRepresentativeDamageReductionAdditive => _eliteRepresentativeDamageReductionAdditive;

    internal static float EliteRepresentativeResistanceRatio => _eliteRepresentativeResistanceRatio;

    internal static float EliteRepresentativeResistanceAdditive => _eliteRepresentativeResistanceAdditive;

    internal static float EliteRepresentativePowerRatio => _eliteRepresentativePowerRatio;

    internal static float EliteRepresentativePowerAdditive => _eliteRepresentativePowerAdditive;

    internal static float EliteRepresentativeAttackSpeedRatio => _eliteRepresentativeAttackSpeedRatio;

    internal static float EliteRepresentativeAttackSpeedAdditive => _eliteRepresentativeAttackSpeedAdditive;

    internal static float EliteRepresentativeSpellSpeedRatio => _eliteRepresentativeSpellSpeedRatio;

    internal static float EliteRepresentativeSpellSpeedAdditive => _eliteRepresentativeSpellSpeedAdditive;

    internal static float EliteRepresentativeMoveSpeedRatio => _eliteRepresentativeMoveSpeedRatio;

    internal static float EliteRepresentativeMoveSpeedAdditive => _eliteRepresentativeMoveSpeedAdditive;

    internal static float EliteRepresentativeKnockbackResistanceRatio => _eliteRepresentativeKnockbackResistanceRatio;

    internal static float EliteRepresentativeKnockbackResistanceAdditive => _eliteRepresentativeKnockbackResistanceAdditive;

    public static void Initialize(FactionInfamyConfigSnapshot config, ManualLogSource log)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _log = log;
        _config = config;


        var core = config.Core;
        var ambush = config.Ambush;
        var seasonal = config.Seasonal;
        var prestige = config.Prestige;
        var elite = config.Elite;
        var squad = elite.Squad;
        var representative = elite.Representative;

        _combatCooldown = core.CombatCooldown;
        _ambushCooldown = ambush.Cooldown;
        _ambushChancePercent = ambush.ChancePercent;
        _ambushLifetime = ambush.Lifetime;
        _minimumAmbushHate = core.MinimumAmbushHate;
        _maximumHate = core.MaximumHate;
        _enableAmbushVisualBuffs = ambush.VisualBuffsEnabled;
        _enableHalloweenAmbush = seasonal.EnableHalloween;
        _ambushesRespectTerritory = ambush.RespectTerritory;
        _disableBloodConsumeOnSpawn = ambush.DisableBloodConsumeOnSpawn;
        _disableCharmOnSpawn = ambush.DisableCharmOnSpawn;
        _enableNativeDropTables = ambush.EnableNativeDropTables;
        _prestigeLevelBonusPerTier = prestige.LevelBonusPerTier;
        _prestigeEliteMultiplier = prestige.EliteMultiplier;
        _halloweenScarecrowMinimum = seasonal.ScarecrowMinimum;
        _halloweenScarecrowMaximum = seasonal.ScarecrowMaximum;
        _halloweenScarecrowRareMultiplier = seasonal.ScarecrowRareMultiplier;
        _halloweenScarecrowRareChancePercent = seasonal.ScarecrowRareChancePercent;
        _seasonalFollowUpChancePercent = seasonal.FollowUpChancePercent;
        _seasonalFollowUpMinimum = seasonal.FollowUpMinimum;
        _seasonalFollowUpMaximum = seasonal.FollowUpMaximum;
        _enableEliteAmbush = elite.Enabled;
        _enableAmbushKnockbackResistance = elite.ApplyKnockbackResistance;
        _eliteHealthMultiplier = squad.HealthMultiplier;
        _eliteDamageReductionMultiplier = squad.DamageReductionMultiplier;
        _eliteResistanceMultiplier = squad.ResistanceMultiplier;
        _elitePowerMultiplier = squad.PowerMultiplier;
        _eliteAttackSpeedMultiplier = squad.AttackSpeedMultiplier;
        _eliteSpellSpeedMultiplier = squad.SpellSpeedMultiplier;
        _eliteMoveSpeedMultiplier = squad.MoveSpeedMultiplier;
        _eliteKnockbackResistanceMultiplier = squad.KnockbackResistanceMultiplier;
        _eliteRepresentativeHealthRatio = representative.HealthRatio;
        _eliteRepresentativeHealthAdditive = representative.HealthBonus;
        _eliteRepresentativeDamageReductionRatio = representative.DamageReductionRatio;
        _eliteRepresentativeDamageReductionAdditive = representative.DamageReductionBonus;
        _eliteRepresentativeResistanceRatio = representative.ResistanceRatio;
        _eliteRepresentativeResistanceAdditive = representative.ResistanceBonus;
        _eliteRepresentativePowerRatio = representative.PowerRatio;
        _eliteRepresentativePowerAdditive = representative.PowerBonus;
        _eliteRepresentativeAttackSpeedRatio = representative.AttackSpeedRatio;
        _eliteRepresentativeAttackSpeedAdditive = representative.AttackSpeedBonus;
        _eliteRepresentativeSpellSpeedRatio = representative.SpellSpeedRatio;
        _eliteRepresentativeSpellSpeedAdditive = representative.SpellSpeedBonus;
        _eliteRepresentativeMoveSpeedRatio = representative.MoveSpeedRatio;
        _eliteRepresentativeMoveSpeedAdditive = representative.MoveSpeedBonus;
        _eliteRepresentativeKnockbackResistanceRatio = representative.KnockbackResistanceRatio;
        _eliteRepresentativeKnockbackResistanceAdditive = representative.KnockbackResistanceBonus;

        LogConfigurationSummary(core, ambush, seasonal, elite, config.Persistence);

        _autosaveBackupCount = config.Persistence.AutosaveBackupCount;
        _autosaveInterval = config.Persistence.AutosaveInterval;
        _autosaveAccumulator = TimeSpan.Zero;

        PlayerHate.Clear();
        var loaded = FactionInfamyPersistence.Load();
        foreach (var pair in loaded)
        {
            var data = pair.Value ?? new PlayerHateData();
            if (SanitizeLoadedHate(data))
            {
                MarkDirty();
            }

            PlayerHate[pair.Key] = data;
            FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(pair.Key, data));
        }

        _dirty = false;
        _initialized = true;
        _log.LogInfo("[Infamy] Faction Infamy system initialised.");
    }

    private static void LogConfigurationSummary(
        InfamyCoreSettings core,
        AmbushSettings ambush,
        SeasonalSettings seasonal,
        EliteSettings elite,
        PersistenceSettings persistence)
    {
        if (_log is null)
        {
            return;
        }

        var autosaveSummary = $"{persistence.AutosaveInterval.TotalMinutes:0.#}m/{persistence.AutosaveBackupCount}";
        var seasonalState = seasonal.EnableHalloween ? "Enabled" : "Disabled";
        var eliteState = elite.Enabled ? "Enabled" : "Disabled";
        var knockbackState = elite.ApplyKnockbackResistance ? "On" : "Off";
        var visualState = ambush.VisualBuffsEnabled ? "On" : "Off";

        _log.LogInfo(
            $"[Infamy] Config → HateGain={core.HateGainMultiplier:0.##}, Decay/s={core.HateDecayPerSecond:0.##}, " +
            $"AmbushChance={ambush.ChancePercent}%, Cooldown={ambush.Cooldown.TotalMinutes:0.#}m, " +
            $"Lifetime={ambush.Lifetime.TotalSeconds:0.#}s, VisualBuffs={visualState}, Autosave={autosaveSummary}, " +
            $"Seasonal={seasonalState}, Elite={eliteState} (Knockback {knockbackState}).");
    }

    private static bool SanitizeLoadedHate(PlayerHateData data)
    {
        if (data is null)
        {
            return false;
        }

        var updates = new List<KeyValuePair<string, HateEntry>>();
        var changed = false;

        foreach (var pair in data.FactionHate)
        {
            var entry = pair.Value;
            var clamped = Math.Clamp(entry.Hate, 0f, _maximumHate);
            if (Math.Abs(clamped - entry.Hate) > 0.001f)
            {
                entry.Hate = clamped;
                changed = true;
            }

            var tier = FactionInfamyTierHelper.CalculateTier(clamped, _maximumHate);
            if (tier != entry.LastAnnouncedTier)
            {
                entry.LastAnnouncedTier = tier;
                changed = true;
            }

            updates.Add(new KeyValuePair<string, HateEntry>(pair.Key, entry));
        }

        for (var i = 0; i < updates.Count; i++)
        {
            var update = updates[i];
            data.SetHate(update.Key, update.Value);
        }

        return changed;
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

        if (_config.Core.HateDecayPerSecond <= 0f)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var removalThreshold = 0.01f;

        var playersToClear = new List<ulong>();

        foreach (var pair in PlayerHate)
        {
            var data = pair.Value;
            var stateChanged = false;

            if (data.InCombat && data.LastCombatStart != DateTime.MinValue)
            {
                var sinceCombat = now - data.LastCombatStart;
                if (sinceCombat >= _combatCooldown)
                {
                    data.InCombat = false;
                    data.LastCombatEnd = now;
                    stateChanged = true;
                }
            }

            if (!IsEligibleForCooldown(data, now))
            {
                if (stateChanged)
                {
                    MarkDirty();
                    FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(pair.Key, data));
                }

                continue;
            }

            if (data.RunCooldown(_config.Core.HateDecayPerSecond, deltaTime, removalThreshold, _maximumHate))
            {
                MarkDirty();
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

        if (_dirty && _autosaveInterval > TimeSpan.Zero)
        {
            _autosaveAccumulator += TimeSpan.FromSeconds(deltaTime);
            if (_autosaveAccumulator >= _autosaveInterval)
            {
                FlushPersistence();
            }
        }
    }

    public static void RegisterHateGain(ulong steamId, string factionId, float baseHate)
    {
        if (!_initialized || steamId == 0UL || string.IsNullOrWhiteSpace(factionId) || baseHate <= 0f)
        {
            return;
        }

        var adjusted = baseHate * _config.Core.HateGainMultiplier;
        var data = PlayerHate.GetOrAdd(steamId, static _ => new PlayerHateData());
        var entry = data.GetHate(factionId);
        var newHate = Math.Clamp(entry.Hate + adjusted, 0f, _maximumHate);
        var newTier = FactionInfamyTierHelper.CalculateTier(newHate, _maximumHate);
        var previousTier = entry.LastAnnouncedTier;
        entry.Hate = newHate;
        entry.LastUpdated = DateTime.UtcNow;
        if (newTier > previousTier)
        {
            var message = FactionInfamyChatConfig.GetTierAnnouncement(factionId, newTier, newHate);
            if (!string.IsNullOrWhiteSpace(message))
            {
                ChatHelper.TrySendSystemMessage(steamId, message);
            }
        }

        entry.LastAnnouncedTier = newTier;
        data.SetHate(factionId, entry);
        MarkDirty();
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

    public static void ReduceHate(ulong steamId, string factionId, float amount)
    {
        if (!_initialized || steamId == 0UL || string.IsNullOrWhiteSpace(factionId) || amount <= 0f)
        {
            return;
        }

        if (!PlayerHate.TryGetValue(steamId, out var data))
        {
            return;
        }

        if (!data.TryGetHate(factionId, out var entry))
        {
            return;
        }

        var newHate = Math.Max(0f, entry.Hate - amount);
        var now = DateTime.UtcNow;
        entry.Hate = newHate;
        entry.LastUpdated = now;
        entry.LastAnnouncedTier = FactionInfamyTierHelper.CalculateTier(newHate, _maximumHate);

        if (newHate <= 0.01f)
        {
            if (data.ClearFaction(factionId) && data.FactionHate.Count == 0)
            {
                if (PlayerHate.TryRemove(steamId, out _))
                {
                    MarkDirty();
                    FactionInfamyRuntime.NotifyPlayerHateCleared(steamId);
                    return;
                }
            }
        }
        else
        {
            data.SetHate(factionId, entry);
        }

        MarkDirty();
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
    }

    public static void RegisterDeath(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        if (PlayerHate.TryRemove(steamId, out _))
        {
            MarkDirty();
            FactionInfamyRuntime.NotifyPlayerHateCleared(steamId);
        }
    }

    public static void RegisterCombatStart(ulong steamId)
    {
        if (!_initialized || steamId == 0UL)
        {
            return;
        }

        var data = PlayerHate.GetOrAdd(steamId, static _ => new PlayerHateData());

        var now = DateTime.UtcNow;

        if (data.InCombat && data.LastCombatStart != DateTime.MinValue && now - data.LastCombatStart < _combatCooldown)
        {
            return;
        }

        data.InCombat = true;
        data.LastCombatStart = now;
        data.LastCombatEnd = DateTime.MinValue;
        MarkDirty();
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
    }

    public static void RegisterCombatEnd(ulong steamId)
    {
        if (!_initialized || steamId == 0UL)
        {
            return;
        }

        if (!PlayerHate.TryGetValue(steamId, out var data))
        {
            return;
        }

        if (!data.InCombat && data.LastCombatEnd != DateTime.MinValue)
        {
            return;
        }

        data.InCombat = false;
        data.LastCombatEnd = DateTime.UtcNow;
        MarkDirty();
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
    }

    public static void RegisterAmbush(ulong steamId, string factionId)
    {
        TryConsumeAmbush(steamId, factionId);
    }

    public static bool TryGetPlayerHate(ulong steamId, out FactionInfamyPlayerSnapshot snapshot)
    {
        if (!_initialized)
        {
            snapshot = new FactionInfamyPlayerSnapshot(steamId, new Dictionary<string, HateEntry>(), DateTime.MinValue, DateTime.MinValue, false);
            return false;
        }

        if (PlayerHate.TryGetValue(steamId, out var data))
        {
            snapshot = CreateSnapshot(steamId, data);
            return true;
        }

        snapshot = new FactionInfamyPlayerSnapshot(steamId, new Dictionary<string, HateEntry>(), DateTime.MinValue, DateTime.MinValue, false);
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
            MarkDirty();
            FactionInfamyRuntime.NotifyPlayerHateCleared(steamId);
        }
    }

    public static int ClearAllPlayerHate()
    {
        if (!_initialized || PlayerHate.IsEmpty)
        {
            return 0;
        }

        var keys = PlayerHate.Keys.ToArray();
        if (keys.Length == 0)
        {
            return 0;
        }

        var clearedIds = new List<ulong>(keys.Length);
        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            if (PlayerHate.TryRemove(key, out _))
            {
                clearedIds.Add(key);
            }
        }

        if (clearedIds.Count == 0)
        {
            return 0;
        }

        MarkDirty();

        for (var i = 0; i < clearedIds.Count; i++)
        {
            FactionInfamyRuntime.NotifyPlayerHateCleared(clearedIds[i]);
        }

        return clearedIds.Count;
    }

    public static IReadOnlyList<FactionInfamyPlayerSnapshot> GetAllPlayerHate()
    {
        if (!_initialized)
        {
            return Array.Empty<FactionInfamyPlayerSnapshot>();
        }

        return PlayerHate
            .Select(static pair => CreateSnapshot(pair.Key, pair.Value))
            .ToList();
    }

    public static IReadOnlyCollection<string> GetTrackedFactions()
    {
        if (!_initialized)
        {
            return Array.Empty<string>();
        }

        return PlayerHate
            .SelectMany(static pair => pair.Value.ExportSnapshot().Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetEligibleAmbushFactions(ulong steamId)
    {
        if (!_initialized || steamId == 0UL)
        {
            return Array.Empty<string>();
        }

        if (!PlayerHate.TryGetValue(steamId, out var data))
        {
            return Array.Empty<string>();
        }

        var now = DateTime.UtcNow;
        var result = new List<string>();

        foreach (var pair in data.FactionHate)
        {
            var entry = pair.Value;
            if (entry.Hate < _minimumAmbushHate)
            {
                continue;
            }

            if (entry.LastAmbush != DateTime.MinValue && now - entry.LastAmbush < _ambushCooldown)
            {
                continue;
            }

            result.Add(pair.Key);
        }

        return result;
    }

    public static bool TryGetHighestHateFaction(ulong steamId, out string factionId, out HateEntry entry)
    {
        factionId = string.Empty;
        entry = default;

        if (!_initialized || steamId == 0UL)
        {
            return false;
        }

        if (!PlayerHate.TryGetValue(steamId, out var data))
        {
            return false;
        }

        var found = false;
        var highest = float.MinValue;
        foreach (var pair in data.FactionHate)
        {
            if (pair.Value.Hate > highest)
            {
                highest = pair.Value.Hate;
                factionId = pair.Key;
                entry = pair.Value;
                found = true;
            }
        }

        return found;
    }

    public static bool TryConsumeAmbush(ulong steamId, string factionId)
    {
        if (!_initialized || steamId == 0UL || string.IsNullOrWhiteSpace(factionId))
        {
            return false;
        }

        if (!PlayerHate.TryGetValue(steamId, out var data))
        {
            return false;
        }

        if (!data.TryGetHate(factionId, out var entry))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (entry.Hate < _minimumAmbushHate)
        {
            return false;
        }

        if (entry.LastAmbush != DateTime.MinValue && now - entry.LastAmbush < _ambushCooldown)
        {
            return false;
        }

        entry.LastAmbush = now;
        data.SetHate(factionId, entry);
        MarkDirty();
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
        return true;
    }

    public static void RollbackAmbushCooldown(ulong steamId, string factionId, DateTime previousTimestamp)
    {
        if (!_initialized || steamId == 0UL || string.IsNullOrWhiteSpace(factionId))
        {
            return;
        }

        if (!PlayerHate.TryGetValue(steamId, out var data))
        {
            return;
        }

        if (!data.TryGetHate(factionId, out var entry))
        {
            return;
        }

        if (entry.LastAmbush == previousTimestamp)
        {
            return;
        }

        entry.LastAmbush = previousTimestamp;
        data.SetHate(factionId, entry);
        MarkDirty();
        FactionInfamyRuntime.NotifyPlayerHateChanged(CreateSnapshot(steamId, data));
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
            FactionInfamyPersistence.Save(snapshot, _autosaveBackupCount);
            _dirty = false;
            _autosaveAccumulator = TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Infamy] Failed to flush infamy persistence: {ex.Message}");
        }
    }

    private static bool IsEligibleForCooldown(PlayerHateData data, DateTime now)
    {
        if (data.InCombat)
        {
            return false;
        }

        if (data.LastCombatEnd == DateTime.MinValue)
        {
            return true;
        }

        var elapsed = now - data.LastCombatEnd;
        return elapsed >= _config.Core.CooldownGrace;
    }

    private static void MarkDirty()
    {
        _dirty = true;
        _autosaveAccumulator = TimeSpan.Zero;
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
                                LastUpdated = faction.Value.LastUpdated,
                                LastAnnouncedTier = faction.Value.LastAnnouncedTier
                            })
                });
    }

    private static FactionInfamyPlayerSnapshot CreateSnapshot(ulong steamId, PlayerHateData data)
    {
        return new FactionInfamyPlayerSnapshot(steamId, data.ExportSnapshot(), data.LastCombatStart, data.LastCombatEnd, data.InCombat);
    }
}

internal readonly record struct FactionInfamyPlayerSnapshot(
    ulong SteamId,
    IReadOnlyDictionary<string, HateEntry> HateByFaction,
    DateTime LastCombatStart,
    DateTime LastCombatEnd,
    bool InCombat);

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

#nullable enable

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyAmbushService
{
    private const float HateReliefFraction = 0.25f;
    private const float MinimumReliefPerSquad = 10f;

    private static readonly ConcurrentDictionary<int, PendingAmbushSpawn> PendingSpawns = new();
    private static readonly ConcurrentDictionary<Entity, ActiveAmbush> ActiveAmbushes = new();
    private static readonly Dictionary<string, AmbushSquadDefinition> SquadDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bandits"] = new AmbushSquadDefinition(
            new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-1030822544), 2, -1, 1.5f, 8f), // Deadeye
                new AmbushUnitDefinition(new PrefabGUID(-301730941), 2, -2, 1f, 6f) // Thug
            }),
        ["Blackfangs"] = new AmbushSquadDefinition(
            new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1864177126), 2, 0, 1.5f, 7f), // Venomblade
                new AmbushUnitDefinition(new PrefabGUID(326501064), 1, 1, 2f, 9f) // Alchemist
            }),
        ["Militia"] = new AmbushSquadDefinition(
            new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1148936156), 3, -1, 2f, 10f), // Rifleman
                new AmbushUnitDefinition(new PrefabGUID(794228023), 1, 1, 1.5f, 6f) // Knight Shield
            }),
        ["Gloomrot"] = new AmbushSquadDefinition(
            new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-322293503), 2, 0, 3f, 10f), // Pyro
                new AmbushUnitDefinition(new PrefabGUID(1732477970), 1, 2, 4f, 12f) // Railgunner
            }),
        ["Legion"] = new AmbushSquadDefinition(
            new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1980594081), 2, 1, 2f, 9f), // Shadowkin
                new AmbushUnitDefinition(new PrefabGUID(-1009917656), 1, 3, 3f, 11f) // Nightmare
            }),
        ["Undead"] = new AmbushSquadDefinition(
            new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-1287507270), 3, -1, 1.5f, 7f), // Skeleton Mage
                new AmbushUnitDefinition(new PrefabGUID(-1365627158), 1, 1, 2f, 8f) // Assassin
            }),
        ["Werewolf"] = new AmbushSquadDefinition(
            new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-951976780), 3, 0, 1.5f, 8f) // Hostile villager werewolf
            })
    };

    private static ManualLogSource? _log;
    private static readonly System.Random Random = new();
    private static bool _initialized;
    private static int _lifetimeSequence;

    public static void Initialize(ManualLogSource log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _initialized = true;
    }

    public static void Shutdown()
    {
        _initialized = false;
        _log = null;
        PendingSpawns.Clear();
        ActiveAmbushes.Clear();
    }

    public static void TryTriggerAmbush(EntityManager entityManager, Entity playerEntity, ulong steamId)
    {
        if (!_initialized || !FactionInfamySystem.Enabled || steamId == 0UL)
        {
            return;
        }

        var chance = FactionInfamySystem.AmbushChancePercent;
        if (chance <= 0 || Random.Next(0, 100) >= chance)
        {
            return;
        }

        if (!FactionInfamySystem.TryGetPlayerHate(steamId, out var snapshot) || snapshot.HateByFaction.Count == 0)
        {
            return;
        }

        var eligible = FactionInfamySystem.GetEligibleAmbushFactions(steamId);
        if (eligible.Count == 0)
        {
            return;
        }

        var eligibleSet = new HashSet<string>(eligible, StringComparer.OrdinalIgnoreCase);
        var target = snapshot.HateByFaction
            .Where(pair => eligibleSet.Contains(pair.Key))
            .OrderByDescending(pair => pair.Value.Hate)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(target.Key))
        {
            return;
        }

        if (!TryResolvePlayerPosition(entityManager, playerEntity, out var position))
        {
            _log?.LogDebug($"[Infamy] Unable to resolve position for ambush target {steamId}.");
            return;
        }

        if (!FactionInfamySystem.TryConsumeAmbush(steamId, target.Key))
        {
            return;
        }

        var playerLevel = ResolvePlayerLevel(entityManager, playerEntity);
        var difficulty = EvaluateDifficulty(target.Value.Hate);
        if (!TrySpawnSquad(steamId, target.Key, playerLevel, position, target.Value.Hate, difficulty))
        {
            _log?.LogWarning($"[Infamy] Failed to spawn ambush squad for faction '{target.Key}'.");
            return;
        }

        NotifyAmbush(playerEntity, steamId, target.Key, target.Value.Hate, difficulty);
    }

    public static void TryHandleSpawnedEntity(EntityManager entityManager, Entity entity, float lifetime)
    {
        if (!_initialized)
        {
            return;
        }

        var key = BitConverter.SingleToInt32Bits(lifetime);
        if (!PendingSpawns.TryGetValue(key, out var pending))
        {
            return;
        }

        if (pending.Remaining <= 0)
        {
            PendingSpawns.TryRemove(key, out _);
            return;
        }

        if (entityManager.HasComponent<UnitLevel>(entity))
        {
            var unitLevel = entityManager.GetComponentData<UnitLevel>(entity);
            unitLevel.Level._Value = pending.UnitLevel;
            entityManager.SetComponentData(entity, unitLevel);
        }

        if (!entityManager.HasComponent<DestroyWhenDisabled>(entity))
        {
            entityManager.AddComponent<DestroyWhenDisabled>(entity);
        }

        if (entityManager.HasComponent<Minion>(entity))
        {
            entityManager.RemoveComponent<Minion>(entity);
        }

        ActiveAmbushes[entity] = new ActiveAmbush(pending.TargetSteamId, pending.FactionId, pending.HateReliefPerUnit);

        pending.Remaining--;
        if (pending.Remaining <= 0)
        {
            PendingSpawns.TryRemove(key, out _);
        }
    }

    public static bool TryHandleAmbushKill(Entity victim, ulong killerSteamId)
    {
        if (!_initialized)
        {
            return false;
        }

        if (!ActiveAmbushes.TryRemove(victim, out var active))
        {
            return false;
        }

        if (active.HateReliefPerKill > 0f)
        {
            FactionInfamySystem.ReduceHate(active.TargetSteamId, active.FactionId, active.HateReliefPerKill);
        }

        if (killerSteamId != 0UL)
        {
            FactionInfamySystem.RegisterCombatStart(killerSteamId);
        }

        return true;
    }

    public static void ClearForPlayer(ulong steamId)
    {
        if (steamId == 0UL || !_initialized)
        {
            return;
        }

        foreach (var pair in ActiveAmbushes.ToArray())
        {
            if (pair.Value.TargetSteamId == steamId)
            {
                ActiveAmbushes.TryRemove(pair.Key, out _);
            }
        }

        foreach (var pair in PendingSpawns.ToArray())
        {
            if (pair.Value.TargetSteamId == steamId)
            {
                PendingSpawns.TryRemove(pair.Key, out _);
            }
        }
    }

    private static bool TrySpawnSquad(ulong steamId, string factionId, int playerLevel, float3 position, float hateValue, AmbushDifficulty difficulty)
    {
        if (!SquadDefinitions.TryGetValue(factionId, out var squad))
        {
            return false;
        }

        var totalUnits = squad.TotalUnits;
        if (totalUnits <= 0)
        {
            return false;
        }

        var totalRelief = Math.Max(MinimumReliefPerSquad, hateValue * HateReliefFraction);
        var reliefPerUnit = totalRelief / totalUnits;

        foreach (var unit in squad.Units)
        {
            var count = Math.Max(1, unit.Count);
            var levelOffset = difficulty.LevelOffset + unit.LevelOffset;
            var targetLevel = Math.Clamp(playerLevel + levelOffset, 1, 999);
            var lifetimeSeconds = GetNextLifetimeSeconds();
            var encodedLifetime = FactionInfamySpawnUtility.EncodeLifetime(lifetimeSeconds, targetLevel, SpawnFaction.Default);

            var pending = new PendingAmbushSpawn(steamId, factionId, targetLevel, count, reliefPerUnit);
            var key = BitConverter.SingleToInt32Bits(encodedLifetime);
            PendingSpawns[key] = pending;

            try
            {
                FactionInfamySpawnUtility.SpawnUnit(unit.Prefab, position, count, unit.MinRange, unit.MaxRange, encodedLifetime);
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Infamy] Failed to spawn ambush unit {unit.Prefab.GuidHash} for faction '{factionId}': {ex.Message}");
                PendingSpawns.TryRemove(key, out _);
            }
        }

        _log?.LogInfo($"[Infamy] Spawned ambush squad for faction '{factionId}' targeting {steamId}.");
        return true;
    }

    private static void NotifyAmbush(Entity playerEntity, ulong steamId, string factionId, float hateValue, AmbushDifficulty difficulty)
    {
        var message = FactionInfamyChatConfig.GetAmbushMessage(factionId, difficulty.Tier, hateValue, difficulty.LevelOffset);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (playerEntity.Exists() && ChatHelper.TrySendSystemMessage(playerEntity, message))
        {
            return;
        }

        if (steamId != 0UL)
        {
            ChatHelper.TrySendSystemMessage(steamId, message);
        }
    }

    private static int GetNextLifetimeSeconds()
    {
        var baseSeconds = (int)Math.Clamp(FactionInfamySystem.AmbushLifetime.TotalSeconds, 10, 990);
        var sequence = System.Threading.Interlocked.Increment(ref _lifetimeSequence);
        var offset = (sequence % 30) * 3; // spread within a 90 second window
        var result = Math.Clamp(baseSeconds + offset, 10, 990);
        return result;
    }

    private static bool TryResolvePlayerPosition(EntityManager entityManager, Entity playerEntity, out float3 position)
    {
        position = float3.zero;
        if (entityManager.TryGetComponentData(playerEntity, out LocalTransform transform))
        {
            position = transform.Position;
            return true;
        }

        if (entityManager.TryGetComponentData(playerEntity, out LocalToWorld localToWorld))
        {
            position = localToWorld.Position;
            return true;
        }

        return false;
    }

    private static int ResolvePlayerLevel(EntityManager entityManager, Entity playerEntity)
    {
        if (entityManager.TryGetComponentData(playerEntity, out UnitLevel unitLevel))
        {
            return Math.Max(1, unitLevel.Level._Value);
        }

        return 1;
    }

    private static AmbushDifficulty EvaluateDifficulty(float hateValue)
    {
        var maximumHate = Math.Max(1f, FactionInfamySystem.MaximumHate);
        var normalized = Math.Clamp(hateValue / maximumHate, 0f, 1f);
        var bucket = 5 - (int)Math.Floor(normalized * 5.0);
        if (normalized >= 0.999f)
        {
            bucket = 1;
        }

        bucket = Math.Clamp(bucket, 1, 5);

        var offset = bucket switch
        {
            1 => 10,
            2 => 0,
            3 => 5,
            4 => -2,
            5 => -20,
            _ => 0
        };

        return new AmbushDifficulty(bucket, offset);
    }

    private sealed class PendingAmbushSpawn
    {
        public PendingAmbushSpawn(ulong targetSteamId, string factionId, int unitLevel, int remaining, float hateReliefPerUnit)
        {
            TargetSteamId = targetSteamId;
            FactionId = factionId;
            UnitLevel = unitLevel;
            Remaining = remaining;
            HateReliefPerUnit = hateReliefPerUnit;
        }

        public ulong TargetSteamId { get; }

        public string FactionId { get; }

        public int UnitLevel { get; }

        public int Remaining { get; set; }

        public float HateReliefPerUnit { get; }
    }

    private readonly struct ActiveAmbush
    {
        public ActiveAmbush(ulong targetSteamId, string factionId, float hateReliefPerKill)
        {
            TargetSteamId = targetSteamId;
            FactionId = factionId;
            HateReliefPerKill = hateReliefPerKill;
        }

        public ulong TargetSteamId { get; }

        public string FactionId { get; }

        public float HateReliefPerKill { get; }
    }

    private readonly struct AmbushUnitDefinition
    {
        public AmbushUnitDefinition(PrefabGUID prefab, int count, int levelOffset, float minRange, float maxRange)
        {
            Prefab = prefab;
            Count = count;
            LevelOffset = levelOffset;
            MinRange = minRange;
            MaxRange = maxRange;
        }

        public PrefabGUID Prefab { get; }

        public int Count { get; }

        public int LevelOffset { get; }

        public float MinRange { get; }

        public float MaxRange { get; }
    }

    private sealed class AmbushSquadDefinition
    {
        public AmbushSquadDefinition(IReadOnlyList<AmbushUnitDefinition> units)
        {
            Units = units;
            TotalUnits = units.Sum(u => Math.Max(1, u.Count));
        }

        public IReadOnlyList<AmbushUnitDefinition> Units { get; }

        public int TotalUnits { get; }
    }

    private readonly struct AmbushDifficulty
    {
        public AmbushDifficulty(int tier, int levelOffset)
        {
            Tier = tier;
            LevelOffset = levelOffset;
        }

        public int Tier { get; }

        public int LevelOffset { get; }
    }
}

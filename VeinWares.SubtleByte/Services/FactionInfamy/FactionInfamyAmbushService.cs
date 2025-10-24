using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services;
using VeinWares.SubtleByte.Utilities;

#nullable enable

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyAmbushService
{
    private const float HateReliefFraction = 0.25f;
    private const float MinimumReliefPerSquad = 10f;
    private const int MaxPositiveLevelOffset = 10;
    private const float PrestigeScalarEpsilon = 0.0001f;
    private const float PrestigeScalarClamp = 5f;

    private static readonly ConcurrentDictionary<int, PendingAmbushSpawn> PendingSpawns = new();
    private static readonly ConcurrentDictionary<Entity, ActiveAmbush> ActiveAmbushes = new();
    private static readonly ConcurrentDictionary<string, FactionTeamData> FactionTeamCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly PrefabGUID BlackfangSentinelPrefab = new(1531777139);
    private static readonly PrefabGUID LevelAuraSelfBuff = new(-2104035188);

    private static bool TryRollVisualBuff(string factionId, out PrefabGUID visualBuff)
    {
        if (!string.IsNullOrWhiteSpace(factionId)
            && FactionInfamyAmbushData.TryGetVisualPool(factionId, out var factionPool)
            && TryPickVisualBuff(factionPool, out visualBuff))
        {
            return true;
        }

        return TryPickVisualBuff(FactionInfamyAmbushData.GetDefaultVisualPool(), out visualBuff);
    }

    private static bool TryPickVisualBuff(IReadOnlyList<PrefabGUID> pool, out PrefabGUID visualBuff)
    {
        if (pool is null || pool.Count == 0)
        {
            visualBuff = default;
            return false;
        }

        var index = Random.Next(pool.Count);
        visualBuff = pool[index];
        return true;
    }

    private static PrefabGUID? ResolveSharedVisualBuff(
        string factionId,
        IEnumerable<AmbushSpawnRequest> requests,
        bool isElite)
    {
        if (!FactionInfamySystem.ShouldApplyAmbushVisualBuffs(isElite) || requests is null)
        {
            return null;
        }

        foreach (var request in requests)
        {
            if (request.VisualStrategy == VisualBuffStrategy.Shared
                && TryRollVisualBuff(factionId, out var visualBuff))
            {
                return visualBuff;
            }
        }

        return null;
    }

    private static float ResolvePrestigeScalar(int prestigeLevel, int difficultyTier, bool useEliteMultipliers)
    {
        if (prestigeLevel <= 0 || FactionInfamySystem.PrestigeLevelBonusPerTier <= 0f)
        {
            return 1f;
        }

        var tier = Math.Max(1, difficultyTier);
        var bonus = Math.Max(0, prestigeLevel) * FactionInfamySystem.PrestigeLevelBonusPerTier * tier;

        if (useEliteMultipliers)
        {
            bonus *= Math.Max(0f, FactionInfamySystem.PrestigeEliteMultiplier);
        }

        var scalar = 1f + bonus;
        return Math.Clamp(scalar, 1f, PrestigeScalarClamp);
    }

    private static AmbushStatMultipliers ResolveAmbushMultipliers(bool useEliteMultipliers, bool isRepresentative, float prestigeScalar)
    {
        var multipliers = useEliteMultipliers
            ? AmbushStatMultipliers.Create(isRepresentative)
            : AmbushStatMultipliers.Identity;

        return multipliers.Scale(prestigeScalar);
    }

    private static ManualLogSource? _log;
    private static readonly System.Random Random = new();
    private static bool _initialized;
    private static int _lifetimeSequence;

    public static bool HasPendingSpawns => PendingSpawns.Count > 0;

    public static void Initialize(ManualLogSource log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));

        FactionInfamyAmbushData.Initialize(log);
        FactionInfamyAmbushData.SquadDefinitionsChanged += OnSquadDefinitionsChanged;
        FactionInfamyAmbushData.LootDefinitionsChanged += OnLootDefinitionsChanged;

        _initialized = true;
    }

    public static void Shutdown()
    {
        FactionInfamyAmbushData.SquadDefinitionsChanged -= OnSquadDefinitionsChanged;
        FactionInfamyAmbushData.LootDefinitionsChanged -= OnLootDefinitionsChanged;
        FactionInfamyAmbushData.ClearLootPrefabCache();

        _initialized = false;
        _log = null;
        foreach (var pair in PendingSpawns.ToArray())
        {
            FactionInfamySpawnUtility.CancelSpawnCallback(pair.Key);
        }
        PendingSpawns.Clear();
        ActiveAmbushes.Clear();
        FactionTeamCache.Clear();
    }

    private static void OnSquadDefinitionsChanged()
    {
        FactionTeamCache.Clear();
        _log?.LogInfo("[Infamy] Reloaded ambush squad configuration.");
    }

    private static void OnLootDefinitionsChanged()
    {
        _log?.LogInfo("[Infamy] Reloaded ambush loot configuration.");
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

        if (FactionInfamySystem.AmbushTerritoryProtectionEnabled
            && TerritoryUtility.IsInsidePlayerTerritory(entityManager, playerEntity, steamId, position, out var territoryIndex))
        {
            _log?.LogDebug($"[Infamy] Blocked ambush for {steamId} inside owned territory (territory index {territoryIndex}).");
            return;
        }

        var previousAmbushTimestamp = target.Value.LastAmbush;

        if (!FactionInfamySystem.TryConsumeAmbush(steamId, target.Key))
        {
            return;
        }

        var playerLevel = ResolvePlayerLevel(entityManager, playerEntity, steamId);
        var difficulty = EvaluateDifficulty(target.Value.Hate);
        var prestigeLevel = 0;
        BloodcraftPrestigeReader.TryGetExperiencePrestige(steamId, out prestigeLevel);

        if (!TrySpawnSquad(steamId, target.Key, playerLevel, position, target.Value.Hate, difficulty, prestigeLevel))
        {
            _log?.LogWarning($"[Infamy] Failed to spawn ambush squad for faction '{target.Key}'.");
            FactionInfamySystem.RollbackAmbushCooldown(steamId, target.Key, previousAmbushTimestamp);
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

        if (ActiveAmbushes.ContainsKey(entity))
        {
            return;
        }

        var marker = (int)Math.Round(lifetime);
        if (!PendingSpawns.TryGetValue(marker, out var pending))
        {
            return;
        }

        var completed = FinalizeAmbushSpawn(entityManager, entity, marker, pending, pending.LifetimeSeconds, pending.Multipliers);
        if (completed)
        {
            FactionInfamySpawnUtility.CancelSpawnCallback(marker);
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
                FactionInfamySpawnUtility.CancelSpawnCallback(pair.Key);
            }
        }
    }

    private static bool TrySpawnSquad(ulong steamId, string factionId, int playerLevel, float3 position, float hateValue, AmbushDifficulty difficulty, int prestigeLevel)
    {
        if (!FactionInfamyAmbushData.TryGetSquadDefinition(factionId, out var squad))
        {
            return false;
        }

        var spawnPlan = BuildSpawnRequests(squad, difficulty);
        var spawnRequests = spawnPlan.CoreRequests;
        if (spawnRequests.Count == 0)
        {
            return false;
        }

        var totalUnits = spawnRequests.Sum(request => request.Count);
        if (spawnPlan.FollowUpRequests.Count > 0)
        {
            totalUnits += spawnPlan.FollowUpRequests.Sum(request => request.Count);
        }
        if (totalUnits <= 0)
        {
            return false;
        }

        var totalRelief = Math.Max(MinimumReliefPerSquad, hateValue * HateReliefFraction);
        var reliefPerUnit = totalRelief / totalUnits;
        var useEliteMultipliers = FactionInfamySystem.EliteAmbushEnabled && difficulty.Tier == 5;
        var prestigeScalar = ResolvePrestigeScalar(prestigeLevel, difficulty.Tier, useEliteMultipliers);

        if (prestigeScalar > 1f + PrestigeScalarEpsilon)
        {
            _log?.LogDebug($"[Infamy] Applying prestige scaling {prestigeScalar:F2}x (prestige {prestigeLevel}, tier {difficulty.Tier}, elite={useEliteMultipliers}) for ambush targeting {steamId}.");
        }

        AmbushSquadTracker? followUpTracker = spawnPlan.FollowUpRequests.Count == 0
            ? null
            : new AmbushSquadTracker(
                steamId,
                factionId,
                playerLevel,
                position,
                difficulty,
                reliefPerUnit,
                useEliteMultipliers,
                prestigeScalar,
                spawnPlan.FollowUpRequests,
                spawnRequests.Count);

        var sharedVisualBuff = ResolveSharedVisualBuff(factionId, spawnRequests, useEliteMultipliers);

        foreach (var request in spawnRequests)
        {
            var unit = request.Definition;
            var count = request.Count;
            var levelOffset = difficulty.LevelOffset + unit.LevelOffset;
            var cappedTarget = Math.Min(playerLevel + levelOffset, playerLevel + MaxPositiveLevelOffset);
            var targetLevel = Math.Clamp(cappedTarget, 1, 999);
            var lifetimeSeconds = GetNextLifetimeSeconds();
            var multipliers = ResolveAmbushMultipliers(useEliteMultipliers, request.IsRepresentative, prestigeScalar);
            var pending = new PendingAmbushSpawn(
                unit.Prefab,
                steamId,
                factionId,
                difficulty.Tier,
                targetLevel,
                count,
                reliefPerUnit,
                lifetimeSeconds,
                multipliers,
                request.VisualStrategy,
                useEliteMultipliers,
                request.VisualStrategy == VisualBuffStrategy.Shared ? sharedVisualBuff : null,
                followUpTracker);
            var marker = 0;

            try
            {
                marker = FactionInfamySpawnUtility.SpawnUnit(
                    unit.Prefab,
                    position,
                    count,
                    unit.MinRange,
                    unit.MaxRange,
                    lifetimeSeconds,
                    (manager, spawnedEntity, key, actualLifetime) =>
                    {
                        if (!PendingSpawns.TryGetValue(key, out var registered))
                        {
                            return;
                        }

                        var completed = FinalizeAmbushSpawn(manager, spawnedEntity, key, registered, actualLifetime, registered.Multipliers);
                        if (completed)
                        {
                            FactionInfamySpawnUtility.CancelSpawnCallback(key);
                        }
                    });

                PendingSpawns[marker] = pending;
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Infamy] Failed to spawn ambush unit {unit.Prefab.GuidHash} for faction '{factionId}': {ex.Message}");
                if (marker != 0)
                {
                    PendingSpawns.TryRemove(marker, out _);
                    FactionInfamySpawnUtility.CancelSpawnCallback(marker);
                }
            }
        }

        _log?.LogInfo($"[Infamy] Spawned ambush squad for faction '{factionId}' targeting {steamId}.");
        return true;
    }

    private static AmbushSpawnPlan BuildSpawnRequests(AmbushSquadDefinition squad, AmbushDifficulty difficulty)
    {
        var requests = new List<AmbushSpawnRequest>();
        var followUps = new List<AmbushSpawnRequest>();

        AddUnits(requests, squad.BaseUnits, isRepresentative: false, VisualBuffStrategy.Shared);

        if (difficulty.Tier != 5)
        {
            return new AmbushSpawnPlan(requests, followUps);
        }

        AddUnits(requests, squad.Tier5Representatives, isRepresentative: true, VisualBuffStrategy.Shared);

        if (!FactionInfamySystem.HalloweenAmbushEnabled)
        {
            return new AmbushSpawnPlan(requests, followUps);
        }

        var seasonalUnits = squad.GetSeasonalUnits(SeasonalAmbushType.Halloween);
        if (seasonalUnits.Count == 0)
        {
            return new AmbushSpawnPlan(requests, followUps);
        }

        var scarecrowCount = RollHalloweenScarecrowCount();
        var followUpCount = RollHalloweenFollowUpCount();

        foreach (var seasonal in seasonalUnits)
        {
            var count = seasonal.UseSharedRollCount
                ? scarecrowCount
                : Math.Max(1, seasonal.Unit.Count);

            if (count > 0)
            {
                requests.Add(new AmbushSpawnRequest(seasonal.Unit, count, isRepresentative: false, VisualBuffStrategy.RandomPerUnit));

                if (followUpCount > 0)
                {
                    var followUp = seasonal.UseSharedRollCount
                        ? followUpCount
                        : Math.Max(1, seasonal.Unit.Count);

                    if (followUp > 0)
                    {
                        followUps.Add(new AmbushSpawnRequest(seasonal.Unit, followUp, isRepresentative: false, VisualBuffStrategy.RandomPerUnit));
                    }
                }
            }
        }

        return new AmbushSpawnPlan(requests, followUps);
    }

    private static void AddUnits(
        List<AmbushSpawnRequest> destination,
        IReadOnlyList<AmbushUnitDefinition> units,
        bool isRepresentative,
        VisualBuffStrategy visualStrategy)
    {
        if (units is null || units.Count == 0)
        {
            return;
        }

        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            destination.Add(new AmbushSpawnRequest(unit, Math.Max(1, unit.Count), isRepresentative, visualStrategy));
        }
    }

    private static int RollHalloweenScarecrowCount()
    {
        var min = Math.Max(0, FactionInfamySystem.HalloweenScarecrowMinimum);
        var max = Math.Max(min, FactionInfamySystem.HalloweenScarecrowMaximum);
        if (max <= 0)
        {
            return 0;
        }

        var count = Random.Next(min, max + 1);

        var rareChance = Math.Clamp(FactionInfamySystem.HalloweenScarecrowRareChancePercent, 0, 100);
        if (rareChance > 0 && Random.Next(0, 100) < rareChance)
        {
            var multiplier = Math.Max(1, FactionInfamySystem.HalloweenScarecrowRareMultiplier);
            count *= multiplier;
        }

        return Math.Max(0, count);
    }

    private static int RollHalloweenFollowUpCount()
    {
        var chance = Math.Clamp(FactionInfamySystem.SeasonalFollowUpChancePercent, 0, 100);
        if (chance <= 0 || Random.Next(0, 100) >= chance)
        {
            return 0;
        }

        var min = Math.Max(0, FactionInfamySystem.SeasonalFollowUpMinimum);
        var max = Math.Max(min, FactionInfamySystem.SeasonalFollowUpMaximum);
        if (max <= 0)
        {
            return 0;
        }

        var count = Random.Next(min, max + 1);
        return Math.Max(0, count);
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

    private static int ResolvePlayerLevel(EntityManager entityManager, Entity playerEntity, ulong steamId)
    {
        if (TryGetPlayerLevel(entityManager, playerEntity, out var level))
        {
            return level;
        }

        if (steamId != 0UL && TryResolvePlayerCharacter(entityManager, steamId, out var resolved) &&
            TryGetPlayerLevel(entityManager, resolved, out level))
        {
            return level;
        }

        return 1;
    }

    private static bool TryGetPlayerLevel(EntityManager entityManager, Entity entity, out int level)
    {
        if (TryGetUnitLevel(entityManager, entity, out level))
        {
            return true;
        }

        if (TryGetEquipmentLevel(entityManager, entity, out level))
        {
            return true;
        }

        level = 0;
        return false;
    }

    private static bool TryGetUnitLevel(EntityManager entityManager, Entity entity, out int level)
    {
        level = 0;
        if (!entityManager.TryGetComponentData(entity, out UnitLevel unitLevel))
        {
            return false;
        }

        level = Math.Max(1, unitLevel.Level._Value);
        return true;
    }

    private static bool TryGetEquipmentLevel(EntityManager entityManager, Entity entity, out int level)
    {
        level = 0;
        if (!entityManager.TryGetComponentData(entity, out Equipment equipment))
        {
            return false;
        }

        var total = equipment.ArmorLevel.Value + equipment.WeaponLevel.Value + equipment.SpellLevel.Value;
        level = Math.Max(1, (int)total);
        return true;
    }

    private static bool TryResolvePlayerCharacter(EntityManager entityManager, ulong steamId, out Entity character)
    {
        character = Entity.Null;
        if (steamId == 0UL)
        {
            return false;
        }

        EntityQuery query;
        try
        {
            query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<User>());
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            var userEntities = query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var userEntity in userEntities)
                {
                    if (!entityManager.TryGetComponentData(userEntity, out User user) || user.PlatformId != steamId)
                    {
                        continue;
                    }

                    var resolved = user.LocalCharacter.GetEntityOnServer();
                    if (!resolved.Exists())
                    {
                        resolved = user.LocalCharacter._Entity;
                    }

                    if (resolved.Exists())
                    {
                        character = resolved;
                        return true;
                    }
                }
            }
            finally
            {
                userEntities.Dispose();
            }
        }
        finally
        {
            query.Dispose();
        }

        return false;
    }

    private static AmbushDifficulty EvaluateDifficulty(float hateValue)
    {
        var bucket = FactionInfamyTierHelper.CalculateTier(hateValue, FactionInfamySystem.MaximumHate);

        var offset = bucket switch
        {
            1 => -20,
            2 => -2,
            3 => 0,
            4 => 5,
            5 => 10,
            _ => 0
        };

        return new AmbushDifficulty(bucket, offset);
    }

    private static bool FinalizeAmbushSpawn(EntityManager entityManager, Entity entity, int marker, PendingAmbushSpawn pending, float lifetimeSeconds, AmbushStatMultipliers multipliers)
    {
        if (entityManager.HasComponent<LifeTime>(entity))
        {
            var lifeTime = entityManager.GetComponentData<LifeTime>(entity);
            lifeTime.Duration = lifetimeSeconds;
            lifeTime.EndAction = lifetimeSeconds < 0 ? LifeTimeEndAction.None : LifeTimeEndAction.Destroy;
            entityManager.SetComponentData(entity, lifeTime);
        }

        ApplyAmbushScaling(entityManager, entity, pending.UnitLevel, multipliers);
        RemoveSentinelLevelAura(entityManager, entity, pending.Prefab);

        if (FactionInfamySystem.ShouldApplyAmbushVisualBuffs(pending.IsElite))
        {
            PrefabGUID? visualBuff = pending.VisualStrategy switch
            {
                VisualBuffStrategy.Shared => pending.SharedVisualBuff,
                VisualBuffStrategy.RandomPerUnit => TryRollVisualBuff(pending.FactionId, out var rolled)
                    ? rolled
                    : null,
                _ => null
            };

            if (visualBuff.HasValue)
            {
                entity.TryApplyVisualBuff(visualBuff.Value);
            }
        }

        if (!entityManager.HasComponent<DestroyWhenDisabled>(entity))
        {
            entityManager.AddComponent<DestroyWhenDisabled>(entity);
        }

        if (entityManager.HasComponent<Minion>(entity))
        {
            entityManager.RemoveComponent<Minion>(entity);
        }

        EnsureFactionAlignment(entityManager, entity, pending);

        if (!FactionInfamySystem.NativeDropTablesEnabled)
        {
            ClearNativeDropTables(entityManager, entity, pending.FactionId);
            ApplyCustomDropTables(entityManager, entity, pending.FactionId);
        }

        var suppressFeed = FactionInfamySystem.SuppressBloodConsumeOnSpawn;
        var suppressCharm = FactionInfamySystem.SuppressCharmOnSpawn;
        if (suppressFeed || suppressCharm)
        {
            SpawnSuppressionService.SuppressSpawnComponents(
                entityManager,
                entity,
                suppressFeed,
                suppressCharm,
                $"ambush faction '{pending.FactionId}'",
                pending.DifficultyTier);
        }

        ActiveAmbushes[entity] = new ActiveAmbush(pending.TargetSteamId, pending.FactionId, pending.HateReliefPerUnit);

        pending.Remaining--;
        if (pending.Remaining <= 0)
        {
            PendingSpawns.TryRemove(marker, out _);
            pending.SquadTracker?.OnCoreRequestCompleted();
            return true;
        }

        return false;
    }

    private static void RemoveSentinelLevelAura(EntityManager entityManager, Entity entity, PrefabGUID prefab)
    {
        if (prefab.GuidHash != BlackfangSentinelPrefab.GuidHash)
        {
            return;
        }

        if (BuffUtility.TryGetBuff(entityManager, entity, LevelAuraSelfBuff, out var buffEntity))
        {
            DestroyUtility.Destroy(entityManager, buffEntity, DestroyDebugReason.TryRemoveBuff);
        }

        if (!entityManager.TryGetComponentData(entity, out ApplyBuffOnGameplayEvent applyBuff))
        {
            return;
        }

        var changed = false;
        if (applyBuff.Buff0.GuidHash == LevelAuraSelfBuff.GuidHash)
        {
            applyBuff.Buff0 = default;
            changed = true;
        }

        if (applyBuff.Buff1.GuidHash == LevelAuraSelfBuff.GuidHash)
        {
            applyBuff.Buff1 = default;
            changed = true;
        }

        if (applyBuff.Buff2.GuidHash == LevelAuraSelfBuff.GuidHash)
        {
            applyBuff.Buff2 = default;
            changed = true;
        }

        if (applyBuff.Buff3.GuidHash == LevelAuraSelfBuff.GuidHash)
        {
            applyBuff.Buff3 = default;
            changed = true;
        }

        if (changed)
        {
            entityManager.SetComponentData(entity, applyBuff);
        }
    }

    private static void EnsureFactionAlignment(EntityManager entityManager, Entity entity, PendingAmbushSpawn pending)
    {
        if (!FactionInfamyVictimResolver.TryResolveFactionGuid(pending.FactionId, out var factionGuid))
        {
            return;
        }

        UpdateFactionReference(entityManager, entity, factionGuid);

        if (!TryCacheFactionTeamData(entityManager, entity, pending.FactionId, factionGuid, out var cachedTeam))
        {
            FactionTeamCache.TryGetValue(pending.FactionId, out cachedTeam);
        }

        if (!cachedTeam.IsValid)
        {
            return;
        }

        ApplyTeamData(entityManager, entity, cachedTeam);
    }

    private static void ClearNativeDropTables(EntityManager entityManager, Entity entity, string factionId)
    {
        if (!entityManager.HasComponent<DropTableBuffer>(entity))
        {
            return;
        }

        var buffer = entityManager.GetBuffer<DropTableBuffer>(entity);
        if (buffer.Length == 0)
        {
            return;
        }

        buffer.Clear();

        var context = string.IsNullOrWhiteSpace(factionId) ? "unknown" : factionId;
        _log?.LogInfo($"[Spawn] Cleared existing DropTableBuffer for ambush faction '{context}' (entity {entity.Index}).");
    }

    private static void ApplyCustomDropTables(EntityManager entityManager, Entity entity, string factionId)
    {
        if (!FactionInfamyAmbushData.TryGetLootDefinition(factionId, out var definition)
            || definition.Entries.Count == 0)
        {
            return;
        }

        if (!entityManager.HasComponent<DropTableBuffer>(entity))
        {
            entityManager.AddBuffer<DropTableBuffer>(entity);
        }

        var buffer = entityManager.GetBuffer<DropTableBuffer>(entity);
        buffer.Clear();

        if (!FactionInfamyAmbushData.TryEnsureLootPrefab(entityManager, factionId, definition, out var prefabEntity)
            || prefabEntity == Entity.Null)
        {
            _log?.LogWarning($"[Spawn] Failed to resolve custom drop table for ambush faction '{factionId}'.");
            return;
        }

        buffer.Add(new DropTableBuffer
        {
            DropTableGuid = definition.DropTableGuid,
            DropTrigger = DropTriggerType.OnDeath,
            RelicType = RelicType.None
        });

        _log?.LogDebug($"[Spawn] Applied custom DropTableBuffer for ambush faction '{factionId}' (entity {entity.Index}).");
    }

    private static void UpdateFactionReference(EntityManager entityManager, Entity entity, PrefabGUID factionGuid)
    {
        if (entityManager.HasComponent<FactionReference>(entity))
        {
            var factionReference = entityManager.GetComponentData<FactionReference>(entity);
            if (factionReference.FactionGuid._Value.GuidHash != factionGuid.GuidHash)
            {
                factionReference.FactionGuid._Value = factionGuid;
                entityManager.SetComponentData(entity, factionReference);
            }
        }
        else
        {
            var factionReference = new FactionReference();
            factionReference.FactionGuid._Value = factionGuid;
            entityManager.AddComponentData(entity, factionReference);
        }
    }

    private static bool TryCacheFactionTeamData(
        EntityManager entityManager,
        Entity entity,
        string factionId,
        PrefabGUID factionGuid,
        out FactionTeamData teamData)
    {
        teamData = default;

        if (!entityManager.HasComponent<FactionReference>(entity))
        {
            return false;
        }

        var factionReference = entityManager.GetComponentData<FactionReference>(entity);
        if (factionReference.FactionGuid._Value.GuidHash != factionGuid.GuidHash)
        {
            return false;
        }

        if (!entityManager.HasComponent<Team>(entity) || !entityManager.HasComponent<TeamReference>(entity))
        {
            return false;
        }

        var team = entityManager.GetComponentData<Team>(entity);
        var teamReference = entityManager.GetComponentData<TeamReference>(entity);

        if (teamReference.Value._Value == Entity.Null)
        {
            return false;
        }

        teamData = new FactionTeamData(factionGuid, team, teamReference);
        FactionTeamCache[factionId] = teamData;
        return true;
    }

    private static void ApplyTeamData(EntityManager entityManager, Entity entity, FactionTeamData teamData)
    {
        if (!teamData.IsValid)
        {
            return;
        }

        if (entityManager.HasComponent<Team>(entity))
        {
            var team = entityManager.GetComponentData<Team>(entity);
            if (team.Value != teamData.Team.Value || team.FactionIndex != teamData.Team.FactionIndex)
            {
                team.Value = teamData.Team.Value;
                team.FactionIndex = teamData.Team.FactionIndex;
                entityManager.SetComponentData(entity, team);
            }
        }

        if (entityManager.HasComponent<TeamReference>(entity))
        {
            var teamReference = entityManager.GetComponentData<TeamReference>(entity);
            if (teamReference.Value._Value != teamData.TeamReference.Value._Value)
            {
                teamReference.Value._Value = teamData.TeamReference.Value._Value;
                entityManager.SetComponentData(entity, teamReference);
            }
        }
    }

    private static void ApplyAmbushScaling(EntityManager entityManager, Entity entity, int targetLevel, AmbushStatMultipliers multipliers)
    {
        if (targetLevel <= 0)
        {
            return;
        }

        if (entityManager.HasComponent<UnitLevel>(entity))
        {
            var unitLevel = entityManager.GetComponentData<UnitLevel>(entity);
            if (unitLevel.Level._Value != targetLevel)
            {
                unitLevel.Level._Value = targetLevel;
                entityManager.SetComponentData(entity, unitLevel);
            }

            if (!entityManager.HasComponent<UnitLevelChanged>(entity))
            {
                entityManager.AddComponent<UnitLevelChanged>(entity);
            }
        }

        if (multipliers.HasHealth && entityManager.HasComponent<Health>(entity))
        {
            var health = entityManager.GetComponentData<Health>(entity);
            var maxHealth = health.MaxHealth;
            maxHealth._Value *= multipliers.HealthMultiplier;
            health.MaxHealth = maxHealth;
            health.Value = Math.Min(health.Value * multipliers.HealthMultiplier, health.MaxHealth._Value);
            health.MaxRecoveryHealth *= multipliers.HealthMultiplier;
            entityManager.SetComponentData(entity, health);
        }

        if (entityManager.HasComponent<UnitStats>(entity))
        {
            var stats = entityManager.GetComponentData<UnitStats>(entity);
            var statsChanged = false;

            if (multipliers.HasPower)
            {
                var physicalPower = stats.PhysicalPower;
                physicalPower._Value *= multipliers.PowerMultiplier;
                stats.PhysicalPower = physicalPower;

                var spellPower = stats.SpellPower;
                spellPower._Value *= multipliers.PowerMultiplier;
                stats.SpellPower = spellPower;

                statsChanged = true;
            }

            if (multipliers.HasResistance)
            {
                var physicalResistance = stats.PhysicalResistance;
                physicalResistance._Value *= multipliers.ResistanceMultiplier;
                stats.PhysicalResistance = physicalResistance;

                var spellResistance = stats.SpellResistance;
                spellResistance._Value *= multipliers.ResistanceMultiplier;
                stats.SpellResistance = spellResistance;

                var fireResistance = stats.FireResistance;
                fireResistance._Value = Math.Max(0, (int)MathF.Round(fireResistance._Value * multipliers.ResistanceMultiplier));
                stats.FireResistance = fireResistance;

                statsChanged = true;
            }

            if (multipliers.HasDamageReduction)
            {
                var damageReduction = stats.DamageReduction;
                damageReduction._Value *= multipliers.DamageReductionMultiplier;
                stats.DamageReduction = damageReduction;

                var corruption = stats.CorruptionDamageReduction;
                corruption._Value *= multipliers.DamageReductionMultiplier;
                stats.CorruptionDamageReduction = corruption;

                statsChanged = true;
            }

            if (statsChanged)
            {
                entityManager.SetComponentData(entity, stats);
            }

            if (!entityManager.HasComponent<UnitBaseStatsTypeChanged>(entity))
            {
                entityManager.AddComponent<UnitBaseStatsTypeChanged>(entity);
            }
        }

        if (entityManager.HasComponent<AbilityBar_Shared>(entity) && (multipliers.HasAttackSpeed || multipliers.HasSpellSpeed))
        {
            var abilityBar = entityManager.GetComponentData<AbilityBar_Shared>(entity);
            var abilityChanged = false;

            if (multipliers.HasAttackSpeed)
            {
                var primarySpeed = abilityBar.PrimaryAttackSpeed;
                primarySpeed._Value *= multipliers.AttackSpeedMultiplier;
                abilityBar.PrimaryAttackSpeed = primarySpeed;
                abilityChanged = true;
            }

            if (multipliers.HasSpellSpeed)
            {
                var abilitySpeed = abilityBar.AbilityAttackSpeed;
                abilitySpeed._Value *= multipliers.SpellSpeedMultiplier;
                abilityBar.AbilityAttackSpeed = abilitySpeed;
                abilityChanged = true;
            }

            if (abilityChanged)
            {
                entityManager.SetComponentData(entity, abilityBar);
            }
        }

        if (multipliers.HasMoveSpeed && entityManager.HasComponent<AiMoveSpeeds>(entity))
        {
            var speeds = entityManager.GetComponentData<AiMoveSpeeds>(entity);
            var walk = speeds.Walk;
            walk._Value *= multipliers.MoveSpeedMultiplier;
            speeds.Walk = walk;

            var run = speeds.Run;
            run._Value *= multipliers.MoveSpeedMultiplier;
            speeds.Run = run;

            var circle = speeds.Circle;
            circle._Value *= multipliers.MoveSpeedMultiplier;
            speeds.Circle = circle;

            var retreat = speeds.Return;
            retreat._Value *= multipliers.MoveSpeedMultiplier;
            speeds.Return = retreat;

            entityManager.SetComponentData(entity, speeds);
        }

        if (multipliers.HasKnockback && entityManager.HasComponent<Buffable>(entity))
        {
            var buffable = entityManager.GetComponentData<Buffable>(entity);
            var knockback = buffable.KnockbackResistanceIndex;
            knockback._Value = Math.Max(0, (int)MathF.Round(knockback._Value * multipliers.KnockbackResistanceMultiplier));
            buffable.KnockbackResistanceIndex = knockback;
            entityManager.SetComponentData(entity, buffable);
        }
    }

    private readonly struct FactionTeamData
    {
        public FactionTeamData(PrefabGUID factionGuid, Team team, TeamReference teamReference)
        {
            FactionGuid = factionGuid;
            Team = team;
            TeamReference = teamReference;
        }

        public PrefabGUID FactionGuid { get; }

        public Team Team { get; }

        public TeamReference TeamReference { get; }

        public bool IsValid => TeamReference.Value._Value != Entity.Null;
    }

    private sealed class PendingAmbushSpawn
    {
        public PendingAmbushSpawn(
            PrefabGUID prefab,
            ulong targetSteamId,
            string factionId,
            int difficultyTier,
            int unitLevel,
            int remaining,
            float hateReliefPerUnit,
            float lifetimeSeconds,
            AmbushStatMultipliers multipliers,
            VisualBuffStrategy visualStrategy,
            bool isElite,
            PrefabGUID? sharedVisualBuff,
            AmbushSquadTracker? squadTracker = null)
        {
            Prefab = prefab;
            TargetSteamId = targetSteamId;
            FactionId = factionId;
            DifficultyTier = difficultyTier;
            UnitLevel = unitLevel;
            Remaining = remaining;
            HateReliefPerUnit = hateReliefPerUnit;
            LifetimeSeconds = lifetimeSeconds;
            Multipliers = multipliers;
            VisualStrategy = visualStrategy;
            IsElite = isElite;
            SharedVisualBuff = sharedVisualBuff;
            SquadTracker = squadTracker;
        }

        public PrefabGUID Prefab { get; }

        public ulong TargetSteamId { get; }

        public string FactionId { get; }

        public int DifficultyTier { get; }

        public int UnitLevel { get; }

        public int Remaining { get; set; }

        public float HateReliefPerUnit { get; }

        public float LifetimeSeconds { get; }

        public AmbushStatMultipliers Multipliers { get; }

        public VisualBuffStrategy VisualStrategy { get; }

        public bool IsElite { get; }

        public PrefabGUID? SharedVisualBuff { get; }

        public AmbushSquadTracker? SquadTracker { get; }
    }

    private sealed class AmbushSquadTracker
    {
        private readonly ulong _steamId;
        private readonly string _factionId;
        private readonly int _playerLevel;
        private readonly AmbushDifficulty _difficulty;
        private readonly float3 _position;
        private readonly float _hateReliefPerUnit;
        private readonly bool _useEliteMultipliers;
        private readonly float _prestigeScalar;
        private readonly List<AmbushSpawnRequest> _followUpRequests;
        private int _remainingCoreRequests;
        private int _followUpQueued;

        public AmbushSquadTracker(
            ulong steamId,
            string factionId,
            int playerLevel,
            float3 position,
            AmbushDifficulty difficulty,
            float hateReliefPerUnit,
            bool useEliteMultipliers,
            float prestigeScalar,
            IReadOnlyList<AmbushSpawnRequest> followUpRequests,
            int remainingCoreRequests)
        {
            _steamId = steamId;
            _factionId = factionId;
            _playerLevel = playerLevel;
            _position = position;
            _difficulty = difficulty;
            _hateReliefPerUnit = hateReliefPerUnit;
            _useEliteMultipliers = useEliteMultipliers;
            _prestigeScalar = prestigeScalar;
            _followUpRequests = followUpRequests?.Count > 0
                ? new List<AmbushSpawnRequest>(followUpRequests)
                : new List<AmbushSpawnRequest>();
            _remainingCoreRequests = Math.Max(0, remainingCoreRequests);
            _followUpQueued = 0;
        }

        public void OnCoreRequestCompleted()
        {
            if (_remainingCoreRequests <= 0)
            {
                return;
            }

            if (Interlocked.Decrement(ref _remainingCoreRequests) <= 0)
            {
                QueueFollowUpWave();
            }
        }

        private void QueueFollowUpWave()
        {
            if (_followUpRequests.Count == 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _followUpQueued, 1) != 0)
            {
                return;
            }

            var spawnedAny = false;
            var sharedVisualBuff = ResolveSharedVisualBuff(_factionId, _followUpRequests, _useEliteMultipliers);

            foreach (var request in _followUpRequests)
            {
                var unit = request.Definition;
                var count = request.Count;
                if (count <= 0)
                {
                    continue;
                }

                var levelOffset = _difficulty.LevelOffset + unit.LevelOffset;
                var cappedTarget = Math.Min(_playerLevel + levelOffset, _playerLevel + MaxPositiveLevelOffset);
                var targetLevel = Math.Clamp(cappedTarget, 1, 999);
                var lifetimeSeconds = GetNextLifetimeSeconds();
                var multipliers = ResolveAmbushMultipliers(_useEliteMultipliers, request.IsRepresentative, _prestigeScalar);
                var pending = new PendingAmbushSpawn(
                    unit.Prefab,
                    _steamId,
                    _factionId,
                    _difficulty.Tier,
                    targetLevel,
                    count,
                    _hateReliefPerUnit,
                    lifetimeSeconds,
                    multipliers,
                    request.VisualStrategy,
                    _useEliteMultipliers,
                    request.VisualStrategy == VisualBuffStrategy.Shared ? sharedVisualBuff : null);

                var marker = 0;

                try
                {
                    marker = FactionInfamySpawnUtility.SpawnUnit(
                        unit.Prefab,
                        _position,
                        count,
                        unit.MinRange,
                        unit.MaxRange,
                        lifetimeSeconds,
                        (manager, spawnedEntity, key, actualLifetime) =>
                        {
                            if (!PendingSpawns.TryGetValue(key, out var registered))
                            {
                                return;
                            }

                            var completed = FinalizeAmbushSpawn(manager, spawnedEntity, key, registered, actualLifetime, registered.Multipliers);
                            if (completed)
                            {
                                FactionInfamySpawnUtility.CancelSpawnCallback(key);
                            }
                        });

                    PendingSpawns[marker] = pending;
                    spawnedAny = true;
                }
                catch (Exception ex)
                {
                    _log?.LogError($"[Infamy] Failed to spawn seasonal follow-up unit {unit.Prefab.GuidHash} for faction '{_factionId}': {ex.Message}");
                    if (marker != 0)
                    {
                        PendingSpawns.TryRemove(marker, out _);
                        FactionInfamySpawnUtility.CancelSpawnCallback(marker);
                    }
                }
            }

            if (spawnedAny)
            {
                _log?.LogInfo($"[Infamy] Spawned seasonal follow-up wave for faction '{_factionId}' targeting {_steamId}.");
            }
        }
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

    private readonly struct AmbushStatMultipliers
    {
        private const float Epsilon = 0.0001f;

        public AmbushStatMultipliers(
            float healthMultiplier,
            float damageReductionMultiplier,
            float resistanceMultiplier,
            float powerMultiplier,
            float attackSpeedMultiplier,
            float spellSpeedMultiplier,
            float moveSpeedMultiplier,
            float knockbackResistanceMultiplier,
            bool applyKnockbackResistance)
        {
            HealthMultiplier = healthMultiplier;
            DamageReductionMultiplier = damageReductionMultiplier;
            ResistanceMultiplier = resistanceMultiplier;
            PowerMultiplier = powerMultiplier;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            SpellSpeedMultiplier = spellSpeedMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            KnockbackResistanceMultiplier = knockbackResistanceMultiplier;
            ApplyKnockbackResistance = applyKnockbackResistance;
        }

        public float HealthMultiplier { get; }

        public float DamageReductionMultiplier { get; }

        public float ResistanceMultiplier { get; }

        public float PowerMultiplier { get; }

        public float AttackSpeedMultiplier { get; }

        public float SpellSpeedMultiplier { get; }

        public float MoveSpeedMultiplier { get; }

        public float KnockbackResistanceMultiplier { get; }

        public bool ApplyKnockbackResistance { get; }

        public bool HasHealth => !IsApproximatelyOne(HealthMultiplier);

        public bool HasDamageReduction => !IsApproximatelyOne(DamageReductionMultiplier);

        public bool HasResistance => !IsApproximatelyOne(ResistanceMultiplier);

        public bool HasPower => !IsApproximatelyOne(PowerMultiplier);

        public bool HasAttackSpeed => !IsApproximatelyOne(AttackSpeedMultiplier);

        public bool HasSpellSpeed => !IsApproximatelyOne(SpellSpeedMultiplier);

        public bool HasMoveSpeed => !IsApproximatelyOne(MoveSpeedMultiplier);

        public bool HasKnockback => ApplyKnockbackResistance && !IsApproximatelyOne(KnockbackResistanceMultiplier);

        private static bool IsApproximatelyOne(float value)
        {
            return Math.Abs(value - 1f) <= Epsilon;
        }

        public AmbushStatMultipliers Scale(float scalar)
        {
            var clamped = Math.Max(0f, scalar);
            if (IsApproximatelyOne(clamped))
            {
                return this;
            }

            var health = Math.Max(0f, HealthMultiplier * clamped);
            var damageReduction = Math.Max(0f, DamageReductionMultiplier * clamped);
            var resistance = Math.Max(0f, ResistanceMultiplier * clamped);
            var power = Math.Max(0f, PowerMultiplier * clamped);
            var attackSpeed = Math.Max(0f, AttackSpeedMultiplier * clamped);
            var spellSpeed = Math.Max(0f, SpellSpeedMultiplier * clamped);
            var moveSpeed = Math.Max(0f, MoveSpeedMultiplier * clamped);
            var knockback = ApplyKnockbackResistance
                ? Math.Max(0f, KnockbackResistanceMultiplier * clamped)
                : KnockbackResistanceMultiplier;

            return new AmbushStatMultipliers(
                health,
                damageReduction,
                resistance,
                power,
                attackSpeed,
                spellSpeed,
                moveSpeed,
                knockback,
                ApplyKnockbackResistance);
        }

        public static AmbushStatMultipliers Create(bool isRepresentative)
        {
            if (!FactionInfamySystem.EliteAmbushEnabled)
            {
                return Identity;
            }

            var health = ResolveMultiplier(
                FactionInfamySystem.EliteHealthMultiplier,
                FactionInfamySystem.EliteRepresentativeHealthRatio,
                FactionInfamySystem.EliteRepresentativeHealthAdditive,
                isRepresentative);
            var damageReduction = ResolveMultiplier(
                FactionInfamySystem.EliteDamageReductionMultiplier,
                FactionInfamySystem.EliteRepresentativeDamageReductionRatio,
                FactionInfamySystem.EliteRepresentativeDamageReductionAdditive,
                isRepresentative);
            var resistance = ResolveMultiplier(
                FactionInfamySystem.EliteResistanceMultiplier,
                FactionInfamySystem.EliteRepresentativeResistanceRatio,
                FactionInfamySystem.EliteRepresentativeResistanceAdditive,
                isRepresentative);
            var power = ResolveMultiplier(
                FactionInfamySystem.ElitePowerMultiplier,
                FactionInfamySystem.EliteRepresentativePowerRatio,
                FactionInfamySystem.EliteRepresentativePowerAdditive,
                isRepresentative);
            var attackSpeed = ResolveMultiplier(
                FactionInfamySystem.EliteAttackSpeedMultiplier,
                FactionInfamySystem.EliteRepresentativeAttackSpeedRatio,
                FactionInfamySystem.EliteRepresentativeAttackSpeedAdditive,
                isRepresentative);
            var spellSpeed = ResolveMultiplier(
                FactionInfamySystem.EliteSpellSpeedMultiplier,
                FactionInfamySystem.EliteRepresentativeSpellSpeedRatio,
                FactionInfamySystem.EliteRepresentativeSpellSpeedAdditive,
                isRepresentative);
            var moveSpeed = ResolveMultiplier(
                FactionInfamySystem.EliteMoveSpeedMultiplier,
                FactionInfamySystem.EliteRepresentativeMoveSpeedRatio,
                FactionInfamySystem.EliteRepresentativeMoveSpeedAdditive,
                isRepresentative);
            var knockback = ResolveMultiplier(
                FactionInfamySystem.EliteKnockbackResistanceMultiplier,
                FactionInfamySystem.EliteRepresentativeKnockbackResistanceRatio,
                FactionInfamySystem.EliteRepresentativeKnockbackResistanceAdditive,
                isRepresentative);
            var applyKnockback = FactionInfamySystem.AmbushKnockbackResistanceEnabled && !IsApproximatelyOne(knockback);

            if (IsApproximatelyOne(health)
                && IsApproximatelyOne(damageReduction)
                && IsApproximatelyOne(resistance)
                && IsApproximatelyOne(power)
                && IsApproximatelyOne(attackSpeed)
                && IsApproximatelyOne(spellSpeed)
                && IsApproximatelyOne(moveSpeed)
                && (!applyKnockback || IsApproximatelyOne(knockback)))
            {
                return Identity;
            }

            return new AmbushStatMultipliers(
                health,
                damageReduction,
                resistance,
                power,
                attackSpeed,
                spellSpeed,
                moveSpeed,
                knockback,
                applyKnockback);
        }

        public static AmbushStatMultipliers Identity { get; } = new(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, false);

        private static float ResolveMultiplier(
            float squadMultiplier,
            float representativeRatio,
            float representativeAdditive,
            bool isRepresentative)
        {
            var resolved = Math.Max(0f, squadMultiplier);

            if (!isRepresentative)
            {
                return resolved;
            }

            var ratio = Math.Max(0f, representativeRatio);
            var additive = Math.Max(0f, representativeAdditive);
            resolved = resolved * ratio + additive;

            return Math.Max(0f, resolved);
        }
    }

    private readonly struct AmbushSpawnPlan
    {
        public AmbushSpawnPlan(List<AmbushSpawnRequest> coreRequests, List<AmbushSpawnRequest> followUpRequests)
        {
            CoreRequests = coreRequests ?? new List<AmbushSpawnRequest>();
            FollowUpRequests = followUpRequests ?? new List<AmbushSpawnRequest>();
        }

        public List<AmbushSpawnRequest> CoreRequests { get; }

        public List<AmbushSpawnRequest> FollowUpRequests { get; }
    }

    private enum VisualBuffStrategy
    {
        Shared,
        RandomPerUnit
    }

    private readonly struct AmbushSpawnRequest
    {
        public AmbushSpawnRequest(
            AmbushUnitDefinition definition,
            int count,
            bool isRepresentative,
            VisualBuffStrategy visualStrategy)
        {
            Definition = definition;
            Count = Math.Max(1, count);
            IsRepresentative = isRepresentative;
            VisualStrategy = visualStrategy;
        }

        public AmbushUnitDefinition Definition { get; }

        public int Count { get; }

        public bool IsRepresentative { get; }

        public VisualBuffStrategy VisualStrategy { get; }
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

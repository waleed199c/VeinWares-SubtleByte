using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
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
    private const int MaxPositiveLevelOffset = 10;

    private static readonly ConcurrentDictionary<int, PendingAmbushSpawn> PendingSpawns = new();
    private static readonly ConcurrentDictionary<Entity, ActiveAmbush> ActiveAmbushes = new();
    private static readonly Dictionary<string, AmbushSquadDefinition> SquadDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bandits"] = new AmbushSquadDefinition(
            baseUnits: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-1030822544), 2, -1, 1.5f, 8f), // Deadeye
                new AmbushUnitDefinition(new PrefabGUID(-301730941), 2, -2, 1f, 6f) // Thug
            },
            tier5Representatives: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-1128238456), 1, 2, 1.5f, 7f) // Bomber
            },
            seasonalUnits: new[]
            {
                AmbushSeasonalDefinition.HalloweenScarecrow
            }),
        ["Blackfangs"] = new AmbushSquadDefinition(
            baseUnits: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1864177126), 2, 0, 1.5f, 7f), // Venomblade
                new AmbushUnitDefinition(new PrefabGUID(326501064), 1, 1, 2f, 9f) // Alchemist
            },
            tier5Representatives: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1531777139), 1, 2, 2f, 9f) // Sentinel
            },
            seasonalUnits: new[]
            {
                AmbushSeasonalDefinition.HalloweenScarecrow
            }),
        ["Militia"] = new AmbushSquadDefinition(
            baseUnits: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1148936156), 3, -1, 2f, 10f), // Rifleman
                new AmbushUnitDefinition(new PrefabGUID(794228023), 1, 1, 1.5f, 6f) // Knight Shield
            },
            tier5Representatives: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(2005508157), 1, 2, 2f, 8f) // Heavy
            },
            seasonalUnits: new[]
            {
                AmbushSeasonalDefinition.HalloweenScarecrow
            }),
        ["Gloomrot"] = new AmbushSquadDefinition(
            baseUnits: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-322293503), 2, 0, 3f, 10f), // Pyro
                new AmbushUnitDefinition(new PrefabGUID(1732477970), 1, 2, 4f, 12f) // Railgunner
            },
            tier5Representatives: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1401026468), 1, 2, 3f, 10f) // Sentry Officer
            },
            seasonalUnits: new[]
            {
                AmbushSeasonalDefinition.HalloweenScarecrow
            }),
        ["Legion"] = new AmbushSquadDefinition(
            baseUnits: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1980594081), 2, 1, 2f, 9f), // Shadowkin
                new AmbushUnitDefinition(new PrefabGUID(-1009917656), 1, 3, 3f, 11f) // Nightmare
            },
            tier5Representatives: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(1912966420), 1, 2, 3f, 10f) // Blood Prophet
            },
            seasonalUnits: new[]
            {
                AmbushSeasonalDefinition.HalloweenScarecrow
            }),
        ["Undead"] = new AmbushSquadDefinition(
            baseUnits: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-1287507270), 3, -1, 1.5f, 7f), // Skeleton Mage
                new AmbushUnitDefinition(new PrefabGUID(-1365627158), 1, 1, 2f, 8f) // Assassin
            },
            tier5Representatives: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-1967480038), 1, 2, 2f, 8f) // Guardian
            },
            seasonalUnits: new[]
            {
                AmbushSeasonalDefinition.HalloweenScarecrow
            }),
        ["Werewolf"] = new AmbushSquadDefinition(
            baseUnits: new[]
            {
                new AmbushUnitDefinition(new PrefabGUID(-951976780), 3, 0, 1.5f, 8f) // Hostile villager werewolf
            },
            seasonalUnits: new[]
            {
                AmbushSeasonalDefinition.HalloweenScarecrow
            })
    };

    private static ManualLogSource? _log;
    private static readonly System.Random Random = new();
    private static bool _initialized;
    private static int _lifetimeSequence;

    public static bool HasPendingSpawns => PendingSpawns.Count > 0;

    public static void Initialize(ManualLogSource log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _initialized = true;
    }

    public static void Shutdown()
    {
        _initialized = false;
        _log = null;
        foreach (var pair in PendingSpawns.ToArray())
        {
            FactionInfamySpawnUtility.CancelSpawnCallback(pair.Key);
        }
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

        var previousAmbushTimestamp = target.Value.LastAmbush;

        if (!FactionInfamySystem.TryConsumeAmbush(steamId, target.Key))
        {
            return;
        }

        var playerLevel = ResolvePlayerLevel(entityManager, playerEntity, steamId);
        var difficulty = EvaluateDifficulty(target.Value.Hate);
        if (!TrySpawnSquad(steamId, target.Key, playerLevel, position, target.Value.Hate, difficulty))
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

    private static bool TrySpawnSquad(ulong steamId, string factionId, int playerLevel, float3 position, float hateValue, AmbushDifficulty difficulty)
    {
        if (!SquadDefinitions.TryGetValue(factionId, out var squad))
        {
            return false;
        }

        var spawnRequests = BuildSpawnRequests(squad, difficulty);
        if (spawnRequests.Count == 0)
        {
            return false;
        }

        var totalUnits = spawnRequests.Sum(request => request.Count);
        if (totalUnits <= 0)
        {
            return false;
        }

        var totalRelief = Math.Max(MinimumReliefPerSquad, hateValue * HateReliefFraction);
        var reliefPerUnit = totalRelief / totalUnits;
        var useEliteMultipliers = FactionInfamySystem.EliteAmbushEnabled && difficulty.Tier == 5;

        foreach (var request in spawnRequests)
        {
            var unit = request.Definition;
            var count = request.Count;
            var levelOffset = difficulty.LevelOffset + unit.LevelOffset;
            var cappedTarget = Math.Min(playerLevel + levelOffset, playerLevel + MaxPositiveLevelOffset);
            var targetLevel = Math.Clamp(cappedTarget, 1, 999);
            var lifetimeSeconds = GetNextLifetimeSeconds();
            var multipliers = useEliteMultipliers
                ? AmbushStatMultipliers.Create(request.IsRepresentative)
                : AmbushStatMultipliers.Identity;
            var pending = new PendingAmbushSpawn(steamId, factionId, targetLevel, count, reliefPerUnit, lifetimeSeconds, multipliers);
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

    private static List<AmbushSpawnRequest> BuildSpawnRequests(AmbushSquadDefinition squad, AmbushDifficulty difficulty)
    {
        var requests = new List<AmbushSpawnRequest>();

        AddUnits(requests, squad.BaseUnits, isRepresentative: false);

        if (difficulty.Tier != 5)
        {
            return requests;
        }

        AddUnits(requests, squad.Tier5Representatives, isRepresentative: true);

        if (!FactionInfamySystem.HalloweenAmbushEnabled)
        {
            return requests;
        }

        var seasonalUnits = squad.GetSeasonalUnits(SeasonalAmbushType.Halloween);
        if (seasonalUnits.Count == 0)
        {
            return requests;
        }

        var scarecrowCount = RollHalloweenScarecrowCount();
        if (scarecrowCount <= 0)
        {
            return requests;
        }

        foreach (var seasonal in seasonalUnits)
        {
            var count = seasonal.UseSharedRollCount
                ? scarecrowCount
                : Math.Max(1, seasonal.Unit.Count);

            requests.Add(new AmbushSpawnRequest(seasonal.Unit, count, isRepresentative: false));
        }

        return requests;
    }

    private static void AddUnits(List<AmbushSpawnRequest> destination, IReadOnlyList<AmbushUnitDefinition> units, bool isRepresentative)
    {
        if (units is null || units.Count == 0)
        {
            return;
        }

        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            destination.Add(new AmbushSpawnRequest(unit, Math.Max(1, unit.Count), isRepresentative));
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
        var maximumHate = Math.Max(1f, FactionInfamySystem.MaximumHate);
        var normalized = Math.Clamp(hateValue / maximumHate, 0f, 1f);
        var bucket = (int)Math.Floor(normalized * 5.0) + 1;
        if (normalized >= 0.999f)
        {
            bucket = 5;
        }

        bucket = Math.Clamp(bucket, 1, 5);

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
            PendingSpawns.TryRemove(marker, out _);
            return true;
        }

        return false;
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

    private sealed class PendingAmbushSpawn
    {
        public PendingAmbushSpawn(ulong targetSteamId, string factionId, int unitLevel, int remaining, float hateReliefPerUnit, float lifetimeSeconds, AmbushStatMultipliers multipliers)
        {
            TargetSteamId = targetSteamId;
            FactionId = factionId;
            UnitLevel = unitLevel;
            Remaining = remaining;
            HateReliefPerUnit = hateReliefPerUnit;
            LifetimeSeconds = lifetimeSeconds;
            Multipliers = multipliers;
        }

        public ulong TargetSteamId { get; }

        public string FactionId { get; }

        public int UnitLevel { get; }

        public int Remaining { get; set; }

        public float HateReliefPerUnit { get; }

        public float LifetimeSeconds { get; }

        public AmbushStatMultipliers Multipliers { get; }
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

        private static float ResolveMultiplier(float baseMultiplier, float representativeRatio, bool isRepresentative)
        {
            var resolved = Math.Max(0f, baseMultiplier);
            if (isRepresentative)
            {
                resolved *= Math.Max(0f, representativeRatio);
            }

            return resolved;
        }

        public static AmbushStatMultipliers Create(bool isRepresentative)
        {
            var health = ResolveMultiplier(FactionInfamySystem.EliteHealthMultiplier, FactionInfamySystem.EliteRepresentativeHealthRatio, isRepresentative);
            var damageReduction = ResolveMultiplier(FactionInfamySystem.EliteDamageReductionMultiplier, FactionInfamySystem.EliteRepresentativeDamageReductionRatio, isRepresentative);
            var resistance = ResolveMultiplier(FactionInfamySystem.EliteResistanceMultiplier, FactionInfamySystem.EliteRepresentativeResistanceRatio, isRepresentative);
            var power = ResolveMultiplier(FactionInfamySystem.ElitePowerMultiplier, FactionInfamySystem.EliteRepresentativePowerRatio, isRepresentative);
            var attackSpeed = ResolveMultiplier(FactionInfamySystem.EliteAttackSpeedMultiplier, FactionInfamySystem.EliteRepresentativeAttackSpeedRatio, isRepresentative);
            var spellSpeed = ResolveMultiplier(FactionInfamySystem.EliteSpellSpeedMultiplier, FactionInfamySystem.EliteRepresentativeSpellSpeedRatio, isRepresentative);
            var moveSpeed = ResolveMultiplier(FactionInfamySystem.EliteMoveSpeedMultiplier, FactionInfamySystem.EliteRepresentativeMoveSpeedRatio, isRepresentative);
            var knockback = ResolveMultiplier(FactionInfamySystem.EliteKnockbackResistanceMultiplier, FactionInfamySystem.EliteRepresentativeKnockbackResistanceRatio, isRepresentative);
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
        private static readonly IReadOnlyList<AmbushUnitDefinition> EmptyUnits = Array.Empty<AmbushUnitDefinition>();

        private readonly Dictionary<SeasonalAmbushType, IReadOnlyList<AmbushSeasonalDefinition>> _seasonalUnits;

        public AmbushSquadDefinition(
            IReadOnlyList<AmbushUnitDefinition> baseUnits,
            IReadOnlyList<AmbushUnitDefinition>? tier5Representatives = null,
            IReadOnlyList<AmbushSeasonalDefinition>? seasonalUnits = null)
        {
            BaseUnits = baseUnits ?? EmptyUnits;
            Tier5Representatives = tier5Representatives ?? EmptyUnits;

            var seasonal = seasonalUnits ?? Array.Empty<AmbushSeasonalDefinition>();
            _seasonalUnits = seasonal.Count == 0
                ? new Dictionary<SeasonalAmbushType, IReadOnlyList<AmbushSeasonalDefinition>>()
                : seasonal
                    .GroupBy(definition => definition.Type)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<AmbushSeasonalDefinition>)group.ToArray());
        }

        public IReadOnlyList<AmbushUnitDefinition> BaseUnits { get; }

        public IReadOnlyList<AmbushUnitDefinition> Tier5Representatives { get; }

        public IReadOnlyList<AmbushSeasonalDefinition> GetSeasonalUnits(SeasonalAmbushType type)
        {
            return _seasonalUnits.TryGetValue(type, out var units)
                ? units
                : Array.Empty<AmbushSeasonalDefinition>();
        }
    }

    private sealed class AmbushSeasonalDefinition
    {
        public AmbushSeasonalDefinition(SeasonalAmbushType type, AmbushUnitDefinition unit, bool useSharedRollCount)
        {
            Type = type;
            Unit = unit;
            UseSharedRollCount = useSharedRollCount;
        }

        public SeasonalAmbushType Type { get; }

        public AmbushUnitDefinition Unit { get; }

        public bool UseSharedRollCount { get; }

        public static AmbushSeasonalDefinition HalloweenScarecrow { get; } = new(
            SeasonalAmbushType.Halloween,
            new AmbushUnitDefinition(new PrefabGUID(-1750347680), 1, 0, 2.5f, 8f),
            useSharedRollCount: true);
    }

    private enum SeasonalAmbushType
    {
        Halloween
    }

    private readonly struct AmbushSpawnRequest
    {
        public AmbushSpawnRequest(AmbushUnitDefinition definition, int count, bool isRepresentative)
        {
            Definition = definition;
            Count = Math.Max(1, count);
            IsRepresentative = isRepresentative;
        }

        public AmbushUnitDefinition Definition { get; }

        public int Count { get; }

        public bool IsRepresentative { get; }
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

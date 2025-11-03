using System;
using System.Collections.Concurrent;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services.FactionInfamy;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services;

internal static class DuelSummonService
{
    private static readonly PrefabGUID DuelAreaPrefab = new(-288427482);
    private static readonly PrefabGUID DuelConnectionBuffPrefab = new(1120504274);

    private static readonly ConcurrentDictionary<int, PendingDuelSummon> PendingMarkers = new();

    public static bool TrySummonForPlayer(
        Entity playerCharacter,
        PrefabGUID challengerPrefab,
        float3 centerPosition,
        float3 challengerPosition)
    {
        if (!playerCharacter.Exists())
        {
            return false;
        }

        ModLogger.Info(
            $"[DuelSummon] Scheduling duel for player {playerCharacter.Index}:{playerCharacter.Version} → challenger {challengerPrefab.GuidHash} | center={centerPosition} challenger={challengerPosition}.",
            verboseOnly: false);

        var pending = new PendingDuelSummon(playerCharacter, centerPosition, challengerPosition);

        try
        {
            var centerMarker = ScheduleSpawn(pending, DuelAreaPrefab, centerPosition, isCenter: true);
            pending.RegisterCenterMarker(centerMarker);

            var challengerMarker = ScheduleSpawn(pending, challengerPrefab, challengerPosition, isCenter: false);
            pending.RegisterChallengerMarker(challengerMarker);

            ModLogger.Info(
                $"[DuelSummon] Pending duel markers registered center={centerMarker} challenger={challengerMarker}.",
                verboseOnly: false);
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"[DuelSummon] Failed to schedule duel summon: {ex.Message}");
            CancelPending(pending);
            return false;
        }
    }

    private static int ScheduleSpawn(
        PendingDuelSummon pending,
        PrefabGUID prefab,
        float3 position,
        bool isCenter)
    {
        ModLogger.Info(
            $"[DuelSummon] Scheduling {(isCenter ? "duel center" : "challenger")} prefab {prefab.GuidHash} at {position}.",
            verboseOnly: false);

        var marker = FactionInfamySpawnUtility.SpawnUnit(
            prefab,
            position,
            count: 1,
            minRange: 0f,
            maxRange: 0f,
            lifetime: -1f,
            preFinalize: (manager, entity, key, _) =>
            {
                if (!PendingMarkers.TryGetValue(key, out var state))
                {
                    return;
                }

                if (isCenter)
                {
                    state.OnCenterSpawned(manager, entity);
                }
                else
                {
                    state.OnChallengerSpawned(manager, entity);
                }

                if (state.TryFinalize(manager))
                {
                    ModLogger.Info("[DuelSummon] Duel summon finalized successfully.", verboseOnly: false);
                    RemovePending(state);
                }
                else
                {
                    ModLogger.Info("[DuelSummon] Duel summon waiting for remaining entities.", verboseOnly: false);
                }
            });

        PendingMarkers[marker] = pending;
        ModLogger.Info($"[DuelSummon] Registered pending marker {marker} for {(isCenter ? "center" : "challenger")}.", verboseOnly: false);
        return marker;
    }

    private static void CancelPending(PendingDuelSummon state)
    {
        if (state.CenterMarker != 0)
        {
            PendingMarkers.TryRemove(state.CenterMarker, out _);
            FactionInfamySpawnUtility.CancelSpawnCallback(state.CenterMarker);
            ModLogger.Warn($"[DuelSummon] Cancelled pending center marker {state.CenterMarker}.");
        }

        if (state.ChallengerMarker != 0)
        {
            PendingMarkers.TryRemove(state.ChallengerMarker, out _);
            FactionInfamySpawnUtility.CancelSpawnCallback(state.ChallengerMarker);
            ModLogger.Warn($"[DuelSummon] Cancelled pending challenger marker {state.ChallengerMarker}.");
        }
    }

    private static void RemovePending(PendingDuelSummon state)
    {
        if (state.CenterMarker != 0)
        {
            PendingMarkers.TryRemove(state.CenterMarker, out _);
            ModLogger.Info($"[DuelSummon] Cleared pending center marker {state.CenterMarker}.", verboseOnly: false);
        }

        if (state.ChallengerMarker != 0)
        {
            PendingMarkers.TryRemove(state.ChallengerMarker, out _);
            ModLogger.Info($"[DuelSummon] Cleared pending challenger marker {state.ChallengerMarker}.", verboseOnly: false);
        }
    }

    private sealed class PendingDuelSummon
    {
        private readonly Entity _player;
        private readonly float3 _centerPosition;
        private readonly float3 _challengerPosition;

        private Entity _centerEntity;
        private Entity _challengerEntity;
        private bool _completed;

        public PendingDuelSummon(Entity player, float3 centerPosition, float3 challengerPosition)
        {
            _player = player;
            _centerPosition = centerPosition;
            _challengerPosition = challengerPosition;
        }

        public int CenterMarker { get; private set; }
        public int ChallengerMarker { get; private set; }

        public void RegisterCenterMarker(int marker) => CenterMarker = marker;
        public void RegisterChallengerMarker(int marker) => ChallengerMarker = marker;

        public void OnCenterSpawned(EntityManager manager, Entity entity)
        {
            _centerEntity = entity;
            EnsureTransform(manager, entity, _centerPosition);
            EnsurePersistentLifetime(manager, entity);
            ModLogger.Info($"[DuelSummon] Center entity spawned {_centerEntity.Index}:{_centerEntity.Version} at {_centerPosition}.", verboseOnly: false);
        }

        public void OnChallengerSpawned(EntityManager manager, Entity entity)
        {
            _challengerEntity = entity;
            EnsureTransform(manager, entity, _challengerPosition);
            EnsurePersistentLifetime(manager, entity);
            ModLogger.Info($"[DuelSummon] Challenger entity spawned {_challengerEntity.Index}:{_challengerEntity.Version} at {_challengerPosition}.", verboseOnly: false);
        }

        public bool TryFinalize(EntityManager manager)
        {
            if (_completed)
            {
                ModLogger.Debug("[DuelSummon] Finalize requested but already completed.");
                return false;
            }

            if (!_centerEntity.Exists() || !_challengerEntity.Exists())
            {
                ModLogger.Debug(
                    $"[DuelSummon] Finalize blocked waiting for center={_centerEntity.Exists()} challenger={_challengerEntity.Exists()}.");
                return false;
            }

            _completed = true;

            TryRegisterPlayer(manager);
            return true;
        }

        private void TryRegisterPlayer(EntityManager manager)
        {
            if (!_player.Exists())
            {
                ModLogger.Warn("[DuelSummon] Player entity destroyed before duel registration.");
                return;
            }

            if (_player.TryApplyAndGetBuff(DuelConnectionBuffPrefab, out var buffEntity) && buffEntity.Exists())
            {
                ModLogger.Info(
                    $"[DuelSummon] Applied duel connection buff {_player.Index}:{_player.Version} → buff {buffEntity.Index}:{buffEntity.Version}.",
                    verboseOnly: false);
                if (manager.HasComponent<EntityOwner>(buffEntity))
                {
                    var owner = manager.GetComponentData<EntityOwner>(buffEntity);
                    owner.Owner = _challengerEntity;
                    manager.SetComponentData(buffEntity, owner);
                    ModLogger.Info(
                        $"[DuelSummon] Linked challenger {_challengerEntity.Index}:{_challengerEntity.Version} as buff owner.",
                        verboseOnly: false);
                }
            }
            else
            {
                ModLogger.Warn("[DuelSummon] Failed to apply duel connection buff to player.");
            }
        }

        private static void EnsurePersistentLifetime(EntityManager manager, Entity entity)
        {
            if (!manager.HasComponent<LifeTime>(entity))
            {
                ModLogger.Debug($"[DuelSummon] Entity {entity.Index}:{entity.Version} has no lifetime component.");
                return;
            }

            var lifetime = manager.GetComponentData<LifeTime>(entity);
            lifetime.Duration = -1f;
            lifetime.EndAction = LifeTimeEndAction.None;
            manager.SetComponentData(entity, lifetime);
            ModLogger.Debug($"[DuelSummon] Extended lifetime for {entity.Index}:{entity.Version}.");
        }

        private static void EnsureTransform(EntityManager manager, Entity entity, float3 position)
        {
            if (manager.HasComponent<LocalTransform>(entity))
            {
                var transform = manager.GetComponentData<LocalTransform>(entity);
                transform.Position = position;
                manager.SetComponentData(entity, transform);
                ModLogger.Debug($"[DuelSummon] Updated LocalTransform for {entity.Index}:{entity.Version} to {position}.");
                return;
            }

            if (manager.HasComponent<Translation>(entity))
            {
                var translation = manager.GetComponentData<Translation>(entity);
                translation.Value = position;
                manager.SetComponentData(entity, translation);
                ModLogger.Debug($"[DuelSummon] Updated Translation for {entity.Index}:{entity.Version} to {position}.");
            }
        }
    }
}

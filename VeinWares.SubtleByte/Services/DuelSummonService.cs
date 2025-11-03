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

        var pending = new PendingDuelSummon(playerCharacter, centerPosition, challengerPosition);

        try
        {
            var centerMarker = ScheduleSpawn(pending, DuelAreaPrefab, centerPosition, isCenter: true);
            pending.RegisterCenterMarker(centerMarker);

            var challengerMarker = ScheduleSpawn(pending, challengerPrefab, challengerPosition, isCenter: false);
            pending.RegisterChallengerMarker(challengerMarker);
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
                    RemovePending(state);
                }
            });

        PendingMarkers[marker] = pending;
        return marker;
    }

    private static void CancelPending(PendingDuelSummon state)
    {
        if (state.CenterMarker != 0)
        {
            PendingMarkers.TryRemove(state.CenterMarker, out _);
            FactionInfamySpawnUtility.CancelSpawnCallback(state.CenterMarker);
        }

        if (state.ChallengerMarker != 0)
        {
            PendingMarkers.TryRemove(state.ChallengerMarker, out _);
            FactionInfamySpawnUtility.CancelSpawnCallback(state.ChallengerMarker);
        }
    }

    private static void RemovePending(PendingDuelSummon state)
    {
        if (state.CenterMarker != 0)
        {
            PendingMarkers.TryRemove(state.CenterMarker, out _);
        }

        if (state.ChallengerMarker != 0)
        {
            PendingMarkers.TryRemove(state.ChallengerMarker, out _);
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
        }

        public void OnChallengerSpawned(EntityManager manager, Entity entity)
        {
            _challengerEntity = entity;
            EnsureTransform(manager, entity, _challengerPosition);
            EnsurePersistentLifetime(manager, entity);
        }

        public bool TryFinalize(EntityManager manager)
        {
            if (_completed)
            {
                return false;
            }

            if (!_centerEntity.Exists() || !_challengerEntity.Exists())
            {
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
                return;
            }

            if (_player.TryApplyAndGetBuff(DuelConnectionBuffPrefab, out var buffEntity) && buffEntity.Exists())
            {
                if (manager.HasComponent<EntityOwner>(buffEntity))
                {
                    var owner = manager.GetComponentData<EntityOwner>(buffEntity);
                    owner.Owner = _challengerEntity;
                    manager.SetComponentData(buffEntity, owner);
                }
            }
        }

        private static void EnsurePersistentLifetime(EntityManager manager, Entity entity)
        {
            if (!manager.HasComponent<LifeTime>(entity))
            {
                return;
            }

            var lifetime = manager.GetComponentData<LifeTime>(entity);
            lifetime.Duration = -1f;
            lifetime.EndAction = LifeTimeEndAction.None;
            manager.SetComponentData(entity, lifetime);
        }

        private static void EnsureTransform(EntityManager manager, Entity entity, float3 position)
        {
            if (manager.HasComponent<LocalTransform>(entity))
            {
                var transform = manager.GetComponentData<LocalTransform>(entity);
                transform.Position = position;
                manager.SetComponentData(entity, transform);
                return;
            }

            if (manager.HasComponent<Translation>(entity))
            {
                var translation = manager.GetComponentData<Translation>(entity);
                translation.Value = position;
                manager.SetComponentData(entity, translation);
            }
        }
    }
}

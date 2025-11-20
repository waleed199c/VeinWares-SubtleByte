using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
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
    private static readonly ConcurrentDictionary<int, ActiveDuel> ActiveDuels = new();
    private static readonly ConcurrentDictionary<Entity, int> ParticipantLookup = new();

    public static void UpdateCombatState(EntityManager manager, Entity participant, bool inCombat)
    {
        if (!participant.Exists())
        {
            return;
        }

        if (!ParticipantLookup.TryGetValue(participant, out var duelId))
        {
            return;
        }

        if (!ActiveDuels.TryGetValue(duelId, out var duel))
        {
            ParticipantLookup.TryRemove(participant, out _);
            return;
        }

        duel.SetCombatState(participant, inCombat);
        if (!inCombat && duel.ShouldEnd())
        {
            EndActiveDuel(manager, duel);
        }
    }

    public static bool TrySummonForPlayer(
        Entity playerCharacter,
        PrefabGUID challengerPrefab,
        float3 centerPosition,
        float3 challengerPosition,
        int maxParticipantsPerDuel = int.MaxValue,
        int supplementalDuelAllowance = 0,
        IReadOnlyList<Entity>? explicitParticipants = null,
        float3? forwardHint = null)
    {
        if (!playerCharacter.Exists())
        {
            return false;
        }

        var manager = Core.EntityManager;
        if (!manager.HasComponent<PlayerCharacter>(playerCharacter))
        {
            ModLogger.Warn(
                $"[DuelSummon] Ignoring duel summon for non-player entity {playerCharacter.Index}:{playerCharacter.Version}.");
            return false;
        }

        if (!manager.TryGetComponentData(playerCharacter, out PlayerCharacter playerCharacterData) ||
            playerCharacterData.UserEntity == Entity.Null ||
            !manager.HasComponent<User>(playerCharacterData.UserEntity))
        {
            ModLogger.Warn(
                $"[DuelSummon] Ignoring duel summon for player {playerCharacter.Index}:{playerCharacter.Version}; missing user component.");
            return false;
        }

        ModLogger.Info(
            $"[DuelSummon] Scheduling duel for player {playerCharacter.Index}:{playerCharacter.Version} → challenger {challengerPrefab.GuidHash} | center={centerPosition} challenger={challengerPosition}.",
            verboseOnly: false);

        maxParticipantsPerDuel = math.max(1, maxParticipantsPerDuel);
        supplementalDuelAllowance = math.max(0, supplementalDuelAllowance);

        var forward = forwardHint ?? math.normalizesafe(challengerPosition - centerPosition, new float3(0f, 0f, 1f));
        if (math.lengthsq(forward) < 0.0001f)
        {
            forward = new float3(0f, 0f, 1f);
        }
        forward.y = 0f;
        forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));

        var pending = new PendingDuelSummon(
            playerCharacter,
            centerPosition,
            challengerPosition,
            challengerPrefab,
            maxParticipantsPerDuel,
            supplementalDuelAllowance,
            explicitParticipants,
            forward);

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
        private readonly PrefabGUID _challengerPrefab;
        private readonly int _maxParticipantsPerDuel;
        private readonly int _supplementalDuelsAllowed;
        private readonly IReadOnlyList<Entity>? _explicitParticipants;
        private readonly float3 _forward;
        private readonly float3 _challengerOffset;

        private Entity _centerEntity;
        private Entity _challengerEntity;
        private bool _completed;

        public PendingDuelSummon(
            Entity player,
            float3 centerPosition,
            float3 challengerPosition,
            PrefabGUID challengerPrefab,
            int maxParticipantsPerDuel,
            int supplementalDuelsAllowed,
            IReadOnlyList<Entity>? explicitParticipants,
            float3 forward)
        {
            _player = player;
            _centerPosition = centerPosition;
            _challengerPosition = challengerPosition;
            _challengerPrefab = challengerPrefab;
            _maxParticipantsPerDuel = math.max(1, maxParticipantsPerDuel);
            _supplementalDuelsAllowed = math.max(0, supplementalDuelsAllowed);
            _explicitParticipants = explicitParticipants;
            _forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
            if (math.lengthsq(_forward) < 0.0001f)
            {
                _forward = new float3(0f, 0f, 1f);
            }
            _forward.y = 0f;
            _forward = math.normalizesafe(_forward, new float3(0f, 0f, 1f));
            _challengerOffset = _challengerPosition - _centerPosition;
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

            EnsureChallengerDuelComponent(manager);
            ConfigureDuelArea(manager);
            TraceDuelEntities(manager);
            TryRegisterPlayer(manager);
            var overflow = InviteNearbyPlayers(manager);
            ScheduleSupplementalDuels(manager, overflow);
            RegisterActiveDuel(manager);
            return true;
        }

        private void EnsureChallengerDuelComponent(EntityManager manager)
        {
            if (!_centerEntity.Exists() || !_challengerEntity.Exists())
            {
                ModLogger.Warn("[DuelSummon] Cannot ensure challenger duel component before entities exist.");
                return;
            }

            if (!manager.HasComponent<VBloodDuelInstance>(_centerEntity))
            {
                ModLogger.Warn("[DuelSummon] Center missing VBloodDuelInstance; cannot propagate duel id to challenger.");
                return;
            }

            var duelInstance = manager.GetComponentData<VBloodDuelInstance>(_centerEntity);
            var expectedId = duelInstance.VBloodDuelId;

            if (manager.HasComponent<VBloodDuelChallenger>(_challengerEntity))
            {
                var challengerData = manager.GetComponentData<VBloodDuelChallenger>(_challengerEntity);
                if (challengerData.VBloodDuelId == expectedId && expectedId != 0)
                {
                    ModLogger.Debug(
                        $"[DuelSummon] Challenger already has matching duel id {expectedId}.");
                    return;
                }

                challengerData.VBloodDuelId = expectedId;
                manager.SetComponentData(_challengerEntity, challengerData);
                ModLogger.Info(
                    $"[DuelSummon] Updated challenger duel id to {expectedId}.",
                    verboseOnly: false);
                return;
            }

            manager.AddComponentData(_challengerEntity, new VBloodDuelChallenger
            {
                VBloodDuelId = expectedId,
            });

            ModLogger.Info(
                $"[DuelSummon] Added VBloodDuelChallenger with duel id {expectedId} to challenger {_challengerEntity.Index}:{_challengerEntity.Version}.",
                verboseOnly: false);
        }

        private void ConfigureDuelArea(EntityManager manager)
        {
            if (!_centerEntity.Exists())
            {
                ModLogger.Warn("[DuelSummon] Cannot configure duel area before center exists.");
                return;
            }

            if (!manager.HasComponent<DuelArea>(_centerEntity))
            {
                ModLogger.Warn("[DuelSummon] Center missing DuelArea component; cannot adjust radius.");
                return;
            }

            var duelArea = manager.GetComponentData<DuelArea>(_centerEntity);
            ModLogger.Info(
                $"[DuelSummon] Using duel area defaults radius={duelArea.Radius} originalRadius={duelArea.OriginalRadius} suddenDeathStart={duelArea.SuddenDeathStartTime} suddenDeathDuration={duelArea.SuddenDeathDuration}.",
                verboseOnly: false);
        }

        private void TraceDuelEntities(EntityManager manager)
        {
            ModLogger.Info(
                $"[DuelSummon] Finalizing duel center={_centerEntity.Index}:{_centerEntity.Version} challenger={_challengerEntity.Index}:{_challengerEntity.Version}.",
                verboseOnly: false);

            if (manager.HasComponent<VBloodDuelInstance>(_centerEntity))
            {
                var duelInstance = manager.GetComponentData<VBloodDuelInstance>(_centerEntity);
                ModLogger.Info(
                    $"[DuelSummon] Center duel instance id={duelInstance.VBloodDuelId}.",
                    verboseOnly: false);
            }
            else
            {
                ModLogger.Warn("[DuelSummon] Center entity missing VBloodDuelInstance component.");
            }

            if (manager.HasComponent<ProjectM.Contest.ContestOwner_Server>(_centerEntity))
            {
                var contest = manager.GetComponentData<ProjectM.Contest.ContestOwner_Server>(_centerEntity);
                var contestEntity = contest.ContestInstance;
                ModLogger.Info(
                    $"[DuelSummon] Center contest instance={contestEntity.Index}:{contestEntity.Version}.",
                    verboseOnly: false);
            }
            else
            {
                ModLogger.Warn("[DuelSummon] Center entity missing ContestOwner_Server component.");
            }

            if (manager.HasComponent<VBloodDuelChallenger>(_challengerEntity))
            {
                var challengerData = manager.GetComponentData<VBloodDuelChallenger>(_challengerEntity);
                ModLogger.Info(
                    $"[DuelSummon] Challenger duel id={challengerData.VBloodDuelId}.",
                    verboseOnly: false);
            }
            else
            {
                ModLogger.Warn("[DuelSummon] Challenger missing VBloodDuelChallenger component.");
            }
        }

        private void TryRegisterPlayer(EntityManager manager)
        {
            if (!_player.Exists())
            {
                ModLogger.Warn("[DuelSummon] Player entity destroyed before duel registration.");
                return;
            }

            if (!TryApplyConnectionBuff(manager, _player, out var buffEntity, "Applied duel connection buff"))
            {
                return;
            }

            LinkBuffOwner(manager, buffEntity, _player);
        }

        private void RegisterActiveDuel(EntityManager manager)
        {
            if (!_centerEntity.Exists() || !manager.HasComponent<VBloodDuelInstance>(_centerEntity))
            {
                ModLogger.Warn("[DuelSummon] Unable to register active duel; missing center duel instance.");
                return;
            }

            var duelInstance = manager.GetComponentData<VBloodDuelInstance>(_centerEntity);
            if (duelInstance.VBloodDuelId == 0)
            {
                ModLogger.Warn("[DuelSummon] Unable to register active duel; duel id is zero.");
                return;
            }

            var duel = ActiveDuels.GetOrAdd(
                duelInstance.VBloodDuelId,
                _ => new ActiveDuel(duelInstance.VBloodDuelId, _centerEntity));

            duel.AddPlayer(_player);
            duel.AddChallenger(_challengerEntity);
            ParticipantLookup[_player] = duelInstance.VBloodDuelId;
            ParticipantLookup[_challengerEntity] = duelInstance.VBloodDuelId;

            TryRegisterNearbyChallengers(manager, duel, duelInstance.VBloodDuelId);
        }

        private void TryRegisterNearbyChallengers(EntityManager manager, ActiveDuel duel, int duelId)
        {
            if (!_centerEntity.Exists())
            {
                return;
            }

            var radiusSq = float.MaxValue;
            if (manager.HasComponent<DuelArea>(_centerEntity))
            {
                var duelArea = manager.GetComponentData<DuelArea>(_centerEntity);
                var radius = math.max(duelArea.Radius, duelArea.OriginalRadius);
                radiusSq = radius * radius;
            }

            var query = manager.CreateEntityQuery(ComponentType.ReadOnly<VBloodDuelChallenger>());
            NativeArray<Entity> challengers = default;

            try
            {
                challengers = query.ToEntityArray(Allocator.Temp);

                foreach (var challenger in challengers)
                {
                    if (!challenger.Exists() || challenger == _challengerEntity)
                    {
                        continue;
                    }

                    if (!manager.TryGetComponentData(challenger, out VBloodDuelChallenger challengerData))
                    {
                        continue;
                    }

                    if (challengerData.VBloodDuelId != 0 && challengerData.VBloodDuelId != duelId)
                    {
                        continue;
                    }

                    if (!TryResolvePosition(manager, challenger, out var position))
                    {
                        continue;
                    }

                    var distanceSq = math.lengthsq(position - _centerPosition);
                    if (distanceSq > radiusSq)
                    {
                        continue;
                    }

                    challengerData.VBloodDuelId = duelId;
                    manager.SetComponentData(challenger, challengerData);

                    duel.AddChallenger(challenger);
                    ParticipantLookup[challenger] = duelId;

                    ModLogger.Info(
                        $"[DuelSummon] Added nearby challenger {challenger.Index}:{challenger.Version} to duel {duelId}.",
                        verboseOnly: false);
                }
            }
            finally
            {
                if (challengers.IsCreated)
                {
                    challengers.Dispose();
                }

                query.Dispose();
            }
        }

        private List<PlayerCandidate> InviteNearbyPlayers(EntityManager manager)
        {
            if (!_centerEntity.Exists())
            {
                ModLogger.Warn("[DuelSummon] Cannot invite nearby players without a center entity.");
                return new List<PlayerCandidate>();
            }

            if (!manager.HasComponent<DuelArea>(_centerEntity))
            {
                ModLogger.Warn("[DuelSummon] Center missing DuelArea component; skipping nearby invitations.");
                return new List<PlayerCandidate>();
            }

            var duelArea = manager.GetComponentData<DuelArea>(_centerEntity);
            var radius = math.max(duelArea.Radius, duelArea.OriginalRadius);
            var radiusSq = radius * radius;

            var candidates = BuildCandidateList(manager, radiusSq);
            if (candidates.Count == 0)
            {
                ModLogger.Info("[DuelSummon] No nearby players eligible for duel invitation.", verboseOnly: false);
                return new List<PlayerCandidate>();
            }

            candidates.Sort((a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));

            var additionalSlots = math.max(0, _maxParticipantsPerDuel - 1);
            var invited = 0;
            var accepted = 0;
            var overflow = new List<PlayerCandidate>();

            foreach (var candidate in candidates)
            {
                if (accepted < additionalSlots)
                {
                    if (!TryApplyConnectionBuff(manager, candidate.Entity, out var buffEntity, "Invited nearby player"))
                    {
                        continue;
                    }

                    LinkBuffOwner(manager, buffEntity, candidate.Entity);
                    invited++;
                    accepted++;
                    continue;
                }

                overflow.Add(candidate);
            }

            ModLogger.Info(
                $"[DuelSummon] Invited {invited} nearby players to the duel (limit {_maxParticipantsPerDuel}).",
                verboseOnly: false);

            return overflow;
        }

        private List<PlayerCandidate> BuildCandidateList(EntityManager manager, float radiusSq)
        {
            var candidates = new List<PlayerCandidate>();

            if (_explicitParticipants != null)
            {
                foreach (var candidate in _explicitParticipants)
                {
                    if (!candidate.Exists() || candidate == _player)
                    {
                        continue;
                    }

                    if (!TryResolvePosition(manager, candidate, out var position))
                    {
                        ModLogger.Debug(
                            $"[DuelSummon] Skipping explicit candidate {candidate.Index}:{candidate.Version}; unable to resolve position.");
                        continue;
                    }

                    var distanceSq = math.lengthsq(position - _centerPosition);
                    candidates.Add(new PlayerCandidate(candidate, position, distanceSq));
                }

                return candidates;
            }

            var query = manager.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
            NativeArray<Entity> players = default;

            try
            {
                players = query.ToEntityArray(Allocator.Temp);

                foreach (var candidate in players)
                {
                    if (!candidate.Exists() || candidate == _player)
                    {
                        continue;
                    }

                    if (!TryResolvePosition(manager, candidate, out var position))
                    {
                        ModLogger.Debug(
                            $"[DuelSummon] Skipping candidate {candidate.Index}:{candidate.Version}; unable to resolve position.");
                        continue;
                    }

                    var distanceSq = math.lengthsq(position - _centerPosition);
                    if (distanceSq > radiusSq)
                    {
                        continue;
                    }

                    candidates.Add(new PlayerCandidate(candidate, position, distanceSq));
                }
            }
            finally
            {
                if (players.IsCreated)
                {
                    players.Dispose();
                }

                query.Dispose();
            }

            return candidates;
        }

        private void ScheduleSupplementalDuels(EntityManager manager, List<PlayerCandidate> overflow)
        {
            if (overflow.Count == 0)
            {
                if (_supplementalDuelsAllowed > 0)
                {
                    ModLogger.Info(
                        "[DuelSummon] Supplemental duels requested but no overflow players detected; skipping extra summons.",
                        verboseOnly: false);
                }

                return;
            }

            if (_supplementalDuelsAllowed <= 0)
            {
                ModLogger.Info(
                    $"[DuelSummon] {overflow.Count} players exceeded duel capacity but no supplemental duels are permitted.",
                    verboseOnly: false);
                return;
            }

            var groups = ChunkOverflow(overflow, _maxParticipantsPerDuel);
            var scheduled = 0;
            var assignedPlayers = 0;

            foreach (var group in groups)
            {
                if (scheduled >= _supplementalDuelsAllowed)
                {
                    break;
                }

                if (group.Count == 0)
                {
                    continue;
                }

                var anchor = group[0];
                if (!anchor.Entity.Exists())
                {
                    continue;
                }

                if (!TryResolvePosition(manager, anchor.Entity, out var anchorPosition))
                {
                    ModLogger.Warn(
                        $"[DuelSummon] Unable to resolve anchor position for supplemental duel targeting {anchor.Entity.Index}:{anchor.Entity.Version}. Skipping group.");
                    continue;
                }

                var center = anchorPosition + _forward * 2.5f;
                center.y = _centerPosition.y;
                var challenger = center + _challengerOffset;

                var participants = new List<Entity>();
                for (var i = 1; i < group.Count; i++)
                {
                    participants.Add(group[i].Entity);
                }

                ModLogger.Info(
                    $"[DuelSummon] Scheduling supplemental duel {scheduled + 1} for {group.Count} players anchored to {anchor.Entity.Index}:{anchor.Entity.Version}.",
                    verboseOnly: false);

                var supplementalScheduled = DuelSummonService.TrySummonForPlayer(
                    anchor.Entity,
                    _challengerPrefab,
                    center,
                    challenger,
                    _maxParticipantsPerDuel,
                    _supplementalDuelsAllowed - scheduled - 1,
                    participants,
                    _forward);

                if (!supplementalScheduled)
                {
                    ModLogger.Warn(
                        $"[DuelSummon] Failed to schedule supplemental duel for anchor {anchor.Entity.Index}:{anchor.Entity.Version}.");
                    continue;
                }

                scheduled++;
                assignedPlayers += group.Count;
            }

            var remaining = overflow.Count - assignedPlayers;
            if (remaining > 0)
            {
                ModLogger.Warn(
                    $"[DuelSummon] {remaining} overflow players remain without a duel assignment.");
            }
        }

        private static List<List<PlayerCandidate>> ChunkOverflow(List<PlayerCandidate> overflow, int maxParticipantsPerDuel)
        {
            var groups = new List<List<PlayerCandidate>>();
            if (overflow.Count == 0 || maxParticipantsPerDuel <= 0)
            {
                return groups;
            }

            var index = 0;
            while (index < overflow.Count)
            {
                var group = new List<PlayerCandidate>(math.min(maxParticipantsPerDuel, overflow.Count - index));
                for (var i = 0; i < maxParticipantsPerDuel && index < overflow.Count; i++, index++)
                {
                    group.Add(overflow[index]);
                }

                groups.Add(group);
            }

            return groups;
        }

        private bool TryResolvePosition(EntityManager manager, Entity entity, out float3 position)
        {
            if (manager.HasComponent<LocalTransform>(entity))
            {
                position = manager.GetComponentData<LocalTransform>(entity).Position;
                return true;
            }

            if (manager.HasComponent<Translation>(entity))
            {
                position = manager.GetComponentData<Translation>(entity).Value;
                return true;
            }

            position = default;
            return false;
        }

        private readonly struct PlayerCandidate
        {
            public PlayerCandidate(Entity entity, float3 position, float distanceSquared)
            {
                Entity = entity;
                Position = position;
                DistanceSquared = distanceSquared;
            }

            public Entity Entity { get; }
            public float3 Position { get; }
            public float DistanceSquared { get; }
        }

        private bool TryApplyConnectionBuff(EntityManager manager, Entity target, out Entity buffEntity, string context)
        {
            buffEntity = Entity.Null;

            if (target.TryApplyAndGetBuff(DuelConnectionBuffPrefab, out buffEntity) && buffEntity.Exists())
            {
                ModLogger.Info(
                    $"[DuelSummon] {context} {target.Index}:{target.Version} → buff {buffEntity.Index}:{buffEntity.Version}.",
                    verboseOnly: false);
                return true;
            }

            if (target.TryGetBuff(DuelConnectionBuffPrefab, out buffEntity) && buffEntity.Exists())
            {
                ModLogger.Info(
                    $"[DuelSummon] {context} reused existing buff {buffEntity.Index}:{buffEntity.Version} on {target.Index}:{target.Version}.",
                    verboseOnly: false);
                return true;
            }

            ModLogger.Warn($"[DuelSummon] {context} failed for {target.Index}:{target.Version}; duel connection buff unavailable.");
            return false;
        }

        private void LinkBuffOwner(EntityManager manager, Entity buffEntity, Entity target)
        {
            if (!buffEntity.Exists())
            {
                ModLogger.Warn($"[DuelSummon] Cannot link duel buff owner for {target.Index}:{target.Version}; buff entity destroyed.");
                return;
            }

            if (manager.HasComponent<EntityOwner>(buffEntity))
            {
                var owner = manager.GetComponentData<EntityOwner>(buffEntity);
                owner.Owner = _challengerEntity;
                manager.SetComponentData(buffEntity, owner);
                ModLogger.Info(
                    $"[DuelSummon] Linked challenger {_challengerEntity.Index}:{_challengerEntity.Version} as buff owner for {target.Index}:{target.Version}.",
                    verboseOnly: false);
            }
            else
            {
                ModLogger.Warn("[DuelSummon] Duel connection buff missing EntityOwner component.");
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

    private static void EndActiveDuel(EntityManager manager, ActiveDuel duel)
    {
        ModLogger.Info($"[DuelSummon] Ending duel {duel.DuelId}; all participants left combat.", verboseOnly: false);

        foreach (var participant in duel.Participants)
        {
            ParticipantLookup.TryRemove(participant, out _);
        }

        ActiveDuels.TryRemove(duel.DuelId, out _);

        foreach (var challenger in duel.Challengers)
        {
            if (!challenger.Exists() || !manager.HasComponent<VBloodDuelChallenger>(challenger))
            {
                continue;
            }

            var challengerData = manager.GetComponentData<VBloodDuelChallenger>(challenger);
            challengerData.VBloodDuelId = 0;
            manager.SetComponentData(challenger, challengerData);
        }

        if (duel.Center.Exists() && manager.Exists(duel.Center))
        {
            manager.DestroyEntity(duel.Center);
        }
    }

    private sealed class ActiveDuel
    {
        private readonly HashSet<Entity> _players = new();
        private readonly HashSet<Entity> _challengers = new();
        private readonly Dictionary<Entity, bool> _combatStates = new();
        private readonly object _sync = new();

        public ActiveDuel(int duelId, Entity center)
        {
            DuelId = duelId;
            Center = center;
        }

        public int DuelId { get; }
        public Entity Center { get; }
        public IReadOnlyCollection<Entity> Challengers
        {
            get
            {
                lock (_sync)
                {
                    return _challengers.ToList();
                }
            }
        }

        public IReadOnlyCollection<Entity> Participants
        {
            get
            {
                lock (_sync)
                {
                    return _combatStates.Keys.ToList();
                }
            }
        }

        public void AddPlayer(Entity player)
        {
            lock (_sync)
            {
                _players.Add(player);
                _combatStates[player] = true;
            }
        }

        public void AddChallenger(Entity challenger)
        {
            lock (_sync)
            {
                _challengers.Add(challenger);
                _combatStates[challenger] = true;
            }
        }

        public void SetCombatState(Entity participant, bool inCombat)
        {
            lock (_sync)
            {
                _combatStates[participant] = inCombat;
            }
        }

        public bool ShouldEnd()
        {
            lock (_sync)
            {
                foreach (var pair in _combatStates.ToList())
                {
                    if (!pair.Key.Exists())
                    {
                        _combatStates[pair.Key] = false;
                    }
                }

                return _combatStates.Count > 0 && _combatStates.Values.All(state => !state);
            }
        }
    }
}

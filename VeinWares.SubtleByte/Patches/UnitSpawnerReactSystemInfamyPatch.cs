using System;
using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Services.FactionInfamy;

namespace VeinWares.SubtleByte.Patches;

[HarmonyPatch(typeof(UnitSpawnerReactSystem), nameof(UnitSpawnerReactSystem.OnUpdate))]
internal static class UnitSpawnerReactSystemInfamyPatch
{
    private static readonly EntityQueryDesc SpawnQueryDescription = new()
    {
        All = new ComponentType[]
        {
            ComponentType.ReadOnly<UnitSpawnHandler>(),
            ComponentType.ReadOnly<SpawnTag>()
        },
        Options = EntityQueryOptions.IncludeDisabled
    };

    private static bool _queryUnavailable;

    private static void Prefix(UnitSpawnerReactSystem __instance)
    {
        if (_queryUnavailable)
        {
            return;
        }

        var infamyEnabled = FactionInfamySystem.Enabled;
        var hasCallbacks = FactionInfamySpawnUtility.HasPendingCallbacks;
        var hasAmbushSpawns = infamyEnabled && FactionInfamyAmbushService.HasPendingSpawns;

        if (!hasCallbacks && !hasAmbushSpawns)
        {
            return;
        }

        EntityQuery query;
        try
        {
            query = __instance.EntityManager.CreateEntityQuery(SpawnQueryDescription);
        }
        catch (Exception)
        {
            _queryUnavailable = true;
            return;
        }

        try
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            ProcessPendingSpawns(__instance.EntityManager, query, infamyEnabled);
        }
        finally
        {
            query.Dispose();
        }
    }

    private static void ProcessPendingSpawns(EntityManager entityManager, EntityQuery query, bool infamyEnabled)
    {
        var entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!entityManager.TryGetComponentData(entity, out LifeTime lifetime))
                {
                    continue;
                }

                var handled = FactionInfamySpawnUtility.TryExecuteSpawnCallback(entityManager, entity, lifetime.Duration);
                if (handled || !infamyEnabled)
                {
                    continue;
                }

                if (FactionInfamyAmbushService.HasPendingSpawns)
                {
                    FactionInfamyAmbushService.TryHandleSpawnedEntity(entityManager, entity, lifetime.Duration);
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}

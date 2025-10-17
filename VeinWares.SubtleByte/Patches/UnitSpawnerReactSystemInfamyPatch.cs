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

    private static void Postfix(UnitSpawnerReactSystem __instance)
    {
        if (!FactionInfamySystem.Enabled || _queryUnavailable || !FactionInfamyAmbushService.HasPendingSpawns)
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

            var entityManager = __instance.EntityManager;
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

                    FactionInfamySpawnUtility.TryExecuteSpawnCallback(entityManager, entity, lifetime.Duration);
                    FactionInfamyAmbushService.TryHandleSpawnedEntity(entityManager, entity, lifetime.Duration);
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
        finally
        {
            query.Dispose();
        }
    }
}

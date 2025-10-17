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

    private static void Prefix(UnitSpawnerReactSystem __instance, out NativeArray<Entity> __state)
    {
        __state = default;

        if (!FactionInfamySystem.Enabled || _queryUnavailable)
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
            if (!query.IsEmptyIgnoreFilter)
            {
                __state = query.ToEntityArray(Allocator.Temp);
            }
        }
        finally
        {
            query.Dispose();
        }
    }

    private static void Postfix(UnitSpawnerReactSystem __instance, ref NativeArray<Entity> __state)
    {
        if (__state.IsCreated)
        {
            var entityManager = __instance.EntityManager;
            for (var i = 0; i < __state.Length; i++)
            {
                var entity = __state[i];
                if (!entityManager.TryGetComponentData(entity, out LifeTime lifetime))
                {
                    continue;
                }

                FactionInfamyAmbushService.TryHandleSpawnedEntity(entityManager, entity, lifetime.Duration);
            }

            __state.Dispose();
        }
    }
}

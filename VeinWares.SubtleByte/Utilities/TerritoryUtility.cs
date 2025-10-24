using System;
using ProjectM;
using ProjectM.CastleBuilding;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace VeinWares.SubtleByte.Utilities;

internal static class TerritoryUtility
{
    public static bool IsInsidePlayerTerritory(EntityManager entityManager, Entity playerEntity, float3 position, out int territoryIndex)
    {
        territoryIndex = -1;

        if (!TryExists(entityManager, playerEntity))
        {
            return false;
        }

        var teamEntity = ResolveTeamEntity(entityManager, playerEntity);
        if (teamEntity == Entity.Null || !TryExists(entityManager, teamEntity))
        {
            return false;
        }

        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CastleTerritory>());
        try
        {
            using var territories = query.ToEntityArray(Allocator.Temp);

            foreach (var territoryEntity in territories)
            {
                if (!entityManager.TryGetComponentData(territoryEntity, out CastleTerritory territory))
                {
                    continue;
                }

                if (territory.IsGlobalDebugTerritory)
                {
                    continue;
                }

                if (!Contains(territory.WorldBounds, position))
                {
                    continue;
                }

                var heartEntity = territory.CastleHeart;
                if (heartEntity == Entity.Null || !TryExists(entityManager, heartEntity))
                {
                    continue;
                }

                if (!entityManager.TryGetComponentData(heartEntity, out TeamReference territoryTeam))
                {
                    continue;
                }

                var ownerTeam = territoryTeam.Value;
                if (ownerTeam == Entity.Null || !TryExists(entityManager, ownerTeam))
                {
                    continue;
                }

                if (ownerTeam == teamEntity)
                {
                    territoryIndex = territory.CastleTerritoryIndex;
                    return true;
                }
            }
        }
        finally
        {
            query.Dispose();
        }

        return false;
    }

    private static Entity ResolveTeamEntity(EntityManager entityManager, Entity entity)
    {
        if (entityManager.TryGetComponentData(entity, out TeamReference teamReference) && teamReference.Value != Entity.Null)
        {
            return teamReference.Value;
        }

        if (entityManager.TryGetComponentData(entity, out PlayerCharacter playerCharacter))
        {
            var userEntity = playerCharacter.UserEntity;
            if (userEntity != Entity.Null
                && entityManager.TryGetComponentData(userEntity, out TeamReference userTeam)
                && userTeam.Value != Entity.Null)
            {
                return userTeam.Value;
            }
        }

        return Entity.Null;
    }

    private static bool Contains(BoundsMinMax bounds, float3 position)
    {
        var min = bounds.Min;
        var max = bounds.Max;
        var x = position.x;
        var z = position.z;

        return x >= min.x && x <= max.x && z >= min.y && z <= max.y;
    }

    private static bool TryExists(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null)
        {
            return false;
        }

        try
        {
            return entityManager.Exists(entity);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using Unity.Entities;
using ProjectM;
using VeinWares.SubtleByte.Extensions;
using Stunlock.Core;

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyVictimResolver
{
    private const float DefaultBaseHate = 2f;
    private const float VBloodHateMultiplier = 10f;

    public static bool TryGetHateForVictim(Entity victim, out string factionId, out float baseHate)
    {
        factionId = string.Empty;
        baseHate = 0f;

        if (!victim.TryGetComponent<FactionReference>(out var factionReference))
        {
            return false;
        }

        var factionGuid = factionReference.FactionGuid._Value;
        if (!FactionInfamyAmbushData.TryGetFactionByGuid(factionGuid.GuidHash, out factionId, out var overrideHate))
        {
            return false;
        }

        baseHate = overrideHate > 0f ? overrideHate : DefaultBaseHate;

        if (victim.Has<VBloodUnit>())
        {
            baseHate *= VBloodHateMultiplier;
        }

        return baseHate > 0f;
    }

    public static bool TryResolveFactionGuid(string factionId, out PrefabGUID factionGuid)
    {
        factionGuid = default;

        return FactionInfamyAmbushData.TryResolveFactionGuid(factionId, out factionGuid);
    }
}

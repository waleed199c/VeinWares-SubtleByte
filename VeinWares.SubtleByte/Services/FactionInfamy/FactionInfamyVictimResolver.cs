using System;
using System.Collections.Generic;
using Unity.Entities;
using ProjectM;
using VeinWares.SubtleByte.Extensions;
using Stunlock.Core;

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyVictimResolver
{
    private const float DefaultBaseHate = 10f;
    private const float VBloodHateMultiplier = 10f;

    private static readonly Dictionary<int, string> AggregatedFactionMap = new()
    {
        { PrefabsFactionIds.Bandits, "Bandits" },
        { PrefabsFactionIds.TradersT01, "Bandits" },
        { PrefabsFactionIds.Blackfangs, "Blackfangs" },
        { PrefabsFactionIds.BlackfangsLivith, "Blackfangs" },
        { PrefabsFactionIds.Militia, "Militia" },
        { PrefabsFactionIds.ChurchOfLum, "Militia" },
        { PrefabsFactionIds.ChurchOfLumSpotVampire, "Militia" },
        { PrefabsFactionIds.TradersT02, "Militia" },
        { PrefabsFactionIds.WorldPrisoners, "Militia" },
        { PrefabsFactionIds.Gloomrot, "Gloomrot" },
        { PrefabsFactionIds.Legion, "Legion" },
        { PrefabsFactionIds.Bear, "Critters" },
        { PrefabsFactionIds.Critters, "Critters" },
        { PrefabsFactionIds.Wolves, "Critters" },
        { PrefabsFactionIds.Undead, "Undead" },
        { PrefabsFactionIds.Werewolf, "Werewolf" },
        { PrefabsFactionIds.WerewolfHuman, "Werewolf" },
    };

    private static readonly Dictionary<string, PrefabGUID> AggregatedFactionReverseMap = BuildReverseMap();

    private static readonly Dictionary<int, float> BaseHateOverrides = new()
    {
        { PrefabsFactionIds.TradersT01, 300f },
        { PrefabsFactionIds.TradersT02, 300f },
        { PrefabsFactionIds.ChurchOfLumSpotVampire, 25f },
        { PrefabsFactionIds.ChurchOfLum, 15f },
        { PrefabsFactionIds.Undead, 5f },
        { PrefabsFactionIds.Werewolf, 20f },
        { PrefabsFactionIds.WerewolfHuman, 20f },
    };

    public static bool TryGetHateForVictim(Entity victim, out string factionId, out float baseHate)
    {
        factionId = string.Empty;
        baseHate = 0f;

        if (!victim.TryGetComponent<FactionReference>(out var factionReference))
        {
            return false;
        }

        var factionGuid = factionReference.FactionGuid._Value;
        if (!AggregatedFactionMap.TryGetValue(factionGuid.GuidHash, out factionId))
        {
            return false;
        }

        baseHate = BaseHateOverrides.TryGetValue(factionGuid.GuidHash, out var overrideValue)
            ? overrideValue
            : DefaultBaseHate;

        if (victim.Has<VBloodUnit>())
        {
            baseHate *= VBloodHateMultiplier;
        }

        return baseHate > 0f;
    }

    public static bool TryResolveFactionGuid(string factionId, out PrefabGUID factionGuid)
    {
        factionGuid = default;

        if (string.IsNullOrWhiteSpace(factionId))
        {
            return false;
        }

        return AggregatedFactionReverseMap.TryGetValue(factionId, out factionGuid);
    }

    private static Dictionary<string, PrefabGUID> BuildReverseMap()
    {
        var reverse = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in AggregatedFactionMap)
        {
            var key = pair.Value;
            if (reverse.ContainsKey(key))
            {
                continue;
            }

            reverse[key] = new PrefabGUID(pair.Key);
        }

        return reverse;
    }

    private static class PrefabsFactionIds
    {
        public const int Bandits = -413163549;
        public const int TradersT01 = 30052367;
        public const int Blackfangs = 932337192;
        public const int BlackfangsLivith = -1460095921;
        public const int Militia = 1057375699;
        public const int ChurchOfLum = 1094603131;
        public const int ChurchOfLumSpotVampire = 2395673;
        public const int TradersT02 = 887347866;
        public const int WorldPrisoners = 1977351396;
        public const int Gloomrot = -1632475814;
        public const int Legion = -772044125;
        public const int Bear = 1344481611;
        public const int Critters = 10678632;
        public const int Wolves = -1671358863;
        public const int Undead = 929074293;
        public const int Werewolf = -2024618997;
        public const int WerewolfHuman = 62959306;
    }
}

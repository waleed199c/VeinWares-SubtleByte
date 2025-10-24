using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM.Shared;
using Stunlock.Core;

#nullable enable

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal readonly struct AmbushUnitDefinition
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

internal sealed class AmbushSquadDefinition
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
                .GroupBy(static definition => definition.Type)
                .ToDictionary(
                    static group => group.Key,
                    static group => (IReadOnlyList<AmbushSeasonalDefinition>)group.ToArray());
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

internal sealed class AmbushSeasonalDefinition
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
}

internal enum SeasonalAmbushType
{
    Halloween
}

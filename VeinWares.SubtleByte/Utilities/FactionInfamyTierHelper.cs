using System;

namespace VeinWares.SubtleByte.Utilities;

internal static class FactionInfamyTierHelper
{
    private const int MaxTier = 5;

    public static int CalculateTier(float hateValue, float maximumHate)
    {
        var max = Math.Max(1f, maximumHate);
        if (max <= 0f)
        {
            return 1;
        }

        var normalized = Math.Clamp(hateValue / max, 0f, 1f);
        var bucket = (int)Math.Floor(normalized * MaxTier) + 1;
        if (normalized >= 0.999f)
        {
            bucket = MaxTier;
        }

        return Math.Clamp(bucket, 1, MaxTier);
    }
}

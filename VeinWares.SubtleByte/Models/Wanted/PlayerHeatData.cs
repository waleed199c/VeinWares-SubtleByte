using System;
using System.Collections.Generic;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Models.Wanted;

internal sealed class PlayerHeatData
{
    private readonly LazyDictionary<string, HeatEntry> _factionHeat = new(() => new HeatEntry());

    public DateTime LastCombatStart { get; set; }

    public DateTime LastCombatEnd { get; set; }

    public IReadOnlyDictionary<string, HeatEntry> FactionHeat => _factionHeat.AsReadOnly();

    public HeatEntry GetHeat(string factionId) => _factionHeat[factionId];

    public void SetHeat(string factionId, HeatEntry entry)
    {
        _factionHeat[factionId] = entry;
    }

    public bool ClearFaction(string factionId)
    {
        return _factionHeat.Remove(factionId);
    }

    public Dictionary<string, HeatEntry> ExportSnapshot()
    {
        var result = new Dictionary<string, HeatEntry>(_factionHeat.Count);
        foreach (var pair in _factionHeat)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    public bool RunCooldown(float decayPerSecond, float deltaSeconds, float removalThreshold)
    {
        if (_factionHeat.Count == 0 || decayPerSecond <= 0f || deltaSeconds <= 0f)
        {
            return false;
        }

        var toRemove = new List<string>();
        var changed = false;
        var decayAmount = decayPerSecond * deltaSeconds;

        foreach (var pair in _factionHeat)
        {
            var entry = pair.Value;
            if (entry.Heat <= 0f)
            {
                if (entry.Heat <= removalThreshold)
                {
                    toRemove.Add(pair.Key);
                }

                continue;
            }

            var newHeat = MathF.Max(0f, entry.Heat - decayAmount);
            if (MathF.Abs(newHeat - entry.Heat) > 0.001f)
            {
                changed = true;
            }

            entry.Heat = newHeat;
            entry.LastUpdated = DateTime.UtcNow;
            _factionHeat[pair.Key] = entry;

            if (newHeat <= removalThreshold)
            {
                toRemove.Add(pair.Key);
            }
        }

        foreach (var faction in toRemove)
        {
            _factionHeat.Remove(faction);
            changed = true;
        }

        return changed;
    }
}

internal struct HeatEntry
{
    public float Heat { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime LastAmbush { get; set; }
}

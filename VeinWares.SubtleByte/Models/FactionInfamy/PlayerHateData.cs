using System;
using System.Collections.Generic;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Models.FactionInfamy;

internal sealed class PlayerHateData
{
    private readonly LazyDictionary<string, HateEntry> _factionHate = new(() => new HateEntry());

    public DateTime LastCombatStart { get; set; }

    public DateTime LastCombatEnd { get; set; }

    public IReadOnlyDictionary<string, HateEntry> FactionHate => _factionHate.AsReadOnly();

    public HateEntry GetHate(string factionId) => _factionHate[factionId];

    public void SetHate(string factionId, HateEntry entry)
    {
        _factionHate[factionId] = entry;
    }

    public bool ClearFaction(string factionId)
    {
        return _factionHate.Remove(factionId);
    }

    public Dictionary<string, HateEntry> ExportSnapshot()
    {
        var result = new Dictionary<string, HateEntry>(_factionHate.Count);
        foreach (var pair in _factionHate)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    public bool RunCooldown(float decayPerSecond, float deltaSeconds, float removalThreshold)
    {
        if (_factionHate.Count == 0 || decayPerSecond <= 0f || deltaSeconds <= 0f)
        {
            return false;
        }

        var toRemove = new List<string>();
        var changed = false;
        var decayAmount = decayPerSecond * deltaSeconds;

        foreach (var pair in _factionHate)
        {
            var entry = pair.Value;
            if (entry.Hate <= 0f)
            {
                if (entry.Hate <= removalThreshold)
                {
                    toRemove.Add(pair.Key);
                }

                continue;
            }

            var newHate = MathF.Max(0f, entry.Hate - decayAmount);
            if (MathF.Abs(newHate - entry.Hate) > 0.001f)
            {
                changed = true;
            }

            entry.Hate = newHate;
            entry.LastUpdated = DateTime.UtcNow;
            _factionHate[pair.Key] = entry;

            if (newHate <= removalThreshold)
            {
                toRemove.Add(pair.Key);
            }
        }

        foreach (var faction in toRemove)
        {
            _factionHate.Remove(faction);
            changed = true;
        }

        return changed;
    }
}

internal struct HateEntry
{
    public float Hate { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime LastAmbush { get; set; }
}

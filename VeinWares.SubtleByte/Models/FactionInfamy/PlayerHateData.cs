using System;
using System.Collections.Generic;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Models.FactionInfamy;

internal sealed class PlayerHateData
{
    private readonly LazyDictionary<string, HateEntry> _factionHate = new(() => new HateEntry());

    public DateTime LastCombatStart { get; set; }

    public DateTime LastCombatEnd { get; set; }

    public bool InCombat { get; set; }

    public IReadOnlyDictionary<string, HateEntry> FactionHate => _factionHate.AsReadOnly();

    public HateEntry GetHate(string factionId) => _factionHate[factionId];

    public bool TryGetHate(string factionId, out HateEntry entry) => _factionHate.TryGetValue(factionId, out entry);

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

    public bool RunCooldown(float decayPerSecond, float deltaSeconds, float removalThreshold, float maximumHate)
    {
        if (_factionHate.Count == 0 || decayPerSecond <= 0f || deltaSeconds <= 0f)
        {
            return false;
        }

        var toRemove = new List<string>();
        var updates = new List<KeyValuePair<string, HateEntry>>(_factionHate.Count);
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

            var newHate = Math.Max(0f, entry.Hate - decayAmount);
            if (Math.Abs(newHate - entry.Hate) > 0.001f)
            {
                changed = true;
            }

            entry.Hate = newHate;
            entry.LastUpdated = DateTime.UtcNow;
            entry.LastAnnouncedTier = FactionInfamyTierHelper.CalculateTier(newHate, maximumHate);
            updates.Add(new KeyValuePair<string, HateEntry>(pair.Key, entry));

            if (newHate <= removalThreshold)
            {
                toRemove.Add(pair.Key);
            }
        }

        for (var i = 0; i < updates.Count; i++)
        {
            var update = updates[i];
            _factionHate[update.Key] = update.Value;
        }

        for (var i = 0; i < toRemove.Count; i++)
        {
            var faction = toRemove[i];
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

    public int LastAnnouncedTier { get; set; }
}

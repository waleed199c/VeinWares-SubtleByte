using System;
using System.Collections.Concurrent;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamySpawnUtility
{
    private static readonly Entity PlaceholderEntity = new();
    private static readonly ConcurrentDictionary<int, PendingSpawnCallback> PendingCallbacks = new();

    public static float EncodeLifetime(int lifetimeSeconds, int level, SpawnFaction faction)
    {
        lifetimeSeconds = Math.Clamp(lifetimeSeconds / 10 * 10, 10, 990);
        var factionDigit = Math.Clamp((int)faction, 0, 9);
        level = Math.Clamp(level, 1, 99);

        var factionComponent = factionDigit;
        var levelComponent = level / 100f;
        var checksumComponent = level / 10000f;

        return lifetimeSeconds + factionComponent + levelComponent + checksumComponent;
    }

    public static bool TryDecodeLifetime(float encodedLifetime, out int level, out SpawnFaction faction)
    {
        var factionDigit = (int)(encodedLifetime % 10);
        faction = Enum.IsDefined(typeof(SpawnFaction), factionDigit)
            ? (SpawnFaction)factionDigit
            : SpawnFaction.Default;

        var levelSection = (encodedLifetime % 1) * 100;
        level = (int)levelSection;

        if (encodedLifetime > 1000 || level <= 0)
        {
            return false;
        }

        var checksumSection = (int)Math.Round((levelSection % 1) * 100);
        if (checksumSection != level)
        {
            switch (level)
            {
                case 15:
                case 40:
                    checksumSection -= 1;
                    break;
                case 54:
                    checksumSection += 1;
                    break;
            }
        }

        return checksumSection == level;
    }

    public static void SpawnUnit(
        PrefabGUID prefab,
        float3 position,
        int count,
        float minRange,
        float maxRange,
        float lifetime,
        Action<EntityManager, Entity>? preFinalize = null)
    {
        if (preFinalize != null)
        {
            var key = BitConverter.SingleToInt32Bits(lifetime);
            PendingCallbacks[key] = new PendingSpawnCallback(count, preFinalize);
        }

        Core.Server.GetExistingSystemManaged<UnitSpawnerUpdateSystem>().SpawnUnit(
            PlaceholderEntity,
            prefab,
            position,
            count,
            minRange,
            maxRange,
            lifetime);
    }

    public static void TryExecuteSpawnCallback(EntityManager entityManager, Entity entity, float lifetime)
    {
        var key = BitConverter.SingleToInt32Bits(lifetime);
        if (!PendingCallbacks.TryGetValue(key, out var callback))
        {
            return;
        }

        try
        {
            callback.Invoke(entityManager, entity);
        }
        catch
        {
            // Suppress any callback errors to avoid breaking spawn flow.
        }

        if (callback.Decrement() <= 0)
        {
            PendingCallbacks.TryRemove(key, out _);
        }
    }

    public static void CancelSpawnCallback(float lifetime)
    {
        var key = BitConverter.SingleToInt32Bits(lifetime);
        PendingCallbacks.TryRemove(key, out _);
    }

    private sealed class PendingSpawnCallback
    {
        private int _remaining;
        private readonly Action<EntityManager, Entity> _callback;

        public PendingSpawnCallback(int remaining, Action<EntityManager, Entity> callback)
        {
            _remaining = Math.Max(1, remaining);
            _callback = callback;
        }

        public int Decrement()
        {
            return System.Threading.Interlocked.Decrement(ref _remaining);
        }

        public void Invoke(EntityManager entityManager, Entity entity)
        {
            _callback(entityManager, entity);
        }
    }
}

internal enum SpawnFaction
{
    Default = 0,
    VampireHunters = 1,
    WantedUnit = 2
}

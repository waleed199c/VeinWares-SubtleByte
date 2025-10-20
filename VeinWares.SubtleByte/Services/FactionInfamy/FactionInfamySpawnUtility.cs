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
    private static int _markerSequence = 100_000;

    public static bool HasPendingCallbacks => !PendingCallbacks.IsEmpty;

    public static int SpawnUnit(
        PrefabGUID prefab,
        float3 position,
        int count,
        float minRange,
        float maxRange,
        float lifetime,
        Action<EntityManager, Entity, int, float>? preFinalize = null)
    {
        var marker = System.Threading.Interlocked.Increment(ref _markerSequence);

        if (preFinalize != null)
        {
            PendingCallbacks[marker] = new PendingSpawnCallback(count, lifetime, preFinalize);
        }

        Core.Server.GetExistingSystemManaged<UnitSpawnerUpdateSystem>().SpawnUnit(
            PlaceholderEntity,
            prefab,
            position,
            count,
            minRange,
            maxRange,
            marker);

        return marker;
    }

    public static bool TryExecuteSpawnCallback(EntityManager entityManager, Entity entity, float lifetime)
    {
        var marker = (int)Math.Round(lifetime);
        if (!PendingCallbacks.TryGetValue(marker, out var callback))
        {
            return false;
        }

        try
        {
            callback.Invoke(entityManager, entity, marker);
        }
        catch
        {
            // Suppress any callback errors to avoid breaking spawn flow.
        }

        if (callback.Decrement() <= 0)
        {
            PendingCallbacks.TryRemove(marker, out _);
        }

        return true;
    }

    public static void CancelSpawnCallback(int marker)
    {
        PendingCallbacks.TryRemove(marker, out _);
    }

    private sealed class PendingSpawnCallback
    {
        private int _remaining;
        private readonly float _lifetime;
        private readonly Action<EntityManager, Entity, int, float> _callback;

        public PendingSpawnCallback(int remaining, float lifetime, Action<EntityManager, Entity, int, float> callback)
        {
            _remaining = Math.Max(1, remaining);
            _lifetime = lifetime;
            _callback = callback;
        }

        public int Decrement()
        {
            return System.Threading.Interlocked.Decrement(ref _remaining);
        }

        public void Invoke(EntityManager entityManager, Entity entity, int marker)
        {
            _callback(entityManager, entity, marker, _lifetime);
        }
    }
}

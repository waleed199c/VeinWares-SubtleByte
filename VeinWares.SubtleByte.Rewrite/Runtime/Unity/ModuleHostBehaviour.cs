using System;
using UnityEngine;

#nullable enable

namespace VeinWares.SubtleByte.Rewrite.Runtime.Unity;

public sealed class ModuleHostBehaviour : MonoBehaviour
{
    internal static Action<float>? TickHandler { get; set; }

    private float _lastUpdateTimestamp;

    private void Awake()
    {
        _lastUpdateTimestamp = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        var now = Time.realtimeSinceStartup;
        var delta = now - _lastUpdateTimestamp;
        _lastUpdateTimestamp = now;

        if (delta <= 0f)
        {
            delta = Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime;
        }

        TickHandler?.Invoke(delta);
    }

    private void OnDestroy()
    {
        TickHandler = null;
    }
}

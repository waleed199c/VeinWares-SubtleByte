using System;
using UnityEngine;

#nullable enable

namespace VeinWares.SubtleByte.Runtime.Unity;

public sealed class ModuleHostBehaviour : MonoBehaviour
{
    internal static Action<float>? TickHandler { get; set; }

    private void Update()
    {
        TickHandler?.Invoke(Time.deltaTime);
    }

    private void OnDestroy()
    {
        TickHandler = null;
    }
}

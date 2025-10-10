using System;
using BepInEx.Logging;
using UnityEngine;
using VeinWares.SubtleByte.Rewrite.Infrastructure;

namespace VeinWares.SubtleByte.Rewrite.Runtime.Unity;

public sealed class ServerBootstrap : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly ModuleHost _host;
    private readonly GameObject _root;
    private readonly ModuleHostBehaviour _behaviour;
    private readonly Action<float> _tickHandler;

    private ServerBootstrap(ModuleHost host, GameObject root, ModuleHostBehaviour behaviour, Action<float> tickHandler, ManualLogSource log)
    {
        _host = host;
        _root = root;
        _behaviour = behaviour;
        _tickHandler = tickHandler;
        _log = log;
    }

    public static ServerBootstrap Start(ModuleHost host, ManualLogSource log)
    {
        var go = new GameObject("SubtleByte.ModuleHost");
        UnityEngine.Object.DontDestroyOnLoad(go);
        var behaviour = go.AddComponent<ModuleHostBehaviour>();
        Action<float> tickHandler = host.Tick;
        ModuleHostBehaviour.TickHandler = tickHandler;
        log.LogDebug("ServerBootstrap created persistent host GameObject.");
        return new ServerBootstrap(host, go, behaviour, tickHandler, log);
    }

    public void Dispose()
    {
        try
        {
            if (_behaviour != null)
            {
                _behaviour.enabled = false;
            }

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            if (ModuleHostBehaviour.TickHandler == _tickHandler)
            {
                ModuleHostBehaviour.TickHandler = null;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning($"Failed to clean up ServerBootstrap GameObject: {ex}");
        }
    }
}

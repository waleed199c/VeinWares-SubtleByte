using System;
using BepInEx.Logging;
using UnityEngine;
using VeinWares.SubtleByte.Template.Infrastructure;

namespace VeinWares.SubtleByte.Template.Runtime.Unity;

public sealed class ServerBootstrap : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly ModuleHost _host;
    private readonly GameObject _root;
    private readonly ModuleHostBehaviour _behaviour;

    private ServerBootstrap(ModuleHost host, GameObject root, ModuleHostBehaviour behaviour, ManualLogSource log)
    {
        _host = host;
        _root = root;
        _behaviour = behaviour;
        _log = log;
    }

    public static ServerBootstrap Start(ModuleHost host, ManualLogSource log)
    {
        var go = new GameObject("SubtleByte.ModuleHost");
        UnityEngine.Object.DontDestroyOnLoad(go);
        var behaviour = go.AddComponent<ModuleHostBehaviour>();
        ModuleHostBehaviour.Host = host;
        log.LogDebug("ServerBootstrap created persistent host GameObject.");
        return new ServerBootstrap(host, go, behaviour, log);
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

            if (ReferenceEquals(ModuleHostBehaviour.Host, _host))
            {
                ModuleHostBehaviour.Host = null;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning($"Failed to clean up ServerBootstrap GameObject: {ex}");
        }
    }
}

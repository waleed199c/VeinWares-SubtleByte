using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using VeinWares.SubtleByte.Rewrite.Configuration;
using VeinWares.SubtleByte.Rewrite.Infrastructure.Diagnostics;
using VeinWares.SubtleByte.Rewrite.Runtime.Scheduling;

namespace VeinWares.SubtleByte.Rewrite.Infrastructure;

public sealed class ModuleHost : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly PerformanceTracker _performanceTracker;
    private readonly IReadOnlyList<Func<IModule>> _moduleFactories;
    private readonly List<IModule> _modules = new();
    private readonly IntervalScheduler _scheduler = new();
    private readonly List<IUpdateModule> _updateModules = new();
    private readonly Harmony _harmony;
    private readonly RewriteConfig _config;

    private ModuleHost(
        ManualLogSource log,
        PerformanceTracker performanceTracker,
        IReadOnlyList<Func<IModule>> moduleFactories,
        Harmony harmony,
        RewriteConfig config)
    {
        _log = log;
        _performanceTracker = performanceTracker;
        _moduleFactories = moduleFactories;
        _harmony = harmony;
        _config = config;
    }

    public int ModuleCount => _modules.Count;

    public static ModuleHost Create(
        ManualLogSource log,
        PerformanceTracker performanceTracker,
        IReadOnlyList<Func<IModule>> moduleFactories,
        RewriteConfig config)
    {
        return new ModuleHost(
            log,
            performanceTracker,
            moduleFactories,
            new Harmony("veinwares.subtlebyte.rewrite.modules"),
            config);
    }

    public void Initialize()
    {
        if (_modules.Count > 0)
        {
            _log.LogWarning("ModuleHost.Initialize called more than once. Ignoring subsequent invocation.");
            return;
        }

        var context = new ModuleContext(_log, _scheduler, _harmony, _performanceTracker, _config);

        foreach (var factory in _moduleFactories)
        {
            IModule module;
            try
            {
                module = factory();
            }
            catch (Exception ex)
            {
                _log.LogError($"Failed to create module from factory {factory.Method.DeclaringType?.FullName ?? "<unknown>"}: {ex}");
                continue;
            }

            try
            {
                _performanceTracker.Measure(module.GetType().Name + ".Initialize", () => module.Initialize(context));
                _modules.Add(module);
                if (module is IUpdateModule updateModule)
                {
                    _updateModules.Add(updateModule);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"Failed to initialize module {module.GetType().FullName}: {ex}");
                module.Dispose();
            }
        }
    }

    public void Tick(float deltaTime)
    {
        _scheduler.Update(deltaTime);

        foreach (var module in _updateModules)
        {
            var name = module.GetType().Name + ".Update";
            try
            {
                _performanceTracker.Measure(name, () => module.OnUpdate(deltaTime));
            }
            catch (Exception ex)
            {
                _log.LogError($"Unhandled exception while ticking module {module.GetType().FullName}: {ex}");
            }
        }
    }

    public void Dispose()
    {
        for (var i = _modules.Count - 1; i >= 0; i--)
        {
            try
            {
                _modules[i].Dispose();
            }
            catch (Exception ex)
            {
                _log.LogError($"Module {_modules[i].GetType().FullName} threw during Dispose: {ex}");
            }
        }

        _modules.Clear();
        _updateModules.Clear();
        _scheduler.Dispose();
        _harmony.UnpatchSelf();
    }
}

using System;
using BepInEx.Configuration;

namespace VeinWares.SubtleByte.Config;

internal static class WantedConfig
{
    private static bool _initialized;
    private static ConfigEntry<bool> _ambushesEnabled;
    private static ConfigEntry<float> _heatGainMultiplier;
    private static ConfigEntry<float> _heatDecayPerMinute;
    private static ConfigEntry<float> _cooldownGraceSeconds;
    private static ConfigEntry<float> _combatCooldownSeconds;
    private static ConfigEntry<float> _ambushCooldownMinutes;
    private static ConfigEntry<float> _minimumAmbushHeat;
    private static ConfigEntry<int> _maximumHeat;
    private static ConfigEntry<int> _autosaveMinutes;
    private static ConfigEntry<int> _autosaveBackups;

    public static void Initialize(ConfigFile configFile)
    {
        if (_initialized)
        {
            return;
        }

        if (configFile is null)
        {
            throw new ArgumentNullException(nameof(configFile));
        }

        _ambushesEnabled = configFile.Bind(
            "Wanted System",
            "Enable Ambush Spawns",
            true,
            "Controls whether the wanted system is allowed to spawn ambush squads when player heat crosses the configured threshold.");

        _heatGainMultiplier = configFile.Bind(
            "Wanted System",
            "Heat Gain Multiplier",
            1.0f,
            "Scales the amount of heat granted per qualifying kill. Values below zero will be clamped to zero.");

        _heatDecayPerMinute = configFile.Bind(
            "Wanted System",
            "Heat Decay Per Minute",
            10.0f,
            "Amount of heat removed from every active faction bucket each minute while the player is eligible for cooldown.");

        _cooldownGraceSeconds = configFile.Bind(
            "Wanted System",
            "Cooldown Grace Seconds",
            30.0f,
            "How long after combat the player must remain idle before their heat begins to decay.");

        _combatCooldownSeconds = configFile.Bind(
            "Wanted System",
            "Combat Cooldown Seconds",
            15.0f,
            "Minimum time between combat triggers that will reset the heat decay timer.");

        _ambushCooldownMinutes = configFile.Bind(
            "Wanted System",
            "Ambush Cooldown Minutes",
            15.0f,
            "Minimum number of minutes that must pass before the same player can be ambushed again.");

        _minimumAmbushHeat = configFile.Bind(
            "Wanted System",
            "Minimum Ambush Heat",
            50.0f,
            "Players must reach this heat level (after multipliers) before ambush squads are considered.");

        _maximumHeat = configFile.Bind(
            "Wanted System",
            "Maximum Heat",
            300,
            "Upper bound for heat per faction. Any calculated heat beyond this value will be clamped.");

        _autosaveMinutes = configFile.Bind(
            "Wanted System",
            "Autosave Minutes",
            5,
            "Interval, in minutes, for persisting wanted heat data to disk. Minimum of one minute.");

        _autosaveBackups = configFile.Bind(
            "Wanted System",
            "Autosave Backups",
            3,
            "Number of rolling backup files to keep whenever the wanted system saves the heat database.");

        ClampValues();

        _initialized = true;
    }

    public static WantedConfigSnapshot CreateSnapshot()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("WantedConfig.Initialize must be called before creating a snapshot.");
        }

        ClampValues();

        return new WantedConfigSnapshot(
            _ambushesEnabled.Value,
            MathF.Max(0f, _heatGainMultiplier.Value),
            MathF.Max(0f, _heatDecayPerMinute.Value) / 60f,
            TimeSpan.FromSeconds(MathF.Max(0f, _cooldownGraceSeconds.Value)),
            TimeSpan.FromSeconds(MathF.Max(1f, _combatCooldownSeconds.Value)),
            TimeSpan.FromMinutes(MathF.Max(1f, _ambushCooldownMinutes.Value)),
            MathF.Max(0f, _minimumAmbushHeat.Value),
            Math.Max(1, _maximumHeat.Value),
            TimeSpan.FromMinutes(Math.Max(1, _autosaveMinutes.Value)),
            Math.Clamp(_autosaveBackups.Value, 0, 20));
    }

    private static void ClampValues()
    {
        if (_heatGainMultiplier.Value < 0f)
        {
            _heatGainMultiplier.Value = 0f;
        }

        if (_heatDecayPerMinute.Value < 0f)
        {
            _heatDecayPerMinute.Value = 0f;
        }

        if (_cooldownGraceSeconds.Value < 0f)
        {
            _cooldownGraceSeconds.Value = 0f;
        }

        if (_combatCooldownSeconds.Value < 1f)
        {
            _combatCooldownSeconds.Value = 1f;
        }

        if (_ambushCooldownMinutes.Value < 1f)
        {
            _ambushCooldownMinutes.Value = 1f;
        }

        if (_minimumAmbushHeat.Value < 0f)
        {
            _minimumAmbushHeat.Value = 0f;
        }

        if (_maximumHeat.Value < 1)
        {
            _maximumHeat.Value = 1;
        }

        if (_autosaveMinutes.Value < 1)
        {
            _autosaveMinutes.Value = 1;
        }

        if (_autosaveBackups.Value < 0)
        {
            _autosaveBackups.Value = 0;
        }
    }
}

internal readonly record struct WantedConfigSnapshot(
    bool AmbushesEnabled,
    float HeatGainMultiplier,
    float HeatDecayPerSecond,
    TimeSpan CooldownGrace,
    TimeSpan CombatCooldown,
    TimeSpan AmbushCooldown,
    float MinimumAmbushHeat,
    int MaximumHeat,
    TimeSpan AutosaveInterval,
    int AutosaveBackupCount);

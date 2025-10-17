using System;
using BepInEx.Configuration;

namespace VeinWares.SubtleByte.Config;

internal static class FactionInfamyConfig
{
    private static bool _initialized;
    private static ConfigEntry<float> _hateGainMultiplier;
    private static ConfigEntry<float> _hateDecayPerMinute;
    private static ConfigEntry<float> _cooldownGraceSeconds;
    private static ConfigEntry<float> _combatCooldownSeconds;
    private static ConfigEntry<float> _ambushCooldownMinutes;
    private static ConfigEntry<int> _ambushChancePercent;
    private static ConfigEntry<float> _ambushLifetimeSeconds;
    private static ConfigEntry<float> _minimumAmbushHate;
    private static ConfigEntry<int> _maximumHate;
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

        _hateGainMultiplier = configFile.Bind(
            "Faction Infamy",
            "Hate Gain Multiplier",
            1.0f,
            "Scales the amount of hate granted per qualifying kill. Values below zero will be clamped to zero.");

        _hateDecayPerMinute = configFile.Bind(
            "Faction Infamy",
            "Hate Decay Per Minute",
            10.0f,
            "Amount of hate removed from every active faction bucket each minute while the player is eligible for cooldown.");

        _cooldownGraceSeconds = configFile.Bind(
            "Faction Infamy",
            "Cooldown Grace Seconds",
            30.0f,
            "How long after combat the player must remain idle before their hate begins to fade.");

        _combatCooldownSeconds = configFile.Bind(
            "Faction Infamy",
            "Combat Cooldown Seconds",
            15.0f,
            "Minimum time between combat triggers that will reset the hate decay timer.");

        _ambushCooldownMinutes = configFile.Bind(
            "Faction Infamy",
            "Ambush Cooldown Minutes",
            15.0f,
            "Minimum number of minutes that must pass before the same player can be ambushed again.");

        _ambushChancePercent = configFile.Bind(
            "Faction Infamy",
            "Ambush Chance Percent",
            50,
            "Percent chance that an eligible combat engagement will spawn an ambush squad.");

        _ambushLifetimeSeconds = configFile.Bind(
            "Faction Infamy",
            "Ambush Squad Lifetime Seconds",
            300.0f,
            "How long ambush squads remain alive before despawning automatically.");

        _minimumAmbushHate = configFile.Bind(
            "Faction Infamy",
            "Minimum Ambush Hate",
            50.0f,
            "Players must reach this hate level (after multipliers) before ambush squads are considered.");

        _maximumHate = configFile.Bind(
            "Faction Infamy",
            "Maximum Hate",
            300,
            "Upper bound for hate per faction. Any calculated hate beyond this value will be clamped.");

        _autosaveMinutes = configFile.Bind(
            "Faction Infamy",
            "Autosave Minutes",
            5,
            "Interval, in minutes, for persisting infamy hate data to disk. Minimum of one minute.");

        _autosaveBackups = configFile.Bind(
            "Faction Infamy",
            "Autosave Backups",
            3,
            "Number of rolling backup files to keep whenever the Faction Infamy system saves the hate database.");

        ClampValues();

        _initialized = true;
    }

    public static FactionInfamyConfigSnapshot CreateSnapshot()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("FactionInfamyConfig.Initialize must be called before creating a snapshot.");
        }

        ClampValues();

        return new FactionInfamyConfigSnapshot(
            Math.Max(0f, _hateGainMultiplier.Value),
            Math.Max(0f, _hateDecayPerMinute.Value) / 60f,
            TimeSpan.FromSeconds(Math.Max(0f, _cooldownGraceSeconds.Value)),
            TimeSpan.FromSeconds(Math.Max(1f, _combatCooldownSeconds.Value)),
            TimeSpan.FromMinutes(Math.Max(1f, _ambushCooldownMinutes.Value)),
            Math.Clamp(_ambushChancePercent.Value, 0, 100),
            TimeSpan.FromSeconds(Math.Max(10f, _ambushLifetimeSeconds.Value)),
            Math.Max(0f, _minimumAmbushHate.Value),
            Math.Max(1, _maximumHate.Value),
            TimeSpan.FromMinutes(Math.Max(1, _autosaveMinutes.Value)),
            Math.Clamp(_autosaveBackups.Value, 0, 20));
    }

    private static void ClampValues()
    {
        if (_hateGainMultiplier.Value < 0f)
        {
            _hateGainMultiplier.Value = 0f;
        }

        if (_hateDecayPerMinute.Value < 0f)
        {
            _hateDecayPerMinute.Value = 0f;
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

        if (_ambushChancePercent.Value < 0)
        {
            _ambushChancePercent.Value = 0;
        }

        if (_ambushChancePercent.Value > 100)
        {
            _ambushChancePercent.Value = 100;
        }

        if (_ambushLifetimeSeconds.Value < 10f)
        {
            _ambushLifetimeSeconds.Value = 10f;
        }

        if (_minimumAmbushHate.Value < 0f)
        {
            _minimumAmbushHate.Value = 0f;
        }

        if (_maximumHate.Value < 1)
        {
            _maximumHate.Value = 1;
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

internal readonly record struct FactionInfamyConfigSnapshot(
    float HateGainMultiplier,
    float HateDecayPerSecond,
    TimeSpan CooldownGrace,
    TimeSpan CombatCooldown,
    TimeSpan AmbushCooldown,
    int AmbushChancePercent,
    TimeSpan AmbushLifetime,
    float MinimumAmbushHate,
    int MaximumHate,
    TimeSpan AutosaveInterval,
    int AutosaveBackupCount);

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
    private static ConfigEntry<bool> _enableHalloweenAmbush;
    private static ConfigEntry<int> _halloweenScarecrowMinimum;
    private static ConfigEntry<int> _halloweenScarecrowMaximum;
    private static ConfigEntry<int> _halloweenScarecrowRareMultiplier;
    private static ConfigEntry<int> _halloweenScarecrowRareChancePercent;
    private static ConfigEntry<int> _seasonalFollowUpChancePercent;
    private static ConfigEntry<int> _seasonalFollowUpMinimum;
    private static ConfigEntry<int> _seasonalFollowUpMaximum;
    private static ConfigEntry<bool> _enableEliteAmbush;
    private static ConfigEntry<float> _eliteHealthMultiplier;
    private static ConfigEntry<float> _eliteDamageReductionMultiplier;
    private static ConfigEntry<float> _eliteResistanceMultiplier;
    private static ConfigEntry<float> _elitePowerMultiplier;
    private static ConfigEntry<float> _eliteAttackSpeedMultiplier;
    private static ConfigEntry<float> _eliteSpellSpeedMultiplier;
    private static ConfigEntry<float> _eliteMoveSpeedMultiplier;
    private static ConfigEntry<float> _eliteKnockbackResistanceMultiplier;
    private static ConfigEntry<float> _eliteRepresentativeHealthRatio;
    private static ConfigEntry<float> _eliteRepresentativeHealthAdditive;
    private static ConfigEntry<float> _eliteRepresentativeDamageReductionRatio;
    private static ConfigEntry<float> _eliteRepresentativeDamageReductionAdditive;
    private static ConfigEntry<float> _eliteRepresentativeResistanceRatio;
    private static ConfigEntry<float> _eliteRepresentativeResistanceAdditive;
    private static ConfigEntry<float> _eliteRepresentativePowerRatio;
    private static ConfigEntry<float> _eliteRepresentativePowerAdditive;
    private static ConfigEntry<float> _eliteRepresentativeAttackSpeedRatio;
    private static ConfigEntry<float> _eliteRepresentativeAttackSpeedAdditive;
    private static ConfigEntry<float> _eliteRepresentativeSpellSpeedRatio;
    private static ConfigEntry<float> _eliteRepresentativeSpellSpeedAdditive;
    private static ConfigEntry<float> _eliteRepresentativeMoveSpeedRatio;
    private static ConfigEntry<float> _eliteRepresentativeMoveSpeedAdditive;
    private static ConfigEntry<float> _eliteRepresentativeKnockbackResistanceRatio;
    private static ConfigEntry<float> _eliteRepresentativeKnockbackResistanceAdditive;
    private static ConfigEntry<bool> _enableAmbushKnockbackResistance;

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

        _enableHalloweenAmbush = configFile.Bind(
            "Faction Infamy",
            "Enable Halloween Ambush",
            false,
            "When true, Tier 5 ambush squads can include seasonal scarecrow reinforcements.");

        _halloweenScarecrowMinimum = configFile.Bind(
            "Faction Infamy",
            "Halloween Scarecrow Minimum",
            1,
            "Minimum number of scarecrows to spawn alongside Tier 5 ambush squads when the Halloween event is enabled.");

        _halloweenScarecrowMaximum = configFile.Bind(
            "Faction Infamy",
            "Halloween Scarecrow Maximum",
            3,
            "Maximum number of scarecrows to spawn alongside Tier 5 ambush squads when the Halloween event is enabled.");

        _halloweenScarecrowRareMultiplier = configFile.Bind(
            "Faction Infamy",
            "Halloween Scarecrow Rare Multiplier",
            2,
            "Multiplier applied to the rolled scarecrow count when the rare Halloween scarecrow chance succeeds.");

        _halloweenScarecrowRareChancePercent = configFile.Bind(
            "Faction Infamy",
            "Halloween Scarecrow Rare Chance Percent",
            5,
            "Percent chance that the scarecrow roll will be multiplied by the rare multiplier value. Set to zero to disable the rare roll.");

        _seasonalFollowUpChancePercent = configFile.Bind(
            "Faction Infamy",
            "Halloween Follow-Up Wave Chance Percent",
            0,
            "Percent chance that a second seasonal ambush wave spawns after the core squad. Set to zero to disable follow-up waves.");

        _seasonalFollowUpMinimum = configFile.Bind(
            "Faction Infamy",
            "Halloween Follow-Up Wave Minimum",
            1,
            "Minimum number of shared seasonal units to spawn in the follow-up wave when enabled.");

        _seasonalFollowUpMaximum = configFile.Bind(
            "Faction Infamy",
            "Halloween Follow-Up Wave Maximum",
            2,
            "Maximum number of shared seasonal units to spawn in the follow-up wave when enabled. Values below the minimum will be clamped.");

        _enableEliteAmbush = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Enable Elite Ambush",
            false,
            "When true, Tier 5 ambush squads receive elite stat multipliers and representative bonuses.");

        _eliteHealthMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Health Multiplier",
            1.0f,
            "Multiplier applied to ambush unit health when elite ambushes are enabled.");

        _eliteDamageReductionMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Damage Reduction Multiplier",
            1.0f,
            "Multiplier applied to ambush unit damage reduction stats when elite ambushes are enabled.");

        _eliteResistanceMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Resistance Multiplier",
            1.0f,
            "Multiplier applied to ambush unit resistances when elite ambushes are enabled.");

        _elitePowerMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Power Multiplier",
            1.0f,
            "Multiplier applied to ambush unit physical and spell power when elite ambushes are enabled.");

        _eliteAttackSpeedMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Attack Speed Multiplier",
            1.0f,
            "Multiplier applied to ambush unit primary attack speed when elite ambushes are enabled.");

        _eliteSpellSpeedMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Spell Speed Multiplier",
            1.0f,
            "Multiplier applied to ambush unit ability (spell) speed when elite ambushes are enabled.");

        _eliteMoveSpeedMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Move Speed Multiplier",
            1.0f,
            "Multiplier applied to ambush unit movement speeds when elite ambushes are enabled.");

        _eliteKnockbackResistanceMultiplier = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Knockback Resistance Multiplier",
            1.0f,
            "Multiplier applied to ambush unit knockback resistance when elite ambushes are enabled.");

        _eliteRepresentativeHealthRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Health Ratio",
            1.0f,
            "Additional multiplier applied to elite representative health on top of the base elite multiplier.");

        _eliteRepresentativeHealthAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Health Bonus",
            0.0f,
            "Flat bonus applied to elite representative health multipliers after the squad multiplier and ratio are applied.");

        _eliteRepresentativeDamageReductionRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Damage Reduction Ratio",
            1.0f,
            "Additional multiplier applied to elite representative damage reduction on top of the base elite multiplier.");

        _eliteRepresentativeDamageReductionAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Damage Reduction Bonus",
            0.0f,
            "Flat bonus applied to elite representative damage reduction multipliers after the squad multiplier and ratio are applied.");

        _eliteRepresentativeResistanceRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Resistance Ratio",
            1.0f,
            "Additional multiplier applied to elite representative resistances on top of the base elite multiplier.");

        _eliteRepresentativeResistanceAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Resistance Bonus",
            0.0f,
            "Flat bonus applied to elite representative resistance multipliers after the squad multiplier and ratio are applied.");

        _eliteRepresentativePowerRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Power Ratio",
            1.0f,
            "Additional multiplier applied to elite representative power on top of the base elite multiplier.");

        _eliteRepresentativePowerAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Power Bonus",
            0.0f,
            "Flat bonus applied to elite representative power multipliers after the squad multiplier and ratio are applied.");

        _eliteRepresentativeAttackSpeedRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Attack Speed Ratio",
            1.0f,
            "Additional multiplier applied to elite representative primary attack speed on top of the base elite multiplier.");

        _eliteRepresentativeAttackSpeedAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Attack Speed Bonus",
            0.0f,
            "Flat bonus applied to elite representative attack speed multipliers after the squad multiplier and ratio are applied.");

        _eliteRepresentativeSpellSpeedRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Spell Speed Ratio",
            1.0f,
            "Additional multiplier applied to elite representative spell speed on top of the base elite multiplier.");

        _eliteRepresentativeSpellSpeedAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Spell Speed Bonus",
            0.0f,
            "Flat bonus applied to elite representative spell speed multipliers after the squad multiplier and ratio are applied.");

        _eliteRepresentativeMoveSpeedRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Move Speed Ratio",
            1.0f,
            "Additional multiplier applied to elite representative movement speed on top of the base elite multiplier.");

        _eliteRepresentativeMoveSpeedAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Move Speed Bonus",
            0.0f,
            "Flat bonus applied to elite representative move speed multipliers after the squad multiplier and ratio are applied.");

        _eliteRepresentativeKnockbackResistanceRatio = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Knockback Resistance Ratio",
            1.0f,
            "Additional multiplier applied to elite representative knockback resistance on top of the base elite multiplier.");

        _eliteRepresentativeKnockbackResistanceAdditive = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Elite Representative Knockback Resistance Bonus",
            0.0f,
            "Flat bonus applied to elite representative knockback resistance multipliers after the squad multiplier and ratio are applied.");

        _enableAmbushKnockbackResistance = configFile.Bind(
            "Faction Infamy - Elite Ambush",
            "Enable Ambush Knockback Resistance",
            false,
            "When true, the elite ambush knockback resistance multiplier is applied to spawned units.");

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

        var scarecrowMin = Math.Max(0, _halloweenScarecrowMinimum.Value);
        var scarecrowMax = Math.Max(scarecrowMin, _halloweenScarecrowMaximum.Value);
        var followUpChance = Math.Clamp(_seasonalFollowUpChancePercent.Value, 0, 100);
        var followUpMin = Math.Max(0, _seasonalFollowUpMinimum.Value);
        var followUpMax = Math.Max(followUpMin, _seasonalFollowUpMaximum.Value);

        var eliteHealth = Math.Max(0f, _eliteHealthMultiplier.Value);
        var eliteDamageReduction = Math.Max(0f, _eliteDamageReductionMultiplier.Value);
        var eliteResistance = Math.Max(0f, _eliteResistanceMultiplier.Value);
        var elitePower = Math.Max(0f, _elitePowerMultiplier.Value);
        var eliteAttackSpeed = Math.Max(0f, _eliteAttackSpeedMultiplier.Value);
        var eliteSpellSpeed = Math.Max(0f, _eliteSpellSpeedMultiplier.Value);
        var eliteMoveSpeed = Math.Max(0f, _eliteMoveSpeedMultiplier.Value);
        var eliteKnockback = Math.Max(0f, _eliteKnockbackResistanceMultiplier.Value);

        var eliteRepHealth = Math.Max(0f, _eliteRepresentativeHealthRatio.Value);
        var eliteRepHealthAdd = Math.Max(0f, _eliteRepresentativeHealthAdditive.Value);
        var eliteRepDamageReduction = Math.Max(0f, _eliteRepresentativeDamageReductionRatio.Value);
        var eliteRepDamageReductionAdd = Math.Max(0f, _eliteRepresentativeDamageReductionAdditive.Value);
        var eliteRepResistance = Math.Max(0f, _eliteRepresentativeResistanceRatio.Value);
        var eliteRepResistanceAdd = Math.Max(0f, _eliteRepresentativeResistanceAdditive.Value);
        var eliteRepPower = Math.Max(0f, _eliteRepresentativePowerRatio.Value);
        var eliteRepPowerAdd = Math.Max(0f, _eliteRepresentativePowerAdditive.Value);
        var eliteRepAttackSpeed = Math.Max(0f, _eliteRepresentativeAttackSpeedRatio.Value);
        var eliteRepAttackSpeedAdd = Math.Max(0f, _eliteRepresentativeAttackSpeedAdditive.Value);
        var eliteRepSpellSpeed = Math.Max(0f, _eliteRepresentativeSpellSpeedRatio.Value);
        var eliteRepSpellSpeedAdd = Math.Max(0f, _eliteRepresentativeSpellSpeedAdditive.Value);
        var eliteRepMoveSpeed = Math.Max(0f, _eliteRepresentativeMoveSpeedRatio.Value);
        var eliteRepMoveSpeedAdd = Math.Max(0f, _eliteRepresentativeMoveSpeedAdditive.Value);
        var eliteRepKnockback = Math.Max(0f, _eliteRepresentativeKnockbackResistanceRatio.Value);
        var eliteRepKnockbackAdd = Math.Max(0f, _eliteRepresentativeKnockbackResistanceAdditive.Value);

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
            Math.Clamp(_autosaveBackups.Value, 0, 20),
            _enableHalloweenAmbush.Value,
            scarecrowMin,
            scarecrowMax,
            Math.Max(1, _halloweenScarecrowRareMultiplier.Value),
            Math.Clamp(_halloweenScarecrowRareChancePercent.Value, 0, 100),
            followUpChance,
            followUpMin,
            followUpMax,
            _enableEliteAmbush.Value,
            eliteHealth,
            eliteDamageReduction,
            eliteResistance,
            elitePower,
            eliteAttackSpeed,
            eliteSpellSpeed,
            eliteMoveSpeed,
            eliteKnockback,
            eliteRepHealth,
            eliteRepHealthAdd,
            eliteRepDamageReduction,
            eliteRepDamageReductionAdd,
            eliteRepResistance,
            eliteRepResistanceAdd,
            eliteRepPower,
            eliteRepPowerAdd,
            eliteRepAttackSpeed,
            eliteRepAttackSpeedAdd,
            eliteRepSpellSpeed,
            eliteRepSpellSpeedAdd,
            eliteRepMoveSpeed,
            eliteRepMoveSpeedAdd,
            eliteRepKnockback,
            eliteRepKnockbackAdd,
            _enableAmbushKnockbackResistance.Value);
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

        if (_halloweenScarecrowMinimum.Value < 0)
        {
            _halloweenScarecrowMinimum.Value = 0;
        }

        if (_halloweenScarecrowMaximum.Value < _halloweenScarecrowMinimum.Value)
        {
            _halloweenScarecrowMaximum.Value = _halloweenScarecrowMinimum.Value;
        }

        if (_halloweenScarecrowRareMultiplier.Value < 1)
        {
            _halloweenScarecrowRareMultiplier.Value = 1;
        }

        if (_halloweenScarecrowRareChancePercent.Value < 0)
        {
            _halloweenScarecrowRareChancePercent.Value = 0;
        }

        if (_halloweenScarecrowRareChancePercent.Value > 100)
        {
            _halloweenScarecrowRareChancePercent.Value = 100;
        }

        if (_seasonalFollowUpChancePercent.Value < 0)
        {
            _seasonalFollowUpChancePercent.Value = 0;
        }

        if (_seasonalFollowUpChancePercent.Value > 100)
        {
            _seasonalFollowUpChancePercent.Value = 100;
        }

        if (_seasonalFollowUpMinimum.Value < 0)
        {
            _seasonalFollowUpMinimum.Value = 0;
        }

        if (_seasonalFollowUpMaximum.Value < _seasonalFollowUpMinimum.Value)
        {
            _seasonalFollowUpMaximum.Value = _seasonalFollowUpMinimum.Value;
        }

        if (_eliteHealthMultiplier.Value < 0f)
        {
            _eliteHealthMultiplier.Value = 0f;
        }

        if (_eliteDamageReductionMultiplier.Value < 0f)
        {
            _eliteDamageReductionMultiplier.Value = 0f;
        }

        if (_eliteResistanceMultiplier.Value < 0f)
        {
            _eliteResistanceMultiplier.Value = 0f;
        }

        if (_elitePowerMultiplier.Value < 0f)
        {
            _elitePowerMultiplier.Value = 0f;
        }

        if (_eliteAttackSpeedMultiplier.Value < 0f)
        {
            _eliteAttackSpeedMultiplier.Value = 0f;
        }

        if (_eliteSpellSpeedMultiplier.Value < 0f)
        {
            _eliteSpellSpeedMultiplier.Value = 0f;
        }

        if (_eliteMoveSpeedMultiplier.Value < 0f)
        {
            _eliteMoveSpeedMultiplier.Value = 0f;
        }

        if (_eliteKnockbackResistanceMultiplier.Value < 0f)
        {
            _eliteKnockbackResistanceMultiplier.Value = 0f;
        }

        if (_eliteRepresentativeHealthRatio.Value < 0f)
        {
            _eliteRepresentativeHealthRatio.Value = 0f;
        }

        if (_eliteRepresentativeHealthAdditive.Value < 0f)
        {
            _eliteRepresentativeHealthAdditive.Value = 0f;
        }

        if (_eliteRepresentativeDamageReductionRatio.Value < 0f)
        {
            _eliteRepresentativeDamageReductionRatio.Value = 0f;
        }

        if (_eliteRepresentativeDamageReductionAdditive.Value < 0f)
        {
            _eliteRepresentativeDamageReductionAdditive.Value = 0f;
        }

        if (_eliteRepresentativeResistanceRatio.Value < 0f)
        {
            _eliteRepresentativeResistanceRatio.Value = 0f;
        }

        if (_eliteRepresentativeResistanceAdditive.Value < 0f)
        {
            _eliteRepresentativeResistanceAdditive.Value = 0f;
        }

        if (_eliteRepresentativePowerRatio.Value < 0f)
        {
            _eliteRepresentativePowerRatio.Value = 0f;
        }

        if (_eliteRepresentativePowerAdditive.Value < 0f)
        {
            _eliteRepresentativePowerAdditive.Value = 0f;
        }

        if (_eliteRepresentativeAttackSpeedRatio.Value < 0f)
        {
            _eliteRepresentativeAttackSpeedRatio.Value = 0f;
        }

        if (_eliteRepresentativeAttackSpeedAdditive.Value < 0f)
        {
            _eliteRepresentativeAttackSpeedAdditive.Value = 0f;
        }

        if (_eliteRepresentativeSpellSpeedRatio.Value < 0f)
        {
            _eliteRepresentativeSpellSpeedRatio.Value = 0f;
        }

        if (_eliteRepresentativeSpellSpeedAdditive.Value < 0f)
        {
            _eliteRepresentativeSpellSpeedAdditive.Value = 0f;
        }

        if (_eliteRepresentativeMoveSpeedRatio.Value < 0f)
        {
            _eliteRepresentativeMoveSpeedRatio.Value = 0f;
        }

        if (_eliteRepresentativeMoveSpeedAdditive.Value < 0f)
        {
            _eliteRepresentativeMoveSpeedAdditive.Value = 0f;
        }

        if (_eliteRepresentativeKnockbackResistanceRatio.Value < 0f)
        {
            _eliteRepresentativeKnockbackResistanceRatio.Value = 0f;
        }

        if (_eliteRepresentativeKnockbackResistanceAdditive.Value < 0f)
        {
            _eliteRepresentativeKnockbackResistanceAdditive.Value = 0f;
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
    int AutosaveBackupCount,
    bool EnableHalloweenAmbush,
    int HalloweenScarecrowMinimum,
    int HalloweenScarecrowMaximum,
    int HalloweenScarecrowRareMultiplier,
    int HalloweenScarecrowRareChancePercent,
    int SeasonalFollowUpChancePercent,
    int SeasonalFollowUpMinimum,
    int SeasonalFollowUpMaximum,
    bool EnableEliteAmbush,
    float EliteHealthMultiplier,
    float EliteDamageReductionMultiplier,
    float EliteResistanceMultiplier,
    float ElitePowerMultiplier,
    float EliteAttackSpeedMultiplier,
    float EliteSpellSpeedMultiplier,
    float EliteMoveSpeedMultiplier,
    float EliteKnockbackResistanceMultiplier,
    float EliteRepresentativeHealthRatio,
    float EliteRepresentativeHealthAdditive,
    float EliteRepresentativeDamageReductionRatio,
    float EliteRepresentativeDamageReductionAdditive,
    float EliteRepresentativeResistanceRatio,
    float EliteRepresentativeResistanceAdditive,
    float EliteRepresentativePowerRatio,
    float EliteRepresentativePowerAdditive,
    float EliteRepresentativeAttackSpeedRatio,
    float EliteRepresentativeAttackSpeedAdditive,
    float EliteRepresentativeSpellSpeedRatio,
    float EliteRepresentativeSpellSpeedAdditive,
    float EliteRepresentativeMoveSpeedRatio,
    float EliteRepresentativeMoveSpeedAdditive,
    float EliteRepresentativeKnockbackResistanceRatio,
    float EliteRepresentativeKnockbackResistanceAdditive,
    bool EnableAmbushKnockbackResistance);

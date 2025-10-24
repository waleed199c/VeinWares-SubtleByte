using System;
using BepInEx.Configuration;

#nullable enable

namespace VeinWares.SubtleByte.Config;

internal static class FactionInfamyConfig
{
    private const string LegacySection = "Faction Infamy";
    private static ConfigEntries? _entries;

    public static void Initialize(ConfigFile configFile)
    {
        if (_entries != null)
        {
            return;
        }

        if (configFile is null)
        {
            throw new ArgumentNullException(nameof(configFile));
        }

        _entries = ConfigEntries.Bind(configFile);
    }

    public static FactionInfamyConfigSnapshot CreateSnapshot()
    {
        if (_entries is null)
        {
            throw new InvalidOperationException("FactionInfamyConfig.Initialize must be called before creating a snapshot.");
        }

        return _entries.BuildSnapshot();
    }

    private sealed class ConfigEntries
    {
        private readonly ConfigFile _file;

        private readonly ConfigEntry<float> _hateGainMultiplier;
        private readonly ConfigEntry<float> _hateDecayPerMinute;
        private readonly ConfigEntry<float> _cooldownGraceSeconds;
        private readonly ConfigEntry<float> _combatCooldownSeconds;
        private readonly ConfigEntry<float> _minimumAmbushHate;
        private readonly ConfigEntry<int> _maximumHate;

        private readonly ConfigEntry<float> _ambushCooldownMinutes;
        private readonly ConfigEntry<int> _ambushChancePercent;
        private readonly ConfigEntry<float> _ambushLifetimeSeconds;
        private readonly ConfigEntry<bool> _ambushesRespectTerritory;
        private readonly ConfigEntry<bool> _enableAmbushVisualBuffs;
        private readonly ConfigEntry<bool> _disableBloodConsumeOnSpawn;
        private readonly ConfigEntry<bool> _disableCharmOnSpawn;
        private readonly ConfigEntry<bool> _enableNativeDropTables;

        private readonly ConfigEntry<int> _autosaveMinutes;
        private readonly ConfigEntry<int> _autosaveBackups;

        private readonly ConfigEntry<bool> _enableHalloweenAmbush;
        private readonly ConfigEntry<int> _halloweenScarecrowMinimum;
        private readonly ConfigEntry<int> _halloweenScarecrowMaximum;
        private readonly ConfigEntry<int> _halloweenScarecrowRareMultiplier;
        private readonly ConfigEntry<int> _halloweenScarecrowRareChancePercent;
        private readonly ConfigEntry<int> _seasonalFollowUpChancePercent;
        private readonly ConfigEntry<int> _seasonalFollowUpMinimum;
        private readonly ConfigEntry<int> _seasonalFollowUpMaximum;

        private readonly ConfigEntry<float> _prestigeLevelBonusPerTier;
        private readonly ConfigEntry<float> _prestigeEliteMultiplier;

        private readonly ConfigEntry<bool> _enableEliteAmbush;
        private readonly ConfigEntry<float> _eliteHealthMultiplier;
        private readonly ConfigEntry<float> _eliteDamageReductionMultiplier;
        private readonly ConfigEntry<float> _eliteResistanceMultiplier;
        private readonly ConfigEntry<float> _elitePowerMultiplier;
        private readonly ConfigEntry<float> _eliteAttackSpeedMultiplier;
        private readonly ConfigEntry<float> _eliteSpellSpeedMultiplier;
        private readonly ConfigEntry<float> _eliteMoveSpeedMultiplier;
        private readonly ConfigEntry<float> _eliteKnockbackResistanceMultiplier;
        private readonly ConfigEntry<float> _eliteRepresentativeHealthRatio;
        private readonly ConfigEntry<float> _eliteRepresentativeHealthAdditive;
        private readonly ConfigEntry<float> _eliteRepresentativeDamageReductionRatio;
        private readonly ConfigEntry<float> _eliteRepresentativeDamageReductionAdditive;
        private readonly ConfigEntry<float> _eliteRepresentativeResistanceRatio;
        private readonly ConfigEntry<float> _eliteRepresentativeResistanceAdditive;
        private readonly ConfigEntry<float> _eliteRepresentativePowerRatio;
        private readonly ConfigEntry<float> _eliteRepresentativePowerAdditive;
        private readonly ConfigEntry<float> _eliteRepresentativeAttackSpeedRatio;
        private readonly ConfigEntry<float> _eliteRepresentativeAttackSpeedAdditive;
        private readonly ConfigEntry<float> _eliteRepresentativeSpellSpeedRatio;
        private readonly ConfigEntry<float> _eliteRepresentativeSpellSpeedAdditive;
        private readonly ConfigEntry<float> _eliteRepresentativeMoveSpeedRatio;
        private readonly ConfigEntry<float> _eliteRepresentativeMoveSpeedAdditive;
        private readonly ConfigEntry<float> _eliteRepresentativeKnockbackResistanceRatio;
        private readonly ConfigEntry<float> _eliteRepresentativeKnockbackResistanceAdditive;
        private readonly ConfigEntry<bool> _enableAmbushKnockbackResistance;

        private bool _dirty;

        private ConfigEntries(ConfigFile file)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));

            _hateGainMultiplier = Bind(Section.Core, "Hate Gain Multiplier", 1.0f,
                "Scales the amount of hate granted per qualifying kill.", LegacySection);

            _hateDecayPerMinute = Bind(Section.Core, "Hate Decay Per Minute", 2.0f,
                "Amount of hate removed from every active faction bucket each minute while the player is eligible for cooldown.", LegacySection);

            _cooldownGraceSeconds = Bind(Section.Combat, "Cooldown Grace Seconds", 30.0f,
                "How long after combat the player must remain idle before their hate begins to fade.", LegacySection);

            _combatCooldownSeconds = Bind(Section.Combat, "Combat Cooldown Seconds", 15.0f,
                "Minimum time between combat triggers that will reset the hate decay timer.", LegacySection);

            _minimumAmbushHate = Bind(Section.Core, "Minimum Ambush Hate", 10.0f,
                "Players must reach this hate level (after multipliers) before ambush squads are considered.", LegacySection);

            _maximumHate = Bind(Section.Core, "Maximum Hate", 100,
                "Upper bound for hate per faction. Any calculated hate beyond this value will be clamped.", LegacySection);

            _ambushCooldownMinutes = Bind(Section.Ambush, "Ambush Cooldown Minutes", 15.0f,
                "Minimum number of minutes that must pass before the same player can be ambushed again.", LegacySection);

            _ambushChancePercent = Bind(Section.Ambush, "Ambush Chance Percent", 50,
                "Percent chance that an eligible combat engagement will spawn an ambush squad.", LegacySection);

            _ambushLifetimeSeconds = Bind(Section.Ambush, "Ambush Squad Lifetime Seconds", 300.0f,
                "How long ambush squads remain alive before despawning automatically.", LegacySection);

            _ambushesRespectTerritory = Bind(Section.Ambush, "Ambushes Respect Territory", true,
                "When enabled, ambush squads will not spawn inside castle territory owned by the targeted player.", LegacySection);

            _enableAmbushVisualBuffs = Bind(Section.Ambush, "Enable Ambush Visual Buffs", true,
                "When true, ambush squads can apply randomised visual buffs to spawned units.", LegacySection);

            _disableBloodConsumeOnSpawn = Bind(Section.Ambush, "DisableBloodConsumeOnSpawn", false,
                "When enabled, ambush spawns strip blood consume components so they cannot be fed upon immediately.", LegacySection);

            _disableCharmOnSpawn = Bind(Section.Ambush, "DisableCharmOnSpawn", false,
                "When enabled, ambush spawns strip charm-related components so they cannot be charmed immediately after spawning.", LegacySection);

            _enableNativeDropTables = Bind(Section.Ambush, "EnableNativeDropTables", false,
                "When true, ambush squads retain their default drop tables instead of clearing them for custom loot handling.", LegacySection);

            _autosaveMinutes = Bind(Section.Persistence, "Autosave Minutes", 5,
                "Interval, in minutes, for persisting infamy hate data to disk.", LegacySection);

            _autosaveBackups = Bind(Section.Persistence, "Autosave Backups", 3,
                "Number of rolling backup files to keep whenever the Faction Infamy system saves the hate database.", LegacySection);

            _enableHalloweenAmbush = Bind(Section.Seasonal, "Enable Halloween Ambush", false,
                "When true, Tier 5 ambush squads can include seasonal scarecrow reinforcements.", LegacySection);

            _halloweenScarecrowMinimum = Bind(Section.Seasonal, "Halloween Scarecrow Minimum", 1,
                "Minimum number of scarecrows to spawn alongside Tier 5 ambush squads when the Halloween event is enabled.", LegacySection);

            _halloweenScarecrowMaximum = Bind(Section.Seasonal, "Halloween Scarecrow Maximum", 3,
                "Maximum number of scarecrows to spawn alongside Tier 5 ambush squads when the Halloween event is enabled.", LegacySection);

            _halloweenScarecrowRareMultiplier = Bind(Section.Seasonal, "Halloween Scarecrow Rare Multiplier", 2,
                "Multiplier applied to the rolled scarecrow count when the rare Halloween scarecrow chance succeeds.", LegacySection);

            _halloweenScarecrowRareChancePercent = Bind(Section.Seasonal, "Halloween Scarecrow Rare Chance Percent", 5,
                "Percent chance that the scarecrow roll will be multiplied by the rare multiplier value.", LegacySection);

            _seasonalFollowUpChancePercent = Bind(Section.Seasonal, "Halloween Follow-Up Wave Chance Percent", 0,
                "Percent chance that a second seasonal ambush wave spawns after the core squad.", LegacySection);

            _seasonalFollowUpMinimum = Bind(Section.Seasonal, "Halloween Follow-Up Wave Minimum", 1,
                "Minimum number of shared seasonal units to spawn in the follow-up wave when enabled.", LegacySection);

            _seasonalFollowUpMaximum = Bind(Section.Seasonal, "Halloween Follow-Up Wave Maximum", 2,
                "Maximum number of shared seasonal units to spawn in the follow-up wave when enabled.", LegacySection);

            _prestigeLevelBonusPerTier = Bind(Section.Prestige, "Prestige Level Bonus Per Tier", 0.01f,
                "Additional stat multiplier applied per Bloodcraft prestige level and ambush tier when calculating ambush scaling.");

            _prestigeEliteMultiplier = Bind(Section.Prestige, "Prestige Elite Multiplier", 1.25f,
                "Additional multiplier applied to the prestige bonus when elite ambush scaling is active.");

            _enableEliteAmbush = Bind(Section.Elite, "Enable Elite Ambush", false,
                "When true, Tier 5 ambush squads receive elite stat multipliers and representative bonuses.");

            _eliteHealthMultiplier = Bind(Section.Elite, "Elite Health Multiplier", 1.0f,
                "Multiplier applied to ambush unit health when elite ambushes are enabled.");

            _eliteDamageReductionMultiplier = Bind(Section.Elite, "Elite Damage Reduction Multiplier", 1.0f,
                "Multiplier applied to ambush unit damage reduction stats when elite ambushes are enabled.");

            _eliteResistanceMultiplier = Bind(Section.Elite, "Elite Resistance Multiplier", 1.0f,
                "Multiplier applied to ambush unit resistances when elite ambushes are enabled.");

            _elitePowerMultiplier = Bind(Section.Elite, "Elite Power Multiplier", 1.0f,
                "Multiplier applied to ambush unit physical and spell power when elite ambushes are enabled.");

            _eliteAttackSpeedMultiplier = Bind(Section.Elite, "Elite Attack Speed Multiplier", 1.0f,
                "Multiplier applied to ambush unit primary attack speed when elite ambushes are enabled.");

            _eliteSpellSpeedMultiplier = Bind(Section.Elite, "Elite Spell Speed Multiplier", 1.0f,
                "Multiplier applied to ambush unit ability (spell) speed when elite ambushes are enabled.");

            _eliteMoveSpeedMultiplier = Bind(Section.Elite, "Elite Move Speed Multiplier", 1.0f,
                "Multiplier applied to ambush unit movement speeds when elite ambushes are enabled.");

            _eliteKnockbackResistanceMultiplier = Bind(Section.Elite, "Elite Knockback Resistance Multiplier", 1.0f,
                "Multiplier applied to ambush unit knockback resistance when elite ambushes are enabled.");

            _eliteRepresentativeHealthRatio = Bind(Section.Elite, "Elite Representative Health Ratio", 1.0f,
                "Additional multiplier applied to elite representative health on top of the base elite multiplier.");

            _eliteRepresentativeHealthAdditive = Bind(Section.Elite, "Elite Representative Health Bonus", 0.0f,
                "Flat bonus applied to elite representative health after the squad multiplier and ratio are applied.");

            _eliteRepresentativeDamageReductionRatio = Bind(Section.Elite, "Elite Representative Damage Reduction Ratio", 1.0f,
                "Additional multiplier applied to elite representative damage reduction on top of the base elite multiplier.");

            _eliteRepresentativeDamageReductionAdditive = Bind(Section.Elite, "Elite Representative Damage Reduction Bonus", 0.0f,
                "Flat bonus applied to elite representative damage reduction after the squad multiplier and ratio are applied.");

            _eliteRepresentativeResistanceRatio = Bind(Section.Elite, "Elite Representative Resistance Ratio", 1.0f,
                "Additional multiplier applied to elite representative resistances on top of the base elite multiplier.");

            _eliteRepresentativeResistanceAdditive = Bind(Section.Elite, "Elite Representative Resistance Bonus", 0.0f,
                "Flat bonus applied to elite representative resistances after the squad multiplier and ratio are applied.");

            _eliteRepresentativePowerRatio = Bind(Section.Elite, "Elite Representative Power Ratio", 1.0f,
                "Additional multiplier applied to elite representative power on top of the base elite multiplier.");

            _eliteRepresentativePowerAdditive = Bind(Section.Elite, "Elite Representative Power Bonus", 0.0f,
                "Flat bonus applied to elite representative power after the squad multiplier and ratio are applied.");

            _eliteRepresentativeAttackSpeedRatio = Bind(Section.Elite, "Elite Representative Attack Speed Ratio", 1.0f,
                "Additional multiplier applied to elite representative primary attack speed on top of the base elite multiplier.");

            _eliteRepresentativeAttackSpeedAdditive = Bind(Section.Elite, "Elite Representative Attack Speed Bonus", 0.0f,
                "Flat bonus applied to elite representative attack speed after the squad multiplier and ratio are applied.");

            _eliteRepresentativeSpellSpeedRatio = Bind(Section.Elite, "Elite Representative Spell Speed Ratio", 1.0f,
                "Additional multiplier applied to elite representative spell speed on top of the base elite multiplier.");

            _eliteRepresentativeSpellSpeedAdditive = Bind(Section.Elite, "Elite Representative Spell Speed Bonus", 0.0f,
                "Flat bonus applied to elite representative spell speed after the squad multiplier and ratio are applied.");

            _eliteRepresentativeMoveSpeedRatio = Bind(Section.Elite, "Elite Representative Move Speed Ratio", 1.0f,
                "Additional multiplier applied to elite representative movement speed on top of the base elite multiplier.");

            _eliteRepresentativeMoveSpeedAdditive = Bind(Section.Elite, "Elite Representative Move Speed Bonus", 0.0f,
                "Flat bonus applied to elite representative movement speed after the squad multiplier and ratio are applied.");

            _eliteRepresentativeKnockbackResistanceRatio = Bind(Section.Elite, "Elite Representative Knockback Resistance Ratio", 1.0f,
                "Additional multiplier applied to elite representative knockback resistance on top of the base elite multiplier.");

            _eliteRepresentativeKnockbackResistanceAdditive = Bind(Section.Elite, "Elite Representative Knockback Resistance Bonus", 0.0f,
                "Flat bonus applied to elite representative knockback resistance after the squad multiplier and ratio are applied.");

            _enableAmbushKnockbackResistance = Bind(Section.Elite, "Enable Ambush Knockback Resistance", false,
                "When true, the elite ambush knockback resistance multiplier is applied to spawned units.");
        }

        internal static ConfigEntries Bind(ConfigFile file)
        {
            var entries = new ConfigEntries(file);
            entries.SaveIfDirty();
            return entries;
        }

        internal FactionInfamyConfigSnapshot BuildSnapshot()
        {
            var core = BuildCore();
            var persistence = BuildPersistence();
            var ambush = BuildAmbush();
            var seasonal = BuildSeasonal();
            var prestige = BuildPrestige();
            var elite = BuildElite();

            SaveIfDirty();

            return new FactionInfamyConfigSnapshot(core, persistence, ambush, seasonal, prestige, elite);
        }

        private InfamyCoreSettings BuildCore()
        {
            var hateGain = ClampFloat(_hateGainMultiplier, 0f);
            var hateDecayPerMinute = ClampFloat(_hateDecayPerMinute, 0f);
            var graceSeconds = ClampFloat(_cooldownGraceSeconds, 0f);
            var combatCooldownSeconds = ClampFloat(_combatCooldownSeconds, 1f);
            var minimumAmbushHate = ClampFloat(_minimumAmbushHate, 0f);
            var maximumHate = ClampInt(_maximumHate, 1);

            return new InfamyCoreSettings(
                hateGain,
                hateDecayPerMinute / 60f,
                TimeSpan.FromSeconds(graceSeconds),
                TimeSpan.FromSeconds(combatCooldownSeconds),
                minimumAmbushHate,
                maximumHate);
        }

        private PersistenceSettings BuildPersistence()
        {
            var autosaveMinutes = ClampInt(_autosaveMinutes, 1);
            var backupCount = ClampInt(_autosaveBackups, 0, 20);

            return new PersistenceSettings(TimeSpan.FromMinutes(autosaveMinutes), backupCount);
        }

        private AmbushSettings BuildAmbush()
        {
            var cooldownMinutes = ClampFloat(_ambushCooldownMinutes, 1f);
            var chancePercent = ClampInt(_ambushChancePercent, 0, 100);
            var lifetimeSeconds = ClampFloat(_ambushLifetimeSeconds, 10f);

            return new AmbushSettings(
                chancePercent,
                TimeSpan.FromMinutes(cooldownMinutes),
                TimeSpan.FromSeconds(lifetimeSeconds),
                _enableAmbushVisualBuffs.Value,
                _ambushesRespectTerritory.Value,
                _disableBloodConsumeOnSpawn.Value,
                _disableCharmOnSpawn.Value,
                _enableNativeDropTables.Value);
        }

        private SeasonalSettings BuildSeasonal()
        {
            var scarecrowMin = ClampInt(_halloweenScarecrowMinimum, 0);
            var scarecrowMax = ClampInt(_halloweenScarecrowMaximum, scarecrowMin);
            var rareMultiplier = ClampInt(_halloweenScarecrowRareMultiplier, 1);
            var rareChance = ClampInt(_halloweenScarecrowRareChancePercent, 0, 100);
            var followUpChance = ClampInt(_seasonalFollowUpChancePercent, 0, 100);
            var followUpMin = ClampInt(_seasonalFollowUpMinimum, 0);
            var followUpMax = ClampInt(_seasonalFollowUpMaximum, followUpMin);

            return new SeasonalSettings(
                _enableHalloweenAmbush.Value,
                scarecrowMin,
                scarecrowMax,
                rareMultiplier,
                rareChance,
                followUpChance,
                followUpMin,
                followUpMax);
        }

        private PrestigeSettings BuildPrestige()
        {
            var levelBonus = ClampFloat(_prestigeLevelBonusPerTier, 0f);
            var eliteBonus = ClampFloat(_prestigeEliteMultiplier, 0f);

            return new PrestigeSettings(levelBonus, eliteBonus);
        }

        private EliteSettings BuildElite()
        {
            var squad = new EliteSquadSettings(
                ClampFloat(_eliteHealthMultiplier, 0f),
                ClampFloat(_eliteDamageReductionMultiplier, 0f),
                ClampFloat(_eliteResistanceMultiplier, 0f),
                ClampFloat(_elitePowerMultiplier, 0f),
                ClampFloat(_eliteAttackSpeedMultiplier, 0f),
                ClampFloat(_eliteSpellSpeedMultiplier, 0f),
                ClampFloat(_eliteMoveSpeedMultiplier, 0f),
                ClampFloat(_eliteKnockbackResistanceMultiplier, 0f));

            var representative = new EliteRepresentativeSettings(
                ClampFloat(_eliteRepresentativeHealthRatio, 0f),
                ClampFloat(_eliteRepresentativeHealthAdditive, 0f),
                ClampFloat(_eliteRepresentativeDamageReductionRatio, 0f),
                ClampFloat(_eliteRepresentativeDamageReductionAdditive, 0f),
                ClampFloat(_eliteRepresentativeResistanceRatio, 0f),
                ClampFloat(_eliteRepresentativeResistanceAdditive, 0f),
                ClampFloat(_eliteRepresentativePowerRatio, 0f),
                ClampFloat(_eliteRepresentativePowerAdditive, 0f),
                ClampFloat(_eliteRepresentativeAttackSpeedRatio, 0f),
                ClampFloat(_eliteRepresentativeAttackSpeedAdditive, 0f),
                ClampFloat(_eliteRepresentativeSpellSpeedRatio, 0f),
                ClampFloat(_eliteRepresentativeSpellSpeedAdditive, 0f),
                ClampFloat(_eliteRepresentativeMoveSpeedRatio, 0f),
                ClampFloat(_eliteRepresentativeMoveSpeedAdditive, 0f),
                ClampFloat(_eliteRepresentativeKnockbackResistanceRatio, 0f),
                ClampFloat(_eliteRepresentativeKnockbackResistanceAdditive, 0f));

            return new EliteSettings(
                _enableEliteAmbush.Value,
                _enableAmbushKnockbackResistance.Value,
                squad,
                representative);
        }

        private ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, string? legacySection = null)
        {
            var entry = _file.Bind(section, key, defaultValue, description);

            if (!string.IsNullOrWhiteSpace(legacySection) && !string.Equals(legacySection, section, StringComparison.OrdinalIgnoreCase))
            {
                TryMigrate(legacySection!, key, entry);
            }

            return entry;
        }

        private void TryMigrate<T>(string legacySection, string key, ConfigEntry<T> target)
        {
            var legacyDefinition = new ConfigDefinition(legacySection, key);
            if (!_file.TryGetEntry(legacyDefinition, out ConfigEntry<T> legacy))
            {
                return;
            }

            target.Value = legacy.Value;
            _file.Remove(legacyDefinition);
            _dirty = true;
        }

        private float ClampFloat(ConfigEntry<float> entry, float min, float? max = null)
        {
            var value = entry.Value;
            if (value < min)
            {
                value = min;
            }

            if (max.HasValue && value > max.Value)
            {
                value = max.Value;
            }

            if (!value.Equals(entry.Value))
            {
                entry.Value = value;
                _dirty = true;
            }

            return value;
        }

        private int ClampInt(ConfigEntry<int> entry, int min, int? max = null)
        {
            var value = entry.Value;
            if (value < min)
            {
                value = min;
            }

            if (max.HasValue && value > max.Value)
            {
                value = max.Value;
            }

            if (value != entry.Value)
            {
                entry.Value = value;
                _dirty = true;
            }

            return value;
        }

        private void SaveIfDirty()
        {
            if (!_dirty)
            {
                return;
            }

            _file.Save();
            _dirty = false;
        }
    }

    private static class Section
    {
        internal const string Core = "Faction Infamy - Core";
        internal const string Combat = "Faction Infamy - Combat";
        internal const string Ambush = "Faction Infamy - Ambush";
        internal const string Persistence = "Faction Infamy - Persistence";
        internal const string Seasonal = "Faction Infamy - Seasonal Events";
        internal const string Prestige = "Faction Infamy - Prestige";
        internal const string Elite = "Faction Infamy - Elite Ambush";
    }
}

internal sealed record FactionInfamyConfigSnapshot(
    InfamyCoreSettings Core,
    PersistenceSettings Persistence,
    AmbushSettings Ambush,
    SeasonalSettings Seasonal,
    PrestigeSettings Prestige,
    EliteSettings Elite);

internal sealed record InfamyCoreSettings(
    float HateGainMultiplier,
    float HateDecayPerSecond,
    TimeSpan CooldownGrace,
    TimeSpan CombatCooldown,
    float MinimumAmbushHate,
    int MaximumHate);

internal sealed record PersistenceSettings(TimeSpan AutosaveInterval, int AutosaveBackupCount);

internal sealed record AmbushSettings(
    int ChancePercent,
    TimeSpan Cooldown,
    TimeSpan Lifetime,
    bool VisualBuffsEnabled,
    bool RespectTerritory,
    bool DisableBloodConsumeOnSpawn,
    bool DisableCharmOnSpawn,
    bool EnableNativeDropTables);

internal sealed record SeasonalSettings(
    bool EnableHalloween,
    int ScarecrowMinimum,
    int ScarecrowMaximum,
    int ScarecrowRareMultiplier,
    int ScarecrowRareChancePercent,
    int FollowUpChancePercent,
    int FollowUpMinimum,
    int FollowUpMaximum);

internal sealed record PrestigeSettings(float LevelBonusPerTier, float EliteMultiplier);

internal sealed record EliteSettings(
    bool Enabled,
    bool ApplyKnockbackResistance,
    EliteSquadSettings Squad,
    EliteRepresentativeSettings Representative);

internal sealed record EliteSquadSettings(
    float HealthMultiplier,
    float DamageReductionMultiplier,
    float ResistanceMultiplier,
    float PowerMultiplier,
    float AttackSpeedMultiplier,
    float SpellSpeedMultiplier,
    float MoveSpeedMultiplier,
    float KnockbackResistanceMultiplier);

internal sealed record EliteRepresentativeSettings(
    float HealthRatio,
    float HealthBonus,
    float DamageReductionRatio,
    float DamageReductionBonus,
    float ResistanceRatio,
    float ResistanceBonus,
    float PowerRatio,
    float PowerBonus,
    float AttackSpeedRatio,
    float AttackSpeedBonus,
    float SpellSpeedRatio,
    float SpellSpeedBonus,
    float MoveSpeedRatio,
    float MoveSpeedBonus,
    float KnockbackResistanceRatio,
    float KnockbackResistanceBonus);

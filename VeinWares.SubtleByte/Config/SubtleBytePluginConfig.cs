using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace VeinWares.SubtleByte.Config
{
    internal static class SubtleBytePluginConfig
    {
        private static ConfigFile _configFile;

        private static ConfigEntry<bool> _emptyBottleRefundEnabled;
        private static ConfigEntry<bool> _relicDebugEventsEnabled;
        private static ConfigEntry<bool> _itemStackServiceEnabled;
        private static ConfigEntry<bool> _debugLogsEnabled;
        private static ConfigEntry<bool> _infamySystemEnabled;

        private static readonly IReadOnlyList<ConfigDefinition> LegacyWantedDefinitions = new[]
        {
            new ConfigDefinition("Wanted System", "Enable Wanted System"),
            new ConfigDefinition("Wanted System", "Enable Ambush Spawns"),
            new ConfigDefinition("Wanted System", "Heat Gain Multiplier"),
            new ConfigDefinition("Wanted System", "Heat Decay Per Minute"),
            new ConfigDefinition("Wanted System", "Cooldown Grace Seconds"),
            new ConfigDefinition("Wanted System", "Combat Cooldown Seconds"),
            new ConfigDefinition("Wanted System", "Ambush Cooldown Minutes"),
            new ConfigDefinition("Wanted System", "Minimum Ambush Heat"),
            new ConfigDefinition("Wanted System", "Maximum Heat"),
            new ConfigDefinition("Wanted System", "Autosave Minutes"),
            new ConfigDefinition("Wanted System", "Autosave Backups")
        };

        public static bool EmptyBottleRefundEnabled => _emptyBottleRefundEnabled?.Value ?? true;
        internal static ConfigEntry<bool> EmptyBottleRefundEnabledEntry => _emptyBottleRefundEnabled;
        public static bool RelicDebugEventsEnabled => _relicDebugEventsEnabled?.Value ?? true;
        public static bool ItemStackServiceEnabled => _itemStackServiceEnabled?.Value ?? true;
        public static bool DebugLogsEnabled => _debugLogsEnabled?.Value ?? false;
        public static bool InfamySystemEnabled => _infamySystemEnabled?.Value ?? false;

        public static void Initialize()
        {
            if (_configFile != null)
                return;

            var path = Path.Combine(Paths.ConfigPath, "Genji.VeinWares-SubtleByte.cfg");
            _configFile = new ConfigFile(path, true);
            RemoveLegacyWantedConfig(_configFile);

            _emptyBottleRefundEnabled = _configFile.Bind(
                "Blood Homogenizer",
                "Empty Bottle Refund",
                true,
                "Refund one Empty Bottle to the Blood Homogenizer output inventory whenever a craft completes.");

            _relicDebugEventsEnabled = _configFile.Bind(
                "Relic Support",
                "Enable Darkness Throne Relic Grant",
                true,
                "When enabled, the Darkness throne detection patch grants the configured relic buffs upon confirmation events.");

            _itemStackServiceEnabled = _configFile.Bind(
                "Item Tweaks",
                "Enable Item Stack Service",
                true,
                "Apply ItemStackConfig.json values to set max stack sizes for configured prefabs on server start.");

            _debugLogsEnabled = _configFile.Bind(
                "Diagnostics",
                "Enable Debug Logs",
                false,
                "When true, SubtleByte emits all diagnostic log messages. Set to false to silence the mod's logging entirely.");

            _infamySystemEnabled = _configFile.Bind(
                "Faction Infamy",
                "Enable Faction Infamy System",
                false,
                "Master toggle for the Faction Infamy gameplay system. When disabled, the infamy modules, commands, and persistence are not initialised.");

            FactionInfamyConfig.Initialize(_configFile);
        }

        public static void SetDebugLogs(bool enabled)
        {
            Initialize();
            if (_debugLogsEnabled == null)
                return;

            _debugLogsEnabled.Value = enabled;
            _configFile?.Save();
        }

        internal static ConfigEntry<bool> InfamySystemEnabledEntry => _infamySystemEnabled;

        private static void RemoveLegacyWantedConfig(ConfigFile configFile)
        {
            if (configFile is null)
            {
                return;
            }

            var removed = false;
            foreach (var definition in LegacyWantedDefinitions)
            {
                // Explicitly specify the type argument for TryGetEntry<T>
                if (!configFile.TryGetEntry<object>(definition, out _))
                {
                    continue;
                }

                configFile.Remove(definition);
                removed = true;
            }

            if (removed)
            {
                configFile.Save();
            }
        }
    }
}

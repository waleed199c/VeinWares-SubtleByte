using BepInEx;
using VampireCommandFramework;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;
using ProjectM;
using Unity.Entities;
using UnityEngine;
using Stunlock.Core;
using XPRising.Commands;
using XPRising.Components.RandomEncounters;
using XPRising.Configuration;
using XPRising.Models;
using XPRising.Systems;
using XPRising.Transport;
using XPRising.Utils;
using XPShared;
using XPShared.Transport.Messages;
using CommandUtility = XPRising.Utils.CommandUtility;
using GlobalMasteryConfig = XPRising.Configuration.GlobalMasteryConfig;

namespace XPRising
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInDependency("XPRising.XPShared")]
    public class Plugin : BasePlugin
    {
        public static Harmony harmony;

        internal static Plugin Instance { get; private set; }

        public static bool IsInitialized = false;
        public static bool BloodlineSystemActive = false;
        public static bool ChallengeSystemActive = true;
        public static bool ExperienceSystemActive = true;
        public static bool PlayerGroupsActive = true;
        public static int MaxPlayerGroupSize = 5;
        public static bool PowerUpCommandsActive = false;
        public static bool RandomEncountersSystemActive = false;
        public static bool WeaponMasterySystemActive = false;
        public static bool WantedSystemActive = true;
        public static bool WaypointsActive = false;

        public static bool IsDebug { get; private set; } = false;
        public static int CommandLogPrivilegeLevel = 100;
        public static int DefaultTextSize = 10;
        
        public static bool ShouldApplyBuffs =>
            ExperienceSystemActive || BloodlineSystemActive || WeaponMasterySystemActive || PowerUpCommandsActive;

        private static ManualLogSource _logger;
        private static World _serverWorld;
        public static World Server
        {
            get
            {
                if (_serverWorld != null) return _serverWorld;

                _serverWorld = GetWorld("Server")
                    ?? throw new System.Exception("There is no Server world (yet). Did you install a server mod on the client?");
                return _serverWorld;
            }
        }

        public static bool IsServer => Application.productName == "VRisingServer";

        private static World GetWorld(string name)
        {
            foreach (var world in World.s_AllWorlds)
            {
                if (world.Name == name)
                {
                    return world;
                }
            }

            return null;
        }

        public void InitCoreConfig()
        {
            BuffUtil.BuffGuid = Config.Bind("Core", "Buff GUID", BuffUtil.BloodBuffVBlood0.GuidHash, "The GUID of the buff that gets used when mastery, bloodline, etc changes.\nDefault is now boneguard set bonus 2, but you can set anything else too.\nThe only reason to change this is if it clashes with another mod.").Value;
            BuffUtil.AppliedBuff = new PrefabGUID(BuffUtil.BuffGuid);
            BuffUtil.LevelUpBuffId = Config.Bind("Core", "Level up Buff GUID", BuffUtil.LevelUpBuffId, "The GUID of the buff that is applied when levelling up.\nSet to 0 to disable.").Value;
            BuffUtil.LevelUpBuff = new PrefabGUID(BuffUtil.LevelUpBuffId);
            CommandLogPrivilegeLevel = Config.Bind("Core", "Command log privilege level", 100, "Mechanism to ensure logs commands that require privilege above specified amount are logged. Default value logs all \"admin\" commands. Set to 101 to not log any commands.").Value;
            var textSizeString = Config.Bind("Core", "Text size", "small", "Can be used to set the text size output by this mod. Expected values: tiny, small, normal.").Value;
            DefaultTextSize = PlayerPreferences.ConvertTextToSize(textSizeString);

            BloodlineSystemActive = Config.Bind("System", "Enable Bloodline Mastery system", false,  "Enable/disable the bloodline mastery system.").Value;
            ChallengeSystemActive = Config.Bind("System", "Enable Challenge system", true,  "Enable/disable the challenge system.").Value;
            ExperienceSystemActive = Config.Bind("System", "Enable Experience system", true,  "Enable/disable the experience system.").Value;
            PlayerGroupsActive = Config.Bind("System", "Enable Player Groups", true,  "Enable/disable the player group system.").Value;
            MaxPlayerGroupSize = Config.Bind("System", "Maximum player group size", 5,  "Set a maximum value for player group size.").Value;
            // Disabling this for now as it needs more attention.
            //RandomEncountersSystemActive = Config.Bind("System", "Enable Random Encounters system", false,  "Enable/disable the random encounters system.").Value;
            WeaponMasterySystemActive = Config.Bind("System", "Enable Weapon Mastery system", false,  "Enable/disable the weapon mastery system.").Value;
            WantedSystemActive = Config.Bind("System", "Enable Wanted system", false,  "Enable/disable the wanted system.").Value;
            
            // I only want to keep waypoints around as it makes it easier to test.
            //WaypointsActive = Config.Bind("Core", "Enable Wanted system", false,  "Enable/disable waypoints.").Value;

            if (WaypointsActive)
            {
                WaypointCommands.WaypointLimit = Config.Bind("Config", "Waypoint Limit", 2, "Set a waypoint limit for per non-admin user.").Value;
            }

            Config.SaveOnConfigSet = true;
            var autoSaveFrequency = Config.Bind("Auto-save", "Frequency", 2, "Request the frequency for auto-saving the database. Value is in minutes. Minimum is 2.");
            var backupSaveFrequency = Config.Bind("Auto-save", "Backup", 0, "Enable and request the frequency for saving to the backup folder. Value is in minutes. 0 to disable.");
            if (autoSaveFrequency.Value < 2) autoSaveFrequency.Value = 2;
            if (backupSaveFrequency.Value < 0) backupSaveFrequency.Value = 0;
            
            AutoSaveSystem.AutoSaveFrequency = TimeSpan.FromMinutes(autoSaveFrequency.Value);
            AutoSaveSystem.BackupFrequency = backupSaveFrequency.Value < 1 ? TimeSpan.Zero : TimeSpan.FromMinutes(backupSaveFrequency.Value);
            
            Plugin.Log(LogSystem.Core, LogLevel.Info, $"Auto-save frequency set to {AutoSaveSystem.AutoSaveFrequency.ToString()}", true);
            var backupFrequencyMessage = AutoSaveSystem.BackupFrequency == TimeSpan.Zero
                ? $"Auto-save backups disabled."
                : $"Auto-save backup frequency set to {AutoSaveSystem.BackupFrequency.ToString()}";
            Plugin.Log(LogSystem.Core, LogLevel.Info, backupFrequencyMessage, true);
        }

        public override void Load()
        {
            // Ensure the logger is accessible in static contexts.
            _logger = base.Log;
            if(!IsServer)
            {
                Plugin.Log(LogSystem.Core, LogLevel.Warning, $"This is a server plugin. Not continuing to load on client.", true);
                return;
            }
            
            var assemblyConfigurationAttribute = typeof(Plugin).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration;
            IsDebug = buildConfigurationName == "Debug";
            
            InitCoreConfig();
            
            Instance = this;
            // Commenting out until such time the RandomEncounterSystem is re-enabled for use.
            //GameFrame.Initialize();
            
            // Load command registry for systems that are active
            // Note: Displaying these in alphabetical order for ease of maintenance
            CommandUtility.AddCommandType(typeof(AllianceCommands), PlayerGroupsActive);
            CommandUtility.AddCommandType(typeof(CacheCommands));
            CommandUtility.AddCommandType(typeof(ExperienceCommands), ExperienceSystemActive);
            CommandUtility.AddCommandType(typeof(MasteryCommands), WeaponMasterySystemActive || BloodlineSystemActive);
            CommandUtility.AddCommandType(typeof(PermissionCommands));
            CommandUtility.AddCommandType(typeof(PlayerInfoCommands));
            CommandUtility.AddCommandType(typeof(WantedCommands), WantedSystemActive);
            CommandUtility.AddCommandType(typeof(LocalisationCommands));
            CommandUtility.AddCommandType(typeof(ChallengeCommands));
            
            if (IsDebug)
            {
                Plugin.Log(LogSystem.Core, LogLevel.Info, $"****** WARNING ******* Build configuration: {buildConfigurationName}", true);
                Plugin.Log(LogSystem.Core, LogLevel.Info, $"THIS IS ADDING SOME DEBUG COMMANDS. JUST SO THAT YOU ARE AWARE.", true);
                
                PowerUpCommandsActive = true;
                RandomEncountersSystemActive = true;
                WaypointsActive = true;
                CommandUtility.AddCommandType(typeof(PowerUpCommands), PowerUpCommandsActive);
                CommandUtility.AddCommandType(typeof(RandomEncountersCommands), RandomEncountersSystemActive);
                CommandUtility.AddCommandType(typeof(WaypointCommands), WaypointsActive);
            }
            
            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            XPShared.Services.ChatService.OnClientRegisterEvent += ClientActionHandler.HandleClientRegistered;

            XPShared.Services.ChatService.RegisterType<ClientAction>((message, steamId) =>
            {
                var player = Cache.SteamPlayerCache[steamId];
                var user = player.UserEntity.GetUser();
                ClientActionHandler.HandleClientAction(user, message);
            });
            Plugin.Log(LogSystem.Core, LogLevel.Info, $"Plugin is loaded [version: {MyPluginInfo.PLUGIN_VERSION}]", true);
        }

        public override bool Unload()
        {
            Config.Clear();
            harmony.UnpatchSelf();
            XPShared.Services.ChatService.OnClientRegisterEvent -= ClientActionHandler.HandleClientRegistered;
            return true;
        }

        public static void Initialize()
        {
            Plugin.Log(LogSystem.Core, LogLevel.Warning, $"Trying to Initialize {MyPluginInfo.PLUGIN_NAME}: isInitialized == {IsInitialized}", IsInitialized);
            if (IsInitialized) return;
            try
            {
                Plugin.Log(LogSystem.Core, LogLevel.Info, $"Initializing {MyPluginInfo.PLUGIN_NAME}...", true);

                //-- Initialize System
                // Pre-initialise some constants
                Helper.Initialise();

                AutoSaveSystem.ConfigFolder = AutoSaveSystem.NormaliseConfigFolder(SettingsManager.ServerHostSettings.Name);

                // Ensure that internal settings are consistent with server settings
                var serverSettings = Plugin.Server.GetExistingSystemManaged<ServerGameSettingsSystem>();
                var startingXpLevel = serverSettings.Settings.StartingProgressionLevel;
                ExperienceSystem.StartingExp = ExperienceSystem.ConvertLevelToXp(startingXpLevel);
                Plugin.Log(LogSystem.Xp, LogLevel.Info,
                    $"Starting XP level set to {startingXpLevel} to match server settings", ExperienceSystemActive);

                DebugLoggingConfig.Initialize();
                L10N.Initialize();
                if (BloodlineSystemActive || WeaponMasterySystemActive) GlobalMasteryConfig.Initialize();
                if (ExperienceSystemActive) ExperienceConfig.Initialize();
                if (WantedSystemActive) WantedConfig.Initialize();

                if (ChallengeSystemActive) ChallengeSystem.Initialise();
                
                //-- Apply configs

                Plugin.Log(LogSystem.Core, LogLevel.Info, "Initialising player cache and internal database...");
                PlayerCache.CreatePlayerCache();
                AutoSaveSystem.LoadOrInitialiseDatabase();

                // Validate any potential change in permissions
                var commands = CommandUtility.GetAllCommands();
                CommandUtility.ValidatedCommandPermissions(commands);
                // Note for devs: To regenerate Command.md and PermissionSystem.DefaultCommandPermissions, uncomment the following:
                // CommandUtility.GenerateCommandMd(commands);
                // CommandUtility.GenerateDefaultCommandPermissions(commands);

                Plugin.Log(LogSystem.Core, LogLevel.Info, $"Setting CommandRegistry middleware");
                CommandRegistry.Middlewares.Add(new CommandUtility.PermissionMiddleware());

                if (RandomEncountersSystemActive)
                {
                    RandomEncounters.GameData_OnInitialize();
                    RandomEncounters.EncounterTimer = new WorldTimer();
                    RandomEncounters.StartEncounterTimer();
                }

                if (ChallengeSystemActive)
                {
                    // Validate challenges
                    ChallengeSystem.ValidateChallenges();
                }

                Plugin.Log(LogSystem.Core, LogLevel.Info, "Finished initialising", true);

                IsInitialized = true;
            }
            catch (Exception e)
            {
                Plugin.Log(LogSystem.Core, LogLevel.Error, $"Initialisation failed! Error: {e.Message}", true);
                Plugin.Log(LogSystem.Core, LogLevel.Error, $"{e.StackTrace}", true);
            }
        }

        public enum LogSystem
        {
            Alliance,
            Bloodline,
            Buff,
            Challenge,
            Core,
            Death,
            Debug,
            Faction,
            Mastery,
            PowerUp,
            RandomEncounter,
            SquadSpawn,
            Wanted,
            Xp
        }
        
        public new static void Log(LogSystem system, LogLevel logLevel, string message, bool forceLog = false)
        {
            var isLogging = forceLog || DebugLoggingConfig.IsLogging(system);
            if (isLogging) _logger.Log(logLevel, ToLogMessage(system, message));
        }
        
        // Log overload to allow potentially more computationally expensive logs to be hidden when not being logged
        public new static void Log(LogSystem system, LogLevel logLevel, Func<string> messageGenerator, bool forceLog = false)
        {
            var isLogging = forceLog || DebugLoggingConfig.IsLogging(system);
            if (isLogging) _logger.Log(logLevel, ToLogMessage(system, messageGenerator()));
        }
        
        // Log overload to allow enumerations to only be iterated over if logging
        public new static void Log(LogSystem system, LogLevel logLevel, IEnumerable<string> messages, bool forceLog = false)
        {
            var isLogging = forceLog || DebugLoggingConfig.IsLogging(system);
            if (!isLogging) return;
            foreach (var message in messages)
            {
                _logger.Log(logLevel, ToLogMessage(system, message));
            }
        }

        private static string ToLogMessage(LogSystem logSystem, string message)
        {
            return $"{DateTime.Now:u}: [{Enum.GetName(logSystem)}] {message}";
        }
    }
}

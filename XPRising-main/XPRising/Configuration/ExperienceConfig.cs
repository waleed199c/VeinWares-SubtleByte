﻿using BepInEx.Configuration;
using BepInEx.Logging;
using XPRising.Systems;
using XPRising.Utils;

namespace XPRising.Configuration;

public static class ExperienceConfig
{
    private static ConfigFile _configFile; 
    
    public static void Initialize()
    {
        Plugin.Log(Plugin.LogSystem.Core, LogLevel.Info, "Loading XP config");
        var configPath = AutoSaveSystem.ConfirmFile(AutoSaveSystem.ConfigPath, "ExperienceConfig.cfg");
        _configFile = new ConfigFile(configPath, true);
        
        // Currently, we are never updating and saving the config file in game, so just load the values.

        ExperienceSystem.MaxLevel = _configFile.Bind("Experience", "Max Level", 110, "Configure the experience system max level.").Value;
        ExperienceSystem.ExpMultiplier = _configFile.Bind("Experience", "Multiplier", 1.5f, "Multiply the EXP gained by player.\n" +
                "Ex.: 0.7f -> Will reduce the EXP gained by 30%\nFormula: BaseExpValue * EXPMultiplier").Value;
        ExperienceSystem.VBloodMultiplier = _configFile.Bind("Experience", "VBlood Multiplier", 15f, "Multiply EXP gained from VBlood kills.\n" +
                "Formula: EXPGained * VBloodMultiplier * EXPMultiplier").Value;
        ExperienceSystem.GroupMaxDistance = _configFile.Bind("Experience", "Group Range", 40f, "Set the maximum distance an ally (player) has to be from the player for them to share EXP with the player. Set this to 0 to disable groups.").Value;
        ExperienceSystem.GroupXpBuffGrowth = _configFile.Bind("Experience", "Group XP buff", 0.3f, "Set the amount of additional XP that a player will get for each additional player in their group.\n" +
                "Example with buff of 0.3: 2 players = 1.3 XP multiplier; 3 players = 1.3 x 1.3 = 1.69 XP multiplier").Value;
        ExperienceSystem.MaxGroupXpBuff = _configFile.Bind("Experience", "Max group XP buff", 2f, "Set the maximum increase in XP that a player can gain when playing in a group.").Value;
        ExperienceSystem.LevelRange = _configFile.Bind("Experience", "Level range", 30f, "Sets a level range over which player XP gain is maximised.\n" +
            "Check documentation for a longer description.").Value;

        ExperienceSystem.PvpXpLossPercent = _configFile.Bind("Rates, Experience", "PvP XP Loss Percent", 0f, "Sets the percentage of XP to the next level lost on a PvP death").Value;
        ExperienceSystem.PveXpLossPercent = _configFile.Bind("Rates, Experience", "PvE XP Loss Percent", 10f, "Sets the percentage of XP to the next level lost on a PvE death").Value;
    }
}
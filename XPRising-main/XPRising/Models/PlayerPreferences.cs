﻿using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using XPRising.Systems;
using XPRising.Transport;

namespace XPRising.Models;

public struct PlayerPreferences
{
    private const string DefaultXpColour = "#ffcc33";
    private const string DefaultMasteryColour = "#ccff33";
    private const string DefaultBloodMasteryColour = "#cc0000";
    private const string DefaultChallengeActiveColour = "#ffcc33";
    private const string DefaultChallengeFailedColour = "#cc0000";
    private const string DefaultChallengeInactiveColour = "#555555";

    public bool LoggingWanted = false;
    public bool LoggingExp = false;
    public bool LoggingMastery = false;
    public bool LoggingChallenges = false;
    public bool IgnoringInvites = false;
    public bool ScrollingCombatText = true;
    public string Language = L10N.DefaultLanguage;
    public int TextSize = Plugin.DefaultTextSize;
    public Actions.BarState UIProgressDisplay = Actions.BarState.Active;
    public string[] BarColours = [];
    [JsonIgnore] public string XpBarColour => BarColours.ElementAtOrDefault(0) ?? DefaultXpColour;
    [JsonIgnore] public string MasteryBarColour => BarColours.ElementAtOrDefault(1) ?? DefaultMasteryColour;
    [JsonIgnore] public string BloodMasteryBarColour => BarColours.ElementAtOrDefault(2) ?? DefaultBloodMasteryColour;
    [JsonIgnore] public string ChallengeActiveBarColour => BarColours.ElementAtOrDefault(3) ?? DefaultChallengeActiveColour;
    [JsonIgnore] public string ChallengeFailedBarColour => BarColours.ElementAtOrDefault(4) ?? DefaultChallengeFailedColour;
    [JsonIgnore] public string ChallengeInactiveBarColour => BarColours.ElementAtOrDefault(3) ?? DefaultChallengeInactiveColour;
    [JsonIgnore]
    public string[] BarColoursWithDefaults => new string[] {XpBarColour, MasteryBarColour, BloodMasteryBarColour, ChallengeActiveBarColour, ChallengeFailedBarColour, ChallengeInactiveBarColour};

    public PlayerPreferences()
    {
    }

    public static int ConvertTextToSize(string textSize)
    {
        return textSize switch
        {
            "tiny" => 10,
            "small" => 12,
            "normal" => 16,
            _ => 12
        };
    }
    
    public static string ConvertSizeToText(int size)
    {
        return size switch
        {
            10 => "tiny",
            12 => "small",
            16 => "normal",
            _ => "small"
        };
    }
}
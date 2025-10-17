using System;
using BepInEx.Configuration;

namespace VeinWares.SubtleByte.Infrastructure;

public sealed class ModuleConfig
{
    public ModuleConfig(
        ConfigEntry<bool> bottleRefundEnabled,
        ConfigEntry<bool> wantedSystemEnabled)
    {
        BottleRefundEnabled = bottleRefundEnabled ?? throw new ArgumentNullException(nameof(bottleRefundEnabled));
        WantedSystemEnabled = wantedSystemEnabled ?? throw new ArgumentNullException(nameof(wantedSystemEnabled));
    }

    public ConfigEntry<bool> BottleRefundEnabled { get; }

    public ConfigEntry<bool> WantedSystemEnabled { get; }
}

using System;
using BepInEx.Configuration;

namespace VeinWares.SubtleByte.Infrastructure;

public sealed class ModuleConfig
{
    public ModuleConfig(ConfigEntry<bool> bottleRefundEnabled)
    {
        BottleRefundEnabled = bottleRefundEnabled ?? throw new ArgumentNullException(nameof(bottleRefundEnabled));
    }

    public ConfigEntry<bool> BottleRefundEnabled { get; }
}

using System;
using BepInEx.Configuration;

namespace VeinWares.SubtleByte.Infrastructure;

public sealed class ModuleConfig
{
    public ModuleConfig(
        ConfigEntry<bool> bottleRefundEnabled,
        ConfigEntry<bool> infamySystemEnabled)
    {
        BottleRefundEnabled = bottleRefundEnabled ?? throw new ArgumentNullException(nameof(bottleRefundEnabled));
        InfamySystemEnabled = infamySystemEnabled ?? throw new ArgumentNullException(nameof(infamySystemEnabled));
    }

    public ConfigEntry<bool> BottleRefundEnabled { get; }

    public ConfigEntry<bool> InfamySystemEnabled { get; }
}

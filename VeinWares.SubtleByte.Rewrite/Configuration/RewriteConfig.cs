using BepInEx.Configuration;

namespace VeinWares.SubtleByte.Rewrite.Configuration;

public sealed class RewriteConfig
{
    public RewriteConfig(ConfigFile config)
    {
        BottleRefundEnabled = config.Bind(
            "Bottle Refund",
            "Enabled",
            true,
            "Refund one Empty Bottle to the Blood Press output inventory when a mix completes.");
    }

    public ConfigEntry<bool> BottleRefundEnabled { get; }
}

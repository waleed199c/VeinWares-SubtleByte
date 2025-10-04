using VampireCommandFramework;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Commands
{
    public static class DebugCommands
    {
        [Command("sb_debug", adminOnly: true)]
        public static void ToggleDebug(ChatCommandContext ctx, string mode = "")
        {
            var on = mode.Equals("on", System.StringComparison.OrdinalIgnoreCase) 
                  || mode.Equals("true", System.StringComparison.OrdinalIgnoreCase) 
                  || mode.Equals("1");
            var off = mode.Equals("off", System.StringComparison.OrdinalIgnoreCase) 
                   || mode.Equals("false", System.StringComparison.OrdinalIgnoreCase) 
                   || mode.Equals("0");
            if (!on && !off)
            {
                ctx.Reply($"[Debug] Verbose logs are {(SubtleBytePluginConfig.DebugLogsEnabled ? "ON" : "OFF")}. Usage: .sb_debug on|off");
                return;
            }
            SubtleBytePluginConfig.SetDebugLogs(on);
            if (on)
                ModLogger.Info("[Debug] Verbose logs enabled via command.");
            else
                ModLogger.Info("[Debug] Verbose logs disabled via command.");
            ctx.Reply($"[Debug] Verbose logs {(on ? "ENABLED" : "DISABLED")}.");
        }
    }
}

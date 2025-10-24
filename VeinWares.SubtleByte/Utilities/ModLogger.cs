using VeinWares.SubtleByte.Config;

namespace VeinWares.SubtleByte.Utilities
{
    internal static class ModLogger
    {
        private static bool VerboseEnabled => SubtleBytePluginConfig.VerboseLogsEnabled;

        public static void Debug(string message)
        {
            if (VerboseEnabled)
            {
                Core.Log?.LogDebug(message);
            }
        }

        public static void Info(string message, bool verboseOnly = true)
        {
            if (!verboseOnly || VerboseEnabled)
            {
                Core.Log?.LogInfo(message);
            }
        }

        public static void Warn(string message)
        {
            Core.Log?.LogWarning(message);
        }

        public static void Error(string message)
        {
            Core.Log?.LogError(message);
        }
    }
}

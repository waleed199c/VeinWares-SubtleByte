using VeinWares.SubtleByte.Config;

namespace VeinWares.SubtleByte.Utilities
{
    internal static class ModLogger
    {
        private static bool Enabled => SubtleBytePluginConfig.DebugLogsEnabled;

        public static void Debug(string message)
        {
            if (Enabled)
                Core.Log?.LogDebug(message);
        }

        public static void Info(string message)
        {
            if (Enabled)
                Core.Log?.LogInfo(message);
        }

        public static void Warn(string message)
        {
            if (Enabled)
                Core.Log?.LogWarning(message);
        }

        public static void Error(string message)
        {
            if (Enabled)
                Core.Log?.LogError(message);
        }
    }
}

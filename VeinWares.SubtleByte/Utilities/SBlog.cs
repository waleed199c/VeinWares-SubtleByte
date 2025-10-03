namespace VeinWares.SubtleByte.Utilities
{
    /// <summary>
    /// Centralized logging with a runtime debug toggle. 
    /// - Info/Debug are gated behind DebugEnabled.
    /// - Warn/Error always log.
    /// No behavioral changes; swap calls from Core.Log.LogX to SBlog.X.
    /// </summary>
    internal static class SBlog
    {
        public static bool DebugEnabled { get; private set; } = false;

        public static void SetDebug(bool enabled)
        {
            DebugEnabled = enabled;
            if (enabled) Warn("[Debug] Verbose logs enabled.");
            else Warn("[Debug] Verbose logs disabled.");
        }

        public static void Debug(string msg)
        {
            if (DebugEnabled) Core.Log.LogDebug(msg);
        }

        public static void Info(string msg)
        {
            if (DebugEnabled) Core.Log.LogInfo(msg);
        }

        public static void Warn(string msg)
        {
            Core.Log.LogWarning(msg);
        }

        public static void Error(string msg)
        {
            Core.Log.LogError(msg);
        }
    }
}

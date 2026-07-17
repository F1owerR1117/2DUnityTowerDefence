using UnityEngine;

/// <summary>
/// Build 中完全抑制 Console 日志弹窗。
/// Editor 中完全不受影响。
/// </summary>
public static class BuildLogSuppressor
{
#if !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Application.unityLogger = new QuietLogger();
    }
#endif

    /// <summary>
    /// 静默日志器：所有日志类型均不输出到 Console。
    /// </summary>
    private class QuietLogger : ILogger
    {
        public LogType filterLogType { get; set; } = LogType.Exception;
        public bool logEnabled { get; set; } = false;
        public bool logHandlerEnabled { get; set; } = false;
        public ILogHandler logHandler { get; set; } = NullHandler.Instance;

        public void Log(LogType logType, object message) { }
        public void Log(LogType logType, string tag, object message) { }
        public void Log(LogType logType, string tag, object message, Object context) { }
        public void Log(object message) { }
        public void Log(string tag, object message) { }
        public void LogWarning(string tag, object message) { }
        public void LogError(string tag, object message) { }
        public bool IsLogTypeAllowed(LogType logType) => false;

        private class NullHandler : ILogHandler
        {
            public static readonly NullHandler Instance = new NullHandler();
            public void LogFormat(LogType logType, Object context, string format, params object[] args) { }
            public void LogException(System.Exception exception, Object context) { }
        }
    }
}

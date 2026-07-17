using System;
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
        Debug.unityLogger = new QuietLogger();
    }
#endif

    private class QuietLogger : ILogger
    {
        public LogType filterLogType { get; set; } = LogType.Exception;
        public bool logEnabled { get; set; } = false;
        public bool logHandlerEnabled { get; set; } = false;
        public ILogHandler logHandler { get; set; } = NullHandler.Instance;

        public void Log(LogType logType, object message) { }
        public void Log(LogType logType, string tag, object message) { }
        public void Log(LogType logType, string tag, object message, UnityEngine.Object context) { }
        public void Log(LogType logType, object message, UnityEngine.Object context) { }
        public void Log(object message) { }
        public void Log(string tag, object message) { }
        public void Log(string tag, object message, UnityEngine.Object context) { }
        public void LogWarning(string tag, object message) { }
        public void LogWarning(string tag, object message, UnityEngine.Object context) { }
        public void LogError(string tag, object message) { }
        public void LogError(string tag, object message, UnityEngine.Object context) { }
        public void LogFormat(LogType logType, string format, params object[] args) { }
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) { }
        public void LogException(Exception exception) { }
        public void LogException(Exception exception, UnityEngine.Object context) { }
        public bool IsLogTypeAllowed(LogType logType) => false;
    }

    private class NullHandler : ILogHandler
    {
        public static readonly NullHandler Instance = new NullHandler();
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) { }
        public void LogException(Exception exception, UnityEngine.Object context) { }
    }
}

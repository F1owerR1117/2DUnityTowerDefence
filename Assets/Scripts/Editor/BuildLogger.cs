#if !UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Build 中抑制 Error/Exception 屏幕弹窗。
/// 通过自定义 ILogger 拦截 Error 类型，仅写入日志文件不弹窗。
/// Editor 中完全不受影响。
/// </summary>
public static class BuildErrorSuppressor
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Application.unityLogger = new BuildSafeLogger(Application.unityLogger);
        Debug.Log("[BuildErrorSuppressor] 已启用 Build 错误弹窗抑制");
    }
}

/// <summary>
/// Build 专用安全日志：Error/Exception 只写文件不弹窗，其他类型正常输出。
/// </summary>
public class BuildSafeLogger : ILogger
{
    private readonly ILogger _default;
    private readonly LogHandler _fileHandler;

    public BuildSafeLogger(ILogger defaultLogger)
    {
        _default = defaultLogger;
        _fileHandler = new LogHandler();
    }

    public LogType filterLogType { get => _default.filterLogType; set => _default.filterLogType = value; }
    public bool logEnabled { get => _default.logEnabled; set => _default.logEnabled = value; }
    public bool logHandlerEnabled { get => _default.logHandlerEnabled; set => _default.logHandlerEnabled = value; }
    public ILogHandler logHandler { get => _default.logHandler; set => _default.logHandler = value; }

    public void Log(LogType logType, object message)
    {
        if (ShouldSuppress(logType)) { LogToFile(logType, message); return; }
        _default.Log(logType, message);
    }

    public void Log(LogType logType, string tag, object message)
    {
        if (ShouldSuppress(logType)) { LogToFile(logType, tag, message); return; }
        _default.Log(logType, tag, message);
    }

    public void Log(LogType logType, string tag, object message, Object context)
    {
        if (ShouldSuppress(logType)) { LogToFile(logType, tag, message); return; }
        _default.Log(logType, tag, message, context);
    }

    public void Log(object message) => _default.Log(message);
    public void Log(string tag, object message) => _default.Log(tag, message);

    public void LogWarning(string tag, object message) => _default.LogWarning(tag, message);
    public void LogError(string tag, object message)
    {
        // LogError 只写文件，不弹窗
        Debug.unityLogger.logHandler.LogFormat(LogType.Error, null, "[{0}] {1}", tag, message);
    }

    public bool IsLogTypeAllowed(LogType logType) => !ShouldSuppress(logType);

    private bool ShouldSuppress(LogType type) =>
        type == LogType.Error || type == LogType.Exception || type == LogType.Assert;

    private void LogToFile(LogType type, object message) =>
        Debug.unityLogger.logHandler.LogFormat(type, null, "{0}", message);

    private void LogToFile(LogType type, string tag, object message) =>
        Debug.unityLogger.logHandler.LogFormat(type, null, "[{0}] {1}", tag, message);

    private class LogHandler : ILogHandler
    {
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            // 写入 Unity 内部日志文件（filelog）
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Application.persistentDataPath, "build_errors.log"),
                $"[{System.DateTime.Now:HH:mm:ss}] [{logType}] {string.Format(format, args)}\n");
        }

        public void LogException(System.Exception exception, Object context) { }
    }
}
#endif

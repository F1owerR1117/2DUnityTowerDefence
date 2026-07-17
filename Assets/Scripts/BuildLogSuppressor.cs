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
        Debug.unityLogger.logEnabled = false;
        Debug.unityLogger.logHandler = new NullHandler();
    }

    private class NullHandler : ILogHandler
    {
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) { }
        public void LogException(Exception exception, UnityEngine.Object context) { }
    }
#endif
}

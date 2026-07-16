using UnityEngine;
using System.IO;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 将日志写入文件，用于构建版本调试。
    /// 挂载到场景中的 GameObject 即可。
    /// </summary>
    public class FileLogger : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private string logFileName = "GameLog.txt";

        private string _logPath;
        private StreamWriter _writer;

        private void Awake()
        {
            if (!enableLogging) return;

            // 构建版本：日志在项目根目录（Data文件夹的上一级）
            // 编辑器：日志在 Assets 文件夹
            #if UNITY_EDITOR
            string logDir = Application.dataPath;
            #else
            string logDir = Path.GetDirectoryName(Application.dataPath);
            #endif

            _logPath = Path.Combine(logDir, logFileName);
            _writer = new StreamWriter(_logPath, true);
            _writer.WriteLine($"=== Log Started: {System.DateTime.Now} ===");
            _writer.Flush();

            Application.logMessageReceived += OnLogMessage;
            Debug.Log($"[FileLogger] 日志文件: {_logPath}");
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
            _writer?.Close();
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (_writer == null) return;

            string logEntry = $"[{System.DateTime.Now:HH:mm:ss.fff}] [{type}] {condition}";
            _writer.WriteLine(logEntry);

            if (type == LogType.Error || type == LogType.Exception)
            {
                _writer.WriteLine($"Stack: {stackTrace}");
            }

            _writer.Flush();
        }

        public string GetLogPath() => _logPath;
    }
}
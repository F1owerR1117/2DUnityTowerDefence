using System.IO;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 网络日志写入文件。
    /// 日志位置：Application.persistentDataPath/network_log_{slot}.txt
    /// </summary>
    public class NetworkLogger : MonoBehaviour
    {
        private StreamWriter _writer;
        private int _slot;

        public void Initialize(int slot)
        {
            _slot = slot;
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"network_log_slot{slot}.txt");
            _writer = new StreamWriter(path, false);
            _writer.WriteLine($"=== Network Log Slot {slot} | {System.DateTime.Now} ===");
            _writer.Flush();
            Debug.Log($"[NetworkLogger] 日志写入: {path}");
        }

        void OnEnable()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessage;
            _writer?.Close();
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith("[Network") || condition.StartsWith("[Bootstrapper") ||
                condition.StartsWith("[飞筒") || condition.StartsWith("[BattleManager"))
            {
                _writer?.WriteLine($"[{type}] {condition}");
                _writer?.Flush();
            }
        }
    }
}

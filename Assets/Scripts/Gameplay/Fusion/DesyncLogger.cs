using UnityEngine;
using System.IO;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// Desync 日志记录器。
    /// 记录 Hash 对比结果，用于调试 Desync 问题。
    /// </summary>
    public class DesyncLogger
    {
        private string _logPath;
        private StreamWriter _writer;

        public DesyncLogger(string fileName = "DesyncLog.txt")
        {
            _logPath = Path.Combine(Application.persistentDataPath, fileName);
            _writer = new StreamWriter(_logPath, true);
            _writer.WriteLine($"=== Desync Log Started: {System.DateTime.Now} ===");
            _writer.Flush();
        }

        /// <summary>
        /// 记录 Hash
        /// </summary>
        public void LogHash(int tick, uint hash, string role)
        {
            _writer.WriteLine($"[{role}] Tick={tick}, Hash={hash}");
            _writer.Flush();
        }

        /// <summary>
        /// 记录 Desync
        /// </summary>
        public void LogDesync(int tick, uint hashA, uint hashB, string roleA, string roleB)
        {
            _writer.WriteLine($"[DESYNC] Tick={tick}");
            _writer.WriteLine($"  {roleA}: Hash={hashA}");
            _writer.WriteLine($"  {roleB}: Hash={hashB}");
            _writer.Flush();

            Debug.LogError($"[DesyncDetector] DESYNC at Tick {tick}! {roleA}={hashA}, {roleB}={hashB}");
        }

        /// <summary>
        /// 记录同步成功
        /// </summary>
        public void LogSync(int tick, uint hash)
        {
            _writer.WriteLine($"[SYNC OK] Tick={tick}, Hash={hash}");
            _writer.Flush();
        }

        public void Close()
        {
            _writer?.Close();
        }
    }
}
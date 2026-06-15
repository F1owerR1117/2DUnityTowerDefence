using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 存档系统（基于 PlayerPrefs）。
    /// 存储玩家金币、首次胜利状态、图鉴解锁等持久化数据。
    ///
    /// 使用方法：
    /// - SaveSystem.Load() 在游戏启动时调用
    /// - SaveSystem.Save() 在游戏结束/退出时调用
    /// - 通过 SaveSystem.Data 访问当前存档数据
    /// - SaveSystem.UnlockCodexEntry(id) 解锁图鉴条目
    /// </summary>
    public static class SaveSystem
    {
        private const string KEY_GOLD = "Save_Gold";
        private const string KEY_FIRST_WIN = "Save_FirstWin";
        private const string KEY_GAMES_PLAYED = "Save_GamesPlayed";
        private const string KEY_GAMES_WON = "Save_GamesWon";
        private const string CODEX_KEY_PREFIX = "Codex_";

        /// <summary>当前存档数据（运行时缓存）</summary>
        public static SaveData Data;

        /// <summary>已解锁的图鉴条目 ID 集合（运行时缓存）</summary>
        private static readonly HashSet<string> _unlockedCodexEntries = new();

        /// <summary>从 PlayerPrefs 加载存档</summary>
        public static void Load()
        {
            Data = new SaveData
            {
                Gold = PlayerPrefs.GetFloat(KEY_GOLD, 0f),
                HasFirstWin = PlayerPrefs.GetInt(KEY_FIRST_WIN, 0) == 1,
                GamesPlayed = PlayerPrefs.GetInt(KEY_GAMES_PLAYED, 0),
                GamesWon = PlayerPrefs.GetInt(KEY_GAMES_WON, 0),
            };
        }

        /// <summary>保存当前存档到 PlayerPrefs</summary>
        public static void Save()
        {
            PlayerPrefs.SetFloat(KEY_GOLD, Data.Gold);
            PlayerPrefs.SetInt(KEY_FIRST_WIN, Data.HasFirstWin ? 1 : 0);
            PlayerPrefs.SetInt(KEY_GAMES_PLAYED, Data.GamesPlayed);
            PlayerPrefs.SetInt(KEY_GAMES_WON, Data.GamesWon);
            PlayerPrefs.Save();
        }

        /// <summary>重置存档（调试用）</summary>
        public static void Reset()
        {
            Data = new SaveData();
            PlayerPrefs.DeleteKey(KEY_GOLD);
            PlayerPrefs.DeleteKey(KEY_FIRST_WIN);
            PlayerPrefs.DeleteKey(KEY_GAMES_PLAYED);
            PlayerPrefs.DeleteKey(KEY_GAMES_WON);
            PlayerPrefs.Save();
        }

        /// <summary>游戏结束时更新存档</summary>
        /// <param name="playerWon">玩家是否胜利</param>
        /// <param name="goldEarned">本局获得金币</param>
        public static void OnGameEnded(bool playerWon, float goldEarned)
        {
            Data.GamesPlayed++;
            Data.Gold += goldEarned;
            if (playerWon)
            {
                Data.GamesWon++;
                if (!Data.HasFirstWin)
                    Data.HasFirstWin = true;
            }
            Save();
        }

        // ─── 图鉴解锁 ───────────────────────────────

        /// <summary>解锁图鉴条目。已解锁时直接返回，不触发写盘。</summary>
        public static void UnlockCodexEntry(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_unlockedCodexEntries.Contains(id)) return;

            _unlockedCodexEntries.Add(id);
            PlayerPrefs.SetInt(CODEX_KEY_PREFIX + id, 1);
            // 不立即 Save()，由调用方在适当时机批量写盘
        }

        /// <summary>查询图鉴条目是否已解锁</summary>
        public static bool IsCodexEntryUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return _unlockedCodexEntries.Contains(id)
                   || PlayerPrefs.GetInt(CODEX_KEY_PREFIX + id, 0) == 1;
        }

        /// <summary>将指定条目标记为已缓存（Load 时调用）</summary>
        public static void LoadCodexEntry(string id)
        {
            if (!string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(CODEX_KEY_PREFIX + id, 0) == 1)
                _unlockedCodexEntries.Add(id);
        }

        /// <summary>批量加载图鉴解锁状态（启动时调用一次）</summary>
        public static void LoadAllCodexEntries(IEnumerable<string> allIds)
        {
            _unlockedCodexEntries.Clear();
            foreach (var id in allIds)
            {
                if (PlayerPrefs.GetInt(CODEX_KEY_PREFIX + id, 0) == 1)
                    _unlockedCodexEntries.Add(id);
            }
        }

        /// <summary>获取已解锁数量</summary>
        public static int GetUnlockedCodexCount() => _unlockedCodexEntries.Count;
    }

    /// <summary>存档数据结构</summary>
    public struct SaveData
    {
        /// <summary>累计金币</summary>
        public float Gold;
        /// <summary>是否已有首次胜利</summary>
        public bool HasFirstWin;
        /// <summary>总对局数</summary>
        public int GamesPlayed;
        /// <summary>胜利次数</summary>
        public int GamesWon;
    }
}

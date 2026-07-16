using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 大厅身份持久服务（DontDestroyOnLoad）。
    /// Host 分配 slot，所有场景读取。
    /// Fusion 只能同步结果，不能生成身份。
    /// </summary>
    public class LobbyIdentityService : MonoBehaviour
    {
        public static LobbyIdentityService Instance { get; private set; }

        private readonly Dictionary<PlayerRef, int> _playerToSlot = new();
        private readonly Dictionary<int, PlayerRef> _slotToPlayer = new();
        private int _nextSlot = 0;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Host 分配 slot（唯一权威入口）。
        /// </summary>
        public int AssignSlot(PlayerRef player)
        {
            int slot = _nextSlot++;
            _playerToSlot[player] = slot;
            _slotToPlayer[slot] = player;
            Debug.Log($"[LobbyIdentity] Player {player.RawEncoded} → Slot {slot}");
            return slot;
        }

        /// <summary>
        /// 清理 slot（玩家离开）。
        /// </summary>
        public void RemoveSlot(PlayerRef player)
        {
            if (_playerToSlot.TryGetValue(player, out int slot))
            {
                Debug.Log($"[LobbyIdentity] Player {player.RawEncoded} left, Slot {slot} freed");
                _playerToSlot.Remove(player);
                _slotToPlayer.Remove(slot);
            }
        }

        public int GetSlot(PlayerRef player)
        {
            return _playerToSlot.TryGetValue(player, out int slot) ? slot : -1;
        }

        public PlayerRef GetPlayer(int slot)
        {
            return _slotToPlayer.TryGetValue(slot, out var player) ? player : default;
        }

        public bool IsReady() => Instance != null;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}

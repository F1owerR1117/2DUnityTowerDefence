using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Gameplay.Network;
using Fusion;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 大厅身份持久服务（DontDestroyOnLoad）。
    /// Host 分配 slot，所有场景读取。
    /// Slot 基于 PlayerRef.RawEncoded 排序确定性分配，
    /// 确保所有机器结果一致。
    /// </summary>
    public class LobbyIdentityService : MonoBehaviour
    {
        public static LobbyIdentityService Instance { get; private set; }

        private readonly Dictionary<PlayerRef, int> _playerToSlot = new();
        private readonly Dictionary<int, PlayerRef> _slotToPlayer = new();

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
        /// 基于 PlayerRef.RawEncoded 排序，确保所有机器结果一致。
        /// </summary>
        public int AssignSlot(PlayerRef player)
        {
            var runner = FusionService.Instance.Runner;
            if (runner == null)
            {
                Debug.LogError("[LobbyIdentity] Runner 不存在，无法分配 slot");
                return -1;
            }

            var allPlayers = runner.ActivePlayers.ToList();
            allPlayers.Sort((a, b) => a.RawEncoded.CompareTo(b.RawEncoded));

            int slot = -1;
            for (int i = 0; i < allPlayers.Count; i++)
            {
                if (allPlayers[i] == player)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                Debug.LogError($"[LobbyIdentity] Player not found: {player}");
                return -1;
            }

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

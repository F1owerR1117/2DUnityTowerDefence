using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 跨场景游戏会话数据（纯静态，无 MonoBehaviour）。
    /// 存储叫分期的结果，供游戏场景读取。
    /// Phase 5 后仅作为桥接数据缓存，核心逻辑由 WorldState 替代。
    /// </summary>
    public static class GameSession
    {
        public static event System.Action OnRuntimeReset;

        // ─── 网络模式（锁定后不可变） ───
        public static bool IsNetworkMode { get; private set; }
        private static bool _networkModeLocked;

        public static void SetNetworkMode(bool value)
        {
            if (_networkModeLocked) return;
            IsNetworkMode = value;
            _networkModeLocked = true;
        }

        public static void ResetNetworkModeLock()
        {
            _networkModeLocked = false;
            IsNetworkMode = false;
        }

        // ─── 叫分结果 ───
        public static float BidMultiplier = 1f;
        public static bool HasResult;
        public static int NetworkSeed;
        public static int LandlordSlot = -1;

        // ─── AI 槽位 ───
        public static HashSet<int> AISlots = new HashSet<int>();
        public static HashSet<int> RawAISlots = new HashSet<int>();

        // ─── 本机身份 ───
        public static bool PlayerIsLandlord => _localPlayerIsLandlord;
        private static bool _localPlayerIsLandlord;

        public static void Reset()
        {
            BidMultiplier = 1f;
            HasResult = false;
            // IsNetworkMode 不在此处修改——由 SetNetworkMode 在启动时锁定
            NetworkSeed = 0;
            LandlordSlot = -1;
            AISlots = new HashSet<int>();
            RawAISlots = new HashSet<int>();
            _localPlayerIsLandlord = false;
            OnRuntimeReset?.Invoke();
        }

        public static void SetResult(bool localIsLandlord, float multiplier, int landlordBaseIndex, int[] farmerBaseIndices)
        {
            _localPlayerIsLandlord = localIsLandlord;
            BidMultiplier = multiplier;
            HasResult = true;
            LandlordSlot = 0;
        }

        public static void SetResultNetwork(int landlordSlot, float multiplier)
        {
            LandlordSlot = landlordSlot;
            BidMultiplier = multiplier;
            HasResult = true;
            // _localPlayerIsLandlord 由 SetLocalPlayerIsLandlord 单独设置
        }

        public static void SetLocalPlayerIsLandlord(bool isLandlord)
        {
            _localPlayerIsLandlord = isLandlord;
        }

        public static bool IsAISlot(int slot) => AISlots.Contains(slot);
    }
}

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
        /// <summary>运行时重置事件</summary>
        public static event System.Action OnRuntimeReset;

        // ─── 叫分结果 ───
        public static float BidMultiplier = 1f;
        public static bool HasResult;
        public static bool IsNetworkMode;
        public static int NetworkSeed;
        public static int LandlordSlot = -1;

        // ─── AI 槽位 ───
        public static HashSet<int> AISlots = new HashSet<int>();
        public static HashSet<int> RawAISlots = new HashSet<int>();

        // ─── 本机身份 ───
        public static bool PlayerIsLandlord => _localPlayerIsLandlord;
        private static bool _localPlayerIsLandlord;

        // ─── 方法 ───

        public static void Reset()
        {
            BidMultiplier = 1f;
            HasResult = false;
            IsNetworkMode = false;
            NetworkSeed = 0;
            LandlordSlot = -1;
            AISlots = new HashSet<int>();
            RawAISlots = new HashSet<int>();
            _localPlayerIsLandlord = false;
            OnRuntimeReset?.Invoke();
        }

        /// <summary>写入叫分结果（单机模式）</summary>
        public static void SetResult(bool localIsLandlord, float multiplier, int landlordBaseIndex, int[] farmerBaseIndices)
        {
            _localPlayerIsLandlord = localIsLandlord;
            BidMultiplier = multiplier;
            HasResult = true;
            IsNetworkMode = false;
            LandlordSlot = 0;
        }

        /// <summary>写入叫分结果（联机模式）</summary>
        public static void SetResultNetwork(int landlordSlot, float multiplier)
        {
            LandlordSlot = landlordSlot;
            BidMultiplier = multiplier;
            HasResult = true;
            IsNetworkMode = true;
            _localPlayerIsLandlord = false;
        }

        /// <summary>设置本机是否地主</summary>
        public static void SetLocalPlayerIsLandlord(bool isLandlord)
        {
            _localPlayerIsLandlord = isLandlord;
        }

        /// <summary>判断指定槽位是否为 AI</summary>
        public static bool IsAISlot(int slot) => AISlots.Contains(slot);
    }
}

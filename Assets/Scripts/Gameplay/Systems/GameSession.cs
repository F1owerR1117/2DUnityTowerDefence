using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 跨场景游戏会话数据（纯静态，无 MonoBehaviour）。
    /// 存储叫分期的结果，供游戏场景读取。
    ///
    /// ⚠️ [已废弃] Phase 5 后降级为数据缓存。
    /// 身份系统由 FusionGameManager.OnPlayerJoined 分配 slot。
    /// 新功能请使用 WorldState 替代。
    /// </summary>
    public static class GameSession
    {
        /// <summary>运行时重置事件</summary>
        public static event System.Action OnRuntimeReset;

        // ─── 保留（数据缓存）───

        /// <summary>叫分倍数（1/2/3）</summary>
        public static float BidMultiplier = 1f;

        /// <summary>是否有有效的叫分结果</summary>
        public static bool HasResult;

        /// <summary>是否为联机模式</summary>
        public static bool IsNetworkMode;

        /// <summary>联机模式下共享的牌组 RNG 种子</summary>
        public static int NetworkSeed;

        /// <summary>地主的 slot（由叫分结果确定）</summary>
        public static int LandlordSlot = -1;

        /// <summary>联机模式下 AI 槽位集合</summary>
        public static HashSet<int> AISlots = new HashSet<int>();

        /// <summary>大厅阶段的原始 AI 槽位</summary>
        public static HashSet<int> RawAISlots = new HashSet<int>();

        // ─── 废弃（Phase 5 不再使用）───

        [System.Obsolete("Phase 5 废弃：身份由 FusionGameManager.OnPlayerJoined 分配")]
        public static int LocalPlayerId;

        [System.Obsolete("Phase 5 废弃：slot 推导由 WorldState 替代")]
        public static int[] PlayerBaseMapping;

        [System.Obsolete("Phase 5 废弃：slot 推导由 WorldState 替代")]
        public static Dictionary<int, int> PlayerSlotMap = new();

        [System.Obsolete("Phase 5 废弃：身份由 FusionGameManager.OnPlayerJoined 分配")]
        public static int LocalActorNumber;

        [System.Obsolete("Phase 5 废弃：slot 推导由 WorldState 替代")]
        public static int LandlordPlayerId = -1;

        [System.Obsolete("Phase 5 废弃：由 WorldState 替代")]
        public static bool SlotReady;

        [System.Obsolete("Phase 5 废弃：由 WorldState 替代")]
        public static event System.Action OnSlotReady;

        // ─── 便捷属性（保留，但内部不再依赖推导）───

        [System.Obsolete("Phase 5 废弃：由 WorldState 替代")]
        public static int MyBaseIndex
        {
            get
            {
                if (PlayerBaseMapping != null && LocalPlayerId >= 0 && LocalPlayerId < PlayerBaseMapping.Length)
                    return PlayerBaseMapping[LocalPlayerId];
                return 0;
            }
        }

        public static bool PlayerIsLandlord => _localPlayerIsLandlord;
        private static bool _localPlayerIsLandlord;

        // ─── 方法 ───

        public static void MarkSlotReady()
        {
#pragma warning disable CS0618 // Phase 5 废弃字段，保留兼容
            if (SlotReady) return;
            SlotReady = true;
#pragma warning restore CS0618
            OnSlotReady?.Invoke();
        }

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

            // 废弃字段也重置（兼容旧代码）
#pragma warning disable CS0618
            LocalPlayerId = 0;
            PlayerBaseMapping = null;
            PlayerSlotMap = new Dictionary<int, int>();
#pragma warning restore CS0618
            LocalActorNumber = -1;
            LandlordPlayerId = -1;
            SlotReady = false;

            OnRuntimeReset?.Invoke();
        }

        /// <summary>
        /// 写入叫分结果（单机模式）。
        /// 保留原始逻辑：地主分配到地主基地，农民随机分配到农民基地。
        /// </summary>
        public static void SetResult(bool localIsLandlord, float multiplier, int landlordBaseIndex, int[] farmerBaseIndices)
        {
            _localPlayerIsLandlord = localIsLandlord;
            BidMultiplier = multiplier;
            HasResult = true;
            IsNetworkMode = false;
            LandlordSlot = 0;

            LocalPlayerId = 0;
            PlayerBaseMapping = new int[3];

            if (localIsLandlord)
            {
                PlayerBaseMapping[0] = landlordBaseIndex;
                ShuffleAndAssign(farmerBaseIndices, 1);
            }
            else
            {
                int playerFarmerIdx = Random.Range(0, farmerBaseIndices.Length);
                PlayerBaseMapping[0] = farmerBaseIndices[playerFarmerIdx];

                int aiSlot = 1;
                PlayerBaseMapping[aiSlot++] = landlordBaseIndex;
                for (int i = 0; i < farmerBaseIndices.Length; i++)
                {
                    if (i != playerFarmerIdx)
                        PlayerBaseMapping[aiSlot++] = farmerBaseIndices[i];
                }
            }
        }

        /// <summary>
        /// 写入叫分结果（联机模式）。
        /// </summary>
        public static void SetResultNetwork(int landlordSlot, float multiplier)
        {
            LandlordSlot = landlordSlot;
            BidMultiplier = multiplier;
            HasResult = true;
            IsNetworkMode = true;
            _localPlayerIsLandlord = false; // 由调用方设置

            // 兼容旧代码
            LocalPlayerId = 0;
        }

        /// <summary>设置本机是否地主</summary>
        public static void SetLocalPlayerIsLandlord(bool isLandlord)
        {
            _localPlayerIsLandlord = isLandlord;
        }

        /// <summary>判断指定槽位是否为 AI</summary>
        public static bool IsAISlot(int slot) => AISlots.Contains(slot);

        /// <summary>将 farmerBaseIndices 随机打乱后从指定位置开始填充</summary>
        private static void ShuffleAndAssign(int[] indices, int startSlot)
        {
            int[] shuffled = (int[])indices.Clone();
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            for (int i = 0; i < shuffled.Length && startSlot + i < 3; i++)
                PlayerBaseMapping[startSlot + i] = shuffled[i];
        }
    }
}

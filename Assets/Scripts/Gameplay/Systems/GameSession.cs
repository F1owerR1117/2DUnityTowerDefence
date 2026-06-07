using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 跨场景游戏会话数据（纯静态，无 MonoBehaviour）。
    /// 存储叫分期的结果，供游戏场景读取。
    ///
    /// 支持单机模式（1 玩家 + 2 AI）和联机模式（3 玩家）。
    /// 联机改造时，只需扩展玩家 ID 分配逻辑，基地映射结构不变。
    /// </summary>
    public static class GameSession
    {
        /// <summary>叫分倍数（1/2/3）</summary>
        public static float BidMultiplier = 1f;

        /// <summary>是否有有效的叫分结果（false = 跳过叫分直接启动）</summary>
        public static bool HasResult;

        /// <summary>本机玩家在 3 人中的 ID（0/1/2）。单机模式固定为 0。</summary>
        public static int LocalPlayerId;

        /// <summary>是否为联机模式</summary>
        public static bool IsNetworkMode;

        /// <summary>联机模式下共享的牌组 RNG 种子，保证所有客户端抽牌一致</summary>
        public static int NetworkSeed;

        /// <summary>联机模式下 AI 槽位集合（slot index → 是否为 AI）</summary>
        public static HashSet<int> AISlots = new HashSet<int>();

        /// <summary>判断指定槽位是否为 AI</summary>
        public static bool IsAISlot(int slot) => AISlots.Contains(slot);

        /// <summary>
        /// 玩家 ID → 基地索引映射（长度 3）。
        /// PlayerBaseMapping[playerId] = baseBuildings 数组索引。
        ///
        /// 示例：[2, 0, 1]
        ///   → 玩家0操控 baseBuildings[2]（LandLord）
        ///   → 玩家1操控 baseBuildings[0]（FarmerA）
        ///   → 玩家2操控 baseBuildings[1]（FarmerB）
        /// </summary>
        public static int[] PlayerBaseMapping;

        // ─── 便捷属性 ───

        /// <summary>本机玩家操控的基地索引</summary>
        public static int MyBaseIndex
        {
            get
            {
                if (PlayerBaseMapping != null && LocalPlayerId >= 0 && LocalPlayerId < PlayerBaseMapping.Length)
                    return PlayerBaseMapping[LocalPlayerId];
                return 0;
            }
        }

        /// <summary>本机玩家是否是地主</summary>
        public static bool PlayerIsLandlord
        {
            get
            {
                if (PlayerBaseMapping == null) return false;
                // 地主的基地索引是 PlayerBaseMapping 中唯一的那个
                // 通过 GameBootstrapper 传入地主基地索引来判断
                return _localPlayerIsLandlord;
            }
        }

        // 内部缓存，由 SetResult 设置
        private static bool _localPlayerIsLandlord;

        // ─── 方法 ───

        /// <summary>重置会话数据（每次新游戏前调用）</summary>
        public static void Reset()
        {
            BidMultiplier = 1f;
            HasResult = false;
            LocalPlayerId = 0;
            PlayerBaseMapping = null;
            _localPlayerIsLandlord = false;
            IsNetworkMode = false;
            NetworkSeed = 0;
            AISlots = new HashSet<int>();
        }

        /// <summary>
        /// 写入叫分结果（单机模式）。
        /// 自动构建基地映射：地主分配到地主基地，农民随机分配到农民基地。
        /// </summary>
        /// <param name="localIsLandlord">本机玩家是否是地主</param>
        /// <param name="multiplier">叫分倍数</param>
        /// <param name="landlordBaseIndex">地主基地在 baseBuildings 中的索引</param>
        /// <param name="farmerBaseIndices">农民基地在 baseBuildings 中的索引数组</param>
        public static void SetResult(bool localIsLandlord, float multiplier, int landlordBaseIndex, int[] farmerBaseIndices)
        {
            _localPlayerIsLandlord = localIsLandlord;
            BidMultiplier = multiplier;
            LocalPlayerId = 0;
            HasResult = true;

            // 构建 3 人映射：[玩家0, AI1, AI2]
            PlayerBaseMapping = new int[3];

            if (localIsLandlord)
            {
                // 玩家是地主 → 玩家操控地主基地，AI 操控农民基地
                PlayerBaseMapping[0] = landlordBaseIndex;
                ShuffleAndAssign(farmerBaseIndices, 1);
            }
            else
            {
                // 玩家是农民 → 随机分配农民基地，AI 操控地主基地 + 剩余农民基地
                int playerFarmerIdx = Random.Range(0, farmerBaseIndices.Length);
                PlayerBaseMapping[0] = farmerBaseIndices[playerFarmerIdx];

                // AI 分配：地主 + 剩余农民
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
        /// 由网络层调用，直接传入完整的基地映射。
        /// </summary>
        public static void SetResultNetwork(int localId, int[] baseMapping, float multiplier)
        {
            LocalPlayerId = localId;
            PlayerBaseMapping = baseMapping;
            BidMultiplier = multiplier;
            HasResult = true;

            // 判断本机玩家是否是地主（地主基地只有一个，且是 landlord 身份）
            // 需要通过 base index 对应的 CardUnit.IsLandlord 判断
            // 此处简化：联机模式下由调用方设置 _localPlayerIsLandlord
        }

        /// <summary>联机模式下设置本机是否地主</summary>
        public static void SetLocalPlayerIsLandlord(bool isLandlord)
        {
            _localPlayerIsLandlord = isLandlord;
        }

        // ─── 辅助 ───

        /// <summary>将 farmerBaseIndices 随机打乱后从 PlayerBaseMapping 的指定位置开始填充</summary>
        private static void ShuffleAndAssign(int[] indices, int startSlot)
        {
            // Fisher-Yates 洗牌
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

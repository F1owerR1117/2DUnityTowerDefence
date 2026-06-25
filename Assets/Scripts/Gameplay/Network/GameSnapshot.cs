using System;
using System.Collections.Generic;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// Event + Snapshot + Tick 三层确定性模型 v1.0
    /// Snapshot = 某一 Tick 下的完整权威状态，可完全重建游戏，不依赖 Event 历史。
    /// </summary>
    [Serializable]
    public class GameSnapshot
    {
        public int Tick;
        public int DeckId;
        public int Remaining;
        public string GamePhase;
        public int SharedPoolRemaining;

        public Dictionary<int, int[]> SlotHands;
        public Dictionary<int, float> SlotGold;
        public Dictionary<int, float> SlotIncomeRates;
        public Dictionary<int, float> UnitHPs;

        public float BidMultiplier;
        public int NetworkSeed;

        public GameSnapshot()
        {
            SlotHands = new Dictionary<int, int[]>();
            SlotGold = new Dictionary<int, float>();
            SlotIncomeRates = new Dictionary<int, float>();
            UnitHPs = new Dictionary<int, float>();
        }

        /// <summary>序列化为扁平数组</summary>
        public object[] Serialize()
        {
            var slotCount = SlotHands.Count;
            var slotIds = new int[slotCount];
            var slotHandsData = new int[slotCount][];
            var slotGoldData = new float[slotCount];
            var slotIncomeData = new float[slotCount];
            int i = 0;
            foreach (var kv in SlotHands)
            {
                slotIds[i] = kv.Key;
                slotHandsData[i] = kv.Value;
                slotGoldData[i] = SlotGold.TryGetValue(kv.Key, out var g) ? g : 0f;
                slotIncomeData[i] = SlotIncomeRates.TryGetValue(kv.Key, out var inc) ? inc : 0f;
                i++;
            }

            var unitCount = UnitHPs.Count;
            var unitIds = new int[unitCount];
            var unitHPData = new float[unitCount];
            int j = 0;
            foreach (var kv in UnitHPs)
            {
                unitIds[j] = kv.Key;
                unitHPData[j] = kv.Value;
                j++;
            }

            return new object[]
            {
                Tick,
                DeckId,
                Remaining,
                GamePhase ?? "Playing",
                SharedPoolRemaining,
                NetworkSeed,
                BidMultiplier,
                slotIds,
                slotHandsData,
                slotGoldData,
                slotIncomeData,
                unitIds,
                unitHPData
            };
        }

        /// <summary>反序列化</summary>
        public static GameSnapshot Deserialize(object[] data)
        {
            if (data == null || data.Length < 13)
                return null;

            var snap = new GameSnapshot
            {
                Tick = NetworkProtocol.SafeInt(data[0]),
                DeckId = NetworkProtocol.SafeInt(data[1]),
                Remaining = NetworkProtocol.SafeInt(data[2]),
                GamePhase = data[3] as string ?? "Playing",
                SharedPoolRemaining = NetworkProtocol.SafeInt(data[4]),
                NetworkSeed = NetworkProtocol.SafeInt(data[5]),
                BidMultiplier = NetworkProtocol.SafeFloat(data[6])
            };

            var slotIds = data[7] as int[] ?? new int[0];
            var slotHandsData = data[8] as int[][] ?? new int[0][];
            var slotGoldData = data[9] as float[] ?? new float[0];
            var slotIncomeData = data[10] as float[] ?? new float[0];

            for (int i = 0; i < slotIds.Length; i++)
            {
                snap.SlotHands[slotIds[i]] = i < slotHandsData.Length ? slotHandsData[i] : new int[0];
                snap.SlotGold[slotIds[i]] = i < slotGoldData.Length ? slotGoldData[i] : 0f;
                snap.SlotIncomeRates[slotIds[i]] = i < slotIncomeData.Length ? slotIncomeData[i] : 0f;
            }

            var unitIds = data[11] as int[] ?? new int[0];
            var unitHPData = data[12] as float[] ?? new float[0];
            for (int k = 0; k < unitIds.Length; k++)
            {
                snap.UnitHPs[unitIds[k]] = k < unitHPData.Length ? unitHPData[k] : 0f;
            }

            return snap;
        }
    }
}

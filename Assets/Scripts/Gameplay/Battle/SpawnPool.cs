using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Entities;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 出兵池。挂到基地/建筑上，定义该建筑能出什么兵。
    /// BattleManager 从此处查预制体，不再使用全局 rankPrefabs。
    /// 每列按 Rank 3~2 顺序排列（共 13 槽）。
    /// 未填槽位自动回退到 _rankPrefabs。
    /// </summary>
    public class SpawnPool : MonoBehaviour
    {
        [Header("基础兵种（按 Rank 3~2 顺序，13 槽）")]
        [SerializeField] private CardUnit[] _rankPrefabs = new CardUnit[13];

        [Header("牌型预制体（为空则回退 _rankPrefabs[rank]）")]

        [Tooltip("三带一：三张组出基础兵，单张 kicker 出诱饵")]
        [SerializeField] private CardUnit[] _baitPrefabs = new CardUnit[13];

        [Tooltip("三带对：三张组出基础兵，对子 kicker 出骑兵")]
        [SerializeField] private CardUnit[] _cavalryPrefabs = new CardUnit[13];

        [Tooltip("连对：整组出一个连对单位")]
        [SerializeField] private CardUnit[] _consecutivePairPrefabs = new CardUnit[13];

        [Tooltip("炸弹：四张同点出一个炸弹单位")]
        [SerializeField] private CardUnit[] _bombPrefabs = new CardUnit[13];

        [Tooltip("四带二：四张组出坦克（主体）")]
        [SerializeField] private CardUnit[] _tankPrefabs = new CardUnit[13];

        [Tooltip("四带二：两张 kicker 出无人机（挂件）")]
        [SerializeField] private CardUnit[] _dronePrefabs = new CardUnit[13];

        [Tooltip("飞机：整组出一个轰炸机单位")]
        [SerializeField] private CardUnit[] _bomberPrefabs = new CardUnit[13];

        [Header("出兵点（为空则用 Transform.position）")]
        [SerializeField] private Transform _spawnPoint;
        public Transform SpawnPoint => _spawnPoint;

        private static int RankToIndex(CardRank rank)
        {
            return rank switch
            {
                CardRank.Three => 0,  CardRank.Four => 1,  CardRank.Five => 2,
                CardRank.Six => 3,    CardRank.Seven => 4, CardRank.Eight => 5,
                CardRank.Nine => 6,   CardRank.Ten => 7,   CardRank.Jack => 8,
                CardRank.Queen => 9,  CardRank.King => 10, CardRank.Ace => 11,
                CardRank.Two => 12,
                _ => -1
            };
        }

        private static CardUnit GetFromArray(CardUnit[] arr, int idx)
        {
            return arr != null && idx >= 0 && idx < arr.Length ? arr[idx] : null;
        }

        /// <summary>按牌值查基础预制体</summary>
        public CardUnit GetPrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_rankPrefabs, idx);
        }

        /// <summary>按牌值查诱饵预制体</summary>
        public CardUnit GetBaitPrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_baitPrefabs, idx) ?? GetFromArray(_rankPrefabs, idx);
        }

        /// <summary>按牌值查骑兵预制体</summary>
        public CardUnit GetCavalryPrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_cavalryPrefabs, idx) ?? GetFromArray(_rankPrefabs, idx);
        }

        /// <summary>按牌值查连对预制体</summary>
        public CardUnit GetConsecutivePairPrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_consecutivePairPrefabs, idx) ?? GetFromArray(_rankPrefabs, idx);
        }

        /// <summary>按牌值查炸弹预制体</summary>
        public CardUnit GetBombPrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_bombPrefabs, idx) ?? GetFromArray(_rankPrefabs, idx);
        }

        /// <summary>按牌值查坦克预制体（四带二主体）</summary>
        public CardUnit GetTankPrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_tankPrefabs, idx) ?? GetFromArray(_rankPrefabs, idx);
        }

        /// <summary>按牌值查无人机挂件预制体（四带二挂件）</summary>
        public CardUnit GetDronePrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_dronePrefabs, idx) ?? GetFromArray(_rankPrefabs, idx);
        }

        /// <summary>按牌值查轰炸机预制体（飞机）</summary>
        public CardUnit GetBomberPrefab(CardRank rank)
        {
            int idx = RankToIndex(rank);
            return GetFromArray(_bomberPrefabs, idx) ?? GetFromArray(_rankPrefabs, idx);
        }
    }
}

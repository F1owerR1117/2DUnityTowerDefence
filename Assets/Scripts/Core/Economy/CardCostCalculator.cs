using System;
using System.Collections.Generic;
using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.Core.Economy
{
    /// <summary>
    /// 卡牌费用计算器（纯 C# 静态类）。
    /// 公式：C_n = 10 × 1.17^(n-3)，TotalCost = ΣC_n × M_type（§2.3）
    /// </summary>
    public static class CardCostCalculator
    {
        // 增长系数
        private const float BaseCostC3 = 10f;
        private const float GrowthRate = 1.17f;

        /// <summary>
        /// 单牌基础市值 C_n = 10 × 1.17^(n-3)
        /// </summary>
        /// <param name="rank">点数（2→16，王→16）</param>
        public static float BaseCost(CardRank rank)
        {
            int n = rank.ToCostIndex();
            return (float)(BaseCostC3 * Math.Pow(GrowthRate, n - 3));
        }

        /// <summary>
        /// 牌型结构系数 M_type（§2.3 官方定义表）
        /// </summary>
        public static float GetTypeCoefficient(CardType type)
        {
            return type switch
            {
                CardType.Single => 1.0f,
                CardType.Pair => 1.0f,
                CardType.Triple => 1.0f,
                CardType.Straight => 0.7f,         // 顺子 7 折
                CardType.Straight6Plus => 0.7f,     // 顺子6+ 同 7 折
                CardType.TripleWithOne => 0.85f,    // 三带一 85 折
                CardType.TripleWithPair => 0.85f,   // 三带二 85 折
                CardType.ConsecutivePair => 0.7f,   // 连对 7 折
                CardType.FourWithTwo => 0.8f,       // 四带二 8 折
                CardType.Plane => 0.8f,             // 飞机 8 折
                CardType.Bomb => 1.2f,              // 炸弹 20% 溢价
                CardType.DoubleKingBomb => 1.0f,    // 王炸无溢价
                _ => 1.0f
            };
        }

        /// <summary>
        /// 计算打出指定牌型的总费用
        /// TotalCost = (所有单牌市值累加) × M_type
        /// </summary>
        /// <param name="cards">所选的全部卡牌</param>
        /// <param name="result">牌型检测结果</param>
        public static float CalculateTotalCost(Card[] cards, CardTypeResult result)
        {
            if (!result.IsValid)
                throw new ArgumentException("无法计算无效牌型的费用", nameof(result));

            float sum = 0f;
            foreach (var card in cards)
            {
                sum += BaseCost(card.Rank);
            }

            float coefficient = GetTypeCoefficient(result.Type);
            return sum * coefficient;
        }

        /// <summary>
        /// 获取全点数基础市值表（用于调试 UI）
        /// </summary>
        public static Dictionary<CardRank, float> GetCostTable()
        {
            var table = new Dictionary<CardRank, float>();
            var ranks = new[]
            {
                CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six,
                CardRank.Seven, CardRank.Eight, CardRank.Nine, CardRank.Ten,
                CardRank.Jack, CardRank.Queen, CardRank.King, CardRank.Ace,
                CardRank.Two, CardRank.Joker
            };

            foreach (var rank in ranks)
            {
                table[rank] = BaseCost(rank);
            }

            return table;
        }
    }
}

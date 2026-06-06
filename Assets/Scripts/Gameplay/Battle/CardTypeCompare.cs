using System.Collections.Generic;
using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 牌型比较工具（纯静态，无状态）。
    /// 判断手牌中是否存在能管上目标牌型的组合，以及两个牌型之间的大小关系。
    ///
    /// 从 DomainSystem.HasCounterInHand / CanCounter 提取，便于独立测试和维护。
    /// </summary>
    public static class CardTypeCompare
    {
        /// <summary>
        /// 判断手牌中是否有能管上目标牌型的组合。
        /// 遍历手牌，检查是否存在能管上的牌型组合。
        /// </summary>
        /// <param name="allCards">所有手牌</param>
        /// <param name="target">目标牌型</param>
        /// <returns>手牌中是否存在能管上的牌型</returns>
        public static bool HasCounterInHand(List<Card> allCards, CardTypeResult target)
        {
            // 检查王炸（两张 Joker）
            bool hasJoker1 = false, hasJoker2 = false;
            foreach (var card in allCards)
            {
                if (card.IsJoker)
                {
                    if (!hasJoker1) hasJoker1 = true;
                    else if (!hasJoker2) hasJoker2 = true;
                }
            }
            if (hasJoker1 && hasJoker2) return true;

            // 检查炸弹（4张同点数）- 可以管非炸弹、非王炸牌型
            if (target.Type != CardType.Bomb && target.Type != CardType.DoubleKingBomb)
            {
                var rankCounts = new Dictionary<CardRank, int>();
                foreach (var card in allCards)
                {
                    if (!rankCounts.ContainsKey(card.Rank))
                        rankCounts[card.Rank] = 0;
                    rankCounts[card.Rank]++;
                }
                foreach (var kvp in rankCounts)
                {
                    if (kvp.Value >= 4) return true;
                }
            }

            // 检查同牌型更大点数
            switch (target.Type)
            {
                case CardType.Single:
                    foreach (var card in allCards)
                    {
                        if (card.Rank > target.MainRank) return true;
                    }
                    break;

                case CardType.Pair:
                    var pairCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!pairCounts.ContainsKey(card.Rank))
                            pairCounts[card.Rank] = 0;
                        pairCounts[card.Rank]++;
                    }
                    foreach (var kvp in pairCounts)
                    {
                        if (kvp.Value >= 2 && kvp.Key > target.MainRank) return true;
                    }
                    break;

                case CardType.Triple:
                    var tripleCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!tripleCounts.ContainsKey(card.Rank))
                            tripleCounts[card.Rank] = 0;
                        tripleCounts[card.Rank]++;
                    }
                    foreach (var kvp in tripleCounts)
                    {
                        if (kvp.Value >= 3 && kvp.Key > target.MainRank) return true;
                    }
                    break;

                case CardType.TripleWithOne:
                    var ttoCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!ttoCounts.ContainsKey(card.Rank))
                            ttoCounts[card.Rank] = 0;
                        ttoCounts[card.Rank]++;
                    }
                    foreach (var kvp in ttoCounts)
                    {
                        if (kvp.Value >= 3 && kvp.Key > target.MainRank && allCards.Count >= 4) return true;
                    }
                    break;

                case CardType.TripleWithPair:
                    var twpCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!twpCounts.ContainsKey(card.Rank))
                            twpCounts[card.Rank] = 0;
                        twpCounts[card.Rank]++;
                    }
                    bool hasBiggerTriple = false;
                    foreach (var kvp in twpCounts)
                    {
                        if (kvp.Value >= 3 && kvp.Key > target.MainRank)
                        {
                            hasBiggerTriple = true;
                            break;
                        }
                    }
                    if (hasBiggerTriple)
                    {
                        foreach (var kvp in twpCounts)
                        {
                            if (kvp.Value >= 2) return true;
                        }
                    }
                    break;

                case CardType.FourWithTwo:
                    var fwtCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!fwtCounts.ContainsKey(card.Rank))
                            fwtCounts[card.Rank] = 0;
                        fwtCounts[card.Rank]++;
                    }
                    foreach (var kvp in fwtCounts)
                    {
                        if (kvp.Value >= 4 && kvp.Key > target.MainRank)
                        {
                            int kickerCount = allCards.Count - 4;
                            if (kickerCount >= 2) return true;
                        }
                    }
                    break;

                case CardType.Plane:
                {
                    var planeCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!planeCounts.ContainsKey(card.Rank))
                            planeCounts[card.Rank] = 0;
                        planeCounts[card.Rank]++;
                    }
                    int planeLen = target.Length;
                    int planeLowestRank = (int)target.MainRank - planeLen + 1;
                    var planeTripleRanks = new List<int>();
                    foreach (var kvp in planeCounts)
                    {
                        if (kvp.Value >= 3)
                            planeTripleRanks.Add((int)kvp.Key);
                    }
                    planeTripleRanks.Sort();
                    for (int startRank = planeLowestRank + 1; startRank <= 14 - planeLen + 1; startRank++)
                    {
                        bool canForm = true;
                        for (int i = 0; i < planeLen; i++)
                        {
                            if (!planeTripleRanks.Contains(startRank + i))
                            {
                                canForm = false;
                                break;
                            }
                        }
                        if (canForm) return true;
                    }
                    break;
                }

                case CardType.ConsecutivePair:
                {
                    var cpCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!cpCounts.ContainsKey(card.Rank))
                            cpCounts[card.Rank] = 0;
                        cpCounts[card.Rank]++;
                    }
                    int cpLen = target.Length;
                    int cpLowestRank = (int)target.MainRank - cpLen + 1;
                    var pairRanks = new List<int>();
                    foreach (var kvp in cpCounts)
                    {
                        if (kvp.Value >= 2)
                            pairRanks.Add((int)kvp.Key);
                    }
                    pairRanks.Sort();
                    for (int startRank = cpLowestRank + 1; startRank <= 14 - cpLen + 1; startRank++)
                    {
                        bool canForm = true;
                        for (int i = 0; i < cpLen; i++)
                        {
                            if (!pairRanks.Contains(startRank + i))
                            {
                                canForm = false;
                                break;
                            }
                        }
                        if (canForm) return true;
                    }
                    break;
                }

                case CardType.Bomb:
                    var bombCounts = new Dictionary<CardRank, int>();
                    foreach (var card in allCards)
                    {
                        if (!bombCounts.ContainsKey(card.Rank))
                            bombCounts[card.Rank] = 0;
                        bombCounts[card.Rank]++;
                    }
                    foreach (var kvp in bombCounts)
                    {
                        if (kvp.Value >= 4 && kvp.Key > target.MainRank) return true;
                    }
                    break;

                case CardType.DoubleKingBomb:
                    return false;

                default:
                    foreach (var card in allCards)
                    {
                        if (card.Rank > target.MainRank) return true;
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// 判断农民牌型是否能管上地主牌型。
        /// </summary>
        /// <param name="domain">地主牌型</param>
        /// <param name="counter">农民牌型</param>
        /// <returns>是否能管上</returns>
        public static bool CanCounter(CardTypeResult domain, CardTypeResult counter)
        {
            // 王炸管一切
            if (counter.Type == CardType.DoubleKingBomb)
                return true;

            // 炸弹管非炸弹
            if (counter.Type == CardType.Bomb && domain.Type != CardType.Bomb && domain.Type != CardType.DoubleKingBomb)
                return true;

            // 同牌型比大小
            if (counter.Type == domain.Type && counter.MainRank > domain.MainRank)
                return true;

            return false;
        }
    }
}

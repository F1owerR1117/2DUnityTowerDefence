using System.Collections.Generic;
using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 封印规则引擎（纯静态，无状态）。
    /// 根据当前牌型判断手牌中哪些牌可以管上，哪些应被封印。
    ///
    /// 从 DomainSystem.GetUnsealedCards 提取，便于独立测试和维护。
    /// </summary>
    public static class SealRuleEngine
    {
        /// <summary>
        /// 获取未被封印的牌列表。
        /// 封印规则：只有能管上当前牌型的牌才不被封印。
        ///
        /// 能管上的牌型：
        /// 1. 王炸（两张 Joker）：可以管上任何牌型
        /// 2. 炸弹（四张同点数）：可以管上非炸弹牌型，或更大的炸弹
        /// 3. 同牌型更大的牌：如更大的对子可以管上对子
        ///
        /// 不能管上的牌型（全部封印）：
        /// - 单张不能管上对子/三条/顺子等
        /// - 对子不能管上单张/三条/顺子等
        /// - 三条不能管上单张/对子/顺子等
        /// </summary>
        public static HashSet<Card> GetUnsealedCards(List<Card> allCards, CardTypeResult sealType)
        {
            var unsealed = new HashSet<Card>();

            // 统计各点数的牌数量
            var rankCounts = new Dictionary<CardRank, List<Card>>();
            foreach (var card in allCards)
            {
                if (!rankCounts.ContainsKey(card.Rank))
                    rankCounts[card.Rank] = new List<Card>();
                rankCounts[card.Rank].Add(card);
            }

            // 1. 王炸可以管上任何牌型
            Card joker1 = default, joker2 = default;
            bool foundJoker = false;
            foreach (var card in allCards)
            {
                if (card.IsJoker)
                {
                    if (!foundJoker)
                    {
                        joker1 = card;
                        foundJoker = true;
                    }
                    else
                    {
                        joker2 = card;
                        break;
                    }
                }
            }
            if (foundJoker && !joker2.Equals(default(Card)))
            {
                unsealed.Add(joker1);
                unsealed.Add(joker2);
            }

            // 2. 炸弹可以管上非炸弹牌型，或更大的炸弹
            foreach (var kvp in rankCounts)
            {
                if (kvp.Value.Count >= 4)
                {
                    // 炸弹可以管上任何非炸弹牌型
                    if (sealType.Type != CardType.Bomb && sealType.Type != CardType.DoubleKingBomb)
                    {
                        foreach (var card in kvp.Value)
                            unsealed.Add(card);
                    }
                    // 炸弹可以管上更小的炸弹
                    else if (sealType.Type == CardType.Bomb && kvp.Key > sealType.MainRank)
                    {
                        foreach (var card in kvp.Value)
                            unsealed.Add(card);
                    }
                }
            }

            // 3. 同牌型更大的牌可以管上
            switch (sealType.Type)
            {
                case CardType.Single:
                    // 单张：更大的单张可以管上
                    foreach (var card in allCards)
                    {
                        if (card.Rank > sealType.MainRank)
                            unsealed.Add(card);
                    }
                    break;

                case CardType.Pair:
                    // 对子：更大的对子可以管上
                    foreach (var kvp in rankCounts)
                    {
                        if (kvp.Value.Count >= 2 && kvp.Key > sealType.MainRank)
                        {
                            foreach (var card in kvp.Value)
                                unsealed.Add(card);
                        }
                    }
                    break;

                case CardType.Triple:
                    // 三条：更大的三条可以管上
                    foreach (var kvp in rankCounts)
                    {
                        if (kvp.Value.Count >= 3 && kvp.Key > sealType.MainRank)
                        {
                            foreach (var card in kvp.Value)
                                unsealed.Add(card);
                        }
                    }
                    break;

                case CardType.Straight:
                case CardType.Straight6Plus:
                {
                    // 顺子：更大的同长度顺子可以管上
                    int straightLen = sealType.Length;
                    int lowestRank = (int)sealType.MainRank - straightLen + 1;

                    var availableRanks = new Dictionary<int, List<Card>>();
                    foreach (var card in allCards)
                    {
                        if (card.IsJoker) continue;
                        int r = (int)card.Rank;
                        if (!availableRanks.ContainsKey(r))
                            availableRanks[r] = new List<Card>();
                        availableRanks[r].Add(card);
                    }

                    var straightUnsealed = new HashSet<Card>();
                    for (int startRank = lowestRank + 1; startRank <= 14 - straightLen + 1; startRank++)
                    {
                        bool canForm = true;
                        for (int i = 0; i < straightLen; i++)
                        {
                            if (!availableRanks.ContainsKey(startRank + i))
                            {
                                canForm = false;
                                break;
                            }
                        }
                        if (canForm)
                        {
                            for (int i = 0; i < straightLen; i++)
                            {
                                foreach (var c in availableRanks[startRank + i])
                                    straightUnsealed.Add(c);
                            }
                        }
                    }

                    foreach (var card in straightUnsealed)
                        unsealed.Add(card);
                    break;
                }

                case CardType.ConsecutivePair:
                {
                    // 连对：更大的同长度连对可以管上
                    int pairLen = sealType.Length;
                    int lowestRank = (int)sealType.MainRank - pairLen + 1;

                    var pairRanks = new Dictionary<int, List<Card>>();
                    foreach (var kvp in rankCounts)
                    {
                        if (kvp.Value.Count >= 2)
                            pairRanks[(int)kvp.Key] = kvp.Value;
                    }

                    var cpUnsealed = new HashSet<Card>();
                    for (int startRank = lowestRank + 1; startRank <= 14 - pairLen + 1; startRank++)
                    {
                        bool canForm = true;
                        for (int i = 0; i < pairLen; i++)
                        {
                            if (!pairRanks.ContainsKey(startRank + i))
                            {
                                canForm = false;
                                break;
                            }
                        }
                        if (canForm)
                        {
                            for (int i = 0; i < pairLen; i++)
                            {
                                foreach (var c in pairRanks[startRank + i])
                                    cpUnsealed.Add(c);
                            }
                        }
                    }

                    foreach (var card in cpUnsealed)
                        unsealed.Add(card);
                    break;
                }

                case CardType.TripleWithOne:
                {
                    // 三带一：更大的三带一可以管上（需要更大的三条 + 至少一张单牌）
                    CardRank? higherTriple = null;
                    foreach (var kvp in rankCounts)
                    {
                        if (kvp.Value.Count >= 3 && kvp.Key > sealType.MainRank)
                        {
                            higherTriple = kvp.Key;
                            foreach (var card in kvp.Value)
                                unsealed.Add(card);
                        }
                    }
                    // 只有存在更大三条且有可用单牌时，单牌才应被解封
                    if (higherTriple.HasValue)
                    {
                        bool hasKicker = false;
                        foreach (var kvp in rankCounts)
                        {
                            if (kvp.Value.Count == 1) { hasKicker = true; break; }
                        }
                        if (hasKicker)
                        {
                            foreach (var kvp in rankCounts)
                            {
                                if (kvp.Value.Count == 1)
                                    foreach (var card in kvp.Value)
                                        unsealed.Add(card);
                            }
                        }
                    }
                    break;
                }

                case CardType.TripleWithPair:
                {
                    // 三带二：更大的三带二可以管上（需要更大的三条 + 至少一对）
                    CardRank? higherTriple = null;
                    foreach (var kvp in rankCounts)
                    {
                        if (kvp.Value.Count >= 3 && kvp.Key > sealType.MainRank)
                        {
                            higherTriple = kvp.Key;
                            foreach (var card in kvp.Value)
                                unsealed.Add(card);
                        }
                    }
                    // 只有存在更大三条且有可用对子时，对子才应被解封
                    if (higherTriple.HasValue)
                    {
                        bool hasPairKicker = false;
                        foreach (var kvp in rankCounts)
                        {
                            if (kvp.Value.Count >= 2) { hasPairKicker = true; break; }
                        }
                        if (hasPairKicker)
                        {
                            foreach (var kvp in rankCounts)
                            {
                                if (kvp.Value.Count >= 2)
                                    foreach (var card in kvp.Value)
                                        unsealed.Add(card);
                            }
                        }
                    }
                    break;
                }

                case CardType.Bomb:
                    // 炸弹：更大的炸弹可以管上（已在上面处理）
                    break;

                case CardType.DoubleKingBomb:
                    // 王炸：无法被管上（已在上面处理）
                    break;

                case CardType.FourWithTwo:
                {
                    // 四带二：更大的四张可以管上（四张已在上面炸弹逻辑中处理）
                    // 额外解封可用的带牌（非炸弹域时，确保有足够的带牌组成四带二）
                    if (sealType.Type != CardType.Bomb && sealType.Type != CardType.DoubleKingBomb)
                    {
                        bool hasHigherFour = false;
                        foreach (var kvp in rankCounts)
                        {
                            if (kvp.Value.Count >= 4 && kvp.Key > sealType.MainRank)
                            {
                                hasHigherFour = true;
                                break;
                            }
                        }
                        if (hasHigherFour)
                        {
                            foreach (var kvp in rankCounts)
                            {
                                if (kvp.Value.Count < 4)
                                {
                                    foreach (var card in kvp.Value)
                                        unsealed.Add(card);
                                }
                            }
                        }
                    }
                    break;
                }

                case CardType.Plane:
                {
                    // 飞机：更大的同长度飞机可以管上
                    int planeLen = sealType.Length;
                    int lowestRank = (int)sealType.MainRank - planeLen + 1;

                    var tripleRanks = new Dictionary<int, List<Card>>();
                    foreach (var kvp in rankCounts)
                    {
                        if (kvp.Value.Count >= 3)
                            tripleRanks[(int)kvp.Key] = kvp.Value;
                    }

                    var planeUnsealed = new HashSet<Card>();
                    for (int startRank = lowestRank + 1; startRank <= 14 - planeLen + 1; startRank++)
                    {
                        bool canForm = true;
                        for (int i = 0; i < planeLen; i++)
                        {
                            if (!tripleRanks.ContainsKey(startRank + i))
                            {
                                canForm = false;
                                break;
                            }
                        }
                        if (canForm)
                        {
                            for (int i = 0; i < planeLen; i++)
                            {
                                foreach (var c in tripleRanks[startRank + i])
                                    planeUnsealed.Add(c);
                            }
                        }
                    }

                    foreach (var card in planeUnsealed)
                        unsealed.Add(card);
                    break;
                }
            }

            return unsealed;
        }
    }
}

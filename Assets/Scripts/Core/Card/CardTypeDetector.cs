using System;
using System.Collections.Generic;
using System.Linq;

namespace DoudizhuTower.Core.Cards
{
    public static class CardTypeDetector
    {
        public static CardTypeResult Detect(Card[] selected, int maxLimit)
        {
            if (selected == null || selected.Length == 0)
                return CardTypeResult.Invalid;
            if (selected.Length > maxLimit)
                return CardTypeResult.Invalid;
            if (HasDuplicates(selected))
                return CardTypeResult.Invalid;

            var sorted = selected.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToArray();
            var groups = GroupByRank(sorted);

            return sorted.Length switch
            {
                1 => DetectSingle(sorted, groups),
                2 => DetectPair(sorted, groups),
                3 => DetectTriple(sorted, groups),
                4 => Detect4Cards(sorted, groups),
                5 => Detect5Cards(sorted, groups),
                >= 6 => Detect6PlusCards(sorted, groups, maxLimit),
                _ => CardTypeResult.Invalid
            };
        }

        private static CardTypeResult DetectSingle(Card[] sorted, Dictionary<CardRank, int> groups)
        {
            // 单张 Joker 不再走献祭，直接作为 Single（英雄召唤由 BattleManager 处理）
            return new CardTypeResult { Type = CardType.Single, MainRank = sorted[0].Rank };
        }

        private static CardTypeResult DetectPair(Card[] sorted, Dictionary<CardRank, int> groups)
        {
            if (groups.Count == 1 && groups.First().Value == 2)
            {
                if (groups.First().Key == CardRank.Joker)
                    return new CardTypeResult { Type = CardType.DoubleKingBomb, MainRank = CardRank.Joker };
                return new CardTypeResult { Type = CardType.Pair, MainRank = groups.First().Key };
            }
            return CardTypeResult.Invalid;
        }

        private static CardTypeResult DetectTriple(Card[] sorted, Dictionary<CardRank, int> groups)
        {
            if (groups.Count == 1 && groups.First().Value == 3)
                return new CardTypeResult { Type = CardType.Triple, MainRank = groups.First().Key };
            return CardTypeResult.Invalid;
        }

        private static CardTypeResult Detect4Cards(Card[] sorted, Dictionary<CardRank, int> groups)
        {
            if (groups.Count == 1 && groups.First().Value == 4)
                return new CardTypeResult { Type = CardType.Bomb, MainRank = groups.First().Key };
            if (TryGetTriplePlusKickers(groups, out var tripleRank, out var kickers, out var pairKickers)
                && kickers.Length == 1 && pairKickers.Length == 0)
                return new CardTypeResult { Type = CardType.TripleWithOne, MainRank = tripleRank, KickerRanks = kickers };
            return CardTypeResult.Invalid;
        }

        private static CardTypeResult Detect5Cards(Card[] sorted, Dictionary<CardRank, int> groups)
        {
            if (TryGetTriplePlusKickers(groups, out var tripleRank, out var kickers, out var pairKickers)
                && kickers.Length == 0 && pairKickers.Length == 1)
                return new CardTypeResult { Type = CardType.TripleWithPair, MainRank = tripleRank, KickerRanks = new[] { pairKickers[0] } };

            var ranks = groups.Keys.OrderBy(r => r).ToArray();
            if (IsConsecutiveRanks(ranks, 5))
                return new CardTypeResult { Type = CardType.Straight, MainRank = ranks[^1], Length = 5 };

            return CardTypeResult.Invalid;
        }

        private static CardTypeResult Detect6PlusCards(Card[] sorted, Dictionary<CardRank, int> groups, int maxLimit)
        {
            if (IsConsecutivePairs(groups, sorted.Length, out var pairMainRank, out var pairLength))
                return new CardTypeResult { Type = CardType.ConsecutivePair, MainRank = pairMainRank, Length = pairLength };

            var ranks = groups.Keys.OrderBy(r => r).ToArray();
            if (IsConsecutiveRanks(ranks, sorted.Length))
                return new CardTypeResult { Type = CardType.Straight, MainRank = ranks[^1], Length = sorted.Length };

            // 四带二：一组四张 + 2 张单牌或 1 对
            if (TryGetFourWithTwo(groups, out var fwRank, out var fwKickers))
                return new CardTypeResult { Type = CardType.FourWithTwo, MainRank = fwRank, KickerRanks = fwKickers };

            // 飞机：至少 2 组连续三张，可带单牌（每张三带一单）或对子（每张三带一对）
            if (TryGetPlane(groups, sorted.Length, out var pRank, out var pKickers, out var pLength))
                return new CardTypeResult { Type = CardType.Plane, MainRank = pRank, KickerRanks = pKickers, Length = pLength };

            return CardTypeResult.Invalid;
        }

        private static Dictionary<CardRank, int> GroupByRank(Card[] cards)
        {
            var dict = new Dictionary<CardRank, int>();
            foreach (var card in cards)
            {
                dict.TryGetValue(card.Rank, out int count);
                dict[card.Rank] = count + 1;
            }
            return dict;
        }

        private static bool HasDuplicates(Card[] cards)
        {
            var set = new HashSet<Card>();
            foreach (var card in cards)
            {
                if (!set.Add(card)) return true;
            }
            return false;
        }

        private static bool TryGetTriplePlusKickers(
            Dictionary<CardRank, int> groups,
            out CardRank tripleRank,
            out CardRank[] singleKickers,
            out CardRank[] pairKickers)
        {
            tripleRank = default;
            singleKickers = Array.Empty<CardRank>();
            pairKickers = Array.Empty<CardRank>();
            CardRank? foundTriple = null;
            var singles = new List<CardRank>();
            var pairs = new List<CardRank>();

            foreach (var kvp in groups)
            {
                if (kvp.Value == 3) { if (foundTriple.HasValue) return false; foundTriple = kvp.Key; }
                else if (kvp.Value == 2) pairs.Add(kvp.Key);
                else if (kvp.Value == 1) singles.Add(kvp.Key);
                else return false;
            }
            if (!foundTriple.HasValue) return false;
            tripleRank = foundTriple.Value;
            singleKickers = singles.ToArray();
            pairKickers = pairs.ToArray();
            return true;
        }

        private static bool IsConsecutiveRanks(CardRank[] ranks, int minLength)
        {
            if (ranks.Length < minLength) return false;
            for (int i = 0; i < ranks.Length - 1; i++)
                if (!ranks[i].IsConsecutiveTo(ranks[i + 1])) return false;
            return true;
        }

        private static bool IsConsecutivePairs(Dictionary<CardRank, int> groups, int totalCards, out CardRank mainRank, out int length)
        {
            mainRank = default; length = 0;
            if (groups.Any(kvp => kvp.Value != 2)) return false;
            var ranks = groups.Keys.OrderBy(r => r).ToArray();
            if (ranks.Length < 3) return false;
            for (int i = 0; i < ranks.Length - 1; i++)
                if (!ranks[i].IsConsecutiveTo(ranks[i + 1])) return false;
            mainRank = ranks[^1]; length = ranks.Length;
            return true;
        }

        /// <summary>四带二检测：一组四张，其余牌凑足 2 张（2 个单张或 1 对）</summary>
        private static bool TryGetFourWithTwo(Dictionary<CardRank, int> groups, out CardRank mainRank, out CardRank[] kickers)
        {
            mainRank = default;
            kickers = Array.Empty<CardRank>();
            CardRank? foundFour = null;
            var kickerList = new List<CardRank>();

            foreach (var kvp in groups)
            {
                if (kvp.Value == 4)
                {
                    if (foundFour.HasValue) return false; // 只能有一组四张
                    foundFour = kvp.Key;
                }
                else if (kvp.Value == 2)
                {
                    kickerList.Add(kvp.Key);
                    kickerList.Add(kvp.Key); // 对子算 2 张
                }
                else if (kvp.Value == 1)
                {
                    kickerList.Add(kvp.Key);
                }
                else
                {
                    return false; // 不允许其他牌型参与
                }
            }

            if (!foundFour.HasValue) return false;
            if (kickerList.Count != 2) return false; // 必须恰好 2 张 kicker

            mainRank = foundFour.Value;
            kickers = kickerList.ToArray();
            return true;
        }

        /// <summary>飞机检测：至少 2 组连续三张，可带单牌或对子</summary>
        private static bool TryGetPlane(Dictionary<CardRank, int> groups, int totalCards, out CardRank mainRank, out CardRank[] kickers, out int length)
        {
            mainRank = default;
            kickers = Array.Empty<CardRank>();
            length = 0;

            var tripleRanks = new List<CardRank>();
            var singleKickers = new List<CardRank>();
            var pairKickers = new List<CardRank>();

            foreach (var kvp in groups)
            {
                if (kvp.Value == 3) tripleRanks.Add(kvp.Key);
                else if (kvp.Value == 2) pairKickers.Add(kvp.Key);
                else if (kvp.Value == 1) singleKickers.Add(kvp.Key);
                else return false; // 有 4 张以上相同点数的牌，不构成飞机
            }

            if (tripleRanks.Count < 2) return false;

            tripleRanks.Sort();

            // 所有三张组必须连续
            for (int i = 0; i < tripleRanks.Count - 1; i++)
                if (!tripleRanks[i].IsConsecutiveTo(tripleRanks[i + 1])) return false;

            int tripleCount = tripleRanks.Count;
            int kickerCardCount = singleKickers.Count + pairKickers.Count * 2;

            // 纯飞机：不带牌
            if (kickerCardCount == 0)
            {
                mainRank = tripleRanks[^1];
                length = tripleCount;
                return true;
            }

            // 飞机带单牌：每张三带一张单牌
            if (kickerCardCount == tripleCount && pairKickers.Count == 0)
            {
                mainRank = tripleRanks[^1];
                length = tripleCount;
                kickers = singleKickers.ToArray();
                return true;
            }

            // 飞机带对子：每张三带一对
            if (kickerCardCount == tripleCount * 2 && singleKickers.Count == 0)
            {
                mainRank = tripleRanks[^1];
                length = tripleCount;
                kickers = pairKickers.SelectMany(p => new[] { p, p }).ToArray();
                return true;
            }

            return false;
        }
    }
}

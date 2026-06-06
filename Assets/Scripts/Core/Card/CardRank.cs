using System;

namespace DoudizhuTower.Core.Cards
{
    public enum CardRank
    {
        Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7,
        Eight = 8, Nine = 9, Ten = 10, Jack = 11, Queen = 12,
        King = 13, Ace = 14, Two = 16,
        /// <summary>Joker 牌（共 2 张，完全相同）</summary>
        Joker = 17,
    }

    public static class CardRankExtensions
    {
        public static int ToCostIndex(this CardRank rank)
        {
            return rank == CardRank.Joker ? 16 : (int)rank;
        }

        public static string ToDisplayString(this CardRank rank)
        {
            return rank switch
            {
                CardRank.Joker => "JOKER",
                CardRank.Jack => "J", CardRank.Queen => "Q",
                CardRank.King => "K", CardRank.Ace => "A",
                CardRank.Two => "2",
                _ => ((int)rank).ToString()
            };
        }

        public static bool IsConsecutiveTo(this CardRank current, CardRank next)
        {
            if (current >= CardRank.Two || next >= CardRank.Two)
                return false;
            return (int)next - (int)current == 1;
        }
    }
}

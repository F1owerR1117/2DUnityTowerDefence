using System;

namespace DoudizhuTower.Core.Cards
{
    public struct Card : IEquatable<Card>, IComparable<Card>
    {
        public CardSuit Suit { get; }
        public CardRank Rank { get; }

        /// <summary>牌堆中的逻辑索引（区分同 Rank/Suit 的牌，如两张 Joker）</summary>
        private readonly int _index;

        /// <summary>网络可序列化的牌堆索引 (0-53)，用于跨客户端牌标识</summary>
        public int DeckIndex => _index;

        /// <summary>全局唯一实例 ID（确保每次创建的牌都是独立实体，即使 Reshuffle 后 _index 重复）</summary>
        private readonly int _instanceId;
        private static int _nextInstanceId;

        public bool IsJoker => Suit == CardSuit.None;

        public Card(CardSuit suit, CardRank rank, int index = 0)
        {
            Suit = suit;
            Rank = rank;
            _index = index;
            _instanceId = _nextInstanceId++;
        }

        public bool Equals(Card other) => _instanceId == other._instanceId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => _instanceId;
        public static bool operator ==(Card left, Card right) => left.Equals(right);
        public static bool operator !=(Card left, Card right) => !left.Equals(right);

        public int CompareTo(Card other)
        {
            int rankCmp = ((int)Rank).CompareTo((int)other.Rank);
            return rankCmp != 0 ? rankCmp : ((int)Suit).CompareTo((int)other.Suit);
        }

        public override string ToString()
        {
            if (IsJoker) return "🃏JOKER";
            string suitChar = Suit switch
            {
                CardSuit.Spade => "♠", CardSuit.Heart => "♥",
                CardSuit.Club => "♣", CardSuit.Diamond => "♦", _ => "?"
            };
            return $"{suitChar}{Rank.ToDisplayString()}";
        }
    }
}

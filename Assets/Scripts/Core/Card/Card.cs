using System;

namespace DoudizhuTower.Core.Cards
{
    public struct Card : IEquatable<Card>, IComparable<Card>
    {
        public CardSuit Suit { get; }
        public CardRank Rank { get; }

        /// <summary>牌在牌堆中的索引 (0-53)</summary>
        private readonly int _index;

        /// <summary>来源牌堆 ID，区分不同牌堆产生的同点数牌</summary>
        private readonly int _deckId;

        /// <summary>网络可序列化的牌堆索引 (0-53)，用于跨客户端牌标识</summary>
        public int DeckIndex => _index;

        /// <summary>来源牌堆 ID</summary>
        public int DeckId => _deckId;

        public bool IsJoker => Suit == CardSuit.None;

        public Card(CardSuit suit, CardRank rank, int index = 0, int deckId = 0)
        {
            Suit = suit;
            Rank = rank;
            _index = index;
            _deckId = deckId;
        }

        public bool Equals(Card other) => _deckId == other._deckId && _index == other._index;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_deckId, _index);
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

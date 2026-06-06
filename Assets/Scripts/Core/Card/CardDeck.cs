using System;
using System.Collections.Generic;

namespace DoudizhuTower.Core.Cards
{
    public class CardDeck
    {
        private readonly Card[] _cards;
        private int _cursor;
        private readonly System.Random _rng;
        private int _reshuffleCount;
        private readonly List<Card> _discardPile = new();
        private readonly Dictionary<CardRank, int> _drawnPerRank = new();

        public int Remaining => TotalCards - _cursor;
        public int TotalCards => _cards.Length;
        public int ReshuffleCount => _reshuffleCount;
        public IReadOnlyList<Card> DiscardPile => _discardPile;

        /// <summary>当前周期已摸出的各点数牌数（含手中+场上+弃牌堆）</summary>
        public IReadOnlyDictionary<CardRank, int> DrawnPerRank => _drawnPerRank;

        /// <summary>某种点数在所有周期中的总牌数（4 × 周期数，Joker 为 2 × 周期数）</summary>
        public int GetTotalPerRank(CardRank rank)
        {
            int perCycle = rank == CardRank.Joker ? 2 : 4;
            return perCycle * (_reshuffleCount + 1);
        }

        public event Action<Card> OnCardDrawn;
        public event Action OnDeckReshuffled;
        public event Action OnDiscarded;

        public CardDeck(int seed)
        {
            _rng = new System.Random(seed);
            _cards = CreateStandardDeck();
            _cursor = 0;
            _reshuffleCount = 0;
            Shuffle();
        }

        private static Card[] CreateStandardDeck()
        {
            var cards = new List<Card>(54);
            var suits = new[] { CardSuit.Spade, CardSuit.Heart, CardSuit.Club, CardSuit.Diamond };
            var ranks = new[] {
                CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six,
                CardRank.Seven, CardRank.Eight, CardRank.Nine, CardRank.Ten,
                CardRank.Jack, CardRank.Queen, CardRank.King, CardRank.Ace, CardRank.Two
            };
            int index = 0;
            foreach (var suit in suits)
                foreach (var rank in ranks)
                    cards.Add(new Card(suit, rank, index++));
            // 2 张 Joker 用不同 index 区分
            cards.Add(new Card(CardSuit.None, CardRank.Joker, index++));
            cards.Add(new Card(CardSuit.None, CardRank.Joker, index++));
            return cards.ToArray();
        }

        public void Shuffle()
        {
            for (int i = _cards.Length - 1; i > _cursor; i--)
            {
                int j = _rng.Next(_cursor, i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        public void Reshuffle()
        {
            _cursor = 0;
            _reshuffleCount++;
            // 弃牌堆不清空 —— 跨周期累积已打出的牌
            _drawnPerRank.Clear();
            Shuffle();
            OnDeckReshuffled?.Invoke();
        }

        public Card Draw()
        {
            if (Remaining <= 0) Reshuffle();
            Card card = _cards[_cursor];
            _cursor++;
            // 记录已摸出的牌
            _drawnPerRank.TryGetValue(card.Rank, out int count);
            _drawnPerRank[card.Rank] = count + 1;
            OnCardDrawn?.Invoke(card);
            return card;
        }

        public int Deal(int count, CardHand targetHand)
        {
            int dealt = 0;
            for (int i = 0; i < count; i++)
            {
                if (targetHand.IsFull) break;
                Card card = Draw();
                if (targetHand.Add(card)) dealt++;
            }
            return dealt;
        }

        public void Discard(Card card) { _discardPile.Add(card); OnDiscarded?.Invoke(); }
        public void Discard(IEnumerable<Card> cards) { foreach (var c in cards) _discardPile.Add(c); OnDiscarded?.Invoke(); }

        /// <summary>根据牌堆索引查找当前牌组中的 Card（用于网络反序列化）</summary>
        public Card GetCardByIndex(int index)
        {
            for (int i = 0; i < _cards.Length; i++)
                if (_cards[i].DeckIndex == index) return _cards[i];
            return default;
        }
    }
}

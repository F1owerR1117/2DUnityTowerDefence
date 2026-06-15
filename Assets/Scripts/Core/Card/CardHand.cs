using System;
using System.Collections.Generic;

namespace DoudizhuTower.Core.Cards
{
    public class CardHand
    {
        private readonly List<Card> _cards;
        private readonly HashSet<Card> _sealedCards = new();
        public int Capacity { get; }
        public int Count => _cards.Count;
        public bool IsFull => _cards.Count >= Capacity;
        public IReadOnlyList<Card> Cards => _cards;
        /// <summary>直接获取内部列表（用于高性能场景，避免类型转换）</summary>
        public List<Card> CardsList => _cards;
        public event Action<List<Card>> OnHandChanged;

        /// <summary>是否有任何卡牌被封印</summary>
        public bool HasSealedCards => _sealedCards.Count > 0;

        public CardHand(int capacity)
        {
            Capacity = capacity;
            _cards = new List<Card>(capacity);
        }

        public bool Add(Card card)
        {
            if (IsFull) return false;
            if (_cards.Contains(card)) return false; // 防止同一张物理牌重复加入
            _cards.Add(card);
            NotifyChanged();
            return true;
        }

        public bool Remove(Card card)
        {
            if (_cards.Remove(card))
            {
                NotifyChanged();
                return true;
            }
            return false;
        }

        public void RemoveRange(IEnumerable<Card> cards)
        {
            bool changed = false;
            foreach (var card in cards)
                changed |= _cards.Remove(card);
            if (changed) NotifyChanged();
        }

        public bool Contains(Card card) => _cards.Contains(card);

        public void Sort()
        {
            _cards.Sort();
            NotifyChanged();
        }

        public Card[] GetSortedCopy()
        {
            var copy = new Card[_cards.Count];
            _cards.CopyTo(copy);
            Array.Sort(copy);
            return copy;
        }

        public void Clear()
        {
            _cards.Clear();
            _sealedCards.Clear();
            NotifyChanged();
        }

        /// <summary>
        /// 设置卡牌封印状态。
        /// </summary>
        public void SetCardSealed(Card card, bool isSealed)
        {
            if (isSealed)
                _sealedCards.Add(card);
            else
                _sealedCards.Remove(card);
        }

        /// <summary>
        /// 检查卡牌是否被封印。
        /// </summary>
        public bool IsCardSealed(Card card)
        {
            return _sealedCards.Contains(card);
        }

        /// <summary>
        /// 清除所有封印状态。
        /// </summary>
        public void ClearAllSeals()
        {
            _sealedCards.Clear();
        }

        /// <summary>
        /// 获取未被封印的卡牌列表（用于 AI 出牌时过滤）。
        /// </summary>
        public List<Card> GetUnsealedCards()
        {
            var result = new List<Card>();
            foreach (var card in _cards)
            {
                if (!_sealedCards.Contains(card))
                    result.Add(card);
            }
            return result;
        }

        private void NotifyChanged() => OnHandChanged?.Invoke(_cards);

        /// <summary>手动触发手牌变更通知（供网络同步直接操作列表后调用）</summary>
        public void NotifyHandModified() => NotifyChanged();
    }
}

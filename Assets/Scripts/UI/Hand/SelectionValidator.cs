using System;
using System.Collections.Generic;
using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.UI.Hand
{
    /// <summary>
    /// 选牌校验器（临时缓冲区）。
    /// 仅在 UI 层维护选中的卡牌列表，最终通过 CommitSelection() 提交给 Gameplay 层。
    /// 绝对禁止直接修改 Core.CardHand 的数据（§RED-04）。
    /// </summary>
    public class SelectionValidator
    {
        private readonly List<Card> _selectedBuffer = new();
        private int _maxSelection;
        private CardTypeResult _lastValidation;

        /// <summary>当前选中的牌（只读）</summary>
        public IReadOnlyList<Card> CurrentSelection => _selectedBuffer;

        /// <summary>最近一次牌型检测结果</summary>
        public CardTypeResult LastValidation => _lastValidation;

        /// <summary>单次出牌上限</summary>
        public int MaxSelection => _maxSelection;

        /// <summary>当前选牌是否构成合规牌型</summary>
        public bool IsValidSelection => _lastValidation.IsValid;

        /// <summary>当前选牌数量</summary>
        public int SelectionCount => _selectedBuffer.Count;

        /// <summary>选牌变更事件</summary>
        public event Action OnSelectionChanged;

        public void Initialize(int maxSelection)
        {
            _maxSelection = maxSelection;
            _selectedBuffer.Clear();
            _lastValidation = CardTypeResult.Invalid;
        }

        /// <summary>
        /// 切换一张牌的选中状态。
        /// 封印牌不可选中（要不起领域/反制护盾）。
        /// </summary>
        /// <param name="card">目标卡牌</param>
        /// <param name="isSealed">该卡牌是否被封印</param>
        /// <returns>操作后该牌是否在缓冲区中</returns>
        public bool ToggleCard(Card card, bool isSealed = false)
        {
            // 已选中 → 取消选中
            if (_selectedBuffer.Contains(card))
            {
                _selectedBuffer.Remove(card);
                Revalidate();
                return false;
            }

            // 封印牌不可选中
            if (isSealed)
                return false;

            // 未选中且未达上限 → 加入
            if (_selectedBuffer.Count < _maxSelection)
            {
                _selectedBuffer.Add(card);
                Revalidate();
                return true;
            }

            // B7: 已达上限则拒绝选中，不移除任何已选卡牌，由 UI 层提供视觉反馈
            return false;
        }

        /// <summary>
        /// 清空选中
        /// </summary>
        public void ClearSelection()
        {
            _selectedBuffer.Clear();
            _lastValidation = CardTypeResult.Invalid;
            OnSelectionChanged?.Invoke();
        }

        /// <summary>
        /// 恢复选中状态（UI 重建后恢复选牌，不触发自动移除逻辑）
        /// </summary>
        public void RestoreSelection(IEnumerable<Card> cards)
        {
            _selectedBuffer.Clear();
            foreach (var card in cards)
            {
                if (_selectedBuffer.Count < _maxSelection)
                    _selectedBuffer.Add(card);
            }
            Revalidate();
        }

        /// <summary>
        /// 提交选中的牌（转移所有权，清空缓冲区）
        /// </summary>
        public Card[] CommitSelection()
        {
            var result = _selectedBuffer.ToArray();
            _selectedBuffer.Clear();
            _lastValidation = CardTypeResult.Invalid;
            OnSelectionChanged?.Invoke();
            return result;
        }

        /// <summary>
        /// 实时校验当前选牌
        /// </summary>
        private void Revalidate()
        {
            if (_selectedBuffer.Count == 0)
            {
                _lastValidation = CardTypeResult.Invalid;
            }
            else
            {
                _lastValidation = CardTypeDetector.Detect(
                    _selectedBuffer.ToArray(), _maxSelection);
            }

            OnSelectionChanged?.Invoke();
        }
    }
}

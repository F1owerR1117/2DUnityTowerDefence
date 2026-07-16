using UnityEngine;
using DoudizhuTower.Gameplay.Fusion;

namespace DoudizhuTower.Gameplay.Fusion.UI
{
    /// <summary>
    /// 手牌只读视图。
    /// 替代 HandArea 的核心显示逻辑，只读取 PlayerState。
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private int maxDisplayCards = 20;

        private readonly System.Collections.Generic.List<GameObject> _cardObjects = new();

        /// <summary>
        /// 根据 PlayerState 渲染手牌。
        /// 只读取 HandCount，不持有任何游戏对象引用。
        /// </summary>
        public void Render(PlayerState player)
        {
            int targetCount = Mathf.Min(player.HandCount, maxDisplayCards);

            // 确保卡牌对象数量匹配
            while (_cardObjects.Count < targetCount)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                _cardObjects.Add(card);
            }

            // 显示/隐藏卡牌
            for (int i = 0; i < _cardObjects.Count; i++)
            {
                _cardObjects[i].SetActive(i < targetCount);
            }
        }

        /// <summary>
        /// 清除所有卡牌显示。
        /// </summary>
        public void Clear()
        {
            foreach (var card in _cardObjects)
            {
                if (card != null) Destroy(card);
            }
            _cardObjects.Clear();
        }
    }
}
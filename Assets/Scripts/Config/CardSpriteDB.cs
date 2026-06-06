using UnityEngine;
using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.Config
{
    [CreateAssetMenu(fileName = "CardSpriteDB", menuName = "DoudizhuTower/Card Sprite DB")]
    public class CardSpriteDB : ScriptableObject
    {
        [Header("黑桃 (Spade)")]
        public Sprite spade_3, spade_4, spade_5, spade_6, spade_7, spade_8, spade_9, spade_10;
        public Sprite spade_J, spade_Q, spade_K, spade_A, spade_2;

        [Header("红心 (Heart)")]
        public Sprite heart_3, heart_4, heart_5, heart_6, heart_7, heart_8, heart_9, heart_10;
        public Sprite heart_J, heart_Q, heart_K, heart_A, heart_2;

        [Header("梅花 (Club)")]
        public Sprite club_3, club_4, club_5, club_6, club_7, club_8, club_9, club_10;
        public Sprite club_J, club_Q, club_K, club_A, club_2;

        [Header("方块 (Diamond)")]
        public Sprite diamond_3, diamond_4, diamond_5, diamond_6, diamond_7, diamond_8, diamond_9, diamond_10;
        public Sprite diamond_J, diamond_Q, diamond_K, diamond_A, diamond_2;

        [Header("Joker & 牌背")]
        public Sprite joker_1, joker_2, back;

        /// <summary>
        /// 获取卡牌对应精灵图
        /// </summary>
        public Sprite GetSprite(Card card)
        {
            if (card.IsJoker) return joker_1;

            string key = card.Suit.ToString().ToLower() + "_" + card.Rank.ToDisplayString().ToLower();
            var field = GetType().GetField(key, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            return field?.GetValue(this) as Sprite;
        }
    }
}

using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.Core.Battle
{
    /// <summary>
    /// 英雄类型（战前 5 选 1）
    /// 单 Joker → 英雄，双 Joker（王炸）→ 觉醒英雄
    /// </summary>
    public enum HeroType
    {
        /// <summary>剑圣：近战输出，攻速快</summary>
        Blademaster,
        /// <summary>铁卫：坦克，血量极高</summary>
        Guardian,
        /// <summary>神射：远程，精准点杀</summary>
        Sharpshooter,
        /// <summary>术士：范围，溅射伤害</summary>
        Warlock,
        /// <summary>灵骑：支援，友军光环</summary>
        SpiritRider,
    }

    /// <summary>
    /// 英雄属性数据（属性由 HeroConfig ScriptableObject 配置）
    /// </summary>
    public struct HeroStats
    {
        public HeroType Type;
        public string Name;
        public float HP;
        public float ATK;
        public float AttackInterval;
        public float MoveSpeed;
        public float Range;
        public float CollisionRadius;

        /// <summary>
        /// 转换为 SoldierStats（用于兵种生成）
        /// </summary>
        public SoldierStats ToSoldierStats()
        {
            return new SoldierStats
            {
                Rank = CardRank.Joker,
                HP = HP,
                ATK = ATK,
                AttackInterval = AttackInterval,
                MoveSpeed = MoveSpeed,
                Range = Range,
                CollisionRadius = CollisionRadius
            };
        }
    }
}

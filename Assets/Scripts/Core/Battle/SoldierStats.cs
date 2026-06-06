using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.Core.Battle
{
    /// <summary>
    /// 兵线枚举（§2.2）
    /// </summary>
    public enum Lane
    {
        None,   // 无路线（双路线可攻击）
        Top,    // 上路
        Bottom  // 下路
    }

    /// <summary>
    /// 阵营身份
    /// </summary>
    public enum Identity
    {
        FarmerA,
        FarmerB,
        Landlord
    }

    /// <summary>
    /// 伤害类型
    /// </summary>
    public enum DamageType
    {
        Physical,   // 普通物理伤害
        Special,    // 技能伤害
        Bomb,       // 炸弹/爆炸伤害
        Burn,       // 灼烧伤害
        True,       // 真实伤害（无视护盾/减免）
    }

    /// <summary>
    /// 兵种属性数据块（纯数据，无 Unity 依赖）。
    /// 所有值来源于 §3.1 属性表。
    /// </summary>
    public struct SoldierStats
    {
        public CardRank Rank;
        public float HP;
        public float ATK;
        public float AttackInterval;   // 秒
        public float MoveSpeed;        // 单位/秒
        public float Range;            // 米
        public float CollisionRadius;  // 米
        public int HitCount;           // 单次攻击动画的打击帧数（默认 1）

        /// <summary>
        /// 计算每秒伤害（DPS = ATK × HitCount / AttackInterval）
        /// </summary>
        public readonly float DPS => ATK * HitCount / AttackInterval;
    }
}

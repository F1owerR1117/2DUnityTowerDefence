using DoudizhuTower.Core.Battle;
using UnityEngine;

namespace DoudizhuTower.Config
{
    /// <summary>
    /// 英雄配置 ScriptableObject。
    /// 存储英雄的属性和技能数据，替代 HeroType.cs 中的硬编码。
    ///
    /// 使用方法：
    /// 1. 在 Project 窗口右键 → Create → DoudizhuTower → Hero Config
    /// 2. 配置英雄属性和技能参数
    /// 3. 在 BattleManager 的 Inspector 中拖入
    /// </summary>
    [CreateAssetMenu(fileName = "NewHeroConfig", menuName = "DoudizhuTower/Hero Config")]
    public class HeroConfig : ScriptableObject
    {
        [Header("英雄信息")]
        [Tooltip("英雄类型")]
        public HeroType heroType;

        [Tooltip("英雄名称")]
        public string heroName = "英雄";

        [Header("基础属性")]
        [Tooltip("生命值")]
        public float hp = 500f;

        [Tooltip("攻击力")]
        public float atk = 35f;

        [Tooltip("攻击间隔（秒）")]
        public float attackInterval = 1.0f;

        [Tooltip("移动速度")]
        public float moveSpeed = 3.0f;

        [Tooltip("攻击范围")]
        public float range = 1.8f;

        [Tooltip("碰撞箱半径")]
        public float collisionRadius = 0.7f;

        [Header("觉醒属性倍率")]
        [Tooltip("觉醒生命值倍率")]
        public float awakenHpMultiplier = 2.0f;

        [Tooltip("觉醒攻击力倍率")]
        public float awakenAtkMultiplier = 2.0f;

        [Tooltip("觉醒移动速度倍率")]
        public float awakenMoveSpeedMultiplier = 1.2f;

        [Tooltip("觉醒攻击范围倍率")]
        public float awakenRangeMultiplier = 1.3f;

        [Tooltip("觉醒碰撞箱倍率")]
        public float awakenCollisionRadiusMultiplier = 1.5f;

        [Header("剑圣技能参数")]
        [Tooltip("剑圣额外伤害触发概率")]
        public float blademasterProcChance = 0.2f;
        [Tooltip("剑圣额外伤害倍率")]
        public float blademasterDamageMultiplier = 0.5f;

        [Header("铁卫技能参数")]
        [Tooltip("铁卫伤害减免")]
        public float guardianDamageReduction = 0.3f;

        [Header("术士技能参数")]
        [Tooltip("术士溅射范围")]
        public float warlockSplashRadius = 1.5f;
        [Tooltip("术士溅射伤害倍率")]
        public float warlockSplashDamageMultiplier = 0.2f;

        [Header("灵骑技能参数")]
        [Tooltip("灵骑光环范围")]
        public float spiritRiderAuraRadius = 4f;
        [Tooltip("灵骑攻速加成")]
        public float spiritRiderAttackSpeedBonus = 1.15f;
        [Tooltip("灵骑移速加成")]
        public float spiritRiderMoveSpeedBonus = 1.15f;

        /// <summary>
        /// 获取基础英雄属性。
        /// </summary>
        public HeroStats GetBaseStats()
        {
            return new HeroStats
            {
                Type = heroType,
                Name = heroName,
                HP = hp,
                ATK = atk,
                AttackInterval = attackInterval,
                MoveSpeed = moveSpeed,
                Range = range,
                CollisionRadius = collisionRadius
            };
        }

        /// <summary>
        /// 获取觉醒英雄属性。
        /// </summary>
        public HeroStats GetAwakenedStats()
        {
            return new HeroStats
            {
                Type = heroType,
                Name = $"觉醒{heroName}",
                HP = hp * awakenHpMultiplier,
                ATK = atk * awakenAtkMultiplier,
                AttackInterval = attackInterval,  // 攻击间隔不变
                MoveSpeed = moveSpeed * awakenMoveSpeedMultiplier,
                Range = range * awakenRangeMultiplier,
                CollisionRadius = collisionRadius * awakenCollisionRadiusMultiplier
            };
        }
    }
}

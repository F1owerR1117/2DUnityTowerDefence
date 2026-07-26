using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 英雄单位配置组件。
    /// 挂载到英雄预制体上，包含英雄的所有配置信息。
    /// 英雄的生成完全取决于其预制体，不再依赖外部 HeroConfig。
    /// </summary>
    [RequireComponent(typeof(CardUnit))]
    public class HeroUnitConfig : MonoBehaviour
    {
        [Header("英雄信息")]
        [Tooltip("英雄名称")]
        public string heroName = "英雄";

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

        [Header("被动技能")]
        [Tooltip("英雄被动技能组件（按顺序执行）")]
        public HeroPassiveBase[] passives;

        /// <summary>
        /// 应用觉醒属性倍率到单位。
        /// </summary>
        public void ApplyAwakenedStats(CardUnit unit)
        {
            if (unit == null) return;
            
            var stats = unit.Stats;
            stats.HP *= awakenHpMultiplier;
            stats.ATK *= awakenAtkMultiplier;
            stats.MoveSpeed *= awakenMoveSpeedMultiplier;
            stats.Range *= awakenRangeMultiplier;
            stats.CollisionRadius *= awakenCollisionRadiusMultiplier;
            unit.SetStats(stats);
        }

        /// <summary>
        /// 应用所有被动技能到单位。
        /// </summary>
        public void ApplyPassives(CardUnit unit, bool awakened)
        {
            if (passives == null) return;
            
            foreach (var passive in passives)
            {
                if (passive != null)
                    passive.Apply(unit, awakened);
            }
        }

        /// <summary>
        /// 移除所有被动技能。
        /// </summary>
        public void RemovePassives(CardUnit unit)
        {
            if (passives == null) return;
            
            foreach (var passive in passives)
            {
                if (passive != null)
                    passive.Remove(unit);
            }
        }
    }
}
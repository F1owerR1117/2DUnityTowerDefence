using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 英雄被动技能基类。
    /// 所有英雄被动技能都继承自此类。
    /// 每个被动技能应该是独立的组件，可以挂载到英雄预制体上。
    /// </summary>
    public abstract class HeroPassiveBase : MonoBehaviour
    {
        [Tooltip("被动技能描述（仅用于 Inspector 显示）")]
        [SerializeField] protected string description = "被动技能";

        /// <summary>
        /// 应用被动技能到单位。
        /// </summary>
        /// <param name="unit">目标单位</param>
        /// <param name="awakened">是否为觉醒状态</param>
        public abstract void Apply(CardUnit unit, bool awakened);

        /// <summary>
        /// 移除被动技能（单位死亡或离开时调用）。
        /// </summary>
        /// <param name="unit">目标单位</param>
        public abstract void Remove(CardUnit unit);

        /// <summary>
        /// 当单位被销毁时自动移除被动技能。
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 子类可以重写此方法进行清理
        }
    }
}
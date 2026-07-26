using DoudizhuTower.Core.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities.Passives
{
    /// <summary>
    /// 剑圣被动技能：攻击时有概率造成额外伤害。
    /// </summary>
    public class BlademasterPassive : HeroPassiveBase
    {
        [Header("剑圣技能参数")]
        [Tooltip("额外伤害触发概率")]
        [Range(0f, 1f)]
        public float procChance = 0.2f;
        
        [Tooltip("额外伤害倍率")]
        public float damageMultiplier = 0.5f;

        private CardUnit _owner;
        private System.Action<CardUnit> _onAttackHandler;

        public override void Apply(CardUnit unit, bool awakened)
        {
            if (unit == null) return;
            
            _owner = unit;
            float mult = awakened ? damageMultiplier * 2f : damageMultiplier;
            
            _onAttackHandler = (target) =>
            {
                if (target != null && target.IsAlive && Random.value < procChance)
                {
                    target.TakeDamage(unit.Stats.ATK * mult, DamageType.Physical);
                }
            };
            
            unit.OnAttackEvent += _onAttackHandler;
        }

        public override void Remove(CardUnit unit)
        {
            if (unit != null && _onAttackHandler != null)
            {
                unit.OnAttackEvent -= _onAttackHandler;
            }
            _onAttackHandler = null;
            _owner = null;
        }

        protected override void OnDestroy()
        {
            if (_owner != null)
                Remove(_owner);
        }
    }
}
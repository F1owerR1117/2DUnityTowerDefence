using DoudizhuTower.Core.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities.Passives
{
    /// <summary>
    /// 术士被动技能：攻击时溅射范围伤害。
    /// </summary>
    public class WarlockPassive : HeroPassiveBase
    {
        [Header("术士技能参数")]
        [Tooltip("溅射范围")]
        public float splashRadius = 1.5f;
        
        [Tooltip("溅射伤害倍率")]
        public float splashDamageMultiplier = 0.2f;

        private CardUnit _owner;
        private System.Action<CardUnit> _onAttackHandler;
        private ContactFilter2D _filter;
        private Collider2D[] _overlapCache = new Collider2D[64];

        public override void Apply(CardUnit unit, bool awakened)
        {
            if (unit == null) return;
            
            _owner = unit;
            _filter = new ContactFilter2D().NoFilter();
            
            float mult = awakened ? splashDamageMultiplier * 2f : splashDamageMultiplier;
            
            _onAttackHandler = (target) =>
            {
                if (target == null || !target.IsAlive) return;
                
                int count = Physics2D.OverlapCircle(unit.VisualCenter, splashRadius, _filter, _overlapCache);
                
                for (int i = 0; i < count; i++)
                {
                    var splash = _overlapCache[i].GetComponentInParent<CardUnit>();
                    if (splash == null || splash == target || !splash.IsAlive)
                        continue;
                    
                    if (splash.IsLandlord == unit.IsLandlord)
                        continue;
                    
                    if (!unit.CanAttackHeight(splash.UnitHeight))
                        continue;
                    
                    splash.TakeDamage(unit.Stats.ATK * mult, DamageType.Special);
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
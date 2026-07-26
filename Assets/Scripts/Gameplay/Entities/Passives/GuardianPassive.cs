using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities.Passives
{
    /// <summary>
    /// 铁卫被动技能：嘲讽源 + 伤害减免。
    /// </summary>
    public class GuardianPassive : HeroPassiveBase
    {
        [Header("铁卫技能参数")]
        [Tooltip("伤害减免比例（0=无减免，1=免疫所有伤害）")]
        [Range(0f, 0.9f)]
        public float damageReduction = 0.3f;
        
        [Tooltip("是否为嘲讽源")]
        public bool isTauntSource = true;
        
        [Tooltip("嘲讽光环半径")]
        public float tauntRadius = 3f;

        private CardUnit _owner;

        public override void Apply(CardUnit unit, bool awakened)
        {
            if (unit == null) return;
            
            _owner = unit;
            
            if (isTauntSource)
            {
                unit.IsTauntSource = true;
                unit.TauntRadius = tauntRadius;
            }
            
            // 觉醒时伤害减免增加
            float reduction = awakened ? Mathf.Min(damageReduction + 0.2f, 0.9f) : damageReduction;
            unit.DamageReduction = reduction;
        }

        public override void Remove(CardUnit unit)
        {
            if (unit != null)
            {
                unit.IsTauntSource = false;
                unit.TauntRadius = 0f;
                unit.DamageReduction = 0f;
            }
            _owner = null;
        }

        protected override void OnDestroy()
        {
            if (_owner != null)
                Remove(_owner);
        }
    }
}
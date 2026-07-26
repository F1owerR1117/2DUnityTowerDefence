using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities.Passives
{
    /// <summary>
    /// 神射被动技能：优先攻击血量最低的敌人。
    /// </summary>
    public class SharpshooterPassive : HeroPassiveBase
    {
        [Header("神射技能参数")]
        [Tooltip("索敌范围（0=使用攻击范围）")]
        public float detectionRange = 999f;

        private CardUnit _owner;
        private System.Func<CardUnit> _originalFindTarget;
        private ContactFilter2D _filter;
        private Collider2D[] _overlapCache = new Collider2D[64];

        public override void Apply(CardUnit unit, bool awakened)
        {
            if (unit == null) return;
            
            _owner = unit;
            _filter = new ContactFilter2D().NoFilter();
            
            // 保存原始索敌方法（如果有）
            _originalFindTarget = unit.OverrideFindTarget;
            
            // 覆盖索敌逻辑：优先攻击血量最低的敌人
            unit.OverrideFindTarget = () =>
            {
                float minPct = float.MaxValue;
                CardUnit best = null;
                
                float range = detectionRange > 0 ? detectionRange : unit.Stats.Range;
                int count = Physics2D.OverlapCircle(unit.VisualCenter, range, _filter, _overlapCache);
                
                for (int i = 0; i < count; i++)
                {
                    var enemy = _overlapCache[i].GetComponentInParent<CardUnit>();
                    if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == unit.IsLandlord)
                        continue;
                    
                    if (!unit.CanAttackHeight(enemy.UnitHeight))
                        continue;
                    
                    float pct = enemy.CurrentHP / enemy.Stats.HP;
                    if (pct < minPct)
                    {
                        minPct = pct;
                        best = enemy;
                    }
                }
                
                return best;
            };
        }

        public override void Remove(CardUnit unit)
        {
            if (unit != null)
            {
                // 恢复原始索敌方法
                unit.OverrideFindTarget = _originalFindTarget;
            }
            _owner = null;
            _originalFindTarget = null;
        }

        protected override void OnDestroy()
        {
            if (_owner != null)
                Remove(_owner);
        }
    }
}
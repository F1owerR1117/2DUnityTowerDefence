using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities.Passives
{
    /// <summary>
    /// 灵骑被动技能：友军光环（攻速 + 移速加成）。
    /// </summary>
    public class SpiritRiderPassive : HeroPassiveBase
    {
        [Header("灵骑技能参数")]
        [Tooltip("光环范围")]
        public float auraRadius = 4f;
        
        [Tooltip("攻速加成倍率")]
        public float attackSpeedBonus = 1.15f;
        
        [Tooltip("移速加成倍率")]
        public float moveSpeedBonus = 1.15f;

        private CardUnit _owner;
        private Coroutine _auraCoroutine;
        private Dictionary<CardUnit, bool> _buffedAllies = new Dictionary<CardUnit, bool>();
        private List<CardUnit> _toRemove = new List<CardUnit>();
        private ContactFilter2D _filter;
        private Collider2D[] _overlapCache = new Collider2D[64];

        public override void Apply(CardUnit unit, bool awakened)
        {
            if (unit == null) return;
            
            _owner = unit;
            _filter = new ContactFilter2D().NoFilter();
            
            // 觉醒时加成增强
            float atkBonus = awakened ? attackSpeedBonus * 1.2f : attackSpeedBonus;
            float spdBonus = awakened ? moveSpeedBonus * 1.2f : moveSpeedBonus;
            
            _auraCoroutine = unit.StartCoroutine(AuraCoroutine(atkBonus, spdBonus));
        }

        public override void Remove(CardUnit unit)
        {
            if (_auraCoroutine != null && unit != null)
            {
                unit.StopCoroutine(_auraCoroutine);
                _auraCoroutine = null;
            }
            
            // 移除所有 buff
            foreach (var kvp in _buffedAllies)
            {
                if (kvp.Key != null && kvp.Key.IsAlive)
                {
                    kvp.Key.RemoveBuff("spirit_rider");
                }
            }
            _buffedAllies.Clear();
            _owner = null;
        }

        private IEnumerator AuraCoroutine(float atkBonus, float spdBonus)
        {
            var delay = new WaitForSeconds(1f);
            
            while (_owner != null && _owner.IsAlive)
            {
                int count = Physics2D.OverlapCircle(_owner.VisualCenter, auraRadius, _filter, _overlapCache);
                var inRange = new HashSet<CardUnit>();
                
                for (int i = 0; i < count; i++)
                {
                    var ally = _overlapCache[i].GetComponentInParent<CardUnit>();
                    if (ally == null || !ally.IsAlive || ally == _owner)
                        continue;
                    
                    if (ally.IsLandlord != _owner.IsLandlord)
                        continue;
                    
                    inRange.Add(ally);
                    
                    if (!_buffedAllies.ContainsKey(ally))
                    {
                        _buffedAllies[ally] = true;
                        ally.ApplyBuff("spirit_rider", new CardUnit.StatBuff(
                            atkInterval: 1f / atkBonus,
                            moveSpeed: spdBonus));
                    }
                }
                
                // 移除离开范围的 buff
                _toRemove.Clear();
                foreach (var kvp in _buffedAllies)
                {
                    if (!inRange.Contains(kvp.Key) || !kvp.Key.IsAlive)
                    {
                        kvp.Key.RemoveBuff("spirit_rider");
                        _toRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var r in _toRemove)
                    _buffedAllies.Remove(r);
                
                yield return delay;
            }
            
            // 清理所有 buff
            foreach (var kvp in _buffedAllies)
            {
                if (kvp.Key != null && kvp.Key.IsAlive)
                    kvp.Key.RemoveBuff("spirit_rider");
            }
            _buffedAllies.Clear();
        }

        protected override void OnDestroy()
        {
            if (_owner != null)
                Remove(_owner);
        }
    }
}
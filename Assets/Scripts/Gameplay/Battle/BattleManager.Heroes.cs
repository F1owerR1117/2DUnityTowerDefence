using System.Collections;
using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Entities;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    public partial class BattleManager
    {
        // ─── 英雄生成 ──────────────────────────────────

        CardUnit SpawnHero(Lane lane, Component sourceBase, bool awakened)
        {
            if (heroPrefab == null) { Debug.LogError("[BattleManager] 未设置英雄预制体"); return null; }
            if (_heroConfig == null) { Debug.LogError("[BattleManager] 未配置 HeroConfig"); return null; }

            var cardType = awakened ? CardType.DoubleKingBomb : CardType.Single;
            var unit = CreateUnitWithPrefab(heroPrefab, CardRank.Joker, lane, sourceBase, cardType);
            if (unit == null) return null;

            if (!_useHeroPrefabStats)
            {
                HeroStats heroStats = awakened ? _heroConfig.GetAwakenedStats() : _heroConfig.GetBaseStats();

                var stats = unit.Stats;
                stats.HP = heroStats.HP; stats.ATK = heroStats.ATK;
                stats.AttackInterval = heroStats.AttackInterval; stats.MoveSpeed = heroStats.MoveSpeed;
                stats.Range = heroStats.Range; stats.CollisionRadius = heroStats.CollisionRadius;
                unit.SetStats(stats);
            }
            else
            {
                if (awakened)
                {
                    float hpMult = _heroConfig.awakenHpMultiplier;
                    float atkMult = _heroConfig.awakenAtkMultiplier;
                    float spdMult = _heroConfig.awakenMoveSpeedMultiplier;
                    float rngMult = _heroConfig.awakenRangeMultiplier;

                    var stats = unit.Stats;
                    stats.HP *= hpMult;
                    stats.ATK *= atkMult;
                    stats.MoveSpeed *= spdMult;
                    stats.Range *= rngMult;
                    unit.SetStats(stats);
                }
            }

            InjectHeroPassives(unit, awakened);

            return unit;
        }

        private void InjectHeroPassives(CardUnit unit, bool awakened)
        {
            float mult = awakened ? _heroConfig.awakenAtkMultiplier : 1f;

            float blademasterProcChance = _heroConfig.blademasterProcChance;
            float blademasterDmgMult = _heroConfig.blademasterDamageMultiplier;
            float guardianDmgReduction = _heroConfig.guardianDamageReduction;
            float warlockSplashRadius = _heroConfig.warlockSplashRadius;
            float warlockSplashDmgMult = _heroConfig.warlockSplashDamageMultiplier;
            float spiritRiderAuraRadius = _heroConfig.spiritRiderAuraRadius;
            float spiritRiderAtkBonus = _heroConfig.spiritRiderAttackSpeedBonus;
            float spiritRiderSpdBonus = _heroConfig.spiritRiderMoveSpeedBonus;

            switch (_selectedHero)
            {
                case HeroType.Blademaster:
                    unit.OnAttackEvent += (target) =>
                    {
                        if (target != null && Random.value < blademasterProcChance)
                            target.TakeDamage(unit.Stats.ATK * blademasterDmgMult, DamageType.Physical);
                    };
                    break;

                case HeroType.Guardian:
                    unit.IsTauntSource = true;
                    unit.DamageReduction = guardianDmgReduction;
                    break;

                case HeroType.Sharpshooter:
                    unit.OverrideFindTarget = () =>
                    {
                        float minPct = float.MaxValue;
                        CardUnit best = null;
                        var filter = new ContactFilter2D().NoFilter();
                        int count = Physics2D.OverlapCircle(unit.VisualCenter, 999f, filter, _overlapCache);
                        for (int i = 0; i < count; i++)
                        {
                            var enemy = _overlapCache[i].GetComponentInParent<CardUnit>();
                            if (enemy == null || !enemy.IsAlive || enemy.IsLandlord == unit.IsLandlord) continue;
                            float pct = enemy.CurrentHP / enemy.Stats.HP;
                            if (pct < minPct) { minPct = pct; best = enemy; }
                        }
                        return best;
                    };
                    break;

                case HeroType.Warlock:
                    unit.OnAttackEvent += (target) =>
                    {
                        var filter = new ContactFilter2D().NoFilter();
                        int count = Physics2D.OverlapCircle(unit.VisualCenter, warlockSplashRadius, filter, _overlapCache);
                        for (int i = 0; i < count; i++)
                        {
                            var splash = _overlapCache[i].GetComponentInParent<CardUnit>();
                            if (splash == null || splash == target || !splash.IsAlive) continue;
                            if (splash.IsLandlord == unit.IsLandlord) continue;
                            if (!unit.CanAttackHeight(splash.UnitHeight)) continue;
                            splash.TakeDamage(unit.Stats.ATK * warlockSplashDmgMult * mult, DamageType.Special);
                        }
                    };
                    break;

                case HeroType.SpiritRider:
                    unit.StartCoroutine(SpiritRiderAuraCoroutine(unit, spiritRiderAuraRadius, spiritRiderAtkBonus, spiritRiderSpdBonus));
                    break;
            }
        }

        private static IEnumerator SpiritRiderAuraCoroutine(CardUnit owner, float auraRadius, float atkSpeedBonus, float moveSpeedBonus)
        {
            var delay = new WaitForSeconds(1f);
            var buffedAllies = new Dictionary<CardUnit, bool>();
            var toRemove = new List<CardUnit>();
            var overlapCache = new Collider2D[64];
            var filter = new ContactFilter2D().NoFilter();

            while (owner != null && owner.IsAlive)
            {
                int count = Physics2D.OverlapCircle(owner.VisualCenter, auraRadius, filter, overlapCache);
                var inRange = new HashSet<CardUnit>();

                for (int i = 0; i < count; i++)
                {
                    var ally = overlapCache[i].GetComponentInParent<CardUnit>();
                    if (ally == null || !ally.IsAlive || ally == owner) continue;
                    if (ally.IsLandlord != owner.IsLandlord) continue;
                    inRange.Add(ally);

                    if (!buffedAllies.ContainsKey(ally))
                    {
                        buffedAllies[ally] = true;
                        ally.ApplyBuff("spirit_rider", new CardUnit.StatBuff(
                            atkInterval: 1f / atkSpeedBonus, moveSpeed: moveSpeedBonus));
                    }
                }

                toRemove.Clear();
                foreach (var kvp in buffedAllies)
                {
                    if (!inRange.Contains(kvp.Key) || !kvp.Key.IsAlive)
                    {
                        kvp.Key.RemoveBuff("spirit_rider");
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (var r in toRemove) buffedAllies.Remove(r);

                yield return delay;
            }

            foreach (var kvp in buffedAllies)
            {
                if (kvp.Key != null && kvp.Key.IsAlive)
                    kvp.Key.RemoveBuff("spirit_rider");
            }
        }
    }
}

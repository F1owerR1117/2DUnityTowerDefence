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
            if (heroPrefab == null)
            {
                Debug.LogError("[BattleManager] 未设置英雄预制体");
                return null;
            }

            var cardType = awakened ? CardType.DoubleKingBomb : CardType.Single;
            var unit = CreateUnitWithPrefab(heroPrefab, CardRank.Joker, lane, sourceBase, cardType);
            if (unit == null) return null;

            // 从预制体获取英雄配置
            var heroConfig = heroPrefab.GetComponent<HeroUnitConfig>();
            
            if (heroConfig != null)
            {
                // 应用觉醒倍率
                if (awakened)
                {
                    heroConfig.ApplyAwakenedStats(unit);
                }
                
                // 应用被动技能
                heroConfig.ApplyPassives(unit, awakened);
            }
            else
            {
                Debug.LogWarning($"[BattleManager] 英雄预制体 {heroPrefab.name} 缺少 HeroUnitConfig 组件");
            }

            return unit;
        }
    }
}
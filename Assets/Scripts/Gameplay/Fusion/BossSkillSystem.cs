using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// Boss 技能系统（纯逻辑，无 MonoBehaviour）。
    /// 只负责生成事件，不直接修改 UnitState。
    /// </summary>
    /// <remarks>
    /// ⚠️ [已废弃] 当前未被 FusionGameManager 使用。
    /// Boss 技能逻辑将在 Phase 7 中按需迁移。
    /// </remarks>
    [System.Obsolete("未被 FusionGameManager 使用，Boss 技能逻辑将在 Phase 7 中按需迁移")]
    public class BossSkillSystem
    {
        // 技能配置
        private const int DASH_INTERVAL = 120;  // 冲锋间隔（Tick）
        private const float DASH_DAMAGE = 50f;
        private const float DASH_WIDTH = 2f;
        private const float DASH_DISTANCE = 5f;

        /// <summary>
        /// 模拟 Boss 技能
        /// </summary>
        public void Simulate(WorldState world, UnitBuffer units, EventBuffer events, int currentTick)
        {
            // 检查是否到达技能触发 Tick
            if (!IsBossTick(currentTick)) return;

            // 执行冲锋技能
            ExecuteDashSkill(world, units, events);
        }

        /// <summary>
        /// 检查是否是 Boss 技能 Tick
        /// </summary>
        private bool IsBossTick(int tick)
        {
            return tick % DASH_INTERVAL == 0;
        }

        /// <summary>
        /// 执行冲锋技能
        /// </summary>
        private void ExecuteDashSkill(WorldState world, UnitBuffer units, EventBuffer events)
        {
            // 1. 找到 Boss（Owner = -1 或特殊标记）
            int bossIndex = FindBoss(units);
            if (bossIndex == -1) return;

            var boss = units.Get(bossIndex);

            // 2. 计算冲锋终点（朝向最近敌人）
            int targetIndex = FindNearestEnemy(boss, units);
            if (targetIndex == -1) return;

            var target = units.Get(targetIndex);
            float dx = target.PosX - boss.PosX;
            float dy = target.PosY - boss.PosY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist < 0.01f) return;

            // 归一化方向
            float nx = dx / dist;
            float ny = dy / dist;

            // 终点
            float endX = boss.PosX + nx * DASH_DISTANCE;
            float endY = boss.PosY + ny * DASH_DISTANCE;

            // Dash 事件由 CombatSystem 处理
        }

        /// <summary>
        /// 找到 Boss 单位
        /// </summary>
        private int FindBoss(UnitBuffer units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units.Get(i);
                // Boss 标记：Owner = -1 或其他特殊标记
                if (unit.Owner == -1 && unit.State != UnitStateConstants.Dead)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 找到最近的敌人
        /// </summary>
        private int FindNearestEnemy(UnitState boss, UnitBuffer units)
        {
            int bestIndex = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units.Get(i);
                if (unit.State == UnitStateConstants.Dead) continue;
                if (unit.Owner == boss.Owner) continue;

                float dx = unit.PosX - boss.PosX;
                float dy = unit.PosY - boss.PosY;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
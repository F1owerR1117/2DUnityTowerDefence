using Fusion;
using UnityEngine;
using System.Collections.Generic;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// Fusion 战斗管理器。
    /// 替代 BattleManager，基于 Tick 状态机。
    /// </summary>
    public class FusionBattleManager : NetworkBehaviour
    {
        [Header("引用")]
        [SerializeField] private FusionGameManager gameManager;

        // =========================
        // 战斗状态
        // =========================
        [Networked]
        public int UnitCount { get; set; }

        [Networked]
        public int TurnNumber { get; set; }

        // =========================
        // 初始化
        // =========================
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                InitializeBattle();
            }
        }

        private void InitializeBattle()
        {
            UnitCount = 0;
            TurnNumber = 0;
        }

        // =========================
        // 主战斗循环（Fusion核心）
        // =========================
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // 处理战斗逻辑
            ProcessCombat();
            CheckVictoryCondition();
        }

        // =========================
        // 战斗处理
        // =========================
        private void ProcessCombat()
        {
            // Phase 3 填充：单位攻击、移动、技能等
        }

        // =========================
        // 胜负判定
        // =========================
        private void CheckVictoryCondition()
        {
            if (gameManager == null) return;

            var world = gameManager.World;

            // 检查是否所有敌方基地被摧毁
            // Phase 3 填充
        }

        // =========================
        // 单位生成（供外部调用）
        // =========================
        public void SpawnUnit(byte slot, byte cardId)
        {
            if (!HasStateAuthority) return;

            UnitCount++;
            // Phase 3 填充：根据 cardId 生成对应单位
        }

        // =========================
        // 单位死亡
        // =========================
        public void OnUnitDied(int unitId)
        {
            if (!HasStateAuthority) return;

            UnitCount--;
            // Phase 3 填充：处理死亡逻辑、金币奖励等
        }
    }
}
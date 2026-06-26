using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// AI 系统（纯逻辑，无 MonoBehaviour）。
    /// 只负责生成意图，不直接控制单位。
    /// </summary>
    public class AISystem
    {
        private const int DECISION_INTERVAL_TICKS = 240;
        private const int SPAWN_INTERVAL_TICKS = 480;

        private int _decisionTickCounter;
        private int _spawnTickCounter;

        public void Simulate(WorldState world, UnitBuffer units, IntentBuffer intents, int currentTick)
        {
            _decisionTickCounter++;
            _spawnTickCounter++;

            // 叫分阶段：每次 Simulate 调用都尝试决策（节流由 ProcessAI 控制）
            if (world.Game.Phase == 0)
            {
                MakeBidDecisions(world, intents);
            }

            // 战斗阶段：AI 生成战斗意图
            if (world.Game.Phase == 1)
            {
                if (_decisionTickCounter >= DECISION_INTERVAL_TICKS)
                {
                    _decisionTickCounter = 0;
                    MakeDecisions(world, units, intents);
                }

                if (_spawnTickCounter >= SPAWN_INTERVAL_TICKS)
                {
                    _spawnTickCounter = 0;
                    MakeSpawnDecisions(world, units, intents);
                }
            }
        }

        /// <summary>
        /// AI 叫分决策（只生成意图，不直接改状态）
        /// </summary>
        private void MakeBidDecisions(WorldState world, IntentBuffer intents)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                var player = GetPlayer(world, slot);
                Debug.Log($"[AI] slot={slot} IsAI={player.IsAI} Bid={player.Bid} CurrentTurn={world.Game.CurrentBidTurn}");
                if (player.IsAI == 0) continue;
                if (player.Bid != 0) continue;
                if (world.Game.CurrentBidTurn != slot) continue;

                int bid = DecideBid(world, player);
                intents.AddBid(slot, bid);
                Debug.Log($"[AI] slot={slot} bid={bid}");
            }
        }

        private int DecideBid(WorldState world, PlayerState player)
        {
            int highest = world.Game.HighestBid;
            int minBid = highest > 0 ? highest + 1 : 1;
            if (minBid > 3) return 0;

            float passChance = 0.5f;
            if (Random.value < passChance) return 0;

            return Mathf.Clamp(minBid, 1, 3);
        }

        private PlayerState GetPlayer(WorldState world, int slot)
        {
            switch (slot)
            {
                case 0: return world.Player0;
                case 1: return world.Player1;
                case 2: return world.Player2;
                default: return world.Player0;
            }
        }

        /// <summary>
        /// 为所有 AI 单位生成决策
        /// </summary>
        private void MakeDecisions(WorldState world, UnitBuffer units, IntentBuffer intents)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units.Get(i);
                if (unit.State == UnitStateConstants.Dead) continue;

                // 只处理 AI 控制的单位（Owner = -1 表示 AI）
                if (unit.Owner != -1) continue;

                var intent = Decide(unit, units);
                if (intent.Type != IntentType.None)
                {
                    intents.Add(intent);
                }
            }
        }

        /// <summary>
        /// 为建筑生成生成决策
        /// </summary>
        private void MakeSpawnDecisions(WorldState world, UnitBuffer units, IntentBuffer intents)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units.Get(i);
                if (unit.State == UnitStateConstants.Dead) continue;

                // 只处理 AI 建筑（Owner = -1）
                if (unit.Owner != -1) continue;

                // 建筑生成单位
                if (unit.HP > unit.MaxHP * 0.5f) // 血量高于 50% 才生成
                {
                    intents.AddSpawn(unit.UnitId, 0, unit.PosX, unit.PosY - 1f);
                }
            }
        }

        /// <summary>
        /// 单个单位的决策逻辑
        /// </summary>
        private UnitIntent Decide(UnitState unit, UnitBuffer units)
        {
            // 1. 找最近敌人
            int targetId = FindNearestEnemy(unit, units);

            if (targetId == -1)
            {
                // 无敌人：待机
                return new UnitIntent
                {
                    UnitId = unit.UnitId,
                    Type = IntentType.Idle
                };
            }

            var target = units.Get(targetId);
            float dist = Distance(unit, target);

            // 2. 在攻击范围内：攻击
            if (dist <= unit.AttackRange)
            {
                return new UnitIntent
                {
                    UnitId = unit.UnitId,
                    Type = IntentType.Attack,
                    TargetId = targetId
                };
            }

            // 3. 不在攻击范围内：移动朝向目标
            return new UnitIntent
            {
                UnitId = unit.UnitId,
                Type = IntentType.Move,
                TargetPosX = target.PosX,
                TargetPosY = target.PosY
            };
        }

        /// <summary>
        /// 找最近敌人
        /// </summary>
        private int FindNearestEnemy(UnitState unit, UnitBuffer units)
        {
            int bestIndex = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                var other = units.Get(i);
                if (other.State == UnitStateConstants.Dead) continue;
                if (other.Owner == unit.Owner) continue;

                float dist = Distance(unit, other);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// 计算两点距离
        /// </summary>
        private float Distance(UnitState a, UnitState b)
        {
            float dx = a.PosX - b.PosX;
            float dy = a.PosY - b.PosY;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
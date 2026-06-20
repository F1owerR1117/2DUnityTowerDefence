using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 战斗系统（纯逻辑，无 MonoBehaviour）。
    /// 在 FixedUpdateNetwork 中被调用，操作 UnitBuffer。
    /// </summary>
    public class CombatSystem
    {
        /// <summary>
        /// 模拟一帧战斗
        /// </summary>
        public void Simulate(UnitBuffer units, EventBuffer events, IntentBuffer intents, float deltaTime)
        {
            // 1. 处理事件
            ProcessEvents(units, events);

            // 2. 处理意图
            ProcessIntents(units, intents, deltaTime);

            // 3. 处理单位行为（无意图的单位）
            var read = units.Read;
            var write = units.Write;
            int count = units.Count;

            for (int i = 0; i < count; i++)
            {
                if (read[i].State == UnitStateConstants.Dead) continue;

                // 检查是否有意图
                var intent = intents.FindByUnitId(read[i].UnitId);
                if (intent.Type == IntentType.None)
                {
                    // 无意图：执行默认行为
                    SimulateUnit(read[i], read, write, i, count, deltaTime);
                }
            }
        }

        /// <summary>
        /// 处理意图
        /// </summary>
        private void ProcessIntents(UnitBuffer units, IntentBuffer intents, float deltaTime)
        {
            for (int i = 0; i < intents.Count; i++)
            {
                var intent = intents.Get(i);
                int unitIndex = units.FindIndex(intent.UnitId);

                if (unitIndex == -1) continue;

                var unit = units.Get(unitIndex);

                switch (intent.Type)
                {
                    case IntentType.Idle:
                        unit.State = UnitStateConstants.Idle;
                        break;

                    case IntentType.Move:
                        MoveTowards(ref unit, intent.TargetPosX, intent.TargetPosY, deltaTime);
                        break;

                    case IntentType.Attack:
                        int targetIndex = units.FindIndex(intent.TargetId);
                        if (targetIndex != -1)
                        {
                            var target = units.Get(targetIndex);
                            HandleAttack(ref unit, ref target, deltaTime);
                            units.Set(targetIndex, target);
                        }
                        break;

                    case IntentType.Spawn:
                        // 生成单位（简化：直接添加）
                        break;
                }

                units.Set(unitIndex, unit);
            }
        }

        /// <summary>
        /// 处理事件
        /// </summary>
        private void ProcessEvents(UnitBuffer units, EventBuffer events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events.Get(i);

                switch (evt.Type)
                {
                    case EventType.Damage:
                        ProcessDamageEvent(units, evt);
                        break;
                    case EventType.Heal:
                        ProcessHealEvent(units, evt);
                        break;
                    case EventType.Dash:
                        ProcessDashEvent(units, evt);
                        break;
                }
            }
        }

        /// <summary>
        /// 处理伤害事件
        /// </summary>
        private void ProcessDamageEvent(UnitBuffer units, GameEvent evt)
        {
            int targetIndex = units.FindIndex(evt.TargetId);
            if (targetIndex == -1) return;

            var target = units.Get(targetIndex);
            target.HP -= (int)evt.Value;

            if (target.HP <= 0)
            {
                target.HP = 0;
                target.State = UnitStateConstants.Dead;
            }

            units.Set(targetIndex, target);
        }

        /// <summary>
        /// 处理治疗事件
        /// </summary>
        private void ProcessHealEvent(UnitBuffer units, GameEvent evt)
        {
            int targetIndex = units.FindIndex(evt.TargetId);
            if (targetIndex == -1) return;

            var target = units.Get(targetIndex);
            target.HP += (int)evt.Value;
            if (target.HP > target.MaxHP)
                target.HP = target.MaxHP;

            units.Set(targetIndex, target);
        }

        /// <summary>
        /// 处理冲锋事件（轨迹扫描）
        /// </summary>
        private void ProcessDashEvent(UnitBuffer units, GameEvent evt)
        {
            float startX = evt.PosX;
            float startY = evt.PosY;
            float damage = evt.Value;
            float width = evt.ExtraFloat;

            // 冲锋终点（简化：用 SourceId 方向计算）
            int sourceIndex = units.FindIndex(evt.SourceId);
            if (sourceIndex == -1) return;

            var source = units.Get(sourceIndex);
            float endX = source.PosX;
            float endY = source.PosY;

            // 扫描轨迹上的单位
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units.Get(i);
                if (unit.State == UnitStateConstants.Dead) continue;
                if (unit.Owner == source.Owner) continue; // 跳过友方

                // 检查单位是否在冲锋轨迹上
                if (IsPointOnLineSegment(unit.PosX, unit.PosY, startX, startY, endX, endY, width))
                {
                    // 造成伤害
                    unit.HP -= (int)damage;
                    if (unit.HP <= 0)
                    {
                        unit.HP = 0;
                        unit.State = UnitStateConstants.Dead;
                    }
                    units.Set(i, unit);
                }
            }

            // 更新 Boss 位置到终点
            source.PosX = endX;
            source.PosY = endY;
            units.Set(sourceIndex, source);
        }

        /// <summary>
        /// 检查点是否在线段上（冲锋轨迹检测）
        /// </summary>
        private bool IsPointOnLineSegment(float px, float py, float x1, float y1, float x2, float y2, float width)
        {
            // 计算点到线段的距离
            float dx = x2 - x1;
            float dy = y2 - y1;
            float lengthSq = dx * dx + dy * dy;

            if (lengthSq < 0.0001f) return false;

            float t = Mathf.Clamp01(((px - x1) * dx + (py - y1) * dy) / lengthSq);

            float closestX = x1 + t * dx;
            float closestY = y1 + t * dy;

            float distX = px - closestX;
            float distY = py - closestY;
            float distSq = distX * distX + distY * distY;

            return distSq <= width * width * 0.25f; // width 是直径，半径 = width/2
        }

        private void SimulateUnit(
            UnitState unit,
            UnitState[] read,
            UnitState[] write,
            int selfIndex,
            int totalCount,
            float deltaTime)
        {
            // 1. 索敌
            int targetId = FindTarget(unit, read, totalCount);

            // 2. 根据目标决定行为
            if (targetId == -1)
            {
                write[selfIndex].State = UnitStateConstants.Idle;
                write[selfIndex].TargetId = -1;
                return;
            }

            write[selfIndex].TargetId = targetId;

            // 3. 计算距离
            var target = read[targetId];
            float dist = Distance(unit, target);

            // 4. 攻击或移动
            if (dist <= unit.AttackRange)
            {
                HandleAttack(ref write[selfIndex], ref write[targetId], deltaTime);
            }
            else
            {
                MoveTowards(ref write[selfIndex], target, deltaTime);
            }
        }

        private int FindTarget(UnitState unit, UnitState[] units, int count)
        {
            int bestId = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var other = units[i];
                if (other.State == UnitStateConstants.Dead) continue;
                if (other.Owner == unit.Owner) continue;

                float dist = Distance(unit, other);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = i;
                }
            }
            return bestId;
        }

        private void HandleAttack(ref UnitState attacker, ref UnitState target, float deltaTime)
        {
            attacker.State = UnitStateConstants.Attack;
            attacker.AttackTimer += deltaTime;

            if (attacker.AttackTimer >= 1.0f)
            {
                target.HP -= 10;
                if (target.HP <= 0)
                {
                    target.HP = 0;
                    target.State = UnitStateConstants.Dead;
                }
                attacker.AttackTimer = 0f;
            }
        }

        private void MoveTowards(ref UnitState unit, UnitState target, float deltaTime)
        {
            unit.State = UnitStateConstants.Move;
            float dx = target.PosX - unit.PosX;
            float dy = target.PosY - unit.PosY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist > 0.01f)
            {
                float nx = dx / dist;
                float ny = dy / dist;
                unit.PosX += nx * unit.MoveSpeed * deltaTime;
                unit.PosY += ny * unit.MoveSpeed * deltaTime;
            }
        }

        private void MoveTowards(ref UnitState unit, float targetX, float targetY, float deltaTime)
        {
            unit.State = UnitStateConstants.Move;
            float dx = targetX - unit.PosX;
            float dy = targetY - unit.PosY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist > 0.01f)
            {
                float nx = dx / dist;
                float ny = dy / dist;
                unit.PosX += nx * unit.MoveSpeed * deltaTime;
                unit.PosY += ny * unit.MoveSpeed * deltaTime;
            }
        }

        private float Distance(UnitState a, UnitState b)
        {
            float dx = a.PosX - b.PosX;
            float dy = a.PosY - b.PosY;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
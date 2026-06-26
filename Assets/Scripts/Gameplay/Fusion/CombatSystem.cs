using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 战斗系统（纯逻辑，从零重写）。
    /// 只做 3 件事：FindTarget → AttackTick → ApplyDamage。
    /// 不搬运旧逻辑，不依赖 CardUnit。
    /// </summary>
    public class CombatSystem
    {
        /// <summary>
        /// 模拟一帧战斗（Host Tick 调用）。
        /// </summary>
        public void Simulate(UnitBuffer units, EventBuffer events, float deltaTime)
        {
            var read = units.Read;
            var write = units.Write;
            int count = units.Count;

            for (int i = 0; i < count; i++)
            {
                if (read[i].State == UnitStateConstants.Dead) continue;

                // 1. FindTarget：没有目标时找一个
                if (read[i].TargetId == -1)
                {
                    int target = FindTarget(read[i], read, count);
                    write[i].TargetId = target;
                    if (target != -1)
                        write[i].State = UnitStateConstants.Move;
                    else
                        write[i].State = UnitStateConstants.Idle;
                }

                // 2. MoveTowards：不在攻击范围内时移动
                if (write[i].TargetId != -1 && write[i].State != UnitStateConstants.Dead)
                {
                    int targetIdx = FindUnitIndex(write[i].TargetId, read, count);
                    if (targetIdx != -1)
                    {
                        float dist = Distance(read[i], read[targetIdx]);
                        if (dist > read[i].AttackRange)
                        {
                            MoveTowards(ref write[i], read[targetIdx], deltaTime);
                            write[i].State = UnitStateConstants.Move;
                        }
                        else
                        {
                            // 3. AttackTick：在攻击范围内时攻击
                            write[i].State = UnitStateConstants.Attack;
                            write[i].AttackTimer += deltaTime;
                            if (write[i].AttackTimer >= read[i].AttackSpeed)
                            {
                                write[i].AttackTimer = 0f;
                                ApplyDamage(ref write[targetIdx], read[i].ATK, events, read[i].UnitId, read[targetIdx].UnitId);
                            }
                        }
                    }
                    else
                    {
                        // 目标已死亡，清除
                        write[i].TargetId = -1;
                        write[i].State = UnitStateConstants.Idle;
                    }
                }
            }
        }

        /// <summary>
        /// FindTarget：找最近的敌方单位。
        /// </summary>
        private int FindTarget(UnitState self, UnitState[] units, int count)
        {
            int bestId = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var other = units[i];
                if (other.State == UnitStateConstants.Dead) continue;
                if (other.Owner == self.Owner) continue; // 跳过友方

                float dist = Distance(self, other);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = other.UnitId;
                }
            }
            return bestId;
        }

        /// <summary>
        /// ApplyDamage：扣除 HP，死亡时产出 Death 事件。
        /// </summary>
        private void ApplyDamage(ref UnitState target, int damage, EventBuffer events, int sourceId, int targetId)
        {
            target.HP -= damage;

            // 产出 Hit 事件
            events.AddHit(targetId, sourceId, damage);

            if (target.HP <= 0)
            {
                target.HP = 0;
                target.State = UnitStateConstants.Dead;
                events.AddDeath(targetId, sourceId);
            }
        }

        /// <summary>
        /// MoveTowards：向目标移动。
        /// </summary>
        private void MoveTowards(ref UnitState unit, UnitState target, float deltaTime)
        {
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

        /// <summary>
        /// 计算两点距离。
        /// </summary>
        private float Distance(UnitState a, UnitState b)
        {
            float dx = a.PosX - b.PosX;
            float dy = a.PosY - b.PosY;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 根据 UnitId 查找索引。
        /// </summary>
        private int FindUnitIndex(int unitId, UnitState[] units, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (units[i].UnitId == unitId) return i;
            }
            return -1;
        }
    }
}

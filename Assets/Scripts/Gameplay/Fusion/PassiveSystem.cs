using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 被动系统（纯逻辑，无 MonoBehaviour）。
    /// 在 CombatSystem 之前执行，对 UnitBuffer 进行修饰。
    /// </summary>
    /// <remarks>
    /// ⚠️ [已废弃] 当前未被 FusionGameManager 使用。
    /// 被动逻辑将在 Phase 7 中按需迁移。
    /// </remarks>
    [System.Obsolete("未被 FusionGameManager 使用，被动逻辑将在 Phase 7 中按需迁移")]
    public class PassiveSystem
    {
        /// <summary>
        /// 应用所有被动效果
        /// </summary>
        public void Apply(UnitBuffer read, UnitBuffer write)
        {
            for (int i = 0; i < read.Count; i++)
            {
                var unit = read.Get(i);
                if (unit.State == UnitStateConstants.Dead) continue;

                ApplyPassives(unit, read, write, i);
            }
        }

        private void ApplyPassives(UnitState unit, UnitBuffer read, UnitBuffer write, int selfIndex)
        {
            // 1. 光环效果（对周围单位施加 buff/debuff）
            ApplyAuras(unit, read, write);

            // 2. 自身被动（buff/debuff）
            ApplySelfPassives(ref write, selfIndex);
        }

        /// <summary>
        /// 光环效果：扫描周围单位，施加 buff/debuff
        /// </summary>
        private void ApplyAuras(UnitState unit, UnitBuffer read, UnitBuffer write)
        {
            for (int i = 0; i < read.Count; i++)
            {
                var other = read.Get(i);
                if (other.State == UnitStateConstants.Dead) continue;
                if (other.UnitId == unit.UnitId) continue;

                float dist = Distance(unit, other);

                // 减速光环
                if (unit.Owner != other.Owner && dist <= 3f) // slowRadius
                {
                    // 标记目标被减速（写入 write buffer）
                    // 实际减速逻辑在移动系统中处理
                }

                // 盾墙光环（友军减伤）
                if (unit.Owner == other.Owner && dist <= 3f) // shieldRange
                {
                    // 标记目标获得减伤（写入 write buffer）
                }
            }
        }

        /// <summary>
        /// 自身被动：修改自己的状态
        /// </summary>
        private void ApplySelfPassives(ref UnitBuffer write, int selfIndex)
        {
            var unit = write.Get(selfIndex);

            // 回血被动
            if (unit.HP < unit.MaxHP && unit.HP > 0)
            {
                unit.HP += 1; // 简化：每帧回 1 HP
                if (unit.HP > unit.MaxHP)
                    unit.HP = unit.MaxHP;
            }

            write.Set(selfIndex, unit);
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
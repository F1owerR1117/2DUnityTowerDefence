using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// CardUnit 移动模块（partial class）。
    /// 负责路径跟随、移动阻挡检测、战斗追击等移动相关逻辑。
    /// </summary>
    public partial class CardUnit
    {
        // ─── 朝向 ─────────────────────────────────────

        private UnitFlipper _flipper;

        /// <summary>
        /// 根据移动方向更新兵种朝向（通过 UnitFlipper 翻转 Visual 子物体）。
        /// 需要在预制体的 Visual 子物体上挂载 UnitFlipper 组件。
        /// </summary>
        private void UpdateFacing(Vector3 moveDir)
        {
            if (_flipper == null)
                _flipper = GetComponentInChildren<UnitFlipper>();

            if (moveDir.x > 0.01f && !_facingRight)
            {
                _facingRight = true;
                _flipper?.SetFacingRight(true);
            }
            else if (moveDir.x < -0.01f && _facingRight)
            {
                _facingRight = false;
                _flipper?.SetFacingRight(false);
            }
        }

        // ─── 移动 ─────────────────────────────────────

        /// <summary>
        /// 路径重投影：将当前坐标投影到路径最近的线段上，只允许前进，防止战斗后掉头
        /// </summary>
        private void ResnapToClosestPathDistance()
        {
            ResnapToClosestPathDistance(allowBackward: false);
        }

        /// <summary>
        /// 路径重投影。allowBackward=true 时允许 _pathDistance 后退（用于行军中的实时校准）
        /// </summary>
        private void ResnapToClosestPathDistance(bool allowBackward)
        {
            if (FollowPath == null || FollowPath.waypoints == null || FollowPath.waypoints.Length < 2) return;

            float previousDistance = _pathDistance;
            float minDist = float.MaxValue;
            float bestPathDist = allowBackward ? 0f : previousDistance; // allowBackward 时从 0 开始
            float accumulated = 0f;

            for (int i = 0; i < FollowPath.waypoints.Length - 1; i++)
            {
                Vector3 start = FollowPath.GetPoint(i);
                Vector3 end = FollowPath.GetPoint(i + 1);
                float segLen = Vector3.Distance(start, end);

                float t = Vector3.Dot(transform.position - start, (end - start).normalized) / segLen;
                t = Mathf.Clamp01(t);

                Vector3 closest = Vector3.Lerp(start, end, t);
                float worldDist = Vector3.Distance(transform.position, closest);

                float projectedDist = accumulated + t * segLen;
                if (worldDist < minDist && (allowBackward || projectedDist >= previousDistance))
                {
                    minDist = worldDist;
                    bestPathDist = projectedDist;
                }

                accumulated += segLen;
            }

            _pathDistance = bestPathDist;
        }

        /// <summary>
        /// 朝目标单位的碰撞箱边缘移动（ClosestPoint 寻路）
        /// </summary>
        protected void MoveTowardTarget(CardUnit target)
        {
            if (target == null) return;
            // 射程内有可攻击的敌方 → 立即截停并切换目标，防止追击穿透
            if (_enemyUnits != null)
            {
                CardUnit closest = null;
                float closestDist = float.MaxValue;
                foreach (var enemy in _enemyUnits)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    if (!CanAttackHeight(enemy.UnitHeight)) continue;
                    float d = GetEdgeDistance(enemy);
                    if (d <= Stats.Range && d < closestDist)
                    {
                        closestDist = d;
                        closest = enemy;
                    }
                }
                if (closest != null)
                {
                    Target = closest;
                    return;
                }
            }
            // 朝碰撞箱边缘移动（ClosestPoint），避免穿入建筑中心
            var targetAsBuilding = (IBuildingTarget)target;
            Vector2 targetEdge = targetAsBuilding.BuildingCollider != null
                ? targetAsBuilding.BuildingCollider.ClosestPoint(VisualCenter)
                : (Vector2)target.transform.position;
            Vector2 dir = (targetEdge - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                UpdateFacing(dir);
                Vector3 nextPos = transform.position + (Vector3)(dir * Stats.MoveSpeed * Time.deltaTime);
                if (!IsBlockedAt(nextPos))
                    transform.Translate((Vector3)(dir * Stats.MoveSpeed * Time.deltaTime), Space.World);
            }
        }

        protected void MoveTowardEnemyBase()
        {
            // 射程内有可攻击的敌方 → 禁止行军，切换目标进入战斗
            if (_enemyUnits != null)
            {
                CardUnit closest = null;
                float closestDist = float.MaxValue;
                foreach (var enemy in _enemyUnits)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    if (!CanAttackHeight(enemy.UnitHeight)) continue;
                    float d = GetEdgeDistance(enemy);
                    if (d <= Stats.Range && d < closestDist)
                    {
                        closestDist = d;
                        closest = enemy;
                    }
                }
                if (closest != null)
                {
                    Target = closest;
                    return;
                }
            }

            // 路径行军：每帧先重投影（消除先前误差），再迈向下一步，移动后重投影校准
            if (FollowPath != null && FollowPath.waypoints != null && FollowPath.waypoints.Length >= 2)
            {
                // 1) 从不带后退限制的重投影开始，获得实际位置在路径上的距离
                ResnapToClosestPathDistance(allowBackward: true);

                // 2) 计算目标位置（前方一个步长），钳制 deltaTime 防止卡帧瞬移
                float step = Stats.MoveSpeed * Mathf.Min(Time.deltaTime, 0.05f);
                float targetDist = Mathf.Min(_pathDistance + step, FollowPath.TotalLength);
                Vector3 targetPos = FollowPath.GetPositionAtDistance(targetDist);

                // 3) 向目标位置移动（MoveTowards 保证不会 overshoot），碰撞箱阻挡时停止
                Vector3 moveDir = targetPos - transform.position;
                UpdateFacing(moveDir);
                Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPos, step);
                if (!IsBlockedAt(nextPos))
                    transform.position = nextPos;

                // 4) 移动完成后再次重投影，消除碰撞/物理造成的误差
                ResnapToClosestPathDistance(allowBackward: true);

                if (_pathDistance >= FollowPath.TotalLength)
                {
                    FollowPath = null;
                    _pathDistance = 0f;
                }
                return;
            }

            // 无路径时：朝当前建筑目标移动（由 OnUpdate 动态检测设置）
            if (CurrentTarget != null && !CurrentTarget.IsDestroyed)
            {
                Vector2 edge = CurrentTarget.BuildingCollider != null
                    ? CurrentTarget.BuildingCollider.ClosestPoint(VisualCenter)
                    : (Vector2)CurrentTarget.transform.position;
                Vector2 dir = (edge - (Vector2)transform.position).normalized;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    UpdateFacing(dir);
                    Vector3 nextPos = transform.position + (Vector3)(dir * Stats.MoveSpeed * Time.deltaTime);
                    if (!IsBlockedAt(nextPos))
                        transform.Translate((Vector3)(dir * Stats.MoveSpeed * Time.deltaTime), Space.World);
                }
            }
            // 无路径且无建筑目标 → 原地待命（OnUpdate 会持续扫描附近建筑）
        }

        // ─── 碰撞箱阻挡检测 ─────────────────────────────

        /// <summary>
        /// 检测指定位置是否有敌方碰撞箱重叠（碰撞箱阻挡判定）。
        /// 用于移动前预判，实现"碰撞箱边缘接触即停止"。
        /// </summary>
        private bool IsBlockedAt(Vector3 pos)
        {
            if (_collider is not BoxCollider2D box) return false;
            Vector2 center = (Vector2)pos + box.offset;
            int count = Physics2D.OverlapBox(center, box.size, 0f, _blockFilter, _blockBuffer);
            for (int i = 0; i < count; i++)
            {
                var other = _blockBuffer[i].GetComponentInParent<CardUnit>();
                if (other != null && other != this && other.IsAlive && other.IsLandlord != this.IsLandlord
                    && other.CanBlockHeight(this.BlockableByHeight))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查向目标移动一步是否会被阻挡。
        /// 用于嘲讽源被阻挡时的降级处理。
        /// </summary>
        public bool IsBlockedAtNextPosition(CardUnit target)
        {
            if (target == null) return false;
            Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude < 0.0001f) return false;
            Vector3 nextPos = transform.position + (Vector3)(dir * Stats.MoveSpeed * Time.deltaTime);
            return IsBlockedAt(nextPos);
        }

        // 碰撞箱阻挡检测缓冲区
        private readonly Collider2D[] _blockBuffer = new Collider2D[32];
        private static readonly ContactFilter2D _blockFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        // ─── 路径诊断 ─────────────────────────────────────

        private float _lastMapLogTime = -10f;

        private void MapPathDiagnostics()
        {
            Vector3 targetPos = FollowPath.GetPositionAtDistance(_pathDistance);
            float gap = Vector3.Distance(transform.position, targetPos);

            // 绿线：小兵实际位置
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 10f, Color.green);
            // 红线：路径目标位置
            Debug.DrawLine(targetPos, targetPos + Vector3.up * 10f, Color.red);
            // 黄线：两者偏差
            Debug.DrawLine(transform.position, targetPos, Color.yellow);

            // 偏差 > 3 时每 2 秒输出详细数值到 Console
            if (gap > 3f && Time.unscaledTime - _lastMapLogTime > 2f)
            {
                _lastMapLogTime = Time.unscaledTime;
                Debug.LogWarning(
                    $"[PathDiag] {name} gap={gap:F1}  _pathDistance={_pathDistance:F1}/" +
                    $"{FollowPath.TotalLength:F1}  pos={transform.position}  " +
                    $"target={targetPos}  speed={Stats.MoveSpeed}" +
                    $"  mode={(Target != null ? "combat" : "marching")}"
                );
            }
        }
    }
}

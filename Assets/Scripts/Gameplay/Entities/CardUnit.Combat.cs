using System.Collections;
using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Battle;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// CardUnit 战斗模块（partial class）。
    /// 负责索敌、攻击、伤害计算、嘲讽检测等战斗相关逻辑。
    /// </summary>
    public partial class CardUnit
    {
        // ─── 索敌 ─────────────────────────────────────

        protected virtual void UpdateTarget()
        {
            // 目标已死亡 → 重新索敌
            if (Target == null || !Target.IsAlive)
            {
                var old = Target;
                Target = FindNearestEnemy();
                // target changed
                return;
            }

            // 目标超出追击范围 → 放弃目标
            if (!IsTargetInRange(Target))
            {
                float edgeDist = GetUnitEdgeDistance(Target);
                if (edgeDist > Stats.Range * 3f)
                {
                    Target = FindNearestEnemy();
                }
            }
        }

        /// <summary>由外部组件覆盖索敌逻辑（如骑兵追远程）</summary>
        public System.Func<CardUnit> OverrideFindTarget;

        /// <summary>由外部组件覆盖攻击距离判定（如点杀扩展范围）。返回对指定目标的有效攻击距离。</summary>
        public System.Func<CardUnit, float> OverrideAttackRange;

        /// <summary>
        /// 找到距离本单位最近的敌方嘲讽光环携带者。
        /// 嘲讽生效前提：目标必须同时落在「光环半径」与「我方攻击范围」的交集内。
        /// 多嘲讽冲突时，优先攻击离自己最近的那个。
        /// </summary>
        private readonly Collider2D[] _tauntBuffer = new Collider2D[64];
        private static readonly ContactFilter2D _tauntFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        private CardUnit FindNearestTauntSourceFor(CardUnit self)
        {
            CardUnit best = null;
            float bestDist = float.MaxValue;
            // 扫描范围取所有嘲讽源中最大的光环半径，保证不漏检
            float scanRadius = 0f;
            if (_enemyUnits != null)
            {
                foreach (var e in _enemyUnits)
                    if (e != null && e.IsAlive && e.IsTauntSource && e.TauntRadius > scanRadius)
                        scanRadius = e.TauntRadius;
            }
            if (scanRadius <= 0f) return null;

            int count = Physics2D.OverlapCircle(self.VisualCenter, scanRadius, _tauntFilter, _tauntBuffer);
            for (int i = 0; i < count; i++)
            {
                var unit = _tauntBuffer[i].GetComponentInParent<CardUnit>();
                if (unit == null || !unit.IsAlive || !unit.IsTauntSource) continue;
                if (unit.IsLandlord == self.IsLandlord) continue;
                if (!self.CanAttackHeight(unit.UnitHeight)) continue;

                // 条件1：自己在嘲讽源的光环半径内
                float auraDist = self.GetUnitEdgeDistance(unit);
                if (auraDist > unit.TauntRadius) continue;
                // 条件2：嘲讽源在自己的攻击范围内
                if (!self.IsTargetInRange(unit)) continue;

                if (auraDist < bestDist)
                {
                    bestDist = auraDist;
                    best = unit;
                }
            }
            return best;
        }

        protected virtual CardUnit FindNearestEnemy()
        {
            // 1. 嘲讽优先级最高（仅在攻击范围内生效）
            var tauntTarget = FindNearestTauntSourceFor(this);
            if (tauntTarget != null)
                return tauntTarget;

            // 2. 兵种特化索敌
            if (OverrideFindTarget != null) return OverrideFindTarget();

            // 3. 默认索敌：用边缘距离替代中心距，大单位不会被漏检
            float detectRange = Mathf.Max(Stats.Range * 2f, 5f);
            CardUnit bestSameLane = null;
            float minSame = float.MaxValue;
            CardUnit bestOther = null;
            float minOther = float.MaxValue;

            if (_enemyUnits == null) return null;
            foreach (var enemy in _enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive || enemy._isBuilding || enemy == this) continue;
                if (!CanAttackHeight(enemy.UnitHeight)) continue;

                float edgeDist = GetUnitEdgeDistance(enemy);

                if (edgeDist > detectRange) continue;
                if (enemy._lane == _lane && edgeDist < minSame)
                {
                    minSame = edgeDist;
                    bestSameLane = enemy;
                }
                else if (edgeDist < minOther)
                {
                    minOther = edgeDist;
                    bestOther = enemy;
                }
            }

            var result = bestSameLane ?? bestOther;
            return result;
        }

        /// <summary>
        /// 动态检测范围内最近的敌方建筑。
        /// 兵种沿路线行进时，自动发现沿途的敌方建筑。
        /// </summary>
        private IBuildingTarget FindNearestEnemyBuilding()
        {
            var allTargets = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            IBuildingTarget best = null;
            float bestDist = float.MaxValue;
            float detectRange = Mathf.Max(Stats.Range * 2f, 5f);

            foreach (var unit in allTargets)
            {
                if (unit == null || !unit._isBuilding || !unit.IsAlive) continue;
                if (unit.IsLandlord == IsLandlord) continue;

                float dist = GetEdgeDistance(unit);
                if (dist <= detectRange && dist < bestDist)
                {
                    bestDist = dist;
                    best = unit;
                }
            }
            return best;
        }

        protected bool IsTargetInRange(CardUnit target)
        {
            if (target == null) return false;
            if (!CanAttackHeight(target.UnitHeight)) return false;
            float range = OverrideAttackRange != null ? OverrideAttackRange(target) : Stats.Range;
            return GetUnitEdgeDistance(target) <= range;
        }

        // ─── 攻击 ─────────────────────────────────────

        /// <summary>每次攻击时的额外伤害累积（供被动技能如人海连击使用）</summary>
        [System.NonSerialized] public float _bonusDamage;

        /// <summary>
        /// 开始攻击流程：设置动画速度 → 播放攻击动画 → 记录目标。
        /// 实际伤害由动画打击帧的 OnAttackHitFrame() 触发。
        /// </summary>
        protected virtual void TryAttack(CardUnit target)
        {
            if (_isAttacking) return;
            if (target == null || target == this) return;
            if (target.IsLandlord == IsLandlord) return;

            _isAttacking = true;
            _attackTarget = target;
            _hitCountDealt = 0;
            _animDone = false;
            _projectileSpawned = false;

            float interval = Stats.AttackInterval;
            float clipLen = GetAttackClipLength();
            float speed = clipLen > 0f ? Mathf.Min(clipLen / interval, 4f) : 1f;
            SetAnimSpeed(speed);
            UpdateAnimatorState(2);

            // 伤害由协程在 AttackInterval 秒后精确触发
            if (_hitCoroutine != null) StopCoroutine(_hitCoroutine);
            _hitCoroutine = StartCoroutine(HitFrameCoroutine(interval));
        }

        /// <summary>
        /// 计算本次攻击伤害（含被动加成），消耗 _bonusDamage。
        /// Animation Event 和协程共用，确保伤害一致。
        /// </summary>
        private float ComputeAndConsumePassiveDamage()
        {
            _bonusDamage = 0f;
            OnAttackEvent?.Invoke(_attackTarget);
            OnAttack(_attackTarget);
            float total = CurrentATK + _bonusDamage;
            Debug.Log($"[DmgCalc] {gameObject.name} baseATK={_baseStats.ATK} Stats.ATK={Stats.ATK} CurrentATK={CurrentATK} bonusDmg={_bonusDamage} total={total}");
            return total;
        }

        /// <summary>
        /// 生成弹丸并发射。Animation Event 和协程兜底共用。
        /// </summary>
        private void SpawnProjectile(float damage)
        {
            Vector3 spawnPos = _firePoint != null ? _firePoint.position : transform.position;

            // 有明确的敌方单位目标 → 打单位
            if (_attackTarget != null && _attackTarget.IsAlive && _attackTarget != this)
            {
                if (_attackTarget.IsLandlord == IsLandlord)
                {
                    _hitCountDealt = Stats.HitCount;
                    return;
                }

                var proj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
                proj.Fire(this, _attackTarget, damage, DamageType.Physical);
            }
            // 无单位目标（正在攻击建筑）→ 打建筑
            else if (_attackTarget == null && CurrentTarget != null && !CurrentTarget.IsDestroyed)
            {
                var proj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
                proj.FireAtBuilding(this, CurrentTarget, damage);
            }
            // 目标已死 → 不发射子弹
        }

        /// <summary>
        /// Animation Event 回调：攻击动画打击帧触发。
        /// 近程：直接施加伤害。远程：生成弹丸。
        /// </summary>
        public void OnAttackHitFrame()
        {
            if (_isRanged && _projectilePrefab != null)
            {
                if (_projectileSpawned) return;
                _projectileSpawned = true;
                float totalDmg = ComputeAndConsumePassiveDamage();
                SpawnProjectile(totalDmg);
            }
            else
            {
                DealAttackDamage();
            }

            _hitCountDealt++;
        }

        /// <summary>
        /// 执行近程攻击伤害。由 OnAttackHitFrame 调用。
        /// </summary>
        private void DealAttackDamage()
        {
            float totalDmg = ComputeAndConsumePassiveDamage();

            // 有明确的敌方单位目标 → 打单位
            if (_attackTarget != null && _attackTarget.IsAlive && _attackTarget != this
                && CanAttackHeight(_attackTarget.UnitHeight))
            {
                _attackTarget.LastAttacker = Summoner != null ? Summoner : this;
                _attackTarget.TakeDamage(totalDmg, DamageType.Physical);
            }
            // 无单位目标（正在攻击建筑）→ 打建筑（需在攻击范围内）
            else if (_attackTarget == null && CurrentTarget != null && !CurrentTarget.IsDestroyed
                && GetEdgeDistance(CurrentTarget) <= Stats.Range)
            {
                CurrentTarget.TakeDamage(totalDmg);
            }
            // 目标已死或不在范围内 → 不造成伤害
        }

        /// <summary>
        /// 攻击协程：等待 Animation Event 触发伤害，超时则兜底触发。
        /// </summary>
        private IEnumerator HitFrameCoroutine(float attackInterval)
        {
            // 等待 Animation Event 触发所有打击帧
            float elapsed = 0f;
            while (_hitCountDealt < Stats.HitCount && elapsed < attackInterval)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 兜底：Animation Event 未全部触发时补足
            while (_hitCountDealt < Stats.HitCount)
            {
                OnAttackHitFrame();
            }
        }

        /// <summary>攻击事件（供外部组件监听）</summary>
        public event System.Action<CardUnit> OnAttackEvent;
        /// <summary>受伤事件（参数：伤害值, 伤害类型）。用于音效和分担，不含撕裂加成。</summary>
        public event System.Action<float, DamageType> OnTakeDamageEvent;
        /// <summary>伤害结算完成事件（参数：最终伤害值, 伤害类型）。含撕裂加成，供飘字使用。</summary>
        public event System.Action<float, DamageType> OnDamageCalculated;
        /// <summary>死亡事件</summary>
        public event System.Action OnDeathEvent;

        protected virtual void OnAttack(CardUnit target) { }

        // ─── 批量伤害结算 ─────────────────────────────

        private static bool _batchDamageEnabled;

        /// <summary>启用/禁用批量伤害结算模式</summary>
        public static void SetBatchDamageEnabled(bool enabled) => _batchDamageEnabled = enabled;

        // ─── 受伤害 ───────────────────────────────────

        public virtual void TakeDamage(float rawDamage, DamageType type)
        {
            if (!IsAlive) return;

            // 真实伤害：无视屏障、盾墙减免、伤害减免
            if (type == DamageType.True)
            {
                if (_batchDamageEnabled)
                {
                    DoudizhuTower.Gameplay.Battle.DamageQueue.Enqueue(this, rawDamage);
                    return;
                }
                _currentHP -= rawDamage;
                OnHPChanged?.Invoke(_unitId, _currentHP);
                if (_currentHP <= 0f) { _currentHP = 0f; Die(); }
                return;
            }

            // 屏障层消耗：每层吸收一次攻击的全部伤害
            if (ShieldBlocks > 0)
            {
                ShieldBlocks--;
                return;
            }

            rawDamage = UnitPassives.ApplyShieldWallGlobal(this, rawDamage, type);

            // 伤害减免（重骑兵/铁骑兵冲锋减伤）
            if (DamageReduction > 0f) rawDamage *= (1f - DamageReduction);

            // 伤害吸收（诱饵护盾/帝王盾）
            if (DamageAbsorbRemaining > 0f)
            {
                float absorbed = Mathf.Min(rawDamage, DamageAbsorbRemaining);
                rawDamage -= absorbed;
                DamageAbsorbRemaining -= absorbed;
                if (rawDamage <= 0f) return;
            }

            // 受击音效（撕裂乘算前，使用原始伤害）
            OnTakeDamageEvent?.Invoke(rawDamage, type);

            // 分担伤害已重定向：跳过原伤害扣除（RedistributeDamage 已调用各目标的 TakeDamage）
            if (ShareRedirected) { ShareRedirected = false; return; }

            // B3: 结算撕裂易伤叠加——每层 +5% 受伤
            float tearMultiplier = UnitPassives.GetTearMultiplier(this);
            float finalDamage = rawDamage * tearMultiplier;

            // 飘字显示实际伤害（含撕裂加成）
            OnDamageCalculated?.Invoke(finalDamage, type);

            // 批量模式：入队，帧末统一结算 HP 和死亡
            if (_batchDamageEnabled)
            {
                DoudizhuTower.Gameplay.Battle.DamageQueue.Enqueue(this, finalDamage);
                return;
            }

            _currentHP -= finalDamage;
            OnHPChanged?.Invoke(_unitId, _currentHP);

            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                Die();
            }
        }

        /// <summary>
        /// 批量结算阶段调用：执行实际 HP 扣除和死亡判定。
        /// 由 DamageQueue.ProcessAll() 调用，不应直接调用。
        /// </summary>
        public void ApplyDamage(float finalDamage)
        {
            if (!IsAlive) return;

            _currentHP -= finalDamage;
            OnHPChanged?.Invoke(_unitId, _currentHP);

            if (_currentHP <= 0f)
            {
                _currentHP = 0f;
                Die();
            }
        }

        // ─── 死亡 ─────────────────────────────────────

        public virtual void Die()
        {
            if (_hitCoroutine != null) { StopCoroutine(_hitCoroutine); _hitCoroutine = null; }
            _isAttacking = false;
            _attackTarget = null;
            _hitCountDealt = 0;
            _animDone = false;
            _projectileSpawned = false;
            _justFinishedAttack = false;
            SetAnimSpeed(1f);

            // UnitAudio 会监听 OnDeathEvent 来播放死亡音效
            OnDeathEvent?.Invoke();

            // 通知击杀者（供召唤师等被动使用）
            if (LastAttacker != null && LastAttacker.IsAlive)
                LastAttacker.OnKillEvent?.Invoke(this);

            if (_isBuilding || _isBoss)
                OnDestroyed?.Invoke(this);

            // 播放死亡动画，动画播完后触发 OnDied（回收到对象池）
            StartCoroutine(PlayDeathAnimCoroutine(() =>
            {
                OnDied?.Invoke(_unitId);
            }));
        }
    }
}

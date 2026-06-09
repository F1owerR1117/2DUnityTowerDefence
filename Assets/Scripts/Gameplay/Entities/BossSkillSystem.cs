using System;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// BOSS 技能系统：管理 BOSS 独特技能的触发、施放和效果。
    /// 支持 HP 阶段触发、定时触发、击杀触发。
    /// 施法期间可配置不可选取、清除控制效果。
    /// </summary>
    public class BossSkillSystem : MonoBehaviour
    {
        // ── 枚举 ──

        public enum SkillTrigger { OnHPThreshold, OnTimer, OnKill }
        public enum SkillEffectType { AoeDamage, AoeStun, Heal, Knockback, Buff, Dash }

        // ── 技能定义 ──

        [Serializable]
        public class BossSkill
        {
            [Tooltip("技能名称（仅用于日志）")]
            public string skillName;

            [Tooltip("触发条件")]
            public SkillTrigger trigger;

            [Header("-- HP 阶段触发 --")]
            [Tooltip("HP 百分比阈值（0-1），低于此值时触发")]
            [Range(0f, 1f)]
            public float hpThreshold = 0.5f;

            [Header("-- 定时触发 --")]
            [Tooltip("冷却时间（秒）")]
            public float cooldown = 15f;

            [Header("-- 施法设置 --")]
            [Tooltip("施法持续时间（秒，0=瞬发）")]
            public float castDuration;

            [Tooltip("施法期间是否不可选取")]
            public bool invulnerable;

            [Tooltip("施法时是否清除控制效果")]
            public bool clearCC;

            [Tooltip("施法动画触发名（留空则不播放动画）")]
            public string animTrigger;

            [Header("-- 效果设置 --")]
            [Tooltip("效果类型")]
            public SkillEffectType effectType;

            [Tooltip("效果数值（伤害百分比/治疗量/眩晕时间/击退距离/Buff 倍率）")]
            public float effectValue = 0.5f;

            [Tooltip("效果半径（AOE/击退，冲刺使用碰撞箱宽度）")]
            public float effectRadius = 8f;

            [Header("-- 冲刺专用 --")]
            [Tooltip("冲刺距离")]
            public float dashDistance = 10f;

            [Tooltip("冲刺速度")]
            public float dashSpeed = 20f;

            [Tooltip("施法后多久触发效果（秒）")]
            public float effectDelay;

            [Header("-- 特效/音效 --")]
            [Tooltip("特效预制体（留空则不播放）")]
            public GameObject vfxPrefab;

            [Tooltip("特效持续时间")]
            public float vfxDuration = 1f;

            [Tooltip("音效（留空则不播放）")]
            public AudioClip sfxClip;

            [Tooltip("音效音量")]
            [Range(0f, 1f)]
            public float sfxVolume = 1f;
        }

        // ── Inspector 配置 ──

        [Header("BOSS 技能列表")]
        [Tooltip("按数组顺序检查触发，先触发的优先")]
        [SerializeField] private BossSkill[] skills;

        // ── 运行时状态 ──

        private CardUnit _owner;
        private UnitAudio _unitAudio;
        private UnitVFX _unitVFX;

        private bool[] _phaseTriggered;
        private float[] _cooldownTimers;
        private bool _isCasting;
        private float _castTimer;
        private BossSkill _currentSkill;
        private bool _effectFired;

        // 冲刺状态
        private bool _isDashing;
        private Vector2 _dashDir;
        private float _dashRemaining;
        private float _dashDamage;
        private float _dashWidth;
        private readonly System.Collections.Generic.HashSet<CardUnit> _dashHitTargets = new();

        private static readonly ContactFilter2D _overlapFilter = new() { useTriggers = true, useLayerMask = false };
        private readonly Collider2D[] _overlapBuffer = new Collider2D[64];

        // ── 生命周期 ──

        private void Awake()
        {
            _owner = GetComponentInParent<CardUnit>();
            _unitAudio = GetComponentInChildren<UnitAudio>();
            _unitVFX = GetComponent<UnitVFX>();

            if (skills == null) skills = Array.Empty<BossSkill>();

            _phaseTriggered = new bool[skills.Length];
            _cooldownTimers = new float[skills.Length];

            // 初始化定时技能冷却
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i].trigger == SkillTrigger.OnTimer)
                    _cooldownTimers[i] = skills[i].cooldown;
            }

            // 订阅击杀事件
            if (_owner != null)
                _owner.OnKillEvent += OnKill;
        }

        private void OnDestroy()
        {
            if (_owner != null)
                _owner.OnKillEvent -= OnKill;
        }

        // ── 触发检查 ──

        private bool CheckTrigger(int index)
        {
            var skill = skills[index];

            switch (skill.trigger)
            {
                case SkillTrigger.OnHPThreshold:
                    return !_phaseTriggered[index] && _owner.HPRatio <= skill.hpThreshold;

                case SkillTrigger.OnTimer:
                    return _cooldownTimers[index] <= 0f;

                case SkillTrigger.OnKill:
                    return false; // 由事件触发
            }
            return false;
        }

        private void OnKill(CardUnit victim)
        {
            if (_isCasting) return;

            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i].trigger == SkillTrigger.OnKill && _cooldownTimers[i] <= 0f)
                {
                    StartCast(skills[i], i);
                    break;
                }
            }
        }

        // ── 施法流程 ──

        private void StartCast(BossSkill skill, int index)
        {
            _isCasting = true;
            _currentSkill = skill;
            _castTimer = 0f;
            _effectFired = false;

            // 标记 HP 阶段已触发
            if (skill.trigger == SkillTrigger.OnHPThreshold)
                _phaseTriggered[index] = true;

            // 清除控制效果
            if (skill.clearCC)
                ClearCC();

            // 不可选取
            if (skill.invulnerable)
                _owner.Invulnerable = true;

            // 打断当前攻击
            _owner.InterruptAttack();

            // 播放动画
            if (!string.IsNullOrEmpty(skill.animTrigger))
                _owner.TriggerAnim(skill.animTrigger);

            // 播放音效
            if (skill.sfxClip != null)
                _unitAudio?.PlayClip(skill.sfxClip, skill.sfxVolume);

            // 冲刺类技能立即启动移动
            if (skill.effectType == SkillEffectType.Dash)
            {
                _effectFired = true;
                ExecuteEffect(skill);
            }
            // 瞬发技能：立即触发效果
            else if (skill.castDuration <= 0f)
            {
                ExecuteEffect(skill);
                EndCast(skill, index);
            }
        }

        private void UpdateCast()
        {
            if (_currentSkill == null) { _isCasting = false; return; }

            _castTimer += Time.deltaTime;

            // 延迟触发效果（冲刺类在 StartCast 中已触发）
            if (!_effectFired && _castTimer >= _currentSkill.effectDelay)
            {
                _effectFired = true;
                if (_currentSkill.effectType != SkillEffectType.Dash)
                    ExecuteEffect(_currentSkill);
            }

            // 冲刺移动
            if (_isDashing)
            {
                UpdateDash();
                // 冲刺完成 → 结束施法
                if (_dashRemaining <= 0f)
                {
                    int idx = Array.IndexOf(skills, _currentSkill);
                    EndCast(_currentSkill, idx >= 0 ? idx : 0);
                }
                return;
            }

            // 普通施法结束
            if (_castTimer >= _currentSkill.castDuration)
            {
                int index = Array.IndexOf(skills, _currentSkill);
                EndCast(_currentSkill, index >= 0 ? index : 0);
            }
        }

        private void EndCast(BossSkill skill, int index)
        {
            _isCasting = false;
            _isDashing = false;
            _owner.Invulnerable = false;
            _currentSkill = null;

            // 重置冷却
            if (skill.trigger == SkillTrigger.OnTimer)
                _cooldownTimers[index] = skill.cooldown;
        }

        // ── 效果执行 ──

        private void ExecuteEffect(BossSkill skill)
        {
            switch (skill.effectType)
            {
                case SkillEffectType.AoeDamage:
                    ExecuteAoeDamage(skill);
                    break;
                case SkillEffectType.AoeStun:
                    ExecuteAoeStun(skill);
                    break;
                case SkillEffectType.Heal:
                    ExecuteHeal(skill);
                    break;
                case SkillEffectType.Knockback:
                    ExecuteKnockback(skill);
                    break;
                case SkillEffectType.Buff:
                    ExecuteBuff(skill);
                    break;
                case SkillEffectType.Dash:
                    ExecuteDash(skill);
                    break;
            }

            // 播放特效
            if (skill.vfxPrefab != null)
            {
                VFXManager.Instance?.SpawnVFX(skill.vfxPrefab, _owner.VisualCenter, null, skill.vfxDuration);
            }
        }

        private void ExecuteAoeDamage(BossSkill skill)
        {
            int count = Physics2D.OverlapCircle(_owner.VisualCenter, skill.effectRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy == _owner) continue;
                if (enemy.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;

                float damage = _owner.Stats.ATK * skill.effectValue;
                enemy.TakeDamage(damage, DamageType.Physical);
            }
        }

        private void ExecuteAoeStun(BossSkill skill)
        {
            int count = Physics2D.OverlapCircle(_owner.VisualCenter, skill.effectRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy == _owner) continue;
                if (enemy.IsLandlord == _owner.IsLandlord) continue;
                if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;

                enemy.StunTimer = Mathf.Max(enemy.StunTimer, skill.effectValue);
            }
        }

        private void ExecuteHeal(BossSkill skill)
        {
            float healAmount = _owner.MaxHP * skill.effectValue;
            _owner.Heal(healAmount);
        }

        private void ExecuteKnockback(BossSkill skill)
        {
            int count = Physics2D.OverlapCircle(_owner.VisualCenter, skill.effectRadius, _overlapFilter, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                if (enemy == null || !enemy.IsAlive || enemy == _owner) continue;
                if (enemy.IsLandlord == _owner.IsLandlord) continue;

                Vector2 pushDir = (enemy.VisualCenter - _owner.VisualCenter);
                if (pushDir.sqrMagnitude < 0.001f)
                    pushDir = UnityEngine.Random.insideUnitCircle.normalized;

                enemy.transform.position += (Vector3)(pushDir.normalized * skill.effectValue);
            }
        }

        private void ExecuteBuff(BossSkill skill)
        {
            // ATK 翻倍 buff
            _owner.ApplyBuff("boss_enrage", new CardUnit.StatBuff(atk: skill.effectValue));
        }

        private void ExecuteDash(BossSkill skill)
        {
            // 冲刺方向：朝当前目标方向，无目标则朝移动方向
            Vector2 dir = Vector2.right; // 默认向右
            if (_owner.Target != null)
                dir = ((Vector2)_owner.Target.VisualCenter - _owner.VisualCenter).normalized;
            else if (_owner.FollowPath != null)
            {
                // 朝路径前进方向
                var pathDir = _owner.FollowPath.GetDirectionAtDistance(0);
                if (pathDir.sqrMagnitude > 0.001f)
                    dir = ((Vector2)pathDir).normalized;
            }

            if (dir.sqrMagnitude < 0.001f)
                dir = Vector2.right;

            // 用碰撞箱宽度作为路径扫描宽度
            float colliderWidth = 1f;
            var col = _owner.Collider2D;
            if (col is BoxCollider2D box)
                colliderWidth = Mathf.Max(box.size.x, box.size.y);
            else if (col != null)
                colliderWidth = Mathf.Max(col.bounds.size.x, col.bounds.size.y);

            _isDashing = true;
            _dashDir = dir;
            _dashRemaining = skill.dashDistance;
            _dashDamage = _owner.Stats.ATK * skill.effectValue;
            _dashWidth = colliderWidth;
            _dashHitTargets.Clear();
        }

        // ── 辅助方法 ──

        private void ClearCC()
        {
            _owner.StunTimer = 0f;
            _owner.SlowRestoreTimer = 0f;
            _owner.RemoveBuff("slow_aura");
            _owner.InterruptAttack();
        }

        /// <summary>强制结束施法（死亡时调用，清除 Invulnerable 状态）</summary>
        private void ForceEndCast()
        {
            _isCasting = false;
            _isDashing = false;
            _owner.Invulnerable = false;
            _currentSkill = null;
        }

        // ── 冲刺逻辑 ──

        private void UpdateDash()
        {
            float step = _currentSkill.dashSpeed * Time.deltaTime;
            if (step > _dashRemaining) step = _dashRemaining;

            Vector2 startPos = _owner.VisualCenter;
            _owner.transform.position += (Vector3)(_dashDir * step);
            _dashRemaining -= step;

            Vector2 endPos = _owner.VisualCenter;

            // 沿冲刺路径检测敌人（用多个 OverlapCircle 覆盖路径）
            float dist = Vector2.Distance(startPos, endPos);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / (_dashWidth * 0.5f)));
            for (int s = 0; s <= steps; s++)
            {
                float t = steps > 0 ? (float)s / steps : 0.5f;
                Vector2 checkPos = Vector2.Lerp(startPos, endPos, t);

                int count = Physics2D.OverlapCircle(checkPos, _dashWidth * 0.5f, _overlapFilter, _overlapBuffer);
                for (int i = 0; i < count; i++)
                {
                    var enemy = _overlapBuffer[i].GetComponentInParent<CardUnit>();
                    if (enemy == null || !enemy.IsAlive || enemy == _owner) continue;
                    if (enemy.IsLandlord == _owner.IsLandlord) continue;
                    if (!_owner.CanAttackHeight(enemy.UnitHeight)) continue;
                    if (!_dashHitTargets.Add(enemy)) continue; // 已命中过，跳过

                    enemy.TakeDamage(_dashDamage, DamageType.Physical);
                }
            }
        }

        // ── 冷却更新（在 Update 中调用） ──

        private void UpdateCooldowns()
        {
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i].trigger == SkillTrigger.OnTimer && _cooldownTimers[i] > 0f)
                    _cooldownTimers[i] -= Time.deltaTime;
            }
        }

        private void Update()
        {
            if (_owner == null) return;

            // 死亡时清除施法状态（防止 Invulnerable 残留）
            if (!_owner.IsAlive)
            {
                if (_isCasting) ForceEndCast();
                return;
            }

            UpdateCooldowns();

            if (_isCasting)
            {
                UpdateCast();
                return;
            }

            for (int i = 0; i < skills.Length; i++)
            {
                if (CheckTrigger(i))
                {
                    StartCast(skills[i], i);
                    break;
                }
            }
        }
    }
}

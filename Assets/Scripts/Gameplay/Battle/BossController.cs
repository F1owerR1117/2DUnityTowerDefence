using System;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// BOSS 控制器：管理 BOSS 的触发条件、激活流程和召唤师能力。
    /// 挂载到 BOSS 预制体上，与 CardUnit(_isBoss=true) 同体。
    /// </summary>
    public class BossController : MonoBehaviour
    {
        public enum SpawnTrigger { OnStart, OnTimer, OnBuildingDestroyed }

        [Header("触发条件")]
        [SerializeField] private SpawnTrigger _trigger = SpawnTrigger.OnStart;
        [Tooltip("OnTimer 模式：延迟秒数")]
        [SerializeField] private float _spawnDelay = 60f;
        [Tooltip("OnBuildingDestroyed 模式：监听哪个建筑的死亡")]
        [SerializeField] private CardUnit _triggerBuilding;

        [Header("BOSS 路线")]
        [SerializeField] private RoutePath _bossRoute;
        [Tooltip("玩家主堡到 BOSS 的路线（BOSS 激活后解锁，使玩家可派兵攻打 BOSS）")]
        [SerializeField] private RoutePath _playerRouteToBoss;

        [Header("召唤师能力（需手动挂载 BuildingAI + SpawnPool + RouteGroup）")]
        [Tooltip("启用后 BOSS 会自动出牌生成兵种")]
        [SerializeField] private bool _enableSummoner = true;
        [Tooltip("BOSS 初始金币")]
        [SerializeField] private float _bossInitialGold = 9999f;
        [Tooltip("BOSS 每秒回金速度")]
        [SerializeField] private float _bossIncomeRate = 10f;
        [Tooltip("BOSS 出牌上限")]
        [SerializeField] private int _bossMaxSelection = 6;
        [Tooltip("BOSS 自动摸牌间隔（秒）")]
        [SerializeField] private float _bossDrawInterval = 3f;

        private BattleManager _battleManager;
        private CardDeck _deck;
        private CardUnit _bossUnit;
        private bool _activated;
        private Renderer[] _renderers;
        private Collider2D[] _colliders;
        private UnitHealthBar _healthBar;
        private BossSkillSystem _bossSkillSystem;

        /// <summary>BOSS 是否已激活（供 BattleManager.GetEnemiesFor 检查）</summary>
        public bool IsActive => _activated;

        /// <summary>注入依赖（由 GameBootstrapper 调用）。显示恢复延迟到 ActivateBoss()。</summary>
        public void Inject(BattleManager battleManager, CardDeck deck)
        {
            _battleManager = battleManager;
            _deck = deck;

            // OnStart 模式：注入完成后直接激活（ActivateBoss 内部恢复显示）
            if (_trigger == SpawnTrigger.OnStart)
                ActivateBoss();
        }

        /// <summary>设置触发条件（可覆盖 Inspector 配置）</summary>
        public void SetTrigger(SpawnTrigger trigger, float delay = 0f, CardUnit building = null)
        {
            _trigger = trigger;
            if (delay > 0f) _spawnDelay = delay;
            if (building != null) _triggerBuilding = building;
        }

        private void Awake()
        {
            _bossUnit = GetComponent<CardUnit>();
            if (_bossUnit == null)
            {
                Debug.LogError("[BossController] 缺少 CardUnit 组件");
                return;
            }
            // 确保是 BOSS 型单位（独立于 _isBuilding，走 BOSS 专用注册路径）
            _bossUnit._isBoss = true;

            // 初始隐藏：禁用所有渲染、碰撞和血条，激活时恢复
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            var rendererList = new System.Collections.Generic.List<Renderer>();
            foreach (var r in allRenderers)
            {
                r.enabled = false;
                rendererList.Add(r);
            }
            _renderers = rendererList.ToArray();

            var allColliders = GetComponentsInChildren<Collider2D>(true);
            var colliderList = new System.Collections.Generic.List<Collider2D>();
            foreach (var c in allColliders)
            {
                c.enabled = false;
                colliderList.Add(c);
            }
            _colliders = colliderList.ToArray();

            // 禁用血条
            _healthBar = GetComponentInChildren<UnitHealthBar>(true);
            if (_healthBar != null)
                _healthBar.gameObject.SetActive(false);

            // 禁用技能系统（激活时恢复）
            _bossSkillSystem = GetComponent<BossSkillSystem>();
            if (_bossSkillSystem != null)
                _bossSkillSystem.enabled = false;
        }

        private void Start()
        {
            // 按触发模式订阅（OnStart 已在 Inject() 中处理）
            switch (_trigger)
            {
                case SpawnTrigger.OnTimer:
                    var timerQueue = FindFirstObjectByType<TimerQueue>();
                    if (timerQueue != null)
                        timerQueue.Schedule(_spawnDelay, ActivateBoss);
                    else
                        Invoke(nameof(ActivateBoss), _spawnDelay);
                    break;

                case SpawnTrigger.OnBuildingDestroyed:
                    if (_triggerBuilding != null)
                        _triggerBuilding.OnDestroyed += _ => ActivateBoss();
                    else
                        Debug.LogWarning("[BossController] OnBuildingDestroyed 模式但 _triggerBuilding 为空");
                    break;
            }
        }

        /// <summary>激活 BOSS（可重复调用但只生效一次）</summary>
        /// <summary>BOSS 激活事件（供演出系统监听）</summary>
        public event Action<BossController> OnBossAwakened;

        public void ActivateBoss()
        {
            if (_activated || _battleManager == null || _bossUnit == null) return;
            _activated = true;

            // 广播激活事件（演出系统在此处介入）
            OnBossAwakened?.Invoke(this);

            // 0. 恢复显示、碰撞、血条和技能系统（Inject 时延迟到这里）
            if (_renderers != null)
                foreach (var r in _renderers) if (r != null) r.enabled = true;
            if (_colliders != null)
                foreach (var c in _colliders) if (c != null) c.enabled = true;
            if (_healthBar != null)
                _healthBar.gameObject.SetActive(true);
            if (_bossSkillSystem != null)
                _bossSkillSystem.enabled = true;

            // 1. 解锁 BOSS 相关路线（玩家可手动切换选择）
            if (_bossRoute != null) _bossRoute.Unlock();
            if (_playerRouteToBoss != null) _playerRouteToBoss.Unlock();

            // 2. 激活 BOSS 战斗行为，注入路径/目标
            _battleManager.ActivateBoss(_bossUnit, _bossRoute);

            // 3. 注册召唤师能力
            if (_enableSummoner && _deck != null)
                _battleManager.RegisterBossAsSummoner(_bossUnit, _deck,
                    _bossInitialGold, _bossIncomeRate, _bossMaxSelection, _bossDrawInterval);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Economy;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    public enum WinCondition { DestroyAll, DestroyOne }

    /// <summary>
    /// 战场管理器（partial class，拆分为 3 个文件）。
    /// BattleManager.cs        — 核心字段、初始化、主循环、胜负判定
    /// BattleManager.Spawning.cs — 牌型生成、通用生成、伤害分担
    /// BattleManager.Heroes.cs  — 英雄生成、被动注入、灵骑光环
    /// </summary>
    public partial class BattleManager : MonoBehaviour
    {
        /// <summary>静态实例（供 UnitPassives 等外部组件调用生成方法）</summary>
        public static BattleManager Instance { get; private set; }

        /// <summary>防止胜利/失败重复触发</summary>
        private bool _gameEnded;

        [Header("胜利条件")]
        [SerializeField] private WinCondition _winCondition = WinCondition.DestroyAll;

        // 基地列表由 GameBootstrapper 注入，不再在 Inspector 中重复配置
        private Component[] baseBuildings;

        [Header("工厂")]
        [SerializeField] private UnitFactory unitFactory;

        [Header("英雄")]
        [SerializeField] private CardUnit heroPrefab;
        [SerializeField] private HeroType _selectedHero = HeroType.Blademaster;
        [Tooltip("英雄配置（优先使用，替代硬编码属性）")]
        [SerializeField] private DoudizhuTower.Config.HeroConfig _heroConfig;
        [Tooltip("是否使用预制体的自定义属性（否则使用 HeroConfig/HeroStats 属性）")]
        [SerializeField] private bool _useHeroPrefabStats = true;

        [Header("状态机")]
        [SerializeField] private GameStateMachine _gameStateMachine;

        [Header("暴君光环")]
        [SerializeField] private bool _enableTyrantAura = true;
        [Tooltip("地主兵种 HP 倍率")]
        [SerializeField] private float _tyrantHpMultiplier = 1.2f;
        [Tooltip("地主兵种 ATK 倍率")]
        [SerializeField] private float _tyrantAtkMultiplier = 1.15f;
        [Tooltip("地主击杀额外金币比例")]
        [SerializeField] private float _killGoldBonusPct = 0.1f;

        [Header("顺子加速")]
        [Tooltip("普通顺子（5 张）攻速倍率（>1=更快，所有单位统一）")]
        [SerializeField] private float _straightSpeedBoost = 1.2f;
        [Tooltip("顺子 6+ 攻速倍率")]
        [SerializeField] private float _straight6SpeedBoost = 1.3f;
        [Tooltip("普通顺子（5 张）移速倍率")]
        [SerializeField] private float _straightMoveSpeed = 1.1f;
        [Tooltip("顺子 6+ 移速倍率")]
        [SerializeField] private float _straight6MoveSpeed = 1.15f;

        [Header("合击参数")]
        [Tooltip("对子第二兵额外伤害倍率 (0.5 = +50%)")]
        [SerializeField] private float _jointDamageBonus = 0.5f;

        [Header("分担参数")]
        [Tooltip("三张分担检测范围")]
        [SerializeField] private float _shareRange = 5f;
        [Tooltip("分担：最高血量单位承受比例 (0.6 = 60%)")]
        [SerializeField] private float _shareMainPct = 0.6f;
        [Tooltip("分担：其他单位各承受比例 (0.2 = 20%)")]
        [SerializeField] private float _shareOtherPct = 0.2f;

        private EconomyManager _economyManager;

        // ─── 对局统计访问器 ──────────────────────────────
        public int CardsPlayedCount => _cardsPlayedCount;
        public int UnitsSpawnedCount => _unitsSpawnedCount;
        public int UnitsKilledCount => _unitsKilledCount;
        public float GoldEarnedTotal => _goldEarnedTotal;

        /// <summary>由 EconomyManager 调用，累计本局获得的金币</summary>
        public void TrackGoldEarned(float amount) => _goldEarnedTotal += amount;

        /// <summary>单位生成事件（用于 UI 层挂钩飘字/信息面板等）</summary>
        public event System.Action<CardUnit> OnUnitSpawned;

        /// <summary>游戏结束事件（bool = 玩家是否胜利）</summary>
        public event System.Action<bool> OnGameEnded;

        // ─── 对局统计 ──────────────────────────────────
        private int _cardsPlayedCount;
        private int _unitsSpawnedCount;
        private int _unitsKilledCount;
        private float _goldEarnedTotal;

        private readonly List<CardUnit> _pendingDeaths = new();
        private readonly List<CardUnit> _allUnits = new();
        private readonly Dictionary<int, CardUnit> _unitById = new();
        private int _globalUnitId;
        private IBuildingTarget[] _allBuildingTargets;
        private readonly List<CardUnit> _activeBosses = new();
        private readonly Collider2D[] _overlapCache = new Collider2D[128];

        // ─── 单位注册 ──────────────────────────────────

        private void RegisterUnit(CardUnit unit)
        {
            if (unit != null && !_allUnits.Contains(unit))
            {
                int id = _globalUnitId++;
                unit.SetUnitId(id);
                _allUnits.Add(unit);
                _unitById[id] = unit;
                _unitsSpawnedCount++;
            }
        }

        private void UnregisterUnit(CardUnit unit)
        {
            if (unit != null)
            {
                _allUnits.Remove(unit);
                _unitById.Remove(unit.UnitId);
            }
        }

        // ─── 敌方查询 ──────────────────────────────────

        public List<CardUnit> GetEnemiesFor(CardUnit unit)
        {
            var result = new List<CardUnit>();
            foreach (var other in _allUnits)
            {
                if (other == null || !other.IsAlive || other == unit) continue;
                if (other.IsLandlord != unit.IsLandlord)
                {
                    if (unit.Lane == Lane.None || other.Lane == Lane.None || other.Lane == unit.Lane)
                        result.Add(other);
                }
            }
            return result;
        }

        private static bool IsLandlord(Component c)
        {
            var cu = c.GetComponent<CardUnit>();
            return cu != null && cu.IsLandlord;
        }

        // ─── 初始化 ──────────────────────────────────

        private void Start()
        {
            if (unitFactory == null) unitFactory = GameObject.Find("EntityPool")?.GetComponent<UnitFactory>();
            if (baseBuildings == null || baseBuildings.Length == 0)
            {
                // GameBootstrapper 会在 Initialize 前注入 baseBuildings
                // 如果此处仍为空，说明注入流程异常
                Debug.LogWarning("[BattleManager] baseBuildings 未被注入，GameObject.Find 不查找未激活对象");
            }
        }

        /// <summary>
        /// 注入基地列表（由 GameBootstrapper 调用，替代 Inspector 配置）。
        /// 必须在 Initialize() 之前调用。
        /// </summary>
        public void SetBaseBuildings(Component[] buildings)
        {
            baseBuildings = buildings;
        }

        public void Initialize()
        {
            Instance = this;
            var allCardUnits = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);

            var list = new List<IBuildingTarget>();

            foreach (var u in allCardUnits)
            {
                if (u == null) continue;

                if (u._isBuilding && u._isBoss)
                {
                    Debug.LogWarning($"[BattleManager] {u.name} 同时设置了 _isBuilding 和 _isBoss，优先走 BOSS 路径。请检查预制体配置。");
                    RegisterUnit(u);
                    u.OnDied -= OnUnitDied; u.OnDied += OnUnitDied;
                }
                else if (u._isBuilding)
                {
                    list.Add(u);
                    u.OnDestroyed += OnInstallationDestroyed;
                }
                else if (u._isBoss)
                {
                    RegisterUnit(u);
                    u.OnDied -= OnUnitDied; u.OnDied += OnUnitDied;
                }
                else
                {
                    RegisterUnit(u);
                    u.OnDied -= OnUnitDied; u.OnDied += OnUnitDied;
                }
            }

            _allBuildingTargets = list.ToArray();
        }

        /// <summary>注入经济系统（供暴君税赋使用）</summary>
        public void SetEconomyManager(EconomyManager em) => _economyManager = em;

        // ─── BOSS 系统 ──────────────────────────────────

        public void ActivateBoss(CardUnit boss, RoutePath route)
        {
            if (boss == null) return;

            bool isLandlord = boss.IsLandlord;
            boss.FollowPath = route;
            if (route != null) SnapToPathStart(boss);
            boss.SetEnemyUnits(GetEnemiesFor(boss));
            boss.SyncBaseScale();

            _activeBosses.Add(boss);
            boss.OnDestroyed += _ =>
            {
                _activeBosses.Remove(boss);
                RecheckVictory();
            };

            OnUnitSpawned?.Invoke(boss);
        }

        public void RegisterBossAsSummoner(CardUnit boss, CardDeck deck,
            float initialGold, float incomeRate, int maxSelection, float drawInterval)
        {
            if (boss == null) return;

            var ai = boss.GetComponent<BuildingAI>();
            if (ai == null)
            {
                Debug.LogWarning("[BattleManager] BOSS 缺少 BuildingAI 组件，跳过召唤师注册");
                return;
            }

            if (boss.GetComponent<RouteGroup>() == null)
                Debug.LogWarning("[BattleManager] BOSS 缺少 RouteGroup 组件，召唤师派兵路线将为空");

            var economy = new EconomySystem(initialGold, incomeRate);
            var hand = new CardHand(20);
            ai.Initialize(hand, economy, this, deck, maxSelection, drawInterval);
            Debug.Log($"[BattleManager] RegisterBossAsSummoner: {boss.name}(ID={boss.UnitId}), ai.enabled={ai.enabled}, hand={hand.Count}, gold={economy.CurrentGold}");
        }

        private bool HasAliveEnemyBoss(bool playerIsLandlord)
        {
            foreach (var boss in _activeBosses)
            {
                if (boss != null && boss.IsAlive && boss.IsLandlord != playerIsLandlord)
                    return true;
            }
            return false;
        }

        private void RecheckVictory()
        {
            bool playerIsLandlord = CardUnit.PlayerIsLandlord;
            if (HasAliveEnemyBoss(playerIsLandlord)) return;

            if (_winCondition == WinCondition.DestroyOne)
            {
                if (GetEnemyInstallations(playerIsLandlord).Any(i => i.IsDestroyed))
                    TriggerVictory();
            }
            else if (_winCondition == WinCondition.DestroyAll)
            {
                if (AllEnemyDestroyed(playerIsLandlord))
                    TriggerVictory();
            }
        }

        private bool IsFriendlyInstallation(IBuildingTarget target, bool unitIsLandlord)
        {
            var cu = target.transform.GetComponent<CardUnit>();
            return cu != null && cu.IsLandlord == unitIsLandlord;
        }

        private IEnumerable<IBuildingTarget> GetEnemyInstallations(bool unitIsLandlord)
            => _allBuildingTargets.Where(i => !IsFriendlyInstallation(i, unitIsLandlord));

        private bool AllEnemyDestroyed(bool unitIsLandlord)
        {
            return GetEnemyInstallations(unitIsLandlord).All(i => i.IsDestroyed);
        }

        // ─── 主循环 ───────────────────────────────────

        private void Update()
        {
            foreach (var unit in _allUnits)
                if (unit != null && unit.IsAlive) unit.SetEnemyUnits(GetEnemiesFor(unit));
            CleanupDeadUnits();
        }

        private void OnUnitDied(int unitId)
        {
            if (!_unitById.TryGetValue(unitId, out var unit) || unit == null) return;
            _unitsKilledCount++;
            if (_enableTyrantAura && _economyManager != null
                && unit.LastAttacker != null
                && unit.LastAttacker.IsLandlord == CardUnit.PlayerIsLandlord)
            {
                _economyManager.AddGold(unit.Stats.ATK * _killGoldBonusPct);
            }
            _pendingDeaths.Add(unit);
        }

        private void CleanupDeadUnits()
        {
            if (_pendingDeaths.Count == 0) return;
            foreach (var unit in _pendingDeaths)
            {
                if (unit == null) continue;
                UnregisterUnit(unit);
                unitFactory.Despawn(unit);
            }
            _pendingDeaths.Clear();
        }

        // ─── 基地事件 ─────────────────────────────────

        private void OnInstallationDestroyed(IBuildingTarget target)
        {
            if (_allBuildingTargets == null) return;

            bool playerIsLandlord = CardUnit.PlayerIsLandlord;
            bool targetIsFriendly = IsFriendlyInstallation(target, playerIsLandlord);

            if (targetIsFriendly)
            {
                if (AllFriendlyDestroyed(playerIsLandlord))
                    TriggerDefeat();
                return;
            }

            if (HasAliveEnemyBoss(playerIsLandlord))
                return;

            if (_winCondition == WinCondition.DestroyOne)
                TriggerVictory();
            else if (AllEnemyDestroyed(playerIsLandlord))
                TriggerVictory();
        }

        private void TriggerVictory()
        {
            if (_gameEnded) return;
            _gameEnded = true;
            _gameStateMachine?.TransitionTo(GamePhase.GameOver);
            OnGameEnded?.Invoke(true);
        }

        internal void TriggerDefeat()
        {
            if (_gameEnded) return;
            _gameEnded = true;
            _gameStateMachine?.TransitionTo(GamePhase.GameOver);
            OnGameEnded?.Invoke(false);
        }

        private bool AllFriendlyDestroyed(bool playerIsLandlord)
        {
            return _allBuildingTargets
                .Where(i => i != null && IsFriendlyInstallation(i, playerIsLandlord))
                .All(i => i.IsDestroyed);
        }
    }
}

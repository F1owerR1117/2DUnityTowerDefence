using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Config;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Economy;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.UI.Hand;
using DoudizhuTower.UI.HUD;
using DoudizhuTower.UI.Battlefield;
using DoudizhuTower.UI.Panels;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// ★ 工业级自动装配管线（§4）。
    /// 自底向上完成所有模块的实例化、依赖注入和焊接。
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Config 配置表")]
        [SerializeField] private EconomyConfig economyConfig;

        [Header("Gameplay 系统")]
        [SerializeField] private EconomyManager economyManager;
        [SerializeField] private TimerQueue timerQueue;
        [SerializeField] private GameStateMachine gameStateMachine;
        [SerializeField] private BattleManager battleManager;

        [Header("基地列表 — 拖入所有基地/建筑预制体")]
        [SerializeField] private Component[] baseBuildings;

        [Header("玩家身份与基地选择")]
        [Tooltip("勾选 = 地主，不勾选 = 农民")]
        [SerializeField] private bool _playerIsLandlord = true;
        [Tooltip("选择你要操控的基地（对应 baseBuildings 数组索引）")]
        [SerializeField] private int playerBaseIndex = 0;

        [Header("UI")]
        [SerializeField] private HandArea handArea;
        [SerializeField] private UnityEngine.UI.Button drawButton;
        [SerializeField] private DoudizhuTower.UI.HUD.CardCounterUI cardCounter;
        [SerializeField] private PauseMenu pauseMenu;
        [SerializeField] private VictoryPanel victoryPanel;
        [SerializeField] private DoudizhuTower.UI.HUD.GameTimerUI gameTimerUI;

        [Header("传送飞筒")]
        [SerializeField] private LaunchTubeUI launchTubeUI;
        [SerializeField] private TempSlotUI tempSlotUI;
        [Tooltip("队友的暂存槽（飞筒发送目标）")]
        [SerializeField] private TempSlotUI teammateTempSlotUI;

        [Header("领域系统")]
        [SerializeField] private DomainSystem domainSystem;
        [SerializeField] private DoudizhuTower.UI.Battlefield.DomainUIController domainUIController;
        [SerializeField] private UnityEngine.UI.Button domainButton;
        [SerializeField] private GameObject laneArea;

        // Core 层实例
        private CardDeck _mainDeck;
        private EconomySystem _economyLogic;
        private bool _isNetworkMode;

        // 事件处理器引用（用于取消订阅）
        private Action<Card[], CardTypeResult, RouteGroup> _onPlayRequestHandler;
        private HandArea _playerHandArea;
        private PauseMenu _wiredPauseMenu;
        private VictoryPanel _wiredVictoryPanel;
        private Action _wiredTimeUpHandler;
        private Action<bool> _wiredGameEndedHandler;

        public CardDeck MainDeck => _mainDeck;
        public EconomySystem EconomyLogic => _economyLogic;

        private void Awake()
        {
            // 读取叫分结果（如果有），否则使用 Inspector 默认值
            if (GameSession.HasResult)
            {
                _playerIsLandlord = GameSession.PlayerIsLandlord;
                playerBaseIndex = GameSession.MyBaseIndex;
            }

            // 必须在 Awake 中设置，保证在所有 UnitHealthBar.Start() 之前生效
            CardUnit.PlayerIsLandlord = _playerIsLandlord;
        }

        private IEnumerator Start()
        {
            // ── Step 0: 确保 UI_Scene 已加载 ────────
            yield return UIManager.WaitForReady();

            // ── Step 0b: 加载存档 ─────────────────
            SaveSystem.Load();

            // ── Step 1: 加载 Config ──────────────────
            var econConfig = economyConfig != null
                ? economyConfig
                : ScriptableObject.CreateInstance<EconomyConfig>();

            // ── Step 2: 实例化 Core 层 ──────────────
            _isNetworkMode = GameSession.IsNetworkMode;
            int seed = _isNetworkMode ? GameSession.NetworkSeed : System.Environment.TickCount;
            _mainDeck = new CardDeck(seed);

            // 启用批量伤害结算：同帧攻击统一结算，消除 Update 执行顺序对战斗结果的影响
            CardUnit.SetBatchDamageEnabled(true);
            bool playerIsLandlord = _playerIsLandlord;

            float incomeRate = econConfig.farmerBaseIncome;
            if (playerIsLandlord)
                incomeRate += econConfig.landlordBonusIncome;
            _economyLogic = new EconomySystem(econConfig.initialGold, incomeRate);

            // ── Step 3: 发初始手牌 ──────────────────
            int handCapacity = playerIsLandlord ? 20 : 17;
            var playerHand = new CardHand(handCapacity);
            _mainDeck.Deal(7, playerHand);

            // 为每个 AI 基地创建独立手牌
            var playerBaseRef = GetPlayerBase();
            var aiHands = new Dictionary<Component, CardHand>();
            if (!_isNetworkMode)
            {
                // 单机模式：非玩家操控的基地 → AI
                foreach (var baseBldg in baseBuildings)
                {
                    if (baseBldg == null) continue;
                    var cu = baseBldg.GetComponent<CardUnit>();
                    if (cu == null) continue;
                    bool isPlayerBase = (baseBldg == playerBaseRef);
                    if (!isPlayerBase && baseBldg.GetComponent<BuildingAI>() != null)
                    {
                        var aiHand = new CardHand(17);
                        _mainDeck.Deal(7, aiHand);
                        aiHands[baseBldg] = aiHand;
                    }
                }
            }
            else
            {
                // 联机模式：为 AI 槽位的基地创建 AI 手牌
                foreach (var aiSlot in GameSession.AISlots)
                {
                    if (aiSlot < 0 || aiSlot >= GameSession.PlayerBaseMapping.Length) continue;
                    int baseIdx = GameSession.PlayerBaseMapping[aiSlot];
                    if (baseIdx < 0 || baseIdx >= baseBuildings.Length) continue;
                    var baseBldg = baseBuildings[baseIdx];
                    if (baseBldg == null) continue;
                    var aiHand = new CardHand(17);
                    _mainDeck.Deal(7, aiHand);
                    aiHands[baseBldg] = aiHand;
                }
            }

            // ── Step 4: 初始化建筑 CardUnit ──
            foreach (var baseBldg in baseBuildings)
            {
                if (baseBldg == null) continue;
                var buildCU = baseBldg.GetComponent<CardUnit>();
                if (buildCU != null && buildCU._isBuilding)
                {
                    buildCU.InitBuildingHP(buildCU.MaxHP > 0 ? buildCU.MaxHP : 1000f);
                }
            }

            // ── Step 5: 依赖注入焊接 ────────────────
            if (economyManager != null)
                economyManager.Initialize(_economyLogic, timerQueue);

            if (battleManager != null)
            {
                battleManager.SetBaseBuildings(baseBuildings);
                battleManager.Initialize();
            }

            if (battleManager != null && economyManager != null)
                battleManager.SetEconomyManager(economyManager);

            // ── Step 5b: BOSS 控制器注入 ────────────────
            if (battleManager != null)
            {
                var bossControllers = FindObjectsByType<BossController>(FindObjectsSortMode.None);
                foreach (var boss in bossControllers)
                {
                    boss.Inject(battleManager, _mainDeck);
                }
            }

            // ── Step 5a: AI 对手初始化 ───
            foreach (var kvp in aiHands)
            {
                var aiCU = kvp.Key.GetComponent<CardUnit>();
                var identity = aiCU != null && aiCU.IsLandlord ? Identity.Landlord : Identity.FarmerA;
                InjectBuildingAI(kvp.Key, kvp.Value, econConfig, battleManager, identity);
            }

            // ── Step 6: 焊接 UI ─────────────────────
            if (handArea == null)
            {
                Debug.LogError("[Bootstrapper] handArea 未在 Inspector 中赋值！游戏无法正常进行");
                yield break;
            }

            {
                var playerBase = GetPlayerBase();
                var playerRouteGroup = playerBase?.GetComponent<RouteGroup>();
                int maxPlay = playerIsLandlord ? 6 : 5;
                handArea.Initialize(playerHand, maxPlay, playerRouteGroup);
                handArea.SetDeck(_mainDeck);
                if (cardCounter != null) { cardCounter.SetDeck(_mainDeck); cardCounter.Refresh(); }

                // 保存引用以便取消订阅
                _playerHandArea = handArea;
                _playerIsLandlord = playerIsLandlord;

                // 使用命名方法替代匿名 lambda
                _onPlayRequestHandler = (cards, result, routeGroup) =>
                {
                    float cost = CardCostCalculator.CalculateTotalCost(cards, result);

                    if (economyManager != null && !economyManager.TrySpendGold(cost))
                    {
                        Debug.LogWarning($"[Bootstrapper] 金币不足！需要 {cost}, 当前 {economyManager.CurrentGold}");
                        domainSystem?.CancelPending();
                        return;
                    }

                    playerHand.RemoveRange(cards);
                    _mainDeck.Discard(cards);
                    if (cardCounter != null) cardCounter.Refresh();
                    battleManager?.DeployCards(cards, result, routeGroup, playerBase);

                    // 触发领域系统
                    if (domainSystem != null)
                    {
                        domainSystem.OnCardPlayed(result, true);
                    }
                    else
                    {
                        Debug.LogWarning("[出牌] domainSystem 为 null，无法触发领域系统");
                    }
                };

                handArea.OnPlayRequest += _onPlayRequestHandler;

                // 领域封印出牌校验：被封印方只能打出能管上领域牌型的牌
                if (domainSystem != null)
                {
                    handArea.PlayValidator = (playResult) =>
                    {
                        if (!domainSystem.IsDomainActive) return true;
                        if (!domainSystem.IsSealedByDomain(playerIsLandlord)) return true;
                        // 炸弹/王炸：直接破封
                        if (playResult.Type == CardType.Bomb || playResult.Type == CardType.DoubleKingBomb)
                            return true;
                        // 能管上的牌：正常出牌（不破封，除非点了反击按钮激活护盾）
                        if (CardTypeCompare.CanCounter(domainSystem.CurrentDomainType, playResult))
                            return true;
                        // 不能管上的牌：封印，不允许出
                        return false;
                    };
                }

                // ── Step 6b: 焊接传送飞筒 + 暂存槽 ────────
                if (_isNetworkMode)
                {
                    // 联机模式：暂不支持飞筒同步，隐藏相关 UI
                    launchTubeUI?.gameObject.SetActive(false);
                    tempSlotUI?.gameObject.SetActive(false);
                    teammateTempSlotUI?.gameObject.SetActive(false);
                }
                else
                {
                    // 单机模式：查找友方 AI（队友）及其手牌
                    CardHand teammateHand = null;
                    Component teammateBase = null;
                    foreach (var kvp in aiHands)
                    {
                        var aiCU = kvp.Key.GetComponent<CardUnit>();
                        bool aiIsLandlord = aiCU != null && aiCU.IsLandlord;
                        if (aiIsLandlord == playerIsLandlord)
                        {
                            teammateHand = kvp.Value;
                            teammateBase = kvp.Key;
                            break;
                        }
                    }

                    // 初始化队友暂存槽并注入到队友 AI
                    if (teammateTempSlotUI != null && teammateHand != null)
                    {
                        teammateTempSlotUI.Initialize(_mainDeck, null, teammateHand);

                        if (teammateBase != null)
                        {
                            var teammateAI = teammateBase.GetComponent<BuildingAI>();
                            if (teammateAI != null)
                                teammateAI.SetTempSlot(teammateTempSlotUI);
                        }
                    }

                    if (launchTubeUI != null)
                    {
                        launchTubeUI.Initialize(handArea);

                        // 飞筒检查的是队友暂存槽（有牌时拒绝传送）
                        if (teammateTempSlotUI != null)
                            launchTubeUI.SetTempSlot(teammateTempSlotUI);

                        launchTubeUI.OnCardTransmitted += (card) =>
                        {
                            // 从玩家手牌移除
                            playerHand.Remove(card);
                            if (cardCounter != null) cardCounter.Refresh();
                            handArea.NotifyHandChanged();

                            // 牌进入队友暂存槽
                            if (teammateTempSlotUI != null)
                            {
                                teammateTempSlotUI.ReceiveCard(card);
                                Debug.Log($"[飞筒] 已传送 {card} 到队友暂存槽");
                            }
                            else
                            {
                                _mainDeck.Discard(card);
                                Debug.Log($"[飞筒] 无队友暂存槽，牌进入弃牌堆: {card}");
                            }
                        };
                    }

                    if (tempSlotUI != null)
                    {
                        tempSlotUI.Initialize(_mainDeck, handArea, playerHand);

                        // 监听基地摧毁 → 清空暂存槽
                        if (baseBuildings != null)
                        {
                            foreach (var bldg in baseBuildings)
                            {
                                if (bldg == null) continue;
                                var cu = bldg.GetComponent<CardUnit>();
                                if (cu != null && cu._isBuilding)
                                {
                                    bool isPlayerBase = bldg == GetPlayerBase();
                                    bool isTeammateBase = bldg == teammateBase;
                                    cu.OnDestroyed += (_) =>
                                    {
                                        if (isPlayerBase)
                                        {
                                            tempSlotUI?.Clear();
                                            if (launchTubeUI != null)
                                                launchTubeUI.SetLocked(true);
                                        }
                                        if (isTeammateBase)
                                        {
                                            teammateTempSlotUI?.Clear();
                                            if (launchTubeUI != null)
                                                launchTubeUI.SetLocked(true);
                                        }
                                    };
                                }
                            }
                        }
                    }

                    // 根据身份隐藏 UI
                    if (playerIsLandlord) {
                        launchTubeUI?.gameObject.SetActive(false);
                        tempSlotUI?.gameObject.SetActive(false);
                        teammateTempSlotUI?.gameObject.SetActive(false);
                    } else {
                        laneArea?.SetActive(false);
                    }
                }
            }

            // ── Step 7: 基地血条使用 UnitHealthBar（与兵种共用） ──

            // ── Step 8: 焊接摸牌按钮 ────────────────
            if (drawButton != null)
            {
                float drawCost = playerIsLandlord ? 10f : 12f;
                float drawInterval = playerIsLandlord ? 5f : 6f;

                timerQueue?.ScheduleLoop(drawInterval, () =>
                {
                    if (playerHand == null || playerHand.IsFull || _mainDeck == null) return;
                    var card = _mainDeck.Draw();
                    playerHand.Add(card);
                    if (cardCounter != null) cardCounter.Refresh();
                    // 通知手牌变化（用于领域系统检查新牌封印状态）
                    handArea?.NotifyHandChanged();
                    AudioManager.Instance?.PlayDrawCard();
                });

                drawButton.onClick.AddListener(() =>
                {
                    if (playerHand.IsFull) return;
                    if (economyManager == null || !economyManager.TrySpendGold(drawCost)) return;

                    var card = _mainDeck.Draw();
                    playerHand.Add(card);
                    if (cardCounter != null) cardCounter.Refresh();
                    handArea?.NotifyHandChanged();
                    AudioManager.Instance?.PlayDrawCard();
                });
            }

            // ── Step 9: 焊接暂停菜单事件 ────────────
            if (pauseMenu == null) pauseMenu = UIManager.Instance?.PauseMenu;
            if (pauseMenu != null)
            {
                pauseMenu.OnRestartRequested += SceneLoader.RestartGame;
                pauseMenu.OnQuitRequested += SceneLoader.LoadMainMenu;
                _wiredPauseMenu = pauseMenu;
            }

            // ── Step 9b: 焊接胜利面板 ───────────────
            if (victoryPanel == null) victoryPanel = UIManager.Instance?.VictoryPanel;
            victoryPanel?.ResetForNewGame();
            PauseMenu.IsGameOver = false;
            if (victoryPanel != null && battleManager != null)
            {
                Action<bool> onGameEnded = (playerWon) =>
                {
                    PauseMenu.IsGameOver = true;
                    gameStateMachine?.StopTimer();
                    var stats = CollectVictoryStats();
                    victoryPanel.Show(playerWon, false, stats);
                    // 保存存档
                    SaveSystem.OnGameEnded(playerWon, stats.goldEarned);
                };
                battleManager.OnGameEnded += onGameEnded;
                _wiredGameEndedHandler = onGameEnded;
                victoryPanel.OnRestartRequested += SceneLoader.RestartGame;
                victoryPanel.OnReturnToMenuRequested += SceneLoader.LoadMainMenu;
                victoryPanel.OnNextLevelRequested += SceneLoader.LoadNextLevel;
                _wiredVictoryPanel = victoryPanel;
            }

            // ── Step 9d: 焊接计时器 ───────────────────
            if (gameTimerUI != null && gameStateMachine != null)
            {
                gameTimerUI.Initialize(gameStateMachine);
            }
            if (gameStateMachine != null && battleManager != null)
            {
                Action onTimeUp = () =>
                {
                    if (PauseMenu.IsGameOver) return;
                    battleManager.TriggerDefeat();
                };
                gameStateMachine.OnTimeUp += onTimeUp;
                _wiredTimeUpHandler = onTimeUp;
            }

            // ── Step 9c: 焊接金币追踪 ────────────────
            if (economyManager != null && battleManager != null)
            {
                economyManager.OnGoldEarned += (amount) => battleManager.TrackGoldEarned(amount);
            }

            // ── Step 10: 初始化领域系统 ───────────────
            if (domainSystem != null)
            {
                domainSystem.Initialize(handArea, null, playerIsLandlord);

                if (!_isNetworkMode)
                {
                    // 单机模式：设置 AI 手牌引用（按阵营区分敌方/友方）
                    CardHand firstEnemyHand = null;
                    foreach (var kvp in aiHands)
                    {
                        var aiCU = kvp.Key.GetComponent<CardUnit>();
                        bool aiIsLandlord = aiCU != null && aiCU.IsLandlord;
                        bool isEnemyAI = aiIsLandlord != playerIsLandlord;
                        if (isEnemyAI)
                        {
                            if (firstEnemyHand == null) firstEnemyHand = kvp.Value;
                            else domainSystem.AddEnemyCardHand(kvp.Value);
                        }
                        else
                        {
                            domainSystem.AddFriendlyCardHand(kvp.Value);
                        }
                    }
                    domainSystem.SetCardHands(playerHand, firstEnemyHand);
                }
                else
                {
                    // 联机模式：领域系统暂不对手牌引用（Phase 2 扩展）
                    domainSystem.SetCardHands(playerHand, null);
                }

                // 地主领域按钮 - 点击后标记待激活，出牌后生效
                if (domainButton != null)
                {
                    domainButton.gameObject.SetActive(playerIsLandlord);
                    domainButton.onClick.AddListener(() =>
                    {
                        if (domainSystem.IsDomainPending)
                        {
                            domainSystem.CancelDomainPending();
                        }
                        else
                        {
                            domainSystem.SetDomainPending();
                            if (!domainSystem.IsDomainPending)
                                Debug.LogWarning("[DomainButton] 无法标记领域待激活（可能冷却中或反制护盾生效中）");
                        }
                    });
                }

                // 初始化领域 UI（覆盖层 + 按钮状态 + 冷却效果统一管理）
                if (domainUIController == null)
                    domainUIController = FindFirstObjectByType<DoudizhuTower.UI.Battlefield.DomainUIController>();
                if (domainUIController != null)
                {
                    domainUIController.Initialize(domainSystem, playerIsLandlord);
                }
                else
                {
                    Debug.LogWarning("[Bootstrapper] DomainUIController 未在 Inspector 中赋值且场景中未找到");
                }
            }

        }

        private void OnDestroy()
        {
            // 取消订阅事件，防止内存泄漏和事件累积
            if (_playerHandArea != null)
                _playerHandArea.PlayValidator = null;
            if (_playerHandArea != null && _onPlayRequestHandler != null)
            {
                _playerHandArea.OnPlayRequest -= _onPlayRequestHandler;
                _onPlayRequestHandler = null;
            }
            if (_wiredPauseMenu != null)
            {
                _wiredPauseMenu.OnRestartRequested -= SceneLoader.RestartGame;
                _wiredPauseMenu.OnQuitRequested -= SceneLoader.LoadMainMenu;
                _wiredPauseMenu = null;
            }
            if (_wiredVictoryPanel != null)
            {
                _wiredVictoryPanel.OnRestartRequested -= SceneLoader.RestartGame;
                _wiredVictoryPanel.OnReturnToMenuRequested -= SceneLoader.LoadMainMenu;
                _wiredVictoryPanel.OnNextLevelRequested -= SceneLoader.LoadNextLevel;
                _wiredVictoryPanel = null;
            }
            if (_wiredGameEndedHandler != null && battleManager != null)
            {
                battleManager.OnGameEnded -= _wiredGameEndedHandler;
                _wiredGameEndedHandler = null;
            }
            if (_wiredTimeUpHandler != null && gameStateMachine != null)
            {
                gameStateMachine.OnTimeUp -= _wiredTimeUpHandler;
                _wiredTimeUpHandler = null;
            }
        }

        /// <summary>获取玩家操控的基地（由 playerBaseIndex 指定）</summary>
        private Component GetPlayerBase()
        {
            if (baseBuildings != null && playerBaseIndex >= 0 && playerBaseIndex < baseBuildings.Length)
                return baseBuildings[playerBaseIndex];
            return null;
        }

        private void InjectBuildingAI(Component baseCtrl, CardHand hand, EconomyConfig econConfig, BattleManager bm, Identity identity)
        {
            if (baseCtrl == null) return;
            var ai = baseCtrl.GetComponent<BuildingAI>();
            if (ai == null) return;

            float incomeRate = econConfig.farmerBaseIncome;
            if (identity == Identity.Landlord)
                incomeRate += econConfig.landlordBonusIncome;

            int maxSelection = identity == Identity.Landlord ? 6 : 5;
            float drawInterval = identity == Identity.Landlord ? 5f : 6f;
            var economy = new EconomySystem(econConfig.initialGold, incomeRate);

            ai.Initialize(hand, economy, bm, _mainDeck, maxSelection, drawInterval);
        }

        private VictoryStats CollectVictoryStats()
        {
            float duration = gameStateMachine != null ? gameStateMachine.ElapsedTime : 0f;

            // 完胜判定：玩家基地满血 → 1.5 倍，否则 1.0 倍
            float coefficient = 1f;
            var playerBaseComp = GetPlayerBase();
            if (playerBaseComp != null)
            {
                var cu = playerBaseComp.GetComponent<CardUnit>();
                if (cu != null && cu.IsAlive && cu.HPRatio >= 1f)
                    coefficient = 1.5f;
            }

            return new VictoryStats
            {
                gameDuration = duration,
                cardsPlayed = battleManager != null ? battleManager.CardsPlayedCount : 0,
                unitsSpawned = battleManager != null ? battleManager.UnitsSpawnedCount : 0,
                unitsKilled = battleManager != null ? battleManager.UnitsKilledCount : 0,
                goldEarned = battleManager != null ? battleManager.GoldEarnedTotal : 0f,
                identityBaseScore = _playerIsLandlord ? 100 : 50,
                bidMultiplier = GameSession.HasResult ? GameSession.BidMultiplier : 1f,
                gameStateCoefficient = coefficient,
            };
        }
    }
}

using System;
using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Economy;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Systems;
using DoudizhuTower.UI.Battlefield;
using DoudizhuTower.UI.Hand;
using DoudizhuTower.UI.HUD;
using Photon.Pun;
using UnityEngine;
using AudioManager = DoudizhuTower.Gameplay.Systems.AudioManager;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 联机游戏管理器。
    /// Event + Snapshot + Tick 三层确定性模型 v2.0（Final Lock）。
    /// Master 唯一权威：所有游戏逻辑由 Master 验证后广播。
    /// Client 为纯投影层：Event 仅缓存，Snapshot 授权执行。
    /// 挂载到游戏场景的 GameObject 上。
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        // ─── 调试工具 ───
        private NetworkLogger _logger;
        private NetworkDebugPanel _debugPanel;

        // ─── 注入引用（由 GameBootstrapper 调用 Initialize） ───
        private INetworkService _net;
        private BattleManager _battleManager;
        private EconomyManager _economyManager;
        private DomainSystem _domainSystem;
        private CardDeck _deck;
        /// <summary>共享池剩余。唯一真相源为 _deck.Remaining。</summary>
        private int _sharedPoolRemaining
        {
            get => _deck?.Remaining ?? 54;
            set { /* 禁止直接写入，唯一写入点为 _deck._cursor */ }
        }
        private int _currentDeckId;
        private bool _gameStarted;
        private float _reconcileTimer;
        private const float RECONCILE_INTERVAL = 2f;

        // ─── v2.0 生命周期阶段（Frozen Architecture） ───
        public enum GameSyncPhase
        {
            INIT,       // 初始化网络、注册事件
            SYNC,       // 仅接受 Snapshot，拒绝所有 Event
            RUN,        // Event 正常处理
            RECONCILE,  // 定期 Snapshot 校正
            END,        // 游戏结束
            RESET       // 清除所有运行态
        }
        private GameSyncPhase _phase = GameSyncPhase.INIT;

        // ─── v2.0 旧状态机兼容（已废弃，仅保留 Transition 逻辑） ───
        private enum SyncState { Pending, Signaling, Applying, Synchronized }
        private SyncState _syncState = SyncState.Pending;
        private readonly Queue<(string key, object value, int sender)> _eventBuffer = new();
        private float _snapshotPollTimer;
        private const float SNAPSHOT_POLL_INTERVAL = 0.2f;
        private const string SNAPSHOT_KEY = "GameSnapshot";
        private HandArea _handArea;
        private CardCounterUI _cardCounter;
        /// <summary>本机玩家手牌。唯一真相源为 _slotHands[_mySlot]。</summary>
        private CardHand _playerHand => _slotHands.ContainsKey(_mySlot) ? _slotHands[_mySlot] : null;
        private Component _playerBase;
        private bool _playerIsLandlord;

        // 槽位映射
        private int _mySlot;
        private int[] _actorNumbers;
        private Component[] _baseBuildings;

        // 每个槽位的手牌（Master 维护所有，Client 只维护自己的）
        private Dictionary<int, CardHand> _slotHands = new();
        // 每个远程玩家的独立牌堆（Master 端，用于同步摸牌）
        private Dictionary<int, DoudizhuTower.Core.Cards.CardDeck> _slotDecks = new();

        // 每个槽位的经济（Master 维护所有）
        private Dictionary<int, EconomySystem> _slotEconomies = new();
        // 已收到 PLAYER_READY 的槽位集合（防止摸牌先于 PLAYER_READY 到达导致手牌不同步）
        private readonly HashSet<int> _playerReadyReceived = new();

        // ─── Tick 层：Master 单调递增逻辑时钟 ───
        private int _tick;
        private int _lastReceivedTick;

        private bool _simulatesCombat;

        // 游戏状态机（用于时间同步）
        private GameStateMachine _gameStateMachine;

        /// <summary>本机槽位索引（供 GameBootstrapper 读取）</summary>
        public int MySlot => _mySlot;

        /// <summary>联机游戏结束事件（客户端收到 GAME_END 广播时触发，参数=本机是否胜利）</summary>
        public event Action<bool> OnNetworkGameEnd;

        /// <summary>注册 AI 槽位的经济系统（由 GameBootstrapper 调用）</summary>
        public void RegisterSlotEconomy(int slot, EconomySystem economy)
        {
            if (economy != null)
                _slotEconomies[slot] = economy;
        }

        /// <summary>注册槽位手牌（由 GameBootstrapper 调用，Master 端需要所有玩家手牌）</summary>
        public void RegisterSlotHand(int slot, CardHand hand)
        {
            if (hand != null)
                _slotHands[slot] = hand;
        }

        // 网络追踪日志序号（排查同步问题用）
        private int _traceSeq;
        private void Trace(string msg, int slot = -1)
        {
            string role = _net.IsMasterClient ? "M" : "C";
            string slotStr = slot >= 0 ? $" slot={slot}" : "";
            Debug.Log($"[NET][{role}][{_traceSeq++}][{msg}]{slotStr}");
        }

        private bool _initialized;

        public void Initialize(
            INetworkService net,
            BattleManager battleManager,
            EconomyManager economyManager,
            DomainSystem domainSystem,
            CardDeck deck,
            CardHand playerHand,
            HandArea handArea,
            CardCounterUI cardCounter,
            Component playerBase,
            bool playerIsLandlord,
            Component[] baseBuildings)
        {
            _net = net;
            _battleManager = battleManager;
            _economyManager = economyManager;
            _domainSystem = domainSystem;
            _deck = deck;
            // 联机模式必须后台运行，否则窗口失焦时 Update 暂停导致模拟分叉
            Application.runInBackground = true;
            // Master Authority Combat：Client 不参与战斗模拟
            _simulatesCombat = _net.IsMasterClient;
            CardUnit.SimulatesCombatDefault = _net.IsMasterClient;
            // Master 广播单位死亡事件
            if (_net.IsMasterClient && _battleManager != null)
                _battleManager.OnUnitDiedEvent += BroadcastUnitDied;
            // 每次生成单位时设置 SimulatesCombat（解决本地多玩家 static 冲突）
            if (_battleManager != null)
                _battleManager.OnUnitSpawned += OnUnitSpawned_SetCombatMode;
            _handArea = handArea;
            _cardCounter = cardCounter;
            _playerBase = playerBase;
            _playerIsLandlord = playerIsLandlord;
            _baseBuildings = baseBuildings;

            _actorNumbers = _net.GetPlayerActorNumbers();
            _mySlot = NetworkProtocol.GetPlayerSlot(_net.LocalActorNumber, _actorNumbers);

            _net.OnCustomEvent += OnNetworkEvent;
            _net.OnPlayerLeft += OnPlayerLeft;
            _net.OnMasterSwitched += OnMasterSwitched;

            // 时间同步：Master 广播 PhotonNetwork.Time，所有客户端统一游戏开始时间
            _gameStateMachine = FindFirstObjectByType<GameStateMachine>();
            SyncGameTime();

            // 注册本机玩家手牌到 _slotHands（Master 端用于验证）
            RegisterSlotHand(_mySlot, playerHand);

            // Master 端：自己的槽位直接用 _mainDeck（AI 不再消耗 _mainDeck，保证 _deckId 一致）
            if (_net.IsMasterClient)
            {
                _slotDecks[_mySlot] = _deck;
            }

            _initialized = true;
            _syncState = SyncState.Pending;
            TransitPhase(GameSyncPhase.SYNC); // v2.0: 进入 SYNC 阶段
            Trace("INITIALIZED");

            if (_net.IsMasterClient)
            {
                // Master: 生成快照 → 存入 Room.Properties → 推进状态机
                _playerReadyReceived.Add(_mySlot);
                StoreSnapshot();
                TransitSyncState(SyncState.Signaling);
            }
            else
            {
                // 客户端: 尝试拉取 + 启动轮询 + 向 Master 报告就绪
                TryTransitFromPull();
                float initGold = _economyManager != null ? _economyManager.CurrentGold : 0f;
                _net.SendToMaster(NetworkProtocol.PLAYER_READY, new object[] { _mySlot, initGold });
            }

            // 初始化调试工具
            _logger = gameObject.AddComponent<NetworkLogger>();
            _logger.Initialize(_mySlot);
            _debugPanel = gameObject.AddComponent<NetworkDebugPanel>();
            _debugPanel.Initialize(_mySlot, _net.IsMasterClient);

            Debug.Log($"[NetworkGame] 初始化完成，本机槽位={_mySlot}, IsMaster={_net.IsMasterClient}");
        }

        // ─── L0: 快照存储（Event + Snapshot + Tick 三层模型） ───

        private void StoreSnapshot()
        {
            if (!_net.IsMasterClient) return;
            int tick = AdvanceTick();
            var snapshot = BuildCurrentSnapshot(tick);
            _net.SetRoomProperty(SNAPSHOT_KEY, snapshot.Serialize());
            _net.SendToAll(NetworkProtocol.SNAPSHOT_PUSH, 0);
            Trace($"SNAPSHOT_STORED tick={tick}");
        }

        /// <summary>局间重置：新一局开始前清除所有运行态（禁止跨局状态残留）</summary>
        public void ResetForNewRound()
        {
            _gameStarted = false;
            _currentDeckId = 0;
            _playerReadyReceived.Clear();
            _syncState = SyncState.Pending;
            _eventBuffer.Clear();
            _clientEventBuffer.Clear();
            _slotHands.Clear();
            _slotDecks.Clear();
            _slotEconomies.Clear();
            _tick = 0;
            _lastReceivedTick = 0;
            _phase = GameSyncPhase.INIT;
            Trace("RESET_FOR_NEW_ROUND");
        }

        /// <summary>
        /// Tick 层核心：Master 单调递增逻辑时钟。
        /// 每次状态变更前调用，所有 Event/Snapshot 必须携带此 Tick。
        /// </summary>
        public int AdvanceTick()
        {
            _tick++;
            Trace($"TICK_ADVANCE tick={_tick}");
            return _tick;
        }

        /// <summary>获取当前 Tick（只读）</summary>
        public int CurrentTick => _tick;

        /// <summary>获取当前生命周期阶段（只读）</summary>
        public GameSyncPhase CurrentPhase => _phase;

        // ─── v2.0 生命周期阶段转换（Frozen Architecture） ───

        /// <summary>
        /// 强制阶段转换（只允许前进，不允许回退）。
        /// 铁律：phase[n+1] > phase[n]
        /// </summary>
        private void TransitPhase(GameSyncPhase nextPhase)
        {
            if (nextPhase <= _phase) return;
            _phase = nextPhase;
            Trace($"PHASE_TRANSITION -> {_phase}");
        }

        /// <summary>
        /// v2.0 Client Event 缓存机制。
        /// Event 仅作为不可变输入流，不直接修改状态。
        /// 缓存后等待 Snapshot 授权执行。
        /// </summary>
        private readonly Queue<GameEvent> _clientEventBuffer = new();
        private const int MAX_EVENT_BUFFER_SIZE = 256;

        /// <summary>Client 缓存 Event（不执行）</summary>
        private void BufferEvent(GameEvent evt)
        {
            if (_clientEventBuffer.Count >= MAX_EVENT_BUFFER_SIZE)
            {
                _clientEventBuffer.Dequeue(); // 丢弃最旧的
                Trace($"EVENT_BUFFER_OVERFLOW discarded oldest");
            }
            _clientEventBuffer.Enqueue(evt);
            Trace($"EVENT_BUFFERED tick={evt.Tick} type={evt.Type}");
        }

        /// <summary>
        /// v2.0 Snapshot 授权执行。
        /// Snapshot 到达后，清空 Event 缓存（Snapshot 是唯一真相源）。
        /// </summary>
        private void FlushEventBuffer()
        {
            int count = _clientEventBuffer.Count;
            if (count > 0)
            {
                _clientEventBuffer.Clear();
                Trace($"EVENT_BUFFER_FLUSHED count={count}");
            }
        }

        // ─── 单调状态机核心（兼容旧逻辑） ───

        /// <summary>单调递增状态跃迁（铁律：nextState 必须 > 当前状态）</summary>
        private void TransitSyncState(SyncState nextState)
        {
            if (nextState <= _syncState) return;
            _syncState = nextState;
            Trace($"SYNC_STATE -> {_syncState}");

            if (_syncState == SyncState.Signaling)
            {
                // 从 Room.Properties 拉取快照
                if (TryExtractSnapshotFromRoom())
                    TransitSyncState(SyncState.Applying);
                else
                    _syncState = SyncState.Pending; // 拉取失败，回退等下一次信号
            }

            if (_syncState == SyncState.Applying)
            {
                ExecuteWorldReconstruction();
            }
        }

        /// <summary>触发源 A：200ms 主动轮询</summary>
        private void PollSnapshot()
        {
            if (_syncState >= SyncState.Applying || _net == null) return;
            _snapshotPollTimer -= Time.deltaTime;
            if (_snapshotPollTimer <= 0f)
            {
                _snapshotPollTimer = SNAPSHOT_POLL_INTERVAL;
                if (HasSnapshotInRoom())
                    TransitSyncState(SyncState.Signaling);
            }
        }

        private bool HasSnapshotInRoom()
        {
            return _net.GetRoomProperty(SNAPSHOT_KEY) != null;
        }

        private bool TryExtractSnapshotFromRoom()
        {
            var data = _net.GetRoomProperty(SNAPSHOT_KEY);
            if (data is object[] arr && arr.Length >= 14)
            {
                var snapshot = GameSnapshot.Deserialize(arr);
                if (snapshot != null && snapshot.Tick > 0)
                {
                    GameSession.NetworkSeed = snapshot.NetworkSeed;
                    GameSession.PlayerBaseMapping = snapshot.PlayerBaseMapping;
                    GameSession.BidMultiplier = snapshot.BidMultiplier;
                    _lastReceivedTick = snapshot.Tick;
                    _currentDeckId = snapshot.DeckId;
                    Trace($"SNAPSHOT_EXTRACTED tick={snapshot.Tick}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>唯一的世界线解封点</summary>
        private void ExecuteWorldReconstruction()
        {
            _syncState = SyncState.Synchronized;
            Trace($"WORLD_RECONSTRUCTED seed={GameSession.NetworkSeed}");
            // Buffer 不在此处 flush，等待 GAME_START 授权后由客户端自行处理
        }

        /// <summary>客户端尝试从 Room 拉取快照（Initialize 时调用）</summary>
        private void TryTransitFromPull()
        {
            if (HasSnapshotInRoom())
                TransitSyncState(SyncState.Signaling);
        }

        private void OnDestroy()
        {
            _initialized = false;
            if (_net != null)
            {
                _net.OnCustomEvent -= OnNetworkEvent;
                _net.OnPlayerLeft -= OnPlayerLeft;
                _net.OnMasterSwitched -= OnMasterSwitched;
            }
            if (_battleManager != null)
            {
                _battleManager.OnUnitDiedEvent -= BroadcastUnitDied;
                _battleManager.OnUnitSpawned -= OnUnitSpawned_SetCombatMode;
            }
            CardUnit.SimulatesCombatDefault = true; // 离开联机时恢复默认
            OnNetworkGameEnd = null;
            OnCardArrived = null;
            OnCardTaken = null;
            _slotHands.Clear();
            _slotDecks.Clear();
            _slotEconomies.Clear();
        }

        // ─── Master 状态同步（定期 + 迁移） ───

        private float _stateSyncTimer;
        private float _hpSyncTimer;
        private float _goldSyncTimer;
        private const float STATE_SYNC_INTERVAL = 5f;
        private const float HP_SYNC_INTERVAL = 2f;
        private const float GOLD_SYNC_INTERVAL = 3f;


        private void Update()
        {
            if (!_initialized) return;
            PollSnapshot(); // L2 主动轨: 200ms 轮询快照

            // 运行期自愈：非 Master 客户端每 2 秒请求 Master 校验状态
            if (_gameStarted && !_net.IsMasterClient)
            {
                _reconcileTimer -= Time.deltaTime;
                if (_reconcileTimer <= 0f)
                {
                    _reconcileTimer = RECONCILE_INTERVAL;
                    _net.SendToMaster(NetworkProtocol.RECONCILE_REQUEST, _currentDeckId);
                }
            }

            // 更新调试面板
            if (_debugPanel != null)
            {
                int handCount = _playerHand?.Count ?? 0;
                float gold = _economyManager?.CurrentGold ?? 0f;
                int deckRemaining = _deck?.Remaining ?? 0;
                _debugPanel.UpdateState(handCount, gold, deckRemaining);
            }

            // 非 Master 客户端：定期同步金币到 Master（防止自动收入导致经济不同步）
            if (!_net.IsMasterClient)
            {
                _goldSyncTimer += Time.deltaTime;
                if (_goldSyncTimer >= GOLD_SYNC_INTERVAL)
                {
                    _goldSyncTimer = 0f;
                    float currentGold = _economyManager?.CurrentGold ?? 0f;
                    float incomeRate = _economyManager?.CoreEconomy?.IncomeRate ?? 0f;
                    _net.SendToMaster(NetworkProtocol.GOLD_UPDATE, new object[] { _mySlot, currentGold, incomeRate });
                }
                return;
            }

            // v2.0: 仅 Master 端执行经济增长（Client 端由 Snapshot 覆盖）
            if (_net.IsMasterClient)
            {
                float dt = Time.deltaTime;
                foreach (var kvp in _slotEconomies)
                    kvp.Value.UpdateEconomy(dt);
            }

            _stateSyncTimer += Time.deltaTime;
            if (_stateSyncTimer >= STATE_SYNC_INTERVAL)
            {
                _stateSyncTimer = 0f;
                BroadcastGameState();
            }

                _hpSyncTimer += Time.deltaTime;
            if (_hpSyncTimer >= HP_SYNC_INTERVAL)
            {
                _hpSyncTimer = 0f;
                BroadcastHPChecksum();
            }
        }

        /// <summary>Master 广播完整游戏状态（定期 + Master 切换前）</summary>
        /// <summary>
        /// Master 广播完整游戏状态快照（Event + Snapshot + Tick 三层模型）。
        /// Snapshot 是唯一权威修正源，Client 只接受 tick > localTick 的快照。
        /// </summary>
        /// <summary>
        /// v2.0 Master 广播完整游戏状态快照。
        /// Snapshot 是唯一真相源，广播后 Client 可进入 RUN 阶段。
        /// </summary>
        public void BroadcastGameState()
        {
            if (!_net.IsMasterClient) return;
            int tick = AdvanceTick();
            var snapshot = BuildCurrentSnapshot(tick);
            _net.SendToAll(NetworkProtocol.MASTER_STATE_SYNC, snapshot.Serialize());
            StoreSnapshot();

            // v2.0: Master 始终处于 RUN 阶段
            if (_phase < GameSyncPhase.RUN)
                TransitPhase(GameSyncPhase.RUN);
        }

        /// <summary>从当前游戏状态构建完整快照（Master 专用）</summary>
        private GameSnapshot BuildCurrentSnapshot(int tick)
        {
            var snapshot = new GameSnapshot
            {
                Tick = tick,
                DeckId = _currentDeckId,
                Remaining = _sharedPoolRemaining,
                GamePhase = _gameStateMachine != null ? _gameStateMachine.CurrentPhase.ToString() : "Playing",
                SharedPoolRemaining = _sharedPoolRemaining,
                NetworkSeed = GameSession.NetworkSeed,
                BidMultiplier = GameSession.BidMultiplier,
                PlayerBaseMapping = GameSession.PlayerBaseMapping
            };

            foreach (var kvp in _slotHands)
            {
                int slot = kvp.Key;
                CardHand hand = kvp.Value;
                int[] cardIndices = new int[hand.Count];
                for (int i = 0; i < hand.Count; i++)
                    cardIndices[i] = hand.Cards[i].DeckIndex;

                snapshot.SlotHands[slot] = cardIndices;
                snapshot.SlotGold[slot] = _slotEconomies.ContainsKey(slot) ? _slotEconomies[slot].CurrentGold : 0f;
                snapshot.SlotIncomeRates[slot] = _slotEconomies.ContainsKey(slot) ? _slotEconomies[slot].IncomeRate : 5f;
            }

            var units = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u != null && u.IsAlive && u.UnitId > 0)
                    snapshot.UnitHPs[u.UnitId] = u.CurrentHP;
            }

            return snapshot;
        }

        // ─── HP 校验和 ───

        /// <summary>Master 广播所有存活单位的 HP（用 UnitId 标识，跨客户端一致）</summary>
        private void BroadcastHPChecksum()
        {
            var units = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            var hpData = new List<object>();
            int aliveCount = 0;

            foreach (var u in units)
            {
                if (u == null || !u.IsAlive || u.UnitId <= 0) continue;
                hpData.Add(u.UnitId);
                hpData.Add(u.CurrentHP);
                aliveCount++;
            }

            // 只在有单位时广播
            if (aliveCount > 0)
                _net.SendToAll(NetworkProtocol.HP_CORRECTION, hpData.ToArray());
        }

        /// <summary>每次单位生成时设置 SimulatesCombat（解决本地多玩家 static 冲突）</summary>
        private void OnUnitSpawned_SetCombatMode(CardUnit unit)
        {
            if (unit != null)
                unit.SimulatesCombat = _simulatesCombat;
        }

        /// <summary>Master 广播单位死亡（Client 播放视觉死亡）</summary>
        private void BroadcastUnitDied(int unitId)
        {
            if (!_net.IsMasterClient) return;
            Trace("UNIT_DIED", unitId);
            _net.SendToAll(NetworkProtocol.UNIT_DIED, new object[] { unitId });
        }

        /// <summary>Master 广播单位攻击（Client 播放攻击动画）</summary>
        public void BroadcastUnitAttack(int unitId, int targetId)
        {
            if (!_net.IsMasterClient) return;
            _net.SendToAll(NetworkProtocol.UNIT_ATTACK, new object[] { unitId, targetId });
        }

        /// <summary>Master 广播单位受击（Client 播放受击动画+飘字）</summary>
        public void BroadcastUnitHit(int unitId, float damage, Vector3 position)
        {
            if (!_net.IsMasterClient) return;
            _net.SendToAll(NetworkProtocol.UNIT_HIT, new object[] { unitId, damage, position.x, position.y, position.z });
        }

        /// <summary>Master 广播眩晕状态（Client 播放眩晕特效）</summary>
        public void BroadcastUnitStun(int unitId, float duration)
        {
            if (!_net.IsMasterClient) return;
            _net.SendToAll(NetworkProtocol.UNIT_STUN, new object[] { unitId, duration });
        }

        /// <summary>Master 广播击退（Client 播放击退动画）</summary>
        public void BroadcastUnitKnockback(int unitId, Vector3 direction, float distance)
        {
            if (!_net.IsMasterClient) return;
            _net.SendToAll(NetworkProtocol.UNIT_KNOCKBACK, new object[] { unitId, direction.x, direction.y, direction.z, distance });
        }

        private void HandleHPCorrection(object[] data)
        {
            if (_net.IsMasterClient) return;

            // 构建 UnitId → CardUnit 映射
            var units = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            var unitMap = new Dictionary<int, CardUnit>();
            foreach (var u in units)
                if (u != null && u.UnitId > 0) unitMap[u.UnitId] = u;

            // 每 2 个元素为一组 [unitId, hp]
            int corrected = 0;
            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                int unitId = SafeInt(data[i]);
                float hp = SafeFloat(data[i + 1]);

                if (!unitMap.TryGetValue(unitId, out var unit)) continue;

                float diff = Mathf.Abs(unit.CurrentHP - hp);
                if (diff > 1f)
                {
                    unit.SetHP(hp);
                    corrected++;
                }
            }

            if (corrected > 0)
                Debug.Log($"[NetworkGame] HP 修正: {corrected} 个单位");
        }

        /// <summary>Client 处理 Master 广播的单位死亡（播放视觉死亡动画+音效）</summary>
        private void HandleUnitDied(object[] data)
        {
            if (_net.IsMasterClient) return;
            if (data.Length < 1) return;
            int unitId = SafeInt(data[0]);

            var units = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u != null && u.UnitId == unitId && u.IsAlive)
                {
                    Trace("UNIT_DIED_RECV", unitId);

                    // v2.0: 通过 UnitAudio 播放死亡音效（网络事件驱动）
                    var audio = u.GetComponent<UnitAudio>();
                    if (audio != null)
                        audio.PlayDeathNetwork();

                    u.VisualDeath();
                    return;
                }
            }
        }

        /// <summary>Client 处理 Master 广播的单位攻击（播放攻击动画+音效）</summary>
        private void HandleUnitAttack(object[] data)
        {
            if (data.Length < 2) return;
            int unitId = SafeInt(data[0]);
            int targetId = SafeInt(data[1]);

            var unit = FindUnitById(unitId);
            if (unit == null) return;

            // 播放攻击动画
            unit.UpdateAnimatorState(2); // Attack state

            // v2.0: 通过 UnitAudio 播放攻击音效（网络事件驱动）
            var audio = unit.GetComponent<UnitAudio>();
            if (audio != null)
                audio.PlayAttackNetwork();
        }

        /// <summary>Client 处理 Master 广播的单位受击（播放受击动画+飘字+音效）</summary>
        private void HandleUnitHit(object[] data)
        {
            if (data.Length < 5) return;
            int unitId = SafeInt(data[0]);
            float damage = SafeFloat(data[1]);
            float posX = SafeFloat(data[2]);
            float posY = SafeFloat(data[3]);
            float posZ = SafeFloat(data[4]);

            var unit = FindUnitById(unitId);
            if (unit == null || !unit.IsAlive) return;

            // v2.0: 通过 UnitAudio 播放受击音效（网络事件驱动）
            var audio = unit.GetComponent<UnitAudio>();
            if (audio != null)
                audio.PlayHitNetwork();

            // 显示飘字
            var pos = new Vector3(posX, posY, posZ);
            var floatingText = FindFirstObjectByType<DoudizhuTower.UI.Floating.FloatingTextPool>();
            if (floatingText != null)
                floatingText.Spawn(damage, pos, DoudizhuTower.Core.Battle.DamageType.Physical);
        }

        /// <summary>Client 处理 Master 广播的眩晕状态（播放眩晕特效）</summary>
        private void HandleUnitStun(object[] data)
        {
            if (data.Length < 2) return;
            int unitId = SafeInt(data[0]);
            float duration = SafeFloat(data[1]);

            var unit = FindUnitById(unitId);
            if (unit == null || !unit.IsAlive) return;

            // v2.0: 使用 VisualStunTimer（仅视觉表现，不污染逻辑层 StunTimer）
            unit.VisualStunTimer = duration;
            unit.UpdateAnimatorState(0); // 回到 Idle（眩晕状态）
        }

        /// <summary>Client 处理 Master 广播的击退（播放击退动画）</summary>
        private void HandleUnitKnockback(object[] data)
        {
            if (data.Length < 5) return;
            int unitId = SafeInt(data[0]);
            float dirX = SafeFloat(data[1]);
            float dirY = SafeFloat(data[2]);
            float dirZ = SafeFloat(data[3]);
            float distance = SafeFloat(data[4]);

            var unit = FindUnitById(unitId);
            if (unit == null || !unit.IsAlive) return;

            // 播放击退视觉效果（仅位移，不修改逻辑位置）
            var direction = new Vector3(dirX, dirY, dirZ).normalized;
            unit.StartCoroutine(KnockbackVisualCoroutine(unit, direction, distance));
        }

        /// <summary>击退视觉协程（仅 Client 端，纯视觉表现）</summary>
        private System.Collections.IEnumerator KnockbackVisualCoroutine(CardUnit unit, Vector3 direction, float distance)
        {
            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 startPos = unit.transform.position;
            Vector3 endPos = startPos + direction * distance;

            while (elapsed < duration && unit != null && unit.IsAlive)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                unit.transform.position = Vector3.Lerp(startPos, endPos, eased);
                yield return null;
            }
        }

        /// <summary>根据 UnitId 查找 CardUnit</summary>
        private CardUnit FindUnitById(int unitId)
        {
            var units = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u != null && u.UnitId == unitId)
                    return u;
            }
            return null;
        }

        private void OnMasterSwitched()
        {
            if (!_initialized) return;

            Debug.Log("[NetworkGame] 本机成为新 Master，等待旧 Master 状态广播...");

            // 新 Master 上任后，请求一次时间同步
            SyncGameTime();
        }

        /// <summary>
        /// v2.0 Client 处理 Master 广播的完整游戏状态快照。
        /// Snapshot 是唯一真相源，覆盖本地状态后清空 Event 缓存。
        /// 规则：incoming.tick > localTick 时覆盖状态，否则丢弃。
        /// </summary>
        private void HandleMasterStateSync(object[] data)
        {
            if (_net.IsMasterClient) return;
            if (data == null || data.Length < 14) return;

            var snapshot = GameSnapshot.Deserialize(data);
            if (snapshot == null || !snapshot.Tick.IsValidTick()) return;

            // Tick 收敛规则：旧数据不可覆盖新状态
            if (snapshot.Tick <= _lastReceivedTick)
            {
                Trace($"SNAPSHOT_STALE tick={snapshot.Tick}<=local={_lastReceivedTick}");
                return;
            }

            Trace($"SNAPSHOT_APPLIED tick={snapshot.Tick}");
            _lastReceivedTick = snapshot.Tick;

            // v2.0: Snapshot 授权后清空 Event 缓存
            FlushEventBuffer();

            // v2.0: 首次收到 Snapshot 后进入 RUN 阶段
            if (_phase < GameSyncPhase.RUN)
                TransitPhase(GameSyncPhase.RUN);

            // 覆盖本地状态（Snapshot 是唯一权威修正源）
            _currentDeckId = snapshot.DeckId;

            foreach (var kvp in snapshot.SlotGold)
            {
                int slot = kvp.Key;
                if (_slotEconomies.ContainsKey(slot))
                    _slotEconomies[slot].SetGold(kvp.Value);
            }

            foreach (var kvp in snapshot.SlotIncomeRates)
            {
                int slot = kvp.Key;
                if (_slotEconomies.ContainsKey(slot))
                    _slotEconomies[slot].SetIncomeRate(kvp.Value);
            }

            foreach (var kvp in snapshot.UnitHPs)
            {
                int unitId = kvp.Key;
                float hp = kvp.Value;
                var units = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
                foreach (var u in units)
                {
                    if (u != null && u.UnitId == unitId && u.IsAlive)
                    {
                        u.SetHP(hp);
                        break;
                    }
                }
            }
        }

        /// <summary>用 Master 广播的手牌数据修正本机手牌</summary>
        private void ReconcileHand(int[] masterCardIndices)
        {
            if (_playerHand == null) return;

            // 构建 Master 手牌集合
            var masterSet = new HashSet<int>(masterCardIndices);
            var localSet = new HashSet<int>();
            for (int i = 0; i < _playerHand.Count; i++)
                localSet.Add(_playerHand.Cards[i].DeckIndex);

            // 找出本地多出的牌（Master 没有但本地有）→ 移除
            var toRemove = new List<Card>();
            for (int i = 0; i < _playerHand.Count; i++)
            {
                Card c = _playerHand.Cards[i];
                if (!masterSet.Contains(c.DeckIndex))
                    toRemove.Add(c);
            }
            foreach (var c in toRemove)
            {
                _playerHand.Remove(c);
                Debug.LogWarning($"[NetworkGame] 手牌校正: 移除本地多余牌 {c}");
            }

            // 找出本地缺少的牌（Master 有但本地没有）→ 添加
            foreach (int idx in masterCardIndices)
            {
                if (!localSet.Contains(idx))
                {
                    Card c = _deck.GetCardByIndex(idx);
                    if (c.DeckIndex >= 0)
                    {
                        _playerHand.Add(c);
                        Debug.LogWarning($"[NetworkGame] 手牌校正: 补充缺失牌 {c}");
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                _handArea?.NotifyHandChanged();
                _cardCounter?.Refresh();
            }
        }

        // ─── 时间同步 ───

        private const string GAME_TIME_SYNC = "GAME_TIME_SYNC";

        private double _networkGameStartTime;
        private double _lastSyncNetworkTime;

        private void SyncGameTime()
        {
            if (_gameStateMachine == null) return;

            if (_net.IsMasterClient)
            {
                // Master 记录游戏开始的网络时间，并广播
                _networkGameStartTime = PhotonNetwork.Time;
                // Master 的 localStartTime 就是当前 Time.time（因为游戏刚开始）
                _gameStateMachine.SyncGameStartTime(Time.time);
                // 广播：[网络开始时间, 已经过时间]
                _net.SendToAll(GAME_TIME_SYNC, new object[] { _networkGameStartTime, 0f });
            }
            // Client 在收到 GAME_TIME_SYNC 事件后同步
        }

        /// <summary>
        /// 延迟加入的客户端请求时间同步（由 GameBootstrapper 在非房主初始化时调用）
        /// </summary>
        public void RequestTimeSync()
        {
            if (!_net.IsMasterClient && _initialized)
                _net.SendToMaster(GAME_TIME_SYNC, new object[0]);
        }

        // ─── 出牌请求（由 HandArea 调用） ───

        public void RequestPlayCards(Card[] cards, CardTypeResult result, RouteGroup routeGroup)
        {
            if (!_initialized || _net == null) return;

            int[] cardIndices = NetworkProtocol.SerializeCards(cards);
            object[] typeData = NetworkProtocol.SerializeCardTypeResult(result);
            int routeIndex = routeGroup != null ? routeGroup.CurrentIndex : 0;
            int baseIndex = Array.IndexOf(_baseBuildings, _playerBase);

            if (_net.IsMasterClient)
            {
                // Master 直接验证并执行
                MasterValidateAndPlay(_mySlot, cardIndices, typeData, routeIndex, baseIndex);
            }
            else
            {
                // Client 发送到 Master 验证（附带当前金币，防止经济不同步）
                float currentGold = _economyManager != null ? _economyManager.CurrentGold : 0f;
                _net.SendToMaster(NetworkProtocol.PLAY_CARDS, new object[]
                {
                    cardIndices, typeData, routeIndex, baseIndex, currentGold
                });
            }
        }

        // ─── AI 出牌广播（由 BuildingAI 调用，仅 Master 执行） ───

        public void BroadcastAIPlay(int slot, Card[] cards, CardTypeResult result, int routeIndex, Component sourceBase)
        {
            if (!_initialized || _net == null || !_net.IsMasterClient) return;

            int baseIndex = Array.IndexOf(_baseBuildings, sourceBase);
            int[] cardIndices = NetworkProtocol.SerializeCards(cards);
            object[] typeData = NetworkProtocol.SerializeCardTypeResult(result);

            // 计算费用并扣除 AI 金币
            float cost = CardCostCalculator.CalculateTotalCost(cards, result);
            var aiEconomy = _slotEconomies.ContainsKey(slot) ? _slotEconomies[slot] : null;
            aiEconomy?.TrySpend(cost);

            // Master 本地执行
            RouteGroup routeGroup = sourceBase?.GetComponent<RouteGroup>();
            if (routeGroup != null && routeIndex >= 0)
                routeGroup.SetRouteIndex(routeIndex);
            _battleManager?.DeployCards(cards, result, routeGroup, sourceBase);

            Debug.Log($"[NetworkGame] BroadcastAIPlay: 槽位={slot}, baseIndex={baseIndex}, cards={cards.Length}");

            // 广播给所有客户端
            _net.SendToAll(NetworkProtocol.PLAY_APPROVED, new object[]
            {
                slot, cardIndices, typeData, routeIndex, baseIndex, cost
            });

            // 广播金币变化（复用上面的 aiEconomy 变量）
            if (aiEconomy != null)
                _net.SendToAll(NetworkProtocol.GOLD_UPDATE, new object[] { slot, aiEconomy.CurrentGold, aiEconomy.IncomeRate });
        }

        // ─── 网络事件处理 ───

        /// <summary>安全拆箱 int（Photon 可能返回 short/byte/long）</summary>
        private static int SafeInt(object o) => NetworkProtocol.SafeInt(o);
        private static float SafeFloat(object o) => NetworkProtocol.SafeFloat(o);

        /// <summary>按 DeckIndex 查找手牌中是否有对应卡牌（跨 CardDeck 实例安全）</summary>
        private static bool ContainsByDeckIndex(CardHand hand, int deckIndex)
        {
            foreach (var c in hand.Cards)
                if (c.DeckIndex == deckIndex) return true;
            return false;
        }

        /// <summary>按 DeckIndex 从手牌中移除卡牌（跨 CardDeck 实例安全）</summary>
        private static bool RemoveByDeckIndex(CardHand hand, int deckIndex)
        {
            for (int i = 0; i < hand.CardsList.Count; i++)
            {
                if (hand.CardsList[i].DeckIndex == deckIndex)
                {
                    hand.CardsList.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>按 DeckIndex 从手牌中批量移除卡牌（触发 OnHandChanged）</summary>
        private static void RemoveRangeByDeckIndex(CardHand hand, Card[] cards)
        {
            bool changed = false;
            foreach (var card in cards)
                changed |= RemoveByDeckIndex(hand, card.DeckIndex);
            if (changed) hand.NotifyHandModified();
        }

        /// <summary>安全拆箱为 object[]，null 时返回空数组</summary>
        private static object[] SafeArray(object o) => o as object[] ?? Array.Empty<object>();

        private void OnNetworkEvent(string key, object value, int senderActor)
        {
            if (!_initialized)
            {
                _eventBuffer.Enqueue((key, value, senderActor));
                return;
            }

            // 触发源 B: SNAPSHOT_PUSH 是纯唤醒信号（不携带数据）
            if (key == NetworkProtocol.SNAPSHOT_PUSH)
            {
                TransitSyncState(SyncState.Signaling);
                return;
            }

            // 收敛门：GAME_START 授权运行（纯状态变更，不 flush）
            if (key == NetworkProtocol.GAME_START)
            {
                _gameStarted = true;
                TransitPhase(GameSyncPhase.RUN); // v2.0: 进入 RUN 阶段
                Trace("GAME_START_RECEIVED");
                return;
            }

            // PLAYER_READY：仅用于收敛计数，立即处理，不进 buffer
            if (key == NetworkProtocol.PLAYER_READY)
            {
                ExecuteEvent(key, value, senderActor);
                return;
            }

            // v2.0: 移除旧的 _gameStarted 检查，统一由 ExecuteEvent 阶段门控处理
            ExecuteEvent(key, value, senderActor);
        }

        private void ExecuteEvent(string key, object value, int senderActor)
        {
            _debugPanel?.LogEvent($"Recv: {key} from {senderActor}");
            if (value == null)
            {
                Debug.LogWarning($"[NetworkGame] 收到空消息: key={key}");
                return;
            }

            // ─── v2.0 阶段门控（Frozen Architecture） ───
            // Master 始终执行；Client 在非 RUN 阶段缓冲事件
            if (!_net.IsMasterClient && _phase != GameSyncPhase.RUN)
            {
                if (_phase == GameSyncPhase.INIT || _phase == GameSyncPhase.SYNC)
                {
                    // SYNC 阶段：缓冲等待 Snapshot 授权
                    BufferEvent(new GameEvent(_tick, -1, key, SafeArray(value)));
                    return;
                }
                // END/RESET 阶段：丢弃所有事件
                if (_phase == GameSyncPhase.END || _phase == GameSyncPhase.RESET)
                    return;
            }

            // ─── Master 逻辑（唯一权威） ───
            switch (key)
            {
                case NetworkProtocol.PLAY_CARDS:
                    if (_net.IsMasterClient)
                        HandlePlayCardsOnMaster(SafeArray(value), senderActor);
                    break;

                case NetworkProtocol.PLAY_APPROVED:
                    Debug.Log($"[NetworkGame] 收到 PLAY_APPROVED 事件, IsMaster={_net.IsMasterClient}");
                    HandlePlayApproved(SafeArray(value));
                    break;

                case NetworkProtocol.PLAY_REJECTED:
                    HandlePlayRejected(SafeArray(value));
                    break;

                case NetworkProtocol.DRAW_CARD:
                    if (_net.IsMasterClient)
                    {
                        var requestData = SafeArray(value);
                        if (requestData.Length < 1) break;
                        int drawSlot = SafeInt(requestData[0]);
                        float drawClientGold = requestData.Length > 1 ? SafeFloat(requestData[1]) : -1f;
                        float drawCost = requestData.Length > 2 ? SafeFloat(requestData[2]) : 0f;
                        // Master 使用自己追踪的金币（已在 Update 中自动累加收入），不信任客户端报告
                        MasterDrawCard(drawSlot, drawCost);
                    }
                    break;

                case NetworkProtocol.DRAW_CARD_RESULT:
                    Trace($"DRAW_RESULT_RECV isMaster={_net.IsMasterClient}");
                    if (!_net.IsMasterClient)
                    {
                        HandleDrawCard(SafeArray(value));
                    }
                    break;

                case NetworkProtocol.NEW_DECK:
                    _currentDeckId = SafeInt(value);
                    _cardCounter?.Refresh();
                    Trace($"NEW_DECK deckId={_currentDeckId}");
                    break;

                case NetworkProtocol.RECONCILE_REQUEST:
                    if (_net.IsMasterClient)
                    {
                        // Master 返回完整游戏状态快照（Event + Snapshot + Tick 三层模型）
                        int tick = AdvanceTick();
                        var reconcileSnapshot = BuildCurrentSnapshot(tick);
                        _net.SendToPlayer(senderActor, NetworkProtocol.SNAPSHOT_RESPONSE, reconcileSnapshot.Serialize());
                    }
                    break;

                case NetworkProtocol.SNAPSHOT_RESPONSE:
                    if (!_net.IsMasterClient)
                    {
                        var snap = GameSnapshot.Deserialize(value as object[]);
                        if (snap != null && snap.Tick > _lastReceivedTick)
                        {
                            Trace($"RECONCILE_APPLIED tick={snap.Tick}");
                            _lastReceivedTick = snap.Tick;
                            _currentDeckId = snap.DeckId;
                            foreach (var kvp in snap.SlotGold)
                                if (_slotEconomies.ContainsKey(kvp.Key))
                                    _slotEconomies[kvp.Key].SetGold(kvp.Value);
                            foreach (var kvp in snap.SlotIncomeRates)
                                if (_slotEconomies.ContainsKey(kvp.Key))
                                    _slotEconomies[kvp.Key].SetIncomeRate(kvp.Value);
                            _cardCounter?.Refresh();
                        }
                    }
                    break;

                case NetworkProtocol.GOLD_UPDATE:
                    HandleGoldUpdate(SafeArray(value));
                    break;

                case NetworkProtocol.DOMAIN_PENDING:
                    HandleDomainPending(value, senderActor);
                    break;

                case NetworkProtocol.COUNTER_PENDING:
                    HandleCounterPending(value, senderActor);
                    break;

                case NetworkProtocol.DOMAIN_ACTIVATE:
                    if (_net.IsMasterClient)
                        HandleDomainActivateOnMaster(SafeArray(value), senderActor);
                    else
                        HandleDomainActivateBroadcast(SafeArray(value));
                    break;

                case NetworkProtocol.COUNTER_ACTIVATE:
                    if (_net.IsMasterClient)
                        HandleCounterActivateOnMaster(SafeArray(value), senderActor);
                    else
                        HandleCounterActivateBroadcast(SafeArray(value));
                    break;

                case NetworkProtocol.GAME_END:
                    HandleGameEnd(SafeArray(value));
                    break;

                case NetworkProtocol.PLAYER_LEFT:
                    HandlePlayerLeft(SafeArray(value));
                    break;

                case GAME_TIME_SYNC:
                    HandleGameTimeSync(value);
                    break;

                case NetworkProtocol.PLAYER_READY:
                    {
                        var readyData = (object[])value;
                        int readySlot = SafeInt(readyData[0]);
                        _playerReadyReceived.Add(readySlot);
                        if (_net.IsMasterClient)
                        {
                            HandlePlayerReady(readyData);
                            float elapsed = _gameStateMachine != null ? _gameStateMachine.ElapsedTime : 0f;
                            _net.SendToAll(GAME_TIME_SYNC, new object[] { _networkGameStartTime, elapsed });
                            // 收敛门：所有真人玩家就绪后广播 GAME_START
                            int expected = 3 - GameSession.AISlots.Count;
                            if (!_gameStarted && _playerReadyReceived.Count >= expected)
                            {
                                _gameStarted = true;
                                _net.SendToAll(NetworkProtocol.GAME_START, 0);
                                Trace($"GAME_START_SENT players={_playerReadyReceived.Count}/{expected}");
                            }
                        }
                    }
                    break;

                case NetworkProtocol.CARD_TRANSFER:
                    if (_net.IsMasterClient)
                        HandleCardTransferOnMaster(SafeArray(value), senderActor);
                    break;

                case NetworkProtocol.CARD_ARRIVE:
                    HandleCardArrive(SafeArray(value));
                    break;

                case NetworkProtocol.CARD_TAKE:
                    if (_net.IsMasterClient)
                        HandleCardTakeOnMaster(SafeArray(value), senderActor);
                    else
                        HandleCardTake(SafeArray(value));
                    break;

                case NetworkProtocol.MASTER_STATE_SYNC:
                    HandleMasterStateSync(SafeArray(value));
                    break;

                case NetworkProtocol.HP_CHECKSUM:
                    // 已弃用，保留兼容
                    break;

                case NetworkProtocol.HP_CORRECTION:
                    HandleHPCorrection(SafeArray(value));
                    break;

                case NetworkProtocol.UNIT_DIED:
                    HandleUnitDied(SafeArray(value));
                    break;

                case NetworkProtocol.UNIT_ATTACK:
                    if (!_net.IsMasterClient)
                        HandleUnitAttack(SafeArray(value));
                    break;

                case NetworkProtocol.UNIT_HIT:
                    if (!_net.IsMasterClient)
                        HandleUnitHit(SafeArray(value));
                    break;

                case NetworkProtocol.UNIT_STUN:
                    if (!_net.IsMasterClient)
                        HandleUnitStun(SafeArray(value));
                    break;

                case NetworkProtocol.UNIT_KNOCKBACK:
                    if (!_net.IsMasterClient)
                        HandleUnitKnockback(SafeArray(value));
                    break;

                case NetworkProtocol.CARD_DISCARDED:
                    if (!_net.IsMasterClient)
                        ApplyCardDiscarded(SafeInt(value));
                    break;
            }
        }

        // ─── 出牌同步 ───

        private void HandlePlayCardsOnMaster(object[] data, int senderActor)
        {
            if (data.Length < 4) return;
            int[] cardIndices = (int[])data[0];
            object[] typeData = (object[])data[1];
            int routeIndex = SafeInt(data[2]);
            int baseIndex = SafeInt(data[3]);

            int senderSlot = NetworkProtocol.GetPlayerSlot(senderActor, _actorNumbers);
            if (senderSlot < 0) return;

            // Master 使用自己追踪的金币（已在 Update 中自动累加收入），不信任客户端报告
            float clientGold = (data.Length > 4) ? SafeFloat(data[4]) : -1f;
            Trace("PLAY_CARDS_RECV", senderSlot);
            MasterValidateAndPlay(senderSlot, cardIndices, typeData, routeIndex, baseIndex, clientGold);
        }

        private void MasterValidateAndPlay(int playerSlot, int[] cardIndices, object[] typeData, int routeIndex, int baseIndex, float clientGold = -1f)
        {
            // 反序列化
            Card[] cards = NetworkProtocol.DeserializeCards(cardIndices, _deck);
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(typeData);

            // 手牌验证：Master 验证所有玩家的手牌
            CardHand hand = (playerSlot == _mySlot)
                ? _playerHand
                : (_slotHands.ContainsKey(playerSlot) ? _slotHands[playerSlot] : null);

            if (hand != null)
            {
                foreach (var card in cards)
                {
                    // 用 DeckIndex 比较（不同 CardDeck 实例的 _deckId 不同，Card.Equals 会失败）
                    if (!ContainsByDeckIndex(hand, card.DeckIndex))
                    {
                        Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 手牌中无此牌: {card} (DeckIndex={card.DeckIndex}), 拒绝出牌");
                        if (playerSlot != _mySlot)
                            _net.SendToPlayer(_actorNumbers[playerSlot], NetworkProtocol.PLAY_REJECTED,
                                new object[] { "手牌校验失败" });
                        return;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 手牌未注册，跳过校验");
            }

            // 领域封印校验（Master 权威，防止客户端绕过封印出牌）
            if (_domainSystem != null && _domainSystem.IsDomainActive)
            {
                bool slotIsLandlord = false;
                if (playerSlot >= 0 && playerSlot < _baseBuildings.Length)
                {
                    var cu = _baseBuildings[playerSlot]?.GetComponent<CardUnit>();
                    if (cu != null) slotIsLandlord = cu.IsLandlord;
                }
                if (_domainSystem.IsSealedByDomain(slotIsLandlord))
                {
                    // 炸弹/王炸：破封放行
                    if (result.Type != CardType.Bomb && result.Type != CardType.DoubleKingBomb)
                    {
                        // 能管上的牌：放行
                        if (!CardTypeCompare.CanCounter(_domainSystem.CurrentDomainType, result))
                        {
                            Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 被领域封印，拒绝出牌: {result.Type}");
                            if (playerSlot != _mySlot)
                                _net.SendToPlayer(_actorNumbers[playerSlot], NetworkProtocol.PLAY_REJECTED,
                                    new object[] { "领域封印" });
                            return;
                        }
                    }
                }
            }

            // 验证金币
            float cost = CardCostCalculator.CalculateTotalCost(cards, result);
            EconomySystem targetEconomy = (playerSlot == _mySlot)
                ? _economyManager?.CoreEconomy
                : (_slotEconomies.ContainsKey(playerSlot) ? _slotEconomies[playerSlot] : null);

            // 经济同步验证日志（观察 Master 与 Client 金币是否长期一致）
            if (playerSlot != _mySlot && targetEconomy != null)
                Debug.Log($"[ECON SYNC] slot={playerSlot} client={clientGold:F0} master={targetEconomy.CurrentGold:F0} cost={cost:F0}");

            // 远程玩家：自动创建经济追踪（PLAYER_READY 可能延迟到达）
            if (playerSlot != _mySlot && !_slotEconomies.ContainsKey(playerSlot))
            {
                var econConfig = Resources.Load<DoudizhuTower.Config.EconomyConfig>("EconomyConfig");
                float incomeRate = econConfig != null ? econConfig.farmerBaseIncome : 5f;
                float initGold = clientGold >= 0f ? clientGold : (econConfig != null ? econConfig.initialGold : 50f);
                targetEconomy = new EconomySystem(initGold, incomeRate);
                _slotEconomies[playerSlot] = targetEconomy;
                Debug.Log($"[NetworkGame] 自动创建槽位 {playerSlot} 经济: 金币={initGold}");
            }

            // Master 使用自己追踪的金币（已在 Update 中自动累加收入），不信任客户端报告
            if (targetEconomy == null || !targetEconomy.TrySpend(cost))
            {
                float gold = targetEconomy?.CurrentGold ?? 0f;
                Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 金币不足: 需要{cost}, 当前{gold}, 客户端报告{clientGold}");
                if (playerSlot == _mySlot)
                {
                    _handArea?.ShowInsufficientGoldFeedback(cost, gold);
                    _economyManager?.FlashGoldText();
                    AudioManager.Instance?.PlayInsufficientGold();
                }
                else
                {
                    _net.SendToPlayer(_actorNumbers[playerSlot], NetworkProtocol.PLAY_REJECTED, new object[] { "金币不足", cost });
                }
                _domainSystem?.CancelPending();
                return;
            }

            // 验证通过，广播执行
            Trace("PLAY_APPROVED", playerSlot);
            _net.SendToAll(NetworkProtocol.PLAY_APPROVED, new object[]
            {
                playerSlot, cardIndices, typeData, routeIndex, baseIndex, cost
            });

            // 广播该玩家的金币变化
            if (targetEconomy != null)
                _net.SendToAll(NetworkProtocol.GOLD_UPDATE, new object[] { playerSlot, targetEconomy.CurrentGold, targetEconomy.IncomeRate });

            // Master 本地也执行
            ExecutePlayApproved(playerSlot, cards, result, routeIndex, baseIndex, cost);
        }

        private void HandlePlayApproved(object[] data)
        {
            if (data.Length < 6) return;
            int playerSlot = SafeInt(data[0]);
            int[] cardIndices = (int[])data[1];
            object[] typeData = (object[])data[2];
            int routeIndex = SafeInt(data[3]);
            int baseIndex = SafeInt(data[4]);
            float cost = SafeFloat(data[5]);

            // Master 已在 MasterValidateAndPlay 中执行，跳过
            if (_net.IsMasterClient) return;
            Trace("PLAY_APPROVED_RECV", playerSlot);

            Debug.Log($"[NetworkGame] HandlePlayApproved: 收到槽位={playerSlot} 的出牌广播, baseIndex={baseIndex}");
            Card[] cards = NetworkProtocol.DeserializeCards(cardIndices, _deck);
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(typeData);
            ExecutePlayApproved(playerSlot, cards, result, routeIndex, baseIndex, cost);
        }

        private void ExecutePlayApproved(int playerSlot, Card[] cards, CardTypeResult result, int routeIndex, int baseIndex, float cost)
        {
            // 获取基地和路线
            Component sourceBase = (baseIndex >= 0 && baseIndex < _baseBuildings.Length)
                ? _baseBuildings[baseIndex] : null;

            // baseIndex=-1 时（AI 基地不在 _baseBuildings 中），按槽位身份查找
            if (sourceBase == null && playerSlot >= 0 && playerSlot < _baseBuildings.Length)
            {
                sourceBase = _baseBuildings[playerSlot];
                Debug.Log($"[NetworkGame] baseIndex=-1, 按槽位 {playerSlot} 回退查找基地: {sourceBase?.name ?? "null"}");
            }

            RouteGroup routeGroup = sourceBase?.GetComponent<RouteGroup>();
            if (routeGroup != null && routeIndex >= 0)
                routeGroup.SetRouteIndex(routeIndex);

            Debug.Log($"[NetworkGame] ExecutePlayApproved: 槽位={playerSlot}, baseIndex={baseIndex}, sourceBase={sourceBase?.name ?? "null"}, " +
                      $"routeGroup={routeGroup != null}, cards={cards.Length}, IsMaster={_net.IsMasterClient}");

            // 扣费：仅本机玩家在非 Master 客户端扣费（Master 已在 MasterValidateAndPlay 中扣过）
            if (!_net.IsMasterClient && playerSlot == _mySlot && _economyManager != null)
                _economyManager.TrySpendGold(cost);

            // 移除手牌（用 DeckIndex 比较，避免 _deckId 不同步导致移除失败）
            if (playerSlot == _mySlot)
            {
                // 本地玩家：从 HandArea 管理的手牌移除
                RemoveRangeByDeckIndex(_playerHand, cards);
                _handArea?.NotifyHandChanged();
            }
            else if (_net.IsMasterClient && _slotHands.ContainsKey(playerSlot))
            {
                // Master 端远程玩家：从追踪手牌移除
                RemoveRangeByDeckIndex(_slotHands[playerSlot], cards);
            }

            // 所有客户端：将出过的牌加入本地弃牌堆 + 刷新记牌器
            _deck.Discard(cards);
            _cardCounter?.Refresh();

            // 生成兵种（所有客户端执行）
            if (_battleManager != null)
            {
                _battleManager.DeployCards(cards, result, routeGroup, sourceBase);
                Debug.Log($"[NetworkGame] DeployCards 执行完成: 槽位={playerSlot}, 兵种数={cards.Length}");
            }
            else
            {
                Debug.LogError("[NetworkGame] _battleManager 为 null，无法生成兵种！");
            }

            // 触发领域系统（isPlayer 表示是否为当前玩家视角的出牌）
            _domainSystem?.OnCardPlayed(result, true);
        }

        private void HandlePlayRejected(object[] data)
        {
            if (data.Length < 1) return;
            string reason = Convert.ToString(data[0]);
            Trace($"PLAY_REJECTED reason={reason}");

            if (reason == "金币不足")
            {
                float cost = data.Length > 1 ? SafeFloat(data[1]) : 0f;
                float currentGold = _economyManager?.CurrentGold ?? 0f;
                _handArea?.ShowInsufficientGoldFeedback(cost, currentGold);
                _economyManager?.FlashGoldText();
                AudioManager.Instance?.PlayInsufficientGold();
            }
            else if (reason == "手牌校验失败")
            {
                _handArea?.ShowInsufficientGoldFeedback(0, 0);
                Debug.LogWarning("[NetworkGame] 手牌校验失败，可能因牌堆不同步（PLAYER_READY 竞态）");
            }
            else if (reason == "领域封印")
            {
                _handArea?.ShowInsufficientGoldFeedback(0, 0);
                AudioManager.Instance?.PlayInsufficientGold();
                Debug.LogWarning("[NetworkGame] 出牌被领域封印");
            }
        }

        // ─── 摸牌同步 ───

        public void RequestDrawCard(float cost = 0f)
        {
            if (!_initialized || _net == null)
            {
                Debug.LogWarning($"[NetworkGame] RequestDrawCard: 未初始化或网络为空 (initialized={_initialized})");
                return;
            }

            Debug.Log($"[NetworkGame] RequestDrawCard: 槽位={_mySlot}, IsMaster={_net.IsMasterClient}, cost={cost}");
            if (_net.IsMasterClient)
            {
                MasterDrawCard(_mySlot, cost);
            }
            else
            {
                float currentGold = _economyManager != null ? _economyManager.CurrentGold : 0f;
                _net.SendToMaster(NetworkProtocol.DRAW_CARD, new object[] { _mySlot, currentGold, cost });
            }
        }

        private void HandleDrawCard(object[] data)
        {
            if (data.Length < 2) return;
            int targetSlot = SafeInt(data[0]);
            int cardIndex = SafeInt(data[1]);
            float drawCost = data.Length > 2 ? SafeFloat(data[2]) : 0f;
            CardRank rank = data.Length > 3 ? (CardRank)SafeInt(data[3]) : CardRank.Three;
            int networkRemaining = data.Length > 4 ? SafeInt(data[4]) : -1;

            // Master 已在 MasterDrawCard 中执行，跳过
            if (_net.IsMasterClient) return;

            // 防污染探针：拒绝旧牌堆的包覆盖新牌堆状态
            // deckId < _currentDeckId 表示这是旧牌堆的摸牌结果，应该丢弃
            int eventDeckId = data.Length > 5 ? SafeInt(data[5]) : _currentDeckId;
            if (eventDeckId < _currentDeckId)
            {
                Debug.LogWarning($"[NetworkGame] HandleDrawCard: 旧牌堆包被拒绝 eventDeckId={eventDeckId} < localDeckId={_currentDeckId}");
                return;
            }

            Debug.Log($"[NetworkGame] HandleDrawCard: 槽位={targetSlot}, remain={networkRemaining}, counter={_cardCounter != null}");

            // 客户端：将卡牌添加到本地手牌
            if (targetSlot == _mySlot)
            {
                // 本地扣费（Master 已验证通过）
                if (drawCost > 0f && _economyManager != null)
                    _economyManager.TrySpendGold(drawCost);

                Card card = _deck.GetCardByIndex(cardIndex);
                Debug.Log($"[NetworkGame] HandleDrawCard: 槽位={targetSlot}, DeckIndex={cardIndex}, Card={card}, cost={drawCost}, HandCount={_playerHand.Count}");
                bool added = _playerHand.Add(card);
                Debug.Log($"[NetworkGame] HandleDrawCard: Add结果={added}, HandCount={_playerHand.Count}");
                _handArea?.NotifyHandChanged();
                AudioManager.Instance?.PlayDrawCard();
            }

            _cardCounter?.Refresh();
        }

        private void MasterDrawCard(int targetSlot, float cost = 0f)
        {
            Card card;
            int cardIndex;

            // 金币验证（远程玩家用客户端报告的金币，Master 自己用本地经济）
            if (cost > 0f)
            {
                EconomySystem drawEconomy = (targetSlot == _mySlot)
                    ? _economyManager?.CoreEconomy
                    : (_slotEconomies.ContainsKey(targetSlot) ? _slotEconomies[targetSlot] : null);
                // 远程玩家：自动创建经济追踪
                if (drawEconomy == null && targetSlot != _mySlot)
                {
                    var econConfig = Resources.Load<DoudizhuTower.Config.EconomyConfig>("EconomyConfig");
                    float incomeRate = econConfig != null ? econConfig.farmerBaseIncome : 5f;
                    drawEconomy = new EconomySystem(econConfig != null ? econConfig.initialGold : 50f, incomeRate);
                    _slotEconomies[targetSlot] = drawEconomy;
                }
                if (drawEconomy == null || !drawEconomy.TrySpend(cost))
                {
                    Debug.LogWarning($"[NetworkGame] MasterDrawCard: 槽位 {targetSlot} 金币不足，需要 {cost}");
                    if (targetSlot != _mySlot)
                        _net.SendToPlayer(_actorNumbers[targetSlot], NetworkProtocol.PLAY_REJECTED,
                            new object[] { "金币不足", cost });
                    return;
                }
                // 广播金币变化
                if (drawEconomy != null)
                    _net.SendToAll(NetworkProtocol.GOLD_UPDATE, new object[] { targetSlot, drawEconomy.CurrentGold, drawEconomy.IncomeRate });
            }

            // GAME_START 收敛门保证所有玩家已注册，此处只做防御性检查
            if (!_playerReadyReceived.Contains(targetSlot))
            {
                Trace("DRAW_REJECTED_NOT_READY", targetSlot);
                return;
            }

            Trace($"MASTER_DRAW slot={targetSlot} ready={_playerReadyReceived.Contains(targetSlot)} pool={_sharedPoolRemaining}");

            // 统一从同步牌堆摸牌（Master 自己的也用 _slotDecks，避免 _mainDeck 被 AI 消耗导致不同步）
            if (!_slotDecks.ContainsKey(targetSlot))
            {
                // 自动创建同步牌堆（处理 PLAYER_READY 还未到达的竞态）
                Debug.Log($"[NetworkGame] MasterDrawCard: 槽位 {targetSlot} 同步牌堆不存在，自动创建");
                var syncDeck = new DoudizhuTower.Core.Cards.CardDeck(GameSession.NetworkSeed);
                syncDeck.Deal(targetSlot * 7, new DoudizhuTower.Core.Cards.CardHand(17)); // 跳过
                syncDeck.Deal(7, new DoudizhuTower.Core.Cards.CardHand(17)); // 消耗初始手牌
                _slotDecks[targetSlot] = syncDeck;
                if (!_slotHands.ContainsKey(targetSlot))
                    _slotHands[targetSlot] = new DoudizhuTower.Core.Cards.CardHand(17);
            }
            var slotDeck = _slotDecks[targetSlot];
            if (slotDeck == null)
            {
                Debug.LogWarning($"[NetworkGame] MasterDrawCard: 槽位 {targetSlot} 同步牌堆不存在");
                return;
            }
            // Draw() 内部会自动 Reshuffle()，不需要手动检查 Remaining
            int prevReshuffleCount = slotDeck.ReshuffleCount;
            card = slotDeck.Draw();
            if (slotDeck.ReshuffleCount > prevReshuffleCount)
            {
                // 新一副完整牌堆，54 张全部可用
                _currentDeckId++;
                _net.SendToAll(NetworkProtocol.NEW_DECK, _currentDeckId);
                Debug.Log($"[NetworkGame] MasterDrawCard: 牌堆重洗 deckId={_currentDeckId}，共享池重置为 54");
            }
            cardIndex = card.DeckIndex;
            Debug.Log($"[NetworkGame] MasterDrawCard: 槽位={targetSlot}, DeckIndex={cardIndex}, Card={card}");

            if (targetSlot == _mySlot)
            {
                _playerHand.Add(card);
                _handArea?.NotifyHandChanged();
            }
            else
            {
                if (_slotHands.ContainsKey(targetSlot))
                    _slotHands[targetSlot].Add(card);
            }

            // 共享池剩余：由 _deck.Remaining 自动计算
            _cardCounter?.Refresh();

            Debug.Log($"[NetworkGame] MasterDrawCard: 广播 pool={_sharedPoolRemaining}, counter={_cardCounter != null}");

            // 广播摸牌结果（含 rank + 共享池剩余 + deckId，供客户端记牌器使用 + 防旧包污染）
            _net.SendToAll(NetworkProtocol.DRAW_CARD_RESULT, new object[] { targetSlot, cardIndex, cost, (int)card.Rank, _sharedPoolRemaining, _currentDeckId });
        }

        // ─── 弃牌同步 ───

        /// <summary>广播弃牌事件（3换1弃置），非 Master 客户端更新本地弃牌堆 + 记牌器。
        /// Master 已在 HandArea.OnCardDiscardRequested 中本地弃牌，不需要重复。</summary>
        public void BroadcastCardDiscarded(int deckIndex)
        {
            _net.SendToAll(NetworkProtocol.CARD_DISCARDED, deckIndex);
        }

        private void ApplyCardDiscarded(int deckIndex)
        {
            var card = _deck.GetCardByIndex(deckIndex);
            _deck.Discard(card);
            _cardCounter?.Refresh();
        }

        // ─── 飞筒传牌同步 ───

        /// <summary>请求传牌给队友（由 LaunchTubeUI 调用）</summary>
        public void RequestCardTransfer(Card card)
        {
            if (!_initialized || _net == null) return;

            int cardIndex = card.DeckIndex;

            if (_net.IsMasterClient)
            {
                MasterHandleCardTransfer(_mySlot, cardIndex);
            }
            else
            {
                _net.SendToMaster(NetworkProtocol.CARD_TRANSFER, new object[] { _mySlot, cardIndex });
            }
        }

        /// <summary>请求取走暂存槽中的牌（由 TempSlotUI 调用）</summary>
        public void RequestCardTake()
        {
            if (!_initialized || _net == null) return;

            if (_net.IsMasterClient)
            {
                MasterHandleCardTake(_mySlot);
            }
            else
            {
                _net.SendToMaster(NetworkProtocol.CARD_TAKE, new object[] { _mySlot });
            }
        }

        private void HandleCardTransferOnMaster(object[] data, int senderActor)
        {
            if (data.Length < 2) return;
            int senderSlot = SafeInt(data[0]);
            int cardIndex = SafeInt(data[1]);
            MasterHandleCardTransfer(senderSlot, cardIndex);
        }

        private void MasterHandleCardTransfer(int senderSlot, int cardIndex)
        {
            // 找到接收方（同阵营的另一个非地主玩家）
            int receiverSlot = FindTeammateSlot(senderSlot);
            if (receiverSlot < 0)
            {
                Debug.LogWarning($"[NetworkGame] 飞筒传牌失败: 槽位 {senderSlot} 没有队友");
                return;
            }

            // 从发送方手牌移除
            if (_slotHands.ContainsKey(senderSlot))
            {
                Card card = _deck.GetCardByIndex(cardIndex);
                if (!_slotHands[senderSlot].Contains(card))
                {
                    Debug.LogWarning($"[NetworkGame] 飞筒传牌失败: 槽位 {senderSlot} 手牌中无此牌");
                    return;
                }
                _slotHands[senderSlot].Remove(card);
            }

            // 广播传牌结果给接收方
            _net.SendToAll(NetworkProtocol.CARD_ARRIVE, new object[] { senderSlot, receiverSlot, cardIndex });

            // Master 本地也执行（如果 Master 是接收方）
            if (receiverSlot == _mySlot)
            {
                Card card = _deck.GetCardByIndex(cardIndex);
                ExecuteCardArrive(senderSlot, card);
            }

            // 如果 Master 是发送方，本地也要移除手牌
            if (senderSlot == _mySlot)
            {
                Card card = _deck.GetCardByIndex(cardIndex);
                _playerHand.Remove(card);
                _cardCounter?.Refresh();
                _handArea?.NotifyHandChanged();
            }
        }

        private void HandleCardArrive(object[] data)
        {
            if (data.Length < 3) return;
            int senderSlot = SafeInt(data[0]);
            int receiverSlot = SafeInt(data[1]);
            int cardIndex = SafeInt(data[2]);

            // 只有接收方执行
            if (receiverSlot != _mySlot) return;
            // Master 已在 MasterHandleCardTransfer 中执行，跳过
            if (_net.IsMasterClient) return;

            Card card = _deck.GetCardByIndex(cardIndex);
            ExecuteCardArrive(senderSlot, card);
        }

        private void ExecuteCardArrive(int senderSlot, Card card)
        {
            // 触发事件，由 GameBootstrapper 将牌放入暂存槽
            OnCardArrived?.Invoke(senderSlot, card);
        }

        private void HandleCardTakeOnMaster(object[] data, int senderActor)
        {
            if (data.Length < 1) return;
            int takerSlot = SafeInt(data[0]);
            MasterHandleCardTake(takerSlot);
        }

        private void MasterHandleCardTake(int takerSlot)
        {
            // 广播取牌（所有客户端清空暂存槽）
            _net.SendToAll(NetworkProtocol.CARD_TAKE, new object[] { takerSlot });
        }

        private void HandleCardTake(object[] data)
        {
            if (data.Length < 1) return;
            int takerSlot = SafeInt(data[0]);
            // 只有取牌方执行（其他客户端的暂存槽本来就不是给他们的）
            if (takerSlot != _mySlot) return;
            OnCardTaken?.Invoke(takerSlot);
        }

        /// <summary>找到同阵营的队友槽位</summary>
        private int FindTeammateSlot(int senderSlot)
        {
            // 队友：同阵营（非地主）的另一个玩家
            bool senderIsLandlord = false;
            if (senderSlot >= 0 && senderSlot < _baseBuildings.Length)
            {
                var cu = _baseBuildings[senderSlot]?.GetComponent<CardUnit>();
                if (cu != null) senderIsLandlord = cu.IsLandlord;
            }

            for (int i = 0; i < _baseBuildings.Length; i++)
            {
                if (i == senderSlot) continue;
                var cu = _baseBuildings[i]?.GetComponent<CardUnit>();
                if (cu == null) continue;
                // 队友 = 同阵营（都是地主或都不是地主）
                if (cu.IsLandlord == senderIsLandlord) return i;
            }
            return -1;
        }

        /// <summary>飞筒传牌到达事件（senderSlot, card）</summary>
        public event Action<int, Card> OnCardArrived;

        /// <summary>飞筒取牌事件（takerSlot）</summary>
        public event Action<int> OnCardTaken;

        // ─── 经济同步 ───

        public void BroadcastGoldUpdate(int slot, float gold)
        {
            if (_net.IsMasterClient)
            {
                float incomeRate = _slotEconomies.ContainsKey(slot) ? _slotEconomies[slot].IncomeRate : 5f;
                _net.SendToAll(NetworkProtocol.GOLD_UPDATE, new object[] { slot, gold, incomeRate });
            }
        }

        private void HandleGoldUpdate(object[] data)
        {
            if (data.Length < 2) return;
            int slot = SafeInt(data[0]);
            float gold = SafeFloat(data[1]);
            float incomeRate = data.Length > 2 ? SafeFloat(data[2]) : -1f;

            // 客户端忽略自身槽位的金币覆盖（客户端是自身金币的权威来源，防止 Master 广播覆盖自动收入）
            if (slot == _mySlot && !_net.IsMasterClient)
                return;

            if (slot == _mySlot && _economyManager != null)
            {
                _economyManager.SetGold(gold);
                if (incomeRate > 0f)
                    _economyManager.SetIncomeRate(incomeRate);
            }

            // 更新 Master 端追踪
            if (_slotEconomies.ContainsKey(slot))
            {
                _slotEconomies[slot].SetGold(gold);
                if (incomeRate > 0f)
                    _slotEconomies[slot].SetIncomeRate(incomeRate);
            }
        }

        // ─── 领域 pending 状态同步 ───

        /// <summary>请求同步领域待激活状态（所有客户端设置/取消 _isDomainPending）</summary>
        public void RequestDomainPending(bool pending)
        {
            if (!_initialized || _net == null) return;
            if (_net.IsMasterClient)
            {
                _domainSystem?.SetDomainPending();
                if (!pending) _domainSystem?.CancelDomainPending();
                _net.SendToAll(NetworkProtocol.DOMAIN_PENDING, pending);
            }
            else
            {
                _net.SendToMaster(NetworkProtocol.DOMAIN_PENDING, pending);
            }
        }

        /// <summary>请求同步反制待激活状态</summary>
        public void RequestCounterPending(bool pending)
        {
            if (!_initialized || _net == null) return;
            if (_net.IsMasterClient)
            {
                _domainSystem?.SetCounterPending();
                if (!pending) _domainSystem?.CancelPending();
                _net.SendToAll(NetworkProtocol.COUNTER_PENDING, pending);
            }
            else
            {
                _net.SendToMaster(NetworkProtocol.COUNTER_PENDING, pending);
            }
        }

        // ─── 领域同步 ───

        private void HandleDomainPending(object value, int senderActor)
        {
            if (_net.IsMasterClient)
            {
                // Master 收到请求 → 广播给所有人
                bool pending = Convert.ToBoolean(value);
                if (pending) _domainSystem?.SetDomainPending();
                else _domainSystem?.CancelDomainPending();
                _net.SendToAll(NetworkProtocol.DOMAIN_PENDING, pending);
            }
            else
            {
                // Client 收到广播 → 设置本地状态
                bool pending = Convert.ToBoolean(value);
                if (pending) _domainSystem?.SetDomainPending();
                else _domainSystem?.CancelDomainPending();
            }
        }

        private void HandleCounterPending(object value, int senderActor)
        {
            if (_net.IsMasterClient)
            {
                bool pending = Convert.ToBoolean(value);
                if (pending) _domainSystem?.SetCounterPending();
                else _domainSystem?.CancelPending();
                _net.SendToAll(NetworkProtocol.COUNTER_PENDING, pending);
            }
            else
            {
                bool pending = Convert.ToBoolean(value);
                if (pending) _domainSystem?.SetCounterPending();
                else _domainSystem?.CancelPending();
            }
        }

        public void RequestDomainActivate(CardTypeResult result)
        {
            if (!_initialized || _net == null) return;

            if (_net.IsMasterClient)
            {
                MasterActivateDomain(result);
            }
            else
            {
                _net.SendToMaster(NetworkProtocol.DOMAIN_ACTIVATE,
                    NetworkProtocol.SerializeCardTypeResult(result));
            }
        }

        private void HandleDomainActivateOnMaster(object[] data, int senderActor)
        {
            int senderSlot = NetworkProtocol.GetPlayerSlot(senderActor, _actorNumbers);
            if (senderSlot < 0) return;
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(data);
            MasterActivateDomain(result);
        }

        private void MasterActivateDomain(CardTypeResult result)
        {
            if (_domainSystem == null || _domainSystem.IsDomainActive) return;

            _net.SendToAll(NetworkProtocol.DOMAIN_ACTIVATE,
                NetworkProtocol.SerializeCardTypeResult(result));

            // Master 本地执行
            _domainSystem.OnCardPlayed(result, true);
        }

        private void HandleDomainActivateBroadcast(object[] data)
        {
            if (_net.IsMasterClient) return;
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(data);
            _domainSystem?.OnCardPlayed(result, true);
        }

        public void RequestCounterActivate(CardTypeResult result)
        {
            if (!_initialized || _net == null) return;

            if (_net.IsMasterClient)
            {
                MasterActivateCounter(result);
            }
            else
            {
                _net.SendToMaster(NetworkProtocol.COUNTER_ACTIVATE,
                    NetworkProtocol.SerializeCardTypeResult(result));
            }
        }

        private void HandleCounterActivateOnMaster(object[] data, int senderActor)
        {
            int senderSlot = NetworkProtocol.GetPlayerSlot(senderActor, _actorNumbers);
            if (senderSlot < 0) return;
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(data);
            MasterActivateCounter(result);
        }

        private void MasterActivateCounter(CardTypeResult result)
        {
            if (_domainSystem == null || !_domainSystem.IsDomainActive) return;

            _net.SendToAll(NetworkProtocol.COUNTER_ACTIVATE,
                NetworkProtocol.SerializeCardTypeResult(result));

            _domainSystem.OnCardPlayed(result, false);
        }

        private void HandleCounterActivateBroadcast(object[] data)
        {
            if (_net.IsMasterClient) return;
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(data);
            _domainSystem?.OnCardPlayed(result, false);
        }

        // ─── 游戏结束 ───

        public void BroadcastGameEnd(bool playerWon, int winnerSlot)
        {
            if (!_net.IsMasterClient) return;
            // v2.0: Master 进入 END 阶段
            TransitPhase(GameSyncPhase.END);
            // 广播赢家是否是地主（而非本机是否胜利），让各客户端自行判断
            bool winnerIsLandlord = (_playerIsLandlord == playerWon);
            _net.SendToAll(NetworkProtocol.GAME_END, new object[] { winnerIsLandlord, winnerSlot });
        }

        private void HandleGameEnd(object[] data)
        {
            // Master 已通过 BattleManager.OnGameEnded 直接处理，跳过
            if (_net.IsMasterClient) return;
            if (data.Length < 2) return;

            bool winnerIsLandlord = Convert.ToBoolean(data[0]);
            int winnerSlot = SafeInt(data[1]);

            // 判断本机玩家是否胜利
            bool localWon = false;
            if (_playerIsLandlord == winnerIsLandlord)
                localWon = true;

            Debug.Log($"[NetworkGame] 游戏结束: 赢家是地主={winnerIsLandlord}, 赢家槽位={winnerSlot}, 本机胜利={localWon}");

            // v2.0: 进入 END 阶段
            TransitPhase(GameSyncPhase.END);

            // 停止状态机
            _gameStateMachine?.TransitionTo(GamePhase.GameOver);
            _gameStateMachine?.StopTimer();

            // 触发 GameBootstrapper 订阅的回调
            OnNetworkGameEnd?.Invoke(localWon);
        }

        // ─── 玩家断线 ───

        private void OnPlayerLeft(string playerName)
        {
            if (!_net.IsMasterClient) return;

            // 找到断线玩家的槽位
            int disconnectedSlot = -1;
            var currentActors = _net.GetPlayerActorNumbers();
            for (int i = 0; i < _actorNumbers.Length; i++)
            {
                bool stillConnected = false;
                foreach (var actor in currentActors)
                {
                    if (actor == _actorNumbers[i]) { stillConnected = true; break; }
                }
                if (!stillConnected) { disconnectedSlot = i; break; }
            }

            Debug.Log($"[NetworkGame] 玩家断线: {playerName}, 槽位={disconnectedSlot}");

            if (disconnectedSlot >= 0 && disconnectedSlot < _baseBuildings.Length)
            {
                var baseBldg = _baseBuildings[disconnectedSlot];
                if (baseBldg != null)
                {
                    var cu = baseBldg.GetComponent<CardUnit>();
                    if (cu != null && !cu._isBoss)
                    {
                        // 确保 BuildingAI 存在并启用
                        var ai = baseBldg.GetComponent<BuildingAI>();
                        if (ai == null)
                            ai = baseBldg.gameObject.AddComponent<BuildingAI>();

                        // 确保手牌存在（正常流程中 HandlePlayerReady 已创建同步手牌）
                        if (!_slotHands.ContainsKey(disconnectedSlot))
                        {
                            // 兜底：玩家在 PLAYER_READY 之前就断线，创建新牌堆
                            var hand = new CardHand(17);
                            var syncDeck = new DoudizhuTower.Core.Cards.CardDeck(GameSession.NetworkSeed);
                            syncDeck.Deal(disconnectedSlot * 7, new CardHand(17)); // 跳过
                            syncDeck.Deal(7, hand);
                            _slotHands[disconnectedSlot] = hand;
                            _slotDecks[disconnectedSlot] = syncDeck;
                        }

                        // 初始化 AI 经济（保留断线玩家的实际金币）
                        EconomySystem economy;
                        if (_slotEconomies.ContainsKey(disconnectedSlot))
                        {
                            economy = _slotEconomies[disconnectedSlot];
                        }
                        else
                        {
                            var econConfig = Resources.Load<DoudizhuTower.Config.EconomyConfig>("EconomyConfig");
                            float incomeRate = econConfig != null ? econConfig.farmerBaseIncome : 5f;
                            float initGold = econConfig != null ? econConfig.initialGold : 50f;
                            economy = new EconomySystem(initGold, incomeRate);
                            _slotEconomies[disconnectedSlot] = economy;
                        }

                        ai.Initialize(_slotHands[disconnectedSlot], economy, _battleManager, _deck, 6, 3f);
                        ai.SetNetworkContext(this, disconnectedSlot);
                        ai.enabled = true;

                        Debug.Log($"[NetworkGame] 槽位 {disconnectedSlot} 已转为 AI 控制");
                    }
                }
            }

            // 广播断线槽位
            _net.SendToAll(NetworkProtocol.PLAYER_LEFT, new object[] { playerName, disconnectedSlot });
        }

        private void HandlePlayerLeft(object[] data)
        {
            string playerName = (string)data[0];
            int slot = data.Length > 1 ? SafeInt(data[1]) : -1;
            Debug.Log($"[NetworkGame] 收到断线通知: {playerName}, 槽位={slot}");
            // 客户端：标记该槽位为 AI（UI 可选显示）
        }

        private void HandleGameTimeSync(object value)
        {
            if (_gameStateMachine == null) return;

            if (_net.IsMasterClient)
            {
                // 收到客户端的时间同步请求 → 回复当前时间
                float elapsed = _gameStateMachine.ElapsedTime;
                _net.SendToAll(GAME_TIME_SYNC, new object[] { _networkGameStartTime, elapsed });
            }
            else
            {
                // 收到 Master 的时间同步广播
                var data = (object[])value;
                if (data.Length < 2) return;
                double networkStartTime = Convert.ToDouble(data[0]);
                float elapsed = SafeFloat(data[1]);

                // 单调性保护：只接受比上次更新的同步消息
                if (networkStartTime <= _lastSyncNetworkTime) return;
                _lastSyncNetworkTime = networkStartTime;

                // 将网络时间映射到本地 Time.time 坐标系
                double currentNetworkTime = PhotonNetwork.Time;
                double networkElapsed = currentNetworkTime - networkStartTime;
                float localStartTime = Time.time - (float)networkElapsed - elapsed;

                _gameStateMachine.SyncGameStartTime(localStartTime);
                Debug.Log($"[NetworkGame] 时间同步: elapsed={elapsed:F2}s, localStart={localStartTime:F2}");
            }
        }

        // ─── 客户端就绪 ───

        private void HandlePlayerReady(object[] data)
        {
            if (data.Length < 2) return;
            int slot = SafeInt(data[0]);
            float initGold = SafeFloat(data[1]);

            // 为该玩家创建/更新经济追踪（纯逻辑，无 UI）
            if (_slotEconomies.ContainsKey(slot))
            {
                // 重连：更新金币为客户端报告的值
                _slotEconomies[slot].SetGold(initGold);
                Debug.Log($"[NetworkGame] 更新玩家 {slot} 经济: 金币={initGold}");
            }
            else
            {
                var econConfig = Resources.Load<DoudizhuTower.Config.EconomyConfig>("EconomyConfig");
                float incomeRate = econConfig != null ? econConfig.farmerBaseIncome : 5f;
                var economy = new EconomySystem(initGold, incomeRate);
                _slotEconomies[slot] = economy;
                Debug.Log($"[NetworkGame] 注册玩家 {slot} 经济: 初始金币={initGold}");
            }

            // 标记该玩家已就绪（允许后续摸牌请求）
            _playerReadyReceived.Add(slot);

            // 注册该玩家的手牌（Master 端用同步牌堆创建，每个玩家用不同偏移避免手牌重复）
            // 始终重新创建（覆盖 MasterDrawCard 自动创建的空手牌）
            {
                var hand = new CardHand(17);
                // 每个槽位跳过前面玩家已消耗的牌，确保不同玩家拿到不同手牌
                var syncDeck = new DoudizhuTower.Core.Cards.CardDeck(GameSession.NetworkSeed);
                syncDeck.Deal(slot * 7, new DoudizhuTower.Core.Cards.CardHand(17)); // 跳过
                syncDeck.Deal(7, hand);
                _slotHands[slot] = hand;
                _slotDecks[slot] = syncDeck;
                Debug.Log($"[NetworkGame] 注册玩家 {slot} 手牌: {hand.Count} 张, 跳过={slot * 7}");
            }
        }
    }
}

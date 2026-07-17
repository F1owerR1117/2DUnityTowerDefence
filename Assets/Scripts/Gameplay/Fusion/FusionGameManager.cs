using System.Collections.Generic;
using Fusion;
using UnityEngine;
using DoudizhuTower.Gameplay.Systems;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Entities;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// Fusion 游戏管理器。
    /// 替代 NetworkGameManager，基于 Tick 状态机 + 双缓冲战斗系统。
    /// </summary>
    public class FusionGameManager : NetworkBehaviour
    {
        // =========================
        // Singleton（跨场景唯一身份源）
        // =========================
        public static FusionGameManager Instance { get; private set; }

        // =========================
        // 核心世界状态（唯一真相）
        // =========================
        [Networked]
        public WorldState World { get; set; }

        [Networked]
        public GamePhase State { get; set; }

        [Header("引用")]
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private ViewBinder viewBinder;
        [SerializeField] private UnitSyncManager unitSyncManager;

        // =========================
        // 本地输入缓存（UI → OnInput → Fusion 网络同步）
        // =========================
        private FusionPlayerInput _localInput;

        // =========================
        // Tick Guard（防重复执行）
        // =========================
        private int _lastTickProcessed = -1;

        // =========================
        // 输入消费栅栏（单次消费）
        // =========================
        private FusionPlayerInput _input;
        private bool _inputConsumed;

        // =========================
        // Tick 驱动计时器
        // =========================
        private int _stateStartTick;
        private int _biddingDurationTicks = 3000; // 30s × 100 tick/s
        private int _turnDurationTicks = 600;     // 6s × 100 tick/s

        /// <summary>当前阶段起始 Tick（供 UI 层计算剩余时间）</summary>
        public int StateStartTick => _stateStartTick;
        /// <summary>叫分阶段总时长 Tick（供 UI 层计算剩余时间）</summary>
        public int BiddingDurationTicks => _biddingDurationTicks;

        // =========================
        // 状态收敛条件
        // =========================
        private int _currentTurnSlot;
        private int _turnTimerTicks;

        // =========================
        // AI 节流
        // =========================
        private GamePhase _nextState;

        // =========================
        // AI 节流
        // =========================
        private int _aiTickCounter;
        private int _tickLogCount;

        // =========================
        // 双缓冲单位系统
        // =========================
        private UnitBuffer _unitBuffer;
        private CombatSystem _combatSystem;
        private AISystem _aiSystem;
        private EventBuffer _eventBuffer;
        private IntentBuffer _intentBuffer;
        private DesyncDetector _desyncDetector;
        private DesyncLogger _desyncLogger;
        private int _nextUnitId = 1;
        private int _currentTick = 0;
        private WorldState _pendingWorld;
        private DoudizhuTower.Gameplay.Battle.BattleManager _battleManager;

        // =========================
        // 叫分输入队列
        // =========================
        private struct BidInput
        {
            public int Slot;
            public int Bid;
        }
        private readonly Queue<BidInput> _bidInputs = new();

        // =========================
        // Slot 分配系统（Phase 5：迁移到 LobbyIdentityService）
        // =========================
        private readonly Dictionary<PlayerRef, int> _playerToSlot = new();
        private readonly Dictionary<int, PlayerRef> _slotToPlayer = new();
        private int _nextSlot = 0;
        private readonly HashSet<int> _aiSlots = new();

        // =========================
        // 手牌管理（Host 本地维护，通过 WorldState.HandCount 同步数量）
        // =========================
        private readonly Dictionary<int, List<byte>> _slotHandCards = new();

        // =========================
        // 初始化
        // =========================
        public override void Spawned()
        {
            Debug.Log($"[FusionGameManager] ===== Spawned() 被调用 ===== HasStateAuthority={HasStateAuthority}");

            // Singleton：跨场景唯一身份源
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 所有端都需要初始化缓冲区
            _unitBuffer = new UnitBuffer();
            _combatSystem = new CombatSystem();
            _aiSystem = new AISystem();
            _eventBuffer = new EventBuffer();
            _intentBuffer = new IntentBuffer();
            _desyncDetector = new DesyncDetector();
            _desyncLogger = new DesyncLogger($"DesyncLog_{gameObject.name}.txt");

            // 查找场景中的 BattleManager（仅 Host 需要）
            if (HasStateAuthority)
            {
                _battleManager = FindFirstObjectByType<DoudizhuTower.Gameplay.Battle.BattleManager>();
                Debug.Log($"[FusionGameManager] BattleManager 查找结果: {_battleManager != null}");
            }

            // Host：为已存在的玩家补分配 slot（Lobby 阶段 FusionGameManager 未 Spawn 导致遗漏）
            if (HasStateAuthority)
            {
                AssignExistingPlayerSlots();
                InitializeGamePhase();
            }
        }

        /// <summary>
        /// 为已在房间中的玩家补分配 slot。
        /// OnlineLobby 阶段 FusionGameManager 未 Spawn，OnPlayerJoinedSlot 被跳过，
        /// 此方法在 Spawned() 时补执行。
        /// </summary>
        private void AssignExistingPlayerSlots()
        {
            if (Runner == null) return;

            // 收集所有已连接玩家，按 RawEncoded 排序确定性分配
            var players = new System.Collections.Generic.List<PlayerRef>();
            foreach (var p in Runner.ActivePlayers)
                players.Add(p);
            players.Sort((a, b) => a.RawEncoded.CompareTo(b.RawEncoded));

            for (int i = 0; i < players.Count && i < 3; i++)
            {
                var player = players[i];
                _playerToSlot[player] = i;
                _slotToPlayer[i] = player;

                if (LobbyIdentityService.Instance != null)
                {
                    LobbyIdentityService.Instance.AssignSlot(player);
                }

                Debug.Log($"[SlotAssign] (Spawned补分配) Player {player.RawEncoded} → Slot {i}");
            }
        }

        // =========================
        // Slot 分配（Phase 5：Host 唯一权威）
        // =========================

        /// <summary>
        /// 玩家加入时 Host 分配 slot（由 FusionService 转发调用）。
        /// slot = 一次性绑定，永不重算。
        /// </summary>
        public void OnPlayerJoinedSlot(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;

            int slot = -1;
            if (LobbyIdentityService.Instance != null)
            {
                slot = LobbyIdentityService.Instance.AssignSlot(player);
            }
            else
            {
                slot = _nextSlot++;
                _playerToSlot[player] = slot;
                _slotToPlayer[slot] = player;
            }

            Debug.Log($"[SlotAssign] Player {player.RawEncoded} → Slot {slot}");

            var world = World;
            var p = GetPlayer(world, slot);
            p.Slot = (byte)slot;
            p.IsAI = 0;
            SetPlayer(ref world, slot, p);

            // 写入 PlayerRef→Slot 映射供 Client 读取
            byte refByte = (byte)(player.RawEncoded & 0xFF);
            switch (slot)
            {
                case 0: world.Game.Slot0PlayerRef = refByte; break;
                case 1: world.Game.Slot1PlayerRef = refByte; break;
                case 2: world.Game.Slot2PlayerRef = refByte; break;
            }

            World = world;
        }

        /// <summary>
        /// 玩家离开时清理 slot（由 FusionService 转发调用）。
        /// </summary>
        public void OnPlayerLeftSlot(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;

            if (LobbyIdentityService.Instance != null)
            {
                LobbyIdentityService.Instance.RemoveSlot(player);
            }
            else
            {
                if (_playerToSlot.TryGetValue(player, out int slot))
                {
                    Debug.Log($"[SlotAssign] Player {player.RawEncoded} left, Slot {slot} freed");
                    _playerToSlot.Remove(player);
                    _slotToPlayer.Remove(slot);
                }
            }
        }

        /// <summary>
        /// Host 分配 AI slot（空位填充）。
        /// AI = slot 的属性，不是网络实体。
        /// </summary>
        public void AssignAISlots(int aiCount)
        {
            if (!Object.HasStateAuthority) return;

            var world = World;
            int assigned = 0;

            for (int slot = 0; slot < 3 && assigned < aiCount; slot++)
            {
                if (_slotToPlayer.ContainsKey(slot)) continue;
                if (GetPlayer(world, slot).IsAI == 1) continue;

                var p = GetPlayer(world, slot);
                p.Slot = (byte)slot;
                p.IsAI = 1;
                SetPlayer(ref world, slot, p);

                assigned++;
                Debug.Log($"[SlotAssign] AI → Slot {slot}");
            }

            World = world;
        }

        /// <summary>添加 AI 槽位（Host 调用，Fusion 自动同步）</summary>
        public void AddAISlot(int slot)
        {
            if (!HasStateAuthority) return;
            if (slot < 0 || slot >= 3) return;

            var world = World;
            var p = GetPlayer(world, slot);
            if (p.IsAI == 1) return; // 已是 AI

            p.Slot = (byte)slot;
            p.IsAI = 1;
            SetPlayer(ref world, slot, p);
            World = world;

            Debug.Log($"[FusionGameManager] AI added → Slot {slot}");
        }

        /// <summary>移除 AI 槽位（Host 调用，Fusion 自动同步）</summary>
        public void RemoveAISlot(int slot)
        {
            if (!HasStateAuthority) return;
            if (slot < 0 || slot >= 3) return;

            var world = World;
            var p = GetPlayer(world, slot);
            if (p.IsAI == 0) return; // 不是 AI

            p.IsAI = 0;
            SetPlayer(ref world, slot, p);
            World = world;

            Debug.Log($"[FusionGameManager] AI removed ← Slot {slot}");
        }

        /// <summary>查询槽位是否为 AI（所有机器可读）</summary>
        public bool IsAISlotByState(int slot)
        {
            var world = World;
            return GetPlayer(world, slot).IsAI == 1;
        }

        /// <summary>
        /// 获取玩家的 slot（通用查询）。
        /// </summary>
        public int GetSlot(PlayerRef player)
        {
            if (_playerToSlot == null) return -1;
            return _playerToSlot.TryGetValue(player, out int slot) ? slot : -1;
        }

        /// <summary>
        /// 获取本机玩家的 slot。
        /// 优先 IdentityService，回退到 WorldState 映射。
        /// </summary>
        public int GetLocalSlot()
        {
            // 优先 IdentityService
            if (IdentityService.Instance != null && IdentityService.Instance.IsReady())
            {
                int slot = IdentityService.Instance.GetLocalSlot();
                if (slot >= 0) return slot;
            }

            // 回退：Client 通过 WorldState 中的 PlayerRef 映射查找自己的 slot
            if (!HasStateAuthority && Runner != null)
            {
                var world = World;
                byte myRef = (byte)(Runner.LocalPlayer.RawEncoded & 0xFF);
                if (world.Game.Slot0PlayerRef == myRef) return 0;
                if (world.Game.Slot1PlayerRef == myRef) return 1;
                if (world.Game.Slot2PlayerRef == myRef) return 2;
            }

            // Host 始终是 slot 0
            if (HasStateAuthority) return 0;

            return -1;
        }

        /// <summary>
        /// 本机 slot 是否已就绪。
        /// </summary>
        public bool IsLocalSlotReady
        {
            get { return IdentityService.Instance != null && IdentityService.Instance.IsReady(); }
        }

        /// <summary>
        /// 获取 slot 对应的 PlayerRef。
        /// </summary>
        public PlayerRef GetPlayerRef(int slot)
        {
            return _slotToPlayer.TryGetValue(slot, out var player) ? player : default;
        }

        /// <summary>
        /// 判断 slot 是否为 AI。
        /// </summary>
        public bool IsAISlot(int slot) => _aiSlots.Contains(slot);

        // =========================
        // 输入缓存（UI → IntentBuffer → FusionPlayerInput → 网络同步）
        // =========================

        /// <summary>设置叫分输入（UI 调用）</summary>
        public void SetBidInput(int slot, int bidValue)
        {
            Debug.Log($"[FusionGameManager] SetBidInput called: slot={slot}, bid={bidValue}");
            _localInput = new FusionPlayerInput
            {
                Action = 3,
                Slot = (byte)slot,
                DataLength = 1,
                D0 = (byte)bidValue
            };
        }

        /// <summary>设置摸牌输入（UI 调用）</summary>
        public void SetDrawInput()
        {
            Debug.Log($"[FusionGameManager] SetDrawInput called, Instance={Instance != null}");
            _localInput = new FusionPlayerInput
            {
                Action = 2
            };
        }

        /// <summary>设置出牌输入（UI 调用，通过 IntentBuffer 序列化）</summary>
        public void SetPlayCardInput(byte[] cardIndices, int routeIndex, int baseIndex)
        {
            if (cardIndices == null || cardIndices.Length == 0) return;

            // 业务层：先写入 IntentBuffer
            int slot = GetLocalSlot();
            _intentBuffer.AddPlayCard(slot, cardIndices, routeIndex, baseIndex);

            // 传输层：序列化元数据到 FusionPlayerInput
            var data = new byte[3 + cardIndices.Length];
            data[0] = (byte)cardIndices.Length;
            data[1] = (byte)routeIndex;
            data[2] = (byte)baseIndex;
            System.Array.Copy(cardIndices, 0, data, 3, cardIndices.Length);

            _localInput = new FusionPlayerInput
            {
                Action = 1,
                Slot = (byte)slot
            };
            _localInput.SetData(data);
        }

        /// <summary>设置领域激活输入（UI 调用）</summary>
        public void SetDomainInput()
        {
            _localInput = new FusionPlayerInput
            {
                Action = 4
            };
        }

        // =========================
        // 本地输入缓存（UI → FusionService.OnInput → Fusion 网络同步）
        // =========================

        /// <summary>供 FusionService.OnInput 调用，设置本机输入缓存</summary>
        public void SetLocalInput(FusionPlayerInput input)
        {
            _localInput = input;
        }

        /// <summary>供 FusionService.OnInput 调用，读取本机输入缓存（不清除，由 ReadInputOnce 统一消费）</summary>
        public bool TryGetLocalInput(out FusionPlayerInput input)
        {
            if (_localInput.Action != 0)
            {
                input = _localInput;
                Debug.Log($"[FusionGameManager] TryGetLocalInput: Action={input.Action} Slot={input.Slot}");
                // Client 端没有 ReadInputOnce 消费，此处清空防重复
                if (!HasStateAuthority)
                    _localInput = default;
                return true;
            }
            input = default;
            return false;
        }

        private void InitializeGamePhase()
        {
            State = GamePhase.Bidding;
            _nextState = State;
            OnStateChanged(GamePhase.Lobby, GamePhase.Bidding);
            var world = World;

            // 从 GameSession 读取叫分结果（桥接层）
            if (GameSession.HasResult)
            {
                world.Game.Seed = GameSession.NetworkSeed;
                world.Game.Phase = 0;
                world.Game.TurnSlot = 0;
                world.Game.DeckCount = 54;

                int landlordSlot = GameSession.LandlordSlot;

                // 创建同步牌堆，发初始手牌
                var syncDeck = new DoudizhuTower.Core.Cards.CardDeck(GameSession.NetworkSeed);

                for (int slot = 0; slot < 3; slot++)
                {
                    var player = CreatePlayer((byte)slot);
                    player.IsAI = (byte)(GameSession.AISlots.Contains(slot) ? 1 : 0);
                    player.Role = (slot == landlordSlot) ? (byte)1 : (byte)2;
                    if (player.Role == 1) { player.Gold = 200; player.IncomeRate = 3; }
                    else { player.Gold = 100; player.IncomeRate = 2; }

                    // 发初始手牌（每个 slot 跳过前面玩家的牌，保证手牌不重复）
                    int handCapacity = (player.Role == 1) ? 20 : 17;
                    var hand = new DoudizhuTower.Core.Cards.CardHand(handCapacity);
                    syncDeck.Deal(slot * 7, new DoudizhuTower.Core.Cards.CardHand(handCapacity)); // 跳过
                    syncDeck.Deal(7, hand);

                    // 存储到本地字典
                    var cardIndices = new List<byte>();
                    for (int i = 0; i < hand.Count; i++)
                        cardIndices.Add((byte)hand.Cards[i].DeckIndex);
                    _slotHandCards[slot] = cardIndices;

                    // 写入 WorldState（只存数量）
                    player.HandCount = (byte)hand.Count;

                    SetPlayer(ref world, slot, player);
                }

                Debug.Log($"[FusionGameManager] 从 GameSession 初始化: seed={world.Game.Seed}, landlordSlot={landlordSlot}, ais=[{string.Join(",", GameSession.AISlots)}]");
            }
            else
            {
                // 兜底：无叫分结果（单机或异常）
                world.Game.Seed = Random.Range(0, 999999);
                world.Game.Phase = 0;
                world.Game.TurnSlot = 0;
                world.Game.DeckCount = 54;

                for (int slot = 0; slot < 3; slot++)
                {
                    var player = CreatePlayer((byte)slot);
                    player.IsAI = (byte)(GameSession.AISlots.Contains(slot) ? 1 : 0);
                    SetPlayer(ref world, slot, player);
                }

                Debug.LogWarning($"[FusionGameManager] 无 GameSession 结果，使用默认值 ais=[{string.Join(",", GameSession.AISlots)}]");
            }

            // Host 写入全部 PlayerRef → Slot 映射供 Client 读取
            // 优先 _slotToPlayer（Spawned 补分配已填充），回退 LobbyIdentityService
            if (Runner != null)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    PlayerRef playerRef = default;

                    // 优先从 _slotToPlayer 查找
                    if (_slotToPlayer.TryGetValue(slot, out var pr) && pr.PlayerId != 0)
                        playerRef = pr;

                    // 回退到 LobbyIdentityService
                    if ((playerRef == null || playerRef.PlayerId == 0) && LobbyIdentityService.Instance != null)
                        playerRef = LobbyIdentityService.Instance.GetPlayer(slot);

                    // Host 自己兜底
                    if (slot == 0 && (playerRef == null || playerRef.PlayerId == 0))
                        playerRef = Runner.LocalPlayer;

                    if (playerRef != null && playerRef.PlayerId != 0)
                    {
                        byte refByte = (byte)(playerRef.RawEncoded & 0xFF);
                        switch (slot)
                        {
                            case 0: world.Game.Slot0PlayerRef = refByte; break;
                            case 1: world.Game.Slot1PlayerRef = refByte; break;
                            case 2: world.Game.Slot2PlayerRef = refByte; break;
                        }
                    }
                }
                Debug.Log($"[FusionGameManager] Host slot mapping: S0={world.Game.Slot0PlayerRef} S1={world.Game.Slot1PlayerRef} S2={world.Game.Slot2PlayerRef}");
            }

            // 释放身份初始化锁
            world.Game.IdentityReady = 1;

            World = world;
        }

        private PlayerState CreatePlayer(byte slot)
        {
            return new PlayerState
            {
                Slot = slot,
                Gold = 0,
                IncomeRate = 1,
                HandCount = 0,
                IsLandlord = 0
            };
        }

        // =========================
        // 单位管理（Simulation 层）
        // =========================

        /// <summary>
        /// 生成单位（只有 StateAuthority 可调用）
        /// </summary>
        public int SpawnUnit(int ownerSlot, float x, float y, int hp, int atk, float attackSpeed, float moveSpeed, float attackRange, byte isLandlord)
        {
            if (!HasStateAuthority) return -1;

            var unit = new UnitState
            {
                UnitId = _nextUnitId++,
                Owner = ownerSlot,
                PosX = x,
                PosY = y,
                HP = hp,
                MaxHP = hp,
                ATK = atk,
                AttackSpeed = attackSpeed,
                TargetId = -1,
                State = UnitStateConstants.Idle,
                AttackTimer = 0f,
                MoveSpeed = moveSpeed,
                AttackRange = attackRange,
                IsLandlord = isLandlord
            };

            _eventBuffer.AddSpawn(unit.UnitId, ownerSlot);
            return _unitBuffer.Add(unit);
        }

        /// <summary>
        /// 获取单位状态（只读）
        /// </summary>
        public UnitState GetUnit(int unitId)
        {
            int index = _unitBuffer.FindIndex(unitId);
            if (index == -1) return default;
            return _unitBuffer.Get(index);
        }

        /// <summary>
        /// 获取所有单位（只读）
        /// </summary>
        public System.ReadOnlySpan<UnitState> GetAllUnits()
        {
            return new System.ReadOnlySpan<UnitState>(_unitBuffer.Read, 0, _unitBuffer.Count);
        }

        // =========================
        // 主游戏循环（工业级管道）
        // =========================
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (!BeginTick()) return;

            if (_tickLogCount < 2)
            {
                Debug.Log($"[Tick] State={State} _nextState={_nextState} Tick={Runner.Tick} Phase={World.Game.Phase}");
                _tickLogCount++;
            }

            // Phase 1: 输入只读一次
            ReadInputOnce();

            // Phase 2: 状态分发
            switch (State)
            {
                case GamePhase.Lobby:
                    TickLobby();
                    break;
                case GamePhase.Bidding:
                    TickBidding();
                    break;
                case GamePhase.Playing:
                    TickPlaying();
                    break;
                case GamePhase.End:
                    break;
            }

            // Phase 3: 原子状态提交
            CommitState();

            _currentTick++;
        }

        /// <summary>Tick Guard：防止同帧重复执行</summary>
        private bool BeginTick()
        {
            if (Runner.Tick == _lastTickProcessed) return false;
            _lastTickProcessed = Runner.Tick;
            return true;
        }

        /// <summary>输入只读一次，缓存到 _input</summary>
        private void ReadInputOnce()
        {
            _inputConsumed = false;
            if (GetInput(out FusionPlayerInput input))
            {
                _input = input;
                _localInput = default;
                Debug.Log($"[ReadInput] Fusion GetInput: Action={input.Action} Slot={input.Slot}");
            }
            else if (_localInput.Action != 0)
            {
                _input = _localInput;
                Debug.Log($"[ReadInput] Fallback _localInput: Action={_localInput.Action} Slot={_localInput.Slot}");
                _localInput = default;
            }
            else
            {
                _input = default;
            }
        }

        /// <summary>消费输入（每个系统只能调用一次）</summary>
        private bool ConsumeInput(out FusionPlayerInput input)
        {
            if (_inputConsumed)
            {
                input = default;
                return false;
            }
            _inputConsumed = true;
            input = _input;
            return input.Action != 0;
        }

        /// <summary>原子状态推进（延迟到 Commit）</summary>
        private void NextState(GamePhase state)
        {
            _nextState = state;
        }

        /// <summary>提交状态变更（Tick 末尾执行）</summary>
        private void CommitState()
        {
            // 收敛器：检查状态完成条件
            TryAdvanceState();

            if (_nextState != State)
            {
                var oldState = State;
                State = _nextState;

                // 状态切换时重置所有子系统
                OnStateChanged(oldState, State);

                Debug.Log($"[STATE] 推进: {oldState} → {State}");
            }
            _nextState = State;
        }

        /// <summary>状态切换生命周期钩子（唯一写入点）</summary>
        private void OnStateChanged(GamePhase oldState, GamePhase newState)
        {
            if (oldState == newState) return;

            // 计时器唯一写入点
            _stateStartTick = Runner.Tick;

            // 同步到 WorldState 供 Client 读取
            var world = World;
            world.Game.StateStartTick = _stateStartTick;
            World = world;

            // 重置 AI 计数器
            _aiTickCounter = 0;

            // 重置输入消费状态
            _inputConsumed = true;
            _input = default;

            // 重置叫分队列
            _bidInputs.Clear();

            // 重置意图缓冲
            _intentBuffer.Clear();

            Debug.Log($"[STATE] Reset → {newState}");
        }

        /// <summary>状态收敛器（唯一判定入口）</summary>
        private void TryAdvanceState()
        {
            var world = World;

            switch (State)
            {
                case GamePhase.Bidding:
                    if (IsBiddingFinished(ref world))
                    {
                        ResolveBidding(ref world);
                        World = world;
                        // 不自动推进到 Playing，等 UI 确认按钮触发
                    }
                    break;
                case GamePhase.Playing:
                    if (IsPlayingFinished(ref world))
                    {
                        NextState(GamePhase.End);
                    }
                    break;
            }
        }

        private bool IsBiddingFinished(ref WorldState world)
        {
            // 条件1：所有玩家都叫过分
            if (world.Game.BidCount >= 3) return true;
            // 条件2：有人叫了最高分
            if (world.Game.HighestBid >= 3) return true;
            // 条件3：计时器超时
            if (Runner.Tick - _stateStartTick >= _biddingDurationTicks) return true;
            return false;
        }

        private bool IsPlayingFinished(ref WorldState world)
        {
            // Fusion 版本不主动判定结束
            // 胜负由 BattleManager.OnGameEnded 驱动
            return false;
        }

        /// <summary>叫分结果判定</summary>
        private void ResolveBidding(ref WorldState world)
        {
            int landlordSlot = world.Game.HighestBidder >= 0
                ? world.Game.HighestBidder
                : (byte)(Runner.Tick % 3);

            for (int i = 0; i < 3; i++)
            {
                var p = GetPlayer(world, i);
                p.Role = (i == landlordSlot) ? (byte)1 : (byte)2;
                p.IsLandlord = (i == landlordSlot) ? (byte)1 : (byte)0;
                SetPlayer(ref world, i, p);
            }

            world.Game.BidWinnerSlot = (byte)landlordSlot;
            world.Game.IsBiddingFinished = 1;
            world.Game.Phase = 1;
            world.Game.TurnSlot = (byte)landlordSlot;

            Debug.Log($"[ResolveBidding] 地主=slot{landlordSlot}, 最高叫分={world.Game.HighestBid}");
        }

        /// <summary>
        /// 只有StateAuthority执行：将本地状态同步到网络状态
        /// </summary>
        // =========================
        // 状态 Tick 方法
        // =========================

        private void TickLobby()
        {
            // 等待叫分开始
        }

        private void TickBidding()
        {
            var world = World;

            // 叫分已结束，跳过处理
            if (world.Game.IsBiddingFinished == 1) return;

            // 处理本地玩家输入
            if (ConsumeInput(out var input) && input.Action == 3)
            {
                int slot = input.Slot;
                int bid = input.DataLength > 0 ? input.D0 : 0;
                Debug.Log($"[TickBidding] LocalInput slot={slot} bid={bid}");
                SubmitBid(slot, bid);
            }

            // 处理叫分队列（玩家 + AI）
            while (_bidInputs.Count > 0)
            {
                var bidInput = _bidInputs.Dequeue();
                ApplyBid(ref world, bidInput.Slot, bidInput.Bid);
            }

            // 处理 AI 叫分意图（从 IntentBuffer 读取）
            while (_intentBuffer.HasBid())
            {
                var bidIntent = _intentBuffer.PopBid();
                ApplyBid(ref world, bidIntent.Slot, bidIntent.Bid);
            }

            // AI 决策（生成新意图到 IntentBuffer）
            ProcessAI();

            World = world;
            // 收敛判定在 CommitState 中统一执行
        }

        private void TickPlaying()
        {
            var world = World;

            // 处理输入
            if (ConsumeInput(out var input))
            {
                ProcessNetworkInput(ref world, input);
            }

            // 处理 AI
            ProcessAI();

            // 处理出牌队列
            ProcessPlayCards(ref world);

            // 处理领域
            ProcessDomain(ref world);

            // 处理传送
            ProcessTransfers(ref world);

            // 经济更新
            UpdateEconomy(ref world);

            // 回合推进
            UpdateTurn(ref world);

            // 战斗模拟
            _combatSystem.Simulate(_unitBuffer, _eventBuffer, Time.deltaTime);

            World = world;

            _unitBuffer.CleanupDead();
            ComputeDesyncHash();
            _unitBuffer.Swap();
        }

        /// <summary>AI 调用（叫分/战斗分别节流，绑定 State 生命周期）</summary>
        private void ProcessAI()
        {
            if (_aiSystem == null) { Debug.LogWarning("[ProcessAI] _aiSystem is null"); return; }

            int ticksSinceStateStart = Runner.Tick - _stateStartTick;
            if (ticksSinceStateStart < 20) return;

            var world = World;

            if (State == GamePhase.Bidding)
            {
                _aiTickCounter++;
                if (_aiTickCounter % 120 != 0) return;
                Debug.Log($"[ProcessAI] Bidding Tick={Runner.Tick} ticksSinceStart={ticksSinceStateStart} counter={_aiTickCounter} CurrentTurn={world.Game.CurrentBidTurn} BidCount={world.Game.BidCount}");
                _aiSystem.Simulate(world, _unitBuffer, _intentBuffer, _currentTick);
            }
            else if (State == GamePhase.Playing)
            {
                // 战斗 AI：每 240 tick（2.4s）决策一次
                _aiTickCounter++;
                if (_aiTickCounter % 240 != 0) return;
                _aiSystem.Simulate(world, _unitBuffer, _intentBuffer, _currentTick);
            }
        }

        private void SyncToNetworkState()
        {
            if (!Object.HasStateAuthority)
                return;

            var world = _pendingWorld;

            // Heartbeat + StateHash
            world.Game.TickCounter = _currentTick;
            world.Game.StateHash = ComputeStateHash(world);

            World = world;
        }

        /// <summary>
        /// 计算 WorldState 的 hash（只包含 [Networked] 同步的数据）。
        /// Host 和 Client 用相同算法，验证同步一致性。
        /// </summary>
        private int ComputeStateHash(WorldState world)
        {
            unchecked
            {
                int hash = 17;
                hash = HashAdd(hash, world.Game.Seed);
                hash = HashAdd(hash, world.Game.Phase);
                hash = HashAdd(hash, world.Game.TurnSlot);
                hash = HashAdd(hash, world.Game.DeckCount);
                hash = HashAdd(hash, world.Game.CurrentBidTurn);
                hash = HashAdd(hash, world.Game.HighestBid);
                hash = HashAdd(hash, world.Game.HighestBidder);
                hash = HashAdd(hash, world.Game.BidCount);
                hash = HashAdd(hash, world.Game.BidWinnerSlot);
                hash = HashAdd(hash, world.Game.IsBiddingFinished);
                hash = HashAdd(hash, world.Game.TickCounter);
                hash = HashAdd(hash, world.Game.DomainActive);
                hash = HashAdd(hash, world.Game.DomainType);
                hash = HashAdd(hash, world.Game.DomainSlot);
                hash = HashAddPlayer(hash, world.Player0);
                hash = HashAddPlayer(hash, world.Player1);
                hash = HashAddPlayer(hash, world.Player2);
                return hash;
            }
        }

        private int HashAdd(int hash, int value)
        {
            return hash * 31 + value;
        }

        private int HashAddPlayer(int hash, PlayerState p)
        {
            hash = HashAdd(hash, p.Slot);
            hash = HashAdd(hash, p.IsAI);
            hash = HashAdd(hash, p.Role);
            hash = HashAdd(hash, p.Bid);
            hash = HashAdd(hash, p.Gold);
            hash = HashAdd(hash, p.IncomeRate);
            hash = HashAdd(hash, p.HandCount);
            hash = HashAdd(hash, p.IsLandlord);
            return hash;
        }

        // =========================
        // Desync 检测
        // =========================
        private void ComputeDesyncHash()
        {
            if (_desyncDetector == null || _desyncLogger == null) return;

            if (_desyncDetector.ShouldComputeHash(_currentTick))
            {
                // 只 hash WorldState（[Networked] 同步的数据），不 hash UnitBuffer
                uint hash = (uint)World.Game.StateHash;

                if (Object.HasStateAuthority)
                {
                    _desyncLogger.LogHash(_currentTick, hash, "Host");
                }
                else
                {
                    _desyncLogger.LogHash(_currentTick, hash, "Client");
                }
            }
        }

        // =========================
        // 公开属性
        // =========================
        public int CurrentTick => _currentTick;
        public UnitBuffer UnitBuffer => _unitBuffer;
        private void LateUpdate()
        {
            if (viewBinder == null || _unitBuffer == null) return;

            // 处理事件（触发视觉表现）
            ProcessEvents();

            // 同步所有 View
            viewBinder.SyncAll(_unitBuffer, Object.HasStateAuthority);
        }

        /// <summary>
        /// 处理 EventBuffer 中的事件，触发视觉表现。
        /// 只在 Host 端执行（Client 通过 WorldState 同步）。
        /// </summary>
        private void ProcessEvents()
        {
            if (!Object.HasStateAuthority) return;

            for (int i = 0; i < _eventBuffer.Count; i++)
            {
                var evt = _eventBuffer.Get(i);
                switch (evt.Type)
                {
                    case EventType.Spawn:
                        OnSpawnEvent(evt);
                        break;
                    case EventType.Hit:
                        OnHitEvent(evt);
                        break;
                    case EventType.Death:
                        OnDeathEvent(evt);
                        break;
                }
            }
        }

        private void OnSpawnEvent(GameEvent evt)
        {
            // Spawn 视觉：由 ViewBinder 在 SyncAll 中处理
        }

        private void OnHitEvent(GameEvent evt)
        {
            // Hit 视觉：触发受击效果
            var view = viewBinder?.GetView(evt.TargetId);
            if (view != null)
            {
                view.PlayHitEffect();
            }
        }

        private void OnDeathEvent(GameEvent evt)
        {
            // Death 视觉：触发死亡效果
            var view = viewBinder?.GetView(evt.TargetId);
            if (view != null)
            {
                view.PlayDeathEffect();
            }
        }

        // =========================
        // 清理
        // =========================
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _desyncLogger?.Close();
            _unitBuffer?.Clear();
            _eventBuffer?.Clear();
            _intentBuffer?.Clear();
        }

        // =========================
        // 输入处理（Fusion NetworkInput 版本）
        // =========================

        /// <summary>
        /// 处理从 Fusion NetworkInput 读取的客户端输入（Host-only）。
        /// 传输层 → 业务层（IntentBuffer）→ GameLogic。
        /// </summary>
        private void ProcessNetworkInput(ref WorldState world, FusionPlayerInput netInput)
        {
            int slot = netInput.Slot;
            if (slot < 0) return;

            switch (netInput.Action)
            {
                case 1: // 出牌：从传输层反序列化到 IntentBuffer
                    {
                        var data = netInput.GetData();
                        if (data.Length < 3) break;
                        int cardCount = data[0];
                        int routeIndex = data[1];
                        int baseIndex = data[2];
                        var cardIndices = new byte[cardCount];
                        System.Array.Copy(data, 3, cardIndices, 0, cardCount);
                        _intentBuffer.AddPlayCard(slot, cardIndices, routeIndex, baseIndex);
                    }
                    break;
                case 2: // 摸牌
                    SubmitDrawCard();
                    break;
                case 3: // 叫分：从传输层反序列化
                    {
                        var data = netInput.GetData();
                        int bidValue = data.Length > 0 ? data[0] : 0;
                        SubmitBid(slot, bidValue);
                    }
                    break;
                case 4: // 领域
                    SubmitDomain(slot);
                    break;
            }
        }

        // =========================
        // 经济系统
        // =========================
        private void UpdateEconomy(ref WorldState world)
        {
            var p0 = world.Player0;
            p0.Gold += p0.IncomeRate;
            world.Player0 = p0;

            var p1 = world.Player1;
            p1.Gold += p1.IncomeRate;
            world.Player1 = p1;

            var p2 = world.Player2;
            p2.Gold += p2.IncomeRate;
            world.Player2 = p2;
        }

        // =========================
        // 回合推进
        // =========================
        private void UpdateTurn(ref WorldState world)
        {
            world.Game.TurnSlot++;
            if (world.Game.TurnSlot >= 3)
                world.Game.TurnSlot = 0;
        }

        // =========================
        // 领域系统（Phase 3：Host 权威，简化版）
        // =========================

        /// <summary>
        /// 处理领域状态。
        /// Host 权威：只有 Host 修改 DomainActive/DomainType/DomainSlot。
        /// Client 只读 WorldState 中的领域状态。
        /// </summary>
        private void ProcessDomain(ref WorldState world)
        {
            // 领域持续时间：每 100 tick 自动关闭
            if (world.Game.DomainActive == 1)
            {
                world.Game.DomainActive = 0;
                Debug.Log($"[ProcessDomain] 领域关闭: slot={world.Game.DomainSlot}");
            }
        }

        /// <summary>
        /// 激活领域（Host 调用）。
        /// </summary>
        public void ActivateDomain(int slot, byte domainType)
        {
            if (!Object.HasStateAuthority) return;

            var world = World;
            world.Game.DomainActive = 1;
            world.Game.DomainType = domainType;
            world.Game.DomainSlot = (byte)slot;
            World = world;

            Debug.Log($"[ProcessDomain] 领域激活: slot={slot}, type={domainType}");
        }

        /// <summary>
        /// 检查玩家是否被领域封印。
        /// </summary>
        public bool IsSealedByDomain(int slot)
        {
            var world = World;
            if (world.Game.DomainActive == 0) return false;
            return world.Game.DomainSlot != slot;
        }

        // =========================
        // 传送系统（Phase 3：飞筒传牌）
        // =========================

        /// <summary>
        /// 处理传送意图（Host 验证 + 执行）。
        /// </summary>
        private void ProcessTransfers(ref WorldState world)
        {
            while (_intentBuffer.HasTransfer())
            {
                var intent = _intentBuffer.PopTransfer();
                ApplyTransfer(ref world, intent);
            }
        }

        /// <summary>
        /// 执行传送（从发送方手牌移除，添加到接收方手牌）。
        /// </summary>
        private void ApplyTransfer(ref WorldState world, TransferIntent intent)
        {
            var sender = GetPlayer(world, intent.SenderSlot);

            // 验证：发送方手牌中是否有此牌
            if (!_slotHandCards.TryGetValue(intent.SenderSlot, out var senderHand))
            {
                Debug.LogWarning($"[ProcessTransfer] sender slot={intent.SenderSlot} 无手牌数据");
                return;
            }

            if (!senderHand.Contains(intent.CardDeckIndex))
            {
                Debug.LogWarning($"[ProcessTransfer] sender slot={intent.SenderSlot} 手牌中无此牌: DeckIndex={intent.CardDeckIndex}");
                return;
            }

            // 找接收方（同阵营队友）
            int receiverSlot = FindTeammateSlot(intent.SenderSlot, world);
            if (receiverSlot < 0)
            {
                Debug.LogWarning($"[ProcessTransfer] sender slot={intent.SenderSlot} 无队友");
                return;
            }

            // 从发送方移除
            senderHand.Remove(intent.CardDeckIndex);
            sender.HandCount = (byte)senderHand.Count;
            SetPlayer(ref world, intent.SenderSlot, sender);

            // 添加到接收方
            if (!_slotHandCards.TryGetValue(receiverSlot, out var receiverHand))
            {
                receiverHand = new List<byte>();
                _slotHandCards[receiverSlot] = receiverHand;
            }
            receiverHand.Add(intent.CardDeckIndex);
            var receiver = GetPlayer(world, receiverSlot);
            receiver.HandCount = (byte)receiverHand.Count;
            SetPlayer(ref world, receiverSlot, receiver);

            Debug.Log($"[ProcessTransfer] slot={intent.SenderSlot} → slot={receiverSlot}, card={intent.CardDeckIndex}");
        }

        /// <summary>
        /// 查找同阵营队友。
        /// </summary>
        private int FindTeammateSlot(int slot, WorldState world)
        {
            var player = GetPlayer(world, slot);
            bool isLandlord = player.IsLandlord == 1;

            for (int i = 0; i < 3; i++)
            {
                if (i == slot) continue;
                var other = GetPlayer(world, i);
                if (other.IsLandlord == (isLandlord ? 1 : 0))
                    return i;
            }
            return -1;
        }

        // =========================
        // 战斗系统（已移至 _combatSystem.Simulate）
        // =========================

        // =========================
        // AI 系统
        // =========================
        private void UpdateAI(ref WorldState world)
        {
            if (_aiSystem == null || _unitBuffer == null) return;

            _aiSystem.Simulate(world, _unitBuffer, _intentBuffer, _currentTick);
        }

        // =========================
        // 叫分系统（Step B：唯一状态机入口）
        // =========================

        /// <summary>叫分结果确认后，推进到 Playing 阶段（由 UI 确认按钮调用）</summary>
        public void ConfirmBidding()
        {
            if (State != GamePhase.Bidding) return;
            var world = World;
            if (world.Game.IsBiddingFinished != 1) return;
            GameSession.SetNetworkMode(true);
            GameSession.SetResultNetwork(world.Game.BidWinnerSlot, world.Game.HighestBid > 0 ? world.Game.HighestBid : 1f);
            NextState(GamePhase.Playing);
        }

        /// <summary>
        /// 提交叫分输入（本地调用）。
        /// </summary>
        public void SubmitBid(int slot, int bid)
        {
            if (State != GamePhase.Bidding) return;
            var world = World;
            if (slot != world.Game.CurrentBidTurn) return;
            _bidInputs.Enqueue(new BidInput { Slot = slot, Bid = bid });
        }

        /// <summary>
        /// Client 通过 RPC 向 Host 提交叫分。
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcSubmitBid(int slot, int bid)
        {
            Debug.Log($"[RpcSubmitBid] slot={slot} bid={bid} from={Runner.LocalPlayer}");
            SubmitBid(slot, bid);
        }

        /// <summary>
        /// Client 通过 RPC 向 Host 请求摸牌。
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcDrawCard(int slot)
        {
            Debug.Log($"[RpcDrawCard] slot={slot}");
            var world = World;
            var player = GetPlayer(world, slot);
            if (player.HandCount >= 20) return;
            if (world.Game.DeckCount <= 0) return;

            byte newCardId = (byte)((54 - world.Game.DeckCount) % 54);
            if (!_slotHandCards.TryGetValue(slot, out var handCards))
            {
                handCards = new List<byte>();
                _slotHandCards[slot] = handCards;
            }
            handCards.Add(newCardId);
            player.HandCount = (byte)handCards.Count;
            SetPlayer(ref world, slot, player);
            world.Game.DeckCount--;
            World = world;
        }

        /// <summary>
        /// Client 通过 RPC 向 Host 提交出牌。
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcPlayCard(int slot, byte[] cardIndices, int routeIndex, int baseIndex)
        {
            Debug.Log($"[RpcPlayCard] slot={slot} cards={cardIndices.Length} route={routeIndex} base={baseIndex}");
            _intentBuffer.AddPlayCard(slot, cardIndices, routeIndex, baseIndex);
        }

        // ─── 客户端同步 RPC ───

        private int _syncedDeckCount;
        private int[] _syncedGold = new int[3];

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcSyncDeckCount(int deckCount)
        {
            if (HasStateAuthority) return;
            _syncedDeckCount = deckCount;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcSyncHandCards(int slot, byte[] cardIndices)
        {
            if (HasStateAuthority) return;
            _slotHandCards[slot] = new List<byte>(cardIndices);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RpcSyncGold(int slot, int gold)
        {
            if (HasStateAuthority) return;
            if (slot >= 0 && slot < 3) _syncedGold[slot] = gold;
        }

        public int GetSyncedDeckCount() => _syncedDeckCount;
        public int GetSyncedGold(int slot) => (slot >= 0 && slot < 3) ? _syncedGold[slot] : 0;

        /// <summary>
        /// 提交摸牌请求（UI 唯一入口）。
        /// </summary>
        public void SubmitDrawCard()
        {
            if (State != GamePhase.Playing) return;

            var world = World;
            int mySlot = GetLocalSlot();
            if (mySlot < 0) return;

            var player = GetPlayer(world, mySlot);
            if (player.HandCount >= 20) return;
            if (world.Game.DeckCount <= 0) return;

            // 从牌堆摸牌
            byte newCardId = (byte)((54 - world.Game.DeckCount) % 54);
            if (!_slotHandCards.TryGetValue(mySlot, out var handCards))
            {
                handCards = new List<byte>();
                _slotHandCards[mySlot] = handCards;
            }
            handCards.Add(newCardId);
            player.HandCount = (byte)handCards.Count;
            SetPlayer(ref world, mySlot, player);
            world.Game.DeckCount--;
            World = world;

            Debug.Log($"[FusionGameManager] 摸牌: slot={mySlot}, card={newCardId}, 剩余={world.Game.DeckCount}");
        }

        /// <summary>
        /// 提交领域激活请求（UI 唯一入口）。
        /// </summary>
        public void SubmitDomain(int slot)
        {
            if (State != GamePhase.Playing) return;

            var world = World;
            if (world.Game.DomainActive == 1) return;

            world.Game.DomainActive = 1;
            world.Game.DomainSlot = (byte)slot;
            World = world;

            Debug.Log($"[FusionGameManager] 领域激活: slot={slot}");
        }

        /// <summary>
        /// 叫分状态机（Host-only，唯一执行点）。
        /// 合并 Fusion Input + AI Intent 两种来源。
        /// </summary>
        private void ProcessBidding(ref WorldState world)
        {
            if (State != GamePhase.Bidding) return;

            // ① 处理 Fusion Input 叫分
            while (_bidInputs.Count > 0)
            {
                var input = _bidInputs.Dequeue();
                ApplyBid(ref world, input.Slot, input.Bid);
            }

            // ② 处理 AI 叫分意图
            while (_intentBuffer.HasBid())
            {
                var bidIntent = _intentBuffer.PopBid();
                ApplyBid(ref world, bidIntent.Slot, bidIntent.Bid);
            }
        }

        /// <summary>
        /// 执行单次叫分（纯状态修改，无副作用）。
        /// </summary>
        private void ApplyBid(ref WorldState world, int slot, int bid)
        {
            // 防御：非叫分阶段禁止执行
            if (world.Game.Phase != 0) return;

            // 轮次校验：只能在自己的回合叫分
            if (slot != world.Game.CurrentBidTurn) return;

            var player = GetPlayer(world, slot);

            // 已叫过不能再叫
            if (player.Bid != 0) return;

            // 写入叫分
            player.Bid = (byte)bid;

            // 更新最高叫分
            if (bid > world.Game.HighestBid)
            {
                world.Game.HighestBid = (byte)bid;
                world.Game.HighestBidder = (byte)slot;
            }

            world.Game.BidCount++;
            SetPlayer(ref world, slot, player);

            Debug.Log($"[ProcessBidding] slot={slot} bid={bid} highest={world.Game.HighestBid} count={world.Game.BidCount}");

            // 推进轮次
            world.Game.CurrentBidTurn = GetNextBidSlot(world, slot);

            // 结束条件
            if (world.Game.BidCount >= 3)
            {
                world.Game.Phase = 1;
                ApplyBidResult(ref world);
            }
        }

        /// <summary>
        /// 获取下一个叫分 slot（跳过已叫过的）。
        /// </summary>
        private byte GetNextBidSlot(WorldState world, int currentSlot)
        {
            for (int i = 1; i <= 3; i++)
            {
                int next = (currentSlot + i) % 3;
                var p = GetPlayer(world, next);
                if (p.Bid == 0) return (byte)next;
            }
            return (byte)currentSlot;
        }

        /// <summary>
        /// 叫分结束：确定地主、设置角色。
        /// </summary>
        private void ApplyBidResult(ref WorldState world)
        {
            int landlordSlot = world.Game.HighestBidder >= 0
                ? world.Game.HighestBidder
                : Random.Range(0, 3);

            for (int i = 0; i < 3; i++)
            {
                var p = GetPlayer(world, i);
                p.Role = (i == landlordSlot) ? (byte)1 : (byte)2;
                p.IsLandlord = (i == landlordSlot) ? (byte)1 : (byte)0;
                SetPlayer(ref world, i, p);
            }

            // 设置叫分结束状态（Fusion 自动同步到 Client）
            world.Game.BidWinnerSlot = (byte)landlordSlot;
            world.Game.IsBiddingFinished = 1;
            world.Game.Phase = 1;

            Debug.Log($"[ProcessBidding] 叫分结束: 地主=slot{landlordSlot}, 最高叫分={world.Game.HighestBid}");

            // 推进状态机到出牌阶段
            NextState(GamePhase.Playing);
        }

        // =========================
        // 出牌系统（Phase 1：手牌验证 + 执行）
        // =========================

        /// <summary>
        /// 处理出牌意图（Host 验证 + 执行）。
        /// 由 RunSimulation 调用，合并 IntentBuffer 中的所有出牌意图。
        /// </summary>
        private void ProcessPlayCards(ref WorldState world)
        {
            while (_intentBuffer.HasPlayCard())
            {
                var intent = _intentBuffer.PopPlayCard();
                ApplyPlayCards(ref world, intent);
            }
        }

        /// <summary>
        /// 执行单次出牌（纯状态修改，无副作用）。
        /// </summary>
        private void ApplyPlayCards(ref WorldState world, PlayCardIntent intent)
        {
            if (intent.CardDeckIndices == null || intent.CardDeckIndices.Length == 0) return;

            var player = GetPlayer(world, intent.Slot);

            // 获取手牌
            if (!_slotHandCards.TryGetValue(intent.Slot, out var handCards))
            {
                Debug.LogWarning($"[ProcessPlayCards] slot={intent.Slot} 无手牌数据");
                return;
            }

            // 验证：手牌中是否有这些牌
            foreach (var cardIndex in intent.CardDeckIndices)
            {
                if (!handCards.Contains(cardIndex))
                {
                    Debug.LogWarning($"[ProcessPlayCards] slot={intent.Slot} 手牌中无此牌: DeckIndex={cardIndex}");
                    return;
                }
            }

            // 验证：金币是否足够（简化：出牌消耗金币 = 牌数 × 10）
            int cost = intent.CardDeckIndices.Length * 10;
            if (player.Gold < cost)
            {
                Debug.LogWarning($"[ProcessPlayCards] slot={intent.Slot} 金币不足: 需要{cost}, 当前{player.Gold}");
                return;
            }

            // 执行：扣除金币
            player.Gold -= cost;

            // 执行：从手牌移除
            foreach (var cardIndex in intent.CardDeckIndices)
            {
                int idx = handCards.IndexOf(cardIndex);
                if (idx >= 0) handCards.RemoveAt(idx);
            }
            player.HandCount = (byte)handCards.Count;

            SetPlayer(ref world, intent.Slot, player);

            Debug.Log($"[ProcessPlayCards] slot={intent.Slot} 出牌 {intent.CardDeckIndices.Length} 张, 剩余 {player.HandCount} 张, 金币 {player.Gold}");

            // 连接 BattleManager 生成兵种
            if (_battleManager != null)
            {
                var cards = new DoudizhuTower.Core.Cards.Card[intent.CardDeckIndices.Length];
                var deck = new DoudizhuTower.Core.Cards.CardDeck(world.Game.Seed);
                for (int i = 0; i < intent.CardDeckIndices.Length; i++)
                    cards[i] = deck.GetCardByIndex(intent.CardDeckIndices[i]);

                var result = CardTypeDetector.Detect(cards, 6);

                // 找到该玩家的基地
                var baseBuildings = FindBaseBuildingsForSlot(intent.Slot);
                if (baseBuildings != null && baseBuildings.Count > 0)
                {
                    var routeGroup = baseBuildings[0].GetComponent<DoudizhuTower.Gameplay.Battle.RouteGroup>();
                    _battleManager.DeployCards(cards, result, routeGroup, baseBuildings[0]);
                    Debug.Log($"[ProcessPlayCards] 已调用 BattleManager.DeployCards: {result.Type}");
                }
                else
                {
                    Debug.LogWarning($"[ProcessPlayCards] slot={intent.Slot} 未找到基地");
                }
            }
        }

        private List<Component> FindBaseBuildingsForSlot(int slot)
        {
            var result = new List<Component>();
            var allBuildings = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            foreach (var cu in allBuildings)
            {
                if (cu._isBuilding && cu.OwnerPlayerId == slot)
                    result.Add(cu);
            }
            return result;
        }

        /// <summary>
        /// 获取玩家手牌 DeckIndex 列表（只读）。
        /// </summary>
        public List<byte> GetHandCards(int slot)
        {
            if (_slotHandCards.TryGetValue(slot, out var handCards))
                return handCards;
            return new List<byte>();
        }

        public byte[] GetHandCardsArray(int slot)
        {
            if (_slotHandCards.TryGetValue(slot, out var handCards))
                return handCards.ToArray();
            return null;
        }

        /// <summary>
        /// 检查玩家手牌中是否有指定牌。
        /// </summary>
        public bool HasCard(int slot, byte deckIndex)
        {
            if (_slotHandCards.TryGetValue(slot, out var handCards))
                return handCards.Contains(deckIndex);
            return false;
        }

        // =========================
        // 辅助方法
        // =========================
        private PlayerState GetPlayer(WorldState world, int slot)
        {
            switch (slot)
            {
                case 0: return world.Player0;
                case 1: return world.Player1;
                case 2: return world.Player2;
                default: return world.Player0;
            }
        }

        private void SetPlayer(ref WorldState world, int slot, PlayerState player)
        {
            switch (slot)
            {
                case 0: world.Player0 = player; break;
                case 1: world.Player1 = player; break;
                case 2: world.Player2 = player; break;
            }
        }

        /// <summary>
        /// 获取指定 slot 的 PlayerState（公共只读接口）。
        /// </summary>
        public PlayerState GetPlayerState(int slot)
        {
            var world = World;
            return GetPlayer(world, slot);
        }

        // =========================
        // View 层接口（供 CardUnitView 调用）
        // =========================

        /// <summary>
        /// 获取单位缓冲区（View 层只读）
        /// </summary>
        public UnitBuffer GetUnitBuffer()
        {
            return _unitBuffer;
        }

    }
}
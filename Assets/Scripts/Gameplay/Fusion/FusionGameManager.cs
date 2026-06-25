using System.Collections.Generic;
using Fusion;
using UnityEngine;
using DoudizhuTower.Gameplay.Systems;

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

        [Header("引用")]
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private ViewBinder viewBinder;
        [SerializeField] private UnitSyncManager unitSyncManager;

        // =========================
        // 本地输入缓存（UI → OnInput → Fusion 网络同步）
        // =========================
        private FusionPlayerInput _localInput;

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

            // 只有 Host 初始化游戏状态
            if (HasStateAuthority)
            {
                InitializeGameState();
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

            // Phase 5：slot 分配委托给 LobbyIdentityService
            int slot = -1;
            if (LobbyIdentityService.Instance != null)
            {
                slot = LobbyIdentityService.Instance.AssignSlot(player);
            }
            else
            {
                // 兜底：LobbyIdentityService 未创建时本地分配
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
                if (_aiSlots.Contains(slot)) continue;

                _aiSlots.Add(slot);

                var p = GetPlayer(world, slot);
                p.Slot = (byte)slot;
                p.IsAI = 1;
                SetPlayer(ref world, slot, p);

                assigned++;
                Debug.Log($"[SlotAssign] AI → Slot {slot}");
            }

            World = world;
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
        /// 获取本机玩家的 slot（Phase 5：委托给 IdentityService）。
        /// </summary>
        public int GetLocalSlot()
        {
            return IdentityService.Instance != null
                ? IdentityService.Instance.GetLocalSlot()
                : -1;
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
        // 输入缓存（UI → Fusion NetworkInput）
        // =========================

        /// <summary>设置叫分输入（UI 调用）</summary>
        public void SetBidInput(int bidValue)
        {
            _localInput = new FusionPlayerInput
            {
                Action = 3,
                BidValue = (byte)bidValue
            };
        }

        /// <summary>设置摸牌输入（UI 调用）</summary>
        public void SetDrawInput()
        {
            _localInput = new FusionPlayerInput
            {
                Action = 2
            };
        }

        /// <summary>设置出牌输入（UI 调用）</summary>
        public void SetPlayCardInput(byte[] cardIndices, int routeIndex, int baseIndex)
        {
            if (cardIndices == null || cardIndices.Length == 0) return;

            var input = new FusionPlayerInput
            {
                Action = 1,
                RouteIndex = (byte)routeIndex,
                BaseIndex = (byte)baseIndex
            };
            input.SetCards(cardIndices);
            _localInput = input;
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
        // Fusion NetworkInput 回调
        // =========================

        public override void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_localInput.Action != 0)
            {
                input.Set(_localInput);
                Debug.Log($"[FusionGameManager] OnInput sent: Action={_localInput.Action}");
                _localInput = default;
            }
        }

        private void InitializeGameState()
        {
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

                world.Player0 = CreatePlayer(0);
                world.Player1 = CreatePlayer(1);
                world.Player2 = CreatePlayer(2);

                Debug.LogWarning("[FusionGameManager] 无 GameSession 结果，使用默认值");
            }

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
        // 主游戏循环（Fusion核心）
        // =========================
        public override void FixedUpdateNetwork()
        {
            if (_currentTick % 100 == 0)
            {
                Debug.Log($"[FusionGameManager] Tick:{_currentTick} HasStateAuthority:{HasStateAuthority} IsValid:{Object.IsValid}");
            }

            // 只有 Host 执行 Simulation
            if (HasStateAuthority)
            {
                // 从 Fusion NetworkInput 读取客户端输入
                if (GetInput(out FusionPlayerInput netInput))
                {
                    Debug.Log($"[FusionGameManager] GetInput: Action={netInput.Action} Bid={netInput.BidValue}");
                    ProcessNetworkInput(ref _pendingWorld, netInput);
                }

                RunSimulation();
                SyncToNetworkState();
            }

            // Client 只读 WorldState（Fusion 自动同步）
            _currentTick++;
        }

        private void RunSimulation()
        {
            if (_currentTick % 100 == 0)
            {
                Debug.Log($"[FusionGameManager] RunSimulation 执行中 Tick:{_currentTick}");
            }

            _eventBuffer.Clear();
            _intentBuffer.Clear();

            var world = World;

            UpdateAI(ref world);
            ProcessBidding(ref world);
            ProcessPlayCards(ref world);
            ProcessDomain(ref world);
            ProcessTransfers(ref world);
            UpdateEconomy(ref world);
            UpdateTurn(ref world);
            _combatSystem.Simulate(_unitBuffer, _eventBuffer, Time.deltaTime);

            _unitBuffer.CleanupDead();
            ComputeDesyncHash();
            _unitBuffer.Swap();

            _pendingWorld = world;
        }

        /// <summary>
        /// 只有StateAuthority执行：将本地状态同步到网络状态
        /// </summary>
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
        /// </summary>
        private void ProcessNetworkInput(ref WorldState world, FusionPlayerInput netInput)
        {
            int slot = GetLocalSlot();
            if (slot < 0) return;

            switch (netInput.Action)
            {
                case 1: // 出牌
                    {
                        var cardIndices = netInput.GetCards();
                        _intentBuffer.AddPlayCard(slot, cardIndices, netInput.RouteIndex, netInput.BaseIndex);
                    }
                    break;
                case 2: // 摸牌
                    SubmitDrawCard();
                    break;
                case 3: // 叫分
                    SubmitBid(slot, netInput.BidValue);
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

        /// <summary>
        /// 提交叫分输入（UI / AI 唯一入口）。
        /// Client → Fusion Input → Host ProcessBidding。
        /// </summary>
        public void SubmitBid(int slot, int bid)
        {
            if (!Object.HasStateAuthority) return;

            _bidInputs.Enqueue(new BidInput { Slot = slot, Bid = bid });
        }

        /// <summary>
        /// 提交摸牌请求（UI 唯一入口）。
        /// </summary>
        public void SubmitDrawCard()
        {
            if (!Object.HasStateAuthority) return;

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
            if (!Object.HasStateAuthority) return;

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
            world.Game.Phase = 1; // 进入出牌阶段

            Debug.Log($"[ProcessBidding] 叫分结束: 地主=slot{landlordSlot}, 最高叫分={world.Game.HighestBid}");
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
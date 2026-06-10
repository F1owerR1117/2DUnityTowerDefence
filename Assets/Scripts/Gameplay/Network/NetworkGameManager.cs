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
    /// Master 权威架构：所有游戏逻辑由 Master 验证后广播执行。
    /// 挂载到游戏场景的 GameObject 上。
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        // ─── 注入引用（由 GameBootstrapper 调用 Initialize） ───
        private INetworkService _net;
        private BattleManager _battleManager;
        private EconomyManager _economyManager;
        private DomainSystem _domainSystem;
        private CardDeck _deck;
        private CardHand _playerHand;
        private HandArea _handArea;
        private CardCounterUI _cardCounter;
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
            _playerHand = playerHand;
            _handArea = handArea;
            _cardCounter = cardCounter;
            _playerBase = playerBase;
            _playerIsLandlord = playerIsLandlord;
            _baseBuildings = baseBuildings;

            _actorNumbers = _net.GetPlayerActorNumbers();
            _mySlot = NetworkProtocol.GetPlayerSlot(_net.LocalActorNumber, _actorNumbers);

            _net.OnCustomEvent += OnNetworkEvent;
            _net.OnPlayerLeft += OnPlayerLeft;

            // 时间同步：Master 广播 PhotonNetwork.Time，所有客户端统一游戏开始时间
            _gameStateMachine = FindFirstObjectByType<GameStateMachine>();
            SyncGameTime();

            // 注册本机玩家手牌到 _slotHands（Master 端用于验证）
            RegisterSlotHand(_mySlot, _playerHand);

            // 非房主客户端：向 Master 报告初始金币
            if (!_net.IsMasterClient)
            {
                float initGold = _economyManager != null ? _economyManager.CurrentGold : 0f;
                _net.SendToMaster(NetworkProtocol.PLAYER_READY, new object[] { _mySlot, initGold });
            }

            _initialized = true;
            Debug.Log($"[NetworkGame] 初始化完成，本机槽位={_mySlot}, IsMaster={_net.IsMasterClient}");
        }

        private void OnDestroy()
        {
            _initialized = false;
            if (_net != null)
            {
                _net.OnCustomEvent -= OnNetworkEvent;
                _net.OnPlayerLeft -= OnPlayerLeft;
            }
            OnNetworkGameEnd = null;
            _slotHands.Clear();
            _slotDecks.Clear();
            _slotEconomies.Clear();
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
                // Client 发送到 Master 验证
                _net.SendToMaster(NetworkProtocol.PLAY_CARDS, new object[]
                {
                    cardIndices, typeData, routeIndex, baseIndex
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

            // 广播给所有客户端
            _net.SendToAll(NetworkProtocol.PLAY_APPROVED, new object[]
            {
                slot, cardIndices, typeData, routeIndex, baseIndex, cost
            });

            // 广播金币变化（复用上面的 aiEconomy 变量）
            if (aiEconomy != null)
                _net.SendToAll(NetworkProtocol.GOLD_UPDATE, new object[] { slot, aiEconomy.CurrentGold });
        }

        // ─── 网络事件处理 ───

        /// <summary>安全拆箱 int（Photon 可能返回 short/byte/long）</summary>
        private static int SafeInt(object o) => Convert.ToInt32(o);

        /// <summary>安全拆箱 float</summary>
        private static float SafeFloat(object o) => Convert.ToSingle(o);

        /// <summary>安全拆箱为 object[]，null 时返回空数组</summary>
        private static object[] SafeArray(object o) => o as object[] ?? Array.Empty<object>();

        private void OnNetworkEvent(string key, object value, int senderActor)
        {
            if (!_initialized) return;
            if (value == null)
            {
                Debug.LogWarning($"[NetworkGame] 收到空消息: key={key}");
                return;
            }

            switch (key)
            {
                case NetworkProtocol.PLAY_CARDS:
                    if (_net.IsMasterClient)
                        HandlePlayCardsOnMaster(SafeArray(value), senderActor);
                    break;

                case NetworkProtocol.PLAY_APPROVED:
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
                        MasterDrawCard(SafeInt(requestData[0]));
                    }
                    else
                    {
                        HandleDrawCard(SafeArray(value));
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
                    if (_net.IsMasterClient)
                    {
                        HandlePlayerReady((object[])value);
                        // 新玩家加入后自动发送时间同步
                        float elapsed = _gameStateMachine != null ? _gameStateMachine.ElapsedTime : 0f;
                        _net.SendToAll(GAME_TIME_SYNC, new object[] { _networkGameStartTime, elapsed });
                    }
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

            MasterValidateAndPlay(senderSlot, cardIndices, typeData, routeIndex, baseIndex);
        }

        private void MasterValidateAndPlay(int playerSlot, int[] cardIndices, object[] typeData, int routeIndex, int baseIndex)
        {
            // 反序列化
            Card[] cards = NetworkProtocol.DeserializeCards(cardIndices, _deck);
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(typeData);

            // 手牌验证：仅 Master 自己的手牌可本地验证，其他玩家跳过（Master 无法追踪远程手牌）
            if (playerSlot == _mySlot)
            {
                CardHand hand = _playerHand;
                foreach (var card in cards)
                {
                    if (!hand.Contains(card))
                    {
                        Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 手牌中无此牌: {card}");
                        return;
                    }
                }
            }

            // 验证金币（使用该槽位对应的经济系统）
            float cost = CardCostCalculator.CalculateTotalCost(cards, result);
            EconomySystem targetEconomy = (playerSlot == _mySlot)
                ? _economyManager?.CoreEconomy
                : (_slotEconomies.ContainsKey(playerSlot) ? _slotEconomies[playerSlot] : null);

            if (targetEconomy == null || !targetEconomy.TrySpend(cost))
            {
                Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 金币不足: {cost}");
                if (playerSlot == _mySlot)
                {
                    _handArea?.ShowInsufficientGoldFeedback(cost, targetEconomy?.CurrentGold ?? 0);
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
            _net.SendToAll(NetworkProtocol.PLAY_APPROVED, new object[]
            {
                playerSlot, cardIndices, typeData, routeIndex, baseIndex, cost
            });

            // 广播该玩家的金币变化
            if (targetEconomy != null)
                _net.SendToAll(NetworkProtocol.GOLD_UPDATE, new object[] { playerSlot, targetEconomy.CurrentGold });

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

            Card[] cards = NetworkProtocol.DeserializeCards(cardIndices, _deck);
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(typeData);
            ExecutePlayApproved(playerSlot, cards, result, routeIndex, baseIndex, cost);
        }

        private void ExecutePlayApproved(int playerSlot, Card[] cards, CardTypeResult result, int routeIndex, int baseIndex, float cost)
        {
            // 获取基地和路线
            Component sourceBase = (baseIndex >= 0 && baseIndex < _baseBuildings.Length)
                ? _baseBuildings[baseIndex] : null;
            RouteGroup routeGroup = sourceBase?.GetComponent<RouteGroup>();
            if (routeGroup != null && routeIndex >= 0)
                routeGroup.SetRouteIndex(routeIndex);

            // 扣费：仅本机玩家在非 Master 客户端扣费（Master 已在 MasterValidateAndPlay 中扣过）
            if (!_net.IsMasterClient && playerSlot == _mySlot && _economyManager != null)
                _economyManager.TrySpendGold(cost);

            // 移除手牌
            if (playerSlot == _mySlot)
            {
                // 本地玩家：从 HandArea 管理的手牌移除
                _playerHand.RemoveRange(cards);
                _deck.Discard(cards);
                _cardCounter?.Refresh();
            }
            else if (_net.IsMasterClient && _slotHands.ContainsKey(playerSlot))
            {
                // Master 端远程玩家：从追踪手牌移除
                _slotHands[playerSlot].RemoveRange(cards);
            }

            // 生成兵种（所有客户端执行）
            _battleManager?.DeployCards(cards, result, routeGroup, sourceBase);

            // 触发领域系统（isPlayer 表示是否为当前玩家视角的出牌）
            _domainSystem?.OnCardPlayed(result, true);
        }

        private void HandlePlayRejected(object[] data)
        {
            if (data.Length < 1) return;
            string reason = Convert.ToString(data[0]);
            Debug.LogWarning($"[NetworkGame] 出牌被拒绝: {reason}");

            if (reason == "金币不足")
            {
                float cost = data.Length > 1 ? SafeFloat(data[1]) : 0f;
                float currentGold = _economyManager?.CurrentGold ?? 0f;
                _handArea?.ShowInsufficientGoldFeedback(cost, currentGold);
                _economyManager?.FlashGoldText();
                AudioManager.Instance?.PlayInsufficientGold();
            }
        }

        // ─── 摸牌同步 ───

        public void RequestDrawCard()
        {
            if (!_initialized || _net == null) return;

            if (_net.IsMasterClient)
            {
                MasterDrawCard(_mySlot);
            }
            else
            {
                _net.SendToMaster(NetworkProtocol.DRAW_CARD, new object[] { _mySlot });
            }
        }

        private void HandleDrawCard(object[] data)
        {
            if (data.Length < 2) return;
            int targetSlot = SafeInt(data[0]);
            int cardIndex = SafeInt(data[1]);

            // Master 已在 MasterDrawCard 中执行，跳过
            if (_net.IsMasterClient) return;

            // 客户端：将卡牌添加到本地手牌
            if (targetSlot == _mySlot)
            {
                Card card = _deck.GetCardByIndex(cardIndex);
                _playerHand.Add(card);
                _cardCounter?.Refresh();
                _handArea?.NotifyHandChanged();
                AudioManager.Instance?.PlayDrawCard();
            }
        }

        private void MasterDrawCard(int targetSlot)
        {
            Card card;
            int cardIndex;

            if (targetSlot == _mySlot)
            {
                if (_deck == null || _deck.Remaining <= 0) return;
                card = _deck.Draw();
                cardIndex = card.DeckIndex;
                _playerHand.Add(card);
                _cardCounter?.Refresh();
            }
            else
            {
                // 远程玩家：从各自的同步牌堆摸牌（与客户端牌堆保持一致）
                var slotDeck = _slotDecks.ContainsKey(targetSlot) ? _slotDecks[targetSlot] : _deck;
                if (slotDeck == null || slotDeck.Remaining <= 0) return;
                card = slotDeck.Draw();
                cardIndex = card.DeckIndex;
                if (_slotHands.ContainsKey(targetSlot))
                    _slotHands[targetSlot].Add(card);
            }

            // 广播摸牌结果
            _net.SendToAll(NetworkProtocol.DRAW_CARD, new object[] { targetSlot, cardIndex });
        }

        // ─── 经济同步 ───

        public void BroadcastGoldUpdate(int slot, float gold)
        {
            if (_net.IsMasterClient)
                _net.SendToAll(NetworkProtocol.GOLD_UPDATE, new object[] { slot, gold });
        }

        private void HandleGoldUpdate(object[] data)
        {
            if (data.Length < 2) return;
            int slot = SafeInt(data[0]);
            float gold = SafeFloat(data[1]);
            if (slot == _mySlot && _economyManager != null)
                _economyManager.SetGold(gold);
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

            // 注册该玩家的手牌（Master 端用同步牌堆创建，保持与客户端一致）
            if (!_slotHands.ContainsKey(slot))
            {
                var hand = new CardHand(17);
                // 用相同种子创建独立牌堆，发初始 7 张牌（与客户端 GameBootstrapper 一致）
                var syncDeck = new DoudizhuTower.Core.Cards.CardDeck(GameSession.NetworkSeed);
                syncDeck.Deal(7, hand);
                _slotHands[slot] = hand;
                _slotDecks[slot] = syncDeck;
                Debug.Log($"[NetworkGame] 注册玩家 {slot} 手牌: {hand.Count} 张");
            }
        }
    }
}

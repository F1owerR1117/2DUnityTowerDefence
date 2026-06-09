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

        // 每个槽位的经济（Master 维护所有）
        private Dictionary<int, EconomySystem> _slotEconomies = new();

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

            _initialized = true;
            Debug.Log($"[NetworkGame] 初始化完成，本机槽位={_mySlot}, IsMaster={_net.IsMasterClient}");
        }

        private void OnDestroy()
        {
            if (_net != null)
            {
                _net.OnCustomEvent -= OnNetworkEvent;
                _net.OnPlayerLeft -= OnPlayerLeft;
            }
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

        // ─── 网络事件处理 ───

        private void OnNetworkEvent(string key, object value, int senderActor)
        {
            switch (key)
            {
                case NetworkProtocol.PLAY_CARDS:
                    if (_net.IsMasterClient)
                        HandlePlayCardsOnMaster((object[])value, senderActor);
                    break;

                case NetworkProtocol.PLAY_APPROVED:
                    HandlePlayApproved((object[])value);
                    break;

                case NetworkProtocol.PLAY_REJECTED:
                    HandlePlayRejected((object[])value);
                    break;

                case NetworkProtocol.DRAW_CARD:
                    HandleDrawCard((object[])value);
                    break;

                case NetworkProtocol.GOLD_UPDATE:
                    HandleGoldUpdate((object[])value);
                    break;

                case NetworkProtocol.DOMAIN_ACTIVATE:
                    if (_net.IsMasterClient)
                        HandleDomainActivateOnMaster((object[])value, senderActor);
                    else
                        HandleDomainActivateBroadcast((object[])value);
                    break;

                case NetworkProtocol.COUNTER_ACTIVATE:
                    if (_net.IsMasterClient)
                        HandleCounterActivateOnMaster((object[])value, senderActor);
                    else
                        HandleCounterActivateBroadcast((object[])value);
                    break;

                case NetworkProtocol.GAME_END:
                    HandleGameEnd((object[])value);
                    break;

                case NetworkProtocol.PLAYER_LEFT:
                    HandlePlayerLeft((object[])value);
                    break;
            }
        }

        // ─── 出牌同步 ───

        private void HandlePlayCardsOnMaster(object[] data, int senderActor)
        {
            int[] cardIndices = (int[])data[0];
            object[] typeData = (object[])data[1];
            int routeIndex = (int)data[2];
            int baseIndex = (int)data[3];

            int senderSlot = NetworkProtocol.GetPlayerSlot(senderActor, _actorNumbers);
            if (senderSlot < 0) return;

            MasterValidateAndPlay(senderSlot, cardIndices, typeData, routeIndex, baseIndex);
        }

        private void MasterValidateAndPlay(int playerSlot, int[] cardIndices, object[] typeData, int routeIndex, int baseIndex)
        {
            // 获取该玩家的手牌
            CardHand hand;
            if (playerSlot == _mySlot)
                hand = _playerHand;
            else if (_slotHands.ContainsKey(playerSlot))
                hand = _slotHands[playerSlot];
            else
            {
                Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 无手牌");
                return;
            }

            // 反序列化
            Card[] cards = NetworkProtocol.DeserializeCards(cardIndices, _deck);
            CardTypeResult result = NetworkProtocol.DeserializeCardTypeResult(typeData);

            // 验证手牌中是否有这些牌
            foreach (var card in cards)
            {
                if (!hand.Contains(card))
                {
                    Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 手牌中无此牌: {card}");
                    if (playerSlot != _mySlot)
                        _net.SendToPlayer(_actorNumbers[playerSlot], NetworkProtocol.PLAY_REJECTED, "手牌中无此牌");
                    return;
                }
            }

            // 验证金币
            float cost = CardCostCalculator.CalculateTotalCost(cards, result);
            if (_economyManager != null && !_economyManager.TrySpendGold(cost))
            {
                Debug.LogWarning($"[NetworkGame] 槽位 {playerSlot} 金币不足: {cost}");
                if (playerSlot == _mySlot)
                {
                    // Master 自身金币不足，直接触发本地反馈
                    _handArea?.ShowInsufficientGoldFeedback(cost, _economyManager.CurrentGold);
                    _economyManager.FlashGoldText();
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

            // Master 本地也执行
            ExecutePlayApproved(playerSlot, cards, result, routeIndex, baseIndex, cost);
        }

        private void HandlePlayApproved(object[] data)
        {
            int playerSlot = (int)data[0];
            int[] cardIndices = (int[])data[1];
            object[] typeData = (object[])data[2];
            int routeIndex = (int)data[3];
            int baseIndex = (int)data[4];
            float cost = Convert.ToSingle(data[5]);

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

            // 扣费（仅客户端执行，Master 已在 MasterValidateAndPlay 中扣过）
            if (!_net.IsMasterClient && _economyManager != null)
                _economyManager.TrySpendGold(cost);

            // 移除手牌（本地玩家的手牌由 HandArea 管理）
            if (playerSlot == _mySlot)
            {
                _playerHand.RemoveRange(cards);
                _deck.Discard(cards);
                _cardCounter?.Refresh();
            }

            // 生成兵种（所有客户端执行）
            _battleManager?.DeployCards(cards, result, routeGroup, sourceBase);

            // 触发领域系统（isPlayer 表示是否为当前玩家视角的出牌）
            _domainSystem?.OnCardPlayed(result, true);
        }

        private void HandlePlayRejected(object[] data)
        {
            string reason = (string)data[0];
            Debug.LogWarning($"[NetworkGame] 出牌被拒绝: {reason}");

            if (reason == "金币不足")
            {
                float cost = data.Length > 1 ? Convert.ToSingle(data[1]) : 0f;
                _handArea?.ShowInsufficientGoldFeedback(cost, _economyManager.CurrentGold);
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
            int targetSlot = (int)data[0];
            int cardIndex = (int)data[1];

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
            if (_deck == null || _deck.Remaining <= 0) return;

            Card card = _deck.Draw();
            int cardIndex = card.DeckIndex;

            if (targetSlot == _mySlot)
            {
                _playerHand.Add(card);
                _cardCounter?.Refresh();
            }
            else if (_slotHands.ContainsKey(targetSlot))
            {
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
            int slot = (int)data[0];
            float gold = Convert.ToSingle(data[1]);
            if (slot == _mySlot && _economyManager != null)
                _economyManager.SetGold(gold);
        }

        // ─── 领域同步 ───

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

        public void BroadcastGameEnd(bool isVictory, int winnerSlot)
        {
            if (_net.IsMasterClient)
                _net.SendToAll(NetworkProtocol.GAME_END, new object[] { isVictory, winnerSlot });
        }

        private void HandleGameEnd(object[] data)
        {
            bool isVictory = (bool)data[0];
            int winnerSlot = (int)data[1];
            Debug.Log($"[NetworkGame] 游戏结束: 胜利={isVictory}, 赢家槽位={winnerSlot}");
            // GameBootstrapper 的 OnGameEnded 会处理结算面板
        }

        // ─── 玩家断线 ───

        private void OnPlayerLeft(string playerName)
        {
            if (!_net.IsMasterClient) return;

            // 找到断线玩家的槽位并转为 AI
            Debug.Log($"[NetworkGame] 玩家断线: {playerName}");
            _net.SendToAll(NetworkProtocol.PLAYER_LEFT, playerName);
        }

        private void HandlePlayerLeft(object[] data)
        {
            string playerName = (string)data[0];
            Debug.Log($"[NetworkGame] 收到断线通知: {playerName}");
            // TODO: 将断线玩家基地转为 AI 控制
        }
    }
}

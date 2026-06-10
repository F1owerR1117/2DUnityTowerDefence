using System;
using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Config;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Economy;
using DoudizhuTower.Gameplay.Entities;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 建筑 AI。挂载到建筑物上，自动从自身读取 SpawnPool 和阵营。
    /// 拥有独立经济和手牌，通过 BattleManager 部署兵种。
    /// 支持使用要不起领域和反制护盾。
    /// </summary>
    public class BuildingAI : MonoBehaviour
    {
        [Header("AI 配置")]
        [SerializeField] private float decisionInterval = 4f;
        [Tooltip("要不起领域使用概率（0-1）")]
        [SerializeField] private float domainUseChance = 0.3f;
        [Tooltip("反击使用概率（0-1）")]
        [SerializeField] private float counterUseChance = 0.5f;
        [Tooltip("暂存槽取牌延迟（秒），防止牌瞬间消失")]
        [SerializeField] private float takeCardDelay = 0.5f;

        public CardHand Hand { get; set; }
        public EconomySystem Economy { get; set; }

        private BattleManager _battleManager;
        private CardDeck _deck;
        private Component _baseCtl;
        private int _maxSelection;
        private bool _isLandlord;
        private DomainSystem _domainSystem;
        private DoudizhuTower.UI.Battlefield.TempSlotUI _tempSlot;

        // 联机同步
        private DoudizhuTower.Gameplay.Network.NetworkGameManager _networkGameManager;
        private int _slotIndex = -1;

        public void SetNetworkContext(DoudizhuTower.Gameplay.Network.NetworkGameManager ngm, int slotIndex)
        {
            _networkGameManager = ngm;
            _slotIndex = slotIndex;
        }

        private float _decisionTimer;
        private float _drawTimer;
        private float _drawInterval;
        private float _incomeGrowthTimer;
        private float _takeCardTimer;

        public void SetTempSlot(DoudizhuTower.UI.Battlefield.TempSlotUI tempSlot)
        {
            _tempSlot = tempSlot;
        }

        public void Initialize(CardHand hand, EconomySystem economy, BattleManager battleManager, CardDeck deck, int maxSelection, float drawInterval)
        {
            Hand = hand;
            Economy = economy;
            _battleManager = battleManager;
            _deck = deck;
            _maxSelection = maxSelection;
            _drawInterval = drawInterval;

            // 检测阵营
            var ownerCU = GetComponent<CardUnit>();
            _isLandlord = ownerCU != null && ownerCU.IsLandlord;

            // 获取 DomainSystem
            _domainSystem = FindFirstObjectByType<DomainSystem>();

            Debug.Log($"[BuildingAI] {name} Initialize: hand={hand.Count}, gold={economy.CurrentGold}, bm={battleManager != null}, enabled={enabled}");

        }

        private bool _initLogged;

        private void Start()
        {
            _baseCtl = GetComponent<CardUnit>();
            if (_baseCtl == null)
                Debug.LogError("[BuildingAI] 需要 CardUnit(_isBuilding/_isBoss) 组件");
        }

        private void OnEnable()
        {
            Debug.Log($"[BuildingAI] {name} OnEnable");
        }

        private void Update()
        {
            if (Hand == null || Hand.Count == 0 || Economy == null)
            {
                if (!_initLogged)
                {
                    _initLogged = true;
                    Debug.LogWarning($"[BuildingAI] {name} 等待初始化: Hand={Hand != null}(count={Hand?.Count ?? -1}), Economy={Economy != null}, bm={_battleManager != null}, baseCtl={_baseCtl != null}");
                }
                // 仍然更新经济和摸牌，即使手牌为空
                if (Economy != null)
                    Economy.UpdateEconomy(Time.deltaTime);
                if (Hand != null && _deck != null)
                {
                    _drawTimer += Time.deltaTime;
                    if (_drawTimer >= _drawInterval)
                    {
                        _drawTimer = 0f;
                        if (!Hand.IsFull)
                        {
                            var card = _deck.Draw();
                            Hand.Add(card);
                            Debug.Log($"[BuildingAI] {name} 摸牌: hand={Hand.Count}");
                        }
                    }
                }
                return;
            }

            // 自动摸牌
            _drawTimer += Time.deltaTime;
            if (_drawTimer >= _drawInterval)
            {
                _drawTimer = 0f;
                if (!Hand.IsFull && _deck != null)
                {
                    var card = _deck.Draw();
                    Hand.Add(card);
                }
            }

            // 暂存槽自动取牌（队友通过飞筒传来的牌，延迟取牌让暂存槽有视觉反馈）
            if (_tempSlot != null && !_tempSlot.IsEmpty && !Hand.IsFull)
            {
                _takeCardTimer += Time.deltaTime;
                if (_takeCardTimer >= takeCardDelay)
                {
                    _takeCardTimer = 0f;
                    var held = _tempSlot.HeldCard;
                    if (held.HasValue)
                    {
                        Hand.Add(held.Value);
                        _tempSlot.Clear();
                    }
                }
            }
            else
            {
                _takeCardTimer = 0f;
            }

            // 经济增长
            _incomeGrowthTimer += Time.deltaTime;
            if (_incomeGrowthTimer >= 60f)
            {
                _incomeGrowthTimer = 0f;
                Economy.SetIncomeRate(Economy.IncomeRate + 1f);
            }

            // 出牌判定
            _decisionTimer += Time.deltaTime;
            if (_decisionTimer >= decisionInterval)
            {
                _decisionTimer = 0f;
                MakeDecision();
            }

            Economy.UpdateEconomy(Time.deltaTime);
        }

        private void MakeDecision()
        {
            if (_battleManager == null || _baseCtl == null)
            {
                Debug.LogWarning($"[BuildingAI] {name} MakeDecision 阻止: bm={_battleManager != null}, baseCtl={_baseCtl != null}");
                return;
            }
            Debug.Log($"[BuildingAI] {name} MakeDecision: hand={Hand.Count}, gold={Economy.CurrentGold:F0}");

            // ── 领域/反击决策 ──
            if (_domainSystem != null)
            {
                // AI 是地主：有概率使用要不起领域
                if (_isLandlord && !_domainSystem.IsDomainActive && !_domainSystem.IsDomainOnCooldown)
                {
                    if (UnityEngine.Random.value < domainUseChance)
                        _domainSystem.SetDomainPending();
                }

                // AI 是农民：有概率使用反击（即使手牌被封印也能反击）
                if (!_isLandlord && _domainSystem.IsDomainActive && !_domainSystem.IsCounterShieldOnCooldown)
                {
                    if (UnityEngine.Random.value < counterUseChance)
                        _domainSystem.SetCounterPending();
                }
            }

            // 检查手牌是否被封印（要不起领域/反制护盾）
            // 注意：被封印的 AI 仍然可以出牌（会自动选择未被封印的牌）
            // 但如果所有牌都被封印，则无法出牌
            if (Hand.HasSealedCards)
            {
                // 检查是否有未被封印的牌
                bool allSealed = true;
                foreach (var card in Hand.CardsList)
                {
                    if (!Hand.IsCardSealed(card))
                    {
                        allSealed = false;
                        break;
                    }
                }
                if (allSealed) return;
            }

            // 获取未被封印的牌
            var allCards = Hand.GetSortedCopy();
            var cards = new List<Card>();
            foreach (var card in allCards)
            {
                if (!Hand.IsCardSealed(card))
                    cards.Add(card);
            }
            int n = cards.Count;

            var playable = new List<(Card[] cards, CardTypeResult result, float cost)>();
            int totalEvaluated = 0;
            const int MaxEvaluations = 1000;

            // B4 优化：按 k 从大到小遍历（高费用牌型优先），且限制总评估次数
            for (int k = _maxSelection; k >= 1 && totalEvaluated < MaxEvaluations; k--)
            {
                if (k > n) continue;
                foreach (var combo in GetCombinations(cards.ToArray(), k))
                {
                    if (++totalEvaluated > MaxEvaluations) break;
                    var result = CardTypeDetector.Detect(combo, _maxSelection);
                    if (result.IsValid)
                    {
                        float cost = CardCostCalculator.CalculateTotalCost(combo, result);
                        playable.Add((combo, result, cost));
                    }
                }
            }

            if (playable.Count == 0)
            {
                Debug.LogWarning($"[BuildingAI] {name} 无有效牌型: cards={n}, evaluated={totalEvaluated}");
                _decisionTimer = 0f;
                return;
            }

            // 地主有待激活领域时，优先选非单张牌型（单张无法开启领域）
            bool preferNonSingle = _isLandlord && _domainSystem != null && _domainSystem.IsDomainPending;
            playable.Sort((a, b) =>
            {
                if (preferNonSingle)
                {
                    bool aSingle = a.result.Type == CardType.Single;
                    bool bSingle = b.result.Type == CardType.Single;
                    if (aSingle != bSingle) return aSingle ? 1 : -1;
                }
                return b.cost.CompareTo(a.cost);
            });

            // 若只有单张可用，清除领域待激活状态避免卡死
            if (preferNonSingle && playable.All(e => e.result.Type == CardType.Single))
            {
                _domainSystem.CancelDomainPending();
            }

            foreach (var entry in playable)
            {
                if (Economy.TrySpend(entry.cost))
                {
                    Hand.RemoveRange(entry.cards);
                    _deck.Discard(entry.cards);

                    var routeGroup = GetComponent<RouteGroup>();
                    int routeIndex = routeGroup != null ? routeGroup.CurrentIndex : 0;

                    // 联机模式：通过 NetworkGameManager 广播出牌
                    if (_networkGameManager != null && _slotIndex >= 0)
                    {
                        _networkGameManager.BroadcastAIPlay(_slotIndex, entry.cards, entry.result, routeIndex, _baseCtl);
                    }
                    else
                    {
                        _battleManager.DeployCards(entry.cards, entry.result, routeGroup, _baseCtl);
                    }

                    // 通知领域系统 AI 出牌（用于触发要不起领域/反制护盾）
                    if (_domainSystem != null)
                    {
                        // 传入 _isLandlord 表示 AI 是否是地主
                        _domainSystem.OnCardPlayed(entry.result, false, _isLandlord);
                    }

                    return;
                }
            }
            Debug.LogWarning($"[BuildingAI] {name} 所有牌型费用不足: gold={Economy.CurrentGold:F0}, playable={playable.Count}");
            _decisionTimer = 0f;
        }

        private static IEnumerable<Card[]> GetCombinations(Card[] cards, int k)
        {
            int n = cards.Length;
            if (k <= 0 || k > n) yield break;

            var indices = new int[k];
            for (int i = 0; i < k; i++) indices[i] = i;

            while (true)
            {
                yield return indices.Select(i => cards[i]).ToArray();

                int j = k - 1;
                while (j >= 0 && indices[j] == n - k + j) j--;
                if (j < 0) yield break;

                indices[j]++;
                for (int m = j + 1; m < k; m++)
                    indices[m] = indices[m - 1] + 1;
            }
        }
    }
}

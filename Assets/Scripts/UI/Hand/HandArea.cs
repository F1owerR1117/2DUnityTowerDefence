using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Systems;
using DoudizhuTower.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Hand
{
    public class HandArea : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private CardWidget cardPrefab;
        [SerializeField] private Button deployButton;
        [SerializeField] private TextMeshProUGUI validationLabel;
        [SerializeField] private TextMeshProUGUI handCountLabel;
        [SerializeField] private Button prevRouteButton;  // 上一条路线
        [SerializeField] private Button nextRouteButton;  // 下一条路线
        [SerializeField] private TextMeshProUGUI routeLabel; // 当前路线名
        [SerializeField] private Image routeIndicator;

        [Header("回收系统 (3换1)")]
        [SerializeField] private TextMeshProUGUI discardCounterLabel;

        [Header("手牌操作")]
        [SerializeField] private CardCounterUI cardCounter;
        [Tooltip("手牌变化时自动排序")]
        [SerializeField] private bool autoSort = true;

        [Header("手牌布局")]
        [SerializeField] private Vector2 cardSize = new(80f, 120f);
        [Tooltip("卡牌重叠极限（负值），-60=每张牌露20px")]
        [SerializeField] private float minSpacing = -60f;
        [Tooltip("卡牌最大间距（正值），控制卡牌不散开")]
        [SerializeField] private float maxSpacing = -30f;

        private SelectionValidator _validator;
        private CardHand _boundHand;
        private RouteGroup _routeGroup;
        private readonly List<CardWidget> _cardWidgets = new();
        private int _discardCount;
        private CardDeck _deck;

        // 封印状态管理（复用集合，避免临时分配）
        private readonly HashSet<Card> _sealedCards = new();
        private readonly HashSet<Card> _tempSealedCards = new();
        private readonly List<Card> _tempCardList = new();
        private bool _isSealed;

        // 防止递归调用
        private bool _isRefreshing;

        /// <summary>设置牌堆引用（由 GameBootstrapper 注入）</summary>
        public void SetDeck(CardDeck deck) => _deck = deck;

        /// <summary>玩家请求出牌事件（参数：选中牌组, 牌型结果, 路线组）</summary>
        public event Action<Card[], CardTypeResult, RouteGroup> OnPlayRequest;
        public event Action<Card> OnCardDiscarded;

        /// <summary>手牌变化事件（用于领域系统检查新牌封印状态）</summary>
        public event Action OnHandChanged;

        /// <summary>出牌前校验回调（由 GameBootstrapper 注入）。返回 false 则拒绝出牌。</summary>
        public Func<CardTypeResult, bool> PlayValidator { get; set; }

        public void Initialize(CardHand hand, int maxSelection, RouteGroup routeGroup)
        {
            _boundHand = hand;
            _routeGroup = routeGroup;
            _validator = new SelectionValidator();
            _validator.Initialize(maxSelection);
            _validator.OnSelectionChanged += OnSelectionChanged;

            if (cardPrefab != null)
            {
                var prefabRt = cardPrefab.GetComponent<RectTransform>();
                if (prefabRt != null)
                    cardSize = prefabRt.sizeDelta;
            }

            _boundHand.OnHandChanged += RefreshHand;
            RefreshHand(_boundHand.Cards as List<Card> ?? new List<Card>(_boundHand.Cards));

            AddContainerShadow();

            UpdateRouteDisplay();

            if (deployButton != null)
            {
                deployButton.interactable = false;
                deployButton.onClick.AddListener(OnDeployClicked);
            }

            // 路线切换按钮
            if (prevRouteButton != null)
                prevRouteButton.onClick.AddListener(() => { _routeGroup?.PrevRoute(); UpdateRouteDisplay(); });
            if (nextRouteButton != null)
                nextRouteButton.onClick.AddListener(() => { _routeGroup?.NextRoute(); UpdateRouteDisplay(); });
        }

        private void UpdateRouteDisplay()
        {
            if (routeLabel != null)
                routeLabel.text = _routeGroup != null ? _routeGroup.CurrentRouteName : "无路线";
            if (routeIndicator != null)
                routeIndicator.color = new Color(0.2f, 0.6f, 1.0f);
            // 单路线时禁用切换按钮
            bool multi = _routeGroup != null && _routeGroup.RouteCount > 1;
            if (prevRouteButton != null) prevRouteButton.interactable = multi;
            if (nextRouteButton != null) nextRouteButton.interactable = multi;
        }

        public void SetRouteUIVisible(bool visible)
        {
            if (prevRouteButton != null) prevRouteButton.gameObject.SetActive(visible);
            if (nextRouteButton != null) nextRouteButton.gameObject.SetActive(visible);
            if (routeLabel != null) routeLabel.gameObject.SetActive(visible);
            if (routeIndicator != null) routeIndicator.gameObject.SetActive(visible);
        }

        public void RefreshHand(List<Card> cards)
        {
            // 防止递归调用
            if (_isRefreshing) return;
            _isRefreshing = true;

            try
            {
                // 保存当前选中状态（复用列表）
                var savedSelection = _validator?.CurrentSelection?.ToList();

                // 保存当前封印状态（直接复制到临时集合，避免分配）
                _tempSealedCards.Clear();
                _tempSealedCards.UnionWith(_sealedCards);

                // 自动排序（在修改卡牌列表前排序，避免触发事件）
                if (autoSort && _boundHand != null)
                {
                    _boundHand.Sort();
                    // 重新获取排序后的卡牌列表
                    cards = _boundHand.Cards as List<Card> ?? new List<Card>(_boundHand.Cards);
                }

                foreach (var widget in _cardWidgets)
                {
                    if (widget != null) Destroy(widget.gameObject);
                }
                _cardWidgets.Clear();

                _validator?.ClearSelection();

                if (cardContainer == null || cardPrefab == null) return;

                foreach (var card in cards)
                {
                    var widget = Instantiate(cardPrefab, cardContainer);
                    widget.Bind(card);
                    widget.OnCardClicked += OnCardWidgetClicked;
                    widget.OnCardDiscardRequested += OnCardDiscardRequested;

                    var rt = widget.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.sizeDelta = cardSize;
                        rt.anchorMin = new Vector2(0.5f, 0.5f);
                        rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = Vector2.zero;
                    }

                    _cardWidgets.Add(widget);

                    // 恢复封印状态
                    if (_tempSealedCards.Contains(card))
                    {
                        widget.SetSealed(true);
                    }
                }

                // 恢复封印状态集合（移除不存在的卡牌）
                _sealedCards.Clear();
                foreach (var card in _tempSealedCards)
                {
                    if (cards.Contains(card))
                        _sealedCards.Add(card);
                }

            if (savedSelection != null && _validator != null && savedSelection.Count > 0)
            {
                var stillValid = savedSelection.Where(c => cards.Contains(c)).ToList();
                if (stillValid.Count > 0)
                    _validator.RestoreSelection(stillValid);
                foreach (var widget in _cardWidgets)
                {
                    if (_validator.CurrentSelection.Contains(widget.BoundCard))
                        widget.SetSelected(true);
                }
            }

            if (handCountLabel != null)
            {
                handCountLabel.color = Color.white;
                handCountLabel.SetText("{0}/{1}", cards.Count, _boundHand?.Capacity ?? 17);
            }

            if (cardContainer != null)
            {
                float containerWidth = cardContainer.rect.width;
                float totalCardWidth = cards.Count * cardSize.x;
                float gaps = cards.Count > 1 ? cards.Count - 1 : 1;
                float spacing = Mathf.Clamp((containerWidth - totalCardWidth) / gaps, minSpacing, maxSpacing);

                var hLayout = cardContainer.GetComponent<HorizontalLayoutGroup>();
                if (hLayout != null) { hLayout.spacing = spacing; }

                var gLayout = cardContainer.GetComponent<GridLayoutGroup>();
                if (gLayout != null) { gLayout.spacing = new Vector2(spacing, gLayout.spacing.y); }
            }

            // 注意：不在 RefreshHand 中触发 OnHandChanged，因为封印状态已经在这里恢复
            // 只在手牌真正变化时（摸牌、出牌）触发
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void OnCardWidgetClicked(CardWidget widget)
        {
            if (widget == null || _validator == null) return;

            // 封印牌不可选中
            bool isSealed = _sealedCards.Contains(widget.BoundCard);

            bool wasSelected = _validator.CurrentSelection.Contains(widget.BoundCard);
            bool result = _validator.ToggleCard(widget.BoundCard, isSealed);

            if (result)
            {
                widget.SetSelected(true);
                AudioManager.Instance?.PlayCardSelect();
            }
            else if (wasSelected)
            {
                // 已选中 → 取消选中成功
                widget.SetSelected(false);
                AudioManager.Instance?.PlayCardSelect();
            }
            else
            {
                widget.SetSelected(false);
                widget.PulseRejection();
                if (validationLabel != null)
                {
                    if (isSealed)
                    {
                        validationLabel.text = "该牌被封印，无法使用";
                        validationLabel.color = Color.red;
                    }
                    else
                    {
                        // B7: 已达上限拒绝选中，给用户视觉反馈
                        validationLabel.text = $"已达上限 ({_validator.MaxSelection} 张)";
                        validationLabel.color = Color.red;
                    }
                }
            }
        }

        private void OnCardDiscardRequested(CardWidget widget)
        {
            if (widget == null || _boundHand == null) return;
            if (_validator != null && _validator.CurrentSelection.Contains(widget.BoundCard))
                return;

            _boundHand.RemoveRange(new[] { widget.BoundCard });

            _deck?.Discard(widget.BoundCard);
            OnCardDiscarded?.Invoke(widget.BoundCard);
            _discardCount++;

            if (discardCounterLabel != null)
                discardCounterLabel.text = $"回收: {_discardCount}/3";

            if (_discardCount >= 3 && _deck != null && !_boundHand.IsFull)
            {
                _discardCount = 0;
                var newCard = _deck.Draw();
                if (newCard != null) _boundHand.Add(newCard);
            }

            if (discardCounterLabel != null)
                discardCounterLabel.text = $"回收: {_discardCount}/3";
            if (cardCounter != null)
                cardCounter.Refresh();
        }

        private void OnSelectionChanged()
        {
            if (validationLabel != null)
            {
                if (_validator.SelectionCount == 0)
                {
                    validationLabel.text = "请选择卡牌";
                    validationLabel.color = Color.gray;
                }
                else if (_validator.IsValidSelection)
                {
                    float cost = DoudizhuTower.Core.Economy.CardCostCalculator.CalculateTotalCost(
                        _validator.CurrentSelection.ToArray(), _validator.LastValidation);
                    validationLabel.text = $"牌型: {_validator.LastValidation}  金钱: {cost:F0}";
                    validationLabel.color = Color.green;
                }
                else
                {
                    validationLabel.text = "不合规牌型";
                    validationLabel.color = Color.red;
                }
            }

            if (deployButton != null)
            {
                bool canPlay = _validator.IsValidSelection
                    && (PlayValidator == null || PlayValidator(_validator.LastValidation));
                deployButton.interactable = canPlay;
            }
        }

        private void OnDeployClicked()
        {
            if (_validator == null || !_validator.IsValidSelection) return;

            var result = _validator.LastValidation;

            // 出牌前校验（领域封印检查等）
            if (PlayValidator != null && !PlayValidator(result))
            {
                AudioManager.Instance?.PlayButtonClick();
                return;
            }

            var cards = _validator.CommitSelection();

            foreach (var widget in _cardWidgets)
                widget.SetSelected(false);

            AudioManager.Instance?.PlayCardDeploy();
            OnPlayRequest?.Invoke(cards, result, _routeGroup);
        }

        private void AddContainerShadow()
        {
            var shadow = GetComponent<Shadow>();
            if (shadow == null)
                shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(6f, -8f);
        }

        /// <summary>
        /// 获取所有手牌（复用列表，避免临时分配）。
        /// 注意：返回的列表不要修改，仅用于读取。
        /// </summary>
        public List<Card> GetAllCards()
        {
            _tempCardList.Clear();
            if (_boundHand != null)
            {
                // 直接使用 CardsList 属性，避免类型转换
                _tempCardList.AddRange(_boundHand.CardsList);
            }
            return _tempCardList;
        }

        /// <summary>
        /// 获取当前选中的牌（复用列表，避免临时分配）。
        /// 注意：返回的列表不要修改，仅用于读取。
        /// </summary>
        public List<Card> GetSelectedCards()
        {
            _tempCardList.Clear();
            if (_validator?.CurrentSelection != null)
                _tempCardList.AddRange(_validator.CurrentSelection);
            return _tempCardList;
        }

        /// <summary>
        /// 设置卡牌封印状态。
        /// </summary>
        /// <param name="card">目标卡牌</param>
        /// <param name="isSealed">是否封印</param>
        public void SetCardSealed(Card card, bool isSealed)
        {
            if (isSealed)
                _sealedCards.Add(card);
            else
                _sealedCards.Remove(card);

            // 更新对应 CardWidget 的显示（使用 == 运算符避免装箱）
            foreach (var widget in _cardWidgets)
            {
                if (widget != null && widget.BoundCard == card)
                {
                    widget.SetSealed(isSealed);

                    // 封印时自动取消已选中状态，防止封印牌被部署
                    if (isSealed && _validator != null && _validator.CurrentSelection.Contains(card))
                    {
                        _validator.ToggleCard(card, false);
                        widget.SetSelected(false);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 清除所有封印状态。
        /// </summary>
        public void ClearAllSeals()
        {
            _sealedCards.Clear();
            foreach (var widget in _cardWidgets)
            {
                if (widget != null)
                    widget.SetSealed(false);
            }
        }

        /// <summary>
        /// 检查卡牌是否被封印。
        /// </summary>
        public bool IsCardSealed(Card card)
        {
            return _sealedCards.Contains(card);
        }

        /// <summary>
        /// 手牌区域是否被整体封印。
        /// </summary>
        public bool IsHandSealed => _sealedCards.Count > 0;

        /// <summary>
        /// 手动触发手牌变化事件（用于摸牌、出牌等场景）。
        /// 注意：RefreshHand 不会自动触发此事件，需要手动调用。
        /// </summary>
        public void NotifyHandChanged()
        {
            OnHandChanged?.Invoke();
        }

        /// <summary>
        /// 获取最后验证的牌型结果（用于领域系统）。
        /// </summary>
        public CardTypeResult? GetLastValidation()
        {
            if (_validator == null || !_validator.IsValidSelection)
                return null;
            return _validator.LastValidation;
        }

        /// <summary>金币不足时的视觉反馈：validationLabel 红色提示 + 已选中牌抖动</summary>
        public void ShowInsufficientGoldFeedback(float cost, float currentGold)
        {
            if (validationLabel != null)
            {
                validationLabel.text = $"金币不足！需要 {cost:F0}，当前 {Mathf.FloorToInt(currentGold)}";
                validationLabel.color = Color.red;
            }

            foreach (var widget in _cardWidgets)
            {
                if (widget != null && _validator.CurrentSelection.Contains(widget.BoundCard))
                    widget.PulseRejection();
            }

            StartCoroutine(ResetValidationLabelAfterDelay(2f));
        }

        /// <summary>手牌已满时的视觉反馈：handCountLabel 闪红 + 提示已达上限（下次 RefreshHand 自动恢复）</summary>
        public void ShowHandFullFeedback()
        {
            if (handCountLabel != null)
            {
                handCountLabel.color = Color.red;
                handCountLabel.text = "已达上限";
            }
        }

        private IEnumerator ResetValidationLabelAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            OnSelectionChanged();
        }

        private void OnEnable()
        {
            GameSession.OnRuntimeReset += ResetRuntime;
        }

        private void OnDisable()
        {
            GameSession.OnRuntimeReset -= ResetRuntime;
        }

        private void ResetRuntime()
        {
            if (_boundHand != null)
                _boundHand.OnHandChanged -= RefreshHand;
            _boundHand = null;
            foreach (var widget in _cardWidgets)
            {
                if (widget != null) Destroy(widget.gameObject);
            }
            _cardWidgets.Clear();
        }

        private void OnDestroy()
        {
            GameSession.OnRuntimeReset -= ResetRuntime;
            _validator?.ClearSelection();
            if (_boundHand != null)
                _boundHand.OnHandChanged -= RefreshHand;
        }
    }
}

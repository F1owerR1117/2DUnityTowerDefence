using DoudizhuTower.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Hand
{
    /// <summary>
    /// 单张卡牌 UI 控件。
    /// 选中时上浮 40px + 缩放 1.08x，禁用 3D 旋转（§5a.3）。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CardWidget : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        [Header("UI 引用")]
        [SerializeField] private Image cardFace;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private Image suitImage;

        [Header("花色图片")]
        [Tooltip("拖入花色 Sprite，为空时隐藏花色图标")]
        [SerializeField] private Sprite spadeSprite;
        [SerializeField] private Sprite heartSprite;
        [SerializeField] private Sprite clubSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Sprite jokerSprite;

        [Header("卡牌图片 (可选)")]
        [SerializeField] private DoudizhuTower.Config.CardSpriteDB spriteDB;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private GameObject sealOverlay;  // 封印覆盖层（锁链）

        [Header("选中动画")]
        [SerializeField] private float selectedYOffset = 40f;
        [SerializeField] private float selectedScale = 1.08f;
        [SerializeField] private float animSpeed = 10f;

        // 状态
        public Card BoundCard { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsSealed { get; private set; }  // 封印状态（要不起领域/反制护盾）

        // 动画目标
        private float _targetY;
        private float _targetScale;
        private RectTransform _rectTransform;

        // 拖拽状态
        private Canvas _rootCanvas;
        private RectTransform _canvasRect;
        private Transform _originalParent;
        private Vector2 _originalAnchoredPos;
        private bool _isDragging;
        private bool _wasDragged; // 拖拽后抑制点击
        private int _originalSiblingIndex;

        public event System.Action<CardWidget> OnCardClicked;
        public event System.Action<CardWidget> OnCardDiscardRequested;
        public event System.Action<CardWidget> OnCardDragStarted;
        public event System.Action<CardWidget> OnCardDragEnded;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _targetY = 0f;
            _targetScale = 1f;
            var img = GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }

        /// <summary>
        /// 绑定卡牌数据
        /// </summary>
        public void Bind(Card card)
        {
            BoundCard = card;
            UpdateCardDisplay();
        }

        private void UpdateCardDisplay()
        {
            if (rankText != null)
                rankText.text = BoundCard.Rank.ToDisplayString();

            if (suitImage != null)
            {
                Sprite suitSprite = BoundCard.IsJoker ? jokerSprite : BoundCard.Suit switch
                {
                    CardSuit.Spade => spadeSprite,
                    CardSuit.Heart => heartSprite,
                    CardSuit.Club => clubSprite,
                    CardSuit.Diamond => diamondSprite,
                    _ => null
                };

                if (suitSprite != null)
                {
                    suitImage.sprite = suitSprite;
                    suitImage.color = Color.white;
                    suitImage.enabled = true;
                }
                else
                {
                    suitImage.enabled = false;
                }
            }

            if (cardFace != null)
            {
                cardFace.color = Color.white;
                if (spriteDB != null)
                    cardFace.sprite = spriteDB.GetSprite(BoundCard);
            }
        }

        /// <summary>
        /// 设置选中状态（上浮 + 放大）
        /// </summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            _targetY = selected ? selectedYOffset : 0f;
            _targetScale = selected ? selectedScale : 1f;

            if (selectedIndicator != null)
                selectedIndicator.SetActive(selected);
        }

        /// <summary>
        /// 设置锁死状态（要不起领域）
        /// </summary>
        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            if (lockOverlay != null)
                lockOverlay.SetActive(locked);
        }

        /// <summary>
        /// 设置封印状态（要不起领域/反制护盾）。
        /// 封印时卡牌变灰 + 显示锁链覆盖层。
        /// </summary>
        public void SetSealed(bool isSealed)
        {
            IsSealed = isSealed;

            // 显示/隐藏锁链覆盖层
            if (sealOverlay != null)
                sealOverlay.SetActive(isSealed);

            // 变灰效果
            if (cardFace != null)
            {
                if (isSealed)
                {
                    cardFace.color = new Color(0.5f, 0.5f, 0.5f, 0.7f); // 灰色半透明
                }
                else
                {
                    // 恢复原色
                    UpdateCardDisplay();
                }
            }

            // 封印时禁用点击
            if (isSealed)
            {
                IsLocked = true;
            }
            else
            {
                // 只有在非锁定状态下才解锁
                if (lockOverlay == null || !lockOverlay.activeSelf)
                    IsLocked = false;
            }
        }

        private void Update()
        {
            if (_isDragging) { UpdateRejection(); return; }

            if (_rectTransform != null)
            {
                var pos = _rectTransform.anchoredPosition;
                pos.y = Mathf.Lerp(pos.y, _targetY, Time.deltaTime * animSpeed);
                _rectTransform.anchoredPosition = pos;

                float scale = Mathf.Lerp(_rectTransform.localScale.x, _targetScale, Time.deltaTime * animSpeed);
                _rectTransform.localScale = Vector3.one * scale;
            }

            UpdateRejection();
        }

        // B7: 选中被拒时视觉反馈（闪烁红色 + 抖动）
        private float _rejectTimer;
        private Color _originalFaceColor;
        private Vector2 _originalAnchorPos;
        private bool _rejecting;

        public void PulseRejection()
        {
            if (_rectTransform == null || _rejecting) return;
            _rejecting = true;
            _rejectTimer = 0.3f;
            _originalAnchorPos = _rectTransform.anchoredPosition;
            if (cardFace != null) _originalFaceColor = cardFace.color;
        }

        private void UpdateRejection()
        {
            if (!_rejecting) return;
            _rejectTimer -= Time.deltaTime;
            if (_rejectTimer <= 0f)
            {
                _rejecting = false;
                if (_rectTransform != null)
                    _rectTransform.anchoredPosition = _originalAnchorPos;
                if (cardFace != null) cardFace.color = _originalFaceColor;
                return;
            }

            // 快速闪烁红色
            if (cardFace != null)
                cardFace.color = (Mathf.FloorToInt(_rejectTimer * 30f) % 2 == 0) ? Color.red : _originalFaceColor;

            // 左右抖动
            float shakeX = Mathf.Sin(_rejectTimer * 80f) * 3f;
            if (_rectTransform != null)
                _rectTransform.anchoredPosition = _originalAnchorPos + new Vector2(shakeX, 0f);
        }

        #region Drag Handlers

        public void OnPointerDown(PointerEventData eventData)
        {
            // 必须实现此接口，否则拖拽事件不会触发
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsLocked) return;
            _wasDragged = true;

            // 缓存 Canvas 引用
            if (_rootCanvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    _rootCanvas = canvas.rootCanvas;
                    _canvasRect = _rootCanvas.GetComponent<RectTransform>();
                }
            }

            // 记录原始位置和父级
            _originalParent = transform.parent;
            _originalAnchoredPos = _rectTransform.anchoredPosition;
            _originalSiblingIndex = transform.GetSiblingIndex();
            _isDragging = true;

            // 临时挂到 Canvas 下以实现全局拖拽渲染层级
            if (_rootCanvas != null)
                transform.SetParent(_rootCanvas.transform, true);

            transform.SetAsLastSibling();

            // 拖拽期间禁用 CanvasGroup 的射线检测，避免挡住下方 UI
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            OnCardDragStarted?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            // 跟随指针移动
            if (_canvasRect != null && _rootCanvas != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, eventData.position, eventData.pressEventCamera, out var localPos);
                _rectTransform.localPosition = localPos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;

            // 恢复射线检测
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;

            // 恢复原始父级和位置
            transform.SetParent(_originalParent, false);
            _rectTransform.anchoredPosition = _originalAnchoredPos;
            transform.SetSiblingIndex(_originalSiblingIndex);

            // 重置动画目标（避免位置偏移）
            _targetY = IsSelected ? selectedYOffset : 0f;

            OnCardDragEnded?.Invoke(this);
        }

        #endregion

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_wasDragged) { _wasDragged = false; return; }
            if (IsLocked) return;
            if (eventData.button == PointerEventData.InputButton.Right)
                OnCardDiscardRequested?.Invoke(this);
            else
                OnCardClicked?.Invoke(this);
        }
    }
}

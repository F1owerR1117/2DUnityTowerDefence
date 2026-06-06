using System;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.UI.Components;
using DoudizhuTower.UI.Hand;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Battlefield
{
    /// <summary>
    /// 传送飞筒 UI（§4.4）。
    /// 农民拖拽 1 张单牌至飞筒触发传送，6 秒冷却。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class LaunchTubeUI : MonoBehaviour, IDropHandler
    {
        [Header("UI 引用")]
        [SerializeField] private Image tubeImage;
        [SerializeField] private CoolDownEffect coolDownEffect;
        [SerializeField] private TextMeshProUGUI cooldownLabel;
        [SerializeField] private Image glowEffect;

        [Header("冷却")]
        [SerializeField] private float cooldownDuration = 6f;

        [Header("视觉")]
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color hoverColor = new Color(0.2f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        [SerializeField] private Color rejectColor = new Color(1f, 0.2f, 0.2f, 0.8f);

        private HandArea _handArea;
        private TempSlotUI _tempSlot;
        private bool _isLocked;
        private bool _isCoolingDown;
        private float _cooldownTimer;

        /// <summary>传送成功事件（外部监听执行实际传牌逻辑）</summary>
        public event Action<Card> OnCardTransmitted;

        /// <summary>是否可用（未锁定且未冷却）</summary>
        public bool IsAvailable => !_isLocked && !_isCoolingDown;

        public void Initialize(HandArea handArea)
        {
            _handArea = handArea;
            _isLocked = false;
            _isCoolingDown = false;
            _cooldownTimer = 0f;

            if (tubeImage == null)
                tubeImage = GetComponent<Image>();

            if (tubeImage != null)
                tubeImage.color = normalColor;

            if (glowEffect != null)
                glowEffect.enabled = false;

            UpdateCooldownDisplay();
        }

        /// <summary>
        /// 设置暂存槽引用（用于拖拽前检查是否已有牌）。
        /// </summary>
        public void SetTempSlot(TempSlotUI tempSlot)
        {
            _tempSlot = tempSlot;
        }

        /// <summary>
        /// 锁定飞筒（基地被摧毁 / 地主获胜时调用）。
        /// </summary>
        public void SetLocked(bool locked)
        {
            _isLocked = locked;
            if (tubeImage != null)
                tubeImage.color = locked ? lockedColor : normalColor;

            if (locked && glowEffect != null)
                glowEffect.enabled = false;
        }

        /// <summary>
        /// 尝试传送当前手牌选中的牌。
        /// 由拖拽结束或点击触发。
        /// </summary>
        public bool TryTransmit()
        {
            if (!IsAvailable) return false;
            if (_handArea == null) return false;
            if (_tempSlot != null && !_tempSlot.IsEmpty)
            {
                ShowReject("暂存槽已有牌");
                return false;
            }

            var selected = _handArea.GetSelectedCards();
            if (selected.Count != 1)
            {
                ShowReject("只能传 1 张单牌");
                return false;
            }

            Card card = selected[0];
            // 校验：必须是单牌（非对子/三张等）
            var result = CardTypeDetector.Detect(new[] { card }, 20);
            if (result.Type != CardType.Single)
            {
                ShowReject("只能传单牌");
                return false;
            }

            // 传送成功
            OnCardTransmitted?.Invoke(card);
            StartCooldown();
            FlashGlow();
            return true;
        }

        /// <summary>
        /// IDropHandler：处理卡牌拖拽到飞筒上的情况。
        /// 需要配合 CardWidget 的拖拽实现。
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            if (!IsAvailable) return;
            if (_tempSlot != null && !_tempSlot.IsEmpty)
            {
                ShowReject("暂存槽已有牌");
                return;
            }

            // 尝试从拖拽对象获取 CardWidget
            var widget = eventData.pointerDrag?.GetComponent<CardWidget>();
            if (widget == null) return;

            // 手动触发选中该牌并尝试传送
            if (_handArea == null) return;

            var selected = _handArea.GetSelectedCards();
            // 如果拖拽的牌不在选中列表中，先检查是否只有这一张
            if (selected.Count == 0 || (selected.Count == 1 && selected[0] == widget.BoundCard))
            {
                // 单牌拖拽 → 传送
                var result = CardTypeDetector.Detect(new[] { widget.BoundCard }, 20);
                if (result.Type == CardType.Single)
                {
                    OnCardTransmitted?.Invoke(widget.BoundCard);
                    StartCooldown();
                    FlashGlow();
                }
                else
                {
                    ShowReject("只能传单牌");
                }
            }
            else
            {
                ShowReject("只能传 1 张单牌");
            }
        }

        private void StartCooldown()
        {
            _isCoolingDown = true;
            _cooldownTimer = cooldownDuration;

            if (coolDownEffect != null)
                coolDownEffect.StartCoolDown(cooldownDuration);
        }

        private void Update()
        {
            if (!_isCoolingDown) return;

            _cooldownTimer -= Time.deltaTime;
            UpdateCooldownDisplay();

            if (_cooldownTimer <= 0f)
            {
                _isCoolingDown = false;
                _cooldownTimer = 0f;

                if (tubeImage != null && !_isLocked)
                    tubeImage.color = normalColor;

                if (coolDownEffect != null)
                    coolDownEffect.StopCoolDown();
            }
        }

        private void UpdateCooldownDisplay()
        {
            if (cooldownLabel != null)
            {
                if (_isCoolingDown)
                {
                    cooldownLabel.gameObject.SetActive(true);
                    cooldownLabel.text = Mathf.CeilToInt(_cooldownTimer).ToString();
                }
                else
                {
                    cooldownLabel.gameObject.SetActive(false);
                }
            }
        }

        private void ShowReject(string message)
        {
            if (tubeImage != null)
            {
                tubeImage.color = rejectColor;
                // 恢复原色
                this.DelayedCall(0.3f, () =>
                {
                    if (tubeImage != null && !_isLocked)
                        tubeImage.color = _isCoolingDown ? lockedColor : normalColor;
                });
            }

            if (cooldownLabel != null)
            {
                cooldownLabel.gameObject.SetActive(true);
                cooldownLabel.text = message;
                cooldownLabel.color = Color.red;
                this.DelayedCall(1f, () =>
                {
                    if (cooldownLabel != null && !_isCoolingDown)
                        cooldownLabel.gameObject.SetActive(false);
                });
            }
        }

        private void FlashGlow()
        {
            if (glowEffect == null) return;
            glowEffect.enabled = true;
            this.DelayedCall(0.5f, () =>
            {
                if (glowEffect != null)
                    glowEffect.enabled = false;
            });
        }

        // 延迟调用辅助（避免引入 Coroutine 依赖）
        private void DelayedCall(float delay, Action callback)
        {
            StartCoroutine(DelayedCallCoroutine(delay, callback));
        }

        private System.Collections.IEnumerator DelayedCallCoroutine(float delay, Action callback)
        {
            yield return new WaitForSeconds(delay);
            callback?.Invoke();
        }

        private void OnDisable()
        {
            if (glowEffect != null)
                glowEffect.enabled = false;
        }
    }
}

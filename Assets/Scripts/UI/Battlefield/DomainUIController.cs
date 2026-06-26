using System;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Battlefield
{
    /// <summary>
    /// 领域 UI 统一控制器。
    /// 合并原 DomainOverlay（覆盖层 + 反击按钮）和 DomainCoolDownUI（按钮视觉 + 冷却效果）。
    ///
    /// 职责：
    /// - 覆盖层显示/隐藏（CanvasGroup alpha）
    /// - 状态文字 + 倒计时文字 + 封印特效
    /// - 领域按钮 ButtonEffect 状态 + CoolDownEffect + 文字
    /// - 反击按钮 点击事件 + 可见性 + interactable + ButtonEffect 状态 + CoolDownEffect + 文字
    ///
    /// 使用方法：
    /// 1. 挂载到 Canvas 上的 GameObject
    /// 2. 在 Inspector 中配置所有引用
    /// 3. 由 GameBootstrapper 调用 Initialize()
    /// </summary>
    public class DomainUIController : MonoBehaviour
    {
        #region Inspector 字段

        [Header("-- 覆盖层 --")]
        [Tooltip("封印/反制状态覆盖层 CanvasGroup")]
        [SerializeField] private CanvasGroup overlayGroup;
        [Tooltip("状态文字（显示封印/反制消息）")]
        [SerializeField] private TextMeshProUGUI statusText;
        [Tooltip("倒计时文字（领域/反制剩余秒数）")]
        [SerializeField] private TextMeshProUGUI timerText;
        [Tooltip("封印特效图片（颜色叠加）")]
        [SerializeField] private Image sealEffectImage;

        [Header("-- 领域按钮 --")]
        [Tooltip("领域按钮的 ButtonEffect（管理状态视觉）")]
        [SerializeField] private ButtonEffect domainButtonEffect;
        [Tooltip("领域冷却效果")]
        [SerializeField] private CoolDownEffect domainCoolDown;

        [Header("-- 反击按钮 --")]
        [Tooltip("反击按钮（控制可见性 + 点击事件）")]
        [SerializeField] private Button counterButton;
        [Tooltip("反击按钮的 ButtonEffect（管理状态视觉）")]
        [SerializeField] private ButtonEffect counterButtonEffect;
        [Tooltip("反击冷却效果")]
        [SerializeField] private CoolDownEffect counterCoolDown;

        [Header("-- 颜色配置 --")]
        [SerializeField] private Color domainColor = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        [SerializeField] private Color counterColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);

        [Header("-- 状态名称（ButtonEffect） --")]
        [SerializeField] private string stateDefault = "default";
        [SerializeField] private string statePending = "pending";
        [SerializeField] private string stateCooldown = "cooldown";

        #endregion

        #region 私有字段

        private DomainSystem _domainSystem;
        private bool _isPlayerLandlord;
        private TextMeshProUGUI _domainBtnText;
        private TextMeshProUGUI _counterBtnText;
        private string _currentDomainState;
        private string _currentCounterState;
        private Action _onCounterCoolDownComplete;

        #endregion

        #region 事件

        public event Action OnCounterButtonClicked;

        #endregion

        #region 初始化

        public void Initialize(DomainSystem domainSystem, bool isPlayerLandlord)
        {
            _domainSystem = domainSystem;
            _isPlayerLandlord = isPlayerLandlord;

            if (_domainSystem == null)
            {
                Debug.LogError("[DomainUIController] DomainSystem 为 null，无法初始化");
                return;
            }

            // 自动获取按钮文字组件
            if (domainButtonEffect != null)
                _domainBtnText = domainButtonEffect.GetComponentInChildren<TextMeshProUGUI>();
            if (counterButtonEffect != null)
                _counterBtnText = counterButtonEffect.GetComponentInChildren<TextMeshProUGUI>();

            // 订阅事件
            _domainSystem.OnDomainActivated += OnDomainActivated;
            _domainSystem.OnDomainDeactivated += OnDomainDeactivated;
            _domainSystem.OnCounterShieldActivated += OnCounterShieldActivated;
            _domainSystem.OnCounterShieldDeactivated += OnCounterShieldDeactivated;

            // 反击按钮点击事件
            if (counterButton != null)
            {
                counterButton.onClick.AddListener(OnCounterButtonClickedHandler);
                counterButton.gameObject.SetActive(!_isPlayerLandlord);
                counterButton.interactable = false;
            }

            // 初始化冷却完成回调
            if (counterCoolDown != null)
            {
                _onCounterCoolDownComplete = () => SetCounterButtonState(true);
                counterCoolDown.OnCoolDownComplete += _onCounterCoolDownComplete;
            }

            // 初始状态
            _currentDomainState = stateDefault;
            _currentCounterState = stateDefault;
            if (domainButtonEffect != null)
                domainButtonEffect.SetState(stateDefault);
            if (counterButtonEffect != null)
                counterButtonEffect.SetState(stateCooldown);

            // 初始隐藏覆盖层
            HideOverlay();
        }

        private void OnDestroy()
        {
            if (counterCoolDown != null && _onCounterCoolDownComplete != null)
                counterCoolDown.OnCoolDownComplete -= _onCounterCoolDownComplete;
            if (_domainSystem != null)
            {
                _domainSystem.OnDomainActivated -= OnDomainActivated;
                _domainSystem.OnDomainDeactivated -= OnDomainDeactivated;
                _domainSystem.OnCounterShieldActivated -= OnCounterShieldActivated;
                _domainSystem.OnCounterShieldDeactivated -= OnCounterShieldDeactivated;
            }
        }

        #endregion

        #region 每帧更新

        private void Update()
        {
            if (_domainSystem == null) return;

            UpdateOverlayTimer();
            UpdateDomainButton();
            UpdateCounterButton();
            SyncCoolDownProgress();
        }

        /// <summary>更新覆盖层倒计时文字。</summary>
        private void UpdateOverlayTimer()
        {
            if (timerText == null) return;

            float remaining = 0f;
            if (_domainSystem.IsDomainActive)
                remaining = _domainSystem.DomainTimeRemaining;
            else if (_domainSystem.IsCounterShieldActive)
                remaining = _domainSystem.CounterShieldTimeRemaining;

            timerText.text = remaining > 0 ? $"{remaining:F1}s" : "";
        }

        /// <summary>更新领域按钮状态 + 文字。</summary>
        private void UpdateDomainButton()
        {
            if (domainButtonEffect == null) return;

            bool isCooling = _domainSystem.IsDomainOnCooldown;
            bool isActive = _domainSystem.IsDomainActive;
            bool isPending = _domainSystem.IsDomainPending;

            // ButtonEffect 状态
            string targetState;
            if (isCooling || isActive)
                targetState = stateCooldown;
            else if (isPending)
                targetState = statePending;
            else
                targetState = stateDefault;

            string actualState = domainButtonEffect.CurrentStateName;
            if (_currentDomainState != targetState || actualState != targetState)
            {
                _currentDomainState = targetState;
                domainButtonEffect.SetState(targetState);
            }

            // 动态文字
            if (_domainBtnText != null)
            {
                if (!_domainBtnText.gameObject.activeSelf)
                    _domainBtnText.gameObject.SetActive(true);

                if (isCooling)
                {
                    int sec = Mathf.CeilToInt(_domainSystem.DomainCooldownRemaining);
                    _domainBtnText.text = sec > 0 ? $"{sec}s" : "开启领域";
                }
                else if (isPending)
                    _domainBtnText.text = "领域待激活";
                else
                    _domainBtnText.text = "开启领域";
            }
        }

        /// <summary>更新反击按钮可见性 + interactable + ButtonEffect 状态 + 文字。</summary>
        private void UpdateCounterButton()
        {
            // 地主视角隐藏反击按钮
            if (_isPlayerLandlord)
            {
                counterButton?.gameObject.SetActive(false);
                return;
            }

            // 可见性
            counterButton?.gameObject.SetActive(true);

            // interactable
            bool canCounter = _domainSystem.IsCounterPending
                           || (_domainSystem.IsDomainActive && !_domainSystem.IsCounterShieldOnCooldown);
            if (counterButton != null)
                counterButton.interactable = canCounter;

            // ButtonEffect 状态
            if (counterButtonEffect != null)
            {
                bool isCooling = _domainSystem.IsCounterShieldOnCooldown;
                bool isActive = _domainSystem.IsCounterShieldActive;
                bool isPending = _domainSystem.IsCounterPending;

                string counterTarget;
                if (isCooling || isActive)
                    counterTarget = stateCooldown;
                else if (isPending)
                    counterTarget = statePending;
                else if (_domainSystem.IsDomainActive)
                    counterTarget = stateDefault;
                else
                    counterTarget = stateCooldown;

                string counterActual = counterButtonEffect.CurrentStateName;
                if (_currentCounterState != counterTarget || counterActual != counterTarget)
                {
                    _currentCounterState = counterTarget;
                    counterButtonEffect.SetState(counterTarget);
                }
            }

            // 动态文字
            if (_counterBtnText != null)
            {
                if (!_counterBtnText.gameObject.activeSelf)
                    _counterBtnText.gameObject.SetActive(true);

                if (_domainSystem.IsCounterPending)
                    _counterBtnText.text = "反制待激活";
                else if (_domainSystem.IsCounterShieldActive)
                    _counterBtnText.text = "反制生效中";
                else if (_domainSystem.IsCounterShieldOnCooldown)
                {
                    int sec = Mathf.CeilToInt(_domainSystem.CounterShieldCooldownRemaining);
                    _counterBtnText.text = sec > 0 ? $"冷却 {sec}s" : "反击";
                }
                else if (_domainSystem.IsDomainActive)
                    _counterBtnText.text = "反击";
                else
                    _counterBtnText.text = "反击（等待领域）";
            }
        }

        /// <summary>同步冷却进度条。</summary>
        private void SyncCoolDownProgress()
        {
            if (_domainSystem.IsDomainOnCooldown)
            {
                float remaining = _domainSystem.DomainCooldownRemaining;
                float total = _domainSystem.DomainCooldown;
                domainCoolDown?.SetProgress(remaining, total);
            }

            if (_domainSystem.IsCounterShieldOnCooldown)
            {
                float remaining = _domainSystem.CounterShieldCooldownRemaining;
                float total = _domainSystem.CounterShieldCooldown;
                counterCoolDown?.SetProgress(remaining, total);
            }
        }

        #endregion

        #region 事件回调

        private void OnDomainActivated(CardTypeResult domainType, float duration)
        {
            if (_isPlayerLandlord)
            {
                ShowOverlay($"要不起领域已开启\n牌型: {domainType}", domainColor);
                domainCoolDown?.StartCoolDown(_domainSystem.DomainCooldown);
            }
            else
            {
                ShowOverlay($"被要不起领域封印\n牌型: {domainType}", domainColor);
            }
        }

        private void OnDomainDeactivated()
        {
            // 反制护盾即将激活时，不隐藏覆盖层
            if (_domainSystem != null && _domainSystem.IsCounterShieldActive)
            {
                ShowCounterShieldOverlay(_domainSystem.CurrentCounterType);
                return;
            }
            HideOverlay();
        }

        private void OnCounterShieldActivated(CardTypeResult counterType, float duration)
        {
            ShowCounterShieldOverlay(counterType);
            if (!_isPlayerLandlord)
            {
                counterCoolDown?.StartCoolDown(_domainSystem.CounterShieldCooldown);
                SetCounterButtonState(false);
            }
        }

        private void OnCounterShieldDeactivated()
        {
            HideOverlay();
        }

        private void OnCounterButtonClickedHandler()
        {
            if (_domainSystem == null) return;

            // 先触发事件（联机模式由 GameBootstrapper 处理网络同步）
            OnCounterButtonClicked?.Invoke();

            // 设置本地标记（反制已点击，DomainSystem 需要此标记区分玩家/AI）
            _domainSystem.SetPlayerClickedCounter();

            // 如果没有订阅者（单机模式），直接设置 pending 状态
            // 有订阅者时，pending 状态由 FusionGameManager 处理
            if (OnCounterButtonClicked == null)
            {
                if (_domainSystem.IsCounterPending)
                    _domainSystem.CancelPending();
                else
                    _domainSystem.SetCounterPending();
            }
        }

        #endregion

        #region 覆盖层控制

        private void ShowOverlay(string message, Color color)
        {
            if (overlayGroup != null)
            {
                overlayGroup.alpha = 1f;
                overlayGroup.interactable = true;
                overlayGroup.blocksRaycasts = true;
            }

            if (statusText != null)
                statusText.text = message;

            if (sealEffectImage != null)
            {
                sealEffectImage.color = color;
                sealEffectImage.gameObject.SetActive(true);
            }
        }

        private void HideOverlay()
        {
            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
                overlayGroup.interactable = false;
                overlayGroup.blocksRaycasts = false;
            }

            if (sealEffectImage != null)
                sealEffectImage.gameObject.SetActive(false);
        }

        private void ShowCounterShieldOverlay(CardTypeResult counterType)
        {
            string message = !_isPlayerLandlord
                ? $"反制护盾生效\n牌型: {counterType}"
                : $"被反制护盾封印\n牌型: {counterType}";
            ShowOverlay(message, counterColor);
        }

        #endregion

        #region 辅助方法

        private void SetCounterButtonState(bool available)
        {
            if (counterButtonEffect == null) return;
            counterButtonEffect.SetState(available ? stateDefault : stateCooldown);
        }

        #endregion
    }
}

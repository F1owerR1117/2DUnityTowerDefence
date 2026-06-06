using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Components
{
    /// <summary>
    /// 通用按钮状态管理器 + 悬停/按下反馈。
    ///
    /// 通过自定义状态（如 "default"、"pending"、"cooldown"）控制按钮的颜色和可交互性，
    /// 绕过 Button.colors 的内置状态机，避免 EventSystem 选中/取消选中导致颜色闪烁。
    /// 悬停放大和按下缩小效果在所有状态下均生效。
    ///
    /// 使用方法：
    /// 1. 挂载到带 Button + Image 的 GameObject 上
    /// 2. 在 Inspector 中配置 states 数组，每个状态指定名称、颜色、是否可交互
    /// 3. 代码中调用 SetState("xxx") 切换状态
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonEffect : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Serializable]
        public class ButtonVisualState
        {
            [Tooltip("状态名称，用于 SetState 调用")]
            public string stateName = "default";

            [Tooltip("该状态下的按钮颜色")]
            public Color color = Color.white;

            [Tooltip("该状态下按钮是否可交互")]
            public bool interactable = true;

            [Tooltip("该状态下的按钮文字（为空则不修改）")]
            public string text;
        }

        [Header("悬停效果")]
        [SerializeField] private float hoverScale = 1.08f;

        [Header("按下效果")]
        [SerializeField] private float pressScale = 0.92f;

        [Header("动画速度")]
        [SerializeField] private float animSpeed = 14f;

        [Header("状态配置")]
        [Tooltip("定义所有视觉状态，第一个为默认状态")]
        [SerializeField] private ButtonVisualState[] states = new[]
        {
            new ButtonVisualState { stateName = "default", color = Color.white, interactable = true }
        };

        private Button _button;
        private Image _image;
        private TextMeshProUGUI _text;
        private Dictionary<string, ButtonVisualState> _stateMap;
        private ButtonVisualState _currentState;

        private Vector3 _baseScale;
        private Vector3 _targetScale;
        private bool _isHovering;
        private bool _isPressed;

        /// <summary>当前状态名称</summary>
        public string CurrentStateName => _currentState?.stateName;

        /// <summary>状态切换事件（旧状态名, 新状态名）</summary>
        public event Action<string, string> OnStateChanged;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
            _text = GetComponentInChildren<TextMeshProUGUI>();

            _baseScale = transform.localScale;
            _targetScale = _baseScale;

            // 构建状态查找表
            _stateMap = new Dictionary<string, ButtonVisualState>(states.Length);
            foreach (var state in states)
            {
                if (!_stateMap.ContainsKey(state.stateName))
                    _stateMap[state.stateName] = state;
            }

            // 将 default 状态的颜色同步为按钮 Image 的实际颜色，
            // 保证"恢复默认"时一定回到按钮的原始外观。
            if (_image != null && _stateMap.TryGetValue("default", out var defaultState))
                defaultState.color = _image.color;

            // 默认使用第一个状态
            _currentState = states.Length > 0 ? states[0] : null;
            ApplyState(_currentState);
        }

        /// <summary>
        /// 切换到指定名称的状态。
        /// </summary>
        public void SetState(string stateName)
        {
            if (_stateMap == null) return;

            if (!_stateMap.TryGetValue(stateName, out var newState))
            {
                Debug.LogWarning($"[ButtonEffect] State '{stateName}' not found on {gameObject.name}");
                return;
            }

            var oldName = _currentState?.stateName;
            _currentState = newState;
            ApplyState(newState);

            if (oldName != stateName)
                OnStateChanged?.Invoke(oldName, stateName);
        }

        /// <summary>
        /// 动态注册或覆盖一个状态（运行时添加新状态）。
        /// </summary>
        public void RegisterState(ButtonVisualState state)
        {
            if (_stateMap == null) _stateMap = new Dictionary<string, ButtonVisualState>();
            _stateMap[state.stateName] = state;
        }

        /// <summary>
        /// 更新当前状态的视觉属性（不切换状态，只刷新显示）。
        /// 适用于同一个状态下颜色需要动态变化的场景（如冷却倒计时渐变）。
        /// </summary>
        public void RefreshCurrentState()
        {
            ApplyState(_currentState);
        }

        /// <summary>
        /// 直接覆盖当前状态的颜色（不改变状态配置，仅修改当前显示）。
        /// 下次 SetState 时会被重置。
        /// </summary>
        public void SetColorOverride(Color color)
        {
            if (_image != null) _image.color = color;
        }

        private void ApplyState(ButtonVisualState state)
        {
            if (state == null) return;

            // 颜色：直接操作 Image.color，绕过 Button.colors 状态机
            if (_image != null)
                _image.color = state.color;

            // 可交互性
            if (_button != null)
                _button.interactable = state.interactable;

            // 文字
            if (!string.IsNullOrEmpty(state.text) && _text != null)
                _text.text = state.text;

            // 不可交互时重置缩放
            if (!state.interactable)
            {
                _isHovering = false;
                _isPressed = false;
                _targetScale = _baseScale;
            }
        }

        // ── 悬停 / 按下动画 ──

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * animSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            _isHovering = true;
            if (!_isPressed)
                _targetScale = _baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            if (!_isPressed)
                _targetScale = _baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            _isPressed = true;
            _targetScale = _baseScale * pressScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            _targetScale = _isHovering ? _baseScale * hoverScale : _baseScale;
        }

        private void OnDisable()
        {
            _isHovering = false;
            _isPressed = false;
            transform.localScale = _baseScale;
            _targetScale = _baseScale;
        }
    }
}

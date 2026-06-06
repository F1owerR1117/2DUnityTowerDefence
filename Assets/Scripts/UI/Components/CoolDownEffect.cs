using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Components
{
    /// <summary>
    /// 冷却效果组件：实现类似英雄联盟的时钟式冷却动画。
    /// 使用 Image.fillAmount 实现圆形填充效果。
    ///
    /// 使用方法：
    /// 1. 创建 UI Image，挂载此脚本
    /// 2. 设置 Image Type 为 Filled，Fill Method 为 Radial 360
    /// 3. 调用 StartCoolDown() 开始冷却
    ///
    /// 支持：
    /// - 顺时针/逆时针旋转
    /// - 剩余时间显示
    /// - 冷却完成回调
    /// - 可选的闪光效果
    /// </summary>
    public class CoolDownEffect : MonoBehaviour
    {
        #region 配置

        [Header("UI 引用")]
        [Tooltip("冷却遮罩 Image（设置为 Filled, Radial 360）")]
        [SerializeField] private Image coolDownImage;

        [Tooltip("剩余时间文本（可选）")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("冷却设置")]
        [Tooltip("冷却颜色")]
        [SerializeField] private Color coolDownColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        [Tooltip("完成时是否闪烁")]
        [SerializeField] private bool flashOnComplete = true;

        [Tooltip("闪烁颜色")]
        [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 0.5f);

        [Tooltip("闪烁持续时间")]
        [SerializeField] private float flashDuration = 0.3f;

        #endregion

        #region 私有字段

        private float _totalDuration;
        private float _remainingTime;
        private bool _isCoolingDown;
        private bool _isPaused;

        // 闪烁效果
        private float _flashTimer;
        private bool _isFlashing;
        private Image _targetImage;

        #endregion

        #region 公共属性

        /// <summary>是否正在冷却中</summary>
        public bool IsCoolingDown => _isCoolingDown;

        /// <summary>剩余冷却时间</summary>
        public float RemainingTime => _remainingTime;

        /// <summary>冷却进度（0=完成，1=刚开始）</summary>
        public float Progress => _totalDuration > 0 ? _remainingTime / _totalDuration : 0f;

        /// <summary>是否暂停</summary>
        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        #endregion

        #region 事件

        /// <summary>冷却开始事件</summary>
        public event Action OnCoolDownStart;

        /// <summary>冷却完成事件</summary>
        public event Action OnCoolDownComplete;

        /// <summary>冷却进度更新事件（参数：剩余时间，总时间）</summary>
        public event Action<float, float> OnCoolDownUpdate;

        #endregion

        #region 生命周期

        private void Awake()
        {
            // 获取目标 Image
            _targetImage = GetComponent<Image>();

            // 初始化冷却 Image
            if (coolDownImage != null)
            {
                coolDownImage.type = Image.Type.Filled;
                coolDownImage.fillMethod = Image.FillMethod.Radial360;
                coolDownImage.fillOrigin = (int)Image.Origin360.Top;
                coolDownImage.fillClockwise = true;
                coolDownImage.fillAmount = 0f;
                coolDownImage.color = coolDownColor;
            }
        }

        private void Update()
        {
            if (!_isCoolingDown || _isPaused) return;

            // 更新冷却时间
            _remainingTime -= Time.deltaTime;
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                CompleteCoolDown();
                return;
            }

            // 更新视觉效果
            UpdateVisual();
            OnCoolDownUpdate?.Invoke(_remainingTime, _totalDuration);

            // 更新闪烁效果
            if (_isFlashing)
            {
                UpdateFlash();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始冷却。
        /// </summary>
        /// <param name="duration">冷却持续时间（秒）</param>
        public void StartCoolDown(float duration)
        {
            if (duration <= 0f) return;

            _totalDuration = duration;
            _remainingTime = duration;
            _isCoolingDown = true;
            _isPaused = false;

            // 显示冷却效果
            if (coolDownImage != null)
            {
                coolDownImage.gameObject.SetActive(true);
                coolDownImage.fillAmount = 1f;
                coolDownImage.color = coolDownColor;
            }

            // 更新视觉
            UpdateVisual();

            // 触发事件
            OnCoolDownStart?.Invoke();
        }

        /// <summary>
        /// 停止冷却（立即完成）。
        /// </summary>
        public void StopCoolDown()
        {
            if (!_isCoolingDown) return;

            _remainingTime = 0f;
            CompleteCoolDown();
        }

        /// <summary>
        /// 增加冷却时间（延长冷却）。
        /// </summary>
        /// <param name="time">增加的时间</param>
        public void AddTime(float time)
        {
            if (!_isCoolingDown) return;

            _remainingTime += time;
            _totalDuration += time;
            UpdateVisual();
        }

        /// <summary>
        /// 减少冷却时间（缩短冷却）。
        /// </summary>
        /// <param name="time">减少的时间</param>
        public void ReduceTime(float time)
        {
            if (!_isCoolingDown) return;

            _remainingTime -= time;
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                CompleteCoolDown();
            }
            else
            {
                UpdateVisual();
            }
        }

        /// <summary>
        /// 设置冷却进度（用于同步）。
        /// </summary>
        /// <param name="remaining">剩余时间</param>
        /// <param name="total">总时间</param>
        public void SetProgress(float remaining, float total)
        {
            _remainingTime = remaining;
            _totalDuration = total;
            _isCoolingDown = remaining > 0f;

            if (_isCoolingDown)
            {
                UpdateVisual();
                if (coolDownImage != null)
                    coolDownImage.gameObject.SetActive(true);
            }
            else
            {
                HideCoolDown();
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 更新视觉效果。
        /// </summary>
        private void UpdateVisual()
        {
            // 更新 fillAmount（0=完成，1=刚开始）
            if (coolDownImage != null)
            {
                float progress = Progress;
                coolDownImage.fillAmount = progress;
            }

            // 更新时间文本
            if (timerText != null)
            {
                if (_remainingTime > 0f)
                {
                    timerText.gameObject.SetActive(true);
                    timerText.text = Mathf.CeilToInt(_remainingTime).ToString();
                }
                else
                {
                    timerText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 完成冷却。
        /// </summary>
        private void CompleteCoolDown()
        {
            _isCoolingDown = false;

            // 隐藏冷却效果
            HideCoolDown();

            // 触发闪烁效果
            if (flashOnComplete)
            {
                StartFlash();
            }

            // 触发完成事件
            OnCoolDownComplete?.Invoke();
        }

        /// <summary>
        /// 隐藏冷却效果。
        /// </summary>
        private void HideCoolDown()
        {
            if (coolDownImage != null)
                coolDownImage.gameObject.SetActive(false);

            if (timerText != null)
                timerText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 开始闪烁效果。
        /// </summary>
        private void StartFlash()
        {
            if (_targetImage == null) return;

            _isFlashing = true;
            _flashTimer = flashDuration;
            _targetImage.color = flashColor;
        }

        /// <summary>
        /// 更新闪烁效果。
        /// </summary>
        private void UpdateFlash()
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _isFlashing = false;
                if (_targetImage != null)
                    _targetImage.color = Color.white;
            }
            else
            {
                // 闪烁渐变
                float t = _flashTimer / flashDuration;
                if (_targetImage != null)
                    _targetImage.color = Color.Lerp(Color.white, flashColor, t);
            }
        }

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 创建冷却效果实例。
        /// </summary>
        /// <param name="parent">父物体</param>
        /// <param name="size">大小</param>
        /// <returns>CoolDownEffect 实例</returns>
        public static CoolDownEffect Create(Transform parent, Vector2 size)
        {
            // 创建根物体
            var go = new GameObject("CoolDownEffect");
            go.transform.SetParent(parent, false);

            // 添加 Image 组件
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;

            // 设置大小
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            // 添加 CoolDownEffect 组件
            var effect = go.AddComponent<CoolDownEffect>();
            effect.coolDownImage = image;

            // 创建时间文本
            var textGo = new GameObject("TimerText");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 24;
            text.text = "0";
            effect.timerText = text;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return effect;
        }

        #endregion
    }
}
